# A sense of others: rung B's draft for the owner

*Draft design spec, 2026-09-22 evening, written by the agent from the animal-kit proposal's
rung B (`fable-propose-animal-kit.md`) under the owner's autonomy answer of the same day,
which lets the agent draft this and has the owner read it before anything is built.
Round 46 in the ruled order, after the mouth (round 45,
`mouth-spec.md`). Everything here is inside D020 (no bearing to another body); the one
place the draft says D020 might have to be reopened is marked as such.*

## What it is

A body can smell food (`Chemical`, round 23's perception build) and its own hunger and
the water, and nothing about other bodies until they touch it. Rung A gives it something
to kill and something to scavenge, and a killer or a scavenger that finds its meal by
drifting is a lottery ticket in the sense `NeuronInput.cs` uses of the absorptive cell
before smell. Rung B adds two senses, both already declared in the genome's sensor enum
or shaped like one that is: a scent of bodies, carried by the water, and the eyespot.

## B1: the scent of bodies

1. **The field.** A third scalar field on the grid beside the marine snow and the spent
   matter (`GridField`, one more instance with its own stock), in scent units, carried by
   the same conservative transporter, stirred at the same mixing, masked to the same disc
   in a tank. It has no matter in it: nothing in either book moves when it is deposited or
   decays, and the audit and the matter residual are untouched by construction.
2. **The source.** Every living body deposits into its cell at
   `ScentPerJouleBurnt` × its metabolic burn that step, so a large or busy body smells
   more and a corpse smells at its decay rate (`CorpseDecayPerSecond` × its charged
   joules × the same constant), which is what makes a scavenger's trail. A leaf that
   burns little smells little; whether plants should smell at all is question 2 below.
3. **The sink.** Every cell decays at `ScentDecayPerSecond`, first order, so the field's
   reach is `sqrt(D / k)` in metres for mixing `D` and decay `k`: at the campaign's
   mixing (2 m²/s) and a decay of 0.02/s the e-fold is 10 m, a body-length gradient
   readable across the tank. The tunable sets the reach and nothing else, and the ledger
   does not see it.
4. **The channel.** `SensorChannel.Scent = 10`, appended after `Depth` in enum order,
   answered at every part as the cell's density squashed by `x / (x + k)` with
   `ScentHalfScale` as `k` (the `Chemical` squash, for the same reason: the field spans
   decades and a linear scale is blind in one of them). Gated into the pool by
   `SenseScent` (`EVOSIM_SENSE_SCENT`) after `Flow`, so a run with it off replays the
   record draw for draw, as every sense knob does. Two parts a body-length apart read a
   gradient, which is D020's route to a direction without a bearing.
5. **One scent or several.** One field for all bodies in this draft. A per-guild split
   (leaves, eaters, corpses) would be three fields at three times the cost, and a round
   on one field says whether the split is worth asking for: if eaters follow the field
   into crowds of eaters, it is.
6. **What is recorded.** The report gains `scent J` (the field's total, in scent units
   despite the column's habit), `scent cv` beside `det cv` and `mat cv`, `scent %` (the
   share of the living with a `Scent` input); `stats.jsonl` the total and the deposited
   and decayed amounts per window; the header `scent per=.. decay=.. half=..`; the
   checkpoint the field's stock.

## B2: the eyespot

7. **The channel.** `SensorChannel.Photo` is declared (index count 3, "Milestone 6") and
   unanswered. This rung answers it with one index of the three: irradiance at the part,
   as the light field already computes it for a leaf's income, shading by bodies overhead
   included, squashed against the surface irradiance so it reads 1 in open water at the
   top and falls with depth and with shadow. Indices 1 and 2 read zero until a direction
   is given to them, and the draft does not give one. Gated by `SensePhoto`
   (`EVOSIM_SENSE_PHOTO`), appended after `Scent`.
8. **What it buys.** A body can hold a depth by light where `Depth` gives it the world's
   coordinate, and it can notice a shadow, which is the first thing a body overhead does
   to a body below. Nothing about it needs a field, so its cost is the light sample the
   leaf already pays for.

## The tunables

`ScentPerJouleBurnt`, `ScentDecayPerSecond`, `ScentHalfScale`, `SenseScent`, `SensePhoto`;
all in `RunConfig` and its hash, refusing every earlier config per §9. With both senses
off and the scent constant at zero the world is round 45's world, and the field is not
allocated.

## The cost

One more field on the 1 m grid, at the cost of the detritus today. The world's step was
11 to 14% of the wall at round 40's crowd and 83 ms a step at founding on round 39's
2,200 m² grid. A third field is therefore about a tenth more wall on the Unity farm, and
less on the .NET farm, where the grid is a smaller share. The eyespot costs nothing
measurable.

## Questions for the owner, before this is built

1. **Should a corpse smell more than a living body?** The draft ties scent to burning,
   which a corpse does at its decay rate and a living body at its upkeep; a fresh corpse
   of a large body then smells about as much as the body did alive. Real carrion smells
   more than the animal. A `CorpseScentMultiplier` above 1 would say so; I would start
   at 1 and read whether scavengers find corpses at all.
2. **Should plants smell?** Under the draft a leaf smells in proportion to its upkeep,
   which is small but not zero, so a grazer can follow it. If the owner wants grazing to
   be found by touch alone, a per-cell-type scent factor with leaves at zero does it, and
   it is the same table shape as the cap table in `mouth-spec.md`.
3. **One field or three.** Rule 5's answer is one; the owner may want the split from the
   start so that a round reads guild-following without a second round.
4. **Where D020 might be reopened.** A field at 1 m cells cannot resolve a neighbour at
   half a metre, so schooling and station-holding beside another body are not reachable
   on scent. If round 46 shows bodies following gradients and never holding station, the
   next sense is a short-range neighbour offset in the part's frame, which is a bearing
   and outside D020. Not proposed; named so the wall is known.

## Round 46's predictions, to pre-register on the build

Round 45's world, three seeds, 30,000 s, both senses in the pool. K1: a `Scent` input
appears in an eater lineage and its share of eaters rises above 0.3 by 20,000 s in 2 of 3.
K2: eaters with a `Scent` input take more per corpse (`unitsEaten` per body) than eaters
without in the same window, in 2 of 3. K3: the field's `scent cv` reads above the
detritus's, because bodies cluster and snow does not, in 3 of 3. K4: a `Photo` input
appears and the depth spread of bodies carrying it is narrower than of those without, in
2 of 3. K5: the books closed, `diverged` 0, and the world's step within 15% of round 45's
per metabolic step.
