using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// What the owner found on opening the room (the fixes of round A). Unit tests had not seen that the recipe list was empty, because the types it was made of
    /// held public fields, which <see cref="Js.Out"/> does not write; these tests serialise the responses and look at what comes out, and one goes through the
    /// real HTTP pipeline.
    /// </summary>
    public class RoomResponseShapeTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        /// <summary>Every object in the JSON that has no member at all, by its path.</summary>
        internal static List<string> EmptyObjects(JsonElement e, string at = "$")
        {
            var found = new List<string>();
            if (e.ValueKind == JsonValueKind.Object)
            {
                if (!e.EnumerateObject().Any()) found.Add(at);
                foreach (JsonProperty p in e.EnumerateObject()) found.AddRange(EmptyObjects(p.Value, at + "." + p.Name));
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (JsonElement x in e.EnumerateArray()) found.AddRange(EmptyObjects(x, at + "[" + i++ + "]"));
            }
            return found;
        }

        /// <summary>The types of a response's object graph that hold public fields: <see cref="Js.Out"/> writes properties only, so each is written as {} or with its fields gone.</summary>
        internal static void FieldBearing(object o, ISet<Type> found, int depth = 0)
        {
            if (o == null || depth > 14) return;
            Type t = o.GetType();
            if (t.IsPrimitive || t.IsEnum || o is string || o is JsonElement || (t.Namespace != null && t.Namespace.StartsWith("System", StringComparison.Ordinal) && !(o is IEnumerable))) return;
            if (o is IDictionary d) { foreach (DictionaryEntry e in d) FieldBearing(e.Value, found, depth + 1); return; }
            if (o is IEnumerable en) { foreach (object x in en) FieldBearing(x, found, depth + 1); return; }
            if (t.GetFields(BindingFlags.Public | BindingFlags.Instance).Length > 0) found.Add(t);
            foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                object v;
                try { v = p.GetValue(o); } catch (TargetInvocationException) { continue; }
                FieldBearing(v, found, depth + 1);
            }
        }

        private string MakeRun(string name, params (string file, string text)[] files)
        {
            string p = Path.Combine(_fx.RunsDir, name);
            Directory.CreateDirectory(p);
            foreach (var (file, text) in files) File.WriteAllText(Path.Combine(p, file), text);
            return p;
        }

        /// <summary>A type with a public field, as the resource types were.</summary>
        public sealed class WithAField { public string Name = "x"; }

        [Fact]
        public void TheResourceListAndTemplatesAreWrittenWithTheirMembers()
        {
            var resolver = new ResourceResolver(_fx.Room);
            JsonElement all = Js.Parse(Js.Serialize(new { resources = resolver.List(), templates = resolver.Templates }));
            Assert.Empty(EmptyObjects(all));
            JsonElement[] resources = all.GetProperty("resources").EnumerateArray().ToArray();
            Assert.NotEmpty(resources);
            foreach (JsonElement r in resources)
            {
                Assert.StartsWith("nursery://", r.GetProperty("uri").GetString());
                Assert.False(string.IsNullOrEmpty(r.GetProperty("name").GetString()));
                Assert.False(string.IsNullOrEmpty(r.GetProperty("description").GetString()));
            }
            Assert.Contains(resources, r => r.GetProperty("uri").GetString() == "nursery://recipe/rounds/nursery/brain2-w5.json");
            foreach (JsonElement t in all.GetProperty("templates").EnumerateArray())
            {
                Assert.Contains("{", t.GetProperty("uriTemplate").GetString());
                Assert.False(string.IsNullOrEmpty(t.GetProperty("name").GetString()));
            }
        }

        [Fact]
        public void NoPublicFieldIsLeftOnAResourceInfoOrATemplate()
        {
            // the cause of the empty list: a field is not a property
            foreach (Type t in new[] { typeof(ResourceInfo), typeof(ResourceTemplateInfo) })
                Assert.Empty(t.GetFields(BindingFlags.Public | BindingFlags.Instance));
        }

        [Fact]
        public void NoResponseTheRoomMakesHoldsATypeWithPublicFields()
        {
            string run = MakeRun("shaped", ("tank-generations.jsonl", "{\"gen\":0,\"seconds\":60,\"netMean\":5,\"top\":{\"score\":9,\"body\":1}}\n{\"gen\":1,\"seconds\":120,\"netMean\":6,\"top\":{\"score\":10,\"body\":1}}\n"),
                ("recipe.json", "{\"name\":\"n\",\"description\":\"a run\",\"search\":{\"generations\":50},\"reward\":{\"class\":\"Net\"}}"));
            RegistryEntry.Append(_fx.Registry, new RegistryEntry { Recipe = "first", RecipeHash = "aa", RecipePath = "rounds/nursery/brain2-w5.json", ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = run, Status = "completed", StartedAtUtc = "2026-10-01T00:00:00Z", EndedAtUtc = "2026-10-01T01:00:00Z" });
            var resolver = new ResourceResolver(_fx.Room);
            var found = new HashSet<Type>();
            var responses = new List<object>
            {
                resolver.List(), resolver.Templates, resolver.Registry(), resolver.Activity(), resolver.Run("1"), resolver.RecipeView("rounds/nursery/brain2-w5.json"),
            };
            foreach (string read in new[] { "nursery://genomes/1", "nursery://exam-plan/1" })
            {
                try { responses.Add(resolver.Read(read)); } catch (Exception e) when (e is ResourceNotFound || e is ResourceRefused) { /* a run with no genomes or no plan is refused, with why: that is a response too, and holds no type */ }
            }
            foreach (object r in responses)
            {
                FieldBearing(r, found);
                Js.Serialize(r);
            }
            Assert.True(found.Count == 0, "types with public fields in a response (written as {}): " + string.Join(", ", found.Select(t => t.FullName)));
        }

        [Fact]
        public void TheScanForPublicFieldsFindsOneWhereItIs()
        {
            var found = new HashSet<Type>();
            FieldBearing(new { items = new List<object> { new WithAField() } }, found);
            Assert.Contains(typeof(WithAField), found);
            // and the writer does write such a type as nothing, which is what emptied the recipe list
            Assert.Equal("{}", Js.Serialize(new WithAField()));
            Assert.Equal(new[] { "$.items[0]" }, EmptyObjects(Js.Parse(Js.Serialize(new { items = new List<object> { new WithAField() } }))).ToArray());
        }
    }

    /// <summary>The room's own HTTP pipeline, on a port of its own (never 5179, which is the room the owner uses).</summary>
    public class RoomHttpTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            Assert.NotEqual(5179, port);
            return port;
        }

        private WebApplication Start(out int port)
        {
            port = FreePort();
            RoomSettings settings = RoomSettings.Parse(Where.SettingsJson(runsDir: "runs").Replace("\"port\":5179", "\"port\":" + port).Replace("artifacts/Evosim.Nursery/bin/Debug/net8.0/Evosim.Nursery.dll", "bin/nursery.dll"));
            var room = new RoomContext(_fx.Root, settings, _fx.Main, _fx.Registry, Path.Combine(_fx.Dir, "activity-http.jsonl"), _fx.Launcher, _fx.Load, 32, _ => _fx.Build);
            WebApplication app = Program.Create(room, Where.WwwRoot);
            app.Start();
            return app;
        }

        [Fact]
        public async Task ApiResourcesThroughTheRealPipelineGivesEveryItemAUri()
        {
            WebApplication app = Start(out int port);
            try
            {
                using var http = new HttpClient();
                string text = await http.GetStringAsync($"http://127.0.0.1:{port}/api/resources");
                JsonElement body = Js.Parse(text);
                JsonElement[] resources = body.GetProperty("resources").EnumerateArray().ToArray();
                Assert.NotEmpty(resources);
                foreach (JsonElement r in resources)
                {
                    Assert.True(r.TryGetProperty("uri", out JsonElement uri), "an item with no uri: " + r.GetRawText());
                    Assert.StartsWith("nursery://", uri.GetString());
                }
                // the recipe screen's picker reads these: a recipe the room holds is in the list under its address
                Assert.Contains(resources, r => r.GetProperty("uri").GetString().StartsWith("nursery://recipe/rounds/nursery/", StringComparison.Ordinal));
                Assert.Empty(RoomResponseShapeTests.EmptyObjects(body));

                // every JSON answer a screen reads on opening, through the same pipeline: none is an empty object
                foreach (string path in new[] { "/api/room", "/api/resource?uri=nursery%3A%2F%2Fregistry", "/api/resource?uri=nursery%3A%2F%2Factivity", "/api/resource?uri=" + Uri.EscapeDataString("nursery://recipe/rounds/nursery/brain2-w5.json") })
                {
                    string answer = await http.GetStringAsync($"http://127.0.0.1:{port}{path}");
                    Assert.NotEqual("{}", answer);
                    Assert.True(Js.Parse(answer).EnumerateObject().Any(), path);
                }
            }
            finally { await app.StopAsync(); }
        }

        [Fact]
        public async Task AResourceTheRoomDoesNotHaveIsAnAnswerWithARefusedFieldAndTheReasonAndNotAFailedRequest()
        {
            // A 4xx would be logged in the browser's console as an error whatever the page does with it, and a run with no exam plan is an ordinary state:
            // the room answers HTTP 200 with { ok: false, refused: true, status: 404 | 400, error } and the screens read `refused`.
            WebApplication app = Start(out int port);
            try
            {
                using var http = new HttpClient();
                async Task<JsonElement> Read(string uri)
                {
                    HttpResponseMessage r = await http.GetAsync($"http://127.0.0.1:{port}/api/resource?uri=" + Uri.EscapeDataString(uri));
                    Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                    JsonElement b = Js.Parse(await r.Content.ReadAsStringAsync());
                    Assert.False(b.GetProperty("ok").GetBoolean());
                    Assert.True(b.GetProperty("refused").GetBoolean());
                    return b;
                }
                JsonElement run = await Read("nursery://run/no-such-run");
                Assert.Equal(404, run.GetProperty("status").GetInt32());
                Assert.Contains("no run", run.GetProperty("error").GetString());
                JsonElement recipe = await Read("nursery://recipe/rounds/nursery/nope.json");
                Assert.Equal(404, recipe.GetProperty("status").GetInt32());
                Assert.Contains("no recipe at rounds/nursery/nope.json", recipe.GetProperty("error").GetString());
                // a bad address is a 400 in the body, and a run that has no exam plan is a refusal that says why, not a failed request
                Assert.Equal(400, (await Read("http://example.com")).GetProperty("status").GetInt32());
                JsonElement noPlan = await Read("nursery://exam-plan/no-such-run");
                Assert.Contains("no run", noPlan.GetProperty("error").GetString());
                // and a read that works has no `refused` field
                JsonElement ok = Js.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/api/resource?uri=" + Uri.EscapeDataString("nursery://registry")));
                Assert.False(ok.TryGetProperty("refused", out _));
            }
            finally { await app.StopAsync(); }
        }
    }

    /// <summary>One folder is one row, and a folder with no run.json that an entry made has the entry's state.</summary>
    public class RegistryOneRowPerFolderTests : IDisposable
    {
        private readonly Fx _fx;
        public RegistryOneRowPerFolderTests() { _fx = new Fx("runs/the-folder"); }
        public void Dispose() => _fx.Dispose();

        private string MakeFolder(string name)
        {
            string p = Path.Combine(_fx.RunsDir, name);
            Directory.CreateDirectory(p);
            File.WriteAllText(Path.Combine(p, "tank-generations.jsonl"), "{\"gen\":0,\"seconds\":60,\"netMean\":5,\"top\":{\"score\":9,\"body\":1}}\n");
            return p;
        }

        private void Enter(string recipe, string folder, string status = "completed", bool rerunnable = true, string because = null) =>
            RegistryEntry.Append(_fx.Registry, new RegistryEntry { Recipe = recipe, RecipeHash = "aa", RecipePath = "rounds/nursery/brain2-w5.json", ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = folder, Status = status, Rerunnable = rerunnable, NotRerunnableBecause = because, StartedAtUtc = "2026-10-01T00:00:00Z", EndedAtUtc = "2026-10-01T01:00:00Z" });

        [Fact]
        public void AnEntryAndARoomJsonRunThatAreTheSameFolderAreOneRowCarryingBoth()
        {
            string folder = MakeFolder("the-folder");
            Enter("first", folder);
            var resolver = new ResourceResolver(_fx.Room);
            JsonElement d = Js.Parse(Js.Serialize(resolver.Registry()));
            Assert.Equal(1, d.GetProperty("items").GetArrayLength());
            Assert.Equal(1, d.GetProperty("entries").GetInt32());
            Assert.Equal(0, d.GetProperty("runs").GetInt32());
            JsonElement row = d.GetProperty("items")[0];
            Assert.Equal("1", row.GetProperty("id").GetString());
            Assert.Equal(1, row.GetProperty("number").GetInt32());
            Assert.Equal("the-folder", row.GetProperty("folderName").GetString());
            Assert.EndsWith("runs/the-folder", row.GetProperty("folder").GetString());
            Assert.Equal("completed", row.GetProperty("phase").GetString());
            // and the folder's own name still finds it
            Assert.Equal("1", resolver.FindRun("the-folder").Id);
            Assert.Equal("1", resolver.FindRun("runs/the-folder").Id);
        }

        [Fact]
        public void AnEntryAnswersToItsFoldersNameEvenWhereRoomJsonDoesNotListTheFolder()
        {
            // entry 2's folder in the real registry (accept-1gen) is in no room.json: run.html?id=accept-1gen was refused since one row per folder was made
            MakeFolder("the-folder");
            string other = MakeFolder("accept-1gen");
            Enter("first", Path.Combine(_fx.RunsDir, "the-folder"));
            Enter("accept-1gen", other);
            var resolver = new ResourceResolver(_fx.Room);
            Assert.Equal("2", resolver.FindRun("2").Id);
            Assert.Equal("2", resolver.FindRun("accept-1gen").Id);
            Assert.Equal("2", resolver.FindRun("ACCEPT-1gen").Id);
            Assert.Equal("2", resolver.FindRun("runs/accept-1gen").Id);
            Assert.Equal("2", Js.Parse(Js.Serialize(resolver.Read("nursery://run/accept-1gen"))).GetProperty("id").GetString());
            // the one row stays one row, and the folder named in room.json still finds its entry
            Assert.Equal(2, Js.Parse(Js.Serialize(resolver.Registry())).GetProperty("items").GetArrayLength());
            Assert.Equal("1", resolver.FindRun("the-folder").Id);
            // two entries made one folder: the name finds the later one, as room.json's twin does
            Enter("accept-1gen-again", other);
            Assert.Equal("3", resolver.FindRun("accept-1gen").Id);
            Assert.Throws<ResourceNotFound>(() => resolver.FindRun("no-such-folder"));
        }

        [Fact]
        public void ARoomJsonRunWhoseFolderNoEntryMadeIsStillItsOwnRow()
        {
            MakeFolder("the-folder");
            MakeFolder("another");
            Enter("first", Path.Combine(_fx.RunsDir, "another"));
            var resolver = new ResourceResolver(_fx.Room);
            JsonElement d = Js.Parse(Js.Serialize(resolver.Registry()));
            Assert.Equal(2, d.GetProperty("items").GetArrayLength());
            Assert.Equal(1, d.GetProperty("entries").GetInt32());
            Assert.Equal(1, d.GetProperty("runs").GetInt32());
        }

        [Fact]
        public void AFolderWithNoRunJsonThatAnEntryMadeSaysWhatTheRegistrySaysAndNotNotRecorded()
        {
            string folder = MakeFolder("the-folder");
            Enter("first", folder, "completed");
            var resolver = new ResourceResolver(_fx.Room);
            JsonElement run = Js.Parse(Js.Serialize(resolver.Run("1")));
            JsonElement state = run.GetProperty("state");
            Assert.Equal("completed", state.GetProperty("phase").GetString());
            Assert.True(state.GetProperty("fromRegistry").GetBoolean());
            Assert.Equal("completed, from the registry; this run predates run.json", state.GetProperty("note").GetString());
            Assert.NotEqual("unrecorded", state.GetProperty("phase").GetString());
            // the same folder asked for by its own name is the same run in the same state
            Assert.Equal("completed", Js.Parse(Js.Serialize(resolver.Run("the-folder"))).GetProperty("state").GetProperty("phase").GetString());
            // a run no entry made, with no record, stays what it is
            using (var other = new Fx("runs/bare"))
            {
                string p = Path.Combine(other.RunsDir, "bare");
                Directory.CreateDirectory(p);
                File.WriteAllText(Path.Combine(p, "tank-generations.jsonl"), "{\"gen\":0,\"seconds\":60,\"netMean\":5,\"top\":{\"score\":9,\"body\":1}}\n");
                JsonElement bare = Js.Parse(Js.Serialize(new ResourceResolver(other.Room).Run("bare")));
                Assert.Equal("unrecorded", bare.GetProperty("state").GetProperty("phase").GetString());
                Assert.False(bare.GetProperty("state").TryGetProperty("fromRegistry", out _));
            }
        }

        [Fact]
        public void TheRegistryGivesEachRowItsRecipeAndSaysWhetherTheFileIsHere()
        {
            string folder = MakeFolder("the-folder");
            Enter("first", folder);
            RegistryEntry.Append(_fx.Registry, new RegistryEntry { Recipe = "second", RecipeHash = "bb", RecipePath = "rounds/nursery/missing.json", ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = Path.Combine(_fx.RunsDir, "nowhere"), Status = "completed", Rerunnable = false, NotRerunnableBecause = "a reason", StartedAtUtc = "2026-10-02T00:00:00Z" });
            JsonElement d = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Registry()));
            JsonElement first = d.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("name").GetString() == "first");
            JsonElement second = d.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("name").GetString() == "second");
            Assert.Equal("rounds/nursery/brain2-w5.json", first.GetProperty("recipePath").GetString());
            Assert.True(first.GetProperty("recipeExists").GetBoolean());
            Assert.Equal("rounds/nursery/missing.json", second.GetProperty("recipePath").GetString());
            Assert.False(second.GetProperty("recipeExists").GetBoolean());
            Assert.False(second.GetProperty("rerunnable").GetBoolean());
            Assert.Equal("a reason", second.GetProperty("notRerunnableBecause").GetString());
        }
    }

    /// <summary>What the screens' sources promise, as the existing source scans do: the five tabs from one place, an icon on every page, the controls of round A.</summary>
    public class RoomScreenSourceTests
    {
        private static string Page(string name) => File.ReadAllText(Path.Combine(Where.WwwRoot, name));
        private static string Js_(string name) => File.ReadAllText(Path.Combine(Where.WwwRoot, "js", name));
        private static readonly string[] Pages = { "registry.html", "recipe.html", "run.html", "tank.html", "genome.html" };

        [Fact]
        public void EveryPageHasTheSharedStripAndNoTabsOfItsOwn()
        {
            foreach (string p in Pages)
            {
                string html = Page(p);
                Assert.Matches(@"<header class=""strip"" id=""strip""", html);
                Assert.DoesNotContain("<nav class=\"tabs\"", html);
                Assert.DoesNotContain("class=\"tab", html);
                Assert.DoesNotContain("<span class=\"brand\"", html);
            }
            Assert.Contains("data-verdict", Page("recipe.html"));
        }

        [Fact]
        public void TheFiveTabsAreOneListInTheMocksOrderAndNumbering()
        {
            string common = Js_("common.js");
            Match m = Regex.Match(common, @"const SCREENS = \[(?<l>[^\]]*(?:\][^\]]*)*?)\];\s*export const PAGE");
            Assert.True(m.Success);
            string[] names = Regex.Matches(m.Groups["l"].Value, @"\['(\w+)', '(\w+)'\]").Select(x => x.Groups[1].Value + ":" + x.Groups[2].Value).ToArray();
            Assert.Equal(new[] { "registry:REGISTRY", "recipe:RECIPE", "run:RUN", "tank:TANK", "genome:GENOME" }, names);
            // numbered in that order, the current one marked, and each carries what it can of the selection
            Assert.Contains("<b>${i + 1}</b>${label}", common);
            Assert.Contains("aria-current=\"page\"", common);
            Assert.Contains("if (name === 'run') put('id', s.run);", common);
            Assert.Contains("if (name === 'genome') { put('run', s.run); put('id', s.genome); }", common);
            Assert.Contains("if (name === 'tank') { put('run', s.run); put('brain', s.brain); put('draw', s.draw); put('tank', s.tank); }", common);
            // kept in the URL when there is one and otherwise in localStorage, which may throw
            Assert.Matches(@"try \{ const s = JSON\.parse\(localStorage\.getItem\(SEL_KEY\)\)", common);
            Assert.Matches(@"try \{ localStorage\.setItem\(SEL_KEY", common);
        }

        [Fact]
        public void EveryPageCarriesAnInlineIconSoThatNoFaviconIsAsked()
        {
            foreach (string p in Pages)
                Assert.Matches(@"<link rel=""icon"" href=""data:image/svg\+xml,", Page(p));
        }

        [Fact]
        public void EveryPagesPollingSaysItsErrorOnThePageAsWellAsInTheConsole()
        {
            string common = Js_("common.js");
            // a fault is logged and said; a refusal (the room's `refused` answer, an expected state) is said on the page and is not a console error
            Assert.Contains("catch (e) { if (!e.refused) console.error(e); showPollError(e); }", common);
            Assert.Contains("role", common);
            // every page polls through common.js's poll, and none keeps a catch of its own that only logs
            foreach (string js in new[] { "registry.js", "recipe.js", "run.js", "tank.js", "genome.js" })
            {
                string text = Js_(js);
                Assert.Contains("poll(", text);
                Assert.DoesNotContain("setInterval(", text.Replace("setInterval(() => getRoom()", ""));
            }
        }

        [Fact]
        public void AMissingRunAndAMissingRecipeAreRefusalsInTheMainAreaWithALinkBack()
        {
            string common = Js_("common.js"), run = Js_("run.js"), recipe = Js_("recipe.js");
            Assert.Contains("export function refuse(", common);
            Assert.Contains("main.innerHTML", common);
            Assert.Contains("refuse({", run);
            Assert.Contains("'No such run'", run);
            Assert.Contains("['registry.html', 'BACK TO THE REGISTRY']", run);
            Assert.Contains("'No such recipe'", recipe);
            Assert.Contains("['recipe.html', 'CHOOSE A RECIPE']", recipe);
            Assert.Contains("return 'stop';", recipe);
        }

        [Fact]
        public void TheRunScreensRerunHonoursRerunnableReferenceAndTheReason()
        {
            string run = Js_("run.js");
            Assert.Contains("e && e.reference ?", run);
            Assert.Contains("e.rerunnable === false", run);
            Assert.Contains("e.notRerunnableBecause", run);
            Assert.Contains("rerunBtn.disabled = !!why;", run);
            Assert.Contains("rerunBtn.title = why ||", run);
            Assert.Contains("$('rerun-why').textContent = why", run);
            Assert.Contains("id=\"rerun-why\"", Page("run.html"));
        }

        [Fact]
        public void TheRegistryOffersOpenRecipeAndALinkOnEachRowWhoseRecipeIsKnown()
        {
            string js = Js_("registry.js");
            Assert.Contains("OPEN RECIPE", js);
            Assert.Contains("recipeLink(i, 'btn', 'OPEN RECIPE')", js);
            Assert.Contains("recipeLink(i, 'hash glink'", js);
            Assert.Contains("i.recipePath && i.recipeExists", js);
        }

        [Fact]
        public void EveryDialogThatNeedsTheMachineOpensAtOnceAndFillsItInWhenTheRoomAnswers()
        {
            string common = Js_("common.js");
            Assert.Contains("reading the machine…", common);
            Assert.Contains("getRoom().then(fill).catch(() => fill(null))", common);
            foreach (string js in new[] { "registry.js", "recipe.js", "run.js", "tank.js" })
                Assert.DoesNotMatch(@"await getRoom\(\)", Js_(js));
            foreach (string js in new[] { "registry.js", "recipe.js", "run.js" })
                Assert.Contains("machine:", Js_(js));
        }

        [Fact]
        public void NoPageWaitsForTheMachinesLoadBeforeItReadsItsOwnData()
        {
            foreach (string js in new[] { "registry.js", "recipe.js", "run.js", "genome.js", "tank.js" })
                Assert.DoesNotMatch(@"=\s*await mountMachine\(", Js_(js));
        }

        [Fact]
        public void EveryScrollbarIsStyledAndTheRegistryTableIsLaidOutToFixedWidths()
        {
            string css = File.ReadAllText(Path.Combine(Where.WwwRoot, "nursery.css"));
            Assert.Contains("scrollbar-color", css);
            Assert.Contains("::-webkit-scrollbar-thumb", css);
            string reg = Page("registry.html");
            Assert.Contains("table-layout: fixed", reg);
            foreach (string col in new[] { "c-n", "c-state", "c-reward", "c-start", "c-gens", "c-spark", "c-best", "c-rerun", "c-recipe" })
                Assert.Contains("col." + col, reg);
            Assert.Contains("class=\"cut\"", Js_("registry.js").Replace("<span class=\"cut\" title", "class=\"cut\" title"));
        }

        [Fact]
        public void TheRecipeScreenKeepsItsCheckAndLaunchBesideTheFormAtAnyWidth()
        {
            string html = Page("recipe.html");
            // the old rule that put the side column on a third row, below the form, is gone
            Assert.DoesNotContain("grid-row: 3 !important", html);
            Assert.Matches(@"@media \(max-width: 1600px\) \{ \.page \{ grid-template-columns: 11rem minmax\(0, 1fr\) minmax\(0, 1fr\) 25rem; \} \}", html);
            // launch comes before the (possibly long) diff list, so that its buttons are in view
            string js = Js_("recipe.js");
            Assert.True(js.IndexOf("class=\"section\" id=\"launch\"", StringComparison.Ordinal) < js.IndexOf("Differs from", StringComparison.Ordinal));
        }

        [Fact]
        public void TheRecipeDiffMatchesArrayItemsByContentAndSaysWhatIsDropped()
        {
            string js = Js_("recipe.js");
            Assert.Contains("function matchItems(a, b)", js);
            Assert.Contains("typeof v.kind === 'string' || typeof v.label === 'string'", js);
            Assert.Contains("drops ${n} of the base's ${baseN} item", js);
            Assert.Contains("drops ${it.dropped.length} of the base's ${bn}", js);
            // the description is a row of the heading section, and what the form could not mark is listed under it
            Assert.Contains("heads.map(h => [h, resolved[h]])", js);
            Assert.DoesNotContain("filter(h => h !== 'description')", js);
            Assert.Contains("only in the base", js);
            // a diff row's key keeps its own width, its value wrapping under it
            string html = Page("recipe.html");
            Assert.Matches(@"\.diffline \.k \{[^}]*flex: 0 0 auto", html);
            Assert.Matches(@"\.diffline \.v \{[^}]*flex: 1 1 8rem", html);
        }

        [Fact]
        public void TheExamTableHasShortHeadingsWithTheWholeLabelInTheTitleAndNarrowColumns()
        {
            string run = Js_("run.js");
            // the headings come from the exam resource's own header list (ExamHeaders), set to wrap at word boundaries and never cut; the whole label stays in the title
            Assert.Contains("const wbr = s =>", run);
            // (a greyed column, B1b item 13, adds its reason between the label and the link's own words)
            Assert.Contains("title=\"${esc(H(c).title)}${offCol(c) ? ' (greyed: ' + esc(offText(c)) + ')' : ''} — open the tank screen", run);
            Assert.Contains("<col style=\"width:3.9rem\">", run);
            Assert.Contains("table-layout: fixed", Page("run.html"));
        }

        [Fact]
        public void TheLeaderChartNamesEachGenerationOnceAndNeverOnAnotherLabel()
        {
            string run = Js_("run.js");
            Assert.Contains("a.findIndex(x => x[0] === g) === k", run);
            Assert.Contains("drawn.some(", run);
            Assert.Contains("const every = Math.max(1, Math.ceil(1.15 * R / Math.max(rh, 1)));", run);
        }

        [Fact]
        public void TheBrainsPanelNeverScrollsInsideItselfAndItsTitleIsShort()
        {
            string html = Page("run.html"), run = Js_("run.js");
            Assert.DoesNotContain("class=\"fields\" style=\"overflow:auto\"", run);
            Assert.Contains("<span class=\"kicker\">Brains</span>", html);
            Assert.Matches(@"\.p-brains \.mult \{ overflow: visible;", html);
        }
    }
}
