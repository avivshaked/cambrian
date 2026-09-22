using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// One step of a hand-built three-link chain, against the same step computed a completely
    /// different way: a maximal-coordinate Newton-Euler system with the joints as explicit
    /// constraint forces, assembled and solved densely inside this test.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this and not a closed form.</b> A three-link floating chain with hinges at
    /// arbitrary angles has no closed-form acceleration worth writing down, and a check against
    /// a second implementation of the same recursion would agree with a sign error in either.
    /// The system below shares no line with <see cref="Aba"/>: eighteen body accelerations and
    /// ten Lagrange multipliers, five per hinge (a force at the anchor and a torque in the plane
    /// the hinge locks), with the anchors' accelerations matched and the relative angular
    /// acceleration held on the free axis. If the articulated-body recursion has a sign wrong
    /// anywhere — in the motion subspace, in the bias term, in the inertia transform — the two
    /// disagree.
    /// </para>
    /// </remarks>
    public sealed class ChainAgainstConstraintSolveTests
    {
        private readonly ITestOutputHelper _out;

        public ChainAgainstConstraintSolveTests(ITestOutputHelper output) => _out = output;

        [Fact]
        public void OneStepOfAThreeLinkChainMatchesAConstraintSolve()
        {
            SolverConfig config = TestBodies.Vacuum(0.001);

            Creature body = TestBodies.Build(
                TestBodies.Chain(3, JointType.Hinge, power: 5f, limit: 3.0f, neurons: false), config);

            // A state with nothing zero in it: the chain is bent, flexing, spinning and moving.
            body.PlaceAt(new Vec3(0.3, -2.0, 0.7),
                QuatD.FromAxisAngle(new Vec3(0.3, 0.8, -0.5), 0.9));

            body.Q[0] = 0.41;
            body.Q[1] = -0.73;
            body.Qd[0] = 1.7;
            body.Qd[1] = -2.3;
            Vec3.Write(body.Spin, 0, new Vec3(0.6, -1.1, 0.35));
            Vec3.Write(body.Velocity, 0, new Vec3(-0.4, 0.25, 0.9));

            Kinematics.Refresh(body);

            // External wrenches on every link, and a joint torque on every degree of freedom.
            var externalTorque = new[]
            {
                new Vec3(0.11, -0.27, 0.42),
                new Vec3(-0.35, 0.18, 0.09),
                new Vec3(0.22, 0.31, -0.16),
            };
            var externalForce = new[]
            {
                new Vec3(1.3, -0.7, 0.5),
                new Vec3(-0.9, 2.1, 0.3),
                new Vec3(0.4, -1.4, -2.2),
            };

            for (int i = 0; i < 3; i++)
            {
                Vec3.Write(body.Fext, 6 * i, externalTorque[i]);
                Vec3.Write(body.Fext, 6 * i + 3, externalForce[i]);
            }

            var tau = new[] { 0.37, -0.82 };
            body.Tau[0] = tau[0];
            body.Tau[1] = tau[1];

            Aba.Solve(body);

            var solverAngular = new Vec3[3];
            var solverLinear = new Vec3[3];
            for (int i = 0; i < 3; i++)
            {
                solverAngular[i] = Vec3.Read(body.Acc, 6 * i);
                solverLinear[i] = Vec3.Read(body.Acc, 6 * i + 3);
            }
            var solverJoint = new[] { body.Tau[0], body.Tau[1] };

            Independent(body, externalTorque, externalForce, tau,
                out Vec3[] checkAngular, out Vec3[] checkLinear, out double[] checkJoint);

            for (int i = 0; i < 3; i++)
            {
                _out.WriteLine($"link {i} dw/dt  aba {solverAngular[i]}  check {checkAngular[i]}");
                _out.WriteLine($"link {i} du/dt  aba {solverLinear[i]}  check {checkLinear[i]}");
            }
            _out.WriteLine($"joint accelerations  aba [{solverJoint[0]:0.######}, {solverJoint[1]:0.######}]  " +
                           $"check [{checkJoint[0]:0.######}, {checkJoint[1]:0.######}]");

            for (int i = 0; i < 3; i++)
            {
                Close(checkAngular[i], solverAngular[i], $"link {i} angular acceleration");
                Close(checkLinear[i], solverLinear[i], $"link {i} linear acceleration");
            }

            for (int j = 0; j < 2; j++)
            {
                double scale = System.Math.Max(1.0, System.Math.Abs(checkJoint[j]));
                Assert.True(System.Math.Abs(checkJoint[j] - solverJoint[j]) < 1e-8 * scale,
                    $"joint {j}: {solverJoint[j]} against {checkJoint[j]}");
            }
        }

        /// <summary>
        /// The same idea for the three-degree joint, where the check is cleaner still: a
        /// spherical joint constrains one point and nothing else, so the system below has three
        /// multipliers and no rotational constraint at all — it never touches the motion
        /// subspace the solver builds, which is exactly the part a three-degree joint gets wrong
        /// if it gets anything wrong.
        /// </summary>
        [Fact]
        public void OneStepOfASphericalJointMatchesAConstraintSolve()
        {
            SolverConfig config = TestBodies.Vacuum(0.001);

            Creature body = TestBodies.Build(
                TestBodies.Chain(2, JointType.Spherical, power: 5f, limit: 3.0f, neurons: false), config);

            body.PlaceAt(new Vec3(-0.6, -4.0, 0.2),
                QuatD.FromAxisAngle(new Vec3(-0.2, 0.5, 0.84), 1.35));

            // A ball joint's configuration is its quaternion; the three angles are read off it.
            var turn = new Vec3(0.55, -0.92, 0.31);
            QuatD.Write(body.BallRotation, 4,
                QuatD.FromAxisAngle(turn, turn.Magnitude));
            Vec3 angles = QuatD.Read(body.BallRotation, 4).RotationVector();
            body.Q[0] = angles.X;
            body.Q[1] = angles.Y;
            body.Q[2] = angles.Z;

            body.Qd[0] = -1.4;
            body.Qd[1] = 2.6;
            body.Qd[2] = 0.8;
            Vec3.Write(body.Spin, 0, new Vec3(-0.5, 0.9, 1.3));
            Vec3.Write(body.Velocity, 0, new Vec3(0.7, -0.3, -0.6));

            Kinematics.Refresh(body);

            var externalTorque = new[] { new Vec3(0.4, -0.15, 0.23), new Vec3(-0.22, 0.51, -0.34) };
            var externalForce = new[] { new Vec3(-1.1, 0.6, 1.8), new Vec3(0.75, -2.3, 0.44) };

            for (int i = 0; i < 2; i++)
            {
                Vec3.Write(body.Fext, 6 * i, externalTorque[i]);
                Vec3.Write(body.Fext, 6 * i + 3, externalForce[i]);
            }

            Aba.Solve(body);

            var solverAngular = new[] { Vec3.Read(body.Acc, 0), Vec3.Read(body.Acc, 6) };
            var solverLinear = new[] { Vec3.Read(body.Acc, 3), Vec3.Read(body.Acc, 9) };

            // Fifteen unknowns: two bodies' accelerations and the force at the ball.
            const int n = 15;
            var m = new double[n * (n + 1)];

            var inertia = new Mat3[2];
            for (int i = 0; i < 2; i++)
            {
                inertia[i] = Mat3.RotateDiagonal(
                    Mat3.Read(body.RotationMatrix, 9 * i), Vec3.Read(body.InertiaLocal, 3 * i));

                Vec3 spin = Vec3.Read(body.Spin, 3 * i);
                PutBlock(m, n, 6 * i, 6 * i, inertia[i]);
                Rhs(m, n, 6 * i, externalTorque[i] - Vec3.Cross(spin, inertia[i] * spin));

                PutBlock(m, n, 6 * i + 3, 6 * i + 3,
                    Mat3.Diagonal(body.Mass[i], body.Mass[i], body.Mass[i]));
                Rhs(m, n, 6 * i + 3, externalForce[i]);
            }

            QuatD rotation = QuatD.Read(body.Rotation, 4);
            Vec3 anchor = Vec3.Read(body.Position, 3) +
                          rotation.Rotate(Vec3.Read(body.ChildAnchor, 3));

            Vec3 toChild = anchor - Vec3.Read(body.Position, 3);
            Vec3 toParent = anchor - Vec3.Read(body.Position, 0);

            AddBlock(m, n, 6, 12, Mat3.Skew(toChild) * -1.0);      // child rotational: -[r]x F
            AddBlock(m, n, 0, 12, Mat3.Skew(toParent));            // parent rotational: +[r]x F
            AddBlock(m, n, 9, 12, Mat3.Identity * -1.0);           // child translational: -F
            AddBlock(m, n, 3, 12, Mat3.Identity);                  // parent translational: +F

            Vec3 childSpin = Vec3.Read(body.Spin, 3);
            Vec3 parentSpin = Vec3.Read(body.Spin, 0);

            AddBlock(m, n, 12, 6, Mat3.Skew(toChild) * -1.0);
            AddBlock(m, n, 12, 9, Mat3.Identity);
            AddBlock(m, n, 12, 0, Mat3.Skew(toParent));
            AddBlock(m, n, 12, 3, Mat3.Identity * -1.0);
            AddRhs(m, n, 12,
                Vec3.Cross(parentSpin, Vec3.Cross(parentSpin, toParent)) -
                Vec3.Cross(childSpin, Vec3.Cross(childSpin, toChild)));

            double[] x = SolveDense(m, n);

            var checkAngular = new[]
            {
                new Vec3(x[0], x[1], x[2]), new Vec3(x[6], x[7], x[8]),
            };
            var checkLinear = new[]
            {
                new Vec3(x[3], x[4], x[5]), new Vec3(x[9], x[10], x[11]),
            };

            for (int i = 0; i < 2; i++)
            {
                _out.WriteLine($"link {i} dw/dt  aba {solverAngular[i]}  check {checkAngular[i]}");
                _out.WriteLine($"link {i} du/dt  aba {solverLinear[i]}  check {checkLinear[i]}");

                Close(checkAngular[i], solverAngular[i], $"link {i} angular acceleration");
                Close(checkLinear[i], solverLinear[i], $"link {i} linear acceleration");
            }
        }

        private static void Close(Vec3 expected, Vec3 actual, string what)
        {
            double scale = System.Math.Max(1.0, expected.Magnitude);
            double error = (expected - actual).Magnitude;
            Assert.True(error < 1e-8 * scale, $"{what}: {actual} against {expected} (error {error:0.###e+00})");
        }

        // ------------------------------------------------------------------ the check

        private static void Independent(
            Creature body, Vec3[] externalTorque, Vec3[] externalForce, double[] tau,
            out Vec3[] angular, out Vec3[] linear, out double[] jointAcceleration)
        {
            const int n = 28;
            var m = new double[n * (n + 1)];

            var inertia = new Mat3[3];
            for (int i = 0; i < 3; i++)
            {
                inertia[i] = Mat3.RotateDiagonal(
                    Mat3.Read(body.RotationMatrix, 9 * i), Vec3.Read(body.InertiaLocal, 3 * i));
            }

            // Each body's own equations. Row block b*6 .. b*6+5.
            for (int b = 0; b < 3; b++)
            {
                Vec3 spin = Vec3.Read(body.Spin, 3 * b);
                Vec3 gyroscopic = Vec3.Cross(spin, inertia[b] * spin);

                PutBlock(m, n, 6 * b, 6 * b, inertia[b]);
                Rhs(m, n, 6 * b, externalTorque[b] - gyroscopic);

                PutBlock(m, n, 6 * b + 3, 6 * b + 3, Mat3.Diagonal(body.Mass[b], body.Mass[b], body.Mass[b]));
                Rhs(m, n, 6 * b + 3, externalForce[b]);
            }

            // Each joint's coupling and its five constraints.
            for (int j = 1; j <= 2; j++)
            {
                int child = j, parent = j - 1;
                int force = 18 + (j - 1) * 5;       // three force unknowns
                int torque = force + 3;             // two torque unknowns

                QuatD rotation = QuatD.Read(body.Rotation, 4 * child);
                Vec3 anchorOffset = rotation.Rotate(Vec3.Read(body.ChildAnchor, 3 * child));
                Vec3 anchor = Vec3.Read(body.Position, 3 * child) + anchorOffset;

                Vec3 axis = (rotation * QuatD.Read(body.JointFrame, 4 * child)).Rotate(Vec3.UnitX);
                Perpendiculars(axis, out Vec3 e1, out Vec3 e2);

                Vec3 toChild = anchor - Vec3.Read(body.Position, 3 * child);
                Vec3 toParent = anchor - Vec3.Read(body.Position, 3 * parent);

                Vec3 childSpin = Vec3.Read(body.Spin, 3 * child);
                Vec3 parentSpin = Vec3.Read(body.Spin, 3 * parent);

                // Child rotational: -[toChild]x F - T  ; rhs gains + tau * axis
                AddBlock(m, n, 6 * child, force, Mat3.Skew(toChild) * -1.0);
                AddColumn(m, n, 6 * child, torque, -e1);
                AddColumn(m, n, 6 * child, torque + 1, -e2);
                AddRhs(m, n, 6 * child, axis * tau[j - 1]);

                // Parent rotational: +[toParent]x F + T ; rhs gains - tau * axis
                AddBlock(m, n, 6 * parent, force, Mat3.Skew(toParent));
                AddColumn(m, n, 6 * parent, torque, e1);
                AddColumn(m, n, 6 * parent, torque + 1, e2);
                AddRhs(m, n, 6 * parent, axis * -tau[j - 1]);

                // Child translational: -F. Parent translational: +F.
                AddBlock(m, n, 6 * child + 3, force, Mat3.Identity * -1.0);
                AddBlock(m, n, 6 * parent + 3, force, Mat3.Identity);

                // Constraint rows: the anchor's acceleration is one point.
                int row = 18 + (j - 1) * 5;

                AddBlock(m, n, row, 6 * child, Mat3.Skew(toChild) * -1.0);
                AddBlock(m, n, row, 6 * child + 3, Mat3.Identity);
                AddBlock(m, n, row, 6 * parent, Mat3.Skew(toParent));
                AddBlock(m, n, row, 6 * parent + 3, Mat3.Identity * -1.0);

                AddRhs(m, n, row,
                    Vec3.Cross(parentSpin, Vec3.Cross(parentSpin, toParent)) -
                    Vec3.Cross(childSpin, Vec3.Cross(childSpin, toChild)));

                // And the two directions the hinge locks.
                double rate = body.Qd[body.DofStart[child]];
                Vec3 axisRate = Vec3.Cross(parentSpin, axis);

                AddRow(m, n, row + 3, 6 * child, e1);
                AddRow(m, n, row + 3, 6 * parent, -e1);
                m[(row + 3) * (n + 1) + n] += Vec3.Dot(axisRate, e1) * rate;

                AddRow(m, n, row + 4, 6 * child, e2);
                AddRow(m, n, row + 4, 6 * parent, -e2);
                m[(row + 4) * (n + 1) + n] += Vec3.Dot(axisRate, e2) * rate;
            }

            double[] x = SolveDense(m, n);

            angular = new Vec3[3];
            linear = new Vec3[3];
            for (int b = 0; b < 3; b++)
            {
                angular[b] = new Vec3(x[6 * b], x[6 * b + 1], x[6 * b + 2]);
                linear[b] = new Vec3(x[6 * b + 3], x[6 * b + 4], x[6 * b + 5]);
            }

            jointAcceleration = new double[2];
            for (int j = 1; j <= 2; j++)
            {
                QuatD rotation = QuatD.Read(body.Rotation, 4 * j);
                Vec3 axis = (rotation * QuatD.Read(body.JointFrame, 4 * j)).Rotate(Vec3.UnitX);
                Vec3 parentSpin = Vec3.Read(body.Spin, 3 * (j - 1));
                double rate = body.Qd[body.DofStart[j]];

                jointAcceleration[j - 1] = Vec3.Dot(
                    angular[j] - angular[j - 1] - Vec3.Cross(parentSpin, axis) * rate, axis);
            }
        }

        private static void Perpendiculars(Vec3 axis, out Vec3 e1, out Vec3 e2)
        {
            Vec3 seed = System.Math.Abs(axis.X) < 0.9 ? Vec3.UnitX : Vec3.UnitY;
            e1 = Vec3.Cross(axis, seed).Normalized;
            e2 = Vec3.Cross(axis, e1).Normalized;
        }

        private static void PutBlock(double[] m, int n, int row, int column, Mat3 block)
        {
            m[row * (n + 1) + column] = block.M00;
            m[row * (n + 1) + column + 1] = block.M01;
            m[row * (n + 1) + column + 2] = block.M02;
            m[(row + 1) * (n + 1) + column] = block.M10;
            m[(row + 1) * (n + 1) + column + 1] = block.M11;
            m[(row + 1) * (n + 1) + column + 2] = block.M12;
            m[(row + 2) * (n + 1) + column] = block.M20;
            m[(row + 2) * (n + 1) + column + 1] = block.M21;
            m[(row + 2) * (n + 1) + column + 2] = block.M22;
        }

        private static void AddBlock(double[] m, int n, int row, int column, Mat3 block)
        {
            m[row * (n + 1) + column] += block.M00;
            m[row * (n + 1) + column + 1] += block.M01;
            m[row * (n + 1) + column + 2] += block.M02;
            m[(row + 1) * (n + 1) + column] += block.M10;
            m[(row + 1) * (n + 1) + column + 1] += block.M11;
            m[(row + 1) * (n + 1) + column + 2] += block.M12;
            m[(row + 2) * (n + 1) + column] += block.M20;
            m[(row + 2) * (n + 1) + column + 1] += block.M21;
            m[(row + 2) * (n + 1) + column + 2] += block.M22;
        }

        /// <summary>One unknown appearing as a vector coefficient down three rows.</summary>
        private static void AddColumn(double[] m, int n, int row, int column, Vec3 coefficient)
        {
            m[row * (n + 1) + column] += coefficient.X;
            m[(row + 1) * (n + 1) + column] += coefficient.Y;
            m[(row + 2) * (n + 1) + column] += coefficient.Z;
        }

        /// <summary>One row picking a direction out of three consecutive unknowns.</summary>
        private static void AddRow(double[] m, int n, int row, int column, Vec3 direction)
        {
            m[row * (n + 1) + column] += direction.X;
            m[row * (n + 1) + column + 1] += direction.Y;
            m[row * (n + 1) + column + 2] += direction.Z;
        }

        private static void Rhs(double[] m, int n, int row, Vec3 v)
        {
            m[row * (n + 1) + n] = v.X;
            m[(row + 1) * (n + 1) + n] = v.Y;
            m[(row + 2) * (n + 1) + n] = v.Z;
        }

        private static void AddRhs(double[] m, int n, int row, Vec3 v)
        {
            m[row * (n + 1) + n] += v.X;
            m[(row + 1) * (n + 1) + n] += v.Y;
            m[(row + 2) * (n + 1) + n] += v.Z;
        }

        private static double[] SolveDense(double[] m, int n)
        {
            int stride = n + 1;

            for (int column = 0; column < n; column++)
            {
                int pivot = column;
                double best = System.Math.Abs(m[column * stride + column]);
                for (int row = column + 1; row < n; row++)
                {
                    double v = System.Math.Abs(m[row * stride + column]);
                    if (v > best) { best = v; pivot = row; }
                }

                Assert.True(best > 1e-12, $"the constraint system is singular at column {column}");

                if (pivot != column)
                {
                    for (int k = 0; k < stride; k++)
                    {
                        double swap = m[column * stride + k];
                        m[column * stride + k] = m[pivot * stride + k];
                        m[pivot * stride + k] = swap;
                    }
                }

                double diagonal = m[column * stride + column];
                for (int row = column + 1; row < n; row++)
                {
                    double factor = m[row * stride + column] / diagonal;
                    if (factor == 0) continue;
                    for (int k = column; k < stride; k++) m[row * stride + k] -= factor * m[column * stride + k];
                }
            }

            var x = new double[n];
            for (int row = n - 1; row >= 0; row--)
            {
                double sum = m[row * stride + n];
                for (int k = row + 1; k < n; k++) sum -= m[row * stride + k] * x[k];
                x[row] = sum / m[row * stride + row];
            }

            return x;
        }
    }
}
