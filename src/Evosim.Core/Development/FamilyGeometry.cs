using System.Collections.Generic;
using System.Threading;

namespace Evosim.Core
{
    /// <summary>
    /// What a family part's geometry needs that its half-extents do not carry, built once and
    /// shared by the part and every scaled copy of it — D233.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One geometry, one set of panels, at one size.</b> A superellipsoid has no closed formula
    /// for its surface, so its drag panels are a measurement: about seven thousand surface points at
    /// the world's resolution of 2 (<see cref="Superellipsoid.AddPanels"/>). Growth rescales a body at
    /// every growth step and a uniform scaling by s moves every panel's centre by s and its area by s²
    /// and leaves its normal alone, so the measurement is made once, here, at the size the developer
    /// built the part at (<see cref="ReferenceHalfExtents"/>), and each scaled copy reads it through
    /// <see cref="PhenotypePart.FamilyScale"/>. Without this a growth step would measure the surface
    /// again for every family part of every growing creature.
    /// </para>
    /// <para>
    /// <b>Resolution 2 is the world's, and it has a slot of its own.</b> The part's surface area and
    /// its exposure factor are read from the resolution-2 panels (<see cref="MeasureResolution"/>), and
    /// the solver's drag asks for <c>PanelsPerAxis</c>, which is 2 in every world run so far. At 2
    /// one build serves all three. At any other resolution the drag set has a slot beside it, so the
    /// two never take turns replacing one another: a step that asked for resolution 3 and then for the
    /// exposure at 2 would otherwise rebuild the surface twice a step.
    /// </para>
    /// <para>
    /// <b>Built lazily, published whole.</b> A set is an immutable object written through one
    /// reference, so two threads that race to build it build equal sets and either may win, and a
    /// reader never sees a half-built one. <see cref="PanelBuilds"/> counts the builds, which is how
    /// the tests show that growth and exposure measure nothing.
    /// </para>
    /// </remarks>
    public sealed class FamilyGeometry
    {
        /// <summary>The resolution <see cref="ReferenceSurfaceArea"/> and the exposure factor are measured at.</summary>
        public const int MeasureResolution = 2;

        // One resolution's panels at the reference size and their total area. Immutable once built.
        private sealed class PanelBuild
        {
            public readonly int Resolution;
            public readonly DragPanel[] Panels;
            public readonly float SurfaceArea;

            public PanelBuild(int resolution, DragPanel[] panels)
            {
                Resolution = resolution;
                Panels = panels;

                // The same sum, in the same order, as PartShape.SurfaceArea takes.
                float total = 0f;
                for (int i = 0; i < panels.Length; i++) total += panels[i].Area;
                SurfaceArea = total;
            }
        }

        private PanelBuild _measured;   // resolution MeasureResolution
        private PanelBuild _other;      // the last other resolution asked for
        private int _builds;
        private ContactCoreBuild _core;
        private int _coreBuilds;
        private Float3 _referenceChildAnchor;
        private bool _hasReferenceChildAnchor;
        private readonly object _strokeLock = new object();
        private readonly List<StrokeEntry> _strokes = new List<StrokeEntry>();

        /// <param name="geometry">The part's superellipsoid, for its form.</param>
        /// <param name="referenceHalfExtents">The half-extents the part was built at.</param>
        public FamilyGeometry(Superellipsoid geometry, Float3 referenceHalfExtents)
        {
            Geometry = geometry ?? throw new System.ArgumentNullException(nameof(geometry));
            ReferenceHalfExtents = referenceHalfExtents;
        }

        /// <summary>
        /// The geometry for a part of <paramref name="shape"/> with <paramref name="form"/> at
        /// <paramref name="halfExtents"/>: null for a shape that takes no form.
        /// </summary>
        /// <remarks>
        /// The superellipsoid is the one family shape, so it is the only one this builds for; a shape
        /// that takes a form and is not that one is refused by name, since there is no geometry here
        /// to give it.
        /// </remarks>
        public static FamilyGeometry For(PartShape shape, PartForm form, Float3 halfExtents)
        {
            if (!shape.TakesForm) return null;

            if (shape is SuperellipsoidShape)
            {
                return new FamilyGeometry(SuperellipsoidShape.GeometryFor(form), halfExtents);
            }

            throw new System.NotSupportedException(
                $"Shape '{shape.Id}' takes a form, and the only family geometry built is the " +
                $"superellipsoid's ('{ShapeIds.Superellipsoid}').");
        }

