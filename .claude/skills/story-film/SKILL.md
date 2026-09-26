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
starting.

## The stages

1. **Open.** Every seed of the round has ended, and HANDOFF's load ruling allows the work.
   `story-flow.py init scratch/story/<round> --round N --runs <arms...>`, then
   `story-flow.py set <folder> entry <the round's logbook entry>` and
   `story-flow.py set <folder> prereg <its pre-registration>`.
2. **Guides.** `python scripts/guide.py <arm> --no-economics` for each run.
3. **Arc.** The writer's first pass, launched with its prompt below. It writes `arc.md` and
   `arc-reads.md` into the story's folder, and its last message lists the open choices.
4. **Arc approved.** The owner's first review. Ask for the ruling in full (CLAUDE.md, "Say when a
   ruling blocks the work"), write the owner's rulings into `arc-ruling.md` in the folder, one
   numbered line each, then `story-flow.py approve <folder> arc`.
5. **Write.** The writer's second pass, launched with its prompt below. It writes `story.md`,
   `story.json`, `checks.tsv` and `make_story.py`. Read `story.md` against `checks.tsv`; a number
   with no row is not filmed. Then `story-script.py render <folder> --force` and read the page
   aloud against the brief's "Before you hand back" list. A draft that reads as a list of findings
   goes back to the writer: write the failing lines and the fault each has, in the brief's words,
   into `writer-notes.md` in the folder, and launch the second pass again with the same prompt.
   Such a draft never goes to the owner.
6. **Script.** The `story-script` skill.
7. **Script approved.** The owner's second review: `script.md`, `story.md` and the
   writer's-against-script counts. Then `story-flow.py approve <folder> script`. Nothing is
   narrated before this approval. It covers the words and the scenes, not their timing.
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

## The writer's prompts

The writer is a `general-purpose` subagent on Opus, run in the background. Its prompt is one of the
two below, word for word, with the two paths filled in: the absolute path of the checkout that
holds this skill, and the story's folder. Add nothing else. Everything the writer needs is in its
brief and in the folder, and a note for it goes into the folder as a file (CLAUDE.md, "A subagent
that a skill launches is briefed from the skill's files alone").

First pass:

```
You are the writer of a round's story film, on its first pass.
Your brief is <checkout>\logbook\specs\story-writer-brief.md. Read it whole and follow it.
The story's folder is <folder>.
```

Second pass:

```
You are the writer of a round's story film, on its second pass.
Your brief is <checkout>\logbook\specs\story-writer-brief.md. Read it whole and follow it.
The story's folder is <folder>.
```

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
  (a heredoc or a Python write, never the Write or Edit tools), and only inside the story's folder.
  Every other subagent returns text.
- Every owner review is a blocking ruling: lead with "I am blocked on ... from you", set the
  decision out in full, and keep working on what is not gated.