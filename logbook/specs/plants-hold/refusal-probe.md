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

## On the fixed build, written before it runs

With the fix on (`EVOSIM_MODULE_GROWABLE`), the forest worlds still refuse 71 to 96 % of their additions for shape
(`forest-run.md`, after "What light at each part found"). A second probe build, branch `probe/refusal-fixed`
(`scratch/wt-refusal-fixed`, `4dee4844`, off `plants-build` `8676833f`, never merged), carries the same counts onto the
build those worlds ran. It also splits "no larger, the chosen node reachable" by whether the node lies on a cycle
through itself, and counts regrowths apart from additions. Its counts start at zero on a resume and are not in the
checkpoint.

`rounds/launch-refusal-fixed.ps1` resumes two worlds on the card from their 10,500 s checkpoints to 12,000 s: the
control with the most tries, `pldr-6203`, and the world with light at each part with the most modules standing,
`plp2-6204`. It runs them twice, as two groups one after the other: first on `plants-build`'s own farm as `plrc-6203`
and `plrc-6204`, then on the probe build as `plrf-6203` and `plrf-6204`. It runs while no other farm and no Unity runs.

- **The probe changes nothing in the world.** A resume on `plants-build` does not repeat its source (`forest-run.md`,
  "What light at each part found"), so each probe run is compared with the plain resume from the same checkpoint
  rather than with its source. Their stats rows are equal on every field but the wall times, the step counts and the
  probe's own columns, and their lineage rows are equal (`scripts/reads/resume-match.py`). If they differ, no count is
  read and the build is looked at instead. This check was revised on 2026-11-07, before the run. The forest-shot
  resume had just shown that a resume on this build parts from its source.
- **Both builds carry the grip fix** (revised on 2026-11-09, before the run). `plants-build` gained the fix for the card's
  grip columns at `3fab5b8e` (`forest-run.md`), which a plain resume on its farm now runs. A probe without the fix would
  read and step held grips differently from its twin. So the probe commit is cherry-picked onto `plants-build`
  `a270a02a`, as `probe/refusal-fixed-grip` `1b220d02` in the same worktree. The two touch no file in common. The
  checkpoints are still the `8676833f` ones, whose held grips carry the stale values; both twins read them alike. The
  fix changed the farm's sources, so both resumes, not only the probe's, are allowed across the source hashes and
  marked cousins. What is
  read and what it decides are unchanged.
- **What is read.** For additions and for plants among them: the share of shape refusals that are "would stand inside
  itself" (D101), "no larger" on a node off every cycle, "no larger" on a node on a cycle, and each other cause. With
  the fix on, no refusal should name a dormant node.
- **What it decides.** If the overlap test carries most of the plants' refusals, the overlap rule is now what limits a
  plant's growth, and it goes to the owner with this count. If "no larger" carries most of them, the module rule has
  another fault, which is agent work under D298. Either way D308's urge waits for the answer.
