using System;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>The genome distance D057's species boundary is measured against.</summary>
    public class SpeciesDistanceTests
    {
        [Fact]
        public void IdenticalGenomesAreZeroDistanceApart()
        {
            Genome g = Fixtures.SelfLoopSpine(3);

            float distance = SpeciesDistance.Between(g, g.Clone(), 1f, 1f, 1f, 1f);

            Assert.Equal(0f, distance);
        }

        [Fact]
        public void TheDistanceIsSymmetricBetweenTwoGenomes()
        {
            Genome a = Fixtures.SelfLoopSpine(3);
            Genome b = a.Clone();
            b.Nodes[0].Dimensions = new Float3(0.9f, 0.5f, 0.5f);
            b.Nodes[0].CellTypeId = CellTypeIds.Photosynthetic;

            float ab = SpeciesDistance.Between(a, b, 1f, 1f, 1f, 1f);
            float ba = SpeciesDistance.Between(b, a, 1f, 1f, 1f, 1f);

            Assert.Equal(ab, ba);
        }

        [Fact]
        public void TheDistanceIsDeterministicAcrossRepeatedCalls()
        {
            Genome a = Fixtures.SelfLoopSpine(3);
            Genome b = Fixtures.SelfLoopSpine(3, segmentScale: 0.8f);

            float first = SpeciesDistance.Between(a, b, 1f, 1f, 1f, 1f);

            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(first, SpeciesDistance.Between(a, b, 1f, 1f, 1f, 1f));
            }
        }

        [Fact]
        public void ACellTypeChangeAloneCanExceedTheThresholdWhenItsWeightIsAtOrAboveTheta()
        {
            const float theta = 0.5f;

            Genome a = Fixtures.SingleBox();
            a.Nodes[0].CellTypeId = CellTypeIds.Structural;

            Genome b = a.Clone();
            b.Nodes[0].CellTypeId = CellTypeIds.Photosynthetic;

            // Every other term zeroed, so only the cell-type term can contribute — isolating
            // the one deliberate commitment D057 makes: a single node changing trade returns
            // exactly 1.0 from the cell-type term, so a weight at or above theta exceeds it.
            float distance = SpeciesDistance.Between(
                a, b, cellTypeWeight: theta * 2f, topologyWeight: 0f, parameterWeight: 0f,
                brainWeight: 0f);

            Assert.True(distance > theta, $"{distance} did not exceed theta={theta}");
            Fixtures.AssertClose(theta * 2f, distance, 1e-4f);
        }

        [Fact]
        public void ABrainOnlyDifferenceIsInvisibleAtDefaultWeightsAndVisibleOnceTheBrainWeightIsRaised()
        {
            Genome a = Fixtures.SingleBox();
            a.Nodes[0].Neurons = new[]
            {
                new NeuronDef
                {
                    Op = NeuronOp.Sin,
                    Frequency = 1f,
                    Amplitude = 1f,
                    Inputs = Array.Empty<NeuronInput>(),
                },
            };

            Genome b = a.Clone();
            // Same node, same shape, same op — a pure oscillator-weight drift and nothing else.
            b.Nodes[0].Neurons[0].Amplitude = 0.4f;

            var defaults = new RunConfig();
            Assert.Equal(0f, defaults.SpeciesBrainWeight);

            float atDefault = SpeciesDistance.Between(
                a, b, defaults.SpeciesCellTypeWeight, defaults.SpeciesTopologyWeight,
                defaults.SpeciesParameterWeight, defaults.SpeciesBrainWeight);

            Assert.Equal(0f, atDefault);

            float withBrainWeight = SpeciesDistance.Between(
                a, b, defaults.SpeciesCellTypeWeight, defaults.SpeciesTopologyWeight,
                defaults.SpeciesParameterWeight, brainWeight: 1f);

            Assert.True(
                withBrainWeight > 0f,
                "raising the brain weight from 0 should surface a brain-only difference");
        }
    }
}
