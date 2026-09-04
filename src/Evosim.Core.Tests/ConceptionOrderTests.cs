using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// <see cref="RunConfig.ConceptionOrder"/> — the order <see cref="World"/> offers the living
    /// their turn at conception (D072, logbook/0056).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things have to be true for the knob to be worth having, and each is one test here.
    /// The default has to be the world every earlier arm ran in, step for step, or the historical
    /// record stops replaying. The new order has to be reproducible from <c>(seed, config)</c> like
    /// everything else (§7), or an arm run under it is not an experiment. And it has to actually
    /// change who breeds when a layer's matter covers fewer children than there are parents who
    /// want one — which is the fault it exists for, and which CLAUDE.md's "identical numbers"
    /// rule says to prove rather than assume.
    /// </para>
    /// <para>
    /// The contest below is the fault in miniature: two identical solvent leaves at one depth, one
    /// child's worth of matter per step, and nothing else able to breed. Under
    /// <see cref="ConceptionOrder.Age"/> the elder takes it every single step and the younger never
    /// breeds at all; under <see cref="ConceptionOrder.Shuffled"/> they split it.
    /// </para>
    /// <para>
    /// <see cref="ConceptionOrder.Reserve"/> — D073, logbook/0057 — is asked the same question by
    /// the same world, with the two contestants no longer equal: one earns more light than the
    /// other. It has to give the matter to the richer of them and to keep doing it whichever of
    /// them is older, because the whole point of the rule is that the winner is chosen by the
    /// energy books rather than by anything the walk knows about age.
    /// </para>
    /// </remarks>
    public class ConceptionOrderTests
    {
        private readonly ITestOutputHelper _output;

        public ConceptionOrderTests(ITestOutputHelper output) => _output = output;

        // ---------------------------------------------------------------------------------
        // 1. The default is the world that already existed.
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TheDefaultIsAgeAndSettingItExplicitlyChangesNothing()
        {
            // The D052/D055 shape, tested rather than asserted in a comment: a run that never
            // heard of this knob must be bit-identical to one that names its default. A world is
            // compared by everything it can be compared by — who is alive and in what order, how
            // many were born and died, and both conserved totals — because the walk order decides
            // *which* creature breeds, and a difference in it need not show up in a population
            // count at all.
            Assert.Equal(ConceptionOrder.Age, new RunConfig().ConceptionOrder);

            var untouched = new World(Reference(), seed: 9);
            var named = new World(Reference(ConceptionOrder.Age), seed: 9);

            Assert.Equal(untouched.Config.Hash(), named.Config.Hash());

            for (int i = 0; i < 200; i++)
            {
                untouched.Step(1f);
                named.Step(1f);
            }

            _output.WriteLine(Describe("untouched", untouched));
            _output.WriteLine(Describe("named Age ", named));

            AssertSameWorld(untouched, named);
        }

        // ---------------------------------------------------------------------------------
        // 2. The new order replays.
        // ---------------------------------------------------------------------------------

        [Fact]
        public void ShuffledReplaysFromTheSameSeedAndConfig()
        {
            // §7's promise applied to the permutation. The stream behind it is the world's own,
            // seeded from Rng.SeedFor(Seed, a reserved index) at construction, so two worlds built
            // the same way draw the same permutations in the same order — and a shuffled arm is an
            // experiment somebody can repeat rather than a one-off.
            var first = new World(Reference(ConceptionOrder.Shuffled), seed: 9);
            var second = new World(Reference(ConceptionOrder.Shuffled), seed: 9);

            for (int i = 0; i < 200; i++)
            {
                first.Step(1f);
                second.Step(1f);
            }

            _output.WriteLine(Describe("first ", first));
            _output.WriteLine(Describe("second", second));

            AssertSameWorld(first, second);
        }

        [Fact]
        public void ShuffledIsADifferentWorldFromAge()
        {
            // The other half of the same guard, and the one this project has learned to insist on:
            // identical numbers across a configuration change mean the change never reached the
            // thing it configures (logbook/0007, logbook/0008). Two worlds differing only in the
            // knob must not agree on everything.
            var age = new World(Reference(ConceptionOrder.Age), seed: 9);
            var shuffled = new World(Reference(ConceptionOrder.Shuffled), seed: 9);

            for (int i = 0; i < 200; i++)
            {
                age.Step(1f);
                shuffled.Step(1f);
            }

            _output.WriteLine(Describe("age     ", age));
            _output.WriteLine(Describe("shuffled", shuffled));

            Assert.NotEqual(age.Config.Hash(), shuffled.Config.Hash());
            Assert.NotEqual(LivingIds(age), LivingIds(shuffled));
        }

        [Fact]
        public void ReserveReplaysFromTheSameSeedAndConfig()
        {
            // §7 again, and easier to keep here than for Shuffled: Reserve takes no draw from any
            // stream at all — the ranking is a function of the living and their energies — so the
            // only way it could fail to replay is a tie broken by whatever Array.Sort felt like.
            // That is the ordering's index fallback, tested rather than argued.
            var first = new World(Reference(ConceptionOrder.Reserve), seed: 9);
            var second = new World(Reference(ConceptionOrder.Reserve), seed: 9);

            for (int i = 0; i < 200; i++)
            {
                first.Step(1f);
                second.Step(1f);
            }

            _output.WriteLine(Describe("first ", first));
            _output.WriteLine(Describe("second", second));

            AssertSameWorld(first, second);
        }

        [Fact]
        public void ReserveIsADifferentWorldFromAge()
        {
            // logbook/0007 and logbook/0008's rule, for the third member: a knob that reached
            // nothing would produce exactly the reassuring agreement this asserts against.
            var age = new World(Reference(ConceptionOrder.Age), seed: 9);
            var reserve = new World(Reference(ConceptionOrder.Reserve), seed: 9);

            for (int i = 0; i < 200; i++)
            {
                age.Step(1f);
                reserve.Step(1f);
            }

            _output.WriteLine(Describe("age    ", age));
            _output.WriteLine(Describe("reserve", reserve));

            Assert.NotEqual(age.Config.Hash(), reserve.Config.Hash());
            Assert.NotEqual(LivingIds(age), LivingIds(reserve));
        }

        // ---------------------------------------------------------------------------------
        // 3. The contest.
        // ---------------------------------------------------------------------------------

        [Theory]
        [InlineData(ConceptionOrder.Age)]
        [InlineData(ConceptionOrder.Shuffled)]
        public void TwoEqualParentsAndOneChildsWorthOfMatter(ConceptionOrder order)
        {
            // logbook/0056's diagnosis, reduced to the smallest world it can be read in. Two
            // identical leaves at one depth, both permanently solvent, and exactly one child's
            // worth of matter in their layer at the start of every step. Everything else that
            // could breed is held out of the way (see Exile), so the only question the world is
            // being asked is which of the two is walked first.
            var world = new World(Contest(order), seed: 4);

            world.Inoculate(Leaf(), count: 2, heightY: SurfaceY);
            Assert.Equal(2, world.Living.Count);

            Organism elder = world.Living[0];
            Organism younger = world.Living[1];

            for (int step = 0; step < Steps; step++)
            {
                Exile(world, elder, younger);
                RestockOneChildsWorth(world);
                world.Step(StepSeconds);
            }

            _output.WriteLine(
                $"{order}: elder #{elder.Id} {elder.Children} children, " +
                $"younger #{younger.Id} {younger.Children}, " +
                $"{elder.Energy:0.#} J and {younger.Energy:0.#} J left, " +
                $"{world.Births} births in all, {world.ConceptionsBlockedByMatter} refused");

            // Both were solvent throughout — otherwise the test measured scarcity of energy
            // rather than the order of the walk, and a zero would mean nothing.
            Assert.True(
                elder.Energy > 0f && younger.Energy > 0f,
                "a contestant starved; the contest was for energy, not for matter");

            if (order == ConceptionOrder.Age)
            {
                // The queue: the elder is walked first every step, takes the layer's only unit of
                // matter every step, and the younger — equally solvent, equally deserving by every
                // rule the design wrote down — never breeds at all.
                Assert.Equal(Steps, elder.Children);
                Assert.Equal(0, younger.Children);
            }
            else
            {
                Assert.True(elder.Children >= 30, $"elder bred {elder.Children} times");
                Assert.True(younger.Children >= 30, $"younger bred {younger.Children} times");

                // And the matter was not conjured: one child per step, however it was split.
                Assert.Equal(Steps, elder.Children + younger.Children);
            }
        }

        [Theory]
        [InlineData(ConceptionOrder.Age, true)]
        [InlineData(ConceptionOrder.Age, false)]
        [InlineData(ConceptionOrder.Reserve, true)]
        [InlineData(ConceptionOrder.Reserve, false)]
        public void OneRichParentAndOnePoorOne(ConceptionOrder order, bool elderIsRicher)
        {
            // The same one-unit-of-matter world, with the contestants no longer equal: a plate
            // that catches roughly three and a half times the light of the leaf beside it, and
            // earns more each step than a child of its own size costs. Which of the two is older
            // is varied because that is the whole claim — under Reserve the books decide and the
            // queue does not, so the answer must not move when the two are inoculated the other
            // way round.
            var world = new World(Contest(order), seed: 4);

            world.Inoculate(elderIsRicher ? Plate() : Leaf(), count: 1, heightY: SurfaceY);
            world.Inoculate(elderIsRicher ? Leaf() : Plate(), count: 1, heightY: SurfaceY);
            Assert.Equal(2, world.Living.Count);

            Organism elder = world.Living[0];
            Organism younger = world.Living[1];
            Organism richer = elderIsRicher ? elder : younger;
            Organism poorer = elderIsRicher ? younger : elder;

            for (int step = 0; step < Steps; step++)
            {
                Exile(world, elder, younger);
                RestockOneChildsWorth(world);
                world.Step(StepSeconds);
            }

            _output.WriteLine(
                $"{order}, {(elderIsRicher ? "elder" : "younger")} richer: " +
                $"richer #{richer.Id} {richer.Children} children at {richer.Energy:0.#} J, " +
                $"poorer #{poorer.Id} {poorer.Children} at {poorer.Energy:0.#} J, " +
                $"{world.Births} births in all, {world.ConceptionsBlockedByMatter} refused");

            Assert.True(
                elder.Energy > 0f && younger.Energy > 0f,
                "a contestant starved; the contest was for energy, not for matter");

            // The plate has to actually be the richer one for the rest to mean anything — a body
            // that earned no more than its rival would make Reserve's win a coin the test could
            // not read (CLAUDE.md: prove the change reached the thing it configures).
            Assert.True(
                richer.Energy > poorer.Energy,
                $"the plate held {richer.Energy:0.#} J against the leaf's {poorer.Energy:0.#} J");

            if (order == ConceptionOrder.Age)
            {
                // Unchanged by any of it: the queue is walked in birth order and cannot see a
                // reserve, so the elder takes the matter whether it is the rich one or not.
                Assert.Equal(Steps, elder.Children);
                Assert.Equal(0, younger.Children);
            }
            else
            {
                // D073: the layer's one unit goes to the largest surplus above the gate, every
                // step, and the poorer body — solvent throughout, and older in half of these
                // cases — never gets a turn while the richer one still wants it.
                Assert.Equal(Steps, richer.Children);
                Assert.Equal(0, poorer.Children);
            }
        }

        // ---------------------------------------------------------------------------------
        // 4. The knob is a tunable like every other.
        // ---------------------------------------------------------------------------------

        [Theory]
        [InlineData(ConceptionOrder.Shuffled)]
        [InlineData(ConceptionOrder.Reserve)]
        public void TheOrderReachesTheHashAndTheFileByName(ConceptionOrder order)
        {
            // The two reflection guards cover this generically; these three assertions say what
            // the generic ones mean for an enum, which is the first scalar enum on RunConfig and
            // therefore the first knob whose file value is a word rather than a number. Every
            // member but the default is walked, because a member that reached neither the hash nor
            // the file would be a run filed under a world it did not have.
            var age = new RunConfig();
            var named = new RunConfig { ConceptionOrder = order };

            Assert.NotEqual(age.Hash(), named.Hash());

            string text = RunConfigJson.Write(named);
            _output.WriteLine(Line(text, "conceptionOrder"));

            Assert.Contains("\"conceptionOrder\": \"" + order + "\"", text);

            RunConfig back = RunConfigJson.Read(text, out string mismatch);
            Assert.Null(mismatch);
            Assert.Equal(order, back.ConceptionOrder);
            Assert.Equal(named.Hash(), back.Hash());
        }

        [Fact]
        public void AnUnknownOrderIsRefusedOnLoad()
        {
            // §9's rule, which matters more here than for a number: an order the file names and
            // the run did not walk is a run filed under settings it never had, and nothing
            // downstream could notice.
            string text = RunConfigJson.Write(new RunConfig())
                .Replace("\"conceptionOrder\": \"Age\"", "\"conceptionOrder\": \"Oldest\"");

            FormatException e = Assert.Throws<FormatException>(() => RunConfigJson.Read(text));

            _output.WriteLine(e.Message);
            Assert.Contains("Oldest", e.Message);
            Assert.Contains("Shuffled", e.Message);
            Assert.Contains("Reserve", e.Message);
        }

        // ---------------------------------------------------------------------------------
        // The contest's world.
        // ---------------------------------------------------------------------------------

        /// <summary>Where the two contestants live: layer 0, lit, and the only layer with matter.</summary>
        private const float SurfaceY = -0.5f;

        /// <summary>
        /// Where everything else is put. Deep enough to be a different layer of both fields, so an
        /// offspring draws its matter from a layer <see cref="RestockOneChildsWorth"/> keeps empty.
        /// </summary>
        private const float ExileY = -40.5f;

        /// <summary>
        /// Matter one child costs. With <see cref="RunConfig.MatterPerTissueJoule"/> at zero this
        /// is the whole price, so it does not depend on how big the child turned out.
        /// </summary>
        private const float ChildMatter = 1f;

        private const int Steps = 200;

        /// <summary>
        /// Long enough that a leaf earns more than a child costs within one step, so both parents
        /// are above the price on every step and matter is the only thing either can run out of.
        /// </summary>
        private const float StepSeconds = 20f;

        private static RunConfig Contest(ConceptionOrder order) => new RunConfig
        {
            ConceptionOrder = order,

            // No floor, so the only creatures in this world are the ones the test put there —
            // AbsorptiveLogTests' and ProducerCountTests' reason, and doubly so here, since a
            // founder trickle would be a third contestant for the layer's one unit of matter.
            MinimumPopulation = 0,
            MaximumPopulation = 100_000,

            Light = new LightModel(400f, 12f),

            // Clones, not mutants. A mutation that fails to develop is a stillbirth that has
            // already paid for its matter (see World.Conceive), which would spend a step's unit
            // without incrementing anybody's child count and make the arithmetic below a range
            // rather than an equality.
            Mutation = Clones(),

            // One flat price per child and a field that neither sinks, stirs nor decays, so the
            // matter deposited at the start of a step is exactly what the walk finds when it gets
            // there — Step settles and mixes before it reproduces.
            MatterPerTissueJoule = 0f,
            MatterPerCreature = ChildMatter,
            InitialMatterPerCubicMetre = 0f,
            MatterSinkMetresPerSecond = 0f,
            MatterMixingDiffusivity = 0f,
            MatterRemineralisationPerSecond = 0f,

            // An old parent that earns less is a second explanation for a child count, and this
            // test is meant to have one.
            SenescenceDoublingSeconds = 0f,

            PerOffspringOverheadJoules = 0.01f,
        };

        /// <summary>Every variation operator off, so an offspring is its parent's genome.</summary>
        private static MutationRates Clones() => new MutationRates
        {
            ScalarChance = 0f,
            AddNodeChance = 0f,
            AddEdgeChance = 0f,
            RemoveEdgeChance = 0f,
            AddNeuronChance = 0f,
            RemoveNeuronChance = 0f,
            RewireInputChance = 0f,
            NeuronOpChance = 0f,
            JointTypeChance = 0f,
            FlagChance = 0f,
            RecursiveLimitChance = 0f,
            ShapeChance = 0f,
            CellTypeChance = 0f,
            BroodSizeChance = 0f,
            EndowmentChance = 0f,
        };

        /// <summary>One photosynthetic box that breeds one cheap child at a time.</summary>
        private static Genome Leaf()
        {
            var g = new Genome();
            g.Nodes.Add(new MorphNode
            {
                CellTypeId = CellTypeIds.Photosynthetic,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });
            g.RootIndex = 0;
            g.Reproduction = new ReproductionTraits
            {
                BroodSize = 1,
                OffspringEndowment = 0.01f,
            };
            return g;
        }

        /// <summary>
        /// The same leaf spread out: a plate of the same green tissue, wider than it is thick.
        /// </summary>
        /// <remarks>
        /// Income is lit area and lit area is a quarter of the surface (§5A.1), so flattening a
        /// body buys light faster than it buys the volume it is charged for. This one holds 2.25
        /// times the leaf's volume — and so costs 2.25 times as much to build a child of — while
        /// catching 3.5 times the light, which is what makes it richer every step rather than
        /// merely bigger. A creature selection could plausibly find, rather than a contrivance:
        /// the shape asymmetry <c>AbsorptiveCell</c>'s remarks describe, used deliberately.
        /// </remarks>
        private static Genome Plate()
        {
            Genome g = Leaf();
            g.Nodes[0].Dimensions = new Float3(0.6f, 0.05f, 0.6f);
            return g;
        }

        /// <summary>
        /// Puts every creature but the two contestants out of the contest, and holds the
        /// contestants at the surface.
        /// </summary>
        /// <remarks>
        /// The children have to go somewhere. Left where they were born they would be solvent
        /// leaves in the contested layer within a step or two, and by the two hundredth step the
        /// two founders would be sharing the layer's one unit with two hundred of their own
        /// offspring — a fair contest, and not the one being measured. Moved to a layer that never
        /// has matter in it they cannot conceive at all, which is exactly what the test needs and
        /// nothing more: they are not killed, not excluded from the walk, and not treated
        /// differently by anything in <see cref="World"/>.
        /// </remarks>
        private static void Exile(World world, Organism elder, Organism younger)
        {
            foreach (Organism creature in world.Living)
            {
                world.Observe(
                    creature,
                    creature == elder || creature == younger ? SurfaceY : ExileY,
                    workJoules: 0f);
            }
        }

        /// <summary>
        /// Empties the matter field and puts one child's price back into the contested layer.
        /// </summary>
        /// <remarks>
        /// Every layer is drained, not just the contested one, because matter is conserved: a
        /// child locks its price away and its death returns it to whatever layer it died in
        /// (D052). Without the drain the exiles' deaths would slowly refill the deep and the
        /// world would stop being the one-unit-per-step contest it was set up as.
        /// </remarks>
        private static void RestockOneChildsWorth(World world)
        {
            NutrientField matter = world.Matter;

            for (int layer = 0; layer < matter.LayerCount; layer++)
            {
                float y = -((layer + 0.5f) * matter.LayerMetres);
                matter.Take(y, (float)matter.StockInLayer(layer) + 1f);
            }

            // A hair over the price rather than exactly it, so that a float rounding down cannot
            // refuse the first parent as well as the second — and far under twice it, so the
            // second parent is refused however the rounding goes.
            matter.Deposit(SurfaceY, ChildMatter * 1.001f);
        }

        // ---------------------------------------------------------------------------------
        // Comparing two worlds.
        // ---------------------------------------------------------------------------------

        /// <summary>The reference world for the replay tests: the defaults, with the knob set.</summary>
        private static RunConfig Reference(ConceptionOrder? order = null)
        {
            var config = new RunConfig();
            if (order.HasValue) config.ConceptionOrder = order.Value;
            return config;
        }

        private static string LivingIds(World world)
        {
            var ids = new List<string>(world.Living.Count);
            foreach (Organism creature in world.Living) ids.Add(creature.Id.ToString());
            return string.Join(",", ids);
        }

        private static string Describe(string label, World world) =>
            FormattableString.Invariant(
                $"{label}: alive {world.Living.Count}, births {world.Births}, deaths {world.Deaths}, ") +
            FormattableString.Invariant(
                $"floor {world.FloorSpawns}, standing {world.StandingJoules:0.######} J, ") +
            FormattableString.Invariant(
                $"in {world.EnergyIn:0.######} J, out {world.EnergyOut:0.######} J, ") +
            FormattableString.Invariant(
                $"matter {world.StandingMatter:0.######}");

        private static void AssertSameWorld(World a, World b)
        {
            Assert.Equal(a.Births, b.Births);
            Assert.Equal(a.Deaths, b.Deaths);
            Assert.Equal(a.FloorSpawns, b.FloorSpawns);
            Assert.Equal(a.Stillbirths, b.Stillbirths);
            Assert.Equal(a.ConceptionsBlockedByMatter, b.ConceptionsBlockedByMatter);

            Assert.Equal(a.EnergyIn, b.EnergyIn);
            Assert.Equal(a.EnergyOut, b.EnergyOut);
            Assert.Equal(a.StandingJoules, b.StandingJoules);
            Assert.Equal(a.StandingMatter, b.StandingMatter);

            // Last and most specific: the same creatures, in the same places in the list. Two
            // walks that breed the same number of times can still breed different bodies, and this
            // is the assertion that can tell.
            Assert.Equal(LivingIds(a), LivingIds(b));
        }

        private static string Line(string text, string key)
        {
            foreach (string line in text.Split('\n'))
            {
                if (line.Contains(key)) return line.Trim();
            }

            return $"({key} not in the file)";
        }
    }
}
