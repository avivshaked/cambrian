# 0044 — Three medicines, one patient

**2026-09-01**  ·  food-chain goal, round 8 · pre-registered before launch, results appended
after

Round 8 was the round with three answers in it. The owner picked three stabilisers, one
geographic, one on the mouth and one on the larder, and each ran across five seeds. All
fifteen arms failed, and fourteen of them failed before their treatment had anything to work
on. Everything above *Results* was written and committed before any arm launched.

The owner chose the three treatments ([D061](../DECISIONS.md), [D062](../DECISIONS.md) and
[D063](../DECISIONS.md)) after the invasion assay and its lineage dissection
([0043](0043-the-transplant.md)) characterised the disease.

## The disease, in one paragraph

Every consumer chain this world has ever grown dies the same way, now measured three times,
in two transplanted chains and one natural one. The boom grazes the deep detritus into the
**trap band**. That band lies above the ~4 J/m³ at which an adult starves. Its top is the ~7 J/m³ at which a 635.6 J brood can be funded in a pre-senescent lifetime. Recruitment then
collapses *at the population peak*, while every adult still feeds. The visible "bust" that
follows is a sterile cohort dying on schedule. A stabiliser therefore succeeds if and only
if it keeps some reachable food above the reproduction threshold through the trough. The
thresholds are per-genotype, and the numbers here are the transplant genome's, but the trap
is structural.

## The three medicines

