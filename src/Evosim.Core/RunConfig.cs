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

        /// <summary>The geometries available to parts — §4.1.</summary>
        /// <remarks>
        /// Ordered, and the order is hashed: shape mutation picks by an RNG draw, so a registry
        /// rebuilt in a different order yields different shapes from the same seed.
        /// </remarks>
        public PartShapeRegistry Shapes { get; set; } = PartShapeRegistry.Standard;

        /// <summary>How often each variation operator fires — §4.5.</summary>
        public MutationRates Mutation { get; set; } = MutationRates.Default;

        /// <summary>Water: density, drag, added mass — §5.2.</summary>
        public FluidConfig Fluid { get; set; } = new FluidConfig();

        /// <summary>How much light reaches each depth — §5A.4, §5A.2b.</summary>
        /// <remarks>
        /// <b>It lives here because it is the most consequential number in the design, and for a
        /// while it was the only one that could not be told apart after the fact.</b>
        /// <see cref="LightModel"/> used to be handed to <c>World</c> alongside a config rather
        /// than inside one, so <see cref="LightModel.SurfaceIrradiance"/> — §5A.2's <i>knob that
        /// decides everything</i> — never reached <see cref="Hash"/>. Every run in the calibration
        /// sweep of §5A.2b, from the extinct end to the runaway end, carried the same
        /// <c>configHash</c>. The whole promise of §7 is that <c>(genome, seed, configHash)</c>
        /// identifies an evaluation, and it did not.
        ///
        /// It escaped the reflection guard for the same reason <see cref="DevelopmentLimits"/>
        /// did, only worse: not a property of this class at all, so nothing walking this class
        /// could have found it. The guard now covers this and every registered cell type
        /// (logbook/0013).
        /// </remarks>
        public LightModel Light { get; set; } = new LightModel();

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
        /// Living creatures below which the population floor spawns founders — §5A.6, D021.
        /// </summary>
        /// <remarks>
        /// <b>Read the floor's firing rate, never just this number.</b> A floor that keeps firing
        /// means the world is not sustaining life, we are — and the run still shows a stable
        /// population, births, deaths and accumulating lineages, every figure consistent with a
        /// working ecosystem and every one of them propped up. The success condition is that this
        /// fires at t=0 and never again; §5A.6b's minimum generation depth is what reports it.
        /// </remarks>
        public int MinimumPopulation { get; set; } = 40;

        /// <summary>
        /// Living creatures above which the world stops and says so — §5A.6, §5A.7, D021.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A hard stop, not a cull.</b> §5A.7's photosynthetic mat does not go extinct, it
        /// explodes — and killing creatures to fit a compute budget is selection performed by us
        /// of the worst kind: arbitrary, invisible in the lineage record, and biased toward
        /// whatever the cull happens to reach first. A world that hits this has told us its
        /// calibration is wrong, and continuing under a cull would hide that behind a population
        /// number we chose.
        /// </para>
        /// <para>
        /// It fires as an exception rather than a flag because a runaway is not a state a caller
        /// can sensibly carry on from, and because every step past the ceiling costs more than
        /// the last — a loop that merely noticed would still be a loop that never returned.
        /// </para>
        /// </remarks>
        public int MaximumPopulation { get; set; } = 5000;

        /// <summary>Most founders the floor may spawn in one step.</summary>
        /// <remarks>
        /// A trickle rather than a cohort. Creatures spawned together tend to die together, which
        /// manufactures a boom-and-bust oscillation that is an artefact of the refill rule rather
        /// than anything the world is doing.
        /// </remarks>
        public int FloorSpawnsPerStep { get; set; } = 2;

        /// <summary>Joules a floor-spawned founder starts with.</summary>
        /// <remarks>
        /// The only energy in the design created from nothing besides sunlight, so it is counted
        /// as income in the §5A.2 audit. It buys a founder time to establish rather than body —
        /// growth does not exist (§5A.6) — and setting it high enough that founders survive
        /// regardless would make the floor a life-support machine. ⚠ Unmeasured — §5A.10.
        /// </remarks>
        public float FounderEnergyJoules { get; set; } = 200f;

        /// <summary>Depth range founders are scattered through, metres.</summary>
        /// <remarks>
        /// Spread rather than placed at the surface. Starting every founder at depth zero would
        /// hand generation zero the best light in the world and make §5A.2's calibration read as
        /// more generous than it is — and it would remove the depth gradient that §5A.4 says is
        /// what stops one strategy winning everywhere.
        /// </remarks>
        public float FounderDepthSpread { get; set; } = 20f;

        /// <summary>Horizontal area of the world, m² — the sun's aperture. DESIGN.md §5A.2b.</summary>
        /// <remarks>
        /// <b>This is the carrying capacity, and it is the only thing that sets one.</b> The world
        /// receives <see cref="LightModel.SurfaceIrradiance"/> × this many watts and no more, so
        /// total photosynthetic income is capped however many creatures there are and whatever
        /// they evolve. Without it a population above break-even grows without bound at every
        /// setting of §5A.2's ratio, which is what the first calibration sweep found
        /// (logbook/0011). Larger worlds support more life in exact proportion; they do not
        /// support a <i>denser</i> one.
        /// </remarks>
        public float WorldAreaSquareMetres { get; set; } = 400f;

        /// <summary>Thickness of one shading layer, metres — <see cref="LightField.LayerMetres"/>.</summary>
        /// <remarks>
        /// A discretisation of who shades whom, so it wants to be near a creature's own size:
        /// bodies are metre-scale (§4.1's dimension range), and a layer much thicker than that
        /// would let a creature shade one floating beside it.
        /// </remarks>
        public float LightLayerMetres { get; set; } = 1f;

        /// <summary>How deep the world is, metres — DESIGN.md §5A.2c.</summary>
        /// <remarks>
        /// <b>The world's first vertical bound, and it exists because detritus has to land
        /// somewhere.</b> Light needs no floor: it attenuates exponentially and simply gets darker
        /// forever. A sinking pool does need one, because energy that falls past the last layer
        /// would vanish, and vanished energy is what §5A.2's audit exists to notice. The deepest
        /// layer is the sea floor and detritus accumulates on it — which is where a scavenging
        /// niche would live, if one evolves.
        /// </remarks>
        public float WorldDepthMetres { get; set; } = 60f;

        /// <summary>How fast dead matter falls, m/s — §5A.2c, §5A.4.</summary>
        /// <remarks>
        /// The rate that decides whether the deep is a niche or a graveyard. Fast, and everything
        /// reaches the floor before anything in the water column can eat it; slow, and the surface
        /// keeps its own dead and the deep starves. ⚠ Unmeasured — §5A.10.
        /// </remarks>
        public float NutrientSinkMetresPerSecond { get; set; } = 0.02f;

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
            sb.Append(Shapes.HashContribution()).Append('|');
            sb.Append(Light.HashContribution()).Append('|');
            sb.Append(Fluid.Density.ToString("R", c)).Append(',');
            sb.Append(Fluid.DragCoefficient.ToString("R", c)).Append(',');
            sb.Append(Fluid.AddedMassCoefficient.ToString("R", c)).Append(',');
            sb.Append(Fluid.PanelsPerAxis).Append('|');
            sb.Append(Development.MaxParts).Append(',');
            sb.Append(Development.MaxDepth).Append(',');
            sb.Append(Development.MinPartVolume.ToString("R", c)).Append(',');
            sb.Append(Development.MaxPartVolume.ToString("R", c)).Append(',');
            sb.Append(Development.MinPartHalfExtent.ToString("R", c)).Append('|');
            sb.Append(PerOffspringOverheadJoules.ToString("R", c)).Append(',');
            sb.Append(WorkCostMultiplier.ToString("R", c)).Append(',');
            sb.Append(NeuralCostPerNeuronWatts.ToString("R", c)).Append(',');
            sb.Append(NeuralCostPerConnectionWatts.ToString("R", c)).Append(',');
            sb.Append(MinimumPopulation).Append(',');
            sb.Append(MaximumPopulation).Append(',');
            sb.Append(FloorSpawnsPerStep).Append(',');
            sb.Append(FounderEnergyJoules.ToString("R", c)).Append(',');
            sb.Append(FounderDepthSpread.ToString("R", c)).Append(',');
            sb.Append(WorldAreaSquareMetres.ToString("R", c)).Append(',');
            sb.Append(LightLayerMetres.ToString("R", c)).Append(',');
            sb.Append(WorldDepthMetres.ToString("R", c)).Append(',');
            sb.Append(NutrientSinkMetresPerSecond.ToString("R", c)).Append(',');
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
            sb.Append(m.ShapeChance.ToString("R", c)).Append(',');
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
            sb.Append(g.FounderTailChance.ToString("R", c)).Append(',');

            for (int i = 0; i < g.JointTypes.Length; i++) sb.Append((int)g.JointTypes[i]).Append('.');
            sb.Append(',');
            for (int i = 0; i < g.BodyCellTypes.Length; i++) sb.Append(g.BodyCellTypes[i]).Append('.');
            sb.Append(',');
            for (int i = 0; i < g.ShapeIdChoices.Length; i++) sb.Append(g.ShapeIdChoices[i]).Append('.');
            sb.Append(',');
            for (int i = 0; i < g.FounderCellTypes.Length; i++) sb.Append(g.FounderCellTypes[i]).Append('.');
        }

        public override string ToString() => $"RunConfig({Hash()})";
    }
}
