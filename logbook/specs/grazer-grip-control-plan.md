# The gripping grazer control: a plan

*2026-10-13, by the speed agent, at the coordinator's request. It is a plan and nothing in it has run. It repeats
`grazer-control-spec.md`'s control with grazers that grip a reef cap at the leaves' depth (HANDOFF's third grazing
track). Every number says whether it is read from code or config, computed, measured or guessed.*

## The question

Round 58's control found one way for a mouth to meet a leaf (`grazer-control-spec.md`, "What the runs showed").
The mouth holds still at the leaves' depth, and the leaves drift onto it. Of 40 brainless grazers, the only killers were a
body resting on what is likely a reef cap at −2.45 m and one in the shallows. This control asks whether a grazer
that grips a cap at that depth kills, eats and outlives its unfed span. If its line lives, its genome is planted
into a world for a longer run.

## The world

Round 57's world, `rounds/env-r57.ps1`, seed 1, founded on `r59-anchor`'s build and run on the card to 12,000 s.
That is round 58's world (`rounds/r58-launch.ps1` runs `env-r57.ps1`) with round 59's pieces, as `r59-build/smoke.ps1`
sets them:

- The drag on turning is on (`EVOSIM_TURNING_DRAG=spin`, `EVOSIM_SPIN_DRAG=0.5`, D161).
- The touch of the rock is on (`EVOSIM_SENSE_BED_TOUCH=1`).
- The anchoring cell is in the table (`EVOSIM_ANCHORING=1`), at the bud weight D165 starts it at, which the
  pre-registration names.
- The leaf smell is off. The owner rules its form after the nursery's arms (D167), the card's fill of the leaf field
  waits for its timing, and a brainless grazer cannot smell.

Round 58's own checkpoints cannot be used. This build reads a version-15 checkpoint only in a world without the
anchoring cell (read from code, `WorldState.PreLeafStateVersion`). A grazer's genome is valid only where the table
holds the cell, so the world is founded afresh.

It is a new realisation of the seed, and round 58's world is not its baseline. The arms are read against this world's own run without
grazers, never against `r58-s1ir`.

## The bodies

The grazers are hand-built, format 12, in `inocula/grazer-grip/`.

**`grazer-grip.json`** is `grazer-control/grazer-plate-late.json` with one part added. A 0.2 m cube of the anchoring
cell sits under the plate on a fixed joint, present at birth, so the plate faces up over the cap. Everything else is
plate-late's:

- It has attack 1 and intake 0 when planted. So the placer honours the asked depth, where a body with intake is
  set by the corpses (read from code, D129).
- A 0.2 m consumer cube at attack 1 and intake 1 is added as a module at its first growth step.
- Its reserve margin is 600 s, so it does not breed before it has eaten.
- Its investment is 1.3, so it is planted whole with the 200 J purse.

The cube's numbers, computed from the cell's settings in `r59smoke-t16`'s config:

| Quantity | Value | How |
|---|---|---|
| Breaking force at full strength | 2,320 N | tenacity 58,000 N/m² times (0.008 m³)^(2/3) |
| A grip's build price | 0.12 J | 5e-5 J per newton |
| Upkeep | 0.02 W | 2.5 W/m³ |
| Drag on the plate in a 0.1 m/s current | about 3 N | broadside, drag coefficient 1 |

So a grip holds against the current with three orders of magnitude to spare. It weakens with its hour's decay, and
from 60 s after the reserve falls under 120 s of standing cost (read from config). A starving grazer therefore lets
go near the end of its life. The whole body is 0.049 m³, under the neutral volume of 0.25 m³, so it has no weight in
the water (read from config). Unfed, it lives about 470 s: plate-late's 480 s less the cube's upkeep (computed).

**`grazer-plate-late.json`**, unchanged, is the drifting arm: the same plate and mouth with no anchoring cell.

## Will a planted grazer touch the rock?

