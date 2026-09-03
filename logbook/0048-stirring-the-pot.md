# 0048 — Stirring the pot

*2026-09-02. Pre-registered before launch; results appended after. Round 12: two
treatments × five seeds, the owner's design ([D066](../DECISIONS.md) and the excretion
amendment to [D065](../DECISIONS.md)), written while round 11
([0047](0047-the-half-life.md)) is finishing and before it is scored.*

## Where round 11 left the patient

At t≈16,000–20,000, all five half-life worlds were alive at ~1,400–1,540, turnover was two
to three times the 10c control's (deaths 1,000–1,800 against 600–1,250 for a whole 10c
run), the deep larder stood at 6.7–10.9 J/m³ — over the absorptive breeding bar in four
seeds of five — and mean depth was −0.4 to −3.5 m. And no seed had produced one inherited
absorptive. The rate gate is open; the location gate is not: detritus sinks at 0.02 m/s and
clears the 24 m lit band in twenty minutes, the surface holds 0.16–0.25 J/m³ where the
mutants are born, and a body under V0 cannot sink to the 10 J/m³ fifty metres below it.

The owner's reading, which became D066: the current was supposed to move creatures on every
axis and move matter, and did neither — a depth-only field of ±2.4 m acting on bodies alone;
and a single uniform flow could not stir in any case, because stirring is *differential*
motion. "What we need is something that can stir the soup." And, on excretion: creatures do
excrete (D052), but at a rate that returns ~5–10% of a body's matter in a lifetime, so the
matter loop closes almost entirely by death and the surface lives in permanent famine —
100,000+ refused conceptions per sample, every seed, every round since 10.

## The treatments

| arm | knobs (all others: round 11's world exactly) | mechanism bet |
|---|---|---|
| **A** (`r12a-s1..5`) | `EVOSIM_PATCHES` 4 · `EVOSIM_CURRENT` 0.1 · `EVOSIM_CURRENT_PERIOD` 6000 · `EVOSIM_CURRENT_CELL` 60 · `EVOSIM_CURRENT_ROLLS` 1 · `EVOSIM_CURRENT_BLINK` 3000 · `EVOSIM_CURRENT_ADVECT` 1 | **the stirred soup**: two convection rolls over four patches, surface to floor, carrying detritus up into the light and small mutants down to the larder in the same parcel; blinking parity for chaotic advection |
| **B** (`r12b-s1..5`) | arm A + `EVOSIM_EXCRETION` **0.01** (was 0.001), fixed matter term non-excretable | **the recycler**: a living body returns its tissue matter within its lifetime; the surface stops starving; conception is no longer a lottery against a drought |

**Dose arithmetic, stated first.** The time factor is the old zero-mean incommensurate pair,
so at the default 300 s period a roll would reverse every ~150 s — the jiggle again. At
6,000 s a roll runs one way for ~3,000 s; with mean |amplitude| ≈ 0.6 × 0.1 m/s the
circuit round a 60 m × 5 m roll (perimeter ~130 m) takes ~2,200 s, so a parcel makes a full
circuit before the flow reverses. The blink at 3,000 s flips which patch rises, out of step
with the reversal, which is what makes the stirring chaotic rather than periodic. Cell depth
60 m is the full column, because the larder is on the floor and the point is to lift it.
Four patches of 25 m² each, ten founders per patch, exchange every step through the rolls —
the opposite regime to round 8's sealed 1/8 pools. Excretion 0.01: at senescence 3,000 (a
lifetime of ~6,000 s) a body returns ~100% of its tissue matter alive; the fixed 3 units stay
until death, so D065's count floor holds.

Budget 30,000 s, wall 600 min, ceiling 8,000, area 100, seeds 1–5. Controls: round 11's arms
(the same world with rolls off and excretion at 0.001).

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `current 0.1 m/s over 6000 s in 60 m cells · rolls blink 3000 s · advect on` and `patches 4`; arm B additionally `excretion 0.01 /J`; all other tokens equal round 11's | header line 3 |
| V2 | no arm replays its round-11 twin past t=0 (patches draw at t=0) | row diff vs `r11-sN.md` at t=100 |
| V3 | `floor` = 0 after t=3,100 | `floor` |
| V4 | the audit column stays at 0.0000% with advection on — conservation in the live world, not only in the suite | `audit` |
| V5 | monitor: 32-min content-growth stall rule, 90-s discriminator before any kill | monitor config |

## Predictions

Scored under D063 unchanged, recruitment clause from `lineage.jsonl`; a pass is a pass in the
discovery regime (mutation 0.005) and is labelled so.

