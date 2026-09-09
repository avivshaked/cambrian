# 0078 — The water as a grid

**2026-09-08**  ·  a representation replaced in one evening, built by agents, reviewed, smoked; two rulings pending

Round 31 was still running when the owner asked where the hole lives. In the vertex world
a take is spread over the vertices within a metre of the mouth by their weights, so the dip
is centred on the vertices and not on the eater, and its footprint is two kernel radii
because each reduced vertex casts its own. Five bodies feeding from the tails of five
vertices lower the water most at the vertices, and a sixth body sitting beside one of them
reads the loss at nearly full weight. It pays for a meal it did not eat. The owner's first
answer was a negative vertex founded at the mouth with the take's amount, so that the field
has a source to lower where the eater is. It is the right diagnosis. The second answer was
to stop representing the water with objects that have to be placed: a grid of cells, each
holding an amount, a body feeding from the one cell it stands in, diffusion between
neighbours, and corpses as particles that decay into the cell they are in. That is
`fable-propose-grid.md`, and this entry is its build.

## The rules, in one paragraph

Two grids over the whole box, detritus at a metre a side and matter coarser. A cell holds
joules or units, never a density. A body feeds from the cell holding its centre, under the
frozen-availability rule the cell field always had. Diffusion is Fick's law between face
neighbours, linear in the difference, at the field's own rate on every axis, zero flux
through the surface and the floor, wrapped at the seams; the scheme is refused above its
stability limit rather than clamped. The current carries stock downstream by upwind
transfer, which spreads a patch along the flow as a by-product, and the build measures that
number. Sinking is the same transfer downward. A corpse is a particle that sinks, rides the
current and decays into its cell. Every operation is a transfer between named places, so
both books close by construction.

## The build

Built in one evening by three agents under the owner's rule that legwork goes to them. An
explorer mapped the seam: every member of the field interface, every call site in the
world, the current's API, the chemical sense's read, the config guards, the vertex tests.
A builder wrote `GridField` behind the interface with the two older fields untouched, its
own contract tests held to the vertex field's, and the three experiments of logbook/0076
ported so the two representations read side by side. A reviewer read the diff against the
spec and the interface's documented contracts and found thirteen things, one of them a
trap. A fourth agent wired the harness and smoked it on the one free worker.

The trap was the sideways mixing knob. The world hands one sideways diffusivity to both
fields, and the two have different vertical rates by design: detritus at the round's
0.02, matter at 2. A grid refuses an anisotropy, so the only value the first build
accepted was zero, which it then read as "stir sideways at the vertical rate". The run
header would have recorded no sideways mixing for a world that mixed sideways. The fix
keeps the header honest: a grid world refuses a sideways rate that differs from the
detritus rate, so the token reads what the detritus does, and the matter grid is mixed at
its own rate on every axis, stated in the knob's remark. The rest of the review was
remarks that asserted something the arithmetic did not, two experiment assertions that
could not fail, a slab volume that meant one thing on the grid and another on the older
fields, and one edit on the cell world's burial path that had no test guarding it. All
fixed; the burial path now has a regression test that the new call is the old call
exactly. The suite is 579.

## What the experiments say

The sitter against the mover on the grid, cell 1 m, 200 s, blind mouths:

| mixing m²/s | mover m/s | sitter J | mover J | mover over sitter |
|---|---|---|---|---|
| 0.2 | 0.03 | 145 | 146 | 1.01 |
| 0.2 | 0.30 | 141 | 154 | 1.10 |
| 0.02 | 0.03 | 34 | 40 | 1.18 |
| 0.02 | 0.30 | 31 | 99 | 3.20 |

The vertex field read 0.95 to 1.11 at 0.2 and 2.2 to 2.5 for a swimmer at 0.02
(logbook/0076). The grid's prize for a swimmer is larger, because the hole is at the
mouth and one cell wide, and coarser cells blunt it: 2.4 at 2 m, 1.8 at 3 m. The corpse
patch reads 9.2 times the mean at 10 s and 1.2 at 100 s at 0.02, against 5.5 and 1.2 on
vertices. The owner's geometry, five mouths packed in one cell and five spread one to a
cell with mixing off: the packed five took 1.0 J between them and the spread five 5.0 J,
and every cell beside the packed one kept its stock to twelve decimal places. A cell has
walls. The current's along-flow spread at 0.3 m/s on 1 m cells is 0.13 m²/s, six times
the round's mixing, which is the number the design states as the current's stirring.

## The smoke

`r32smoke`, seed 1 on worker 4 at dt 0.02 for 300 s: `field grid`, `cell=1 mcell=2.5`,
`mixing 0.02 m2/s`, `h-mix 0.02 m2/s`, ended on budget, no divergence, `audit` 0.0000% and
`mat resid` 0 on every row. The first attempt on the launcher failed on the trap above,
and its manifest is kept beside the second's. Worker 4 carried a stale lockfile from the
arm that crashed on it (0077); it was removed after a check that no process held it.

## The corpses

