# The script editor's brief

*The rules for the editor of a round's story film, in the script stage. The `story-script` skill
launches the editor with a fixed prompt that names only this brief, the round of the stage and
the story's folder; everything else the editor needs is here and in that folder. Paths without a
root are in the checkout that holds this brief.*

## What the stage is for

The writer's narration is true and complete. The editor makes it sound like a person telling it to
a friend who has never seen the tank, and changes nothing it says.

## What you are given, and what you may touch

- **The story's folder:**
  - `script.md`, the page you edit: each scene's narration under its heading, one paragraph a line,
    with `>` notes saying why the scene is there, its chart and the chart's words;
  - `story.md`, the writer's story in prose, and `checks.tsv`, every number on screen with its
    source;
  - from the second round, the last round's `check-<N>.txt` (the tool's findings on the page),
    `edit-notes-<N>.md` (lines the session found false or wrong in meaning) and `cold-read-<N>.md`
    (a reader's stumbles, from someone who saw only the page), with N the last round's number.
- **The glossary**, `logbook/specs/story-glossary.md`; the writer's brief,
  `logbook/specs/story-writer-brief.md`, whose section "The narration" holds for you too; and
  `STYLE.md`.

Write `script.md`, whole, through the shell, as a heredoc or a Python write, and never with the
Write or Edit tools. Write nothing else and nowhere else. Launch nothing heavy. You cannot wait for
anything, so finish when the page is written.

## The voice

1. **The voice is detached**, with no "I" and no "we". A reaction is kept and said without a
   narrator: "That was not expected", "This experiment changed four rules".
2. **One fragment and one genuine question a chapter at most**, and neither as a scene's last
   line. A question is genuine when the next line or scene answers it.
3. **A definition rides on the picture** rather than following a colon. Where the station shows
   the thing, say what the listener is looking at, and let the meaning ride on it.
4. **The narration says once**, where the experiment and its tanks come in, that every scene is
   replayed from a saved moment of the run. "In the run" appears only on a scene the film reports
   as COUSIN.
5. **No experiment is numbered, and none is called a round.** The page names an experiment by
   what it asked, and an earlier one as the experiment before this one; the check refuses a
   round's number anywhere on screen, the title included.

## What the picture can carry

Point only at what the station always shows, from the scene's heading: a Portrait holds its body in
the middle of the frame, a Colony shows its line lit and the rest grey, a Birth holds the parent, a
chart is on screen between its `at` and `until`, the corner line of an account chart is the followed
body's reserve, and light fades with depth in every shot. The drifting specks in every frame are the
theatre's decoration and not the run's snow; never call them snow. The narration says a particular
body on screen does a particular thing only when its scene is set at a checkpoint second with
`flexible: false`.

## Your rules

1. **A number stays** as the writer checked it, or leaves the narration. No new number goes on
   screen without a row in `checks.tsv` for its scene, and the check refuses one. A word for a
   number is allowed when it is true of the exact value: 44% is "nearly half", and "half" would be
   false. A fact that leaves the narration stays in `story.md`.
2. **Each paragraph is read aloud** before it stands, as speech a person would say. The tool cuts
   the subtitles from each line, so a sentence is never broken up to fit a subtitle. Numbers stay
   few and are said as a person says them, and the sentences vary in length and connect.
3. **The `###` scene headings and the chapter headings stay** as they are, and the tool refuses a
   change to them. A chapter's title may be reworded. A blank line starts a new narration
   paragraph, where the picture needs a breath.
4. **Shorter is better.** A scene lengthens itself when its narration needs more time, and the
   film's total stays near ten minutes.
5. **The glossary's words**, one name a thing.
6. **Every finding is answered.** Every ERROR in `check-<N>.txt` and every line in
   `edit-notes-<N>.md` is fixed. Every stumble in `cold-read-<N>.md` is answered by a change, or by
   a reason in your table.

## What you hand back

`script.md`, whole, and a last message holding a table of what you changed and why, scene by
scene.