# The consumer's second founding: the build spec

*Fable, 2026-09-23, for `fable-propose-round-46.md`'s question 2, the recommended form (a
second founding window when there is food), written before the ruling so the build is a day.
The other two forms (intake on non-consumer founders; a larger consumer reserve) are not
specified here; if one is chosen this file is rewritten.*

## 1. The fact it answers

Round 45: 24, 26 and 14 consumer founders a seed, every one dead childless by 447 s at median
ages of 20 to 55 s; the charged field at 100 s held eleven units over a million cells; the
floor closed at 3,000 s; the snow reached its plateau of 80 to 110 kJ by 5,000 s; seed 3's 62
corpses lay uneaten. The founding lottery draws a mouth into a world with nothing to eat.

## 2. The rule

From `ConsumerFoundingAtSeconds` (0 = off, the recorded world) the world admits
`ConsumerFoundingCount` founders that carry a consumer node, spread evenly over
`ConsumerFoundingWindowSeconds` at the floor's own cadence (`FloorSpawnsPerStep` a metabolic
step until the window's share for that step is met), whatever the living count is and
whether or not the first floor has closed. A founder is drawn as `EnforceFloor` draws one
(`GenomeFactory.Founder` with the run's cell registry, so the consumer node carries this
run's intake cap) and kept only if it has a consumer node; a draw without one is discarded
and drawn again, each draw from its own `Rng.SeedFor(Seed, _nextIndex++)` slot, so the
world stays one realisation of its seed and the discard changes no other body's draw. Placement
is the founder placer's (`TryReserveFounder`, the founder depth spread, D109's founders-in-matter
rule if on), so the second cohort lands where the first would. They are founders in every
column: generation 0, `floor` counts them, `gen min` reads 0 while they live, and the lineage
row carries `founding: 2` so the clade scorer and the reads can tell the cohorts apart.

## 3. Where it lives

- **`RunConfig`**: the three tunables above, `EVOSIM_CONSUMER_FOUNDING_AT`,
  `EVOSIM_CONSUMER_FOUNDING_COUNT` (40, the first founding's size), `EVOSIM_CONSUMER_FOUNDING_WINDOW`
  (1,000 s); the header token `consumers found at 6000 s (40 over 1000 s)` or `consumers
  found once`. They refuse every earlier config, as every tunable does; `RunConfigTests` and
  `RunConfigJsonTests` catch an omission.
- **`World.EnforceFloor`** gains the second window beside the first, sharing the founder
  draw and the acceptance (`EnsureFounderAcceptance`); the counters `SecondFoundingSpawned`
  and `SecondFoundingDiscards` in `stats.jsonl`, the table printing `found2`.
- **`lineage.jsonl`** birth rows carry `fnd` (1 or 2) on a founder row.
- **`WorldState`** checkpoints the window's progress (spawned so far), so a resume across it
  is the unbroken run (`CheckpointFidelity` reads it).
- The Unity farm does not bind the three; a world built there founds once.

## 4. What it does not do

It does not choose the founders' genomes beyond the guild: a random consumer is as likely to
be a bad one as the first cohort's were, and the lottery is what founds. It does not feed
them: the window is set where the snow already is (6,000 s in the pre-registration; the
reader's `snowJ` at 5,000 s in every round 45 seed says the plateau is there), and a cohort
that still starves says the mouth's economy is short, not the founding. It is not the
inoculation route (`InoculateAtSeconds` admits copies of one named genome; this admits random
founders of a guild).

## 5. Tests

1. With `ConsumerFoundingAtSeconds` 0 the crowd fixture's 3,000 s regress is identical in
   every field and the lineage byte-equal.
2. With it at 100 s, count 10, window 100 s, in a Core world with the first floor closed:
   ten founders with a consumer node are admitted between 100 and 200 s, none before, none
   after, the discards counted, and no other body's draw moves (the non-founder lineage rows
   before 100 s byte-equal to the run without it).
3. A resume from a checkpoint inside the window admits the remainder and not the whole.
4. A 3,000 s dt 0.02 screen of round 45's launcher with the window at 1,500 s (the snow is
   thin there; the screen is of the machinery, not the ecology): the second cohort's births
   appear at 1,500 s with `fnd 2`, both books close, and `intake %` reads above 0 while they
   live.
