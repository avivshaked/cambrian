# 0100 — A tank four times as wide

**2026-09-14, night, pre-registered before launch**  ·  round 38: round 37b's world at 400 m² with the matter held at 6,000 units and corpses as objects (D089 rulings 3 and 4, confirmed by the owner 2026-09-13); a replacement under D091, read for its mechanism, the goal rule read and not required

## What it asks

In every world so far the bodies sit a body length apart: round 35's median nearest
neighbour was 0.63 to 0.70 m, round 37b's 0.66 to 0.74 m. With food within reach in every
direction a sitter eats as well as a swimmer, and that, as inference, is why movement has
never paid its price in thirty-seven rounds (D089's context). Round 38 keeps every unit of
matter and gives it four times the water. The question is not whether the world stands. It
is what a dilute world does to the crowd, to the food's shape and to founding. Rounds 39
to 41 then price a stroke and a sense in a world where they have something to buy.

## The world

Round 37b's world (0095's five changes on the tank; ten seeds read the same, 0099) with
one knob turned and one value held. The area goes to 400 m², the disc's radius from
5.64 m to 11.28 m. The matter budget is held at 6,000 units, which is what 37b's 100 m²
held at one unit per cubic metre, so the seeded density falls to 0.235 units/m³. The 5 m
matter cell's mask overshoots the disc by 6%; the total is exact. Corpses decay at
0.005/s as since round 32. The launcher is `rounds/launch-r38.ps1`: seeds 1 to 5,
30,000 s, dt 0.01, through the queue with the pre-registration check on this entry. The
build is 37b's with the trace's second pass and the field's coefficient of variation
merged. Both move the hashes, so the smokes and the digest are run before the merge and
named in the launch section. The report carries `det cv` and `mat cv` for the first time,
and every diverged dump names its first non-finite step. Round 37b's ten seeds are the
control for every reading below.

The arithmetic was checked before launch (D089's check 3, run 2026-09-13). A 5 m matter
cell holds 31 units at the new density and a child costs 8 to 16, so a founder that
reaches one cell can breed. The grid is about 24,000 detritus cells over 400 live columns.
The physics follows bodies, so the pace should follow the population and not the water.

## Rules, before scoring

V1: every header carries `space tank r=11.28 m (400 m2), depth 60, wall, bed`, `area 400
m2`, `matterBudget 6000`, `fluidAccel 1`, `current 0.1 m/s transport`, `dispersal=5 m`,
`driveLimit >0.01`, `linkPhoto 0.5`, `addedMass 0.5`, `dt=0.01`, and every manifest one
`simHash`, one `coreHash`, one `configHash`, `physicsJobWorkers 0` and `prereg.json`
naming this entry's commit; the three hashes are recorded in the launch section at the
first launch and every later manifest must match them. V2: `audit` 0.0000% and `mat resid`
0 on every row. V3: `wraps` 0 on every row. V4: every diverged dump has its trace file, and
the trace names a first non-finite step with at least one finite frame before it. V5: a
manifest reading `error` or `stopped` is censored and read at its last sample. The entry is
not written until the world has been watched in the theatre, and frames of a live arm are
looked at at about 3,000 and 6,000 s.

## Predictions

Round 37b's ten seeds: `alive` at 30,000 s 1,585 to 1,840; births by 1,100 s about 120
with `mat blk` near a hundred attempts per birth; the rim quarter 0.17 to 0.27; `x sd`
2.6 to 2.9 m on a disc that reads 2.82 spread; the three-dimensional nearest neighbour
0.66 to 0.74 m; inherited eaters at least 10 at the end in ten of ten; seeds holding at
least 10 inherited jointed bodies at the end three of ten; throws 43 in three seeds, none
in the fresh five; peak-to-trough of `alive` after 5,000 s 1.1 to 2.4.

| # | prediction | falsified by |
|---|---|---|
| D1 | **founding survives the dilution**: births by 1,000 s at least 40 in every seed, and `alive` at 5,000 s at least 300 in 4 of 5 | the timeline; `mat blk` and `mat short` against `births` in the first 3,000 s are recorded beside it |
| D2 | **the world stands, smaller or not**: `alive` at 30,000 s between 700 and 2,300 in 4 of 5 | `alive` |
| D3 | **the crowd thins as the water does**: the three-dimensional nearest-neighbour median at 15,000 and 30,000 s between 1.3 and 1.9 of 37b's at matched abundance in at least 3 of the anchors that match (a quarter of the density in three dimensions reads 1.59) | `nn_matched.py` against 37b's ten |
| D4 | **the disc stays mixed at the new radius**: the rim quarter between 15% and 40% at 5,000, 15,000 and 30,000 s in 4 of 5; `cols` at least 60% of the live columns from 5,000 s; `x sd` between 4.0 and 6.8 m at the three times (a spread disc of radius 11.28 m reads 5.64) | `p3` over `alive`; the reader |
| D5 | **the eaters stand in thin water**: inherited absorptive bodies at least 10 at the last sample in at least 3 of 5; D063 as amended read and not required | `inherit`; the scorer |
| D6 | **the joint's fate does not turn on the dilution alone**: seeds holding at least 10 inherited jointed bodies at 30,000 s between 0 and 2 of 5 (the base's rate is three of ten) | `jnt inh` |
| D7 | **the tank throws only newborns, and the trace sees the step**: `diverged` between 0 and 10 per million jointed body-seconds in 4 of 5; every dump a jointed body under 10 s old with a mass ratio under 3; every trace with a finite frame before its first non-finite step | the manifests, `diverged-read.py` |
| D8 | **the pace follows the bodies**: wall seconds per 1,000 simulated seconds per living body within 0.7 and 1.6 of 37b's ten-seed mean | `run.json`, `pace.tsv`'s normalisation |

