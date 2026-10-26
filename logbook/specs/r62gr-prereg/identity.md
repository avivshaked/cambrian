# The card identity sitting for the growth build

*2026-10-26, the speed agent, for the coordinator. The growth build carries D241's climb, the growth-reserve gene, the
adult breeding gate and the juvenile-strength switch (`logbook/specs/growth-runway-design.md`), built by b4 on branch
`growth` with `shape-genes` merged. The sitting ran on the coordinator's card go, from the clean tree of `growth` at
`de79b51c` in `scratch/wt-growth`, with that worktree as the working directory. Its Release farm was built from
`9f41fd02`, whose source the tree keeps but for one test file. Each group founded seed 6201 for 600 s on the RTX 4090,
in single precision with the card's transport, at 10 threads on logical 22 to 31, with the heat stop on. Every world
ended at its budget, with no divergence. The comparisons are in `identity/`, beside this file, each by
`scripts/reads/gpu-accept.py`, and every one exits 0.*

## Before the card

b4's build report: Core 311 of 311, the Farm 57 of 57 and the Dynamics fixture tests 37 of 37 on lane A. The re-pinned
config fixtures are round 42 at `1d5b1496e18d9d51` and round 57 seed 2 at `7e8456d3ae873543`. The crowd fixture,
re-recorded on the merged tree (`runs/cefix-s4/2026-10-26-203434-b2f9267b`), equals the pace build's in every stats and
lineage row.

The tree hashes to core `361aa924`, dynamics `29144991` and farm `f9719a88` (`Manifest.HashSourceTree`, reproduced, and
named on every manifest).

## Part 1: the climb off, against the pace build, at group 32

`rounds/identity-growth-off.ps1` runs the pace sitting's part 2 worlds: `kg`, `t3`, `k3` and `k3n`. Each is compared
with `scratch/pace-build/identity-on/r62pon-<world>-g32`, the same world on the pace build at 14 threads on 16 to 29.

| World | Stats | Digest | Lineage | Times real time, growth build / pace build |
|---|---|---|---|---|
| `kg` | identical, 60 samples | identical, 601 rows | byte-equal, 1,582 lines | 3.22 / 3.11 |
| `t3` | identical, 60 samples | identical, 601 rows | byte-equal, 1,723 lines | 3.39 / 3.29 |
| `k3` | identical, 60 samples | identical, 1,801 rows | byte-equal, 1,628 lines | 2.00 / 1.99 |
| `k3n` | identical, 60 samples | identical, 1,801 rows | byte-equal, 1,632 lines | 1.87 / 1.86 |

So with T = 0 the growth build is the pace build on the card, bit for bit, at both pace factors. It is so at 10 threads
against 14, and with b4's shape genes merged in. In 600 s of small worlds, 10 threads cost no pace (measured, one
reading).

## Part 2: growth group 1's worlds, at groups 32 and 64

`rounds/identity-growth-on.ps1` runs `kg` with the climb off beside the group's four worlds with it on, in one card
process. Each is compared with itself at the other group size, and `kg` with part 1's.

| World | Stats | Digest | Lineage | Config | Times real time at 32 |
|---|---|---|---|---|---|
| `kg` | identical | identical, 601 rows | byte-equal, 1,582 lines | as part 1 | |
| `t60` | identical | identical, 601 rows | byte-equal, 1,427 lines | `6dbb0315e486251f` | 2.74 |
| `t120` | identical | identical, 601 rows | byte-equal, 862 lines | `7796b957054d16da` | 2.81 |
| `t240` | identical | identical, 601 rows | byte-equal, 305 lines | `9e73cabb18b29863` | 3.07 |
| `t120s` | identical | identical, 601 rows | byte-equal, 843 lines | `4d8d5ead1e75b62f` | 2.91 |

`kg` at 32 in part 2 equals part 1's `kg`, so a world with the climb off is unchanged by sharing a card process with
worlds that have it on.

Two things are visible beside the identity:
- **The switches reach the config.** The strength arm's header reads "climb T 120 s (1.44 consumer clocks) reserve
  30 s [1-300] mut 0.08 · juvenile strength scaled". The farm's consumer clock, from the world's cell table, is 83 s.
  The design's figure of about 60 s was a grazer's own tissue over its upkeep.
- **Founding is slower under the climb.** Lineage holds fewer rows by 600 s as T rises, from 1,427 at T = 60 to 305 at
  T = 240, against 1,723 in `t3` (measured). The founders breed only once adult, and that is the adult gate working.

## The launch

Growth group 1 launches from `scratch/wt-growth` with this file's commit merged in, on the same cores and threads. Its
manifests must carry core `361aa924`, dynamics `29144991`, farm `f9719a88`, and the four configs above.
