### D087

**2026-09-09 — Bodies that grow**

**Status:** ruled by the owner, in conversation: the design on 2026-09-08 night ("love it!
go ahead"), the five constants the build put in front of the owner on 2026-09-09 ("proceed
with your recommendations"). Built in both halves and smoked the same day (logbook/0081);
round 34 is its first round, after round 33 reads. Absorbs `fable-propose-growth.md`,
deleted on ruling.

**Context.** Every creature the world had held was born at its adult size: a parent paid a
whole body at the tissue price plus an endowment of reserve, and `ReproductionTraits`
carried the brood and that endowment as its two evolved numbers. Every trait the world has
selected on is a switch, a joint or not, a stomach or not, and a switch is what mutation
breaks. The owner's idea was the first dial: size at birth, the litter and the adult size,
each mutating by a little, so that selection can climb a slope rather than jump a gap.
The refinement that followed replaced a size-at-birth dial with a threshold, so that the
size of a child is the product of what the parent banked and how many it split it over.

**Ruled.**

1. **Three genome dials.** `BroodSize` stays. `BirthInvestment`, a fraction of the parent's
   own tissue value, replaces `OffspringEndowment` in joules. `AdultScale` is one scalar the
   developer multiplies into every node's dimensions, so the plan and its size are separate
   things to mutate. Each dial mutates by a graded step under one gate, `InvestmentChance`
   and `AdultScaleChance` at 0.08 each (the build had gated the two new dials twice, at
   0.0064 per birth in effect; one gate is the ruling). The genome format is 5.

2. **The threshold is the investment.** A parent breeds when its reserve holds
   `BirthInvestment` times its own tissue joules plus the brood's overhead, and spends
   exactly that. Each child's share is the investment over the litter; the share is the
   child's whole start, its body at birth and its first reserve, split by
   `NewbornReserveFraction` (0.2), a world constant kept out of the genome so that no lineage
   can set its children's reserve to zero and pocket the difference. The investment's
   default is 0.5, and founders draw theirs from 0.25 to 1.0, so that the founding lottery
   samples the dial.

3. **Two edges, and neither is a clamp.** A share worth more than the child's adult body is
   capped at the adult body and the surplus stays with the parent; the reserve is capped
   beside the body, so that a child cannot be born with more reserve than body. A share
   whose body would put any newborn part under `MinNewbornPartKilograms` (0.5 kg) does not
   make a smaller child: the conception does not happen, counted as
   `ConceptionsUnderMassFloor`, and the parent keeps its reserve. Every divergence on record
   was a newborn and the lightest link among them weighed 0.14 kg.

4. **The plan is fixed at birth, the scale is not.** A child is developed once, at its adult
   size, so that the small-part pruning rule judges the adult and a newborn never loses a
   part it would have grown. Growth is one scalar per body, the cube root of its body
   fraction on every axis, applied to every half-extent, position and anchor.

5. **Growth is a transfer and it comes first.** Each metabolic step a body below its adult
   size moves reserve into tissue at the tissue price and draws matter from its cell at
   `MatterPerTissueJoule`, keeping `GrowthReserveFloor` (0.1 of its tissue value, twelve to
   seventeen seconds of upkeep in the smoke, kept and read in the round) as a buffer. A body
   short of matter grows what it paid for and the shortfall is counted
   (`GrowthShortOfMatter`). Both books close by construction: tissue is standing energy and
   is deposited at death, and matter drawn during growth is locked in the body as
   conception's draw is.

6. **Senescence is unchanged.** A genome that grows large spends more of its wear clock
   growing and less breeding, and that trade needs no knob.

7. **Founders are born as children are**, at their own birth fraction with the newborn
   reserve, and the floor's founder float stays.

8. **The physics changes in place.** Every `GrowthStepSeconds` (10 s) of simulated time the
   harness sets each part's collider extents, mass and both anchors from the scaled
   phenotype without rebuilding the articulation, drops the drag panels so that the one
   place they are built rebuilds them, and recomputes mass from volume so that added mass
   cannot compound. `resizeJumpMetres` (exact, read 0 over 1,093 resizes) and
   `resizeStepMetres` (the root's travel in the step after, blind to a snap smaller than
   the current's drift) are the instrument.

9. **The runaway ceiling reads biomass.** `MaximumPopulation` counts bodies, and a body is
   no longer a unit of biomass; `MaximumTissueJoules` ends a run as a runaway when the
   living bodies' standing tissue exceeds it, beside the count. Round 34's value is 8,000
   bodies' worth of round 32's mean tissue per body (the arithmetic is in the launcher).

10. **The record reads the dials**: the living population's mean adult scale, investment,
    brood and body fraction in the table (`adult scale`, `invest`, `brood`, `body frac`),
    `bf` and `as` on lineage birth rows, the two counters in `stats.jsonl`.

**Rejected.** A size-at-birth dial beside the threshold (the owner's own refinement: size is
the product of the threshold met and the litter). An evolved reserve fraction (a lineage
would set its children's reserve to zero). A clamp at the mass floor (a smaller child than
the parent paid for is a different creature wearing its price). Growing the plan by adding
parts, deferred with §4.2's pruning rule as the reason.

**What it changes.** Every stored genome is refused (format 5; re-extract from a new
snapshot), and every seed is a new realisation. At the proposal's defaults breeding is two
to five times cheaper than it was, and 0081's smoke bred seven times faster through founding
than round 32's same seed and stripped the matter at its layer within 300 s; a world that
breeds like that is matter-limited from the start, and round 34 reads whether that is a
different ecology or the same one reached sooner. The price of a bud (D082) becomes the
genome's, since a small newborn is a cheap one; a newborn joint is smaller and lighter than
an adult's, which is where the divergences have been. `rounds/launch-r34.ps1` carries the
growth knobs and the header carries `growth reserve= floor= minkg= step= invest=
scale/invest chance=` and the biomass ceiling.

