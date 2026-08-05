using System;
using System.Collections.Generic;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The config hash of DESIGN.md §7, over the tunables of §5A.10.
    /// </summary>
    public class RunConfigTests
    {
        private readonly ITestOutputHelper _output;

        public RunConfigTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void TwoDefaultConfigsAgree()
        {
            Assert.Equal(new RunConfig().Hash(), new RunConfig().Hash());
        }

        [Fact]
        public void EveryTunableOnRunConfigChangesTheHash()
        {
            // The failure this guards against is specific and has happened twice on this project
            // in a different form: a parameter that never reaches the thing it configures
            // (logbook/0007, logbook/0008). A tunable missing from the hash is the same fault at
            // the record-keeping layer — two materially different runs become indistinguishable
            // after the fact, and the identical results look like evidence rather than a bug.
            //
            // Driven by reflection so that adding a property to RunConfig without adding it to
            // Hash() fails here, rather than silently years later.
            string baseline = new RunConfig().Hash();
            var missed = new List<string>();

            foreach (PropertyInfo p in typeof(RunConfig).GetProperties())
            {
                if (!p.CanWrite) continue;

                var config = new RunConfig();
                if (p.PropertyType == typeof(float)) p.SetValue(config, (float)p.GetValue(config) + 7.5f);
                else if (p.PropertyType == typeof(int)) p.SetValue(config, (int)p.GetValue(config) + 3);
                else continue;   // sub-objects are covered by their own tests below

                if (config.Hash() == baseline) missed.Add(p.Name);
            }

            _output.WriteLine(missed.Count == 0
                ? "every scalar tunable reaches the hash"
                : "missing from Hash(): " + string.Join(", ", missed));

            Assert.Empty(missed);
        }

        [Fact]
        public void EveryTunableOnRandomGenomeOptionsChangesTheHash()
        {
            string baseline = new RunConfig().Hash();
            var missed = new List<string>();

            foreach (PropertyInfo p in typeof(RandomGenomeOptions).GetProperties())
            {
                if (!p.CanWrite) continue;

                var config = new RunConfig { Genome = new RandomGenomeOptions() };
                if (p.PropertyType == typeof(float))
                    p.SetValue(config.Genome, (float)p.GetValue(config.Genome) + 7.5f);
                else if (p.PropertyType == typeof(int))
                    p.SetValue(config.Genome, (int)p.GetValue(config.Genome) + 3);
                else if (p.PropertyType == typeof(string[]))
                    p.SetValue(config.Genome, new[] { CellTypeIds.Consumer });
                else continue;

                if (config.Hash() == baseline) missed.Add(p.Name);
            }

            _output.WriteLine(missed.Count == 0
                ? "every genome-generation tunable reaches the hash"
                : "missing from Hash(): " + string.Join(", ", missed));

            Assert.Empty(missed);
        }

        [Fact]
        public void CellTypeTuningChangesTheHash()
        {
            var tuned = new RunConfig
            {
                CellTypes = new CellTypeRegistry(
                    new StructuralCell(), new LinkCell(), new PhotosyntheticCell(0.2f),
                    new AbsorptiveCell(), new ConsumerCell()),
            };

            Assert.NotEqual(new RunConfig().Hash(), tuned.Hash());
        }

        [Fact]
        public void CellUpkeepChangesTheHash()
        {
            // §5A.10's first entry. The ratios between the five upkeep rates decide which trophic
            // strategies can pay for themselves at all, so this is among the most consequential
            // things a run can vary — and the one most likely to be varied by accident.
            var tuned = new RunConfig
            {
                CellTypes = new CellTypeRegistry(
                    new StructuralCell(0.5f), new LinkCell(), new PhotosyntheticCell(),
                    new AbsorptiveCell(), new ConsumerCell()),
            };

            Assert.NotEqual(new RunConfig().Hash(), tuned.Hash());
        }

        [Fact]
        public void ConsumerYieldsChangeTheHash()
        {
            var tuned = new RunConfig
            {
                CellTypes = new CellTypeRegistry(
                    new StructuralCell(), new LinkCell(), new PhotosyntheticCell(),
                    new AbsorptiveCell(), new ConsumerCell(carrionYield: 0.6f)),
            };

            Assert.NotEqual(new RunConfig().Hash(), tuned.Hash());
        }

        [Fact]
        public void FluidTuningChangesTheHash()
        {
            var tuned = new RunConfig { Fluid = new FluidConfig { DragCoefficient = 2.5f } };
            Assert.NotEqual(new RunConfig().Hash(), tuned.Hash());
        }

        [Fact]
        public void AYieldAboveOneIsRejected()
        {
            // A feeder that keeps more than it takes is a free-energy source (§11.2), and a food
            // chain that gains energy at every level has no reason to end.
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConsumerCell(carrionYield: 1.5f));
        }

        [Fact]
        public void TheHashIsStableAcrossCalls()
        {
            // It is written into run records, so a hash that varied between calls would make
            // every stored record unmatchable against a re-run.
            var config = new RunConfig();
            string first = config.Hash();

            for (int i = 0; i < 10; i++) Assert.Equal(first, config.Hash());
            _output.WriteLine($"default config hash: {first}");
        }
    }
}
