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
                if (!Nudge(config, p)) continue;   // sub-objects are covered by their own test

                if (config.Hash() == baseline) missed.Add(p.Name);
            }

            _output.WriteLine(missed.Count == 0
                ? "every scalar tunable reaches the hash"
                : "missing from Hash(): " + string.Join(", ", missed));

            Assert.Empty(missed);
        }

        /// <summary>
        /// Every sub-config RunConfig owns, walked the same way.
        /// </summary>
        /// <remarks>
        /// <b>Named individually rather than reflected over, and that is the point.</b> This test
        /// once covered only <see cref="RandomGenomeOptions"/>, which is why
        /// <see cref="DevelopmentLimits.MaxPartVolume"/> reached neither the hash nor the JSON
        /// (logbook/0011): the guard against forgetting a tunable had itself forgotten a whole
        /// object. Discovering sub-configs by reflection would have caught that one and would fail
        /// the same way again — silently passing for whatever it had not thought to look at, since
        /// <see cref="RunConfig.Shapes"/> and <see cref="RunConfig.CellTypes"/> are registries
        /// with no settable scalars and cannot be nudged like this. A literal list can only be
        /// wrong in a way a human reading it can see.
        /// </remarks>
        public static IEnumerable<object[]> SubConfigs => new[]
        {
            new object[] { typeof(RandomGenomeOptions) },
            new object[] { typeof(DevelopmentLimits) },
            new object[] { typeof(MutationRates) },
            new object[] { typeof(FluidConfig) },
            new object[] { typeof(LightModel) },
        };

        [Theory]
        [MemberData(nameof(SubConfigs))]
        public void EveryTunableOnEverySubConfigChangesTheHash(Type subConfig)
        {
            string baseline = new RunConfig().Hash();
            var missed = new List<string>();
            int checkedCount = 0;

            PropertyInfo owner = FindOwner(subConfig);

            foreach (PropertyInfo p in subConfig.GetProperties())
            {
                if (!p.CanWrite) continue;

                // A fresh config each time, and its own sub-object rather than a new one: every
                // Default here is `=> new ...`, so this is already private to this iteration, and
                // LightModel has no parameterless constructor to call anyway.
                var config = new RunConfig();

                if (!Nudge(owner.GetValue(config), p)) continue;
                checkedCount++;

                if (config.Hash() == baseline) missed.Add(p.Name);
            }

            _output.WriteLine($"{subConfig.Name}: {checkedCount} tunables checked");
            if (missed.Count > 0) _output.WriteLine("missing from Hash(): " + string.Join(", ", missed));

            // A sub-config with nothing checkable means the walk quietly stopped covering it —
            // an all-enum or all-registry object would pass this test by never testing anything.
            Assert.True(checkedCount > 0, $"{subConfig.Name} exposed no nudgeable tunable");
            Assert.Empty(missed);
        }

        private static PropertyInfo FindOwner(Type subConfig)
        {
            foreach (PropertyInfo p in typeof(RunConfig).GetProperties())
            {
                if (p.PropertyType == subConfig && p.CanWrite) return p;
            }

            throw new InvalidOperationException(
                $"RunConfig has no writable property of type {subConfig.Name}. Either it was " +
                "removed and this list is stale, or it was never wired up at all.");
        }

        /// <summary>Changes a property to something different, or returns false if it cannot.</summary>
        private static bool Nudge(object target, PropertyInfo p)
        {
            if (p.PropertyType == typeof(float))
            {
                p.SetValue(target, (float)p.GetValue(target) + 7.5f);
            }
            else if (p.PropertyType == typeof(int))
            {
                p.SetValue(target, (int)p.GetValue(target) + 3);
            }
            else if (p.PropertyType == typeof(bool))
            {
                p.SetValue(target, !(bool)p.GetValue(target));
            }
            else if (p.PropertyType == typeof(string[]))
            {
                p.SetValue(target, new[] { CellTypeIds.Consumer });
            }
            else
            {
                return false;
            }

            return true;
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
