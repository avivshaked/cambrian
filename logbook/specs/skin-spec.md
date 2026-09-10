# Build spec: the theatre's skin, first day (owner: "go ahead", 2026-09-10)

Read `research/theatre-look/README.md` first, in full: it is the reading this build follows, and
its "first day's cut" is this spec. Then `CLAUDE.md` (the theatre paragraph; the rule that
anything under `Assets/Evosim` is simulation source and the theatre lives beside it; the
snapshot entry), `unity/Assets/Theatre/TheatrePalette.cs` (how every body is repainted after it
is built, `Body.Renderers`, the material property block on first paint), `TheatreRunner.cs`,
`TheatreReplay.cs`, `SnapshotCamera.cs` (the render to texture, `SilenceTheWater`, the composited
box), `unity/Assets/Evosim/Sim/PhenotypeBuilder.cs` (read only: `VisualPlan`, how the visual
GameObjects are made from primitives, `ResizeColliderAndVisual`, which rescales the visual
transform on growth), `SeaFloor.cs` (read only) and `WaterBounds.cs` (read only; find it with grep).

Rules: edit only under `unity/Assets/Theatre/` and, if a renderer feature or pipeline setting is
unavoidable, the URP assets under `unity/Assets/Rendering/` (say exactly what changed and why);
nothing under `unity/Assets/Evosim` or `src/`; no binary assets: every mesh, texture, material
and shader is generated or written in code (`.shader` HLSL files for URP are fine; Shader Graph
files are not, since they cannot be reviewed in a diff); nothing written outside the repository
(pictures under `scratch/snaps/`); no commit; never poll, sleep or wait (a Unity launch with
`-Wait` is one blocking command and is allowed); never run Unity against `unity/`; test on
`unity-w7` only, refreshed first with `./scripts/new-worker.ps1 -Workers @(7)` from PowerShell
(exits 1 on success; read its `refreshed` line); workers 2 to 6 are running arms and must not be
touched; doc-comment register: why, with the source; no em-dashes.

## The two constraints that are not negotiable

1. **Sizes stay true.** Every visual stays inside its collider's extents. A rounded mesh is
   clamped inward; a puff is bounded by a fraction of the smallest half-extent. A picture that
   lies about size misleads the next reading.
2. **Legibility before prettiness.** Guild must read at a glance (producer, stomach, mixotroph,
   structural), a joint must be visible, and a small body must still be visible in the top view
   at 1600 px across 20 m. When a choice trades legibility for looks, legibility wins.

## What to build

### 1. Dark-field lighting and water

Following the note's "Lighting, before any material": a near-black blue water colour as the
camera background and as exponential depth fog (URP `RenderSettings.fog`, density tuned so the
far wall of the 20 m box is dim but present in the side view); low ambient; one raking
directional light from above and behind the default camera, plus a weak fill from the opposite
side so that the far side of a body is not lost. Applies in Play mode and in `SnapshotCamera`'s
renders alike; the composited box, seams and label stay.

### 2. One body material

An HLSL URP shader, forward, instanced, applied to every body renderer by `TheatrePalette`:

- Fresnel rim carrying the guild colour, muted (HSL, saturation and lightness capped), on a
  darker, desaturated body colour of the same hue.
- Thickness-faked subsurface (the single-pass Battlefield approximation, John Austin's URP port
  is the reference): a warm transmission term where the light is behind the body.
- A Voronoi cell mottle, procedural in the shader, low contrast, scale in world metres so that
  a big body has more cells than a small one.
- A faint inner glow driven by the body's reserve fraction, as the palette's tint does now
  (well fed brighter, starving pale), through the same per-body property the palette already
  sets. Keep whichever batching strategy the palette uses and say which.
- A joint: the part attached by a joint gets a visible short neck, a thin capsule between the
  two anchors, drawn by the theatre, coloured as the palette's jointed colour, inside the
  parent's collider footprint (it is presentation and must not add colliders).

### 3. Rounded meshes

Generate once per primitive kind, at start: a rounded cube (Catlike Coding's clamp
construction, roundness about 0.25 of the half-extent, normals smoothed) and a smooth sphere,
unit-sized, and swap each visual `MeshFilter`'s mesh for the rounded one with the same transform,
so that `ResizeColliderAndVisual`'s rescale on growth still applies. The rounded cube is inset,
never outset. Then a vertex puff in the shader along the smoothed normal of at most 3% of the
smallest half-extent, so the flat faces read as soft, and say in the remark why the bound.

### 4. The water's furniture

- Marine snow: one particle system over the whole box, a few thousand small unlit motes,
  slow fall, gentle drift, faint; one system regardless of the population.
- Caustics: a procedural caustics texture (a couple of overlaid Voronoi edges) panned slowly,
  projected from above onto the sea bed and faintly onto bodies in the top ten metres, through
  the body shader and a bed material.
- The bed: a sand material on the sea floor's renderer with a procedural normal map (ripples),
  triplanar so it does not stretch.

### 5. Pictures, and iterate

Refresh worker 7 and run `./scripts/theatre-snap.ps1 r35-s3 -At 1000,5000 -Worker 7` (the arm
is running on worker 4; the snapshot only reads its files, and the hashes match this tree, so
the label must not say `NOT A FAITHFUL REPLAY`; if it does, say why). Read the PNGs with the
Read tool. Judge against the two constraints and the note's intent: dark field, bodies lit at
the edge, guild readable, joints visible, no body outside its collider (compare a top view
against the same time drawn by `python scripts/positions-read.py r35-s3 --at 1000`, which draws
the recorded positions; dots and bodies must coincide). Fix and rerun until it reads. Take
one extra portrait side view: `-Views side -Size 900x1600`.

### 6. Record

`CLAUDE.md`'s theatre paragraph: two sentences naming the skin (where it lives, that it changes
no hash, the shader file). Nothing else in the record; the logbook entry is the caller's.

## Report

The shader's key passage; how meshes are swapped and where the size bound is enforced; the file
list with sizes; the pictures' paths and what you saw in each, honestly, including what does not
read; the frame time in Play mode at r35-s3's population if you can read it from the log;
anything that did not fit.
