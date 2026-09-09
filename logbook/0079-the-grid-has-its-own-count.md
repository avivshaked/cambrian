# 0079 — The grid has its own count

**2026-09-09**  ·  round 32, pre-registered before launch; the base round of the grid world

Round 31 (logbook/0077) was the vertex world's last word, and the water was rebuilt as a
grid the same night (logbook/0078, D086). A new representation gets its own count under
the goal rule before anything is asked of it, as the vertex world did in round 30. This is
that round. Nothing is under test but the representation itself.

## What is held

Round 31's launcher in every knob that survives the representation: D082's prices, the
contact world with the three senses on, added mass 0.5, the global brain retired,
single-threaded physics, detritus mixing 0.02, remineralisation off, 30,000 s, five seeds.

## What changes

`EVOSIM_FIELD grid`, detritus cells of 1 m and matter cells of 5 m (D086, from 0078's
screens), matter mixed at its own 2 m²/s on every axis where the vertex world walked it
sideways at 0.02, and corpses as particles decaying at 0.005 per second. Everything else
is the water's own physics on a different carrier. The launcher is `scratch/launch-r32.ps1`
at its defaults.

## Validity checks

- **V1 header.** Every token of round 31's, with `field grid`, `cell=1 mcell=5`,
  `corpse=0.005/s`, `mixing 0.02 m2/s`, `h-mix 0.02 m2/s`. Read from the report, never
  from the launch command.
- **V2 identities.** `audit` within a joule and `mat resid` 0 to the rounding on every row,
  `mat short` 0, corpses counted as standing.
- **V3 manifests.** `ended budget`, `physicsJobWorkers 0`, `gitDirty false`, one `simHash`
  and one `coreHash` across the five; `diverged` read per arm with the usual caveat.

## Predictions

The reference is round 31's same seed, the vertex world at the same mixing.

| # | prediction | falsified by |
|---|---|---|
| M0 | **the grid stands**: `alive` at 30,000 s within 0.5 to 1.5 of round 31's same seed in every arm, and D063 as amended holds in at least 3 of 5 | `alive`, the scorer |
| M1 | **the identities close**: `audit` 0.0000% and `mat resid` 0 at every sample of every arm; neither impulse limiter binds | `audit`, `mat resid`, the manifests |
| M2 | **the loop closes**: `det out` within 0.5 to 2 of `det exuded` per window over the last 6,000 s in every seed that passes M0 | `det out`, `det exuded` |
| M3 | **the matter economy is the vertex world's**: `mat locked` at the end within 10% of round 31's same seed in at least 4 of 5 (0078's screens: within 1% at 5 m) | `mat locked` |
| M4 | **corpses hold a small standing share**: `corpseJoules` under 10% of `detritus J` plus `corpseJoules` at the last sample in every arm, since a 139 s half-life is a twentieth of a lifetime | `stats.jsonl` |
| M5 | **the larder still goes deep**: `det deep` at the end above round 30's same seed in at least 4 of 5, as round 31's was, since sinking is the same physics on either carrier | `det deep` |
| M6 | **no swimmer, still**: `jnt inh` reads 0 at the last sample in at least 4 of 5, because no joint survives founding on any carrier so far and this round changes nothing a joint costs | `jnt inh` |

## The two-sided readings

- **M0 fails:** the grid does not carry the world the vertex field carried; read `mat blk`
  against births and `J/m3 here` per seed before anything else, since the cell walls are
  the one thing that is new.
- **M1 fails:** the round is censored and the fault fixed before anything is read.
- **M3 fails:** the 5 m cell does not do at 0.01 what it did at 0.02; the screens are
  re-read at the fine step.
- **M5 fails:** the grid's sinking differs from the vertex field's in a way 0078 did not
  measure, and the column experiment is rerun on both before the floor lever is touched.
- **M6 fails, a jointed guild persists:** the sharper hole did what the vertex hole could
  not, and the round is read for M1 to M3 of logbook/0077 before growth is built.

## What the round does not ask

Whether movement pays, whether a sense is used, whether the floor should give back. The
first two wait for growth (`fable-propose-growth.md`, round 33), the third for this round's
`det deep`.

## Launch

*2026-09-09, midday.* The owner ruled ("proceed"): matter cells of 5 m, matter mixed at
2 m²/s on every axis, corpses at 0.005 per second (D086). The first launch did not carry
the ruling. The launcher's defaults were edited by a script whose assertion failed on a
line ending, and the five arms went out at 2.5 m with corpses off; the headers said so
within a minute, which is what the rule about reading settings from the header and never
from the launch command is for. All five were stopped through the stop script, their
manifests reading `stopped manual-other` under the same arm names, and relaunched on the
corrected launcher. `r32-s1` to `r32-s5` on workers 2 to 6 at dt 0.01 for 30,000 s, wall
1,200 min, every header reading `field grid`, `cell=1 mcell=5`, `corpse=0.005/s`,
`mixing 0.02 m2/s`, `h-mix 0.02 m2/s` and `work x0.25`; every manifest `simHash b7f589d0…`,
`coreHash 9e1f47b2…`, `physicsJobWorkers 0`, `gitDirty false`, commit `ebac31f`. The
launcher refused any other `simHash`. The machine holds the five arms and nothing else.
Read as they land, against the predictions and the scorer.

