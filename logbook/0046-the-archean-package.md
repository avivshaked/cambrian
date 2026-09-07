# 0046 — The Archean package

**2026-09-02**  ·  food-chain goal, round 10 · pre-registered before launch, results appended
after

The bodies in this world had always sunk, every one of them, all their lives. Round 10 made
a founder-sized body neutral and left growth as the thing that sinks you. The drowning
stopped, and it did not come back. Getting there took three tries. The first world bloomed
into a surface film the machine could not afford, and the second did the same in a dish a
quarter the size. The third, with a fixed matter price per body, gave the campaign its first
five worlds that ran a full budget alive, uncensored and bounded. Not one of them grew a
food chain.

The round is one treatment across five seeds, on the owner's design
([D064](../DECISIONS.md)). It follows rounds 8 and 9, which scored zero passes between them.
Their post-mortems found the worlds drowning rather than starving
([0044](0044-three-medicines.md) results, [0045](0045-the-dose-and-the-dice.md)). Each
pre-registration below was written and committed before the arms it governs launched.

## The diagnosis, in one paragraph

Every body carried the same excess density whatever its size, so the whole population sank
its whole life. It held the photic band only because breeding is concentrated where the
light is. A matter drought paused births for ~1,500 s, and the standing crowd sank out of
the light together. Three worlds died with full larders, free matter and a young population.
One of the three was an untreated control. Float tissue works, and selection discards it to
~1% between crises, because a floatless producer breeds cheaper before it sinks out. Chain
arrival, downstream of all this, was drought-gated: absorptive mutants appeared and could
not breed through the drought around them.

## The treatment

| arms | knobs (all others: round 6's world exactly) | mechanism bet |
|---|---|---|
| `r10-s1..5` | `EVOSIM_NEUTRAL_VOLUME` 0.25 · `EVOSIM_FOUNDER_DEPTH` 60 · `EVOSIM_CELLTYPE_MUTATION` 0.005 · no refuge | size-dependent buoyancy (D064): a founder-sized body floats in place, growth is what sinks you, universal across guilds; founders scattered through the full 60 m column; the ratified 5× discovery regime for arrival |

The dose arithmetic, stated before results. V0 = 0.25 m³ is the founder 90th percentile,
over n = 200 draws of the default `RandomGenomeOptions`. Those volumes run min 0.040, median
0.141, p90 0.257 and max 0.438 m³. Nine founders in ten are neutral by construction, and the
largest tenth sink at ≤ 31% of the constant. A body at twice the median founder sinks at 7%,
one at 1 m³ at 60%, one at 2 m³ at 75%.

The alternative is named now rather than later. V0 = 0.44 makes every sampled founder
neutral. It is the dose to try if the trade turns out to start too early, meaning chains
fail with survivors still sinking. It is not the dose to try if the world blooms, which
would argue the other way.

`TissueExcessDensity` stays at 0.02, so a large body sinks as every body did before, and the
worlds run to date are this rule's large-body limit. Founder depth 60 is the full
`WorldDepthMetres`. The photic band ends near 23.7 m, so roughly six founders in ten start
in the dark and die of it by selection rather than by rule.

Budget 30,000 s, wall 600 min, ceiling 8,000, seeds 1–5. The controls are round 6's arms by
bit-identity, since `NeutralBodyVolume` = 0 and founder depth 20 reproduce the old world bit
for bit, which the suite enforces.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `neutralV 0.25 m3`, `founderDepth 60 m`, `cellType mut 0.005`, `refuge 0 m`, and nothing else differs from round 6's world | header line 3 |
| V2 | no arm replays its round-6 twin past t=0 — the treatment binds from the first physics step for every founder, so a token-identical prefix of any length is proof the knob did not reach the physics | row diff vs `d056-sN.md` at t=100 |
| V3 | `floor` = 0 after t=3,100 everywhere | `floor` |
| V4 | monitor carries the 32-min content-growth stall rule and the 90-s CPU+byte discriminator before any kill | monitor config |

## Predictions, and the column that falsifies each

The round is scored under the amended goal rule, [D063](../DECISIONS.md), with the
recruitment clause computed from `lineage.jsonl`. A pass is a pass **in the discovery
regime**, at mutation 0.005, and is labelled so wherever it is claimed.

| # | prediction | falsified by |
|---|---|---|
| W1 | **no drowning death**: no world's `depth m` slides more than 10 m below its pre-drought mean while `births` is frozen — the small fraction holds the light through every drought | `depth m`, `births` |
| W2 | **producers persist** to budget in ≥ 4 of 5 seeds (the second disease is cured; the first — the cohort trap — is not what this round treats) | `**Ended:**` |
| W3 | **float is kept**: the evolved float share (`float` / `alive`) at t = 20,000 exceeds round 6's ~1% in the median seed — buoyancy is now the price of size, not insurance | `float`, `alive` |
| W4 | **size sorts by guild**: at any sample with ≥ 10 inherited absorptives, their mean body volume exceeds the producers' — big sinkers down where the detritus is (needs the per-guild volume read; if the report lacks it, computed from the run's lineage + genomes post hoc and labelled as such) | lineage / snapshots |
| W5 | **chains arrive** (first inherited absorptive birth) in ≥ 3 of 5 seeds, now that droughts are survivable and the larder is reachable by growth | `inherit`, lineage |
| W6 | **the round's answer**: ≥ 3 of 5 seeds pass D063 | the scoring table |

