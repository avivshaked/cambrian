# graphics

Draws every graphic the shot list names, the stills of real bodies they show and the words on screen. They
are transparent clips the edit lays over the footage, and a reviewer reads them for faults.

## Deliverable

In the film's folder:

- `graphics/clips/<shot id>-<digest>.mov`: one clip a graphic shot, transparent, at the settings' size and
  frame rate. It holds the drawing, timed to its shot's beats, with its first frame held before it and its last
  held after it for the handles.
- `graphics/words/<shot id>-w<i>-<digest>.mov`: one clip a line of words on screen, transparent, as long as the
  words show, fading in and out.

`<digest>` is the first ten characters of the file's SHA-256, and `graphics.json` names each drawing's current
file. A drawing rendered again gets a new file beside the old one and never writes over it, so a cut already
built in Resolve, which links the file where it lies, keeps the drawing it was built from. Drawings rendered
before 2026-09-28 keep their old names, which `graphics.json` records.
- `graphics/stills/<name>.png`: one picture a body a graphic shows. It is square and transparent, drawn in
  the theatre's skin.
- `graphics/sheets/`: `<shot id>-sheet.png` for each graphic, `words-sheet.png` and `stills-sheet.png`. A
  graphic's sheet holds its first frame, each beat just after it shows, its middle and its last, over a
  checkerboard that shows where it is clear.
- `graphics.json`: every clip and still, and what it was drawn from, in the form below.

The step is done when `check` passes and the reviewer answered `REVIEW: OK`, or the owner accepted what it
faulted.

## How a graphic is drawn

- **In Blender**, from a scene the animator writes as `graphics/scenes/<shot id>.py`. The scene imports
  [storyblender.py](storyblender.py), which gives it its shot's clock, the film's look and its stills.
  `render` runs Blender headless on it: EEVEE, a clear film and the Standard view, so a style colour comes
  out as the style writes it. The scene lists in `DRAWS` every word and number it draws, with the places
  in `facts.md` that hold it. `render` checks each number against those places and checks that something
  new shows at each beat. Blender renders the shot's frames and no others.
- **Blender, not Remotion.** The owner asked for graphics of production grade, which set Manim aside, and
  chose Blender by eye on 2026-09-28. Each tool drew one of round 49's charts in round 48's look, over
  round 48's footage. Remotion rendered ten times faster and drew the same layout; Blender looked better. The two differed only where see-through layers overlap, which
  Blender blends in linear light. That test lived in `scratch/bakeoff/`. The same day the owner kept
  Blender over drawing the graphics as Fusion compositions inside Resolve: a graphic is made and checked
  without Resolve open, and other users can have Blender without buying Resolve.
- **In round 48's look**, which the owner liked and which [graphics-style.json](graphics-style.json)
  writes down. IBM Plex Sans is for words and Plex Mono for numbers, in the theatre interface's inks. There
  is no hue but a guild's and no gridlines. A chart sits on a translucent plate at round 48's place, and a
  full card dims the picture under it. [look/](look/) holds four of round 48's frames to compare with.
