# 14 — Relatives look alike because the skin is read from the genome

[Piece 01](01-a-graph-that-grows-a-body.md) showed how a genome, the recipe an animal inherits, grows a body. This
piece is about something the world itself never sees. The theatre is the program that draws a recorded world for the
films, kept apart from the simulation ([D008](../DECISIONS.md#d008)). It now paints each animal's skin from numbers it
reads off that animal's genome. A child's skin then sits close to its parent's, and an animal from another family
wears something else.

The wish is old in this project, and the build is new. The owner asked for inherited skins in September, and the
theatre's working session built the map on 29 October 2026. One number was checked: how far apart two skins are,
against how far apart their genomes are, over parents and children, cousins and strangers. The two moved together,
with a rank correlation of 0.72 over 6,012 pairs of animals. The colours then went through a run of versions on sheets
of stills. On 31 October the owner judged the looks good enough for now.

## The theatre draws, and the world never sees it

The world runs on the farm, a separate program that steps the simulation and records it. The theatre reads those
recordings and draws them. Its skin moves no hash ([the theatre's notes](../unity/Assets/Theatre/CLAUDE.md)). A hash,
here, is a fingerprint of the simulation's code that every recording carries. Because the skin is outside it, a
recording made before a skin change still replays after one.

So everything in this piece is paint. I take it that no creature can see another's colour and that nothing in the
world selects on it. That is my inference from the separation: no sense in the world reads the theatre's drawing, and
the farm never runs the theatre's code.

## A skin from a hash made strangers of near kin

A film about evolution needs its viewer to see a family. In September the project listed ways to make the theatre's
pictures more striking ([the menu](../logbook/specs/striking-theatre-menu.md)). One item was inherited skin genes. The
menu called it the only one that makes a clade visible as a clade, where a clade is all the descendants of one
founder. The owner ruled on 2026-09-16 that skin genes were wanted, cost permitting, and that a version inside the
theatre came first. True skin genes in the genome would have changed its format. Every stored genome would have been
refused and every world's course changed, so that version was left for a round boundary.

The first version inside the theatre used a hash of the body plan ([logbook
0104](../logbook/0104-the-theatre-gets-its-second-look.md)). The body plan is the list of parts the genome grows. A
hash of it is a number that looks random, and every per-body choice of the skin was drawn from that number. A child
that inherited its parent's plan therefore wore its parent's skin.

The skin plan names the flaw in that ([its section 5](../logbook/specs/theatre-skin-plan.md)). Any change to the
plan's text changes the whole number. Two plans one mutation apart then look as different as two strangers. The
pattern's key had a smaller version of the same fault, since it sorted genes into bins and a step across a bin edge
swapped the pattern. The owner's direction of 2026-10-25 asked for one continuous map from genome to skin. A large
genome difference should look large, and a small one small.

## What the theatre reads

The map starts from features, quantities measured on the genome and on the adult body it grows. The build takes 26 of
them ([the build record](../logbook/specs/skin-map-build/README.md)), in four groups.

- The first group is the share of the body's volume in each kind of tissue, such as structure, link, nerve, leaf,
  absorber and biting tissue.
- The second measures the body plan, such as how many parts it has and the length of its longest chain. It also counts
  the mirrored parts and takes the shape of the box that holds the body.
- The third is two genes that children keep. One is the share of energy a parent puts into a child, and the other the
  spare reserve it keeps before breeding.
- The last is the brain's size and summaries of its weights.

Two rules govern what may enter ([the skin plan](../logbook/specs/theatre-skin-plan.md), section 5(b)). No hash and no
founder's seed sets a skin number. And a gene is used only once a parent-and-child check shows that children keep it.
The two breeding genes passed that check, carried unchanged in about 92% of round 62's parent and child pairs. One
small thing may still come from a hash, the phase of the noise, which moves where the mottle's cells sit without
changing what the pattern is.

A small brain gave trouble. Its few weights are a noisy reading. Without a correction, a brain of one to three links
swung two of the skin's numbers by half their range between nearly identical genomes. So each brain-weight feature is
pulled toward the population's average. It keeps a share n/(n+4) of its own value, where n is the brain's count of
connections. A brain of four connections keeps half.

Plants are left out. A body that is half or more leaf tissue by volume is a producer, and it gets no map. It is drawn
as before, and its blades take a colour set per world by the owner's ruling of 2026-10-29
([D256](../DECISIONS.md#d256)).

## The projection is fixed once

The 26 features come in different units, a share here and a count there. A projection puts them on one footing ([the
skin plan](../logbook/specs/theatre-skin-plan.md), section 5(c)). First each feature is standardised against a
reference population: its mean is taken away, and it is divided by its spread. A value of 0 then means typical, and 1
means one spread above typical.

Then the cloud of standardised features is turned, with a principal-component rotation. The first new axis lies along
the direction in which the population varies most, the second along the next, and so on. The rotation is kept as fixed
numbers in the theatre's code, with the population it came from named beside them. The same numbers serve every world
and every later round, so a genome draws the same skin wherever it lives.

Each of the skin's ten numbers is then an S-shaped function of a weighted sum of those turned features. The S-shape
squeezes any input into the range 0 to 1. A genome far from the reference runs into the end of a range, and never
wraps round to the other end. The table gives what each number drives.

| # | number | what it drives on the skin |
|---|---|---|
| 1 | base hue | the body's tint |
| 2 | base saturation | how strong that tint is |
| 3 | pattern regime | where the pattern sits between spots, net and stripes |
| 4 | pattern scale | the size of the pattern's marks |
| 5 | rim hue | the colour of the band along the body's edge |
| 6 | rim width | the width of that band |
| 7 | mottle strength | how strongly the surface is mottled |
| 8 | iridescence | the strength of the sheen |
| 9 | countershade | dark above and pale below |
| 10 | carve character | how deeply the surface is carved |

Nine are wired into the drawing. The carve is not, because it is one value shared by the whole material, which the
skin's geometry also reads.

The first fit used one world, `r62-h6202`, at 24,000 simulated seconds. A world, here, is one run of the simulation on
one seed, and a round is a set of worlds launched together. The coordinator, the session that plans the rounds,
ordered on 2026-10-29 that the fit be pooled over several worlds instead. It now uses the 2,131 animals alive at
24,000 s in ten worlds of rounds 62 and 63. Each number is re-centred on them, so that the middle 80% of animals
spread across most of its range.

The colour took one more decision that day. An animal's hue is picked on a path from blue through violet, magenta and
red to gold, with no greens, so an animal never reads as a plant. The coordinator relayed that the owner wanted a rich
variety among the animals.

## One number, checked

The check compares two distances ([the skin plan](../logbook/specs/theatre-skin-plan.md), section 5(e)). Skin distance
is the straight-line distance between two animals' ten numbers, each in the range 0 to 1. Genome distance is the
species distance, the simulation's own measure of how far one genome has drifted from another. It weighs differences
in tissue, in the graph's shape, in the parts' settings and in the brain. The project uses it to draw the line between
species.

The pairs came in three kinds. One is a parent and its child. Cousins are two animals of one generation that share a
great-grandparent, and strangers are two animals from different clades.

The measure of agreement is Spearman's rank correlation, written ρ. Sort all the pairs by skin distance, sort them
again by species distance, and ask how far the two orders agree. A ρ of 1 means the same order, and 0 means no
relation. The coordinator set two bars on 2026-10-25. The first is ρ above 0.5. The second counts the bad corner,
pairs whose genomes are close and whose skins are far apart. Close means below the 25th percentile of species
distance, and far means above the 75th percentile of skin distance. That share must stay under 5%.

The table gives the three fits. Look first at the corner column, then at the gap between parent and child and between
strangers.

| fit | pairs | ρ | bad corner | parent and child, median and 95th percentile | strangers, median |
|---|---|---|---|---|---|
| one world, every body | 16,884 | 0.774 | 0.0% | 0.002, 0.49 | 0.81 |
| pooled animals, every body | 16,884 | 0.758 | 0.0% | 0.002, 0.71 | 1.01 |
| pooled animals, animals only | 6,012 | 0.721 | 0.0% | 0.025, 0.71 | 1.26 |

Both bars pass on every row. Among animals, the typical child sits fifty times nearer its parent than a typical
stranger does. The working folder's notes add the steps between: two siblings sit 0.10 apart at the median and two
cousins 0.39. Those two figures are not in the build record.

The tail is where the map earns its keep or fails. One parent and child in twenty differ by 0.71 or more, about half
the typical gap between strangers. Under the first fit, the build record looked at the large jumps. There were 74
parent and child pairs with a skin jump over 0.5 at a species distance under 1. Every one came from the body. In about
two thirds of them the number of parts had changed. In the rest, one joint had folded a two-part body from end-on to
side-on.

So the jumps are the map doing what it was asked. A child whose body really changed looks changed. The species
distance scores a new part as a small step, and the skin, which reads the part count directly, scores it as a large
one. That reading is mine. A read file in the working folder agrees with it. Most of the jumps' distance lay in the
rim's width, the carve and the saturation, and the features that moved most were the part count and the chain length.

## The number passed, and the pictures failed twice

Two faults came through the sheets of stills and not through the number. The first sheet was fitted on one world in
which 93% of the bodies were producers. Kin looked alike there, but strangers did not look different, because the
animals had been placed against a crowd of plants. Pooling the fit across worlds fixed that.

The fourth sheet showed that an animal's hue came almost entirely from what it ate. Eaters came out pink-red to
orange, and absorbers one shade of violet, whatever their lineage. On 2026-10-30 the theatre's session screened four
replacements and kept a hybrid. Its base hue is the rank of a lineage score, and diet moves the saturation instead.

My reading of both faults is that the check was blind to them by its shape. It takes the distance over all ten numbers
at once. It can say that skins track genomes in sum, and it cannot say that the most visible number, the colour,
tracks the lineage. A per-number check would have caught the second fault. That check has not been built.

## The looks, in order

Every sheet shows the same subjects. One family from `r62-h6202` covers three generations: a grandparent, its two
children and three grandchildren. Thirty unrelated animals come one from each of thirty lineages across ten worlds.
Each sheet also carries the owner's pick of the best creature look so far, a frame from a close clip of animal 53880
chosen on 2026-10-27. In that frame I see a rust-red animal shaped like a club, covered in a coral-pink maze.

The sheets are in a working folder outside the project's documents, and what I say I saw in them is my observation.
What follows keeps apart the looks the project's documents say the owner accepted and the versions between them.

The documents hold these acceptances, in order.

- **The plan-hash skin**, 2026-09-16, accepted as the first step inside the theatre ([logbook
  0104](../logbook/0104-the-theatre-gets-its-second-look.md)). Kin who shared a plan shared a skin.
- **A biological direction**, 2026-10-25. The owner asked for forms that read as biological rather than geometric,
  judged against photographs of marine life, and for the continuous map this piece describes (the skin plan).
- **The best-so-far frame**, 2026-10-27, which became the yardstick on every later sheet ([HANDOFF](../HANDOFF.md),
  item 8, as of 2026-10-31).
- **The map moved up and the plants set apart**, 2026-10-29. The coordinator moved the map forward at the owner's
  request, and the owner ruled a blade colour per world for the producers (D256).
- **Full colour**, 2026-10-30. The hue path ran from blue to gold through the pinks, so most eaters landed in pink and
  magenta. The owner ruled that the animals take the whole colour wheel ([HANDOFF](../HANDOFF.md), the session table).
- **Good enough for now**, 2026-10-31. The look carried forward is version 7e3's, with its marks darker and stronger
  in each animal's own hue, plus edge and outline fixes from 7e5. The owner made clear that keeping pink out had never
  been the aim, and that coral is welcome ([HANDOFF](../HANDOFF.md), the theatre section).

Between full colour and the last acceptance, the working folder's notes record a run of versions. Each answered
something the owner said on the sheet before. They are in no tracked file, so this paragraph and the next rest on the
working folder alone. Version 6 gave lightness, colour strength and gloss each their own reading of the genome, beside
hue. It followed the owner's finding that version 5 was too green, and a request for blacks, whites, browns, reds and
blues. Versions 6b to 6d muted the bodies and moved the crowd in the middle of the range to rust and brown.

Version 7 brought reef colour, after the owner asked why the newer animals looked less varied than a reef. A board of
22 reef photographs was fetched for it with the owner's approval. Version 7c gave each animal one of four pattern
families: a maze, bars, an eyespot or an edge outline. In 7d each of five patterns had its own switch in the genome,
so an animal could carry any mix. Over the 2,131 animals, a parent and child agree on each switch 93 to 97% of the
time. Kin share the whole mix in 84% of pairs, and strangers in 12%. Version 7e drew a mouth as an oral disc, with
radial stripes inside a lip ring.

## What the sheets show

![The third sheet: one family and four pairs of unrelated animals, drawn by the plan-hash skin and then by the skin
map](../logbook/specs/skin-map-build/skin-map-sheet-6e.png)

The sheet above is the third, kept beside the build record. Each pair of rows draws the same bodies twice, first by
the plan-hash skin and then by the skin map. In the top pair, the family of one clade goes from hot pink to salmon
with a pale maze. Each child carries its skin distance from its parent, 0.00 to 0.21, and 0.60 for the grandchild
that grew a tall stalk. In the middle pair, a parent and child from each of four worlds come out salmon, blue with
cyan, magenta and violet-pink, and each child matches its parent. Under the plan-hash skin the same eight were pink
and pale yellow. The producers at the bottom are drawn as before. The second sheet,
[6d](../logbook/specs/skin-map-build/skin-map-sheet-6d.png), is the same but for its producers, which it turned khaki.

I looked at five of the sheets. On the first, the plan-hash look of the time drew the whole family hot pink. The map
drew it salmon-orange with a pale gold maze, and the unrelated animals below came out the same salmon. The grandchild
that grew a tall stalk stands apart, at a skin distance of 0.57 from its parent. The green producers are the same in
both rows.

The second sheet spreads the strangers. A clumped animal from one world turns blue with cyan lobes, and two others
turn magenta and violet-pink. The colours are saturated to the point of neon against the black.

The version 6 family sheet shows the change of mood. Versions 4 and 5 drew the family magenta and crimson. Version 6's
first readings turn it charcoal with a faint green sheen, and its two multicolour rows lay cyan or pink zebra mazes
over the charcoal. In version 7 the family is brown with a coral maze and lime-yellow tips. One grandchild's stalk
takes a different colour from the rest of the family in nearly every row.

The latest sheet, version 7e4's thirty animals, is the most varied. One animal is black with coral bars, which turn
orange in the overshoot column. One is red with an electric-blue tail, one magenta with a yellow tail, and one gold
with a dark-red oral disc. Others are deep indigo, and violet with cyan bands. The producers stay pale yellow and
olive in every column. In its family sheet, five members are dark maroon with a coral maze. The sixth, 40020, is a
glassy pale lilac, and the family no longer reads as one.

## What is still open

The family's split has a cause in the notes. One reading of the genome feeds two pattern switches at once, and 40020
sits near both thresholds, so a small difference turns its maze off and its outline on. The notes plan to give each
switch its own part of the genome. That cause rests on the working folder alone.

The build record lists three more. The pattern's seed still comes from a hash of the pattern's key, which flips at a
bin edge. The theatre caches a skin by creature id, so a sheet that mixes worlds could reuse one world's skin for
another's body with the same id. And the skin plan still names one world as the reference population, where the fit
now pools ten.

The drawing code is not yet in the main line of the project. The feature reader and its test are, under
`scripts/skin-map/`, and the theatre's code for the map sits on a branch behind a switch that is off by default. The
mouths come next, and each animal's mouth will take a kind from its main eating part's shape. Kin will share a kind
([HANDOFF](../HANDOFF.md), the theatre section).

## What here is inference

- **No creature sees another's colour, and nothing selects on it** is the author's inference from the separation of
  farm and theatre, which no source draws out.
- **The species distance scores a new part as a small step and the skin as a large one** is the author's reading of
  the build record's jump count. A read file in `scratch/skin-map/` supports it, outside the project's documents.
- **Siblings at 0.10 and cousins at 0.39** come only from the working folder's notes.
- **The check was blind to the hue's source by its shape** is the author's reading. A per-number check would test it.
- **The versions from 6 to 7e, and the owner's remarks that prompted each**, come from the notes in
  `scratch/owner/genome-to-skin-family-sheet/`, a working folder. So do the pattern switches' agreement figures.
- **The family's split, and its cause in one reading feeding two switches**, come from the same notes.
- **What the sheets show**, including the best-so-far frame's description, is the author's own look at pictures in
  that working folder.

## Where it is

| file | what it holds |
|---|---|
| [`theatre-skin-plan.md`](../logbook/specs/theatre-skin-plan.md), section 5 | the design: the ten numbers, the features, the projection, the check and its bars |
| [`skin-map-build/README.md`](../logbook/specs/skin-map-build/README.md) | what was built, the three fits, the jumps, the hue rework, what is open |
| [`skin-map-sheet-6d.png`](../logbook/specs/skin-map-build/skin-map-sheet-6d.png), [`skin-map-sheet-6e.png`](../logbook/specs/skin-map-build/skin-map-sheet-6e.png) | the second and third sheets, kept beside the build record |
| [`skin_features.py`](../scripts/skin-map/skin_features.py), [`test_skin_features.py`](../scripts/skin-map/test_skin_features.py) | the feature reader, the projection and the check, with its test |
| [`gen_constants.py`](../scripts/skin-map/gen_constants.py) | writes the fit's numbers into the theatre's code |
| [`SpeciesDistance.cs`](../src/Evosim.Core/Genome/SpeciesDistance.cs) | the species distance the check compares against |
| [`TheatrePalette.cs`](../unity/Assets/Theatre/TheatrePalette.cs) | the theatre's per-body choices, seeded from the plan |
| [`striking-theatre-menu.md`](../logbook/specs/striking-theatre-menu.md) | the September ruling that skin genes were wanted |
| [logbook 0088](../logbook/0088-a-skin-for-the-boxes.md), [0104](../logbook/0104-the-theatre-gets-its-second-look.md) | the theatre's first skin, and the plan-hash skin |
| [`DECISIONS.md`](../DECISIONS.md) D008, D256 | the farm kept apart from the theatre; the plants' colour per world |
| [`unity/Assets/Theatre/CLAUDE.md`](../unity/Assets/Theatre/CLAUDE.md) | the skin moves no hash |
