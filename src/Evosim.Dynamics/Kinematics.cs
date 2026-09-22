namespace Evosim.Dynamics
{
    /// <summary>
    /// The outward pass: world poses, world velocities, the motion subspace of every joint and
    /// the velocity-product acceleration that goes with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>World-oriented spatial quantities, at each link's own origin.</b> Every angular and
    /// linear quantity here is written in world axes, with the linear one taken at the link's
    /// origin — which is the part's centre of mass. That choice is what makes the articulated
    /// inertia transform in <see cref="Aba"/> cheap: the transform between a parent and a child
    /// is a pure translation, so no 3x3 rotation product appears in the recursion at all. The
    /// price is one rotation of each link's diagonal inertia tensor into world per step, which
    /// is a fraction of what the rotations in the link-frame formulation would cost.
    /// </para>
    /// <para>
    /// <b>A multi-degree-of-freedom joint is a composition of revolutes, done analytically.</b>
    /// The joint rotation is <c>Rx(q0) Ry(q1) Rz(q2)</c> in the joint frame, truncated to the
    /// joint's own degree count — the same X-then-Y-then-Z ordering PhysX's spherical joint
    /// drives are addressed in (<c>xDrive</c>, <c>yDrive</c>, <c>zDrive</c>) and the same one
    /// <c>EffectorDriver.DriveAxis</c> uses. There is no massless intermediate body: the motion
    /// subspace of the whole joint and its derivative are written out, so the articulated
    /// inertia is never singular.
    /// </para>
    /// </remarks>
    public static class Kinematics
    {
        /// <summary>
        /// Refreshes poses only. Used at build and after a placement, where the velocities are
        /// whatever the caller has just set.
        /// </summary>
        public static void Refresh(Creature body)
        {
            Poses(body);
            Velocities(body);
        }

        /// <summary>World pose of every link, from the root pose and the joint angles.</summary>
        public static void Poses(Creature body)
        {
            QuatD.Write(body.Rotation, 0, body.BaseRotation);
            Mat3.Write(body.RotationMatrix, 0, body.BaseRotation.ToMatrix());
            Vec3.Write(body.Position, 0, body.BasePosition);

            for (int i = 1; i < body.Links; i++)
            {
                int p = body.Parent[i];

                QuatD parentRotation = QuatD.Read(body.Rotation, 4 * p);
                QuatD rest = QuatD.Read(body.RestFrame, 4 * i);
                QuatD frame = QuatD.Read(body.JointFrame, 4 * i);
                QuatD joint = JointRotation(body, i);

                QuatD rotation = (parentRotation * rest * joint * frame.Conjugate).Normalized;

                QuatD.Write(body.Rotation, 4 * i, rotation);
                Mat3.Write(body.RotationMatrix, 9 * i, rotation.ToMatrix());

                Vec3 parentPosition = Vec3.Read(body.Position, 3 * p);
                Vec3 parentAnchor = parentRotation.Rotate(Vec3.Read(body.ParentAnchor, 3 * i));
                Vec3 childAnchor = rotation.Rotate(Vec3.Read(body.ChildAnchor, 3 * i));

                Vec3.Write(body.Position, 3 * i, parentPosition + parentAnchor - childAnchor);
            }
        }

        /// <summary>
        /// The motion subspace, every link's world velocity and the bias acceleration. Call
        /// after <see cref="Poses"/>; the root's own velocity is left as the caller set it.
        /// </summary>
        public static void Velocities(Creature body)
        {
            Vec3.Write(body.Cbias, 0, Vec3.Zero);
            Vec3.Write(body.Cbias, 3, Vec3.Zero);

            for (int i = 1; i < body.Links; i++)
            {
                int p = body.Parent[i];
                int n = body.DofCount[i];
                int at = body.DofStart[i];

                QuatD rotation = QuatD.Read(body.Rotation, 4 * i);
                QuatD frame = QuatD.Read(body.JointFrame, 4 * i);
                QuatD jointWorld = rotation * frame;   // the joint frame, in world axes

                Vec3 childAnchor = rotation.Rotate(Vec3.Read(body.ChildAnchor, 3 * i));

                Vec3 parentSpin = Vec3.Read(body.Spin, 3 * p);
                Vec3 parentVelocity = Vec3.Read(body.Velocity, 3 * p);

                Vec3 relativeSpin = Vec3.Zero;
                Vec3 sigma = Vec3.Zero;
                Vec3 frameSpin = parentSpin;           // angular velocity of intermediate frame j

                for (int d = 0; d < n; d++)
                {
                    Vec3 axis = jointWorld.Rotate(SubspaceAxis(body, i, d));

                    Vec3.Write(body.Sang, 9 * i + 3 * d, axis);
                    Vec3.Write(body.Slin, 9 * i + 3 * d, -Vec3.Cross(axis, childAnchor));

                    double rate = body.Qd[at + d];
                    relativeSpin += axis * rate;

                    if (n == 3) continue;   // a ball joint's sigma is taken below, in one line

                    // The axis is fixed in the frame that follows its own rotation, so its
                    // apparent world-frame derivative is that frame's angular velocity crossed
                    // into it. Accumulated in the same sweep because frame j's velocity is
                    // frame j-1's plus this degree of freedom's contribution.
                    frameSpin += axis * rate;
                    sigma += Vec3.Cross(frameSpin, axis) * rate;
                }

                // A ball joint's three axes are fixed in the CHILD rather than in three nested
                // frames, so every one of them turns with the child and the whole bias collapses
                // to one cross product.
                if (n == 3) sigma = Vec3.Cross(parentSpin, relativeSpin);

                Vec3 spin = parentSpin + relativeSpin;

                Vec3 offset = Vec3.Read(body.Position, 3 * i) - Vec3.Read(body.Position, 3 * p);
                Vec3 velocity = parentVelocity +
                                Vec3.Cross(parentSpin, offset) -
                                Vec3.Cross(relativeSpin, childAnchor);

                Vec3.Write(body.Spin, 3 * i, spin);
                Vec3.Write(body.Velocity, 3 * i, velocity);

                // c = [ sigma ; wp x (ui - up) - sigma x c - wrel x (wi x c) ]
                Vec3 linear =
                    Vec3.Cross(parentSpin, velocity - parentVelocity) -
                    Vec3.Cross(sigma, childAnchor) -
                    Vec3.Cross(relativeSpin, Vec3.Cross(spin, childAnchor));

                Vec3.Write(body.Cbias, 6 * i, sigma);
                Vec3.Write(body.Cbias, 6 * i + 3, linear);
            }
        }

        /// <summary>
        /// The joint's relative rotation, expressed in the joint frame:
        /// <c>Rx(q0) Ry(q1) Rz(q2)</c>, truncated to the joint's degrees of freedom.
        /// </summary>
        public static QuatD JointRotation(Creature body, int link)
        {
            int n = body.DofCount[link];
            if (n == 0) return QuatD.Identity;

            // A ball joint carries its rotation as a quaternion rather than as three angles —
            // see Creature.BallRotation for why.
            if (n == 3) return QuatD.Read(body.BallRotation, 4 * link);

            int at = body.DofStart[link];
            QuatD r = QuatD.FromAxisAngle(Creature.AxisOf(0), body.Q[at]);
            for (int d = 1; d < n; d++)
            {
                r = r * QuatD.FromAxisAngle(Creature.AxisOf(d), body.Q[at + d]);
            }
            return r;
        }

        /// <summary>
        /// Column <paramref name="d"/> of the motion subspace, in the child's joint frame.
        /// </summary>
        /// <remarks>
        /// Axis <i>d</i> is fixed in the frame that follows rotation <i>d</i>, so seen from the
        /// child it is the axis carried back through every rotation after it. For a one-degree
        /// joint that is the constant X axis, which is the revolute case.
        /// </remarks>
        private static Vec3 SubspaceAxis(Creature body, int link, int d)
        {
            int n = body.DofCount[link];
            int at = body.DofStart[link];

            // A ball joint's velocity coordinates ARE the relative angular velocity in the
            // child's joint frame, so its subspace is the identity and never degenerates.
            if (n == 3) return Creature.AxisOf(d);

            Vec3 s = Creature.AxisOf(d);
            for (int m = n - 1; m > d; m--)
            {
                s = QuatD.FromAxisAngle(Creature.AxisOf(m), -body.Q[at + m]).Rotate(s);
            }
            return s;
        }
    }
}
