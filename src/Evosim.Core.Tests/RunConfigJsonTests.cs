using System;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>Run settings serialization — DESIGN.md §5A.10, §7, §9.</summary>
    public class RunConfigJsonTests
    {
        private readonly ITestOutputHelper _output;

        public RunConfigJsonTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void DefaultSettingsRoundTrip()
        {
            var original = new RunConfig();
            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(original), out string mismatch);

            Assert.Null(mismatch);
            Assert.Equal(original.Hash(), back.Hash());
        }

        [Fact]
        public void EveryTunableSurvivesTheRoundTrip()
        {
            // The same reflection guard as the hash test, aimed at the other half of the problem.
            // A field in the hash but missing from the file loads as its default, so the loaded
            // settings hash differently from the ones that were saved — visible here, and
            // otherwise visible only as a run that quietly used different numbers.
            foreach (PropertyInfo p in typeof(RunConfig).GetProperties())
            {
                if (!p.CanWrite) continue;

                var config = new RunConfig();

                // Floats are nudged the way RunConfigTests.NudgeFloat nudges them, and for the
                // reason recorded there: a tunable may validate its own range, +7.5 is outside
                // plenty of legitimate ones, and a knob that refuses a nonsense value is doing
                // its job rather than failing this. RunConfig.ExudationFraction is a fraction and
                // is the first such knob directly on RunConfig; a fixed nudge would have made
                // this guard demand that every knob be unbounded, which is the opposite of
                // "loading refuses rather than defaults". Nothing is skipped: a float that cannot
                // be moved at all is reported below, since a tunable no run can vary is not one.
                if (p.PropertyType == typeof(float))
                {
                    Assert.True(NudgeFloat(config, p), $"{p.Name} accepted no value but its own");
                }
                else if (p.PropertyType == typeof(int)) p.SetValue(config, (int)p.GetValue(config) + 3);
                else continue;

                RunConfig back = RunConfigJson.Read(RunConfigJson.Write(config));
                Assert.True(config.Hash() == back.Hash(), $"{p.Name} did not survive the round trip");
            }
        }

        /// <summary>Moves a float tunable to a different legal value, or reports that it cannot.</summary>
        /// <remarks>
        /// A copy of <see cref="RunConfigTests"/>'s helper of the same name, deliberately: the two
        /// guards are meant to be readable one at a time, and a shared helper would put the rule
        /// that decides what "nudged" means in a third file neither of them names.
        /// </remarks>
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
                    // the original would leave this round-tripping a value that never moved.
                    if ((float)p.GetValue(target) != original) return true;
                }
            }

            return false;
        }

        [Fact]
        public void EveryGenomeOptionSurvivesTheRoundTrip()
        {
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

                RunConfig back = RunConfigJson.Read(RunConfigJson.Write(config));
                Assert.True(config.Hash() == back.Hash(), $"{p.Name} did not survive the round trip");
            }
        }

        [Fact]
        public void TunedCellTypesSurviveTheRoundTrip()
        {
            var config = new RunConfig
            {
                CellTypes = new CellTypeRegistry(
                    new StructuralCell(0.7f),
                    new LinkCell(0.05f, 3.1f),
                    new PhotosyntheticCell(0.19f, 2.2f),
                    new AbsorptiveCell(0.9f, 4.4f),
                    new ConsumerCell(31f, 5.5f, 0.75f, 0.45f, 0.15f)),
            };

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(config));

            Assert.Equal(config.Hash(), back.Hash());

            var consumer = (ConsumerCell)back.CellTypes.Resolve(CellTypeIds.Consumer);
            Fixtures.AssertClose(0.75f, consumer.CarrionYield, 0f);
            Fixtures.AssertClose(5.5f, consumer.UpkeepWattsPerCubicMetre, 0f);
        }

        [Fact]
        public void CellTypeOrderIsPreserved()
        {
            // Cell-type mutation picks by an RNG draw, so the order decides which type a given
            // draw yields. A registry reloaded in a different order is a different world.
            var config = new RunConfig
            {
                CellTypes = new CellTypeRegistry(
                    new ConsumerCell(), new StructuralCell(), new LinkCell()),
            };

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(config));

            Assert.Equal(
                string.Join(",", config.CellTypes.Ids()),
                string.Join(",", back.CellTypes.Ids()));
        }

        [Fact]
        public void AHandEditedFileIsFlaggedRatherThanSilentlyAccepted()
        {
            // Editing this file by hand is the point of it existing, so this is not an error.
            // But the stored hash is then stale, and anything already filed under it was
            // produced by different numbers — which is worth being told once.
            string text = RunConfigJson.Write(new RunConfig())
                .Replace("\"workCostMultiplier\": 1", "\"workCostMultiplier\": 2.5");

            RunConfigJson.Read(text, out string mismatch);

            _output.WriteLine(mismatch);
            Assert.NotNull(mismatch);
        }

        [Fact]
        public void AnUnregisteredCellTypeIsRefusedOnLoad()
        {
            string text = RunConfigJson.Write(new RunConfig())
                .Replace("\"id\": \"structural\"", "\"id\": \"crystalline\"");

            FormatException e = Assert.Throws<FormatException>(() => RunConfigJson.Read(text));

            _output.WriteLine(e.Message);
            Assert.Contains("crystalline", e.Message);
        }

        [Fact]
        public void ACustomCellTypeCanBeTaughtToTheLoader()
        {
            // The extensibility claim, tested rather than asserted in a comment: a new type needs
            // the type itself plus one Register call, and nothing in the serializer changes.
            CellTypeJson.Register("test-ballast", (n, upkeep) => new BallastCell(upkeep));

            var config = new RunConfig
            {
                CellTypes = new CellTypeRegistry(new StructuralCell(), new BallastCell(2f)),
            };

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(config));

            Assert.Equal(config.Hash(), back.Hash());
            Fixtures.AssertClose(2f, back.CellTypes.Resolve("test-ballast").UpkeepWattsPerCubicMetre, 0f);
        }

        private sealed class BallastCell : CellType
        {
            public BallastCell(float upkeep = 1.5f) : base(upkeep) { }
            public override string Id => "test-ballast";
            public override CellIntake Acquire(in CellContext context) => CellIntake.None;
        }

        [Fact]
        public void TheFileIsReadableByAPerson()
        {
            string text = RunConfigJson.Write(new RunConfig());

            _output.WriteLine(text.Substring(0, Math.Min(1200, text.Length)) + "\n...");
            Assert.Contains("\n", text);
        }
    }
}
