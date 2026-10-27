using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// D234: a mirrored twin's joint drive is reflected with its geometry, so that one brain signal bends
    /// and beats a pair as mirror images rather than in a seesaw. Under D228's switch only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a twin seesaws.</b> A mirrored part's transform is <c>R · F</c> with <c>F = diag(-1, 1, 1)</c>,
    /// and the solver poses it from the proper rotation <c>R</c> alone. Its joint axes are ordinary vectors
    /// in that frame, so the same angle turns it about the mirror image of its original's axis, and the
    /// mirror image of a turn is a turn the other way.
    /// </para>
    /// <para>
    /// <b>Which signs.</b> A link is posed as <c>R_p · rest · J(q) · frame⁻¹</c>, with the joint's own
    /// rotation <c>J = Rx(q0) Ry(q1) Rz(q2)</c> cut to its degrees of freedom. For a twin to move as the
    /// mirror image of its original, its <c>J</c> must satisfy <c>frame J_twin frame⁻¹ = F frame J_orig
    /// frame⁻¹ F</c>, that is <c>J_twin = K J_orig K</c> with <c>K = frame⁻¹ F frame</c>, a reflection.
    /// Conjugating a rotation by a reflection keeps its angle if the reflection leaves the axis alone up to
    /// sign, and reverses the angle if the axis lies in the mirror plane:
    /// </para>
    /// <list type="bullet">
    /// <item>A hinge's frame is turned, so <c>K = diag(1, 1, -1)</c> and its one axis <c>x</c> lies in the
    /// plane: the angle is negated.</item>
    /// <item>Every other joint's frame is the identity, so <c>K = F</c>. Its first axis <c>x</c> is the
    /// reflection's own axis and is reversed by it, which cancels the reversal of the angle: the first
    /// degree of freedom is not negated. Its second and third axes lie in the plane and are negated.</item>
    /// </list>
    /// <para>
    /// A degree of freedom is negated by <see cref="MirrorMask"/>, one bit a degree of freedom: every
    /// one of a hinge's, and the second and third of any other. The stored angle, rate and torque of the
    /// body are the solver's own and never change sign. What changes is how the drive and the senses read
    /// them: through <see cref="DofSign"/>, with the stops read through <see cref="SignedLimitLo"/> and
    /// <see cref="SignedLimitHi"/>. The stops themselves are stored swapped and negated (<c>[-hi, -lo]</c>)
    /// because the limit spring acts on the stored angle. A product of rotations reflects term by term,
    /// which is why the same mask serves a universal and a ball joint. Quantities that are squared, as the
    /// stroke's power and the limb push, need no sign.
    /// </para>
    /// <para>
    /// The mask is derived from the part and not stored in the checkpoint: it is rewritten with the joint
    /// stops at a build and at every growth resize.
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        /// <summary>
        /// The sign of the signed joint quantities of one degree of freedom of a link: -1 where the link's
        /// <see cref="MirrorMask"/> has the degree of freedom's bit set, and +1 everywhere else. Exactly 1
        /// for every link of a body with no reflected part, so multiplying by it moves no bit.
        /// </summary>
        public double DofSign(int link, int d) => ((MirrorMask[link] >> d) & 1) != 0 ? -1.0 : 1.0;

        /// <summary>The lower stop of one degree of freedom as the signed reading has it: the stored stop, or the negated upper one.</summary>
        public double SignedLimitLo(int dof, double sign) => sign < 0 ? -LimitHi[dof] : LimitLo[dof];

        /// <summary>The upper stop of one degree of freedom as the signed reading has it: the stored stop, or the negated lower one.</summary>
        public double SignedLimitHi(int dof, double sign) => sign < 0 ? -LimitLo[dof] : LimitHi[dof];

        /// <summary>
        /// The <see cref="MirrorMask"/> of one part: zero unless the part reflects its drive
        /// (<see cref="PhenotypePart.ReflectedDrive"/>) and has a joint.
        /// </summary>
        internal static int MirrorMaskOf(PhenotypePart part)
        {
            if (!part.ReflectedDrive || part.IsRoot) return 0;

            int n = part.JointType.DofCount();
            bool turned = FrameIsTurned(part.JointType);
            int mask = 0;

            for (int d = 0; d < n; d++)
            {
                if (turned || d > 0) mask |= 1 << d;
            }

            return mask;
        }

        /// <summary>
        /// The axis of a part's own frame that its reflection is about, 0 to 2, or -1 for a part that reflects
        /// nothing: the one place the normal is named.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>It is the frame's x in every case, and the reason is in the decomposition.</b> A part's full
        /// transform is <c>L</c>, however many mirrors of however many axes its ancestors and its own edge applied.
        /// <see cref="Mat4.Decompose"/> takes a negative determinant (<c>Mat4.cs</c>, <c>mirrored = Determinant3 &lt; 0f</c>)
        /// and restores a proper rotation by negating the first column and nothing else (<c>cx = -cx</c>), so
        /// <c>L = R · diag(-1, 1, 1)</c> whatever plane the mirror was about. The solver poses the part from
        /// <c>R</c>; the geometry stands where <c>L</c> puts it. A vector read in the part's geometry frame is
        /// <c>L⁻¹ v = diag(-1, 1, 1) · Rᵀ v</c>, and the stored frame <c>R</c> differs from it by that flip alone.
        /// A mirror about y or z in the edge, or a mirrored parent, changes <c>R</c> and the part's place but not
        /// the flip, because the decomposition absorbs the whole reflection into the same first column. So the
        /// stored frame's x is the normal of the reflection that turns the part into its twin's mirror image, as a
        /// vector the senses read in the part's frame, for every mirrored part. A part that was reflected twice has
        /// a positive determinant, is not <see cref="PhenotypePart.Mirrored"/>, and reads in the frame it has.
        /// </para>
        /// <para>
        /// The reading then agrees with the joint drive's: both are the part's own frame read as its geometry
        /// stands, so a twin's whole loop of sense and drive is the mirror image of its original's.
        /// </para>
        /// </remarks>
        internal static int FrameNormalOf(PhenotypePart part) => part.ReflectedDrive ? 0 : -1;

        /// <summary>
        /// A vector read in a link's own frame, in the geometry frame the link stands in: the component along the
        /// link's <see cref="FrameNormal"/> negated, and the vector as it is for a link that reflects nothing.
        /// </summary>
        public Vec3 ReflectFrameVector(int link, Vec3 local)
        {
            switch (FrameNormal[link])
            {
                case 0: return new Vec3(-local.X, local.Y, local.Z);
                case 1: return new Vec3(local.X, -local.Y, local.Z);
                case 2: return new Vec3(local.X, local.Y, -local.Z);
                default: return local;
            }
        }
    }
}
