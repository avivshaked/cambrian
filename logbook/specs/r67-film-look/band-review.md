# Stills round 4, the band set: the coordinator's read (2026-11-09, 23:20)

Read for the coordinator's ruling. This note stays out of the cut folder, so the film reviewer never reads it. The source
is `scratch/film-light/c8/pass13/stills-4/` (33 stills, head e49e12eb, cut by c8). Each still was read at its full 960x540
against CLAUDE.md's render-check list. The numbers come from `measure.py`, as film.py measures them at 960, and are in
`measures.txt`. The reference frames are four frames of rounds 50-57 v3 (`scratch/coord/edgecheck/ref-*.png`): luma 55 to 68,
tone 47 to 63, edges 18 to 37 (the clip's tenth percentile is 16.1), G/B 0.88 to 0.96. Each still is one middle frame, so
motion is not read here.

## Common to every variant: a known cause

- **Focus.** The band set runs without FOCUS_RULE, so every still has the f/1.4 lens with autofocus at the centre.
  - 1.2a: the near leaves at the top corners are soft.
  - 1.6a: the lower half's pads blur out.
  - 2.6a: the near leaf at the lower left is soft.
  - 2.5a and 4.1a: the coral is soft.
  - 6.3a: the subject itself is soft, since the centre falls between two bodies.

  No shot here has a hero at 2/5 of the width, so under D341 each would fail on its blur. The cause is known, and the focus
  set (F) answers it. The edges under the floor (2.5a 7, 4.1a 12.7, 6.3a 12.0, 2.2a 14.9) are partly the lens and partly empty
  water. Which part is which is read on F's stills, whose lens follows the rule.
- **The founders' portraits are small.** The subject holds 0.27 of the width in 2.5a, 0.16 in 4.1a and 0.16 in 6.3a, under
  B3's 2/5 floor. The hold aims at 0.25 and dips to 0.17 and 0.15 mid-take (c8's trace). F's HERO_SCREEN 0.5 is the fix.
- **The lower halves of 2.2a, 2.5a, 4.1a and 6.3a are empty dark water.** The camera looks up at subjects near the surface.
  This is composition, which no band variant changes.
- **1.2a cuts a pink body and two brown leaves at the top edge.**

## The variants

- **R (HDR_TARGET): passes.** At normal contrast it changes nothing the eye sees: at most 14 levels in 1.2a and 6 in 2.2a,
  with tone and edges equal. With 2.2a's lower-right water stretched fivefold (`2.2a-A-over-R-lowerright-stretch.png`), A
  shows the staircase of 8-bit contour steps, the grid that the grade stretches. R's gradients are smooth. It does what it is
  for, and nothing else. c8 measured the share of flat 6 px plateaus in the lower-right water: it falls from 0.23 to 0.10 on
  2.2a, and from 0.25 to 0.05 on 1.2a.
- **L (gain 1.8 with METER_TOP): fails.**
  - The band brightens a little.
  - The bottom fifth darkens by 4 to 10 levels in every shot.
  - Tone and edges fall in five of six shots.
  - 2.6a shows a faint seam at about half height.
- **H (gain 3 with METER_TOP): fails.**
  - The band reads as a lighter rippled ceiling.
  - The bottom fifth darkens by 6 to 17 levels.
  - 2.6a has a hard horizontal seam at about 0.49 of the height, lit cyan above and dark below.
- **I (in-scatter with METER_TOP): fails.**
  - It smooths the surface's ripples into a flat haze, so the band reads as fog, not a surface. This shows in 2.6a, 2.5a and
    4.1a.
  - Edges fall the most: 2.6a 9.6 against A's 16.1, and 6.3a 7.7 against 12.0.
- **W (WINDOW_CAP -10): passes, mildly.**
  - More of the bright ripples and less of the dark band sit in frame.
  - Tone and edges rise against A: 1.6a 86 and 23.5 against 72 and 22.0; 4.1a 55 against 47; 6.3a edges 13.8 against 12.0;
    2.2a 15.9 against 14.9.
  - The bottom fifth is equal or brighter.
  - No pale sheet of the window appears in any still.
  - 1.6a's top left has hard black patches between the bright ripples, harsher than A.
  - 2.6a's still is A's, byte for byte (the cap never binds on a level look).
- **A (the film look)** keeps the most natural surface of the four band treatments (A, L, H and I) in 2.6a.

## For the ruling

- R and W enter the film look.
- L, H and I leave.
- MIRROR_GAIN and MIRROR_INSCATTER stay off.
- The band left in A and W is the surface seen from below, dark outside the window with lit ripples. That is the physics, and
  the reference's bottom fifths are as dark (40 to 50).
- **Ruled after the reviewer's verdicts** (film-stills-4-review.md: on 1.6a, H, I and W pass; every other shot fails on size,
  light or the lens). W and R join the look's wide section. H and I fail elsewhere, so they cannot join a dial that covers
  every shot. The look file changes only after chain-r4 ends, since the theatre reads the main checkout's look at every
  launch, and the focus set is still rendering. The ruling is recorded as D342, with the focus rule's verdict from stills 5.
  Pass 17 waits for that verdict.
