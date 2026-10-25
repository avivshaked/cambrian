# The contact-eating build's card identity (2026-10-24)

The pre-registration (`predictions.md`) says the card identity runs prove the build on the card before the launch.
This file records those runs. The outputs named below are in `identity/`, beside this file.

## What ran

`rounds/identity-r62ce.ps1` ran twice on the coordinator's go: once at group 32 and once at group 64, one sitting at a
time. Each sitting founded seed 6201 twice, for 600 s each, on the RTX 4090 in single precision with the card's
transport:

- **r62ceid-off**: the dial off, `rounds/env-r62-h.ps1`, config hash `6901478b8e2fea6a`.
- **r62ceid-on**: the dial on, `rounds/env-r62ce.ps1`, config hash `a99d10a5c2485585`.

The dial-off hash is not round 62's `c5c9f4167e070655`. The config now carries three fields that round 62's build had
not got (`intakeReachFromMouth`, `contactEating` and `remainsWindowSeconds`), each at its off value.

The four runs are under `runs/r62ceid-off-g32`, `-on-g32`, `-off-g64` and `-on-g64`. Both sittings ran the same
Release farm, built before either from the source of `4935a4b8`. The manifests name commit `b7611b58`, which changes
no source. Their code hashes are equal: core `5a66b5fd`, dynamics `bb71b218`, farm `96ab5286`
and kernel `91063e49`. The group 32 sitting reads `gitDirty false`. The group 64 sitting reads `gitDirty true`,
because while it started I edited a reader script, `scripts/reads/compare-rows.py`. That script is in none of the
hashed projects, and I restored it afterwards. Every world ended at its budget with no body diverged, at about 4.05
times real time.

## Group 32 against group 64

`python -I scripts/reads/gpu-accept.py <run at 32> <run at 64>`, for each world:

| World | Exit | Stats | Digest | Lineage |
|---|---|---|---|---|
| dial off | 0 | identical on all 60 samples to 600 s | identical on all 601 rows to step 60,000 | byte-equal, 1,618 lines |
| dial on | 0 | identical on all 60 samples to 600 s | identical on all 601 rows to step 60,000 | byte-equal, 1,638 lines |

The outputs are `off-32-vs-64-accept.txt` and `on-32-vs-64-accept.txt`.

## The dial off at group 32 against round 62's record

This compares `r62ceid-off-g32` with `runs/r62-h6201/2026-10-23-090508-c5c9f416` over its first 600 s. That run is
round 62's world on the same seed, recorded at group 32 on the build before contact eating (`b5fcd5b6`).

**gpu-accept's raw exit is 1, on the seven intake-origin columns alone.** The new build writes seven stats columns
that `b5fcd5b6` did not: `intakeOwnKillJ`, `intakeOtherKillJ`, `intakeCarrionJ`, `mouthedIntakeOwnKillJ`,
`mouthedIntakeOtherKillJ`, `mouthedIntakeCarrionJ` and `intakeOriginFrom`, from `44712ce3`. Its death rows likewise
carry `iok`, `iak` and `ic`. gpu-accept reports the first difference at 10 s in seven fields, which are these. On the
rest it reads: digest identical on all 601 shared rows to step 60,000, and lineage byte-equal on the shorter run's
1,618 lines. The output is `off-g32-vs-r62-h6201-accept.txt`.

**The comparison without them** is

```
python -I scripts/reads/compare-run-rows.py runs/r62-h6201/2026-10-23-090508-c5c9f416 runs/r62ceid-off-g32/2026-10-24-021920-6901478b --from 0
```

It leaves out the `wall*` fields and those ten names, and compares every other field the two rows share. Over 60
stats rows (10 to 600 s), it compared 179 fields a row, 10,740 values, and found no difference. The fields in one run
only are the seven columns and nothing else. Lineage over (0, 600] s holds 1,618 rows in each, equal in order apart
from `iok`, `iak` and `ic`. The output is `off-g32-vs-r62-h6201-rows.txt`.

## What it shows

With the dial off, the card steps round 62's world bit for bit as round 62's build did. With the dial on, the card agrees
with itself across group sizes, remains included. At 600 s the dial-on world held 13 remains standing, and 40 had
closed, burning 5,514 J. Its audit and matter residuals were -5.5e-6 and -5.0e-7. No bite had fed by then, because no
mouth was working yet. So these runs prove the remains on the card, and not yet the bites.
