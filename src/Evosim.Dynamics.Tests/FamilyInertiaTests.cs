using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D233's second piece in the solver: a family part's inertia is the geometry's, a named part's is
    /// the old overload's to the bit, and a body that holds a family part builds and resizes without
    /// measuring its surface again.
    /// </summary>
    public sealed class FamilyInertiaTests
    {
        private static readonly PartShapeRegistry Family = new PartShapeRegistry(
            new BoxShape(), new SphereShape(), new CapsuleShape(), new SuperellipsoidShape());

        private static readonly PartForm Egg = new PartForm(0.6f, 1f, 1.5f);

        private static Genome FamilyGenome()
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits
            {
                BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f,
            };

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.2f, 0.45f, 0.15f),
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Superellipsoid,
                Form = Egg,
                JointType = JointType.Fixed,
                JointLimits = System.Array.Empty<Float2>(),
                RecursiveLimit = 1,
            });

            return genome;
        }

        [Fact]
        public void AFamilyPartsInertiaIsTheGeometrysTimesTheMass()
        {
            Phenotype body = Developer.Develop(FamilyGenome(), shapes: Family);
            PhenotypePart part = body.Parts[0];
            PartShape shape = Family.Resolve(part.ShapeId);
            const double mass = 3.5;

            Vec3 inertia = Creature.InertiaOf(shape, part, mass);
            Double3 perMass = new Superellipsoid(Egg).InertiaPerMass(part.HalfExtents);

            Assert.Equal(perMass.X * mass, inertia.X);
            Assert.Equal(perMass.Y * mass, inertia.Y);
            Assert.Equal(perMass.Z * mass, inertia.Z);

            // The half-extents overload cannot give it: the tensor needs the form.
            Assert.Throws<System.ArgumentException>(() => Creature.InertiaOf(shape, part.HalfExtents, mass));
        }

        [Theory]
        [InlineData(ShapeIds.Box)]
        [InlineData(ShapeIds.Sphere)]
        [InlineData(ShapeIds.Capsule)]
        public void ANamedPartsInertiaIsTheOldOverloadsToTheBit(string id)
        {
            var genome = TestBodies.Box(new Float3(0.2f, 0.45f, 0.15f));
            genome.Nodes[0].ShapeId = id;

            Phenotype body = Developer.Develop(genome);
            PhenotypePart part = body.Parts[0];
            PartShape shape = PartShapeRegistry.Standard.Resolve(id);

            Vec3 viaPart = Creature.InertiaOf(shape, part, 2.25);
            Vec3 viaExtents = Creature.InertiaOf(shape, part.HalfExtents, 2.25);

            Assert.Equal(viaExtents.X, viaPart.X);
            Assert.Equal(viaExtents.Y, viaPart.Y);
            Assert.Equal(viaExtents.Z, viaPart.Z);
        }

        [Fact]
        public void ABodyWithAFamilyPartBuildsAndResizesWithoutMeasuringItsSurface()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;

            Phenotype adult = Developer.Develop(FamilyGenome(), shapes: Family);
            FamilyGeometry shared = adult.Parts[0].Family;
            int builds = shared.PanelBuilds;

            var creature = new Creature(0, adult, config, Family);

            // The built body carries the geometry's inertia, not the box's, and the drag panels sum
            // to the part's area.
            double plain = creature.PlainMass[0];
            Vec3 expected = Creature.InertiaOf(Family.Resolve(ShapeIds.Superellipsoid), adult.Parts[0], plain)
                * (creature.Mass[0] / plain);
            Assert.Equal(expected.X, creature.InertiaLocal[0]);
            Assert.Equal(expected.Y, creature.InertiaLocal[1]);
            Assert.Equal(expected.Z, creature.InertiaLocal[2]);

            double area = 0;
            for (int i = creature.PanelStart[0]; i < creature.PanelStart[1]; i++) area += creature.PanelArea[i];
            Assert.InRange(area / adult.Parts[0].SurfaceArea, 1 - 1e-5, 1 + 1e-5);

            foreach (float s in new[] { 0.4f, 0.7f, 1.2f })
            {
                Phenotype grown = adult.Scaled(s, Family);
                creature.Resize(grown);

                PhenotypePart part = grown.Parts[0];
                Vec3 tensor = Creature.InertiaOf(Family.Resolve(ShapeIds.Superellipsoid), part, creature.PlainMass[0])
                    * (creature.Mass[0] / creature.PlainMass[0]);
                Assert.Equal(tensor.Y, creature.InertiaLocal[1]);

                double grownArea = 0;
                for (int i = creature.PanelStart[0]; i < creature.PanelStart[1]; i++) grownArea += creature.PanelArea[i];
                Assert.InRange(grownArea / part.SurfaceArea, 1 - 1e-5, 1 + 1e-5);
            }

            Assert.Equal(builds, shared.PanelBuilds);
        }
    }
}
