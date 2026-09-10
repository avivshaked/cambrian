# Spike spec: shared space (D076's measurement)

Fable-written spec for an implementing agent. The owner has ruled the direction (D076,
2026-09-05): a world without contact cannot work; creatures will share one volume and
matter will in time be particles, or particles and ambient fields together. Before the
world rules are written, the cost has to be measured. This spike measures it. Read CLAUDE.md
first (the spike gotchas apply: PhysX sleeps undriven bodies, so drive them and report mean
speed; suspiciously good numbers are a broken harness; never run `-batchmode` against a
project the Editor has open). Nothing is written outside the repository.

## The question

How much does one shared volume with creature-to-creature collisions cost against today's
tiling, at the populations the ecosystem actually runs (hundreds to thousands), and at what
footprint? The answer decides the world's footprint, the population per world, and whether
the island model moves forward in the path.

## Where it lives

`unity/Assets/Evosim/Sim/Editor/SharedSpaceSpike.cs`, entry
`Evosim.Sim.EditorTools.SharedSpaceSpike.Run` (namespace `EditorTools`, folder `Editor/`; the
Milestone1Smoke gotcha). Writes `runs/spike-shared-space/<timestamp>/results.csv` and a
`summary.md` (runs/ is gitignored; the findings go into the logbook entry Fable writes). Run
it against worker copy `unity-w6` after refreshing it from the tree (`new-worker.ps1` from
inside pwsh with a real array; verify the printed hash). It must not be run while the main
project or `unity-w2..w5`, `unity-w7` are in use by anything else you did not start.

## What it builds

Real creatures, not proxies: `GenomeFactory` founders drawn with the reference world's
`RandomGenomeOptions` (so about two in five carry a joint), developed and built through
`PhenotypeBuilder` into one physics scene, with `FluidEnvironment` applying drag exactly as
`Ecosystem.Step` does (the fluid is most of the per-body cost; a spike without it measures
the wrong loop) and the test sine driving every joint at full amplitude so nothing sleeps.
Gravity and buoyancy as the world has them. Placement without overlap: rejection-sample
positions on bounding spheres inside the volume; spawn-time depenetration is a force
(logbook/0007) and would poison the measurement.

## The conditions

| axis | values |
|---|---|
| population N | 250, 500, 1,000, 2,000 |
| space | **tiled** (today's: 100 m lattice, mutually ignoring layers) · **shared** (one volume, one layer, collisions on) |
| footprint (shared only) | 10 × 10 m (today's `EVOSIM_AREA` 100), 20 × 20 m (`WorldAreaSquareMetres`' default 400), 50 × 50 m; depth 60 m in all |
| step | dt 0.01 only (the confirming step; contacts at 0.02 are a different question) |

Every cell: 200 warm-up steps discarded, then 1,000 measured steps. Record per cell:
ms per physics step (mean, p95), contacts per step (a per-part `OnCollisionStay` counter;
report per body per step as well), bodies awake (fraction), mean body speed (the sleep
check), any non-finite body (fail the cell and say so), and the number of bodies that left
the volume. Repeat the two smallest shared cells three times to show the noise.

## What the summary answers

1. **The cost ratio** shared / tiled at each N and footprint. The design's expectation
   (§5A.9) is that contact cost is "rare and local"; the measurement replaces the
   expectation.
2. **The population that holds real time**: the largest N at which ms/step ≤ 10 (dt 0.01
   at 1×) in each shared footprint, alone on the machine.
3. **Density**: contacts per body per step against bodies per cubic metre. Where it
   climbs steeply is the density the footprint proposal has to stay below.
4. **Stability**: any divergence, any body flung out of the volume, in shared cells.

## Constraints

- **A quiet machine.** The absolute numbers are meaningless with the confirmation arms
  running; wait for the lead's go, which comes when they end. Do the build, the compile
  check and a 50-body smoke of the harness before that; run the matrix after.
- Do not change `Ecosystem.cs`, `PhenotypeBuilder.cs`'s layer policy for the ecosystem, or
  anything in the physics loop. The spike takes its own copies of what it needs; if a
  shared-layer build needs one switch in `PhenotypeBuilder`, add it as an optional parameter
  with today's behaviour as the default and show the default path is the same expression.
- Commit on `main` (ending `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`), do not
  push; the lead reviews and pushes. No `--no-verify`, no history rewrite.

Hand back: the commit, `results.csv`, the summary with the four answers above and the
machine state during the matrix (`Get-Process Unity` count, and whether anything else ran),
and anything the spec asked for that you could not do as written and why.
