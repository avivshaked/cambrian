# Round 60's fed-island screens: the read (2026-10-15, agent 08)

Eight worlds, 10,000 s each, on r60-fed `56dbcd8d`, seeds 1 and 2. Every world ended on budget and passed
card-run-check. Per-world output is in `scratch/r60s-read/r60s-<arm>-s<seed>.txt`, and the maps are in
`scratch/r60s-read/fields/<arm>/`. The reads used:

- `screen_read.py`: cols, x sd, burial against influx over the 2,000 s before each time, and the desert's top-12 m matter;
- `scratch/r60-patchiness/helpers/crowd_vs_bed.py`: empty 10 m squares, and the cv of bodies per square against a Poisson crowd's;
- `scripts/field-map.py --layers 3 --footprint 100`: the crowd's share in the islands as seeded.

## At 10,000 s, against the proposal's bars

| world | alive | empty 10 m squares (bar ≥ 0.3) | cv per square / Poisson | in footprint (bar ≥ 0.6) | desert top-12 m, units/m³ (bar < 0.00025) | burial / influx, last 2,000 s (bar ≥ 0.5) | x sd, m |
|---|---|---|---|---|---|---|---|
| c s1 | 1,571 | 0.01 | 0.57 / 0.37 | 0.14 | 0.0054 | none fed | 38.9 |
| i1 s1 | 2,393 | 0.02 | 0.94 / 0.30 | 0.32 | 0.0057 | 0.90 | 37.2 |
| i2 s1 | 656 | **0.44** | 2.03 / 0.58 | 0.51 | 0.0024 | 1.10 | 25.2 |
| i3 s1 | 1,978 | 0.04 | 0.99 / 0.33 | 0.35 | 0.0037 | 1.15 | 36.3 |
| c s2 | 1,587 | 0.01 | 0.48 / 0.37 | 0.20 | 0.0062 | none fed | 41.9 |
| i1 s2 | 2,739 | 0.03 | 1.04 / 0.29 | 0.45 | 0.0057 | 0.87 | 43.7 |
| i2 s2 | 1,929 | **0.40** | 1.96 / 0.37 | **0.61** | 0.0020 | 0.99 | 45.0 |
| i3 s2 | 2,133 | 0.06 | 1.06 / 0.33 | 0.43 | 0.0029 | 1.06 | 42.7 |

The islands as seeded are 0.17 of the live columns in every world. At 5,000 s, i2's worlds already read 0.60
and 0.56 empty, with 0.58 and 0.62 in the footprint.

## Patchy or soup

- **The control is soup on both seeds.** 1% of 10 m squares are empty, and the crowd's share in the islands
  (0.14, 0.20) is below the islands' own 0.17 share of the water. The map shows bodies spread evenly over a
  broad central dome of matter.
- **i1 and i3 are soup with lumps.** Bodies per square vary about three times as much as a random crowd
  would, twice the control's figure, and 32 to 45% of the crowd is on the islands. But 94 to 98% of 10 m
  squares still hold bodies. The maps show one dense knot on an island, with bodies scattered across every
  part of the tank between.
- **i2, D071's faster settling, is patchy on both seeds.** 40 to 44% of the 10 m squares are empty, and
  bodies per square vary three to five times as much as in a random crowd. The empty squares are not a thin
  crowd's chance gaps: a random scatter at seed 2's 7.4 bodies a square would leave almost none empty
  (e^-7.4, about 0.0006), and seed 1's 3.0 a square about 0.05. On the maps, seed 2 has a dense knot on its
  northwest island and a crowd in the southeast, around a desert that covers about half the tank. Seed 1 has
  one oasis on its merged western pair, with an empty east half.
