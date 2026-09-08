# Proposal: bodies that grow — the birth investment, the litter, and the adult size as genome dials

*Fable, 2026-09-08 night, from the owner's idea of the same evening and the refinement that
followed it. For the round after the grid's base round. Absorbed into DECISIONS.md on
ruling, then deleted.*

## What the world has and what it lacks

A child is born at its full adult size. The parent pays the whole body at the tissue price
plus an endowment of reserve, and `ReproductionTraits` already carries two evolved numbers:
`BroodSize`, the children per event, and `OffspringEndowment`, the joules each starts with.
The remark on the endowment, written before reproduction existed, says what this proposal
does: "with growth the same number would also decide how big it gets to be, and the
trade-off would sharpen considerably." DESIGN §5A.6 defers growth in one line and the
organism's remarks say the body is fixed from birth to death.

Every trait the world has selected on is a switch. A joint or not, a stomach or not, a sense
or not. Switches are what mutation breaks. Size at birth, the litter and the adult size are
dials, and a dial mutates by a little. This is the first place in the world where selection
could climb a slope rather than jump a gap, and it is the axis life history theory is built
on: many small offspring against few large ones, and grow first or breed first.

## The rules

1. **Three genome dials.** `BroodSize` stays. `OffspringEndowment` in joules is replaced by
   `BirthInvestment`, a fraction of the parent's own tissue value that it banks before it
   breeds and then spends on the litter. `AdultScale` is new, one scalar the developer
   multiplies into every node's dimensions, so that the body plan and its size are separate
   things to mutate. Each dial mutates by a graded step under the rates the existing brood and
   endowment mutations use; `AdultScale` under the dimension perturbation.

2. **The threshold is the investment.** A parent breeds when its reserve holds
   `BirthInvestment` times its own tissue joules. It spends exactly that. Each child in the
   litter gets an equal share, and the share is the child's whole start: its body at birth and
   its first reserve, split by a world constant, `NewbornReserveFraction`, default 0.2, kept
   out of the genome so that no lineage can set its children's reserve to zero and pocket the
   difference. A child's size at birth is therefore the investment over the litter, as a
   fraction of its adult body's value. The endowment's job is done by the reserve part of the
   share and the fixed per-offspring overhead stays as it is.

3. **Two edges, and neither is a clamp.** A share worth more than the child's adult body is
   capped at the adult body and the surplus stays with the parent. A share whose body part
   would put any newborn part under `MinNewbornPartKilograms`, a world constant, does not
   make a smaller child: the conception does not happen, and a genome whose investment over
   its litter sits below that floor never breeds. Every divergence on record was a newborn, and
   the lightest link among them weighed 0.14 kg; the floor's first value is 0.5 kg and the
   first screens read the `diverged` column against it.

4. **The body plan is fixed at birth, the scale is not.** The child develops once, at its
   adult size, so that the small-part pruning rule judges the adult and a newborn does not
   lose parts it would have grown. Growth is one scalar per body, from the birth fraction to
   one, applied as the cube root per axis to every part's half-extents; mass follows volume
   and the anchors are re-derived from the extents, which the developer already does.

5. **Growth is a transfer and it comes first.** Each metabolic step a body below its adult
   size moves reserve into tissue at the tissue price, and draws matter from its cell at the
   matter-per-tissue rate, up to what its cell holds. It keeps `GrowthReserveFloor`, a world
   constant, default 0.1 of its current tissue value, as a buffer and invests the rest. A body
   at its adult size invests nothing and banks toward the threshold. Both identities close by
   construction: tissue joules are standing energy and are deposited at death as now, and the
   matter drawn during growth is locked in the body as conception's draw is now. A body that
   cannot draw the matter grows only as far as the energy it has and the matter it could get;
   the shortfall is a column.

6. **Senescence is unchanged.** A genome that grows large spends more of its wear clock
   growing and less breeding. That is the second trade-off and it needs no knob.

7. **Founders are born as children are.** A founder is drawn at its adult scale and placed at
   its own birth fraction with the newborn reserve, so that the founding lottery runs under the
   same rule as every birth. The floor's founder float stays.

8. **The physics changes in place.** At a cadence of one growth step per `GrowthStepSeconds`,
   default 10 s, the harness sets each part's collider extents, mass and both anchors from the
   scaled phenotype without rebuilding the articulation, so the body keeps its velocities, and
   rebuilds the drag panels. Added mass is reapplied from the new volumes. This is the
   engineering in the proposal and it gets a smoke and a replay-identity check before any round.

9. **The record reads the dials.** The report gains the living population's mean
   `AdultScale`, `BirthInvestment` and brood, and the mean body scale of the living against
   their adult size. A lineage birth row gains the child's birth fraction and its adult
   scale. The lineage's per-creature limits stay as they are otherwise.

10. **Every earlier genome and config is refused.** The genome format moves for the two new
    fields and the retired endowment, per §9. Every seed is a new realisation.

## What it changes for the questions already open

The price of a bud (D082) becomes the genome's, since a small newborn is a cheap one, and a
lineage can find the price the world will bear rather than being handed it. A newborn joint
is smaller and lighter than an adult's, which is where the divergences have been, and rule 3
is the guard. Nothing here makes a mover pay. What it makes is a population that can differ
by degree, which every question about movement, sense and predation has lacked.

## Sequence

After the grid's base round has its count. One build: Core first, with the dials, growth and
the ledger under test; the harness second, with the in-place rescale smoked and replayed. A
fast screen for the divergence column and the dials' first drift, then a round at 0.01 that
reads the dials' distributions over time before it reads anything about movement. Predation
waits behind it.

## For ruling

- The two dials beside brood: `BirthInvestment` as a fraction of the parent's tissue value,
  `AdultScale` as one scalar on the plan.
- The three world constants: `NewbornReserveFraction` 0.2, `GrowthReserveFloor` 0.1,
  `MinNewbornPartKilograms` 0.5.
- Growth first, then breeding, with no evolved split between them on this pass.
- Founders born at their birth fraction.
