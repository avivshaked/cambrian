using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D110, light by exposure: a part's projected area in its pose, the hull's shadow in the same
    /// pose, and the rule that the two sides of the light are one quantity.
    /// <c>logbook/specs/light-exposure-spec.md</c> §6, tests 1 to 4 and the two-sides check.
    /// </summary>
    public class LightExposureTests
    {
        private readonly ITestOutputHelper _output;

        public LightExposureTests(ITestOutputHelper output) => _output = output;

        private static readonly Float3 Up = new Float3(0f, 1f, 0f);

        /// <summary>One part of the given half-extents and shape, developed as the world would.</summary>
        private static Phenotype OnePart(Float3 half, string shapeId = ShapeIds.Box, string cell = null)
        {
            var genome = new Genome();
            MorphNode node = Fixtures.Box();
            node.Dimensions = half;
            node.ShapeId = shapeId;
            if (cell != null) node.CellTypeId = cell;
            genome.Nodes.Add(node);
            genome.RootIndex = 0;
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 2f };
            return Developer.Develop(genome);
        }

        /// <summary>A rotation drawn uniformly over the sphere of rotations: a normalised Gaussian 4-vector.</summary>
        private static Quat RandomRotation(Rng rng)
        {
            double x = rng.Gaussian(), y = rng.Gaussian(), z = rng.Gaussian(), w = rng.Gaussian();
            double n = Math.Sqrt(x * x + y * y + z * z + w * w);
            return new Quat((float)(x / n), (float)(y / n), (float)(z / n), (float)(w / n));
        }

        /// <summary>The world's up in a frame standing at <paramref name="q"/>: its inverse applied to up.</summary>
        private static Float3 UpIn(Quat q) =>
            new Quat(-q.X, -q.Y, -q.Z, q.W).Rotate(Up);

        // ------------------------------------------------------------------ test 1

        [Fact]
        public void AThinSheetReadsTwiceFlatAndItsEdgeOnEdge()
        {
            // hy a tenth of hx and hz. Flat, the shadow is the broad face 4·hx·hz; turned a quarter
            // about x, the part's y goes to the world's z and the shadow is the edge 4·hx·hy.
            //
            // The spec writes the first as "2 · 4hx·hz / LitArea", which is twice the quantity it
            // defines in §1 (e = projected / LitArea, 2 for a flat thin sheet); the factor asserted
            // here is §1's, and test 2's mean of 1 is what pins the normalisation.
            PhenotypePart part = OnePart(new Float3(0.5f, 0.05f, 0.5f)).Parts[0];
            Float3 h = part.HalfExtents;

            float flat = part.ExposureFactor(Quat.Identity);
            float edge = part.ExposureFactor(Quat.FromAxisAngle(new Float3(1f, 0f, 0f), (float)(Math.PI / 2)));

            float flatExpected = 4f * h.X * h.Z / part.LitArea;
            float edgeExpected = 4f * h.X * h.Y / part.LitArea;

            _output.WriteLine($"flat {flat:0.######} against {flatExpected:0.######}; edge {edge:0.######} against {edgeExpected:0.######}");

            Assert.InRange(flat, flatExpected - 1e-3f, flatExpected + 1e-3f);
            Assert.InRange(edge, edgeExpected - 1e-3f, edgeExpected + 1e-3f);
            // 5/3 at a tenth, tending to 2 as the sheet thins; edge-on, 1/3 at a tenth.
            Assert.True(flat > 1.6f && edge < 0.4f, "a thin sheet lying flat should read near 2 and on edge near 0");
        }

        // ------------------------------------------------------------------ test 2

        [Fact]
        public void TheFactorAveragesOneOverRandomPoses()
        {
            // Cauchy: the mean projected area of a convex body over every orientation is a quarter
            // of its surface, which is LitArea, so the mean factor is 1 — the reason a random crowd's
            // total income does not move when the tunable goes on.
            PhenotypePart part = OnePart(new Float3(0.5f, 0.05f, 0.5f)).Parts[0];
            var rng = new Rng(20260923UL);

            const int n = 10_000;
            double sum = 0.0;
            for (int i = 0; i < n; i++) sum += part.ExposureFactor(RandomRotation(rng));

            double mean = sum / n;
            _output.WriteLine($"mean over {n} poses {mean:0.#####}");
            Assert.InRange(mean, 0.98, 1.02);
        }

        [Fact]
        public void ACapsuleAveragesOneOnItsBox()
        {
            // §1: a capsule takes the box formula on its half-extents, a ratio of the box's own two
            // quantities, so it averages 1 as a box does.
            PhenotypePart part = OnePart(new Float3(0.1f, 0.4f, 0.1f), ShapeIds.Capsule).Parts[0];
            var rng = new Rng(7UL);

            double sum = 0.0;
            for (int i = 0; i < 10_000; i++) sum += part.ExposureFactor(RandomRotation(rng));

            Assert.InRange(sum / 10_000, 0.98, 1.02);
        }

        // ------------------------------------------------------------------ test 3

        [Fact]
        public void ASphereReadsOneAtAnyAngle()
        {
            PhenotypePart part = OnePart(new Float3(0.3f, 0.3f, 0.3f), ShapeIds.Sphere).Parts[0];
            var rng = new Rng(3UL);

            for (int i = 0; i < 100; i++) Assert.Equal(1f, part.ExposureFactor(RandomRotation(rng)));
        }

        // ------------------------------------------------------------------ test 4

        [Fact]
        public void ACubesShadowIsItsFaceFlatAndRootTwoItsFaceAtFortyFive()
        {
            const float half = 0.5f;
            Phenotype cube = OnePart(new Float3(half, half, half));
            float face = (2f * half) * (2f * half);

            float level = cube.ShadowArea(Up);
            float tilted = cube.ShadowArea(UpIn(Quat.FromAxisAngle(new Float3(1f, 0f, 0f), (float)(Math.PI / 4))));

            _output.WriteLine($"face {face}; shadow level {level:0.######}, at 45 deg {tilted:0.######}");

            Fixtures.AssertClose(face, level, 1e-5f);
            Fixtures.AssertClose((float)Math.Sqrt(2.0) * face, tilted, 1e-5f);
        }

        [Fact]
        public void AOnePartBodysHullShadowIsItsPartsExposedArea()
        {
            // For a one-box body the hull is the box, so the hull's shadow in a pose and the part's
            // exposed area in that pose are one number by two routes — the harness's three dot
            // products against the hull's face sum. A disagreement would be a cap that bites a body
            // laid out in the open.
            Phenotype sheet = OnePart(new Float3(0.5f, 0.05f, 0.3f));
            var rng = new Rng(11UL);

            for (int i = 0; i < 200; i++)
            {
                Quat q = RandomRotation(rng);
                float[] e = { sheet.Parts[0].ExposureFactor(q) };

                float exposed = sheet.ExposedLitArea(e);
                float shadow = sheet.ShadowArea(UpIn(q));

                Assert.InRange(shadow, exposed * (1f - 1e-4f), exposed * (1f + 1e-4f));
                Assert.InRange(sheet.LitAreaFactor(e, UpIn(q), capOn: true), 1f - 1e-4f, 1f);
            }
        }

        [Fact]
        public void TheCapInAPoseIsNeverAboveOne()
        {
            // Sixteen coincident boxes: the hull is one box and the parts bill sixteen. Whatever
            // the pose, the shadow is one box's projection and the exposed sum sixteen of it, so the
            // cap reads a sixteenth; and it never hands a body more than its parts present.
            Phenotype knot = Developer.Develop(
                Fixtures.CoincidentKnot(16, 0.25f, CellTypeIds.Photosynthetic),
                new DevelopmentLimits { MaxParts = 16, MaxDepth = 16 });
            var rng = new Rng(13UL);

            for (int i = 0; i < 200; i++)
            {
                Quat q = RandomRotation(rng);
                var e = new float[knot.PartCount];
                for (int p = 0; p < e.Length; p++) e[p] = knot.Parts[p].ExposureFactor(q);

                float factor = knot.LitAreaFactor(e, UpIn(q), capOn: true);
                Assert.InRange(factor, 0f, 1f);
                Assert.InRange(factor, 1f / knot.PartCount - 1e-4f, 1f / knot.PartCount + 1e-4f);
            }
        }

        [Fact]
        public void AskingForTheFacesMovesNoSilhouette()
        {
            // §2: the surface D099 prints must not move by a bit when the faces are collected
            // beside it. Two hundred random bodies, the area with and without the list, compared
            // as bit patterns, and the phenotype's silhouette is the same number again.
            int fellBack = 0;

            for (ulong seed = 1; seed <= 200; seed++)
            {
                Phenotype body = Developer.Develop(GenomeFactory.Random(new Rng(seed)));

                var corners = new System.Collections.Generic.List<Double3>();
                foreach (PhenotypePart part in body.Parts) ConvexHull.AppendPartCorners(part, corners);

                var faces = new System.Collections.Generic.List<Double3>();
                double plain = ConvexHull.SurfaceArea(corners, out bool fellA, out _);
                double withFaces = ConvexHull.SurfaceArea(corners, out bool fellB, out _, faces);

                Assert.Equal(BitConverter.DoubleToInt64Bits(plain), BitConverter.DoubleToInt64Bits(withFaces));
                Assert.Equal(fellA, fellB);
                Assert.Equal((float)(plain / 4.0), body.SilhouetteArea);
                Assert.True(faces.Count > 0);
                if (fellA) fellBack++;
            }

            _output.WriteLine($"200 random bodies, {fellBack} fell back to the box");
        }

        [Fact]
        public void GrowthScalesTheShadowAsItScalesTheSilhouette()
        {
            // The faces are shared with every Scaled copy and multiplied by the square of the
            // length ratio, so a body at half its length shades a quarter in any pose.
            Phenotype adult = OnePart(new Float3(0.5f, 0.05f, 0.3f));
            Phenotype half = adult.Scaled(0.5f);
            Float3 up = UpIn(Quat.FromAxisAngle(new Float3(0.6f, 0f, 0.8f), 0.7f));

            Fixtures.AssertClose(adult.ShadowArea(up) * 0.25f, half.ShadowArea(up), 1e-6f);
            Assert.Equal(adult.HullFaceCount, half.HullFaceCount);
        }

        // ------------------------------------------------------------------ the two sides

        /// <summary>A world lit from above with nothing in it but what a test puts there.</summary>
        private static RunConfig Stage(bool capOn, bool byExposure) => new RunConfig
        {
            MinimumPopulation = 0,
            MaximumPopulation = 100,
            Light = new LightModel(100f, 12f),
            LightSilhouetteCap = capOn,
            LightByExposure = byExposure,

            // Enough to live a step on and too little to breed on: a child's shade would join the
            // parent's in the layer, and the test reads one body's shade.
            FounderEnergyJoules = 5f,
            UptakeRatePerSquareMetre = 0f,
            Development = new DevelopmentLimits { MaxParts = 16, MaxDepth = 16 },
            WorldAreaSquareMetres = 100_000f,
        };

        /// <summary>
        /// Steps one inoculated body once with a pose set by hand, then checks that the shade it
        /// contributed and the area its parts were billed on are one number: the shade by
        /// re-solving a fresh field with the area the phenotype says, the billing by reading the
        /// light income back through the irradiance it was paid at.
        /// </summary>
        private void AssertBothSidesAgree(Genome genome, bool capOn, bool byExposure)
        {
            var world = new World(Stage(capOn, byExposure), seed: 5);
            world.Inoculate(genome, count: 1, heightY: -1f);

            Organism creature = world.Living[0];

            // A pose: the body turned 70° about a tilted axis, each part's factor from that pose.
            Quat pose = Quat.FromAxisAngle(new Float3(0.6f, 0f, 0.8f), 1.2f);
            var exposure = new float[creature.Phenotype.PartCount];
            for (int p = 0; p < exposure.Length; p++)
            {
                exposure[p] = creature.Phenotype.Parts[p].ExposureFactor(pose * creature.Phenotype.Parts[p].Rotation);
            }
            creature.PartExposure = exposure;
            creature.UpInBody = UpIn(pose);

            world.Step(1f);
            Assert.Single(world.Living);

            Phenotype body = creature.Phenotype;
            float expected = byExposure
                ? body.EffectiveLitArea(exposure, creature.UpInBody, capOn)
                : body.EffectiveLitArea(capOn);

            // The shading side: a fresh field given exactly the expected area solves to the same
            // shading the world's did, to the bit.
            var probe = new LightField(world.Field.Model, world.Field.WorldArea, world.Field.LayerMetres);
            probe.Contribute(creature.HeightY, expected);
            probe.Solve();
            Assert.Equal(probe.ShadingAt(creature.HeightY), world.Field.ShadingAt(creature.HeightY));

            // The income side: what the parts were billed on, as Metabolism bills them, summed.
            float billed = 0f;
            float factor = byExposure
                ? body.LitAreaFactor(exposure, creature.UpInBody, capOn)
                : body.LitAreaFactor(capOn);
            for (int p = 0; p < body.PartCount; p++)
            {
                billed += byExposure
                    ? body.Parts[p].LitArea * exposure[p] * factor
                    : body.Parts[p].LitArea * factor;
            }

            // And the world actually paid on it: the income over the irradiance the body sat in
            // is its billed area times the leaf's efficiency, the same for both routes.
            float irradiance = world.Field.IrradianceAt(creature.HeightY, creature.Patch, creature.X, creature.Z);
            EnergyLedger predicted = byExposure
                ? Metabolism.StepAt(body, world.Config, irradiance, 0f, 0f, 0f, 1f, 0f, exposure, creature.UpInBody)
                : Metabolism.StepAt(body, world.Config, irradiance, 0f, 0f, 0f, 1f, 0f);

            _output.WriteLine(
                $"cap {capOn}, exposure {byExposure}: shade {expected:0.########} m2, billed {billed:0.########} m2, " +
                $"income {creature.Lifetime.LightIncome:0.########} J against {predicted.LightIncome:0.########} J");

            Assert.InRange(billed, expected * (1f - 1e-5f), expected * (1f + 1e-5f));
            Assert.Equal(predicted.LightIncome, creature.Lifetime.LightIncome);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void TheShadeCastIsTheAreaBilledForASheet(bool capOn, bool byExposure) =>
            AssertBothSidesAgree(
                OnePartGenome(new Float3(0.5f, 0.05f, 0.3f), CellTypeIds.Photosynthetic), capOn, byExposure);

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void TheShadeCastIsTheAreaBilledForAKnot(bool capOn, bool byExposure) =>
            AssertBothSidesAgree(
                Fixtures.CoincidentKnot(16, 0.25f, CellTypeIds.Photosynthetic), capOn, byExposure);

        [Fact]
        public void WithTheTunableOffASetExposureIsNotRead()
        {
            // The off path's promise: a world with LightByExposure false never enters the new
            // methods, so an exposure array set on a creature by anyone changes nothing.
            Genome genome = OnePartGenome(new Float3(0.5f, 0.05f, 0.3f), CellTypeIds.Photosynthetic);

            float Income(bool setExposure)
            {
                var world = new World(Stage(capOn: true, byExposure: false), seed: 5);
                world.Inoculate(genome, count: 1, heightY: -1f);
                if (setExposure) world.Living[0].PartExposure = new[] { 2f };
                world.Step(1f);
                return world.Living[0].Lifetime.LightIncome;
            }

            Assert.Equal(Income(false), Income(true));
        }

        [Fact]
        public void AStaleExposureIsReadAsTheAverage()
        {
            // An array of the wrong length names another plan's parts; the world takes the
            // orientation average for both sides rather than read it.
            Genome genome = OnePartGenome(new Float3(0.5f, 0.05f, 0.3f), CellTypeIds.Photosynthetic);

            float Income(float[] exposure, bool on)
            {
                var world = new World(Stage(capOn: true, byExposure: on), seed: 5);
                world.Inoculate(genome, count: 1, heightY: -1f);
                world.Living[0].PartExposure = exposure;
                world.Living[0].UpInBody = Up;
                world.Step(1f);
                return world.Living[0].Lifetime.LightIncome;
            }

            Assert.Equal(Income(null, on: false), Income(new[] { 2f, 2f }, on: true));
            Assert.True(Income(new[] { 2f }, on: true) > Income(null, on: false) * 1.5f);
        }

        private static Genome OnePartGenome(Float3 half, string cell)
        {
            var genome = new Genome();
            MorphNode node = Fixtures.Box();
            node.Dimensions = half;
            node.CellTypeId = cell;
            genome.Nodes.Add(node);
            genome.RootIndex = 0;
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 2f };
            return genome;
        }
    }
}
