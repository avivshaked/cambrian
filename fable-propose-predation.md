# Proposal: predation — how creatures meet, and what a bite is

*Fable, 2026-09-05. For the owner's ruling after the movement round is read (D075's
order). Built on `scratch/predation-survey.md` (read-only, cited to file and line) and on
DESIGN.md §5A.3. Absorbed into DECISIONS.md on ruling, then deleted.*

## What is already decided, and what is already built

The design settles more than I expected. Predation is a behaviour, not a body type: one
`Consumer` cell covers scavenging, grazing and predation, told apart only by what it
touches, with yields tiered by target — carrion 0.8, living photosynthetic tissue 0.5,
living consumer tissue 0.2, all marked unmeasured (§5A.3, §5A.10). A bite is an energy
transfer that loses at every step like every other feeding transfer. `Damage` is a
per-part sensor reading the fraction of the part's own stored energy taken in the last
step; `Contact` is a per-part sensor and is how a consumer finds tissue. Attack and
defence are explicitly deferred. And the code is further along than the logbook suggests:
`ConsumerCell.Acquire` already implements both routes, carrion (live in every run since
the leak) and a live bite reading a `TissueContact` — but the metabolic step hands it
`contact: null` every time, so the second route has never executed
(`src/Evosim.Core/Ecosystem/Metabolism.cs:243`).

## The fact the proposal turns on: creatures cannot meet

Creatures are tiled on a lattice 100 m apart on mutually ignoring collision layers
(`Ecosystem.cs:93`, `PhenotypeBuilder.cs:97`; §6.3). Horizontal position is real in the
physics and inert in the ecology; the ecological coordinates are depth and patch. Nothing
in the world has ever touched anything else. DESIGN.md §5A.8's table says tiling was
repurposed into spatial partitioning so that creatures can meet; that is the intent, and
it has not happened (the same doc upkeep commit corrects the table). So before a bite can
be specified, the world needs a rule for how two bodies come into contact at all, and
that rule is the ruling this proposal asks for.

## The fork: physical space or an encounter rule

**A. Shared physical space.** Untile: every creature in one volume, creature-to-creature
collisions on, contact from PhysX's contact reports, a bite where a consumer part touches
tissue. It is the design's picture — morphology as the sensory apparatus, bearing computed
by the body, chase and flight in three dimensions. Two facts stand against it now. The
world's footprint is 100 m² (`EVOSIM_AREA`), so 2,300–3,500 bodies with several parts
each in a 10 × 10 × 60 m column is half a body per cubic metre: shared space at that
density is a crowd, not an ocean, and a different world from the one round 18 passed.
And §5A.9's throughput was measured to 512 creatures with no contacts; per-body engine
interop is already the dominant cost, and contact callbacks land on exactly that line.
Untiling is a Milestone-scale change to the physics, unmeasured at this population.

**B. An encounter rule.** Contact is an ecological event drawn from the world's RNG, not
a physical one. Once per metabolic step, a consumer part with a mouth (a `Consumer` cell
on a part) encounters tissue in its own layer and patch at a rate set by mass action: the
probability of an encounter in the step is `1 − exp(−k · V_mouth · ρ_tissue · Δt)`, where
`ρ_tissue` is the living tissue volume per cubic metre in that layer and patch (a sum the
world already keeps per creature; one pass per step to bin it) and `k` an encounter
constant. The prey is drawn from the layer's occupants weighted by tissue volume; the
bitten part is drawn within the prey weighted the same way. The bite is then the existing
`TissueContact` → `ConsumerCell.Acquire` path, untouched. `Contact` reads 1 on the biting
part in the step it bites and on the bitten part in the step it is bitten; `Damage` reads
the fraction taken, as the design says. Fleeing is leaving the layer or the patch, which
is what depth control and the current already move; chasing is following. The rule is
deterministic from the seed, costs one binning pass and one draw per mouth per metabolic
step, and closes the audit through machinery that already closes it.

