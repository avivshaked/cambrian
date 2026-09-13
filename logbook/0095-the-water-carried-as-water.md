# 0095 — The water carried as water

**2026-09-13, pre-registered before launch**  ·  round 37b: round 37's tank with the streams, the fluid acceleration force, the conservative transporter, the wall clearance and the throw trace; a replacement under D091, the tank's base

## The world

Round 37's world (logbook/0093: round 36's prices in a tank of 100 m², 60 m deep, a glass
wall, ring patches, a masked grid, dispersal 5 m, `linkPhoto 0.5`, the limiter off) with
five things changed in the water and nothing in the ecology, all of them corrections to what
the world is and none of them a treatment (D091):

1. **The streams** (D090): in a tank the current is a 27-term spectrum of horizontal eddies
   and small overturning cells, no swirl about the axis, tangential at the glass, phases and
   envelopes advancing at irrational rates from the seed; RMS 0.1 m/s as before.
2. **The fluid acceleration force** (D090): every part feels
   `c·(ρ_water·V + m_added)·Du/Dt`, `c` = 1 (`EVOSIM_FLUID_ACCEL`, header `fluidAccel 1`),
   the water's acceleration along its path, analytic in a tank. Without it a drag-only body
   on a curved streamline drifts outward and round 37 was a ring (0094).
