# The nursery at a pace factor: design (D240)

*2026-10-26, the nursery lane's agent, for the coordinator's review before any build. Branch `nursery-pace`, from `pace`
at `9a6d593e`. D240 rules that the nursery trains under every condition set for the world, the pace of life first.
D239 and `logbook/specs/pace-build.md` say what the pace factor k is. This file says what the nursery needs to carry it,
where, and how each part is checked. Nothing here is built.*

## What changes, and what does not

The nursery does not step physics itself. Each tank builds the farm's `Simulation` with a step dt and a count of physics
steps per metabolic step, and `Simulation` builds its solver through `SolverConfig.FromWorld`. On `pace` that path already:
- reads k from the world's config;
- refuses a step count that does not hold 0.5·k physics seconds;
- keeps the solver's clock in world time;
- divides the water's velocity by k and its acceleration by k² at the seam in `Water.cs`.

So the water and the current are converted for a tank with no nursery code at all, provided the nursery hands `Simulation`
the right count. Core, the economy and every rate per world second are untouched, as for the farm.

What the nursery must change is its own clocks. Today it assumes that one physics step is dt of world time: in the step
count, in the watchers' sample times, in the frames' time and in a restep's sample spacing. At k = 1 every one of those
stays exactly as it is.

## The sites

1. **The step count.** `Tank.cs:180` and `LeafTank.cs:382` compute `stepsPerMetabolic = round(0.5 / dt)`. Both take
   `EnvBinding.ResolvePhysicsStep(dt, config.PaceFactor, out steps)` instead, which gives 0.5·k/dt and refuses, by name and
   with the allowed steps, a dt that does not divide 0.5·k. At k = 1 it gives today's count for every dt the nursery
   accepts today. `LeafTank.cs:421`'s fresh simulation for the complement check takes the same count.
2. **The refusal.** `Recipe.cs:681-689` refuses any k other than 1. It is lifted, and replaced by the same
   `ResolvePhysicsStep` check at recipe read, so a recipe whose `setup.dt` cannot make a metabolic step at its k is refused
   by `--check`, not by the first episode.
3. **The watchers' clock.** `EpisodeProbe.PhysicsSeconds` (`TankWatcher.cs:150`) is `Steps × dt`, the clock the poses are on.
   A new member, `EpisodeProbe.ClockSeconds`, is world time between economy steps: `PhysicsSeconds` itself at k = 1, the
   same expression, so the bits are today's, and `Steps × dt / k` (in double) otherwise. Its doc says so.
   `PatchWatcher.cs:220` and `:250` take their sample time from it, so arrivals, follow-ups, time in smell and alive seconds
   are in world seconds, the switch schedule's own clock (`LeafSwitching` already reads `world.ElapsedSeconds`).
   `RestepTests`' allow-list of probe members gains `ClockSeconds`. `PhysicsSeconds` stays, for anything that wants the
   physics clock.
4. **The frames.** `FrameRecorder.cs:336` writes the frame's `t` from `PhysicsSeconds`; it takes `ClockSeconds`, so `t` is
   world time and `drag-read.py` and `split-read.py` give speeds in metres per world second with no change. `episode.json`
   gains `paceFactor` beside `dt`, so a reader can tell.
5. **A restep's sample spacing.** `Restep.cs:408` computes steps per sample as 1 / (rate × dt), so a sample rate is per
   physics second. It becomes k / (rate × dt), and a rate is per world second. 10 Hz at k = 3 and dt 0.01 is 30 steps a
   sample. The integer check stays and names k in its message.
6. **The tank loop and the switches** already run on `world.ElapsedSeconds` (`Tank.cs:329`, `LeafSwitching.cs:305`): no change.
   The purse is seconds of upkeep, a world rate: no change. `TankProfile` counts physics steps for its own timing: no change.
7. **The record.** The nursery's run record and `--check`'s printout name k beside dt and the steps per metabolic step, so
   a batch says which pace it ran at. The world config already carries k, and the config hash and the episode config hash
   already change with it.

Not touched: the room (`Evosim.NurseryRoom`), which reads tanks through the same code and inherits the change; anything under
`unity/`.

## How a recipe carries k and the stroke's price