What B gives up, stated plainly: the body does not compute a bearing to prey, a long
body is no better at finding tissue than a compact one, and a chase is a race along one
axis and across four patches rather than through water. What it keeps is everything the
design says predation is *for*: a second trophic level, a cost to crowding, a reason to
move, a reason to sense, an arms race in energy rather than geometry. The physical
picture is not abandoned; it is deferred to the island model, where per-scene populations
are tens rather than thousands and shared space is affordable, and the encounter rule is
the abstraction that holds until then.

**Recommendation: B**, with A recorded as the intended successor once the island model
exists and throughput allows it.

## The rules of B, enough to build

1. **Mouths.** A part carries a mouth if any of its cells is a `Consumer`. Mouth volume is
   the part's consumer tissue volume. Founders and mutation already draw the cell type
   (`EVOSIM_CELLTYPE_MUTATION`); nothing new in the genome.
2. **Encounter.** Per metabolic step, per mouth, as above. `k` is a `RunConfig` tunable
   (`EVOSIM_ENCOUNTER`), default 0 so every existing configuration replays bit for bit; the
   screen sets it. The draw uses a dedicated `Rng` stream (the convention `ConceptionOrder`
   set), so an encounter does not shift any other draw.
3. **The bite.** Draws up to `BiteJoulesPerSecond × V_mouth × Δt` from the bitten part's
   tissue joules, yield tiered as coded (carrion / grazing / predation); the taker keeps
   the yield, the remainder is waste as in every transfer. A part cannot draw from its
   own creature.
4. **What the prey loses.** The bitten part's tissue energy, and with it volume at
   `TissueEnergyPerCubicMetre`. A part drained below the development floor
   (`minPartVolume`) is dead tissue: the creature dies with `DeathCause.Eaten` (a new
   cause, the third) and its remaining tissue is carrion through the existing death path.
   No mid-life pruning of parts in the first cut — a bitten creature is whole until it is
   dead — because part removal changes the articulation and that is the theatre's and the
   physics' problem, not the economy's.
5. **Matter.** A bite moves matter with the energy it takes: the taken joules carry their
   `MatterPerTissueJoule` share into the eater's locked matter, the waste share returns to
   the layer's free matter where the bite happened. The identity
   `initial + influx − buried = free + locked` holds by construction; the test asserts it.
6. **Sensors.** `Contact` and `Damage` wired in `CreatureSensors` as the design specifies
   and added to the pool behind two more `Sense*` booleans, default off, in the pattern of
   the perception build. Without them a consumer is a filter feeder; with them it can
   learn to bite where it is and leave where it is bitten.
7. **Out of scope.** Attack and defence (armour, toxicity, resistance), as the design
   defers them; per-part pruning; any change to yields (the screen reads them).
8. **Instrument.** Columns `bites`, `bite J` (joules transferred per window), `eaten`
   (deaths by predation per window), `consumer` and `consumer inh`; and the food chain's
   depth: mean depth of consumers against producers.

## The round that would test it

Not before the movement round is read (D075). Then a screen at 0.02 on the adopted open
world, seeds 2 and 4, two doses of `k` against `k = 0`, and the reading is two-sided:
consumers persist as an inherited line (the predator valley bridged) and producers are
not driven under (the grazing lawn does not collapse) at one dose or the other; or
consumers never hold at any dose, in which case the carrion bridge is measured as
insufficient and cell-type mutation density-dependence (§5A.3's second bridge) is the
next lever. Confirmation at 0.01 under D063 as amended with a predation clause the owner
words then. Every arm reads `eaten` against `starved` for the first time in the project's
history that cause of death discriminates anything.

## What the owner rules on

1. The fork: the encounter rule (B) now, physical contact deferred to the island model —
   or A, at the cost of a physics change unmeasured at this population.
2. The unit of loss: the bitten part's tissue, creature death at the development floor,
   no mid-life pruning.
3. Matter moving with the bite.
4. `DeathCause.Eaten` as the third cause.
5. Yields left as coded for the screen.

*The correction to §5A.8's tiling claim is doc upkeep and goes in regardless.*
