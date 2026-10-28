using System;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The test that swims an episode (S3 of logbook/specs/nursery-room-tank-plan.md; S4 is retired on main, below). <b>It is not part of a default run</b>: it is a
    /// <see cref="SwimFactAttribute"/>, skipped unless <c>EVOSIM_NURSERY_SWIM=1</c>, and traited <c>Category=Swim</c>.
    /// Run it on purpose, on a machine that is not timing anything. <b>The command</b> (PowerShell, from the repository root; Unity's SDK,
    /// since no .NET SDK is installed system-wide):
    /// <code>
    /// $env:EVOSIM_NURSERY_SWIM = "1"
    /// $dotnet = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe"
    /// &amp; $dotnet build src/Evosim.Nursery -c Release -m:1
    /// &amp; $dotnet test src/Evosim.Nursery.Tests -c Release -m:1 --filter "Category=Swim"
    /// </code>
    /// <para>
    /// <b>It does not swim inside the xunit host.</b> The pilot was swum by the Release nursery program with tiered compilation off, and a swim in the
    /// test host (tiering on, or a Debug build) proves nothing either way. It starts that program, <c>artifacts/Evosim.Nursery/bin/Release/net8.0/
    /// Evosim.Nursery.dll</c> (or <c>EVOSIM_NURSERY_DLL</c>), as a child process of Unity's <c>dotnet.exe</c>, in the environment the room gives a re-step (no
    /// <c>DOTNET_*</c>, no <c>EVOSIM_TANK_TRACE</c>); see <see cref="RestepProcess"/>. It skips, saying why, if the program is absent or older than
    /// its sources (so the Release build above comes first), and this project sets <c>TieredCompilation</c> off too, as Dynamics.Tests and Farm.Tests do.
    /// </para>
    /// <para>
    /// It reads a file the repository does not track and skips, naming the file, where it is absent: the pilot's pool copied beside the S1 recipe
    /// (<c>scratch/nursery-trials/s1-pilot-pool.tsv</c>, in this tree: a request's paths stay under the repository root).
    /// </para>
    /// </summary>
    [Collection("restep")]
    [Trait("Category", "Swim")]
    public class RestepSwimTests : IDisposable
    {
        private const string PilotPool = "scratch/nursery-trials/s1-pilot-pool.tsv";
        private const int Minutes = 15;

        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("restep-swim");

        public RestepSwimTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        // ------------------------------------------------------------------ S3: the watcher changes nothing

        [SwimFact(PilotPool)]
        public void S3_AWatchedSwimAndAnUnwatchedSwimGiveByteForByteTheSameResult()
        {
            // The pilot's draw 0 under its own brains, thirty simulated seconds long: the short episode, and the same six genomes in the same slots.
            string recipe = Path.Combine(_dir, "s3-short.json");
            File.WriteAllText(recipe, "{\"base\": \"rounds/nursery/brain2-w10.json\", \"name\": \"s3-short\", \"setup\": {\"seconds\": 30}}");
            string request = RestepFixtures.WriteRequest(_dir, RestepFixtures.RequestJson(RestepFixtures.Relative(recipe), Recipe.Load(recipe).Hash(), PilotPool, RestepFixtures.PilotTank0));
            string unwatched = Path.Combine(_dir, "unwatched"), watched = Path.Combine(_dir, "watched");

            // Two children of the same Release program: one with no watcher at all (the test-only --no-watch), one with the recording watcher (the room's own).
            (int code, string stdout, string stderr) = RestepProcess.Run(Minutes, "--restep", request, "--out", unwatched, "--no-watch", "--threads", "1");
            _out.WriteLine(stdout);
            Assert.True(code == 0, "the unwatched swim: " + stderr);
            (int code2, string stdout2, string stderr2) = RestepProcess.Run(Minutes, "--restep", request, "--out", watched, "--threads", "1");
            _out.WriteLine(stdout2);
            Assert.True(code2 == 0, "the recording watcher's swim: " + stderr2);

            // The unwatched run wrote its result and nothing else; the recording run wrote its four files.
            Assert.Equal(new[] { Path.Combine(unwatched, "result.json") }, Directory.GetFileSystemEntries(unwatched));
            foreach (string name in new[] { "episode.json", "frames.bin", "events.jsonl", "result.json" })
                Assert.True(File.Exists(Path.Combine(watched, name)), "the watched run's " + name + " is missing");

            // result.json is the seed, the control, and every slot's net as its round-trip text and its 64 bits: the two are equal byte for byte.
            byte[] a = File.ReadAllBytes(Path.Combine(unwatched, "result.json")), b = File.ReadAllBytes(Path.Combine(watched, "result.json"));
            Assert.True(a.SequenceEqual(b), "result.json differs between the unwatched and the watched swim:\n--- unwatched\n" + File.ReadAllText(Path.Combine(unwatched, "result.json")) +
                                            "\n--- watched\n" + File.ReadAllText(Path.Combine(watched, "result.json")));
            JsonNode result = Json.Parse(File.ReadAllText(Path.Combine(unwatched, "result.json")));
            Assert.Equal(RestepFixtures.PilotSeed0, result["episodeSeed"].AsString());
            Assert.Contains(result["members"].Items(), m => m["landed"].AsBool());

            // And the recording watcher's own run: it recorded frames, and a solver digest at the end.
            JsonNode episode = Json.Parse(File.ReadAllText(Path.Combine(watched, "episode.json")));
            Assert.True(episode["frames"].AsInt() > 0);
            Assert.NotEqual("0000000000000000", episode["finalSolverDigest"].AsString());
            AssertTieringWasOff(episode);
            _out.WriteLine($"30 s episode: {episode["frames"].AsInt()} frames of {episode["frameFloats"].AsInt()} channels, digest {episode["finalSolverDigest"].AsString()}");
        }

        private static void AssertTieringWasOff(JsonNode episode)
        {
            JsonNode tiered = episode["build"]["tieredCompilation"];
            Assert.True(tiered.Kind == JsonNode.NodeKind.String && string.Equals(tiered.AsString(), "false", StringComparison.OrdinalIgnoreCase),
                "the child ran with tiered compilation " + (tiered.Kind == JsonNode.NodeKind.String ? tiered.AsString() : "unknown") + ", and the pilot's program ran it off");
        }

        // ------------------------------------------------------------------ S4: retired on main; it runs at the old commit

        // S4 re-stepped the pilot's own draw 0 (registry entry 1's recipe, rounds/nursery/brain2.json) and required its six nets to equal the pilot's
        // per[0] bit for bit. It restates the pilot's draw, which only a build that loads the pilot's world can make: this build refuses
        // configs/7319d6b528cc77f1.json, which lacks the season and floor-pool-share tunables. It runs at nursery-room e824ed4 (the test is there, with
        // this file's command), or on any later build that still loads that config.
    }
}
