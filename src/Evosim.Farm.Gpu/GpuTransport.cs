using System;
using System.Runtime.InteropServices;
using Evosim.Core;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace Evosim.Farm.Gpu
{
    /// <summary>
    /// The snow's potential-driven transport on the card with the CPU's bits: GridField's edge
    /// sampling, face assembly, outflow bound, upwind fluxes and three passes as kernels, each the
    /// CPU loop's arithmetic in its order and grouping, with every product an explicitly rounded
    /// multiply that PTX never contracts into a fused multiply-add (logbook/specs/fma-probe). The
    /// farm hands it to the snow's grid under EVOSIM_GPU_TRANSPORT (GridField.TransportDevice); the
    /// grid keeps the transport's decisions and asks this for the arithmetic.
    /// </summary>
    /// <remarks>
    /// Round 50's tank matched the CPU in all 7,993,869 edges at five instants and two group sizes,
    /// and in all 2,624,832 cells over three whole steps, one of them split into two substeps
    /// (logbook/specs/transport-probe). Off the card (ILGPU's CPU accelerator) a product is the plain
    /// one, which .NET never fuses either, so the same kernels serve the Farm tests.
    /// </remarks>
    public sealed class GpuTransport : IPotentialTransportDevice, IDisposable
    {
        /// <summary>Threads a group, or the accelerator's ceiling if lower. Every output is one thread's own, so any size gives the same bits.</summary>
        public const int Group = 64;

        public struct EdgeParams
        {
            public int Component, Nx, Ny, Nz, Stride, Step, HasBed, FadeOn, HasReef, Overturning;
            public double H, DepthMetres;
            public float Scale;
        }

        public struct GridParams
        {
            public int Nx, Ny, Nz, Split, LayerStride;
        }

        private readonly Accelerator _acc;
        private readonly bool _onCard;
        private readonly int _group;

        private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<byte>,
            ArrayView<float>, ArrayView<double>, ArrayView<double>, EdgeParams> _edgeK;
        private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<byte>,
            ArrayView<int>, ArrayView<double>, ArrayView<double>, ArrayView<double>, GridParams> _assembleK;
        private Action<Index1D, ArrayView<byte>, ArrayView<double>, ArrayView<double>, ArrayView<double>,
            ArrayView<double>, GridParams, double> _outflowK;
        private Action<Index1D, ArrayView<byte>, ArrayView<double>, ArrayView<double>, ArrayView<double>,
            ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>, GridParams, double> _fluxK;
        private Action<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>,
            GridParams> _applyK;

        private readonly MemoryBuffer1D<double, Stride1D.Dense>[] _cols = new MemoryBuffer1D<double, Stride1D.Dense>[3];
        private readonly MemoryBuffer1D<double, Stride1D.Dense>[] _deps = new MemoryBuffer1D<double, Stride1D.Dense>[3];
        private readonly MemoryBuffer1D<double, Stride1D.Dense>[] _bed = new MemoryBuffer1D<double, Stride1D.Dense>[3];
        private readonly MemoryBuffer1D<byte, Stride1D.Dense>[] _open = new MemoryBuffer1D<byte, Stride1D.Dense>[3];
        private readonly MemoryBuffer1D<float, Stride1D.Dense>[] _reef = new MemoryBuffer1D<float, Stride1D.Dense>[3];
        private readonly MemoryBuffer1D<double, Stride1D.Dense>[] _edges = new MemoryBuffer1D<double, Stride1D.Dense>[3];
        private readonly EdgeParams[] _edgeParams = new EdgeParams[3];
        private MemoryBuffer1D<byte, Stride1D.Dense> _live;
        private MemoryBuffer1D<int, Stride1D.Dense> _lowest;
        private MemoryBuffer1D<double, Stride1D.Dense> _stock, _fx, _fy, _fz, _qx, _qy, _qz, _rowMax, _inst;
        private double[] _rowMaxHost;
        private GridParams _grid;
        private float _cellVolume;
        private int _cells;

        // The grid's stock, pinned and registered with the driver as page-locked for as long as the
        // grid hands this array, so its copies are direct transfers (GpuColumns' remarks).
        private double[] _pinnedStock;
        private GCHandle _pin;
        private PageLockScope<double> _stockLock;

        public GpuTransport(Accelerator accelerator)
        {
            _acc = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
            _onCard = accelerator.AcceleratorType == AcceleratorType.Cuda;
            _group = Math.Min(Group, accelerator.MaxNumThreadsPerGroup);
        }

        /// <summary>Whether the grid took this device (its tables are up).</summary>
        public bool Accepted { get; private set; }

        public bool Accept(PotentialTransportPlan plan)
        {
            if (plan == null) return false;

            ReleaseTables();

            _grid = new GridParams
            {
                Nx = plan.Nx, Ny = plan.Ny, Nz = plan.Nz, Split = plan.SplitColumns ? 1 : 0,
                LayerStride = plan.LayerStride,
            };
            _cells = plan.Live.Length;
            _cellVolume = plan.CellVolume;

            PotentialTransportAxis[] axes = { plan.X, plan.Y, plan.Z };
            for (int a = 0; a < 3; a++)
            {
                PotentialTransportAxis axis = axes[a];
                _cols[a] = _acc.Allocate1D(axis.Columns);
                _deps[a] = _acc.Allocate1D(axis.Depths);
                _bed[a] = _acc.Allocate1D(axis.Bed ?? new double[PotentialTransportPlan.BedTerms]);
                _open[a] = _acc.Allocate1D(Bytes(axis.Open));
                _reef[a] = _acc.Allocate1D(axis.ReefFade ?? new float[1]);
                _edges[a] = _acc.Allocate1D<double>(axis.Length);
                _edgeParams[a] = new EdgeParams
                {
                    Component = a, Nx = plan.Nx, Ny = plan.Ny, Nz = plan.Nz, Stride = axis.Stride,
                    Step = plan.DepthStep, HasBed = plan.HasBed ? 1 : 0, FadeOn = plan.ShoreFade ? 1 : 0,
                    HasReef = axis.ReefFade != null ? 1 : 0, Overturning = plan.Overturns ? 1 : 0,
                    H = plan.CellMetres, DepthMetres = plan.DepthMetres, Scale = plan.Scale,
                };
            }

            _live = _acc.Allocate1D(Bytes(plan.Live));
            _lowest = _acc.Allocate1D(plan.LowestLive);
            _stock = _acc.Allocate1D<double>(_cells);
            _fx = _acc.Allocate1D<double>(_cells);
            _fy = _acc.Allocate1D<double>(_cells);
            _fz = _acc.Allocate1D<double>(_cells);
            _qx = _acc.Allocate1D<double>(_cells);
            _qy = _acc.Allocate1D<double>(_cells);
            _qz = _acc.Allocate1D<double>(_cells);
            _rowMax = _acc.Allocate1D<double>(plan.Nx * plan.Ny);
            _rowMaxHost = new double[plan.Nx * plan.Ny];
            _inst = _acc.Allocate1D<double>(PotentialTransportPlan.InstantLength);

            _edgeK ??= _acc.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>,
                ArrayView<double>, ArrayView<byte>, ArrayView<float>, ArrayView<double>, ArrayView<double>,
                EdgeParams>(EdgeKernel, _group);
            _assembleK ??= _acc.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>,
                ArrayView<double>, ArrayView<byte>, ArrayView<int>, ArrayView<double>, ArrayView<double>,
                ArrayView<double>, GridParams>(AssembleKernel, _group);
            _outflowK ??= _acc.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<byte>, ArrayView<double>,
                ArrayView<double>, ArrayView<double>, ArrayView<double>, GridParams, double>(OutflowKernel, _group);
            _fluxK ??= _acc.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<byte>, ArrayView<double>,
                ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>,
                ArrayView<double>, GridParams, double>(FluxKernel, _group);
            _applyK ??= _acc.LoadImplicitlyGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>,
                ArrayView<double>, ArrayView<double>, GridParams>(ApplyKernel, _group);

            Accepted = true;
            return true;
        }

        public void Upload(double[] stock)
        {
            Pin(stock);
            _stock.View.CopyFromCPU(stock);
        }

        public void Sample(double[] instant)
        {
            _inst.View.CopyFromCPU(instant);

            for (int a = 0; a < 3; a++)
            {
                _edgeK((int)_edges[a].Length, _cols[a].View, _deps[a].View, _bed[a].View, _open[a].View,
                    _reef[a].View, _inst.View, _edges[a].View, _edgeParams[a]);
            }

            _assembleK(_cells, _edges[0].View, _edges[1].View, _edges[2].View, _live.View, _lowest.View,
                _fx.View, _fy.View, _fz.View, _grid);
        }

        public double LargestOutflowFraction(float seconds)
        {
            // GridField.LargestOutflowFraction's scale, in its types: a double half times the float
            // step, over the float cell volume.
            double scale = 0.5 * seconds / _cellVolume;

            _outflowK(_grid.Nx * _grid.Ny, _live.View, _fx.View, _fy.View, _fz.View, _rowMax.View, _grid, scale);
            _rowMax.View.CopyToCPU(_rowMaxHost);

            double answer = 0d;
            for (int r = 0; r < _rowMaxHost.Length; r++) if (_rowMaxHost[r] > answer) answer = _rowMaxHost[r];
            return answer;
        }

        public void Apply(float seconds)
        {
            // GridField.ApplyFaces' scale: a float step over the float cell volume, widened.
            double scale = seconds / _cellVolume;

            _fluxK(_cells, _live.View, _stock.View, _fx.View, _fy.View, _fz.View, _qx.View, _qy.View, _qz.View,
                _grid, scale);
            _applyK(_cells, _stock.View, _qx.View, _qy.View, _qz.View, _grid);
        }

        public void Download(double[] stock) => _stock.View.CopyToCPU(stock);

        public void Dispose()
        {
            ReleaseTables();
            Unpin();
        }

        private void Pin(double[] stock)
        {
            if (!_onCard || ReferenceEquals(stock, _pinnedStock)) return;

            Unpin();
            _pin = GCHandle.Alloc(stock, GCHandleType.Pinned);
            _stockLock = _acc.CreatePageLockFromPinned(stock);
            _pinnedStock = stock;
        }

        private void Unpin()
        {
            _stockLock?.Dispose();
            _stockLock = null;
            if (_pin.IsAllocated) _pin.Free();
            _pinnedStock = null;
        }

        private void ReleaseTables()
        {
            for (int a = 0; a < 3; a++)
            {
                _cols[a]?.Dispose(); _deps[a]?.Dispose(); _bed[a]?.Dispose();
                _open[a]?.Dispose(); _reef[a]?.Dispose(); _edges[a]?.Dispose();
                _cols[a] = null; _deps[a] = null; _bed[a] = null; _open[a] = null; _reef[a] = null; _edges[a] = null;
            }

            _live?.Dispose(); _lowest?.Dispose(); _stock?.Dispose();
            _fx?.Dispose(); _fy?.Dispose(); _fz?.Dispose();
            _qx?.Dispose(); _qy?.Dispose(); _qz?.Dispose();
            _rowMax?.Dispose(); _inst?.Dispose();
            _live = null; _lowest = null; _stock = null; _fx = null; _fy = null; _fz = null;
            _qx = null; _qy = null; _qz = null; _rowMax = null; _inst = null;
            Accepted = false;
        }

        private static byte[] Bytes(bool[] a)
        {
            var o = new byte[a.Length];
            for (int i = 0; i < a.Length; i++) o[i] = a[i] ? (byte)1 : (byte)0;
            return o;
        }

        // An explicitly rounded product: never contracted into a fused multiply-add on the card,
        // and the plain product elsewhere, which .NET never fuses.
        private static double M(double p, double q)
        {
            if (CudaAsm.IsSupported)
            {
                CudaAsm.Emit("mul.rn.f64 %0, %1, %2;", out double r, p, q);
                return r;
            }

            return p * q;
        }

        private static float MF(float p, float q)
        {
            if (CudaAsm.IsSupported)
            {
                CudaAsm.Emit("mul.rn.f32 %0, %1, %2;", out float r, p, q);
                return r;
            }

            return p * q;
        }

        // CurrentField.Eddies: one (m, j) block's three vertical modes, in k order.
        private static double Eddies(double ay, double cosM, double sinM, double f, int k0,
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

        // GridField.HorizontalEdgePlane (components 0 and 2) and VerticalEdgePlane (1) at one edge,
        // through CurrentField's hoisted PotentialAt, StreamsPotentialFrom, MapFrom and
        // FadedPotentialOf; a tank's, so no wrap.
        private static void EdgeKernel(Index1D idx, ArrayView<double> cols, ArrayView<double> deps,
            ArrayView<double> bed, ArrayView<byte> open, ArrayView<float> reef, ArrayView<double> inst,
            ArrayView<double> edges, EdgeParams s)
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

            int cb = c * PotentialTransportPlan.ColumnTerms;
            int db = depthIdx * PotentialTransportPlan.DepthTerms;
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
                int bb = c * PotentialTransportPlan.BedTerms;
                double yy = y;
                if (yy > 0d) yy = 0d;
                else if (yy < bed[bb]) yy = bed[bb];
                double stretch = bed[bb + 1];
                double scale = M(yy, s.DepthMetres) / bed[bb + 2];
                double a = M(scale, bed[bb + 3]);
                double b = M(scale, bed[bb + 4]);

                if (s.FadeOn != 0)
                {
                    double f = bed[bb + 5];
                    if (f == 0d)
                    {
                        px = 0f; py = 0f; pz = 0f;
                    }
                    else
                    {
                        px = MF((float)M(f, (double)ax + M(a, (double)ayf)), s.Scale);
                        py = MF((float)M(f, M(stretch, (double)ayf)), s.Scale);
                        pz = MF((float)M(f, (double)az + M(b, (double)ayf)), s.Scale);
                    }
                }
                else
                {
                    px = MF((float)((double)ax + M(a, (double)ayf)), s.Scale);
                    py = MF((float)M(stretch, (double)ayf), s.Scale);
                    pz = MF((float)((double)az + M(b, (double)ayf)), s.Scale);
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

        // GridField.AssembleFaces at one cell of a tank.
        private static void AssembleKernel(Index1D idx, ArrayView<double> ex, ArrayView<double> ey,
            ArrayView<double> ez, ArrayView<byte> live, ArrayView<int> lowest, ArrayView<double> fx,
            ArrayView<double> fy, ArrayView<double> fz, GridParams g)
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

            int east = ix + 1 == nx ? -1 : (iy * nx + ix + 1) * nz + iz;
            if (east >= 0 && live[east] == 0) east = -1;
            fx[cell] = east >= 0
                ? ey[eyEast + iz] + ez[ezEast + iz] - ey[eyEast + iz + 1] - ez[ezBelowEast + iz]
                : 0d;

            int front = iz + 1 == nz ? -1 : row + iz + 1;
            if (front >= 0 && live[front] == 0) front = -1;
            fz[cell] = front >= 0
                ? ex[exBelow + iz + 1] + ey[eyEast + iz + 1] - ex[exHere + iz + 1] - ey[eyWest + iz + 1]
                : 0d;

            fy[cell] = iy < lowest[lowRow + iz] && (g.Split == 0 || live[cell + g.LayerStride] != 0)
                ? ez[ezBelowEast + iz] + ex[exBelow + iz] - ez[ezBelowWest + iz] - ex[exBelow + iz + 1]
                : 0d;
        }

        // GridField.LargestOutflowFraction's inner loop over one (iy, ix) row; the host takes the
        // max over rows, which is the same max in any order.
        private static void OutflowKernel(Index1D idx, ArrayView<byte> live, ArrayView<double> fx,
            ArrayView<double> fy, ArrayView<double> fz, ArrayView<double> rowMax, GridParams g, double scale)
        {
            int r = idx;
            int nx = g.Nx, nz = g.Nz;
            int ix = r % nx; int iy = r / nx;
            int row = (iy * nx + ix) * nz;
            int above = row - nx * nz;
            int westRow = ix == 0 ? -1 : (iy * nx + ix - 1) * nz;
            double largest = 0d;

            for (int iz = 0; iz < nz; iz++)
            {
                int cell = row + iz;
                if (live[cell] == 0) continue;
                double sum = Math.Abs(fx[cell]) + Math.Abs(fz[cell]) + Math.Abs(fy[cell]);
                if (westRow >= 0) sum += Math.Abs(fx[westRow + iz]);
                if (iz > 0) sum += Math.Abs(fz[row + iz - 1]);
                if (iy > 0) sum += Math.Abs(fy[above + iz]);
                double outflow = M(sum, scale);
                if (outflow > largest) largest = outflow;
            }

            rowMax[r] = largest;
        }

        // GridField.ApplyFaces' flux loop at one cell of a tank.
        private static void FluxKernel(Index1D idx, ArrayView<byte> live, ArrayView<double> stock,
            ArrayView<double> fx, ArrayView<double> fy, ArrayView<double> fz, ArrayView<double> qx,
            ArrayView<double> qy, ArrayView<double> qz, GridParams g, double scale)
        {
            int cell = idx;
            int nx = g.Nx, nz = g.Nz;
            int iz = cell % nz; int t = cell / nz; int ix = t % nx; int iy = t / nx;
            if (live[cell] == 0) { qx[cell] = 0d; qy[cell] = 0d; qz[cell] = 0d; return; }

            int row = (iy * nx + ix) * nz;

            double q = fx[cell];
            if (q != 0d)
            {
                // A face onto the glass or a dead cell carries nothing (AssembleFaces), so a
                // negative face always has a live cell east of it.
                int east = ix + 1 == nx ? cell : (iy * nx + ix + 1) * nz + iz;
                qx[cell] = M(M(q > 0d ? stock[cell] : stock[east], q), scale);
            }
            else
            {
                qx[cell] = 0d;
            }

            q = fz[cell];
            if (q != 0d)
            {
                int front = iz + 1 == nz ? cell : row + iz + 1;
                qz[cell] = M(M(q > 0d ? stock[cell] : stock[front], q), scale);
            }
            else
            {
                qz[cell] = 0d;
            }

            q = fy[cell];
            qy[cell] = q != 0d ? M(M(q > 0d ? stock[cell] : stock[cell + nx * nz], q), scale) : 0d;
        }

        // ApplyEast, ApplyDown and ApplyFront fused at one cell: each pass only moves fluxes
        // computed before any of them, so a cell's result is its stock with its six fluxes added
        // and taken in the passes' order. Within a pass the upstream neighbour's flux arrives
        // before the cell's own leaves, except at a wrap, where the cell's own leaves first.
        private static void ApplyKernel(Index1D idx, ArrayView<double> stock, ArrayView<double> qx,
            ArrayView<double> qy, ArrayView<double> qz, GridParams g)
        {
            int cell = idx;
            int nx = g.Nx, ny = g.Ny, nz = g.Nz;
            int iz = cell % nz; int t = cell / nz; int ix = t % nx; int iy = t / nx;
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

            if (iy > 0) { f = qy[cell - nx * nz]; if (f != 0d) s = s + f; }
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
    }
}