## The two-sided readings, before the answer

- **W1–W2 hold, W6 passes:** the goal is met in the discovery regime; the frontier moves to
  movement, where the argument is already written — a swimmer pays for depth only when it
  needs it, which is cheaper than the float this round makes evolution buy.
- **W1–W2 hold, W5 fails:** the drowning is cured and arrival is *still* gated — by
  something unmeasured; the diagnosis reopens at the mutant's first day, not at the world.
- **W1–W2 hold, W5 holds, W6 fails:** chains arrive into a world that no longer drowns and
  bust anyway — the cohort trap ([0043](0043-the-transplant.md)) is back on the table on its
  own, cleanly separated from the drowning for the first time.
- **W2 fails by runaway (≥ 3 seeds ceiling-censored):** the named risk — a world of neutral
  floaters blooms permanently; canopy shading did not self-limit; the light economy is the
  next conversation, and V0 goes *down*, not up.
- **W1 fails:** the rule did not reach the physics (check V2 first), or size-dependence at
  this dose is too weak to hold a drought — then V0 = 0.44.

## Launch

Five arms, at most 5 concurrent machine-wide. `r10-s1..s4` go on the refreshed workers 4–7
at once, because round 9's two stragglers occupy workers 2–3 and are read-only to this
round. `r10-s5` follows as the first straggler ends. Headers are verified against this table
before any arm is believed. Results are appended below.

## Round 10 stopped early, and round 10b runs at area 100 (2026-09-02, owner's ruling)

Here is what the first 4,000 s said. V1 and V2 both passed, with headers exact and every arm
diverging from its round-6 twin at t=100. Then the named risk arrived at once in all five
seeds.

Mean depth ran −1.2 to −2.4 m, against −20 m in every earlier world, so the rule reached the
physics decisively and the population became a surface film. Populations reached 3,900–5,300
at t≈3,400–4,600, where round-6 controls held 1,000–1,600, with 300,000–800,000 refused
conceptions per sample. On that trajectory every seed hits the 8,000 ceiling by
t≈6,000–7,000 and the round censors itself. That is the reading pre-registered above as W2
failing by runaway.

The owner read it and ruled. The ceiling is an instrument limit rather than a world outcome,
so exceeding it is not wrong, only unaffordable, because throughput is population. The world
has real limits, in light through shading and in matter through D048, but no mortality other
than starvation and age, so a bloom overshoots them. At area 400 the overshoot sits above
what the machine can simulate.

Area is the carrying capacity and the only thing that sets one (DESIGN.md §5A.2b). The same
world at 100 m² should hold a quarter of the bodies for the same per-square-metre dynamics.
The ceiling then becomes 32,000-equivalent, and the arms run about four times faster. Round
5b rejected small dishes (logbook/0040) for shading-driven darkness and irreversible
sinking, under the old physics where every body sank. D064 removes that, so the objection is
retested rather than deferred to. The owner put it plainly: *"if we can achieve our goal in
a smaller aquarium, we can always make it bigger later."*

Round 10 is censored early by this ruling. The five arms were stopped at t≈4,200–4,900.
Their reports carry no footer, every completed row stands, and they are read as the runaway
signal above rather than as outcomes. Scored 0 of 5, censored.

