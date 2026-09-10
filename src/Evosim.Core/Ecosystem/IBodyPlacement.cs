namespace Evosim.Core
{
    /// <summary>
    /// Where a body may go — D077's shared space, asked of whoever owns the coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The seam §6.1 requires.</b> Once the world's footprint is literal, a birth can fail for
    /// a reason the economy knows nothing about: there is no room. But a box is a set of
    /// coordinates, and <c>Evosim.Core</c> may not have any — nothing here reads x or z, and
    /// <see cref="World"/> has never known where a creature is beyond its height. So the world
    /// asks for room and is told yes or no, and <c>Evosim.Sim</c>'s <c>Ecosystem</c> — which owns
    /// the articulations and therefore the positions — is what answers.
    /// </para>
    /// <para>
    /// <b>Reserve, then commit.</b> A reservation is taken before the parent is charged, so a
    /// refusal costs nothing: the energy is not spent, the matter is not drawn, and no lineage row
    /// is written, because none of it happened. The reservation is bound to a creature id only
    /// once that creature exists (<see cref="Commit"/>), and dropped if it turns out not to
    /// (<see cref="Release"/>) — a genome that develops into no parts at all is stillborn for the
    /// older reason and never reaches this.
    /// </para>
    /// <para>
    /// <b>One reservation is outstanding at a time.</b> Conception is a sequential walk over the
    /// living (<c>World.Reproduce</c>) and every reservation is committed or released before the
    /// next is taken, so an implementation needs one slot rather than a queue.
    /// </para>
    /// </remarks>
    public interface IBodyPlacement
    {
        /// <summary>
        /// Reserves room for a child about to be born beside <paramref name="parent"/>.
        /// </summary>
        /// <param name="parent">The parent, already alive and therefore already somewhere.</param>
        /// <param name="adult">
        /// The developed body <b>at its adult size</b>, for the room that has to be held. Not the
        /// body the child is born with: since D087 a child is born at a fraction of this and grows
        /// into it without moving, so a spot that fits the newborn is a spot it grows out of and
        /// into its neighbours (logbook/0082's contact cluster). What the world promises a newborn
        /// is room for the animal it becomes.
        /// </param>
        /// <param name="heightY">
        /// In, the depth the child would be admitted at — its parent's. Out, the depth it is
        /// actually placed at, which the implementation may only ever <b>raise</b>: a world with a
        /// solid sea bed cannot put a body inside the rock, and a parent resting on the floor has
        /// to breed beside itself rather than below itself (<c>scratch/floor-spec.md</c>, rule 2).
        /// Untouched in the ordinary case, so nothing but a body against the bed sees a difference.
        /// </param>
        /// <param name="patch">
        /// The patch the reserved position falls in — D077 reads a patch from a position rather
        /// than inheriting an index, so this is what the child is admitted with.
        /// </param>
        /// <returns>False when the neighbourhood is full: a crowded stillbirth.</returns>
        bool TryReserveOffspring(Organism parent, Phenotype adult, ref float heightY, out int patch);

        /// <summary>
        /// Reserves room anywhere in the world for a body that has no parent — a floor founder
        /// (<c>World.EnforceFloor</c>) or an inoculant (<c>World.Inoculate</c>).
        /// </summary>
        /// <param name="body">
        /// The developed body, for its size. <b>Still the newborn here</b>, where
        /// <see cref="TryReserveOffspring"/> takes the adult since 2026-09-10: a founder and an
        /// inoculant are born at their own birth fraction too (growth rule 7) and grow in place
        /// exactly as a child does, so the same argument applies to them and the same change has
        /// not been made, because the founder rule is the owner's to move and this one was ruled
        /// for births. A floor spawn into a full world therefore still reserves a spot it can
        /// grow out of.
        /// </param>
        /// <param name="heightY">
        /// In, the depth it was drawn at; only x and z are free. Out, the depth it is actually
        /// placed at — raised, and never lowered, when the draw would have put the body in the
        /// sea bed. See <see cref="TryReserveOffspring"/>.
        /// </param>
        /// <param name="patch">The patch the reserved position falls in.</param>
        /// <returns>False when the world is too full to admit it.</returns>
        bool TryReserveFounder(Phenotype body, ref float heightY, out int patch);

        /// <summary>
        /// The patch a living creature's body is actually in — D077's "a patch is a region".
        /// </summary>
        /// <remarks>
        /// Asked of every living creature once per metabolic step, before anything in the economy
        /// reads <see cref="Organism.Patch"/>. Returns the creature's current patch unchanged when
        /// the placer has nothing recorded for it, so a gap in the bookkeeping cannot silently
        /// move an animal to patch 0.
        /// </remarks>
        int PatchOf(Organism creature);

        /// <summary>Binds the outstanding reservation to the creature that got it.</summary>
        void Commit(long creatureId);

        /// <summary>Drops the outstanding reservation: the birth did not happen after all.</summary>
        void Release();
    }
}
