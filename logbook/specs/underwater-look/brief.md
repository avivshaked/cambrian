# The underwater look, first batch: the build brief

Written 2026-10-07. The owner asked that morning for renders that feel more underwater without losing the
4K clarity they have, with the water itself visible and the world above the water shown. The owner left
the choices to the coordinator. Two surveys stand behind this brief: an Opus reading of the skin
(`scratch/underwater-look/survey.txt`) and a Sonnet survey of underwater rendering on the web. What
follows is what to build.

## Rules for every item

- Everything lives under `unity/Assets/Theatre/`. Nothing under `Assets/Evosim`, `src/Evosim.Core` or the
  farm changes, so no hash moves and every recording still replays.
- Each item sits behind its own switch, an environment dial `EVOSIM_THEATRE_*` and a field on the skin,
  **off by default**. With every switch off, a picture is the same as today's. A combined dial
  `EVOSIM_THEATRE_LOOK=3` turns the whole batch on for films and the close still.
- The census stills (side, end, top, corner) and the solo stills (`TheatreSoloStill`, a cut-out on a
  transparent background) keep today's look whatever the dials say. They are records and must stay
  comparable.
- Nothing softens a body within about 3 m of the camera. Effects grow with distance; edges are never
  resampled; no full-screen blur, no refraction wobble across the frame, no depth of field beyond what
  the portraits already use.
- No third-party code is vendored. The web survey named community URP packages for volumetric fog; they
  are reference reading only, and every shader here is written in the project.
- Every global colour set from script takes `.linear` (the theatre's linear-space gotcha).
- Time-stable: anything noisy is a function of position and the shaders' clock, never of a random draw
  per frame, so a film does not shimmer.

## The items

1. **One sun.** The key light turns behind every camera (`TheatreSkin.cs:212-218`), but the sun the water
   draws (the window's disc, the caustics' shift, the shafts) is pushed only when the world is dressed
   (`PushWater`, `ResolveSun`, `TheatreSkin.cs:632-673`). Give the sun one world bearing, fixed per
   world, and push it whenever the key moves, or keep the key camera-relative for modelling and draw
   everything the water shows from the fixed bearing. Check: two shots at different bearings put the
   disc at the same compass point.
2. **The water you can see: absorption and in-scatter, apart.** In `EvoMixFog` (`TheatreWater.hlsl:169-182`),
   which every surface goes through, replace the one grey factor with two terms:
   `colour * exp(-sigma_abs * d) + inscatter * (1 - exp(-sigma_bs * d))`. `sigma_abs` is per channel,
   red first (pure water's ratio is about 0.36, 0.07 and 0.01 per metre for red, green and blue,
   scaled down so the tank stays readable). `sigma_bs` is separate and lower. The in-scatter colour is
   the water column's colour at that depth, brightened toward the sun by a forward-peaked phase
   function (Henyey–Greenstein, g about 0.8), so the water glows where the light comes from. Tune so a
   body at 3 m keeps its guild's hue and the far glass dissolves into lit water.
3. **Light by depth and by the run's own day.** The theatre's water darkens over 18 m and its key and
   ambient do not darken at all; round 57's light falls over 6 m (`attenuationDepth`). Let
   `PictureConfig` read the run's `light` block, and scale the key, the ambient and the leaf glow by
   `exp(y / attenuationDepth)` times the day factor (Core's `DayFactorAt`, and the weather factor where
   the build has it, both pure functions of the second), with red taken out faster with depth, a floor
   so the deep is not black, and the exposure meter compensating so a deep shot is still readable.
   Check: the winter frame is darker than the summer frame by about the run's own factor before the
   meter, and readable after it.
4. **Shafts through the volume.** Replace, behind the switch, the nine additive quads
   (`TheatreSkin.cs:1264-1383`, `TheatreShafts.shader`) with a raymarched single-scattering pass:
   along each view ray, at 32 to 64 steps at full resolution, sample the light the surface sends to
   that point (the caustic field the water already computes for the bed, `TheatreWater.hlsl:273-294`,
   evaluated at the sample's depth along the refracted sun ray), times the phase function, times the
   absorption to the sample and to the eye. Shafts then come from the same waves as the caustics and
   move with them. Stop the march at the depth buffer so a body in front is never covered.
5. **Snow around the camera, lit by the shafts.** Three layers of motes in shells around each shot's
   camera (near, middle, far), small and sharp, the near ones capped in size, brightness taken from the
   item-4 light at the mote. Today's 2,600 motes over the whole tank show nowhere at this size.
6. **The sky from below.** Give the window an overcast from the day and weather factors (more cloud, a
   softer disc on a dull day), a slight colour split at the window's rim, and a mirror outside it that
   brightens toward the window. Nothing is drawn that the run does not imply: no setting sun, since the
   day factor never reaches zero.

Not in this batch: shadows (a later question for the owner, at 1.3 to 2 times the frame time), the
camera above the surface and the waterline shot (the next batch, one to three days), anything beyond
the glass, the grade (done in Resolve if at all).

## How it is judged

Before-and-after stills from `r57-s2` (`runs/r57-s2/2026-10-06-065827-dfc0e881`) at 36,000 s (summer)
and 27,000 s (winter): the sky and bed views from the snapshot, and a close and an orbit frame from the
checkpoint with `theatre-film.ps1 -Freeze`. Each pair side by side, read for mean luma and the 1st to
99th percentile spread, the bed, the reef, the caustics, the shafts, body outlines at a 100% crop, and
the captions. The coordinator takes and reads them, and shows the owner only a result that is better.
