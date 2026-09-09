# 0081 — Born at a third of itself

**2026-09-09**  ·  bodies that grow, built in two halves and smoked; the test suite found to be 78% experiment; five constants in front of the owner

Every creature the world has ever held was born at its adult size. The owner's idea of the
night before (`fable-propose-growth.md`) was to let a child be born small and grow, with
three genome dials deciding how small, how many, and how big: the fraction of its own
tissue a parent banks before it breeds, the litter, and the adult scale. This entry is the
build, the smoke, and what the smoke says about the constants the proposal left for ruling.
The rules themselves are in the proposal until it is absorbed.

## The Core half

An agent built it to `scratch/growth-core-spec.md` while round 32 ran. `BirthInvestment`
replaces the endowment in joules, and `AdultScale` multiplies every node's dimensions
before development. A child is developed once at its adult size and then scaled by the
cube root of its body fraction. So the pruning rule judges the adult, and a newborn never
loses a part it would have grown. `World.Grow` moves reserve into tissue at the tissue price and
matter from the body's cell into the body. Both books close by construction. The closure
test steps a grid world with growth, births, deaths, corpses, influx, burial and a rolling
current for 3,000 steps and reads the audit and the matter identity at 1e-6. The
genome format is 5 and refuses 4, so every inoculum and snapshot on disk is refused by this
build, per §9; a lineage birth row now carries `bf` and `as`, the birth fraction and the
adult scale.

The build had findings, and a reviewer added ten more, all fixed before the suite was run
again. Three matter for the ruling below.

- **At the proposal's defaults, breeding is two to five times cheaper than before.** A
  parent used to pay a whole adult body plus an endowment; it now pays half its tissue
  value, split over the litter. Twelve test worlds ran away at their old irradiance and
  had to be recalibrated. `MaximumPopulation` still ends a runaway, but it counts bodies,
  and a body is no longer a fixed amount of biomass.
- **The dials were double-gated.** A perturbation ran under the endowment's rate and then
  a second rate, 0.0064 per birth in effect; it now runs under one gate of 0.08 each.
- **Founders drew no spread.** Every founder was born with investment 0.5; the launcher
  now draws it from 0.25 to 1.0 so that the founding lottery samples the dial.

The full suite passed, 604 of 604, at 11:41.

## The Unity half

The Sim reads a creature's phenotype live for drag, lit area and the bounding radius, and
sets mass and colliders once at birth, so a body that grew in Core alone would have the
drag of its new size and the mass of its old one. A second agent built the resize to
`scratch/growth-unity-spec.md`. Once every `GrowthStepSeconds` of simulated time, every
body whose fraction moved has its collider extents, mass and both joint anchors set from
the scaled phenotype, without rebuilding the articulation. Its drag panels are dropped, so
that the one place panels are built rebuilds them. Mass is recomputed from volume rather
than multiplied in place, so added mass cannot compound. The transforms are not written;
the anchors carry the size and the solver places the links.

Two numbers watch for the failure mode, a body thrown by an anchor change. `resizeJumpMetres`
is the root's displacement between the resize and the next read with no physics step
between, and `resizeStepMetres` is how far a resized root travelled in the metabolic step
after. The first is exact and read 0 over 1,093 resizes. The second cannot see a snap
smaller than the current's drift, 15 cm per half-second at 0.3 m/s, and the root has no
joint, so a child link that snapped to its new anchor would show only there. It read 6 cm,
which is drift. A per-link reading is the instrument to build if a later round's
`diverged` column says the resize is a suspect.

## The smoke

`r34smoke`, seed 1, 600 s at dt 0.02 on worker 7, the round 32 world with the growth
defaults and the founder investment range 0.25 to 1.0. Compile clean, `ended budget`,
`audit` 0.0000% and `mat resid` 0 on every row, `diverged` 0, `stillb` 0.

| t | alive | births | body frac | adult scale | invest | brood | mat here | mat blk |
|---|---|---|---|---|---|---|---|---|
| 100 | 41 | 21 | 0.902 | 1.000 | 0.658 | 1.63 | 0.507 | 0 |
| 300 | 71 | 68 | 0.907 | 0.989 | 0.557 | 2.04 | 0.119 | 1,910 |
| 600 | 155 | 156 | 0.937 | 0.975 | 0.585 | 2.18 | 0.052 | 13,532 |

