---
name: story-script
description: Edit a story film's captions into a script a person would read aloud - an editor and a cold reader in up to three rounds, with the mechanical checks between them. Use as the script stage of the story-film flow, or when the owner says a film's prose sounds machine-written.
---

# Story script

The rules are in `logbook/specs/story-script-brief.md`; read it first, with
`logbook/specs/story-glossary.md` and STYLE.md §7 (story films). The tool is
`scripts/story-script.py`, and the story's folder is `scratch/story/<round>/` with `story.json`
and `checks.tsv` in it.

Checklist:

1. `python scripts/story-script.py render <folder>` writes `script.md`, one line a caption.
2. `python scripts/story-script.py check <folder> --stats` for the writer's counts.
3. **The editor**: an Opus subagent (`general-purpose`), told to write `script.md` and nothing
   else, through the shell and never with the Write or Edit tools. Give it the brief, the glossary,
   `script.md`, `checks.tsv` and any findings and cold-read notes so far. It writes the whole page
   and returns a table of what it changed and why.
4. The session runs `story-script.py apply <folder>` (the first apply keeps `story.draft.json`), then
   `story-script.py check <folder>`. Every ERROR goes back to the editor before the cold read.
   Then read `edits.tsv`'s new rows for meaning, since the check sees numbers and words, not
   claims. Every fact a line gains is checked against `story.md` and `checks.tsv`, and against
   the clip's plan and contact sheet once the story is filmed: where the body sits in the frame,
   the provenance word, whether the chart is drawn. The session fixes a false line itself and
   tells the editor. Round 48's trial found four this way: "the corner says COUSIN" on a film
   whose every scene read FAITHFUL, "in the middle" for a body the director framed on the left,
   and a naming rule narrowed to one line.
5. **The cold reader**: a Sonnet subagent, told to use no tool and write no file.
   `story-script.py cold <folder>` writes `script.cold.md`: the page without its notes, each
   scene's heading cut to its number. Paste that page into the prompt, so the reader sees
   nothing else. It returns its stumbles, the three most machine-like lines and the story in two
   sentences. Save its answer as `cold-read-N.md`.
6. Compare the cold reader's two sentences with the arc. Another round from step 3 if it missed
   the turn or stumbled, at most three rounds in all.
7. Hand on `script.md`, `story.json`, `story.draft.json`, `edits.tsv`, the cold reads and the
   `--stats` table, writer's against script's, to the owner's review.

Rules:

- A scene or chapter is never added, removed or moved here: `apply` refuses it, and it goes back
  to the writer.
- A number the check refuses is not filmed. A new fact means a row in `checks.tsv` from a query, the
  writer's work.
- Neither subagent waits on anything. The editor writes only `script.md`, through the shell (the
  owner, 2026-09-26); the cold reader uses no tool and writes nothing.
