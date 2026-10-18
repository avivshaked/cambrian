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

**Three vertical shapes,** sin(qπη) for q = 1, 2, 3, as the streams carry three. A mode is a (vector, q) pair, so 20
to 25 vectors give 60 to 75 modes; the probe says whether 80 holds the card's pace.

**Time.** Each mode's phase and amplitude envelope advance at their own irrational rates, as the streams' terms do
(`CurrentField.cs:2178-2186`), computed once per step on the host and read from a table on the card, as the
"instants" are today.

**The spectrum.** Amplitudes fall with wavenumber so that the largest swirls carry most of the speed and the
smallest still stir: energy roughly ∝ |k|^-5/3 over the band (inference from ocean mesoscale spectra; to be
tuned by eye in the theatre). The whole field is scaled to an RMS of 0.1 m/s over the water.

## Round the shoals

A shoal must be an island in the stream function, so that water goes round it. Taper the horizontal pattern
toward a constant where the water is shallow: multiply the potential's horizontal part by a smooth factor
s(h) = smoothstep(h_low, h_high, h), with h_low = the shoal's top depth and h_high = about 6 m. Over a shoal's top the
pattern is flat, so the water there barely moves sideways, and the flow is forced round the shoal's edge, along its
depth contours.

**A speed cap over the shoals.** Where the taper turns, the flow squeezes and speeds up. The cap, about 0.15 m/s
where h < h_high, is built into the potential, by limiting how steeply the taper may turn. It is never applied by
scaling the velocity afterwards, because scaling a curl field breaks its zero divergence and with it the matter
books. Whether a clamp of the taper's slope or a second, local term is simpler on the card is the speed agent's call.

## Checks before it runs a round

1. Divergence at a grid of points, to machine precision.
2. No flow through the bed or the surface at any point.
3. Exact match across each join: velocity at x = 0 and x = L, and likewise z.
4. Speed: RMS 0.1 m/s over the water, and no point over a shoal above the cap.
5. A tracer screen: passive particles released over one oasis, counted over the others after 6,000 s, against the
   tank's streams on round 61's map, so the mixing is compared as well as the shape.
6. The card's double kernel equals the CPU solver, as for every field.

## Open, for the speed agent's reply

- The probe's number, which sets 20 or 25 vectors.
- Whether the cap is cheaper as a clamp of the taper or as a second, local potential term.
