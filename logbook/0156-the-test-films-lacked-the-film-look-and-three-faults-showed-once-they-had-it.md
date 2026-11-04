# 0156 — The test films lacked the film look, and three faults showed once they had it

**2026-11-04**  ·  The theatre's death look and death cloud, phases 118 to 126

Until the evening of 2026-11-03, the theatre's test films did not use the look the films are meant to have. The theatre is
the program that films the farm's recorded worlds. The look is a set of dials the owner has agreed, one ruling at a time.
The test films took theirs from tables of their own, which had fallen behind. On 2026-11-03 one script began handing every film the
agreed look. Within a night, three faults came out that the old tables had hidden.

- A close-up camera lost its subject under the surface.
- The pale corpse shone near white.
- The cloud a death releases showed as orange coins.

The camera and the corpse are fixed. The cloud's colour is fixed, but its shape is not, and it now waits on a trial.

## Why the test films were off the look

The agreed look lives in one file, `scripts/theatre-film-look.json`, since 2026-11-03 at 11:13. It holds one dial per entry
with the ruling that set it, in three parts. The wide part is for every shot and the events part for births, bites and
deaths. The close part is the close look, the look for a near portrait of one body. Before the evening of 2026-11-03,
every test film on the render worker took its dials from the render script's own tables instead. That script steps
through one film after another on worker 7, and I call each run of it a phase.

Commit c8ec407b, at 19:33 that evening, added a reader that sets every dial of the agreed look the environment has not
already set. The first phase to use it was phase 118. Every test film through phase 116 ran without it. A script that
compares a film's dials with the look's (`scratch/theatre-realism/tools/offlook.py`) found the following.

- The tables had no close look at all. They lacked the second skin and its band. Their portrait fill was 0.4 where the look
  says 0.8, and their one skin was at level 3 where the look says 6.
- Each film also missed some of the ruled events. Phase 116's film of 63868's death had no bite crumbs, no wound plume, no
  birth cloud and no gape before a bite.
- A few dials joined the look after those films ran, so those films could not have had them. These are the lineage hue and
  join 2. I do not count them as missed.

The first phase on the reader then failed for a reason of its own. Its check before any film draws a fixed shot with every
dial off and compares it, pixel for pixel, with a reference. The reader had just taught that shot the film look. A switch
that tells the reader to set nothing (`EVOSIM_THEATRE_FILM_LOOK=0`, commit 6bc6acb1) fixed it.

## The close-up lost its subject at the surface

With the close look on, a ten-second portrait of 63868 lost its subject. I had suspected two of that afternoon's changes.
Phase 119 turned off join 2 and the no-box rule, and the portrait was still lost. Phase 120 pinned the portrait fill at
0.4, and the subject came back. So the cause was the fill of 0.8.

The camera's own rules explain why. The eye must stay 1 m under the surface. Its search starts from a view that looks 15
degrees down and tilts at most 30 degrees from there. So the steepest it can look up is 15 degrees. At a fill of 0.8 the
eye stands 1.19 m from the body. From that close, looking up 15 degrees puts it only 0.3 m below the body. This body
floated 0.4 m under the surface, so every bearing broke the 1 m rule. At a fill of 0.4 the eye stands
2.39 m off and clears it.

My first fix let the camera tilt further up when every bearing broke a rule (commit 6d6512d6). It found a clean bearing,
and the first frame it took showed a planet. Looking up 30 degrees from 1.19 m puts Snell's window in the top third of the
frame. That is the circle of sky you see through the surface from below, and here it is a smooth pale disc with a hard rim
and bands of orange cloud. Real water does not look like that. The eye was also too close to hold the body still for ten
seconds while it drifted at 0.22 m/s. The corpse filled the frame and then half left it.

The fix that stands (commit f2a4d508) keeps the tilts and stands the eye further off instead: 1.4 to 2.5 times the
distance, up to three body lengths. On 63868 it chose 2.39 m with no rule broken on any of 300 frames, the same eye as
the good film from phase 116. A shot that finds a clean bearing never reaches this second search. To prove it, I filmed
65564's portrait again on the fixed build, and all 120 frames matched the earlier film pixel for pixel.

This is a known limit of the surface shader. Seen from below at a steep angle, the surface shows a pale disc with a hard rim
and banded orange cloud tints. No film looks up that steeply once the extra tilts are gone. A safari camera or a later
shot could, though, and whoever builds one should check the frame for it.

