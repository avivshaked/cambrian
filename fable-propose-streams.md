# Proposal: streams, not a gyre

**Ruled by the owner, 2026-09-12 afternoon** ("Yes. It reads right"), on the agent's amended
reading after the owner's own: "What we don't want is a current that creates centrifugal
forces that send all the creatures to the rim. The currents should not act as unified fields
but more like streams." Supersedes the slip-wall proposal of the same afternoon, withdrawn.
Absorbed into DECISIONS.md when the build lands, then deleted.

## What round 37 showed

The glass gathers. The rim ring, a quarter of the area, holds 58 to 96% of the bodies at
every sample of every seed read so far (13,000 to 23,800 s), and the population swings
between about 700 and 1,750 on a cycle near 10,000 s. At the peaks the whole population is
a crust one body thick against the glass in the top three metres (seed 1 at 15,000 s: 1,457
of 1,518 bodies in the rim ring, mean depth −1.4 m, nearest neighbour 0.27 m, water at the
bodies 0.025 m/s where the knob is 0.1). `wraps` 0, `diverged` 0 in all five, the jointed
line alive in numbers in two seeds.

## Why, as inference from the fluid model

The fluid model pulls a body toward the water's velocity by drag and nothing else. Real water
also holds a parcel on a curved streamline by its pressure gradient, which is why plankton stay
with the water. Without that term any coherent rotation about the tank's axis is a centrifuge
for every body in it, whatever its density: the body lags the turning water, its inertia
carries it straight, and it drifts to the rim. D089's gyre has one swirl about the axis by
construction, so it centrifuges; the swirl vanishing at the glass and the overturning cell
upwelling along it then hold the crust at the surface. A faster swirl at the rim (the withdrawn
proposal) would centrifuge harder.

The box's current (D088) is a sum of a handful of eddies at different scales whose phases drift
and never repeat. No eddy keeps a centre for long, so the small outward drift inside each
averages to mixing, and round 36 filled the box evenly. That field is the model.

## The rule

The tank's current is a spectrum of streams and no swirl about the axis:

1. **No azimuthal swirl term.** The `S·s(1 − s²)` part of D089's gyre is removed.
2. **Horizontal eddies from a vertical vector potential**, as now, but a spectrum: azimuthal
   modes `m` = 1 to 4, two radial families per `m` (the current `s^m(1 − s²)` and a second
   with an interior node, `s^m(1 − s²)(1 − 2s²)`), vertical modes `q` = 1 to 3, each term with
   its own phase drifting at an irrational rate and its own amplitude slowly modulated
   between half and full, all seeded by the run seed. Tangential at the glass by construction
   (the radial term carries `(1 − s²)`), finite on the axis.
3. **Vertical overturning as several small cells with random phases**: Stokes stream functions
   `Ψ = Ψ₀·r²(1 − s)²·sin(qπy/D)·g(θ, t)`-free axisymmetric cells for `q` = 1 to 3, with
   the squared wall factor so neither the radial nor the vertical flow reaches the glass, and
   independent drifting phases so no cell stands still or lines up with a wall.
4. **Normalisation as now**: measured RMS at the knob, vertical per-axis RMS matched to the
   horizontal, the Courant bound from the measured maximum.
5. **Two new checks in `GyreTests`** (renamed `StreamsTests`): a passive tracer integrated
   through the field alone with the campaign's drag response time keeps a uniform radial
   distribution over 30,000 s (the rim quarter within 0.2 and 0.3 of the tracers, averaged
   over 200 tracers), and the mean speed within half a metre of the glass reads at least half
   the RMS. The 600 s smoke's rim share is read against a quarter.

## Queued, not in this build

The pressure-gradient term in the fluid model (the force on a body from the water's own
acceleration along its path, the term that keeps a neutral body on a streamline). It would
let any prescribed current carry bodies faithfully and is a change to DESIGN §5.2's fluid
model, so it goes to the owner as its own proposal with the ledger's reading of what it
costs a swimmer.

## The sequence

Round 37 completes and is read against 0093 (M1 fails by the "glass gathers" fork). Round 37b,
the same world on the streams, replaces it as the tank's base; round 38's dilution is read
against 37b. One change per round is kept. The throw trace (branch `throws`) lands in the same
build. Cost: one round.
