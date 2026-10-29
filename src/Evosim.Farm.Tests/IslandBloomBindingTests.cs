using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D251's launcher names reach the config, and the header names the bloom only when it is on.
    /// </summary>
    public sealed class IslandBloomBindingTests
    {
        private static Dictionary<string, string> Fed(params (string, string)[] more)
        {
            var env = new Dictionary<string, string>
            {
                { "EVOSIM_SHARED_SPACE", "1" },
                { "EVOSIM_SHAPE", "tank" },
                { "EVOSIM_AREA", "400" },
                { "EVOSIM_DEPTH", "20" },
                { "EVOSIM_FIELD", "grid" },
                { "EVOSIM_FIELD_CELL", "1" },
                { "EVOSIM_FIELD_MATTER_CELL", "2" },
                { "EVOSIM_MATTER_BUDGET", "300" },
                { "EVOSIM_MATTER_ISLANDS", "8" },
                { "EVOSIM_MATTER_ISLAND_COVER", "0.2" },
                { "EVOSIM_MATTER_ISLAND_DEPTH", "6" },
                { "EVOSIM_MATTER_INFLUX", "1.2" },
                { "EVOSIM_MATTER_INFLUX_AT", "islands" },
            };
            foreach ((string k, string v) in more) env[k] = v;
            return env;
        }

        private static (string header, RunConfig config, World world) Build(Dictionary<string, string> env)
        {
            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(env));
            RunConfig config = EnvBinding.BuildConfig(settings);
            var world = new World(config, settings.Seed);
            SpaceFacts space = SpaceFacts.Of(world, hasWall: true, hasFloor: true, threads: 1, inoculumHashShort: null);
            return (Report.HeaderLine(settings, config, space, "9.9.9.9"), config, world);
        }

        [Fact]
        public void UnsetTheBloomIsOffAndTheHeaderSaysNothingNew()
        {
            (string line, RunConfig config, World world) = Build(Fed());

            Assert.Equal(IslandBloom.None, config.IslandBloom);
            Assert.Equal(2500f, config.IslandBloomPeriodMinSeconds);
            Assert.Equal(6000f, config.IslandBloomPeriodMaxSeconds);
            Assert.Equal(0.8f, config.IslandBloomAmplitudeMin);
            Assert.Equal(1f, config.IslandBloomAmplitudeMax);
            Assert.True(config.IslandBloomHoldsTotal);
            Assert.DoesNotContain("island bloom", line);
            Assert.Equal("", Report.IslandBloomLine(world));
        }

        [Fact]
        public void TheNamesReachTheConfigTheHeaderAndTheIslandLine()
        {
            (string line, RunConfig config, World world) = Build(Fed(
                ("EVOSIM_ISLAND_BLOOM", "cycles"),
                ("EVOSIM_BLOOM_PERIOD_MIN", "3000"),
                ("EVOSIM_BLOOM_PERIOD_MAX", "5000"),
                ("EVOSIM_BLOOM_AMPLITUDE_MIN", "0.5"),
                ("EVOSIM_BLOOM_AMPLITUDE_MAX", "0.9"),
                ("EVOSIM_BLOOM_HOLDS_TOTAL", "0")));

            Assert.Equal(IslandBloom.Cycles, config.IslandBloom);
            Assert.Equal(3000f, config.IslandBloomPeriodMinSeconds);
            Assert.Equal(5000f, config.IslandBloomPeriodMaxSeconds);
            Assert.Equal(0.5f, config.IslandBloomAmplitudeMin);
            Assert.Equal(0.9f, config.IslandBloomAmplitudeMax);
            Assert.False(config.IslandBloomHoldsTotal);
            Assert.Contains(" · island bloom cycles period 3000-5000 s amplitude 0.5-0.9 lets the total move · configHash", line);

            string islands = Report.IslandBloomLine(world);
            Assert.StartsWith("island bloom, " + world.IslandInflux.IslandCount + " islands", islands);
            Assert.Contains("0: P ", islands);
            Assert.Contains(" columns, at ", islands);

            // Held is the default under cycles.
            (string held, _, _) = Build(Fed(("EVOSIM_ISLAND_BLOOM", "cycles")));
            Assert.Contains("holds the total", held);

            RunConfig off = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(Fed())));
            Assert.NotEqual(off.Hash(), config.Hash());
        }

        [Fact]
        public void UnsetTheFarSmellIsOffAndTheHeaderSaysNothingNew()
        {
            (string line, RunConfig config, World world) = Build(Fed(("EVOSIM_LEAF_SCENT", "strength")));

            Assert.Equal(IslandFarSmell.None, config.IslandFarSmell);
            Assert.Equal(0.005f, config.IslandFarSmellGain);
            Assert.Equal(10f, config.IslandFarSmellBandMetres);
            Assert.Equal(30f, config.IslandFarSmellReachMetres);
            Assert.DoesNotContain("far smell", line);
            Assert.False(world.HasIslandFarSmell);
        }

        [Fact]
        public void TheFarSmellNamesReachTheConfigAndTheHeader()
        {
            (string line, RunConfig config, World world) = Build(Fed(
                ("EVOSIM_LEAF_SCENT", "strength"),
                ("EVOSIM_FAR_SMELL", "leaves"),
                ("EVOSIM_FAR_SMELL_GAIN", "0.01"),
                ("EVOSIM_FAR_SMELL_BAND", "4"),
                ("EVOSIM_FAR_SMELL_REACH", "5")));

            Assert.Equal(IslandFarSmell.Leaves, config.IslandFarSmell);
            Assert.Equal(0.01f, config.IslandFarSmellGain);
            Assert.Equal(4f, config.IslandFarSmellBandMetres);
            Assert.Equal(5f, config.IslandFarSmellReachMetres);
            Assert.Contains(" · island far smell leaves gain 0.01 band 4 m reach 5 m · configHash", line);
            Assert.True(world.HasIslandFarSmell);

            // It does not need the cycles, and beside them the header names both, the cycles first.
            Assert.DoesNotContain("island bloom", line);
            (string both, _, _) = Build(Fed(
                ("EVOSIM_LEAF_SCENT", "strength"), ("EVOSIM_ISLAND_BLOOM", "cycles"), ("EVOSIM_FAR_SMELL", "leaves")));
            Assert.Contains(" · island bloom cycles ", both);
            Assert.True(both.IndexOf(" · island bloom ", StringComparison.Ordinal) < both.IndexOf(" · island far smell ", StringComparison.Ordinal));

            RunConfig off = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(Fed(("EVOSIM_LEAF_SCENT", "strength")))));
            Assert.NotEqual(off.Hash(), config.Hash());
        }

        [Fact]
        public void AnUnknownFarSmellIsRefusedAndAFarSmellWithoutALeafSmellIsRefusedByTheWorld()
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => EnvBinding.Read(EnvBinding.Of(Fed(("EVOSIM_FAR_SMELL", "halo")))));
            Assert.Contains("none, leaves", e.Message);

            ArgumentException w = Assert.Throws<ArgumentException>(() => Build(Fed(("EVOSIM_FAR_SMELL", "leaves"))));
            Assert.Contains("IslandFarSmell is Leaves", w.Message);
        }

        [Fact]
        public void AnUnknownBloomIsRefused()
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => EnvBinding.Read(EnvBinding.Of(Fed(("EVOSIM_ISLAND_BLOOM", "pulses")))));
            Assert.Contains("none, cycles", e.Message);
        }
    }
}
