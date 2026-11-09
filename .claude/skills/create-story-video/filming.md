# filming

Films the footage of every shot of the shot list from the run's own record, run shots and the footage under
the graphics alike, with handles on both sides, and has the clips reviewed for faults before the edit takes
them.

## Deliverable

In the film's folder:

- `clips/<shot id>-<digest>.mp4`: one clip a shot with footage, at the settings' size and frame rate, with no
  words, numbers or labels in its frames. It holds the shot's stretch of the run and a handle on each side, so
  that the edit can move a cut or make a dissolve. `<digest>` is the first ten characters of the file's SHA-256,
  and `clips.json` names the current file of each shot. A clip filmed again gets a new file beside the old one
  and never writes over it: Resolve links a file where it lies, so a cut already built keeps showing the clip
  it was built from. (Round 49: pass 5 wrote 3 s-handle offline clips over the 4K ones the rough cut v3 linked
  at a 10 s in-point, and Resolve showed 2.10a, 6.2a and 8.5a offline from a quarter of the way in.)
- `clips/<shot id>-sheet.png`: eight of its frames, left to right and top to bottom: the first, the
  action's start, a quarter, half and three quarters through the action, the action's end, halfway through
  the handle after, and the last.
- `clips.json`: every clip and what it was filmed from, in the form below.

The step is done when `clips.json` holds a clip of every shot with footage as the shot list now has it, and the
reviewer answered `REVIEW: OK` (every take passes the bar), or the owner accepted the clips it faulted.

## How a shot is filmed

By the project's window route, which nothing in this skill changes:

- `scripts/story-windows.py` plans one window a shot.
- `scripts/story-windows.ps1` records each window on the farm, with the farm program that recorded the run,
  as its `run.json` names it (`source.programPath`). A later build refuses the run's config once a tunable
  has been added since, so `plan` refuses a pass whose runs name different programs, or a program that is
  gone. `plan` also refuses a program whose source is not the run's. It finds the git checkout that holds the
  program's folder and compares its working tree with the run's `source.gitCommit`, over the simulation
  source only: `src/Evosim.Core`, `src/Evosim.Dynamics`, `src/Evosim.Farm` and `src/Evosim.Farm.Gpu`
  (`git diff --name-only <commit>`). A moved HEAD and an uncommitted edit both show. It refuses when any file
  differs, and names the run, the program, the commit, the checkout and up to ten of the files. It refuses
  too when the checkout does not hold the commit, and when the folder is in no checkout. A manifest with no
  commit, or `unknown`, gets a plan line saying the source could not be checked. A pass that checks says so in
  a plan line. The `farm_by_run` setting names another program for a run. The farm replays the run from the checkpoint before it and writes where every body is, thirty
  times a second. It compares the replay with the run's own rows, and a clip whose replay agreed reads
  `FAITHFUL`.
- `scripts/theatre-safari.ps1 -FromWindows` films each window in the Unity theatre, on the render worker.
  Nothing is simulated in the Editor.
