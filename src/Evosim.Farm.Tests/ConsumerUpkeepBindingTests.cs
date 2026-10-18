using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D177's launch knob for the consumer cell's upkeep, <c>EVOSIM_UPKEEP_CONSUMER</c>: it sets that
    /// one value in the config's cell table and prints it in the header, unset it leaves the config
    /// as it was, and a config written with it set differs from one without only in that value and
    /// the hash, and loads.
    /// </summary>
    public sealed class ConsumerUpkeepBindingTests
    {
        private static RunConfig Built(Dictionary<string, string> env) =>
            EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(env)));

        private static float Upkeep(RunConfig config, string id) =>
            config.CellTypes.Resolve(id).UpkeepWattsPerCubicMetre;

        [Fact]
        public void TheKnobSetsTheConsumersUpkeepAloneAndTheHeaderPrintsIt()
        {
            RunConfig plain = Built(new Dictionary<string, string>());
            RunConfig set = Built(new Dictionary<string, string> { { "EVOSIM_UPKEEP_CONSUMER", "2" } });

            Assert.Equal(6f, Upkeep(plain, CellTypeIds.Consumer));
            Assert.Equal(2f, Upkeep(set, CellTypeIds.Consumer));

            Assert.Equal(plain.CellTypes.Count, set.CellTypes.Count);
            for (int i = 0; i < plain.CellTypes.Count; i++)
            {
                CellType a = plain.CellTypes.At(i), b = set.CellTypes.At(i);
                Assert.Equal(a.Id, b.Id);
                if (a.Id == CellTypeIds.Consumer) continue;
                Assert.Equal(a.FullHashContribution(), b.FullHashContribution());
            }

            Assert.Equal(" · consumer upkeep 2 W/m3", Report.RoundFiftyNineToken(set));
            Assert.Equal("", Report.RoundFiftyNineToken(plain));

            // With round 59's other knobs, it follows them.
            RunConfig both = Built(new Dictionary<string, string>
            {
                { "EVOSIM_UPKEEP_CONSUMER", "2" }, { "EVOSIM_UNBITTEN_NO_CORPSE", "1" },
            });
            Assert.Equal(" · unbitten burnt · consumer upkeep 2 W/m3", Report.RoundFiftyNineToken(both));

            Assert.ThrowsAny<ArgumentException>(() => Built(new Dictionary<string, string> { { "EVOSIM_UPKEEP_CONSUMER", "0" } }));
        }

        /// <summary>
        /// Unset, the consumer cell is <see cref="ConsumerCell"/>'s own, and the config's hash is the
        /// hash of the table built with the parameterless cell, as the binding built it before the knob.
        /// </summary>
        [Fact]
        public void UnsetTheConfigHashIsTheOneBuiltWithTheConsumerCellsOwnUpkeep()
        {
            RunConfig plain = Built(new Dictionary<string, string>());
            string before = plain.Hash();

            CellType[] types = Enumerable.Range(0, plain.CellTypes.Count).Select(i => plain.CellTypes.At(i)).ToArray();
            int at = Array.FindIndex(types, t => t.Id == CellTypeIds.Consumer);
            var own = new ConsumerCell { TissueEnergyPerCubicMetre = types[at].TissueEnergyPerCubicMetre };
            Assert.Equal(own.FullHashContribution(), types[at].FullHashContribution());

            types[at] = own;
            RunConfig rebuilt = Built(new Dictionary<string, string>());
            rebuilt.CellTypes = new CellTypeRegistry(types);
            Assert.Equal(before, rebuilt.Hash());

            // And the knob at the cell's own value is no change at all.
            Assert.Equal(before, Built(new Dictionary<string, string> { { "EVOSIM_UPKEEP_CONSUMER", "6" } }).Hash());
        }

        /// <summary>
        /// The saved config.json at 2 differs from the unset one in the consumer's upkeep and the
        /// config's own hash, and in nothing else; it loads with no hash mismatch and the consumer at
        /// 2, which is the reading the ledger (Evosim.Ledger, through RunConfigJson.Read) makes.
        /// </summary>
        [Fact]
        public void TheSavedConfigDiffersInTheConsumersUpkeepAloneAndLoads()
        {
            RunConfig plain = Built(new Dictionary<string, string>());
            RunConfig set = Built(new Dictionary<string, string> { { "EVOSIM_UPKEEP_CONSUMER", "2" } });

            string plainText = RunConfigJson.Write(plain);
            string setText = RunConfigJson.Write(set);
            Assert.NotEqual(plain.Hash(), set.Hash());

            string[] a = plainText.Split('\n'), b = setText.Split('\n');
            Assert.Equal(a.Length, b.Length);

            var differing = new List<int>();
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) differing.Add(i);

            Assert.Equal(2, differing.Count);
            int hashLine = differing.Single(i => a[i].Contains(plain.Hash()));
            Assert.Contains(set.Hash(), b[hashLine]);

            int upkeepLine = differing.Single(i => i != hashLine);
            Assert.Contains("\"upkeepWattsPerCubicMetre\"", a[upkeepLine]);
            Assert.Contains("6", a[upkeepLine]);
            Assert.Contains("2", b[upkeepLine]);

            // The line belongs to the consumer's entry: the nearest id above it.
            string id = Enumerable.Range(0, upkeepLine).Reverse().Select(i => a[i]).First(l => l.Contains("\"id\""));
            Assert.Contains("\"" + CellTypeIds.Consumer + "\"", id);

            RunConfig loaded = RunConfigJson.Read(setText, out string mismatch);
            Assert.Null(mismatch);
            Assert.Equal(2f, Upkeep(loaded, CellTypeIds.Consumer));
            Assert.Equal(set.Hash(), loaded.Hash());

            // Kept for the ledger's own reading (scripts/ledger.ps1 -Config).
            string dir = SimulationTests.Scratch("consumer-upkeep");
            File.WriteAllText(Path.Combine(dir, "config-consumer-6.json"), plainText);
            File.WriteAllText(Path.Combine(dir, "config-consumer-2.json"), setText);
        }
    }
}
