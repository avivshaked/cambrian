# 0093 — The water gets a wall

**2026-09-12**  ·  round 37 pre-registered: round 36's world in a cylinder of the same area with a glass wall, ring patches and a gyre; the first round in the tank, and the first whose report cannot print a wrap

The owner watched round 36 seed 1 in the theatre and saw bodies jump (logbook/0091). It was
the wrap. The box was periodic on both horizontal axes, chosen in D077 when nothing crossed a
seam, and since D088's carrying current every body crossed one about once every hundred
seconds; the table's `wraps` column had counted it for two rounds. The owner asked for a
literal wall of glass and then for a round world. D089 records the ruling: a cylinder of
the same 100 m², a wall where the seam was, a gyre for the current, and the grid masked to
the circle. This round asks whether the world that stood in the box stands in the tank. It
reads the three things a wall can do to a crowd: gather it at the glass, leave the middle
empty, or change nothing.

## The world

Round 36's launcher with the shape set to the tank and nothing else moved. The free joint's
prices stay at zero, with the idle charge at 0.0001 W per newton-metre. The link earns light
at half a leaf's rate, dispersal is 5 m, the current 0.1 m/s RMS, the cells 1 m for detritus
and 5 m for matter, the seeds the same. The footprint is a disc of radius 5.64 m. The four
patches are rings of equal area from the axis out to the rim, so `p0` is the centre and `p3`
the rim. The grid carries 6,000 live cells at 1 m. The current is the gyre of D089's second
clause, its RMS at the same knob. The drive limiter is not on: its check failed (D089), and the header
reads `driveLimit >0.01`. Five seeds, dt 0.01, 30,000 s, `rounds/launch-r37.ps1` defaults.
The build is the tank merge's, and the Launch section carries its hashes.

Per-step change, so every seed is a new realisation: the water is a different field and
the cells sit in different places. The round is read across five seeds against round 36's
distribution, never seed against seed.

## Rules, before scoring

**Verification.** V1: every header carries round 36's tokens (`linkPhoto 0.5`, `idle 0.0001`,
`work x0`, `neuron 0 W + 0 W/input`), `space tank r=5.64 m (100 m2), depth 60, wall, bed`,
`driveLimit >0.01` and `matterBudget 0`; every manifest one build and `physicsJobWorkers 0`.
V2: `audit` 0.0000% and `mat resid` 0 on every row. V3: `wraps` 0 on every row, by
construction; a nonzero is a bug and the round is stopped. V4: no diverged dump whose
reason is the radius guard (a root beyond the glass); one such dump is read before anything
else, since it means a body went through the wall. V5: `diverged` per arm read against
round 36's same seed; a manifest reading `error` is censored.

**Predictions.** The readings are the table's `alive`, `cols`, `x sd`, `p0` to `p3`, `jnt inh`,
`mean m/s` and `diverged`, and the positions file's nearest-neighbour median and radial
histogram. Round 36's numbers at 30,000 s: `alive` 1,154 / 1,624 / 1,342 / 1,329 / 1,650 by
seed; `cols` 100 of 100 from 5,000 s in every seed; `jnt inh` 73 / 4 / 0 / 0 / 0; `mean m/s`
0.081 to 0.085.

| # | prediction | falsified by |
|---|---|---|
| M0 | **the world stands**: `alive` at 30,000 s within 0.5 to 1.5 of round 36's same seed; D063 as amended read but not required (round 36 read 5 of 5) | `alive`, the scorer |
| M1 | **the glass does not gather**: the rim ring `p3`, a quarter of the area, holds between 15% and 40% of `alive` at 5,000, 15,000 and 30,000 s in at least 3 of 5 | `p3` over `alive` |
| M2 | **the middle is not empty**: `cols` at least 60 of the live columns at 5,000 s onward in at least 3 of 5, and `x sd`, now a plain deviation, between 2.0 and 3.4 m (a uniform disc reads R/2 = 2.82 m) | `cols`, `x sd` |
| M3 | **the crowd is round 36's**: the median nearest neighbour within 0.8 to 1.25 of round 36's same seed at 5,000 and 30,000 s in at least 3 of 5; the same area holds the same matter, so the container alone changes no density | `positions-read.py` |
| M4 | **the joint's fate is the world's, not the container's**: the number of seeds holding at least 10 inherited jointed bodies at 30,000 s within one of round 36's (one of five) | `jnt inh` |
| M5 | **the gyre carries as the box's field did**: `mean m/s` within 0.7 to 1.3 of round 36's same seed at 5,000 and 30,000 s | `mean m/s` |
| M6 | **no more divergence than the box**: `diverged` per arm at most twice round 36's same seed plus 2 | the manifests |

