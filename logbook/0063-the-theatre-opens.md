# 0063 — The theatre opens

**2026-09-05**  ·  a build record, not a round

The theatre's first cut exists. A run directory replays in a rendered world, with an
identity check on screen that tells the viewer whether they are watching the run or a cousin
of it. Nothing rendered has been seen by anyone yet, because batch mode has no display.

This is D075's parallel item and DESIGN.md §6.1's second program, written from the
implementing agent's report, `scratch/theatre-build-report.md`.

## What was built

Four commits, and 521 Core tests.

| commit | what it carries |
|---|---|
| `1392ccd` | a creature id on snapshot rows |
| `0fa6568` | the theatre |
| `8a4179c` | the theatre moved out of the hashed tree |
| `7b39e7d` | a double-snapshot fix |

Mode B is the rendered world. `Evosim/Rebuild Theatre Scene` generates the scene at
`Assets/Scenes/Theatre.unity`.

The scene holds a URP camera, the water drawn as a wire volume with its surface and floor,
and a `TheatreRunner`.

Point the runner at a run directory, or at the arm directory above it, and press Play. It
rebuilds the world from `config.json`, and from `run.json`'s seed and physics step.

From there it steps `Ecosystem` itself, with `Physics.simulationMode` set to script.

The controls are pause, pace and seek, the last of them unpaced with the camera off until it
reaches a target time, plus free-fly and follow cameras. Click a creature to select it and
read its id, generation, kind, age, reserve, depth and speed. Colour is by cell type, tinted
by reserve.

The identity check is built in. At every sample of `stats.jsonl` the runner compares its
live state against the recorded row and names the first differing column in red.

| compared at every sample |
|---|
| `alive`, `births`, `deaths`, `auditResidual`, mean height |

It refuses a run whose `simHash` or `coreHash` differ from the build's, and plays such a run
under an override, with a banner.

On a 1,000-s reference recording at 0.01, in `th-ref` on round 24's settings, the source was
identical. So were 10 of 10 samples, replayed at 35 times real time headless.

The creature id on snapshot rows arrives with `GenomeJson` format 4. The id is the row's
first field, written by the snapshot writer rather than by the genome serializer, because a
genome is a recipe and an id is a body.

Across five runs, 396 ids join `lineage.jsonl`: present, born before the snapshot, and not
dead before it. Format 4 refuses format-3 files by name and version, and the inocula in
`scratch/` are format 3 and stay so.

Mode A is one creature. A genome comes from a snapshot, by row or by id, or from a file. It
is grown and wired to its own brain, sensors and effectors, in the reference water.

The viewer places a smell density, since no run stores a field, and a test-sine toggle
stands as the null. Headless, a two-part creature travelled 0.67 m under its brain against
1.60 m under the sine in 30 s, with all seven channels finite.

## Two things the build found

The theatre lives at `unity/Assets/Theatre/`, outside the tree that `simHash` covers.

As first built it sat under `Assets/Evosim`, so editing a HUD label changed the simulation
hash and refused every earlier recording.

It was moved with the GUIDs intact. A label edit now leaves `simHash` unchanged, at
`ea3e5c18…` before and after, while the theatre's own tree hash moves.

The second finding is that `simHash` is a property of a checkout rather than of a commit.

Three simulation files sit CRLF on disk while their neighbours are LF, and `core.autocrlf`
hides that from `git diff`. Reconstructing a tree from git blobs gives a third hash again.
It is in CLAUDE.md now.

The double-snapshot fix closes a smaller hole. A run that ended on a snapshot boundary wrote
its population twice, at 88 rows for 44 creatures.

`Snapshot` now refuses a second write at the same elapsed time.

The re-recorded reference has 44 rows, with byte-identical `stats.jsonl` and
`lineage.jsonl`.

The fix changes `simHash` for launches after it, to `995cda59…`, and round 24 stays on its
builds.

## What needs eyes

No display exists in batch mode, so nothing rendered has been validated. That covers the
scene as a picture, the overlay, Play-mode identity, selection and follow, the colours, and
the rendered pace on a 4,000-body world.

The headless pass proves the loader and the comparison, in the mode the recording was made
in, and no more. The owner opens `Theatre.unity` and looks.

Patch boundaries are not drawn, because a patch is an index rather than a region (D061), and
that stays true until D076's footprint makes it one.
