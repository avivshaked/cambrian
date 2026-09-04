# 0054 — The confirmation

*2026-09-04. Pre-registered before launch. D070's confirmation round: the leak at the
confirmation step, five seeds, scored under the goal rule as written.*

## Why this round exists

The screen (0053) passed on both seeds: with producers releasing 15% of their light
intake into the water, the world's stomachs held inherited lines of 46 and 133 at
20,000 s, never below ten after reaching it, while the control on the same build held
none. That was at the 0.02 screening step and for 20,000 s. D063 as amended scores a
full run — 30,000 s — and every run before 0052 was at 0.01, which stays the
confirmation step (0052's verdict). So this is the round that can pass: the first since
D063 was set in which the mechanism has already been seen to work.

## The arms

Five seeds, one arm each, dt 0.01, 30,000 s, round 14's clearance-10 world plus
`EVOSIM_EXUDATION 0.15`: `r18x-s1` … `r18x-s5` on w2–w7 as workers free (≤ 5 concurrent).
Launcher `scratch/launch-r18.ps1`. All workers refreshed to the build that carries the
exudation knob, the flux columns and the absorptive log (`absorptive.jsonl`), and
hash-checked. Seeds 1 and 2 are the screen's seeds at the finer step — different
realisations, per 0052, so the screen's numbers are a distribution to compare against,
not rows.

No sequential rule and no futility stop this time: the mechanism is established and the
question is the count, so all five run to budget. Wall clock: 30,000 s at 0.01 with five
arms sharing the machine is 5–6 h each.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `exudation 0.15`, `dt=0.01`, `clearance 10`, `sink 0.002 m/s, matter 0.002 m/s`, `vent off`; every other token equals round 14's | header line 3 |
| V2 | `floor` = 0 after t=3,100 | `floor` |
| V3 | audit 0.0000% every sample; `det in + det exuded − det out` summed equals `detritus J` to the rounding | `audit`, the flux columns |
| V4 | `absorptive.jsonl` is written and `abs logged` matches its row count per sample (the log's first scored round) | the run directory |

## Scoring

D063 as amended, per seed: producers persist to the end · an absorptive lineage is
inherited for ≥ 20 consecutive samples · it is still alive (≥ 10 individuals) at the last
sample · at least one absorptive birth within the last 20 samples (from `lineage.jsonl`).
A pass at cell-type mutation 0.005 is a discovery-regime pass and is labelled so. The
round passes if ≥ 3 of 5 seeds pass.

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

- **M1 holds:** the goal is met, in the discovery regime, and DESIGN.md takes exudation
  as a world rule with the review's fraction and caveats (D070). The frontier moves to
  0049's reading, one lever later: movement, in water that moves and feeds — and to the
  questions the pass leaves open: whether a *late* stomach can invade (the assay at 0.15,
  run two lifetimes past inoculation), and where the line's size stops tracking the leak
  (the bracket 0.05 / 0.13).
- **M1 fails with M3–M4 holding:** the leak is there and eaten and the lines still fall
  short at 0.01 — the finer step changes something the screen did not see (the film, the
  sink, contact). Read `absorptive.jsonl` first: it says where every stomach was and what
  it earned, which 0050's dissection could not.
- **M2 fails (mutant-rooted lines):** a stronger result than predicted — the leak lets a
  late stomach invade as well as keeping the founders' alive.
- **M5 fails:** the 15% tax binds at the finer step; the bracket downward.
- **M6 fails:** the log and the ledger disagree on what a stomach earns in this water —
  the instrument or the ledger has a term wrong, and that is settled before anything is
  concluded from either.

## Launch

Five arms, workers refreshed to the current build and hash-checked, the monitor's watch
list carrying them. Headers verified before any arm is believed. Results appended below.

## Results

*2026-09-04, five arms to budget at dt 0.01 (128–304 min wall, 1.6–3.9× real time). V1
held on every header; V2 — `floor` 0 from 3,100 in all five; V3 — audit 0.0000% at every
row of every arm, and `det in + det exuded − det out` summed equals the final `detritus J`
to the tenth of a joule in all five; V4 — `absorptive.jsonl` written in every run and
`abs logged` equals its rows per sample (the log's first scored round).*

**The round passes: 4 of 5.** Scored under D063 as amended — producers persist · an
absorptive line inherited ≥ 20 consecutive samples · ≥ 10 alive at the last sample · an
absorptive birth in the last 20 samples — at cell-type mutation 0.005, so a
**discovery-regime pass**, labelled as such.

| seed | alive at 30,000 | inherited every sample from | ≥ 10 from | peak | at 30,000 | absorptive births in last 20 samples | lineage roots (founders / mutants) | clauses | verdict |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 1,834 | t=2,400 (277) | 3,400 | 58 (t=8,800) | **4** | 0 | 47 / 3 | ✓ ✓ ✗ ✗ | fail |
| 2 | 1,722 | t=300 (298) | 800 | 128 (t=25,100) | **91** | 23 | 17 / 5 | ✓ ✓ ✓ ✓ | **pass** |
| 3 | 1,818 | t=800 (293) | 2,100 | 171 (t=13,100) | **103** | 13 | 25 / 4 | ✓ ✓ ✓ ✓ | **pass** |
| 4 | 1,828 | t=1,400 (287) | 2,400 | 76 (t=30,000) | **76** | 21 | 37 / 4 | ✓ ✓ ✓ ✓ | **pass** |
| 5 | 1,490 | t=2,400 (277) | 4,600 | 345 (t=28,500) | **221** | 133 | 69 / 1 | ✓ ✓ ✓ ✓ | **pass** |

The flux and the field, t > 10,000, and the stomachs' own ledger at t=20,000 from
`absorptive.jsonl`:

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

**What the failing seed says, and it is the round's finding.** Seed 1's line peaked at
58, and its last seven stomachs at t=20,000 sat at −15 m in 13.7 J/m³ — the richest water
of the round — earning +0.08 W each, with **537 J in reserve, four times a child's
price, and no children**. They were not starving; they were blocked. Every mature world
in this round is matter-starved: from t≈15,000, conceptions refused for want of matter run
at 100,000–290,000 per 100-s window in every seed, matter locked in bodies sits at ~5,500
units, and free matter in the water is a few hundredths of a unit per cubic metre against
a child's price of ~3.5 from the parent's own layer. A stomach at −15 m gets matter only
as dead bodies' matter sinks past it at 0.002 m/s, and the producers above re-lock what
their dead release before it goes. Across the five seeds the line's size at the end
tracks the free matter at depth in the second half of the run: 0.2–0.55 units/m³ in seed
5 (221), 0.1–0.2 in seed 3 (103), 0.15 in seed 2 (91), 0.08–0.1 in seeds 4 (76) and 1 (4).
That is a correlation over five seeds and an inferred mechanism — the block counter is not
per guild — but the absorptive log closes the gap: the failing seed's stomachs held full
reserves in full water and did not breed, which only a refused conception explains.

**What was confirmed.** With producers releasing 15% of their light intake into the
water, this world holds a second trophic level: inherited lines of 76–221 stomachs at the
end of a full 30,000-s run in four seeds of five, every one of them tracing to the
founding lottery's stomachs that the leak kept alive, all four still recruiting in the
last 20 samples, with the audit closed and the producers at 1,500–1,800. The energy
income was the constraint (0050, D070), and the leak lifted it; the stomachs eat 74–103%
of what lands. The standing field is not the measure of the second level's income and
never was — seeds 1 and 4 had the richest fields and the smallest lines.

**What the pass is not.** Not a mutant invasion: every line is founder-descended (M2),
and whether a *late* stomach can now establish is the invasion assay at 0.15, run two
lifetimes past inoculation (0051's instrument note). Not a pass outside the discovery
regime: cell-type mutation was 0.005. And not the end of the constraints: the matter
economy at depth is the next one, and its levers exist as knobs — matter excretion
(`EVOSIM_EXCRETION`, 0.01 all campaign), the matter price of a child, the matter sink
rate — which are world rules and the owner's.

## Verdict

**The goal rule (D063 as amended) is met: 4 of 5 seeds, discovery regime, 2026-09-04.**
Exudation at 0.15 becomes a world rule (D070 confirmed; DESIGN.md §5A.2c). The frontier
moves to matter at depth — a proposal for the owner follows — and, past that, to 0049's
reading: movement, in water that moves and feeds.

### Addendum: scored again by connected clade (2026-09-04, after the Sol/GPT review of 2026-09-03)

The review points out that D063's wording scores an aggregate — the `inherit` column
counts every living absorptive creature whose parent expressed the trait, whatever clade
it belongs to, and the recruitment clause accepts any absorptive birth — so a set of
unrelated short-lived clades could add up to a streak, and an unrelated late mutant could
satisfy recruitment for a sterile cohort. That is right. So the round was scored a second
time by **connected clade**: a clade begins at an absorptive birth whose parent did not
express the trait (or at an absorptive founder), membership follows the parent chain
while the trait is inherited, and the three clauses are asked of *one* clade — alive for
≥ 20 consecutive samples to the end, ≥ 10 living members at the last sample, an inherited
absorptive birth within the last 20 samples inside that clade (`scratch/clade-score.py`,
from `lineage.jsonl`'s birth and death events).

| seed | clades with a living member at 30,000 | the largest clade: root, born | members ever | alive at end | alive-streak (samples) | ≥ 10 from | inherited births in last 20 samples | clade verdict |
|---|---|---|---|---|---|---|---|---|
| 1 | 3 | **mutant** 549, t=4,413 | 79 | 2 | 256 | 5,700 | 0 | fail |
| 2 | 3 | founder 31, t=8 | 287 | 48 | 300 | 800 | 9 | **pass** |
| 3 | 3 | founder 34, t=9 | 409 | 59 | 300 | 2,100 | 13 | **pass** |
| 4 | 1 | **mutant** 3936, t=16,265 | 79 | 77 | 138 | 22,100 | 21 | **pass** |
| 5 | 2 | founder 252, t=2,372 | 1,840 | 221 | 277 | 4,600 | 133 | **pass** |

**4 of 5 holds under the strict reading.** Two things the aggregate hid: seed 4's whole
line at the end is *one clade rooted in a mutant born at t=16,265* — a late invasion in a
mature world that reached 77 in 14,000 s — and seed 1's largest clade was mutant-rooted too;
so M2 ("the lines are founder-descended") is corrected at the clade level: three of the
four passing seeds are founder-rooted, one is a mutant's. The screen's arms (0053) pass by
clade as well (46 and 120 in one clade each), and the leak-world assay's largest inoculant
clade holds 19 alive at 20,000 s with 7 recent inherited births. Whether the clade reading
is D063's intended one is the owner's to ratify; both readings are now on record and agree.

*Ruled the same day: the connected-clade reading is D063's, with a stability clause (the
clade ≥ 10 through the last two lifetimes — minima here 48, 41, 24, 127) and the producers
scored as an inherited photosynthetic lineage. Round 18 stands as the first pass under the
amended rule, 4 of 5; the producer clause is recorded when the `photo` columns exist.*
