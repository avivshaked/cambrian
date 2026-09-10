# 0050 — The stomach's gearing

**2026-09-03**  ·  food-chain goal, round 14 · pre-registered before launch, results appended
after

A stomach in this world had always cleared one cubic metre of water per second for each
cubic metre of its tissue. Nobody had ever measured whether that was enough to live on. It
was not. A leaf at the surface earns about forty times what a stomach earns in the best
water any seed has offered. This round turned the clearance rate up by five and by ten. At
ten, two worlds grew the campaign's first absorptive lines past twenty members, and both
lines ate their water down below their own break-even and starved. No second wave came after
them.

The round is two doses of one knob, five seeds each, on the owner's ruling that *"we need
the absorbers to have children"*. It was pre-registered while round 13 was still running,
and launched as round 13's workers freed.

## Where round 13 stood when this was written

At t ≈ 18,000–20,000 of 30,000, round 13 ([0049](0049-marine-snow.md)) had done what its
physics promised and nothing its biology needed. The deep field held 2–4 J/m³ against round
12's 9–10 at the same time, and the floor share sat at 0.4%. Both vent seeds cleared M6,
with over 1,400 alive by t=10,000. The trapdoor is closed.

The larder reached the population in one seed of five, `r13a-s2` at −12 m in 6–9 J/m³ from
t=12,000. The other four floated up into a surface film a couple of metres thick, where the
field reads 0.3–1 most of the time. Refused conceptions ran *higher* than round 12's, so M2
failed.

No chain started anywhere in it. Absorptive mutants appeared at one or two per sample in
every arm, and none left an absorptive child. The telling case is `r13a-s2`, a mutant in 7–9
J/m³ with refusals flat since t=9,000 and no line. That is 0049's pre-registered reading
where M1 holds and M4 fails. The gate is inside the mutant.