- **Beside the footage, never over the event.** Every shot carries footage, and a graphic is a layer over
  its own shot's footage: the footage is the star and shows the event, and the graphic carries the message
  beside it (the owner's ruling of 2026-09-28). So over footage a graphic stands on the corner card and
  never on the dim, and `render` fails one that covers more than a third of the frame. The full card and
  the dim are for a graphic that stands alone, over a flat colour: the film's opening or ending, or a
  drawing that works only on a plain ground, as the shot list decides.
- **Transparent.** The edit lays each graphic over its own shot's footage, filmed for it. Round 48's
  cards stood on the world the same way. Resolve reads a QuickTime Animation
  clip's alpha as it comes: the owner laid the test's clip over footage on 2026-09-28, and the card showed
  with the footage through it. A chroma key could not do this: most of a graphic is partly see-through.
- **The tool draws the words on screen.** `words` sets them in Plex Sans at the lower left, clear of both
  cards and of the subtitles, fading in and out.
- **The stills are the theatre's.** `stills` launches `TheatreSoloStill`
  (`unity/Assets/Theatre/Editor/TheatreSoloStill.cs`) once on the render worker. It grows each body from
  its row in the run's snapshot nearest the shot's second. The run's own development limits and the row's
  plan grow it, so the still is the body the run carried. It draws it from the close view's bearing, with
  the close view's back light, on a clear background. It draws without the grade, because the render
  pipeline drops a camera's alpha under post-processing. So a still has no tonemapping, bloom or
  vignette, and its colours are the skin's own, a little flatter than the film's.

**The limits:**

- `TheatreSoloStill` has been compiled against worker 6's assemblies and never run. Its first launch is
  its test, and the stills sheet shows whether the background is clear and the body lit.
- The render worker must carry the theatre of `unity/Assets/Theatre`, as for filming. It is refreshed from
  PowerShell with `./scripts/new-worker.ps1 -Workers <n>`.
- Blender sizes a font by its bounding box, not by its em, and `storyblender.py` converts. In the test the
  row labels measured 102 pixels where round 48's measured 103.
- At eight threads, `render` took 41 seconds for a graphic of 226 frames at 1920x1080 and 105 seconds at
  3840x2160, checks and clip included. The 4K clip, with its 10 s handles, is 41 MB.
- Blender's dither is off. Its faint noise broke the clip's runs of equal pixels, and the same 4K clip was
  920 MB with it on. PNG and ProRes 4444 were larger than QuickTime Animation on the same frames (82 and
  281 MB).

## The settings

`graphics-settings.json`, beside this file, holds the owner's defaults. The tool copies it and the style
into the film's folder the first time, and from then on reads the copies.

| Setting | Default | What it is |
|---|---|---|
| `handles_s` | 3 | The seconds a graphic's first and last frames are held before and after it, as for the clips (the owner, 2026-09-28: 3 s in place of 10 s). |
| `fps` | 30 | Frames a second. |
| `size` | `3840x2160` | The clips' frame, the same as the footage's: 4K, as the edit is (the owner's ruling of 2026-09-28). |
| `draft_size` | `960x540` | The frame the animators draw at and the reviewer reads, 16:9. `finish` renders every drawing at `size` once the reviewer passes it (the owner, 2026-09-28: "render a low res version for the reviewer, and only after approving do we render the high quality"). A 4K render took about 2 minutes, and a graphic takes up to three. Leave it out, or set it to `size`, to draw at full size from the start. |
| `codec` | `qtrle` | QuickTime Animation with alpha, which Resolve reads; `prores4444` is the other. |
| `blender` | `C:/Program Files/Blender Foundation/Blender 5.1/blender.exe` | The Blender the owner installed. |
| `blender_version` | `5.1.1` | The version `setup` asks for. |
| `render_threads` | 8 | The threads a render may take, a quarter of the machine. |
| `render_minutes` | 10 | How long one graphic's render may take before it is stopped. |
| `fade_s` | 0.3 | How long a line of words takes to fade in and out. |
| `unity` | the project's Editor | The Unity Editor the stills are drawn with. |
| `worker` | 6 | The render worker, `unity-w<n>`. |
| `runs_root` | `runs` | Where the runs are, from the checkout's root. |
| `still_size` | 1600 | A still's side, in pixels. |
| `still_wall_minutes` | 30 | How long the stills' Editor may run before it stops itself. |
| `style` | `graphics-style.json` | The look. |

## graphics.json

```json
{"schema": "create-story-video/graphics/1", "shots_sha256": "<64 hex>",
 "graphics": {"1.3a": {"key": "<16 hex>", "file": "graphics/clips/1.3a.mov", "sha256": "<64 hex>",
   "frames": 826, "action_frames": 226, "handle_frames": 300, "fps": 30, "width": 1920, "height": 1080,
   "pix_fmt": "argb", "scene_sha256": "<64 hex>", "stills_sha256": {},
   "draws": [["168 to 186 s", "[facts.md:L77]"], ["100", "axis"]],
   "beats_seen": [{"t": 0.0, "change": 0.21}, {"t": 3.68, "change": 0.04}], "clear": [0.61, 0.61], "covered": [0.13, 0.13], "over": "footage",
   "sheet": "graphics/sheets/1.3a-sheet.png", "sheet_times_s": [0, 0.6, 3.68, 4.28, 7.5],
   "blender": "5.1.1", "rendered": "2026-09-28T10:00:00"}},
 "stills": {"r49-s2-11248-4914": {"key": "<16 hex>", "png": "graphics/stills/r49-s2-11248-4914.png",
   "sha256": "<64 hex>", "run_dir": "runs/r49-s2/…", "snapshot_s": 4900, "genome": "…", "drawn": "…"}},
 "words": {"1.3a-w1": {"key": "<16 hex>", "file": "graphics/words/1.3a-w1.mov", "sha256": "<64 hex>",
   "frames": 94, "pix_fmt": "argb", "card": "…", "drawn": "…"}}}
```

- A graphic's clip holds `handle_frames`, then its `action_frames`, then `handle_frames` again. The edit
  places the action at the shot's `from_s`.
- A record is current while its key, its file, its scene and its stills are what they were when it was
  drawn. A new shot list or new settings draw again only what they changed.
- `beats_seen` is the share of the frame that changed within 0.6 s of each beat. `clear` is the share of
  the middle and last frames under half opacity, and `covered` the share at an opacity of 16 in 255 or
  more. `over` is `footage` or `alone`, as the shot list has it.

## Working files

In the film's folder. Run k of the step is numbered from 1, going on from the highest there.

- `graphics/plan.json`: what the shot list asks for, with each item's key. `plan` writes it.
- `graphics/scenes/<shot id>.py`: the animator's scenes.
- `graphics/work/`: Blender's frames and log for each graphic, the word cards' frames, the stills' list and
  the theatre's `result.json`. The tool clears each part before it draws it again.
- `graphics-<k>-plan.txt`, `graphics-<k>-setup.txt`, `graphics-<k>-stills.txt`, `graphics-<k>-words.txt`,
  `graphics-<k>-check.txt`: what the tool printed for the main session.
- `graphics-<k>-render-<shot id>-<m>.txt`: what `render` printed for the animator, its m-th render.
- `graphics-<k>-animator-<shot id>.md`: the animator's answer, saved word for word.
- `graphics-<k>-review.md`: the reviewer's answer, saved word for word.
- `graphics-<k>-owner.md`: the owner's words on the render and the faults, saved word for word.

## Who does what

| Who | Brief | Reads | Writes |
|---|---|---|---|
| the tool | [graphics.py](graphics.py) | `shots.json`, `facts.md`, `script.md`, the settings and style, the run's snapshots, the scenes | the plan, the clips, the stills, the sheets and `graphics.json` |
| the animator | [animator-brief.md](animator-brief.md) | its graphic's plan, `facts.md`, the style, [storyblender.py](storyblender.py), [look/](look/), its last printout and any review of it | its scene and the render printouts, through the shell, and its answer |
| the reviewer | [graphics-reviewer-brief.md](graphics-reviewer-brief.md) | the plan, `graphics.json`, `facts.md`, the sheets and [look/](look/) | nothing but its answer |
| the main session | this file | what the tool prints, the answers, the owner's words | those, saved |
| the owner | | the plan's counts, the sheets and the faults | the go on the stills' render, and what to do about a fault |

## The tool

`python <checkout>\.claude\skills\create-story-video\graphics.py <command> …`. Every command prints its
verdict on its first line and how long it took on its last.

- `plan <folder>`: `READY` with a line an item, each current or not, or `NOTHING TO DRAW`.
- `setup <folder>`: `READY` when Blender is where the settings say, at their version. `WRONG VERSION` or
  `MISSING` otherwise.
- `stills <folder>`: draws every still not current, in one Editor launch, and checks each one. It is square
  and transparent, and the body lies inside it clear of the edge. `PASS`, `FAIL` or `REFUSED`. It refuses
  on a busy machine or a held, stale or entry-less render worker, and it waits for the Editor, so it runs in
  the background.
- `words <folder>`: draws every line of words not current. `PASS`, or `FAIL` on words too long to fit.
- `render <folder> <shot id>`: renders one graphic's scene. `PASS` when its labels hold their places, it
  lasts its shot, each beat shows, and, over footage, it covers at most a third of the frame. Otherwise `FAIL`, with the scene's or
  Blender's own error when it failed. It `REFUSED`s a graphic with no scene, or whose stills are not drawn.
- `check <folder>`: `PASS` when every graphic, still and word card the shot list asks for is drawn and
  current, and every label still holds its places. A `drafts:` line names each one drawn at the draft size.
- `finish <folder>`: renders every graphic and word card drawn at the draft size again at `size`, from the
  same scene and with the same checks. `PASS` when none is left at the draft size. It `REFUSED`s while any
  item is not drawn and current. It takes about 2 minutes a graphic, so it runs in the background.

## Launching the animator and the reviewer

Each is launched with its prompt, word for word. Only the parts in angle brackets are filled in:
`<checkout>` and `<folder>` as absolute paths, `<k>` the step's run and `<id>` the graphic's shot id.

```text
You are the animator of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\animator-brief.md. Read it whole and follow it.
The film's folder is <folder>.
You draw graphic <id>, in run <k> of the graphics step.
Write only inside the film's folder, and only through the shell. Never write in your session scratchpad or in TEMP.
Your time budget is 25 minutes.
```

```text
You are the graphics reviewer of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\graphics-reviewer-brief.md. Read it whole and follow it.
The film's folder is <folder>.
You review run <k> of the graphics step.
Write nothing. Your answer is your whole report.
Your time budget is 15 minutes.
```

The animator is a general-purpose agent on opus. The reviewer is an `Explore` agent on opus, which is given
nothing of the project but its brief.

## The procedure

1. If the film's folder has no `shots.json`, stop: the shot list step writes it first.
2. Run `plan <folder>` and save what it prints in `graphics-<k>-plan.txt`. On `NOTHING TO DRAW`, go to
   step 7.
3. Run `setup <folder>` and save what it prints in `graphics-<k>-setup.txt`. On `BLENDER: READY`, go on.
   Otherwise tell the owner you are blocked on Blender, give them the lines it printed, and stop. Blender
   is the owner's to install or update.
4. If the plan has stills to draw, ask the owner for their go on the render, since they are asked before
   any Unity render (their standing rule). Say how many stills, and that it is one Editor launch on the
   render worker. Save their words in `graphics-<k>-owner.md`. On a yes, run `stills <folder>` in the
   background and save what it prints in `graphics-<k>-stills.txt`. On `STILLS: FAIL` or `REFUSED`, show
   the owner the lines and the full path of the stills sheet, as plain text, and stop.
5. Run `words <folder>` and save what it prints in `graphics-<k>-words.txt`. On `WORDS: FAIL`, the words it
   names are too long for the frame, and go back to the shot list step.
6. For each graphic the plan lists as not current, launch the animator for it, two at a time while the
   machine's Utility counter reads under half. At the draft size a render is short, and `render` re-reads
   `graphics.json` just before it writes, so two renders keep each other's records (round 49 drew its first
   22 one at a time, at 4K, in about four hours). Save each answer in `graphics-<k>-animator-<id>.md`, word
   for word.
   - `GRAPHIC: RENDERED`: go on to the next.
   - `GRAPHIC: NOT RENDERED`, or anything else: launch it once more, since it reads its own last printout.
     After its second answer, show the owner both answers and ask what to do.
   An animator that has written nothing for twenty minutes, with no Blender running, has stopped: an
   interrupted turn of the main session ends its agents without an answer (round 49's 4.2a, 2026-09-28). Record
   that it gave none, in `graphics-<k>-animator-<id>-lost.txt`, and launch it again; it reads its last printout.
7. Run `check <folder>` and save what it prints in `graphics-<k>-check.txt`. On `GRAPHICS: FAIL`, the items
   it names go back to step 4, 5 or 6, once. After that the owner rules.
8. Launch the reviewer for run k. Save its answer in `graphics-<k>-review.md`, word for word.
   - `REVIEW: OK`: go to step 9.
   - `REVIEW: FAULTS`: show the owner each fault with the full path of its sheet, as plain text, and ask
     what to do. Save their words in `graphics-<k>-owner.md`. A graphic they want changed is drawn in run
     k+1: its animator reads the review and their words. A fault in the shot list's own words or beats goes
     back to the shot list step. A fault they accept leaves the graphic as it is. When no graphic is left
     to draw again, go to step 9.
9. Run `finish <folder>` in the background and save what it prints in `graphics-<k>-finish.txt`. On
   `FINISH: PASS`, run `check <folder>` again and save it in `graphics-<k>-check-final.txt`; the step is done.
   On `FINISH: FAIL`, show the owner the lines. A rough draft of the edit may carry drafts; the final edit
   refuses them.

Tell the owner when an answer says it stopped at its time budget.

## Before the first film

The step has not run yet. Round 49 is its first film. Before it, the owner gives the go on the first stills'
render. After it:

- The owner looks at the stills sheet, the first launch of `TheatreSoloStill`.
- The owner compares the first graphic's sheet with [look/](look/) by eye, for the size of its words.
- This file records the pace.
