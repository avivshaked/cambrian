# Round 67's plants at their end: a census of body plans

The owner started the plant work on 2026-11-06, with plant varieties part of it. This census is its starting point: what
plants round 67's worlds held when they ran away over the ceiling of 25,000 bodies (`logbook/specs/r67-read/looks.md`).

## How it was read

`genome-query.py plans <run> --at T --cell photosynthetic --top 1000000 --json` groups the living plants by plan, the
signature of the nodes development reaches. `scripts/reads/plant-census.py` counts the plans and their makeup. The
worlds are `runs/r67r1-6202` at 44,000 s, `r67r1-6203` at 38,000 s and `r67r1-6204` at 43,000 s, each a few hundred
seconds before its runaway. The node count is what a genome can build, which bounds the parts a body has from above,
since development may prune.

## What the worlds held

| | 6202 | 6203 | 6204 |
|---|---|---|---|
| Living plants | 19,181 | 18,688 | 18,897 |
| Distinct plans | 1,657 | 2,495 | 2,482 |
| Effective number of plans | 16.4 | 19.0 | 21.4 |
| Genomes that build one node | 46.1 % | 51.4 % | 56.0 % |
| two nodes | 39.1 % | 28.8 % | 29.9 % |
| three or more | 14.8 % | 19.7 % | 14.1 % |
| Root a superellipsoid | 86.2 % | 29.1 % | 51.2 % |
| Root a box | 12.0 % | 66.1 % | 47.5 % |
| Light-catching cells only | 91.2 % | 87.7 % | 89.2 % |
| A moving joint below the root | 0.8 % | 1.5 % | 4.2 % |

The commonest plan in every world is one flat blade, alone. On 6202 it is a superellipsoid 69 by 65 cm and 3 cm thick,
held by 3,400 plants, 17.7 % of them. On 6203 it is a box 52 by 107 cm and 18 cm thick (3,166 plants), and on 6204 a
superellipsoid 74 by 71 cm and 18 cm thick (3,061 plants). The next plans are blades of the same kind, single or
doubled, 40 to 90 cm across.

Few bodies grew by the module rule. Each world held 129 to 223 grown-on modules in all at its end. On 6202, 3,659
additions went through and 756,984 were refused for shape, most of them not plants' (`refusal-probe.md`).

## What it says about varieties

- **The plans differ in detail, and the plants do not differ in kind.** About nine plants in ten are made of
  light-catching cells alone, and almost none move a joint. A few carry a second kind of cell: buoyancy on 6203 (4.6 %),
  link or consumer cells on 6204 (4.6 and 3.5 %), structural cells on 6202 (3.0 %).
- **The shape family reached the plants.** No founder carried the superellipsoid, random or trained (checked on 6202
  and 6203), so it entered by mutation. It is the root of 86 % of 6202's
  plants and half of 6204's, as flat discs, where among jointed bodies it held 35 % on 6202 and 12 % on 6204.
- **No plant is large.** The largest plans build a few nodes, and the body with the world's largest part on 6203 had
  two parts.

Inference: the worlds hold one variety of plant, the drifting blade, in many sizes and in two shapes. A second variety
would be a plant that makes its living a different way: one that holds the rock (D296), one that grows tall towards the
light (D297), or one that keeps its matter in many blades rather than budding them off (the owner's idea of
2026-11-03). Each is a rule the owner rules on.

## What a part costs against a body: an estimate under load

The owner's second plant idea asks first what a part costs against a body. Round 67's stats rows carry each stage's
wall time with the body steps and link steps behind it, so the cost can be estimated from the run itself. Over 6202's
last 6,000 s (38,090 to 44,080 s, 18,415 bodies on average, 113 harness steps a body a simulated second):

| Per simulated second | Machine time |
|---|---|
| A body's harness (settling, metabolism, growth, the senses) | 30.6 µs |
| A body's share of the world's step (births, deaths, fields, the ledger) | 17.9 µs |
| A link's physics | 21.9 µs |

So a body costs about 48 µs a simulated second before its parts, and each part adds about 22 µs. A plant of four blades
held as one body would cost about 136 µs, against 280 µs for the same four blades as four bodies. Over the run the
harness's cost a body step rose with the crowd, from 0.10 µs near 8,500 s to 0.31 µs near 40,500 s. The physics cost a
link step fell, from 0.59 µs to 0.22 µs, as the card's work grew more parallel.

These are readings under load: four worlds shared one process on logical 20 to 31, and a timing read is taken with
nothing else running (the root `CLAUDE.md`). They give the size of the saving, about half for a four-blade plant, and
a quiet timing is still owed before a figure is ruled on. The contact reach of a large part is not measured here.
