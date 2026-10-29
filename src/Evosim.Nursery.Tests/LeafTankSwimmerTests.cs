using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm;
using Evosim.Nursery;
using Evosim.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The leaf tank's mouth, rocking and swimmer (logbook/specs/nursery-leaf-tank-plan.md, steps 3, 6 and 4): the episode's <c>eaten</c>
    /// and <c>turn</c> as the nursery's own <c>TankEpisode</c> reads them, swum in-process through <see cref="Restep"/> on the test's tank
    /// (LeafTankFixtures.cs), and the hand-built swimmer in <c>inocula/leaf-tank/</c> with its one-row pool.
    /// </summary>
    [Collection("restep")]
    public class LeafTankSwimmerTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("leaf-swimmer");

        public LeafTankSwimmerTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        /// <summary>The swimmer, repository-relative; its SHA-256 over the bytes as committed (LF), which the pools' README records.</summary>
        public const string Plate = "inocula/leaf-tank/swimmer.json";
        public const string PlateSha256 = "3a937a06b830a2eedcbdf86a8f85bc6dedeb82c5de99bf5a3f3a53ee4e46ab22";

        /// <summary>The one-row pool that holds it as body 0.</summary>
        public const string PlatePool = "rounds/nursery/pools/6a27d8acada28e03.tsv";

        private static string Full(string relative) => Path.Combine(RestepFixtures.Root, relative.Replace('/', Path.DirectorySeparatorChar));

        internal static Genome Swimmer() => GenomeJson.Read(File.ReadAllText(Full(Plate)));

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        private static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }

        /// <summary>A recipe on the test's tank (no leaves unless given) and a pool the test writes; its path.</summary>
        private string Recipe(RunConfig config, int count, Func<int, Genome> bred, string leaves = null, int carrion = 0, double seconds = 2,
            string name = "recipe.json")
        {
            (string path, string hash) = LeafTankFixtures.WriteConfig(_dir, config);
            string pool = LeafTankFixtures.WriteBredPool(_dir, count, bred);
            return LeafTankFixtures.Write(_dir, name, LeafTankFixtures.RecipeJson(path, hash, pool, count, leaves, seconds, carrion));
        }

        /// <summary>The swimmer's root alone: one consumer box, no joint, no mouth (the body that should not rock).</summary>
        internal static Genome Box()
        {
            Genome g = Swimmer();
            g.Nodes[0].Edges.Clear();
            g.Nodes.RemoveAt(1);
            g.Nodes[0].Attack = 0f;
            g.Nodes[0].Intake = 0f;
            return g;
        }

        // ------------------------------------------------------------------ eaten

        /// <summary>Each member's mouth count when the watcher begins and when it ends, and the world's intake at the end.</summary>
        private sealed class Mouths : ITankWatcher
        {
            public double[] First, Last;
            public double UnitsEaten, JoulesPerUnit;

            public void Begin(EpisodeProbe probe)
            {
                First = Enumerable.Range(0, probe.MemberCount).Select(probe.MouthJoules).ToArray();
            }

            public void Physics(EpisodeProbe probe) { }

            public void Metabolic(EpisodeProbe probe) { }

            public void End(EpisodeProbe probe)
            {
                Last = Enumerable.Range(0, probe.MemberCount).Select(probe.MouthJoules).ToArray();
                UnitsEaten = probe.UnitsEaten;
                JoulesPerUnit = probe.JoulesPerUnit;
            }
        }

        /// <summary>The test's tank with corpses that are objects and a reach that covers the tank, so a mouth anywhere eats.</summary>
        private static RunConfig Feeding()
        {
            RunConfig c = LeafTankFixtures.Config(leaves: false);
            c.CorpseDecayPerSecond = 1e-4f;
            c.IntakeReachMetres = 30f;
            return c;
        }

        [Fact]
        public void EatenIsTheMouthsCountOverTheEpisodeAndAMouthlessBodyReadsZero()
        {
            string recipe = Recipe(Feeding(), 3, k => k == 0 ? Swimmer() : RestepFixtures.Founder((ulong)k + 1), carrion: 4);
            var seen = new Mouths();
            RestepOutcome o = LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, seen);
            int swimmer = Array.IndexOf(RestepFixtures.Deal(3, 3, 0, 0), 0);

            for (int k = 0; k < 3; k++)
            {
                RestepMemberOutcome m = o.Members[k];
                _out.WriteLine(FormattableString.Invariant(
                    $"slot {k}{(k == swimmer ? " (the swimmer)" : "")}: landed {m.Landed}, survived {m.Survived}, eaten {m.Eaten:R} J (mouth {seen.First[k]:R} to {seen.Last[k]:R}), food {m.Food:R} J, net {m.Net:R} J"));
                Assert.True(m.Landed);
                Assert.Equal(Bits(seen.Last[k] - seen.First[k]), Bits(m.Eaten));
            }
            Assert.True(o.Members[swimmer].Eaten > 0d, "the swimmer's mouth ate nothing beside four corpses");
            for (int k = 0; k < 3; k++)
                if (k != swimmer) Assert.Equal(0d, o.Members[k].Eaten);

            // Every joule the world's mouths took, less the waste returned to the water, is in the swimmer's count.
            double kept = seen.UnitsEaten * seen.JoulesPerUnit * (1d - Feeding().IntakeWasteFraction);
            Assert.InRange(o.Members[swimmer].Eaten, kept * (1d - 1e-5), kept * (1d + 1e-5));
        }

        // ------------------------------------------------------------------ turn

        /// <summary>
        /// The root's angular speed read through the probe on each economy step the member lived: the same reads the episode makes, so its mean
        /// is the episode's <c>turn</c> to the bit. With <see cref="Spin"/> set, it also holds the root at that spin about the vertical after
        /// every step, which the probe cannot do: the solver is reached by reflection, as nothing outside a test ever may.
        /// </summary>
        private sealed class Rocking : ITankWatcher
        {
            public double Spin = double.NaN;
            public double Sum;
            public int Count;
            public double Mean => Count > 0 ? Sum / Count : double.NaN;

            public void Begin(EpisodeProbe probe) { }

            public void Physics(EpisodeProbe probe) => Hold(probe);

            public void Metabolic(EpisodeProbe probe)
            {
                if (probe.Alive(0) && probe.TryRootSpin(0, out double wx, out double wy, out double wz))
                {
                    Sum += Math.Sqrt(wx * wx + wy * wy + wz * wz);
                    Count++;
                }
                Hold(probe);
            }

            public void End(EpisodeProbe probe) { }

            private void Hold(EpisodeProbe probe)
            {
                if (double.IsNaN(Spin)) return;
                var sim = (Simulation)typeof(EpisodeProbe).GetField("_sim", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(probe);
                var focal = (Organism[])typeof(EpisodeProbe).GetField("_focal", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(probe);
                if (!sim.TryPose(focal[0].Id, out Creature body)) return;
                body.Spin[0] = 0d;
                body.Spin[1] = Spin;
                body.Spin[2] = 0d;
            }
        }

        [Fact]
        public void ABodyAtRestReadsNoTurn()
        {
            string recipe = Recipe(LeafTankFixtures.Config(leaves: false), 1, _ => Box());
            var seen = new Rocking();
            RestepMemberOutcome m = LeafTankFixtures.Swim(_dir, recipe, 1, 1, 0, seen).Members[0];
            _out.WriteLine(FormattableString.Invariant($"at rest: turn {m.Turn:R} rad/s over {seen.Count} steps, path {m.Path:R} m"));

            Assert.True(seen.Count > 0);
            Assert.Equal(Bits(seen.Mean), Bits(m.Turn));
            Assert.InRange(m.Turn, 0d, 1e-3);
        }

        [Fact]
        public void ASpunBodyReadsItsSpin()
        {
            const double spin = 0.8;
            string recipe = Recipe(LeafTankFixtures.Config(leaves: false), 1, _ => Box());
            var seen = new Rocking { Spin = spin };
            RestepMemberOutcome m = LeafTankFixtures.Swim(_dir, recipe, 1, 1, 0, seen).Members[0];
            _out.WriteLine(FormattableString.Invariant($"held at {spin} rad/s: turn {m.Turn:R} rad/s over {seen.Count} steps"));

            Assert.True(seen.Count > 0);
            Assert.Equal(Bits(seen.Mean), Bits(m.Turn));
            // Each read follows one step of the water's drag from the spin held, so it is the spin within that step's loss.
            Assert.InRange(m.Turn, spin * 0.98, spin * 1.0001);
        }

        // ------------------------------------------------------------------ the restep

        [Fact]
        public void ARestepCarriesEveryReadingOfTheEpisodeAndASecondSwimMatchesItToTheBit()
        {
            // Every reading the episode fills in (the nursery's private Resident) is a field of the restep's outcome, by name and type.
            Type resident = typeof(Restep).Assembly.GetType("Evosim.Nursery.Program+Resident", throwOnError: true);
            FieldInfo[] outcome = typeof(RestepMemberOutcome).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo f in resident.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                FieldInfo g = outcome.SingleOrDefault(x => x.Name == f.Name);
                Assert.True(g != null, "the restep's outcome lacks the episode's " + f.Name);
                Assert.Equal(f.FieldType, g.FieldType);
            }
            Assert.Contains(outcome, f => f.Name == "Eaten");
            Assert.Contains(outcome, f => f.Name == "Turn");

            string recipe = Recipe(Feeding(), 3, k => k == 0 ? Swimmer() : RestepFixtures.Founder((ulong)k + 1), carrion: 4);
            RestepOutcome a = LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, null), b = LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, null);
            for (int k = 0; k < 3; k++)
                foreach (FieldInfo f in outcome)
                {
                    object x = f.GetValue(a.Members[k]), y = f.GetValue(b.Members[k]);
                    if (x is double dx) Assert.True(Bits(dx) == Bits((double)y), $"slot {k}'s {f.Name}: {dx:R} then {(double)y:R}");
                    else Assert.Equal(x, y);
                }
            int swimmer = Array.IndexOf(RestepFixtures.Deal(3, 3, 0, 0), 0);
            Assert.True(a.Members[swimmer].Eaten > 0d);
            Assert.True(double.IsFinite(a.Members[swimmer].Turn));
        }

        // ------------------------------------------------------------------ the swimmer

        [LocalDataTreeFact("rounds/nursery/pools/README.md")]
        public void ThePlateIsOneLineInThisBuildsFormatAndItsHashIsTheOneRecorded()
        {
            byte[] bytes = File.ReadAllBytes(Full(Plate));
            string text = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain("\r", text);
            Assert.Equal(GenomeJson.Write(GenomeJson.Read(text)) + "\n", text);
            Assert.Equal(PlateSha256, Sha256(bytes));
            Assert.Contains(PlateSha256, File.ReadAllText(Full("rounds/nursery/pools/README.md")));
        }

        [Fact]
        public void ThePlateGrowsAMouthedRootAndOneLimbOnAVerticalHinge()
        {
            Genome g = Swimmer();
            Phenotype body = Developer.Develop(g);
            Assert.Equal(2, body.PartCount);
            Assert.Equal(1, body.TotalDof);
            PhenotypePart root = body.Parts[0], limb = body.Parts[1];
            Assert.Equal(CellTypeIds.Consumer, root.CellTypeId);
            Assert.True(root.Attack > 0f && root.Intake > 0f, "the root has no mouth");
            Assert.Equal(CellTypeIds.Link, limb.CellTypeId);
            Assert.Equal(JointType.Hinge, limb.JointType);
            Assert.DoesNotContain(body.Parts, p => p.CellTypeId == CellTypeIds.Absorptive || p.CellTypeId == CellTypeIds.Photosynthetic);
            Assert.DoesNotContain(g.Nodes, n => n.CellTypeId == CellTypeIds.Buoyancy || n.Lift != 0f);
            Assert.Empty(g.Brain.Outputs);
            Assert.Empty(g.Brain.Hidden);
            Assert.Empty(g.Brain.Links);
            Assert.Equal(1.3f, g.Reproduction.BirthInvestment);
            Assert.Equal(600f, g.Reproduction.ReserveMargin);

            // In the solver, standing upright: the hinge's axis (the joint frame's x, as the steering checks read it) is vertical, and the
            // limb's length (a capsule's y) lies level and points out from the root's face, 0.56 m from its centre: the root's 0.3 m
            // half-depth and the limb's 0.26 m half-length, r58's link limit.
            var creature = new Creature(0, body, new SolverConfig());
            creature.PlaceAt(new Vec3(0, -5, 0), QuatD.Identity);
            Mat3 link = Mat3.Read(creature.RotationMatrix, 9);
            Vec3 hinge = link * QuatD.Read(creature.JointFrame, 4).Rotate(new Vec3(1, 0, 0));
            Vec3 length = link * new Vec3(0, 1, 0);
            Vec3 offset = Vec3.Read(creature.Position, 3) - Vec3.Read(creature.Position, 0);
            _out.WriteLine(FormattableString.Invariant(
                $"hinge axis ({hinge.X:0.###}, {hinge.Y:0.###}, {hinge.Z:0.###}), limb along ({length.X:0.###}, {length.Y:0.###}, {length.Z:0.###}), limb centre at ({offset.X:0.###}, {offset.Y:0.###}, {offset.Z:0.###}) from the root's"));
            Assert.True(Math.Abs(hinge.Y) > 0.999, $"the hinge axis is not vertical: y {hinge.Y}");
            Assert.True(Math.Abs(length.Y) < 1e-6, $"the limb does not lie level: y {length.Y}");
            Assert.InRange(Math.Abs(Vec3.Dot(length, offset)), 0.559, 0.561);
            Assert.InRange(Math.Abs(offset.Y), 0d, 1e-6);
        }

        [LocalDataTreeFact("rounds/nursery/pools/6a27d8acada28e03.tsv")]
        public void ThePoolIsThePlateAloneAndNamedByItsHash()
        {
            byte[] bytes = File.ReadAllBytes(Full(PlatePool));
            Assert.Equal(Path.GetFileNameWithoutExtension(PlatePool), Sha256(bytes).Substring(0, 16));
            Assert.Equal("0\t" + File.ReadAllText(Full(Plate)), Encoding.UTF8.GetString(bytes));
        }

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz", "rounds/nursery/pools/6a27d8acada28e03.tsv")]
        public void ARecipeWithLeavesTakesThePoolAndRefusesTheSwimmerWithALeafAdded()
        {
            const string leaves = "{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 4, \"spentUnitsPerCubicMetre\": 0}";
            (string config, string hash) = LeafTankFixtures.WriteConfig(_dir, LeafTankFixtures.Config());
            string recipe = LeafTankFixtures.Write(_dir, "pool.json", LeafTankFixtures.RecipeJson(config, hash, PlatePool, 1, leaves, 0.5));
            RestepMemberOutcome m = Assert.Single(LeafTankFixtures.Swim(_dir, recipe, 1, 1, 0, null).Members);
            Assert.True(m.Landed);

            // The same swimmer with a photosynthetic box on the root's far face.
            Genome lit = Swimmer();
            lit.Nodes.Add(new MorphNode
            {
                CellTypeId = CellTypeIds.Photosynthetic, ShapeId = ShapeIds.Box, JointType = JointType.Fixed, RecursiveLimit = 1,
                Dimensions = new Float3(0.2f, 0.05f, 0.2f), JointLimits = Array.Empty<Float2>(),
            });
            lit.Nodes[0].Edges.Add(new MorphEdge
            {
                Id = 2, Child = 2, ParentAnchor = new Float3(0f, 0f, -1f), ChildAnchor = new Float3(0f, 0f, 1f), Orientation = Quat.Identity, Scale = Float3.One,
            });
            lit.NextEdgeId = 3;
            string refused = Recipe(LeafTankFixtures.Config(), 1, _ => lit, leaves, seconds: 0.5, name: "lit.json");
            var e = Assert.Throws<InvalidDataException>(() => LeafTankFixtures.Swim(_dir, refused, 1, 1, 0, null));
            Assert.Contains("carries photosynthetic tissue", e.Message);
            Assert.Contains("the recipe's leaves refuse it", e.Message);
        }
    }

    /// <summary>
    /// D240: the nursery at the world's pace factor k (logbook/specs/nursery-pace-design.md, "Tests"). The tank counts 0.5·k / dt physics steps to a
    /// metabolic step, the watchers and the frames keep world time, a restep's sample rate is per world second, and the stroke's price and the
    /// push are world dials a recipe sets. Swum in-process through <see cref="Restep"/> on the test's tank (LeafTankFixtures.cs), so the nursery's
    /// own <c>TankEpisode</c> builds the solver. The current's tolerance is the design's 2 %. Its home is this file, beside the box it borrows.
    /// </summary>
    [Collection("restep")]
    public class PaceFactorTests : IDisposable
    {
        private const string ThreeTimes = "{\"PaceFactor\": 3}";

        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("pace");

        public PaceFactorTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        private static string Inv(double d) => d.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>A bench recipe on the test's tank at dt <paramref name="dt"/>, its world overrides as JSON (the pace factor among them); its path.</summary>
        private string WriteRecipe(RunConfig config, int count, Func<int, Genome> bred, string overrides = "{}", double dt = 0.01, double seconds = 2,
            double aboveBed = 0.5, string moreControls = "", string name = "recipe.json")
        {
            (string path, string hash) = LeafTankFixtures.WriteConfig(_dir, config);
            string pool = LeafTankFixtures.WriteBredPool(_dir, count, bred);
            return LeafTankFixtures.Write(_dir, name, LeafTankFixtures.RecipeJson(path, hash, pool, count, null, seconds, 0, null, overrides, dt, aboveBed, moreControls));
        }

        private static string PaceOverride(float k) => k == 1f ? "{}" : "{\"PaceFactor\": " + Inv(k) + "}";

        // ------------------------------------------------------------------ the steps and the clock

        /// <summary>Every call a watcher is made: the steps taken, the physics clock and the tank's clock; and what the episode ended on.</summary>
        private sealed class Clocks : ITankWatcher
        {
            public readonly List<(long steps, double physics, double clock)> Calls = new List<(long steps, double physics, double clock)>();
            public int StepsPerMetabolic;
            public long EndSteps;
            public double EndPhysics, EndClock, EndEconomy;

            public void Begin(EpisodeProbe probe) => StepsPerMetabolic = probe.StepsPerMetabolicStep;

            public void Physics(EpisodeProbe probe) => Calls.Add((probe.Steps, probe.PhysicsSeconds, probe.ClockSeconds));

            public void Metabolic(EpisodeProbe probe) => Calls.Add((probe.Steps, probe.PhysicsSeconds, probe.ClockSeconds));

            public void End(EpisodeProbe probe)
            {
                EndSteps = probe.Steps;
                EndPhysics = probe.PhysicsSeconds;
                EndClock = probe.ClockSeconds;
                EndEconomy = probe.EconomySeconds;
            }
        }

        private Clocks SwimClocks(float k, double dt = 0.01, double seconds = 2)
        {
            string recipe = WriteRecipe(LeafTankFixtures.Config(leaves: false), 1, null, PaceOverride(k), dt, seconds, name: "clocks.json");
            var seen = new Clocks();
            LeafTankFixtures.Swim(_dir, recipe, 1, 1, 0, seen);
            return seen;
        }

        [Fact]
        public void AMetabolicStepHoldsHalfAKSecondsOfPhysics()
        {
            // k = 3 at dt 0.01 is 150 steps to a metabolic step, at dt 0.03 is 50, and k = 1 is the 50 it always was. The count is read off the
            // probe the tank built (the Simulation's), and the steps taken in a second of world time are that count twice over.
            Clocks a = SwimClocks(3f, 0.01, 1);
            Assert.Equal(150, a.StepsPerMetabolic);
            Assert.Equal(300, a.EndSteps);

            Clocks b = SwimClocks(3f, 0.03, 1);
            Assert.Equal(50, b.StepsPerMetabolic);
            Assert.Equal(100, b.EndSteps);

            Clocks c = SwimClocks(1f, 0.01, 1);
            Assert.Equal(50, c.StepsPerMetabolic);
            Assert.Equal(100, c.EndSteps);
        }

        [Fact]
        public void ADtThatCannotMakeAMetabolicStepAtTheFactorIsRefusedWhenTheRecipeIsReadByNameAndValue()
        {
            // 0.5 · 3 = 1.5 s of physics to a metabolic step: 0.04 does not divide it (37.5 steps). The refusal is the recipe's, so --check makes it.
            string recipe = WriteRecipe(LeafTankFixtures.Config(leaves: false), 1, null, ThreeTimes, dt: 0.04, name: "refused.json");
            var e = Assert.Throws<InvalidDataException>(() => Recipe.Load(recipe));
            _out.WriteLine(e.Message);
            Assert.Contains("setup.dt", e.Message);
            Assert.Contains("0.04", e.Message);
            Assert.Contains("PaceFactor of 3", e.Message);
            Assert.Contains("Must divide", e.Message);   // what would be accepted, in the farm's own words
        }

        [Fact]
        public void TheTanksClockIsThePhysicsClockToTheBitAtOneAndWorldTimeAtThree()
        {
            Clocks one = SwimClocks(1f), three = SwimClocks(3f);
            _out.WriteLine($"k=1: {one.Calls.Count} calls, ended at clock {Inv(one.EndClock)} (physics {Inv(one.EndPhysics)}, economy {Inv(one.EndEconomy)}); " +
                           $"k=3: {three.Calls.Count} calls, ended at clock {Inv(three.EndClock)} (physics {Inv(three.EndPhysics)}, economy {Inv(three.EndEconomy)})");

            // k = 1: two seconds of 0.01 s steps (a call a step), and at every one of them the clock and the physics clock are the same bits.
            Assert.Equal(200L, one.EndSteps);
            Assert.Equal(one.EndSteps, (long)one.Calls.Count);
            foreach ((long steps, double physics, double clock) in one.Calls)
                Assert.True(Bits(physics) == Bits(clock), $"step {steps}: physics {Inv(physics)}, clock {Inv(clock)}");

            // k = 3: three times the steps for the same world seconds, and the clock is the physics clock over 3, in double.
            Assert.Equal(600L, three.EndSteps);
            Assert.Equal(three.EndSteps, (long)three.Calls.Count);
            foreach ((long steps, double physics, double clock) in three.Calls)
                Assert.True(Bits(physics / 3d) == Bits(clock), $"step {steps}: physics {Inv(physics)}, clock {Inv(clock)}");

            // The episode ends at the recipe's two world seconds at either factor: the economy's clock, and the tank's within the float step's sum
            // (a step of 0.01 f is 0.00999999977..., so 200 of them are 1.99999995 s).
            Assert.InRange(one.EndEconomy, 2d - 1e-6, 2d + 1e-6);
            Assert.InRange(three.EndEconomy, 2d - 1e-6, 2d + 1e-6);
            Assert.InRange(one.EndClock, 2d - 1e-6, 2d + 1e-6);
            Assert.InRange(three.EndClock, 2d - 1e-6, 2d + 1e-6);
        }

        [Fact]
        public void ARestepAtThreeFramesEveryTenthOfAWorldSecondAndItsLastFrameIsTheEpisodesLength()
        {
            string recipe = WriteRecipe(LeafTankFixtures.Config(leaves: false), 1, null, ThreeTimes, seconds: 2, name: "frames.json");
            RestepPlan plan = LeafTankFixtures.Plan(_dir, recipe, 1, 1, 0);

            // 10 Hz of world time at k = 3 and dt 0.01 is a frame every 30 physics steps; a metabolic step is 150.
            Assert.Equal(3f, plan.PaceFactor);
            Assert.Equal(150, plan.StepsPerMetabolicStep);
            Assert.Equal(30, plan.StepsPerSample);
            string described = plan.Describe();
            Assert.Contains("sample rate 10 per second (every 30 steps)\n", described);
            Assert.Contains("pace factor 3, 150 physics steps a metabolic step\n", described);

            string frames = Path.Combine(_dir, "frames.bin"), events = Path.Combine(_dir, "events.jsonl");
            int count, floats;
            using (var recorder = new FrameRecorder(frames, events, plan.SampleRate, plan.StepsPerSample))
            {
                Restep.Swim(plan, recorder);
                count = recorder.FrameCount;
                floats = recorder.Channels.Count;
            }
            byte[] bytes = File.ReadAllBytes(frames);
            Assert.Equal(count * floats * 4, bytes.Length);
            float T(int frame) => BitConverter.ToSingle(bytes, frame * floats * 4);   // the first channel of a frame is t, little-endian float32

            // Two world seconds at 10 Hz: twenty frames, 0.1 s apart from 0.1 s to the episode's length.
            Assert.Equal(20, count);
            Assert.InRange(T(0), 0.1 - 1e-4, 0.1 + 1e-4);
            for (int i = 1; i < count; i++)
                Assert.InRange(T(i) - T(i - 1), 0.1 - 1e-4, 0.1 + 1e-4);
            Assert.InRange(T(count - 1), 2d - 1e-4, 2d + 1e-4);
        }

        [Fact]
        public void ASampleRateIsPerWorldSecondSoOneThatIsNotAWholeNumberOfStepsAtTheFactorIsRefusedNamingIt()
        {
            string recipe = WriteRecipe(LeafTankFixtures.Config(leaves: false), 1, null, ThreeTimes, seconds: 1, name: "rate.json");

            // 3 / (20 × 0.01) = 15 steps: kept. 3 / (7 × 0.01) = 42.86 steps: refused, and the message names the factor.
            Assert.Equal(15, LeafTankFixtures.Plan(_dir, recipe, 1, 1, 0, sampleRate: 20d).StepsPerSample);
            var e = Assert.Throws<InvalidDataException>(() => LeafTankFixtures.Plan(_dir, recipe, 1, 1, 0, sampleRate: 7d));
            Assert.Contains("not a whole number", e.Message);
            Assert.Contains("PaceFactor of 3", e.Message);

            // At k = 1 the same rate is the 1 / (rate × dt) of every restep before: 10 Hz at dt 0.01 is every 10 steps.
            string plain = WriteRecipe(LeafTankFixtures.Config(leaves: false), 1, null, "{}", seconds: 1, name: "rate-1.json");
            RestepPlan one = LeafTankFixtures.Plan(_dir, plain, 1, 1, 0);
            Assert.Equal(10, one.StepsPerSample);
            Assert.Equal(1f, one.PaceFactor);
            Assert.Equal(50, one.StepsPerMetabolicStep);
        }

        // ------------------------------------------------------------------ the water

        // No still-water swimmer here. The design asked for the plate to swim about k times as far a world second at k = 3, but the plate
        // barely swims in a tank: 0.005 m a world second along its path under a constant power of 0.5, and 0.007 under full power, on the
        // runs of 2026-10-26, too little to read a ratio from. The stroke per world second is the solver's, which the pace build tests in
        // still water itself (logbook/specs/pace-build.md); the nursery's share is the step count and the clock, tested above, and the
        // water's seam, tested by the drift below.

        /// <summary>The members' root poses at two instants of the tank's clock, the first at or after <c>t0</c> and the second at or after <c>t1</c>.</summary>
        private sealed class Drift : ITankWatcher
        {
            private readonly double _t0, _t1;
            private bool _a, _b;
            public LinkPose?[] A = Array.Empty<LinkPose?>(), B = Array.Empty<LinkPose?>();
            public double ClockA, ClockB;

            public Drift(double t0, double t1) { _t0 = t0; _t1 = t1; }

            public void Begin(EpisodeProbe probe)
            {
                A = new LinkPose?[probe.MemberCount];
                B = new LinkPose?[probe.MemberCount];
            }

            public void Physics(EpisodeProbe probe) => Take(probe);

            public void Metabolic(EpisodeProbe probe) => Take(probe);

            public void End(EpisodeProbe probe) { }

            private void Take(EpisodeProbe probe)
            {
                double t = probe.ClockSeconds;
                if (!_a && t >= _t0) { _a = true; ClockA = t; Read(probe, A); }
                if (!_b && t >= _t1) { _b = true; ClockB = t; Read(probe, B); }
            }

            private static void Read(EpisodeProbe probe, LinkPose?[] into)
            {
                for (int m = 0; m < into.Length; m++)
                    if (probe.TryLinkPose(m, 0, out LinkPose pose)) into[m] = pose;
            }

            public bool Complete => _a && _b;

            /// <summary>The members' mean distance between the two instants, per world second of the tank's clock between them.</summary>
            public double MeanMetresPerWorldSecond()
            {
                double sum = 0d;
                int n = 0;
                for (int m = 0; m < A.Length; m++)
                {
                    if (A[m] == null || B[m] == null) continue;
                    double dx = B[m].Value.X - A[m].Value.X, dy = B[m].Value.Y - A[m].Value.Y, dz = B[m].Value.Z - A[m].Value.Z;
                    sum += Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    n++;
                }
                return n == 0 ? double.NaN : sum / n / (ClockB - ClockA);
            }
        }

        private double DriftSpeed(float k)
        {
            // The tank's streams at 0.1 m/s, slow in time (a 6,000 s period) as round 42's were, on a tank of 1,600 m² (22.6 m radius) so that the
            // drift does not reach the glass. Four passive boxes (the plate's root alone: no joint, no mouth, no brain, neutral in the water) are
            // let go in it; the first nine world seconds are left for each to take up the water's speed, and the next twenty are measured.
            RunConfig c = LeafTankFixtures.Config(leaves: false);
            c.WorldAreaSquareMetres = 1600f;
            c.Fluid.AddedMassCoefficient = 0.5f;
            c.Fluid.FluidAccelerationCoefficient = 1f;
            c.Current.Mode = CurrentMode.Transport;   // a tank's water is its streams, which the world refuses to read as the box's rolls
            c.Current.Speed = 0.1f;
            c.Current.PeriodSeconds = 6000f;

            string recipe = WriteRecipe(c, 4, _ => LeafTankSwimmerTests.Box(), PaceOverride(k), 0.01, 30, aboveBed: 5, name: "drift.json");
            var seen = new Drift(9d, 29d);
            RestepOutcome o = LeafTankFixtures.Swim(_dir, recipe, 4, 4, 0, seen);
            Assert.True(seen.Complete, "the episode ended before the second instant");
            Assert.All(o.Members, m => Assert.True(m.Survived, "a drifting box died, and left the tank"));
            double speed = seen.MeanMetresPerWorldSecond();
            _out.WriteLine($"k={Inv(k)}: mean {Inv(speed)} m per world second between {Inv(seen.ClockA)} and {Inv(seen.ClockB)} s");
            return speed;
        }

        [Fact]
        public void ABodyAdriftInTheTanksStreamsMovesTheSameDistancePerWorldSecondAtOneAndAtThree()
        {
            // The path from the nursery's tank to Water.cs's seam: the water the solver is handed is the world's divided by k, so a body that
            // follows it covers the same ground in a world second at any k.
            double one = DriftSpeed(1f), three = DriftSpeed(3f);
            _out.WriteLine($"k=1 {Inv(one)} m/s, k=3 {Inv(three)} m/s, ratio {Inv(three / one)}");

            Assert.True(one > 0.02, $"the water barely moved the boxes at k=1 ({Inv(one)} m per world second), so the ratio reads nothing");
            Assert.InRange(three / one, 0.98, 1.02);
        }

        // ------------------------------------------------------------------ the dials

        [Fact]
        public void TheStrokesPriceAndThePushAreWorldDialsARecipeSetsAndTheTanksSolverCarriesThem()
        {
            RunConfig config = LeafTankFixtures.Config(leaves: false);
            Recipe plain = Recipe.Load(WriteRecipe(config, 1, null, "{}", name: "plain.json"));
            const string dialled = "{\"Fluid.LimbPushCoefficient\": 0.9, \"WorkCostMultiplier\": 0.25, \"PaceFactor\": 3}";
            Recipe with = Recipe.Load(WriteRecipe(config, 1, null, dialled, name: "dialled.json"));

            // It loads, and it hashes apart from the recipe without: each dial alone does too.
            Assert.NotEqual(plain.Hash(), with.Hash());
            foreach (string one in new[] { "{\"Fluid.LimbPushCoefficient\": 0.9}", "{\"WorkCostMultiplier\": 0.25}", ThreeTimes })
                Assert.NotEqual(plain.Hash(), Recipe.Load(WriteRecipe(config, 1, null, one, name: "one.json")).Hash());

            // The config the episode is built from carries them, and the plain recipe's does not.
            RunConfig c = with.EpisodeConfig(File.ReadAllText(with.ConfigFullPath));
            RunConfig p = plain.EpisodeConfig(File.ReadAllText(plain.ConfigFullPath));
            Assert.Equal(0.9f, c.Fluid.LimbPushCoefficient);
            Assert.Equal(0.25f, c.WorkCostMultiplier);
            Assert.Equal(3f, c.PaceFactor);
            Assert.NotEqual(c.Hash(), p.Hash());
            Assert.NotEqual(0.9f, p.Fluid.LimbPushCoefficient);
            Assert.NotEqual(0.25f, p.WorkCostMultiplier);
            Assert.Equal(1f, p.PaceFactor);

            // The solver the tank builds (a Simulation, as TankEpisode builds it from the episode's config and the step count) reads the push and
            // the factor from that config, and the world it steps keeps the price.
            var world = new World(c, 1UL);
            EnvBinding.ResolvePhysicsStep(with.Dt, c.PaceFactor, out int steps);
            using (var sim = new Simulation(world, 1UL, with.Dt, steps, 1, null))
            {
                Assert.Equal(0.9d, sim.Solver.LimbPushCoefficient, 5);
                Assert.Equal(3d, sim.Solver.PaceFactor);
                Assert.Equal(150, sim.StepsPerMetabolicStep);
                Assert.Equal(0.25f, world.Config.WorkCostMultiplier);
            }
        }
    }
}