Through its world, as it carries every world dial. `paceFactor` is a tunable in the config's `physics` group, so a
recipe sets it with `world.overrides.PaceFactor`, or names a config file that holds it. The same goes for the stroke's
price, `WorkCostMultiplier` (`world.overrides.WorkCostMultiplier`, the farm's `EVOSIM_WORK_COST`), and for the thrust arm's
push, `Fluid.LimbPushCoefficient` (the farm's `EVOSIM_LIMB_PUSH`), which `SolverConfig.FromWorld` reads from the same
config. Rung 1 already sets `Fluid.ShapedAddedMass` that way. A recipe at k = 1 with no such override hashes as it does
today. No new recipe field is needed, so the recipe format does not move.

After pace lands, the nursery's configs are re-pinned with `paceFactor` at 1 (the `-w8` files), and the curriculum's
recipes take `"PaceFactor": 3` as an override, and the stroke's price if the screens set one.

## The step at k = 3

D240 says the nursery trains under the world's conditions, and the physics step is one of them. The nursery's recipes run
at dt 0.01. At k = 3 that is 150 physics steps a metabolic step, three times today's compute per world second, so a bench
or a rung costs three times the machine time. dt 0.03 would keep today's cost, but it is a different integration of the
same body. The coordinator ruled on 2026-10-26 that the nursery's dt is the world's: 0.01 now, so 150 steps a metabolic
step at k = 3. If a later screen adopts a coarser step, the nursery follows it.

## Tests

- **k = 1 is today.** The recorded fixtures pass unchanged: `LeafPatchesTodayTests`, `PatchRowsTodayTests`, the biting-world
  digest and every recipe hash pinned in `LeafTankTests`. A new test asserts that `ClockSeconds` and `PhysicsSeconds` are
  the same bits at k = 1, at several steps.
- **Steps.** A recipe at k = 3 with dt 0.01 makes 150 steps a metabolic step, with dt 0.03 makes 50, and with dt 0.04 is
  refused at read, by name.
- **The clock.** A tank episode at k = 3 ends at the recipe's seconds of world time, its last watcher sample's
  `ClockSeconds` is that length, and its frames' last `t` is that length too.
- **Still water: dropped when built.** The design asked for the swimmer plate (`LeafTankSwimmerTests.Plate`) to cover 2.4
  to 3.6 times the distance per world second at k = 3 as at k = 1, swimming 60 world seconds in still water. The plate
  barely swims in a tank. On the runs of 2026-10-26 it moved 0.005 m a world second along its path under a constant
  power of 0.5, and 0.007 m under full power, too little to read a ratio from. The stroke per world second is the
  solver's, which the pace build tests in still water itself (`pace-build.md`). The nursery's share is the step count
  and the clock, which the tests above check exactly.
- **The current, as built.** Four passive boxes (the plate's root alone, neutral in the water) in the tank's streams
  (`CurrentMode.Transport`, 0.1 m/s, a 6,000 s period) move the same mean distance per world second at k = 1 and k = 3,
  within 2%, between 9 and 29 world seconds. It goes through a tank, to check that the nursery's path reaches
  `Water.cs`'s seam. A leaf was not used, because the leaf pools are not tracked, and a tank has no uniform current.
- **The turn column.** `turn` is the mean root spin in radians per world second, the solver's spin times k (the
  coordinator's ruling of 2026-10-26), so every nursery output reads on the switch schedule's clock.
- **A restep at k = 3** with a 10 Hz sample rate records frames 0.1 world seconds apart.
- **The dials.** A recipe with `WorkCostMultiplier` and `Fluid.LimbPushCoefficient` overrides loads, hashes apart from one
  without, and the solver its tank builds carries them.

## What the blades should expect

From the calibration bench's ledger at k = 1 (`scratch/curriculum/bench-read/reading.md`): a blade under full power spends
8.9 W on push work and 0.31 W on upkeep, and dies when about 1,190 J of its 1,245 J is gone, at 129 s. A top under full
power spends 0.67 W of work. These figures are derived, not run. They assume the work billed scales linearly with
`WorkCostMultiplier`, and that at k the work per world second is k times today's, since the body strokes k times as often
and each stroke costs the same. They ignore food.

Each cell is 1,190 J over (work × k × price + 0.31 W), the seconds until the store is gone with no food:

| | work price 1 | price /3 | price /9 | the thrust arm's price 0.5/k² |
|---|---|---|---|---|
| blade, full power (8.9 W), k = 1 | 129 s | 363 s | 916 s | 250 s |
| blade, full power, k = 3 | 44 s | 129 s | 363 s | 664 s |
| blade, own brain (6.0 W), k = 1 | 189 s | 515 s | 1,218 s | 360 s |
| blade, own brain, k = 3 | 65 s | 189 s | 515 s | 908 s |
| top, full power (0.67 W), k = 1 | 1,214 s | 2,231 s | 3,095 s | 1,845 s |
| top, full power, k = 3 | 513 s | 1,214 s | 2,231 s | 2,822 s |

So at k = 3 a driven blade drains three times faster still, 44 s at full power. A price of 1/3 at k = 3 only gives back
today's 129 s, and 1/9 gives 363 s. The thrust arm's price is set together with its push, `EVOSIM_LIMB_PUSH` at 0.3·k²,
which changes the work a stroke does, so its column holds only if the drive stays the same. Inference: even a top runs dry
in about 9 minutes at full power at k = 3 unless it eats, so the starting purse and the food are read again at k = 3.

## The layout at k = 3

The layout (e), with discs of 7 m, 31 leaves, a ring of 15.1 m and a tank of 10,700 m², was chosen at k = 1 under rule 2,
because no disc that met rule 1 held the leaves. At k = 3 the tops should swim about three times as far per world second:
0.129 m/s becomes about 0.39 m per world second. That must be measured at k = 3 on the built nursery, from a frames restep
of the bench. In 150 s that reach is about 58 m, so (e)'s worst path of 24.2 m takes about 63 s, and rule 1 holds with a
wide margin. The drift does not change with k, since the water is the same per world second.

Two readings, for the coordinator:
- keep (e) as it stands, which already passed its fit check, so that crossing is easy at k = 3;
- or widen the ring, so that a directed crossing takes a set share of the half-period again. The glass allows a ring up to
  about 39 m with a 6 m margin. A ring of 25 m gives a worst path of 44 m, about 114 s at 0.39 m/s, with coverage unchanged.
  Each new ring needs its own fit check.

Rule 1's margin should be judged on the speed measured at k = 3, not on the factor alone.

The coordinator ruled on 2026-10-26 for the second reading. First the tops' speed is measured at k = 3 on the built
nursery. Then rule 1 is applied at that speed, together with D236's condition that drift alone misses the new disc within
one period. Drift is unchanged per world second while swimming triples, so keeping (e) may make travel too easy: the ring
is widened as far as rule 1 allows, with coverage at or under 10%, and the fit check runs on the result.

## Order of work

1. The coordinator rules on this design.
2. A Sonnet builder makes sites 1-7 and the tests on `nursery-pace`, edits only.
3. I review, then compile and test in a HOLD window after the pace group ends.
4. After pace lands on main: merge, the `-w8` re-save of rung 1 and the bench with k = 3, a frames restep to measure
   the k = 3 speed, and the layout per k.
