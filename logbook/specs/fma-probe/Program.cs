// logbook/specs/fma-probe: does the RTX 4090 fuse a multiply and an add under ILGPU as the farm
// builds its context (Cuda().EnableAlgorithms())? Built and run from a copy under scratch/ (a build
// here would leave bin/ and obj/ in the record) with this project file beside it:
//
//   <Project Sdk="Microsoft.NET.Sdk">
//     <PropertyGroup>
//       <OutputType>Exe</OutputType>
//       <TargetFramework>net8.0</TargetFramework>
//       <Nullable>disable</Nullable>
//       <TieredCompilation>false</TieredCompilation>
//       <InvariantGlobalization>true</InvariantGlobalization>
//     </PropertyGroup>
//     <ItemGroup>
//       <PackageReference Include="ILGPU" Version="1.5.3" />
//       <PackageReference Include="ILGPU.Algorithms" Version="1.5.3" />
//     </ItemGroup>
//   </Project>
//
// Result on 2026-09-26: the default fuses (x*y+z differs from the CPU in 148,749 of 1,048,576 doubles,
// a 24-term chain in 472,478; single the same); a bit cast around a product changes nothing; every
// product written as inline mul.rn.f64 (CudaAsm.Emit) matches the CPU in all four patterns, 0 of
// 1,048,576.

// Each kernel line is evaluated on the card and on the CPU, where .NET never fuses, and the bits
// compared. A difference means the card fused (or rounded otherwise).
using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

static class Probe
{
    const int Chain = 24;

    static void KernelD(Index1D i, ArrayView<double> a, ArrayView<double> b, ArrayView<double> c,
        ArrayView<double> d, ArrayView<double> o, int n)
    {
        double x = a[i], y = b[i], z = c[i], w = d[i];
        o[i] = x * y + z;
        o[n + i] = x * y - z * w;
        double ay = 0;
        for (int k = 0; k < Chain; k++)
        {
            int j = (i + k * 7919) % n;
            double amplitude = a[j] * w;
            double cosChi = y * b[j] - z * c[j];
            ay -= amplitude * x * cosChi;
        }
        o[2 * n + i] = ay;
        o[3 * n + i] = (x + y) * (z + w) + x * w;
    }


    static double R(double v) => Interop.IntAsFloat(Interop.FloatAsInt(v));

    // An explicitly rounded multiply, which PTX never contracts into a fused multiply-add.
    static double M(double p, double q)
    {
        CudaAsm.Emit("mul.rn.f64 %0, %1, %2;", out double r, p, q);
        return r;
    }

    static void KernelDR(Index1D i, ArrayView<double> a, ArrayView<double> b, ArrayView<double> c,
        ArrayView<double> d, ArrayView<double> o, int n)
    {
        double x = a[i], y = b[i], z = c[i], w = d[i];
        o[i] = R(x * y) + z;
        o[n + i] = R(x * y) - R(z * w);
        double ay = 0;
        for (int k = 0; k < Chain; k++)
        {
            int j = (i + k * 7919) % n;
            double amplitude = R(a[j] * w);
            double cosChi = R(y * b[j]) - R(z * c[j]);
            ay -= R(R(amplitude * x) * cosChi);
        }
        o[2 * n + i] = ay;
        o[3 * n + i] = R((x + y) * (z + w)) + R(x * w);
    }


    static void KernelDM(Index1D i, ArrayView<double> a, ArrayView<double> b, ArrayView<double> c,
        ArrayView<double> d, ArrayView<double> o, int n)
    {
        double x = a[i], y = b[i], z = c[i], w = d[i];
        o[i] = M(x, y) + z;
        o[n + i] = M(x, y) - M(z, w);
        double ay = 0;
        for (int k = 0; k < Chain; k++)
        {
            int j = (i + k * 7919) % n;
            double amplitude = M(a[j], w);
            double cosChi = M(y, b[j]) - M(z, c[j]);
            ay -= M(M(amplitude, x), cosChi);
        }
        o[2 * n + i] = ay;
        o[3 * n + i] = M(x + y, z + w) + M(x, w);
    }

    static void KernelF(Index1D i, ArrayView<float> a, ArrayView<float> b, ArrayView<float> c,
        ArrayView<float> d, ArrayView<float> o, int n)
    {
        float x = a[i], y = b[i], z = c[i], w = d[i];
        o[i] = x * y + z;
        o[n + i] = x * y - z * w;
        float ay = 0;
        for (int k = 0; k < Chain; k++)
        {
            int j = (i + k * 7919) % n;
            float amplitude = a[j] * w;
            float cosChi = y * b[j] - z * c[j];
            ay -= amplitude * x * cosChi;
        }
        o[2 * n + i] = ay;
        o[3 * n + i] = (x + y) * (z + w) + x * w;
    }

    static double[] CpuD(double[] a, double[] b, double[] c, double[] d, int n)
    {
        var o = new double[4 * n];
        for (int i = 0; i < n; i++)
        {
            double x = a[i], y = b[i], z = c[i], w = d[i];
            o[i] = x * y + z;
            o[n + i] = x * y - z * w;
            double ay = 0;
            for (int k = 0; k < Chain; k++)
            {
                int j = (i + k * 7919) % n;
                double amplitude = a[j] * w;
                double cosChi = y * b[j] - z * c[j];
                ay -= amplitude * x * cosChi;
            }
            o[2 * n + i] = ay;
            o[3 * n + i] = (x + y) * (z + w) + x * w;
        }
        return o;
    }

