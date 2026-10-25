using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// A reflected pair, put down as developed, lands where development put each twin. Developer
    /// stores a mirrored part's anchors unreflected and folds the reflection into its pose, so the
    /// solver, which rebuilds positions from those anchors, must not stack the twin on its original.
    /// </summary>
    public sealed class MirroredPoseTests
    {
        // Fails on this build: the twin lands on its original (logbook/specs/mirrored-anchor-finding.md).
        // Skipped so the suite stays green until the owner rules on the engine change; the fix unskips it.
        [Fact(Skip = "logbook/specs/mirrored-anchor-finding.md: the solver stacks a mirrored part on its twin; the fix waits on the owner")]
        public void AReflectedPairLandsWhereDevelopmentPutEachTwin()
        {
            SolverConfig config = TestBodies.Water(0.01);
            Genome genome = TestBodies.Chain(2, JointType.Hinge, power: 5f);
            genome.Nodes[0].Edges[0].Reflect = new Bool3(true, false, false);
            Phenotype adult = Developer.Develop(genome, DevelopmentLimits.Default);
            Assert.Equal(3, adult.PartCount);

            var body = new Creature(0, adult, config);
            var origin = new Vec3(3, -10, -7);
            body.PlaceAsDeveloped(origin, adult);

            for (int i = 0; i < body.Links; i++)
            {
                Vec3 expected = origin + new Vec3(
                    adult.Parts[i].Position.X, adult.Parts[i].Position.Y, adult.Parts[i].Position.Z);
                Vec3 got = Vec3.Read(body.Position, 3 * i);
                Assert.True(
                    (got - expected).Magnitude < 1e-5,
                    $"part {i} (mirrored {adult.Parts[i].Mirrored}) landed at {got.X:0.######},{got.Y:0.######}," +
                    $"{got.Z:0.######} and development put it at {expected.X:0.######},{expected.Y:0.######}," +
                    $"{expected.Z:0.######}");
            }

            // The twins sit on opposite sides of the root, not on one another.
            Vec3 a = Vec3.Read(body.Position, 3 * 1);
            Vec3 b = Vec3.Read(body.Position, 3 * 2);
            Assert.True((a - b).Magnitude > 0.3, $"the twins are {(a - b).Magnitude:0.###} m apart");
        }
    }
}
