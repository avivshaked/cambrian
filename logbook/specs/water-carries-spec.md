# Build spec: the fluid acceleration force (fable-propose-water-carries.md, 2026-09-12)

Read `CLAUDE.md` in full, then `D:\Projects\experiments\evolution-simulator\fable-propose-water-carries.md`
(the proposal; the rule is put to the owner, the build is agent work and lands behind a
tunable whose default is today), then `logbook/specs/streams-spec.md` and `StreamsTests.cs`
(the tracer check this term is meant to make passable), `src/Evosim.Core/Environment/FluidModel.cs`
and `FluidConfig` (drag, added mass as effective mass, D081), `CurrentField.cs` (`VelocityAt`
and the streams' `StreamsAt`/`StreamsUnit`; the transport field and the rolls),
`unity/Assets/Evosim/Sim/FluidEnvironment.cs` (where drag is applied per part every physics
step; the fluid's per-step force loop) and `EvolutionRun.cs` (tunables, the header line),
DESIGN §5.2.

Rules: edit only in this worktree (`D:\Projects\experiments\evolution-simulator\scratch\wt-streams`,
branch `streams`, on top of the streams commit); write nothing outside it, nothing under the
session scratchpad or TEMP; no commit; no Unity; no `.md` files; do not touch running workers,
`runs/` or the main tree; never sleep, poll or wait. `./scripts/core-test.ps1 -Filter <Class>`
as you go and the default suite once at the end. Doc comments say why and cite this spec.

## The invariant

`FluidConfig.FluidAccelerationCoefficient` (`EVOSIM_FLUID_ACCEL`) defaults to 0, at which no
force is added and every hash-bearing number is unchanged (the Box digest is the caller's
check). The reflection tests (`RunConfigTests`, `RunConfigJsonTests`) must pass with the new
tunable wired; the header prints `fluidAccel <value>` unconditionally beside `addedMass`.

## The term

1. **`CurrentField.AccelerationAt(x, y, z, seconds)`**: the water's acceleration
   `Du/Dt = ∂u/∂t + (u·∇)u` at a point, for every mode (rolls, transport, streams). The field is
   analytic, so take the derivatives by central finite differences on the field itself: time
   by `±0.01 s` about `seconds`, space by `±0.05 m` on each axis, with the same clamps at the
   surface, the floor and the glass as `VelocityAt`. Seven extra field samples per call; memo
   the streams' per-instant phases as the build already does. Write a test that the numerical
   `Du/Dt` of the transport field agrees with a finer difference to 1%, and that a uniform
   steady field gives zero.
2. **The force on a part**: `F = coefficient · (ρ_water · V_part + m_added) · Du/Dt`, where
   `m_added` is the added mass the part already carries (`FluidModel.EffectiveMass` less the
   plain mass), `V_part` its volume, `Du/Dt` sampled at the part's position at the current
   time. Applied in `FluidEnvironment`'s per-step loop beside the drag, on every part, in
   every current mode when the coefficient is above 0. The vertical component goes through
   D050's rule exactly as the current's own vertical velocity does (no upward net force at or
   above the waterline); say in the comment which existing guard you route it through.
3. **The tracer check** (`StreamsTests`): the tracer integrator gains the same term,
   `dv/dt = (u − v)/τ + c·Du/Dt` with `c` = 1 for a neutral body (its displaced mass equals
   its mass; state that assumption), and the assertion the streams spec asked for is now made:
   the rim share for `τ` = 0.5 s and 2 s between 0.2 and 0.3 over the last window; print the
   shares at `c` = 0 and `c` = 1 side by side so the record has both.
4. **Cost accounting**: the force does no work on a body at rest relative to the water, so
   the energy books are untouched; confirm no audit term reads it, and say so.

## Tests

`FluidAccelerationTests` (new): the derivative checks above; a part in a solid-body rotating
test field (a small test-only field, or the transport field at one instant) with the term at
1 follows the water's circle to within 5% of the radius over ten turns where at 0 it spirals
out; the term at 0 adds exactly zero force. `StreamsTests` as above. `RunConfig*Tests` green.
Default suite green.

## Final message

Tables: files changed; the tunable and its header token; the derivative test's numbers; the
tracer shares at `c` = 0 and 1 for both `τ`; the suite's count and time; what is unverified
(everything under `unity/` is uncompiled) and any place the spec was ambiguous and what you
chose.
