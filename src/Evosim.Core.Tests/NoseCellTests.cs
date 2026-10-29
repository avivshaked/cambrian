using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D235's nose in Core: the type and its table, the noise law, the hash it draws from, the
    /// gate that leaves smell ports on noses alone, and its price by area.
    /// </summary>
    public sealed class NoseCellTests
    {
        private readonly ITestOutputHelper _out;

        public NoseCellTests(ITestOutputHelper output) => _out = output;

        private static CellTypeRegistry WithNose(NoseCell nose = null) =>
            CellTypeRegistry.Standard.Appending(nose ?? new NoseCell());

        private static readonly SensorChannel[] Pool =
        {
            SensorChannel.Depth, SensorChannel.LeafScent, SensorChannel.LeafScentDirection,
            SensorChannel.Scent, SensorChannel.Chemical, SensorChannel.Flow,
        };

        /// <summary>A structural root with a link hanging off +x, and a nose on the link's far face.</summary>
        private static Genome WithANose(string noseType = CellTypeIds.Nose)
        {
            var g = new Genome { RootIndex = 0, AdultScale = 1f };
            g.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f };

            g.Nodes.Add(Node(new Float3(0.2f, 0.2f, 0.2f), CellTypeIds.Structural, JointType.Fixed));
            g.Nodes.Add(Node(new Float3(0.15f, 0.05f, 0.1f), CellTypeIds.Link, JointType.Hinge));
            g.Nodes.Add(Node(new Float3(0.1f, 0.05f, 0.05f), noseType, JointType.Fixed));
            g.Nodes[0].Edges.Add(Edge(1));
            g.Nodes[1].Edges.Add(Edge(2));
            return g;
        }

        private static MorphNode Node(Float3 half, string type, JointType joint) => new MorphNode
        {
            Dimensions = half,
            CellTypeId = type,
            ShapeId = ShapeIds.Box,
            JointType = joint,
            JointLimits = joint == JointType.Hinge ? new[] { new Float2(-1f, 1f) } : Array.Empty<Float2>(),
            Power = joint == JointType.Hinge ? 5f : 0f,
            RecursiveLimit = 1,
        };

        private static MorphEdge Edge(int child) => new MorphEdge
        {
            Child = child,
            ParentAnchor = new Float3(1f, 0f, 0f),
            ChildAnchor = new Float3(-1f, 0f, 0f),
            Orientation = Quat.Identity,
            Scale = Float3.One,
        };

        // ------------------------------------------------------------------ the type

        [Fact]
        public void TheNoseIsNotStandardAndIsASoftOrganThatEarnsNothing()
        {
            Assert.DoesNotContain(CellTypeIds.Nose, CellTypeRegistry.Standard.Ids());

            var nose = new NoseCell();
            Assert.Equal(0f, nose.AttackMax);
            Assert.Equal(0f, nose.IntakeMax);
            Assert.Equal(0f, nose.ProtectionMax);
            Assert.Equal(1f, nose.ToughnessMax);
            Assert.False(nose.AllowsJoint);
            Assert.False(nose.FixesLight);
            Assert.False(nose.FacingIntake);
            Assert.Equal(CellIntake.None.FromLight, nose.Acquire(new CellContext(1f, 1f, 1f, 100f, 100f)).FromLight);
            Assert.Equal(0f, nose.Acquire(new CellContext(1f, 1f, 1f, 100f, 100f)).FromPool);

            // A nose with a joint, or with a claw, is refused.
            Genome jointed = WithANose();
            jointed.Nodes[2].JointType = JointType.Hinge;
            jointed.Nodes[2].JointLimits = new[] { new Float2(-1f, 1f) };
            Assert.NotEmpty(jointed.Validate(WithNose()));

            Genome clawed = WithANose();
            clawed.Nodes[2].Attack = 0.5f;
            Assert.NotEmpty(clawed.Validate(WithNose()));

            Assert.Empty(WithANose().Validate(WithNose()));
            Assert.NotEmpty(WithANose().Validate(CellTypeRegistry.Standard));
        }

        [Fact]
        public void ItsNumbersSurviveTheConfigAndEachReachesTheHash()
        {
            var nose = new NoseCell(
                upkeepWattsPerCubicMetre: 3.5f, upkeepWattsPerSquareMetre: 0.7f, referenceAreaSquareMetres: 0.12f,
                leafNoise: 0.3f, leafBackground: 1.5f, carrionNoise: 4f, carrionBackground: 25f,
                chemicalNoise: 0.2f, chemicalBackground: 2f, facingIntake: true,
                intakeFullScaleMetresPerSecond: 0.4f) { BudWeight = 0.5f };
            var config = new RunConfig { CellTypes = WithNose(nose) };

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(config));
            var read = (NoseCell)back.CellTypes.Resolve(CellTypeIds.Nose);

            Assert.Equal(config.Hash(), back.Hash());
            Assert.Equal(nose.FullHashContribution(), read.FullHashContribution());
            Assert.Equal(0.7f, read.UpkeepWattsPerSquareMetre);
            Assert.True(read.FacingIntake);
            Assert.Equal(0.4f, read.IntakeFullScaleMetresPerSecond);

            // Each constructor argument moves the hash on its own.
            string reference = new NoseCell().HashContribution();
            var changed = new[]
            {
                new NoseCell(upkeepWattsPerCubicMetre: 4f), new NoseCell(upkeepWattsPerSquareMetre: 0.6f),
                new NoseCell(referenceAreaSquareMetres: 0.2f), new NoseCell(leafNoise: 0.3f),
                new NoseCell(leafBackground: 2f), new NoseCell(carrionNoise: 6f),
                new NoseCell(carrionBackground: 21f), new NoseCell(chemicalNoise: 0.3f),
                new NoseCell(chemicalBackground: 2f), new NoseCell(facingIntake: true),
                new NoseCell(intakeFullScaleMetresPerSecond: 0.5f),
            };
            foreach (NoseCell c in changed) Assert.NotEqual(reference, c.HashContribution());
            Assert.Equal(changed.Length, changed.Select(c => c.HashContribution()).Distinct().Count());

            // A table without the nose is not touched by any of this.
            Assert.NotEqual(new RunConfig().Hash(), config.Hash());
        }

        [Fact]
        public void BadNumbersAreRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NoseCell(upkeepWattsPerCubicMetre: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NoseCell(upkeepWattsPerSquareMetre: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NoseCell(referenceAreaSquareMetres: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NoseCell(leafNoise: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NoseCell(leafBackground: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NoseCell(carrionBackground: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NoseCell(intakeFullScaleMetresPerSecond: 0f));

            // Noise 0 is allowed: the nursery's arm with the noise off.
            Assert.Equal(0.0, new NoseCell(leafNoise: 0f).Sigma(NoseSmell.Leaf, 3.0, 0.1));
        }

        // ------------------------------------------------------------------ the price

        [Fact]
        public void ANosePaysByVolumeAndBySurfaceArea()
        {
            var nose = new NoseCell(upkeepWattsPerCubicMetre: 3f, upkeepWattsPerSquareMetre: 0.5f);
            var context = new CellContext(seconds: 2f, volume: 0.002f, surfaceArea: 0.1f);
            Assert.Equal((3f * 0.002f + 0.5f * 0.1f) * 2f, nose.Upkeep(context), 6);

            // The world's own bill carries the area: the same body under two area prices differs
            // by the price times the nose's surface.
            Phenotype body = Developer.Develop(WithANose(), cellTypes: WithNose());
            PhenotypePart part = body.Parts.Single(p => p.CellTypeId == CellTypeIds.Nose);

            float priced = Metabolism.StandingWatts(body, new RunConfig { CellTypes = WithNose(new NoseCell(upkeepWattsPerSquareMetre: 0.5f)) });
            float free = Metabolism.StandingWatts(body, new RunConfig { CellTypes = WithNose(new NoseCell(upkeepWattsPerSquareMetre: 0f)) });
            _out.WriteLine($"nose area {part.SurfaceArea:0.####} m2, standing {priced:0.######} W against {free:0.######} W");
            Assert.Equal(0.5f * part.SurfaceArea, priced - free, 5);
        }

        // ------------------------------------------------------------------ the noise law

        [Fact]
        public void FourTimesTheAreaHalvesTheNoiseAndTheBackgroundSetsWhereItGrows()
        {
            var nose = new NoseCell(leafNoise: 0.25f, leafBackground: 1f, referenceAreaSquareMetres: 0.1f);

            Assert.Equal(0.25, nose.Sigma(NoseSmell.Leaf, 0.0, 0.1), 7);
            Assert.Equal(0.125, nose.Sigma(NoseSmell.Leaf, 0.0, 0.4), 7);
            Assert.Equal(0.5, nose.Sigma(NoseSmell.Leaf, 0.0, 0.025), 7);

            // Above the background the noise grows with the signal: at c = 3·background, twice.
            Assert.Equal(0.5, nose.Sigma(NoseSmell.Leaf, 3.0, 0.1), 7);
            Assert.Equal(nose.Sigma(NoseSmell.Leaf, 0.0, 0.1), nose.Sigma(NoseSmell.Leaf, -5.0, 0.1));

            // The reading never goes below zero, and z = 0 reads the field.
            Assert.Equal(0.0, nose.Read(NoseSmell.Leaf, 0.1, 0.1, -3.0));
            Assert.Equal(0.7, nose.Read(NoseSmell.Leaf, 0.7, 0.1, 0.0));

            // Each smell keeps its own two.
            var split = new NoseCell(leafNoise: 1f, carrionNoise: 2f, chemicalNoise: 3f);
            Assert.NotEqual(split.Sigma(NoseSmell.Leaf, 0, 0.1), split.Sigma(NoseSmell.Carrion, 0, 0.1));
            Assert.NotEqual(split.Sigma(NoseSmell.Carrion, 0, 0.1), split.Sigma(NoseSmell.Chemical, 0, 0.1));

            // The direction form: 4σ / L.
            Assert.Equal(4.0 * 0.25 / 0.2, nose.GradientSigma(NoseSmell.Leaf, 0.0, 0.1, 0.2), 7);
            Assert.True(double.IsFinite(nose.GradientSigma(NoseSmell.Leaf, 0.0, 0.0, 0.0)));
        }

        [Fact]
        public void FacingIntakeWidensTheAreaOnlyWhenOnAndOnlyIntoTheWater()
        {
            Assert.Equal(0.1, new NoseCell().EffectiveArea(0.1, 0.3));

            var facing = new NoseCell(facingIntake: true, intakeFullScaleMetresPerSecond: 0.3f);
            Assert.Equal(0.2, facing.EffectiveArea(0.1, 0.3), 6);
            Assert.Equal(0.1, facing.EffectiveArea(0.1, -0.3));
            Assert.Equal(0.1, facing.EffectiveArea(0.1, 0.0));
        }

        // ------------------------------------------------------------------ the draw

        [Fact]
        public void TheDrawHasMeanZeroAndVarianceOneAndIsBounded()
        {
            const int n = 200000;
            double sum = 0, squares = 0, fourth = 0, most = 0;
            for (int i = 0; i < n; i++)
            {
                double z = NoseCell.Normal(NoseCell.SniffKey(42UL, i % 97, (ulong)(i / 97), i), i % 6);
                sum += z;
                squares += z * z;
                fourth += z * z * z * z;
                most = Math.Max(most, Math.Abs(z));
            }

            double mean = sum / n, variance = squares / n - mean * mean, kurtosis = fourth / n / (variance * variance);
            _out.WriteLine($"mean {mean:0.#####}, variance {variance:0.#####}, kurtosis {kurtosis:0.###}, largest {most:0.###}");

            Assert.True(Math.Abs(mean) < 0.01);
            Assert.True(Math.Abs(variance - 1.0) < 0.01);
            Assert.True(most <= 2.0 * Math.Sqrt(3.0));

            // Four uniforms: kurtosis 3 − 6/(5·4) = 2.7, close to a bell curve's 3.
            Assert.InRange(kurtosis, 2.65, 2.75);
        }

        [Fact]
        public void TheDrawIsAPureFunctionOfEachOfItsKeys()
        {
            ulong key = NoseCell.SniffKey(7UL, 12, 0xABCDEFUL, 1000);
            Assert.Equal(key, NoseCell.SniffKey(7UL, 12, 0xABCDEFUL, 1000));
            Assert.Equal(NoseCell.Normal(key, 0), NoseCell.Normal(key, 0));

            Assert.NotEqual(key, NoseCell.SniffKey(8UL, 12, 0xABCDEFUL, 1000));
            Assert.NotEqual(key, NoseCell.SniffKey(7UL, 13, 0xABCDEFUL, 1000));
            Assert.NotEqual(key, NoseCell.SniffKey(7UL, 12, 0xABCDEEUL, 1000));
            Assert.NotEqual(key, NoseCell.SniffKey(7UL, 12, 0xABCDEFUL, 1001));

            var draws = Enumerable.Range(0, 6).Select(d => NoseCell.Normal(key, d)).ToArray();
            Assert.Equal(6, draws.Distinct().Count());

            // Pinned, so a change to the arithmetic, which the card's kernels must match, is seen.
            _out.WriteLine($"key {key:X16}, z0 {draws[0]:R}");
            Assert.Equal(NoseCell.Normal(key, 0), PinnedSum(key, 0));
        }

        // The draw restated from its definition, the midpoints taken one by one.
        private static double PinnedSum(ulong key, int draw)
        {
            ulong h = Rng.SeedFor(key, (ulong)draw);
            double u = 0;
            for (int i = 0; i < 4; i++) u += (((h >> (16 * i)) & 0xFFFFUL) + 0.5) / 65536.0;
            return (u - 2.0) * Math.Sqrt(3.0);
        }

        // ------------------------------------------------------------------ the gate

        [Fact]
        public void UnderANoseTableOnlyNosesSmellAndEveryCopyKeepsIt()
        {
            Phenotype plain = Developer.Develop(WithANose(CellTypeIds.Structural));
            Assert.All(plain.Parts, p => Assert.True(p.Smells));

            Phenotype nosed = Developer.Develop(WithANose(), cellTypes: WithNose());
            Assert.Equal(new[] { false, false, true }, nosed.Parts.Select(p => p.Smells).ToArray());

            // A table with the nose gates a body that grew no nose: nothing smells.
            Phenotype blind = Developer.Develop(WithANose(CellTypeIds.Structural), cellTypes: WithNose());
            Assert.All(blind.Parts, p => Assert.False(p.Smells));

            Assert.Equal(new[] { false, false, true }, nosed.Scaled(0.5f).Parts.Select(p => p.Smells).ToArray());
        }

        [Fact]
        public void SmellPortsSitOnNosesAloneAndEveryOtherPortIsUnchanged()
        {
            Phenotype plain = Developer.Develop(WithANose(CellTypeIds.Structural));
            Phenotype nosed = Developer.Develop(WithANose(), cellTypes: WithNose());

            List<BrainPort> before = CentralWiring.Ports(plain, Pool);
            List<BrainPort> after = CentralWiring.Ports(nosed, Pool);

            // Without the nose's table every part carries every smell, as before.
            Assert.Equal(3 * (1 + 3 + 1 + 1), before.Count(p => p.Channel.IsSmell()));

            Assert.All(after.Where(p => p.Channel.IsSmell()), p => Assert.Equal(2, p.Part));
            Assert.Equal(1 + 3 + 1 + 1, after.Count(p => p.Channel.IsSmell()));

            // The senses that are not smell are the same ports with the same ids.
            Assert.Equal(
                before.Where(p => !p.Channel.IsSmell()).Select(p => p.Id),
                after.Where(p => !p.Channel.IsSmell()).Select(p => p.Id));

            // A stored link from a gated port no longer resolves, so it reads nothing.
            Dictionary<ulong, BrainPort> all = CentralWiring.AllPorts(nosed);
            Assert.DoesNotContain(BrainIds.Port(nosed.Parts[0].PathId, SensorChannel.LeafScent, 0), all.Keys);
            Assert.Contains(BrainIds.Port(nosed.Parts[2].PathId, SensorChannel.LeafScent, 0), all.Keys);
        }

        [Fact]
        public void TwoNosesCarryTwoSetsOfPorts()
        {
            Genome g = WithANose();
            g.Nodes[0].Edges.Add(new MorphEdge
            {
                Child = 2, ParentAnchor = new Float3(0f, 1f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity, Scale = Float3.One,
            });

            Phenotype body = Developer.Develop(g, cellTypes: WithNose());
            int[] noses = body.Parts.Where(p => p.CellTypeId == CellTypeIds.Nose).Select(p => p.Index).ToArray();
            Assert.Equal(2, noses.Length);

            List<BrainPort> leaf = CentralWiring.Ports(body, Pool).Where(p => p.Channel == SensorChannel.LeafScent).ToList();
            Assert.Equal(noses, leaf.Select(p => p.Part).ToArray());
            Assert.Equal(2, leaf.Select(p => p.Id).Distinct().Count());
        }
    }
}