This is the plan's main risk. A grip forms on the first physics step on which a link carrying the cell touches the
bed or a reef (read from code, the plan's step 4). The placer sets a founder or an inoculant that lands over a cap's
upper half on the cap's table. It sets it there with a gap of its bounding radius plus 5 cm (read from code,
`PlacementFloor.ClearOfReefs`), so a planted grazer hovers about half a metre over the rock.

Nothing then presses it down. In `r59smoke-t16`, round 57's world with the cell budded at weight 20, 2 of 4,566 links
touched rock at 2,000 s, and none had gripped (measured).

Cap tops lie 2 to 4 m down (depth 3 m, jitter 1 m, read from config). Run A planted at 4 m, the leaves' mean depth
then. `r59g-grip-read.py` reads it as follows (measured):

- 8 of its 20 grazers were set on cap tables, at 2.45 to 3.20 m, and the other 12 stayed near 4 m.
- 3 of the 8 held still for their whole lives with no grip, and the other 5 moved off after 20 to 44% of theirs.
- None of the 3 killed. The one killer was still for a fifth of its life, and still at its first kill.
- At 12,310 s no leaf lay within 1.5 m of a still grazer, against 0.12 per moving one.

So a body set on a cap may stay there without a grip. Whether a grip forms is still unknown, since the record keeps
no grip per body, and the smoke measures it before any long run. The grazers are planted at 4 m as in run A, so D can
be read against A directly.

Holding still may not be enough. In round 58's seed 1, 1,100 to 1,900 leaves stay between 1.5 and 5.5 m at every
1,500 s, 43% of them at 12,000 s. Over the run the crowd's mean depth sinks from 4 m to 12 m (measured). That is
about 0.014 leaves a cubic metre in the caps' band. A plate held against a 0.1 m/s current sweeps about 2 m² of
water, so it would meet a leaf about once in 360 s. A leaf eaten keeps about 82 J, and the grazer burns about
0.4 W, so it needs one about every 200 s (computed, rough).

And the water over a cap barely moves. The current's streams fade near rock, from 0 in it to 1 at 8 m from it
(`ReefGeometry.FadeAt`, `reefFadeMetres` 8). `GrazerCapWaterTests` samples the current in this world on seed 1,
whose 14 caps lie 2.01 to 3.95 m down, every 500 s from 12,000 to 18,000 s (measured):

| Height over the table | Over the caps, mean and most (m/s) | Open water at the same height, mean and most (m/s) |
|---|---|---|
| 0.1 m | 0.0001, 0.0006 | 0.099, 0.99 |
| 0.57 m, where a planted grazer sits | 0.0025, 0.016 | 0.098, 0.90 |
| 2 m | 0.0076, 0.079 | 0.094, 0.51 |

A grazer held on a cap meets the water about 40 times slower than one held in open water would. At the rate above,
that is about one leaf in four hours (computed). Only a leaf that sinks or swims onto the cap reaches it, as run A's
two victims sank onto their killer. So G is likely to read as run A's still grazers did. It is still worth running:
it measures that rather than computing it, and it is the first real world in which grips form.

