# Build spec: streams, not a gyre (fable-propose-streams.md, ruled 2026-09-12)

Read `CLAUDE.md` in full, then `fable-propose-streams.md` in the main tree
(`D:\Projects\experiments\evolution-simulator\fable-propose-streams.md`; it is the ruling and
the reasoning), then `src/Evosim.Core/Environment/CurrentField.cs` (the gyre: `BuildGyre`,
`GyreUnit`, the doc block above them, the normalisation, `GyreComponentRms`), `TankGeometry.cs`,
`src/Evosim.Core.Tests/GyreTests.cs` and `TankTests.cs`, `logbook/specs/tank-spec.md` (the
gyre's original contract, which this replaces), and `FluidModel`/`FluidConfig` in Core for the
drag response time the tracer test needs.

Rules: edit only in this worktree (`D:\Projects\experiments\evolution-simulator\scratch\wt-streams`,
branch `streams`); write nothing outside it, nothing under the session scratchpad or TEMP; no
commit; no Unity; no `.md` files; do not touch running workers, `runs/` or the main tree; never
sleep, poll or wait. Run the Core suite with a filter as you go
(`./scripts/core-test.ps1 -Filter StreamsTests`, `-Filter TankTests`) and the default suite once
at the end (about a minute). Doc comments say why and cite this spec by path.

## The invariant

The Box world is untouched: the periodic transport field, the rolls, every hash-bearing
number. `WorldShape Tank` selects the streams as it selected the gyre. No tunable is added;
the streams are the tank's current, full stop. `RunConfig` does not change, so every config the
tank build reads still reads.

## The field (replaces the gyre; keep the code's shape, replace the construction)

The tank's current, `s = r/R`, `y` down-negative as everywhere, `D` the depth:

1. **No swirl.** The azimuthal `S·s(1 − s²)(1 + ½cos(πy/D))` term is deleted.
2. **The horizontal eddies**, from a vertical vector potential, a spectrum:
   `A_y = Σ a_k · f_k(s) · cos(m_k θ + φ_k(t)) · sin(q_k π y/D)` over every combination of
   `m` = 1..4, radial family `j` = 1..2 and `q` = 1..3 (24 terms), with
   `f_1(s) = s^m (1 − s²)` and `f_2(s) = s^m (1 − s²)(1 − 2s²)`. `v_r = (1/r)∂A_y/∂θ` and
   `v_θ = −∂A_y/∂r`, the `1/r` cancelled analytically against `s^m` as the gyre does; `v_r`
   vanishes at the glass through `(1 − s²)` and is finite on the axis. Raw amplitudes `a_k`
   fall with the mode's wavenumber as `1/sqrt(m² + j² + q²)` so that the large streams carry
   most of the energy, as an ocean spectrum does.
3. **The overturning**, several axisymmetric cells: `Ψ_q = Ψ₀ · c_q · r² (1 − s)² · sin(qπy/D)`
   for `q` = 1..3 with `v_r = −(1/r)∂Ψ/∂y`, `v_y = (1/r)∂Ψ/∂r`, so `v_r ∝ r(1 − s)²` and
   `v_y ∝ (1 − s)(1 − 2s)`, both zero at the glass; each with its own phase; `c_q = 1/q`.
4. **Phases and amplitudes random-walk, seeded.** Every term's phase advances at its own
   irrational rate `ω_k = (2π/Period)·(1 + k·φ)` (`φ` the golden ratio, as the transport field
   does), and each term's amplitude is modulated as `a_k · (0.75 + 0.25·sin(ω'_k t + ψ_k))`
   with `ω'_k` a slower irrational rate, so no term stands still and none ever switches off.
   Initial phases and the modulation phases come from the run seed through the field's own
   `Rng`, so a seed replays.
5. **Normalisation as the gyre's**: measure on the lattice the horizontal and vertical mean
   squares over the live volume and several phases, scale the overturning so the vertical
   per-axis RMS equals the horizontal per-axis RMS, then scale the whole so the total RMS is
   `Speed`; `MaximumTransportSpeed` from the measured maximum times 1.1. Keep
   `GyreComponentRms` (rename to `StreamsComponentRms`: Eddies, Overturning) and the
   divergence and wall tests.

## Tests (`GyreTests` becomes `StreamsTests`; keep every test that still applies)

- Divergence at 1,000 random interior points below 1e-3 of the RMS per metre; radial velocity
  at 200 points on the glass below 1e-6; vertical velocity at the surface and the floor below
  1e-6; RMS at the knob to 1%; per-axis RMS within 20% of each other; seed reproducibility;
  the dead-pocket fraction printed.
- **The wall layer moves**: the mean speed at 500 random points within 0.5 m of the glass, over
  several phases, at least half the RMS.
- **The tracer keeps its spread** (the ruling's own check): 200 passive tracers placed uniformly
  in area over the disc at random depths, integrated for 30,000 s at a 0.5 s step through the
  field with a first-order drag response `dv/dt = (u(x,t) − v)/τ`, `τ` = 2 s (about a body's:
  say in the doc comment how you derived it from `FluidModel`'s drag at the campaign's body
  size, or state 2 s as the assumed response time if the derivation is not clean), reflected
  at the glass and clamped at the surface and the floor. Assert that the share of tracers in
  the rim quarter (`s² > 0.75`) averaged over the last 10,000 s lies between 0.2 and 0.3, and
  print the four ring shares and the mean radial drift. Run the same tracer through the OLD
  gyre construction once, in a test marked `Slow`, and print its rim share, so the record has
  the number the ruling was made on; if keeping the old construction alive for one test is
  more than a hundred lines, drop that test and say so.
- `TankTests` unchanged and green.

## Also in this build (an instrument fix, `unity/Assets/Evosim/Sim/Ecosystem.cs`)

`MeasureHorizontalSpread` in the tank counted occupied columns among all columns of the
bounding square while its denominator counted only the columns whose centres are inside the
circle, so round 37 printed `cols 104/100`. Count an occupied column only if its centre is
inside the circle (the same test as the denominator), and say why in the comment. Nothing
else under `unity/` changes; the caller compiles it.

## Final message

Tables: files changed; the field's terms with their counts; the measured RMS per component,
maximum-to-RMS, dead pockets, the wall-layer speed, the tracer's ring shares (streams, and the
old gyre if kept); the suite's count and time; what is unverified and any place the spec was
ambiguous and what you chose.
