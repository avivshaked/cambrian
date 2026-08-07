using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>Part geometry — DESIGN.md §4.1, and its effect on drag (§5.2).</summary>
    public class ShapeTests
    {
        private readonly ITestOutputHelper _output;

        public ShapeTests(ITestOutputHelper output) => _output = output;

        private static readonly Float3 Unit = new Float3(0.5f, 0.5f, 0.5f);

        [Fact]
        public void VolumesMatchTheAnalyticFormulae()
        {
            Fixtures.AssertClose(1f, new BoxShape().Volume(Unit), 1e-5f);
            Fixtures.AssertClose(
                (4f / 3f) * (float)Math.PI * 0.125f, new SphereShape().Volume(Unit), 1e-5f);

            // A capsule whose half-length equals its radius is a sphere, and should measure as
            // one — the degenerate case is a limit of the shape, not an error.
            Fixtures.AssertClose(
                new SphereShape().Volume(Unit), new CapsuleShape().Volume(Unit), 1e-5f);
        }

        [Fact]
        public void ASphereHoldsAboutHalfItsBoundingBox()
        {
            // Why development prunes on the shape's volume rather than the box's: the two differ
            // by a factor near two, and mass, upkeep and drag all follow the shape.
            float ratio = new SphereShape().Volume(Unit) / new BoxShape().Volume(Unit);

            _output.WriteLine($"sphere / box volume: {ratio:P1}");
            Assert.InRange(ratio, 0.5f, 0.53f);
        }

        [Theory]
        [InlineData(ShapeIds.Box)]
        [InlineData(ShapeIds.Sphere)]
        [InlineData(ShapeIds.Capsule)]
        public void PanelAreasSumToTheSurfaceArea(string id)
        {
            // Panels do not have to tile the surface — the drag model samples rather than
            // integrating — but a set summing to the wrong total scales every force on that
            // shape, which would make shape choice a fitness lever rather than a morphological
            // one. Cheapest way to catch that: check the total.
            PartShape shape = PartShapeRegistry.Standard.Resolve(id);
            var h = new Float3(0.3f, 0.7f, 0.3f);

            var panels = new List<DragPanel>();
            shape.AddPanels(h, 3, panels);

            float total = 0f;
            foreach (DragPanel p in panels) total += p.Area;

            float expected = ExpectedSurfaceArea(id, h);
            _output.WriteLine($"{id}: {panels.Count} panels, {total:0.####} m² vs {expected:0.####}");

            Fixtures.AssertClose(expected, total, expected * 0.02f);
        }

        private static float ExpectedSurfaceArea(string id, Float3 h)
        {
            switch (id)
            {
                case ShapeIds.Box:
                    return 8f * (h.X * h.Y + h.Y * h.Z + h.Z * h.X);

                case ShapeIds.Sphere:
                {
                    float r = (h.X + h.Y + h.Z) / 3f;
                    return 4f * (float)Math.PI * r * r;
                }

                default:
                {
                    float r = (h.X + h.Z) / 2f;
                    float span = Math.Max(0f, h.Y - r);
                    return 2f * (float)Math.PI * r * (2f * span) + 4f * (float)Math.PI * r * r;
                }
            }
        }

        [Theory]
        [InlineData(ShapeIds.Box)]
        [InlineData(ShapeIds.Sphere)]
        [InlineData(ShapeIds.Capsule)]
        public void PanelNormalsPointOutward(string id)
        {
            // An inward-facing normal would collect thrust instead of drag on that panel — a
            // free-energy source of exactly the kind §11.2 exists to catch, and one that would
            // be invisible in aggregate.
            PartShape shape = PartShapeRegistry.Standard.Resolve(id);
            var panels = new List<DragPanel>();
            shape.AddPanels(new Float3(0.2f, 0.6f, 0.2f), 2, panels);

            foreach (DragPanel p in panels)
            {
                Assert.True(Float3.Dot(p.Centre, p.Normal) > 0f,
                    $"{id}: panel at {p.Centre} faces {p.Normal}, which points inward");
            }
        }

        [Fact]
        public void OnlyASphereHasNoPreferredDirection()
        {
            // The claim that decides what each shape is good for. A box and a capsule present
            // different areas depending on which way they move, and that difference is what a
            // paddle is. A sphere presents the same area every way, so it cannot paddle at all —
            // a real prediction of the model, worth having a test say out loud.
            var h = new Float3(0.1f, 0.6f, 0.1f);
            var config = new FluidConfig { Density = 1000f, DragCoefficient = 1.5f };

            _output.WriteLine("| shape | along Y | across X | ratio |");
            _output.WriteLine("|---|---|---|---|");

            foreach (string id in PartShapeRegistry.Standard.Ids())
            {
                PartShape shape = PartShapeRegistry.Standard.Resolve(id);
                var scratch = new List<DragPanel>();

                FluidModel.Drag(shape, h, Quat.Identity, new Float3(0f, 1f, 0f), Float3.Zero,
                    config, scratch, out Float3 along, out _);
                FluidModel.Drag(shape, h, Quat.Identity, new Float3(1f, 0f, 0f), Float3.Zero,
                    config, scratch, out Float3 across, out _);

                float a = (float)Math.Sqrt(Float3.Dot(along, along));
                float b = (float)Math.Sqrt(Float3.Dot(across, across));
                float ratio = b / Math.Max(1e-6f, a);

                _output.WriteLine($"| {id} | {a:0.##} | {b:0.##} | {ratio:0.##} |");

                if (id == ShapeIds.Sphere) Fixtures.AssertClose(1f, ratio, 0.08f);
                else Assert.True(ratio > 1.5f, $"{id} should be strongly anisotropic, got {ratio:0.##}");
            }
        }

        [Fact]
        public void OnlyABoxCanBeFlatAndFlatIsTheStrongestPaddle()
        {
            // BoxShape's documentation claims this, and it is the reason boxes are drawn twice as
            // often as the other shapes (GenomeFactory.ShapeIdChoices). Worth measuring rather
            // than asserting: at rod-like extents a capsule is actually the more anisotropic of
            // the two. The claim is specifically about flatness, so the fixture has to be flat.
            var flat = new Float3(0.6f, 0.05f, 0.6f);
            var config = new FluidConfig { Density = 1000f, DragCoefficient = 1.5f };

            _output.WriteLine("| shape | broadside | edge-on | ratio |");
            _output.WriteLine("|---|---|---|---|");

            float boxRatio = 0f;
            foreach (string id in PartShapeRegistry.Standard.Ids())
            {
                PartShape shape = PartShapeRegistry.Standard.Resolve(id);
                var scratch = new List<DragPanel>();

                FluidModel.Drag(shape, flat, Quat.Identity, new Float3(0f, 1f, 0f), Float3.Zero,
                    config, scratch, out Float3 broadside, out _);
                FluidModel.Drag(shape, flat, Quat.Identity, new Float3(1f, 0f, 0f), Float3.Zero,
                    config, scratch, out Float3 edgeOn, out _);

                float a = (float)Math.Sqrt(Float3.Dot(broadside, broadside));
                float b = (float)Math.Sqrt(Float3.Dot(edgeOn, edgeOn));
                float ratio = a / Math.Max(1e-6f, b);

                _output.WriteLine($"| {id} | {a:0.##} | {b:0.##} | {ratio:0.##} |");

                if (id == ShapeIds.Box) boxRatio = ratio;
                else Assert.True(boxRatio > ratio,
                    $"a flat box is {boxRatio:0.##}× directional, {id} is {ratio:0.##}× — the " +
                    "doc comment on BoxShape is wrong");
            }

            Assert.True(boxRatio > 5f, $"a flat box should be strongly directional, got {boxRatio:0.##}");
        }

        [Fact]
        public void ChildrenAttachToTheActualSurface()
        {
            // Anchors are directions and each shape decides where its surface is. Scaling by
            // half-extents instead would attach children to a bounding box that is not there,
            // leaving a visible gap on every round part.
            foreach (string id in PartShapeRegistry.Standard.Ids())
            {
                PartShape shape = PartShapeRegistry.Standard.Resolve(id);
                var h = new Float3(0.2f, 0.5f, 0.2f);

                foreach (Float3 anchor in new[]
                {
                    new Float3(1f, 0f, 0f), new Float3(0f, 1f, 0f), new Float3(0f, 0f, -1f),
                    new Float3(-1f, 0f, 0f), new Float3(0f, -1f, 0f), new Float3(0f, 0f, 1f),
                })
                {
                    Float3 p = shape.SurfacePoint(anchor, h);

                    Assert.True(shape.ContainsPoint(p * 0.98f, h),
                        $"{id}: just inside {anchor} is reported outside");
                    Assert.False(shape.ContainsPoint(p * 1.05f, h),
                        $"{id}: just outside {anchor} is reported inside");

                    // On the surface is not enough — it has to be on the surface in the
                    // direction that was asked for. Checking only the first two let a capsule
                    // return the SIDE of its end cap for a pole anchor: a legitimate surface
                    // point, a radius away from the right one, and a child attached there sits
                    // inside its own parent. All six axes, because the bug was on the negative
                    // one and three anchors had missed it.
                    float length = (float)Math.Sqrt(Float3.Dot(p, p));
                    Assert.True(length > 1e-6f, $"{id}: surface point for {anchor} is the origin");

                    Fixtures.AssertClose(1f, Float3.Dot(p * (1f / length), anchor), 1e-3f);
                }
            }
        }

        [Fact]
        public void AnUnknownShapeIsRefusedRatherThanDefaulted()
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => PartShapeRegistry.Standard.Resolve("dodecahedron"));

            Assert.Contains("dodecahedron", e.Message);
        }

        [Fact]
        public void ShapeOrderAndMembershipAreInTheConfigHash()
        {
            // Shape mutation picks by an RNG draw, so order decides which shape a draw yields.
            var a = new RunConfig { Shapes = new PartShapeRegistry(new BoxShape(), new SphereShape()) };
            var b = new RunConfig { Shapes = new PartShapeRegistry(new SphereShape(), new BoxShape()) };
            var c = new RunConfig { Shapes = new PartShapeRegistry(new BoxShape()) };

            Assert.NotEqual(a.Hash(), b.Hash());
            Assert.NotEqual(a.Hash(), c.Hash());
        }

        [Fact]
        public void RandomGenomesUseEveryShapeAndAllDevelop()
        {
            var counts = new Dictionary<string, int>();
            foreach (string id in PartShapeRegistry.Standard.Ids()) counts[id] = 0;

            for (ulong seed = 1; seed <= 300; seed++)
            {
                Genome g = GenomeFactory.Random(new Rng(seed));
                Assert.Empty(g.Validate());

                foreach (PhenotypePart part in Developer.Develop(g).Parts) counts[part.ShapeId]++;
            }

            foreach (var kv in counts) _output.WriteLine($"{kv.Key,-10} {kv.Value} parts");
            foreach (var kv in counts) Assert.True(kv.Value > 0, $"no {kv.Key} was ever grown");
        }

        [Fact]
        public void ShapeSurvivesSerializationAndMutation()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                Genome g = GenomeFactory.Random(new Rng(seed));

                Genome back = GenomeJson.Read(GenomeJson.Write(g));
                for (int i = 0; i < g.Nodes.Count; i++)
                {
                    Assert.Equal(g.Nodes[i].ShapeId, back.Nodes[i].ShapeId);
                }

                foreach (MorphNode node in Mutator.Mutate(g, new Rng(seed)).Nodes)
                {
                    Assert.True(PartShapeRegistry.Standard.Contains(node.ShapeId),
                        $"mutation produced shape '{node.ShapeId}'");
                }
            }
        }

        [Fact]
        public void ShapeMutationAlwaysChangesTheShape()
        {
            // The operator excludes the current value, so firing it always does something.
            // Redrawing the same shape would make the effective rate depend on how many shapes
            // are registered — adding a fourth would quietly change how often the others mutate.
            var rates = new MutationRates { ShapeChance = 1f, CellTypeChance = 0f, AddNodeChance = 0f };

            int compared = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                Genome parent = GenomeFactory.Random(new Rng(seed));
                Genome child = Mutator.Mutate(parent, new Rng(seed), rates);

                for (int i = 0; i < Math.Min(parent.Nodes.Count, child.Nodes.Count); i++)
                {
                    Assert.NotEqual(parent.Nodes[i].ShapeId, child.Nodes[i].ShapeId);
                    compared++;
                }
            }

            _output.WriteLine($"{compared} nodes compared");
            Assert.True(compared > 100);
        }
    }
}