If too few grip, the fix is a planting rule for the assay alone. It would set an inoculant on a cap whose table lies
near the asked depth, touching it, and leave the world's placer as it is. That adds a tunable to the assay, so every
fixture the build re-recorded is re-recorded again. It is about a day of CPU work (guessed from step 6's 0.75 days),
and it goes to the coordinator before it is built.

## The runs

Every run is alone on the card, under the heat watch, through `card-run.ps1` from a launcher `rounds/r59g-control.ps1`
built on `r58g-control.ps1`. That launcher sets the assay's four settings in its own environment, as the earlier one
did. Group 32, single precision, the snow's transport, the scent's fill, the concurrent mode and the spin at 5,000,
all as round 58 ran.

0. **The founding check**, since a founded card run meets code a resume never reaches (`src/Evosim.Farm.Gpu/CLAUDE.md`).
   It is the CPU accelerator founded with every flag, `-Seconds 30 -CheckpointEvery 15`, off the card at 4 threads,
   then a resume from 15 s, then 60 s founded on the card (`found-short`, `r59g-found60`). The accelerator half
   passed on 2026-10-13 at `de0cdbaf`. Both legs ended by their budgets with no overflow, nvcuda was loaded by no
   process, the resume was the whole run after 15 s, and the config hash is `9820d73fc11e070f`. It took 5 minutes.
1. **Found**, `r59g-found`: founded, 12,000 s, checkpoints every 1,500 s.
2. **Zero**, `r59g-zero`: resumed at 12,000 s for 300 s with no grazers, against the founding run's rows past
   12,000 s by `gpu-accept.py`. It proves the arms' baseline.
3. **The smoke**, `r59g-smoke`: 60 grazer-grip at 4 m, 60 s, checkpoints every 30 s. Its CPU twin is the same 60 s on
   the CPU engine, off the card. The card forms grips by its contact test, which D168 brought near the CPU's but not
   onto it.
   - **The gate**: at least 15 of the 60 hold a grip on a cap by 60 s on the card, and the CPU twin within a few
     of that. Below it, the planting rule above is put to the coordinator, and nothing longer runs.
4. **G**, `r59g-grip`: 60 grazer-grip at 4 m, 3,000 s, checkpoints every 300 s. Its first 600 s also run on the CPU
   engine, off the card, for the grips' comparison below.
5. **D**, `r59g-drift`: 60 grazer-plate-late at 4 m, 3,000 s, checkpoints every 300 s.

Sixty to an arm, against the earlier control's twenty, because one grazer in twenty held still. Sixty grazers draw
about 25 W and bring 12 kJ in their purses into a world of about 3,000 bodies, booked as the earlier inoculants were
(computed).

If G's grippers outlive 470 s and kill, their genome then goes into the world for a long run. Investment
falls to what lets the line breed, with a child that clears the newborn mass floor (computed when it is reached).
The grazers are planted at 12,000 s and run to 24,000 s. That run reads whether the line persists, spreads over the
caps and mutates. Its design is put to the coordinator with G's and D's readings.

## The reads

Per arm, against zero over the same seconds:

- **Grips.** `ckpt-grip.py` reads at each checkpoint how many hold and when each formed. It also gives the depth and
  the cap of each, how long each held, and whether a broken one broke by force, by decay or by the reserve.
- **Kills**, from lineage kill rows whose eater is an inoculant or its descendant: parts killed and bodies eaten,
  split by whether the eater held a grip at the kill. Also the counters `partsKilled`, `bodiesEaten` and
  `corpsesFromKills`.
- **Eating** is `unitsEaten` above zero's.
- **Life** is read from each grazer's death. Starving near 470 s shows a body that never ate, and a life well past it shows one
  that did.
- **Reach**: the eater's distance from each corpse at the kill (`r58g-reach.py`).
- **Breeding**: births from the line. With investment 1.3 this is a weak reading, as before.
- **The leaves at the caps**: the leaves within a metre above a cap's table against those over open water at the
  same depth, from positions. If grippers starve, this says whether leaves reach the caps at all.
- **The world**: `alive` and the leaves' count against zero.

### What each outcome means

- **Grippers kill, eat and outlive the unfed span, and drifters do not.** Holding still is enough, and the long run
  follows.
- **Grippers hold but starve.** The leaves do not come to the caps at that depth. The leaves-at-the-caps reading says
  whether a different depth or a cap nearer the surface would serve.
- **Grippers kill but do not eat.** Reach is the block, which is a world rule. A proposal goes to the owner.

## What the runs found

The window ran on 2026-10-13 at `16ff46d1`, alone on the card, and every run's check held, with no overflow and the
card at most 52 C (measured).

- **Found**, `r59g-found`: 12,000 s at 5.84 times real time, 34 minutes, 2,016 alive at the end. Its leaves lay 4.2 m
  down on average at 12,000 s, 48% of them in the caps' band, against round 58 seed 1's 43%.
- **Zero**, `r59g-zero`: 300 s at 4.37 times real time. Its compare against the founding run is empty, because the
  founding run ends at 12,000 s and holds no rows past it. The resume itself is proven by the founding check.
- **The smoke** failed its gate. No grip formed in the world in 60 s, on the card or on the CPU twin. The two agree
  sample for sample. 10 of the 60 were still at 60 s, each at the height it was planted.
- **The gap.** A read-only probe (`src/Evosim.CubeGap`) restores a checkpoint and gives each anchoring link's
  reef distance less its contact sphere's radius. The nine grazers set on cap tables had their cube 0.271 m over the
  rock at 12,030 and 12,060 s, unchanged to the millimetre. Their nearest link, the plate's sphere, was 0.05 m over it,
  the placer's clearance. The cube hangs inside the bounding radius the plate sets, so it lands about 0.22 m above
  that clearance (inferred from the placer's code).
- **G**, `r59g-grip`, run on the coordinator's word as the natural-contact read: 3,000 s at 4.28 times real time. The
  world's grip joules are 0 at all ten checkpoints, and no body carrying the cell held, formed or broke a grip. That
  covers the founded world's own anchored lineages too, 70 such bodies at 12,300 s. At 12,300 s the cap grazers' plates
  still sat 0.04 to 0.07 m over the rock, and no link touched it. All 60 died by 12,610 s, 59 starved and one of age.
  Their median age was 485 s, and 55 of 60 lived 470 s or more: the unfed span. They killed 8, all while moving.
  The six still for their whole lives killed nothing. Its first 600 s on the CPU engine (`r59g-grip-c`) agree with
  the card sample for sample to 12,200 s, then within a few hundredths of a metre. The CPU twin killed 7 and formed
  no grip either.
- **D** was not run: with no grip in G it would only repeat run A.

So a planted grazer neither touches the rock nor eats. Weightless in water that barely moves, it hovers at the
placer's clearance until it starves (inferred from the above). The control cannot test holding until something sets
the cube on the rock. That is either the assay's planting rule above, or a world rule such as weight in the cell,
which is the owner's. The owner ruled on 2026-10-13 to close the control on this read, with neither built.
The grazers that held still on cap tables for their whole lives already answer its question there: they starved.

## The machine

| Step | Card | CPU, off the card |
|---|---|---|
| Founding check | 60 s on the card, about 2 min | the accelerator, about 10 min at 4 threads |
| Found, 12,000 s | about 35 to 50 min | |
| Zero, 300 s | about 2 min | |
| The smoke, 60 s | about 1 min | its twin, about 5 min at 16 threads |
| G and D, 3,000 s each | about 9 to 12 min each | G's first 600 s, about 9 min at 16 threads |
| Reads | | minutes |

Card times are guessed from two measured paces. Run A stepped at 6.0 times real time with about 4,000 alive on
2026-10-12, and round 57's world from 1,000 s at 3.8 with 3,500 alive on 2026-10-13. In all it is about 1.5 hours of the card, in one or two
windows the coordinator holds. The building is CPU work on logical 16 to 31, about half a day. It covers the genome, a
filtered Core test that develops it and checks the cube is there at birth, the launcher, and the reads' additions.

It runs on the card. The CPU engine steps this world at about 1.1 times real time (measured, round 57 at
47,000 s), so the founding alone would take about three hours there. In the gripping test world the card held the
same grips as the CPU to 200 s. By 300 s it had formed 21 and broken 11, against the CPU's 14 and 3 (measured on the
build before D168, `r59-anchor-build-plan.md`, step 8). So the smoke's CPU twin is part of the gate, and G's first
600 s run on the CPU engine as well, about 9 minutes on 16 threads (computed from the 1.1 pace). Their grips are
compared at 300 and 600 s. If the card breaks grips the CPU keeps, G and D are run on the CPU, at about 45 minutes
each.
