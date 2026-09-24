# What would make the films breathtaking: a review of round 47's safari

*2026-09-24. The owner asked what to change next to make the simulation breathtaking. An
Opus 5.5 subagent looked at all 24 contact sheets of round 47 seed 2's safari
(`scratch/safari/r47-s2/2026-09-24/`) and about a dozen full frames, measured the tonal range
of ten, and read the skin, the grade, the snapshot camera, the safari's plans and the three
theatre specs. It checked the safari worktree so as not to propose again what is in flight
there (the leaves, the knuckles, the hue per clade, the call-outs). It wrote nothing; this
file is its report, set down by the main agent. Hours are the agent's rough estimates.
Where it says "I believe" or "a guess", so does this file.*

## The three biggest problems

**The water is murky and has no light source.** Every frame is teal on teal. The spread of
luminance from the 1st to the 99th percentile is 0.23 to 0.49 in most frames and 0.06 on
the floor shot. Mean red is 0.04 to 0.13 of full scale in every frame measured, so there is
almost no warm colour anywhere. The key light comes from the camera, not the world:
`CapturePlaced` re-aims it from behind the lens on every render. Nothing casts a shadow,
because every light is `LightShadows.None` and no theatre shader has a shadow pass. So the
reef caps shade nothing on screen, although the world's light model charges for their
shade. The water's furniture is still sized for the old 20 m box: nine shafts 0.4 to 1.3 m
wide in a tank 167 m across, and 2,600 snow motes in about a million cubic metres. One
lone shaft shows in scenes 05, 09, 15 and 19, and snow shows nowhere.

**The bodies read as cut card or confetti.** Leaves are uniform bright mint slabs, and seen
edge-on they turn into thousands of dashes (scenes 01, 02, 03, 23). The backlit glow the
owner approved never fires in a film, because the key sits behind the camera; only the
portraits get a back light. The agent's reading of `TheatreBody.shader`, marked as
inference: on a thin leaf seen at an angle, the rim and the inner glow cover the whole face,
so the guild colour becomes paint on the face, which the look's rules forbid. The best
frames are the sphere bodies of scenes 09, 10 and 18, which read as coral. The veins in
flight on the safari branch darken transmitted light, and with the key behind the lens
there is none, so they will not show until the lighting changes.

**The camera never gets close, and nothing seems to happen.** Portrait subjects sit 5 to
31 m away through fog and are small in frame; in scenes 05 and 07 they cannot be picked
out. Births are specks under a caption saying one happened (04, 17, 21). A colony shot
cannot show a colony: the 0.5 m/s ceiling holds the pull-back to about 7.6 m, and the clades
spread over 36 to 284 m (06, 08, 10, 12, 15, 19, 24). Some shots are dead. Scene 13 is 20 s
of black, empty sand, and scene 02 spends its last 15 s on sand. Scene 22 is black in its
right third, because its eye sits beside a reef stem (`SafariPlans.Fixed` never checks for
reef) and looks 71 m through fog. Scene 20 opens with a body filling the lens. Every frame
also carries burnt-in capitals such as "IN THIS COUSIN: BODY 4984 BORN TO BODY 4528".

Two things found in passing. **Ambient occlusion is built and does nothing**: no theatre
shader samples it, and `AfterOpaque` is off. That is read from the code. **Depth of field
is probably ineffective too**, which is not verified. The occlusion's source is the depth
and normals texture, and no theatre shader has a depth or depth-normals pass. So the agent
believes the bodies are missing from the depth texture that depth of field, the soft shafts
and the snow read. A frame debugger check would settle it. Separately, the portraits focus a
50 mm lens at f/5.6 about 10 m out, which keeps about 6 to 30 m sharp, and no frame shows a
blurred background.

## The ranked changes

