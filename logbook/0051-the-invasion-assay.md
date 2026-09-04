# 0051 — The invasion assay

*2026-09-03. Pre-registered within the hour of launching the first arm and before its
inoculation fired at t=5,000; results appended after. Round 15: not a goal round — a
screen. The owner's ruling of the same day: stop learning one number a day from a ten-hour
physics run when the number can be measured in two hours or computed in a second.*

## Why an assay, not another round

Rounds 12, 13 and 14 all asked the same question — does an absorptive line breed to
replacement — and each answered it by t≈10,000 with a single mutant that appeared by
chance, in a run that had to go to 30,000 for the goal rule. One lucky mutant is a sample
of one; whether it leaves a line is as much demographic luck as fitness. The quantity that
decides the goal is the stomach's **invasion fitness when rare**: the per-capita growth of
a small absorptive population dropped into an established producer world. The run already
has the instrument for exactly this (`EVOSIM_INOCULATE`, D-entry on inoculation; `Inoculate`
admits N verbatim copies of a genome at a chosen time and depth, seeded from the world's
own stream so the run replays). Fifty copies give statistics one mutant cannot, and 7,000 s
after inoculation is two to three lifetimes at senescence 3,000.

This runs in parallel with round 14 ([0050](0050-the-stomachs-gearing.md)), whose seed-1
arms are past t=10,000: `r14c5-s1` built a line of seven inherited by t=7,000 and lost it
by t=10,700; `r14c10-s1` had no line at t=5,000. Round 14 continues under the owner's
sequential rule (seeds 3–5 only if a line appears in seeds 1–2).

## Design

| arm | world | clearance | inoculum |
|---|---|---|---|
| `r15i-c10` | round 13 arm A, seed 2 (the seed whose population sat at −12 m in 6–9 J/m³ from t=12,000) | 10 | 50 copies at t=5,000, −12 m |
| `r15i-c1` | same | 1 (the control — the world in which the stomach failed) | same |
| `r15i-c5` | same | 5 | same |

**The inoculum** is the first genome carrying an absorptive node in `r13a-s2`'s t=17,000
snapshot: a **single-node pure stomach** (one absorptive part, no leaf; brood 1, endowment
103 J), SHA-256 `342f472a7dda…`, kept at `scratch/inoculum-r13a-s2-t17000.json` on this
machine (run data, not committed; the header records the hash). It is the body the world
itself produced, not one we designed. Budget 12,000 s, wall 300 min; every other knob
`r13a-s2`'s (sink 0.002 both fields, vent off, rolls 0.3 m/s in 30 m cells, excretion 0.01,
4 patches, area 100, ceiling 8,000, floor closes 3,000, mutation 0.005). Launch order c10,
c1, c5 as workers free, ≤ 5 arms on the machine with round 14's.

**Measurement**, from `lineage.jsonl` (births carry `k`: `f` floor, `r` reproduction, `i`
inoculation; `p` parent id; `abs` expressed; deaths carry `e:"d"`):

- **R0 observed**: children per inoculant, and children per member over the whole inoculant
  lineage (descendants = every birth whose parent chain reaches an `i` row).
- **Lineage size** by generation and by time; alive at t=12,000.
- **Inoculant lifetimes** (death time − 5,000).
- **Expressed share**: fraction of the lineage's children with `abs = 1`.
- `mat blk` in patch 0 around the inoculation, to see whether the stomach's refusals differ
  from the producers'.

