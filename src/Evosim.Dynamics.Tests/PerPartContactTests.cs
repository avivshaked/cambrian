using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Per-part contact, D114: <c>logbook/specs/per-part-contact-spec.md</c> section 5, tests 1
    /// to 4. Tests 5 and 6, the regress with the switch off and the screen with it on, are farm
    /// runs and are not here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The forces are read off <see cref="Creature.Fext"/> after one contact pass</b>, called
    /// directly rather than through a step, so nothing else (drag, weight, a drive) is in the
    /// rows. The solver's step clears the rows before its own contact pass, so this is the same
    /// arithmetic the step does, taken by itself.
    /// </para>
    /// <para>
    /// <b>The crowd test reads the recording with the new tunable supplied.</b> Adding
    /// <c>RunConfig.ContactPerPart</c> makes this build refuse <see cref="RunFixture"/>'s
    /// <c>config.json</c>, which is the refuse-rather-than-default rule working. So test 4 reads
    /// the same file with the one field this build added written into its <c>world</c> group, and
    /// the same snapshot's genomes; the fixture itself is re-recorded by the caller, not here.
    /// </para>
    /// </remarks>
    public sealed class PerPartContactTests
    {
        private readonly ITestOutputHelper _out;

        public PerPartContactTests(ITestOutputHelper output) => _out = output;

        // --------------------------------------------------------------- test 1

        /// <summary>
        /// Two one-link bodies touching: the force under per-part contact is the recorded model's
        /// to the bit, on both bodies, in open water and against the bed and the glass.
        /// </summary>
        [Fact]
        public void TwoOnePartBodiesArePushedToTheBitAsTheBodySpherePushesThem()
        {
            // Open water: the pair alone, so the two pushes are each other's reaction.
            (double[] bodyA, double[] bodyB) = OnePartPair(perPart: false, nearWalls: false);
            (double[] partA, double[] partB) = OnePartPair(perPart: true, nearWalls: false);

            Report("open water, body sphere", bodyA, bodyB);
            Report("open water, per part  ", partA, partB);

            Assert.True(Magnitude(bodyA) > 0, "the pair was meant to overlap and was not pushed");
            SameBits(bodyA, partA, "open water, body A");
            SameBits(bodyB, partB, "open water, body B");

            // Equal and opposite, up to the one-link spread's factor m (1/m), which can move the
            // last bit of either side.
            for (int k = 0; k < 3; k++)
            {
                Assert.Equal(-bodyA[3 + k], bodyB[3 + k], 12);
            }

            // Against the bed of a shallow world and the glass of a narrow tank, both at once.
            (double[] wallA, double[] wallB) = OnePartPair(perPart: false, nearWalls: true);
            (double[] partWallA, double[] partWallB) = OnePartPair(perPart: true, nearWalls: true);

            Report("bed and glass, body sphere", wallA, wallB);
            Report("bed and glass, per part  ", partWallA, partWallB);

            SameBits(wallA, partWallA, "bed and glass, body A");
            SameBits(wallB, partWallB, "bed and glass, body B");
        }

        private static (double[] A, double[] B) OnePartPair(bool perPart, bool nearWalls)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.CreatureContact = true;
            config.ContactPerPart = perPart;

            if (nearWalls)
            {
                config.WorldDepthMetres = 5.0;
                config.TankRadiusMetres = 3.0;
            }

            // Two boxes of unequal, unround sizes, so their masses are nothing a factor of one
            // over them would round cleanly.
            Creature a = TestBodies.Build(TestBodies.Box(new Float3(0.13f, 0.11f, 0.17f)), config, id: 3);
            Creature b = TestBodies.Build(TestBodies.Box(new Float3(0.19f, 0.07f, 0.12f)), config, id: 8);

            Vec3 at = nearWalls
                ? new Vec3(config.TankAxisX + 2.8, -4.85, config.TankAxisZ)
                : new Vec3(0.0, -20.0, 0.0);

            a.PlaceAt(at, QuatD.FromAxisAngle(new Vec3(0.3, 1.0, -0.2).Normalized, 0.7));
            b.PlaceAt(at + new Vec3(-0.3, 0.02, 0.05), QuatD.FromAxisAngle(new Vec3(-0.6, 0.2, 1.0).Normalized, 1.9));

            if (nearWalls)
            {
                // Both on the bed, and the first against the glass, or the case tests nothing.
                Assert.True(a.ContactCentre.Y - a.ContactRadius < -config.WorldDepthMetres, "A is off the bed");
                Assert.True(b.ContactCentre.Y - b.ContactRadius < -config.WorldDepthMetres, "B is off the bed");
                Assert.True(
                    a.ContactCentre.X - config.TankAxisX + a.ContactRadius > config.TankRadiusMetres,
                    "A is off the glass");
            }

            // Moving, so the damper is in the sum and not only the spring.
            SetVelocity(a, new Vec3(0.3, -0.2, 0.1));
            SetVelocity(b, new Vec3(-0.1, 0.05, 0.4));

            ContactPass(config, a, b);

            return (Row(a, 0), Row(b, 0));
        }

        // --------------------------------------------------------------- test 2

        /// <summary>
        /// A two-link body whose far link touches a one-link body: the push lands on the far
        /// link's row alone, with the two links' reduced mass, and the root's row reads zero.
        /// </summary>
        [Fact]
        public void APushOnTheFarLinkLandsOnThatLinkWithTheTwoLinksReducedMass()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.CreatureContact = true;
            config.ContactPerPart = true;

            Creature chain = TestBodies.Build(TestBodies.Chain(2, neurons: false), config, id: 1);
            Creature ball = TestBodies.Build(TestBodies.Box(new Float3(0.1f, 0.1f, 0.1f)), config, id: 2);

            chain.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);

            Vec3 root = Vec3.Read(chain.Position, 0);
            Vec3 far = Vec3.Read(chain.Position, 3);
            double farReach = chain.LinkReach[1];
            double ballReach = ball.LinkReach[0];

            // Five centimetres into the far link's sphere, straight out along the chain.
            Vec3 along = (far - root).Normalized;
            ball.PlaceAt(far + along * (farReach + ballReach - 0.05), QuatD.Identity);

            Vec3 ballAt = Vec3.Read(ball.Position, 0);
            Assert.True(
                (ballAt - root).Magnitude > chain.LinkReach[0] + ballReach,
                "the test needs the ball clear of the root's sphere");
            Assert.True(
                (ballAt - root).Magnitude < chain.ContactRadius + ball.ContactRadius,
                "and inside the body sphere, so the two models differ");

            ContactPass(config, chain, ball);

            // By hand, the law in Contacts.ApplyPerPart: the far link's mass against the ball's.
            double mFar = chain.Mass[1];
            double mBall = ball.Mass[0];
            double reduced = mFar * mBall / (mFar + mBall);

            Vec3 between = far - ballAt;
            double distance = between.Magnitude;
            double penetration = farReach + ballReach - distance;
            Vec3 normal = between * (1.0 / distance);
            double stiffness = reduced * config.ContactOmega * config.ContactOmega;
            double approach = 0;   // both at rest
            double damping = 2.0 * config.ContactDampingRatio * reduced * config.ContactOmega;

            Vec3 expected = normal * ContactLaw.PairPush(
                stiffness * penetration - damping * approach, reduced, approach, config);

            double[] rootRow = Row(chain, 0);
            double[] farRow = Row(chain, 1);

            _out.WriteLine(Invariant($"masses: far link {mFar:R} kg, ball {mBall:R} kg, whole chain {chain.TotalMass:R} kg"));
            _out.WriteLine(Invariant($"reduced mass: links {reduced:R} kg, bodies {chain.TotalMass * mBall / (chain.TotalMass + mBall):R} kg"));
            _out.WriteLine(Invariant($"penetration {penetration:R} m"));
            _out.WriteLine("root row " + Format(rootRow));
            _out.WriteLine("far  row " + Format(farRow));
            _out.WriteLine(Invariant($"expected force ({expected.X:R}, {expected.Y:R}, {expected.Z:R})"));

            for (int k = 0; k < 6; k++) Assert.Equal(0.0, rootRow[k]);

            Assert.Equal(0.0, farRow[0]);
            Assert.Equal(0.0, farRow[1]);
            Assert.Equal(0.0, farRow[2]);
            Assert.Equal(expected.X, farRow[3]);
            Assert.Equal(expected.Y, farRow[4]);
            Assert.Equal(expected.Z, farRow[5]);

            Assert.True(Magnitude(farRow) > 0, "the far link was meant to be pushed");

            // And the ball feels the reaction, to within the one-link spread's last bit.
            double[] ballRow = Row(ball, 0);
            for (int k = 0; k < 3; k++) Assert.Equal(-farRow[3 + k], ballRow[3 + k], 12);
        }

        // --------------------------------------------------------------- test 3

        /// <summary>
        /// The giant: a fan of seven copies of one leaf, each 1.75 times the last, with a leaf a
        /// metre from its root and about twelve from its seventh copy. The body sphere pushes the
        /// leaf, because the fan's sphere is centred on its root and reaches the seventh copy;
        /// per-part contact does not, because no part of the fan is near it.
        /// </summary>
        [Fact]
        public void TheGiantsSphereNoLongerShovesALeafItsPartsDoNotTouch()
        {
            (double[] recorded, double recordedFan, int links, double tip) = FanAndLeaf(perPart: false);
            (double[] perPart, double perPartFan, _, _) = FanAndLeaf(perPart: true);

            _out.WriteLine(Invariant($"fan: {links} parts, seventh copy's centre {tip:0.##} m from the leaf"));
            _out.WriteLine("leaf, body sphere " + Format(recorded));
            _out.WriteLine("leaf, per part    " + Format(perPart));
            _out.WriteLine(Invariant($"fan's whole contact force, body sphere {recordedFan:G6} N, per part {perPartFan:G6} N"));

            Assert.Equal(7, links);
            Assert.True(tip > 10.0, "the seventh copy was meant to be far from the leaf");

            Assert.True(Magnitude(recorded) > 0, "the body sphere was meant to push the leaf");
            for (int k = 0; k < 6; k++) Assert.Equal(0.0, perPart[k]);
            Assert.Equal(0.0, perPartFan);
        }

        private static (double[] Leaf, double Fan, int Links, double Tip) FanAndLeaf(bool perPart)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.CreatureContact = true;
            config.ContactPerPart = perPart;

            Creature fan = TestBodies.Build(Fan(), config, id: 7597);
            Creature leaf = TestBodies.Build(TestBodies.Box(new Float3(0.2f, 0.02f, 0.2f)), config, id: 7600);

            fan.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);

            // A metre from the root on the side away from the copies.
            Vec3 root = Vec3.Read(fan.Position, 0);
            Vec3 tipAt = Vec3.Read(fan.Position, 3 * (fan.Links - 1));
            Vec3 away = (root - tipAt).Normalized;
            leaf.PlaceAt(root + away * 1.0, QuatD.Identity);

            ContactPass(config, fan, leaf);

            double fanTotal = 0;
            for (int i = 0; i < fan.Links; i++) fanTotal += Magnitude(Row(fan, i));

            double tip = (Vec3.Read(leaf.Position, 0) - tipAt).Magnitude;

            return (Row(leaf, 0), fanTotal, fan.Links, tip);
        }

        /// <summary>
        /// One thin leaf copying itself off its +X face six times, each copy 1.75 times the last:
        /// the shape of round 44 seed 1's creature 7597 (<c>scratch/logs/giant-7597.txt</c>),
        /// straightened, and built as a determinate chain rather than through the module gene,
        /// since the solver sees only the parts.
        /// </summary>
        private static Genome Fan()
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits
            {
                BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f,
            };

            var leaf = new MorphNode
            {
                Dimensions = new Float3(0.1f, 0.1f, 0.02f),
                CellTypeId = CellTypeIds.Photosynthetic,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 7,
            };

            leaf.Edges.Add(new MorphEdge
            {
                Child = 0,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = new Float3(1.75f, 1.75f, 1.75f),
            });

            genome.Nodes.Add(leaf);
            return genome;
        }

        // --------------------------------------------------------------- test 4

        /// <summary>
        /// The recorded crowd under per-part contact, a thousand bodies for a thousand steps: the
        /// digest at one thread is the digest at sixteen.
        /// </summary>
        [Fact]
        public void PerPartContactDoesNotSeeTheThreadCount()
        {
            Assert.True(Crowd.Present, Crowd.Why);

            ulong one = CrowdDigest(threads: 1, out long pairs, out int alive, out int total, out double cell);
            ulong sixteen = CrowdDigest(threads: 16, out long pairs16, out _, out _, out _);

            _out.WriteLine($"digest  1 thread  {one:x16}");
            _out.WriteLine($"digest 16 threads {sixteen:x16}");
            _out.WriteLine(Invariant($"overlapping body pairs summed over the steps: {pairs} and {pairs16}"));
            _out.WriteLine(Invariant($"link cell {cell:0.###} m; alive at the end {alive} of {total}"));

            Assert.Equal(one, sixteen);
            Assert.Equal(pairs, pairs16);

            // A digest of a world where nothing touched says nothing about the contact's order.
            Assert.True(pairs > 0, "no link of any body touched another's in a thousand steps");
            Assert.True(
                alive >= (total * 95) / 100,
                $"only {alive} of {total} bodies were still finite");
        }

        private static ulong CrowdDigest(
            int threads, out long pairs, out int alive, out int total, out double cell)
        {
            SolverConfig solver = Crowd.Solver(perPart: true, instrument: true);
            Assert.True(solver.ContactPerPart, "the switch did not reach the solver through From");

            DynamicsWorld world = RunFixture.Scatter(
                solver, 1000, threads, drop: 0.2,
                config: Crowd.Config(perPart: true), bodies: Crowd.Bodies);

            for (int step = 0; step < 1000; step++) world.Step();

            pairs = world.OverlapPairs;
            total = world.Creatures.Count;
            alive = total - world.NonFiniteBodies();
            cell = world.ContactCellMetres;

            return world.Digest();
        }

        // --------------------------------------------------------------- the wall reading

        /// <summary>
        /// What the contact phase costs on the same crowd under each model: the grid's build and
        /// every body's contact pass, serially on one thread, before each of 1,000 steps.
        /// </summary>
        /// <remarks>
        /// <b>A reading, not a test of anything</b>, and only honest on an idle machine. The pass
        /// is timed on a grid of its own over the committed spheres the step is about to read, and
        /// its writes into <c>Fext</c> are cleared by the step's own pass, so the trajectory is the
        /// one the world would have taken without it. Slow, so the default run skips it.
        /// </remarks>
        [Fact]
        [Trait("Category", "Slow")]
        public void ContactPhaseWallOnTheCrowdUnderBothModels()
        {
            Assert.True(Crowd.Present, Crowd.Why);

            foreach (bool perPart in new[] { false, true })
            {
                SolverConfig solver = Crowd.Solver(perPart, instrument: false);
                DynamicsWorld world = RunFixture.Scatter(
                    solver, 1000, threads: 8, drop: 0.2,
                    config: Crowd.Config(perPart), bodies: Crowd.Bodies);

                var grid = new ContactGrid();
                int[] scratch = new int[64];
                IReadOnlyList<Creature> bodies = world.Creatures;

                long gridTicks = 0, passTicks = 0, entries = 0;

                for (int step = 0; step < 1000; step++)
                {
                    long t0 = Stopwatch.GetTimestamp();

                    if (perPart) grid.BuildLinks(bodies);
                    else grid.Build(bodies);

                    long t1 = Stopwatch.GetTimestamp();

                    for (int i = 0; i < bodies.Count; i++)
                    {
                        if (!bodies[i].Alive) continue;
                        Contacts.Apply(bodies[i], i, grid, solver, ref scratch);
                    }

                    long t2 = Stopwatch.GetTimestamp();

                    gridTicks += t1 - t0;
                    passTicks += t2 - t1;
                    entries += grid.Entries;

                    world.Step();
                }

                double ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

                _out.WriteLine(
                    Invariant($"{(perPart ? "per part " : "per body ")}: grid {ms(gridTicks):0} ms, pass {ms(passTicks):0} ms, ") +
                    Invariant($"contact phase {ms(gridTicks + passTicks):0} ms over 1,000 steps of {bodies.Count} bodies ") +
                    Invariant($"({world.TotalLinks()} links); mean cell entries {entries / 1000.0:0}, cell {grid.CellSize:0.###} m; ") +
                    Invariant($"the whole step's bodies phase {world.PhaseMs(2)} ms at 8 threads"));
            }
        }

        // --------------------------------------------------------------- the crowd

        /// <summary>
        /// <see cref="RunFixture"/>'s recording, read with <c>world.contactPerPart</c> supplied —
        /// the one field this build added and the recording predates.
        /// </summary>
        private static class Crowd
        {
            private static readonly object Gate = new object();
            private static string _text;
            private static List<Phenotype> _bodies;

            public static bool Present => Why == null;

            public static string Why
            {
                get
                {
                    if (!File.Exists(Path.Combine(RunFixture.RunDirectory, "config.json")) ||
                        !File.Exists(Path.Combine(RunFixture.RunDirectory, "snapshots", RunFixture.Snapshot)))
                    {
                        return "the crowd fixture is not on this machine: " + RunFixture.RunDirectory;
                    }

                    try
                    {
                        Load();
                    }
                    catch (Exception e)
                    {
                        return "this build cannot read the crowd fixture's config even with " +
                               "contactPerPart supplied: " + e.Message;
                    }

                    return _bodies.Count == 0
                        ? "this build refused every genome in the crowd fixture's snapshot"
                        : null;
                }
            }

            public static List<Phenotype> Bodies
            {
                get { Load(); return _bodies; }
            }

            /// <summary>The recorded config with the switch written in at <paramref name="perPart"/>.</summary>
            public static RunConfig Config(bool perPart)
            {
                Load();

                string value = perPart ? "true" : "false";
                string text = _text.Contains("\"contactPerPart\"")
                    ? _text
                    : _text.Replace("\"world\": {", "\"world\": {\n    \"contactPerPart\": " + value + ",");

                RunConfig config = RunConfigJson.Read(text, out _);
                config.ContactPerPart = perPart;
                return config;
            }

            /// <summary>The recorded world for the solver, as <see cref="RunFixture.Solver"/> builds it.</summary>
            public static SolverConfig Solver(bool perPart, bool instrument)
            {
                RunConfig config = Config(perPart);

                var bed = new BedShape(
                    (float)TankGeometry.RadiusFor(config.WorldAreaSquareMetres),
                    config.WorldDepthMetres, config.BedReliefMetres,
                    config.BedTiltMetres, config.BedScaleMetres, RunFixture.BedSeed);

                SolverConfig solver = SolverConfig.FromWorld(config, 0.01, bed);
                solver.ContactInstrument = instrument;
                solver.ContactEvents = instrument;
                return solver;
            }

            private static void Load()
            {
                lock (Gate)
                {
                    if (_bodies != null) return;

                    _text = File.ReadAllText(Path.Combine(RunFixture.RunDirectory, "config.json"));
                    RunConfig config = Config0(_text);

                    var developed = new List<Phenotype>();
                    foreach (string line in File.ReadLines(
                                 Path.Combine(RunFixture.RunDirectory, "snapshots", RunFixture.Snapshot)))
                    {
                        if (line.Length == 0) continue;
                        if (developed.Count >= 1000) break;

                        try
                        {
                            Genome genome = GenomeJson.Read(line);
                            developed.Add(Developer.Develop(genome, config.Development, null, config.Shapes));
                        }
                        catch (Exception)
                        {
                            // RunFixture's own rule: a genome this build refuses is not under test.
                        }
                    }

                    _bodies = developed;
                }
            }

            private static RunConfig Config0(string text) =>
                RunConfigJson.Read(
                    text.Contains("\"contactPerPart\"")
                        ? text
                        : text.Replace("\"world\": {", "\"world\": {\n    \"contactPerPart\": false,"),
                    out _);
        }

        // --------------------------------------------------------------- helpers

        /// <summary>One contact pass over the given bodies, the grid built as the step builds it.</summary>
        private static void ContactPass(SolverConfig config, params Creature[] bodies)
        {
            var list = new List<Creature>(bodies);
            var grid = new ContactGrid();

            if (config.ContactPerPart) grid.BuildLinks(list);
            else grid.Build(list);

            int[] scratch = new int[8];
            for (int i = 0; i < list.Count; i++)
            {
                Array.Clear(list[i].Fext, 0, list[i].Fext.Length);
                Contacts.Apply(list[i], i, grid, config, ref scratch);
            }
        }

        private static void SetVelocity(Creature body, Vec3 v)
        {
            for (int i = 0; i < body.Links; i++) Vec3.Write(body.Velocity, 3 * i, v);
            body.RefreshContactSphere();
            body.CommitContactSphere();
        }

        private static double[] Row(Creature body, int link)
        {
            var row = new double[6];
            Array.Copy(body.Fext, 6 * link, row, 0, 6);
            return row;
        }

        private static double Magnitude(double[] row) =>
            Math.Sqrt(row[3] * row[3] + row[4] * row[4] + row[5] * row[5]);

        private void Report(string what, double[] a, double[] b) =>
            _out.WriteLine(what + ": A " + Format(a) + "  B " + Format(b));

        private static void SameBits(double[] expected, double[] actual, string what)
        {
            for (int k = 0; k < expected.Length; k++)
            {
                long e = BitConverter.DoubleToInt64Bits(expected[k]);
                long a = BitConverter.DoubleToInt64Bits(actual[k]);
                Assert.True(
                    e == a,
                    Invariant($"{what}, component {k}: body sphere {expected[k]:R}, per part {actual[k]:R}"));
            }
        }

        private static string Format(double[] row) =>
            Invariant($"[{row[0]:R}, {row[1]:R}, {row[2]:R} | {row[3]:R}, {row[4]:R}, {row[5]:R}]");

        private static string Invariant(FormattableString s) => FormattableString.Invariant(s);
    }
}
