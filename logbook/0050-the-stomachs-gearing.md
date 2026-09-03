# 0050 — The stomach's gearing

*2026-09-03. Pre-registered while round 13 was still running; results appended after. Round 14:
two doses of one knob, five seeds each, on the owner's ruling ("we need the absorbers to have
children"). Launches as round 13's workers free.*

## Where round 13 stood when this was written

At t ≈ 18,000–20,000 of 30,000, round 13 ([0049](0049-marine-snow.md)) had done what its
physics promised and nothing its biology needed. The deep field held 2–4 J/m³ against round
12's 9–10 at the same time, the floor share sat at 0.4%, and both vent seeds cleared M6 with
over 1,400 alive by t=10,000. The trapdoor is closed. But the larder reached the population in
one seed of five (`r13a-s2`, −12 m, 6–9 J/m³ from t=12,000); the other four floated up into a
surface film a couple of metres thick where the field reads 0.3–1 most of the time. Refused
conceptions ran *higher* than round 12's (M2 failing). And no chain started anywhere:
absorptive mutants appeared at one or two per sample in every arm and none left an absorptive
child. `r13a-s2` is the telling case — a mutant in 7–9 J/m³ with refusals flat since t=9,000
and no line — which is 0049's pre-registered "M1 holds, M4 fails" reading: the gate is inside
the mutant.