- **What the maps add (inference).** The seed 2 crowd in the southeast hugs the glass at x 140 to 160. Part
  of its clustering may therefore be the wall's, not the island's. That is worth a look in the theatre
  before round 60 is designed. Farm runs cannot be replayed in the Editor, so the theatre would show a
  related run (the Editor's own realisation of the same seed and settings), not these exact worlds.

## The bars no arm meets

- **No desert is below the leaf's break-even.** i2's desert holds 0.0020 to 0.0024 units/m³, about eight
  times the 0.00025 of round 57's commonest leaf. Yet i2's desert is nearly empty of bodies (inference): the
  desert's emptiness is not starvation at that break-even. Either the break-even differs for these worlds'
  genomes, or a crowd's own draw-down and the dispersal limit set the edge. Not read further.
- **i2 on seed 1 has 656 alive, under the 1,000 bar.** It fell to 524 at 5,000 s. Seed 2's i2 has 1,929.
- **i2's standing matter is still falling at 10,000 s** (seed 1 from 7,906 to 6,421; seed 2 from 9,643 to
  8,728), with burial at about influx by the end. The proposal expected the stock to level near 20,000 s.

## r60s-big's winner: i2

Influx 1.2 units/s on the islands, burial 0.001/s, settling 0.02 m/s.

- It is the only arm that answers D175's question: oases with water between them, on both seeds, from
  5,000 s on. i1 and i3 are the control's soup with lumps.
- It meets the empty-squares bar on both seeds and the footprint bar on seed 2, and it meets the burial bar
  on both seeds.

The risks for r60s-big, which runs on seed 1:

- seed 1's i2 crowd is the smallest of the eight;
- the stock had not levelled by 10,000 s;
- the glass may be shaping part of the clustering.

Choosing i2 is within the screened range of the proposal's question 3. Whether the agent may choose is the
owner's ruling when the screens are read (D175).

## The machine

| step | groups of four | wall | pace | card |
|---|---|---|---|---|
| s1, s2 | 8 farm threads on logical 16 to 23 | 37 to 41 min | 4.1 to 5.1 times real time | at most 8,921 MiB, 53 °C |

solo (i1 alone on seed 1) ran 1,000 s in 2.3 min, 7.19 times real time, with a card peak of 6,250 MiB.
Assay was held during each group, and the CPU read 54 to 85 °C. The first two tries of s1 heat-stopped at
91 to 94 °C, each about 15 s after an Assay job started.

## r60s-big: i2 at 88,000 m², seed 1 (run 05:05 to 05:43, read 05:50)

The run ended on budget at 10,000 s and passed every card-run-check condition. It took 37.5 min, at 4.44 times
real time, with a card peak of 6,953 MiB. Per-world output is in `r60s-big-s1.txt`, and the maps are in
`fields/r60s-big-s1/`.

| t | alive | empty 10 m squares | cv / Poisson | in footprint (0.09 of the water) | desert top-12 m, median | desert columns below break-even | standing | burial / influx |
|---|---|---|---|---|---|---|---|---|
| 1,000 | 1,332 | 0.87 | 4.57 / 0.90 | 0.75 | 0.00039 | 0.40 | 13,720 | 2.08 |
| 5,000 | 273 | 0.93 | 5.31 / 1.90 | 0.84 | 0.00026 | 0.48 | 6,710 | 1.90 |
| 10,000 | 381 | 0.92 | 6.03 / 1.60 | 0.63 | 0.00029 | 0.47 | 5,035 | 1.13 |

- **It is patchy, more than any screen.** Nine squares in ten are empty, and most of the crowd stands in
  the islands, which are 9% of the water. Here the desert does reach the leaf's break-even: half its columns
  are below 0.00025. The maps show two oases at 10,000 s, a large one in the south-east and a small one in
  the north, 250 m apart. A third, in the south-west, had nearly died out by then. Nothing lives in the
  interior.
- **The crowd collapsed.** It fell from 1,332 at 1,000 s to 273 at 5,000 s, and stood at 381 at 10,000 s,
  well under the 1,000 bar. The leaves are almost a monoculture: two stomachs at 10,000 s.
- **The stock fell by two thirds.** It went from 15,000 to 5,035 units. Burial ran at twice the influx
  early, and was still 1.13 of it at 10,000 s, so the stock had not levelled.
