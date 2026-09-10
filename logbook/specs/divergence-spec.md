# Divergence handling and the error manifest — implementation spec

Fable, 2026-09-04, after logbook/0056's censored arm `r20q-s1` (owner: "fix the bug(s)").
Default-preserving: a run in which no body diverges must be step-for-step identical to
today — the only new code that runs on a healthy step is a finiteness check.

## The fault

`r20q-s1` (dt 0.02, seed 1, `EVOSIM_CONCEPTION_ORDER shuffled`, commit `23a6bd8`) died at
t=15,345: `scratch/logs/evosim-r20q-s1.log` shows PhysX refusing `{NaN, NaN, NaN}` forces
and torques for all three parts of one jointed creature (`Part00_n0`, `Part01_n3`,
`Part02_n3`) for nine consecutive physics steps, after which `World.Observe` refused the
creature's non-finite height and the run threw. The articulation solver diverged; the
fluid model relayed NaN velocities into NaN drag. Nothing in `Evosim.Sim` sets an
`ArticulationBody` velocity cap (`maxJointVelocity` defaults unbounded). The only other
NaN in any log is 0052's 0.05 arm, which the drag limiter was built for.

## 1. Divergence is a death, not a crash

In `Ecosystem.Step()` immediately after `Physics.Simulate(FixedDt)` (before `Fluid.Settle`),
check every living body: root `transform.position`, and every part's `linearVelocity`
and `angularVelocity`, finite. Do it cheaply — a flat loop over the parts the fluid
already iterates; no allocation. For any body that fails:

- **Dump first.** Write `runs/<arm>/<run>/diverged/<creatureId>.json` (create the
  directory on first use) with: `t`, `physicsStep`, the creature's genome (the existing
  `GenomeJson` writer), part count, jointed flag, and for every part its last *finite*
  state — keep a one-step-old copy of position, linear and angular velocity per part in
  the same arrays the fluid samples (`_velocity`, `_spin` are already there; add
  position) — plus the effector torques the driver applied on the step before. This is
  the root-cause record; one file per diverged creature, and never more than 50 per run.
- **Then kill it as a death.** `DeathCause` gains `Diverged`. `World` gets
  `KillDiverged(Organism creature)` — the same bookkeeping as a starvation death in the
  death loop (tissue to the nutrient field at the last finite height, locked matter
  returned, lineage row with `"c":"diverged"`, counters) so the audit and the matter
  identity still close — and a `Diverged` count. Reuse the existing death path; do not
  write a second copy of the deposit logic. The body is destroyed the way a starved body
  is. Use the last finite height, never the NaN one.
- **Log one line** to the Unity log per diverged creature (id, t, part count) and none
  otherwise.

`World.Observe`'s non-finite refusal stays exactly as it is: it is the guard for anything
that slips past this check, and the check runs before `Metabolise`, so a diverged body
never reaches `Observe`.

Report: a `diverged` column appended after `photo inh` (append-only rule), the running
total; JSONL field `diverged`. Manifest: `divergedTotal` in the termination block.

Core tests: `DeathCause.Diverged` serialises by name in the lineage row; `KillDiverged`
closes the energy audit and the matter identity (build a small world, kill one body via
the new method, assert `EnergyOut`/`Nutrients`/`MatterInBodies` moved exactly as a
starvation of the same body would); the `Diverged` count increments; a world in which it
is never called is bit-identical to today (the existing determinism tests cover this if
the call sites are only in `Ecosystem`).

## 2. The error manifest carries the last known facts

`EvolutionRun.Run()`'s catch writes `RunEnding` with zeros for `physicsSteps`, `births`,
`alive`, `dragImpulsesLimited`, and the wall clock. Keep the last known values on the
manifest object as the run goes (the loop already touches `manifest.LastSimulatedSeconds`
each metabolic step — extend that to steps, births, alive, `DragImpulsesLimited`,
`Diverged`, and the wall clock from `clock`) and write those on the error path. Also
add `divergedTotal` to the orderly ending.

## 3. Reproduce, and read the dump

After the suite is green and `unity-w7` compiles clean: refresh `unity-w2` (idle — check
`Get-Process Unity` shows no process with `unity-w2` in its command line) and relaunch
`r20q-s1`'s exact configuration as `r20q-s1-replay` through `rounds/launch-r20.ps1
-Seed 1 -Worker 2 -ExpectSimHash <the new hash>` (it sets `EVOSIM_CONCEPTION_ORDER
shuffled` and everything else). The `Age`/`Shuffled` paths are untouched since commit
`23a6bd8`, so the replay must reproduce `runs/r20q-s1.md` row for row up to t=15,300
(verify: diff the two reports' table rows to that t — CLAUDE.md's determinism rule; if
they differ, stop and report, because then the build changed a healthy step). At
t≈15,345 the check should fire, write `diverged/3075.json`, kill the creature, and the
run should continue to 20,000 s with `diverged` reading 1 from that sample and the audit
still 0.0000%. Report: whether the rows matched, the dump's contents in summary (part
count, joint types, the last finite velocities and torques — how fast was it spinning
the step before?), and whether any further body diverged.

Do not attempt the physics fix itself (a joint velocity cap, drive scaling, solver
iterations) in this build — that is a design decision to be made from the dump, and any
of them is a butterfly across every run. Report what the dump says and stop.

## Verify before handing back

`./scripts/core-test.ps1` green with the count; zero `error CS` on `unity-w7`; a 300-s
default arm on `unity-w7` byte-identical below the header to `runs/r20v-age1.md`
(same settings as that arm; read its `config.json`); the replay above. Rules: write only
inside the repository (`scratch/` for temporary files; never Windows TEMP or a session
scratchpad); do not commit; do not touch workers other than `unity-w7` and `unity-w2`;
`stop-arm.ps1` is the only way to stop an arm. Keep the repository's comment voice.
