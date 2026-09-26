// logbook/specs/transport-probe: does a card kernel carry the snow's transport with the CPU's bits?
// Built and run from a copy under scratch/transport-probe/ (a build here would leave bin/ and obj/ in
// the record) with this project file beside it:
//
//   <Project Sdk="Microsoft.NET.Sdk">
//     <PropertyGroup>
//       <OutputType>Exe</OutputType>
//       <TargetFramework>net8.0</TargetFramework>
//       <Nullable>disable</Nullable>
//       <TieredCompilation>false</TieredCompilation>
//       <InvariantGlobalization>true</InvariantGlobalization>
//       <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
//     </PropertyGroup>
//     <ItemGroup>
//       <PackageReference Include="ILGPU" Version="1.5.3" />
//       <PackageReference Include="ILGPU.Algorithms" Version="1.5.3" />
//       <ProjectReference Include="../../src/Evosim.Core/Evosim.Core.csproj" />
//     </ItemGroup>
//   </Project>
//
// Results on 2026-09-26, round 50's tank (r50smoke-s1's config: 168 x 93 x 168 cells of 1 m, the bed
// with its beach's fade, the reefs, the overturning on), the RTX 4090, Core's threads at 16:
//   Stage 1, the edges: 0 of 7,993,869 differ at five instants (3, 4,000, 4,000.25, 12,345.5 and
//   27,799.75 s) at group sizes 32 and 256. The CPU's SampleEdges took 35 to 38 ms; the card's three
//   kernels 3.3 to 7.6 ms with their launches and the wait.
//   Stage 2, whole steps from a synthetic stock: 0 of 2,624,832 cells differ over three steps of
//   0.5 s, one of which the outflow bound split into two substeps. The CPU's Advect took 48 to 51 ms
//   (93 with two substeps); the card 5.0 to 5.2 ms warm without the stock's copies. Steps of 6, 12
//   and 20 s were refused by the CPU's a-priori Courant check before either path ran.
//   The copies: the stock up and back took 6.1 to 6.4 ms from a pageable array and 1.87 to 1.96 ms
//   from the same array pinned and registered as page-locked, and 1.90 ms through the plain
//   CopyFromCPU and CopyToCPU once it was registered: the driver sees the range.
// The farm's own version is src/Evosim.Farm.Gpu/GpuTransport.cs, behind EVOSIM_GPU_TRANSPORT.
using System;
using System.Diagnostics;
using System.Reflection;
using Evosim.Core;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

public struct Scalars
{
    public int Component, Nx, Ny, Nz, Stride, Step, HasBed, FadeOn, HasReef, Overturning;
    public double H, DepthMetres;
    public float Scale;
}

public static class Probe
{
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    static object Get(object o, string name)
    {
        FieldInfo f = o.GetType().GetField(name, Any);
        if (f == null) throw new MissingFieldException(o.GetType().Name, name);
        return f.GetValue(o);
    }

    static double M(double p, double q)
    {
        CudaAsm.Emit("mul.rn.f64 %0, %1, %2;", out double r, p, q);
        return r;
    }

    static float MF(float p, float q)
    {
        CudaAsm.Emit("mul.rn.f32 %0, %1, %2;", out float r, p, q);
        return r;
    }

    // CurrentField.Eddies, the same products in the same grouping.
    static double Eddies(double ay, double cosM, double sinM, double f, int k0,
        double s1, double s2, double s3, ArrayView<double> inst)
    {
        for (int q = 0; q < 3; q++)
        {
            int k = k0 + q;
            double profile = q == 0 ? s1 : q == 1 ? s2 : s3;
            double amplitude = M(inst[48 + k], profile);
            double cosChi = M(cosM, inst[k]) - M(sinM, inst[24 + k]);
            ay = ay - M(M(amplitude, f), cosChi);
        }
        return ay;
    }

