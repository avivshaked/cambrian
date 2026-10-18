# Why a card resume diverges at once: the checkpoint and the GPU engine (2026-10-15)

A read-only code survey of `scratch/wt-r59/src` at `c851d57c`, made by an agent for round 59's B3. The key lines of
cause 1 were checked by hand. Its finding: the checkpoint does not carry the card's link-sphere rest, D168's
`LEx/LEy/LEz`.

## Cause 1: the link-sphere rest is lost on restore

Read in code:
- `gpu-runner.template:2532-2534`. Pack writes the rest as `LinkContactCentre - (float)LinkContactCentre`.
- `gpu-runner.template:2882-2884`. Unpack folds the rest into the host `LinkContactCentre` as `sph7 + sph14`.
- `Creature.cs:679-689` (`RestoreLinkSpheres`, called at `Creature.State.cs:238`). Restore rebuilds
  `LinkContactCentre` from the poses, not from the file. Its remark calls this exact. That holds on the CPU engine,
  not on the card since `2175d8de` (2026-10-13).
- The kernel's bed, flat-bed, glass, reef and pair tests read the committed sphere's rest (`gpu-kernel.template`
  5210-5215, 5320, 5339-5342, 5381, 5423). For one step after a resume that rest is 0.
- `CheckpointFidelity` runs the CPU engine only, where the centre equals the position, so it cannot see this.

Inference: the error is up to about one float step of position, about 1 to 15 µm at 10 to 150 m. That is enough to
flip resting bodies across `pen > 0`, so the bed-or-glass count moves first, as B3's did at 45,001 s.

## Cause 2: every body is split again at pack

Read in code: `gpu-runner.template:2338-2345`. The unbroken run splits a body's base into float and carry only at
birth or resize; a resume splits every body.

Inference: lossless except at exact half-ulp ties. The link rest's two-sum chain (`kernel:5898-5903`) lets it exceed
half a float step, so storing `LinkContactCentre` in double alone would not reproduce the card's pair. A fix carries
the pair itself, or rebuilds the rest with the kernel's own `Poses` arithmetic at pack.

## Carried exactly, or rewritten before it is read

Per-link damage and contact senses, rock touch (`TUCH`), grip (`GRIP`), held overlap ids, `Settled`,
`PreviousCentre` and `PreviousRoot`, brains, drive history, RNG, the scent field's refill, the snow transport (no
device state), the step's overlap list and `TouchedBedOrGlass` (rewritten before they are read), `Fext`, `Tau` and
`LimitImplicit`.

## A prediction already contradicted

The survey predicted that the 30 s CPU-accelerator founded check would also diverge on this build. Round 59's check
(`r59-k`, `d5e70be8`, 2026-10-14) matched at 15 s. Inference: at 15 s few bodies rest on the bed, so a zero rest
changes nothing yet.
