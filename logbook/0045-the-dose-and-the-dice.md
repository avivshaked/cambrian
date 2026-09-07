# 0045 — The dose, and the dice

**2026-09-02**  ·  food-chain goal, round 9 · pre-registered before launch, results appended
after

Round 9 doubled the dose of round 8's most promising medicine and gave every seed five times
the mutation supply. It scored zero of five. Two seeds died with a full larder over their
heads. That is what sent the diagnosis away from the pantry and towards the water column.

The round is one treatment across five seeds, fired by two pre-registered contingencies
without new deliberation, under the owner's standing priority of the fastest credible pass
([HANDOFF](../HANDOFF.md)).

## Why this round exists before round 8 finished

Round 8's verdict became formally determined mid-round, with three stragglers still running.
Arm A went 0-for-4 by producer extinction, the founding suppression, which is dose-generic.
Arm B self-censored by runaway in every seed that ran clean. Arm C can reach at most 1 of 5,
after `r8c-s3`'s whole-world crash at t=27,068.5. No treatment can reach the 3-of-5 bar, so
Y6 fails whatever the stragglers do, and both of 0044's pre-registered contingencies fire.

The first is the dose. `r8c-s1`'s lineage dissection ([0044](0044-three-medicines.md)
results, forthcoming) showed the 0.2 meter working as a meter, at a peak of 317 against the
natural 908, with recruitment sustained ~5,400 s. The trap closed anyway, at the **edible**
density. The physical deep pool sat near 19 J/m³. The edible fraction of it fell to ≈5.9
J/m³ at the clade's last birth, under the ≈7 J/m³ reproduction threshold. The fraction is
the whole knob, since the edible floor is `fraction × refuge J / area`, so 0.2 was
arithmetically unable to keep the pantry above the bar. Holding ≥7 through the measured
trough needs ≥0.35–0.4, and this round runs 0.4.

The second is the dice. Round 8 confirmed 0044's named risk, that arrival is a lottery at
mutation 0.001. Arm C saw no absorptive breeding at all in the seeds s2 and s4, which round
6 had used too. The pre-registered response is [D056](../DECISIONS.md)'s. It reruns the best
treatment at cellType mutation 5× (0.005) as a **discovery regime**, reported as a different
evolutionary regime and never as a silent knob turn.

## The treatment

| arms | knobs (all others: round 6's world exactly) | mechanism bet |
|---|---|---|
| `r9-s1..5` | `EVOSIM_FLOOR_REFUGE` 1 · `EVOSIM_REFUGE_FRACTION` 0.4 · `EVOSIM_CELLTYPE_MUTATION` 0.005 | the larder at the measured dose: edible floor ≈ 2× the 0.2 arm's ≈ 11–13 J/m³ at the trough — above the ≈7 reproduction bar with margin; the dice give every seed a real chance to field a chain at all |

The dose arithmetic, stated before results. At `r8c-s1`'s trough the refuge held ≈11,700 J
over 400 m², a physical ≈29 J/m³. A fraction of 0.4 shows ≈11.8 J/m³ edible, clear of 7,
while the meter still hides 60% of the stock from any one boom. Budget 30,000 s, wall 600
min, ceiling 8,000, seeds 1–5. The base world is identical to round 8's, which is round 6's
world.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `refuge 1 m at 0.4 edible`, `cellType mut 0.005`, and nothing else differs from round 6's world | header line 3 |
| V2 | `floor` = 0 after t=3,100 everywhere | `floor` |
| V3 | monitors carry the **content-growth** stall rule (report byte-size unchanged across 4 × 240 s samples ⇒ wedge alert) — mtime is fooled by the hang, three occurrences now | monitor config |

## Predictions

The round is scored under the amended goal rule, [D063](../DECISIONS.md). A pass here is a
pass **in the discovery regime**, and it will be reported with that label everywhere the
result is claimed.

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

Five arms, at most 5 concurrent machine-wide, since round 8's three stragglers were still
running at launch. `r9-s1` and `r9-s2` go first on free workers, and the rest follow as
stragglers end. Headers are verified before any arm is believed. Results are appended below.

## Results (2026-09-02, all five arms accounted for)

The score is 0 of 5. It was formally determined mid-round, at 0-for-3 by the time s4 ended,
and recorded then in [0044](0044-three-medicines.md)'s addendum. The last two arms changed
nothing.

| arm | ending | at end (or cut): alive / absorpt / inherited | scored |
|---|---|---|---|
| `r9-s1` | extinct t=18,847.5 | 0 / 0 / 0 | fail — the drowning, no chain ever |
| `r9-s2` | extinct t=20,433 | 0 / 0 / 0 | fail — the drowning, no chain ever |
| `r9-s3` | runaway t=10,556 | 7,883 / 13 / 3 | censored — round 9's only establishment attempt, cut by the ceiling |
| `r9-s4` | budget | 1,581 / 3 / 0 | fail — survived chainless; three absorptive singletons alive at the end, no inherited birth all run |
| `r9-s5` | wall clock at t=23,500 | 3,388 / 7 / 0 | censored — survived to the cut, chainless |

- Z1 is falsified. An inherited absorptive birth happened in one seed of five, s3, with
  three inherited at the cut. The 5× dice supplied singletons in every seed and could not
  buy a single breeding through a drought. Arrival is gated by drought rather than by
  mutation supply, and D056's premise failed its first direct test.
- Z2 is untestable as posed. No established chain collapsed, because none established. The
  one thing the dose did show is that at 0.4 the edible floor sat at 12–20 J/m³ in the two
  worlds that died. The larder was full and above the bar the whole time, so the trap
  theory's threshold was never the constraint here.
- Z3 is falsified, with zero passes.

What the round actually taught is written up in 0044's results and addendum, because that is
where the lineage dissections were done as they happened. The two extinctions were not
larder deaths. Births froze with free matter at the surface, a young population and a full
pantry. The standing crowd sank out of the photic band and starved in the dark. It went from
−20.9 to −48.7 m in one world, and from −19.7 to −36.7 m in the other.

Float tissue was present at ~1% and its carriers were the last alive. The final survivor of
`r9-s2` was a floater at −14.5 m. That diagnosis became [D064](../DECISIONS.md) and round 10
([0046](0046-the-archean-package.md)).

Of the pre-registered readings, the third is the one that matches. Chains did not arrive,
and the diagnosis reopened at the world rather than at the knob. The reading as written put
that reopening at the mutant's first day. It happened at the water column instead.

One piece of bookkeeping remains. Arm s3 wedged once, and it was a false alarm. The arm was
alive and slow under six-arm load. The monitor threshold was raised to 32 min after that,
and the discriminator was written into CLAUDE.md. The column misread that inflated every
in-flight chain report of rounds 8 and 9 was caught here and is recorded in 0044.
