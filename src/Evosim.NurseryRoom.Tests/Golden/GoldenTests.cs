using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using Evosim.Nursery;
using Evosim.NurseryRoom.Tests.E2e;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Evosim.NurseryRoom.Tests.Golden
{
    /// <summary>
    /// What agents read through the room's MCP is saved, one answer per resource, on the fixture runs and recipes of <see cref="E2eWorld"/> (logbook/specs/nursery-room-tests-plan.md, step 4).
    /// A change in an answer can break an agent without breaking any screen, so each answer is compared exactly: a field's name, its order and its value, except for the fields
    /// <see cref="Masks"/> lists, whose values change from run to run. Re-record with ROOM_GOLDEN_UPDATE=1, on purpose, and say why in the commit.
    /// </summary>
    public sealed class GoldenWorld : IDisposable
    {
        internal readonly E2eWorld World = new E2eWorld();
        public void Dispose() => World.Dispose();
    }

    public class GoldenTests : IClassFixture<GoldenWorld>
    {
        private readonly E2eWorld _w;
        public GoldenTests(GoldenWorld world) { _w = world.World; }

        /// <summary>
        /// Every field whose value changes from run to run, in one list, by path (<see cref="Mask"/>). Nothing else is masked, and no pattern over the text is: a field not listed here
        /// is compared exactly. A test fails when an entry here finds no field in any answer, so the list cannot hold a name the room stopped using.
        /// </summary>
        internal static readonly IReadOnlyList<Mask> Masks = new List<Mask>
        {
            // the clock, ages and file times
            new Mask("nowUtc", "clock", "the time of the read (registry, run)"),
            new Mask("items[].startedUtc", "clock", "a run with no start of its own is dated by its folder's creation time, which is the test's own"),
            new Mask("items[].progress.lastRowUtc", "clock", "the file time of the last row the test wrote"),
            new Mask("items[].progress.lastRowAgeSeconds", "age", "now less that file time"),
            new Mask("progress.lastRowUtc", "clock", "the file time of the last row the test wrote"),
            new Mask("progress.lastRowAgeSeconds", "age", "now less that file time"),
            new Mask("stall.lastWriteUtc", "clock", "the file time of the latest file the test wrote"),
            new Mask("stall.sinceWriteSeconds", "age", "now less that file time"),
            new Mask("files[].modifiedUtc", "clock", "the file time of each file the test wrote"),
            // the change stamp embeds the folders' creation ticks
            new Mask("items[].version", "stamp", "the run folder's creation ticks sit inside the stamp (the live run's folder is made when the test starts)"),
            new Mask("version", "stamp", "the registry's stamp joins the runs' stamps; and a run's, with its folder's file times where it has any"),
            // the test repository's commit, and the file's SHA-256, which follows the checkout's line endings
            new Mask("gitState.detail", "git", "git's sentence names the commit's short hash and date"),
            new Mask("gitState.lastCommit.hash", "git", "the commit's short hash, made by the test's own git repository"),
            new Mask("gitState.lastCommit.date", "git", "the commit's date, made by the test's own git repository"),
            new Mask("fileSha256", "sha256", "the SHA-256 of the file as checked out: its line endings follow the machine's git settings"),
            // not hidden: the file's text, with its line endings made one kind
            new Mask("text", "text", "the recipe file as checked out: its line endings follow the machine's git settings", MaskKind.Lf),
        };

        // ------------------------------------------------------------------ what is read

        private const string RunUri = "nursery://run/" + E2eWorld.Alpha;
        private const string RecipeUri = "nursery://recipe/" + E2eWorld.Committed;
        private const string CompareUri = "nursery://exam-compare/" + E2eWorld.Alpha + "/vs/" + E2eWorld.Gamma;
        /// <summary>A run cannot be its own control: the resolver refuses.</summary>
        private const string RefusedUri = "nursery://exam-compare/" + E2eWorld.Alpha + "/vs/" + E2eWorld.Alpha;

        /// <summary>The recipe's git state is asked of the real git in the fixture's repository (<see cref="E2eWorld"/> makes one), not a fake.</summary>
        private ResourceResolver Resolver() => new ResourceResolver(_w.Room);

        private object Read(string uri) => Resolver().Read(uri);

        private JsonNode Masked(object answer, ISet<string> hit = null) => GoldenAnswer.Masked(answer, Masks, _w.Root, hit);

        /// <summary>The refused read as both of its callers see it: <c>/api/resource</c> over HTTP (the room hosted on a free port) and the MCP's read.</summary>
        private object Refusal(string uri)
        {
            using (RoomHost host = _w.Host())
            using (var http = new HttpClient())
            {
                HttpResponseMessage response = http.GetAsync(host.Base + "/api/resource?uri=" + Uri.EscapeDataString(uri)).GetAwaiter().GetResult();
                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var mcp = Assert.ThrowsAny<McpException>(() => McpSurface.Read(Resolver(), uri));
                return new
                {
                    uri,
                    api = new { httpStatus = (int)response.StatusCode, body = JsonNode.Parse(body) },
                    mcp = new { errorCode = (int?)(mcp as McpProtocolException)?.ErrorCode, message = mcp.Message },
                };
            }
        }

        // ------------------------------------------------------------------ the five answers

        [Fact]
        public void TheRegistryAnswerIsTheSavedOne() => GoldenAnswer.Check("registry", Masked(Read("nursery://registry")));

        [Fact]
        public void ARunWithAnExamAnswersAsSaved() => GoldenAnswer.Check("run", Masked(Read(RunUri)));

        [Fact]
        public void ARecipeAnswersAsSaved() => GoldenAnswer.Check("recipe", Masked(Read(RecipeUri)));

        [Fact]
        public void TwoExamsComparedAnswerAsSaved() => GoldenAnswer.Check("exam-compare", Masked(Read(CompareUri)));

        [Fact]
        public void ARefusedReadAnswersAsSaved() => GoldenAnswer.Check("refused", Masked(Refusal(RefusedUri)));

        // ------------------------------------------------------------------ the guards on the saved answers themselves

        [Fact]
        public void EveryMaskInTheListMasksSomething()
        {
            var hit = new HashSet<string>();
            foreach (object answer in new[] { Read("nursery://registry"), Read(RunUri), Read(RecipeUri), Read(CompareUri), Refusal(RefusedUri) })
                Masked(answer, hit);
            string[] idle = Masks.Select(m => m.Path).Where(p => !hit.Contains(p)).ToArray();
            Assert.True(idle.Length == 0, "these masks find no field in any of the five answers, so the room no longer says them: " + string.Join(", ", idle));
        }

        [Fact]
        public void NoSavedAnswerHoldsTheFixtureRoot()
        {
            string[] roots = { _w.Root, _w.Root.Replace('\\', '/'), _w.Root.Replace("\\", "\\\\"), _w.Fx.Dir, _w.Fx.Dir.Replace('\\', '/') };
            foreach ((string name, object answer) in new[] { ("registry", Read("nursery://registry")), ("run", Read(RunUri)), ("recipe", Read(RecipeUri)), ("exam-compare", Read(CompareUri)), ("refused", Refusal(RefusedUri)) })
            {
                string text = GoldenAnswer.Pretty(Masked(answer));
                foreach (string root in roots)
                    Assert.False(text.Contains(root, StringComparison.OrdinalIgnoreCase), $"the \"{name}\" answer holds an absolute path, {root}: mask the field that holds it with a Root mask, so that the rest of its text is still compared");
            }
        }

        [Fact]
        public void ARootMaskKeepsTheRestOfTheTextAndALfMaskOnlyTheLineEndings()
        {
            // a made-up root: nothing is read or written there
            string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetPathRoot(Where.Root), "fixture-root"));
            var masks = new List<Mask> { new Mask("a", "p", "test", MaskKind.Root), new Mask("b", "t", "test", MaskKind.Lf), new Mask("c", "t", "test") };
            JsonNode n = GoldenAnswer.Masked(new { a = root + "\\scratch\\run", b = "x\r\ny", c = "changes", d = "kept" }, masks, root);
            Assert.Equal("<root>/scratch/run", (string)n["a"]);
            Assert.Equal("x\ny", (string)n["b"]);
            Assert.Equal("<masked:t>", (string)n["c"]);
            Assert.Equal("kept", (string)n["d"]);
        }

        [Fact]
        public void TheDifferenceIsReportedAsAPathWithBothValues()
        {
            JsonNode saved = JsonNode.Parse("{\"a\":1,\"b\":{\"c\":[1,2,{\"d\":\"x\"}]}}");
            Assert.Null(GoldenAnswer.FirstDifference(saved, JsonNode.Parse("{\"a\":1,\"b\":{\"c\":[1,2,{\"d\":\"x\"}]}}"), "$"));
            Assert.Equal("$.b.c[2].d: saved \"x\", new \"y\"", GoldenAnswer.FirstDifference(saved, JsonNode.Parse("{\"a\":1,\"b\":{\"c\":[1,2,{\"d\":\"y\"}]}}"), "$"));
            Assert.Contains("member #1", GoldenAnswer.FirstDifference(saved, JsonNode.Parse("{\"a\":1,\"B\":{\"c\":[1,2,{\"d\":\"x\"}]}}"), "$"));
            Assert.Contains("member #0", GoldenAnswer.FirstDifference(saved, JsonNode.Parse("{\"b\":{\"c\":[1,2,{\"d\":\"x\"}]},\"a\":1}"), "$"));   // the order counts
            Assert.Contains("holds 3 element(s), the new one 2", GoldenAnswer.FirstDifference(saved, JsonNode.Parse("{\"a\":1,\"b\":{\"c\":[1,2]}}"), "$"));
        }
    }
}