## The two-sided readings

- **M0 to M6 hold:** the container was the tear and nothing else. Round 38 dilutes this world
  and is read against it.
- **M1 fails, the rim above 40%:** the glass gathers. Two candidate causes, read apart. The
  dispersal disc is clipped at the wall, so a parent at the rim has children drawn inward
  only, and a pile at the glass is not births. Or the gyre's eddies deliver bodies to the
  wall faster than the swirl moves them along it; the positions file's radial velocity of
  bodies against the field's says so. The theatre says whether bodies rest against the
  glass, and the wall's contact count in the smoke is the baseline.
- **M1 fails, the rim below 15%:** the wall repels, which only the clipped disc can do; the
  dispersal rule is re-asked at the glass.
- **M2 fails:** the gyre has pockets the field test did not show (0.004 of live cells below a
  tenth of the RMS, D089), or bodies collect in its downwelling; read `cols` against the
  positions file's radial histogram before blaming either.
- **M3 fails:** the container alone moved the density, and round 38 is read against round 37
  rather than round 36.
- **M6 fails:** the wall throws bodies. The radius guard's dumps say whether the glass or the
  solver did it, and a wall that throws is stopped before round 38.

## What the round does not ask

Not the dilution (round 38), the bed with shape (39), the light sense (40) or the stroke's
price (41). Not the limiter, whose check failed and which no round runs. Not the two-by-two
box, which D089 superseded before it ran. Not the glass as a picture: the theatre draws the
tank's outline and rings, and a visible wall is a skin item.

## Launch

All five seeds launched on 2026-09-12 between 08:28 and 08:31 through the launch queue, on the
merged main tree after the full Core suite passed (666 of 666 in 18 minutes) and every worker
was refreshed and its sources checked against the tree. One build: `simHash c50c465b…`,
`coreHash e6797e6e…`, `configHash 96d4bce6`, `physicsJobWorkers 0`. Every header reads
`space tank r=5.64 m (100 m2), depth 60, wall, bed`, `driveLimit >0.01`, `matterBudget 0`,
`linkPhoto 0.5`, dt 0.01 and round 36's prices.

| seed | run | worker | launched |
|---|---|---|---|
| 1 | `2026-09-12-072848-96d4bce6` | 2 | 08:28 |
| 2 | `2026-09-12-072921-96d4bce6` | 3 | 08:29 |
| 3 | `2026-09-12-072956-96d4bce6` | 4 | 08:29 |
| 4 | `2026-09-12-073031-96d4bce6` | 5 | 08:30 |
| 5 | `2026-09-12-073107-96d4bce6` | 6 | 08:31 |

The render queue (`scripts/render-queue.ps1`, 5,000, 15,000 and 30,000 s, cap 4) was started
with the launch queue and takes each arm as it ends. The run directories' names carry the
local clock an hour behind the launch log's.

## Sources

- `fable-propose-aquarium.md` as absorbed into D089; `logbook/specs/tank-spec.md`.
- logbook/0089 (round 36 pre-registered), 0092 (round 36 read), 0091 (what the theatre showed).
- `rounds/launch-r37.ps1`; `scratch/snaps/r37tank2/` for the smoke's pictures.

*Note, 2026-09-12 evening (from the Astra review).* This entry's predictions were committed
at 08:31:49, after the first manifest (08:28:48) and the fifth (08:31:07). The thresholds
were in the working tree before the queue started and did not change after it; the record
calls that delayed archival. From round 37b the launcher refuses to start without a
committed entry (`launch-queue.ps1 -Prereg`).
