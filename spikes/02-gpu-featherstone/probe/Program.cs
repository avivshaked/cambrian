using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ComputeSharp;

static class IlgpuProbe
{
    public static void Kernel(Index1D i, ArrayView<float> buf, int n)
    {
        var a = new float[8];                 // does ILGPU accept a local array?
        for (int k = 0; k < 8; k++) a[k] = buf[i + k * n] * 2.0f;
        float s = 0;
        for (int k = 0; k < 8; k++) s += a[k];
        buf[i] = s;
    }

    public static void KernelD(Index1D i, ArrayView<double> buf, int n)
    {
        double s = 0;
        for (int k = 0; k < 8; k++) s += buf[i + k * n] * 2.0;
        buf[i] = Math.Sqrt(s) + Math.Sin(s);   // double transcendental on CUDA
    }
}

[ThreadGroupSize(64, 1, 1)]
[GeneratedComputeShaderDescriptor]
readonly partial struct CsProbe : IComputeShader
{
    private readonly ReadWriteBuffer<float> buf;
    private readonly int n;
    public CsProbe(ReadWriteBuffer<float> b, int nn) { buf = b; n = nn; }

    public void Execute()
    {
        int i = ThreadIds.X;
        // A local array is refused by the shader compiler (CMPS0025 stackalloc, CMPS0032 pointer;
        // 2026-09-22), so a per-thread scratch has to be fixed fields or a group-shared buffer.
        float s = 0;
        for (int k = 0; k < 8; k++) s += buf[i + k * n] * 2f;
        buf[i] = Hlsl.Sqrt(s * s + 1f) + Hlsl.Sin(s);
    }
}

class P
{
    static void Main()
    {
        Console.WriteLine("=== ILGPU ===");
        try
        {
            using var ctx = Context.Create(b => b.Cuda().EnableAlgorithms());
            foreach (var d in ctx) Console.WriteLine($"  device: {d.Name} [{d.AcceleratorType}]");
            using var acc = ctx.GetPreferredDevice(preferCPU: false).CreateAccelerator(ctx);
            Console.WriteLine($"  using: {acc.Name} / {acc.AcceleratorType}");
            if (acc is CudaAccelerator ca)
                Console.WriteLine($"  CC {ca.Device.Architecture}, SMs {ca.Device.NumMultiprocessors}, maxThreads/group {ca.MaxNumThreadsPerGroup}");
            int n = 1024;
            using var buf = acc.Allocate1D<float>(n * 8);
            buf.MemSetToZero();
            try
            {
                var k = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, int>(IlgpuProbe.Kernel);
                k(n, buf.View, n); acc.Synchronize();
                Console.WriteLine("  local array float kernel: OK");
            }
            catch (Exception e) { Console.WriteLine("  local array float kernel FAILED: " + e.GetType().Name + ": " + e.Message.Split('\n')[0]); }

            using var bufd = acc.Allocate1D<double>(n * 8);
            bufd.MemSetToZero();
            try
            {
                var kd = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>, int>(IlgpuProbe.KernelD);
                kd(n, bufd.View, n); acc.Synchronize();
                Console.WriteLine("  double kernel: OK");
            }
            catch (Exception e) { Console.WriteLine("  double kernel FAILED: " + e.GetType().Name + ": " + e.Message.Split('\n')[0]); }
        }
        catch (Exception e) { Console.WriteLine("  ILGPU context FAILED: " + e.GetType().Name + ": " + e.Message); }

        Console.WriteLine("=== ComputeSharp ===");
        try
        {
            var dev = GraphicsDevice.GetDefault();
            Console.WriteLine($"  device: {dev.Name}, dedicated {dev.DedicatedMemorySize / (1024 * 1024)} MB, doubles={dev.IsDoublePrecisionSupportAvailable()}");
            int n = 1024;
            using var b = dev.AllocateReadWriteBuffer<float>(n * 8);
            dev.For(n, new CsProbe(b, n));
            var outp = new float[n * 8];
            b.CopyTo(outp);
            Console.WriteLine("  shader dispatch: OK, out[0]=" + outp[0]);
        }
        catch (Exception e) { Console.WriteLine("  ComputeSharp FAILED: " + e.GetType().Name + ": " + e.Message); }
    }
}
