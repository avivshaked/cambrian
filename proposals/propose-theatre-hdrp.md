# The theatre on HDRP: what it would give, and a plan

*2026-10-16. A proposal for the owner. Nothing in it has started: the owner asked for the plan to be fixed now and the
work held while tokens are being saved. When the owner rules, the ruling goes into `DECISIONS.md` and this file is
deleted.*

## Why

The owner wants a shallow depth of field with real bokeh in the theatre's pictures. The theatre runs on Unity's
Universal Render Pipeline (URP 17.5, Unity 6.5), which has depth of field in a Gaussian and a Bokeh mode, and the
theatre has used both since 2026-09-26 (`unity/Assets/Theatre/`, the portrait and safari focus settings). Unity's
other pipeline, the High Definition Render Pipeline (HDRP), has a physical camera, physically based depth of field and
a path tracer. It also has most of what an underwater scene needs: light that scatters in the water, a water surface
seen from below, and caustics.

Unity put HDRP in maintenance on 2026-02-23: no new features, fixes for stability, regressions and critical issues,
and no end date given ([Unity's post](https://discussions.unity.com/t/render-pipelines-strategy-for-2026/1710004)).
URP is Unity's priority, and its plans for 2026 include physical light units, auto exposure, a physical sky,
real-time global illumination and screen-space reflections. So HDRP looks better today and will not improve, and
URP will close part of the gap over 2026 and 2027.

The port is agent work of about a day or two (an estimate; earlier estimates in weeks were done in an afternoon). The
real costs are the card's time, since the theatre never runs beside a card round, and the owner's time to approve the
look again.

## What HDRP would give the theatre

Each item is something HDRP has in Unity 6 (from its manual and Unity's posts); what it would mean for these worlds
is my reading, marked where it is a guess.

**The water and its light**
- *Volumetric light.* Light scatters through the water in three dimensions, so sun shafts, haze and the fading of
  colour with distance come from one model, and a body or a coral canopy casts a shadow shaft. The theatre's shafts
  and fog are approximations built by hand.
- *Local fog volumes with a density texture.* The haze can be thicker in some places than others. The world's matter
  field could drive it, so a fed oasis looks murky with life and the desert looks clear (a guess at the look; the
  data exists).
- *The water system's underwater view.* The surface seen from below, with its waves, the bright window overhead,
  caustics on the bed and on bodies, absorption that turns distance blue-green, and the waterline when the camera
  sits at the surface.
- *A physical sky and clouds* for the shots at or above the waterline. The sun's angle could follow the world's day
  and seasons, and the clouds its weather factor (a guess at the use).
- *Physical light units and exposure.* The sun's strength in real units, so the light falls off with depth as the
  world's own light does, and the picture stays exposed from the shallows to the deep.
- *Light that bounces*, by ray tracing on the RTX 4090 or in screen space: the sand lighting a body's underside.
- *Real shadows*, ray-traced or contact shadows, so bodies shade the bed, and the pools of light under D194's coral
  canopy show.

**The bodies and the bed**
- *Subsurface scattering and transmission.* A backlit leaf glows green, and a thin or translucent body lets light
  through.
- *Layered and iridescent materials.* A wet sheen, or a colour that shifts with angle, if a body's look ever carries
  a trait.
- *Decals* projected onto the bed and rock: settled detritus, or the matter on the floor.
- *Tessellation and displacement* for the bed and the rock up close.

**The camera and the film**
- *A physical camera.* Aperture, shutter and sensitivity work as a real camera's do, so depth of field, motion blur
  and brightness agree.
- *Physically based depth of field and motion blur*, with the bokeh's shape set by the aperture's blades.
- *The path tracer.* Film frames rendered by tracing light physically, seconds a frame, with exact depth of field,
  motion blur and soft light. Unity's Recorder accumulates its samples frame by frame.
- *Custom passes*, to draw the theatre's own layers (the identity check, an outline round one lineage) inside the
  picture rather than over it.

**What it costs to run**
- In real time on the RTX 4090 for exploring, at a higher base cost than URP. How smoothly thousands of bodies run with
  volumetric light on is a guess until tried.
- The path tracer is for film frames only.
- It is heavier on the card than today's theatre, which already never runs beside a card round.

## The plan

The present theatre stays as it is throughout and keeps making films. The HDRP theatre is a separate Unity project
beside it, `unity-hdrp/`, with its own `Library/`, so neither can corrupt the other and nothing about the simulation or
any recorded run changes. The theatre sits outside `Assets/Evosim`, so `simHash` is untouched either way.

1. **The spike.**
   - Build: the HDRP project, with the theatre's replay of one recorded moment. The body, bed, rock, earth and snow
     shaders are ported, and volumetric light, the underwater view, the physical camera and a path-traced still are
     added. About half a day to a day of agent work (estimated).
   - Look: the same moments in both theatres, side by side, checked against the last agreed film for size,
     brightness, the bed, the reef, the caustics, the shafts and the bodies, as CLAUDE.md asks of every test render.
     The moments: a portrait of one body against a crowd, an oasis from above, the desert with an oasis in the
     distance, and the waterline.
   - Machine: Unity in the gaps between card runs only.
   - The owner rules whether to go on.
2. **The port.** Everything the theatre does moves across: live mode, the overlay, the safari, the film paths and the
   recorder. Then the look in cycles with the owner until it is approved, and the film path on the path tracer. The
   URP theatre is retired only after the owner approves a whole film from the HDRP one.
3. **The world in the picture.** The pieces that read the world's own data: haze from the matter field, the sky from
   the day, the seasons and the weather, and the shade under the coral (D194).

## What it does not change

The farm, the simulation and every recorded run. A pipeline change is a presentation change; no round's result depends
on it.

## For the owner to rule

1. Whether to run the spike, and when. The owner has said not tomorrow, while tokens are saved.
2. The separate project (`unity-hdrp/`, recommended: nothing at risk) or the port in place on a branch.
3. The spike's moments, if not the four above.