        /// <summary>The part's superellipsoid. Immutable, and the same object for every copy.</summary>
        public Superellipsoid Geometry { get; }

        /// <summary>The half-extents the panels are measured at, metres.</summary>
        public Float3 ReferenceHalfExtents { get; }

        /// <summary>How many times panels were built: once per resolution asked for, barring a race.</summary>
        public int PanelBuilds => Volatile.Read(ref _builds);

        /// <summary>
        /// The surface area at the reference size, m²: the sum of the resolution-2 panels' areas.
        /// A copy at length ratio s has this times s².
        /// </summary>
        public float ReferenceSurfaceArea => BuildFor(MeasureResolution).SurfaceArea;

        /// <summary>
        /// The panels at the reference size for a resolution, built on first use. Shared, so a caller
        /// must not change them; the array is the set's own.
        /// </summary>
        public IReadOnlyList<DragPanel> ReferencePanels(int resolution) =>
            BuildFor(resolution < 1 ? 1 : resolution).Panels;

        // ---------------------------------------------------------------- the fin law's strip integral (D255)

        // One degree of freedom's reference values, keyed by the bits of its stroke direction. Immutable.
        private sealed class StrokeEntry
        {
            public readonly long X, Y, Z;
            public readonly double MassOverDensity, FirstMomentOverDensity;

            public StrokeEntry(long x, long y, long z, double mass, double moment)
            {
                X = x;
                Y = y;
                Z = z;
                MassOverDensity = mass;
                FirstMomentOverDensity = moment;
            }
        }

        /// <summary>How many strip integrals were computed here: once per distinct stroke direction.</summary>
        public int StrokeBuilds { get; private set; }

        /// <summary>
        /// The joint's place in the part's frame at the size the developer built the part at, the part's
        /// <see cref="PhenotypePart.ChildAnchorLocal"/> then. Set by the developer when the part is made; a
        /// scaled copy has this times its <see cref="PhenotypePart.FamilyScale"/>, and never divides its own
        /// anchor by the scale to find it.
        /// </summary>
        public Float3 ReferenceChildAnchor
        {
            get
            {
                if (!_hasReferenceChildAnchor)
                {
                    throw new System.InvalidOperationException(
                        "This family geometry was not made by the developer, so it holds no joint anchor at its " +
                        "reference size (D255). The fin law's strip integral needs one.");
                }

                return _referenceChildAnchor;
            }
        }

        internal void SetReferenceChildAnchor(Float3 anchor)
        {
            _referenceChildAnchor = anchor;
            _hasReferenceChildAnchor = true;
        }

        /// <summary>
        /// D255's strip integrals for one degree of freedom at the reference size, per unit density:
        /// <c>M/ρ = ∫ π/4 c² ds</c> and <c>I1/ρ = ∫ π/4 c² s ds</c> (<see cref="Superellipsoid.StrokeIntegrals(Float3, Double3, Double3, Double3, int, out double, out double)"/>).
        /// A copy at length ratio s has M/ρ times s³ and I1/ρ times s⁴.
        /// </summary>
        /// <param name="dofAxis">
        /// The degree's rotation axis in the link's frame: the joint frame applied to the degree's axis. The limb's
        /// axis is the reference anchor's negative, the unit stroke direction is <c>dofAxis × axis</c> normalised,
        /// as the step takes them, and neither depends on the part's size.
        /// </param>
        /// <param name="massOverDensity">M/ρ at the reference size, m³; 0 when the stroke has no part across the axis.</param>
        /// <param name="firstMomentOverDensity">I1/ρ at the reference size, m⁴.</param>
        /// <remarks>
        /// Computed once for each distinct direction (by its bits) and kept, so every scaled copy and every body built
        /// from this part reads the same two doubles. Two threads that race build equal values and either may win.
        /// </remarks>
        public void ReferenceStroke(Double3 dofAxis, out double massOverDensity, out double firstMomentOverDensity)
        {
            massOverDensity = 0.0;
            firstMomentOverDensity = 0.0;

            Float3 anchor = ReferenceChildAnchor;
            var toOrigin = new Double3(-(double)anchor.X, -(double)anchor.Y, -(double)anchor.Z);
            double reach = toOrigin.Length;
            if (!(reach > 0.0)) return;

            Double3 axis = toOrigin * (1.0 / reach);
            Double3 motion = Double3.Cross(dofAxis, axis);
            double motionLength = motion.Length;
            if (!(motionLength > 0.0)) return;

            Double3 stroke = motion * (1.0 / motionLength);
            long x = System.BitConverter.DoubleToInt64Bits(stroke.X);
            long y = System.BitConverter.DoubleToInt64Bits(stroke.Y);
            long z = System.BitConverter.DoubleToInt64Bits(stroke.Z);

            lock (_strokeLock)
            {
                for (int i = 0; i < _strokes.Count; i++)
                {
                    StrokeEntry held = _strokes[i];
                    if (held.X != x || held.Y != y || held.Z != z) continue;

                    massOverDensity = held.MassOverDensity;
                    firstMomentOverDensity = held.FirstMomentOverDensity;
                    return;
                }
            }

            var joint = new Double3(anchor.X, anchor.Y, anchor.Z);
            Geometry.StrokeIntegrals(
                ReferenceHalfExtents, joint, axis, stroke, Superellipsoid.StrokeResolution,
                out massOverDensity, out firstMomentOverDensity);

            lock (_strokeLock)
            {
                _strokes.Add(new StrokeEntry(x, y, z, massOverDensity, firstMomentOverDensity));
                StrokeBuilds++;
            }
        }

