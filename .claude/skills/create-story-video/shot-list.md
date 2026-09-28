# shot list

Directs the film again, now that the narration has set its timing. Every passage's picture is placed on
the narration's timeline and cut into shots where the words ask for it. Every graphic's beats are timed to
the sentences they draw.

## Deliverable

`shots.json` in the film's folder, in the form [shots-schema.md](shots-schema.md) fixes. Filming takes
the footage of every shot, and the graphics step its graphics and the bodies they draw.

Every shot carries footage, and a graphic is a layer over its own shot's footage. The footage is the star:
it shows the event, and the graphic carries the message without hiding it. Only a graphic for the film's
opening or ending, or one that works only on a plain ground, stands alone over no footage (the owner's
ruling of 2026-09-28).

The shot list keeps the spoken words, the holds and the passage numbers, and keys on them
([script-schema.md](script-schema.md), "What comes later"). It may change a passage's picture and its
words on screen, and cut a passage into several shots.

The step is done when `shots.py check` passes a draft, which copies it to `shots.json`. The owner does not
approve the shot list (their ruling of 2026-09-27). They judge the film the shots make, at filming's
review and in the edit.

## What the shot-lister decides

- **Where the picture changes.** A passage is one shot unless its words ask for more: a passage whose
  sentences point at different things, or one long enough that a single picture would stall. A cut falls
  where a sentence starts or in a pause, and a shot lasts at least 2 seconds.
- **What each shot films.** The camera move, the bodies and the stretch of the run, for a run shot and
  under a graphic alike. Under a graphic it is the event the graphic speaks of. Footage plays at the speed
  of life, so its stretch is as long as the shot. The shot-lister chooses where in the script's
  seconds it lies, and filming adds the handles.
- **What each graphic shows, and when.** Its beats, each timed to the sentence that names it, and the real
  bodies it draws. Over footage it keeps to the corner card, beside the event.
- **Which graphics stand alone**, over no footage, and why.
- **The words on screen.**

Everything the shot list names comes from `facts.md`, with its places.

## Working files

In the film's folder. Draft n is numbered from 1. When the step starts again in a folder that already holds
drafts, the numbers go on from the highest there.

- `shots-<n>.json`: draft n, as the shot-lister wrote it.
- `shots-<n>-run.md`: the shot-lister's answer on draft n, saved word for word.
- `shots-<n>-check.txt`: what `shots.py check` printed on draft n.
- `shots-<n>-owner.md`: the owner's words, when the loop reaches its limit, saved word for word.
- `shots.json`: the draft that passed.
- `shots-sheet.txt`: what `shots.py sheet` printed for `shots.json`.

## Who does what

| Who | Brief | Reads | Writes |
|---|---|---|---|
| the tool | [shots.py](shots.py) | `shots-<n>.json`, `script.md` through `script-check.py`'s parser, `narration.json`, `facts.md` | what it prints; `shots.json` on a pass |
| the shot-lister | [shot-lister-brief.md](shot-lister-brief.md) | `script.md`, `narration.json`, `facts.md`, the form, and the last draft's check | `shots-<n>.json` and its answer |
| the main session | this file | what the tool prints, the answers, the owner's words | those, saved |
| the owner | | the check and the sheet, when the loop reaches its limit | what the next draft should change |

## The tool

Run it with the Python that runs the project's scripts, as
`python <checkout>\.claude\skills\create-story-video\shots.py <command> …`. Every command prints its
verdict on its first line and how long it took on its last.

- `check <folder> <n>` checks draft n against the form and the rules in [shots-schema.md](shots-schema.md).
  It prints `SHOTS CHECK: PASS` and copies the draft to `shots.json`, or `FAIL` with an `ERROR` line for each
  fault, or `REFUSED` when the draft, `narration.json` or a readable `script.md` is missing. Its second line
  counts the shots of footage alone, the graphics over footage and the graphics alone, the bodies to draw,
  the seconds of footage and the seconds with none. An `alone` line names each graphic that stands alone,
  with its length and why. Its
  `departs from the script` lines say where the draft leaves the script's directing, and refuse nothing.
- `sheet <shots json>` prints one line a shot: its id, where it starts, its length, its camera and bodies or
  `graphic`, and the start of what the viewer sees.

## Launching the shot-lister

The shot-lister is launched with this prompt, word for word. Only the parts in angle brackets are filled in:
`<checkout>` is the checkout that holds this file, `<folder>` the film's folder, both as absolute paths, and
`<n>` the draft's number.

```text
You are the shot-lister of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\shot-lister-brief.md. Read it whole and follow it.
The film's folder is <folder>.
You write draft <n>.
Write only inside the film's folder, and only through the shell. Never write in your session scratchpad or in TEMP.
Your time budget is 15 minutes.
```

It is a general-purpose agent on opus. It writes its own file through the shell, as the story's writers do.

## The procedure

1. If the film's folder has no `narration.json`, stop: the narration step writes it first.
2. Launch the shot-lister for draft n. Save its answer in `shots-<n>-run.md`, word for word. If its first
   line is not `SHOTS: WRITTEN`, show the owner the answer and stop.
3. Run `check <folder> <n>` and save what it prints in `shots-<n>-check.txt`.
   - `SHOTS CHECK: PASS`: run `sheet <folder>\shots.json` and save what it prints in `shots-sheet.txt`.
     Tell the owner in one line how many shots there are, give the check's `alone` lines, and give its
     `departs from the script` lines. The step is done.
   - `SHOTS CHECK: FAIL`: go back to step 2 for draft n+1. The shot-lister reads the last check. After
     three drafts that failed, show the owner the last check and ask what the next draft should change.
     Save their words in `shots-<n>-owner.md`, and go back to step 2, where the shot-lister reads them too.
   - `SHOTS CHECK: REFUSED`: show the owner what it printed and stop.

Tell the owner when a shot-lister's answer says it stopped at its time budget.

## Before the first film

The step has not run yet. On its first film, round 49, the owner reads the sheet once, to see that the
directing works, although later films go straight on to filming.
