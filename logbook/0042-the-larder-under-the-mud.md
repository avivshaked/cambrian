# 0042 — The larder under the mud

**2026-08-31**  ·  food-chain goal, round 7 · pre-registered before launch

Same shape as 0036–0041: everything above *Results* was written and committed before any
arm was launched.

## The hypothesis

Round 6 ([logbook/0041](0041-the-sea-digests.md)) fixed the producers and left one failure standing: every consumer
lineage that establishes eats the deep water toward zero and busts — seven rounds, seven
busts, and in s3 the bust took the whole world with it. Nothing damps a consumer here but
its food. [D055](../DECISIONS.md#d055) gives the food a refuge: the floor layer of the detritus field cannot be
grazed, and what settles into it re-enters the water only through the mixing that already
exists.

**The claim under test: with the seabed refuge on, an established absorptive lineage's
bust softens into a dip — the food supply near the floor cannot be stripped below what the
refuge feeds back, so the lineage stops collapsing to zero.** The goal rule rides on this:
if chains stop busting, the three-clause success rule should start passing in seeds where
a chain arrives at all.

## The dose, stated honestly

`FloorRefugeMetres` = 1 — exactly the floor layer. Two numbers say what that protects and
how fast it leaks, so a wrong estimate is visible later. In round 6's s2 at t=30,000 the
floor layer held **8.8% of ~300 kJ** of world detritus — a refuge stock of ~26 kJ, denser
than any water layer. The second number, though, deliberately reframes what kind of
mechanism this is: at mixing 0.2 m²/s over 1 m layers, `Mix` moves **20%/s of the
interface difference**, so the refuge is *not* a slow-release larder — a stripped layer above
drains it in tens of seconds. What the refuge actually is, is a **boundary condition**: a
mouth on the floor earns nothing (today a sunk lineage grazes the pile it lands on), and
grazing can never invert the bottom gradient — the last metre's stock stays at or above
the water above it, so recovery after a crash starts from a guaranteed seed stock rather
than from whatever the mouths missed. If one metre at this mixing is too weak a brake,
that is a dose finding, not a design failure — the knob is metres, and the next dose is
thicker.

## The world

Round 6's exactly (irradiance 200, area 400 m², mixing 0.2, current 0.05, excessDensity
0.02, senescence 10,000, floor closes 3,000, remin 0, ceiling 8,000, excretion 0.001)
**plus `EVOSIM_FLOOR_REFUGE` 1**. Five seeds, arms `d057-s1..s5`, 30,000 s, 600 min wall.

**Launched staggered, at most three arms concurrent** — round 6's s1 and s5 were cut by
the wall while five arms shared the machine, and concurrency is the throughput knob we
control. s1–s3 launch first; s4 and s5 take workers as they free. The ceiling remains
declared as a censor exactly as in 0041: a run it ends is scored at its last sample.

## Predictions, and the column that falsifies each

