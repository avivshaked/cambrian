using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Both ends of the size range, and what happens to a body that leaves it —
    /// DESIGN.md §4.2, §4.5, §5A.6, D024.
    /// </summary>
    /// <remarks>
    /// Mutation perturbs a half-extent by a Gaussian scaled to the half-extent itself, which makes
    /// size a multiplicative random walk with no stationary distribution. §4.5 depends on the
    /// lower tail being absorbed. Until logbook/0011 nothing absorbed the upper one.
    /// </remarks>
    public class SizeBoundsTests
    {
        private readonly ITestOutputHelper _output;

        public SizeBoundsTests(ITestOutputHelper output) => _output = output;

        private static Genome Box(float x, float y, float z, string cell = null)
        {
            var genome = new Genome { RootIndex = 0 };
            genome.Nodes.Add(new MorphNode
            {
                CellTypeId = cell ?? CellTypeIds.Photosynthetic,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(x, y, z),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });
            return genome;
        }

        [Fact]
        public void AGiantIsPrunedJustAsAMoteIs()
        {
            var limits = new DevelopmentLimits { MinPartVolume = 1e-4f, MaxPartVolume = 1e6f };

            // 8 * 100^3 = 8e6 m³, comfortably over.
            Phenotype giant = Developer.Develop(Box(100f, 100f, 100f), limits);
            Assert.Equal(0, giant.PartCount);
            Assert.Equal(1, giant.PrunedForVolume);

            // And the ordinary case is untouched.
            Phenotype normal = Developer.Develop(Box(0.3f, 0.3f, 0.3f), limits);
            Assert.Equal(1, normal.PartCount);
        }

        [Fact]
        public void EveryDerivedQuantityStaysRepresentable()
        {
            // The failure this bound exists for. A half-extent of 10^18 m gives a volume of 10^54,
            // past float.MaxValue — so upkeep is infinite, energy goes to −∞, and §5A.2's audit is
            // permanently NaN with no way back.
            var config = new RunConfig();

            Phenotype p = Developer.Develop(
                Box(1e18f, 1e18f, 1e18f), config.Development, null, config.Shapes);

            Assert.Equal(0, p.PartCount);

            // Nothing survived, so nothing can poison the books.
            EnergyLedger ledger = Metabolism.Step(
                p, config, new LightModel(100f, 12f), 0f, 0f, 0f, 1f);

            Assert.True(float.IsFinite(ledger.Income));
            Assert.True(float.IsFinite(ledger.Expenditure));
        }

        [Fact]
        public void AWaferIsThickenedRatherThanDeleted()
        {
            // Flatness is a real and valuable trait — a flat box is the strongest paddle in the
            // registry — so a thin part must survive. It is the *unbounded* thinness that has to
            // stop, because volume does not bound surface area and the economy pays for area.
            var limits = new DevelopmentLimits { MinPartHalfExtent = 0.01f };

            Phenotype p = Developer.Develop(Box(1e-25f, 1e-5f, 30f), limits);

            Assert.Equal(1, p.PartCount);
            Assert.Equal(0.01f, p.Parts[0].HalfExtents.X, 6);
            Assert.Equal(0.01f, p.Parts[0].HalfExtents.Y, 6);
            Assert.Equal(30f, p.Parts[0].HalfExtents.Z, 4);

            // A genuinely flat but sane part keeps its shape exactly.
            Phenotype flat = Developer.Develop(Box(0.02f, 0.5f, 0.5f), limits);
            Assert.Equal(0.02f, flat.Parts[0].HalfExtents.X, 6);
        }

        [Fact]
        public void ThickeningHappensBeforeTheVolumeLimitsAreApplied()
        {
            // Order matters: the genome asked for a part of volume 8e-30 m³, far under the floor,
            // but the part that will actually be built is 0.01 x 0.01 x 30, or 0.012 m³. Checking
            // the genome's figure would prune a part that is comfortably legal once built.
            var limits = new DevelopmentLimits
            {
                MinPartVolume = 1e-4f, MaxPartVolume = 1e6f, MinPartHalfExtent = 0.01f,
            };

            Phenotype p = Developer.Develop(Box(1e-15f, 1e-15f, 30f), limits);

            Assert.Equal(1, p.PartCount);
            _output.WriteLine($"built {p.Parts[0].HalfExtents}, volume {p.Parts[0].Volume:0.####} m³");
        }

        [Fact]
        public void ABodylessCreatureIsStillbornRatherThanImmortal()
        {
            // With no parts there is nothing to price, so income and upkeep are both exactly zero,
            // energy never moves, and §5A.6's death-at-zero never fires. Such a creature would
            // occupy a slot against the population floor forever while costing and doing nothing.
            // It is reachable: §4.5's extinction-by-shrinking prunes the root as readily as any
            // other node.
            var config = new RunConfig
            {
                MinimumPopulation = 20,
                MaximumPopulation = 5000,
                // Nothing at all can develop — every part is under the floor.
                Development = new DevelopmentLimits
                {
                    MinPartVolume = 1e9f, MaxPartVolume = 1e12f, MinPartHalfExtent = 0.01f,
                },
            };

            var world = new World(config, new LightModel(100f, 12f), 1);
            for (int i = 0; i < 200; i++) world.Step(1f);

            _output.WriteLine(
                $"stillbirths {world.Stillbirths}, floor spawns {world.FloorSpawns}, " +
                $"living {world.Living.Count}");

            Assert.Equal(0, world.Living.Count);
            Assert.True(world.Stillbirths > 0, "nothing could develop, so nothing should have lived");
            Assert.Equal(world.FloorSpawns, world.Stillbirths);

            // The floor kept trying — which is the correct, visible behaviour for a world where
            // nothing can live, and is exactly what D021 wants reported rather than smoothed over.
            Assert.True(world.FloorSpawns > 100);
        }

        [Fact]
        public void AStillbirthDoesNotCreateOrDestroyEnergy()
        {
            // A floor spawn's endowment is created at admission, so a refused one must never have
            // been created. An offspring's was already deducted from its parent, so a refused one
            // must be recorded as leaving. Getting either backwards leaves §5A.2's audit open by
            // exactly the endowment, per stillbirth, forever.
            var config = new RunConfig
            {
                MinimumPopulation = 20,
                Development = new DevelopmentLimits
                {
                    MinPartVolume = 1e9f, MaxPartVolume = 1e12f, MinPartHalfExtent = 0.01f,
                },
            };

            var world = new World(config, new LightModel(100f, 12f), 1);
            for (int i = 0; i < 200; i++) world.Step(1f);

            _output.WriteLine($"in {world.EnergyIn:0.####} J, out {world.EnergyOut:0.####} J");

            Assert.Equal(0.0, world.EnergyIn, 6);
            Assert.Equal(0.0, world.EnergyOut, 6);
        }
    }
}
