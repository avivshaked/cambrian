using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D192's launcher name reaches the config, the header names the overrides only when set, and an
    /// unknown name refuses before a world is built.
    /// </summary>
    public sealed class SeedOverridesBindingTests
    {
        private static Dictionary<string, string> Tank(string overrides)
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
            if (overrides != null) env["EVOSIM_SEED_OVERRIDES"] = overrides;
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
        public void UnsetThereAreNoOverridesAndTheHeaderSaysNothingNew()
        {
            string line = Header(Tank(null), out RunConfig config);

            Assert.Empty(config.SeedOverrides);
            Assert.DoesNotContain(" · seeds ", line);
        }

        [Fact]
        public void TheLandscapeSeedReachesTheConfigTheWorldAndTheHeader()
        {
            string line = Header(Tank(" landscape=42 "), out RunConfig config);

            Assert.Equal(new[] { "landscape=42" }, config.SeedOverrides);
            Assert.Contains(" · seeds landscape=42 · ", line);
            Assert.Equal(42UL, new World(config, 6101).LandscapeSeed);

            RunConfig unset = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(Tank(null))));
            Assert.NotEqual(unset.Hash(), config.Hash());
        }

        [Fact]
        public void AnUnknownNameRefuses()
        {
            Assert.ThrowsAny<ArgumentException>(() => EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(Tank("dice=3")))));
        }
    }
}
