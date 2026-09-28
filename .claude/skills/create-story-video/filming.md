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
reviewer answered `REVIEW: OK`, or the owner accepted the clips it faulted.

## How a shot is filmed

By the project's window route, which nothing in this skill changes:

- `scripts/story-windows.py` plans one window a shot.
- `scripts/story-windows.ps1` records each window on the farm, with the farm program that recorded the run,
  as its `run.json` names it (`source.programPath`). A later build refuses the run's config once a tunable
  has been added since, so `plan` refuses a pass whose runs name different programs, or a program that is
  gone. The farm replays the run from the checkpoint before it and writes where every body is, thirty
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

**The far shots' fog.** The world's cameras (`Arrival`, `Descent`, `Card` and `Floor`) look through tens of
metres of water. The story look's fog, 0.022, leaves a body 30 m off about two thirds of its light and one
55 m off about a quarter. So round 49's first Arrival at 4K read flat and soft: its frames ran from 31 to 70
of 255, and its edges were weaker than in nine seconds out of ten of round 48's film (the owner, 2026-09-28:
"it looks really soft and somewhat dark"). The setting `far_fog` gives those four cameras a fog of their
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
its second, with no chapter card, no captions and no chart. A shot is filmed again only when the fields it
was filmed from change, which `clips.json` records as its key. So a new shot list films only the shots it
changed.

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

**The pace** at 4K is about six times real time: a minute of footage takes six minutes to film, counting
each scene's window and its exposure settle (round 49, the first scenes of seed 2, 5.8 to 6.3 times). Each
run adds a few minutes of Unity start-up. A frame's render and read-back take about 67 ms, and two threads
then write it as a PNG at about 270 ms a 4K frame each, which sets the pace. A film of 41 shots and about
1,140 s of footage therefore takes about two hours to film.

## The settings

`film-settings.json`, beside this file, holds the owner's defaults. The tool copies it into the film's folder
the first time, and from then on reads the copy.

| Setting | Default | What it is |
|---|---|---|
| `handles_s` | 3 | The seconds recorded before and after each shot's stretch, where the run allows it. The edit moves a cut by at most 2 s and a dissolve needs about half a second, so 3 s covers both, and a shot that needs more is filmed again on its own (the owner, 2026-09-28, in place of their 10 s of 2026-09-27). Round 49 was filmed at 10 s, and about 70% of its footage never reached the screen. The setting is in every shot's key, so changing it films every shot again. |
| `handles_by_shot` | `{}` | A shot's own handle, by its id, where the film's does not suit it: a Colony shot whose second member is born inside the film's handle, say. It is in that shot's key alone, so setting it films that shot again and no other. |
| `size_by_shot` | `{}` | A shot's own size, by its id: an offline pass films the shots set small here, is reviewed, and the 4K pass films the same shots again once they are taken out. A pass films at one size, so the plan refuses a pass whose shots ask two. In that shot's key alone. |
| `takes_by_shot` | `{}` | A shot's take number, by its id, 1 or more. Raising it films that shot again when nothing else in its key changed, as after a fix to a camera in the theatre, which no key sees. In that shot's key alone. |
| `fps` | 30 | Frames a second, for the windows and the clips. |
| `size` | `3840x2160` | The clips' frame: 4K, as the edit is (the owner's ruling of 2026-09-28). |
| `worker` | 6 | The render worker, `unity-w<n>`. |
| `runs_root` | `runs` | Where the runs are, from the checkout's root. |
| `farm_threads` | 10 | The farm's threads for recording, a third of the machine. |
| `wall_minutes` | 180 | How long the theatre may film before it stops itself. |
| `far_fog` | none | The far shots' own fog density, 0.002 to 0.08; none keeps the story look's 0.022 (see "The far shots' fog"). The look test sets it. |
| `close_focus` | none | The close shots' own focus (see "The close shots' focus and side"); none keeps the grade's, URP's Bokeh at f/2. The look test sets it. |
| `reference` | round 48's film | The last film the owner accepted, which the sheets are read against. |

## clips.json

```json
{"schema": "create-story-video/clips/1", "shots_sha256": "<64 hex>",
 "reference": {"file": "…", "luma": 41.2, "sheet": "film/reference-sheet.png", "tone": 35.0, "tone_p10": 25.0,
               "edges": 7.82, "edges_p10": 3.25},
 "clips": [
   {"shot": "1.1a", "key": "<16 hex>", "pass": 1, "file": "clips/1.1a.mp4", "sha256": "<64 hex>",
    "frames": 793, "fps": 30, "width": 1920, "height": 1080, "run": "r49-s2", "clip_start_s": 4884.5,
    "action_in_s": 10.0, "action_out_s": 16.42, "handle_before_s": 10.0, "handle_after_s": 10.0,
    "provenance": "FAITHFUL", "luma": 38.7, "jump_ratio": 2.4, "tone": 29.0, "edges": 7.66, "rgb": [16.3, 62.6, 78.3], "fog": null,
    "focus": {"mode": "gaussian", "radius": 1.5, "start": 1.1, "end": 2}, "side": "left",
    "sheet": "clips/1.1a-sheet.png", "sheet_times_s": [0, 10.0, 11.6, 13.21, 14.81, 16.42, 21.71, 26.4]}]}
```

