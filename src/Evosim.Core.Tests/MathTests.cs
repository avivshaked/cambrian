using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    public class MathTests
    {
        [Fact]
        public void TranslateMovesAPoint()
        {
            Mat4 m = Mat4.Translate(new Float3(1f, 2f, 3f));
            Fixtures.AssertClose(new Float3(1f, 2f, 3f), m.MultiplyPoint(Float3.Zero));
        }

        [Fact]
        public void TranslateLeavesADirectionAlone()
        {
            Mat4 m = Mat4.Translate(new Float3(1f, 2f, 3f));
            Fixtures.AssertClose(new Float3(1f, 0f, 0f), m.MultiplyVector(new Float3(1f, 0f, 0f)));
        }

        [Fact]
        public void CompositionAppliesRightmostFirst()
        {
            // Translate then rotate about Z by 90 degrees: the point should end up rotated.
            Mat4 rot = Mat4.Rotate(Quat.FromAxisAngle(new Float3(0f, 0f, 1f), (float)System.Math.PI / 2f));
            Mat4 trs = Mat4.Translate(new Float3(1f, 0f, 0f));

            Fixtures.AssertClose(new Float3(0f, 1f, 0f), (rot * trs).MultiplyPoint(Float3.Zero));
            Fixtures.AssertClose(new Float3(1f, 0f, 0f), (trs * rot).MultiplyPoint(Float3.Zero));
        }

        [Fact]
        public void QuaternionRotationMatchesMatrixRotation()
        {
            var rng = new Rng(88);
            for (int i = 0; i < 200; i++)
            {
                Quat q = rng.NextRotation();
                Float3 v = rng.NextFloat3(-2f, 2f);

                Fixtures.AssertClose(q.Rotate(v), Mat4.Rotate(q).MultiplyVector(v), 1e-3f);
            }
        }

        [Fact]
        public void DecomposeRoundTripsPositionRotationAndScale()
        {
            var rng = new Rng(1234);
            for (int i = 0; i < 200; i++)
            {
                Float3 t = rng.NextFloat3(-5f, 5f);
                Quat r = rng.NextRotation();
                Float3 s = new Float3(rng.Range(0.2f, 3f), rng.Range(0.2f, 3f), rng.Range(0.2f, 3f));

                Mat4.Trs(t, r, s).Decompose(out Float3 dt, out Quat dr, out Float3 ds, out bool mirrored);

                Assert.False(mirrored);
                Fixtures.AssertClose(t, dt, 1e-3f);
                Fixtures.AssertClose(s, ds, 1e-3f);

                // q and -q are the same rotation, so compare their action on a vector.
                Float3 probe = new Float3(0.3f, -0.7f, 0.55f);
                Fixtures.AssertClose(r.Rotate(probe), dr.Rotate(probe), 1e-3f);
            }
        }

        [Fact]
        public void MirroringFlipsTheDeterminantAndIsReported()
        {
            Mat4 m = Mat4.Mirror(new Bool3(true, false, false)) * Mat4.Translate(new Float3(1f, 0f, 0f));

            Assert.True(m.Determinant3 < 0f);

            m.Decompose(out Float3 pos, out Quat rot, out Float3 scale, out bool mirrored);

            Assert.True(mirrored);
            Fixtures.AssertClose(new Float3(-1f, 0f, 0f), pos);
            Assert.True(scale.X < 0f, "a mirrored frame should report negative scale on the flipped axis");

            // The recovered rotation must be proper, not a reflection.
            Fixtures.AssertClose(1f, Mat4.Rotate(rot).Determinant3, 1e-3f);
        }

        [Fact]
        public void TwoMirrorsCancelBackToAProperFrame()
        {
            Mat4 m = Mat4.Mirror(new Bool3(true, true, false));
            Assert.True(m.Determinant3 > 0f);

            m.Decompose(out _, out _, out _, out bool mirrored);
            Assert.False(mirrored);
        }

        [Theory]
        [InlineData(false, false, false, 1)]
        [InlineData(true, false, false, 2)]
        [InlineData(true, true, false, 4)]
        [InlineData(true, true, true, 8)]
        public void ReflectionFlagsProduceTwoFourOrEightCopies(bool x, bool y, bool z, int expected)
        {
            // [K12 §2.1, p.3]: "if one, two or three reflection flags are enabled, two, four
            // or eight mirrored copies of a child node are created in the phenotype graph."
            var flags = new Bool3(x, y, z);

            Assert.Equal(expected, flags.CopyCount);
            Assert.Equal(expected, flags.MirrorCombinations().Count());
        }

        [Fact]
        public void MirrorCombinationsAreDistinctAndStartUnmirrored()
        {
            List<Bool3> combos = new Bool3(true, true, true).MirrorCombinations().ToList();

            Assert.Equal(Bool3.None, combos[0]);
            Assert.Equal(8, combos.Distinct().Count());
        }

        [Fact]
        public void BoxVolumeUsesFullExtentsNotHalfExtents()
        {
            // Half-extents of 0.5 describe a unit cube.
            Fixtures.AssertClose(1f, new Float3(0.5f, 0.5f, 0.5f).BoxVolume);
        }

        [Fact]
        public void JointDofCountsMatchTheirTypes()
        {
            Assert.Equal(0, JointType.Fixed.DofCount());
            Assert.Equal(1, JointType.Hinge.DofCount());
            Assert.Equal(1, JointType.Twist.DofCount());
            Assert.Equal(2, JointType.HingeTwist.DofCount());
            Assert.Equal(2, JointType.TwistHinge.DofCount());
            Assert.Equal(2, JointType.Universal.DofCount());
            Assert.Equal(3, JointType.Spherical.DofCount());
        }
    }
}
