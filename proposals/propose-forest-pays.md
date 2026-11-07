# Proposal: the forest pays its plants and feeds its grazers

*Draft for the owner, 2026-11-07, after D323's screen and the owner's question that day: what moves a plant to grip,
and why does a gripping plant not grow toward the light? It builds on `logbook/specs/plants-hold/forest-run.md`, the
sibling test in `logbook/specs/plants-hold/grip-read/`, and the survey `logbook/specs/plants-hold/grip-research.md`,
whose sources were read at abstract level. World rules are the owner's. Each step says which parts are agent work.
Absorbed into DECISIONS.md on ruling, then deleted.*

## The proposal in one paragraph

The owner wants a forest because it is the key to grazers that feed properly without their numbers running away. A grip
already pays: a plant that settles on rock breeds about five times as much as its sibling that drifts. What stops the
forest is that it cannot spread. Most children that try to settle find no spot, the winter kills the grippers, and a
gripping plant gains nothing by growing taller. A real kelp forest rests on three things. Its blades earn by their
height, so plants stack into a canopy. It sheds most of what it grows as drifting food, which keeps its grazers fed
without eating the living stand. And it stands on reef in lit water. Our world lacks the first and has the third only
weakly. The second it has already: the forest sheds a large stream of dead plants within the grazers' reach, and they
take 1 to 2 % of it, because they are few. So I propose two steps, each behind its own switch and each started
overpowered (D262). First, light and matter at each part (D297), which is built. Then more lit rock from D204's shoals,
only if room still limits after the first. Blade turnover, drafted as a second step, is withdrawn.

## What the measures found

| Measure | Found | File |
|---|---|---|
| What a grip is worth | Children of the same gripping parents, born alike, that settled on rock had 3.75 to 7.53 children each, against 0.69 to 1.41 for their siblings that found no spot and drifted (four worlds) | `grip-read/grip-test.out` |
| Grips that no rule placed | 16 to 72 gripping bodies a world are children of plants that never gripped, 10 to 34 % of all gripping bodies | `grip-read/grip-test.out` |
| Room | 63 to 87 % of the children that try to settle find no spot near their parent and no deeper than it | `rest-read/read5-pld.out` |
| Height | Gripping plants sit at a median 3.6 to 4.1 m, drifting plants at 2.1 to 2.4 m. A body earns light at its centre, so a taller plant earns nothing for its height | `grip-read/grip-worth.out` |
| The winter | With the rest at a delay of 100 to 600 s, a forest held through the full winter on 6202 (74 gripping bodies on one island) and failed on 6203 | `forest-run.md` |
| Dead plant matter today | Contact eating is on in the forest worlds (D219, D220), so no corpse forms. A dropped blade, and every body that dies, stays in the water as remains a mouth can bite for 500 s. Then the remains burn: their energy leaves the world and their matter goes to the spent field where they lie, which the plants draw on. Plants drop blades only when poor, 13 to 2,689 drops a world | `WorldModules.cs`, `WorldContact.cs`, the run's config |
| What grazers eat | Over 12,000 s in six forest worlds, mouths took 1,490 to 8,531 J from remains and 11,073 to 99,044 J from living leaves, while 297,000 to 715,000 J of remains burnt uneaten. The forest already sheds a large stream of dead plants, and the grazers take 1 to 2 % of it | `remains-read/remains-eaten.out` |
| Where the remains lie | After 3,000 s, plant remains sit at the living plants' depth (median 2.4 to 2.9 m against 1.8 to 2.6 m), barely sink, and drift a median 4 m in their 500 s. They lie as near an animal, any living body without a leaf, as living plants do (within 3 m of one: 1.9 to 13.4 % of remains, 2.8 to 18.8 % of living plants). A sample holds 14 to 117 animals against 900 to 2,400 plant bodies, a fifth of them remains | `remains-read/remains-where-*.out` |

## What real forests do

From the survey, at abstract level; the parts it marks as uncited are left out here.
- A holdfast keeps a kelp in shallow, lit water where moving water brings nutrients. Drifters win where there is no lit
  rock, as Sargassum does in the open sea.
