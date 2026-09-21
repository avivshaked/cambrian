using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Where a body lands when it is put down, and why it is not the origin.
    /// </summary>
    /// <remarks>
    /// <c>PhenotypeBuilder.Build</c> parents every part under one object placed at <c>start</c>
    /// with no rotation of its own, so part <c>i</c> lands at <c>start + Parts[i].Position</c>
    /// wearing <c>Parts[i].Rotation</c> — the pose development gave it, translated. The spike's
    /// bench stood the root upright at <c>start</c> instead, which moves and turns the whole
    /// body. That is invisible in a vacuum and is not invisible here:
    /// <see cref="SensorChannel.OrientationUp"/> and <see cref="SensorChannel.Depth"/> both read
    /// it on the first step, and both feed the brain.
    /// </remarks>
    public sealed class BodyPoseTests
    {
        [Fact]
        public void PlacingAsDevelopedReproducesEveryPartsDevelopedPose()
        {
            SolverConfig config = TestBodies.Water(0.01);
            Genome genome = TestBodies.Chain(4, JointType.Universal, power: 5f, limit: 0.8f);
            Phenotype adult = Developer.Develop(genome, DevelopmentLimits.Default);

            var body = new Creature(0, adult, config);
            var origin = new Vec3(3, -10, -7);
            body.PlaceAsDeveloped(origin, adult);

            for (int i = 0; i < body.Links; i++)
            {
                Vec3 expected = origin + new Vec3(
                    adult.Parts[i].Position.X, adult.Parts[i].Position.Y, adult.Parts[i].Position.Z);
                Vec3 got = Vec3.Read(body.Position, 3 * i);

                // 1e-5 and not machine precision: development stores a pose in float and the
                // kinematics rebuild it in double from the anchors, so the two agree to about
                // 1e-7. A placement error is centimetres at least, so the gap is unambiguous.

                Assert.True(
                    (got - expected).Magnitude < 1e-5,
                    $"part {i} landed at {got.X:0.######},{got.Y:0.######},{got.Z:0.######} " +
                    $"and development put it at {expected.X:0.######},{expected.Y:0.######}," +
                    $"{expected.Z:0.######}");

                QuatD want = QuatD.From(adult.Parts[i].Rotation);
                QuatD have = QuatD.Read(body.Rotation, 4 * i);

                // Up to sign: a quaternion and its negative are one rotation.
                double dot = want.X * have.X + want.Y * have.Y + want.Z * have.Z + want.W * have.W;
                Assert.True(
                    1.0 - System.Math.Abs(dot) < 1e-6,
                    $"part {i} is not wearing the attitude development gave it (dot {dot:0.#########})");
            }
        }

    }
}
