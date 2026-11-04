# 0158 — The test films kept two dials off the look, and the look is now their one source

**2026-11-04**  ·  The theatre's test films and the death cloud, phases 128 to 130

Entry 0156 said the test films had lacked the agreed film look until a reader began handing it to every film. That was
half true. The look lives in one file, `scripts/theatre-film-look.json`, and the reader sets every dial of it that the
environment has left unset. The render script steps through the test films one after another on worker 7. It still set
dials of its own from three tables before the reader ran, and on two of them the tables won.

- The one skin was at level 3, the wide shot's level, in every close film, where the close look says 6. Levels 4 to 6 draw
  a body's parts as one rig and join them with a fillet instead of a ball.
- Growth was not eased between metabolic steps in 44 of the script's steps, where D290 ruled it eased. The steps had
  inherited the switch by copying, from the days when the ease was on trial.

The reader said so on every film, in a line that read "2 kept different". I did not read that line until I set out to
empty the tables. Every picture the owner had from the death-look trial carried both.

## The tables emptied, proved in two films

The tables are now empty. Their keys are kept, because the script clears those variables between steps. The first table
is kept too, as the base of the check that opens every chain: a fixed shot with every dial off, compared against a
reference. Two films proved the change.

The first pinned the two dials at their old values by hand. The reader then named only those two as kept different, and
112 of the 120 frames matched the last film made from the tables. The other eight differed by one level at one or two
pixels each, all in the first half second. The same specks, at the same places on the screen, had appeared between two
earlier films that differed only by a log switch that draws nothing. That pointed to a variation between runs, and away
from the tables. A repeat of the pinned film, made unchanged at the head of the next chain, settled it. The repeat
matched the last film from the tables in every pixel of every frame, so emptying the tables changes nothing once the two
dials are held. And it differed from the first pinned film at the same specks. Two runs of one build and one
configuration had drawn different pixels. The variation is real, but it does not come on every run.

The second film dropped the pins, and the reader said "0 kept different". It was not the same picture with finer joints,
though. The close camera frames its subject as drawn, and at level 6 the grazer 65564 is drawn 1.08 m long where level 3
drew it 0.8 m. So the camera stood at 1.62 m where it had stood at 1.2 m, on a bearing turned 30 degrees, and almost
every pixel moved. The close look's framing therefore depends on the skin level, and a comparison across levels needs a
fixed eye. Level 6 also costs a median 0.70 s a frame on the main thread against 0.51 s at level 3.

## The owner's pictures on the agreed look

The coordinator had three of the owner's six pictures made again on the agreed look, the three of 63868's death. The
owner's two choices rest on them: the pale corpse, and the cloud at an animal's death. Pictures 3, 4 and 6 test the
cloud's form where a leaf is eaten. The two departures touch neither the cloud nor how much it lifts the water. A new
camera would have changed their framing and nothing else, so they stay as they were.

On the agreed look 63868 is drawn smooth and round, its tail joined by a fillet, and the camera looks up at it a little.
The findings held through the change. The pale corpse averages 151 of 255 against the grey drain's 87, 1.7 times as
bright, as before. A new row puts the haze in the water's colour at this animal's death. It lifts the water by 12 a
second after the death and by 10 a second later, where the haze as it was lifts it by 9 and 8. It reads as a soft teal
veil of matter round the corpse.

## The cloud, seen with nothing living behind it

The cloud that 0157 measured forms where 65564 eats 78,610, and 78,610 is a leaf. An eaten leaf leaves no corpse, so the
only way to look at the cloud alone was to aim the camera at the spot. A new dial, `EVOSIM_THEATRE_FILM_CLOSE_AT`, stands
the still close camera at a bearing, an elevation and a distance from a point in the world. The run's position records
gave the bearings with open water behind the spot. The haze's log put the cloud 2.45 m and 2.53 m from the two cameras,
against the 2.5 m asked. So the records and the theatre's world agree.

Looking down, with the bed behind it, the haze in the water's colour reads as murk: a dull grey-teal smudge where the
leaf was. Looking level, with the distant crowd behind, it reads as a pale column lighter than the water, and could pass
for a glow. What lies behind the cloud decides which it reads as.

## A guard that a lost character skipped

Each change to the render script goes through a small script that asserts its counts, run first on a copy. On phase
128's, the first line lost a character. The default path in it still took effect, and the script edited the real file
before its trial on the copy. The copy, taken a moment before, was untouched, and a proper trial from it matched the real
file byte for byte. The scripts since record the trial's hashes. They write to the real file only when that trial has
passed, the file is unchanged since, no chain is running, and the result equals the trial's.

## Open

- What is drawn in the first half second of a film that is not seeded. A speck that varies between runs weakens every
  "identical" the opening check relies on for films that start there.
