# Proposal: a bound on how far a body reaches

*Fable, 2026-09-22 night, after round 44's read (logbook/0113) and round 45's first launch. For
the owner's ruling before round 45 relaunches. Absorbed into DECISIONS.md on ruling, then
deleted.*

## What happened

Round 44 seed 1 grew a leaf that copies itself, and each copy is bigger than the last. Body
7597's photosynthetic node has a self-edge whose scale mutation has drifted above 1, so every
module is about 1.75 times its parent along one axis; its growth gene is indeterminate, so the
module rule kept adding copies while the reserve allowed. At the genome minimum (three copies)
it is a 1.5 m fan; at its module ceiling (seven) the seventh leaf is 14.6 m long, 2.4 m wide
and 2 cm thick, and the body's bounding radius is 21 m in a tank 53 m across
(`scratch/logs/giant-7597.txt`, `scripts/plot-body.py`; the pictures the owner saw). The
record cannot say how many copies the run had built, because no file held module counts until
tonight's build; its own census (0.82 overlapping pairs a body, 99% persistent, where the
minimum crowd gives 0.02) says giants were there.

Two things followed from it, and one is fixed. The solver slowed sevenfold because the contact
grid sized its cell at two of the largest radius, which put every body in every other's
candidate list; the grid now enters a sphere in every cell it covers and is exact at a cell of
two mean radii, bit-identical (CLAUDE.md's gotcha, the regress). The other is the world: a 21 m
sphere that pushes 600 bodies, a crowd shoved to the glass and driven deep (H5's failure in
seed 1: rim quarter 0.48, `cols` 0.85, `upt lim` 23%), and a leaf that shades a fifth of the
tank's surface.

## Why the rules allow it

Nothing prices a part's size. Tissue is paid by volume and a sheet 2 cm thick is nearly free;
light is paid by area and this one is huge. `DevelopmentLimits.MaxPartVolume` is a million cubic
metres, set so that only the arithmetic is protected, on the argument that "the economics forbid
giants" because income scales with area and upkeep with volume. That argument is false for a
sheet, whose area over volume is one over its thickness, and thickness is clamped at 1 cm; the
remark beside `MinPartHalfExtent` already says what is missing, a cost that scales with area.
`maxHalfExtent` (0.4 m) bounds the founder's draw only; the scalar mutation walks every extent
and every edge scale without bound; `MaxParts` and `MaxDepth` bound the count of copies and not
their size. The pre-module world was bounded by `maxRecursiveLimit` 4: a ratio of 1.75 over four
copies is 5.4 times. The module gene lets a node copy itself up to sixteen times: 1.75 to the
fifteenth is four thousand.

## The choices

1. **A bound on the body's reach**, a development guard rail beside `MaxParts`:
   `DevelopmentLimits.MaxBodyReachMetres` (`EVOSIM_MAX_REACH`, 0 = off, so every recorded
   config replays). Development prunes a part whose farthest corner lies farther than the
   bound from the root's origin, with its subtree, and counts it (`PrunedForReach`); the
   module rule's shape test already refuses an addition that prunes more than the body
   already was, so a module past the bound is refused and counted under `ref shape`; a
   founder or a child whose minimum develops past it is a stillbirth, as one over `MaxParts`
   is. **Recommended for round 45, at 3 m**: the campaign's bodies at the genome minimum read a
   p99 radius of 1.7 m and a largest of 2.0 m, so 3 m leaves every recorded body alone and
   stops the fan at its fourth leaf. It is a bound and not a price: it says what the tank is
   scaled for, the way `MaxParts` says what the solver is scaled for.
2. **A cost that scales with area**, an upkeep per square metre of surface beside the one
   per cubic metre. The honest economic answer, and the one the design's own remark names;
   it re-prices every leaf in the world, so it is a new economy and a base round of its own,
   not a change under the mouth's round. Recommended as the next economy change after the
   animal kit, pre-registered on its own predictions.
3. **Modules that do not compound scale**: a copy past the recursive limit is built at the
   scale of the last copy within it. It stops the geometric series but not a chain of
   sixteen equal leaves (up to 24 m at the campaign's extents), so it does not bound the
   body; not recommended alone.
4. **Nothing**, and read the giants as the world's answer. The tank is then a world in which
   one lineage can shade a fifth of the surface and shove the crowd to the glass, the
   contact model (one sphere per body) is wrong for it by construction, and every rung of
   the animal kit is read on a crowd that is being bulldozed. Not recommended.

## What each implies

- Choice 1 adds a tunable: every `config.json` before it is refused by the build (rounds 44
  and 45's first launch included; routine, CLAUDE.md's rule), and a world with the bound on
  is a new realisation of every seed. Build and tests about an hour; 0114's world section
  amended and re-committed before the launch. `simHash` is untouched (Core only).
- Choice 2 is a round of its own, after the kit or in place of a rung, the owner's order.
- Whatever the choice, DESIGN.md's risk table has a row to amend: "the infinitely thin
  sheet" says `MinPartHalfExtent` keeps the arithmetic representable and the light running
  out bounds the body. The giant shows the second half false for a sheet that copies itself:
  its light is not shared with its copies, since a body never shades itself. The row gets
  the ruling's mitigation, and the decision entry cites this file's numbers.
- The contact model stays one sphere per body under any choice; a 3 m body still pushes at
  3 m. Per-part contact is the design question already in front of the owner and is not
  needed for the relaunch once bodies are bounded.

## The question

Relaunch round 45 on the fixed build with choice 1 at 3 m (my recommendation), with choice 1
at another bound, or as the world stands (choice 4) — and whether choice 2 is queued as the
economy change after the kit.

## Built meanwhile, off

Choice 1 is in the tree with its default at 0, so that the relaunch can follow the answer
within minutes either way. The tunable is `DevelopmentLimits.MaxBodyReachMetres`, set by
`EVOSIM_MAX_REACH` and printed in the header as `reach 3 m` or `reach off`. Development
prunes a part whose farthest corner is farther than the bound from the root's origin, with
its subtree, and counts it in `Phenotype.PrunedForReach`; the root is never past it. The
module rule's shape test refuses an addition that would be pruned, before it is paid for,
under `ref shape`. Two tests cover it: a twenty-segment spine cut to the bound, every
remaining corner inside it and the other counters at zero; and an indeterminate leaf that
meets a bound set just past its minimum, its first module refused once with its reserve
untouched and added once the bound is off. With the bound off the tree replays `r45fix-s4`
sample for sample (`runs/r45fixb-s4`, the re-recorded fixture). So choice 4 is the tree as
it stands, and choice 1 is one variable in the launcher.
