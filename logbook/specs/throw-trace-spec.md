# Build spec: the throw trace (owner "sounds good. go ahead", 2026-09-12)

Read `CLAUDE.md` in full first (the gotchas on divergence, the dump cap, growth's in-place
resize, `simHash`, and the subagent rules), then `scratch/research-throws/notes.txt` in the
main tree (the search this instrument answers), then `unity/Assets/Evosim/Sim/Ecosystem.cs`
(`CheckFinite`, `Dump`, `DumpBodies`, the resize loop near `GrowthStepSeconds`) and
`unity/Assets/Evosim/Sim/PhenotypeBuilder.cs` (`Build`, `Resize`, the 0.001 kg mass floor).

Rules: edit only in this worktree (`D:\Projects\experiments\evolution-simulator\scratch\wt-throws`);
write nothing outside it, and nothing under the session scratchpad or TEMP; no commit; no
Unity (the caller compiles on a worker); no `.md` files; do not touch running workers or
`runs/`; never sleep, poll or wait. Doc comments say why and cite this spec by path.

## What it is

A diagnostic only. It changes no force, no mass, no clamp, no order of operations in the
physics loop, so a Box digest under this build must be identical to today's (the caller
checks with a digest pair). It answers one question the dumps cannot: what a jointed body's
links were doing on the step before one went non-finite, and how its masses were arranged.

## The trace

1. **Mass ratios.** In `PhenotypeBuilder.Build` and `Resize`, after the masses are set,
   compute for every joint the ratio `max(parentMass, childMass) / min(parentMass, childMass)`
   using the masses actually written to the `ArticulationBody` (the effective mass when the
   fluid is on). Store the body's largest ratio on the instance (`MaxJointMassRatio`) and the
   masses per link. `Ecosystem` keeps the largest ratio seen in the run and the number of
   bodies whose ratio exceeded 10 at any build or resize; both go to the manifest
   (`maxJointMassRatio`, `bodiesOverMassRatio10`) and to `stats.jsonl` at every sample.

2. **The ring.** For every jointed body (`TotalDof > 0`), every physics step, record one
   frame per link: position, linear velocity, angular velocity, the joint's velocity vector
   where the link has one, and a flag saying whether this body was resized on this step.
   Keep the last three frames per body in a preallocated ring (no allocation per step; float
   arrays sized at build and reused). Rigid one-part bodies are not traced. The cost is a few
   hundred bodies times a few links per step; measure nothing, but keep it tight.

3. **The dump.** When `CheckFinite` finds a non-finite link or root, or the height or radius
   guard fires, write `diverged/<id>-trace.json` beside the existing `<id>.json`: the three
   frames oldest to newest with their step numbers and times, the masses per link, the
   joint mass ratios, the body's age, the step of its last resize, and how many steps before
   the dump that was. One JSON document, indented, hand-readable. The existing dump and the
   `MaxDumps` cap are unchanged, and the trace respects the same cap.

4. **The reader.** `scripts/reads/diverged-read.py` (main tree copy is the same file) gains,
   when a trace file exists, columns for the largest mass ratio, the steps since the last
   resize, and the largest link speed and angular speed in the newest finite frame; the
   summary line adds the share of dumps with a ratio over 10 and the share within 5 steps of
   a resize. Old dumps without a trace print a dash. Test it on `runs/r36-s1/*/diverged/`
   (no traces, dashes) with `python scripts/reads/diverged-read.py r36-s1` from the main tree
   after copying the script there is NOT allowed; test in the worktree by pointing the script
   at the main tree's run with an absolute path if it takes one, or by a fixture dump you
   write under `scratch/wt-throws/scratch/` (create it; it is gitignored).

## The smoke

`Milestone1Smoke` or `SharedSpaceSmoke` (pick the one whose harness already builds a
jointed body) gains a part: build a two-link jointed body, read its mass ratio, resize it
by the growth path, read it again, then write a NaN into one link's velocity through the
harness's own check path (if there is no way to inject one, drive the check with a fake
non-finite position through a test hook that is `internal` and documented as test-only),
and assert that the trace dump lands with three frames, the ratio, and the resize step.

