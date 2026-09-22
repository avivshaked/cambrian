using Evosim.Dynamics;

namespace Gpu.Spike
{
    /// <summary>
    /// The CPU reference: the same reduced step as the kernel, run on real
    /// <see cref="Creature"/> objects through <see cref="Evosim.Dynamics"/>'s own routines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two routines are copied rather than called, and both are named here.</b>
    /// <see cref="JointTorques"/> is a verbatim copy of <c>DynamicsWorld.JointTorques</c>, which
    /// is private; the task's rule is to copy rather than widen anything in <c>src/</c>.
    /// <see cref="FrozenDrive"/> is the tail of <c>EffectorDrive.Drive</c> with the ten-sample
    /// moving average frozen at the step's values — which is the spike's own reduction, not the
    /// library's, so it could not have been called in any case.
    /// </para>
    /// <para>
    /// Everything else is the library: <see cref="Fluid.Apply"/>, <see cref="Aba.Solve"/>,
    /// <see cref="Aba.Integrate"/>, <see cref="Kinematics.Poses"/> and
    /// <see cref="Kinematics.Velocities"/>, in the order <c>DynamicsWorld.StepOne</c> calls them.
    /// </para>
    /// </remarks>
    internal static class RefStep
    {
        /// <summary>One step of one body. <paramref name="driveTorque"/> is 3 per link, in the link's own frame.</summary>
        public static void Step(Creature body, SolverConfig config, double[] driveTorque, double dt)
        {
            System.Array.Clear(body.Fext, 0, body.Fext.Length);
            System.Array.Clear(body.Tau, 0, body.Tau.Length);

            FrozenDrive(body, driveTorque);
            Fluid.Apply(body, config);
            JointTorques(body, config);

            Aba.Solve(body);
            Aba.Integrate(body, dt);

            Kinematics.Poses(body);
            Kinematics.Velocities(body);
        }

        /// <summary>
        /// The tail of <c>EffectorDrive.Drive</c>: the link-frame torque on the child and its
        /// reaction on the parent. The drive limiter is not here because it does not engage at
        /// dt 0.01 (<c>SolverConfig.LimitersEngage</c>).
        /// </summary>
        private static void FrozenDrive(Creature body, double[] driveTorque)
        {
            for (int b = 1; b < body.Links; b++)
            {
                if (body.DofCount[b] == 0) continue;

                Mat3 rotation = Mat3.Read(body.RotationMatrix, 9 * b);
                Vec3 world = rotation * Vec3.Read(driveTorque, 3 * b);

                Vec3.Add(body.Fext, 6 * b, world);
                Vec3.Add(body.Fext, 6 * body.Parent[b], -world);
            }
        }

        /// <summary>
        /// Copied verbatim from <c>DynamicsWorld.JointTorques</c> (private there), less its two
        /// ledger calls, which record work and move nothing.
        /// </summary>
        private static void JointTorques(Creature body, SolverConfig config)
        {
            double damping = config.JointDriveDamping;
            double dt = config.StepSeconds;

            for (int i = 1; i < body.Links; i++)
            {
                int n = body.DofCount[i];
                if (n == 0) continue;

                int at = body.DofStart[i];
                for (int d = 0; d < n; d++)
                {
                    int j = at + d;
                    double q = body.Q[j];
                    double rate = body.Qd[j];
                    double torque = -damping * rate;
                    double resist = damping * dt;

                    double past = q < body.LimitLo[j] ? q - body.LimitLo[j]
                        : q > body.LimitHi[j] ? q - body.LimitHi[j]
                        : 0;

                    if (past != 0)
                    {
                        double k = body.LimitStiffness[j];
                        double c = body.LimitDamping[j];

                        torque += -k * past - c * rate;
                        resist += (c + k * dt) * dt;
                    }

                    body.LimitImplicit[j] = resist;
                    body.Tau[j] += torque;
                }
            }
        }
    }
}
