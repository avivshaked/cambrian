# The module rule's shape refusals, split by cause (a probe on round 67's world)

D298's first step (`DECISIONS.md`) and test 6 of `logbook/specs/plants-hold/spec.md` ask why the module rule refuses
nearly every addition for shape, before any rule changes. In round 67 it refused 756,984 additions on seed 6202 and let
3,659 through.

## How it was read

A probe build, branch `probe/refusal-causes` (`scratch/wt-refusal-probe`, off `card-kit`, never merged), splits the rule's
refusal test into its five conditions and counts each, for every body and for bodies with a photosynthetic part (the
lineage row's `pho`). The conditions are not exclusive. The counts are not in the checkpoint, so they start at zero on
a resume. The first build, `4beaef4d`, resumed `runs/r67r1-6202` from its 42,000 s checkpoint for 600 s on the card,
as round 67 ran (group 32, the snow's transport on the card), as `scratch/plants-work/runs/r67rp-6202`.

**The probe changes nothing in the world.** From 42,010 s to 42,600 s its 60 stats rows equal the recorded run's in all
12,720 fields but the wall times, the step counts and its own counts, and its lineage rows are the recorded run's byte
for byte.

## What it found, 42,000 to 42,600 s

| Cause | Every body | Plants |
|---|---|---|
| The body would be no larger | 45,587 (89.6 %) | 5,080 (61.8 %) |
| The body would stand inside itself (D101) | 5,280 (10.4 %) | 3,138 (38.2 %) |
| More pruned for the part limit | 0 | 0 |
| More pruned for depth | 0 | 0 |
| More pruned for reach | 0 | 0 |
| Two or more at once | 0 | 0 |
| All shape refusals | 50,867 | 8,218 (16.2 % of all) |

About 170 additions went through in the same 600 s, and 20,330 bodies were alive at 42,600 s, about four in five of
them plants.

- **Most refusals are not plants'.** Plants are about four bodies in five and hold 16 % of the refusals. The rule's
  near-total refusal in the world's totals is mostly other bodies'.
- **The limits on parts, depth and reach refuse nothing.** Every refusal is either a body that would be no larger, or
  one that would stand inside itself.
- **Among plants, the overlap test carries 38 %.** A plant's next blade would stand inside the plant itself.

A body is "no larger" when development builds the new module and then drops it, or never builds it. Development drops
a part for its volume, too small or too large (`Developer.cs`, `PrunedForVolume`), and the rule's test does not ask
about volume, so a module dropped for volume shows only as "no larger".

The second build, `0e524da6`, split "no larger" into dropped for volume and the rest, and ran the same resume for
300 s (`r67rp2-6202`). **No refusal was a module dropped for its volume.** All 22,570 refusals of a body that would be
no larger, 2,600 of them plants', had another cause. So the guess that scaled-down modules fall under the smallest
part volume was wrong.

## A dormant node

The rule picks the lowest indeterminate node whose count is under its ceiling. A body's count starts at the node's
recursive limit, and the ceiling is the larger of that limit and the node's `maxModules`. The rule does not ask whether
development ever reaches the node from the root along the genome's edges. A node it never reaches is a dormant gene,
and growing it adds no part, so a body whose chosen node is dormant is refused at every growth step for its life.

The living genomes on 6202 at 42,000 s, read with `genome-query.py`'s `reachable()`:

| | Plants | Other bodies |
|---|---|---|
| No indeterminate node | 10,473 (81.3 %) | 2,552 (53.3 %) |
| Indeterminate, none able to grow past its count | 1,406 (10.9 %) | 1,463 (30.5 %) |
| The rule's chosen node is reachable | 911 (7.1 %) | 352 (7.3 %) |
| The rule's chosen node is dormant | 91 (0.7 %) | 424 (8.8 %) |

Inference until counted: the 515 bodies whose chosen node is dormant make most of the refusals, since each fails at
every growth step for life while a body that grows reaches its ceiling and stops asking. A third build counts the
refusals whose chosen node is dormant.

The third build, `b4346ce0`, counted the refusals whose chosen node is dormant, and ran the same resume for 300 s
(`r67rp3-6202`, 42,000 to 42,300 s):

| Cause | Every body | Plants |
|---|---|---|
| The chosen node is dormant | 13,275 (52.7 %) | 1,215 (30.1 %) |
| No larger, with the chosen node reachable | 9,295 (36.9 %) | 1,385 (34.3 %) |
| The body would stand inside itself | 2,642 (10.5 %) | 1,436 (35.6 %) |
| All shape refusals | 25,212 | 4,036 |

## A node off every cycle

A body's count of a node caps how many times the node may occur along one path from the root (`Developer.CountFor`,
`CanEnter`). A node that lies on no cycle occurs at most once on a path, whatever its count, so raising its count adds
no part. Of the living bodies on 6202 at 42,000 s whose chosen node is reachable, 317 of 352 other bodies have no edge
from the node to itself, and 143 of 911 plants. Inference: these make most of the 9,295 refusals of a reachable node,
for the same reason as the dormant ones (a cycle through other nodes was not looked for).

## What follows

Two causes, together about nine refusals in ten, are a fault in the rule. It picks a node whose count can never add a
part, either a node development never reaches or one on no cycle, and the body then tries the same futile addition at
every growth step for life. Under D298 a fault is fixed as agent work: the rule picks the lowest indeterminate node
under its ceiling that development reaches and that lies on a cycle through itself, behind a switch that is off by
default. Bodies that were stuck would then grow their next node that can grow, and most of those bodies are not plants.

The third cause, a plant's next blade standing inside the plant, is the geometry, and a third of the plants' refusals.
Fixing it would change what a body may be, so under D298 it comes back to the owner as its own rule.
