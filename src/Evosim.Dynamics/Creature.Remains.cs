using System;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// D220, contact eating: a body whose organism is remains, a dead body or a cut-off limb lying in the water for a
    /// window. The solver keeps it as a physical body and nothing more.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the flag takes away.</b> The step runs no senses and no brain for it, pushes no drive torque and no
    /// stroke (every degree of freedom is limp: the joint damper and the limit springs are all that act on a joint),
    /// takes no float lift (a buoyancy cell sinks as the tissue it is), holds no grip, and draws no work. What stays is
    /// everything a body is made of: drag, buoyancy and its offset torque, gravity, contact, the water, the limits.
    /// </para>
    /// <para>
    /// <b>With no flag set the step's arithmetic is unchanged</b> (<c>DynamicsWorld.StepOne</c> and
    /// <c>Fluid.Apply</c> each test it once and take their old path), so a world with the dial off steps every body
    /// to the bit it always did.
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        /// <summary>
        /// True for a body whose organism is remains. Set by <see cref="BecomeRemains"/> and never cleared: remains
        /// do not come back to life. Not written to the checkpoint by the body: the farm writes it beside the body
        /// and sets it on the rebuilt one, as it sets every wiring the constructor cannot know.
        /// </summary>
        public bool Remains { get; private set; }

        /// <summary>
        /// Turns the body into remains: the drive is silenced (its envelope, its smoothing window and its last
        /// torque all zero), every grip is let go and the next steps run without a brain. The pose, the velocity and
        /// the joint coordinates are not touched, so the body carries on from where it died.
        /// </summary>
        public void BecomeRemains()
        {
            if (Remains) return;

            Remains = true;
            Drive.Silence();

            Array.Clear(DriveSignal, 0, DriveSignal.Length);
            Array.Clear(BendSignal, 0, BendSignal.Length);
            Array.Clear(DriveTorque, 0, DriveTorque.Length);

            if (GripHeld != null)
            {
                for (int i = 0; i < Links; i++)
                {
                    if (!GripHeld[i]) continue;

                    GripHeld[i] = false;
                    GripStrength[i] = 0;
                    GripsBroken++;
                }
            }

            GripOwedJoules = 0;
        }

        /// <summary>
        /// Poses this body, a new one built on a phenotype that is part of another body's, from the links of the
        /// body it was cut from: the root link takes the pose and the velocity of the source link that is its own
        /// part, and every other link takes the source link's joint coordinates and rates, so that each link stands
        /// where its source link stood and moves as it moved.
        /// </summary>
        /// <param name="source">The body the parts were cut from, as it stood. Left untouched.</param>
        /// <param name="sourceLink">
        /// For each link of this body, the link of <paramref name="source"/> that is the same part, or -1 for none.
        /// Entry 0 must name a link: the root's pose is the whole of where the body is.
        /// </param>
        /// <remarks>
        /// <b>Pose and velocity, not a copy of the source's arrays.</b> A link's world pose is derived from the
        /// root's and the joint coordinates, so copying those and running the kinematics gives every link the pose
        /// of its source when the two bodies' anchors agree, and the nearest the new body's own anchors allow when
        /// they do not (the source's solver body can lag the organism's size by up to a growth step). Call it between
        /// steps, as <see cref="AdoptStateFrom"/> is called: it commits the contact sphere.
        /// </remarks>
        public void PoseFromLinksOf(Creature source, int[] sourceLink)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (sourceLink == null) throw new ArgumentNullException(nameof(sourceLink));

            if (sourceLink.Length < Links)
            {
                throw new ArgumentException(
                    $"The map covers {sourceLink.Length} links and this body has {Links}.", nameof(sourceLink));
            }

            int root = sourceLink[0];
            if (root < 0 || root >= source.Links)
            {
                throw new ArgumentException(
                    $"The root's source link is {root} in a body of {source.Links} links: a body cut from another is " +
                    "posed by its root, and one with no root link has no place.", nameof(sourceLink));
            }

            BasePosition = Vec3.Read(source.Position, 3 * root);
            BaseRotation = QuatD.Read(source.Rotation, 4 * root);

            Vec3.Write(Velocity, 0, Vec3.Read(source.Velocity, 3 * root));
            Vec3.Write(Spin, 0, Vec3.Read(source.Spin, 3 * root));

            for (int i = 1; i < Links; i++)
            {
                int from = sourceLink[i];
                if (from < 0 || from >= source.Links) continue;

                QuatD.Write(BallRotation, 4 * i, QuatD.Read(source.BallRotation, 4 * from));

                int dof = DofCount[i];
                if (dof == 0 || source.DofCount[from] != dof) continue;

                int to = DofStart[i];
                int at = source.DofStart[from];
                if (to < 0 || at < 0) continue;

                Array.Copy(source.Q, at, Q, to, dof);
                Array.Copy(source.Qd, at, Qd, to, dof);
            }

            Kinematics.Refresh(this);
            RefreshContactSphere();
            CommitContactSphere();
        }
    }
}
