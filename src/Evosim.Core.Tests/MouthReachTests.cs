using System;
using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D214's dial, <see cref="RunConfig.IntakeReachFromMouth"/>: with it on a corpse is in reach of a body when
    /// any of the posed mouth points the harness handed Core lies within the corpse's radius plus the reach,
    /// each from its own point. Off, the reach is measured from the body's centre as it always was, and the
    /// points are neither stored nor read. Core poses nothing, so these tests hand it the points.
    /// </summary>
    // World.IntakeByColumns and Parallelism.Threads are statics: this collection runs their tests one at a time.
    [Collection("Parallelism.Threads")]
    public class MouthReachTests
    {
        private readonly ITestOutputHelper _output;

        public MouthReachTests(ITestOutputHelper output) => _output = output;

        private const float Reach = 1f;

        private static RunConfig Box(float reach, bool fromMouth) => new RunConfig
        {
            Light = new LightModel(100f, 12f),
            SharedSpace = true,
            FieldModel = MatterField.Grid,
            WorldAreaSquareMetres = 144f,
            HorizontalPatches = 4,
            WorldDepthMetres = 24f,
            InitialMatterPerCubicMetre = 0f,
            MinimumPopulation = 0,
            MaximumPopulation = 100_000,
            CorpseDecayPerSecond = 0.001f,
            IntakeReachMetres = reach,
            IntakeWasteFraction = 0.25f,
            IntakeReachFromMouth = fromMouth,
        };

        // A world with one corpse at `corpseAt` and one mouth with its centre at `centre`. The corpse is a
        // 0.4 m box's worth of tissue, so its sphere-equivalent radius is about a quarter of a metre and its
        // reach about 1.25 m.
        private static World OneCorpseAndOneMouth(
            RunConfig config, Float3 corpseAt, Float3 centre, out Organism mouth)
        {
            var world = new World(config, seed: 11);

            world.Inoculate(Fixtures.ArmedBox(half: 0.2f), 1, heightY: -2f);
            Organism dying = world.Living[0];
            world.Observe(dying, corpseAt, 0f);
            world.KillDiverged(dying);
            Assert.Single(world.Corpses);

            world.Inoculate(
                Fixtures.ArmedBox(intake: 1f, half: 0.2f, cellTypeId: CellTypeIds.Consumer), 1, heightY: -2f);
            mouth = world.Living.Single(c => c.HasIntake);
            world.Observe(mouth, centre, 0f);
            return world;
        }

        private static void Hand(World world, Organism mouth, params Float3[] points)
        {
            var flat = new float[3 * points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                flat[3 * i] = points[i].X;
                flat[3 * i + 1] = points[i].Y;
                flat[3 * i + 2] = points[i].Z;
            }

            world.ObserveMouths(mouth, flat, points.Length);
        }

        private static bool Ate(World world)
        {
            world.ApplyMouth(0.5f);
            return world.UnitsEaten > 0d;
        }

        private static readonly Float3 Corpse = new Float3(6f, -2f, 3f);
        private static readonly Float3 Near = new Float3(6.5f, -2f, 3f);
        private static readonly Float3 Far = new Float3(2f, -2f, 3f);

        // ---------------------------------------------------------------- off

        [Fact]
        public void OffTheCentreDecidesAndThePointsAreNeitherStoredNorRead()
        {
            // The mouth point is in reach and the centre is not: off, nothing is eaten, and the handed
            // points are dropped without being kept.
            World world = OneCorpseAndOneMouth(Box(Reach, fromMouth: false), Corpse, Far, out Organism mouth);
            Hand(world, mouth, Near);

            Assert.Null(mouth.MouthPoints);
            Assert.Equal(0, mouth.MouthPointCount);
            Assert.False(Ate(world));
            Assert.Equal(0L, world.MouthPointFallbacks);

            // The converse: the centre is in reach and the point is not, and it eats.
            World second = OneCorpseAndOneMouth(Box(Reach, fromMouth: false), Corpse, Near, out Organism other);
            Hand(second, other, Far);
            Assert.True(Ate(second));
        }

        [Fact]
        public void OffAWorldGivesTheSameBitsWhetherOrNotPointsAreHanded()
        {
            Eaten without = Run(fromMouth: false, handPoints: false, columns: true, steps: 6);
            Eaten with = Run(fromMouth: false, handPoints: true, columns: true, steps: 6);

            Assert.True(BitConverter.Int64BitsToDouble(without.Units) > 0d, "nothing was eaten, so nothing was tested");
            AssertSame(without, with);
        }

        // ---------------------------------------------------------------- on

        [Fact]
        public void OnABodyWhoseCentreIsOutOfReachButWhoseMouthPointIsInReachEats()
        {
            World world = OneCorpseAndOneMouth(Box(Reach, fromMouth: true), Corpse, Far, out Organism mouth);
            Hand(world, mouth, Near);

            Assert.Equal(1, mouth.MouthPointCount);
            Assert.True(Ate(world), "the mouth point was in reach and nothing was eaten");
            Assert.Equal(0L, world.MouthPointFallbacks);
        }

        [Fact]
        public void OnABodyWhoseCentreIsInReachButWhoseMouthPointIsNotEatsNothing()
        {
            World world = OneCorpseAndOneMouth(Box(Reach, fromMouth: true), Corpse, Near, out Organism mouth);
            Hand(world, mouth, Far);

            Assert.False(Ate(world), "the mouth point was out of reach and the body ate from its centre");
            Assert.Equal(0L, world.MouthPointFallbacks);
        }

        [Fact]
        public void OnTheHeightCountsToo()
        {
            // The point is level with the corpse's x and z and 3 m above it: out of reach by y alone.
            World world = OneCorpseAndOneMouth(Box(Reach, fromMouth: true), Corpse, Near, out Organism mouth);
            Hand(world, mouth, new Float3(6f, 1f, 3f));

            Assert.False(Ate(world));
        }

        [Fact]
        public void OnTwoEatingPartsEitherOneInReachIsEnough()
        {
            World world = OneCorpseAndOneMouth(Box(Reach, fromMouth: true), Corpse, Far, out Organism mouth);
            Hand(world, mouth, Far, Near);
            Assert.True(Ate(world), "the second point was in reach");

            World reversed = OneCorpseAndOneMouth(Box(Reach, fromMouth: true), Corpse, Far, out Organism other);
            Hand(reversed, other, Near, Far);
            Assert.True(Ate(reversed), "the first point was in reach");

            World neither = OneCorpseAndOneMouth(Box(Reach, fromMouth: true), Corpse, Near, out Organism none);
            Hand(neither, none, Far, new Float3(10f, -2f, 3f));
            Assert.False(Ate(neither), "neither point was in reach");
        }

        [Fact]
        public void OnTheLastPointsHandedReplaceTheEarlierOnes()
        {
            World world = OneCorpseAndOneMouth(Box(Reach, fromMouth: true), Corpse, Far, out Organism mouth);
            Hand(world, mouth, Near, Near);
            Hand(world, mouth, Far);

            Assert.Equal(1, mouth.MouthPointCount);
            Assert.False(Ate(world));
        }

        [Fact]
        public void OnABodyWithNoPointsIsMeasuredFromItsCentreAndCounted()
        {
            World inReach = OneCorpseAndOneMouth(Box(Reach, fromMouth: true), Corpse, Near, out Organism mouth);
            Assert.Equal(0, mouth.MouthPointCount);
            Assert.True(Ate(inReach), "a body with no points was not measured from its centre");
            Assert.Equal(1L, inReach.MouthPointFallbacks);

            World outOfReach = OneCorpseAndOneMouth(Box(Reach, fromMouth: true), Corpse, Far, out Organism other);
            Assert.False(Ate(outOfReach));
            Assert.Equal(1L, outOfReach.MouthPointFallbacks);

            // Handing a count of zero is the same as handing nothing, and a second pass counts again.
            Hand(outOfReach, other);
            Assert.False(Ate(outOfReach));
            Assert.Equal(2L, outOfReach.MouthPointFallbacks);
        }

        [Fact]
        public void OnTheSeamsShorterArcApplies()
        {
            // The box is 24 m long (x) and 6 m wide (z). The corpse is at x = 0.3 and the point at x = 23.8: 0.5 m
            // apart across the seam and 23.5 m apart plainly. The centre is mid-box, far from both.
            var centre = new Float3(12f, -2f, 3f);

            World world = OneCorpseAndOneMouth(
                Box(Reach, fromMouth: true), new Float3(0.3f, -2f, 3f), centre, out Organism mouth);
            Assert.Equal(24f, world.Nutrients.LengthMetres);
            Assert.Equal(6f, world.Nutrients.WidthMetres);
            Hand(world, mouth, new Float3(23.8f, -2f, 3f));
            Assert.True(Ate(world), "the shorter arc across the seam was not taken on x");

            // And the same on z, 5.8 m and 0.3 m in a box 6 m wide.
            World alongZ = OneCorpseAndOneMouth(
                Box(Reach, fromMouth: true), new Float3(12f, -2f, 5.8f), centre, out Organism other);
            Hand(alongZ, other, new Float3(12f, -2f, 0.3f));
            Assert.True(Ate(alongZ), "the shorter arc across the seam was not taken on z");

            // A point 3 m from the corpse across neither seam is out of reach on the same box.
            World apart = OneCorpseAndOneMouth(
                Box(Reach, fromMouth: true), new Float3(0.3f, -2f, 3f), centre, out Organism third);
            Hand(apart, third, new Float3(3.3f, -2f, 3f));
            Assert.False(Ate(apart));
        }

        // ---------------------------------------------------------------- the columns

        private sealed class Eaten
        {
            public long[] Corpses;
            public long[] Mouths;
            public long Units, Waste, Snow;
            public long CorpsesEaten;
        }

        // The box is 24 m long and 6 m wide; a share of the positions lie within 0.6 m of a seam.
        private static Float3 Anywhere(Rng rng)
        {
            float x = rng.Range(0f, 24f), z = rng.Range(0f, 6f);
            if (rng.Range(0f, 1f) < 0.3f) x = rng.Range(0f, 1f) < 0.5f ? rng.Range(0f, 0.6f) : rng.Range(23.4f, 24f);
            if (rng.Range(0f, 1f) < 0.3f) z = rng.Range(0f, 1f) < 0.5f ? rng.Range(0f, 0.6f) : rng.Range(5.4f, 6f);
            return new Float3(x, -rng.Range(1f, 4f), z);
        }

        // Each mouth's centre is anywhere, and its points lie up to 2.5 m from it, several column-widths at the
        // short reach, in every direction: a point's column is often not the centre's column or its
        // neighbour's. With `handPoints` off, no mouth is handed any.
        private static Eaten Run(bool fromMouth, bool handPoints, bool columns, int steps, float reach = 0.5f)
        {
            int was = Parallelism.Threads;
            bool wasColumns = World.IntakeByColumns;
            try
            {
                Parallelism.Threads = 4;
                World.IntakeByColumns = columns;
                var world = new World(Box(reach, fromMouth), seed: 11);
                var rng = new Rng(7);

                // The points have a stream of their own, so handing them or not leaves the world's positions alone.
                var pointRng = new Rng(99);

                world.Inoculate(Fixtures.ArmedBox(half: 0.2f), 600, heightY: -2f);
                List<Organism> dying = world.Living.ToList();
                foreach (Organism body in dying)
                {
                    world.Observe(body, Anywhere(rng), 0f);
                    world.KillDiverged(body);
                }

                world.Inoculate(
                    Fixtures.ArmedBox(intake: 1f, half: 0.2f, cellTypeId: CellTypeIds.Consumer), 80, heightY: -2f);
                List<Organism> mouths = world.Living.Where(c => c.HasIntake).ToList();
                Assert.Equal(80, mouths.Count);

                int pointless = 0;
                foreach (Organism mouth in mouths)
                {
                    Float3 centre = Anywhere(rng);
                    world.Observe(mouth, centre, 0f);
                    if (!handPoints) continue;

                    // Every fifth mouth is handed none, to take the centre's fallback beside the points.
                    if (pointless++ % 5 == 4) continue;

                    int count = 1 + pointRng.Range(3);
                    var flat = new float[3 * count];
                    for (int k = 0; k < count; k++)
                    {
                        flat[3 * k] = centre.X + pointRng.Range(-2.5f, 2.5f);
                        flat[3 * k + 1] = centre.Y + pointRng.Range(-0.5f, 0.5f);
                        flat[3 * k + 2] = centre.Z + pointRng.Range(-2.5f, 2.5f);

                        // Points are in Core's frame as the solver leaves them, which may be outside the box.
                        // The world's own wrap is the seam's shorter arc, so no wrapping is done here.
                    }

                    world.ObserveMouths(mouth, flat, count);
                }

                List<Corpse> corpses = world.Corpses.ToList();
                for (int step = 0; step < steps; step++) world.ApplyMouth(0.5f);

                return new Eaten
                {
                    Corpses = corpses.Select(c => BitConverter.DoubleToInt64Bits(c.Joules)).ToArray(),
                    Mouths = mouths.Select(m => BitConverter.DoubleToInt64Bits(m.Energy)).ToArray(),
                    Units = BitConverter.DoubleToInt64Bits(world.UnitsEaten),
                    Waste = BitConverter.DoubleToInt64Bits(world.DetritusDepositedTotal),
                    Snow = BitConverter.DoubleToInt64Bits(world.Nutrients.TotalJoules),
                    CorpsesEaten = world.CorpsesEaten,
                };
            }
            finally
            {
                Parallelism.Threads = was;
                World.IntakeByColumns = wasColumns;
            }
        }

        private static void AssertSame(Eaten a, Eaten b)
        {
            Assert.Equal(a.Corpses, b.Corpses);
            Assert.Equal(a.Mouths, b.Mouths);
            Assert.Equal(a.Units, b.Units);
            Assert.Equal(a.Waste, b.Waste);
            Assert.Equal(a.Snow, b.Snow);
            Assert.Equal(a.CorpsesEaten, b.CorpsesEaten);
        }

        private void OnTheColumnsEatTheWalksBits(float reach, int steps)
        {
            Eaten walk = Run(fromMouth: true, handPoints: true, columns: false, steps, reach);
            Eaten cols = Run(fromMouth: true, handPoints: true, columns: true, steps, reach);

            double units = BitConverter.Int64BitsToDouble(walk.Units);
            _output.WriteLine($"reach {reach} m, points: {walk.Corpses.Length} corpses, {walk.Mouths.Length} mouths, {units:0.####} units eaten");
            Assert.True(units > 0d, "nothing was eaten, so nothing was tested");
            AssertSame(walk, cols);

            // The points moved who eats: the same world measured from the centres does not eat the same.
            Eaten centres = Run(fromMouth: false, handPoints: true, columns: false, steps, reach);
            Assert.NotEqual(walk.Units, centres.Units);
        }

        [Fact]
        public void OnTheColumnsEatTheSameBitsAsTheWalkWithPointsFarFromTheCentreAtAShortReach() =>
            OnTheColumnsEatTheWalksBits(0.5f, 6);

        [Fact]
        public void OnTheColumnsEatTheSameBitsAsTheWalkWithPointsFarFromTheCentreAtAMetre() =>
            OnTheColumnsEatTheWalksBits(1f, 6);

        [Fact]
        public void OnTheColumnsEatTheSameBitsAsTheWalkAtAReachOfMostOfTheBox() =>
            OnTheColumnsEatTheWalksBits(5f, 3);

        // ---------------------------------------------------------------- the dial's refusal

        [Fact]
        public void TheDialOnWithNoReachIsRefused()
        {
            Assert.False(new RunConfig().IntakeReachFromMouth);

            Assert.Throws<ArgumentException>(() => new World(Box(0f, fromMouth: true), seed: 1));
            Assert.NotNull(new World(Box(0f, fromMouth: false), seed: 1));
            Assert.NotNull(new World(Box(Reach, fromMouth: true), seed: 1));
        }

        [Fact]
        public void AMouthPointThatIsNotFiniteIsRefused()
        {
            World world = OneCorpseAndOneMouth(Box(Reach, fromMouth: true), Corpse, Near, out Organism mouth);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                world.ObserveMouths(mouth, new[] { 1f, float.NaN, 3f }, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                world.ObserveMouths(mouth, new[] { 1f, 2f, 3f }, 2));
        }
    }
}
