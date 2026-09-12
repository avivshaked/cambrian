# Build spec: the transporter keeps uniform water uniform (Astra review F1, 2026-09-12)

Read `CLAUDE.md` in full, then `logbook/specs/streams-spec.md` and
`water-carries-spec.md`, then `src/Evosim.Core/Environment/CurrentField.cs` (the transport
field's construction and `Unit`, lines ~1100–1230 on this branch; the streams' construction
and `StreamsUnit`, ~1390–1480 and ~2106–2200; the instant memo), `GridField.cs` (`Advect`,
`Sweep`, `ApplyEast`/`ApplyDown`/`ApplyFront`, `EastOf`/`FrontOf`, the mask `_live`,
`MaximumSubsteps`), and the tests `GridFieldTests.cs`, `CurrentTransportTests.cs`,
`StreamsTests.cs`, `TankTests.cs`.

Rules: edit only in this worktree (`D:\Projects\experiments\evolution-simulator\scratch\wt-streams`,
branch `streams`); write nothing outside it, nothing under the session scratchpad or TEMP; no
commit; no Unity; no `.md` files; do not touch running workers, `runs/` or the main tree; never
sleep, poll or wait. Do not edit `SharedVolume.cs`, `TankWall.cs`, `TankGeometry.cs` or
`SharedSpaceSmoke.cs` (another builder owns them in a sibling worktree).
`./scripts/core-test.ps1 -Filter <Class>` as you go and the default suite once at the end.
Doc comments say why and cite this spec.

## The fault, reproduced

A uniform dissolved concentration carried by incompressible water in a closed container stays
uniform. The grid's transporter does not keep it so. Seeded at 1 unit/m³ with no organisms,
carried by the campaign's field (0.1 m/s, period 6,000 s, half-second steps, seed 1's
stream), after 600 s:

| container | cell | mixing | min | max | sd / mean |
|---|---:|---:|---:|---:|---:|
| box, transport field | 1 m | 0.02 m²/s | 0.357 | 2.454 | 30% |
| box, transport field | 5 m | 2 m²/s | 0.907 | 1.113 | 5% |
| tank, streams (this branch) | 1 m | 0.02 m²/s | 0.248 | 4.278 | 34% |
| tank, streams (this branch) | 1 m | off | 0.000 | 15.9 | 102% |
| tank, streams (this branch) | 5 m | 2 m²/s | 0.898 | 1.122 | 6% |

The total is conserved to 1e-15 throughout. The probe is `scratch/astra-check/Program.cs`
(read it; it is the shape the new test takes).

The cause is in `Sweep`: the horizontal velocity is sampled at a cell's centre and used for
its east and front faces, the vertical at the lower interface, and the three axis passes are
applied one after another, each on the stock the previous pass left. The face velocities are
therefore not divergence-free on the grid, and even where they were, a sequential sweep
compresses along one axis before the next expands. Conservation and positivity hold; the
constant field does not.

## The repair: face fluxes from the vector potential

Every current the campaign runs is the curl of a vector potential. The transport field is
`A = (Φ_m(y)·f_m, 0, Φ_m(y)·g_m)` summed over modes (the doc on `TransportAt`); the streams
are `A_y = Σ a_k f_j(s) cos(mθ + ψ_k) sin(qπy/D)` (eddies) plus the overturning cells'
`A_θ = Ψ_q / r = Ψ₀ c_q r (1 − s)² sin(qπy/D)` (Stokes stream function; check the sign
convention against `StreamsUnit` numerically, see the tests). The flux of a curl through a
face is the circulation of the potential round the face's edges (Stokes), so:

1. **`CurrentField.PotentialAt(x, y, z, seconds)`** returns `A` in Cartesian components, in
   the same units and with the same scale as `VelocityAt` (the transport scale, the streams'
   two scales, the envelope and phases, `Speed`), for `Transport` in a box and for the streams
   in a tank. The instant memo applies. `Rolls` has no potential and keeps its scheme
   untouched, byte for byte: every recorded world through round 33 replays on it.
