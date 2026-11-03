# The card identity sitting for D294's hunger and D299's breeding urge

*2026-11-03, the coordinator. From `scratch/wt-forager` at `30258f19` (branch `forager-kit`: D292 to D294 and D299,
with the card copies of D294 and D299 in `f1948bfb`), its Release farm built from the same tree. The branch's
`rounds/identity-kit.ps1` ran three card groups one after another. Each ran on logical 20 to 29 at 10 threads, with
the transport and the heat stop. g32 ran from 20:51 to 21:00, g64 from 21:04 to 21:34, and the resume from 21:35 to
22:03. Every group ended at its budget. The runs are under `scratch/card-kit/identity/`.*

## The verdict: it passes

| Check | Result |
|---|---|
| C, every switch off, against `runs/r65-c-<seed>`'s first 1,200 s | identical on both seeds: stats on 120 samples, digest on 1,201 rows to step 120,000, lineage byte-equal (3,114 and 2,662 lines); `gpu-accept.py` exit 0 (`accept-off-*.txt`) |
| HU, both switches on, at group 32 against 64, to 1,200 s | identical on the same three records, lineage 2,444 and 2,331 lines; exit 0 (`accept-on-*.txt`) |
| HU resumed from its 1,200 s checkpoint at group 32, against group 64's run to 6,000 s | equal on every field of 480 rows, 99,360 values a seed, but the two counters that count from the process's start (`harnessBodySteps`, `fluidLinkSteps`); lineage rows equal, 21,378 and 19,380 (`resume-*.txt`, `scripts/reads/kit-reads/resume-fields.py`) |
| the switches change the world | yes: C and HU part at step 200 and at the lineage's line 16, on both seeds; exit 1 (`differ-*.txt`) |
| overflows, refusals, divergence | none in any run |

C carries a new config hash, `2ba8bbfe` where round 65's control was `2991f19e`, because the build adds the kit's
tunables. The world it steps is the same, row for row. So the card path with the kit's switches off is the one round
65 ran, and with it D280's and D283's off paths on the card.

## What the sitting exercised

Both copies are read from the first second. `scripts/reads/kit-reads/readers.py` counts the genomes whose brain links
from the root's hunger port or its gate port. It computes the port ids as `CentralBrain.cs` does, and the energy port
it computes being found as well is the check on that arithmetic.

| World | Genomes | Read energy | Read hunger | Read the gate sense |
|---|---|---|---|---|
| HU, 6202, to 1,200 s | 2,039 | 2,022 | 2,036 | 2,003 |
| HU, 6203, to 1,200 s | 2,087 | 2,080 | 2,084 | 2,079 |
| C, 6202, to 1,200 s | 2,500 | 682 | 0 | 0 |

The founders of this world are wired all to all from the pool (D146, D147). With the switches on the pool holds both
senses, so nearly every body reads them (inference for the cause; the counts are measured).

The urge never held a birth back. `urgeHeldBack` is 0 in every stats row of all four HU worlds, to 6,000 s. The urge
starts high (`BreedingUrgeRule.FreshBias`) and stayed above its threshold, so the gate on births was never closed here.
The urge's value reaching the host after each block is covered by `GpuHungerUrgeTests` on the CPU accelerator, bit for
bit at `on` and at `knockout`. Whether evolution lowers the urge far enough to close the gate is for the breeding
screen to show (`logbook/specs/breeding-urge/spec.md`, section 7).

## Pace

With four worlds in the group, C's first 1,200 s took 6.7 minutes, 3.0 times real time, as did HU's. HU's two worlds at
group 64 ran 6,000 s in 29.9 minutes, 3.3 times real time. This is not a timing read, since c8's Unity render ran beside
the sitting on logical 16 to 19 from 20:53. The bearing's cost was timed alone (`logbook/specs/forager-kit/bearing-timing.md`).