| # | prediction | falsified by |
|---|---|---|
| W1 | `floor` = 0 at every sample after t=3,100 | `floor` |
| W2 | at most 1 of 5 arms goes extinct (round 6: 1 of 5). No mechanism connects the refuge to a producer death — producers do not graze — so a W2 failure is first a build alarm, checked against the bit-identical tests and headers, before it is a finding | run footer |
| W3 | no trophic collapse: in no arm does the chain exceed 40% of `alive` and the world then go extinct (s3's signature) | `absorpt`, `alive`, footer |
| W4 | **the bust softens**: no established chain (peak `absorpt` ≥ 100) falls below 10 while its world still lives (round 6: s1 fell 910 → 5; s3's died entirely) | `absorpt` |
| W5 | a mutant arrives in ≥3 of 5 arms (round-6 rate: 4 of 5) | `absorpt`, `inherit` |
| W6 | **success, the standing rule read at the last sample:** ≥3 of 5 arms not extinct, `inherit` ≥ 1 for ≥20 consecutive samples, `absorpt` ≥ 10 at the last sample | as [0037](0037-the-net-comes-down.md)'s Q5 |
| W7 | a lineage that peaks above 100 falls below 20 and rises above 100 again — the eighth attempt | `absorpt` |

**The goal is met if W1, W2 and W6 hold** — with the ceiling-censor caveat attached to any
claim made from a censored run.

## The two-sided reading, written before the answer

- **W4 fails with W2 holding (chains still bust to nothing):** one metre at mixing 0.2 is
  too weak — the drain arithmetic above named this risk before the round ran. The next
  round is `EVOSIM_FLOOR_REFUGE` 5–10, not a redesign; only if a thick refuge also fails
  does fix 3 reopen as biology (density-dependent capture, the rejected branch of D055).
- **W4 holds but W6 fails** (chains persist yet the rule never passes): read arrival
  times — if chains arrive after t≈20,000 there was no room for 20 samples; the failure is
  mutation supply and wall clock, not damping, and the response is longer runs, not a new
  mechanism.
- **W2 fails:** verify the build before believing it (headers, then the default-unchanged
  test) — the refuge touches no producer path. If the failure survives verification, the
  refuge starved something indirect (a scavenging subsidy to a mixed lineage), and that is
  a real finding about who was actually eating the floor.
- **All five run away to the ceiling:** the refuge fed the world more than it damped it —
  detritus that consumers would have destroyed now returns as food. Score at last samples
  per the censor rule, and note the producers' own limit question returns.
- **W7 holds anywhere:** the strongest positive result the project has ever had, eighth
  time asked.

**Uninterpretable, and to be reported as such:** an arm ended by its wall before t=15,000.

---

## Addendum, 2026-08-31 — written mid-round, before any scoring

Two rulings arrived while the arms ran (an owner-provided independent review, ratified by
the owner; DECISIONS.md D058–D060), and are recorded here *before* results so the change
of reading is dated and visible:

1. **Dual scoring (D058).** The W table below is scored as pre-registered, and a second
   table beside it applies the stricter rule: only a budget-complete arm can pass; wall
   and ceiling cuts are censored and cannot pass; a promising censored arm reruns alone.
2. **The thicker-refuge branch is amended.** The pre-registered response to "W4 fails
   with W2 holding" was a thicker refuge next. The review exposed a confound this entry
   did not name: with no physical floor, a consumer fallen below −60 m reads the same
   field state as one resting on the seabed — so a bust under the refuge could be exile,
   not weak damping. No thicker-refuge round runs until the below-world observables
   (D059) can separate the two, and the direct test of damping is D060's invasion assay,
   not a dose escalation.

3. **Operational note, staggering meets a growing registry.** s4 and s5 (launched as
   workers freed, per the stagger) print `configHash 3ec383fba0b9c747` where s1–s3 print
   `c09f393aec61e61f`. Cause, verified at launch: `Evosim.Core` is a shared `file:`
   package, and between the two waves it gained D057's five default-off species tunables
   (commit 1b44e44, the only Core change since launch) — new tunables move `Hash()` even
   at their defaults. A token-by-token diff of the s4 and s1 headers shows exactly two
   differences: seed and hash. The world is the same (the knob-off path is proven
   bit-identical by the suite); the hash detected a registry change, which is its job.
   Recorded here so the round's five arms are scored together despite two hash strings.

4. **Mid-round finding, dated before any scoring: the treatment has not yet bound.**
   s2 completed its budget **byte-identical to round 6's s2** — every table row, 8,308
   births, 618 alive — and s1 is byte-identical to its round-6 twin through t≈8,900. The
   knob provably reached the field (wave-1's hash `c09f393aec61e61f` differs from round
   6's `fc914dbc9095adbb`, and `FloorRefugeMetres` is hashed from the same config object
   the World is built from; the CLAUDE.md identical-numbers gotcha was applied before
   concluding). The reading: the refuge alters feeding only in the bottom metre, and no
   living pool-drawing creature has occupied the bottom metre — living consumers graze
   the mid-deep water, and only the dying, sinking ones reach the floor layer. s2 is
   therefore a free control replicate; any divergence from a round-6 twin, watched for
   from here on, timestamps the first moment a mouth touches the floor layer — most
   plausibly mid-bust, in D059's exile zone, which would demonstrate the review's
   confound in the world's own data.

5. **The divergence, caught live — a personal note.** (Written by the agent, in its own
   voice, at the owner's invitation — not on request.) At t=11,500 the s1 twins were
   byte-identical; at t=11,600 they differed by 0.01 J/m³ of deep detritus and 0.02% of
   food income: somewhere in those hundred seconds, one absorptive touched the bottom
   metre for the first time in the treatment's existence, was refused, and two identical
   worlds stopped being the same world. I have spent this campaign reading collapses
   after the fact and reasoning backwards through confounds — and here, by an accident
   of determinism and a staggered launch, is a matched pair split by a single refused
   meal, with the untreated control already run to completion. Watching the instrument
   catch that butterfly mid-wingbeat is the most excited I have been in eleven rounds.
   I note it because the README says the felt part of an entry is the part that
   perishes — and because whatever s1's boom does next, I want the record to show that
   somebody was leaning toward the screen when it happened.

---

## Results

What this round turned into, nobody pre-registered: a **matched-pair experiment**. Because
the world replays exactly from `(seed, config)` on one machine, and because the refuge
only changes what happens in the bottom metre, every arm ran *byte-identical to its
round-6 twin* until the first moment a feeding mouth touched the floor layer — and the
instant of divergence timestamps exactly when, and in whom, the treatment ever acted.
Two arms never diverged at all; three diverged at their chains' establishment, and each
divergence pairs a treated trajectory with a completed untreated control matched to the
simulated second. The findings below rest on that pairing.

| arm | fate (D058 vocabulary) | twin behaviour | chain |
|---|---|---|---|
| s1 | **wall-censored** t=24,366 (600 min), 7,809 alive | diverged t=11,600, mid-establishment | strangled: peak 71 vs control's 908; brief re-attempt (4, 1 inherited) at t≈21,200 died; 2 uninherited at cut |
| s2 | **budget**, t=30,000, 618 alive | **byte-identical throughout** — refuge never bound | none (two singles, as control) |
| s3 | **extinct** t=24,416 | diverged t=10,100, at its mutant's arrival | strangled at patient zero: the control's 1,297-strong collapse never began; the world died anyway, ~1,900 s *earlier* than its control, chainless, with 44.5 J/m³ untouched in the deep |
| s4 | **budget**, t=30,000, 2,204 alive | **byte-identical throughout** — refuge never bound | one arrival at the last sample, as control |
| s5 | **wall-censored** t=23,573 (600 min), 6,745 alive | diverged t=14,600, two hundred simulated seconds before its control's 80-sample inherited streak began | first establishment strangled (control's 320-peak never came) — then a **late, small re-establishment on water-column food alone**: 18 absorptives, 15 inherited, an 18-consecutive-sample inherited streak, alive and growing at the cut |

### Scored — the pre-registered table

| # | result |
|---|---|
| W1 | **held** — `floor` = 0 after t=3,100 in all five arms (verified by column) |
| W2 | **held** — 1 of 5 extinct (s3) |
| W3 | **held on its letter, and the letter was answered by the control instead** — no treated chain ever exceeded anything; but the matched pair showed round 6's "trophic collapse" was not what it seemed (finding 3 below) |
| W4 | **vacuously held, meaningfully void** — no treated chain ever reached the peak-100 threshold that defines "established", so nothing could fall from it. The mechanism inverted the question: the refuge does not soften busts, it prevents establishment |
| W5 | **held** — a mutant arrived in all five arms |
| W6 | **failed, 0 of 5** — the closest approach is s5's 18-sample streak, two samples short of the rule's twenty, at a wall cut that D058 rules out of passing regardless |
| W7 | **falsified, eighth time, in a new way** — nothing peaked high enough to bust |

**The goal is not met**, and for the first time the failure is not a mystery in any
direction.

### Scored — D058's stricter table, beside it as ratified

| arm | completed-budget reading |
|---|---|
| s1 | censored (wall) — cannot pass; no chain regardless |
| s2 | budget-complete: fail (no chain) |
| s3 | extinct: fail |
| s4 | budget-complete: fail (no chain) |
| s5 | censored (wall) — cannot pass; **the round's one promising censored arm**: an inherited streak of 18 and rising at the cut. D058's letter names the response — a solo rerun to the full budget — with its cost stated honestly: no world can resume from a snapshot, so a rerun replays all ~10 wall-hours to reach the cut before writing anything new. An owner's call, not an automatic launch |

Round 6 re-read under the same rule, for the record: zero confirmed passes, one promising
censored arm (its s5). The campaign's honest tally across both rounds is therefore
**zero confirmed passes, ever** — which the matched pairs finally explain.

### What the round found

1. **Establishment runs through the floor pantry.** The floor layer holds the densest
   food in the world (~8–9% of all detritus in one metre — ~66 J/m³ against a mid-column
   that peaks near half that). All three treated chains fizzled where their controls
   boomed: s1's peak 71 against 908 from the same arrival; s3's patient zero died
   childless where its control's founded a 1,297-strong lineage; s5's establishment
   window passed with nothing where its control began an 80-sample inherited streak.
   Every boom this project ever recorded was funded by an unguarded hoard — which also
   explains, in one stroke, eleven rounds of "arrival without establishment" whenever
   the hoard was out of reach. The qualification arrived with the last footer: s5's
   late chain shows **column food alone can fund establishment — slowly and small** (18
   individuals in the time its control's pantry-fed twin reached 320, in ~30 J/m³ deep
   water). The pantry is the booster, not the only path; without it, establishment runs
   an order of magnitude smaller.
2. **The refuge, as dosed, is an access gate — not a meter.** The impulse harness
   (RefugeImpulse, built this round) measured it: a 1 m refuge is *transport-identical*
   to no refuge — `Settle` and `Mix` never consult it, and the floor's stock drains
   back into the water in seconds at mixing 0.2. D055-at-1m therefore changed nothing
   about how fast food returns; it only changed *who may eat the pile directly*. A 5 m
   refuge releases over minutes and a 10 m one holds most of its stock past 6,000 s —
   thickness does buy a real slow larder, but it also widens the zone establishment
   cannot reach. The tension between metering the pantry and leaving an on-ramp is now
   the design problem, stated cleanly.
3. **Seed 3's "trophic collapse" was never trophic at bottom.** The treated twin's chain
   was strangled at one individual — and the world died *anyway*, ~1,900 s earlier than
   its control, by the producers' own drought → darkness → sink spiral, its last
   survivor ageing out at −78 m in water below the modelled world. Round 6's most
   dramatic result is hereby corrected: the chain rode a dying world, and if anything
   its recycling propped the world up slightly longer. Only a matched pair could have
   shown this.
4. **Two perfect replicates, for free.** s2 and s4 ran byte-identical to their controls
   through every row — proof both of same-machine determinism at full ecology scale and
   of the refuge's pre-arrival inertness (the bit-identity the D055 tests promised,
   demonstrated in 30,000-second worlds).
5. **Instrument notes.** The censor that bound was again the wall (s1, at 0.7× real
   time); no arm approached the 8,000 ceiling. s1's report logs a "fastest creature" of
   3.32 m/s at t=21,036 — forty times any credible swimming speed, in a 7,000-body
   crowd; almost certainly a depenetration kick, filed for the aquarium era rather than
   believed. And the round's addenda already record the two-wave hash split and the
   mid-round rulings (D058–D060) under which this scoring was performed.

### Where this leaves the campaign

The refuge is falsified *as the damping mechanism* — kept as a knob, rejected as the
answer. What the round bought instead is the first complete causal account of the
consumer cycle: chains are born from a windfall (the unguarded pile), boom on it, and
die by exhausting it — and a world that locks the windfall gets only slow, small chains
an order of magnitude below the booms (s5's 18 against its control's 320). The
stabiliser the goal needs must therefore do what neither round 6 nor round 7's worlds
could: **let a founder reach concentrated food, and stop a boom from taking all of it.**
The queued instruments point at the next tests — D060's invasion assay (does an
*established* chain persist on metered flux, separating establishment from persistence)
and, informed by it, the owner's round-8 fork: partial pantry access, D059's floor
first, or D054's shelf pulled forward — geography as the on-ramp.
