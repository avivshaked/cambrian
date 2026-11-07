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
gripping plant gains nothing by growing taller. A real kelp forest rests on three things our world lacks or has only
weakly. Its blades earn by their height, so plants stack into a canopy. It sheds most of what it grows as drifting
food, which keeps its grazers fed without eating the living stand. And it stands on reef in lit water. I propose three
steps, in this order, each behind its own switch and each started overpowered (D262). First, light and matter at each
part (D297), which is built. Second, blade turnover: a plant's oldest blade wears out on a schedule and drifts off as
food, and the module rule grows it back. Third, more lit rock from D204's shoals, only if room still limits after the
first two.

## What the measures found

| Measure | Found | File |
|---|---|---|
| What a grip is worth | Children of the same gripping parents, born alike, that settled on rock had 3.75 to 7.53 children each, against 0.69 to 1.41 for their siblings that found no spot and drifted (four worlds) | `grip-read/grip-test.out` |
| Grips that no rule placed | 16 to 72 gripping bodies a world are children of plants that never gripped, 10 to 34 % of all gripping bodies | `grip-read/grip-test.out` |
| Room | 63 to 87 % of the children that try to settle find no spot near their parent and no deeper than it | `rest-read/read5-pld.out` |
| Height | Gripping plants sit at a median 3.6 to 4.1 m, drifting plants at 2.1 to 2.4 m. A body earns light at its centre, so a taller plant earns nothing for its height | `grip-read/grip-worth.out` |
| The winter | With the rest at a delay of 100 to 600 s, a forest held through the full winter on 6202 (74 gripping bodies on one island) and failed on 6203 | `forest-run.md` |
| Dropped blades today | A blade the module rule drops becomes a corpse that sinks at 5 cm/s, settles and decays with a half-life of 500 s, and a mouth can eat a corpse (D129). Plants drop blades only when poor | `WorldModules.cs`, the run's config |

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

## 2. Blade turnover (a world rule, the owner's)

A plant's added blades wear out. Each added blade older than the turnover time is shed and becomes a corpse in the
water, as D106's drop makes one today, and the module rule grows it back while the plant can afford it. The turnover
time starts short, overpowered, and is dialled back. It reads only the blade's own age, so the rule never reads the
world's state.

What it should do: a forest then sheds a steady stream of food at its island. Grazers that eat corpses can live on it
without biting the living stand. The shed matter settles and decays at the island, where the plants draw it again, so
the forest feeds its own water. Two things must be checked before it is specified. One is whether today's grazers find
and eat corpses, or only bite living blades. The other is how the matter books carry a steady shed, and the rule then needs a
builder.

## 3. More lit rock (D204's shoals)

D204 ruled the island shoals and they were never built. They would give each island a shallow bed of rock in the light.
They come only if room still limits the forest after steps 1 and 2, since they change the islands' water and matter in
the same stroke.

## Not proposed

A rule that grows a plant toward the light. The survey found no source for directed growth in kelp: fronds rise because
the growing tip and its float point up. Here, height should come from evolution once D297 makes it pay.

## What it implies

- **For the record:** step 1 needs no new decision. Steps 2 and 3 each take a decision, and step 3 builds D204.
- **For the rounds:** the forest world becomes the base for the grazer rounds, and it departs from round 67's world.
- **For the machine:** one card screen of about 2 h for step 1, a builder's window and a screen for step 2, and a
  larger build for step 3.
- **The risk:** D297 changes how animals earn as well, and a forest that feeds grazers can still collapse to a barren
  if they outgrow its shedding.

## The questions

1. Step 1: run the screen with light and matter at each part, on four seeds?
2. Step 2: rule blade turnover as a world rule, to be specified and built after step 1's result?
3. Step 3: hold the shoals unless room still limits?
