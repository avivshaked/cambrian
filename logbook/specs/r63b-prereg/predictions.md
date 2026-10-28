# The bloom screen: do islands on their own clocks make a world worth filming?

*Pre-registration, drafted by the coordinator on 2026-10-28 while the build's checks ran. It is gate G2 of the grazer
film's plan (`logbook/specs/grazer-film-plan.md`, D254), and it carries out D251. The build's hashes, the launcher and
the card group are filled in when the build lands. The screen launches from a clean worktree at this file's final
commit, after the card notice, HOLD and the heat watch.*

## The questions

1. Do grazers survive and evolve in a world whose fed islands bloom and fade out of step?
2. Does the bloom reach the food? Do the leaves on an island rise and fall with its influx?
3. Which world gives the film the most story? That covers crossings, crowds that gather at blooms, busts and
   recoveries, and a lineage that changes.
4. Does a smell that reaches from a blooming grove, and fat to cross on, add to that story?

## The worlds

Four arms on two seeds, `6202` and `6203`, 24,000 world seconds each, eight worlds in one card group.

| Arm | Bloom cycles | Far smell | Fat |
|---|---|---|---|
| A | off | off | off |
| B | on | off | off |
| C | on | on | off |
| D | on | on | on |

- **The base in every arm** is `r62cf`'s world (`rounds/env-r62cf.ps1`). D245's energy sense is set as D249 requires of
  every round after it, `HalfScale` with k = 30 s. Arm A is therefore not a replay of `r62cf`: it differs in that
  sense alone.
- **The cycles (B to D):** each of the plan's 11 islands draws its period from 2,500 to 6,000 s, its amplitude from
  0.8 to 1.0, and its phase uniformly. The world's total influx is held constant.
- **The far smell (C and D):** G = 0.005, a grove band B = 10 m, and a reach R = 30 m beyond the band, from the census
  in `logbook/specs/bloom-census/`.
- **Fat (D):** D247's fat with its specification's prices: the cap's form `BodyAndStore` at s = 300 s, and fat's room
  at 50,000 J/m³. In this world a cap at 1,000 s binds almost no one. One at 300 s binds 1.7 to 8.2 % of all bodies,
  and would trim the reserves of 20 to 44 % of living grazers (`logbook/specs/fat-step0/`, `grazers.txt`). Fat has room
  to buy only where the cap binds, and that trim is its cost.
- **The build** merges `bloom`, `fat` and `g2read` on main, so that the read's trait command opens the screen's
  configs. G0 has passed for both: switch-off digests, suites, the crowd
  re-record, the card identity sittings, and the pace check (bloom at no more than 15 % slower).

## The read and the gates

The read is `scripts/reads/bloom/g2-read.py`, as `logbook/specs/bloom-build/g2-read-spec.md` specifies it. That file
is the definition of every gate and readout below.

| Gate | Pass |
|---|---|
| 1. Grazers survive | every window from 9,000 s holds a mean of at least 30 grazers and never 0; D222 passes by `r62cf`'s recorded procedure |
| 2. The bloom reaches the food (B to D) | every judged island's leaf area follows its influx factor, with a correlation of at least 0.5 at the best lag |
| 3. Grazers evolve | one trait shifts by at least one scale unit between the first and last windows, from 6,000 s |

An arm passes when it passes on both seeds. The read then ranks the passing arms on four parts of story material:
- crossings at a banded gap of at least 10 m;
- islands with a crowd correlation of at least 0.3;
- recoveries;
- the largest trait shift. The ranks are summed, and ties go to the simpler arm. The winner is the film's world. Fat goes
into the combined round if arm D passes every gate.

**Fallbacks**, from the plan:
- If gate 2 fails in B to D, the period is lengthened to 5,000 to 10,000 s and the screen runs again.
- If no bloom arm passes, the film's world is arm A.

## Predictions

These are the coordinator's, written before any run. They are guesses, and are marked as such.

- **Gate 1.** A passes, since `r62cf` passed on these seeds under the same rule: its lowest window mean from
  9,000 s is 39.1 in 6202 and 34.1 in 6203. 6203's window from 9,000 to 12,000 s is close to the floor, and it is
  where A would fail first if the energy sense costs grazers anything. At the sensitivity floor of 40, every `r62cf`
  seed fails in that window (means 39.1, 34.1 and 30.6), so every arm is likely to fail at 40. That reading is
  information, not the gate. B and C pass, because the total
  influx is held constant (guess, moderate confidence). D is the likeliest to fail: fat's upkeep and room are a new
  cost (guess).
- **Gate 2.** It passes in B to D on most judged islands. The best lag is 500 to 2,000 s, because matter sinks through
  12 m of island in about 600 s and leaves take time to grow (guess). The shortest periods, near 2,500 s, are the
  likeliest to fall under 0.5.
- **Gate 3.** At least one trait shifts by one scale unit in most arms, most likely limb push or the brain's drive,
  since `r62cf`'s selection already favours movers (guess, low confidence).
- **Story.** C has more crowd-correlated islands than B, since the far smell is what lets a grazer find a bloom.
  Crossings in C are two to four times A's. Over the span from 6,000 s, at a 10 m gap, `r62cf-6202` made 5 and
  `r62cf-6203` made 8. Recoveries after a bust were 4 and 1. Arm A's crowd count is 0 by construction, since it has no
  bloom to gather at. I would not be
  surprised by no change at all, since grazers live about a minute and a crossing takes two to four (guess, low
  confidence).
- **The winner.** C, or D if fat does not cost survival (guess).

## What would change the plan

- **Gate 1 fails in A.** The base world has changed between `r62cf` and this build, through the energy sense or the
  merge. Stop and find out why before reading any other arm.
- **Gate 2 fails everywhere.** The period is too short against the leaves' lag. Take the plan's fallback.
- **Every arm fails gate 3.** Evolution is too slow to show in 24,000 s. G7's 48,000 s round is then the place to
  look, and the film's story leans on ecology rather than on a changing lineage.
