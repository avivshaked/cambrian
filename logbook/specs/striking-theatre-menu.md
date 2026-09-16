# Making the theatre striking: a diagnosis and a menu

**2026-09-16**  ·  asked by the owner ("put an opus sub agent to do some exploration and thinking how to make the theatre a lot more visually striking; it can plan usage of open source libraries, or leonardo, or even if the output justifies the cost, additional resources"; and, on the constraints, "it could suggest alternatives to our decisions should that make sense"). An Opus subagent's design pass, read-only, from the pictures first and the code second. The agent's reading follows; the report is below it as delivered, with its judgements marked as readings of the pictures.

## The reading (the agent's)

**The diagnosis is verified and it is a defect, not taste.** The subagent found, and I checked
in the files, that the theatre renders with Unity's image chain mostly off: the project is
in gamma colour space (`ProjectSettings.asset`, `m_ActiveColorSpace: 0`), the renderer has
no post-processing data and no features (`UniversalRenderer.asset`), the pipeline has no
volume profile and no anti-aliasing (`UniversalRenderPipelineAsset.asset`, the default
profile's `components: []`), the snapshot's target is 8-bit, and the key light is placed
once from the fly camera's starting rotation (`TheatreSkin.cs:362`, called once at
`TheatreRunner.cs:219`) and never re-aimed, so five of the six snapshot views and every
flight around a body are lit by the fill. The dark field was designed for values that sit
in the bottom tenth of the range, and nothing redistributes them. That is the whole of
"can barely see anything", and it is a day's work, hash-safe, before any question of
style is asked.

**Order.** The one-day pass first, as the report says and for its reason: every later
judgement is made from pictures, and the pictures are taken through a broken chain. Then
the one-week pass, with before-and-after pairs at each step so the owner's eye rules. The
ambitious version is the three specs already queued (timeline, checkpoints, safari) plus
the inherited skin genes, which is the owner's genome decision and stays a proposal.

