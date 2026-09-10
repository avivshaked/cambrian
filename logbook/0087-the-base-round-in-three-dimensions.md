# 0087 — The base round in three dimensions

**2026-09-10**  ·  round 35 pre-registered: round 33's world with D088's dispersal and current, five seeds at 0.01; the rules written before the first arm has a row

The owner's ruling on the five questions of 0086 was "proceed with your recommendations":
dispersal stays at 5 m, the box keeps its shape, the water slows to 0.1 m/s, founders
reserve the adult as children do, and the plotting library may be installed. Round 35 is
the first round in the three-dimensional world and becomes the base for everything after
it, the free joint (round 34, logbook/0080) first.

## The world

Round 33's launcher (growth, the grid, corpses at 0.005/s, mixing 0.02, the D082 prices,
the 30,000 J ceiling) with D088 on it: a newborn set down over a 5 m disc about its parent,
`CurrentMode Transport` at an RMS of 0.1 m/s over 6,000 s, every destroy immediate, the
placer reserving the adult's radius for births and founders alike. Five seeds, dt 0.01,
30,000 s, workers 2 to 6, `scratch/launch-r35.ps1` defaults. The header must read
`dispersal=5 m` and `current 0.1 m/s transport`, and every arm's manifest must carry one
build and `physicsJobWorkers 0`.

Two things changed since the viewing arm of 0086: the speed (0.3 there) and the founders'
reservation. Both are per-step changes, so no arm of this round replays that one.

## Rules, before scoring

**Verification.** V1: every header carries the two tokens above and the `cols`, `cols abs`,
`x sd` columns; every manifest one `simHash` and one `coreHash`. V2: `audit` 0.0000% and
`mat resid` 0 on every row. V3: `diverged` read on every arm; a run with any is read with
that caveat and a run whose manifest reads `error` is censored.

**The world stands and stays spread.** S1: `cols` at or above 90 of 100 on every sample
after 3,000 s, in every seed. A seed that falls under it is read for why before anything
else in it is read. S2: `positions.jsonl` is read with `scripts/positions-read.py` at 5,000,
15,000 and 30,000 s, and the entry reports the median nearest-neighbour distance and the
kin-within-a-metre count; the pictures at the same three times are taken with
`scripts/theatre-snap.ps1` and the entry says what they show, per the theatre rule.

**The goal rule.** G0: D063 as amended, by connected clade, per seed. The bar is round 33's
five of five; the reading is against it and against round 32's two of five. The question
0079 left open, why the eaters' lines stop recruiting on the grid, is re-asked here: G1, the
inherited stomach births in the last window, per seed, against round 33's 51 to 187.

**The dials.** G2: adult scale, investment and litter at the end, per seed, against round
33's 0.39 to 0.60, 0.20 to 0.49 and 1.9 to 5.1. The 0082 rule stands: three dials pushed
the same way in five seeds of five is the world pushing them.

**Movement.** G3: `mean m/s` is the water and is not read as locomotion. A jointed line's
survival is read on `jnt inh` and `food jnt` against `food rig`, as in 0082's G4. Nothing
about swimming is claimed from this round; that is round 34's.

**What this round cannot answer.** Whether the difference from round 33 is the dispersal,
the current or the founders' reservation: all three moved at once, by the owner's ruling
that the fixed world comes first. The one-at-a-time separations are 0084's bin 3 and come
after the base is read.

## Launch

2026-09-10, 11:28 to 11:29 UTC: `r35-s1` to `r35-s5` on workers 2 to 6, commit `8e1ebaf`,
`simHash b5d31a48…`, `coreHash cafcb692…`, `configHash 2129e5bf…` on all five,
`physicsJobWorkers 0`. Every header reads `current 0.1 m/s transport over 6000 s`,
`dispersal=5 m`, `dt=0.01`; V1's tokens verified from the reports before this line was
written. One monitor watches the five for their end, an error signature and a stalled
report.

## Results

*To be written when the arms land.*

## Sources

Logbook/0082, 0083, 0084, 0085, 0086; D063, D086, D087, D088; `scratch/launch-r35.ps1`.
