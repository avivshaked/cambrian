# 0045 — The dose, and the dice

*2026-09-02. Pre-registered before launch; results appended after. Round 9: one treatment
× five seeds, fired by two pre-registered contingencies without new deliberation, per the
owner's standing priority (fastest credible pass, [HANDOFF](../HANDOFF.md)).*

## Why this round exists before round 8 finished

Round 8's verdict became formally determined mid-round, with three stragglers still
running: arm A went 0-for-4 by producer extinction (founding suppression, dose-generic),
arm B self-censored by runaway in every seed that ran clean, and arm C — after `r8c-s3`'s
whole-world crash at t=27,068.5 — can reach at most 1 of 5. No treatment can reach the
3-of-5 bar, so Y6 fails whatever the stragglers do, and both of 0044's pre-registered
contingencies fire:

1. **The dose.** `r8c-s1`'s lineage dissection ([0044](0044-three-medicines.md) results,
   forthcoming) showed the 0.2 meter *worked as a meter* — peak 317 vs the natural 908,
   recruitment sustained ~5,400 s — but the trap closed anyway at the **edible** density:
   the physical deep pool sat near 19 J/m³ while the edible fraction of it fell to
   ≈5.9 J/m³ at the clade's last birth, under the ≈7 J/m³ reproduction threshold. The
   fraction is the whole knob: the edible floor is `fraction × refuge J / area`, so 0.2
   was arithmetically unable to keep the pantry above the bar. Holding ≥7 through the
   measured trough needs ≥0.35–0.4. **This round runs 0.4.**
2. **The dice.** Round 8 confirmed 0044's named risk: arrival is a lottery at mutation
   0.001 (C saw no absorptive breeding at all in s2 and s4, same seeds as round 6). The
   pre-registered response is [D056](../DECISIONS.md)'s: rerun the best treatment at
   cellType mutation 5× (0.005) as a **discovery regime**, reported as a different
   evolutionary regime, never a silent knob turn.

## The treatment

| arms | knobs (all others: round 6's world exactly) | mechanism bet |
|---|---|---|
| `r9-s1..5` | `EVOSIM_FLOOR_REFUGE` 1 · `EVOSIM_REFUGE_FRACTION` 0.4 · `EVOSIM_CELLTYPE_MUTATION` 0.005 | the larder at the measured dose: edible floor ≈ 2× the 0.2 arm's ≈ 11–13 J/m³ at the trough — above the ≈7 reproduction bar with margin; the dice give every seed a real chance to field a chain at all |

Dose arithmetic, stated before results: at `r8c-s1`'s trough the refuge held ≈11,700 J
over 400 m², a physical ≈29 J/m³; 0.4 of that shows ≈11.8 J/m³ edible — clear of 7 —
while the meter still hides 60% of the stock from any one boom. Budget 30,000 s, wall
600 min, ceiling 8,000, seeds 1–5. Base world identical to round 8's (round 6's world).

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `refuge 1 m at 0.4 edible`, `cellType mut 0.005`, and nothing else differs from round 6's world | header line 3 |
| V2 | `floor` = 0 after t=3,100 everywhere | `floor` |
| V3 | monitors carry the **content-growth** stall rule (report byte-size unchanged across 4 × 240 s samples ⇒ wedge alert) — mtime is fooled by the hang, three occurrences now | monitor config |

## Predictions

Scored under the amended goal rule ([D063](../DECISIONS.md)); a pass here is a pass **in
the discovery regime** and will be reported with that label everywhere the result is
claimed.

| # | prediction | falsified by |
|---|---|---|
| Z1 | chains arrive in ≥ 4 of 5 seeds (the 5× dice fix the lottery) | `inherit`, lineage |
| Z2 | no treated boom's collapse coincides with edible floor density ≥ 7 J/m³ — the trap theory's threshold survives the dose change | lineage + `refuge J` |
| Z3 | **the round's answer**: ≥ 3 of 5 seeds pass the amended rule — the dose was the missing half of a mechanism whose meter half already worked | the scoring table |

Two-sided, before the answer: if Z3 passes, the goal is met (discovery regime, stated as
such) and the frontier moves to movement. If chains arrive and still bust with the edible
floor above 7, Z2 falls and the trap theory itself — not the dose — was incomplete;
that reopens the diagnosis, not the knob. If Z1 fails at 5× mutation, arrival was never
about mutation supply and D056's premise needs rereading.

## Launch

Five arms, ≤ 5 concurrent machine-wide (round 8's three stragglers still running at
launch: `r9-s1`/`r9-s2` go first on free workers, the rest as stragglers end). Headers
verified before any arm is believed. Results appended below.
