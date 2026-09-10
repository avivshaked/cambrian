# Predation survey — specified vs. built vs. absent

Read-only survey for a design proposal. Every claim below is cited `file:line`. Section
headers mark **[SPECIFIED]** (written in DESIGN.md/DECISIONS.md as a design rule),
**[BUILT]** (code exists and runs today), or **[ABSENT/OPEN]** (not decided or not built).

## 1. DESIGN.md

### Cell types and feeding rule — §5A.1, §5A.3 [SPECIFIED, partially BUILT]

- Cell type table: `Consumer` gains energy from "Tissue on contact — living or dead...
  Works on carrion without perception, which is what makes it survivable early"
  (DESIGN.md:904).
- §5A.3 (DESIGN.md:1313-1343) is the core predation spec:
  - A `Consumer` part "gains energy on contact with tissue. Yield depends on **what it
    touches**" (DESIGN.md:1315), three tiers: dead tissue (high yield, no resistance,
    "scavenging works while drifting blind — no perception needed"), living
    `Photosynthetic` tissue (moderate, "grazing"), living `Consumer` tissue (low and
    contested, "predation proper. Needs perception to be worth attempting")
    (DESIGN.md:1317-1321).
  - "Herbivore and carnivore are therefore **behavioural outcomes of what a creature ends
    up eating**, not separate body types" (DESIGN.md:1323-1325) — one cell type, no
    separate "carnivore" morphology.
  - **The predator valley and its two bridges** (DESIGN.md:1327-1339): a `Consumer` part
    costs upkeep from the mutation onward but pays nothing until perception + directed
    movement + prey density coexist — "populations do not cross valleys that wide."
    Bridge 1: carrion feeding pays from generation 1 ("Detritivore → scavenger → predator
    is a gradient rather than a leap"). Bridge 2: cell-type mutation
    (`Photosynthetic → Consumer`) is density-dependent and self-timing.
  - **"Attack and defence — whether a target resists, at what cost, and whether armour or
    toxicity are expressible — are deliberately unspecified. They are the next layer and
    should be designed against observed behaviour, not guessed now."** (DESIGN.md:1341-1343)
    — explicit non-decision, load-bearing for the proposal.

### §4.4 Contact/Damage sensor channels [SPECIFIED, NOT BUILT]

- "Damage is readable on every part, not only on links. Once creatures eat each other
  (§5A.3), being bitten is the most consequential thing that happens to a body cell...
  It reads as the fraction of the part's own stored energy taken over the last step, so
  it is scale-free" (DESIGN.md:552-557).
- "Contact moves forward with it. Draft 3 scoped contact to terrain and Milestone 5; §5A
  makes it aquatic, because contact is how a consumer cell finds tissue to bite. Both
  channels are needed as soon as predation is, which is Milestone 3." (DESIGN.md:559-561)
  — note DESIGN.md's own milestone table (§10) later renumbers predation to Milestone 7;
  this line is stale relative to that renumbering.
- "No relay mechanism is added for damage to reach the rest of the creature... a hit
  propagates one node per step" via existing `ParentNode`/`ChildNode` inputs, deliberately,
  as "what a conduction delay looks like" (DESIGN.md:563-568).

### §5A.2c Carrion — the bridge that exists [BUILT]

- "Carrion is the predator valley's bridge, and it now exists." `ConsumerCell` "can now
  scavenge the detritus pool at `CarrionYield`. Detritivore → scavenger → predator is a
  gradient the population can actually walk." (DESIGN.md:1172-1177) — explicitly only the
  scavenging half; live-tissue biting is not claimed built here.
- The energy audit is closed with feeding included: "`EnergyIn − EnergyOut ==
  StandingJoules`... Measured residual over a 300 s run with births, deaths and feeding:
  0.0000%" (DESIGN.md:1179-1183).

### §5A.2, §5A.9 — energy accounting and cost of a bite [SPECIFIED]

- "Sunlight is the only primary input. Nutrients are recycled dead matter; **predation
  transfers between creatures**; death returns tissue to the nutrient pool. Metabolism is
  the only outflow" (DESIGN.md:1018-1020).
- Bite economics, "Open parameters" table (DESIGN.md:1780-1782):
  `ConsumerCell.BiteRate` = "Joules swallowed per m³ per second — what limits feeding in
  rich water"; `ConsumerCell.ScavengeRate` = "Water searched for carrion... fail
  differently"; `ConsumerCell.*Yield` = "Fraction kept per target type. Carrion highest —
  the predator valley's bridge."
- §5A.9 cost note: PhysX solver islands scale well because "Creatures in open water are
  mostly far apart and stay separate islands... it degrades only where creatures touch,
  **which here means predation — rare and local**" (DESIGN.md:1704-1706) — i.e. the design
  expects contact-solving cost from predation to be rare/local, not a throughput risk at
  today's population.

### Milestones and roadmap [SPECIFIED]

- Milestone table: "**7** | Food web: `Consumer` cells, carrion, predation, attack and
  defence | Trophic levels, or clear evidence of why not (§5A.7)" (DESIGN.md:2105).
  "Milestone 3 is the pivot" language (DESIGN.md:2109-2112) is about metabolism/starvation
  generally, not predation specifically — predation itself sits at Milestone 7.
- Failure mode table: "No carnivores ever | Predator valley; trait purged before it can
  pay | Carrion feeding, plus cell-type mutation (§5A.3)" (DESIGN.md:1625).

## 2. DECISIONS.md — entries touching predation/consumers/cell types/damage

One-line summaries, in file order:

- **D019** (DECISIONS.md:44) — `Neural` is a cell type, discounts rather than gates neurons.
  Active. (Cell-type mechanics precedent, not predation itself.)
- **D048** (DECISIONS.md:73) — Producers must consume matter; nutrient is matter, light is
  energy. Active, built — the matter/energy split that predation's transfers must respect.
- **D055** (DECISIONS.md:80) — "The seabed is a refuge — the floor layer cannot be grazed."
  Rejected as a world rule (starves the benthic consumer); kept as an instrument knob.
  Directly about consumer/grazing dynamics.
- **D075** (DECISIONS.md:100, full text at 3626-3659) — **The ruled roadmap.** Owner: "agreed,
  lock it in" (2026-09-04). Order after the open-budget milestone: (1) movement that pays,
  (2) the theatre in parallel, **(3) predation — "a consumer cell type eating living
  tissue; after movement, because the design says it needs perception to be worth
  attempting (§5A's table)"** (DECISIONS.md:3649-3650), (4) cell-type expansion / vent
  community, (5) archive and islands. This is the current, owner-ratified sequencing:
  predation is next-but-one, gated on movement paying first.
- Narrative entries (not individually numbered decisions but load-bearing prose): the
  "predator valley" origin story at DECISIONS.md:2057-2080 (six cell types as independent
  atoms spanning geological eras — photosynthesis ~3.5 Ga, detritivory ~2 Ga, **predation
  ~0.8 Ga**, nervous systems/muscle ~0.6 Ga); D048's entry text (DECISIONS.md:784-806)
  narrates the carrion bridge landing ("Consumers can finally eat" — carrion only, since
  `TissueContact` "needs physics and does not arrive until Milestone 4").
- Consumer-bust / prey-refuge stability work (D0xx around DECISIONS.md:2478-2565,
  2742-2849) is about detritus/absorptive-consumer population dynamics and refuge theory
  (Křivan 2013, prey-refuge stabiliser), not about the bite mechanic itself — it studies
  whether an *existing* (hand-placed/invaded) consumer lineage persists, using the carrion
  route only.
- A vent-community cell type (chemosynthesis) is flagged as future cell-type work
  "with the cell-type..." at DECISIONS.md:3432, adjacent to but distinct from predation.

## 3. research/LITERATURE-REVIEW.md — predator-prey corpus coverage

- **PolyWorld [Y94]** (LITERATURE-REVIEW.md:592-602) — "An endogenous-selection ecology
  from 1994 that charges energy linearly per neuron and per synapse, prices every
  behaviour, and **lets predation and mimicry emerge**. The neural-charge mechanism (p.7)
  directly anticipates §5A.2's neural term" — read at targeted passages (pp.1,7,10-11),
  verified against text. This is the design's one directly-relevant precedent for
  emergent predation from an economic/neural-cost model.
- **Round 3 sweep 1** was scoped explicitly to *"Artificial-ecosystem primaries — PolyWorld,
  Gene Pool, Tierra, Avida, Geb, and any system with emergent (not scripted) trophic
  structure"* (LITERATURE-REVIEW.md:258-261) and *"Trophic emergence, recycling, neural
  cost, motility economics"* (LITERATURE-REVIEW.md:264-267).
- **Round 4 sweeps** covered consumer-resource stability theory (paradox of enrichment,
  Rosenzweig-MacArthur, prey refuge, functional response types, Beddington-DeAngelis
  interference) and spatial predator-prey persistence (Huffaker, metapopulation
  synchrony, Avida spatial structure) (LITERATURE-REVIEW.md:288-297) — these feed the
  consumer-bust/refuge decisions in DECISIONS.md, not the bite mechanic.
- **Transfer efficiency literature** — [PC95] (Pauly & Christensen): 140 estimates across
  48 trophic models, TL2-TL4, ~10% mean, no trend with level (LITERATURE-REVIEW.md:729-734).
  [ED21]: producer→herbivore 13% (11-17%), herbivore→fish 10% (7-12%), whole-ecosystem
  span <1%-52% (LITERATURE-REVIEW.md:767-774). This is the empirical anchor a predation
  proposal would use to calibrate `PredationYield`/`GrazingYield` against real food webs —
  currently those two knobs (0.2 / 0.5 defaults in code, see §4 below) are unmeasured
  guesses, not literature-derived.
- **Gap, stated plainly by the review itself**: "Searched (§3.6). **Precedent half of Q7
  answered; the emergent-trophic-structure half has leads, not held sources.**"
  (LITERATURE-REVIEW.md:1011-1014) — i.e. the review found *that* predation/trophic
  structure emerges in comparable systems (PolyWorld) but has not yet read primary sources
  establishing *how* those systems structure the bite/attack/defence mechanic itself. That
  gap is exactly what DESIGN.md §5A.3 marks "deliberately unspecified."

## 4. Code — src/Evosim.Core

### Cell types — `src/Evosim.Core/Cells/StandardCellTypes.cs`

- `ConsumerCell` (StandardCellTypes.cs:532-702) is fully implemented with two feeding
  routes coded into `Acquire`:
  - **Carrion/scavenging route (StandardCellTypes.cs:666-670)**: reads
    `context.NutrientDensity` (the detritus pool, a scalar field — not a physical body),
    capped by `ScavengeVolume` (ClearanceRate-style water search). **This route is live in
    every run.**
  - **Live-bite route (StandardCellTypes.cs:672-676)**: `TissueContact contact =
    context.Contact; if (contact == null || contact.AvailableJoules <= 0f) return carrion;`
    — reads `context.Contact`, takes `Math.Min(contact.AvailableJoules, bite)` at a yield
    from `YieldAgainst(contact)` (StandardCellTypes.cs:633-639: `CarrionYield` if
    `!contact.IsAlive`, `PredationYield` if the target is also `Consumer`, else
    `GrazingYield`).
  - **This live-bite code path is never exercised today** — see below.
- Default knobs (StandardCellTypes.cs:569-576): `BiteRate = 20f` J/s per m³ before yield;
  `CarrionYield = 0.8f`; `GrazingYield = 0.5f`; `PredationYield = 0.2f`;
  `ScavengeRate = 1f`. Constructor enforces `CarrionYield` documented as needing to stay
  the largest of the three (comment at StandardCellTypes.cs:562-567), but **this ordering
  is not runtime-enforced** — no check compares the three at construction; only each
  individually is required `∈ [0,1]` (StandardCellTypes.cs:591-593, 606-615).
- `CellTypeIds.Consumer = "consumer"` (StandardCellTypes.cs:16). No separate "predator" or
  "carnivore" type exists — matches the design's stated position that carnivory is
  behavioural, not morphological.

### `TissueContact` / `CellContext` — `src/Evosim.Core/Cells/CellContext.cs`

- `TissueContact` (CellContext.cs:195-216) carries `Type` (`CellType`), `AvailableJoules`,
  `IsAlive` — exactly what `ConsumerCell.Acquire` needs. **This class exists and is fully
  usable but nothing in the simulator ever constructs one for a live creature-to-creature
  encounter** — no `new TissueContact(...)` call site exists anywhere outside
  `CellContext.cs`'s own constructor (confirmed by repo-wide grep).
- `CellContext.Contact` (CellContext.cs:74-79): "Set by the simulator from contact... how
  herbivory and carnivory come apart" — but the only place `CellContext` is actually
  constructed each metabolic step, `Metabolism.StepAt`
  (`src/Evosim.Core/Ecosystem/Metabolism.cs:237-248`), **hard-codes `contact: null`**
  (Metabolism.cs:243). So `ConsumerCell`'s live-bite branch is dead code in every run to
  date; only the carrion branch ever fires.

### `DeathCause` — `src/Evosim.Core/Ecosystem/Organism.cs`

- `enum DeathCause { Starved = 0, Diverged = 1 }` (Organism.cs:262-283). **No
  predation/eaten/killed cause exists.** `Starved` is explicitly "The only cause the
  ecology has" (Organism.cs:264-265); `Diverged` is a numerical-instability censor, "Not an
  ecological cause, and it must never be read as one" (Organism.cs:272-273). A predation
  death (a creature reduced to zero energy, or to zero remaining tissue, by another
  creature's bite) would today be recorded as `Starved` — matches CLAUDE.md's own gotcha
  that `Starved` is "the only `DeathCause` implemented, so cause of death discriminates
  nothing."

### Sensor channels — `src/Evosim.Core/Genome/NeuronInput.cs`, `src/Evosim.Core/Brain/SensorChannels.cs`

- `SensorChannel` enum (NeuronInput.cs:52-98+): `JointAngle=0`, `JointAngularVelocity=1`,
  `Contact=2`, `OrientationUp=3`, `Photo=4`, `Damage=5`, `Chemical=6`, (`Energy`, `Flow`
  follow, not shown in this excerpt). Doc comment on `Contact`: "Originally scoped to
  terrain and Milestone 5. §5A made it aquatic too: contact is how a consumer cell finds
  tissue to bite (§5A.3)" (NeuronInput.cs:60-68). Doc comment on `Damage`: "Available on
  every part... being bitten is the most consequential thing" (NeuronInput.cs:76-98).
- `SensorChannels.Implemented` (SensorChannels.cs:42-48) — the authoritative list of what a
  simulator actually answers today: **`JointAngle, JointAngularVelocity, OrientationUp,
  Depth`. `Contact` and `Damage` are NOT in this list** — i.e. not implemented, matching
  CLAUDE.md's "four sensor channels read" note (though CLAUDE.md names `Chemical`,
  `Energy`, `Flow` as the unread ones — `Contact`/`Damage` are additionally unread and
  CLAUDE.md doesn't call them out by name, but the code confirms it).
- Unity side, `unity/Assets/Evosim/Sim/CreatureSensors.cs:120-147`: `Read(partIndex,
  channel, index)` switch handles `Depth`, `OrientationUp`, `JointAngle`,
  `JointAngularVelocity` explicitly; `default: return 0f;` with an explicit comment:
  "Contact and Damage arrive with predation (Milestone 3); Chemical needs the nutrient
  field sampled at a world position, Energy needs the organism's reserve carried across
  the seam, and Photo is Milestone 6" (CreatureSensors.cs:138-143). **Confirms: Contact
  and Damage sensor reads are unwritten, not merely disabled.**

## 5. Unity collision / spatial model — is there anything to bite yet?

This is the most important code-reality finding for the proposal, and it partially
contradicts DESIGN.md's own "repurposed tiling" language.

- `PhenotypeBuilder.CreatureLayer = 8` (PhenotypeBuilder.cs:102) — **every creature's every
  part shares one Unity physics layer.** Doc comment: "self-collision is deliberately not
  enforced — Sims permitted overlap at joints... This changes at Milestone 5, when land
  needs contact" (PhenotypeBuilder.cs:96-100) — this comment is about a single creature's
  *own* parts overlapping, a different sense of "self-collision" from the toggle below.
- `FluidEnvironment.ConfigureScene(bool selfCollision = true)`
  (FluidEnvironment.cs:120-126): `Physics.IgnoreLayerCollision(CreatureLayer,
  CreatureLayer, !selfCollision)`. Because **all creatures share one layer**, this call
  cannot distinguish "a creature's own parts" from "another creature's parts" — the doc
  comment says so explicitly: "Creatures are kept apart by tiling at 100 m (§6.3) rather
  than by layer, since **a layer cannot distinguish "my parts" from "another creature's"**"
  (FluidEnvironment.cs:116-118). So `selfCollision: true` (the value passed at
  `EvolutionRun.cs:425`, the ecosystem's long-run entry point) means: collisions between
  **any two colliders on `CreatureLayer`** are NOT ignored by Unity — physically enabled
  at the engine level, for both intra- and inter-creature contact.
- **What actually prevents inter-creature contact today is spatial separation, not
  layers**, and this is stated as current fact (not aspiration) in two places:
  - `unity/Assets/Evosim/Sim/Ecosystem.cs:34-40`: "Depth is the whole ecology, for now...
    **Horizontal position is real in physics and ecologically inert: creatures are tiled
    far apart (§6.3) and cannot meet, which is what makes predation a Milestone 7 problem
    rather than one this has to solve now.**"
  - `logbook/0022-the-conveyor-belt-and-the-thin-soup.md:18-19`: "§6.3 tiles creatures a
    hundred metres apart so they cannot collide, which is what makes predation a
    Milestone 7 problem, and a physical corpse would sit in its own tile where nothing
    could ever reach it."
  - No `Vector3`/position-assignment or explicit tile-size code was found in
    `EvolutionRun.cs` itself (grepped; no matches) — the separation is architectural
    (each creature gets its own physics-space tile) rather than a runtime rule visible in
    that file; `Ecosystem.cs` and `logbook/0022` are the authoritative statements of the
    behaviour.
  - **This is a live tension with DESIGN.md itself**: §5A.8's supersession table claims
    tiling was already "**Repurposed.** ... An ecosystem is one shared world where
    creatures must be able to meet, so tiling becomes spatial partitioning rather than
    isolation" (DESIGN.md:1642). The code and the logbook say the opposite is still true
    in the running ecosystem: creatures are isolated by tiling exactly as before, and
    cannot meet. A predation proposal must resolve this before anything else — contact
    detection is moot while horizontal separation is enforced.
- **No `OnCollisionEnter`/`OnCollisionStay`/`GetContacts` call exists anywhere in
  `unity/Assets/Evosim`** (repo-wide, case-insensitive grep) outside of prose comments
  referencing "contact" in a physics/momentum sense. The only per-step "contact" reasoning
  in code is internal momentum-conservation testing in `Milestone1Smoke.cs` (a creature's
  own parts touching each other), not creature-to-creature. **PhysX/ArticulationBody
  natively supports collision events and `Collider.GetContacts`/`OnCollisionEnter` per
  the Unity API, and none of that machinery is wired into the sim** — it is a pure
  addition, not a fix to something broken.

## 6. Primer and logbook — predation intent

- `primer/04-nobody-decides-who-wins.md:23` — "There is no reason for a predator: nothing
  in the objective rewards optimising for distance" (framing exogenous-fitness's absence
  of predation motive, sets up why *endogenous* selection can produce one).
- `primer/04-nobody-decides-who-wins.md:48` — describes `Consumer` cells gaining energy
  "from tissue they touch — which is where herbivores and carnivores come" from, mirroring
  §5A.3, aimed at a lay reader.
- `logbook/0012-the-body-that-cost-nothing.md:19` — "The predator valley §5A.3 worries
  about was not wide. It was infinite" (an early, since-revised note on how bad the valley
  was before the carrion bridge existed).
- `logbook/0022-the-conveyor-belt-and-the-thin-soup.md:18-19` — quoted above (§5 of this
  report): the tiling/predation-deferral rationale.
- `logbook/0010-the-world-starts-with-almost-nothing.md:100` and
  `logbook/0044-three-medicines.md:121` use "bite"/"predates" in unrelated senses (idiom,
  and "precedes" respectively) — no additional design content.
- No entry in `primer/` or `logbook/` proposes a concrete bite/attack/defence mechanism;
  all mentions either restate §5A.3 for a lay reader or note predation as deferred.

## 7. Cost and scale — §5A.9 and current population

- §5A.9's measured throughput table (DESIGN.md:1662-1670) is at populations up to 512,
  self-collision on, and states real-time now holds to 512 creatures with the drag/physics
  optimisations described; the note that "it degrades only where creatures touch, which
  here means predation — rare and local" (DESIGN.md:1704-1706) is a design *expectation*,
  not a measurement — no throughput number in DESIGN.md was taken with creatures actually
  contacting each other, because none do yet (§5 above).
- Current running arms hold populations of roughly **2,305 to 3,493** creatures at the end
  of a 20,000 s run under the current vent-shape settings (logbook/0060-the-outflow.md:100),
  consistent with CLAUDE.md's "2,000–3,500 at the current settings" figure. This is
  4.5-7x the population at which §5A.9's own table stops (512), so per-contact cost at
  today's scale is unmeasured territory even setting aside that contact doesn't happen at
  all yet.
- §5A.9 also names the load-bearing cost driver at current scale: "per-body engine
  interop... is now the largest term we own" (DESIGN.md:1712-1713) — any bite mechanism
  that adds Unity-side per-collider work (OnCollisionEnter callbacks, GetContacts,
  physical proximity queries) lands on exactly the budget line §5A.9 says is already the
  bottleneck, independent of the arithmetic a bite itself would need.

---

## (a) Specified — what the design already decides

1. Predation is a **behavioural outcome**, not a body type: one `Consumer` cell type
   covers scavenging, grazing and predation, differentiated only by what it touches
   (DESIGN.md:1315-1325).
2. **Yield is tiered by target**: carrion highest/no resistance, living-photosynthetic
   moderate ("grazing"), living-consumer lowest and contested ("predation proper")
   (DESIGN.md:1317-1321; coded as `CarrionYield=0.8, GrazingYield=0.5,
   PredationYield=0.2` in StandardCellTypes.cs:572-574).
3. **The predator valley must be bridged**, and the two named bridges are carrion feeding
   (built) and density-dependent cell-type mutation (`Photosynthetic → Consumer`, not yet
   measured for predation specifically) (DESIGN.md:1327-1339).
4. **A bite is an energy transfer between creatures, lossy at every step** — the taker
   keeps only `Yield` of what is drawn; the rest leaves the world as waste, exactly like
   every other feeding transfer (DESIGN.md:1018-1020, CellContext.cs:139-192's
   `CellIntake` machinery, already generalised for this).
5. **Damage is a per-part sensor reading the fraction of a part's own stored energy taken
   over the last step** — scale-free, no separate normalisation constant, and it
   propagates through the existing `ParentNode`/`ChildNode` neuron-input machinery with no
   new relay mechanism (DESIGN.md:552-568).
6. **Contact is a per-part sensor**, needed as soon as predation is, meant to be how a
   consumer *finds* tissue to bite (DESIGN.md:559-561, NeuronInput.cs:60-68).
7. **Attack/defence (resistance, armour, toxicity) is explicitly out of scope** for the
   base mechanism and left for a later, observation-driven layer (DESIGN.md:1341-1343).
8. **Sequencing is owner-ruled (D075)**: predation comes after movement-that-pays and
   after (or alongside) the theatre build, before the cell-type/vent expansion and before
   the archive (DECISIONS.md:3626-3659). It is gated explicitly on perception being
   "worth attempting" (§5A's own table).
9. A carrion death should feed the pool at the same `TissueEnergyPerCubicMetre` a live
   bite would draw from — the parent/offspring/death symmetry that already closes the
   audit (DESIGN.md:1153-1163) — implying a live-eaten creature's remaining tissue must
   still resolve consistently with death-and-deposit accounting, not a separate rule.

## (b) Open — what a proposal must decide

1. **How creatures physically meet at all.** Today horizontal position is "ecologically
   inert" and creatures are tiled 100 m+ apart specifically so they cannot collide
   (Ecosystem.cs:34-40, logbook/0022:18-19) — this contradicts DESIGN.md §5A.8's claim
   that tiling was already repurposed to let creatures meet (DESIGN.md:1642). Resolving
   this — real shared 3D space with movement, vs. some cheaper proximity abstraction — is
   the prerequisite question, and it's exactly what D075 item 1 ("movement that pays")
   is meant to produce first.
2. **How contact is detected and turned into a `TissueContact`.** `CellContext.Contact`
   and `TissueContact` already exist in `Evosim.Core` and `ConsumerCell.Acquire` already
   consumes them correctly (StandardCellTypes.cs:672-676) — the missing piece is entirely
   on the Unity/simulator side: nothing populates `contact:` (Metabolism.cs:243 hardcodes
   null), and no `OnCollisionEnter`/`GetContacts` wiring exists at all. Whether to use
   Unity collision events, an overlap/proximity query, or something cheaper is undecided.
3. **Whether the bitten part or the whole creature is the unit that loses tissue/dies.**
   `TissueContact.AvailableJoules` (CellContext.cs:200-201) suggests part-level
   granularity is already modeled in the type, but nothing decides whether a fully-drained
   part is pruned from the phenotype mid-life, whether the creature just loses reserve
   energy, or whether a bite can sever a limb.
4. **What death-by-predation is recorded as.** `DeathCause` has only `Starved` and
   `Diverged` (Organism.cs:262-283); whether predation gets its own cause (and whether
   that changes how CLAUDE.md's "cause of death discriminates nothing" gotcha reads) is
   undecided.
5. **How the `Contact` and `Damage` sensors get wired into `CreatureSensors.cs`'s switch**
   (currently `default: return 0f` for both, CreatureSensors.cs:138-145) — what "contact"
   means concretely (any collider touching? only another creature's tissue? only a
   `Consumer`'s mouth touching something?) is unspecified beyond the sensor's stated
   semantics.
6. **Whether the `CarrionYield > GrazingYield > PredationYield` ordering is enforced at
   runtime** or remains only a doc comment (StandardCellTypes.cs:562-567) — currently
   unenforced; a proposal changing any of the three should decide whether to add the
   guard the comment implies exists.
7. **Whether/how bite yields should be calibrated against the literature's transfer-
   efficiency figures** (~10% per trophic level, [PC95]/[ED21], LITERATURE-REVIEW.md:
   729-774) rather than the current unmeasured 0.2/0.5/0.8 defaults — §5A.10 already
   flags `ConsumerCell.*Yield` as unmeasured (DESIGN.md:1782).
8. **Attack and defence** — DESIGN.md explicitly defers this (DESIGN.md:1341-1343); a
   proposal should say whether it stays deferred or is scoped in.
9. **Interaction with the matter budget.** D048's matter/energy split (DECISIONS.md:73,
   DESIGN.md:1244-1311) means a bite transfers *energy* cleanly via the existing
   `CellIntake` machinery, but whether a bite also transfers *matter* (nitrogen/
   phosphorus, `World.StandingMatter`) immediately rather than only on death, and how that
   interacts with "death returns matter to the layer it died in" (DESIGN.md:1272-1273),
   is not addressed anywhere in DESIGN.md's predation section.
10. **Per-contact engine cost at current scale.** §5A.9's cost table stops at 512
    creatures and predicts contact cost will be "rare and local" (DESIGN.md:1704-1706) as
    an expectation, not a measurement; current arms run 2,300-3,500 creatures
    (logbook/0060), well past where anything about collision cost has been measured, and
    §5A.9 separately flags per-body Unity engine interop (not raw arithmetic) as the
    already-dominant cost term (DESIGN.md:1712-1713) that any new collision-event
    machinery would add to directly.
