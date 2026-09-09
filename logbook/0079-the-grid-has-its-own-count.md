# 0079 — The grid has its own count

**2026-09-09**  ·  round 32, pre-registered before launch; the base round of the grid world; read the same evening: the mechanism holds in every arm and the goal rule in two seeds of five

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

## Results

*2026-09-09, evening.* All five ended on budget at 30,000 s in 10 to 11 hours of wall
clock, one build (`simHash b7f589d0…`, `coreHash 9e1f47b2…`), `physicsJobWorkers 0`,
`gitDirty false`, no divergence in any arm, neither impulse limiter bound, `audit` 0.0000%
and `mat resid` 0 on every row. V1 to V3 hold without a caveat. Read against round 31's
same seed with `scratch/r32-read.py` (seed 3's control is `r31-s3b`, the rerun that
finished), and scored by connected clade.

| arm | alive (r31) | scorer | stomach clade at end (r31 `inherit`) | `det out` / `det exuded`, last 6,000 s | `mat locked` / r31 | corpse share of the larder | `det deep` (r30) | `mat blk` last window (r31) |
|---|---|---|---|---|---|---|---|---|
| `r32-s1` | 1,923 (1,757) | fail, 9 alive, sterile | 9 (20) | 0.76 | 1.004 | 0.02% | 3.61 (0.72) | 156k (248k) |
| `r32-s2` | 1,932 (1,824) | fail, line lost by 5,000 s | 3 (132) | 0.97 | 0.988 | 0.01% | 16.65 (0.43) | 91k (n/r) |
| `r32-s3` | 1,925 (1,768) | pass, 24 | 24 (44) | 0.73 | 1.006 | 0.03% | 2.24 (0.29) | 135k (200k) |
| `r32-s4` | 1,928 (1,706) | pass, 77 | 82 (134) | 0.89 | 1.014 | 0.05% | 2.96 (2.92) | 28k (128k) |
| `r32-s5` | 1,932 (1,772) | fail, 15 alive, sterile | 16 (53) | 0.98 | 1.003 | 0.03% | 3.26 (0.63) | 121k (n/r) |

The mechanism predictions all hold, in every arm. **M1** the identities close and nothing
binds. **M2** the loop closes, the larder returning 0.73 to 0.98 of what the producers
exude. **M3** the matter locked in bodies is within 1.4% of the vertex world's in every seed,
0078's screen at 5 m reproduced at the fine step. **M4** corpses hold 0.01 to 0.05% of the
standing larder, three hundred to five hundred of them at a time, sinking as they decay.
**M5** the larder goes deep in every seed. **M6** no swimmer: the last inherited joint is
gone by about 1,500 s in every arm, as in every priced world.

**M0 fails on its second clause.** The population stands, 6 to 13% above round 31's same
seed in every arm, so the grid carries at least the leaves the vertex field carried. The
goal rule holds in two seeds of five, against four in round 31 and five in round 30. The
inherited stomach lines are thinner from the first sample and thin all the way down:

| t | s1 (r31) | s2 (r31) | s3 (r31) | s4 (r31) | s5 (r31) |
|---|---|---|---|---|---|
| 5,000 | 116 (19) | 0 (82) | 134 (91) | 112 (180) | 78 (182) |
| 15,000 | 29 (18) | 0 (313) | 80 (148) | 161 (156) | 48 (86) |
| 30,000 | 9 (20) | 3 (132) | 24 (44) | 82 (134) | 16 (53) |

Seed 2 lost its line before 5,000 s and never founded another until a clade of four at
25,000 s. Seeds 1 and 5 held a line through the last two lifetimes and it was sterile at
the end: nine and fifteen bodies with no inherited birth in the last 20 samples. Seeds 3 and
4 pass with lines of 24 and 77, both lower than round 31's 44 and 134 in the same seeds and
both falling through the second half. Round 31's lines fell and recovered; round 32's only
fall.

What the pre-registered reading asked to look at first, `mat blk` against births and
`J/m3 here`, does not show the cell walls refusing the eaters. The grid refuses fewer
conceptions per window than the vertex field did in every seed with a control, 28k to
156k against 128k to 248k, on 4% fewer births. And the water at a stomach is no emptier:
`J/m3 here` over the last 6,000 s reads 1.0 to 2.9 J/m³ on the grid against 0.9 to 3.6 on
the vertices, and `food rig`, the rigid eater's take, reads the same or higher, 12 to 26
against 4 to 39. The eaters that remain eat as well as they did. There are fewer of them,
and their lines do not recruit.

Two columns are new to the record and neither explains it yet. The corpse count sits at
three to five hundred bodies in every arm from 10,000 s, a standing store of dead eaters and
leaves that used to deposit at once and now sinks for two minutes first; where it lands is
the deep larder, which reads flatter and steadier on the grid (2.2 to 3.6 in four seeds,
against round 31's swings between 0.9 and 38) and one seed's is very deep, seed 2 at 16.7
with 658 J in the refuge. And `stillb` reads 1 to 8 in three arms where every round before
read 0; `mat orphan` still reads 0 to the table's precision, so the matter is accounted for,
but a stillbirth is a conception the world booked and then could not place, and there were
none on the vertex field.

## Verdict

The grid does what it was built to do and the world is worse for eaters in it. Every
mechanism prediction holds: the books close, the loop closes, the matter economy is the
vertex world's to 1.4%, corpses are a small standing share, the larder goes deep, no joint
survives. The population is higher in every seed. And the goal rule, four of five on the
vertex water, is two of five on the grid, with every inherited stomach line thinner from
its founding and falling to the end. The first reading in 0079's list, that the cell walls
refuse the eaters' conceptions, is not what the columns say: the grid refuses fewer
conceptions than the vertex field did, and the surviving eaters eat as well or better. The
lines thin because they do not recruit, and the round's columns cannot say why. The
candidates are the two things that changed: a mouth that drains one cell rather than a
metre of water, so that a stomach beside a producer no longer shares the producer's
exudate with the water around it, and corpses that carry what a dead body held two
minutes downstream and down before it feeds anyone. Both are readable, the first with a
per-guild feeding trace and the second by running one seed with corpses at 0, and neither
is run before round 33 reads, because round 33 is this world with growth in it and its
eaters are born a third of their size.

What the round does not say: that the grid is the wrong water. The vertex field's four
passes were read in 0077 as the pairs feeding on a hole in the water at large, which is
the geometry the grid was built to remove. A world that holds fewer eaters honestly is
not a worse instrument than one that held more by an accident of where the hole fell.
It is a harder world, and the question the campaign asks of it is unchanged.