    static void EdgeKernel(Index1D idx, ArrayView<double> cols, ArrayView<double> deps,
        ArrayView<double> bed, ArrayView<byte> open, ArrayView<float> reef, ArrayView<double> inst,
        ArrayView<double> edges, Scalars s)
    {
        int e = idx;
        int nx = s.Nx, ny = s.Ny, nz = s.Nz;
        double h = s.H;
        int c, depthIdx;
        float y;

        if (s.Component == 0)
        {
            int nzp = nz + 1;
            int k = e % nzp; int t = e / nzp; int i = t % nx; int j = t / nx;
            if (j == 0 || j == ny) { edges[e] = 0d; return; }
            c = i * nzp + k;
            depthIdx = (j - 1) * s.Stride + c * s.Step;
            y = -(float)M(j, h);
        }
        else if (s.Component == 2)
        {
            int k = e % nz; int t = e / nz; int i = t % (nx + 1); int j = t / (nx + 1);
            if (j == 0 || j == ny) { edges[e] = 0d; return; }
            c = i * nz + k;
            depthIdx = (j - 1) * s.Stride + c * s.Step;
            y = -(float)M(j, h);
        }
        else
        {
            int nzp = nz + 1;
            int k = e % nzp; int t = e / nzp; int i = t % (nx + 1); int j = t / (nx + 1);
            c = i * nzp + k;
            depthIdx = j * s.Stride + c * s.Step;
            y = -(float)M(j + 0.5, h);
        }

        if (open[e] == 0) { edges[e] = 0d; return; }

        // StreamsPotentialFrom.
        int cb = c * 19;
        int db = depthIdx * 3;
        float ax = 0f, ayf = 0f, az = 0f;
        if (deps[db] == 0d)
        {
            double sinPhi = deps[db + 1], cosPhi = deps[db + 2];
            double s1 = sinPhi;
            double s2 = M(M(2d, sinPhi), cosPhi);
            double s3 = M(sinPhi, M(M(4d, cosPhi), cosPhi) - 1d);
            double ay = 0d;
            ay = Eddies(ay, cols[cb + 3], cols[cb + 7], cols[cb + 11], 0, s1, s2, s3, inst);
            ay = Eddies(ay, cols[cb + 3], cols[cb + 7], cols[cb + 12], 3, s1, s2, s3, inst);
            ay = Eddies(ay, cols[cb + 4], cols[cb + 8], cols[cb + 13], 6, s1, s2, s3, inst);
            ay = Eddies(ay, cols[cb + 4], cols[cb + 8], cols[cb + 14], 9, s1, s2, s3, inst);
            ay = Eddies(ay, cols[cb + 5], cols[cb + 9], cols[cb + 15], 12, s1, s2, s3, inst);
            ay = Eddies(ay, cols[cb + 5], cols[cb + 9], cols[cb + 16], 15, s1, s2, s3, inst);
            ay = Eddies(ay, cols[cb + 6], cols[cb + 10], cols[cb + 17], 18, s1, s2, s3, inst);
            ay = Eddies(ay, cols[cb + 6], cols[cb + 10], cols[cb + 18], 21, s1, s2, s3, inst);
            double w = 0d;
            if (s.Overturning != 0)
            {
                double wall = cols[cb + 2];
                for (int q = 0; q < 3; q++)
                {
                    double sinKy = q == 0 ? s1 : q == 1 ? s2 : s3;
                    w = w + M(M(M(inst[72 + q], wall), wall), sinKy);
                }
            }
            ax = (float)M(w, cols[cb + 1]);
            ayf = (float)ay;
            az = (float)M(-w, cols[cb]);
        }

        float px, py, pz;
        if (s.HasBed != 0)
        {
            int bb = c * 6;
            double yy = y;
            if (yy > 0d) yy = 0d;
            else if (yy < bed[bb]) yy = bed[bb];
            double cc = bed[bb + 1];
            double scale = M(yy, s.DepthMetres) / bed[bb + 2];
            double a = M(scale, bed[bb + 3]);
            double b = M(scale, bed[bb + 4]);
            if (s.FadeOn != 0)
            {
                double f = bed[bb + 5];
                if (f == 0d) { px = 0f; py = 0f; pz = 0f; }
                else
                {
                    px = (float)M(f, (double)ax + M(a, (double)ayf));
                    py = (float)M(f, M(cc, (double)ayf));
                    pz = (float)M(f, (double)az + M(b, (double)ayf));
                    px = MF(px, s.Scale); py = MF(py, s.Scale); pz = MF(pz, s.Scale);
                }
            }
            else
            {
                px = (float)((double)ax + M(a, (double)ayf));
                py = (float)M(cc, (double)ayf);
                pz = (float)((double)az + M(b, (double)ayf));
                px = MF(px, s.Scale); py = MF(py, s.Scale); pz = MF(pz, s.Scale);
            }
        }
        else
        {
            px = MF(ax, s.Scale); py = MF(ayf, s.Scale); pz = MF(az, s.Scale);
        }

        float value = s.Component == 0 ? px : s.Component == 1 ? py : pz;
        if (s.HasReef != 0) value = MF(value, reef[e]);
        edges[e] = M(value, h);
    }


    public struct Grid3
    {
        public int Nx, Ny, Nz, Tank, Split, LayerStride;
    }