**On the reopened decisions.** R3 (versioned framing) and R5 (hue in the film's chrome,
never the instrument's) cost nothing and I would take both. R1 (a lit top layer) and R2
(guild hue through backlit translucency) are the owner's: both are the dark-field rule's
edges and both are where the report reads the largest "alive" gain. R4 (a glow in film
frames only, markers in every cited picture) is safe as bounded. D4, drawn eyes, I would
refuse for now: a creature that has not evolved an eye should not wear one, and the world
may yet evolve a light sense (round 40) whose organ can be drawn honestly.

**Where the schedule puts it.** The one-day pass slots before the timeline (HANDOFF item
2a) since the timeline's pictures should be taken through the fixed chain; the week pass
folds into the safari's build. Leonardo's zero cost (the owner's Essentials plan) makes
the report's "target frames for the look" use the right first use: generate what the tank
should look like, put it beside a close view, and turn the dials toward it, uploading
nothing.

## The report (Opus, 2026-09-16)

### 1. Diagnosis from the pictures

**The display chain is the first problem, not the art.** Four settings mean the theatre is
rendering with most of Unity's image-making switched off:

| Finding | Where | Consequence |
|---|---|---|
| `m_ActiveColorSpace: 0`, gamma, not linear | `unity/ProjectSettings/ProjectSettings.asset:49` | Lighting maths runs on gamma-encoded values; falloff, the Fresnel rim and the fog behave differently from how they were reasoned about |
| `postProcessData: {fileID: 0}`, `m_RendererFeatures: []` | `unity/Assets/Rendering/UniversalRenderer.asset:27,30` | Post-processing cannot run at all: no tonemapping, no bloom, no ambient occlusion, no full-screen pass |
| `m_VolumeProfile: {fileID: 0}`; `DefaultVolumeProfile.asset` has `components: []` | `unity/Assets/Rendering/UniversalRenderPipelineAsset.asset`, `unity/Assets/DefaultVolumeProfile.asset:14` | No grade exists to be applied even if the stack were on |
| `m_MSAA: 1`, `antiAliasing = 1`, `RenderTextureFormat.ARGB32` | the URP asset; `SnapshotCamera.cs:242-247` | No anti-aliasing of any kind, and an 8-bit output container |

The consequence, as a reading of the pictures: the whole image lives in the bottom tenth of
the code values with no curve to redistribute it. The water is (0.012, 0.032, 0.048)
(`TheatreSkin.cs:49`), in gamma space RGB 3, 8, 12 of 255. The sand is 0.012 shaded and
0.066 lit (`TheatreBed.shader:39-40`), at most 17 of 255. A starving body's face is
`BodyLightness` 0.18 times the starving floor 0.22 (`TheatrePalette.cs:97,75`), about 10 of
255. Without a tonemap a body's key-lit facets clip to white while everything else sits in
four or five distinguishable levels, which is what `r39big-m9-t600-bed.png` looks like: a
grey ramp you can only just find, the picture the owner said "can barely see anything"
about.

**The key light is aimed at a camera that is not the one rendering.** `TheatreSkin.Apply`
is called once from `TheatreRunner.cs:219` with the fly camera, and the key is placed at
`behind * Euler(38, -26, 0)`, the fly camera's starting rotation, and never re-aimed. Fly to
the other side of a body and the 0.55 fill lights it, not the 2.1 key; in a headless
snapshot the six views look from six bearings while the key stays on one. A half-day fix,
and a large part of the complaint.

**The census views are diagrams, not photographs.** In `0101-r38-s1-t15000-side.png` the
tank (400 m², 60 m deep) fits vertically, so the box occupies about 295 of 1,600 pixels:
82% of the frame is empty background, at about 13 px per metre. A 0.3 m body projects to
under 4 px, below `MinimumBodyPixels`, and is replaced by a stamped 5 px square that ignores
occlusion (`SnapshotCamera.cs:183-199`). Every dot in the side, top and iso views is a
marker. Correct for a census, useless for video.

**What reads well.** `r37b-s7-t5000-close.png` and `r37-s1-t15000-close.png` are good: the
carve reads, the guild rim reads, the mottle reads, the depth-fog separation reads.
`skin4-r36-s1-t5000-sky.png` is the most striking image in the set, Snell's window with the
sun disc and bodies silhouetted against it, and it is a non-default view nobody has judged.
The material work is largely done; the exposure, the aiming and the framing are what lose
it.

**Two more specifics.** `r38-s1-t30000-close.png` is dark partly because it is true:
brightness carries reserve, and round 38's world was starving; the signal is real and falls
off the bottom of the display range. And motion: the provenance picture shows "200× requested,
22.64× actual" at 67 bodies; at 22× with dt 0.01 each rendered frame advances about 40
physics steps and bodies strobe. Cinematic motion needs a pace near 1×, at which a late
second is hours away, which is why the checkpoints are the real enabler for film.

### 2. The menu

Effort in agent-days; money £0 unless stated; "hash-safe" means nothing under
`Assets/Evosim` changes.

#### A. Exposure, grading and the image chain

| # | Item | Visual change | Touches | Days | Risk | Rec. |
|---|---|---|---|---|---|---|
| A1 | Turn the post stack on: assign `PostProcessData` to the renderer, a global Volume with tonemapping (Neutral or ACES), colour adjustments (post-exposure, contrast), vignette, mild bloom at a low threshold so a rim blooms and nothing else does | Midtones lift, highlights roll off instead of clipping, the rim glows as light rather than paint; the single largest gain available | `Assets/Rendering/*.asset`, a new `Assets/Theatre/TheatreVolume.asset`, about 20 lines in `TheatreSkin` | 1 | Changes how every future picture looks (framing unchanged) | Do first |
| A2 | Linear colour space and a re-tune of the dark-field dials | Correct falloff and fog; the dark field stops crushing; every colour value in the skin, the palette and the shaders re-tuned | `ProjectSettings.asset` plus a tuning pass and a re-shoot of `design/reference-frames/` | 2 | A full reimport on every worker copy; every dial wrong until re-tuned | Week pass, with before-and-after pairs |
| A3 | Supersample: render at twice the size and box-downsample in the readback | Clean silhouettes on the carve; works headless | `SnapshotCamera.cs`, about 30 lines | 0.5 | Four times the readback (a 4K render is 33 MB) | Yes; the simplest anti-aliasing that survives batch mode |
| A4 | SMAA on the film camera (URP 17 ships FXAA, SMAA, TAA; TAA cannot combine with MSAA) | Anti-aliasing for the Editor and the Recorder | the camera setup in `TheatreRunner` | 0.25 | TAA into a render texture under batch mode unverified; use SMAA | Yes, for the Editor path |
| A5 | 16-bit render target (`RGBAHalf`) and HDR grading | Kills banding in the fog gradient, visible as vertical bands in the side view | `SnapshotCamera.cs`, the URP asset's grading mode | 0.5 | Larger readback; PNG stays 8-bit, so it helps only if the grade happens before encode (with A1 it does) | Yes, with A1 |

#### B. Lighting

| # | Item | Visual change | Touches | Days | Risk | Rec. |
|---|---|---|---|---|---|---|
| B1 | Aim the key at the rendering camera: re-call `Apply` per snapshot view, re-aim per frame in Play mode or parent the key to the camera at a fixed offset | Every view gets its raking key; flying around a body keeps it lit; the second-largest gain | `TheatreSkin.Apply`, `SnapshotCamera.Capture`, `TheatreRunner.Update` | 0.5 | A moving key makes two frames of one second differ if the camera moved; lock it per render for the census set | Do first |
| B2 | A three-point rig for portraits: key, fill and a back light behind the subject | A rim-lit silhouette, the plankton look, without a third front light | the close view; the safari's portrait station | 1 | A back light is the dark-field idea done properly, not a breach | Yes |
| B3 | A floor light: `RakeTheFloor()` (bed view only, intensity 4) generalised into a dim permanent bounce over the bed | The floor stops being a shadow of itself in every view | `TheatreSkin` | 0.5 | Lifting the floor lifts bodies near it; watch the guild contrast | Yes |
| B4 | Screen-space ambient occlusion (a URP 17 renderer feature) | Contact shading in the carve's hollows and where bodies touch; a crowd reads as volume rather than confetti | `UniversalRenderer.asset` | 0.5 | A depth-normals pass; needs the depth texture | Yes, cheap |

#### C. The water

| # | Item | Visual change | Touches | Days | Risk | Rec. |
|---|---|---|---|---|---|---|
| C1 | Real volumetric light: `CristianQiu/Unity-URP-Volumetric-Light`, MIT, a render pass that raymarches the main light | God rays that respond to the camera and to occlusion, instead of nine fixed additive quads placed from a seed (`TheatreSkin.ShaftMesh`) | vendored under `Assets/Theatre/ThirdParty/` | 2 | Third-party code read before vendoring; per-frame cost | Week pass |
| C1b | Or improve the cards: depth-soften the shaft quads against the depth buffer, jitter per frame | Most of the look for a fifth of the work, no dependency | `TheatreShafts.shader` | 0.5 | None | The cheap alternative |
| C2 | Marine snow with depth: size and density by distance, VFX Graph or a depth-aware snow shader | 2,600 motes of 3 cm over 2,200 m² are invisible at distance and a speckle up close; near snow big and soft, far snow gone | `TheatreSkin.BuildSnow`, `TheatreSnow.shader` | 1 | Keep it out of the census views | Week pass |
| C3 | Height-graded fog: lighter near the surface, black below the lit band, keyed to `SurfaceLightMetres` (18 m) and the run's attenuation depth (12 m) | Depth becomes legible; a descent shot gains a story | `TheatreSkin`, `TheatreWater.hlsl` | 1 | Reopens "the water is black" near the surface (R1) | Week pass |
| C4 | The surface from below, promoted: the sky view into the default set and the arrival shot | Free striking footage | `theatre-snap.ps1`, the safari's stations | 0.25 | None | Do first |

#### D. The bodies

| # | Item | Visual change | Touches | Days | Risk | Rec. |
|---|---|---|---|---|---|---|
| D1 | Rounding cap and raw-collider key (queued, HANDOFF item 11) | A near-cubic box stops reading as a ball | `TheatreMeshes` | 0.5 | None | Yes |
| D2 | Reserve carries saturation, not only brightness: raise `Starving` from 0.22 and desaturate instead | A starving world still reads | `TheatrePalette.cs:75` | 0.5 | Weakens the "this world is dying" read at a glance | Yes |
| D3 | Translucency by guild: producers get real transmission (`_TransScale` exists at 1.3), eaters stay opaque | A leaf glowing when backlit, the most "alive" cue available for free | `TheatreBody.shader` | 1 | Touches the rim-only colour rule (R2) | Week pass |
| D4 | Eyes or spots: one or two small specular dots on the root part, deterministic from the body id | The technique reading's own conclusion, "let eyes do the work of reading as alive" | `TheatreBody.shader` | 1 | A creature with no evolved eye wearing a drawn one is a claim the world does not make | Owner's call |
| D5 | Size-dependent detail: carve frequency and mottle scale by part size | Big bodies stop looking like inflated small ones | `TheatreBody.shader` | 0.5 | None | Yes |
| D6 | Inherited skin genes (HANDOFF item 11): six to ten neutral genome numbers the shader reads, so relatives look alike | The biggest narrative win: you can see a clade | `Evosim.Core`: moves `coreHash`, bumps the genome format to 6, refuses every stored genome | 3 | Not hash-safe in the Core sense; an owner-reserved genome decision | Owner's call; in no pass below |

#### E. The floor, the glass, the frame

| # | Item | Visual change | Touches | Days | Risk | Rec. |
|---|---|---|---|---|---|---|
| E1 | Sediment that shows where detritus lies: read the grid's detritus field and shade the sand | The floor becomes an instrument | `TheatreSkin`, `TheatreBed.shader` | 1.5 | A second colour meaning on the furniture | Week pass |
| E2 | CC0 sand textures (Poly Haven, ambientCG) as an optional albedo and normal | Real grain instead of procedural noise | `Assets/Theatre/` | 0.5 | Breaks the skin's "no committed binary" principle | Owner's call |
| E3 | The glass as glass: a faint Fresnel sheet on the wall instead of 48 wire chords | The arrival shot needs a wall you can see through and see | `WaterBounds.ShowTank` | 1 | Must not obscure the census | Week pass |
| E4 | Aspect by world shape: a deep narrow tank defaults to a portrait frame | Recovers the wasted frame in the side view | `theatre-snap.ps1`, `SnapshotCamera` | 0.5 | Changes census framing (R3) | See R3 |

#### F. Camera, motion and the video pipeline

| # | Item | Visual change | Touches | Days | Risk | Rec. |
|---|---|---|---|---|---|---|
| F1 | Depth of field for portraits (URP 17 volume, Gaussian or bokeh) | Real depth of field at a 28° lens separates a body from a crowd | the volume profile and the camera | 0.5 | Needs A1 | Yes |
| F2 | Cinemachine 3.1 for the safari's orbits (Unity Companion licence, free with the Editor) | Eased orbits, damped follow, lens blends, the safari's grammar for free | the manifest and `Assets/Theatre` | 1, inside the safari | A dependency; the hand-rolled version is about two days | Yes, with the safari |
| F3 | The Recorder pipeline: `com.unity.recorder` 5.1.6 is in the manifest and unused | 4K at 60 with constant-framerate capture, so a slow replay still yields smooth video | `Evosim.Theatre.Editor` | 1 | The Recorder's docs say it does not work in batch mode; capture runs in the owner's Editor; the batch-without-nographics pattern is unverified with it | Yes |
| F4 | A pace lock for filming: 1× or below (a portrait may want 0.25×) | Removes the strobe; at 22× each frame is about 40 physics steps | `TheatreRunner` | 0.25 | Filming a late second then takes hours, which is what the checkpoints are for | Yes |
| F5 | Motion blur at a shutter matched to the capture rate | 30 fps footage reads as film | the volume | 0.25 | Needs A1 | Yes |
| F6 | A colour-grading LUT for the videos, separate from the instrument's grade | One consistent channel look | the volume profile | 0.5 | None | Week pass |

#### G. Free libraries worth knowing

`CristianQiu/Unity-URP-Volumetric-Light` (MIT); Keijiro Takahashi's `Kino` (Unlicense; his
other repositories follow the pattern but were not checked one by one); Poly Haven and
ambientCG (CC0, no attribution); IBM Plex (SIL OFL 1.1, in use). Not available in URP 17:
volumetric fog, screen-space reflections, auto-exposure (HDRP has them). Everything else on
the wish list ships in the volume system: bloom, tonemapping, depth of field, vignette,
colour adjustments, curves, LUT, film grain, motion blur, lens distortion, chromatic
aberration, Panini, white balance, split toning, lift-gamma-gain, shadows-midtones-highlights,
channel mixer, screen-space lens flare. Unity Personal is fine: the threshold is $200,000
trailing revenue and funding, and the splash screen is optional in Unity 6.

### 3. Alternatives that reopen a listed decision

The two hard rules are untouched: nothing under `Assets/Evosim` changes, and the provenance
word still says whether a replay is faithful.

**R1, the water is black by design.** Protects the dark field and the rim's contrast.
Alternative: the top ten metres as lit blue-green water under a depth-graded fog (C3), true
black below the lit band. Cost: near the surface the rim loses its black backdrop and a
green producer against blue-green water is harder to separate. Paid another way: the guild
reads on the rim and the body; near the surface the back light (B2) carries the silhouette;
the census views keep the current fog so nothing in the record changes. The world's own
light model says the top 12 to 18 m is lit, so a black surface layer is less honest than a
graded one.

**R2, colour carries meaning on the rim only.** Protects against flat coloured boxes and
against a texture putting hue where it means nothing. Alternative: guild hue also through
transmission (D3): a producer glows green when backlit because light passes through it, an
eater does not. Cost: hue on the face of a backlit body; two guilds could be confused at a
grazing angle. Paid another way: transmission is gated on the lighting geometry, not painted,
and face albedo stays at 0.18. Arguably an extension of the rim rule; the owner's to rule.

**R3, the census views are unchanged in framing.** Protects comparability across entries.
Alternative: a portrait aspect for deep narrow worlds (E4). Cost: a picture in a later entry
is not directly comparable to an earlier one. Paid another way: freeze the current framing
as "census v1", stamp the framing version into the label the picture already carries, and
take both for one round as the bridge. A1's grading has the same property and needs the
same versioning note.

**R4, markers stand in for bodies too small to draw.** Protects "where are the bodies" as a
question a picture can answer. Alternative: in film frames only, a soft additive glow that
respects occlusion. Cost: a count read off a film frame under-reports. Paid another way:
markers stay in every picture the record cites; the glow exists only in the safari's
output, where no caption states a count from the image.

**R5, no hue in the chrome.** Protects the instrument's honesty. Alternative: the film's
chrome (title cards, lower thirds, chapter cards) may carry hue. Cost: almost none, provided
the two are never on screen together. Paid another way: the safari hides the interface while
recording; that is the boundary. The rule binds the instrument, not the film.

### 4. Where Leonardo fits, at zero cost to the owner

Target frames for the look itself: generate what a deep dark-field tank should look like,
put it beside `r37b-s7-t5000-close.png`, and tune the shader toward it; nothing is
uploaded, since a prompt is not project data. Title cards, chapter cards, thumbnails,
channel art. The arrival shot's backdrop if the tank is ever shown from outside. Concept
art for a look that does not exist yet (a bioluminescent pass, a night cycle) before two
days are spent building it. Not for bodies, the bed, caustics, the snow, per-clade skins
from a genome hash, or anything the record must reproduce; any project image going up needs
the owner's approval for that instance.

### 5. Three packages

**The one-day pass, "make the existing look visible"**: A1, B1, A3, C4, D2, B3. The
largest single step available: every picture in the archive is the theatre seen through a
broken display chain, and this turns it on. About a day and a half, £0.

**The one-week pass, "make it cinematic"**: the above plus A2, A5, B2, B4, C1b or C1, C2,
C3 (pending R1), D1, D3 (pending R2), D5, E3, F1, F3, F4, F5. Footage fit for YouTube: a
descent becomes a descent, a portrait separates from its crowd, a producer glows with the
sun behind it. Six to seven days, £0.

**The ambitious version, "a film, not frames"**: the above plus the three specs already
queued (the timeline, the checkpoints, which are a rendering prerequisite since no late
second can be filmed at 1× without them, and the safari director with Cinemachine), E1, F6,
and, if the owner rules for it, D6. Fifteen to eighteen days on top, £0 in tools.

**Which first: the one-day pass.** Every judgement in the other two is made from pictures,
and the pictures are taken through a chain with no tonemap, no anti-aliasing, 8-bit output
and a light pointing the wrong way. It is also the only package whose gain is certain to be
large; the rest is re-assessed from the pictures the first pass produces.

### 6. Open questions for the owner

1. Grading changes how every future picture looks, though not its framing. Freeze the
   current look as v1, stamp a look version into the label, and grade from here on; or
   keep the record's pictures ungraded and grade only film frames? (Recommended: the first.)
2. Linear colour space costs two days, a full reimport on every worker copy, and a re-tune
   of every dial. Worth it for correct falloff forever, or leave gamma alone?
3. R1 and R2: may the top ten metres read as lit water, and may guild hue enter through
   backlit translucency? Both are the dark-field rule's edges, and both are where the
   largest "alive" gains sit.
4. D4, drawn eyes: an ornament that says so, or not at all?
5. D6, inherited skin genes: the only item that makes a clade visible as a clade, and the
   only one that touches `Evosim.Core`. For the videos, or after the campaign?
