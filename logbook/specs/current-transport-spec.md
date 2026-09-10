# Build spec: a current that carries bodies (owner's ruling, 2026-09-10)

Repo d:\Projects\experiments\evolution-simulator. Read `CLAUDE.md` (tunables and the two reflection
tests; the header convention; the grid gotcha on `D·dt/cell²` and the Courant half; "watch a round
in the theatre"), `logbook/0083` (the ribbons), `DECISIONS.md` D059 (the standing-wave current),
D066 (rolls per patch), D067 (the vent), D086 (the grid's advection), then `CurrentField.cs`
(`VelocityAt`, `RollOrSteady`, the vent terms), `GridField.Advect` (~818: velocities are cached
per layer and patch, `_velocityX[iy*k+patch]`, `_velocityZ`, and the vertical at layer
interfaces), `FluidEnvironment.cs` (~278: a body's drag is against `body.linearVelocity − water`,
water from `Current.VelocityAt(heightY, seconds, patch, patchCount)`), and `Ecosystem.cs` where
the creature's patch and position are known at the gather phase.

Rules: nothing written outside the repo; no commit; never poll, sleep or wait; never run Unity
against `unity/` (the owner's Editor); one short batch smoke on `unity-w7` at the end, launched
and left; filtered Core tests only; doc-comment register: why, with the incident; no em-dashes.

## The owner's world

"creatures have full degrees of freedom and can be anywhere within the confines of the world.
currents move them up and down, left and right, in all directions really. diffusion happens in
all directions." Diffusion already does (D086). Dispersal at birth is built separately
(`OffspringDispersalMetres`, `EVOSIM_OFFSPRING_DISPERSAL`, already in `launch-r35.ps1` as `$Dispersal`; landed 2026-09-10 08:07 and smoked on worker 7 as `r35smoke`). This spec is the current.

A second agent is editing `PhenotypeBuilder.cs` (`Destroy`), `SeaFloor.cs`, `Ecosystem.DestroyAll` and `SharedVolume.TryReserveOffspring`'s reservation radius, and `Assets/Theatre/`, at the same time as you. Do not touch those regions; if a compile in your smoke fails in one of them, report it and leave it.

## What to build

### 1. `CurrentField` gains a transport mode

- `RunConfig.CurrentMode` (enum, `[Tunable("world")]`, serialised by name): `Rolls` (the existing
  D059/D066 field, the default so every recorded config replays) and `Transport`.
- `Transport` is a three-dimensional, divergence-free, time-varying velocity field over the box,
  deterministic from the run seed, at RMS speed `CurrentSpeedMetresPerSecond` (the existing
  knob). Build it as the curl of a vector potential made of a handful of Fourier modes (three to
  six), wavelengths of the order of the box's length, width and depth, each mode with a slowly
  drifting phase at the period `CurrentPeriodSeconds` (existing knob) and incommensurate
  multiples of it, so the pattern never repeats and a particle is not returned. Periodic in x
  (length) and z (width); the vertical component must vanish at the surface (y = 0) and the floor
  (y = −depth): use `sin(π y / depth)` factors on the terms that produce vertical velocity, and
  keep the field divergence-free after that factor (derive it from the potential, do not patch
  components by hand). Scale the amplitude so that the RMS speed over the box equals the knob.
  Document the construction in the remark with the equations.
- `VelocityAt(x, y, z, seconds)` is the new primary sampler; the existing `(heightY, seconds,
  patch, patchCount)` overload keeps working for the `Rolls` mode and, in `Transport` mode,
  samples at the patch's centre x and z (say so in its remark; the harness call sites move to
  the positional sampler below).
- The vent (D067) stays as is in either mode.

### 2. Bodies ride it

- `FluidEnvironment` samples the water at each body's root position (x, y, z) in `Transport`
  mode, so drag pulls the body with the local water in three dimensions. The old overload
  stays for `Rolls`.
- D050's rule stops upward net force at the surface: check that a body carried upward by the
  water still obeys it, and that the floor's contact keeps a body from being pushed through
  the bed. Say what you checked.

### 3. The grid advects with the local velocity

- `GridField.Advect` in `Transport` mode samples the velocity per cell centre (x, y, z), not per
  layer and patch, for the detritus grid and the matter grid alike; upwind transfer between face
  neighbours on all three axes, wrap in x and z, no flux through the surface and the floor, as
  now. The Courant condition is checked against the field's maximum speed, not the RMS, and
  refused above one half as now. Conservation to the last joule on every operator, as now; the
  existing conservation tests must pass, and add one for `Transport`.
- Cost: state it. A per-cell velocity sample for 6,000 detritus cells at every advection step is
  fine; if the sampler is expensive, cache the field on the grid once per metabolic step and
  say so.

### 4. Record

- Header token `current <speed> m/s <mode> ...` replacing or extending the existing token so
  the mode reads from the header; `EVOSIM_CURRENT_MODE` env var.
- `stats.jsonl`: nothing new beyond the position instrument the dispersal build adds.

### 5. Tests

- The field is divergence-free (finite-difference divergence at random points below a
  tolerance), periodic in x and z, zero vertical velocity at the surface and floor, RMS speed at
  the knob within 5%, and a particle integrated for one period is displaced (not returned) by
  more than a cell.
- `RunConfigTests` and `RunConfigJsonTests` pass with the new enum.
- A grid conservation test in `Transport` mode over a few thousand steps.

### 6. Smoke

`rounds/launch-r35.ps1` (the dispersal launcher) gains `[string]$CurrentMode = 'Transport'`
wired to `EVOSIM_CURRENT_MODE`. Refresh worker 7 (`./scripts/new-worker.ps1 -Workers @(7)` from
PowerShell) and launch `./rounds/launch-r35.ps1 -Seed 3 -Worker 7 -Seconds 600 -Name r35tsmoke
-Dt 0.02`; return once the manifest lines print. Report the diff hunks of `CurrentField.cs` and
`GridField.Advect`, the diff stat, the tests and their verdicts, the header token, the launch
lines, and anything that did not fit.
