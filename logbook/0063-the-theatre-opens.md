# 0063 — The theatre opens

*2026-09-05. Not a round: the build record for the theatre's first cut (D075's parallel
item; DESIGN.md §6.1's second program), written from the implementing agent's report
(`scratch/theatre-build-report.md`). Nothing rendered has been seen by anyone yet.*

## What was built

Four commits: `1392ccd` (a creature id on snapshot rows), `0fa6568` (the theatre),
`8a4179c` (the theatre moved out of the hashed tree), `7b39e7d` (a double-snapshot fix).
521 Core tests.

- **Mode B, the rendered world.** `Evosim/Rebuild Theatre Scene` generates
  `Assets/Scenes/Theatre.unity`: a URP camera, the water drawn as a wire volume with the
  surface and floor, and a `TheatreRunner`. Point it at a run directory (or the arm
  directory above it), press Play, and it rebuilds the world from `config.json` and
  `run.json` (seed and physics step) and steps `Ecosystem` itself with
  `Physics.simulationMode` set to script. Pause, pace, seek (unpaced with the camera off
  until a target time), free-fly and follow cameras, click to select a creature and read
  its id, generation, kind, age, reserve, depth and speed. Colour by cell type, tint by
  reserve.
- **The identity check, built in.** At every `stats.jsonl` sample the runner compares its
  live `alive`, `births`, `deaths`, `auditResidual` and mean height with the recorded row
  and names the first differing column in red. It refuses a run whose `simHash` or
  `coreHash` differ from the build's, and plays it under an override with a banner. On a
  1,000-s reference recording at 0.01 (`th-ref`, round 24's settings): source identical,
  **10 of 10 samples identical**, replayed at 35× headless.
- **The creature id on snapshot rows.** `GenomeJson` format 4; the id is the row's first
  field, written by the snapshot writer rather than by the genome serializer (a genome is
  a recipe, an id is a body). 396 ids across five runs join `lineage.jsonl`: present, born
  before the snapshot, not dead before it. Format 4 refuses format-3 files by name and
  version; the inocula in `scratch/` are format 3 and stay so.
- **Mode A, one creature.** A genome from a snapshot (by row or id) or a file, grown and
  wired to its own brain, sensors and effectors in the reference water, with a smell
  density the viewer can place (no run stores a field) and a test-sine toggle as the
  null. Headless: a two-part creature travelled 0.67 m under its brain against 1.60 m
  under the sine in 30 s, all seven channels finite.

## Two things the build found

- **The theatre lives at `unity/Assets/Theatre/`, outside the tree `simHash` covers.** As
  first built it sat under `Assets/Evosim`, so editing a HUD label changed the simulation
  hash and refused every earlier recording. Moved with the GUIDs intact; a label edit
  now leaves `simHash` unchanged (`ea3e5c18…` before and after) while the theatre's own
  tree hash moves.
- **`simHash` is a property of a checkout, not a commit.** Three simulation files sit
  CRLF on disk while their neighbours are LF, and `core.autocrlf` hides that from
  `git diff`; reconstructing a tree from git blobs gives a third hash. In CLAUDE.md.

The double-snapshot fix: a run ending exactly on a snapshot boundary wrote its
population twice (88 rows for 44 creatures). `Snapshot` now refuses a second write at
the same elapsed time; the re-recorded reference has 44 rows and byte-identical
`stats.jsonl` and `lineage.jsonl`. It changes `simHash` for launches after it
(`995cda59…`); round 24 stays on its builds.

## What needs eyes

No display exists in batch mode, so nothing rendered has been validated: the scene as
a picture, the overlay, Play-mode identity (the headless pass proves the loader and the
comparison in the mode the recording was made in), selection and follow, the colours,
and the rendered pace on a 4,000-body world. The owner opens `Theatre.unity` and looks.
Patch boundaries are not drawn, because a patch is an index, not a region (D061), and
that stays true until D076's footprint makes it one.
