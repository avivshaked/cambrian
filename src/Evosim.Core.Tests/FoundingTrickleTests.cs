using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D115, the founding trickle — <c>logbook/specs/founding-trickle-spec.md</c> §1 and §3's
    /// tests 1 to 3, the parts of them a Core world can answer. The regress of test 1's second
    /// clause (a world at rate 0 is bit for bit the recorded one on the crowd fixture) is a farm
    /// run and is not here; the farm's half of test 3, the ending, is in
    /// <c>Evosim.Farm.Tests.FoundingTrickleEndingTests</c>.
    /// </summary>
    public class FoundingTrickleTests
    {
        private readonly ITestOutputHelper _output;

        public FoundingTrickleTests(ITestOutputHelper output) => _output = output;

        private const float OnePerThirty = 1f / 30f;
        private const float MetabolicStep = 0.5f;

        /// <summary>A small cell world whose floor closes early and whose trickle is on.</summary>
        private static RunConfig TrickleWorld(float rate, float closes = 50f) => new RunConfig
        {
            Light = new LightModel(120f, 12f),
            MinimumPopulation = 20,
            MaximumPopulation = 100_000,
            FloorClosesAfterSeconds = closes,
            FoundingTricklePerSecond = rate,
        };

        // ------------------------------------------------------------------ test 1: the draw

        /// <summary>
        /// At one per 30 s over 30,000 s the trickle adds about a thousand founders, and the same
        /// seed draws the same count on every step.
        /// </summary>
        [Fact]
        public void TheDrawAtOnePerThirtySecondsAddsAboutAThousandARunAndRepeats()
        {
            const int Steps = (int)(30_000f / MetabolicStep);
            double mean = (double)OnePerThirty * MetabolicStep;

            foreach (ulong seed in new ulong[] { 1UL, 2UL, 3UL })
            {
                var first = new Rng(Rng.SeedFor(seed, World.TrickleIndex));
                var again = new Rng(Rng.SeedFor(seed, World.TrickleIndex));

                long total = 0;
                int mostInOneStep = 0;

                for (int step = 0; step < Steps; step++)
                {
                    int a = World.TrickleCount(first, mean);
                    int b = World.TrickleCount(again, mean);

                    Assert.Equal(a, b);
                    total += a;
                    if (a > mostInOneStep) mostInOneStep = a;
                }

                _output.WriteLine(
                    $"seed {seed}: {total} founders in {Steps} steps of 0.5 s at 1/30 s " +
                    $"(most in one step {mostInOneStep})");

                Assert.InRange(total, 900L, 1100L);
            }
        }

        /// <summary>
        /// At a mean of 0 the draw takes nothing from the stream, which is what keeps a world at
        /// rate 0 off its trickle stream entirely.
        /// </summary>
        [Fact]
        public void AtRateZeroTheStreamIsNotDrawn()
        {
            var rng = new Rng(Rng.SeedFor(7UL, World.TrickleIndex));
            ulong before = rng.State;

            Assert.Equal(0, World.TrickleCount(rng, 0d));
            Assert.Equal(before, rng.State);

            // And a world with the trickle off, past the floor's closing, spawns nobody by it.
            var world = new World(TrickleWorld(rate: 0f), seed: 5);
            for (int i = 0; i < 400; i++) world.Step(MetabolicStep);

            Assert.False(world.FoundersStillArriving);
            Assert.Equal(0L, world.TrickleSpawns);
        }

        /// <summary>
        /// The world's trickle is the stream the test above reads: the attempts it counts after
        /// the floor closes are the sum of the draws a fresh stream at its seed gives over the
        /// same steps.
        /// </summary>
        [Fact]
        public void TheWorldsTrickleIsItsOwnStream()
        {
            const float Rate = 0.4f;
            const float Closes = 50f;
            const int Steps = 800;
            const ulong Seed = 9UL;

            var world = new World(TrickleWorld(Rate, Closes), Seed);
            var stream = new Rng(Rng.SeedFor(Seed, World.TrickleIndex));

            long expected = 0;
            for (int i = 0; i < Steps; i++)
            {
                world.Step(MetabolicStep);
                if (world.ElapsedSeconds >= Closes)
                {
                    expected += World.TrickleCount(stream, (double)Rate * MetabolicStep);
                }
            }

            _output.WriteLine(
                $"trickle attempts {world.TrickleSpawns} (the stream says {expected}); " +
                $"floor spawns {world.FloorSpawns}; alive {world.Living.Count}");

            Assert.True(expected > 0);
            Assert.Equal(expected, world.TrickleSpawns);
        }

        // ------------------------------------------------------------------ test 2: the books

        /// <summary>
        /// A trickle founder is booked as a floor founder is — its reserve and tissue over ρ into
        /// the matter influx, step by step — and its lineage row reads <c>k: "f"</c> and
        /// <c>src: "trickle"</c>, where the floor's founders read <c>src: "floor"</c>.
        /// </summary>
        [Fact]
        public void ATrickleFounderIsBookedAndLabelledAsAFounder()
        {
            var world = new World(TrickleWorld(rate: 0.5f, closes: 30f), seed: 4);

            int floorRows = 0;
            int trickleRows = 0;
            int checkedSteps = 0;

            for (int i = 0; i < 600; i++)
            {
                double influxBefore = world.MatterInfluxedTotal;
                world.Step(MetabolicStep);

                var living = new Dictionary<long, Organism>();
                foreach (Organism c in world.Living) living[c.Id] = c;

                double tricklePurse = 0d;
                bool anyFloorRow = false;
                bool anyTrickleRow = false;

                foreach (LineageEvent evt in world.DrainLineageEvents())
                {
                    if (evt.Kind != LineageEventKind.Birth || evt.BirthKind != BirthKind.Floor) continue;

                    string json = evt.ToJson();
                    Assert.Contains("\"k\":\"f\"", json);

                    if (evt.Source == FounderSource.Trickle)
                    {
                        Assert.Contains("\"src\":\"trickle\"", json);
                        Assert.Equal(-1L, evt.ParentId);
                        Assert.Equal(0, evt.GenerationDepth);

                        Organism founder = living[evt.Id];
                        tricklePurse += founder.Energy + founder.TissueJoules;
                        trickleRows++;
                        anyTrickleRow = true;
                    }
                    else
                    {
                        Assert.Equal(FounderSource.Floor, evt.Source);
                        Assert.Contains("\"src\":\"floor\"", json);
                        floorRows++;
                        anyFloorRow = true;
                    }
                }

                // A step after the floor has closed, in which the trickle added someone: the whole
                // of the influx is the trickle founders' purses over ρ (the world has no other
                // influx), exactly as leg 9 books a floor founder's.
                if (anyTrickleRow && !anyFloorRow)
                {
                    double influx = world.MatterInfluxedTotal - influxBefore;
                    Assert.Equal(tricklePurse / world.Config.JoulesPerUnit, influx, 9);
                    checkedSteps++;
                }
            }

            _output.WriteLine(
                $"floor rows {floorRows}, trickle rows {trickleRows}, steps checked {checkedSteps}; " +
                $"floor spawns {world.FloorSpawns}, trickle spawns {world.TrickleSpawns}");

            Assert.True(floorRows > 0, "the floor founded nobody");
            Assert.True(trickleRows > 0, "the trickle founded nobody");
            Assert.True(checkedSteps > 0);

            // An inoculant and a child carry no source.
            string birth = LineageEvent.Birth(
                1d, 1L, 0L, BirthKind.Reproduction, 1, 0u, false, false, true, 0,
                1f, 1f, 0f, 0, false, false, false).ToJson();
            Assert.DoesNotContain("\"src\"", birth);
        }

        /// <summary>
        /// Both books close over 3,000 s of a grid world whose floor closes at 500 s and whose
        /// trickle then runs at one per 5 s: the founders it adds are income both books see.
        /// </summary>
        [Fact]
        public void BothBooksCloseUnderTheTrickle()
        {
            const float Area = 144f;
            const int Patches = 4;
            const float Depth = 24f;

            var config = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = Patches,
                WorldDepthMetres = Depth,
                NutrientMixingDiffusivity = 0.2f,
                HorizontalMixingDiffusivity = 0.2f,
                MatterMixingDiffusivity = 0.2f,
                FloorClosesAfterSeconds = 500f,
                FoundingTricklePerSecond = 0.2f,
            };

            var world = new World(config, seed: 3);
            for (int step = 0; step < 6_000; step++) world.Step(MetabolicStep);

            double identity = world.MatterResidual;

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births}, floor {world.FloorSpawns}, " +
                $"trickle {world.TrickleSpawns}; audit {world.AuditResidual:R} of " +
                $"{world.EnergyIn:0} J in; matter identity {identity:R} of " +
                $"{world.MatterInitialTotal:0} + {world.MatterInfluxedTotal:0.###} in");

            Assert.True(world.TrickleSpawns > 0);
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(identity) <= 1e-6 * (world.MatterInitialTotal + world.MatterInfluxedTotal),
                $"matter identity {identity:R}");
        }

        // ------------------------------------------------------------------ test 3: the floor and the empty world

        /// <summary>
        /// The floor closes as it did with the trickle on: no floor spawn after the closing,
        /// while the trickle keeps adding.
        /// </summary>
        [Fact]
        public void TheFloorClosesAsBeforeWithTheTrickleOn()
        {
            const float Closes = 40f;
            var world = new World(TrickleWorld(rate: 0.3f, closes: Closes), seed: 6);

            long floorAtClose = -1;
            for (int i = 0; i < 600; i++)
            {
                world.Step(MetabolicStep);
                if (floorAtClose < 0 && world.ElapsedSeconds >= Closes) floorAtClose = world.FloorSpawns;
            }

            _output.WriteLine(
                $"floor spawns {world.FloorSpawns} (at the closing {floorAtClose}), " +
                $"trickle {world.TrickleSpawns}");

            Assert.True(floorAtClose > 0);
            Assert.Equal(floorAtClose, world.FloorSpawns);
            Assert.True(world.TrickleSpawns > 0);
        }

        /// <summary>
        /// A world emptied by hand under the trickle is still being founded and is refounded; the
        /// same world with the trickle off is not.
        /// </summary>
        [Fact]
        public void AWorldEmptiedUnderTheTrickleIsRefounded()
        {
            World Emptied(float rate)
            {
                var world = new World(TrickleWorld(rate, closes: 20f), seed: 8);
                for (int i = 0; i < 60; i++) world.Step(MetabolicStep);

                foreach (Organism c in new List<Organism>(world.Living)) world.KillDiverged(c);
                Assert.Empty(world.Living);
                return world;
            }

            World on = Emptied(0.5f);
            World off = Emptied(0f);

            Assert.True(on.FoundersStillArriving);
            Assert.False(off.FoundersStillArriving);

            long trickleBefore = on.TrickleSpawns;
            int steps = 0;
            while (on.Living.Count == 0 && steps < 400)
            {
                on.Step(MetabolicStep);
                off.Step(MetabolicStep);
                steps++;
            }

            _output.WriteLine(
                $"refounded after {steps} steps: alive {on.Living.Count}, trickle attempts " +
                $"{on.TrickleSpawns - trickleBefore}; the world without it: alive {off.Living.Count}");

            Assert.NotEmpty(on.Living);
            Assert.True(on.TrickleSpawns > trickleBefore);
            Assert.Empty(off.Living);
        }

        // ------------------------------------------------------------------ the refusals

        [Fact]
        public void ATrickleOfMoreThanOneFounderAStepIsRefused()
        {
            var config = new RunConfig();
            config.FoundingTricklePerSecond = 2f;
            Assert.Equal(2f, config.FoundingTricklePerSecond);

            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingTricklePerSecond = 2.01f);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingTricklePerSecond = -0.1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingTricklePerSecond = float.NaN);

            // A world stepped at more than the metabolic step would break the same bound.
            var world = new World(TrickleWorld(rate: 0.5f, closes: 1f), seed: 1);
            world.Step(MetabolicStep);
            world.Step(MetabolicStep);
            Assert.Throws<InvalidOperationException>(() => world.Step(3f));
        }

        [Fact]
        public void ATrickleWithoutTheFloorsSpawningIsRefused()
        {
            RunConfig config = TrickleWorld(OnePerThirty);
            config.FloorSpawnsPerStep = 0;

            Assert.Throws<ArgumentException>(() => new World(config, seed: 1));
        }

        [Fact]
        public void ATrickleUnderAFloorThatNeverClosesIsRefused()
        {
            RunConfig config = TrickleWorld(OnePerThirty);
            config.FloorClosesAfterSeconds = 0f;

            Assert.Throws<ArgumentException>(() => new World(config, seed: 1));
        }
    }
}
