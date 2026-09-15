# Round 39: the floor gets a shape (draft; becomes the entry once 0101's numbers are in)

*2026-09-15, drafted before round 38's read; the baseline figures marked «0101» are filled
from that entry before this is committed as the pre-registration.*

## What it asks

Whether a floor with places makes places in the water. Round 38 put the campaign's world
in a tank four times as wide with the same matter, and the water stayed near uniform:
`det cv` and `mat cv` sat at «0101» after the fields filled, and the eaters boomed to
hundreds on a standing crop they then exhausted, in every seed. D092 gives the floor a
shape (logbook/specs/bed-spec.md): three bands of relief, a tilt along one diameter, the
water pulled through the floor-following map so it slows in the hollows and quickens over
the ridges, the grid masked below the rock, one mesh collider. This round runs round 38's
world on that floor and reads the pockets, and where the bodies sit against them.

## The world

Round 38's launcher with three dials (`rounds/launch-r39.ps1`): `EVOSIM_BED_RELIEF` 1 m,
`EVOSIM_BED_TILT` 6 m, `EVOSIM_BED_SCALE` 0 (a third of the diameter, 7.52 m). Everything
else is round 38's: 400 m², 6,000 units of matter, the streams at 0.1 m/s with the
acceleration force, corpses at 0.005/s, dt 0.01, 30,000 s, five seeds. The floor a seed
gets is drawn from the seed: seed 3's reads 3 hollows and 1 ridge with a range of 1.00 m,
the bands' steepest slope 26° and 36° with the tilt (`r39smoke`); the shallow arc sits
3 m above the mean depth, 27 m below the band the crowd lives in.

## Rules, before scoring

V1: every header carries round 38's tokens and
`bed relief 1 m tilt 6 m scale 7.52 m (hollows n, ridges n, range x.xx m, steepest nn°
bands nn°, bound binds|clear)` with its own seed's numbers; every manifest one `simHash`,
one `coreHash`, one `configHash`, `physicsJobWorkers 0`, the seven `bed*` fields, and
`prereg.json` naming this entry's commit. V2: `audit` 0.0000% and `mat resid` 0 on every
row. V3: `wraps` 0 on every row. V4: every diverged dump has its trace, and a dump whose
reason names the bed is read as the floor guard's, apart from the throws. V5: a manifest
reading `error` or `stopped` is censored and read at its last sample. The entry is not
written until the world has been watched in the theatre, and frames of a live arm are
looked at at about 3,000 and 6,000 s.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| E1 | **the floor changes nothing the matter decides**: births by 1,000 s and `alive` at 30,000 s inside round 38's five-seed range («0101») in 4 of 5 | the timeline |
| E2 | **the water makes pockets**: `det cv` after 5,000 s above round 38's five-seed maximum at the same times («0101») in 4 of 5, and above it at 15,000 and 30,000 s in every seed | `det cv`, `mat cv` |
| E3 | **the sediment gathers where the floor is low**: `floor low %` above the lowest quarter's own share of the disc (about 24% under a 6 m tilt; the read computes it from the map) at 15,000 and 30,000 s in 4 of 5, and the floor stock's lowest decile holding at least twice the highest decile's (`floorStockByFloorDecile`) | the two floor columns, the decile field |
| E4 | **the eaters find the low ground**: the absorptive bodies' median height above their own column's floor below the leaves' at 15,000 s in 4 of 5, read from `positions.jsonl` against the map rebuilt from the config and seed | `positions-read.py` with the map |
| E5 | **the boom and bust is not the floor's to fix**: the eaters' inherited count peaks above 300 and falls below 50 within 10,000 s of the peak in at least 3 of 5, as in round 38 («0101»: the peaks and troughs) | `inherit` at every 1,000 s |
| E6 | **the disc stays mixed on a slope**: the rim quarter 15 to 40% at 5,000, 15,000 and 30,000 s in 4 of 5; `cols` at least 60% of the live columns from 5,000 s; the crowd's centre along the tilt's diameter within 1.5 m of the axis at the three times (a shift toward the deep side is the water's downwelling, toward the shallow the light's) | `p3` over `alive`; the reader with the tilt direction from the manifest |
| E7 | **the rock throws nothing new**: no dump whose reason names the bed in 5 of 5; `diverged` between 0 and 10 per million jointed body-seconds in 4 of 5 | the manifests, `diverged-read.py` |
| E8 | **the pace holds**: wall seconds per 1,000 simulated seconds per living body within 0.7 and 1.6 of round 38's five-seed mean («0101»), and within 1.15 of it in at least 3 of 5 | `run.json`, `pace.tsv` |

Recorded and not predicted: the three-dimensional nearest neighbour against round 38's;
the jointed count's course; stillbirths; `corpses`; the height of the leaves above their
floor; the hollows' occupancy (bodies within a hollow's radius of a hollow's centre, from
the positions and the map, against the hollows' share of the disc).

## The two-sided readings

- **E1 fails low:** the floor costs the world bodies the matter did not: read where the
  matter went (`mat here`, the matter grid's `mat cv`), since a masked cell under the rock
  is a cell the seeded matter never had.
- **E2 fails:** the water does not make pockets at this relief and these speeds; the entry
  reads the streams' speed over a hollow against over a ridge from the field itself
  (Core, from the config) before saying the relief is too small, and the shelf round is
  the next dial rather than a larger relief in the same tank.
- **E3 fails with E2 holding:** the pockets are in the water column and not on the floor;
  the sink is too slow to bring them down in 30,000 s (0.002 m/s is 30,000 s over 60 m).
- **E4 fails:** the eaters sit where the leaves are, whatever the floor does; the pockets
  are not what the eaters' line lives on, and the boom's cause is read elsewhere (E5).
- **E5 fails (no bust):** the floor's pockets carry the eaters through the trough; the
  finding of round 38 is answered by the bed and the entry says so.
- **E6 fails on the centre:** the slope moves the crowd; read the leaves' and the eaters'
  centres apart.
- **E7 fails on a bed dump:** the placer or the collider put a body into the rock, or a
  body was pushed through the mesh; the dump's place against the map says which.
- **E8 fails:** the cost is the physics against the mesh, not the water; the per-part
  probe (`scratch/bed-build/unity/probe/`) is repeated with a mesh collider's cost.

## What the round does not ask

Not the shelf (the tilt raised into the lit band), not the beach, not rocks. Not whether a
stroke pays. Not the light sense (round 40).
