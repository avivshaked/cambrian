# Shared-space spike — measurement report

D076's measurement, taken. The harness is `80fcedd` on `main`; the matrix ran on a quiet
machine on 2026-09-05 and **the answer is that shared space is free at every density the
ecosystem would run, and that what actually binds the footprint is packing, not cost.**

## Where the numbers are

| | path |
|---|---|
| matrix (23 cells) | `runs/spike-shared-space/2026-09-05-202709-matrix/results.csv` (+ `summary.md`, `config.json`) |
| tiled-nc arm (2 cells) | `runs/spike-shared-space/2026-09-05-202844-tiled-nc/results.csv` |
| logs | `scratch/logs/spike-matrix.log`, `scratch/logs/spike-tilednc.log` |

Conditions, from the report headers: Unity 6000.5.6f1, dt 0.01, 200 warm-up + 1,000 measured
steps per cell, test sine 1.2 Hz on every joint, seed 1, `GenomeFactory.Founder` bodies,
`configHash=8f6d7529503551d8` (tissue excess density 0.02, neutral body volume 0.25, drag
1.5, panels 2, added mass 0; current 0.3 m/s with rolls and advection, 4 patches).
`solverIterations=6`, `velocityIterations=1`, `maxDepenetrationVelocity=0.02`.

Worker 6 was refreshed from `main` at HEAD `3693262` immediately before the run;
`Assets/Evosim` simHash `995cda594d03655eaaaa55b8c6ed1a61e2a15952b687a73809c03820a6cda6d9`
and `Assets/Theatre` `c9cd6486…`, both identical between `unity/` and `unity-w6/`.

## Machine state

`Get-Process Unity` was **0** immediately before each launch and **0** after both finished.
During each run the count was **2**: my `unity-w6` process and its own asset-importer child.
No other Unity process appeared at any point; nothing else was started on the machine. The
matrix took 1.26 min of stepping, the tiled-nc arm 0.12 min.

---

# The four answers

## 1. The cost ratio, shared / tiled

| N | 10 m | 20 m | 50 m |
|---|---|---|---|
| 250 | 0.91× | 0.88× | 0.90× |
| 500 | 1.05× | 1.00× | 0.99× |
| 1,000 | 1.11× | 0.99× | 1.12× |
| 2,000 | *placement failed* | 1.03× | 1.04× |

**There is no cost.** Every ratio is between 0.88× and 1.12×, and the repeated cells put the
harness's own noise band at about that width: the identical `shared 10×10×60 N=250` cell
measured 0.652, 0.678 and 0.712 ms/step (a 1.09× spread) and `N=500` measured 1.258, 1.276
and 1.270 (1.01×).

The decisive detail is *which* cells are high. At N=1,000 the 50 m footprint — where the
contact counter reports **exactly zero contacts** — reads 1.12×, and the 10 m footprint,
which is the only one with any contacts at all, reads 1.11×. A ratio that is the same whether
or not there are contacts is measuring the machine, not the mechanism. DESIGN.md §5A.9's
expectation that contact cost is "rare and local" is not merely upheld; at these densities
contact cost is **below the noise floor of a 1,000-step timing**.

Where the time actually goes, at tiled N=2,000: fluid 3.44 ms, `Physics.Simulate` 1.99 ms,
settle 0.68 ms. **The fluid pass is 56% of the step and PhysX is 33%** — so the thing to
optimise if throughput ever binds is our own drag loop, not the solver, and certainly not
contacts.

## 2. The population that holds real time (≤ 10 ms/step at dt 0.01)

| space | footprint | largest N measured within budget | ms/step | headroom |
|---|---|---|---|---|
| tiled | — | 2,000 | 6.108 | 1.64× real time |
| shared | 10 m | 1,000 | 2.802 | 3.57× — *limited by packing, not cost* |
| shared | 20 m | 2,000 | 6.281 | 1.59× |
| shared | 50 m | 2,000 | 6.367 | 1.57× |

**All of 2,000 fits, in every footprint that can hold it, with 36–39% of the budget to
spare.** The `shared 10 m` row says 1,000 only because the 2,000-body cell could not be
placed; its cost, extrapolated from the other two footprints, would have been ~6.3 ms.

Measured scaling of the whole step, tiled: 0.719 → 1.200 → 2.534 → 6.108 ms at N = 250,
500, 1,000, 2,000. That is superlinear, about N^1.27 over the top octave (per-doubling
factors 1.67, 2.11, 2.41). Extrapolating on that exponent, **1× real time lands near
N ≈ 2,900**, alone on the machine, physics and fluid only.

