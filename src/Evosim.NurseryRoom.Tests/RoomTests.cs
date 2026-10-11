using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>Where the tests find the repository and write their synthetic files: inside the build folder, which is inside the repository.</summary>
    internal static class Where
    {
        public static readonly string Root = RoomContext.FindRoot(AppContext.BaseDirectory);
        public static string WwwRoot => Path.Combine(Root, "src", "Evosim.NurseryRoom", "wwwroot");

        public static string Fresh(string name)
        {
            string d = Path.Combine(AppContext.BaseDirectory, name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        /// <summary>
        /// The room's settings as JSON: every key the room requires, the ones a test varies given. Its heat script is a file that is not there, so that a room
        /// a test makes without a heat reader of its own reads the heat as unreadable and never runs the real script.
        /// </summary>
        public static string SettingsJson(string runs = "", string extra = "", string bind = "127.0.0.1", string runsDir = "scratch/nursery", string activityFile = "rounds/nursery/activity.jsonl", string affinity = "0xFFFF0000", string trialsDir = "scratch/nursery-trials", string queueFile = "scratch/machine/queue.md") =>
            "{\"port\":5179,\"bind\":\"" + bind + "\",\"pollSeconds\":30,\"runs\":[" + runs + "]," +
            "\"dotnet\":\"dotnet\",\"nurseryDll\":\"artifacts/Evosim.Nursery/bin/Debug/net8.0/Evosim.Nursery.dll\",\"runsDir\":\"" + runsDir + "\",\"activityFile\":\"" + activityFile + "\",\"affinity\":\"" + affinity + "\",\"trialsDir\":\"" + trialsDir + "\"," +
            "\"queueFile\":\"" + queueFile + "\",\"heatScript\":\"" + NoHeatScript + "\"" + extra + "}";

        /// <summary>The heat script the tests' settings name: not there, so the real one is never run.</summary>
        public const string NoHeatScript = "scripts/no-heat-check-in-the-room-tests.ps1";

        public static RoomSettings Settings(params string[] runs) => RoomSettings.Parse(SettingsJson(string.Join(",", runs.Select(r => "\"" + r + "\""))));
    }

    public class SettingsTests
    {
        [Fact]
        public void AnUnknownKeyIsRefusedByName()
        {
            var e = Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(Where.SettingsJson(extra: ",\"theme\":\"dark\"")));
            Assert.Contains("unknown theme", e.Message);
        }

        [Fact]
        public void AMissingKeyIsRefusedByName()
        {
            var e = Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(Where.SettingsJson().Replace("\"pollSeconds\":30,", "")));
            Assert.Contains("missing pollSeconds", e.Message);
        }

        [Fact]
        public void OnlyLoopbackIsAccepted()
        {
            Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(Where.SettingsJson(bind: "0.0.0.0")));
        }

        [Fact]
        public void ARootedRunFolderIsRefused()
        {
            Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(Where.SettingsJson("\"D:/x\"")));
        }

        [Fact]
        public void TheCommittedSettingsLoad()
        {
            RoomSettings s = RoomSettings.Load(Path.Combine(Where.Root, "rounds", "nursery", "room.json"));
            Assert.Equal(5179, s.Port);
            Assert.Equal("127.0.0.1", s.Bind);
            Assert.Equal(30, s.PollSeconds);
            Assert.Contains("scratch/nursery/pilot-deep-brain2", s.Runs);
            Assert.Equal("rounds/nursery/activity.jsonl", s.ActivityFile);
            Assert.Equal("scratch/nursery", s.RunsDir);
            Assert.Equal(0xFFFFFFCFUL, s.Affinity);   // every processor but 4 and 5: the bound the room never leaves
            Assert.Equal("scratch/machine/queue.md", s.QueueFile);
            Assert.Equal("scripts/temp-check.ps1", s.HeatScript);
            Assert.False(string.IsNullOrEmpty(s.Dotnet));
            Assert.False(string.IsNullOrEmpty(s.NurseryDll));
        }

        [Theory]
        [InlineData("dotnet")]
        [InlineData("nurseryDll")]
        [InlineData("runsDir")]
        [InlineData("activityFile")]
        [InlineData("affinity")]
        [InlineData("trialsDir")]
        [InlineData("queueFile")]
        [InlineData("heatScript")]
        public void EachNewKeyIsRequiredAndNamedWhenMissing(string key)
        {
            using var doc = JsonDocument.Parse(Where.SettingsJson());
            var kept = doc.RootElement.EnumerateObject().Where(p => p.Name != key).Select(p => "\"" + p.Name + "\":" + p.Value.GetRawText());
            var e = Assert.Throws<InvalidDataException>(() => RoomSettings.Parse("{" + string.Join(",", kept) + "}"));
            Assert.Contains("missing " + key, e.Message);
        }

        [Theory]
        [InlineData("0x0")]
        [InlineData("0x00000000")]
        [InlineData("FFFF0000")]          // no 0x
        [InlineData("0xFFFF00000000000000")]   // more than 64 bits
        [InlineData("0xFFFG")]
        [InlineData("")]
        [InlineData("-1")]
        [InlineData("0x")]
        public void AnAffinityThatIsNotANonZeroHexMaskIsRefusedByName(string bad)
        {
            var e = Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(Where.SettingsJson(affinity: bad)));
            Assert.Contains("affinity", e.Message);
            Assert.Contains("room.json", e.Message);   // the settings file is named, as the other keys' refusals name it
            e = Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(Where.SettingsJson(affinity: bad), "some/other-room.json"));
            Assert.Contains("some/other-room.json", e.Message);
        }

        [Theory]
        [InlineData("../x")]
        [InlineData("/abs/trials")]
        [InlineData("D:/trials")]
        [InlineData("rounds/nursery")]
        [InlineData("rounds/nursery/trials")]
        [InlineData("a b")]
        [InlineData("a\\\"b")]
        [InlineData("trials.")]
        [InlineData("")]
        public void ATrialsFolderThatIsOutsideThePlainRulesIsRefusedByName(string bad)
        {
            var e = Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(Where.SettingsJson(trialsDir: bad)));
            Assert.Contains("trialsDir", e.Message);
            Assert.Contains("room.json", e.Message);
        }

        [Fact]
        public void ATrialsFolderIsReadRelativeAndWithForwardSlashes()
        {
            Assert.Equal("scratch/nursery-trials", RoomSettings.Parse(Where.SettingsJson()).TrialsDir);
            Assert.Equal("scratch/t/x", RoomSettings.Parse(Where.SettingsJson(trialsDir: "scratch\\\\t\\\\x\\\\")).TrialsDir);
        }

        [Fact]
        public void AnAffinityIsReadAsHexAndWrittenBackTheSameWay()
        {
            Assert.Equal(0xFFFF0000UL, RoomSettings.Parse(Where.SettingsJson()).Affinity);
            Assert.Equal(0x3UL, RoomSettings.Parse(Where.SettingsJson(affinity: "0X3")).Affinity);
            Assert.Equal(ulong.MaxValue, RoomSettings.Parse(Where.SettingsJson(affinity: "0xffffffffffffffff")).Affinity);
            Assert.Equal("0xFFFF0000", CoreMask.Format(0xFFFF0000UL));
        }

        [Fact]
        public void AMaskIsCheckedAgainstTheMachinesProcessors()
        {
            Assert.Null(CoreMask.Problem(0xFFFF0000UL, 0xFFFFFFFFUL));
            Assert.Null(CoreMask.Problem(ulong.MaxValue, ulong.MaxValue));
            Assert.Contains("lacks", CoreMask.Problem(0x1_0000_0000UL, 0xFFFFFFFFUL));
            Assert.Contains("processor(s) 16, 17", CoreMask.Problem(0x00030000UL, 0xFFFFUL));
            Assert.Contains("0", CoreMask.Problem(0UL, 0xFFFFFFFFUL));
            Assert.Contains("unknown", CoreMask.Problem(0x1UL, 0UL));
            // a machine whose processors are not the lowest ones (a system mask with a gap): a mask is checked against the processors it has
            Assert.Null(CoreMask.Problem(0xF0UL, 0xFFUL));
            Assert.Contains("processor(s) 0", CoreMask.Problem(0x1UL, 0xFFFF0000UL));
        }

        [Fact]
        public void AMaskAllowsAsManyThreadsAsItNamesProcessors()
        {
            Assert.Equal(16, CoreMask.Count(0xFFFF0000UL));
            Assert.Null(CoreMask.ThreadsProblem(16, 0xFFFF0000UL));
            string over = CoreMask.ThreadsProblem(17, 0xFFFF0000UL);
            Assert.Contains("17 threads", over);
            Assert.Contains("allows 16 processors", over);
            Assert.Contains("0xFFFF0000", over);
        }

        [Theory]
        [InlineData("runsDir", "../elsewhere")]
        [InlineData("runsDir", "D:/runs")]
        [InlineData("activityFile", "/etc/activity.jsonl")]
        [InlineData("activityFile", "a/../../b.jsonl")]
        public void AFolderOrFileOutsideTheRepositoryIsRefusedByName(string key, string value)
        {
            string json = key == "runsDir" ? Where.SettingsJson(runsDir: value) : Where.SettingsJson(activityFile: value);
            Assert.Contains(key, Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(json)).Message);
        }
    }

    public class ReaderTests : IDisposable
    {
        private readonly string _dir = Where.Fresh("readers");
        public void Dispose() => Directory.Delete(_dir, true);

        private string P(string name) => Path.Combine(_dir, name);

        [Fact]
        public void APartialLastLineIsNotReadUntilItIsComplete()
        {
            File.WriteAllText(P("a.jsonl"), "{\"gen\":0}\n{\"gen\":1}\n{\"gen\":2");
            var rows = new LiveRows(P("a.jsonl"));
            rows.Poll();
            Assert.Equal(2, rows.Rows.Count);
            Assert.Equal(0, rows.Bad);
            // the writer finishes the line: it is read whole on the next poll
            File.AppendAllText(P("a.jsonl"), "}\n");
            List<JsonElement> added = rows.Poll();
            Assert.Single(added);
            Assert.Equal(3, rows.Rows.Count);
            Assert.Equal(2, rows.Rows[2].GetProperty("gen").GetInt32());
        }

        [Fact]
        public void AFileGrownBetweenTwoReadsGivesOnlyTheNewRows()
        {
            File.WriteAllText(P("g.jsonl"), "{\"gen\":0}\n");
            var rows = new LiveRows(P("g.jsonl"));
            Assert.Single(rows.Poll());
            long first = rows.Offset;
            Assert.Empty(rows.Poll());                      // nothing added: nothing read
            File.AppendAllText(P("g.jsonl"), "{\"gen\":1}\n{\"gen\":2}\n");
            List<JsonElement> added = rows.Poll();
            Assert.Equal(2, added.Count);
            Assert.Equal(3, rows.Rows.Count);
            Assert.True(rows.Offset > first);
            Assert.Equal(new FileInfo(P("g.jsonl")).Length, rows.Offset);
        }

        [Fact]
        public void AFileHeldOpenForWritingIsReadable()
        {
            using (var w = new FileStream(P("live.jsonl"), FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                byte[] b = System.Text.Encoding.UTF8.GetBytes("{\"gen\":0}\n{\"gen\":1}\n");
                w.Write(b, 0, b.Length);
                w.Flush();
                var rows = new LiveRows(P("live.jsonl"));
                rows.Poll();
                Assert.Equal(2, rows.Rows.Count);
                Assert.Null(rows.Problem);
            }
        }

        [Fact]
        public void ABadLineIsCountedAndTheRowsAfterItAreStillRead()
        {
            File.WriteAllText(P("b.jsonl"), "{\"gen\":0}\nnot json\n\r\n{\"gen\":1}\r\n");
            var rows = new LiveRows(P("b.jsonl"));
            rows.Poll();
            Assert.Equal(2, rows.Rows.Count);
            Assert.Equal(1, rows.Bad);
        }

        [Fact]
        public void AFileThatIsReplacedByAShorterOneStartsOver()
        {
            File.WriteAllText(P("r.jsonl"), "{\"gen\":0}\n{\"gen\":1}\n{\"gen\":2}\n");
            var rows = new LiveRows(P("r.jsonl"));
            rows.Poll();
            File.WriteAllText(P("r.jsonl"), "{\"gen\":9}\n");
            rows.Poll();
            Assert.Single(rows.Rows);
            Assert.Equal(9, rows.Rows[0].GetProperty("gen").GetInt32());
        }

        [Fact]
        public void AMissingFileIsNotAnErrorAndAppearsLater()
        {
            var rows = new LiveRows(P("later.jsonl"));
            Assert.Empty(rows.Poll());
            Assert.False(rows.Exists);
            File.WriteAllText(P("later.jsonl"), "{\"gen\":0}\n");
            Assert.Single(rows.Poll());
        }

        [Fact]
        public void ARunFolderReadsEveryFileAndLeavesWhatIsAbsentNull()
        {
            // the files of a run in its search: no run.json, no recipe.json, no scores, no exam
            File.WriteAllText(P("tank-generations.jsonl"),
                "{\"gen\":0,\"seconds\":100,\"netMean\":10,\"netBest\":20,\"top\":{\"score\":20,\"body\":3}}\n" +
                "{\"gen\":1,\"seconds\":220,\"netMean\":12,\"netBest\":25,\"top\":{\"score\":25,\"body\":1}}\n" +
                "{\"gen\":2,\"seconds\":340,\"netMean\":14,\"netBest\":");   // unfinished
            File.WriteAllText(P("tank-crossovers.jsonl"), "{\"transferred\":true,\"baseBetter\":true,\"refused\":null,\"d\":0.5,\"mutated\":true,\"end\":\"sense\"}\n{\"transferred\":false,\"baseBetter\":false,\"refused\":\"x\",\"d\":0.1,\"mutated\":false,\"end\":\"loop\"}\n");
            File.WriteAllText(P("nursery.log"), "one\ntwo\n");
            File.WriteAllText(P("tank-best.jsonl"), "{\"nodes\":[{},{}],\"brain\":{\"outputs\":[{}],\"hidden\":[{},{}],\"links\":[{},{},{}]}}\n");

            var f = new RunFolder(_dir);
            f.Refresh();
            Dictionary<string, object> s = f.Snapshot(DateTime.UtcNow);
            Assert.Equal(2, f.Generations.Count);                       // the third line is not complete
            Assert.Null(s["run"]);
            Assert.Null(s["recipe"]);
            Assert.Empty((System.Collections.IEnumerable)s["scores"]);
            Assert.Null(s["exam"]);
            string json = Js.Serialize(s);
            using JsonDocument d = JsonDocument.Parse(json);
            JsonElement root = d.RootElement;
            Assert.Equal("unrecorded", root.GetProperty("state").GetProperty("phase").GetString());
            Assert.Equal(1, root.GetProperty("progress").GetProperty("lastGen").GetInt32());
            Assert.Equal(2.0, root.GetProperty("progress").GetProperty("paceMinutes").GetDouble(), 6);   // 120 s a generation
            Assert.Equal(2, root.GetProperty("crossovers").GetProperty("rows").GetInt32());
            Assert.Equal(1, root.GetProperty("crossovers").GetProperty("transferred").GetInt32());
            Assert.Equal(0.3, root.GetProperty("crossovers").GetProperty("meanDistance").GetDouble(), 6);
            Assert.Equal(3, root.GetProperty("best").GetProperty("last").GetProperty("links").GetInt32());
            Assert.Equal(2, root.GetProperty("log").GetProperty("lines").GetArrayLength());

            // the nursery finishes the line and writes a row to the log
            File.AppendAllText(P("tank-generations.jsonl"), "20}\n");
            File.AppendAllText(P("nursery.log"), "three\n");
            f.Refresh();
            Assert.Equal(3, f.Generations.Count);
            Assert.Equal(3, f.LogTail.Count);
        }

        [Fact]
        public void ARecipeAndARunRecordGiveThePlannedGenerationsAndThePhase()
        {
            File.WriteAllText(P("recipe.json"), "{\"name\":\"x\",\"description\":\"d\",\"search\":{\"generations\":100},\"reward\":{\"class\":\"Net\"},\"exam\":{\"controls\":[{\"kind\":\"own\"},{\"kind\":\"frozen\"}]}}");
            File.WriteAllText(P("run.json"), "{\"status\":\"completed\",\"startedAtUtc\":\"2026-10-02T10:00:00Z\"}");
            File.WriteAllText(P("tank-generations.jsonl"), "{\"gen\":0,\"seconds\":60}\n");
            File.WriteAllText(P("tank-exam.jsonl"), "{\"brain\":\"own\",\"genome\":0,\"body\":0,\"net\":1,\"per\":[1,2]}\n");
            var f = new RunFolder(_dir);
            f.Refresh();
            using JsonDocument d = JsonDocument.Parse(Js.Serialize(f.Snapshot(DateTime.UtcNow)));
            JsonElement p = d.RootElement.GetProperty("progress");
            Assert.Equal(100, p.GetProperty("planned").GetInt32());
            Assert.Equal(1, p.GetProperty("examBrains").GetInt32());
            Assert.Equal(2, p.GetProperty("examExpected").GetInt32());
            Assert.Equal("completed", d.RootElement.GetProperty("state").GetProperty("phase").GetString());
            Assert.Equal("x", d.RootElement.GetProperty("name").GetString());
        }

        [Fact]
        public void TheSnapshotAndTheListsOfARunFolderAreCopies()
        {
            File.WriteAllText(P("tank-generations.jsonl"), "{\"gen\":0,\"seconds\":60}\n{\"gen\":1,\"seconds\":120}\n");
            File.WriteAllText(P("nursery.log"), "one\n");
            File.WriteAllText(P("tank-crossovers.jsonl"), "{\"transferred\":true,\"end\":\"sense\"}\n");
            var f = new RunFolder(_dir);
            f.Refresh();
            Dictionary<string, object> s = f.Snapshot(DateTime.UtcNow);
            var gens = (System.Collections.IList)s["generations"];
            IReadOnlyList<JsonElement> listed = f.Generations;
            IReadOnlyCollection<string> tail = f.LogTail;
            Assert.Equal(2, gens.Count);
            // the nursery writes more, and the room reads it, while those are still held
            File.AppendAllText(P("tank-generations.jsonl"), "{\"gen\":2,\"seconds\":180}\n");
            File.AppendAllText(P("nursery.log"), "two\n");
            File.AppendAllText(P("tank-crossovers.jsonl"), "{\"transferred\":false,\"end\":\"loop\"}\n");
            f.Refresh();
            Assert.Equal(2, gens.Count);
            Assert.Equal(2, listed.Count);
            Assert.Single(tail);
            Assert.Equal(3, f.Generations.Count);
            Assert.NotSame(f.Generations, f.Generations);
            // the crossover totals in an earlier snapshot do not change under it either
            using JsonDocument first = JsonDocument.Parse(Js.Serialize(s));
            Assert.Single(first.RootElement.GetProperty("crossovers").GetProperty("ends").EnumerateObject());
        }

        [Fact]
        public void ARunFolderReadWhileItIsWrittenNeverThrows()
        {
            string gen = P("tank-generations.jsonl");
            File.WriteAllText(gen, "");
            var f = new RunFolder(_dir);
            var failure = new List<Exception>();
            using var stop = new System.Threading.ManualResetEventSlim();
            var writer = new System.Threading.Thread(() =>
            {
                for (int i = 0; i < 400 && !stop.IsSet; i++)
                {
                    File.AppendAllText(gen, "{\"gen\":" + i + ",\"seconds\":" + (i * 60) + ",\"top\":{\"score\":" + i + "}}\n");
                    f.Refresh();
                }
            });
            writer.Start();
            try
            {
                for (int i = 0; i < 150; i++)
                    Js.Serialize(f.Snapshot(DateTime.UtcNow));
            }
            catch (Exception e) { failure.Add(e); }
            stop.Set();
            writer.Join();
            Assert.Empty(failure);
        }

        [Fact]
        public void ALargeDeltaIsReadInChunksWithLinesCarriedAcrossThem()
        {
            // 30 000 lines of about 100 bytes: three megabytes, so lines straddle the 1 MB chunks
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 30000; i++) sb.Append("{\"gen\":").Append(i).Append(",\"pad\":\"").Append('x', 70).Append("\"}\n");
            sb.Append("{\"gen\":30000,\"pa");   // unfinished
            File.WriteAllText(P("big.jsonl"), sb.ToString());
            Assert.True(new FileInfo(P("big.jsonl")).Length > 2 * LiveLines.ChunkBytes);
            var rows = new LiveRows(P("big.jsonl"));
            rows.Poll();
            Assert.Equal(30000, rows.Rows.Count);
            Assert.Equal(0, rows.Bad);
            Assert.Equal(29999, rows.Rows[29999].GetProperty("gen").GetInt32());
            for (int i = 0; i < 30000; i += 997) Assert.Equal(i, rows.Rows[i].GetProperty("gen").GetInt32());
            File.AppendAllText(P("big.jsonl"), "d\":\"\"}\n");
            Assert.Single(rows.Poll());
        }

        [Fact]
        public void AFileWhoseCreationTimeChangedIsTakenAsReplaced()
        {
            File.WriteAllText(P("c.jsonl"), "{\"gen\":0}\n{\"gen\":1}\n");
            var lines = new LiveLines(P("c.jsonl"));
            Assert.Equal(2, lines.Poll().Lines.Count);
            // written again, and longer, with a creation time of its own
            File.WriteAllText(P("c.jsonl"), "{\"gen\":7}\n{\"gen\":8}\n{\"gen\":9}\n");
            File.SetCreationTimeUtc(P("c.jsonl"), DateTime.UtcNow.AddHours(-3));
            LiveLines.PollResult r = lines.Poll();
            Assert.True(r.Reset);
            Assert.Equal(3, r.Lines.Count);
            Assert.Contains("\"gen\":7", r.Lines[0]);
        }

        [Fact]
        public void TheChangeKeyCarriesThePhaseAndWhetherTheProcessIsAlive()
        {
            File.WriteAllText(P("tank-generations.jsonl"), "{\"gen\":0,\"seconds\":60}\n");
            var me = System.Diagnostics.Process.GetCurrentProcess();
            string started = me.StartTime.ToUniversalTime().ToString("o");
            File.WriteAllText(P("run.json"), "{\"status\":\"running\",\"processId\":" + me.Id + ",\"processStartedAtUtc\":\"" + started + "\"}");
            var f = new RunFolder(_dir);
            f.Refresh();
            string live = (string)f.Snapshot(DateTime.UtcNow, full: false)["version"];
            Assert.EndsWith("-search-alive", live);
            // the process dies: no file changes, and the key still does
            File.WriteAllText(P("run.json"), "{\"status\":\"running\",\"processId\":2147480000,\"processStartedAtUtc\":\"" + started + "\"}");
            File.SetLastWriteTimeUtc(P("run.json"), File.GetLastWriteTimeUtc(P("run.json")));
            f.Refresh();
            string dead = (string)f.Snapshot(DateTime.UtcNow, full: false)["version"];
            Assert.EndsWith("-killed-gone", dead);
            Assert.NotEqual(live, dead);
        }

        [Fact]
        public void ARunningRecordWhoseProcessIsGoneReadsAsKilled()
        {
            Assert.Equal("killed", RunFolder.Phase("running", false, false, true, true));
            Assert.Equal("exam", RunFolder.Phase("running", true, true, true, true));
            Assert.Equal("search", RunFolder.Phase("running", null, false, true, true));
            Assert.Equal("unrecorded", RunFolder.Phase(null, null, false, true, false));
        }
    }

    public class ExamTests
    {
        private static JsonElement Row(string brain, int genome, double net, double? score, double[] per, double? fresh = null) =>
            Js.Parse($"{{\"brain\":\"{brain}\",\"genome\":{genome},\"body\":{genome * 2},\"net\":{net},\"per\":[{string.Join(",", per)}]" +
                (score.HasValue ? $",\"score\":{score}" : "") + (fresh.HasValue ? $",\"fresh\":{fresh}" : "") + "}");

        [Fact]
        public void CellsArePairedDrawByDrawWithTheirStandardError()
        {
            var rows = new[]
            {
                Row("own", 0, 10, null, new[] { 10.0, 12, 14, 8 }),
                Row("knockout X", 0, 6, null, new[] { 5.0, 7, 9, 3 }),     // own less it: 5 each, no spread
                Row("frozen", 0, 7, null, new[] { 9.0, 11, 10, 4 }),       // 1, 1, 4, 4
            };
            ExamTable.Result r = ExamTable.Build(rows);
            Assert.Equal(new[] { "own", "knockout X", "frozen" }, r.Brains);   // in the file's order
            Assert.Equal("per", r.Basis);
            ExamTable.Cell ko = r.Bodies[0].Cells[1], fz = r.Bodies[0].Cells[2];
            Assert.Equal(5.0, ko.Diff.Value, 9);
            Assert.Equal(0.0, ko.Se.Value, 9);
            Assert.True(ko.Clears);
            Assert.Equal(2.5, fz.Diff.Value, 9);
            Assert.Equal(Math.Sqrt(3.0 / 4), fz.Se.Value, 9);   // sample variance 3, over 4 draws
            Assert.Equal(4, fz.N);
            Assert.True(fz.Clears);
            Assert.Null(r.Bodies[0].Cells[0].Diff);
        }

        [Fact]
        public void WithoutPerTheScoreIsUsedAndHasNoStandardError()
        {
            var rows = new[]
            {
                Js.Parse("{\"brain\":\"own\",\"genome\":0,\"body\":0,\"net\":9,\"score\":9}"),
                Js.Parse("{\"brain\":\"b\",\"genome\":0,\"body\":0,\"net\":5,\"score\":4}"),
            };
            ExamTable.Result r = ExamTable.Build(rows);
            Assert.Equal("score", r.Basis);
            Assert.Equal(5.0, r.Bodies[0].Cells[1].Diff.Value, 9);
            Assert.Null(r.Bodies[0].Cells[1].Se);
            Assert.False(r.Bodies[0].Cells[1].Clears);
        }

        [Fact]
        public void ThePooledRowPairsGenomeByGenomeOnTheBasisOfTheCells()
        {
            var rows = new List<JsonElement>();
            double[] own = { 10, 20, 30 }, ctl = { 8, 17, 25 };   // differences 2, 3, 5
            for (int g = 0; g < 3; g++)
            {
                rows.Add(Row("own", g, own[g], own[g], new[] { own[g] }));
                rows.Add(Row("c", g, ctl[g], ctl[g], new[] { ctl[g] }));
            }
            ExamTable.Cell pooled = ExamTable.Build(rows).Pooled[1];
            Assert.Equal(10.0 / 3, pooled.Diff.Value, 9);
            double mean = 10.0 / 3, ss = (2 - mean) * (2 - mean) + (3 - mean) * (3 - mean) + (5 - mean) * (5 - mean);
            Assert.Equal(Math.Sqrt(ss / 2 / 3), pooled.Se.Value, 9);
        }

        [Fact]
        public void AnotherRewardPairsOnTheScoreNotOnNetPerDraw()
        {
            // net per draw is not what this run selected for: the score differs from the mean of the draws
            var rows = new[]
            {
                Row("own", 0, 10, 7.0, new[] { 10.0, 12, 14, 8 }),
                Row("c", 0, 6, 1.5, new[] { 5.0, 7, 9, 3 }),
            };
            ExamTable.Result r = ExamTable.Build(rows, "own", rewardIsNet: false);
            Assert.Equal("score", r.Basis);
            ExamTable.Cell c = r.Bodies[0].Cells[1];
            Assert.Equal(5.5, c.Diff.Value, 9);           // 7 less 1.5, not 5 (the draws' difference)
            Assert.Null(c.Se);
            Assert.False(c.Clears);
            Assert.Equal(5.5, r.Pooled[1].Diff.Value, 9); // the pooled row on the same basis
            Assert.Equal(7.0, r.Bodies[0].OwnScore.Value);

            // the same rows under Net pair draw by draw, on net per draw
            ExamTable.Result net = ExamTable.Build(rows, "own", rewardIsNet: true);
            Assert.Equal("per", net.Basis);
            Assert.Equal(5.0, net.Bodies[0].Cells[1].Diff.Value, 9);
            Assert.NotNull(net.Bodies[0].Cells[1].Se);
            Assert.Equal(4.0, net.Pooled[1].Diff.Value, 9);   // net 10 less net 6
        }

        [Fact]
        public void AnotherRewardWithoutAScoreHasNoCells()
        {
            var rows = new[] { Row("own", 0, 10, null, new[] { 10.0, 12 }), Row("c", 0, 6, null, new[] { 5.0, 7 }) };
            ExamTable.Result r = ExamTable.Build(rows, "own", rewardIsNet: false);
            Assert.Equal("score", r.Basis);
            Assert.Null(r.Bodies[0].Cells[1].Diff);
            Assert.Null(r.Pooled[1].Diff);
        }

        [Fact]
        public void TheOwnColumnIsTheLabelTheRecipeGivesAndABodyWithoutOneStaysNull()
        {
            var rows = new[]
            {
                Js.Parse("{\"brain\":\"mine\",\"genome\":0,\"net\":4,\"per\":[4,4]}"),
                Js.Parse("{\"brain\":\"frozen\",\"genome\":0,\"net\":1,\"per\":[1,1]}"),
            };
            ExamTable.Result r = ExamTable.Build(rows, "mine");
            Assert.Equal("mine", r.Own);
            Assert.Null(r.Bodies[0].BodyIndex);
            Assert.Equal(3.0, r.Bodies[0].Cells[1].Diff.Value, 9);
            Assert.Equal("mine", ExamTable.Build(rows).Own);   // no label given: the first brain the file names, as own is absent
        }

        [Fact]
        public void ARunsExamPairsOnWhatItsRecipeSelectedFor()
        {
            string dir = Where.Fresh("examrun");
            try
            {
                File.WriteAllText(Path.Combine(dir, "tank-exam.jsonl"),
                    "{\"brain\":\"own\",\"genome\":0,\"body\":0,\"net\":10,\"per\":[10,10],\"score\":7}\n" +
                    "{\"brain\":\"frozen\",\"genome\":0,\"body\":0,\"net\":6,\"per\":[6,6],\"score\":1.5}\n");
                // a recipe whose reward is not Net: the cells are score differences
                File.WriteAllText(Path.Combine(dir, "recipe.json"), "{\"name\":\"x\",\"reward\":{\"class\":\"WeightedSum\"},\"exam\":{\"controls\":[{\"kind\":\"own\"},{\"kind\":\"frozen\"}]}}");
                var f = new RunFolder(dir);
                f.Refresh();
                using (JsonDocument d = JsonDocument.Parse(Js.Serialize(f.Snapshot(DateTime.UtcNow))))
                {
                    JsonElement exam = d.RootElement.GetProperty("exam");
                    Assert.Equal("score", exam.GetProperty("basis").GetString());
                    Assert.Equal(5.5, exam.GetProperty("bodies")[0].GetProperty("cells")[1].GetProperty("diff").GetDouble(), 9);
                }
                // Net in the recipe, and no recipe at all: pair on net per draw
                File.WriteAllText(Path.Combine(dir, "recipe.json"), "{\"name\":\"x\",\"reward\":{\"class\":\"Net\"},\"exam\":{\"controls\":[{\"kind\":\"own\"},{\"kind\":\"frozen\"}]}}");
                var g = new RunFolder(dir);
                g.Refresh();
                using (JsonDocument d = JsonDocument.Parse(Js.Serialize(g.Snapshot(DateTime.UtcNow))))
                    Assert.Equal("per", d.RootElement.GetProperty("exam").GetProperty("basis").GetString());
                File.Delete(Path.Combine(dir, "recipe.json"));
                var h = new RunFolder(dir);
                h.Refresh();
                using (JsonDocument d = JsonDocument.Parse(Js.Serialize(h.Snapshot(DateTime.UtcNow))))
                    Assert.Equal("per", d.RootElement.GetProperty("exam").GetProperty("basis").GetString());
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void ACellsFreshComesFromItsOwnRow()
        {
            var rows = new[] { Row("own", 0, 1, 1, new[] { 1.0, 2 }, 1.25), Row("c", 0, 1, 1, new[] { 1.0, 2 }, 0.75) };
            ExamTable.Result r = ExamTable.Build(rows);
            Assert.True(r.HasFresh);
            Assert.Equal(1.25, r.Bodies[0].OwnFresh.Value);
            Assert.Equal(0.75, r.Bodies[0].Cells[1].Fresh.Value);
        }
    }

    public class RecipeCheckTests : IDisposable
    {
        private readonly string _dir = Where.Fresh("recipes");
        public void Dispose() => Directory.Delete(_dir, true);

        private OperationList Ops() => new OperationList(new RoomContext(Where.Root, Where.Settings(), Where.Root));

        private static JsonElement Args(string path) => Js.Parse("{\"path\":\"" + path + "\"}");

        private static JsonElement Result(OperationResult r)
        {
            Assert.True(r.Ok, r.Error);
            return Js.Parse(Js.Serialize(r.Result));
        }

        [Fact]
        public void Brain2ChecksOutWithItsHash()
        {
            JsonElement r = Result(Ops().Invoke("recipe.check", Args("rounds/nursery/brain2-w5.json")));
            Assert.True(r.GetProperty("ok").GetBoolean());
            Assert.Equal("b45716afab91a524d8e42954a71bd491f2ae56dc298421ad93255547253f5001", r.GetProperty("hash").GetString());
            Assert.Contains("\"name\": \"brain2-w5\"", r.GetProperty("canonical").GetString());
        }

        [Fact]
        public void AnUnknownFieldIsRefusedWithItsPath()
        {
            string text = File.ReadAllText(Path.Combine(Where.Root, "rounds", "nursery", "brain2-w5.json"));
            string variant = text.Replace("\"world\": {", "\"world\": {\n    \"extra\": 1,");
            Assert.NotEqual(text, variant);
            string full = Path.Combine(_dir, "variant.json");
            File.WriteAllText(full, variant);
            string rel = Path.GetRelativePath(Where.Root, full).Replace('\\', '/');

            JsonElement r = Result(Ops().Invoke("recipe.check", Args(rel)));
            Assert.False(r.GetProperty("ok").GetBoolean());
            Assert.Equal("world", r.GetProperty("field").GetString());
            Assert.Contains("unknown extra", r.GetProperty("message").GetString());
        }

        [Fact]
        public void TheFieldPathIsParsedOutOfTheNurserysMessage()
        {
            JsonElement r = Js.Parse(Js.Serialize(OperationList.RefusalOf("p.json", "recipe: search.draws must be 1 to 999 (seeds ...)")));
            Assert.Equal("search.draws", r.GetProperty("field").GetString());
            Assert.Equal("must be 1 to 999 (seeds ...)", r.GetProperty("message").GetString());
            JsonElement other = Js.Parse(Js.Serialize(OperationList.RefusalOf("p.json", "D:/x/p.json: Unexpected character")));
            Assert.Equal(JsonValueKind.Null, other.GetProperty("field").ValueKind);
        }

        [Fact]
        public void APathOutsideTheRepositoryIsARefusedCallAsItIsForTheResource()
        {
            OperationResult outside = Ops().Invoke("recipe.check", Args("../outside.json"));
            Assert.False(outside.Ok);
            Assert.Equal(400, outside.Status);
            Assert.Contains("leaves the repository", outside.Error);
            Assert.Equal(400, Ops().Invoke("recipe.check", Args("C:/Windows/win.ini")).Status);
            var room = new RoomContext(Where.Root, Where.Settings(), Where.Root, Path.Combine(_dir, "none.jsonl"));
            Assert.Throws<ResourceRefused>(() => new ResourceResolver(room).Read("nursery://recipe/../outside.json"));
        }

        [Fact]
        public void ARecipeTheNurseryRefusesIsASuccessfulCheckWhoseAnswerIsNo()
        {
            string text = File.ReadAllText(Path.Combine(Where.Root, "rounds", "nursery", "brain2-w5.json"));
            string full = Path.Combine(_dir, "bad.json");
            File.WriteAllText(full, text.Replace("\"world\": {", "\"world\": {\n    \"extra\": 1,"));
            OperationResult r = Ops().Invoke("recipe.check", Args(Path.GetRelativePath(Where.Root, full).Replace('\\', '/')));
            Assert.True(r.Ok);
            Assert.Equal(200, r.Status);
            Assert.False(Js.Parse(Js.Serialize(r.Result)).GetProperty("ok").GetBoolean());
        }

        [Fact]
        public void TheRecipeResourceReadsAFileThatCannotBeOpenedAsARawProblem()
        {
            string full = Path.Combine(_dir, "held.json");
            File.WriteAllText(full, "{}");
            var room = new RoomContext(Where.Root, Where.Settings(), Where.Root, Path.Combine(_dir, "none.jsonl"));
            string rel = Path.GetRelativePath(Where.Root, full).Replace('\\', '/');
            using (new FileStream(full, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                JsonElement r = Js.Parse(Js.Serialize(new ResourceResolver(room).Read("nursery://recipe/" + rel)));
                Assert.False(r.GetProperty("ok").GetBoolean());
                Assert.NotEqual(JsonValueKind.Null, r.GetProperty("rawProblem").ValueKind);
            }
        }

        [Fact]
        public void TheRecipeResourceCarriesTheCheckAndTheHash()
        {
            var room = new RoomContext(Where.Root, Where.Settings(), Where.Root, Path.Combine(_dir, "none.jsonl"));
            JsonElement r = Js.Parse(Js.Serialize(new ResourceResolver(room).Read("nursery://recipe/rounds/nursery/brain2-w5.json")));
            Assert.True(r.GetProperty("ok").GetBoolean());
            Assert.Equal("b45716afab91a524d8e42954a71bd491f2ae56dc298421ad93255547253f5001", r.GetProperty("hash").GetString());
            Assert.Equal("founders", r.GetProperty("resolved").GetProperty("bodies").GetProperty("source").GetString());
        }
    }

    public class OperationTests
    {
        private static OperationList Ops() => new OperationList(new RoomContext(Where.Root, Where.Settings(), Where.Root));

        [Fact]
        public void DescribeListsEveryOperationAndFiltersByFamily()
        {
            OperationList ops = Ops();
            JsonElement all = Js.Parse(Js.Serialize(ops.Invoke("describe", null).Result));
            string[] ids = all.GetProperty("operations").EnumerateArray().Select(o => o.GetProperty("id").GetString()).ToArray();
            Assert.Equal(ops.All.Select(o => o.Id).ToArray(), ids);
            Assert.Contains("recipe.check", ids);
            Assert.Contains("describe", ids);
            JsonElement one = Js.Parse(Js.Serialize(ops.Invoke("describe", Js.Parse("{\"family\":\"recipe\"}")).Result));
            Assert.All(one.GetProperty("operations").EnumerateArray(), o => Assert.Equal("recipe", o.GetProperty("family").GetString()));
            Assert.Equal(400, ops.Invoke("describe", Js.Parse("{\"family\":\"nope\"}")).Status);
        }

        [Fact]
        public void UnknownOperationsAndBadArgumentsAreRefused()
        {
            // a mutating operation that is refused still writes its row: here in a log of this test's own
            string dir = Where.Fresh("unknown-ops");
            OperationList ops = new OperationList(new RoomContext(Where.Root, Where.Settings(), Where.Root, null, Path.Combine(dir, "activity.jsonl")));
            Assert.Equal(404, ops.Invoke("media.picture", Js.Parse("{}")).Status);
            Assert.Equal(400, ops.Invoke("run.launch", Js.Parse("{}")).Status);
            Assert.Single(File.ReadAllLines(Path.Combine(dir, "activity.jsonl")));
            Directory.Delete(dir, true);
            Assert.Equal(400, ops.Invoke("recipe.check", Js.Parse("{}")).Status);
            Assert.Equal(400, ops.Invoke("recipe.check", Js.Parse("{\"path\":3}")).Status);
            Assert.Equal(400, ops.Invoke("recipe.check", Js.Parse("{\"path\":\"a\",\"extra\":1}")).Status);
        }

        [Fact]
        public void AnOperationThatChangesSomethingRequiresAReason()
        {
            var op = new Operation { Family = "x", Action = "y", Mutates = true, Schema = "{\"type\":\"object\",\"properties\":{\"reason\":{\"type\":\"string\"}},\"additionalProperties\":false}" };
            Assert.Contains("reason", OperationList.Validate(op, Js.Parse("{}")));
            Assert.Contains("reason", OperationList.Validate(op, Js.Parse("{\"reason\":\" \"}")));
            Assert.Null(OperationList.Validate(op, Js.Parse("{\"reason\":\"because\"}")));
        }

        [Fact]
        public void AMutatingOperationTakesAReasonItsSchemaDoesNotList()
        {
            var op = new Operation { Family = "x", Action = "y", Mutates = true, Schema = "{\"type\":\"object\",\"properties\":{\"a\":{\"type\":\"string\"}},\"additionalProperties\":false}" };
            Assert.Null(OperationList.Validate(op, Js.Parse("{\"a\":\"1\",\"reason\":\"because\"}")));
            Assert.Contains("reason", OperationList.Validate(op, Js.Parse("{\"a\":\"1\"}")));
            Assert.Contains("unknown argument", OperationList.Validate(op, Js.Parse("{\"a\":\"1\",\"reason\":\"r\",\"b\":1}")));
            // a reading operation does not take one unless it lists it
            var reading = new Operation { Family = "x", Action = "z", Mutates = false, Schema = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}" };
            Assert.Contains("unknown argument", OperationList.Validate(reading, Js.Parse("{\"reason\":\"r\"}")));
        }

        [Fact]
        public void ASchemaIsParsedWhenItIsGivenNotWhenItIsFirstUsed()
        {
            Assert.ThrowsAny<JsonException>(() => new Operation { Family = "x", Schema = "{not json" });
            var op = new Operation { Family = "x", Schema = "{\"type\":\"object\"}" };
            Assert.Equal("object", op.SchemaElement.GetProperty("type").GetString());
        }

        [Fact]
        public void AMissingResourceARefusedAddressAndAnUnknownToolAreProtocolErrors()
        {
            OperationList ops = Ops();
            var room = new RoomContext(Where.Root, Where.Settings(), Where.Root, Path.Combine(Where.Fresh("mcperr"), "none.jsonl"));
            var resolver = new ResourceResolver(room);
            var missing = Assert.Throws<McpProtocolException>(() => McpSurface.Read(resolver, "nursery://run/99"));
            Assert.Equal(McpErrorCode.InvalidParams, missing.ErrorCode);
            Assert.Contains("no run", missing.Message);
            var refused = Assert.Throws<McpProtocolException>(() => McpSurface.Read(resolver, "http://example.com"));
            Assert.Equal(McpErrorCode.InvalidParams, refused.ErrorCode);
            var tool = Assert.Throws<McpProtocolException>(() => McpSurface.Call(ops, "nope", new Dictionary<string, JsonElement>()));
            Assert.Equal(McpErrorCode.InvalidParams, tool.ErrorCode);
            Assert.Contains("nope", tool.Message);
            // a missing action and bad arguments stay results the agent can read
            Assert.True(McpSurface.Call(ops, "recipe", new Dictionary<string, JsonElement>()).IsError == true);
            Assert.True(McpSurface.Call(ops, "recipe", new Dictionary<string, JsonElement> { ["action"] = Js.Parse("\"check\"") }).IsError == true);
        }

        [Fact]
        public void AFlatSchemaSaysWhichActionsTakeASharedProperty()
        {
            var members = new List<Operation>
            {
                new Operation { Family = "f", Action = "a", Mutates = true, Summary = "a", Schema = "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\",\"description\":\"The file.\"},\"only_a\":{\"type\":\"integer\"}}}" },
                new Operation { Family = "f", Action = "b", Mutates = true, Summary = "b", Schema = "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}}}" },
                new Operation { Family = "f", Action = "c", Mutates = false, Summary = "c", Schema = "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"only_c\":{\"type\":\"boolean\"}}}" },
            };
            JsonElement p = McpSurface.Schema(members).GetProperty("properties");
            Assert.Equal("The file. (taken by every action)", p.GetProperty("path").GetProperty("description").GetString());
            Assert.Equal("(taken by action a)", p.GetProperty("only_a").GetProperty("description").GetString());
            Assert.Equal("(taken by action c)", p.GetProperty("only_c").GetProperty("description").GetString());
            Assert.True(p.TryGetProperty("reason", out _));
        }

        [Fact]
        public void TheMcpToolsAreOneAFamilyAndCallTheSameOperations()
        {
            OperationList ops = Ops();
            var tools = McpSurface.Tools(ops);
            Assert.Equal(ops.Families.ToArray(), tools.Select(t => t.Name).ToArray());
            Assert.Contains(tools, t => t.Name == "recipe");
            Assert.Contains(tools, t => t.Name == "describe");
            JsonElement schema = tools.First(t => t.Name == "recipe").InputSchema;
            Assert.Equal("check", schema.GetProperty("properties").GetProperty("action").GetProperty("enum")[0].GetString());

            var args = new Dictionary<string, JsonElement> { ["action"] = Js.Parse("\"check\""), ["path"] = Js.Parse("\"rounds/nursery/brain2-w5.json\"") };
            var result = McpSurface.Call(ops, "recipe", args);
            Assert.NotEqual(true, result.IsError);
            string text = ((ModelContextProtocol.Protocol.TextContentBlock)result.Content[0]).Text;
            Assert.Contains("b45716afab91a524d8e42954a71bd491f2ae56dc298421ad93255547253f5001", text);

            var describe = McpSurface.Call(ops, "describe", new Dictionary<string, JsonElement>());
            Assert.NotEqual(true, describe.IsError);
            Assert.True(McpSurface.Call(ops, "recipe", new Dictionary<string, JsonElement>()).IsError == true);   // no action
        }
    }

    public class ResourceTests : IDisposable
    {
        private readonly string _dir = Where.Fresh("resources");
        public void Dispose() => Directory.Delete(_dir, true);

        private string MakeRun(string name, params (string file, string text)[] files)
        {
            string p = Path.Combine(_dir, name);
            Directory.CreateDirectory(p);
            foreach (var (file, text) in files) File.WriteAllText(Path.Combine(p, file), text);
            return p;
        }

        private ResourceResolver Resolver(string registry, params string[] runs) =>
            new ResourceResolver(new RoomContext(Where.Root, Where.Settings(runs), Where.Root, registry, Path.Combine(_dir, "activity.jsonl")));   // its own activity log: the tree's committed one lists real launches

        [Fact]
        public void TheRegistryListsEntriesAndRoomRunsNewestFirst()
        {
            string older = MakeRun("older", ("tank-generations.jsonl", "{\"gen\":0,\"seconds\":60,\"netMean\":5,\"top\":{\"score\":9}}\n"));
            string newer = MakeRun("newer", ("tank-generations.jsonl", "{\"gen\":0,\"seconds\":60,\"netMean\":7,\"top\":{\"score\":11}}\n{\"gen\":1,\"seconds\":120,\"netMean\":8,\"top\":{\"score\":12}}\n"),
                ("recipe.json", "{\"name\":\"n\",\"description\":\"newer run\",\"search\":{\"generations\":50},\"reward\":{\"class\":\"Net\"}}"));
            string reg = Path.Combine(_dir, "registry.jsonl");
            RegistryEntry.Append(reg, new RegistryEntry { Recipe = "first", RecipeHash = "aa", RecipePath = "rounds/nursery/first.json", ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = older, Status = "completed", StartedAtUtc = "2026-10-01T00:00:00Z", EndedAtUtc = "2026-10-01T01:00:00Z" });
            RegistryEntry.Append(reg, new RegistryEntry { Recipe = "second", RecipeHash = "bb", RecipePath = "rounds/nursery/second.json", ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = newer, Status = "completed", StartedAtUtc = "2026-10-02T00:00:00Z", EndedAtUtc = "2026-10-02T01:00:00Z" });

            ResourceResolver r = Resolver(reg, "scratch/nursery/does-not-exist");
            JsonElement d = Js.Parse(Js.Serialize(r.Read("nursery://registry")));
            Assert.Equal(2, d.GetProperty("entries").GetInt32());
            Assert.Equal(1, d.GetProperty("runs").GetInt32());
            string[] names = d.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).ToArray();
            Assert.Equal(new[] { "second", "first", "does-not-exist" }, names);
            JsonElement second = d.GetProperty("items")[0];
            Assert.Equal(50, second.GetProperty("progress").GetProperty("planned").GetInt32());
            Assert.Equal(12.0, second.GetProperty("best").GetDouble());
            // no tank-scores.jsonl: the top genome's score per generation, named as such
            Assert.Equal("topScore", second.GetProperty("measure").GetString());
            Assert.Contains("top genome's score", second.GetProperty("measureLabel").GetString());
            Assert.Equal(new[] { 11.0, 12.0 }, second.GetProperty("series").EnumerateArray().Select(x => x.GetDouble()).ToArray());
            Assert.False(d.GetProperty("items")[2].GetProperty("exists").GetBoolean());
        }

        [Fact]
        public void TheRegistrySparklineDrawsTheRewardsMeanWhereTheRunHasScores()
        {
            string run = MakeRun("scored",
                ("tank-generations.jsonl", "{\"gen\":0,\"seconds\":60,\"netMean\":7,\"top\":{\"score\":11}}\n{\"gen\":1,\"seconds\":120,\"netMean\":8,\"top\":{\"score\":12}}\n"),
                ("tank-scores.jsonl", "{\"gen\":0,\"reward\":\"WeightedSum\",\"members\":[{\"value\":2},{\"value\":4}]}\n{\"gen\":1,\"reward\":\"WeightedSum\",\"members\":[{\"value\":6},{\"value\":8}]}\n"));
            string rel = Path.GetRelativePath(Where.Root, run).Replace('\\', '/');
            JsonElement d = Js.Parse(Js.Serialize(Resolver(Path.Combine(_dir, "none.jsonl"), rel).Read("nursery://registry")));
            JsonElement item = d.GetProperty("items")[0];
            Assert.Equal("rewardMean", item.GetProperty("measure").GetString());
            Assert.Contains("WeightedSum", item.GetProperty("measureLabel").GetString());
            Assert.Equal(new[] { 3.0, 7.0 }, item.GetProperty("series").EnumerateArray().Select(x => x.GetDouble()).ToArray());
            Assert.Equal(12.0, item.GetProperty("best").GetDouble());
        }

        [Fact]
        public void APathIsShownRelativeToTheRepositoryWhereItLiesUnderIt()
        {
            string main = Path.Combine(_dir, "main");
            string tree = Path.Combine(main, "scratch", "wt-x");
            Directory.CreateDirectory(tree);
            var room = new RoomContext(tree, Where.Settings(), main, Path.Combine(main, "rounds", "nursery", "registry.jsonl"));
            Assert.Equal("scratch/nursery/pilot", room.Display(Path.Combine(main, "scratch", "nursery", "pilot")));   // the main checkout's
            Assert.Equal("runs/a", room.Display(Path.Combine(tree, "runs", "a")));                                    // the worktree's
            Assert.Equal("rounds/nursery/registry.jsonl", room.Display(room.RegistryFile));
            string elsewhere = Path.Combine(Path.GetTempPath(), "elsewhere");
            Assert.Equal(elsewhere, room.Display(elsewhere));
        }

        [Fact]
        public void ARunIsAddressedByRegistryNumberOrByFolderName()
        {
            string folder = MakeRun("by-name", ("tank-generations.jsonl", "{\"gen\":0,\"seconds\":60}\n"));
            string reg = Path.Combine(_dir, "none.jsonl");
            string rel = Path.GetRelativePath(Where.Root, folder).Replace('\\', '/');
            ResourceResolver r = Resolver(reg, rel);
            string leaf = Path.GetFileName(folder);
            JsonElement d = Js.Parse(Js.Serialize(r.Read("nursery://run/" + leaf)));
            Assert.Equal(leaf, d.GetProperty("id").GetString());
            Assert.Equal(1, d.GetProperty("generations").GetArrayLength());
            Assert.Throws<ResourceNotFound>(() => r.Read("nursery://run/7"));

            RegistryEntry.Append(reg, new RegistryEntry { Recipe = "e", Out = folder, Status = "completed", StartedAtUtc = "2026-10-02T00:00:00Z" });
            Assert.Equal("1", Js.Parse(Js.Serialize(Resolver(reg, rel).Read("nursery://run/1"))).GetProperty("id").GetString());
        }

        [Fact]
        public void TheActivityFeedIsEmptyUntilSomethingIsDone()
        {
            var room = new RoomContext(Where.Root, Where.Settings(), Where.Root, Path.Combine(_dir, "none.jsonl"), Path.Combine(_dir, "no-activity.jsonl"));
            JsonElement a = Js.Parse(Js.Serialize(new ResourceResolver(room).Read("nursery://activity")));
            Assert.Equal(0, a.GetProperty("entries").GetArrayLength());
            Assert.False(a.GetProperty("fileExists").GetBoolean());
        }

        [Fact]
        public void BadAddressesAreRefused()
        {
            ResourceResolver r = Resolver(Path.Combine(_dir, "none.jsonl"));
            Assert.Throws<ResourceRefused>(() => r.Read("http://example.com"));
            Assert.Throws<ResourceRefused>(() => r.Read("nursery://recipe/../outside.json"));
            Assert.Throws<ResourceNotFound>(() => r.Read("nursery://nosuch/3"));   // genome/{run}/{id} is a resource since phase 3c: a genome address with no id is refused instead
            Assert.Throws<ResourceNotFound>(() => r.Read("nursery://recipe/rounds/nursery/absent.json"));
        }

        [Fact]
        public void TheMcpResourcesAreWhatTheResolverOffers()
        {
            ResourceResolver r = Resolver(Path.Combine(_dir, "none.jsonl"));
            Assert.Equal(r.Templates.Select(t => t.UriTemplate).ToArray(), McpSurface.Templates(r).Select(t => t.UriTemplate).ToArray());
            Assert.Equal(r.List().Select(x => x.Uri).ToArray(), McpSurface.Resources(r).Select(x => x.Uri).ToArray());
            Assert.Contains(r.List(), x => x.Uri == "nursery://registry");
            Assert.Contains(r.List(), x => x.Uri == "nursery://recipe/rounds/nursery/brain2-w5.json");
        }
    }

    public class ScreenParityTests
    {
        /// <summary>The operations a screen's button names before the room builds them. Each must be drawn disabled, with a title that says phase 2.</summary>
        private static readonly string[] PhaseTwo = { "media.picture", "media.export" };

        /// <summary>An operation id as a screen may write it: literal words and dots, nothing computed.</summary>
        private static readonly Regex LiteralId = new Regex(@"^[a-z][A-Za-z0-9_]*(\.[a-z][A-Za-z0-9_]*)?$");

        private static IEnumerable<string> Sources() =>
            Directory.GetFiles(Where.WwwRoot, "*.html", SearchOption.AllDirectories).Concat(Directory.GetFiles(Where.WwwRoot, "*.js", SearchOption.AllDirectories));

        private static OperationList Ops() => new OperationList(new RoomContext(Where.Root, Where.Settings(), Where.Root));

        /// <summary>Every data-op value, whatever it holds (so that a computed one is seen and refused).</summary>
        private static IEnumerable<(string file, string tag, string id)> DataOps()
        {
            foreach (string file in Sources())
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"<[a-zA-Z]+[^<>]*?data-op=[""']([^""']*)[""'][^<>]*>|data-op=[""']([^""']*)[""']"))
                {
                    string id = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                    yield return (Path.GetFileName(file), m.Value, id);
                }
        }

        /// <summary>The first argument of every callOp(...) call: a literal's text, or null when it is not a literal.</summary>
        private static IEnumerable<(string file, string id)> CallOps()
        {
            foreach (string file in Directory.GetFiles(Where.WwwRoot, "*.js", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (Match m in Regex.Matches(text, @"(?<!function\s)(?<!function\*\s)\bcallOp\s*\((?<arg>[^,)]*)"))
                {
                    Match lit = Regex.Match(m.Groups["arg"].Value.Trim(), @"^(['""`])(?<id>[^'""`$]*)\1$");
                    yield return (Path.GetFileName(file), lit.Success ? lit.Groups["id"].Value : null);
                }
            }
        }

        /// <summary>The keys of the object literal each bindOps({...}) call is given.</summary>
        private static IEnumerable<(string file, string key)> BindOpsKeys()
        {
            foreach (string file in Directory.GetFiles(Where.WwwRoot, "*.js", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (Match m in Regex.Matches(text, @"(?<!function\s)\bbindOps\s*\(\s*(?<open>\{)"))
                    foreach (string key in ObjectKeys(text, m.Groups["open"].Index))
                        yield return (Path.GetFileName(file), key);
            }
        }

        /// <summary>The keys at the top level of the object literal that opens at <paramref name="open"/>, quoted or bare.</summary>
        private static List<string> ObjectKeys(string t, int open)
        {
            var keys = new List<string>();
            int depth = 0;
            bool expectKey = false;
            for (int i = open; i < t.Length; i++)
            {
                char c = t[i];
                if (c == '{' || c == '(' || c == '[') { depth++; if (depth == 1) expectKey = true; continue; }
                if (c == '}' || c == ')' || c == ']') { depth--; if (depth == 0) break; continue; }
                if (c == '\'' || c == '"' || c == '`')
                {
                    int j = i + 1;
                    while (j < t.Length && t[j] != c) { if (t[j] == '\\') j++; j++; }
                    if (depth == 1 && expectKey)
                    {
                        int k = j + 1;
                        while (k < t.Length && char.IsWhiteSpace(t[k])) k++;
                        keys.Add(k < t.Length && t[k] == ':' ? t.Substring(i + 1, j - i - 1) : null);
                        expectKey = false;
                    }
                    i = j;
                    continue;
                }
                if (depth == 1 && c == ',') { expectKey = true; continue; }
                if (depth == 1 && expectKey && !char.IsWhiteSpace(c))
                {
                    int j = i;
                    while (j < t.Length && (char.IsLetterOrDigit(t[j]) || t[j] == '_' || t[j] == '$')) j++;
                    keys.Add(j > i ? t.Substring(i, j - i) : null);
                    expectKey = false;
                    i = Math.Max(j, i + 1) - 1;
                }
            }
            return keys.Where(k => k != null).ToList();
        }

        [Fact]
        public void TheAllowListHoldsNoOperationTheRoomHasBuilt()
        {
            var ids = new HashSet<string>(Ops().All.Select(o => o.Id));
            foreach (string id in PhaseTwo)
                Assert.False(ids.Contains(id), $"{id} is built: take it off the phase-2 list, and enable its controls");
        }

        [Fact]
        public void EveryControlNamesALiteralOperationThatExistsOrIsListedAsPhaseTwo()
        {
            var ids = new HashSet<string>(Ops().All.Select(o => o.Id));
            int found = 0;
            foreach ((string file, string tag, string id) in DataOps())
            {
                found++;
                Assert.True(LiteralId.IsMatch(id), $"{file}: data-op \"{id}\" is not a literal operation id");
                Assert.True(ids.Contains(id) || PhaseTwo.Contains(id), $"{file}: data-op \"{id}\" is not an operation");
            }
            Assert.True(found > 0);
        }

        [Fact]
        public void EveryCallOpNamesALiteralOperationThatExists()
        {
            var ids = new HashSet<string>(Ops().All.Select(o => o.Id));
            int found = 0;
            foreach ((string file, string id) in CallOps())
            {
                found++;
                Assert.True(id != null, $"{file}: a callOp call whose first argument is not a literal operation id");
                Assert.True(LiteralId.IsMatch(id), $"{file}: callOp \"{id}\" is not a literal operation id");
                Assert.True(ids.Contains(id), $"{file}: callOp \"{id}\" is not a built operation");
            }
            Assert.True(found > 0);
        }

        [Fact]
        public void EveryBindOpsKeyNamesAnOperationThatExists()
        {
            var ids = new HashSet<string>(Ops().All.Select(o => o.Id));
            int found = 0;
            foreach ((string file, string key) in BindOpsKeys())
            {
                found++;
                Assert.True(ids.Contains(key), $"{file}: bindOps handles \"{key}\", which is not a built operation");
            }
            Assert.True(found > 0);
        }

        [Fact]
        public void TheScansSeeWhatTheyAreMeantToRefuse()
        {
            // the scans themselves: a computed id, an unquoted attribute and a template are all seen
            Assert.Equal(new[] { "a", "b" }, ObjectKeys("bindOps({ 'a': x => 1, b: async y => { 'no': 2 }, ...z })", 8).ToArray());
            Assert.DoesNotMatch(LiteralId, "${op}");
            Assert.DoesNotMatch(LiteralId, "run.launch.now");
            Assert.Matches(LiteralId, "recipe.check");
            Assert.Matches(LiteralId, "compare");
        }

        [Fact]
        public void AControlWhoseOperationIsNotBuiltIsDrawnDisabledWithATitleSayingPhaseTwo()
        {
            var ops = Ops();
            foreach ((string file, string tag, string id) in DataOps())
            {
                if (!tag.StartsWith("<")) continue;   // an attribute inside a script's template string is checked by the scan above
                if (ops.Find(id) != null) { Assert.DoesNotContain(" disabled", tag); continue; }
                Assert.Contains(" disabled", tag);
                Assert.Matches(@"title=""[^""]*phase 2", tag);
            }
        }

        [Fact]
        public void NoPageLoadsAnythingFromTheInternet()
        {
            foreach (string file in Sources().Concat(Directory.GetFiles(Where.WwwRoot, "*.css")))
                Assert.DoesNotMatch(@"(src|href)=[""']https?://|url\([""']?https?://|@import[^;]*https?://|from\s+[""']https?://", File.ReadAllText(file));
        }
    }

    public class GuardTests
    {
        private const int Port = 5179;

        private static RequestDelegate Pipeline()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.Configure<HostFilteringOptions>(RoomGuards.ConfigureHosts);
            var app = new ApplicationBuilder(services.BuildServiceProvider());
            RoomGuards.Use(app, Port);
            app.Run(ctx => { ctx.Response.StatusCode = 200; return Task.CompletedTask; });
            return app.Build();
        }

        private static async Task<int> Send(string method, string path, string host = "127.0.0.1:5179", string origin = null, string contentType = null)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = method;
            ctx.Request.Path = path;
            ctx.Request.Host = new HostString(host);
            if (origin != null) ctx.Request.Headers["Origin"] = origin;
            if (contentType != null) ctx.Request.ContentType = contentType;
            ctx.Response.Body = new MemoryStream();
            await Pipeline()(ctx);
            return ctx.Response.StatusCode;
        }

        [Theory]
        [InlineData("127.0.0.1:5179")]
        [InlineData("localhost:5179")]
        [InlineData("LOCALHOST:5179")]
        [InlineData("[::1]:5179")]
        [InlineData("127.0.0.1")]
        public async Task TheRoomsOwnNamesAreAnswered(string host) => Assert.Equal(200, await Send("GET", "/api/room", host));

        [Theory]
        [InlineData("evil.example")]
        [InlineData("evil.example:5179")]
        [InlineData("127.0.0.1.evil.example:5179")]
        [InlineData("192.168.1.20:5179")]
        [InlineData("10.0.0.1")]
        public async Task AnyOtherHostNameIsRefusedWith400(string host) => Assert.Equal(400, await Send("GET", "/api/room", host));

        [Theory]
        [InlineData("http://127.0.0.1:5179")]
        [InlineData("http://localhost:5179")]
        public async Task TheRoomsOwnOriginsPass(string origin)
        {
            Assert.Equal(200, await Send("GET", "/api/room", origin: origin));
            Assert.Equal(200, await Send("POST", "/api/op/recipe.check", origin: origin, contentType: "application/json"));
        }

        [Theory]
        [InlineData("http://evil.example")]
        [InlineData("http://127.0.0.1:5180")]
        [InlineData("http://127.0.0.1")]
        [InlineData("https://127.0.0.1:5179")]
        [InlineData("http://localhost.evil.example:5179")]
        [InlineData("null")]
        [InlineData("")]
        public async Task AnotherOriginIsRefusedWith403OnEveryPath(string origin)
        {
            foreach (string path in new[] { "/api/room", "/api/resource", "/api/op/recipe.check", "/mcp", "/run.html", "/" })
                Assert.Equal(403, await Send(path.StartsWith("/api/op") || path == "/mcp" ? "POST" : "GET", path, origin: origin, contentType: "application/json"));
        }

        [Fact]
        public async Task TwoOriginHeadersAreRefused()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = "GET";
            ctx.Request.Path = "/api/room";
            ctx.Request.Host = new HostString("127.0.0.1", Port);
            ctx.Request.Headers["Origin"] = new Microsoft.Extensions.Primitives.StringValues(new[] { "http://127.0.0.1:5179", "http://evil.example" });
            ctx.Response.Body = new MemoryStream();
            await Pipeline()(ctx);
            Assert.Equal(403, ctx.Response.StatusCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("text/plain")]
        [InlineData("application/x-www-form-urlencoded")]
        [InlineData("multipart/form-data; boundary=x")]
        [InlineData("application/jsonp")]
        public async Task AnOperationWithoutJsonIsRefusedWith415(string contentType) =>
            Assert.Equal(415, await Send("POST", "/api/op/recipe.check", contentType: contentType));

        [Theory]
        [InlineData("application/json")]
        [InlineData("application/json; charset=utf-8")]
        [InlineData("Application/JSON")]
        public async Task AnOperationWithJsonPasses(string contentType) =>
            Assert.Equal(200, await Send("POST", "/api/op/recipe.check", contentType: contentType));

        [Fact]
        public async Task OnlyOperationsNeedTheContentType()
        {
            Assert.Equal(200, await Send("GET", "/api/resource"));
            Assert.Equal(200, await Send("POST", "/mcp", contentType: "text/plain"));   // the MCP transport has its own rules
        }

        [Theory]
        [InlineData("http://127.0.0.1:5179/", true)]
        [InlineData("http://[::1]:5179/", true)]
        [InlineData("http://localhost:5179", false)]    // a name is not an address: the check wants the bound IP
        [InlineData("http://0.0.0.0:5179", false)]
        [InlineData("http://[::]:5179", false)]
        [InlineData("http://192.168.1.20:5179", false)]
        [InlineData("http://+:5179", false)]
        public void OnlyLoopbackAddressesPassTheStartupCheck(string address, bool loopback) =>
            Assert.Equal(loopback, RoomGuards.IsLoopbackAddress(address));
    }
}
