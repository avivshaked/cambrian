using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D110's harness read (<c>Simulation.Exposure</c> in the farm): a link's exposure factor
    /// taken from row 1 of its world matrix, and the world's up carried into the body's own frame,
    /// checked against the same quantities taken from the link's quaternion.
    /// </summary>
    /// <remarks>
    /// The farm's method is private and one line of arithmetic a link; what can go wrong in it is
    /// the convention — a row where a column was meant reads the link's own y axis in the world
    /// instead of the world's up on the link's axes, which gives a plausible factor and the wrong
    /// one. This pins the convention the method relies on, on a posed body of the solver.
    /// </remarks>
    public sealed class ExposureReadTests
    {
        private static Quat ToCore(QuatD q) => new Quat((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);

        [Fact]
        public void RowOneOfALinksMatrixIsTheUpItsFactorIsTakenAgainst()
        {
            SolverConfig config = TestBodies.Water(0.01);
            Genome genome = TestBodies.Chain(4, JointType.Universal, power: 5f, limit: 0.8f);
            Phenotype adult = Developer.Develop(genome, DevelopmentLimits.Default);

            var body = new Creature(0, adult, config);
            body.PlaceAsDeveloped(new Vec3(0, -10, 0), adult);
            body.BaseRotation = QuatD.FromAxisAngle(new Vec3(0.3, 0.8, -0.52).Normalized, 1.1);
            for (int d = 0; d < body.Dof; d++) body.Q[d] = 0.3 * (d % 3 - 1);
            Kinematics.Poses(body);

            double[] m = body.RotationMatrix;

            for (int i = 0; i < body.Links; i++)
            {
                PhenotypePart part = adult.Parts[i];
                int at = 9 * i + 3;

                float fromRow = part.ExposureFactor(m[at], m[at + 1], m[at + 2]);
                float fromQuat = part.ExposureFactor(ToCore(QuatD.Read(body.Rotation, 4 * i)));

                Assert.True(
                    System.Math.Abs(fromRow - fromQuat) < 1e-5f,
                    $"link {i}: {fromRow} from the matrix row, {fromQuat} from the quaternion");
            }

            // The world's up in the developer's frame, as the harness builds it: the root link's
            // row 1 turned by the root part's developed rotation. Against the definition: the
            // body frame stands at rootWorld * conj(rootPart), and up in it is that rotation's
            // inverse applied to the world's up.
            Float3 upOnRoot = new Float3((float)m[3], (float)m[4], (float)m[5]);
            Float3 harness = adult.Parts[0].Rotation.Rotate(upOnRoot);

            Quat rootWorld = ToCore(QuatD.Read(body.Rotation, 0));
            Quat rootPart = adult.Parts[0].Rotation;
            Quat bodyFrame = rootWorld * new Quat(-rootPart.X, -rootPart.Y, -rootPart.Z, rootPart.W);
            Float3 definition = new Quat(-bodyFrame.X, -bodyFrame.Y, -bodyFrame.Z, bodyFrame.W)
                .Rotate(new Float3(0f, 1f, 0f));

            Assert.True(System.Math.Abs(harness.X - definition.X) < 1e-5f, $"x {harness.X} against {definition.X}");
            Assert.True(System.Math.Abs(harness.Y - definition.Y) < 1e-5f, $"y {harness.Y} against {definition.Y}");
            Assert.True(System.Math.Abs(harness.Z - definition.Z) < 1e-5f, $"z {harness.Z} against {definition.Z}");
        }
    }
}