| # | prediction | falsified by |
|---|---|---|
| S1 | **the soup is stirred**: in arm A, surface detritus (`J/m3 here`) at t=10,000–20,000 is ≥ 10× round 11's (≥ 2 J/m³), and `det patch sd` is non-zero and fluctuating — the rolls put food where the mutants are | `J/m3 here`, `det patch sd` |
| S2 | **producers survive the stirring** (the Sverdrup risk): ≥ 4 of 5 arm-A seeds reach budget alive; if the whole column's stirring puts producers in the dark half the time, `alive` collapses within the first 5,000 s and this is falsified | `**Ended:**`, `alive` |
| S3 | **the famine ends** in arm B: refused conceptions per sample at t > 10,000 fall by ≥ 5× against arm A's | `mat blk` |
| S4 | **chains arrive** (first inherited absorptive birth) in ≥ 3 of 5 seeds in at least one arm | `inherit`, lineage |
| S5 | **the round's answer**: ≥ 3 of 5 seeds pass D063 in at least one arm. Written honestly: the location analysis says A is the lever the gate needs; the drought analysis says B is what lets a mutant breed once it gets there; the pre-registration predicts **B passes and A does not**, and would count A passing alone as the more surprising result | the scoring table |

## The two-sided readings

- **B passes:** the goal is met (discovery regime, stated as such); the frontier moves to
  movement, in water that finally moves.
- **A passes, B fails:** the excretion dose broke something (check S3's direction — too
  much matter can bloom past the light); the goal is met on A and B's dose is retuned later.
- **S1 holds, S4 fails in both:** the food reaches the mutants and they still do not breed
  — the gate is inside the mutant (energy to fund a brood at 2–10 J/m³ with a body this
  small), and the next round looks at the absorptive's own economics, not the world's.
- **S2 fails:** Sverdrup wins; cell depth comes down to 30 m and the larder is lifted by a
  second, deeper roll later.
- **S1 fails:** the rolls did not reach the physics (check V1/V4 first) or the dose is too
  weak; period and speed go up.

## Launch

Ten arms, ≤ 5 concurrent: `r12a-s1..s5` first on the workers round 11 frees (worker 7 is
free at writing), `r12b-s1..s5` after, interleaved so neither arm waits on one machine
incident. Headers verified against the table before any arm is believed. Results appended
below.

---

