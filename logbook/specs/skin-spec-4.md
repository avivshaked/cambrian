# Build spec: the theatre's skin, fourth day: the sun and the surface (owner, 2026-09-11 evening)

Read `research/theatre-look/README.md`, the three earlier specs (`skin-spec.md`, `skin-spec-2.md`,
`skin-spec-3.md`), then `unity/Assets/Theatre/TheatreSkin.cs`, `TheatreBody.shader`,
`TheatreBed.shader`, `TheatreSnow.shader`, `WaterBounds.cs` and `SnapshotCamera.cs`.

The owner ran the theatre on round 36 seed 1 and said: "I couldn't see the sun, or the water
shimmer, the underwater ripple effect, or many of the things we discussed." They are not
there. The three skin days were about bodies; the water has a colour, a fog and snow, and a
caustic term painted on bodies from nowhere in particular. This day is the water's.

Rules as before: edit only under `unity/Assets/Theatre/` in the worktree `scratch/wt-skin4`
(branch `skin4`), never the main tree, whose Editor the owner has open; nothing under
`unity/Assets/Evosim` or `src/`; no binary assets, everything generated at start; nothing
written outside the repository; no commit; never wait; no Unity (every editor slot is taken;
the caller compiles and photographs afterwards). Doc-comment register as before: why, with
the source. No hash moves: the theatre is outside `simHash` by construction.

## What the owner should see

Looking up from inside the water: a bright surface that ripples, a sun through it, and
shafts of light coming down that move with the ripple. Looking across: a shimmer on the
bodies and the bed that comes from the same surface. Looking at the box from outside, in
the snapshot views: nothing new in the way, the top view still sees the world.

## The items

1. **The surface plane.** One quad at y = 0 covering the box's footprint with a margin,
   generated at start beside the bed, drawn from below only (cull the face seen from above,
   so the top view and the side views from outside are unchanged). Its shader animates a
   ripple: two or three octaves of directional waves (sum of sines or a gradient noise) in
   world x and z, advected slowly, from which the per-pixel normal is built. Amplitude and
   wavelength are dials on the skin (`SurfaceWaveMetres`, `SurfaceWaveLengthMetres`), read
   from the environment as the other dials are (`TheatreSkin.Dial`, `EVOSIM_THEATRE_WAVE`,
   `EVOSIM_THEATRE_WAVELENGTH`), defaults a few centimetres and a metre or two: a calm sea,
   not surf. It is a visual; no body is moved by it.
2. **Snell's window and the sun.** From below, the surface shows the sky refracted into a
   cone of about 97 degrees around the vertical, and outside that cone the surface is a
   mirror of the dark water (total internal reflection). The shader takes the view
   direction and refracts it through the rippled normal with the water's index (1.33).
   Inside the window it shows a sky gradient with a sun disc where the refracted ray meets
   the sun's direction; outside it shows the fog colour darkened. The sun's direction comes
   from the scene's directional light so the key light and the sun agree. The ripple
   breaks the window's edge and wobbles the sun, which is the shimmer.
3. **Light shafts.** A small number of translucent vertical slabs or a cone bundle under
   the sun, generated at start and drawn additively. They fade with depth over the first
   fifteen or twenty metres and at grazing view angles. Their brightness is modulated by
   the same ripple function sampled at their top, so the shafts move with the surface.
   They must not write depth and must not occlude bodies. Cheap: this is a handful of
   quads, not a volumetric pass.
4. **The caustics agree with the surface.** `TheatreBody.shader`'s caustic term and a
   matching term on `TheatreBed.shader` are driven from the same ripple function, same
   time, same dials. The light pattern on a body's upper surface is then the surface above
   it focused, and it fades with depth on the same curve as the shafts. Put the ripple
   function in one shared `.hlsl` include under `Assets/Theatre/` so there is one copy.
5. **Depth.** Everything above fades with depth below the surface: the shafts, the
   caustics, the window's brightness seen from far down. The world is 60 m deep and the
   habitable band is the top twenty; below that the water is the dark field it is now.
6. **A view for the picture.** `SnapshotCamera.cs` gains a view `sky`. The camera sits
   three metres below the surface at the box's centre, looking up at about 60 degrees from
   horizontal with a wide field of view, so one frame holds the window, the sun, the
   shafts and the bodies beneath them. Add it to the views list and the error message. (The `box`
   branch also edits this file; the caller resolves that merge.)

## What it is not

Not physics: no wave moves a body, no current changes, no hash moves. Not a post-process
volumetric pass: the shafts are geometry. Not a sun that sets: the scene's light direction
is fixed for now; the day cycle is the simulation's and is not drawn yet.

## Pictures

Round 36 seed 1 at 5,000 s in the `sky` view and the `close` view, plus one `side` view to
show the outside is unchanged. One worker refreshed with the worktree's `Assets/Theatre`.
