using System;
using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The intake's columns (World.IntakeByColumns) visit only the mouths and corpses beside each other, and must eat
    /// the same bits as the walk of every pair: every corpse's joules, every mouth's reserve, the units eaten, the
    /// waste and the snow. Round 61's c6101x spent 156 ms a step in that walk at 398 mouths among 62,754 corpses.
    /// </summary>
    // World.IntakeByColumns and Parallelism.Threads are statics: this collection runs their tests one at a time.
    [Collection("Parallelism.Threads")]
    public class IntakeColumnsTests
    {
        private readonly ITestOutputHelper _output;

        public IntakeColumnsTests(ITestOutputHelper output) => _output = output;

        private static RunConfig Box(float reach) => new RunConfig
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
        };

        private sealed class Eaten
        {
            public long[] Corpses;
            public long[] Mouths;
            public long Units, Waste, Snow;
            public long CorpsesEaten;
        }

        // Corpses and mouths over the whole 12 m box, a share of them within a metre of a seam, so that pairs meet
        // across the wrap as well as inside it.
        private static Float3 Anywhere(Rng rng)
        {
            float x = rng.Range(0f, 12f), z = rng.Range(0f, 12f);
            if (rng.Range(0f, 1f) < 0.3f) x = rng.Range(0f, 1f) < 0.5f ? rng.Range(0f, 0.6f) : rng.Range(11.4f, 12f);
            if (rng.Range(0f, 1f) < 0.3f) z = rng.Range(0f, 1f) < 0.5f ? rng.Range(0f, 0.6f) : rng.Range(11.4f, 12f);
            return new Float3(x, -rng.Range(1f, 4f), z);
        }

        private static Eaten Run(bool columns, float reach, int steps)
        {
            int was = Parallelism.Threads;
            bool wasColumns = World.IntakeByColumns;
            try
            {
                Parallelism.Threads = 4;
                World.IntakeByColumns = columns;
                var world = new World(Box(reach), seed: 11);
                var rng = new Rng(7);

                Genome bare = Fixtures.ArmedBox(half: 0.2f);
                world.Inoculate(bare, 600, heightY: -2f);
                List<Organism> dying = world.Living.ToList();
                foreach (Organism body in dying)
                {
                    world.Observe(body, Anywhere(rng), 0f);
                    world.KillDiverged(body);
                }

                world.Inoculate(Fixtures.ArmedBox(intake: 1f, half: 0.2f, cellTypeId: CellTypeIds.Consumer), 80, heightY: -2f);
                List<Organism> mouths = world.Living.Where(c => c.HasIntake).ToList();
                Assert.Equal(80, mouths.Count);
                foreach (Organism mouth in mouths) world.Observe(mouth, Anywhere(rng), 0f);

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

        private void Same(float reach, int steps)
        {
            Eaten walk = Run(false, reach, steps);
            Eaten cols = Run(true, reach, steps);

            double units = BitConverter.Int64BitsToDouble(walk.Units);
            _output.WriteLine($"reach {reach} m: {walk.Corpses.Length} corpses, {walk.Mouths.Length} mouths, {units:0.####} units eaten, {walk.CorpsesEaten} corpses emptied");
            Assert.True(units > 0d, "nothing was eaten, so nothing was tested");
            Assert.True(walk.Corpses.Length >= 500, "fewer corpses than planted: the kills did not leave them");

            Assert.Equal(walk.Corpses, cols.Corpses);
            Assert.Equal(walk.Mouths, cols.Mouths);
            Assert.Equal(walk.Units, cols.Units);
            Assert.Equal(walk.Waste, cols.Waste);
            Assert.Equal(walk.Snow, cols.Snow);
            Assert.Equal(walk.CorpsesEaten, cols.CorpsesEaten);
        }

        /// <summary>A short reach: many columns, pairs inside a column, across a column's edge and across the seam.</summary>
        [Fact]
        public void TheColumnsEatTheSameBitsAsTheWalkAtAShortReach() => Same(0.5f, 8);

        /// <summary>The fixture's reach of a metre, as the intake's own parallel test uses.</summary>
        [Fact]
        public void TheColumnsEatTheSameBitsAsTheWalkAtAMetre() => Same(1f, 8);

        /// <summary>A reach past a quarter of the box: three columns or fewer, where every column is a neighbour.</summary>
        [Fact]
        public void TheColumnsEatTheSameBitsAsTheWalkAtAReachOfMostOfTheBox() => Same(5f, 4);
    }
}
