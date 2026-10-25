using System;

namespace Evosim.Core
{
    /// <summary>
    /// A part's form: the genes a shape reads beside the half-extents — D233.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="None"/> for box, sphere and capsule.</b> Those three read the half-extents and
    /// nothing else, so a part of one carries <see cref="None"/>, three zeros, and
    /// <see cref="Genome.Validate"/> refuses a form on a node whose shape takes none. A shape that does
    /// take one (<see cref="PartShape.TakesForm"/>) refuses <see cref="None"/> in turn, so a family part
    /// is never measured without its genes.
    /// </para>
    /// <para>
    /// For the tapered superellipsoid the genes are three. <b>Profile</b> (ε1) sets how the outline
    /// rounds along the long axis, Y: small for a flat-ended block, 1 for an egg's round end.
    /// <b>Section</b> (ε2) sets each slice across Y: small for a rectangle, 1 for an ellipse, 2 for a
    /// diamond with an edge. <b>Taper</b> (τ) is the width at the +Y end over the width at the −Y end;
    /// the width at the middle stays the half-extent the genome gives.
    /// </para>
    /// <para>
    /// The ranges are D233's, and they keep every member convex: <see cref="Superellipsoid.ProfileConcavity"/>
    /// says why that reduces to one function of profile and taper, and the shape tests check it
    /// across the whole range. <see cref="Genome.Validate"/> enforces them on a node of a shape that
    /// takes a form, and <see cref="SuperellipsoidShape"/> refuses a form outside them; the geometry
    /// itself (<see cref="Superellipsoid"/>) accepts the wider set in which its formulas still hold, so
    /// the tests can walk it to the box and ball limits.
    /// </para>
    /// </remarks>
    public readonly struct PartForm : IEquatable<PartForm>
    {
        public const float ProfileMin = 0.1f;
        public const float ProfileMax = 1f;
        public const float SectionMin = 0.1f;
        public const float SectionMax = 2f;
        public const float TaperMin = 0.25f;
        public const float TaperMax = 4f;

        /// <summary>The form of a part whose shape reads the half-extents alone: all three genes 0.</summary>
        public static readonly PartForm None = default;

        /// <summary>ε1, the exponent of the outline along Y.</summary>
        public readonly float Profile;

        /// <summary>ε2, the exponent of each slice across Y.</summary>
        public readonly float Section;

        /// <summary>τ, the width at +Y over the width at −Y.</summary>
        public readonly float Taper;

        public PartForm(float profile, float section, float taper)
        {
            Profile = profile;
            Section = section;
            Taper = taper;
        }

        /// <summary>Whether this is <see cref="None"/>: all three genes exactly 0.</summary>
        public bool IsNone => Profile == 0f && Section == 0f && Taper == 0f;

        /// <summary>Whether all three genes lie inside D233's ranges.</summary>
        public bool InRange =>
            Profile >= ProfileMin && Profile <= ProfileMax &&
            Section >= SectionMin && Section <= SectionMax &&
            Taper >= TaperMin && Taper <= TaperMax;

        public bool Equals(PartForm other) =>
            Profile.Equals(other.Profile) && Section.Equals(other.Section) && Taper.Equals(other.Taper);

        public override bool Equals(object obj) => obj is PartForm other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Profile.GetHashCode();
                h = h * 397 ^ Section.GetHashCode();
                return h * 397 ^ Taper.GetHashCode();
            }
        }

        public override string ToString() =>
            FormattableString.Invariant($"profile={Profile} section={Section} taper={Taper}");
    }
}
