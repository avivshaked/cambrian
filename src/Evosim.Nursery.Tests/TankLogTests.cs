using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// What the nursery writes for the tank and genome screens (TankLog.cs): member ids, who swam with whom, where each member came from,
    /// and the exam's plan in run.json. Nothing here swims an episode. The loop that calls these cannot run without swimming, so its
    /// pure parts are the things tested, and a test that reads the source says the loop uses them.
    /// </summary>
    public class TankLogTests
    {
        // ---------------------------------------------------------------- ids

        [Fact]
        public void AMemberIdIsTheGenerationAndTheIndexInThatGenerationsPopulation()
        {
            Assert.Equal("g0.0", TankLog.MemberId(0, 0));
            Assert.Equal("g12.345", TankLog.MemberId(12, 345));
            Assert.Equal("p7", TankLog.PoolId(7));
        }

        [Fact]
        public void TheIdsOfAGenerationAreDistinctAndFollowThePopulationsOrder()
        {
            string[] ids = Enumerable.Range(0, 100).Select(i => TankLog.MemberId(3, i)).ToArray();
            Assert.Equal(100, ids.Distinct().Count());
            Assert.Equal("g3.0", ids[0]);
            Assert.Equal("g3.99", ids[99]);
        }

        // ---------------------------------------------------------------- lineage

        private static string[] Parents(JsonNode row) => row["parents"].Items().Select(x => x.AsString()).ToArray();

        [Fact]
        public void AGenerationZeroMemberHasNoParentsAndIsNoElite()
        {
            JsonNode row = Json.Parse(TankLog.LineageRow(0, "g0.5", 2, 0, Array.Empty<string>(), elite: false));
            Assert.Equal(0, row["gen"].AsInt());
            Assert.Equal("g0.5", row["id"].AsString());
            Assert.Equal(2, row["body"].AsInt());
            Assert.Equal(0, row["born"].AsInt());
            Assert.Empty(Parents(row));
            Assert.False(row["elite"].AsBool());
        }

        [Fact]
        public void AnEliteCarriedUnchangedNamesItselfAGenerationAgoAsItsOneParentAndKeepsItsBirth()
        {
            // the elite was g4.9 and is g5.0: born in generation 2, which a carried copy does not change
            string[] parents = TankLog.CopyParents("g4.9");
            JsonNode row = Json.Parse(TankLog.LineageRow(5, "g5.0", 1, 2, parents, elite: true));
            Assert.Equal(new[] { "g4.9" }, Parents(row));
            Assert.True(row["elite"].AsBool());
            Assert.Equal(2, row["born"].AsInt());
            Assert.Equal("g5.0", row["id"].AsString());
        }

        [Fact]
        public void AMutatedCopyHasOneParentAndIsNoElite()
        {
            JsonNode row = Json.Parse(TankLog.LineageRow(5, "g5.20", 1, 5, TankLog.CopyParents("g4.3"), elite: false));
            Assert.Equal(new[] { "g4.3" }, Parents(row));
            Assert.False(row["elite"].AsBool());
            Assert.Equal(5, row["born"].AsInt());
        }

        [Fact]
        public void ACrossoverChildHasTwoParentsTheBaseFirst()
        {
            var passed = new CrossoverRecord { Transferred = true };
            // the base is the parent the tournament put second and the donor the first: the order is the base's, never the draw's
            string[] parents = TankLog.CrossParents("g4.8", "g4.2", passed);
            Assert.Equal(new[] { "g4.8", "g4.2" }, parents);
            JsonNode row = Json.Parse(TankLog.LineageRow(5, "g5.31", 0, 5, parents, elite: false));
            Assert.Equal(new[] { "g4.8", "g4.2" }, Parents(row));
        }

        [Fact]
        public void ACrossoverThatWasNotDrawnOrWasRefusedLeavesTheBaseAsTheOneParent()
        {
            Assert.Equal(new[] { "g4.8" }, TankLog.CrossParents("g4.8", "g4.2", null));
            Assert.Equal(new[] { "g4.8" }, TankLog.CrossParents("g4.8", "g4.2", new CrossoverRecord { Transferred = false, Refused = "no-route" }));
        }

        [Fact]
        public void ALineageRowIsOneLine()
        {
            string row = TankLog.LineageRow(1, "g1.0", 0, 1, new[] { "g0.1", "g0.2" }, elite: false);
            Assert.DoesNotContain("\n", row);
            Assert.DoesNotContain("\r", row);
            Assert.StartsWith("{\"gen\":1,\"id\":\"g1.0\"", row);
        }

        // ---------------------------------------------------------------- the draws

        /// <summary>The shuffle as <c>SwimTanks</c> wrote it before the plan was factored out, copied here so that the one in TankLog is held to it.</summary>
        private static int[][] LegacyTanks(int n, int m, ulong seed)
        {
            var order = Enumerable.Range(0, n).ToArray();
            var shuffle = new Rng(Rng.SeedFor(seed, 0x73687566UL));
            for (int k = n - 1; k > 0; k--)
            {
                int j = shuffle.Range(k + 1);
                (order[k], order[j]) = (order[j], order[k]);
            }
            var tanks = new List<int[]>();
            for (int at = 0; at < n; at += m) tanks.Add(order.Skip(at).Take(m).ToArray());
            return tanks.ToArray();
        }

        [Theory]
        [InlineData(0, 8)]
        [InlineData(1, 8)]
        [InlineData(7, 8)]
        [InlineData(8, 8)]
        [InlineData(20, 8)]
        [InlineData(100, 8)]
        [InlineData(100, 3)]
        [InlineData(50, 1)]
        public void ThePlanIsTheShuffleSwimTanksAlwaysDealt(int population, int tankSize)
        {
            ulong[] seeds = { 0UL, 1UL, 0x67656EUL, 0xFFFFFFFFFFFFFFFFUL, Rng.SeedFor(5, 0x67656EUL ^ 7) };
            int[][][] plan = TankLog.PlanTanks(population, tankSize, seeds);
            Assert.Equal(seeds.Length, plan.Length);
            for (int d = 0; d < seeds.Length; d++)
            {
                int[][] legacy = LegacyTanks(population, tankSize, seeds[d]);
                Assert.Equal(legacy.Length, plan[d].Length);
                for (int t = 0; t < legacy.Length; t++) Assert.Equal(legacy[t], plan[d][t]);
            }
        }

        [Fact]
        public void EveryMemberSwimsOnceInEveryDrawAndOnlyTheLastTankIsShort()
        {
            int[][][] plan = TankLog.PlanTanks(100, 8, new ulong[] { 11UL, 12UL, 13UL });
            foreach (int[][] draw in plan)
            {
                Assert.Equal(Enumerable.Range(0, 100), draw.SelectMany(t => t).OrderBy(i => i));
                Assert.Equal(13, draw.Length);
                Assert.All(draw.Take(12), t => Assert.Equal(8, t.Length));
                Assert.Equal(4, draw[12].Length);
            }
            // a different seed deals a different set of tanks
            Assert.NotEqual(plan[0].SelectMany(t => t), plan[1].SelectMany(t => t));
        }

        [Fact]
        public void ThePlanIsTheSameEveryTimeItIsAskedFor()
        {
            int[][][] a = TankLog.PlanTanks(40, 8, new ulong[] { 99UL, 100UL });
            int[][][] b = TankLog.PlanTanks(40, 8, new ulong[] { 99UL, 100UL });
            for (int d = 0; d < 2; d++)
                for (int t = 0; t < a[d].Length; t++) Assert.Equal(a[d][t], b[d][t]);
        }

        [Fact]
        public void ADrawsRowHoldsTheGenerationTheDrawTheSeedAsAStringAndEveryTankAsItsMemberIds()
        {
            string[] ids = Enumerable.Range(0, 10).Select(i => TankLog.MemberId(2, i)).ToArray();
            ulong seed = 0xFFFFFFFFFFFFFFF1UL;   // above 2^53: a JSON number would not hold it in a browser
            int[][] tanks = TankLog.PlanTanks(10, 4, new[] { seed })[0];
            string line = TankLog.DrawRow(2, 1, seed, tanks, ids);
            Assert.DoesNotContain("\n", line);
            JsonNode row = Json.Parse(line);
            Assert.Equal(2, row["gen"].AsInt());
            Assert.Equal(1, row["draw"].AsInt());
            Assert.Equal(seed.ToString(), row["seed"].AsString());
            JsonNode[] shown = row["tanks"].Items().ToArray();
            Assert.Equal(3, shown.Length);
            for (int t = 0; t < tanks.Length; t++)
                Assert.Equal(tanks[t].Select(i => ids[i]), shown[t].Items().Select(x => x.AsString()));
            // all ten members appear, each once
            Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal), shown.SelectMany(t => t.Items()).Select(x => x.AsString()).OrderBy(x => x, StringComparer.Ordinal));
        }

        [Fact]
        public void AnExamRowNamesTheBrainInPlaceOfTheGenerationAndTakesTheSameShape()
        {
            string[] ids = { "g50.3", "g50.8", "g50.1", "g50.9" };
            int[][] tanks = TankLog.PlanTanks(4, 2, new[] { 7UL })[0];
            JsonNode row = Json.Parse(TankLog.ExamDrawRow("knockout: scent", 3, 5, 7UL, tanks, ids));
            Assert.Equal("knockout: scent", row["brain"].AsString());
            Assert.Equal(3, row["b"].AsInt());   // the brain's index in the exam's brains, beside its label
            Assert.Equal(5, row["draw"].AsInt());
            Assert.Equal("7", row["seed"].AsString());
            Assert.Equal(2, row["tanks"].Items().Count());
            Assert.False(row.Has("gen"));
            Assert.Equal(new[] { "brain", "b", "draw", "seed", "tanks" }, row.Keys().ToArray());
        }

        [Fact]
        public void TwoBrainsWithOneLabelAreToldApartByTheirIndex()
        {
            string[] ids = { "p0", "p1" };
            int[][] tanks = TankLog.PlanTanks(2, 2, new[] { 1UL })[0];
            JsonNode a = Json.Parse(TankLog.ExamDrawRow("own", 0, 0, 1UL, tanks, ids));
            JsonNode b = Json.Parse(TankLog.ExamDrawRow("own", 1, 0, 1UL, tanks, ids));
            Assert.Equal(a["brain"].AsString(), b["brain"].AsString());
            Assert.NotEqual(a["b"].AsInt(), b["b"].AsInt());
        }

        [Theory]
        [InlineData("exam", "tank-exam-draws.jsonl")]
        [InlineData("bench", "tank-bench-draws.jsonl")]
        public void TheDrawsFileOfAnExamOrABenchIsNamedForIt(string name, string file)
        {
            Assert.Equal(file, TankLog.BenchOrExamDrawsFile(name));
            Assert.True(RegistryEntry.IsResult(file));
            Assert.Equal(TankLog.ExamDrawsFile, TankLog.BenchOrExamDrawsFile("exam"));
            Assert.Equal(TankLog.BenchDrawsFile, TankLog.BenchOrExamDrawsFile("bench"));
        }

        [Fact]
        public void ACrossoverOfAMemberWithItselfKeepsItsIdTwiceInParents()
        {
            // The tournament can hand the base back as the donor. The record is that the crossover ran: both parents, which are one member.
            var passed = new CrossoverRecord { Transferred = true };
            Assert.Equal(new[] { "g3.4", "g3.4" }, TankLog.CrossParents("g3.4", "g3.4", passed));
            JsonNode row = Json.Parse(TankLog.LineageRow(4, "g4.0", 1, 4, TankLog.CrossParents("g3.4", "g3.4", passed), elite: false));
            Assert.Equal(new[] { "g3.4", "g3.4" }, Parents(row));
            // not passed: the base alone, as for any copy
            Assert.Equal(new[] { "g3.4" }, TankLog.CrossParents("g3.4", "g3.4", new CrossoverRecord { Transferred = false }));
        }

        [Fact]
        public void TheSearchsFileAndTheShuffleAgreeForAGivenSeedAndPopulation()
        {
            // A population of 20 in tanks of 8, three draws: the file's rows are written from the plan, and the plan is the shuffle the
            // loop swims. Built here the way EvolveTank builds them, and compared with the legacy shuffle member by member.
            const int n = 20, m = 8;
            ulong[] seeds = Enumerable.Range(0, 3).Select(e => Rng.SeedFor(1UL ^ 0x67656EUL, (ulong)(4 * 1000 + e))).ToArray();
            string[] ids = Enumerable.Range(0, n).Select(i => TankLog.MemberId(4, i)).ToArray();
            int[][][] plan = TankLog.PlanTanks(n, m, seeds);
            var file = new StringWriter();
            for (int d = 0; d < seeds.Length; d++) TankLog.Append(file, TankLog.DrawRow(4, d, seeds[d], plan[d], ids));
            string[] lines = file.ToString().Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, lines.Length);
            for (int d = 0; d < 3; d++)
            {
                JsonNode row = Json.Parse(lines[d]);
                Assert.Equal(d, row["draw"].AsInt());
                Assert.Equal(seeds[d].ToString(), row["seed"].AsString());
                int[][] legacy = LegacyTanks(n, m, seeds[d]);
                Assert.Equal(
                    legacy.Select(t => string.Join(",", t.Select(i => ids[i]))),
                    row["tanks"].Items().Select(t => string.Join(",", t.Items().Select(x => x.AsString()))));
            }
        }

        // ---------------------------------------------------------------- the source

        private static string TankSource()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string file = Path.Combine(dir.FullName, "src", "Evosim.Nursery", "Tank.cs");
                if (File.Exists(file)) return File.ReadAllText(file);
            }
            throw new FileNotFoundException("src/Evosim.Nursery/Tank.cs was not found above the test program");
        }

        [Fact]
        public void TheLoopDealsItsTanksOnlyByThePlanTheFilesAreWrittenFrom()
        {
            string source = TankSource();
            // the shuffle's salt lives in TankLog alone: a second copy in the loop would be a second shuffle the file does not describe
            Assert.DoesNotContain("0x73687566", source);
            Assert.Contains("TankLog.PlanTanks(", source);
            // the exam names its tanks too, and the search its draws and its lineage
            Assert.Contains("TankLog.ExamDrawRow(", source);
            Assert.Contains("TankLog.DrawRow(", source);
            Assert.Contains("TankLog.LineageRow(", source);
        }

        // ---------------------------------------------------------------- run.json and the registry

        [Fact]
        public void TheExamsPlanIsWrittenIntoTheRunRecordInOrderWithSeedsAsStrings()
        {
            var record = new RunRecord
            {
                RecipeHash = null, Seed = 1, EpisodeConfigJson = "{}",
                Exam = new ExamPlan
                {
                    Brains = new List<ExamPlan.BrainEntry>
                    {
                        new ExamPlan.BrainEntry { Label = "own", Kind = "own" },
                        new ExamPlan.BrainEntry { Label = "frozen", Kind = "frozen" },
                        new ExamPlan.BrainEntry { Label = "full power", Kind = "full" },
                    },
                    Genomes = new List<ExamPlan.GenomeEntry>
                    {
                        new ExamPlan.GenomeEntry { Id = "g50.7", Body = 2 },
                        new ExamPlan.GenomeEntry { Id = "g50.2", Body = 0 },
                    },
                    Episodes = 8, Salt = 0x62656E6368UL, Seeds = new[] { 0xFFFFFFFFFFFFFFF1UL, 5UL },
                },
            };
            JsonNode exam = Json.Parse(record.Render())["exam"];
            Assert.Equal(new[] { "own", "frozen", "full power" }, exam["brains"].Items().Select(b => b["label"].AsString()));
            Assert.Equal(new[] { "own", "frozen", "full" }, exam["brains"].Items().Select(b => b["kind"].AsString()));
            Assert.Equal(new[] { "g50.7", "g50.2" }, exam["genomes"].Items().Select(g => g["id"].AsString()));
            Assert.Equal(new[] { 2, 0 }, exam["genomes"].Items().Select(g => g["body"].AsInt()));
            Assert.Equal(8, exam["episodes"].AsInt());
            // the salt is a 64-bit number as the seeds are: a string, so that a browser does not round it
            Assert.Equal("422608528232", exam["salt"].AsString());
            Assert.Equal("exam", exam["name"].AsString());
            Assert.Equal(new[] { "18446744073709551601", "5" }, exam["seeds"].Items().Select(x => x.AsString()));
        }

        [Fact]
        public void ABenchsPlanSaysItIsABenchAndAHighSaltSurvivesAsAString()
        {
            var record = new RunRecord
            {
                Seed = 1, EpisodeConfigJson = "{}",
                Exam = new ExamPlan { Name = "bench", Episodes = 2, Salt = 0xFFFFFFFFFFFFFFF1UL, Seeds = new[] { 3UL } },
            };
            JsonNode exam = Json.Parse(record.Render())["exam"];
            Assert.Equal("bench", exam["name"].AsString());
            Assert.Equal("18446744073709551601", exam["salt"].AsString());
        }

        [Fact]
        public void ARecordWithNoExamPlanHasNoExamField()
        {
            var record = new RunRecord { Seed = 1, EpisodeConfigJson = "{}" };
            Assert.False(Json.Parse(record.Render()).Has("exam"));
        }

        [Theory]
        [InlineData("tank-draws.jsonl")]
        [InlineData("tank-exam-draws.jsonl")]
        [InlineData("tank-bench-draws.jsonl")]
        [InlineData("tank-lineage.jsonl")]
        public void TheThreeNewFilesAreResultsThatRepeatBitForBit(string name)
        {
            Assert.True(RegistryEntry.IsResult(name));
            Assert.Equal(name, name == TankLog.DrawsFile ? TankLog.DrawsFile : name == TankLog.ExamDrawsFile ? TankLog.ExamDrawsFile : name == TankLog.BenchDrawsFile ? TankLog.BenchDrawsFile : TankLog.LineageFile);
        }

        [Fact]
        public void TheRunRecordIsStillNoResult()
        {
            Assert.False(RegistryEntry.IsResult("run.json"));
            Assert.False(RegistryEntry.IsResult("timing.jsonl"));
        }
    }
}