- **My reading (inference): the dose was set for 22,000 m².** The budget (15,000 units) and the influx
  (1.2 units/s) did not grow with the area. So the stock is spread over four times the water and buried at
  four times as many floors, and the same influx feeds a crowd no larger than the small tank's. This one
  has a fifth of i2 seed 2's crowd and a little over half of i2 seed 1's. Scaling the influx, and perhaps
  the budget, with the area would be a new dose outside the screened range. That is round 60's dose,
  question 3, and stays the owner's.
- **Every oasis sits against the glass.** This sharpens the open point from seed 2's south-east crowd:
  whether the wall makes the oases, or only the islands' placement does. It is for the theatre before
  round 60 is designed.
- **The theatre frame at 10,000 s agrees** (`scratch/snaps/r60s-big-s1/`, drawn from the snapshot). Almost
  all 381 bodies are in one cluster in the south-east quadrant whose southern edge touches the glass, with a
  thin trail west along the glass and a sparse scatter on the north rim. The interior is empty but for a few
  stragglers among the reefs. From the side they are a sheet at the surface. The frame draws no matter.

## r60s-big2: big at a dose scaled to its area, seed 1 (D181; run 07:01 to 07:58, read 07:59)

The influx is 4.8 units/s and the starting matter 60,000 units, four times big's, with founders placed as in
big. The run ended on budget and passed every card-run-check condition. It took 54.0 min, at 3.09 times real
time, with a card peak of 8,516 MiB and the CPU at most 85 °C. Per-world output is in `r60s-big2-s1.txt`, and
the maps are in `fields/r60s-big2-s1/`.

| t | alive | empty 10 m squares | cv / Poisson | in footprint (0.09 of the water) | desert top-12 m, mean / median | desert columns below break-even | standing | burial / influx |
|---|---|---|---|---|---|---|---|---|
| 1,000 | 7,372 | 0.75 | 3.87 / 0.38 | 0.70 | 0.0035 / 0.0014 | 0.13 | 55,029 | 2.04 |
| 5,000 | 3,579 | 0.69 | 3.40 / 0.53 | 0.68 | 0.0017 / 0.0012 | 0.07 | 32,022 | 1.71 |
| 10,000 | 6,617 | 0.55 | 2.76 / 0.39 | 0.42 | 0.0014 / 0.0011 | 0.04 | 29,361 | 0.96 |

Against the proposal's five bars at 10,000 s it meets three, and the stock has levelled, which the proposal
expected only near 20,000 s:

| bar | big2 | big |
|---|---|---|
| alive ≥ 1,000 | 6,617, met | 381 |
| empty 10 m squares ≥ 0.3 | 0.55, met | 0.92 |
| in footprint ≥ 0.6 | 0.42, not met | 0.63 |
| desert top-12 m below 0.00025 | mean 0.0014, median 0.0011, not met | median 0.00029 |
| burial / influx ≥ 0.5 | 0.96, met | 1.13 |
| standing matter levelled (expected, not a bar) | 28,959 at 7,500 s, 29,361 at 10,000 s, yes | still falling |

- **The scaled dose holds a crowd.** It dipped from 7,372 at 1,000 s to 3,579 at 5,000 s and came back to
  6,617, seventeen times big's 381 and about three and a half times i2 seed 2's on a quarter of the water.
- **The stock levelled at about half its start.** It fell from 60,000 to 32,000 units by 5,000 s, then held
  near 29,000, with burial at 0.96 of the influx over the last 2,000 s. Big's was still falling at 10,000 s.
- **It is patchy, though less than big.** Over half the 10 m squares are empty, and bodies per square vary
  seven times as much as a random crowd would. But the desert is fed. Its median column holds about four
  times a leaf's break-even, and only 4% of its columns are below it. The share of the crowd on the islands'
  footprint fell from 0.70 at 1,000 s to 0.42.
- **The map at 10,000 s.** The largest crowd is again in the south-east against the glass, with a dense knot
  and an arc of bodies around a richer patch. A second crowd lines the north glass for about 150 m. A smaller
  knot sits on a rich patch in the south-west, also near the glass, and a loose scatter crosses the centre
  north of middle. Stomachs (red on the map) are scattered through the north and south-east crowds, dozens
  where big had two; I have not counted them.
