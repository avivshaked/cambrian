# Proposal: matter that is fed and lost, so a larger tank holds oases and not soup

*2026-10-14. On the owner's question the same day: could round 60 concentrate the matter and enlarge
the tank, so that life grows in micro-biomes but the tank is not a soup of creatures, as the ocean is
not. For the owner's ruling; absorbed into DECISIONS.md on ruling, then deleted. The design study
behind it, with its sources, helper scripts and ledger readings, is `scratch/r60-patchiness/report.txt`.*

## 1. Why the tank is soup

Round 45 (D108, D109) made the tank ten times larger and seeded the matter as islands. The islands
founded the world and then spread: the current smears the matter grid at about 0.1 m²/s, so a 30 m
island is gone in about 9,000 s. Round 57 shows the end state. Its matter is a closed stock: what sinks
is stored on the bed under every column and comes back up, so the whole floor acts as a source. From
12,000 s on, no 10 m square of r57-s2 is empty.

The budget is the deeper cause. The commonest leaf of r57-s2 at 48,000 s breaks even at about
0.00025 units of spent matter per cubic metre (`scripts/ledger.ps1`, -0.018 W at 0.0001 and +0.008 W
at 0.0003). The 15,000 units spread evenly are about 100 times that. Once mixed, the water pays
everywhere, and the crowd fills it. The bodies also carry matter: they wander 28 to 38 m in a life
of about 1,000 s, spreading at 0.2 to 0.4 m²/s, faster than the field itself.

## 2. How the ocean avoids it

The ocean's patches are a steady state, not a starting condition. New nutrient arrives in a few
places: upwelling on coasts and around islands, vents, river mouths, fronts and eddies. It is lost
everywhere, by sinking out of the light and by burial. Most of the open ocean is a nutrient desert,
and the reef islands of the Pacific raise the phytoplankton near them by up to 86% out to about
30 km in water otherwise barren (Gove et al. 2016, *Nat Commun* 7:10581). The study's table gives a
fetched source for each mechanism.

A patch fed at a point and losing matter at rate k has a size of about √(D/k), D being the mixing.
With round 57's settling, the matter's halo is about 24 m; the crowd around it reaches 25 to 35 m
further, the distance a body wanders in its life (both the study's inference from the measured
numbers).

## 3. The proposal: islands that are fed

D109's islands are kept and become the sources. D074's influx is deposited every step into the island
columns' top 12 m, each column weighted by its starting stock, and D074's burial takes matter from
each column's own floor cell. The tank then holds a flow rather than a stock: the crowd is set by the
influx (about 2,100 bodies per unit a second for each pass a unit makes through a body before burial),
and the stock levels near 20,000 s.

Two small builds in Core, a few hours with their tests:

- **The influx on the islands.** `MatterInfluxAt` gains the value `Islands`. Enums are stored by
  name, so no field is added and every older config and checkpoint still loads.
- **Burial at each column's floor.** Burial now takes the grid's bottom layer, which on round 57's
  sloped bed exists only under the deepest hollow, so it would act almost nowhere. Taking each
  column's lowest live cell is what D074 meant on a bedded tank; a flat grid is unchanged to the bit,
  and no bedded run has ever buried.

It is a new realisation of every seed. Influx and burial run on the host, so the card should need no
port; the first screen confirms it.

## 4. The tank

The mechanism can be screened at round 57's 22,000 m², whose seeds already hold two or three islands
100 to 134 m apart (seed 1's pair at 70 m will likely merge). Several separate micro-biomes need about
four times the area, 88,000 m² (radius 167 m), with islands at a 90 m wavelength: about four to six
oases, 30 to 100 m of desert between their crowds (inference). The crowd stays near round 57's,
because it follows the supply, so the physics costs about the same; the larger grids add an estimated
10 to 25% of wall. Two of the world's settings would need a ruling at that size: the bed's 41 m tilt
(held or scaled) and the reef count (64 now, which leaves a quarter of today's reef cover over four
times the floor unless raised to 256).

## 5. The screens

10,000 s each, seeds 1 and 2, on round 59's world: a control, the fed islands (influx 1.2 units/s,
burial 0.001/s), the same with D071's faster settling (0.02 m/s), the same with burial 0.003/s, and
then the best of them at four times the area on seed 1. They are read for empty 10 m squares (round 57:
0.02), the crowd's share inside the islands' footprint, the desert's matter against the break-even,
burial against influx, the wall and the card's memory, and pictures at 5,000 and 10,000 s. About
45 to 70 minutes a group of three on the card; they fit around round 59.

## 6. Alternatives

- **Upwelling plumes.** The closest to the real ocean: the closed stock kept, the bed filled by the
  sinking, the deep returned only through a few rising columns. New current terms on the CPU and the
  card, every config refused, several agent-days, and risks of its own (bodies lifted through the top,
  the crowd thrown into a ring). Worth it only if fed islands prove too static.
- **Wandering sources.** The fed islands, drifting or re-drawn every so often, as vents and whale
  falls are born and die. This puts a price on moving between patches, which is the grazing campaign's
  question. One new field, so every older config is refused. Proposed for round 61, one change a round.

## 7. Questions for the ruling

1. Is the tank's matter an open budget, fed on the islands and buried at each column's floor, rather
   than a closed stock?
2. Is the larger tank four times round 57's area, or another size, and with the bed's tilt and the
   reef count scaled with it?
3. May the agent choose the influx and burial within the screened range (1.2 units/s; burial 0.001 to
   0.003/s; settling 0.002 or 0.02 m/s) from the screens' readings?
4. Do the founders start in the islands, as in round 45, or where round 57 places them?
