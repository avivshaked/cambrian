using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// Round B1: what the run screen and the registry read off the room. The arithmetic is checked against hand-worked cases, never against the code's own output.
    /// </summary>
    public class ExamComparisonTests
    {
        private static ExamTable.Result Exam(params (string brain, int genome, double net)[] rows) =>
            ExamTable.Build(rows.Select(r => Js.Parse(Js.Serialize(new { brain = r.brain, genome = r.genome, body = r.genome, net = r.net }))).ToList(), null, true);

        private static IEnumerable<(string, int, double)> Rows(string brain, double[] own, double[] other)
        {
            for (int g = 0; g < own.Length; g++) { yield return ("own", g, own[g]); yield return (brain, g, other[g]); }
        }

        [Fact]
        public void ThePooledDifferencesAndTheirDifferenceAreTheHandWorkedOnes()
        {
            // This run, brain k: own 10, 12, 14 against 8, 8, 8 -> differences 2, 4, 6: mean 4, variance 4, se = sqrt(4/3) = 1.154701.
            // The control, brain k: own 10, 10, 10 against 9, 8, 7 -> 1, 2, 3: mean 2, variance 1, se = sqrt(1/3) = 0.577350.
            // Their difference is 2, its standard error sqrt(4/3 + 1/3) = sqrt(5/3) = 1.290994, and two of those are 2.581989: 2 is within, so no difference.
            // Brain j: this run 5, 6, 7 (own 10, 12, 14 against 5, 6, 7 gives 5, 6, 7), mean 6, se 0.577350; the control 0, 1, 2 (own 10, 10, 10 against 10, 9, 8), mean 1, se 0.577350.
            // Their difference is 5, its standard error sqrt(2/3) = 0.816497, and two of those 1.632993: 5 is beyond, so they differ.
            var run = Exam(Rows("k", new[] { 10.0, 12, 14 }, new[] { 8.0, 8, 8 }).Concat(Rows("j", new[] { 10.0, 12, 14 }, new[] { 5.0, 6, 7 }).Where(r => r.Item1 == "j")).ToArray());
            var control = Exam(Rows("k", new[] { 10.0, 10, 10 }, new[] { 9.0, 8, 7 }).Concat(Rows("j", new[] { 10.0, 10, 10 }, new[] { 10.0, 9, 8 }).Where(r => r.Item1 == "j")).ToArray());
            ExamComparison c = ExamCompare.Build(run, control);
            Assert.True(c.Comparable);
            Assert.Null(c.Note);               // the same number of genomes, the same basis: nothing to say
            ExamCompareBrain k = c.Brains.Single(b => b.Label == "k"), j = c.Brains.Single(b => b.Label == "j");

            Assert.Equal(4.0, k.Run.Diff.Value, 9);
            Assert.Equal(Math.Sqrt(4.0 / 3), k.Run.Se.Value, 9);
            Assert.Equal(2.0, k.Control.Diff.Value, 9);
            Assert.Equal(Math.Sqrt(1.0 / 3), k.Control.Se.Value, 9);
            Assert.Equal(2.0, k.Delta.Value, 9);
            Assert.Equal(Math.Sqrt(5.0 / 3), k.DeltaSe.Value, 9);
            Assert.Equal(1.290994449, k.DeltaSe.Value, 8);
            Assert.False(k.Differs);

            Assert.Equal(6.0, j.Run.Diff.Value, 9);
            Assert.Equal(1.0, j.Control.Diff.Value, 9);
            Assert.Equal(5.0, j.Delta.Value, 9);
            Assert.Equal(Math.Sqrt(2.0 / 3), j.DeltaSe.Value, 9);
            Assert.True(j.Differs);
            Assert.Contains("√(se₁² + se₂²)", c.Rule);
            Assert.Contains("two", c.Rule);
        }

        [Fact]
        public void ADifferenceOfExactlyTwoCombinedStandardErrorsIsNotMarked()
        {
            // The bar is "more than two": a difference equal to it does not pass. Run: differences 1 and 3 (mean 2, variance 2, se sqrt(2/1/2) = 1).
            // Control: differences 0 and 0 (mean 0, se 0). Delta = 2, combined se = 1, two of them = 2: equal, so not marked.
            var run = Exam(("own", 0, 11), ("b", 0, 10), ("own", 1, 13), ("b", 1, 10));
            var control = Exam(("own", 0, 10), ("b", 0, 10), ("own", 1, 10), ("b", 1, 10));
            ExamCompareBrain b = ExamCompare.Build(run, control).Brains.Single();
            Assert.Equal(2.0, b.Run.Diff.Value, 9);
            Assert.Equal(1.0, b.Run.Se.Value, 9);
            Assert.Equal(0.0, b.Control.Se.Value, 9);
            Assert.Equal(2.0, b.Delta.Value, 9);
            Assert.Equal(1.0, b.DeltaSe.Value, 9);
            Assert.False(b.Differs);
        }

        [Fact]
        public void TwoExamsOnDifferentFootingsAreListedAndNoBrainIsMarked()
        {
            // One run selected on a reward other than Net, so its cells are differences of score; the other on net. They are not one quantity.
            JsonElement R(string brain, int g, double net, double score) => Js.Parse(Js.Serialize(new { brain, genome = g, body = g, net, score }));
            var scored = ExamTable.Build(new[] { R("own", 0, 5, 50), R("a", 0, 4, 10), R("own", 1, 5, 60), R("a", 1, 3, 20) }, null, false);
            var net = Exam(("own", 0, 5), ("a", 0, 4), ("own", 1, 5), ("a", 1, 3));
            ExamComparison c = ExamCompare.Build(scored, net);
            Assert.False(c.Comparable);
            Assert.Equal("score", c.RunBasis);
            Assert.Equal("net", c.ControlBasis);
            Assert.Contains("not on one footing", c.Note);
            ExamCompareBrain a = c.Brains.Single();
            Assert.NotNull(a.Delta);          // the numbers are still there to read
            Assert.Null(a.Differs);           // but nothing is marked
        }

        [Fact]
        public void ABrainOnlyOneExamSwamIsListedWithWhy()
        {
            var run = Exam(("own", 0, 5), ("a", 0, 4), ("own", 1, 6), ("a", 1, 3), ("b", 0, 1), ("b", 1, 2));
            var control = Exam(("own", 0, 5), ("a", 0, 4), ("own", 1, 6), ("a", 1, 3));
            ExamComparison c = ExamCompare.Build(run, control);
            ExamCompareBrain b = c.Brains.Single(x => x.Label == "b");
            Assert.Null(b.Control);
            Assert.Null(b.Differs);
            Assert.Contains("control", b.Why);
            Assert.DoesNotContain(c.Brains, x => x.Label == "own");
        }

        [Fact]
        public void AStandardErrorNeedsTwoGenomes()
        {
            var one = Exam(("own", 0, 5), ("a", 0, 4));
            ExamCompareBrain b = ExamCompare.Build(one, one).Brains.Single();
            Assert.Null(b.Run.Se);
            Assert.Null(b.Differs);
            Assert.Contains("two genomes", b.Why);
        }
    }

    public class ExamHeaderTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        /// <summary>The labels the pilot's exam file names, in its order.</summary>
        private static readonly string[] PilotLabels =
        {
            "own", "frozen", "full", "knockout", "brainless", "knockout Depth", "knockout Energy", "knockout Flow", "knockout Scent",
            "knockout Stroke,Hold,Ramp", "knockout JointAngle,JointAngularVelocity", "constant 0.3", "constant 0.4", "constant 0.5", "constant 0.6", "constant 0.7",
            "kinesis rate -0.5 10", "kinesis rate 0 20",
        };

        private List<JsonElement> Controls()
        {
            Recipe r = Recipe.Load(_fx.Full("rounds/nursery/brain2-w5.json"));
            return Js.Items(Js.At(Js.Parse(r.Canonical()), "exam", "controls")).ToList();
        }

        [Fact]
        public void ThePilotsEighteenBrainsGetHeadingsThatCarryTheChannelOrTheWholeWord()
        {
            List<JsonElement> controls = Controls();
            Assert.Equal(18, controls.Count);
            List<ExamHeader> h = ExamHeaders.Build(PilotLabels, controls);
            Assert.Equal(new[]
            {
                "own", "frozen", "full", "KO Chemical", "brainless", "KO Depth", "KO Energy", "KO Flow", "KO Scent", "KO Stroke+2", "KO JointAngle+1",
                "constant 0.3", "constant 0.4", "constant 0.5", "constant 0.6", "constant 0.7", "kinesis rate -0.5 10", "kinesis rate 0 20",
            }, h.Select(x => x.Short).ToArray());
            // the bare knockout takes the channel from the recipe's control at its index, and the title carries it whole
            Assert.Equal("knockout Chemical", h[3].Title);
            Assert.Equal("knockout", h[3].Label);
            Assert.Equal("knockout Stroke,Hold,Ramp", h[9].Title);
            Assert.Equal("knockout JointAngle,JointAngularVelocity", h[10].Title);
            // no two headings alike, and every kind is the word the nursery's label begins with
            Assert.Equal(18, h.Select(x => x.Short).Distinct().Count());
            Assert.Equal(new[] { "own", "frozen", "full", "knockout", "brainless" }, h.Take(5).Select(x => x.Kind).ToArray());
            Assert.All(h.Skip(11).Take(5), x => Assert.Equal("constant", x.Kind));
            Assert.Equal(new[] { 3, 5, 6, 7, 8, 9, 10 }, h.Where(x => x.Kind == "knockout").Select(x => x.Index).ToArray());
        }

        [Fact]
        public void NoWordIsEverCutToAStub()
        {
            List<ExamHeader> h = ExamHeaders.Build(PilotLabels, Controls());
            foreach (ExamHeader x in h)
            {
                // every word of a short heading is a whole word of the title, the abbreviation KO, or a first channel and its "+n" (never a prefix of a longer word)
                foreach (string word in x.Short.Split(' '))
                {
                    string bare = System.Text.RegularExpressions.Regex.Replace(word, @"\+\d+$", "");
                    bool whole = bare == "KO" || x.Title.Split(' ', ',').Contains(bare);
                    Assert.True(whole, $"\"{word}\" in the heading \"{x.Short}\" is not a whole word of \"{x.Title}\"");
                }
                Assert.DoesNotContain("…", x.Short);
                Assert.False(x.Short.EndsWith("."), x.Short);
            }
            Assert.Equal("brainless", h[4].Short);
        }

        [Fact]
        public void AKnockoutWhoseControlIsNotThereIsNamedWholeNotCutToKO()
        {
            // no recipe (an old run): the bare label stays a whole word; "KO" alone would be four knockouts that read alike
            List<ExamHeader> h = ExamHeaders.Build(new[] { "own", "knockout", "knockout Flow" }, null);
            Assert.Equal("knockout", h[1].Short);
            Assert.Equal("KO Flow", h[2].Short);
            // a control of another kind at that index is not taken for the knockout's
            List<ExamHeader> g = ExamHeaders.Build(new[] { "own", "knockout" }, Js.Items(Js.Parse("[{\"kind\":\"own\"},{\"kind\":\"frozen\"}]")).ToList());
            Assert.Equal("knockout", g[1].Short);
        }

        [Fact]
        public void TheRunResourceCarriesTheHeadingsAndTakesAKnockoutsChannelFromTheEntrysRecipe()
        {
            // a run with no recipe of its own whose registry entry names brain2 (the recipe's hash agreeing): the channel comes from that file
            string folder = Path.Combine(_fx.RunsDir, "pilot");
            Directory.CreateDirectory(folder);
            var lines = new List<string>();
            foreach (string label in PilotLabels)
                for (int g = 0; g < 2; g++) lines.Add(Js.Serialize(new { brain = label, genome = g, body = g, net = 10.0 + g, per = new[] { 10.0 + g, 11.0 + g } }));
            File.WriteAllLines(Path.Combine(folder, "tank-exam.jsonl"), lines);
            string hash = Recipe.Load(_fx.Full("rounds/nursery/brain2-w5.json")).Hash();
            RegistryEntry.Append(_fx.Registry, new RegistryEntry { Recipe = "brain2", RecipeHash = hash, RecipePath = "rounds/nursery/brain2-w5.json", ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = folder, Status = "completed", Rerunnable = false, NotRerunnableBecause = "a test", StartedAtUtc = "2026-10-01T00:00:00Z", EndedAtUtc = "2026-10-01T01:00:00Z" });
            JsonElement run = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Run("1")));
            JsonElement[] heads = run.GetProperty("exam").GetProperty("headers").EnumerateArray().ToArray();
            Assert.Equal(18, heads.Length);
            Assert.Equal("KO Chemical", heads[3].GetProperty("short").GetString());
            Assert.Equal("knockout Chemical", heads[3].GetProperty("title").GetString());
            Assert.Equal("brainless", heads[4].GetProperty("short").GetString());
            Assert.True(run.GetProperty("recipeExists").GetBoolean());

            // the entry's recipe hash does not agree with the file: the file is not what ran, so the channel is not taken from it
            RegistryEntry.Append(_fx.Registry, new RegistryEntry { Recipe = "brain2", RecipeHash = "00", RecipePath = "rounds/nursery/brain2-w5.json", ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = folder + "2", Status = "completed", Rerunnable = false, NotRerunnableBecause = "a test", StartedAtUtc = "2026-10-01T00:00:00Z", EndedAtUtc = "2026-10-01T01:00:00Z" });
            Directory.CreateDirectory(folder + "2");
            File.Copy(Path.Combine(folder, "tank-exam.jsonl"), Path.Combine(folder + "2", "tank-exam.jsonl"));
            JsonElement other = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Run("2")));
            Assert.Equal("knockout", other.GetProperty("exam").GetProperty("headers")[3].GetProperty("short").GetString());
        }
    }

    public class RunSignalTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private static readonly DateTime Now = new DateTime(2026, 10, 2, 18, 0, 0, DateTimeKind.Utc);

        private static (string, DateTime?)[] Writes(params (string, DateTime?)[] w) => w;

        // ---- the stall rule

        [Fact]
        public void ARunMarkedRunningIsStalledPastThreeMedianGenerationsOfSilence()
        {
            // generations took 60, 60, 120, 60 s: the median gap is 60 s, so the limit is 180 s
            double[] seconds = { 60, 120, 240, 300, 360 };
            Assert.Equal(60.0, StallRule.MedianGap(seconds));
            Stall quiet = StallRule.Evaluate(seconds, Writes(("tank-generations.jsonl", Now.AddSeconds(-181))), Now, true);
            Assert.True(quiet.Evaluated);
            Assert.True(quiet.Stalled);
            Assert.Equal(180.0, quiet.LimitSeconds);
            Assert.Equal(181.0, quiet.SinceWriteSeconds);
            Assert.Equal("tank-generations.jsonl", quiet.LastWriteFile);
            Stall fine = StallRule.Evaluate(seconds, Writes(("tank-generations.jsonl", Now.AddSeconds(-179))), Now, true);
            Assert.True(fine.Evaluated);
            Assert.False(fine.Stalled);
            // exactly at the limit is not past it
            Assert.False(StallRule.Evaluate(seconds, Writes(("a", Now.AddSeconds(-180))), Now, true).Stalled);
        }

        [Fact]
        public void TheLatestWriteOfAnyFileCountsAndAnEvenCountTakesTheMiddleTwo()
        {
            // gaps 30, 90: median 60. The exam file was written 100 s ago though the generation file 10 minutes ago: the run is not silent.
            double[] seconds = { 30, 60, 150 };
            Assert.Equal(60.0, StallRule.MedianGap(seconds));
            Stall s = StallRule.Evaluate(seconds, Writes(("tank-generations.jsonl", Now.AddMinutes(-10)), ("tank-exam.jsonl", Now.AddSeconds(-100)), ("nursery.log", null)), Now, true);
            Assert.False(s.Stalled);
            Assert.Equal("tank-exam.jsonl", s.LastWriteFile);
            Assert.Equal(100.0, s.SinceWriteSeconds);
        }

        [Fact]
        public void TheAlarmIsNotRaisedForARunThatIsNotRunningOrHasNoMedianYet()
        {
            double[] seconds = { 60, 120, 180 };
            Stall done = StallRule.Evaluate(seconds, Writes(("a", Now.AddHours(-5))), Now, false);
            Assert.False(done.Evaluated);
            Assert.False(done.Stalled);
            Assert.Contains("not marked running", done.Reason);
            Stall noMedian = StallRule.Evaluate(new double[] { 60 }, Writes(("a", Now.AddHours(-5))), Now, true);
            Assert.False(noMedian.Evaluated);
            Assert.False(noMedian.Stalled);
            Assert.Contains("median", noMedian.Reason);
            Assert.Null(StallRule.MedianGap(new double[] { 5 }));
        }

        // ---- the exam's end

        [Fact]
        public void TheExamEndsWhenTheRemainingRowsAreWrittenAtThePaceOfTheRowsSoFar()
        {
            // 4 brains x 6 genomes = 24 rows. 12 rows in the 36 minutes from the start (10:00) to the last row (10:36): 3 min a row. 12 rows remain: 36 min after 10:36, 11:12.
            var start = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);
            ExamEnd e = ExamEstimate.Compute(12, 4, 6, start, start.AddMinutes(36), "the exam's start");
            Assert.Equal("estimated", e.State);
            Assert.Equal(24, e.RowsExpected);
            Assert.Equal(180.0, e.SecondsPerRow.Value, 9);
            Assert.Equal(start.AddMinutes(72), e.EndsUtc.Value);
            Assert.Contains("12 of 24 rows (4 brains × 6 genomes)", e.Basis);
            Assert.Contains("180 s a row", e.Basis);
            Assert.Contains("the exam's start", e.Basis);
        }

        [Fact]
        public void WhatCannotBeComputedIsSaidAsUnknownWithWhy()
        {
            var t = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);
            Assert.Equal("unknown", ExamEstimate.Compute(3, null, 6, t, t.AddMinutes(1), "x").State);
            Assert.Contains("no recipe", ExamEstimate.Compute(3, null, 6, t, t.AddMinutes(1), "x").Reason);
            Assert.Equal("unknown", ExamEstimate.Compute(0, 4, 6, t, null, "x").State);
            Assert.Contains("no exam row", ExamEstimate.Compute(0, 4, 6, t, null, "x").Reason);
            Assert.Contains("start", ExamEstimate.Compute(2, 4, 6, null, t, "x").Reason);
            Assert.Equal("complete", ExamEstimate.Compute(24, 4, 6, t, t.AddMinutes(60), "x").State);
            Assert.Equal("unknown", ExamEstimate.Compute(2, 4, 6, t, t, "x").State);   // a last row no later than the start gives no pace
        }

        [Fact]
        public void ARunInItsExamCarriesTheEstimateAndTheStallAlarmInItsSnapshot()
        {
            string folder = Path.Combine(_fx.RunsDir, "live");
            Directory.CreateDirectory(folder);
            DateTime now = DateTime.UtcNow;
            File.WriteAllText(Path.Combine(folder, "run.json"), "{\"status\":\"running\"}");
            File.WriteAllText(Path.Combine(folder, "recipe.json"), "{\"name\":\"live\",\"search\":{\"generations\":5},\"reward\":{\"class\":\"Net\"},\"exam\":{\"controls\":[{\"kind\":\"own\"},{\"kind\":\"frozen\"},{\"kind\":\"brainless\"}]}}");
            File.WriteAllLines(Path.Combine(folder, "tank-generations.jsonl"), new[] { 60, 120, 180 }.Select((s, i) => "{\"gen\":" + i + ",\"seconds\":" + s + ",\"top\":{\"score\":1,\"body\":0}}"));
            // 3 brains x 2 genomes = 6 rows; three written: own, frozen, brainless of genome 0
            File.WriteAllLines(Path.Combine(folder, "tank-exam.jsonl"), new[] { "own", "frozen", "brainless" }.Select(b => Js.Serialize(new { brain = b, genome = 0, body = 0, net = 1.0, per = new[] { 1.0, 2.0 } })));
            File.WriteAllText(Path.Combine(folder, "tank-exam-genomes.tsv"), "0\t{}\n1\t{}\n");
            File.SetLastWriteTimeUtc(Path.Combine(folder, "tank-exam-genomes.tsv"), now.AddMinutes(-60));
            File.SetLastWriteTimeUtc(Path.Combine(folder, "tank-exam.jsonl"), now.AddMinutes(-54));
            foreach (string f in new[] { "run.json", "recipe.json", "tank-generations.jsonl" }) File.SetLastWriteTimeUtc(Path.Combine(folder, f), now.AddMinutes(-61));
            RunFolder rf = _fx.Room.Runs.Get(folder);
            rf.Refresh(0);
            Dictionary<string, object> snap = rf.Snapshot(now);
            Assert.Equal("exam", Js.Parse(Js.Serialize(snap["state"])).GetProperty("phase").GetString());
            var end = (ExamEnd)snap["examEnd"];
            Assert.Equal("estimated", end.State);
            Assert.Equal(3, end.Brains);
            Assert.Equal(2, end.Genomes);
            Assert.Equal(6, end.RowsExpected);
            Assert.Equal(120.0, end.SecondsPerRow.Value, 3);
            Assert.Equal(now.AddMinutes(-54).AddSeconds(360), end.EndsUtc.Value, TimeSpan.FromSeconds(2));
            var stall = (Stall)snap["stall"];
            Assert.True(stall.Evaluated);
            Assert.True(stall.Stalled);                 // 54 minutes of silence against a limit of three minutes (a median generation of 60 s)
            Assert.Equal("tank-exam.jsonl", stall.LastWriteFile);

            // a quiet moment ago: the same run is not stalled
            File.SetLastWriteTimeUtc(Path.Combine(folder, "tank-exam.jsonl"), now.AddSeconds(-20));
            var fresh = new RunFolder(folder);
            fresh.Refresh(0);
            Dictionary<string, object> live = fresh.Snapshot(now);
            Assert.False(((Stall)live["stall"]).Stalled);

            // a finished run carries neither
            File.WriteAllText(Path.Combine(folder, "run.json"), "{\"status\":\"completed\"}");
            File.SetLastWriteTimeUtc(Path.Combine(folder, "tank-exam.jsonl"), now.AddMinutes(-54));
            RunFolder again = new RunFolder(folder);
            again.Refresh(0);
            Dictionary<string, object> over = again.Snapshot(now);
            Assert.Null(over["examEnd"]);
            Assert.False(((Stall)over["stall"]).Stalled);
            Assert.False(((Stall)over["stall"]).Evaluated);
        }
    }

    public class RunActivityTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private void Row(string op, object args, string outcome = "ok", object result = null, string who = "mcp", string reason = "why", string time = "2026-10-02T12:00:00Z") =>
            _fx.Room.Activity.Append(new { timeUtc = time, operation = op, args, reason, who, tree = ".", outcome, detail = (string)null, result });

        private string Enter(string name, string recipe = "rounds/nursery/brain2-w5.json")
        {
            string folder = Path.Combine(_fx.RunsDir, name);
            Directory.CreateDirectory(folder);
            RegistryEntry.Append(_fx.Registry, new RegistryEntry { Recipe = name, RecipeHash = "aa", RecipePath = recipe, ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = folder, Status = "completed", Rerunnable = true, StartedAtUtc = "2026-10-01T00:00:00Z", EndedAtUtc = "2026-10-01T01:00:00Z" });
            return folder;
        }

        [Fact]
        public void TheActivityOfARunIsItsLaunchStopsRerunsAndEpisodeStepsAndNothingOfAnother()
        {
            Enter("alpha"); Enter("beta");
            Row("run.launch", new { recipe = "rounds/nursery/brain2-w5.json", name = "alpha", threads = 2 }, result: new { id = "alpha", folder = "runs/alpha" }, time: "2026-10-02T12:00:01Z");        // mine
            Row("run.launch", new { recipe = "rounds/nursery/brain2-w5.json", name = "beta", threads = 2 }, result: new { id = "beta", folder = "runs/beta" }, time: "2026-10-02T12:00:02Z");           // beta's
            Row("run.stop", new { id = "1" }, result: new { id = "alpha" }, time: "2026-10-02T12:00:03Z");                                                                                      // mine, by its number
            Row("run.stop", new { id = "beta" }, result: new { id = "beta" }, time: "2026-10-02T12:00:04Z");                                                                                    // beta's
            Row("episode.step", new { run = "alpha", brain = 1, draw = 0, tank = 0 }, result: new { run = "1" }, time: "2026-10-02T12:00:05Z");                                                // mine
            Row("episode.step", new { run = "2", brain = 1, draw = 0, tank = 0 }, result: new { run = "2" }, time: "2026-10-02T12:00:06Z");                                                    // beta's, by its number
            Row("run.rerun", new { entry = "1", name = "alpha-rerun", threads = 2 }, result: new { id = "alpha-rerun", folder = "runs/alpha-rerun" }, time: "2026-10-02T12:00:07Z");           // a rerun of mine
            Row("run.launch", new { recipe = "rounds/nursery/brain2-w5.json", name = "alpha", threads = 2 }, outcome: "refused", time: "2026-10-02T12:00:08Z");                                  // refused for my name
            Row("recipe.write", new { path = "rounds/nursery/x.json" }, result: new { path = "rounds/nursery/x.json" }, time: "2026-10-02T12:00:09Z");                                        // no run's
            var resolver = new ResourceResolver(_fx.Room);

            JsonElement alpha = Js.Parse(Js.Serialize(resolver.Read("nursery://activity/alpha")));
            Assert.Equal(5, alpha.GetProperty("shown").GetInt32());
            Assert.Equal(9, alpha.GetProperty("total").GetInt32());
            string[] times = alpha.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("timeUtc").GetString()).ToArray();
            Assert.Equal(new[] { "2026-10-02T12:00:08Z", "2026-10-02T12:00:07Z", "2026-10-02T12:00:05Z", "2026-10-02T12:00:03Z", "2026-10-02T12:00:01Z" }, times);   // newest first
            Assert.Equal("1", alpha.GetProperty("run").GetString());

            JsonElement beta = Js.Parse(Js.Serialize(resolver.Read("nursery://activity/2")));
            Assert.Equal(new[] { "2026-10-02T12:00:06Z", "2026-10-02T12:00:04Z", "2026-10-02T12:00:02Z" }, beta.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("timeUtc").GetString()).ToArray());
            Assert.Empty(RoomResponseShapeTests.EmptyObjects(alpha));
            // and the whole log is still what nursery://activity answers
            Assert.Equal(9, Js.Parse(Js.Serialize(resolver.Read("nursery://activity"))).GetProperty("shown").GetInt32());
        }

        [Fact]
        public void ARunTheRoomDoesNotKnowHasNoActivityToRead()
        {
            var resolver = new ResourceResolver(_fx.Room);
            Assert.Throws<ResourceNotFound>(() => resolver.Read("nursery://activity/no-such-run"));
        }

        [Fact]
        public void TheFilterTakesTheRowsAMatcherAcceptsInTheirOrder()
        {
            JsonElement R(string json) => Js.Parse(json);
            var rows = new[]
            {
                R("{\"operation\":\"run.launch\",\"outcome\":\"ok\",\"args\":{\"name\":\"a\"},\"result\":{\"id\":\"a\",\"folder\":\"x/a\"}}"),
                R("{\"operation\":\"run.stop\",\"outcome\":\"ok\",\"args\":{\"id\":\"a\"}}"),
                R("{\"operation\":\"recipe.write\",\"outcome\":\"ok\",\"args\":{\"id\":\"a\"}}"),
                R("{\"operation\":\"run.stop\",\"outcome\":\"ok\",\"args\":{\"id\":\"b\"}}"),
            };
            List<JsonElement> mine = ActivityFilter.ForRun(rows, s => s == "a", f => f == "x/a");
            Assert.Equal(new[] { "run.launch", "run.stop" }, mine.Select(r => r.GetProperty("operation").GetString()).ToArray());
        }
    }

    public class RegistryBaseAndEpisodeRefusalTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private string Enter(string name, string recipePath, string status = "completed")
        {
            string folder = Path.Combine(_fx.RunsDir, name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "tank-generations.jsonl"), "{\"gen\":0,\"seconds\":60,\"netMean\":5,\"top\":{\"score\":9,\"body\":1}}\n");
            RegistryEntry.Append(_fx.Registry, new RegistryEntry { Recipe = name, RecipeHash = "aa", RecipePath = recipePath, ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = folder, Status = status, Rerunnable = true, StartedAtUtc = "2026-10-01T00:00:00Z", EndedAtUtc = "2026-10-01T01:00:00Z" });
            return folder;
        }

        [Fact]
        public void TheRegistryNamesTheBaseOfARecipeAndWhetherThisTreeHoldsIt()
        {
            File.WriteAllText(_fx.Full("rounds/nursery/zz-based.json"), "{\"base\":\"rounds/nursery/brain2-w5.json\",\"name\":\"zz-based\"}");
            File.WriteAllText(_fx.Full("rounds/nursery/zz-orphan.json"), "{\"base\":\"rounds/nursery/not-here.json\",\"name\":\"zz-orphan\"}");
            Enter("with-base", "rounds/nursery/zz-based.json");
            Enter("no-base", "rounds/nursery/brain2-w5.json");
            Enter("orphan", "rounds/nursery/zz-orphan.json");
            Enter("no-recipe", "rounds/nursery/missing-recipe.json");
            JsonElement items = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Registry())).GetProperty("items");
            JsonElement Item(string name) => items.EnumerateArray().Single(i => i.GetProperty("name").GetString() == name);
            Assert.Equal("rounds/nursery/brain2-w5.json", Item("with-base").GetProperty("basePath").GetString());
            Assert.True(Item("with-base").GetProperty("baseExists").GetBoolean());
            Assert.Equal(JsonValueKind.Null, Item("no-base").GetProperty("basePath").ValueKind);
            Assert.False(Item("no-base").GetProperty("baseExists").GetBoolean());
            Assert.Equal("rounds/nursery/not-here.json", Item("orphan").GetProperty("basePath").GetString());
            Assert.False(Item("orphan").GetProperty("baseExists").GetBoolean());
            Assert.Equal(JsonValueKind.Null, Item("no-recipe").GetProperty("basePath").ValueKind);
        }

        [Fact]
        public void AnEpisodeOfARunWhoseExamPlanIsRefusedIsRefusedAndNamesNoEpisodeFolder()
        {
            // the entry's recipe hash is not the file's, so the plan is not rebuilt (it is not guessed): the plan is refused, and so is every episode of it
            Enter("planless", "rounds/nursery/brain2-w5.json");
            var resolver = new ResourceResolver(_fx.Room);
            Assert.Throws<ResourceRefused>(() => resolver.Read("nursery://exam-plan/1"));
            ResourceRefused e = Assert.Throws<ResourceRefused>(() => resolver.Read("nursery://episode/1/0/0/0"));
            Assert.Contains("no episode of run 1 can be read", e.Message);
            Assert.DoesNotContain("episodes", e.Message);
            Assert.DoesNotContain("b0-d0-t0", e.Message);
            Assert.DoesNotContain(_fx.Dir, e.Message);
            // a run that is not there is a refusal too (404), not a state
            Assert.Throws<ResourceNotFound>(() => resolver.Read("nursery://episode/no-such-run/0/0/0"));
        }

        private static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            Assert.NotEqual(5179, port);
            return port;
        }

        [Fact]
        public async Task ThroughTheRealPipelineTheRefusedEpisodeIsAnAnswerWithARefusedFieldAndNoFolderPath()
        {
            Enter("planless", "rounds/nursery/brain2-w5.json");
            int port = FreePort();
            RoomSettings settings = RoomSettings.Parse(Where.SettingsJson(runsDir: "runs").Replace("\"port\":5179", "\"port\":" + port).Replace("artifacts/Evosim.Nursery/bin/Debug/net8.0/Evosim.Nursery.dll", "bin/nursery.dll"));
            var room = new RoomContext(_fx.Root, settings, _fx.Main, _fx.Registry, Path.Combine(_fx.Dir, "activity-http.jsonl"), _fx.Launcher, _fx.Load, 32, _ => _fx.Build);
            WebApplication app = Program.Create(room, Where.WwwRoot);
            app.Start();
            try
            {
                using var http = new HttpClient();
                HttpResponseMessage r = await http.GetAsync($"http://127.0.0.1:{port}/api/resource?uri=" + Uri.EscapeDataString("nursery://episode/1/0/0/0"));
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                string text = await r.Content.ReadAsStringAsync();
                JsonElement b = Js.Parse(text);
                Assert.True(b.GetProperty("refused").GetBoolean());
                Assert.False(b.GetProperty("ok").GetBoolean());
                Assert.Contains("no episode of run 1 can be read", b.GetProperty("error").GetString());
                Assert.DoesNotContain("episodes", text);
                Assert.False(b.TryGetProperty("folder", out _));
                // and the new reads answer through it, none of them an empty object
                foreach (string uri in new[] { "nursery://activity/1", "nursery://registry" })
                {
                    JsonElement ok = Js.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/api/resource?uri=" + Uri.EscapeDataString(uri)));
                    Assert.False(ok.TryGetProperty("refused", out _), uri);
                    Assert.Empty(RoomResponseShapeTests.EmptyObjects(ok));
                }
            }
            finally { await app.StopAsync(); }
        }

        [Fact]
        public void TheExamCompareReadSetsTwoRunsExamsBesideEachOtherAndRefusesARunAsItsOwnControl()
        {
            string Make(string name, double[] own, double[] other)
            {
                string folder = Enter(name, "rounds/nursery/brain2-w5.json");
                var lines = new List<string>();
                for (int g = 0; g < own.Length; g++)
                {
                    lines.Add(Js.Serialize(new { brain = "own", genome = g, body = g, net = own[g] }));
                    lines.Add(Js.Serialize(new { brain = "frozen", genome = g, body = g, net = other[g] }));
                }
                File.WriteAllLines(Path.Combine(folder, "tank-exam.jsonl"), lines);
                return folder;
            }
            Make("one", new[] { 10.0, 12, 14 }, new[] { 5.0, 6, 7 });
            Make("two", new[] { 10.0, 10, 10 }, new[] { 10.0, 9, 8 });
            Enter("three", "rounds/nursery/brain2-w5.json");   // no exam rows
            var resolver = new ResourceResolver(_fx.Room);
            JsonElement c = Js.Parse(Js.Serialize(resolver.Read("nursery://exam-compare/1/vs/2")));
            Assert.True(c.GetProperty("available").GetBoolean());
            JsonElement brain = c.GetProperty("comparison").GetProperty("brains").EnumerateArray().Single();
            Assert.Equal("frozen", brain.GetProperty("label").GetString());
            Assert.Equal(6.0, brain.GetProperty("run").GetProperty("diff").GetDouble(), 9);
            Assert.Equal(1.0, brain.GetProperty("control").GetProperty("diff").GetDouble(), 9);
            Assert.True(brain.GetProperty("differs").GetBoolean());
            Assert.Empty(RoomResponseShapeTests.EmptyObjects(c));
            // by folder name too, and a control with no exam is a state, not a refusal
            Assert.True(Js.Parse(Js.Serialize(resolver.Read("nursery://exam-compare/one/vs/two"))).GetProperty("available").GetBoolean());
            JsonElement none = Js.Parse(Js.Serialize(resolver.Read("nursery://exam-compare/1/vs/3")));
            Assert.False(none.GetProperty("available").GetBoolean());
            Assert.Contains("control", none.GetProperty("reason").GetString());
            Assert.Throws<ResourceRefused>(() => resolver.Read("nursery://exam-compare/1/vs/1"));
            Assert.Throws<ResourceRefused>(() => resolver.Read("nursery://exam-compare/1"));
            Assert.Throws<ResourceNotFound>(() => resolver.Read("nursery://exam-compare/1/vs/nope"));
            // listed as a template, and the MCP lists the same
            Assert.Contains(resolver.Templates.Select(t => t.UriTemplate), t => t == "nursery://exam-compare/{run}/vs/{control}");
            Assert.Contains(resolver.Templates.Select(t => t.UriTemplate), t => t == "nursery://activity/{run}");
        }
    }
}
