# The leaf smell on the card: a timing at two reaches

D174 puts the leaf smell into round 59's worlds in its direction form, at a reach of 6 m. It takes 3 m instead
only if a tank check shows that 3 m pays as well and 6 m costs the card's pace. This is the card's half of that choice. It times the leaf
field's fill and the whole step's pace at both reaches, interleaved, two runs each. It also answers the question
the build left open: whether a fill on the card would pay (`r59-anchor-build-plan.md`, "The leaf smell's field
stays filled on the host").

## What is timed

The world fills the leaf field on the host, once a metabolic step (`World.FillLeafScent`). Each living body with
lit photosynthetic tissue adds a cone that falls to zero at the reach. The card reads the filled grid, sent up
every block as the carrion grid is (read from code). The cone's cells grow as the cube of the reach over the cell,
so a 6 m reach fills about eight times the cells of a 3 m one (computed). The grid itself, and so the upload, is
the same at both.

Core's world probe (`WorldPhaseProbe`, built with `-p:WorldPhaseProbe=1`) prints the world step's phases every
20 s. Its "(scent fill)" phase holds the leaf fill and the carrion's hand-over to the card. The hand-over is small,
since the card fills the carrion field (read from code). That phase is the fill's cost. The pace is
`pace-calc.py`'s stepping pace, on a plain build.

## The worlds

The worlds are round 59's as D174 sets it (`rounds/env-r59l.ps1`, seed 1). Round 57's world gains the drag on
turning in its spin form, the touch of the rock and its sense, and the leaf smell in its direction form. It has
no anchoring cell. The smell's cell is 2 m and its half scale 1 m², the farm's defaults, as
`r59smoke-t16` ran them. If the pre-registration names others, this timing is taken again at those.

A resume cannot change a world's settings, so each reach has its own world. The CPU engine founds both to 1,000 s
off the card, on 4 threads pinned to logical 16-19, with a checkpoint there. The coordinator narrowed it from 8
threads on 16-23 on 2026-10-14 for the heat beside the tank's arm on 24-31, after the 6 m world had founded at 8. D168's window timed `r59smoke-t16`,
which held 3,495 bodies at 1,000 s and founded at 2.8 times real time on 16 threads (measured). So each founding
took 13 minutes on 8 (measured, 6 m), and about twice that on 4 (guessed). The card then resumes each world's checkpoint. A card run from a
CPU engine's checkpoint is a cousin of the recording (`card-run.ps1` passes `-AllowSourceMismatch`), and a timing
needs no identity.

## The steps

All three are `rounds/r59l-timing.ps1 -Commit <commit> -Step <step>`, run from this branch's worktree.

1. **found**, off the card: `r59l-found6` and `r59l-found3`, one after the other. Each run's `config.json` must
   name the direction form at its reach, and the two configHashes must differ, or the reach did not reach the
   world.
2. **check**, off the card: ILGPU's CPU accelerator with every card switch resumes `r59l-found6` at 1,000 s for
   2 s, on 4 threads pinned to logical 16-19. It meets the card's resume path with the leaf smell before the window
   does.
3. **window**, on the card, alone, in a quiet machine the coordinator holds. The card's settings are round 58's: group 32,
   single precision, the snow's transport, the scent's fill, the concurrent mode and the spin at 5,000. The farm
   runs 16 threads on logical 16-31. A minute's cooling before each run, and the heat stop throughout.
   - The pace: four 300 s runs from 1,000 s, at 6, 3, 3 and 6 m (`r59l-p6-1` to `r59l-p6-4`), the lengths of
     D168's window.
   - The fill: four 60 s runs of the world-probe build, in the same order (`r59l-w6-1` to `r59l-w6-4`). The probe
     build sits in a second worktree at the same commit (`scratch/wt-r59-leafprobe`), so the two builds never share
     an output folder. D168's probes ran 30 s, but this probe prints once in 20 s, and 60 s gives three lines a run.
   - The reads: `pace-calc.py` over the pace runs and `phase-calc.py` over each probe run.

Every run must end by its budget with its books closed, its manifest naming the clean commit, and on the card
`card-run-check.ps1`'s every condition and no overflow. A probe run's stderr must hold the probe's lines and a pace
run's none. Anything else stops the step.

## What the numbers decide

- **The reach**: if 6 m's pace is within the two runs' spread of 3 m's, the reach costs the card nothing worth a
  choice. D174's 6 m then stands on the tank check alone.
- **A fill on the card**: if the fill's phase is a few percent of the stepping wall at 6 m, it stays on the host.
  If it is a large share, a card fill like `GpuScent`'s is worth building before the round, and that goes to the
  coordinator with the number.

## The machine

| Step | Where | Time |
|---|---|---|
| found | logical 16-19, 4 threads, off the card | 13 minutes at 6 m on 8 threads (measured); about 25 at 3 m on 4 (guessed) |
| check | logical 16-19, 4 threads, off the card | about 10 to 20 minutes (guessed) |
| window | the card, 16 threads on logical 16-31 | about 25 to 30 minutes |

The window's minutes are computed from D168's. Its eight 300 s runs, two 30 s probes and the drift test took 32
minutes on 2026-10-13, at about 3.8 times real time. A 300 s run steps about 80 s, and its start-up, its checks and
the minute's cooling bring it to about 3.5 minutes. A 60 s probe run comes to about 2.5. Four of each and the reads
make about 25 minutes, and each run's wall limit is 20.

## What the window measured

The window ran on 2026-10-14 from 15:17 to 15:34, alone on the card in a quiet machine the coordinator held, at
`7105c799`. All eight runs ended by their budgets, and `card-run-check.ps1` passed every condition on each. The pace
runs printed no probe lines and the probe runs three each. The card peaked at 52 C and 6.6 GB of memory, the owner's
desktop's 4.3 GB included (measured).

| | 6 m | 3 m |
|---|---|---|
| Stepping pace, times real time, two runs (`pace-calc.py`) | 7.02, 7.14 | 7.41, 7.40 |
| Bodies over the pace runs | 3,439 to 3,584 | 3,534 to 3,602 |
| "(scent fill)", ms per simulated second (share of the stepping wall) | 5.6 (3.9%), 5.6 (3.8%) | 5.2 (3.7%), 4.2 (3.0%) |
| Physics, ms per simulated second | 92.8, 94.8 | 90.0, 91.1 |

- **The reach**: the 6 m world stepped about 4% slower than the 3 m one, against a spread of under 2% between
  its own two runs (measured). The fill accounts for under 1 ms of that, about 0.6% of the wall. Most of the rest
  is in the physics step, which reads no reach (measured). The two worlds are different realisations, so that part
  is likely their bodies rather than the smell (inference). D174 kept 6 m on the tank's reading the same day, and
  this does not argue against it.
- **A fill on the card**: the leaf fill and the carrion's hand-over together take about 4% of the stepping wall at
  6 m (measured). A card fill could win back at most that, so the field stays filled on the host.
- **How sure**: each probe run had one 20 s probe line inside its stats stretch, so each fill number is one sample
  of 40 metabolic steps. Both 6 m samples read 5.6 and the 3 m samples 4.2 and 5.2, so the fill's difference
  between the reaches is within its noise (measured).
