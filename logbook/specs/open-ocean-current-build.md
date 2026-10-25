# The open ocean's current: what was built (D204)

*2026-10-19, the speed agent, on r62-base. The CPU half of `logbook/specs/open-ocean-current.md`, which is the
specification. This file says how the code meets it, what it chose where the spec left a choice, and what is not
built yet.*

## The shape of it

`CurrentMode.Ocean` is a third value of the current's mode, read only in a box. A tank's water is the streams
whatever the mode says, and the world refuses the ocean in a tank with the water moving, as it refuses the rolls.

The field is written once, over a flat bed, in `CurrentField.Ocean.cs`. Everything a floor does to it is the code
the tank's streams already use: the floor-following map, its Piola transform, the shore's fade and the analytic
acceleration's chain rule. These call four dispatchers (`FlatUnit`, `FlatPotentialUnit` and their two gradients),
which return the ocean's flat field in a box and the streams' in a tank. So a property proved of the map holds for
both waters. The tank's arithmetic is unchanged to the bit, since in a tank each dispatcher makes the call the tank
made before.

The water follows a floor only under the ocean. `SetBox` keeps the bed it is handed, and `ChooseBed` decides whether
the water reads it: a tank's always, a box's under the ocean alone. `Mode` is now a property with a backing field
that calls `ChooseBed` when it changes, so a mode set after the box cannot leave the water reading the wrong floor.
A box handed a floor with relief refuses the rolls and the transport field while the water moves. It also refuses a
floor that does not span it, and a tank's floor.

## The field

The potential is `A = (0, A_y, 0)` in the flat coordinates, with

`A_y = Σ_v Σ_q W_vq(t)·S_q(η)·cos(k_v·x + φ_vq(t))`

over 25 wave vectors and three layers, 75 modes. `η` is the mapped height, 0 at the bed and 1 at the surface. The
layers are `S_q = sin((q − ½)πη)`, the coordinator's choice of 2026-10-19 over the spec's first `sin(qπη)`: still at
the bed, moving most at the surface. Its curl is horizontal swirls layered in depth, with the vertical motion a sloped floor
asks for and none over a flat one.

The grid's books need every component of the pulled-back potential to be zero at the bed, and every layer is zero
there. They do not need it at the surface, since a horizontal face reads only `A'_x = A_x + a·A_y` and
`A'_z = A_z + b·A_y`, and the map's `a` and `b` vanish at `y = 0`. `A_y` is left free at the waterline, and so the
surface moves.

A sample costs one sine and one cosine per vector. The instant table holds every mode's `W·cos φ` and `W·sin φ`, and a
sample sums each vector's three layers against them before it reads its phase `χ`. That is the card probe's finding,
kept on the CPU so the card can copy it term for term.

Every entry folds `x` and `z` into the box before it reads anything, the floor included, so the two sides of a seam
read the same water to rounding. `ColumnAt` folds too, so the grid's precomputed columns read the floor the direct
path reads.

## Choices the spec left open

- **The 25 vectors**, by a rule rather than by hand (`scripts/reads/open-ocean-vectors.py`). The radii are geometric
  from 2 to 10 in 25 steps and the directions step by the golden angle over the half-plane. Each target takes the
  nearest unused integer vector `(m, n)`, one of each ± pair, and no vector on an axis is eligible below
  `|(m, n)| = 5`. The spec asked for off-axis vectors only "at the lowest wavenumbers". A first draw with the line at
  3 took `(3, 0)`, a jet 100 m across, so the line moved to 5. One vector lies on an axis, `(0, 6)`, 50 m across, and
  the widest gap between directions is 18°. `OceanCurrentTests` holds the table to the rule.
- **The spectrum.** The radii are geometric, so each vector stands for a band of wavenumbers as wide as its own
  `|k|`. Energy `∝ |k|^(−5/3)` then gives each mode an RMS speed `∝ |k|^(−1/3)` and a potential `∝ |k|^(−4/3)`. The
  layers are weighted `1/q`. That weighting is my choice, to be read in the theatre.
- **The rates.** These follow the streams' rule, `±(1 + j·φ)` in units of `2π/Period` with alternating signs, over
  the 75 modes in table order. So `j` rises with `|k|` and the largest swirls change slowest. At round 61's 6,000 s
  period the slowest mode turns in 6,000 s and the fastest in about 130 s. The envelopes breathe as the streams' do.
