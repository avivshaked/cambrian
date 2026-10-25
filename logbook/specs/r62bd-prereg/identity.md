# The D226 build's card identity (2026-10-24)

The pre-registration (`predictions.md`) says the card identity runs prove the build on the card before the launch.
This file records them. The outputs named below are in `identity/`, beside this file.

## What ran

`rounds/identity-r62bd.ps1` ran twice on the coordinator's go, once at group 32 and once at group 64, one sitting at a
time. Each sitting founded seed 6201 twice, for 600 s each, on the RTX 4090 in single precision with the card's
transport:

- **r62bdid-off**: the new dial at its default of 1, in `rounds/env-r62cs-w1500.ps1`, config hash `60e7fe563fbe0c74`.
- **r62bdid-x16**: the dial at 16 with the intake scale at 16, in `rounds/env-r62bd-x16-i16.ps1`, config hash
  `169145a496b923e7`.

The four runs are under `scratch/contact-eating/identity-bd`. All four manifests name commit `0bf939dd` with a clean
tree. Their core hash is `69a403e6`. Every world ended at its budget, with both books closed on every row.

## Group 32 against group 64

`python -I scripts/reads/gpu-accept.py <run at 32> <run at 64>`, for each world:

| World | Exit | Stats | Digest | Lineage |
|---|---|---|---|---|
| dial at 1 | 0 | identical on all 60 samples to 600 s | identical on all 601 rows to step 60,000 | byte-equal, 1,570 lines |
| dial at 16 | 0 | identical on all 60 samples to 600 s | identical on all 601 rows to step 60,000 | byte-equal, 1,582 lines |

The outputs are `off-32-vs-64-accept.txt` and `x16-32-vs-64-accept.txt`.

## The dial at 1 against D224's build

`r62bdid-off-g32` is the same world as `runs/r62csid-on-g32`, which D224's build recorded at group 32
(`../r62cs-prereg/identity.md`). gpu-accept exits 0 between the two. Stats are identical on 60 samples, the digest on
601 rows, and lineage is byte-equal on 1,570 lines (`off32-vs-r62csid-on-g32.txt`). The config hash differs only
because the config now carries `biteDamageScale` at 1. `scripts/reads/compare-run-rows.py` agrees, with 11,340 values
equal and no field in one run alone (`off-g32-vs-r62csid-on-g32-rows.txt`).

The world at 16 has parted from the world at 1 by 600 s, with 1,582 lineage rows against 1,570. That says nothing
about the dial: the two worlds also differ in the remains' window (500 s against 1,500 s) and the intake scale (16
against 1), and no mouth has bitten by 600 s. That the dial deepens a wound is shown by the check
(`check/README.md`) and by the Core tests.

## The launch build, after the mouth-blow counters

The counters of D227's read (`MouthBlows` and `MouthBlowsFinishing`, stats columns `mouthBlows` and
`mouthBlowsFinishing`) change the code, so the identity was run again on the launch build.
`rounds/identity-r62bd-launch.ps1` ran on the coordinator's go, once at group 32 and once at 64, from commit
`4e94b0e0` with a clean tree, core hash `37b4f26e`. Each sitting founded seed 6201 four times for 600 s, under
`scratch/contact-eating/identity-bdl`:

- **r62bdl-off**: the dial at 1, in `rounds/env-r62cs-w1500.ps1`, config hash `60e7fe563fbe0c74`;
- **r62bdl-x2**, **-x4** and **-x16**: the screen's three arms, in `rounds/env-r62bd-x<scale>-i16.ps1`, config
  hashes `6da8f3c6156c8a0e`, `490de19bef1482d0` and `169145a496b923e7`.

All eight ended at their budget. gpu-accept exits 0 for each world at 32 against 64, with stats identical on 60
samples, the digest on 601 rows and lineage byte-equal (`launch-<world>-32-vs-64-accept.txt`). The world at 1 against
`runs/r62csid-on-g32` exits 0 as well (`launch-off32-vs-r62csid-on-g32.txt`). And `r62bdl-x16-g32` against
`r62bdid-x16-g32`, the same world before the counters, has 11,340 values equal and lineage equal apart from the
origin columns. The only fields in one run alone are the two new columns, so counting changes nothing the world
computes (`launch-x16-g32-vs-r62bdid-x16-g32-rows.txt`).

Both counters read 0 at 600 s in every arm, since no mouth has bitten yet. The Core tests show that they count, and
the screen's first looks after about 3,000 s show it in a live world.
