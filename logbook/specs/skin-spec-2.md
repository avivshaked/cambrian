# Build spec: the theatre's skin, second day: impressions (owner, 2026-09-10 evening)

Read `research/theatre-look/README.md`, `logbook/0088`, `logbook/specs/skin-spec.md` (the first day),
then `unity/Assets/Theatre/TheatreMeshes.cs`, `TheatreSkin.cs`, `TheatreBody.shader`,
`TheatreBed.shader`, `TheatrePalette.cs` (`Dress`, `Fit`), `SnapshotCamera.cs`.

The owner looked at the first day and said: "it's not bad, but it's still very very
geometric. I thought we'd be able to use the skin to create the feel of impressions in the
shapes and make them look a lot less geometric." The rounding and the mottle left the faces
flat and parallel. This day is about the silhouette.

Rules as on the first day: edit only under `unity/Assets/Theatre/`; nothing under
`unity/Assets/Evosim` or `src/`; no binary assets; nothing written outside the repository;
no commit; never poll, sleep or wait (one `Start-Process -Wait` per launch is allowed);
never run Unity against `unity/`; test on `unity-w7` only, refreshed first with
`./scripts/new-worker.ps1 -Workers @(7)` from PowerShell (exits 1 on success); workers 2 to 6
are running arms, do not touch them; if `unity-w7/Temp/UnityLockfile` exists with no Unity
process holding `unity-w7` (check `Get-CimInstance Win32_Process` command lines), remove it and
say so; doc-comment register: why, with the source; no em-dashes.

The two constraints stand: **sizes stay true** (nothing outside the collider; carving inward
is allowed and a carved body is a little smaller than its box, which under-reports rather
than over-reports), and **legibility before prettiness**.

## What to build

### 1. Carved silhouettes

- **Denser shared meshes.** The rounded cube and the sphere at enough vertices for
  displacement to read: of the order of 24 segments per edge for the cube and a subdivided
  icosphere of similar density. Shared, generated once, so the cost is per vertex in the
  shader and not per body in memory.
- **Inward displacement in the vertex stage**, two octaves of 3D noise in the part's own
  object space (so the impressions travel with the part and do not swim as it moves), seeded
  per body from its id (a per-instance property; keep the palette's batching strategy):
  large lobes with a wavelength of about the part's own size, and finer wrinkles at a fifth
  of that. Displacement is `-normal * depth * noise01`, never positive, with `depth` a fraction
  of the smallest half-extent exposed as one dial, `_CarveFraction`, default 0.2, and the
  puff of the first day removed or folded into this (say which). The bound: the deepest
  carve is `_CarveFraction * smallest half-extent`, and the mesh is already inset, so no
  vertex leaves the collider.
- **Normals recomputed from the carved field** (finite differences of the same noise in the
  vertex or fragment stage) so that dents shade and catch the rim; without this the carve is
  invisible under the lighting.
- **Where two parts meet**, deepen the carve near the joint anchor on both parts so that a
  junction reads as a pinch rather than one box entering another; the neck from the first
  day stays.
- **Per-guild character**, lightly: producers a little more lobed (leafy), stomachs a little
  more wrinkled, structural parts smoother. Small differences; the guild is still told by
  colour.

### 2. The bed

The same carve on the sea floor's display mesh: a denser plane, displaced downward only (never
above the collider's plane) by low-frequency noise so it reads as a rippled bed rather than a
plane, with normals recomputed.

### 3. Pictures, and the dial

Refresh worker 7 and run `./scripts/theatre-snap.ps1 r35-s1 -At 5000 -Worker 7` (seed 1 has
ended; the hashes match this tree and the label must not say `NOT A FAITHFUL REPLAY`) and a
portrait side: `-Views side -Size 900x1600`. Also render a close view: add a fifth view
`close` to the snapshot entry (optional views list) that frames the six largest bodies at
the requested time from a three-quarter angle at a distance that fills the frame, so that a
silhouette can be judged; a world view at 13 px per metre cannot show a wrinkle. Read every
PNG with the Read tool. Iterate on `_CarveFraction` and the noise scales until a body reads as
grown rather than built, then render the same close view at `_CarveFraction` 0.1, 0.2 and 0.35
(an env var `EVOSIM_THEATRE_CARVE` read by the skin, default 0.2) and keep all three sets so
the owner can pick from pictures.

### 4. Record

`CLAUDE.md`'s theatre paragraph: one sentence on the carve dial and the close view.

## Report

The displacement passage of the shader and where the bound is enforced; the close-view
construction; the file list with sizes; the pictures' paths at the three carve settings and what
you saw in each, honestly; anything that did not fit.