- Recruits settle mostly within a few metres of the parent, with rare long jumps in storms. Hard rock limits a forest.
- A canopy shades what grows beneath it, to about a tenth of the surface's light under a dense one, and kelp moves
  stored carbon from old blades to growing ones.
- Kelps shed about 82 % of their production as detritus. Urchins in a kelp bed eat drift and almost no living kelp.
  When drift is scarce they turn to the living plants, and a forest can collapse to a barren, which takes far fewer
  grazers to hold than it took to cause.
- Perennial kelps carry the winter on stores built in summer.

## 1. Light and matter at each part (D297)

D297 is built and switched off: each part earns light at its own height and column, casts its own share of shade, and
draws matter from the cell it stands in. It was settled by the coordinator under D291, so its screen is agent work, and
it waits for the owner only because the owner asked to discuss before acting. On, it gives a gripping plant a reason to
raise its blades, and a canopy a reason to form. It applies to every body, animals included, so the world departs from
round 67's, and its cost per step is unknown.

The screen: the 100 to 600 s rest world with D297 on, on 6202, 6203 and two new seeds, for 12,000 s, one card group of
about 2 h, against D323's worlds. It reads the forest test, the height of the gripping plants' blades, the blades per
gripping plant, children per gripping plant against drifting, the grips of their own, and the pace. Height has to evolve,
since no rule points growth at the light. A gripping plant lives about 2,000 s, so 12,000 s is about six generations,
and if the blades start to rise, a long world follows.

## 2. Blade turnover, withdrawn

A plant's added blades wear out. Each added blade older than the turnover time is shed and stays in the water as
remains, as a module drop leaves them today. The module rule grows it back while the plant can afford it. The
turnover time starts short, overpowered, and is dialled back. It reads only the blade's own age, so the rule never reads
the world's state.

What it was meant to do: give the forest a steady stream of food at its island, which grazers could live on without
biting the living stand. The read of the six forest worlds changes the order. The forest already sheds a large stream of
dead plants, and the grazers take 1 to 2 % of it and bite living leaves five to twelve times as much. Blade turnover alone
would add to a stream the grazers leave. The matter books need no change: burning remains send their matter to the spent
field where they lie, so a shed stays at the island if its remains do.

The read of where the remains lie answered why. The remains are within reach: they hang among the living plants at
the same depth and lie as near the animals. Per body, the animals bite them a third to two thirds as much as living
leaves (inference from the totals; why less is not read). What is scarce is animals: 14 to 117 in a world of 900 to
2,400 plant bodies. The forest already makes more drift food than its grazers can eat, so blade turnover would feed no
one new. What holds the grazers back is their own side, the search and the pace of life that the grazer work already
names. I would take step 2 out of this proposal. Blade turnover can come back later as a question about the plants, a
way to shed weight before winter.

## 3. More lit rock (D204's shoals)

D204 ruled the island shoals and they were never built. They would give each island a shallow bed of rock in the light.
They come only if room still limits the forest after steps 1 and 2, since they change the islands' water and matter in
the same stroke.

## Not proposed

A rule that grows a plant toward the light. The survey found no source for directed growth in kelp: fronds rise because
the growing tip and its float point up. Here, height should come from evolution once D297 makes it pay.

## What it implies

- **For the record:** step 1 needs no new decision. Withdrawing step 2 needs none either. Step 3 takes a decision and
  builds D204.
- **For the rounds:** the forest world becomes the base for the grazer rounds, and it departs from round 67's world.
- **For the machine:** one card screen of about 2 h for step 1, and a larger build for step 3.
- **The risk:** D297 changes how animals earn as well, and a forest that feeds grazers can still collapse to a barren
  if they outgrow its shedding.

## The questions

1. Step 1: run the screen with light and matter at each part, on four seeds?
2. Step 2: withdraw blade turnover and leave the grazers' side to the grazer work? The forest already sheds more food
   within the grazers' reach than they eat.
3. Step 3: hold the shoals unless room still limits?