    // GridField.AssembleFaces, one cell a thread.
    static void AssembleKernel(Index1D idx, ArrayView<double> ex, ArrayView<double> ey, ArrayView<double> ez,
        ArrayView<byte> live, ArrayView<int> lowest, ArrayView<double> fx, ArrayView<double> fy,
        ArrayView<double> fz, Grid3 g)
    {
        int cell = idx;
        int nx = g.Nx, ny = g.Ny, nz = g.Nz;
        int iz = cell % nz; int t = cell / nz; int ix = t % nx; int iy = t / nx;
        if (live[cell] == 0) { fx[cell] = 0d; fy[cell] = 0d; fz[cell] = 0d; return; }
        int row = (iy * nx + ix) * nz;
        int lowRow = ix * nz;
        int exHere = (iy * nx + ix) * (nz + 1);
        int exBelow = ((iy + 1) * nx + ix) * (nz + 1);
        int eyWest = (iy * (nx + 1) + ix) * (nz + 1);
        int eyEast = eyWest + (nz + 1);
        int ezBelowWest = ((iy + 1) * (nx + 1) + ix) * nz;
        int ezBelowEast = ezBelowWest + nz;
        int ezEast = (iy * (nx + 1) + ix) * nz + nz;
        int eastRow = -1;
        if (nx >= 2)
        {
            int next = ix + 1;
            if (next == nx) next = g.Tank != 0 ? -1 : 0;
            if (next >= 0) eastRow = (iy * nx + next) * nz;
        }
        int east = eastRow < 0 ? -1 : eastRow + iz;
        if (east >= 0 && live[east] == 0) east = -1;
        fx[cell] = east >= 0
            ? ey[eyEast + iz] + ez[ezEast + iz] - ey[eyEast + iz + 1] - ez[ezBelowEast + iz]
            : 0d;
        int front = -1;
        if (nz >= 2)
        {
            int next = iz + 1;
            if (next == nz) next = g.Tank != 0 ? -1 : 0;
            if (next >= 0)
            {
                front = row + next;
                if (live[front] == 0) front = -1;
            }
        }
        fz[cell] = front >= 0
            ? ex[exBelow + iz + 1] + ey[eyEast + iz + 1] - ex[exHere + iz + 1] - ey[eyWest + iz + 1]
            : 0d;
        fy[cell] = ny >= 2 && iy < lowest[lowRow + iz] && (g.Split == 0 || live[cell + g.LayerStride] != 0)
            ? ez[ezBelowEast + iz] + ex[exBelow + iz] - ez[ezBelowWest + iz] - ex[exBelow + iz + 1]
            : 0d;
    }

    // GridField.LargestOutflowFraction's inner loop, one (iy, ix) row a thread; the max over rows is
    // taken on the host, and a max is the same whatever the order.
    static void OutflowKernel(Index1D idx, ArrayView<byte> live, ArrayView<double> fx, ArrayView<double> fy,
        ArrayView<double> fz, ArrayView<double> rowMax, Grid3 g, double scale)
    {
        int r = idx;
        int nx = g.Nx, nz = g.Nz;
        int ix = r % nx; int iy = r / nx;
        bool tank = g.Tank != 0;
        int row = (iy * nx + ix) * nz;
        int above = row - nx * nz;
        int west = ix == 0 ? (tank ? -1 : nx - 1) : ix - 1;
        int westRow = west < 0 ? -1 : (iy * nx + west) * nz;
        int backOfFirst = tank ? -1 : nz - 1;
        double largest = 0d;
        for (int iz = 0; iz < nz; iz++)
        {
            int cell = row + iz;
            if (live[cell] == 0) continue;
            double sum = Math.Abs(fx[cell]) + Math.Abs(fz[cell]) + Math.Abs(fy[cell]);
            if (westRow >= 0) sum += Math.Abs(fx[westRow + iz]);
            int back = iz == 0 ? backOfFirst : iz - 1;
            if (back >= 0) sum += Math.Abs(fz[row + back]);
            if (iy > 0) sum += Math.Abs(fy[above + iz]);
            double outflow = M(sum, scale);
            if (outflow > largest) largest = outflow;
        }
        rowMax[r] = largest;
    }

