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
