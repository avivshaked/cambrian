# Proposal: light by exposure, so a leaf's angle to the sky is what it earns on

*Fable, 2026-09-23, from the owner's observation on round 45's pictures ("some of the leaves are
not parallel to the surface, but rather the opposite, almost perpendicular to it ... the
amount of energy has to be derived from the exposure to the field, where angles would
matter"). Not built. A world rule, so the owner's to rule on; absorbed into DECISIONS.md on
ruling, then deleted.*

## What the code does now

A part's lit area is a quarter of its surface (`PhenotypePart.LitArea`, DESIGN §5A.1). That
is Cauchy's formula: for any convex body the projected area averaged over every orientation
is a quarter of its surface. The income (`Metabolism`, per part) and the shadow
(`LightField.Contribute`, per body) both use that number, and the silhouette cap (D099) is
the convex hull's surface over four, the same average for the whole body. Nothing about a
part's actual orientation reaches the economy: `World.Observe` takes a body's height or
centre and its work, and the rotation lives in the solver. So a leaf flat to the surface and
the same leaf on edge earn the same and shade the same. The rule was chosen when bodies had
no orientation worth reading (a tiled world, a column each); it is the right average for a
crowd of random orientations and the wrong number for any one body.

## What the recorded poses say

`scripts/reads/tilt.py` reads each body's root part from the snapshot's genome and its
rotation from `poses.jsonl`, and prints the angle between the part's thinnest axis and the
vertical: 0 is flat to the surface, 90 is on edge (`logbook/specs/r45-read/tilt.txt`).

| seed, second | bodies | still at birth rotation | flat (<15°) | on edge (>75°) | mean flat factor |
|---|---|---|---|---|---|
| s1, 5,000 | 1,815 | 0.90 | 0.02 | 0.92 | 0.06 |
| s1, 30,000 | 5,251 | 0.06 | 0.04 | 0.28 | 0.49 |
| s2, 5,000 | 1,928 | 0.36 | 0.37 | 0.20 | 0.66 |
| s2, 30,000 | 6,145 | 0.19 | 0.38 | 0.13 | 0.73 |
| s3, 5,000 | 986 | 0.34 | 0.03 | 0.50 | 0.35 |
| s3, 30,000 | 4,380 | 0.14 | 0.03 | 0.36 | 0.44 |

The flat factor is the mean of |cos tilt|, 1 for a crowd lying flat, 0 for one on edge, 0.50
for random orientations. Two things in it. Early, most of a crowd stands as it was born. A
genome whose thinnest dimension is x or z develops as a vertical sheet, and a neutrally
buoyant uniform box has no righting moment, so nothing turns it. Seed 1 at 5,000 s: 90% at
the birth rotation, 92% on edge. By the end the orientations are near random in two seeds
and lean flat in the third, and the leaves have thinned from a thin-over-thick ratio of 0.5
to 0.1. Selection found thinness, because the income reads surface; it cannot find
flatness, because the income does not read the angle. My reading: the pictures showed
leaves on edge because the world has never charged or paid for the angle.

## The rule proposed

A part earns on, and shades with, its projected area onto the horizontal in its actual pose:
for an oriented box, the sum over its three face pairs of the face's area times the absolute
cosine between its normal and the vertical. Its mean over orientations is the quarter-surface
the code uses now, so a random crowd's total income is unchanged; a flat leaf earns twice
what it earns today and an edge-on one about nothing. The silhouette cap becomes the hull's
projection onto the horizontal in the same pose (the one-sided sum over the hull's faces of
area times the positive cosine), which is the shadow the body casts. Both sides of the light
move together, as D099 required: nothing collects light it does not also deny to what is
below it.

The harness computes the exposure from each link's rotation at the metabolic step; the
solver has every rotation, and the GPU port's design already brings rotations down once a
metabolic step. It hands the world a per-part exposure factor beside the height, as it hands
`PartDamage`. Core's `Metabolism` multiplies each part's lit area by it and the
shadow sums the same product. A tunable, `LightByExposure` (`EVOSIM_LIGHT_EXPOSURE`, header
`light by exposure` or `light averaged`), off by default so every recorded config replays; it
refuses every config written before it, as every tunable does.

## What it implies

- **A new realisation of every seed** when on, and a base round to read it on. Off, the
  recorded worlds replay bit for bit.
- **Orientation becomes a trait under selection, and a pose becomes worth holding.** Today
  a body has no reason to be any way up. With the rule, a leaf pays for standing on edge,
  and the ways to lie flat are the ways the physics offers: a shape whose stable drift is
  broadside up, a body with a righting moment (a centre of mass below its centre of
  buoyancy, which a uniform box does not have and a two-part body can), or a joint that
  holds an angle. The last is the first reason for a joint in this world's record,
  read against round 42's finding that a joint costs and earns nothing.
- **The readings that move.** `mean m/s` and the depth columns do not; `photoArea` in
  `absorptive.jsonl` becomes the exposed area and its column note says so; the ledger
  (`scripts/ledger.ps1`) takes an exposure factor, default 1, and a screen of a leaf at 0.5
  (random) against 1 (flat) says what the prize is before the round runs. The tilt reader
  is the round's instrument, with `mean flat factor` the column to watch.
- **Two-sided reading for its round.** If the flat factor climbs above 0.5 and holds, the
  crowd found a way to lie flat and the entry says which of the three. If it stays at 0.5,
  no way to lie flat is reachable at this founding, which is a reading about the body kit
  and not about the light.
- **The prices.** The rule changes no price; the light per m² is unchanged. The break-even
  leaf of DESIGN §5A.2b is unchanged at random orientation and halves its needed area flat.
- **What it does not do.** No sun angle: the light is straight down, as `LightField`'s
  layers are, and the projection is onto the horizontal. No self-shading within a body
  beyond the hull's cap. No cost to the pose itself.

## The question for the owner

Whether a part earns and shades on its projected area in its actual pose (the rule above),
as a tunable off by default and on in the next base round, or whether the orientation-averaged
quarter-surface stays the world's rule and the leaves' angles stay unread. My recommendation
is the rule: it makes the picture and the books agree, it costs a few dot products a part a
metabolic step, and it is the first thing in this world that would pay a body for holding a
pose.