3. **The conservative transporter** (the Astra review's F1, `logbook/specs/transport-conserves-spec.md`):
   the grid's face fluxes are the circulation of the current's vector potential round each
   face, all three axes from one stock, so uniform water stays uniform to rounding; round
   37's grid made 34% patchiness from uniform water in 600 s.
4. **The wall clearance** (F2, `wall-clearance-spec.md`): a body is placed only where its
   whole reserved sphere is inside the glass.
5. **The throw trace** (`throw-trace-spec.md`): joint mass ratios at every build and resize,
   a three-frame ring per multi-link body, a trace file beside every diverged dump; and
   `cols` counted on the columns whose centres are inside the circle, in the report and the
   reader.

Five seeds, 1 to 5, 30,000 s, dt 0.01, one build, launched through the queue with the
pre-registration committed (`-Prereg`). Round 37 is the control for every mechanism reading
below; the goal rule is read and not required (D091, a replacement).

## Rules, before scoring

V1: every header carries `space tank r=5.64 m (100 m2), depth 60, wall, bed`, `current 0.1
m/s transport`, `fluidAccel 1`, `dispersal=5 m`, `driveLimit >0.01`, `linkPhoto 0.5`,
`dt=0.01`, and every manifest one `simHash`, one `coreHash`, one `configHash`,
`physicsJobWorkers 0`, `prereg.json` naming this entry's commit. V2: `audit` 0.0000% and
`mat resid` 0 on every row. V3: `wraps` 0 on every row. V4: every diverged dump has its
trace file. V5: a manifest reading `error` is censored and read as such. The entry is not
written until the world has been watched in the theatre, and the agent looks at frames of a
live arm at about 3,000 and 6,000 s before the read (the owner's rule of 2026-09-12).

## Predictions

Round 37's numbers at 30,000 s: `alive` 682 / 1,557 / 925 / 1,660 / 1,831 by seed; the rim
quarter 0.45 to 0.98 at the named times; corrected `cols` 45 to 90; `x sd` 2.6 to 3.9 m;
nearest neighbour 0.33 to 0.67 of round 36's at matched abundance; `jnt inh` 0 / 3 / 148 /
0 / 0; `mean m/s` 0.68 to 0.71 of round 36's; `diverged` 0 in every seed; the population's
peak-to-trough ratio over t ≥ 5,000 s 1.9 to 5.4.

| # | prediction | falsified by |
|---|---|---|
| M0 | **the world stands**: `alive` at 30,000 s within 0.5 to 1.5 of round 37's same seed; D063 as amended read but not required | `alive`, the scorer |
| M1 | **the water does not gather**: the rim ring `p3` holds between 15% and 40% of `alive` at 5,000, 15,000 and 30,000 s in at least 3 of 5; and the mean drift of a body's radius over 100 s, by starting radius over the whole run, lies within ±0.5 m in every bin inside 4 m in at least 3 of 5 (round 37: +1.1 to +3.5 m) | `p3` over `alive`; `dispersal.py`'s drift table |
| M2 | **the middle is filled**: corrected `cols` at least 60 of the live columns at 5,000 s onward in at least 3 of 5, and `x sd` between 2.0 and 3.4 m at the three times in at least 3 of 5 (a spread disc reads 2.82 m) | the reader's `cols`, `x sd` |
| M3 | **the crowd loosens to the box's**: the three-dimensional nearest-neighbour median at matched abundance within 0.8 to 1.25 of round 36's same seed in at least 3 of the anchors that find a match (round 37: 0.33 to 0.67 at every anchor) | `nn_matched.py` |
| M4 | **the joint's fate is the world's**: the number of seeds holding at least 10 inherited jointed bodies at 30,000 s within one of round 37's one | `jnt inh` |
| M5 | **the water carries**: `mean m/s` within 0.7 to 1.3 of round 36's same seed at 5,000 and 30,000 s in at least 3 of 5 (round 37 read 0.68 to 0.71, the crust's own slow water) | `mean m/s` |
| M6 | **the tank still throws nothing**: `diverged` 0 in at least 4 of 5 seeds, and every dump that exists carries a trace file | the manifests, `diverged/` |
| M7 | **the cycle damps**: the peak-to-trough ratio of `alive` over t ≥ 5,000 s below 2.5 in at least 3 of 5 (round 37: 1.9, 3.3, 5.4, 1.9, 3.2) | `cycle.tsv`'s method |
| M8 | **the pace holds**: wall-clock seconds per 1,000 simulated seconds, from the manifests, within 1.5 of round 37's same seed (the force costs a stencil the analytic derivative and the single substep were meant to pay back) | `run.json` timestamps |

## The two-sided readings

- **M0 to M8 hold:** the water was the tear. Round 37b is the tank's base; the fresh-seed
  batch (seeds 6 to 10, D091) runs on it, round 38 dilutes it and reads against it.
- **M1 fails, the rim above 40% with the drift still outward:** the force at `c` = 1 is not
  enough for the bodies the world grows, which means their drag response time is longer
  than the tracer check's 2 s. Read `τ` from the positions file (a body's speed against
  the field's over 100 s is not a velocity, so read the drift's magnitude by size class from
  the snapshots' adult scale) and put `c` above 1 to the owner as a rule, or the body's
  drag as the fault. The streams are not re-tuned first: D090's rejection stands.
- **M1 fails, the rim below 15%:** the force over-corrects and the streams' inward return
  is now what a body follows; the tracer check's band was 0.20 to 0.30 and a world below
  0.15 is the force's, read `c` against the tracer at the world's τ.
- **M1 holds and M2 fails on `x sd`:** the bodies are spread in radius and clumped in
  angle, which is the streams' eddies holding a crowd: read the radial histogram beside the
  angular one before anything is called a fault.
- **M3 fails with M1 holding:** the tank's crowding is not the ring's; depth is the other
  suspect (0094's caveat), read at matched depth.
- **M6 fails:** a tank throws, so the wrap was not the whole mechanism; the trace files are
  read before anything is proposed, which is what the trace was built for.
- **M7 fails with M1 holding:** the cycle is not the crust's; a producer canopy that shades
  and starves the eaters under it is the next candidate, read from `shade %` and the
  eaters' depth against the producers' over one cycle.
- **M8 fails:** the pace is the force's cost after all; read the per-step profile from the
  log before the analytic route is blamed.

## What the round does not ask

Not the dilution (38), the bed with shape (39), the light sense (40), the stroke's price
(41) or the fluid model's lift (the path's 6b). Not which of the five changes did what: a
replacement is read as a baseline (D091), and a one-part control is cheap if a later
question needs it. Not the pockets the owner asked for on 2026-09-12: the field patchiness
reading queued for this round's read is the instrument for that, not a rule.

## Launch

The `streams` branch (the transporter, the clearance, the reader's `cols`, the streams, the
force, the trace) merged into main as `867408f` after the full suite ran green on its
checkout (687 tests, 19 minutes); the arms run on main's checkout, whose `simHash` is
`5e164d01...` (the branch checkout's `13a906a3...` differs by line endings on 39 files and
nothing else, the CLAUDE.md gotcha), `coreHash ad5c952a...`, `configHash 2430e660`,
`physicsJobWorkers 0`. Seed 1 launched at 23:26 on 2026-09-12 on worker 5, its header
verified (`space tank r=5.64 m (100 m2), depth 60, wall, bed`, `fluidAccel 1`,
`dispersal=5 m`, `driveLimit >0.01`, `linkPhoto 0.5`, `addedMass 0.5`, `dt=0.01`, seed 1).
Seeds 2 to 5 are queued behind round 37's last four renders, which hold workers 2, 3, 4 and
7 under the cap of five editors: the queue (`launch-queue.ps1 -Refresh`, added tonight)
refreshes each worker as a render releases it and launches the next seed on it, worker 6
first, so the seeds start hours apart on one build; the render queue for the five arms
follows the last launch. The queue's first pass could not read the arm name from the
launcher (it reports with `Write-Host`, which the capture missed), so seed 1's
`prereg.json` was written by hand from the queue's log with a note saying so, and the
capture was fixed for the rest. The chain's log is `scratch/logs/r37b-chain.out`.

*01:15.* Round 37's renders of seeds 2, 4 and 5 were still at their 5,000 s frames after
two and a half hours and would have held three workers until morning, so the agent stopped
them (every seed of round 37 has its 5,000 s frames, seed 1 its 15,000 and 30,000, and 0094
is written) and kept seed 3's, the jointed seed, on worker 2. A killed Editor leaves its
lock file and the queue read the three workers as busy until the agent removed them (the
queue now removes a lock no process holds). Seeds 2, 3 and 4 launched at 01:14 to 01:15 on
workers 3, 4 and 7, each refreshed first, headers and hashes as seed 1's, `prereg.json`
written by the queue this time. *02:17.* Seed 3's render was still at its 15,000 s frames
after five and a half hours, so the agent stopped it too and seed 5 launched on worker 2
at 02:17, header and hash as the others'. The five seeds start within three hours of one
another on one build (`simHash 5e164d01`, `coreHash ad5c952a`, `configHash 2430e660` in
every manifest).

*Seed 1 looked at, per the owner's rule.* At 3,000 s (290 alive) the disc is filled across
all four rings with no crust, the rim quarter 20% of the living; at 6,000 s (1,101 alive)
the same, eaters mixed through the producers, and the side view shows bodies spread through
the top 50 m where round 37 kept a 3 m film. Frames in `scratch/snaps/r37b-s1/`; the
6,000 s frame needed a 150-minute wall clock (the 30-minute default timed out on the live
run).
