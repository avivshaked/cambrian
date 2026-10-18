using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// The tank screen (step S6) on a fixture episode. Nothing swims: the nursery is never started, <c>episode.step</c> goes to a fake launcher, and the finished
    /// files are written by <see cref="TankFixture"/> in the nursery's own format.
    /// </summary>
    public class TankFixtureTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose() => _f.Dispose();

        private static readonly string[] EpisodeKeys =
        {
            "format", "program", "request", "build", "recipeHash", "configHash", "episodeConfigHash", "episodeSeed", "control", "members", "seconds", "dt", "sampleRate",
            "stepsPerSample", "frames", "frameFloats", "encoding", "islandRule", "islands", "finalSolverDigest", "channels",
        };

        private TankFixture.Result Make(int b = 0, int d = 0, int t = 0, Func<int, int, string> net = null)
        {
            _f.Ok(_f.Step(b, d, t));
            return TankFixture.Swim(_f, b, d, t, net ?? ((slot, pool) => EpisodeFixture.E(b, pool, d)));
        }

        [Fact]
        public void TheFixtureWrittenThroughTheNurserysOwnTypesReadsBackAsTheRoomParsesIt()
        {
            TankFixture.Result r = Make();
            string folder = _f.Cache(0, 0, 0);
            Assert.Equal(new[] { "episode.json", "events.jsonl", "frames.bin", "pool.tsv", "request.json", "result.json", "step.json" },
                Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray());

            // the room's own reading of the folder: done, and every slot equal to the run's rows
            JsonElement v = _f.Read(0, 0);
            Assert.Equal("done", v.GetProperty("state").GetString());
            Assert.Equal("EXACT", v.GetProperty("verdict").GetString());
            Assert.Equal(6, v.GetProperty("comparison").GetProperty("slots").GetArrayLength());
            Assert.Equal("91c0b7d2e5a3f648", v.GetProperty("provenance").GetProperty("episodeBuild").GetProperty("nurseryHash").GetString());
            Assert.Equal(".NET 8.0.8", v.GetProperty("provenance").GetProperty("episodeBuild").GetProperty("runtime").GetString());
            Assert.Equal(_f.Plan().Draws[0].Seed, v.GetProperty("episodeSeed").GetString());

            // the header: the keys the nursery's writer writes, in its order, and the nursery's own parser takes the request it echoes
            JsonElement epi = Js.Parse(File.ReadAllText(Path.Combine(folder, "episode.json")));
            Assert.Equal(EpisodeKeys, epi.EnumerateObject().Select(p => p.Name).ToArray());
            RestepRequest echoed = RestepRequest.Parse(epi.GetProperty("request").GetRawText());
            Assert.Equal(echoed.ToJson(), epi.GetProperty("request").GetRawText());
            Assert.Equal(_f.Plan().Draws[0].Tanks[0].ToArray(), echoed.Members);
            Assert.Equal(RestepRequest.Parse(File.ReadAllText(Path.Combine(folder, "request.json"))).Members, echoed.Members);
            Assert.Equal(1UL, echoed.Seed);

            Assert.Equal(300, epi.GetProperty("frames").GetInt32());
            Assert.Equal(30.0, epi.GetProperty("seconds").GetDouble());
            Assert.Equal(10.0, epi.GetProperty("sampleRate").GetDouble());
            Assert.Equal(r.FrameFloats, epi.GetProperty("frameFloats").GetInt32());
            JsonElement[] channels = epi.GetProperty("channels").EnumerateArray().ToArray();
            Assert.Equal(r.FrameFloats, channels.Length);
            Assert.Equal("t", channels[0].GetProperty("name").GetString());
            Assert.Equal("s", channels[0].GetProperty("unit").GetString());
            Assert.Equal(channels.Length, channels.Select(c => c.GetProperty("name").GetString()).Distinct().Count());
            Assert.All(channels, c => Assert.False(string.IsNullOrEmpty(c.GetProperty("unit").GetString())));

            // six bodies of two links, a carrion piece or three, and the slot order of the plan
            JsonElement[] members = epi.GetProperty("members").EnumerateArray().ToArray();
            Assert.Equal(6, members.Length);
            Assert.All(members, m => Assert.Equal(2, m.GetProperty("links").GetInt32()));
            Assert.Equal(new[] { 1, 3, 0, 10, 4, 8 }, members.Select(m => m.GetProperty("body").GetInt32()).ToArray());
            Assert.Equal(3, channels.Count(c => Regex.IsMatch(c.GetProperty("name").GetString(), @"^corpse\d+\.joules$")));
            Assert.Equal(24, channels.Count(c => Regex.IsMatch(c.GetProperty("name").GetString(), @"^m\d\.link[01]\.(x|z)$")));   // 6 bodies, 2 links, x and z

            // the islands, from the nursery's own scan of the fixture's snow
            JsonElement isl = epi.GetProperty("islands");
            Assert.True(isl.GetProperty("read").GetBoolean());
            Assert.True(isl.GetProperty("enterAt").GetDouble() > 0);
            Assert.True(isl.GetProperty("pointsInIsland").GetInt32() > 0);
        }

        [Fact]
        public void FramesBinIsFramesTimesTheChannelCountTimesFourBytesAndLittleEndianFloat32()
        {
            TankFixture.Result r = Make();
            string path = Path.Combine(_f.Cache(0, 0, 0), "frames.bin");
            Assert.Equal((long)r.Frames * r.FrameFloats * 4, new FileInfo(path).Length);
            byte[] bytes = File.ReadAllBytes(path);
            // the first value of every frame is the physics clock, 0.1 s apart, from 0.1 s to 30 s
            for (int k = 0; k < r.Frames; k++)
                Assert.Equal((float)((k + 1) / 10.0), BitConverter.ToSingle(bytes, k * r.FrameFloats * 4));
            // a member that is alive reads 1 on its alive channel in the first frame; the one that dies reads 0 from its death on, and NaN beyond it
            string[] names = Js.Parse(File.ReadAllText(Path.Combine(_f.Cache(0, 0, 0), "episode.json"))).GetProperty("channels").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToArray();
            int Col(string name) => Array.IndexOf(names, name);
            float At(int frame, string name) => BitConverter.ToSingle(bytes, (frame * r.FrameFloats + Col(name)) * 4);
            Assert.Equal(1f, At(0, "m5.alive"));
            Assert.Equal(1f, At(223, "m5.alive"));    // t = 22.4 s
            Assert.Equal(0f, At(224, "m5.alive"));    // t = 22.5 s: the economy step that finds it empty
            Assert.True(float.IsNaN(At(224, "m5.reserve")));
            Assert.True(float.IsNaN(At(299, "m5.link0.x")));
            Assert.False(float.IsNaN(At(299, "m0.link0.x")));
            // the carrion the second body takes from is empty at the end, and the third piece is whole
            Assert.Equal(0f, At(299, "corpse1.joules"));
            Assert.Equal(40f, At(299, "corpse2.joules"));
        }

        [Fact]
        public void TheEventsFileHoldsTheRecordersFourTypesOneJsonLineEachInTimeOrder()
        {
            TankFixture.Result r = Make();
            string[] lines = File.ReadAllText(Path.Combine(_f.Cache(0, 0, 0), "events.jsonl")).Split('\n');
            Assert.Equal("", lines.Last());   // a newline after the last line
            JsonElement[] rows = lines.Where(l => l.Length > 0).Select(l => Js.Parse(l)).ToArray();
            Assert.Equal(r.EventLines.Count, rows.Length);
            Assert.Equal(rows.Select(x => x.GetProperty("t").GetDouble()).OrderBy(x => x).ToArray(), rows.Select(x => x.GetProperty("t").GetDouble()).ToArray());
            Assert.Equal(new[] { "birthHeld", "carrionEaten", "death", "islandEntered" }, rows.Select(x => x.GetProperty("type").GetString()).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
            JsonElement[] carrion = rows.Where(x => x.GetProperty("type").GetString() == "carrionEaten").ToArray();
            Assert.Equal(3, carrion.Length);
            Assert.All(carrion, c => Assert.True(c.GetProperty("memberIsInferred").GetBoolean()));
            Assert.Equal(JsonValueKind.Null, carrion[2].GetProperty("member").ValueKind);                      // two candidates: no member is named
            Assert.Equal(new[] { 0, 3 }, carrion[2].GetProperty("candidates").EnumerateArray().Select(x => x.GetInt32()).ToArray());
            JsonElement death = rows.Single(x => x.GetProperty("type").GetString() == "death");
            Assert.Equal(5, death.GetProperty("member").GetInt32());
            Assert.Equal(22.5, death.GetProperty("t").GetDouble());
            Assert.Contains(rows, x => x.GetProperty("type").GetString() == "islandEntered" && x.GetProperty("member").GetInt32() == 0);
        }

        [Fact]
        public void ABodyThatDidNotLandHasNoLinksAndIsTheNullThatMatchesTheRunsNull()
        {
            // brain 1's genome 3 did not land on draw 0, in the run's rows (EpisodeFixture.NullAt)
            TankFixture.Result r = Make(1, 0, 0);
            JsonElement epi = Js.Parse(File.ReadAllText(Path.Combine(_f.Cache(1, 0, 0), "episode.json")));
            JsonElement[] members = epi.GetProperty("members").EnumerateArray().ToArray();
            JsonElement none = members.Single(m => m.GetProperty("pool").GetInt32() == 3);
            Assert.False(none.GetProperty("landed").GetBoolean());
            Assert.Equal(0, none.GetProperty("links").GetInt32());
            Assert.Equal(new[] { 2, 2, 2, 2, 0, 2 }, members.Select(m => m.GetProperty("links").GetInt32()).ToArray());   // pool 3 sits in slot 4 of draw 0's tank
            JsonElement v = _f.Read(1, 0);
            Assert.Equal("EXACT", v.GetProperty("verdict").GetString());
            // the not-landed body's alive channel reads 0 in every frame, and it has no pose to read
            string[] names = epi.GetProperty("channels").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToArray();
            int slot = Array.FindIndex(members, m => m.GetProperty("pool").GetInt32() == 3);
            Assert.Contains($"m{slot}.alive", names);
            Assert.DoesNotContain($"m{slot}.link0.x", names);
            Assert.Equal(r.FrameFloats, names.Length);
        }

        /// <summary>The fixture cannot drift from the real writer: every name, unit and key it writes is a literal in the nursery's own source.</summary>
        [Fact]
        public void EveryChannelNameUnitEventTypeAndHeaderKeyTheFixtureWritesIsInTheNurserysSource()
        {
            string recorder = File.ReadAllText(Path.Combine(Where.Root, "src", "Evosim.Nursery", "FrameRecorder.cs"));
            string restep = File.ReadAllText(Path.Combine(Where.Root, "src", "Evosim.Nursery", "Restep.cs"));
            foreach (string lit in new[]
            {
                "t", "s", "alive", "flag", "reserve", "J", "tissue", "gestation", "ledger.food", "ledger.light", "ledger.work", "ledger.upkeep", "ledger.neural", "ledger.handling", "ledger.exuded",
                "J per economy step", "snow", "J/m3", "x", "y", "z", "qx", "qy", "qz", "qw", "m", "quaternion", "power, -1 to 1", "bend, -1 to 1", "hidden", "unit output",
                "normalised, about -1 to 1", "corpse", "joules", "carrionEaten", "islandEntered", "death", "birthHeld", "memberIsInferred", "candidates", "corpsesEmptied", "funds", "gate", "member",
            })
                Assert.Contains("\"" + lit + "\"", recorder);
            foreach (string lit in new[] { "link", "sense.", ".p", ".dof", "drive", "bend" }) Assert.Contains("\"" + lit + "\"", recorder);

            // the header's keys, in the order the nursery writes them: found in RestepSwimAndWrite's own text, one after the other
            int from = restep.IndexOf("private static int RestepSwimAndWrite", StringComparison.Ordinal);
            int to = restep.IndexOf("private static int RestepSwimUnwatched", StringComparison.Ordinal);
            Assert.True(from > 0 && to > from);
            string method = restep.Substring(from, to - from);
            int at = 0;
            foreach (string key in EpisodeKeys)
            {
                Match m = Regex.Match(method.Substring(at), @"(Field|Raw|BeginObject|BeginArray)\(""" + Regex.Escape(key) + @"""");
                Assert.True(m.Success, $"episode.json's key \"{key}\" is not written, in this order, by RestepSwimAndWrite");
                at += m.Index + m.Length;
            }
            // and the fixture writes the very same list: nothing the real writer does not, and nothing missing
            Assert.Equal(EpisodeKeys, Js.Parse(File.ReadAllText(Path.Combine(Make().Folder, "episode.json"))).EnumerateObject().Select(p => p.Name).ToArray());
        }
    }

    public class TankVerdictTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose() => _f.Dispose();

        private static string FlipLastBit(string net) =>
            BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(double.Parse(net, CultureInfo.InvariantCulture)) ^ 1L).ToString("R", CultureInfo.InvariantCulture);

        [Fact]
        public void TheBadgesThreeStatesAreTheResourcesThreeVerdictsAndEachCarriesItsDataToTheTable()
        {
            // EXACT: every slot equal
            _f.Ok(_f.Step(0, 0));
            TankFixture.Swim(_f, 0, 0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0));
            JsonElement exact = _f.Read(0, 0);
            Assert.Equal("EXACT", exact.GetProperty("verdict").GetString());
            JsonElement[] eq = exact.GetProperty("comparison").GetProperty("slots").EnumerateArray().ToArray();
            Assert.All(eq, s => Assert.True(s.GetProperty("equal").GetBoolean()));
            Assert.All(eq, s => Assert.Equal(s.GetProperty("expected").GetProperty("net").GetString(), s.GetProperty("actual").GetProperty("net").GetString()));
            Assert.Equal(6, exact.GetProperty("comparison").GetProperty("equal").GetInt32());
            Assert.Equal(6, exact.GetProperty("comparison").GetProperty("of").GetInt32());

            // DIFFERS: one bit of one slot, which the table must name and show as two different "R" strings
            _f.Ok(_f.Step(0, 1));
            TankFixture.Swim(_f, 0, 1, 0, (slot, pool) => slot == 2 ? FlipLastBit(EpisodeFixture.E(0, pool, 1)) : EpisodeFixture.E(0, pool, 1));
            JsonElement differs = _f.Read(0, 1);
            Assert.Equal("DIFFERS", differs.GetProperty("verdict").GetString());
            JsonElement c = differs.GetProperty("comparison");
            Assert.Equal(new[] { 2 }, c.GetProperty("differs").EnumerateArray().Select(x => x.GetInt32()).ToArray());
            JsonElement slot2 = c.GetProperty("slots")[2];
            Assert.False(slot2.GetProperty("equal").GetBoolean());
            Assert.NotEqual(slot2.GetProperty("expected").GetProperty("net").GetString(), slot2.GetProperty("actual").GetProperty("net").GetString());
            Assert.Equal(16, slot2.GetProperty("expected").GetProperty("bits").GetString().Length);
            Assert.Equal(5, c.GetProperty("equal").GetInt32());
            Assert.Contains("slot 2", c.GetProperty("reason").GetString());

            // UNCOMPARED: the run's rows are not there, so nothing is equal and nothing is different
            _f.Ok(_f.Step(0, 2));
            TankFixture.Swim(_f, 0, 2, 0, (slot, pool) => EpisodeFixture.E(0, pool, 2));
            File.Delete(Path.Combine(_f.RunFolder, "tank-exam.jsonl"));
            JsonElement un = _f.Read(0, 2);
            Assert.Equal("UNCOMPARED", un.GetProperty("verdict").GetString());
            JsonElement[] slots = un.GetProperty("comparison").GetProperty("slots").EnumerateArray().ToArray();
            Assert.All(slots, s => Assert.Equal(JsonValueKind.Null, s.GetProperty("equal").ValueKind));
            Assert.All(slots, s => Assert.False(s.GetProperty("expected").GetProperty("known").GetBoolean()));
            Assert.Contains("tank-exam.jsonl is not in the run", un.GetProperty("comparison").GetProperty("reason").GetString());

            // the screen: each verdict word becomes its own class, glyph and mark, none of them a colour alone
            string js = File.ReadAllText(Path.Combine(Where.WwwRoot, "js", "tank.js")), html = File.ReadAllText(Path.Combine(Where.WwwRoot, "tank.html"));
            Assert.Contains("view.verdict.toLowerCase()", js);
            Assert.Contains("{ exact: '=', differs: '≠', uncompared: '?' }", js);
            foreach (string cls in new[] { "exact", "differs", "uncompared" }) Assert.Contains(".verdict." + cls + " {", html);
            Assert.Contains("'<b>=</b>equal'", js);
            Assert.Contains("'<b>≠</b>DIFFERS'", js);
            Assert.Contains("'<b>·</b>not compared'", js);
            // a differing row is drawn by shape and text as well as tint: a bar down its edge and a wavy underline under its numbers
            Assert.Contains("table.slots tr.ne td:first-child { box-shadow: inset", html);
            Assert.Contains("text-decoration-style: wavy", html);
            // the table's columns are the brief's: member id, body, expected per[d], re-stepped net, and the mark
            Assert.Contains("<th class=\"l\">member</th><th>body</th><th>expected<br>run's per[${v.draw}]</th><th>re-stepped<br>net</th><th class=\"l\">bits equal</th>", js);
        }

        [Fact]
        public void ADoneEpisodesFilesAreServedOnlyOnceItsResultIsThereAndOnlyTheThreeNamed()
        {
            var resources = new ResourceResolver(_f.Room);
            _f.Ok(_f.Step(0, 0));
            string folder = _f.Cache(0, 0, 0);
            File.WriteAllBytes(Path.Combine(folder, "frames.bin"), new byte[40]);
            // still stepping: no result.json, so no stream is served
            Assert.Throws<ResourceNotFound>(() => EpisodeSteps.FileOf(_f.Room, resources, "1", 0, 0, 0, "frames"));
            TankFixture.Swim(_f, 0, 0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0));
            foreach ((string which, string name, string type) in new[] { ("episode", "episode.json", "application/json; charset=utf-8"), ("events", "events.jsonl", "application/x-ndjson; charset=utf-8"), ("frames", "frames.bin", "application/octet-stream") })
            {
                (string path, string contentType) = EpisodeSteps.FileOf(_f.Room, resources, "1", 0, 0, 0, which);
                Assert.Equal(Path.Combine(folder, name), path);
                Assert.Equal(type, contentType);
                Assert.True(File.Exists(path));
            }
            Assert.Throws<ResourceRefused>(() => EpisodeSteps.FileOf(_f.Room, resources, "1", 0, 0, 0, "result"));
            Assert.Throws<ResourceRefused>(() => EpisodeSteps.FileOf(_f.Room, resources, "1", 0, 0, 0, "../result.json"));
            Assert.Throws<ResourceRefused>(() => EpisodeSteps.FileOf(_f.Room, resources, "1", 0, 0, 0, "request"));
            Assert.Throws<ResourceNotFound>(() => EpisodeSteps.FileOf(_f.Room, resources, "nope", 0, 0, 0, "frames"));
            Assert.Throws<ResourceNotFound>(() => EpisodeSteps.FileOf(_f.Room, resources, "1", 0, 5, 0, "frames"));   // no such episode in the cache
        }
    }

    public class TankStepButtonTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose()
        {
            try { if (File.Exists(_f.TimingFile)) File.SetAttributes(_f.TimingFile, FileAttributes.Normal); } catch (IOException) { }
            _f.Dispose();
        }

        private JsonElement Status(LoadReading load) => Js.Parse(Js.Serialize(Program.RoomStatus(_f.Room, load, DateTime.UtcNow)));

        [Fact]
        public void TheButtonsTwoReasonsAreInTheRoomsStatusAndNothingRefusesOtherwise()
        {
            // nothing in the way: null, and the screen leaves the button enabled
            Assert.Equal(JsonValueKind.Null, Status(LoadReading.Of(10)).GetProperty("stepRefusal").ValueKind);

            // a timing read that is live: the reason names who and why, and the kind is timing
            _f.WriteTiming(EpisodeFixture.TimingJson("evolution-simulator-6e", "a pace read of the tank bench", DateTimeOffset.UtcNow.AddHours(1)));
            JsonElement timing = Status(LoadReading.Of(10)).GetProperty("stepRefusal");
            Assert.Equal("timing", timing.GetProperty("kind").GetString());
            Assert.Contains("evolution-simulator-6e", timing.GetProperty("reason").GetString());
            Assert.Contains("a pace read of the tank bench", timing.GetProperty("reason").GetString());
            Assert.Contains("no episode is stepped", timing.GetProperty("reason").GetString());
            // and it wins over the cap when both would refuse
            Assert.Equal("timing", Status(LoadReading.Of(100)).GetProperty("stepRefusal").GetProperty("kind").GetString());
            File.Delete(_f.TimingFile);

            // the cap: the same text the operation would refuse with
            JsonElement cap = Status(LoadReading.Of(100)).GetProperty("stepRefusal");
            Assert.Equal("cap", cap.GetProperty("kind").GetString());
            Assert.Contains("past the cap of 16 of 32", cap.GetProperty("reason").GetString());
            _f.Fx.Load.Value = LoadReading.Of(100);
            Assert.Contains(cap.GetProperty("reason").GetString(), _f.Step(0, 0).Error);
            _f.Fx.Load.Value = LoadReading.Of(10);

            // a load that cannot be read refuses too, and says so
            JsonElement unread = Status(LoadReading.Failed("no counter")).GetProperty("stepRefusal");
            Assert.Equal("cap", unread.GetProperty("kind").GetString());
            Assert.Contains("cannot be read", unread.GetProperty("reason").GetString());

            // a step that lives counts against the cap, so a nearly full machine refuses the next
            _f.Ok(_f.Step(0, 0));
            Assert.Equal(JsonValueKind.Null, Status(LoadReading.Of(0)).GetProperty("stepRefusal").ValueKind);
        }

        [Fact]
        public void TheScreenDisablesTheButtonFromThatReasonAndSaysItBesideTheButton()
        {
            string html = File.ReadAllText(Path.Combine(Where.WwwRoot, "tank.html")), js = File.ReadAllText(Path.Combine(Where.WwwRoot, "js", "tank.js"));
            // the markup holds the button enabled (the page decides), with the operation it calls named literally
            Match button = Regex.Match(html, @"<button[^>]*id=""step""[^>]*>");
            Assert.True(button.Success);
            Assert.Contains("data-op=\"episode.step\"", button.Value);
            Assert.DoesNotContain(" disabled", button.Value);
            // the page reads what refuses from /api/room's stepRefusal and shows it as the button's title and as a line beside it
            Assert.Contains("lastRoom.stepRefusal.reason", js);
            Assert.Contains("b.disabled = !!off;", js);
            Assert.Contains("b.title = off ||", js);
            Assert.Contains("why.textContent = off ? 'not now: ' + off : '';", js);
            Assert.Contains("id=\"stepwhy\"", html);
            // a step in progress is a third reason, and a plan that cannot be read a fourth
            Assert.Contains("'this episode is being stepped now'", js);
            Assert.Contains("the exam plan cannot be read, so there is nothing to step", js);
        }
    }

    public class TankParityTests
    {
        private static string Html => File.ReadAllText(Path.Combine(Where.WwwRoot, "tank.html"));
        private static string Js_ => File.ReadAllText(Path.Combine(Where.WwwRoot, "js", "tank.js"));

        [Fact]
        public void TheParityScanFindsTheTankScreensOneControlAndItNamesABuiltMutatingOperation()
        {
            // the same scans ScreenParityTests runs over every page, here shown to see this page's control and not to pass over it
            var ops = new OperationList(new RoomContext(Where.Root, Where.Settings(), Where.Root));
            string[] dataOps = Regex.Matches(Html, @"data-op=""([^""]*)""").Select(m => m.Groups[1].Value).ToArray();
            Assert.Equal(new[] { "episode.step" }, dataOps);
            Operation op = ops.Find("episode.step");
            Assert.NotNull(op);
            Assert.True(op.Mutates);
            string[] calls = Regex.Matches(Js_, @"(?<!function\s)\bcallOp\s*\(([^,)]*)").Select(m => m.Groups[1].Value.Trim()).ToArray();
            Assert.Equal(new[] { "'episode.step'" }, calls);
            Assert.Matches(@"bindOps\(\{\s*'episode\.step':", Js_);
            // a mutating control asks for a reason, as every one does
            Assert.Contains("ask({", Js_);
            Assert.Contains("run: reason => callOp('episode.step'", Js_);
            // the page writes no other operation's id, and no operation that is not built
            foreach (Match m in Regex.Matches(Html + Js_, @"\bepisode\.[a-z]+"))
                Assert.NotNull(ops.Find(m.Value));
        }

        [Fact]
        public void WhatWaitsForLaterStepsIsDrawnDisabledWithTheStepNamedAndBindsNoOperation()
        {
            string[] expected = { "S8", "S9", "S10", "S11", "S11", "S12" };
            MatchCollection later = Regex.Matches(Html, @"<button class=""tag"" type=""button"" disabled title=""(S\d+): [^""]*"">([^<]*)</button>");
            Assert.Equal(expected, later.Select(m => m.Groups[1].Value).ToArray());
            Assert.Equal(new[] { "FOLLOW · 3D", "LIVE BRAIN", "SIDE BY SIDE", "PNG", "VIDEO", "EXPORT FOR BLENDER" }, later.Select(m => m.Groups[2].Value).ToArray());
            Assert.DoesNotContain("data-op=\"media", Html);
            // three.js is not vendored in this step, and the page loads nothing but its own script
            Assert.DoesNotMatch(@"<script[^>]*three", Html);
            Assert.DoesNotMatch(@"import[^;]*three", Js_);
            Assert.False(Directory.GetFiles(Where.WwwRoot, "*three*", SearchOption.AllDirectories).Any());
            Assert.Single(Regex.Matches(Html, @"<script"));
        }

        [Fact]
        public void NothingOnThePageNamesAnExperimentsChannelUnitOrMemberCount()
        {
            // the screen finds a channel by the recorder's suffix and takes units, counts and names from the episode's own header
            foreach (string word in new[] { "Scent", "Chemical", "knockout", "brain2", "pilot", "deep-brain", "tankSize", "snow density of", " J/m3" })
                Assert.DoesNotContain(word, Html + Js_);
            Assert.Contains("epi.members", Js_);
            Assert.Contains("epi.channels", Js_);
            Assert.Contains("model.energyUnit", Js_);
        }
    }

    public class TankLinkTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose() => _f.Dispose();

        [Fact]
        public void TheRunScreensExamHeadingsLinkToTheTankScreenByBrainIndexAndTheDrawPickedAndItsDataCarriesTheDrawCount()
        {
            string js = File.ReadAllText(Path.Combine(Where.WwwRoot, "js", "run.js"));
            Assert.Contains("function tankHref(id, brain) { return `tank.html?run=${encodeURIComponent(id)}&brain=${brain}&draw=${tankDraw}`; }", js);
            Assert.Contains("const i = e.brains.indexOf(c); return `<a class=\"glink\" data-tank=\"${i}\" href=\"${tankHref(d.id, i)}\"", js);   // matched by place in the file, never by label
            Assert.Contains("tankLink(e.own)", js);
            Assert.Contains("${tankLink(c)}</th>`).join('')", js);   // a pinned column leads, but every heading is the same link
            Assert.Contains("id=\"tank-draw\"", js);
            Assert.Contains("a.href = tankHref(d.id, a.dataset.tank)", js);

            // the draw picker's range comes from the exam resource: the most draws any row's per holds
            JsonElement run = Js.Parse(Js.Serialize(new ResourceResolver(_f.Room).Read("nursery://run/1")));
            Assert.Equal(32, run.GetProperty("exam").GetProperty("draws").GetInt32());
            Assert.Equal(18, run.GetProperty("exam").GetProperty("brains").GetArrayLength());
        }

        [Fact]
        public void TheGenomeScreensSwimItInTheTankIsALinkEnabledForAnExamGenomeAndTheTankScreenPicksTheTankThatHoldsIt()
        {
            string html = File.ReadAllText(Path.Combine(Where.WwwRoot, "genome.html")), js = File.ReadAllText(Path.Combine(Where.WwwRoot, "js", "genome.js"));
            Match swim = Regex.Match(html, @"<a [^>]*id=""swim""[^>]*>SWIM IT IN THE TANK</a>");
            Assert.True(swim.Success);
            Assert.DoesNotContain("<button class=\"btn btn--off\" type=\"button\" disabled title=\"phase 3: the tank screen is not built yet", html);
            Assert.Contains("a.href = `tank.html?run=${encodeURIComponent(runId)}&genome=${d.examIndex}`;", js);
            Assert.Contains("!d.kinds.includes('exam') || d.examIndex == null", js);
            Assert.Contains("setSwim(d);", js);

            // an exam genome's number is its line in the genomes file, which is what the plan's tanks hold: the tank of draw 0 that holds pool index 3 is the one the screen picks
            Assert.Contains(3, _f.Plan().Draws[0].Tanks[0]);
            string tankJs = File.ReadAllText(Path.Combine(Where.WwwRoot, "js", "tank.js"));
            Assert.Contains("d.tanks.findIndex(m => m.includes(wantGenome))", tankJs);
        }
    }

    /// <summary>The smoke server for screenshots: the fixture episodes on port 5180 (never the room's 5179), a fake launcher so that nothing can swim, and a stop file that ends it.</summary>
    public class TankSmokeServer
    {
        [SmokeFact]
        public void ServesTheFixtureEpisodesOnPort5180UntilTheStopFileAppears()
        {
            string stop = Environment.GetEnvironmentVariable(SmokeFactAttribute.StopVariable);
            using var f = new EpisodeFixture();

            // the registry entry of the fixture's run, with builds that look like builds (the episodes' own differ from them in two trees)
            RegistryEntry entry = RegistryEntry.ReadAll(f.Fx.Registry).Single();
            entry.GitCommit = "9e2d7a40b18c5f63a07d1e9b42c8f5036a1d7be4"; entry.CoreHash = "9f1c02aa3b7d4e65"; entry.DynamicsHash = "77be31c09a2d58f4"; entry.FarmHash = "c4e8a1d2f0936b57"; entry.NurseryHash = "3be4410e8d27a9c1";
            File.WriteAllText(f.Fx.Registry, entry.ToJson() + "\n", new UTF8Encoding(false));

            f.Ok(f.Step(0, 0)); TankFixture.Swim(f, 0, 0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0), new TankFixture.Build { CoreHash = "9f1c02aa3b7d4e65", DynamicsHash = "77be31c09a2d58f4", FarmHash = "c4e8a1d2f0936b57" });
            f.Ok(f.Step(0, 1)); TankFixture.Swim(f, 0, 1, 0, (slot, pool) => slot == 2 ? BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(double.Parse(EpisodeFixture.E(0, pool, 1), CultureInfo.InvariantCulture)) ^ 1L).ToString("R", CultureInfo.InvariantCulture) : EpisodeFixture.E(0, pool, 1));
            f.Ok(f.Step(1, 0)); TankFixture.Swim(f, 1, 0, 0, (slot, pool) => EpisodeFixture.E(1, pool, 0));
            f.Ok(f.Step(0, 2));   // a step that is still going: no result yet, its process alive in the fake list

            // a settings copy of the smoke's own, not under rounds/: the fixture's folders, a port of its own, and a nursery that is a file of text (the launcher is a fake)
            string settingsFile = Path.Combine(f.Fx.Dir, "room-smoke.json");
            File.WriteAllText(settingsFile, Where.SettingsJson(runsDir: "runs").Replace("\"port\":5179", "\"port\":5180").Replace("artifacts/Evosim.Nursery/bin/Debug/net8.0/Evosim.Nursery.dll", "bin/nursery.dll"));
            RoomSettings settings = RoomSettings.Load(settingsFile);
            Assert.Equal(5180, settings.Port);
            var room = new RoomContext(f.Fx.Root, settings, f.Fx.Main, f.Fx.Registry, Path.Combine(f.Fx.Dir, "activity-smoke.jsonl"), f.Launcher, f.Fx.Load, 32, _ => f.Fx.Build, null, f.Procs, f.Copier);
            WebApplication app = Program.Create(room, Where.WwwRoot);
            app.Start();
            using var stopped = new ManualResetEventSlim(File.Exists(stop));
            using var watcher = new FileSystemWatcher(Path.GetDirectoryName(stop), Path.GetFileName(stop)) { EnableRaisingEvents = true };
            watcher.Created += (s, e) => stopped.Set();
            File.WriteAllText(stop + ".ready", "5180");
            stopped.Wait(TimeSpan.FromMinutes(40));
            app.StopAsync().GetAwaiter().GetResult();
        }
    }
}
