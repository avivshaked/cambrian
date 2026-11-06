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

## The second run (D315, D316), written before it starts

The owner ruled that a settling child goes no deeper than its parent and an anchored founder no deeper than 9 m (D315),
and that island coral grows taller and varied, trunks 6 to 16 m (D316). The second run is the first run's world with
both (`rounds/env-plants-forest2.ps1`, `EVOSIM_SETTLE_NO_DEEPER` 1, `EVOSIM_ANCHORED_FOUNDER_MAX_DEPTH` 9 and the coral
trunk heights), launched by `rounds/launch-plants-forest2.ps1` as `plg-6202` and `plg-6203`, 12,000 s at group 32, then
the same two worlds at group 64 for 1,200 s. The build is `plants-build` with the depth switches merged.

**What counts** is the first run's test unchanged: at 12,000 s, one island's coral carries at least 20 living gripping
bodies, at least four in five of them born after 3,000 s. Read besides: the gripping bodies by depth and by island at
every 1,500 s, whether a stand outlasts a lean half of the season (period 6,000 s), the anchored founders refused for want
of a spot within 9 m, the children that found no spot no deeper than their parent, and the swimmers' numbers against
the first run's, since taller coral changes the upper water they live in.

## What the second run found

The two worlds ran from 17:11 to 18:43 on 2026-11-06 at 2.9 and 2.2 times real time, as
`scratch/plants-work/runs/plg-6202/2026-11-06-171122-1171cc99` and `plg-6203/2026-11-06-171122-1171cc99`. Both books
closed: the largest energy residual was 3.4e-5 J and the largest matter residual 7.2e-6. The reads are `read2.py`,
`read3.py` and `read4.py` in `scratch/plants-work/forest-read/`, with their outputs beside them.

**No forest holds.** At 12,000 s, 6202 has two gripping bodies and 6203 has one. The answer, by the test fixed above,
is no again.

| Seed | Gripping, living, at 1,500 s | 3,000 s | 4,500 s | 6,000 s | 7,500 s | 9,000 s | 10,500 s | 12,000 s |
|---|---|---|---|---|---|---|---|---|
| 6202 | 35 | 44 | 32 | 7 | 17 | 15 | 11 | 2 |
| 6203 | 41 | 35 | 18 | 4 | 6 | 14 | 18 | 1 |

**The depth limits did what they were for.** The gripping bodies' median depth was 3.2 to 4.6 m on 6202 and 3.3 to 6.0 m
on 6203 at the marks above, against 6.6 to 9.8 m in the first run. A gripping body's median time from its first grip to
its death rose from 364 to 860 s on 6202 and from 314 to 700 s on 6203. The 9 m limit refused two anchored founders of 51
on 6202 and one of 51 on 6203, because the taller coral puts rock within 9 m almost everywhere.

**Most children now find no place to settle.** By 12,000 s, 107 children settled and 364 found no spot on 6202, and 80
settled and 656 found none on 6203: 77 % and 89 % failed, against 23 % and 42 % in the first run. Inference: a parent
that grips on a crest has little rock in its reach that lies no deeper than it, so a stand cannot spread down its own
island.

**Each winter takes the stand.** This world has two seasons (D151). Summer runs to 2,750 s and from 5,750 to 8,750 s, at
1.6 times the year's mean light; winter runs from 2,750 to 5,750 s and from 8,750 to 11,750 s, at 0.4 times. Counting
every body seen gripping at a 100 s sample:

| Seed | Season | Began gripping (founders) | Grippers died | Of those, starved | Gripping at its end |
|---|---|---|---|---|---|
| 6202 | summer 1 | 84 (10) | 30 | 14 | 52 |
| 6202 | winter 1 | 50 (13) | 97 | 58 | 9 |
| 6202 | summer 2 | 30 (8) | 18 | 7 | 19 |
| 6202 | winter 2 | 29 (16) | 47 | 30 | 1 |
| 6203 | summer 1 | 101 (8) | 56 | 21 | 48 |
| 6203 | winter 1 | 49 (16) | 92 | 52 | 2 |
| 6203 | summer 2 | 33 (12) | 20 | 10 | 18 |
| 6203 | winter 2 | 40 (12) | 54 | 35 | 1 |

The other deaths are of age (D128), and one eaten in each of two seasons on 6203. In each winter about twice as many
grippers died as began, and most of the dead starved, at 3 to 6 m. The first run's worlds rose and fell with the same
seasons.

The test's moment, 12,000 s, falls 250 s after the second winter ends, the lowest point of the year, which this file did
not see when it was written. The answer does not rest on it. After the first winter, the most gripping bodies in the
whole world at any 100 s sample were 20 on 6202, at 8,500 s, and 18 on 6203, at 8,700 s. No single island reached 20
even at the end of a summer.

Plants as a whole fell in winter too, by less: on 6202 the photosynthetic bodies went from 768 at 3,000 s to 339 at 6,000
s while the gripping ones went from 44 to 7. The swimmers' numbers moved both ways against the first run's, larger on
6202 and smaller on 6203, with no steady difference. Regrowth ran this time on both seeds, 39 and 56 regrowths by
12,000 s.

Inference: two things now stop a forest, and this run does not separate them. Children cannot settle near a crest
parent, so a stand cannot replace its dead; and the winter starves grippers even in shallow water.

**The card check passes.** The same two worlds ran at group 64 for 1,200 s (`plg-g64-6202` and `plg-g64-6203`, from
18:46). They equal the group 32 worlds' first 1,200 s on both seeds, in `forest2-accept-6202.txt` and
`forest2-accept-6203.txt`: stats identical on all 120 shared samples, the digest on all 1,201 rows to step 120,000, and
the lineage byte-equal on the shorter run's 779 and 2,484 lines.

## Without the winter: a diagnostic, written before it starts

The second run does not say whether the winter or the settling limit stopped its forest. This run takes the second
run's world with one setting changed, the season's amplitude at 0, so the light keeps its day of 1,000 s and the
year's mean but has no winter (`rounds/env-plants-nowinter.ps1`, launched by `rounds/launch-plants-nowinter.ps1` as
`plw-6202` and `plw-6203`, 12,000 s at group 32 on `plants-build`). It is a diagnostic. The plant work's world keeps
its seasons unless the owner rules otherwise.

**How it is read.** The forest test is the same as above. If it passes on either seed, the winter is what stopped the
second run. If no island holds 20 gripping bodies, four in five of them born after 3,000 s, at any 100 s sample after
3,000 s on either seed, the settling limit stops a forest even without a winter. Anything between is reported as it
stands. Read besides: the share of settling children that found no spot, against the second run's 77 and 89 %, and the
grippers that began and died in each 3,000 s span, against the second run's table.
