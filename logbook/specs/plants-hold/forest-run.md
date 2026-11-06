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

## What it found

The two worlds ran from 13:41 to 14:57 on 2026-11-06 at 2.3 to 3.9 times real time, as
`scratch/plants-work/runs/plf-6202/2026-11-06-134402-c7e97670` and `plf-6203/2026-11-06-134402-c7e97670`. Both books
closed: the largest energy residual was 3.6e-4 J and the largest matter residual 4.6e-6, under the 0.1 J and 1e-5 lines.
The reads are `scratch/plants-work/forest-read/` and `scratch/plants-work/settle-probe/`.

**No forest holds.** At 12,000 s, 6202 has no gripping body and 6203 has one. The answer, by the test fixed above, is no.

| Seed | Gripping, living, at 1,500 s | 3,000 s | 4,500 s | 6,000 s | 7,500 s | 9,000 s | 10,500 s | 12,000 s |
|---|---|---|---|---|---|---|---|---|
| 6202 | 21 | 22 | 6 | 2 | 23 | 17 | 3 | 0 |
| 6203 | 22 | 11 | 2 | 2 | 1 | 0 | 2 | 1 |

**Settled children grip, and most starve.** Every child set on coral by D307 carries the grip flag by the first sample
after its birth, 1 to 10 s, and keeps it until it dies; the tracker's counts equal the worlds' `childrenSettled` and
`childrenSettleFailed` exactly (123 and 45 on 6202, 61 and 44 on 6203, to 6,000 s). No grip was let go. Most children
were set below their parent: the child's height less the parent's has a median of -4.5 m on 6202 and -2.2 m on 6203,
because the coral in the parent's reach runs down the island's flanks. Light falls as e^(-d/6) from the surface, to a
fifth at 9 m, and below about 9 m a plant starves within a few hundred seconds, founder or newborn: of children set more
than 3 m below their parent, 79 of 81 starved on 6202 (median life 330 s), and 14 of 16 founders that gripped at 9 to
18 m starved there (median 148 to 170 s).

**A forest renewed itself for one season on 6202.** After the founders set at the start died, settled children grew a
second stand: 23 gripping plants at 7,500 s and 38 at 8,500 s, every one born after 3,000 s. 37 of the 38 stood in one
patch at x 155 to 184 m, z 130 to 160 m, 6.5 to 16.8 m deep, and all 37 were children born in the world. The 38th,
4698, was a founder that arrived with the trickle at 7,885 s and gripped alone at x 102. Such late founders kept
arriving with a holdfast and gripping through the run, 33 on 6202 and 44 on 6203 from 3,250 to 11,620 s (the film
lane's count). The lean half of the season (period 6,000 s) took it: 3 at 10,500 s and none at
12,000 s. Inference: the forest can renew itself through its children in the light, and a stand that started shallower
might outlast the lean season.

The counters `gripsFormed` and `gripsBroken` are totals over the living bodies and rise and fall with them, so they do
not show grips forming over time; the positions' grip flag does. A grip released because a body's reserve ran out is
counted as broken.

**The card check passes.** The same two worlds at group 64 for 1,200 s (`plf-g64-6202` and `plf-g64-6203`, from
15:01; the first launch refused beside the first group's closing watch, and the launcher now waits) equal the group 32
worlds' first 1,200 s on both seeds by `scripts/reads/gpu-accept.py`: stats identical on all 120 shared samples, the
digest on all 1,201 rows to step 120,000, and the lineage byte-equal on the shorter run's 859 and 935 lines
(`forest-accept-6202.txt`, `forest-accept-6203.txt`). Settling ran inside that span, 37 and 19 children by 1,200 s, so
the grip state a conception reads is the same at either group size. Regrowth did not: the first regrowths came later,
48 on 6203 by 12,000 s and none on 6202, so the check does not cover them.
