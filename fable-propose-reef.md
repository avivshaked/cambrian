# Proposal: shade that something casts — the shelf reef, turbidity, and the ideas D109 set aside

*Fable, 2026-09-22 night, from the conversation that ruled D109. Captured at the owner's ask
("Agreed. But capture the ideas for later") for a base round after round 45; not built.
Absorbed into DECISIONS.md on ruling, then deleted.*

## What this is for

D109 seeds the matter as islands and the water stirs them into soup within about 5,000 s: a
stock in moving water cannot hold a shape. The one thing in this world that cannot be stirred
is the light, so a pattern in the light is the one landscape that would last. The first cut
was a map: a noise field multiplying the surface irradiance column by column. The owner's
objection stands and is recorded in D109. Light is a uniform field decaying with depth, it has
no concentration, and a map of shade with nothing casting it is a landscape we designed, which
is the argument DESIGN §5A.0b makes against designed founders. So the map is built, tested and
at 0 (`LightShadeDepth`, `LightShadeDriftMetresPerHour`), and this file holds the forms a
lasting light pattern could take without that objection, for the owner to choose among when a
round wants one.

## 1. The shelf reef (the owner's idea)

"Land islands on the surface connected to a column-like structure. That will give shade over
areas."

**What it is.** In a few places the bed rises to the surface as a rock column, and a shelf sits
on top of it at the waterline: a table. The water under the shelf is dark, since the sun is
overhead and the shelf is opaque; the water beside it is lit in full. The column and the
shelf are solids. The placer keeps bodies out of them, the crowd collides with them, a body
under the shelf cannot reach the surface, and the current goes round them.

**Why it answers the objection the map could not.** The shade is cast by an object in the
world, drawn by the theatre, that the crowd can touch. It continues D092 (a bed with relief)
rather than adding a new kind of thing: a bed that reaches the surface in places is the same
heightfield taken further. And it is a step, 0 beneath and 1 beside, rather than a gradient
nobody can point at.

**What it would take** (about a day, my estimate). The column from the bed's own heightfield
(`BedShape`), raised to the surface inside a disc per column, so the bed contact and the mask
already handle it. The shelf as a disc at y = 0 with a thickness. Its underside is a third
contact plane beside the bed and the glass, a ceiling: a body pushed up against it stops, as
at the surface today. The light's column factor 0 beneath the shelf, through the
`ColumnFactor` hook D109 built. The placer's refusal of a spot inside the rock. The grid's
cells inside the column masked out as the tank's outside is, so no matter sits in rock. The
theatre's mesh for both. The current: the streams are tangential at the glass by construction
and know nothing of a column. The first cut lets the water pass through the rock and says so
in the header; the second adds the column to the potential's boundary.

**The design question inside it: where the shelves stand relative to the matter islands.**
Two different worlds. Shelves over the deserts: the lit water is the island water, the dark
water holds a reserve of spent matter that diffuses into the light, and the crowd lives where
it founded, the deserts kept empty by the light rather than by the matter. Shelves over the
islands: the founding water is dark, so no leaf founds in it, and the islands are a refuge and
a larder for whatever lives without light, which at round 45's kit is a stomach. A leaf then
founds at the shelf's edge and the crowd is a ring. The first is D109's intent made solid and
the second is a different experiment, and which one is the owner's to say.

The dials are the number of shelves (a few), their radius (of the order of the island
wavelength, 30 to 60 m), and whether the column is narrow, a pillar, or the shelf's full
width, a wall. The shade is the shelf's footprint and nothing else.

## 2. Turbidity: the crowd shades its own water

Light in real water is attenuated by what is in it, and the marine snow is in it. The
attenuation depth becomes a function of the snow's density in the column above a body, read
from the snow grid's column sums, which the fields dump already writes. Crowded water is then
darker by the crowd's own doing, and clear water is round 44's.

Nothing about it is designed: the pattern is the crowd's, it moves with the crowd, and it
decays when the crowd goes. It is also a negative feedback on a crowd's density that the
economy does not have yet, since a crowd that makes snow darkens itself.

**What it would take** (half a day). One term in the light field, the layer's transmission
per patch or per column from the snow above it; a calibration of how much snow halves the
light, a ledger screen against round 44's snow and shade columns; and a header token. It is a
per-step change and a new realisation of every seed. The canopy's arithmetic is per layer and
per patch. A per-column attenuation would need the hook the shade map uses, a body's own
irradiance scaled by its column, or a per-column canopy, which is the larger build.

It is no structure that lasts, being as transient as the snow.

## 3. The shade map as built, and its drift

Kept as a dial for a screen that wants to ask what a lasting light pattern does to the crowd
without building a reef. The depth is `EVOSIM_LIGHT_SHADE`: 0 is no map, and above 0 the
islands are lit in full while the darkest desert gets one less the depth of the surface
irradiance, the map saturating across every island column (D109's second cut). The drift is
`EVOSIM_LIGHT_SHADE_DRIFT`, in metres an hour: the map slides along a diagonal so the shade
moves over the seasons of a run. That was the owner's idea from the varied-diffusion
conversation, "the rate can move around slowly between grid cells so over time islands and
deserts shift". A screen's dial and never a round's world unless the owner rules otherwise;
the reef is the world's form of it.

## 4. Two smaller things set aside the same night

**Finer matter cells to slow the smearing.** The islands spread by the gyre's upwind transport
on the 5 m cells, about 0.1 m²/s by a Gaussian fit on `bigF`, whatever the explicit rate. A
2.5 m cell should halve that, and a 1 m cell (the snow's, 1.05 million cells) bring it near
the dial. None of this is measured. The cell is ruled (D086), and the old objection to 2.5 m,
matter stranded in cells too small for a child, no longer applies under D098, since nothing
draws a field at conception. A screen for a round that wants its islands to last.

**A varied diffusion rate** (the owner's idea, withdrawn): matter in the deserts diffusing
slowly, in the islands fast. Rejected in D109 on the arithmetic: Fick's law lets a slow cell
hold nothing in, and the flattener is the transport. Recorded here so it is not proposed
again without the arithmetic.

## What I recommend

The shelf reef, over the deserts, as the next base round's light structure. Screen it first
with the shade map at depth 1 over the same footprint, which is a step with the darkest desert
at 0, to see what the crowd does under a hard shadow before the geometry is built. Turbidity
beside it as the crowd's own feedback, whichever way the reef is ruled.
