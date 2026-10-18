# The scent field's exact restore (round 57's build)

*Part of round 57's build, 2026-10-06. The gap was found by `--verify-checkpoint` on round 56 seed 2's
30,000 s checkpoint on 2026-10-05 (`logbook/specs/r56-build/verify-checkpoint-r56s2.log`).*

## The gap

`ScentField` is derived and not carried. `World.Step` fills it from the corpses straight after
`StepCorpses` (`World.cs`, the corpse phase). The farm's growth step runs after `World.Step`
(`Simulation.Metabolise`, then `ApplyGrowth`, then `World.ApplyModuleRule`). A module drop there
appends its remains to `_corpses` (`WorldModules.ShedRemains`). The live run's field leaves those
remains out until the next metabolic step's fill. A restore (`WorldState`, after `WEND`) fills from
every corpse, so a resumed run smells them for the physics steps up to that fill and the unbroken run
does not.

## What else touches the corpses between the fill and a checkpoint

A checkpoint is written at the end of a metabolic step, after the farm's growth step, with no physics
step between.

- `ShedRemains` from the growth step: an append. This is the gap.
- `KillDiverged`, from a physics step: an append. It happens before the next metabolic step's
  `StepCorpses` and fill, so it is inside the next fill and never between a fill and a checkpoint.
- Eating a corpse (`ApplyMouth`) and `StepCorpses`' sinking, leaking and compaction: inside
  `World.Step`, before the fill.

So between the fill and a checkpoint the list only grows at its end.

## The fix

- `World` records how many corpses the last fill covered. `Scent.Fill` takes the list and a count, or
  the world passes a prefix. One `int`, set where the fill is.
- The checkpoint writes that count beside the corpses, and the state version goes up by one. A stream
  of the version before has no count and is refused for a world with `SenseScent` on. That costs
  nothing, because round 57's new tunables already refuse every earlier `config.json`.
- The restore fills from the first `count` corpses.
- The world is unchanged. No sum, order or timing that a live run computes moves, so every
  `SenseScent` world replays as before, apart from the checkpoint's format.

The other way to fix it is to refill the field whenever a corpse is added, so that it always holds every
corpse. That would make a resume exact with no format change. But the live run would then smell a
module's remains half a metabolic step sooner, which is a new realisation of every scent world. It is
not taken.

## The acceptance

- A Core test: a world with `SenseScent` on, a corpse appended after the fill (through the module
  rule), checkpointed and restored. The restored field equals the live one cell for cell.
- `--verify-checkpoint` on a round 57 screen checkpoint passes, its twin step included. Round 56's
  checkpoints cannot be read by round 57's build.
- No round 57 seed is resumed before both pass.
