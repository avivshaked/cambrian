using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Size-dependent buoyancy — D064, <see cref="BuoyancyModel.ExcessDensityFactor"/>.
    /// </summary>
    public class BuoyancyModelTests
    {
        private readonly ITestOutputHelper _output;

        public BuoyancyModelTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void ZeroNeutralVolumeIsExactlyOne()
        {
            // The off state has to be *exactly* 1 and not 0.9999998: the apply site multiplies
            // the excess density by this, so anything but 1 makes every pre-D064 run
            // irreproducible by a rounding error nobody would ever look for.
            Assert.Equal(1f, BuoyancyModel.ExcessDensityFactor(0f, 0f));
            Assert.Equal(1f, BuoyancyModel.ExcessDensityFactor(1e-6f, 0f));
            Assert.Equal(1f, BuoyancyModel.ExcessDensityFactor(0.05f, 0f));
            Assert.Equal(1f, BuoyancyModel.ExcessDensityFactor(1000f, 0f));
            Assert.Equal(1f, BuoyancyModel.ExcessDensityFactor(-1f, 0f));

            // Negative is the same off state, not an inverted rule.
            Assert.Equal(1f, BuoyancyModel.ExcessDensityFactor(0.05f, -0.01f));
        }

        [Fact]
        public void AtOrBelowTheNeutralVolumeIsNeutral()
        {
            const float v0 = 0.004f;

            Assert.Equal(0f, BuoyancyModel.ExcessDensityFactor(v0, v0));
            Assert.Equal(0f, BuoyancyModel.ExcessDensityFactor(v0 / 2f, v0));
            Assert.Equal(0f, BuoyancyModel.ExcessDensityFactor(v0 / 1000f, v0));
            Assert.Equal(0f, BuoyancyModel.ExcessDensityFactor(0f, v0));
            Assert.Equal(0f, BuoyancyModel.ExcessDensityFactor(-1f, v0));
        }

        [Fact]
        public void EightTimesTheNeutralVolumeIsThreeQuarters()
        {
            // (1/8)^(2/3) = 1/4, so the factor is 3/4 exactly. The one point on the curve with a
            // closed form, and the anchor the knob is calibrated against.
            foreach (float v0 in new[] { 1e-4f, 0.004f, 0.05f, 1f })
            {
                float f = BuoyancyModel.ExcessDensityFactor(8f * v0, v0);
                _output.WriteLine($"V0={v0}: factor(8·V0) = {f:0.########}");
                Assert.Equal(0.75f, f, 6);
            }
        }

        [Fact]
        public void MonotoneNonDecreasingInVolume()
        {
            const float v0 = 0.004f;
            float previous = -1f;

            for (int i = 0; i <= 400; i++)
            {
                // Below V0 through to 100x it, so the flat neutral region and the rising region
                // are both walked.
                float v = v0 * (0.1f + i * 0.25f);
                float f = BuoyancyModel.ExcessDensityFactor(v, v0);

                Assert.InRange(f, 0f, 1f);
                Assert.True(f >= previous,
                    $"factor fell from {previous} to {f} at V={v} (V0={v0})");
                previous = f;
            }
        }

        [Fact]
        public void ApproachesOneForALargeBody()
        {
            const float v0 = 0.004f;

            // Converges to today's constant, which is what makes this a refinement of §5.2 rather
            // than a different rule: a big enough body sinks exactly as it did before D064.
            // The ratio decides it: at 10^k times V0 the shortfall is 10^(-2k/3), so 1e3 gives
            // 0.99 and 1e6 gives 0.9999 exactly — the thresholds are one decade of shortfall
            // looser than the closed form, not round numbers.
            Assert.True(BuoyancyModel.ExcessDensityFactor(1e3f * v0, v0) > 0.98f);
            Assert.True(BuoyancyModel.ExcessDensityFactor(1e6f * v0, v0) > 0.999f);
            Assert.True(BuoyancyModel.ExcessDensityFactor(1e9f * v0, v0) > 0.99999f);
            Assert.True(BuoyancyModel.ExcessDensityFactor(1e9f * v0, v0) <= 1f);
        }

        [Fact]
        public void ScaleFreeInTheRatio()
        {
            // The rule depends on V/V0 only, so the same ratio gives the same factor whatever the
            // absolute size — which is what lets the knob be read as "the volume that is neutral"
            // rather than as a second density.
            float a = BuoyancyModel.ExcessDensityFactor(0.02f, 0.005f);
            float b = BuoyancyModel.ExcessDensityFactor(20f, 5f);

            _output.WriteLine($"factor(4·V0) = {a:0.########} / {b:0.########}");
            Assert.Equal(a, b, 6);
        }
    }
}