- **My reading (inference).** Scaling the dose with the area answered big's collapse: the crowd lives and the
  stock levels. What it costs is the desert. Four times the matter keeps the water between the islands above
  a leaf's break-even, so the crowd spreads off the islands, and the footprint bar is missed. The oases at the
  glass are there again at this dose. That they recur at four times the matter suggests the wall or the
  current shapes them, not the dose. Which of the two, the field dumps and a frame would have to say.

## r60s-big3 and r60s-big3h: islands 30 m off the glass, founders on the matter (D185; run 10:11 to 11:54, read 11:55)

Both worlds are big2's tank on seed 1 with two changes. **No island column lies within 30 m of the glass** (D185's
margin). **Founders follow the matter** (D181's FoundersFollowMatter, D109's rule) in place of the placement big
and big2 used. Because both changed at once, nothing below can say which of the two made a difference from
big2. big3 keeps big2's dose (influx 4.8 units/s, 60,000 units to start). big3h halves it (2.4 units/s, 30,000).

Both ended on budget and passed every card-run-check condition, on r60-fed `05fd672e` with a clean tree, each
at the configHash of its founding:

| world | configHash | wall | pace | card peak | CPU |
|---|---|---|---|---|---|
| big3 | 419371d53a8a79eb | 54.4 min | 3.06x | 53 °C, 8,654 MiB | 63-77 °C |
| big3h | a180a952d36a94b6 | 41.6 min | 4.01x | 53 °C, 7,108 MiB | 61-77 °C |

Per-world output is in `r60s-big3-s1.txt` and `r60s-big3h-s1.txt`, and the maps are in `fields/r60s-big3-s1/`
and `fields/r60s-big3h-s1/`. The islands as seeded here are 0.13 of the water (448 columns), against 0.09 in
big and big2.

| world, t | alive | empty 10 m squares | cv / Poisson | in footprint | desert top-12 m, mean / median | desert columns below break-even | standing | burial / influx |
|---|---|---|---|---|---|---|---|---|
| big3, 1,000 | 2,188 | 0.69 | 3.04 / 0.61 | 0.49 | 0.0095 / 0.0055 | 0.12 | 47,172 | 3.69 |
| big3, 5,000 | 2,012 | 0.56 | 2.63 / 0.64 | 0.57 | 0.0023 / 0.0016 | 0.13 | 24,339 | 1.57 |
| big3, 10,000 | 3,228 | 0.44 | 2.32 / 0.51 | 0.54 | 0.0020 / 0.0016 | 0.12 | 23,137 | 0.91 |
| big3h, 1,000 | 948 | 0.75 | 3.21 / 0.93 | 0.49 | 0.0049 / 0.0029 | 0.15 | 23,645 | 3.67 |
| big3h, 5,000 | 404 | 0.82 | 3.66 / 1.43 | 0.66 | 0.0013 / 0.00087 | 0.25 | 10,617 | 1.72 |
| big3h, 10,000 | 611 | 0.79 | 3.96 / 1.16 | 0.69 | 0.0012 / 0.00079 | 0.23 | 8,567 | 1.03 |

Against the proposal's bars at 10,000 s:

| bar | big3 | big3h | big2 |
|---|---|---|---|
| alive ≥ 1,000 | 3,228, met | 611, not met | 6,617, met |
| empty 10 m squares ≥ 0.3 | 0.44, met | 0.79, met | 0.55, met |
| in footprint ≥ 0.6 | 0.54, not met | 0.69, met | 0.42, not met |
| desert top-12 m below 0.00025 | median 0.0016, not met | median 0.00079, not met | median 0.0011, not met |
| burial / influx ≥ 0.5 | 0.91, met | 1.03, met | 0.96, met |
| standing levelled (expected, not a bar) | 22,082 at 7,500 s, 23,137 at 10,000 s, yes | 8,792, then 8,567, nearly | yes |