A permanent script, `scripts/lineage-invasion.ps1`, does this (the owner's rule: recurring
analyses become scripts).

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `clearance N`, `inoculate 50 @ 5000 s, 12 m, genome 342f472a7dda`, `sink 0.002 m/s, matter 0.002 m/s`, `vent off`; every other token equals `r13a-s2`'s | header line 3 (c10: held) |
| V2 | the assay fired: 50 `k:"i"` births at t≈5,000, and the report's `absorpt` jumps by ~50 at the next sample | lineage, `absorpt` |
| V3 | `floor` = 0 after t=3,100 (the world is established before the inoculation) | `floor` |
| V4 | audit 0.0000% every sample | `audit` |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the stomach cannot invade at clearance 1**: R0 observed < 1 over the lineage, and the lineage is extinct or ≤ 5 alive at t=12,000 | `r15i-c1` lineage |
| M2 | **it can at clearance 10**: R0 > 1, ≥ 50 descendants born by t=12,000, ≥ 10 alive at t=12,000 | `r15i-c10` lineage |
| M3 | **the dose orders it**: per-capita rate c10 > c5 > c1 | the three lineages |
| M4 | **expression is not the gate**: ≥ 90% of the lineage's children read `abs = 1` (a one-node stomach has little to mutate into at 0.005) | `abs` share |
| M5 | **the assay agrees with the calculator**: the ledger tool's R0 for this genome at −12 m and the observed detritus density is on the same side of 1 as the assay in each arm | `scripts/ledger.ps1` output vs the lineages |

## The two-sided readings

- **M2 holds and round 14's c10 arms still form no line:** fitness is fine and establishment
  from a single mutant is the bottleneck — demographic stochasticity, not economics. The
  levers are then supply (a founder guild, a higher absorptive share among founders) and are
  world rules for the owner; the goal rule's silence on how the line arises matters here.
- **M2 fails:** a stomach that out-earns a leaf on paper still cannot invade — the world's
  ledger and the calculator's disagree, or something outside the ledger (matter refusals in
  the stomach's patch, the dark excursion, shading) is binding. Compare the calculator's R0
  with the observed lifetimes and children first; that comparison is the whole point of
  running both.
- **M1 fails (invasion at clearance 1):** the world's own stomach could always invade and the
  mutation route was the bottleneck — a mixotroph mutant carrying a leaf *and* a stomach is a
  worse body than a pure stomach, and mutation never produces the pure one in the light.
  Then round 14's knob was unnecessary and the answer is in how absorptive bodies arise.
- **M4 fails:** the mutation operator turns stomachs back into leaves faster than a line can
  grow; that is a Core question (mutation rates per cell type), not a world one.

## The calculator's prediction, recorded before the assay ended

