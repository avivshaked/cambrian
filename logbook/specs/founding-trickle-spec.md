# The founding trickle, and founders that follow their food: the build spec

*Fable, 2026-09-23 late afternoon, from the owner's rulings in conversation that replaced the
timed second founding window (`consumer-founding-spec.md`, superseded): "the evolution keeps
creating founders at probability P per tick ... so that when conditions change, new lines can
evolve", "use probability to make it more likely to land in high matter concentrates", and,
on the empty world, "a world that dies out should be game over. no point in running an empty
simulation if we are not adding creatures to it." D115 and D116. Two rules, both tunables, both
off by default so every recorded world replays.*

## 1. The trickle (D115)

- **`RunConfig.FoundingTricklePerSecond`**, float, default 0 = off; `EVOSIM_TRICKLE` in
  `EnvBinding`; the header token `trickle 1/30 s` or `trickle off`. Round 46's value is
  `1/30`.
- Each metabolic step after the floor has closed (and from t = 0 if it never opens), the
  world draws the number of founders to add from a Poisson of mean `rate · dt` on the world's
  own stream (`Rng.SeedFor(seed, TrickleIndex)`, a new reserved index beside `BedShapeIndex`),
  and spawns each as the floor spawns one: the same random genome draw, the same placer path
  (`TryReserveFounder`, the founder depth spread, the acceptance rule of section 2), the same
  reserve and investment, booked as matter influx exactly as a floor founder is (D098's leg 9,
  `MatterInfluxedTotal`). The lineage row is `k: "f"` as a founder's is, and carries `src:
  "trickle"` beside it so a read separates the two founder sources; the floor's rows carry
  `src: "floor"` from this build. The table gains `trickle` beside `floor`, a per-window count.
- The floor is unchanged: it founds to `MinimumPopulation` and closes at
  `FloorClosesAfterSeconds` as now. The trickle does not top up a crash; it adds at its rate
  whatever the count.
- **The empty world.** The farm's `extinct` ending stays the rule: a world with no living
  body ends the run the step it empties. With the trickle on, founders are still being added,
  so the ending is not taken while the trickle runs; the manifest's ending block names which.
  A world that empties under a trickle is refounded at the trickle's pace and the record shows
  it as a crash with a gap in every inherited column, which the clade scorer reads as new
  clades.
- Refused: a rate that would add more than one founder a metabolic step on average (`rate ·
  dt > 1`), and a rate above 0 with `FloorSpawnsPerStep` 0 (the spawn path is the floor's).

## 2. Founders follow their food (D116)

- **`RunConfig.FoundersFollowFood`**, bool, default false; `EVOSIM_FOUNDERS_FOLLOW_FOOD`;
  the header's founder token reads `founders in their food` (in place of `founders in
  matter`) when on. Refused together with `FoundersFollowMatter` (one rule or the other).
- D109's acceptance is the column's dissolved matter over the tank's richest column. This
  rule chooses the field by the founder's body: the developed phenotype's cell types decide
  whether it eats snow (an absorptive part), light and dissolved matter (a photosynthetic
  part), or both. The acceptance is the column stock of that field over the field's richest
  column, and for a mixotroph the larger of the two ratios. A body with neither (a founder of
  bare tissue) is accepted anywhere, as D109 accepts one when the field is empty.
- `IBodyPlacement.FounderAcceptance` gains the body: `Func<Phenotype, float, float, float>`
  (the D109 delegate is wrapped to ignore it, so a recorded world's draw is unchanged; the
  Unity farm's placer takes the same signature). The `refusals` count the placer keeps is
  read as now.
- The snow's column stock is the detritus grid's (`ColumnStockAt` and `MaxColumnStock` on
  `World.Nutrients`), which exist for the matter grid and are added to the detritus one; both
  read the mask, so a column over the beach's shoal is one live cell.

## 3. Tests

1. The Poisson draw: at rate 1/30 over 30,000 s the count is 1,000 ± 100 and the draw is the
   same sequence for the same seed; at rate 0 the world's stream is not advanced (bit-for-bit
   the recorded world on the crowd fixture's regress).
2. A trickle founder is booked: `MatterInfluxedTotal` rises by its tissue and reserve over ρ,
   both books close after 3,000 s of a dt 0.02 screen, and its lineage row reads `k: "f",
   src: "trickle"`.
3. The floor closes as before with the trickle on, and a world emptied by hand under the
   trickle does not end `extinct` and refounds; the same world with the trickle off ends
   `extinct` the step it empties.
4. Founders follow their food: on a grid with snow in one column and dissolved matter in
   another, a stomach founder's accepted places cluster on the first and a leaf's on the
   second (a thousand draws, the ratio over 5:1), and a mixotroph's on both; with
   `FoundersFollowMatter` on and this off, the draw equals D109's bit for bit.
5. `RunConfigTests` and `RunConfigJsonTests` pass untouched; both fields refuse every earlier
   config.

## 4. What the round reads

`trickle` against `floor` and `births`; the consumer founders' places against the snow's
columns (positions at their first sample against the column sums); whether any trickle
founder with a stomach leaves an inherited consumer line (`ink` inherited on a birth whose
parent's source is the trickle), which is the pre-registration's K3 in its new form; and
whether the crowd's evolution is contaminated (the inherited columns against the founders'
share of births, under 5%).

## 5. As built (2026-09-23 evening, an Opus subagent in a worktree, merged at `3f48f6f`)

Sections 1 to 3 as written, with 25 tests: eleven for the trickle (the draw at 1/30 gives
1,006, 1,024 and 1,007 founders over 30,000 s for seeds 1 to 3, never more than two a step,
and repeats; the stream is not drawn at rate 0; a trickle founder is booked as influx to
1e-9 and its row reads `k: "f", src: "trickle"`; both books close under a trickle of 0.2 a
second over 3,000 s; the floor closes as before; a world emptied by hand is refounded with
the trickle on and stays empty with it off; the refusals), five for the food rule (a stomach
lands on the snow 8.3 to 1, a leaf on the matter 8.9 to 1, a mixotroph about evenly, bare
tissue anywhere; under D109 the acceptance equals the old arithmetic exactly for every body),
and nine in the farm (the `extinct` decision under each setting; `EVOSIM_TRICKLE` as a number
or `1/N`; the tokens and the column). Eight departures. `WorldState.StateVersion` is 9 and
`Checkpoint.Version` 4, because a resume that did not carry the trickle's generator and its
count would redraw founders the unbroken run had drawn. The `trickle` column is appended at
the table's end, not beside `floor`. A rate above 0 with a floor that never closes is
refused rather than left inert, and the food rule off a grid is refused where D109's did
nothing. The rate·dt bound is checked at the setter against the half-second step and again at
`EnforceTrickle`. `MaxColumnStock` is recomputed for every candidate spot as D109's was, a
full pass over the 1 m snow grid, milliseconds at one founder per 30 s and not timed.
`EVOSIM_TRICKLE=1/30` is read as `1f/30`, so a decimal spelling hashes the same only if it
parses to that float; the header prints `1/30 s` either way. And the Unity farm binds
neither variable; its founder rows carry `src: "floor"` from this build.