The ledger says where it is. Here are the figures per cubic metre of tissue, from the code
(`AbsorptiveCell.Acquire`, `PhotosyntheticCell.Acquire`, and the registry's upkeeps):

| tissue | income | upkeep | net |
|---|---|---|---|
| absorptive in the surface film (0.3–1 J/m³) | 0.3–1 W | 4 W | −3 to −4 W |
| absorptive in `r13a-s2`'s water (7–9 J/m³) | 7–9 W | 4 W | +3 to +5 W |
| absorptive in round 12's deep larder (15–21 J/m³) | 15–21 W | 4 W | +11 to +17 W |
| photosynthetic at the surface (estimate) | ~50 W | 3 W | ~+47 W |

The photosynthetic figure is for a 0.2 m cube, top face lit, at 200 W/m² irradiance and
efficiency 0.05, unshaded. It is an order-of-magnitude estimate rather than a measurement.

An absorptive part's income is density × clearance rate. The clearance rate has been 1 m³/s
per m³ of tissue (`EVOSIM_CLEARANCE`) in every round, so a stomach breaks even at 4 J/m³. A
mutant that swaps a leaf for a stomach gives up roughly forty watts per cubic metre of that
part, in the best water any seed has offered. It breeds slower than its siblings and drifts
out, at the 0.75 children per member of 0048.

Marine snow made this worse in one respect. It lifted the food into the light, where a leaf
beats a stomach ten to one, and left the deep below break-even. The one place a stomach
could out-earn a leaf no longer exists.

## The treatment

| arm | knobs (all others: round 13 arm A exactly — rolls 0.3 m/s in 30 m cells, period 6,000, blink 3,000, fields advected, 4 patches, excretion 0.01, sink 0.002 both fields, vent off) | break-even | stomach at 7 J/m³ |
|---|---|---|---|
| **c5** (`r14c5-s1..5`) | `EVOSIM_CLEARANCE` **5** (was 1) | 0.8 J/m³ | 35 W/m³ gross, +31 net |
| **c10** (`r14c10-s1..5`) | `EVOSIM_CLEARANCE` **10** | 0.4 J/m³ | 70 W/m³ gross, +66 net |

The dose arithmetic, stated first. At 5 a stomach in 7 J/m³ earns about what a leaf earns at
the surface. The surface film itself, at 0.3–1 J/m³, is then at or above break-even. At 10
the stomach out-earns the leaf in any water above ~5 J/m³, and the film is profitable. The
two doses bracket two claims. Dose 5 says a stomach can pay for itself where the mutants are
born, and dose 10 says a stomach beats a leaf where the food is.

No satiation cap and no clearance toe are set, so intake stays linear in density, with
D062's knobs off as in every round since. The knob is `AbsorptiveCell.ClearanceRate`, listed
as unmeasured in DESIGN.md §5A.10, and this round is its first measurement. The reasoning
and the rejected alternatives are in D068.

Budget 30,000 s, wall 600 min, ceiling 8,000, area 100, seeds 1–5. The controls are round
13's arm A (`r13a-s1..3`), the same world at clearance 1.

Round 13 is cut to five arms. Seeds 4 and 5 of both round-13 arms are not launched. The
machine holds five arms, the owner ruled for this round, and round 13's chain result was
zero of five in every arm at two-thirds of budget. It is scored on `r13a-s1..3` and
`r13b-s1..2`, and labelled so.

Amended on 2026-09-03, while the seed-1 arms were at t≈10,000, on the owner's ruling (D069).
The round runs under a sequential-seed rule and a futility stop. The sequential-seed rule
runs seeds 1 and 2 of both arms first, and seeds 3 to 5 only if an inherited line appears in
either. The futility stop ends an arm with no inherited absorptive by t=15,000 and scores it
as failed. Seeds 1 and 2 were launched before the ruling and run to budget regardless.
`r14c5-s1`'s early line, seven inherited at t=7,000, had faded to zero by t=10,700 when this
was written.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `clearance 5` / `clearance 10`, `sink 0.002 m/s, matter 0.002 m/s`, `vent off`; every other token equals `r13a-sN`'s | header line 3 |
| V2 | no arm replays `r13a-sN` past t=0 (compare the full row, including the field columns — the creature columns alone are identical at t=100 whenever only the fields changed, 0049's V2 lesson) | row diff at t=500 |
| V3 | `floor` = 0 after t=3,100 | `floor` |
| V4 | audit 0.0000% every sample | `audit` |
| V5 | monitor: 32-min content-growth stall rule, 90-s discriminator before any kill | monitor config |

## Predictions

The round is scored under D063 unchanged, with the recruitment clause from `lineage.jsonl`.
A pass is a pass in the discovery regime, at mutation 0.005, and is labelled so.

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

Ten arms, at most 5 concurrent, interleaved so that both arms have early seeds.

| batch | arms |
|---|---|
| first five | `r14c5-s1`, `r14c10-s1`, `r14c5-s2`, `r14c10-s2`, `r14c5-s3` |
| then | the rest, as workers free |

No code changed for this round, so a worker needs a hash check rather than a refresh.
`rounds/queue-r14.ps1` watches round 13's five workers and launches the next arm on each as
it ends cleanly and hash-checks. It appends each arm to the round-13 monitor's watch list,
so the same monitor covers both rounds. Headers are verified against the table before any
arm is believed, and results are appended below.

## Results

Final on 2026-09-04. Six arms ended, and four more launched under the sequential rule, of
which two were stopped under the futility rule on the owner's word. Seed 5 of each dose will
not be launched, on the owner's ruling the same day. The mechanism is clear, and another
draw against a ceiling of six cannot reach ten (see `fable-propose-detritus-flux.md`).

The round is scored against D063 as amended. Producers persist, and an absorptive line is
inherited for ≥ 20 consecutive samples. At least 10 of it are alive at the last sample, with
an absorptive birth within the last 20 samples, read from `lineage.jsonl`.

V1 was verified from each header at launch. V2 holds, since every arm differs from its
`r13a-sN` at the t=500 row. V3 holds, with `floor` 0 from t=3,100 in all four, and V4 holds,
with the audit at 0.0000% at every sample in all four.

| arm | ended | alive at end | longest inherited run (samples, t) | peak inherited | inherited at end | absorptive births in last 20 samples | clauses | verdict |
|---|---|---|---|---|---|---|---|---|
| `r14c5-s1` | budget | 1,747 | **138** (16,300–30,000) | 9 (t=6,700) | 4 | 2 | ✓ ✓ ✗ ✓ | **fail** — on the ≥ 10 alive clause alone |
| `r14c5-s2` | budget | 1,775 | 0 | 0 | 0 | 1 (a mutant, not inherited) | ✓ ✗ ✗ ✓ | **fail** — no line ever; the population lived in the film at −2 to −5 m |
| `r14c10-s1` | budget | 1,838 | 95 (5,000–14,400) | **22** (t=7,200) | 0 | 3 (mutants, none inherited) | ✓ ✓ ✗ ✓ | **fail** — the line grazed its field and died out by t=16,000 |
| `r14c10-s2` | budget | 1,731 | 155 (8,800–24,200) | **48** (t=14,400) | 0 | 0 | ✓ ✓ ✗ ✗ | **fail** — the campaign's largest line, 48 → 12 → 4 → 0 in water it thinned to 0.4–0.7 J/m³; gone by t=24,400, no second wave by 30,000 |
| `r14c10-s3` | **stopped at t=14,500** under the futility rule (owner, 2026-09-03 night) | 1,735 | 0 | 0 | 0 | — | ✓ ✗ ✗ · | **fail** — no line by 15,000; the population in the surface film at 0.7 m |
| `r14c5-s3` | **stopped at t=17,900** under the futility rule | 1,785 | 0 | 0 | 0 | — | ✓ ✗ ✗ · | **fail** — no line by 15,000; surface film at 0.3 m |
| `r14c10-s4` | budget | 1,840 | 90 (21,100–30,000) | 2 (t=23,000) | 2 | 1 | ✓ ✓ ✗ ✓ | **fail** — the richest water of the round (−14.5 m in 4–8 J/m³, stock 20,600 J at t=20,000) and **no boom**: 61 absorptive births across the run, never more than 5 alive, a line of 2; the five graze the stock down at 0.24 W net |
| `r14c5-s4` | budget | 1,846 | 66 (8,000–14,500) | 2 (t=8,600) | 0 | 0 | ✓ ✓ ✗ ✗ | **fail** — a line of 1–2 at −4 m in 0.9 J/m³, below clearance 5's breeding density; gone by t=14,700 |

The two clearance-10 seeds are the first worlds in this campaign to grow an absorptive line
past twenty members. Both lines are gone or going by the run's second half. The shape is the
same in each. The line forms in water at 3.3–4.1 J/m³ at the population's depth and grows
for six to eight thousand seconds. The field at that depth falls as it grows.

`r14c10-s1`'s field at −15 m went from 3.3–4.0 to 1.1–1.3 J/m³ by t=8,000. In `r14c10-s2`
the field at −10.5 m went from 4.1 to 0.4–0.7. Each line ended below its own break-even, and
starved.

After `r14c10-s1`'s line died the field rebuilt to 2.0–2.2 J/m³ by t=18,000–20,000. No
second line formed in the 12,000 s that remained. Single mutants appeared from t=22,000 and
none was inherited. One cycle, and no second wave.

The clearance-5 seed that formed a line kept it. It held 3 to 9 members for the last 13,700
s of the run, in 3–7 J/m³ at the population's depth, and never reached ten.

The prediction table, so far:

| # | prediction | standing |
|---|---|---|
| M1 | lines form: `inherit` ≥ 1 in ≥ 3 of 5 seeds per arm, ≥ 10 at some sample in one | **c10: held on the ≥ 10 clause** (peaks 22 and 48 in seeds 1–2) but not on the 3-of-5 clause as run — seed 3 stopped without a line, seed 5 not launched; c5: 2 of 4 seeds with a line (peaks 9 and 2), none ≥ 10 |
| M2 | ≥ 3 of 5 seeds pass in one arm; c10 predicted to pass | **0 of 8 — falsified** (six to budget, two stopped under the futility rule, seed 5 of each dose not launched); the c10 lines fail on the alive clause by overshoot, not by never forming — a third reading the two-sided list did not have |
| M3 | the chain grazes: `J/m3 here` at t > 15,000 below the seed's `r13a-sN`, `det deep` with it | **held where it can be read cleanly**: `r14c10-s2` against `r13a-s2` at matched depth (−10.5 vs −11.5 m), 0.74 against 6.55 J/m³, `det deep` 1.08 against 4.75; `r14c10-s1`'s field fell while its line lived and rebuilt after it died. But `det deep` is lower in the no-line seed `r14c5-s2` too (3.5 against 4.75), and `J/m3 here` is read at the population's depth, which moved between rounds in three of four seeds — so neither column discriminates on its own |
| M4 | no bloom | **held** in four of four (max 1,858; ceiling 8,000) |
| M5 | producers persist, ≥ 1,000 | **held** in four of four |
| M6 | c10 > c5 at matched seeds | **held** in 3 of 4 pairs (22 vs 9; 48 vs 0; 0 vs 0 at seed 3, both stopped; 2 vs 2 at seed 4 — a tie) |

### Instrument note: the detritus income, measured (`r14c10-s1-flux`)

Added on the night of 2026-09-03. D070 gated the exudation build on measuring the income the
proposal had inferred from a slope. The detritus-flux instrument went into the build the
same night, reporting `det in` and `det out` in joules per window.

The two columns come from `World.DetritusDepositedTotal` and `DetritusTakenTotal`. That
world was rerun on the new build at the 0.02 screening step for 15,000 s. It took 31.8 min
of wall clock at 7.9× real time, with 967 drag impulses limited and the audit at 0.0000%
throughout. The identity held to the joule, since the summed windows, in minus out, equal
the standing stock at every row, at 19,826.7 J at the end both ways.

| phase (s) | gross income (W) | grazed (W) | deaths/s | J of tissue per death | mean alive | mean stomachs |
|---|---|---|---|---|---|---|
| 0–3,000 | 4.28 | 2.68 | 0.049 | 87 | 64 | 4.5 (founders) |
| 3,000–5,000 | 2.45 | 0 | 0.069 | 36 | 256 | 0 |
| 5,000–8,000 | 1.57 | 0.03 | 0.100 | 16 | 719 | 0 |
| 8,000–11,000 | 1.12 | 0.04 | 0.128 | 8.8 | 1,233 | 0.8 |
| 11,000–13,000 | 0.82 | 0.09 | 0.137 | 6.0 | 1,518 | 3.8 |
| 13,000–15,000 | 0.57 | 0.16 | 0.120 | 4.7 | 1,658 | 4.0 |

Two readings come out of it. The first is that the income is a founding pulse that decays.
The death rate does not fall; it rises from 0.05 to 0.13 per second as the population fills
in. What falls is the tissue a dead body carries, from 87 J when founders die to 4.7 J by
t=15,000, and still falling. The world selects for small bodies, because a small body is a
cheaper child, and a small body is a small corpse.

At 15,000 the income was 0.57 W and dropping by a third every 2,000 s. The 0.01 arm's own
record is where that curve lands. Over 14,000–30,000 it ran at a net 0.19–0.24 W, with about
one stomach grazing and 2–3 J per death.

The gate's first reading is met. The mature income is of the inferred order, a few tenths of
a watt rather than the watts of the founding era, and the ceiling of about six stomachs
stands. A single 30,000-s arm on this build would put a measured number on the mature phase
itself. It was not run tonight, because the 0.01 record already gives it and the decision
does not turn on the second decimal.

The second reading is a point for the rule. Exudation is a fraction of *intake*, and intake
does not shrink with the body the way a corpse does. A population of tiny producers still
fixes the same watts per lit area. A flux that rides on intake is immune to the selection
that has been starving the second level, and one that rides on death is not.

One more same-seed butterfly, noted here. This realisation put the population at −4 to −6 m,
where the 0.01 one sat at −15, and it grew a line of 2 from t=11,000. That is 0052's
finding, again.

### Closing, 2026-09-04

Round 14 is 0 of 8 and closed. Clearance 10 grew the campaign's first lines past twenty, at
22 and 48, and both ate their stock and crashed. Clearance 5 held lines of 1–9 that never
reached ten, and three seeds never left the surface film.

The last arm, `r14c10-s4`, closes the argument. It had the richest water of the round, a
stock of 20,600 J at −14.5 m in 4–8 J/m³, and no boom. There were sixty-one absorptive
births across the run. The dissection the next morning splits them into fifty-one founders
of the floor era, six mutants born after the floor closed, and four inherited children.
Three creatures bred in the whole run, never more than five stomachs were alive at once, and
the line ended at two.

Five stomachs at clearance 10 in that water graze about half a watt, and the income is a few
tenths. The stock fell at 0.24 W net through the last 10,000 s. That is the flux ceiling
read directly. The water can be rich and the line still cannot be ten, because what refills
the water is small.

Why six mutants made a line of two was dissected the next morning. The ledger gives the
inoculum an R0 of 4–6 in that water, and the answer is not the bodies. The ledger was run on
two pure stomachs pulled from the run's last snapshot, at 0.0019 and 0.0031 m³. It gives R0
2–6 across 4–8 J/m³, which are the inoculum's figures. What the run's outputs cannot say is
where those stomachs were. `J/m3 here` is patch 0 at the population's mean depth, and a
mutant in another patch or depth may have sat in half a joule.

Two side findings came with it. The inherited children that died did so 74–253 s after
birth, with a 101 J endowment. No upkeep in this world can explain that, and no output
records the cause. One sampled mixotroph genome's absorptive subtree is also pruned at
development by `minPartVolume`, at 0.0001 m³, because its accumulated edge scale takes the
part to 5×10⁻⁵. So a genome that reads absorptive can develop into a pure leaf.

The instrument that answers all three is a per-creature ledger row for every living
absorptive body, logging depth, patch, density seen, intake and reserve at each sample. It
is queued behind the exudation build.

D070's second gate reading is met on this, since a larger stock did not turn the crash into
a cycle holding ten. The gate is open, and the exudation build starts from
`logbook/specs/exudation-spec.md`.

On the two stopped arms, recorded 2026-09-04. The Sol/GPT review of 2026-09-03 proposed
reporting them as censored and screen-negative rather than failed. The owner reaffirmed the
rule as ruled. An arm stopped for futility is stopped because it is expected to fail on the
merits, the stop saves time, and the result is a negative result. Censoring is for error or
a fault in the experiment, and an arm that could only be reported as censored should not be
stopped. The labels above stand as they are.

Here is what this is, read with 0051. The assay showed that fifty stomachs at clearance 10
graze their water to a tenth of what fifty at clearance 1 leave, and earn the same. Here the
same thing happens at population scale, from a single mutant. The line grows on the field it
found, thins it below break-even, and crashes, with no second wave inside the budget.

The goal's alive clause is a stability clause, and clearance 10 buys growth without
stability. Clearance 5 buys a line that neither grows nor dies. The knob that was meant to
raise income raised it, and the field absorbed the gain. That is the consumer–resource
answer to whether the gearing passes on its own, and the answer is no.

Three stabilisers exist and have not run in this world. Each was tried once, in round 8's
arm B (toe 4, satiation 20) and in rounds 7 and 9 (the refuge). Those were all worlds
without rolls, the slow sink or a geared stomach. The three are the clearance toe, the floor
refuge and satiation. The toe (`EVOSIM_CLEARANCE_TOE`) is type III feeding, where a stomach
in thin water stops grazing before it empties it. The refuge (`EVOSIM_FLOOR_REFUGE`) is a
fraction of the field no stomach can reach. They are the next screen, and the ledger
calculator is the first place to run them.

The owner's question of the day, whether an oscillating equilibrium is reachable with the
current cell types, is now an empirical one with a partial answer. One cycle has been seen,
and it did not come back within 12,000 s. The seed-3 and seed-4 arms will show whether that
holds.
