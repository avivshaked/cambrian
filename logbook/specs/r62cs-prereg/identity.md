# The D224 build's card identity (2026-10-24)

The pre-registration (`predictions.md`) says the card identity runs prove the build on the card before the launch.
This file records them. The outputs named below are in `identity/`, beside this file.

## What ran

`rounds/identity-r62cs.ps1` ran twice on the coordinator's go: once at group 32 and once at group 64, one sitting at a
time. Each sitting founded seed 6201 twice, for 600 s each, on the RTX 4090 in single precision with the card's
transport:

- **r62csid-off**: the new dial off, `rounds/env-r62ce.ps1`, config hash `a10a932a79257c44`.
- **r62csid-on**: the new dial on with the remains' window at 1,500 s, `rounds/env-r62cs-w1500.ps1`, config hash
  `b5de43749017cfbb`, the screen's top arm.

The four runs are under `runs/r62csid-off-g32`, `-on-g32`, `-off-g64` and `-on-g64`. All four manifests name commit
`6f176e27` with a clean tree, and their code hashes are equal: core `75e257f4`, dynamics `bb71b218`, farm `d6da2b07`
and kernel `91063e49`. The kernel is `r62ce`'s, since D224 changes no card code. Every world ended at its budget with
no body diverged, at about 4.0 to 4.1 times real time.

## Group 32 against group 64

`python -I scripts/reads/gpu-accept.py <run at 32> <run at 64>`, for each world:

| World | Exit | Stats | Digest | Lineage |
|---|---|---|---|---|
| dial off | 0 | identical on all 60 samples to 600 s | identical on all 601 rows to step 60,000 | byte-equal, 1,638 lines |
| dial on, 1,500 s | 0 | identical on all 60 samples to 600 s | identical on all 601 rows to step 60,000 | byte-equal, 1,570 lines |

The outputs are `off-32-vs-64-accept.txt` and `on-32-vs-64-accept.txt`.

## The dial off against `r62ce`'s build

`r62csid-off-g32` is the same world as `runs/r62ceid-on-g32`, which `r62ce`'s build recorded at group 32 that morning
(`../r62ce-prereg/identity.md`). With D224's dial off, gpu-accept exits 0 between the two. Stats are identical on 60
samples, the digest on 601 rows, and lineage is byte-equal on 1,638 lines (`off32-vs-r62ceid.txt`). The config hash
differs, `a10a932a` against `a99d10a5`, only because the config now carries `biteTakesStore` at its off value.
`scripts/reads/compare-run-rows.py` agrees, with 11,280 values equal and no field in one run alone
(`off-g32-vs-r62ceid-on-g32-rows.txt`).

## What it does not show

**`biteStore` was 0 in both dial-on sittings.** By 600 s no mouth is working yet, so no bite took a reserve. The
sittings prove the 1,500 s window on the card, with 58 remains standing at 600 s, but not the store share's path.
The store share is Core's arithmetic on the host, and the card only steps the bodies. So my reading is that the Core
tests and the CPU proofs cover it, and that is inference. The coordinator accepted it for the launch on that basis
(2026-10-24). After the screen, a 600 s window from one of its dial-on checkpoints where `biteStore` is above zero
is to be run at 32 against 64, to show it on the card.

## The window after the screen

`rounds/identity-r62cs-window.ps1` ran on the coordinator's go on 2026-10-24, once at group 32 and once at 64, from
commit `ddb62242` with a clean tree. Each sitting resumed `r62cs-w1500-6201` and `r62cs-w60-6201` from their 6,000 s
checkpoints for 600 s, in one group, since a card group is two worlds or more. The runs are under
`scratch/contact-eating/identity-cs-window`. All four ended at their budget. Their core hash is `75e257f4`, as the
screen's, and both books closed on every row. `biteStore` rose from 5,604 to 9,491 J in the first world and from
6,765 to 9,952 J in the second, so the store share's path was stepped.

| World | Exit | Stats | Digest | Lineage |
|---|---|---|---|---|
| w1500-6201 | 0 | identical on all 60 samples to 6,600 s | identical on all 600 rows to step 660,000 | byte-equal, 3,017 lines |
| w60-6201 | 0 | identical on all 60 samples to 6,600 s | identical on all 600 rows to step 660,000 | byte-equal, 3,352 lines |

The outputs are `window-w1500-32-vs-64-accept.txt` and `window-w60-32-vs-64-accept.txt`. So the store share steps
alike at groups 32 and 64 on the card.

**The resume is not the unbroken run.** `scripts/reads/compare-run-rows.py` between each screen world and its
window at group 32, from 6,000 s, finds the first difference at the first row, 6,010 s. In both worlds it is in
`gestatedJoules`, in the eighth significant figure: 132,058.157 against 132,058.148 in w1500-6201. Lineage
differs from row 32 (`window-w1500-g32-vs-screen-rows.txt`, `window-w60-g32-vs-screen-rows.txt`). The card's notes say
a card resume repeats its run exactly.

**It is older than D224's dial.** `rounds/probe-r62cs-resume.ps1` ran on the coordinator's go the same evening, from
commit `7e0811da` with a clean tree. It resumed `r62cs-w1500-6201` and `r62cs-w1500-6202` from 1,500 s for 100 s.
Over that span `biteStore` stayed at 0, so the store share never acted. Both worlds part from the screen's rows at
the first row, 1,510 s, again in `gestatedJoules` in the seventh or eighth significant figure. Lineage parts at
rows 17 and 13 (`probe-w1500-6201-from1500-vs-screen-rows.txt`, `probe-w1500-6202-from1500-vs-screen-rows.txt`).
So the parting comes with contact eating, the remains or this build's checkpoint, and which one is not yet found.
Until it is, a stopped world of this build gets a fresh rerun or is read as a cousin.
