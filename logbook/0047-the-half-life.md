# 0047 — The half-life

*2026-09-02. Pre-registered before launch; results appended after. Round 11: one
treatment × five seeds, the owner's ruling on the agent's recommendation, written while
round 10c ([0046](0046-the-archean-package.md)) is still running and before it is scored.*

## Where round 10c left the patient

At t≈16,000–19,000, all five 10c worlds were alive, uncensored and holding the light —
mean depth −2 to −7 m thirteen thousand seconds after the matter cap bound, the state
that drowned every earlier world within 1,500 s. Population levelled at ~1,050–1,300, where
the matter arithmetic put it. What did not happen was the chain. The reason is rate:
senescence at 10,000 s is a *linear* wear — upkeep × (1 + age / 10,000) — and a tiny body
at the surface earns enough light to carry two or three times its base upkeep, so it lives
to age ~20,000 s. Mean age passed 11,000; deaths ran at ~2% of the population per 1,000 s;
the detritus rain was a drizzle and the deep larder crept from 4.7 to 5.9 J/m³ over 13,000 s,
on course to cross the absorptive breeding bar (~7 for the known genotype) at about the
budget. Absorptive singletons survived 9,000 s at the surface without one birth: food
50 m below, unreachable by a body too small to sink.

Two gates, named before the data: **rate** (the larder fills too slowly) and **location**
(the larder is deep; the mutants are shallow). This round pulls the rate lever only.

## The treatment

| arms | knobs (all others: round 10c exactly) | mechanism bet |
|---|---|---|
| `r11-s1..5` | `EVOSIM_SENESCENCE` **3000** (was 10,000) | the same creature with a 3× light margin dies at age ~6,000 s instead of ~20,000: five or six turnovers per run instead of one; three times the detritus rain, three times the matter returned, three times the generations selection gets |

**Dose arithmetic, stated first.** Wear = 1 + age/3,000: ×2 at 3,000 s, ×4 at 9,000, ×11
at the budget. 3,000 is the value rounds 1–3 ran with (raised to 10,000 in round 4 to spare
producers a death that was the drowning in disguise, logbook/0039), so its founding
behaviour is on record: founders breed before they wear out. If turnover scales as
predicted, the deep larder reaches ~7 J/m³ by t≈10,000 rather than ≈30,000. The 10c arms
are the control — the same world at 10,000, run to budget.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `senescence 3000 s` and every 10c token unchanged | header line 3 |
| V2 | token-identical to `r10c-sN` through founding, diverging by the first sample where age matters (t ≤ 1,000) — a prefix beyond ~1,000 s means the knob did not reach the world | row diff vs `r10c-sN.md` |
| V3 | `floor` = 0 after t=3,100 | `floor` |
| V4 | monitor: 32-min content-growth stall rule, 90-s CPU+byte discriminator before any kill | monitor config |

## Predictions

Scored under D063 unchanged, recruitment clause from `lineage.jsonl` (exact); a pass is a
pass in the discovery regime (mutation 0.005) and is labelled so.

| # | prediction | falsified by |
|---|---|---|
| X1 | **turnover triples**: deaths per 1,000 s at t=10,000–20,000 are ≥ 2.5× round 10c's at the same age, and mean age at t=20,000 is under 5,000 s | `deaths`, `age s` |
| X2 | **the larder crosses the bar**: `det deep` ≥ 7 J/m³ by t=15,000 in ≥ 4 of 5 seeds | `det deep` |
| X3 | **no synchrony crash**: no seed drops below 25% of its plateau population; where a cohort dies together the world rebounds within 3,000 s with `depth m` unchanged (the round-3 age-synchrony death was the drowning; without it, a synchronous die-off is a matter pulse) | `alive`, `depth m` |
| X4 | **chains arrive** (first inherited absorptive birth) in ≥ 3 of 5 seeds | `inherit`, lineage |
| X5 | **the round's answer**: ≥ 3 of 5 seeds pass D063 | the scoring table |

## The two-sided readings

- **X1–X4 hold, X5 passes:** goal met (discovery regime); the frontier moves to movement.
- **X1–X2 hold, X4 fails:** the larder is full and the mutants still cannot reach it —
  location is the binding gate, and the next lever is detritus sink speed (by D064's own
  Stokes logic the detritus of tiny bodies should sink slowly and linger in the light), or
  an absorptive that can afford to be large.
- **X4 holds, X5 fails:** chains arrive into a world that neither drowns nor blooms and bust
  anyway — the cohort trap (0043) is back, isolated for the first time.
- **X3 fails:** the synchrony death is not drowning-mediated after all; dose down to 5,000.
- **X1 fails:** wear is not what limits lifespan here (check V2 first).

## Launch

Five arms on the workers 10c frees, in seed order; the 10c arms run to budget first — they
are this round's control and the campaign's first uncensored producer worlds. Headers
verified before any arm is believed. Results appended below.

---

## Results (2026-09-03): 0 of 5

All five arms reached budget with producers alive (1,573–1,767 at t=30,000), no drowning
(mean depth −0.3 to −3.5 m throughout), floor 0 after founding, audit 0.0000%. Headers
verified. No seed produced one inherited absorptive, so the recruitment clause was never
reached and the lineage files were not needed.

| # | prediction | r11 (s1–s5) | 10c control | verdict |
|---|---|---|---|---|
| X1 | deaths per 1,000 s at t=10,000–20,000 ≥ 2.5× 10c; mean age at 20,000 < 5,000 s | 56 / 55 / 104 / 67 / 49 (mean 66); age 8,352 / 8,845 / 7,227 / 7,811 / 9,344 | 22 / 37 / 30 / 27 / 56 (mean 34); age 8,422–11,777 | **falsified** on both clauses: turnover ~1.9×, not 2.5×; ages 7,200–9,300 |
| X2 | `det deep` ≥ 7 J/m³ by t=15,000 in ≥ 4 of 5 | 9.0 / 6.3 / 7.6 / 10.1 / 10.3 at 15,000 | 5.7 / 4.0 / 3.9 / 5.8 / 6.3 | **holds** (4 of 5) |
| X3 | no seed below 25% of its plateau | every seed grew monotonically from founding to budget | same | **holds**, vacuously — there was no cohort death to survive |
| X4 | first inherited absorptive in ≥ 3 of 5 | `inherit` 0 in all five for the whole run; 0–2 mutant absorptives alive at the end | 0 / 1 / 0 / 0 / 0 | **falsified** |
| X5 | ≥ 3 of 5 pass D063 | 0 of 5 | 0 of 5 | **falsified** |

**What the half-life did and did not do.** The dose reading was wrong in an instructive way:
wear = 1 + age/3,000 is a slope, not a cliff, and a creature whose light margin is 3× keeps
breeding at ×3 and ×4 wear. Mean age at t=20,000 came down from ~11,000 s to ~8,000, not to
under 5,000; turnover roughly doubled instead of tripling. That was enough for X2 — the larder
crossed the 7 J/m³ bar by t=15,000 in four seeds and stood at 7.9–12.7 at the end, against
5.7–8.9 in the control — and it changed nothing at the surface, where the mutants are born:
`J/m3 here` sat at 0.09–0.5 all round. The pre-registered reading for "X2 holds, X4 fails"
was the location gate, and the round measured it cleanly: the rate side of the chain is
open and the food is fifty metres below the only creatures that could eat it. D066
([0048](0048-stirring-the-pot.md)) is the response. The round's five arms are round 12's
still-water controls.
