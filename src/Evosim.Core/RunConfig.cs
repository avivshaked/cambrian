using System;
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

        /// <summary>
        /// Standing tissue joules across the living above which the world stops and says so, in
        /// addition to <see cref="MaximumPopulation"/>. Rule 9, the owner's ruling of 2026-09-09
        /// after logbook/0081.
        /// </summary>
        /// <remarks>
        /// <b>0 means off.</b> Growth (D081) made a body count stop measuring biomass: a child is
        /// born at a median third of its adult body, so 5,000 grown bodies and 5,000 newborns are
        /// not the same photosynthetic mat, and the count ceiling alone can no longer tell them
        /// apart. This reads <see cref="World.StandingTissueJoules"/> instead of the count, the
        /// same check §5A.7 was built to make, against the quantity growth left it blind to. §9's
        /// refuse-rather-than-default rule applies here like any other tunable: a config written
        /// before this field exists is refused on load, not silently run with it off.
        /// </remarks>
        [Tunable("population", Unit = "J")]
        public double MaximumTissueJoules { get; set; }

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
        /// as income in the §5A.2 audit. Setting it high enough that founders survive regardless
        /// would make the floor a life-support machine.
        /// <para>
        /// It buys time, and since fable-propose-growth.md rule 5 (2026-09-08) time is also how a
        /// founder buys body: a founder is admitted at its genome's birth fraction, holding this
        /// scaled by that fraction, and it grows into its adult size out of what it earns. So this
        /// is still not a body — nobody is handed one — but it is now the runway a founder has to
        /// earn one on, and a value that leaves a founder starving before it can grow is a
        /// different world from one that lets it reach adulthood. ⚠ Unmeasured — §5A.10.
        /// </para>
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

        /// <summary>
        /// How far a newborn may be set down from its parent, metres. 0 is D077's rule exactly:
        /// touching, at a random compass angle.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Nothing in the world moved a sitter sideways until this knob.</b> A newborn is
        /// placed against its parent (D077 rule 5), the rolling current returns a body to where
        /// it found it by design (D037), and no other rule touches x or z. So a clade is a column
        /// packed around the spot its founder landed on, for the whole run. On 2026-09-10 the
        /// theatre showed round 33 seed 3 as two vertical ribbons about a metre wide, and no
        /// column in the record could have shown it: the report carries a mean depth and per-patch
        /// bins and nothing about x or z. Since the grid (D086) a mouth drains the one cell it
        /// stands in, so a ribbon like that drains the same handful of cells for thirty thousand
        /// seconds.
        /// </para>
        /// <para>
        /// <b>A propagule in the sea drifts before it settles</b>, which is the biology this
        /// stands in for. Above 0 the child's horizontal position is drawn uniformly over a disc
        /// of this radius about the parent, uniform in area rather than in radius, and never
        /// closer than the two bounding spheres allow. The depth is still the parent's, because
        /// that is the depth the parent's income was earned at.
        /// </para>
        /// <para>
        /// Default 0, so every launcher in the record still describes the world it ran, and the
        /// placer takes the same branch it always took. The rule itself lives in
        /// <c>Evosim.Sim</c>'s <c>SharedVolume</c>, with the rest of the box.
        /// <c>EVOSIM_OFFSPRING_DISPERSAL</c> in the header, which is not
        /// <c>EVOSIM_DISPERSAL</c>: that name already belongs to
        /// <see cref="DispersalChancePerStep"/>, D061's retired patch lottery, and one name for
        /// two knobs would set both from one launcher.
        /// </para>
        /// </remarks>
        [Tunable("population", Unit = "m")]
        public float OffspringDispersalMetres { get; set; }

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

        /// <summary>
        /// Whether creatures share one literal volume — D077's footprint world. False is every
        /// run before D077: the tiled lattice, the inherited patch index and the two transport
        /// lotteries, unchanged to the character.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>One switch over six rules, because they are one world.</b> True makes the box
        /// literal — <see cref="HorizontalPatches"/> regions of
        /// <c>sqrt(WorldAreaSquareMetres / K)</c> metres side by side on a ring,
        /// <see cref="WorldDepthMetres"/> deep — and with it: a creature's patch is read from
        /// where its root actually is rather than inherited at birth; the horizontal boundary is
        /// periodic, so a body that leaves the box is translated back in at the far face;
        /// <see cref="DispersalChancePerStep"/>'s lottery and D066's body-transport lottery are
        /// retired, because bodies now change patch by being moved; and a newborn is placed
        /// beside its parent, overlap-free, rather than on a lattice a hundred metres away.
        /// A birth with nowhere to go is a counted <i>crowded</i> stillbirth
        /// (<see cref="World.CrowdedStillbirths"/>).
        /// </para>
        /// <para>
        /// <b>False is not "the same world with the geometry switched off".</b> It is the
        /// historical record: the two retired lotteries draw from the RNG stream, so a world that
        /// skipped them would not replay, and placement changes where every body starts. The two
        /// branches are therefore whole worlds, and only the false one is bit-identical to what
        /// is on file.
        /// </para>
        /// <para>
        /// The physical placement rule is <c>Evosim.Sim</c>'s — §6.1 forbids
        /// <c>UnityEngine</c> here and a box is a set of coordinates — so <see cref="World"/>
        /// asks an <see cref="IBodyPlacement"/> for room and knows nothing else about it.
        /// <c>EVOSIM_SHARED_SPACE</c> in the header.
        /// </para>
        /// </remarks>
        [Tunable("world")]
        public bool SharedSpace { get; set; }

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

        /// <summary>
        /// Matter every child costs on top of <see cref="MatterPerTissueJoule"/> × tissue,
        /// regardless of how small its body is — D065.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A body costs a minimum of matter to exist.</b> Real cells carry a machinery mass —
        /// genome, membrane, ribosomes — that no amount of shrinking removes, so the price of
        /// being alive is not proportional to size all the way down to zero. With the price purely
        /// proportional, a lineage can pay for one more individual by making every individual
        /// smaller, and the population count ratchets upward without bound while the standing
        /// tissue stays flat: matter limits mass, and nothing limits number.
        /// </para>
        /// <para>
        /// <b>What the fixed term buys is a ceiling on head-count.</b> With it, the number of
        /// bodies the world can hold is bounded by total matter / (fixed + proportional), which is
        /// finite however small bodies get — so selection for miniaturisation runs into a wall
        /// rather than an open ramp, and crowding becomes a cost a lineage can be selected against.
        /// </para>
        /// <para>
        /// Charged at conception from the parent's own layer, locked into the child's
        /// <see cref="Organism.LockedMatter"/> alongside the proportional part, and returned by the
        /// same excretion (<see cref="ExcretionPerJoule"/>) and death legs — the fixed term is not
        /// a separate substance, only a second term in one price.
        /// </para>
        /// <para>⚠ Unmeasured (§5A.10). <b>Zero by default</b>, so every world measured before D065
        /// is bit-identical; a run that turns it on says so in its own header and config hash.</para>
        /// </remarks>
        [Tunable("world")]
        public float MatterPerCreature { get; set; }

        // ---------------------------------------------------------------- growth
        //
        // fable-propose-growth.md (2026-09-08). A child is born at a fraction of its adult body
        // and grows into the rest, so three things the world used to be able to assume are now
        // decisions: how much of its start is reserve rather than body, how much reserve a
        // growing body keeps back, and how small a newborn part the physics will accept. None of
        // them is in the genome, and each has its own reason not to be.

        /// <summary>
        /// The share of a newborn's start it holds as reserve rather than body, 0 to 1 — rule 2.
        /// </summary>
        /// <remarks>
        /// <b>Out of the genome deliberately.</b> A child's whole start is its parent's investment
        /// over the litter, and this is where the split between "body" and "something to live on"
        /// is made. A lineage allowed to set it would set it to zero, hand every child a body with
        /// no reserve, and pocket the difference as extra size — children that are born larger and
        /// starve before their first meal, which is not a strategy the world should be able to
        /// express. ⚠ Unmeasured (§5A.10); 0.2 is the proposal's first value.
        /// </remarks>
        [Tunable("growth")]
        public float NewbornReserveFraction { get; set; } = 0.2f;

        /// <summary>
        /// Reserve a growing body keeps rather than investing, as a fraction of its own current
        /// tissue value — rule 5.
        /// </summary>
        /// <remarks>
        /// <b>Growth is a transfer and it comes first, so without a floor it would be total.</b> A
        /// body below its adult size moves everything above this into tissue every step, which is
        /// what makes "grow first, breed later" fall out of the ordering rather than being
        /// declared as a rule. The floor is what stops a growing creature from starving itself the
        /// instant the water goes quiet: it is a buffer measured against the body it already has,
        /// so a large body keeps a large one.
        /// <para>
        /// <b>The arithmetic is thin and the number should be read knowing it.</b> Tissue is worth
        /// about 500 J per cubic metre of body and upkeep runs 3 to 4 W per cubic metre, so a
        /// reserve of 0.1 of tissue value is 50 J against 3 to 4 W: twelve to seventeen seconds of
        /// upkeep, whatever the body's size, because both terms scale with volume. A growing body
        /// therefore sits at the edge of starvation for as long as it is growing, and a quiet
        /// patch of water for one sampling interval kills it. That is a real cost of growing fast
        /// rather than an oversight, but it is the owner's to raise. ⚠ Unmeasured (§5A.10); 0.1 is
        /// the proposal's first value.
        /// </para>
        /// </remarks>
        [Tunable("growth")]
        public float GrowthReserveFloor { get; set; } = 0.1f;

        /// <summary>
        /// Lightest part a newborn may be built with, kg. A conception producing anything lighter
        /// does not happen — rule 3.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A refusal, not a clamp, and the divergence record is why.</b> Every divergence on
        /// file was a newborn, and the lightest link among them weighed 0.143 kg (logbook/0059,
        /// logbook/0077): a light body driven by a joint sized for an adult is what spins up by
        /// thousands of radians a second in one step. Growth makes small bodies ordinary rather
        /// than rare, so the guard has to exist before the round does. Clamping the child up to
        /// the floor instead would silently hand a lineage a bigger child than it paid for, which
        /// is free energy; refusing leaves the parent holding its reserve and makes a genome whose
        /// investment over its litter sits below the floor simply childless, which selection can
        /// see.
        /// </para>
        /// <para>
        /// Mass is <see cref="PartDensityKilogramsPerCubicMetre"/> times the part's volume, which
        /// is the number the harness gives the articulation. ⚠ Unmeasured (§5A.10); 0.5 kg is the
        /// proposal's first value, and the first screens read the <c>diverged</c> column against
        /// it.
        /// </para>
        /// </remarks>
        [Tunable("growth", Unit = "kg")]
        public float MinNewbornPartKilograms { get; set; } = 0.5f;

        /// <summary>
        /// How often the harness applies a grown body to the physics, seconds — rule 8.
        /// </summary>
        /// <remarks>
        /// <b>Read by the harness and by nothing in this assembly.</b> The economy grows a body on
        /// every metabolic step, because growth is a transfer and a transfer paid at a cadence is
        /// a transfer that has to be reconciled; what happens at a cadence is the expensive half,
        /// resizing colliders, masses, anchors and drag panels on a live articulation. So Core
        /// moves the joules and the harness reads <see cref="Organism.BodyFraction"/> when this
        /// says to. It is a tunable rather than a constant in the harness because it changes what
        /// the physics does, and §7 says anything that changes a trajectory belongs in the hash.
        /// </remarks>
        [Tunable("growth", Unit = "s")]
        public float GrowthStepSeconds { get; set; } = 10f;

        /// <summary>
        /// What a body part weighs per cubic metre, kg/m³ — the density the harness builds
        /// articulation bodies at.
        /// </summary>
        /// <remarks>
        /// <b>A constant, not a tunable, and a duplicate on purpose.</b> The authority is
        /// <c>PhenotypeBuilder.DensityKgPerM3</c> in <c>Evosim.Sim</c>, which Core may not
        /// reference (§6.1's no-<c>UnityEngine</c> rule runs in this direction too). Core needs the
        /// number only to read <see cref="MinNewbornPartKilograms"/> as a mass, and a knob here
        /// would be a second answer to a question the physics has already settled: water is 1000
        /// kg/m³ and a neutrally buoyant body is the same.
        /// </remarks>
        public const float PartDensityKilogramsPerCubicMetre = 1000f;

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

        /// <summary>
        /// The matter the world is seeded with, in units. 0 (the default) is
        /// <see cref="InitialMatterPerCubicMetre"/> times the live volume, which is every run on
        /// file; above 0 it is the total, and the density is derived from it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>It decouples the area dial from the matter budget</b> —
        /// <c>fable-propose-aquarium.md</c> ruling 2. Until this knob the two moved together:
        /// <see cref="WorldAreaSquareMetres"/> multiplies the volume and the seed is a density,
        /// so a world four times the footprint held four times the matter and therefore about
        /// four times the bodies. That makes dilution unaskable, and dilution is what the
        /// movement rounds need: round 35's median nearest neighbour is 0.63 to 0.70 m, a body
        /// every metre in the upper band, and with food within a body length in every direction a
        /// sitter eats as well as a swimmer. Held at a total, four times the area is a quarter of
        /// the density at the same number of bodies, and compute follows bodies rather than cubic
        /// metres.
        /// </para>
        /// <para>
        /// <b>Only the matter.</b> The detritus seed is unchanged, because detritus is not seeded
        /// — the light makes it — and the one quantity a closed world's population is denominated
        /// in is its matter (D048, D074).
        /// </para>
        /// <para>
        /// <b>The live volume, not the box's.</b> In a <see cref="WorldShape.Tank"/> on a
        /// <see cref="MatterField.Grid"/> the divisor is the cells the mask calls live times the
        /// cell volume, so the seeded total is the number asked for whatever the circle cuts off
        /// the corners of the array. <c>EVOSIM_MATTER_BUDGET</c> in the header, beside
        /// <c>matter</c>'s own token.
        /// </para>
        /// </remarks>
        [Tunable("world", Unit = "units")]
        public float MatterBudgetUnits
        {
            get => _matterBudgetUnits;
            set => _matterBudgetUnits = value >= 0f && !float.IsInfinity(value) && !float.IsNaN(value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(MatterBudgetUnits), value,
                    "A budget is finite and not negative; 0 is the density rule.");
        }

        private float _matterBudgetUnits;

        /// <summary>
        /// The container the world is: D077's periodic box, or the tank —
        /// <c>fable-propose-aquarium.md</c> ruling 1. <see cref="WorldShape.Box"/> by default, so
        /// every recorded world is the one it always was.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The wrap became visible when the water started carrying.</b> The box is periodic on
        /// both horizontal axes and D077 chose that while nothing crossed a seam; since D088's
        /// transport field a body crosses one about every hundred seconds (<c>r36-s1</c>: 1,143
        /// wraps per window at 1,154 alive), and the owner watching round 36 seed 1 in the
        /// theatre saw bodies displaced — a jump, not a death and a birth. A wall is the honest
        /// container for water that carries.
        /// </para>
        /// <para>
        /// <b>What the shape changes, and it is one change:</b> the footprint is a disc of
        /// <c>R = sqrt(area/π)</c> in a bounding square <c>[0, 2R)²</c>
        /// (<see cref="TankGeometry"/>); the patches are rings of equal area rather than a row of
        /// squares; the horizontal boundary is a static collider rather than a translation, so
        /// <c>wraps</c> reads 0 by construction; the grid carries a mask and stirs and advects
        /// only between live cells; the current is a spectrum of streams, tangential at the glass
        /// by construction; and the placer draws founders over the disc. The area, the depth, the
        /// patch count, the prices and every other rule are untouched.
        /// </para>
        /// <para>
        /// <b>Refused rather than half-built.</b> <see cref="World"/> refuses a tank on
        /// <see cref="MatterField.Cells"/> (a one-dimensional ring of patches is not an annulus)
        /// or on <see cref="MatterField.Vertices"/> (a set of positions in a periodic box, with no
        /// mask and a seam where the glass is),
        /// a tank with <see cref="PatchesAcross"/> above 1 (a layout is the box's), a tank with
        /// D061's dispersal lottery (the same ring), and a tank under
        /// <see cref="CurrentMode.Rolls"/> at a nonzero speed (the rolls are a field over the
        /// box's patches). <c>EVOSIM_SHAPE</c> in the header, which names the tank as
        /// <c>space tank r=5.64 m (100 m2), depth 60, wall, bed</c>.
        /// </para>
        /// </remarks>
        [Tunable("world")]
        public WorldShape WorldShape { get; set; } = WorldShape.Box;

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
        /// <para>
        /// The counterweight to <see cref="MatterSinkMetresPerSecond"/>. With no mixing, matter
        /// drains to the floor and the photic zone becomes permanently sterile — D036's failure,
        /// in the currency that now gates reproduction rather than the one that gates feeding.
        /// </para>
        /// <para>
        /// <b>On a grid this is the rate on every axis, not the vertical one alone.</b>
        /// <see cref="HorizontalMixingDiffusivity"/> is one knob for two substances and it belongs
        /// to the detritus, which is what a run header's h-mix token names. A cubic cell has no
        /// preferred axis (D084), so <c>World.Step</c> hands the matter grid this rate sideways as
        /// well, and the number a grid world stirs its matter across the ring at is stated here
        /// rather than in a second knob nobody sets.
        /// </para>
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
        /// Free matter added to the world every second — D074's influx. Zero is every run before
        /// it, in which matter is conserved and nothing but death gives any back.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Matter has never had a source.</b> <see cref="InitialMatterPerCubicMetre"/> seeds a
        /// stock at construction and D048 conserves it from there: reproduction locks it into a
        /// body, death returns it, and the total never moves. Energy is not run that way — light
        /// enters at the surface every step and respiration takes it out again — and the asymmetry
        /// is why a world can hold full stomachs in full water and still refuse them children
        /// (logbook/0054's failing seed). This is the missing leg, with
        /// <see cref="MatterBurialPerSecond"/> as its outflow: rivers and dust in at the top,
        /// sediment out at the bottom, the same open-budget shape §5A.2 already gives the joule.
        /// </para>
        /// <para>
        /// <b>Deposited per metabolic step</b>, this many units times the step's seconds, before
        /// the field settles — see <see cref="MatterInfluxAt"/> for where. It is not a density:
        /// scaling the world's area does not scale the influx, because the number is a statement
        /// about what the world receives rather than about how thick the water is.
        /// </para>
        /// </remarks>
        [Tunable("world", Unit = "matter/s")]
        public float MatterInfluxPerSecond { get; set; } = 0f;

        /// <summary>
        /// Where <see cref="MatterInfluxPerSecond"/> lands — D074. Default
        /// <see cref="Evosim.Core.MatterInflux.Surface"/>.
        /// </summary>
        /// <remarks>
        /// The two routes matter takes into a real ocean, and they arrive at opposite ends of the
        /// column: rivers and dust at the top, where the light already is, and hydrothermal supply
        /// at the bottom, where D067's plume is already carrying water upward. Which one a world
        /// gets is a world rule, so it is a knob rather than a constant — and at influx 0 both
        /// members are the same world, bit for bit.
        /// </remarks>
        [Tunable("world")]
        public MatterInflux MatterInfluxAt { get; set; } = MatterInflux.Surface;

        /// <summary>
        /// The fraction of each patch's floor-layer free matter that leaves the world every
        /// second — D074's burial. Zero is every run before it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The outflow, and the reason an influx does not simply accumulate.</b> An open budget
        /// with a source and no sink is a ramp: whatever the influx is, the standing stock grows
        /// without bound and the matter gate stops binding at some time nobody chose. A
        /// first-order loss from the floor gives the pool an equilibrium instead — the stock at
        /// which burial equals influx — and makes the sea floor a place matter can actually be
        /// lost, which is what sediment is.
        /// </para>
        /// <para>
        /// <b>Free matter only, and only from the floor layer.</b> Never detritus (that is
        /// <see cref="World.Nutrients"/>' pool and a different substance), and never matter locked in a
        /// living body (<see cref="World.MatterInBodies"/>) — burying a creature's skeleton out
        /// from under it while it is still alive would open the matter identity, and the identity
        /// is the only thing that can catch this mechanism going wrong.
        /// </para>
        /// </remarks>
        [Tunable("world", Unit = "1/s")]
        public float MatterBurialPerSecond { get; set; } = 0f;

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
        /// Matter a living body returns per joule of upkeep it pays — D052.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Turnover, not death.</b> Until D052 matter left a body only once, at death
        /// (<see cref="MatterPerTissueJoule"/>'s reverse leg), and a body sinks after it dies — so
        /// nothing a living creature took at the surface ever came back to the surface, and a
        /// drought there outlasted a lifetime (logbook/0037–0039). This pays a fraction of it back
        /// continuously, at the body's own depth, in proportion to <c>EnergyLedger.Upkeep</c> —
        /// the microbial loop at its simplest.
        /// </para>
        /// <para>
        /// <b>Capped at what the body still holds, and the child's price is unchanged.</b> A body
        /// cannot excrete matter it does not have, so the amount taken each step is
        /// <c>min(locked, this × upkeep)</c>; death still returns whatever is left. Reproduction
        /// still prices a child at <see cref="MatterPerTissueJoule"/> per joule of tissue, paid
        /// from the parent's layer as before — a parent that has excreted most of its own matter
        /// has not thereby made its children cheaper.
        /// </para>
        /// <para>⚠ Unmeasured — no primary source on new versus regenerated production has been
        /// added to the review yet (D052). Zero by default, so the world is bit-identical until a
        /// run asks otherwise; <c>EVOSIM_EXCRETION</c> in the header.</para>
        /// </remarks>
        [Tunable("world", Unit = "matter/J")]
        public float ExcretionPerJoule { get; set; }

        /// <summary>
        /// Thickness of the seabed no mouth can reach, metres — D055. Zero is today's floor: every
        /// layer of <see cref="NutrientField"/>'s detritus is grazeable, including the one resting
        /// on the sediment.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A refuge for the food, not the feeder.</b> `AbsorptiveCell` captures at a fixed
        /// clearance whatever the density is, so a consumer-resource cycle here has no damping:
        /// seven rounds established a lineage that ate the deep water down to a bust
        /// (logbook/0041). Burying the floor layer beyond any mouth's reach gives the pool an
        /// inaccessible reserve that refills the water above it at a rate the gradient sets, not
        /// the consumer — the classic prey-refuge stabiliser. <c>Deposit</c>, <c>Settle</c>,
        /// <c>Mix</c> and <c>Remineralise</c> are untouched, so matter still arrives, piles and
        /// leaves exactly as before; only what feeding can price changes.
        /// </para>
        /// <para>
        /// <b>Applies to <see cref="World.Nutrients"/> only, not <see cref="World.Matter"/>.</b>
        /// Nobody grazes the matter field — it is drawn at conception, a payment rather than a
        /// meal — so a matter refuge would only sterilise a bottom band with nothing to damp.
        /// </para>
        /// <para>
        /// <b>Metres, not a boolean.</b> The tunable machinery is float-typed throughout, "how
        /// thick is the ungrazeable sediment" is the honest physical quantity, and a thicker
        /// refuge is the obvious next dose if one layer's larder proves too small.
        /// </para>
        /// <para>⚠ Project inference from general consumer–resource theory, not a source the
        /// literature review has searched (D055). Zero by default, so the world is bit-identical
        /// until a run asks otherwise; <c>EVOSIM_FLOOR_REFUGE</c> in the header.</para>
        /// </remarks>
        [Tunable("world", Unit = "m")]
        public float FloorRefugeMetres { get; set; }

        /// <summary>
        /// How the water holds what is dissolved and suspended in it — D083. <see cref="MatterField.Cells"/>
        /// is every recorded run: one number per layer and patch. <see cref="MatterField.Vertices"/>
        /// is a set of vertices each holding joules at a position, read through a kernel.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Cells price a body's water at the cell's number</b>, so a body that stays does not
        /// deplete its own water and one that moves does not refresh it; undirected movement earns
        /// nothing and selection deletes the muscles (DESIGN §0r). Vertices give a body a hole to
        /// eat and to leave, and give the field a gradient at the kernel's scale. Requires
        /// <see cref="SharedSpace"/>: a vertex is somewhere, and so must the bodies be.
        /// </para>
        /// <para>Default Cells, so every launcher on file still describes the world it ran.</para>
        /// </remarks>
        [Tunable("field")]
        public MatterField FieldModel { get; set; } = MatterField.Cells;

        /// <summary>Reach of a mouth and of a density read, m — the kernel's support (D083).</summary>
        /// <remarks>
        /// Larger is smoother and less local; smaller is noisier and needs more vertices to read
        /// cleanly. A read fluctuates as <c>1/√n</c> in the vertices it covers; at 1 m over water
        /// seeded at <see cref="FieldVertexJoules"/> per vertex the kernel holds about thirty.
        /// ⚠ Unmeasured (§5A.10).
        /// </remarks>
        [Tunable("field", Unit = "m")]
        public float FieldKernelMetres { get; set; } = 1f;

        /// <summary>
        /// The matter field's own reach, m — how far a conception gathers matter from, and the
        /// reach of the matter density a report reads (D083, amended the same night).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The base world's headroom, kept.</b> A child costs 8 to 16 units of matter at
        /// round 29's prices, and a cell 5 m by 5 m by 1 m held 25 at the seeded density, so
        /// the gate in <c>World.Conceive</c> had headroom for one child in fresh water. The
        /// detritus kernel of 1 m reaches 4.2 m³, which is 4 units at the same density, and the
        /// first screen on seed 2 bred nothing in 10,000 s for that reason alone: every founder
        /// stood in water that could never afford its child (logbook/0074). At 1.8 m the reach is
        /// 24.4 m³, the cell's volume to within 3%, so the matter gate binds where it bound in
        /// the base world and the change the round reads is the detritus, not the matter.
        /// </para>
        /// <para>⚠ Unmeasured (§5A.10), like the detritus kernel; it is the cell's volume, not a
        /// measured gathering radius.</para>
        /// </remarks>
        [Tunable("field", Unit = "m")]
        public float FieldMatterKernelMetres { get; set; } = 1.8f;

        /// <summary>
        /// Where <see cref="VertexField.Cull"/> starts merging nearest pairs when the count is
        /// over <see cref="FieldVertexCap"/>, m — it widens from here toward the kernel (D083).
        /// </summary>
        /// <remarks>Nothing merges below the cap: a deposit joins the nearest vertex within the
        /// kernel, and vertices that drift together stay two. ⚠ Unmeasured (§5A.10).</remarks>
        [Tunable("field", Unit = "m")]
        public float FieldMergeMetres { get; set; } = 0.25f;

        /// <summary>
        /// The count <see cref="VertexField.Cull"/> merges down toward, per field (D083).
        /// </summary>
        /// <remarks>A budget on the bill rather than on the physics: over it, the merge radius
        /// widens toward the kernel until the count fits, which coarsens the field where it is
        /// densest. The report's <c>vtx</c> column says how close a run sits to it.</remarks>
        [Tunable("field")]
        public int FieldVertexCap { get; set; } = 100000;

        /// <summary>
        /// Joules in one emitted vertex — what the vent and the surface influx found a vertex
        /// with — and the mass the seeded lattice is spaced for (D083).
        /// </summary>
        /// <remarks>At <see cref="InitialMatterPerCubicMetre"/> 1 the default spaces the seed
        /// lattice at 0.5 m, some fifty thousand vertices in a 10 by 10 by 60 m column.
        /// ⚠ Unmeasured (§5A.10).</remarks>
        [Tunable("field", Unit = "J")]
        public float FieldVertexJoules { get; set; } = 0.125f;

        /// <summary>
        /// The side of a detritus grid cell, m. <see cref="MatterField.Grid"/> only
        /// (<c>fable-propose-grid.md</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is the whole resolution of the water, bought once.</b> Inside one cell the
        /// density is one number, so the cell is how big a hole a still mouth can eat and how far
        /// a mover has to go to find fresh water. The proposal's own argument sets it: the cell is
        /// chosen at the body's scale, so that an eater drains the cell it stands in while the cell
        /// beside it keeps its water, and a mover that crosses one boundary finds a full one. A
        /// metre is about a body length at round 30's sizes.
        /// </para>
        /// <para>
        /// <b>It is not the vertex field's kernel and must not be read as one.</b>
        /// <see cref="FieldKernelMetres"/> is a support radius: at 1 m it reaches 4.2 m³, four
        /// times what a 1 m cell holds. The same number in the two knobs therefore offers a mouth
        /// four times the water on the vertex field, so a grid round at 1 m is not a vertex round
        /// at 1 m with the samples tidied into rows.
        /// </para>
        /// <para>
        /// <b>It must divide the box on all three axes</b>, or <see cref="GridField"/> refuses the
        /// world: a part cell at a seam holds less than a whole one and would be priced as a whole
        /// one. The box is <c>K·sqrt(area/K)</c> long, <c>sqrt(area/K)</c> wide and
        /// <see cref="WorldDepthMetres"/> deep.
        /// </para>
        /// <para>
        /// <b>It also sets the mixing step.</b> Explicit diffusion on a grid is stable while
        /// <c>D·dt/cell²</c> stays below 1/6, so at 0.2 m²/s and a 1 m cell the metabolic step
        /// has to be under 0.83 s; the field refuses a longer one rather than quietly clamping.
        /// ⚠ Unmeasured (§5A.10).
        /// </para>
        /// </remarks>
        [Tunable("field", Unit = "m")]
        public float FieldCellMetres { get; set; } = 1f;

        /// <summary>
        /// The side of a free-matter grid cell, m. <see cref="MatterField.Grid"/> only.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Coarser than the detritus grid, and for the reason D083 gave its matter kernel a
        /// wider reach.</b> A child costs 8 to 16 units of matter at round 29's prices and water
        /// at the seeded density holds about one per cubic metre, so a 1 m cell could never afford
        /// a conception and the first vertex screen on seed 2 bred nothing in 10,000 s for exactly
        /// that (logbook/0074). At 3 m the cell is 27 m³, about a tenth over the old cell's
        /// 24.4 m³, so the matter gate binds close to where it bound in the base world.
        /// </para>
        /// <para>
        /// <b>Same two rules as <see cref="FieldCellMetres"/></b>: it must divide the box, and it
        /// sets its own mixing limit, which a wider cell makes far easier to satisfy. The default
        /// matter diffusivity is 2 m²/s, which at a half-second step needs a cell wider than
        /// 2.45 m; 3 m clears it and 1 m does not, which is a second reason the two grids differ.
        /// </para>
        /// <para>
        /// <b>The campaign's box admits no cell that leaves the gate where it was, and choosing
        /// between the two it does admit is a world rule rather than a default.</b> Rounds 28 to
        /// 31 run 100 m² over four patches and 60 m deep, which is a box 20 m long, 5 m wide and
        /// 60 m deep; 3 m divides only the depth, so a grid world at this default is refused at
        /// construction. What divides all three is 2.5 m and 5 m, and neither is 24.4 m³. At 2.5 m
        /// the cell holds 15.6 m³, about 15 units at the seeded density, which caps the largest
        /// child the world can afford, since a child costs 8 to 16. At 5 m it holds 125 m³ and
        /// loosens the gate about five times over. A capped gate and a slack gate are different
        /// ecologies, so which one runs is the owner's to say. ⚠ Unmeasured (§5A.10).
        /// </para>
        /// </remarks>
        [Tunable("field", Unit = "m")]
        public float FieldMatterCellMetres { get; set; } = 3f;

        /// <summary>
        /// How fast a corpse hands its remaining tissue and matter to the water it is in, per
        /// second. <c>fable-propose-grid.md</c> rule 6. Zero is every run on file: a death
        /// deposits the whole body at once and no corpse exists.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A corpse that stays an object is something a body can reach.</b> Death is the one
        /// large parcel of food this ecology makes, and today it is dissolved into the water on
        /// the step the body dies, at which point it is a density like any other and a sitter
        /// reads it as well as a swimmer does. Held as a particle it sinks, drifts and stays worth
        /// crossing a cell for, which is the first prize a mover could win that a sitter cannot
        /// have (the proposal's closing). Exudation (D070) and excretion (D052) stay dissolved and
        /// are not touched by this: they are trickles from a living body, not a parcel.
        /// </para>
        /// <para>
        /// <b>The number is a half-life.</b> The decay is a fraction of what is left per second,
        /// so the half-life is <c>ln 2 / rate</c>: 0.005/s is about 139 s, and the proposal's own
        /// reading of it is a corpse that is still worth swimming to a couple of minutes after
        /// the death. Larger is a puff, smaller is a sinking larder.
        /// </para>
        /// <para>
        /// <b>Zero by default, so every launcher on record still describes the world it ran.</b>
        /// At 0 <c>World.Bury</c> deposits exactly where it always did and the corpse list is
        /// never touched, so the cell, vertex and grid worlds on file replay bit for bit.
        /// <c>EVOSIM_CORPSE_DECAY</c> in the header. ⚠ Unmeasured (§5A.10).
        /// </para>
        /// </remarks>
        [Tunable("field", Unit = "1/s")]
        public float CorpseDecayPerSecond { get; set; }

        /// <summary>
        /// Fraction of a refuge layer's density that feeding can see and take, in [0, 1] —
        /// arm C's knob on D055's refuge. Zero is D055's own refuge: total exclusion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why total exclusion needed a dial.</b> D055 found the hard refuge falsified at both
        /// ends — it starves establishment (round 7) and starves an already-established consumer
        /// (the invasion assay, D060) — because this ecology's only evolved consumer is benthic
        /// and feeds where it sinks, so a refuge that covers the whole floor removes its entire
        /// feeding ground rather than protecting a reserve. The owner's standing hypothesis
        /// (logbook/0043) is that the deeper distortion is whole-layer horizontal access, not
        /// floor access at all — this knob lets that be tested without reopening D055 itself: a
        /// partial refuge leaves a consumer something to eat on the floor while still holding
        /// back a reserve the gradient refills.
        /// </para>
        /// <para>
        /// <b>Scope and mechanism, mirroring D055 exactly.</b> Applies to <see cref="NutrientField"/>
        /// only, via <see cref="NutrientField.RefugeEdibleFraction"/> — <c>Deposit</c>, <c>Settle</c>,
        /// <c>Mix</c> and <c>Remineralise</c> are untouched, so matter still arrives, piles and
        /// leaves exactly as before; only what feeding can price changes. Has no effect at all when
        /// <see cref="FloorRefugeMetres"/> is 0: with no refuge layers, there is nothing for a
        /// fraction to apply to.
        /// </para>
        /// <para>
        /// <b>Taking is self-limiting, not perfectly conserving across a step.</b> Each call to
        /// <see cref="NutrientField.Take(float, float, int)"/> may remove up to <c>fraction</c> of whatever the layer
        /// holds <i>at that moment</i>, so repeated draws within one step approach but do not cross
        /// the edible share of what remains — the simplest form that cannot double-dip, at the cost
        /// of not being an exact per-step bound. See the class remarks on
        /// <see cref="NutrientField"/> for the full reasoning.
        /// </para>
        /// <para>⚠ Untested against the theory D055's entry cites (Křivan 2013): a partial refuge's
        /// equilibrium has not been derived the way the total-exclusion case was. Zero by default,
        /// so the world is bit-identical until a run asks otherwise — the D052/D055 shape;
        /// <c>EVOSIM_REFUGE_FRACTION</c> in the header.</para>
        /// </remarks>
        [Tunable("world")]
        public float RefugeEdibleFraction { get; set; }

        /// <summary>
        /// The order <see cref="World.Reproduce"/> offers the living their turn at conception —
        /// D072, logbook/0056. <see cref="Evosim.Core.ConceptionOrder.Age"/> is every run before
        /// this knob existed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>An order nobody chose was deciding who breeds.</b> DESIGN.md specifies no order at
        /// all, and <c>World</c>'s <c>_living</c> is birth-ordered because births append and death
        /// removes in place. A child's matter is drawn from its parent's own layer at the moment
        /// that parent is walked, so in a layer whose stock covers one child and whose solvent
        /// bodies number ten, the oldest takes it every step and a younger body breeds only when
        /// everyone older is dead or broke. Measured in logbook/0056: median parent age in the
        /// reference world's plateau 3,352–4,536 s, against 376–558 s during growth, with 48–62%
        /// of plateau births going to parents older than a whole lifetime.
        /// </para>
        /// <para>
        /// That is the engine doing something the design did not ask for, which is a fault by
        /// this project's own rule rather than a world rule the record ever chose — and it is a
        /// selection pressure for outliving the queue rather than for fecundity.
        /// </para>
        /// <para>
        /// <b>And removing it took the stomachs with it</b> — D073, logbook/0057. The queue was
        /// the accidental route from an energy advantage to a reproductive one, because a body
        /// that eats well survives longer and a body that survives longer is walked earlier; with
        /// it gone, every solvent body has the same fecundity whatever its income, and §5A's
        /// energy economy selects at the plateau only for not starving.
        /// <see cref="Evosim.Core.ConceptionOrder.Reserve"/> is the same route with the right
        /// variable: the walk is by energy surplus above the gate, so a reserve is what buys the
        /// scarce matter.
        /// </para>
        /// <para>
        /// <b>Behind a knob, defaulting to the fault.</b> The historical record has to replay, and
        /// whether the reference world adopts the fix is the owner's ruling on a measured result
        /// rather than a silent change. <c>EVOSIM_CONCEPTION_ORDER</c> in the header, which always
        /// carries a <c>conception</c> token so a reader never has to know whether a missing one
        /// means "age" or "written before the knob existed".
        /// </para>
        /// </remarks>
        [Tunable("world")]
        public ConceptionOrder ConceptionOrder { get; set; } = ConceptionOrder.Age;

        /// <summary>
        /// Filter-feeding intake power cap, W per m³ of tissue — D062's satiation plateau. Zero
        /// is off: an <see cref="AbsorptiveCell"/>'s draw is unbounded, as it always was.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The mouth's physical limit.</b> A true type I functional response is not a
        /// simplification of a real filter feeder, it is an impossibility: handling time forces a
        /// plateau on every catalogued type I response in [JKT04]'s 814-response survey. Round 8's
        /// arm B (D062) adds the plateau this world never had, as a direct answer to the
        /// recruitment-collapse mechanism logbook/0043 traced through the lineage record: every
        /// boom grazes the pool below the reproduction break-even before any adult stops surviving.
        /// </para>
        /// <para>
        /// <b>Where it bites.</b> Bounds the effective <c>density × ClearanceRate</c> product — the
        /// intake power per m³ of tissue — at this ceiling, applied after
        /// <see cref="ClearanceToeDensity"/>'s toe. See <see cref="AbsorptiveCell.Acquire"/>.
        /// </para>
        /// <para>⚠ Unmeasured — a dose chosen by the round that first turns it on. Zero by
        /// default, so the world is bit-identical until a run asks otherwise; the D052/D055
        /// shape. <c>EVOSIM_SATIATION</c> in the header.</para>
        /// </remarks>
        [Tunable("feeding", Unit = "W/m3")]
        public float SatiationWattsPerCubicMetre { get; set; }

        /// <summary>
        /// Density below which filter-feeding clearance relaxes toward zero, J/m³ — D062's
        /// type-III toe. Zero is off: clearance is the plain type-I rate at every density.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The relaxation-at-low-density stabiliser.</b> [DBWM05] found that a slight
        /// relaxation toward type III (q=0.1 of the way there) eliminated extinctions in model
        /// food webs. D062's honest doubt, pre-registered: a pure intake cap
        /// (<see cref="SatiationWattsPerCubicMetre"/>) slows the boom, but it is this toe — which
        /// lets the pool escape upward at low density — that may be the half that stops a bust,
        /// since adults keep grazing at survival rates right through the trough.
        /// </para>
        /// <para>
        /// <b>The exact form.</b> Effective clearance is <c>ClearanceRate × density / (density +
        /// this)</c> — a soft type-III toe. At density 0 the factor is 0; at density equal to this
        /// value the factor is exactly ½, which is the toe's own definition (halved at the named
        /// density); it rises toward 1 as density climbs past the toe. See
        /// <see cref="AbsorptiveCell.Acquire"/>.
        /// </para>
        /// <para>⚠ Unmeasured — a dose chosen by the round that first turns it on. Zero by
        /// default, so the world is bit-identical until a run asks otherwise; the D052/D055
        /// shape. <c>EVOSIM_CLEARANCE_TOE</c> in the header.</para>
        /// </remarks>
        [Tunable("feeding", Unit = "J/m3")]
        public float ClearanceToeDensity { get; set; }

        /// <summary>
        /// Fraction of photosynthetic intake a living producer releases into the nutrient field
        /// each step — D070's exudation. Zero is off: a producer feeds the water only by dying,
        /// which is every run before this knob existed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The flux, not the gearing, caps the second trophic level.</b> Round 14
        /// (logbook/0050) grew the campaign's first absorptive lines and both grazed their field
        /// below break-even and died back to a handful. The reason is one column: whole-world
        /// standing detritus fell from 9,744 J to 4,215 J while the line rebuilt at 0.19 W with no
        /// grazer on it, and dead tissue is the field's only income — so that slope *is* the
        /// income. The D069 ledger prices a clearance-10 stomach's break-even at ~0.03 W, so the
        /// flux sustains a standing line of about six against a producer economy of ~17 W. That is
        /// a producer→consumer transfer of ~1%; the measured step is 13% (11–17%) [ED21 p.14], and
        /// the difference is that this world's producers exude nothing while they live. D070 adds
        /// the missing route — dissolved organic matter, the input to the microbial loop.
        /// </para>
        /// <para>
        /// <b>The exact form.</b> Each metabolic step a body deposits <c>this × LightIncome</c> —
        /// the light it actually kept, after senescence wear — into <see cref="World.Nutrients"/>
        /// at its own height and patch, and its <see cref="EnergyLedger.Net"/> falls by exactly
        /// those joules. A flat fraction of intake, with no size term and no growth-phase gate,
        /// because exudation is isometric in cell volume (slope 0.95) and does not differ between
        /// growth stages [LS13 p.1]. Not applied to <see cref="EnergyLedger.FoodIncome"/> —
        /// stomachs do not exude. See <see cref="Metabolism.StepAt"/>.
        /// </para>
        /// <para>
        /// <b>The audit closes by construction.</b> <see cref="World.EnergyIn"/> still counts the
        /// gross light, and the exuded joules move from a body's reserve into the nutrient field —
        /// two accounts <see cref="World.StandingJoules"/> already sums whole, so §5A.2's equality
        /// needs no new term. What it does need is a counter:
        /// <see cref="World.DetritusExudedTotal"/>, so the detritus-flux instrument shows deaths
        /// and exudation as separate incomes rather than one slope.
        /// </para>
        /// <para>
        /// <b>Known wrong in shape, deliberately.</b> Real percentage extracellular release is
        /// roughly *independent* of irradiance while particulate production is not, so PER rises
        /// under low light and peaks at the base of the euphotic layer [MCP05 p.1, p.8–9]. A
        /// fraction of intake therefore under-releases exactly where the real ocean releases most.
        /// Per-biomass release is the open world-rule alternative; fraction-of-intake is built
        /// first because it is the form D070 ruled on.
        /// </para>
        /// <para>⚠ The magnitude has a citation; the dose for *this* world does not. Review round
        /// 5 (LITERATURE-REVIEW.md Q10) puts real PER at 10–20% of primary production as a
        /// world-ocean range, a cross-system mean of 13%, ~20% flat across a 150-fold productivity
        /// range [MCP05 p.1, p.9] and 37–41% in oligotrophic water; D070 screens at 0.15. Zero by
        /// default, so the world is bit-identical until a run asks otherwise; the D052/D055 shape.
        /// <c>EVOSIM_EXUDATION</c> in the header.</para>
        /// <para>
        /// <b>Refused rather than clamped, at every entry point.</b> Above 1 a producer would
        /// release more than it took and pay the difference out of its own reserve every step;
        /// below 0 it would mine the field for free. Neither is a world anybody meant to ask for,
        /// so the setter throws rather than silently correcting — §9's rule applied to the knob
        /// itself, which is what makes it hold for a hand-edited <c>config.json</c> and for
        /// <c>EVOSIM_EXUDATION</c> alike. Same shape as
        /// <see cref="LightModel.DayNightAmplitude"/>.
        /// </para>
        /// </remarks>
        [Tunable("feeding")]
        public float ExudationFraction
        {
            get => _exudationFraction;
            set => _exudationFraction = value >= 0f && value <= 1f
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(ExudationFraction), value,
                    "Must be in [0, 1]. Above 1 a producer releases more than it earns from " +
                    "light and funds the difference from its reserve; below 0 it draws energy " +
                    "out of the nutrient field for nothing.");
        }

        private float _exudationFraction;

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
        /// Drift threshold θ for D057's species boundary — a child founds a new species when its
        /// <see cref="SpeciesDistance"/> from its parent's species' founding genome exceeds this.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Zero means off, and off means no distance computations run at all</b> — every
        /// creature is species 0, bit-identical to a build with no species machinery. D057 names
        /// species accounting pure instrumentation, and that has to hold at the level of
        /// performance too: a founder lookup and a genome comparison on every birth is not free,
        /// and a run that never asks the question should not pay for one it never uses.
        /// </para>
        /// <para>
        /// <b>Read against <see cref="SpeciesCellTypeWeight"/>:</b> setting that weight at or
        /// above this threshold makes a single cell-type-changing mutation alone exceed it, which
        /// is D057's deliberate commitment — gaining or losing a trophic trade is always a
        /// speciation event by construction.
        /// </para>
        /// <para>⚠ Provisional (D057). Calibrated the project's usual way: measure the
        /// distribution of single-mutation distances in a reference world and set this several
        /// typical-mutation-lengths out — see <c>SpeciesCalibration</c> in
        /// Evosim.Core.Tests, which prints that distribution rather than assuming one.
        /// <c>EVOSIM_SPECIES_THETA</c> in the header.</para>
        /// </remarks>
        [Tunable("species")]
        public float SpeciesDriftThreshold { get; set; }

        /// <summary>
        /// Distance per unit of cell-type-multiset difference in <see cref="SpeciesDistance"/> — D057.
        /// </summary>
        /// <remarks>
        /// <see cref="SpeciesDistance"/>'s cell-type term already returns exactly 1.0 for one node
        /// changing trade — the multiset symmetric difference of one type lost and one type
        /// gained, halved — so this weight is directly "distance contributed per node that
        /// changes trade" and can be read off against <see cref="SpeciesDriftThreshold"/> without
        /// further arithmetic. ⚠ Provisional — §5A.1's cell-type axis has no measured distance of
        /// its own yet; defaulted equal to the other two weights until a calibration round says
        /// otherwise.
        /// </remarks>
        [Tunable("species")]
        public float SpeciesCellTypeWeight { get; set; } = 1f;

        /// <summary>
        /// Distance per unit of node/edge/brain-connectivity difference in
        /// <see cref="SpeciesDistance"/> — D057.
        /// </summary>
        /// <remarks>⚠ Provisional — see <see cref="SpeciesCellTypeWeight"/>.</remarks>
        [Tunable("species")]
        public float SpeciesTopologyWeight { get; set; } = 1f;

        /// <summary>
        /// Distance per unit of normalised continuous body-field difference in
        /// <see cref="SpeciesDistance"/> — D057.
        /// </summary>
        /// <remarks>⚠ Provisional — see <see cref="SpeciesCellTypeWeight"/>.</remarks>
        [Tunable("species")]
        public float SpeciesParameterWeight { get; set; } = 1f;

        /// <summary>
        /// Distance per unit of normalised continuous brain-field difference in
        /// <see cref="SpeciesDistance"/> — D057, and the owner's amendment to it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Zero by default, and deliberately not equal to the other three weights.</b> Brain
        /// weights and oscillator parameters are currently close to selectively neutral in this
        /// world — movement has never paid its energy cost and perception is four of ten sensor
        /// channels (CLAUDE.md's own state-of-play) — so nothing is holding a connection weight
        /// anywhere in particular, and an unselected continuous value random-walks. Folded into a
        /// founder-anchored threshold at any positive weight, that walk would eventually cross it
        /// on drift alone and call the crossing speciation: a stopwatch on noise, not a boundary.
        /// </para>
        /// <para>
        /// The knob exists to be raised between rounds once brains are worth having and holding.
        /// At that point it is what lets two lineages sharing one body plan but controlling it
        /// differently — cryptic species — show up as more than one, which
        /// <see cref="SpeciesTopologyWeight"/> alone cannot: it already prices a rewired
        /// connection as present-or-absent, not as how differently two present connections are
        /// weighted.
        /// </para>
        /// </remarks>
        [Tunable("species")]
        public float SpeciesBrainWeight { get; set; }

        /// <summary>
        /// Simulated seconds at which <see cref="World.Inoculate"/> fires — D060's invasion assay.
        /// Zero means never; the harness (<c>EVOSIM_INOCULATE_AT</c>) calls it once, the first
        /// time <see cref="World.ElapsedSeconds"/> crosses this value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Not a <see cref="World"/> tunable by itself.</b> <see cref="World.Inoculate"/> takes
        /// the genome, count and depth as explicit arguments rather than reading them off
        /// <see cref="RunConfig"/>, because a genome is a file, not a number — it has no legal
        /// value to default to and no way to reach <see cref="Hash"/>. These three fields exist so
        /// the *timing and dose* of an inoculation are still config, hashed and replayable like
        /// everything else; the genome's own identity is recorded separately, in the run's own
        /// header and <c>run.json</c> (D060's built note).
        /// </para>
        /// <para>⚠ Zero by default, so the world is bit-identical until a run asks otherwise —
        /// the D052/D055 shape.</para>
        /// </remarks>
        [Tunable("assay", Unit = "s")]
        public float InoculateAtSeconds { get; set; }

        /// <summary>How many copies <see cref="World.Inoculate"/> admits — D060.</summary>
        /// <remarks>
        /// A float rather than matching <see cref="MinimumPopulation"/>,
        /// <see cref="MaximumPopulation"/> and <see cref="FloorSpawnsPerStep"/>'s <c>int</c> —
        /// cast to one at the call site instead. Those three predate this decision and are not a
        /// precedent to break for; the harness's own <c>Env</c> helper parses every knob as a
        /// float, so a dose this small (D060 calls it "the same small inoculum") is not worth a
        /// second parsing path just to carry an integer. ⚠ Unmeasured; the default favours a
        /// handful over a cohort.
        /// </remarks>
        [Tunable("assay")]
        public float InoculateCount { get; set; } = 5f;

        /// <summary>
        /// Metres below the surface <see cref="World.Inoculate"/> places every copy — D060.
        /// </summary>
        /// <remarks>
        /// Fixed rather than spread, unlike <see cref="FounderDepthSpread"/>: a founder's scatter
        /// exists so generation zero does not all start in the single best patch of light: an
        /// inoculant is not generation zero, it is a hand-placed treatment, and the assay's whole
        /// premise — paired worlds, same seeds, same instant — wants every copy to start at the
        /// one depth the pre-registration names.
        /// </remarks>
        [Tunable("assay", Unit = "m")]
        public float InoculateDepthMetres { get; set; } = 50f;

        /// <summary>
        /// Horizontal cells per depth layer, K ≥ 1 — D061's patchy world. 1 is today's world:
        /// every layer a single, perfectly-mixed column.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The mechanism D061 exists to build.</b> Round 7.5 found that every depth layer
        /// behaves as a perfectly-stirred tank — a creature at the right depth feeds from the
        /// entire world at once, with no travel and no local depletion — which is the provably
        /// unstable limit of a consumer-resource system [FM15 p.1, p.19] and forecloses movement
        /// ever paying. Splitting each layer into K patches gives the world the spatial structure
        /// real persistence depends on: Huffaker's 120-position universe held three oscillations
        /// where every simple universe died [HUF58 p.39-41], and eight throttled-bridge islands
        /// with fewer plants outlived one continuous platform by more than 3× [JN97 p.7].
        /// </para>
        /// <para>
        /// A float used as an int, per this project's standing convention for a knob that is
        /// conceptually a count (<see cref="InoculateCount"/> makes the same choice, for the same
        /// reason: the harness's <c>Env</c> helper parses every knob as a float, and a second
        /// parsing path is not worth adding for one integer). Cast to one at every call site. 1
        /// by default: every geometry, storage and RNG change D061 makes collapses back to
        /// today's world exactly, and the guard is bit-identity, not merely "close".
        /// </para>
        /// <para>⚠ Unmeasured — round 8 runs K=8, Janssen's number [JN97]. <c>EVOSIM_PATCHES</c>
        /// in the header.</para>
        /// </remarks>
        [Tunable("patches")]
        public float HorizontalPatches { get; set; } = 1f;

        /// <summary>
        /// How many of <see cref="HorizontalPatches"/> lie across z, A ≥ 1 — the box's shape.
        /// 1 is the world every run on file was measured in: every patch in one row along x.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A layout, not a new geometry</b> (fable-propose-box.md). The patches along x are
        /// <c>K / A</c>, so the box is <c>W·K/A</c> long, <c>W·A</c> wide and
        /// <see cref="WorldDepthMetres"/> deep with <c>W = sqrt(area / K)</c> unchanged. The area,
        /// the depth, the patch count and the patch side all stay where they were; what moves is
        /// which patch sits where. Four patches at A = 2 are quadrants of a 10 by 10 m footprint
        /// rather than four strips of a 20 by 5 m one.
        /// </para>
        /// <para>
        /// <b>Nothing chose the old shape.</b> It fell out of one line: the length was the patch
        /// count times the width and the width was one patch, written when no part of the ecology
        /// read a horizontal position. It became the shape of the water. D088's dispersal disc is
        /// 5 m in radius in a 5 m width, so a child's z is a lottery rather than a distance; the
        /// transport field's eddies are as wide as the box and no wider; and the two-axis spread
        /// readings are taken over a footprint four times longer than it is wide.
        /// </para>
        /// <para>
        /// A float used as an int, per <see cref="HorizontalPatches"/>'s convention and for its
        /// reason. <see cref="World"/> refuses a world whose A does not divide K, and refuses A
        /// above 1 beside the cell field or D061's dispersal lottery: both walk a
        /// one-dimensional ring of patches, which a two-by-two layout is not.
        /// </para>
        /// <para>⚠ Unmeasured. <c>EVOSIM_PATCHES_ACROSS</c> in the header, which names the layout
        /// as <c>shared &lt;along&gt;x&lt;across&gt;x&lt;W&gt; m</c>.</para>
        /// </remarks>
        [Tunable("patches")]
        public float PatchesAcross { get; set; } = 1f;

        /// <summary>
        /// Sideways exchange between adjacent patches within a layer, m²/s — D061. Zero is off:
        /// no patch ever hears from its neighbour.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Its own knob, deliberately far smaller than <see cref="NutrientMixingDiffusivity"/>.</b>
        /// The arithmetic that shaped D061: boom-to-bust here runs ≈5,000–8,000 s, the current is
        /// 0.05 m/s, and a 400 m² world is ~20 m across — anything advected at current speed
        /// crosses the world in ~400 s, fifteen times inside one cycle. Patches coupled at
        /// anything near that rate are one pool with extra bookkeeping, so horizontal exchange
        /// has to be its own slow throttle rather than reusing the vertical rate or the current.
        /// </para>
        /// <para>
        /// The same first-order Fick's-law form <see cref="NutrientField.Mix"/> already uses
        /// vertically, applied across the ring of patches instead of the stack of layers — see
        /// <see cref="NutrientField.Mix"/>'s remarks for the geometry (patch width, not layer
        /// thickness) and the wraparound.
        /// </para>
        /// <para>⚠ Unmeasured. Zero by default, so a K&gt;1 world with this off has patches that
        /// never exchange detritus at all — isolated columns, not merely throttled ones. The
        /// D052/D055 shape. <c>EVOSIM_H_MIXING</c> in the header.</para>
        /// <para>
        /// In a vertex world (D083, <see cref="MatterField.Vertices"/>) this is the sideways
        /// diffusivity of every vertex's random walk, with <see cref="NutrientMixingDiffusivity"/>
        /// the vertical one: the step is σ = √(2·D·dt) per axis, so at 0 the water moves only up
        /// and down. D084 rules it equal to the vertical rate (0.2 m²/s) so the walk is
        /// isotropic; the cell world's configs keep the value they ran.
        /// </para>
        /// </remarks>
        [Tunable("patches", Unit = "m2/s")]
        public float HorizontalMixingDiffusivity { get; set; }

        /// <summary>
        /// Per-metabolic-step probability a creature moves to an adjacent patch — D061. Zero is
        /// off: nothing ever disperses, whatever <see cref="HorizontalPatches"/> is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Metapopulation-style movement, not continuous advection</b> — the owner's explicit
        /// choice, and the regime with the empirical persistence record: throttled bridges
        /// between islands, not a current strong enough to mix them (D061's rejected
        /// alternative). A flat per-step chance rather than a rate, because the metabolic step is
        /// already this world's unit of time for every other per-creature decision (reproduction,
        /// feeding).
        /// </para>
        /// <para>⚠ Unmeasured. Zero by default, so nothing here perturbs a K=1 world (there is
        /// nowhere to disperse to) or a K&gt;1 world that has not asked for movement. The
        /// D052/D055 shape. <c>EVOSIM_DISPERSAL</c> in the header.</para>
        /// </remarks>
        [Tunable("patches")]
        public float DispersalChancePerStep { get; set; }

        /// <summary>
        /// Whether each patch's producers shade only their own column, as 0/1 — D061's
        /// endogenous inequality. Zero is today's world: one shared canopy pooled across every
        /// patch.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The mechanism that makes patches unequal without a painted-on constant.</b> D061
        /// names this the deliberate answer to [HZ13 p.5]'s finding that identical patches buy
        /// nothing — subdivision alone is the null result, and the patches have to actually
        /// differ. A crowded patch darkening itself is inequality the creatures generate, the
        /// first reason a producer has ever had to be somewhere else rather than everywhere the
        /// light is best.
        /// </para>
        /// <para>
        /// A float rather than a bool, per this project's standing convention for a knob that is
        /// conceptually binary (<see cref="FloorRefugeMetres"/>'s remark makes the same case): the
        /// tunable machinery is float-typed throughout, and a second type is not worth adding for
        /// one on/off switch. Compared against zero at every call site, exactly like
        /// <see cref="NutrientMixingDiffusivity"/>.
        /// </para>
        /// <para>⚠ Unmeasured. Zero by default — bit-identical to a world that pools every
        /// patch's shading into one shared canopy, which is what every earlier run measured
        /// whether or not it had ever heard of patches. <c>EVOSIM_PATCH_SHADING</c> in the
        /// header.</para>
        /// </remarks>
        [Tunable("patches")]
        public float PerPatchShading { get; set; }

        /// <summary>
        /// The physics timestep the run integrated at, seconds — DESIGN.md §6.2's queued item,
        /// closed. Must divide the 0.5 s metabolic step exactly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Recorded here because it is the one setting that changes every number and was in
        /// no hash.</b> The step is not an implementation detail of the solver: PhysX replays bit
        /// for bit on one machine, so a run at 0.02 s is a different chaotic realisation of the
        /// same seed — ±20% population, four metres of depth, half the larder by t=5,000
        /// (logbook/0052). Two runs at different steps therefore share a <c>configHash</c> and
        /// disagree about everything, which is §7's failure in the direction it was written to
        /// catch: the hash exists to <i>detect</i> that two runs differed.
        /// </para>
        /// <para>
        /// <b>Not the mechanism, only the record.</b> Nothing in <c>Evosim.Core</c> integrates
        /// physics — <c>Evosim.Sim</c>'s <c>Ecosystem.ConfigurePhysicsStep</c> does, and it is
        /// still the thing the engine is configured from. This carries the same number into the
        /// hash and into <c>config.json</c>, and enforces the same rule at the setter so a
        /// hand-edited file cannot name a step the simulator would refuse.
        /// </para>
        /// <para>
        /// <b>Refused rather than clamped</b>, the shape <see cref="ExudationFraction"/> uses and
        /// for the same reason: a step that does not divide 0.5 would leave the metabolic clock
        /// running at something other than 2 Hz, which is not a world anybody meant to ask for.
        /// The legal set is discrete — 0.5/n — so this is one of the few knobs a continuous
        /// search cannot nudge; <c>RunConfigTests.NudgeFloat</c> halves and doubles for it.
        /// <c>EVOSIM_DT</c> in the header, where it prints as <c>dt=</c>.
        /// </para>
        /// </remarks>
        [Tunable("physics", Unit = "s")]
        public float PhysicsStepSeconds
        {
            get => _physicsStepSeconds;
            set => _physicsStepSeconds = DividesTheMetabolicStep(value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(PhysicsStepSeconds), value,
                    "Must be in (0, 0.5] and divide the 0.5 s metabolic step exactly: 0.01, " +
                    "0.02, 0.025, 0.05, 0.1, 0.125, 0.25 or 0.5.");
        }

        private float _physicsStepSeconds = 0.01f;

        /// <summary>
        /// Whether the drive impulse limiter applies at every step rather than only above dt 0.01
        /// — <c>fable-propose-limiter.md</c>. False by default, which is every run on file.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What it gates.</b> <c>Evosim.Sim</c>'s <c>EffectorDriver</c> caps each degree of
        /// freedom's drive torque so one physics step cannot add more than 30 rad/s of joint
        /// angular velocity — about five revolutions a second, faster than anything that has ever
        /// swum here — and counts every bind as <c>driveImpulsesLimited</c>. The cap was gated to
        /// steps coarser than 0.01 so that the confirming step kept replaying the record, and at
        /// 0.01 every drive torque is applied as computed.
        /// </para>
        /// <para>
        /// <b>Why it is asked for now.</b> Round 34 drove the stroke flat out with every price at
        /// zero and lost fifty bodies across five seeds, every dump read carrying an active joint
        /// and a median age near 1,200 s: adults thrown out of the world by the solver rather
        /// than selected against, about two percent of jointed births (logbook/0080, logbook/0090).
        /// The rounds ahead make the stroke cheap on purpose, so the leak grows with the
        /// programme.
        /// </para>
        /// <para>
        /// <b>A tunable and not a new constant</b>, so the record stays readable in its own
        /// terms: at false the driver keeps today's rule to the character and every recorded run
        /// replays under its own config. The 30 rad/s stays a constant — it is a physical bound,
        /// and a second dial there would invite tuning the solver rather than the world.
        /// <c>EVOSIM_DRIVE_LIMIT_ALWAYS</c>, and the header prints <c>driveLimit always</c> or
        /// <c>driveLimit &gt;0.01</c>.
        /// </para>
        /// </remarks>
        [Tunable("physics")]
        public bool DriveLimitAtEveryStep { get; set; }

        /// <summary>
        /// Whether a genome in this run may draw <see cref="SensorChannel.Chemical"/> — the smell
        /// of food in the water at a part, §4.4.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The channel is answered whether this is on or off; what this decides is who may
        /// reach for it.</b> <c>CreatureSensors</c> reports a real density from the day it was
        /// wired, so a hand-authored genome carrying the input reads a real number in any run.
        /// This gates <see cref="SensorPool"/>, which is what founder generation and mutation
        /// draw from — and that is the gate that has to exist, because a longer pool consumes the
        /// same RNG draw and returns a different channel, so every run in the historical record
        /// would stop replaying the moment the drawable set changed.
        /// </para>
        /// <para>
        /// Off by default, the shape every knob here ships in: the world is bit-identical until a
        /// round asks otherwise, and the <c>configHash</c> is not. <c>EVOSIM_SENSE_CHEMICAL</c>,
        /// and the header's <c>senses</c> token lists what the pool actually holds.
        /// </para>
        /// </remarks>
        [Tunable("sense")]
        public bool SenseChemical
        {
            get => _senseChemical;
            set { _senseChemical = value; _sensorPool = null; }
        }

        /// <summary>
        /// Whether a genome in this run may draw <see cref="SensorChannel.Energy"/> — its own
        /// reserve, as seconds of life left. See <see cref="SenseChemical"/> for what the gate is.
        /// </summary>
        [Tunable("sense")]
        public bool SenseEnergy
        {
            get => _senseEnergy;
            set { _senseEnergy = value; _sensorPool = null; }
        }

        /// <summary>
        /// Whether a genome in this run may draw <see cref="SensorChannel.Flow"/> — the water's
        /// velocity relative to a part, per axis. See <see cref="SenseChemical"/>.
        /// </summary>
        [Tunable("sense")]
        public bool SenseFlow
        {
            get => _senseFlow;
            set { _senseFlow = value; _sensorPool = null; }
        }

        private bool _senseChemical;
        private bool _senseEnergy;
        private bool _senseFlow;
        private SensorChannel[] _sensorPool;

        /// <summary>
        /// Edible density that reads as half scale on <see cref="SensorChannel.Chemical"/>, J/m³.
        /// </summary>
        /// <remarks>
        /// ⚠ Unmeasured (§5A.10). The squash is <c>x / (x + k)</c> with this as <c>k</c>: it
        /// reads exactly ½ at <c>k</c>, is 0 in empty water, and approaches 1 without ever
        /// arriving. The alternative — a linear clamp at some ceiling — makes a gradient sensor
        /// saturate in rich water, which is blind exactly where the food is; the same argument
        /// <c>CreatureSensors.FullScaleRadPerSecond</c>'s remark makes for a constant, with
        /// the added point that this field spans two decades (0.3–100 J/m³ across the round-23
        /// reports) and no linear scale keeps resolution across both ends of that.
        /// </remarks>
        [Tunable("sense", Unit = "J/m3")]
        public float ChemicalHalfScaleJoulesPerCubicMetre { get; set; } = 10f;

        /// <summary>
        /// Reserve that reads as full scale on <see cref="SensorChannel.Energy"/>, seconds.
        /// </summary>
        /// <remarks>
        /// ⚠ Unmeasured (§5A.10). The squash is <c>tanh(seconds / k)</c>, so a creature holding a
        /// whole senescence scale of reserve reads as sated and one about to starve reads near 0.
        /// §4.4 rejects a hard threshold — a creature that only notices hunger at some line has no
        /// reason to act before it — but a squash still needs a scale, and 3,000 s is the one the
        /// world already uses for a lifetime.
        /// </remarks>
        [Tunable("sense", Unit = "s")]
        public float EnergyFullScaleSeconds { get; set; } = 3000f;

        /// <summary>
        /// Relative water speed that reads as full scale on <see cref="SensorChannel.Flow"/>, m/s.
        /// </summary>
        /// <remarks>
        /// ⚠ Unmeasured (§5A.10). Per axis, clamped to [-1, 1]. A constant for
        /// <c>CreatureSensors.FullScaleRadPerSecond</c>'s reason — dividing by anything the
        /// genome or the timestep can move makes what a creature perceives depend on it — and 0.3
        /// m/s because that is the reference world's own current speed, so a body drifting with the
        /// water reads near 0 and one swimming across it reads a fraction of full scale.
        /// </remarks>
        [Tunable("sense", Unit = "m/s")]
        public float FlowFullScaleMetresPerSecond { get; set; } = 0.3f;

        /// <summary>
        /// What a genome in this run may draw a sensor reference from — the four channels of every
        /// run before the sense knobs existed, plus whichever of them is switched on.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Appended in enum order, never inserted.</b> <c>Chemical</c>, <c>Energy</c> and
        /// <c>Flow</c> follow <see cref="SensorChannel.Depth"/>, so at the default this array is
        /// <see cref="SensorChannels.DefaultPool"/> element for element, one draw of
        /// <see cref="Rng.Pick{T}"/> consumes what it always consumed, and the founder and
        /// mutation streams are unchanged. That is the replay requirement, and it is why the pool
        /// is a list rather than a filter applied afterwards.
        /// </para>
        /// <para>
        /// Cached, and the cache is dropped by each setter rather than by a revision counter:
        /// this is read once per sensor draw, which is tens of times per birth.
        /// </para>
        /// </remarks>
        public SensorChannel[] SensorPool()
        {
            if (_sensorPool != null) return _sensorPool;

            if (!_senseChemical && !_senseEnergy && !_senseFlow)
            {
                _sensorPool = SensorChannels.DefaultPool;
                return _sensorPool;
            }

            int extra = (_senseChemical ? 1 : 0) + (_senseEnergy ? 1 : 0) + (_senseFlow ? 1 : 0);
            var pool = new SensorChannel[SensorChannels.DefaultPool.Length + extra];

            Array.Copy(SensorChannels.DefaultPool, pool, SensorChannels.DefaultPool.Length);

            int at = SensorChannels.DefaultPool.Length;
            if (_senseChemical) pool[at++] = SensorChannel.Chemical;
            if (_senseEnergy) pool[at++] = SensorChannel.Energy;
            if (_senseFlow) pool[at] = SensorChannel.Flow;

            _sensorPool = pool;
            return _sensorPool;
        }

        /// <summary>
        /// The rule <c>Ecosystem.ConfigurePhysicsStep</c> enforces, stated once here so the two
        /// cannot drift — same bound, same tolerance.
        /// </summary>
        private static bool DividesTheMetabolicStep(float dt)
        {
            const float metabolicStepSeconds = 0.5f;

            if (!(dt > 0f) || dt > metabolicStepSeconds) return false;

            float steps = metabolicStepSeconds / dt;
            int rounded = (int)Math.Round(steps);

            return rounded >= 1 && Math.Abs(steps - rounded) <= 1e-4f;
        }

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

    /// <summary>
    /// The order in which the living are offered their turn at conception — D072, logbook/0056;
    /// <see cref="Reserve"/> is D073, logbook/0057. See <see cref="RunConfig.ConceptionOrder"/>
    /// for what it decides and <see cref="World.Reproduce"/> for where it is spent.
    /// </summary>
    /// <remarks>
    /// Named for what each one is rather than for which is right. None of them is a scoring rule:
    /// all three walk every living body once per step and all three let the same solvency gate
    /// decide, so the only difference is who is asked first when a layer's matter covers fewer
    /// children than there are parents who want one.
    /// </remarks>
    public enum ConceptionOrder
    {
        /// <summary>
        /// Birth order — <c>_living</c>'s own, oldest first. Every run before this knob existed,
        /// and the default, so the record replays.
        /// </summary>
        Age = 0,

        /// <summary>
        /// A fresh uniformly random permutation of the living each step, from a stream of the
        /// world's own. Same seed and config, same permutations, same run (§7).
        /// </summary>
        Shuffled = 1,

        /// <summary>
        /// Descending energy surplus above the breeding gate, ties by list index — D073. Energy
        /// buys fecundity: when a layer's matter covers one child, the parent with the most to
        /// spare takes it. Deterministic, and it takes no draw from any stream.
        /// </summary>
        Reserve = 2,
    }

    /// <summary>
    /// What shape the water is — <see cref="RunConfig.WorldShape"/>,
    /// <c>fable-propose-aquarium.md</c> ruling 1.
    /// </summary>
    public enum WorldShape
    {
        /// <summary>
        /// D077's box: <c>K/A</c> patches along x by <c>A</c> across z, periodic on both
        /// horizontal axes. Every run in the record, and the default so that it stays so.
        /// </summary>
        Box = 0,

        /// <summary>
        /// A cylinder of water with a glass wall: a disc of <c>R = sqrt(area/π)</c> in a bounding
        /// square <c>[0, 2R)²</c> about an axis at <c>(R, R)</c>, <c>K</c> rings of equal area
        /// for patches, and a spectrum of streams for a current.
        /// </summary>
        /// <remarks>
        /// The wall replaces the seam a carrying current made visible, and the disc has a centre
        /// and a rim, which is the ecological axis a round tank has where a row of patches has
        /// none. <see cref="TankGeometry"/> holds the arithmetic.
        /// </remarks>
        Tank = 1,
    }

    /// <summary>How <see cref="RunConfig.FieldModel"/> holds the water's stock — D083.</summary>
    public enum MatterField
    {
        /// <summary>One number per layer and patch: every recorded run.</summary>
        Cells = 0,

        /// <summary>Vertices holding joules at positions, read through a kernel — <see cref="VertexField"/>.</summary>
        Vertices = 1,

        /// <summary>
        /// A 3D grid of cubic cells of <see cref="RunConfig.FieldCellMetres"/>:
        /// <see cref="GridField"/>, <c>fable-propose-grid.md</c>.
        /// </summary>
        /// <remarks>
        /// The vertex world's property, bought with an array index instead of a kernel sum: a
        /// still mouth eats its own cell, a moving mouth leaves it behind. Requires
        /// <see cref="RunConfig.SharedSpace"/>, because a cell is a place.
        /// </remarks>
        Grid = 2,
    }

    /// <summary>
    /// Where <see cref="RunConfig.MatterInfluxPerSecond"/>'s deposit lands — D074. See
    /// <see cref="RunConfig.MatterInfluxAt"/> for why it is a knob and
    /// <see cref="World.Step"/> for when it is spent.
    /// </summary>
    /// <remarks>
    /// Named for the route rather than for the layer, because the layer is a consequence: what a
    /// world is being told is where its matter comes from, and the depth follows from that. At an
    /// influx of 0 the two are the same world.
    /// </remarks>
    public enum MatterInflux
    {
        /// <summary>
        /// Rivers and dust: the top layer, spread equally over every patch — the deposit arrives
        /// where the light already is, and has to sink to reach anything deeper.
        /// </summary>
        Surface = 0,

        /// <summary>
        /// A hydrothermal supply: all of it in <see cref="CurrentField.VentPatch"/> at
        /// <see cref="CurrentField.VentDepthMetres"/>, so it enters at the bottom of the one
        /// column D067's plume carries upward.
        /// </summary>
        Vent = 1,
    }
}
