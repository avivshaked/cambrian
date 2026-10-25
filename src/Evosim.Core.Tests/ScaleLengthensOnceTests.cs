using System;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D199's reading three of D196, <see cref="DevelopmentLimits.ScaleLengthensOnce"/>: on a
    /// path from the root, an edge lengthens an axis the first time the path follows it and never
    /// again, and shortens an axis every time, down to the thickness floor.
    /// </summary>
    public class ScaleLengthensOnceTests
    {
        private static readonly DevelopmentLimits On = new DevelopmentLimits { ScaleLengthensOnce = true };

        private static Genome Spine(Float3 scale, int limit)
        {
            Genome g = Fixtures.SelfLoopSpine(limit);
            g.Nodes[0].Dimensions = new Float3(0.25f, 0.25f, 0.25f);
            g.Nodes[0].Edges[0].Scale = scale;
            return g;
        }

        private static Float3 Ratio(Phenotype p, int part) => new Float3(
            p.Parts[part].HalfExtents.X / p.Parts[0].HalfExtents.X,
            p.Parts[part].HalfExtents.Y / p.Parts[0].HalfExtents.Y,
            p.Parts[part].HalfExtents.Z / p.Parts[0].HalfExtents.Z);

        private static void Close(float expected, float actual) =>
            Assert.True(Math.Abs(expected - actual) <= 1e-5f * Math.Max(1f, Math.Abs(expected)),
                $"expected {expected}, got {actual}");

        [Fact]
        public void ASelfEdgeLengthensOnceAndKeepsShortening()
        {
            var scale = new Float3(1.5f, 0.8f, 1f);
            Phenotype off = Developer.Develop(Spine(scale, 4));
            Phenotype on = Developer.Develop(Spine(scale, 4), On);

            Assert.Equal(4, off.PartCount);
            Assert.Equal(4, on.PartCount);

            for (int k = 0; k < 4; k++)
            {
                // Off, the recorded rule: every copy multiplies again on every axis.
                Close((float)Math.Pow(1.5, k), Ratio(off, k).X);
                Close((float)Math.Pow(0.8, k), Ratio(off, k).Y);

                // On: the length is taken once and kept; the width keeps tapering.
                Close(k == 0 ? 1f : 1.5f, Ratio(on, k).X);
                Close((float)Math.Pow(0.8, k), Ratio(on, k).Y);
                Close(1f, Ratio(on, k).Z);
            }
        }

        [Fact]
        public void AShorteningAxisReachesTheThicknessFloorAsItDidBefore()
        {
            // 0.25 m tapering by 0.3 a copy passes the 0.02 m floor at the fourth part.
            var scale = new Float3(1.2f, 0.3f, 1f);
            Phenotype off = Developer.Develop(Spine(scale, 4));
            Phenotype on = Developer.Develop(Spine(scale, 4), On);

            Assert.Equal(off.PartCount, on.PartCount);
            for (int k = 0; k < on.PartCount; k++)
            {
                Assert.Equal(off.Parts[k].HalfExtents.Y, on.Parts[k].HalfExtents.Y);
            }

            Assert.Equal(DevelopmentLimits.Default.MinPartHalfExtent, on.Parts[3].HalfExtents.Y);
            Assert.True(on.Parts[3].HalfExtents.X < off.Parts[3].HalfExtents.X);
        }

        [Fact]
        public void ACycleThroughTwoNodesAppliesEachEdgeOncePerPath()
        {
            // A -> B -> A -> B -> A: A's limit 3, B's 2, five parts down one path.
            var g = new Genome();
            MorphNode a = Fixtures.Box(0.25f, JointType.Fixed, 3);
            MorphNode b = Fixtures.Box(0.25f, JointType.Fixed, 2);
            MorphEdge ab = Fixtures.FaceToFace(1);
            ab.Scale = new Float3(2f, 0.5f, 1f);
            MorphEdge ba = Fixtures.FaceToFace(0);
            ba.Scale = new Float3(1.5f, 1f, 0.9f);
            a.Edges.Add(ab);
            b.Edges.Add(ba);
            g.Nodes.Add(a);
            g.Nodes.Add(b);
            g.RootIndex = 0;

            Phenotype off = Developer.Develop(g);
            Phenotype on = Developer.Develop(g, On);
            Assert.Equal(5, off.PartCount);
            Assert.Equal(5, on.PartCount);

            var expectedOff = new[]
            {
                new Float3(1f, 1f, 1f), new Float3(2f, 0.5f, 1f), new Float3(3f, 0.5f, 0.9f),
                new Float3(6f, 0.25f, 0.9f), new Float3(9f, 0.25f, 0.81f),
            };
            var expectedOn = new[]
            {
                new Float3(1f, 1f, 1f), new Float3(2f, 0.5f, 1f), new Float3(3f, 0.5f, 0.9f),
                new Float3(3f, 0.25f, 0.9f), new Float3(3f, 0.25f, 0.81f),
            };

            for (int k = 0; k < 5; k++)
            {
                Close(expectedOff[k].X, Ratio(off, k).X);
                Close(expectedOff[k].Y, Ratio(off, k).Y);
                Close(expectedOff[k].Z, Ratio(off, k).Z);
                Close(expectedOn[k].X, Ratio(on, k).X);
                Close(expectedOn[k].Y, Ratio(on, k).Y);
                Close(expectedOn[k].Z, Ratio(on, k).Z);
            }
        }

        [Fact]
        public void AGenomeWhoseEdgesLengthenNothingDevelopsIdenticallyEitherWay()
        {
            // The rule touches only an axis an edge lengthens, so a genome with every scale at or
            // under 1 is the same body to the bit, part for part.
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Genome g = GenomeFactory.Random(new Rng(seed));
                foreach (MorphNode node in g.Nodes)
                {
                    foreach (MorphEdge edge in node.Edges)
                    {
                        edge.Scale = new Float3(
                            Math.Min(edge.Scale.X, 1f), Math.Min(edge.Scale.Y, 1f), Math.Min(edge.Scale.Z, 1f));
                    }
                }

                Phenotype off = Developer.Develop(g);
                Phenotype on = Developer.Develop(g, On);

                Assert.Equal(off.PartCount, on.PartCount);
                for (int i = 0; i < off.PartCount; i++)
                {
                    Assert.Equal(off.Parts[i].HalfExtents, on.Parts[i].HalfExtents);
                    Assert.Equal(off.Parts[i].Position, on.Parts[i].Position);
                    Assert.Equal(off.Parts[i].SourceNode, on.Parts[i].SourceNode);
                }
            }
        }

        [Fact]
        public void TheSwitchIsCarriedByACloneAndNamedInTheLimitsText()
        {
            DevelopmentLimits copy = On.Clone();
            Assert.True(copy.ScaleLengthensOnce);
            Assert.Contains("scale=lengthens-once", On.ToString());
            Assert.DoesNotContain("scale=", DevelopmentLimits.Default.ToString());
        }
    }
}