**The ledger says where.** Per cubic metre of tissue, from the code
(`AbsorptiveCell.Acquire`, `PhotosyntheticCell.Acquire`, the registry's upkeeps):

| tissue | income | upkeep | net |
|---|---|---|---|
| absorptive in the surface film (0.3–1 J/m³) | 0.3–1 W | 4 W | −3 to −4 W |
| absorptive in `r13a-s2`'s water (7–9 J/m³) | 7–9 W | 4 W | +3 to +5 W |
| absorptive in round 12's deep larder (15–21 J/m³) | 15–21 W | 4 W | +11 to +17 W |
| photosynthetic at the surface (estimate) | ~50 W | 3 W | ~+47 W |

The photosynthetic figure is for a 0.2 m cube, top face lit, 200 W/m² irradiance, efficiency
0.05, unshaded — an order-of-magnitude estimate, not a measurement. An absorptive part's
income is density × clearance rate, and the clearance rate has been 1 m³/s per m³ of tissue
(`EVOSIM_CLEARANCE`) in every round, so a stomach breaks even at 4 J/m³. A mutant that swaps a
leaf for a stomach gives up roughly forty watts per cubic metre of that part, in the best water
any seed has offered. It breeds slower than its siblings and drifts out: the 0.75 children per
member of 0048. Marine snow made this worse in one respect — it lifted the food into the light,
where a leaf beats a stomach ten to one, and left the deep below break-even, so the one place a
stomach could out-earn a leaf no longer exists.

## The treatment

| arm | knobs (all others: round 13 arm A exactly — rolls 0.3 m/s in 30 m cells, period 6,000, blink 3,000, fields advected, 4 patches, excretion 0.01, sink 0.002 both fields, vent off) | break-even | stomach at 7 J/m³ |
|---|---|---|---|
| **c5** (`r14c5-s1..5`) | `EVOSIM_CLEARANCE` **5** (was 1) | 0.8 J/m³ | 35 W/m³ gross, +31 net |
| **c10** (`r14c10-s1..5`) | `EVOSIM_CLEARANCE` **10** | 0.4 J/m³ | 70 W/m³ gross, +66 net |

**Dose arithmetic, stated first.** At 5 a stomach in 7 J/m³ earns about what a leaf earns at
the surface, and the surface film itself (0.3–1 J/m³) is at or above break-even; at 10 the
stomach out-earns the leaf in any water above ~5 J/m³ and the film is profitable. The two
doses bracket "a stomach can pay for itself where the mutants are born" (5) and "a stomach
beats a leaf where the food is" (10). No satiation cap and no clearance toe are set, so intake
stays linear in density (D062's knobs off, as in every round since). The knob is
`AbsorptiveCell.ClearanceRate`, listed as unmeasured in DESIGN.md §5A.10; this round is its
first measurement. Reasoning and the rejected alternatives are D068. Budget 30,000 s, wall
600 min, ceiling 8,000, area 100, seeds 1–5. Controls: round 13's arm A (`r13a-s1..3`, the
same world at clearance 1).

**Round 13 is cut to five arms.** Seeds 4 and 5 of both round-13 arms are not launched: the
machine holds five arms, the owner ruled for this round, and round 13's chain result was zero
of five in every arm at two-thirds of budget. It is scored on `r13a-s1..3` and `r13b-s1..2`
and labelled so.

*Amended 2026-09-03 while the seed-1 arms were at t≈10,000, on the owner's ruling (D069):
the round runs under the **sequential-seed rule** — seeds 1 and 2 of both arms first, seeds
3–5 only if an inherited line appears in either — and the **futility stop** — an arm with no
inherited absorptive by t=15,000 is stopped and scored as failed. Seeds 1–2 were launched
before the ruling and run to budget regardless. `r14c5-s1`'s early line (seven inherited at
t=7,000) had faded to zero by t=10,700 when this was written.*

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `clearance 5` / `clearance 10`, `sink 0.002 m/s, matter 0.002 m/s`, `vent off`; every other token equals `r13a-sN`'s | header line 3 |
| V2 | no arm replays `r13a-sN` past t=0 (compare the full row, including the field columns — the creature columns alone are identical at t=100 whenever only the fields changed, 0049's V2 lesson) | row diff at t=500 |
| V3 | `floor` = 0 after t=3,100 | `floor` |
| V4 | audit 0.0000% every sample | `audit` |
| V5 | monitor: 32-min content-growth stall rule, 90-s discriminator before any kill | monitor config |

## Predictions

Scored under D063 unchanged, recruitment clause from `lineage.jsonl`; a pass is a pass in the
discovery regime (mutation 0.005) and is labelled so.

| # | prediction | falsified by |
|---|---|---|
| M1 | **lines form**: `inherit` ≥ 1 in ≥ 3 of 5 seeds per arm, and ≥ 10 inherited at some sample in at least one seed (round 13: never above 0 in any arm) | `inherit`, lineage |
| M2 | **the round's answer**: ≥ 3 of 5 seeds pass D063 in at least one arm. Predicted: **c10 passes, c5 is marginal** — at 5 the stomach only matches the leaf where the food is richest, at 10 it wins everywhere the mutants are born | the scoring table |
| M3 | **the chain is real — it grazes**: in seeds where a line forms, `J/m3 here` at t > 15,000 falls below the same seed's `r13a-sN` reading (a feeding guild depletes its field), and `det deep` falls with it | `J/m3 here`, `det deep` |
| M4 | **no bloom**: no seed reaches the 8,000 ceiling; an absorber runaway is bounded by its own field and a producer runaway would be the same world as round 13's, which did not bloom | `**Ended:**`, `alive` |
| M5 | **producers persist**: `alive` ≥ 1,000 at the last sample in every seed — absorptive tissue eats detritus, not bodies, so a producer collapse here would be a matter effect, not grazing | `alive` |
| M6 | **the dose orders the effect**: c10 shows more inherited absorptives than c5 at matched seeds and times, in ≥ 3 of 5 pairs | `inherit` by seed |

## The two-sided readings

- **M2 passes:** the goal is met (discovery regime, stated as such), and the frontier moves to
  movement, in water that moves and feeds — 0049's reading, one lever later.
- **M1 fails at clearance 10:** the ledger says a stomach out-earns a leaf and the line still
  does not form — income was not the gate. The candidates are matter at conception (`mat blk`
  against the mutant's patch) and expression (an absorptive parent whose children develop no
  absorptive part; `lineage.jsonl` carries the expressed flag per birth, so this one is
  countable). Either way the next step is the per-creature ledger instrument — intake, upkeep
  and reserve logged for absorptive individuals — before another world change, because two
  rounds have now moved the world under a mutant whose budget nobody has seen.
- **c5 passes:** the lower dose suffices; c10 is a dose check and the world keeps 5.
- **M1 holds, M3 fails:** lines exist but do not dent the field — they are small, and the
  question becomes what caps them (matter is the first suspect).
- **M4 fails:** a bloom at higher clearance means the absorbers found a runaway; censor, read
  which guild ran, and the lever is `MatterPerCreature` or the ceiling, not clearance back.
- **M6 fails with M1 holding:** the effect is not monotone in dose — a saturating term
  (satiation is off, so this would be the matter draw) sits between clearance and children.

## Launch

Ten arms, ≤ 5 concurrent, interleaved so both arms have early seeds: `r14c5-s1`, `r14c10-s1`,
`r14c5-s2`, `r14c10-s2`, `r14c5-s3`, then the rest as workers free. No code changed for this
round, so a worker needs a hash check, not a refresh. `scratch/queue-r14.ps1` watches round
13's five workers and launches the next arm on each as it ends cleanly and hash-checks; it
appends each arm to the round-13 monitor's watch list, so the same monitor covers both rounds.
Headers verified against the table before any arm is believed. Results appended below.

## Results

*Interim, 2026-09-03 evening, updated that night: four arms ended, four
more launched under the sequential rule (`r14c10-s3`, `r14c10-s4`, `r14c5-s3`, `r14c5-s4`),
of which two were stopped under the futility rule on the owner's word; seed 5 of each dose
will not be launched (owner, same ruling: the mechanism is clear and another draw against
a ceiling of six cannot reach ten — see `fable-propose-detritus-flux.md`). Scored against D063 as amended: producers persist · an absorptive line
inherited for ≥ 20 consecutive samples · ≥ 10 of it alive at the last sample · an
absorptive birth within the last 20 samples (from `lineage.jsonl`). V1 verified from each
header at launch; V2 — every arm differs from its `r13a-sN` at the t=500 row; V3 — `floor`
0 from t=3,100 in all four; V4 — audit 0.0000% at every sample in all four.*

| arm | ended | alive at end | longest inherited run (samples, t) | peak inherited | inherited at end | absorptive births in last 20 samples | clauses | verdict |
|---|---|---|---|---|---|---|---|---|
| `r14c5-s1` | budget | 1,747 | **138** (16,300–30,000) | 9 (t=6,700) | 4 | 2 | ✓ ✓ ✗ ✓ | **fail** — on the ≥ 10 alive clause alone |
| `r14c5-s2` | budget | 1,775 | 0 | 0 | 0 | 1 (a mutant, not inherited) | ✓ ✗ ✗ ✓ | **fail** — no line ever; the population lived in the film at −2 to −5 m |
| `r14c10-s1` | budget | 1,838 | 95 (5,000–14,400) | **22** (t=7,200) | 0 | 3 (mutants, none inherited) | ✓ ✓ ✗ ✓ | **fail** — the line grazed its field and died out by t=16,000 |
| `r14c10-s2` | budget | 1,731 | 155 (8,800–24,200) | **48** (t=14,400) | 0 | 0 | ✓ ✓ ✗ ✗ | **fail** — the campaign's largest line, 48 → 12 → 4 → 0 in water it thinned to 0.4–0.7 J/m³; gone by t=24,400, no second wave by 30,000 |
| `r14c10-s3` | **stopped at t=14,500** under the futility rule (owner, 2026-09-03 night) | 1,735 | 0 | 0 | 0 | — | ✓ ✗ ✗ · | **fail** — no line by 15,000; the population in the surface film at 0.7 m |
| `r14c5-s3` | **stopped at t=17,900** under the futility rule | 1,785 | 0 | 0 | 0 | — | ✓ ✗ ✗ · | **fail** — no line by 15,000; surface film at 0.3 m |

The two clearance-10 seeds are the first worlds in this campaign to grow an absorptive
line past twenty members, and both lines are gone or going by the run's second half. The
shape is the same in each: the line forms in water at 3.3–4.1 J/m³ at the population's
depth, grows for six to eight thousand seconds, and the field at that depth falls as it
grows — `r14c10-s1`'s at −15 m from 3.3–4.0 to 1.1–1.3 J/m³ by t=8,000, `r14c10-s2`'s at
−10.5 m from 4.1 to 0.4–0.7 — until the line is below its own break-even and starves.
After `r14c10-s1`'s line died the field rebuilt to 2.0–2.2 J/m³ by t=18,000–20,000, and
no second line formed in the 12,000 s that remained: single mutants appeared from t=22,000
and none was inherited. One cycle, no second wave. The clearance-5 seed that formed a line
kept it: 3 to 9 members for the last 13,700 s of the run in 3–7 J/m³ at the population's
depth, never reaching ten.

The prediction table, so far:

| # | prediction | standing |
|---|---|---|
| M1 | lines form: `inherit` ≥ 1 in ≥ 3 of 5 seeds per arm, ≥ 10 at some sample in one | **c10: held on the ≥ 10 clause** (peaks 22 and 48 in seeds 1–2) but not on the 3-of-5 clause as run — seed 3 stopped without a line, seed 5 not launched; c5: 2 of 4 seeds with a line (peaks 9 and 2), none ≥ 10 |
| M2 | ≥ 3 of 5 seeds pass in one arm; c10 predicted to pass | **0 of 6 scored** (four to budget, two stopped under the futility rule); the c10 lines fail on the alive clause by overshoot, not by never forming — a third reading the two-sided list did not have |
| M3 | the chain grazes: `J/m3 here` at t > 15,000 below the seed's `r13a-sN`, `det deep` with it | **held where it can be read cleanly**: `r14c10-s2` against `r13a-s2` at matched depth (−10.5 vs −11.5 m), 0.74 against 6.55 J/m³, `det deep` 1.08 against 4.75; `r14c10-s1`'s field fell while its line lived and rebuilt after it died. But `det deep` is lower in the no-line seed `r14c5-s2` too (3.5 against 4.75), and `J/m3 here` is read at the population's depth, which moved between rounds in three of four seeds — so neither column discriminates on its own |
| M4 | no bloom | **held** in four of four (max 1,858; ceiling 8,000) |
| M5 | producers persist, ≥ 1,000 | **held** in four of four |
| M6 | c10 > c5 at matched seeds | **held** in 2 of 2 pairs so far (22 vs 9; 48 vs 0) |

**What this is, read with 0051.** The assay showed that fifty stomachs at clearance 10
graze their water to a tenth of what fifty at clearance 1 leave, and earn the same. Here
the same thing happens at population scale, from a single mutant: the line grows on the
field it found, thins it below break-even, and crashes, with no second wave inside the
budget. The goal's alive clause is a stability clause, and clearance 10 buys growth
without stability. Clearance 5 buys a line that neither grows nor dies. The knob that was
meant to raise income raised it, and the field absorbed the gain — which is the
consumer–resource answer to "does the gearing pass": not on its own. The stabilisers that
exist and have not run in this world — tried once each in round 8's arm B (toe 4, satiation
20) and rounds 7 and 9 (refuge), all in worlds without rolls, the slow sink or a geared
stomach — the clearance toe (`EVOSIM_CLEARANCE_TOE`, type III
feeding: a stomach in thin water stops grazing before it empties it), the floor refuge
(`EVOSIM_FLOOR_REFUGE`: a fraction of the field no stomach can reach), satiation — are the
next screen, and the ledger calculator is the first place to run them. The owner's
question of the day, whether an oscillating equilibrium is reachable with the current cell
types, is now an empirical one with a partial answer: one cycle has been seen, and it did
not come back within 12,000 s; the seed-3 and seed-4 arms will show whether that holds.