2. **The grid samples edges, not cells.** For each grid edge (three families: along x, y, z)
   the value `E = A(edge midpoint)·(unit edge direction)·h`. Each face's flux
   `Q = Σ ±E` over its four edges in right-hand order about the face's outward normal. Because
   every edge appears in two faces of any one cell with opposite sign, every cell's net flux
   is exactly zero for any edge values whatever: a uniform field stays uniform to rounding.
   Store `Q` per cell for the east, lower and front faces (the arrays `_fluxX/_fluxY/_fluxZ`
   are the right shape); in the box the x and z edges wrap. Cost: about three potential
   samples per live cell per substep against two velocity samples today.
3. **The boundaries.** Edge values on the surface plane (y = 0) and on the floor plane
   (y = −D) are set to exactly zero: the potential's horizontal components vanish there
   analytically (`sin(qπy/D)`) and `Math.Sin(−π)` is not zero. In the tank every edge that
   touches a dead cell is set to zero: then every face between a live and a dead cell has
   flux zero (all four of its edges touch the dead cell) and every live cell still
   telescopes to zero. Every remaining edge's midpoint is inside the circle (the mean of four
   live-cell centres, and a disc is convex), so no clamped sample is used. State the
   consequence: the discrete field is tangential to the stair-step, so its wall layer is
   slower than the analytic field's; measure and report the discrete face-velocity RMS
   against the analytic RMS on 1 m and 5 m cells.
4. **Upwind, all three axes from one stock.** Compute the three flux arrays from the same
   `_stock` and only then apply them (calling the three existing `Apply*` in sequence on
   precomputed arrays is the same arithmetic as one combined apply). The transfer across a
   face is `Q·dt/h³` of the upwind cell's stock. Never clamp a face fraction: a clamp is a
   divergence. Positivity needs each cell's outflow fractions to sum to at most one, so
   choose the substep count from the fluxes themselves: compute the fluxes for the full step
   at the step's clock, take the largest per-cell outflow sum over live cells, and substep so
   it stays at or below 0.75 (the first substep reuses those fluxes scaled by 1/n; later
   substeps sample at their own clock as now). `MaximumSubsteps` stays the refusal.
   `MaximumTransportSpeed`'s a-priori Courant check may stay as a guard; say which decides.
5. **Bodies and corpses** keep reading `VelocityAt`; nothing outside the grid changes.

## Tests (new class `ConservativeTransportTests`, plus the existing ones green)

- **Constant field**: seeded at 1 unit/m³ over 1,200 half-second steps, box and tank, 1 m and
  5 m cells, mixing off and at the campaign's rates, seed 1's stream at 0.1 m/s and the box's
  0.3 m/s: max |c − 1| below 1e-9 in every case. Print the table above's columns for the new
  scheme beside the old numbers.
- **The potential is the field**: at 500 random interior points and five instants, the
  central-difference curl of `PotentialAt` (0.02 m) against `VelocityAt`: RMS error below
  1e-3 of the RMS speed, worst below 1%, for the transport field and for the streams at
  `Speed` = 0.37 (a scale other than 1 so the scale is proven carried).
- **Conservation and positivity** retained (`EveryOperatorConservesTheTotal` and the rest),
  the tank's dry cells still dry, a Gaussian patch carried keeps its total and never goes
  negative, and the downstream/upstream and wrap tests still pass (rewrite their setup if
  they depended on the centre-sampled route; keep their assertions).
- **Resolution**: the discrete RMS at 1 m and 5 m against the analytic, reported, not gated.
- **Cost**: nanoseconds per `Advect` step for the tank's 1 m detritus grid and its 5 m matter
  grid at 0.1 m/s, before (git stash or the old route kept in a test-only copy) and after;
  at most 1.5 times the old per substep, and print the substep counts the campaign's cases
  take (tank 0.1 m/s on 1 m and 5 m; box 0.3 m/s on 1 m and 5 m).
- `GridFieldExperiments` sitter/mover (Slow): do not run them; the caller does.
- The default suite green.

## Final message

Tables: files changed; the constant-field table before and after; the curl check's numbers;
the discrete-versus-analytic RMS by cell size; the timings and substep counts; the suite's
count and time; anything unverified or ambiguous and what you chose (the sign convention of
`A_θ`, the substep margin, what happened to the a-priori Courant guard).