- The two overlap, run by run (the owner's idea, 2026-09-28). The pass records the run with the fewest
  shots first, and the theatre films it as soon as its windows are in, while the next run records. The
  farm works the processor and the theatre mostly the card, so together they stay near half the machine:
  the farm at `farm_threads` and the Editor at four job workers. The theatre films one run at a time,
  since a worker takes one Editor. A run whose recording failed is not filmed, and the next pass films its
  shots. On round 49's pass 2, before the overlap, the recording took 37 minutes with the card idle.
  The `overlap` setting, false, turns the overlap off: the pass records every window, and the theatre films
  after the last.
  A heat guard runs beside every pass: `film-guard.ps1` under the rule the `guard` setting names (`-CardTerms`,
  `-FilmTerms` or D258's own stops), `-KillTree` on the pid in
  `started.json` and `-StopFile` on `film\STOP`. On a trip it writes the stop file, with the time and the reason
  inside, and kills the pass's process tree. `start` refuses while the file exists, and a running pass records and
  films nothing more once it appears, and ends `FILM RUN: FAILED` with the text in `done.json`'s `stopped`. Wait
  until `scripts/temp-check.ps1` reads the CPU under 75 °C, tell the machine's coordinator, and delete the file
  (the coordinator's ruling of 2026-11-07; `scripts/tests/story-video-check/test_stop_file.py`).

**The far shots' fog.** The world's cameras (`Arrival`, `Descent`, `Card` and `Floor`) look through tens of
metres of water. The story look's fog, 0.022, leaves a body 30 m off about two thirds of its light and one
55 m off about a quarter. So round 49's first Arrival at 4K read flat and soft: its frames ran from 31 to 70
of 255, and its edges were weaker than in nine seconds out of ten of round 48's film (the owner, 2026-09-28,
found it really soft and somewhat dark). The setting `far_fog` gives those four cameras a fog of their
own, and the close cameras keep the look's. `plan` writes it into each far scene of the story. The theatre
sets it before the take's first render and says so in its log, and `collect` refuses a far clip whose log
does not. `far_fog` is part of a far shot's key, so changing it films the far shots again and no others.

**The close shots' focus and side.** The theatre focuses its `Portrait` and `Birth` cameras on their subject.
The setting `close_focus` gives those two cameras a focus of their own in place of the grade's. It takes
the form the theatre's story reader takes: a `mode` of `bokeh`, `gaussian` or `off`, with any of
`aperture`, `format`, `radius`, `start`, `end` and `supersample`. Round 49's is the owner's pick from the
focus test (option E): `{"mode": "gaussian", "radius": 1.5, "start": 1.1, "end": 2}`. A `Portrait` under a
graphic asks the director for its subject in the left third, since the graphic's card takes the right.
Otherwise the director puts it on the side it came from, which is the right half the time. `collect` refuses a close clip whose theatre log
does not say its focus, and a portrait under a graphic whose log does not put its subject on the left. Both
are in the shot's key, like the far fog.

`film.py plan` writes the director's story for these tools: one scene a shot, the shot's camera, held at
its second, with no chapter card, no captions and no chart. It links into the pass every window an earlier
pass of the same film recorded over the same run, seconds and frame rate, and the farm records only the
rest: a pass that changes only the render (the look, the snow, the glass, a camera) records nothing (the
owner, 2026-10-11). A shot is filmed again only when the fields it
was filmed from change, which `clips.json` records as its key. So a new shot list films only the shots it
changed.

A clip's key holds the look's version (`theatre_look`), the look's dials as `look_dials` (the first 16 hex characters
of a sha256 of the sorted, space-free JSON of the `wide` and `events` sections of the main checkout's
`scripts/theatre-film-look.json`, the sections the safari reads) and the theatre build as `seat` (`theatre_commit`'s
first 8 characters, lower-cased, so a short and a full hash key alike). A take on the wrong dials or the wrong seat
must never be kept silently, so changing either films every shot again; to keep old takes after a reseat that is
proven not to change the drawing, the coordinator marks them kept, by name, in the flow. `plan` refuses when the look
file is missing and prints `look dials <digest>, seat <commit>` in its header.

**The limits:**

- The director opens a birth's take 8 seconds before the birth, a constant in its code, so a `Birth`
  shot's handle before is shorter than the rest. Making it a scene field is a change to the theatre's C#
  and needs a Unity compile, which is the owner's to allow.
- Handles stop at the run's first second, and a second short of its last row.
- The planner cannot always place a body in its family. It then films the scene as another camera, and
  `plan` refuses the pass.
- A `Portrait` whose body is not alive through the take films the largest of its family instead. Only the
  review can see it.
- The render worker must carry the theatre of `unity/Assets/Theatre`. `plan` warns and `start` refuses
  when it does not. It is refreshed from PowerShell with `./scripts/new-worker.ps1 -Workers <n>`, which is
  a Unity reimport.

**The pace is set by the render under the underwater look**, not by the PNG write. Round 57's look test, at
1920x1080 with the 2x supersample and `theatre_look` 4, took a median of 240 ms a frame on a descent and 706 ms
on the floor for the render and read-back on the main thread. The PNG encode, 83 to 103 ms, ran on the
writer's threads beside it and held nothing up. The cost follows the scene, not the size: at 1080p an arrival
took 1,512 ms and a card 560 ms, and at 960x540 the same kinds of shot took 1,043 and 1,413 ms. So a small
offline pass saves little time under the look, and a film of about 21,000 frames takes hours at any size: plan
the 4K pass as an overnight job, raise `wall_minutes` to fit, and measure it first on a short 4K test. The window
recordings take one to three minutes a shot on the farm at 10 threads, slower on the efficiency cores. Each
run adds a few minutes of Unity start-up. Without the look, as on round 49, the render
took about 67 ms and the PNG write about 270 ms a 4K frame, and the pass ran at about six times real time.

## The settings

`film-settings.json`, beside this file, holds the owner's defaults. The tool copies it into the film's folder
the first time, and from then on reads the copy.

| Setting | Default | What it is |
|---|---|---|
| `handles_s` | 3 | The seconds recorded before and after each shot's stretch, where the run allows it. The edit moves a cut by at most 2 s and a dissolve needs about half a second, so 3 s covers both, and a shot that needs more is filmed again on its own (the owner, 2026-09-28, in place of their 10 s of 2026-09-27). Round 49 was filmed at 10 s, and about 70% of its footage never reached the screen. The setting is in every shot's key, so changing it films every shot again. |
| `handles_by_shot` | `{}` | A shot's own handle, by its id, where the film's does not suit it: a Colony shot whose second member is born inside the film's handle, say. It is in that shot's key alone, so setting it films that shot again and no other. |
| `size_by_shot` | `{}` | A shot's own size, by its id: an offline pass films the shots set small here, is reviewed, and the 4K pass films the same shots again once they are taken out. A pass films at one size, so the plan refuses a pass whose shots ask two. In that shot's key alone. |
| `takes_by_shot` | `{}` | A shot's take number, by its id, 1 or more. Raising it films that shot again when nothing else in its key changed, as after a fix to a camera in the theatre, which no key sees. In that shot's key alone. |
| `subject_by_shot` | `{}` | A world shot's subject by its id, for now only `"plants"`, for a Card whose `see` asks for plants and not animals. `plan` writes it as the scene's `subject` in place of `world`, refuses an entry for a shot not on a world station (Arrival, Descent, Floor, Card), and warns of an id not in `shots.json`. In that shot's key alone, and only when set. A subject reaches the picture only on a theatre build whose director knows it; a build that does not films the shot as `world`, so the pass is planned only on a head the coordinator ruled in for it. |
| `fps` | 30 | Frames a second, for the windows and the clips. |
| `size` | `3840x2160` | The clips' frame: 4K, as the edit is (the owner's ruling of 2026-09-28). |
| `worker` | 6 | The render worker, `unity-w<n>`. |
| `theatre_root` | null | The checkout that holds the render worker `unity-w<n>` and the `theatre-safari.ps1` the pass runs, absolute or from the checkout's root; null is this checkout. The plan's worker check, `machine`'s lock check, the theatre's takes and its log all read from it. It must hold `scripts/theatre-safari.ps1`. Not in a shot's key. |
| `theatre_commit` | null | A commit or branch name. When set, `plan` and `start` refuse unless `git rev-parse HEAD` of the `theatre_root` checkout equals `git rev-parse <theatre_commit>` there; the refusal names both. The theatre lane reseats that checkout between films, and both its workers compile its `src`. Its first 8 characters, lower-cased, are in every shot's key as `seat`. |
| `guard` | `standard` | The heat guard `start` launches beside the job (`film-guard.ps1`, hidden, as its sibling): `card terms` is D258's stops with one CPU reading of 90 C, or two in a row at 85 C; `film terms` is D258's stops with one CPU reading of 95 C, or two in a row at 90 C (the coordinator's terms for the theatre and film windows); `standard` is D258's own stops; null runs none. Its pid is in `started.json` and its log is `guard.log` in the pass folder. `plan`'s header says which. A guard that does not start stops the job. Not in a shot's key. |
| `guard_minutes` | 600 | How long the guard watches, in minutes. Not in a shot's key. |
| `runs_root` | `runs` | Where the runs are, from the checkout's root. |
| `runs_root_by_run` | `{}` | A folder for a run, by its name: `{run name: folder}`, absolute or from the checkout's root, each an existing folder that holds the run, for a film whose runs lie under more than one root (round 67's forest test is under `scratch/plants-work/runs`). A run named here is looked up there in place of `runs_root`, by `plan` and by the theatre's `-RunsRoot`. `story-windows.py` takes one root, so a pass films the runs under one root, the root with the most shots first, and the plan names the shots that wait. Not in a shot's key. |
| `runs_only` | `null` | The only runs the next pass films, as a list of run names; null films every run. The plan names the shots of the other runs as waiting, and refuses a list that leaves nothing to film. It films one run by itself, to be judged before the rest are filmed, as after a driver fault cut a take short. Set it back to null for the pass after. Not in a shot's key. |
| `max_scenes` | `null` | The most scenes one pass films, a whole number of 1 or more; null sets no cap. After `runs_only` and the splits by build, colour and root, `plan` keeps the first `max_scenes` shots to film in shot-list order and names the rest as waiting for the next passes; the estimate and the wall are computed from the shots kept. It exists because the theatre leaks memory per scene: round 67's coordinator ruled on 2026-11-08 that the refilm runs in passes of at most 12 scenes, each its own Unity process. Not in a shot's key. |
| `record_only` | `false` | True makes the pass record the farm windows of every shot whose clip is missing or stale and film nothing. `plan` then keeps `runs_only` and the split by farm build and runs root, and ignores `max_scenes` and the splits by colour and size, since a window depends on none of them; it makes no estimate of the theatre and no wall refusal, and says in a line how many shots on how many runs it records. `run` records each run's windows as now and starts no theatre; `done.json` and `plan.json` record `record_only: true`, and `wait` answers `FILMING: DONE` when every recording exited 0. `collect`, `slides` and `encode` refuse such a pass. It exists because a window is a CPU replay from a checkpoint that depends on nothing about the camera, the look, the colour or the size, and was most of round 67's last pass (4.4 hours); the windows can be recorded while the camera fixes wait, and the next filming pass reuses them. Not in a shot's key. |
| `farm_threads` | 10 | The farm's threads for recording, a third of the machine. |
| `farm_cores` | null | The logical processors the window recorder and its farms are pinned to, at below-normal priority, such as `"16-31"`; null leaves them where the job runs. |
| `theatre_cores` | null | The logical processors the pwsh that starts each theatre-safari is pinned to, at below-normal priority, such as `"16-19"`. The Unity Editor it starts inherits the mask from that pwsh, as Windows children do; null launches the theatre unpinned. Not in a shot's key. |
| `overlap` | true | Whether the theatre films a run while the farm records the next. False records every run's windows first and films after, so the farm and Unity never run together. Not in a shot's key. |
| `farm_by_run` | `{}` | A farm program for a run, by its name: `{run name: path to an Evosim.Farm.exe}`, absolute or from the checkout's root, each an existing file. A run named here records its windows on that program in place of the one its `run.json` names, and the build split, the one-program refusal and `plan.json`'s `farm` all take it. `plan`'s source check applies to it as to the manifest's. Not in a shot's key: the farm records the windows and does not change what the theatre draws. |
| `wall_minutes` | 180 | How long the theatre may film a run before it stops itself. `plan` estimates each run against it (the three rows below) and refuses a run that is over. |
| `theatre_start_s` | 300 | The seconds Unity takes to start and load, once a run. Measured on round 67's pass 2 at 3840x2160 (2026-11-07). A film's older copy without it takes the default. Not in a shot's key. |
| `theatre_window_s` | 240 | The seconds the theatre takes to load a window and skin its living bodies, once a window. About 2.5 minutes at mid-run crowds and 6.5 minutes or more at a crowded late second. Not in a shot's key. |
| `theatre_frame_s` | 0.45 | The seconds a frame costs at 3840x2160, scaled by the pass's pixel count against 3840x2160 and never below a quarter of it, so a 960x540 pass costs 0.1125. Not in a shot's key. |
| `theatre_look` | 4 | The theatre's underwater look, `EVOSIM_THEATRE_LOOK`, 0 to 4: 4 is both batches (depth light and in-scatter, the shafts shaded by the reefs, the run's own snow drifting with the current by the film's own second, the far fade and the tone curve), the look the owner chose on 2026-10-10; 0 is the old look. The theatre reads it from the environment and is off without it, so the job sets it for every theatre it starts. It is in every shot's key, and each clip in `clips.json` names it. |
| `theatre_stream` | false | Each take's frames go straight to the graphics card's encoder, which writes the take's mp4 with no PNG on disk (`theatre-safari.ps1 -Stream`). It stays off until a comparison film against the PNG path has been read. When it is on it is in every shot's key, so turning it on films every shot again, and each clip in `clips.json` says whether it was streamed. A film's settings without it read as off. |
| `snow` | none | The snow's dials under the underwater look, any of `gain` (the motes' brightness, 0 to 4, `EVOSIM_THEATRE_SHELL_GAIN`), `quantity` (their number as a multiple of the look's, 0.25 to 3, `EVOSIM_THEATRE_SNOW_QUANTITY`) `least` (the least share of the reference density a column counts as, 0 to 1, so water thin in snow still shows some, `EVOSIM_THEATRE_SNOW_LEAST`) and `ambient` (a share of the open water's brightness each mote adds, 0 to 6, `EVOSIM_THEATRE_SNOW_AMBIENT`; near a winter bed the sun alone leaves the motes unseen at any gain or quantity); none keeps the look's own snow. Set when it is, it is in every shot's key, and `collect` refuses a clip whose theatre log does not show it. Round 57's 4K shots near the bed showed almost no snow at the look's own (2026-10-11). |
| `glass`, `earth` | true | Whether the theatre draws a tank's glass and the earth at it (`EVOSIM_THEATRE_GLASS`, `EVOSIM_THEATRE_EARTH`). From inside the water the far glass reads as a faint pane with a seam across the frame, and the owner asked on 2026-10-11 that round 57's film not show the tank. Off is in every shot's key, and `collect` checks the theatre's `skin:` line. |
| `caustics` | none | The caustic net's strength on the bed, the reef and the bodies, as a multiple of the shaders' own, 0 to 2 (`EVOSIM_THEATRE_CAUSTIC_SCALE`); none keeps the shaders'. In every shot's key when set, and checked like the glass. |
| `far_fog` | none | The far shots' own fog density, 0.002 to 0.08; none keeps the story look's 0.022 (see "The far shots' fog"). The look test sets it. |
| `close_focus` | gaussian, radius 1.5, 1.1 to 2 | The close shots' own focus (see "The close shots' focus and side"); none keeps the grade's, URP's Bokeh at f/2. The default is round 49's option E, which the owner chose on 2026-09-28; an agreed look carries into every film without asking. The look test checks it. |
| `reference` | `assets/full-episodes/rounds-50-57 v3.mp4` | The last finished episode. It is the floor the bar's light is held to (the bar, item 1), and the sheets are read against it. `film/reference.json` caches its numbers and names its `file`; `collect` measures again when that file is not the one the settings name now. |

