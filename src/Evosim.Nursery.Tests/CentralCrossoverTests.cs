using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The crossover and the brain distance on D146's one brain: whatever two brains of one body
    /// are crossed, the child is a valid brain; a route that meets another output stops there; a
    /// crossover past the hidden cap is refused; and the distance is zero only between equals.
    /// </summary>
    public class CentralCrossoverTests
    {
        private static MorphNode Box(float half, JointType joint)
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(half, half, half),
                JointType = joint,
                RecursiveLimit = 1,
                CellTypeId = joint == JointType.Fixed ? CellTypeIds.Structural : CellTypeIds.Link,
                Power = joint == JointType.Fixed ? 0f : 50f,
            };
            node.ResampleJointLimits(new Float2(-1f, 1f));
            return node;
        }

        // A root with two hinges, on two edges.
        private static Genome Body()
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Box(0.5f, JointType.Fixed));
            g.Nodes.Add(Box(0.3f, JointType.Hinge));
            g.Nodes.Add(Box(0.3f, JointType.Hinge));
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f) });
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 2, ParentAnchor = new Float3(-1f, 0f, 0f), ChildAnchor = new Float3(1f, 0f, 0f) });
            CentralWiring.Convert(g);
            return g;
        }

        private static readonly SensorChannel[] Pool = { SensorChannel.JointAngle, SensorChannel.Depth, SensorChannel.Chemical };

        private static Genome Founder(ulong seed)
        {
            Genome g = Body();
            g.Brain = CentralWiring.Effective(g.Brain, Developer.Develop(g), new Rng(seed), Pool, new CentralBrainOptions(), founder: true);
            return g;
        }

        private static Genome Mutated(Genome g, Rng rng, int times, CentralBrainOptions o)
        {
            Genome c = g.Clone();
            Phenotype body = Developer.Develop(c);
            for (int i = 0; i < times; i++) CentralMutation.Mutate(c.Brain, body, Pool, rng, o);
            return c;
        }

        [Fact]
        public void EveryCrossOfTwoGrownBrainsIsAValidBrain()
        {
            var o = new CentralBrainOptions { AddUnitChance = 0.6f, AddLinkChance = 0.9f };
            var rng = new Rng(17);
            var brains = new List<Genome>();
            for (int k = 0; k < 8; k++) brains.Add(Mutated(Founder((ulong)k + 1), rng, 30, o));

            int transferred = 0;
            for (int i = 0; i < 400; i++)
            {
                Genome a = brains[rng.Range(brains.Count)], b = brains[rng.Range(brains.Count)];
                Genome child = Crossover.Cross(a, b, rng, 0, out CrossoverRecord r);
                Assert.Empty(child.Brain.Validate());
                Assert.Empty(child.Validate());
                if (r.Transferred) transferred++;
                Assert.Equal(a.Brain.Outputs.Count, child.Brain.Outputs.Count);
            }

            Assert.True(transferred > 100, $"only {transferred} of 400 crosses passed a route");
        }

        [Fact]
        public void ARouteThatMeetsAnotherOutputStopsThere()
        {
            Genome donor = Founder(3);
            Phenotype body = Developer.Develop(donor);
            List<BrainOutput> outs = CentralWiring.Outputs(body);
            // Every output's only input: the output before it, so every route meets an output first.
            donor.Brain.Links.Clear();
            for (int i = 0; i < outs.Count; i++)
                donor.Brain.Links.Add(new BrainLink { From = outs[(i + outs.Count - 1) % outs.Count].Id, To = outs[i].Id, Weight = 0.7f });

            Genome child = Crossover.Cross(Founder(4), donor, new Rng(5), 0, out CrossoverRecord r);
            Assert.True(r.Transferred);
            Assert.Equal(RouteEnd.Unresolved, r.End);
            Assert.Single(r.Route);
            Assert.Empty(child.Brain.Validate());
        }

        [Fact]
        public void ACrossPastTheHiddenCapIsRefused()
        {
            Genome donor = Founder(6);
            // A chain of hidden units in front of every output, so any route copies at least one.
            foreach (BrainUnit output in donor.Brain.Outputs.ToArray())
            {
                BrainLink strongest = donor.Brain.Links.Where(l => l.To == output.Id).OrderByDescending(l => System.Math.Abs(l.Weight)).First();
                CentralMutation.Split(donor.Brain, strongest);
            }

            // The base already holds one hidden unit, so at a cap of 1 any copied unit is one too many.
            Genome baseGenome = Founder(7);
            ulong own = CentralMutation.Split(baseGenome.Brain, baseGenome.Brain.Links[0]);

            Genome child = Crossover.Cross(baseGenome, donor, new Rng(8), 1, out CrossoverRecord r);
            Assert.False(r.Transferred);
            Assert.Equal("limit", r.Refused);
            Assert.Equal(own, Assert.Single(child.Brain.Hidden).Id);

            // And at a cap of 2 the same cross passes.
            Crossover.Cross(baseGenome, donor, new Rng(8), 2, out CrossoverRecord passed);
            Assert.True(passed.Transferred);
        }

        [Fact]
        public void TheDistanceIsZeroOnlyBetweenEquals()
        {
            Genome a = Founder(9);
            Assert.Equal(0f, Crossover.BrainDistance(a, a.Clone()));
            Genome b = a.Clone();
            b.Brain.Links[0].Weight += 0.25f;
            Assert.Equal(0.25f, Crossover.BrainDistance(a, b), 4);
            b.Brain.Links.RemoveAt(1);
            Assert.Equal(1.25f, Crossover.BrainDistance(a, b), 4);
        }
    }
}