- `clip_start_s` is the run's second at the clip's first frame.
- `action_in_s` and `action_out_s` are where the shot's stretch lies in the clip, in seconds from its first
  frame. The edit takes the clip from `action_in_s` and has the handles either side.
- `luma` is the clip's mean luma, 0 to 255, beside the reference's.
- `jump_ratio` is the clip's largest change between two frames over its median change. A body that pops in
  size, or a frame that flashes, shows as a high ratio; `collect` prints any at 6 or more.
- `tone` is the clip's median spread of luma from the 10th to the 90th percentile, and `edges` its median
  Sobel edge strength, both read at 1920 wide, three frames a second. A flat frame has a small spread, and
  a soft or fogged one has weak edges. `collect` prints a clip under the reference's tenth percentile of
  either as flat or soft.
- `rgb` is the clip's mean red, green and blue, 0 to 255, each frame averaged over a 160 by 90 image at three
  frames a second. The edit's grade stretches the clip's contrast about it.
- `fog` is the fog the clip was filmed in when it was not the look's, and null otherwise.
- `focus` is the close shot's own focus when it was not the grade's, and `side` the side a portrait under a
  graphic put its subject; null otherwise.

## Working files

In the film's folder, for pass k, numbered from 1 and going on from the highest there:

- `film/pass-<k>/`: the tool's own. `story.json` and `plan.json`, `windows/` with `windows.json` and the
  recorded windows, `windows-plan.txt`, `start.log`, `started.json`, `run.log` and `run.err.log`,
  `film-<run>.log` for each run's filming, and `done.json`.
- `film/reference.json` and `film/reference-sheet.png`: the reference film's luma and eight of its frames,
  made once a film.
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
| the filmer | [filmer-brief.md](filmer-brief.md) | what the tool prints | the tool's printouts, through the shell, and its answer |
| the reviewer | [film-reviewer-brief.md](film-reviewer-brief.md) | `shots.json`, `clips.json`, the sheets and the reference sheet | nothing but its answer |
| the main session | this file | what the tool prints, the answers, the owner's words | those, saved |
| the owner | | the plan's counts, the faults and the sheets | the go on the render, and what to do about a fault |

## The tool

`python <checkout>\.claude\skills\create-story-video\film.py <command> …`. Every command prints its verdict
on its first line and how long it took on its last.

