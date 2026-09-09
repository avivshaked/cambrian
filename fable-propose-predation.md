# Proposal: predation on contact — what a bite is, in a world where bodies touch

*Fable, consolidated 2026-09-07 into one operative design. It folds in the seven conditions
added after the outside review of 2026-09-06 and the points the Astra review of 2026-09-07
raised, so the owner rules on one text. The 2026-09-06 draft, which this replaces, is in
the git history. Built on `scratch/predation-survey.md`, DESIGN.md §5A.3 and §4.4, D076 and
D077. For the ruling after round 28 and the movement round (D075's order). Absorbed into
DECISIONS.md on ruling, then deleted.*

> **Premises moved, 2026-09-09.** D086 made the water a grid (a body feeds from the cell
> holding its centre, and a corpse is a particle that sinks and decays) and D087 made
> bodies grow (colliders, mass and anchors are reset in place every growth step, and a
> newborn is a fraction of its adult). So the integrity pool cannot be pinned at birth, the
> resize this proposal said was not possible is built, and every "layer" below is a cell.
> The proposal is re-cut before it is put up for ruling.

## What the world now has that it did not

Creatures share one volume and touch. Round 28's arms count 300 to 1,500 contact pairs
per physics step at the fine step (logbook/0070), through `Physics.ContactEvent`, with the
audit closed and no measurable cost from the contacts themselves (0064). The bite machinery
exists and has never run: `ConsumerCell.Acquire` has a live-bite route reading a
`TissueContact`, tiered by what it touches (carrion 0.8, living photosynthetic 0.5, living
consumer 0.2, all unmeasured), and the metabolic step hands it `contact: null`. `Contact`
and `Damage` are specified per-part sensors that read zero. This proposal is the seam
between the two: which contacts become bites, what a bite takes, what the bitten loses,
and how it is all counted.

## The rules

1. **A mouth is a part with a `Consumer` cell.** Nothing new in the genome; founders and
   cell-type mutation already draw the type. Mouth volume is the part's consumer tissue.

2. **A bite is a contact, held, between a mouth and another creature's part.** Each
   metabolic step the contact pairs of the last physics step are read, and a pair is a bite
   if it also existed at the start of the step: one step of continuous contact, so a
   glancing collision is not a meal and a body pressed against another is. No proximity
   rule and no draw from the RNG. Contact is physics.

   The contact counter keeps no identity, so the bite path keeps its own: a canonical
   (creature, part) key for each collider, pairs deduplicated across colliders, and the set
   ordered by stable id before it is walked. The callback may run on a worker thread, which
   matters now that the physics thread count is recorded and the shared world replays only
   at zero workers (D078): contacts are collected there and consumed on the main thread in
   that order.

3. **What a bite takes, and when.** Bites are priced after the feeding allocation of the
   step, from a frozen list of contacts, never while other meals are still being priced: the
   feeding fault the Astra review reproduced (R1) is what happens when an interaction is paid
   while the ones after it are still being priced. Each mouth may take up to
   `BiteJoulesPerSecond × V_mouth × Δt` from its target; each target's total loss in a step
   is capped first, and several mouths on one target divide that cap in mouth proportion.
   The draw goes through the existing `TissueContact` → `Acquire` path with its tiered
   yields; the eater keeps the yield, and the rest is waste as in every transfer.
   `BiteJoulesPerSecond` is a tunable (`EVOSIM_BITE`), **default 0**, so every recorded world
   replays byte for byte; the screen sets it. A part cannot bite its own creature.

4. **What the bitten loses: an injury pool with fixed geometry.** `TissueJoules` is a figure
   for the whole creature while part volumes come from the phenotype, so a per-part loss of
   tissue and volume has no balance and would need a collider to change size mid-life. A
   creature therefore carries an integrity pool, equal to its tissue joules at birth, that
   bites draw down; its body does not change shape. Lifecycle: the pool is full at birth,
   nothing heals it in the first cut, it is separate from the energy reserve (a bite does not
   make the bitten hungrier; it makes it closer to dead), and a creature whose pool reaches
   zero dies with `DeathCause.Eaten`, the third cause, through the normal death path. What
   death releases is what it releases today: the remaining tissue joules and the locked
   matter, less what bites already carried away.

5. **Matter moves with the bite, into a named reserve.** The joules taken carry their
   `MatterPerTissueJoule` share out of the bitten creature's locked matter. The yield's share
   goes into the eater's *captured matter reserve*, a named field beside its locked matter
   and not silently into the structural matter of an unchanged body; the waste share returns
   to the layer's free matter where the bite happened. The reserve is what an eater breeds
   from first: a conception draws its matter price from the reserve before the layer, so a
   predator is a body that need not wait on the water for matter. Death returns the reserve
   with the rest. The identity `StandingMatter = free + locked + reserves` holds by
   construction, and a test asserts it.

6. **The senses.** `Contact` and `Damage` wired in `CreatureSensors` and added to the pool
   behind `SenseContact` and `SenseDamage`, default off, in the perception build's pattern.
   `Contact` reads 1 on any part in a creature–creature pair that step; `Damage` reads the
   fraction of the pool the last step took. Without them a consumer is a filter feeder that
   eats what the water presses on it; with them it can learn to hold and to flee.

7. **Instruments.** `bites` and `bite J` per window; `eaten` per window; `consumer` and
   `consumer inh` (the guild has never had a column); mean depth of consumers against
   producers; the food chain in the footer; and the income streams kept apart, carrion
   against live bite against light, so a surviving consumer that lives on corpses is not
   reported as a predator.

8. **Out of scope.** Attack and defence (§5A.3 defers them), limb loss, healing, any change
   to the yields (the screen reads them), any change to the vent or the box.

## Why contact and not proximity

The 2026-09-05 draft proposed an encounter rule drawn from the RNG because creatures could
not meet. They can now, and the measurement removed the cost objection (D076). Contact
keeps everything the design says predation is for, morphology as the sense organ, the body
computing a bearing, a chase through water, and it keeps the record honest: a bite happens
where two bodies are, not where a lottery says they might be.

## What the numbers are, and are not

The three yields are engineering hypotheses until the literature round on live contact
feeding lands (research §9's queue). Population-level trophic transfer efficiency, the
13% of [ED21], is not a per-bite assimilation yield, and the proposal does not read it as
one. `BiteJoulesPerSecond` is found with the ledger before any worker is spent (D069): the
minimum dose at which a biter replaces itself on a producer of the round's typical size,
then a narrow bracket around it. Two guessed doses are not the screen.

## The round that would test it

After round 28 is read and the movement round has run. **At dt 0.01 from the first screen**,
not 0.02: a bite needs contact to persist between metabolic steps, so the physics step
changes the treatment itself. Seeds 2 and 4, the ledger's bracket of doses against 0,
senses `Contact` and `Damage` on in one arm per dose and off in the other, so the dose and
the senses are never changed together in the first comparison. Two-sided: consumers persist
as an inherited line and producers are not driven under at one dose or the other; or
consumers never hold, in which case the carrion bridge is measured as insufficient and
§5A.3's second bridge, density-dependent cell-type mutation, is the next lever.
Confirmation at 0.01 under D063 as amended with a predation clause the owner words then.

Three things are reported apart, because they are three claims. Carrion consumption is what
the world already has. Contact feeding is what the vent's crowd will produce first, bodies
pressed together by the conveyor: it is reported as contact feeding and not as predation.
Active pursuit, a consumer reaching or holding prey through its own actuation, is claimed
only when the movement assay shows it. `eaten` against `starved` is the first time cause of
death discriminates anything in the record.

## What the owner rules

1. A bite is a held contact between a mouth and another creature's part, with the identity
   and ordering of rule 2.
2. The unit of loss is an integrity pool with fixed geometry; death at zero;
   `DeathCause.Eaten` as the third cause; no limb loss and no healing in the first cut.
3. Matter moves with the bite into a named captured-matter reserve that a conception draws
   from first.
4. Bites priced after the feeding allocation, each target capped before mouths divide it.
5. Yields left as coded for the screen; `BiteJoulesPerSecond` found by the ledger; the
   round at dt 0.01; contact feeding reported as such until pursuit is shown.
