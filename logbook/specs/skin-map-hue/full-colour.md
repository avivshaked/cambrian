# Full colour: the whole wheel, a value axis, and OKLab levelling

The owner ruled on 2026-10-30 that an animal's base hue is no longer limited to the marine path from blue to gold. The
coordinator relayed it with a suggestion: the full hue wheel plus a value axis, from two genome features, kin still close,
overshooting the range first. The theatre lane built it on the branch `theatre-look-fullcolour` and drew three sheets that
day. Each is the same 30 unrelated animals and one family of six as `dials.md`, with v4's pick (the hybrid, deepen 0.5) as the
reference look.

## The build

Every dial is off by default. With them off the film look is identical to the pixel, and step 0 of each render checked it.

- `EVOSIM_THEATRE_SKIN_MAP=3` (`f0b4cd6d`) runs the hybrid of mode 2 and puts the base hue on the whole wheel, 165 degrees
  plus 360 times the rank.
- It also gives each body a value rank. That is the rank of a second lineage score: the generalised eigenvector with the second
  largest eigenvalue of the hue score's own problem (`scripts/fit_value.py`, written to `value.json` and from there into
  `TheatreSkinMapConstants.cs` by `scripts/skin-map/gen_constants.py`'s fifth argument).
- `EVOSIM_THEATRE_SKIN_MAP_VALUE` (0 to 1) moves a body's lightness with that rank, toward near black for a low rank and pale for
  a high one.
- `EVOSIM_THEATRE_SKIN_MAP_OKLAB` (`bbbf02ea`) levels the body, rim and light-through colours in OKLab: the body is set to
  one perceived lightness, the other two move with it, and each colour's chroma is capped. Under it the value axis moves that
  lightness. Its lightness, chroma cap and pale reach are dials (`_OKLAB_L`, `_OKLAB_CHROMA`, `_OKLAB_PALE_REACH`, `a69c5a75`).

The second score is heritable but weaker than the first. Its spread over the parent-child spread is 17.0, against the hue
score's 52.4. A parent and child differ in value rank by 0.003 at the median and 0.22 at the 95th percentile, and strangers by
0.31 at the median. It correlates 0.10 with diet and 0.09 with the hue rank. Mostly it contrasts capsule parts with box parts,
with margin and investment after. The C#'s ranks matched the Python's to within 7e-4, about one and a half places in 2,131.

## Three sheets

The read is `scripts/v5_read.py`. Its colour distances are CIELAB delta E between mean body colours, as in `dials.md`. L* is the
mean body colour's CIELAB lightness, and the pink count is the rendered hue within 30 degrees of 340.

**The wheel in HSL** (`full-colour-v5-sheet.png`, `f0b4cd6d`).

| look | hue spread | pink, of 30 | hue sectors, of 12 | L*, median | kin, median (max) | strangers |
|---|---|---|---|---|---|---|
| v4's pick | 52 | 12 | 8 | 71 | 3.5 (11.8) | 50.2 |
| wheel | 61 | 5 | 11 | 91 | 3.5 (8.0) | 43.8 |
| wheel, value 0.5 | 61 | 4 | 11 | 88 | 4.8 (6.4) | 42.0 |
| wheel, value 1 | 61 | 4 | 10 | 88 | 3.9 (8.5) | 38.5 |

The hues spread, but the yellows, limes and cyans drew as near-white neon. HSL lightness is not perceived lightness, so at the
lightness where a magenta reads at L* 70 a yellow reads near 95. The value axis moved a body by up to 22 L* where the body
colour is what shows. On about half the animals it moved nothing, because the rim and light-through colours kept their own
lightness. This sheet did not go to the owner.

**OKLab levelling at L 0.68** (`full-colour-v5b-sheet.png`, `bbbf02ea`). The neon went, and olive, moss and ochre-coral
appeared. But the median body read at L* 84, since the shader's light adds about 15 to 20 to the levelled colour. At value 1
the pale end washed out to near white. Chroma 0.20 against 0.14 set strangers 42 apart against 37.

**Darker levels** (`full-colour-v5c-sheet.png` and `-family.png`, `a69c5a75`), all at chroma 0.20 and pale reach 0.35:

