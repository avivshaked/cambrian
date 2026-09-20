# The streams in a wide shallow tank

*2026-09-20 evening. The owner ruled the bed raised into the lit band in the 2,200 m² tank,
and the streams refuse a tank that flat. This is what was measured, the relaxation the
algebra supports, and the one number that is the owner's. Nothing is built yet. The probe is
`src/Evosim.Core.Tests/StreamsShallowProbe.cs` (seven tests, marked slow, 14 s:
`./scripts/core-test.ps1 -All -Filter StreamsShallowProbe -ShowOutput`). It reads
`CurrentField`'s private passes by reflection and changes no production code, and on every
tank that constructs its amplitude equals the field's to the last bit.*

## 1. Why the refusal happens

The streams are a gyre and eddies plus an overturning cell. `BuildStreams` solves for the
cell's amplitude β so that the vertical RMS equals the horizontal RMS on each axis, which is
D088's rule. With the streams' horizontal mean square normalised to 1 and the cross term
zero by symmetry, the condition is `a·β² = 1/2` with `a = V − H1/2`. `V`, the cell's
vertical mean square at unit amplitude, is a constant of the mode set, 0.026523. `H1`, its
horizontal mean square, grows as `(R/D)²`. So `a` falls as the tank flattens and crosses
zero at one depth.

| depth, m (R = 26.46 m) | D/R | builds today | a | β | vertical : horizontal |
|---|---|---|---|---|---|
| 45 | 1.70 | yes | 0.02116 | 4.86 | 1.00 |
| 35 | 1.32 | yes | 0.01766 | 5.32 | 1.00 |
| 30 | 1.13 | yes | 0.01446 | 5.88 | 1.00 |
| 25 | 0.94 | yes | 0.00915 | 7.39 | 1.00 |
| 20 | 0.76 | no | −0.00062 | none | refused |
| 15 | 0.57 | no | −0.02173 | none | refused |

The most vertical motion the cell can carry against the horizontal, `k_max`, is one
constant times the aspect: `k_max = 1.308 · D/R`, to four figures across areas from 100 to
2,200 m² and depths from 30 to 60 m. The critical depth is `R / 1.308`, 20.2 m in this
tank. The seed moves it by a thousandth and the bed not at all, since the balance is solved
on the flat field before the bed's pass. The bed has a wall of its own: at relief 1.5 m and
tilt 30 m `BedShape` refuses a depth of 17.5 m or less, so a 20 m tank clears it by 2.5 m.

Two things the table says beside the refusal. A tank just above the critical depth builds
today and is mostly overturning cell: β is 7.4 at 25 m and would be about 16 at 21 m, the
balance bought by drowning the eddies. Nothing recorded sits there (round 42 is at 4.86,
its 30 m screen at 5.88). And capping β for tanks that build today would break their
replay, so that band is left as it is and named here.

## 2. The relaxation

Generalise the target and leave the solver alone. Let `k` be the vertical RMS over the
horizontal RMS on one axis. The condition becomes the same quadratic with
`a_k = V − k²·H1/2`, `b_k = −k²·C`, `c_k = −k²·H0/2`, and at `k = 1` these are today's three
coefficients term for term. The rule acts only in the branch where today's code throws, as
the `else` of the existing test on `a`, so every tank that builds today takes the same
lines and replays by construction.

In that branch `k = λ · k_max`, with `k_max = sqrt(2V/H1)` measured from the pass already
running, never written as a literal. `λ` must be under 1, because the ratio approaches
`k_max` only as β goes to infinity. It trades vertical motion against how much of the water
is overturning cell, and it is what the water is, so it is the owner's.

| λ | at 20 m: vertical : horizontal | β | reading |
|---|---|---|---|
| 0.90 | 0.89 | 8.9 | nearly balanced axes; the cell 1.8 times round 42's amplitude, the eddies a smaller share |
| 0.76 | 0.75 | 5.0 | the cell at round 42's amplitude, so the eddies keep the share they have today; the vertical a quarter weaker than the horizontal |
| 0.60 | 0.59 | 3.4 | a quiet column under lively horizontal water |

The total RMS stays the knob at any β, because the scale is fitted after β: the probe
injected the first row's amplitudes and read 0.1006 m/s at a knob of 0.1, with 0.0606,
0.0534 and 0.0600 m/s on x, y and z at 20 m. The vertical does not collapse; the
horizontal takes up the slack.

## 3. What else was checked

Nothing downstream assumes equal axes. The grid's substep bound is measured after β and
falls in a shallow tank (1.03 m/s at 20 m against 1.11 at 25 m). The closed-form
acceleration and the vector potential are linear in the cell's amplitude, so the
conservative transport's telescoping holds at any β. No test asserts the refusal.

Tests the build needs: a shallow case of `StreamsTests`' axis balance asserting the ratio
the rule chose and the knob; shallow cases of the divergence, wall, surface and bed checks
at the larger β; a constant-field check beside conservation on a shallow tank; the body and
tracer spread tests at 20 m, since a shallow tank puts more of its energy on curved
horizontal streamlines, which is D090's centrifuge; and a guard that round 42's geometry at
45 m and 30 m gives the same β, scale and transport bound to the bit before and after.

The run should say what water it had: one header token beside the current's,
`axes v:h 0.75`, reading `1.00` on every balanced tank, and once in `run.json`. It is
derived from depth and radius, so there is no tunable and no recorded config is refused. The
token lives under `Assets/Evosim`, so it moves `simHash` and ships with the Core change,
between rounds.