- `plan <folder> <k>`: plans pass k for every shot with footage not yet filmed as it stands. `READY`, `NOTHING TO
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

## Launching the filmer and the reviewer

Each is launched with its prompt, word for word. Only the parts in angle brackets are filled in:
`<checkout>` and `<folder>` as absolute paths, `<k>` the pass, `<r>` the filmer's run, 1 or 2.

```text
You are the filmer of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\filmer-brief.md. Read it whole and follow it.
The film's folder is <folder>.
You work on pass <k>, run <r>.
Write only inside the film's folder, and only through the shell. Never write in your session scratchpad or in TEMP.
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
   Nothing is filmed in full before the owner agrees its look (the owner, 2026-09-28: "stop wasting time by
   rendering a whole bunch of things before we agree on the right settings").
   - Film a few shots only: the flattest far shots and one of each close camera, at 1920x1080, and only the
     values being weighed. A value already filmed is read from its clip.
   - Film each option for about 3 s, never a window's full length. The test's `story.json` asks 3 s of each
     scene. Its `windows.json` keeps each recorded window's own `from` and `to`, which the theatre checks
     against the recording. A scene may be shorter than its window. Ten options then take about five minutes
     where ten full windows took thirty (the owner, 2026-09-28: "no that's too long").
   - Put every option of one shot at the same place in the story, modulo 6. A portrait's move is drawn
     from the scene's place: its family from the place modulo 3, its sense from the place over 3. So
     options at places 1, 2 and 3 film three different arcs. Round 49's focus test sat its options at
     places 1, 7, 13 and so on. It filled the places between with copies that have no window, and played
     only its own with `theatre-safari.ps1 -Scenes`. Under a story, `-Scenes` takes story numbers.
   - When the owner asks to choose, bring a sheet of a few options: one frame each, labelled, with a crop
     at full size where the difference is fine. Otherwise bring one proposal. (Round 49's focus: "i'd like
     a sheet with options, like we did for fog, and i'll tell you which ones works best for me".)
   - Judge them yourself, on a sheet beside the reference at the same camera. Read the tone, the edges and
     the brightness as `collect` measures them, and read the frames by eye (the owner: "i want you to find
     the best settings. you can see the film in the sheet").
   - Bring the owner one proposal, with the sheet and the numbers it rests on, and not a menu. Save their
     words in `film-<k>-owner.md`. A no goes back to the test.
4. Ask the owner for their go on the render, since they are asked before any Unity render (their standing
   rule). Give the number of shots with footage and their seconds of footage, from the shot list's check, and say
   that the handles add about twenty seconds a shot. Save their words in `film-<k>-owner.md`, below any
   earlier. On a no, stop.
5. Launch the filmer for pass k, run 1. Save its answer in `film-<k>-filmer-1.md`, word for word.
   - `FILMING: STARTED`: go on.
   - `FILMING: NOTHING TO FILM`: go to step 8.
   - `FILMING: NOT STARTED`, or anything else: show the owner the answer and stop.
6. Run `wait <folder> <k> <minutes>` in the background, with the settings' `wall_minutes` and 30 more, and
   save what it prints in `film-<k>-wait.txt`, below any earlier.
   - `FILMING: DONE`: go on.
   - `FILMING: STILL RUNNING`: run it again. After the third, show the owner and ask whether to wait longer.
   - `FILMING: FAILED`: show the owner what it printed and the last lines of `film/pass-<k>/run.log`, and
     stop.
7. Launch the filmer for pass k, run 2. Save its answer in `film-<k>-filmer-2.md`, word for word.
   - `CLIPS: PASS`: go on.
   - `CLIPS: FAIL`: show the owner the answer, and film the shots it names in pass k+1 from step 2 without
     waiting for them: the owner's go on the render covers every shot of the plan, so a shot that failed on a
     detail is filmed again without asking (the owner, 2026-09-28: "why are you waiting on me to render a
     missing shot?"). Fix the detail first, in the settings, never by hand. Ask only when the fix changes a
     shot, since a changed shot goes back to the shot list. After two passes in a row that failed, the owner
     rules what to do.
8. Launch the reviewer for pass k. Save its answer in `film-<k>-review.md`, word for word.
   - `REVIEW: OK`: the step is done.
   - `REVIEW: FAULTS`: show the owner each fault with the full path of its sheet, as plain text, and ask
     what to do. Save their words in `film-<k>-review-owner.md`. A shot they want changed goes back to the
     shot list step, and pass k+1 films it; a fault they accept leaves the clip as it is.

Tell the owner when an answer says it stopped at its time budget, and how long the pass took, from
`started.json` to `done.json`.

- **A colony's members are counted where its window opens, handle and all.** Round 49's shot 4.4a pulled
  back on body 11248 and its child 11299 from 4,920 s. The child was born at 4,914.5 s, and a 10 s handle
  opened the window at 4,910 s, where the director found a colony of one and refused the scene. A Colony
  shot whose members are born near its start needs a handle shorter than the gap, and `collect` names it
  only after the whole pass has run. Give such a shot its own handle in `handles_by_shot`, which changes that
  shot's key and no other's, so the next pass films it alone.

## Queued for the next film

- **An offline pass of every shot, reviewed, before the 4K pass.** Round 49's 4K pass took about 2 h 45 min, and
  its review then faulted 25 of 41 shots for what they show, not how they look: descents that end in the sand,
  leaves over the subject, arrivals from inside the water. None of that needs 4K to see (the owner, 2026-09-28:
  "why didn't you render them in 'offline' quality first to review?"). The look test checks the look on a few
  shots; it never checked each shot's content. So the order becomes: every shot at a small size (about 960x540,
  no focus supersample), the film reviewer and the owner on those clips, fixes filmed again at that size, and only
  then the 4K pass of the shots that passed. Until the tool has the pass, film it by setting `size` for a pass and
  back, which films every shot again at each change, since the size is in every shot's key.

- **No PNG frames on disk** (the owner, 2026-09-28: "skip the png write to disc"). The theatre's frame writer
  hands each frame, as read back, straight to an ffmpeg process that encodes it on the card's video encoder
  (NVENC), in place of writing a PNG a frame and encoding them at the end. It is expected to take most of
  the CPU off the pass and to run it two to three times faster, but that is a guess until it is measured.
  Build it in the writer behind a switch, with the PNG path kept for stills and checks. Then film the same
  shots both ways, and compare a frame of each and the pace, before a film is made with it.
- **The whole of steps 7 and 8 takes too long** (the owner, 2026-09-28: "well that's a lot. we need to work
  on shortening this. but ok for this video."). Round 49's estimate was about two hours of filming and four
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
  (2026-09-28: "we can add this improvement in later videos"). It needs a lens pass of the theatre's own:
  a depth-aware blur before URP's post-processing, sized as a share of the frame's height.
- **The graphics card waits on the CPU.** Round 49's pass 3 drew and read back a 4K frame in 60 ms (a far
  shot) to 108 ms (a close-up with its focus blur). Writing it as a PNG took 320 to 344 ms of CPU, and two
  writer threads set the pace at about 165 ms a frame. The card read 30 to 40% busy while the CPU was
  loaded, and the owner saw it ("i see the cpu churning, but the gpu barely moving").
- **The overlap has run only against stand-ins.** Its first real pass is the check that the theatre films
  one run while another run's windows are still being recorded into the same folder.
