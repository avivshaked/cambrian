using System.Globalization;
using System.Text;

namespace Evosim.Core
{
    /// <summary>
    /// Every value a run may be varied by, in one place — DESIGN.md §5A.10 and §7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This exists so that experiments are possible.</b> Almost every number in §5A is
    /// unmeasured, and the honest response to an unmeasured number is to sweep it and see what
    /// the world does, not to pick one and write it into a class. A constant compiled into a
    /// type is a constant no run can vary, and an unmeasured constant that cannot be varied is
    /// an assumption wearing the costume of a fact.
    /// </para>
    /// <para>
    /// <b>It is also half of §7.</b> The promise there is that any evaluation is reproducible
    /// from <c>(genome, seed, configHash)</c>. The hash has to be taken over something, and this
    /// is that something. <see cref="Hash"/> covers every field, so a run that changed a
    /// photosynthetic efficiency and a run that did not are distinguishable after the fact —
    /// which matters most in the case where results come out identical, since by now that has
    /// twice meant a configuration change never reached the thing it configured.
    /// </para>
    /// <para>
    /// <b>Nothing here is a genome trait.</b> Brood size and offspring endowment are evolved and
    /// live on <see cref="Genome.Reproduction"/>; what lives here is the world they are spent
    /// into, such as <see cref="PerOffspringOverheadJoules"/>. The test of which side something
    /// belongs on: if a creature could benefit by choosing its own value, it is a world constant,
    /// because evolution will choose whichever value is free.
    /// </para>
    /// </remarks>
    public sealed class RunConfig
    {
        /// <summary>How the initial population is drawn — §4.1.</summary>
        public RandomGenomeOptions Genome { get; set; } = RandomGenomeOptions.Default;

        /// <summary>Caps applied while growing a genome into a body — §4.2.</summary>
        public DevelopmentLimits Development { get; set; } = DevelopmentLimits.Default;

        /// <summary>How often each variation operator fires — §4.5.</summary>
        public MutationRates Mutation { get; set; } = MutationRates.Default;

        /// <summary>Water: density, drag, added mass — §5.2.</summary>
        public FluidConfig Fluid { get; set; } = new FluidConfig();

        /// <summary>
        /// The cell types available, their upkeep and their feeding rates — §5A.1.
        /// </summary>
        /// <remarks>
        /// The registry's <i>order</i> is part of the hash as well as its contents, because
        /// cell-type mutation picks by an RNG draw and ordering therefore decides which type a
        /// given draw yields. Two registries holding the same types in a different order are not
        /// interchangeable.
        /// </remarks>
        public CellTypeRegistry CellTypes { get; set; } = CellTypeRegistry.Standard;

        /// <summary>
        /// Fixed cost per offspring on top of its endowment, in joules — §5A.6.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The term that makes brood size a strategy.</b> With cost strictly proportional to
        /// energy invested, one brood of four and four broods of one are indistinguishable —
        /// same energy, same offspring, only the timing differs — and brood size selects for
        /// nothing. This is what separates them, and what stops brood size running away: each
        /// extra offspring costs something regardless of how little it is given.
        /// </para>
        /// <para>
        /// A world constant and deliberately not evolvable. A creature permitted to set its own
        /// overhead would set it to zero within a few generations, and then every lineage would
        /// converge on the largest brood it could express.
        /// </para>
        /// <para>⚠ Unmeasured — §5A.10.</para>
        /// </remarks>
        public float PerOffspringOverheadJoules { get; set; } = 25f;

        /// <summary>
        /// Metabolic joules charged per joule of mechanical work at the joints — §5A.2.
        /// </summary>
        /// <remarks>
        /// The exchange rate between <c>∫|τ·ω| dt</c> and a joule of sunlight, and the single
        /// most consequential unmeasured number in the design: it sets whether moving is worth
        /// doing at all. Too high and every lineage converges on the photosynthetic mat; too low
        /// and motion is free, which removes the pressure that makes efficient swimming
        /// interesting in the first place.
        ///
        /// ⚠ Unmeasured, and not measurable until the economy runs — §5A.10. Above 1 is not a
        /// mistake: muscle is lossy, so a joule delivered at the joint costs more than a joule.
        /// </remarks>
        public float WorkCostMultiplier { get; set; } = 1f;

