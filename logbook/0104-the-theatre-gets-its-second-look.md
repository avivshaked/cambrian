# 0104 — The theatre gets its second look

**2026-09-16, afternoon**  ·  the look's design pass built in one day on the owner's ruling; the menu and the rulings are `specs/striking-theatre-menu.md`; every recording still replays

---

## What was wrong

The owner's "can barely see anything" of the night before was the display chain rather
than the art. The theatre rendered in gamma colour space, with no post-process data, no
volume, no anti-aliasing and an 8-bit target. Its key light was aimed once at wherever the
fly camera happened to start. And the dark-field palette's values sat in the bottom tenth
of the range with nothing to lift them. The water was 0.012, 0.032, 0.048 and a starving
body's face near 0.04. The design pass listed twenty-odd items across the chain, the lights, the
water, the bodies, the tank and the filming. The owner ruled on the five that were world
rules, and asked for all of it now rather than spread over three days.

## What was built, and what the pictures said

The whole day was one loop. Edit, refresh worker 7, render the same second of the same
smoke from five views, look. The smoke was `r39big-m9` at 600 s, with 77 bodies alive. It
took six renders, and each found something.

1. **The chain, with the grade off.** Linear colour space, the post stack, supersampled
   snapshots box-filtered down, the key aimed from behind whichever camera renders. The
   pictures were the old pictures with the markers drawn at twice their size, because the
   camera now renders at double resolution and the markers were stamped in its pixels.
   They are projected in output pixels now.
2. **The grade on.** A vignette, and nothing else visible. An exposure of 0.8 EV on values
   near black is still near black, so the scene had to carry its own light.
3. **The lit top water, first cut.** One flat bright teal from top to bottom, and every body
   a silhouette against it. The backdrop read the water column only a few metres above and
   below the eye, so a side view spanning 45 m got one colour. The close view's fog
   framing, tuned for near-black water, drowned the subject in teal as well.
4. **The second cut.** The gradient was there, and the water was still five times brighter
   than the numbers said. A global shader colour is handed over raw, where the engine
   converts a material colour or a render setting from sRGB in a linear project. Converted
   by hand, the field went dark again.
5. **The third cut, with the rest of the week's list.** The dark field was back, with the
   water lit at the waterline and falling to the deep by e-folds of the light's reach. The
   leaf glowed green through its own tissue with a back light behind it, and the box
   outline was right. The sand was still a dark slab, because the rake that lit its greys
   in gamma space did not in linear, so its strength went from 4 to 7.
6. **The last batch.** The glass, the wet sheen, motion blur for a moving camera, the skin
   genes.

The before-and-after pair went to the owner after the fifth render:
`images/0104-look1-close.png` against `images/0104-look2-close.png`, and the side views.

A seventh render, of round 39's first seed at 1,500 s with 652 bodies alive, was the first
crowd on the new look (`images/0104-r39-s1-t1500-close.png`): a shoal of glowing leaves
with the eaters grey among them, the far ones falling into the lens's blur. It also showed
the glass standing in the census views, where it is marked as something only the inside of
the water sees. The marker had never worked. The skin's furniture is created with the
engine's do-not-save flag, and the engine's object finder leaves such objects out, so the
shafts had stood in every side view since the day they were built. The marker now keeps a
list of itself.

## What the look is now

Every picture from this build carries `look 2` in its label. Setting the grade dial to
zero restores look 1 for a comparison, and the label says so. Nothing under the simulation's
source tree moved, so every recording replays.

The chain is linear colour and a global volume built in code. The volume carries neutral
tonemapping, so a hue stays its hue as it rolls off, with 0.4 EV of exposure, contrast 8,
a mild bloom, a vignette, depth of field held for a portrait and camera motion blur for a
flight. Every camera is anti-aliased, snapshots are rendered at twice the output and
averaged down, and ambient occlusion runs as a renderer feature. Every number is a dial,
listed in `TheatreGrade.cs`.

The lights follow the viewer. The key and the fill turn with it, every frame in Play mode and around
each snapshot render, and are put back after. Both are stronger for linear space. A
portrait gets a third light behind the subject.

The water is a column, with the lit shallow at the waterline, the deep below, and the
reach of the surface light between them. Every surface's fog mixes towards the column's
colour at the height it is seen through. The backdrop is a skybox painting the same column
by the direction of the look, and the ambient is graded from it. A dial flattens the whole
of it back to the field the theatre had before.

On the bodies, the guild's hue reaches the face only as light coming through the tissue.
A producer transmits, an eater does not, a strut is between. Absorptive tissue gets a wet
sheen, which is a highlight and never an organ. A big part carries finer wrinkles than a
small one, and a near-cubic box keeps its corners. The seed every per-body choice is drawn
from now comes from the body plan rather than the creature's id. A child that inherits its
parent's plan therefore wears its parent's skin. The genome's own version is a proposal for
a round boundary.

In the atmosphere, the shafts and the snow soften against whatever is behind them, and
snow at the lens fades in. The tank's wall is a faint sheet that brightens at a grazing
angle, drawn for a viewer inside the water only.

Two keys are new. `L` holds the pace at or under real time for filming, and `X` shows the
raw collider shapes.

## Two things learned that will bite again

A global shader colour is not converted to linear, where a material colour is. Every global
colour in a linear project is converted by hand, and the first picture after a new one is
the check.

A render is a sixth Editor, and its start-up is what spins the machine up, where the replay
does not. The owner heard the fans three times in forty minutes. The snapshot script now
runs the render Editor at four job workers, and the owner's rule is pictures or a test suite
on top of five arms, never both.

## What is not done

The Recorder pipeline and its overlay bug wait for the safari's build, where they belong.
The genome version of the skin genes is a proposal and waits for a round boundary. The
round 39 pictures on this look come as the seeds land.
