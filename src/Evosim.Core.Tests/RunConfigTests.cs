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
            new object[] { typeof(CurrentField) },
        };

        [Fact]
        public void TheSubConfigListCoversEveryTunableGroup()
        {
            // The hole the list above left open, closed. CurrentField was added to RunConfig as a
            // [TunableGroup] and the theory beside this one passed — by never looking at it, which
            // is the exact fault it was written for after DevelopmentLimits escaped the same way
            // (logbook/0011). A literal list can only be wrong in a way a human reading it can see;
            // nothing was making a human read it.
            //
            // Reflection is used here to check the list rather than to replace it, so the argument
            // for keeping it literal survives: the list still decides what gets nudged and how,
            // and this only refuses to let it fall behind RunConfig.
            var listed = new HashSet<Type>();
            foreach (object[] row in SubConfigs) listed.Add((Type)row[0]);

            var missing = new List<string>();

            foreach (PropertyInfo p in typeof(RunConfig).GetProperties())
            {
                if (p.GetCustomAttribute<TunableGroupAttribute>() == null) continue;
                if (listed.Contains(p.PropertyType)) continue;

                // A group with no nudgeable scalar cannot be tested this way and is covered by
                // EverySettableValueIsDeclaredTunable instead — registries are the standing
                // example. Anything with a settable float, int or bool has no such excuse.
                bool nudgeable = false;
                foreach (PropertyInfo q in p.PropertyType.GetProperties())
                {
                    if (!q.CanWrite) continue;
                    if (q.PropertyType == typeof(float) ||
                        q.PropertyType == typeof(int) ||
                        q.PropertyType == typeof(bool))
                    {
                        nudgeable = true;
                        break;
                    }
                }

                if (nudgeable) missing.Add($"{p.Name} ({p.PropertyType.Name})");
            }

            Assert.Empty(missing);
        }

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
                // Shrinking, because a tunable may validate its own range and +7.5 is outside
                // plenty of legitimate ones — LightModel.DayNightAmplitude is a fraction. A fixed
                // nudge would make this guard demand that every knob be unbounded, which is the
                // opposite of what the project wants: "loading refuses rather than defaults", and
                // a knob that refuses a nonsense value is doing its job.
                //
                // It must still fail loudly if NO nudge works — the caller does not increment its
                // checked count, and the "exposed no nudgeable tunable" assert catches a
                // sub-config that has quietly become untestable.
                return NudgeFloat(target, p);
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

        /// <summary>Moves a float tunable to a different legal value, or reports that it cannot.</summary>
        private static bool NudgeFloat(object target, PropertyInfo p)
        {
            var original = (float)p.GetValue(target);

            for (float delta = 7.5f; delta > 1e-4f; delta *= 0.5f)
            {
                foreach (float candidate in new[] { original + delta, original - delta })
                {
                    try
                    {
                        p.SetValue(target, candidate);
                    }
                    catch (Exception e) when (
                        e is ArgumentOutOfRangeException ||
                        e.InnerException is ArgumentOutOfRangeException)
                    {
                        continue;
                    }

                    // A setter is free to clamp rather than throw, and one that clamped back to
                    // the original would leave this reporting a hash that never had a chance to
                    // change. Confirm the value actually moved.
                    if ((float)p.GetValue(target) != original) return true;
                }
            }

            // The additive search above assumes a knob's legal values form an interval. One does
            // not: RunConfig.PhysicsStepSeconds must divide the 0.5 s metabolic step, so its legal
            // set is 0.5/n and no offset of 0.01 by a halving delta lands on a member of it. That
            // knob is still perfectly variable — 0.02 is the screening step every round 16 arm
            // ran at — so falling back to a multiplicative move keeps the guard honest rather
            // than making it demand that every legal set be continuous.
            foreach (float candidate in new[] { original * 2f, original * 0.5f })
            {
                try
                {
                    p.SetValue(target, candidate);
                }
                catch (Exception e) when (
                    e is ArgumentOutOfRangeException ||
                    e.InnerException is ArgumentOutOfRangeException)
                {
                    continue;
                }

                if ((float)p.GetValue(target) != original) return true;
            }

            return false;
        }

        [Fact]
        public void EverySettableValueIsDeclaredTunable()
        {
            // The guard that replaces four hundred hand-maintained sites. A knob is declared once
            // with [Tunable]; the hash, the file and the reader are all derived from that. So the
            // only way to add a knob and have it escape is to add a settable property and not mark
            // it — which is what this fails on, immediately, rather than years later when two runs
            // come back identical and nobody can say why (logbook/0011, logbook/0013).
            //
            // Note what it demands: not "declare it" but "declare it, or say what it is instead".
            // [TunableGroup] and [TunableRegistry] are the two ways to say a property is not a knob,
            // and both are statements someone wrote down. Silence is the failure.
            var missing = new List<string>();

            Walk(typeof(RunConfig), "", missing);

            _output.WriteLine(missing.Count == 0
                ? $"{ConfigSchema.Of(new RunConfig()).Count} tunables, all declared"
                : "settable but not [Tunable]: " + string.Join(", ", missing));

            Assert.Empty(missing);
        }

        private static void Walk(Type type, string prefix, List<string> missing)
        {
            foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetCustomAttribute<TunableAttribute>() != null) continue;

                if (p.GetCustomAttribute<TunableGroupAttribute>() != null)
                {
                    Walk(p.PropertyType, prefix + p.Name + ".", missing);
                    continue;
                }

                // Registries carry their own contribution and their own serializer, and are marked
                // as such so that "not walkable" is a stated fact rather than an oversight.
                if (p.GetCustomAttribute<TunableRegistryAttribute>() != null) continue;

                if (!p.CanWrite || !p.CanRead) continue;

                // Everything settable, not a list of types we thought of. The first version of this
                // test checked float/int/bool/string[] and passed while RandomGenomeOptions.
                // JointTypes — a JointType[], settable, and materially part of the experiment — sat
                // outside the hash and outside the file. A test that only looks for the failures it
                // already imagined is the same fault as the code it is guarding (logbook/0013).
                missing.Add($"{prefix}{p.Name} ({p.PropertyType.Name})");
            }
        }

        [Fact]
        public void TheSchemaIsSortedSoTheHashCannotMoveWithTheRuntime()
        {
            // Type.GetProperties() is documented not to guarantee an order. A hash taken in
            // discovery order would be stable on this runtime and silently different on the next,
            // which turns §7's promise into one that holds until someone upgrades .NET.
            IReadOnlyList<TunableEntry> schema = ConfigSchema.Of(new RunConfig());

            for (int i = 1; i < schema.Count; i++)
            {
                Assert.True(
                    string.CompareOrdinal(schema[i - 1].Path, schema[i].Path) < 0,
                    $"{schema[i - 1].Path} then {schema[i].Path} — not sorted, or a duplicate path");
            }

            Assert.True(schema.Count > 60, $"only {schema.Count} tunables found — the walk broke");
        }

        [Fact]
        public void EveryTunableCarriesAGroupAndAUsableType()
        {
            foreach (TunableEntry entry in ConfigSchema.Of(new RunConfig()))
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Group), $"{entry.Path} has no group");
                Assert.False(string.IsNullOrWhiteSpace(entry.Key), $"{entry.Path} has no key");

                Assert.True(
                    entry.ValueType == typeof(float) || entry.ValueType == typeof(int) ||
                    entry.ValueType == typeof(bool) || entry.ValueType == typeof(string[]) ||
                    ConfigSchema.EnumElementOf(entry.ValueType) != null,
                    $"{entry.Path} is a {entry.ValueType.Name}, which the file format cannot carry");
            }
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