        /// <summary>Metabolic joules per neuron per second — §5A.2.</summary>
        /// <remarks>
        /// With <see cref="NeuralCostPerConnectionWatts"/>, this is what prices thinking. Both
        /// exist because a brain that costs nothing grows without limit, in the same way a part
        /// that costs nothing does (§5A.1). ⚠ Unmeasured — §5A.10.
        /// </remarks>
        public float NeuralCostPerNeuronWatts { get; set; } = 0.05f;

        /// <summary>Metabolic joules per neuron input per second — §5A.2.</summary>
        /// <remarks>
        /// Separate from the per-neuron cost because connections are where the combinatorial
        /// growth is: neurons scale linearly with body size and connections need not.
        /// ⚠ Unmeasured — §5A.10.
        /// </remarks>
        public float NeuralCostPerConnectionWatts { get; set; } = 0.01f;

        /// <summary>Chance a mutation changes a part's cell type — §5A.3.</summary>
        /// <remarks>
        /// Deliberately small. It is one of the two bridges across the predator valley — the
        /// route by which a herbivore becomes a carnivore once there is something worth eating —
        /// so it must be possible; but a cell type that flips often is not a trait, it is noise,
        /// and lineages cannot specialise around it. "Very scarce" is the requirement; the value
        /// is a guess. ⚠ Unmeasured — §5A.10.
        /// </remarks>
        public float CellTypeMutationChance { get; set; } = 0.01f;

        /// <summary>
        /// A stable digest of everything above — the <c>configHash</c> of §7.
        /// </summary>
        /// <remarks>
        /// <para>
        /// FNV-1a over an invariant-culture rendering of every field. Not cryptographic and not
        /// meant to be: the job is to notice that two runs differed, not to resist an adversary.
        /// </para>
        /// <para>
        /// <b>What it is for.</b> PhysX is not bitwise deterministic across machines or Unity
        /// versions, so this cannot promise portability and does not try. It exists to
        /// <i>detect</i> a mismatch — and the case it earns its keep on is the one where two
        /// runs produce identical output. That has now twice meant a configuration change never
        /// reached the thing it configured (logbook/0007, logbook/0008), and a hash that differs
        /// while the results do not is the cheapest way to tell that apart from a parameter that
        /// genuinely does not matter.
        /// </para>
        /// </remarks>
        public string Hash()
        {
            var sb = new StringBuilder();
            var c = CultureInfo.InvariantCulture;

            sb.Append(CellTypes.HashContribution()).Append('|');
            sb.Append(Fluid.Density.ToString("R", c)).Append(',');
            sb.Append(Fluid.DragCoefficient.ToString("R", c)).Append(',');
            sb.Append(Fluid.AddedMassCoefficient.ToString("R", c)).Append(',');
            sb.Append(Fluid.PanelsPerAxis).Append('|');
            sb.Append(Development.MaxParts).Append(',');
            sb.Append(Development.MaxDepth).Append(',');
            sb.Append(Development.MinPartVolume.ToString("R", c)).Append('|');
            sb.Append(PerOffspringOverheadJoules.ToString("R", c)).Append(',');
            sb.Append(WorkCostMultiplier.ToString("R", c)).Append(',');
            sb.Append(NeuralCostPerNeuronWatts.ToString("R", c)).Append(',');
            sb.Append(NeuralCostPerConnectionWatts.ToString("R", c)).Append(',');
            sb.Append(CellTypeMutationChance.ToString("R", c)).Append('|');
            AppendMutationRates(sb, c);
            AppendGenomeOptions(sb, c);

            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < sb.Length; i++)
            {
                hash ^= sb[i];
                hash *= 1099511628211UL;
            }

            return hash.ToString("x16", c);
        }

