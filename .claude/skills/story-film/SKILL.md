---
name: story-film
description: Make a round's story film for the owner - a writer's shot list across all the round's seeds, filmed by the theatre's safari director and joined into one film with subtitles in scratch/owner. Use when the owner asks for a round's story, a film of how a round went, or a repeat of the round 48 story films.
---

# Story film

The procedure lives in the repository, so that any agent can follow it. Read all three before
starting:

- `logbook/specs/story-film.md`: the steps (guides, writer, check, render, join, delivery).
- `logbook/specs/story-writer-brief.md`: the writer's rules (truth, the glossary rule, the
  opening chapter on how the world works, the story's shape, captions, charts).
- `logbook/specs/story-glossary.md`: every word a film may use, checked against the code.

Checklist:

1. Every seed of the round has ended. Read HANDOFF for the machine's load ruling: a render is
   one heavy job, and nothing heavy runs beside it.
2. `python scripts/guide.py <arm> --no-economics` for each seed.
3. An Opus writer subagent with the brief and the glossary. Tell it its folder
   (`scratch/story/<round>/`), that the runs are read-only and that nothing heavy runs. It starts
   from `logbook/specs/story-r48-v2/` (the readers and the builder) and returns `story.md`,
   `story.json` and `checks.tsv`.
4. Read `story.md` against `checks.tsv`. A number with no row is not filmed. A clause of a seed
   that stopped early is censored, as the round's reader marks it (`short`).
5. `scripts/theatre-safari.ps1 <arm> -Story <story.json> -Check` per seed.
6. Render one seed at a time in a detached chain (`-Folder story-final -DeleteFrames`), and look
   at every contact sheet.
7. `python scripts/story-assemble.py <story.json> scratch/owner/round-NN-story.mp4 <folders...>`
   joins the clips and burns the subtitles. Copy `story.md` beside the film, and give the owner
   the full path as plain text.

From round 49 the farm records each scene's window and the theatre only draws it
(`logbook/specs/record-and-film-spec.md`, Part B). When that path is built, `story-film.md`
says so and replaces step 6.
