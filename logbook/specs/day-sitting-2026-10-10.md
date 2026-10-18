# The day sitting of 2026-10-10: the speed items timed on the card

*Run by the speed agent from 12:36 to 13:15 on 2026-10-10, alone on the card, under the coordinator's heat watch
(card at most 53 °C, the CPU at most 80 °C). The script is `logbook/specs/nursery-speed/day-sitting.ps1` and its log
`scratch/logs/day-sitting.log`. Its pass ends the speed work that gated round 58 (D160).*

## What ran

First the stray test on the card, `GpuStrayTests.AStrayIsLostOnTheCard`. It passed all 12 cases, five kinds of
stray each per part and not, in single and double, with no driver event.

Then six builds, two runs each, in the order K E A R B G G B R A E K, so that a drift over the sitting falls
evenly on every build. Each run resumes `r57-s2` at 47,000 s for 300 s, in single precision, group 32, with the
snow's transport, concurrent mode and the scent on the card, spin 5,000, 16 threads on logical 16 to 31, and a
one-minute cooling pause before it. Every run was identical to `r57-ref58-card1` in stats, digest and lineage,
read by `gpu-accept` with its engine-line check.

## The pace

The pace is `pace-calc.py`'s stepping wall, start-up left out:

| Build | Commit | Runs | Mean | Against K | ms of wall a simulated s |
|---|---|---|---|---|---|
| K, the reference | `c8762e9e` | 5.249, 5.275 | 5.262 | | 190.0 |
| E, the spin sitting's build | `efa39d62` | 5.264, 5.290 | 5.277 | +0.3% | 189.5 |
| A, 1a | `d2488f3c` | 5.376, 5.389 | 5.383 | +2.3% | 185.7 |
| R, the arena on 1a | `1096d734` | 5.470, 5.505 | 5.487 | +4.3% | 182.2 |
| B, the arena and 1b | `96b0c597` | 5.464, 5.498 | 5.481 | +4.2% | 182.4 |
| G, `r58-grow` | `5e618441` | 5.802, 5.682 | 5.742 | +9.1% | 174.1 |

Card-run-check's whole-wall figure, start-up included, ran 4.52 to 4.93 over the same runs.

## What it says

- The kernels regenerated since the spin sitting cost nothing: K and E differ by less than the spread of two runs
  of one build, about 0.5%. The 4.42 against 5.24 times real time that raised the question compared two
  measures. 4.42 was card-run-check's whole wall and 5.24 pace-calc's stepping wall, and K reads 4.52 to 4.59
  and 5.25 to 5.28 on them today.
- The copy line (1a, then the arena) saves 7.8 ms a simulated second, which matches the probe's copy savings.
- 1b adds nothing measurable on top. The speed agent's guess is that the upload it shortens is no longer on the
  critical path. It is kept, being correct and identical.
- `r58-grow` is the largest single gain, 15.9 ms a simulated second. Its two runs spread more than the others',
  2%, so its figure is the least certain.
- The copy line and `r58-grow` change different layers, so together they should give about 13%. That is an
  inference, since they have not yet run as one build.
