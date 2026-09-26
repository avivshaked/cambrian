# A round's story film: the procedure

*Written 2026-09-25, after round 48's film (`scratch/owner/round-48-story.mp4`, 22 scenes and a
title in 8 min 47 s). The owner asked for the process to be repeatable. This is the procedure; the
writer's rules are in [`story-writer-brief.md`](story-writer-brief.md) and a film's words in
[`story-glossary.md`](story-glossary.md). The skill `.claude/skills/story-film/` runs it. Revised the same
evening, when the owner asked for one flow from the runs to the timeline: the arc and the script
stages, a narration stage, three owner reviews, a Resolve timeline and captions that stay
editable after filming (section 0). The flow before that revision is tagged `story-film-v2`.*

A story film tells one round across all its seeds. A writer reads the runs and writes a shot list;
the safari's director films each scene; one script joins the clips. A scene is filmed one of two
ways. Stepped live in the Editor from the nearest checkpoint, every frame is a cousin. A birth on
screen then need not be the recorded one, and the captions say "in the run" where that matters.
Played from a window the farm recorded (section 5's second half), nothing is stepped in the Editor.
The scene then carries its window's verdict, which reads FAITHFUL when the farm's replay agreed
with the run's own rows.

## 0. The flow

Any session that runs this flow has to reach the same film (the owner, 2026-09-26), so no step is
left to the session's judgment. Every verdict comes from a tool, from a subagent launched with a
fixed prompt (`.claude/skills/story-film/prompts.md`) whose answer is saved word for word from its
transcript, or from the owner's words saved as they wrote them. `flow.json` records each verdict
with a hash of what it was about, and every loop has a limit after which the owner rules.
Commit `4115d2f` records the review that found the steps a session used to decide.

| stage | what happens | it leaves | whose |
|---|---|---|---|
| open | the story's folder; the entry and pre-registration committed; every run ended | `flow.json` | agent |
| guides | a guide for each run (section 2) | `guide/guide.json` per run | agent |
| arc | the writer's first pass (section 3) | `arc.md`, `arc-reads.md`, `arc-choices.md` | writer |
| arc approved | the owner rules on the arc and its choices | `owner/arc-K-<verdict>.md`, an approval | owner |
| write | the writer's second pass (section 3) | `story.md`, `story.json`, `checks.tsv`, `make_story.py`, `writer-report.md`, `glossary-rows.md` | writer |
| draft | the tool's check, the listener and the fact checker (section 3) | `drafts/K/` | tool, listener, fact checker |
| script | rounds of edit, check and reading (section 3a) | `script.md`, `script/N/`, `story.draft.json`, `edits.tsv` | editor, tool, readers |
| script approved | the owner reads the script | `owner/script-K-<verdict>.md`, an approval | owner |
| narrate | the narration of the approved words, which sets each scene's length (section 3b) | `narration/` | agent, service |
| check | the director's check per run (section 4) | a mark per run, with its log | agent |
| film | the scenes filmed (section 5) | `story.filmed.json`, the clips | agent |
| assemble | the Resolve timeline, or the ffmpeg film (section 6) | `resolve/plan.json`, `build.json` | agent |
| timeline approved | the owner watches the timeline | `owner/timeline-K-<verdict>.md`, an approval | owner |
| glossary | the writer's new rows appended to the glossary | a mark | agent |
| deliver | the render in `scratch/owner/` | a mark with the film's hash | agent |

`python scripts/story-flow.py next scratch/story/<round>` walks this table, does each mechanical
step itself and prints what comes next: a subagent and its prompt, a command, or the owner's
ruling. `status` prints the same without changing anything. `save <folder> <agent id>` records a
reader's answer, and `owner <folder> <stage> <file> --verdict <v>` the owner's. An approval records
a hash of what the owner saw, so an edit after it reads as stale. The film stage names every scene
whose filmed fields (station, second, subject, chapter, chart) changed, or which now runs longer
than its clip; a caption is not one of them. The script is approved before anything is narrated,
and its approval covers the words and the scenes and not their timing, which the narration sets
afterwards.

## 1. Before starting

- Every seed of the round has ended. The guide reads a whole run, and seed 3 of round 48 had no
  guide until its run ended.
- The machine's current load ruling is in HANDOFF. Under the owner's option B of 2026-09-25,
  rendering is the one heavy job and nothing heavy runs beside it.
- The story's working folder is `scratch/story/<round>/`.

## 2. A guide for each seed

```powershell
python scripts/guide.py r48-s1 --no-economics
```

This writes `guide/guide.json` and `guide.md` beside the run: every clade as a card, with its
made-up name, its founder, its guild and its ranked facts. `--no-economics` skips the ledger pass,
which the story does not need. The director places a story's subjects from the guide when it
exists and from the lineage when it does not.

## 3. The writer

The writer is a `general-purpose` subagent on Opus, launched with the prompt `next` prints and
nothing else; its rules are [`story-writer-brief.md`](story-writer-brief.md), and it finds the
round's entry and pre-registration through `flow.json`. It reads the runs through the shell and
writes its own files into the story's folder the same way, never with the Write or Edit tools, and
runs nothing heavy. It runs in two passes. The first writes `arc.md`, `arc-reads.md` and
`arc-choices.md`, and the owner rules on them before a line of narration is written. The second
writes the story from the arc as ruled.

Every draft the second pass hands back is reviewed before anyone reads it. `story-script.py check`
counts what can be counted of the narration rules, and refuses any number whose words stand in no
`shown` cell of `checks.tsv`. The listener ([`story-listener-brief.md`](story-listener-brief.md))
hears the page against the writer's own rules and names each fault; the fact checker
([`story-facts-brief.md`](story-facts-brief.md)) asks of every `checks.tsv` row whether the words
are true of the value. A draft passes with no ERROR, the listener's PASS and no row false. A draft
that fails goes back to the writer, which reads its `drafts/K/`; three failures in a row go to the
owner, who passes the draft or returns it with their words.

Round 48's second film's readers are in [`story-r48-v2/`](story-r48-v2/) (`runlib.py`, `facts.py`,
`extras.py`, `make_story.py`), a model for reading the runs and building the shot list.

The fields the director reads are in `unity/Assets/Theatre/SafariStory.cs`. Each scene carries:

- a number `n`, a `run`, a `station` and a `second`;
- a `subject`: `world`, `founder body N`, `body N` or `guide clade N`;
- `seconds` on screen, and `captions` as `{at, text}`;
- optionally `chapter`, which plays an 8 s chapter card first, and `flexible: false`, which holds
  the scene at its second.

The stations are Arrival, Descent, Portrait, Floor, Birth, Colony, Time and Card. A Card, and
the 8 s chapter card, is a slow drift through the crowd at its second, made to be talked over.
A `full` chart may sit on any station but a Portrait or a Birth, whose body it would cover.

## 3a. The script

The writer's narration is true and complete; the script stage makes it sound like a person telling
it. The skill `.claude/skills/story-script/` says what each round does, the editor's rules are
[`story-script-brief.md`](story-script-brief.md), and the tool is `scripts/story-script.py`.
`render` shows `story.json` as `script.md`, one narration paragraph a line, and `apply` reads it
back, cutting each paragraph into subtitles with `split_cues`, re-timing what changed, keeping the
writer's version as `story.draft.json` and logging every change with its round and author in
`edits.tsv`. In each round the editor edits the page, the tool checks it, a fact checker judges
every edited line and a cold reader who sees only the page says where it stumbled and tells the
story back, and an arc comparer says whether that summary holds the arc. A clean round, the third
round or an owner's round closes the stage. The session never edits the page.

## 3b. The narration

The narration service is being written. The stage, what it needs from the service and this
repository's side of it (`scripts/story-narration.py`, built and tested on a synthetic narration)
are [`story-narration.md`](story-narration.md). Once the owner has approved the script, each
scene's paragraphs are spoken before filming. Their lengths, with a fixed padding, set each scene's length, and the captions are
timed to the speech. Until the service exists the stage is skipped
(`story-flow.py skip <folder> narrate`), and the captions keep the writer's timing.

## 4. The check

```powershell
./scripts/theatre-safari.ps1 r48-s1 -Story scratch/story/r48/story.json -Check -Worker 5 `
    -RunsRoot D:\Projects\experiments\evolution-simulator\runs
```

`-Check` runs the director over the story's scenes for one seed. It takes a frame every two
seconds, asserts the camera is above the bed, outside every body and under the speed ceiling, and
prints one verdict line. The Editor's log prints every field the story reader could not map
(`[Theatre] safari story:`). Run it for each seed with scenes in the story.

## 5. The render

Render one seed at a time, in a chain started detached so that it survives the session
(`logbook/specs/story-render-chains/render-chain-1.ps1` and `render-chain-2.ps1`, round 48's first
film's, are the pattern):

```powershell
./scripts/theatre-safari.ps1 r48-s1 -Story <story.json> -Worker 5 -RunsRoot <main tree>\runs `
    -Folder story-final -DeleteFrames -WallMinutes 300
```

- **`-Scenes`** names the story's own numbers, to film part of a seed.
- **No text in the frames.** A story's frames carry neither captions nor the label
  (`EVOSIM_THEATRE_STORY_BURN_TEXT` unset); the join sets both. `-BurnText` stamps the old
  5×7 bitmap text into the frames, the first film's look, and `-NoBurnText` forces it off.
- **The output:** each clip is `scratch/safari/<arm>/story-final/story-NN-<arm>-<station>-<subject>.mp4`,
  with a contact sheet.
- **Run from a worktree,** the script writes under that worktree's `scratch/` and needs `-RunsRoot`.
- **Pace:** round 48's 22 scenes took 1 h 47 min on one worker.
- **Look before joining.** The caller opens each contact sheet: this is the "watch it in the theatre"
  of CLAUDE.md, and the round's logbook entry says what was seen.

### From the farm's film windows

Built on 2026-09-25 (B3 of [`record-and-film-spec.md`](record-and-film-spec.md)); round 49's film
windows ran on it on 2026-09-26. Three steps replace the render's one: plan, record, film.

```powershell
python scripts/story-windows.py <story.json> --out scratch/story-windows/<round> `
    --runs-root <main tree>\runs --runs r48-s1 --scenes 1,3
./scripts/story-windows.ps1 scratch/story-windows/<round> -Threads 10
./scripts/theatre-safari.ps1 r48-s1 -Story <story.json> -FromWindows scratch/story-windows/<round> `
    -Worker 5 -RunsRoot <main tree>\runs -Folder story-final -DeleteFrames
```

- **The plan** is `windows.json`: one window a scene, and two for a time scene. Each spans the
  scene's chapter card and takes as the director times them, and one second more.
- **A birth's window** is set on a birth the lineage holds. That is the story's child, else the
  named parent's child nearest the second, else a child of the clade's line.
- **The recording** runs the farm once a window, one window at a time, at a third of the machine
  by default. A window already recorded over the same span is skipped. Anything else in its
  folder is moved aside, never deleted.
- **The film.** Each scene opens its own window. A scene with no recorded window is skipped, and
  `scenes.tsv` says why; nothing is stepped in its place.
- **The word.** The corner shows the window's verdict: FAITHFUL, COUSIN or UNVERIFIED. A held scene
  past the run's last row reads UNVERIFIED. `scenes.tsv` carries the word a scene.
- **What a window lacks.** It holds no body's reserve, so a chart of one draws its births alone.

## 6. The join and the delivery

```powershell
python scripts/story-assemble.py scratch/story/r48/story.json scratch/owner/round-48-story.mp4 `
    <folder of seed 1's clips> <folder of seed 2's clips> <folder of seed 3's clips>
```

The script sets the captions and the provenance label as subtitles in IBM Plex, from each
folder's `captions.tsv`. It writes `<film>.ass` and the film without subtitles,
`<film>.clean.mp4`, beside the film. A caption fixed in the `.ass` is re-burned without a
render: `story-assemble.py --reburn <film>.clean.mp4 <film>.ass <out>.mp4`. Its options are
`--subtitles auto|burn|file|off`, `--no-label` and `--threads` (four by default, under the
owner's half-machine ruling). The script opens on the story's title, joins the clips in story
order, marks every missing scene as skipped, and writes `<film>.scenes.tsv` beside the film.
Its table gives each scene's provenance word. A film whose scenes carry more than one word says
so in a note and a last line.

**A caption changed after filming** needs no new filming. `--captions story` takes each scene's captions from
`story.json` as it stands, placed as the director places them, over the clips already filmed.
The director's own lines (a story's birth adds one) are told apart by `story.filmed.json`, the
story as it was filmed, which the film stage copies before rendering. The table counts the
captions changed since filming and names any caption a clip is too short to hold. Without that
copy, a filmed line in a slot the story leaves free is taken for the director's, which keeps a
deleted caption; so the copy is made every time.

**The Resolve timeline** is how the flow usually assembles a film (the `story-resolve` skill and
[`story-resolve.md`](story-resolve.md)). `scripts/story-resolve.py` plans with the assembler
(`--captions story` by default), then builds a new timeline `<name> vK` in the configured
project, reusing every clip already in its bin; a caption edit is a plan and a build, and the
captions' SRT carries a hash of its cues in its name so that Resolve cannot reuse an old one. The
ffmpeg film above is the fallback where Resolve Studio is not available, and a check against it.

To deliver, copy `story.md` beside the film in `scratch/owner/` and give the owner the full path as
plain text. Neither file links nor attachment cards reach the owner.

## 7. What the owner asked for next (2026-09-25)

After the first film the owner asked for four things:

- every term explained before it is used;
- charts in the film;
- a brighter picture;
- a real story shape: hope, setback, turn and ending; triumph, tragedy or bittersweet.

The first and fourth belong to the writer's brief. The second and third are theatre code: the
`chart` field in `story.json` and a story look with an exposure meter. The subtitles and the
moving Cards answer the owner's second look at the first film ("why do the subtitles look so
bad?", "must it be on a black screen?"). All of it was written on 2026-09-25 on the safari
branch (`fc236a7`, `91d3b08`, `ac33d7a`, `868bd94`) and has not yet been seen in Unity. The
chart's form is in the writer's brief. The look is on for stories only:
`EVOSIM_THEATRE_STORY_LOOK` unset means on for a story, `1` means on for any safari and `0` off.
Its dials are `EVOSIM_THEATRE_STORY_DEEP`, `_SHALLOW`, `_AMBIENT`, `_FOG`, `_REACH`,
`_VIGNETTE`, `_LAMP`, `_LUMA`, `_LUMA_DEPTH`, `_EV_MIN` and `_EV_MAX`.
`theatre-safari.ps1 -NoStoryLook` films the old look for a comparison. Round 48's second story, written to the new brief, is
[`story-r48-v2/`](story-r48-v2/): 20 scenes, 7 chapters and 15 charts in 9 min 39 s.
