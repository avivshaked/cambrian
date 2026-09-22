namespace Evosim.Core
{
    /// <summary>
    /// Two bodies touching, and the nearest pair of parts at which they touch — D106 item 5, the
    /// one thing the mouth needs from the physics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Core does not find these and could not.</b> Where a body is in the world is the
    /// harness's business — a <see cref="Phenotype"/>'s part positions are in the creature's own
    /// frame, and the only world position this assembly holds is <see cref="Organism.Point"/>, the
    /// centre <c>World.Observe</c> was handed. So the farm hands a list of these over each
    /// metabolic step, the way it hands positions over, and the damage rule is arithmetic on it.
    /// </para>
    /// <para>
    /// <b>One pair per pair of bodies, and it is the nearest one.</b> D106 item 4 settles that
    /// contact is a distance test at the metabolic step and no physics; the spec's rule 5 settles
    /// which part is hit when several are in contact. Both sides of a pair are named because a
    /// fight is symmetric: each body's part damages the other's, net of what the other's armour
    /// absorbs, in the same step and from the same record.
    /// </para>
    /// <para>
    /// <b>Part indices are into each body's <see cref="Organism.Phenotype"/> as it stands at the
    /// step the list was taken.</b> A plan change — a module added, a part bitten off — reindexes
    /// a body, so a list is good for the step it was made on and no longer, which is why
    /// <c>World.SetContacts</c> takes it immediately before the step that reads it.
    /// </para>
    /// </remarks>
    public readonly struct CreatureContact
    {
        public CreatureContact(long bodyA, int partA, long bodyB, int partB)
        {
            BodyA = bodyA;
            PartA = partA;
            BodyB = bodyB;
            PartB = partB;
        }

        /// <summary>One body's <see cref="Organism.Id"/>.</summary>
        public readonly long BodyA;

        /// <summary>The part of <see cref="BodyA"/> nearest the other body.</summary>
        public readonly int PartA;

        /// <summary>The other body's <see cref="Organism.Id"/>.</summary>
        public readonly long BodyB;

        /// <summary>The part of <see cref="BodyB"/> nearest <see cref="BodyA"/>.</summary>
        public readonly int PartB;

        public override string ToString() =>
            System.FormattableString.Invariant($"{BodyA}[{PartA}] ~ {BodyB}[{PartB}]");
    }
}