        /// <remarks>
        /// Written out field by field rather than by reflection. Reflection would pick up new
        /// fields automatically, which sounds like the safer choice and is the opposite: it
        /// would also silently pick up anything added for a non-behavioural reason, and quietly
        /// change every stored hash. An explicit list fails visibly — a new tunable that nobody
        /// added here shows up as two different configurations sharing a hash, which is what the
        /// tests below check for.
        /// </remarks>
        private void AppendMutationRates(StringBuilder sb, CultureInfo c)
        {
            MutationRates m = Mutation;

            sb.Append(m.ScalarChance.ToString("R", c)).Append(',');
            sb.Append(m.ScalarStdDev.ToString("R", c)).Append(',');
            sb.Append(m.AddNodeChance.ToString("R", c)).Append(',');
            sb.Append(m.NewNodeHalfExtent.ToString("R", c)).Append(',');
            sb.Append(m.NodeExtinctionHalfExtent.ToString("R", c)).Append(',');
            sb.Append(m.AddEdgeChance.ToString("R", c)).Append(',');
            sb.Append(m.RemoveEdgeChance.ToString("R", c)).Append(',');
            sb.Append(m.AddNeuronChance.ToString("R", c)).Append(',');
            sb.Append(m.RemoveNeuronChance.ToString("R", c)).Append(',');
            sb.Append(m.RewireInputChance.ToString("R", c)).Append(',');
            sb.Append(m.NeuronOpChance.ToString("R", c)).Append(',');
            sb.Append(m.JointTypeChance.ToString("R", c)).Append(',');
            sb.Append(m.FlagChance.ToString("R", c)).Append(',');
            sb.Append(m.RecursiveLimitChance.ToString("R", c)).Append(',');
            sb.Append(m.CellTypeChance.ToString("R", c)).Append(',');
            sb.Append(m.BroodSizeChance.ToString("R", c)).Append(',');
            sb.Append(m.EndowmentChance.ToString("R", c)).Append(',');
            sb.Append(m.MaxBroodSize).Append(',');
            sb.Append(m.MaxNodes).Append('|');
        }

        private void AppendGenomeOptions(StringBuilder sb, CultureInfo c)
        {
            RandomGenomeOptions g = Genome;

            sb.Append(g.MinNodes).Append(',').Append(g.MaxNodes).Append(',');
            sb.Append(g.MaxEdgesPerNode).Append(',');
            sb.Append(g.MinRecursiveLimit).Append(',').Append(g.MaxRecursiveLimit).Append(',');
            sb.Append(g.MinHalfExtent.ToString("R", c)).Append(',');
            sb.Append(g.MaxHalfExtent.ToString("R", c)).Append(',');
            sb.Append(g.MinEdgeScale.ToString("R", c)).Append(',');
            sb.Append(g.MaxEdgeScale.ToString("R", c)).Append(',');
            sb.Append(g.ReflectChance.ToString("R", c)).Append(',');
            sb.Append(g.TerminalChance.ToString("R", c)).Append(',');
            sb.Append(g.RotateChance.ToString("R", c)).Append(',');
            sb.Append(g.MaxEdgeTiltDegrees.ToString("R", c)).Append(',');
            sb.Append(g.MinNeuronsPerNode).Append(',').Append(g.MaxNeuronsPerNode).Append(',');
            sb.Append(g.MinOscillatorHz.ToString("R", c)).Append(',');
            sb.Append(g.MaxOscillatorHz.ToString("R", c)).Append(',');
            sb.Append(g.MinJointLimit.ToString("R", c)).Append(',');
            sb.Append(g.MaxJointLimit.ToString("R", c)).Append(',');
            sb.Append(g.MinLinkHalfExtent.ToString("R", c)).Append(',');
            sb.Append(g.MaxLinkHalfExtent.ToString("R", c)).Append(',');
            sb.Append(g.LinkChance.ToString("R", c)).Append(',');
            sb.Append(g.MinLinkPower.ToString("R", c)).Append(',');
            sb.Append(g.MaxLinkPower.ToString("R", c)).Append(',');
            sb.Append(g.MinBroodSize).Append(',').Append(g.MaxBroodSize).Append(',');
            sb.Append(g.MinOffspringEndowment.ToString("R", c)).Append(',');
            sb.Append(g.MaxOffspringEndowment.ToString("R", c)).Append(',');

            for (int i = 0; i < g.JointTypes.Length; i++) sb.Append((int)g.JointTypes[i]).Append('.');
            sb.Append(',');
            for (int i = 0; i < g.BodyCellTypes.Length; i++) sb.Append(g.BodyCellTypes[i]).Append('.');
        }

        public override string ToString() => $"RunConfig({Hash()})";
    }
}