Round 32's seed 1 at 600 s, the same world at dt 0.01 with bodies born whole: 47 alive, 22
births, `mat here` 0.82, `mat blk` 39. Growth breeds seven times as fast through founding
and strips the matter at the founders' layer within 300 s; 219 conceptions were refused at
the mass floor and 230 growth steps went short of matter. The children are born at a third
of themselves: the birth fraction's median is 0.31 across the 156 births, its range 0.07
to 1.0, and one in forty is capped at the adult body. The adult scale has already drifted
from 1.0 to a range of 0.58 to 1.04. Seven bodies whose parent had a joint were alive at
600 s, which reads as nothing yet at that age but is the column round 34 will watch.

`mat orphan` reads 6.5e-5 units on 6,000, which looked like a broken invariant until round
32's same seed read the same order on 129 rows of 130; the figure has been float noise
since before growth, and the table's rounding hides it. The residual is 3e-7 against round
32's 5e-11, which is growth's extra transfers rounding.

## What is in front of the owner

The design was approved on the night of 2026-09-08. The build turned up five constants the
proposal left open or set without a world to read, and the smoke gives them numbers.

1. **The investment default, 0.5.** Kept as proposed, with the runaway instrument turned
   into a biomass ceiling rather than a body count, since a body is no longer a unit of
   biomass. The smoke's founding is seven times faster than round 32's; a world that
   breeds like that is matter-limited, and it will be read for whether that is a different
   ecology or the same one reached sooner.
2. **One gate of 0.08 for the three dials** in place of the double gate.
3. **Founder investment drawn from 0.25 to 1.0**, so the lottery samples the dial.
4. **The reserve cap.** A share above the adult body is capped and the surplus stays with
   the parent, with the reserve capped beside the body so that a child cannot be born with
   more reserve than body.
5. **`GrowthReserveFloor` 0.1** is twelve to seventeen seconds of reserve at the smoke's
   upkeep, which is a thin buffer; kept, and read in the round.

Nothing launches on these until they are ruled; round 33, the free joint, is next and needs
none of them.

## The suite was 78% experiment

The confirming run took 27 minutes, and the owner asked whether a unit suite should. A
timed run put durations on every test, and three classes held 78% of the CPU seconds.
`CalibrationSweep` took 1,640 s, one of its two methods 20 minutes on its own.
`VertexFieldExperiments` took 950 s. `SnapshotReadbackTests` took 136 s, because it read
every snapshot file under the 11 GB gitignored `runs/` in full before checking the format
of the first row, a cost that grew with every round and competed with five live arms for
the disk. None of the three guards a rule; the experiments' numbers are in the logbook and
DESIGN already. They now carry a `Slow` trait, `core-test.ps1` skips the trait by default
and runs it under `-All`, and the readback test decides staleness from a file's first line.
Every closure and replay guard stays in the default run, and the default run is 592 tests in
58 s beside five arms. CLAUDE.md's claim of "a couple of minutes" was stale by a week and
is corrected.

A note on how the day went. The agent that applied the reviewer's fixes was told to run
the suite last, and it did. Then it waited for the verdict by issuing a no-op shell command
every four seconds for an hour, because a subagent cannot sleep and cannot return without
its result. The owner saw the wall of commands and stopped the session. The rule
that came out of it is in CLAUDE.md: an agent's task ends when its edits are in, and the
caller runs anything long in the background where a completion notice is free.

## Sources

`fable-propose-growth.md` (the rules); `scratch/growth-core-spec.md` and
`scratch/growth-unity-spec.md` (the briefs); `src/Evosim.Core.Tests/GrowthTests.cs`;
`runs/r34smoke/2026-09-09-111229-cf1be697/` (smoke); `runs/r32-s1/` (the comparison);
`scratch/logs/trx/growth-confirm.trx` (the timed suite); logbook/0079 (round 32).