- **Seed 1's former glass islands now sit at the margin line.** On the 100 s map, the north island (near x 175,
  z 295), the west one (x 45, z 155) and the south-east one (x 240, z 55) lie just inside the 30 m line, where
  big2 had them against the glass. New islands stand in the interior: the north-west (x 75, z 215), the centre
  (x 150, z 180), the south of centre (x 195, z 105) and the south-west (x 70, z 80). Island matter within 20 m
  of the glass is 0.003 at 100 s, against 0.55 in big2.
- **The mixing evens the matter against the glass from either side, and piles none there.** By 1,000 s the
  matter within 20 m of the glass rises to 0.165 in big3 and 0.162 in big3h, toward the water's 0.224. In big2
  it fell from 0.55 to 0.41 over the same time. On the 1,000 s map a faint band runs from the north island to
  the north glass.
- **The oases left the glass with the islands.** Every dense knot on the 10,000 s maps of both worlds sits on an
  island, 34 to 81 m from the glass by my reading of the knot centres off the map. The nearest is the
  south-east one at about 34 m. None touches the glass. In big3 a loose scatter still lines the west and north
  glass between knots. In big3h, almost nothing does. My reading (inference): the glass did not make big2's
  oases; the islands' placement did, which is what the fifty-seed count said of the placement itself.
- **big3 holds a crowd and is patchy, but less than big2 by one bar.** 3,228 alive, 0.44 of squares empty, and
  a stock levelled near 23,000 units. More of the crowd is on the islands than in big2 (0.54 against 0.42), but
  under the 0.6 bar. Its desert is fed like big2's, at six times a leaf's break-even in the median column. The
  1,000 s crowd is a third of big2's (2,188 against 7,372). Whether the founder rule or the margin did that,
  these two runs cannot say.
- **big3h is the patchiest of the big tanks but too thin.** 79% of the squares are empty, 0.69 of the crowd is
  on the islands, and a quarter of the desert's columns are below break-even. But it stands at 611, under the
  1,000 bar. It fell to 404 at 5,000 s and recovered a little. The desert's median is still about three times
  the break-even. On the 10,000 s map the west island holds the densest matter of the tank and no bodies.
  Mixotrophs (orange) gather in the south-east and south-west knots, dozens of them; I have not counted them.
- **Between the two doses** (inference): halving the dose takes big3h from 3,228 alive to 611 and its desert
  from about six times break-even to three. No dose screened yet both meets the 1,000 bar and leaves a desert
  below break-even. The footprint bar is met only at the half dose.
- **No theatre frame was taken of either world.** One can be drawn from the 10,000 s snapshot on 01's word.

## r60s-big3 run on to 20,000 s (D186; resume check 12:37 to 13:09, run 13:10 to 14:24, read 14:36)

**The resume first.** r60s-big3r-s1 resumed big3's 5,000 s checkpoint on the card and ran to 10,000 s, on the
binaries that ran big3 (a later commit, nothing under src/ changed). Every row file after 5,000 s is the unbroken
run's: lineage 20,106 rows, stats 500, digest 5,000, absorptive 4,403, positions 500 (group-resume-compare --lone;
the stats' wall timings and two process counters left out by design). Its 10,000 s checkpoint holds the unbroken
run's state: every header field but the source run's name equal, and the payload equal by length (76,886,356
bytes), by digest (d3bf8696ccec655a) and byte for byte (scripts/reads/ckpt-compare.py). It ended with 3,228 alive,
as big3 did. So r60s-big3c-s1, resumed from big3's own 10,000 s checkpoint, continues big3 itself.

