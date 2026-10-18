using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D185's launcher name reaches the config, and the header names the margin only when it is set.
    /// </summary>
    public sealed class GlassMarginBindingTests
    {
        private static Dictionary<string, string> Tank(string margin)
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
            };
            if (margin != null) env["EVOSIM_MATTER_ISLAND_MARGIN"] = margin;
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
        public void UnsetTheMarginIsZeroAndTheHeaderSaysNothingNew()
        {
            string line = Header(Tank(null), out RunConfig config);

            Assert.Equal(0f, config.MatterIslandGlassMarginMetres);
            Assert.Contains(" · matter islands 8 m cover 0.2 to 6 m · ", line);
            Assert.DoesNotContain("off the glass", line);
        }

        [Fact]
        public void TheMarginReachesTheConfigAndTheHeader()
        {
            string line = Header(Tank("3"), out RunConfig config);

            Assert.Equal(3f, config.MatterIslandGlassMarginMetres);
            Assert.Contains(" · matter islands 8 m cover 0.2 to 6 m off the glass 3 m · ", line);

            RunConfig unset = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(Tank(null))));
            Assert.NotEqual(unset.Hash(), config.Hash());
        }
    }
}
