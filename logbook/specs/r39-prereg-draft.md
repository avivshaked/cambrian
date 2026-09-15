# Round 39: the floor gets a shape, in a tank wide enough to slope (draft; becomes 0102 once 0101's numbers are in)

*2026-09-15, rewritten for D093 before round 38's read; the figures marked «0101» are
filled from that entry and those marked «smoke» from the sizing smokes' headers before
this is committed as the pre-registration.*

## What it asks

Two things, and the entry says which prediction is which. **Whether a floor with places
makes places in the water**: round 38 put the campaign's world in a tank four times as
wide with the same matter, and the water stayed near uniform («0101»: `det cv` and
`mat cv` after the fields filled), while the eaters boomed to hundreds on a standing crop
they then exhausted, in every seed («0101»: the peaks and troughs). D092 gives the floor
a shape (`logbook/specs/bed-spec.md`): three bands of relief, a tilt along one diameter,
the water pulled through the floor-following map so it slows in the hollows and quickens
over the ridges, the grid masked below the rock, one mesh collider. **And whether a floor
that rises into the light is lived on**: D093 sized the tank so the tilt runs 30 m across
a 52.9 m diameter, from a shallow arc whose floor sits 30 m under the surface, the bottom
of the band the crowd lives in («0101»: mean depth 18 to 29 m in round 38's 60 m), to a
deep arc at 60 m. The first question is the pockets'; the second is the shelf's, folded
into this round by the owner's sizing (D093, ruling 3), and its predictions are marked
**light** below.

## The world

Round 38's launcher with the tank resized and three bed dials (`rounds/launch-r39.ps1`):
`EVOSIM_AREA` 2,200 m² (radius 26.46 m; every round through 38 ran 400 m² or less),
`EVOSIM_DEPTH` 45 m (new; every round through 38 ran the 60 m default), the founders
drawn over the 45 m, `EVOSIM_BED_RELIEF` 1.5 m, `EVOSIM_BED_TILT` 30 m (a 29.6° ramp under
the 30° cap), `EVOSIM_BED_SCALE` 0 (a third of the diameter, 17.6 m), `EVOSIM_MATTER_BUDGET`
11,000 units (D093's ruling 4 as run: five founding smokes at 600 s and dt 0.02 read 25,
61, 179, 159 and 301 births at 6,000, 9,000, 10,500, 11,000 and 12,000 units against
round 38's smoke's 166, a cliff under 10,500; 11,000 is the smallest that founds like
round 38 with a margin above the cliff). Everything else is round 38's:
the streams at 0.1 m/s with the acceleration force, corpses at 0.005/s, dt 0.01, 30,000 s,
five seeds. The water is five and a half times round 38's. The floor a seed gets is drawn
from the seed: seed 3's reads 2 hollows and 1 ridge with a range of 1.50 m, the bands' steepest
slope 17° and 40° with the tilt (`r39big-m110`'s header).

## Rules, before scoring

V1: every header carries round 38's tokens with `space tank r=26.46 m (2200 m2), depth
45, wall, bed` and `bed relief 1.5 m tilt 30 m scale 17.6 m (hollows n, ridges n, range
x.xx m, steepest nn° bands nn°, bound binds|clear)` with its own seed's numbers; every
manifest one `simHash`, one `coreHash`, one `configHash`, `physicsJobWorkers 0`, the
seven `bed*` fields, `worldDepthMetres 45`, and `prereg.json` naming this entry's commit.
V2: `audit` 0.0000% and `mat resid` 0 on every row. V3: `wraps` 0 on every row. V4: every
diverged dump has its trace, and a dump whose reason names the bed is read as the floor
guard's, apart from the throws. V5: a manifest reading `error` or `stopped` is censored
and read at its last sample. The entry is not written until the world has been watched in
the theatre, and frames of a live arm are looked at at about 3,000 and 6,000 s, the bed
view among them.

## Predictions

Round 38's five seeds («0101»): `alive` at 30,000 s «0101»; births by 1,000 s «0101»;
`det cv` after 5,000 s «0101» and its five-seed maximum at 15,000 and 30,000 s «0101»;
the eaters' inherited peak «0101» and trough «0101»; the rim quarter «0101»; the pace
«0101» wall seconds per 1,000 simulated seconds per living body.

| # | prediction | falsified by |
|---|---|---|
| E1 | **the matter decides the crowd, not the water**: `alive` at 30,000 s between 0.7 and 1.5 times round 38's five-seed mean scaled by the budget over 6,000 («0101») in 4 of 5, and births by 1,000 s at least 40 in every seed | the timeline; `mat blk` and `mat short` against `births` in the first 3,000 s recorded beside it |
| E2 | **the water makes pockets**: `det cv` after 5,000 s above round 38's five-seed maximum at the same times («0101») in 4 of 5, and above it at 15,000 and 30,000 s in every seed | `det cv`, `mat cv` |
| E3 | **the sediment gathers where the floor is low**: `floor low %` above the lowest quarter's own share of the disc (the read computes it from the map; near a quarter under a straight tilt) at 15,000 and 30,000 s in 4 of 5, and the floor stock's lowest decile holding at least twice the highest decile's (`floorStockByFloorDecile`) | the two floor columns, the decile field |
| E4 | **the eaters find the low ground**: the absorptive bodies' median height above their own column's floor below the leaves' at 15,000 s in 4 of 5, read from `positions.jsonl` against the map rebuilt from the config and seed | `positions-read.py` with the map |
| E5 | **the boom and bust is not the floor's to fix**: the eaters' inherited count peaks above 300 times the budget over 6,000 and falls below a sixth of its peak within 10,000 s of the peak in at least 3 of 5, as in round 38 («0101») | `inherit` at every 1,000 s |
| E6 | **the disc stays mixed on a slope**: the rim quarter 15 to 40% at 5,000, 15,000 and 30,000 s in 4 of 5; `cols` at least 60% of the live columns from 5,000 s | `p3` over `alive`; `cols` |
| E7 | **the rock throws nothing new**: no dump whose reason names the bed in 5 of 5; `diverged` between 0 and 10 per million jointed body-seconds in 4 of 5 | the manifests, `diverged-read.py` |
| E8 | **the pace is the grid's**: wall seconds per 1,000 simulated seconds per living body within 1.5 and 3.5 of round 38's five-seed mean («0101»); the smokes read 3.0 at matched founding bodies, the grid at five and a half times the cells, and the physics' share grows with the crowd | `run.json`, `pace.tsv` |
| E9 | **light: the leaves take the shallow side**: the photosynthetic bodies' centre along the tilt's diameter at least 4 m toward the shallow arc from the axis at 15,000 and 30,000 s in 4 of 5, and the leaves within 2 m of the floor on the shallow half at least twice those on the deep half at the same times | the reader with the tilt direction from the manifest; the map |
| E10 | **light: the floor in the light is lived on**: bodies within 2 m of the floor as a share of the living at 15,000 s at least twice round 38's («0101», where the floor is 30 m below the crowd) in 4 of 5 | `positions-read.py` against the map |
| E11 | **light: the eaters go where the leaves are, and the sediment goes the other way**: the absorptive centre along the diameter within 3 m of the leaves' at 15,000 s in at least 3 of 5, so that E4 (above the low ground) and E9 (with the leaves) cannot both hold in the same seed; the entry says which held | the two centres from the reader |

Recorded and not predicted: the three-dimensional nearest neighbour against round 38's
at matched abundance; the jointed count's course; stillbirths; `corpses`; the height of
the leaves above their floor; the hollows' occupancy (bodies within a hollow's radius of a
hollow's centre, from the positions and the map, against the hollows' share of the disc);
the whole crowd's centre along the diameter, which E9 and E4 pull opposite ways.

## The two-sided readings

- **E1 fails low:** the water costs the world bodies the matter did not. Read where the
  matter went (`mat here`, the matter grid's `mat cv`). A masked cell under the rock is a
  cell the seeded matter never had, and a 5 m matter cell in this much water holds about
  what one child costs (D093's context; the 6,000 smoke read 25 births by 600 s against
  round 38's 166, with 6,673 conceptions blocked on matter).
- **E2 fails:** the water does not make pockets at this relief and these speeds; the entry
  reads the streams' speed over a hollow against over a ridge from the field itself
  (Core, from the config) before saying the relief is too small.
- **E3 fails with E2 holding:** the pockets are in the water column and not on the floor;
  the sink is too slow to bring them down in 30,000 s.
- **E4 fails with E11 holding:** the eaters sit where the leaves are, whatever the floor
  does; the pockets are not what the eaters' line lives on, and the boom's cause is read
  elsewhere (E5).
- **E5 fails (no bust):** the floor's pockets or the shallow floor carry the eaters through
  the trough; the finding of round 38 is answered by the bed and the entry says by which
  half, from E4 against E9.
- **E6 fails on the rim:** the slope moves the crowd against the glass on the deep side
  (the water's downwelling) or the shallow side (the light); read the leaves' and the
  eaters' rim shares apart.
- **E7 fails on a bed dump:** the placer or the collider put a body into the rock, or a
  body was pushed through the mesh; the dump's place against the map says which.
- **E8 fails:** the cost is the physics against the mesh or the grid's transport at five
  times the cells, not the bodies; the per-part probe (`scratch/bed-build/unity/probe/`)
  is repeated at the new box and the smoke's step cost is read against the round's.
- **E9 fails:** the leaves do not move toward the light in 30,000 s. Either the current
  carries them faster than the light sorts them, or the shallow floor is no better lit
  than the band they float in (the light reaches 1/e at 12 m; the shallow arc is at
  30 m). The light sense (round 40) is then the round that asks the question the crowd
  could not answer by drifting.
- **E10 fails:** the floor in the light is no more lived on than the floor in the dark.
  Nothing in this world settles on purpose (the anchoring cell is round 43), and the
  round says so rather than reading a shelf into the numbers.

## What the round does not ask

The beach (a floor that breaks the surface), rocks and overhangs wait. Whether a stroke
pays is round 41's question, the light sense round 40's, the anchoring cell round 43's.
