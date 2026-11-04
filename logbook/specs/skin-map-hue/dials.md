# Three dials to make the hybrid's hue show on screen

On screen the hybrid hue spread the animals less than it did on paper (`README.md`). On 2026-10-30 the coordinator asked
for dials that would make the map show. Three were built, each off by default and identical to the pixel at 0:

- `EVOSIM_THEATRE_SKIN_MAP_TURN_ALL` (`69e2928f`) gives the map's palette turn to the animal colours that did not take it.
- `EVOSIM_THEATRE_SKIN_MAP_SAT_FLOOR` (`69e2928f`) floors the base saturation of a non-producer.
- `EVOSIM_THEATRE_SKIN_MAP_DEEPEN` (`e827f5c8`, branch `theatre-look-skinmap-deepen`) brings a non-producer's body lightness
  down toward 0.42.

## What the trace found

`colour-sources.md` lists every colour source of an animal in the film look. Nearly all of them already take the turn. The
maze has no colour of its own: the shader draws it from the body colour, darker. Only one constant colour does not take the
turn. That is the rim band's warm stripe on a consumer's box part, which carries on over the part's sides and draws the pale
tan-cream on every eater. The pastel itself comes from lightness. `Muted` fixes each tissue's lightness, at 0.64
to 0.66 for bone, nerve and the absorber, and only caps saturation.

## The stills

These are the 30 unrelated animals of the hue sheet, plus six of one r62-h6202 family. They were drawn in nine looks, with
the film look's rim band, iridescence and countershade on, and read with `scripts/hue_dials.py`. The colour distances are
CIELAB delta E between mean body colours. The kin pairs are the family's five parent-child pairs. The pink band is the
rendered hue between 310 and 10 degrees, within 30 of pink (340).

| look | rendered hue spread | pink band, of 30 | mean saturation | kin, median (max) | strangers, median |
|---|---|---|---|---|---|
| today (map 1) | 49 | 19 | 0.44 | 3.3 (32.2) | 42.2 |
| today, deepen 0.5 | 48 | 20 | 0.47 | 3.5 (27.8) | 44.8 |
| hybrid (map 2) | 53 | 17 | 0.43 | 3.8 (10.4) | 47.3 |
| hybrid, turn 0.5 or 1 | 53 | 17 | 0.43 | 3.5 to 3.7 (11.0 to 11.7) | 47.4 |
| hybrid, floor 0.5, with or without turn 1 | 53 | 17 | 0.43 to 0.44 | 3.5 to 3.8 (10.4 to 11.7) | 47.4 to 47.5 |
| hybrid, deepen 0.5 | 52 | 17 | 0.47 | 3.5 (11.8) | 50.2 |
| hybrid, deepen 1 | 51 | 16 | 0.47 | 2.9 (11.5) | 49.9 |

`scripts/hue_dials_diff.py` measured each dial against the hybrid's pixels. Turn 1 moved 13.5 per cent of the opaque pixels
by more than 2 levels. Turn 0.5 moved 11.5 and the floor 8.9, mostly on the bone, nerve and absorber animals. Deepen 0.5
moved 79 per cent. Every dial acts, but only deepen shows at sheet size. The turn shows little because it is relative to the
body's guild, and a pink or magenta eater's turn is small. The band therefore goes from orange to tan-orange and never takes
the palette's hue.

`dials-sheet.png` and `dials-family.png` show today, today with deepen 0.5, the hybrid, and the hybrid with deepen 0.5 and 1.
The turn and floor looks are left off them, since they draw as the hybrid does.

## The reading

The theatre lane picked the hybrid with deepen 0.5. It keeps kin closest and sets strangers furthest apart, and it is a
little less washed out than the hybrid alone. It does not fix what reads painted, which is the hue: most eaters stay pink to
magenta. Beside the owner's best-so-far frame, a rust-red creature, that reads as candy. Today's salmon family looks more
alive than any hybrid row.

That limit comes from the palette's path. The ruled range runs from blue to gold through violet, magenta, pink and red, and
an eater's colours fall in its middle. The path is the owner's to rule, and the question went to them with the sheet on
2026-10-30. The options put were to keep the path, to run it through the reef's tones, or to keep it at less saturation.
The coordinator ruled the same day that the warm band stays as it is, as the eaters' second tone. It is to be looked at
again if a reef-tone palette is chosen.
