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
