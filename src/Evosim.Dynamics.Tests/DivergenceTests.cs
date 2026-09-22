using System.IO;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Package F's acceptance: the four guards, the trace's last finite frames, and a dump that
    /// reads like the farm's.
    /// </summary>
    /// <remarks>
    /// Everything written here goes under <c>scratch/farm-port/contacts/dumps/</c>, never into a
    /// run directory: a dump belongs to the run that produced it, and these belong to a test.
    /// </remarks>
    public sealed class DivergenceTests
    {
        private static readonly string Scratch =
            @"D:\Projects\experiments\evolution-simulator\scratch\farm-port\contacts\dumps";

        private readonly ITestOutputHelper _out;

        public DivergenceTests(ITestOutputHelper output) => _out = output;

        /// <summary>A world with a glass and a shaped bed, on a body built by hand.</summary>
        private static SolverConfig World(bool bed = true)
        {
            var config = new SolverConfig
            {
                StepSeconds = 0.01,
                WorldDepthMetres = 45,
                TankRadiusMetres = 26,
                FloorIsSolid = true,
                CreatureContact = true,
                ContactInstrument = true,
            };

            if (bed) config.Bed = new BedShape(26f, 45f, 1.5f, 30f, 0f, RunFixture.BedSeed);

            return config;
        }

        private static Creature Chain(SolverConfig config, int id = 0) =>
            TestBodies.Build(TestBodies.Chain(links: 3), config, id);

        private static Creature Leaf(SolverConfig config, int id = 0) =>
            TestBodies.Build(TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f)), config, id);

        private static Creature Body(SolverConfig config, Vec3 at)
        {
            Creature body = Chain(config);
            body.PlaceAt(at, QuatD.Identity);
            return body;
        }

        [Fact]
        public void AHealthyBodyIsNotDiverged()
        {
            SolverConfig config = World();
            Creature body = Body(config, new Vec3(26, -20, 26));

            Assert.False(Divergence.Diverged(body, config, out string reason));
            Assert.Null(reason);
        }

        [Fact]
        public void TheHeightGuardFires()
        {
            SolverConfig config = World();
            Creature body = Body(config, new Vec3(26, 0, 26));

            // Past the surface by more than the world is deep — World.HeightIsInTheWorld's
            // upper bound, which is the depth above y = 0.
            body.Position[1] = 46;

            Assert.True(Divergence.Diverged(body, config, out string reason));
            _out.WriteLine(reason);
            Assert.Contains("root height", reason);
        }

        [Fact]
        public void TheHeightGuardFiresOnANonFiniteRoot()
        {
            SolverConfig config = World();
            Creature body = Body(config, new Vec3(26, -20, 26));
            body.Position[1] = double.NaN;

            Assert.True(Divergence.Diverged(body, config, out string reason));
            _out.WriteLine(reason);
            Assert.Equal("a non-finite root height", reason);
        }

        [Fact]
        public void TheGlassGuardFires()
        {
            SolverConfig config = World();
            Creature body = Body(config, new Vec3(26, -20, 26));

            // Two metres past the wall, which is one metre past the guard's margin.
            body.Position[0] = 26 + 28;

            Assert.True(Divergence.Diverged(body, config, out string reason));
            _out.WriteLine(reason);
            Assert.Contains("outside the glass", reason);

            // And a body a hand's breadth past the glass is an ordinary depenetration, not a
            // throw — the margin is what keeps the guard from killing a body brushing the wall.
            body.Position[0] = 26 + 26.4;
            Assert.False(Divergence.Diverged(body, config, out _));
        }

        [Fact]
        public void TheBedGuardFiresAndNamesHowFarUnder()
        {
            SolverConfig config = World();
            Creature body = Body(config, new Vec3(26, -20, 26));

            double floorY = config.Bed.FloorY(26, 26);
            body.Position[1] = floorY - body.ContactRadius - 0.5;

            Assert.True(Divergence.Diverged(body, config, out string reason));
            _out.WriteLine(reason);
            Assert.StartsWith("below the bed by ", reason);

            // On a flat world the same body is fine: the guard is the shaped bed's, and D092
            // added nothing at all to the flat path.
            SolverConfig flat = World(bed: false);
            Assert.False(Divergence.Diverged(body, flat, out _));
        }

        [Fact]
        public void TheLinkGuardFires()
        {
            SolverConfig config = World();
            Creature body = Body(config, new Vec3(26, -20, 26));

            body.Position[3 * 2 + 1] = double.PositiveInfinity;

            Assert.True(Divergence.Diverged(body, config, out string reason));
            _out.WriteLine(reason);
            Assert.Equal("link 2 is not finite", reason);
        }

        /// <summary>
        /// An injected non-finite link: the ring refuses the poisoned frames and keeps the three
        /// finite ones before them, and the dump says which step the body left on.
        /// </summary>
        [Fact]
        public void AnInjectedNonFiniteLinkLeavesADumpWithThreeFiniteFrames()
        {
            SolverConfig config = World(bed: false);
            config.CreatureContact = false;

            Creature body = Chain(config);
            body.PlaceAt(new Vec3(26, -20, 26), QuatD.Identity);
            body.EnableTrace();

            var world = new DynamicsWorld(config);
            world.AddInIdOrder(body);

            for (int step = 0; step < 10; step++) world.Step();

            Assert.Equal(Creature.TraceFrames, body.TraceHeld);
            Assert.Equal(-1, body.FirstNonFiniteStep);

            long lastGood = body.TraceStepAt(
                (body.TraceCursor + Creature.TraceFrames - 1) % Creature.TraceFrames);

            body.InjectNonFiniteForTest(link: 2);

            // Two more steps, both of them refused: the ring is left holding the last three
            // steps on which every one of the body's numbers was finite.
            world.Step();
            world.Step();

            Assert.Equal(Creature.TraceFrames, body.TraceHeld);
            Assert.True(body.FirstNonFiniteStep > 0);
            Assert.Equal(2, body.FirstNonFiniteLink);

            // And the same link in the state, so the guard has something to condemn. The two
            // are separate on purpose: the ring's refusal is about what was recorded and the
            // guard is about what the solver holds, and a test that conflated them would pass
            // with either one broken.
            body.Spin[3 * 2] = double.NaN;

            Assert.True(Divergence.Diverged(body, config, out string reason));
            _out.WriteLine($"reason: {reason}, first non-finite step {body.FirstNonFiniteStep}");

            string directory = Path.Combine(Scratch, "injected");
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);

            var dumps = new DivergenceDumps(directory);

            Assert.True(dumps.Write(new DivergedBody
            {
                Body = body,
                Config = config,
                PhysicsStep = world.Steps,
                ElapsedSeconds = world.ElapsedSeconds,
                Reason = reason,
                AgeSeconds = 12.5,
                GenerationDepth = 3,
                LastObservedHeightY = -20,
            }));

            string dump = File.ReadAllText(Path.Combine(directory, body.Id + ".json"));
            string trace = File.ReadAllText(Path.Combine(directory, body.Id + "-trace.json"));

            _out.WriteLine(trace.Substring(0, System.Math.Min(600, trace.Length)));

            Assert.Contains("\"divergenceReason\"", dump);
            Assert.Contains("\"traceOmitted\": null", dump);
            Assert.Contains("\"partStates\"", dump);

            Assert.Contains("\"framesHeld\": 3", trace);
            Assert.Contains("\"framesFinite\": 3", trace);
            Assert.Contains(
                "\"firstNonFiniteStep\": " + body.FirstNonFiniteStep.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                trace);
            Assert.Contains("\"firstNonFiniteLink\": 2", trace);

            // The newest frame the ring kept is the last step before the injection.
            Assert.Contains(
                "\"step\": " + lastGood.ToString(System.Globalization.CultureInfo.InvariantCulture),
                trace);
            Assert.DoesNotContain("NaN", trace);
        }

        /// <summary>An unjointed body keeps no ring, and its post-mortem says why.</summary>
        [Fact]
        public void AnUnjointedBodyHasNoTraceAndTheDumpSaysSo()
        {
            SolverConfig config = World(bed: false);
            Creature body = Leaf(config);
            body.PlaceAt(new Vec3(26, -20, 26), QuatD.Identity);
            body.EnableTrace();

            Assert.False(body.HasTrace);

            string directory = Path.Combine(Scratch, "unjointed");
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);

            var dumps = new DivergenceDumps(directory);

            Assert.True(dumps.Write(new DivergedBody
            {
                Body = body, Config = config, PhysicsStep = 1, ElapsedSeconds = 0.01,
                Reason = "a test",
            }));

            Assert.Contains(
                "no trace: unjointed body",
                File.ReadAllText(Path.Combine(directory, body.Id + ".json")));

            Assert.False(File.Exists(Path.Combine(directory, body.Id + "-trace.json")));
        }

        /// <summary>Fifty dumps and no more — a run counts every divergence and keeps fifty.</summary>
        [Fact]
        public void TheDumpCapIsFifty()
        {
            SolverConfig config = World(bed: false);

            string directory = Path.Combine(Scratch, "cap");
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);

            var dumps = new DivergenceDumps(directory);

            for (int i = 0; i < 60; i++)
            {
                Creature body = Leaf(config, id: i);
                body.PlaceAt(new Vec3(26, -20, 26), QuatD.Identity);

                bool written = dumps.Write(new DivergedBody
                {
                    Body = body, Config = config, PhysicsStep = i + 1, ElapsedSeconds = 0.01 * i,
                });

                Assert.Equal(i < DivergenceDumps.MaxDumps, written);
            }

            Assert.Equal(DivergenceDumps.MaxDumps, dumps.Written);
            Assert.Equal(
                DivergenceDumps.MaxDumps, Directory.GetFiles(directory, "*.json").Length);
        }
    }
}
