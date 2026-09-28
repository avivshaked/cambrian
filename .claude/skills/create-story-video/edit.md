# edit

Composes the film. Every clip, graphic, line of words, piece of narration and subtitle goes onto one
timeline at the place the shot list and the narration give it. The editor and the owner watch a proof of
that timeline before it is built in DaVinci Resolve, where the owner finishes the film.

## Deliverable

- `edit/plan-<n>.json` in the film's folder: draft n of the timeline, every item at its frame.
- `edit/proof-<n>.mp4` and `edit/proof-<n>-sheet.png`: a small film of draft n made by ffmpeg, with its
  sound and burned subtitles, and one frame from the middle of each shot. It is the draft the owner watches.
- In Resolve, a bin and a timeline named `<name> vK` under the configured bin, K the first number free:

| Track | Holds |
|---|---|
| V1 Picture | the footage of every shot, a graphic's included, and a plate under a graphic that stands alone |
| V2 Label | the provenance label over each piece of footage, linked to it |
| V3 Graphics | the graphics, transparent |
| V4 Words | the words on screen, transparent |
| ST1 Subtitles | the narration's sentences, as Resolve subtitles |
| A1 Music | the music the machine's settings name, faded at the film's end |
| A2 Narration | the narration, a clip a piece |

A blue marker stands at every passage, named by its id, with its first sentence as its note.

The step is done when the build passed and the owner approves the timeline. Dissolves, the mix and the
render are the owner's, in Resolve.

## How the film is laid out

- **Time is the narration's.** Every shot starts at its `from_s`, where 0 is the narration's start.
- **Footage enters at its action.** Every shot's clip is taken from its `action_in_s`. Its handles stay in
  the clip, so the owner can move a cut or make a dissolve in Resolve.
- **Under a graphic** lies its own shot's footage, filmed for it: every shot carries footage, and the
  footage is the star (the owner's ruling of 2026-09-28). Only under a graphic that stands alone does a
  plate of the settings' colour fill the picture.
- **Each clip is graded** towards the reference film's tone (the owner's choice on round 49's proposal
  sheet, 2026-09-28: "the right column looks better"). The grade stretches the clip's contrast about its own
  mean colour, so its colour stays where it was. The stretch is the reference's tone spread over the clip's,
  at least 1 and at most 1.6, then a power of 0.9 lifts the shadows. Both numbers come from `collect`'s
  `tone` and `rgb`. The plan writes each clip's grade as an ASC CDL. The proof applies it with ffmpeg, and
  the build sets it on node 1 of each piece of footage with `SetCDL`. `grade: null` leaves the clips as
  filmed.
- **A cut between two shots** stays where the shot list put it, unless the editor moves it, by at most
  two seconds and within both clips' handles.
- **The provenance label** ticks the run's second over its footage, as round 48's did: "FAITHFUL · r49-s2 ·
  4,914 s". It is drawn by ffmpeg in IBM Plex Mono. `label: false` leaves it out.
- **The subtitles** are the sentences as spoken, each at its own times from `narration.json`. A name we
  give shows without its braces. A sentence too long for one cue is cut into cues of at most two lines of 42
  characters, each timed in proportion to its characters. In Resolve they are subtitles, burned at the
  render or exported beside the film.
- **The narration pieces** sit at their frames. The frame rounds a piece's start by at most 17 ms, and the
  proof places them to the millisecond.

**What the build can and cannot do**, from what the Resolve API was found to do
(`logbook/specs/story-resolve.md`):

- The subtitle file goes onto the empty timeline first, because Resolve puts a subtitle file at the
  timeline's end.
- The subtitles take Resolve's default style, since the API cannot set it.
- The build makes no dissolves, because the API has no call for a transition.
- No earlier build placed a clip from a frame other than its first. This one reads every item back and
  fails on any that did not land where the plan put it.
- The grade's `SetCDL` has not yet run on a real build. The first build is the check that Resolve's CDL
  and the proof's ffmpeg grade look alike, and a mismatch is read from a frame of each.
- The build never opens, changes or deletes an existing timeline.
- Resolve must be running, with Preferences, System, General, External scripting using set to Local, and
  the configured project open.
- The clips and graphics are 1920 by 1080 on a 3840 by 2160 timeline, and Resolve scales them to fit.