Round 10b follows from it. `r10b-s1..5` is the package as in the table above with one
change, `EVOSIM_AREA` at 100 where it was 400. Founders stay at 40, so founding density is
4×, and the column stays 60 m deep, so the deep volume shrinks with the area. The dose and
all other knobs are unchanged: V0 0.25, founder depth 60, mutation 0.005 and no refuge.

There is no bit-identical control at area 100, because the area binds at t=0. The comparison
set is round 6's world per square metre, plus round 5b's area-100 arms as the old-physics
reference. V1 checks that the headers carry `area 100 m2` and the round-10 tokens. V3 and V4
are unchanged, and V2 is replaced by a t=100 diff against `r10-sN`, which must differ, since
the area binds from the first step.

Predictions W1–W6 carry over unchanged, with one addition. W7 says the bloom equilibrates
below the ceiling. At least 3 of 5 seeds reach budget without runaway, with `alive`
levelling where canopy closure sets it rather than where the instrument does. If W7 fails at
area 100, the bloom is not a question of dish size, and the light economy is the next
conversation. Same budget, wall, ceiling and scoring rule.

Six arms run concurrently for a few hours, since r9-s5 is still finishing on worker 3. The
five 10b worlds are a quarter the size, so the load is below round 8's.

## Round 10b: no dish is small enough (2026-09-02)

W7 failed at area 100. V1 held, with `area 100 m2` beside the D064 tokens and one new hash
`d1e4670ad3928db0`. The five worlds were surface films again, at −1 to −3 m. The matter cap
arrived on schedule, with 85–90% of the 6,000 units locked in bodies by t≈4,000. What the
smaller dish could not change is the divisor. Here is mean matter per body, every 1,000 s:

| arm | 1k | 2k | 3k | 4k | 5k | 6k |
|---|---|---|---|---|---|---|
| s1 | 13.5 | 5.9 | 3.5 | 2.2 | | |
| s2 | 14.4 | 6.2 | 3.5 | 2.3 | | |
| s3 | 9.6 | 3.8 | 2.2 | 1.6 | | |
| s4 | 16.1 | 8.7 | 5.7 | 4.2 | 3.3 | 2.7 |
| s5 | 14.0 | 8.0 | 4.5 | 2.9 | 2.1 | |

The figure halves every ~1,100 s in every seed, with no floor in sight. The count is 6,000
divided by that divisor, and it climbed past 3,000 at t≈5,000 with the bloom still
accelerating. Round 10 at area 400 had shown the same ratchet, from 25 to 4.1 units per body
over 3,200 s, so the two dish sizes agree. D064 works, and the strategy it makes free is to
get small.

The world is reproducing real biology, since picoplankton dominate nutrient-starved surface
water for the same surface-to-volume reason, and the computer cannot afford it. W7 is
falsified. The arms were stopped at the launch of 10c and are read to their last row:

| arm | last row | alive | units per body |
|---|---|---|---|
| s1 | t=5,200 | 3,681 | 1.44 |
| s2 | t=5,200 | 3,412 | 1.58 |
| s3 | t=4,500 | 3,804 | 1.37 |
| s4 | t=6,700 | 2,280 | 2.43 |
| s5 | t=5,900 | 3,395 | 1.59 |

They are scored censored, 0 of 5. Chains were singletons only, with no inherited birth in
any arm.

## Round 10c: the fixed matter cost (D065), pre-registered before launch

The owner ruled on the agent's recommendation: a body costs a minimum of matter to exist.
`r10c-s1..5` is the 10b configuration plus `EVOSIM_MATTER_PER_CREATURE` **3**.

The dose arithmetic, stated first. Six thousand units, divided by 3 plus a proportional
share now at 1.6–2.7, caps the count near 1,100–1,400 bodies. That holds whatever the bodies
weigh, against the 8,000 ceiling. The bloom should level where matter runs out rather than
where the instrument does.

V1 checks that headers carry `matter 0.5/J + 3 each from 1/m3` beside the 10b tokens. V2
requires a difference from `r10b-sN` at the first conception, since the fixed term binds at
the first child rather than at t=0. A token-identical prefix through founding is therefore
expected, and is not a failure. V3 and V4 are as above.

