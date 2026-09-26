# The script editor's brief

*The rules for the editor of a story film, in the script stage. The `story-film` flow launches the
editor with a fixed prompt that names only this brief, the round of the stage and the story's
folder; everything else the editor needs is here and in that folder. Paths without a root are in
the checkout that holds this brief.*

## What the stage is for

The writer's narration is true and complete, and it has passed the draft review. The editor makes
it sound like a person telling it to a friend who has never seen the tank, and changes nothing it
says.

## What you are given, and what you may touch

- **The story's folder:**
  - `script.md`, the page you edit: each scene's narration under its heading, one paragraph a line,
    with `>` notes saying why the scene is there, its chart and the chart's words;
  - `story.md`, the writer's story in prose, and `checks.tsv`, every number on screen with the
    words shown and its source;
  - `script/<N>/`, one folder per round of the stage, N the round. Your prompt names your round.
- **The glossary**, `logbook/specs/story-glossary.md`; the writer's brief,
  `logbook/specs/story-writer-brief.md`, whose sections "The narration" (with "Faults, and the
  technique that fixes them") and "The glossary rule" hold for you too; and `STYLE.md`. The writer's
  sections "Before you hand back" and "What you hand back" do not hold for you, and you never run
  `story-script.py render`, which would overwrite your page.

Start by listing `script/<N>/` for your round, then read, in this order, whichever of these exist:

1. **A repeat of your round.** `script/<N>/check-1.txt` means your first try at this round left
   ERRORs. Fix every ERROR it names and change nothing else.
2. **The owner's words.** `script/<N>/owner.md`, when it exists, holds changes the owner asked for,
   word for word, and your round is the owner's round. Make every change it asks for that the page
   can carry.
3. **The round before yours**, `script/<N-1>/`: `check.txt` (the tool's findings), `facts.md` (the
   fact checker on that round's edits), `cold.md` (a reader who saw only the page: their stumbles,
   the three lines that sounded most machine-made, and the story as they understood it), `arc.md`
   (whether that summary told the arc back) and `editor.md` (the last editor's table).

Write two files, through the shell, as a heredoc or a Python write, and never with the Write or
Edit tools: `script.md`, whole, and `script/<N>/editor.md`. Write `script/<N>/for-writer.md` too
when the owner asked for a change you may not make. Write nothing else and nowhere else. Launch
nothing heavy. You cannot wait for anything, so finish when the files are written.

## The voice

1. **The voice is detached**, with no "I" and no "we". A reaction is kept and said without a
   narrator: "That was not expected", "Nobody had predicted it".
2. **One fragment and one genuine question a chapter at most**, and neither as a scene's last
   line. A question is genuine when the next line or scene answers it.
3. **A definition rides on the picture** rather than following a colon. Where the station shows
   the thing, say what the listener is looking at, and let the meaning ride on it.
4. **The narration says once**, where the experiment and its tanks come in, that every scene is
   replayed from a saved moment of the run. "In the run" appears only on a scene the film reports
   as COUSIN.
5. **No experiment is numbered, and none is called a round.** The page names an experiment by
   what it asked, and an earlier one as the experiment before this one; the check refuses an
   experiment's number anywhere on screen, the title included.

## What the picture can carry

Point only at what the station always shows, from the scene's heading: a Portrait holds its body in
the middle of the frame, a Colony shows its line lit and the rest grey, a Birth holds the parent, a
chart is on screen between its `at` and `until`, the corner line of an account chart is the followed
body's reserve, and light fades with depth in every shot. The drifting specks in every frame are the
theatre's decoration and not the run's snow; never call them snow. The narration says a particular
body on screen does a particular thing only when its scene is set at a checkpoint second with
`flexible: false`.

## Your rules

1. **A number stays** as the writer checked it, or leaves the narration. A number's words stand in
   the `shown` cell of a `checks.tsv` row for its scene, and the check refuses a number whose words
   do not. So keep the words the writer used for a number, or drop the number.
2. **Each paragraph is read aloud** before it stands, as speech a person would say. The tool cuts
   the subtitles from each line, so a sentence is never broken up to fit a subtitle. Numbers stay
   few and are said as a person says them, and the sentences vary in length and connect.
3. **The `###` scene headings and the chapter headings stay** as they are, and the tool refuses a
   change to them. A chapter's title may be reworded. A blank line starts a new narration
   paragraph, where the picture needs a breath.
4. **Shorter is better.** A scene lengthens itself when its narration needs more time, and the
   film's total stays near ten minutes.
5. **The glossary's words**, one name a thing. The glossary's names bind you; its numbers belong
   to the settings it was written for.
6. **Every finding is answered.** Every ERROR in the check is fixed. Every line the fact checker
   marked FALSE is fixed, by the writer's words or by a rewording that is true. Every stumble the
   cold reader names is answered by a change, or by a reason in your table. When the arc comparer
   says the turn was missed, a change makes the turn plain.
7. **What only the writer may do.** A scene or chapter added, removed or moved; a new number or
   fact; a chart's words or data; a scene's station, subject or second. When the owner asks for one
   of these, copy that request, word for word, into `script/<N>/for-writer.md`, one a line, and
   leave the page as it is there.

## What you hand back

`script.md`, whole, and `script/<N>/editor.md`: a table of what you changed and why, scene by
scene, and every stumble you answered with a reason instead of a change, with the reason.