**The channel intro, round 49 only.** The owner's intro card (`assets/cards/cambrian-intro-v1-4k-prores422hq.mov`,
8 s, no sound, in Resolve's Master/Channel Branding bin) goes straight after the hook, the story's first act.
Round 49's script was approved before the card existed, so its edit settings carry it: `intro` names the file
and the act it follows. The plan lays it on V1 after that act's last shot and moves everything after it later by
the card's length. From the next film the script places the intro and the outro instead, as passages of their
own (the owner, 2026-09-28: "the intro (and later the outro) should be part of the script file, rather than
hardcoded into the software"), and the shot list carries them like any other shot. `intro` stays null then.

## The settings

`edit-settings.json`, beside this file, holds the owner's defaults, which the tool copies into the film's
folder the first time. The machine's own settings are story-resolve.py's, in `.env` at the checkout's root:
the project, the bin, the timeline's size and rate, the music and its fade.

| Setting | Default | What it is |
|---|---|---|
| `name` | `""` | The bin's and timeline's name before its `vK`; empty takes the film folder's name without its stamp. |
| `label` | `true` | The provenance label over the footage. |
| `subtitle_chars` | 42 | The longest subtitle line. |
| `subtitle_lines` | 2 | Lines in one subtitle. |
| `plate` | `#061018` | The colour where no footage reaches. |
| `proof_size` | `960x540` | The proof's frame. |
| `threads` | 8 | ffmpeg's threads, a quarter of the machine. |
| `intro` | `null` | Round 49 only: `{"file": <path>, "after_act": 1}`, the channel intro laid after that act. From the next film the script places it. |
| `grade` | `{"most": 1.6, "power": 0.9}` | Each clip's grade: its contrast stretched towards the reference's tone by at most `most`, then `power`. `null` leaves the clips as filmed. |
| `rough` | `false` | `true` lays out a rough draft before every clip and graphic is in, so the owner can watch and listen early and a reshoot is found before the graphics are done (the owner, 2026-09-28). The plate holds a missing clip's time, a missing graphic or word card is left off, the plan's verdict is `EDIT PLAN: ROUGH` with a `rough:` line for each item, and the proof and the build repeat those lines. A rough build's bin and timeline are named `<name> vK rough`. A plan records its sources' checksums, so a graphic finished between the plan and the build refuses the build: plan again. Set it back to `false` for the finished draft. |

## The editor's decisions

`edit-<n>.json` in the film's folder, read by `plan <folder> <n>`:

```json
{"schema": "create-story-video/edit-decisions/1",
 "cuts": [{"after": "2.1b", "move_s": -0.8}]}
```

- `cuts` names the shot before a cut between two shots with footage, and moves the cut by `move_s` seconds,
  later when positive. The move is at most two seconds, and within the handles.

## Working files

In the film's folder. Draft n is numbered from 1, going on from the highest there.

- `edit/`: the tool's own. It holds `plan-<n>.json`, the subtitles, `labels/`, `plates/`, `proof-<n>/`
  with the proof's parts, `proof-<n>.mp4` and its sheet, and `build-<n>-v<K>.json`, the build's report.
  The subtitles are `subtitles-<hash>.srt`, named by a hash of their cues, because Resolve reuses an
  imported file by its path.
- `edit-<n>.json`: the editor's decisions for draft n, when it made any.
- `edit-<n>-plan.txt`, `edit-<n>-proof.txt`, `edit-<n>-resolve.txt`, `edit-<n>-build.txt`: what the tool
  printed for the main session.
- `edit-<n>-editor.md`: the editor's answer on draft n, saved word for word.
- `edit-<n>-owner.md`: the owner's words on draft n's proof, on Resolve and on the build, saved word for word.

## Who does what

| Who | Brief | Reads | Writes |
|---|---|---|---|
| the tool | [edit.py](edit.py) | `narration.json`, `shots.json`, `clips.json`, `graphics.json`, `graphics/plan.json`, the settings, the decisions | the plan, the subtitles, the labels, the plates, the proof and the build |
| the editor | [editor-brief.md](editor-brief.md) | the plan, the proof's sheet, the shot list, the clips' and graphics' sheets | its decisions and the plan's printout, through the shell, and its answer |
| the main session | this file | what the tool prints, the answers, the owner's words | those, saved |
| the owner | | the proof, and the timeline in Resolve | what to change, the go on the build, and the approval |

## The tool

`python <checkout>\.claude\skills\create-story-video\edit.py <command> …`. Every command prints its verdict
on its first line and how long it took on its last.

- `plan <folder> <n>`: lays draft n out, with `edit-<n>.json` when there is one. It writes the subtitles,
  draws the labels and the plates, and prints a line for each piece of the picture. It answers `READY`,
  `FAIL` on a decision it cannot follow, or `REFUSED` when a step before the edit has not finished or was
  made for another shot list.
- `proof <folder> <n>`: makes draft n's proof and its sheet. `PASS`, or `FAIL` when the proof's length or
  sound is wrong, or `REFUSED` when a file the plan was made from has changed since. It takes minutes, so it
  runs in the background.
- `resolve <folder>`: `READY` when Resolve answers and the configured project is open, else `NOT READY`
  with why. It changes nothing.
- `build <folder> <n>`: builds draft n in Resolve as a new bin and timeline. `PASS` when every item landed
  where the plan put it, else `FAIL` with each that did not, or `REFUSED`. Resolve links each file where it
  lies, so a build shows what its files hold, not what they held when it was built. The filming and graphics
  steps never write over a file for that reason. A cut built from clips that were later filmed again still
  shows its own; the new clips reach Resolve only through a new plan and a new build.

## Launching the editor

It is launched with its prompt, word for word. Only the parts in angle brackets are filled in: `<checkout>`
and `<folder>` as absolute paths, and `<n>` the draft it reads.

```text
You are the editor of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\editor-brief.md. Read it whole and follow it.
The film's folder is <folder>.
You read draft <n>.
Write only inside the film's folder, and only through the shell. Never write in your session scratchpad or in TEMP.
Your time budget is 15 minutes.
```

The editor is a general-purpose agent on opus.

## The procedure

1. If the film's folder lacks `narration.json`, `shots.json`, `clips.json` or `graphics.json`, stop: the
   steps before the edit write them. Their reviews must have answered OK, or the owner accepted what they
   faulted. A rough draft (the `rough` setting) is the exception: it is laid out while the filming and
   graphics steps still run, for the owner to watch and listen before they end (the owner, 2026-09-28: "if
   something needs reshooting we know it before all the graphics are done"). The owner watches it in
   Resolve and not in a proof ("why not use resolve? we have to use it anyway"), so it skips steps 3 to 5
   and goes from its plan to steps 6 and 7; its `rough:` lines say what is not in yet. A new rough draft
   after a re-film plans the same draft again, since a build already made keeps the files it linked
   (keep the old `plan-<n>.json` beside it as `plan-<n>-v<K>.json`, K the build it made).
2. Run `plan <folder> <n>` and save what it prints in `edit-<n>-plan.txt`. On `FAIL` or `REFUSED`, show the
   owner the lines and stop.
3. Run `proof <folder> <n>` in the background and save what it prints in `edit-<n>-proof.txt`. On `FAIL` or
   `REFUSED`, show the owner the lines and stop. The proof has hung on 4K graphics: round 49's final draft
   (30 graphics and 42 word cards, qtrle at 3840x2160) left ffmpeg idle at the overlay pass for 36 minutes
   after the picture pass had finished, and it was stopped. Until that is fixed, a proof whose ffmpeg has
   used no CPU for five minutes has hung, and the owner checks the draft in Resolve.
4. Launch the editor on draft n. Save its answer in `edit-<n>-editor.md`, word for word.
   - `EDIT: READY`: go on.
   - `EDIT: DECISIONS`: it wrote `edit-<n+1>.json`, and its plan was ready. Go back to step 2 with draft
     n+1. After the third draft, show the owner the last proof and let them rule.
   - `EDIT: NOT READY`, or anything else: show the owner the answer and ask what to do.
5. Copy draft n's proof and sheet into `scratch/owner/`, give the owner their full paths as plain text, and
   ask them to watch it. Save their words in `edit-<n>-owner.md`. A change to a shot goes back to the shot
   list step. A change to where a cut falls goes to the editor, who reads the
   owner's words, at step 4. On their yes, go on.
6. Run `resolve <folder>` and save what it prints in `edit-<n>-resolve.txt`. On `NOT READY`, tell the owner
   you are blocked on Resolve. Give what it printed, ask them to open Resolve with scripting set to Local and
   the project open, and ask whether another agent is working in Resolve. Then run it again.
7. Ask the owner for their go on the build, and save their words. A rough draft built again after a
   re-film is covered by the go on the first rough build, and by any build the session told the owner it
   would make unless they objected; build it without asking again (the owner, 2026-09-28, when v4 waited on
   a second question: "didn't we say we're doing the 4th cut on resolve? what happened?"). Run `build <folder> <n>` and save what it
   prints in `edit-<n>-build.txt`.
   - `BUILD: PASS`: tell the owner the timeline's name. The step is done when they approve it.
   - `BUILD: FAIL`: show the owner every error. The timeline stays for them to look at or delete; never
     delete it yourself.

Tell the owner when an answer says it stopped at its time budget.

## Before the first film

The step has not run yet. The first build is its test in Resolve: the in-points, the subtitles, the
labels and the graphics' transparency. The owner renders one second of it (`logbook/specs/story-resolve.md`)
and compares it with the proof. The owner also rules on the clips' size, 1920 by 1080 on a 3840 by 2160
timeline.
