# The mushroom reef: a rock with a cap that shades (round 47)

*Fable, 2026-09-23 afternoon, from the owner's description in conversation ("the reefs I have
in mind are like mushrooms made of rock (or ground), so the top creates a shadow. if you
prefer we can have floating islands if that's better") and the agreement that the beach is
round 46 and the reef the round after. What to build so that a tank holds a few rock columns
with overhanging caps: a lit table on top where snow lands, a shaded room underneath, a stem
to go round. A floating island is the same object with its cap at the surface and no stem,
and both come from one piece of machinery. Off by default so every recorded world replays.
The beach spec (`beach-spec.md`) is the pattern, and the fade it built for the shore is the
tool this spec reuses for the rock. Nothing here is built; the round-46 read decides what the
reef is for.*

## 1. The shape

A reef is a solid of revolution about a vertical axis at `(x, z)`: a stem of radius `r_s`
from the floor to the cap's underside at depth `d_cap + t`, and a cap of radius `r_c` and
thickness `t` whose top lies at depth `d_cap`. The rock is the union of the two cylinders,
with the cap's rim rounded by `t/2` so a body slides off rather than catching. `r_s = 0`
with `d_cap = 0` is a floating island: a disc of rock at the surface with nothing under it.

A tank carries `ReefCount` of them, placed by the run's seed uniformly over the disc with the
caps at least `2·r_c` apart and `r_c + 5 m` from the glass, over water deep enough for the
stem (a reef is refused where the floor under it is shallower than `d_cap + t + 2 m`, so the
beach's shelf carries none). The round's dials, all in one `reef` group and refusing every
earlier `config.json`: `ReefCount` (0 = off, the recorded world), `ReefCapRadiusMetres`,
`ReefCapDepthMetres`, `ReefCapThicknessMetres`, `ReefStemRadiusMetres`, `ReefFadeMetres`
(section 3). A first set to picture, not to pre-register: three reefs, caps 8 m across at
3 m under the surface, 2 m thick, stems 2 m across.

## 2. What the rock is to each system

- **The grid.** A cell whose centre lies inside a reef is dead, as a cell under the floor is
  (D092's mask, item 9 of the bed spec), and no flux crosses a face into it. This is the one
  place the grid's shape changes: a column can now have water above the cap, rock, and water
  below, and `GridField._lowestLive` (one lowest live layer a column) becomes a list of live
  intervals a column. Settling stops at the lowest live cell of the interval the snow is in:
  snow above the cap lands on the cap's top, in the light, which is the table; snow below the
  cap falls to the floor as it does now. Mixing and transport already run face by face and
  need nothing. The matter grid's 5 m cells take the same mask by centre; a cap thinner than
  a matter cell casts no matter shadow, and that is stated rather than fixed.
- **The light.** The cap is an opaque body in `LightField`: at world start it contributes a
  shadow of its full disc at its depth to every column it covers, once, as a body's
  silhouette is contributed every step (`Field.Contribute`), so the water under the cap is
  dark by the same arithmetic that darkens the water under a leaf. This is the shade that
  something casts, the form the owner asked for against the painted map (D109). The stem
  casts nothing (it is vertical). The cap's top is lit at its depth as any water is.
- **The current.** The streams' potential is multiplied by a fade `g(p)` that is 0 inside the
  rock and 1 beyond `ReefFadeMetres` from its surface, quintic between, the shore fade's
  construction (`beach-spec.md` section 3 as built): the velocity is `g·u + ∇g × A`, still
  divergence-free, with no flux through the rock because `g` vanishes there and `∇g × A` is
  tangential to `g`'s level sets. The closed-form acceleration takes the extra terms as the
  shore's does. The cost the beach measured is the price here too: the second term is a
  current along the rock's contours of the order of `|A|/fade`. The beach's sweep (its
  spec's section 3 as built, 2026-09-23) read that current on round 45's tank at 15 m of
  fade: a plain quintic in the distance put the water in the crowd's band at 2.9 times the
  tank's RMS on average and 11.4 at its fastest, the contour term alone at 2.0 and 11.4, and
  the beach shipped a product of the quintic with a turnover in the depth, which read 1.1
  and 4.2 in the band. A rock has no depth to turn over, so the reef gets the plain form and
  its reading, and the plain form only falls to the RMS's order at 40 m of fade (1.2 in the
  band, 0.5 for the contour term). The refusal binds first: a fade longer than half the
  caps' spacing makes the tank still, so `ReefFadeMetres` is under half the minimum
  spacing, and the smoke prints the fastest water within one cap radius of each reef, which
  is the number to read before the round. Three caps 8 m across in an 84 m tank can be
  spaced 40 m apart, which allows a fade under 20 m and a band at about twice the RMS around
  the rock (the plain form at 25 m read 1.7). The pre-registration says whether that is a
  feature of the reef or a cost to be paid down with fewer, wider-spaced caps.
- **The contact.** The rock is a static shape in `Contacts`: a sphere against a capped
  cylinder is a signed distance with a closed form (the stem's axis, the cap's slab and its
  rounded rim), and the push is the bed's law with the bed's material, on the existing
  `bedOrGlass` count. A body on the cap's top rests as on a floor; under the cap it is pushed
  down by the underside as the surface clamp pushes up; against the stem it is pushed out as
  the glass pushes in.
- **The placer.** A reservation inside the rock or within a body's radius of it is refused as
  one under the floor is (`PlacementFloor`'s rule extended with the reef's signed distance);
  D109's founders-in-matter rule is unchanged. A founder over a cap lands on it.
- **The divergence guard.** A root inside the rock by more than its radius dies as a counted
  `Diverged` death with a dump naming the reef, as the radius guard does.
- **The theatre.** The skin draws each reef from the same dials with the sand material for the
  stem and a rock material for the cap, and the `-From snapshot` reader takes the dials from
  the config as it takes the bed's. Pictures of the smoke from the side and from under a cap
  are looked at before the pre-registration.

## 3. What is refused, and what is not built

Refused: a reef in a box (the streams and the mask are the tank's); a reef whose cap would
break the surface (`d_cap` under 0.5 m, unless `r_s = 0`, the island); a stem in the beach's
shelf; a fade over half the spacing. Not built: a reef with a hole through the cap, a
cap that is not a disc, rock that erodes, snow sliding off a cap (settling stays vertical, so
a cap's table keeps what lands on it and remineralises it there), and any change to the eaters,
the prices or the senses.

## 4. Tests

1. The mask: a column through a cap has two live intervals; the live cell count equals the
   tank's less the rock's cells within a cell ring; with `ReefCount` 0 the mask is the
   recorded one bit for bit.
2. The constant field stays uniform over 600 s at the campaign's mixing and current with
   three reefs, and each cap column's totals hold (the beach's test 3 method).
3. Settling: snow seeded above a cap ends on the cap's top cell; snow seeded below ends on
   the floor; nothing in rock.
4. The faded streams: divergence, floor, glass and rock normal fluxes at D092's orders; the
   water inside the rock exactly still; the acceleration analytic within 0.1% of the stencil
   (the beach's test 2 method); the fastest water within a cap radius printed.
5. The light: the irradiance directly under a cap's centre equals the surface's attenuated
   by the cap's shadow, and beside the cap it is unshaded.
6. Contact: a sphere released above a cap settles on its top; one released under it against
   the underside is pushed down; one against the stem is pushed out; a body cannot be placed
   in rock.
7. With `ReefCount` 0 the crowd fixture's regress is identical in every field.
8. A 600 s smoke of round 46's launcher with three reefs closes both books, prints the reef
   readings, and its pictures are looked at.

## 5. What the round reads

Where the snow lies: on the caps' tables against the open floor and the beach's shelf. Where
the eaters and the plants sit: over, under and beside the caps, from the positions file
against the reefs' places in the config. Whether anything lives in the dark room under a cap,
and on what. The pace, and the fastest water at the rock.