Predictions W1–W6 carry over. W7 is re-posed as W7′. The count levels below the ceiling in ≥
3 of 5 seeds, at or under ~1,500, with matter per body no longer the thing that sets it. One
more is added, because this is the first world where a bloom can end without killing
everyone. W8 says the first drought resolves by turnover rather than by drowning. After the
matter cap binds, births resume within one senescence time (10,000 s) as deaths return
matter, with `depth m` unchanged. If W7′ fails at a fixed cost of 3, the proportional share
was not the divisor's problem, and the diagnosis reopens at the conception price itself.
Same budget, wall, ceiling, seeds and scoring rule.

## Round 10c results (2026-09-02, all five arms at budget)

The score is 0 of 5 passes under D063. Five of five producer worlds reached the budget alive
and uncensored, which no round in this campaign had done before. V1 held, with one hash,
`7a4a0816d6975a3f`, and every token exact.

| arm | ending | alive at end (= peak) | births / deaths | absorptive max / inherited max | depth range t ≥ 6,000 | det deep at end | mean age at end |
|---|---|---|---|---|---|---|---|
| `r10c-s1` | budget | 1,580 | 2,080 / 595 | 8 / 0 | −5.7 … −2.6 m | 6.7 | 15,543 s |
| `r10c-s2` | budget | 1,510 | 2,301 / 848 | 12 / 0 | −4.5 … −2.1 | 5.7 | 14,306 |
| `r10c-s3` | budget | 1,493 | 2,137 / 709 | 16 / 1 | −7.8 … −5.7 | 6.1 | 14,728 |
| `r10c-s4` | budget | 1,491 | 2,066 / 692 | 12 / 1 | −3.2 … −0.9 | 8.1 | 15,406 |
| `r10c-s5` | budget | 1,607 | 2,747 / 1,245 | 10 / 0 | −3.5 … −0.9 | 8.9 | 13,546 |

- W1 held, five of five. The largest depth excursion after the matter cap bound was ~3 m. The
  drowning that killed three worlds in rounds 6 to 9 within 1,500 s of a birth pause did not
  occur in 24,000 s of throttled births. The rule in D064 cured that death.
- W2 held, five of five. Producers persisted to budget in every seed, uncensored.
- W3 is falsified. Float share at t=20,000 ran 0.5%, 0, 0.2%, 0 and 0. The prediction assumed
  bodies would *grow* and need lift. The world went the other way, and a body under V0 is
  neutral for free, so float is redundant rather than insurance. That is the correct outcome
  of the rule and a wrong prediction about which side of V0 selection would visit.
- W4 is moot. No absorptive guild established to be measured.
- W5 is falsified. One inherited absorptive individual appeared once in s3 and once in s4,
  with no lineage. There were singleton mutants in every seed, all run, all at the surface.
- W6 is falsified, with zero passes.
- W7′ held, with a creep. Counts levelled at 1,490–1,610 by the budget, under the ceiling by
  a factor of five, uncensored, where the matter arithmetic put them. They were still rising
  slowly at the end, because the proportional share of the price kept falling toward the
  fixed floor, from 6 to 3.7 units per body. The fixed cost does what it was built for, and
  the plateau is the floor's rather than the dish's.
- W8 held, five of five, in a weaker form than written. Births never fully stopped after the
  cap. They ran at ~50–90 per 1,000 s on turnover, with depth unchanged. The drought
  resolved by turnover, and the turnover was a drizzle. There were 595–1,245 deaths in
  30,000 s, and a mean age of 13,500–15,500 s at the end. A tiny body at the surface
  out-earns its wear.

The reading from the pre-registered list is the second one, where W1 and W2 hold and W5
fails. The drowning is cured and arrival is still gated, and this time the gate is
measurable. Two gates show in the table. The first is rate. The larder crossed the ~7 J/m³
breeding bar in two seeds only at the very end, at 8.1 in s4 and 8.9 in s5. Deaths were too
few to fill it any sooner. The second is location. In those two seeds the food was above the
bar and no chain came. The mutants are born small at the surface, where detritus reads 0.07
J/m³, and a body under V0 cannot sink to where the food is. Round 11
([0047](0047-the-half-life.md)) pulls the rate lever and leaves the location gate to be read
from its result.

Here is what this round is. For the first time, in every seed and for a full budget, the
world's producers were a stable, bounded, self-regulating population. There was no drowning,
no runaway, no floor and no wall. The goal's first clause is met by construction now, and
the whole remaining problem is the second trophic level.
