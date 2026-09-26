---
name: story-resolve
description: Compose a round's story film as a DaVinci Resolve timeline instead of joining it with ffmpeg - the scenes on V1, the provenance label on V2, the captions as Resolve subtitles, music from assets/, a marker at every scene - so the owner can edit the film in Resolve. Use after the story-film skill has filmed a story's clips, or when the owner asks to build, rebuild or check a story timeline in Resolve.
---

# Story film in Resolve

The procedure lives in the repository, so that any agent can follow it. Read
`logbook/specs/story-resolve.md` before starting: it has the layout, the setup, and what the
Resolve API does that its documentation does not say. It replaces only the join (step 7) of the
story-film skill; the guides, the writer, the check and the render come first, as that skill says.

Checklist:

1. Every scene's clip is filmed and its contact sheet looked at (story-film, step 6).
2. Resolve is running with external scripting set to Local, and
   `python scripts/story-resolve.py --check` shows the configured project open and the assets
   present. The per-machine settings are in `.env` (keys in `scripts/story-resolve.env.example`);
   never write a machine's values into this skill.
3. Ask the owner before building if another agent may be working in Resolve. A build changes the
   current timeline and, for a moment, the Deliver page's settings.
4. Plan and build in one step:
   `python scripts/story-resolve.py <story.json> scratch/story/<round>/resolve <clip folders...>`.
   The captions are the story's as it stands; a caption edited after filming is another plan and
   build, a new `vK` that reuses the clips already imported.
5. Read `build.json` in the out folder. Every error there is a scene, caption or label that is not
   where the plan put it; fix it before telling the owner.
6. Render one second in Resolve and check it against the ffmpeg film or the last film the owner
   accepted (CLAUDE.md's reference-sheet rule), then give the owner the timeline's name and bin.

Rules:

- A build only adds a bin and a timeline named `<name> vK`. Never delete or change the owner's
  timelines, bins or render queue. A test build is named `zz probe ...` and deleted afterwards,
  with the owner's current timeline put back.
- Settings changed by script change the Deliver page, so save them as a render preset first and
  load it back after (the builder does this for the title).
- A render is a heavy job under the machine's load ruling in HANDOFF.
