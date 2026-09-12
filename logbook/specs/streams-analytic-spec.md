# Build spec: the streams' analytic acceleration (D090's cost note, 2026-09-12)

Read `CLAUDE.md`, then `logbook/specs/streams-spec.md` and `water-carries-spec.md`, then
`src/Evosim.Core/Environment/CurrentField.cs` (`StreamsUnit`, `StreamsAt`, `AccelerationAt`,
`MaterialDerivative`, the instant memo) and `src/Evosim.Core.Tests/FluidAccelerationTests.cs`
and `StreamsTests.cs`.

Rules: edit only in this worktree (`D:\Projects\experiments\evolution-simulator\scratch\wt-streams`,
branch `streams`); write nothing outside it, nothing under the session scratchpad or TEMP; no
commit; no Unity; no `.md` files; do not touch running workers, `runs/` or the main tree; never
sleep, poll or wait. `./scripts/core-test.ps1 -Filter <Class>` as you go and the default suite
once at the end.

## Why

`AccelerationAt` samples the field nine times per part per physics step (a central stencil in
space and time), about eight times a velocity sample, and the build's own estimate is that it
halves the farm's pace in a tank at `fluidAccel 1`. The streams are analytic: every term is a
product of a radial polynomial, a cosine in `θ`, a sine in `y` and a phase and envelope in
`t`, so `∂u/∂t` and the Jacobian `∇u` have closed forms and `(u·∇)u` is one matrix-vector
product. The transport field (the box) and the rolls keep the finite difference: no box round
runs with the term on, and their cost is not the campaign's.

## The change

1. In `StreamsUnit` (or a sibling `StreamsUnitWithGradient`), return alongside the velocity
   its time derivative and its 3×3 spatial Jacobian in Cartesian components, from the same
   term loop: differentiate each term's radial factor, azimuthal factor and vertical factor
   analytically, convert the cylindrical derivatives to Cartesian once per call, and apply the
   same clamps at the surface, the floor and the glass that `StreamsAt` applies (outside the
   water the derivative is whatever the clamped sample's is; state the choice). The envelope's
   time derivative is part of `∂u/∂t`.
2. `AccelerationAt` uses the analytic route when the shape is the tank, and the finite
   difference otherwise. The normalisation scale and the memo apply to both.
3. Cost: one call must cost no more than three velocity samples' worth; print the measured
   nanoseconds per call for `VelocityAt` and `AccelerationAt` in the test output, as the
   previous build did (271 ns and 2,245 ns).

## Tests (`FluidAccelerationTests`)

- The analytic `Du/Dt` against the finite-difference stencil at 400 random interior points
  and several instants: RMS disagreement below 0.5% of the RMS acceleration, worst pointwise
  below 2% (the stencil's own error against a finer stencil was 0.37% RMS and 1.0% worst, so
  the two should agree to that order; if they disagree by more, the analytic form has a bug,
  not the stencil).
- Divergence of the analytic Jacobian (its trace) below 1e-4 of the RMS speed per metre at
  the same points, which checks the Jacobian independently.
- `StreamsTests.ABodyRidesTheWater` (Slow) still passes at `c` = 1 with the analytic route.
- The default suite green.

## Final message

Tables: files changed; the agreement numbers; the timing before and after; the tracer band;
the suite's count and time; anything unverified or ambiguous and what you chose.
