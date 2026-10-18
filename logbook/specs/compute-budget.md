# The compute budget

*2026-10-06. What the machine buys, in generations, and what each speed-up and each planned feature
would add to it or take from it. It serves D153, the owner's ruling of 2026-10-06: a speed-up that changes
the world is judged by its trade-off against the goal. The goal is very long runs of evolved bodies and
brains, with many creatures, on this machine, in a world that keeps growing more complex. Every number
says whether it is measured, believed (worked out from measured shares) or guessed. Re-measure and
rewrite this page when a lever lands.*

## The unit

A farm round is one shared world, so its cost is not counted in evaluations. It is counted in
**generations per machine-day**: how many generations of bodies one day of the machine runs. The day
is taken inside the owner's limit of 16 of the 32 logical processors.

A generation is about 1,000 simulated seconds. A leaf, the body that fills every recent round, lives
about 1,250 s and first breeds at about 800 s (measured on round 55). The world sets that tempo, and no
speed-up below changes it.

## What a farm run costs (measured)

Rounds 55 and 56 at 8 threads, 236 windows across four seeds, fitted:

> wall seconds per simulated second ≈ 0.12 + (living links) / 14,000

- **The world floor, 0.12 to 0.14 s per simulated second**, is paid whatever lives: the snow, the
  fields, the current. Physics is 72 to 77% of a round's wall and the world step 20 to 25%.
- **Each living link adds a cost.** A body is stepped 100 times a simulated second, and every link
  samples the water, feels drag and is searched for contacts. At 8 threads the farm runs about 11,000
  to 12,700 link-seconds per wall-second.
- **Today's bodies are small**: the living average 1.4 to 2.1 links (`fluidLinkSteps` over
  `harnessBodySteps`). Most of the crowd is rigid bodies of one or two parts. The jointed were 0.05 to
  7.6% of the living at the ends of round 56's seeds and of round 57's screen. A crowd of 7,500 is
  about 15,000 links.
- The fit was taken with two or three runs sharing the machine. Round 56 seed 2's profile, alone at 8
  threads, ran 0.88 times real time at 7,500 bodies, where the fit gives 0.85.

## Generations per machine-day, today

One seed at 8 threads, so the machine runs two seeds at once:

| The crowd | Links | Wall per simulated second | Generations a day, per seed |
|---|---|---|---|
| 2,000 leaves | 4,000 | 0.40 | 216 |
| 7,500 leaves (round 56's end) | 15,000 | 1.18 | 73 |
| 7,500 bodies of 5 links | 37,500 | 2.77 | 31 |
| 7,500 bodies of 10 links | 75,000 | 5.4 | 16 |
| 2,000 bodies of 10 links | 20,000 | 1.53 | 56 |

All computed from the fit. Whether one seed at 16 threads runs twice as fast as at 8 is not measured.

What "really evolved" might need is open. The owner has said they do not know how long that takes, since
no long run has been made. The closest study in the literature ran 150 generations of 300 rigid-body
creatures, 25 to 30 times over [K12 §3.3, p.10]. Another ran 1,000 generations of 100 voxel creatures
[EA23 §3.4, p.10] (both in DESIGN.md §1). Round 57 runs about 50 generations a seed. My inference: a few hundred to a
thousand generations is the scale to plan for. At today's crowd of small leaves, 1,000 generations is
about two weeks of the machine for two seeds. With 7,500 bodies of ten links it is about two months.

That last line is the budget's main point. The goal asks for bodies that evolve to be complex, and
the cost grows with every link they evolve. A speed-up worth 1.3 times today is still worth 1.3 times on
a crowd of complex bodies. But that crowd may be five times heavier.

## The levers

| Lever | What it buys | Kind | Status |
|---|---|---|---|
| Bit-identical CPU work (the speed plan's farm items: A2 variants, P9, P10, F1, F2) | 1.15 to 1.3 times (believed) | Same world | The speed plan, step by step |
| The water held 0.5 s at a part (W1) | about 1.24 times (measured beside a second run, not alone) | World rule | Round 58 candidate |
| N1, N2, R2, R3, B3, B4 | 1 to 15% each (believed or guessed) | New realisation or world rule | Round 58 candidates, `proposals/propose-r58-speed.md` on `nursery-speed` |
| The snow's transport on the card (T1) | about 20% of the wall (51's estimate) | Same world within rounding, checked | After the card's hold |
| The solver on the card in single precision (N3) | about 6 times the solver on the CPU; the solver proper is under 5% of the samples today, so the gain depends on what else moves with it | New realisation | The round 53 card experiment, then the owner |
| A cheap tier for still bodies | Unknown. Rigid bodies are most of the living, so one stepped at a quarter of the cost would make a crowd of them up to about 3 times cheaper (guessed) | World rule | Not designed |
| Fewer bodies | Linear in links, down to the world floor | World rule | The owner's |
| Shorter lives | Linear in generations: a world whose generation is 500 s doubles every row above | World rule (the tempo dial the owner left for later) | The owner's |
| The nursery | A generation of 192 tanks of 16 in 98.7 s on 14 threads (measured, D152's base recipe): about 875 generations a day | A different experiment: episodes, scored | Running; round 55's inoculants did not take in the world |

The levers multiply when they touch different costs, and do not when they touch the same one. The
contact search and the water at the links are a quarter and a third of the samples (the farm profile
of 2026-10-05). W1, N1 and R2 all cut the water, so their gains overlap.

The cheap tier deserves a measurement first, since it is the only lever aimed at the crowd itself. Two
readings would size it. One is the share of the bodies' phase that goes to rigid bodies against jointed
ones. The other is how much a rigid body's motion relative to the water changes from step to step. Neither is
taken.

## What the planned complexity costs

The owner wants a more complex world: more shapes, more cell types and more world features (D153).
Each is costed against the same unit. None of this is measured; the shares are from the profile.

| Feature | Where its cost lands | Guess |
|---|---|---|
| Larger, jointed bodies | Every link pays the water, drag and contacts; the articulated solve grows with the joints | Linear in links (the table above); the solve is under 5% today |
| The light sense (round 58) | One light reading per body per brain step | Under 2% |
| More cell types | A sensing cell that samples a field each step costs like a water sample, about a link's worth; a cell that only changes the economy costs almost nothing | 0 to 1 link per cell |
| More shapes | The contact test per pair, and drag on a shape that is no box | Small for spheres and capsules; large for meshes |
| More world features | Each feature with a distance field is read by every link near it; the reef's `SignedDistance` alone is 4.7% of the samples | A few % each |
| Predation on contact | The contact pairs already found, plus a rule per pair | Small |

## What to measure next

- The farm at 16 threads against 8, on one seed, alone, to learn whether one seed runs twice as fast.
- The share of the bodies' phase spent on rigid bodies, and how still one is relative to its water.
- The pace of round 57 itself, whose winters cut the crowd: the fit should hold, and its reader
  records the living per year.
- Each round 58 candidate's gain alone, as the proposal sets out.
