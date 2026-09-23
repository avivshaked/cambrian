using System.Collections.Generic;
using Evosim.Core;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The reefs in the farm (<c>logbook/specs/reef-spec.md</c>): the six variables reach the
    /// config, the header names them, and the manifest records where the seed put the reefs.
    /// </summary>
    public class ReefFarmTests
    {
        [Fact]
        public void TheSixVariablesReachTheConfigAndTheHeader()
        {
            var block = new Dictionary<string, string>
            {
                ["EVOSIM_REEF_COUNT"] = "3",
                ["EVOSIM_REEF_CAP_RADIUS"] = "4",
                ["EVOSIM_REEF_CAP_DEPTH"] = "3",
                ["EVOSIM_REEF_CAP_THICKNESS"] = "2",
                ["EVOSIM_REEF_STEM_RADIUS"] = "1",
                ["EVOSIM_REEF_FADE"] = "15",
            };

            RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block)));

            Assert.Equal(3, config.ReefCount);
            Assert.Equal(4f, config.ReefCapRadiusMetres);
            Assert.Equal(3f, config.ReefCapDepthMetres);
            Assert.Equal(2f, config.ReefCapThicknessMetres);
            Assert.Equal(1f, config.ReefStemRadiusMetres);
            Assert.Equal(15f, config.ReefFadeMetres);
            Assert.Equal("reefs 3 cap r=4 m at 3 m t=2 m stem r=1 m fade 15 m", ReefGeometry.HeaderToken(config));
            Assert.Empty(EnvBinding.UnknownNames(block.Keys));

            RunConfig plain = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>())));
            Assert.Equal(0, plain.ReefCount);
            Assert.Equal("no reef", ReefGeometry.HeaderToken(plain));
            Assert.NotEqual(plain.Hash(), config.Hash());
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
            var reefs = new ReefGeometry(4f, 3f, 2f, 1f, 15f, new[] { 10.5d, 60d }, new[] { 20.25d, 70d });

            Manifest.RecordReefs(manifest, reefs);
            JsonNode m = Json.Parse(Manifest.Render(manifest, ending: null));

            Assert.Equal(2, m["reefs"].Count);
            Assert.Equal(10.5, m["reefs"][0]["x"].AsDouble(), 9);
            Assert.Equal(20.25, m["reefs"][0]["z"].AsDouble(), 9);
            Assert.Equal(70, m["reefs"][1]["z"].AsDouble(), 9);

            var none = new RunManifest
            {
                ArmName = "reef", Seed = 7, RequestedSeconds = 600f, RequestedWallMinutes = 60f,
                ConfigHash = "ff557bce2685293a", PhysicsStepSeconds = 0.01f, MetabolicStepSeconds = 0.5f,
                EngineVersion = "1.2.3.4", Threads = 8, GitCommit = new string('0', 40), GitDirty = false,
                CoreHash = new string('a', 64), DynamicsHash = new string('b', 64), FarmHash = new string('c', 64),
                ProgramPath = "bin", RepoRoot = "repo",
            };
            Manifest.RecordReefs(none, null);
            Assert.Equal(0, Json.Parse(Manifest.Render(none, ending: null))["reefs"].Count);
        }
    }
}
