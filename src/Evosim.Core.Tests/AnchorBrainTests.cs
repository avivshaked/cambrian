using System;
using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D268's anchoring build in Core: the cell's three new numbers (this file's first section), and the brain's hold output
    /// and two grip senses (the sections after it, as each step lands). The switch is on the cell, so a world without the
    /// cell is untouched by any of it.
    /// </summary>
    public sealed class AnchorBrainTests
    {
        private readonly ITestOutputHelper _out;

        public AnchorBrainTests(ITestOutputHelper output) => _out = output;

        // ------------------------------------------------------------------ the cell's numbers

        [Fact]
        public void TheDefaultsLeaveTheGripAsRoundFiftyNineBuiltIt()
        {
            var cell = new AnchoringCell();
            Assert.False(cell.BrainHold);
            Assert.Equal(-0.5f, cell.ReleaseBelow);
            Assert.Equal(5e-5f, cell.ReleaseJoulesPerNewton);
            Assert.Equal(cell.BuildJoulesPerNewton, cell.ReleaseJoulesPerNewton);
        }

        [Fact]
        public void TheNewNumbersSurviveAJsonRoundTripAndReachTheHash()
        {
            var cell = new AnchoringCell(
                tenacityNewtonsPerSquareMetre: 3e4f, buildJoulesPerNewton: 5e-8f, decaySeconds: 900f,
                reserveThresholdSeconds: 45f, reserveLagSeconds: 15f, upkeepWattsPerCubicMetre: 3f,
                brainHold: true, releaseBelow: -0.25f, releaseJoulesPerNewton: 2e-8f);
            var config = new RunConfig { CellTypes = CellTypeRegistry.Standard.Appending(cell) };

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(config), out string mismatch);
            Assert.Null(mismatch);
            Assert.Equal(config.Hash(), back.Hash());

            var read = (AnchoringCell)back.CellTypes.Resolve(CellTypeIds.Anchoring);
            Assert.True(read.BrainHold);
            Assert.Equal(-0.25f, read.ReleaseBelow);
            Assert.Equal(2e-8f, read.ReleaseJoulesPerNewton);
            Assert.Equal(5e-8f, read.BuildJoulesPerNewton);
        }

        [Fact]
        public void EachNewNumberMovesTheCellsHash()
        {
            string Hash(AnchoringCell c) => c.HashContribution();
            string plain = Hash(new AnchoringCell());

            Assert.NotEqual(plain, Hash(new AnchoringCell(brainHold: true)));
            Assert.NotEqual(plain, Hash(new AnchoringCell(releaseBelow: -0.4f)));
            Assert.NotEqual(plain, Hash(new AnchoringCell(releaseJoulesPerNewton: 1e-5f)));
            Assert.Equal(plain, Hash(new AnchoringCell()));

            // And through the table, which is what the config hash reads.
            string Table(AnchoringCell c) => CellTypeRegistry.Standard.Appending(c).HashContribution();
            Assert.NotEqual(Table(new AnchoringCell()), Table(new AnchoringCell(brainHold: true)));
        }

        [Theory]
        [InlineData("brainHold")]
        [InlineData("releaseBelow")]
        [InlineData("releaseJoulesPerNewton")]
        public void AMissingNewFieldIsRefusedByName(string field)
        {
            var config = new RunConfig { CellTypes = CellTypeRegistry.Standard.Appending(new AnchoringCell()) };
            string text = RunConfigJson.Write(config);

            // The anchoring cell is the table's last entry; rename the field in it alone, which leaves it missing.
            string key = "\"" + field + "\"";
            int at = text.LastIndexOf(key, StringComparison.Ordinal);
            Assert.True(at >= 0, $"no {key} in the written config");
            string cut = text.Remove(at, key.Length).Insert(at, "\"" + field + "Gone\"");

            Exception e = Assert.ThrowsAny<Exception>(() => RunConfigJson.Read(cut));
            _out.WriteLine(e.Message);
            Assert.Contains(field, e.Message);
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        [InlineData(1.01f)]
        [InlineData(-1.01f)]
        public void AThresholdOutsideTheOutputsRangeIsRefused(float below)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnchoringCell(releaseBelow: below));
        }

        [Theory]
        [InlineData(-1f)]
        [InlineData(0f)]
        [InlineData(1f)]
        public void AThresholdInTheRangeIsAccepted(float below)
        {
            Assert.Equal(below, new AnchoringCell(releaseBelow: below).ReleaseBelow);
        }

        [Theory]
        [InlineData(-1e-9f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void ANegativeOrNonFiniteReleasePriceIsRefused(float price)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnchoringCell(releaseJoulesPerNewton: price));
        }

        [Fact]
        public void AZeroReleasePriceIsAllowedWhereAZeroBuildPriceIsNot()
        {
            var free = new AnchoringCell(releaseJoulesPerNewton: 0f);
            Assert.Equal(0f, free.ReleasePrice(210f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnchoringCell(buildJoulesPerNewton: 0f));
        }

        [Fact]
        public void BothPricesScaleByTheFullBreakingForce()
        {
            var cell = new AnchoringCell(buildJoulesPerNewton: 5e-8f, releaseJoulesPerNewton: 2e-8f);
            float force = cell.BreakingForce(Cube(0.06f));

            Assert.Equal(2e-8f * force, cell.ReleasePrice(force), 12);
            Assert.Equal(5e-8f * force, cell.BuildPrice(force), 12);
            Assert.Equal(0f, cell.ReleasePrice(-5f));

            // A bud twice the size costs 2^(2/3) times as much to let go of: by area, as the force is.
            float big = cell.BreakingForce(2f * Cube(0.06f));
            Assert.Equal(Math.Pow(2.0, 2.0 / 3.0), cell.ReleasePrice(big) / cell.ReleasePrice(force), 4);
        }

        private static float Cube(float edge) => edge * edge * edge;

        // ------------------------------------------------------------------ the brain's hold output and senses

        private sealed class Field : ISensorField
        {
            public readonly Dictionary<(int, SensorChannel, int), float> Values = new Dictionary<(int, SensorChannel, int), float>();
            public float Read(int partIndex, SensorChannel channel, int index) =>
                Values.TryGetValue((partIndex, channel, index), out float v) ? v : 0f;
        }

        private static CellTypeRegistry Table(bool hold) =>
            CellTypeRegistry.Standard.Appending(new AnchoringCell(brainHold: hold));

        private static RunConfig Config(bool hold) => new RunConfig { CellTypes = Table(hold) };

        private static MorphEdge Edge(int child, Float3 at, Bool3 reflect) => new MorphEdge
        {
            Child = child,
            ParentAnchor = at,
            ChildAnchor = -at,
            Orientation = Quat.Identity,
            Scale = Float3.One,
            Reflect = reflect,
        };

        /// <summary>
        /// A root, a hinge P on its +X face and, on its +Y face, an anchoring part with a mirror copy on -Y when
        /// <paramref name="pair"/>. Parts in order: root 0, P 1, the anchoring parts 2 (and 3).
        /// </summary>
        private static Genome Gripper(bool anchors = true, bool pair = true)
        {
            var g = new Genome();
            g.Nodes.Add(Fixtures.Box());
            g.Nodes.Add(Fixtures.Box(0.3f, JointType.Hinge));
            g.Nodes[0].Edges.Add(Edge(1, new Float3(1f, 0f, 0f), Bool3.None));
            if (anchors)
            {
                MorphNode anchor = Fixtures.Box(0.2f);
                anchor.CellTypeId = CellTypeIds.Anchoring;
                g.Nodes.Add(anchor);
                g.Nodes[0].Edges.Add(Edge(2, new Float3(0f, 1f, 0f), pair ? new Bool3(false, true, false) : Bool3.None));
            }

            CentralWiring.Convert(g);
            return g;
        }

        private static Phenotype Develop(Genome g, bool hold) =>
            Developer.Develop(g, DevelopmentLimits.Default, cellTypes: Table(hold));

        private static string Dump(CentralBrain b) =>
            string.Join(";", b.Outputs.Select(u => $"{u.Id:x}:{u.Bias:R}")) + "|" +
            string.Join(";", b.Hidden.Select(u => $"{u.Id:x}:{u.Bias:R}")) + "|" +
            string.Join(";", b.Links.Select(l => $"{l.From:x}>{l.To:x}:{l.Weight:R}"));

        [Fact]
        public void TheNewChannelsTakeNumbersSeventeenAndEighteen()
        {
            Assert.Equal(17, (int)SensorChannel.Gripping);
            Assert.Equal(18, (int)SensorChannel.GripPull);
            Assert.Equal(2, (int)BrainOutputKind.Grip);
            Assert.Equal(1, SensorChannel.Gripping.IndexCount());
            Assert.Equal(1, SensorChannel.GripPull.IndexCount());
        }

        [Fact]
        public void OffEveryPortOutputAndPoolIsWhatItWas()
        {
            // The pool: with the cell absent, with it present and off, and with it present and on.
            SensorChannel[] standard = new RunConfig().SensorPool();
            Assert.Equal(SensorChannels.DefaultPool, standard);
            Assert.Equal(standard, Config(hold: false).SensorPool());
            Assert.Equal(standard.Concat(new[] { SensorChannel.Gripping, SensorChannel.GripPull }), Config(hold: true).SensorPool());

            // A body with no anchoring part is the body it was under either table, ports and outputs and a founder's brain.
            Genome plain = Gripper(anchors: false);
            Phenotype under = Developer.Develop(plain, DevelopmentLimits.Default, cellTypes: CellTypeRegistry.Standard);
            Phenotype off = Develop(plain, hold: false);
            Phenotype on = Develop(plain, hold: true);
            Assert.All(on.Parts, p => Assert.False(p.Grips));

            foreach ((Phenotype body, bool hold) in new[] { (off, false), (on, true) })
            {
                SensorChannel[] pool = Config(hold).SensorPool();
                Assert.Equal(
                    CentralWiring.Outputs(under).Select(o => (o.Id, o.Kind, o.DriveIndex)),
                    CentralWiring.Outputs(body).Select(o => (o.Id, o.Kind, o.DriveIndex)));
                Assert.Equal(
                    CentralWiring.Ports(under, standard).Select(p => p.Id),
                    CentralWiring.Ports(body, pool).Select(p => p.Id));
                Assert.Equal(
                    Dump(CentralWiring.Effective(new CentralBrain(), under, new Rng(5), standard, new CentralBrainOptions(), founder: true)),
                    Dump(CentralWiring.Effective(new CentralBrain(), body, new Rng(5), pool, new CentralBrainOptions(), founder: true)));
            }

            // An anchoring part under the switch off has none of it either.
            Phenotype anchored = Develop(Gripper(), hold: false);
            Assert.All(anchored.Parts, p => Assert.False(p.Grips));
            Assert.DoesNotContain(CentralWiring.Outputs(anchored), o => o.Kind == BrainOutputKind.Grip);
            Assert.DoesNotContain(
                CentralWiring.Ports(anchored, Config(hold: false).SensorPool()), p => p.Channel == SensorChannel.Gripping);
        }

        [Fact]
        public void ReplacingTheTableDropsTheCachedPool()
        {
            var config = new RunConfig();
            Assert.Equal(SensorChannels.DefaultPool, config.SensorPool());
            config.CellTypes = Table(hold: true);
            Assert.Equal(SensorChannel.GripPull, config.SensorPool().Last());
            config.CellTypes = Table(hold: false);
            Assert.Equal(SensorChannels.DefaultPool, config.SensorPool());
        }

        [Fact]
        public void TheGripSensesComeAfterTheLeafSmellInThePool()
        {
            RunConfig config = Config(hold: true);
            config.SenseBedTouch = true;
            config.LeafScent = LeafScentForm.Direction;
            SensorChannel[] pool = config.SensorPool();

            Assert.Equal(SensorChannels.DefaultPool.Length + 4, pool.Length);
            Assert.Equal(SensorChannel.BedTouch, pool[pool.Length - 4]);
            Assert.Equal(SensorChannel.LeafScentDirection, pool[pool.Length - 3]);
            Assert.Equal(SensorChannel.Gripping, pool[pool.Length - 2]);
            Assert.Equal(SensorChannel.GripPull, pool[pool.Length - 1]);

            // The leaf smell's own slot is unmoved by the line that now advances past it.
            var without = new RunConfig { SenseBedTouch = true, LeafScent = LeafScentForm.Direction };
            Assert.Equal(without.SensorPool(), pool.Take(pool.Length - 2));
        }

        [Fact]
        public void OnAnAnchoringPartGainsOneHoldOutputAfterEveryJointOutputAndTwoPortsAndNothingElseDoes()
        {
            Phenotype body = Develop(Gripper(), hold: true);
            Assert.Equal(4, body.PartCount);
            Assert.Equal(new[] { false, false, true, true }, body.Parts.Select(p => p.Grips).ToArray());

            List<BrainOutput> outputs = CentralWiring.Outputs(body);
            Assert.Equal(
                new[] { BrainOutputKind.Power, BrainOutputKind.Bend, BrainOutputKind.Grip, BrainOutputKind.Grip },
                outputs.Select(o => o.Kind).ToArray());
            Assert.Equal(new[] { 0, 0, -1, -1 }, outputs.Select(o => o.DriveIndex).ToArray());
            Assert.Equal(new[] { 2, 3 }, outputs.Skip(2).Select(o => o.Part).ToArray());
            Assert.Equal(BrainIds.Output(body.Parts[2].PathId, 0, BrainOutputKind.Grip), outputs[2].Id);
            Assert.Equal(4, outputs.Select(o => o.Id).Distinct().Count());

            SensorChannel[] pool = Config(hold: true).SensorPool();
            List<BrainPort> grip = CentralWiring.Ports(body, pool)
                .Where(p => p.Channel == SensorChannel.Gripping || p.Channel == SensorChannel.GripPull).ToList();
            Assert.Equal(4, grip.Count);
            Assert.Equal(new[] { 2, 2, 3, 3 }, grip.Select(p => p.Part).ToArray());
            Assert.Equal(0, CentralWiring.IndicesOn(body.Parts[0], SensorChannel.Gripping));
            Assert.Equal(0, CentralWiring.IndicesOn(body.Parts[1], SensorChannel.GripPull));
            Assert.Equal(1, CentralWiring.IndicesOn(body.Parts[3], SensorChannel.GripPull));

            // The body's other copies keep the flag.
            Assert.All(body.Scaled(0.5f, PartShapeRegistry.Standard).Parts.Skip(2), p => Assert.True(p.Grips));
        }

        [Fact]
        public void RemovingThePartTakesItsOutputsPortsAndLinksAway()
        {
            Genome g = Gripper();
            Phenotype body = Develop(g, hold: true);
            List<BrainOutput> outputs = CentralWiring.Outputs(body);

            var brain = new CentralBrain();
            foreach (BrainOutput o in outputs) brain.Outputs.Add(new BrainUnit { Id = o.Id, Bias = 0.5f });
            ulong gripPort = BrainIds.Port(body.Parts[2].PathId, SensorChannel.Gripping, 0);
            brain.Links.Add(new BrainLink { From = gripPort, To = outputs[2].Id, Weight = 0.4f });
            brain.Links.Add(new BrainLink { From = gripPort, To = outputs[0].Id, Weight = 0.4f });
            brain.Links.Add(new BrainLink { From = outputs[2].Id, To = outputs[0].Id, Weight = 0.2f });

            CentralBrain kept = CentralWiring.Effective(brain, body);
            Assert.Equal(4, kept.Outputs.Count);
            Assert.Equal(3, kept.Links.Count);

            // The same genome without the part: its hold outputs, the two ports and every link that named them go.
            Phenotype lost = Develop(Gripper(anchors: false), hold: true);
            CentralBrain after = CentralWiring.Effective(brain, lost);
            Assert.Equal(2, after.Outputs.Count);
            Assert.DoesNotContain(after.Outputs, u => u.Id == outputs[2].Id || u.Id == outputs[3].Id);
            Assert.Empty(after.Links);

            // And under the switch off the genome keeps its stored hold outputs unused: the body has none to run.
            Phenotype off = Develop(g, hold: false);
            CentralBrain unused = CentralWiring.Effective(brain, off);
            Assert.Equal(2, unused.Outputs.Count);
            Assert.Empty(unused.Links);
        }

        [Fact]
        public void AMirrorTwinsHoldOutputCopiesItsTwinsBiasAndLinksMovedToItsOwnPorts()
        {
            Phenotype body = Develop(Gripper(), hold: true);
            List<BrainOutput> outputs = CentralWiring.Outputs(body);
            ulong hinge = BrainIds.Port(body.Parts[1].PathId, SensorChannel.JointAngle, 0);

            var brain = new CentralBrain();
            brain.Outputs.Add(new BrainUnit { Id = outputs[0].Id });
            brain.Outputs.Add(new BrainUnit { Id = outputs[1].Id });
            brain.Outputs.Add(new BrainUnit { Id = outputs[2].Id, Bias = 0.7f });   // the first copy's; the second is new
            ulong own = BrainIds.Port(body.Parts[2].PathId, SensorChannel.Gripping, 0);
            brain.Links.Add(new BrainLink { From = own, To = outputs[2].Id, Weight = 0.3f });
            brain.Links.Add(new BrainLink { From = hinge, To = outputs[2].Id, Weight = -0.6f });

            CentralBrain grown = CentralWiring.Effective(brain, body);
            BrainUnit twin = grown.Outputs.Single(u => u.Id == outputs[3].Id);
            Assert.Equal(0.7f, twin.Bias);

            ulong twinOwn = BrainIds.Port(body.Parts[3].PathId, SensorChannel.Gripping, 0);
            List<BrainLink> into = grown.Links.Where(l => l.To == outputs[3].Id).ToList();
            Assert.Equal(2, into.Count);
            Assert.Equal(0.3f, into.Single(l => l.From == twinOwn).Weight);
            Assert.Equal(-0.6f, into.Single(l => l.From == hinge).Weight);
            Assert.DoesNotContain(into, l => l.From == own);
        }

        [Fact]
        public void WithNoTwinTheHoldOutputGoesUpTheChainThenIsDrawnFreshOrLeftSilent()
        {
            // An anchoring part under an anchoring part (a chain): the child copies the parent's.
            var g = new Genome();
            g.Nodes.Add(Fixtures.Box());
            MorphNode outer = Fixtures.Box(0.2f);
            outer.CellTypeId = CellTypeIds.Anchoring;
            MorphNode inner = Fixtures.Box(0.2f);
            inner.CellTypeId = CellTypeIds.Anchoring;
            g.Nodes.Add(outer);
            g.Nodes.Add(inner);
            g.Nodes[0].Edges.Add(Edge(1, new Float3(1f, 0f, 0f), Bool3.None));
            g.Nodes[1].Edges.Add(Edge(2, new Float3(1f, 0f, 0f), Bool3.None));
            CentralWiring.Convert(g);

            Phenotype body = Develop(g, hold: true);
            List<BrainOutput> outputs = CentralWiring.Outputs(body);
            Assert.Equal(2, outputs.Count);
            Assert.All(outputs, o => Assert.Equal(BrainOutputKind.Grip, o.Kind));

            var brain = new CentralBrain();
            brain.Outputs.Add(new BrainUnit { Id = outputs[0].Id, Bias = -0.25f });
            Assert.Equal(-0.25f, CentralWiring.Effective(brain, body).Outputs.Single(u => u.Id == outputs[1].Id).Bias);

            // With nothing to copy and a stream, it is drawn fresh; with no stream it is silent.
            CentralBrain fresh = CentralWiring.Effective(
                new CentralBrain(), body, new Rng(3), Config(hold: true).SensorPool(), new CentralBrainOptions());
            Assert.Equal(2, fresh.Outputs.Count);
            Assert.All(fresh.Outputs, u => Assert.Equal(CentralWiring.FreshHoldBias, u.Bias));
            CentralBrain silent = CentralWiring.Effective(new CentralBrain(), body);
            Assert.All(silent.Outputs, u => Assert.Equal(0f, u.Bias));
            Assert.Empty(silent.Links);
        }

        [Fact]
        public void AFounderWiresTheHoldOutputFromEveryPoolPortWithABiasOfTwo()
        {
            Phenotype body = Develop(Gripper(), hold: true);
            SensorChannel[] pool = Config(hold: true).SensorPool();
            List<BrainPort> ports = CentralWiring.Ports(body, pool);
            List<BrainOutput> outputs = CentralWiring.Outputs(body);

            CentralBrain founder = CentralWiring.Effective(
                new CentralBrain(), body, new Rng(11), pool, new CentralBrainOptions(), founder: true);
            Assert.Equal(4, founder.Outputs.Count);
            Assert.Equal(2f, CentralWiring.FreshHoldBias);

            var weights = new List<float>();
            foreach (BrainOutput hold in outputs.Where(o => o.Kind == BrainOutputKind.Grip))
            {
                Assert.Equal(2f, founder.Outputs.Single(u => u.Id == hold.Id).Bias);
                List<BrainLink> into = founder.Links.Where(l => l.To == hold.Id).ToList();
                Assert.Equal(ports.Count, into.Count);
                Assert.Equal(ports.Select(p => p.Id).OrderBy(x => x), into.Select(l => l.From).OrderBy(x => x));
                weights.AddRange(into.Select(l => l.Weight));
            }

            // Spread 1 over the square root of the port count (D147), as every output's.
            double rms = Math.Sqrt(weights.Sum(w => (double)w * w) / weights.Count);
            double expected = 1.0 / Math.Sqrt(ports.Count);
            _out.WriteLine($"{ports.Count} ports, rms {rms:0.000} against {expected:0.000}");
            Assert.InRange(rms / expected, 0.6, 1.5);

            // Its joint outputs draw before it, so they are the draws they were with the switch off.
            CentralBrain jointsOnly = CentralWiring.Effective(
                new CentralBrain(), Develop(Gripper(), hold: false), new Rng(11),
                Config(hold: false).SensorPool(), new CentralBrainOptions(), founder: true);
            Assert.Equal(2, jointsOnly.Outputs.Count);
            Assert.Equal(
                jointsOnly.Outputs.Select(u => (u.Id, u.Bias)),
                founder.Outputs.Take(2).Select(u => (u.Id, u.Bias)));
        }

        [Fact]
        public void TheHoldOutputsAreTheUnitsAfterTheJointOutputsAndFeedTheHoldArray()
        {
            Genome g = Gripper();
            Phenotype body = Develop(g, hold: true);
            List<BrainOutput> outputs = CentralWiring.Outputs(body);

            g.Brain.Outputs.Add(new BrainUnit { Id = outputs[0].Id, Bias = 0.5f });
            g.Brain.Outputs.Add(new BrainUnit { Id = outputs[1].Id, Bias = -0.5f });
            g.Brain.Outputs.Add(new BrainUnit { Id = outputs[2].Id, Bias = 3f });
            g.Brain.Outputs.Add(new BrainUnit { Id = outputs[3].Id, Bias = 0f });
            ulong gripped = BrainIds.Port(body.Parts[3].PathId, SensorChannel.Gripping, 0);
            g.Brain.Links.Add(new BrainLink { From = gripped, To = outputs[3].Id, Weight = -2f });

            Brain brain = Brain.For(Develop(g, hold: true));
            Assert.Equal(1, brain.TotalDof);
            Assert.Equal(2, brain.GripCount);
            Assert.Equal(2, brain.GripPart(0));
            Assert.Equal(3, brain.GripPart(1));
            Assert.True(Brain.MaskReads(brain.SensorMask, SensorChannel.Gripping));
            Assert.False(Brain.MaskReads(brain.SensorMask, SensorChannel.GripPull));

            var field = new Field();
            field.Values[(3, SensorChannel.Gripping, 0)] = 1f;
            var drive = new float[1];
            var bend = new float[1];
            var hold = new float[2];
            brain.Step(0.5f, drive, field, bend, hold);

            Assert.Equal(Math.Tanh(0.5), drive[0], 5);
            Assert.Equal(Math.Tanh(-0.5), bend[0], 5);
            Assert.Equal(Math.Tanh(3.0), hold[0], 5);
            Assert.Equal(Math.Tanh(-2.0), hold[1], 5);

            // Without the array nothing is written, and the drive is as it was.
            var drive2 = new float[1];
            Brain.For(Develop(g, hold: true)).Step(0.5f, drive2, field);
            Assert.Equal(drive[0], drive2[0]);

            // A body whose table has the switch off has no hold outputs at all.
            Brain off = Brain.For(Develop(g, hold: false));
            Assert.Equal(0, off.GripCount);
            Assert.Equal(1, off.TotalDof);
        }

        [Fact]
        public void TheJointOutputsKeepTheirUnitsWhetherOrNotThePartGrips()
        {
            Genome g = Gripper();
            Phenotype on = Develop(g, hold: true);
            Phenotype off = Develop(g, hold: false);
            foreach (BrainOutput o in CentralWiring.Outputs(on).Where(o => o.Kind != BrainOutputKind.Grip))
            {
                Assert.Contains(CentralWiring.Outputs(off), p => p.Id == o.Id && p.DriveIndex == o.DriveIndex && p.Kind == o.Kind);
            }

            Assert.Equal(2, CentralWiring.Outputs(off).Count);
        }

        [Fact]
        public void ABrainHoldWorldNeedsTheCentralBrain()
        {
            RunConfig config = Config(hold: true);
            config.ContactPerPart = true;
            config.NeuralCostPerNeuronWatts = 0f;
            config.NeuralCostPerConnectionWatts = 0f;

            ArgumentException e = Assert.Throws<ArgumentException>(() => new World(config, seed: 1));
            _out.WriteLine(e.Message);
            Assert.Contains("BrainHold", e.Message);

            config.Brain.BrainModel = BrainModel.Central;
            Assert.NotNull(new World(config, seed: 1));

            // With the switch off the per-node world is as it was.
            RunConfig plain = Config(hold: false);
            plain.ContactPerPart = true;
            Assert.NotNull(new World(plain, seed: 1));
        }

        [Fact]
        public void AnInoculatedGripperIsWiredWithItsHoldOutputsAndStepsInAWorld()
        {
            RunConfig config = Config(hold: true);
            config.ContactPerPart = true;
            config.SenseBedTouch = true;
            config.NeuralCostPerNeuronWatts = 0f;
            config.NeuralCostPerConnectionWatts = 0f;
            config.Brain.BrainModel = BrainModel.Central;
            config.MinimumPopulation = 0;

            var world = new World(config, seed: 2);
            world.Inoculate(Gripper(), 1, -5f);
            Organism o = world.Living.Single();

            List<BrainOutput> outputs = CentralWiring.Outputs(o.Phenotype);
            Assert.Equal(4, outputs.Count);
            foreach (BrainOutput hold in outputs.Where(x => x.Kind == BrainOutputKind.Grip))
            {
                Assert.Equal(2f, o.Genome.Brain.Outputs.Single(u => u.Id == hold.Id).Bias);
                Assert.NotEmpty(o.Genome.Brain.Links.Where(l => l.To == hold.Id));
            }

            Assert.Equal(2, Brain.For(o.Phenotype).GripCount);
            for (int i = 0; i < 20; i++) world.Step(0.5f);
            Assert.NotEmpty(world.Living);
        }
    }
}
