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

**Mid-round instrument correction (2026-09-02, recorded while three arms still ran).**
Every in-flight chain status I read before this date was wrong: I was quoting columns 26/27
(`float` / `flt inh`) as `absorpt` / `inherit` (columns 14/15). With the right columns, **no
round-8 arm other than the flagship `r8c-s1` ever established an absorptive chain** — the
"chains" I logged in `r8b-s5`, `r8c-s3` and `r8c-s5` were float-tissue counts. `r8c-s3`'s
lineage confirms it: twenty absorptive births in the whole run, one inherited (t=4), founders
all starved by t=501, singleton mutants after. Scoring below uses `lineage.jsonl`, which is
immune to this mistake.

Three mid-round findings, dated before scoring:

1. **The treatment that never bound.** `r8c-s2` and `r8c-s4` are token-for-token identical
   to `d056-s2`/`d056-s4` for the full 30,000 s (verified column-by-column at t=15,000; the
   only diff is the report's new `species` column, empty in the old format). V2's replay
   property ran to full length because no absorptive ever fed in the refuge layer — so those
   two arms tested nothing about the refuge. A no-arrival seed is not a treated seed.
2. **A second disease, seen three times.** `d056-s3` (control!), `r8c-s3` and `r9-s2` all
   died the same whole-world death: a long matter drought during the producer era (thousands
   of refused conceptions per sample) ends, **births freeze anyway**, and the population
   free-falls to zero by deaths alone — with free matter available (mat top ≈ 0.5), zero
   refused conceptions, and the larder untouched and still growing (refuge J at maximum at
   extinction). The reading: the drought outlasts the reproductive window of every cohort
   alive during it; when matter returns, the survivors are uniformly post-reproductive and
   senescence finishes the world. This killer predates the refuge — it lives in the
   untreated world — and it also explains failed arrival: absorptive singletons appear
   during the drought and cannot breed for the same reason nothing else can.
3. **Arrival is drought-gated, not mutation-gated.** `r9-s2` at 5× cellType mutation drew
   absorptive mutants repeatedly (singletons throughout) and still never got one inherited
   birth — the mutants landed in the drought. D056's premise (arrival limited by mutation
   supply) fails its first direct test; 0045's Z1 should be read with this in hand.

**Mid-round re-order (2026-09-01, recorded before any B/C arm finished).** Arm A went
0-for-3 by producer extinction (t=6,478, 6,596, 13,206 — the founding-suppression death,
dose-generic) and is formally unable to reach 3-of-5. The interleave existed to hedge
treatments against machine incidents; with A's outcome determined there is nothing left
to hedge, and the owner's standing priority is the fastest credible pass — so `r8a-s4`
and `r8a-s5` move to the back of the queue (still run, per this pre-registration; only
later). No dose, budget or scoring rule changes.

---

## Results (2026-09-02, all fifteen arms accounted for)

**Score: 0 of 15 seeds pass the amended rule. No treatment reaches 1, let alone 3.**

| arm | ending | at end (or cut): alive / absorpt / inherited | scored |
|---|---|---|---|
| r8a-s1 | extinct t=6,478.5 | 0 / 0 / 0 | fail |
| r8a-s2 | extinct t=6,596 | 0 / 0 / 0 | fail |
| r8a-s3 | extinct t=13,206.5 | 0 / 0 / 0 | fail |
| r8a-s4 | extinct t=11,259.5 | 0 / 0 / 0 | fail |
| r8a-s5 | extinct t=19,055 | 0 / 0 / 0 | fail |
| r8b-s1 | runaway t=7,185 | 7,878 / 1 / 0 | censored |
| r8b-s2 | **wedged**, killed at t=10,800 | 3,498 / 0 / 0 | censored |
| r8b-s3 | runaway t=4,099 | 7,500 / 1 / 0 | censored |
| r8b-s4 | budget | 5 / 0 / 0 | fail |
| r8b-s5 | wall clock at t=17,800 | 1,746 / 0 / 0 | censored |
| r8c-s1 | **wedged**, killed at t=21,400 | 4,166 / 45 / 45 | censored; fails even at the cut (below) |
| r8c-s2 | budget | 618 / 0 / 0 | fail (untreated — see addendum) |
| r8c-s3 | extinct t=27,068.5 | 0 / 0 / 0 | fail |
| r8c-s4 | budget | 2,204 / 1 / 0 | fail (untreated — see addendum) |
| r8c-s5 | runaway t=19,594.5 | 7,672 / 7 / 6 | censored |

