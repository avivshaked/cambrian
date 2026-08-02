using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// DESIGN.md §7: every evaluation is defined by (genome, seed, configHash) with no
    /// ambient randomness. That claim is only worth anything if the generator is stable.
    /// </summary>
    public class RngTests
    {
        [Fact]
        public void SameSeedProducesSameSequence()
        {
            var a = new Rng(12345);
            var b = new Rng(12345);

            for (int i = 0; i < 1000; i++)
            {
                Assert.Equal(a.NextUInt(), b.NextUInt());
            }
        }

        [Fact]
        public void DifferentSeedsDiverge()
        {
            var a = new Rng(1);
            var b = new Rng(2);

            int identical = 0;
            for (int i = 0; i < 100; i++)
            {
                if (a.NextUInt() == b.NextUInt()) identical++;
            }

            Assert.True(identical < 5, $"{identical}/100 draws matched across different seeds.");
        }

        [Fact]
        public void SequencesAreIndependentStreams()
        {
            var a = new Rng(7, sequence: 1);
            var b = new Rng(7, sequence: 2);

            Assert.NotEqual(a.NextUInt(), b.NextUInt());
        }

        [Fact]
        public void NextFloatStaysInUnitInterval()
        {
            var rng = new Rng(99);
            for (int i = 0; i < 10000; i++)
            {
                float v = rng.NextFloat();
                Assert.InRange(v, 0f, 0.9999999f);
            }
        }

        [Fact]
        public void IntRangeCoversEveryValueAndNeverExceedsBound()
        {
            var rng = new Rng(4);
            var seen = new HashSet<int>();

            for (int i = 0; i < 5000; i++)
            {
                int v = rng.Range(6);
                Assert.InRange(v, 0, 5);
                seen.Add(v);
            }

            Assert.Equal(6, seen.Count);
        }

        [Fact]
        public void GaussianIsCentredWithUnitVariance()
        {
            var rng = new Rng(2024);
            const int n = 200000;

            double sum = 0, sumSq = 0;
            for (int i = 0; i < n; i++)
            {
                float v = rng.Gaussian();
                sum += v;
                sumSq += (double)v * v;
            }

            double mean = sum / n;
            double variance = sumSq / n - mean * mean;

            Assert.True(System.Math.Abs(mean) < 0.02, $"mean was {mean}");
            Assert.True(System.Math.Abs(variance - 1.0) < 0.05, $"variance was {variance}");
        }

        [Fact]
        public void ForkDoesNotRepeatTheParentStream()
        {
            var parent = new Rng(555);
            Rng child = parent.Fork(3);

            uint p = parent.NextUInt();
            uint c = child.NextUInt();

            Assert.NotEqual(p, c);
        }

        [Fact]
        public void RotationsAreUnitQuaternions()
        {
            var rng = new Rng(31);
            for (int i = 0; i < 1000; i++)
            {
                Quat q = rng.NextRotation();
                Fixtures.AssertClose(1f, q.SqrMagnitude, 1e-3f);
            }
        }
    }
}
