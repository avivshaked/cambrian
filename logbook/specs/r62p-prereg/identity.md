# The card identity sitting for the pace build

*2026-10-26, the speed agent, for the coordinator. The pace build carries D239's pace factor (`logbook/specs/pace-build.md`).
The sitting ran on the coordinator's card go, from the clean tree of branch `pace` at `ac2d7bac` in `scratch/wt-pace`,
with its Release farm built from that commit. Each group founded seed 6201 four times for 600 s on the RTX 4090, in
single precision with the card's transport, at 14 threads on logical 16 to 29, with the heat stop on. Every world ended
at its budget. The comparisons are in `identity/`, beside this file.*

## Before the card

On the host, the crowd fixture re-recorded on the build at the pace factor 1 (`runs/cefix-s4/2026-10-26-013311-c4235450`)
equals integration branch 1's (`2026-10-25-174030-d1ead9fe`) in all 340,000 stats values over 20,000 s, and in every
lineage row apart from the excluded columns. The suites passed: Core 1,515; Dynamics 345 with one skip; the Farm 440
without `GpuMeanTests`, which crashes the CPU accelerator when pinned (`src/Evosim.Farm.Gpu/CLAUDE.md`), after one fix;
Nursery 463 with one skip. The fix: at a pace factor above 1 the host stamped a throw-trace frame in physics seconds
and the card in world seconds, which the CPU-accelerator test at k = 3 found. Both stamp world seconds now, which is
the physics step itself at 1.

The pace build writes the two mouth-blow columns whenever contact eating is on, where integration branch 1 wrote them
only with the bite damage off 1. So every comparison against integration branch 1 is run with
`gpu-accept.py --new-columns mouthBlows,mouthBlowsFinishing`, which leaves a column out where only one run holds it.

## Part 1: the pace factor at 1, at group 32

`rounds/identity-pace-off.ps1` runs integration branch 1's switch-off group (the bite dial at 1, and the 2x, 4x and 16x
arms). Each world is compared with `scratch/fin-shape/identity-int1-off/r62i1off-<world>-g32`, the same world on
integration branch 1's build.

`gpu-accept.py` exits 0 for all four. Stats are identical on 60 samples, the digest on 601 rows to step 60,000, and
lineage is byte-equal (1,570 lines and three of 1,582). So at the pace factor 1, the pace build is integration branch
1's build on the card, bit for bit, with D233's shape family and D236's curriculum on main between them changing
nothing in these worlds.

| World | Wall to 600 s, integration 1 | This build |
|---|---|---|
| Dial at 1 | 3.392 min | 3.307 min |

All four worlds of this build ran in 3.3 minutes, 3.01 to 3.02 times real time, against integration 1's 2.95. So the
pace factor at 1 costs nothing measurable, on one reading (measured).

## Parts 2 and 3: the first group's worlds, at group 32 and then 64

`rounds/identity-pace-on.ps1` runs the pace screen's first group's three worlds beside the kill-meal screen's, so that
one card process holds worlds at the pace factors 1 and 3, as the group will:
- `kg`, the kill-meal screen's world at the pace factor 1 (`rounds/env-r62kg.ps1`);
- `t3`, the thrust arm at 1 (`rounds/env-r62p-t3.ps1`);
- `k3` and `k3n`, the pace factor at 3 with the newborn reserve at 0.5 and 0.2 (`rounds/env-r62p-k3.ps1`, `-k3n.ps1`).

| World | Config hash | Pace factor | 32 against 64 | Digest rows | Alive at 600 s | Wall to 600 s, 32 / 64 |
|---|---|---|---|---|---|---|
| `kg` | `1a16aab93cefed1b` | 1 | identical | 601 to step 60,000 | 1,457 | 3.211 / 3.228 min |
| `t3` | `a9cd635c6257af36` | 1 | identical | 601 to step 60,000 | 1,628 | 3.042 / 3.082 min |
| `k3` | `54a03b83b457ba72` | 3 | identical | 1,801 to step 180,000 | 1,532 | 5.023 / 5.045 min |
| `k3n` | `5954a5cc5e5954b5` | 3 | identical | 1,801 to step 180,000 | 1,516 | 5.376 / 5.404 min |

`gpu-accept.py` exits 0 for all four pairs: stats identical on 60 samples, the digest on every row, lineage byte-equal.
The worlds at 3 hold three physics steps a world step, 180,000 to the others' 60,000, as the build intends.

`kg` at group 32 is also compared with the gut build's own world, `r62i1on-kg-g32` (`on-kg-g32-vs-int1.txt`): identical
on 60 stats samples and 601 digest rows, lineage byte-equal. So the kill-meal screen's world steps the same at the pace
factor 1 on this build as on the build it was pre-registered on, beside worlds at 3 in the same process. Its config
hash differs from the pre-registration's `277612f7dc63322b` only because the config now holds `physics.paceFactor`.

Every world ended at its budget, with nothing diverged, no overflow and no body refused for its class. The books'
largest audit residual was 4.7e-6 J and the largest matter residual 1.8e-6. The card peaked at 52.8 °C and 151 W across
the three groups (`card-run`'s `nvidia-smi` log).

At the founding's size, a world at the pace factor 3 ran at 1.85 to 1.99 times real time, against 3.1 to 3.3 at 1
(measured). That is about 0.6 of the pace for three times the physics. My reading is that the card's other work, the
metabolism and the writers, does not triple (inference).

## The build the launch must carry

The group launches from `scratch/wt-pace` after this file and the pre-registration are merged into branch `pace`, so
its `gitCommit` differs from `ac2d7bac`. The build's three hashes must be these, which every world of the sitting
recorded:
- core `8de5866c020f69ee016ab512d9221f43f8f1ca774fd6500dc92760204777eba1`;
- dynamics `5def5ed2225cfff36b172dcbc2c210b8cf74cabd23acd392d9c49d559ad200b3`;
- farm `de92616d4fb30f2b52816f980b9cd1f6fa684e90138ce86ec82ef718fc0652c2`.

The three worlds' config hashes must be the table's, `54a03b83b457ba72`, `a9cd635c6257af36` and `5954a5cc5e5954b5`.
