# 0159 — The first-second specks come with the death cue

**2026-11-04**  ·  The theatre's test films, phases 131 and 132, and D301 brought into the look

Entry 0158 left one thing open. Two runs of one film, on one build and one configuration, had drawn different pixels in
the first half second. They differed by one level of brightness, at one or two pixels a frame, at the same few spots on
the screen. A third run matched the first in every pixel. So the variation was real, but it did not come on every run.

That weakens a sentence in 0156. There I filmed 65564's portrait again, to prove that a fix to the close camera left a
good shot alone. All 120 frames matched the earlier film pixel for pixel. They did match. A match on one run says
less than I took it to say, though, when the same film can differ on the next. The proof landed on a run where the
frames happened to agree. What it proves still holds, because a fault in the camera would have moved every frame and
not a few pixels in the first half second. It is one sample of a film that varies, and I wrote it as a proof of identity.

## Which part of the picture varies

The film is 4 s of the grazer 65564 from 22,524 s, in round 65's world. The window it is filmed from records 127 births,
deaths and kills between 22,518 and 22,525 s. That runs from six seconds before the film's first frame to one second after it. Nineteen of
them fall in the film's first second, and a death falls on its first frame. Nothing else in the theatre was found to
be unseeded. So the diagnostic switched the event effects off one family at a time and filmed each variant three times.

| Variant | Runs | Pairs that differ | What differs |
|---|---|---|---|
| A, every effect on | 3 (made earlier) | 2 of 3 | 8 frames, all by frame 14, 1 level, 1 to 2 pixels |
| B, the birth and bite effects off | 3 | 3 of 3 | 9 to 14 frames, all by frame 14, mostly 1 level and 1 to 4 pixels |
| C, the death cue off | 5 | 0 of 10 | nothing: every frame identical |
| D, every dial off (the check that opens every chain) | every chain today | none | nothing |

Variant B turned off the crumbs, the wound plume, the plant fragments, the bite bubbles, oxygen pearling and the birth's
cloud and bubbles. Its runs still differed, so these effects are not the only source. Variant C turned off the death cue
with its remains and their crumbling, and its five runs drew the same pixels every time.

Five identical runs are a lead and not a proof. In A, only one run in three drew the specks. At that rate, a variant
that still carried the cause would come out five alike about once in seven tries by luck alone. The chance is
(2/3)^5 + (1/3)^5, or 0.14. Step 0's identity, which compares a fixed shot with every dial off, has the same caveat
for any film that starts on a death. It holds on the shot it checks, because that shot draws no death cue.

## The odd thing about B

Turning the birth and bite effects off made the specks more frequent and larger, where I expected them to vanish or stay
the same. B drew them in every run, against A's one run in three. One B run carried a speck of 6 levels across 8 pixels
in frame 10, at a spot the A films never showed. Another carried one of 2 levels in frame 13. A's specks never passed 1
level. I read this, as inference, as the death cue's variation landing on whatever else is drawn at that spot. Take some
effects away, and what it lands on changes in size and in how often it shows. Three runs cannot tell that apart from
chance.

The cause inside the death cue is not found yet. My guess is something the cue sets up from a body already dead or dying
when the film starts, and does not seed. Finding it is a reading of the code.

## D301 in the agreed look

The owner ruled the death look on 2026-11-04 (D301): a corpse turns pale, and a death releases a soft haze in the water's
own colour. The agreed look now carries both dials (`EVOSIM_THEATRE_DEATH_LOOK` 3 and `EVOSIM_THEATRE_DEATH_HAZE` 2), moved
from the file's trial section to its events. The safari and every film read the same file.

The theatre's code reached main the same day as one squash commit. The line it came from descends from three commits
whose author address the push guard refuses, and a plain merge would have carried them into main's history. The squash
was checked before main moved: its content equals a true merge, its author is the project's address, and none of the
three is among its ancestors.

To prove that the look alone now draws D301, I filmed 63868's death again with the two dials left to the file. The
earlier film set them by hand, and it is the source of the owner's picture of the pale corpse with the haze. The reader
filled every dial from the file and kept none at another value. All 300 frames matched that earlier film pixel for
pixel. The film's first second holds a death with the cue on, where the specks above can come. So the match could
have missed by a few pixels, and it did not. At five seconds after the death the pale corpse matched the
picture's own, all 89,264 of its pixels, at a mean brightness of 151 of 255.

## Open

- What in the death cue varies between runs, and whether it can be seeded.
- The blades of round 65's films. The look file holds one stand-in pair of blade stops for any world without its own
  row, and nothing read the table of rows. So every film of round 65 drew its blades on the stand-in, in a light it was
  not set for. The calibration is queued.