**Mid-round dose change (2026-09-02, after the first arm, before any other launched).**
`r12a-s1` at cell depth 60 m never founded: 40 founders held at ~40 through founding, a
peak of 186 at t=5,100, 237 births in all and none after t≈7,000, extinct at t=11,203 —
against round 11's ~485 alive by t=3,000 and ~885 by 6,000. Mean depth sat at −21 to −27 m
from t=1,100 onward in a world whose photic band ends near 24 m: the full-column rolls
carry neutral founders round the whole cell, so a producer spends half its life in the
dark and never funds a brood. Shade was 5–11% and surface matter 0.05–0.8 — neither light
competition nor drought; darkness by transport. **S2 falsified in its founding form** at
the first seed, by the signature it named (collapse inside the first 5,000 s — here a
failure to ever rise). The pre-registered response fires: **cell depth 60 → 30 m** for
every remaining arm, A and B alike. Producers then spend ~20% of a circuit below the light,
inside a 3× margin; detritus deposited in the top 30 m recirculates within the lit band
(the roll's upward leg at ~0.06 m/s outruns the 0.02 m/s sink), and what escapes below 30 m
accumulates on the floor as before — the surface larder is the one this round is for. The
cell-60 arm stands as the Sverdrup measurement. Relaunched arms are named `r12a30-sN` and
`r12b30-sN`; every other knob, prediction and reading is unchanged. S2's "≥ 4 of 5" now
applies to the cell-30 arms.

**Second dose change (2026-09-03, on the first cell-30 arm's evidence, before any other cell-30 arm ran past founding).**
`r12a30-s1` founded normally (409 alive by t=4,600, 1,442 at t=25,100, mean depth -1.5 to -3 m,
no drowning) - so the cell-30 fix holds - and **S1 fails**: `J/m3 here` at t=10,000-25,000
sat at 0.06-0.4 J/m3, against a predicted >= 2, while the deep larder rose to 13.6. The
mechanism is in the roll's shape, not in the seed: w(d) = speed x |A(t)| x sin(pi d / H) is
zero at the waterline by construction (the anti-0022 property), so at 0.1 m/s and mean |A|
of ~0.6 the upward leg outruns the 0.02 m/s sink only below d ~ 3.2 m - and the neutral
film lives at 2-3 m, inside the dead zone the profile leaves at the top. V1 and V4 held
(header exact, audit 0.0000%), so the pre-registered response fires: **speed 0.1 -> 0.3 m/s**
for every remaining arm; the delivery depth comes up to ~1 m, the circuit shortens to ~700 s,
and creatures cross patches every ~30 s. Period stays 6,000. `r12b30-s1`, launched minutes
earlier at 0.1, is stopped and does not count. Relaunched arms: `r12x-sN` (A at 0.3) and
`r12y-sN` (B at 0.3); `r12a30-s1` runs to budget as the speed-0.1 record. S1-S5 unchanged.

**S1 fails at 0.3 m/s too, and the mechanism is the roll's floor, not its top (2026-09-03,
`r12x-s1` at t=14,600, V1 and V4 held).** Surface detritus came up from 0.06-0.4 to
0.24-0.53 J/m3 - the dead zone at the top did close - and stopped there, five times short of
the 2 J/m3 S1 named, while the deep larder rose to 11.7. The budget table says where the
food went: with the rolls on, the *floor's* share of detritus is 2.2%, against 8.1% in the
still round-11 world, yet total detritus grows at the same rate and the 54 m reading climbs
as fast as ever - the remains are piling in the dark half of the column, below the cell and
above the floor. A roll that stops at 30 m is a trapdoor: on the down leg a parcel reaches
the cell's floor in ~150 s, where w is zero by the same construction that makes it zero at
the surface, and the 0.02 m/s sink carries it out of the roll for good. Nothing lifted on
the up leg was ever below 30 m, so every circuit loses whatever touched the bottom, and the
surface holds one circuit's worth of fresh deaths and no more. The roll is a conveyor to the
deep. Raising the speed shortens the circuit without closing the trapdoor, so the
pre-registered "dose goes up" reading is spent: S1 is falsified by mechanism at both doses,
and the next lever is a world rule (below), the owner's to choose.

Two levers close the loop, and they are not the same bet. **(i) Stokes for the remains**:
0.02 m/s is ~1,700 m/day, a large-aggregate rate; the remains of a 0.01 m3 body are marine
snow at metres per day. At 0.002 m/s the trapdoor leaks ten times slower, remains circulate
in the lit roll for ~10 circuits, and dissolved matter - which sinks at the same 0.02 and is
the surface famine's other half - stays where the bodies are. One knob (`EVOSIM_SINK`,
`EVOSIM_MATTER_SINK`, both added today, header `sink 0.002 m/s, matter 0.002 m/s`). **(ii) A
second roll below the first** (the reading S2 pre-registered): the lower 30 m rolls too, so
what falls through the trapdoor is lifted back to the interface, where the upper roll's
down leg... does not take it - stacked rolls share a zero-w interface, and material crosses
it upward only by diffusion. (ii) is a geometry change whose delivery to the film is
uncertain; (i) is the physics D064 already used for bodies, applied to their remains.
Recommended: round 13 = arm B's world (rolls 0.3 + excretion 0.01) + sink 0.002 for both
fields, five seeds, with round 12's x/y arms as the sink-0.02 controls; and round 12 stops
at the two arms running (`r12x-s1`, `r12y-s1`, to budget), since eight more seeds would
replicate an S1 failure the mechanism already explains. S3 (the famine) is still read from
`r12y-s1`.

**Arm A's record (`r12x-s1`, budget reached 2026-09-03).** 1,578 alive at t=30,000, mean
depth −3.6 to −5.6 m, mean age 3,300 → 10,900 s, audit 0.0000% throughout; one mutant
absorptive alive at the end, none ever inherited. Surface detritus 0.16–0.90 J/m³, deep 16.7,
floor share 2.1–2.8%. S2 holds for the one seed that ran; S1 and S4 fail. Arm A stops here.

**The first chain since round 8, and how it died (`r12y-s3`, dissected from `lineage.jsonl`
by a subagent; report in `scratch/`).** One mutant absorptive, id 971, was born at t=6,961 by
reproduction from a producer parent, on patch 3, in surface water at ~1 J/m³. Its line ran
four generations — 971 → 1403 → 1741 → 2470, born 6,961 / 8,840 / 10,100 / 13,155 — as a
strict single-child chain with no branching, every member unjointed, on patch 3, and dead of
starvation at 7,723 / 8,250 / 8,244 / 9,496 s of age. The last member had no child and died at
t=22,650; the inherited count never exceeded 3. Twenty-two other absorptives were founders
(dead within 430 s, childless) and three later mutants (t=15,221–18,018) had no children by
the copy. Only one `DeathCause` exists in the code (`Starved`), so cause of death carries no
information here; and the lineage rows carry no per-creature depth, volume or energy — the
snapshots hold genome graphs, not phenotypes, and cannot be joined to lineage ids.

