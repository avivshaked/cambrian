using System.Collections.Generic;

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

        /// <summary>
        /// The id of the body whose bite made this corpse, or -1 when no bite did: an age, a starvation or a
        /// divergence death, a part a module rule shed, a corpse placed by hand, or a kill whose finishing
        /// blow the pass could not name. Set once when the corpse is founded, and carried in a checkpoint.
        /// A record and nothing else: no rule of the world reads it, only the intake's books of who ate
        /// what (<see cref="World.IntakeOwnKillJoules"/> and its two neighbours).
        /// </summary>
        /// <remarks>
        /// <b>The attacker the kill row names.</b> It is the <c>by</c> of the <c>"e":"k"</c> row the same kill
        /// wrote, from the same <c>_lastAttacker</c> entry, so a corpse and its kill row never disagree.
        /// </remarks>
        public long KilledBy { get; internal set; } = -1L;

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

        /// <summary>
        /// Spent units the body held at death, units — D206. They leave with the corpse's decay
        /// instalments, into the spent field, and the last of them when the corpse leaves.
        /// </summary>
        public double SpentUnits { get; internal set; }

        /// <summary>Simulated seconds since the death that founded it.</summary>
        public double AgeSeconds { get; internal set; }

        /// <summary>
        /// D127: what it held when it was founded, J. A corpse's size in a picture is its body's
        /// scaled by the cube root of <see cref="Joules"/> over this, and
        /// <see cref="RunConfig.CorpseLeavesBelowFraction"/> reads the same ratio.
        /// </summary>
        public double FirstJoules { get; }

        /// <summary>
        /// D127: at rest on the first solid surface under it, a reef's cap or the bed, with
        /// <see cref="RunConfig.CorpseSettles"/> on. A settled corpse neither sinks nor is carried,
        /// and dissolves where it lies.
        /// </summary>
        public bool Settled { get; internal set; }

        /// <summary>
        /// The body it was, at its size when it died, for a picture. Held in memory only: a
        /// checkpoint does not carry it, so a restored corpse has none and a picture takes its plan
        /// from the record by <see cref="CreatureId"/>. Nothing in the economy reads it.
        /// </summary>
        public Phenotype Body { get; internal set; }

        /// <summary>
        /// D127, for a picture: the body's module counts when it died (<see cref="Organism.ModuleCounts"/>),
        /// null for the genome's own minimum. With <see cref="LostPartPaths"/> and
        /// <see cref="ScaledBy"/> and the genome the record holds under <see cref="CreatureId"/>, it
        /// develops the corpse's body again. A limb shed by a living body carries none of the three.
        /// </summary>
        public int[] ModuleCounts { get; internal set; }

        /// <summary>D127, for a picture: the parts the body had lost when it died, null for none.</summary>
        public List<int[]> LostPartPaths { get; internal set; }

        /// <summary>
        /// D127, for a picture: the body's size when it died as a length ratio to its adult
        /// (<see cref="Phenotype.ScaledBy"/>); 0 for a limb shed by a living body.
        /// </summary>
        public float ScaledBy { get; internal set; }

        /// <summary>
        /// D127, for a picture: the root's orientation when the body died, as the solver held it,
        /// set by the harness that stepped the body (<see cref="SetPose"/>). Identity until then.
        /// </summary>
        public Quat Rotation { get; private set; } = Quat.Identity;

        /// <summary>Whether <see cref="Rotation"/> has been set.</summary>
        public bool HasPose { get; private set; }

        /// <summary>
        /// Stamps the orientation the body died in, once. A record and nothing else: no rule of
        /// the world reads it, and it is carried in a checkpoint so that a film started from one
        /// draws the corpse as it lay.
        /// </summary>
        public void SetPose(Quat rotation)
        {
            if (HasPose) return;
            Rotation = rotation;
            HasPose = true;
        }

        /// <summary>Puts a checkpoint's pose back, set or not.</summary>
        internal void RestorePose(Quat rotation, bool hasPose)
        {
            Rotation = rotation;
            HasPose = hasPose;
        }

        internal Corpse(long creatureId, Float3 position, int patch, double joules)
            : this(creatureId, position, patch, joules, joules)
        {
        }

        internal Corpse(long creatureId, Float3 position, int patch, double joules, double firstJoules)
        {
            CreatureId = creatureId;
            Position = position;
            Patch = patch;
            Joules = joules;
            FirstJoules = firstJoules;
        }

        /// <summary>Where a deposit from this corpse lands: its position and its patch.</summary>
        public FieldPoint Point => new FieldPoint(Position, Patch);

        public override string ToString() =>
            System.FormattableString.Invariant(
                $"corpse of {CreatureId}: {Joules:0.###} J, {AgeSeconds:0} s old");
    }
}
