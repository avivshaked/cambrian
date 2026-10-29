# The shape screen: do family limbs spread, and do they swim no worse?

*By c0 on 2026-10-29, before any world of it ran, and committed before the screen launches from its commit. It is G5 in
the grazer film's plan (`logbook/specs/grazer-film-plan.md`, D254), after G4, the shape build (D252, D255). G2's world
is `r63c`'s arm B (`rounds/env-r63c-b.ps1`, D259), written `r63c-b` below; its 24,000 s runs are `runs/r63c-b-6202` and
`-6203`, `2026-10-29-123625-716cd9ee`.*

## The question

When a world lists D233's family and turns on D229's shaped added mass, do family limbs take hold in the jointed
bodies? And do the grazers that carry them swim at least nearly as fast as grazers in the same world with both off?

## The worlds

| Arm | Shapes | Shaped added mass (D229) | Form walk (`EVOSIM_FORM_CHANCE`) |
|---|---|---|---|
| off | box, sphere, capsule | off | 0 |
| on | box, sphere, capsule, superellipsoid | on | 0.08 |

- **The world is `r63c-b`, on seeds `6202` and `6203`, for 24,000 world seconds.** These are G2's seeds and length.
  Everything but the three switches is `r63c-b`'s.
- **The arm "on"** is `rounds/env-r63s-on.ps1`: `r63c-b`'s launcher with `EVOSIM_SHAPED_ADDED_MASS = 1`,
  `EVOSIM_SHAPES = 'box,sphere,capsule,superellipsoid'` and `EVOSIM_FORM_CHANCE = 0.08`.
- **The form walk is 0.08 a node,** `ScalarChance`'s value. Each step is `ScalarStdDev`'s Gaussian on a gene's log,
  so the form genes walk at the rate and step of every other scalar gene, and the screen adds no new number. The
  identity sittings used 0.05; the value does not enter what they check.
- **A part enters the family only by `ShapeChance`,** 0.02 a node, which has no launcher binding and is unchanged.
  The founders are `r63c-b`'s and hold no family part, so every family limb in the screen is a mutant's.
- **The arm "off" is `r63c-b`'s own runs on these seeds, not rerun.** With every new switch off, the merged build steps
  `r63c-b` bit for bit (G0, checked on the merged tree against `r63c-b`'s prefix, below), so a rerun would replay them. This is
  `r63c`'s precedent for its arm A.
- **The build** is main at this file's commit. Three branches land there together:
  - G4, the shape build;
  - the nose at `99258173`, with its switch off, since G3 is still open;
  - `remains-farm`, D220's remains in film windows.
  Its farm hash `dc035ea9779311e1`, Dynamics hash `6a5a06a4d8eeb78b` and Core hash `811b6e062fe6d263` are those the
  merged sitting's manifests carry (below), and must appear in the screen's.
- **The launcher** is `rounds/launch-r63s.ps1`: one card group named `r63s` of the two "on" worlds, at group 32, the
  snow's transport on the card, checkpoints every 1,500 s, the heat stop on, cores 8 to 15 at 8 threads, off the
  cores of the nose lane's nursery search, which may run beside it on the processor alone. It runs from
  `scratch/wt-shape-land` at this file's commit, with that worktree's own Release farm.

## Checked before the launch

On the merged tree, at `6f3c4d93` on 2026-10-29 (the shape spec's "As built, the landing",
`logbook/specs/shape-build/spec.md`, with its read in `logbook/specs/shape-build/merged/accept-land.txt`):

1. The kernels regenerate from the merged templates with no diff, and Core, Dynamics, the Farm and the nursery pass.
   `GpuMeanTests` has no result, since it crashes ILGPU's CPU accelerator at any processor count; the steps below
   stand in for it.
2. **The switch-off sitting.** `r63c-b` with every new switch off, at group 32, to 600 s on 6202 and 6203, is bit for bit
   with the prefix of `r63c-b`'s runs: all 60 stats samples, all 601 digest rows, and the lineage byte-equal over 1,661
   and 1,613 lines.
3. **The family sitting.** The arm "on" (config `f73b299db0a24445`) at groups 32 and 64, to 600 s on both seeds, gives
   identical stats and digests, and the lineage is byte-equal over 1,666 and 1,581 lines.

**The reuse of `r63c-b`'s runs as "off" rests on step 2 alone,** as `r63c`'s reuse of `r63b`'s arm A rested on its
switch-off check. `r63c-b`'s launcher sets neither the shaped added mass nor a family, so its runs are the plan's copy
with both off.

## The read and the gates

The read is `scripts/reads/shape/g5-read.py` (`9b2c9240`): gate 2 by `genome-query.py`'s own
`find --jointed-shape` rule, gate 3 by `grazer-motion-read.py`'s pairs, and gate 1 by `g2-read.py`.

| Gate | Pass |
|---|---|
| 1. Grazers survive | G2's survival gate, as `r63c` ran it: `g2-read.py --survive-from 12000`, every window from 12,000 s with a mean of at least 30 grazers and never 0 (D256), and D222 by `r62cf`'s recorded procedure |
| 2. Evolution uses the shapes | in the snapshot at 24,000 s, at least 10 % of living jointed bodies carry a family limb |
| 3. Swimming does not suffer | the median speed of moving grazers in "on" is at least 80 % of "off"'s |

- **A jointed body** is a living body with a grown non-root node whose joint is not Fixed. **A family limb** is such a
  node whose shape is the superellipsoid. Both are read from the snapshot's genomes, as
  `genome-query.py find --jointed-shape superellipsoid` reads them.
- **A moving grazer's speed** is `grazer-motion-read.py`'s horizontal speed over one pose gap, for each pair of frames
  of the same living grazer at or above its `--still` bar of 0.005 m/s. The median is taken over every such pair from
  12,000 to 24,000 s, the windows gate 1 judges. Each seed is read against the same seed's "off".
- **The bar against the noise.** Two seeds of one world already differ: in `r62cf`, 6202's moving grazers swim at
  0.863 of 6203's (the read's smoke test, `--every 10`, `smoke-g5.txt` beside this file). Gate 3 compares
  each seed with itself, which removes the bed and the founders' draw. But after 24,000 s two runs of one seed have
  diverged as far as two seeds, so a ratio near 0.8 is within the noise. The read runs at `--every 1`.
