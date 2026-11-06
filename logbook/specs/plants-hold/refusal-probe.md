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
about volume, so a module dropped for volume shows only as "no larger". Inference until counted: the scaled-down
modules of a repeating node fall under the smallest part volume. The second build, `0e524da6`, splits "no larger" into
dropped for volume and the rest, and its run follows.
