using System.Collections.Generic;
using Evosim.Core;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The reefs in the farm (<c>logbook/specs/reef-spec.md</c>, the group as redesigned on
    /// 2026-09-23 night): the ten variables reach the config, the header names them with the
    /// draw's count and cover, the retired three are not bound, and the manifest records every
    /// reef the seed drew.
    /// </summary>
    public class ReefFarmTests
    {
        [Fact]
        public void TheTenVariablesReachTheConfigAndTheHeader()
        {
            var block = new Dictionary<string, string>
            {
                ["EVOSIM_REEF_COVER"] = "0.25",
                ["EVOSIM_REEF_MAX_COUNT"] = "40",
                ["EVOSIM_REEF_CAP_RADIUS_MIN"] = "5",
                ["EVOSIM_REEF_CAP_RADIUS_MAX"] = "12",
                ["EVOSIM_REEF_ROUGHNESS"] = "0.2",
                ["EVOSIM_REEF_CAP_DEPTH"] = "4",
                ["EVOSIM_REEF_CAP_DEPTH_JITTER"] = "1.5",
                ["EVOSIM_REEF_CAP_THICKNESS"] = "1.5",
                ["EVOSIM_REEF_STEM_FRACTION"] = "0.2",
                ["EVOSIM_REEF_FADE"] = "10",
            };

            RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block)));

            Assert.Equal(0.25f, config.ReefCover);
            Assert.Equal(40, config.ReefMaxCount);
            Assert.Equal(5f, config.ReefCapRadiusMinMetres);
            Assert.Equal(12f, config.ReefCapRadiusMaxMetres);
            Assert.Equal(0.2f, config.ReefOutlineRoughness);
            Assert.Equal(4f, config.ReefCapDepthMetres);
            Assert.Equal(1.5f, config.ReefCapDepthJitterMetres);
            Assert.Equal(1.5f, config.ReefCapThicknessMetres);
            Assert.Equal(0.2f, config.ReefStemRadiusFraction);
            Assert.Equal(10f, config.ReefFadeMetres);
            Assert.Equal(
                "reefs ? cover 0.25 (? got) cap r=5-12 m rough 0.2 at 4 m ±1.5 t=1.5 m stem 0.2 fade 10 m",
                ReefGeometry.HeaderToken(config, null));
            Assert.Empty(EnvBinding.UnknownNames(block.Keys));

            RunConfig plain = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>())));
            Assert.Equal(0f, plain.ReefCover);
            Assert.Equal(64, plain.ReefMaxCount);
            Assert.Equal(6f, plain.ReefCapRadiusMinMetres);
            Assert.Equal(16f, plain.ReefCapRadiusMaxMetres);
            Assert.Equal(8f, plain.ReefFadeMetres);
            Assert.Equal("no reef", ReefGeometry.HeaderToken(plain, null));
            Assert.NotEqual(plain.Hash(), config.Hash());

            // The first build's three are retired with the count: named, they are unknown.
            Assert.Equal(
                new[] { "EVOSIM_REEF_CAP_RADIUS", "EVOSIM_REEF_COUNT", "EVOSIM_REEF_STEM_RADIUS" },
                EnvBinding.UnknownNames(new[] { "EVOSIM_REEF_COUNT", "EVOSIM_REEF_CAP_RADIUS", "EVOSIM_REEF_STEM_RADIUS" }));
        }

        [Fact]
        public void TheManifestRecordsTheReefsAxes()
        {
            var manifest = new RunManifest
            {
                ArmName = "reef", Seed = 7, RequestedSeconds = 600f, RequestedWallMinutes = 60f,
                ConfigHash = "ff557bce2685293a", PhysicsStepSeconds = 0.01f, MetabolicStepSeconds = 0.5f,
                EngineVersion = "1.2.3.4", Threads = 8, GitCommit = new string('0', 40), GitDirty = false,
                CoreHash = new string('a', 64), DynamicsHash = new string('b', 64), FarmHash = new string('c', 64),
                ProgramPath = "bin", RepoRoot = "repo",
            };
            var reefs = new ReefGeometry(2f, 8f, new[]
            {
                new ReefGeometry.Reef
                {
                    X = 10.5d, Z = 20.25d, CapRadius = 6.5d, CapDepth = 3.25d, StemRadius = 1.625d,
                    A2 = 0.1d, A3 = 0.05d, A4 = 0.125d, P2 = 1.5d, P3 = 2.5d, P4 = 3.5d, NoiseSeed = 4000000000u,
                },
                new ReefGeometry.Reef { X = 60d, Z = 70d, CapRadius = 8d, CapDepth = 2.5d, StemRadius = 2d },
            }, coverTarget: 0.25d, coverGot: 0.2475d);

            Manifest.RecordReefs(manifest, reefs);
            JsonNode m = Json.Parse(Manifest.Render(manifest, ending: null));

            Assert.Equal(2, m["reefs"].Count);
            Assert.Equal(10.5, m["reefs"][0]["x"].AsDouble(), 9);
            Assert.Equal(20.25, m["reefs"][0]["z"].AsDouble(), 9);
            Assert.Equal(6.5, m["reefs"][0]["r"].AsDouble(), 9);
            Assert.Equal(3.25, m["reefs"][0]["depth"].AsDouble(), 9);
            Assert.Equal(1.625, m["reefs"][0]["stem"].AsDouble(), 9);
            Assert.Equal(0.125, m["reefs"][0]["a4"].AsDouble(), 9);
            Assert.Equal(2.5, m["reefs"][0]["p3"].AsDouble(), 9);
            Assert.Equal(4000000000d, m["reefs"][0]["noiseSeed"].AsDouble(), 0);
            Assert.Equal(70, m["reefs"][1]["z"].AsDouble(), 9);
            Assert.Equal(0d, m["reefs"][1]["a2"].AsDouble(), 9);
            Assert.Equal(0.25, m["reefCover"].AsDouble(), 9);
            Assert.Equal(0.2475, m["reefCoverGot"].AsDouble(), 9);

            var none = new RunManifest
            {
                ArmName = "reef", Seed = 7, RequestedSeconds = 600f, RequestedWallMinutes = 60f,
                ConfigHash = "ff557bce2685293a", PhysicsStepSeconds = 0.01f, MetabolicStepSeconds = 0.5f,
                EngineVersion = "1.2.3.4", Threads = 8, GitCommit = new string('0', 40), GitDirty = false,
                CoreHash = new string('a', 64), DynamicsHash = new string('b', 64), FarmHash = new string('c', 64),
                ProgramPath = "bin", RepoRoot = "repo",
            };
            Manifest.RecordReefs(none, null);
            JsonNode n = Json.Parse(Manifest.Render(none, ending: null));
            Assert.Equal(0, n["reefs"].Count);
            Assert.Equal(0d, n["reefCoverGot"].AsDouble());
        }
    }
}
