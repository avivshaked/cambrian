using System;
using System.Collections.Generic;
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

        [Fact]
        public void WhatTheJointProbesActuallyMadeAffordable()
        {
            // Checking my own experiment rather than its result. Two probes were launched to ask
            // whether an affordable joint is useful, and the question is only answerable if they
            // made one. The three charges are 1.30 W of forfeited photosynthesis, 0.51 W of
            // higher link upkeep, and 0.40 W of idle capacity — and BOTH probes moved only the
            // last, which is the smallest.
            var cases = new (string name, float power, float idle)[]
            {
                ("control     (5-20 N·m, idle 0.02)",  20f,  0.02f),
                ("joint-weak  (1-4 N·m,  idle 0.02)",   2.5f, 0.02f),
                ("joint-strong(10-20 N·m, idle 0.002)", 15f,  0.002f),
            };

            foreach (var c in cases)
            {
                var config = new RunConfig { Light = new LightModel(64f, 12f) };
                config.Genome.MaxLinkPower = 120f;
                config.CellTypes = new CellTypeRegistry(
                    new StructuralCell(), new LinkCell(c.idle), new NeuralCell(),
                    new PhotosyntheticCell(), new AbsorptiveCell(), new ConsumerCell());

                var plant = Price(config, TwoPart(CellTypeIds.Photosynthetic, 0f, JointType.Fixed));
                var hinge = Price(config, TwoPart(CellTypeIds.Link, c.power, JointType.Hinge));

                _output.WriteLine(
                    $"{c.name,-34} jointless {plant.net,7:0.###} W -> jointed {hinge.net,7:0.###} W " +
                    $"({100f * hinge.net / plant.net,5:0.#}% of it)");
            }

            _output.WriteLine("");
            _output.WriteLine(
                "If the probe arms sit near zero rather than comfortably positive, they are " +
                "break-even arms, and break-even is not viability (§5A.6d). They would then be " +
                "unable to answer whether a joint is USEFUL, because no creature in them can " +
                "afford to find out.");
        }
        /// <summary>
        /// Muscle that earns is the only term big enough to make a two-part flagellate solvent.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The three charges on a 20 N·m hinge are 1.30 W of forfeited photosynthesis, 0.51 W of
        /// link upkeep and 0.40 W of idle capacity (logbook/0026). Every sweep this project has
        /// run — D031, D032, and the two probes of 2026-08-28 — moved one of the second two, which
        /// together are less than half the first. This asserts the arithmetic that says they could
        /// not have worked, and that the remaining term can.
        /// </para>
        /// <para>
        /// The claim is deliberately about <i>solvency</i> and not about swimming. Whether an
        /// affordable joint is any use is the measurement logbook/0026 named as still open, and it
        /// needs a run rather than a test.
        /// </para>
        /// </remarks>
        [Fact]
        public void MuscleThatEarnsIsWhatMakesATwoPartFlagellateSolvent()
        {
            Genome jointless = TwoPart(CellTypeIds.Photosynthetic, 0f, JointType.Fixed);
            Genome jointed = TwoPart(CellTypeIds.Link, 20f, JointType.Hinge);

            var rows = new List<(float photo, float net)>();

            foreach (float photo in new[] { 0f, 0.25f, 0.5f, 1f })
            {
                RunConfig config = World();
                config.CellTypes = new CellTypeRegistry(
                    new StructuralCell(),
                    new LinkCell(
                        0.02f,
                        photosyntheticEfficiency: photo * PhotosyntheticCell.DefaultEfficiency),
                    new NeuralCell(),
                    new PhotosyntheticCell(),
                    new AbsorptiveCell(),
                    new ConsumerCell());

                (float net, _, _) = Price(config, jointed);
                rows.Add((photo, net));
            }

            (float plantNet, _, _) = Price(World(), jointless);

            _output.WriteLine($"two photosynthetic parts, no joint : {plantNet,8:F4} W");
            foreach ((float photo, float net) in rows)
            {
                _output.WriteLine(
                    $"one part + 20 N.m hinge, linkPhoto {photo:F2} : {net,8:F4} W" +
                    (net > 0f ? "  solvent" : "  insolvent"));
            }

            // The world as it stands: insolvent before it actuates once. This is D042.
            Assert.True(rows[0].net < 0f, $"expected insolvent at linkPhoto 0, got {rows[0].net}");

            // And solvent once the forfeited income is returned. Half is enough, which is the
            // point: the term is large enough that it does not need to be taken all the way.
            Assert.True(
                rows[2].net > 0f,
                $"expected solvent at linkPhoto 0.5, got {rows[2].net}");

            // Still worse than simply being a plant, or the trade-off has been abolished rather
            // than priced and a joint would drift neutrally instead of being selected.
            Assert.True(
                rows[3].net < plantNet,
                $"a fully photosynthetic link ({rows[3].net}) must still lose to two green " +
                $"parts ({plantNet}), or there is no cost to carrying a joint at all");
        }

        [Fact]
        public void TheCeilingOnAnEarningMuscleIsSetByCapacity()
        {
            // The 88% figure D043 rests on was measured at Power = 20 N.m — MaxLinkPower, the
            // most expensive joint a founder can draw. Power is evolvable per node down to
            // MinLinkPower, and LinkCell bills in proportion to it, so the interesting question
            // is not what the worst joint costs but what the cheapest one does: a lineage that
            // can lower its capacity is walking down this curve, and whether the curve reaches
            // parity decides whether "structurally closed" is a property of the design or of the
            // number the test happened to pick.
            Genome jointless = TwoPart(CellTypeIds.Photosynthetic, 0f, JointType.Fixed);
            (float plantNet, _, _) = Price(World(), jointless);

            _output.WriteLine($"two photosynthetic parts, no joint : {plantNet,8:F4} W");
            _output.WriteLine("");
            _output.WriteLine("  N.m    linkPhoto 0     linkPhoto 1    share of plant");

            float bestShare = 0f;

            foreach (float power in new[] { 5f, 10f, 20f, 60f, 120f })
            {
                var nets = new float[2];
                float[] photos = { 0f, 1f };

                for (int i = 0; i < photos.Length; i++)
                {
                    RunConfig config = World();
                    config.CellTypes = new CellTypeRegistry(
                        new StructuralCell(),
                        new LinkCell(
                            0.02f,
                            photosyntheticEfficiency: photos[i] * PhotosyntheticCell.DefaultEfficiency),
                        new NeuralCell(),
                        new PhotosyntheticCell(),
                        new AbsorptiveCell(),
                        new ConsumerCell());

                    (nets[i], _, _) = Price(config, TwoPart(CellTypeIds.Link, power, JointType.Hinge));
                }

                float share = plantNet > 0f ? nets[1] / plantNet : 0f;
                bestShare = Math.Max(bestShare, share);
                _output.WriteLine(
                    $"{power,5:F0}  {nets[0],10:F4} W  {nets[1],10:F4} W  {100f * share,12:F1}%");
            }

            // The curve must actually move with capacity, or the charge is not doing its job and
            // D032's whole premise — that billing for capacity is what stops evolution sitting at
            // the ceiling — is false.
            Assert.True(
                bestShare > 0.88f,
                $"the cheapest earning joint reaches {100f * bestShare:F1}% of a plant, no better " +
                "than the 20 N.m case D043 measured — capacity is not being priced");

            // And it must still not reach parity, or a joint is free and would drift rather than
            // be selected.
            Assert.True(
                bestShare < 1f,
                $"an earning joint reached {100f * bestShare:F1}% of a plant — at parity there is " +
                "no cost to carrying one");
        }
        /// <summary>
        /// How deep a creature can live, which bounds everything sinking could ever be worth.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Income falls exponentially with depth and upkeep does not fall at all, so there is a
        /// depth below which nothing solvent exists. That depth is the <i>whole</i> arena any
        /// depth-based selection pressure has to work in: a sink that carries a creature past it
        /// does not make swimming valuable, it makes the world uninhabitable, and the run reports
        /// the same thing either way — a population pinned at the floor with generation 0.
        /// </para>
        /// <para>
        /// This is what the <c>sink</c> arm of 2026-08-28 needed and did not have. It ran at
        /// 0.15 kg/m³, carrying creatures to −20 m, and spent 40,000 s entirely floor-fed while
        /// mean lifetime expenditure ran at 2.7x mean lifetime income (logbook/0027).
        /// </para>
        /// </remarks>
        [Fact]
        public void HowDeepACreatureCanStillPayItsBills()
        {
            Genome plant = TwoPart(CellTypeIds.Photosynthetic, 0f, JointType.Fixed);
            RunConfig config = World();

            _output.WriteLine($"{"depth m",8} {"irradiance",11} {"net W",10}");

            float lastSolvent = 0f, firstInsolvent = float.NaN;

            for (float depth = 0f; depth >= -40f; depth -= 2f)
            {
                Phenotype body = Developer.Develop(
                    plant, config.Development, shapes: config.Shapes);

                float irradiance = config.Light.IrradianceAt(depth);
                EnergyLedger led = Metabolism.StepAt(
                    body, config, irradiance,
                    nutrientDensity: 0f, workJoules: 0f, seconds: 1f);

                _output.WriteLine($"{depth,8:F0} {irradiance,11:F2} {led.Net,10:F4}");

                if (led.Net > 0f) lastSolvent = depth;
                else if (float.IsNaN(firstInsolvent)) firstInsolvent = depth;
            }

            _output.WriteLine("");
            _output.WriteLine(
                $"solvent to {lastSolvent:F0} m; insolvent from {firstInsolvent:F0} m. " +
                $"Habitable band {Math.Abs(lastSolvent):F0} m.");

            // A band at all, or depth cannot be a gradient and no sink rate is survivable.
            Assert.True(
                lastSolvent < 0f,
                "nothing is solvent below the surface, so depth is not a selectable axis");

            // And a bounded one — if a creature is solvent at 40 m the light model is not
            // attenuating and the depth column in every run means nothing.
            Assert.False(
                float.IsNaN(firstInsolvent),
                "solvent at every depth to 40 m: attenuation is not biting");
        }
        /// <summary>
        /// What fraction of founders are born somewhere they can pay their bills.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Founders are scattered over <see cref="RunConfig.FounderDepthSpread"/> — 20 m — into a
        /// habitable band that is 8 m at 64 W/m².</b> `World.SpawnFounders` places them at
        /// <c>-rng.Range(0, spread)</c>, and its comment says "through the lit zone"; the lit zone
        /// is where a creature is solvent, and that was never measured until 2026-08-28
        /// (logbook/0027). Half of every floor spawn is therefore born below break-even, which
        /// halves the mutational supply of every run this project has performed.
        /// </para>
        /// <para>
        /// The default is not changed here. <b>Where founders start is a design decision</b> —
        /// D036 rejected shrinking the spread on the grounds that it flattens the vertical
        /// structure §5A.4 exists to provide, and that argument is untouched by this. What this
        /// asserts is the weaker thing that has to hold for any run to mean anything: that a
        /// world is not spawning founders almost entirely into water they cannot live in.
        /// </para>
        /// </remarks>
        [Fact]
        public void MostFoundersAreBornSomewhereTheyCanLive()
        {
            RunConfig config = World();
            Genome plant = TwoPart(CellTypeIds.Photosynthetic, 0f, JointType.Fixed);
            Phenotype body = Developer.Develop(plant, config.Development, shapes: config.Shapes);

            // Walk down in fine steps; the last solvent depth is the bottom of the band.
            float band = 0f;
            for (float d = 0f; d >= -60f; d -= 0.25f)
            {
                EnergyLedger led = Metabolism.StepAt(
                    body, config, config.Light.IrradianceAt(d),
                    nutrientDensity: 0f, workJoules: 0f, seconds: 1f);
                if (led.Net > 0f) band = -d; else break;
            }

            float spread = config.FounderDepthSpread;
            float viable = Math.Min(1f, band / Math.Max(0.0001f, spread));

            _output.WriteLine(
                $"irradiance {config.Light.SurfaceIrradiance:F0} W/m2, habitable band {band:F2} m, " +
                $"founder spread {spread:F0} m -> {viable * 100f:F0}% of founders born solvent");

            Assert.True(
                viable >= 0.25f,
                $"only {viable * 100f:F0}% of founders are born above the break-even depth " +
                $"({band:F2} m) given a spread of {spread:F0} m. Below a quarter, the floor is " +
                "mostly manufacturing corpses and no run's mutational supply means what it says.");
        }
    }
}
