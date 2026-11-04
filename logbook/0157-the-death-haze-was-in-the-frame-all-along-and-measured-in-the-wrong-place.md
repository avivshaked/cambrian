# 0157 — The death haze was in the frame all along, and I had measured it in the wrong place

**2026-11-04**  ·  The theatre's death cloud, phase 127

Entry 0156 left the death cloud's shape to a trial. The cloud is the matter a body releases where it dies, which D281
asked the films to show. Its first form drew round motes, and at the close look they read as coins or dots at any
colour. The second form already on trial was a soft haze of puffs with broken edges. I had measured it at one level of
brightness above the water, out of 255, and called it almost nothing. The trial was to make that haze visible and draw
it in the water's own colour.

## The haze was there

A new log (`EVOSIM_THEATRE_DEATH_HAZE_LOG`) counts, every half second, how much of each haze is in the frame. At the
kill that 0156 filmed, 65564 eating 78,610 at 22,525.5 s, the haze was never missing. One second after the kill, all ten
of its puffs were on screen, 1.5 m from the camera, about 14 cm across their radius and about half opaque.

So the fault was mine. The sheet measured each form inside one box, and I had placed that box where the motes went. The
haze rises elsewhere, around 65564's head and a pink body behind it. Measured where it lies, it lifts the water by 4 to
8 levels as it was. In the water's colour at twice the opacity, it lifts it by 5 to 10. The table in 0156 gives it "+1, +1,
almost nothing", and that row measured the wrong part of the frame.

| Form, in the haze's own region | 0.17 s after the kill | 1 s | 2 s |
|---|---|---|---|
| D281's motes | +17 | +8 | +3 |
| The haze as it was | +4 | +8 | +6 |
| The haze in the water's colour, twice as opaque (`EVOSIM_THEATRE_DEATH_HAZE` 2) | +5 | +10 | +8 |

At full size the haze in the water's colour is the only form that reads as one cloud: a soft teal veil, with no dots
and no orange. The haze as it was keeps that shape but stays a faint brown. Most of either sits behind 65564 from the
first second on, because the close camera follows the killer and the cloud forms at its mouth. The veil is a little
lighter than the water. Beside a living body, that lightness can read as the body's own glow. A shot held on the dead
body would settle which it is.

## A death with no cloud

The owner may decide that a death releases no cloud. Until now the only way to film that was to turn off the whole death
cue, which also turned off the pale corpse. A new switch (`EVOSIM_THEATRE_DEATH_NO_CLOUD`, off by default) draws no
cloud and leaves the drain and the corpse's look on. On 63868's death of old age, against the film 0156 showed, the two
are identical before the death. They differ only where the cloud was for about three seconds after it, and match to
within one level from five seconds on. There the camera holds the corpse, and the old haze shows plainly as a brownish
veil.

## Who decides

The pictures and a recommendation went to the owner in `scratch/owner/death-look-and-motes/`. If a death releases a
cloud, the coordinator and I recommend the haze in the water's colour. Whether it releases one at all is the owner's
call, and so is the light on the pale corpse, which 0156 left open.

The code is commit a3901f98 on the theatre's branch, `theatre-look-merged`, and is not yet on main.

I placed that box where I expected the cloud to be. A one-line log would have shown me where it was, and in the end it
did.
