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
        [TunableGroup]
        public RandomGenomeOptions Genome { get; set; } = RandomGenomeOptions.Default;

        /// <summary>Caps applied while growing a genome into a body — §4.2.</summary>
        [TunableGroup]
        public DevelopmentLimits Development { get; set; } = DevelopmentLimits.Default;

        /// <summary>The geometries available to parts — §4.1.</summary>
        /// <remarks>
        /// Ordered, and the order is hashed: shape mutation picks by an RNG draw, so a registry
        /// rebuilt in a different order yields different shapes from the same seed.
        /// </remarks>
        [TunableRegistry]
        public PartShapeRegistry Shapes { get; set; } = PartShapeRegistry.Standard;

        /// <summary>How often each variation operator fires — §4.5.</summary>
        [TunableGroup]
        public MutationRates Mutation { get; set; } = MutationRates.Default;

        /// <summary>Water: density, drag, added mass — §5.2.</summary>
        [TunableGroup]
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
        [TunableGroup]
        public LightModel Light { get; set; } = new LightModel();

        /// <summary>Water that moves — §5A.4, D036. Still by default.</summary>
        [TunableGroup]
        public CurrentField Current { get; set; } = new CurrentField();

        /// <summary>
        /// The cell types available, their upkeep and their feeding rates — §5A.1.
        /// </summary>
        /// <remarks>
        /// The registry's <i>order</i> is part of the hash as well as its contents, because
        /// cell-type mutation picks by an RNG draw and ordering therefore decides which type a
        /// given draw yields. Two registries holding the same types in a different order are not
        /// interchangeable.
        /// </remarks>
        [TunableRegistry]
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
        [Tunable("economy", Unit = "J")]
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
        [Tunable("economy")]
        public float WorkCostMultiplier { get; set; } = 1f;

        /// <summary>Metabolic joules per neuron per second — §5A.2.</summary>
        /// <remarks>
        /// With <see cref="NeuralCostPerConnectionWatts"/>, this is what prices thinking. Both
        /// exist because a brain that costs nothing grows without limit, in the same way a part
        /// that costs nothing does (§5A.1). ⚠ Unmeasured — §5A.10.
        /// </remarks>
        [Tunable("economy", Unit = "W")]
        public float NeuralCostPerNeuronWatts { get; set; } = 0.05f;

        /// <summary>Metabolic joules per neuron input per second — §5A.2.</summary>
        /// <remarks>
        /// Separate from the per-neuron cost because connections are where the combinatorial
        /// growth is: neurons scale linearly with body size and connections need not.
        /// ⚠ Unmeasured — §5A.10.
        /// </remarks>
        [Tunable("economy", Unit = "W")]
        public float NeuralCostPerConnectionWatts { get; set; } = 0.01f;

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
        [Tunable("population")]
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
        [Tunable("population")]
        public int MaximumPopulation { get; set; } = 5000;

        /// <summary>Most founders the floor may spawn in one step.</summary>
        /// <remarks>
        /// A trickle rather than a cohort. Creatures spawned together tend to die together, which
        /// manufactures a boom-and-bust oscillation that is an artefact of the refill rule rather
        /// than anything the world is doing.
        /// </remarks>
        [Tunable("population")]
        public int FloorSpawnsPerStep { get; set; } = 2;

        /// <summary>
        /// Simulated seconds after which the floor stops intervening, however far the population
        /// has fallen — D021, and the contamination gotcha in CLAUDE.md.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Zero means never</b> — bit-identical to the floor's behaviour before this knob
        /// existed, and the default for exactly that reason. D021 wants the floor to "fire at t=0
        /// and never again"; in every run so far it instead keeps firing through every later
        /// population crash, because <see cref="MinimumPopulation"/> is checked with no memory of
        /// how much time has passed. Each rescue seeds fresh generation-zero founders into a world
        /// that is supposed to be sustaining itself, so a trait's later share is partly a readout
        /// of the founder draw rather than of what survived (CLAUDE.md, "morphology share is
        /// contaminated by the population floor").
        /// </para>
        /// <para>
        /// A positive value closes the floor once <see cref="World.ElapsedSeconds"/> reaches it —
        /// the floor does nothing at all past that point, including refusing to top a population
        /// up from zero. The founding cohort is unaffected: there is no seeding path apart from
        /// the floor (see <see cref="World"/>'s remarks), so it still fires on the world's first
        /// step regardless of this value, provided that step's duration is less than the
        /// threshold — set this below one step's length and there is no founding cohort either.
        /// A world that crashes to zero after the floor closes is allowed to stay at zero; that is
        /// a real outcome, not a bug to paper over.
        /// </para>
        /// </remarks>
        [Tunable("population", Unit = "s")]
        public float FloorClosesAfterSeconds { get; set; } = 0f;

        /// <summary>Joules a floor-spawned founder starts with.</summary>
        /// <remarks>
        /// The only energy in the design created from nothing besides sunlight, so it is counted
        /// as income in the §5A.2 audit. It buys a founder time to establish rather than body —
        /// growth does not exist (§5A.6) — and setting it high enough that founders survive
        /// regardless would make the floor a life-support machine. ⚠ Unmeasured — §5A.10.
        /// </remarks>
        [Tunable("population", Unit = "J")]
        public float FounderEnergyJoules { get; set; } = 200f;

        /// <summary>Depth range founders are scattered through, metres.</summary>
        /// <remarks>
        /// Spread rather than placed at the surface. Starting every founder at depth zero would
        /// hand generation zero the best light in the world and make §5A.2's calibration read as
        /// more generous than it is — and it would remove the depth gradient that §5A.4 says is
        /// what stops one strategy winning everywhere.
        /// </remarks>
        [Tunable("population", Unit = "m")]
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
        [Tunable("world", Unit = "m2")]
        public float WorldAreaSquareMetres { get; set; } = 400f;

        /// <summary>Thickness of one shading layer, metres — <see cref="LightField.LayerMetres"/>.</summary>
        /// <remarks>
        /// A discretisation of who shades whom, so it wants to be near a creature's own size:
        /// bodies are metre-scale (§4.1's dimension range), and a layer much thicker than that
        /// would let a creature shade one floating beside it.
        /// </remarks>
        [Tunable("world", Unit = "m")]
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
        [Tunable("world", Unit = "m")]
        public float WorldDepthMetres { get; set; } = 60f;

        /// <summary>How fast dead matter falls, m/s — §5A.2c, §5A.4.</summary>
        /// <remarks>
        /// The rate that decides whether the deep is a niche or a graveyard. Fast, and everything
        /// reaches the floor before anything in the water column can eat it; slow, and the surface
        /// keeps its own dead and the deep starves. ⚠ Unmeasured — §5A.10.
        /// </remarks>
        [Tunable("world", Unit = "m/s")]
        public float NutrientSinkMetresPerSecond { get; set; } = 0.02f;

        /// <summary>How fast the floor gives detritus back, s⁻¹ — D051.</summary>
        /// <remarks>
        /// <see cref="NutrientSinkMetresPerSecond"/> pays into the floor and never out of it; in
        /// still water a run long enough ratchets every joule onto the sediment. With
        /// <see cref="NutrientMixingDiffusivity"/> above zero the floor already exchanges with the
        /// water above it and this leak is redundant — measured at 0.2 m²/s, logbook/0036.
        /// A rate constant rather than a velocity: the floor is a stock being decayed, not a
        /// distance being crossed, and there is no layer thickness below it for a velocity to
        /// mean anything against. Zero by default, so the world is bit-identical until a run
        /// asks otherwise.
        /// </remarks>
        [Tunable("world", Unit = "1/s")]
        public float NutrientRemineralisationPerSecond { get; set; } = 0f;

        /// <summary>
        /// Matter a child's tissue costs, per joule of that tissue — D048. Zero disables the
        /// whole mechanism.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The point is not the cost, it is who pays it.</b> Until D048 the producer consumed
        /// nothing: <c>PhotosyntheticCell.Acquire</c> returns light and draws no pool, so nothing a
        /// creature did made its own position worse, there was no negative feedback anywhere on
        /// occupying the best spot, and every world sorted to the surface and stayed. The depth
        /// axis was a ramp with its maximum at the boundary rather than a landscape.
        /// </para>
        /// <para>
        /// Reproduction is the right place to charge it, and not only for convenience. §5A.6 has
        /// no growth — tissue is created exactly once, when a child is made — and no amount of
        /// sunlight builds a daughter cell without nitrogen and phosphorus. So a nutrient-starved
        /// world does not kill its inhabitants; it stops them breeding, which is what actually
        /// happens to a nutrient-limited bloom.
        /// </para>
        /// <para>
        /// <b>Zero by default</b>, so §5A.2's ledger and every arm measured before D048 are
        /// unchanged, and a run that turns this on says so in its own header and config hash.
        /// ⚠ The ratio is unmeasured — pick it against
        /// <see cref="InitialMatterPerCubicMetre"/> and read the blocked-conception count, which
        /// is the only number that says whether matter is binding at all.
        /// </para>
        /// </remarks>
        [Tunable("world")]
        public float MatterPerTissueJoule { get; set; }

        /// <summary>Matter the world starts with, per cubic metre — D048.</summary>
        /// <remarks>
        /// Seeded uniformly through the column and thereafter conserved: reproduction removes it
        /// from the layer the parent is in, death returns it to the layer the body is in, and it
        /// sinks and mixes like detritus. Nothing creates it, so the surface is stripped by
        /// whatever succeeds there and the deep is fed by what dies and falls — which is the
        /// ocean's actual vertical structure and the reason gas vesicles exist (D049).
        /// </remarks>
        [Tunable("world")]
        public float InitialMatterPerCubicMetre { get; set; } = 1f;

        /// <summary>How fast matter falls, m/s — D048.</summary>
        /// <remarks>
        /// Separate from <see cref="NutrientSinkMetresPerSecond"/> rather than shared. They
        /// describe different things — corpses carrying energy, and dissolved matter — and a
        /// single knob would make the deep-versus-graveyard trade-off inseparable from the
        /// nutrient gradient this is meant to create.
        /// </remarks>
        [Tunable("world", Unit = "m/s")]
        public float MatterSinkMetresPerSecond { get; set; } = 0.02f;

        /// <summary>How strongly the water stirs matter vertically, m²/s — D048.</summary>
        /// <remarks>
        /// The counterweight to <see cref="MatterSinkMetresPerSecond"/>. With no mixing, matter
        /// drains to the floor and the photic zone becomes permanently sterile — D036's failure,
        /// in the currency that now gates reproduction rather than the one that gates feeding.
        /// </remarks>
        [Tunable("world", Unit = "m2/s")]
        public float MatterMixingDiffusivity { get; set; } = 2f;

        /// <summary>How fast the floor gives matter back, s⁻¹ — D051.</summary>
        /// <remarks>
        /// Matter's own copy of <see cref="NutrientRemineralisationPerSecond"/>, separate for the
        /// same reason <see cref="MatterSinkMetresPerSecond"/> is separate from
        /// <see cref="NutrientSinkMetresPerSecond"/>: dissolved matter and detrital energy are
        /// different pools even though this model conflates particulate and dissolved within
        /// each one. Zero by default, so the world is bit-identical until a run asks otherwise.
        /// </remarks>
        [Tunable("world", Unit = "1/s")]
        public float MatterRemineralisationPerSecond { get; set; } = 0f;

        /// <summary>
        /// How strongly the water stirs detritus vertically, m²/s — §5A.4, D036.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The world's only return path for energy.</b> Without it, light enters at the surface,
        /// bodies sink past everything that could eat them, and every joule the world has ever
        /// received ends on the sea floor: 77.5% of all dead matter on the sediment, and a
        /// measured nutrient density of exactly zero everywhere anything lives (logbook/0021). The
        /// audit balanced perfectly throughout, because the energy was never lost — it was
        /// immobilised.
        /// </para>
        /// <para>
        /// <b>Read it against <see cref="NutrientSinkMetresPerSecond"/>, which is the only thing it
        /// competes with.</b> Diffusion spreads over a distance like the square root of time while
        /// sinking covers one linear in it, so the balance between them is what decides whether
        /// there is a nutrient gradient through the column or a line on the floor. Their ratio has
        /// length units — it is the depth over which mixing wins — and that depth is the thickness
        /// of the habitable layer for anything that eats.
        /// </para>
        /// <para>⚠ Unmeasured (§5A.10). Default 0: the world does not stir until a run asks it to.</para>
        /// </remarks>
        [Tunable("world", Unit = "m2/s")]
        public float NutrientMixingDiffusivity { get; set; }

        /// <summary>
        /// Seconds of life after which a body costs twice as much to keep — DESIGN.md §5A.2, D038.
        /// Zero is an immortal world.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Nothing died of age, and the design already knew.</b> §5A.6b records that "a handful
        /// of immortal generation-zero photosynthesisers pin the minimum at zero permanently", and
        /// the response was to change the instrument (D025) rather than the ecology. But a
        /// creature whose income covers its upkeep simply never dies, so a successful lineage is
        /// never replaced — only added to. Measured: 98 deaths against 1,164 births, an 8% death
        /// rate, with the literal t=0 founders still alive at t=3,500 (logbook/0023). Selection
        /// needs differential mortality as well as differential reproduction, and there was
        /// essentially none.
        /// </para>
        /// <para>
        /// <b>It changes the terms of trade rather than killing anybody.</b> A maximum lifespan
        /// would be an exogenous rule — us deciding how long a creature ought to live, which is
        /// the kind of judgement §5A.0 exists to remove. Senescence as an ageing metabolism keeps
        /// death where §5A.6 puts it: the reserve reaches zero. An old creature starves, and how
        /// long it takes depends on how good it was at earning, which is the world's answer rather
        /// than ours.
        /// </para>
        /// <para>
        /// <b>Both sides of the ledger, from this one number.</b> At age <c>t</c> the wear factor
        /// is <c>1 + t/this</c>: upkeep and neural cost are multiplied by it and income is divided
        /// by it, so an old body spends more <i>and</i> converts less. Costs alone would be the
        /// cheaper implementation and the wrong biology — senescence is loss of function first
        /// and expense second, and a creature that photosynthesised at full efficiency until the
        /// day it starved would be an odd thing to call old. Note that what falls is what a
        /// creature <i>keeps</i>: it still draws the same joules from the pool, and the shortfall
        /// leaves through the transfer loss §5A.3 already accounts for. So an ageing population
        /// depletes the larder exactly as fast while feeding itself worse, which is the
        /// density-dependence §5A.7's ceiling stands in for.
        /// </para>
        /// <para>
        /// <b>Linear, and not heritable.</b> Linear because the doubling time is then a number
        /// with a plain meaning; and not heritable because there is no cost here to repairing
        /// damage, so an evolvable senescence rate would go straight to zero and buy immortality
        /// for free — a §11.2 free lunch arriving through the ledger. Making it evolvable needs
        /// the disposable-soma trade-off, where repair competes with reproduction for the same
        /// joules, and that is a larger design than a knob.
        /// </para>
        /// <para>⚠ Unmeasured (§5A.10). Default 0: nothing ages until a run asks it to.</para>
        /// </remarks>
        [Tunable("world", Unit = "s")]
        public float SenescenceDoublingSeconds { get; set; }

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
        /// <summary>
        /// A stable digest of every tunable — the <c>configHash</c> of §7.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Derived from <see cref="ConfigSchema"/> rather than written out by hand.</b> The
        /// hand-written version required every knob to be listed here as well as on the property,
        /// in the JSON writer and in the JSON reader — around a hundred knobs across four hundred
        /// sites — and both faults §7 exists to catch came from exactly that:
        /// <c>DevelopmentLimits.MaxPartVolume</c> reached two of the four, and
        /// <see cref="Light"/> reached none (logbook/0011, logbook/0013).
        /// </para>
        /// <para>
        /// <b>Sorted by path, never by reflection order.</b> <c>Type.GetProperties()</c> is
        /// documented not to guarantee an order, so a digest taken in discovery order would be
        /// stable on one runtime and silently different on the next — which would turn §7's
        /// promise into one that holds until someone upgrades .NET. The sort is ordinal, over
        /// names, and therefore a property of the code rather than of the host.
        /// </para>
        /// <para>
        /// FNV-1a: a fixed integer recurrence, for the same reason <see cref="Rng"/> is PCG rather
        /// than <c>System.Random</c> — a digest whose algorithm may change between framework
        /// versions cannot identify anything.
        /// </para>
        /// </remarks>
        public string Hash()
        {
            var sb = new StringBuilder();

            foreach (TunableEntry entry in ConfigSchema.Of(this))
            {
                sb.Append(entry.Path).Append('=').Append(entry.Format()).Append('|');
            }

            // The registries are not walkable the same way: their members have constructor-only
            // parameters and their membership varies, so each carries its own contribution. Their
            // *order* is part of it, because mutation picks by an RNG draw and a registry rebuilt
            // in a different order yields different types from the same seed.
            sb.Append(CellTypes.HashContribution()).Append('|');
            sb.Append(Shapes.HashContribution()).Append('|');

            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < sb.Length; i++)
            {
                hash ^= sb[i];
                hash *= 1099511628211UL;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
