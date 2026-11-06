# The forest run: does a forest hold on an island once a gripping plant's children settle? (2026-11-06)

Written before the run starts, so that what counts as a forest is fixed before anyone sees the result.

In the anchored founders' smoke (`anchor-smoke.md`), holdfasts set on island coral gripped, but every child was
placed in open water, so the forest lasted one generation. D307 now lets a gripping plant's child settle on coral within its
parent's reach, and D298 lets a bitten blade grow back. This run asks whether a forest then holds on the rock beyond its founders.

## What runs

Branch `plants-build` (`scratch/wt-plants-build`), with D307 and D298's regrowth merged in at `02a57851`. With every
switch off, that build steps the crowd fixture's world row for row as the build before it. The launcher is
`rounds/launch-plants-forest.ps1`, and the world is `rounds/env-plants-forest.ps1`: the smoke's world (round 67's world
with D268's additions, the anchoring cell's bud weight at 4, half the plant founders anchored and half of those
growable, the module rule's growable choice on) with `EVOSIM_ANCHOR_SETTLE` 1 and `EVOSIM_MODULE_REGROW` 1. It runs two
worlds, seeds 6202 and 6203, on the card at group 32 for 12,000 s, with a checkpoint every 1,500 s, under
`scratch/plants-work/runs/plf-6202` and `plf-6203`. The same two worlds then run at group 64 for 1,200 s.

## What counts

**A forest holds on an island** if, at 12,000 s, the coral of one island carries at least 20 living bodies that hold a
grip (positions flag 16), and at least four in five of the gripping bodies alive then were born after 3,000 s. The
second test asks that the forest renewed itself through its children rather than being the founders and their first
offspring grown old. An island is the coral column group the placer reads (`CoralIslandColumns`); a gripping body is
assigned to the island under its position.

**The run answers no** if no island meets both tests on either seed. A forest that meets the first test but not the
second is reported as a forest of founders that did not renew itself, which is not a hold.

**The card check passes** if the group 64 worlds equal the group 32 worlds' first 1,200 s on the stats, the digest and
the lineage, by `scripts/reads/gpu-accept.py`. If it fails, the forest run is not used for the film until the cause
is found.

## What is read besides

At every 1,500 s checkpoint: the gripping bodies by island, the children settled and those that found no spot
(`childrenSettled`, `childrenSettleFailed`), the regrowths and their refusals, and the living plants and animals. The
film lane gets the times and places of the thickest forest if one holds.