Read that as a ceiling, not a forecast: no brains, no sensors, no economy, no births or
deaths are in this loop, and `MaximumPopulation` is 5,000. The honest statement is that
**shared space does not move the population ceiling at all** — tiled and shared reach it
together.

## 3. Density — contacts per body per step against bodies per m³

| footprint | N | bodies/m³ | pairs/step | pairs/body/step | contact points/step |
|---|---|---|---|---|---|
| 50 m | 250 | 0.0017 | 0 | 0 | 0 |
| 50 m | 500 | 0.0033 | 0 | 0 | 0 |
| 50 m | 1,000 | 0.0067 | 0 | 0 | 0 |
| 50 m | 2,000 | 0.0133 | 0 | 0 | 0 |
| 20 m | 250 | 0.0104 | 0 | 0 | 0 |
| 20 m | 500 | 0.0208 | 0 | 0 | 0 |
| 20 m | 1,000 | 0.0417 | 0 | 0 | 0 |
| 10 m | 250 | 0.0417 | 0 | 0 | 0 |
| 20 m | 2,000 | 0.0833 | 0.35 | 0.00017 | 0.37 |
| 10 m | 500 | 0.0833 | 0.02 | 0.00004 | 0.02 |
| 10 m | 1,000 | 0.1667 | 0.39 | 0.00039 | 0.71 |
| 10 m | 2,000 | 0.3333 | — | — | *placement failed* |

**These are individual events, not a rate.** The largest count in the matrix, 0.39 pairs per
step at 1,000 bodies in 10 × 10 × 60 m, is 390 pair-steps over the whole 1,000-step window —
one pair of creatures resting against each other for about 40% of the run, or a handful of
brief touches. That is why the column is not monotone in density: 0.0833 bodies/m³ gives 0.02
pairs/step in one cell and 0.35 in another. At this rarity the number is Poisson, and three
repeats of two cells is not enough to average it.

Where the knee is, from the instrument-test cell in the smoke: **at 41.9 bodies/m³ the count
is 13.47 pairs per body per step.** So a 250× increase in density from 0.167 to 41.9 buys a
~35,000× increase in contacts per body — the curve is steeply superlinear, and the ecosystem's
candidate footprints sit at the flat end of it, three orders of magnitude below where contact
cost becomes visible.

**The density the footprint proposal has to stay below is therefore not a cost limit.** Nothing
in the range 0.0017–0.33 bodies/m³ costs anything. The binding constraint is the next answer.

### The real constraint: packing, at 0.33 bodies/m³

`shared 10×10×60 N=2000` **failed to place** — rejection sampling jammed and the lattice
fallback could not fit the bodies either. This is the one hard number the matrix produced:

- founder bounding radii in that draw: mean 0.630 m, max 1.045 m;
- 2,000 mean spheres = 2,099 m³ of a 6,000 m³ box = **35% fill**, which is at the 3D
  random-sequential-adsorption jamming fraction (≈0.38) — so no non-overlapping arrangement
  is reachable by sequential placement;
- the same footprint at N=1,000 is 17% fill and placed, but took 17,735 rejections for 1,000
  bodies (17.7 per body, against 0.3 per body at N=250);
- 20 × 20 × 60 m at N=2,000 is 9% fill and placed easily.

**This is a statement about bounding spheres, not about solid bodies.** A founder's actual
collider is far smaller than the sphere that contains it, so 2,000 creatures would physically
fit in 10 × 10 × 60 m. But it maps onto a constraint the ecosystem really has: `Ecosystem.Build`
has to put a newborn somewhere that does not overlap, spawn-time depenetration is a force
(logbook/0007), and the search for a free spot gets expensive long before it gets impossible —
17.7 rejections per body at 17% fill, and unsatisfiable at 35%.

So: **today's `EVOSIM_AREA` 100 (10 × 10 m) cannot hold the populations the world already
runs.** At 60 m depth it is comfortable to about 1,000 bodies and unplaceable at 2,000.
`WorldAreaSquareMetres`' default of 400 (20 × 20 m) holds 2,000 at 9% fill with room to spare.
That is a footprint recommendation the cost measurement alone would never have produced.

## 4. Stability

- **No divergence anywhere.** Zero non-finite bodies in every one of the 23 matrix cells and
  both tiled-nc cells; no cell was cut short.
- **Nothing was flung.** Fastest body seen in any cell: **1.523 m/s**, and the same figure
  appears in the tiled cells, so it is the test sine driving a founder, not a contact
  artefact.
