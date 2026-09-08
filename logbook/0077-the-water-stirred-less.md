# 0077 — The water stirred less

**2026-09-08**  ·  round 31, pre-registered before launch; one number changes

Round 30 (logbook/0075) met the goal rule in every seed and kept no swimmer. The one
jointed line that lasted fed as well as the sitters and moved no faster than the drift.
Logbook/0076 asked the field why and measured the answer: at the world's mixing rate of
0.2 m²/s the hole a sitting mouth eats is refilled in about five seconds, faster than the
mouth drinks, so a mover and a sitter eat the same. At a tenth of the rate a swimmer eats
two and a half times what a sitter eats, a corpse is a patch for a minute, and the larder
stays near the producers that made it. The owner ruled (D085) that the water is stirred
less and nothing else changes. This is that round.

## What is held

Everything in round 30's launcher: the vertex world with its two kernels, merge distance,
cap and quantum; D082's prices (neuron 0.005 W, connection 0.001 W, work at a quarter);
round 28's contact world with the three senses on, added mass 0.5, the global brain
retired, single-threaded physics; 30,000 s, five seeds. The matter field's own mixing stays
at 2 m²/s: the conception gate is not the prize under test. Remineralisation stays off,
because 0076 measured that nothing reaches the floor in a lifetime at either rate.

## What changes

`EVOSIM_MIXING 0.02` and `EVOSIM_H_MIXING 0.02`, from 0.2. This is the detritus field's
random walk, vertical and sideways, and D084's rule that the two are equal holds at the new
value. The step per half second goes from 0.45 m to 0.14 m, and the time a 1 m hole takes
to refill from about 5 s to about 50 s. Nothing in Core moves; the launcher is
`scratch/launch-r31.ps1`.

The cost stated before the reading. Less stirring also means a stomach's own water is
refilled more slowly from the field at large, so a stomach line that lived on the mixing
carrying food to it (0076 measured the sitter's income tripled by it) may thin. That is
the price of a world in which sitting still costs something, and M0 reads whether the
world can pay it.

## Sequencing

Five seeds at dt 0.02 for 10,000 s on workers 2 to 6 first (`r31q-s1` to `r31q-s5`), read
within the step and only for whether the world stands, the identities close, a stomach
line forms and the loop closes. Nothing about joints, swimming or depth is read from them.
If the screen does not fail the world, the round runs at 0.01 for 30,000 s on the same
workers (`r31-s1` to `r31-s5`), and that is the round this entry scores.

## Validity checks

- **V1 header.** Every token of round 30's, with `mixing 0.02 m2/s` and `h-mix 0.02 m2/s`.
  Read from the report.
- **V2 identities.** `audit` within a joule, `mat resid` 0 to the rounding, `mat short` 0,
  `vtx` below the cap, `verticesMerged` 0.
- **V3 manifests.** `ended budget`, `diverged 0`, `physicsJobWorkers 0`, `gitDirty false`,
  one `coreHash`.

## Predictions

The goal is D063's three seeds of five. The reference is the base world's own count, which
is round 30's five.

| # | prediction | falsified by |
|---|---|---|
| M0 | **the world stands with its water stirred less**: `alive` at 30,000 s within 0.5 to 1.5 of round 30's same seed in every arm, and D063 as amended holds in at least 3 of 5 | `alive`, the scorer |
| M1 | **a still stomach eats a hole**: `food rig` over the last 6,000 s below round 30's same seed by 30% or more in at least 3 of 5, with the layer mean `J/m3 here` not lower by as much, since the hole is at the body and not in the water at large | `food rig`, `J/m3 here` |
| M2 | **a jointed guild persists**: `jnt inh` at least 10 at every sample over the last 6,000 s in at least 2 of 5 (round 30 read 0 in every arm at the end; seed 4's line of 14 lasted to 10,000 s) | `jnt inh` |
| M3 | **movement pays where it exists**: in every arm with a jointed guild at the end, `food jnt` exceeds `food rig` by more than 20% over the last 6,000 s, with `spd jnt` above `spd rig` by 50% or more (0076: a mover at the drift speed gains 12 to 26%, a swimmer 2.2 to 2.5 times) | `food jnt`, `food rig`, `spd jnt`, `spd rig` |
| M4 | **the loop still closes**: `det out` within 0.5 to 2 of `det exuded` per window over the last 6,000 s in every seed that passes M0, and `detritus J` at the end below three times round 30's same seed | `det out`, `det exuded`, `detritus J` |
| M5 | **the larder stays shallow**: `det deep` (the deep layer's edible density) at the end below round 30's same seed in at least 4 of 5, and `% on floor` below 5% in every arm (0076's column profile: 86% in the top 20 m at 0.02, nothing on the floor) | `det deep`, `% on floor` |
| M6 | **the identities close**: `audit` 0.0000%, `mat resid` 0, `mat short` 0 at every sample; neither impulse limiter binds; `vtx` below the cap | `audit`, `mat resid`, `mat short`, the manifests |

## The two-sided readings

- **M0 fails:** the stomach lines lived on the stirring, and a world in which sitting costs
  cannot feed a sitter. The exudate fraction and the mixing rate between 0.02 and 0.2 are
  the levers, both the owner's; the first arm is 0.05, halfway on a log scale.
- **M0 holds, M1 fails:** the hole is not there in the world though it is in the
  experiment; the producers beside every stomach refill it. Read `food rig` against
  `J/m3 here` per seed, then the exudate fraction is the lever (D085 item 3).
- **M1 holds, M2 fails:** the hole exists and no swimmer lives in it. The prize is real and
  unreachable at this stroke, so the question is the drive: read `spd jnt` in the early
  samples for whether any line moved faster than the drift, and the stroke's cost and the
  added mass are next, the owner's.
- **M2 holds, M3 fails:** swimmers persist without eating better, so a jointed body pays
  for something else; `dep jnt` against `dep rig`, then the active-versus-clamped assay.
- **M1 to M3 hold:** movement pays. The owner words the movement clause of the goal rule,
  and predation on contact is next (D081's order).
- **M5 fails:** the food went deep after all and 0076's one-lifetime profile did not hold
  over ten; remineralisation is turned on in the round after, and D085's withdrawal of it
  is reversed in writing.
- **M6 fails:** the round is censored and the fault fixed before anything is read.

## What the round does not ask

Whether a sense is used. Round 30 carried one in one seed of five at the new price, and a
sense that reads a gradient is worth carrying only where following it buys something; that
question waits for a world in which M1 holds. Nor whether exudate should found vertices
(D085 item 4), which is orthogonal to the prize and queued behind this round.
