---
name: story-script
description: The script stage of the story-film flow - rounds of an editor, the tool's check, a fact checker, a cold reader and an arc comparer, run by story-flow.py next, until a round is clean or the third round ends. Use to understand or resume the script stage of a story film; the story-film skill runs it.
---

# Story script

The script stage makes the writer's narration sound like a person telling it. It runs inside the
story-film flow, and `python scripts/story-flow.py next <folder>` drives it: this skill says what
happens, so that a session knows what `next` is doing. The editor's rules are in
`logbook/specs/story-script-brief.md`, the fact checker's in `logbook/specs/story-facts-brief.md`,
and every prompt is in `.claude/skills/story-film/prompts.md`. The tool is `scripts/story-script.py`.

## A round

Each round N has a folder, `script/<N>/`, in the story's folder.

1. **The editor** (`general-purpose`, Opus) edits `script.md` and writes `script/<N>/editor.md`,
   its table. In the owner's round it reads `script/<N>/owner.md` and writes
   `script/<N>/for-writer.md` for what only the writer may change.
2. **Apply and check**, by `next`: `story-script.py apply --round N --author editor`, then the check
   into `script/<N>/check.txt`. An ERROR sends the editor back once to fix it (its first try's
   files become `check-1.txt` and `editor-1.md`). An ERROR after the second try restores the
   writer's words in those scenes (`story-script.py revert`), since the writer's draft passed the
   same check.
3. **The fact checker** (`Explore`, Opus) judges every edited line against `story.md`, `checks.tsv`
   and the picture; **the cold reader** (`Explore`, Sonnet) reads only `script/<N>/cold-page.md`,
   the page with no notes, and reports its stumbles and the story as it understood it. They run
   together, and `save` records each answer.
4. **The arc comparer** (`Explore`, Sonnet) reads only `script/<N>/arc-pair.md`, the writer's arc
   beside the cold reader's summary, and answers TOLD or MISSED.

A round is clean with no line false, no stumble and the arc told, and a clean round closes the
stage. Otherwise round N+1 begins, and its editor reads round N's files. The third round closes the
stage whatever it ends with, and the owner reads what remains. In a closing round, a line still
false gets the writer's words back. An owner's round, opened by `--verdict changes`, also closes
the stage.

## Rules

- A scene or chapter is never added, removed or moved here: `apply` refuses it, and it goes to the
  writer through `for-writer.md`.
- A number the check refuses is not filmed. A new fact means a row in `checks.tsv` from a query,
  the writer's work.
- No subagent waits on anything. The editor writes only `script.md`, `script/<N>/editor.md` and
  `script/<N>/for-writer.md`, through the shell. The readers write nothing.
- The session never edits `script.md`; a caption the owner wants changed goes through their words
  and an owner's round.