r60s-big3c-s1 ended on budget at 20,000 s and passed every card-run-check condition: 72.4 min, 2.30 times real
time, card peak 53 °C and 8,836 MiB, CPU 53 to 79 °C. Per-world output is in `r60s-big3c-s1.txt`, and the maps
are in `fields/r60s-big3c-s1/` (the islands as seeded taken from big3's 100 s dump, `field-map.py --footprint-run`).

| t | alive | empty 10 m squares | cv / Poisson | in footprint (0.13 of the water) | desert top-12 m, mean / median | desert columns below break-even | standing | burial / influx |
|---|---|---|---|---|---|---|---|---|
| 10,000 (big3) | 3,228 | 0.44 | 2.32 / 0.51 | 0.54 | 0.0020 / 0.0016 | 0.12 | 23,137 | 0.91 |
| 15,000 | 6,946 | 0.29 | 1.73 / 0.35 | 0.46 | 0.0012 / 0.00099 | 0.15 | 24,769 | 0.82 |
| 20,000 | 11,690 | 0.24 | 1.48 / 0.27 | 0.40 | 0.0011 / 0.00082 | 0.11 | 29,470 | 0.77 |

- **The crowd grows and the patches dissolve.** It went from 3,228 to 11,690 in 10,000 s and was still growing.
  Empty squares fell below the 0.3 bar by 15,000 s and stood at 0.24 at 20,000 s; the share on the islands fell
  from 0.54 to 0.40. Against the bars at 20,000 s it meets only the crowd's and the burial's.
- **The stock rose again** from 23,137 to 29,470 units, burial falling to 0.77 of the influx, so it has not
  levelled. The desert thins as the crowd grows (median from 0.0016 to 0.00082 units/m³, about three times a
  leaf's break-even), but it does not reach the break-even.
- **The crowd goes deeper.** Its depth quartiles went from 1.2, 2.3 and 4.4 m at 10,000 s to 2.2, 6.5 and 12.2 m
  at 20,000 s, and its share over the 20 to 30 m bed rose from 0.21 to 0.30, nearly the bed's own 0.31.
- **The maps.** At 15,000 s the island knots are still there (north at x 175, z 290; west at x 45, z 155;
  north-west at x 70, z 215; south-west; south of centre; south-east), with the west half filling between them
  and a first thin line along the north glass. At 20,000 s the west half is one crowd from the glass to x 110,
  and a new band of bodies, many of them mixotrophs (orange), lines the north-east glass from about z 130 to 290,
  at x 280 to 330, where no island is and the matter map is pale. The south and east interior stay nearly empty.
- **My reading (inference).** At the scaled dose the islands seed the crowd, and by 20,000 s the crowd outgrows
  them: it fills the water between the western islands and reaches the glass again, now away from any island.
  The 30 m margin kept the islands off the glass, but not the crowd. Whether the north-east band is a crowd fed
  by the drift of matter from the north island along the glass, or a guild that gathers at a wall, the maps do not
  say; a matter map at finer time steps and a frame would.

## r60s-big3m: the middle dose, seed 1 (D186; run 14:37 to 15:28, read 15:30)

big3's world at 3.6 units/s and 45,000 units, founded on the CPU at `9b1ac255` (configHash cdcf91c751d3e799, the
same islands as big3, 3,501 of 3,501 columns shared), run on the card at `a096730a`. It ended on budget and passed
every card-run-check condition: 46.2 min, 3.61 times real time, card peak 53 °C and 8,630 MiB, CPU 56 to 80 °C. The
chain's first launch of it was stopped at 860 s by a fault in card-run (its hash read before a slow founding had
written run.json), fixed in `a096730a`; that try is set aside in `scratch/wt-r60-fed/scratch/r60s-big3m-try1/`.
Per-world output is in `r60s-big3m-s1.txt`, and the maps are in `fields/r60s-big3m-s1/`.

| t | alive | empty 10 m squares | cv / Poisson | in footprint (0.13 of the water) | desert top-12 m, mean / median | desert columns below break-even | standing | burial / influx |
|---|---|---|---|---|---|---|---|---|
| 1,000 | 1,531 | 0.72 | 3.04 / 0.73 | 0.48 | 0.0072 / 0.0043 | 0.13 | 35,418 | 3.68 |
| 5,000 | 1,120 | 0.65 | 2.68 / 0.86 | 0.52 | 0.0019 / 0.0012 | 0.17 | 17,253 | 1.65 |
| 10,000 | 1,827 | 0.59 | 2.88 / 0.67 | 0.62 | 0.0016 / 0.0012 | 0.17 | 15,143 | 0.98 |

The three doses at 10,000 s, against the proposal's bars:

| bar | big3h (2.4 units/s) | big3m (3.6) | big3 (4.8) | big3 at 20,000 s |
|---|---|---|---|---|
| alive ≥ 1,000 | 611, not met | 1,827, met | 3,228, met | 11,690, met |
| empty 10 m squares ≥ 0.3 | 0.79, met | 0.59, met | 0.44, met | 0.24, not met |
| in footprint ≥ 0.6 | 0.69, met | 0.62, met | 0.54, not met | 0.40, not met |
| desert top-12 m median below 0.00025 | 0.00079, not met | 0.0012, not met | 0.0016, not met | 0.00082, not met |
| burial / influx ≥ 0.5 | 1.03, met | 0.98, met | 0.91, met | 0.77, met |
| standing, the last 2,500 s | 8,792 to 8,567 | 15,018 to 15,143 | 22,082 to 23,137 | 24,769 to 29,470 (5,000 s) |

- **The middle dose is the first screen to meet both the crowd's bar and the footprint's.** It meets four of the
  five; only the desert misses, at about five times a leaf's break-even in the median column. The stock has
  levelled near 15,100 units, with burial at the influx.
- **The map at 10,000 s.** Six dense knots, each on an island: north (x 175, z 290), north-west (x 70, z 215), west
  (x 45, z 155), south-west (x 70, z 80), south of centre (x 205, z 95) and south-east (x 240, z 60). A loose
  scatter lies between the western knots and north of the centre. The east half and almost all the glass are
  empty, but for a thin scatter along the west glass. Stomachs and mixotrophs are a handful.
- **My reading (inference).** big3 looked much like this at 10,000 s and dissolved by 20,000 s as its crowd grew
  to fill the water between the islands. big3m's crowd is smaller and its desert hungrier, so it may hold its
  patches longer, but nothing here shows it past 10,000 s, and a round runs 48,000 s. Running big3m on to 20,000 s
  from its 10,000 s checkpoint, as big3 was, would answer that for about an hour of the card; the resume is shown
  exact on this build and world.

## r60s-big3m run on to 20,000 s (D187; run 18:21 to 19:22, read 19:23)

Ended on budget, every card-run-check condition held; 58.1 min, 2.87 times real time; card peak 53 °C, CPU 74 to
86 °C. resumedFrom big3m at 10,000 s, sourceMismatch False. Output `r60s-big3mc-s1.txt`, maps `fields/r60s-big3mc-s1/`.

| t | alive | empty 10 m squares | cv / Poisson | in footprint | desert median | standing | burial / influx |
|---|---|---|---|---|---|---|---|
| 10,000 (big3m) | 1,827 | 0.59 | 2.88 / 0.67 | 0.62 | 0.0012 | 15,143 | 0.98 |
| 15,000 | 2,872 | 0.53 | 2.45 / 0.54 | 0.60 | 0.00097 | 15,470 | 0.92 |
| 20,000 | 4,716 | 0.41 | 1.87 / 0.43 | 0.49 | 0.00081 | 17,979 | 0.80 |

- At 15,000 s it still meets four bars. By 20,000 s the crowd has grown to 4,716, the footprint share is under the
  bar (0.49), empty squares are still over it (0.41), and the stock is rising again.
- The 20,000 s map shows the knots on the islands still there, the west half filling between them, and a band of
  bodies along the north-east glass from about z 100 to 300, where no island lies: big3's 20,000 s pattern,
  reached more slowly (inference).

## r60s-big3h run on to 20,000 s (the owner's ruling of the evening; run 22:04 to 22:46, read 22:48)

Ended on budget, every card-run-check condition held; 41.9 min, 3.98 times real time; card peak 52 °C, CPU 53 to
87 °C. resumedFrom big3h at 10,000 s, sourceMismatch False. Output `r60s-big3hc-s1.txt`, maps `fields/r60s-big3hc-s1/`.

| t | alive | empty 10 m squares | cv / Poisson | in footprint | desert median | standing | burial / influx |
|---|---|---|---|---|---|---|---|
| 12,500 | 651 | - | - | - | 0.00065 | 8,304 | 1.05 |
| 15,000 | 1,005 | 0.70 | 3.15 / 0.92 | 0.65 | 0.00068 | 8,401 | 0.98 |
| 20,000 | 1,719 | 0.64 | 2.75 / 0.71 | 0.59 | 0.00073 | 9,258 | 0.91 |