## Not in this build

No cap, clamp, floor change, solver setting, damping or armature. Those are a proposal to
the owner after this instrument has read round 38's dumps (`fable-propose-*.md`).

## Final message

Tables: files changed; the manifest and stats fields added; the trace file's schema; the
smoke's new assertions; the reader's new columns; what is unverified (everything under
`unity/` is uncompiled until the caller compiles it) and any place the spec was ambiguous
and what you chose.

## Second pass, 2026-09-14: the ring must keep the last finite frames, and the forces

**What the first pass could not say.** Round 37b threw 43 bodies and every dump carried a
trace (0097); in 42 of 43 all three frames were already non-finite. The ring is written
after every `Physics.Simulate` (`Ecosystem.RecordTrace`, called on every step), and
`CheckFinite` runs at the metabolic cadence, so a body that goes non-finite inside one step
overwrites its own last finite frames up to fifty times before it is found. The one dump
with finite frames (seed 4's id 702) died on the height guard with finite numbers. The
instrument caught the anatomy (two-part newborns, mass ratios under 2.4, away from the wall
and from any resize) and missed the onset, and the control `r37bc-s5` then showed the
onset is the fluid acceleration force's (0 throws without it, 37 with it, same seed and
build). The next question is what the force did on the step before the blow-up, and the
ring has to hold that step.

Three changes, all in `Ecosystem.RecordTrace`, the frame layout and the dump; nothing in
the physics, so the box digest stays identical (the trace reads and never writes):

1. **A non-finite frame is not written.** Before a link's frame is stored, its position,
   linear and angular velocity are tested finite (`float.IsFinite` on the nine numbers; the
   joint velocities too where read). If any is not, the body's ring is left as it was, the
   body's `FirstNonFiniteStep` is set once (the step number and the link index), and the
   cursor does not advance. The ring then holds the last three finite frames of every link,
   whatever the check's cadence, and the dump names the step at which the first non-finite
   number appeared and how many steps before the dump that was.
2. **The forces go into the frame.** `FluidEnvironment.Apply` already keeps the per-part
   force arrays it adds (the drag force and, when the coefficient is above 0, the
   acceleration force in `_accelForce`) and the water acceleration it sampled. Expose them
   read-only per part for the step just taken (an array the environment fills and
   `RecordTrace` reads; no allocation per step), and store per link in the frame: the drag
   force, the acceleration force and the sampled water acceleration (nine floats more per
   link; `TraceFloatsPerLink` grows and the dump's reader with it). The joint's drive
   target and the drive's applied torque where the harness has them are worth the same
   treatment if they are already in hand; otherwise not in this pass.
3. **The dump reads the ring in order and says what it holds**: `firstNonFiniteStep`,
   `stepsFromFirstNonFiniteToDump`, `framesFinite` (3, or fewer for a body younger than three
   steps, which is most of 37b's), and per frame per link the forces beside the motion.
   `scripts/reads/diverged-read.py` gains the largest acceleration-force magnitude and the
   largest water acceleration in the newest frame, and prints them in the anatomy table.

**Tests.** A Core-side test is not possible (the ring is Unity-side); the smoke's check
(part 5 of the first pass) gains a forced case: a body whose link velocity is set
non-finite by the test between two steps must dump with three finite frames and a
`firstNonFiniteStep` equal to the step after the injection. The 600 s box digest must be
identical to the current reference, as in the first pass, and a tank smoke with the force
on must read `diverged` and `stillb` as the fixture does.

**Hashes.** `Ecosystem.cs` and `FluidEnvironment.cs` are under `Assets/Evosim`, so
`simHash` moves; Core is untouched unless the reader's shared JSON helper needs a field.
It lands in the build window after the fresh seeds have launched (HANDOFF's queue), with
`field cv`, before round 38.
