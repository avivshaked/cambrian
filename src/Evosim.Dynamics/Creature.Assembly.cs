using System;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// D231: a stroke pushes its whole assembly. The constants the powered stroke's push reads for a
    /// jointed link that carries parts welded beyond it, written at a build and again at every resize.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The assembly.</b> A jointed link (a parent and at least one degree of freedom) owns itself and
    /// every later link reached from it through links with no degree of freedom, up to the next jointed
    /// link. Links are ordered so that a parent comes before its child, so one forward pass sets each
    /// link's anchor: its parent's anchor where it is welded, and itself where it is not.
    /// </para>
    /// <para>
    /// <b>Where the welded parts stand.</b> From the solver's own anchors and rest frames, composed as
    /// <see cref="Kinematics.Poses"/> composes them with every joint at rest, relative to the anchoring
    /// link and in its frame. Not from <see cref="PhenotypePart.Position"/>: the two agree except for a
    /// mirrored part while <c>ReflectMirroredAnchors</c> is off, where the solver joins the part somewhere
    /// else than development put it, and the solver's joint is the body that is simulated and dragged.
    /// </para>
    /// <para>
    /// <b>The strips.</b> For each degree of freedom, every part of the assembly is projected onto the
    /// limb's axis (joint to the anchoring link's origin) and onto <c>n</c>, the unit vector of
    /// axis × stroke direction, by its support function. The chord <c>c(s)</c> at a distance <c>s</c> from
    /// the joint is the width of the envelope of the <c>n</c>-intervals of the parts whose axis-interval
    /// covers <c>s</c>; it is constant between the sorted interval ends, so the integrals are exact sums
    /// over those pieces.
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        /// <summary>
        /// The most parts an assembly may have under <see cref="SolverConfig.StrokePushesAssembly"/>. The
        /// strip integral sorts two ends per part in a buffer of fixed size on the stack. A body whose
        /// assembly has more parts is refused at build or resize with an <see cref="ArgumentException"/>
        /// that names this bound; nothing is truncated. <see cref="DevelopmentLimits.MaxParts"/> is 16 by
        /// default, so no recorded body comes near it.
        /// </summary>
        public const int MaxAssemblyParts = 32;

        /// <summary>
        /// For each link, the number of parts in its assembly if it is a jointed link, and 0 otherwise.
        /// Null unless <see cref="SolverConfig.StrokePushesAssembly"/>. A jointed link with 1 takes D229's
        /// own lines in <see cref="Fluid.Apply(Creature, SolverConfig)"/>.
        /// </summary>
        public int[] StrokeAssemblyParts;

        /// <summary>
        /// For each degree of freedom, indexed like <see cref="DofStart"/>: the cosine between its axis
        /// and the limb's, both in the link's frame. Null unless the switch is on; 0 for a link whose
        /// assembly has one part.
        /// </summary>
        public double[] StrokeAlong;

        /// <summary>
        /// For each degree of freedom: the added mass of the assembly moving along that degree's stroke
        /// direction, kg, <c>M = ∫ ρπ/4 c² ds</c>. Null unless the switch is on; 0 for a link whose assembly
        /// has one part.
        /// </summary>
        public double[] StrokeAddedMass;

        /// <summary>
        /// For each degree of freedom: the first moment of that added mass about the joint, kg·m,
        /// <c>I1 = ∫ ρπ/4 c² s ds</c>. Its lever is <c>I1 / M</c>. Null unless the switch is on.
        /// </summary>
        public double[] StrokeFirstMoment;

        // Each link's anchoring link, and its turn and place relative to that link, in that link's frame.
        private int[] _strokeAnchor;
        private double[] _strokeRotation;
        private double[] _strokePosition;

        /// <summary>
        /// The switch's two refusals, taken from the solver config before anything is built so that a body
        /// that would be pushed by neither law never exists.
        /// </summary>
        internal static void RefuseStrokeAssembly(SolverConfig config)
        {
            if (!config.StrokePushesAssembly) return;

            if (!config.ShapedAddedMass)
            {
                throw new ArgumentException(
                    "SolverConfig.StrokePushesAssembly is on and ShapedAddedMass is off. D231 integrates D229's " +
                    "plate formula in strips along the assembly, so it needs the fin law " +
                    "(EVOSIM_SHAPED_ADDED_MASS).");
            }

            if (config.LimbPushCoefficient > 0 && !(config.PowerPushRadiansPerSecond > 0))
            {
                throw new ArgumentException(
                    "SolverConfig.StrokePushesAssembly is on and the limb push is the swing push " +
                    "(LimbPushCoefficient above 0 without the bend-and-power drive). The swing's direction " +
                    "changes every step and D231's strips are taken along a fixed stroke, so only the " +
                    "power push is built.");
            }
        }

        private void AllocateStrokeAssembly()
        {
            StrokeAssemblyParts = new int[Links];
            StrokeAlong = new double[Dof];
            StrokeAddedMass = new double[Dof];
            StrokeFirstMoment = new double[Dof];
            _strokeAnchor = new int[Links];
            _strokeRotation = new double[4 * Links];
            _strokePosition = new double[3 * Links];
        }

        /// <summary>
        /// Writes the anchors, the assemblies and every jointed link's constants, in place. Call after
        /// <see cref="WriteSizedProperties"/>, whose anchors, rest frames and half-extents it reads.
        /// </summary>
        private void WriteStrokeAssembly()
        {
            int n = Links;
            Array.Clear(StrokeAssemblyParts, 0, n);
            Array.Clear(StrokeAlong, 0, Dof);
            Array.Clear(StrokeAddedMass, 0, Dof);
            Array.Clear(StrokeFirstMoment, 0, Dof);

            // One forward pass: a link is welded when it has a parent and no degree of freedom, and its
            // parent always comes first. Its place relative to its anchor composes Kinematics.Poses at rest.
            for (int k = 0; k < n; k++)
            {
                int parent = Parent[k];
                if (parent < 0 || DofCount[k] > 0)
                {
                    _strokeAnchor[k] = k;
                    QuatD.Write(_strokeRotation, 4 * k, QuatD.Identity);
                    Vec3.Write(_strokePosition, 3 * k, Vec3.Zero);
                    continue;
                }

                _strokeAnchor[k] = _strokeAnchor[parent];

                QuatD parentRotation = QuatD.Read(_strokeRotation, 4 * parent);
                Vec3 parentPosition = Vec3.Read(_strokePosition, 3 * parent);
                QuatD rest = QuatD.Read(RestFrame, 4 * k);
                QuatD frame = QuatD.Read(JointFrame, 4 * k);

                QuatD rotation = (parentRotation * rest * frame.Conjugate).Normalized;
                Vec3 place = parentPosition +
                             parentRotation.Rotate(Vec3.Read(ParentAnchor, 3 * k)) -
                             rotation.Rotate(Vec3.Read(ChildAnchor, 3 * k));

                QuatD.Write(_strokeRotation, 4 * k, rotation);
                Vec3.Write(_strokePosition, 3 * k, place);
            }

            for (int k = 0; k < n; k++) StrokeAssemblyParts[_strokeAnchor[k]]++;
            for (int k = 0; k < n; k++)
            {
                if (Parent[k] < 0 || DofCount[k] == 0) StrokeAssemblyParts[k] = 0;
            }

            // Fixed-size buffers, on the stack, for the parts' intervals.
            Span<int> members = stackalloc int[MaxAssemblyParts];
            Span<double> axisLo = stackalloc double[MaxAssemblyParts];
            Span<double> axisHi = stackalloc double[MaxAssemblyParts];
            Span<double> sideLo = stackalloc double[MaxAssemblyParts];
            Span<double> sideHi = stackalloc double[MaxAssemblyParts];

            for (int i = 0; i < n; i++)
            {
                int count = StrokeAssemblyParts[i];
                if (count < 2) continue;

                if (count > MaxAssemblyParts)
                {
                    throw new ArgumentException(
                        "Link " + i + " anchors an assembly of " + count + " parts and D231 takes at most " +
                        MaxAssemblyParts + " (Creature.MaxAssemblyParts). Nothing is truncated, so the body is refused.");
                }

                int found = 0;
                for (int k = i; k < n; k++)
                {
                    if (_strokeAnchor[k] == i) members[found++] = k;
                }

                Vec3 childAnchor = Vec3.Read(ChildAnchor, 3 * i);
                Vec3 toOrigin = -childAnchor;
                double reach = toOrigin.Magnitude;
                if (!(reach > 0)) continue;   // the step's powered push needs a reach too

                Vec3 axis = toOrigin * (1.0 / reach);
                QuatD jointFrame = QuatD.Read(JointFrame, 4 * i);
                int start = DofStart[i];

                for (int d = 0; d < DofCount[i]; d++)
                {
                    Vec3 dofAxis = jointFrame.Rotate(AxisOf(d));
                    double along = Vec3.Dot(dofAxis, axis);
                    StrokeAlong[start + d] = along;

                    Vec3 motion = Vec3.Cross(dofAxis, axis);
                    double motionLength = motion.Magnitude;
                    if (!(motionLength > 0)) continue;

                    Vec3 strokeDirection = motion * (1.0 / motionLength);
                    Vec3 across = Vec3.Cross(axis, strokeDirection);
                    double acrossLength = across.Magnitude;
                    if (!(acrossLength > 0)) continue;
                    Vec3 side = across * (1.0 / acrossLength);

                    for (int m = 0; m < found; m++)
                    {
                        int k = members[m];
                        PhenotypePart part = Phenotype.Parts[k];
                        QuatD rotation = QuatD.Read(_strokeRotation, 4 * k);
                        Vec3 centre = Vec3.Read(_strokePosition, 3 * k);

                        double along0 = Vec3.Dot(centre - childAnchor, axis);
                        double side0 = Vec3.Dot(centre, side);
                        double alongHalf = SupportHalfWidth(part.ShapeId, part.HalfExtents, rotation, axis);
                        double sideHalf = SupportHalfWidth(part.ShapeId, part.HalfExtents, rotation, side);

                        axisLo[m] = along0 - alongHalf;
                        axisHi[m] = along0 + alongHalf;
                        sideLo[m] = side0 - sideHalf;
                        sideHi[m] = side0 + sideHalf;
                    }

                    StripIntegrals(
                        axisLo.Slice(0, found), axisHi.Slice(0, found),
                        sideLo.Slice(0, found), sideHi.Slice(0, found),
                        _config.Density, out double mass, out double moment);

                    StrokeAddedMass[start + d] = mass;
                    StrokeFirstMoment[start + d] = moment;
                }
            }
        }

        /// <summary>
        /// A part's half-width along a unit direction: the support function of its shape less its centre's.
        /// A box's is D229's caliper <c>Σ |e·e_j| h_j</c>; a capsule's is <c>halfSpan |e·u| + r</c> with
        /// <c>u</c> its long axis, local Y; a sphere's is <c>r</c>.
        /// </summary>
        /// <param name="shapeId">The part's <c>ShapeId</c>.</param>
        /// <param name="halfExtents">The part's half-extents, in its own frame.</param>
        /// <param name="rotation">The part's own frame, in the frame <paramref name="direction"/> is in.</param>
        /// <param name="direction">A unit vector.</param>
        internal static double SupportHalfWidth(string shapeId, Float3 halfExtents, QuatD rotation, Vec3 direction)
        {
            if (shapeId == ShapeIds.Sphere) return SphereShape.Radius(halfExtents);

            if (shapeId == ShapeIds.Capsule)
            {
                double r = CapsuleShape.Radius(halfExtents);
                double halfSpan = CapsuleShape.HalfSpan(halfExtents);
                return halfSpan * Math.Abs(Vec3.Dot(direction, rotation.Rotate(Vec3.UnitY))) + r;
            }

            if (shapeId == ShapeIds.Box)
            {
                return Math.Abs(Vec3.Dot(direction, rotation.Rotate(Vec3.UnitX))) * Math.Abs((double)halfExtents.X) +
                       Math.Abs(Vec3.Dot(direction, rotation.Rotate(Vec3.UnitY))) * Math.Abs((double)halfExtents.Y) +
                       Math.Abs(Vec3.Dot(direction, rotation.Rotate(Vec3.UnitZ))) * Math.Abs((double)halfExtents.Z);
            }

            throw new ArgumentException(
                "D231's strips have no support function for the shape '" + shapeId +
                "'; they know box, capsule and sphere.", nameof(shapeId));
        }

        /// <summary>
        /// The strip integrals of one assembly, exactly: <c>M = Σ ρπ/4 c² Δs</c> and
        /// <c>I1 = Σ ρπ/4 c² s_mid Δs</c> over the pieces between the sorted interval ends, on each of which
        /// the chord <c>c</c> is constant (the width of the envelope of the side-intervals of the parts whose
        /// axis-interval covers the piece), so the midpoint is exact. A piece no part covers adds nothing.
        /// </summary>
        /// <param name="axisLo">Each part's lower end along the axis, from the joint.</param>
        /// <param name="axisHi">Each part's upper end along the axis.</param>
        /// <param name="sideLo">Each part's lower end across the stroke and the axis.</param>
        /// <param name="sideHi">Each part's upper end across the stroke and the axis.</param>
        /// <param name="density">The water's density, kg/m³.</param>
        /// <param name="mass">M, kg.</param>
        /// <param name="firstMoment">I1, kg·m.</param>
        internal static void StripIntegrals(
            ReadOnlySpan<double> axisLo, ReadOnlySpan<double> axisHi,
            ReadOnlySpan<double> sideLo, ReadOnlySpan<double> sideHi,
            double density, out double mass, out double firstMoment)
        {
            int parts = axisLo.Length;
            if (parts > MaxAssemblyParts)
            {
                throw new ArgumentException(
                    "An assembly of " + parts + " parts is past D231's bound of " + MaxAssemblyParts +
                    " (Creature.MaxAssemblyParts).");
            }

            Span<double> cuts = stackalloc double[2 * MaxAssemblyParts];
            int ends = 0;
            for (int k = 0; k < parts; k++)
            {
                cuts[ends++] = axisLo[k];
                cuts[ends++] = axisHi[k];
            }

            // Insertion sort: a fixed recurrence, so the same input is the same bits on any thread.
            for (int a = 1; a < ends; a++)
            {
                double value = cuts[a];
                int b = a - 1;
                while (b >= 0 && cuts[b] > value)
                {
                    cuts[b + 1] = cuts[b];
                    b--;
                }
                cuts[b + 1] = value;
            }

            double quarterPi = Math.PI / 4.0;
            mass = 0;
            firstMoment = 0;

            for (int p = 0; p + 1 < ends; p++)
            {
                double from = cuts[p];
                double to = cuts[p + 1];
                double length = to - from;
                if (!(length > 0)) continue;

                double mid = 0.5 * (from + to);
                bool covered = false;
                double hi = 0, lo = 0;

                for (int k = 0; k < parts; k++)
                {
                    if (!(axisLo[k] < mid && mid < axisHi[k])) continue;

                    if (!covered)
                    {
                        hi = sideHi[k];
                        lo = sideLo[k];
                        covered = true;
                    }
                    else
                    {
                        if (sideHi[k] > hi) hi = sideHi[k];
                        if (sideLo[k] < lo) lo = sideLo[k];
                    }
                }

                if (!covered) continue;

                double chord = hi - lo;
                double piece = density * quarterPi * chord * chord * length;
                mass += piece;
                firstMoment += piece * mid;
            }
        }
    }
}
