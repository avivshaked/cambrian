using System;
using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D146's one brain (proposals/propose-brain-v2.md): ids that follow the limb, ports that come
    /// and go with the body, outputs that cannot be deleted, a plain network with loops, growth by
    /// mutation, the genome file's format 12, and a world that runs it.
    /// </summary>
    public class CentralBrainTests
    {
        private sealed class Field : ISensorField
        {
            public readonly Dictionary<(int, SensorChannel, int), float> Values = new Dictionary<(int, SensorChannel, int), float>();
            public float Read(int partIndex, SensorChannel channel, int index) =>
                Values.TryGetValue((partIndex, channel, index), out float v) ? v : 0f;
        }

        private static Genome Spine(int segments)
        {
            Genome g = Fixtures.SelfLoopSpine(segments);
            CentralWiring.Convert(g);
            return g;
        }

        private static Phenotype Develop(Genome g) => Developer.Develop(g);

        // ------------------------------------------------------------------ ids

        [Fact]
        public void EveryPartHasItsOwnPathIdAndKeepsItInEveryCopy()
        {
            Genome g = Spine(4);
            Phenotype body = Develop(g);
            Assert.Equal(4, body.PartCount);

            ulong[] ids = body.Parts.Select(p => p.PathId).ToArray();
            Assert.Equal(ids.Length, ids.Distinct().Count());
            Assert.Equal(BrainIds.Root, ids[0]);

            Phenotype again = Develop(g);
            Assert.Equal(ids, again.Parts.Select(p => p.PathId).ToArray());

            Phenotype small = body.Scaled(0.5f, PartShapeRegistry.Standard);
            Assert.Equal(ids, small.Parts.Select(p => p.PathId).ToArray());
            Assert.Same(body.Brain, small.Brain);

            Phenotype cut = body.WithoutSubtrees(new[] { false, false, true, false }, out _);
            Assert.Equal(ids.Take(2), cut.Parts.Select(p => p.PathId));
        }

        [Fact]
        public void AnEdgeIdKeepsAPartsIdWhenAnEarlierEdgeIsRemoved()
        {
            // Root with two jointed children on two edges. Removing the first edge shifts the
            // second edge's index; its numbered id keeps the child's path id.
            var g = new Genome();
            MorphNode root = Fixtures.Box();
            g.Nodes.Add(root);
            g.Nodes.Add(Fixtures.Box(0.3f, JointType.Hinge));
            root.Edges.Add(Fixtures.FaceToFace(1));
            MorphEdge second = Fixtures.FaceToFace(1);
            second.ParentAnchor = new Float3(-1f, 0f, 0f);
            second.ChildAnchor = new Float3(1f, 0f, 0f);
            root.Edges.Add(second);
            CentralWiring.Convert(g);

            ulong before = Develop(g).Parts[2].PathId;
            root.Edges.RemoveAt(0);
            Phenotype after = Develop(g);
            Assert.Equal(2, after.PartCount);
            Assert.Equal(before, after.Parts[1].PathId);
        }

        [Fact]
        public void NumberingGivesACopiedEdgeAFreshIdAndKeepsTheFirst()
        {
            Genome g = Spine(2);
            MorphEdge first = g.Nodes[0].Edges[0];
            g.Nodes[0].Edges.Add(first.Clone());
            Assert.Equal(first.Id, g.Nodes[0].Edges[1].Id);

            g.NumberEdges();
            Assert.NotEqual(first.Id, g.Nodes[0].Edges[1].Id);
            Assert.Equal(1UL, first.Id);
            Assert.True(g.NextEdgeId > g.Nodes[0].Edges[1].Id);
        }

        // ------------------------------------------------------------------ ports and outputs

        [Fact]
        public void PortsFollowTheBodyAndTheJointChannelsHaveOnePerDegreeOfFreedom()
        {
            Genome g = Spine(3);
            Phenotype body = Develop(g);
            var pool = new[] { SensorChannel.JointAngle, SensorChannel.Depth, SensorChannel.Flow, SensorChannel.Energy };
            List<BrainPort> ports = CentralWiring.Ports(body, pool);

            // Root: rigid (no angle), depth 1, flow 3, energy 1. Two hinges: angle 1, depth 1, flow 3.
            Assert.Equal(5 + 5 + 5, ports.Count);
            Assert.Single(ports, p => p.Channel == SensorChannel.Energy);
            Assert.Equal(ports.Count, ports.Select(p => p.Id).Distinct().Count());

            List<BrainOutput> outputs = CentralWiring.Outputs(body);
            Assert.Equal(4, outputs.Count);
            Assert.Equal(new[] { 0, 0, 1, 1 }, outputs.Select(o => o.DriveIndex));
            Assert.Equal(new[] { BrainOutputKind.Power, BrainOutputKind.Bend, BrainOutputKind.Power, BrainOutputKind.Bend }, outputs.Select(o => o.Kind));
        }

        [Fact]
        public void AFounderIsWiredFromEveryPortToEveryOutput()
        {
            Genome g = Spine(3);
            Phenotype body = Develop(g);
            var pool = new[] { SensorChannel.JointAngle, SensorChannel.Chemical };
            var options = new CentralBrainOptions();
            CentralBrain brain = CentralWiring.Effective(g.Brain, body, new Rng(7), pool, options, founder: true);

            int ports = CentralWiring.Ports(body, pool).Count;
            Assert.Equal(4, brain.Outputs.Count);
            Assert.Equal(4 * ports, brain.Links.Count);
            Assert.Empty(brain.Hidden);
            Assert.All(brain.Outputs, u => Assert.Equal(0f, u.Bias));   // D147: biases start at 0
            Assert.Contains(brain.Links, l => l.Weight != 0f);

            // Seeded, so the same founder is the same brain.
            CentralBrain twin = CentralWiring.Effective(g.Brain, body, new Rng(7), pool, options, founder: true);
            Assert.Equal(brain.Links.Select(l => l.Weight), twin.Links.Select(l => l.Weight));
        }

        // ------------------------------------------------------------------ the network

        private static (Genome g, Phenotype body) OneHinge()
        {
            Genome g = Spine(2);
            Phenotype body = Develop(g);
            foreach (BrainOutput o in CentralWiring.Outputs(body)) g.Brain.Outputs.Add(new BrainUnit { Id = o.Id });
            return (g, body);
        }

        [Fact]
        public void APortIsReadThisStepAndTheOutputsAreTanhOfTheirSums()
        {
            (Genome g, Phenotype body) = OneHinge();
            List<BrainOutput> outs = CentralWiring.Outputs(body);
            ulong depth = BrainIds.Port(body.Parts[1].PathId, SensorChannel.Depth, 0);
            g.Brain.Outputs[0].Bias = 0.2f;
            g.Brain.Links.Add(new BrainLink { From = depth, To = outs[0].Id, Weight = 1.5f });
            g.Brain.Outputs[1].Bias = -0.4f;

            Brain brain = Brain.For(Develop(g));
            Assert.True(brain.IsCentral);
            Assert.Equal(1, brain.TotalDof);
            Assert.Equal(1 << (int)SensorChannel.Depth, brain.SensorMask);

            var field = new Field();
            field.Values[(1, SensorChannel.Depth, 0)] = 0.5f;
            var drive = new float[1];
            var bend = new float[1];
            brain.Step(0.02f, drive, field, bend);

            Fixtures.AssertClose((float)Math.Tanh(0.2 + 1.5 * 0.5), drive[0]);
            Fixtures.AssertClose((float)Math.Tanh(-0.4), bend[0]);
        }

        [Fact]
        public void AUnitReadsTheStepBeforeSoALoopIsAMemory()
        {
            (Genome g, Phenotype body) = OneHinge();
            ulong power = CentralWiring.Outputs(body)[0].Id;
            ulong depth = BrainIds.Port(body.Parts[0].PathId, SensorChannel.Depth, 0);

            // A hidden unit between the sense and the power: two steps from sense to drive.
            ulong h = BrainIds.Hidden(depth, power, 0);
            g.Brain.Hidden.Add(new BrainUnit { Id = h });
            g.Brain.Links.Add(new BrainLink { From = depth, To = h, Weight = 2f });
            g.Brain.Links.Add(new BrainLink { From = h, To = power, Weight = 1f });
            g.Brain.Links.Add(new BrainLink { From = power, To = power, Weight = 0.5f });   // a loop on itself

            Brain brain = Brain.For(Develop(g));
            var field = new Field();
            field.Values[(0, SensorChannel.Depth, 0)] = 1f;
            var drive = new float[1];

            brain.Step(0.02f, drive, field);
            Assert.Equal(0f, drive[0]);   // the hidden unit has not spoken yet

            brain.Step(0.02f, drive, field);
            float first = (float)Math.Tanh(Math.Tanh(2.0));
            Fixtures.AssertClose(first, drive[0]);

            brain.Step(0.02f, drive, field);
            Fixtures.AssertClose((float)Math.Tanh(Math.Tanh(2.0) + 0.5 * first), drive[0]);
        }

        [Fact]
        public void TheStateCarriesByUnitIdAndRoundTrips()
        {
            (Genome g, Phenotype body) = OneHinge();
            ulong power = CentralWiring.Outputs(body)[0].Id;
            g.Brain.Outputs[0].Bias = 1f;
            g.Brain.Links.Add(new BrainLink { From = power, To = power, Weight = 0.3f });
            Brain a = Brain.For(Develop(g));
            var drive = new float[1];
            for (int i = 0; i < 5; i++) a.Step(0.02f, drive);

            Brain b = Brain.For(Develop(g));
            b.CarryStateFrom(a, new[] { 0, 1 });
            var d2 = new float[1];
            a.Step(0.02f, drive);
            b.Step(0.02f, d2);
            Assert.Equal(drive[0], d2[0]);

            var stream = new System.IO.MemoryStream();
            using (var w = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) a.WriteState(w);
            stream.Position = 0;
            Brain c = Brain.For(Develop(g));
            using (var r = new System.IO.BinaryReader(stream)) c.ReadState(r);
            a.Step(0.02f, drive);
            c.Step(0.02f, d2);
            Assert.Equal(drive[0], d2[0]);
        }

        // ------------------------------------------------------------------ limbs come and go

        [Fact]
        public void ANewSegmentCopiesItsParentJointWithItsOwnSenses()
        {
            Genome g = Spine(2);
            Phenotype body = Develop(g);
            var pool = new[] { SensorChannel.JointAngle };
            g.Brain = CentralWiring.Effective(g.Brain, body, new Rng(3), pool, new CentralBrainOptions(), founder: true);

            BrainOutput parentPower = CentralWiring.Outputs(body)[0];
            float bias = g.Brain.Outputs.Single(u => u.Id == parentPower.Id).Bias;
            ulong ownAngle = BrainIds.Port(body.Parts[1].PathId, SensorChannel.JointAngle, 0);
            float weight = g.Brain.Links.Single(l => l.From == ownAngle && l.To == parentPower.Id).Weight;

            g.Nodes[0].RecursiveLimit = 3;
            Phenotype longer = Develop(g);
            CentralBrain grown = CentralWiring.Effective(g.Brain, longer);

            BrainOutput newPower = CentralWiring.Outputs(longer)[2];
            Assert.Equal(2, longer.Parts[2].Index);
            Assert.Equal(bias, grown.Outputs.Single(u => u.Id == newPower.Id).Bias);

            // The parent's link from its own angle becomes the new joint's from its own angle.
            ulong newAngle = BrainIds.Port(longer.Parts[2].PathId, SensorChannel.JointAngle, 0);
            Assert.Equal(weight, grown.Links.Single(l => l.From == newAngle && l.To == newPower.Id).Weight);

            // The original joint is untouched.
            Assert.Equal(weight, grown.Links.Single(l => l.From == ownAngle && l.To == parentPower.Id).Weight);
        }

        [Fact]
        public void ALostLimbTakesItsPortsAndOutputsWithIt()
        {
            Genome g = Spine(3);
            Phenotype body = Develop(g);
            var pool = new[] { SensorChannel.JointAngle, SensorChannel.Depth };
            g.Brain = CentralWiring.Effective(g.Brain, body, new Rng(5), pool, new CentralBrainOptions(), founder: true);
            ulong tip = body.Parts[2].PathId;

            g.Nodes[0].RecursiveLimit = 2;
            Phenotype shorter = Develop(g);
            CentralBrain kept = CentralWiring.Effective(g.Brain, shorter);

            Assert.Equal(2, kept.Outputs.Count);
            Assert.DoesNotContain(kept.Links, l =>
                l.From == BrainIds.Port(tip, SensorChannel.Depth, 0) || l.From == BrainIds.Port(tip, SensorChannel.JointAngle, 0));
            Assert.DoesNotContain(kept.Outputs, u => u.Id == BrainIds.Output(tip, 0, BrainOutputKind.Power));
        }

        // ------------------------------------------------------------------ growth

        [Fact]
        public void ASplitKeepsThePathAndNamesTheUnitByTheLinkItSplit()
        {
            (Genome g, Phenotype body) = OneHinge();
            ulong power = CentralWiring.Outputs(body)[0].Id;
            ulong depth = BrainIds.Port(body.Parts[0].PathId, SensorChannel.Depth, 0);
            var link = new BrainLink { From = depth, To = power, Weight = -0.7f };
            g.Brain.Links.Add(link);

            ulong h = CentralMutation.Split(g.Brain, link);
            Assert.Equal(BrainIds.Hidden(depth, power, 0), h);
            Assert.Contains(g.Brain.Links, l => l.From == depth && l.To == h && l.Weight == 1f);
            Assert.Contains(g.Brain.Links, l => l.From == h && l.To == power && l.Weight == -0.7f);
            Assert.DoesNotContain(g.Brain.Links, l => l.From == depth && l.To == power);

            // The same split again (the link put back) is the next k, not a collision.
            var again = new BrainLink { From = depth, To = power, Weight = 0.1f };
            g.Brain.Links.Add(again);
            Assert.Equal(BrainIds.Hidden(depth, power, 1), CentralMutation.Split(g.Brain, again));
            Assert.Empty(g.Brain.Validate());
        }

        [Fact]
        public void MutationGrowsTheBrainAndNeverRemovesAnOutput()
        {
            Genome g = Spine(3);
            Phenotype body = Develop(g);
            SensorChannel[] pool = SensorChannels.DefaultPool;
            var options = new CentralBrainOptions { AddUnitChance = 0.5f, AddLinkChance = 0.8f, RemoveLinkChance = 0.3f, RemoveUnitChance = 0.2f };
            g.Brain = CentralWiring.Effective(g.Brain, body, new Rng(1), pool, options, founder: true);
            int outputs = g.Brain.Outputs.Count;

            var rng = new Rng(11);
            for (int i = 0; i < 400; i++) CentralMutation.Mutate(g.Brain, body, pool, rng, options);

            Assert.Equal(outputs, g.Brain.Outputs.Count);
            Assert.NotEmpty(g.Brain.Hidden);
            Assert.True(g.Brain.Hidden.Count <= options.MaxHiddenUnits);
            Assert.Empty(g.Brain.Validate());
            Assert.Empty(g.Validate());

            // And it still runs.
            Brain brain = Brain.For(Develop(g));
            var drive = new float[brain.TotalDof];
            var bend = new float[brain.TotalDof];
            for (int i = 0; i < 10; i++) brain.Step(0.02f, drive, null, bend);
            Assert.All(drive, v => Assert.True(v >= -1f && v <= 1f));
        }

        // ------------------------------------------------------------------ the file

        [Fact]
        public void ACentralGenomeIsFormat12AndRoundTripsByteForByte()
        {
            Genome g = Spine(3);
            g.Brain = CentralWiring.Effective(g.Brain, Develop(g), new Rng(9), SensorChannels.DefaultPool, new CentralBrainOptions(), founder: true);
            CentralMutation.Split(g.Brain, g.Brain.Links[0]);

            string text = GenomeJson.Write(g);
            Assert.Contains("\"format\":12", text);
            Genome back = GenomeJson.Read(text);
            Assert.Equal(text, GenomeJson.Write(back));
            Assert.Equal(g.NextEdgeId, back.NextEdgeId);
            Assert.Equal(g.Nodes[0].Edges[0].Id, back.Nodes[0].Edges[0].Id);
            Assert.Equal(g.Brain.Links.Count, back.Brain.Links.Count);

            // The old model's genome is still format 11, with no new key.
            string old = GenomeJson.Write(Fixtures.SelfLoopSpine(3));
            Assert.Contains("\"format\":11", old);
            Assert.DoesNotContain("brain\"", old);
            Assert.DoesNotContain("nextEdgeId", old);
        }

        [Fact]
        public void AGenomeCarriesOneBrainOrTheOther()
        {
            Genome g = Spine(2);
            g.Nodes[0].Neurons = new[] { new NeuronDef { Op = NeuronOp.Sum, Inputs = Array.Empty<NeuronInput>() } };
            Assert.Contains(g.Validate(), m => m.Contains("central brain"));
        }

        // ------------------------------------------------------------------ the review's cases

        private static RunConfig CentralConfig()
        {
            var config = new RunConfig { NeuralCostPerNeuronWatts = 0f, NeuralCostPerConnectionWatts = 0f };
            config.Brain.BrainModel = BrainModel.Central;
            return config;
        }

        private static MorphEdge Edge(int child, float side) => new MorphEdge
        {
            Child = child,
            ParentAnchor = new Float3(side, 0f, 0f),
            ChildAnchor = new Float3(-side, 0f, 0f),
            Orientation = Quat.Identity,
            Scale = Float3.One,
        };

        [Fact]
        public void ARigidPartThatGainsAJointIsWiredWithoutATwiceLinkedSense()
        {
            // The review's blocker: a dense founder's joint X reads every part's senses, rigid Y's
            // included. When Y gains a joint and copies X, X's own Depth link moves to Y's Depth,
            // and X's link from Y's Depth must not be copied beside it.
            var g = new Genome();
            g.Nodes.Add(Fixtures.Box());
            g.Nodes.Add(Fixtures.Box(0.3f, JointType.Hinge));
            g.Nodes.Add(Fixtures.Box(0.3f));
            g.Nodes[0].Edges.Add(Edge(1, 1f));
            g.Nodes[0].Edges.Add(Edge(2, -1f));
            CentralWiring.Convert(g);

            var pool = new[] { SensorChannel.Depth };
            Phenotype before = Develop(g);
            g.Brain = CentralWiring.Effective(g.Brain, before, new Rng(2), pool, new CentralBrainOptions(), founder: true);
            Assert.Empty(g.Brain.Validate());

            ulong xPower = BrainIds.Output(before.Parts[1].PathId, 0, BrainOutputKind.Power);
            float xOwnDepth = g.Brain.Links.Single(l => l.To == xPower && l.From == BrainIds.Port(before.Parts[1].PathId, SensorChannel.Depth, 0)).Weight;

            MorphNode y = g.Nodes[2];
            y.JointType = JointType.Hinge;
            y.CellTypeId = CellTypeIds.Link;
            y.Power = 50f;
            y.ResampleJointLimits(new Float2(-1f, 1f));
            Phenotype after = Develop(g);
            CentralBrain grown = CentralWiring.Effective(g.Brain, after);

            Assert.Empty(grown.Validate());
            ulong yPower = BrainIds.Output(after.Parts[2].PathId, 0, BrainOutputKind.Power);
            ulong yDepth = BrainIds.Port(after.Parts[2].PathId, SensorChannel.Depth, 0);
            BrainLink own = grown.Links.Single(l => l.To == yPower && l.From == yDepth);
            Assert.Equal(xOwnDepth, own.Weight);   // the moved link wins
        }

        [Fact]
        public void EachJointsUnitsDriveItsOwnDegreeOfFreedom()
        {
            Genome g = Spine(3);
            Phenotype body = Develop(g);
            float[] biases = { 0.1f, -0.2f, 0.3f, -0.4f };
            List<BrainOutput> outs = CentralWiring.Outputs(body);
            for (int i = 0; i < outs.Count; i++) g.Brain.Outputs.Add(new BrainUnit { Id = outs[i].Id, Bias = biases[i] });

            Brain brain = Brain.For(Develop(g));
            var drive = new float[2];
            var bend = new float[2];
            brain.Step(0.02f, drive, null, bend);
            Fixtures.AssertClose((float)Math.Tanh(0.1), drive[0]);
            Fixtures.AssertClose((float)Math.Tanh(-0.2), bend[0]);
            Fixtures.AssertClose((float)Math.Tanh(0.3), drive[1]);
            Fixtures.AssertClose((float)Math.Tanh(-0.4), bend[1]);
        }

        [Fact]
        public void StateCarriesToTheSameUnitWhenAnEarlierLimbIsGone()
        {
            // Two jointed limbs on two edges; the first edge removed moves the second limb's units
            // down the buffer. A carry by position would give it the first limb's state.
            var g = new Genome();
            g.Nodes.Add(Fixtures.Box());
            g.Nodes.Add(Fixtures.Box(0.3f, JointType.Hinge));
            g.Nodes.Add(Fixtures.Box(0.3f, JointType.Hinge));
            g.Nodes[0].Edges.Add(Edge(1, 1f));
            g.Nodes[0].Edges.Add(Edge(2, -1f));
            CentralWiring.Convert(g);
            Phenotype both = Develop(g);
            float[] biases = { 0.9f, -0.5f, -0.7f, 0.2f };
            List<BrainOutput> outs = CentralWiring.Outputs(both);
            for (int i = 0; i < outs.Count; i++)
            {
                g.Brain.Outputs.Add(new BrainUnit { Id = outs[i].Id, Bias = biases[i] });
                g.Brain.Links.Add(new BrainLink { From = outs[i].Id, To = outs[i].Id, Weight = 0.8f });
            }

            Brain a = Brain.For(Develop(g));
            var da = new float[2];
            for (int i = 0; i < 6; i++) a.Step(0.02f, da);

            Genome lost = g.Clone();
            lost.Nodes[0].Edges.RemoveAt(0);
            Brain b = Brain.For(Develop(lost));
            Assert.Equal(1, b.TotalDof);
            b.CarryStateFrom(a, new[] { 0, 2 });

            var db = new float[1];
            a.Step(0.02f, da);
            b.Step(0.02f, db);
            Assert.Equal(da[1], db[0]);
            Assert.NotEqual(da[0], db[0]);
        }

        [Fact]
        public void ANewMirrorCopyTakesItsTwinsWiringBeforeItsParents()
        {
            // Root, a hinge P, and on P a mirrored pair of hinges. The brain knows P and the first
            // copy; the second copy takes the first copy's bias, not P's.
            var g = new Genome();
            g.Nodes.Add(Fixtures.Box());
            g.Nodes.Add(Fixtures.Box(0.3f, JointType.Hinge));
            g.Nodes.Add(Fixtures.Box(0.2f, JointType.Hinge));
            g.Nodes[0].Edges.Add(Edge(1, 1f));
            var pair = new MorphEdge
            {
                Child = 2, ParentAnchor = new Float3(0f, 1f, 0f), ChildAnchor = new Float3(0f, -1f, 0f),
                Orientation = Quat.Identity, Scale = Float3.One, Reflect = new Bool3(false, true, false),
            };
            g.Nodes[1].Edges.Add(pair);
            CentralWiring.Convert(g);
            Phenotype body = Develop(g);
            Assert.Equal(4, body.PartCount);

            // P power, P bend, copy 1 power, copy 1 bend, copy 2 power, copy 2 bend.
            List<BrainOutput> outs = CentralWiring.Outputs(body);
            g.Brain.Outputs.Add(new BrainUnit { Id = outs[0].Id, Bias = 0.11f });
            g.Brain.Outputs.Add(new BrainUnit { Id = outs[1].Id, Bias = 0.12f });
            g.Brain.Outputs.Add(new BrainUnit { Id = outs[2].Id, Bias = 0.55f });
            g.Brain.Outputs.Add(new BrainUnit { Id = outs[3].Id, Bias = 0.56f });

            CentralBrain grown = CentralWiring.Effective(g.Brain, body);
            Assert.Equal(0.55f, grown.Outputs.Single(u => u.Id == outs[4].Id).Bias);
            Assert.Equal(0.56f, grown.Outputs.Single(u => u.Id == outs[5].Id).Bias);
        }

        [Fact]
        public void ACentralWorldRefusesANeuralPriceItCouldNotCharge()
        {
            var config = new RunConfig();
            config.Brain.BrainModel = BrainModel.Central;
            Assert.True(config.NeuralCostPerNeuronWatts > 0f);
            Assert.Throws<ArgumentException>(() => new World(config, seed: 1));
            Assert.NotNull(new World(CentralConfig(), seed: 1));
        }

        [Fact]
        public void AnInoculantWithAnEmptyBrainIsWiredToItsBody()
        {
            var world = new World(CentralConfig(), seed: 3);
            Genome g = Spine(3);
            world.Inoculate(g, 1, -5f);
            Organism o = world.Living.Single();
            Assert.Equal(4, o.Genome.Brain.Outputs.Count);
            Assert.NotEmpty(o.Genome.Brain.Links);
            Assert.Empty(g.Brain.Outputs);   // the caller's genome is not touched
        }

        // ------------------------------------------------------------------ the world

        [Fact]
        public void AWorldOfTheOneBrainFoundsAndBreedsCreaturesThatCarryIt()
        {
            var config = CentralConfig();
            var world = new World(config, seed: 4);
            for (int i = 0; i < 600 && world.Births < 5; i++) world.Step(1f);

            Assert.NotEmpty(world.Living);
            Assert.True(world.Births > 0, "no birth in 600 s");
            foreach (Organism o in world.Living)
            {
                Assert.NotNull(o.Genome.Brain);
                Assert.Empty(o.Genome.Validate(config.CellTypes));
                Assert.All(o.Genome.Nodes, n => Assert.Empty(n.Neurons));
                Assert.Equal(2 * Brain.For(o.Phenotype).TotalDof, o.Genome.Brain.Outputs.Count);
            }

            // And the default world is untouched: no organism carries the one brain.
            var plain = new World(new RunConfig(), seed: 4);
            for (int i = 0; i < 50; i++) plain.Step(1f);
            Assert.All(plain.Living, o => Assert.Null(o.Genome.Brain));
        }
    }
}
