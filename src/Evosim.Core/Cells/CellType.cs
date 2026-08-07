using System.Globalization;

namespace Evosim.Core
{
    /// <summary>
    /// What a part is made of, and therefore how it earns and spends energy — DESIGN.md §5A.1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Energy acquisition is a property of a <b>part</b>, not of a creature. That is what makes
    /// trophic strategy a morphological trait the §4.1 graph already encodes: a species is a
    /// distribution of cell types over a body plan, and speciation is a change in that
    /// distribution. No separate species, niche or strategy concept is required.
    /// </para>
    /// <para>
    /// <b>Subclass to add a type.</b> A type needs an <see cref="Id"/>, an upkeep, and an
    /// <see cref="Acquire"/> rule; register it in a <see cref="CellTypeRegistry"/> and it is
    /// available to development, mutation and serialization without touching any of them.
    /// </para>
    /// <para>
    /// <b>Every type costs something.</b> <see cref="UpkeepWattsPerCubicMetre"/> is not allowed
    /// to be zero, including for structural tissue. A part that costs nothing is a free lever:
    /// arbitrarily large bodies and arbitrarily long limbs with no pressure to be economical.
    /// That is the same class of fault as the free momentum in §11.2 — a resource with no price
    /// is spent without limit.
    /// </para>
    /// </remarks>
    public abstract class CellType
    {
        /// <summary>
        /// Stable identifier, serialized into genomes (§9) and into the config hash (§7).
        /// </summary>
        /// <remarks>
        /// A string rather than an enum member deliberately. Genomes outlive code: an enum
        /// forces renumbering when a type is inserted, and a genome stored as an ordinal then
        /// silently means something else. A string that no longer resolves fails loudly
        /// instead — see <see cref="CellTypeRegistry.Resolve"/>.
        /// </remarks>
        public abstract string Id { get; }

        /// <summary>
        /// Standing metabolic cost, watts per cubic metre of tissue. Must be greater than zero.
        /// </summary>
        /// <remarks>
        /// Set per instance rather than fixed per class, so a run can sweep it. This is the
        /// first entry in §5A.10's list of unmeasured numbers — "basal metabolic rate per unit
        /// volume, per part type" — and the ratios between the five types decide which trophic
        /// strategies are viable at all. A value hardcoded in a class is a value nobody can
        /// vary, and an unmeasured constant that cannot be varied is an assumption pretending
        /// to be a fact.
        /// </remarks>
        public float UpkeepWattsPerCubicMetre { get; }

        /// <param name="upkeepWattsPerCubicMetre">
        /// Standing cost per cubic metre. Rejected at zero: see the class remarks — a free part
        /// is a free lever.
        /// </param>
        protected CellType(float upkeepWattsPerCubicMetre)
        {
            if (!(upkeepWattsPerCubicMetre > 0f) || float.IsInfinity(upkeepWattsPerCubicMetre))
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(upkeepWattsPerCubicMetre), upkeepWattsPerCubicMetre,
                    "Every cell must cost something to keep alive. A part that costs nothing is " +
                    "a free lever, and bodies grow without limit against one.");
            }

