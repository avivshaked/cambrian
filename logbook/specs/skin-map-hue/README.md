# The skin map's animal hue: what it did, and the hybrid that replaces it

On 2026-10-30 the four-worlds sheet showed three of four unrelated lineages in pink to violet, and the coordinator asked
whether the map's hues bunch. They do, and the cause is in the fit. The pooled fit's base hue reads only three inputs, the
consumer, absorptive and photosynthetic shares of a body's tissue. So an animal's hue is its diet. This folder holds the
numbers that showed it, the screen of four replacements, and the scripts that made both.

## What today's map does

`hue-spread.md` and `hue-histogram.png` cover the fit's own fitting set: the 2,131 non-producers alive at 24,000 s in its ten
reference worlds. Each is scored as the C# scores it and placed on the marine palette, 220 degrees plus 190 times the base hue.

- 750 bodies, 35 per cent, share one violet hue. They are pure absorbers, mostly one part with no brain. Their inputs are
  all the same (`spikes.md`).
- 160 bodies with some photosynthetic tissue sit at the palette's blue end, clipped at base hue 0.
- The consumers spread from pink-red to red-orange, and none reaches gold. The highest base hue is about 0.86, or 23 degrees.
- Counted by clade, 156 of 264 clades are red-orange and none is gold.

## The screen

`hue-screen.md` compares four rules for base hue on all ten worlds. It then repeats the comparison on the five worlds the
lineage weights were not fitted on.

| Rule | What it is | Result |
|---|---|---|
| today | the sigmoid of the three diet shares | 35 per cent in one 5-degree bin, no gold |
| lineage | the sigmoid of a lineage score | still bunched, no gold |
| rank | today's score ranked over the fitting set | the absorbers stay tied, 32 per cent in one bin |
| hybrid | the lineage score ranked; diet moves saturation instead | the largest bin 3 per cent, gold 13 per cent, kin alike |

The lineage score is the weighting of every feature but the three diet shares that maximises the spread between bodies over
the spread between a parent and its child. It is fitted on five worlds, with a ridge of 0.05.

The hybrid was chosen. Each diet now spreads across the palette. The hue's middle half is 50 degrees wide among consumers,
44 among absorbers and 36 among the mixed feeders. Today it is 17, 0 and 0. A parent and child differ by 0.8 degrees of hue
at the median. Their skin distance is 0.66 at the 95th percentile, against 0.72 today.

The cost is in `correlations.md`. Every channel of the map already moves with the others, and the hybrid's hue does so more,
at +0.88 with the pattern's regime. A fifth rule held the score uncorrelated with the other nine channels. That made hue
independent, but a parent and child then differed by 57 degrees at the 95th percentile, and most clades' medians fell to
magenta. Independence there cost the likeness of kin, so the hybrid stays.

## Where it runs

`EVOSIM_THEATRE_SKIN_MAP=2` draws the hybrid, and 1 is today's map unchanged (`TheatreSkinMap.cs`, `1370c3a7`). The numbers
are generated into `TheatreSkinMapConstants.cs` from `hybrid.json`, which `scripts/hue_hybrid_export.py` writes.
`scripts/check_rule.py` re-does the C#'s rule in Python, statement by statement, against the screen's own output. It agrees
to the nine decimals of the reference. Clones share one lineage score, and a different order of summation moves it by its
last bit, so the rank treats scores within 1e-12 as equal. Distinct scores are never closer than 1.6e-10.

## The scripts

The scripts read each world's `features.csv` from `scratch/skin-map/pooled-*`, as `skin_features.py` wrote them.
`make_worlds6.py` writes the commands that make the six worlds' files the first sheet did not need. Those files are derived
data and are not committed; the commands rebuild them from the runs in about three minutes.

## Full colour

The owner ruled on 2026-10-30 for the whole hue wheel. `full-colour.md` records mode 3, its value axis and the OKLab
levelling, with the three sheets drawn that day and their faults.
