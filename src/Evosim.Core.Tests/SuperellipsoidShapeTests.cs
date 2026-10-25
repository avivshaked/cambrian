using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D233's second piece: a part's form carried from the node to the developed part and into every
    /// shape call. The named shapes must be untouched to the bit (the form overloads with
    /// <see cref="PartForm.None"/> are the old methods), the family shape must be the geometry it
    /// wraps, and growth and exposure must read one measurement of the surface, not a new one.
    /// </summary>
    public class SuperellipsoidShapeTests
    {
        private readonly ITestOutputHelper _output;

        public SuperellipsoidShapeTests(ITestOutputHelper output) => _output = output;

        private static readonly PartShapeRegistry FamilyRegistry = new PartShapeRegistry(
            new BoxShape(), new SphereShape(), new CapsuleShape(), new SuperellipsoidShape());

        private static readonly PartForm Egg = new PartForm(0.6f, 1f, 1.5f);

        private static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

        private static void SameBits(Float3 a, Float3 b, string where)
        {
            Assert.True(Bits(a.X) == Bits(b.X) && Bits(a.Y) == Bits(b.Y) && Bits(a.Z) == Bits(b.Z),
                where + $": {a} against {b}");
        }

        private static void SamePanels(IReadOnlyList<DragPanel> a, IReadOnlyList<DragPanel> b, string where)
        {
            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                SameBits(a[i].Centre, b[i].Centre, where + " centre " + i);
                SameBits(a[i].Normal, b[i].Normal, where + " normal " + i);
                Assert.True(Bits(a[i].Area) == Bits(b[i].Area), where + " area " + i);
            }
        }

        public static IEnumerable<object[]> NamedShapes()
        {
            yield return new object[] { ShapeIds.Box };
            yield return new object[] { ShapeIds.Sphere };
            yield return new object[] { ShapeIds.Capsule };
        }

        // ---------------------------------------------------------------- the form

        [Fact]
        public void NoneIsThreeZerosAndNothingElseIsNone()
        {
            Assert.True(PartForm.None.IsNone);
            Assert.True(default(PartForm).IsNone);
            Assert.False(PartForm.None.InRange);
            Assert.False(new PartForm(0f, 0f, 1f).IsNone);
            Assert.False(new PartForm(0f, 1f, 0f).IsNone);
            Assert.False(Egg.IsNone);
            Assert.True(Egg.InRange);
        }

        // ---------------------------------------------------------------- the named shapes, untouched

        [Theory]
        [MemberData(nameof(NamedShapes))]
        public void ANamedShapeGivesTheSameBitsWithNoneAsWithoutAForm(string id)
        {
            PartShape shape = PartShapeRegistry.Standard.Resolve(id);
            Assert.False(shape.TakesForm);

            var rng = new Rng(2026);
            for (int trial = 0; trial < 200; trial++)
            {
                Float3 h = rng.NextFloat3(0.05f, 1f);
                Float3 anchor = rng.NextFloat3(-1.2f, 1.2f);
                Float3 point = rng.NextFloat3(-1.2f, 1.2f);
                int resolution = rng.Range(1, 5);

                Assert.True(Bits(shape.Volume(h)) == Bits(shape.Volume(h, PartForm.None)), "volume");
                SameBits(shape.SurfacePoint(anchor, h), shape.SurfacePoint(anchor, h, PartForm.None), "surface point");
                Assert.Equal(shape.ContainsPoint(point, h), shape.ContainsPoint(point, h, PartForm.None));

                var without = new List<DragPanel>();
                var with = new List<DragPanel>();
                shape.AddPanels(h, resolution, without);
                shape.AddPanels(h, resolution, PartForm.None, with);
                SamePanels(without, with, id + " panels");

                Assert.True(
                    Bits(shape.SurfaceArea(h, resolution)) == Bits(shape.SurfaceArea(h, PartForm.None, resolution)),
                    "surface area");
            }
        }

        [Theory]
        [MemberData(nameof(NamedShapes))]
        public void ANamedShapeGivenAFormThrows(string id)
        {
            PartShape shape = PartShapeRegistry.Standard.Resolve(id);
            var h = new Float3(0.3f, 0.5f, 0.2f);
            var panels = new List<DragPanel>();

            ArgumentException e = Assert.Throws<ArgumentException>(() => shape.Volume(h, Egg));
            Assert.Contains(id, e.Message);
            Assert.Contains("profile=0.6", e.Message);

            Assert.Throws<ArgumentException>(() => shape.SurfacePoint(Float3.One, h, Egg));
            Assert.Throws<ArgumentException>(() => shape.ContainsPoint(Float3.Zero, h, Egg));
            Assert.Throws<ArgumentException>(() => shape.AddPanels(h, 2, Egg, panels));
            Assert.Throws<ArgumentException>(() => shape.SurfaceArea(h, Egg, 2));
            Assert.Empty(panels);
        }

        // ---------------------------------------------------------------- the family shape

        [Fact]
        public void TheFamilyShapeRefusesNoneAnAbsentFormAndAFormOutsideTheRanges()
        {
            var shape = new SuperellipsoidShape();
            var h = new Float3(0.3f, 0.5f, 0.2f);
            Assert.True(shape.TakesForm);
            Assert.Equal(ShapeIds.Superellipsoid, shape.Id);

            ArgumentOutOfRangeException none =
                Assert.Throws<ArgumentOutOfRangeException>(() => shape.Volume(h, PartForm.None));
            Assert.Contains("needs a form", none.Message);
            Assert.Contains("0.25", none.Message);

            var outside = new[]
            {
                new PartForm(0.05f, 1f, 1f), new PartForm(1.5f, 1f, 1f),
                new PartForm(0.5f, 0.05f, 1f), new PartForm(0.5f, 2.5f, 1f),
                new PartForm(0.5f, 1f, 0.2f), new PartForm(0.5f, 1f, 5f),
                new PartForm(float.NaN, 1f, 1f),
            };

            foreach (PartForm form in outside)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => shape.Volume(h, form));
                Assert.Throws<ArgumentOutOfRangeException>(() => shape.SurfacePoint(Float3.One, h, form));
                Assert.Throws<ArgumentOutOfRangeException>(() => shape.ContainsPoint(Float3.Zero, h, form));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => shape.AddPanels(h, 2, form, new List<DragPanel>()));
                Assert.Throws<ArgumentOutOfRangeException>(() => shape.SurfaceArea(h, form, 2));
            }
        }

        [Fact]
        public void TheFamilyShapeIsNeverMeasuredWithoutItsForm()
        {
            var shape = new SuperellipsoidShape();
            var h = new Float3(0.3f, 0.5f, 0.2f);

            Assert.Throws<InvalidOperationException>(() => shape.Volume(h));
            Assert.Throws<InvalidOperationException>(() => shape.SurfacePoint(Float3.One, h));
            Assert.Throws<InvalidOperationException>(() => shape.ContainsPoint(Float3.Zero, h));
            Assert.Throws<InvalidOperationException>(() => shape.AddPanels(h, 2, new List<DragPanel>()));
            Assert.Throws<InvalidOperationException>(() => shape.SurfaceArea(h));
        }

        [Theory]
        [InlineData(1f, 1f, 1f)]
        [InlineData(0.1f, 0.1f, 0.25f)]
        [InlineData(0.6f, 1f, 1.5f)]
        [InlineData(1f, 2f, 4f)]
        public void TheFamilyShapeIsTheGeometryItWraps(float profile, float section, float taper)
        {
            var form = new PartForm(profile, section, taper);
            var shape = new SuperellipsoidShape();
            var fresh = new Superellipsoid(form);
            var h = new Float3(0.3f, 0.7f, 0.2f);

            Assert.True(Bits(fresh.Volume(h)) == Bits(shape.Volume(h, form)), "volume");

            var rng = new Rng(11);
            for (int i = 0; i < 40; i++)
            {
                Float3 anchor = rng.NextFloat3(-1f, 1f);
                Float3 point = rng.NextFloat3(-0.8f, 0.8f);
                SameBits(fresh.SurfacePoint(anchor, h), shape.SurfacePoint(anchor, h, form), "surface point");
                Assert.Equal(fresh.ContainsPoint(point, h), shape.ContainsPoint(point, h, form));
            }

            var expected = new List<DragPanel>();
            var actual = new List<DragPanel>();
            fresh.AddPanels(h, 2, expected);
            shape.AddPanels(h, 2, form, actual);
            SamePanels(expected, actual, "panels");

            float sum = 0f;
            foreach (DragPanel p in expected) sum += p.Area;
            Assert.True(Bits(sum) == Bits(shape.SurfaceArea(h, form, 2)), "surface area");
        }

        [Fact]
        public void TheCacheNeverChangesAnAnswer()
        {
            // More forms than the cache holds, asked in an order that hits, misses and evicts, on this
            // thread and on others: every answer is a fresh geometry's.
            var shape = new SuperellipsoidShape();
            var h = new Float3(0.3f, 0.7f, 0.2f);
            var forms = new[]
            {
                new PartForm(0.2f, 0.3f, 0.5f), new PartForm(0.4f, 0.8f, 2f), new PartForm(0.6f, 1f, 1.5f),
                new PartForm(0.8f, 1.5f, 3f), new PartForm(1f, 2f, 4f), new PartForm(0.3f, 1.2f, 0.3f),
            };

            void Walk()
            {
                var rng = new Rng(5);
                for (int i = 0; i < 300; i++)
                {
                    PartForm form = forms[rng.Range(forms.Length)];
                    float expected = new Superellipsoid(form).Volume(h);
                    Assert.True(Bits(expected) == Bits(shape.Volume(h, form)), form.ToString());
                }
            }

            Walk();
            Task.WaitAll(Task.Run(Walk), Task.Run(Walk), Task.Run(Walk));
        }

        [Fact]
        public void TheHashNamesTheIdAndTheRangesAndTheStandardTableLacksThem()
        {
            string hash = new SuperellipsoidShape().HashContribution();
            Assert.StartsWith(ShapeIds.Superellipsoid, hash);
            Assert.Contains("profile=0.1..1", hash);
            Assert.Contains("section=0.1..2", hash);
            Assert.Contains("taper=0.25..4", hash);

            Assert.DoesNotContain(ShapeIds.Superellipsoid, PartShapeRegistry.Standard.HashContribution());
            Assert.Equal(3, PartShapeRegistry.Standard.Count);
            Assert.Contains(hash, FamilyRegistry.HashContribution());
        }

        // ---------------------------------------------------------------- the genome

        private static Genome FamilyGenome(PartForm form, bool withChild)
        {
            var genome = new Genome { RootIndex = 0 };

            MorphNode root = Fixtures.Box(0.3f);
            root.ShapeId = ShapeIds.Superellipsoid;
            root.Form = form;
            root.Dimensions = new Float3(0.2f, 0.45f, 0.15f);
            genome.Nodes.Add(root);

            if (withChild)
            {
                genome.Nodes.Add(Fixtures.Box(0.15f));

                MorphEdge edge = Fixtures.FaceToFace(1);
                edge.ParentAnchor = new Float3(0.6f, 0.8f, 0.4f);
                root.Edges.Add(edge);
            }

            return genome;
        }

        [Fact]
        public void CloneCarriesTheForm()
        {
            Genome genome = FamilyGenome(Egg, withChild: true);
            Genome clone = genome.Clone();

            Assert.Equal(Egg, clone.Nodes[0].Form);
            Assert.True(clone.Nodes[1].Form.IsNone);
        }

        [Fact]
        public void TwoFamilyNodesDifferByTheirLogGenesEachRangeOneUnit()
        {
            // The parameter units alone. Doubling the taper is a quarter of its range on the log
            // scale (ln 2 / ln 16); halving the profile is ln 2 / ln 10 of its own.
            Genome a = FamilyGenome(new PartForm(0.6f, 1f, 1f), withChild: false);
            Genome b = FamilyGenome(new PartForm(0.6f, 1f, 2f), withChild: false);
            Genome c = FamilyGenome(new PartForm(0.3f, 1f, 2f), withChild: false);

            Assert.Equal(0f, SpeciesDistance.Between(a, a.Clone(), 0f, 0f, 1f, 0f));
            Fixtures.AssertClose((float)(Math.Log(2) / Math.Log(16)), SpeciesDistance.Between(a, b, 0f, 0f, 1f, 0f), 1e-5f);
            Fixtures.AssertClose((float)(Math.Log(2) / Math.Log(10)), SpeciesDistance.Between(b, c, 0f, 0f, 1f, 0f), 1e-5f);

            // From one end of every range to the other is three units, one a gene.
            Genome low = FamilyGenome(new PartForm(PartForm.ProfileMin, PartForm.SectionMin, PartForm.TaperMin), false);
            Genome high = FamilyGenome(new PartForm(PartForm.ProfileMax, PartForm.SectionMax, PartForm.TaperMax), false);
            Fixtures.AssertClose(3f, SpeciesDistance.Between(low, high, 0f, 0f, 1f, 0f), 1e-5f);
        }

        [Fact]
        public void AFamilyNodeAgainstANamedOneCountsOnlyItsShapeMismatch()
        {
            // The form adds no parameter unit unless both nodes carry one, so a family node against a
            // box of the same dimensions differs by its topology's one shape unit and by nothing else.
            Genome family = FamilyGenome(Egg, withChild: false);
            Genome box = family.Clone();
            box.Nodes[0].ShapeId = ShapeIds.Box;
            box.Nodes[0].Form = PartForm.None;

            Assert.Equal(0f, SpeciesDistance.Between(family, box, 0f, 0f, 1f, 0f));
            Assert.Equal(1f, SpeciesDistance.Between(family, box, 0f, 1f, 0f, 0f));
        }

        [Fact]
        public void AFamilyNodeWithoutAFormIsOneIssue()
        {
            Genome genome = FamilyGenome(PartForm.None, withChild: false);

            IReadOnlyList<string> issues = genome.Validate(shapes: FamilyRegistry);

            string issue = Assert.Single(issues);
            Assert.Contains("Node 0", issue);
            Assert.Contains("takes a form", issue);
            Assert.Contains("none", issue);
        }

        [Fact]
        public void ANamedNodeWithAFormIsOneIssue()
        {
            Genome genome = Fixtures.SingleBox();
            genome.Nodes[0].Form = Egg;

            string issue = Assert.Single(genome.Validate());
            Assert.Contains("Node 0", issue);
            Assert.Contains("takes no form", issue);

            // The same with the registry that holds the family: a box still takes none.
            Assert.Single(genome.Validate(shapes: FamilyRegistry));
        }

        [Fact]
        public void AFamilyNodeOutsideTheRangesIsOneIssue()
        {
            Genome genome = FamilyGenome(new PartForm(0.5f, 1f, 9f), withChild: false);

            string issue = Assert.Single(genome.Validate(shapes: FamilyRegistry));
            Assert.Contains("Node 0", issue);
            Assert.Contains("taper in [0.25, 4]", issue);
        }

        [Fact]
        public void AValidFamilyGenomeHasNoIssuesWithItsRegistryAndTheUsualUnknownShapeWithTheStandardOne()
        {
            Genome genome = FamilyGenome(Egg, withChild: true);

            Assert.Empty(genome.Validate(shapes: FamilyRegistry));

            string issue = Assert.Single(genome.Validate());
            Assert.Equal(
                "Node 0: unknown shape 'superellipsoid'. Registered: box, sphere, capsule.", issue);
        }

        [Fact]
        public void TheJsonWriterRefusesAFamilyNode()
        {
            Genome genome = FamilyGenome(Egg, withChild: false);

            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => GenomeJson.Write(genome));
            Assert.Contains("not built yet", e.Message);

            // And the named genome writes, as it always did.
            Assert.False(string.IsNullOrEmpty(GenomeJson.Write(Fixtures.SingleBox())));
        }

        [Fact]
        public void AShapeMutationAwayFromTheFamilyClearsTheForm()
        {
            var rates = new MutationRates();
            foreach (PropertyInfo property in typeof(MutationRates).GetProperties())
            {
                if (property.PropertyType == typeof(float) && property.Name.EndsWith("Chance"))
                {
                    property.SetValue(rates, 0f);
                }
            }
            rates.ShapeChance = 1f;

            Genome parent = FamilyGenome(Egg, withChild: false);

            var landed = new HashSet<string>();
            for (ulong seed = 1; seed <= 30; seed++)
            {
                Genome child = Mutator.Mutate(parent, new Rng(seed), rates);

                Assert.NotEqual(ShapeIds.Superellipsoid, child.Nodes[0].ShapeId);
                Assert.True(child.Nodes[0].Form.IsNone, "the form must go with the shape");
                landed.Add(child.Nodes[0].ShapeId);
            }

            // The family node is not in the standard list, so every standard shape is a candidate.
            Assert.Equal(3, landed.Count);

            // The parent is not edited.
            Assert.Equal(Egg, parent.Nodes[0].Form);
        }

        // ---------------------------------------------------------------- development

        [Fact]
        public void AFamilyGenomeDevelopsWithItsFormItsVolumeAndItsArea()
        {
            Genome genome = FamilyGenome(Egg, withChild: false);
            Phenotype body = Developer.Develop(genome, shapes: FamilyRegistry);

            PhenotypePart part = Assert.Single(body.Parts);
            var geometry = new Superellipsoid(Egg);

            Assert.Equal(Egg, part.Form);
            Assert.NotNull(part.Family);
            Assert.Equal(1f, part.FamilyScale);
            Assert.Equal(Egg, part.Family.Geometry.Form);
            Assert.Equal(part.HalfExtents, part.Family.ReferenceHalfExtents);
            Assert.True(Bits(geometry.Volume(part.HalfExtents)) == Bits(part.Volume), "volume is the geometry's");

            var fresh = new List<DragPanel>();
            geometry.AddPanels(part.HalfExtents, 2, fresh);
            float expected = 0f;
            foreach (DragPanel p in fresh) expected += p.Area;
            Fixtures.AssertClose((double)expected, (double)part.SurfaceArea, expected * 1e-5);

            // One build served the area, and nothing else has asked yet.
            Assert.Equal(1, part.Family.PanelBuilds);
        }

        [Fact]
        public void AChildAnchoredOnAFamilyParentSitsOnItsSurface()
        {
            foreach (PartForm form in new[] { Egg, new PartForm(0.3f, 0.5f, 2.5f), new PartForm(1f, 1f, 0.5f) })
            {
                Phenotype body = Developer.Develop(FamilyGenome(form, withChild: true), shapes: FamilyRegistry);
                Assert.Equal(2, body.PartCount);

                PhenotypePart parent = body.Parts[0];
                PhenotypePart child = body.Parts[1];
                var geometry = new Superellipsoid(form);

                double inside = geometry.InsideFunction(child.ParentAnchorLocal, parent.HalfExtents);
                _output.WriteLine($"{form}: inside function at the anchor {inside:0.000000}");
                Assert.InRange(inside, 1.0 - 1e-4, 1.0 + 1e-4);

                // The named child carries no form and no family.
                Assert.True(child.Form.IsNone);
                Assert.Null(child.Family);
                Assert.Equal(1f, child.FamilyScale);
            }
        }

        [Fact]
        public void AFamilyGenomeWithoutItsRegistryOrItsFormDoesNotDevelop()
        {
            Assert.Throws<ArgumentException>(() => Developer.Develop(FamilyGenome(Egg, withChild: false)));
            Assert.Throws<ArgumentException>(
                () => Developer.Develop(FamilyGenome(PartForm.None, withChild: false), shapes: FamilyRegistry));
        }

        [Fact]
        public void ThePartGeometryAsksTheShapeWithTheForm()
        {
            Phenotype body = Developer.Develop(FamilyGenome(Egg, withChild: true), shapes: FamilyRegistry);
            PhenotypePart part = body.Parts[0];
            var geometry = new Superellipsoid(Egg);

            // The middle of the part is inside, the far corner of its box is not.
            Assert.True(part.ContainsPoint(part.Position, FamilyRegistry));
            Float3 corner = part.Position + part.Rotation.Rotate(part.HalfExtents);
            Assert.False(part.ContainsPoint(corner, FamilyRegistry));
            Assert.True(geometry.ContainsPoint(Float3.Zero, part.HalfExtents));
        }

        // ---------------------------------------------------------------- growth

        [Fact]
        public void AScaledFamilyBodyShareItsGeometryAndMeasuresNoSurface()
        {
            Phenotype adult = Developer.Develop(FamilyGenome(Egg, withChild: true), shapes: FamilyRegistry);
            FamilyGeometry shared = adult.Parts[0].Family;
            PartShape shape = FamilyRegistry.Resolve(ShapeIds.Superellipsoid);
            var geometry = new Superellipsoid(Egg);

            int builds = shared.PanelBuilds;
            Assert.Equal(1, builds);

            foreach (float s in new[] { 0.2f, 0.37f, 0.5f, 0.81f, 1.3f })
            {
                Phenotype grown = adult.Scaled(s, FamilyRegistry);
                PhenotypePart part = grown.Parts[0];

                Assert.Same(shared, part.Family);
                Assert.Equal(Egg, part.Form);
                Fixtures.AssertClose(s, part.FamilyScale, 1e-6f);
                Assert.True(grown.Parts[1].Family == null && grown.Parts[1].Form.IsNone);

                // The panels the solver gets at this size against a fresh measure at it.
                DragPanelSet set = DragPanelSet.For(shape, part, 2);
                var fresh = new List<DragPanel>();
                geometry.AddPanels(part.HalfExtents, 2, fresh);
                Assert.Equal(fresh.Count, set.Count);

                float size = Math.Max(part.HalfExtents.X, Math.Max(part.HalfExtents.Y, part.HalfExtents.Z));
                for (int i = 0; i < fresh.Count; i++)
                {
                    Fixtures.AssertClose(fresh[i].Centre, set.Centres[i], 1e-5f * size);
                    Fixtures.AssertClose(fresh[i].Normal, set.Normals[i], 1e-5f);
                    Assert.InRange(
                        set.Areas[i] / fresh[i].Area, 1.0 - 1e-5, 1.0 + 1e-5);
                }

                // Volume and surface against a fresh measure at the new size.
                Assert.True(Bits(geometry.Volume(part.HalfExtents)) == Bits(part.Volume), "volume at " + s);

                float area = 0f;
                foreach (DragPanel p in fresh) area += p.Area;
                Assert.InRange(part.SurfaceArea / area, 1.0 - 1e-5, 1.0 + 1e-5);

                Assert.Equal(builds, shared.PanelBuilds);
            }

            // The cut copies keep the geometry too: they change no half-extent.
            Phenotype cut = adult.WithoutSubtrees(new[] { false, true }, out _);
            Assert.Same(shared, cut.Parts[0].Family);
            Assert.Equal(Egg, cut.Parts[0].Form);

            Phenotype selected = adult.Select(new[] { 0 });
            Assert.Same(shared, selected.Parts[0].Family);

            Phenotype twice = adult.Scaled(0.5f, FamilyRegistry).Scaled(0.5f, FamilyRegistry);
            Fixtures.AssertClose(0.25f, twice.Parts[0].FamilyScale, 1e-6f);
            Assert.Equal(builds, shared.PanelBuilds);

            // A resolution the world does not usually ask for is built once, beside the first.
            DragPanelSet three = DragPanelSet.For(shape, adult.Scaled(0.5f, FamilyRegistry).Parts[0], 3);
            Assert.Equal(54, three.Count);
            Assert.Equal(builds + 1, shared.PanelBuilds);
            _ = adult.Parts[0].ExposureFactor(0, 1, 0);
            DragPanelSet.For(shape, adult.Parts[0], 3);
            DragPanelSet.For(shape, adult.Parts[0], 2);
            Assert.Equal(builds + 1, shared.PanelBuilds);
        }

        [Fact]
        public void AFullSizeFamilyPartGivesTheReferencePanelsToTheBit()
        {
            Phenotype adult = Developer.Develop(FamilyGenome(Egg, withChild: false), shapes: FamilyRegistry);
            PhenotypePart part = adult.Parts[0];

            var scaled = new List<DragPanel>();
            part.AddFamilyPanels(2, scaled);
            SamePanels(new List<DragPanel>(part.Family.ReferencePanels(2)), scaled, "scale 1");
        }

        [Fact]
        public void ANamedPartHasNoFamilyPanels()
        {
            Phenotype body = Developer.Develop(Fixtures.SingleBox());
            Assert.Throws<InvalidOperationException>(
                () => body.Parts[0].AddFamilyPanels(2, new List<DragPanel>()));

            // And the part overload takes the shape's own panels, as the old one did.
            PartShape box = PartShapeRegistry.Standard.Resolve(ShapeIds.Box);
            DragPanelSet viaPart = DragPanelSet.For(box, body.Parts[0], 3);
            DragPanelSet viaExtents = DragPanelSet.For(box, body.Parts[0].HalfExtents, 3);
            Assert.Equal(viaExtents.Count, viaPart.Count);
            for (int i = 0; i < viaPart.Count; i++)
            {
                SameBits(viaExtents.Centres[i], viaPart.Centres[i], "centre");
                Assert.True(Bits(viaExtents.Areas[i]) == Bits(viaPart.Areas[i]));
            }
        }

        // ---------------------------------------------------------------- exposure

        [Fact]
        public void AFamilyPartsExposureIsReadOffItsOwnPanelsAndMeasuresNothingNew()
        {
            Phenotype adult = Developer.Develop(FamilyGenome(Egg, withChild: false), shapes: FamilyRegistry);
            PhenotypePart part = adult.Parts[0];

            var fresh = new List<DragPanel>();
            new Superellipsoid(Egg).AddPanels(part.HalfExtents, 2, fresh);

            var ups = new[]
            {
                new Float3(0f, 1f, 0f), new Float3(1f, 0f, 0f), new Float3(0.6f, 0.64f, 0.48f),
                new Float3(-0.3f, 0.8f, 0.52f),
            };

            Phenotype smaller = adult.Scaled(0.4f, FamilyRegistry);
            Assert.Equal(1, part.Family.PanelBuilds);

            for (int call = 0; call < 100; call++)
            {
                Float3 up = ups[call % ups.Length];
                float length = (float)Math.Sqrt(up.X * up.X + up.Y * up.Y + up.Z * up.Z);
                double x = up.X / length, y = up.Y / length, z = up.Z / length;

                float expected = Superellipsoid.ExposureFactor(fresh, x, y, z);
                Assert.Equal(expected, part.ExposureFactor(x, y, z));

                // A scaled copy has the same factor: it is a ratio of two areas that scale together.
                Assert.Equal(expected, smaller.Parts[0].ExposureFactor(x, y, z));
            }

            Assert.Equal(1, part.Family.PanelBuilds);

            // The mean over every pose is 1, as for the other shapes (Cauchy's formula).
            double sum = 0;
            int n = 0;
            var rng = new Rng(3);
            for (int i = 0; i < 400; i++)
            {
                Float3 d = rng.NextFloat3(-1f, 1f);
                double len = Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
                if (len < 0.1 || len > 1) continue;
                sum += part.ExposureFactor(d.X / len, d.Y / len, d.Z / len);
                n++;
            }
            _output.WriteLine($"mean exposure over {n} directions: {sum / n:0.000}");
            Assert.InRange(sum / n, 0.9, 1.1);
        }
    }
}