Recorded and not predicted, because the instrument is new or the baseline absent: `det cv`
and `mat cv` at every 1,000 s per seed, the first reading of the field's coefficient of
variation on any round, from which round 39's threshold is set. Also recorded: `corpses`
and `det deep`; stillbirths against births; `mean m/s`, still the water's; the founding
time and kind of every passing clade. And the depth band's shape from the positions file,
since a crowd that compresses into the lit band would read D3 below 1.3 for a reason the
reading should name.

## The two-sided readings

- **D1 fails:** the dilution starves founding. The round is censored as a world that cannot
  start, and D089's ruling 3 is amended to the owner (a smaller step, 200 m², or more
  matter) before anything else runs; the ledger's check was wrong about what a founder
  reaches.
- **D2 fails low (a seed under 700):** the world stands only where the food gathers, and the
  entry reads where that is from `det cv` and the positions file; the bed with shape
  (round 39) is then a rescue and not a refinement.
- **D3 reads below 1.3 with D4 holding:** the crowd did not thin with the water; it found
  the lit band and packed it, and the dilution bought nothing a sense could use. Read the
  depth band's spread against 37b's before saying so.
- **D3 reads above 1.9:** the crowd is thinner than the water, so bodies are avoiding each
  other or dying where they are dense; the eaters' distribution is the first thing to read.
- **D5 fails:** thin water starves the eaters first, which is the reading the shading and
  the eaters' recruitment questions (0079) wait on; the goal rule is not required, so the
  base still moves to 400 m² on the owner's ruling, and the entry says the eaters' bar moved.
- **D6 reads 3 or more of 5:** the dilute world keeps the joint where the dense one lost it,
  which is the first sign in thirty-eight rounds of a stroke buying something; round 41
  moves up.
- **D7 fails on the anatomy (an adult thrown):** a second mechanism; the trace is the
  instrument. **D7 fails on the trace (no finite frame before the first non-finite step):**
  the ring's second pass missed again and the blow-up is inside one step; the drive is read
  next.
- **D8 fails:** the grid or the field's reading costs more than the arithmetic said; the
  per-step profile is not available and the entry says so.

## What the round does not ask

Not whether a stroke pays: nothing prices one differently here. Not the pockets' shape
beyond the ring statistic and `det cv`. Not the bed (round 39), the light sense (round 40)
or the fluid terms (before round 41).

## Launch

*2026-09-15, 00:20 to 00:23.* The build was validated one Editor at a time under the cap
(`scratch/r38build-chain.ps1`, log `scratch/logs/r38build-chain.out`) on a candidate tree
of main with `trace2` and `fieldcv` merged (worker 3 carrying its Assets and its Core).
The shared-space smoke passed with part 5's forced case: three finite frames held, the
first non-finite step and link named, every frame's links carrying drag, acceleration
force and water acceleration. The 600 s box digest (`boxdig-r37b` on main's build against
`boxdig-r38build` on the candidate, seed 3, dt 0.02) is identical over all 31 steps. The
dilute tank smoke `r38smoke` (600 s, dt 0.02, seed 3) printed the header this entry's V1
names and founded: 166 births by 600 s against 93 in 37b's seed 1 at the same time, with
about 90 blocked attempts per birth as before, `mat here` 0.037, no stillbirth, both
ledgers closed, `det cv` 2.4 falling to 1.2 and `mat cv` 0.17 rising to 0.47 as the first
bodies drew the matter down. Main was fast-forwarded to the candidate (`582ee6a`) and the
workers refreshed. The manifest's `coreHash` on the candidate read main's, since it hashes
the main tree's Core by path (CLAUDE.md's gotcha from this chain); the build's hashes are
the first launched seed's, below.
