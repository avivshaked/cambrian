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
is added to `PictureConfig` in the same change; nothing checks it.

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