## The pale corpse shone near white

D290 opened a trial of how a dead body looks, because dead tissue in water goes pale and milky. Variant 3, the corpse
that takes the water, turns the skin a dull grey behind a front that crosses the body from its tips inward. Its target
brightness had been set to 0.22 on 2026-11-03, so that the corpse would show at about 0.55 of full brightness in a close
shot.

I first suspected the value never reached the shader, because the corpse looked bright. A new log
(`EVOSIM_THEATRE_DEATH_MILK_LOG`) showed it did. The test that settled it compared the same eye and the same seconds with
the death look off, where the body drains to grey. Five seconds after death the grey corpse measured 79 to 91 of
255, and the pale one 177 to 180, about 0.70 of full brightness.

The reason was the order of two changes. The 0.22 was set before the films gained a fill light by the camera. I re-keyed
it to 0.13 for that light. The corpse now shows at 135 to 150, about 1.7 times the grey corpse and near the 0.55 it was
meant for. It reads as pale dead tissue and does not glow.

Beside a photograph of a dead sea bream on the seabed it is still too even. The real fish is cooler and darker toward its
edges, while ours barely shades on the side away from the light. I think that is the fill light flattening it, but I have
not measured it.

## The orange coins were the death cloud

In one close shot, large soft orange discs crowded the water behind 65564. Turning off the wound plume did not remove
them, and nor did turning off the crumbs. The run's own records then named the cause. The lineage file holds a row for
every birth and death, and a positions file holds where each body was. From 22,514 s to 22,528 s, no birth happened
within 16 m of 65564. One death happened 0.7 m away: 65564 ate 78,610 at 22,525.5 s. The film agrees to the half second:
no discs at 22,524 s or at 22,525 s, and a clump at 65564's mouth at 22,525.5 s. With the death cue off (phase 122) the
discs were gone.

So the coins are the cloud D281 asked for, a dull cloud of matter released at a death and drawn as the bite crumbs are. At
the close look it is neither dull nor crumb-like. Each mote takes the dead body's paint, drained partway to grey and
unlit. In dim water at depth that paint is brighter than the water around it. One measured (78, 78, 56) against water at
(5, 64, 97). The motes are also a fortieth to a twenty-fifth of the body's size, 25 to 35 mm across their radius here. At
the close look a pixel is about a millimetre, so that is three times a crumb.

A new dial, `EVOSIM_THEATRE_DEATH_MOTES`, tried the colour and the size apart. The table gives each form's brightness
against the same region with no cloud, 0.17 s and 1 s after the kill.

| Form | Brightness against no cloud | What it looked like |
|---|---|---|
| D281's motes | +10, +4 | orange coins |
| Crumb-sized, the paint as before | not measured | a swarm of orange spots |
| Half the water's own colour | not measured | soot or ink |
| 1.25 of the water's colour, same size | +13, +5 | a haze of teal discs |
| 1.1 of the water's colour, same size | +10, +4 | a haze of teal discs |
| 1.1 of the water's colour, crumb-sized, four times as many | +8, +3 | a speckled haze of dots |
| The haze form already on trial | +1, +1 | almost nothing |

Keying the colour to the water removed the orange. No brightness made the motes read as a cloud, though. At every step
they were round dots of one size and one edge, and you could count them. The fault left is their shape. The only open
photograph of a released cloud that we found is a dugong raising sediment, one continuous billow. It is a loose guide to
tissue, and D290's pale and milky dead tissue points the same way. A trial is proposed and not filmed: the haze form,
which already billows and breaks into lobes, made visible and keyed to the water
(`scratch/theatre-realism/death-look/motes-shape-proposal.md`).

## What changes, and who decides

Four changes are on the theatre's branch, `theatre-look-merged`, and not yet on main. They are the camera's back-off, the
milk log, the corpse's key of 0.13, and the motes' dial at 1.1. The pictures went to the owner in
`scratch/owner/death-look-and-motes/`. They show the grey corpse beside the pale one, the pale one beside the sea bream,
and the cloud in four forms and none.

Whether a death releases a cloud at all is the owner's call. So is the pale look itself, which D290 left open.

An aside: I lost about a third of the night to camera geometry I could have done on paper. The arithmetic for the steep
tilt was right. It cleared the surface by a few centimetres. I had not asked what the camera would be looking at once it
got there.
