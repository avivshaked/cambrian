# 0089 — A hinge on a producer

**2026-09-11**  ·  round 36 pre-registered: round 34's free joint with the link part earning light at half a leaf's rate; the first of the ten rounds ruled this morning

Round 34's first seed (0080's Launch section) read the free joint through founding, 73
inherited jointed bodies at 5,000 s against 72 at 1,000, and then lost them, 11 at 15,000 s
and none at 30,000. The price kept joints from founding; something else thins them after.
0080's two-sided reading for that shape names drift or a slow disadvantage in the body
itself, the part the joint hangs, which pays tissue's upkeep and earns nothing. This round
removes that. The link cell has had a photosynthetic efficiency since D032's neighbourhood
(DESIGN §5A.10, "unmeasured, deliberately defaulted to zero"), with the note that every
early swimmer fed and moved with the same cell and that a muscle earning nothing is a
late, large-animal arrangement charged to the first thing that ever moved. It is launchable
as `EVOSIM_LINK_PHOTO` and has never been set.

## The world

Round 34's launcher, itself round 35's with the four prices at zero (the idle charge at
0.0001 W per newton-metre, since Core refuses a literal zero), with the link part earning
light at 0.5 of a leaf's efficiency. Nothing else moves. Five seeds, dt 0.01, 30,000 s,
`rounds/launch-r36.ps1` defaults. Round 34's same seed is the control; the build is round
35's (`simHash b5d31a48…`, `coreHash cafcb692…`) unless something lands before launch, in
which case this entry says so.

The Core comment warns that at 1.0 a link is a photosynthetic cell that also moves, the
trade-off disappears and joints drift neutrally, "as uninformative as never affording one".
Half is the first rung of a ladder, not a chosen value: a jointed leaf earns from both
parts and pays for both, and is not yet a strictly better leaf.

## Rules, before scoring

**Verification.** V1: every header carries round 34's tokens and `linkPhoto 0.5`; every
manifest one build and `physicsJobWorkers 0`. V2: `audit` 0.0000% and `mat resid` 0 on
every row. V3: `diverged` read per arm against round 34's same seed (seed 1 read 3); a run
whose manifest reads `error` is censored.

**Predictions.** The reading is `jnt inh`, bodies alive whose parent had a joint, as a share
of `alive`, and the positions file's jointed count and depth against the rigid.

| # | prediction | falsified by |
|---|---|---|
| M0 | **the world stands**: `alive` at 30,000 s within 0.5 to 1.5 of round 34's same seed; D063 as amended read but not required | `alive`, the scorer |
| M1 | **the part was the reason**: `jnt inh` over `alive` at 30,000 s at least 0.1 in at least 3 of 5, and at least 10 jointed bodies at every sample of the last 6,000 s in those arms (round 34 seed 1 read 0) | `jnt inh`, `alive` |
| M2 | **founding is survived**, as in round 34: `jnt inh` at 5,000 s at least a quarter of its value at 1,000 s in at least 3 of 5 | `jnt inh` at 1,000 and 5,000 s |
| M3 | **no more divergence than round 34**: `diverged` per arm at most round 34's same seed plus 2 | the manifests |
| M4 | **depth is not bought**: the mean depth of jointed bodies within 3 m of the rigid at 5,000, 15,000 and 30,000 s in at least 3 of 5 (round 34 seed 1 read jointed bodies 2 to 7 m shallower); a shallower reading in 3 of 5 is a hint that the stroke or the extra part buys depth, and the ledger separates them | `positions-read.py`'s `joint y` and `rigid y` |

**Two reads that the intake column cannot give**, promised at 0080's first seed: R1, the
ledger calculator on a jointed genome from a round 34 snapshot against the same genome with
its joint removed, for the net watts and R0 of the hinge; R2, the birth rate per jointed
parent against per rigid parent from `lineage.jsonl`. Both are done for round 34's entry
and repeated here, so that "eats better" comes with the net beside it.

## The two-sided readings

- **M1 and M2 hold:** the part was the reason. A jointed population stands in a world with
  a vertical prize and free brains; rounds 37 and 38 (the box, the light sense) build on a
  standing line, and the price ladder (round 39) asks what it can bear.
- **M2 holds and M1 fails**, round 34's shape again: neither the price nor the part. The
  line thins by drift or by a cost not yet named; R1 and R2 say which, and the long arm's
  question is sharpened before it is run.
- **M1 holds and M2 fails:** the line is lost at founding and recovers from a late mutant, a
  different route to the same standing population; read the founders' draw against
  round 34's.
- **M4 fails, jointed bodies shallower in 3 of 5:** the first sign in the record that a hinge
  buys a place in the light, and round 38's light sense has something to work with; the
  buoyant share is read beside it, since gas is the cheaper way up.

## Launch

Five seeds on round 34's launcher with `EVOSIM_LINK_PHOTO` 0.5 (`rounds/launch-r36.ps1`),
`configHash 55a61291`, build `simHash b5d31a48`, `coreHash cafcb692`, the round 34 build. The
header of every arm was read for `linkPhoto 0.5`, `idle 0.0001 W/N·m`, `current 0.1 m/s
transport` and `space shared 4x5x5 m` before it was counted as launched.

| arm | launched | run | worker |
|---|---|---|---|
| `r36-s1` | 2026-09-11 07:18 | `2026-09-11-071830-55a61291` | 5 |
| `r36-s2` | 2026-09-11 07:20 | `2026-09-11-072004-55a61291` | 6 |
| `r36-s3` | 2026-09-11 11:25 | `2026-09-11-112526-55a61291` | 5 |
| `r36-s4` | 2026-09-11 18:08 | `2026-09-11-170833-55a61291` | 6 |
| `r36-s5` | 2026-09-11 19:23 | `2026-09-11-182321-55a61291/` | 5 |

All five launched on one build by `scripts/launch-queue.ps1`, two workers at a time, in
twelve hours. Seeds 1, 2 and 3 ended at budget the same day (12:06, 18:05 and 19:20, with
1,154, 1,624 and 1,342 alive); their reads wait for the set.

## What the round does not ask

Whether the stroke is used for anything, and whether a jointed leaf at 0.5 is simply a
better leaf. The first is round 38's; the second is the ladder's next rung, and a jointed
share that rises above the founder draw in every seed is read as the warning coming true
rather than as a swimmer.

## Sources

Logbook/0080 (and its Launch section), 0082, 0087; D032, D081, D082, D087, D088; DESIGN
§5A.10; `src/Evosim.Core/Cells/StandardCellTypes.cs` (`LinkCell`); `rounds/launch-r36.ps1`;
HANDOFF.md, the path as ruled 2026-09-11.