- **0 to 3 bodies left the volume**, out of 250–2,000, in 12 simulated seconds — and only in
  the 10 m and 20 m footprints, never in 50 m, never in tiled. This is advection, not
  instability: the current runs at 0.3 m/s, which carries a body 3.6 m in 12 s, and **the
  world has no horizontal bounds at all.** Depth is clamped at the top by D050 and the fields
  clamp at the floor, but x and z are unbounded.

  That is a finding for the world rules rather than for the harness. Tiling made horizontal
  bounds meaningless — a creature that drifted was still alone in its own tile. **A shared
  volume needs a boundary rule** (wrap, reflect, or a restoring current), and the choice is
  the owner's: a wrap makes the world a torus and changes what "a patch" means, a reflection
  puts walls in an ocean, and doing nothing means the population slowly advects out of its
  own light field. Nothing here needs it fixed to proceed, but the footprint decision and the
  boundary decision are the same decision.

---

## The tiled-nc arm — the layer question, isolated

Run separately at N=1,000 with a same-session tiled control
(`runs/spike-shared-space/2026-09-05-202844-tiled-nc/`):

| cell | ms/step | p95 | fluid | physics | settle |
|---|---|---|---|---|---|
| tiled (one layer, collisions enabled, 100 m apart) | 2.619 | 3.374 | 1.549 | 0.829 | 0.240 |
| tiled-nc (layer 8 ignoring itself) | 2.407 | 2.899 | 1.401 | 0.809 | 0.197 |

**Turning creature collision off entirely buys 2.4% of the physics pass** (0.829 → 0.809 ms) —
and the whole-step difference of 8% is mostly in the *fluid* pass (1.549 → 1.401), which the
collision matrix cannot touch and which is therefore noise of the same size. The matrix's own
tiled N=1,000 cell read 2.534 ms, between the two.

So the answer is: **leaving creature-to-creature collision enabled costs essentially nothing
even when 1,000 creatures are 100 m apart.** There was never a broadphase penalty being paid
for the tiling arrangement, and there is no performance argument for the layer trick in either
direction.

---

## What was measured, and what was not

- **No brains, no sensors, no economy, no births or deaths.** The drive is the test sine.
  Those costs are per creature and identical in both arms, so the shared/tiled ratio is
  untouched by their absence — but ms/step here is below the ecosystem's, so answer 2 is a
  ceiling.
- **Founders, not an evolved population.** Generation zero is one cell and sometimes a tail
  (365 parts across 250 creatures — 1.5 parts each). An evolved body is larger, presents more
  surface, and has a bigger bounding sphere, so **both** the contact rate in answer 3 and the
  packing pressure in the sidebar are *floors* for a world that has been running.
  `EVOSIM_SPIKE_BODIES=viable` runs the same matrix with `RandomViable` bodies (3+ parts) if
  that is worth a second pass — on this evidence it is the packing number, not the cost
  number, that would move.
- **Uniform placement, not the ecosystem's vertical distribution.** Bodies are spread evenly
  through the water column. A real population concentrates — at the floor, in the light, in a
  patch — so the local density that matters is higher than N/volume. The 41.9 bodies/m³ figure
  says there is a lot of room before that matters.

## Deviations from the spec, unchanged from the build report

1. **The `OnCollisionStay` counter the spec asked for reads zero in batch mode.** Unity does
   not dispatch MonoBehaviour collision messages outside play mode, `[ExecuteAlways]` included.
   Proved with the `contact-check` cell: physics went 0.19 → 1.43 ms/step while the callbacks
   reported 0 and `Physics.ContactEvent` reported 673 pairs/step. The counter is still attached
   and still reported in its own CSV columns; the working instrument is `Physics.ContactEvent`
   with `Collider.providesContacts` set per part.
2. **Today's tiling does not use mutually ignoring layers** — one layer, collisions enabled,
   distance alone. The `tiled` arm is that; `tiled-nc` above isolates the difference. This is
   why the shared arm needed no change to `PhenotypeBuilder`.
3. **`EVOSIM_SPIKE_CONFIG` cannot take a round-24 `config.json`** — those predate the `sense`
   tunable group and `RunConfigJson` refuses missing fields rather than defaulting them. The
   spike states the round-24 world itself and writes the hashed config it used beside the
   results.

## What this means for D076

Shared space is not a performance decision. The measurement that was supposed to price
creature-to-creature contact found that contact is too rare at ecosystem densities to price at
all — under one contacting pair per step among a thousand creatures, and no measurable cost
against tiling anywhere in the matrix. Two things came out of it that were not the question:

1. **The footprint is set by packing, not by cost.** 10 × 10 m cannot hold 2,000; 20 × 20 m
   can, at 9% fill. Spawn placement, not the solver, is what gets expensive as the box fills.
2. **A shared volume needs a horizontal boundary rule**, because the world does not have one
   and tiling was what hid that.

Both are world rules, and therefore the owner's.
