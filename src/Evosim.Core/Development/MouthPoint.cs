using System;

namespace Evosim.Core
{
    /// <summary>
    /// Where a part's mouth is: one point in the part's own frame, and the test for which parts
    /// have a mouth at all. The solver, the world's reach test and the theatre's mark all call
    /// these, so the three cannot drift apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The mouth is the end of the part away from where it hangs.</b> A part with a parent
    /// points from its joint anchor to its centre, and its mouth is where that line leaves the
    /// body: the centre of the box's face, the sphere's surface point, the capsule's far tip, the
    /// point where a family part's axis meets its surface (D255). The
    /// root has no joint of its own, so it takes the side opposite its first child; a body of one
    /// part takes the positive end of its longest axis. Every one of those is a function of the
    /// part's own numbers, so the answer is the same on every machine and at every thread count.
    /// </para>
    /// <para>
    /// <b>Frame.</b> The origin is the part's centre and the axes are the part's own, which is the
    /// frame <see cref="PhenotypePart.Rotation"/> turns into the creature's (or, in the farm, the
    /// solver's) frame. <see cref="Posed"/> does that last step.
    /// </para>
    /// <para>
    /// <b>Scale.</b> Everything is read from the phenotype handed in, so a body at its current
    /// size is asked as <c>adult.Scaled(s)</c>, whose half-extents and anchors already carry the
    /// growth; never the adult genome's size.
    /// </para>
    /// <para>
    /// <b>Mirrored parts.</b> No special case is made: the anchors are read as the solver's
    /// <c>Kinematics.Poses</c> reads them, so the point agrees with the link pose the solver
    /// holds, and a twin and its mirror image have the same local point. <see cref="Developer"/>
    /// stores a mirrored part's anchors unreflected, and the solver therefore joins the part where
    /// its twin is joined (<c>logbook/specs/mirrored-anchor-finding.md</c>). A fix that reflects
    /// the stored anchors moves this point with them, and needs no change here.
    /// </para>
    /// </remarks>
    public static class MouthPoint
    {
        /// <summary>
        /// A part with an intake above this is an eating part. Attack plays no part: the reach
        /// gates carrion intake alone.
        /// </summary>
        public const float IntakeThreshold = 0f;

        /// <summary>
        /// True when the part takes food from a corpse in reach — its
        /// <see cref="PhenotypePart.Intake"/> is above <see cref="IntakeThreshold"/>.
        /// </summary>
        public static bool IsEatingPart(PhenotypePart part)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            return part.Intake > IntakeThreshold;
        }

        /// <summary>
        /// The mouth point of part <paramref name="partIndex"/> in that part's own frame, metres.
        /// </summary>
        /// <param name="phenotype">The body, at the size it now stands at.</param>
        /// <param name="partIndex">Index into <see cref="Phenotype.Parts"/>.</param>
        /// <remarks>
        /// <para>
        /// <b>Direction.</b> A part with a parent looks along <c>-ChildAnchorLocal</c>, from its
        /// joint to its centre. The root looks along the negative of the
        /// <see cref="PhenotypePart.ParentAnchorLocal"/> of its first child, the lowest-indexed
        /// part whose parent it is, which is the first in development order and cannot tie. A
        /// part with no direction at all, a single-part body or an anchor at the centre, looks
        /// along the positive end of its longest half-extent, the lowest axis index winning a tie.
        /// </para>
        /// <para>
        /// <b>Point.</b> A box takes the centre of the face its direction mostly points through,
        /// the lowest axis index winning a tie. A sphere takes its surface point along the
        /// direction, at <see cref="SphereShape.Radius"/>. A capsule takes the tip of the cap on
        /// the side the direction's y points to, at <see cref="CapsuleShape.HalfSpan"/> plus
        /// <see cref="CapsuleShape.Radius"/> along its long axis (y), and the positive tip when
        /// that component is zero. A family part (D233) takes the box's axis rule, the axis the
        /// direction mostly points through, and the point where that axis through the part's origin
        /// meets the true surface (<see cref="Superellipsoid.AxisReach"/>): its origin is the centre
        /// of mass, up to <see cref="Superellipsoid.CentreOffset"/> from the box's centre, so the
        /// box's face centre would stand off the tip. A shape this does not know is read as a box.
        /// </para>
        /// <para>
        /// Float arithmetic in a fixed order, no randomness and no collections, so the answer is
        /// bit-identical for the same phenotype wherever it is asked.
        /// </para>
        /// </remarks>
        public static Float3 Local(Phenotype phenotype, int partIndex)
        {
            if (phenotype == null) throw new ArgumentNullException(nameof(phenotype));
            if (partIndex < 0 || partIndex >= phenotype.PartCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(partIndex), partIndex,
                    "A body of " + phenotype.PartCount + " parts has no part " + partIndex + ".");
            }

