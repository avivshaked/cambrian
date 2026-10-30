# r63s's G5 read

The shape screen is pre-registered in `logbook/specs/r63s-prereg/predictions.md`. It ran the arm "on" on seeds 6202 and
6203: `r63c-b`'s world with D233's family listed, D229's shaped added mass on and the form walk at 0.08. It was one card group
from `scratch/wt-shape-land` at `d03a19b2`, 24,000 s each, in `runs/r63s-on-{6202,6203}/2026-10-29-220442-f73b299d`. Both
worlds reached their budget, in 149 and 151 wall minutes. The manifests carry config `f73b299db0a24445`, farm
`dc035ea9779311e1`, Dynamics `6a5a06a4d8eeb78b` and Core `811b6e062fe6d263`, the pre-registration's values. The arm
"off" is `r63c-b`'s own runs, `runs/r63c-b-{6202,6203}/2026-10-29-123625-716cd9ee`. This folder is the read, which
`scripts/reads/shape/r63s-read.ps1` makes again from the runs.

- `g5-6202.txt` and `g5-6203.txt` are `scripts/reads/shape/g5-read.py` at `--every 1`: gates 2 and 3, and the reported
  lines.
- `g2-survive.txt` is `scripts/reads/bloom/g2-read.py` with `--survive-from 12000` over both "on" worlds. Only its gate 1
  is G5's. Its gates 2 and 3 are G2's, which this screen does not judge.

## What it says

The arm passes every gate on both seeds, so by the pre-registration the family and the shaped added mass go into G7's
round (D254).

| Seed | Gate 1, survive | Gate 2, family share of jointed bodies | Gate 3, moving-grazer speed on/off |
|---|---|---|---|
| 6202 | pass | 0.193 (141 of 732) | 0.978 (0.2474 against 0.2529 m/s) |
| 6203 | pass | 0.180 (109 of 605) | 0.984 (0.2617 against 0.2658 m/s) |

- **Grazers survive.** The window means from 12,000 s are 64, 68, 99 and 85 on 6202, and 35, 60, 88 and 83 on 6203,
  and D222 passes on both seeds. The sensitivity at a floor of 40, not part of the gate, fails on 6203 in its first judged window
  (mean 34.5).
- **The shapes are reachable and they last.** About a fifth of jointed bodies carry a family limb at 24,000 s. The arm
  "off" carries none, as it must.
- **Swimming costs under 3 %.** The ratios sit well inside the noise the pre-registration measured between two seeds of
  one world (0.863).

## What it does not say

- It does not say that selection favours the shapes. Few grazers carry a family limb: 22 of about 3,400 with moving pairs on 6202,
  and 40 of about 2,900 on 6203. Most family limbs are in other bodies. On 6202 the carriers swim slower than the rest
  (0.136 against 0.248 m/s), and on 6203 they swim level (0.264 against 0.262). The form genes have barely walked: the
  median family limb sits 0.108 and 0.000 range log widths from the member it entered at. That mutation alone carried
  the share past 10 % fits all of this, and is an inference.
- Nor does it give the fin law's cost in pace. "On" ran 1.32 times as fast as "off" on both seeds. The two sat in different groups,
  two worlds against `r63c-b`'s four, so the ratio says nothing about the law.
- Its "jointed" is not the stats column's. The read's count (732 and 605) is bodies whose genome grows a non-root node on a
  joint that is not Fixed. That is the pre-registration's definition, and it is wider than `stats.jsonl`'s `jointed`
  column (238 and 156 at the end).

## Seen in the theatre

I drew both worlds at 12,000 and 24,000 s from their own snapshots and recorded poses on 2026-10-30, from the side,
the top and a corner. The theatre was main's at `9235a0fc`, on a fresh worker 6 in `scratch/wt-shape-land`
(`scratch/shape-build/g5/theatre/snap-r63s.ps1`). Three of the frames are in `logbook/images/r63s/`. Every frame says
it is reconstructed, at the recorded size and pose. The markers are coloured by guild: green photosynthetic, orange
absorptive, grey structural or a corpse.

- **Six crowds over the islands, and near-empty water between them, in both seeds.** That is `r63c-b`'s world as its
  read describes it. From 12,000 to 24,000 s each crowd grows and spreads, from 3,031 bodies drawn to 8,254 on 6202
  and from 1,880 to 6,256 on 6203. A seventh crowd fills in at the north-west corner of each world.
- **Every crowd sits in the upper half of the water.** The side views show a slab from the surface down to about
  12 m, and nothing near the bed.
