using System;
using System.IO;
using Evosim.Core;
using Evosim.Farm.Gpu;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// EVOSIM_GPU_TRANSPORT at test scale: the snow's transport through <see cref="GpuTransport"/>
    /// on ILGPU's CPU accelerator gives the grid's own numbers to the bit, in the gpu engine's test
    /// tank with its bed, its beach and, in one case, its reefs. Off the card a product is the plain
    /// one, as on the CPU, so this checks the transcription; the card's own check is the digest of a
    /// farm run against one without the device (logbook/specs/transport-probe).
    /// </summary>
    public class GpuTransportTests
    {
        private readonly ITestOutputHelper _out;

        public GpuTransportTests(ITestOutputHelper output) => _out = output;

        private static World Seeded(bool reefs)
        {
            (World world, _) = GpuTank.Build(perPart: false, reefs: reefs);
            var snow = (GridField)world.Nutrients;
            float r = world.TankRadiusMetres;

            // Ten boxes of snow at different depths and places, so every face has a gradient.
            for (int k = 0; k < 10; k++)
            {
                var centre = new Float3(r + 5f * MathF.Cos(k), -2.5f - 1.5f * k, r + 5f * MathF.Sin(k));
                snow.DepositBox(1000.0 * (k + 1), centre, new Float3(2f, 1f, 2f));
            }

            return world;
        }

        private static byte[] State(World world)
        {
            using var bytes = new MemoryStream();
            using (var w = new BinaryWriter(bytes)) world.WriteState(w);
            return bytes.ToArray();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TheTransportOnTheAcceleratorIsTheGrids(bool reefs)
        {
            World cpu = Seeded(reefs);
            World dev = Seeded(reefs);
            Assert.Equal(State(cpu), State(dev));

            using Context context = Context.Create(b => b.CPU(new CPUDevice(4, 1, 1)).EnableAlgorithms());
            using Accelerator accelerator = context.GetCPUDevice(0).CreateCPUAccelerator(context, CPUAcceleratorMode.Parallel);
            using var device = new GpuTransport(accelerator);

            var cpuSnow = (GridField)cpu.Nutrients;
            var devSnow = (GridField)dev.Nutrients;
            devSnow.TransportDevice = device;

            double seconds = 1234.5;
            int steps = 0;
            byte[] before = State(cpu);

            foreach (float dt in new[] { 0.5f, 0.5f, 0.5f, 0.5f, 4f, 8f })
            {
                Exception cpuRefusal = null, devRefusal = null;
                try { cpuSnow.Advect(cpu.Config.Current, seconds, dt, cpuSnow.PatchWidthMetres); }
                catch (ArgumentException e) { cpuRefusal = e; }
                try { devSnow.Advect(dev.Config.Current, seconds, dt, devSnow.PatchWidthMetres); }
                catch (ArgumentException e) { devRefusal = e; }

                Assert.True(device.Accepted, "the device declined the tank's tables");
                Assert.Equal(cpuRefusal?.Message, devRefusal?.Message);
                Assert.True(State(cpu).AsSpan().SequenceEqual(State(dev)),
                    "the stocks part after the step at " + seconds + " s of " + dt + " s");

                if (cpuRefusal == null) steps++;
                seconds += dt;
            }

            Assert.True(steps >= 4, "too few steps were carried to check anything");
            Assert.False(before.AsSpan().SequenceEqual(State(cpu)), "the water moved no snow, so the check checked nothing");
            _out.WriteLine(steps + " steps carried, the last at " + seconds + " s; snow " + cpuSnow.Recount().ToString("R") + " J");
        }
    }
}