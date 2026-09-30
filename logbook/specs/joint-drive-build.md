# The bend-and-power joint drive: build record (2026-09-30)

D135's build, on the branch `joint-drive` (worktree `scratch/wt-joint`, commits `5a6d912`, `887d570`, `59bcfd4`).
Every measurement below is one body alone in the still-water tank (100 m² by 5 m, no current, no food) from
`Evosim.Nursery --swim-test`, on configs brought forward from `scratch/nursery/runs/nursery-small-still` with the
four new settings added (`scratch/nursery/runs-jd/still-w*/config.json`). Speeds are the root's horizontal path
over 10 s legs, per body, then the median over bodies.

## What was built

- **Core.** `DriveGenes` (threshold, reaction, hold, release) on every node, read on a joint; genome format 11;
  `scripts/convert-format10-to-11.py` brought `inocula/` and the test fixtures forward at the defaults (0, 0.1 s,
  0.1 s, 0.1 s). `Brain.Step` fills a second output per degree of freedom, neuron n + d, the bend.
  `FluidConfig.PowerPushRadiansPerSecond` (`EVOSIM_POWER_PUSH`, Ω; 0 is the torque drive) and `PoseHertz`
  (`EVOSIM_POSE_HZ`). `RandomGenomeOptions.DrawDriveGenes` (`EVOSIM_DRIVE_GENES`) and
  `MutationRates.DriveGeneChance` (`EVOSIM_DRIVE_GENE_CHANCE`), both off by default, so no recorded founding
  lottery or birth draws anything new.
- **Dynamics.** `EffectorDrive`: each command read from [-1, 1] onto [0, 1]; the power off at or below the
  threshold and rescaled above it; the envelope (a command is taken only after the last has been held its time,
  and only when it differs by more than 0.01; the ramp is the reaction from rest and the release after); the bend
  held by a critically damped spring of `PoseHertz`, stiffness and damping scaled by the link's smallest inertia,
  its torque capped by Power. `Fluid.Apply`: the push from the power, and the push from a real swing turned off
  under the drive. The push's work is booked as drive work, so the work cost prices it. The envelope is written
  to a checkpoint only in a world that runs the drive.
- **Farm.** The four settings bound; the header prints `powerPush` and the drive genes' state; the graphics card
  refuses a world with the drive, as it refuses the limb push.
- **Tests.** Core 1,038 pass (every pinned number of a recorded world unmoved). `BendAndPowerTests` (10) and
  `LimbPushTests` (8) pass. The Dynamics suite's other 10 failures read the `r52fix-s4` crowd, whose config this
  build refuses; they fail the same way on the `nursery` branch, and the crowd needs re-recording on this build.

## Calibration of Ω (30 founders, 120 s, 2 seeds, the power read as its positive half)

| Ω (rad/s) | full power | moving over 1 cm/s |
|---|---|---|
| 1 | 2.0 cm/s | 24 of 30 |
| 1.5 | 4.1 cm/s | 28 of 30 |
| 2 | 6.2 cm/s | 30 of 30 |
| 3 | 10.2 cm/s | 30 of 30 |

Ω = 2 puts a founder at full power inside the owner's 4 to 8 cm/s. On 100 founders under the final reading, full
power is a median 7.8 cm/s, and the fastest 17.8 cm/s, about half a body length a second.

## Two faults found and fixed

**The push from a real swing was left on.** A brain with no power and a bend swept as a sine swam a median 1.2
cm/s at 1 Hz (16 of 30 over 1 cm/s, the fastest 9.6). Ruling 1 replaces that push, and with it off the same
brains swim 0.1 cm/s (drag's lopsidedness), and full power is unchanged.

**A random brain barely moved.** With the power read as the positive half of the neuron's output, 100 founders'
own brains had power on in 46% of steps at a mean of 0.05, and 16 moved over 1 cm/s. An instrument read each
brain's raw outputs against its thresholds and gave the thrust each candidate reading would give, as a share of
full power's (medians over 100 founders; the speeds are estimates as full power's times the share's square root,
and the reading in force estimated 0.7 cm/s where 0.0 was measured, so they run high):

| reading of the power | thrust share | estimated speed | over 1 cm/s |
|---|---|---|---|
| positive half, rescaled, push squared (built first) | 0.019 | 0.7 cm/s | 46% |
| positive half, gate only | 0.038 | 1.3 cm/s | 52% |
| positive half, push linear | 0.057 | 1.4 cm/s | 55% |
| positive half, gate only, push linear | 0.086 | 1.7 cm/s | 59% |
| [-1, 1] onto [0, 1], push squared (adopted) | 0.172 | 2.8 cm/s | 88% |
| [-1, 1] onto [0, 1], push linear | 0.352 | 4.1 cm/s | 93% |

Measured under the adopted reading: own brains a median 1.4 cm/s, 60 of 100 over 1 cm/s and 14 over 4, the
fastest 14.2; frozen 0.0; half power (a silent neuron) 0.8; the bend wagged alone 0.1. The adopted reading is
the bend's, and the owner's words ("an output from 0 to 1"); it was chosen by the agent inside the ruling and is
reported to the owner as such.

## The films

`scripts/render-swim-film.py` draws a `--film` trace (every link's pose, power, bend, pivot and hinge axis, 30 a
second): each link a box at its simulated pose, the stroke drawn from the logged power about the real hinge in the
owner's curve, the camera side-on to the travel over a fixed 10 cm grid. Founder 72 of the 100 (a leaf trunk and
a one-joint tail, like every founder the options build), 120 s traced and the 20 s in which it travels farthest
shown: under its own brain (96 to 116 s, 11 to 16 cm/s near the surface, its power pulsing between about 0.2 and
0.9) and at full power with the bend centred (35 to 55 s, about 20 cm/s, climbing from 3.6 m to 0.65 m because its
tail hangs down and the push runs along it). Checked on a five-frame sheet and a strip of six consecutive frames:
the tail wags between frames, the trunk leads (travel against trunk-to-tail, cos -0.99 and -0.91), the hinge axis
stands square to the limb. The videos are `scratch/owner/jd-swim-own.mp4` and `jd-swim-full.mp4`; the traces are
under `scratch/swimtest/jd4-film/`.

## What is open

- Every founder is two parts with one joint, so a chain's drawn wave (ruling 9) has not been seen.
- The work cost is 0 in these configs, so ruling 4's price is untested; the bench sets it before a round.
- The theatre does not draw the stroke yet, and the farm's pose stream does not log the power.
- The `r52fix-s4` crowd fixture needs re-recording on this build before a merge.
