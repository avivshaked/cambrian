using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using ModelContextProtocol;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// The exam plan resource (nursery://exam-plan/{run}): the pilot's plan rebuilt from registry entry 1, a recorded plan with its draws file, a recipe whose
    /// hash is not the entry's, and a run that cannot be rebuilt. Nothing swims.
    /// </summary>
    public class ExamPlanTests : IDisposable
    {
        private readonly string _dir = Where.Fresh("examplan");
        public void Dispose() => Directory.Delete(_dir, true);

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        // The numbers below were worked out once with an independent port of the formulas (scratch work, not this code):
        //   seed e of an exam = SplitMix64 finaliser of ((seed ^ salt) * 0x9E3779B97F4A7C15 + (generation * 1000 + e)),
        //   a draw's tanks = Fisher-Yates over PCG-XSH-RR 64/32 seeded SeedFor(draw seed, 0x73687566), cut into tanks.
        private const string Seed0 = "4691847893231451394", Seed1 = "762935761760705462", Seed2 = "10010438469680113871", Seed31 = "9524339634574736232";

        // ------------------------------------------------------------------ the seeds

        [Fact]
        public void TheSeedsAreThePinnedValuesOfTheFormula()
        {
            // The pilot: seed 1 (brain2.json), salt 0x62656e6368 ("bench"), so the stream is 1 ^ salt = 0x62656e6369.
            ulong[] s = ExamPlans.SeedsFor(1UL, 0x62656E6368UL, 0, 32);
            Assert.Equal(32, s.Length);
            Assert.Equal(Seed0, s[0].ToString());
            Assert.Equal(Seed1, s[1].ToString());
            Assert.Equal(Seed2, s[2].ToString());
            Assert.Equal(Seed31, s[31].ToString());
            // Another seed and salt, and a search generation: the index is generation * 1000 + e.
            Assert.Equal("6057843808676189187", ExamPlans.SeedsFor(0UL, 0UL, 3, 6)[5].ToString());
            ulong[] t = ExamPlans.SeedsFor(0x1234UL, 0xFFFFFFFFFFFFFFF1UL, 0, 2);
            Assert.Equal("17964135087863474760", t[0].ToString());
            Assert.Equal("9947916469830849084", t[1].ToString());
        }

        [Fact]
        public void TheNurserysOwnDealMatchesThePinnedTanks()
        {
            int[][][] plan = TankLog.PlanTanks(10, 4, new[] { 7UL, 8UL });
            Assert.Equal(Pinned, plan);
        }

        /// <summary>Pool 10, tanks of 4, seeds 7 and 8: [draw][tank][slot] = pool index.</summary>
        private static readonly int[][][] Pinned =
        {
            new[] { new[] { 2, 7, 9, 6 }, new[] { 0, 3, 5, 1 }, new[] { 8, 4 } },
            new[] { new[] { 6, 1, 8, 3 }, new[] { 9, 4, 7, 5 }, new[] { 0, 2 } },
        };

        // ------------------------------------------------------------------ the pilot, rebuilt from registry entry 1

        [RealRunFact("pilot-deep-brain2/tank-exam-genomes.tsv", "pilot-deep-brain2/tank-exam.jsonl", "pilot-deep-brain2/nursery.log")]
        public void ThePilotsPlanIsRebuiltFromItsRegistryEntry()
        {
            // The registry lives in the main checkout, which holds the pilot's folder: found as the real-run facts find it.
            string main = RealRunFactAttribute.Find("pilot-deep-brain2/nursery.log");
            var room = new RoomContext(Where.Root, Where.Settings("scratch/nursery/pilot-deep-brain2"), main);
            var r = new ResourceResolver(room);

            // By its registry number and by its folder name: the same plan.
            ExamPlanView byEntry = (ExamPlanView)r.Read("nursery://exam-plan/1");
            ExamPlanView byFolder = (ExamPlanView)r.Read("nursery://exam-plan/pilot-deep-brain2");
            Assert.Equal(Js.Serialize(byEntry).Replace("\"run\":\"1\"", "").Replace("\"run\":\"pilot-deep-brain2\"", ""), Js.Serialize(byFolder).Replace("\"run\":\"1\"", "").Replace("\"run\":\"pilot-deep-brain2\"", ""));

            ExamPlanView p = byEntry;
            Assert.Equal("reconstruction", p.Source);
            Assert.Equal("exam", p.Name);
            Assert.Equal(1, p.Entry);
            Assert.Equal("1", p.RunSeed);
            Assert.Equal("422608528232", p.Salt);
            Assert.Equal(32, p.Episodes);
            Assert.Equal(8, p.TankSize);

            // 18 brains, in the recipe's order; the label at index 3 is the recipe's, and the run's rows carry another.
            Assert.Equal(18, p.Brains.Count);
            Assert.Equal(Enumerable.Range(0, 18), p.Brains.Select(b => b.Index));
            Assert.Equal("own", p.Brains[0].Label);
            Assert.Equal("knockout Chemical", p.Brains[3].Label);
            Assert.Equal("knockout", p.Brains[3].RunLabel);
            Assert.True(p.Brains[3].LabelDiffers);
            Assert.Contains("labelled knockout where the recipe's is knockout Chemical", p.Brains[3].KnownDifference);
            Assert.Equal(new[] { 3 }, p.Brains.Where(b => b.LabelDiffers).Select(b => b.Index).ToArray());
            Assert.All(p.Brains, b => Assert.NotNull(b.RunLabel));
            Assert.Equal("constant 0.3", p.Brains[11].Label);
            Assert.Equal(0.3, Js.Num(p.Brains[11].Control, "power").Value, 5);
            Assert.Equal("Chemical", Js.Items(Js.At(p.Brains[3].Control, "channels")).Single().GetString());

            // Six genomes, bodies 0, 1, 3, 4, 8 and 10, in pool order.
            Assert.Equal(new[] { 0, 1, 3, 4, 8, 10 }, p.Genomes.Select(g => g.Body).ToArray());
            Assert.Equal(new[] { "exam.0", "exam.1", "exam.2", "exam.3", "exam.4", "exam.5" }, p.Genomes.Select(g => g.Id).ToArray());

            // 32 seeds, as strings, the first three pinned.
            Assert.Equal(32, p.Seeds.Count);
            Assert.Equal(new[] { Seed0, Seed1, Seed2 }, p.Seeds.Take(3).ToArray());
            Assert.Equal(Seed31, p.Seeds[31]);

            // One tank of all six per draw, dealt by the nursery's own shuffle.
            Assert.Equal(32, p.Draws.Count);
            Assert.All(p.Draws, d =>
            {
                Assert.Single(d.Tanks);
                Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, d.Tanks[0].OrderBy(x => x).ToArray());
                Assert.Equal("reconstructed", d.TanksFrom);
            });
            Assert.Equal(new[] { 1, 2, 0, 5, 3, 4 }, p.Draws[0].Tanks[0].ToArray());
            Assert.Equal(new[] { 0, 4, 3, 5, 1, 2 }, p.Draws[1].Tanks[0].ToArray());
            Assert.Equal(new[] { 3, 4, 5, 2, 0, 1 }, p.Draws[2].Tanks[0].ToArray());
            Assert.Equal(new[] { 2, 0, 5, 4, 1, 3 }, p.Draws[31].Tanks[0].ToArray());
            Assert.Equal(Seed0, p.Draws[0].Seed);

            // Every check the plan makes of itself held: the recipe's hash, the log's line, the rows' shape and the labels.
            Assert.All(p.Checks, c => Assert.True(c.Ok, c.Name + ": " + c.Detail));
            Assert.Equal(new[] { "recipe", "nursery.log", "exam rows", "brain labels" }, p.Checks.Select(c => c.Name).ToArray());
            Assert.Contains("labelled knockout where the recipe's is knockout Chemical", string.Join("|", p.KnownDifferences));

            // Where each input came from is said, for each of the four.
            Assert.Equal(new[] { "brains", "genomes", "seeds", "tanks" }, p.Inputs.Select(i => i.Input).ToArray());
            Assert.Contains("brain2.json", p.Inputs[0].From);

            // The wire form: seeds are strings, so a browser never rounds them.
            JsonElement j = Js.Parse(Js.Serialize(p));
            Assert.Equal(JsonValueKind.String, j.GetProperty("seeds")[0].ValueKind);
            Assert.Equal(JsonValueKind.String, j.GetProperty("draws")[0].GetProperty("seed").ValueKind);
            Assert.Equal(Seed0, j.GetProperty("draws")[0].GetProperty("seed").GetString());
        }

        [RealRunFact("pilot-deep-brain2/nursery.log")]
        public void ThePlanIsServedThroughTheMcpAndTheApisSharedResolver()
        {
            string main = RealRunFactAttribute.Find("pilot-deep-brain2/nursery.log");
            var r = new ResourceResolver(new RoomContext(Where.Root, Where.Settings("scratch/nursery/pilot-deep-brain2"), main));
            Assert.Contains(r.Templates.Select(t => t.UriTemplate), t => t == "nursery://exam-plan/{run}");
            Assert.Contains(r.List().Select(x => x.Uri), u => u == "nursery://exam-plan/1");
            Assert.Equal(r.Templates.Select(t => t.UriTemplate).ToArray(), McpSurface.Templates(r).Select(t => t.UriTemplate).ToArray());
            var read = McpSurface.Read(r, "nursery://exam-plan/1");
            JsonElement j = Js.Parse(((ModelContextProtocol.Protocol.TextResourceContents)read.Contents[0]).Text);
            Assert.Equal(32, j.GetProperty("seeds").GetArrayLength());
            Assert.Equal(18, j.GetProperty("brains").GetArrayLength());
        }

        // ------------------------------------------------------------------ a run that recorded its plan (3b)

        private static string IdOf(int index) => "g5." + ((index * 7) % 10);

        /// <summary>A synthetic 3b run: ten genomes (ids in a scrambled order), two brains, seeds 7 and 8, tanks of 4, and the draws file the nursery writes.</summary>
        private string Recorded(Action<List<string>> tweakDraws = null, bool draws = true, bool tankSizeInRecord = true)
        {
            string folder = Path.Combine(_dir, "rec" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(folder);
            string ids = string.Join(",", Enumerable.Range(0, 10).Select(i => "{\"id\":\"" + IdOf(i) + "\",\"body\":" + (i % 3) + "}"));
            string setup = tankSizeInRecord ? "\"setup\":{\"tankSize\":4}," : "";
            File.WriteAllText(Path.Combine(folder, "run.json"),
                "{\"status\":\"completed\",\"seed\":1,\"recipe\":{\"path\":\"x\",\"name\":\"x\",\"hash\":\"x\",\"resolved\":{" + setup +
                "\"exam\":{\"episodes\":2,\"salt\":\"0x9\",\"controls\":[{\"kind\":\"own\"},{\"kind\":\"constant\",\"power\":0.5}]}}}," +
                "\"exam\":{\"name\":\"exam\",\"brains\":[{\"label\":\"own\",\"kind\":\"own\"},{\"label\":\"constant 0.5\",\"kind\":\"constant\"}],\"genomes\":[" + ids + "],\"episodes\":2,\"salt\":\"9\",\"seeds\":[\"7\",\"8\"]}}", Utf8);
            if (draws)
            {
                var rows = new List<string>();
                string[] labels = { "own", "constant 0.5" };
                var idList = Enumerable.Range(0, 10).Select(IdOf).ToArray();
                for (int b = 0; b < 2; b++)
                    for (int d = 0; d < 2; d++)
                        rows.Add(TankLog.ExamDrawRow(labels[b], b, d, d == 0 ? 7UL : 8UL, Pinned[d], idList));
                tweakDraws?.Invoke(rows);
                File.WriteAllLines(Path.Combine(folder, TankLog.ExamDrawsFile), rows, Utf8);
            }
            return folder;
        }

        private ResourceResolver Resolver(string folder, string registry = null)
        {
            string rel = Path.GetRelativePath(Where.Root, folder).Replace('\\', '/');
            return new ResourceResolver(new RoomContext(Where.Root, Where.Settings(rel), Where.Root, registry ?? Path.Combine(_dir, "none.jsonl")));
        }

        [Fact]
        public void ARecordedPlanIsReadFromRunJsonAndItsDrawsFileAndTheReconstructionEqualsIt()
        {
            string folder = Recorded();
            var p = (ExamPlanView)Resolver(folder).Read("nursery://exam-plan/" + Path.GetFileName(folder));
            Assert.Equal("record", p.Source);
            Assert.Equal("exam", p.Name);
            Assert.Null(p.Entry);
            Assert.Equal("1", p.RunSeed);
            Assert.Equal("9", p.Salt);
            Assert.Equal(4, p.TankSize);
            Assert.Equal(new[] { "own", "constant 0.5" }, p.Brains.Select(b => b.Label).ToArray());
            Assert.Equal(0.5, Js.Num(p.Brains[1].Control, "power").Value, 5);
            Assert.Equal(new[] { "7", "8" }, p.Seeds.ToArray());
            Assert.Equal(Enumerable.Range(0, 10).Select(IdOf).ToArray(), p.Genomes.Select(g => g.Id).ToArray());

            // Every draw's tanks are the recorded ones (as pool indices, by id), and they are the pinned deal.
            Assert.Equal(2, p.Draws.Count);
            for (int d = 0; d < 2; d++)
            {
                Assert.Equal("recorded", p.Draws[d].TanksFrom);
                Assert.Equal(Pinned[d].Select(t => t.ToArray()).ToArray(), p.Draws[d].Tanks.Select(t => t.ToArray()).ToArray());
            }
            // And the reconstruction by PlanTanks equals the recorded file for every draw: the check says so.
            ExamCheckView agree = p.Checks.Single(c => c.Name == "tanks against TankLog.PlanTanks");
            Assert.True(agree.Ok, agree.Detail);
            Assert.Contains("all 2 recorded draws", agree.Detail);
            Assert.True(p.Checks.Single(c => c.Name == "draws file").Ok);
        }

        [Fact]
        public void ARecordedPlanWithNoDrawsFileDealsTheTanksAndSaysSo()
        {
            string folder = Recorded(draws: false);
            var p = (ExamPlanView)Resolver(folder).Read("nursery://exam-plan/" + Path.GetFileName(folder));
            Assert.All(p.Draws, d => Assert.Equal("reconstructed", d.TanksFrom));
            Assert.Equal(Pinned[1].Select(t => t.ToArray()).ToArray(), p.Draws[1].Tanks.Select(t => t.ToArray()).ToArray());
            Assert.Contains(p.Notes, n => n.Contains("wrote no tank-exam-draws.jsonl"));
        }

        [Fact]
        public void ADrawsFileThatDisagreesWithTheDealIsReportedAndItsTanksAreShown()
        {
            // Draw 0's first two members swapped in the file, for both brains: the record and the reconstruction differ.
            string folder = Recorded(rows =>
            {
                for (int k = 0; k < rows.Count; k++)
                    if (rows[k].Contains("\"draw\":0")) rows[k] = rows[k].Replace("\"" + IdOf(2) + "\",\"" + IdOf(7) + "\"", "\"" + IdOf(7) + "\",\"" + IdOf(2) + "\"");
            });
            var p = (ExamPlanView)Resolver(folder).Read("nursery://exam-plan/" + Path.GetFileName(folder));
            ExamCheckView c = p.Checks.Single(x => x.Name == "tanks against TankLog.PlanTanks");
            Assert.False(c.Ok);
            Assert.Contains("differs", c.Detail);
            Assert.Equal(new[] { 7, 2, 9, 6 }, p.Draws[0].Tanks[0].ToArray());
            Assert.Equal("recorded", p.Draws[0].TanksFrom);
        }

        [Fact]
        public void ADrawsFileTheBrainsDisagreeInIsNotUsed()
        {
            string folder = Recorded(rows =>
            {
                // Brain 1's draw 1 names the members in another order than brain 0's.
                int k = rows.FindIndex(x => x.Contains("\"b\":1") && x.Contains("\"draw\":1"));
                rows[k] = rows[k].Replace("\"" + IdOf(6) + "\",\"" + IdOf(1) + "\"", "\"" + IdOf(1) + "\",\"" + IdOf(6) + "\"");
            });
            var p = (ExamPlanView)Resolver(folder).Read("nursery://exam-plan/" + Path.GetFileName(folder));
            ExamCheckView c = p.Checks.Single(x => x.Name == "draws file");
            Assert.False(c.Ok);
            Assert.Contains("differ from another brain's", c.Detail);
            Assert.All(p.Draws, d => Assert.Equal("reconstructed", d.TanksFrom));
        }

        [Fact]
        public void ARecordedPlanWithNoTankSizeAnywhereDealsNoTank()
        {
            string folder = Recorded(draws: false, tankSizeInRecord: false);
            var p = (ExamPlanView)Resolver(folder).Read("nursery://exam-plan/" + Path.GetFileName(folder));
            Assert.Null(p.TankSize);
            Assert.All(p.Draws, d => Assert.Null(d.Tanks));
            Assert.Contains(p.Notes, n => n.Contains("tank size is not known"));
        }

        [Fact]
        public void ARecordWithMoreEpisodesThanSeedsIsRefused()
        {
            string folder = Recorded(draws: false);
            string path = Path.Combine(folder, "run.json");
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"seeds\":[\"7\",\"8\"]", "\"seeds\":[\"7\"]"), Utf8);
            var e = Assert.Throws<ResourceRefused>(() => Resolver(folder).Read("nursery://exam-plan/" + Path.GetFileName(folder)));
            Assert.Contains("lists 1 seeds for 2 episodes", e.Message);
        }

        // ------------------------------------------------------------------ the reconstruction's refusals

        private static string RealRecipe => "rounds/nursery/brain2-w5.json";

        private string Registry(string recipeHash, string folder)
        {
            string path = Path.Combine(_dir, "registry-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".jsonl");
            var e = new RegistryEntry
            {
                Recipe = "brain2", RecipeHash = recipeHash, RecipePath = RealRecipe, ConfigHash = "x", GitCommit = "x", CoreHash = "x", DynamicsHash = "x", FarmHash = "x", NurseryHash = "x",
                Out = folder, Status = "completed", StartedAtUtc = "x", EndedAtUtc = "x", NotRerunnableBecause = "x",
            };
            File.WriteAllText(path, e.ToJson() + "\n", Utf8);
            return path;
        }

        /// <summary>A pool of two genomes (their text is not read by the plan) for a run with no record.</summary>
        private string OldRun()
        {
            string folder = Path.Combine(_dir, "old" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "tank-exam-genomes.tsv"), "3\t{}\n7\t{}\n", Utf8);
            return folder;
        }

        [Fact]
        public void ARecipeWhoseHashIsNotTheEntrysIsRefusedAndSaysSo()
        {
            string folder = OldRun();
            string wrong = new string('0', 64);
            var r = Resolver(folder, Registry(wrong, folder));
            var e = Assert.Throws<ResourceRefused>(() => r.Read("nursery://exam-plan/1"));
            Assert.Contains("hashes to", e.Message);
            Assert.Contains(wrong, e.Message);
            Assert.Contains("registry entry 1", e.Message);
            Assert.Contains("would be guesses", e.Message);
        }

        [Fact]
        public void ARecipeWhoseHashIsTheEntrysRebuildsAPlanFromAPoolOfTwo()
        {
            string folder = OldRun();
            string hash = Recipe.Load(Path.Combine(Where.Root, "rounds", "nursery", "brain2-w5.json")).Hash();
            var r = Resolver(folder, Registry(hash, folder));
            var p = (ExamPlanView)r.Read("nursery://exam-plan/1");
            Assert.Equal("reconstruction", p.Source);
            Assert.Equal(new[] { 3, 7 }, p.Genomes.Select(g => g.Body).ToArray());
            Assert.Equal(18, p.Brains.Count);
            Assert.Equal(32, p.Seeds.Count);
            // Two genomes in tanks of 8: one tank of two, in the order the nursery's shuffle gives.
            Assert.All(p.Draws, d => Assert.Equal(new[] { 0, 1 }, d.Tanks.Single().OrderBy(x => x).ToArray()));
            // No exam rows and no log here: said, and nothing failed.
            Assert.All(p.Checks, c => Assert.True(c.Ok, c.Name + ": " + c.Detail));
            Assert.Contains(p.Notes, n => n.Contains("tank-exam.jsonl is not in the run"));
        }

        [Fact]
        public void ARunWithNoEntryAndNoPlanSaysItCannotBeRebuiltAndWhy()
        {
            string folder = OldRun();
            var e = Assert.Throws<ResourceRefused>(() => Resolver(folder).Read("nursery://exam-plan/" + Path.GetFileName(folder)));
            Assert.Contains("cannot be rebuilt", e.Message);
            Assert.Contains("holds no run.json", e.Message);
            Assert.Contains("no registry entry names this run's folder", e.Message);
        }

        [Fact]
        public void ARunJsonWithNoExamPlanAndNoEntryIsRefusedNamingThatToo()
        {
            string folder = OldRun();
            File.WriteAllText(Path.Combine(folder, "run.json"), "{\"status\":\"running\"}", Utf8);
            var e = Assert.Throws<ResourceRefused>(() => Resolver(folder).Read("nursery://exam-plan/" + Path.GetFileName(folder)));
            Assert.Contains("run.json holds no exam plan", e.Message);
        }

        [Fact]
        public void AnEntryWhosePoolFileIsMissingIsRefusedNamingTheFile()
        {
            string folder = OldRun();
            File.Delete(Path.Combine(folder, "tank-exam-genomes.tsv"));
            string hash = Recipe.Load(Path.Combine(Where.Root, "rounds", "nursery", "brain2-w5.json")).Hash();
            var e = Assert.Throws<ResourceRefused>(() => Resolver(folder, Registry(hash, folder)).Read("nursery://exam-plan/1"));
            Assert.Contains("tank-exam-genomes.tsv is not in the run's folder", e.Message);
        }

        [Fact]
        public void AnUnknownRunIsNotFound()
        {
            Assert.Throws<ResourceNotFound>(() => Resolver(OldRun()).Read("nursery://exam-plan/nosuchrun"));
            Assert.Throws<ResourceRefused>(() => Resolver(OldRun()).Read("nursery://exam-plan/"));
        }
    }
}