            PhenotypePart part = phenotype.Parts[partIndex];
            Float3 h = part.HalfExtents;

            Float3 d = Direction(phenotype, part, partIndex);
            if (!(d.SqrMagnitude > 1e-24f)) d = LongestAxis(h);

            if (part.ShapeId == ShapeIds.Sphere)
            {
                return d.Normalized * SphereShape.Radius(h);
            }

            if (part.ShapeId == ShapeIds.Capsule)
            {
                float tip = CapsuleShape.HalfSpan(h) + CapsuleShape.Radius(h);
                return new Float3(0f, d.Y < 0f ? -tip : tip, 0f);
            }

            float ax = Math.Abs(d.X), ay = Math.Abs(d.Y), az = Math.Abs(d.Z);

            // D255. A family part: the box's axis rule, the true surface along that axis.
            if (part.Family != null)
            {
                Superellipsoid geometry = part.Family.Geometry;
                if (ax >= ay && ax >= az) return new Float3(geometry.AxisReach(h, 0, d.X < 0f), 0f, 0f);
                if (ay >= az) return new Float3(0f, geometry.AxisReach(h, 1, d.Y < 0f), 0f);
                return new Float3(0f, 0f, geometry.AxisReach(h, 2, d.Z < 0f));
            }

            // A box, and anything the standard shapes do not name.

            if (ax >= ay && ax >= az) return new Float3(d.X < 0f ? -Math.Abs(h.X) : Math.Abs(h.X), 0f, 0f);
            if (ay >= az) return new Float3(0f, d.Y < 0f ? -Math.Abs(h.Y) : Math.Abs(h.Y), 0f);
            return new Float3(0f, 0f, d.Z < 0f ? -Math.Abs(h.Z) : Math.Abs(h.Z));
        }

        /// <summary>
        /// <paramref name="local"/>, a point in a part's own frame, in the frame the part's pose
        /// is given in: <c>position + rotation * local</c>.
        /// </summary>
        /// <remarks>
        /// <see cref="Quat.Rotate(Float3)"/> is the convention Core uses everywhere a part's own
        /// frame is turned into the creature's (<see cref="PhenotypePart.ExposureFactor(Quat)"/>
        /// takes the part's axes this way), and the farm's solver poses a link by the same
        /// rotation, so a caller holding the solver's position and rotation gets the solver's
        /// point.
        /// </remarks>
        public static Float3 Posed(Float3 local, Float3 partWorldPosition, Quat partWorldRotation) =>
            partWorldPosition + partWorldRotation.Rotate(local);

        // The direction the mouth looks along, in the part's own frame, or zero when there is none.
        private static Float3 Direction(Phenotype phenotype, PhenotypePart part, int partIndex)
        {
            if (part.ParentIndex >= 0) return -part.ChildAnchorLocal;

            // The root. Parents precede children in the part list, so the first child in
            // development order is the lowest index whose parent is this part.
            for (int i = partIndex + 1; i < phenotype.PartCount; i++)
            {
                if (phenotype.Parts[i].ParentIndex == partIndex) return -phenotype.Parts[i].ParentAnchorLocal;
            }

            return Float3.Zero;
        }

        // The positive unit vector along the longest half-extent, the lowest axis winning a tie.
        private static Float3 LongestAxis(Float3 h)
        {
            float x = Math.Abs(h.X), y = Math.Abs(h.Y), z = Math.Abs(h.Z);

            if (x >= y && x >= z) return new Float3(1f, 0f, 0f);
            if (y >= z) return new Float3(0f, 1f, 0f);
            return new Float3(0f, 0f, 1f);
        }
    }
}
