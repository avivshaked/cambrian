# r59-pos64: single precision's contacts, measured

`r59-pos64` is a measurement branch off `r59-anchor`. It asks how much of the card's contact shortfall in single
precision can be cured, and at what cost. Its numbers are made off the card, on ILGPU's CPU accelerator. The world is
round 57's with round 59's pieces, resumed from `r59smoke-t16`'s checkpoint at 1,000 s for 10 s. The launcher and its
two readers are under `logbook/specs/r59-build/`. The diagnosis that led here is in `r59-anchor-build-plan.md`, under
step 8's card half.

The owner approved the carry, the local frame and the lattice as one change on 2026-10-13 (D168). It was to land on
`r59-anchor` if a card window showed it cost under about 5% of the pace. It cost about 1%, and it is merged.

## What was built and what it bought

Per step over the 10 s:

| Build | Commit | Pairs | Held | Unheld | Bed or glass | Bodies over 1 cm from double |
|---|---|---|---|---|---|---|
| Double, and the CPU engine | — | 13.20 | 13.00 | 0.20 | 5.92 | — |
| Single as the card runs it | `cc7ee3f7` | 6.08 | 5.79 | 0.29 | 3.14 | 62 |
| The base position's carry | `8c977913` | 7.18 | 5.73 | 1.45 | 3.49 | 2 |
| Contacts in a local frame | `2175d8de` (pairs only) | 12.33 | 11.58 | 0.75 | 3.57 | 0 |
| The same, with the floor's and the glass's first-order terms | `2175d8de` | 12.33 | 11.58 | 0.75 | 4.21 | 0 |
| The same, with the glass's term alone (built for this run, not committed) | — | 12.33 | 11.58 | 0.75 | 4.20 | 0 |
| The floor and the glass on a 0.25 m lattice | `d41ab4bf` | 12.33 | 11.58 | 0.75 | 5.45 | 0 |

Alive, births and deaths are double's in every row, and the last run has no overflow. In double every change is
switched off and the kernels keep the solver's arithmetic, which the double-on-accelerator identity tests show.

The carry makes the base position a compensated sum. Without it, a free body near x = 150 m drifting at 0.01, 0.05
or 0.2 mm/s does not move at all in single (`DriftDiagnostic`). With it, the body moves as the CPU moves it.

The local frame keeps each link position's rounding error beside it and takes every contact difference from the
parts. The broad phase is untouched.

The lattice takes the contact's floor and glass from the nearest point of a 0.25 m lattice, described below.

| PTX instructions | `r59-anchor` | The local frame | The lattice |
|---|---|---|---|
| The class step kernel | 23,139 | 23,445 | 23,692 |
| ContactLinks | 4,008 | 4,074 | 4,325 |

The counts are static: a loop's body is counted once. None of the three has a predicated `bra.uni`. Memory grows by
12 bytes a body and three floats a link row in each sphere set.

## On the card

One window on 2026-10-13, alone on the card, ran `r59-build/d168-window.ps1`. It resumed round 57's world from 1,000 s
for 300 s, `r59-anchor`'s build and this branch's interleaved at each group (anchor, this, this, anchor). The pace is
the stepping wall's, in times real time, over 290 s with about 3,500 alive:

| Group | `r59-anchor` | This branch | Cost |
|---|---|---|---|
| 32 | 3.811, 3.802 | 3.769, 3.770 | 0.97% |
| 64 | 3.718, 3.669 | 3.659, 3.642 | 1.16% |

At group 64 the anchor's own pace fell 1.3% from its first run to its last, as the card warmed. So the cost reads
as about 1%, give or take 1%. Each build's runs are identical between the groups and between repeats (`gpu-accept.py`:
digests on all 300 rows, lineage byte-equal).

A probe of each build gave the kernels' shapes. The contact kernel goes from 59 registers a thread to 62. Its local
memory and its 24 groups a multiprocessor do not change. The class step kernel keeps 96 registers, and its local
memory grows by 24, 48 and 96 bytes a thread in the three classes, about 1%. The other thirteen kernels do not change.

The carry survives the driver's own compile. `DriftDiagnostic.SlowBodiesDriftOnTheCard` moved the slow bodies at
x = 150 m by 0.10000, 0.50000 and 2.00000 mm on the card, which is the drift to the last digit shown. The card reached 52 °C, with no driver
event.

## The floor and the glass on a lattice

### Why they round

The bed's height function sums the tilt and twelve relief modes (three bands of four) in single, at values of tens of
metres: `TiltX·dx + TiltZ·dz − Offset + Σ a·cos(kx·dx + kz·dz + φ)`. Each product and sum rounds to 2 to 4 µm, and
the floor `−BedDepth + h` rounds again. The result is a fixed function of the float x and z. It holds still while they
do, and it jumps by microns when either steps, which is every 8 to 15 µm of travel at these coordinates. Round 57's
bed has no friction, so a resting body creeps sideways and keeps meeting those jumps. The rest depth is 0.5 µm. The
glass's radius rounds the same way, at about 8 µm near a tank wall 84 m from the axis.

Correcting the floor to first order along its gradient bought nothing (4.21 against 4.20 for the glass's term alone).
The floor's error is inside the function, and only a fixed reference removes it.

### What was built

The floor under a point is taken from the nearest point of a 0.25 m lattice, which a float holds without rounding. The
height there is rounded once and holds for the whole lattice square. The rest of the floor is a small difference,
evaluated in small arguments (`BedHeightNear`):

- the tilt's part is `Tilt·(x − x0)`;
- each mode's part is `a·[cos χ0·(cos δ − 1) − sin χ0·sin δ]`, with `δ = k·(x − x0)`;
- `cos δ − 1` and `sin δ` come from their series, since δ stays near a tenth of a radian on this lattice;
- the gradient is taken at the true point the same way.

The offset `x − x0` is the float's offset plus the rest, taken from the parts as the contacts already are. So the
floor moves smoothly with the body's true position. It jumps only where a body crosses a lattice line, once in 0.25 m
of travel. The glass takes the radius at the lattice point, its difference from the tank's radius first, and the change
from there to second order (`GlassOutsideNear`).

Only the contact's bed branch uses them. Founding, the fields, the CPU solver and the double kernels keep
`BedHeightAndGradient`. They hold no state: no column, no checkpoint field and no change on the host.

### What was rejected

Keeping the floor in float-float arithmetic would carry the tilt products and the twelve-mode sum as pairs of floats,
with float-float cos and sin. By the arithmetic that costs four to eight times the bed's present work. It would add
much more code to verify, and for a resting body it buys nothing over the lattice. Double on the card is ruled out:
ILGPU's double trig loops on its argument there (`src/Evosim.Farm.Gpu/CLAUDE.md`).

## The reef

The reef does not round the way the bed does. `ReefOf` first subtracts nearby floats (`x − reefX`, `y − reefY`), which
loses nothing, and only then forms distances of a few metres. Those round to 0.2 to 0.5 µm, near the rest depth rather
than several times over it. The local frame's first-order term along the reef's gradient already applies. I have read
the round cap's path and have not read the shaped cap's. A grip forms on rock, and on the reef in single it should meet
rock nearly as steady as in double. That is inference; no measurement separates the reef.
