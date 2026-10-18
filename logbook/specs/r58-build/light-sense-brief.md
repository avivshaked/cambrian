# Round 58's light sense: the brief

*This is the brief for the agent that builds the light sense for round 58 and for the nursery's light bench. It covers the sense itself, its wiring, its mutation and its switches. The owner has ruled that round 58 brings in the light sense, that no founder is wired to it, and that mutation can add it through `CentralMutation.AddLink`. The nursery bench needs three arms: no sense, unwired and wired. Line numbers are on branch `round-57` at `1917815` (worktree `scratch/wt-r57`). Drafted 2026-10-06 by an Opus subagent from the code, read-only, and saved as it came, with the processor limit set by the coordinator; the owner ruled both questions at the end on 2026-10-06 (D154).*

## Where you work

- Edit only in the round-58 worktree the caller names. Reach it by absolute path and `git -C`, and never `cd` into it.
- Put notes, logs and anything you make for yourself in `D:\Projects\experiments\evolution-simulator\scratch\r58-build\`. Helper scripts and lists go in `scratch\r58-build\helpers\`. Never write to your session scratchpad, to TEMP or anywhere else outside the project.
- While round 57 runs, use at most 2 logical processors, pinned to logical 16 and 17 (affinity `[IntPtr][int64]196608`, never a 0x literal), and only when the coordinator gives the slot. Build with `-m:1`. Run only the fast filtered tests (`scripts/core-test.ps1 -Filter ...`, and the Dynamics and Farm tests by filter). Never run a full suite, a farm run or a smoke; the caller runs those. Your task ends when the edits and the filtered tests are in.
- Commit nothing. The nursery's code, configs and recipes belong to another agent. Change none of them.

## What "off" means here

With the sense off, every world trajectory must be today's to the bit: stats, lineage, positions, the state digests, and the thread-identity word. The config hash cannot stay the same. `RunConfig.Hash()` writes every tunable's path and value (`RunConfig.cs:3504-3528`), so any new tunable changes every hash, at any value. Loading also refuses a `config.json` that lacks a field. So config files and pinned hashes are re-recorded, as in every build that adds a tunable (`src/Evosim.Core/CLAUDE.md`, "Adding a tunable..."). A recorded digest must never need re-recording. If one does, then off is not off.

## 1. What `Photo` reads

**What the code and design already commit to.**
- `SensorChannel.Photo = 4` is declared as "Photoreceptor triple. Milestone 6" (`NeuronInput.cs:73-74`).
- `SensorChannels.IndexCount` gives it three indices (`SensorChannels.cs:109`).
- Channels are "normalised to approximately [-1, 1]" (`NeuronInput.cs:35`).
- The `Depth` remarks treat Photo as irradiance, a depth proxy "only while the sun is up" (`NeuronInput.cs:168-173`).
- DESIGN §4.4 lists a "photosensor triple" per part (`DESIGN.md:736-738`). DESIGN §5A.5 ties that triple to colour vision, the seeing of other bodies' colour (`DESIGN.md:1783-1785`).
- Nothing in the code reads Photo today. `CreatureSenses.Read` answers it 0 through its default case (`CreatureSenses.cs:364`).

**Recommendation: one reading per part, the irradiance at the part's own position, with every shade photosynthesis sees.** The value is

`I = World.Field.IrradianceAt(partY, patch, partX, partZ)`

This is the same call that photosynthesis makes at the body's centre (`World.cs:3551-3555`). It already multiplies these terms:
- Beer-Lambert depth (`LightModel.IrradianceAt`, `LightModel.cs:569-575`)
- the canopy factor of the part's layer, pooled or per patch (`LightField.cs:415-441`)
- the shade map and the reef caps (`LightField.cs:448-458`)
- `DayFactor`, which is day × season × weather (`LightField.cs:393`, `LightModel.cs:353-389`)

The reading is `Photo = I / (I + k)` and lies in [0, 1). Here k is a new tunable, `LightSenseHalfScaleWattsPerSquareMetre`. `NeuronInput.Index` is ignored, as it is for Energy, Contact and Damage (`CreatureSenses.cs:332-353`).

**Why per part.** Two parts at different depths read different values, so a body gets a vertical gradient from its own shape. This is the rule Chemical and Scent follow.

**Why x/(x+k).** It is the squash Chemical and Scent already use (`CreatureSenses.cs:243, 249`). It reads 0 in the dark, and darkness is a real state here: a reef cap lets through exactly 0 (`ReefGeometry.cs:1219`), and a night at amplitude 1 reaches 0. It never saturates. That gives the property the round needs: at any one depth, a bright day reads above a dull one. The table below is computed for round 57's world (200 W/m², 1/e at 6 m, 25 m deep) at k = 25 W/m², at a day factor of 1 and one weather s.d. either side (×0.71, ×1.29):

| Depth | Dull day | Mean | Bright day |
|---|---|---|---|
| Surface | 0.850 | 0.889 | 0.912 |
| 12 m | 0.435 | 0.520 | 0.583 |
| Bed, 25 m | 0.081 | 0.110 | 0.138 |

**Why k = 25 W/m² by default.** It is about the light at 12 m on a mean day (27 W/m²). The nursery costing puts a leaf's own break-even there. This is a design choice and ⚠ unmeasured (§5A.10), so it is a tunable.

**Why the exposure is left out.** Under `LightByExposure` a part's income also depends on how it faces (`Organism.PartExposure`). The sense reports the light in the water at the part, not what the part catches. `OrientationUp` already gives the facing, and a brain can combine the two. Folding the facing in would confound pose with sky.

**Why not the triple.** A colour triple is vision of other bodies (§5A.5), a much larger build. A triple of light along the part's three axes is the light times the facing, which duplicates `OrientationUp`, triples the ports and dilutes mutation. So change `IndexCount(Photo)` from 3 to 1. One reading then means one port per part, and no dead indices. Dead indices are the fault `SensorChannels`' remarks warn against (`SensorChannels.cs:18-25`). If colour vision is built later, it takes a new enum value. The genome format does not change, because Photo already serialises by name. Whether this re-scoping of DESIGN's "triple" is acceptable is owner question Q2.

**When it is read.** `CreatureSenses.Sample` reads it on every physics step, before the brain, and only when the body's brain mask reads Photo (`Brain.MaskReads`). Each read is a pure read of state that only `World.Step` writes, and `World.Step` runs serially between physics steps: `DayFactor` (`World.cs:2601`), the canopy lists (`World.cs:3397-3444`), `ShadeAt`'s clock, and the static reefs. So the result is the same at any thread count. The sky and canopy are one metabolic step stale, as Contact and Damage are, and the position is current.

## 2. How it enters the brain

- **Pool.** `RunConfig.SensorPool()` appends `Photo` last, after `Ramp`, when `SenseLight` is on (`RunConfig.cs:3403-3441`). Appending keeps every earlier index, and that is the replay rule (`RunConfig.cs:3266-3270`).
- **Ports per part: 1.** `CentralWiring.IndicesOn` reaches Photo through its default case (`CentralWiring.cs:83-84`), so the `IndexCount` change is enough. `AllPorts` (`CentralWiring.cs:124-141`) then lists one Photo id per part instead of three. That is only a lookup table. No recorded genome, inoculum or fixture carries a Photo input: a search of `inocula/`, `rounds/` and `src/` finds none.
- **The founder branch skips it.** `Effective`'s fresh branch wires from `Ports(body, pool)` (`CentralWiring.cs:333-350`), and `pool` is read nowhere else in `Effective`. Change that branch as follows:
  1. Draw the base links from the pool's ports with every Photo port removed. Take the weight scale from that base count.
  2. With Photo appended last, these ports are exactly the off world's ports. So the base draws, their order and their scale are the off world's, bit for bit.
  3. If `options.FounderReadsLight` is set, add a second pass after every output has been wired. In output order and then part order, draw each Photo link with `Chance(FounderLinkShare)` and `Gaussian(0, the base scale)` from the same `fresh` stream.
  4. A wired founder is therefore the unwired founder plus its light links, member by member. The nursery costing asked for exactly that pairing.
  5. The rule covers every output that is wired fresh, not only a founder's: a body's first joint with nothing to copy is wired the same way. Effective's copy branch moves a light link to the new part's port like any other (`CentralWiring.cs:315-320`).
- **Founder genomes.** `World.SpawnFounder` draws `GenomeFactory.Founder` from `Config.SensorPool()` (`World.cs:5180-5181`). Add `RunConfig.FounderSensorPool()`, which is `SensorPool()` without Photo and returns the cached array itself when Photo is absent. Use it there. Under the one brain this keeps founder bodies identical across all three arms. Without it, one draw indexes a longer array, so bodies match only "almost surely" through `Rng.Range`'s rejection sampling (`Rng.cs:96-107`).
- **Mutation draws it with no change to `AddLink`.** `AddLink` draws its source from `Ports(body, pool)` plus the units (`CentralMutation.cs:86-105`), and the World passes the full pool (`World.cs:5145-5150`). Believed share of add-link draws: about 7% on a three-part body with two one-DOF joints under round 57's pool (3 light ports of about 45 sources). That is about 2% of births at `AddLinkChance` 0.3. The nursery costing assumed three indices and 4 or 5 light links a generation; at one index, expect about half that.
- **Legacy brain.** Under `BrainModel.PerNode`, founders draw neurons from `FounderSensorPool()` and so never read light, while `Mutator` can add a Photo input from the full pool. The World refuses `FounderReadsLight` under PerNode. No round needs it.
- **Unity.** Do not add Photo to `SensorChannels.Implemented` (`SensorChannels.cs:52-61`). That list is a promise checked by the Unity Milestone 1 smoke. Contact, Damage, Scent and the drive sense are not on it either. The Unity farm and the theatre's solo creature read 0 for Photo.

## 3. The tunables

| Name | Group | Default | Env | What it changes |
|---|---|---|---|---|
| `RunConfig.SenseLight` | sense | false | `EVOSIM_SENSE_LIGHT` | Appends Photo to the pool. The setter drops the pool cache, as `SenseDrive`'s does (`RunConfig.cs:3310-3315`) |
| `RunConfig.LightSenseHalfScaleWattsPerSquareMetre` | sense, unit W/m2 | 25 | `EVOSIM_LIGHT_SENSE_SCALE` | The k of x/(x+k). The setter refuses a value that is not finite or not above 0 |
| `CentralBrainOptions.FounderReadsLight` | brain | false | `EVOSIM_SENSE_LIGHT_FOUNDERS` | The wired arm: fresh wiring adds light links |

- **Why `FounderReadsLight` sits in the brain group.** `Effective` already receives `CentralBrainOptions` at every caller, including the nursery's `FreshBrain` (`Program.cs:668`) and `Tank.cs:603`. No signature changes and no caller can forget it. The nursery sets it through a `brain.*` override.
- **The three arms.** No sense: `SenseLight` false. Unwired: `SenseLight` true. Wired: `SenseLight` and `FounderReadsLight` both true.
- **The config hash.** Each tunable adds one `path=value|` entry to the hash string, so every hash changes, off included. With the sense off, the pool array is `DefaultPool` or today's array element for element. `Effective`'s base wiring is unchanged and nothing samples light. So the trajectory is today's.
- **Refusals, in the World's constructor beside `SenseScent`'s and `SenseDrive`'s (`World.cs:2324-2350`).**
  - `FounderReadsLight` with `SenseLight` off.
  - `FounderReadsLight` under PerNode.
- **Refusal in the farm.** `FarmGpu.Open` refuses `SenseLight` with the scent and the limb push (`FarmGpu.cs:37`), because the card's kernel has no light.

## 4. The tests

Core, fast:
- **Off is today's pool.** With every other sense on, `SenseLight` false gives today's array element for element. `SenseLight` true appends exactly Photo after `Ramp`. The cache follows the setter. Model these on `DriveSensePoolTests` and `SensorPoolTests.EnabledChannelsAreAppendedAfterDepth`, and update that test's full-pool array.
- **Off is today's founders.** Record a digest of the first 50 founder genomes and their wired brains on the round-57 build, before any edit, and pin it as a constant. Use round 57's settings and the one brain. The new build with the sense off must give the same digest. The unwired arm (`SenseLight` on) must give the same digest too.
- **Unwired founders read nothing.** Over many seeds and bodies, no founder brain has a link from a Photo port, and `Brain.SensorMask` lacks bit 4. The same holds for a body's first joint wired fresh.
- **Wired founders read it.** At `FounderLinkShare` 1, every fresh output has a link from every part's Photo port. The non-light links equal the unwired founder's in order, source and weight. The mask has bit 4.
- **Mutation adds it.** On a fixed body with the sense on, `AddLink` takes a Photo source at about its share of `ports + units` over 10,000 draws. With the sense off it never does. In a small world run for a few hundred seconds, some child carries a light link.
- **Index count.** `IndexCount(Photo)` is 1, and `IndicesOn` gives one Photo port on every part, root included.
- **Refusals.** Each refusal above throws, with a message that names the setting.

Dynamics:
- **The reading against `LightModel`.** Build a two-part body at two known heights. Give `CreatureSenses.Light` a `LightField` over round 57's light: 200 W/m², 6 m, day 1,000 s at 0.5, two-season 6,000 s at 0.6 with a 250 s ramp, weather 0.3, correlation 0.5, seed 57.
  - With no canopy, at summer noon, winter midnight and two days of different weather, `Photo` equals `I/(I+k)`, where `I = 200·exp(y/6)·DayFactorAt(t)`. Equal means bit for bit, or else the build states the ulp and why.
  - On the same day factor, a bright day reads above a dull one at the surface, at 12 m and at the bed.
  - The deeper part reads lower.
  - A layer contributed above reads through the canopy factor.
  - A part under a reef cap reads 0.
  - A shade map is applied at the part's own column.
  - A non-finite part reads 0.
  - A body whose mask lacks Photo samples nothing.
- **Thread identity.** A world of wired bodies is identical at 1 and 4 threads.

Farm:
- **Checkpoint fidelity with the sense on.** Use wired founders in a world crowded enough that the canopy factor is below 1. Restore at a checkpoint and step on; the result must equal the unbroken run. Without the canopy save in section 5 this test fails (believed): after a restore, the canopy lists are empty until the next `World.Step`, so for one metabolic interval the physics steps read the unshaded light (`LightField.cs:177-183, 415-420`).
- **Header and env.** `EnvBinding` carries the three settings, and the header names them.

The caller runs the long checks: `-All`, the Dynamics and Farm suites, and a recorded round-57 run relaunched with the sense off. That run should match its stats and lineage to the bit to 6,000 s, as r57-s2 did on round 57's build.

## 5. What changes, what it costs, and what is re-recorded

**Core**
- `Genome/NeuronInput.cs`: the Photo summary (73-74), the class remarks (40-41) and the Depth remark (168-173). Photo becomes one reading of the light at the part.
- `Brain/SensorChannels.cs`: `IndexCount` drops Photo from the group of three (109), and the remarks change.
- `RunConfig.cs`: `SenseLight` and its field, `LightSenseHalfScaleWattsPerSquareMetre`, `SensorPool()`'s count and append (3403-3441), and `FounderSensorPool()`.
- `Brain/CentralBrainOptions.cs`: `FounderReadsLight`.
- `Brain/CentralWiring.cs`: `Effective`'s fresh branch and the second pass (333-350). Update the `Ports` summary (95-97) and the `CentralBrain.cs:73` remark.
- `Ecosystem/World.cs`: `SpawnFounder` uses `FounderSensorPool()` (5180-5181), and the refusals (near 2324-2350).
- `Environment/LightField.cs`: write and restore methods for `_factor`, `_factorByPatch`, `_deepFactor` and `_deepFactorByPatch`. Update the "Not a sense" remark (478).
- `Ecosystem/WorldState.cs`: write those factors after `DayFactor` (261-262) and read them after it (463), only when `Config.SenseLight` is on. This follows the conditional-write precedent at 248-256, so an off world writes today's bytes and the layout version stays.

**Dynamics**
- `CreatureSenses.cs`: a `Light` property (`LightField`; null reads 0, as `Scent` does), `_readsLight`, a per-part `_light` array and the half scale.
  - Sample's early return (187), the non-finite branch (204-220), the per-part loop and a `Read` case.
  - Use the body's patch, clamped as the smell's is (192-193).
- `SolverConfig.cs`: carry the half scale where `ChemicalHalfScaleJoulesPerCubicMetre` is carried (213, 282-283).

**Farm**
- `Reconcile.cs`: set `solver.Senses.Light = World.Field` beside the nutrients and the scent at both places (214-216, 366-368). Answer it whether the sense is on or off, as Chemical is (`RunConfig.cs:3216-3223`). An off world is still today's, because no genome reads Photo.
- `EnvBinding.cs`: the three settings beside `EVOSIM_SENSE_DRIVE` (420, 719, 1431).
- `Report.cs`: `light` in `SensesToken` (559-585), and a header token for the half scale and for wired or unwired founders.
- `FarmGpu.cs:37`: the refusal.
- `CheckpointFidelity.cs`: add `CreatureSenses._light` to `FilledBeforeReadIn` (848-857). Skip the `Light` reference as a shared service.
- Recommended instrument: a stats field for the bodies whose brain reads light, with a mean count of light links. Write it only when `SenseLight` is on, so an off run's rows stay today's bytes. Round 58's question is whether selection keeps the sense, and this field answers it.

**Cost per step (believed, to be measured with nothing else running).**
- Each part of a body whose brain reads Photo pays per physics step: one `exp`, a layer lookup, the reef loop, the shade map if there is one, and a division.
- The reef loop is one bounding-circle test per reef, about 23 in round 57's world. An `atan2` and three cosines run only near a cap's edge (`ReefGeometry.cs:341-355, 1202-1211`).
- Estimate: 30 to 100 ns per part-step. That is under 5% of the physics phase with every body reading, and near nothing while few do.
- A body that does not read Photo pays one flag test. Births pay one more port per part in `Ports`. A checkpoint gains a few hundred floats.

**Fixtures and configs (re-record, never edit a digest)**
- The config fixture `src/Evosim.Core.Tests/fixtures/r42-config.json` becomes the next `pfix` (round 42's launcher run 10 s on this build), and the hash the Farm tests pin follows it.
- The Dynamics crowd in `RunFixture.cs` becomes the next `rNNfix-s4`, and the joint-damper pair follows it.
- Every recorded run's `config.json` is refused by this build. Record that in `logbook/specs/build-readings.md`.
- No genome format bump, so `inocula/` is untouched.
- For the nursery's agent, not this build: migrate `rounds/nursery/configs/` (`55aab9ae45f61732.json` is round 57's) to the new hash, and re-pin the Nursery tests that name a config. Those tests are `RecipeReadTests`, `RecipeTests`, `RegistryTests`, `RestepTests`, `RestepSwimTests` and `RunRecordTests`. Re-record the room's golden answers (`Golden/answers/recipe.json`, `E2eWorld`, `EpisodeTests`, `ExamPlanTests`).
- `DrawFounders` (`Program.cs:604-605`) should strip Photo as it strips the drive sense, or call `FounderSensorPool()`.
- If round 58 has other builds that add tunables, re-record once, for all of them together.

## What to report

Report:
- the files changed
- each tunable's default
- every refusal's message
- the pinned founder digest, and the build it was recorded on
- the fixtures re-recorded
- the filtered tests, with their counts
- anything the code made you decide differently from this brief, with the reason
- for the nursery's recipes: the exact override paths of the two switches (the nursery has drafted `SenseLight` and `Brain.FounderReadsLight`), and that the World accepts `SenseLight` together with weather and a day cycle, with a test that builds such a world

## The owner's questions, ruled

The owner said yes to both on 2026-10-06 (D154): the reading includes the canopy, with its checkpoint save, and Photo is one reading per part.

**Q1. Does the light sense report the shade of other bodies?**
- **Recommended: yes.** The sense reads exactly the light photosynthesis would pay on at that point: depth, sky, reef caps, shade map and the canopy of bodies above. A body can then sense a crowd overhead as well as the weather.
  - What it costs: the canopy lists must go into a checkpoint when the sense is on (section 5). Without that save, a resumed run parts from an unbroken one.
  - What it means for the record: this is a new kind of information about other bodies, which so far only contact, damage and scent give.
- **The alternative: the water's light without the crowd.** The reading is depth × sky × caps × map.
  - What it costs: nothing in a checkpoint. It is cheaper and simpler.
  - What it changes: a body under a dense canopy reads more light than it earns, so the sense and the income disagree exactly where competition bites.
- **The exact question:** should round 58's light sense include the canopy shade of other bodies, yes or no?
- **What proceeds without it:** everything except the canopy term and the checkpoint save. Ruling no removes one factor from the read and drops the save.

**Q2. May `Photo` become a one-reading light sense rather than DESIGN's "photosensor triple"?**
- **Recommended: yes.** One reading per part, the light at the part. Colour vision (§5A.5), if ever built, takes a new channel.
- **What it implies:** DESIGN §4.4 (736-743) and §5A.5 (1783-1785) are amended to say so, with a DECISIONS entry. Nothing in any run changes, because no genome reads Photo.
- **The alternative:** keep three indices. That would mean three copies of one signal, or a facing-weighted triple that repeats `OrientationUp`. Either way, about two thirds of the sense's mutation draws go to redundant inputs.
- **The exact question:** may Photo be re-scoped to one light reading, leaving colour vision to a future channel?

The build choices that do not need the owner are taken above, with their reasons:
- x/(x+k) with k = 25 W/m², and its alternatives (a log scale needs a floor for true darkness; a ratio clamped at 1 saturates on bright days)
- one port per part
- wired links drawn after the base wiring, so the arms pair member by member
- the switch in `CentralBrainOptions`
- the exposure left out
- the reading taken every physics step
- the stats field written only when the sense is on
