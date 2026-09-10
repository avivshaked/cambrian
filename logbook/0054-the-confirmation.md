# 0054 — The confirmation

**2026-09-04**  ·  food-chain goal, round 18 · pre-registered before launch, results appended
after

This is the round that met the goal. Four seeds of five ended a full run with an inherited
line of stomachs 76 to 221 strong, still breeding in the last twenty samples. The world they
did it in is one where producers leak 15% of the light they fix into the water. The fifth
seed had the richest water of the round and the smallest line, and understanding why gave
the round its finding. Those stomachs were not hungry. They were blocked, holding four times
a child's price in reserve with no matter to build one from.

The round is D070's confirmation: the leak at the confirmation step, over five seeds, scored
under the goal rule as written.

## Why this round exists

The screen (0053) passed on both seeds. Producers released 15% of their light intake into
the water. The world's stomachs then held inherited lines of 46 and 133 at 20,000 s, never
below ten after reaching it. The control on the same build held none.

That was at the 0.02 screening step and for 20,000 s. D063 as amended scores a full run of
30,000 s, and every run before 0052 was at 0.01, which stays the confirmation step by 0052's
verdict. So this is the round that can pass, and the first since D063 was set in which the
mechanism has already been seen to work.

## The arms

Five seeds, one arm each, at dt 0.01 for 30,000 s, in round 14's clearance-10 world plus
`EVOSIM_EXUDATION 0.15`.

The arms are `r18x-s1` to `r18x-s5`, on w2 to w7 as workers free, at most 5 concurrent.

The launcher is `rounds/launch-r18.ps1`. All workers are refreshed to the build that
carries the exudation knob, the flux columns and the absorptive log, and hash-checked.

Seeds 1 and 2 are the screen's seeds at the finer step. They are different
realisations, per 0052, so the screen's numbers are a distribution to compare against rather
than rows.