| arm | treatment | knobs (all others: round 6's world exactly) | mechanism bet |
|---|---|---|---|
| **A** (`r8a-s1..5`) | patchy world (D061) | `EVOSIM_PATCHES` 8 · `EVOSIM_H_MIXING` 0.01 · `EVOSIM_DISPERSAL` 0.0001 · `EVOSIM_PATCH_SHADING` 1 | geography: local busts stay local, ungrazed patches re-arm past 7 J/m³ by rain, asynchrony reseeds |
| **B** (`r8b-s1..5`) | satiation cap + toe (D062) | `EVOSIM_SATIATION` 20 · `EVOSIM_CLEARANCE_TOE` 4 | the mouth: cap slows the boom's drawdown; the toe relaxes grazing exactly in the trap band so the pool can climb back out |
| **C** (`r8c-s1..5`) | partial pantry (D055 + fraction) | `EVOSIM_FLOOR_REFUGE` 1 · `EVOSIM_REFUGE_FRACTION` 0.2 | the larder: floor shows ~13 J/m³ effective — above the reproduction threshold, so establishment lives, but the boom can only strip a fifth at a time |

The dose arithmetic, stated before results. Arm A's exchange throttle follows D061's
wavelength constraint. Patch width is ≈ 7 m and the mixing timescale L²/D ≈ 5,000 s, which
is about one bust cycle. The dispersal expectation is about one patch hop per 5,000 s.

Arm B's cap at 20 W/m³ is 5× upkeep. A capped feeder still breeds a brood in ~1,700 s at
saturation, but cannot gorge at the floor pantry's 66. The toe halves clearance at the
survival break-even. Arm C's fifth of 66 J/m³ ≈ 13 clears the 7 J/m³ bar with margin.

The controls are round 6's five arms, `d056-s1..5`, by bit-identity. Every new knob at
default is bit-identical, which the suite enforces, so those runs remain the untreated
world. Budget 30,000 s, wall 600 min, ceiling 8,000, seeds 1–5, mutation unchanged at 0.001.

## The risk named first: arrival

Round 6 grew natural chains in three seeds of five, s1, s3 and s5, while s2 and s4 never
bred an absorptive in 30,000 s. Treatments act on chains that arrive. So the amended rule's
3-of-5 bar means a treatment can only pass if persistence holds in essentially every seed
where a chain shows up.

If round 8 fails on arrival, with fewer than 3 seeds ever establishing a chain under a
treatment, the pre-registered response is [D056](../DECISIONS.md)'s contingency, already
sequenced for this case. That contingency reruns the best-performing treatment with cellType
mutation at 5× (0.005), as a separate *discovery* round. It is reported as a different
evolutionary regime rather than a silent knob turn inside this one.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry each arm's exact treatment tokens and nothing else differs from round 6's world | header line 3 |
| V2 | arm B and C worlds replay their round-6 twins token-for-token (shared-column prefix) until the treatment first binds — B until the first absorptive feeding, C until the first floor-layer feeding. Arm A cannot replay (patch assignment draws from the seed stream at t=0 when K>1) — its V-check is header + suite bit-identity tests only | row diff vs `d056-sN.md` |
| V3 | `floor` = 0 after t=3,100 everywhere | `floor` |
| V4 | monitors carry the stall rule (report mtime silent > 30 min ⇒ alert) — the 0043 hang's lesson, now standing practice | monitor config |

## Predictions, and the column that falsifies each

The round is scored under the amended goal rule, [D063](../DECISIONS.md). It asks for ≥3 of
5 seeds with producers alive at the end. In each of those seeds an absorptive lineage must
be inherited for ≥ 20 consecutive samples, with ≥ 10 alive at the last sample. It must also
show ≥ 1 absorptive birth within the last 20 samples. That birth clause is computed from
`lineage.jsonl`, which every arm now writes.

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

Fifteen arms, staggered at no more than 4 concurrent on workers 2–7, which is below round
7's worst load. The waves interleave across treatments, so no treatment is hostage to one
machine incident. The first four are `r8a-s1, r8b-s1, r8c-s1, r8a-s2`. Workers were
refreshed to carry D061 and D062 before wave 1, and headers were verified against this table
before any arm was believed. Results are appended below.

## I was reading the wrong two columns

Recorded on 2026-09-02, while three arms still ran. Every in-flight chain status I gave
before that date was wrong, because I was reading the first pair of columns below as though
it were the second.

| columns | pair | what they count |
|---|---|---|
| 26 / 27 | `float` / `flt inh` | float tissue |
| 14 / 15 | `absorpt` / `inherit` | the absorptive chain |

With the right columns, no round-8 arm other than the flagship `r8c-s1` ever established an
absorptive chain. The three chains I had logged in one B arm and two C arms were
float-tissue counts.

`r8c-s3`'s lineage confirms it: twenty absorptive births in the whole run, one inherited at
t=4, founders all starved by t=501, and singleton mutants after. The scoring below uses
`lineage.jsonl`, which is immune to this mistake.

## Three findings recorded before scoring

The first is a treatment that never bound. Two of the C arms ran token-for-token identical
to their round-6 controls for the full 30,000 s. I verified that column by column at
t=15,000. The only difference is the report's new `species` column, empty in the old format.

| treated arm | its round-6 control |
|---|---|
| `r8c-s2` | `d056-s2` |
| `r8c-s4` | `d056-s4` |

V2's replay property ran to full length because no absorptive ever fed in the refuge layer,
so those two arms tested nothing about the refuge. A no-arrival seed is not a treated seed.

The second finding is another disease, seen three times. One of the three is a control,
`d056-s3`, which matters because it puts this killer in the untreated world.

The other two are `r8c-s3` and `r9-s2`. All three died the same whole-world death. A long
matter drought during the producer era, with thousands of refused conceptions per sample,
ends. Births freeze anyway, and the population free-falls to zero by deaths alone. Matter
was free at the time (mat top ≈ 0.5), refused conceptions were zero, and the larder was
untouched and still growing, with refuge J at its maximum at extinction.

My reading is that the drought outlasts the reproductive window of every cohort alive during
it. When matter returns, the survivors are uniformly post-reproductive, and senescence
finishes the world. This killer predates the refuge, since it lives in the untreated world.
It also explains failed arrival: absorptive singletons appear during the drought and cannot
breed for the same reason nothing else can.

The third finding is that arrival is drought-gated rather than mutation-gated. `r9-s2` at 5×
cellType mutation drew absorptive mutants repeatedly, singletons throughout, and still never
got one inherited birth, because the mutants landed in the drought. D056's premise, that
arrival is limited by mutation supply, fails its first direct test, and 0045's Z1 should be
read with this in hand.

## Arm A drops down the queue

Recorded on 2026-09-01, before any B or C arm finished. Arm A went 0-for-3 by producer
extinction, at t=6,478, 6,596 and 13,206, which is the founding-suppression death and
dose-generic. It is formally unable to reach 3-of-5.

The interleave existed to hedge treatments against machine incidents, and with A's outcome
determined there is nothing left to hedge. The owner's standing priority is the fastest
credible pass, so `r8a-s4` and `r8a-s5` move to the back of the queue. They still run, per
this pre-registration, only later. No dose, budget or scoring rule changes.

## Results (2026-09-02, all fifteen arms accounted for)

The score is 0 of 15 seeds passing the amended rule. No treatment reaches 1, let alone 3.

| arm | ending | at end (or cut): alive / absorpt / inherited | scored |
|---|---|---|---|
| `r8a-s1` | extinct t=6,478.5 | 0 / 0 / 0 | fail |
| `r8a-s2` | extinct t=6,596 | 0 / 0 / 0 | fail |
| `r8a-s3` | extinct t=13,206.5 | 0 / 0 / 0 | fail |
| `r8a-s4` | extinct t=11,259.5 | 0 / 0 / 0 | fail |
| `r8a-s5` | extinct t=19,055 | 0 / 0 / 0 | fail |
| `r8b-s1` | runaway t=7,185 | 7,878 / 1 / 0 | censored |
| `r8b-s2` | **wedged**, killed at t=10,800 | 3,498 / 0 / 0 | censored |
| `r8b-s3` | runaway t=4,099 | 7,500 / 1 / 0 | censored |
| `r8b-s4` | budget | 5 / 0 / 0 | fail |
| `r8b-s5` | wall clock at t=17,800 | 1,746 / 0 / 0 | censored |
| `r8c-s1` | **wedged**, killed at t=21,400 | 4,166 / 45 / 45 | censored; fails even at the cut (below) |
| `r8c-s2` | budget | 618 / 0 / 0 | fail (untreated — see addendum) |
| `r8c-s3` | extinct t=27,068.5 | 0 / 0 / 0 | fail |
| `r8c-s4` | budget | 2,204 / 1 / 0 | fail (untreated — see addendum) |
| `r8c-s5` | runaway t=19,594.5 | 7,672 / 7 / 6 | censored |

The flagship deserves its own line. At its wedge cut `r8c-s1` still held 45 inherited
absorptives, and its last clade birth was t=16,366, five thousand seconds earlier. That is a
sterile cohort standing at parade rest. [D063](../DECISIONS.md)'s recruitment clause was
added for this shape, and it fails the arm even read to the cut. Under the unamended rule
the arm would have *passed* at the cut, so the amendment earned its keep in its first round.

### The predictions, scored

- Y1 is falsified. Arrival was not world-generic. First absorptive breeding happened in one
  seed of fifteen, `r8c-s1`. Arm B produced only never-breeding singletons, and arm A's
  producers died before any chain could form.
- Y2 held where it was testable. The flagship's trap closed at the *edible* density, ≈5.9
  J/m³ at the last clade birth, under the ≈7 bar, while the physical pool sat near 19. No
  established chain's recruitment survived a trough with reachable food above the bar,
  because no other chain established at all.
- Y3 is moot. No chain formed in any patchy world. What A showed instead was a **founding cost**. Eight 1/8-size matter pools, slow horizontal mixing and per-patch shading choke the
  producer lottery before any consumer question is asked. All five seeds died of it. The
  dose was constant, so this is dose-generic only in the tested corner.
- Y4 is unanswerable. Every clean B world ran away before its first chain, so there is no
  post-establishment minimum to compare. The satiation cap and toe appear to have made the
  *producer* economy stronger. I suspect a founder-recycling effect, in which toe-starved
  absorptive founders die early and return their matter into a matter-throttled founding. B
  worlds grew ≈8× faster than their twins and censored themselves by ceiling.
- Y5 is falsified. Establishment under C happened in one seed of five rather than two.
- Y6 is falsified, with zero passes. The pre-registration predicted at least one of A or B would
  pass; neither came close, and each failed *upstream* of its mechanism bet.

### What the round actually taught

The two-sided reading that fits is the fourth one, in which all three medicines fail, the
trap survives, and Y2's data is re-read first. One amendment goes with it. That re-read,
being this file's addendum plus the r9 lineage dissections in
[0045](0045-the-dose-and-the-dice.md), found the thresholds right and the *world model
behind the treatments* wrong.

The worlds are not dying of larder exhaustion. Three of them died with full larders, free
matter and a young population. Births freeze, and the standing crowd, denser than water its
whole life, sinks out of the photic band and starves in the dark. The depth timelines say
it, in a world 24 m deep:

| arm | mean depth, start → end |
|---|---|
| `r9-s1` | −20.9 → −48.7 m |
| `r9-s2` | −19.7 → −36.7 m |
| `d056-s3` (control) | −65 → −98 m |

Birth is the only upward flux selection maintains. Float tissue exists and works. `r9-s2`'s
literal last survivor was a floater holding at −14.5 m. Selection prices it out to ~1%
between crises, because a floatless producer breeds cheaper before it sinks out. All three
medicines treated the pantry, and the patient was drowning. This is a post-hoc diagnosis,
marked as such: none of it was predicted above, and it is the round-10 question.

One piece of bookkeeping remains. The round cost two arms to the hang, occurrences three and
four, in `r8b-s2` and `r8c-s1`.

Their kill-and-refresh procedure and the content-growth stall
rule are now in CLAUDE.md's gotchas, tightened again after a false stall alert nearly killed
the live `r9-s3`. The threshold is ≥30 min, with the 90-s byte and CPU discriminator before
any kill.
