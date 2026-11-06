# The anchored founders' smoke: grips form on island coral, and the children drift (2026-11-06)

The grip smoke on round 67's world formed no grip, because no founder carried the anchoring cell
(`grip-smoke-r67.md`). D296 sets half the plant founders on island coral with a holdfast of the cell, and D298 gives
half of those blades that can grow. This smoke asks three things: whether the card carries a body whose root is a
holdfast, whether a holdfast set on rock grips under true-shape contact, and whether the anchored plants hold the rock.

## What ran

Branch `plants-build` (`scratch/wt-plants-build`, `e670d777`), `rounds/launch-plants-anchor.ps1`: round 67's world
with D268's additions, the anchoring cell's bud weight at 4, `EVOSIM_ANCHORED_FOUNDERS` 0.5, `EVOSIM_ANCHORED_GROWABLE`
0.5 and the module rule's growable choice on; D297 off. Two worlds, seeds 6202 and 6203, founded on the card at group 32
for 3,000 s, as `scratch/plants-work/runs/pla-6202` and `pla-6203`. They ran at about three times real time.

## What it found

**The card carries** a body whose root is a holdfast. No world diverged, and both books closed: the largest energy residual was 2.9e-6 J and the
largest matter residual 2.9e-6, under the 0.1 J and 1e-5 lines.

**A holdfast set on rock grips**, the first grip formed in a world.

| Seed | Anchored founders set | Refused for want of a spot | Made growable | Grips held at 300 s | at 600 s | at 3,000 s |
|---|---|---|---|---|---|---|
| 6202 | 10 | 1 | 5 | 8 | 8 | 4 |
| 6203 | 12 | 0 | 8 | 12 | 11 | 1 |

Grips also break and form again often: on 6202 at 900 s the row counts 42 formed and 37 broken against 5 held. Whether
that is the newborn holdfast growing into the rock as the body grows, as the build's report warned, is not read here.

**The line breeds**, and its children drift. The table counts living bodies carrying the anchoring cell, from
`genome-query.py find --cell anchoring`.

| Seed, time | Living bodies | Carrying the cell | Founders (g0) | Born (g1 and on) | Grips held |
|---|---|---|---|---|---|
| 6202, 600 s | 252 | 96 | 8 | 88 | 8 |
| 6202, 3,000 s | 975 | 256 | 3 | 253 | 4 |
| 6203, 600 s | 180 | 37 | 11 | 26 | 11 |
| 6203, 3,000 s | 1,941 | 155 | 1 | 154 | 1 |

Only founders hold grips. A child is placed beside its parent at the plants' dispersal (D264's 30 m), in open water at
its parent's depth, so a holdfast born from a holdfast never touches rock. Inference: the anchored founders' line
spreads as drifting plants that carry an idle holdfast, and the forest on the coral lasts one generation.

**The growable blades grow** and shed. On 6203 the module rule added 46 modules and dropped 40 by 3,000 s, with 59
refused for shape; on 6202, 13 added and 3 dropped.

## What follows

A forest on the rock needs children that reach the rock. Today nothing brings a holdfast's child to the coral: no plant
sinks (D064 leaves small bodies neutral), and the dispersal places it in the water. Bringing it there changes what a
birth is, so it is a world rule and goes to the owner (`proposals/propose-plants-grow.md`, section 6). The grip churn is
read next, with the probes, before any screen.
