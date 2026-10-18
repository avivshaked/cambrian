# Round 58's hollows on the bed: the design

Written 2026-10-07. The choices in its last section were made by the coordinator under the owner's
delegation of that day, and are recorded in D155 with the bar for a material change. The owner ruled that morning that round 58 is round 57's world with
D131's hollows, the animal kit's rung C2: places on the bed a few metres across that gather sinking snow
and corpses, so that some of the bed is much richer than the rest and a community can form around one.
The design below is from reading the code and the record (an Opus design pass); nothing is built. Marks:
**read** is in the code or the record, **inference** is reasoned from it, **guess** is a number for the
screens to test.

## Why today's bed gathers almost nothing

Round 57's bed has 1.5 m of relief in three bands of cosines over a 13.8° ramp (`BedShape.cs:98-111`,
`545-573`; round 57's config). Its counter finds one to nine "hollows" a seed (`BedShape.cs:310-328`), but
they hold nothing:

- **The dips are open on the ramp** (inference). A dip holds what falls in only if it is deeper than the
  ramp's fall across it, about 1.1 m over 4.4 m. The bands' dips are 0.4 to 0.75 m deep, so they spill
  downhill.
- **Snow gathers weakly** (inference). It sinks straight down its column and never slides
  (`GridField.cs:2015-2064`). Mixing over sinking puts a 10 m scale on its profile, so a floor 1.5 m
  deeper holds only about 1.16 times the snow.
- **Corpses do not gather at all** (read). A corpse stops where it lands and never moves again
  (`World.cs:3065-3159`), so a dip catches only what falls on its own footprint.
- **There is no lee** (inference). The water's speed in a dip is about the flat field's, and a near-neutral
  body with no bed friction cannot be held by a dip.

So shape alone is expected to change little. What gathers food is a closed pit with walls steep enough to
shed, and a rule that moves a settled corpse downhill.

## The design

**The pits.** Each hollow has a flat floor out to half its radius and a wall that rises as a quintic step,
which keeps the water's acceleration continuous, as the shore's fade does. The screen's first values
(guess): 12 pits, rim radius 5 m, depth 1.5 m, walls of about 48°, about 4% of the bed.

**One shape for every reader.** The pits are added terms inside `BedShape`'s own height, gradient and
Hessian, so the contact, the grid, the water, the placer, the divergence guard, the report and the theatre
all read one floor with no code of their own. The floor outside the pits is the same bits. The theatre's
`SnapshotWorld` and `World` call one shared builder. The card inlines the bed, so it refuses hollows, as it
already refuses these rounds.

**Placement.** From a stream of the world seed's own, after the reefs, a margin clear of every cap, the
glass, the shore band and each other. The reefs come out the same bits with hollows on or off. A count
that cannot be placed refuses the run.

**The contact's early-out stays exact.** `ClearOfAnyFloor` (`ContactBed.cs:111-119`) needs the floor's
upper bound and its steepest slope. Pits only lower the floor, so the upper bound holds as it is.
`SteepestSlopeBound` is raised by the steepest wall.

**No lee in round 58** (recommended). The cheap lee, the current faded inside each pit as at the reefs and
shore, would make a ring jet around each pit rather than calm water (inference, from the beach's reading
in `reef-spec.md`). Holding a body in place waits for the anchoring cell.

**Settings, every one off by default** (the nursery's tank included):

| Setting | Env | Default | Screen value (guess) |
|---|---|---|---|
| `BedHollowCount` | `EVOSIM_BED_HOLLOWS` | 0 | 12 |
| `BedHollowRadiusMetres` | `EVOSIM_BED_HOLLOW_RADIUS` | 0 | 5 |
| `BedHollowDepthMetres` | `EVOSIM_BED_HOLLOW_DEPTH` | 0 | 1.5 |
| `BedHollowMaxWaterDepthMetres` | `EVOSIM_BED_HOLLOW_MAX_DEPTH` | 0 (no bound) | 30 |
| `CorpseReposeDegrees` | `EVOSIM_CORPSE_REPOSE` | 0 (never slide) | 30 |
| `CorpseSlideMetresPerSecond` | `EVOSIM_CORPSE_SLIDE` | 0 | 0.05 |

**Tests.** Off is today's floor, by a digest of lattice points over round 57's three seeds, and the founder
digest stays `fe66eefd0bc1bdf7`. On: the gradient and Hessian against finite differences through both wall
edges; the floor never above the base; the contact's fast path equal to the exact one on the walls; the
slope bound above every wall. The water stays free of divergence with no flux through a wall. A corpse on
a wall ends on the floor, none moves outside a pit, and the books close. Placement is deterministic and
refuses what it cannot place. The digest is the same at 1 and N threads. Last, round 57's launcher with
hollows off replays `r57-s2`'s rows for 600 s byte for byte.

## Cost

About 3 agent-days: the pit term and its bounds, the placement, the slide rule, the readings inside the
pits, the tests and the re-recording (`pfix22`, `r58fix-s4`, the nursery's configs, `build-readings.md`).
The step cost is expected to be small (guess): no new cosines, one bucket lookup a bed sample, and a
gradient a step for each corpse still sliding.

## The screens, after round 57 and the card's first slot

Seed 2, 10,000 s each, one at a time on half the machine, about 4 to 6 hours each (guess).

| Run | What it asks |
|---|---|
| H0 | No run: `r57-s2`'s own rows, which the hollows-off replay shows the new build reproduces |
| H1 | 12 pits, radius 5 m, depth 1.5 m, no sliding: shape alone |
| H2 | H1 with corpses sliding above 30°: the catch |
| H3 | Scale, chosen by H2: 24 pits, or 8 of radius 8 m and depth 2 m |

Each is read for the corpses and their joules per square metre inside the pits and outside, and for the
floor's stock in both. It counts the bodies within 2 m of a pit's floor by guild, with founders apart,
since D129 lands mouth founders at corpses by rule. It reads births by pit-dwellers, the living count
and guilds against H0, and the pace. The theatre takes one shelf pit and one deep pit, close and from the side, at 5,000 and 10,000 s,
beside the same spot in H0.

## The choices, and what D155 took

D155 took (b), steep walls, no lee, the 16 to 30 m band, the pits and the slide as one change, and
D129's placement kept with founders read apart. The options as they were set out:

1. **How a hollow gathers food.** (a) Shape only: no new rule, and probably little effect. (b) Corpses
   slide down slopes steeper than a repose angle, then lodge (recommended): a new rule, but on round 57's
   floor it acts only on the pits' walls, since nothing else is steeper than about 27°. (c) Whatever
   settles inside a rim lodges at its floor: the strongest catch and the least physical. Snow creeping
   downhill is a further option, about a day more, not recommended for round 58.
2. **Steep walls.** Sliding on a 13.8° ramp needs walls of about 45° or more, steeper than the bands'
   30°. Gentle walls catch nothing.
3. **A lee.** None now (recommended), or a half-day probe first and two to three days to build, at the
   risk of a ring jet around each pit.
4. **Where.** Anywhere outside the shore band, or within a band of water depth near the crowd, about 16 to
   30 m (recommended).
5. **One change a round** (D079): whether the pits and the slide rule count as one change. The agent's
   view is that they do, since the slide acts only where the pits make slopes for it.
6. **Mouth founders** (D129). They land at corpses, so they will start in the piles. Keep that and read
   founders apart, or place them elsewhere.