What the numbers say against the producers born in the same window (n=2,318): the
absorptives lived *longer* (mean age at death 8,428 s against 6,310) and bred *less* (0.75
children per creature against 0.93; the population's modal brood is one). The genome that
ran four generations unchanged carries two photosynthetic nodes beside the absorptive one, so
the line was a mixotroph, and it neither starved faster nor grew: it drifted out. That is the
reading the pre-registration called "the gate is inside the mutant", sharpened — at
0.4–1.2 J/m³ of surface food, eating detritus adds nothing an absorptive can turn into a
second child, and a line whose members average under one child each is a random walk with
an absorbing wall at zero. Ten individuals at the last sample need a line whose reproductive
number is above one, which is a food question before it is a genome question, and the sink
proposal is aimed at exactly it. The dissection's one measured surprise: absorptives were
jointed 8 times in 29 against 31 in 3,792 for everyone else — small n, flagged, not read.

---

## Results (2026-09-03): 0 of 6

Six arms to budget (arm A: `r12x-s1`; arm B: `r12y-s1..5`), all at speed 0.3 and cell 30;
headers verified (V1), floor 0 after founding (V3), audit 0.0000% every sample with advection
on (V4), no wedge (V5). The cell-60 and speed-0.1 arms stand as measurements, not seeds.
Producers alive at t=30,000 in every arm: 1,578 (A) and 1,696–1,772 (B).

| # | prediction | arm A (x-s1) | arm B (y-s1..5) | verdict |
|---|---|---|---|---|
| S1 | surface detritus ≥ 2 J/m³ at t=10,000–20,000, patch sd non-zero and moving | 0.16–0.90; sd 0.4 → 4.4 | film seeds (s1, s3): 0.14–1.03; mid-water seeds (s2, s4, s5): 1.3–3.2, over 2 in about half the samples; sd 0.3 → 5.6 | **falsified** as written (the trapdoor); the stirring itself is real — patch sd rose from ~0.4 to 4–6 J/m³ in every arm |
| S2 | ≥ 4 of 5 arm-A seeds reach budget alive | 1 of 1 | 5 of 5 | holds on the seeds that ran |
| S3 | refused conceptions at t > 10,000 fall ≥ 5× against arm A | 105k–189k | s2 34k–65k (3–4×), s4 12k–128k, s5 42k–155k, s3 53k–186k, s1 161k–219k (none) | **falsified**: 1–4×, seed-dependent |
| S4 | first inherited absorptive in ≥ 3 of 5 seeds in one arm | 0 | 1 of 5 (s3, t=8,840–22,650) | **falsified** |
| S5 | ≥ 3 of 5 pass D063 | 0 | 0 | **falsified** |

**Two depth regimes in one arm.** Arm B's seeds split at founding and never crossed: s1 and
s3 formed the surface film every D064 world has formed (mean −2 to −4 m, sd 6–8), while s2,
s4 and s5 settled at −14 to −15 m — the middle of the roll — with sd 5 and stayed there to
budget, at 28–36% shade and 1,700–1,780 alive. The mid-water seeds saw 2–3 J/m³ at their
mean depth for much of the run and produced zero inherited absorptives; the film seeds saw
under 1 and produced the one line that came and went. So the breeding bar for an absorptive
line is above 3 J/m³ in stirred water, which agrees with the ~7 J/m³ the deep larder had to
reach in round 11 (X2) and with the line's economics above. What decides which regime a seed
falls into is not measured — the founder draw is the obvious candidate — and a per-guild
depth cannot be read from the lineage (CLAUDE.md's gotcha). Note also that `J/m3 here` is
the detritus field at the population's *mean* depth in patch 0, not an average over
creatures; with patch sd at 4–6 it is a coarse proxy, and the mid-water figures are the
better-founded of the two because that population's spread is narrower.

**What the round established.** The rolls reach the physics and stir the fields (patch sd),
carry bodies (a mid-water population is impossible in still water under D064), and keep
producers alive at 30 m cells (S2). They do not deliver the larder to the film, because a
roll that stops above the floor is a conveyor to the deep. Excretion 0.01 cuts refusals by
1–4× and does not end the famine (S3). One line formed in the film at ~1 J/m³ and drifted out
at 0.75 children per member. The round's pre-registered readings point the same way from
three sides — S1's trapdoor, S3's partial famine, and the line's economics — at the sink
speed of remains and matter, proposed above as round 13 and awaiting the owner's ruling.
