---
name: story-film
description: Make a round's story film for the owner end to end, or pick one up where it stopped - guides, the arc, the writer, the script edit, narration, the owner's reviews, filming, the Resolve timeline and delivery. Use when the owner asks for a round's story or a film of how a round went, or to resume a story film in progress.
---

# Story film

This skill runs the whole flow and hands each stage to its own procedure. The flow spans days and
sessions, so its state lives in the story's folder, `scratch/story/<round>/`, and one command
says where it stands and what comes next:

```
python scripts/story-flow.py status scratch/story/<round>
```

Run it first in every session that touches a story, and after every stage. The procedure, the
stage table and each stage's details are in `logbook/specs/story-film.md`; read it before
starting. The flow before this one (writer, check, render, ffmpeg join) is tagged `story-film-v2`.

## The stages

1. **Open.** Every seed of the round has ended, and HANDOFF's load ruling allows the work.
   `story-flow.py init scratch/story/<round> --round N --runs <arms...>`.
2. **Guides.** `python scripts/guide.py <arm> --no-economics` for each run.
3. **Arc.** The writer's first pass: an Opus subagent with `logbook/specs/story-writer-brief.md`
   and the glossary writes `arc.md` into the story's folder through the shell.
4. **Arc approved.** The owner's first review. Ask for the ruling in full (CLAUDE.md, "Say when a
   ruling blocks the work"), then `story-flow.py approve <folder> arc`.
5. **Write.** The writer's second pass, from the approved arc: `story.md`, `story.json` and
   `checks.tsv` (the procedure says how, under CLAUDE.md's rule on subagents that write files).
   Read `story.md` against `checks.tsv`; a number with no row is not filmed.
6. **Script.** The `story-script` skill.
7. **Script approved.** The owner's second review: `script.md`, `story.md` and the
   writer's-against-script counts. Then `story-flow.py approve <folder> script`. Nothing is
   narrated before this approval (the owner, 2026-09-26); it covers the words and the scenes, not
   their timing.
8. **Narrate.** `python scripts/story-narration.py segments <folder>` writes what the service
   speaks; the service's results go into `narration/narration.json` (its form is in the script's
   help); `story-narration.py timing <folder>` times the story from them
   (`logbook/specs/story-narration.md`). Until the service exists: `story-flow.py skip <folder> narrate`.
9. **Check.** `scripts/theatre-safari.ps1 <arm> -Story <story.json> -Check` for each run with
   scenes, then `story-flow.py mark <folder> check <arm>`.
10. **Film.** Copy `story.json` to `story.filmed.json`, film per the procedure one seed at a time,
    look at every contact sheet, then `story-flow.py set <folder> clips <clip folders...>`.
11. **Assemble.** `python scripts/story-resolve.py <story.json> <folder>/resolve <clip folders...>`
    builds a new timeline in Resolve (the `story-resolve` skill). Without Resolve,
    `python scripts/story-assemble.py <story.json> <film.mp4> <clip folders...> --captions story`.
12. **Timeline approved.** The owner's last review, in Resolve. `story-flow.py approve <folder> timeline`.
13. **Deliver.** Render, copy the film and `story.md` to `scratch/owner/`, give the owner the path
    as plain text, then `story-flow.py mark <folder> deliver`.

## Changes after a stage is done

- **A caption:** edit `script.md`, `story-script.py apply`, `check`, narrate the changed paragraphs,
  and assemble again. Nothing is filmed again; the assembly takes the captions from `story.json`.
- **A scene's station, second, subject, length or chart:** `status` names the scenes filmed on the
  old values. Film those again.
- **An approval** goes stale when what was approved changes. `status` says so, and the owner sees
  the change.

## Rules

- A heavy job (a render, a narration job, a farm run) never starts beside another. Read the
  machine first, under HANDOFF's current ruling.
- A subagent waits on nothing. The writer and the editor write their own files through the shell
  (a heredoc or a Python write, never the Write or Edit tools), and only inside the story's folder
  their brief names (the owner, 2026-09-26). Every other subagent returns text.
- Every owner review is a blocking ruling: lead with "I am blocked on ... from you", set the
  decision out in full, and keep working on what is not gated.