*Added at t≈7,000 of `r15i-c10`, with the arm's lineage read only to t=6,731 (every
inoculant had bred exactly once, on its 200 J stake, and nothing since). `scripts/ledger.ps1`
(the same day's build; D069) on the inoculum at −12 m:*

| clearance | density (J/m³) | net W at birth | lifetime (s) | R0 | first child (s) |
|---|---|---|---|---|---|
| 1 | 0.5–10 | −0.007 to +0.013 | 6,300–9,000 | 0 | never |
| 5 | 1 / 2 / 4 / 7 / 10 | 0.002 / 0.013 / 0.035 / 0.067 / 0.099 | 7,400–14,100 | 0 / 0 / 1 / 1 / 2 | – / – / 950 / 426 / 278 |
| 10 | 0.5 / 1 / 2 / 4 / 7 / 10 | 0.002 / 0.013 / 0.035 / 0.078 / 0.142 / 0.207 | 7,400–16,700 | 0 / 0 / 1 / 2 / 4 / 6 | – / – / 950 / 362 / 190 / 130 |
| any, **same shape as a leaf** | any (≤ 12 m) | 0.068 | 9,900 | 2 | 417 |

The body is 0.0022 m³ with a child priced at ~129 J (endowment 103 + overhead 25 + 1 J of
tissue), so a child costs the same whatever the body earns, and R0 is set by net watts alone.
**Prediction:** the assay's field at the population's depth reads 0.7–1.4 J/m³, where the
calculator gives R0 = 0 at every clearance — the lineage should not grow past the founders'
first children, and M2 should *fail* unless the stomachs sit in water ≥ 2 J/m³ (R0 1) or
≥ 4 (R0 2, the leaf's figure). M5 is therefore a live test: if the lineage grows anyway, the
calculator is missing a term the world has.

## Results

### `r15i-c10` — the stomach cannot invade at clearance 10 in this water

*Ended at budget, 12,000 s in 78 min wall (2.6× real time). V1–V4 held: the inoculation
fired at t=5,000 exactly, 50 births with `k:"i"`, all 50 expressing the stomach.*

| measure | value |
|---|---|
| inoculants | 50, all dead by t=12,000; lifetimes 6,571–6,984 s (mean 6,732) |
| children per inoculant | exactly 1.00 — every one bred once, immediately, on its 200 J founding stake |
| descendants born | 51 (50 in generation +1, **1** in generation +2) |
| R0 observed | 0.96 over completed members — and that figure is the founding stake; the 50 children, born with the genome's own 103 J endowment, produced one child between them |
| lineage alive at t=12,000 | 48 — the generation +1 children, aged ~7,000 s, on the same clock that killed their parents |
| expressed absorptive among descendants | 100% |
| field at the population's depth (−10 m) | 1.4 J/m³ at inoculation, 0.26–0.7 from t=7,000 on |

| # | prediction | verdict |
|---|---|---|
| M2 | invades at clearance 10: R0 > 1, ≥ 50 descendants, ≥ 10 alive | **falsified** on the clause that matters — R0 < 1 by a wide margin once the founding stake is discounted; the 51 descendants and 48 alive are the stake and the not-yet-dead, not growth |
| M4 | expression is not the gate | **held** — 100% |
| M5 | the assay agrees with the calculator | **held** — the calculator's table, recorded above before the arm ended, gave R0 = 0 and a lifetime of 7,400–9,000 s at 0.5–1 J/m³; observed: one child from fifty naturally-endowed members, lifetimes ~6,700 s |
| M1, M3 | the control (c1) and the middle dose (c5) | not yet run — see below |

**What happened.** Fifty stomachs arrived in water at 1.4 J/m³, each spent its stake on one
child at once (the breeding gate is met at admission: 200 J against a ~129 J price), and
then the hundred of them, with the seven natives already there, grazed the field at their
depth down to a quarter of a joule per cubic metre — below the clearance-10 break-even of
0.4. From there a 0.0022 m³ body clears at most a few thousandths of a watt: enough to
sit, not enough to reach 129 J again before senescence. They died between 6,571 and
6,984 s of age, in a band of four hundred seconds, which is what a population starving on
the same clock looks like. The children, born with 103 J rather than 200, never reached
the gate at all.

This is not a failure to invade so much as an overshoot: the stomachs ate their own
window. The number that decides the goal is whether the field and the stomach population
can settle at a density where R0 = 1 with more than ten alive — consumer-resource
dynamics, not invasion fitness alone — and that is what round 14's clearance-10 arms are
measuring (`r14c10-s1`'s line of 21 fading in 1 J/m³ water it thinned itself).

**The control and the middle dose.** The calculator's table gives R0 = 0 for this body at
clearance 1 at every density in the table and at clearance 5 below 4 J/m³, and it has now
agreed with the assay at clearance 10 on both R0 and lifetime. `r15i-c1` still runs, as
the control that makes the assay an experiment, when a worker is free after `r14c10-s2`
and `r16dt-01`; `r15i-c5` is dropped as redundant with the table — the owner can reinstate
it. Owner's question of the day, answered here: a steady trickle of founders would have
produced this result continuously (one stake-funded child each, then starvation), so the
assay is timed against the calculator's window instead, and the next inoculation goes in
where and when the timeline says a stomach could breed.

### `r15i-c1` — the control gives the same answer, in water ten times richer

*Ended at budget, 12,000 s in 70.6 min wall (2.8× real time). V1–V4 held: header
`clearance 1 · inoculate 50 @ 5000 s, 12 m, genome 342f472a7dda · sink 0.002 m/s, matter
0.002 m/s · vent off`, every other token `r13a-s2`'s; 50 `k:"i"` births at t=5,000 and
`absorpt` 0 → 50 at that sample; `floor` 0 from t=3,100; audit 0.0000% throughout.*

| measure | `r15i-c1` (clearance 1) | `r15i-c10`, for comparison |
|---|---|---|
| inoculants | 50, all dead; lifetimes 5,677–5,813 s (mean 5,743) | 50, all dead; 6,571–6,984 (mean 6,732) |
| children per inoculant | exactly 1.00 — every one bred once, at once, on its 200 J stake | exactly 1.00 |
| descendants born | 50 (all generation +1; **no** generation +2) | 51 (one in generation +2) |
| R0 observed | 0.89 over completed members — the stake again | 0.96 |
| lineage alive at t=12,000 | 44, the children, aged ~6,900 s | 48 |
| expressed absorptive among descendants | 98% | 100% |
| field at the population's depth (−10.5 to −13.7 m) | **2.6–5.3 J/m³** throughout | 1.4 at inoculation, 0.26–0.7 from t=7,000 |

| # | prediction | verdict |
|---|---|---|
| M1 | cannot invade at clearance 1: R0 < 1 and extinct or ≤ 5 alive at 12,000 | **R0 clause held** (0.89; zero children from the fifty naturally endowed members); **the alive clause is falsified as written**, by the same artefact that flattered c10 — 44 alive are children born at t≈5,000 who have not yet reached the age their parents died at |
| M3 | the dose orders the per-capita rate: c10 > c5 > c1 | **falsified** on the pair that ran — c10 and c1 gave the identical figure, one child per inoculant, and that child is the founding stake at both doses; c5 not run |
| M4 | expression is not the gate | **held** — 98% |
| M5 | assay and calculator on the same side of 1 | **held** — the table gave R0 = 0 at clearance 1 at every density, and the world gave 0 from every naturally endowed member; the calculator's lifetime band was 6,300–9,000 s, the world's 5,700 — 10% short of the band's floor, which is the matter draw or the drift it does not have |

**What the pair says.** Read the field rows together. The clearance-1 stomachs sat in
2.6–5.3 J/m³ and the clearance-10 stomachs in 0.26–0.7, and both earned about the same:
a tenfold gearing bought a tenfold-thinner field, because fifty stomachs at clearance 10
graze the water at their depth ten times faster than fifty at clearance 1 do, and the
water was already the bottleneck at 1. Income = density × clearance, and the stomachs set
the density. That is the consumer–resource point in one line, and it is why the assay
cannot see the dose (M3) — the dose is absorbed into the field before it reaches the
ledger. Round 14's clearance-10 arms are showing the same thing at population scale
(`r14c10-s2`'s line, 45 → 12 in water it thinned to 0.5–0.7 J/m³).

**An instrument note, against the next assay.** Inoculating at t=5,000 and stopping at
12,000 leaves 7,000 s — one lifetime — so the "alive at" clause reads the children's age,
not the lineage's establishment, in both arms; M1's and M2's alive clauses were
unscoreable as pre-registered. An assay that means to score establishment has to run at
least two lifetimes past the inoculation (to t=20,000 or beyond on this clock), or score
*generation +2 births* instead of members alive. The calculator, which has no such
horizon, was the better instrument here: both arms' R0 came out where its table said.

## Verdict

Closed 2026-09-03, two arms of three (`r15i-c5` dropped as redundant with the table; the
owner can reinstate it). The inoculum — the world's own stomach from `r13a-s2` — cannot
invade the round-13 world at clearance 1 *or* at clearance 10: one stake-funded child
each, no naturally endowed member ever reached the breeding gate, at either dose, in
water the stomachs themselves set. The calculator predicted both outcomes before either
arm ended (M5 held twice). M4 held twice: expression is not the gate. The dose does not
order the outcome (M3), because the field absorbs it. What the assay could not score —
establishment over more than one lifetime — is a design fault of this assay, noted above,
and the question it leaves is the one round 14 is now answering at population scale:
whether stomachs and their field can settle anywhere with R0 = 1 and more than ten alive.

### Amendment 2: the assay in the leak world (2026-09-04, pre-registered before launch)

Round 18 met the goal rule with founder-descended lines (0054, M2 held in all five seeds):
the leak kept the founding lottery's stomachs alive, and no line in any seed was rooted in
a mutant. Whether a *late* stomach can now establish is therefore open, and it is this
assay's question, asked again in the world that passed. One arm, `r15i-c10-x15`: seed 2,
clearance 10, `EVOSIM_EXUDATION 0.15`, dt 0.02 (the screening step), 50 copies of the same
inoculum at t=5,000 and −12 m, **run to 20,000 s** so establishment is read two lifetimes
past the inoculation (the instrument note above). Launcher `scratch/launch-r15.ps1
-Exudation 0.15 -Dt 0.02 -Seconds 20000`. This world already carries founder-descended
stomachs (0053's `r17x-s2` had 66 inherited at t=3,000 at this seed and step), so the
inoculants are read by their `k:"i"` id, not by the `inherit` column.

| # | prediction | falsified by |
|---|---|---|
| A1 | **the inoculants establish**: R0 over completed inoculant lineages > 1, and ≥ 50 descendants born by t=20,000 with ≥ 10 of generation +2 or later | `scripts/lineage-invasion.ps1 r15i-c10-x15` |
| A2 | **still alive two lifetimes on**: ≥ 10 inoculant descendants alive at t=20,000 | the same |
| A3 | **matter, not energy, is what binds the late ones too**: the inoculant descendants' mean `netW` in `absorptive.jsonl` at t > 12,000 is positive, and the world's `mat blk` window is > 50,000 by then | `absorptive.jsonl`, `mat blk` |

Readings: A1 and A2 hold — the leak lets a late stomach invade as well as keeping the
founders' alive, and the goal's silence on how a line arises stops mattering. A1 fails
with A3 holding — the late stomach earns but cannot conceive for want of matter at −12 m,
the same constraint 0054 named, and the matter-at-depth proposal is the answer for both.
A1 fails with A3's first clause failing — the inoculant body does not earn in this water,
which the ledger under the 0.15 config would have to explain.

**Results (2026-09-04, `r15i-c10-x15` to budget: 20,000 s in 38 min at 8.7× real time;
header verified — `exudation 0.15`, `dt=0.02`, the inoculation tokens; audit 0.0000%
throughout).** The late stomach establishes. The 50 inoculants lived 3,724–11,239 s (mean
9,480, against 6,732 in this world without the leak) and every one bred; their lineage
ran **18 generations deep** by t=20,000 — 205 descendants born, 102 of them in generation
+2 or later, about eleven births per generation from +3 on — and **112 were alive at the
end**, at −15 m in 1.6–2.8 J/m³.

| # | prediction | verdict |
|---|---|---|
| A1 | R0 > 1 over completed members; ≥ 50 descendants with ≥ 10 in generation +2 or later | **R0 clause falsified as measured** (0.72 over the 143 completed members — the count is dragged by the inoculants' own 1.06 and by early deaths, while 112 of the lineage are still alive and uncounted); **descendants clause held** by a wide margin (205; 102 at +2 or later) |
| A2 | ≥ 10 inoculant descendants alive at 20,000 | **held** — 112 |
| A3 | descendants' mean `netW` > 0 at t > 12,000; `mat blk` > 50,000 | **held as worded** (+0.003 W; 300,000 blocked per window) — but read the reserves: the descendants held **48 J** on average, under a child's price, and spent everything they earned on 0.74 children each. They live at replacement on *energy*, the chemostat regime, not blocked with full reserves as `r18x-s1`'s stomachs were. The founder-descended stomachs in the same run sat higher (−6 m) in 5.2 J/m³ with 231 J in reserve and a *negative* net (−0.023 W) — a different, larger-bodied population (0.0054 m³ against the inoculum's 0.0022) |

So the goal's silence on how a line arises stops mattering: in the leak world a stomach
that arrives late, alone in its kind at its depth, founds a lineage that is still there
and still breeding two lifetimes on — the answer the original assay could not give at
either dose (one stake-funded child each, then starvation), given by the same body in the
same seed with one world rule changed. The lineage holds at about a hundred with ~11
births per generation and no growth after t≈8,000: replacement, on 1.6–2.8 J/m³ at
clearance 10, exactly where the ledger's R0 = 1 sits. Whether matter at −15 m would bind
it if the energy did not is not separable from this run.
