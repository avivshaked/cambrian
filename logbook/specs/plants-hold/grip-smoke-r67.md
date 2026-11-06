# The grip smoke on round 67's world (2026-11-06)

The owner asked on 2026-11-06 whether the grippers work. The anchoring cell (D268) has been on main, off by default,
since 2026-10-31. No grip had formed in any world: round 59's planted grippers hung 0.27 m above the rock, and D268's
smoke of 2026-10-31 formed none, both under sphere contact. Worlds have used true-shape contact since round 65, and no
world with the cell had run under it.

## What ran

Round 67's world with D268's additions as `rounds/env-anchor-brain-check.ps1` makes them on main: the anchoring cell in
the table, the touch of the rock and its sense, the drag on turning and the brain's hold. The cell's bud weight was 8
rather than 1, to start it overpowered (D262). Launcher `rounds/env-r67-grip.ps1` and `rounds/launch-r67-grip.ps1` on
`card-kit` (`0c94b308`). Two worlds, seeds 6202 and 6203, founded on the card for 3,000 s, as
`scratch/plants-work/runs/r67g-6202` and `r67g-6203`. Both ran at about four times real time, and both books closed.

## What it found

| Seed | Living bodies at 3,000 s | Carrying the anchoring cell | Grips formed | Touches of the bed or glass |
|---|---|---|---|---|
| 6202 | 2,582 | 24 | 0 | 10,038 |
| 6203 | 3,050 | 18 | 0 | 1,715 |

- **No grip formed in either world.**
- **The cell stayed rare.** On 6202, 29 of the 4,478 genomes written in 3,000 s carried it. The random founders draw
  their cells from `founderCellTypes` (photosynthetic, absorptive, consumer), which does not hold the anchoring cell, so
  it enters only by mutation (`cellTypeChance` 0.005 a node), and its bud weight does not reach the founders.

So the smoke does not answer whether a grip can form under true-shape contact. It shows that a gripper no founder
carries stays rare for the first 3,000 s. The test that answers it needs founders that carry the cell, set touching the
rock: D296's anchored founders, half the plant founders set against island coral, which is the plants spec's build
(`logbook/specs/plants-hold/spec.md`, rule 1, and its proofs).