| look | hue spread | pink, of 30 | L*, median (p10 to p90) | kin, median (max) | strangers |
|---|---|---|---|---|---|
| L 0.58, value 0.5 | 62 | 5 | 72 (66 to 83) | 3.7 (10.1) | 46.9 |
| L 0.50, value 0.5 | 62 | 5 | 66 (56 to 76) | 3.6 (9.4) | 48.1 |
| L 0.42, value 0.5 | 63 | 5 | 56 (46 to 76) | 3.2 (9.0) | 47.0 |
| L 0.50, value 1 | 62 | 5 | 71 (53 to 82) | 3.1 (18.0) | 52.6 |

The lane's pick is L 0.50 at value 0.5. Olive and moss bodies show their maze, and there are coral, rose, violet, teal, sky blue
and green animals, and one rust (r62cf-6203 48556). Its largest kin gap, 9.4, is the smallest of any version. The sheet went to
the owner as version 5 of `scratch/owner/genome-to-skin-family-sheet/`.

## What version 5 left

- About a dozen of the 30 render olive to leaf green. They are the hue ranks 0.70 to 0.76 that the marine path drew pink, and
  beside real leaves they would read as plants. A levelled yellow from about 50 degrees renders olive, so the band that reads
  as a plant is wider than the hue numbers suggest.
- At value 0.5 nothing reaches near black or deep brown. Value 1 reaches darker, at a kin gap of 18.
- Bodies that are mostly plant tissue keep the producers' own colours, by the owner's ruling of 2026-10-29, so a few animals
  do not change across any look.

Turning the wheel's start (`_WHEEL_START`, `aa328419`; `full-colour-v5d-sheet.png`) did not cure the green. From 145, 130 and 115
degrees the hue spread rose from 62 to 70 and strangers from 48 to 59. But the green only moved to other animals. A
whole wheel always has a green somewhere, and some lineage lands on it.

## Version 6: four axes

On 2026-10-30 the owner looked at version 5. They found green dominating, and asked where the blacks, whites, silvers, browns,
reds and blues were. The coordinator briefed four separate axes, and the owner asked for animals of several colours, with
Chromodoris as the reference. Both builds are on the branch and off by default.

