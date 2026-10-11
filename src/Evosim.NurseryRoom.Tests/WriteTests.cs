using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    internal sealed class FakeLauncher : IRunLauncher
    {
        public readonly List<LaunchSpec> Started = new List<LaunchSpec>();
        public Exception Throw;

        /// <summary>What the next start reports; the default is a run that is detached from the room.</summary>
        public Func<LaunchOutcome> Outcome = () => new LaunchOutcome { Pid = 4242 };

        /// <summary>When set, the launcher reports this mask as applied whatever it was asked (0: none), as a launcher that pinned otherwise would.</summary>
        public ulong? Applied;

        public LaunchOutcome Start(LaunchSpec spec)
        {
            if (Throw != null) throw Throw;
            Started.Add(spec);
            LaunchOutcome o = Outcome();
            o.Affinity = Applied ?? spec.Affinity;   // the real launcher reports the mask it set on the process
            return o;
        }
    }

    /// <summary>
    /// The machine's queue as a test gives it: text that is parsed as the room parses the file. By default its one Running line is the room's slot,
    /// logical 16-31, which inside the fixtures' bound 0xFFFF0000 leaves all sixteen free.
    /// </summary>
    internal sealed class FixedQueue : IQueueReader
    {
        public const string File = "scratch/machine/queue.md";
        public const string RoomLine = "- the room (a test) · on the coordinator's go · cores: 16-31 · room";

        public string Text = With();
        public QueueReading Read() => QueueReading.Parse(Text, File);

        /// <summary>A queue whose Running section is the room's line and these other job lines (each without its "- ").</summary>
        public static string With(params string[] others) =>
            "# The machine's queue\n\n## Running\n\n" + RoomLine + "\n" + string.Concat(others.Select(o => "- " + o + "\n")) + "\n## Next, in order\n\n- later · cores: 16-31\n";
    }

    /// <summary>The heat check as a test gives it: OK unless a test says otherwise. The script is never run.</summary>
    internal sealed class FixedHeat : IHeatReader
    {
        public HeatReading Value = HeatReading.Of(HeatReading.Ok, "2026-10-11 12:00:00  OK  gpu 50C (a test's reading)");
        public HeatReading Read() => Value;
    }

    /// <summary>
    /// A room on synthetic files: a repository root of its own in a fresh folder of the build output (an <c>src/Evosim.Core</c> marker, and
    /// <c>rounds/nursery/</c> holding copies of brain2-w5.json, its configs and appended.txt), so that nothing a test writes lands in the source
    /// tree; its own registry, activity log and run folders beside it; a fake launcher so that nothing swims; and a fixed queue and heat verdict.
    /// </summary>
    internal sealed class Fx : IDisposable
    {
        public readonly string Dir = Where.Fresh("writes");

        /// <summary>The synthetic repository root the room runs in: the main checkout, or a worktree of it under <c>scratch/wt</c> (<see cref="InWorktree"/>).</summary>
        public readonly string Root;

        /// <summary>The main checkout: <see cref="Root"/> itself, unless the room runs in a worktree.</summary>
        public readonly string Main;

        public readonly FakeLauncher Launcher = new FakeLauncher();
        public readonly FixedQueue Queue = new FixedQueue();
        public readonly FixedHeat Heat = new FixedHeat();
        public readonly RoomContext Room;
        public readonly OperationList Ops;
        public RunRecord Build = new RunRecord { CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", GitCommit = "abc1234" };

        public string Rel(string full) => Path.GetRelativePath(Root, full).Replace('\\', '/');
        public string RunsDir => Path.Combine(Root, "runs");
        public string Registry => Path.Combine(Dir, "registry.jsonl");
        public string Brain2 => File.ReadAllText(Path.Combine(Root, "rounds", "nursery", "brain2-w5.json"));

        public Fx(params string[] roomRuns) : this(false, "dotnet-host", roomRuns) { }

        /// <summary>A room whose root is a worktree under the main checkout's scratch/, the registry and the activity log being the main checkout's.</summary>
        public static Fx InWorktree(params string[] roomRuns) => new Fx(true, "dotnet-host", roomRuns);

        /// <summary>A room whose room.json names this dotnet host.</summary>
        public static Fx WithDotnet(string dotnet) => new Fx(false, dotnet, new string[0]);

        /// <summary>A room whose room.json holds this affinity.</summary>
        public static Fx WithAffinity(string affinity) => new Fx(false, "dotnet-host", new string[0], affinity);

        /// <summary>A room on a machine whose processors are these (a fake system mask), with this affinity in room.json.</summary>
        public static Fx WithSystemMask(ulong systemMask, string affinity = "0xFFFF0000") => new Fx(false, "dotnet-host", new string[0], affinity, systemMask);

        private Fx(bool worktree, string dotnet, string[] roomRuns, string affinity = "0xFFFF0000", ulong? systemMask = null)
        {
            Main = worktree ? Path.Combine(Dir, "main") : Path.Combine(Dir, "repo");
            Root = worktree ? Path.Combine(Main, "scratch", "wt") : Main;
            foreach (string r in new[] { Main, Root }.Distinct())
                Directory.CreateDirectory(Path.Combine(r, "src", "Evosim.Core"));
            string nursery = Path.Combine(Root, "rounds", "nursery"), real = Path.Combine(Where.Root, "rounds", "nursery");
            Directory.CreateDirectory(Path.Combine(nursery, "configs"));
            File.Copy(Path.Combine(real, "brain2-w5.json"), Path.Combine(nursery, "brain2-w5.json"));
            File.Copy(Path.Combine(real, "appended.txt"), Path.Combine(nursery, "appended.txt"));
            foreach (string c in Directory.GetFiles(Path.Combine(real, "configs"), "*.json"))
                File.Copy(c, Path.Combine(nursery, "configs", Path.GetFileName(c)));
            Directory.CreateDirectory(Path.Combine(Root, "bin"));
            File.WriteAllText(Path.Combine(Root, "bin", "nursery.dll"), "not a program; the launcher is a fake");
            string runs = string.Join(",", roomRuns.Select(r => "\"" + r + "\""));
            RoomSettings settings = RoomSettings.Parse(
                "{\"port\":5179,\"bind\":\"127.0.0.1\",\"pollSeconds\":30,\"runs\":[" + runs + "],\"dotnet\":\"" + dotnet + "\"," +
                "\"nurseryDll\":\"bin/nursery.dll\",\"runsDir\":\"runs\",\"activityFile\":\"rounds/nursery/activity.jsonl\",\"affinity\":\"" + affinity + "\",\"trialsDir\":\"scratch/nursery-trials\"," +
                "\"queueFile\":\"scratch/machine/queue.md\",\"heatScript\":\"scripts/no-heat-check-in-the-room-tests.ps1\"}");
            Room = new RoomContext(Root, settings, Main, Registry, Path.Combine(Dir, "activity.jsonl"), Launcher, Queue, Heat, systemMask.HasValue ? (int?)null : 32, _ => Build, systemMask);
            Ops = new OperationList(Room);
        }

        /// <summary>A name for a recipe file under the synthetic root's rounds/nursery.</summary>
        public string RecipePath() => "rounds/nursery/zz-test-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json";

        public string Full(string rel) => Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>The temporary files a recipe write leaves in rounds/nursery (a write's own name is unique, so a test lists the folder).</summary>
        public string[] WritingFiles() => Directory.GetFiles(Full("rounds/nursery"), "*.writing");

        public OperationResult Call(string id, object args, string who = OperationList.Screen) =>
            Ops.Invoke(id, Js.Parse(args as string ?? Js.Serialize(args)), who);

        public static JsonElement Result(OperationResult r)
        {
            Assert.True(r.Ok, r.Error);
            return Js.Parse(Js.Serialize(r.Result));
        }

        public List<JsonElement> Rows() => Room.Activity.ReadAll();

        public JsonElement LastRow()
        {
            List<JsonElement> rows = Rows();
            Assert.NotEmpty(rows);
            return rows[rows.Count - 1];
        }

        public void Dispose() => Directory.Delete(Dir, true);

        public string Json(string path, string reason = "a test", string text = null, string expect = null) =>
            Js.Serialize(new Dictionary<string, object> { ["path"] = path, ["text"] = text ?? Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"zz-written\""), ["expectHash"] = expect, ["reason"] = reason }
                .Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    public class RecipeWriteTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        [Fact]
        public void ANewRecipeIsCheckedWrittenAndRecordedWithItsTextAsAHash()
        {
            string path = _fx.RecipePath();
            string text = _fx.Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"zz-written\"");
            JsonElement r = Fx.Result(_fx.Call("recipe.write", _fx.Json(path, "a trial of the writer", text)));
            Assert.True(r.GetProperty("created").GetBoolean());
            Assert.Equal("zz-written", r.GetProperty("name").GetString());
            Assert.Equal(text, File.ReadAllText(Path.Combine(_fx.Root, path)));
            Assert.Equal(RunRecord.FileSha256(Path.Combine(_fx.Root, path)), r.GetProperty("fileSha256").GetString());
            Assert.Empty(_fx.WritingFiles());

            JsonElement row = _fx.LastRow();
            Assert.Equal("recipe.write", row.GetProperty("operation").GetString());
            Assert.Equal("ok", row.GetProperty("outcome").GetString());
            Assert.Equal("screen", row.GetProperty("who").GetString());
            Assert.Equal("a trial of the writer", row.GetProperty("reason").GetString());
            JsonElement args = row.GetProperty("args");
            Assert.Equal(path, args.GetProperty("path").GetString());
            Assert.False(args.TryGetProperty("text", out _));
            Assert.Equal(64, args.GetProperty("textSha256").GetString().Length);
            Assert.Equal(text.Length, args.GetProperty("textChars").GetInt32());
            Assert.DoesNotContain("zz-written", File.ReadAllText(_fx.Room.Activity.Path));
            Assert.True(DateTime.TryParse(row.GetProperty("timeUtc").GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t));
            Assert.Equal(DateTimeKind.Utc, t.Kind);
        }

        [Fact]
        public void ReplacingAFileNeedsItsCurrentHashAndTwoWritersCannotOverwriteEachOther()
        {
            string path = _fx.RecipePath();
            string full = Path.Combine(_fx.Root, path);
            Fx.Result(_fx.Call("recipe.write", _fx.Json(path)));
            string first = RunRecord.FileSha256(full);
            string variant = _fx.Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"zz-second\"");

            // no hash: refused, and the refusal says what the hash is
            OperationResult noHash = _fx.Call("recipe.write", _fx.Json(path, text: variant));
            Assert.False(noHash.Ok);
            Assert.Equal(400, noHash.Status);
            Assert.Contains(first, noHash.Error);
            Assert.Contains("expectHash is required", noHash.Error);

            // a stale hash: refused
            OperationResult stale = _fx.Call("recipe.write", _fx.Json(path, text: variant, expect: new string('0', 64)));
            Assert.False(stale.Ok);
            Assert.Contains("Someone changed it", stale.Error);
            Assert.Equal(first, RunRecord.FileSha256(full));

            // the right one (in capitals too): written, and the old hash is returned
            JsonElement ok = Fx.Result(_fx.Call("recipe.write", _fx.Json(path, text: variant, expect: first.ToUpperInvariant())));
            Assert.False(ok.GetProperty("created").GetBoolean());
            Assert.Equal(first, ok.GetProperty("previousSha256").GetString());
            Assert.Equal(variant, File.ReadAllText(full));

            // the second writer, still holding the first hash, is refused
            OperationResult late = _fx.Call("recipe.write", _fx.Json(path, expect: first));
            Assert.False(late.Ok);
            Assert.Equal(variant, File.ReadAllText(full));
        }

        [Fact]
        public void AHashForAFileThatIsNotThereIsRefused()
        {
            OperationResult r = _fx.Call("recipe.write", _fx.Json(_fx.RecipePath(), expect: new string('a', 64)));
            Assert.False(r.Ok);
            Assert.Contains("does not exist", r.Error);
        }

        [Fact]
        public void ARecipeTheNurseryRefusesIsNotWrittenAndAnExistingFileIsLeftAsItWas()
        {
            string bad = _fx.Brain2.Replace("\"world\": {", "\"world\": {\n    \"extra\": 1,");
            string path = _fx.RecipePath();
            OperationResult r = _fx.Call("recipe.write", _fx.Json(path, text: bad));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("nursery refuses", r.Error);
            Assert.Contains("extra", r.Error);
            Assert.DoesNotContain(_fx.Root, r.Error);
            Assert.False(File.Exists(Path.Combine(_fx.Root, path)));
            Assert.Empty(_fx.WritingFiles());
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
            Assert.Contains("extra", _fx.LastRow().GetProperty("detail").GetString());

            Fx.Result(_fx.Call("recipe.write", _fx.Json(path)));
            string good = File.ReadAllText(Path.Combine(_fx.Root, path));
            OperationResult again = _fx.Call("recipe.write", _fx.Json(path, text: bad, expect: RunRecord.FileSha256(Path.Combine(_fx.Root, path))));
            Assert.False(again.Ok);
            Assert.Equal(good, File.ReadAllText(Path.Combine(_fx.Root, path)));
        }

        [Fact]
        public void ARecipeThatNamesItselfAsItsBaseIsRefusedAndTheFileRestored()
        {
            string path = _fx.RecipePath();
            Fx.Result(_fx.Call("recipe.write", _fx.Json(path)));
            string full = Path.Combine(_fx.Root, path);
            string before = File.ReadAllText(full);
            string selfBase = "{\"base\":\"" + path + "\",\"name\":\"zz-self\"}";
            OperationResult r = _fx.Call("recipe.write", _fx.Json(path, text: selfBase, expect: RunRecord.FileSha256(full)));
            Assert.False(r.Ok);
            Assert.Equal(before, File.ReadAllText(full));
            Assert.Empty(_fx.WritingFiles());
        }

        [Theory]
        [InlineData("rounds/nursery/room.json")]
        [InlineData("rounds/nursery/registry.jsonl")]
        [InlineData("rounds/nursery/sub/x.json")]
        [InlineData("rounds/nursery/x.txt")]
        [InlineData("rounds/other.json")]
        [InlineData("src/Evosim.Core/x.json")]
        [InlineData("../outside.json")]
        [InlineData("rounds/nursery/../../escape.json")]
        [InlineData("C:/Windows/x.json")]
        [InlineData("/etc/x.json")]
        [InlineData("rounds/nursery/con.json")]            // a device name with any extension
        [InlineData("rounds/nursery/CON.json")]
        [InlineData("rounds/nursery/nul.x.json")]
        [InlineData("rounds/nursery/lpt1.json")]
        [InlineData("rounds/nursery/com9.json")]
        [InlineData("rounds/nursery/a b.json")]            // only A-Z a-z 0-9 . _ - in a name
        [InlineData("rounds/nursery/a&b.json")]
        [InlineData("rounds/nursery/a%b.json")]
        [InlineData("rounds/nursery/a^b.json")]
        [InlineData("rounds/nursery/a.json ")]             // a trailing space is refused where it stands, not normalised away
        [InlineData("rounds/nursery/a.json\n")]           // and so is a trailing line break
        [InlineData("rounds/nursery/.hidden.json")]
        [InlineData("rounds/nursery/a.json.")]             // Windows drops a trailing dot: this would name a.json
        [InlineData("rounds/nursery/a.")]
        public void OnlyAJsonFileDirectlyUnderRoundsNurseryIsWrittenAndNeverTheSettings(string path)
        {
            OperationResult r = _fx.Call("recipe.write", _fx.Json(path));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
            Assert.False(File.Exists(Path.Combine(_fx.Root, "rounds", "other.json")));
            Assert.False(Directory.Exists(Path.Combine(_fx.Root, "rounds", "nursery", "sub")));
        }

        [Fact]
        public void TheRoomSettingsFileCannotBeOverwritten()
        {
            string room = Path.Combine(_fx.Root, "rounds", "nursery", "room.json");
            // The synthetic root holds no settings file; the refusal goes by the name alone.
            if (!File.Exists(room)) File.WriteAllText(room, "{}\n");
            string before = RunRecord.FileSha256(room);
            Assert.False(_fx.Call("recipe.write", _fx.Json("rounds/nursery/room.json", expect: before)).Ok);
            Assert.Equal(before, RunRecord.FileSha256(room));
        }

        [Fact]
        public void EmptyTextIsRefused()
        {
            Assert.False(_fx.Call("recipe.write", _fx.Json(_fx.RecipePath(), text: "  ")).Ok);
        }

        [Fact]
        public void ADiffOfTwoRecipesIsFieldByFieldAndNeedsABaseOrAnOther()
        {
            string path = _fx.RecipePath();
            string variant = _fx.Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"zz-diff\"").Replace("\"seed\": \"0x1\"", "\"seed\": \"0x2\"");
            Assert.NotEqual(_fx.Brain2, variant);
            Fx.Result(_fx.Call("recipe.write", _fx.Json(path, text: variant)));

            JsonElement d = Fx.Result(_fx.Call("recipe.diff", "{\"path\":\"" + path + "\",\"other\":\"rounds/nursery/brain2-w5.json\"}"));
            Assert.False(d.GetProperty("identical").GetBoolean());
            Assert.False(d.GetProperty("b").GetProperty("isBase").GetBoolean());
            string[] paths = d.GetProperty("differs").EnumerateArray().Select(x => x.GetProperty("path").GetString()).ToArray();
            Assert.Contains("name", paths);
            Assert.Contains("seed", paths);
            Assert.Equal(paths.OrderBy(p => p, StringComparer.Ordinal).ToArray(), paths);
            JsonElement seed = d.GetProperty("differs").EnumerateArray().First(x => x.GetProperty("path").GetString() == "seed");
            Assert.NotEqual(seed.GetProperty("a").ToString(), seed.GetProperty("b").ToString());

            JsonElement same = Fx.Result(_fx.Call("recipe.diff", "{\"path\":\"rounds/nursery/brain2-w5.json\",\"other\":\"rounds/nursery/brain2-w5.json\"}"));
            Assert.True(same.GetProperty("identical").GetBoolean());
            Assert.Empty(same.GetProperty("differs").EnumerateArray());

            // brain2 names no base: with no other there is nothing to compare
            OperationResult nobase = _fx.Call("recipe.diff", "{\"path\":\"rounds/nursery/brain2-w5.json\"}");
            Assert.False(nobase.Ok);
            Assert.Contains("no base", nobase.Error);
            // a diff changes nothing and so writes no row
            Assert.Single(_fx.Rows());
        }

        [Fact]
        public void ADiffAgainstTheRecipesOwnBaseMarksIt()
        {
            string basePath = _fx.RecipePath(), childPath = _fx.RecipePath();
            Fx.Result(_fx.Call("recipe.write", _fx.Json(basePath)));
            string child = "{\"base\":\"" + basePath + "\",\"name\":\"zz-child\",\"seed\":\"0x9\"}";
            Fx.Result(_fx.Call("recipe.write", _fx.Json(childPath, text: child)));
            JsonElement d = Fx.Result(_fx.Call("recipe.diff", "{\"path\":\"" + childPath + "\"}"));
            Assert.True(d.GetProperty("b").GetProperty("isBase").GetBoolean());
            Assert.Equal(basePath, d.GetProperty("b").GetProperty("path").GetString());
            Assert.Equal(new[] { "name", "seed" }, d.GetProperty("differs").EnumerateArray().Select(x => x.GetProperty("path").GetString()).ToArray());
        }

        [Fact]
        public void TheRecipeResourceCarriesItsTextAndHashAndNoDiffOfItsOwn()
        {
            JsonElement r = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Read("nursery://recipe/rounds/nursery/brain2-w5.json")));
            Assert.Equal(_fx.Brain2, r.GetProperty("text").GetString());
            Assert.Equal(RunRecord.FileSha256(Path.Combine(_fx.Root, "rounds", "nursery", "brain2-w5.json")), r.GetProperty("fileSha256").GetString());
            Assert.False(r.TryGetProperty("differs", out _));
        }
    }

    public class ActivityTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        [Fact]
        public void ARowIsOneLineAndAnEmbeddedLineBreakIsRefused()
        {
            var log = new ActivityLog(Path.Combine(_fx.Dir, "own.jsonl"));
            log.AppendLine("{\"a\":1}");
            Assert.Throws<ArgumentException>(() => log.AppendLine("{\"a\":\n1}"));
            Assert.Throws<ArgumentException>(() => log.AppendLine("{\"a\":\r1}"));
            log.Append(new { reason = "has a \n break in a value", n = 2 });   // escaped by the serialiser: still one line
            string[] lines = File.ReadAllLines(log.Path);
            Assert.Equal(2, lines.Length);
            Assert.Contains("\\n", lines[1]);
        }

        [Fact]
        public void TheLogIsAppendedWhileAnotherReaderHoldsItOpen()
        {
            var log = new ActivityLog(Path.Combine(_fx.Dir, "shared.jsonl"));
            log.Append(new { n = 1 });
            using (var held = new FileStream(log.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                log.Append(new { n = 2 });
                log.Append(new { n = 3 });
            }
            List<JsonElement> rows = log.ReadNewestFirst(10, out int total, out int unreadable);
            Assert.Equal(new[] { 3, 2, 1 }, rows.Select(r => r.GetProperty("n").GetInt32()).ToArray());
            Assert.Equal(3, total);
            Assert.Equal(0, unreadable);
            Assert.Equal(2, log.ReadNewestFirst(2, out _, out _).Count);
        }

        [Fact]
        public void AnUnreadableLineIsCountedAndTheRestAreRead()
        {
            string p = Path.Combine(_fx.Dir, "mixed.jsonl");
            File.WriteAllText(p, "{\"n\":1}\nnot json\n{\"n\":2}\n[1]\n");
            var log = new ActivityLog(p);
            Assert.Equal(2, log.ReadNewestFirst(10, out int total, out int bad).Count);
            Assert.Equal(2, total);
            Assert.Equal(2, bad);
        }

        [Fact]
        public void EveryOperationThatChangesSomethingAppendsOneRowAndTheFeedReadsNewestFirst()
        {
            _fx.Call("recipe.write", "{\"path\":\"rounds/nursery/zz-no.json\",\"text\":\"{}\",\"reason\":\"first\"}");
            _fx.Call("recipe.write", "{\"path\":\"rounds/nursery/zz-no.json\",\"text\":\"{}\",\"reason\":\"second\"}", OperationList.Mcp);
            Assert.Equal(2, _fx.Rows().Count);
            JsonElement feed = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Read("nursery://activity")));
            JsonElement[] entries = feed.GetProperty("entries").EnumerateArray().ToArray();
            Assert.Equal(new[] { "second", "first" }, entries.Select(e => e.GetProperty("reason").GetString()).ToArray());
            Assert.Equal(new[] { "mcp", "screen" }, entries.Select(e => e.GetProperty("who").GetString()).ToArray());
            Assert.Equal(2, feed.GetProperty("total").GetInt32());
            // a reading operation leaves no row
            _fx.Call("recipe.check", "{\"path\":\"rounds/nursery/brain2-w5.json\"}");
            Assert.Equal(2, _fx.Rows().Count);
        }

        [Fact]
        public void ARefusalForAMissingReasonIsRecordedToo()
        {
            OperationResult r = _fx.Call("recipe.write", "{\"path\":\"rounds/nursery/zz-no.json\",\"text\":\"{}\"}");
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("reason", r.Error);
            JsonElement row = _fx.LastRow();
            Assert.Equal("refused", row.GetProperty("outcome").GetString());
            Assert.Equal(JsonValueKind.Null, row.GetProperty("reason").ValueKind);
            Assert.Contains("reason", row.GetProperty("detail").GetString());
        }

        [Theory]
        [InlineData("two\nlines")]
        [InlineData("a\rb")]
        [InlineData("tab\there")]
        [InlineData("bell\u0007")]
        public void AReasonWithALineBreakOrAControlCharacterIsRefusedAndRecordedCleaned(string reason)
        {
            OperationResult r = _fx.Call("recipe.write", Js.Serialize(new { path = "rounds/nursery/zz-no.json", text = "{}", reason }));
            Assert.False(r.Ok);
            Assert.Contains("control character", r.Error);
            string line = File.ReadAllLines(_fx.Room.Activity.Path).Last();
            Assert.False(line.Any(char.IsControl), "the row is text without a control character: " + line);
            JsonElement row = _fx.LastRow();
            Assert.Equal("refused", row.GetProperty("outcome").GetString());
            Assert.All(row.GetProperty("reason").GetString(), c => Assert.False(char.IsControl(c)));
        }

        [Fact]
        public void AReasonOverFiveHundredCharactersIsRefusedAndTheRowHoldsAtMostThat()
        {
            string reason = new string('x', 501);
            OperationResult r = _fx.Call("recipe.write", Js.Serialize(new { path = "rounds/nursery/zz-no.json", text = "{}", reason }));
            Assert.False(r.Ok);
            Assert.Contains("501", r.Error);
            Assert.True(_fx.LastRow().GetProperty("reason").GetString().Length <= ActivityLog.MaxReason + 1);
            Assert.True(_fx.Call("recipe.write", Js.Serialize(new { path = "rounds/nursery/zz-no.json", text = "{}", reason = new string('x', 500) })).Error?.Contains("501") != true);
        }

        [Fact]
        public void ARowHoldsNoPathOutsideTheRepositoryAndNoAbsolutePathAtAll()
        {
            _fx.Call("recipe.write", "{\"path\":\"C:/Users/someone/secret.json\",\"text\":\"{}\",\"reason\":\"r\"}");
            _fx.Call("recipe.write", "{\"path\":\"../up.json\",\"text\":\"{}\",\"reason\":\"r\"}");
            _fx.Call("recipe.write", Js.Serialize(new { path = "rounds/nursery/zz-no.json", text = _fx.Brain2.Replace("\"world\": {", "\"world\": {\"extra\": 1,"), reason = "r" }));
            string all = File.ReadAllText(_fx.Room.Activity.Path);
            Assert.False(all.Contains("someone", StringComparison.Ordinal));
            Assert.False(all.Contains(_fx.Root, StringComparison.OrdinalIgnoreCase));
            Assert.False(all.Contains(_fx.Root.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
            Assert.Contains("(a path outside the repository)", all);
            Assert.False(all.Contains("C:", StringComparison.Ordinal));
        }

        [Fact]
        public void AnActivityLogThatCannotBeWrittenIsSaidInTheResultAndDoesNotUndoTheOperation()
        {
            // the log's own path is a folder: the append fails
            string blocked = Path.Combine(_fx.Dir, "blocked.jsonl");
            Directory.CreateDirectory(blocked);
            var room = new RoomContext(_fx.Root, Where.Settings(), _fx.Root, _fx.Registry, blocked, _fx.Launcher, _fx.Queue, _fx.Heat, 32);
            var ops = new OperationList(room);
            string path = _fx.RecipePath();
            OperationResult r = ops.Invoke("recipe.write", Js.Parse(_fx.Json(path)));
            Assert.True(r.Ok);
            Assert.Contains("activity row could not be written", r.Warning);
            Assert.True(File.Exists(Path.Combine(_fx.Root, path)));
        }

        [Fact]
        public void TheMcpCallsAreRecordedAsMcp()
        {
            var args = new Dictionary<string, JsonElement>
            {
                ["action"] = Js.Parse("\"write\""), ["path"] = Js.Parse("\"rounds/nursery/zz-no.json\""), ["text"] = Js.Parse("\"{}\""), ["reason"] = Js.Parse("\"through the tool\""),
            };
            var result = McpSurface.Call(_fx.Ops, "recipe", args);
            Assert.True(result.IsError == true);   // the nursery refuses "{}"; the call still counts
            JsonElement row = _fx.LastRow();
            Assert.Equal("mcp", row.GetProperty("who").GetString());
            Assert.Equal("through the tool", row.GetProperty("reason").GetString());
            Assert.Equal("recipe.write", row.GetProperty("operation").GetString());
        }
    }

    public class LaunchTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private static string Launch(string name, int threads = 4, string recipe = "rounds/nursery/brain2-w5.json", string reason = "a test") =>
            Js.Serialize(new { recipe, name, threads, reason });

        [Fact]
        public void ALaunchRunsTheNurseryOnTheRecipeInANewFolderAndShowsInTheRegistryAtOnce()
        {
            JsonElement r = Fx.Result(_fx.Call("run.launch", Launch("first-run", 4)));
            Assert.Single(_fx.Launcher.Started);
            LaunchSpec spec = _fx.Launcher.Started[0];
            Assert.Equal("dotnet-host", spec.FileName);
            Assert.Equal(_fx.Root, spec.WorkingDirectory);
            string folderRel = _fx.Rel(Path.Combine(_fx.RunsDir, "first-run"));
            Assert.Equal(Path.Combine(_fx.RunsDir, "first-run"), spec.Folder);
            Assert.True(Directory.Exists(spec.Folder));
            Assert.Empty(Directory.GetFileSystemEntries(spec.Folder));   // the nursery refuses a folder that holds anything
            Assert.Equal(spec.Folder + ".launch.log", spec.LogFile);
            Assert.Equal(Path.Combine(_fx.Root, "bin", "nursery.dll"), spec.Arguments[0]);
            Assert.Equal(new[] { "--recipe", "rounds/nursery/brain2-w5.json", "--out", folderRel, "--threads", "4" }, spec.Arguments.Skip(1).ToArray());
            Assert.Equal(4242, r.GetProperty("pid").GetInt32());
            Assert.True(r.GetProperty("detached").GetBoolean());
            Assert.Equal(JsonValueKind.Null, r.GetProperty("launcherNote").ValueKind);
            Assert.Equal(".", r.GetProperty("tree").GetString());
            Assert.Equal("first-run", r.GetProperty("id").GetString());
            Assert.Equal(folderRel, r.GetProperty("folder").GetString());
            // the run is pinned to room.json's affinity, and the result says so
            Assert.Equal(0xFFFF0000UL, spec.Affinity);
            Assert.Equal("0xFFFF0000", r.GetProperty("affinity").GetString());

            // the registry screen shows it at once, and so does a room that starts afterwards from the activity log
            JsonElement reg = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Registry()));
            JsonElement item = reg.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == "first-run");
            Assert.Equal("run", item.GetProperty("kind").GetString());
            Assert.True(item.GetProperty("launched").GetBoolean());
            Assert.True(item.GetProperty("exists").GetBoolean());
            var again = new RoomContext(_fx.Root, Where.Settings(), _fx.Root, _fx.Registry, _fx.Room.Activity.Path, _fx.Launcher, _fx.Queue, _fx.Heat, 32);
            Assert.Contains(new ResourceResolver(again).Catalog(out _), c => c.Id == "first-run" && c.Launched);

            JsonElement row = _fx.LastRow();
            Assert.Equal("run.launch", row.GetProperty("operation").GetString());
            Assert.Equal("ok", row.GetProperty("outcome").GetString());
            Assert.Equal("a test", row.GetProperty("reason").GetString());
            Assert.Equal("first-run", row.GetProperty("args").GetProperty("name").GetString());
            Assert.Equal(4, row.GetProperty("args").GetProperty("threads").GetInt32());
            Assert.Equal(folderRel, row.GetProperty("result").GetProperty("folder").GetString());
            Assert.Equal("0xFFFF0000", row.GetProperty("result").GetProperty("affinity").GetString());
            Assert.DoesNotContain(_fx.Root, File.ReadAllText(_fx.Room.Activity.Path));
        }

        [Fact]
        public void ARerunIsPinnedAndRecordedToo()
        {
            // a rerun goes through the same launch path; its record lists affinity beside threads
            OperationList ops = _fx.Ops;
            Operation rerun = ops.All.Single(o => o.Id == "run.rerun");
            Operation launch = ops.All.Single(o => o.Id == "run.launch");
            var sample = new { affinity = "0xFFFF0000", threads = 4, id = "x", pid = 1 };
            Assert.Contains("\"affinity\"", Js.Serialize(launch.Record(sample)));
            Assert.Contains("\"affinity\"", Js.Serialize(rerun.Record(sample)));
        }

        [Fact]
        public void ALaunchWhoseMaskNamesAProcessorTheMachineLacksIsRefusedAndStartsNothing()
        {
            // the room's tests give the machine 32 logical processors (bits 0 to 31): bit 32 is one it lacks
            using (Fx room = Fx.WithAffinity("0x100000000"))
            {
                OperationResult r = room.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name = "no-such-core", threads = 2, reason = "a test" }));
                Assert.False(r.Ok);
                Assert.Equal(400, r.Status);
                Assert.Contains("lacks", r.Error);
                Assert.Contains("processor(s) 32", r.Error);
                Assert.Empty(room.Launcher.Started);
                Assert.False(Directory.Exists(Path.Combine(room.RunsDir, "no-such-core")));
                Assert.False(File.Exists(Path.Combine(room.RunsDir, "no-such-core.launch.log")));
            }
        }

        [Fact]
        public void ARunThatEndsUpInTheRegistryIsListedOnceNotTwice()
        {
            Fx.Result(_fx.Call("run.launch", Launch("twice-run")));
            string folder = Path.Combine(_fx.RunsDir, "twice-run");
            var entry = new RegistryEntry { Recipe = "brain2", RecipeHash = "h", RecipePath = "rounds/nursery/brain2-w5.json", Out = folder, Status = "completed", StartedAtUtc = "2026-10-02T00:00:00Z", EndedAtUtc = "2026-10-02T01:00:00Z" };
            RegistryEntry.Append(_fx.Registry, entry);
            var items = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Registry())).GetProperty("items").EnumerateArray().ToArray();
            Assert.Single(items);
            Assert.Equal("entry", items[0].GetProperty("kind").GetString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("Upper")]
        [InlineData("-leading")]
        [InlineData("has space")]
        [InlineData("under_score")]
        [InlineData("dots.here")]
        [InlineData("../escape")]
        [InlineData("a/b")]
        [InlineData("12")]
        [InlineData("fine\n")]               // a $ anchor would let a trailing line break through
        [InlineData("fine\r\n")]
        [InlineData("con")]                  // a Windows device name, with or without an extension
        [InlineData("nul")]
        [InlineData("aux")]
        [InlineData("prn")]
        [InlineData("com1")]
        [InlineData("lpt9")]
        [InlineData("a&b")]
        [InlineData("a%b")]
        [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
        public void ABadFolderNameIsRefusedAndNothingStarts(string name)
        {
            OperationResult r = _fx.Call("run.launch", Launch(name));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("name", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.False(Directory.Exists(_fx.RunsDir));
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
        }

        [Fact]
        public void AnExistingFolderIsRefused()
        {
            Directory.CreateDirectory(Path.Combine(_fx.RunsDir, "taken"));
            OperationResult r = _fx.Call("run.launch", Launch("taken"));
            Assert.False(r.Ok);
            Assert.Contains("exists", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            // and a second launch of a name just launched
            Fx.Result(_fx.Call("run.launch", Launch("fresh")));
            Assert.False(_fx.Call("run.launch", Launch("fresh")).Ok);
            Assert.Single(_fx.Launcher.Started);
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
        }

        [Fact]
        public void ARecipeTheNurseryRefusesStartsNothing()
        {
            string bad = _fx.RecipePath();
            // not written through the room: the operation under test is the launch, and it must refuse what is on disk
            File.WriteAllText(Path.Combine(_fx.Root, bad), _fx.Brain2.Replace("\"world\": {", "\"world\": {\n    \"extra\": 1,"));
            OperationResult r = _fx.Call("run.launch", Launch("never", recipe: bad));
            Assert.False(r.Ok);
            Assert.Contains("nursery refuses", r.Error);
            Assert.Contains("extra", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "never")));
            Assert.False(_fx.Call("run.launch", Launch("never", recipe: "rounds/nursery/no-such.json")).Ok);
            Assert.False(_fx.Call("run.launch", Launch("never", recipe: "../x.json")).Ok);
            Assert.False(_fx.Call("run.launch", Launch("never", recipe: "C:/x.json")).Ok);
        }

        [Fact]
        public void ATreeWithoutTheNurseryBuiltIsRefused()
        {
            File.Delete(Path.Combine(_fx.Root, "bin", "nursery.dll"));
            OperationResult r = _fx.Call("run.launch", Launch("nobuild"));
            Assert.False(r.Ok);
            Assert.Contains("not built", r.Error);
            Assert.Contains("dotnet build src/Evosim.Nursery -c Release", r.Error);    // the message says how to build it
            Assert.Empty(_fx.Launcher.Started);
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "nobuild")));
            Assert.False(File.Exists(Path.Combine(_fx.RunsDir, "nobuild.launch.log")));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void ThreadsBelowOneAreRefused(int threads)
        {
            Assert.False(_fx.Call("run.launch", Launch("nothreads", threads)).Ok);
            Assert.Empty(_fx.Launcher.Started);
        }

        [Fact]
        public void AMissingReasonIsRefusedAndRecorded()
        {
            OperationResult r = _fx.Call("run.launch", "{\"recipe\":\"rounds/nursery/brain2-w5.json\",\"name\":\"noreason\",\"threads\":2}");
            Assert.False(r.Ok);
            Assert.Contains("reason", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.Equal("run.launch", _fx.LastRow().GetProperty("operation").GetString());
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
        }

        [Fact]
        public void ALaunchPastTheFreeCoresIsRefusedWithTheNumbersAndOneThatFitsIsPinnedToThem()
        {
            // the room's slot is 16-31; two other Running lines claim 16-19 and 20-23 inside it, so 24-31 are free
            _fx.Queue.Text = FixedQueue.With("speed (a test) · off the card · cores: 16-19", "video (a test) · a refilm · cores: 20-23 · card");
            OperationResult r = _fx.Call("run.launch", Launch("toomany", 9));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Equal("9 threads asked is more than the 8 free cores (logical 24-31): the room's slot is logical 16-31, less 16-19 claimed by \"speed (a test)\", "
                + "less 20-23 claimed by \"video (a test)\". Nothing was started.", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "toomany")));
            Assert.False(File.Exists(Path.Combine(_fx.RunsDir, "toomany.launch.log")));
            JsonElement row = _fx.LastRow();
            Assert.Equal("refused", row.GetProperty("outcome").GetString());
            Assert.Contains("8 free cores (logical 24-31)", row.GetProperty("detail").GetString());

            // eight fit exactly, and the run is pinned to the free cores, not to room.json's affinity (0xFFFF0000, the fixtures' bound)
            JsonElement ok = Fx.Result(_fx.Call("run.launch", Launch("justfits", 8)));
            Assert.Equal(0xFF000000UL, Assert.Single(_fx.Launcher.Started).Affinity);
            Assert.Equal("0xFF000000", ok.GetProperty("affinity").GetString());
            Assert.Equal("24-31", ok.GetProperty("machine").GetProperty("free").GetString());
            Assert.Equal("16-31", ok.GetProperty("machine").GetProperty("slot").GetString());
            Assert.Equal("0xFF000000", _fx.LastRow().GetProperty("result").GetProperty("affinity").GetString());

            // the run it started now counts: one more thread does not fit, and the refusal says why
            string more = _fx.Call("run.launch", Launch("onemore", 1)).Error;
            Assert.StartsWith("1 thread asked, plus 8 threads granted to runs this room started that are still running or began in the last 3 minutes and to episode re-steps whose process lives, is 9, more than the 8 free cores (logical 24-31): ", more);
        }

        [Fact]
        public void AQueueTheRoomCannotUseRefusesEveryLaunchAndSaysWhy()
        {
            _fx.Queue.Text = "# The machine's queue\n\n## Next, in order\n\n- a job · cores: 16-31 · room\n";
            Assert.Equal("the machine's queue scratch/machine/queue.md has no ## Running section, so the room's slot is not known; refusing 1 thread. Nothing was started.",
                _fx.Call("run.launch", Launch("nosection", 1)).Error);
            _fx.Queue.Text = "## Running\n\n- speed (a test) · cores: 0-3\n";
            Assert.Equal("no Running line of the machine's queue scratch/machine/queue.md carries the room tag, so the room has no slot, and it starts nothing until the coordinator gives it one; refusing 2 threads. Nothing was started.",
                _fx.Call("run.launch", Launch("noslot", 2)).Error);
            _fx.Queue.Text = "## Running\n\n- the room (a test) · room\n";
            Assert.Equal("the room's line \"the room (a test)\" in the machine's queue scratch/machine/queue.md has no cores: tag, so the room's slot is not known; refusing 1 thread. Nothing was started.",
                _fx.Call("run.launch", Launch("noslotcores", 1)).Error);
            _fx.Queue.Text = FixedQueue.With("video (a test) · a refilm on unity-w6 · card");
            Assert.Equal("1 thread asked is more than the 0 free cores: the room's slot is logical 16-31, less 16-31 claimed by \"video (a test)\", which has no cores: tag and so claims every processor. Nothing was started.",
                _fx.Call("run.launch", Launch("allclaimed", 1)).Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.Equal(4, _fx.Rows().Count(row => row.GetProperty("outcome").GetString() == "refused"));
        }

        [Fact]
        public void TheHeatRefusesWhenHotOrUnreadAndAWarnIsStartedAndSaid()
        {
            _fx.Heat.Value = HeatReading.Of(HeatReading.Hot, "2026-10-11 12:00:00  HOT  gpu 86C [GPU 86 C]");
            Assert.Equal("the heat check reads HOT (2026-10-11 12:00:00  HOT  gpu 86C [GPU 86 C]), and the room starts nothing on a hot machine; refusing 4 threads. Nothing was started.",
                _fx.Call("run.launch", Launch("hot", 4)).Error);
            _fx.Heat.Value = HeatReading.FromExit(3, "");
            Assert.Equal("the heat check cannot say whether the machine is too hot (the heat check exited 3); refusing 4 threads. Nothing was started.",
                _fx.Call("run.launch", Launch("unread", 4)).Error);
            Assert.Empty(_fx.Launcher.Started);

            _fx.Heat.Value = HeatReading.Of(HeatReading.Warn, "2026-10-11 12:00:00  WARN  gpu 81C [GPU 81 C]");
            JsonElement warm = Fx.Result(_fx.Call("run.launch", Launch("warm", 4)));
            Assert.Single(_fx.Launcher.Started);
            Assert.Equal("WARN", warm.GetProperty("heat").GetProperty("verdict").GetString());
            Assert.Contains("WARN does not refuse", warm.GetProperty("heat").GetProperty("note").GetString());
            Assert.Equal("WARN", _fx.LastRow().GetProperty("result").GetProperty("heat").GetProperty("verdict").GetString());
        }

        [Fact]
        public void ALauncherThatFailsLeavesNoFolderAndIsRecordedAsFailed()
        {
            _fx.Launcher.Throw = new InvalidOperationException("no such program");
            OperationResult r = _fx.Call("run.launch", Launch("broken"));
            Assert.False(r.Ok);
            Assert.Equal(500, r.Status);
            Assert.Contains("no such program", r.Error);
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "broken")));
            Assert.Equal("failed", _fx.LastRow().GetProperty("outcome").GetString());
            // and the name is free again
            _fx.Launcher.Throw = null;
            Fx.Result(_fx.Call("run.launch", Launch("broken")));
        }

        [Fact]
        public void TheCommandLineIsOneCmdLineThatRedirectsItsOutputOutsideTheRunsFolder()
        {
            var spec = new LaunchSpec
            {
                FileName = "C:/Program Files/dotnet/dotnet.exe", Arguments = new[] { "D:/x/n.dll", "--recipe", "r.json", "--out", "runs/a-b", "--threads", "4" },
                WorkingDirectory = "D:/x", Folder = "D:/x/runs/a-b", LogFile = "D:/x/runs/a-b.launch.log",
            };
            Assert.Equal("\"\"C:/Program Files/dotnet/dotnet.exe\" \"D:/x/n.dll\" \"--recipe\" \"r.json\" \"--out\" \"runs/a-b\" \"--threads\" \"4\" > \"D:/x/runs/a-b.launch.log\" 2>&1 < NUL\"", ProcessRunLauncher.CmdLine(spec));
        }
    }

    public class RerunTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private RegistryEntry Entry(string hash = null, string reason = null, bool rerunnable = true) => new RegistryEntry
        {
            Recipe = "brain2", RecipeHash = hash ?? Recipe.Load(Path.Combine(_fx.Root, "rounds", "nursery", "brain2-w5.json")).Hash(),
            RecipePath = "rounds/nursery/brain2-w5.json", Out = Path.Combine(_fx.Dir, "entry-out"), Status = "completed", StartedAtUtc = "2026-10-02T00:00:00Z", EndedAtUtc = "2026-10-02T01:00:00Z",
            GitCommit = "abc1234", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n",
            Rerunnable = rerunnable, NotRerunnableBecause = rerunnable ? null : reason ?? "the tree was dirty",
        };

        private static string Rerun(string entry, string name = "again", int threads = 4) => Js.Serialize(new { entry, name, threads, reason = "a test" });

        [Fact]
        public void ARerunOfAnEntryOnItsOwnBuildStartsTheNurseryWithRerun()
        {
            RegistryEntry.Append(_fx.Registry, Entry());
            JsonElement r = Fx.Result(_fx.Call("run.rerun", Rerun("1")));
            LaunchSpec spec = _fx.Launcher.Started.Single();
            Assert.Equal(new[] { "--rerun", "1", "--out", _fx.Rel(Path.Combine(_fx.RunsDir, "again")), "--threads", "4" }, spec.Arguments.Skip(1).ToArray());
            Assert.Equal(_fx.Root, spec.WorkingDirectory);
            Assert.Equal("rerun", r.GetProperty("kind").GetString());
            Assert.Equal("run.rerun", _fx.LastRow().GetProperty("operation").GetString());
            Assert.Equal("ok", _fx.LastRow().GetProperty("outcome").GetString());

            // the same entry by 8 or more digits of its recipe's hash
            string hash = Entry().RecipeHash;
            Fx.Result(_fx.Call("run.rerun", Rerun(hash.Substring(0, 10), "by-hash")));
            Assert.Equal("--rerun", _fx.Launcher.Started[1].Arguments[1]);
            Assert.Equal(hash.Substring(0, 10), _fx.Launcher.Started[1].Arguments[2]);
        }

        [Fact]
        public void ABuildWhoseTreeHashesDifferFromTheEntrysIsRefused()
        {
            RegistryEntry.Append(_fx.Registry, Entry());
            _fx.Build = new RunRecord { CoreHash = "c", DynamicsHash = "d", FarmHash = "OTHER", NurseryHash = "OTHER2", GitCommit = "fff0000" };
            OperationResult r = _fx.Call("run.rerun", Rerun("1"));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("farm, nursery tree hashes differ", r.Error);
            Assert.Contains("abc1234", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "again")));
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
        }

        [Fact]
        public void AnEditedRecipeAnUnknownEntryAndAnEntryThatCannotBeRerunAreRefused()
        {
            RegistryEntry.Append(_fx.Registry, Entry(hash: new string('e', 64)));
            Assert.Contains("edited after its run", _fx.Call("run.rerun", Rerun("1")).Error);

            Assert.Contains("no entry \"9\"", _fx.Call("run.rerun", Rerun("9")).Error);
            Assert.Contains("no entry \"0\"", _fx.Call("run.rerun", Rerun("0")).Error);
            Assert.Contains("neither", _fx.Call("run.rerun", Rerun("zzzzzzzzzz")).Error);
            Assert.Contains("0 rerunnable first runs", _fx.Call("run.rerun", Rerun("abcdef0123")).Error);

            RegistryEntry.Append(_fx.Registry, Entry(rerunnable: false, reason: "dirty tree"));
            Assert.Contains("dirty tree", _fx.Call("run.rerun", Rerun("2")).Error);

            var reference = Entry();
            reference.Reference = new RegistryEntry.ReferenceNote { Build = "b", Verdict = "tied" };
            RegistryEntry.Append(_fx.Registry, reference);
            Assert.Contains("reference entry", _fx.Call("run.rerun", Rerun("3")).Error);
            Assert.Empty(_fx.Launcher.Started);
        }

        [Fact]
        public void ARerunIsCappedAndNamedLikeALaunch()
        {
            RegistryEntry.Append(_fx.Registry, Entry());
            _fx.Queue.Text = FixedQueue.With("speed (a test) · cores: 16-29");   // two free cores: the rerun's four do not fit
            Assert.Contains("more than the 2 free cores (logical 30-31)", _fx.Call("run.rerun", Rerun("1")).Error);
            _fx.Queue.Text = FixedQueue.With();
            Assert.Contains("name", _fx.Call("run.rerun", Rerun("1", "Bad Name")).Error);
            Directory.CreateDirectory(Path.Combine(_fx.RunsDir, "again"));
            Assert.Contains("exists", _fx.Call("run.rerun", Rerun("1")).Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.Equal(3, _fx.Rows().Count);
        }

        [Fact]
        public void AMissingReasonIsRefused()
        {
            RegistryEntry.Append(_fx.Registry, Entry());
            Assert.Contains("reason", _fx.Call("run.rerun", "{\"entry\":\"1\",\"name\":\"x\",\"threads\":1}").Error);
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
        }
    }

    public class StopTests : IDisposable
    {
        private readonly Fx _fx;
        private readonly string _folder;
        public StopTests()
        {
            _fx = new Fx("scratch/live");
            _folder = Path.Combine(_fx.Root, "scratch", "live");
            Directory.CreateDirectory(_folder);
        }
        public void Dispose() => _fx.Dispose();

        private static string Stop(string id) => Js.Serialize(new { id, reason = "a test" });

        private void WriteRun(string status, bool stopFile, bool alive = true)
        {
            using (Process self = Process.GetCurrentProcess())
            {
                string started = alive ? self.StartTime.ToUniversalTime().ToString("o") : DateTime.UtcNow.AddDays(-9).ToString("o");
                File.WriteAllText(Path.Combine(_folder, "run.json"),
                    "{\"status\":\"" + status + "\",\"processId\":" + (alive ? self.Id : 999999) + ",\"processStartedAtUtc\":\"" + started + "\"" +
                    (stopFile ? ",\"stopFile\":\"STOP\"" : "") + ",\"build\":{\"gitCommit\":\"0123456789abcdef\"}}");
            }
        }

        [Fact]
        public void ARunningRunGetsAStopFileAndIsToldItsBuildReadsIt()
        {
            WriteRun("running", stopFile: true);
            JsonElement r = Fx.Result(_fx.Call("run.stop", Stop("live")));
            Assert.True(File.Exists(Path.Combine(_folder, "STOP")));
            Assert.True(r.GetProperty("written").GetBoolean());
            Assert.True(r.GetProperty("readsStopFile").GetBoolean());
            Assert.Contains("between generations", r.GetProperty("note").GetString());
            Assert.EndsWith("/STOP", r.GetProperty("stopFile").GetString());
            Assert.DoesNotContain(_fx.Root, File.ReadAllText(_fx.Room.Activity.Path));
            JsonElement row = _fx.LastRow();
            Assert.Equal("run.stop", row.GetProperty("operation").GetString());
            Assert.Equal("ok", row.GetProperty("outcome").GetString());

            // asked again: already there, nothing rewritten
            JsonElement again = Fx.Result(_fx.Call("run.stop", Stop("live")));
            Assert.True(again.GetProperty("alreadyThere").GetBoolean());
            Assert.False(again.GetProperty("written").GetBoolean());
        }

        [Fact]
        public void ABuildThatPredatesTheStopFileIsToldItMayNotReadIt()
        {
            WriteRun("running", stopFile: false);
            JsonElement r = Fx.Result(_fx.Call("run.stop", Stop("live")));
            Assert.True(File.Exists(Path.Combine(_folder, "STOP")));
            Assert.False(r.GetProperty("readsStopFile").GetBoolean());
            Assert.Contains("predates the stop file", r.GetProperty("note").GetString());
            Assert.Contains("may not read it", r.GetProperty("note").GetString());
            Assert.Contains("0123456", r.GetProperty("note").GetString());
        }

        [Fact]
        public void AFolderWithNoRecordThatTheRoomDidNotLaunchIsRefusedAndNothingIsWritten()
        {
            OperationResult r = _fx.Call("run.stop", Stop("live"));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("no run.json", r.Error);
            Assert.Contains("did not launch", r.Error);
            Assert.False(File.Exists(Path.Combine(_folder, "STOP")));
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());

            // some rows with no record are no better: an unrecorded folder is not offered a STOP file either
            File.WriteAllText(Path.Combine(_folder, "tank-generations.jsonl"), "{\"gen\":0}\n");
            Assert.False(_fx.Call("run.stop", Stop("live")).Ok);
            Assert.False(File.Exists(Path.Combine(_folder, "STOP")));
        }

        [Fact]
        public void ARunTheRoomLaunchedIsStoppedBeforeItBeginsAndIsReportedSo()
        {
            Fx.Result(_fx.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name = "early", threads = 2, reason = "a test" })));
            string folder = Path.Combine(_fx.RunsDir, "early");
            Assert.True(Directory.Exists(folder));
            JsonElement r = Fx.Result(_fx.Call("run.stop", Js.Serialize(new { id = "early", reason = "changed my mind" })));
            Assert.True(File.Exists(Path.Combine(folder, "STOP")));
            Assert.True(r.GetProperty("stoppedBeforeItBegan").GetBoolean());
            Assert.Contains("stopped before it began", r.GetProperty("note").GetString());
            Assert.True(_fx.LastRow().GetProperty("result").GetProperty("stoppedBeforeItBegan").GetBoolean());

            // the room reads a folder holding only STOP (and a launcher's log) the same way
            File.WriteAllText(Path.Combine(folder, "early.launch.log"), "--out is not empty\n");
            Assert.True(RunFolder.StoppedBeforeItBegan(folder));
            JsonElement reg = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Registry()));
            JsonElement item = reg.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == "early");
            Assert.Equal("stopped", item.GetProperty("phase").GetString());
            File.WriteAllText(Path.Combine(folder, "nursery.log"), "a line\n");
            Assert.False(RunFolder.StoppedBeforeItBegan(folder));
        }

        [Theory]
        [InlineData("completed")]
        [InlineData("exited")]
        [InlineData("failed")]
        [InlineData("stopped")]
        public void AnEndedRunIsRefusedAndNothingIsWritten(string status)
        {
            WriteRun(status, stopFile: true);
            OperationResult r = _fx.Call("run.stop", Stop("live"));
            Assert.False(r.Ok);
            Assert.Contains("has ended", r.Error);
            Assert.False(File.Exists(Path.Combine(_folder, "STOP")));
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
        }

        [Fact]
        public void ARunWhoseProcessIsGoneIsRefusedAndNeverKilled()
        {
            WriteRun("running", stopFile: true, alive: false);
            OperationResult r = _fx.Call("run.stop", Stop("live"));
            Assert.False(r.Ok);
            Assert.Contains("process is gone", r.Error);
            Assert.False(File.Exists(Path.Combine(_folder, "STOP")));
            using (Process self = Process.GetCurrentProcess()) Assert.False(self.HasExited);
        }

        [Fact]
        public void AnUnknownRunIsRefusedAndRecorded()
        {
            OperationResult r = _fx.Call("run.stop", Stop("nowhere"));
            Assert.False(r.Ok);
            Assert.Contains("no run", r.Error);
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
            Assert.Contains("reason", _fx.Call("run.stop", "{\"id\":\"live\"}").Error);
        }
    }

    public class CompareTests : IDisposable
    {
        private readonly Fx _fx;
        private readonly string _a, _b;
        public CompareTests()
        {
            _fx = new Fx();
            _a = Path.Combine(_fx.Dir, "a"); _b = Path.Combine(_fx.Dir, "b");
            Directory.CreateDirectory(_a); Directory.CreateDirectory(_b);
            foreach (string d in new[] { _a, _b })
            {
                File.WriteAllText(Path.Combine(d, "same.jsonl"), "{\"gen\":0}\n{\"gen\":1}\n");
                File.WriteAllBytes(Path.Combine(d, "genomes.bin"), new byte[] { 1, 2, 3 });
            }
            // each folder is a run the room knows by its registry entry
            RegistryEntry.Append(_fx.Registry, new RegistryEntry { Recipe = "a", RecipeHash = "h1", RecipePath = "p", Out = _a, Status = "completed", StartedAtUtc = "2026-10-01T00:00:00Z", EndedAtUtc = "2026-10-01T01:00:00Z" });
            RegistryEntry.Append(_fx.Registry, new RegistryEntry { Recipe = "b", RecipeHash = "h2", RecipePath = "p", Out = _b, Status = "completed", StartedAtUtc = "2026-10-02T00:00:00Z", EndedAtUtc = "2026-10-02T01:00:00Z" });
        }
        public void Dispose() => _fx.Dispose();

        private JsonElement Compare(string a, string b) => Fx.Result(_fx.Call("compare", "{\"a\":\"" + a + "\",\"b\":\"" + b + "\"}"));
        private static JsonElement File_(JsonElement r, string name) => r.GetProperty("files").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);

        [Fact]
        public void TwoFoldersWithTheSameBytesAreIdenticalAndAFolderComparedWithItselfToo()
        {
            JsonElement r = Compare("1", "2");
            Assert.Equal("identical", r.GetProperty("verdict").GetString());
            Assert.True(r.GetProperty("resultsIdentical").GetBoolean());
            Assert.All(r.GetProperty("files").EnumerateArray(), f => Assert.True(f.GetProperty("identical").GetBoolean()));
            Assert.Equal(new[] { "genomes.bin", "same.jsonl" }, r.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("name").GetString()).ToArray());
            Assert.Equal(64, File_(r, "same.jsonl").GetProperty("sha256A").GetString().Length);
            Assert.Equal("identical", Compare("1", "1").GetProperty("verdict").GetString());
            // a comparison changes nothing and writes no row
            Assert.Empty(_fx.Rows());
        }

        [Fact]
        public void ATextFileThatDiffersGivesItsFirstDifferingLineWithBothSidesCutToTwoHundred()
        {
            File.WriteAllText(Path.Combine(_a, "rows.jsonl"), "{\"n\":0}\n" + new string('a', 300) + "\n{\"n\":2}\n");
            File.WriteAllText(Path.Combine(_b, "rows.jsonl"), "{\"n\":0}\n" + new string('b', 300) + "\n{\"n\":3}\n");
            JsonElement r = Compare("1", "2");
            Assert.Equal("differs", r.GetProperty("verdict").GetString());
            JsonElement f = File_(r, "rows.jsonl");
            Assert.False(f.GetProperty("identical").GetBoolean());
            Assert.Equal(2, f.GetProperty("firstDifferentLine").GetInt32());
            Assert.Equal(new string('a', 200) + "...", f.GetProperty("lineA").GetString());
            Assert.Equal(new string('b', 200) + "...", f.GetProperty("lineB").GetString());
            Assert.False(File_(r, "same.jsonl").GetProperty("firstDifferentLine").ValueKind == JsonValueKind.Number);
            Assert.NotEqual(f.GetProperty("sha256A").GetString(), f.GetProperty("sha256B").GetString());
        }

        [Fact]
        public void AFileInOneFolderOnlyAndALineMissingFromTheShorterFileAreBothSeen()
        {
            File.WriteAllText(Path.Combine(_a, "only-a.log"), "x\n");
            File.WriteAllText(Path.Combine(_a, "long.txt"), "1\n2\n3\n");
            File.WriteAllText(Path.Combine(_b, "long.txt"), "1\n2\n");
            JsonElement r = Compare("1", "2");
            JsonElement only = File_(r, "only-a.log");
            Assert.False(only.GetProperty("identical").GetBoolean());
            Assert.Equal(JsonValueKind.Null, only.GetProperty("sha256B").ValueKind);
            Assert.Equal(JsonValueKind.Null, only.GetProperty("firstDifferentLine").ValueKind);
            JsonElement l = File_(r, "long.txt");
            Assert.Equal(3, l.GetProperty("firstDifferentLine").GetInt32());
            Assert.Equal("3", l.GetProperty("lineA").GetString());
            Assert.Equal("<end of file>", l.GetProperty("lineB").GetString());
        }

        [Fact]
        public void ABinaryFileThatDiffersGivesHashesOnlyAndTheResultsVerdictIgnoresLogsAndTheRecord()
        {
            File.WriteAllBytes(Path.Combine(_b, "genomes.bin"), new byte[] { 9 });
            JsonElement bin = File_(Compare("1", "2"), "genomes.bin");
            Assert.False(bin.GetProperty("identical").GetBoolean());
            Assert.Equal(JsonValueKind.Null, bin.GetProperty("firstDifferentLine").ValueKind);
            File.WriteAllBytes(Path.Combine(_b, "genomes.bin"), new byte[] { 1, 2, 3 });

            File.WriteAllText(Path.Combine(_a, "nursery.log"), "run a\n");
            File.WriteAllText(Path.Combine(_b, "nursery.log"), "run b\n");
            File.WriteAllText(Path.Combine(_a, "run.json"), "{\"a\":1}");
            File.WriteAllText(Path.Combine(_b, "run.json"), "{\"a\":2}");
            JsonElement r = Compare("1", "2");
            Assert.Equal("differs", r.GetProperty("verdict").GetString());
            Assert.True(r.GetProperty("resultsIdentical").GetBoolean());
        }

        [Fact]
        public void AStopFileIsNotAResultAndASubfolderIsListedAsNotCompared()
        {
            File.WriteAllText(Path.Combine(_a, "STOP"), "stop requested");
            Directory.CreateDirectory(Path.Combine(_a, "sub"));
            File.WriteAllText(Path.Combine(_a, "sub", "x.txt"), "x");
            JsonElement r = Compare("1", "2");
            Assert.Equal("differs", r.GetProperty("verdict").GetString());            // STOP is in one folder only: a file that differs
            Assert.True(r.GetProperty("resultsIdentical").GetBoolean());              // but it is not a result
            JsonElement sub = r.GetProperty("subfolders").EnumerateArray().Single();
            Assert.Equal("sub", sub.GetProperty("name").GetString());
            Assert.True(sub.GetProperty("inA").GetBoolean());
            Assert.False(sub.GetProperty("inB").GetBoolean());
            Assert.Equal("not compared", sub.GetProperty("note").GetString());
            Assert.NotEqual(JsonValueKind.Null, r.GetProperty("verdictNote").ValueKind);
            Assert.DoesNotContain(r.GetProperty("files").EnumerateArray(), f => f.GetProperty("name").GetString() == "x.txt");
        }

        [Fact]
        public void AFileBeingAppendedToIsReadWithoutLockingItsWriter()
        {
            using (var writer = new FileStream(Path.Combine(_a, "live.jsonl"), FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                writer.Write(Encoding.UTF8.GetBytes("{\"n\":1}\n"));
                writer.Flush();
                File.WriteAllText(Path.Combine(_b, "live.jsonl"), "{\"n\":1}\n");
                Assert.True(File_(Compare("1", "2"), "live.jsonl").GetProperty("identical").GetBoolean());
            }
        }

        [Fact]
        public void AnUnknownRunIsRefused()
        {
            Assert.Contains("no run", _fx.Call("compare", "{\"a\":\"1\",\"b\":\"nowhere\"}").Error);
            Assert.Equal(400, _fx.Call("compare", "{\"a\":\"1\"}").Status);
        }
    }

    public class RegisteredRecipeTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private string Copy(string rel)
        {
            string full = _fx.Full(rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, _fx.Brain2);
            return rel;
        }

        private static string Text(ModelContextProtocol.Protocol.CallToolResult r) => ((ModelContextProtocol.Protocol.TextContentBlock)r.Content[0]).Text;

        [Theory]
        [InlineData("rounds/other/x.json")]
        [InlineData("rounds/x.json")]
        [InlineData("scratch/x.json")]
        public void ARecipeOutsideRoundsNurseryIsLaunchedAsATrialThroughEitherDoor(string rel)
        {
            if (!File.Exists(_fx.Full(rel))) Copy(rel);

            // the screen's door
            JsonElement screen = Fx.Result(_fx.Call("run.launch", Js.Serialize(new { recipe = rel, name = "trial-one", threads = 2, reason = "a test" })));
            Assert.True(screen.GetProperty("trial").GetBoolean());
            Assert.Equal(rel, screen.GetProperty("recipe").GetString());

            // the MCP's door
            var mcpArgs = new Dictionary<string, JsonElement>
            {
                ["action"] = Js.Parse("\"launch\""), ["recipe"] = Js.Parse(Js.Serialize(rel)), ["name"] = Js.Parse("\"trial-two\""),
                ["threads"] = Js.Parse("2"), ["reason"] = Js.Parse("\"a test\""),
            };
            ModelContextProtocol.Protocol.CallToolResult mcp = McpSurface.Call(_fx.Ops, "run", mcpArgs);
            Assert.True(mcp.IsError != true, Text(mcp));
            Assert.True(Js.Parse(Text(mcp)).GetProperty("result").GetProperty("trial").GetBoolean());

            // both started: the nursery was given the recipe where it lies, in a new folder each
            Assert.Equal(2, _fx.Launcher.Started.Count);
            Assert.All(_fx.Launcher.Started, s => Assert.Equal(new[] { "--recipe", rel }, s.Arguments.Skip(1).Take(2).ToArray()));
            Assert.True(Directory.Exists(Path.Combine(_fx.RunsDir, "trial-one")));
            Assert.True(Directory.Exists(Path.Combine(_fx.RunsDir, "trial-two")));

            // the activity rows say trial
            List<JsonElement> rows = _fx.Rows();
            Assert.Equal(new[] { "screen", "mcp" }, rows.Select(r => r.GetProperty("who").GetString()).ToArray());
            Assert.All(rows, r =>
            {
                Assert.Equal("ok", r.GetProperty("outcome").GetString());
                Assert.True(r.GetProperty("result").GetProperty("trial").GetBoolean());
            });

            // the catalogue marks both as trials, and so does a room that starts afterwards from the activity log
            JsonElement[] items = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Registry())).GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(2, items.Length);
            Assert.All(items, i => Assert.True(i.GetProperty("trial").GetBoolean()));
            var again = new RoomContext(_fx.Root, Where.Settings(), _fx.Root, _fx.Registry, _fx.Room.Activity.Path, _fx.Launcher, _fx.Queue, _fx.Heat, 32);
            Assert.All(new ResourceResolver(again).Catalog(out _), c => Assert.True(c.Trial));

            // and the run screen's data carries it
            Assert.True(Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Run("trial-one"))).GetProperty("trial").GetBoolean());
        }

        [Fact]
        public void ARegisteredRecipesLaunchIsNoTrialInItsResultItsRowOrTheCatalogue()
        {
            JsonElement r = Fx.Result(_fx.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name = "registered-one", threads = 2, reason = "a test" })));
            Assert.False(r.GetProperty("trial").GetBoolean());
            Assert.False(_fx.LastRow().GetProperty("result").GetProperty("trial").GetBoolean());
            JsonElement item = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Registry())).GetProperty("items").EnumerateArray().Single();
            Assert.False(item.GetProperty("trial").GetBoolean());
            Assert.False(Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Run("registered-one"))).GetProperty("trial").GetBoolean());
        }

        [Fact]
        public void ARecipeInASubfolderOfRoundsNurseryIsStillRefusedAndIsNoTrial()
        {
            Copy("rounds/nursery/sub/x.json");
            OperationResult r = _fx.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/sub/x.json", name = "sub-one", threads = 2, reason = "a test" }));
            Assert.False(r.Ok);
            Assert.Contains("the room launches only plain files directly under rounds/nursery/", r.Error);
            Assert.Empty(_fx.Launcher.Started);
        }

        [Fact]
        public void ATrialRecipeTheNurseryRefusesStartsNothing()
        {
            Directory.CreateDirectory(_fx.Full("rounds/other"));
            File.WriteAllText(_fx.Full("rounds/other/bad.json"), "{}");
            OperationResult r = _fx.Call("run.launch", Js.Serialize(new { recipe = "rounds/other/bad.json", name = "bad-trial", threads = 2, reason = "a test" }));
            Assert.False(r.Ok);
            Assert.Contains("the nursery refuses", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "bad-trial")));
        }

        [Fact]
        public void ARegisteredRecipeStillLaunchesAndAPathWithAStrayCharacterDoesNot()
        {
            Fx.Result(_fx.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name = "registered", threads = 2, reason = "a test" })));
            Assert.Single(_fx.Launcher.Started);
            File.WriteAllText(_fx.Full("rounds/nursery/a&b.json"), _fx.Brain2);
            OperationResult r = _fx.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/a&b.json", name = "stray", threads = 2, reason = "a test" }));
            Assert.False(r.Ok);
            Assert.Contains("outside A-Z", r.Error);
            Assert.Single(_fx.Launcher.Started);
        }
    }

    public class CommandLineTests
    {
        private static LaunchSpec Spec(params string[] args) => new LaunchSpec
        {
            FileName = "C:/Program Files/dotnet/dotnet.exe", Arguments = new[] { "D:\\x\\n.dll" }.Concat(args).ToList(),
            WorkingDirectory = "D:/x", Folder = "D:/x/runs/a-b", LogFile = "D:/x/runs/a-b.launch.log",
        };

        [Fact]
        public void PlainArgumentsPassAndThePathsMayHoldASpace()
        {
            Assert.Null(CommandLine.Problem(Spec("--recipe", "rounds/nursery/brain2-w5.json", "--out", "runs/a-b.c_d", "--threads", "4")));
            Assert.Null(CommandLine.Problem(Spec("--rerun", "ab12cd34ef", "--out", "C:\\runs\\x:y")));
            var spaced = Spec("--threads", "4");
            spaced.Arguments = new[] { "C:/Program Files/nursery/n.dll", "--threads", "4" };
            Assert.Null(CommandLine.Problem(spaced));
        }

        [Theory]
        [InlineData("a&b")]
        [InlineData("a|b")]
        [InlineData("a>b")]
        [InlineData("a<b")]
        [InlineData("a^b")]
        [InlineData("%PATH%")]
        [InlineData("a!b")]
        [InlineData("a b")]            // an option's value takes no space: only a path from the settings may
        [InlineData("a\"b")]
        [InlineData("a;b")]
        [InlineData("a(b)")]
        [InlineData("a`b")]
        [InlineData("a$b")]
        [InlineData("a'b")]
        [InlineData("a,b")]
        [InlineData("a=b")]
        [InlineData("a\nb")]
        [InlineData("")]
        public void AnArgumentWithAnyOtherCharacterIsRefusedAndNeverCleaned(string bad)
        {
            string problem = CommandLine.Problem(Spec("--out", bad));
            Assert.NotNull(problem);
            Assert.Contains("argument 3", problem);
            Assert.Contains("nothing was started", problem);
        }

        [Theory]
        [InlineData("C:/x&y/dotnet.exe")]
        [InlineData("C:/x%y/dotnet.exe")]
        [InlineData("C:/x\"y/dotnet.exe")]
        [InlineData("C:/x(86)/dotnet.exe")]
        public void TheProgramAndTheDllPathsFromTheSettingsAreCheckedToo(string bad)
        {
            LaunchSpec a = Spec("--threads", "4");
            a.FileName = bad;
            Assert.Contains("the program", CommandLine.Problem(a));
            LaunchSpec b = Spec("--threads", "4");
            b.Arguments = new[] { bad, "--threads", "4" };
            Assert.Contains("argument 1", CommandLine.Problem(b));
            LaunchSpec c = Spec("--threads", "4");
            c.LogFile = bad + ".launch.log";
            Assert.Contains("the log file", CommandLine.Problem(c));
        }

        [Fact]
        public void AQuoteIsRefusedByTheQuotingAndNeverRemovedAndTheLineTurnsDelayedExpansionOff()
        {
            Assert.Throws<ArgumentException>(() => ProcessRunLauncher.Quote("a\"b"));
            string line = ProcessRunLauncher.CommandLineFor("C:\\Windows\\System32\\cmd.exe", Spec("--threads", "4"));
            Assert.StartsWith("\"C:\\Windows\\System32\\cmd.exe\" /d /s /v:off /c ", line);
            Assert.EndsWith(" > \"D:/x/runs/a-b.launch.log\" 2>&1 < NUL\"", line);
        }

        [Fact]
        public void ARoomWhoseSettingsNameADotnetWithAMetacharacterLaunchesNothingAndOneWithASpaceLaunches()
        {
            using (Fx bad = Fx.WithDotnet("C:/x&y/dotnet.exe"))
            {
                OperationResult r = bad.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name = "never", threads = 2, reason = "a test" }));
                Assert.False(r.Ok);
                Assert.Equal(400, r.Status);
                Assert.Contains("the program", r.Error);
                Assert.Empty(bad.Launcher.Started);
                Assert.False(Directory.Exists(Path.Combine(bad.RunsDir, "never")));
                Assert.Equal("refused", bad.LastRow().GetProperty("outcome").GetString());
            }
            using (Fx spaced = Fx.WithDotnet("C:/Program Files/Unity/dotnet.exe"))
            {
                Fx.Result(spaced.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name = "fine", threads = 2, reason = "a test" })));
                Assert.Equal("C:/Program Files/Unity/dotnet.exe", spaced.Launcher.Started.Single().FileName);
            }
        }

        [Fact]
        public void TheRealLauncherStartsAPlainProgramWithItsOutputInTheLogAndSaysWhetherItLeftTheRoomsJob()
        {
            if (!OperatingSystem.IsWindows()) return;
            string dir = Where.Fresh("real-launch");
            try
            {
                string log = Path.Combine(dir, "out.launch.log");
                var spec = new LaunchSpec
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "hostname.exe"), Arguments = new string[0],
                    WorkingDirectory = dir, Folder = Path.Combine(dir, "out"), LogFile = log,
                };
                LaunchOutcome o = new ProcessRunLauncher().Start(spec);
                Assert.True(o.Pid > 0);
                // detached means no note; a launcher that could not break away says so
                Assert.Equal(o.Detached, o.Note == null);
                for (int i = 0; i < 100 && !(File.Exists(log) && new FileInfo(log).Length > 0); i++) System.Threading.Thread.Sleep(100);
                Assert.True(File.Exists(log) && new FileInfo(log).Length > 0, "hostname's output reaches the log through cmd's redirection");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            }
        }

        [Fact]
        public void AnArgumentWithAMetacharacterRefusesTheStartInTheRealLauncherBeforeAnyProcessExists()
        {
            if (!OperatingSystem.IsWindows()) return;
            LaunchSpec spec = Spec("--out", "a&b");
            Assert.Throws<ArgumentException>(() => new ProcessRunLauncher().Start(spec));
        }
    }

    public class CapGrantTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private static string Launch(string name, int threads) =>
            Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name, threads, reason = "a test" });

        private void RunningRecord(string folder)
        {
            Directory.CreateDirectory(folder);
            using (Process self = Process.GetCurrentProcess())
                File.WriteAllText(Path.Combine(folder, "run.json"),
                    "{\"status\":\"running\",\"processId\":" + self.Id + ",\"processStartedAtUtc\":\"" + self.StartTime.ToUniversalTime().ToString("o") + "\"}");
        }

        [Fact]
        public void AfterALaunchTheNextOneCountsWhatWasGranted()
        {
            Fx.Result(_fx.Call("run.launch", Launch("one", 12)));

            OperationResult r = _fx.Call("run.launch", Launch("two", 8));     // the queue still gives 16 free cores; the room granted 12 of them
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("12 threads granted", r.Error);
            Assert.Contains("is 20, more than the 16 free cores (logical 16-31)", r.Error);
            Assert.Single(_fx.Launcher.Started);

            JsonElement fits = Fx.Result(_fx.Call("run.launch", Launch("three", 4)));   // 12 + 4 is exactly the free cores
            Assert.Equal(12, fits.GetProperty("machine").GetProperty("granted").GetInt32());
            Assert.Equal(2, _fx.Launcher.Started.Count);
        }

        [Fact]
        public void ARunStartedLongAgoCountsOnlyWhileItsRecordSaysItRunsAndARecentOneCountsWhateverItsFolderHolds()
        {
            DateTime now = DateTime.UtcNow;
            string running = Path.Combine(_fx.RunsDir, "old-running"), done = Path.Combine(_fx.RunsDir, "old-done"), bare = Path.Combine(_fx.RunsDir, "old-bare"), fresh = Path.Combine(_fx.RunsDir, "fresh");
            RunningRecord(running);
            Directory.CreateDirectory(done);
            File.WriteAllText(Path.Combine(done, "run.json"), "{\"status\":\"completed\"}");
            Directory.CreateDirectory(bare);
            Directory.CreateDirectory(fresh);
            _fx.Room.NoteLaunched("old-running", _fx.Room.MainRelative(running), 7, now.AddMinutes(-10));
            _fx.Room.NoteLaunched("old-done", _fx.Room.MainRelative(done), 5, now.AddMinutes(-10));
            _fx.Room.NoteLaunched("old-bare", _fx.Room.MainRelative(bare), 3, now.AddMinutes(-4));
            Assert.Equal(7, _fx.Room.GrantedThreads(now));                 // only the one still running
            _fx.Room.NoteLaunched("fresh", _fx.Room.MainRelative(fresh), 2, now.AddMinutes(-2));
            Assert.Equal(9, _fx.Room.GrantedThreads(now));                 // a launch of the last 3 minutes counts with no record at all
        }

        [Fact]
        public void TheGrantIsInTheNumbers()
        {
            Slot slot = Cap.SlotOf(QueueReading.Parse(FixedQueue.With(), FixedQueue.File), 0xFFFF0000UL);
            HeatReading ok = HeatReading.Of(HeatReading.Ok, "OK");
            Assert.Null(Cap.Refusal(4, slot, ok, grantedThreads: 12));     // 12 granted and 4 asked: exactly the 16 free
            string both = Cap.Refusal(5, slot, ok, grantedThreads: 12);
            Assert.Contains("5 threads asked, plus 12 threads granted", both);
            Assert.Contains("is 17, more than the 16 free cores", both);
        }

        [Fact]
        public void ALauncherThatCouldNotBreakAwaySaysSoInTheResultAndTheRow()
        {
            _fx.Launcher.Outcome = () => new LaunchOutcome { Pid = 77, Detached = false, Note = "breakaway was refused" };
            JsonElement r = Fx.Result(_fx.Call("run.launch", Launch("inside", 2)));
            Assert.False(r.GetProperty("detached").GetBoolean());
            Assert.Equal("breakaway was refused", r.GetProperty("launcherNote").GetString());
            JsonElement result = _fx.LastRow().GetProperty("result");
            Assert.False(result.GetProperty("detached").GetBoolean());
            Assert.Equal("breakaway was refused", result.GetProperty("launcherNote").GetString());
        }
    }

    public class WorktreeRowTests : IDisposable
    {
        private readonly Fx _fx = Fx.InWorktree();
        public void Dispose() => _fx.Dispose();

        [Fact]
        public void ARowFromAWorktreeNamesItsFolderRelativeToTheMainCheckoutAndCarriesTheTree()
        {
            Assert.Equal("scratch/wt", _fx.Room.TreeRelative);
            JsonElement r = Fx.Result(_fx.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name = "wt-run", threads = 2, reason = "a test" })));
            Assert.Equal("scratch/wt/runs/wt-run", r.GetProperty("folder").GetString());
            Assert.Equal("scratch/wt/runs/wt-run.launch.log", r.GetProperty("launchLog").GetString());
            Assert.Equal("scratch/wt", r.GetProperty("tree").GetString());
            // the command line is the worktree's own: the working directory is its root and --out is relative to it
            LaunchSpec spec = _fx.Launcher.Started.Single();
            Assert.Equal(_fx.Root, spec.WorkingDirectory);
            Assert.Equal(new[] { "--recipe", "rounds/nursery/brain2-w5.json", "--out", "runs/wt-run", "--threads", "2" }, spec.Arguments.Skip(1).ToArray());

            JsonElement row = _fx.LastRow();
            Assert.Equal("scratch/wt", row.GetProperty("tree").GetString());
            Assert.Equal("scratch/wt/runs/wt-run", row.GetProperty("result").GetProperty("folder").GetString());
            Assert.DoesNotContain(_fx.Dir, File.ReadAllText(_fx.Room.Activity.Path));

            // every reader resolves a row against the main checkout: a room started later in the main checkout, and one in the worktree, both find the folder
            string folder = Path.Combine(_fx.Root, "runs", "wt-run");
            var inMain = new RoomContext(_fx.Main, Where.Settings(), _fx.Main, _fx.Registry, _fx.Room.Activity.Path, _fx.Launcher, _fx.Queue, _fx.Heat, 32);
            Assert.Equal(folder, new ResourceResolver(inMain).Catalog(out _).Single(c => c.Id == "wt-run").Folder);
            var inTree = new RoomContext(_fx.Root, Where.Settings(), _fx.Main, _fx.Registry, _fx.Room.Activity.Path, _fx.Launcher, _fx.Queue, _fx.Heat, 32);
            Assert.Equal(folder, new ResourceResolver(inTree).Catalog(out _).Single(c => c.Id == "wt-run").Folder);
            Assert.True(inMain.IsLaunchedFolder(folder));
        }

        [Fact]
        public void ARowFromTheMainCheckoutCarriesADotAndAFolderOutsideTheMainCheckoutIsNeverNamed()
        {
            using (var main = new Fx())
            {
                Assert.Equal(".", main.Room.TreeRelative);
                Fx.Result(main.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name = "m-run", threads = 2, reason = "a test" })));
                Assert.Equal(".", main.LastRow().GetProperty("tree").GetString());
                Assert.Equal("runs/m-run", main.LastRow().GetProperty("result").GetProperty("folder").GetString());
                Assert.Equal("(a path outside the repository)", main.Room.MainRelative(Path.Combine(main.Dir, "elsewhere")));
                Assert.Null(main.Room.ResolveFromMain("../up"));
                Assert.Null(main.Room.ResolveFromMain("C:/x"));
                Assert.Null(main.Room.ResolveFromMain("a/../../up"));
            }
        }

        [Fact]
        public void ARowThatNamesAFolderAboveTheMainCheckoutIsNeverTakenForARun()
        {
            // a tampered log must not make the room write a STOP file anywhere it likes
            _fx.Room.Activity.Append(new { timeUtc = DateTime.UtcNow.ToString("o"), operation = "run.launch", args = new { name = "evil", threads = 1 }, outcome = "ok", result = new { folder = "../../../elsewhere" } });
            Assert.DoesNotContain(new ResourceResolver(_fx.Room).Catalog(out _), c => c.Id == "evil");
        }
    }

    public class ReplaceGuardTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private void Enter(string recipePath, string name = "zz-entry") =>
            RegistryEntry.Append(_fx.Registry, new RegistryEntry
            {
                Recipe = name, RecipeHash = "h", RecipePath = recipePath, Out = Path.Combine(_fx.Dir, "out-" + name), Status = "completed",
                StartedAtUtc = "2026-10-02T00:00:00Z", EndedAtUtc = "2026-10-02T01:00:00Z",
            });

        private string Write(string path, string text = null, string expect = null) => _fx.Json(path, "a test", text, expect);

        [Fact]
        public void ARecipeARegistryEntryNamesIsNeverReplacedAndANewFileIsTheWay()
        {
            string path = _fx.RecipePath();
            Fx.Result(_fx.Call("recipe.write", Write(path)));
            string hash = RunRecord.FileSha256(_fx.Full(path));
            string before = File.ReadAllText(_fx.Full(path));
            Enter(path);

            OperationResult r = _fx.Call("recipe.write", Write(path, _fx.Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"zz-edited\""), hash));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("registry entry 1", r.Error);
            Assert.Contains("cannot be replaced", r.Error);
            Assert.Contains("new file", r.Error);
            Assert.Equal(before, File.ReadAllText(_fx.Full(path)));
            JsonElement row = _fx.LastRow();
            Assert.Equal("refused", row.GetProperty("outcome").GetString());
            Assert.Contains("registry entry 1", row.GetProperty("detail").GetString());

            // creating a new file stays allowed, one that starts from the old as its base too
            Fx.Result(_fx.Call("recipe.write", Write(_fx.RecipePath())));
            string child = _fx.RecipePath();
            Fx.Result(_fx.Call("recipe.write", Write(child, "{\"base\":\"" + path + "\",\"name\":\"zz-child\"}")));
        }

        [Fact]
        public void ARecipeInTheBaseChainOfARegisteredOneIsNeverReplacedEither()
        {
            string a = _fx.RecipePath(), b = _fx.RecipePath(), c = _fx.RecipePath(), unrelated = _fx.RecipePath();
            Fx.Result(_fx.Call("recipe.write", Write(a)));
            Fx.Result(_fx.Call("recipe.write", Write(b, "{\"base\":\"" + a + "\",\"name\":\"zz-b\"}")));
            Fx.Result(_fx.Call("recipe.write", Write(c, "{\"base\":\"" + b + "\",\"name\":\"zz-c\"}")));
            Fx.Result(_fx.Call("recipe.write", Write(unrelated)));
            Enter(c, "zz-c");

            foreach (string dependedOn in new[] { a, b })
            {
                string hash = RunRecord.FileSha256(_fx.Full(dependedOn));
                OperationResult r = _fx.Call("recipe.write", Write(dependedOn, _fx.Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"zz-edited\""), hash));
                Assert.False(r.Ok, dependedOn);
                Assert.Contains("registry entry 1", r.Error);
                Assert.Contains("base chain", r.Error);
                Assert.Equal(hash, RunRecord.FileSha256(_fx.Full(dependedOn)));
            }
            // a file nothing registered depends on is replaced as before
            JsonElement ok = Fx.Result(_fx.Call("recipe.write", Write(unrelated, _fx.Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"zz-free\""), RunRecord.FileSha256(_fx.Full(unrelated)))));
            Assert.False(ok.GetProperty("created").GetBoolean());
        }

        [Fact]
        public void AnUnreadableRegistryRefusesAReplaceButNotANewFile()
        {
            string path = _fx.RecipePath();
            Fx.Result(_fx.Call("recipe.write", Write(path)));
            string hash = RunRecord.FileSha256(_fx.Full(path));
            File.WriteAllText(_fx.Registry, "not json\n");
            OperationResult r = _fx.Call("recipe.write", Write(path, _fx.Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"zz-edited\""), hash));
            Assert.False(r.Ok);
            Assert.Contains("registry cannot be read", r.Error);
            Fx.Result(_fx.Call("recipe.write", Write(_fx.RecipePath())));
        }
    }

    public class ActivityHardeningTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        [Fact]
        public void FreeTextLosesUncAndRootedPathsAsWellAsDrivePathsAndKeepsWhatIsNotAPath()
        {
            string cleaned = ActivityLog.Clean(@"a \\host\share\x b //srv/share/y c /etc/passwd d C:\Users\x e C:/x f rounds/nursery/a.json g http://h/x h and/or i 1/2 j", 500);
            foreach (string gone in new[] { "host", "srv", "etc", "passwd", "Users", "C:" })
                Assert.DoesNotContain(gone, cleaned);
            foreach (string kept in new[] { "rounds/nursery/a.json", "http://h/x", "and/or", "1/2" })
                Assert.Contains(kept, cleaned);
        }

        [Fact]
        public void TwoLogsOnOneFileAppendedFromManyThreadsKeepEveryRowWhole()
        {
            Assert.StartsWith("Global\\", ActivityLog.GlobalMutexName);
            Assert.StartsWith("Local\\", ActivityLog.LocalMutexName);
            string file = Path.Combine(_fx.Dir, "two.jsonl");
            var a = new ActivityLog(file);
            var b = new ActivityLog(file);
            var tasks = Enumerable.Range(0, 8).Select(t => System.Threading.Tasks.Task.Run(() =>
            {
                ActivityLog log = t % 2 == 0 ? a : b;
                for (int i = 0; i < 40; i++) log.Append(new { t, i, pad = new string('x', 300) });
            })).ToArray();
            System.Threading.Tasks.Task.WaitAll(tasks);
            List<JsonElement> rows = a.ReadNewestFirst(int.MaxValue, out int total, out int unreadable);
            Assert.Equal(320, total);
            Assert.Equal(0, unreadable);
            Assert.Equal(320, rows.Count);
        }

        [Fact]
        public void TheLaunchedRunsAreReadFromTheLogAgainOnlyWhenItsLengthOrWriteTimeChanged()
        {
            _fx.Room.Activity.Append(new { timeUtc = DateTime.UtcNow.ToString("o"), operation = "run.launch", args = new { name = "aaaa", threads = 2 }, outcome = "ok", result = new { folder = "runs/aaaa" } });
            Assert.Equal("aaaa", _fx.Room.LaunchedRuns().Single().Name);
            string path = _fx.Room.Activity.Path;
            DateTime written = File.GetLastWriteTimeUtc(path);
            // the same length and the same write time: what is served is the parse already made
            File.WriteAllText(path, File.ReadAllText(path).Replace("aaaa", "bbbb"));
            File.SetLastWriteTimeUtc(path, written);
            Assert.Equal("aaaa", _fx.Room.LaunchedRuns().Single().Name);
            // a changed write time reads it again
            File.SetLastWriteTimeUtc(path, written.AddSeconds(5));
            Assert.Equal("bbbb", _fx.Room.LaunchedRuns().Single().Name);
        }
    }

    public class McpAndScreenTests
    {
        [Fact]
        public void TheNewActionsAreInTheirFamiliesAndNoToolWasAdded()
        {
            using var fx = new Fx();
            var tools = McpSurface.Tools(fx.Ops);
            // the episode family, with its one action, is the tank screen's (phase 3); the recipe and run families took no new tool in phase 2
            Assert.Equal(new[] { "recipe", "run", "episode", "compare", "describe" }, tools.Select(t => t.Name).ToArray());
            string[] Actions(string tool) => tools.First(t => t.Name == tool).InputSchema.GetProperty("properties").GetProperty("action").GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Equal(new[] { "check", "write", "diff" }, Actions("recipe"));
            Assert.Equal(new[] { "launch", "rerun", "stop" }, Actions("run"));
            Assert.Equal(new[] { "step" }, Actions("episode"));
            Assert.Equal(new[] { "recipe.check", "recipe.write", "recipe.diff", "run.launch", "run.rerun", "run.stop", "episode.step", "compare", "describe" }, fx.Ops.All.Select(o => o.Id).ToArray());
        }

        [Fact]
        public void DescribeReturnsTheSchemasOfTheWrites()
        {
            using var fx = new Fx();
            JsonElement d = Fx.Result(fx.Call("describe", "{\"family\":\"run\"}"));
            JsonElement launch = d.GetProperty("operations").EnumerateArray().Single(o => o.GetProperty("id").GetString() == "run.launch");
            Assert.True(launch.GetProperty("mutates").GetBoolean());
            Assert.Equal(new[] { "recipe", "name", "threads" }, launch.GetProperty("schema").GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray());
            JsonElement all = Fx.Result(fx.Call("describe", "{}"));
            JsonElement write = all.GetProperty("operations").EnumerateArray().Single(o => o.GetProperty("id").GetString() == "recipe.write");
            Assert.Contains("expectHash", write.GetProperty("schema").GetProperty("properties").EnumerateObject().Select(p => p.Name));
            Assert.False(all.GetProperty("operations").EnumerateArray().Single(o => o.GetProperty("id").GetString() == "recipe.diff").GetProperty("mutates").GetBoolean());
            Assert.False(all.GetProperty("operations").EnumerateArray().Single(o => o.GetProperty("id").GetString() == "compare").GetProperty("mutates").GetBoolean());
        }

        [Fact]
        public void ARunLaunchedThroughTheMcpIsRecordedAsMcp()
        {
            using var fx = new Fx();
            var args = new Dictionary<string, JsonElement>
            {
                ["action"] = Js.Parse("\"launch\""), ["recipe"] = Js.Parse("\"rounds/nursery/brain2-w5.json\""), ["name"] = Js.Parse("\"by-agent\""),
                ["threads"] = Js.Parse("2"), ["reason"] = Js.Parse("\"an agent's trial\""),
            };
            var result = McpSurface.Call(fx.Ops, "run", args);
            Assert.NotEqual(true, result.IsError);
            JsonElement row = fx.LastRow();
            Assert.Equal("mcp", row.GetProperty("who").GetString());
            Assert.Equal("an agent's trial", row.GetProperty("reason").GetString());
            Assert.Single(fx.Launcher.Started);
        }

        [Fact]
        public void TheCommittedListOfAppendedFilesHoldsTheRegistryAndTheActivityLogRoomJsonNames()
        {
            RoomSettings s = RoomSettings.Load(Path.Combine(Where.Root, "rounds", "nursery", "room.json"));
            IReadOnlyList<string> listed = RunRecord.AppendedPaths(Where.Root);
            Assert.Contains(RegistryEntry.RelativePath, listed);
            Assert.Contains(s.ActivityFile, listed);
            Assert.Equal("rounds/nursery/activity.jsonl", s.ActivityFile);
            // and the file the room starts from is checked against it
            var room = new RoomContext(Where.Root, s, Where.Root);
            Program.CheckActivityFileIsListed(room);
        }

        [Fact]
        public void AnActivityFileOffTheListRefusesTheRoomNamingBothFiles()
        {
            RoomSettings off = RoomSettings.Parse(Where.SettingsJson(activityFile: "rounds/nursery/other-log.jsonl"));
            var room = new RoomContext(Where.Root, off, Where.Root);
            var e = Assert.Throws<InvalidDataException>(() => Program.CheckActivityFileIsListed(room));
            Assert.Contains("rounds/nursery/other-log.jsonl", e.Message);
            Assert.Contains("appended.txt", e.Message);
        }

        [Fact]
        public void ANurseryRunsDirtyCheckLeavesOutTheActivityLogWhichIsOnTheList()
        {
            // the exclusions the nursery builds are the list's, so the activity log cannot make a tree dirty
            Assert.Contains("rounds/nursery/activity.jsonl", RunRecord.AppendedPaths(Where.Root));
        }
    }
}
