using System;
using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The intake's first pass sums each corpse's demand on any thread (card-next-r57.md Part 8), and
    /// a crowd of mouths among a field of corpses must eat the same bits at one thread and at four:
    /// every corpse's joules, every mouth's reserve, the units eaten, the waste and the snow.
    /// </summary>
    // Parallelism.Threads is a static: every test class that sets it shares this collection, so xUnit runs them one at a time.
    [Collection("Parallelism.Threads")]
    public class IntakeParallelTests
    {
        private readonly ITestOutputHelper _output;

        public IntakeParallelTests(ITestOutputHelper output) => _output = output;

        private static RunConfig Box() => new RunConfig
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
            IntakeReachMetres = 1f,
            IntakeWasteFraction = 0.25f,
        };

        private sealed class Eaten
        {
            public long[] Corpses;
            public long[] Mouths;
            public long Units, Waste, Snow;
            public long CorpsesEaten;
        }

        private static Eaten Run(int threads)
        {
            int was = Parallelism.Threads;
            try
            {
                Parallelism.Threads = threads;
                var world = new World(Box(), seed: 11);
                var rng = new Rng(5);

                // Three hundred corpses of a bare box, crowded into a 10 m square 3 m deep.
                Genome bare = Fixtures.ArmedBox(half: 0.2f);
                world.Inoculate(bare, 300, heightY: -2f);
                List<Organism> dying = world.Living.ToList();
                foreach (Organism body in dying)
                {
                    world.Observe(body, new Float3(rng.Range(1f, 11f), -rng.Range(1f, 4f), rng.Range(1f, 11f)), 0f);
                    world.KillDiverged(body);
                }

                // Forty mouths among them, so most corpses are wanted by more than one.
                world.Inoculate(Fixtures.ArmedBox(intake: 1f, half: 0.2f, cellTypeId: CellTypeIds.Consumer), 40, heightY: -2f);
                List<Organism> mouths = world.Living.Where(c => c.HasIntake).ToList();
                Assert.Equal(40, mouths.Count);
                foreach (Organism mouth in mouths)
                {
                    world.Observe(mouth, new Float3(rng.Range(1f, 11f), -rng.Range(1f, 4f), rng.Range(1f, 11f)), 0f);
                }

                List<Corpse> corpses = world.Corpses.ToList();
                for (int step = 0; step < 6; step++) world.ApplyMouth(0.5f);

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
            }
        }

        [Fact]
        public void ACrowdOfMouthsEatsTheSameBitsAtOneThreadAndAtFour()
        {
            Eaten one = Run(1);
            Eaten four = Run(4);

            double units = BitConverter.Int64BitsToDouble(one.Units);
            _output.WriteLine($"{one.Corpses.Length} corpses, {one.Mouths.Length} mouths: {units:0.####} units eaten, {one.CorpsesEaten} corpses emptied");
            Assert.True(units > 0d, "nothing was eaten, so nothing was tested");
            Assert.True(one.Corpses.Length >= 250, "fewer corpses than planted: the kills did not leave them");

            Assert.Equal(one.Corpses, four.Corpses);
            Assert.Equal(one.Mouths, four.Mouths);
            Assert.Equal(one.Units, four.Units);
            Assert.Equal(one.Waste, four.Waste);
            Assert.Equal(one.Snow, four.Snow);
            Assert.Equal(one.CorpsesEaten, four.CorpsesEaten);
        }
    }
}
