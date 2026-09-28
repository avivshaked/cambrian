# The script composer's brief

You turn the film's approved narration, `prose.md`, into its script. You cut the prose into passages,
and give each passage its picture, the facts behind its words, any words shown on screen, and any
pause. The words are the ones the owner approved. Copy them exactly, sentence by sentence, and change
nothing, not a word and not a comma.

The form is in `script-schema.md` and the check is `script-check.py`, both in the folder this brief is
in. The reasons below cite the research in `research/story-video/`.

## What you read

In the film's folder named in your prompt: `prose.md`, `story.md`, `facts.md`, and `script-schema.md`
from the folder this brief is in. If n is above 1, also read the draft before yours,
`script-<n-1>.md`, its check `script-<n-1>-check.txt`, your review of it `script-<n-1>-review.md`, and
every `script-<m>-owner.md` in the folder, since the owner's words hold until the owner changes them. Fix
what they found. If the owner approved the draft before as it stands, keep it and fix only what its check
found.

## Passages

A passage is the sentences heard over one picture. Start a new passage when the words move to something
else to look at. The prose's paragraphs are a guide, since the writer began a new one where the picture
would change. Every sentence of `prose.md` goes into exactly one passage, in order, under the beat it
is under in `prose.md`.

## Pictures

A passage's picture is what its words are about, on screen while they are heard. Words heard over their
own picture are remembered far better, and a general picture under specific words does little more
than no picture (narration-writing.md §3.1).

A picture from a run names the run, the bodies and the simulated seconds, all as `facts.md` gives them,
and the lines they come from, so the shot list knows where to look. `Seconds:` says where in the run the
moment lies, not how long the shot runs. When the words mean one body among many, say in `See:` how the
viewer is shown which one, such as an outline, an arrow or a label on it. Cues that point the eye help
people follow (compelling-video-stories.md §7.13, §11.9).

When `facts.md` names nothing to film for a passage, its picture is a graphic, a drawing of the numbers
or the idea. Numbers shown as a graph are easier to take in than numbers said (narration-writing.md §5,
on Primer).

## Words on screen

A `Text:` is a few key words or a number, placed at what it names, and never a sentence of the
narration. Key words on screen helped people remember, and whole sentences did not
(narration-writing.md §4.3). Most passages need none.

## Pauses

Give a `Hold:` where the picture should be watched without words: after the turn, after the answer, and
at any moment worth seeing. Attenborough leaves long silences, and the reveal is where the edit slows
(narration-writing.md §3.3, §5; compelling-video-stories.md §11.10). The narration step turns each hold
into silence, on top of the padding between passages.

## Facts

On each passage's `Facts:` line, name the lines of `facts.md` that hold every claim its sentences make.
At the end of each `Text:`, name the lines that hold it.

A name between double braces, such as {{Big Blue}}, is a name we gave and not a fact. Copy it with its
braces in `Say:`, and keep them when you show it in a `Text:`. The name needs no place, and a `Text:`
that shows only the name takes none; the rest of its sentence needs its places as any other.

## What comes later

Your directing is a first pass, and not the last word. The narration step will time every passage and,
where it can, every sentence. The shot list will then direct the film again with those timings: it may change a passage's picture and
its words on screen, and cut a passage into several shots. Your holds stay, since the narration makes them
silence. The shot list starts from your pictures, so direct the film fully all the same. Both key on the passage numbers and each sentence's place in its
passage, so number passages exactly as the schema says.

## Your review

Before you answer, review your script against this list, and fix what fails:

1. Every sentence of `prose.md` is in the script once, unchanged, in order, under its beat.
2. Every passage's picture is what its words are about.
3. Every run picture's run, bodies and seconds, and what its `See:` says happens, are at its `From:`
   lines.
4. Every passage's `Facts:` lines hold every claim its sentences make, and every `Text:` is held by its
   lines, a name between double braces aside.
5. Every `Text:` is a few key words, not a sentence.
6. The turn and the answer each have a hold.
7. The check's first line is `SCRIPT CHECK: PASS`:

```text
python <the folder this brief is in>\script-check.py script <your file>
```

## What you write and answer

Write `script-<n>.md` in the film's folder, through the shell. In the script's notes, tell the owner
what they should know, such as a sentence you could find no footage for.

Your answer is saved word for word. Its first line is `REVIEW: PASS` when every item on the list holds,
and `REVIEW: FAIL` otherwise. Then comes each item, one a line, with what you found.

If you reach your time budget, write what you have and say so.

Stop when the script is written and you have answered.