- Half the middle dose keeps a small crowd. It rises from 651 to 1,719 and ends just under the footprint bar
  (0.59 against 0.6), with two thirds of the squares still empty. The desert stays near three times a leaf's
  break-even, so the hunger did not come from the lower dose.
- The 20,000 s map shows five or six separate island crowds, scattered leaves between them, and the start of a
  band along the north-east glass, thinner than big3mc's. My reading (inference): the same path as big3 and
  big3m, slowed by the smaller crowd.

## The desert screens r60s-d005 and r60s-d02 (D190; run 23:18 to 01:00, read 01:01)

big3m's world and dose with the desert's floor buried at 0.005/s and 0.02/s against the islands' 0.001/s, seed 1,
founded on desert-burial fa0a5e6c as one card group to 20,000 s. Both ended on budget, every card-run-check
condition held; 99.5 min, 3.35 times real time; card peak 53 °C, CPU 60 to 87 °C. Output `r60s-d005-s1.txt`,
`r60s-d02-s1.txt`, `desert-r60-read.txt`; maps `fields/r60s-d005-s1/` and `fields/r60s-d02-s1/`.

| world | t | alive | empty 10 m squares | cv / Poisson | in footprint | desert median | below break-even | standing | burial / influx |
|---|---|---|---|---|---|---|---|---|---|
| big3m(c), 0.001 | 10,000 | 1,827 | 0.59 | 2.88 / 0.67 | 0.62 | 0.0012 | - | 15,143 | 0.98 |
| | 20,000 | 4,716 | 0.41 | 1.87 / 0.43 | 0.49 | 0.00081 | - | 17,979 | 0.80 |
| d005, 0.005 | 10,000 | 1,064 | 0.78 | 3.93 / 0.88 | 0.74 | 0.00041 | 0.35 | 7,421 | 0.95 |
| | 15,000 | 1,686 | 0.69 | 3.41 / 0.70 | - | 0.00044 | 0.35 | 8,220 | 0.89 |
| | 20,000 | 3,727 | 0.52 | 2.66 / 0.48 | 0.59 | 0.00041 | 0.32 | 9,585 | 0.88 |
| d02, 0.02 | 10,000 | 791 | 0.80 | 4.22 / 1.02 | 0.75 | 0.00024 | 0.51 | 5,314 | 0.94 |
| | 15,000 | 1,417 | 0.74 | 3.59 / 0.76 | - | 0.00027 | 0.48 | 6,432 | 0.89 |
| | 20,000 | 2,221 | 0.68 | 3.34 / 0.61 | 0.74 | 0.00027 | 0.47 | 7,458 | 0.94 |

- **D191's test at 20,000 s** (at least 1,000 alive, 0.6 on the islands, 0.3 of the squares empty): d02 holds
  (2,221, 0.74, 0.68). d005 misses on the islands by a hundredth (3,727, 0.59, 0.52).
- **The desert is hungrier, and the stock lower.** The desert median falls from about three times a leaf's
  break-even to 1.6 times at 0.005/s and to about break-even at 0.02/s, where half the desert columns are below it.
  The standing stock is about half of big3mc's.
- **The maps at 20,000 s.** d02 shows six island crowds with little between them and almost nothing at the glass.
  d005 grows a band along the north-east glass, as big3mc did, and spreads between the western islands.
- **Migrants (round 60's reader, positions every 100 s).** In d005, 164 to 304 a year; in d02, 82 to 296. All are
  leaves or jointed swimmers: neither world has a mouthed body (no hunters, no grazers). One to four root lines live
  on two islands or more at a year's end. The largest root line holds 0.74 of d005's living at 20,000 s and 0.57 of
  d02's.
- **My reading (inference).** The faster rate keeps the crowd on its islands to 20,000 s, at a smaller crowd.
  It still grows: d02 doubles every 5,000 s or so, so 48,000 s is not shown. With no mouths, the migrants are
  drifting leaves and swimmers, not hunters crossing between islands.
