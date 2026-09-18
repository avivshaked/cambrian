namespace Evosim.Core
{
    /// <summary>
    /// A dead body still holding its charged matter, drifting in the water until it has leaked
    /// all of it into it. <c>fable-propose-grid.md</c> rule 6, as D098 leaves it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a corpse is an object and a trickle is not.</b> Everything else that moves energy
    /// from a body into the water is small and continuous: exudation (D070) is a share of a
    /// producer's income each step, and burning (D098's leg 2) returns a spent unit for every
    /// joule spent. Both are dissolved on arrival because that is what they physically are. A
    /// death is the one large
    /// parcel this ecology makes, and dissolving it on the step the body dies turns it into a
    /// density that a sitter reads as well as a swimmer does. Kept as a particle it is a place
    /// worth going to, which is the first prize a mover could win that a sitter cannot have.
    /// </para>
    /// <para>
    /// <b>No collider on this pass.</b> A corpse is a Core object with a position, seen by the
    /// economy and by nothing in the physics. Predation on contact is the seam this opens, not
    /// something it builds.
    /// </para>
    /// <para>
    /// <b>One stock, and it is standing in both books.</b> D098 made a body's tissue and its
    /// reserve the same substance as the water's dissolved matter, so a corpse has one account
    /// rather than two: its joules are part of §5A.2's audit and the same joules over
    /// <see cref="RunConfig.JoulesPerUnit"/> are part of the matter identity, from the instant
    /// the body dies to the instant the last of them is deposited. A corpse is a third account
    /// beside the water and the bodies, never a hole between them:
    /// <see cref="World.StandingJoules"/> and <see cref="World.MatterResidual"/> both read it.
    /// </para>
    /// </remarks>
    public sealed class Corpse
    {
        /// <summary>The creature this was, so a dissection can join it to <c>lineage.jsonl</c>.</summary>
        public long CreatureId { get; }

        /// <summary>Where it is, m. Founded at the body's last accepted position.</summary>
        public Float3 Position { get; internal set; }

        /// <summary>
        /// D061's horizontal cell, read from <see cref="Position"/> as it drifts. The cell field
        /// addresses a deposit by this and the grid and vertex fields by the position, so both
        /// have to stay in step; see <c>World.StepCorpses</c> for the tiled world's exception.
        /// </summary>
        public int Patch { get; internal set; }

        /// <summary>Charged matter not yet given back to the water, J — tissue plus reserve.</summary>
        public double Joules { get; internal set; }

        /// <summary>Simulated seconds since the death that founded it.</summary>
        public double AgeSeconds { get; internal set; }

        internal Corpse(long creatureId, Float3 position, int patch, double joules)
        {
            CreatureId = creatureId;
            Position = position;
            Patch = patch;
            Joules = joules;
        }

        /// <summary>Where a deposit from this corpse lands: its position and its patch.</summary>
        public FieldPoint Point => new FieldPoint(Position, Patch);

        public override string ToString() =>
            System.FormattableString.Invariant(
                $"corpse of {CreatureId}: {Joules:0.###} J, {AgeSeconds:0} s old");
    }
}
