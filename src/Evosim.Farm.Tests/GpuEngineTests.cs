using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A small tank with everything the gpu engine carries switched on: the streams with their
    /// acceleration, the bed with its relief and its beach, the reefs, D111's torque, per-part
    /// contact on request, and the senses the farm wires. Built through the farm's own binding
    /// and Core's own world, as a launch builds it, and stepped at the solver level.
    /// </summary>
    internal static class GpuTank
    {
        public static Dictionary<string, string> Launcher(bool perPart, bool reefs)
        {
            Dictionary<string, string> block = Common(perPart);
            if (!reefs) return block;

            // With no reef every reef dial stands at its default (ReefGeometry.Refuse).
            block["EVOSIM_REEF_COVER"] = "0.1";
            block["EVOSIM_REEF_MAX_COUNT"] = "6";
            block["EVOSIM_REEF_CAP_RADIUS_MIN"] = "2";
            block["EVOSIM_REEF_CAP_RADIUS_MAX"] = "3";
            block["EVOSIM_REEF_CAP_DEPTH"] = "8";
            block["EVOSIM_REEF_CAP_DEPTH_JITTER"] = "0";
            block["EVOSIM_REEF_CAP_THICKNESS"] = "1";
            block["EVOSIM_REEF_FADE"] = "2";
            return block;
        }

        private static Dictionary<string, string> Common(bool perPart) => new Dictionary<string, string>
        {
            { "EVOSIM_SHAPE", "tank" }, { "EVOSIM_AREA", "400" }, { "EVOSIM_DEPTH", "20" },
            { "EVOSIM_PATCHES", "4" }, { "EVOSIM_SHARED_SPACE", "1" }, { "EVOSIM_FOUNDER_DEPTH", "12" },
            { "EVOSIM_FIELD", "grid" }, { "EVOSIM_FIELD_CELL", "1" }, { "EVOSIM_FIELD_MATTER_CELL", "5" },
            { "EVOSIM_MATTER_BUDGET", "300" }, { "EVOSIM_IRRADIANCE", "200" },
            { "EVOSIM_MIXING", "0.02" }, { "EVOSIM_H_MIXING", "0.02" },
            { "EVOSIM_CURRENT", "0.1" }, { "EVOSIM_CURRENT_MODE", "Transport" },
            { "EVOSIM_CURRENT_PERIOD", "6000" }, { "EVOSIM_CURRENT_CELL", "30" },
            { "EVOSIM_CURRENT_ROLLS", "1" }, { "EVOSIM_CURRENT_BLINK", "3000" }, { "EVOSIM_CURRENT_ADVECT", "1" },
            { "EVOSIM_FLUID_ACCEL", "1" }, { "EVOSIM_ADDED_MASS", "0.5" }, { "EVOSIM_SURFACE_RESTORE", "1" },
            { "EVOSIM_EXCESS_DENSITY", "0.02" }, { "EVOSIM_NEUTRAL_VOLUME", "0.25" },
            { "EVOSIM_BED_RELIEF", "1.5" }, { "EVOSIM_BED_TILT", "10" },
            { "EVOSIM_BED_SHORE", "1" }, { "EVOSIM_BED_SHORE_FADE", "4" },
            { "EVOSIM_BUOYANCY_OFFSET_COST", "0.02" },
            { "EVOSIM_CONTACT_PER_PART", perPart ? "1" : "0" },
            { "EVOSIM_DT", "0.02" }, { "EVOSIM_WATER_HOLD", "0" },
            { "EVOSIM_SENSE_CHEMICAL", "1" }, { "EVOSIM_SENSE_ENERGY", "1" }, { "EVOSIM_SENSE_FLOW", "1" },
            { "EVOSIM_SENSE_CONTACT", "1" }, { "EVOSIM_SENSE_DAMAGE", "1" },
        };

        public static (World world, SolverConfig solver) Build(bool perPart = true, bool reefs = true, ulong seed = 7UL)
        {
            RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(Launcher(perPart, reefs))));
            var world = new World(config, seed);

            SolverConfig solver = SolverConfig.FromWorld(config, config.PhysicsStepSeconds, world.Bed, world);
            solver.ContactInstrument = true;
            solver.ContactEvents = true;
            return (world, solver);
        }

        /// <summary>A fixed reserve, so the energy sense reads a number and not the stand-in.</summary>
        private sealed class Reserve : IReserveSource
        {
            public float SecondsOfReserve { get; set; }
        }

        /// <summary>
        /// A random viable body the gpu engine holds (sixteen links at most, three inputs a
        /// neuron), placed as the farm places one and wired as the farm wires one.
        /// </summary>
        public static Creature Body(int id, SolverConfig solver, World world, Rng rng, Vec3 at, int maxNeurons = 256)
        {
            for (int attempt = 0; ; attempt++)
            {
                Genome genome = GenomeFactory.RandomViable(rng, minParts: 2);
                Phenotype plan = Developer.Develop(genome, DevelopmentLimits.Default);
                if (plan.PartCount > 16) continue;

                var body = new Creature(id, plan, solver);
                if (GpuSlots.OverInputs(body) || Math.Max(GpuSlots.NeuronsOf(body), body.Brain.NeuronCount) > maxNeurons)
                {
                    if (attempt < 200) continue;
                }

                body.PlaceAsDeveloped(at, plan);
                body.Senses.Nutrients = world.Nutrients;
                body.Senses.Reserve = new Reserve { SecondsOfReserve = 40f + id };

                var contact = new bool[body.Links];
                var damage = new float[body.Links];
                for (int l = 0; l < body.Links; l++)
                {
                    contact[l] = (l + id) % 3 == 0;
                    damage[l] = 0.1f * ((l + id) % 4);
                }

                body.Senses.Contact = contact;
                body.Senses.Damage = damage;

                if (body.Jointed) body.EnableTrace();
                return body;
            }
        }

        /// <summary>The tank's axis, where Core's tank puts it.</summary>
        public static Vec3 Axis(World world) => new Vec3(world.TankRadiusMetres, 0, world.TankRadiusMetres);

        /// <summary>
        /// Everything a checkpoint holds of the world and every body, and the per-step outputs a
        /// checkpoint derives: the committed link spheres, the external forces, the drive signal,
        /// the overlap lists with their link pairs, the bed-or-glass flag and the event list.
        /// </summary>
        public static byte[] Snapshot(DynamicsWorld world)
        {
            using var stream = new MemoryStream();
            using (var w = new CanonicalWriter(stream))
            {
                world.WriteState(w);
                w.Write(world.OverlapPairsThisStep);
                w.Write(world.OverlapPairsJointedThisStep);
                w.Write(world.OverlapPairsHeldThisStep);
                w.Write(world.OverlapBodiesThisStep);
                w.Write(world.BedOrGlassBodiesThisStep);

                foreach (OverlapPair pair in world.Overlaps)
                {
                    w.Write(pair.A); w.Write(pair.B); w.Write(pair.Jointed); w.Write(pair.Held);
                    w.Write(pair.PartA); w.Write(pair.PartB);
                }

                foreach (Creature c in world.Creatures)
                {
                    c.WriteState(w);
                    Doubles(w, c.Fext);
                    Doubles(w, c.Tau);
                    foreach (float v in c.DriveSignal) w.Write(v);

                    if (c.LinkContactActive)
                    {
                        Doubles(w, c.LinkContactCentre);
                        Doubles(w, c.LinkContactRadius);
                        Doubles(w, c.LinkContactVelocity);
                    }

                    w.Write(c.OverlapCount);
                    for (int k = 0; k < c.OverlapCount; k++)
                    {
                        w.Write(c.OverlapId(k));
                        w.Write(c.OverlapPart(k));
                        w.Write(c.OverlapOtherPart(k));
                    }

                    w.Write(c.TouchedBedOrGlass);
                }
            }

            return stream.ToArray();
        }

        /// <summary>
        /// A writer that writes every NaN as one pattern, the digest's rule (gpu-port-spec.md
        /// section 4): a lost body's state holds NaNs whose sign says which compilation ran, so a
        /// raw-bit comparison of two engines' checkpoints would part on the loss and nothing else.
        /// </summary>
        private sealed class CanonicalWriter : BinaryWriter
        {
            public CanonicalWriter(Stream stream) : base(stream, Encoding.UTF8, leaveOpen: true) { }

            public override void Write(double value) => base.Write(double.IsNaN(value) ? double.NaN : value);

            public override void Write(float value) => base.Write(float.IsNaN(value) ? float.NaN : value);
        }

        private static bool Same(double a, double b) =>
            BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b) || (double.IsNaN(a) && double.IsNaN(b));

        private static void Doubles(BinaryWriter w, double[] values)
        {
            // NaN written as one pattern: two compilations may keep different NaN payloads in a
            // lost body, and the digest reads them as one (logbook/0115).
            foreach (double v in values) w.Write(double.IsNaN(v) ? double.NaN : v);
        }

        /// <summary>Where two snapshots first part, for the failure message.</summary>
        public static string FirstDifference(DynamicsWorld cpu, DynamicsWorld gpu)
        {
            for (int i = 0; i < cpu.Creatures.Count; i++)
            {
                Creature a = cpu.Creatures[i], b = gpu.Creatures[i];
                string where = "body " + a.Id + " (" + a.Links + " links, " + a.Dof + " dof)";

                string d = Compare(a.Position, b.Position, "Position") ?? Compare(a.Velocity, b.Velocity, "Velocity") ??
                           Compare(a.Spin, b.Spin, "Spin") ?? Compare(a.Q, b.Q, "Q") ?? Compare(a.Qd, b.Qd, "Qd") ??
                           Compare(a.Water, b.Water, "Water") ?? Compare(a.WaterAcceleration, b.WaterAcceleration, "WaterAcceleration") ??
                           Compare(a.RelativeVelocity, b.RelativeVelocity, "RelativeVelocity") ??
                           Compare(a.Fext, b.Fext, "Fext") ?? Compare(a.Tau, b.Tau, "Tau") ??
                           (a.Alive != b.Alive ? "Alive " + a.Alive + " against " + b.Alive : null) ??
                           (a.OverlapCount != b.OverlapCount ? "OverlapCount " + a.OverlapCount + " against " + b.OverlapCount : null) ??
                           (!Same(a.MechanicalWorkJoules, b.MechanicalWorkJoules) ? "work" : null) ??
                           (!Same(a.DissipatedJoules, b.DissipatedJoules) ? "dissipated" : null) ??
                           (!Same(a.ContactRadius, b.ContactRadius) ? "ContactRadius" : null);

                if (d != null) return where + ": " + d;

                for (int n = 0; n < a.DriveSignal.Length; n++)
                {
                    if (BitConverter.SingleToInt32Bits(a.DriveSignal[n]) != BitConverter.SingleToInt32Bits(b.DriveSignal[n]) &&
                        !(float.IsNaN(a.DriveSignal[n]) && float.IsNaN(b.DriveSignal[n])))
                    {
                        return where + ": DriveSignal[" + n + "] " + a.DriveSignal[n].ToString("R") + " against " +
                               b.DriveSignal[n].ToString("R");
                    }
                }
            }

            return "the world's counters or a checkpointed field outside the list above " +
                   "(census " + cpu.OverlapPairs + "/" + gpu.OverlapPairs + ", clock " +
                   cpu.ElapsedSeconds.ToString("R") + "/" + gpu.ElapsedSeconds.ToString("R") + ")";
        }

        private static string Compare(double[] a, double[] b, string name)
        {
            for (int k = 0; k < a.Length; k++)
            {
                bool same = BitConverter.DoubleToInt64Bits(a[k]) == BitConverter.DoubleToInt64Bits(b[k]) ||
                            (double.IsNaN(a[k]) && double.IsNaN(b[k]));
                if (!same)
                {
                    return name + "[" + k + "] " + a[k].ToString("R") + " against " + b[k].ToString("R") +
                           " (" + (a[k] - b[k]).ToString("R") + ")";
                }
            }

            return null;
        }

        /// <summary>The engine on ILGPU's CPU accelerator: the transcription's test, at four threads.</summary>
        public static GpuOptions CpuDouble() => new GpuOptions { Device = "cpu", Precision = "double", CpuThreads = 4 };
    }

    /// <summary>The kernels are the template's output (logbook/specs/gpu-port-spec.md section 8, test 1).</summary>
    public class GpuKernelSourceTests
    {
        [Fact]
        public void TheGeneratedKernelsAreTheTemplatesOutput()
        {
            string dir = KernelGenerator.FindSourceDirectory(AppContext.BaseDirectory);

            if (Environment.GetEnvironmentVariable("EVOSIM_GPU_REGENERATE") == "1") KernelGenerator.WriteAll(dir);

            foreach ((string name, string text) in KernelGenerator.GenerateAll(dir))
            {
                string committed = KernelGenerator.Lf(File.ReadAllText(Path.Combine(dir, name)));
                Assert.True(
                    committed == text,
                    name + " is not what gpu-kernel.template and gpu-runner.template generate: edit the " +
                    "templates, not the output, and run this test with EVOSIM_GPU_REGENERATE=1.");
            }
        }

        [Fact]
        public void EveryClassIsWrittenOncePerPrecision()
        {
            string dir = KernelGenerator.FindSourceDirectory(AppContext.BaseDirectory);
            foreach ((string name, string text) in KernelGenerator.GenerateAll(dir))
            {
                for (int c = 0; c < KernelGenerator.ClassCount; c++)
                {
                    Assert.Contains("public static void Step" + c + "(", text);
                    Assert.Contains("const int MaxLinks = " + KernelGenerator.ClassLinks[c] + ";", text);
                }

                Assert.DoesNotContain("__", text.Replace("k__BackingField", ""));
            }
        }
    }

    /// <summary>What the engine refuses, each naming its setting (section 3, test 6).</summary>
    public class GpuRefusalTests
    {
        private static void Refused(Action build, string setting)
        {
            var e = Assert.ThrowsAny<Exception>(build);
            Assert.Contains(setting, e.Message);
        }

        [Fact]
        public void ABoxIsRefusedNamingTheShape()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            s.TankRadiusMetres = 0;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_SHAPE");
        }

        [Fact]
        public void AWaterHoldIsRefusedNamingIt()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            s.WaterHoldSeconds = 0.5;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_WATER_HOLD");
        }

        [Fact]
        public void AnotherStepIsRefusedNamingIt()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            s.StepSeconds = 0.005;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_DT");
        }

        [Fact]
        public void TheRollsAreRefusedNamingTheMode()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            s.Current.Mode = CurrentMode.Rolls;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_CURRENT_MODE");
        }

        [Fact]
        public void AVentIsRefusedNamingIt()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            s.Current.VentSpeed = 0.1f;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_VENT");
        }

        [Fact]
        public void TheEnginesOwnWordsAreRefusedNamingTheirSettings()
        {
            Refused(() => new GpuOptions { Device = "opencl" }.Validate(), "EVOSIM_GPU_DEVICE");
            Refused(() => new GpuOptions { Precision = "half" }.Validate(), "EVOSIM_GPU_PRECISION");
            Refused(() => new GpuOptions { Mean = "tree" }.Validate(), "EVOSIM_GPU_MEAN");
            Refused(() => new GpuOptions { GroupSize = 4096 }.Validate(), "EVOSIM_GPU_GROUP");
        }

        [Fact]
        public void TheEngineSettingsAreReadAndAreNotTunables()
        {
            var block = GpuTank.Launcher(perPart: true, reefs: false);
            string plain = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block))).Hash();

            block["EVOSIM_ENGINE"] = "GPU";
            block["EVOSIM_GPU_DEVICE"] = "cpu";
            block["EVOSIM_GPU_PRECISION"] = "double";
            block["EVOSIM_GPU_GROUP"] = "64";
            block["EVOSIM_GPU_CONCURRENT"] = "1";
            EnvSettings s = EnvBinding.Read(EnvBinding.Of(block));

            Assert.True(s.EngineIsGpu);
            Assert.Equal("cpu", s.GpuDevice);
            Assert.Equal("double", s.GpuPrecision);
            Assert.Equal(64, s.GpuGroup);
            Assert.True(s.GpuConcurrent);
            Assert.Equal(plain, EnvBinding.BuildConfig(s).Hash());
            Assert.Empty(EnvBinding.UnknownNames(block.Keys));
        }
    }

    /// <summary>The current's instants, transcribed, against the field's own pinned slots.</summary>
    public class GpuInstantTests
    {
        [Fact]
        public void TheInstantsAreTheFieldsOwnToTheBit()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            var world = new GpuWorld(s);

            double seconds = 0;
            for (int k = 0; k < 300; k++)
            {
                if (k % 60 == 0 || k > 290)
                {
                    (int values, int mismatches) = world.VerifyInstant(seconds);
                    Assert.True(values > 0, "no slot of the field was pinned at " + seconds.ToString("R") + " s");
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

    /// <summary>
    /// The transcription on ILGPU's CPU accelerator in double, against the solver, at the
    /// scale the owner's load rule allows a test (a handful of bodies, a hundred-odd steps, four
    /// threads). The farm-scale acceptance is the caller's (gpu-port-spec.md section 4).
    /// </summary>
    public class GpuCpuAcceleratorTests
    {
        private readonly ITestOutputHelper _out;

        public GpuCpuAcceleratorTests(ITestOutputHelper output) => _out = output;

        private static (DynamicsWorld world, World core) Pair(bool perPart, bool reefs)
        {
            (World core, SolverConfig solver) = GpuTank.Build(perPart, reefs);
            return (new DynamicsWorld(solver) { Threads = 2 }, core);
        }

        private static void Populate(DynamicsWorld world, World core, int count, ulong seed, int firstId)
        {
            var rng = new Rng(seed);
            Vec3 axis = GpuTank.Axis(core);

            for (int k = 0; k < count; k++)
            {
                // A cluster a couple of metres across at mid-depth, so bodies touch each other;
                // the fifth at the glass and the sixth on the bed, so the world's contacts run.
                var at = new Vec3(axis.X + 0.7 * (k % 3) - 0.7, -9.0 - 0.6 * (k / 3), axis.Z + 0.5 * (k % 2));
                if (k == 4) at = new Vec3(axis.X + core.TankRadiusMetres - 0.4, -6.0, axis.Z);
                if (k == 5) at = new Vec3(axis.X - 2.0, -core.Config.WorldDepthMetres + 0.6, axis.Z - 2.0);
                world.AddInIdOrder(GpuTank.Body(firstId + k, world.Config, core, rng, at));
            }
        }

        private void AssertSame(DynamicsWorld cpu, DynamicsWorld gpu, string when)
        {
            byte[] a = GpuTank.Snapshot(cpu), b = GpuTank.Snapshot(gpu);
            if (a.Length == b.Length && a.AsSpan().SequenceEqual(b)) return;

            Assert.Fail("The engines part " + when + ": " + GpuTank.FirstDifference(cpu, gpu));
        }

        /// <summary>
        /// Section 8's tests 2 and 3 at test scale: a block at a time, with a death and a birth
        /// between blocks so a slot is freed and taken again, every body and every counter the
        /// same to the bit after every block.
        /// </summary>
        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void DoubleOnTheCpuAcceleratorIsTheSolver(bool perPart, bool reefs)
        {
            (DynamicsWorld cpu, World coreA) = Pair(perPart, reefs);
            (DynamicsWorld gpu, World coreB) = Pair(perPart, reefs);

            Populate(cpu, coreA, 6, 11UL, 1);
            Populate(gpu, coreB, 6, 11UL, 1);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);
            _out.WriteLine("kernels compiled in " + backend.CompileMs.ToString("0") + " ms on " + backend.DeviceName);

            AssertSame(cpu, gpu, "before the first block");

            for (int block = 0; block < 4; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "after block " + block);

                // A death and a birth: the lowest-id body leaves, a new one arrives at the end.
                if (block == 1)
                {
                    cpu.Remove(cpu.Creatures[0].Id);
                    gpu.Remove(gpu.Creatures[0].Id);
                    Populate(cpu, coreA, 1, 99UL, 50);
                    Populate(gpu, coreB, 1, 99UL, 50);
                }
            }

            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
            _out.WriteLine("pairs " + gpu.OverlapPairs + ", bed or glass " + gpu.BedOrGlassBodies);
        }

        /// <summary>
        /// EVOSIM_GPU_CONCURRENT: with the size classes on a stream each, the engine still gives
        /// the solver's numbers to the bit after every block, the death and the birth included.
        /// The card's own check is the digest at two group sizes (13af2c3's message).
        /// </summary>
        [Fact]
        public void TheClassesOnConcurrentStreamsAreTheSolver()
        {
            (DynamicsWorld cpu, World coreA) = Pair(true, true);
            (DynamicsWorld gpu, World coreB) = Pair(true, true);

            Populate(cpu, coreA, 9, 11UL, 1);
            Populate(gpu, coreB, 9, 11UL, 1);

            GpuOptions options = GpuTank.CpuDouble();
            options.Concurrent = true;
            using var backend = new GpuBackend(gpu.Config, options);
            gpu.UseBackend(backend);
            Assert.EndsWith(" concurrent", backend.HeaderToken());

            for (int block = 0; block < 4; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "after block " + block);

                if (block == 1)
                {
                    cpu.Remove(cpu.Creatures[0].Id);
                    gpu.Remove(gpu.Creatures[0].Id);
                    Populate(cpu, coreA, 1, 99UL, 50);
                    Populate(gpu, coreB, 1, 99UL, 50);
                }
            }

            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>Section 8's test 5: a lost body hashes the same on both engines from its loss on.</summary>
        [Fact]
        public void ALostBodyDigestsTheSameOnBothEngines()
        {
            string root = Path.Combine(AppContext.BaseDirectory, "gpu-tests", "digest");
            string dirA = Path.Combine(root, "cpu"), dirB = Path.Combine(root, "gpu");
            foreach (string d in new[] { dirA, dirB })
            {
                if (Directory.Exists(d)) Directory.Delete(d, recursive: true);
                Directory.CreateDirectory(d);
            }

            (DynamicsWorld cpu, World coreA) = Pair(perPart: false, reefs: false);
            (DynamicsWorld gpu, World coreB) = Pair(perPart: false, reefs: false);
            Populate(cpu, coreA, 3, 5UL, 1);
            Populate(gpu, coreB, 3, 5UL, 1);

            // The poison: a non-finite velocity on one link before the first step, which the
            // integration spreads through the body and the finiteness check then loses.
            cpu.Creatures[1].Velocity[0] = double.NaN;
            gpu.Creatures[1].Velocity[0] = double.NaN;
            cpu.Creatures[2].InjectNonFiniteForTest(0);
            gpu.Creatures[2].InjectNonFiniteForTest(0);

            cpu.EnableDigest(dirA, 5, null);
            gpu.EnableDigest(dirB, 5, null);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            for (int block = 0; block < 2; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
            }

            cpu.CloseDigest();
            gpu.CloseDigest();

            Assert.False(cpu.Creatures[1].Alive);
            Assert.False(gpu.Creatures[1].Alive);
            Assert.Equal(cpu.Creatures[2].FirstNonFiniteStep, gpu.Creatures[2].FirstNonFiniteStep);

            string a = File.ReadAllText(Path.Combine(dirA, "digest.jsonl"));
            string b = File.ReadAllText(Path.Combine(dirB, "digest.jsonl"));
            Assert.False(string.IsNullOrEmpty(a));
            Assert.Equal(a, b);
            AssertSame(cpu, gpu, "after the loss");
        }

        /// <summary>
        /// Section 8's test 4: a body over the top class is refused at its first block, counted,
        /// left unstepped as the CPU leaves a lost body, and the others step on.
        /// </summary>
        [Fact]
        public void ABodyOverTheTopClassIsRefusedAndCountedAndTheRestStep()
        {
            (DynamicsWorld gpu, World core) = Pair(perPart: false, reefs: false);
            Populate(gpu, core, 5, 3UL, 1);

            int least = int.MaxValue;
            foreach (Creature c in gpu.Creatures) least = Math.Min(least, Math.Max(GpuSlots.NeuronsOf(c), c.Brain.NeuronCount));

            int over = 0;
            foreach (Creature c in gpu.Creatures) if (Math.Max(GpuSlots.NeuronsOf(c), c.Brain.NeuronCount) > least) over++;
            Assert.True(over > 0, "the draw gave every body the same neuron count");

            GpuOptions options = GpuTank.CpuDouble();
            int ceiling = Math.Max(1, least);
            options.ClassNeurons = new[] { ceiling, ceiling, ceiling, ceiling };

            using var backend = new GpuBackend(gpu.Config, options);
            gpu.UseBackend(backend);

            var before = new Dictionary<int, double>();
            foreach (Creature c in gpu.Creatures) before[c.Id] = c.Position[1];

            gpu.StepBlock(25);
            gpu.StepBlock(25);

            Assert.Equal(over, (int)backend.RefusedForClass);

            foreach (Creature c in gpu.Creatures)
            {
                bool refused = Math.Max(GpuSlots.NeuronsOf(c), c.Brain.NeuronCount) > ceiling;
                Assert.Equal(!refused, c.Alive);
                if (refused) Assert.Equal(before[c.Id], c.Position[1]);
                else Assert.NotEqual(before[c.Id], c.Position[1]);
            }

            Assert.Equal(50L, gpu.Steps);
        }
    }
}
