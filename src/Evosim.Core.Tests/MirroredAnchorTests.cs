using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D228, <see cref="DevelopmentLimits.ReflectMirroredAnchors"/>: a mirrored part's joint
    /// anchors are stored in its proper frame. Off by default and carried by the clone, the
    /// hash and the config file; a body that reflects nothing develops bit for bit the same on
    /// and off; a body that reflects keeps its geometry and changes only the anchors the
    /// derivation names.
    /// </summary>
    public sealed class MirroredAnchorTests
    {
        private static readonly DevelopmentLimits Off = new DevelopmentLimits { MaxParts = 64 };
        private static readonly DevelopmentLimits On = new DevelopmentLimits { MaxParts = 64, ReflectMirroredAnchors = true };

        [Fact]
        public void ItIsOffByDefaultAndTheCloneAndTheTextCarryIt()
        {
            Assert.False(DevelopmentLimits.Default.ReflectMirroredAnchors);
            Assert.DoesNotContain("anchors=", DevelopmentLimits.Default.ToString());

            DevelopmentLimits copy = On.Clone();
            Assert.True(copy.ReflectMirroredAnchors);
            Assert.Contains("anchors=reflected", On.ToString());
        }

        [Fact]
        public void ItMovesTheHashAndSurvivesASaveAndReload()
        {
            var off = new RunConfig();
            var on = new RunConfig();
            on.Development.ReflectMirroredAnchors = true;
            Assert.NotEqual(off.Hash(), on.Hash());

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(on), out string mismatch);
            Assert.True(mismatch == null, mismatch);
            Assert.True(back.Development.ReflectMirroredAnchors);
        }

        [Fact]
        public void ABodyWithNoReflectionDevelopsBitForBitTheSameOnAndOff()
        {
            int compared = 0;

            for (int seed = 1; seed <= 24; seed++)
            {
                Genome genome = GenomeFactory.Random(new Rng(7000UL + (ulong)seed));
                foreach (MorphNode node in genome.Nodes)
                {
                    foreach (MorphEdge edge in node.Edges) edge.Reflect = Bool3.None;
                }

                Phenotype off = Developer.Develop(genome, Off);
                Phenotype on = Developer.Develop(genome, On);

                Assert.Equal(off.PartCount, on.PartCount);
                Assert.DoesNotContain(off.Parts, p => p.Mirrored);

                for (int i = 0; i < off.PartCount; i++)
                {
                    AssertBitIdentical(off.Parts[i], on.Parts[i], $"seed {seed} part {i}");
                    compared++;
                }

                Assert.Equal(Bits(off.TotalLitArea), Bits(on.TotalLitArea));
                Assert.Equal(Bits(off.SilhouetteArea), Bits(on.SilhouetteArea));
                Assert.Equal(off.PrunedForVolume, on.PrunedForVolume);
                Assert.Equal(off.PrunedForParts, on.PrunedForParts);
            }

            Assert.True(compared > 60, $"only {compared} parts were compared");
        }

        [Fact]
        public void ABodyThatReflectsKeepsItsGeometryAndChangesOnlyTheAnchorsOfMirroredPartsAndTheirChildren()
        {
            // root -> a (reflected in x) -> b (not reflected). a's mirrored copy is a Mirrored part;
            // b under it hangs from a mirrored parent, so its parent anchor is flipped too.
            Genome genome = Chain3(new Bool3(true, false, false), Bool3.None);
            Phenotype off = Developer.Develop(genome, Off);
            Phenotype on = Developer.Develop(genome, On);

            Assert.Equal(5, off.PartCount);
            Assert.Equal(off.PartCount, on.PartCount);

            int mirroredParts = 0;

            for (int i = 0; i < off.PartCount; i++)
            {
                PhenotypePart a = off.Parts[i], b = on.Parts[i];

                // Everything development decides is the same; only the two anchors may move.
                Assert.Equal(Bits(a.Position.X), Bits(b.Position.X));
                Assert.Equal(Bits(a.Position.Y), Bits(b.Position.Y));
                Assert.Equal(Bits(a.Position.Z), Bits(b.Position.Z));
                Assert.Equal(a.Rotation, b.Rotation);
                Assert.Equal(a.Mirrored, b.Mirrored);
                Assert.Equal(a.HalfExtents, b.HalfExtents);

                if (a.IsRoot) continue;

                // The child's side flips on x for a mirrored part, and is otherwise as stored before.
                Float3 expectedChild = a.Mirrored
                    ? new Float3(-a.ChildAnchorLocal.X, a.ChildAnchorLocal.Y, a.ChildAnchorLocal.Z)
                    : a.ChildAnchorLocal;
                Assert.Equal(expectedChild, b.ChildAnchorLocal);

                // The parent's side is F_parent · M · anchor, whose x sign flips once for the copy's
                // own mirror and once for a mirrored parent. Here the parts are root, a, b, a' and b':
                // a' flips by its mirror (its parent, the root, is proper) and b' by its parent a'
                // (the edge a to b reflects nothing), so the parent anchor flips exactly where the
                // part is Mirrored.
                Float3 expectedParent = a.Mirrored
                    ? new Float3(-a.ParentAnchorLocal.X, a.ParentAnchorLocal.Y, a.ParentAnchorLocal.Z)
                    : a.ParentAnchorLocal;
                Assert.Equal(expectedParent, b.ParentAnchorLocal);

                if (a.Mirrored) mirroredParts++;
            }

            // The mirrored copy of a, and b under it (b's own transform is improper too).
            Assert.Equal(2, mirroredParts);
        }

        // root -> a -> b, a Box root and two Link nodes whose anchors sit off every axis, so a
        // reflection in any plane moves the joint.
        private static Genome Chain3(Bool3 firstReflect, Bool3 secondReflect)
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f };

            for (int i = 0; i < 3; i++)
            {
                genome.Nodes.Add(new MorphNode
                {
                    Dimensions = new Float3(0.2f, 0.1f, 0.15f),
                    CellTypeId = i == 0 ? CellTypeIds.Structural : CellTypeIds.Link,
                    ShapeId = ShapeIds.Box,
                    JointType = i == 0 ? JointType.Fixed : JointType.Hinge,
                    JointLimits = i == 0 ? Array.Empty<Float2>() : new[] { new Float2(-1f, 1f) },
                    Power = i == 0 ? 0f : 5f,
                    RecursiveLimit = 1,
                });
            }

            genome.Nodes[0].Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(0.8f, 0.5f, 0.4f),
                ChildAnchor = new Float3(-0.7f, 0.3f, -0.2f),
                Orientation = Quat.FromEuler(0.3f, 0.5f, -0.4f),
                Scale = Float3.One,
                Reflect = firstReflect,
            });

            genome.Nodes[1].Edges.Add(new MorphEdge
            {
                Child = 2,
                ParentAnchor = new Float3(-0.6f, 0.4f, 0.5f),
                ChildAnchor = new Float3(0.5f, -0.3f, 0.2f),
                Orientation = Quat.FromEuler(-0.2f, 0.1f, 0.6f),
                Scale = Float3.One,
                Reflect = secondReflect,
            });

            return genome;
        }

        private static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

        // Every public instance property of a part, floats by their bits, structs by their public fields,
        // arrays by their elements: a field added to PhenotypePart is compared without an edit here.
        private static void AssertBitIdentical(object a, object b, string where)
        {
            foreach (PropertyInfo property in typeof(PhenotypePart).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;
                Same(property.GetValue(a), property.GetValue(b), where + "." + property.Name);
            }
        }

        private static void Same(object a, object b, string where)
        {
            if (a == null || b == null)
            {
                Assert.True(a == null && b == null, where + ": one is null");
                return;
            }

            Type t = a.GetType();

            if (a is float fa)
            {
                Assert.True(Bits(fa) == Bits((float)b), where + $": {fa:R} against {(float)b:R}");
            }
            else if (t.IsPrimitive || t.IsEnum || a is string || a is decimal)
            {
                Assert.True(a.Equals(b), where + $": {a} against {b}");
            }
            else if (a is IEnumerable ea && !(a is string))
            {
                var xs = new List<object>();
                var ys = new List<object>();
                foreach (object x in ea) xs.Add(x);
                foreach (object y in (IEnumerable)b) ys.Add(y);
                Assert.True(xs.Count == ys.Count, where + ": lengths differ");
                for (int i = 0; i < xs.Count; i++) Same(xs[i], ys[i], where + "[" + i + "]");
            }
            else if (t.IsValueType)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Same(field.GetValue(a), field.GetValue(b), where + "." + field.Name);
                }
            }
            else
            {
                // A shared reference: the same node's neuron array on both bodies.
                Assert.True(ReferenceEquals(a, b) || a.Equals(b), where + ": different references");
            }
        }
    }
}