    // GridField.ApplyFaces' flux loop, one cell a thread.
    static void FluxKernel(Index1D idx, ArrayView<byte> live, ArrayView<double> stock, ArrayView<double> fx,
        ArrayView<double> fy, ArrayView<double> fz, ArrayView<double> qx, ArrayView<double> qy,
        ArrayView<double> qz, Grid3 g, double scale)
    {
        int cell = idx;
        int nx = g.Nx, nz = g.Nz;
        int iz = cell % nz; int t = cell / nz; int ix = t % nx; int iy = t / nx;
        if (live[cell] == 0) { qx[cell] = 0d; qy[cell] = 0d; qz[cell] = 0d; return; }
        bool wraps = g.Tank == 0;
        int row = (iy * nx + ix) * nz;
        int below = row + nx * nz;
        int nextX = ix + 1;
        if (nextX == nx) nextX = wraps ? 0 : -1;
        int eastRow = nextX < 0 ? -1 : (iy * nx + nextX) * nz;
        double q = fx[cell];
        if (q != 0d)
        {
            int east = eastRow < 0 ? -1 : eastRow + iz;
            if (east >= 0 && live[east] == 0) east = -1;
            double sv = q > 0d ? stock[cell] : stock[east < 0 ? cell : east];
            qx[cell] = M(M(sv, q), scale);
        }
        else qx[cell] = 0d;
        q = fz[cell];
        if (q != 0d)
        {
            int nextZ = iz + 1;
            if (nextZ == nz) nextZ = wraps ? 0 : -1;
            int front = nextZ < 0 ? -1 : row + nextZ;
            if (front >= 0 && live[front] == 0) front = -1;
            double sv = q > 0d ? stock[cell] : stock[front < 0 ? cell : front];
            qz[cell] = M(M(sv, q), scale);
        }
        else qz[cell] = 0d;
        q = fy[cell];
        qy[cell] = q != 0d ? M(M(q > 0d ? stock[cell] : stock[below + iz], q), scale) : 0d;
    }

    // ApplyEast, ApplyDown and ApplyFront fused: each pass only moves precomputed fluxes, so a cell's
    // result is its own stock with the six fluxes added and taken in the passes' order. Within a pass
    // the upstream neighbour's flux arrives before the cell's own leaves, except at a wrap, where the
    // cell's own leaves first.
    static void ApplyKernel(Index1D idx, ArrayView<double> stock, ArrayView<double> qx, ArrayView<double> qy,
        ArrayView<double> qz, Grid3 g)
    {
        int cell = idx;
        int nx = g.Nx, ny = g.Ny, nz = g.Nz;
        int iz = cell % nz; int t = cell / nz; int ix = t % nx; int iy = t / nx;
        int layer = nx * nz;
        double s = stock[cell];
        double f;
        if (ix > 0)
        {
            f = qx[cell - nz]; if (f != 0d) s = s + f;
            f = qx[cell]; if (f != 0d) s = s - f;
        }
        else
        {
            f = qx[cell]; if (f != 0d) s = s - f;
            f = qx[cell + (nx - 1) * nz]; if (f != 0d) s = s + f;
        }
        if (iy > 0) { f = qy[cell - layer]; if (f != 0d) s = s + f; }
        if (iy < ny - 1) { f = qy[cell]; if (f != 0d) s = s - f; }
        if (iz > 0)
        {
            f = qz[cell - 1]; if (f != 0d) s = s + f;
            f = qz[cell]; if (f != 0d) s = s - f;
        }
        else
        {
            f = qz[cell]; if (f != 0d) s = s - f;
            f = qz[cell + nz - 1]; if (f != 0d) s = s + f;
        }
        stock[cell] = s;
    }
    static double[] FlattenColumns(CurrentField.StreamsColumn[] a)
    {
        var o = new double[a.Length * 19];
        for (int i = 0; i < a.Length; i++)
        {
            var x = a[i];
            double[] v = { x.Dx, x.Dz, x.Wall, x.Cos1, x.Cos2, x.Cos3, x.Cos4, x.Sin1, x.Sin2, x.Sin3,
                x.Sin4, x.F11, x.F12, x.F21, x.F22, x.F31, x.F32, x.F41, x.F42 };
            Array.Copy(v, 0, o, i * 19, 19);
        }
        return o;
    }

    static double[] FlattenDepths(CurrentField.StreamsDepth[] a)
    {
        var o = new double[a.Length * 3];
        for (int i = 0; i < a.Length; i++)
        {
            o[i * 3] = a[i].AtFace ? 1d : 0d;
            o[i * 3 + 1] = a[i].SinPhi;
            o[i * 3 + 2] = a[i].CosPhi;
        }
        return o;
    }

    static double[] FlattenBed(CurrentField.BedColumn[] a, CurrentField current, bool fadeOn)
    {
        MethodInfo shore = typeof(CurrentField).GetMethod("ShoreFade", Any);
        var o = new double[a.Length * 6];
        var args = new object[4];
        for (int i = 0; i < a.Length; i++)
        {
            var x = a[i];
            double fade = 1d;
            if (fadeOn)
            {
                args[0] = x.Depth; args[1] = null; args[2] = null; args[3] = null;
                shore.Invoke(current, args);
                fade = (double)args[1];
            }
            double[] v = { x.FloorY, x.Stretch, x.DepthSquared, x.SlopeX, x.SlopeZ, fade };
            Array.Copy(v, 0, o, i * 6, 6);
        }
        return o;
    }