*2026-09-09, small hours.* Rule 6 followed the grid by a fifth agent. A death founds a
corpse carrying the body's tissue joules and its locked matter at the place it died. Once
a metabolic step, before the fields' own passes, every corpse sinks at the detritus sink
speed, rides the current where the fields do, wraps at the seams, and pays the decay rate
times the step of what it still holds into the nutrient and matter fields at its position.
Under a millionth of a joule and a unit the rest goes in whole and the corpse is dropped.
Both books count a corpse as standing, so `audit` and `mat resid` read zero while a body
is half water and half object. The knob is `CorpseDecayPerSecond`, default 0, at which the
death path is the old two deposits verbatim; every world on record replays. The table
gains a `corpses` column and the statistics three fields.

Six tests. The half-life at 0.005 per second measured 138.5 s against 138.6 predicted.
The grid closure test with decay on holds both identities to a millionth over 3,000 steps
with births, deaths, influx, burial and a rolling current. The smoke on worker 4 at
0.005 per second ended on budget with 35 corpses for 35 deaths in 300 s, the identities
closed, the same last row on three launches. The suite is 585.

## Two rulings the build put in front of the owner

- **The matter cell.** The campaign's box is 20 m by 5 m by 60 m, and 3 m divides only the
  depth. What divides all three is 2.5 m, whose cell holds 15.6 m³ and caps the largest
  child a full cell can afford, and 5 m, whose cell holds 125 m³ and loosens the gate about
  five times over the vertex world's 24 m³. My recommendation is 5 m: it removes a size cap
  the world never had, and the population's matter ceiling still binds at the world's total
  stock. The smoke ran at 2.5 m.
- **Matter mixed on every axis at 2 m²/s.** The vertex world walked matter at 2 vertically
  and 0.02 sideways. On a grid it goes to 2 on every axis. I recommend it stands, since a
  cube has no preferred axis and the vertical rate was the one chosen for the gate.

## Screens for the matter cell

*2026-09-09, morning.* Round 31 landed and freed the workers, so the matter-cell ruling
gets numbers rather than arithmetic. Four fast-step screens, dt 0.02 for 10,000 s, seeds 1
and 2 at each size: `r32q-m25-s1` and `r32q-m25-s2` at 2.5 m on workers 2 and 3,
`r32q-m5-s1` and `r32q-m5-s2` at 5 m on workers 5 and 6. Everything else is round 31's
launcher on the grid with corpses at 0.005 per second. Every header reads `field grid`,
`cell=1`, the arm's `mcell`, `corpse=0.005/s`, `mixing 0.02 m2/s`, `h-mix 0.02 m2/s` and
`work x0.25`; every manifest `simHash b7f589d0…`, `coreHash 9e1f47b2…`, `physicsJobWorkers 0`,
and the launcher refused any other `simHash`. Read within the step and only for whether
the world stands, the identities close, a stomach line forms and `mat blk` against births
at each cell size. Nothing about depth or joints is read from them.

All four ended on budget in under an hour each. Read against round 31's screens of the
same seeds (`r31q-s1`, `r31q-s2`, the vertex world at the same mixing).

| arm | alive | stomach line | scorer | refusals per birth, 3,000 to 10,000 s | matter locked in bodies | free matter at the mean depth | corpses standing |
|---|---|---|---|---|---|---|---|
| `r32q-m25-s1` | 1,544 | 108 | pass, 109 | 7,450 | 4,713 | 0.183 | 371 |
| `r32q-m25-s2` | 1,552 | 126 | pass, 127 | 8,540 | 4,727 | 0.185 | 329 |
| `r32q-m5-s1` | 1,838 | 59 | pass, 56 | 7,480 | 5,745 | 0.023 | 366 |
| `r32q-m5-s2` | 1,864 | 209 | fail, a line born at 8,240 s | 7,450 | 5,788 | 0.022 | 390 |
| `r31q-s1` | 1,524 | 46 | fail | 6,370 | 5,800 | 0.011 | |
| `r31q-s2` | 1,570 | 33 | fail | 6,440 | 5,786 | 0.049 | |

The grid stands at both sizes, the identities close on every row of every arm, a stomach
line forms in every seed and the scorer passes three of four at 10,000 s where the vertex
screens passed neither seed. The refusals per birth are the same at both sizes and within a
fifth of the vertex world's, which says the gate binds on the world's stock and not on the
cell. What the cell size moves is where the stock sits. At 5 m the matter locked in bodies
is 5,750 to 5,790 of the world's 6,000, the vertex world's figure to within 1%, and the
free water reads 0.02. At 2.5 m it is 4,710 to 4,730, with 0.18 standing free: about a
fifth of the world's matter sits in cells too small to afford a child and is never locked.
That is the size cap the arithmetic predicted, seen as stranded stock, and it costs the
2.5 m worlds about 300 bodies. The 5 m cell reproduces the vertex world's matter economy
and the 2.5 m cell does not. My recommendation stands at 5 m, with the numbers behind it.
Nothing about joints is read here: the count of bodies born with one peaked at 0 to 3.
The share of bodies carrying a sense is 25% in one arm of each pair and 1 to 3% in the
other, the same spread round 31 showed at the fine step.

## What it costs the record

Every config written before this build lacks the two cell tunables and is refused by it,
rounds 30 and 31 among them, per §9's rule. Their arithmetic replays on the vertex field
under their own builds. Every seed under the grid is a new realisation, and round 32 is the
grid's base round at round 31's prices and mixing before anything else is asked of it.