            UpkeepWattsPerCubicMetre = upkeepWattsPerCubicMetre;
        }

        /// <summary>
        /// Whether a part of this type may have a movable joint to its parent — §5A.1.
        /// </summary>
        /// <remarks>
        /// False for everything except <see cref="LinkCell"/>. Two parts cannot move relative to
        /// each other unless a link sits between them, so motion costs a part and a creature
        /// with no links is rigid. Enforced by <c>Genome.Validate</c> rather than left as a
        /// convention: a genome whose stomach is also its elbow would develop, run, and be
        /// scored, and nothing downstream could tell it was never meant to be legal.
        /// </remarks>
        public virtual bool AllowsJoint => false;

        /// <summary>Energy this cell acquires over one step, in joules. Never negative.</summary>
        /// <remarks>
        /// Upkeep is charged separately by the caller, so an implementation returns gross
        /// intake rather than net. Returning a net figure would let a type quietly refund its
        /// own costs, which is exactly the sort of thing that is invisible until a population
        /// is living on it.
        /// </remarks>
        public abstract float Acquire(in CellContext context);

        /// <summary>Standing cost of a part over one step, in joules.</summary>
        /// <remarks>
        /// Virtual because capacity costs money even when idle, and only some types have a
        /// capacity to charge for — see <see cref="LinkCell"/>. The base term is proportional to
        /// tissue volume: being large costs more to keep alive, which is the only reason a
        /// creature has any pressure to be no larger than it needs.
        /// </remarks>
        public virtual float Upkeep(in CellContext context) =>
            UpkeepWattsPerCubicMetre * System.Math.Max(0f, context.Volume) * context.Seconds;

        /// <summary>Convenience overload for a part with no capacity to maintain.</summary>
        public float Upkeep(float volume, float seconds) =>
            Upkeep(new CellContext(seconds, volume));

        /// <summary>
        /// What this part's neurons cost, as a multiple of the per-neuron rate in
        /// <see cref="RunConfig.NeuralCostPerNeuronWatts"/> — DESIGN.md §5A.1.
        /// </summary>
        /// <param name="neuronCount">Neurons hosted on this part.</param>
        /// <param name="volume">The part's volume, cubic metres.</param>
        /// <remarks>
        /// <para>
        /// <b>1 for everything except <see cref="NeuralCell"/>.</b> Every cell hosts neurons at
        /// full price — a nerve net, which is what a creature without a brain has and what a
        /// cnidarian has for real. Neural tissue does not grant permission to think, it makes
        /// thinking cheaper.
        /// </para>
        /// <para>
        /// <b>Discount rather than a cap, and the difference is not stylistic.</b> Capping
        /// neurons by tissue volume would couple genome <i>validity</i> to part size — and under
        /// §4.5 parts change size constantly, since extinction-by-shrinking is the whole removal
        /// mechanism. A cell that shrank would invalidate a genome that was legal when it was
        /// written, and a genome whose legality depends on a mutation elsewhere in it is one that
        /// cannot be reasoned about locally.
        /// </para>
        /// <para>
        /// The discount is also what makes cephalization an economic outcome instead of a rule.
        /// §4.3 requires a neuron to sit on the part whose joint it drives, so motor neurons
        /// cannot leave the muscles — but everything else is cheaper where the tissue is, and so
        /// it concentrates. Nothing anywhere says "grow a head".
        /// </para>
        /// </remarks>
        public virtual float NeuronCostMultiplier(int neuronCount, float volume) => 1f;

        /// <summary>
        /// Writes this type's own tunable parameters into an already-open JSON object — §9.
        /// </summary>
        /// <remarks>
        /// The id and the upkeep are written by <see cref="CellTypeJson"/>, since every type has
        /// them. Override to add whatever else a subclass takes, and register a matching reader
        /// with <see cref="CellTypeJson.Register"/> — the two together are what keep the type
        /// system extensible without the serializer needing to know the full list.
        /// </remarks>
        public virtual void WriteParameters(Json.Writer writer) { }

        /// <summary>
        /// False colour for the cell-type view, as linear RGB in [0,1] — an instrument, not
        /// an appearance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is not §5A.5's colour, and the two must never be merged.</b> §5A.5 makes part
        /// colour an <i>evolvable genome field</i>: inert until creatures can see, and then a
        /// channel for camouflage, warning colouration, mimicry and display. That is a trait a
        /// creature <i>has</i>. This is a label for what a part <i>does</i>, chosen by us and
        /// heritable by nobody.
        /// </para>
        /// <para>
        /// Painting parts by cell type in the ordinary view would spend the exact visual channel
        /// that trait needs, and would then have to be taken away again — so the viewer offers
        /// them as separate modes and this one is never the creature's natural appearance.
        /// </para>
        /// <para>
        /// Deliberately not a constructor argument and deliberately absent from
        /// <see cref="HashContribution"/>: it cannot change a result, so a run that differs only
        /// in it is the same run. A tunable that reached the hash would make two identical
        /// experiments look different, which is the mirror of the fault §7 exists to catch.
        /// </para>
        /// </remarks>
        public virtual Float3 InspectionColour => new Float3(0.80f, 0.80f, 0.82f);

        /// <summary>
        /// Everything about this type that changes behaviour, for the config hash (§7).
        /// </summary>
        /// <remarks>
        /// Override when a subclass adds tunable parameters. The default covers the id and the
        /// upkeep only; a type whose feeding rate is configurable and does not extend this
        /// makes two materially different runs hash identically, and §7's whole purpose is to
        /// <i>detect</i> that.
        /// </remarks>
        public virtual string HashContribution() =>
            string.Format(
                CultureInfo.InvariantCulture, "{0}:upkeep={1:R},joint={2}", Id, UpkeepWattsPerCubicMetre, AllowsJoint);

        public override string ToString() => Id;
    }
}
