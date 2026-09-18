# Proposal: the light's reach, for round 40

**2026-09-18**  ·  for the owner's ruling. The owner ruled the direction this morning ("Let's do it"); this file puts the value and the shape of the round in front of them. On ruling it is absorbed into DECISIONS.md as D096 and deleted.

## What is proposed

One number moves: the light's attenuation depth, the depth over which the irradiance falls
by a factor of e. It has been 12 m in every round since the light model was built, and it
was a hard-coded 12 in the launcher until today. The proposal is **6 m**, exposed as
`EVOSIM_LIGHT_REACH` and carried in every header as `light reach 6 m`. Nothing else in
the world changes. Round 40 is round 39's world with the reach halved, five seeds, three
arms at a time.

## Why 6 m

The ledger priced one leaf from round 39 (seed 1's snapshot at 30,000 s, the smallest kind
of leaf, one part) at every depth with the reach at 12, 9, 6, 4 and 3 m. The number that
matters is the deepest water where the leaf's expected children per lifetime reaches one.

| reach | deepest depth with R0 ≥ 1 | R0 at 2 m | R0 at 6 m | R0 at 10 m |
|---|---|---|---|---|
| 12 m (today) | 20 m | 10 | 6 | 4 |
| 9 m | 15 m | 9 | 5 | 2 |
| **6 m** | **10 m** | **8** | **3** | **1** |
| 4 m | 6 m | 6 | 1 | 0 |
| 3 m | 4 m | 5 | 0 | 0 |

At 12 m the leaves float at 16 to 20 m, at the edge of where a lone leaf still breeds, and
45 m of the water is lit enough to live in. At 6 m the same leaf breeds only in the top ten
metres and its children come three times as fast at 6 m as at 10 m, so the band both rises
and steepens. At 4 m the band is the top six metres, which is the surface film that has
been trouble before (0033, 0056). Six is the halving that moves the band out of the deep
water without pushing it against the top.

The lit volume follows from the same table. At 6 m, the top ten metres of the 2,200 m² disc are 22,000 m³ of
the tank's 99,000 m³. Everything below is dark water where detritus sinks and no leaf can
live, which is the niche the eaters have never had.

## What is expected, and what would falsify it

The pre-registration entry carries the predictions with their numbers. In outline:

1. The leaves pack into the top ten metres: the photosynthetic median depth above 10 m at
   15,000 and 30,000 s in 4 of 5, against 16 to 20 m in round 39.
2. Shading becomes a selected quantity: `shade %` above round 39's peak in 4 of 5, and the
   leaves' adult scale or lift moving by degree with depth (the rung 2 candidate).
3. The eaters get the dark column: the absorptive median depth below the leaves' by more
   than round 39's gap in 4 of 5, and an absorptive clade alive at the end in more seeds
   than round 39's two.
4. The boom and bust slows: the first bust takes longer than round 39's 1,900 to 2,600 s
   in 4 of 5.
5. More matter is free: the standing matter in bodies below round 39's 70 to 75% in 4 of 5.
6. The base readings hold: the disc stays mixed, nothing throws, the matter's books close.

The two-sided readings are written into the entry. If the leaves pile into the surface
film, with their median above 3 m, the reach is too short and 9 m is the retry. If the
eaters bust as fast as before, the dark column is not what limits them, and the read turns
to the matter.

## What it costs

Nothing in code beyond the one launcher line, already made. `simHash` has moved since round
39 for the timing split, so the round runs on a new build in any case; every worker is
refreshed before the launch. Five seeds at three arms is two batches of about eleven hours
at round 39's crowd, and the crowd may be smaller.

## What is not proposed

Changing the surface irradiance, the day cycle, or the leaf's efficiency. Combining the
reach with the shelf: the shelf goes into the lit band the new light defines, in its own
round after this one.