There is no sequential rule and no futility stop this time. The mechanism is established and
the question is the count, so all five run to budget. On wall clock, 30,000 s at 0.01 with
five arms sharing the machine is 5 to 6 hours each.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `exudation 0.15`, `dt=0.01`, `clearance 10`, `sink 0.002 m/s, matter 0.002 m/s`, `vent off`; every other token equals round 14's | header line 3 |
| V2 | `floor` = 0 after t=3,100 | `floor` |
| V3 | audit 0.0000% every sample; `det in + det exuded − det out` summed equals `detritus J` to the rounding | `audit`, the flux columns |
| V4 | `absorptive.jsonl` is written and `abs logged` matches its row count per sample (the log's first scored round) | the run directory |

## Scoring

D063 as amended, per seed. Producers persist to the end. An absorptive lineage is inherited
for ≥ 20 consecutive samples. It is still alive, at ≥ 10 individuals, at the last sample.
And there is at least one absorptive birth within the last 20 samples, read from
`lineage.jsonl`.

A pass at cell-type mutation 0.005 is a discovery-regime pass and is labelled so. The round
passes if ≥ 3 of 5 seeds pass.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the round passes**: ≥ 3 of 5 seeds meet all four clauses | the scoring table |
| M2 | **the lines are founder-descended**, as in the screen: in every seed with a line, the majority of absorptive lineage roots are floor-era founders, not mutants | `lineage.jsonl` |
| M3 | **the leak is the income** in every seed: `det exuded` ≥ 10× `det in` at t > 10,000 | the flux columns |
| M4 | **the stomachs eat what lands**: `det out` within 30% of `det exuded` at t > 10,000, and the field at the population's depth below 2 J/m³ | the flux and field columns |
| M5 | **producers persist**: `alive` ≥ 1,000 at the end in every seed; no ceiling | `alive`, `**Ended:**` |
| M6 | **the absorptive log agrees with the ledger**: for the stomachs alive at t=20,000, mean `netW` from `absorptive.jsonl` is positive, and mean `densityHere` is within a factor of two of `J/m3 here` at that sample | `scripts/absorptive-log.ps1` |

## The two-sided readings

- If M1 holds, the goal is met in the discovery regime, and DESIGN.md takes exudation as a
  world rule with the review's fraction and caveats (D070). The frontier moves to 0049's
  reading, one lever later: movement, in water that moves and feeds. It also moves to the
  questions the pass leaves open. One is whether a *late* stomach can invade, which the
  assay at 0.15 tests, run two lifetimes past inoculation. The other is where the line's
  size stops tracking the leak, which the bracket 0.05 / 0.13 tests.

- If M1 fails with M3 and M4 holding, the leak is there and eaten and the lines still fall
  short at 0.01. The finer step then changes something the screen did not see, in the film,
  the sink or contact. Read `absorptive.jsonl` first, since it says where every stomach was
  and what it earned, which 0050's dissection could not.

- If M2 fails and the lines are mutant-rooted, that is a stronger result than predicted: the
  leak lets a late stomach invade as well as keeping the founders' alive.

- If M5 fails, the 15% tax binds at the finer step, and the bracket goes downward.

- If M6 fails, the log and the ledger disagree on what a stomach earns in this water. Either
  the instrument or the ledger has a term wrong, and that is settled before anything is
  concluded from either.

## Launch

Five arms, with workers refreshed to the current build and hash-checked, and the monitor's
watch list carrying them. Headers are verified before any arm is believed, and results are
appended below.

## Results

Five arms reached budget on 2026-09-04 at dt 0.01, in 128–304 min of wall clock at 1.6–3.9×
real time.

V1 held on every header, and V2 held with `floor` 0 from 3,100 in all five.

V3 held as well, at 0.0000% every row of every arm. Summed over the run,
`det in + det exuded − det out` equals the final `detritus J` to the tenth of a joule in all
five.

V4 held too, since `absorptive.jsonl` was written in every run and `abs logged` equals its
rows per sample, in the log's first scored round.

The round passes, 4 of 5. It is scored under D063 as amended. Producers persist, an
absorptive line is inherited for ≥ 20 consecutive samples, and ≥ 10 of it are alive at the
last sample. There is an absorptive birth in the last 20 samples. Cell-type mutation was
0.005, so this is a discovery-regime pass, labelled as such.

| seed | alive at 30,000 | inherited every sample from | ≥ 10 from | peak | at 30,000 | absorptive births in last 20 samples | lineage roots (founders / mutants) | clauses | verdict |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 1,834 | t=2,400 (277) | 3,400 | 58 (t=8,800) | **4** | 0 | 47 / 3 | ✓ ✓ ✗ ✗ | fail |
| 2 | 1,722 | t=300 (298) | 800 | 128 (t=25,100) | **91** | 23 | 17 / 5 | ✓ ✓ ✓ ✓ | **pass** |
| 3 | 1,818 | t=800 (293) | 2,100 | 171 (t=13,100) | **103** | 13 | 25 / 4 | ✓ ✓ ✓ ✓ | **pass** |
| 4 | 1,828 | t=1,400 (287) | 2,400 | 76 (t=30,000) | **76** | 21 | 37 / 4 | ✓ ✓ ✓ ✓ | **pass** |
| 5 | 1,490 | t=2,400 (277) | 4,600 | 345 (t=28,500) | **221** | 133 | 69 / 1 | ✓ ✓ ✓ ✓ | **pass** |

Here are the flux and the field at t > 10,000, with the stomachs' own ledger at t=20,000
from `absorptive.jsonl`:

| seed | corpses (W) | leak (W) | eaten (W) | `J/m3 here` (producers' depth) | producers' depth | stomachs' depth | density the stomachs saw | stomachs' mean net (W) | stomachs' mean reserve (J) |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 0.38 | 12.2 | 9.1 | 13.6 | −14.3 | −15.2 | 13.7 | +0.079 | **537** |
| 2 | 0.55 | 9.6 | 10.0 | 2.4 | −13.2 | −14.9 | 3.2 | +0.040 | 114 |
| 3 | 0.15 | 19.3 | 18.8 | 1.3 | +0.4 | −5.5 | 5.2 | +0.009 | 302 |
| 4 | 0.30 | 16.7 | 14.0 | 13.6 | −12.4 | −14.1 | 17.5 | +0.058 | **445** |
| 5 | 0.88 | 28.6 | 29.6 | 9.7 | −2.6 | −15.1 | 0.9 | +0.008 | 82 |

| # | prediction | verdict |
|---|---|---|
| M1 | the round passes, ≥ 3 of 5 | **held — 4 of 5** |
| M2 | the lines are founder-descended | **held in all five** (17–69 founder roots against 1–5 mutant roots) |
| M3 | the leak is the income, ≥ 10× the corpses | **held in all five** (17× to 129×) |
| M4 | the stomachs eat what lands (within 30%) and the field stays below 2 J/m³ | **first half held in all five** (74–103% of the leak eaten); **second half falsified in four** — and the reason is the finding below: where the line is small the leak piles up uneaten (13.6 J/m³ in seeds 1 and 4), and `J/m3 here` reads the producers' depth, which in seeds 3 and 5 is 6–13 m above the stomachs |
| M5 | producers persist ≥ 1,000, no ceiling | **held in all five** (1,490–1,834; max 1,848) |
| M6 | the log agrees with the ledger: stomachs' mean net positive; density seen within 2× of `J/m3 here` | **first half held in all five** (+0.008 to +0.079 W); **second half held in three** and failed in seeds 3 and 5 by 33× and 2.0×, where the two guilds live at different depths — the report's field column is the producers' and cannot stand in for the stomachs'. The log is the instrument for the second level from here |

The failing seed says the most, and it is the round's finding. Seed 1's line peaked at 58.
Its last seven stomachs at t=20,000 sat at −15 m in 13.7 J/m³, the richest water of the
round, earning +0.08 W each. They held **537 J in reserve, four times a child's price, and
had no children**. They were not starving. They were blocked.

Every mature world in this round is matter-starved. From t≈15,000, conceptions refused for
want of matter run at 100,000–290,000 per 100-s window in every seed. Matter locked in
bodies sits at ~5,500 units. Free matter in the water is a few hundredths of a unit per
cubic metre, against a child's price of ~3.5 from the parent's own layer.

A stomach at −15 m gets matter only as dead bodies' matter sinks past it at 0.002 m/s. The
producers above re-lock what their dead release before it gets there. Across the five seeds
the line's size at the end tracks the free matter at depth in the second half of the run.
Seed 5 had 0.2–0.55 units/m³ and 221 alive, seed 3 had 0.1–0.2 and 103, and seed 2 had 0.15
and 91. Seeds 4 and 1 had 0.08–0.1, with 76 and 4.

That is a correlation over five seeds and an inferred mechanism, since the block counter is
not per guild. The absorptive log closes the gap: the failing seed's stomachs held full
reserves in full water and did not breed, which only a refused conception explains.

Here is what was confirmed. With producers releasing 15% of their light intake into the
water, this world holds a second trophic level. There were inherited lines of 76–221
stomachs at the end of a full 30,000-s run, in four seeds of five. Every one of them traces
to the founding lottery's stomachs that the leak kept alive. All four were still recruiting
in the last 20 samples, with the audit closed and the producers at 1,500–1,800.

The energy income was the constraint (0050, D070), and the leak lifted it. The stomachs eat
74–103% of what lands. The standing field is not the measure of the second level's income
and never was, since seeds 1 and 4 had the richest fields and the smallest lines.

Here is what the pass is not. It is not a mutant invasion, since every line is
founder-descended (M2). Whether a *late* stomach can now establish is the invasion assay at
0.15, run two lifetimes past inoculation, per 0051's instrument note. It is not a pass
outside the discovery regime, since cell-type mutation was 0.005.

It is also not the end of the constraints. The matter economy at depth is the next one, and
its levers exist as knobs. They are matter excretion (`EVOSIM_EXCRETION`, at 0.01 all
campaign), the matter price of a child, and the matter sink rate. Those are world rules and
the owner's.

## Verdict

The goal rule, D063 as amended, **is met**. That is 4 of 5 seeds, in the discovery regime,
on 2026-09-04. Exudation at 0.15 becomes a world rule, with D070 confirmed and DESIGN.md
§5A.2c taking it. The frontier moves to matter at depth, with a proposal for the owner to
follow, and past that to 0049's reading: movement, in water that moves and feeds.

### Addendum: scored again by connected clade (2026-09-04, after the Sol/GPT review of 2026-09-03)

The review points out that D063's wording scores an aggregate. The `inherit` column counts
every living absorptive creature whose parent expressed the trait, whatever clade it belongs
to, and the recruitment clause accepts any absorptive birth. So a set of unrelated
short-lived clades could add up to a streak, and an unrelated late mutant could satisfy
recruitment for a sterile cohort. That is right.

So the round was scored a second time by **connected clade**. A clade begins at an
absorptive birth whose parent did not express the trait, or at an absorptive founder.
Membership follows the parent chain while the trait is inherited. The three clauses are then
asked of *one* clade. It must be alive for ≥ 20 consecutive samples to the end and hold ≥ 10
living members at the last sample. It must also show an inherited absorptive birth within
the last 20 samples, inside that clade. The scorer is `scripts/reads/clade-score.py`, reading
`lineage.jsonl`'s birth and death events.

| seed | clades with a living member at 30,000 | the largest clade: root, born | members ever | alive at end | alive-streak (samples) | ≥ 10 from | inherited births in last 20 samples | clade verdict |
|---|---|---|---|---|---|---|---|---|
| 1 | 3 | **mutant** 549, t=4,413 | 79 | 2 | 256 | 5,700 | 0 | fail |
| 2 | 3 | founder 31, t=8 | 287 | 48 | 300 | 800 | 9 | **pass** |
| 3 | 3 | founder 34, t=9 | 409 | 59 | 300 | 2,100 | 13 | **pass** |
| 4 | 1 | **mutant** 3936, t=16,265 | 79 | 77 | 138 | 22,100 | 21 | **pass** |
| 5 | 2 | founder 252, t=2,372 | 1,840 | 221 | 277 | 4,600 | 133 | **pass** |

4 of 5 holds under the strict reading. Two things the aggregate hid. Seed 4's whole line at
the end is one clade, rooted in a mutant born at t=16,265. That is a late invasion in a
mature world, and it reached 77 in 14,000 s. Seed 1's largest clade was mutant-rooted too.

So M2, that the lines are founder-descended, is corrected at the clade level: three of the
four passing seeds are founder-rooted, and one is a mutant's. The screen's arms (0053) pass
by clade as well, at 46 and 120 in one clade each. The leak-world assay's largest inoculant
clade holds 19 alive at 20,000 s, with 7 recent inherited births.

Whether the clade reading is D063's intended one is the owner's to ratify. Both readings are
now on record and they agree.

The owner ruled the same day. The connected-clade reading is D063's, with a stability clause
and with the producers scored as an inherited photosynthetic lineage. The stability clause
asks that the clade hold ≥ 10 through the last two lifetimes, and the minima here are 48,
41, 24 and 127. Round 18 stands as the first pass under the amended rule, 4 of 5, and the
producer clause is recorded when the `photo` columns exist.
