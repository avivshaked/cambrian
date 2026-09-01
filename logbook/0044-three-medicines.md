# 0044 — Three medicines, one patient

*2026-09-01. Pre-registered before launch; results appended after. Round 8: fifteen arms,
three treatments × five seeds, decided by the owner
([D061](../DECISIONS.md)/[D062](../DECISIONS.md)/[D063](../DECISIONS.md)) after the
invasion assay and its lineage dissection ([0043](0043-the-transplant.md)) characterised
the disease.*

## The disease, in one paragraph

Every consumer chain this world has ever grown dies the same way, now measured three
times (two transplanted chains, one natural): the boom grazes the deep detritus into the
**trap band** — above the ~4 J/m³ where an adult starves, below the ~7 J/m³ where a
635.6 J brood can be funded in a pre-senescent lifetime — and recruitment collapses *at
the population peak* while every adult still feeds. The visible "bust" that follows is a
sterile cohort dying on schedule. A stabiliser therefore succeeds **iff it keeps some
reachable food above the reproduction threshold through the trough** (thresholds are
per-genotype — those numbers are the transplant genome's — but the trap is structural).

## The three medicines

| arm | treatment | knobs (all others: round 6's world exactly) | mechanism bet |
|---|---|---|---|
| **A** (`r8a-s1..5`) | patchy world (D061) | `EVOSIM_PATCHES` 8 · `EVOSIM_H_MIXING` 0.01 · `EVOSIM_DISPERSAL` 0.0001 · `EVOSIM_PATCH_SHADING` 1 | geography: local busts stay local, ungrazed patches re-arm past 7 J/m³ by rain, asynchrony reseeds |
| **B** (`r8b-s1..5`) | satiation cap + toe (D062) | `EVOSIM_SATIATION` 20 · `EVOSIM_CLEARANCE_TOE` 4 | the mouth: cap slows the boom's drawdown; the toe relaxes grazing exactly in the trap band so the pool can climb back out |
| **C** (`r8c-s1..5`) | partial pantry (D055 + fraction) | `EVOSIM_FLOOR_REFUGE` 1 · `EVOSIM_REFUGE_FRACTION` 0.2 | the larder: floor shows ~13 J/m³ effective — above the reproduction threshold, so establishment lives, but the boom can only strip a fifth at a time |

Dose arithmetic, stated before results: arm A's exchange throttle follows D061's
wavelength constraint (patch width ≈ 7 m, mixing timescale L²/D ≈ 5,000 s ≈ one bust
cycle; dispersal expectation ≈ one patch hop per 5,000 s). Arm B's cap at 20 W/m³ is 5×
upkeep — a capped feeder still breeds a brood in ~1,700 s at saturation, but cannot gorge
at the floor pantry's 66; the toe halves clearance at exactly the survival break-even.
Arm C's fifth of 66 J/m³ ≈ 13 clears the 7 J/m³ bar with margin.

**Controls are round 6's five arms** (`d056-s1..5`), by bit-identity: every new knob at
default is bit-identical (suite-enforced), so those runs remain the untreated world.
Budget 30,000 s, wall 600 min, ceiling 8,000, seeds 1–5, mutation unchanged at 0.001.

## The honest risk, named first: arrival

Round 6 grew natural chains in three seeds of five (s1, s3, s5); s2 and s4 never bred an
absorptive in 30,000 s. Treatments act on chains that arrive — so the amended rule's 3-of-5
bar means **a treatment can only pass if persistence holds in essentially every seed where
a chain shows up**. If round 8 fails on arrival (fewer than 3 seeds ever establish a chain
under a treatment), the pre-registered response is [D056](../DECISIONS.md)'s contingency,
already sequenced for exactly this: rerun the best-performing treatment with cellType
mutation 5× (0.005) as a separate *discovery* round, reported as a different evolutionary
regime — not a silent knob turn inside this one.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry each arm's exact treatment tokens and nothing else differs from round 6's world | header line 3 |
| V2 | arm B and C worlds replay their round-6 twins token-for-token (shared-column prefix) until the treatment first binds — B until the first absorptive feeding, C until the first floor-layer feeding. Arm A cannot replay (patch assignment draws from the seed stream at t=0 when K>1) — its V-check is header + suite bit-identity tests only | row diff vs `d056-sN.md` |
| V3 | `floor` = 0 after t=3,100 everywhere | `floor` |
| V4 | monitors carry the stall rule (report mtime silent > 30 min ⇒ alert) — the 0043 hang's lesson, now standing practice | monitor config |

## Predictions, and the column that falsifies each

Scored under the **amended goal rule** ([D063](../DECISIONS.md)): ≥3 of 5 seeds with
producers alive at the end, an absorptive lineage inherited ≥ 20 consecutive samples,
≥ 10 alive at the last sample, **and ≥ 1 absorptive birth within the last 20 samples**
(computed from `lineage.jsonl`, exact — every arm now writes it).

| # | prediction | falsified by |
|---|---|---|
| Y1 | chains arrive (first absorptive breeding) in ≥ 3 seeds under every treatment — arrival is world-generic, not treatment-sensitive | `inherit`, lineage |
| Y2 | **the trap theory holds**: in every arm where an established chain's recruitment collapses, deep edible density at the last clade birth is below ~7 J/m³; where recruitment continues past a trough, some reachable pool sat above it | lineage + `det deep`/`refuge J`/patch columns |
| Y3 | **arm A shows asynchrony**: `det patch sd` rises above zero when a chain establishes, and at least one boom busts in some patches while `absorpt` stays > 0 — a local bust that stayed local | `det patch sd`, `absorpt` |
| Y4 | **arm B softens the drawdown**: post-establishment minimum of `det deep` is higher than the same seed's round-6 minimum wherever round 6 had a chain | `det deep` vs `d056-sN.md` |
| Y5 | **arm C establishes through the metered larder**: establishment occurs in ≥ 2 seeds and no treated boom exceeds half its round-6 twin's peak `absorpt` (the meter meters) | `absorpt` |
| Y6 | **the round's answer**: at least one treatment passes the amended rule (≥ 3 of 5 seeds). Written honestly: the trap analysis says B's toe attacks the mechanism most directly, the literature's persistence record belongs to A, and C is the cheapest — the pre-registration predicts **at least one of A or B passes**, and does not predict which | the scoring table |

## The two-sided readings, before the answer

- **A passes, B/C fail:** geography is the missing law — the owner's hypothesis wins on
  the merits; the movement frontier gets its prize next.
- **B passes, A fails:** the trap was always about the mouth; patches without a viable
  mouth just spread the same collapse thinner. D062's toe graduates from hedge to
  mechanism; A's knobs stay for the movement question later.
- **C passes:** the cheap knob was enough; A and B become refinements, and the
  partial-pantry dose curve is the next sweep.
- **All three fail with chains arriving (Y1 holds, Y6 fails):** the trap survives three
  different medicines — re-read Y2's data first (if the trap theory itself failed, the
  thresholds were wrong, not the treatments), then the round-9 conversation starts from
  the failure signatures, not from a new guess.
- **Y1 fails (arrival-limited):** D056's 5× discovery rerun of the best performer, as
  above.

## Launch

Fifteen arms, staggered ≤ 4 concurrent on workers 2–7 (below round 7's worst load), waves
interleaved across treatments (`r8a-s1, r8b-s1, r8c-s1, r8a-s2` first) so no treatment is
hostage to one machine incident. Workers refreshed to carry D061/D062 before wave 1;
headers verified against this table before any arm is believed. Results appended below.