- **`EVOSIM_THEATRE_SKIN_MAP_AXES`** (`7d305e0c`) sets lightness, chroma and gloss each from its own lineage score. The scores
  are the third and fourth generalised eigenvectors of the same problem (`scripts/fit_axes.py`, into the constants by
  `gen_constants.py`'s sixth and seventh arguments). Their spread over the parent-child spread is 8.97 for chroma and 8.64 for
  sheen; the hue score's is 52.4 and value's 17.0.
  - Hue runs on an arc from 170 degrees forward through blue, violet and red to 55, skipping green. Its dials are
    `_ARC_START` and `_ARC_END`.
  - The OKLab lightness is `lerp(LIGHT_DARK, LIGHT_PALE, value rank)`.
  - Chroma is set, not capped: `CHROMA_MAX * chroma rank ^ CHROMA_POWER`, down to grey.
  - The tissue's sheen is multiplied by `lerp(SHEEN_LOW, SHEEN_HIGH, sheen rank ^ SHEEN_POWER)`.
  - `_BROWN` pulls a body whose hue lies near 42 degrees toward a dark, quiet brown.
- **The multicolour dials** (`68c42448`):
  - `_PART_COLOUR` moves each part's hue and lightness along the arc by its distance from the root.
  - `_PATTERN_TINT` draws the maze in a second colour. `_PATTERN_HUE` sets how far along the arc that colour is, and
    `_PATTERN_FLIP` how far its lightness is flipped across the middle.
  - `_COUNTER_BOOST` makes the countershading stronger.
  - `_RIM_CONTRAST` turns the edge stripe half the wheel from the body.

  The film's one skin copies the largest part's block, so a body carries one pattern tint; part colours blend per vertex.

The read gained a texture measure: the standard deviation of the body's luma less its 4-pixel blur, which keeps the maze, pores
and carve and drops the broad shading. It is given as a ratio to the same animal in v4's pick, and an animal under half has lost
its pattern.

**The first set** (`full-colour-v6-sheet.png` and `-family.png`, eight looks):

| look | hue spread | pink, of 30 | L*, median (p10 to p90) | kin, median (max) | strangers |
|---|---|---|---|---|---|
| four axes, defaults | 69 | 8 | 78 (47 to 94) | 2.2 (24.6) | 44.2 |
| more greys, chroma power 3 | 94 | 5 | 79 (47 to 96) | 2.2 (21.0) | 40.4 |
| brown 1 | 69 | 8 | 76 (44 to 94) | 2.2 (24.6) | 44.6 |
| light 0.20 to 0.82 | 68 | 11 | 76 (49 to 90) | 2.5 (23.1) | 45.1 |
| multicolour, pattern half the arc away | 67 | 8 | 76 (60 to 86) | 2.5 (23.3) | 36.4 |
| multicolour, pattern 0.08 away | 63 | 12 | 76 (57 to 86) | 3.6 (21.6) | 38.2 |

The green went and the range came: near black, near white, rust, slate. Against a strip of photographs of real nudibranchs,
flatworms, worms and octopuses, though, no look read as an animal of the sea.

- The pale half drew candy pastel: 58036, 59232, 62464, 47834, 75707, 58958 and 77286. 83460 drew white, and 58944 and 62119
  icy cyan.
- 54035, 61406 and 57968 drew neon.
- The multicolour pattern turned the mazes into zebra stripes. That was striking on the dark family and toy-like on the pale
  bodies.
- 83460 and 44339 lost their maze.

Only a few were plausible: 43328, 44339 and 54040 in grey-mauve, 48556, and 53866 and 52133 in rust.

**Version 6b, muted** (`full-colour-v6b-sheet.png` and `-family.png`). The lightness ran from 0.04 to 0.62 and chroma to 0.11,
with brown 1. That base was drawn alone, with quiet accents, and with the close pattern at full strength. The accents were a
pattern 0.08 along the arc, flipped by 0.2; part colour 0.5 with part light 0.3; and rim contrast.

| look | pink, of 30 | L*, median (p10 to p90) | texture against v4 | kin, median (max) | strangers |
|---|---|---|---|---|---|
| muted alone | 11 | 62 (31 to 77) | 0.96, four lost | 1.5 (16.5) | 42.5 |
| muted, quiet accents | 16 | 62 (39 to 77) | 1.39, none lost | 2.1 (15.0) | 36.7 |
| dark, close pattern at full strength | 12 | 64 (50 to 77) | 2.55, none lost | 2.7 (13.2) | 29.9 |

Muted with quiet accents was the first look that read like the sea, and the lane's pick; the coordinator agreed.

- Its bodies are dusky rust, brown, taupe, mauve, plum and slate, with the maze in a lighter or darker tone of the same family.
- 53866 and 44339 are rust-brown with a cream maze, the nearest yet to the owner's reference frame.
- 54035 is purple with pale spots and a rust limb.

Its faults:

- Mauve crowds the sheet: 16 of 30 lie within 30 degrees of pink, because the muted colour turns the old red-to-pink crowd mauve.
- 61406, 77286 and 57968 stay vivid violet.
- Strangers sit closer, at 37 against 48 to 50.
- It has no neutral animal at all. A tally of the mean body colours by CIELAB hue, with those under chroma 8 counted as
  charcoal, grey or bone, gives 8 mauve or pink, 7 violet, 6 yellow or olive, 5 red, rust or brown, 3 blue, 1 teal and no
  neutral.

Muted alone has the most natural colours, but the four darkest animals lose their maze: 44339, 43328, 53866 and 52133. The
full-strength close pattern shouts, at 2.6 times v4's maze contrast, and leaves glaring white limb tips on 44540, 54040 and 53866.

## Version 6c: a warm middle and neutral ends

The coordinator's next brief, 2026-10-30, asked for three changes:

- land the mauve crowd on rust and brown;
- quiet the three violets;
- show animals at both ends of lightness at low chroma: charcoal, bone, and pewter with the sheen. The owner had named black,
  white and silver.

Three dials, off by default (`0354b3e9`), make that possible:

- **`_ARC_MID`** sets the hue for base hue 0.5. The arc is then two linear halves. The crowd sits at hue ranks 0.5 to 0.8, so a
  middle at 0 degrees spreads the upper half over red, rust and brown, while the lower half keeps cyan, blue and violet.
- **`_BROWN_WIDTH`** sets the brown band's half-width, 30 degrees by default.
- **`_NEUTRAL`** multiplies the chroma by `1 - n * max(wv, ws)`, where:
  - wv rises smoothly from 0 to 1 as `|2 * value rank - 1|` goes from 0.3 to 1, so the lightness ends go grey;
  - ws rises as the sheen rank goes from 0.75 to 1, so the glossiest animals go silver or pewter.

  The pattern tint's chroma floor scales by the same factor, so a grey animal gets a grey or bone maze.

The sheet draws three looks on muted with quiet accents. The middle sits at 345, at 0 and at 15 degrees, the last the overshoot.
Each look also widens the brown band, lowers the chroma top to 0.10 or 0.095 and sets the neutral dial to 0.6 or 1. Each lifts
the pale end toward bone, from 0.66 to 0.74.

| look | mauve, pink | red, rust, brown | yellow, olive | blue | violet | neutral | L*, median (p10 to p90) | texture against v4 | kin, median (max) | strangers |
|---|---|---|---|---|---|---|---|---|---|---|
| v6b's pick | 8 | 5 | 6 | 3 | 7 | 0 | 62 (39 to 77) | 1.39 | 2.1 (15.0) | 36.7 |
| middle 345, neutral 0.6 | 8 | 8 | 6 | 3 | 2 | 3 | 62 (40 to 77) | 1.41 | 2.1 (17.0) | 33.6 |
| middle 0, neutral 1 | 4 | 10 | 6 | 4 | 2 | 4 | 60 (42 to 77) | 1.40 | 2.1 (18.5) | 32.6 |
| middle 15, neutral 1 | 1 | 9 | 9 | 4 | 2 | 5 | 58 (42 to 81) | 1.43 | 1.8 (20.4) | 32.5 |

(`full-colour-v6c-sheet.png` and `-family.png`.) The middle at 0 degrees was the lane's pick.

- The crowd went warm. 61406 drew terracotta, 54035 red-brown with pale spots, and 43328, 54040, 77273 and 75707 brown.
- Slate and blue stayed: 50990, 44540 and 59232.
- The first neutrals appeared: 44339 near-black brown, 48556 and 58036 pewter, and 83460 white.
- No animal lost its maze.

Its faults:

- 83460 glares white, since the lifted pale end made it the brightest body on the sheet.
- 62119 (lilac-pink) and 57968 (indigo) still look painted.
- 62464 and 53356 stay pale mauve, and 58944 icy blue.
- Only 4 of 30 are neutral. The pale animals' value ranks sit at 0.77 to 0.82, where the value weight is only about 0.3, so
  the greying edge (0.3 on `|2v - 1|`) starts too late for them.

The middle at 15 cured the mauve but tipped into a brown-and-olive sameness with fewer reds.

## Version 6d: blacks and quieter violets

Version 6d answered 6c's faults with three more dials, off by default (`b11b5b0c`):

- **`_NEUTRAL_EDGE`** moves the start of the greying weight on `|2v - 1|` from 0.3 to the dial's value, so animals nearer the
  middle of the lightness range grey too.
- **`_DARK`** pulls a low value rank toward `LIGHT_DARK` and scales its chroma down with it, so the darkest animals go
  near-black instead of deep brown.
- **`_VIOLET`** cuts chroma in a raised cosine centred on 290 degrees, 45 degrees either side.

All three looks sit on 6c's pick, with the dark end at `LIGHT_DARK` 0.04 (0.02 in the overshoot):

| look | edge | dark | violet | mauve, pink | red, rust, brown | blue | violet | charcoal | grey, pewter | texture against v4 | kin, median (max) | strangers |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| v6c's pick | | | | 4 | 10 | 4 | 2 | 1 | 2 | 1.40 | 2.1 (18.5) | 32.6 |
| mild | 0.15 | 0.5 | 0.4 | 5 | 6 | 4 | 2 | 1 | 4 | 1.44 | 1.8 (21.7) | 31.8 |
| middle | 0.12 | 0.8 | 0.6 | 4 | 5 | 5 | 1 | 2 | 4 | 1.44 | 1.7 (24.2) | 31.8 |
| overshoot | 0.10 | 1 | 0.8 | 4 | 5 | 5 | 1 | 2 | 4 | 1.44 | 1.5 (25.8) | 32.9 |

(`full-colour-v6d-sheet.png` and `-family.png`.) The middle was the lane's pick, and the first world render's skin.

- The first true blacks: 44339 black with a grey maze, 43328 charcoal-brown.
- 57968 went from painted indigo to near-black plum, and 62119 from periwinkle to grey-lilac.
- 58958 and 75707 turned pewter and grey-olive.

Its faults: 83460 still glared white, and there was no bone and no clear silver. The family split, too. Its four darkest
members went black while 40020 stayed pink-brown, so the largest parent-to-child gap grew to 24. The cost underneath all of it was
saturation: the rendered bodies averaged 0.28, against 0.47 in v4, and no body was vivid.

## Version 7: reef colour and a second zone

On 2026-10-30 the owner, through the coordinator, asked for reef-level variety, judged against Red Sea and Indo-Pacific
photographs. That means many strong hues, two or three colour zones, several pattern families, and a spread from vivid to drab. A board
of 22 licensed images from Wikimedia Commons was fetched as the reference; it stays out of git, in `scratch/owner/realism-refs/`.

Version 7 came in two steps. The first (`261d12de`) added:

- **The zone, a fifth axis,** is the fifth generalised eigenvector of hue_screen's (Q, S) problem. Its eigenvalue is 5.05, and
  its kin p95 rank gap 0.316, against a stranger median of about 0.29. `scripts/fit_zone.py` writes its constants to `zone.json`, which
  `gen_constants` takes as its eighth argument (`8325d8c0` on main).
- **`_ZONE`, `_ZONE_T`, `_ZONE_CHROMA` and `_ZONE_PATTERN`.** A part past `t` along the body takes a second hue, at arc position
  `base + 0.25 + 0.5 * zone rank`, with chroma at least `_ZONE_CHROMA`. The rim takes it too, and with `_ZONE_PATTERN` the maze.
- **`_YELLOW_LIFT`**, which pulls L toward 0.82 within 35 degrees of 60, so a yellow stays light as a reef yellow does.

The reef look raised the chroma top to 0.20 and opened the arc to the whole wheel but the leaf greens:

| look | vivid | yellow | blue | two zones | mauve, pink | L*, median (p10 to p90) | texture against v4 | kin, median (max) | strangers |
|---|---|---|---|---|---|---|---|---|---|
| v6d's pick | 0 | 0 | 5 | 5 | 4 | 60 (39 to 78) | 1.44 | 1.7 (24.2) | 31.8 |
| reef | 4 | 1 | 0 | 7 | 3 | 73 (46 to 90) | 1.25 | 1.9 (22.1) | 38.2 |
| reef, maze in the zone hue | 3 | 0 | 4 | 10 | | 73 (47 to 90) | 1.24 | 2.8 (22.4) | 39.6 |
| reef overshoot | 5 | 0 | 3 | 16 | | 70 (46 to 87) | 1.29 | 3.1 (23.3) | 45.9 |

(`full-colour-v7-sheet.png` and `-family.png`; the read leaves the mauve count blank where its table was not kept.) The reef
look was the pick. For the first time animals had two zones: 54035 magenta-red with an electric-blue tail, 57968 indigo with
cyan, 48556 and 44540 brown and grey with a sky-blue tail. Its faults were a pastel pale half (58036, 59232, 62464, 53356,
47834, 52133, 47867 and 75707 in cream, peach and pink, which the board does not show) and a zone hue that could land on lime or
mint.

83460's glare was tested by rendering it three ways. Its L* is 92.8 as it stands, 78.3 with the light through its thin body and
its glow cut, and 77.7 with the pale end at 0.50. Both causes play a part, and its neighbour 58944 drops less from the light cut.

## Version 7b: no pastels

Version 7b changed only settings. The pale end came down (`LIGHT_PALE` 0.58, 0.55, 0.52), and both arc ends moved away from
green (start 185 to 195 degrees, end 68 to 60). Chroma power went to 1 (0.8 in the overshoot) so that the middle band carries
colour. The zone pattern was off.

| look | vivid | two zones | mauve, pink | blue | violet | neutral | L*, median (p10 to p90) | texture against v4 | kin, median (max) | strangers |
|---|---|---|---|---|---|---|---|---|---|---|
| reef | 4 | 7 | 3 | 0 | 3 | 5 | 73 (46 to 90) | 1.25 | 1.9 (22.1) | 38.2 |
| mild: pale 0.58, arc 185 to 68 | 5 | 7 | 5 | 2 | 4 | 1 | 64 (38 to 84) | 1.36 | 1.8 (18.2) | 42.4 |
| middle: pale 0.55, arc 190 to 62, chroma 0.22 | 4 | 3 | 5 | 2 | 4 | 2 | 65 (36 to 83) | 1.4 | 1.8 (17.3) | 42.0 |
| overshoot | | | | | | | 60 (34 to 79) | | 1.8 (16.6) | 44.5 |

(`full-colour-v7b-sheet.png` and `-family.png`; the overshoot's counts were not kept.) The middle was the pick, agreed with the
coordinator. The pastels went warm: tan, gold, orange and rust carry their mazes, and orange arrived. The zone hues stayed out
of the greens. Its faults: warm colours dominate, the blues and purples sit mostly in tails, a few animals stay pale and cold
(50990 icy, 83460, 58944, 62119), and the family's tips are dark teal.

## Version 7c: pattern families

The second step of version 7 (`611e42f9`) gives each animal one of four pattern families:

- 0, maze only;
- 1, bars across the body and tail;
- 2, one eyespot;
- 3, an edge outline.

The sixth axis was fitted for this, but at eigenvalue 3.67 its kin p95 gap is 0.445, well past the stranger median, so it
does not keep kin together. The family is keyed off the two strongest heritable axes after hue instead:
`(value >= 0.5 ? 2 : 0) + (sheen >= 0.5 ? 1 : 0)`.

The mark is drawn in the zone hue at `_ZONE_CHROMA`, L 0.88 on a dark body and 0.14 on a light one. The shader reads
`_FamilyColor` and `_FamilyParams`. Bars need the whole body's extent, so on the one skin `TheatreOneSkin` passes the rest box
of the skin's UV5 as `_FamilyBox` and `_FamilyExtent`. `_FAMILY` turns it on, and `_FAMILY_FORCE` (-1 off, or 0 to 3) puts
every animal in one family so that each can be judged.

| look | vivid | two zones | blue | L*, median (p10 to p90) | texture against v7b | kin, median (max) | strangers |
|---|---|---|---|---|---|---|---|
| v7b's pick | 4 | 3 | 2 | 65 (36 to 83) | 1.00 | 1.8 (17.3) | 42.0 |
| families as the genome picks them | 4 | 5 | 3 | 70 (38 to 83) | 1.03 | 1.8 (17.3) | 40.9 |
| all bars | 0 | 11 | 5 | 76 (65 to 83) | 1.21 | 1.5 (4.3) | 27.1 |
| all eyespots | 3 | 5 | 3 | 66 (52 to 83) | 1.10 | 2.5 (11.1) | 38.2 |
| all edge outlines | 4 | 5 | 2 | 68 (41 to 83) | 1.05 | 3.3 (9.0) | 38.5 |

(`full-colour-v7c-sheet.png` and `-family.png`.) Of the 24 animals (the six plants are left as they were), the genome puts 4 in
maze only, 5 in bars, 5 in eyespots and 10 in edge outlines. The genome's own choice was the pick, agreed with the coordinator.

- The bars read on all five: 44540, 54040, 77273, 48556 and 52133.
- Kin keep one mark: the family's five dark members are all maze only.
- No animal loses its maze.

Its faults, checked on full-size crops (`scripts/crop_v7c.py`):

- The edge outline reads as a highlight, and it goes to the largest group.
- An eyespot lands wherever its UV puts it. It is a clear ring on 58958 and a collar on 62119. On 54035 it is small and on the
  tail. On 61406 and 77286 it is on the far side from the stills camera.
- At L 0.88 the marks read white or ice blue, and the zone hue is lost.
- Barring every animal drops the vivid count to none and turns 61406 and 77286 candy-cane.

The next dials are agreed with the coordinator. The edge outline gets wider and takes the zone hue, eyespots keep to the main
body's larger faces, and the marks take more chroma and less lightness. Beside them come more blue and purple bodies and a
less icy 50990.
