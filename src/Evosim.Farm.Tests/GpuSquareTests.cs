using System;
using System.Collections.Generic;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D204's open square on the gpu engine: <see cref="GpuTank"/>'s world in a 60 m box, with the
    /// open ocean's current over a floor with relief and the box's seams read across. No shore
    /// and no reefs, which the engine refuses around the ocean until their fades are shown to
    /// match.
    /// </summary>
    internal static class GpuSquare
    {
        public static readonly IReadOnlyDictionary<string, string> Settings = new Dictionary<string, string>
        {
            { "EVOSIM_SHAPE", "box" }, { "EVOSIM_AREA", "3600" }, { "EVOSIM_PATCHES", "4" },
            { "EVOSIM_PATCHES_ACROSS", "2" }, { "EVOSIM_BED_TILT", "0" }, { "EVOSIM_BED_SHORE", "0" },
            { "EVOSIM_BED_SHORE_FADE", "0" }, { "EVOSIM_CURRENT_MODE", "ocean" }, { "EVOSIM_READ_ACROSS_SEAMS", "1" },
        };

        public static (World world, SolverConfig solver) Build(bool perPart = true, ulong seed = 7UL) =>
            GpuTank.Build(perPart, reefs: false, seed: seed, extra: Settings);
    }

    /// <summary>The ocean's instants, transcribed, against the field's own pinned slots.</summary>
    public class GpuSquareInstantTests
    {
        [Fact]
        public void TheOceansInstantsAreTheFieldsOwnToTheBit()
        {
            (_, SolverConfig s) = GpuSquare.Build(perPart: false);
            Assert.Equal(CurrentMode.Ocean, s.Current.Mode);

            var world = new GpuWorld(s);
            Assert.True(world.Ocean);

            double seconds = 0;
            for (int k = 0; k < 300; k++)
            {
                if (k % 60 == 0 || k > 290)
                {
                    (int values, int mismatches) = world.VerifyInstant(seconds);
                    Assert.True(values > 0, "no slot of the ocean was pinned at " + seconds.ToString("R") + " s");
                    Assert.Equal(0, mismatches);
                }

                seconds += 0.02;
            }

            foreach (double at in new[] { 1234.5678, 5999.99, 12000.01, 29999.5 })
            {
                (int values, int mismatches) = world.VerifyInstant(at);
                Assert.True(values > 0);
                Assert.Equal(0, mismatches);
            }
        }
    }

    /// <summary>The engine's refusals around the open square.</summary>
    public class GpuSquareRefusalTests
    {
        private static void Refused(Action act, string setting)
        {
            NotSupportedException e = Assert.Throws<NotSupportedException>(act);
            Assert.Contains(setting, e.Message);
        }

        [Fact]
        public void TheSquareIsCarried()
        {
            (_, SolverConfig s) = GpuSquare.Build(perPart: false);
            GpuWorld.Refuse(s);
        }

        [Fact]
        public void ABoxWithoutItsSeamsIsRefusedNamingThem()
        {
            (_, SolverConfig s) = GpuSquare.Build(perPart: false);
            s.SeamLengthMetres = 0;
            s.SeamWidthMetres = 0;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_READ_ACROSS_SEAMS");
        }

        [Fact]
        public void TheTanksStreamsInABoxAreRefusedNamingTheMode()
        {
            (_, SolverConfig s) = GpuSquare.Build(perPart: false);
            s.Current.Mode = CurrentMode.Transport;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_CURRENT_MODE");
        }
    }

    /// <summary>
    /// D194's coral in the open square (coral-build.md, step 2): built from the launcher's names, one rock
    /// handed to the solver, the current and the card, and named in the header and the manifest.
    /// </summary>
    public class GpuSquareCoralTests
    {
        private readonly ITestOutputHelper _out;

        public GpuSquareCoralTests(ITestOutputHelper output) => _out = output;

        public static readonly IReadOnlyDictionary<string, string> Coral = new Dictionary<string, string>
        {
            { "EVOSIM_MATTER_ISLANDS", "25" }, { "EVOSIM_MATTER_ISLAND_COVER", "0.15" },
            { "EVOSIM_MATTER_ISLAND_DEPTH", "6" }, { "EVOSIM_MATTER_ISLAND_MARGIN", "8" },
            { "EVOSIM_REEF_SHAPE", "coral" },
        };

        [Fact]
        public void TheSquaresCoralReachesEveryReaderTheRecordAndTheCard()
        {
            var extra = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in GpuSquare.Settings) extra[kv.Key] = kv.Value;
            foreach (KeyValuePair<string, string> kv in Coral) extra[kv.Key] = kv.Value;

            (World world, SolverConfig s) = GpuTank.Build(perPart: true, reefs: false, extra: extra);

            Assert.NotNull(world.Coral);
            Assert.Null(world.Reefs);
            Assert.Same(world.Coral, s.Coral);
            Assert.Same(world.Coral, s.Current.Coral);

            GpuWorld.Refuse(s);
            Assert.Equal(world.Coral.Count, new GpuWorld(s).CoralCount);

            var manifest = new RunManifest();
            Manifest.RecordCoral(manifest, world.Coral);
            Assert.Equal(world.Coral.Count, manifest.CoralColonies);
            Assert.Equal(world.Coral.Coral.Digest(), manifest.CoralDigest);

            string token = Report.CoralToken(world.Config, world.Coral);
            Assert.StartsWith(", coral " + world.Coral.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " colonies", token);
            Assert.Equal("", Report.CoralToken(world.Config, null));
            _out.WriteLine(token);
        }
    }

    /// <summary>
    /// The open square on ILGPU's CPU accelerator in double, against the solver, at a test's scale.
    /// The roots stay clear of the edges, since the card wraps a root inside its block and the
    /// solver leaves that to the farm's Simulation between steps; the links reach across a seam,
    /// so the water is sampled outside the box and folded, and two bodies touch across one.
    /// </summary>
    public class GpuSquareCpuAcceleratorTests
    {
        private readonly ITestOutputHelper _out;

        public GpuSquareCpuAcceleratorTests(ITestOutputHelper output) => _out = output;

        private static (DynamicsWorld world, World core) Pair(bool perPart)
        {
            (World core, SolverConfig solver) = GpuSquare.Build(perPart);
            return (new DynamicsWorld(solver) { Threads = 2 }, core);
        }

        private static void Populate(DynamicsWorld world, World core, int count, ulong seed, int firstId,
                                     string layout = "seams")
        {
            var rng = new Rng(seed);
            double length = core.BoxLengthMetres, width = core.BoxWidthMetres;
            double floor = -core.Config.WorldDepthMetres;

            for (int k = 0; k < count; k++)
            {
                // A pair touching across the seam at x = 0, a body reaching over the seam at z = 0, a
                // cluster in the middle, and one on the floor.
                Vec3 at = (k % 6) switch
                {
                    0 => new Vec3(0.45, -9.0, width / 2),
                    1 => new Vec3(length - 0.45, -9.0, width / 2),
                    2 => new Vec3(length / 3, -7.0, 0.5),
                    3 => new Vec3(length / 2 - 0.4, -10.0, width / 2 + 0.3),
                    4 => new Vec3(length / 2 + 0.4, -10.0, width / 2 - 0.3),
                    _ => new Vec3(length / 2 + 3.0, floor + 2.2, width / 2 + 3.0),
                };

                // Clear of every seam: the ocean alone. Reaching over one: the fold, and no contact across.
                if (layout == "middle") at = new Vec3(length / 2 + 2.5 * (k % 3) - 2.5, at.Y, width / 2 + 2.5 * (k / 3));
                if (layout == "reach" && k == 1) at = new Vec3(length / 2 - 6.0, -9.0, width / 2 - 6.0);
                world.AddInIdOrder(GpuTank.Body(firstId + k, world.Config, core, rng, at));
            }
        }

        private void AssertSame(DynamicsWorld cpu, DynamicsWorld gpu, string when)
        {
            byte[] a = GpuTank.Snapshot(cpu), b = GpuTank.Snapshot(gpu);
            if (a.Length == b.Length && a.AsSpan().SequenceEqual(b)) return;

            Assert.Fail("The engines part " + when + ": " + GpuTank.FirstDifference(cpu, gpu));
        }

        [Theory]
        [InlineData(true, "seams")]
        [InlineData(false, "seams")]
        [InlineData(false, "middle")]
        [InlineData(false, "reach")]
        public void DoubleOnTheCpuAcceleratorIsTheSolverInTheSquare(bool perPart, string layout)
        {
            (DynamicsWorld cpu, World coreA) = Pair(perPart);
            (DynamicsWorld gpu, World coreB) = Pair(perPart);

            Populate(cpu, coreA, 6, 11UL, 1, layout);
            Populate(gpu, coreB, 6, 11UL, 1, layout);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);
            _out.WriteLine("kernels compiled in " + backend.CompileMs.ToString("0") + " ms on " + backend.DeviceName);

            AssertSame(cpu, gpu, "before the first block");

            for (int block = 0; block < 4; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "after block " + block);

                if (block == 1)
                {
                    cpu.Remove(cpu.Creatures[0].Id);
                    gpu.Remove(gpu.Creatures[0].Id);
                    Populate(cpu, coreA, 1, 99UL, 50, layout);
                    Populate(gpu, coreB, 1, 99UL, 50, layout);
                }
            }

            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
            _out.WriteLine("pairs " + gpu.OverlapPairs + ", bed or glass " + gpu.BedOrGlassBodies);
        }
    }
}
