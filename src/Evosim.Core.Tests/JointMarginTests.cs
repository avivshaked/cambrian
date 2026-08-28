using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// What owning a joint costs, against what a creature earns — DESIGN.md §5A.1, §5A.2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The oldest unsolved failure in this project: nothing has ever evolved a working
    /// muscle.</b> D031 and D032 swept actuator cost across irradiances from 64 to 400 W/m² and
    /// found nothing alive with a joint at any setting. That established the failure is not a
    /// knob, and did not establish what it is.
    /// </para>
    /// <para>
    /// Three failures are possible and they want different fixes. <b>Unreachable</b> — mutation
    /// cannot produce a jointed creature; ruled out, because 20% of founders have joints
    /// (logbook/0026). <b>Useless</b> — joints work and buy nothing. <b>Unaffordable</b> — a
    /// joint costs more than a creature can pay, so it is eliminated before its benefit can ever
    /// be tested. These measure the third, because it is the one that can be settled by
    /// arithmetic and because if it holds, the second cannot even be asked.
    /// </para>
    /// <para>
    /// <b>A link is charged twice.</b> <see cref="LinkCell.Acquire"/> returns
    /// <see cref="CellIntake.None"/>, so link tissue earns nothing — the volume forfeits whatever
    /// it would have made as photosynthetic tissue. On top of that sits
    /// <see cref="LinkCell.IdleWattsPerNewtonMetre"/> × power × dof, paid every second whether or
    /// not the joint moves. Neither charge depends on the creature doing anything, so both are
    /// paid in full by a creature that never actuates.
    /// </para>
    /// </remarks>
    public class JointMarginTests
    {
        private readonly ITestOutputHelper _output;

        public JointMarginTests(ITestOutputHelper output) => _output = output;

        /// <summary>The world every recent run was measured in.</summary>
        private static RunConfig World(float maxLinkPower = 20f)
        {
            var config = new RunConfig { Light = new LightModel(64f, 12f) };
            config.Genome.MaxLinkPower = maxLinkPower;
            return config;
        }

        /// <summary>
        /// A two-part creature: a photosynthetic root plus one child of the given cell type.
        /// </summary>
        /// <remarks>
        /// Half-extent 0.35 m, which is what real survivors measure — the snapshots show
        /// dimensions around 0.348. A one-metre fixture would be a creature this world has never
        /// produced, and the whole point here is what an actual creature can afford.
        /// </remarks>
        private static Genome TwoPart(string childCell, float power, JointType joint)
        {
            var g = new Genome();

            MorphNode root = Fixtures.Box(half: 0.35f);
            root.CellTypeId = CellTypeIds.Photosynthetic;

            MorphNode child = Fixtures.Box(half: 0.35f, joint: joint);
            child.CellTypeId = childCell;
            child.Power = power;
            if (joint == JointType.Fixed) { child.Power = 0f; child.JointLimits = Array.Empty<Float2>(); }

            root.Edges.Add(Fixtures.FaceToFace(1));
            g.Nodes.Add(root);
            g.Nodes.Add(child);
            g.RootIndex = 0;
            return g;
        }

        private (float net, float income, float costs) Price(
            RunConfig config, Genome genome, float workJoules = 0f)
        {
            Phenotype body = Developer.Develop(genome, config.Development, shapes: config.Shapes);

            EnergyLedger led = Metabolism.StepAt(
                body, config, config.Light.IrradianceAt(-2f),
                nutrientDensity: 10f, workJoules: workJoules, seconds: 1f);

            return (led.Net, led.Income, led.Expenditure);
        }

        [Fact]
        public void OwningAJointCostsMostOfWhatACreatureEarns()
        {
            // The measurement. Same body, same volume, same depth — the only difference is
            // whether the second part is photosynthetic tissue or a hinge.
            RunConfig config = World();

            var plant = Price(config, TwoPart(CellTypeIds.Photosynthetic, 0f, JointType.Fixed));
            var idle = Price(config, TwoPart(CellTypeIds.Link, 20f, JointType.Hinge));

            _output.WriteLine(
                $"two photosynthetic parts : income {plant.income,7:0.####} - costs {plant.costs,7:0.####} = {plant.net,8:0.####} W");
            _output.WriteLine(
                $"one part + an idle hinge : income {idle.income,7:0.####} - costs {idle.costs,7:0.####} = {idle.net,8:0.####} W");
            _output.WriteLine("");

            float lost = plant.net - idle.net;
            _output.WriteLine(
                $"a hinge costs {lost:0.####} W, which is {100f * lost / Math.Max(1e-9f, plant.net):0.#}% " +
                "of what the same creature earns without one");

            Assert.True(plant.net > 0f, "the reference creature is not solvent, so nothing here means anything");
        }

        [Fact]
        public void TheIdleChargeAloneIsComparedAgainstTheWholeSurplus()
        {
            // Separating the two charges, because they have different fixes. The forfeited income
            // is structural — a link cannot photosynthesise, and §5A.1 means that deliberately.
            // The idle charge is a knob (D031's, still unmeasured) and could be set anywhere.
            RunConfig config = World();

            var plant = Price(config, TwoPart(CellTypeIds.Photosynthetic, 0f, JointType.Fixed));
            var structural = Price(config, TwoPart(CellTypeIds.Structural, 0f, JointType.Fixed));
            var hinge = Price(config, TwoPart(CellTypeIds.Link, 20f, JointType.Hinge));

            float forfeited = plant.net - structural.net;   // income the volume no longer earns
            float idleCharge = structural.net - hinge.net;  // what capacity costs to own

            _output.WriteLine($"surplus with two photosynthetic parts : {plant.net:0.####} W");
            _output.WriteLine($"income forfeited by non-earning tissue: {forfeited:0.####} W");
            _output.WriteLine($"idle charge for 20 N·m of capacity   : {idleCharge:0.####} W");
            _output.WriteLine($"surplus left to a jointed creature    : {hinge.net:0.####} W");

            Assert.True(
                Math.Abs((forfeited + idleCharge) - (plant.net - hinge.net)) < 1e-4f,
                "the two charges do not add up to the difference, so this decomposition is wrong");
        }

        /// <summary>N photosynthetic parts in a chain, the last of which may be a link.</summary>
        private static Genome Chain(int photosyntheticParts, bool endsInJoint, float power)
        {
            var g = new Genome();
            int total = photosyntheticParts + (endsInJoint ? 1 : 0);

            for (int i = 0; i < total; i++)
            {
                bool link = endsInJoint && i == total - 1;
                MorphNode n = Fixtures.Box(half: 0.35f, joint: link ? JointType.Hinge : JointType.Fixed);
                n.CellTypeId = link ? CellTypeIds.Link : CellTypeIds.Photosynthetic;
                n.Power = link ? power : 0f;
                if (!link) n.JointLimits = Array.Empty<Float2>();
                if (i < total - 1) n.Edges.Add(Fixtures.FaceToFace(i + 1));
                g.Nodes.Add(n);
            }

            g.RootIndex = 0;
            return g;
        }

        [Fact]
        public void HowBigABodyHasToBeBeforeItCanCarryAJoint()
        {
            // Income scales with the body; the idle charge does not. So affordability is not
            // fixed — a large enough creature can carry capacity a small one cannot. This asks
            // how large, because "joints are unaffordable" and "joints need a body larger than
            // anything this world evolves" are different problems with different fixes.
            //
            // Real survivors average about 1.1 parts (logbook/0018 onward), so anything needing
            // several photosynthetic parts to pay for one hinge is out of reach in practice even
            // where it is solvent on paper.
            RunConfig config = World(maxLinkPower: 120f);

            foreach (float power in new[] { 5f, 20f })
            {
                _output.WriteLine($"--- a hinge carrying {power:0.#} N·m ---");

                for (int parts = 1; parts <= 8; parts++)
                {
                    var with = Price(config, Chain(parts, endsInJoint: true, power));
                    var without = Price(config, Chain(parts, endsInJoint: false, 0f));

                    string verdict = with.net > 0f ? "solvent" : "insolvent";
                    _output.WriteLine(
                        $"  {parts} photosynthetic part(s): jointless {without.net,7:0.###} W, " +
                        $"jointed {with.net,7:0.###} W  {verdict}");
                }
                _output.WriteLine("");
            }
        }

        [Fact]
        public void ThePowerAJointCarriesDecidesWhetherItCanBeOwnedAtAll()
        {
            // §5A.10 marks the idle rate unmeasured, and D032 corrected a claim that this knob and
            // MaxLinkPower were interchangeable: both enter cost, only MaxLinkPower enters
            // benefit. So the affordable capacity is what this locates — the power at which a
            // jointed creature stops being solvent at all, before it has moved once.
            RunConfig config = World();

            var plant = Price(config, TwoPart(CellTypeIds.Photosynthetic, 0f, JointType.Fixed));
            _output.WriteLine($"reference surplus, no joint: {plant.net:0.####} W");
            _output.WriteLine("");

            foreach (float power in new[] { 1f, 2f, 5f, 8f, 10f, 20f, 60f, 120f })
            {
                config.Genome.MaxLinkPower = Math.Max(power, 120f);
                var hinge = Price(config, TwoPart(CellTypeIds.Link, power, JointType.Hinge));

                string verdict = hinge.net > 0f ? "solvent" : "INSOLVENT before moving";
                _output.WriteLine(
                    $"  {power,5:0.#} N·m capacity: net {hinge.net,8:0.####} W  " +
                    $"({100f * hinge.net / Math.Max(1e-9f, plant.net),5:0.#}% of the jointless creature)  {verdict}");
            }
        }

        [Fact]
        public void HowMuchOfAFounderGenomeSurvivesDevelopment()
        {
            // Founders draw 2-5 nodes (RandomGenomeOptions.MinNodes/MaxNodes) and real survivors
            // average about 1.1 parts. A node only becomes a part if an edge reaches it from the
            // root, so the question is whether creatures are small because selection prefers
            // small, or because most of the genome is orphaned before selection ever sees it.
            //
            // These are different problems. The first is ecology and would be answered by
            // changing what the world rewards; the second is development, and no amount of
            // selection pressure can act on material that never becomes a body.
            var config = new RunConfig { Light = new LightModel(64f, 12f) };
            var options = new RandomGenomeOptions();
            var rng = new Rng(90210);

            const int Draws = 2000;
            int nodes = 0, parts = 0, jointed = 0, singlePart = 0, canCarryAJoint = 0;

            for (int i = 0; i < Draws; i++)
            {
                Genome g = GenomeFactory.Founder(rng, options);
                Phenotype body = Developer.Develop(g, config.Development, shapes: config.Shapes);

                nodes += g.Nodes.Count;
                parts += body.Parts.Count;
                if (body.Parts.Count == 1) singlePart++;

                bool hasJoint = false;
                foreach (PhenotypePart part in body.Parts)
                {
                    if (part.JointType.DofCount() > 0) { hasJoint = true; break; }
                }
                if (hasJoint) jointed++;

                // Three parts is what the affordability sweep says a 20 N·m hinge needs.
                if (body.Parts.Count >= 3) canCarryAJoint++;
            }

            double meanNodes = (double)nodes / Draws;
            double meanParts = (double)parts / Draws;

            _output.WriteLine($"founders drawn: {Draws:N0}");
            _output.WriteLine($"  mean nodes in the genome : {meanNodes:0.##}");
            _output.WriteLine($"  mean parts after growth  : {meanParts:0.##}");
            _output.WriteLine($"  genome reaching the body : {100.0 * meanParts / meanNodes:0.#}%");
            _output.WriteLine($"  exactly one part         : {100.0 * singlePart / Draws:0.#}%");
            _output.WriteLine($"  carrying a joint         : {100.0 * jointed / Draws:0.#}%");
            _output.WriteLine($"  big enough for a 20 N·m hinge (>=3 parts): {100.0 * canCarryAJoint / Draws:0.#}%");

            Assert.True(meanParts > 0, "no founder developed into anything");
        }
    }
}