        // ---------------------------------------------------------------- the contact core

        /// <summary>
        /// How many times this part fetched its contact core, from <see cref="ContactCoreCache.Shared"/> or by a build
        /// there: once, barring a race. Whether a fetch built is the cache's count, not this one.
        /// </summary>
        public int ContactCoreBuilds => Volatile.Read(ref _coreBuilds);

        /// <summary>
        /// The 98 points of the contact core at the reference size, three doubles a point, in the shifted
        /// frame and the lattice's order (<see cref="Superellipsoid.ContactCoreCube"/>), built on first
        /// use. Shared, so a caller must not change them; the array is the set's own. A copy at length ratio
        /// s has this times s.
        /// </summary>
        public double[] ReferenceCorePoints => CoreFor().Points;

        /// <summary>The contact core's margin at the reference size, metres. A copy at length ratio s has this times s.</summary>
        public double ReferenceCoreMargin => CoreFor().Margin;

        /// <summary>
        /// How far the core swept by its margin stands outside the solid at the reference size, metres
        /// (<see cref="Superellipsoid.ContactCore(Float3, out double, out double, out double)"/>). A copy at length ratio s has this times s.
        /// </summary>
        public double ReferenceCoreBulge => CoreFor().Bulge;

        /// <summary>
        /// How far the solid stands outside the core swept by its margin at the reference size, metres.
        /// A copy at length ratio s has this times s.
        /// </summary>
        public double ReferenceCoreShortfall => CoreFor().Shortfall;

        /// <summary>
        /// A radius about the link's origin that holds every core point plus the margin and every point of
        /// the solid at the reference size, metres. A copy at length ratio s has this times s.
        /// </summary>
        public double ReferenceReach => CoreFor().Reach;

        /// <summary>
        /// A radius about the link's origin that holds every point of the solid at the reference size, metres
        /// (<see cref="Superellipsoid.Reach"/>): the contact core is not built for it. A copy at length ratio s has
        /// this times s.
        /// </summary>
        public double ReferenceSolidReach => Geometry.Reach(ReferenceHalfExtents);

        private ContactCoreBuild CoreFor()
        {
            ContactCoreBuild held = Volatile.Read(ref _core);
            if (held != null) return held;

            // Through the process's cache, keyed on the form and the reference half-extents exactly: a part whose
            // genes did not change since an ancestor's reuses that core, which is its cold build to the bit.
            ContactCoreBuild built = ContactCoreCache.Shared.For(Geometry, ReferenceHalfExtents);
            Interlocked.Increment(ref _coreBuilds);
            Volatile.Write(ref _core, built);
            return built;
        }

        private PanelBuild BuildFor(int resolution)
        {
            bool measured = resolution == MeasureResolution;

            PanelBuild held = Volatile.Read(ref measured ? ref _measured : ref _other);
            if (held != null && held.Resolution == resolution) return held;

            var panels = new List<DragPanel>(6 * resolution * resolution);
            Geometry.AddPanels(ReferenceHalfExtents, resolution, panels);
            var built = new PanelBuild(resolution, panels.ToArray());
            Interlocked.Increment(ref _builds);

            if (measured) Volatile.Write(ref _measured, built);
            else Volatile.Write(ref _other, built);
            return built;
        }
    }
}
