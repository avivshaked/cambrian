using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D190's launcher name reaches the config, and the header names the desert's rate only when it is set.
    /// </summary>
    public sealed class DesertBurialBindingTests
    {
        private static Dictionary<string, string> Tank(string desert)
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
                { "EVOSIM_MATTER_INFLUX", "0.5" },
                { "EVOSIM_MATTER_INFLUX_AT", "islands" },
                { "EVOSIM_MATTER_BURIAL", "0.001" },
            };
            if (desert != null) env["EVOSIM_MATTER_BURIAL_DESERT"] = desert;
            return env;
        }

        private static string Header(Dictionary<string, string> env, out RunConfig config)
        {
            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(env));
            config = EnvBinding.BuildConfig(settings);
            var world = new World(config, settings.Seed);
            SpaceFacts space = SpaceFacts.Of(world, hasWall: true, hasFloor: true, threads: 1, inoculumHashShort: null);
            return Report.HeaderLine(settings, config, space, "9.9.9.9");
        }

        [Fact]
        public void UnsetTheDesertIsOffAndTheHeaderSaysNothingNew()
        {
            string line = Header(Tank(null), out RunConfig config);

            Assert.Equal(-1f, config.MatterBurialDesertPerSecond);
            Assert.Contains(", burial 0.001/s · ", line);
            Assert.DoesNotContain("desert burial", line);
        }

        [Fact]
        public void TheDesertRateReachesTheConfigAndTheHeader()
        {
            string line = Header(Tank("0.02"), out RunConfig config);

            Assert.Equal(0.02f, config.MatterBurialDesertPerSecond);
            Assert.Contains(", burial 0.001/s, desert burial 0.02/s · ", line);

            RunConfig unset = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(Tank(null))));
            Assert.NotEqual(unset.Hash(), config.Hash());
        }
    }
}