## A shot's colour

A shot with footage may carry `colour`, the name of a colour in the film's `colours.json` (the form is in
[shots-schema.md](shots-schema.md)). The main session writes that file, never the tools. For each colour it
holds what the colour shows, a map file (`file`, from the repository's root, with its `sha256`) and the runs
whose body ids the map is written for. The map has a line a body, `id`, a tab and a turn. The theatre turns
each named body's hue by its turn, a signed fraction of the colour wheel. It reads the map once, from
`EVOSIM_THEATRE_TEAMS`, when its Editor starts. So:

- **A pass films one colour.** After the split by farm build, `plan` splits the shots left to film by colour,
  in the same way, no colour counting as one. The colour with the most shots goes first, and ties go by name
  with no colour first. A plan line says how many shots wait for the next passes. `plan.json` records the
  pass's colour as `{"name", "file", "sha256"}`, or null.
- **The variable is the pass's alone.** `run` sets `EVOSIM_THEATRE_TEAMS` to the absolute path of the pass's
  map. It removes the variable from a pass with no colour, so a value left in the shell never reaches it.
- **The colour is in the shot's key** with the map's sha256, and only when the shot has one. A film filmed
  before keeps its keys, and a changed map films its shots again.
- **`plan` and `collect` refuse the faults `shots.py check` refuses.** In the file: it is malformed, a map is
  missing, or a `sha256` is not its map's. In a shot: the colour is not a string, `colours.json` lacks it, the
  shot has no footage, or the shot's run is not one the colour lists.
- **`collect` reads the theatre's log.** The theatre writes `[Theatre] teams: <N> bodies turned from <path>
  (EVOSIM_THEATRE_TEAMS)` when it loads a map. `collect` refuses a clip of a coloured pass whose log has no such
  line with N above 0 naming the pass's file. It refuses a clip of an uncoloured pass whose log has one. Paths are
  compared with slashes and case normalised. Each clip in `clips.json` names its colour.

## A shot's canopy

A shot whose camera is `Arrival` or `Descent` may carry `"canopy": true` (the form is in
[shots-schema.md](shots-schema.md)). The theatre reads the canopy per scene, from the story, so it goes to the
theatre as a field of that shot's scene and a canopy shot shares its run's pass with the other shots.

- **`plan` writes `"canopy": true` into the shot's scene in `story.json`**, and only for a shot that has it. Each
  plan entry records `canopy`. No pass is split by it, `run` passes no switch for it, and the safari's folder and
  log keep their names.
- **The canopy is in the shot's key**, only when the shot has it, so no other shot is filmed again.
- **`collect` reads the theatre's log.** The director writes `take N of <slug> at T s: canopy: ...` for each take
  planned as the canopy shot. `collect` refuses a canopy shot whose log has no such line for its scene. Under
  `EVOSIM_THEATRE_CANOPY_GROUP` the director's group descent in the canopy's place counts too, shown by its line's
  `in place of the canopy descent under the densest column of leaves`.

## clips.json

```json
{"schema": "create-story-video/clips/1", "shots_sha256": "<64 hex>",
 "reference": {"file": "…", "luma": 41.2, "sheet": "film/reference-sheet.png", "tone": 35.0, "tone_p10": 25.0,
               "edges": 7.82, "edges_p10": 3.25,
               "at": {"960": {"tone": 33.0, "tone_p10": 23.0, "edges": 11.9, "edges_p10": 5.4}}},
 "clips": [
   {"shot": "1.1a", "key": "<16 hex>", "pass": 1, "file": "clips/1.1a.mp4", "sha256": "<64 hex>",
    "frames": 793, "fps": 30, "width": 1920, "height": 1080, "run": "r49-s2", "clip_start_s": 4884.5,
    "action_in_s": 10.0, "action_out_s": 16.42, "handle_before_s": 10.0, "handle_after_s": 10.0,
    "provenance": "FAITHFUL", "luma": 38.7, "jump_ratio": 2.4, "tone": 29.0, "edges": 7.66,
    "look_width": 1920, "reference_look": {"tone": 35.0, "tone_p10": 25.0, "edges": 7.82, "edges_p10": 3.25}, "rgb": [16.3, 62.6, 78.3], "fog": null,
    "focus": {"mode": "gaussian", "radius": 1.5, "start": 1.1, "end": 2}, "side": "left",
    "sheet": "clips/1.1a-sheet.png", "sheet_times_s": [0, 10.0, 11.6, 13.21, 14.81, 16.42, 21.71, 26.4]}]}
```

- `clip_start_s` is the run's second at the clip's first frame.
- `action_in_s` and `action_out_s` are where the shot's stretch lies in the clip, in seconds from its first
  frame. The edit takes the clip from `action_in_s` and has the handles either side.
- `luma` is the clip's mean luma, 0 to 255, beside the reference's, which is the floor: a clip under it is
  flagged `dark`.
- `takes` is the frames of each take the scene was filmed as, in order, or null when the log did not give them.
- `flags` lists what `collect` flagged on the clip: `provenance`, `jump`, `flat`, `soft` and `dark`. None of them
  fails the collect by itself. The reviewer judges them against the bar.
- `jump_ratio` is the clip's largest change between two frames over its median change, within takes. A body that pops in
  size, or a frame that flashes, shows as a high ratio; `collect` prints any at 6 or more.
- `tone` is the clip's median spread of luma from the 10th to the 90th percentile, and `edges` its median
  Sobel edge strength, three frames a second. A flat frame has a small spread, and a soft or fogged one has weak
  edges. Both are read at the clip's own width, capped at 1920 (`look_width`), because a frame scaled up first has
  every edge spread over more pixels and reads softer. The reference is read at that same width, scaled down from its
  own frames, and `reference_look` holds its four numbers there; `reference.json` caches the other widths under `at`
  beside the 1920 ones at its top level. `collect` prints a clip under the reference's tenth percentile of either at
  its width as flat or soft. When the reference is narrower than the clip, or of another aspect ratio, the clip is
  not compared: `reference_look` is null, it gets neither flag, and its line says so; judge its tone and edges by eye
  against the reference sheet.
- `rgb` is the clip's mean red, green and blue, 0 to 255, each frame averaged over a 160 by 90 image at three
  frames a second. The edit's grade stretches the clip's contrast about it.
- `fog` is the fog the clip was filmed in when it was not the look's, and null otherwise.
- `focus` is the close shot's own focus when it was not the grade's, and `side` the side a portrait under a
  graphic put its subject; null otherwise.
- `colour` is the name of the colour the clip was filmed in, and null for the bodies' own colours.

## Working files

In the film's folder, for pass k, numbered from 1 and going on from the highest there:

- `film/pass-<k>/`: the tool's own. `story.json` and `plan.json`, `windows/` with `windows.json` and the
  recorded windows, `windows-plan.txt`, `start.log`, `started.json`, `run.log` and `run.err.log`,
  `film-<run>.log` for each run's filming, and `done.json`.
- `film/reference.json` and `film/reference-sheet.png`: the reference film's luma and eight of its frames,
  made once a film, and again when the settings name another reference file. The reference is the floor the
  bar's light is held to, not the target.
- `film/pass-<k>/slides/index.html` and `film/pass-<k>/slides/img/`: the deck `slides` builds for pass k.
- `film-<k>-machine.txt`, `film-<k>-wait.txt`: what the tool printed for the main session.
- `film-<k>-plan.txt`, `film-<k>-start.txt`, `film-<k>-collect.txt`: what the tool printed for the filmer.
- `film-<k>-owner.md`: the owner's words on the render and on a busy machine, saved word for word.
- `film-<k>-filmer-<r>.md`: the filmer's answer on run r, saved word for word.
- `film-<k>-review.md`: the reviewer's answer, saved word for word.
- `film-<k>-review-owner.md`: the owner's words on the faults, saved word for word.

## Who does what

| Who | Brief | Reads | Writes |
|---|---|---|---|
| the tool | [film.py](film.py) | `shots.json`, the settings, the run's lineage and stats, the director's output | the pass's files, the clips, their sheets and `clips.json` |
| the filmer | [filmer-brief.md](filmer-brief.md) | what the tool prints | the tool's printouts, and its answer |
| the reviewer | [film-reviewer-brief.md](film-reviewer-brief.md) | `shots.json`, `clips.json`, the sheets, the pass's slides images and the reference sheet | nothing but its answer |
| the main session | this file | what the tool prints, the answers, the owner's words | those, saved |
| the owner | | the plan's counts, the faults and the sheets | the go on the render, and what to do about a fault |

## The tool

`python <checkout>\.claude\skills\create-story-video\film.py <command> …`. Every command prints its verdict
on its first line and how long it took on its last.

- `plan <folder> <k>`: plans pass k for every shot with footage not yet filmed as it stands, on one farm build:
  a window is recorded on the build that recorded its run, so a film whose runs come from several builds films
  the build with the most shots first and names the shots that wait for the next passes, and `collect` keeps
  every pass's clips. A pass films one colour as well ("A shot's colour"). `READY`, `NOTHING TO
  FILM` or `REFUSED`, with a line a shot giving its camera, its stretch with handles, and any short handle.
- `machine <folder>`: `FREE`, or `BUSY` on a farm run, a batchmode Unity, an ffmpeg, a compute process on
  the card, a held render worker or one carrying another theatre.
- `start <folder> <k>`: starts the pass as a detached job through PowerShell, so that it outlives whoever
  started it, and returns within seconds. `STARTED` with its process id, or `REFUSED`. PowerShell's output
  goes to `start.log` and never to a pipe: the job inherits PowerShell's handles, and a pipe it holds keeps
  `start` waiting until the whole pass ends. It refuses a pass that has a job already, known by its
  `run.log`, even when no `started.json` was written, since a second job would film into the same folder.
- `run <folder> <k>`: the job itself. It records each run's windows and films each run as its windows come
  in, as above, and writes `done.json`. Its own lines go to `run.log`, the recording's too, and each run's
  filming to `film-<run>.log`. Only `start` runs it.
- `wait <folder> <k> <minutes>`: looks every 30 seconds until the job ends or the minutes pass. `DONE`,
  `FAILED` or `STILL RUNNING`. It ends by itself, so it can run in the background.
- `collect <folder> <k>`: copies the pass's clips into `clips/`, makes their sheets and numbers, and writes
  `clips.json`. `PASS` when every shot with footage has a clip as it now stands, `FAIL` with the shots that do not.
  It prints a clip's luma under the reference's as `dark`, beside `flat` and `soft`. It measures `jump_ratio`
  within takes, leaving out the change across each cut between the takes of one scene, and records the takes' frames
  as `takes`. It flags `provenance` only for a label that is missing or is neither FAITHFUL nor COUSIN.
- `slides <folder> <k>`: builds a deck of pass k's collected takes in `film/pass-<k>/slides/index.html`, its
  images beside it in `slides/img/`: a dark page, one slide on screen at a time, arrow keys to move, nothing
  loaded but its images. A first slide names the film, the pass, the size, the number of takes and the date.
  A slide a take, in the shot list's order, holds the shot's id and camera station, its run and seconds, what
  the viewer should see (`see`), the take's sheet, one full-size frame from the action's middle at the clip's
  own size, and its numbers from `clips.json` beside the reference's with each flag. The last slide holds the
  bar, word for word. The images are copies, so the deck survives a later collect. `SLIDES: READY` and the
  deck's path, or `SLIDES: REFUSED` for a pass that is not planned and collected.
- `encode <folder> <k>`: for a pass whose theatre ended at its own wall, before `theatre-safari.ps1` reached its
  encode block. For each run in `done.json` it reads the safari folder's `scenes.tsv` (none: it says so and goes on)
  and does the script's encode in Python for each `played` scene: each take's frames to `take-<n>.mp4` with the
  script's ffmpeg arguments, one take copied, a `Time` scene's last two takes crossfaded, any other station joined
  through `parts.txt`, then `film-sheet.py`, then the take's frames deleted. A scene whose `<slug>.mp4` exists is
  passed over, so it can run again; other outcomes are listed. It refuses, keeping the frames and going on to the
  next scene, a scene folder with no row in `scenes.tsv`, and a scene whose frames on disk differ from the
  theatre's log (`take <n> of <slug> ended: <F> frames`, read from `film/pass-<k>/theatre-<run>.log`): no take
  lines, a take folder the log does not name, or a take with another count. It prints `ENCODE: DONE`, `ENCODE:
  NOTHING TO ENCODE` or `ENCODE: FAILED` with the scenes that failed, a line a scene, and writes the same to
  `film/pass-<k>/encode.log`. ffmpeg runs below normal priority on the settings' `theatre_cores`. The function
  must agree with the script's encode block.
- `stop <folder> <k> <reason...>`: ends a running pass early without losing its finished takes. For a pass with
  `started.json` and no `done.json` it writes the stop file, `film/STOP`, in the heat guard's one-line form: the
  time, the reason, and `(a clean stop: the scene under way completes, then the theatre writes scenes.tsv)`. It
  refuses a pass not started or ended, an empty reason, and an existing stop file (it prints what that says). When
  the theatre checkout's `theatre-safari.ps1` declares `-StopFile`, `run` gave the safari the file (`done.json`
  records it as `stop_file`, else `null`): the safari checks it between scenes, finishes the scene under way, writes
  `scenes.tsv` and exits 1 with `STOPPED at scene`, and `run` films no run after it. Without the parameter the
  theatre does not see the file until its run ends, and only the runs not yet filmed are skipped; `stop` says which
  holds. Use it to end a pass early; the heat guard's kill stays for driver events and heat, and loses the run under
  way. `start` refuses while the file exists, naming its text.
- `plan` also estimates each run's time in the theatre and refuses a run over `wall_minutes` (the faults list,
  "A plan over the wall").

## Launching the filmer and the reviewer

Each is launched with its prompt, word for word. Only the parts in angle brackets are filled in:
`<checkout>` and `<folder>` as absolute paths, `<k>` the pass, `<r>` the filmer's run, 1 or 2.

```text
You are the filmer of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\filmer-brief.md. Read it whole and follow it.
The film's folder is <folder>.
You work on pass <k>, run <r>.
Write only inside the film's folder. Never write in your session scratchpad or in TEMP.
Your time budget is 20 minutes.
```

```text
You are the film reviewer of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\film-reviewer-brief.md. Read it whole and follow it.
The film's folder is <folder>.
You review pass <k>.
Write nothing. Your answer is your whole report.
Your time budget is 15 minutes.
```

The filmer is a general-purpose agent on sonnet, because it runs the tool and quotes it and judges no
clip (SKILL.md's rule on models). The reviewer is an `Explore` agent on opus, which is given
nothing of the project but its brief: it reads pictures and never needs to know how they were made.

## The procedure

The main session runs only the lines below that name it. `plan`, `start` and `collect` are the filmer's, and
`film.py` refuses to plan a pass twice, so a plan the main session runs itself makes the filmer's refused
(round 49's pass 7: the pass had to be removed and the filmer run again).

1. If the film's folder has no `shots.json`, stop: the shot list step writes it first.
2. Run `machine <folder>` and save what it prints in `film-<k>-machine.txt`. On `MACHINE: BUSY`, tell the
   owner you are blocked on the machine, give what it listed, and ask whether to go ahead. Save their words
   in `film-<k>-owner.md`. Go on only on `MACHINE: FREE` or their yes.
3. The look test, before the first pass of a film and whenever `size`, `far_fog` or the story look changes.
   Nothing is filmed in full before the owner agrees its look (the owner ruled on 2026-09-28 to
   stop spending time rendering many things before the settings are agreed).
   - Film a few shots only: the flattest far shots and one of each close camera, at 1920x1080, and only the
     values being weighed. A value already filmed is read from its clip.
   - Film each option for about 3 s, never a window's full length. The test's `story.json` asks 3 s of each
     scene. Its `windows.json` keeps each recorded window's own `from` and `to`, which the theatre checks
     against the recording. A scene may be shorter than its window. Ten options then take about five minutes
     where ten full windows took thirty (the owner, 2026-09-28, found ten full windows too long).
   - Put every option of one shot at the same place in the story, modulo 6. A portrait's move is drawn
     from the scene's place: its family from the place modulo 3, its sense from the place over 3. So
     options at places 1, 2 and 3 film three different arcs. Round 49's focus test sat its options at
     places 1, 7, 13 and so on. It filled the places between with copies that have no window, and played
     only its own with `theatre-safari.ps1 -Scenes`. Under a story, `-Scenes` takes story numbers.
   - When the owner asks to choose, bring a sheet of a few options: one frame each, labelled, with a crop
     at full size where the difference is fine. Otherwise bring one proposal. (For round 49's focus the owner asked
     for a sheet with options, as was done for fog, and said they would name the ones that work best for them.)
   - Judge them yourself, on a sheet beside the reference at the same camera. Read the tone, the edges and
     the brightness as `collect` measures them, and read the frames by eye (the owner ruled that the agent finds
     the best settings itself, since the film can be read in the sheet).
   - Film it as a film of its own: a folder beside the film's (`<film>-look<j>`) holding its `shots.json`, a
     few of the film's shots cut to about 4 s, and its own `film-settings.json` at 1920x1080 with 1 s handles,
     run through this tool's `plan`, `start`, `wait` and `collect`. The film's own passes stay untouched.
   - Before reading it, check three things. Each clip's frames carry the theatre's label, which names the look
     that drew them: it must name the underwater look. `python scripts/film-motion.py <frames-dir>` on each
     clip's frames: no frame may change more than twice the median. And a jointed body, where the film has
     one, is drawn with its joints closed (`logbook/specs/theatre-joins/brief.md`).
   - Read it beside the reference and beside the underwater look's clip the owner liked,
     `scratch/owner/underwater-look-2/clip-new-look.mp4` and its `sbs-*.png` stills.
   - Bring the owner one proposal, with the sheet and the numbers it rests on, and not a menu. Save their
     words in `film-<k>-owner.md`. A no goes back to the test.
4. **The test pass, before any 4K pass.** Round 49's 4K pass took about 2 h 45 min, and its review then faulted
   25 of 41 shots for what they show, not how they look: descents that end in the sand, leaves over the subject,
   arrivals from inside the water. None of that needs 4K to see (the owner, 2026-09-28, asked why they were not
   rendered in offline quality first, for review). The look test checks the look on a few shots; it never checked
   each shot's content. And on 2026-11-07 round 67's second pass filmed seven takes at 4K that the owner saw as night
   shots, dark and with no clear subject, because this pass did not run. The pass is a test, and it is the bar's
   item 5. The bar, word for word as the machine's coordinator wrote it on 2026-11-07:

   ```text
   0. Amazing, judged by eye at full size: light that draws the eye, colour, depth, a subject worth looking at, a composition that holds as a nature-film still. "As good as the last film" fails.
   1. Light: luminous. As a floor, mean luma no lower than rounds-50-57 v3's, 60 to 70. The surface glows above, never a dark ceiling. Shadow sides lit by the water. Shafts kept.
   2. Water: clear teal, not navy-black. Haze only far off.
   3. Subject: every take has one. For a portrait or event it fills at least a fifth of the width, at the centre or on a third, never cut by the frame. The event happens in frame.
   4. Camera: toward or across the light. Never down into empty dark water or level over an empty floor. A descent ends on life.
   5. Test takes first: every shot at 960x540 before any 4K pass. Slides of each take, its sheet and a full-size frame, judged against this bar by eye first, directed and refilmed until it passes. The owner sees only passing takes, with their slides.
   ```

   - Film every shot at 960x540: `size` for the pass, or `size_by_shot` for the shots set small (a pass films at one
     size). No focus supersample. Then `collect` and `slides` (the filmer's runs; the main session runs the rest).
   - Launch the film reviewer on pass k. It judges each take against the bar from its slide, by eye: the sheet and
     the full-size frame, read as pictures. Save its answer in `film-<k>-review.md`, word for word.
   - The main session reads the slides itself, directs the fix of every take that fails, and films those takes again
     at that size, until each passes. A camera station or span goes back to the shot list through a
     `shots-<n>-owner.md`-style file of the reviewer's answer. A light or framing dial goes to the theatre's lane.
     A fix to a camera that changes no key is raised in `takes_by_shot`.
   - The deck goes to the machine's coordinator before the owner sees it. The owner sees only passing takes, with
     their slides.
   - Then the 4K pass films the shots that passed: take them out of `size_by_shot`, or set `size`, which films them
     again at 4K since the size is in every shot's key. Its `plan` estimates the wall first.
5. Ask the owner for their go on the render, since they are asked before any Unity render (their standing
   rule). Give the number of shots with footage and their seconds of footage, from the shot list's check, and say
   that the handles add about twenty seconds a shot. Save their words in `film-<k>-owner.md`, below any
   earlier. On a no, stop.
6. Launch the filmer for pass k, run 1. Save its answer in `film-<k>-filmer-1.md`, word for word.
   - `FILMING: STARTED`: go on.
   - `FILMING: NOTHING TO FILM`: go to step 9.
   - `FILMING: NOT STARTED`, or anything else: show the owner the answer and stop.
7. Run `wait <folder> <k> <minutes>` in the background, with the settings' `wall_minutes` and 30 more, and
   save what it prints in `film-<k>-wait.txt`, below any earlier.
   - `FILMING: DONE`: go on.
   - `FILMING: STILL RUNNING`: run it again. After the third, show the owner and ask whether to wait longer.
   - `FILMING: FAILED`: a failure whose `run.log` and theatre log (`film/pass-<k>/theatre-<run>.log`) show the
     theatre's own time-out ("TIMED OUT at scene") or its clean stop ("STOPPED at scene", after `stop`) and whose
     safari folder holds a `scenes.tsv` is encoded with
     `encode <folder> <k>`, then collected as usual; the scenes it did not reach are filmed in the next pass.
     Any other failure: show the owner what it printed and the last lines of `film/pass-<k>/run.log`, and
     stop.
8. Launch the filmer for pass k, run 2. Save its answer in `film-<k>-filmer-2.md`, word for word.
   - `CLIPS: PASS`: go on.
   - `CLIPS: FAIL`: show the owner the answer, and film the shots it names in pass k+1 from step 2 without
     waiting for them: the owner's go on the render covers every shot of the plan, so a shot that failed on a
     detail is filmed again without asking (the owner, 2026-09-28, asked why the agent was waiting on them to render a
     missing shot). Fix the detail first, in the settings, never by hand. Ask only when the fix changes a
     shot, since a changed shot goes back to the shot list. After two passes in a row that failed, the owner
     rules what to do.
9. Launch the reviewer for pass k. Save its answer in `film-<k>-review.md`, word for word.
   - `REVIEW: OK`: the step is done.
   - `REVIEW: FAULTS`: direct the fixes as in step 4 first, from each take's `fix:` line. Then show the owner each
     fault that remains with the full path of its sheet, as plain text, and ask what to do. Save their words in `film-<k>-review-owner.md`. A shot they want changed goes back to the
     shot list step, and pass k+1 films it; a fault they accept leaves the clip as it is.

Tell the owner when an answer says it stopped at its time budget, and how long the pass took, from
`started.json` to `done.json`.

A record-only pass (`record_only` true in `film-settings.json`) is planned, started and waited on like any pass, the heat
guard included, and is not collected: `collect`, `slides` and `encode` refuse it, and the review has nothing to read. It
records the farm windows and no clip. The next filming pass reuses them, because `plan` links an earlier pass's window
of the same run, seconds and frame rate (`reuse_windows`). Set `record_only` back to false before that pass is planned, or
it records again and films nothing.

- **A colony's members are counted where its window opens, handle and all.** Round 49's shot 4.4a pulled
  back on body 11248 and its child 11299 from 4,920 s. The child was born at 4,914.5 s, and a 10 s handle
  opened the window at 4,910 s, where the director found a colony of one and refused the scene. A Colony
  shot whose members are born near its start needs a handle shorter than the gap, and `collect` names it
  only after the whole pass has run. Give such a shot its own handle in `handles_by_shot`, which changes that
  shot's key and no other's, so the next pass films it alone.

## Queued for the next film

- **No PNG frames on disk** (the owner ruled on 2026-09-28 to skip the PNG write to disc). It is built behind
  the `theatre_stream` setting, which is off. Each take's frames go to one ffmpeg process as they are read
  back. It encodes them on the card's video encoder (NVENC) into the take's mp4. The PNG path stays as it was
  for stills and checks. On synthetic frames the stream's clip matches the PNG path's in size, order,
  orientation and colour (`scripts/tests/theatre-stream/test_stream_encode.py`). It has not filmed a shot
  yet. The comparison is next. Film the same shots both ways with `-DownsampleCheck`, then compare a frame
  of each, the clips' colour and brightness, and the pace, before a film is made with it. Under the underwater
  look the render sets the pace (see "The pace"), so the stream takes the PNG encode's CPU off the pass but is
  not expected to shorten it much.
- **The whole of steps 7 and 8 takes too long** (the owner, 2026-09-28, found it long, said it needs shortening, and accepted it for this video). Round 49's estimate was about two hours of filming and four
  to five of graphics, most of it in animator agents drawing one graphic at a time. The levers, measured
  where they can be: 3 s handles (already the default); frames straight to the card's encoder (above);
  several animators at once, since their time is mostly the agent's and not the machine's; and fewer
  graphics per film, or graphics built from templates the animator fills in rather than writes.

## Before the first film

Round 49 is the step's first film and the window route's first whole film. After it, this file records
the pace, and the owner compares one clip's sheet with the reference by eye.

The tests stand in for the farm and the theatre, so its first real passes found what they could not
(2026-09-28). Each fault is fixed in the tool and has a test in `scratch/story-video/film-test/`:

- **Pass 1 failed in six seconds.** The windows were recorded on the checkout's newest farm build, which
  refuses round 49's `config.json` for a tunable added since ("Missing required field
  'foundersFollowIncomeDepth'"). `plan` now takes the program that recorded the runs from their
  `run.json`, and refuses runs recorded by different programs or by one that is gone.
- **Pass 2's `start` never returned.** It read PowerShell's output through a pipe that the detached job
  inherited, and it would have waited for the whole pass. So `started.json` was never written, `wait`
  refused, and nothing stopped a second `start`. `start` now writes PowerShell's output to a file, and
  refuses a pass that has a job already. That pass's `started.json` was written by hand from the job's
  process id and its `run.log`.
- **The first 4K clip went to the owner before anyone looked at it**, and they found it soft and dark.
  It was neither the encode (a source frame and its encode agree to 44 dB) nor the render (its edges are
  crisp at full size). It was the fog over a far shot's distance. The meter had held the clip's mean at the
  brightness the owner chose on round 48; what the clip lacked was range. `collect` now measures tone and
  edges.
- **The whole pass started before its look was agreed**, and was stopped on the owner's word at the ninth
  of seed 2's 34 scenes. The look test, step 3, now comes first. Round 49's was made by hand from pass 2's
  windows, linked under new scene numbers in `film/look-1/`. Each take ran its window's full length, on the
  belief that the theatre refuses a scene shorter than its window. That was wrong: the refusal was a
  re-planned window whose span no longer matched its recording. The focus test (`film/focus-2/`) ran 3 s a
  take. `film.py` has no command for either yet.
- **The close-ups' focus is Unity's, and at 4K it is weak.** URP caps its Bokeh blur at 14 pixels of the
  render target's height, so a supersampled 4K frame blurs a quarter as much as a plain 1080 one, and its
  Gaussian blur is gentler still. Round 49's options (`film/focus-2/`) softened a far crowd's edges by at
  most a fifth. The owner wants cinema-like shallow focus, and deferred it to a later video on its cost
  (2026-09-28, saying the improvement can be added in later videos). It needs a lens pass of the theatre's own:
  a depth-aware blur before URP's post-processing, sized as a share of the frame's height.
- **The graphics card waits on the CPU.** Round 49's pass 3 drew and read back a 4K frame in 60 ms (a far
  shot) to 108 ms (a close-up with its focus blur). Writing it as a PNG took 320 to 344 ms of CPU, and two
  writer threads set the pace at about 165 ms a frame. The card read 30 to 40% busy while the CPU was
  loaded, and the owner saw the CPU churning while the GPU barely moved.
- **The overlap has run only against stand-ins.** Its first real pass is the check that the theatre films
  one run while another run's windows are still being recorded into the same folder.

Round 57's film is the first under the underwater look, and its look test found three more (2026-10-10):

- **A portrait of a body born inside its handle was missed.** The founder, body 11630, was born at 5,177.5 s.
  Its portrait's stretch started at 5,178 s, and the 1 s handle opened its window at 5,177 s, when the body did
  not yet exist. The director finds no subject and misses the scene. The 4K pass then opened it at 5,178 s,
  half a second after the birth row, and missed it again: the body is first drawn at 5,178.04 s, the frame
  after the step that placed it. `plan` now opens a shot of a named body no earlier than a second after the
  body's birth, and refuses a stretch that starts before that, which then goes back to the shot list
  (`scripts/tests/story-video-check/test_lead_in.py`).
- **The safari script stopped at its frame delete.** Another process held a take's first PNG open while
  `-DeleteFrames` ran. `Remove-Item` threw, and the script exited before it encoded the other scenes. The
  delete now retries and then warns, and never fails the run. That pass's clips were encoded from their frames
  by `scripts/look-check/encode-rest.ps1`, with the script's own arguments.
- **A take's first frame can differ from the rest.** Shot 7.1b's first frame lacked the husks on the bed and
  drew the horizon differently, and the low-res pass's floor shot of r57-s1 opened on a dark frame without
  its husks. From the second frame on each was steady. The frame lies in the handle, so the edit cuts it. It
  is a theatre fault, not fixed.
- **`plan` does not see a film's own farm.** `start` refuses while any farm process runs, a pass's own window
  recorder included, so a second pass or a test cannot start beside a pass that is still recording.
- **The farm and the theatre on the same cores ran the CPU to 92 °C.** The `farm_cores` setting pins the
  window recorder, and so every farm it starts, to other cores at below-normal priority; the film's is 16-31,
  the coordinator's ruling of 2026-10-10 (`scripts/tests/story-video-check/test_farm_cores.py`).
- **A film window beside a Unity step heats the processor.** On 2026-10-30 a four-thread film window started
  beside a Unity step, and the CPU went from 78 to 100 °C in seven seconds (`logbook/specs/machine-and-tools.md`,
  "A farm film window starts as a burst too"). Round 67's film added `overlap` on 2026-11-07, so a pass can run
  its farm and its theatre one after the other (`scripts/tests/story-video-check/test_overlap.py`).
- **A run's program path can name a worktree rebuilt since.** `source.programPath` names a folder in a
  worktree under `scratch/wt-*`, and the worktree can be rebuilt for other work after the run. Round 67's film
  found it on 2026-11-07: plf-6202 was recorded at 1519f7cd, and its worktree then held a later build with a
  different Core. `plan` checked only that the program existed. The windows would have been recorded on the
  wrong build, and only their identity verdicts (`COUSIN` for `FAITHFUL`) would have shown it, after the
  machine time was spent. `plan` now refuses it by the source check, and `farm_by_run` names another program
  for the run (`scripts/tests/story-video-check/test_farm_source.py`).
- **A plan was refused for a run under another root.** Round 67's film found on 2026-11-07 that `plan` refused with no run directory for plf-6202 under `runs`, before any split. Those shots would have waited for a later pass. The forest test lies under `scratch/plants-work/runs`. `runs_root_by_run` names the folder per run, and the pass splits by root (`scripts/tests/story-video-check/test_runs_root.py`).
- **Round 67's worker lived in a worktree film.py could not reach.** Its film found on 2026-11-07 that worker 7 sat
  in the theatre lane's worktree, at `scratch/wt-theatre-look/unity-w7`. film.py looked for it at the repository root:
  `plan` warned of no worker and `start` refused. `theatre_root` now names the checkout.
- **A look from the wrong place is refused.** On a theatre branch `theatre-film-look.ps1` falls back to its own
  copy of the dials, warning "the main checkout's theatre-film-look.json was not found", and takes a stand-in
  gate for a world with no row, warning "has no row in scripts/theatre-world-spots.tsv" (the eyespot gate) or
  "has no row in scripts/theatre-world-stops.tsv" (the blade stops). A world is calibrated before it is filmed, so
  `collect` refuses a take whose theatre log or `film-<run>.log` holds any of the three.
- **A `Descent` in round 57's tank sinks only a little, at mid-water among the table tops.** It never shows
  the surface or reaches the bed in its stretch, so the reviewer faulted six descents whose `see` promised
  both, and passed 1.1a, which moves the same way. They were accepted as they are (`film-5-review-owner.md`).
  Read a descent's `see` against 1.1a's sheet before calling it a fault.
- **Every `Floor` take of one tank followed one path.** `SafariPlans.Floor` went to the lowest point of the
  bed on a lattice around the tank's axis and trucked up the steepest slope from it, whatever the second, so
  round 57's fifteen Floor shots of r57-s2 opened on the same picture, the same husk in the same place. No
  single sheet shows it; put the Floor shots' sheets side by side (`film-6-review.md`, `edit-1-owner.md`).
  The take now trucks up the slope toward the densest cluster of stomachs within 3 m of the bed at its own
  second, ending 5 m short of it, with the approach turned up to 45 degrees by the scene's place; the
  hollow is kept for a second with no stomach on the bed, and the plan line says which it took. A fix to a
  camera changes no shot's key, so its shots are filmed again by raising them in `takes_by_shot`.
- **A re-film can change a world shot's camera, because the move is drawn from the scene's place in the
  pass.** A world shot's hash is `scene.Index * 7919 + 17` (`SafariDirector`), and its index is its place in
  that pass's story. Round 57's look re-film (pass 8, 55 shots in one story) kept every descent's and
  every arrival's path but turned all fifteen Floor shots to other bearings than pass 7 had filmed them at
  (`film-8-review-owner.md`). Not fixed yet: the fix is a hash from the shot itself, its id, which keeps
  a shot's move in any pass. Until then, a re-film that must keep the accepted moves films each shot at
  the place it had, and a look-only re-film is checked against the old takes' plan lines.
- **A plan over the wall.** On 2026-11-07 round 67's second pass sent a run of 41 windows to the theatre under a
  180-minute wall it could not meet. The theatre encodes a run's takes only after Unity exits, and exits before
  encoding when Unity times out, so a time-out loses every take of the run. That run was stopped by hand. `plan`
  now estimates each run's time in the theatre as `theatre_start_s` + its windows x `theatre_window_s` + its frames
  x `theatre_frame_s` (scaled by the pass's pixels), prints a line a run against `wall_minutes`, and refuses a run
  that is over, naming it and saying that `wall_minutes` must rise or the run's shots split across passes. The
  costs were measured on that pass at 3840x2160: 41 windows and 20,586 frames come to about 323 minutes
  (`scripts/tests/story-video-check/test_test_pass.py`).
- **Pass 2 of round 67 was judged against the wrong floor, and its test pass did not run.** The reference was round
  48's film (luma 56.6) where the last finished episode, rounds 50-57 v3, reads 60 to 70, so seven dark 4K takes
  passed the checks. The reference setting now names that episode, `collect` flags a clip under its luma as `dark`,
  and the test pass with its slides is a step of the procedure.
- **A 960x540 test take read soft beside a 4K reference.** Round 67's film, 2026-11-08: a test pass's takes were
  scaled up to 1920 before their edges were measured, so they read soft beside a 4K reference scaled down to 1920
  (shot 9.1a: edges 9.58 against the tenth percentile 11.1). `collect` now measures a take at its own size against the
  reference at that size (the coordinator's ruling of 2026-11-08,
  `scripts/tests/story-video-check/test_look_width.py`). Measured again that way, pass 5's takes and the reference
  both rose about one and a half times (the reference's tenth percentile from 11.1 to 16.09 at 960 wide), and 40 of
  the 41 takes kept their flags: 9.1a read 14.87 and stayed soft. So the scaling was real but small, and most of those
  takes are softer than the reference in fact.
- **A take filmed as another station.** Round 67's 6.4a was planned as a Birth in body 9471's clade on a resumed
  run, and the director filmed it as an Arrival, with only a warning in its log: it reads a resumed run's own lineage
  alone, and 9471 was born before the resume began. `collect` passed it. It now refuses a take whose `station` in the
  theatre's `scenes.tsv` differs from the plan's camera, and points to the theatre's log
  (`scripts/tests/story-video-check/test_test_pass.py`, 2026-11-08).
- **A planned cut and a COUSIN label failed a shot.** Round 67's film, 2026-11-08, found shot 2.4b failing F4 and F7. The
  birth is filmed as two takes, the parent and then the newborn, and the cut between them gave a `jump_ratio` of 51.9
  where every other change was under twice the median. Every take on the farm route is labelled COUSIN, which the
  reviewer read as a fault and asked for a FAITHFUL run that cannot exist. `collect` now reads each take's frames from
  the theatre's log and measures `jump_ratio` within takes, and COUSIN raises no flag
  (`scripts/tests/story-video-check/test_take_jumps.py`).
- **A canopy is asked for per scene.** Round 67's film, 2026-11-08: the coordinator ruled the canopy for the
  opening descent alone. The theatre takes canopy per scene from the story, not only per call, so `plan` writes
  it into that shot's scene and the shot is filmed in its run's pass with the others
  (`scripts/tests/story-video-check/test_canopy.py`).
- **A run reached the theatre's wall with nothing encoded.** Round 67's film, 2026-11-08: pass 5's r67r1-6202 ran at
  about ten minutes a scene against the plan's estimate of five, and reached its wall with about 30 of 41 scenes
  filmed and none encoded, since the script exits before its encode block when the theatre quits with code 1.
  `encode` does that block (`scripts/tests/story-video-check/test_encode.py`).
- **A pass could only be killed.** Round 67's film, 2026-11-08: pass 5 could be ended only by the guard's kill,
  which would have lost every finished take of the run under way, since a kill writes no `scenes.tsv`. The theatre
  gained a stop file, `-StopFile`, read between scenes, and film.py passes it and writes it with `stop`
  (`scripts/tests/story-video-check/test_stop_clean.py`).
- **The director draws no tint.** Shot 6.9a asked for the family tinted along the bed and was filmed as a
  rock table with no family in view. It went back to the shot list as a `Floor` shot, and a tint now comes
  only from a shot's `colour`.
- **One long pass leaked the theatre's memory.** Round 67's film, 2026-11-08: the theatre leaked about 1.6 GiB an
  hour, and then about 12 GiB an hour in one long pass, so passes are capped by `max_scenes`
  (`scripts/tests/story-video-check/test_max_scenes.py`).
- **A change of dials and seat kept old takes.** Round 67's film, 2026-11-08: 33 takes were counted as filmed across a
  change of dials (D329) and of seat (2dc6d92c), because the key held only the look's version number. It now holds
  the look's digest and the seat (`scripts/tests/story-video-check/test_look_key.py`).