- **The arm passes when it passes every gate on both seeds.** Then the family and the shaped added mass go into G7's
  round (D254). Otherwise the round goes with today's shapes.

**Reported, not gated:**
- the share of family limbs among jointed non-root nodes;
- how far the form genes have walked from the member a part entered at;
- the share of grazers that carry a family limb, and their median speed against the grazers that do not, in the same
  world;
- the pace of "on" against `r63c-b`'s own runs, as a ratio of world seconds per wall second. Inference, to be tested by
  this ratio: the bloom, fat and the energy sense leave the fin law's cost as (c2) measured it. The two sittings ran
  at different times, so the ratio is read with that caveat.

These say whether the shapes spread by mutation alone or by selection. With `ShapeChance` at 0.02 a node and a third
of its draws landing on the family, mutation alone may carry gate 2 past 10 % within 24,000 s (inference, not
computed). So gate 2 shows that the shapes are reachable and survive. It does not show that they are favoured.

## Predictions

These are c0's guesses, written before any run.

- **Gate 1** holds in both seeds, as it did for `r63c-b` (guess). D229's law changes the push of today's shapes as well as
  the family's, so the screen changes how every jointed grazer swims, not only which shapes it may grow.
- **Gate 2** passes in both seeds, at a share of 15 to 40 % (guess, for the reason above).
- **Gate 3** passes, with the speed ratio between 0.85 and 1.1 (guess). The fin law changes every limb's push from the
  first second. The founders were bred under the old law, so the early ratio may sit below 1 and recover as the
  lineages adapt.
- The form genes walk by less than a third of their ranges' log width from where parts entered (guess).

## What would change the plan

- **The merged build's hashes or its switch-off sitting differ.** Stop: the build is not the one this file names, and
  "off" must be run.
- **Gate 3 fails on both seeds while gate 1 holds.** The fin law costs grazers speed. Since D229's law is the law
  that is on, not the family, a further screen with the family listed and the fin law off would separate the two. That
  screen is the owner's to order.
- **Gate 2 passes only because of mutation.** The reported lines show it, and the record says so. The shapes enter the
  round on the gate as written.
