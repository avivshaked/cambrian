# The theatre: what will bite you

Split out of the root [`CLAUDE.md`](../../../CLAUDE.md) on 2026-09-30 so that it is read only when work reaches this area. Read before taking a picture, filming, replaying a run in the theatre or working on its interface. `unity/CLAUDE.md` holds the rules for running Unity itself.

The root file's rules still apply. The entries below were moved word for word, so their dates, their "until" clauses and any reference to "this file" or "CLAUDE.md" are as they were written in the root file.

## Commands

**Watch a run** (D075's first cut). The theatre is a separate program from the farm: it reads
a run directory and writes nothing into it. Open `unity/Assets/Scenes/Theatre.unity` in the
Editor (rebuild it with `Evosim/Rebuild Theatre Scene`), put a run directory — or the arm
directory above it — in the Theatre Runner's `Run Directory`, and press Play. **Mode B** rebuilds
the world from `config.json` plus `run.json`'s seed and step and steps it with rendering on; the
HUD's identity check compares `alive`, `births`, `deaths`, `auditResidual` and `meanHeight`
against the run's own `stats.jsonl` at every sample, so a viewer knows whether they are watching
the run or a cousin of it. **Mode A** grows one genome from a snapshot row and drives it under its
own brain, alone, with no economy. Both refuse a run this build did not record unless
`Allow Source Mismatch` is ticked, in which case the overlay says it is not a faithful replay.
**The skin** lives beside them under `unity/Assets/Theatre/` and moves no hash, so it cannot
orphan a recording: dark-field lighting and fog, rounded meshes generated at start, a neck at
every joint, marine snow and a sand bed, all from `TheatreSkin.cs` and `TheatreMeshes.cs`.
`TheatreBody.shader` is the one body material, carrying the guild on a Fresnel rim over faked
subsurface and a Voronoi mottle. Since the second day it also **carves** each body inward by two
octaves of noise in the part's own object space, deepened at a joint anchor, with the normal
rebuilt per pixel from the same field. The depth is `EVOSIM_THEATRE_CARVE` (default 0.2, clamped
at 0.5) times the part's smallest half-extent, and the displacement is never positive, which is
the whole of what keeps a visual inside its collider. A sphere part is drawn as the genome's
three half-extents scaled so the longest semi-axis is the collider's radius: the genome's intent,
not the physics, which collides as the ball (`research/theatre-look/README.md` is the reading).

**An agent has no eyes, so it takes pictures.**
`./scripts/theatre-snap.ps1 r35tsmoke3 -At 300,600 [-Worker 6] [-Views side,top] [-Size 900x1600]`
runs `Evosim.Theatre.EditorTools.TheatreSnapshot.Run`, which replays the run in Play mode with the
identity check on and writes the box from the side, the end, the top and a corner at each named
second into `scratch/snaps/<arm>/`, each frame fitted from the run's own config and labelled with the
arm, the second, the living count and whether the replay is faithful. A world view at thirteen pixels a
metre cannot show a wrinkle. So a fifth view, **`close`**, frames the largest body and the
largest of its neighbours from a three-quarter angle at a distance that fills the frame, with no
box and no markers. It is a portrait of one crowd and never a census, it is never in the default
set, and `-Carve` sets the carve depth for the run. Its Unity command line is
`-batchmode` **without `-quit`** (the entry quits itself) and **without `-nographics`** (a picture
needs a graphics device); `Evosim/Theatre/Snapshot Now` takes the same four of the world on screen. **A worker's `Assets/Theatre` goes stale silently**: `new-worker.ps1` copies it with the rest, but
nothing checks it at a render the way `simHash` checks the simulation, so a worker refreshed before
a skin change replays faithfully in the old skin, and refuses a view it has never heard of (four
renders on 2026-09-10 died in a minute on `'close' is not a view`). Refresh the render worker
first; `Assets/Evosim` is unchanged by it, so the recording still replays.
**Nothing is committed to `theatre-look-merged` while a render chain runs.** Each step checks that
the worker's seated head contains the branch, so a commit made mid-chain, even to a document,
makes every later step refuse (2026-11-02, two films of phase100). A commit for the branch goes to
the theatre lane, which lands it between chains.
**`-From snapshot` draws the frame instead of replaying it** (2026-09-18,
`logbook/specs/snapshot-render-spec.md`). The genomes come from `snapshots/NNNNNNNNN.jsonl` and the
places from `positions.jsonl`, joined on the organism id. A picture of any second of any run then
costs seconds instead of a re-simulation from the first. Two things it cannot recover, and every
frame says so on a line reading `RECONSTRUCTED FROM SNAPSHOT`. No file holds a body's orientation,
so every body is drawn upright in the developer's own frame. None holds how far it had grown, so
every body is drawn at its adult size. Only a second the run wrote a snapshot at can be drawn. The
`close` view and `-Chrome` are refused in this mode, and the pictures carry `-recon-` in the name
before the view. **A picture may read a run recorded before a tunable** (§11 of the spec, the
owner's ruling of 2026-09-18). Picture-only readers take the water's shape from a config the
strict reader refuses, and the bodies' plans from a genome of format 4 or 5. Both are tried after
the strict readers, and the label's first line then ends `· OLD-RUN READ`. The rule that loading
refuses rather than defaults is untouched for everything that simulates.
**The picture-only config reader must take every development limit, or a picture prunes
what the run carried.** `PictureConfig` read five of `DevelopmentLimits`' seven until
2026-09-25 and left `floorsWeighRigidGroups` off, so every picture of round 48 on a build
that refuses its config (every film window and reconstruction after D124) dropped every
3 cm bud under `minPartVolume`: the story's leaf with a stomach was drawn without it, and
the first film, on the strict reader, had it (`9f5bfe8`). A development limit added to Core
is added to `PictureConfig` in the same change; nothing checks it. The same holds for anything
else a picture draws from the config: the snow drifts by the current group and sinks by the
nutrient sink, so `PictureConfig.ReadTheCurrent` reads both, or a refused run's motes drift
differently from the same run read strictly.
**The theatre reads an older run with a proven-off switch filled as off** (the coordinator's
ruling of 2026-11-02). A new tunable makes the strict reader refuse every config written before
it. `RunRecord` then asks it again with that switch filled as off, but only for a switch listed in
`RunRecord.ProvenOffSwitches`. Each listed switch points to its recorded proof: with it off, the
world is the one that ran before it existed (for `biteNeedsMouth`, r66's check 2 and the crowd
fixture re-record). The filled config describes the world that ran, so the label
stays as it is and the identity with the reference stills keeps its meaning. Each fill logs one
`[Theatre] config fill:` line naming the run and the switches, and the stored hash's mismatch says
why. **Any theatre code that reads a run's config again strictly calls
`RunRecord.ReadConfigFillingProvenOff`, never `RunDirectory.ReadConfig`.** A direct read falls to
`PictureConfig` on an older run and loses what only the strict reader carries: r65's water read
directly lost its current, so the snow stopped drifting and the blades' flutter changed while
the label and the bodies stayed right. A switch with no such proof stays refused, and the old-run
reader takes the run. Everywhere else, the farm, Core and the nursery included, loading still
refuses rather than defaults.

**A run is filmed from a checkpoint** (2026-09-23 evening, the owner's request for clips at
5,000, 15,000 and 30,000 s): `./scripts/theatre-film.ps1 r46-s1 -At 5000 [-Worker 6] [-Shots
orbit,close,drift] [-Seconds 60] [-Fps 30] [-Turns 0.25] [-MotionBlur 0]` runs
`Evosim.Theatre.EditorTools.TheatreFilm.Run` (same launch shape as the snapshot: `-batchmode`,
no `-quit`, no `-nographics`), which restores the checkpoint at or before the second in live
mode, steps the world itself one frame interval at a time, renders each shot through the
snapshot's RenderTexture read-back with the interface off but the provenance word, and
writes `scratch/films/<arm>/<s>/<shot>/frame-NNNNNN.png`; the script then runs ffmpeg to
`scratch/films/<arm>/<arm>-t<s>-<shot>.mp4`. Every clip is a labelled cousin (live play on
the Editor's Mono), which the owner accepted for filming. Three things bite. The film's
clock is the physics step count since the restore, because `live.ElapsedSeconds` moves in
half-second metabolic steps and put five frames on one instant. Each shot has its own
camera, because the grade's motion blur drew the jump between shots on a shared one; the
blur is tuned for the fly camera at screen rate and smears any fast move at a low frame
rate, so `-MotionBlur 0` for those. And a full orbit at the tank's radius is metres a second,
far past the owner's rule that nothing moves faster than a body swims, so the default is a
quarter turn. The `drift` shot compiled and was never filmed. The film pins
`Time.captureDeltaTime` to one frame interval, so the shaders' clock (the caustic net, the
shafts, the ripples) advances one frame per capture whatever the wall clock does; the log
counts the intervals that missed. Three diagnostics come with it: `-Freeze` (the world is
not stepped, so a clip's only motion is the camera's), `-Raw` (the colliders as the physics
has them, the runner's X key) and `-Trace` (one row per link per frame in `trace.tsv` beside
the shot directories: the solver's pose, the view's transform, every visual's mesh and
scale, the screen position under the close shot's camera; `scripts/film-trace.py`
ranks the jumps). They are the order to use them in. **From the evening of 2026-09-23 the
shots carry a fixed reference** (the owner: nothing in the first clips said whether the
camera or the creatures moved). An orbit whose lift the surface caps stays level at the
crowd's depth and aims five degrees below the centroid, so the bed and the far glass fill
the lower frame; its ring shrinks until every point of it clears the bed by two metres
(the beach's shoal rises into a ring at the glass's room) and its turn is cut to hold the
arc under 0.5 m/s, so a 60 s orbit at a 60 m ring is about six hundredths of a turn. The
close shot is held still, a quarter further back than the following framing, for
`-CloseSeconds` (20 s; a drifting subject leaves a still frame in tens of seconds);
`-CloseFollow` restores the follow and the dolly. A moving shot's eye is smoothed over half
a second, so a push off a body is a glide and not a quarter-metre jump. The plan line in the
log says what was shrunk and cut, and the tally line what pushed.
**No reconstruction and no live frame dressed a sphere or a capsule until 2026-09-23.**
`TheatrePalette` and `TheatreMeshes` knew the engine's primitives by the names `Sphere` and
`Cylinder`, which `CreatePrimitive` gives them in `PhenotypeBuilder`; `SnapshotWorld` and
`LiveWorldView` borrow them through `Resources.GetBuiltinResource`, where Unity 6000.5 calls
them `pSphere1` and `pCylinder1` (the cube is `Cube` either way, so boxes were right). A
replay of a Unity recording was dressed as designed, and every snapshot picture and every
live frame since 2026-09-18 kept the raw meshes, drew a sphere part as the collider's ball
and never as the genome's three half-extents, gave no joint its pinch, and drew a capsule's
shaft as a disc standing out of its carved caps (the caps are cut a quarter of the way in,
the shaft's carve is bounded by its own thickness), which flashed wide for one frame when a
tumbling body carried it through edge-on: the owner's bulge in round 46's first films. The
clock, the motion blur and a frozen world were excluded first; the trace named the body and
its mesh names in one line. The meshes are recognised by reference and by both names now,
and a capsule whose half-span is under its radius is drawn as one ellipsoid at the part's
centre, inside the capsule (`TheatrePalette.Dress`). Every reconstruction picture before
the fix carries the raw look; and a body identified in a picture is identified by the
trace's screen column, not by which id the shot log names, since the crop that showed the
bulge held neither of the close shot's two named bodies.

**The underwater look is a switch, off by default** (`TheatreLook.cs`; the build brief
`logbook/specs/underwater-look/brief.md`). `EVOSIM_THEATRE_LOOK=3` turns on one sun bearing,
absorption and in-scatter over the water's part of each ray, light by depth and by the run's own day
(read from `stats.jsonl`'s `dayFactor`, which carries round 57's weather), raymarched shafts, snow
shells around the camera and the window's overcast; each item also has its own dial, and the tuning
dials (`EVOSIM_THEATRE_SCATTER` 0.02, `_ABSORB`, `_PHASE_G`, `_MARCH_STRENGTH`, `_LOOK_METER`,
`_DAY_GAMMA`, `_METER_DAY`) are read once at dress. `EVOSIM_THEATRE_LOOK=4` adds the second batch
(`logbook/specs/underwater-look/brief-2.md`). Far things fade into the backdrop, which has a gain
under one for an eye in the water (`_FADE_BACKDROP`, `_FADE_START`, `_FADE_SCATTER`, `_WATER_GAIN`).
A hue-keeping tone curve runs after the post-processing for a camera in the water (`_TONE_STEP`,
`TheatreToneStep`, queued per camera with no change to the renderer asset). The shafts are soft sheets
from a low-frequency field at the surface, shaded by the reefs' caps (`_SHAFT_PLACE` and its dials).
`EVOSIM_THEATRE_SHAFT_DEBUG=1` draws the shaft term alone over a black frame and `=2` the same without
the caps, so a difference of the two stills shows the caps' shade. The shells' snow and the bed's dusting come
from the run's `fields/` dumps (`_RUN_SNOW`). The snow drifts with Core's current at the camera by the
world's seconds (`_SNOW_DRIFT`), from a `CurrentField` built afresh from the config and never the live
world's. **Films take the owner's snow of 2026-10-27**: `EVOSIM_THEATRE_SNOW_QUANTITY=0.3`, `SNOW_LEAST=0`,
`SHELL_GAIN=0.4` and `SNOW_AMBIENT=1.6`, fewer and dimmer motes, set in the film look a launcher passes. The
dials' own defaults are unchanged, so a dials-off check keeps the old snow. A film drives the drift by its own continuous second, the one its label prints, passed to
`CapturePlaced`. The world's second moves in half-second metabolic steps, and a drift read off it
jumps every twelfth frame at 24 fps. The label then names the snow's source. The census and solo stills keep the old look
whatever the dial says, so `logbook/images/` stays comparable, and the label names the look that drew
a frame. **A clip is checked frame to frame, never on a contact sheet alone**:
`python scripts/film-motion.py <frames-dir> [--strip <frame> <out.png>]` measures the change between
consecutive frames in the snow, the shafts and the bodies, and no frame may change more than twice the
median. Its bands are placed for the creature close-up at 1920x1080 and scaled for other sizes; move them
for another shot. A sheet of frames 1.6 s apart passed the snow's half-second jump, and the owner saw it in
the clip. A batch import parses a shader without compiling it: `Evosim.Theatre.EditorTools.LookShaderCheck.Run`
compiles every theatre pass for d3d11, and its log carries three errors from a shader broken on
purpose (`Look Check Broken`), which are the check working.

**A film takes the agreed film look, whose one source is `scripts/theatre-film-look.json`** (D290). Its `wide` section
holds for every shot, `close` is added for a portrait or a close shot, and `events` marks births, bites and deaths.
`trial` lists dials under A/B that no film takes until a ruling moves them up, and `previewOnly` the previews' own. Each
entry names its ruling, and the values are read there, never from a list kept here. `theatre-film.ps1` and
`theatre-safari.ps1` set the look through `Use-FilmLook` (`scripts/theatre-film-look.ps1`), which reads the main
checkout's copy from any worktree. When it cannot find that copy, it reads the worktree's own and warns. It fills only the dials the environment leaves unset, and names each dial a caller
holds at another value, so that no film departs from the look unseen. A film departs only for a need of its own, such
as a show filter or a comparison, and says which dial it changes and why. A change to the look is a change to the file,
with its ruling. Unity's dials not in the file stay at their code defaults, and many of those are not 0.

**Two dials ignore the environment in `theatre-safari.ps1`.** It clears `EVOSIM_THEATRE_STORY_LOOK` and
`EVOSIM_THEATRE_SAFARI_CANOPY` at its start, then takes them from the film look or its own switches (`-StoryLook`,
`-NoStoryLook`, `-Canopy`). A value a caller sets in the environment for either is dropped. D327's first meter runs
came out with the story look off for that reason. A change to either goes in the file, or a run passes the switch.
`theatre-film.ps1`'s C# never applies the story look, so D327's exposure meter does not reach a film made that way. The
interactive Editor reads no film look at all: it takes the environment it was opened with and the code's defaults.

**A logged NullReferenceException on opening a window is survived.** `TheatreRunner.OpenWhateverModeSays`
(`TheatreRunner.cs:431`) logs one through `Debug.LogError` and the run carries on and renders. An error check that stops
on any exception line stops a good run, as phase184's first try did. Stop on compile and shader errors, and require
frames in every scene.

**The films do what the safari does, unless the owner has ruled otherwise for that one thing** (the owner, 2026-11-03).
Both read the same file, the safari its `wide` and `events` sections, so a dial entered in either of those reaches
both. `close` is the films' alone. Some of the safari's looks, dials and effects are set
elsewhere, in `SafariPlans.cs`, `TheatreSafari.cs` or its launcher. Each is entered in the file too, without asking,
and the owner is told after. The safari and the films differ only where the owner has ruled it, and the file's entry names
that ruling.

The blades' two stops are per world (D256), from their world's row in `scripts/theatre-world-stops.tsv`. Both
launchers pass their arm to `Use-FilmLook -World`, which takes the row named by the arm, or by the arm without its
trailing seed, so `r62cf-6202` reads `r62cf`. A world with no row takes the file's stand-in, and the reader warns. Each row is set so
that the blades land on the underwater photographs' hue, 102 to 108, in that world's own light. A stop chosen on one
world's light does not carry to another: trial d's stops, chosen on the test stage, read tan at hue 90 in r62cf's light.
A world with no row is calibrated before it is filmed. `scripts/theatre-blade-stops.py` measures a frame's blades and
gives the stops that move them to hue 105, saturation 0.40 and lightness 0.25. The new stops are rendered, measured
again and entered as a row. The small round producers are a different alga from the blades, with a stop of their own
and `ALGA_ROUND_PHOTO` left at 0.

The owner liked the portraits of round 63b's creatures (2026-10-29, `scratch/owner/bloom-round-creature-portraits/`).
They were drawn in the wide shot's skin rather than the close look, at the Portrait station from a farm film window.
That was the film look at `ONE_SKIN=3` with `SKIN2` off, cropped and enlarged twice. So a portrait of the film may take
either skin, and the two are compared on the same subject before one is chosen.
A limb seen along its length at a grazing angle draws its maze in a plainer tone even at `PATTERN_AA=2`. That is the
accepted look: sampling along the footprint's long axis shows no difference at film distance. A rod that still reads as
plugged into the body needs a different shape at its root, a fillet or a flare. The pattern cannot fix it.

**The normal view draws no hunger, and leans toward realism** (the owner's ruling of 2026-11-01, D276). No body is shaded
by its reserve in a film or any other normal view. A per-body world value drawn as colour belongs in a filter view, behind
its own dial and off in the film look, as the cell view is. `FIXED_TINT` gives every animal one tint and
`FIXED_TINT_PRODUCER` every producer another. The proposed film values are 1 for animals, their sated colour, and 0.07 for
producers, the tint the leaves were tuned in, since a leaf's reserve sits near zero (the mean of r65-c-6203's producers
at 22,539.5 s). They enter the film look when the owner accepts look preview 20. Unset, `FIXED_TINT` leaves each body
shaded by its reserve, which is the hunger view: brightness from 0.45 to 1, saturation from 0.35 to 1 and the rim glow
scaled by the tint. That is also the dials-off frame, so the trunk's identity with rig 6 holds. A film that needs hunger
on screen says so in its brief and leaves `FIXED_TINT` unset.

**A change to the look is compiled in the whole film look before it is judged.** The dials-off check compiles only the
variant with every dial off. It cannot see a symbol used under one keyword and declared under another, which fails only in
the film look's own set of keywords. So every build is first rendered for a few small frames in the film look at
`ONE_SKIN=3`, and again in the close look. Any shader error in the safari's log fails it.

**The dials-off check has two references, chosen by ancestry** (the coordinator's ruling of 2026-11-08). Its portrait
of body 27382 on `r62d-u-6102` is itself a resume of `r61-p6102`. The director's resume fix (`630184b7`) reads the
source's births, so it places 27382 in its clade and films the portrait from the other side. That changes the dials-off
frames by design. `slot-r62kg.ps1` therefore checks a build that contains `630184b7` against `ref-dials-off-resume`, and
any other build against `look-dials-off-rig6`. `seat-ok` names the reference used. The move was proved when `630184b7`
alone matched `a538f33a` with every dial off, while rig 6 differed in all nine frames
(`scratch/film-light/c8/check187/`).

**A `-Check` run writes its own log**, `theatre-safari-CHECK-<arm>.log`, beside the filming log
`theatre-safari-<arm>.log`. Read the check's log. Phase 187 first read the filming log of the pass before, from a build
without the fix, and reported a fault that the check itself did not have.

**Round 62's branching coral is drawn in the rock's look, one mesh a colony** (`TheatreCoralRock.cs`,
`TheatreSkin.BuildCoral`). `EVOSIM_THEATRE_CORAL=0` switches it off. A live world draws its own
`World.Coral`; a film window and a reconstruction grow it from the run's config with
`TheatreCoralRock.PlaceFor`, which logs the digest against `run.json`'s `coralDigest`. A box with
relief builds its landscape bed first (`BedShape.ForBox`), which takes minutes in the Editor and
seconds on the farm. Without the box bed (below) the theatre's own sand is flat for a box, so a
trunk can run up to two metres under its foot. The camera keeps off the coral as it keeps off the reefs
(`FilmPlans.WorldBounds.ReefDistance`). The picture-only config reader reads the coral group and
the fields the placer reads beside it (`PictureConfig.ReadTheCoral`) when the file names a coral,
so a coral run the strict reader refuses (the grazer screen, written before D212's spoilage clock)
still grows its coral, and the digest line says whether it is the farm's.
**A box world's film draws the bed the farm stepped, with `EVOSIM_THEATRE_BOX_BED=1`** (D278, in the
film look). With it off the floor is drawn flat at the box's bottom, while the farm's bed rises and
falls by about 1.5 m, so a body resting on it can sit up to about 0.6 m under the drawn floor or
above it. With it on, `TheatreBoxBed` builds the run's `BedShape`, and the floor and the coral stand
on it. The first build of a run's bed takes about 2.5 minutes. It is cached under
`scratch/theatre-cache/bed/`, keyed by the run's bed inputs and Core's source digest, read back in
under a second, and checked against the shape on a 1 m grid when it is written. The log line
`[Theatre] box bed (D278): ...` says which path was taken. What it fixes on screen is a body lost
under the floor: in the floor folder of 2026-11-01, body 66698's close shot showed empty floor, and
with the dial on it shows the body on the bed. A body the flat floor drew hovering now rests on the
bed, but no film shows the difference. No body casts a shadow, and the close shot looks along the
bed, so resting and a 0.6 m hover read alike from that camera. That is a limit of the lighting, not
of the bed, and showing contact would need a level side view, which is not built.
`EVOSIM_THEATRE_CORAL_LIVE` (0, the stone, by default) lays living tissue over it: a tint per colony,
a corallite in every cell of `EVOSIM_THEATRE_CORAL_POLYP` (0.14 m) with its cup shaded as relief,
paler tips toward the crown; `_CORAL_LIVE_GAIN` scales the tints. At 0 a frame is the stone's to the
pixel. It shows only in near shots: a wide shot's coral is a silhouette in the fog.
`EVOSIM_THEATRE_CORAL_SHAPE` (0 by default) cuts the mesh inward only, so every piece stays inside
the collider the farm used: plate rims lobed and ragged to 0.7 of their radius, branch tips knobbed
and forked. `EVOSIM_THEATRE_CORAL_WARM` (0 by default) swaps part of the living tints for warm ones
in the top few metres (none at 9 m deep, full from 3 m up). Both are a picture and nothing else;
at 0 a frame is the one without them. `CORAL_SHAPE` and `CORAL_LIVE` each take a further look at 2,
laid over the look at 1; at 0 and 1 every frame is the one from before, and the new shader terms sit
behind properties that only a 2 sets (`_CoralForm`, `_Bare`). `CORAL_SHAPE` at 2 tapers each branch
to its child's radius and a tip to 0.55 of its base, swells each prong of a tip into an axial polyp
with a cup, and thins each plate toward its rim, all inward. The shader adds corallite bumps along
the branches (3.5 cm apart) and growth rings and radial ridges on the plates, as relief only.
`CORAL_LIVE` at 2 draws each colony's tissue and tips from a palette taken from the reference board,
on a stream of its own (most brown or olive, some green, a few pink, a few with blue tips),
with the warm crowns over it as before. The lower, older parts of each colony show a dull off-white
bare skeleton, from an age the mesh carries in its second texture coordinate.
No colony draws the palette's mauve, whose share is 0: under the blue-green water a purple or mauve
tint reads blue-violet, however muted, and two such colonies sat in the close shot.
`CORAL_SHAPE` and `CORAL_LIVE` each take a bold look at 3, laid over the look at 2; at 0 to 2 every frame
is the one from before, as the bush is built only at 3 and the shader's new term sits behind `_Vivid`,
which only a 3 sets. `CORAL_SHAPE` at 3 grows a bush inside each piece: a branch's own core thins to 0.4
of its radius, about nine sprigs per radius of its length grow from the core toward the capsule's surface,
three to five splayed prongs with an axial cup crown each tip, and each plate thins and drops to the
bottom of its slab under a mat of short upright sprigs. Every sprig and prong ends at least 6% of its
piece's radius (or half thickness) inside it. The bush triples the coral's vertices, to about 4.5 million
for round 62's 30 colonies. `CORAL_LIVE` at 3 keeps level 2's hue per colony and its bare foot, with the
tissue about six times brighter (a luminance near 0.2) and a little more saturated, and cream, lime or
blue-white tips near 0.5. The pale tips follow a tip reading the bush writes into the second texture
coordinate, so they show only with `CORAL_SHAPE` at 3 too. The fog still takes a far colony to the
water's value, and the polyp tufts take the tip colour, so at 3 they are as bright as the tips.
`EVOSIM_THEATRE_CORAL_POLYP_TUFTS` (0 by default) puts the polyps out on living tissue at either level: a star
of pale translucent tentacles in each corallite's cup, wavering a few degrees on `EvoTime()`, and a faint soft
fringe at the silhouette, none of it on bare skeleton or stone. It is shader only, behind `_Polyps`, which only
the dial sets, and the tufts fade out where a corallite falls under a few pixels.

**The realism work is judged against photographs of real marine life** (the owner's guidance, 2026-10-24). A new
look or dial is shown beside the reference board in `scratch/owner/realism-refs/`, one folder per category and
every file's source and licence in its `sources.csv`. It is compared there with the animal or tissue it stands for,
not only with the theatre's earlier look. The board holds public, licensed photographs fetched as reference, and
none of them is vendored into the project.

**Preview first** (the owner's rule of 2026-10-27). A new look item reaches the owner first as one still pair, the dial
off against on, at film distance and beside a reference still; clips and series of takes follow for the items kept. The
preview goes to the coordinator, who tells the owner, and the work carries on in order without waiting for an answer,
until the owner reviews it and stops or redirects it.

The look of every cell type lives in one table, `TheatreTissue`: its colour, sheen, transmission and the three
numbers that carve its surface. A new cell type gets one entry there. A type with no entry is drawn as structural
and named once in the log. Today's entries reproduce the look from before the table. The guild colours in
`TheatreRunner`'s inspector no longer drive the drawing; a colour is changed in the table.

**A dial that is off must leave today's arithmetic as written, in the same order.** Under Mono, moving a sum into a
helper can round one bit differently. The bloom then spreads that bit into stray pixels, and the dials-off check
fails. This happened in b44, when the palette's seed sums were moved into helpers
(`logbook/specs/theatre-skin-plan.md`, ONE SKIN). Code that the off path shares with a dial stays inline, and only the
dial itself uses the helper. Branches that the off path never takes can still move a pixel: the shader compiler may
emit different code for the parts once such branches are in the source (b46). When a dial's build fails its dials-off
check that way, its shader lines go behind a keyword that only its own material enables, with no bisecting first (the
coordinator, 2026-10-25), as the one skin's are (`_ONE_SKIN`, below), the nose pit's and the algal blade's.

Two dials, both off by default, change how tissue looks; at 0 a frame is today's to the pixel.
`EVOSIM_THEATRE_TISSUE_LOOK` makes `TheatreTissue.Of` answer from a second set of entries, one look per cell type
drawn from the reference board (a cream villous absorber, a maroon glossy biter, ivory structure, a glassy float, a
brown holdfast, a pale nerve, a banded muscle link, and today's leaf a little wetter). Each entry also carries the
body's lightness, the rim's colour, strength and sharpness, the light through it (tint and translucent half
extent) and the mottle, which the palette sets per renderer only while the dial is on. At 2 the dial answers from
a third set, the bold looks: a peach-orange absorber, a scarlet biter with an orchid rim, a green-gold leaf, a
glassy float with a bright rim, a pale blue-white nerve, ivory-sand structure, an olive-brown holdfast and a banded
pink-red link, at a lightness of 0.26 to 0.66 against the first set's 0.12 to 0.30, each with its own saturation
cap (`TissueLook.Saturation`) where the first set takes the palette's 0.45. The warm ones are redder than the board,
because the light by depth takes red first; the comment over the bold looks in `TheatreTissue` says what the light,
the water and the grade do to a colour. Below 2 the dial draws the first set. `EVOSIM_THEATRE_SSS` makes
the light through a part depend on its thickness along the view and the key, tinted more deeply the thicker it
is; makes the reserve's glow come from inside (brightest looking straight through); and reads the sheen off a
normal turned back toward the uncarved surface, at 0.6 of its strength. The white flecks on dark bodies in round
62's dives are that sheen catching each flat terrace of the carve: they stay under the tissue look alone and go
under `EVOSIM_THEATRE_SSS`. Each new shader term sits behind a branch on
`_TissueLook` or `_Sss`, never behind a multiply by zero, and the palette never sets either with its dial off.

The bold looks (`TISSUE_LOOK=2`) also carry papillae and a frilled edge, from three `TissueLook` fields
(`Papillae`, `PapillaeCellsPerMetre`, `Frill`) set on `_Papillae`, `_PapillaeCellsPerMetre` and `_Frill` only at 2, each
behind a branch on a value over zero. Papillae are shading alone: a dome per feature point of a jittered grid in the
part's own metres tilts the normal and marks the colour (a paler tip, a darker base), and they fade out where their
spacing falls under about three to seven pixels. No vertex moves for them, because the mesh's vertices are far apart
beside a papilla. The frill is a cut on a blade (the width across the blade shrinks toward the midrib between
scallops, so the outline stays inside the box) and a pleat in the rim's shading on any other part, since a notch on an
opaque body would need a dropped pixel that the depth passes would not share. The absorber and the biter carry
papillae strongly, the structure, holdfast and link lightly; the leaf and the absorber carry the frill.

`EVOSIM_THEATRE_SOFT` (0, off) draws a body as one soft skin rather than a stack of flats, and at 0 a frame is
today's to the pixel, with either tissue dial on or off. The carve keeps its lobes but is cut to three quarters of
its depth, its wrinkles weigh half, and its stretch is spread over a wider band, so few points sit on a plateau and the walls
between them are gentle (`SoftCarve`). The mottle's cells are two and a half times as wide and its marking gentler.
The carved normal is turned part of the way back toward the uncarved one, and most of the way near a joint anchor,
where two parts meet. The rim's power is lowered, so the rim falls off softly, and its strength is unchanged. Each
scales what the tissue look sets when that dial is on, and the material's own values when it is off. The carve
still only cuts inward, so a visual stays inside its collider. The terms sit behind a branch on `_Soft`, which only
the palette sets, and only while the dial is on. The constants (`SoftCarveDepth`, `SoftWrinkleGain`, `SoftBand*`,
`SoftMottle*`, `SoftRimPower`, `SoftSmooth`, `SoftSeamSmooth`) live in `TheatreBody.shader`. At 2 the body is one
smooth hull as well, laid over the soft skin: the carve is cut to 0.3 of the soft depth with its wrinkles at 0.15
of the soft weight, which keeps a faint lobe so a body is not an ellipsoid; the normal is turned back again, about
seven tenths of the way over the body and all but the whole way at a joint, so parts that overlap read as one
skin; and the mottle is fine and faint. Those terms sit behind a branch on `_SoftHull`, which the palette sets
beside `_Soft` only at 2, so below 2 the dial draws what it did. Their constants are the `Hull*` ones beside the
soft dial's.

`EVOSIM_THEATRE_ORGANIC` (0, off) draws a box part's outline as an animal's or a plant's, so a slab reads as a pebble
or a flatworm's body. At 0 a frame is today's to the pixel. Before the taper and the bend, the section across the
long axis is scaled toward that axis by one factor in (0, 1], the product of four terms. The section is rounded toward
a superellipse of power 2.5 in the two coordinates over their half extents, so a square section turns round and a flat
one elliptic. The last half of each half length closes on an elliptic arc to 0.55 of the section at the very end. The
section swells to its full width at a peak and falls to 0.88 at both ends. The peak sits within 15 % of the length of
the middle, and one end leans across the axis by up to 0.14. The peak, the lean's direction and its end come from
`_Carve.x`, so they cannot flicker. The long axis keeps its length, every term only shrinks the section, and the clamp
still comes last, so the drawn box stays inside its collider. The normal is rebuilt from the map's Jacobian, composed
with the taper's. Spheres, capsules and leaves are left alone. The terms sit behind a branch on `_Organic`, which only
the palette sets, and only while the dial is on. The constants (`Organic*`) live in `TheatreBody.shader` beside
`OrganicFactor`.

The organic dial also lobes the absorbers, so a stomach reads as a fleshy sac and not as a carved block. While it is
on, the palette multiplies an absorptive part's lobe gain (`_Carve.y`) by `OrganicAbsorberLobes` (1.6) in
`TheatrePalette`, and touches nothing else. The gain is a weight in the carve's mix of lobes and wrinkles, so the lobes
take more of the field and the wrinkles less. The depth is still `_CarveFraction` times the guild's take, capped by
`_CarveMaximum`, so the carve cuts no deeper than before and still only inward. With the dial off the value set is
the table's.

`EVOSIM_THEATRE_ORGANIC=2` lays soft tissue over the organic silhouette, so a box reads as a flatworm's or a
nudibranch's skin and not as a worn pebble or a mussel shell. The dial runs 0 to 2, and at 0 and 1 every frame is the
one without it, to the pixel: the palette sets `_OrganicTissue` only at 2, beside `_Organic`, and the terms sit behind a
branch on it, or on the tissue flag the vertex stage hands the fragment (`TEXCOORD6`). It reaches the parts the
silhouette reaches, the box meshes (`ShapeBody`'s `box`: the mesh's shape channel says box and not leaf), and no
sphere, capsule or leaf. Four terms. (1) The carve's field is `TissueCarve`: the lobes at `SoftCarve`'s wavelength and
offsets, mapped through a smooth stretch with no saturating band (the ledges of `EvoCarve` and `SoftCarve` are that
band), at 0.5 of the depth, through `CarveDepth` so the cap has the last word. (2) Wrinkles in the same field, a
second value noise in the part's metres that is four times as long along the longest axis as across it (the axis
choice is `ShapeBox`'s, through `AtLeast`), with a share of the field from the guild's own wrinkle weight, up to half.
(3) Pores are shading alone: `PapillaField`'s dome read as a dimple, 250 to the metre, tilting the normal and
darkening the body a little, fading out between three and eight pixels of spacing; and the sheen's exponent falls from
28 to 9 and its gain to 0.45, a broad soft glow. (4) A part counts as flattened when its smallest half extent is at
most 0.55 of its middle one (`AtMost`'s tolerance), and its thickness then falls to 0.4 of itself toward the rim, from
0.6 of the way out, while the rim ruffles along the thin axis in three to six waves round the perimeter, phase from
`_Carve.x` and `_Individual` (`TissueMargin`, run first, before the organic map). A box whose sides are under 0.4 of
each other is drawn as a leaf and never reaches term 4, so the margin reaches the boxes from 0.4 to 0.55. The margin
outside the collider is 0: terms 1 and 2 cut inward only, term 3 moves nothing, and term 4 thins the half thickness
and ruffles by at most half of what it gave up, so the thinned half thickness plus the ruffle is under the box's own
before `ShapeBox`'s clamp, which still has the last word. The box mesh has 24 segments to a face edge, which carries
the margin's fall and the ruffle as geometry and the crease noise only to eight cycles an object unit
(`TissueCreaseMaxCycles`); the per-pixel normal shows the rest. The constants (`Tissue*`) live in `TheatreBody.shader`.

`EVOSIM_THEATRE_BLEND` (0, off) draws a joint between two boxes as one tissue flaring into the next, and at 0 every
knuckle is sized as before, to the pixel. The parent side's knuckle becomes a flare (`TheatrePalette.Flare`). It is the
knuckle's ellipsoid on the same face axis, its centre sunk into the parent by `FlareSink` (0.35) of its half length.
On each cross axis it leaves the parent's face a blend radius r wider than the child. It closes onto the child's side
at most r up from the face. The radius r is `BlendShare` (0.25) of the smaller of the two
parts' smallest half extents, and never more than `BlendMaximum` (0.02 m). The cross radii are capped at nine tenths
of the anchor's clearance to the parent's side faces, as the knuckle's are. The reach into the parent is the knuckle's
own length, or less where the narrower axis's rise asks for less. The honesty margin is r. With the joint straight,
the flare is outside both colliders only in a ring, at most r above the parent's face and r beyond the child's side. In a bend, the dome above the face shows by no more than the knuckle's own exception. Only a
box parent with a box child takes the flare, and neither may be drawn as a leaf. A sphere's or a capsule's parent
knuckle keeps its rules, and a round child or a leaf would leave a ring standing off it, so those joints keep today's
knuckle. The child side's knuckle and the bridges are unchanged.

`EVOSIM_THEATRE_COUNTERSHADE` (0, off) grades a body's own colour from dark above to pale beneath, as a swimming
animal is, and at 0 every frame is the one without it, to the pixel, with any other dial on or off. The factor on the
colour runs through a smooth curve of the world-space normal's y, read before the carve so a wrinkle does not flicker
between dark and pale: `CountershadeTop` (0.8) facing straight up, `CountershadeUnder` (1.1) facing straight down, and
no channel is taken over one by it. A blade takes `CountershadeLeafShare` (0.5) of the grade. It follows the world's up
and not the body's, so a body that rolls keeps its dark side up. It touches the body colour alone, which is `body` in
the fragment before the light: the sheen, the rim, the glow and the light through the tissue are left as they are. A
neck is graded by its own normal (a leaf's neck at full strength, since it is a sphere mesh and not a blade). The
constants live in `TheatreBody.shader` beside `Countershade`.

`EVOSIM_THEATRE_SKIN_MAP` (0, off) draws a body's look from ten numbers computed from its genome and not from the plan
hash (`TheatreSkinMap.cs`, `logbook/specs/theatre-skin-plan.md` section 5), and at 0 every frame is today's to the pixel.
`TheatreSkinMap.Register` develops the genome at its adult size under the run's own limits, computes 26 features (cell-type
shares, body-plan measures, investment, margin, brain measures), standardises them by the numbers in the generated
`TheatreSkinMapConstants.cs` (the reference is the non-producers alive at 24,000 s in ten worlds pooled, named in its header with
each world's count; a producer is a body whose photosynthetic share of volume is 0.5 or more. `scratch/skin-map/port/gen_constants.py`
writes it from `projection.json` and `weights.json`, and redoing it is a ruled change), shrinks each brain weight marker by
n / (n + 4) for a brain of n connections, clips every z to plus or minus 3, and takes the logistic of each parameter's biased,
weighted inputs. Each parameter's weights and bias are folded with a gain, so the reference's median body sits at 0.5 and its p10
and p90 near 0.1 and 0.9; a fresh fit needs no new term in the C#. Kept by
the body's id, once; the four worlds register next to `TheatrePattern.Register`, and the stills entry through
`TheatrePalette.RegisterForSkinMap`. A body with no entry is drawn without the map. Ten parameters, each on a range that puts
the reference median near today's constant. A producer is drawn as it is without the map: `TheatreSkinMap.TryGet` gives no
vector for one, since its colour is the alga look's, set per world by the owner's ruling of 2026-10-29, and the re-centred
saturation and carve turned the blades khaki. Its vector is still made and logged. A non-producer's base hue picks a
hue on the marine palette, a straight sweep of the wheel from 220 degrees (blue) through violet, magenta and red to 50 degrees
(gold, at 410), with no green or cyan; 0 is the blue end and 1 the gold end. The body's main tint is the colour of the cell type with the most
volume weighted by that colour's saturation, leaving out photosynthetic tissue (`SkinMainTurnOf`). The turn is the palette hue less
the main tint's hue, by the short way round, and every colour of the body (parts, rim, light through, necks) takes that one
turn, hue alone (`Turned(..., hueOnly: true)` keeps saturation and value exactly), so its tissues keep their hues' places beside
the main tint. A part the alga look recolours keeps the producers' narrow turn. The base saturation scales the body's saturation cap from 0.55 to 1.45; the regime and the scale choose
the pattern tile from the vector (`TheatrePattern.SolvedForMap`, the nearest of today's nine bins, the scale as a multiple from
0.7 to 1.4) in place of investment and margin, and in place of SKIN2's clade pattern; the rim hue turns the rim band's warm
colour by up to a quarter of the wheel either way and the rim width scales its width from 0.4 to 1.6; the mottle strength is
the soft mottle's gain, 0.6 to 2.2; the iridescence scales the tissue's strength 0.2 to 1.8; the countershade is twice today's
grade, capped at 1.6; the carve character multiplies the guild's carve depth 0.6 to 1.6. The noise phase and the carve's seed
keep the plan hash. **It acts through the dials it replaces from, so those must be on:** `RIM_BAND`, `MOTTLE_SOFT`,
`IRIDESCENCE`, `COUNTERSHADE` and `PATTERN` (the film look sets only the second and the last). The shader's side is
`_EVO_SKIN_MAP`, a `multi_compile` keyword in all three passes that `TheatreSkinMap.EnsureKeyword` turns on with the first
vector, with `_SkinMap` and `_SkinMapOn` per renderer (`RimBandMapped`, `CountershadeMapped`, the soft mottle's gain); off, the
keyword is never enabled and the source the compiler sees is today's. The taper is not driven: it is one value for the material
that the one skin's geometry and the knuckles also read. `EVOSIM_THEATRE_SKIN_MAP_LOG=1` logs one line a body when its vector
is first computed, `skin map: id <id> v <ten, six decimals> f <the features>`, for the check against `skin_vectors.csv`. A
still of given genomes: `TheatreSoloStill` (`EVOSIM_STILLS_LIST`; `scratch/skin-map/port/make_stills_list.py` builds the list),
painted under each row's own id with the dial on. **A solo still draws the body's parts through `PhenotypeBuilder`, which has
neither the one skin nor the family mesh**, so `ONE_SKIN` and the close look draw the same still: a comparison of skins, a mouth on
the skin or a one-skin-only mark (`_FAMILY_SPOT`) is filmed as a live portrait (a safari scene from a film window), never as a
solo still. The dial 2 is the hybrid hue: 1 is the map above, unchanged, and 2 changes two of
its ten values after they are computed. The base hue becomes the middle rank of a lineage score among the fitting set's scores,
the score being a weighted sum of the same clipped, shrunk z values over the features that are not diet shares, and the base
saturation becomes the mean of today's saturation and today's diet logistic. The numbers are in the generated constants
(`Hybrid*`, from `scratch/skin-map/hue-screen/hybrid.json`, the rule of `hue_screen.py`'s hybrid).

`EVOSIM_THEATRE_SKIN_MAP_TURN_ALL` (0 to 1, 0 off) gives that share of a body's palette turn to the animal colours that do not
take it. Read by the trace of a solo still under the film look, almost every colour of an animal already does: the guild colour,
the body's muted colour (muted from the turned guild), the rim, the light through the tissue and the glow (the tissue's own colours,
turned) and the pattern's pigment, which the shader works out from the body's colour. The one constant that does not is the rim
band's warm yellow-orange on a consumer's box parts (`RimBandWarm`), which only the map's rim hue turned. So the dial adds
`TurnAll` times two pi times the palette turn to the rim hue written in `_SkinMap.y` (`TheatreSkinMap.WriteTurnAll`, after
`Write`), for a wide-hue part in colour; no shader code or keyword is involved, and the rotation about the grey axis follows an HSV
hue turn only approximately. A producer, a part the alga look recolours, a corpse and a greyed body are not touched. The colour
ramp's tips and the chromatophore pigment are warm constants too, but their dials are not in the film look and the dial leaves
them. With the dial at 0 the palette's hook is one test of a static `false` and nothing is written.

`EVOSIM_THEATRE_SKIN_MAP_SAT_FLOOR` (0 to 1, 0 off) puts a floor under the base saturation that `TheatreSkinMap.SaturationGain`
reads for a non-producer: the gain is `Lerp(0.55, 1.45, max(base saturation, floor))`. It raises the body colour's saturation cap,
which binds on the pale tissues (bone, nerve, the bold absorber: their guild saturation is above `look.Saturation` times the gain),
and does nothing for a tissue whose own saturation is under the cap. At 0 the method returns the line it always did.

`EVOSIM_THEATRE_SKIN_MAP_DEEPEN` (0 to 1, 0 off) brings the lightness a non-producer's body colour is muted to down toward a
ceiling: `L' = L - Deepen * max(0, L - 0.42)` (`TheatreSkinMap.Deepened`, ceiling `DeepenCeiling`), so a tissue paler than 0.42
(bone, nerve and the absorber sit near 0.64 to 0.66 in the bold set, and read pastel in any hue because `Muted` fixes the lightness
and only caps the saturation) comes down by the dial's share of the way and a darker one is untouched. The palette applies it to
the three lines in `TheatrePalette.Set` that hand a lightness to `Muted` for the body colour (the plain `BodyLightness` and the
tissue look's `look.Lightness`, both saturation branches), for a wide-hue part in colour (`wideHue`, in colour, not greyed). The rim and the light through the
tissue take their own lightness (the tissue's `RimColour` and `TransTint`, or `RimLightness`), and the glow follows the rim, so
they do not move; the maze pattern's pigment is worked out from the body colour in the shader and follows it. A producer, a part the
alga look recolours (frond lightness `AlgaFrondLightness`), a corpse and a greyed body are not touched. At 0 `DeepenOn` is false and
the palette's one local reads false.

`EVOSIM_THEATRE_SKIN_MAP=3` is full colour (the owner's ruling of 2026-10-30), and `EVOSIM_THEATRE_SKIN_MAP_VALUE` (0 to 1, 0 off)
is its value axis. Mode 3 runs mode 2's hybrid unchanged and puts a non-producer's base hue on the whole wheel,
`Repeat((start + 360 * base hue) / 360, 1)`, the start `EVOSIM_THEATRE_SKIN_MAP_WHEEL_START` (`WheelStartDegrees`, 0 to
360, default 165, read only in mode 3), instead of the marine path, which modes 1 and 2 keep. It also gives
each entry a value rank (`Entry.ValueRank`, NaN in the other modes, not one of the ten values): the middle rank of a second
lineage score among `ValueFittingScores`, computed from the same clipped, shrunk z values as the hue score. The second score is the
generalised eigenvector with the second-largest eigenvalue of the hue score's own problem, so over the fitting set it is
uncorrelated with the hue score; its numbers are `value.json`, written by `scratch/skin-map/full-colour/fit_value.py` and put in
`TheatreSkinMapConstants.cs` (`Value*`) by `scripts/skin-map/gen_constants.py` with `value.json` as its fifth argument. With the value
dial over zero in mode 3, a non-producer's body colour takes `s = Value * (2 * rank - 1)`: for `s < 0` its lightness becomes
`L * (1 + 0.85 s)`, for `s > 0` `L + (0.95 - L) * 0.7 * s` (`TheatreSkinMap.Valued`), applied after the deepen dial at the same three
`Muted` calls and under the same condition, plus a vector. The rim, the light through the tissue and the glow keep their own lightness.
Both are off by default and identical at 0: `ValueOn` is a static false unless the mode is 3 and the dial is over zero, so the
palette's new local reads false and no line of it changes. Mode 3 logs `value <rank>` at the end of each `SKIN_MAP_LOG` line; modes
1 and 2 log as before.

`EVOSIM_THEATRE_SKIN_MAP_OKLAB` (0 or 1, 0 off, only in mode 3) levels the colours in OKLab, because HSL lightness is not perceived
lightness (yellows, limes and cyans render near-white at the L where magentas sit much darker) and because the value dial moved the body
alone, so the rim and the light through kept their lightness and the darkest body was nowhere near black. `TheatreSkinMap.Level` takes
the body, rim and light-through colours of a wide-hue part in colour with a vector, as the gamma-encoded sRGB `Muted` made them, sets the
body to the OKLab L `EVOSIM_THEATRE_SKIN_MAP_OKLAB_L` (0.3 to 0.9, default 0.68) moved by the value rank (`s = Value * (2 * rank - 1)`; below zero `T * (1 + 0.6 s)`, above
`T + (0.95 - T) * reach * s`, the reach `EVOSIM_THEATRE_SKIN_MAP_OKLAB_PALE_REACH`, 0 to 1, default 0.7), adds the same shift to the rim's and the light through's L, clamps L to [0.02, 0.98], caps each chroma at
`EVOSIM_THEATRE_SKIN_MAP_OKLAB_CHROMA` (0.05 to 0.35, default 0.14; all three read only while the levelling is on), and bisects chroma down where a
colour leaves the sRGB gamut. The palette calls it once, after the tissue-look block and before the alga look, where all three are
final. Under it the value dial acts through the levelling and not through the HSL lightness: the palette's `valued` local is false
while `OkOn` is true. Off by default and identical at 0: `OkOn` is a static false unless the mode is 3 and the dial is on, so the two
new palette lines read false and nothing changes. The shader's light adds to the target: at the defaults a body renders at
about CIELAB L* 84 (v5b sheet, `logbook/specs/skin-map-hue/`), and the pale end at value 1 washes out to near white.

`EVOSIM_THEATRE_SKIN_MAP_AXES` (0 or 1, 0 off, read once) gives every colour property of a non-producer its own heritable axis, each a middle rank of one eigenvector of hue_screen's (Q, S) problem (hue the first, value the second, chroma the third, sheen the fourth; `scratch/skin-map/full-colour/v6/fit_axes.py`, `TheatreSkinMapConstants.ChromaWeights`/`SheenWeights`). `AxesOn = Mode == 3 && OkOn && Axes > 0.5`, so it needs the levelling on, and every new line is a test of it. `Entry.ChromaRank` and `SheenRank` exist in mode 3 (NaN elsewhere) and are logged after `value` on the `SKIN_MAP_LOG` line. While on: (1) `PaletteHue` places the base hue on an arc from `EVOSIM_THEATRE_SKIN_MAP_ARC_START` (0 to 360, default 170) forward through 360 to `_ARC_END` (default 55), 245 degrees at the defaults, skipping green, which stays the plants'; (2) `Level`'s target L is `lerp(_LIGHT_DARK, _LIGHT_PALE, ValueRank)` (defaults 0.12 and 0.90; 0.02 to 0.6 and 0.5 to 0.98), replacing `OKLAB_L` and the value nudge, with the rim and light through moving by the same shift; (3) every one of the three colours has its chroma set, not capped, to `_CHROMA_MAX * ChromaRank ^ _CHROMA_POWER` (defaults 0.22 and 1.5; 0.02 to 0.37 and 0.25 to 4), keeping each colour's hue and the gamut bisection, so a low rank is really grey and a vivid one vivid; (4) a wide-hue part in colour that is not dead, aged or out of focus, neck included, has its sheen multiplied by `lerp(_SHEEN_LOW, _SHEEN_HIGH, SheenRank ^ _SHEEN_POWER)` (defaults 0.5, 4 and 2) and clamped to [0, 1], in `TheatrePalette` before the block's `_Sheen` write (`TheatreSkinMap.Sheened`); the shader's `_Sheen` range is 0 to 1 and its highlight is linear in it, so only that clamp and, if on, the `SHEEN_CAP` soft knees limit the white. (5) `EVOSIM_THEATRE_SKIN_MAP_BROWN` (0 to 1, default 0): a body whose palette hue h (degrees, after the arc) is in the warm band has `b = Brown * w`, w a raised cosine about 42 degrees with half-width 30 (1 at 42, 0 at 12, 72 and outside); in `Level`, after the axes have set L and the chroma, L becomes `lerp(L, min(L, 0.38), b)` and every colour's chroma `lerp(C, 0.075, b)`; the rim and light through still move by the body's shift. Off by default and identical at 0, and modes 0 to 2 and mode 3 without AXES compute what they did. The debug line `skin map axes on` names every range.

Three multicolour dials turn the one body colour into several, all read once, all 0 by default and identical at 0, and each active only while `TheatreSkinMap.AxesOn` (mode 3, the levelling and `AXES`), so each new line is a test of a static false. Every hue shift is made in arc space, along the axes arc between `ARC_START` and `ARC_END`, never round the whole wheel, so no limb, tip or spot lands in the green the arc skips: a position on the arc is mapped by `PaletteHue`, and a colour turns by the difference of two palette hues, the short way round, carried over as an OKLab hue angle (approximately; the two wheels are not the same wheel). (1) `EVOSIM_THEATRE_SKIN_MAP_PART_COLOUR` (0 to 1) moves each part's three colours from the body's by the part's place `t` (its `DistanceFromRoot` over the body's largest, 0 for a body of one part) and its own elongation `u = 1 - min/max` of its half extents. In `Level`: the target L gains `PartColour * dL * PART_LIGHT * t` (`_PART_LIGHT`, 0 to 1, default 0.45; dL is +1 for a value rank under 0.5, so a dark body gets paler tips, else -1), and a lift stops at `_PART_LIGHT_MAX` (OKLab L, 0 to 1, default 0 and off; never below where each colour stood without the lift, a darkening untouched), held for the body, the rim and the light through each, because a pale link lifted near 1 clips to white once lit and the rim and light through ride the lift from above the body's lightness; the part's arc position is `p = base hue + PartColour * (dH * PART_HUE * t + PART_NODE * (2u - 1))` (`_PART_HUE`, 0 to 1, default 0.6; `_PART_NODE`, 0 to 0.5, default 0.25, both shares of the arc; dH is +1 for a chroma rank of 0.5 or more, else -1), folded into [0, 1] by reflection (1.1 is 0.9) so a limb stays continuous with its trunk, and body, rim and light through turn by `PaletteHue(p) - PaletteHue(base hue)`. `t` and `u` reach `Level` by optional parameters that are NaN (no shift) when not given; the palette fills them at the part paint call, and a neck or bridge is not given them and takes the body colour. (2) `EVOSIM_THEATRE_SKIN_MAP_PATTERN_TINT` (0 to 1) draws the skin pattern's pigment in a second colour, the shader's `_PatternTint` (rgb the colour, alpha the share, default 0 and so no change; declared in the Properties block and the shared `UnityPerMaterial` buffer beside `_Pattern`, and lerped into the pigment at the four sites that darken the body colour for it: `PatternSkin`'s pigment and its skin-join pigment, and the `dark` pigment of `PatternSkin2` and of `JoinPigment2`; a Skin2 `pale` pigment is not tinted). The tint's hue is the body's own hue (before the part shift) turned along the arc to `q = Repeat(base hue + PATTERN_HUE, 1)` (`_PATTERN_HUE`, 0 to 1, default 0.5, a share of the arc, wrapping, not reflecting; 0 keeps the body's hue), its OKLab L is the part's body L flipped across the middle by `PATTERN_FLIP` (`_PATTERN_FLIP`, 0 to 0.8, default 0.45; below 0.5 `min(0.95, L + flip)`, else `max(0.05, L - flip)`), its chroma `max(body chroma, 0.10)` bisected into the gamut, scaled by the part's brightness. The palette writes it on every part's block while the dial is on (alpha 0 for a part that is not a wide-hue part in colour, so no tint leaks through a reused renderer block). The one skin carries it without change: `TheatreOneSkin` copies the largest part's whole block (`source.GetPropertyBlock`) and zeroes only the per-part object-unit properties, so `_PatternTint` (and the rim and countershade `_SkinMap`) reach the film's renderer as the largest part's one value, uniform over the skin. (3) `EVOSIM_THEATRE_SKIN_MAP_COUNTER_BOOST` (0 to 1) raises `CountershadeShare`'s maximum to `lerp(1.6, 3.0, boost)` and its per-unit to `lerp(2, 4, boost)`, for a darker back and a paler belly; `EVOSIM_THEATRE_SKIN_MAP_RIM_CONTRAST` (0 or 1) gives a wide-hue part's rim band the body's palette turn plus half a turn (`TheatreSkinMap.WriteRimContrast`, which rewrites `_SkinMap` as `WriteTurnAll` does, with angle `RimHueRadians + turn * 2 pi + pi`, after turn-all's write, so it wins where `TURN_ALL` is also on). Each dial that is on logs one line from `EnsureKeyword`, naming its values and the arc-space rule.

The pattern families draw a mark over a body in a second colour, the zone hue at a strong lightness contrast (`TheatreSkinMap.FamilyColour`), each dial read once and only while `AxesOn`, 0 by default and identical at 0. `EVOSIM_THEATRE_SKIN_MAP_FAMILY` (0 to 1) is the marks' strength, `_FamilyColor`'s alpha and `_FamilyParams.y`. Alone it gives each body one family by its value and sheen ranks (`FamilyOf`: 0 the maze only, 1 white bars, 2 an eyespot, 3 a dark edge outline), and `_FAMILY_FORCE` (-1 to 3, default -1) puts every body in one family, for tests only. Four dials tune them. `_FAMILY_EDGE` (0 to 1) widens the outline, carried in `_FamilyParams.w`. `_FAMILY_LIGHT` (0 to 1) moves the mark colour's OKLab L toward the middle (0.88 to 0.70 on a dark body, 0.14 to 0.30 on a pale one), so the zone hue shows rather than white or black. `_FAMILY_CHROMA` (0 to 0.3) sets the mark's chroma in place of `ZONE_CHROMA` when it is over 0. `_FAMILY_SPOT` (0 or 1) centres a one skin's eyespot on the body's largest part (`TheatreOneSkin` writes `_FamilySpot`), and a body drawn as parts is unchanged. `EVOSIM_THEATRE_FAMILY_SPOT_SOFT` (0 to 1, default 0; `TheatreSkin`, the global `_EvoFamilySpotSoft`) fills the eyespot's black core with the mark's colour by the dial, so the spot is a ring and a disc of one colour, a mark rather than an iris round a pupil. The eyespot's size has no dial: its radius is 0.35 of the body's thickness (or the largest part's, under `_FAMILY_SPOT`), the core half of it. At 0 the global is never set and the core's line multiplies by exactly one. `_FAMILY_MIX` (0 or 1) makes the families combinable. The maze, the bars, the eyespot, the outline and the colour zones each have their own heritable gate, the mean of two of the entry's ranks over a threshold fitted to the population (`scratch/skin-map/full-colour/v7d/gates.py`: about 65, 30, 25, 30 and 40 % on, 13 % with no mark, and kin sharing the whole mix 85 % of the time against 11 % for strangers; no two gates share a rank's sense, since the edge gate once read value beside the maze gate's 1 - value and one modest difference in value turned 40020's maze off and its outline on together). Bars, eyespot and outline reach the shader as a bitmask in `_FamilyMix.x`, and `FamilyMask` draws the largest of the selected marks' shares. Each mark's share has its own function, which the single-family path calls with the same expressions. The maze gate zeroes the body's `_Pattern.y` at the palette's two pattern writes (`TheatreSkinMap.MazeStrength`), and the zones gate keeps the body's own hue where the zone hue would turn it (`ZoneGateOn`). `_FAMILY_FORCE` at 0 or more turns the whole mix off. `SKIN_MAP_LOG` adds `mix maze bars eyespot edge zones` as five 0/1 flags to each body's line while the mix is on.

`EVOSIM_THEATRE_SKIN_MAP_FAMILY_EDGE_BEND` (0 to 1, default 0) keeps the edge outline to where the surface bends. The outline is a rim on the view angle (`FamilyEdgeShare`), so on a flat part seen at a glancing angle it covers the whole face, which read as a glassy wash (40020, 54035). Over zero, `WriteFamily` puts the dial in `_FamilyMix.z` and the share is scaled by `lerp(1, saturate(bend * EdgeBendScale), dial)`, with `bend` the length of `fwidth` of the normalised world normal and `EdgeBendScale` 25. On a flat face the normal hardly changes from pixel to pixel, while at an edge or a silhouette it turns fast on screen. `bend` is taken in the open beside `familyDPos`, outside the family branch, since a derivative inside a branch is undefined. It is a screen measure, so a body far away bends more per pixel than the same body close. At 0 the share is the rim alone and every pixel is as before; it acts only under the mix, where `_FamilyMix` is written.

`EVOSIM_THEATRE_SKIN_MAP_FAMILY_PINK_AWAY` (0 to 1, default 0) moves the pattern families' mark colour out of hot pink, which it lands in on about a fifth of the animals once it is not near-white. Read once and only while `AxesOn`, as the other family dials are. Over zero, `FamilyColour` takes the mark's OKLab hue, after the turn and before the colour is made, and where it lies in the pink band (`PinkCentre` 340 with half-width `PinkHalfWidth` 50, so 290 to 30 wrapping through 0; a pale mark anywhere from 315 to 25 still reads as pink) moves it toward the band's nearer edge. The weight is a raised cosine, 1 at the centre and 0 at the edges and outside, and the hue moves by the dial times the weight times the angular distance to that edge, so at 1 the centre goes all the way to an edge and a hue near an edge barely moves. A hue exactly at the centre goes to 290 (violet-blue), not 30. The zone parts' own hue and the body's hue are unchanged: only the mark's is moved. At 0 the line is not run and `FamilyColour` is the function without it, so every pixel is today's; the `skin map axes on` line gains a clause for the dial only when it is over zero. **It does not take the marks out of pink.** Most warm marks sit near the coral edge, where it moves them a few degrees: v7e2's measured at hue 10 to 22, and the share of bodies within 30 degrees of pink rose from 9 of 30 to 14. A pale warm mark reads pink at any angle, so lightness is the lever, not hue.

`EVOSIM_THEATRE_SKIN_MAP_FAMILY_PINK_DEEPEN` (0 to 1, default 0) darkens the warm marks instead. Read once and only while `AxesOn`. Over zero, `FamilyColour` moves a light mark on a dark body (body OKLab L under 0.5) from `FamilyLightDark` toward `WarmDeepL` 0.55 by the dial times `WarmWeight(hue)`: 1 within `WarmFlat` 0.75 of the band's half-width from `WarmCentre` 350, falling by a raised cosine to 0 at `WarmHalfWidth` 60 (so 290 to 50 through 0), 0 outside. The hue is kept, after `PINK_AWAY` if that is on, so kin still match, and a pale salmon or rose mark goes rust, brick or terracotta. A mark on a pale body is already dark and is unchanged. At 0 the line is not run and every pixel is today's; the `skin map axes on` line gains a clause only when it is over zero. **Darkening alone reads coral-red, not rust:** v7e3's marks went to L 0.65 to 0.73 and chroma 0.15 at their own hue of 13 to 20, and the count within 30 degrees of pink went only from 14 to 13 of 30.

`EVOSIM_THEATRE_SKIN_MAP_FAMILY_RUST` (0 to 1, default 0) turns the coral side toward rust, for use with `PINK_DEEPEN`. Read once and only while `AxesOn`. Over zero, after `PINK_AWAY`, a mark's OKLab hue from `RustFrom` (= `PinkCentre`, 340) through 0 to `RustHue` 50 moves toward 50 by the dial times the way there (`RustTurn`), ramped in by a raised cosine over its first `RustRamp` 10 degrees. That is a linear squeeze that keeps 1 minus the dial of the side's spread and stays monotonic: at 1 every coral mark lands on 50 (the overshoot), and at 0.6 the side spans about 37 to 50. It starts at 340 because `PINK_AWAY` already splits the band there, sending marks below it toward violet, so hues either side of 340 were already apart; starting lower would pull kin near 320 to 340 across 90 degrees. `PINK_DEEPEN`'s weight is taken on the hue before this turn. At 0 the line is not run; the `skin map axes on` line gains a clause only when it is over zero. `scratch/skin-map/full-colour/v7d/rust_map.py` tabulates the combined mapping.

`EVOSIM_THEATRE_COLOUR_RAMP` (0, off) changes a part's colour smoothly along its long axis, from the end that joins its
parent (the base, which keeps the tissue's colour) to its free end (the tip, where the body colour is multiplied by the
tissue's `TissueLook.RampTip`). The tip is one entry per tissue in every look set: leaf (1.20, 1.12, 0.78), a yellow-green
tip; absorber (1.25, 1.15, 0.95), paler and warmer; biter (1.25, 1.20, 1.15), a lighter margin; link (1.28, 1.20, 1.20);
and structure, nerve, float and holdfast (1.10 each), a small paling. None moves a channel by more than about 30 %. The
palette gives each visual the axis (`RampOf`, `_RampAxis`), in object units like the pinches, so a capsule's shaft and
caps share one ramp. The long axis is chosen from the genome's half extents through the tie tolerance (a capsule's is its
Y), and the base is the end the part's own anchor (`ChildAnchorLocal`) sits at, when that anchor is out at least half
the half length along the axis. A root, or a part whose anchor is on its side or middle, takes a fixed end from the
part's seed (`RampEndMix`), as the organic dial's lean takes its end from `_Carve.x`. A blade reads its own base-to-tip
coordinate (`leaf.x`) instead. Necks carry no ramp, so one at a part's tip end is the tissue's base colour against a
tip up to 30 % off it. The term sits behind a branch on `_RampTip.w`, which only the palette sets, and only while the
dial is on.

`EVOSIM_THEATRE_LEAF` (0, off) draws a blade (the lamina path) as a living leaf, and at 0 every frame is the one without
it, to the pixel, with any other dial on or off. Three terms, each behind a branch on `_LeafLook`, which only the palette
sets and only while the dial is on, and each on a blade alone (a neck of leaf tissue is a sphere mesh and is left as it
is). (1) No facets: the blade's carve is `LeafCarve`, the lobes of `TissueCarve` (`SoftCarve`'s wavelength and offsets)
through a smooth monotone stretch (`LeafCarveStretch` 3, gentler than the tissue's 5) with no band, no terrace and no
wrinkle octave, at `LeafCarveShare` (0.4) of the depth the blade would have had after its own cap of half the lens's
thickness there, so at most 0.2 of the lens's half thickness; it cuts inward only, in [0, 1] times that depth. The
fragment's rebuilt normal asks the same field (`CarvedNormal`'s flag). The midrib, the lens's ridge and the shading's, stays.
(2) Light through the sheet: when the key is behind the face the camera sees (minus the dot of the blade's normal before
the carve and the key's direction, a smoothstep from `LeafBackLow` 0.15 to `LeafBackHigh` 0.85) the blade adds its
tissue's light-through tint pushed warm (`LeafBacklitTint`, 1.15, 1.05, 0.62) under the key and the depth's light, at
`LeafBacklitGain` (0.9) times the lens's thinness to the power `LeafBacklitFalloff` (1.5), so the rim glows most and the
midrib, whose veins already take thickness down, least. Light only. (3) A margin that ruffles out of plane: in
`LeafPoint`, so the normal follows by `ShapeLamina`'s differences as the curl and the frill do. The whole sheet moves
along its thin axis by a wave round the outline (the perimeter coordinate, base to tip along one margin and back along the
other), 6 to 12 whole waves from `_Carve.x` and a second harmonic at twice the count plus one (0.3 of the amplitude),
growing by a smoothstep from nothing at `LeafRuffleStart` (0.55) of the way out across the blade to the full amplitude at
the rim, and faded at the stalk. The amplitude is `LeafRuffleAmplitude` (0.08) of the blade's half width. The honesty
bound: it is at most `LeafRuffleRoomShare` (0.5) of the room, and the room at a point is the box's half thickness less the
lens's own half thickness there less the curl's lift there (`LeafRuffleCurlShare`, 1). So the lens, the curl and the
ruffle together are inside the box wherever the curl alone is, and the margin the ruffle adds outside the collider is 0. A
blade whose curl already fills its box, a wide thin one, has no room and does not ruffle; set the curl share to 0 to bound
the ruffle by the lens alone, and the curl and the ruffle together may then pass the box where the curl alone does. The sway
dial's room takes the other half (`LeafRoom` is halved under this dial), so the two together are inside as well.
`EVOSIM_THEATRE_LEAF=2` adds two things on top of level 1, which is unchanged at 0 and 1: the palette sends the level in
`_LeafLook` and the shader tests it above 1.5. The margin by growth mixes a finer wave into the ruffle toward the rim, a
convex mix `(1 - m) coarse + m fine` with `m` rising to `LeafRuffleFineShare` (0.65) from `LeafRuffleFineStart` (0.8) of the
way out, the fine row `LeafRuffleFineRatio` (2.6) times the coarse count and whole, so the wave stays in [-1, 1] and the
bound above holds as it was. Kelp relief puts rows of blisters (`LeafBull*`: every 0.12 of the length, one row a side
between 0.14 and 0.86 of the way to the rim, steepest tilt 0.3, a hollow on the lower face) in the normal on a blade whose
length is over `LeafKelpRatio` (2.5) times its width; no vertex moves. The constants (`LeafCarve*`, `LeafBack*`, `LeafBacklit*`, `LeafRuffle*`) live in `TheatreBody.shader` beside `LeafCarve`,
`LeafScallop` and the fragment's `Velvet*` block.

`EVOSIM_THEATRE_ALGA` (0, off) draws a photosynthetic blade (the lamina path) as a seaweed or a seagrass and not as a
land plant's leaf. At 0 every frame is the one without it, to the pixel, with any other dial on or off. The palette then
sets none of `_Alga`, `_AlgaDeep` and `_AlgaShallow` and changes no colour, brightness or sheen, and every shader term
sits behind a branch on `_Alga` or `_AlgaDeep.w`, whose defaults are 0. The palette sets `_Alga` on a photosynthetic part
alone: one on a living one, `AlgaCorpse` (0.25) on a corpse, which keeps the form and takes that share of the sheen and
the light through. (1) The form comes from the blade's own length over width (its two larger half extents), smoothly and
with no hash in the character (`AlgaShapeOf`, `AlgaFormOf`); the part's seed sets only phases, where a wave starts and
which face a cup or an arch bows toward, and `_Individual` nothing. A strap, from `AlgaStrapLow` (2.5) to `AlgaStrapHigh`
(3.5), has parallel sides `AlgaStrapWidth` (0.92) of the box's half width and a taper only at the stalk, over
`AlgaBaseHalfWidths` (1.5) half widths. Its tip closes in a half circle in metres, frayed inward by `AlgaFrayDepth`
(0.1, swinging between 0.4 and 1.2 of that along the edge). It twists: the sheet moves along its thin axis by its distance across times a sine along its length
(`AlgaTwistsPerAspect` 0.15 waves a unit of length over width, `AlgaTwistAmplitude` 0.12 of the half width at the rim). A
sheet, under `AlgaSheetLow` (1.5) to `AlgaSheetHigh` (2.1), is sin(pi s) to the power 0.5, broad and round, and its
margin ruffles with the leaf dial's level 2 margin by growth even with that dial off, at the leaf dial's amplitude times
how far the blade is from a strap. Between, the two outlines blend by the form (0 a sheet, 1 a strap, half way between
the two ramps). The ruffle's waves round the outline are `AlgaRuffleWavesBase` (4) plus `AlgaRuffleWavesPerAspect` (2) a
unit of length over width, from 6 to 16, the two whole counts either side blended by the fraction, so the count moves
smoothly and each wave still closes at the base. The curl's weights come from the form (cup 0.35 to 0.12, arch 0.15 to
0.3, frill 0.5 to 0.2, sheet to strap) scaled to add up to `AlgaCurlBudget` (0.6). No lobes (`LeafScallop` at zero depth and the
outline's lobes at zero), no veins and no midrib ridge in the lens or the shading (`LeafVeins`), but for a faint kelp
midrib at `AlgaKelpMidrib` (0.5) of the leaf's on a strap more than `AlgaKelpLow` to `AlgaKelpHigh` (4.5 to 5.5) times as
long as it is wide. (2) The colour: a producer's base colour comes from a family of four sRGB anchors joined by straight
lines, at 0, a third, two thirds and one: `AlgaGreen` (0.42, 0.62, 0.34, the guild's green turned 20 degrees to yellow
and less saturated), `AlgaOlive` (0.47, 0.45, 0.24), `AlgaGolden` (0.60, 0.45, 0.22) and `AlgaRed` (0.50, 0.24, 0.22).
`AlgaColour` takes the family's hue and saturation and scales its value by the guild colour's over 0.72. The place
along it is the proportions' alone (`AlgaPositionOf`: 0.1 for a sheet to 0.5 for a strap, plus up to 0.15 as the
thickness over the width rises from 0.5 to 1.0). A round blade, one whose length over width is under `AlgaRoundLow`
(1.2) and gone by `AlgaRoundHigh` (1.5, both in the shader and the palette, which must move together), moves to
`AlgaPositionRound` (0.42), an ochre between the olive anchor and the golden one, the colour of Padina's fan, by how
round it is. The depth darkens and never turns the hue: the palette sends one factor on all three channels,
`AlgaDeepShade` (0.6) at the floor and `AlgaShallowShade` (1.0) at the waterline, and the shader applies it to the
body, the rim and the light through, piecewise linearly by the visual's centre's share of the way from the waterline to
the floor of the world's water box (`AlgaDepthTint`); before a skin has pushed the box the share is a half, and there is
no tint. The factor is equal on the three channels because a per channel one cuts red and reads as mint or teal. The
rim and the light through are muted to a fixed HSL lightness of 0.58 and 0.6, which over the family's low saturation is
a pastel, so for a producer they take the guild colour's hue, its HSV saturation times `AlgaRimSaturationGain` (1.25,
at most `AlgaRimSaturationCap` 0.7) and the muted value times `AlgaRimValue` (0.92) (`AlgaRich`), tissue look or not. The
lineage's turn, the reserve's saturation and the grey apply to it as to any guild colour, and a greyed body takes no
depth tint. A neck of producer tissue takes its part's family colour and depth tint, and no form. (3) The
surface: a wet sheen, `AlgaWetGain` (0.22) at the power `AlgaWetPower` (9), broader and softer than the body's 28, over
the tissue's own; and the transmission's strength on a blade times `AlgaThroughSheet` (1.45) on a sheet to
`AlgaThroughStrap` (0.9) on a strap, every transmitted term at once, each still by the lens's thinness. On a thin sheet
(the sheet form, not a round fan and not a strap) a second light through, `AlgaSeeGain` (1.0) of the first, is added
in the blade's own colour (its body's hue at full value, taken to the one and a half and warmed half way to
`LeafBacklitTint`) and cut per channel at the rim's brightest (the rim colour, `_RimStrength` and the blade's 0.45 of
it, under the same water cuts), so it never outshines the rim. A round blade is a fan: `AlgaBands` (6) faint rings of
tone, `AlgaBandDepth` (0.14) either way, centred on the stalk end of the blade (`AlgaBandTone`), and its margin, the
outer `AlgaRollWidth` (0.15) of the way out, rolled toward the face its cup bows toward by up to `AlgaRollHeight` (0.12)
of the half width at the edge, at most `AlgaRollRoomShare` (0.25) of the room times how round it is, which the ruffle
gives up (its share is 0.5 less 0.25 times the strap's less 0.25 times the roundness), so the ruffle and the roll stay
inside the share the ruffle had. (4) A stalk part
that hangs a blade by a chain of only children, up to four long, draws at `AlgaStipeShade` (0.7) of its brightness, and a
float whose parent or child is a blade draws in the olive anchor with a sheen of at least `AlgaBladderSheen` (0.45), a
gas bladder; colour and gloss only, and the stipe is not drawn rounder. (5) A free-drifting blade, one of a body with no
anchoring part (`HasAnchoring`), is drawn as a Sargassum-like frond; an anchored blade keeps the drawing above. The palette
flags a frond by `_AlgaShallow.w` of one, zero on every other part, and keeps the flag on a greyed or dead one, which keeps
its form. `AlgaShapeOf` reads it and marks the shape `l.alga` 2 (every other test of `l.alga` is over or under a half), and
the fragment reads it as `AlgaIsFrond`. The outline is a stem, `AlgaFrondStem` (0.08) of the half width, with three rows of broad
blades on each side, the second and third among the first and the outline the widest of the three, so the blades overlap and fill
each other's gaps. The first row has 4 to 7 blades a side by length over width (`AlgaFrondCountA`), the second 1.5 times that plus
one (`AlgaFrondCountB`) at `AlgaFrondSecondSize` (0.85) of the size, the third 2.2 times it plus two (`AlgaFrondCountC`) at
`AlgaFrondThirdSize` (0.65), and the two sides are out of step by `AlgaFrondSide` (0.41, `LeafScallop`'s). A blade is a bump in
the width along the stem: a rise to a pointed apex at 0.6 of its length and a shorter fall, both fuller than a line
(`AlgaFrondRise` 0.5, `AlgaFrondFall` 0.6), cut with fine teeth inward (`AlgaFrondTooth` 0.12 of its width, 2.5 to a blade). Each
blade has its own size (`AlgaFrondSizeLeast` 0.7 to one) and its own length (a row's cells are
stretched and squeezed by `AlgaFrondJitter`, 0.4), both smooth functions of its number and the seed's phases, so the blades are
uneven in size and spacing and nothing repeats at a fixed pitch. Under the blades runs a web (`AlgaFrondWeb`, 0.15 of the
envelope); the envelope is full from 0.18 of the length, shortens by `AlgaFrondEnvTip` (0.3) from 0.7 and closes in a round end
over the last 0.07. On the stem sit 3 to 5 round bladders, discs of radius `AlgaFrondBladderRadius` (0.17) of the half width in the
plane of the blade, at most `AlgaFrondBeadSpan` (0.45) of the gap between two along it, and standing off it by 0.6 of hT at
most (`AlgaFrondBladderShare`) less the arch's lift; they cut the blades away round them. Its lightness differs a little from
blade to blade, by up to `AlgaFrondTone` (0.12) either way: the fragment takes the tone of the blade that makes the outline at
its station and side (`AlgaFrondBladeTone`, which works out the blade counts from the aspect `leaf.w` carries, as
`AlgaFormOfLeaf` does). It has no cup, frill, ruffle, ellipse outline, bands or roll. Its curl is an arch of `AlgaFrondArch`
(0.3) and a droop. Each side hangs out of the plane by the square of its distance from the stem times a slow wave along it
(`AlgaFrondDroop`, 0.12 of the half width at the rim of a full blade, `AlgaFrondDroopPerBlade` waves to a blade, the wave mostly
toward the face the arch bows toward and a little the other way, the two sides out of step). It takes the strap's twist's place
(`l.twistAmp` is zero) and spends no part of the room; the arch and the droop together stay under the 0.2 of the half width that
`TheatreMeshes.LeafBounds` allows beyond the box, and `LeafRoom` takes the droop's most off the sway's room. Its colour is the
family at 0.45 to 0.6 by the blade's own place, the body at HSL saturation `AlgaFrondSaturation` (0.55) and lightness
`AlgaFrondLightness` (0.32) so it reads golden-brown and not mint, and the rim and the light through from the same guild colour
(HSV saturation 0.85, `AlgaFrondGuildSaturation`) by `AlgaRich`. Its wet sheen is `AlgaFrondWetGain` (0.05) at the power
`AlgaFrondWetPower` (24), its light through is `AlgaThroughFrond` (1.0), and the gold see-through light stays whatever its
proportions. The bound is the one above, kept. The blades' width is at most one, only inward, and multiplies the whole width:
`AlgaOutline` returns the stem plus the envelope, or a bladder where that is wider, and `AlgaFray`'s place carries the cut over
it. A bladder's width is at most 0.17, and its thickness replaces the lens there and is at most 0.6 of the box's half thickness
less the arch's lift, so the thickness is inside the box wherever the arch alone is. `LeafRoom` counts that thickness from a
tenth to nine tenths of the length, so the sway keeps what is left. The stem's lens is at most one, and `AlgaFleshCap` takes the
lens at one for a frond. The frond's numbers ride in shape fields a frond does not otherwise use (`AlgaFrondOf`'s comment lists
them), and its constants (`AlgaFrond*`) sit before `AlgaShapeOf` and in `TheatrePalette` beside `AlgaCorpse`. The area a frond
fills on screen is set by `AlgaFrondWeb`, `AlgaFrondSizeLeast`, `AlgaFrondRise` and `AlgaFrondFall`, `AlgaFrondEnvTip`, the two
rows' counts and sizes (`AlgaFrondSecondSize`, `AlgaFrondThirdSize`); the bladders' read by `AlgaFrondBladderRadius`. With the leaf dial on, ALGA's outline, curl, lens,
colour and ruffle count take precedence, and the leaf dial's carve, backlit glow, ruffle amplitude and kelp blisters stay.
The bound is the leaf dial's, kept. The outline is at most one and the fray only cuts in; the lens is never thicker than
the leaf dial's, and a blade's half thickness is capped at `AlgaThinHalf` (0.08) of its half width (`AlgaFleshCap`, in
`ShapeLamina` and `LeafSway`); the curl keeps its budget. The ruffle and the twist share `LeafRuffleRoomShare` (0.5) of the room at
each point, the twist taking `AlgaTwistRoomShare` (0.25) times the strap's share and the roll `AlgaRollRoomShare` times the roundness. `LeafRoom` halves the sway's room under
this dial as under the leaf dial, and reads the outline before the fray. So the lens, the curl, the ruffle, the twist and
the bend together are inside the collider box wherever the curl alone is. The constants (`Alga*`) live in
`TheatreBody.shader` beside `AlgaShapeOf` and `LeafBacklitTint`, and in `TheatrePalette` beside `_alga`; the form's four
thresholds are in both, and the two must move together.

`EVOSIM_THEATRE_ALGA_BLADE` (0, off) draws the free-drifting frond (ALGA's item 5) as one or a few broad blades that fill about
0.8 of the part's face, as sea lettuce and a kelp's blade do, in place of the stem with leaflets. It does nothing unless
`EVOSIM_THEATRE_ALGA` is on too. At 0 a frame is the one without it, to the pixel, with any other dial on or off, and with ALGA on
and this at 0 the fronds draw as ALGA's item 5 says. Every shader line it adds is inside `#if defined(_EVO_ALGA_BLADE)`, under
`#pragma multi_compile _ _EVO_ALGA_BLADE` in each of the three passes, so with those regions and pragma lines removed the shader is
the one without them, byte for byte. `TheatreSkin.EnsureAlgaBladeKeyword` enables the keyword once, only when the dial is on, from
the look's push and from the palette the first time it paints a frond; nothing turns it off. It adds no property and no CBUFFER
entry, and the palette sets nothing new: the frond flag (`_AlgaShallow.w`) is the switch, and each plant's numbers are hashes of the
part's seed (`_Carve.x`; `AlgaBladePlant`, multiplies and fractions with no `sin`, so the vertex and the pixel agree). (1) The
outline is one envelope (`AlgaBladeBound`): a base that fills quickly over `AlgaBladeBaseLeast` to `AlgaBladeBaseMost` of the
length, a body at `AlgaBladePeakLeast` to one of the box's half width, and a rounded tip over `AlgaBladeTipLeast` to
`AlgaBladeTipMost`. Each side is cut inward by `AlgaBladeCut`: a margin that wanders (three sines at 1.7 to 13 waves along the
blade, `AlgaBladeEdgeDepthLeast` to `AlgaBladeEdgeDepthMost` of the width at the deepest, a phase of its own on each side), a side
scale down to `AlgaBladeSideLeast`, and, on a plant with two or three blades (`l.lobe`: the second from a hash over
`AlgaBladeCountSecond`, the third over `AlgaBladeCountThird` and on a part longer than `AlgaBladeCountAspect` times its width), one
or two smooth clefts a side. The width is the envelope times a cut in (0, 1], so it is inside the box's face wherever the frond's
was. The frond's functions (`AlgaFrondWidth`, `AlgaFrondBound`, `AlgaFrondCut`, `AlgaFrondLens`, `AlgaFrondBladeTone`) answer from
the blade under the keyword, `AlgaFrondOf` hands the blade its numbers in the shape fields a frond leaves free (`AlgaBladeOf`'s
comment lists them), and there are no bladders. (2) The blade twists and curls slightly: the strap's twist at `AlgaBladeTwist`
(0.16) of the half width over `AlgaBladeTwistsLeast` to `AlgaBladeTwistsMost` waves, and a cup, an arch and a frill of
`AlgaBladeCup`, `AlgaBladeArch` and `AlgaBladeFrill` (0.55 of the curl budget of 0.6), with the frond's droop and bend as they were.
The margin also ruffles out of the plane at `AlgaBladeRuffle` times the leaf dial's amplitude, bounded by the room as the sheet's
ruffle is, so a part too thin to have room ruffles only in the plane. The curl, the droop and the twist keep to the bound ALGA's
item 5 states. (3) A faint midrib (`AlgaBladeMidrib`, in the lens and in `LeafVeins`) comes in over a length over width of
`AlgaBladeMidribLow` to `AlgaBladeMidribHigh` and is none on a short part. For the light through the tissue the fragment adds
`AlgaBladeGlow` of the see-through light, weighted from `AlgaBladeGlowStart` of the way out to the rim, so the margin glows more
when the key is behind it, up to `AlgaBladeCapGain` of the rim's brightest. (4) The colour keeps the frond's golden-olive family;
each plant leans browner or greener by a multiplier on its light (`AlgaBladeLean`, between `AlgaBladeLeanBrown` and
`AlgaBladeLeanGreen`), and its lightness drifts slowly over the blade (`AlgaBladeToneAmp`, `AlgaBladeTone`) where the frond's
leaflets each had their own. The constants (`AlgaBlade*`) live in `TheatreBody.shader` after `AlgaFrondWetPower`.

`EVOSIM_THEATRE_ALGA_BLADE` at 2 (its range is 0 to 3; 1 is the paragraph above) gives the broad blade three more things, all
shading, so no vertex moves and nothing leaves the collider: a colour for each plant, light through the blade, and a finer ruffle
along the margin. At 0 and 1 a frame is the one without them, to the pixel. Every shader line it adds is inside `#if
defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)`, under `#pragma multi_compile _ _EVO_ALGA_BLADE2` in each of the three
passes that carry `_EVO_ALGA_BLADE`'s, so with those regions and pragma lines removed the shader is level 1's, byte for byte.
`TheatreSkin.EnsureAlgaBlade2Keyword` enables the second keyword once, only at level 2, from the same two places as the first. (1)
Colour (`AlgaBladeColour`): a plant's place on a ramp is its seed hash (`AlgaBladePlant(63)`) blended with the visual's depth
share (`AlgaDepthShare`) by `AlgaBladeRampDepth`, from sea lettuce's yellow-green (`AlgaBladeRampA`, sRGB 0.55, 0.78, 0.22)
through a mid green (`AlgaBladeRampB`) to kelp's olive-brown (`AlgaBladeRampC`, 0.42, 0.36, 0.16). It multiplies the blade's whole
light by the ratio of the ramp's hue to the body's, at the ramp's luminance over the mid green's to the exponent
`AlgaBladeLumaShare`, `AlgaBladeRampShare` of the way, in place of the level-1 lean, and only on a living blade (`_AlgaDeep.w`); a
greyed or dead one keeps the lean and its grey. Within a blade (`AlgaBladeWithin`) the base and tip are paler and a faint zone
along the midrib is darker. (2) Light through: when the key is behind the blade as the camera sees it, the fragment adds the
see-through's hue at full value, more saturated by `AlgaBladeBackSaturation`, at `AlgaBladeBackGain` of the key's direct term
(`keyColour` times `_KeyGain`) at full backlight, falling off as the alignment of the view and the key, bent by the normal as
`Transmission` bends it (`_TransDistortion`), to `AlgaBladeBackPower`, by the blade's thickness and thinness and under the water's
cut of the light through. (3) Ruffle (`AlgaBladeRuffleNormal`): the vertex stage already ruffles the margin out of the plane as
far as the room allows, so this one is in the normal alone, in the outer `AlgaBladeShadeRim` of the half width,
`AlgaBladeShadeWavesLeast` to `AlgaBladeShadeWavesMost` waves round the outline from the plant's hash, tilting the normal by at
most about `AlgaBladeShadeTilt` (rise over run) along the margin. It is laid in the blade's own coordinates and fades out as its
waves near a pixel each. The constants (`AlgaBlade*`) live in `TheatreBody.shader` beside level 1's, with the ramp's functions
after `AlgaDepthTint`.

`EVOSIM_THEATRE_ALGA_BLADE` at 3 (its range is 0 to 3; 2 is the paragraph above) is level 2 and two more things: light through the
thin blade, and a ruffled margin that moves vertices, always inward or within the box's room. At 0, 1 and 2 a frame is the one
without them, to the pixel. Every shader line it adds is inside `#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE3)`, under
`#pragma multi_compile _ _EVO_ALGA_BLADE3` in each of the three passes that carry `_EVO_ALGA_BLADE2`'s, so with those regions and
pragma lines removed the shader is level 2's, byte for byte. `TheatreSkin.EnsureAlgaBlade3Keyword` enables the third keyword once,
only at level 3, from the same two places as the others, and the first two are on then too; its C# lines sit between `// BLADE3
begin` and `// BLADE3 end`. (1) Light through: when the eye looks toward the key through the blade, the fragment adds the
see-through's hue shifted toward yellow-green (`AlgaBlade3Shift`) and brought to full value, at `AlgaBlade3Gain` (the strength) of
the key's direct term at full backlight, falling off as the alignment of the view and the key, bent by the normal as
`Transmission` bends it (`_TransDistortion`), to `AlgaBlade3Power`, by the blade's thickness and by how thin the tissue is at the
pixel (the lens's: full at the margin, `AlgaBlade3ThinMiddle` of that on the midline), under the water's cut of the light through.
It is shading only and reads nothing on the screen. It replaces level 2's back light, which level 3 keeps at
`AlgaBlade3BackKeep` (0), its hue at `AlgaBlade3ThruSaturation` of its saturation. The blade's colour at 3 is calmer too:
`AlgaBladeColour`'s ramp target goes `AlgaBlade3UlvaShare` of the way to the reference's pale yellow-olive (`AlgaBlade3Ulva`)
at the target's own luminance, times `AlgaBlade3Value`. (2) Ruffled margin: in `LeafPoint`, in the outer `AlgaBlade3RimWidth` of the
half width and nowhere else, both parts noise in the blade's own coordinates so they stay on the tissue. The scallops
(`AlgaBlade3Scallop`) cut the outline inward by up to `AlgaBlade3ScallopDepth` of the half width, `AlgaBlade3ScallopsLeast` to
`AlgaBlade3ScallopsMost` rounded lobes a margin with a sharp notch between, the cells jittered and each notch of its own depth;
the factor is in [1 - depth, 1] on the distance across, so nothing is drawn outside the outline level 2 draws. The wave out of the
plane (`AlgaBlade3Wave`) is value noise round the outline, at most `AlgaBlade3WaveHeight` of the box's half thickness and at most
`AlgaBlade3RoomShare` of the room the lens and the curl leave, which is the bound the margin's other ruffle keeps to; `LeafRoom`
takes the same share off the sway's room, so the ruffle, the twist, the wave and the sway together stay inside the box wherever
the curl alone does. A part with no room has no wave and keeps its scallops. The depth passes share `LeafPoint`, so a shadow
matches its blade. The constants (`AlgaBlade3*`) live in `TheatreBody.shader` after level 2's.

`EVOSIM_THEATRE_ALGA_RAMP_PHOTO` (0 to 1, default 0), read once through `TheatreSkin.Dial`, moves level 2's colour ramp toward
photographed sea lettuce, because a broad blade at `ALGA_BLADE` 2 draws a neon green and the cause is the ramp's first two stops. It is
zero unless `EVOSIM_THEATRE_ALGA_BLADE` is at 2 or 3 (`AlgaBlade2On`). By the dial's share, in linear colour, `AlgaBladeRampA` (sea
lettuce) goes toward `AlgaBladeRampPhotoA` and `AlgaBladeRampB` (mid green) toward `AlgaBladeRampPhotoB`: each is its stop's own
luminance at the photographs' chromaticity, so `AlgaBladeRampMidLuma` and the lightness term are unchanged. Kelp (`AlgaBladeRampC`) is
unchanged. `AlgaBladeColour` takes its target again from the moved stops straight after the ramp's lookup, so level 3's pull toward
`AlgaBlade3Ulva` still applies to the moved target, after it. Every shader line it adds is inside `#if defined(_EVO_ALGA_RAMP_PHOTO)`,
nested in `defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)`, under `#pragma multi_compile _ _EVO_ALGA_RAMP_PHOTO` in each of the
three passes that carry `_EVO_ALGA_BLADE2`'s, so with those regions and pragma lines removed the shader is the one without them, byte
for byte. The value is the global `_EvoAlgaRampPhoto`; `TheatreSkin.EnsureAlgaRampPhoto` sets it and enables the keyword once, only
when the dial is above 0, from the same place as level 2's, and the log has one line naming the value and the two stops. Its C# lines
sit between `// RAMPPHOTO begin` and `// RAMPPHOTO end`. At 0 a frame and every C# path are the ones without it, to the pixel.

`EVOSIM_THEATRE_ALGA_RAMP_STOP_A`, `EVOSIM_THEATRE_ALGA_RAMP_STOP_B` and `EVOSIM_THEATRE_ALGA_RAMP_SHARE` tune the photographed ramp
without a rebuild, and are read only while `EVOSIM_THEATRE_ALGA_RAMP_PHOTO` is above 0. A stop is three numbers `r,g,b` in linear colour,
each 0 to 1, parsed in the invariant culture; set, it replaces `AlgaBladeRampPhotoA` or `AlgaBladeRampPhotoB`, the colour the dial moves
that stop toward. The share is one number, 0 to 1; set, it replaces `AlgaBladeRampShare` in `AlgaBladeColour`. A value that is malformed
or out of range is refused with a log line naming the variable, and the shader's constant stands, as it does for a variable left unset.
The globals `_EvoAlgaRampStopA`, `_EvoAlgaRampStopB` and `_EvoAlgaRampShare` are `float4`, `w` 1 when set and 0 when not; the shader tests
`w > 0.5` and leaves the constants' own lines as they are, so with all three unset a frame is the one without them, to the pixel. Their
shader lines sit inside `#if defined(_EVO_ALGA_RAMP_PHOTO)`, with no keyword or pragma of their own. `TheatreSkin.EnsureAlgaRampStops`
sets them and logs the values in force once, naming which came from the environment; its C# lines sit between `// RAMPSTOPS begin` and `//
RAMPSTOPS end`.

`EVOSIM_THEATRE_ALGA_ROUND_PHOTO` (0 to 1, default 0), read once through `TheatreSkin.Dial`, brings the round producers onto the
blades' photographed colour. A round producer is a photosynthetic part drawn as a sphere, capsule or superellipsoid:
`TheatrePalette.DrawnAsLeaf` sends only boxes down the blade path, so these never reach `AlgaBladeColour` and keep the palette's muted
family green. It is zero unless `EVOSIM_THEATRE_ALGA_RAMP_PHOTO` is above 0 (`AlgaRampPhotoOn`), whose stops it reads. At value v,
`AlgaRoundPhotoTone` multiplies such a part's `body`, straight after `body *= algaTint;`, by v of the way from 1 to the multiplier a blade
gets at the ramp's mid-green stop: the same hue ratio, lightness term and share as `AlgaBladeColour`, with the target the mid-green photo
stop (`_EvoAlgaRampStopB` when set, else `AlgaBladeRampB` moved toward `AlgaBladeRampPhotoB` by `_EvoAlgaRampPhoto`) and none of a blade's
own inputs (no `leaf`, no `AlgaBladeWithin`, no plant's place on the ramp). It applies where `_AlgaDeep.w > 0.5`, `input.leaf.w < 0.5`, not
`skinMesh` (a swimmer's merged skin) and not `thallusMesh` (under `_EVO_THALLI`); stipes, bladders and corpses have `_AlgaDeep.w` 0 and
keep their colour. Every shader line it adds is inside `#if defined(_EVO_ALGA_ROUND_PHOTO)`, nested in `defined(_EVO_ALGA_BLADE) &&
defined(_EVO_ALGA_BLADE2) && defined(_EVO_ALGA_RAMP_PHOTO)`, under `#pragma multi_compile _ _EVO_ALGA_ROUND_PHOTO` in each of the three
passes that carry `_EVO_ALGA_RAMP_PHOTO`'s, so with those regions and pragma lines removed the shader is the one without them, byte for
byte. The value is the global `_EvoAlgaRoundPhoto`; `TheatreSkin.EnsureAlgaRoundPhoto` sets it and enables the keyword once, only when
the dial is above 0, straight after `EnsureAlgaRampStops`, and the log has one line naming the value and the target in force. Its C#
lines sit between `// ROUNDPHOTO begin` and `// ROUNDPHOTO end`. At 0 a frame and every C# path are the ones without it, to the pixel.

`EVOSIM_THEATRE_ALGA_ROUND_TARGET` (0 to 1, default 0), read once through `TheatreSkin.Dial`, gives the round producers the blades' colour
on screen. A blade's colour is a multiplier on its whole light (`leafTone`, applied as `lit *= leafTone` after the key, ambient, rim, glow and
light through), because a tone on the body, the rim's colour or the light through alone is re-tinted by the water's teal key and ambient and
hardly moves a pixel. At value v, a round part's `leafTone` is multiplied by `lerp(1, roundTone, v)`, where `AlgaRoundTargetTone(body)` is
`AlgaBladeColour`'s arithmetic at the ramp's mid-green photo stop: the hue by the ratio of the target to the `body` it is handed (the same
`body`, at the same point, as a blade's call) at equal luminance, bounded as there; the lightness by the target's luminance over
`AlgaBladeRampMidLuma` at the exponent `AlgaBladeLumaShare`; the whole taken the ramp's share of the way (`_EvoAlgaRampShare` when set, else
`AlgaBladeRampShare`). The target is read where `ROUND_PHOTO` reads it (`_EvoAlgaRampStopB` when set, else `AlgaBladeRampB` moved toward
`AlgaBladeRampPhotoB` by `_EvoAlgaRampPhoto`), never a constant, so a stop that comes from the world is followed; none of a blade's own inputs
(no `leaf`, no `AlgaBladeWithin`, no plant's place on the ramp) is read. The guard is `ROUND_PHOTO`'s, and a blade, a swimmer's skin, a thallus,
a stipe, a bladder and a corpse keep their colour; `LOOK_PROBE` 2 and 3 still override `leafTone` where they do. The dial is independent of
`ROUND_PHOTO` and, with both on, reads the `body` it has already changed. It is zero unless `EVOSIM_THEATRE_ALGA_RAMP_PHOTO` is above 0. Its
shader lines sit in `#if defined(_EVO_ALGA_ROUND_TARGET)`, nested in `defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2) &&
defined(_EVO_ALGA_RAMP_PHOTO)`, with the keyword's pragma beside `ROUND_PHOTO`'s in each of its three passes. The value is the global
`_EvoAlgaRoundTarget`; `TheatreSkin.EnsureAlgaRoundTarget` sets it, enables the keyword once and logs the value and the target in force, only
when the dial is above 0. Its C# lines sit between `// ROUNDTARGET begin` and `// ROUNDTARGET end`. At 0 a frame and every C# path are the
ones without it, to the pixel.
`EVOSIM_THEATRE_ALGA_ROUND_STOP` (`r,g,b` in linear colour, each 0 to 1, parsed and refused as the ramp's stops are) is read only while
`ROUND_TARGET` is above 0. Set, it replaces the target for the round producers alone, so the blades' stops can move per world without moving
the round producers' target. The global is `_EvoAlgaRoundStop`, `w` 1 when it is set; unset, every pixel is the one without it.

`EVOSIM_THEATRE_ALGA_ARC` (0, off; range 0 to 2: 1 from 0.5, 2 from 1.5) bends a broad blade with the water past it as a rod bends.
It is a dial of its own: `EVOSIM_THEATRE_ALGA_BEND`'s paragraph above holds at every value, and nothing of that dial's code,
global (`_EvoAlgaBend`) or push is edited. The arc acts only with `EVOSIM_THEATRE_ALGA`, `EVOSIM_THEATRE_SWAY` and
`EVOSIM_THEATRE_ALGA_BLADE` at 2 or 3, and with `EVOSIM_THEATRE_ALGA_BEND` above 0, because the blade's flow reaches the shader
through that dial's plumbing (`_SwayPart.w`, `AlgaBendFlow`, `TheatrePalette.Flow`). At 0 a frame and every C# path are the
ones without it, to the pixel, at every `ALGA_BEND` value. Every shader line it adds is inside `#if defined(_EVO_ALGA_ARC) &&
defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)` (the global's declaration inside `#if defined(_EVO_ALGA_ARC)`), under
`#pragma multi_compile _ _EVO_ALGA_ARC` in each of the three passes that carry `_EVO_ALGA_BLADE3`'s, so with those regions and
pragma lines removed the shader is the one without them, byte for byte; its C# lines sit between `// ARC begin` and
`// ARC end`. `TheatreSkin.EnsureAlgaArcKeyword` enables the keyword once, only at those levels, and `PushAlgaArc` sets the global
`_EvoAlgaArc` (x the level, zero while the look is held). How it meets `ALGA_BEND`: above 1 that dial's lean already spends room
past the box, so the arc yields, does nothing on any blade, and the palette logs that once (`LogArcYield`). At 1 or less the lean has
kept `AlgaBendSwayKeep` of the sway's swing and spends up to `AlgaBendStream` of its strength (the dial, to one) of the room's
amplitude on the thin axis and up to `AlgaBendLateral` of it on the width axis, and the arc takes what is left of each. With the
lean off the arc keeps the sway's share itself and takes the rest of the thin room and `AlgaBendLateral` of the width room.
A point at arc u from the anchor, over the far end's, moves by the closed form of an angle bend, theta = tip u^2: sideways by
tip L u^3 / 3 and toward the anchor by tip^2 L u^5 / 10 (L the arc from the anchor to the far end), so the blade turns and does not
stretch, and its normal turns with it. The arc is in the flow's plane: the thin axis and the width axis each have a tip angle, from
the flow's component across the long axis. Each tip angle is the axis' limit times the drive: the flow share to `AlgaArcCurrentPower`
(0.5, so a slow current counts for more), times a wave along the blade (`AlgaArcWaveHertz` 0.3, `AlgaArcWaves` 0.5, swinging the
drive by `AlgaArcWaveShare` either way, the phase the sway's own `_SwayPart.y`, so neighbours differ), times the hold. Level 1 stays
inside the box: the thin axis allows a tip angle of 3 times its room's amplitude over L, the width axis 3 times its share of the room
the widest station leaves beside the outline over L (the envelope's peak, `AlgaBladePeakLeast` to one of the half width, so a blade
that fills its face has almost none), each at most `AlgaArcTipCap` (30 degrees) times the flow's component. A thin blade, whose curl
and droop fill its box, has no thin room and does not bend at level 1. Level 2 is a trial beyond the collider (D242): the thin axis'
tip angle is at least `AlgaArc2OutAngle` (20 degrees) times the flow's component, with no limit from the box, so a vertex at the
blade's tip can stand outside it by L tip / 3 less the box's half thickness. The palette logs that distance once per blade, in metres
and as a share of the blade's length (`TheatrePalette.LogArcTrial`; `AlgaArc2OutAngleDegrees` is the shader's angle and the two must
move together). The renderer's bounds (`TheatreMeshes.LeafBounds`) do not grow with level 2, so a blade whose box is just off screen
can be culled while its arc is on screen. The constants (`AlgaArc*`) live in `TheatreBody.shader` beside `AlgaBendFlow`. Leave it off
for a `-Freeze` check, as the sway is.
`EVOSIM_THEATRE_ALGA_ARC_ROOM` (0, off; range 0 to 0.3) lets the arc's thin axis reach that share of L beyond the box, a preview
beyond D242 that acts only where the arc acts and only on the thin axis. The thin-axis tip angle becomes 3 times the thin room over
L plus the dial, at most `AlgaArcRoomCeiling` (60 degrees, where the closed form's cut sine and cosine stop following a rod): level
1's limit is that angle and its cap the larger of `AlgaArcTipCap` and it, and level 2's angle the larger of `AlgaArc2OutAngle` and
it, each times the drive and the flow's component as before. The value reaches the shader in `_EvoAlgaArc.y`, grows
`TheatreMeshes.LeafBounds` by the dial times the blade's length each side on the thin axis, and `TheatrePalette.LogArcRoom` logs the
reach per blade; at 0 every pixel and C# path is the one without it, and its C# lines sit between `// ARCROOM begin` and
`// ARCROOM end`.
`EVOSIM_THEATRE_ALGA_ARC_DRIVE` (0, off; range 0 to 3) is a diagnostic for finding what holds the arc down, never for a film, and
acts only where the arc acts, under the keyword `_EVO_ALGA_ARC_DRIVE` and the global `_EvoAlgaArcDrive`; at 0 every pixel and C#
path is the one without it, and its C# lines sit between `// ARCDRIVE begin` and `// ARCDRIVE end`. At 1 each axis of every
acting blade bends by its full limit (ROOM's lift where ROOM is on), the flow's component and the drive taken as one, at 2 the
blade is drawn grey by the flow's component across its thin axis and at 3 by the drive, black for none and white for all, before
any light.

`EVOSIM_THEATRE_ALGA_FLUTTER` (0, off; range 0 to 1) ripples a broad blade out of its plane in a current along it, as a flag does,
where the arc bends it only across the flow's component on its thin axis and so leaves a blade the current runs along straight. It
acts only where the arc acts, under the keyword `_EVO_ALGA_FLUTTER` and in `_EvoAlgaArc.z`, and adds to the thin axis' angle the
dial times that axis' full limit (the diagnostic's mode 1 limit, ROOM's rule where ROOM is on), times the current's speed to
`AlgaArcCurrentPower` (not its direction) and the hold, times a wave from the anchor to the free end at `AlgaFlutterHertz` and
`AlgaFlutterWaves`; the sum is cut to that limit, so the box, or ROOM's reach, is never left. The closed form displaces each station
by its own angle, so the wave moves the blade smoothly, and the normal, turned by the arc's angle at the station, follows the sheet
only roughly where the wave is steep. At 0 every pixel and C# path is the one without it; its shader lines are inside
`#if defined(_EVO_ALGA_FLUTTER)` under `#pragma multi_compile _ _EVO_ALGA_FLUTTER` in the three passes that carry
`_EVO_ALGA_ARC_DRIVE`'s, and its C# lines sit between `// FLUTTER begin` and `// FLUTTER end`.

`EVOSIM_THEATRE_ALGA_FLUTTER_NOISE` (0, off; range 0 to 1) makes the flutter less regular. It acts only where the flutter acts, under
the keyword `_EVO_ALGA_FLUTTER_NOISE` (nested in the flutter's, with its own `multi_compile` in the same three passes) and in
`_EvoAlgaArc.w`. At its value v each blade gets a ripple of its own, from four hashes of the part's `_Individual` (which differs by
part; `_SwayPart.y` is the creature's phase plus a fixed lag per joint, so parts at one depth of one body share it): a frequency from
`AlgaFlutterHertz` toward `AlgaFlutterNoiseHertzLow` to `AlgaFlutterNoiseHertzHigh`, an amplitude factor from one toward
`AlgaFlutterNoiseAmpLow` to `AlgaFlutterNoiseAmpHigh` (mean one), a phase of its own, times v, added to the wave's, and the offset in
time of a slow gust, three sines at `AlgaFlutterGustHertzA`, `B` and `C` averaged and mapped to `AlgaFlutterGustFloor` to
`AlgaFlutterGustCeiling` (mean near one), that multiplies the ripple by v. The frequency is constant per blade, so the wave's phase
stays continuous in time. The cut to the thin axis' limit after the sum is the flutter's own, so a peak over the limit is cut. At 0
every pixel and C# path is the one without it; its shader lines are inside `#if defined(_EVO_ALGA_FLUTTER_NOISE)`, and its C# lines sit
between `// FLUTTERNOISE begin` and `// FLUTTERNOISE end`.

`EVOSIM_THEATRE_FROND_SWIMMER` (0 off by default, 1 on) keeps the frond rule off swimmers. `TheatrePalette` draws every
photosynthetic part drawn as a leaf, on a body with no anchoring part, as a free-drifting frond, and that also catches a
jointed swimmer carrying a leaf part. On, a body with a link part draws that part as a leaf sheet inside its own box.
Off, nothing differs: the dial's lines sit between `// FROND begin` and `// FROND end`.

`EVOSIM_THEATRE_THALLI` (0 off by default, 1 on) draws a compact producer as a seaweed thallus that fills its box and never
leaves it, where the blade path draws a flat lamina. Off, a frame is the one without it, to the pixel. Its shader lines are inside
`#if defined(_EVO_THALLI)` under `#pragma multi_compile _ _EVO_THALLI` in the three passes that carry `_EVO_NOSE_PIT`'s, and its
C# lines sit between `// THALLI begin` and `// THALLI end`. `TheatreSkin.EnsureThalliKeyword` enables the keyword once, from the
look's push and from the first thallus dressed. A part qualifies when the blade path drew it as a leaf, it is photosynthetic, its
body has no link part (a swimmer's frond keeps its blade), and it passes the compact test (`TheatreMeshes.ThallusShaped`): its
smallest side over the middle above `ThalliFlattest` (0.5) and its middle over the largest at least `ThalliSlenderest` (0.3). The
form rule (`ThallusFormOf`): middle over largest under `ThalliLong` (0.65) is Codium; else smallest over middle under
`ThalliSquat` (0.75) is Padina; else Ulva. Within `ThalliBand` (0.05) of either threshold a hash of the plan's seed, the part's
index and its half extents moves the line, so a part keeps its form in every frame and run. The mesh (`TheatreMeshes.Thallus`) is
built whole in C#: Ulva three or four ruffled sheets from one base, Padina a slit funnel or two fans with an inrolled rim, Codium a
trunk forking in twos into even capsules. Its growth axis is Codium's longest, Padina's shortest and Ulva's anchor axis, its base
at the anchor's end. Each form fits its centre lines or mid-surfaces into the box less its thickness on every axis, so every
vertex is inside `ThalliFill` (0.97) of the box, and each is clamped there besides. One mesh is cached per form, variant, growth
axis, base end and size class (sides over the largest in steps of 0.1), at most `ThalliMeshCap` (96); a part past the cap keeps
its blade. A thallus is 1,190 to 2,668 vertices. Its solid flag (UV3.x 0.2, 0.25, 0.3) is under a half, so every older test reads
it as neither box, blade nor skin. The shader does not carve it, and sways it on the two axes across its growth by the reach the
mesh baked per vertex (UV4.x, at most that vertex's room), then clamps it into the box. The vertex colour carries tone, lime and
thinness: Padina's six bands and limy rim, Ulva's thin margin (more light through, and light from the surface at
`ThalliUlvaSkyGlow`). Codium's sheen is replaced by a weak broad one. The palette writes each form's own colour
(`ThalliUlvaHue`, `ThalliPadinaHue`, `ThalliCodiumHue`) at the paint's brightness, blended toward the clade's producer colour by
`ThalliCladeShare` (0.25). A greyed body or a corpse keeps the paint's grey. One line per run, at the theatre's close or the
application's quit, counts the parts that took each form, the meshes and their vertices, and the parts the full cache refused.

`EVOSIM_THEATRE_VELVET` (0, off) draws an absorber part (the absorptive cell type) as a sponge or a colonial tunicate, and
at 0 every frame is the one without it, to the pixel, with any other dial on or off. It is light and shading only, so
nothing can leave the collider. The palette sets `_Velvet` on an absorptive part's renderers and `_VelvetOsculum` beside it
(`OsculumOf`), and a neck keeps its tissue's skin. Three terms, each behind a branch on `_Velvet`. (1) A matte skin: the
sheen is `VelvetSheenGain` (0.3) of the body's at the exponent `VelvetSheenPower` (5, the body's is 28), the rim's power is
`VelvetRimPower` (0.7) of the material's, and at grazing angles (facing over `VelvetRimLow` 0.55) the rim rises to
`VelvetRimGain` (1.6) times its strength. (2) Pores: a dimple with a paler lip on a jittered grid in the part's own metres
(`VelvetPoreSpacing` 0.015, `VelvetPoreDiameter` 0.004; the points are `PapillaField`'s, 0.15 to 0.85 of a cell), each
counted by its distance across the surface alone and weighed by how far off the surface it lies, so about one to a cell of
the surface, and summed, so no two switch over. They tilt the normal (the dimple's depth `VelvetPoreDepth` and the lip's
height `VelvetPoreLip`, in radii, bounded as the papillae's are), darken the body in the dimple by `VelvetPoreDark` (0.55)
and lift the lip toward a paler tone (`VelvetLipGain`, `VelvetLipLift`), and fade out as the spacing falls from 12 to 5
pixels. The grid is in metres along the part's axes from the visual's object position and scale, true on a box and a ball;
on a capsule's shaft, whose mesh is two units tall, the spacing is stretched along the shaft. (3) One osculum on the end of
the part's long axis away from its joint (the colour ramp's tip: `RampOf`'s axis and end, copied), of radius
`VelvetOsculumShare` (0.35) of the part's smallest half extent: a hole inside 0.55 of the radius whose wall runs out to
0.8, and a raised pale lip on 0.95 of it. It is drawn in the normal and the colour (the hole keeps 0.12 of the light), not
cut, because an opening as wide as that would take more than the carve's bound; the pores keep away from it, and it fades
out as its radius falls from 8 to 3 pixels. (4) The carve: a velvet absorber takes LEAF=1's smooth `LeafCarve` at
`LeafCarveShare` of the depth (the shader's `leafSoft`, in the vertex stage and the fragment's rebuilt normal alike),
since the band's terraces make it read as faceted rock; on a box under ORGANIC=2 the soft tissue's carve still comes
first. `EVOSIM_THEATRE_VELVET=2` (the shader tests `_Velvet` above 1.5; 0 and 1 are
unchanged) replaces the pores with star systems (`VelvetStars`, `VelvetStar*`), as a colonial tunicate has them, in colour
alone: centres on a jittered hexagonal lattice 0.025 m apart in the plane each flat face shows (the planes weighed by the
normal, so an edge or a capsule's side blends two), 6 to 12 pale oval zooids pointing at a dark central pore, a darker
surround and a faint line between neighbours, faded out as the spacing falls from 14 to 6 pixels. The osculum stays as at
level 1. The constants (`Velvet*`) live in `TheatreBody.shader` beside the fragment. A zooid takes the body's hue lifted by `VelvetStarPaleGain` (1.25) but to at least `VelvetStarPaleFloor` (0.14, linear) of luminance and `1 - VelvetStarPaleHue` (0.2) of the way to white, so the stars show pale on a dark absorber without the white of a printed flower; a lift of the body alone leaves them near black. The systems nearly touch (`VelvetStarRadius` 0.39 of the spacing, `VelvetStarJitter` 0.06, the most the no-overlap bound allows at that radius), each with a dark shared pore of `VelvetStarPoreRadius` 0.22 of its radius, as a Botryllus colony's systems share their outlet.

`EVOSIM_THEATRE_LINK` (0, off) draws a capsule part (a link) as a worm's body or an arm is, narrowing toward its free end
and segmented along its length, and at 0 every frame is the one without it, to the pixel, with any other dial on or off.
The palette sets `_LinkAxis` and `_LinkShape` on a capsule's shaft and caps only (`LinkOf`), never on a capsule drawn as
one ball (`NearlyASphere`), a sphere, a box, a blade or a neck, and every term sits behind a branch on `_LinkShape.w`.
(1) The taper (`LinkTaper`, in `ShapeBody` after `ShapeBox`). A capsule is a shaft and two caps, three meshes on one
axis, so one function of the position along that axis (the colour ramp's base end, `BaseSignOf`; t runs zero at the base
pole to one at the free pole) scales each vertex's cross-section toward the axis: one across the base cap, a smoothstep
down to `LinkTipShare` (0.6, in `TheatrePalette`) of the base radius along the shaft, and that across the tip cap. The
smoothstep has no slope at either end, so neither seam is a crease; the tip cap is drawn at the tip's radius along its
whole length and not shortened, so the free pole stays on the collider's pole and a child hung there still meets it. The
normal is the inverse transpose of the map's Jacobian. The carve, the pinches and the bands still read the undeformed
position. Only the cross-section shrinks, so the drawn link is inside its collider: the margin outside it is 0. A bridge
into a link runs `LinkStandBack` deeper, by the radius the taper took across the ray. (2) The fringe, in shading
(`Link*` in the fragment). Segments are `LinkSegmentShare` (0.6) of the base radius long, counted along the whole
capsule so the shaft and the caps share them; with the tissue look's muscle bands on, the segments take the bands' own
coordinate (`LinkPhase`), so a groove sits on a ring. Across the surface a faint groove darkens the light; at the
silhouette (`LinkEdgeLow` to `LinkEdgeHigh` of 1 - n.v, the frill's band) each segment carries a pale lobe, a rim over
`LinkLobeRim` times the plain one with a pale tone added, a nearly dark gap in the groove, and `LinkBristlesPerSegment`
(4) fine dark lines across the lobe. All of it fades out as the segments fall from 8 to 3 pixels apart, the bristles
first. Light and colour only, so no geometry beyond the taper moves.

`EVOSIM_THEATRE_RIM_BAND` (0, off) paints a coloured margin round a consumer part (the biters), as a sea slug's mantle
or a flatworm's rim is, and at 0 every frame is the one without it, to the pixel, with any other dial on or off. Colour
only: no vertex moves and no light changes. The palette sets `_RimBand` to one on a consumer part drawn in colour and to
zero on everything else (a neck, a greyed body, a corpse), and the step sits behind a branch on it, on the box mesh alone
(the mesh's flag, carried to the fragment in `Varyings.shape`); a capsule, a sphere and a blade are left as they were. It is read at
the undisplaced object position, so it stays on the tissue. The outline is the section across the part's thinnest axis
(`TissueMargin`'s section and tie rule): the distance e from the middle toward the edge, a p-norm of 8 of the other two
coordinates over the mesh half, so the band runs round the rim of the broad faces and its warm stripe continues down the
sides (e is about 1 there). It begins at `RimBandInner` (0.82). Two stripes: a pale line over the first `RimBandLine`
(0.25) of the band, near white with `RimBandPaleTrace` (0.15) of the body's hue, then a warm yellow-orange band to the
edge (`RimBandWarm`, luminance one), each crossfading over `RimBandEdge` (0.1) of the band. Each stripe's colour is the
body's own luminance times a lift (`RimBandPaleLift` 1.6, `RimBandWarmLift` 1.4), so it is at most that much lighter than
the body, mixed in at `RimBandPaleStrength` (0.85) and `RimBandWarmStrength` (0.75). The pale line fades out as its width
falls from 6 to 3 pixels (`RimBandFadeHigh`, `RimBandFadeLow`): the width is read from the part's size and overstates it
where the margin folds away edge-on, and a thinner line breaks into dashes along the fold. It acts on the body colour before the colour ramp and the countershade, which grade it as they
grade the body. The constants (`RimBand*`) live in `TheatreBody.shader` beside `RimBand`.

`EVOSIM_THEATRE_CHROMATOPHORES` (0, off) speckles a body with pigment cells that open and close, as an octopus's or a
cuttlefish's skin does, and at 0 every frame is the one without it, to the pixel, with any other dial on or off. Colour
only: no vertex moves and no light changes. The palette sets `_Chromatophores`, the tissue's strength and not a toggle,
only while the dial is on (`ChromatophoresOf`): one on a consumer and a link, `ChromatophoresAbsorber` and
`ChromatophoresStructural` (0.4 each) on an absorber and a structural part, zero on a leaf, a nerve, a float and a holdfast,
and zero on a neck, a greyed body and a corpse. The step sits behind a branch on it, after the rim band and before the
colour ramp and the countershade, and reads the undisplaced object position in the part's own metres, so the cells stay on
the tissue. (1) The cells are the velvet pores' search: a jittered grid of `ChromaSpacing` (6 mm), the 27 cells round the
pixel, each point taken across the surface and weighed by how far off it lies, the discs summed and clamped so no two switch
over. Each disc has a radius of `ChromaRadius` (0.3 of a cell) that breathes on its own phase from the cell's hash,
r = r0 (1 + `ChromaBreath` 0.4 sin(omega t + phi)), with a period of `ChromaBreathPeriod` (4 s); the pigment is the body's
colour times `ChromaPigment` (0.5, 0.28, 0.24), darker and warmer, shown at up to `ChromaOpacity` (0.85). (2) A passing
cloud is a plane wave, s = smoothstep over sin(k x - omega_c t), along a direction and a phase from `_Carve.x` and
`_Individual`, of a wavelength `ChromaCloudWavelength` (0.5) of the part's longest size and a period `ChromaCloudPeriod`
(10 s); it raises the radius of the cells it passes by up to `ChromaCloudOpen` (0.5). (3) Time is `EvoTime()`, the film's clock,
taken as frac(t / period), so a frame drawn twice is the same frame. (4) Where the spacing falls from `ChromaFadeHigh` (7) to
`ChromaFadeLow` (3) pixels the term goes to its mean colour, the tissue mixed with the pigment by `ChromaMeanCover` (0.22),
so a far body shows a uniform, slightly darker tint and does not shimmer. The mean cover is a figure worked out from the
radius and the swing; change it with them. On a capsule's shaft, whose mesh is two units tall, the grid is stretched along
the shaft, as the velvet pores' is. The constants (`Chroma*`) live in `TheatreBody.shader` beside `Chromatophores`. The passing cloud also darkens the tissue toward the pigment by itself, up to `ChromaCloudDark` (0.8) of the way at its crest and at any distance: the cells alone are a pixel or two at portrait range and read as almost nothing on a dark body, while a dark band crossing the skin is what footage of a cuttlefish shows.

`EVOSIM_THEATRE_PATTERN` (0, off) paints a body with a reaction-diffusion pattern, spots, stripes or a net, as a nudibranch's or a flatworm's skin has, and at 0 every frame is the one without it, to the pixel, with any other dial on or off: nothing is solved, no texture or dictionary is made, and the palette sets none of `_Pattern`, `_PatternShift` and `_PatternTex`. Colour only. `TheatrePattern.cs` holds the solver, the library and the keys. (1) The key: the genome's node cell types (a sorted multiset, hashed), its reproductive investment rounded to `InvestmentStep` (0.05) and its reserve margin as one of `MarginBins` (24) log bins of log(1 + margin) over 0 to 600 s (a log bin, not steps of seconds, which split a lineage's drift at small margins). Kin share all three (round 62: a child carries each unchanged in about 92% of pairs), so a lineage keeps its pattern and a mutation that crosses a step replaces it; a real window holds 750 to 1,200 distinct keys. The palette is handed a phenotype, which has neither number, so a caller with the genome calls `TheatrePattern.Register(id, genome)` once a body (`LiveWorldView`, `FilmWindowWorld`, `SnapshotWorld` do); a body never registered takes `FallbackKey` (its parts' cell types, the middle of both ranges). (2) The regime and the library: investment, over 0.05 to 1.3 (`InvestmentLow`, `InvestmentHigh`), is the place along the path, low investment spots, the middle the net, high investment stripes; that (F, k) is put on the nearest of `RegimeBins` (9) points along the path spots, a bend, the net, a shoulder, stripes (the bend and the shoulder, `BendFeed`/`ShoulderFeed` and their kills, steer round flat ground where no pattern forms, which a straight net to stripes line crosses), nine bins so the three anchors are sampled exactly. Investment alone, because round 62's margins are all 140 to 640 s, high on the log scale: a margin weight puts every body in the middle bins, 3 to 6, where the clades look alike, while along investment its big clades spread from spots to stripes (`scratch/theatre-realism/pattern_regimes.py` prints both maps per clade). A tile is a (regime bin, seed bin) and nothing else, the seed bin being the key's hash modulo `SeedBins` (4), so at most 36 tiles are ever solved, each lazily the first time a body needs it and kept for the process, and every key beyond them shares one. What differs between bodies on one tile is placement, all from the full key's hash: an offset into the tile (u and v), a quarter-turn of the mapping (0 to 3), and the tile's length, which follows the margin's bin from `MarginScaleLowBin` (15, about 60 s) to the top, from `ScaleLow` to `ScaleHigh` (0.7 to 1.4) times the body's own size. (3) The solver: Gray-Scott on a 128 x 128 torus, explicit Euler at dt 1 with the 5-point Laplacian, Du 0.16, Dv 0.08, `Steps` 6,000, from u = 1, v = 0 with six 10-cell squares (u 0.5, v 0.25) and a little noise drawn by the project's `Rng` seeded from the tile's number, one thread, so a tile solves to the same bytes every time. The anchors in (F, k) were checked with `scratch/theatre-realism/pattern-agent/gs_check.py` and are spots (0.030, 0.062), stripes (0.060, 0.062) and a net with holes (0.039, 0.058). Pearson's quoted spots at (0.035, 0.065) die out on this grid and seed. v is stored over its own highest value as an R8 texture (repeat, trilinear, mip maps built, 21.8 KB a tile, under 0.8 MB for the library); a field whose pigment would cover more than `MaxCover` (0.5) is stored inverted, so the pigment is always the smaller phase and a net shows as its holes. A solve takes about 0.6 s in numpy; the C# time in Mono is not measured, and the log prints each tile's milliseconds with the count solved and the total, so the cost of a take is the last such line. The solver's four float buffers (256 KB) are made once and reused. (4) The shader (`PatternSkin`, `Pattern*`, behind a branch on `_Pattern.y`, after the chromatophores and before the colour ramp): triplanar on the part's three axis planes in its own metres at the undisplaced object position, weights from the uncarved normal in the part's axes to the power `PatternBlendPower` (4), over a tile of `PatternTileShare` (0.25) of the body's longest part times the regime's multiple, the same for all its parts, turned by the key's quarter-turns and moved by the key's offset plus a step per part index. v goes through `smoothstep(PatternLow 0.35, PatternHigh 0.65)` (the same numbers as `ThresholdLow` and `ThresholdHigh` in `TheatrePattern.cs`) to a cover that mixes the body toward its pigment: luminance `PatternDarken` (0.42), saturation `PatternSaturation` (1.35), shown at up to `PatternOpacity` (0.9) times the tissue's strength, one, or `PatternSmaller` (0.5) on an absorber and a structural part, none on a leaf, a neck, a greyed body and a corpse. The texture is read at an explicit level from the pixel's size, so the call needs no derivative, and where a pattern cell (a tile over `PatternCellsPerTile`, 12) falls from 5 to 2 pixels the term goes to the tile's mean cover, so a far body shows a uniform tint and does not shimmer. The tile length follows the body's drawn size, so a body that grows takes its pattern with it. The constants live in `TheatreBody.shader` beside `PatternSkin` and in `TheatrePalette` and `TheatrePattern`.

`EVOSIM_THEATRE_IRIDESCENCE` (0, off) gives the sheen the colour of a thin wet film's interference, as octopus skin, sea slugs and comb jellies show in their highlights, and at 0 every frame is the one without it, to the pixel, with any other dial on or off: the palette sets no `_Iridescence`, and the fragment copies the key's colour into `sheenKey` and takes the tint only behind a branch on it. The sheen alone changes, on every branch of its chain (the plain one, under `_Sss`, under the soft tissue and the velvet's), keeping its strength and shape; the body, the rim and the light through the tissue are untouched, and no vertex moves. The palette sets `_Iridescence`, the tissue's strength and not a toggle, only while the dial is on (`IridescenceOf`): one on a consumer and a link, `IridescenceFloat` (0.5) on a buoyancy part, `IridescenceDry` (0.2) on an absorber and a structural part, zero on a leaf, a nerve and a holdfast, and zero on a neck, a greyed body and a corpse. `IridescentTint` takes a film of index `IridescentIndex` (1.33) seen at theta between the geometric normal and the view, with sin(theta_t) = sin(theta) / n, and reads I = 1/2 + 1/2 cos(4 pi n d cos(theta_t) / lambda + pi) at `IridescentWavelengths` (650, 532, 450 nm for red, green, blue). The pi is the soap film's phase flip (one reflection flips, one does not): a film far thinner than the wavelength reflects nothing. A wet skin over denser tissue would flip both and swap the colours; this is the two-beam form and not Belcour and Barla's full sum. The three are divided by their mean (floored at `IridescentMeanFloor`, 0.05), so the brightness is kept and only the hue moves. The thickness is `IridescentBase` (300 nm) plus `IridescentSwing` (150 nm) times (2 noise - 1), the noise a smooth value noise of the part's metres at `IridescentScale` (6 cm) a cycle, seeded from `_Carve.x` and `_Individual`, so the colours drift over the skin and shift as the body turns. The tint mixes white toward the interference colour by `IridescentMix` (0.6) times the tissue's strength. The depth light takes red away under water, so it reads blue, violet and green. The constants (`Iridescent*`) live in `TheatreBody.shader` beside `IridescentTint`. The comb jelly's rainbow is cilia diffracting light, not a film; the board photographs there are a look reference, not evidence for the mechanism. Under the soft tissue (ORGANIC=2), whose sheen is broad, the film's colour goes only on the highlight's core, the share a tight lobe would light (`IridescentCorePower`, 19 = 28 - 9), and the rest of the broad sheen stays white: spread over the whole broad lobe, the thickness noise crossed a body as even rainbow bands, an oil slick.

`EVOSIM_THEATRE_MOTTLE_SOFT` (0, off) blends the mottle's tone between neighbouring Voronoi cells and drops the dark
wall between them (`MottleSoft`), so a strong mottle, the bold tissue looks' above all, reads as tissue and not as a
mosaic of flat tiles. It also reads the mottle on the part's own position in metres, with an offset per body, so
the cells ride on the skin. With the dial off the mottle is read in world space, and a swimming body slides
through it: frames a second apart show a different tiling of the same skin. Shading only, and at 0 a frame is
the one without it.

`EVOSIM_THEATRE_CELL_VIEW` (0, off) is a diagnostic and not a look: every part is drawn in one flat colour for its cell
type, so a creature can be read by its cells. Behind a branch on `_CellView`, as soon as the fragment's normal is known,
the shader returns `_CellColour` nearly unlit (below), with alpha as the rest of the fragment sets it, and none of
the mottle, rim, sheen, pattern, chromatophores, carve shading, water cut or depth light, so the colour reads true. The vertex stage, the shadow caster and the depth passes
are untouched: the drawn shape is the one without the dial. The palette (`CellViewColourOf`) sets `_CellView` and
`_CellColour` on every part while the dial is on, a greyed body and a corpse included, and a neck takes the colour of
the part it hangs from. The colours are Okabe and Ito's, chosen to stay apart for a colour-blind viewer: structural
`BBBBBB`, link `F0E442`, neural near white `F2F2F2`, photosynthetic `009E73`, absorptive `E69F00`, consumer the set's
reddish purple `CC79A7`, apart from the absorptive orange (the owner's choice of 2026-10-25, over the set's vermilion
`D55E00`, which sat close to the orange in a crowd), buoyancy
`56B4E9`, anchoring `000000`, the set's black rather than its blue, which sat beside the buoyancy's sky blue (sRGB;
Unity converts the block's `Color` to linear). The colour is drawn nearly unlit and outside the water: the key's
wrapped diffuse shades it between `CellViewShadeFloor` (0.78) and one, with no ambient, fog or water absorption, so a
colour reads the same near and far; the frame's grade (exposure, neutral tonemap) still applies, and the colour is scaled by `CellViewExposureLift` (2^-0.4) against the default post-exposure. The dial has one palette: any value above zero draws it. A legend, the eight names beside
their swatches in the lower left corner of the frame, is drawn in the bitmap font whenever the dial is on, whether or
not the safari's `-NoBurnText` is given (`SnapshotCamera.DrawCellLegend`); `EVOSIM_THEATRE_CELL_VIEW_LEGEND` (1, shown)
set to 0 hides it for clean stills. At 0 the palette sets neither property, the shader's terms are skipped by the
branch, nothing of the legend is drawn, and a frame is the one without the dial.

`EVOSIM_THEATRE_BOARD_LIGHT` (0, off) is a measurement and not a look: behind a branch on `_BoardLight` the bodies
alone take a white key and a neutral fill and ambient, with no depth light, caustics, `_EvoBodyWater` cuts or fog,
while the backdrop, the scenery and the frame's grade keep the water, so a pair of frames with it off and on shows
how much of a body's colour the water takes and how much is the tissue's own, and the log says it is on where the
label does not.

`EVOSIM_THEATRE_SWAY` (0, off) moves a body at rest, so it still looks alive, and at 0 a frame is today's to the
pixel, whatever the tissue, light-through and soft dials say. A photosynthetic part drawn as a blade bends about its
own anchor, more toward its free end, by up to 3 % of its largest half extent, once every 3 to 5 s. A buoyancy part
draws in along its smallest axis by up to 4 % of that half extent and lets go, once every 2 to 3 s. Nothing else
moves. Corpses, necks and the raw shapes hold still too. Each body takes its period and phase from its id, and each
joint out from the root puts a part 0.06 of a cycle behind, so neighbours are out of step and a body moves as one.
Tissue near a joint anchor holds still, so it does not pull away from a neck. Neither term can leave the collider.
The float's draw only ever squeezes toward the visual's own centre plane, and every collider is mirror symmetric
across that plane. The blade moves only along its thin axis, by one amplitude for the whole blade: the most that
keeps every station inside its box once the lens and the curl have taken their most there (`LeafRoom`,
`LeafSwayAmplitude`). A blade at least two fifths as thick as it is wide, as round 47's are, keeps the whole swing.
One under a quarter keeps little or none, since its curl already fills its box. The clock is `EvoTime()`, the caustics' and the snow's, so a frame's sway depends on the second it is drawn at
and not on how fast it rendered. Two renders match frame for frame only when that clock does: the safari's take
clock, or `_Time.y` reached by the same number of frames. Leave the dial off for a `-Freeze` check, since the sway
moves a frozen world. The terms sit behind a branch on `_Sway`, which only the palette sets, and only while the dial
is on. The constants (`SwayLeafTip`, `SwayFloatDraw`, `SwayKind*`, `SwayRootStation`, `SwayRoom*`,
`SwayLensAtFifth`, `SwayRidgeTail`) live in `TheatreBody.shader`,
and the periods and the lag (`LeafSway*`, `FloatPulse*`, `SwayLagPerJoint`) in `TheatrePalette`.

`EVOSIM_THEATRE_ALGA_BEND` (0, off) leans a marine producer's blade with the water past it. It acts only with
`EVOSIM_THEATRE_ALGA` and `EVOSIM_THEATRE_SWAY` both on, and its value (0 to 4) scales the lean: 0 to 1 scales the share of the
room the sway leaves that the lean may spend, and 1 to 4 adds a lean by the blade's length, described below. The water is the world's current
where the blade is, less the blade's own velocity, its horizontal direction and speed only. In a synced view
(`LiveWorldView.Sync`) both come from the solver's own per-link arrays, `Creature.Water` and `Creature.Velocity`. In a fed
view (a film window) the current is the run's, as the snow's drift reads it (`TheatreSkin.WaterAt`, from
`TheatreRunSnow.VelocityAt`), and the velocity is `LiveWorldView.VelocityOf`, which the film window must set (with
`LiveWorldView.SecondOf`, the frame's run second); unset, the blade feels the current alone. No current and no velocity
means no lean. There is no seeded or fixed direction. `TheatrePalette.Flow` packs each blade's flow into `_SwayPart.w`
(`FlowCodeOf`; the shader's `AlgaBendFlow` unpacks it), so the dial adds no property, vertex input or interpolator, and
`_EvoAlgaBend` (a global, z the strength, w the switch) is the only new uniform; `TheatreSkin` sets it only while the dial
is on. The bend lives inside `LeafSway`, behind `_EvoAlgaBend.w` and `l.alga`. A long thin blade streams along its thin
axis, a sheet flutters, and a lateral lean uses the room the outline leaves beside it. The blade's sway gives up half its
swing, and the stream (0.35), the flutter (0.15) and the lean together fit the room `LeafRoom` and `LeafSwayAmplitude` give
the sway, so the collider bound holds up to a dial of 1. That room is the box's half thickness less the lens and the curl, and on
a thin blade it is little or none, so the lean at 1 is a fraction of a thickness (a rim moves by a pixel or two); it is also
scaled by the flow share, `1 - exp(-speed / AlgaBendSpeedScale)`, about a quarter at a run's few centimetres a second, and by a
smoothstep of the half length over the half thickness from 4 to 40. Above 1 the lean no longer reads the room. From 1 to
`1 + AlgaBendMoreSpan` (3) the blade leans, at its free end and as the same square of the distance from its anchor, by
`AlgaBendReach` (0.5) of its half length, toward the flow on its thin axis and its width axis alike, in metres and so outside
the box and its outline, as the curl is ruled to leave it. The reach is scaled by the dial's share of that span, by the flow share
raised to `1 / (1 + AlgaBendFlowRaise more)` (`AlgaBendFlowRaise` 3, so a slow current counts for more), by the gust and the
hold, and by a smoothstep of the half length over the half thickness from `AlgaBendReachSlenderLow` (2) to
`AlgaBendReachSlenderHigh` (8), so a plump blade stays put. It is behind a test of the dial above 1, so a dial of 1 or under
draws what it always drew. The renderer's bounds (`TheatreMeshes.LeafBounds`) do not grow with it, so a blade whose box is just off
screen can be culled while its lean is on screen. A flat blade in a horizontal current streams little, since its thin axis is vertical
and the outline leaves little room beside it; it flutters. At 0 the shader, the block and every global are the ones without
the dial, to the pixel. The constants (`AlgaBend*`) live in `TheatreBody.shader` beside `LeafSway`, and `AlgaBendSpeedScale`
(0.1 m/s) in `TheatrePalette`. Leave it off for a `-Freeze` check, as the sway is.

`EVOSIM_THEATRE_SWIM_STROKE` (0, off) draws the stroke D135 leaves to the theatre, in a film window. The solver
never swings a powered limb, so the pose holds each limb still and the theatre swings it from the power the
window logged in `film.stroke.jsonl`, in the owner's curve from `scripts/render-swim-film.py`
(`SwimStrokeCurve`, `TheatreSwimStroke`, applied between `RecordedPoses.Apply` and the view's `Place`). A joint
whose axis lies along its link, a twist, draws nothing, since the push ignores twist. The wave that a long thin
run of powered links carries is the theatre's reading of ruling 9 and not a ruling. A window without the file
draws no stroke and says so once, and with the dial off no file is read and every frame is the frame without it.
The omega is the run's `fluid.powerPushRadiansPerSecond`, which `PictureConfig` still reads when the strict
reader refuses a config.

`EVOSIM_THEATRE_REMAINS` (0, off) draws the remains of contact eating (D220) in a film window. A window of a run
with remains carries `remains.jsonl.gz`: a first row for each remains body (those standing at the start, those
that died inside and each cut-off limb, with the body it was cut from, the second its window began, the scale and
plan it was developed on and the map that selects its parts), a shed row each time it loses parts, and a genome for
each the window's own genomes file has none for. A body whose pose carries flag 8 is then drawn by
`FilmWindowWorld.RemainsView` (a `LiveWorldView` with `Remains = true`), in its own colours taken 0.3 of the way to
grey and dulled to 0.85 (`RemainsGreyness`, `RemainsBrightness`; the reserve of 0 also takes the
starving body's saturation, so these are set low, and the rim and the necks keep their colour), still, and faded linearly over the run's
`mouth.remainsWindowSeconds` from the second its remains began. With the dial off the reader parses the rows and
nothing reads them, so every frame is the frame without them, remains standing at the start included, which stay
undrawn. A run whose config has no contact eating, an older build's, never draws remains. Two readings to hold: the
window's `bites` counts the run's `k` rows alone, with `r` and `rc` counted apart, and `FilmWindowEvent.Type` is
the whole kind where `Kind` stays the first letter (`c` for `rc`).
A debt: the window reads Core's internal `Organism.RemainsMap` through a cached reflection delegate
(`FilmWindow.RemainsRecorder.MapFor`), which throws by name if the property goes. A public accessor retires the
delegate, and lands in a commit that moves `coreHash` anyway (the coordinator, 2026-10-24).

`EVOSIM_THEATRE_ONE_SKIN` (0, off; levels 1 to 6 below) is a spike, not a look yet. It draws a body's tissues as one smooth skinned surface,
and its specialised parts on top as before. `TheatreOneSkin.cs` holds it and `LiveWorldView` calls it, so every body
the stepped route or a film window draws takes it, and `SnapshotWorld`'s reconstructions do not. The body is the parts
whose cell type is in `EVOSIM_THEATRE_SKIN_BODY`, a comma-separated list of Core's ids. The default is
`structural,absorptive,buoyancy,neural,link,consumer`, so leaves and holdfasts stay specialised. A body with fewer
than two merged parts is left as it is. Each merged part is the distance field of the shape it is drawn as. A box is
the rounded cube the palette swaps in, under ShapeBox's taper and bend. With `EVOSIM_THEATRE_ORGANIC` on it takes the
organic silhouette too, and at 2 the soft tissue's margin and its ruffle. The shader's map is inverted in closed form,
so the field's zero set is the drawn surface; the carve, the joint pinch, the float's pulse and the sway are left out.
`TheatreOneSkin.DrawnBox` copies the shader's constants, and the two must move together. A capsule is its collider
squashed across by the palette's aspect, and one shorter than its radius is the single ellipsoid the palette draws. A
sphere is the ellipsoid it is drawn as. A box drawn as a leaf keeps its collider's rounded box, as does every part
under the raw shapes or with no palette. A link keeps its untapered capsule under `EVOSIM_THEATRE_LINK`, since the
palette works its taper out only at its first paint. Each is inset by k/4, and they are joined by the polynomial smooth
minimum, with k the smaller of half the median of the parts' smallest half extents and 3 cm. That minimum is never
more than k/4 below the plain one (the fold is clamped there), so the skin lies inside the union of the drawn parts,
and so of the colliders. Development puts a child's anchor on its parent's, so two colliders meet only at a point or a
face, and there the inset union has no thickness. Level 1 is that, as specified. Level 2 adds a ball at each joint
between two merged parts, of radius min(k, half the thinner part's smallest half extent). The balls are the one place
the skin may leave the colliders, by at most that radius plus k/4 from the anchor. Level 3 is level 2 plus the carve
at rest, described at the end. The mesh is built once per body,
from the adult in its rest frame. Surface nets run on a grid whose cell is the larger of the smallest half extent over
4 and the body's extent over 64. Two passes of Taubin smoothing follow, then every vertex left outside is pulled back
along the gradient, and the normals are the gradient's. Each vertex's half thickness is half the chord along its
inward normal to where the field turns positive again, marched in quarter cells on the sampled grid and capped at the
body's extent. A vertex takes up to four bones, by a softmax of -d/k. Each bone is a `SkinBone` under its part's
transform. A growth step sets every bone's scale to the grown phenotype's `ScaledBy`, so a body never rebuilds as it
grows. A new plan is a new body and builds a new skin, with no cross-fade. Per frame only Unity's skinning runs. After
every paint the skin takes the largest merged part's material and block, less the properties in that part's own
object units. It also takes its world scale, the bones' growth times the root's scale, as `_SkinScale`, written again
on a growth and on a corpse's shrink. Its colour is per vertex: each merged part's colour as the palette painted it
(`TheatrePalette.PaintedColourOf`), blended by the skinning weights. It is blended again at a paint where a part's
colour has moved by more than 0.5 %, so a dim, a corpse or a focus change shows at the body's next paint. The merged
parts' renderers and every neck between two merged parts are switched off; a neck to a specialised part stays. The
body shader knows the skin by its shape channel (x = 3) and gives it no box shaping, leaf terms, link taper,
float pulse, pores or soft tissue, and no carve below level 3. On the skin it takes the vertex colour for `_BaseColor`, and the vertex's half
thickness times `_SkinScale` for the light through the tissue, under `EVOSIM_THEATRE_SSS` as well. Its markings read
the rest frame. The rest position (UV5) places both of the mottle's paths, the papillae, the chromatophores, the
pattern, the iridescence and the velvet. The rest normal (UV6) picks the plane the chromatophores, the pattern and
the velvet are laid on. The rest frame's axes, from screen derivatives, carry the papillae's and the velvet pores'
slopes into the world. The rim, the light-through tint and the cell view are the largest part's, and the
chromatophores' cloud wavelength, the bands and the frill still read the root's matrix. The log prints one line a
body: parts, vertices, triangles, milliseconds, k, the cell, the grid, the joint balls, the vertices pulled inside,
the boxes drawn shaped, the range of the half thickness and the thin parts left out. The renderer's bounds are authored, not recomputed
(`updateWhenOffscreen` off). The parts carry the body's attitude and the skin object never turns, so the rest mesh's box
points the wrong way. `Posed()` carries each bone's bind-space box of the vertices it dominates through the bone's pose
into the skin object's space and takes the union, and `LiveWorldView.Place` and `Pose` call it once the parts are
placed. The bones' scale carries the growth. Skinning weights under 1e-3 are dropped and the rest renormalised, so no
bone carries the whole mesh. At every level the key light's sheen on the skin is a Blinn lobe of power 96
(`SkinSheenPower`), where the parts' is 28: at 28 the uncarved skin shows one broad highlight disc fixed on the screen,
because the key is aimed from the camera. The skin's sheen lobe and level 3 (every shader line they add) sit inside
`#if defined(_ONE_SKIN)`, and the shader's three passes (ForwardLit, DepthOnly, DepthNormals) each carry
`#pragma multi_compile_local _ _ONE_SKIN`. Without the keyword the preprocessed source is the text from before them,
so a part compiles as it did. They were first added as plain branches that a part never takes, and the dials-off check
then moved one pixel on a part's edge by one level (b46; the compiler's doing, by inference, as no taken branch
differed). The level 1 and 2 skin code from before them is not behind the keyword and passed its checks as it is. The
CBUFFER entries (`_SkinScale`, `_SkinCarve`, `_SkinCarveLength`) and the Properties lines stay unconditional, since the
SRP batcher wants one layout across variants. The skin draws with a copy of the parts' material that has `_ONE_SKIN` on
(`TheatreOneSkin.KeywordedOf`: one copy per source material, shared by every skin, brought up to the source's
properties at each `Dress`), and no part's material ever has it. A fading pair is left to the palette's `SetFade`,
which makes its pair from the skin's copy. Level 3 sets `_SkinCarve` and `_SkinCarveLength` on the skin's block, and
every shader term of it sits behind `_SkinCarve`. The skin reads one carve field for the whole body, at the vertex's
rest position (UV5) over one body-wide length, the median of the parts' smallest half extents at rest
(`CarveLengthShare` scales it; a part reads its own object units, one of which is its full extent), with the largest
part's seed. Each tissue's character is blended per vertex by the skinning weights into UV7 at a paint
(`TheatreOneSkin.BlendCarve`, from `TheatrePalette.PaintedCharacterOf`): the wrinkles' share of the lobe and wrinkle
gains, the depth share, and the weights of the soft-tissue box and of the soft leaf, which choose `TissueCarve` or
`LeafCarve` over the plain carve. The depth is `CarveDepth`'s fraction of the vertex's own half thickness, at most half
of it, inward along the skinned normal, with no pinch. The vertex stage carries the wrinkle share, the depth in metres
and the tissue weight in the varyings' leaf xyz, which a skin leaves free, and the soft-leaf weight in the shape's y, so
there is no new interpolator. The fragment rebuilds the normal from the gradient at the interpolated rest position and
carries it to the world through `SkinRestAxes`, as the papillae's slope is. The depth share is eased by a per-vertex concavity
(`Concavities`, from the mesh's own chords within k: the largest normal curvature, so a waist reads as a crease), so the creases
between lobes keep level 2's waist, and on carved tissue the sheen lobe widens (power toward 40) and weakens (half strength) with the carve's slope. A part whose smallest half extent is under 1.5 cells of the marching grid
is left out of the skin and drawn as its own part, since the mesher loses it. At 0 no object is made, no renderer
touched and no property set, and a frame is the one without it.

`EVOSIM_THEATRE_ONE_SKIN=4` is level 3 as a rig: the body reads as one creature that bends, and every statement of it sits behind
`TheatreOneSkin.Rigged`, so levels 1 to 3 build as they do without it. Three things change. The joint between two merged parts
is a ball and a pair blend whose radius is 0.7 (`RigJointShare`) of the thinner part's smallest half extent, with no 3 cm
cap: the pair blend is the smooth minimum of the two parts' fields at that radius, and the field is the lowest of the global
union (k as before, so away from joints the skin stays inside the colliders) and every pair blend. The skin may leave the
colliders at a joint by at most the ball's radius plus the inset (the pair blend alone by at most a quarter of its radius). The
grid's rim and the cull margin grow by that. The weights near a joint move toward the softmax of the joint's two parts' distances
alone, at 0.5 (`RigWeightShare`) of the joint radius, which runs the bend over about two radii; the share is the weight the
vertex already puts on the pair, times a smoothstep that is one within 1.5 radii of the anchor and zero beyond 3
(`RigZoneInner`, `RigZoneOuter`), so a third part's vertex is not pulled in and away from joints the weights are the softmax's
of k. The colour blends by the same weights, so a tissue's colour runs across a joint over the same width. Every part joins the
mesh, where the lower levels take the six types of `EVOSIM_THEATRE_SKIN_BODY`'s default, except a holdfast (`anchoring`), a nose
and a producer drawn as a blade (`TheatrePalette.DrawnAsLeaf`), which stand apart; a producer drawn as anything else
merges, and a flat box of another type drawn as a leaf stays in the mesh as a rounded box, as it does at every level. A set
`EVOSIM_THEATRE_SKIN_BODY` still rules the list. The mesh is built once per body at rest and per frame only Unity's skinning
runs; the log prints a `one skin rig` line a body with the joints, their radii and which types are in and out. A part under 1.5
cells of the grid still stands apart, as at every level.

`EVOSIM_THEATRE_ONE_SKIN=5` is level 4 filled to the collider at each joint, so the creature reads as thick as the world
makes it and two jointed parts meet as their colliders do. Every statement of it sits behind `TheatreOneSkin.Filleted`, or
behind the fill below above 0, which is 0 below level 5, so levels 1 to 4 build as they do without it. Two things change at
each joint between merged parts. (1) The ramp: each of the two parts' fields eases from the shape it is drawn as to its
collider, by a smoothstep in the distance from the smaller of the two faces (a box's face rectangle, or a round part's point
or line of contact), whole on that face and nothing beyond `Rig5RampShare` (1) of the thicker part's smallest half extent.
A box eases to its collider with the four edges along the face normal rounded as level 1's rounded box is, so its section at
the face is the face with rounded corners. The taper, the organic silhouette with its end rounding, and the margin fall to
nothing at the face, and two faces that meet in the world meet in the drawing. The skin's carve eases out over the same
ramp, through UV7's depth share. (2) The fillet takes the place of level 4's ball: the smooth minimum of the two parts at
`Rig5FilletShare` (0.4) of the thicker part's smallest half extent, at least `Rig5FilletFloor` (2) k so that it closes the
two insets between faces that meet, faded out with the ramp. It is held above the field of the two colliders and the wedge
between their faces (beyond both face planes to within k/4, and inside both faces' footprints; a round part's footprint is
the patch round its contact within which it is k/4 deep), so it draws nothing outside the colliders but in that wedge. Where
a big face meets a small one flat on, the big face stays flat round the small part and the corner between them is the
colliders' own; a round cap on a face keeps the neck the world has. The weights and the merged types are level 4's, the
log's rig line still prints level 4's joint radius (the weights' scale), and a `one skin rig5` line follows with the
fillets' radii, the ramps' reach, the fill and the carve's cap. At level 5 the skin's carve also cuts at most
`Rig5CarveCells` (0.5) grid cells deep, so that a cut taken as a share of a large or uneven half thickness cannot fold the
mesh. `EVOSIM_THEATRE_SKIN_FILL` (0 to 1, default 0), read at level 5 alone, cuts the skin's drawn box less
deeply by a factor of 1 - fill: the taper, the organic silhouette's rounding, ends, bulge and lean, the soft tissue's
margin and ruffle, and the skin's carve. At 1 a box is the rounded cube the palette swaps in, inside its collider but for
its rounded edges and corners. The parts' material and shader are untouched; the dial moves the skin's own shape alone.

`EVOSIM_THEATRE_ONE_SKIN=6` is level 5 that follows a bent hinge, so the tissue runs through a bend as a finger's does.
Every statement of it sits behind `TheatreOneSkin.BentHinge`, so levels 1 to 5 build as they do without it. Two things
change. (1) The sleeve, in the rest mesh: at a joint whose larger part (the one whose face is not the ramp's) is a drawn box
meeting on a box face, eight points a sleeve radius under that face are tested against the larger part as drawn. If one lies
outside it, the joint is waisted (a taper or an organic end narrowing toward the joint), and the smaller part's section is
carried straight into the larger part along the face normal. It is a capsule of radius `Rig6SleeveShare` (1) of the smaller
part's section (the smaller of a box face's half sizes, or a round part's smallest half extent), at most `Rig6SleeveCap`
(0.5) of the larger part's smallest half extent, running `Rig6SleeveDepth` (1) of its half extent in, joined to it by a
smooth minimum of `Rig6SleeveBlend` (1) radius and cut to its collider. The larger part's ramp then eases to its collider
only inside its drawn shape and the sleeve, so the skin narrows no further than the smaller part and no bead stands round the
contact. A joint that is not waisted keeps level 5's rest mesh. (2) The bend, per frame, from `Posed`: each hinge's bend is
the angle between its two bones' turns from rest. Past `Rig6BendLow` (10 degrees) each vertex of the hinge's zone (within the
larger of `RigZoneOuter` joint radii and the ramp's reach of the anchor, with weight on the two bones) is moved onto the field
evaluated with every part in its posed frame: each part reads its rest field at the point carried back by its own bone, and
the fillet's wedge is read with each face where its part stands, so the outside of a bend rounds over inside the opened
wedge and the inside creases. From where linear-blend skinning puts the vertex, up to `Rig6Steps` (3) Newton steps run
along the posed gradient, stopping where two steps' gradients turn by more than `Rig6StopDegrees` (55), a crease. The move
goes back into rest space by the inverse of the vertex's blended matrix, so Unity's skinning lands it there, capped at
`Rig6MaxPush` (1.5) fillet radii and eased in by a smoothstep of the bend up to `Rig6BendHigh` (30 degrees). The normal is
the posed gradient carried back the same way. The mesh's positions and normals are written each frame a hinge is bent and
set back once none is. The rest position (UV5) and rest normal (UV6) stay, so the markings ride on the tissue. It runs only
on a skin the camera saw the frame before (`Renderer.isVisible`), and not when no bone moved. The bones' boxes for the
bounds grow by the push cap. The bound is D242's: the posed field is inside the posed colliders and the wedge between their
faces, and the sleeve inside the larger collider. So a round cap on a face keeps the neck the world has, straight or bent.
The log prints a `one skin rig6` line per body with each hinge's faces' sizes and its sleeve, and a `one skin rig6 bend`
line each time a hinge's bend passes another 15 degrees, with the vertices moved and the milliseconds that frame took.
`EVOSIM_THEATRE_RIG6_BEYOND` (0 to 0.5, default 0), read at level 6 alone, is a test: at a hinge whose contact is a round
end on a face it lets the fillet reach outside the posed colliders and the wedge by up to that many of the round end's
radius r, its radius at least four times that, so the bend can read as a finger; every picture's label then carries
`joint tissue beyond the colliders (test)` (`SnapshotCamera.LabelLines`), and a `one skin rig6 beyond` line gives the
widest the body's tissue has gone beyond the bound, at rest and each time it grows by a quarter.

`EVOSIM_THEATRE_SKIN_SEAT` (0 to 4, default 0, off; useful from 1), read at every level of the one skin, lets a drawn box's
face meet its collider's where another part stands on it. The drawn box shapes a section only inward (the taper and bend,
the organic silhouette, the soft tissue's margin and ruffle), so at a joint anchor the drawn face can lie well under the
collider's and the joined part is drawn floating above the body. With the dial on, each drawn box takes a seat at every joint
anchor on it (each child's `ParentAnchorLocal`, and its own `ChildAnchorLocal` unless it is the root), of radius the dial times
the smallest half extent of the part on the joint's far side. At a point of the box's field the weight w is the largest over
the seats of one minus a smoothstep of the distance to the seat from r to 2r, and the shaping is blended toward none by w:
the organic factor toward 1, the margin's thickness toward 1 and its ruffle toward 0, the taper's scales toward 1 and the
bend's offset toward 0. The pillow's rounded cube stays, and the clamp box still floors the field, so nothing is drawn outside
a collider (D242). Off, no seat is made and no term is evaluated. `EVOSIM_THEATRE_SKIN_SEAT_PROBE=1` only logs, whatever the
dial: per body, at most 8 `skin seat probe` lines, each with the body, the part and the other part, the anchor in the part's
frame, and the drop, the distance from the anchor to the drawn surface along the inward normal of the face it sits on (0 when
the anchor is inside, -1 when the march finds no surface), once with the seat off and once on at the dial's value (2 when the
dial is off).

`EVOSIM_THEATRE_SKIN_WHOLE` (0 or 1, default 0), read once through `TheatreSkin.Dial` and only at the rig levels
(`EVOSIM_THEATRE_ONE_SKIN` 4 and up), makes the skin wrap the whole animal and leave out only its organs. An animal is a body with at
least one `link` part; a body with none (a plant) keeps the rule above, and a set `EVOSIM_THEATRE_SKIN_BODY` keeps ruling the list, which
turns the dial off. The organs are the nose, the holdfast (`anchoring`) and the mouth, which is a mark carved in the skin and not a part,
so a consumer merges like any other. Two things change. (1) A producer joins the mesh whether or not it is drawn as a blade
(`TheatrePalette.DrawnAsLeaf`) and is drawn by its collider, as any merged part is (D242). Its renderer is hidden with the others: a
blade is the lamina mesh on the part's one renderer, with its veins, curl and sway in the shader and the part's block, so there is no
second object, and the skin never takes its material and block from a blade. A producer in the skin does not sway. (2) A thin part is
kept, where the skin leaves a part under 1.5 cells (`ThinPartCells`) of the grid out and draws it apart. The parts the pass drops come
back but the organs, and the cell becomes the smaller of the usual one and the thinnest merged part's smallest half extent over 1.5,
never under the longest extent over `WholeCapDivisor`, which holds the grid to `WholeMaxCells` (128) cells an axis, padding included.
A part the cap would still leave under 1.5 cells is left out and counted, and the grid time grows with the cells: a refined body has up to
about seven times the cells of its usual grid, which only a body with a thin part pays. Round 62's safari logs put that at 3 to 4 % of
animals, at about 1 to 2 s more per skin build and 5 to 10 s at worst, against about 0.25 to 0.3 s for an animal today. A skin is built
once, when the body first comes into view, and a growth step only scales its bones. So the cost is a one-off pause and not a frame rate. A
crowd shot of 100 animals pays about 4 to 8 s more as they come into view: wall time in a film, which renders at a fixed step, and a
pause of 1 to 2 s per such body in a live view. The nose and the holdfast are handled as they are
without the dial, a nose under 1.5 cells of the usual grid included. The `one skin rig` line then ends with `whole:`, the producers and
the thin parts brought in, the parts left out at the cap, the cell and the usual cell, the grid and how many cells the thinnest part
spans; for a plant it ends with `whole: not an animal`. At 0 every statement runs as before.

`EVOSIM_THEATRE_SKIN_JOIN` (0 or 1, default 0) makes a limb's tissue read as the body's across its joint. It is read once
through `TheatreSkin.Dial`, and only at the rig levels (`EVOSIM_THEATRE_ONE_SKIN` 4 and up). The skin reads its pattern in one
frame, the rest frame, at one tile, strength, offset and turn for the body, all from the largest part's block. So at a joint
only the surface's turn against the triplanar read's three planes changes. A box's faces each read one plane. A round limb's
side turns between two, where the plain blend averages two unrelated fields below the pigment's threshold, so the side goes
plain while the limb's end and the fillet at its root stay patterned. With the dial on, three things change in the shader,
under the local keyword `_EVO_SKIN_JOIN` that `TheatreOneSkin.KeywordedOf` turns on in the skin's material copy alone.
(1) Each plane's field is thresholded first, and the covers are blended about the tile's mean cover (`_Pattern.w`) with the
weights renormalised by the root of their squares (`JoinCover`). That keeps the mean cover and nearly all the contrast. The
weights are the rest normal to the 8th power (`JoinBlendPower`; the parts use 4), so the lesser of two planes holds over a fifth
of the weight only within 5 degrees of their diagonal, against 10. One plane at full weight gives the cover the dial-off read
gives. (2) The pigment is worked out from the body's colour (`_BaseColor`, the largest part's) in place of the pixel's
(`JoinToneShare`, 1), so parts of different colour wear one pigment. (3) SKIN2's crease at a joint keeps 0.35
(`JoinCreaseKeep`) of its darkening and of its groove's depth, a gentle fold. The frame stays one frame for the whole skin, so
there is no seam to blend. A round limb's pattern can still run up to 1.4 times longer round the limb near a diagonal of the
planes. Colour and normal only: nothing moves, and D242 holds. At 0, and below level 4, a frame is the one without it, to the
pixel.

`EVOSIM_THEATRE_PATTERN_AA` (0, 1 or 2, default 0) makes the one skin's pattern follow the pixel's footprint on a surface
foreshortened in one direction. It is read once through `TheatreSkin.Dial`, acts on the one skin alone, and needs
`EVOSIM_THEATRE_SKIN2` on, since `PatternSkin2` is what it reads. The pattern fades to its mean where a cell is under about a
pixel across (`cellPixels`, from `pixelMetres`), and picks its mip level by the same length. `pixelMetres` is
`length(fwidth(positionWS))`, the footprint's long axis, so a limb's shaft seen along its length fades although its cells are
several pixels across the other way, and draws no maze. With the dial above 0, the fade and the mip level read another measure,
worked out in the open in `Fragment` from the world derivatives it already takes and left in `PatternAaMetres` for `PatternSkin2`
alone, under the local keyword `_EVO_PATTERN_AA` that `TheatreOneSkin.KeywordedOf` turns on in the skin's material copy, with the
dial's value in the global `_EvoPatternAa`. At 1 it is the area measure, `sqrt(length(cross(ddx, ddy)))`, the geometric mean of the
two axes. At 2 it is the minor axis held no smaller than the major over `PatternAaMaxAniso` (8, what an 8x anisotropic sampler
resolves): `max(min(a, b), max(a, b) / 8)`. A measure that is not finite or is under `PatternAaAreaFloor` (1e-6 m) is not used,
and `pixelMetres` stands. A part, and a pixel off the skin, are as they were. Colour only: nothing moves, and D242 holds. At 0 a
frame is the one without it, to the pixel.

`EVOSIM_THEATRE_PADDLE_LIMB` (0 or 1, default 0), read once through `TheatreSkin.Dial` and only at the rig levels
(`EVOSIM_THEATRE_ONE_SKIN` 4 and up), draws a link part as a paddle: round and thick where it joins the body, flat and wide toward
its free end. Only the field the skin is marched from changes (`TheatreOneSkin.PaddleShape`), so the skin stays one surface with the
body's colour and markings throughout, and the collider and the world are untouched. A link is reshaped when it is a capsule the skin
draws as a capsule, or a box, whose half length is at least 2.5 of its radius (`PaddleLengthShare`: a capsule's half span plus radius
over its radius, a box's longest half extent over its middle one); when it has a joint to a merged part at one end, its anchor at least
half the half length from the centre (`PaddleRootShare`), which is the root: its parent's joint, else its first merged child's; and when
no other part stands on its blade. The part's field is today's over the first quarter of the way from the root to the start of the
blade's rounded end (`PaddleStartShare`), and is blended into the blade's from there by a smootherstep, so the profile has no kink and
the sleeve, the fillet and the joint's weights meet the shape they meet today. The blade is the collider squashed across its thickness:
its section an ellipse whose half width is the collider's radius (a box's middle half extent, along its widest face) and whose visible
half thickness is a third of its visible half width (`PaddleMinorShare`), its end a squashed half disc. Today's shape and the blade are
both inside the collider, so their blend is, and the outline stays inside it (D242). A capsule drawn with its narrow axis at least 0.8
of its wide one (`PaddleRoundShare`) lays its blade in the plane of its axis and its hinge axis, so the stroke meets it broadside; a
flatter one keeps its wider extent as the blade's width and is never drawn thicker across than it is today. The cell is refined until
each tip's half thickness spans 1.5 cells (`ThinPartCells`), to 128 cells an axis (`WholeMaxCells`), and a tip the cap leaves thinner
is floored there, so a body with a paddle pays a refined grid's build time as `EVOSIM_THEATRE_SKIN_WHOLE` does. The light through thin
tissue rises toward the blade's edge from the vertex's own half thickness, with no shader change. The log prints one `paddle limb` line
per body with a link in the skin: the links reshaped, each tip's visible half width and half thickness, the cell and the usual cell,
why each other link stays round, and the largest distance any vertex stands outside its own collider on the blade, outside every
collider on the blade, and at the root, which keeps today's shape and the hinge's bridge.
`EVOSIM_THEATRE_PADDLE_WIDTH` (1 to 3, default 1, read once through `TheatreSkin.Dial`, and only with this dial on) is an owner preview that draws outside the collider (D242): at N above 1 a round capsule's blade grows along the same blend from the collider's radius to N times it at the free end (`PaddleShape.WidenPoint`, between `// WIDTH begin` and `// WIDTH end`), its half thickness as at 1 so that it reads thinner against its width, a box and a flat capsule are unchanged, and the `paddle limb` line adds N and reports the vertices' true distance outside the collider. The C# lines sit between `// PADDLE begin` and `// PADDLE end`. At 0, and
below level 4, a frame is the one without it, to the pixel.
`EVOSIM_THEATRE_PADDLE_HINGE` (0 or 1, default 0, read once through `TheatreSkin.Dial`, and only with `EVOSIM_THEATRE_PADDLE_LIMB` 1) lays a flat capsule's blade on its hinge as a
round capsule's is, its width the collider's radius and its half thickness a third of the visible half width (never past the radius), and turns a box's blade to the one of its
two short axes nearer the hinge, with the width and thickness bounds swapped with them, so the blade stays inside its collider and meets the stroke broadside; the log has one
`paddle orientation` line per paddle, the angle between its thin axis and the hinge, and says which the dial re-laid.

`EVOSIM_THEATRE_DEBUG_TINT` (0 or 1, default 0), read once, is a diagnostic and never a look. At 1 every renderer under a
creature's root is drawn in one flat colour by its kind, with no light, fog, pattern or mark, so a frame answers what draws a
piece of a body. The kinds (`TheatreDebugTint.KindOf`, from the names and meshes the theatre gives what it makes): fin web cyan,
part piece red for a box, orange for a rod's cylinder shaft and yellow for a ball or a cap, knuckle green, bridge dark green, mouth
mark grey, anything else blue. The one skin is coloured per vertex (`TheatreOneSkin.DebugColours`): black within 1.35 mouth radii of a
skin mouth (`DebugMouthReach`, the shader's `MouthReach`), white where its largest bone weight is under 0.75 (`DebugBlendShare`), a
joint's blend, and otherwise by its largest bone's slot, magenta, violet, pink and plum in turn. The skin's joint balls and sleeves
are part of the skin's mesh, not renderers of their own. A renderer on the body shader takes `_DebugTint` on its block, and the
forward fragment returns it first under the keyword `_EVO_DEBUG_TINT`; one on another shader (the mouth mark) takes an unlit
material of its colour. The pass runs at the end of every paint and every skin dress. The log has one `debug tint` legend line a run,
one count line per body (renderers drawn of all, by kind) and one `debug tint skin` line per skin (vertices by colour, each slot's
part). The C# lines sit between `// DEBUGTINT begin` and `// DEBUGTINT end`, the shader's inside `#if defined(_EVO_DEBUG_TINT)`. At 0
a frame is the one without it, to the pixel.

`EVOSIM_THEATRE_LOOK_STAGE` (0 to 3, default 0), read once through `TheatreSkin.Dial`, is a diagnostic and never a look, and stays off in
a film. Above 0 a producer's pixel, a blade or under `THALLI` a thallus, is returned early from the forward fragment with its colour at
one stage, opaque and in linear as the shader holds it, and every other pixel draws as usual. At 1 it is the body colour as it enters
the lighting, after every tone and tint on the body and before the key's diffuse term; at 2 the diffuse light
alone; at 3 the whole lit colour before the water, the rim, the glow, the light through, the sheen, the caustic and the leaf tone
included and the fog not yet mixed. The value is the global `_EvoLookStage`, the shader's lines sit under `_EVO_LOOK_STAGE` in the
forward pass alone (the depth passes draw no colour), the log has one line a run naming what the value draws, and the C# lines sit
between `// LOOKSTAGE begin` and `// LOOKSTAGE end`. The camera's tonemapping, post exposure, bloom and vignette still act on the
returned colour. At 0 a frame is the one without it, to the pixel.

`EVOSIM_THEATRE_LOOK_PROBE` (0 to 3, an integer, default 0), read once through `TheatreSkin.Dial`, is a diagnostic and never a look, and stays
off in a film. It switches one colour term in the forward fragment, to show whether the term is live. At 1 the multiply by the level-2
colour ramp (`AlgaBladeColour`) on a living blade is skipped, so the ramp's ratio is one, the lean's division before it still runs, and
every other term is untouched. At 2 a sea-lettuce thallus (`ThalliIsUlva`, under `THALLI`) takes `float3(1, 0, 1)` as its leaf tone, so it
draws magenta wherever the multiply on a part's whole light (`lit *= leafTone`) reaches it. At 3 every thallus takes a flat leaf tone by
its form, at the same line and under `THALLI`: sea lettuce (`ThalliIsUlva`) `float3(1, 0, 0)`, Padina (`ThalliFlagPadina` within
`ThalliFlagTolerance`) `float3(0, 1, 0)` and Codium (`ThalliIsCodium`) `float3(0, 0, 1)`, which shows what form each thallus is. The
value is the global `_EvoLookProbe`, the shader's lines sit under `_EVO_LOOK_PROBE` (probes 2 and 3 also under `_EVO_THALLI`) in the
forward pass alone, the log has one line a run naming what the value does, and the C# lines sit between `// LOOKPROBE begin` and
`// LOOKPROBE end`. At 0 a frame is the one without it, to the pixel.

`EVOSIM_THEATRE_JOIN_DEBUG` (0 to 4, default 0), read once, is a diagnostic and never a look, for the pale patternless crescent
round a limb's root under `EVOSIM_THEATRE_SKIN_JOIN`. It acts on the one skin alone, under the local keyword `_EVO_JOIN_DEBUG` that
`TheatreOneSkin.KeywordedOf` turns on in the skin's material copy while the dial is above 0, with the global `_EvoJoinDebug` at its
value. The skin's final colour is replaced by an unlit false colour, returned at the end of the forward fragment, so nothing after
the view's numbers (light, fog, grade) touches it. At 1 the triplanar plane weights are red, green and blue for x, y and z, as
`JoinCover` computes them after `JoinBlendPower` and the normalisation. At 2 the cover `JoinCover` returns is grey, black at 0 and
white at 1, with no colour, band, light or crease after it. Both need `SKIN_JOIN` on, and show magenta where `JoinCover` did not run.
At 3 red is the amount `Skin2MarginB` lerps the body toward its band colour (its cover times its opacity, every term, so it needs
`SKIN2` at 2 or more), green is the luminance the thin backlight adds, over `JoinDebugThinFull` (0.25) for full green, and blue is the
vertex's crease mark (marks z) as the vertex carries it, before `JoinCreaseKeep`. At 4 the stretch of the maze's rest coordinate
against the drawn surface, `length(fwidth(rest metres))` over `length(fwidth(world position))` times `_SkinScale` so that an
unstretched surface reads 1, is `JoinDebugStretchGrey` (0.5) grey at 1, blue toward `JoinDebugStretchLow` (0.5) and red toward
`JoinDebugStretchHigh` (2), each full at its value, on a log scale. Nothing moves. The C# lines sit between `// JOINDEBUG begin` and
`// JOINDEBUG end`, the shader's inside `#if defined(_EVO_JOIN_DEBUG)`. At 0 a frame is the one without it, to the pixel.

`EVOSIM_THEATRE_SKIN2` (0, off; 1 on) gives the one skin a second look: a diffuse sheen, light through the tissue,
asymmetry, a pattern by clade and a margin band on its edges. At 0 a frame is the one without it, to the pixel, with any
other dial on or off: its shader lines are all inside `#if defined(_EVO_SKIN2)`, under a `multi_compile` keyword in each
of the three passes that `TheatreSkin.EnsureSkin2Keyword` turns on, only while the dial is on, and every term is behind
a test of the skin's own shape flag (the shape channel's x above 2.5), so a part draws as it did apart from the part terms below. It shades and colours
only: no vertex moves, and nothing is read from the screen, so every mark rides on the tissue in the rest frame. The
skin mesh carries four marks per vertex in its leaf channel (UV4, zero otherwise; `TheatreOneSkin.Skin2Marks`), built
once from the rest mesh: x how thin the tissue is, one minus the vertex's half thickness over the 80th percentile of its
part's, kept whole on a flat part and at 0.35 on a round one, smoothed over the mesh; y how near the rest normal lies to
its part's rim, the part's flatness times one minus the normal's cosine with its thin axis; z a joint's crease, one
where the two heaviest skinning weights are even and zero from a quarter to three quarters; w the crease's depth, 0.15
of the half thickness. The body-wide length the noises are read over is `_SkinCarveLength`, which the skin sets at every
level under the dial. (1) The sheen replaces the tight lobe (power 96, as the velvet's replaces the sheen): a lobe of
power 20 at half the strength, on the same normal and under `_Sss` its gain, and a Fresnel sheen at 0.11 of the key by
`(1 - n.v)^3`, the key's wrap and the tissue's gloss (`saturate(_Sheen * 10)`). Parts keep power 28 or the soft tissue's
lobe. (2) The diffuse wraps 0.35 wider than `_Wrap`, and the light the wider wrap adds is a half mix of the body's
colour and the transmission hue at the body's luminance; thin tissue (x from 0.3 to 0.85) takes up to 0.9 times
`Transmission` at full thickness on top of the metre-based one, which is one nearly everywhere on a small body. (3)
Asymmetry: a value noise of the rest position, wavelength three times `_SkinCarveLength`, seeded by `_Carve.x` and
`_Individual`, moves the colour's value by 6 % and its hue by 0.04 of the wheel either way, and sets the margin's width;
the pattern's coordinates are warped by 0.08 of a tile over 1.6 tiles; a second octave of relief, 0.3 of
`_SkinCarveLength` long at a slope of about 0.12, tilts the normal and fades out under three to eight pixels; a crease
darkens by 0.18 and tilts the normal by Mikkelsen's surface gradient of its depth. (4) The pattern's regime comes from
the clade, not from investment (`TheatrePattern.StyleOf`): by a hash of the clade 30 % spots (bins 0, 1), 25 % stripes
(7, 8), 23 % a fine mottle (a net, bins 3 to 5, at 0.45 of the tile and 0.85 of the strength), 22 % nearly plain (0.12
of the strength, 1.4 tiles). The palette writes the clade's two fractions into `_PatternShift`'s z and w after a part's
own paint (`PaintSkin2`; z is zero on a greyed body, a corpse and a neck), and the tile the style names into `_Pattern`
and `_PatternTex` where the pattern dial draws one, on every part. The clade is the founder's number from
`TheatrePalette.LineageOf` when a caller has set it (the safari does), else the hash of the pattern key's cell types,
which kin share. `PatternSkin2` is `PatternSkin` on the one skin with the warp and a pigment pair in the body's own hue
family, picked by the clade's second fraction (`_PatternShift.w`): the dark pair as before, or for 35 % of clades a pale
one at 1.9 times the body's luminance, the hue turned by up to 0.07 of the wheel. (5) The margin band: the edge measure
is the larger of x (a little lifted by y) and y's own call (0.55 to 0.9), the band starts at `1 - sqrt(2w - w^2)` for a
width w of 4 to 10 % of the local half width by the asymmetry noise, and is painted before the colour ramp and the
countershade. By the clade's first fraction (`_PatternShift.z`) a quarter have none, 30 % orange with a pale inner line
(white on a warm body), 15 % pale, 10 % dark (pale on a dark body) and 20 % the body's complement at 1.4 times its
saturation; its luminance is the body's times 1.6 plus 0.06, between 0.16 and 0.6. `RIM_BAND`, on box parts, is left as
it is on the skin; a part's band is the next paragraph's. The constants (`Skin2*`) live in `TheatreBody.shader` beside `PatternSkin2`, and the style's in `TheatrePattern`.

`EVOSIM_THEATRE_SKIN2` on a part (the same dial and keyword; a part has none of its own) gives a body part that is not the one
skin, and is drawn in animal tissue, the skin's look as nearly as a part can. Every line of it is in a `#if defined(_EVO_SKIN2)`
region of its own beside the skin's, behind `skin2Part` (the shape channel's x not above 2.5, `_Skin2Part` above one half, not a
blade, `_Velvet` off), so the skin's lines are as they were and a part draws as it did with the dial off. The palette's
`PaintSkin2` writes `_Skin2Part` per renderer: one on a part in colour, zero on a greyed body and a corpse, on a producer
(photosynthetic), a holdfast (anchoring), a gas bladder (a float next to a blade), a stipe (a structural chain hanging a blade) and a
velvet absorber; a neck is not painted there and keeps zero. A part has none of the skin's vertex marks, so `Skin2PartMarks` makes
them from its frame at the undisplaced object position: the thin axis is the smallest half extent (`HalfExtentsWS`, RIM_BAND's tie
rule), the flatness the skin's (`Skin2PartFlatLow` 0.1 to `Skin2PartFlatHigh` 0.6 of one minus the smallest over the middle half
extent), mark x one minus the local thickness times a lerp from `Skin2PartRoundThin` (0.35) to one by the flatness, mark y the flatness
times one minus the mesh normal's cosine with the thin axis. The thickness is a half ellipse whose half thickness is the smallest half
extent (world metres, since a part's matrix is not scaled: there is no `_SkinScale`): on the box it is sqrt(1 - e^2) with e RIM_BAND's
section (the p-norm of 8 of the two other coordinates over the mesh half), on a capsule or a sphere the normal's cosine with the thin
axis, the mesh normal being the world normal taken back through the matrix (its components along the part's axes times the column
lengths). (1) The sheen: a part's lobe is the `_Sss` branch's (power 28 on the normal turned `SssSheenSmooth` back toward the uncarved
one, at `SssSheenGain`) or the plain one (power 28); both are replaced, from `litBeforeSheen`, by the skin's lobe (power 20 at half the
gain) and Fresnel (0.11), with the skin's constants. The soft tissue's own lobe (`tissueNormal.x` above a half, power 9 at 0.45) is
broader than that and stays, and takes the Fresnel alone. (2) The wrap and the backlight are the skin's lines (0.35 wider, half
tinted by the transmission hue; thin backlight from mark x between 0.3 and 0.85, at full thickness). (3) Asymmetry is `Skin2Tone`
on the part's metres, its wavelength three times the part's smallest half extent (`Skin2PartNoiseShare`, 1), seeded by `_Carve.x`
(on a part the body's seed plus a step per part, so a pair of limbs differ) and `_Individual`; there is no sign term, because a
part's object axes carry no left and right, and the noise already differs across the part. (4) `PaintSkin2` already gives every part
the clade's tile, strength, turn, cover, offset and texture where the pattern dial draws one; a part under this dial draws it with
`PatternSkin2` (the warp, and the clade's dark or pale pigment pair) on its own planes, in place of `PatternSkin`'s dark pair. (5) The
margin band is `Skin2Margin` with the part's marks: the larger of x (a little lifted by y) and y's own call, the width 4 to 10 % by
the noise, the colour by `_PatternShift.zw`, none for a clade without one, a greyed body, a corpse or a neck. It is read at the
undisplaced object position, so it stays on the tissue, and nothing reads the screen. Where the clade has a band and the part is flat
(flatness above 0.001), it replaces `RIM_BAND`'s (the body goes back to what it was before `RimBand`); a clade with none, or a round
part, keeps `RIM_BAND` as it was. The constants (`Skin2Part*`) live in `TheatreBody.shader` beside `Skin2PartMarks`.

`EVOSIM_THEATRE_SKIN2=2` (the dial reads 0 to 2; 1 is the two paragraphs above, unchanged) lays the margin band where a real margin is:
the body's outline seen across its broad face, as a flatworm's or a nudibranch's mantle edge is, and not the thin rim of a flat part
seen edge-on. Its shader lines are all inside `#if defined(_EVO_SKIN2) && defined(_EVO_SKIN2B)`, under a `multi_compile` keyword in each
of the three passes that `TheatreSkin.EnsureSkin2Keyword` turns on beside `_EVO_SKIN2`, at level 2 only, so at 0 or 1 a frame is the one
without it, to the pixel, with any other dial on or off. It replaces the band's cover and its colour (`Skin2MarginB`, run over the body as
level 1's `Skin2Margin` left it, on the skin and on a part) and nothing else: the creases, the sheen, the light through the tissue and the
pattern are level 1's. The cover is the product of three terms, each a smoothstep of an interpolated measure evaluated per pixel, so no
per-vertex step shows. (1) Thinness: mark x, a smoothstep `Skin2BSoft` (0.12) either side of the start the band's width sets, which is
level 1's (4 to 10 % of the local half width by the asymmetry noise). (2) The rim gate: mark y from `Skin2BRimLow` (0.25) to
`Skin2BRimHigh` (0.6). It is the rest normal's turn away from the part's thinnest axis, the part's flatness times one minus the normal's
cosine with that axis, so a broad face has none however it is seen, and a round part has none. On the skin `TheatreOneSkin.Skin2BOutline`
multiplies it by a hinge fade of the vertex's second heaviest skinning weight over the four (the mesh's own `BoneWeight`, so no channel is
added): one below `Skin2BHingeLow` (0.08), zero from `Skin2BHingeHigh` (0.2), so a joint's neck, where two parts share the vertex, takes
no band. (3) The view: one minus the absolute cosine of the geometric normal with the direction to the eye, from `Skin2BViewLow` (0.15) to
`Skin2BViewHigh` (0.55), so the band shows where the surface turns away from the eye, the outline across a face, and a rim wall that faces
the eye shows none. The first two are the rest frame's and ride on the tissue; the third is shading, as a Fresnel term is, and reads no
screen position. The colour is level 1's palette without the complement: the clades that took it take the body's own hue turned
`Skin2BNearTurn` (0.08 of the wheel), the quarter of clades without a band still have none, and every band hue is held to
`Skin2BSaturation` (1.1) times the body's HSV saturation, at least `Skin2BSatFloor` (0.08). A part takes the same function with its own
marks, which are made per pixel from its frame: its band is the outline across its broad face, and its side wall seen edge-on has none.

`EVOSIM_THEATRE_SKIN2_BAND` (1 to 4, default 1; read only at `EVOSIM_THEATRE_SKIN2=2`) makes the level 2 band bolder at the outline, as a
flatworm's or a nudibranch's mantle margin is. It multiplies the band's width (level 1's 4 to 10 % of the local half width, which sets the
start `Skin2MarginB`'s thinness smoothstep sits on), capped at `Skin2BBandWidthCap` (0.25 of the local half width), and its opacity
(`Skin2MarginOpacity`, 0.9), capped at 1. The rim gate, the view term, the hinge fade and the saturation cap (`Skin2BSaturation`, 1.1) are
as they were, so the band is still drawn only at the outline. The new shader lines are all inside the level 2 region
(`#if defined(_EVO_SKIN2) && defined(_EVO_SKIN2B)`), no keyword is added, and `TheatreSkin.EnsureSkin2Keyword` writes the dial to the global
`_EvoSkin2BBand` beside the keyword; the shader reads it as `max(_EvoSkin2BBand, 1)`. At 1 each changed expression is the old one to the
bit (the width times one, then the smaller of it and a cap above the widest band; the opacity times one, then the smaller of it and one).
Above 1 the log has one `skin 2 band` line.
At 4 the band is level 3's (the width and the opacity stop at 3, so the cap is `Skin2BBandWidthCap3`, 0.5) with three more edges on
the one skin, which has no thin or rim mark on a round body and so no band below 4. Each recolours the drawn surface in the colour
`Skin2MarginB` already computes for the clade and moves nothing, and the cover is the largest of level 3's and the three, each times
its own strength (`Skin2B4Edges`). A part and the mouth's lip are level 3's. (1) Part ends: the last `Skin2BEndFraction` (0.15) of a
part's half length along its longest axis, a per-vertex mark in UV0 that no other path writes (`TheatreOneSkin.Skin2BEnds`; zero in
the middle, one at the very end), multiplied by the part's elongation (a smoothstep of one minus the middle half extent over the
longest, from `Skin2BEndElongLow` 0.1 to `Skin2BEndElongHigh` 0.4, so a sphere has no ends) and by the hinge fade, so a joint face
buried in a neighbour takes none. The interpolators are full, so the mark rides in the skin's tissue flag (`Varyings.tissueNormal.x`,
zero on a skin and read against a half by every reader) times `Skin2B4EndPack` (0.4). The shader shows it from `Skin2B4EndLow` (0.15)
to `Skin2B4EndHigh` (0.55) at `Skin2B4EndStrength` (0.9). (2) The crease: mark z within `Skin2B4CreaseWidth` (0.2) of
`Skin2B4CreaseAt` (0.5), a thin line along each flank of a joint's crease and not its whole width, at `Skin2B4CreaseStrength` (0.8).
(3) A thin silhouette rim: level 2's view term from `Skin2B4SilLow` (0.55) to `Skin2B4SilHigh` (0.95), narrower than level 2's, times
`Skin2B4SilFloor` (0.35) to one by the thinness mark x so a fat part's is faint, at `Skin2B4SilStrength` (0.6). The shader lines are
all inside `#if defined(_EVO_SKIN2_BAND4)`, a `multi_compile` keyword in each of the three passes that
`TheatreSkin.EnsureSkin2Keyword` turns on only at 4; below 4 the shader and the skin mesh (which then has no UV0) are as they were, to
the bit. The log has one `skin 2 band 4` line.

`EVOSIM_THEATRE_SHEEN_CAP` (0, off; 1 on; read only with `EVOSIM_THEATRE_SKIN2` on) keeps a surface that faces the light in its
own colour under SKIN2's sheen, with a soft highlight. It rolls off the white the sheen adds: the lobe and the Fresnel on the one
skin and on a part, or on a part with the soft tissue its own lobe and the Fresnel. The ceiling is `SheenCapShare` (0.5) of the
luminance of the body's own key light at the pixel (`litBeforeSheen`: the diffuse, the wider wrap and the light through), reached by
the soft knee `c (1 - exp(-w / c))`, so a faint sheen passes nearly whole and the peak stops at the ceiling. The white is scaled, not
recoloured, so an iridescent tint keeps its hue. Uncapped, the lobe's peak is `_Sheen` times `Skin2SheenGain` of the key, 0.225 on a
biter. That is about three times a red body's own light, and leaves the surface under a third of its saturation; at 0.5 the
highlight lifts it by 0.58 EV and leaves 70 %. The fill, the lamp, the ambient, the rim, the glow, the exposure and the grade are
untouched. Its shader lines are all inside `#if defined(_EVO_SHEEN_CAP)` regions within SKIN2's. The keyword is a `multi_compile` in
each of the three passes, and `TheatreSkin.EnsureSkin2Keyword` turns it on beside `_EVO_SKIN2` only while the dial is on, so at 0 a
frame is the one without it, to the pixel, with any other dial on or off. The log has one `sheen cap on` line.
The range is 0 to 2: at 2 it is 1 where SKIN2 is on, and where SKIN2 is off it rolls the white the base tissue's own specular adds
(the soft tissue's lobe, the `_Sss` branch's or the plain one, and on the one skin at ONE_SKIN 3 its tight lobe) off toward the same
ceiling, on the one skin and on a creature's part (not a blade, a velvet absorber, a seaweed or a thallus), by `SheenCappedBase`
inside `#if defined(_EVO_SHEEN_CAP2)`, a `multi_compile` in the same three passes that `TheatreSkin.EnsureSheenCapBaseKeyword` turns
on only at 2 with SKIN2 off, so at 0 and 1 a frame is as it was, to the pixel.

`EVOSIM_THEATRE_NOSE_PIT` (0, off) draws a nose part (cell type `nose`, D235's) at its true size with an olfactory pit
cupped into the face at its sensing tip (D242, N2: a short snout with a nostril). At 0 a frame is the one without it, to the
pixel, with any other dial on or off: its shader lines are all inside `#if defined(_EVO_NOSE_PIT)`, under a `multi_compile`
keyword in each of the three passes that `TheatreNosePit` turns on the first time it paints a nose and nothing turns off, so a
world with no nose never compiles them, and the palette's one hook is a test of the dial. The pit is centred on `MouthPoint.Local`,
the point the world reads the smell at, with a radius of 0.6 of the tip face's smaller half extent and a depth of half the nose's
length, at most 0.8 of that half extent. It reaches the shader as `_Pit` (the tip in the visual's object units, the depth in radii
in w) and `_PitRadii` (the radius along each object axis), set per renderer by `TheatreNosePit.Paint` after the palette's own
`Set`, so a growth step and a corpse's shrink leave it right. A vertex inside the pit is moved inward along its normal by a cup
(a flat floor out to 0.35 radius, a smoothstep wall to the rim) and the carve is calmed round it; the fragment tilts the normal
as the cup's wall and a slight lip, and darkens the albedo and the light with the cup's depth. It is occlusion alone: no
emission, no light and no glow, which wait for bioluminescence as a world rule. Every move is inward, so the nose stays inside
its collider. The box mesh has 24 segments to a face edge, which puts about 14 vertices across a pit on a square face, and the
fragment carries the rest, so a very long thin face gets a coarse cup. The pit is drawn on the part's own renderer: the one skin
does not merge a nose by default, and a nose merged into it (`EVOSIM_THEATRE_SKIN_BODY`) is drawn without the pit. A blade, a raw
shape (`X`) and a mesh the theatre did not make get none. `Evosim.Theatre.EditorTools.NosePitStill.Run` draws a hand-made
body (a 0.2 by 0.12 by 0.12 m root and a 0.06 by 0.03 by 0.03 m nose on its +x face, developed with the nose table) from
three views into `scratch/nose-pit/`; launch it as `TheatreSoloStill` is launched, once at each setting of the dial, and it
refuses to run without the nose table.

`EVOSIM_THEATRE_NOSE_SNOUT` (0, off; 1 on) draws a nose part that stands apart from the one skin as a snout in the body's own
colour, narrowing to its tip, where without it the nose is a straight-sided box. At 0 a frame is the one without it, to the
pixel, with any other dial on or off: it adds no shader line, and every C# addition sits behind a test of the dial. It acts on a
nose part drawn as a rounded box on its own renderer, with or without the pit's dial; a nose merged into the skin
(`EVOSIM_THEATRE_NOSE_PIT=2`), a raw shape (`X`), a blade, and a sphere or capsule nose are left as they are. (1) The shape is a
mesh built from the nose's rounded cube and cached by class (`TheatreMeshes.Snout`: one per tip axis and end, rounding, skirt
and flare, at most 48, past which a nose keeps its box). From the base face to the tip the cross section falls from the
collider's to 0.5 of it (`SnoutTipShare`) as `1 - 0.5 u^1.4` (`SnoutTaperPower`), u the distance from the base face over the
length, so that part is inside the collider. Below the base face a skirt runs into the parent by `SnoutBuryShare` (1.0) of the
nose's smaller half width, at most `SnoutParentShare` (0.4) of the parent's thickness along its face normal, and widens as a
quadratic to `SnoutFlareShare` (0.5) of each half width past the collider, cut to the parent's clearance round the joint, so the
skirt lies inside the parent's collider (D242's tissue across a hinge). The parent's face comes from the child's
`ParentAnchorLocal` and the parent's half extents; a sphere or capsule parent gets a skirt and no flare. The parent's carve can
sink its drawn surface below its collider face, so a deep dimple at the joint can show the skirt's edge. The mesh is not a box to
the shader, so the box taper, bend, clamp and soft tissue do not act on it; its carve does, at `SnoutCarveTake` (0.5) of the
nose's usual depth because the tip is narrow, and the joint pinch is set to zero so the base is not waisted. (2) The colour is the
root part's painted colours (body, rim and light through, read back from its block as `TheatrePalette.PaintedColourOf` reads one),
copied to the nose once the whole body is painted, so the lineage's turn, the reserve and a greyed or dead body follow; the
knuckle at the nose's joint takes the root's guild colour. From `SnoutLightenFrom` (0.35) of the nose's length to its tip the
shader's colour ramp (`_RampAxis`, `_RampTip`) multiplies the body colour by up to 1 + `SnoutLighten` (0.3) in linear light, hue
kept; a greyed body or a corpse takes no ramp. The pattern is the body's, as on every part: the pattern pass already gives the
nose the body's tile, strength and cover, and the snout adds none. (3) The pit keeps `MouthPoint.Local` and the pit's rules, on
the narrowed tip: its radius is `RadiusShare` (0.6) of the tip's smaller half width and its depth the pit's rule on that half
width. The log prints one `nose snout` line per nose: the base and tip half widths, the flare past the collider in metres and as
a share of the half width, the skirt, the pit radius, and the hue taken and where from.

Where a part stands apart from the one skin and its parent is in it (a nose, by default), the skin writes one `nose seat` line per
such part at its first dress, at every level of the skin and whatever the dials; it only logs. It reads the line through the joint
anchor along the inward normal of the parent's box face the anchor sits on (the axis of the largest anchor over half extent), in the
parent's rest metres: the depth of the drawn skin there against the face, from the built mesh (negative behind the face), and of the
parent's drawn box alone with no seat and with the seats it was built with (`DrawnBox.Drop`); the part's back face; the gap, every
stretch of the line outside the skin from the back face to the body, and every skin crossing, so a seat's dome standing proud of a
receded face reads as an entry, an exit and a second entry; the seat the parent took there; and each knuckle and bridge the palette
has at that joint, with its span along the line, whether it is drawn and whether it lies in the gap, and the snout's skirt when
`EVOSIM_THEATRE_NOSE_SNOUT` is on. `EVOSIM_THEATRE_SEAT_MARKER` (0, off; 1 on) is a diagnostic: at such a joint it hides the
palette's knuckles and bridges and leaves out the `EVOSIM_THEATRE_SKIN_SEAT` seat the part would make on its parent's drawn box.
`EVOSIM_THEATRE_ORGAN_SEAT` (0, off; 1 on; 2 on with the cone's spread at 0.5, a narrower seat) seats a part standing apart on a Fixed joint whose parent is drawn as a box: in place of
SKIN_SEAT's ball the box takes a cone seat, whose weight is 1 within a radius of the face axis through the anchor and down to the
box's own drop there with no seat, and falls by a smoothstep over `OrganSeatBand` (1) of the face radius beyond the cone's side or
its depth. The radius is the part's largest half reach across the face at the face (`OrganSeatFootprint`, 1) and widens by
`OrganSeatSpread` (1, a 45-degree flank) for each metre in. The box's shaping is eased by that weight as SKIN_SEAT eases it, so its
face is pulled out to the collider's across the part's footprint and runs back into the body, and the pillow's cube and the clamp box
keep it inside the collider (D242). The palette's bridges at that joint are hidden and its knuckles kept, which close the skin's
inset of a quarter of k. A joint that moves, a parent not drawn as a box and an anchor its box already reaches take no cone. Both
dials are read once, at every level of the skin; at 0 no statement of either runs, so a frame is the one without them, to the pixel.

`EVOSIM_THEATRE_FAMILY_MESH` (0 off by default, 1 on) draws a superellipsoid part (D233's fourth shape) as its own solid,
where without it the part is drawn as a box of its half extents. At 0 a frame is the one without it, to the pixel, with any
other dial on or off: it adds no shader line and no keyword, and its C# lines sit between `// FAMILYMESH begin` and
`// FAMILYMESH end`, a case before `default` in `SnapshotWorld.AddVisuals` and in `LiveWorldView.Plan`, each guarded by
`TheatreFamilyMesh.Wants`, which is false at 0. On, a part whose shape is a `SuperellipsoidShape` and whose `Family` is set
takes `TheatreFamilyMesh.For`: six cube faces of 24 by 24 quads (3,750 vertices, 6,912 triangles), every vertex put on the
surface by `Superellipsoid.SurfacePointAndNormal` with Core's own normal there, and nothing welded across a face. The mesh is
in the unit solid's units, the surface at the family's `ReferenceHalfExtents` over twice those half extents, which depends on
the form alone, so there is one mesh to a form (cached by `PartForm`, about 160 kB each, dropped by `TheatreMeshes.Release`),
and the visual's scale is twice the reference half extents times `PhenotypePart.FamilyScale` (`TheatreFamilyMesh.ScaleOf`),
which is the box's own 2|h|. Everything that reads a visual's scale as the part's size (the palette's pinches, ramps and
anchors, the shader's `HalfExtentsWS`, the carve's depth and wavelength) therefore reads it as it does for a box. The mesh
carries 0 in the shape channel (UV3), a plain surface to the shader: no box taper, bend, clamp or soft tissue, and the carve,
which only cuts inward. `TheatrePalette` is unchanged: it swaps a mesh only for the engine's own cube, sphere and cylinder,
matched by reference or name, and keeps any other, under the raw shapes (`X`) too. A part with no family, or a reference half
extent of zero, keeps the box. The one skin (`TheatreOneSkin`) is not changed by it. The log prints one line at the first
superellipsoid part drawn, with the resolution.

`EVOSIM_THEATRE_FAMILY_SKIN` (0 off by default, 1 on) draws a family part inside the one skin, where without it the skin
builds the part's field as a box (the `default` of `PrimitiveOf`, `ColliderOf5` and `Face5`). At 0 a frame is the one without
it, to the pixel, with any other dial on or off: it adds no shader line and no keyword, the dial is read once in
`TheatreFamilySkin.Enabled`, and every statement of `TheatreOneSkin` that reaches the class sits between `// FAMILYSKIN begin`
and `// FAMILYSKIN end` behind that read. On, a part whose shape is a `SuperellipsoidShape` and whose `Family` is set (with the
same reference-half-extent test as `TheatreFamilyMesh.Wants`) becomes a primitive of kind `KindFamily`, at the part's true half
extents and not the palette's squashed aspect. Its field, `TheatreFamilySkin.Solid.At`, is `(G - 1) x min(a f, b, c f)` in the
part's own frame, which is the frame shifted to the centre of mass, with `G = F^(e1/2)` for Core's inside function F, the
profile exponent e1 and the taper `f = 1 + k t`. F is homogeneous of degree 2/e1 in the untapered unit frame, so G grows in step
with length along a ray: the field is zero exactly on Core's surface and has the right sign everywhere. Its size is an
estimate, as the ellipsoid kind's bound is: under the distance on a long thin solid, and with a section exponent above 1 up to
2^(e2/2 - 1/2) times it across a section's diagonal (about 1.27 at e2 1.7), which moves only the smooth joins' widths. Beyond the ends (`|t|` over 1), where Core's function is
infinite, the same formula takes f at the nearest end and the true t, which is finite and positive there. The primitive's
cull box holds the whole solid with its taper and shift, and is grown by the margin times the solid's aspect (`Solid.Pad`) so
the field's low reading on a thin solid never drops it early. The skin's k/4 inset applies as for every kind, with no rounding
of the family kind's own, so the skin follows Core's surface and stands inside it (D252). At levels 5 and 6 `ColliderOf5` gives
the part collider kind 3, the same field, and `ColliderAt5` reads it, so the hinge clamp holds tissue inside the family solid;
`Face5` takes the box's rule for a joint's face on the solid's own reach (the widest slice across, the two poles along Y put
at different distances by the taper and the shift), so a joint's plane touches the solid. A family part takes no paddle (so
`EVOSIM_THEATRE_PADDLE_LIMB` and `EVOSIM_THEATRE_PADDLE_HINGE` skip it, and `PaddleColliderOutside` reads its solid in the
paddle log), no drawn-box taper or bend, and is never `leafShaped`. The log prints two lines at the first family primitive
built: what the kind is, and the largest `|field|`, in millimetres, over Core's `SurfacePointAndNormal` points on the cube
lattice (8 by 8 cells to a face), which is zero up to float rounding when the field and Core agree.

`EVOSIM_THEATRE_NO_BOX` (0 off by default, 1 on, 2 every skin's boxes and the nose's snout too) is the owner's rule of 2026-10-31: no creature is drawn on screen as a box. The
physics stays boxes. At 0 a frame is the one without it, to the pixel, with any other dial on or off: it adds no shader line and no
keyword, the dial is read once in `TheatreNoBox.Enabled`, and every statement of it sits behind that read (`// NOBOX begin` and
`// NOBOX end` in the palette, the meshes, the one skin, `SnapshotWorld` and `TheatreSoloStill`). On: a box part the palette dresses
and does not draw as a leaf takes `TheatreNoBox.Mesh()`, a superellipsoid `|x|^p + |y|^p + |z|^p = 1` (48 rings by 96 sectors, the
gradient for its normals) inside the rounded cube's own box and carrying its shape channel, so the shader shapes it as the box
(taper, bend, organic, soft tissue, all inward); `EVOSIM_THEATRE_NO_BOX_EXPONENT` (2 to 6, default 2.6; 2 is an ellipsoid) is `p`.
`TheatreMeshes.PrimitiveFor` maps it to the engine's cube for the raw shapes, and the nose pit and the mouth read it as they read
the rounded cube. `TheatreOneSkin.Build` builds for a body of one merged part, where it needs two without the rule, and the
reconstructions that never built a skin (`SnapshotWorld`, `TheatreSoloStill`) build it, from the part transforms and visuals they
already hold, behind the skin's own dial. The leaf, the sphere, the capsule and the family meshes are not touched, and the snout
is touched at level 2 alone. `EVOSIM_THEATRE_NO_BOX_EXPONENT` reaches only a box drawn as its own part: a box that is part of a
skin of two merged parts or more is still a drawn box in the skin's field at level 1, and there the exponent leaves the frame
unchanged to the pixel. Level 2 (`TheatreNoBox.EllipsoidAll`) makes every box in a skin of any size the ellipsoid of its half
extents inside it (`TheatreOneSkin.NoBoxSolid`, `KindEllipsoid`, p 2, not the exponent), a flat box that `DrawnAsLeaf` names
included: it is the lens of its half extents, not `KindBox`'s tile with flat faces. A photosynthetic box that `DrawnAsLeaf`
names is the leaf lamina and is exempt. The skin does not merge it by default (`RigMerges`), and where `EVOSIM_THEATRE_SKIN_WHOLE`
merges it, it stays `KindBox`. A box not dressed by the palette (`shaped` false: the raw shapes) stays the collider's rounded
box. At level 2 the nose's snout (`TheatreMeshes.Snout`) is the same map, the axial taper, the skirt and the flare, on
`TheatreNoBox.Mesh()` in place of the rounded cube: a tapered rounded solid with no flat tip face, cached by class as before
under `SnoutMeshCap` (the key carries no pillow bucket, so one class serves every cubicness). The pit is a vertex-stage cup
about the tip point (`_Pit`), and the solid has a vertex at the tip: where the tip axis is x or z, the grid's seam (theta 0, 90,
180 or 270 degrees at the equator) puts one on the axis, and where it is y, the pole does; the grid is 3.75 degrees a step.
`EVOSIM_THEATRE_NO_BOX_SKIN_EXPONENT` (2 to 6, default 2, read once) squares the solid inside a skin at levels 1 and 2: over 2, a
box that would be the ellipsoid in the skin is `KindSuperEllipsoid`, the superellipsoid `|x/a|^p + |y/b|^p + |z/c|^p = 1` of that
exponent on the same semi-axes (the field is `(f - 1) / |grad f|` with `f` the gauge `(sum |e|^p)^(1/p)`, which is the
ellipsoid's own bound at p 2). At exactly 2 the skin takes the ellipsoid path unchanged. A sphere's or capsule's ellipsoid is not
squared. The box counter sees only part renderers, so at 1 and 2 its `box parts 0` says nothing of the boxes inside a skin. The
skin's log line gives a
`half thickness` range: half the chord of a march along each vertex's inward normal through the sampled field, never under half a
step (0.125 cell). The level-3 skin carve and the MOUTH2 cut read it, and so do SKIN2's thin, rim and crease marks with the cap
at 0. On a flat body's rim the normal lies in the body's plane and the chord runs across the body's width (0.557 m on body 64217,
about 0.17 m thick), so the carve folds the rim through the faces: toothed ends and sheared facets, at every cell size.
`EVOSIM_THEATRE_SKIN_THICK_CAP` (0, 1 or 2, read once, at every skin level and with `NO_BOX` on or off) bounds it. The owner
ruled cap 2 for the film (D278), and the code default is 2, so a render with the dial unset is capped as a film is. 0 is no
bound, the look before the ruling, and a render that wants it (a cap-off comparison) sets 0. The cap changes every skinned body (it acts on about 2,000 of a window's skins at NO_BOX 0).
With the cap on, the carve and the mouth cut read the capped thickness, and SKIN2's marks read the thickness from before the cap
(D281). Read capped, SKIN2 would light a thinned tip as a translucent margin, which shows as cream patches at level 6. As it
is, a level-6 tip keeps its colour and turns a few levels brighter, from its thinner shape alone. At 1 the bound is the
smallest plain half extent of the vertex's dominant bone (`weights[v].boneIndex0`). At 2 it is the smaller of that and the thin-axis
chord: a march both ways from the vertex along the dominant bone's thin axis, in the rest frame the field is built in. A tapered
tip is far thinner than the part's plain half extent, so 1 still folds a tip. Where the thin axis grazes the surface its chord comes
out short and the carve fades there. The log line adds `(half thickness capped on N v, max before M m)` at 1 and `(half thickness
bounded on N v (thin axis A, extent E), max before M m)` at 2.
`EVOSIM_THEATRE_BOX_LOG` (0 off by default, 1 on) logs, once per placed capture, `[Theatre] box count: frame F (sec S) bodies in
view B, bodies with a box N, box parts P, rounded (no-box) parts R`, where a box part is an enabled part renderer in the camera's
frustum drawing the engine's cube or a rounded cube, and once per boxed part per run `[Theatre] box part: id I part K cell C shape S
half-extents (x, y, z) skin ...`, naming whether the one skin built, left the part out or was not built and the rule that said so
(`TheatreOneSkin` keeps that record only while the log is on). The log changes no pixel.
A solo still frames the body's renderers, so a no-box mesh, which is smaller than its box, pulls the camera in and a pair at 0 and 1
is not like for like. `EVOSIM_STILLS_FRAME_SAVE=<file>` writes each still's frame and bearing there, one line a still, and
`EVOSIM_STILLS_FRAME_LOAD=<file>` frames a still listed there from that line instead (`TheatreSoloStill.PinFrame`, logged as
`[Theatre] stills frame:`). Save on the 0 pass and load on the 1 pass. Lines are appended, so each pair takes a new file.

`Evosim.Theatre.EditorTools.TheatreShapeBench.Run` needs no world and no genome: it draws seven forms of the family at half
extents (0.1, 0.2, 0.1) m as the mesh builds them, Core's surface points over each as 3 mm red spheres, to the folder in
`EVOSIM_THEATRE_BENCH_OUT` (it refuses without it) as `family-mesh-side.png` and `family-mesh-threequarter.png`, 1600 by 600,
and logs the largest distance from a point to the mesh in millimetres, one line a form; launch it as `NosePitStill` is.

`EVOSIM_THEATRE_BODY_DAPPLE` (0, off; steps 1, 2 and 3) lays moving patches of the water's light over bodies in the top
metres, on the one skin and on parts alike, in place of the body caustic's few hairlines on upward faces. At 0 a frame is the
one without it, to the pixel, with any other dial on or off: its shader lines are all inside `#if defined(_EVO_BODY_DAPPLE)`,
under a `multi_compile` keyword in each of the three passes that `TheatreSkin.EnsureBodyDappleKeyword` turns on when the look is
pushed, only while the dial is on. The light is the wave field's own focusing (`EvoCausticGain`, which is one at the flat
water) through a broader net than the caustic's, `EvoDappleNet`: `saturate((gain - 0.6) * 0.625)`, so a flat surface gets a soft
base of 0.25 and the cusps reach 1, patches with bright edges and not hairlines. It keeps the caustic's fade to `_CausticReach`
and its clock, `EvoTime()`, and takes the side wrap `saturate(0.4 + 0.6 * n.y)` for the upward face's `saturate(n.y)`, so an upward
face takes all of it, a side face 0.4 and an underside little. The step sets the strength, in the global `_EvoDapple` that
`TheatreSkin.PushBodyDapple` writes: 0.7, 1.0 and 1.4 times the caustic colour at the net's brightest; `_CausticStrength` itself is
not read. It acts only where the caustic would (the board light's fade of zero, and a neck's strength of zero, both leave it out),
and a held look (a census render) sets the global to zero, which draws the old caustic. Its constants (`DappleThreshold`,
`DappleScale`, `DappleSideFloor`, `DappleSideSlope`) live in `TheatreBody.shader` after `_EvoAlgaBend`, and the strengths in
`TheatreSkin.BodyDappleStrengths`.

`EVOSIM_THEATRE_NOSE_PIT=2` (with `EVOSIM_THEATRE_ONE_SKIN=4`) joins the nose to the one skin at its true size and carves its pit in
the skin. At 0 and 1 a frame is the one without it, to the pixel: every shader line it adds is inside
`#if defined(_EVO_NOSE_PIT) && defined(_EVO_NOSE_SKIN)`, and `_EVO_NOSE_SKIN` (a `multi_compile` keyword in each of the three passes) is
turned on by `TheatreNosePit.EnsureSkinKeyword` only when a skin is built with a nose in it, so level 1 compiles the text it compiles
without this level. The dial is read once, as `TheatreNosePit.Level`, and `TheatreNosePit.OnSkin` is level 2 on the rig. (1) The rig's
merge list takes the nose (`TheatreOneSkin.RigMerges`): it joins by its collider as any merged part, with its own bone and the rig's
weights, unless `EVOSIM_THEATRE_SKIN_BODY` is set (that list rules) or it is under 1.5 cells of the grid, in which case it stands apart
and takes level 1's pit. A body without a nose is built as it was. (2) The pit is the nose pit's, at `MouthPoint.Local` with
`TheatreNosePit.Measure`'s radius and depth, cut in the skin's vertex stage as the mouth's opening is. The centre in the body's rest
metres and the radius there reach the shader as `_PitSkin` and the depth as `_PitSkinDepth`, on the skin's block at every dress, and each
vertex carries its skinning weight toward the nose's bone in UV1, so the cup moves with the nose and a neighbouring part's surface takes
none of it. The mouth's skinning-weight helper and its method are reused; its slots are not, because its four openings share one depth,
wear the mouth's throat and lip, and sit behind their own keyword, so the nose has one slot of its own and a second nose on a body gets no
pit. The cup is the pit's (`PitCup`, `PitClean`), inward only and no deeper than half the vertex's half thickness. The fragment tilts the
normal as the pit's wall and lip and darkens the albedo and the light by the cup's depth, from the vertex's share, which reaches it as
`pitSkin` in the forward varyings (TEXCOORD9). (3) With `EVOSIM_THEATRE_TISSUE_LOOK` on, `TheatreTissue` holds a `nose` entry in its
two tissue tables only while level 2 is on: sRGB (0.96, 0.88, 0.84), paler than the other tissues (lightness 0.34 in the first set, 0.72
at a saturation cap of 0.55 in the bold set), 0.1 more transmission than structure, and a mottle four times as fine. Under SKIN2 the
skin's colour blends by bone weight, so that pale tissue reaches the skin through the same per-vertex blend. On the skin the nose also
carries lamellae, fine rings of the distance from the pit's centre in the rest frame at a quarter of the body pattern's cell
(`NoseLamella*` in `TheatreBody.shader`), a darkening that starts outside the lip and is gone as a ridge falls under a pixel, and
`NoseTransGain` (0.1) more light through the tissue, both weighted by the nose's share of the vertex. Nothing is drawn outside the nose's
collider except the rig's joint region. The log prints a `one skin nose` line for a body that has a nose: whether it joined the skin, the
pit's radius and depth, and which parts stand apart by type and smallest half extent.

`EVOSIM_THEATRE_MOUTH_MARK` (0, off; 1 on) puts a small unlit yellow sphere on the mouth of every eating part
(`TheatreMouthMarks.cs`), parented to the part's object so it follows the pose and a corpse's shrink. Its radius is a quarter
of the part's smallest half extent and never under 12 mm. It asks Core for both the part and the point
(`MouthPoint.IsEatingPart`, `MouthPoint.Local`, of the phenotype the body is drawn at), so the mark sits where the solver and
the world's reach test measure from, and a mark that looks wrong is a finding about the mouth or the pose and not about the
theatre. The live world (as it builds a body and on a growth step), the film window and the reconstruction build their own
parts and call it as they build; the PhysX replay and the solo creature are built by `Evosim.Sim`, so the runner adds the marks
to them as it repaints. The palette leaves marks alone (`TheatreMouthMarks.Strip`). The one skin switches off a merged part's
own renderers and leaves the part's object, so on a skinned body the mark is still drawn, as a sphere at the point in the
part's frame and not on the skin. With the dial at 0 nothing is made and no existing path changes.
`EVOSIM_THEATRE_MOUTH2` replaces the sphere: with it on no mark is made.

`EVOSIM_THEATRE_MOUTH2` (0, off; 1 on) draws an opening with a lip on the mouth of every eating part (`MouthPoint.IsEatingPart`),
at `MouthPoint.Local`, on a part and on the one skin (`TheatreMouth2.cs`, and `_Mouth*` in `TheatreBody.shader`). It replaces
`EVOSIM_THEATRE_MOUTH_MARK`: with it on the sphere is not made. At 0 a frame is the one without it, to the pixel, with any other dial
on or off: its shader lines are all inside `#if defined(_EVO_MOUTH2)`, under a `multi_compile` keyword in each of the three passes
that `TheatreMouth2` turns on the first time it paints an eating part or builds a skin with one, and the palette's one hook is a test
of the dial. It is the nose pit's method. The radius is 0.5 of the eating face's smaller half extent (`RadiusShare`; a sphere takes its
smallest) and the depth 0.4 of the radius (`DepthShare`), at most half the part's half length along the mouth's axis. A vertex inside
the radius moves inward along its normal by the nose pit's cup (a flat floor out to 0.35 radius, a smoothstep wall to the rim), the
carve is calmed round it, and nothing moves outward, so the outline stays inside the collider. The fragment darkens the albedo toward
a deep warm red-brown throat (`MouthThroat`, full inside 0.55 radius, gone by 0.95) and the light by 0.45, and tilts the normal as the
cup's wall and as a lip: a ring from 1.0 to 1.25 radii whose height is 0.25 of the depth, raised by its shading alone (the normal
leans inward across its inner half and outward across the outer, and a soft highlight of 0.18 sits on the outer half). The lip is
skin2's margin colour for the body's clade (`Skin2Margin` at full cover, from `_PatternShift.zw`, so SKIN2 on and a clade with a
band), else the body's colour lifted 15 % in value. On a part the point reaches the shader as `_Mouth` (in the visual's object units,
the depth in radii in w) and `_MouthRadii`, set per renderer by `TheatreMouth2.Paint` after the palette's own `Set`. On the one skin
it reaches it as `_MouthSkin0` to `_MouthSkin3`, each a mouth's centre in the body's rest metres (the eating part's centre plus its
rotation times Core's point, from the adult's rest pose) with its radius in w, written on the skin's block at every dress, and each
vertex carries in UV2 its skinning weight toward the part of each mouth. The cut is that weight times the cup, so the opening moves
with its part and a neighbouring part's surface takes none, and on the skin it goes no deeper than half the vertex's half thickness.
The skin's share in the mouth's parts is carried to the fragment as minus the varyings' `leaf.w`, which a skin leaves free and every
reader tests against one half. A skin carries four mouths: the first four eating merged parts in part order get an opening, and any
more get none; a part too thin for the skin is drawn as its own part and has its own. A blade, a raw shape (`X`) and a mesh the
theatre did not make get none. The skin's mesh is as coarse as its grid, so the cup is a few vertices across and the fragment carries
the rest.

`EVOSIM_THEATRE_MOUTH2=2` (the dial reads 0 to 2; 1 is the paragraph above, unchanged) puts the opening on the surface that is drawn. At
level 1 a skin's mouth is Core's point on the collider's face, and the skin is drawn inside the collider (the taper and the grid's inset
leave it short of the face), so the cup can find no vertex within its radius. At level 2 `TheatreMouth2.PlaceOnSkin` takes the nearest skin
vertex whose heaviest bone is the eating part's and moves the point inward along the part's mouth axis to that vertex's axial coordinate, at
most `MoveHalfLengthShare` (0.5) of the part's half length and never outward, so the point stays on the line from the part's centre to its
face. The radius is the old rule's, or `RadiusShare` of the smaller half width of the drawn section where that is smaller: the half widths
across the axis of that bone's vertices within `SectionDepthShare` (1.0) radii of the moved point along it, read from at least
`SectionMinVertices` (6) of them. Nothing is drawn outside the collider (D242): the move is inward, the radius never grows and the shader's
cut is level 1's. A part drawn alone is not moved. The merged index of a mouthed part is its bone index (the weights, the bind poses and the
bones are all indexed by the merged order), so the bone needs no fix. With the dial on at either level the log has one
`[Theatre] mouth2 skin:` line per skin (the body, the merged count, and per mouth the merged index and the bone it skins to, the vertices
that bone dominates, the point and radius in rest metres, and the distance from the point to the nearest of those vertices, in metres and
in radii; at level 2 also the move, the distance after it and the new radius) and one `[Theatre] mouth2 part:` line per eating part drawn
alone (the body, the part, the point in the part's frame, the radius). Logging changes no pixel.

`EVOSIM_THEATRE_MOUTH2_DISC` (0 or 1, default 0) draws the mouth as an oral disc after the sea anemone's and the lamprey's, read once in `TheatreMouth2` and with any effect only while `MOUTH2` is on; the shader lines are all inside the existing `#if defined(_EVO_MOUTH2)` blocks and every one is behind `_MouthDisc.a` over zero, which only the dial sets, so at 0 every pixel is today's with any other dial on or off. The opening and its throat stay. The lip ring (1.0 to 1.25 radii, its shading unchanged) takes the mark colour, the body's `_FamilyColor` rgb where its alpha is over zero, else today's lip colour. Round it, a disc from 1.25 to 1.9 radii is tinted toward a disc colour by `DiscShare` (0.9), the share fading to zero over its outer quarter, in N radial stripes that alternate the disc colour with the body's, sharp-edged with a pixel-wide fade and blurred to the mean where a stripe is finer than a pixel. The disc colour is the body's colour at another OKLab lightness, lighter by 0.25 on a body under L 0.5 and darker by 0.25 above (`TheatreSkinMap.MouthDiscColour`), made in C# from the block's own `_BaseColor`, so a part's zone hue and a grey or a corpse carry, and passed as `_MouthDisc` (rgb the colour in the shader's linear space, a the share). On a one skin it is the largest part's colour, read in `DressSkin`. N is 6, 8, 10 or 12 from a hash of the body's skin-map entry (`TheatreSkinMap.MouthStripes`, so kin share it), 8 without an entry, and travels in `_MouthRadii.w`; a part that does not eat carries it too, for a skin whose largest part is one. The stripes turn about the mouth's axis in a basis built from the axis and the part's up (the rest frame's up on the skin; x where the axis is within 0.9 of up). On a part the axis is `_MouthAxis`; on the skin `PlaceOnSkin` notes each mouth's axis and the angle between the rest frame's reference and the part's own (`AxisPhase`) in a two-generation table keyed by the mouth as the skin carries it, because `TheatreOneSkin` keeps only the centre and radius, and `DressSkin` writes them as `_MouthAxisSkin0` to `_MouthAxisSkin3`, so the stripes stand where they stand on the part drawn alone. A mouth missing from the table draws the disc without stripes. Colour only: no vertex moves for it, so nothing is drawn outside the collider (D242).

`EVOSIM_THEATRE_MOUTH2_KINDS` (0 or 1, default 0) gives the oral disc a kind by the shape of the body's mouth parts, read once in `TheatreMouth2` and with any effect only while `MOUTH2_DISC` is on. The mouth's own numbers cannot carry it, since attack and intake sit at their ceiling in every eater, so `MouthKind` takes the main mouth part (the largest eating part by the product of its half extents), calls it long when its largest half extent over the next leads the next over the smallest and is at least 1.4 (`KindRatio`), flat when the second ratio leads and is at least 1.4, and round otherwise, whatever its shape, since the theatre draws a stretched sphere as an ellipsoid; the count is the number of eating parts. At 1.8 with a sphere always round, every eater of the v7e5k stills came out round. Three or more eating parts give kind 2, the sucker. Otherwise a round part gives kind 1, the toothed ring; a long box whose middle over smallest half extent is also at least 1.4 gives kind 4, the beak; any other long part gives kind 3, the crown. About 80 % of mouth parts are flat, since the body plan is flattened (`scratch/skin-map/full-colour/v7d/mouth_shapes.py`), so a flat part's kind follows the heritable skin-map stripe count: 6 the stripes of today (kind 0), 8 the toothed ring, 10 the gape (kind 5), 12 the sucker. The stripe count is `6 + 2 * floor(4 * SheenRank)` (`TheatreSkinMap.MouthStripes`), a population quantile kin share; it was `frac(11 * baseHue)`, which gave r62-h6202's family of six four different mouths. `EVOSIM_STILLS_FROM=mouth` turns each solo still to face its main mouth (the largest eating part), 0.3 of the way from its outward axis toward the usual bearing and framed on that part, and every still logs `TheatreSoloStill: id N mouth part P code W kind K stripes S` from the painted `_MouthRadii.w`, which is what a sheet labels a kind by. `EVOSIM_STILLS_FROM=mouthclose` faces it the same way but frames a box 3.2 mouth radii each way from the mouth point. That is not close enough for the suckers, and the camera is not their fault: with the stills facing the mouth, each logs a `mouth geometry` line, and on v7e9 Core's point and the drawn opening coincide and the eye sits 12 degrees off the outward axis, the 0.3 turn. A box part's mouth is the centre of the face its joint direction points through, which on a part flat in one axis is often the rim, and the opening's radius is half that face's smaller half extent, the thickness. On a lens-shaped visual, sharp at the rim, the round opening wraps over the two slopes, and seen head-on it reads as an edge-on body with a squashed mouth; a part rounded at that face, as the disc 53866, reads. `EVOSIM_THEATRE_MOUTH2_FLAT` (0 to 1, default 0, any effect only with `MOUTH2_DISC` on, since it needs the disc's axis) is the share of the way from the mouth's distance in space to its distance taken flat along its axis (`_MouthFlat`, `MouthFlatten` in the shader), so the opening, the lip, the disc and the kinds are round seen along the axis; a point more than 2.5 radii behind the mouth's plane has the rest added back twice over, so the far side of the part takes no mouth (`EVOSIM_THEATRE_MOUTH2_FLAT_SLACK`, 0 to 10, 0 the 2.5, sets that slack). The dial is unproven, not shown to fail. At 1 it changed 1,500 to 2,600 pixels round each mouth, so the share reached the shader, but the suckers' openings did not change to the eye at slack 2.5 (v7e10) or 5 (v7e11), and 5 gave 2.5's pixels within the renders' run-to-run noise of one level. Identical pixels across a dial change mean the value may never have reached the shader, and nothing logs the slack, so it is not known whether the slack never binds on these parts (and the squash is something other than the surface running away along the axis) or never arrived. A render painting the mouth's u in false colour, the dial off against on and at both slacks, settles it. Nothing streaked over the top or bottom from the usual bearing. The dial stays off by default and the sucker row of the mouth sheet stays marked open. It reaches every place the distance is measured: the part's cup and colour (`MouthPartU`), and on the one skin the cut, the nearest mouth and its axis (`MouthSkinU`). At 0 nothing is written and each distance is today's expression behind a branch, so the dials-off check cannot see it: it is checked by a still pair whose off look matches the same stills from the head before it to the pixel. The kind rides in `_MouthRadii.w` as the stripe count plus 16 times the kind, on every part of the body (a part that does not eat carries it too), so the one skin gets it from its largest part's block as it gets the stripe count, and the shader reads a value under 16 as kind 0. Each kind is colour only, in the disc's polar frame. The toothed ring is twice the stripe count of pale triangular teeth on the lip's inner edge, pointing in to 0.6 radii, with the throat's darkening eased under them since it is applied after the disc's colour. The sucker is three concentric rows of small round pale dots between 1.25 and 1.9 radii, each row half a step round from the one before. The crown is the stripe count of lobes out to 2.6 radii, narrowing to rounded tips, with the body's colour between them. The beak is two dark plates over the opening out to the lip, meeting at a pale seam along the disc's first tangent axis, and it takes the throat's darkening off under the plates. The gape is a dark slit 1.8 radii long and 0.4 wide with a lip of the disc colour round it. At 0 the code is the stripes' own and every pixel is today's.

`EVOSIM_THEATRE_MOUTH2_BITE` (0 to 1, default 0, any effect only with `MOUTH2` on) is the strength of the bite pulse, step 1 of `logbook/specs/mouth-bite-animation-design.md`: on an onset a mouth's lip and disc lift toward white by 0.6 x strength x e(t) and its distance u is divided by 1 + 0.25 x strength x e(t), over 0.35 film seconds (colour and normals only, no vertex moves). The timers (film seconds since the onset, 1e4 idle) are `_MouthBite` (a part's in x; the one skin's four slots in x to w) and the strength, flash share and test period are `_MouthBiteParams`, written per frame by `TheatreMouthBite`, from the palette's paint, the skin's dress and `TheatreSkin.PushFrame` (and the runner). At 0 nothing is read or written and the shader's branch is not entered, so the dials-off check holds. `EVOSIM_THEATRE_MOUTH2_BITE_TEST` (seconds, default 0) is a fake onset every N film seconds (the render clock) on every mouth, `EVOSIM_THEATRE_MOUTH2_BITE_AT` (seconds, unset by default) pins every timer to one value for stills and no onset is read then. In a film window whose pose stream is version 5 or later the farm's ate bit is the source (`TheatreMouthBite.ReadAteBits`): bit k of a body's `AteMask` is its k-th eating part (`MouthPoint.IsEatingPart`, in part order), the genome developed at the plan in force at the frame gives the part index, an onset is a frame whose bit is set after a frame where it was clear, and while it stays set another comes each 0.35 film seconds, so sustained eating is a rhythm (on the one skin, an onset goes to the slot that holds that part's mouth). The kill rows are then not read for onsets. On an older stream, or a window with no pose frames, the kill rows are the fallback: each `{"e":"k","id":VICTIM,"by":KILLER,"byPart":P}` is an onset at its `t` on the killer's part P. In both an onset within 0.35 film seconds of the one before it is dropped, the onsets are read once per window at the speed the film has set by its first frame, and one `[Theatre] mouth 2 bite:` line gives the source, the stream version, the onsets scheduled, the mouths and the refractory drops. The pin and the test period take precedence over both. `EVOSIM_THEATRE_KILL_CONTACT_LOG` (0 or 1, default 0, independent of the bite dial) logs, for each kill row of a played film window, `[Theatre] kill contact: ...` with the signed distance from the killer part's mouth point to the nearest victim part's surface (the victim part is the nearest, since the row names none), the mouth radius and their ratio, at the pose frame nearest the row, or `absent` with the reason. With `EVOSIM_THEATRE_FILM_SUBJECT` set as well, the dial writes one `[Theatre] ate contact: ...` line for each ate-bit onset of the subject, at the onset's nearest frame. It names the other drawn body's part nearest the eating part by the true-shape gap (cores plus margins, `Evosim.Dynamics.ConvexCore`, as the host's `ContactTrueShape.Pair` measures it), says `leaf` or `animal`, and gives the kill line's mouth-to-surface and ball numbers. It ends `touch` where the margins exceed the cores' distance, else `far`. An onset that a kill row of the window matches (the same killer and part, within one pose frame's interval) is measured differently, because a killing bite swallows its victim in the pass that sets the bit, so at the onset frame the victim is gone and the nearest body is some other one. A kill (`root` 1) is measured at the frame before the onset's nearest, against the victim's parts alone, and the line (`... (kill) victim at onset − 1 ...`, written at the onset frame) names the victim part nearest and whether it is the row's. A limb cut (`root` 0) is measured at that frame and at the onset frame, and is a touch where either is. The line gives how far the eating part's centre moved to the onset frame, and ends `touch`, `touch within a frame's motion` (the surface gap is no more than that move) or `far`. A wound, which has no kill row, is measured at the onset frame as before. The candidates are the live bodies, the corpses and, with `EVOSIM_THEATRE_REMAINS=1`, the remains, whose kinds read `corpse` and `remains`. `EVOSIM_THEATRE_ATE_CONTACT_ALL=1` measures every ate-bit onset of every body in the window, whatever the subject, and ends the window with an `ate contact window:` line counting touch and far by kind and by match (kill, cut, wound), with how many touches were within a frame's motion. A corpse's shrink and a newborn's growth are not applied to the core, so their gaps read a little low. With the bite dial on and `EVOSIM_THEATRE_FILM_SUBJECT` set, a farm-window film also places the subject's mouths (`TheatreMouthBite.LogPlaced`, called by `TheatreFilm.DriveWindow` after the close shot's capture, else the first shot's): one `[Theatre] bite frame F t=S body B mouth K px X Y r R pulse P facing D on-screen Y|N` line per eating part of each subject body per film frame. K is the part index. X Y is `TheatreMouth2.Measure`'s mouth point, in pixels from the top left. It is the point the shader's opening and the kill-contact log use, projected through a pinhole of the field of view and aspect that `SnapshotCamera.CapturePlaced` sets, with no lens and no blur. R is the mouth's radius at that depth, in pixels. P is the strength times the shader's pulse at the timer `TimerOf` gave that part in the frame; the pulse is a C# copy of `MouthBitePulse`, kept beside the shader's text. D is the dot product of the mouth's outward normal and the unit direction from the mouth to the camera: 1 faces the camera, a negative value faces away. `TheatreMouth2` holds no disc normal, so the normal is the direction of the mouth point from the part's centre, through the part's drawn transform. `MouthPoint.Local` lies on the axis of the face the mouth is cut in, so that direction is the face's normal. On-screen means the whole disc lies in the frame and its centre is beyond the 0.05 m near plane. A centre behind that plane reads NaN for X, Y and R. With either dial off nothing here runs. These dials are built and not yet compiled or checked to the pixel.

**A mouth's bite has no flash** (the owner, 2026-11-02, D281). `EVOSIM_THEATRE_MOUTH2_BITE_FLASH` defaults to 0. With the
bite pulse on (`EVOSIM_THEATRE_MOUTH2_BITE=1`) the onsets still drive the gape and the bite's crumbs, and no mouth lifts toward
white. 0.33 and 1 are still read, for a comparison; 1 is the flash as it was before the ruling. A film rendered before the
change and rendered again with the dial unset loses every flash, so an identity check against an older film sets the dial
to 1.

`EVOSIM_THEATRE_MOUTH2_RELIEF` (0 to 1, default 0) is the strength of a relief on four of the kinds. It is read once in `TheatreMouth2`, with any effect only while `MOUTH2_KINDS` or `MOUTH2_DISC` is on: with the kinds off every mouth is the oral disc, and the relief shapes it all the same. Under it the oral disc (kind 0), the toothed ring (1), the sucker (2) and the gape (5) read as openings with a lip and an inner wall rather than painted ovals. It is shading and colour only, as the lip is, so no vertex moves for it (D242), and every number is reached at 1 and scaled by the dial. On kinds 0 and 1 the round lip stands three times as high (`MouthReliefLipGain` 2) and the cup's wall tilts twice as far. The wall, from 0.3 radii to the lip, takes a lining colour, the body's darkened to half and mixed 0.55 toward a flesh red, and the throat's dark is held to the floor inside 0.35 to 0.6 radii. The light on the wall is occluded by `MouthLightDark` times the cup's depth there, so the far wall catches the light and the near one falls into shadow. Kind 0's stripes become radial furrows 0.08 radii high, the disc-coloured stripe standing up. Kind 1's teeth narrow to half their half pitch at the base and stand as ridges 0.08 radii high along their centre lines, so one flank takes the light and the other falls away. They are enamel, graded from 0.7 at the gum to full at the tip, with the gaps between them falling 0.8 of the way to the throat. Each of kind 2's dots is a small cup, a rim 0.03 radii high round a dimple darkened to 0.35 of the dot's colour, with the rim lit to 1.35. The sucker takes only its cups from the dial and keeps its dark throat and lined floor: kinds 0 and 1's lip gain, lining and floor-held throat would lighten it into a pale dish. On the gape the round lip, its tilt and the round throat give way to the slit's, and the slit opens from 0.4 radii across to 0.9: without the round throat a slit 0.4 across leaves a floor of a line, and the mouth reads shut. The slit is a trench: its wall falls 0.3 radii from the edge to 0.55 of the ellipse's radius and takes the lining, the floor inside that is the throat, and its lip is a ring raised 0.12 radii. The kind reaches out to the end of that lip (2.3 radii). All the tilts turn to the world through the disc's own basis, are bounded by `MouthSlopeBound` and fade on a far body as the lip's does. At 0 nothing is written and every line is behind a branch or a multiply by exactly one, so the dials-off check covers it. With `FLAT`, `RELIEF`, `FACE`, `SMALL` or `PLAIN` on, the log has one `mouth2 dials` line with their values, and the stills facing the mouth add a `mouth disc` line per renderer with the axis and the three values its block holds.

`EVOSIM_THEATRE_MOUTH2_SMALL` (0 to 1, default 0), read once in `TheatreMouth2` with any effect only while `MOUTH2_DISC` is on, fades the disc and every kind's marks by the mouth's size on screen. The disc mix is multiplied by `smoothstep(MouthSmallGone, MouthSmallFull, radius in pixels)`, 10 and 28 px, mixed in by the dial, and the disc's stripe edge is held at least at `MouthSmallSoftEdge` (0.4) times the dial at every size. The relief's furrows and ridges (`MOUTH2_RELIEF`) fade by the same factor, since their shading alone drew a cog's teeth on a world shot's mouth (phase143). The reason is a world shot: there a mouth is 15 to 25 px across, and kind 0's hard pale stripes read as a cog's teeth. The opening and the lip are left as they are. At 0 nothing is written and both lines are behind a branch.

`EVOSIM_THEATRE_MOUTH2_PLAIN` (0 to 1, default 0), read once in `TheatreMouth2` with any effect only while `MOUTH2_DISC` is on, draws the mouth on the body's plain colour. The fragment keeps the body's colour from just before the chromatophores and the pattern (`mouthPlainBody`). Out to the lip's outer edge (1.25 radii, soft over `MouthLipEdge`) the body is mixed back to it by the dial, so no mark of the skin runs across the lip, the wall or the throat. Without it a maze skin's black strokes cross the lining, and the throat, which takes the darker of its own colour and the body's, keeps a stroke whole as a glyph in the mouth's centre. The lip then takes the body's own hue, its plain colour lifted (`MouthLifted`), instead of the mark colour the disc gives it, at `MouthPlainLipStep` (0.5) of its step from the body. The reason is a world shot: there a bright saturated lip round the dark throat reads as an eye's iris and pupil. The relief's tilt still shades the lip. The disc's stripes fade out by the dial, so at 1 none is drawn: in the disc colour they drew dark wedges round a pale body's mouth in a portrait (phase143), and in the body's plain colour, which on a maze skin is the maze's dark ground, dark smudges beside the mouth (phase145). Under a dial below 1 what is left of them takes the body's plain colour lifted. The relief's furrows still shade the disc. At 0 nothing is written and every line is behind a branch.

`EVOSIM_THEATRE_MOUTH2_FACE` (0 or 1, default 0), read once in `TheatreMouth2` and with any effect only while `MOUTH2_KINDS` is on, faces a sucker on the body's rim along its own axis, where its rows read as rings. A sucker here is a body painted kind 2 (`TheatreMouth2.PaintedKind`, read from `_MouthRadii.w`). A mouth is on the rim (`FacesOnRim`) when its part is flat, its middle half extent over its smallest at least `KindRatio`, and the mouth's axis lies mostly across the thin axis (under 0.5 along it). In the stills facing the mouth (`EVOSIM_STILLS_FROM=mouth` or `mouthclose`) the 0.3 turn toward the usual bearing becomes 0, and in a portrait from the mouth (`EVOSIM_THEATRE_PORTRAIT_FROM=mouth`) `PORTRAIT_FROM_ANGLE` becomes `FaceAngle` (0 degrees). Each logs a `face-on` line. It changes the camera only. With `MOUTH2_FLAT` off a rim sucker seen along its axis still shows the opening squashed across the rim's two slopes, so the two dials are meant to be judged together. At 0 every still and every portrait is the one without it.

`EVOSIM_THEATRE_PORTRAIT_FROM` (`mouth` or `nose`; unset by default, and anything else is ignored with a warning) places a safari
portrait's eye by a point of its body (`SafariPlans.PortraitFromPoint`, `FilmPlans.PortraitFrom`), so a mouth or a nose that faces away
from the usual orbit is seen. The point is the first eating part's `MouthPoint.Local` (`mouth`) or the first nose part's sensing tip
(`nose`, `MouthPoint.Local` of the part as `FilmNurseryShots`' nose shot takes it), taken at every frame through the part's transform
(`LiveWorldView.TryBody`). The eye stands on the point's outward axis, from the part's centre through the point, turned off it by
`EVOSIM_THEATRE_PORTRAIT_FROM_ANGLE` degrees (5 to 80, default 30, so an opening reads as a cup and not a ring), and looks at the point,
the focus on it. The distance is the usual portrait's (`EVOSIM_THEATRE_PORTRAIT_FILL`, 1.5 to 3 body lengths from the point), pushed out
to the edge of the body's reach plus the plan's margin when it would stand inside. The point and the eye's direction are smoothed over
1.5 s. The side of the axis is chosen once, in the part's own frame, from twelve: a clear eye first, then the fewest bodies across the
lens, then the highest. A body with no such point is filmed as the orbit and the log has one `portrait from` line saying so; with the
point, the log has one line with the part, the point, and the eye's distance and angle.
`blade`, beside `EVOSIM_THEATRE_PADDLE_LIMB` 1, puts the eye on the face normal of the body's longest paddle (`TheatreOneSkin.TryBlade`, the thin axis the skin
was marched from, the sign that points more upward chosen once) and looks at the middle of the blade's rounded free end, so the blade is seen broadside and
not as a rod, with the same angle, distance and smoothing as `mouth`; the log has one `portrait from blade angles` line per take, the angle between the eye ray
and that normal at the start, its median and its largest, as planned and as the film kept it.

`EVOSIM_THEATRE_FILM_RAW=1` (`-Raw`) draws a film's bodies as the colliders the physics has, for a nursery film too: it sets the
palette's `RawShapes`, clears it, and turns the skin's carve, taper and bend off (`TheatreSkin.ShowRawShapes`), as the runner's `X` key
and `EVOSIM_THEATRE_SAFARI_RAW` do, and logs one `film: RAW` line. A part that a one skin has merged is drawn by the skin and not as its
own collider, whether or not the raw shapes are on, and a skin built before the switch keeps the outline it was built with.

`EVOSIM_THEATRE_UPTAKE` (0, off; 1 fullness; 2 fullness and inflow motes) shows how much each body's absorptive tissue is
taking in (`TheatreUptake.cs`, `TheatreUptakeMotes.cs`, `TheatreUptake.hlsl`, and `_Uptake` in `TheatreBody.shader`). The source
is the run's own `absorptive.jsonl` (each body's `foodW`, in watts for the whole body, averaged over the rows within 15 s of the
second on screen, against the 95th percentile of `foodW` over the rows read; not per cubic metre of tissue, which would show the
local nutrient density and not the intake), so it is a sampled reading and not a per-frame one, and a body with no row is drawn
as always. Every absorptive part shows its body's u. Fullness swells the parts along their normals by up to 6% of the smallest
half-extent and deepens their colour within its hue (HSL lightness down by up to 0.22, saturation up by up to 0.15), both times
u. It is the one term besides a leaf's curl that can leave a collider. The motes are 4 to 6 mm warm specks lit like tissue, one
for each `MilliJoulesPerMote` (1.5) of food income, drifting in over 1.5 s from a shell 1.5 to 2.5 half-extents out; they are a
pure function of the second, so a still and a film draw the same ones, and they are for portraits at the run's own pace. Census
views keep the old look. The run is found from the frame's `Record.Path`; a view with no run directory (the solo creature)
draws nothing and logs why once, and a live world that is a cousin of the recording reads the recorded uptake by creature id,
which may name another body than the one drawn. Every hook is one `if (TheatreUptake.Enabled)`, and the shader code is compiled
only into the `_EVO_UPTAKE` variant (a `multi_compile` keyword in each of the three passes that `TheatreUptake` enables once,
when the dial is on, and nothing ever disables), so with the dial at 0 nothing is made and the shader is the one it was. The
look is written to the parts' own renderers and is not made for the one skin: the skin switches those off, and it takes the
largest merged part's property block for its own, `_Uptake` with it.

`EVOSIM_THEATRE_CAUSTIC_VARIETY` (0, off) builds the caustic net the bed, the rocks, the bodies, the shafts and the snow are lit by
from a richer wave field, so it reads as a real sea's net of uneven loops, sharp in one place and soft in the next, drifting and
breathing, and not as one lattice of one size. At 0 a frame is the one without it, to the pixel, with any other dial on or off: its
shader lines are all inside `#if defined(_EVO_CAUSTIC_VARIETY)`, under `#pragma multi_compile _ _EVO_CAUSTIC_VARIETY` in the
forward pass of each of `TheatreBed`, `TheatreRock`, `TheatreBody`, `TheatreShaftMarch` and `TheatreSnowShells` (the depth passes
never read the caustic), and `TheatreSkin.EnsureCausticVarietyKeyword` enables the keyword once, from the look's push, only when the
dial is on. It changes `EvoCausticGain` alone, through `EvoCausticGainVaried` in `TheatreWater.hlsl`: `EvoRipple`, the drawn
ceiling and `EvoBeamAt` are untouched, so the ceiling's waves and the light on the bed no longer share one pattern. Six trains from
0.27 to 2.5 of the wave length dial replace three, each with its own heading and speed and a share of the curvature chosen so no
scale dominates; the sampling point is warped by a smooth noise about 6 m across, by up to a quarter of the wave length; each
train rides a group envelope about 8.5 m across that swells and fades over about 7 s; a drifting noise nudges each train's phase,
so there is no single beat; and with depth the fold clamp rises from 0.14 to 0.4 over the light's reach while the finer trains
blur away first, so deep caustics are softer and dimmer. The noise is a sine-free hash, defined in the file, and no texture is
read. The constants are the `EVO_CV_*` block beside `EvoCausticGainVaried`. A shader that samples the gain many times a pixel
(`TheatreShaftMarch`, `TheatreSnowShells`) defines `EVO_CAUSTIC_LITE` before the include under the keyword, which keeps today's
three trains and takes only the depth-raised fold clamp. There is no chromatic split: it would cost two more evaluations of the
whole field and the gain is a scalar for every caller.

**Start a story's film windows (`scripts/story-windows.ps1`) directly, never from a PowerShell `Start-Job`.**
Run from inside a job, a window's `Evosim.Farm.exe` sat at 0.3 s of processor time with an empty log, and it
held a card group's launch, whose busy check counts every farm process; run directly, the same window
finished in about a minute. That the job's input pipe is the cause is an inference. A window is a farm
process, so none starts while a card launch is pending.
`EVOSIM_THEATRE_LABEL_ROWS` holds a still's fit at that many caption lines, so a caveat line on one
world's picture does not shrink its box against another's in a series.

**A farm-window film starts at the second it was asked for** (`./scripts/theatre-film.ps1 <arm> -FromFarm <window> -At S`, which sets `EVOSIM_THEATRE_SEEK`). The runner seeks the window there when it opens, and then plays it in real time through the scene's load (`TheatreRunner.StepWindow`, until `Paused`). A film that took its clock from wherever the window had reached would start a few seconds late, by a different amount each run. Two films of one window could then not be compared frame for frame. So `TheatreFilm.SetUp` seeks the window again after it sets `Paused`, to the frame nearest the asked second, which is the frame the first seek chose. It takes `_t0` from that frame and logs `[Theatre] film: start pinned at S s (asked A s, the window had reached R s)`. Only a farm window with a seek is pinned: no `-At`, a checkpoint, a nursery tank and a stepped world are as they were. The film's log is `scratch/logs/theatre-film-<Arm><tag>-<yyyyMMddTHHmmss>.log`, so a second film of one arm does not overwrite the first's.

**A film times its own stages, always.** The Editor log carries `[Theatre] timing: <stage> <s> s (since start <s> s)` lines from the first load of `TheatreFilm` (the clock is a UTC tick count in `SessionState`, so it survives the Play-mode reload) to the exit, and one `[Theatre] timing: dress <when>: ...` line after the warm-up and another over the whole film, with the one skins', the pattern tiles', the palette's paint pieces' and the skin dress's seconds (the sums are `DressTimers`, in `TheatrePattern.cs`) and the count of log lines the Editor received, by kind. The mouth and nose placements the one skin makes are inside its `one skin` time. Setting `EVOSIM_FILM_LOG_TIMESTAMPS=1` in the environment of `theatre-film.ps1` adds Unity's `-timestamps`, so every line of the log carries its wall time; unset, the launch is as before.

**Release code does not speed a batch film.** Neither Unity's `-releaseCodeOptimization` argument nor setting `CompilationPipeline.codeOptimization` from an `[InitializeOnLoad]` hook rebuilt `Evosim.Core`, and with the mode reading Release the film's skins, tiles and coral took the same seconds as in Debug, to within 2%. Both were removed. A film logs `[Theatre] code optimization: <Debug|Release> (Evosim.Theatre built <time>, Evosim.Core built <time>)` at the entry point and at the first Play-mode tick, so a later attempt can prove its own mode.

**The coral's seconds are the bed's.** In a box world `TheatreCoralRock.PlaceFor` builds the relief bed (`BedShape`) to stand the coral on, and with the box bed off every caller discards it (`out _`). In r64t-t-6203 the `coral placed` line reads 153.9 s for the bed and 0.0 s for the placement, about a third of a film's wall time.

**The coral is cached, and a hit builds no bed** (`TheatreCoralCache.cs`, `EVOSIM_THEATRE_CORAL_CACHE`, 1 by default, 0 neither reads nor writes). `PlaceFor` looks in `<repository root>/scratch/theatre-cache/coral/<key>.json` before it builds the bed. The key is a SHA-256 over the run's `coreHash` and `configHash`, both seeds, every config field the bed and the placer read, the loaded `Evosim.Core` module's id (it changes with every recompile of the code that grows the coral) and a format version. A file is used only when the coral it rebuilds has `run.json`'s `coralDigest`; otherwise the log says `coral cache: miss <key8>: <why>` and the coral grows as before. Only a grown coral whose digest is `run.json`'s is written, and a run whose `run.json` records no digest is never cached. On a hit `landscapeBed` is null, which every caller discards (`out _`); a caller that needs the bed must not take it from `PlaceFor`. Look for `coral cache: hit` in the log of a film's second render; to time a growth again, delete the file or set the dial to 0.

**A close shot plans its camera over the whole shot, so two films compare frame for frame only at the same start and the same length.** A 10.5 s film from the table window's start framed another view at every frame than the 29.5 s film did, over half the pixels apart at the same second. A frame-diff baseline is a film of the same window, start and length. Anything that culls by the camera, such as dressing only the bodies near it, takes its margin from the planned path over the whole shot, never from the frames drawn so far.

`EVOSIM_THEATRE_FILM_CLOSE_LOOK` picks where the close shot's bearing search starts. Unset, it starts from the three-quarter view; `up` looks up from below and `down` steeply down. `mouth` looks into the subject's first eating part's mouth, from the reverse of that mouth's outward normal at the film's first frame, the normal the bite line's `facing` reads. The forward's height is held to 0.8, so the camera's up stays usable. The plan line then ends `, looking into mouth K (normal x y z)`; a subject with no drawn eating part keeps the three-quarter view and logs `close look mouth asked, no mouth on body B`.

**A nursery tank is played back like a film window, from `EVOSIM_THEATRE_NURSERY`** (`NurseryEpisodeWorld.cs`, Mode `Nursery`). The variable names a tank folder that `Evosim.Nursery --restep` wrote: `episode.json`, `frames.bin` (float32 little-endian, frame-major, every channel found by its name in `episode.json` and never by its offset), and optionally `episode-config.json`, `leaves.json` and `leaf-frames.bin`. `EVOSIM_THEATRE_NURSERY_ROOT` is the repository root that the request's `pool` (and, without an `episode-config.json`, its `recipe` and the recipe's `world.config`) is relative to. With the variable unset every statement runs as before: the mode is inserted lines in `TheatreRunner`, `TheatreFilm` and `SafariDirector`, and a film takes `_fromNursery` as a third source beside the checkpoint and the window (`DriveNursery`). Nothing is stepped and no joint is solved: a link's recorded place and attitude are the part's centre and its world rotation, so the root is link 0's place and the rest hang from it as `LiveWorldView.Place` takes them. A swimmer's id is its slot; a leaf's is 1000 plus its id in `leaves.json`, and swimmers come first in `IdAt`. A film second that lands on a recorded frame (within 0.2 ms, about ten times the float32 clock's own error) shows that frame exactly; between two, places are lerped and attitudes slerped, `Interpolated` turns true for good and the first caption line gains "interpolated between recorded frames", so a caption that says it means the film started off the recorded frames. Until a film has taken over (`NoteFilmRate`, called once the runner is paused) the world shows nothing the runner's own playback clock asks for, because that clock runs while the film sets up and would move the film's first second off the frames; `NoteFilmRate` logs how far the start is from the nearest recorded frame. A body is hidden on any frame where one of its floats is NaN, which is how both files mark a dead swimmer and a gone leaf. The config is the episode's whole `RunConfig` from `episode-config.json`, and the two tunables its writers lacked, `mouth.killSwallowsBody` (false) and `physics.paceFactor` (1), are inserted and named in the log; any other missing field, a pool row or a pool file that is missing, an unset `EVOSIM_THEATRE_NURSERY_ROOT`, a frame file of the wrong length, a clock that does not rise, and a swimmer whose developed part count is not its recorded links each refuse the open by name. A leaf in the same case is left out and counted on the label. Bodies are painted sated. A swimmer is drawn at developed size (the open logs the recorded link spacing against it; swimmers measure 0.985 to 1.000). A leaf with two links or more is drawn at its recorded size, its recorded link spacing over its developed spacing at each frame, to the nearest 0.01, because a leaf can still be growing in the tank; the frames carry no tissue or body fraction for a leaf. A single-link leaf has no spacing and is drawn at developed size, and the open logs one line with the scale range applied and the single-link count. The bed is built from the config (its relief included), and no corpse, current or snow is drawn. The safari is not wired to it beyond `World` and `View`: it plans scenes from a run directory, its clades and checkpoints or from farm film windows, none of which a tank has. Two shots of the film follow one swimmer instead (`FilmNurseryShots.cs`, nursery films only): `follow` keeps the swimmer, named by its pool member (`-Member`, the pool row in `episode.json`'s `members[].pool`; its body id is its slot, `members[].slot`), side-on to its heading and ten degrees up, filling `-Fill` of the frame's height (0.3 to 0.8, default 0.7) with its centre, length and heading smoothed over 1.5, 2 and 3 s; and `nose` is a short pass (`-NoseSeconds`, default 3, from `-NoseAt`, default the film's start) fixed in the nose part's own frame, 40 degrees off its axis, looking at the sensing tip (`MouthPoint.Local` through the part's recorded pose) with the nose's length filling a third of the frame, never nearer than the swimmer's own reach plus the film's quarter metre. The film's window is `-At` to `-To`; a shot numbers its frames from 0.

**A look set names the defaults of the lens, snow, body, rock and grade dials** (`TheatreLook.LookSet`, from
`EVOSIM_THEATRE_LOOK_SET`). Unset, `fix` is the default wherever the film's look is on (`EVOSIM_THEATRE_LOOK` at 1 or more) and `today` for census and solo stills; `fix` for new films was decided on 2026-10-19 by the coordinating agent
under the owner's overnight delegation, to be confirmed by the owner. `today` reproduces every film and frame made
before the set existed, so a render to be compared with an earlier one sets `EVOSIM_THEATRE_LOOK_SET=today`. A
single dial's own environment variable still wins over the set, and the log's look line begins `look set fix` or
`look set today`.

Keys: `Space` pause, `[` `]` pace, `L` pace lock (at or under real time, for filming), `K` seek,
`C` colour, `X` raw shapes (the colliders as the physics has them, no rounding, carve, taper or
bend), `F` follow, `R` reload, `H` hide, `P` provenance, click to select; fly with `WASD`+`QE`, right-drag to look, wheel for speed.
`EVOSIM_THEATRE_RUN`, `EVOSIM_THEATRE_GENOME`, `EVOSIM_THEATRE_SEEK` and
`EVOSIM_THEATRE_OVERRIDE` set the same fields from a script.

**The interface is UI Toolkit under `Assets/Theatre/UI/`** (logbook/0096, built 2026-09-13 from
the owner's `design/SPEC.md` and `logbook/specs/theatre-ui-spec.md`): the strip with the
provenance word and the identity's coverage, the transport bar and timeline, the census with
the audit and the matter residual, the popover, the inspector, the solo census. Four things
about it bite. **A cousin's ids are unverifiable**: the runner's id map names the body on
screen, but the recording's `lineage.jsonl` belongs to another realisation, so under
`EVOSIM_THEATRE_OVERRIDE` the ancestry and the dead panel are withheld and only what the live
body carries is shown. **`ScreenCapture` writes nothing under `-batchmode`**: every picture of
the interface goes through `TheatreUiCapture` (the world camera and the panel into one
`RenderTexture`, read back), which is what `TheatreUiCheck` and `theatre-snap.ps1 -Chrome`
use; the default snapshot stays chromeless so `logbook/images/` stays comparable. **The type
and the rhythm take a 1.5 step at 3400 px** (the agent's ruling for the owner's 3840 monitor;
the design's rule is against uniform panel scaling, not against a density step), carried by
literal mirrors under `.is-wider` because a custom property declared there reached nothing,
cause unsettled: the stylesheet states the six sizes twice and both move together. **The
font assets are dynamic**: a few KB each, rasterised at runtime from the three Plex TTFs
beside them, so a player build would have to carry the TTFs. The end-to-end is
`Evosim.Theatre.EditorTools.TheatreUiCheck.Run` (`-batchmode`, no `-quit`, no `-nographics`;
`EVOSIM_THEATRE_RUN` for a world, `EVOSIM_THEATRE_GENOME` for solo, `EVOSIM_THEATRE_OVERRIDE=1`
against `runs/r42smoke` for the cousin states, which is the one recorded run this build
opens under another `simHash`; `runs/r37bsmoke3` served until 2026-09-22 and is now refused
on a missing `bed` field, and `runs/r37-s1` cannot serve, its config predates two tunables),
asserting every field against the replay and writing every state at 1920 and 3840 into
`scratch/snaps/ui/`; a fixture is a 300 s smoke recorded on the worker that runs it. The
live-mode end-to-end is `Evosim.Theatre.EditorTools.LiveUiCheck.Run` (`91aea20`, 2026-09-22;
same launch shape, `EVOSIM_THEATRE_RUN` a farm run and `EVOSIM_THEATRE_CHECKPOINT` its
checkpoint), 129 assertions over the same panels answered from the live world, and it
finishes with the refusal when the live world does not open rather than falling through to
a solo verdict, which the replay path once did too (2026-09-13's note in `Drive()`).

Both modes also run headless, which is how they are tested:

```powershell
$env:EVOSIM_THEATRE_RUN = "$PWD/runs/th-ref"
Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    '-projectPath', "$PWD/unity-w6", '-batchmode', '-quit', '-nographics',
    '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreIdentityCheck.Run',
    '-logFile', "$PWD/scratch/logs/theatre-identity.log")
```

That entry is the **edit-mode** check, and its verdict line now says so. It drives one physics step
per iteration of a loop of its own, so it passed for three days while the Play-mode replay was
parting from its recording (logbook/0083). `TheatreIdentityCheck.RunInPlayMode` is the other one.
Same environment, plus `EVOSIM_THEATRE_SAMPLES` (20) and `EVOSIM_THEATRE_WALL_MINUTES` (30), and
**launched without `-quit`**. It enters Play mode, lets the Theatre Runner's own `Update` step the
run many steps to a frame, prints the identity verdict at every sample, and quits the Editor with
0 or 1. `EVOSIM_THEATRE_PLAYMODE=1` sends the plain `Run` entry there instead.

## Gotchas

- **A safari dial read before Play mode is lost.** Entering Play mode reloads the domain and resets the safari's
  statics. Only what is packed in `SessionState` crosses the reload. Read a new dial in Play mode, beside the story
  look's own dials, or pack it. `3663bda2` read `EVOSIM_THEATRE_SCENE_CLOCK` while parsing the request, so the clock
  never switched on, and phase 191's first run stopped on its own guard (fixed at `d6d53981`).
- **A subject picked from the farm's positions may not exist in the theatre's cousin.** A safari or film steps its own
  realisation, so a farm body id can be absent there; the safari then films another member of the clade and says so in
  the log (`the guide's exemplar ... is not alive in this cousin`). Pick subjects from the cousin's own positions, or check
  the id against the cousin before a render.
- **The coral close shot's frame 4 flickers in one small patch.** In `r62g-reach`'s close story, a 10x9 box at
  (51,17) of frame-000003, a fogged speck in open water, renders one of at least three ways from run to run. It
  differs by at most 2/255 on 15 to 26 pixels, on the same build with nothing refreshed. On 2026-10-24 two runs of
  one build disagreed (b25b), and the patch differed in 5 of 9 off runs that day, so a rerun does not clear it. An off check
  whose only difference lies wholly inside the box (40,5)-(75,40) of that frame, at 3/255 or less, is that
  flicker and passes; `scratch/theatre-realism/flicker-ok.py` reads a look-check report that way. Any other
  difference is real.
- **A shader that picks an axis by comparing sizes from the model matrix needs a tolerance.** The matrix's
  column lengths on a rotated body carry rounding of about 1e-7, and its sign changes from frame to frame as the
  body turns. Two genome sides that are equal therefore swap places in an exact `>=`, and whatever hangs on the
  choice swaps with them. All four of round 62's swimmer bodies carry a square slab, 0.3 by 0.15 by 0.3. Its taper
  and bend jumped a quarter turn and back every few frames while its logged pose was smooth (2026-10-24).
  `TheatreBody.shader` compares through `AtLeast` and `AtMost`, within a relative thousandth. A new axis choice
  goes through them too. A "ties resolve in a fixed order" comment holds only for bitwise-equal numbers.
- **A film window of a card run is faithful at its first instant only.** A round run on the graphics card in single
  precision is a different realisation from the CPU, and `--film-window` always steps on the CPU. The window
  agrees with the run at its starting checkpoint and parts at the next report second, 10 s in, in
  `auditResidual` and `meanHeight`; this happened in every longer window of round 62's worlds (2026-10-23 and
  -24). A window shorter than 10 s compares only that first row, so it reads FAITHFUL ("1 of 1"), and that
  verdict means nothing more. Caption such a film as a close cousin from its checkpoint. A source whose hashes
  match the run's does not change this.
- **The water's clock counts every frame rendered before a scene, unless `EVOSIM_THEATRE_SCENE_CLOCK` is on.** The
  shaft march, the old shafts, the surface and the bed's caustics read Unity's `_Time.y`, stepped once per captured
  frame. A scene's ripples and shafts therefore depend on every scene filmed before it in the same Unity run. A
  rerun of the same scene list is identical, which is what the identity checks rely on. A retake alone, or a pass
  with another scene list, shows other shafts and lets the meter settle up to 0.4 EV elsewhere. `SCENE_CLOCK` 1
  (`3663bda2`, the theatre lane) sets the water's own clock (`EvoTime()` in `TheatreWater.hlsl`) to each take's
  world second, so a scene's water depends on that scene alone (read in Play mode from `d6d53981`). With it and
  `STORY_METER_SCENE` on, a scene filmed alone and after another settles within 0.01 EV and is near-identical, not
  byte-identical: specks at the edges of mid-distance bodies still differ (phase 191). Inference: something on the C#
  side, perhaps `TheatreSkin.AdvanceDrift`'s `Time.timeSinceLevelLoad`, still runs on Unity's clock. No code sets
  `EVOSIM_THEATRE_SAFARI_CLOCK`.
- **Prove a shading term is live in the shot before building its fix.** Render one take with the term forced to zero or to
  a false colour, and show the pixels move. Light-through terms are zero when the key is behind the camera, and the film's
  shots look down with the key above. Three colour dials in a row were built on a dead term and came out byte-identical
  (`THALLI 2`, `ALGA_BACK_GREY`, `THALLI_SEE`, 2026-10-28).
- **A render chain does not die with its heat guard.** A guard that trips writes `STOP`, stops the Unity processes it names
  and exits, while the chain behind it waits in its rest; lifting `STOP` would let that chain render with no guard. After any
  trip, stop the chain's own process and launch a fresh one, which starts a fresh guard. A worker the guard's pattern does not
  name runs on through a trip, so the pattern names every worker the chain renders on (2026-10-28).
- **Gate every launch on its own heat reading.** The launching command reads the temperature and launches only under 80 °C;
  a reading taken a minute earlier is not the gate (2026-10-28, a launch at 81 °C).
- **The skin can hide a joint, so every joint is bridged** (`TheatrePalette`'s necks, the
  theatre-joins brief in `logbook/specs/theatre-joins/`). Rounded leaves, sphere parts drawn as
  the genome's ellipsoid, and wavy plate rims all once left gaps where Core's anchors coincide. A
  neck now runs from each anchor to both drawn surfaces, inside the collider. A block anchored on
  a plate's very edge can still read loose (r56-s2 #90944). Before calling any body detached,
  grow it with Core's `Developer` from the round's own sources and measure the anchor gaps.
- **A continuation from a farm checkpoint reads "configHash differ" on a build whose RunConfig is
  the farm's own.** Round 47's continuation showed it, and so did round 56 seed 2's on the merged
  build of 2026-10-05, whose RunConfig is byte for byte round 56's (`a174d013…` in the checkpoint,
  `c7ba90b5…` from the Editor's `world.Config.Hash()`). The cause is not found. A guess is that
  Mono and .NET 8 write a double differently inside `Hash()`. Until it is found, the word says
  nothing about the build, and a configuration change is checked against the farm's own hash.
- **A fresh worker's first batch run logs an `ArgumentOutOfRangeException` from
  `UnityEditor.Search.SearchInit.IndexationOnStartup`.** It is Unity's search index on a new
  Library, not the theatre's code, and the run goes on.
- **Watch a round in the theatre before writing it up, and say what was seen.** On 2026-09-10 the
  owner opened round 33 seed 3 in the theatre and saw the whole world as two vertical ribbons a
  metre wide in a box twenty metres long (logbook/0083). A newborn was placed touching its
  parent (D077), the current returned every body to where it found it (D037, written when
  "nothing reads horizontal position" was true), and no body ever swam, so every clade was a
  column packed around its founder's spot for the whole run, draining its own cells since the
  grid. Thirty-three rounds were read on a report that carries a mean depth and per-patch bins
  and nothing about x or z, and the theatre had existed for three days without being pointed
  at a scored run. The rule from it: no round's entry is written until the world has been
  watched in the theatre, and the entry says what it looked like; and the table's `cols`,
  `cols abs` and `x sd` (the occupied 1 m columns of the footprint and the spread of x) are
  read with `alive`. The rounds' books, prices and verdicts stand as measured; every claim
  about where food is relative to bodies is confounded and logbook/0084 lists the retries.
- **A theatre render's wall is set from the replay's pace, and its wait can outlive the
  Editor.** Round 39 seed 1's full-length render (5,000, 15,000 and 30,000 s at 2,000 bodies
  beside three arms) reached 15,000 s in five hours and its 600-minute wall about 40 minutes
  short of 30,000 s (2026-09-18): the theatre quits itself at `EVOSIM_THEATRE_WALL_MINUTES`
  with no `timed out` line in the log, and the frame is simply missing. A full-length replay
  of a 30,000 s seed runs no faster than the farm did, so give it half again the seed's own
  wall (900 minutes at this crowd). And `theatre-snap.ps1`'s `Start-Process -Wait` waits for
  the Editor's process tree: an orphaned `Unity.Licensing.Client` whose parent had exited
  held the first render chain for an hour after the Editor was gone. A chain that calls the
  script in sequence should read the Editor's exit from the log and the pictures, and
  stop a helper whose parent is dead before the next render.
- **A theatre snapshot of a live run can time out before its later frames.** The early pictures
  of `r37-s1` at 5,000 and 15,000 s on a machine running five arms reached the first in about
  forty minutes and timed out at ninety before the second (2026-09-12); a live run replays no
  faster than the farm did. Take early frames one at a time, or wait for the queue. And
  the script's default wall of 30 minutes is short for even a first frame at 3,000 s
  beside three arms and a render: round 41's early look timed out on it with nothing
  written (2026-09-18). Pass `-WallMinutes` from the farm's own pace, about a minute per
  20 simulated seconds at 500 bodies on a loaded machine.
- **A pass that touches `Assets/Evosim` orphans the smoke recorded before it.** The tank's
  second pass moved `simHash` after `r37tank` was recorded, and the theatre refused its
  pictures on the mismatch (2026-09-12). Record the smoke you will photograph on the tree
  you will merge, after the last pass, or photograph before the next one.
- **The project is in linear colour space from 2026-09-16 (logbook/0104), and a global shader
  colour is not converted.** A material colour, a render setting and a camera's background are
  converted from sRGB by the engine; `Shader.SetGlobalColor` hands the numbers over raw. The
  first lit-water pictures were five times too bright for exactly that. Every global colour
  takes `.linear` by hand, and the first picture after a new one is the check. The owner's
  open Editor on `unity/` reimports on the flip.
- **`FindObjectsByType` does not see the skin's furniture.** Every piece of it is created with
  `HideFlags.DontSave`, and the engine's finders leave such objects out, so the snapshot's
  inside-only hiding found nothing for six days and the shafts stood in every side view
  (logbook/0104). A marker on such an object keeps its own list (`TheatreInsideOnly.All`);
  never look for the theatre's own objects with a finder.
- **A theatre render is a sixth Editor, and its start-up is the load, not the replay.** The
  asset scan and the script and shader compiles run on every core; three renders in forty
  minutes had the owner hearing the fans. `theatre-snap.ps1` runs the Editor at four job
  workers; batch the changes and render once, and never beside a test suite.
- **`EVOSIM_THEATRE_DRESS_SET_COUNT=1` counts the bodies a still close shot could show, and changes nothing.** In a farm-window
  film it logs `[Theatre] dress set: N of M bodies (...)` once per close shot, after the shots are planned and before the warm-up
  (`TheatreDressSet.cs`). N is the bodies any film frame of the shot puts inside the planned frustum, or near the eye, by the
  window's recorded roots and each body's reach plus `EVOSIM_THEATRE_DRESS_SET_MARGIN` (default 1 m) or
  `EVOSIM_THEATRE_DRESS_SET_EYE` (default 3 m). A body not on screen at the plan's frame takes the largest adult reach on screen.
  A following close shot, a nursery or a live film logs that it was not swept. The count is read-only: with
  `EVOSIM_THEATRE_DRESS_NEAR` off every body is still built and dressed as before, so a film with the count on should be
  byte-identical to one with it off (by construction, not yet proved).
- **`EVOSIM_THEATRE_DRESS_NEAR=1` dresses only that set, and every frame has to stay byte-identical to the dial off.** It serves a
  farm-window film whose shots are all still close shots (not `FILM_RAW`); any other film logs `[Theatre] dress near: off for
  this film (<reason>)` and is as before. The film tells the runtime before Play mode (`TheatreDressSet.Wanted`), so the
  window's first frame already builds every body (root, parts, visuals) but leaves its one skin pending
  (`LiveWorldView.DeferSkins`). The close plan builds the subject's skin before it reads the subject's drawn bounds
  (`FilmPlans.PlanClose`, `BuildSkinOf`), and dresses it if the rotation had already painted the body. After the plan the set
  is swept and its skins are built (`ApplySet`); from then a body outside the set is skipped by the paint rotation (the cursor
  still walks over it), by `DressUndressed`, and by the warm-up's gate, which logs how many it passed over. The mouth and nose
  keywords are switched on when a body is built, from its parts (`NoteOrgans`), and a body carrying a nose is built at once
  until the nose skin's keyword is on, since only a built skin sets that. The skin map's keyword is set by
  `TheatreSkinMap.Register`, which the window runs for every body, so it needs nothing. The paint switches on other global
  state at the first body that has the feature (the alga blade, arc and thalli keywords, behind their own dials); those are
  not reproduced, so leave the alga dials off or prove a film with them on.
- **Under the underwater look (`EVOSIM_THEATRE_LOOK=4`), `EVOSIM_THEATRE_STORY_AMBIENT` and `EVOSIM_THEATRE_STORY_FOG` change
  nothing in the frame.** On theatre-look-refilm `2dc6d92c` (2026-11-08, worker 7), cells with the ambient at 2 and 3 and the fog
  at 0.022 rendered as the film look's ambient 1 and fog 0.035 did, at both seconds tested: no pixel moved by more than 2
  levels (at most 5), and luma, tone, edges and hue read the same to the second decimal (D330). The scene's fill at 2.4
  against 0.8 moved 3% of the pixels, the bodies, by up to 39 levels. The story look's log line printed the new values all
  the same (`ambient x2`, `fog 0.022`), so a dial reaching StoryLook is no proof that it reaches the render. Those two keys of
  `scripts/theatre-film-look.json` are inert on this build. A body's shadow side is lifted by the scene's fill
  (`EVOSIM_THEATRE_FILL`, opposite the sun, which the body shader adds as an extra light). The near-field haze comes from the
  second batch's fade (`EVOSIM_THEATRE_FADE_START`, `_FADE_FULL`, `_FADE_SCATTER`). Before tuning a story-look key, prove it
  is live: render one cell with it moved and compare the frames byte for byte.