- **The scale is exact in time.** Every phase drifts at its own incommensurate rate, so over time the cross terms
  between modes average to zero. What is left at a point is each mode's own mean square, `⟨E²⟩·½·W²·S_q²·(f²|U_v|² +
  |∇f × p|²)`. Here `U_v` is the mode's curl carried through the map, `p = (a, c, b)` and `f` is the shore's fade. That
  reduces the whole water to a walk over columns and heights with no clock in it. The walk takes 2.5 m columns and 24
  heights per column, weighted by each column's depth.
- **The bound** for the grid's Courant refusal is a true ceiling across modes and heights: every envelope at 1, every
  layer at its largest, and `|U_v| ≤ |k_v|·√(c² + a² + b²)`. It is the largest over the walk's columns with a tenth
  added for the water between them, as the streams' is.
- **The acceleration is analytic.** Round 61's worlds ran with the fluid acceleration at 1, so it is asked at every
  link on every step. The box's old route, a nine-sample stencil, would cost nine of these sums a call. The flat
  gradient is one pass over the vectors, and the map's chain rule is the streams'.
- **The pin** fills the ocean's own four instant slots for both groupings of the clock. A pinned field is then a pure
  read, as the tank's is, for the grid's edge pass and the solver's threaded water pass.

## Outside the field

- Four readers took the water by place under the transport field and now do so under any mode but the rolls:
  `GridField.Advect`, its centre-sampled twin, the Dynamics water pass (`Water.cs`) and a drifting corpse.
- A launcher names the mode with `EVOSIM_CURRENT_MODE=ocean`. The farm's header prints "ocean", and the current's own
  string reads "m/s RMS ocean".
- **Not built here.** The card refuses a box today (`GpuWorld.Refuse`), and its copy of the ocean comes after this
  half is read. The Unity farm and `FluidEnvironment` sit under `Assets/Evosim`, whose digest is `simHash`. They were
  left unedited so that no recording refuses to replay. Under the ocean they would sample the water at a patch's
  centre. The theatre's live mode steps `Evosim.Dynamics` and reads the ocean through `Water.cs`.

## The checks

`OceanCurrentTests` reads the spec's checks 1 to 4 and 7 on a flat bed and on a square floor. The square is 120 m a
side and 25 m deep, and the knob is 0.1 m/s over a 6,000 s period. The open square's floor takes tens of seconds to fit over its
half-metre columns, so the class reads a smaller square, where the properties are the same. One test, traited slow,
reads the RMS and the bound at the open square's own 300 m. The numbers below are from 2026-10-19, on the 120 m
square unless they say otherwise.

| Check | Flat bed | Square floor |
|---|---|---|
| 1. Worst divergence over 1,000 points, a 0.05 m stencil | 2.8e-6 /s | 2.8e-6 /s |
| 2. Fastest water at the floor | 0 | 3.7e-8 m/s |
| 2. Vertical velocity at and above the surface | exactly 0 | exactly 0 |
| 3. Worst velocity across a seam | 5.7e-7 m/s | 5.6e-7 m/s |
| 4. RMS over the water, sampled, against the knob (120 m) | 99.6% | 100.7% |
| 4. The same on the open square (300 m) | 100.1% | 101.2% |
| 7. Largest net flux through a section, against its gross | 5.6e-9 | 6.0e-9 |
| The potential's curl against the velocity, RMS error | 0.012% | 0.012% |
| The analytic acceleration against a stencil four times finer | 0.03% | 0.04% |

A shallow floor with the shore's fade on is 8 m deep, with the fade from 1 m to 7 m. It reads the acceleration at
0.04%, with 89 of its 400 points over columns in the fade's band. On the grid, a uniform concentration on 2 m cells in
a 60 m square stays at 1.000000 for 300 s on both beds. Its total does not drift, and every cell's net flux is zero. The surface's own RMS is 0.141 m/s against the 0.1 over the water: the layers move most at the top, as chosen.

The bound runs 5 to 7 times the fastest sample (2.6 m/s at 0.1 m/s on the open square's floor). It is a true
ceiling. Over time, 75 modes on incommensurate rates come arbitrarily close to lining up, so a lattice maximum would
not be one. Under the grid's conservative route the bound only decides the refusal. At 2.6 m/s on 1 m cells and a
0.5 s step it is a Courant number of 1.3, against the 4 that eight substeps allow. That leaves room up to 0.3 m/s on
1 m cells.

The tank is unchanged: Core's fast suite (1,239 tests), the Farm's (353, without `GpuMeanTests`, which cannot run
pinned to four processors) and the Dynamics' (228) all pass.

Check 5, the tracer screen, is `src/Evosim.OceanTracers`. It builds a world's floor, current and islands
from a launcher's settings, as `World` does. It releases 200 tracers over each island of 10 or more columns, at 0,
20,000 and 40,000 s. It carries them through the water on a 2 s midpoint step. At each reading it counts where the
releases stand: over their own island, over another, or over the desert between. On 2026-10-19 it read round 61's
tank, on the map seed it was founded on, against the open square. Both run 0.1 m/s RMS over a 6,000 s period.

| World | Releases | Over another island at 1,500 / 3,000 / 12,000 s | RMS travel at the same seconds |
|---|---|---|---|
| Round 61's tank, landscape 1 | 18 | 2.1 / 1.9 / 2.0% (the last at 6,000 s) | 56 / 70 / 73 m (the last at 6,000 s) |
| The square, landscape 1, cover 0.05 | 12 | 0.1 / 0.0 / 0.4% | 36 / 51 / 75 m |
| The square, landscape 1, cover 0.067 | 18 | 0.1 / 0.4 / 0.7% | 35 / 49 / 73 m |
| The square, landscape 11, cover 0.067 | 18 | 1.3 / 1.6 / 1.6% | 46 / 56 / 79 m |

Little water passes between oases in either world. Most of a release ends over the desert, and about a tenth to a
sixth stays over its own island. Travel grows more slowly than diffusion would: four times the time gives about 1.4
times the distance, where diffusion would give twice. The modes stir the water in place more than they carry it
anywhere. The coordinator ruled on 2026-10-19 that this little exchange is acceptable for now, and that the ocean's
rates stay as they are.

Cover 0.067 is the coordinator's ruling of the same day. The cover applies inside the 45 m margin, and 0.067 gives
the square round 61's island area of about 118 columns. Of map seeds 0 to 39 at that cover, three give five or six
islands of 10 or more columns (the tool's `--maps`): 1, 3 and 11. The square is founded on 11, for two reasons. Its
six islands are the most even: the largest holds 0.21 of the island columns, against 0.36 on seed 1. And it passes
about four fifths of the tank's exchange, where seed 1 passes a third. Part of that difference is the current
itself, since the seed lays out the current along with the islands. Part is 11's five specks under 10 columns,
which count as other islands here and which seed 1 does not have.

The share of the living over the islands is not a question tracers can answer. It comes from the square's first
founding on the CPU. Check 6, the card against the CPU, comes with the card's copy.