The flagship deserves its own line of honesty: at its wedge cut `r8c-s1` still held 45
inherited absorptives — and its last clade birth was t=16,366, five thousand seconds
earlier. A sterile cohort standing at parade rest. [D063](../DECISIONS.md)'s recruitment
clause was added for exactly this shape, and it fails the arm even read-to-cut. Under the
unamended rule this arm would have *passed* at the cut; the amendment earned its keep in
its first round.

### The predictions, scored

- **Y1 — falsified.** Arrival was not world-generic: first absorptive breeding happened in
  exactly one seed of fifteen (`r8c-s1`). B produced only never-breeding singletons; A's
  producers died before any chain could form.
- **Y2 — held where testable.** The flagship's trap closed at the *edible* density
  (≈5.9 J/m³ at the last clade birth, under the ≈7 bar, while the physical pool sat near
  19); no established chain's recruitment survived a trough with reachable food above the
  bar, because no other chain established at all.
- **Y3 — moot.** No chain formed in any patchy world; what A showed instead was a
  **founding cost**: eight 1/8-size matter pools, slow horizontal mixing and per-patch
  shading choke the producer lottery before any consumer question is asked. All five seeds
  died of it. The dose was constant, so this is dose-generic only in the tested corner.
- **Y4 — unanswerable.** Every clean B world ran away before its first chain; there is no
  post-establishment minimum to compare. The satiation cap + toe appear to have made the
  *producer* economy stronger (a suspected founder-recycling effect: toe-starved absorptive
  founders die early and return their matter in a matter-throttled founding), and B worlds
  grew ≈8× faster than their twins — self-censoring by ceiling.
- **Y5 — falsified.** Establishment under C: one seed of five, not two.
- **Y6 — falsified.** Zero passes. The pre-registration predicted at least one of A or B
  would pass; neither came close, and each failed *upstream* of its mechanism bet.

### What the round actually taught

The two-sided reading that fits is the fourth one — "all three fail with the trap
surviving three different medicines, re-read Y2's data first" — with one amendment: the Y2
re-read (this file's addendum, plus the r9 lineage dissections in
[0045](0045-the-dose-and-the-dice.md)) found the thresholds were right but the *world
model behind the treatments* was wrong. The worlds are not dying of larder exhaustion.
Three of them died with full larders, free matter, and a young population — births freeze
and the standing crowd, denser than water its whole life, sinks out of the photic band and
starves in the dark (depth timelines: `r9-s1` −20.9→−48.7 m, `r9-s2` −19.7→−36.7,
control `d056-s3` −65→−98 in a 24 m world). Birth is the only upward flux selection
maintains: float tissue exists and works — `r9-s2`'s literal last survivor was a floater
holding at −14.5 m — but selection prices it out to ~1% between crises because a floatless
producer breeds cheaper before it sinks out. All three medicines treated the pantry; the
patient was drowning. *(Post-hoc diagnosis, marked as such: none of this was predicted
above. It is the round-10 question.)*

Bookkeeping: the round also cost two arms to the hang (occurrences three and four:
`r8b-s2`, `r8c-s1`), whose kill-and-refresh procedure and content-growth stall rule are
now in CLAUDE.md's gotchas, tightened again after a false stall alert nearly killed the
live `r9-s3` (threshold ≥30 min, and the 90-s byte+CPU discriminator before any kill).
