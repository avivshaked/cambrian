# Proposal: the water carries as water does

**For the owner, 2026-09-12 evening.** One change to the fluid model (DESIGN §5.2), to go with
the streams the owner ruled this afternoon (`fable-propose-streams.md`). Round 37b runs on both.

## What the streams build found

The streams field was built as ruled: 27 terms, no swirl about the axis, tangential at the
glass, the wall layer moving at 0.96 of the RMS, divergence-free to 2e-4 (`StreamsTests`,
branch `streams`). Its tracer check then said what the field cannot fix:

| what is carried | rim share (a quarter is uniform) |
|---|---|
| a perfect tracer, in the streams | 0.251 |
| a body lagging the water by 0.5 s, in the streams | 0.914 |
| a body lagging by 2 s, in the streams | 0.830 |
| a body lagging by 2 s, in D089's gyre | 0.977 |

The field keeps a parcel of water spread evenly, as an incompressible flow must. What gathers
is the lag. A body in our fluid model is pulled toward the water's velocity by drag and by
nothing else, so on any curved streamline it fails to turn as sharply as the water and drifts
outward by about `τ·u_θ²/r` per second, `τ` its response time. Averaged over the tank that
drift is outward for any current with any azimuthal motion at all (the build's derivation, in
`StreamsTests`' doc comment, confirmed by the four controls above). Our bodies' `τ` from §5.2's
drag at the campaign's size is 0.4 to 1.3 s, which at the round's water speed is a drift of
centimetres per second: the whole population at the rim within an hour of simulated time,
which is what round 37 shows. The owner's word for it was right: a centrifuge.

## Why real water does not do this

A parcel of water on a curve is held on it by the pressure gradient across the flow. A neutral
body in that water feels the same gradient, as a force equal to the displaced mass times the
water's acceleration along its path, plus the added mass's share. That term is the second term
of the Morison equation and the first of the Maxey–Riley equation, and with it a neutrally
buoyant body follows the water to first order: it is a tracer. Our model has the drag and the
added mass (D081) and not this term, which did not matter in a box with no walls and a
returning current, and matters in a tank.

## The rule proposed

The fluid model gains the water's acceleration force on every part:

`F = (ρ_water · V_part + m_added) · Du/Dt`, with `Du/Dt = ∂u/∂t + (u·∇)u` the water's
acceleration at the part's position, from the current field itself (the field is analytic in
time and place, so the derivative is exact or by a tight finite difference on the field, never
on the grid). Applied every physics step beside the drag, in any current mode.

- A tunable coefficient on it: `FluidConfig.FluidAccelerationCoefficient`
  (`EVOSIM_FLUID_ACCEL`), default 0, so every recorded world replays under its own config;
  1 is the physical value. The header prints `fluidAccel 1`.
- The tracer check in `StreamsTests` gains the term, and the assertion the spec asked for (rim
  share between 0.2 and 0.3 for a lagging body) becomes reachable and is asserted.
- The ledger's reading of what it costs a swimmer: nothing directly, since the force does no
  work on a body at rest in the water and the drag already prices motion relative to the
  water; the smoke reads `work J/s` before and after.
- D050's rule at the surface and the floor are unchanged: the vertical component of the term
  goes through the same clamp the current's own vertical velocity does.

## The sequence

Round 37 completes and is read (M1 fails by the "glass gathers" fork, with the mechanism named).
Round 37b is the same world on the streams with the term at 1, one build with the throw trace,
and replaces round 37 as the tank's base; round 38's dilution reads against it. The owner's
"mimic the oceans" is then two things in one rule: water that moves like streams, and bodies
that ride it like water. Cost: the same one round already counted.

## What is asked

The rule: the fluid acceleration force at coefficient 1 in the campaign (a change to the fluid
model, the owner's). The default 0 keeps the record; the tunable keeps the box's rounds
comparable if the owner ever wants the box rerun with it.
