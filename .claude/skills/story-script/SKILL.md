---
name: story-script
description: Edit a story film's narration into a script a person would read aloud - an editor and a cold reader in up to three rounds, with the mechanical checks between them. Use as the script stage of the story-film flow, or when the owner says a film's prose sounds machine-written.
---

# Story script

The editor's rules are in `logbook/specs/story-script-brief.md`. The tool is
`scripts/story-script.py`, and the story's folder is `scratch/story/<round>/` with `story.json`,
`story.md` and `checks.tsv` in it. The page, `script.md`, shows each scene's narration one paragraph
a line, and the tool cuts the subtitles from it.

Checklist, with N the round of the stage, from 1:

1. `python scripts/story-script.py render <folder> --force` writes `script.md`.
2. `python scripts/story-script.py check <folder> --stats > <folder>/check-0.txt` for the writer's
   counts.
3. **The editor**: a `general-purpose` subagent on Opus, run in the background, with its prompt
   below. It writes `script.md` and returns a table of what it changed and why.
4. `story-script.py apply <folder>` (the first apply keeps `story.draft.json`), then
   `story-script.py check <folder> > <folder>/check-N.txt`. Any ERROR goes back to the editor
   before the cold read, as round N again.
5. Read `edits.tsv`'s new rows for meaning, since the check sees numbers and words and not claims.
   Every fact a line gains is checked against `story.md` and `checks.tsv`, and against the clip's
   plan and contact sheet once the story is filmed: where the body sits in the frame, the
   provenance word, whether the chart is drawn. Fix a false line in `script.md`, apply again, and
   write each fix and its reason into `<folder>/edit-notes-N.md` for the next round's editor.
6. **The cold reader**: a `general-purpose` subagent on Sonnet, run in the background, told to
   use no tool, with its prompt below. `story-script.py cold <folder>` writes `script.cold.md`, the
   page without its notes and each scene's heading cut to its number, and that page goes into the
   prompt where the template says. Save its answer, as it came, as `<folder>/cold-read-N.md`.
7. Compare the cold reader's two sentences with the arc in `story.json`. A summary that misses the
   turn is a failed read, whatever the stumbles say. Another round from step 3 if it missed the
   turn or stumbled, at most three rounds in all. The loop stops early when a cold read finds no
   stumble and tells the arc back.
8. Hand on `script.md`, `story.json`, `story.draft.json`, `edits.tsv`, the cold reads and the
   `--stats` table, writer's against script's, to the owner's review, with the last cold read's
   notes beside the script.

## The prompts

Each prompt is used word for word, with only its placeholders filled in: the absolute path of the
checkout that holds this skill, the story's folder, the round N and, for the cold reader, the page.
Add nothing else. What the session wants a subagent to know goes into the folder as a file
(CLAUDE.md, "A subagent that a skill launches is briefed from the skill's files alone").

The editor:

```
You are the script editor of a round's story film, on round <N> of the script stage.
Your brief is <checkout>\logbook\specs\story-script-brief.md. Read it whole and follow it.
The story's folder is <folder>.
```

The cold reader:

```
You are watching a short film about a simulated tank of water, and reading its narration as it
plays. You have never seen this tank or anything about it. Use no tool. The narration follows,
scene by scene.

<the contents of script.cold.md>

Return, in this order:
1. Every stumble, as scene, line and kind: a word you cannot define, a line you had to read
   twice, a line that sounds written rather than spoken, a slogan or an advert, a jump you cannot
   follow, a number you cannot picture.
2. The three lines that sound most like a machine wrote them.
3. The story in two sentences, as you understood it.
```

## Rules

- A scene or chapter is never added, removed or moved here: `apply` refuses it, and it goes back
  to the writer.
- A number the check refuses is not filmed. A new fact means a row in `checks.tsv` from a query,
  the writer's work.
- Neither subagent waits on anything. The editor writes only `script.md`, through the shell; the
  cold reader uses no tool and writes nothing.