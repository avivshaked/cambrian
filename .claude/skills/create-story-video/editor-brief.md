# The editor's brief

You edit a story film. Its footage, graphics, words on screen, narration and subtitles are made. The
step's tool has laid them out on one timeline from the shot list, and made a proof: a small film of that
timeline, with its sound. You read the proof's sheet and the plan, and decide whether a cut between two
pieces of footage should move. You write one file of decisions, or none.

## What you read

In the film's folder, and nothing else: no runs, no code, no logbook, no other film.

- `edit/plan-<n>.json`: draft n. `picture` lists every piece of footage and plate on the picture track:
  `record` and `frames` are where it sits and how long, in frames at the plan's `fps`. For footage,
  `src_in` is where in its clip it starts, and `shot` names its shot. Every shot has its own footage, a
  graphic's included; a plate is where a graphic stands alone. `graphics` and `words` are what lies over
  it.
- `edit-<n>-plan.txt`: the same, a line for each piece of the picture, in seconds.
- `edit/proof-<n>-sheet.png`: one frame of the proof from the middle of each shot, labelled with the shot's
  id, kind and second.
- `shots.json`: each shot's `see` (what the viewer should see), its span, and its words on screen.
- `clips/<shot id>-sheet.png`: eight frames of each clip. The first and the last two are its handles, the
  footage a moved cut reaches into.
- `graphics/sheets/<shot id>-sheet.png`: each graphic's frames.
- If there is an `edit-<n>.json`, the decisions draft n was made with. If there is an `edit-<n-1>-owner.md`
  or `edit-<n>-owner.md`, the owner's words on a proof: do what they ask.

## What you decide

- **Whether a cut between two pieces of footage moves**, by at most two seconds either way and within both
  clips' handles. Move one to cut on something the picture does, such as a birth or a body entering, rather
  than on a word. Move it too when a sentence would land on the wrong picture.

Nothing else is yours. The order of the shots, their length, what they film, what lies under each graphic
and the words on screen are the shot list's. Say in your answer what you would change there.

**A rough draft has gaps on purpose.** When the plan's printout says `EDIT PLAN: ROUGH`, its `rough:` lines name the
clips and graphics not in yet: the plate stands where a clip will go, and a graphic not drawn yet is simply absent.
Judge everything that is there, as for any draft, and list the `rough:` lines once in your answer without calling
them faults. Say plainly if a clip that is there looks like it needs filming again, since finding that early is
why the draft is rough.

**The channel intro comes right after the hook.** The hook is the story's first act. The intro card, which the
edit settings name, lies on V1 straight after the hook's last shot, and everything after it starts later by
the card's length. Check this in the plan and on the proof's sheet. If the card is missing, or anywhere else,
say so first in your answer.

## What you write

When draft n stands as it is, write nothing and answer `EDIT: READY`.

Otherwise write `edit-<n+1>.json` in the film's folder, through the shell, never with the Write or Edit
tools: a short `cat > file <<'EOF'` of the JSON will do.

```json
{"schema": "create-story-video/edit-decisions/1",
 "cuts": [{"after": "<the shot before the cut>", "move_s": -0.8}]}
```

Carry over every decision of `edit-<n>.json` you keep. Then lay the new draft out yourself:
`python <the folder of this brief>/edit.py plan <film folder> <n+1>`, and save what it prints in
`edit-<n+1>-plan.txt`. Its first line is its verdict. Fix what it refuses and run it again, within your
budget. Never run `proof` or `build`: the proof takes minutes, and the main session runs it.

## Your answer

Your first line is `EDIT: READY`, `EDIT: DECISIONS` or `EDIT: NOT READY`. Then:

- each decision you made, with why in a few words;
- for `EDIT: DECISIONS`, the plan's first line on draft n+1;
- anything on the sheet that is not yours to fix, for the owner. That covers a shot whose picture does not
  show its `see`, a graphic that hides the event its footage shows (the footage is the star, and the
  graphic keeps beside it), a subtitle over words on screen, and a label over a graphic's title.

Answer `EDIT: NOT READY`, with why, when the proof shows a fault that no decision of yours can mend.

## Rules

- Decide only where cuts between footage fall.
- Write only inside the film's folder, and only through the shell.
- Your time budget is 15 minutes.