    static float[] CpuF(float[] a, float[] b, float[] c, float[] d, int n)
    {
        var o = new float[4 * n];
        for (int i = 0; i < n; i++)
        {
            float x = a[i], y = b[i], z = c[i], w = d[i];
            o[i] = x * y + z;
            o[n + i] = x * y - z * w;
            float ay = 0;
            for (int k = 0; k < Chain; k++)
            {
                int j = (i + k * 7919) % n;
                float amplitude = a[j] * w;
                float cosChi = y * b[j] - z * c[j];
                ay -= amplitude * x * cosChi;
            }
            o[2 * n + i] = ay;
            o[3 * n + i] = (x + y) * (z + w) + x * w;
        }
        return o;
    }

    static void Main()
    {
        const int n = 1 << 20;
        var rng = new Random(20260926);
        var a = new double[n]; var b = new double[n]; var c = new double[n]; var d = new double[n];
        for (int i = 0; i < n; i++)
        {
            a[i] = (rng.NextDouble() * 2 - 1) * Math.Pow(2, rng.Next(-8, 8));
            b[i] = (rng.NextDouble() * 2 - 1) * Math.Pow(2, rng.Next(-8, 8));
            c[i] = (rng.NextDouble() * 2 - 1) * Math.Pow(2, rng.Next(-8, 8));
            d[i] = (rng.NextDouble() * 2 - 1) * Math.Pow(2, rng.Next(-8, 8));
        }
        var af = Array.ConvertAll(a, v => (float)v); var bf = Array.ConvertAll(b, v => (float)v);
        var cf = Array.ConvertAll(c, v => (float)v); var df = Array.ConvertAll(d, v => (float)v);

        string[] names = { "x*y+z", "x*y-z*w", "a 24-term chain", "(x+y)*(z+w)+x*w" };

        using var context = Context.Create(builder => builder.Cuda().EnableAlgorithms());
        using var acc = context.CreateCudaAccelerator(0);
        Console.WriteLine("device: " + acc.Name);

        var kd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>, int>(KernelD);
        var kf = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, int>(KernelF);

        using var da = acc.Allocate1D(a); using var db = acc.Allocate1D(b);
        using var dc = acc.Allocate1D(c); using var dd = acc.Allocate1D(d);
        using var dout = acc.Allocate1D<double>(4 * n);
        kd(n, da.View, db.View, dc.View, dd.View, dout.View, n);
        acc.Synchronize();
        double[] gd = dout.GetAsArray1D();
        double[] cd = CpuD(a, b, c, d, n);


        var kdr = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>, int>(KernelDR);
        using var dout2 = acc.Allocate1D<double>(4 * n);
        kdr(n, da.View, db.View, dc.View, dd.View, dout2.View, n);
        acc.Synchronize();
        double[] gdr = dout2.GetAsArray1D();
        for (int e = 0; e < 4; e++)
        {
            int diff = 0;
            for (int i = 0; i < n; i++)
                if (BitConverter.DoubleToInt64Bits(gdr[e * n + i]) != BitConverter.DoubleToInt64Bits(cd[e * n + i])) diff++;
            Console.WriteLine($"{names[e],-18} double, products through a bit cast: {diff,8} of {n} differ");
        }

        var kdm = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>, int>(KernelDM);
        using var dout3 = acc.Allocate1D<double>(4 * n);
        kdm(n, da.View, db.View, dc.View, dd.View, dout3.View, n);
        acc.Synchronize();
        double[] gdm = dout3.GetAsArray1D();
        for (int e = 0; e < 4; e++)
        {
            int diff = 0;
            for (int i = 0; i < n; i++)
                if (BitConverter.DoubleToInt64Bits(gdm[e * n + i]) != BitConverter.DoubleToInt64Bits(cd[e * n + i])) diff++;
            Console.WriteLine($"{names[e],-18} double, products as mul.rn.f64: {diff,8} of {n} differ");
        }
        using var fa = acc.Allocate1D(af); using var fb = acc.Allocate1D(bf);
        using var fc = acc.Allocate1D(cf); using var fdv = acc.Allocate1D(df);
        using var fout = acc.Allocate1D<float>(4 * n);
        kf(n, fa.View, fb.View, fc.View, fdv.View, fout.View, n);
        acc.Synchronize();
        float[] gf = fout.GetAsArray1D();
        float[] cfo = CpuF(af, bf, cf, df, n);

        for (int e = 0; e < 4; e++)
        {
            int diffD = 0, diffF = 0;
            for (int i = 0; i < n; i++)
            {
                if (BitConverter.DoubleToInt64Bits(gd[e * n + i]) != BitConverter.DoubleToInt64Bits(cd[e * n + i])) diffD++;
                if (BitConverter.SingleToInt32Bits(gf[e * n + i]) != BitConverter.SingleToInt32Bits(cfo[e * n + i])) diffF++;
            }
            Console.WriteLine($"{names[e],-18} double: {diffD,8} of {n} differ; single: {diffF,8} of {n} differ");
        }
    }
}