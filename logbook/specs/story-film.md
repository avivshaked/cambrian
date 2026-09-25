# A round's story film: the procedure

*Written 2026-09-25, after round 48's film (`scratch/owner/round-48-story.mp4`, 22 scenes and a
title in 8 min 47 s). The owner asked for the process to be repeatable. This is the procedure; the
writer's rules are in [`story-writer-brief.md`](story-writer-brief.md) and a film's words in
[`story-glossary.md`](story-glossary.md). A skill under `.claude/skills/story-film/` will point here. It waits for the
owner, because every write under `.claude/` asks them.*

A story film tells one round across all its seeds. A writer reads the runs and writes a shot list;
the safari's director films each scene from the runs' checkpoints; one script joins the clips. Every
frame is a cousin, because the Editor steps each scene live from the nearest checkpoint. A birth on
screen need not be the recorded one, and the captions say "in the run" where that matters.

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

The writer is an Opus subagent. Its brief is `story-writer-brief.md` together with the round's
logbook entry and pre-registration. It is told the folder it may write to
(`scratch/story/<round>/`), that the runs are read-only and that nothing heavy runs. It returns
three files:

- `story.md`, the prose for the owner;
- `story.json`, the shot list the director films;
- `checks.tsv`, every number on screen with the file and query it came from.

The scripts that produced the numbers of round 48's second film are in
[`story-r48-v2/`](story-r48-v2/) (`runlib.py`, `facts.py`, `extras.py`, `make_story.py`), beside the
story, shot list and checks they wrote. A new writer copies them and starts from them. The first
film's scripts, which they replace, stayed in `scratch/story/r48/`.

The fields the director reads are in `unity/Assets/Theatre/SafariStory.cs`. Each scene carries:

- a number `n`, a `run`, a `station` and a `second`;
- a `subject`: `world`, `founder body N`, `body N` or `guide clade N`;
- `seconds` on screen, and `captions` as `{at, text}`;
- optionally `chapter`, which plays an 8 s chapter card first, and `flexible: false`, which holds
  the scene at its second.

The stations are Arrival, Descent, Portrait, Floor, Birth, Colony, Time and Card.

Before anything is filmed, the caller reads `story.md` against `checks.tsv`. A number with no row
there is not filmed.

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
- **The output:** each clip is `scratch/safari/<arm>/story-final/story-NN-<arm>-<station>-<subject>.mp4`,
  with a contact sheet.
- **Run from a worktree,** the script writes under that worktree's `scratch/` and needs `-RunsRoot`.
- **Pace:** round 48's 22 scenes took 1 h 47 min on one worker.
- **Look before joining.** The caller opens each contact sheet: this is the "watch it in the theatre"
  of CLAUDE.md, and the round's logbook entry says what was seen.

## 6. The join and the delivery

```powershell
python scripts/story-assemble.py scratch/story/r48/story.json scratch/owner/round-48-story.mp4 `
    <folder of seed 1's clips> <folder of seed 2's clips> <folder of seed 3's clips>
```

The script opens on the story's title, joins the clips in story order, marks every missing scene
as skipped, and writes `<film>.scenes.tsv` beside the film.

To deliver, copy `story.md` beside the film in `scratch/owner/` and give the owner the full path as
plain text. Neither file links nor attachment cards reach the owner.

## 7. What the owner asked for next (2026-09-25)

After the first film the owner asked for four things:

- every term explained before it is used;
- charts in the film;
- a brighter picture;
- a real story shape: hope, setback, turn and ending; triumph, tragedy or bittersweet.

The first and fourth belong to the writer's brief. The second and third are theatre code: the
`chart` field in `story.json` and a story look with an exposure meter. Both were written on
2026-09-25 on the safari branch (`fc236a7`, `91d3b08`, `ac33d7a`) and have not yet been seen in
Unity. The chart's form is in the writer's brief. The look is on for stories only. `EVOSIM_THEATRE_STORY_LOOK` unset
means on for a story, `1` means on for any safari and `0` off. Its dials are `EVOSIM_THEATRE_STORY_DEEP`, `_SHALLOW`, `_AMBIENT`, `_FOG`, `_REACH`, `_VIGNETTE`,
`_LAMP`, `_LUMA`, `_LUMA_DEPTH`, `_EV_MIN` and `_EV_MAX`. `theatre-safari.ps1 -NoStoryLook` films
the old look for a comparison. Round 48's second story, written to the new brief, is
[`story-r48-v2/`](story-r48-v2/): 20 scenes, 7 chapters and 15 charts in 9 min 39 s.
