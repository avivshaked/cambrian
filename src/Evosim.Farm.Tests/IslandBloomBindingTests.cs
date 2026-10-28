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
        public void AnUnknownBloomIsRefused()
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => EnvBinding.Read(EnvBinding.Of(Fed(("EVOSIM_ISLAND_BLOOM", "pulses")))));
            Assert.Contains("none, cycles", e.Message);
        }
    }
}
