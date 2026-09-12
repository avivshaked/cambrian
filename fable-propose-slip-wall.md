# Proposal: the water slips along the glass

**For the owner, 2026-09-12 afternoon.** One world rule, the shape of the gyre at the wall, and
one sequence change, a repeat of the tank round on the corrected water before the dilution.
Read from round 37 at 13,000 to 23,800 s, mid-run; the round completes as pre-registered and
its entry carries the full read.

## What round 37 shows

The glass gathers. Logbook/0093's M1 predicted the rim ring (`p3`, a quarter of the area) would
hold 15 to 40% of the bodies. It holds 58 to 96% at every sample of every seed so far, and the
population swings between about 700 and 1,750 with a period near 10,000 s where round 36 grew
steadily. At the peaks the whole population is a crust one metre thick against the glass and
in the top three metres: seed 1 at 15,000 s has 1,457 of 1,518 bodies in the rim ring, every
leaf at a mean depth of −1.4 m, a median nearest neighbour of 0.27 m, and a mean water speed
at the bodies of 0.025 m/s where the knob is 0.1. The positions reader's top view is a ring
(`scratch/positions/r37-s1/r37-s1-t0015000-top.png`) and its side view a film. Meanwhile
`wraps` reads 0 everywhere, `diverged` reads 0 in all five seeds at 13,000 to 23,800 s, and
the jointed line is alive in numbers in two seeds (137 and 210 inherited), which are the parts
of the container that worked.

## Why, as inference from the field's own construction

The gyre was built tangential and closed at the glass, and the swirl was built to vanish there
(D089's second clause: `v_θ = S·s(1 − s²)…`, zero at `s = 1`). The overturning's vertical
velocity is `v_y = Ψ₀(2 − 3s)sin(qπy/D)`, which at the glass is `−Ψ₀ sin(qπy/D)`: the wall is
where the water goes up and down fastest. So a body that reaches the glass finds no water
moving it along the wall, a vertical flow that carries it to the surface or the bed, and a
surface restore and a floor that stop it there. The eddies do move water inward at the wall
half the time, but a body at the glass sits a radius inside `s = 1` where their radial term
`(1 − s²)` is already small. The wall is a ratchet: outward motion is absorbed, inward motion
is weak, tangential motion is zero. The dead-pocket read (0.004 of live cells below a tenth of
the RMS) did not see it because the trap is a layer one body thick, not a pocket of cells.

The population swing follows: the crust strips the rim's cells, starves, thins, the survivors
are carried round again, and the crust rebuilds. It is the ecology of a trap, not of a tank.

## The rule proposed

The water slips along the glass. Three changes to the gyre, each a curl or a pure swirl as
before, so it stays divergence-free by construction, and the wall stays impermeable:

1. **The swirl does not vanish at the glass.** `v_θ = S·s·(1 + ½cos(πy/D))`, solid-body
   rotation in radius, its speed largest at the rim. It is still azimuthal only, so still
   divergence-free, and it carries a body along the glass at the field's full speed.
2. **The overturning puts no vertical flow through the wall layer.** `Ψ = Ψ₀·r²(1 − s)²·sin(qπy/D)`,
   so `v_r ∝ r(1 − s)²` and `v_y ∝ (1 − s)(1 − 2s)`, both zero at the glass; the cell rises
   in the middle and sinks in the outer half rather than at the wall.
3. **The eddies are unchanged**: their tangential term is already nonzero at the wall and
   their radial term vanishes there.

Normalisation as now (measured RMS at the knob, vertical matched to horizontal), plus one
new measure in `GyreTests`: the mean speed within half a metre of the glass, which must read
at least half the RMS, and the share of bodies in the rim ring in a 600 s smoke read against
a quarter.

## Alternatives considered

A radial return near the wall (a thin layer of inward flow): not divergence-free without a
matching source, and a field that pushes bodies off a wall is a force the world does not
have. A softer contact (the wall as a spring): a body still stops where the water stops. The
walled square: the same trap on four flat walls, with corners. Doing nothing and reading
round 37 as it is: the trap is the container's artefact, and every reading in the tank is
confounded by it until the water at the glass moves.

## The sequence

Round 37 completes and is read against 0093 (M1 fails by the "glass gathers" fork; the rest
read as pre-registered). Then **round 37b**, the same world on the slipping water, replaces
round 37 as the tank's base, so that round 38's dilution is read against a tank that is not a
trap. One change per round is kept: 37b changes the water at the wall and nothing else; 38
changes the density. The throw trace (branch `throws`) lands in the same build. Cost: one
round, about a day.

## What is asked

1. The rule: the water slips along the glass, as above (a world rule, the owner's).
2. The sequence: 37b before 38 (the agent's under the grant, recorded here for the record).
