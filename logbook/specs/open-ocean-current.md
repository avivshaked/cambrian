# The open ocean's current: a draft spec (D204)

*2026-10-18, the coordinator. A draft for the speed agent, who builds it on the CPU and the card. The count of modes
waits for the card probe of 24 against 80 modes. Everything here follows D090's lessons: no source and no sink, no
coherent swirl that holds a centre, and phases that never repeat.*

## What it must do

- Fill a square of side L = 300 m, joined at both pairs of edges, and match itself exactly across each join.
- Mix at several sizes, from about 30 m (half an island's width) to 150 m (half the square), so that oases 90 m
  apart trade bodies and matter now and then and never through one steady channel.
- Follow the floor: run along depth contours, flow round a shoal and not over it, and never pass through the bed.
- Keep round 61's speed, an RMS of 0.1 m/s over the water, with a cap over the shoals.
- Cost on the card no more than a few times today's streams per sample.

## The construction

**A vector potential in the mapped coordinate, as `StreamsSlopedUnit` builds today's floor-following field.** Let
h(x, z) be the water's depth at a column and η = y / h(x, z) its mapped height, 0 at the bed and 1 at the surface.
The velocity is the curl of a potential whose terms are a horizontal pattern times a vertical shape in η. Built
that way, the field is divergence-free and has no flow through the bed or the surface, by construction, whatever
the floor does.

**The horizontal patterns are Fourier modes on the torus.** Each mode is a wave vector k = (2π/L)(m, n) with
integers m and n, so it joins exactly at both edges. Take a chosen set of about 20 to 25 vectors with
2 ≤ |(m, n)| ≤ 10, spread over directions so that no direction dominates: wavelengths from L/2 = 150 m down to
L/10 = 30 m. Exclude (m, n) = (0, 0) and keep one of each ± pair. Each vector carries a cosine and a sine term.
At the lowest wavenumbers, where most of the energy sits, use off-axis vectors only, (2, 1), (1, 2), (2, −1),
(1, −2), (2, 2) and so on: a vector with m or n zero is a straight jet that wraps the square in one length, a closed
conveyor until its phase drifts. On the card each vector takes one sincos of its phase, on positions already folded into the square: the speed
agent's probe found that cheaper on this card than building phasors by integer powers (logbook/specs/open-ocean-mode-probe/).

**Three vertical shapes,** sin((q − ½)πη) for q = 1, 2, 3: zero at the bed and ±1 at the surface, so the water is
still against the floor and moves most at the top, as a real ocean's does, and drifting plants and matter travel
between oases. The grid needs the potential to vanish only at the bed, so every check holds. The tank's sin(qπη)
kept the surface still, which the coordinator set aside on 2026-10-19 on the speed agent's finding. A mode is a (vector, q) pair: 25 vectors
give 75 modes. The probe measured 75 modes at about 1.4 times today's streams per point, about 0.15 ns, noise beside a
step.

**Time.** Each mode's phase and amplitude envelope advance at their own irrational rates, as the streams' terms do
(`CurrentField.cs:2178-2186`), computed once per step on the host and read from a table on the card, as the
"instants" are today.

**The spectrum.** Amplitudes fall with wavenumber so that the largest swirls carry most of the speed and the
smallest still stir: energy roughly ∝ |k|^-5/3 over the band (inference from ocean mesoscale spectra; to be
tuned by eye in the theatre). The whole field is scaled to an RMS of 0.1 m/s over the water.

## Round the shoals

A shoal must be an island in the stream function, so that water goes round it. The taper already exists as the
shore fade of the beach spec, section 3 (`CurrentField.ShoreFade`, `FadedPotential`: A' = f(d)·m(d/D)·A, the depth
factor m cancelling the stretch of the mapped coordinate), and it is reused as it stands. Over a shoal's top the
potential is flat, so the water there barely moves sideways and goes round the shoal's edge, along its depth
contours.

**The speed cap is the fade's width.** Where the fade turns, the flow squeezes and speeds up, at about |A| divided
by the fade's width. The cap is stated against the open water's own figure: the fastest flow over a shoal's shelf
no faster than the open water's fastest (or the same at the 99th percentile). The fade's width meets it, a property
of the potential, so the field stays a curl and the matter books stay closed; no clamp is applied to the velocity.
In round 45's tank a 15 m fade reached 4.5 times the RMS on the shelf and a 40 m fade 0.95, against about 4 in open
water, so the width is set by a sweep when the shoals land. Until then, a bed that never comes shallower than the
shore plus the fade leaves the fade the identity, and the first build ships with it off, bit for bit.

## Checks before it runs a round

1. Divergence at a grid of points, to machine precision.
2. No flow through the bed or the surface at any point.
3. A match across each join, at a tolerance: positions are folded into [0, L) before evaluation, and in float the
   phase at x = L is not bit-equal to the phase at 0.
4. Speed: RMS 0.1 m/s over the water, and over each shoal's shelf no faster than the open water's fastest.
5. A tracer screen: passive particles released over one oasis, counted over the others after 6,000 s, against the
   tank's streams on round 61's map, so the mixing is compared as well as the shape.
6. The card's double kernel equals the CPU solver, as for every field.
7. The mean velocity over the square is zero, so nothing flows round the torus as a whole.

## Open

- The fade's width, swept when the shoals land.