| # | Change | Scenes it fixes | Hours | Machine cost | Owner ruling? | Risk |
|---|---|---|---|---|---|---|
| 1 | **Light from the world's sun.** In film mode the key follows the refracted sun ray the skin already computes, the camera keeps a fill, and shadows go on. Shadow, depth and depth-normals passes share the carve's vertex function. Rim and glow scale down on thin leaves, so light through the tissue carries the guild colour. | 01, 02, 03, 06, 21, 23, 24; shows the veins; wakes the occlusion and the depth of field | 8 to 12 | a shadow pass over about 5,600 bodies, perhaps 1.3 to 2 times the frame time (a guess) | **Yes**: it moves off the accepted rule "light from above and slightly behind the camera". The fallback with no ruling is a back light from the sun in every film shot, which gives the glow and no shadows. | medium: undersides go dark in side views, and carved meshes may show shadow artefacts |
| 2 | **A canopy shot.** The eye 8 to 15 m under the densest column, looking up 60 to 75 degrees with a 60 to 70 degree lens, a slow rise or turn, Snell's window in frame. At 30,000 s, 97% of the living (5,453 leaves) sit in the top 4 m. | 03, 23; gives the arrival and the descent somewhere to go | 4 to 6 | one scene a check | no | low with #1; without it the leaves go black against a blown window |
| 3 | **Close, with real depth of field.** Portraits and births at 1.5 to 3 body lengths with a 45 to 60 degree lens, the subject filling 30 to 50% of the frame, the focal length matched to the field of view, the aperture at f/1.4 to 2.8, births framed on the parent the director already rehearses. | 03, 05, 07, 11, 14, 16, 20, 23; 04, 17, 21 | 4 to 6 | two or three scenes | no; the spec already asks for a third of the frame | neighbours crossing the lens more often (#8) |
| 4 | **A time-lapse from the record.** Poses every 10 s and snapshots every 100 s give one frame a sample, 300 times real speed: 3,000 frames, 100 s of film for the whole bloom and the gyre. Drawn from the record, so faithful, not a cousin. | 22; a spine for the film | 12 to 16 | hours for a full run; a check on 100 frames | a label reading "x300"; no interpolation between samples without a ruling, since it adds motion the world did not record; bodies are drawn at adult size, so caption it, or record body size in the positions file from round 48 on (a recording change that moves no hash) | a leaf's angle aliases at a 10 s sample and may flicker; the reconstruction's pace a frame is unmeasured |
| 5 | **Water with depth.** Marine snow in a volume around the camera whose density follows the live snow field; clearer water for wide shots, so the reef's tables recede in layers (the arrival's 55 m sightline keeps about 2% of the light today); optionally absorption by colour, so near things keep warm tones and far things go blue. | 01, 02, 06, 08, 22, 24 | 8 to 12 | negligible | the snow and the visibility, no; absorption by colour, yes, under the water rule, because the world's light has one band | snow can read as sensor noise at the encoder's quality; clear wide shots weaken the dark field |
| 6 | **Shafts at the tank's scale.** Twenty to forty soft shafts placed around each shot, or, after #1, a volumetric pass on the sun's shadow map: beams through gaps in the canopy and past the caps. | 01, 02, 05, 09, 15, 19 | 4 to 6; volumetric 12 to 16 | volumetric perhaps 20 to 40% more frame time (a guess) | only if third-party code is vendored (CristianQiu's URP volumetric light, MIT, read first); writing our own needs none | beams with no shadow to cast them are decoration and can turn kitsch |
| 7 | **Colony shots that show the colony.** Cut to a second, wide shot at the colony's own scale instead of the 7.6 m pull-back, and turn on the call-outs already built (the clade tinted, the rest greyed, the sparkline). | 06, 08, 10, 12, 15, 19, 24 | 2 to 4 | one scene | the call-outs are the ruling already pending | the tint reads as an overlay unless it fades in and out |
| 8 | **A quality gate in the director.** It scores the frames it writes (the share of pixels an occluder within 1 m of the lens covers, the subject's share, the median luminance, the change over the take) and plans again or drops the take; the fixed time shot and the birth shots get the reef's bounds. | 13, 22, 20, the end of 02 | 3 to 5 | none | no | a good take dropped by mistake |
| 9 | **Captions and provenance out of the pixels.** Clean frames; captions as a subtitle track built from `captions.tsv`, set in IBM Plex (already in the repository), sentence case, one plain fact each; provenance as a small corner mark in the same face or an end card; the T= stamp dropped. | all 24 | 2 to 4 | none | **yes**: the accepted rules on captions and on "the provenance word in a corner" | a clip that travels without its track loses its provenance |
| 10 | **An exterior opening from Leonardo.** The arrival "from outside the glass" shows no glass, no surface and no tank. Open on an exterior the owner makes (a lit glass cylinder in a dark hall), then cut to the canopy shot. | 01 | 1 for the prompt sheet, plus the owner's time | none | already allowed: generated imagery outside the world | the painted exterior may not match the rendered interior |

## What the agent would do first

Numbers 1, 2 and 3, in that order, because they compound. The first makes the work in
flight pay. A leaf's outline and veins show only when light comes through it toward the
lens, and the same change wakes the occlusion and the depth of field and lets the caps cast
the shade the world charges for. The second points the camera at what this world has in
abundance, a sunlit canopy of thousands of leaves under Snell's window; a leaf is a line
from the side and a whole shape from below. The third takes the fog out from between the
lens and the subject, which is the main cause of the milky frames. All three can be checked
on scenes 03, 23 and 04. The first needs a ruling, and its fallback, a back light from the
sun, lets the work start before one.

## The bigger lever is not the skin

- **The speed ceiling freezes every wide shot.** "Nothing moves faster than a body swims",
  0.5 m/s, suits a 1 m subject; in a 167 m tank it makes every wide shot a still. The agent
  would restate it as a limit on movement across the screen (nothing crosses more than
  about 5% of the frame a second). A wide shot could then crane at a few metres a second
  while a close one stays slow. It is the owner's rule.
- **A story, not a catalogue.** Today the film is 24 scenes of about 20 s, each clade given
  the same portrait and colony pair. Build it on the bloom instead: the empty tank, the
  founders, the first canopy, 5,600 leaves, with the time-lapse as its spine. Most shots
  run 6 to 10 s and a few hero shots 20. The first seconds are the best shot, not a murky
  push under "R47-S2 AT 30,000 S".
- **Subjects chosen by how they look.** Five of the twelve portrait subjects are single flat
  leaves that read the same (03, 05, 16, 20, 23), and many featured clades had 5 to 31
  members. Since 97% of the late crowd is one clade, the hue per clade in flight will not
  vary the 30,000 s crowd.
- **The dead shots cut.** Empty sand in 13 and at the end of 02 is true, since nothing lives
  in the dark, and worth 5 s, not 35.
- **Fewer numbers on screen.** There are 51 captions in about eight minutes; the numbers
  belong in the narration.
- **Sound is half of mesmerising.** A score and a room tone cost nothing in the render, and
  they are the owner's edit.
- **Delivery.** Keep the PNG frames as the master, since the mp4s are proxies at CRF 18.
  The agent's general knowledge, not checked here: an upload at 1440p or 4K gets a better
  encoder and more bitrate, which fog gradients and snow need.

## The rulings it asks for

1. The key light (#1).
2. Absorption by colour in the water (#5).
3. Where the captions and the provenance go (#9).
4. The call-outs (#7, already pending).
5. Whether the time-lapse may interpolate poses between samples (#4).
6. The camera's speed ceiling.
