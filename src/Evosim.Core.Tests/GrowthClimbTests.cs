using System;
using System.Collections.Generic;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D241: growth as a climb. A rate law whose time constant makes one dial, the time to adulthood T, a
    /// reserve threshold the genome carries, and an adult breeding gate; at T = 0 none of it is entered.
    /// </summary>
    /// <remarks>
    /// <b>Written without being run</b> (the build's brief forbade it); the caller runs them in a test window. The
    /// bodies are single unjointed structural boxes with no income, so the only things that move a reserve are the
    /// standing cost and growth, and every figure a test compares against is read off the world it just built.
    /// </remarks>
    public class GrowthClimbTests
    {
        private readonly ITestOutputHelper _output;

        public GrowthClimbTests(ITestOutputHelper output) => _output = output;

        /// <summary>ln((1 - 0.65^(1/4)) / (1 - 0.95^(1/4))): the time constants between 0.65 and 0.95 of the adult.</summary>
        private static readonly double Divisor =
            Math.Log((1d - Math.Pow(0.65d, 0.25d)) / (1d - Math.Pow(0.95d, 0.25d)));

        // ---------------------------------------------------------------- fixtures

        /// <summary>One unjointed structural box that is born at <paramref name="birthFraction"/> of its adult.</summary>
        private static Genome Juvenile(float birthFraction, float halfExtent = 0.3f)
        {
            var genome = new Genome
            {
                RootIndex = 0,

                // NewbornFrom: BirthInvestment / BroodSize x (1 - NewbornReserveFraction), 0.2 by default.
                Reproduction = new ReproductionTraits
                {
                    BroodSize = 1,
                    BirthInvestment = birthFraction / (1f - new RunConfig().NewbornReserveFraction),
                },
            };

            genome.Nodes.Add(new MorphNode
            {
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(halfExtent, halfExtent, halfExtent),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });

            return genome;
        }

        /// <summary>An empty stage with no mutation and a reserve so large that the allowance is what binds.</summary>
        private static RunConfig Stage(float t, float founderEnergy = 1e7f)
        {
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(200f, 12f),
                GrowthAdultSeconds = t,
                FounderEnergyJoules = founderEnergy,
            };

            foreach (PropertyInfo property in typeof(MutationRates).GetProperties())
            {
                if (property.PropertyType == typeof(float) && property.Name.EndsWith("Chance"))
                {
                    property.SetValue(config.Mutation, 0f);
                }
            }

            return config;
        }

        private static Organism Found(RunConfig config, Genome genome, out World world)
        {
            world = new World(config, seed: 3);
            world.Inoculate(genome, count: 1, heightY: -1f);
            return world.Living[0];
        }

        /// <summary>What a newborn is, read off a world with a reserve to spare: its standing cost, tissue, adult and fraction.</summary>
        private static (double watts, double tissue, double adult, float fraction) Probe(RunConfig config, Genome genome)
        {
            Organism o = Found(config, genome, out _);
            return (o.StandingWatts, o.TissueJoules, o.AdultTissueJoules, o.BodyFraction);
        }

        // ---------------------------------------------------------------- MakeAdult

        [Fact]
        public void MakeAdultPutsAJuvenileAtItsAdultAndBothBooksStillClose()
        {
            // D241, 6d's question: the tissue MakeAdult gives is booked as income, as a founder's start is, so the
            // energy audit and the matter identity close after it as they did before it.
            RunConfig config = Stage(60f);
            Organism o = Found(config, Juvenile(0.65f), out World world);
            Assert.True(o.BodyFraction < 1f, $"the founder is a juvenile at T = 60 (fraction {o.BodyFraction})");

            double auditBefore = world.AuditResidual, matterBefore = world.MatterResidual;
            double owed = o.AdultTissueJoules - o.TissueJoules;
            double reserve = o.Energy;

            world.MakeAdult(o);

            Assert.Equal(1f, o.BodyFraction);
            Assert.Same(o.AdultPhenotype, o.Phenotype);
            Assert.Equal(o.AdultTissueJoules, o.TissueJoules);
            Assert.Equal(reserve, o.Energy);
            Assert.Equal(owed, world.AdultGiftJoules, 6);

            double scale = Math.Max(1d, world.StandingJoules);
            Assert.True(Math.Abs(world.AuditResidual - auditBefore) <= 1e-9 * scale,
                $"the audit moved from {auditBefore:R} to {world.AuditResidual:R}");
            Assert.True(Math.Abs(world.MatterResidual - matterBefore) <= 1e-9 * Math.Max(1d, world.MatterInitialTotal),
                $"the matter identity moved from {matterBefore:R} to {world.MatterResidual:R}");

            // And one step on, as any world's books are.
            world.Step(0.5f);
            Assert.True(Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R} a step after");
        }

        [Fact]
        public void MakeAdultRefusesABodyNotAmongTheLiving()
        {
            RunConfig config = Stage(60f);
            Organism o = Found(config, Juvenile(0.65f), out World world);
            Organism stranger = Found(config, Juvenile(0.65f), out _);
            Assert.Throws<InvalidOperationException>(() => world.MakeAdult(stranger));
            Assert.True(o.BodyFraction < 1f);
        }

        // ---------------------------------------------------------------- T = 0

        [Fact]
        public void AtZeroTheWorldGrowsAsItAlwaysDidAndCarriesNoGene()
        {
            RunConfig config = Stage(0f);
            Organism o = Found(config, Juvenile(0.65f), out World world);

            Assert.Null(o.Genome.GrowthReserveSeconds);
            Assert.InRange(o.BodyFraction, 0.64f, 0.66f);

            world.Step(0.5f);

            // Today's rule: everything above the floor goes into the body in one step.
            Assert.Equal(1f, o.BodyFraction);
            Assert.Equal(o.AdultTissueJoules, o.TissueJoules);
        }

        [Fact]
        public void AtZeroTwoWorldsStepToTheSameStateAndNoRowCarriesTheGene()
        {
            var rows = new List<string>();
            var energies = new List<double>();

            for (int run = 0; run < 2; run++)
            {
                RunConfig config = Stage(0f, founderEnergy: 300f);
                Organism o = Found(config, Juvenile(0.65f), out World world);
                for (int i = 0; i < 40; i++) world.Step(0.5f);

                energies.Add(o.Energy);
                foreach (LineageEvent e in world.DrainLineageEvents()) rows.Add(e.ToJson());
            }

            Assert.Equal(energies[0], energies[1]);
            Assert.True(rows.FindAll(r => r.Contains("\"e\":\"b\"")).Count >= 2, "each run should have queued a birth row");
            Assert.All(rows, r => Assert.DoesNotContain("\"grs\"", r));
        }

        // ---------------------------------------------------------------- the law

        [Fact]
        public void TheDivisorIsAboutTwoPointZeroEightOne()
        {
            Assert.InRange(Divisor, 2.080, 2.082);
        }

        [Fact]
        public void OneStepsAllowanceIsTheClosedForm()
        {
            const float t = 120f;
            Organism o = Found(Stage(t), Juvenile(0.65f), out World world);

            double m0 = o.TissueJoules, big = o.AdultTissueJoules;
            world.Step(0.5f);

            double tau = t / Divisor;
            double r0 = Math.Pow(m0 / big, 0.25d);
            double r1 = 1d - (1d - r0) * Math.Exp(-0.5d / tau);
            double expected = big * Math.Pow(r1, 4d);

            _output.WriteLine($"m0 {m0:0.####} J of {big:0.####} J: expected {expected:0.#####} J, got {o.TissueJoules:0.#####} J");

            Assert.True(o.BodyFraction < 1f);
            Fixtures.AssertClose(expected, o.TissueJoules, big * 1e-4);
        }

        [Fact]
        public void TheStepIsExactSoTwoHalvesAreOneWhole()
        {
            const float t = 120f;
            Organism two = Found(Stage(t), Juvenile(0.65f), out World twice);
            Organism one = Found(Stage(t), Juvenile(0.65f), out World once);

            twice.Step(0.5f);
            twice.Step(0.5f);
            once.Step(1f);

            Fixtures.AssertClose(one.TissueJoules, two.TissueJoules, one.AdultTissueJoules * 1e-4);
        }

        [Fact]
        public void AnUnlimitedNewbornAtPointSixFiveReachesTheSnapAtTWithinOneMetabolicStep()
        {
            const float t = 60.2f;
            const float dt = 0.5f;
            Organism o = Found(Stage(t), Juvenile(0.65f), out World world);

            int guard = 0;
            while (o.BodyFraction < 1f)
            {
                world.Step(dt);
                Assert.True(++guard < 2000, "the body never reached its adult");
            }

            double elapsed = world.ElapsedSeconds;
            _output.WriteLine($"adult at {elapsed:0.###} s for T = {t}");

            Assert.InRange(elapsed, t - 0.01, t + dt + 0.01);
            Assert.Equal(o.AdultTissueJoules, o.TissueJoules);
        }

        [Fact]
        public void AReserveUnderTheGeneTimesTheStandingCostDoesNotGrowAndOneAboveItDoes()
        {
            // The rate law is made fast (T = 0.1) so that only the reserve can hold the body back.
            RunConfig probeConfig = Stage(0.1f);
            probeConfig.GrowthReserveStartSeconds = 150f;
            var (watts, tissue, _, f) = Probe(probeConfig, Juvenile(0.65f));

            double keepGene = 150d * watts;
            double keepFloor = probeConfig.GrowthReserveFloor * tissue;
            Assert.True(keepGene > 1.5d * keepFloor, $"the gene must be the larger keep: {keepGene} against {keepFloor}");

            // Below the line: half of keep, less what the step's own standing cost takes.
            RunConfig poor = Stage(0.1f, founderEnergy: (float)(0.5d * keepGene / f));
            poor.GrowthReserveStartSeconds = 150f;
            Organism stays = Found(poor, Juvenile(0.65f), out World poorWorld);
            float before = stays.BodyFraction;
            double tissueBefore = stays.TissueJoules;
            poorWorld.Step(0.5f);
            Assert.Equal(before, stays.BodyFraction);
            Assert.Equal(tissueBefore, stays.TissueJoules);

            // Above it: twice keep.
            RunConfig rich = Stage(0.1f, founderEnergy: (float)(2d * keepGene / f));
            rich.GrowthReserveStartSeconds = 150f;
            Organism grows = Found(rich, Juvenile(0.65f), out World richWorld);
            richWorld.Step(0.5f);
            Assert.True(grows.BodyFraction > before, "a reserve above the gene's keep should have grown the body");
        }

        [Fact]
        public void TheGeneBindsExactlyWhenTheReserveIsScarce()
        {
            RunConfig probeConfig = Stage(0.1f);
            probeConfig.GrowthReserveStartSeconds = 150f;
            var (watts, tissue, _, f) = Probe(probeConfig, Juvenile(0.65f));
            double keepGene = 150d * watts;

            // 1.2 keep to begin with, and the step's standing cost comes off before growth, so the spare is a
            // small share of keep and the reserve, not the rate law, is what limits the investment.
            RunConfig config = Stage(0.1f, founderEnergy: (float)(1.2d * keepGene / f));
            config.GrowthReserveStartSeconds = 150f;
            Organism o = Found(config, Juvenile(0.65f), out World world);
            world.Step(0.5f);

            Assert.True(o.BodyFraction > f);
            Fixtures.AssertClose(keepGene, o.Energy, keepGene * 1e-3);
        }

        [Fact]
        public void TheTissueFloorBindsWhenItIsTheLargerKeep()
        {
            RunConfig probeConfig = Stage(0.1f);
            probeConfig.GrowthReserveStartSeconds = 1f;
            var (watts, tissue, _, f) = Probe(probeConfig, Juvenile(0.65f));

            double keepFloor = probeConfig.GrowthReserveFloor * tissue;
            Assert.True(keepFloor > 2d * 1d * watts, $"the floor must be the larger keep: {keepFloor} against {watts}");

            RunConfig config = Stage(0.1f, founderEnergy: (float)(1.5d * keepFloor / f));
            config.GrowthReserveStartSeconds = 1f;
            Organism o = Found(config, Juvenile(0.65f), out World world);
            world.Step(0.5f);

            Assert.True(o.BodyFraction > f);
            Fixtures.AssertClose(keepFloor, o.Energy, keepFloor * 1e-3);
        }

        // ---------------------------------------------------------------- breeding

        [Fact]
        public void WithTheClimbAJuvenileIsRefusedConceptionAndAnAdultIsNot()
        {
            // A slow climb, and a reserve far above any gate, so only the adult gate can refuse.
            RunConfig slow = Stage(1000f);
            Organism juvenile = Found(slow, Juvenile(0.65f), out World slowWorld);
            slowWorld.Step(0.5f);

            Assert.True(juvenile.BodyFraction < 1f);
            Assert.Equal(0L, slowWorld.Births);

            // An adult at birth (a share of 3 caps at the whole body) breeds on the same reserve.
            Genome adult = Juvenile(0.65f);
            adult.Reproduction.BirthInvestment = 3f;
            Organism parent = Found(Stage(1000f), adult, out World adultWorld);
            Assert.Equal(1f, parent.BodyFraction);
            adultWorld.Step(0.5f);

            Assert.True(adultWorld.Births > 0, "an adult with a full reserve should have bred");
        }

        [Fact]
        public void WithoutTheClimbTheSameJuvenileGrowsInTheStepAndBreedsInIt()
        {
            Organism o = Found(Stage(0f), Juvenile(0.65f), out World world);
            world.Step(0.5f);

            Assert.Equal(1f, o.BodyFraction);
            Assert.True(world.Births > 0);
        }

        // ---------------------------------------------------------------- the gene

        [Fact]
        public void TheGenesLogStepStaysInRangeAndMovesAndDrawsNothingAtChanceZero()
        {
            MutationRates silent = Silent();
            Genome parent = GenomeFactory.Random(new Rng(7));

            // At chance 0 a gene-carrying parent and a gene-less one draw the same stream, so their children
            // differ in nothing but the gene, which is carried through unchanged.
            Genome withGene = parent.Clone();
            withGene.GrowthReserveSeconds = 30f;

            Genome plainChild = Mutator.Mutate(parent, new Rng(11), silent);
            Genome geneChild = Mutator.Mutate(withGene, new Rng(11), silent);
            Assert.Equal(30f, geneChild.GrowthReserveSeconds);
            geneChild.GrowthReserveSeconds = null;
            Assert.Equal(GenomeJson.Write(plainChild), GenomeJson.Write(geneChild));

            // At a chance above 0 a gene-less parent still draws nothing for it.
            MutationRates on = Silent();
            on.GrowthReserveChance = 1f;
            Genome plainOn = Mutator.Mutate(parent, new Rng(11), on);
            Assert.Null(plainOn.GrowthReserveSeconds);
            Assert.Equal(GenomeJson.Write(plainChild), GenomeJson.Write(plainOn));

            // At chance 1 the gene steps, inside the range, near both ends.
            foreach (float start in new[] { 1.05f, 30f, 299f })
            {
                Genome walker = parent.Clone();
                walker.GrowthReserveSeconds = start;
                bool moved = false;

                for (ulong seed = 1; seed <= 200; seed++)
                {
                    Genome child = Mutator.Mutate(
                        walker, new Rng(seed), on, growthReserveMin: 1f, growthReserveMax: 300f);

                    float gene = child.GrowthReserveSeconds.Value;
                    Assert.InRange(gene, 1f, 300f);
                    if (gene != walker.GrowthReserveSeconds.Value) moved = true;

                    walker.GrowthReserveSeconds = gene;
                }

                Assert.True(moved, $"the gene never moved from {start}");
            }
        }

        [Fact]
        public void SpeciesDistanceAddsNothingWithoutTheGeneAndTheLogGapWithIt()
        {
            Genome a = Fixtures.SingleBox();
            Genome b = Fixtures.SingleBox();

            Assert.Equal(0f, SpeciesDistance.Between(a, b, 0f, 0f, 1f, 0f));

            // One carries it and the other does not: still nothing.
            a.GrowthReserveSeconds = 30f;
            Assert.Equal(0f, SpeciesDistance.Between(a, b, 0f, 0f, 1f, 0f));

            // Both carry it: |ln a - ln b| / ln(300 / 1).
            b.GrowthReserveSeconds = 3f;
            float expected = (float)(Math.Abs(Math.Log(30d) - Math.Log(3d)) / Math.Log(300d));
            Fixtures.AssertClose(expected, SpeciesDistance.Between(a, b, 0f, 0f, 1f, 0f), 1e-5f);
        }

        [Fact]
        public void ValidateRefusesAGeneThatIsNotAPositiveFiniteNumber()
        {
            Genome g = Fixtures.SingleBox();
            Assert.Empty(g.Validate());

            foreach (float bad in new[] { 0f, -3f, float.NaN, float.PositiveInfinity })
            {
                g.GrowthReserveSeconds = bad;
                Assert.Contains(g.Validate(), issue => issue.Contains("Growth reserve"));
            }

            g.GrowthReserveSeconds = 30f;
            Assert.Empty(g.Validate());
        }

        [Fact]
        public void CloneCopiesTheGene()
        {
            Genome g = Fixtures.SingleBox();
            g.GrowthReserveSeconds = 42f;
            Assert.Equal(42f, g.Clone().GrowthReserveSeconds);

            g.GrowthReserveSeconds = null;
            Assert.Null(g.Clone().GrowthReserveSeconds);
        }

        // ---------------------------------------------------------------- the genome file

        private static Genome Family()
        {
            var genome = new Genome { RootIndex = 0 };
            MorphNode root = Fixtures.Box(0.3f);
            root.ShapeId = ShapeIds.Superellipsoid;
            root.Form = new PartForm(0.6f, 1f, 1.5f);
            genome.Nodes.Add(root);
            return genome;
        }

        private static Genome Variant(bool family, bool gene, bool brain)
        {
            Genome genome = family ? Family() : Fixtures.SingleBox();
            if (gene) genome.GrowthReserveSeconds = 30f;
            if (brain) genome.Brain = new CentralBrain();
            return genome;
        }

        [Theory]
        [InlineData(false, false, false, 11)]
        [InlineData(false, false, true, 12)]
        [InlineData(true, false, false, 13)]
        [InlineData(true, false, true, 14)]
        [InlineData(false, true, false, 13)]
        [InlineData(false, true, true, 14)]
        [InlineData(true, true, false, 13)]
        [InlineData(true, true, true, 14)]
        public void TheFormatMatrixPicksTheLowestFormatAndRoundTrips(bool family, bool gene, bool brain, int format)
        {
            Genome genome = Variant(family, gene, brain);
            string text = GenomeJson.Write(genome);

            Assert.Equal(format, GenomeJson.FormatFor(genome));
            Assert.Contains("\"format\":" + format + ",", text);

            if (format >= 13)
            {
                Assert.Contains(gene ? "\"lifespan\":1,\"growthReserve\":30," : "\"lifespan\":1,\"growthReserve\":null,", text);
            }
            else
            {
                Assert.DoesNotContain("growthReserve", text);
            }

            Genome back = GenomeJson.Read(text);
            Assert.Equal(gene ? 30f : (float?)null, back.GrowthReserveSeconds);
            Assert.Equal(text, GenomeJson.Write(back));
        }

        private static FormatException Refused(string text) =>
            Assert.Throws<FormatException>(() => GenomeJson.Read(text));

        [Fact]
        public void TheReaderRefusesEachWayTheGeneAndTheFormatCanDisagree()
        {
            string plain = GenomeJson.Write(Fixtures.SingleBox());
            string withGene = GenomeJson.Write(Variant(family: false, gene: true, brain: false));

            // A key in format 11 (and 12).
            FormatException e = Refused(plain.Replace("\"lifespan\":1,", "\"lifespan\":1,\"growthReserve\":30,"));
            Assert.Contains("growthReserve", e.Message);
            Assert.Contains("formats 13 and 14", e.Message);

            Genome central = Fixtures.SingleBox();
            central.Brain = new CentralBrain();
            e = Refused(GenomeJson.Write(central).Replace("\"lifespan\":1,", "\"lifespan\":1,\"growthReserve\":30,"));
            Assert.Contains("growthReserve", e.Message);

            // The key missing in format 13 (and 14).
            e = Refused(withGene.Replace("\"growthReserve\":30,", ""));
            Assert.Contains("missing", e.Message);

            Genome centralGene = Variant(family: false, gene: true, brain: true);
            e = Refused(GenomeJson.Write(centralGene).Replace("\"growthReserve\":30,", ""));
            Assert.Contains("missing", e.Message);

            // A value that is not a positive number.
            foreach (string bad in new[] { "0", "-5" })
            {
                e = Refused(withGene.Replace("\"growthReserve\":30,", "\"growthReserve\":" + bad + ","));
                Assert.Contains("finite and positive", e.Message);
            }

            // A format 13 or 14 that holds neither extension.
            e = Refused(
                plain.Replace("\"format\":11", "\"format\":13")
                     .Replace("\"lifespan\":1,", "\"lifespan\":1,\"growthReserve\":null,"));
            Assert.Contains("it has neither", e.Message);

            e = Refused(withGene.Replace("\"growthReserve\":30,", "\"growthReserve\":null,"));
            Assert.Contains("it has neither", e.Message);
        }

        [Fact]
        public void TheUnknownFormatMessageNamesBothExtensions()
        {
            FormatException e = Refused(GenomeJson.Write(Variant(true, true, false)).Replace("\"format\":13", "\"format\":99"));
            Assert.Contains("growth-reserve", e.Message);
            Assert.Contains("13", e.Message);
        }

        // ---------------------------------------------------------------- admission

        [Fact]
        public void AdmissionGivesTheStartOnACloneAndLeavesTheCallersGenomeAlone()
        {
            RunConfig config = Stage(60f);
            config.GrowthReserveStartSeconds = 45f;
            Genome shared = Juvenile(0.65f);

            Organism o = Found(config, shared, out _);

            Assert.Null(shared.GrowthReserveSeconds);
            Assert.Equal(45f, o.Genome.GrowthReserveSeconds);
            Assert.NotSame(shared, o.Genome);
        }

        [Fact]
        public void AdmissionKeepsAGeneItWasGivenInsideTheRange()
        {
            Genome carried = Juvenile(0.65f);
            carried.GrowthReserveSeconds = 7f;

            Organism o = Found(Stage(60f), carried, out _);

            Assert.Same(carried, o.Genome);
            Assert.Equal(7f, o.Genome.GrowthReserveSeconds);
        }

        [Fact]
        public void AdmissionRefusesAGeneInAWorldWithoutTheClimbAndOneOutsideTheRange()
        {
            Genome carried = Juvenile(0.65f);
            carried.GrowthReserveSeconds = 30f;

            var noClimb = new World(Stage(0f), seed: 3);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(
                () => noClimb.Inoculate(carried, count: 1, heightY: -1f));
            Assert.Contains("GrowthReserveSeconds", e.Message);
            Assert.Contains("no climb", e.Message);

            Genome outside = Juvenile(0.65f);
            outside.GrowthReserveSeconds = 500f;

            var climb = new World(Stage(60f), seed: 3);
            e = Assert.Throws<InvalidOperationException>(
                () => climb.Inoculate(outside, count: 1, heightY: -1f));
            Assert.Contains("GrowthReserveSeconds", e.Message);
            Assert.Contains("500", e.Message);
            Assert.Contains("[1, 300]", e.Message);
        }

        [Fact]
        public void TheWorldRefusesAClimbConfigItCannotRun()
        {
            Assert.Throws<ArgumentException>(() => new World(Stage(-1f), seed: 1));
            Assert.Throws<ArgumentException>(() => new World(Stage(float.NaN), seed: 1));

            RunConfig noMin = Stage(60f);
            noMin.GrowthReserveMinSeconds = 0f;
            Assert.Throws<ArgumentException>(() => new World(noMin, seed: 1));

            RunConfig startAboveMax = Stage(60f);
            startAboveMax.GrowthReserveStartSeconds = 500f;
            Assert.Throws<ArgumentException>(() => new World(startAboveMax, seed: 1));

            RunConfig minAboveStart = Stage(60f);
            minAboveStart.GrowthReserveMinSeconds = 40f;
            Assert.Throws<ArgumentException>(() => new World(minAboveStart, seed: 1));

            // At T = 0 the range is not read, so a launcher need not make it consistent.
            RunConfig off = Stage(0f);
            off.GrowthReserveMinSeconds = 0f;
            _ = new World(off, seed: 1);
        }

        // ---------------------------------------------------------------- the birth row

        [Fact]
        public void TheBirthRowCarriesTheGeneOnlyWhenTheClimbIsOn()
        {
            RunConfig on = Stage(60f);
            on.GrowthReserveStartSeconds = 45f;
            Found(on, Juvenile(0.65f), out World climbing);

            string birth = null;
            foreach (LineageEvent e in climbing.DrainLineageEvents())
            {
                if (e.Kind == LineageEventKind.Birth) birth = e.ToJson();
            }

            Assert.NotNull(birth);
            Assert.EndsWith("\"grs\":45}", birth);

            Found(Stage(0f), Juvenile(0.65f), out World plain);
            foreach (LineageEvent e in plain.DrainLineageEvents())
            {
                Assert.DoesNotContain("\"grs\"", e.ToJson());
            }
        }

        // ---------------------------------------------------------------- the metabolic clock

        [Fact]
        public void ACellTypesClockIsItsTissueOverItsUpkeep()
        {
            CellType consumer = CellTypeRegistry.Standard.Resolve(CellTypeIds.Consumer);
            double expected = (double)consumer.TissueEnergyPerCubicMetre / consumer.UpkeepWattsPerCubicMetre;

            Assert.Equal(expected, CellType.MetabolicClockSeconds(consumer));
            Assert.True(expected > 0d && !double.IsInfinity(expected));
        }

        private static MutationRates Silent()
        {
            var rates = new MutationRates();
            foreach (PropertyInfo property in typeof(MutationRates).GetProperties())
            {
                if (property.PropertyType == typeof(float) && property.Name.EndsWith("Chance"))
                {
                    property.SetValue(rates, 0f);
                }
            }
            return rates;
        }
    }
}
