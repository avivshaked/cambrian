using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Package E's acceptance: a thousand of round 42's bodies on round 42's shaped bed, inside
    /// round 42's glass, counted by an instrument that does not depend on how the work was split.
    /// </summary>
    public sealed class ContactWorldTests
    {
        private readonly ITestOutputHelper _out;

        public ContactWorldTests(ITestOutputHelper output) => _out = output;

        /// <summary>
        /// A thousand bodies dropped over the rock for sixty seconds: none lost, none inside the
        /// rock, none through the glass.
        /// </summary>
        [Fact]
        public void ABodyDroppedOnTheShapedBedStaysOnIt()
        {
            OnTheBed(seconds: 60);
        }

        /// <summary>The same, for ten minutes — the package's own gate.</summary>
        [Fact]
        [Trait("Category", "Slow")]
        public void ABodyDroppedOnTheShapedBedStaysOnItForTenMinutes()
        {
            OnTheBed(seconds: 600);
        }

        private void OnTheBed(double seconds)
        {
            Assert.True(RunFixture.Present, RunFixture.Why);

            SolverConfig solver = RunFixture.Solver();
            BedShape bed = solver.Bed;

            Assert.True(bed.HasRelief);

            DynamicsWorld world = RunFixture.Scatter(solver, 1000, threads: 24);

            int steps = (int)(seconds / solver.StepSeconds);
            double deepest = 0;
            double furthest = 0;

            for (int step = 0; step < steps; step++)
            {
                world.Step();

                // Every step, not every sample: a body the solver loses is taken out of the
                // contact set and stops moving, so a check taken 500 steps later would find a
                // quiet world and a corpse in it.
                if (world.NonFiniteBodies() > 0)
                {
                    for (int i = 0; i < world.Creatures.Count; i++)
                    {
                        Creature lost = world.Creatures[i];
                        if (lost.Alive) continue;

                        _out.WriteLine(
                            $"lost {lost.Id} at step {step}: {lost.Links} links, {lost.Dof} dof, " +
                            $"{lost.TotalMass:0.###} kg");
                    }

                    Assert.Equal(0, world.NonFiniteBodies());
                }

                if (step % 500 != 0 && step != steps - 1) continue;

                for (int i = 0; i < world.Creatures.Count; i++)
                {
                    Creature body = world.Creatures[i];

                    double x = body.Position[0], y = body.Position[1], z = body.Position[2];
                    double under = bed.FloorY(x, z) - body.ContactRadius - y;
                    if (under > deepest) deepest = under;

                    double dx = x - solver.TankAxisX, dz = z - solver.TankAxisZ;
                    double out2 = System.Math.Sqrt(dx * dx + dz * dz) - solver.TankRadiusMetres;
                    if (out2 > furthest) furthest = out2;

                    Assert.False(
                        Divergence.Diverged(body, solver, out string reason),
                        $"creature {body.Id} at step {step}: {reason}");
                }
            }

            _out.WriteLine($"{steps} steps of {world.Creatures.Count} bodies, dt {solver.StepSeconds}");
            _out.WriteLine($"deepest below (floor - radius)  {deepest:0.####} m");
            _out.WriteLine($"furthest past the glass          {furthest:0.####} m");
            _out.WriteLine(
                $"overlap pairs {world.OverlapPairs}, jointed {world.OverlapPairsJointed}, " +
                $"held {world.OverlapPairsHeld}, bodies {world.OverlapBodies}, " +
                $"bed or glass {world.BedOrGlassBodies}");

            Assert.True(deepest <= 0, $"a body reached {deepest:0.###} m under floor - radius");
            Assert.True(furthest <= Divergence.GlassMarginMetres);
        }

        /// <summary>
        /// The five counters and the whole event list, at one thread and at twenty-four.
        /// </summary>
        [Fact]
        public void TheInstrumentAndTheEventListDoNotDependOnTheThreadCount()
        {
            Assert.True(RunFixture.Present, RunFixture.Why);

            (string counters, List<string> events, ulong digest) one = Instrument(1);
            (string counters, List<string> events, ulong digest) many = Instrument(24);

            _out.WriteLine("  1 thread  " + one.counters);
            _out.WriteLine(" 24 threads " + many.counters);
            _out.WriteLine($"last step's pairs: {one.events.Count} and {many.events.Count}");

            Assert.Equal(one.digest, many.digest);
            Assert.Equal(one.counters, many.counters);
            Assert.Equal(one.events, many.events);
        }

        private static (string, List<string>, ulong) Instrument(int threads)
        {
            SolverConfig solver = RunFixture.Solver();

            // Packed on the rock and admitted crowded, so the grid has work to do and the
            // instrument has something to count from the first step. Survival is not what this
            // test asks — a body the solver loses is out of the instrument at both thread counts
            // and the question is only whether the counting agrees.
            DynamicsWorld world = RunFixture.Scatter(solver, 300, threads, drop: 0.2, crowd: true);

            for (int step = 0; step < 1500; step++) world.Step();

            var events = new List<string>();
            long previousA = long.MinValue, previousB = long.MinValue;

            foreach (OverlapPair pair in world.Overlaps)
            {
                // Lower id first, and the whole list ascending — what a bite would consume, and
                // what makes two lists comparable without either being sorted first.
                Assert.True(pair.A < pair.B);
                Assert.True(pair.A > previousA || (pair.A == previousA && pair.B > previousB));

                previousA = pair.A;
                previousB = pair.B;

                events.Add(pair.ToString());
            }

            Assert.Equal(world.OverlapPairsThisStep, world.Overlaps.Count);

            string counters =
                System.FormattableString.Invariant($"pairs {world.OverlapPairs}, ") +
                System.FormattableString.Invariant($"jointed {world.OverlapPairsJointed}, ") +
                System.FormattableString.Invariant($"held {world.OverlapPairsHeld}, ") +
                System.FormattableString.Invariant($"bodies {world.OverlapBodies}, ") +
                System.FormattableString.Invariant($"bed/glass {world.BedOrGlassBodies}");

            return (counters, events, world.Digest());
        }

        /// <summary>
        /// Turning the instrument on changes no trajectory, which is what lets a run be read
        /// without being a different run.
        /// </summary>
        [Fact]
        public void TheInstrumentMovesNothing()
        {
            Assert.True(RunFixture.Present, RunFixture.Why);

            Assert.Equal(Stepped(instrument: false), Stepped(instrument: true));
        }

        private static ulong Stepped(bool instrument)
        {
            SolverConfig solver = RunFixture.Solver(instrument: instrument, events: instrument);
            DynamicsWorld world = RunFixture.Scatter(solver, 200, threads: 4, drop: 0.2);

            for (int step = 0; step < 600; step++) world.Step();
            return world.Digest();
        }

        /// <summary>
        /// A bed with no relief is the flat slab and not a general path with a zero in it —
        /// <c>SeaFloor.Build</c>'s own gate, and <c>PlacementFloor</c>'s.
        /// </summary>
        [Fact]
        public void AReliefLessBedIsTheFlatPath()
        {
            Assert.True(RunFixture.Present, RunFixture.Why);

            RunConfig flat = RunConfigJson.Read(
                RunConfigJson.Write(RunFixture.Config), out _);

            flat.BedReliefMetres = 0f;
            flat.BedTiltMetres = 0f;

            var bed = new BedShape(
                (float)RunFixture.TankRadius, flat.WorldDepthMetres, 0f, 0f, 0f, RunFixture.BedSeed);

            Assert.False(bed.HasRelief);
            Assert.Null(SolverConfig.FromWorld(flat, 0.01, bed).Bed);
            Assert.Null(SolverConfig.FromWorld(flat, 0.01, null).Bed);
        }

        /// <summary>The glass stands at the axis the farm's wall and placer stand at.</summary>
        [Fact]
        public void TheGlassStandsWhereTheFarmPutsIt()
        {
            Assert.True(RunFixture.Present, RunFixture.Why);

            SolverConfig solver = RunFixture.Solver();

            Assert.Equal(TankGeometry.RadiusFor(RunFixture.Config.WorldAreaSquareMetres),
                solver.TankRadiusMetres, 12);
            Assert.Equal(solver.TankRadiusMetres, solver.TankAxisX, 12);
            Assert.Equal(solver.TankRadiusMetres, solver.TankAxisZ, 12);
        }
    }
}
