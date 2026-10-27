# Integration branch 2's card identity sitting

*2026-10-27, the speed agent, on the coordinator's card go, with assay-d6 told and b0 holding its compile. From
`scratch/wt-int2` at `8f1b82b8` (branch `integration-2`: D231's stroke on the whole assembly and D234's twin drive,
main merged in at `77e18b8f`), with its Release farm built from the clean tree. `scratch/int2-hold/sitting.ps1` ran three card
groups one after another, each on logical 22 to 31 at 10 threads with the transport and the heat stop, from 23:12:55 to
23:30:22. Every group ended at its budget. The runs are under `scratch/int2-hold/identity-off/` and
`scratch/int2-hold/identity-on/`.*

## The verdict: it passes

| Check | Result |
|---|---|
| `r62i2off-6202`, `-6203`, `-6204` against `runs/r62cf-<seed>`'s first 600 s | identical: stats on 60 samples, digest on 601 rows, lineage byte-equal; `scripts/reads/gpu-accept.py` exit 0 for all three (`accept-off.txt`) |
| `r62i2on-fin`, `-asm`, `-twin`, `-all` at group 32 against 64 | identical on the same three records; exit 0 for all four (`accept-on.txt`) |
| each switch changes its world | yes, for all four pairs below (`differ.txt`) |
| overflows, refusals, divergence | none in any run |

The switch-off worlds carry a new config hash, `ecbba999` where r62cf's was `bcc3af98`, because the build adds D231's
tunable. The world they step is the same, row for row.

**Each switch reached the world.** Identical numbers across a switch would mean the switch was never applied, so each
was compared with the world it changes, at group 32:

| Pair | First difference |
|---|---|
| switches off against the fin law | step 200 |
| the fin law against D231 on it | 390 s (step 39,000) |
| switches off against the twin drive | 340 s (step 34,000) |
| D231 against all three | 401 s (step 40,100) |

D231 and the twin drive first change the world a few hundred seconds in. That fits a switch that acts only on a body
with a part welded beyond a joint, or a mirrored jointed pair, once such a body drives (inference).

## Pace

With every switch off, the first 600 s took 152.7, 150.0 and 157.5 s of wall. On the build before, r62cf's own first
600 s took 156.5, 150.6 and 162.7 s. So the switch-off pace is r62cf's, 0.4 to 3% faster, which is within the
noise. Neither timing was taken on a quiet machine: b0 rendered beside the sitting, as the theatre's Unity sat beside
r62cf.

Inside part 2's groups, D231 costs nothing measurable over the fin law alone: 3.19 min for both at group 32, and
3.10 against 3.06 min at 64. Part 2's pace of 3.1 to 3.3 times real time cannot be compared with part 1's 3.8 to 4.0,
because its groups hold four worlds where part 1's held three.

## The build

Farm `7d0b0b32`, core `be6dc1b7` and dynamics `e6c5f372`, the hashes of `d11b9bc0`. `8f1b82b8` changed only
`RunFixture.cs` and the launchers. The crowd fixture is `runs/cefix-s4/2026-10-27-220008-7bc16123`, equal to
`b2f9267b`'s on every stats and lineage row and on the 20,000 s snapshot. The host suites passed on it:

| Suite | Result |
|---|---|
| Core, slow tests included | 1,620 |
| Dynamics | 403, with 1 skip |
| Farm, without `GpuMeanTests` | 412 |
| CPU accelerator | 32 |

`GpuMeanTests` crashes the CPU accelerator at every core count. The same crash happens at `df2bf3b8`, so integration-2
did not cause it.

## Sources

| Claim | Source |
|---|---|
| the three groups' times and exits | `scratch/int2-hold/sitting.log` |
| identity, overflows, pace per run | `accept-off.txt`, `accept-on.txt` |
| each switch changes its world | `differ.txt` |
| r62cf's first 600 s | `wallTotalMs` at t = 600 in each `runs/r62cf-<seed>/2026-10-27-180610-bcc3af98/stats.jsonl` |
| the launchers | `rounds/identity-int2-off.ps1`, `rounds/identity-int2-on.ps1` and `rounds/env-int2-*.ps1` on `integration-2` |
| when each switch first bites | inference |
