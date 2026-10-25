using System;
using System.Collections.Generic;
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
    /// EVOSIM_GPU_TRANSPORT in the box (D204's open square): the snow's transport under the open
    /// ocean through <see cref="GpuTransport"/> on ILGPU's CPU accelerator gives the grid's own
    /// numbers to the bit, on a shaped floor, with and without the coral, across the seams. Off the
    /// card a product is the plain one, as on the CPU, so this checks the transcription; the card's
    /// own check is a farm run's digest against one without the device.
    /// </summary>
    public class GpuTransportBoxTests
    {
        private readonly ITestOutputHelper _out;

        public GpuTransportBoxTests(ITestOutputHelper output) => _out = output;

        private const float Side = 60f;

        private static World Square(bool coral)
        {
            var block = new Dictionary<string, string>
            {
                { "EVOSIM_SHAPE", "box" }, { "EVOSIM_AREA", "3600" }, { "EVOSIM_DEPTH", "20" },
                { "EVOSIM_PATCHES", "4" }, { "EVOSIM_PATCHES_ACROSS", "2" }, { "EVOSIM_SHARED_SPACE", "1" },
                { "EVOSIM_FOUNDER_DEPTH", "12" },
                { "EVOSIM_FIELD", "grid" }, { "EVOSIM_FIELD_CELL", "1" }, { "EVOSIM_FIELD_MATTER_CELL", "5" },
                { "EVOSIM_MATTER_BUDGET", "300" }, { "EVOSIM_IRRADIANCE", "200" },
                { "EVOSIM_MIXING", "0.02" }, { "EVOSIM_H_MIXING", "0.02" },
                { "EVOSIM_CURRENT", "0.1" }, { "EVOSIM_CURRENT_MODE", "ocean" },
                { "EVOSIM_CURRENT_PERIOD", "6000" }, { "EVOSIM_CURRENT_ADVECT", "1" },
                { "EVOSIM_READ_ACROSS_SEAMS", "1" },
                { "EVOSIM_BED_RELIEF", "1.5" }, { "EVOSIM_BED_TILT", "0" },
                { "EVOSIM_DT", "0.02" },
            };

            if (coral)
            {
                block["EVOSIM_REEF_SHAPE"] = "coral";
                block["EVOSIM_MATTER_ISLANDS"] = "25";
                block["EVOSIM_MATTER_ISLAND_COVER"] = "0.15";
                block["EVOSIM_MATTER_ISLAND_DEPTH"] = "6";
                block["EVOSIM_MATTER_ISLAND_MARGIN"] = "8";
            }

            RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block)));
            var world = new World(config, 11UL);
            var snow = (GridField)world.Nutrients;

            // Ten boxes of snow at different depths and places, some across a seam, so every face
            // has a gradient and the wrap carries something.
            for (int k = 0; k < 10; k++)
            {
                var centre = new Float3(
                    (6f * k + 0.5f) % Side, -2.5f - 1.2f * k, (Side - 7f * k + 0.5f + Side) % Side);
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
        [InlineData(false)]
        [InlineData(true)]
        public void TheBoxTransportOnTheAcceleratorIsTheGrids(bool coral)
        {
            World cpu = Square(coral);
            World dev = Square(coral);
            Assert.Equal(State(cpu), State(dev));
            Assert.Equal(coral, dev.Config.Current.Coral != null);

            using Context context = Context.Create(b => b.CPU(new CPUDevice(4, 1, 1)).EnableAlgorithms());
            using Accelerator accelerator = context.GetCPUDevice(0).CreateCPUAccelerator(context, CPUAcceleratorMode.Parallel);
            using var device = new GpuTransport(accelerator);

            var cpuSnow = (GridField)cpu.Nutrients;
            var devSnow = (GridField)dev.Nutrients;
            Assert.True(devSnow.OfferTransportDevice(device, dev.Config.Current), "the device declined the square's tables");

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

                Assert.True(device.Accepted, "the device declined the square's tables");
                Assert.Equal(cpuRefusal?.Message, devRefusal?.Message);
                Assert.True(State(cpu).AsSpan().SequenceEqual(State(dev)),
                    "the stocks part after the step at " + seconds + " s of " + dt + " s");

                if (cpuRefusal == null) steps++;
                seconds += dt;
            }

            // The CPU side took the tabled path the plan was built from, and the device side never sampled.
            Assert.True(cpuSnow.OceanPhasePasses >= steps, "the CPU's grid did not read the phase tables");
            Assert.Equal(0L, devSnow.OceanPhasePasses);

            Assert.True(steps >= 4, "too few steps were carried to check anything");
            Assert.False(before.AsSpan().SequenceEqual(State(cpu)), "the water moved no snow, so the check checked nothing");
            _out.WriteLine(steps + " steps carried, the last at " + seconds + " s; snow " + cpuSnow.Recount().ToString("R") + " J");
        }
    }
}
