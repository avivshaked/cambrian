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
the cold reader's notes. It writes the whole page to `script.md` through the shell and nothing
else, never with the Write or Edit tools (the owner, 2026-09-26), and returns a table of what it
changed and why. The session applies the page and checks it.

Its rules:

1. **A number stays** as the writer checked it, or leaves the screen; no new number
   goes on screen without a row in `checks.tsv` for its scene, and the check refuses one. A word
   for a number is allowed when it is true of the exact value (44% is "nearly half"; "half" would be false).
   A fact that leaves the screen stays in `story.md`.
2. **Each paragraph is read aloud** before it stands, as speech a person would say. The page shows
   each paragraph on one line and the tool cuts the subtitles from it (2026-09-26), so a sentence is
   never broken up to fit a subtitle. Numbers stay few and are said as a person says them, and the
   sentences vary in length and connect (the writer's brief, "The narration").
3. **The `###` scene headings and the chapter headings stay** as they are:
   scenes and chapters are the writer's and the film's, and the tool refuses a change to them. A
   chapter's title may be reworded. A blank line starts a new narration paragraph, where the
   picture needs a breath.
4. **Shorter is better**, and a scene lengthens itself when its captions need more time, and the
   film's total stays near ten minutes.
5. **The glossary's words**, one name a thing, as in the writer's brief.

## The cold reader

The cold reader is a Sonnet subagent, a different model on purpose, given only the page with its notes stripped
and each scene's heading cut to its number (`script.cold.md`, from `story-script.py cold`), pasted
into its prompt. It never sees the runs, the glossary or this brief. It is told it is watching a
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

After every apply the session reads the round's new rows in `edits.tsv` for meaning. The check
sees numbers and words, and a line can keep every number and still say something false. Each
fact a line gains is checked against `story.md` and `checks.tsv`, and against the clip's plan and
contact sheet once the story is filmed. The session fixes a false line itself and tells the
editor in the next round.

The stage hands on `script.md`, `story.json` as applied, the writer's `story.draft.json`,
`edits.tsv` (every change, before and after), each round's cold read as `cold-read-N.md`, and the
check's `--stats` table, writer's against script's, for the owner.

## The first trial (2026-09-25)

The stage was first run on round 48's second story ([`story-r48-v2/`](story-r48-v2/)). Its 20
scenes and 92 captions were written to the writer's brief before this stage existed, and filmed
from the farm's recorded windows, every scene reading FAITHFUL. Three rounds ran, each with an
Opus editor, then apply and the check, then a fresh Sonnet cold reader. Everything they left is in
[`story-r48-v2/script-trial/`](story-r48-v2/script-trial/): the page, the story as applied,
every edit, the three cold reads and the check's counts.

The check went from 6 errors and 21 warnings on the writer's draft to none. The draft's errors
were five narrator words and one caption held too briefly. Eighteen of its warnings were colons.
The colons and narrator words went to zero, captions with a number fell from 52% to 47%, and the
scenes grew from 518 s to 551 s. The cold readers found 10, 8 and 8 stumbles. The first read the
film's guess about why the eaters faded as its cause. The second and third told the story back
as the arc has it. The third had the turn in full: the leaf line that kept a stomach was the
largest line carrying one in any tank.

The check could not see what mattered most. Reading the edits for meaning found seven false
lines, four of the editor's and three that were the writer's all along. The editor wrote that
the corner said COUSIN on a film whose every scene read FAITHFUL. It put two bodies "in the
middle" where the director had framed each a third off centre. It narrowed the naming rule to
the one case in view. It wrote a child's share as taken from the energy in its parent's body,
when it is sized by that body and paid from the reserve. Two times were counted from a scene's
start but heard when the line is read. "Starves 44.5 s from here", on screen at 29.5 s, put a
death at 74 s into a 46 s scene. And a chart mark reading "we met it here" was on screen
outside the page.

Five things changed in the tools because of it. Apply now times a changed scene from a fixed
base, the draft or the filmed story, and gives back length a scene no longer needs. A chart's
words show on the page and pass the word checks. The check warns on a time counted from the
scene. Narration timing takes `least_seconds` as a floor, since scene 11's death would have
fallen outside a scene timed from its words. And the writer's brief now writes for the recorded
window.

What is left goes to the owner beside the script. Scene 8 carries the newcomers, the one in ten,
the landing rule, the gift and a growth in half a second. The film never squares that growth
with body 201's slow one. The cold reader called three of those lines machine-written. It also tripped
on "family" turning into "line", and on a last round the film never shows. The edits changed the
length of twelve scenes and one chart, so a film of this script refilms thirteen scenes. With
narration the voice sets every length again, so these lengths hold only for a film without it.

The editor's first round took 19 minutes and the others 4 each. Each cold read took about 2.