    static byte[] Bytes(bool[] a)
    {
        var o = new byte[a.Length];
        for (int i = 0; i < a.Length; i++) o[i] = a[i] ? (byte)1 : (byte)0;
        return o;
    }

    static int Main(string[] argv)
    {
        string runDir = argv.Length > 0 ? argv[0]
            : "D:/Projects/experiments/evolution-simulator/runs/r50smoke-s1/2026-09-26-082440-a2cda4b0";
        Parallelism.Threads = 16;
        RunConfig config = RunDirectory.ReadConfig(runDir, out string mismatch);
        if (mismatch != null) Console.WriteLine("config hash mismatch: " + mismatch);
        config.FoundingTricklePoolShare = 0f; // the pool is the bodies', not the grid's
        var world = new World(config, 1, null);
        var grid = (GridField)world.Nutrients;
        CurrentField current = config.Current;

        // One advect builds every lazy table (the face buffers, the open mask, the bed columns, the
        // streams' terms, the reefs' fades).
        grid.Advect(current, 4000.0, 0.5f, grid.PatchWidthMetres);

        int nx = (int)Get(grid, "_nx"), ny = (int)Get(grid, "_ny"), nz = (int)Get(grid, "_nz");
        double h = grid.CellMetres;
        Console.WriteLine($"grid {nx} x {ny} x {nz} at {h} m, shape {grid.Shape}");

        bool fadeOn = (bool)Get(current, "_fadeOn");
        float speed = (float)Get(current, "_speed");
        float streamsScale = (float)Get(current, "_streamsScale");
        double bedScale = (double)Get(current, "_bedScale");
        double overturning = (double)Get(current, "_streamsOverturning");
        double depthMetres = (float)Get(current, "_depthMetres");
        int step = (int)Get(grid, "_streamsDepthStep");

        string[] axes = { "X", "Y", "Z" };
        var bedArrays = new CurrentField.BedColumn[3][];
        var colArrays = new CurrentField.StreamsColumn[3][];
        var depArrays = new CurrentField.StreamsDepth[3][];
        var strides = new int[3];
        var opens = new bool[3][];
        var reefs = new float[3][];
        for (int a = 0; a < 3; a++)
        {
            bedArrays[a] = (CurrentField.BedColumn[])Get(grid, "_bedColumn" + axes[a]);
            colArrays[a] = (CurrentField.StreamsColumn[])Get(grid, "_streamsColumn" + axes[a]);
            depArrays[a] = (CurrentField.StreamsDepth[])Get(grid, "_streamsDepth" + axes[a]);
            strides[a] = (int)Get(grid, "_streamsStride" + axes[a]);
            opens[a] = (bool[])Get(grid, "_edgeOpen" + axes[a]);
            reefs[a] = (float[])Get(grid, "_edgeFade" + axes[a]);
        }
        bool hasBed = bedArrays[0] != null;
        bool hasReef = reefs[0] != null;
        if (colArrays[0] == null) { Console.WriteLine("no hoisted streams terms: not this probe's path"); return 2; }
        float scaleF = hasBed ? (float)(speed * streamsScale * bedScale) : speed * streamsScale;
        Console.WriteLine($"bed {hasBed}, shore fade {fadeOn}, reefs {hasReef}, overturning {overturning}, depth step {step}, strides {strides[0]}/{strides[1]}/{strides[2]}");

        using var context = Context.Create(b => b.Cuda().EnableAlgorithms());
        using Accelerator acc = context.CreateCudaAccelerator(0);
        Console.WriteLine("device " + acc.Name);

        var dCols = new MemoryBuffer1D<double, Stride1D.Dense>[3];
        var dDeps = new MemoryBuffer1D<double, Stride1D.Dense>[3];
        var dBed = new MemoryBuffer1D<double, Stride1D.Dense>[3];
        var dOpen = new MemoryBuffer1D<byte, Stride1D.Dense>[3];
        var dReef = new MemoryBuffer1D<float, Stride1D.Dense>[3];
        var dEdges = new MemoryBuffer1D<double, Stride1D.Dense>[3];
        var lengths = new int[3];
        var up = Stopwatch.StartNew();
        for (int a = 0; a < 3; a++)
        {
            dCols[a] = acc.Allocate1D(FlattenColumns(colArrays[a]));
            dDeps[a] = acc.Allocate1D(FlattenDepths(depArrays[a]));
            dBed[a] = acc.Allocate1D(hasBed ? FlattenBed(bedArrays[a], current, fadeOn) : new double[6]);
            dOpen[a] = acc.Allocate1D(Bytes(opens[a]));
            dReef[a] = acc.Allocate1D(hasReef ? reefs[a] : new float[1]);
            lengths[a] = opens[a].Length;
            dEdges[a] = acc.Allocate1D<double>(lengths[a]);
        }
        Console.WriteLine($"tables up in {up.ElapsedMilliseconds} ms; edges {lengths[0]} / {lengths[1]} / {lengths[2]}");
        var dInst = acc.Allocate1D<double>(75);

        MethodInfo sample = typeof(GridField).GetMethod("SampleEdges", Any, null,
            new[] { typeof(CurrentField), typeof(double) }, null);
        object instantField = null;

        foreach (int group in new[] { 32, 256 })
        {
            var kernel = acc.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>,
                ArrayView<double>, ArrayView<byte>, ArrayView<float>, ArrayView<double>, ArrayView<double>, Scalars>(
                EdgeKernel, group);

            foreach (double t in new[] { 4000.0, 4000.25, 12345.5, 27799.75, 3.0 })
            {
                current.PinInstant(t);
                instantField = Get(current, "_pinned");
                var cos = (double[])Get(instantField, "Cos");
                var sin = (double[])Get(instantField, "Sin");
                var eddy = (double[])Get(current, "_pinnedEddyBase");
                var cell = (double[])Get(current, "_pinnedCellBase");
                var inst = new double[75];
                Array.Copy(cos, 0, inst, 0, 24);
                Array.Copy(sin, 0, inst, 24, 24);
                Array.Copy(eddy, 0, inst, 48, 24);
                Array.Copy(cell, 0, inst, 72, 3);
                current.UnpinInstant();

                var cpu = Stopwatch.StartNew();
                sample.Invoke(grid, new object[] { current, t });
                double cpuMs = cpu.Elapsed.TotalMilliseconds;
                var want = new[] { ((double[])Get(grid, "_edgeX")).Clone() as double[],
                    ((double[])Get(grid, "_edgeY")).Clone() as double[],
                    ((double[])Get(grid, "_edgeZ")).Clone() as double[] };

                dInst.CopyFromCPU(inst);
                acc.Synchronize();
                var card = Stopwatch.StartNew();
                for (int a = 0; a < 3; a++)
                {
                    var s = new Scalars
                    {
                        Component = a, Nx = nx, Ny = ny, Nz = nz, Stride = strides[a], Step = step,
                        HasBed = hasBed ? 1 : 0, FadeOn = fadeOn ? 1 : 0, HasReef = hasReef ? 1 : 0,
                        Overturning = overturning != 0d ? 1 : 0, H = h, DepthMetres = depthMetres,
                        Scale = scaleF,
                    };
                    kernel(lengths[a], dCols[a].View, dDeps[a].View, dBed[a].View, dOpen[a].View,
                        dReef[a].View, dInst.View, dEdges[a].View, s);
                }
                acc.Synchronize();
                double cardMs = card.Elapsed.TotalMilliseconds;

                long diff = 0, nonzero = 0;
                string first = null;
                for (int a = 0; a < 3; a++)
                {
                    double[] got = dEdges[a].GetAsArray1D();
                    for (int e = 0; e < got.Length; e++)
                    {
                        if (want[a][e] != 0d) nonzero++;
                        if (BitConverter.DoubleToInt64Bits(got[e]) != BitConverter.DoubleToInt64Bits(want[a][e]))
                        {
                            diff++;
                            first ??= $"{axes[a]}[{e}] card {got[e]:R} cpu {want[a][e]:R}";
                        }
                    }
                }
                Console.WriteLine($"group {group} t {t}: {diff} of {lengths[0] + lengths[1] + lengths[2]} edges differ ({nonzero} nonzero); cpu {cpuMs:0.0} ms, card {cardMs:0.00} ms" + (first == null ? "" : "; first " + first));
            }
        }

        // Stage 2: the whole advect (faces, outflow, substeps, fluxes and the apply) against
        // GridField.Advect on the CPU from the same synthetic stock.
        var stockRef = (double[])Get(grid, "_stock");
        var live = (bool[])Get(grid, "_live");
        var lowestLive = (int[])Get(grid, "_lowestLive");
        bool split = (bool)Get(grid, "_splitColumns");
        int layerStride = (int)Get(grid, "_layerStride");
        if (nx < 2 || ny < 2 || nz < 2 || grid.Shape != WorldShape.Tank) { Console.WriteLine("not a tank of three wide axes"); return 3; }
        var g3 = new Grid3 { Nx = nx, Ny = ny, Nz = nz, Tank = 1, Split = split ? 1 : 0, LayerStride = layerStride };
        Console.WriteLine($"stage 2: {stockRef.Length} cells, layer stride {layerStride}, split {split}");

        var seed = new double[stockRef.Length];
        for (int i = 0; i < seed.Length; i++)
        {
            if (!live[i]) continue;
            double v = Math.Sin(i * 12.9898) * 43758.5453;
            seed[i] = 0.05 + 2.0 * (v - Math.Floor(v));
        }

        var dLive = acc.Allocate1D(Bytes(live));
        var dLowest = acc.Allocate1D(lowestLive);
        var dStock = acc.Allocate1D<double>(stockRef.Length);
        var dFx = acc.Allocate1D<double>(stockRef.Length);
        var dFy = acc.Allocate1D<double>(stockRef.Length);
        var dFz = acc.Allocate1D<double>(stockRef.Length);
        var dQx = acc.Allocate1D<double>(stockRef.Length);
        var dQy = acc.Allocate1D<double>(stockRef.Length);
        var dQz = acc.Allocate1D<double>(stockRef.Length);
        var dRowMax = acc.Allocate1D<double>(nx * ny);
        float cellVolume = grid.CellVolume;

        var edgeK = acc.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>,
            ArrayView<double>, ArrayView<byte>, ArrayView<float>, ArrayView<double>, ArrayView<double>, Scalars>(EdgeKernel, 64);
        var assembleK = acc.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>,
            ArrayView<double>, ArrayView<byte>, ArrayView<int>, ArrayView<double>, ArrayView<double>,
            ArrayView<double>, Grid3>(AssembleKernel, 64);
        var outflowK = acc.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<byte>, ArrayView<double>,
            ArrayView<double>, ArrayView<double>, ArrayView<double>, Grid3, double>(OutflowKernel, 64);
        var fluxK = acc.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<byte>, ArrayView<double>,
            ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>,
            ArrayView<double>, Grid3, double>(FluxKernel, 64);
        var applyK = acc.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>,
            ArrayView<double>, ArrayView<double>, Grid3>(ApplyKernel, 64);

        double[] InstantAt(double t)
        {
            current.PinInstant(t);
            object pinned = Get(current, "_pinned");
            var inst = new double[75];
            Array.Copy((double[])Get(pinned, "Cos"), 0, inst, 0, 24);
            Array.Copy((double[])Get(pinned, "Sin"), 0, inst, 24, 24);
            Array.Copy((double[])Get(current, "_pinnedEddyBase"), 0, inst, 48, 24);
            Array.Copy((double[])Get(current, "_pinnedCellBase"), 0, inst, 72, 3);
            current.UnpinInstant();
            return inst;
        }

        void CardEdges(double t)
        {
            dInst.CopyFromCPU(InstantAt(t));
            for (int a = 0; a < 3; a++)
            {
                var sc = new Scalars
                {
                    Component = a, Nx = nx, Ny = ny, Nz = nz, Stride = strides[a], Step = step,
                    HasBed = hasBed ? 1 : 0, FadeOn = fadeOn ? 1 : 0, HasReef = hasReef ? 1 : 0,
                    Overturning = overturning != 0d ? 1 : 0, H = h, DepthMetres = depthMetres, Scale = scaleF,
                };
                edgeK(lengths[a], dCols[a].View, dDeps[a].View, dBed[a].View, dOpen[a].View,
                    dReef[a].View, dInst.View, dEdges[a].View, sc);
            }
            assembleK(stockRef.Length, dEdges[0].View, dEdges[1].View, dEdges[2].View, dLive.View,
                dLowest.View, dFx.View, dFy.View, dFz.View, g3);
        }

        void CardApply(float stepDt)
        {
            double scaleApply = stepDt / cellVolume;
            fluxK(stockRef.Length, dLive.View, dStock.View, dFx.View, dFy.View, dFz.View, dQx.View,
                dQy.View, dQz.View, g3, scaleApply);
            applyK(stockRef.Length, dStock.View, dQx.View, dQy.View, dQz.View, g3);
        }

        int CardAdvect(double t, float dt)
        {
            CardEdges(t);
            double outflowScale = 0.5 * dt / cellVolume;
            outflowK(nx * ny, dLive.View, dFx.View, dFy.View, dFz.View, dRowMax.View, g3, outflowScale);
            double[] rows = dRowMax.GetAsArray1D();
            double outflow = 0d;
            for (int r = 0; r < rows.Length; r++) if (rows[r] > outflow) outflow = rows[r];
            int substeps = outflow <= 0.75 ? 1 : (int)Math.Ceiling(outflow / 0.75);
            if (substeps > GridField.MaximumSubsteps) throw new InvalidOperationException("too many substeps");
            float stepDt = dt / substeps;
            CardApply(stepDt);
            for (int i = 1; i < substeps; i++)
            {
                CardEdges(t + i * (double)stepDt);
                CardApply(stepDt);
            }
            return substeps;
        }

        foreach (var (t, dt) in new[] { (4000.0, 0.5f), (12345.5, 0.5f), (27799.5, 0.5f), (4000.0, 6f), (9000.0, 12f), (3000.0, 20f) })
        {
            Array.Copy(seed, stockRef, seed.Length);
            var cpu = Stopwatch.StartNew();
            try { grid.Advect(current, t, dt, grid.PatchWidthMetres); }
            catch (ArgumentException ex) { Console.WriteLine($"t {t} dt {dt}: the CPU refuses ({ex.Message.Substring(0, 60)}...)"); continue; }
            double cpuMs = cpu.Elapsed.TotalMilliseconds;
            var want = (double[])stockRef.Clone();

            dStock.CopyFromCPU(seed);
            acc.Synchronize();
            var card = Stopwatch.StartNew();
            int substeps = CardAdvect(t, dt);
            acc.Synchronize();
            double cardMs = card.Elapsed.TotalMilliseconds;
            double[] got = dStock.GetAsArray1D();
            double total = card.Elapsed.TotalMilliseconds;

            long diff = 0, moved = 0; string first = null;
            for (int i = 0; i < got.Length; i++)
            {
                if (want[i] != seed[i]) moved++;
                if (BitConverter.DoubleToInt64Bits(got[i]) != BitConverter.DoubleToInt64Bits(want[i]))
                {
                    diff++;
                    first ??= $"cell {i} card {got[i]:R} cpu {want[i]:R}";
                }
            }
            Console.WriteLine($"advect t {t} dt {dt}: {substeps} substep(s); {diff} of {got.Length} cells differ ({moved} moved); cpu {cpuMs:0.0} ms, card {cardMs:0.00} ms without the stock's copies, {total:0.00} with the download" + (first == null ? "" : "; first " + first));
        }

        // The copies, timed alone: the stock up and back is what the farm would pay per step.
        var copy = Stopwatch.StartNew();
        for (int r = 0; r < 10; r++) { dStock.CopyFromCPU(seed); dStock.CopyToCPU(stockRef); }
        acc.Synchronize();
        Console.WriteLine($"stock up and back, pageable: {copy.Elapsed.TotalMilliseconds / 10:0.00} ms");

        // The same copies from the same managed array pinned and registered as page-locked, so the
        // card's copy engine reads and writes it directly.
        var pin = System.Runtime.InteropServices.GCHandle.Alloc(stockRef, System.Runtime.InteropServices.GCHandleType.Pinned);
        using (var scope = acc.CreatePageLockFromPinned(stockRef))
        {
            Array.Copy(seed, stockRef, seed.Length);
            dStock.View.CopyFromPageLockedAsync(scope);
            acc.Synchronize();
            var locked = Stopwatch.StartNew();
            for (int r = 0; r < 10; r++) { dStock.View.CopyFromPageLockedAsync(scope); dStock.View.CopyToPageLockedAsync(scope); }
            acc.Synchronize();
            Console.WriteLine($"stock up and back, page-locked: {locked.Elapsed.TotalMilliseconds / 10:0.00} ms ({2.0 * stockRef.Length * 8 / 1e6:0.0} MB)");
            var lockedUp = Stopwatch.StartNew();
            for (int r = 0; r < 10; r++) dStock.View.CopyFromPageLockedAsync(scope);
            acc.Synchronize();
            Console.WriteLine($"stock up alone, page-locked: {lockedUp.Elapsed.TotalMilliseconds / 10:0.00} ms");
            var plain = Stopwatch.StartNew();
            for (int r = 0; r < 10; r++) { dStock.CopyFromCPU(stockRef); dStock.CopyToCPU(stockRef); }
            acc.Synchronize();
            Console.WriteLine($"stock up and back, registered array through the plain CopyFromCPU/CopyToCPU: {plain.Elapsed.TotalMilliseconds / 10:0.00} ms");
            var rows = Stopwatch.StartNew();
            for (int r = 0; r < 10; r++) { dStock.View.SubView(0, stockRef.Length / 2).CopyFromCPU(ref stockRef[0], stockRef.Length / 2); dStock.View.SubView(0, stockRef.Length / 2).CopyToCPU(ref stockRef[0], stockRef.Length / 2); }
            acc.Synchronize();
            Console.WriteLine($"half the stock up and back by ref through the registered array: {rows.Elapsed.TotalMilliseconds / 10:0.00} ms");
        }
        pin.Free();
        return 0;    }
}