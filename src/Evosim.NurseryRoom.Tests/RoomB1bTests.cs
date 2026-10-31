using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Evosim.Core;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// Round B1b: the exam against a control body by body, the band's exam progress, the greyed controls and the episode address. The numbers are hand-worked.
    /// </summary>
    public class ExamBodiesTests
    {
        // one row per (brain, genome): a body, the draws of that brain's net (per) or only a net
        private static ExamTable.Result Exam(params object[] rows) =>
            ExamTable.Build(rows.Select(r => Js.Parse(Js.Serialize(r))).ToList(), null, true);

        private static object Own(int genome, int body, double[] per) => new { brain = "own", genome, body, net = per.Average(), per };
        private static object OwnNet(int genome, int body, double net) => new { brain = "own", genome, body, net };
        private static object Other(int genome, int body, double[] per) => new { brain = "k", genome, body, net = per.Average(), per };

        [Fact]
        public void EachBodysOwnNetIsTheMeanOfItsDrawsWithTheStandardErrorOfThatMean()
        {
            // body 3, one genome, draws 10, 12, 14: mean 12, sample variance (4 + 0 + 4) / 2 = 4, se = sqrt(4 / 3) = 1.154701.
            // the control swam body 3 with draws 20, 20: mean 20 and variance 0, so a standard error of 0.
            var run = Exam(Own(0, 3, new[] { 10.0, 12, 14 }), Other(0, 3, new[] { 1.0, 1, 1 }));
            var control = Exam(Own(0, 3, new[] { 20.0, 20 }), Other(0, 3, new[] { 1.0, 1 }));
            ExamBodyCompare b = ExamCompare.Build(run, control).Bodies.Single();
            Assert.Equal(3, b.Body);
            Assert.Equal(12.0, b.Run.Net.Value, 9);
            Assert.Equal(Math.Sqrt(4.0 / 3), b.Run.Se.Value, 9);
            Assert.Equal(3, b.Run.N);
            Assert.Equal("draws", b.Run.Over);
            Assert.Equal(20.0, b.Control.Net.Value, 9);
            Assert.Equal(0.0, b.Control.Se.Value, 9);
        }

        [Fact]
        public void SeveralGenomesOfOneBodyAreOneBodyAndASingleNumberHasNoStandardError()
        {
            // two genomes of body 5, each with draws 2, 4 and 6, 8: pooled draws 2, 4, 6, 8, mean 5, variance (9 + 1 + 1 + 9) / 3 = 20/3, se = sqrt(20 / 3 / 4)
            var run = Exam(Own(0, 5, new[] { 2.0, 4 }), Own(1, 5, new[] { 6.0, 8 }), Other(0, 5, new[] { 0.0, 0 }), Other(1, 5, new[] { 0.0, 0 }));
            ExamBodySide s = ExamCompare.Build(run, run).Bodies.Single().Run;
            Assert.Equal(5.0, s.Net.Value, 9);
            Assert.Equal(Math.Sqrt(20.0 / 3 / 4), s.Se.Value, 9);
            Assert.Equal(4, s.N);
            Assert.Equal(2, s.Genomes);

            // rows with no draws (only a net): the genomes' nets are the numbers, 10 and 14 for body 7: mean 12, variance 8, se 2; one genome alone has none
            var nets = Exam(OwnNet(0, 7, 10), OwnNet(1, 7, 14), OwnNet(2, 8, 5));
            ExamComparison c = ExamCompare.Build(nets, nets);
            ExamBodySide seven = c.Bodies.Single(x => x.Body == 7).Run, eight = c.Bodies.Single(x => x.Body == 8).Run;
            Assert.Equal(12.0, seven.Net.Value, 9);
            Assert.Equal(2.0, seven.Se.Value, 9);
            Assert.Equal("genomes", seven.Over);
            Assert.Equal(5.0, eight.Net.Value, 9);
            Assert.Null(eight.Se);
        }

        [Fact]
        public void BodiesOneRunSwamAndTheOtherDidNotAreSaidSoAndKeepTheirOwnSide()
        {
            var run = Exam(Own(0, 0, new[] { 1.0, 3 }), Own(1, 1, new[] { 2.0, 4 }), Own(2, 4, new[] { 5.0, 7 }));
            var control = Exam(Own(0, 1, new[] { 10.0, 12 }), Own(1, 9, new[] { 6.0, 8 }));
            ExamComparison c = ExamCompare.Build(run, control);
            Assert.Equal(new[] { 0, 1, 4, 9 }, c.Bodies.Select(b => b.Body).ToArray());
            Assert.Equal(new[] { 0, 4 }, c.RunOnlyBodies);
            Assert.Equal(new[] { 9 }, c.ControlOnlyBodies);
            Assert.Null(c.Bodies.Single(b => b.Body == 0).Control);
            Assert.Null(c.Bodies.Single(b => b.Body == 9).Run);
            Assert.NotNull(c.Bodies.Single(b => b.Body == 1).Run);
            Assert.NotNull(c.Bodies.Single(b => b.Body == 1).Control);
            Assert.Equal("the two runs' bodies differ: bodies 0, 4 are in this run's exam only; body 9 is in the control's exam only", c.BodiesNote);
            // the same bodies on both sides: nothing to say
            Assert.Null(ExamCompare.Build(run, run).BodiesNote);
        }

        [Fact]
        public void ARowThatNamesNoBodyIsLeftOutAndCounted()
        {
            var run = Exam(new { brain = "own", genome = 0, net = 3.0, per = new[] { 3.0, 3 } }, Own(1, 2, new[] { 1.0, 1 }));
            ExamComparison c = ExamCompare.Build(run, run);
            Assert.Equal(new[] { 2 }, c.Bodies.Select(b => b.Body).ToArray());
            Assert.Equal(1, c.RunUnplaced);
            Assert.Contains("name no body", c.BodiesNote);
        }

        [Fact]
        public void TheComparisonIsServedWithItsBodiesAsPropertiesNotAnEmptyObject()
        {
            var run = Exam(Own(0, 3, new[] { 10.0, 12, 14 }));
            JsonElement j = Js.Parse(Js.Serialize(ExamCompare.Build(run, run)));
            JsonElement b = j.GetProperty("bodies")[0];
            Assert.Equal(3, b.GetProperty("body").GetInt32());
            Assert.Equal(12.0, b.GetProperty("run").GetProperty("net").GetDouble(), 9);
            Assert.True(b.GetProperty("control").GetProperty("se").GetDouble() > 0);
            Assert.Equal(JsonValueKind.Null, j.GetProperty("bodiesNote").ValueKind);
        }
    }

    public class EpisodeAddressTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose() => _f.Dispose();

        [Fact]
        public void ABrainADrawOrATankTheRunDoesNotHaveIsRefusedInTheWordsTheTankScreenUses()
        {
            // the fixture's exam is the pilot's: 18 brains, 32 draws, one tank a draw
            Assert.Equal("the address asked for brain 18, but this run has 18 brains (0 to 17)", Assert.Throws<ResourceRefused>(() => _f.Read(18, 0)).Message);
            Assert.Equal("the address asked for brain 99, but this run has 18 brains (0 to 17)", Assert.Throws<ResourceRefused>(() => _f.Read(99, 0)).Message);
            Assert.Equal("the address asked for draw 32, but this run has 32 draws (0 to 31)", Assert.Throws<ResourceRefused>(() => _f.Read(0, 32)).Message);
            Assert.Equal("the address asked for tank 1, but this run has 1 tank (0 to 0)", Assert.Throws<ResourceRefused>(() => _f.Read(0, 0, 1)).Message);
            // the brain is looked at first: a bad draw too is not the one named
            Assert.Contains("brain 40", Assert.Throws<ResourceRefused>(() => _f.Read(40, 40, 40)).Message);
        }

        [Fact]
        public void TheLastBrainDrawAndTankThereAreAreReadAsBefore()
        {
            JsonElement v = _f.Read(17, 31, 0);
            Assert.Equal("none", v.GetProperty("state").GetString());
            Assert.Equal(17, v.GetProperty("brain").GetInt32());
        }

        [Fact]
        public void AnAddressWithNoTankDealtIsRefusedWithThatReason()
        {
            var plan = new ExamPlanView { Brains = { new ExamBrainView() }, Draws = { new ExamDrawView { Draw = 0, Tanks = null } } };
            Assert.Equal("draw 0 has no tank dealt in the plan, so there is no episode to read", EpisodeSteps.AddressProblem(plan, 0, 0, 0));
            Assert.Null(EpisodeSteps.AddressProblem(new ExamPlanView { Brains = { new ExamBrainView() }, Draws = { new ExamDrawView { Tanks = new List<List<int>> { new List<int> { 0 } } } } }, 0, 0, 0));
        }
    }

    public class ExamProgressTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private string Live(string name, string status, int[] genomeCountsPerBrain, bool recipe = true, int genomes = 2)
        {
            string folder = Path.Combine(_fx.RunsDir, name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "run.json"), "{\"status\":\"" + status + "\"}");
            if (recipe) File.WriteAllText(Path.Combine(folder, "recipe.json"), "{\"name\":\"" + name + "\",\"search\":{\"generations\":5},\"reward\":{\"class\":\"Net\"},\"exam\":{\"controls\":[{\"kind\":\"own\"},{\"kind\":\"frozen\"},{\"kind\":\"brainless\"}]}}");
            File.WriteAllLines(Path.Combine(folder, "tank-generations.jsonl"), new[] { 60, 120, 180 }.Select((s, i) => "{\"gen\":" + i + ",\"seconds\":" + s + ",\"top\":{\"score\":1,\"body\":0}}"));
            string[] labels = { "own", "frozen", "brainless" };
            var rows = new List<string>();
            for (int b = 0; b < genomeCountsPerBrain.Length; b++)
                for (int g = 0; g < genomeCountsPerBrain[b]; g++) rows.Add(Js.Serialize(new { brain = labels[b], genome = g, body = g, net = 1.0, per = new[] { 1.0, 2.0 } }));
            File.WriteAllLines(Path.Combine(folder, "tank-exam.jsonl"), rows);
            File.WriteAllText(Path.Combine(folder, "tank-exam-genomes.tsv"), string.Concat(Enumerable.Range(0, genomes).Select(g => g + "\t{}\n")));
            return folder;
        }

        private JsonElement Progress(string folder)
        {
            var rf = new RunFolder(folder);
            rf.Refresh(0);
            return Js.Parse(Js.Serialize(rf.Snapshot(DateTime.UtcNow)["progress"]));
        }

        [Fact]
        public void BrainsDoneAreThoseWhoseEveryGenomeRowIsWrittenOfTheBrainsThePlanNames()
        {
            // 3 brains of 2 genomes each: own done (2 rows), frozen begun (1 row), brainless not yet
            JsonElement p = Progress(Live("mid", "running", new[] { 2, 1 }));
            Assert.Equal(3, p.GetProperty("examExpected").GetInt32());
            Assert.Equal(2, p.GetProperty("examGenomes").GetInt32());
            Assert.Equal(1, p.GetProperty("examDone").GetInt32());
            Assert.Equal(2, p.GetProperty("examBrains").GetInt32());   // two brains have a row: one is begun
        }

        [Fact]
        public void AFinishedExamSaysAllOfAll()
        {
            JsonElement p = Progress(Live("done", "completed", new[] { 2, 2, 2 }));
            Assert.Equal(3, p.GetProperty("examDone").GetInt32());
            Assert.Equal(3, p.GetProperty("examExpected").GetInt32());
        }

        [Fact]
        public void AnExamThatHasWrittenNoRowYetHasNotBegunAndSaysNoBrainsDone()
        {
            JsonElement p = Progress(Live("fresh", "running", new int[0]));
            Assert.Equal(JsonValueKind.Null, p.GetProperty("examDone").ValueKind);
            Assert.Equal(3, p.GetProperty("examExpected").GetInt32());
        }

        [Fact]
        public void ARunWithNoRecipeTakesTheBrainsItPlansFromItsEntrysRecipeAndSaysNoneWhenThereIsNone()
        {
            // no recipe and no plan: the planned count is not known, and is not made up
            JsonElement p = Progress(Live("norecipe", "completed", new[] { 2, 2 }, recipe: false));
            Assert.Equal(JsonValueKind.Null, p.GetProperty("examExpected").ValueKind);
            Assert.Equal(2, p.GetProperty("examDone").GetInt32());
        }

        [Fact]
        public void TheRunResourceGivesAPilotShapedRunItsEighteenPlannedBrainsFromTheEntrysRecipe()
        {
            string folder = Path.Combine(_fx.RunsDir, "pilot");
            Directory.CreateDirectory(folder);
            var lines = new List<string>();
            string[] labels = Recipe.Load(_fx.Full("rounds/nursery/brain2-w11.json")).Controls.Select(c => c.Label).ToArray();
            foreach (string label in labels)
                for (int g = 0; g < 2; g++) lines.Add(Js.Serialize(new { brain = label, genome = g, body = g, net = 10.0, per = new[] { 10.0, 11.0 } }));
            File.WriteAllLines(Path.Combine(folder, "tank-exam.jsonl"), lines);
            RegistryEntry.Append(_fx.Registry, new RegistryEntry { Recipe = "brain2", RecipeHash = Recipe.Load(_fx.Full("rounds/nursery/brain2-w11.json")).Hash(), RecipePath = "rounds/nursery/brain2-w11.json", ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = folder, Status = "completed", Rerunnable = false, NotRerunnableBecause = "a test", StartedAtUtc = "2026-10-01T00:00:00Z", EndedAtUtc = "2026-10-01T01:00:00Z" });
            JsonElement p = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).Run("1"))).GetProperty("progress");
            Assert.Equal(18, p.GetProperty("examExpected").GetInt32());
            Assert.Equal(18, p.GetProperty("examDone").GetInt32());
            Assert.Equal(2, p.GetProperty("examGenomes").GetInt32());
        }
    }

    /// <summary>
    /// The exam columns greyed on the right rule: a control is uninformative for a genome when it leaves that genome exactly as it is (a knockout of channels the genome
    /// reads at no port; brainless on a genome with no sense link). The count is the nursery's own knockout, run on the genome.
    /// </summary>
    public class UninformativeColumnTests
    {
        private static readonly string[] Labels = { "own", "frozen", "full", "brainless", "knockout Chemical", "knockout Flow" };
        private const int Frozen = 1, Full = 2, Brainless = 3, KoChemical = 4, KoFlow = 5;

        private static List<ExamHeader> Headers(params string[] labels) => ExamHeaders.Build(labels.Length == 0 ? Labels : labels, null);

        /// <summary>A one-brain genome (the room's synthetic one) whose links read exactly these channels.</summary>
        private static Genome Central(params SensorChannel[] reads)
        {
            Genome g = GenomeResourceTests.Synthetic();
            g.Brain.Links.Clear();
            Phenotype body = Developer.Develop(g);
            foreach (SensorChannel ch in reads)
                g.Brain.Links.Add(new BrainLink { From = GenomeResourceTests.Port(body, 1, ch), To = GenomeResourceTests.Out(body, 1, BrainOutputKind.Power), Weight = 0.5f });
            Assert.Empty(g.Brain.Validate());
            return g;
        }

        /// <summary>A per-part-brain genome (no one brain): one neuron on the first limb, reading Chemical or nothing.</summary>
        private static Genome PerPart(bool readsChemical)
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(GenomeResourceTests.Box(0.5f, JointType.Fixed));
            g.Nodes.Add(GenomeResourceTests.Box(0.3f, JointType.Hinge));
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f) });
            g.Nodes[1].Neurons = new[]
            {
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0.1f, Inputs = readsChemical ? new[] { NeuronInput.FromSensor(SensorChannel.Chemical, 0, 0.5f) } : Array.Empty<NeuronInput>() },
            };
            Assert.Null(g.Brain);
            return g;
        }

        private static ExamReachResult Reach(List<ExamHeader> headers, params Genome[] genomes) =>
            ExamReach.Build(headers, genomes.Select((g, i) => new KeyValuePair<int, string>(i, GenomeJson.Write(g))), null);

        private static List<int> Off(ExamReachResult r, int genome) => r.Genomes.Single(x => x.Genome == genome).Columns;

        [Fact]
        public void ACentralGenomeThatReadsOnlyChemicalIsGreyedOnAnotherChannelsKnockoutAndNowhereElse()
        {
            ExamReachResult r = Reach(Headers(), Central(SensorChannel.Chemical));
            Assert.Equal(new[] { KoFlow }, Off(r, 0));
            // frozen and full cost any genome something whatever its neurons, own is the baseline, and brainless cuts the one sense link
            Assert.DoesNotContain(Frozen, Off(r, 0));
            Assert.DoesNotContain(Full, Off(r, 0));
            Assert.DoesNotContain(0, Off(r, 0));
            Assert.DoesNotContain(Brainless, Off(r, 0));
            Assert.DoesNotContain(KoChemical, Off(r, 0));
        }

        [Fact]
        public void ACentralGenomeWithNoSenseLinkIsGreyedOnBrainlessAndOnEveryKnockoutButNotOnFrozenOrFull()
        {
            ExamReachResult r = Reach(Headers(), Central());
            Assert.Equal(new[] { Brainless, KoChemical, KoFlow }, Off(r, 0));
        }

        [Fact]
        public void APerPartGenomeThatReadsOnlyChemicalIsGreyedOnAnotherChannelsKnockoutAndNowhereElse()
        {
            ExamReachResult r = Reach(Headers(), PerPart(true));
            Assert.Equal(new[] { KoFlow }, Off(r, 0));
        }

        [Fact]
        public void APerPartGenomeWithNoSenseLinkIsGreyedOnBrainlessAndOnEveryKnockout()
        {
            ExamReachResult r = Reach(Headers(), PerPart(false));
            Assert.Equal(new[] { Brainless, KoChemical, KoFlow }, Off(r, 0));
        }

        [Fact]
        public void EachGenomeListsItsOwnColumnsAndTheControlsSayWhatTheyCut()
        {
            ExamReachResult r = Reach(Headers(), Central(SensorChannel.Chemical), PerPart(false));
            Assert.Equal(new[] { KoFlow }, Off(r, 0));
            Assert.Equal(new[] { Brainless, KoChemical, KoFlow }, Off(r, 1));
            Assert.Equal(new[] { "Chemical" }, r.Controls.Single(c => c.Col == KoChemical).Channels);
            Assert.Equal("brainless", r.Controls.Single(c => c.Col == Brainless).Kind);
            Assert.DoesNotContain(r.Controls, c => c.Col == Frozen || c.Col == Full || c.Col == 0);
        }

        [Fact]
        public void ANameThatIsNotAChannelAGenomeThatDoesNotLoadAndAGenomeThatLosesNothingGiveNoEntry()
        {
            // "Nonsense" is no SensorChannel, "7" is a number and not a name: that knockout is never greyed
            List<ExamHeader> h = Headers("own", "knockout Nonsense", "knockout 7", "knockout Chemical");
            Assert.Null(ExamReach.Build(h, new[] { new KeyValuePair<int, string>(0, GenomeJson.Write(Central(SensorChannel.Chemical))) }, null));
            // a genome that does not load has no entry; one that does has its own
            ExamReachResult r = ExamReach.Build(Headers(), new[] { new KeyValuePair<int, string>(0, "not json"), new KeyValuePair<int, string>(1, GenomeJson.Write(PerPart(false))) }, null);
            Assert.Single(r.Genomes);
            Assert.Equal(1, r.Genomes[0].Genome);
            // a header list with no knockout and no brainless has nothing to grey
            Assert.Null(ExamReach.Build(Headers("own", "frozen", "full"), new[] { new KeyValuePair<int, string>(0, GenomeJson.Write(PerPart(false))) }, null));
        }

        [Fact]
        public void TheNurserysCountIsTheKnockoutsOwnAndNullMeansEveryChannel()
        {
            Genome central = Central(SensorChannel.Chemical, SensorChannel.JointAngle);
            Assert.Equal(2, KnockoutReach.Count(central, null, DevelopmentLimits.Default, PartShapeRegistry.Standard));
            Assert.Equal(1, KnockoutReach.Count(central, new[] { SensorChannel.Chemical }, DevelopmentLimits.Default, PartShapeRegistry.Standard));
            Assert.Equal(0, KnockoutReach.Count(central, new[] { SensorChannel.Flow }, DevelopmentLimits.Default, PartShapeRegistry.Standard));
            Genome part = PerPart(true);
            Assert.Equal(1, KnockoutReach.Count(part, null, DevelopmentLimits.Default, PartShapeRegistry.Standard));
            Assert.Equal(1, KnockoutReach.Count(part, new[] { SensorChannel.Chemical }, DevelopmentLimits.Default, PartShapeRegistry.Standard));
            Assert.Equal(0, KnockoutReach.Count(part, new[] { SensorChannel.Flow }, DevelopmentLimits.Default, PartShapeRegistry.Standard));
        }

        [Fact]
        public void TheRunResourceListsPerGenomeTheColumnsThatLeaveItUnchangedAndSaysNothingWhenItCannotRead()
        {
            using var f = new Fx();
            string folder = Path.Combine(f.RunsDir, "reach");
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "tank-exam.jsonl"), Labels.SelectMany(label => new[] { 0, 1 }.Select(gi =>
                Js.Serialize(new { brain = label, genome = gi, body = gi, net = 1.0, per = new[] { 1.0, 2.0 } }))));
            File.WriteAllText(Path.Combine(folder, "tank-exam-genomes.tsv"), "0\t" + GenomeJson.Write(Central(SensorChannel.Chemical)) + "\n1\t" + GenomeJson.Write(PerPart(false)) + "\n");
            RegistryEntry.Append(f.Registry, new RegistryEntry { Recipe = "reach", RecipeHash = "aa", RecipePath = "rounds/nursery/brain2-w11.json", ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = folder, Status = "completed", Rerunnable = false, NotRerunnableBecause = "a test", StartedAtUtc = "2026-10-01T00:00:00Z", EndedAtUtc = "2026-10-01T01:00:00Z" });
            JsonElement run = Js.Parse(Js.Serialize(new ResourceResolver(f.Room).Run("1")));
            JsonElement u = run.GetProperty("uninformative");
            JsonElement g0 = u.GetProperty("genomes")[0], g1 = u.GetProperty("genomes")[1];
            Assert.Equal(0, g0.GetProperty("genome").GetInt32());
            Assert.Equal(new[] { KoFlow }, g0.GetProperty("columns").EnumerateArray().Select(x => x.GetInt32()).ToArray());
            Assert.Equal(1, g1.GetProperty("genome").GetInt32());
            Assert.Equal(new[] { Brainless, KoChemical, KoFlow }, g1.GetProperty("columns").EnumerateArray().Select(x => x.GetInt32()).ToArray());
            JsonElement ko = u.GetProperty("controls").EnumerateArray().Single(c => c.GetProperty("col").GetInt32() == KoChemical);
            Assert.Equal("knockout", ko.GetProperty("kind").GetString());
            Assert.Equal("Chemical", ko.GetProperty("channels")[0].GetString());
            Assert.False(run.TryGetProperty("examTraits", out _));

            // a run with no genomes file says nothing: a null, so that the screen greys nothing
            string bare = Path.Combine(f.RunsDir, "bare");
            Directory.CreateDirectory(bare);
            File.WriteAllLines(Path.Combine(bare, "tank-exam.jsonl"), new[] { Js.Serialize(new { brain = "own", genome = 0, net = 1.0 }), Js.Serialize(new { brain = "brainless", genome = 0, net = 1.0 }) });
            RegistryEntry.Append(f.Registry, new RegistryEntry { Recipe = "bare", RecipeHash = "aa", RecipePath = "rounds/nursery/brain2-w11.json", ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Out = bare, Status = "completed", Rerunnable = false, NotRerunnableBecause = "a test", StartedAtUtc = "2026-10-01T00:00:00Z", EndedAtUtc = "2026-10-01T01:00:00Z" });
            Assert.Equal(JsonValueKind.Null, Js.Parse(Js.Serialize(new ResourceResolver(f.Room).Run("2"))).GetProperty("uninformative").ValueKind);
        }

        [Fact]
        public void TheExamScreenGreysACellWhereItsGenomeListsTheColumnAndAColumnWhereEveryGenomeDoesAndNamesNoOscillator()
        {
            string js = File.ReadAllText(Path.Combine(Where.WwwRoot, "js", "run.js"));
            Assert.Contains("const offCell = (c, genome) =>", js);
            Assert.Contains("const offCol = c => e.bodies.length > 0 && e.bodies.every(r => offCell(c, r.genome));", js);
            Assert.Contains("this genome reads no port of ${listOf(x.channels)}, so the knockout leaves it unchanged and its difference is zero by construction", js);
            Assert.Contains("this genome has no sense link, so brainless leaves it unchanged", js);
            Assert.DoesNotContain("NEEDS_OSC", js);
            Assert.DoesNotContain("scillator", js);
            Assert.DoesNotContain("examTraits", js);
        }
    }

    /// <summary>Four small faults from the last walk: the band's headline in a title, the control with nothing in common, the load capped at 100 for a person, a thread in the singular.</summary>
    public class SmallFaultTests
    {
        private static string Js_(string name) => File.ReadAllText(Path.Combine(Where.WwwRoot, "js", name));

        [Fact]
        public void TheDiffersBoxTitleHoldsTheWholeHeadlineThenTheWordsAboutOpeningTheList()
        {
            string run = Js_("run.js");
            Assert.Contains("`${kicker}: ${text}. What this run's recipe differs by from its control's: opens the list`", run);
        }

        [Fact]
        public void TheNoBrainInCommonLineSaysWhatTheControlsExamSwamAndDoesNotContradictItself()
        {
            string run = Js_("run.js");
            Assert.Contains("The control's exam swam none of the brains this run sets against its own", run);
            Assert.Contains("swam ${esc(((ctl.data.exam && ctl.data.exam.brains) || []).join(', ') || 'nothing')}", run);
            Assert.DoesNotContain("swam none of this run's brains", run);
        }

        [Fact]
        public void TheStripShowsTheSlotAndTheHeatAndNoLongerAnyLoad()
        {
            string common = Js_("common.js");
            Assert.DoesNotContain("capLogical", common);
            Assert.DoesNotContain("percent", common);
            Assert.Contains("r.machine", common);
            Assert.Contains("r.heat", common);
            Assert.Contains("m.freeCount", common);
            Assert.Contains("so at most ${n} ${n === 1 ? 'thread fits' : 'threads fit'}", common);
        }

        [Fact]
        public void OneThreadIsAskedInTheSingular()
        {
            HeatReading ok = HeatReading.Of(HeatReading.Ok, "OK");
            Slot none = Cap.SlotOf(QueueReading.Parse(FixedQueue.With("x · cores: 16-31"), FixedQueue.File), 0xFFFF0000UL);
            Assert.StartsWith("1 thread asked is more than the 0 free cores: ", Cap.Refusal(1, none, ok, 0));
            Assert.StartsWith("2 threads asked is more than the 0 free cores: ", Cap.Refusal(2, none, ok, 0));
            Slot one = Cap.SlotOf(QueueReading.Parse(FixedQueue.With("x · cores: 16-30"), FixedQueue.File), 0xFFFF0000UL);
            Assert.StartsWith("2 threads asked is more than the 1 free core (logical 31): ", Cap.Refusal(2, one, ok, 0));
            Assert.StartsWith("1 thread asked, plus 1 thread granted", Cap.Refusal(1, one, ok, 1));
            Assert.EndsWith("refusing 1 thread", Cap.Refusal(1, one, HeatReading.Of(HeatReading.Hot, "HOT"), 0));
            Assert.StartsWith("1 thread asked, but the affinity mask", CoreMask.ThreadsProblem(1, 0UL));
            Assert.StartsWith("3 threads asked, but the affinity mask", CoreMask.ThreadsProblem(3, 0b11UL));
        }
    }
}
