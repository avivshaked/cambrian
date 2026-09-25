# The script editor's brief

*Written 2026-09-25, after the owner read round 48's second film and said its prose still felt
machine-written. The writer's brief ([`story-writer-brief.md`](story-writer-brief.md)) makes the
captions true and complete. This brief makes them sound like a person reading them to a friend.
The stage it serves is the script stage of the story-film flow
(`.claude/skills/story-script/SKILL.md`); the tool is `scripts/story-script.py`.*

## What was wrong with round 48's captions

The check's counts on round 48's second film (`story-script.py check --stats` on
[`story-r48-v2/`](story-r48-v2/)): 92 captions averaging 55 characters, one every 5.6 s, and
more than half of them carrying a number, 79 numbers in all. 18 of them explain a word after a
colon ("A stomach eats snow: dead matter drifting in the water."). Some end on a slogan ("What
breeds more, spreads.", "The gift paid."). Five speak as "I" or "we". Few point at what is on
screen. Each caption is correct; read in a row they sound like a list of findings, and once a
narrator speaks them that will be what the viewer hears.

## The voice (the owner's rulings, 2026-09-25)

1. **The voice is detached**, with no "I" and no "we" in a caption. A reaction is kept and said without a
   narrator: "I didn't expect that" becomes "That was not expected"; "we changed four rules"
   becomes "This round changed four rules".
2. **One fragment and one genuine question a chapter at most**, and neither as a scene's last
   line. A question is genuine when the next caption or scene answers it.
3. **A definition rides on the picture** rather than following a colon. Where the station shows the thing, say
   what the viewer is looking at, and let the definition ride on it.
4. **"In the run" is said once**, in the film's first chapter, which says that every scene is re-run from a
   saved moment and the numbers are the original run's. After that the phrase appears only where
   the difference matters: a birth or a count the replay may not show as it happened.

## What the picture can carry

Point only at what the station always shows, from the scene's heading: a Portrait holds its
body in the middle of the frame, a Colony shows its line lit and the rest grey, a Birth holds the
parent, a chart is on screen between its `at` and `until`, the corner line of an account chart is
the followed body's reserve, and light fades with depth in every shot. The drifting specks in
every frame are the theatre's decoration and not the run's snow (`TheatreSkin.cs`); never call
them snow. A replay is a cousin, so a caption never says a particular body on screen does a
particular thing unless the scene is held at a checkpoint with `flexible: false`.

## The editor

The editor is an Opus subagent. It gets this brief, the glossary, `script.md` (with its `>` notes: why each
scene is there, its chart), `checks.tsv` and, from the second round on, the check's findings and
the cold reader's notes. It writes no file (CLAUDE.md's rule of 2026-09-25 on subagents that
edit), and returns the whole page in its final message, in a single fenced block, followed by a
table of what it changed and why. The session writes the page, applies it and checks it.

Its rules:

1. **A number stays** as the writer checked it, or leaves the screen; no new number
   goes on screen without a row in `checks.tsv` for its scene, and the check refuses one. A word
   for a number is allowed when it is true of the exact value (44% is "nearly half"; "half" would be false).
   A fact that leaves the screen stays in `story.md`.
2. **Each caption is read aloud** before it stands, and is a sentence a person would say. One number a caption where it
   can be, and a comparison before a second number. Vary the length of sentences. A caption over
   70 characters is split or cut.
3. **The `###` scene headings and the chapter headings stay** as they are:
   scenes and chapters are the writer's and the film's, and the tool refuses a change to them. A
   chapter's title may be reworded. A blank line between two captions starts a new narration
   paragraph, where the picture needs a breath.
4. **Shorter is better**, and a scene lengthens itself when its captions need more time, and the
   film's total stays near ten minutes.
5. **The glossary's words**, one name a thing, as in the writer's brief.

## The cold reader

The cold reader is a Sonnet subagent, a different model on purpose, given only the page with its notes stripped
(`script.cold.md`). It never sees the runs, the glossary or this brief. It is told it is watching a
short film about a simulated tank of water and reading its narration. It returns:

- every stumble, as scene, line and kind: a word it cannot define, a line it had to read twice, a
  line that sounds written rather than spoken, a slogan or an advert, a jump it cannot follow, a
  number it cannot picture;
- the three lines that sound most like a machine wrote them;
- the story in two sentences, as it understood it.

The session compares the two sentences with the arc in `story.json`. A summary that misses the turn
is a failed read, whatever the stumbles say.

## The loop

At most three rounds: the editor, then `apply`, then `check` (every ERROR fixed before the cold
read), then the cold reader. The next round's editor gets the findings and the notes. The loop
stops early when a cold read finds no stumble and tells the arc back. What is left after three
rounds goes to the owner at the review, with the cold reader's last notes beside the script.

The stage hands on `script.md`, `story.json` as applied, the writer's `story.draft.json`,
`edits.tsv` (every change, before and after), each round's cold read as `cold-read-N.md`, and the
check's `--stats` table, writer's against script's, for the owner.