- **The seeds differ in their absorbers.** On 6203 the central island carries a dense orange crowd, which grows from
  12,000 to 24,000 s and reaches lower in the water than the leaves around it. On 6202 there are only a few orange
  markers anywhere.
- **The family cannot be seen.** At this scale every body is a marker a few pixels across, and the theatre draws a
  superellipsoid part with a box's mesh. Picking out family limbs waits for the skin port.
- Each frame has pose rows with no genome to draw: 468 and 1,341 on 6202, 342 and 988 on 6203. That they are remains,
  which have no genome row, is inference.

## The floaters

The runs' `diverged/` folders hold more records in "on" than in `r63c-b`: 36 and 50 against 12 and 26. The coordinator
asked for them to be measured before G7 takes the family in. `floaters.txt` is `scripts/reads/shape/r63s-floaters.py` over
all four runs, and the code read beside it is of `src/Evosim.Dynamics/Fluid.cs` and `Contacts.cs` at `9235a0fc`.

- **No living body diverged.** Every record in all four runs is a remains piece. Every one left through the top of the
  world, its root more than 25 m above the surface and rising at 0.4 to 4.9 m/s when the guard caught it.
- **The records are twins.** Half of each run's records share their second with the one before. I checked one pair in
  each arm: `11518` and `11519` in "on", `27463` and `27464` in `r63c-b`. Each is two pieces cut from one body at one
  second, with the same volume, position and velocity to the last digit. "On" lost 18 and 25 pairs, and `r63c-b` 6 and 13.
- **The pieces come mostly from family bodies in "on".** A piece has no genome row, so the proxy is its source body's
  root shape. Of the remains cut from superellipsoid-rooted bodies, 32 of 788 and 50 of 912 diverged (0.041 and 0.055). From box
  roots the share is 0.002 and 0 in "on", and 0.005 and 0.006 in `r63c-b`. Sphere roots give 0.017 and 0 in "on", and 0
  and 0.041 in `r63c-b`. So the share is not peculiar to the family: one sphere lineage in `r63c-b` reached it too.
- **Nothing in buoyancy or drag treats the superellipsoid apart.** Buoyancy is one signed term per link, scaled by the
  link's mass and the whole body's volume. The waterline is a point test on the link's origin height, with no
  part-submerged fraction for any shape, and remains take no lift. A family part differs only in its volume, its mass
  and the drag panels its measured geometry gives it. Above the surface these worlds pull a body down by its full weight
  (`surfaceRestoringFraction` is 1), so nothing in the water's laws can carry one 25 m into the air.
- **What lifts the twins is the pair contact.** Each body sums its own contact force against every neighbour it
  overlaps. Where two centres lie closer than 1e-9 m, the normal falls back to straight up, `Vec3.UnitY`, at
  `Contacts.cs` lines 987-989 for the body's sphere and 1212-1214 for a link's (these worlds use the second). Each twin
  finds the other at its own centre, so both are pushed up and neither apart. `ContactLaw.PairPush` caps a step's push at
  the reduced mass times `MaxSeparationSpeed` (1 m/s) over the step (0.01 s). Twins never separate, so their approach
  speed stays 0 and every step grants the whole allowance: about 50 m/s² each, five times gravity, in the water and in
  the air, against which only drag holds. The push repeats until they leave the world. That this is the whole of the launch is inference: it fits the
  twins' identical states and the steady rise, and no twin was traced step by step.
- **Why family bodies make more twins is not known.** A body cut into pieces gives coincident twins when two of its
  parts sit at one place. One of the launched genomes, a two-part superellipsoid leaf, grows from a self-recursive,
  reflected edge. That such lineages favour coincident parts, rather than the shape doing it, is a guess.

The count is small: 1.9 % and 2.2 % of the remains founded in "on", against 0.4 % and 1.3 % in `r63c-b`, and no living
body, and it moves no gate. The contact fallback is a
defect older than the family, and it is the solver's, so a fix is a world rule's change for the owner. Pushing coincident
pairs apart along a fixed horizontal axis by id order would be one such fix.

## The predictions

- **Gate 1 holds on both seeds:** right.
- **Gate 2 passes at 15 to 40 %:** right, at 18 and 19 %.
- **Gate 3 passes at 0.85 to 1.1:** right, at 0.98 on both.
- **The form genes walk less than a third of their log width:** right, at a median of 0.11 and 0 for family limbs. The median for every grown family node is 0.13
  and 0.14.
