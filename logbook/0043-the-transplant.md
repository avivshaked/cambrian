# 0043 — The transplant

*2026-09-01. Pre-registered before launch; results appended after. Arms `d060-s2r0`,
`d060-s2r1`, `d060-s4r0`, `d060-s4r1`. Decision under test:
[D060](../DECISIONS.md) (the invasion assay), probing [D055](../DECISIONS.md) (the seabed
refuge).*

**This is a diagnostic, not a goal attempt.** D060 ratified inoculation as a labeled
instrument: a hand of god places a proven consumer into the world, so nothing these arms
do can count toward the standing 3-of-5 goal, which requires chains that arise on their
own. What they can do is answer the question round 7 could not.

## The question round 7 left open

Round 7 ([0042](0042-the-larder-under-the-mud.md)) ended in a clean fork. Consumer chains
establish by grazing the floor pantry — the densest food anywhere — and the refuge, built
to damp their boom-bust cycle, turned out instead to be an access gate: it stopped chains
from *starting* and never got to show whether it stops them from *dying*. Every treated
establishment was strangled at birth; every untreated one boomed and busted. No arm ever
held an established chain under the refuge, so the mechanism it was designed to test went
untested.

The assay skips establishment. Take one verified absorptive genome from round 6, inject
the same small inoculum into paired worlds — one with the refuge, one without — at the
same simulated moment, and watch what each world does to a chain that already exists.

## Why seeds 2 and 4

Both worlds survive to budget in this configuration (round 7: 618 and 2,204 alive at
t=30,000) and neither ever grew a natural absorptive lineage — s2's `inherit` column is 0
at all 300 samples, s4's peaks at a single blip of 1. So after injection, **every
inherited absorptive is a descendant of the inoculum**; the readout is unconfounded by
natural arrivals. That the treatment never bound in these seeds in round 7 (both ran
byte-identical to their round-6 twins) is exactly what makes them clean assay chambers.

## The instrument, and the dose stated honestly

`World.Inoculate` develops N copies of a stored genome and admits them on the
floor-founder pattern: endowment `FounderEnergyJoules` (200 J), energy credited to
`EnergyIn` so the audit still closes, no matter debt, generation 0, no parent — which is
what makes their offspring land in the `inherit` column. Three hashed tunables
(`InoculateAtSeconds`, `InoculateCount`, `InoculateDepthMetres`) plus the genome file,
whose SHA-256 prints in the header — an arm whose inoculum did not arrive is visible in
its own report.

The inoculum: **5 creatures at t=8,000, at −50 m**, from `inocula/d056-s5-absorptive.json`
(SHA-256 `e6f8e4da1edb…`, copied byte-for-byte from round 6 s5's final snapshot at
t=22,721 — a creature that was alive in the campaign's only surviving late chain). The
genome carries three nodes but develops to a **one-part body**: a single absorptive
sphere, ~0.023 m³, brood 2 — and that is not an unlucky pick. All 50 absorptive genomes
in that snapshot of 4,927 develop to solitary absorptive blobs; development prunes
everything else for volume. The consumer this ecology actually evolved is a single-celled
filter feeder, and that is what we transplant. At t=8,000 both worlds are
alive and past the floor era (s2: 216 alive, 101.6 kJ detritus standing, 14.3 J/m³ in the
deep layers; s4: 552 alive, 55.3 kJ, 6.1 J/m³), and the injection adds ~1–2 kJ of new
energy — one to four percent of the standing larder. The assay measures **access**, not
enrichment.

## The world

Round 6's exactly, passed explicitly rather than by default: irradiance 200, area 400 m²,
mixing 0.2, current 0.05, excessDensity 0.02, senescence 10,000, floor closes 3,000,
remin 0, ceiling 8,000, excretion 0.001. Per pair: `EVOSIM_FLOOR_REFUGE` 0 (control) or 1
(treatment). Budget 20,000 s, wall 600 min. All four arms concurrent on freshly refreshed
workers — their combined population (≤ ~2,500 each, by the round-7 trajectories of these
seeds) is below what three round-7 arms carried at once.

The config hash will differ from both round-7 waves: the workers pick up the species
columns and the inoculation tunables together. This is the documented pattern
([0042](0042-the-larder-under-the-mud.md), addendum 3) — the world is the same when the
knob-off tests say so and the header tokens differ only where the treatment says they
should.

## Validity checks, before any prediction is read

| # | check | read from |
|---|---|---|
| V1 | before t=8,000 each arm replays its round-7 twin token-for-token (same seed, treatment not yet applied — refuge 0 arms also match, since round 7's refuge never bound in these seeds) | table rows vs `d057-s2.md` / `d057-s4.md` |
| V2 | within each pair, r0 and r1 stay byte-identical until the first refuge binding after injection; the divergence timestamp is the moment the treatment first mattered | row diff |
| V3 | `floor` = 0 at every sample after t=3,100 — the floor stays closed; the only hand of god is the registered one | `floor` |

A V1 or V3 failure is a build alarm before it is a finding.

## Predictions, and the column that falsifies each

| # | prediction | falsified by |
|---|---|---|
| X1 | the inoculum lives: `absorpt` ≥ 1 at the first sample after t=8,000 in all four arms | `absorpt` |
| X2 | **it establishes**: `inherit` ≥ 1 by t=10,000 in at least 3 of 4 arms — a proven genome placed next to concentrated food breeds within 2,000 s | `inherit` |
| X3 | **the control busts**: in at least one r0 arm the lineage booms (`absorpt` peak ≥ 100) and then falls below 10 while the world lives — the natural cycle, reproduced on demand | `absorpt` |
| X4 | **the treatment persists**: in at least one r1 arm, `inherit` ≥ 1 for ≥ 20 consecutive samples *and* `absorpt` ≥ 10 at the last sample — the goal metric, hit by a hand-placed chain | `inherit`, `absorpt` |
| X5 | **the refuge meters**: within each pair, the r1 peak `absorpt` is below the r0 peak | `absorpt` |

## The two-sided reading, written before the answer

- **X2 and X4 hold, X3 holds:** the refuge is a persistence mechanism whose only failure
  in round 7 was the on-ramp. Round 8 becomes *establishment access + refuge* — a partial
  pantry (fractional edibility rather than zero) is the leading design.
- **X2 holds, X4 fails, X3 holds (busts everywhere):** the refuge does not rescue an
  established chain either; the cycle is deeper than floor access, and the round-8 fork
  tilts toward [D059](../DECISIONS.md)'s physical seabed or [D054](../DECISIONS.md)'s
  shelf — changing what the world *is*, not what may be eaten.
- **X2 holds in r0 only (the treatment kills the transplant):** one metre of refuge
  starves even an established consumer at these column densities — D055 rejected as a
  world rule at this dose, and the partial-pantry redesign becomes the *only* branch.
- **X1 or X2 fails everywhere:** even a proven genome cannot make a living at t=8,000
  densities. That answers the owner's standing question — *world problem, not mutation
  problem* — at a stroke, and the follow-up is a later injection time (a fuller larder),
  not a different genome.
- **X3 fails (no boom in the controls):** these seeds' worlds do not support the boom at
  all; the round-6 busts were seed-specific, and the assay needs s1 or s3's world instead.
  A quiet, honest miss — rerun, do not overread.

## Scoring

Per [D058](../DECISIONS.md): only budget-complete arms answer; an extinct arm is a
failure of its world, not of the assay; a wall- or ceiling-cut arm is censored and its
predictions are read only up to the cut. Results appended below when the arms land.

---

## Mid-round addendum — the instrument verified (t≈8,100, all arms)

All four headers carry the treatment (`inoculate 5 @ 8000 s, 50 m, genome e6f8e4da1edb`),
pairs share config hashes as they must, and the checks read:

- **V1 holds.** All 79 pre-injection rows in all four arms replay their twins
  token-for-token on the shared 36 columns (the d060 reports append seven newer columns;
  the comparison is on the common prefix). One false alarm first: the checking script cut
  the rows one field wide, keeping the new `species` column against the twin's trailing
  emptiness, and flagged every row. The instrument was right and the ruler was bent —
  worth recording because the pre-registration's own rule ("a V1 failure is a build alarm
  before it is a finding") is what forced the second look.
- **X1 holds.** `absorpt` = 5 in all four arms at t=8,000 and t=8,100 — the transplants
  are alive. `inherit` still 0 at 8,100: no descendants yet.
- **V2 in progress.** Within each seed the pair is still identical at t=8,100, and the
  first divergence from the *twins* lands exactly at the injection row (s2: 262 alive vs
  259 — five inoculants minus what their competition displaced). The hand moved the world
  at precisely the pre-registered instant and nowhere earlier.

---

## Results

All four arms ran to their full 20,000 s budget — no wall cuts, no ceiling, no
extinctions. Under [D058](../DECISIONS.md) every arm answers. V2 completed cleanly:
**both pairs diverged at t=8,900**, nine hundred simulated seconds after injection, and
V3 held (`floor` = 0 after t=3,100 everywhere).

That t=8,900 number is itself the round's first finding. The transplants were placed at
−50 m, in the water column. Within ~900 s they were feeding at the floor — in both
seeds, at the same sample. The consumer this ecology evolved is not a drifting filter
feeder that happens to visit the bottom; it is **benthic**. It sinks (tissue runs
0.02 kg/m³ over water), lands on the pile, and eats where it lands.

### The fates

| arm | treatment | lineage established | peak `absorpt` | at t=20,000 | lineage fate |
|---|---|---|---|---|---|
| d060-s2r0 | none | t=9,700 | 135 | **56 alive, 56 inherited** | alive, declining |
| d060-s2r1 | refuge 1 m | never | 5 (the transplants) | 0 | **extinct by t=13,100, zero descendants** |
| d060-s4r0 | none | t=11,300 | 121 | **93 alive, 90 inherited** | alive, declining |
| d060-s4r1 | refuge 1 m | never | 5 (the transplants) | 0 | **extinct by t=11,400, zero descendants** |

In both control worlds the five transplants sank, grazed the floor pantry, and founded a
lineage that boomed past 100. In both treated worlds the same five creatures sank to the
same floor, sat on ~13 kJ of food they were forbidden to price, and starved — the water
column at their depth (~0.7–1.3 J/m³) kept them alive for 3–5 thousand seconds but never
funded a 281 J brood. Not one descendant, in either seed.

### The predictions, scored

| # | prediction | verdict |
|---|---|---|
| X1 | transplants alive at first sample | **holds**, all four arms |
| X2 | `inherit` ≥ 1 by t=10,000 in ≥3 of 4 | **fails** — 1 of 4 (s2r0 at 9,700; s4r0 took until 11,300; the treatments never). Establishment is real but slower than the 2,000 s the pre-registration guessed |
| X3 | a control booms ≥100 then busts <10 in-budget | **fails** — both controls boomed (135, 121) but ended alive at 56 and 93, declining as the deep larder drew down from ~12 to ~2–4 J/m³. The bust is on its way; 20,000 s did not contain it |
| X4 | a treated lineage persists (20-sample streak, ≥10 at end) | **fails** — both treated lineages extinct without a descendant |
| X5 | treated peak < control peak | **holds** trivially, 5 vs 135 and 5 vs 121 |

### The reading

This is the pre-registration's third branch, verbatim: *"one metre of refuge starves
even an established consumer at these column densities — D055 rejected as a world rule
at this dose."* The refuge is not a meter, not a damper, and not a stabiliser that
lacked an on-ramp. For a benthic consumer — the only kind this ecology has ever made —
it is total exclusion from the only food dense enough to live on.

Three things the paired structure makes unambiguous:

1. **The world can hold a chain; the mutation supply is what never delivered one to
   seeds 2 and 4.** Round 7 ran these worlds 30,000 s and no absorptive ever bred.
   Hand one verified genome across the establishment gap and the chain builds itself in
   both seeds. The owner's question — world problem or mutation problem? — splits
   cleanly: *establishment* is a world problem (the on-ramp is narrow and the refuge
   closed it); *arrival* is a mutation-supply problem.
2. **The controls satisfied the goal-shaped metric at this horizon** — post-injection
   inherit streaks of 104 and 88 consecutive samples, both far past 20, with 56 and 93
   alive at the last sample. A transplanted chain in the *unmodified* world outlives the
   goal rule's bar for as long as we watched. The honest caveat sits beside it: both
   were declining, the larder under them was nearly eaten, and round 6 says what comes
   after a peak like that when the run is longer.
3. **A consumer-free treated world is the owner's oil field.** With the transplants dead
   and no graze on the deep, s2r1's deep stock climbed monotonically to 30 J/m³ and its
   world swelled to over a thousand producers — energy burying itself in an ungrazeable
   floor, exactly the one-way carbon story the refuge was always going to write once
   nothing could eat it back.

### Instrument replays (added 2026-09-01, before launch)

Review round 4 raised a question this round's data cannot answer as recorded: are the
busts consumer–resource cycles or **cohort cycles** (one dominant generation grazes the
pool below its own break-even and starves — a mechanism neither refuges nor patches
address)? The discriminator is cycle period against consumer generation time, which
needs the birth/death record — and the lineage-events instrument was built *after* the
arms ran, proven inert (a world drained or undrained steps bit-identically, suite 374).

So the two control arms are replayed as `d060b-s2r0` and `d060b-s4r0`: same seeds, same
config, same hash expected — deterministic twins of scored runs, now writing
`lineage.jsonl`. Validity check before any reading: the replay's sample rows must match
the original token-for-token. This is instrumentation of an already-pre-registered
condition, not a new treatment; nothing here touches the round-8 fork.

**Results (same day).** Both replays are perfect twins — config hash `1734ee6d195cd439`
unchanged, zero differing sample rows against the originals — and the lineage record
rewrites this round's most optimistic reading:

| | s2r0 | s4r0 |
|---|---|---|
| clade births (5 inoculants + descendants) | 135 | 122 |
| generation time (parent birth → child birth), median | 1,234 s | 1,276 s |
| **last clade birth** | **t=13,938** | **t=16,915** |
| world (non-clade) births after that instant | thousands | 504 |
| clade state at budget (t=20,000) | 56 alive, sterile 6,062 s | 93 alive, sterile 3,085 s |

**Both control chains were demographically dead long before the budget.** Recruitment
ceased at the population peak in both seeds — after which the "decline" the reports
showed was a sterile cohort aging toward extinction (s4's clade deaths accelerating 5 →
14 → 10 per thousand seconds; two inoculants dying at ~10,500 s, the senescence knee)
while the rest of the world bred freely. The boom is ~4–5 generations long, and it ends
not in a mortality wave but in a **recruitment collapse**: the cohort grazes the pool
below the *reproduction* break-even (a 562 J brood is unfundable at post-boom densities)
while every adult still clears its *survival* break-even — so the population pins its own
food in the gap between the two thresholds: alive, grazing, sterile. Structurally the de
Roos–Persson cohort trap review round 4 flagged ([LITERATURE-REVIEW.md] §9 item 7); the
lineage record cannot separate the energy and matter sides of the refused conceptions,
but both are the same story of at-depth exhaustion under grazing.

Two corrections follow:

1. **This entry's "alive, declining" verdicts on the controls were too kind.** The
   controls did not show persistence with a downslope; they showed a four-generation
   boom, then a walking-dead tail. Round 6's booms should be presumed to carry the same
   hidden structure.
2. **The standing goal rule has a blind spot, now demonstrated rather than suspected:**
   `inherit` ≥ 1 with ≥ 10 alive at the last sample is satisfied for thousands of
   seconds by a lineage whose last birth is long past — s2r0's 104-sample streak was
   sterile from sample ~60 onward. A rule amendment (an absorptive *birth* within the
   last N samples) is the owner's to make; every future scoring should report last-birth
   time alongside the streak either way.

What any round-8 stabiliser must now do is stated by the mechanism: keep some food,
somewhere, above the **reproduction** threshold — above zero is not enough, and above
the survival threshold is exactly the trap.

**One more replay, launched on the same reasoning** (2026-09-01): correction 1 above
says round 6's natural booms "should be presumed" to carry the same structure — a
presumption cheaply convertible to measurement. `d056b-s1` replays round 6's seed 1 (the
campaign's biggest natural boom, 908 → 5, a full cycle including the mortality crash the
assay's budget truncated) with the lineage instrument. It answers: does recruitment
collapse precede the mortality crash in a natural, evolved chain, and by how long?
Validity check as before — rows must match `d056-s1.md` token-for-token on the shared
column prefix (the hash differs: three generations of default-off tunables have entered
Core since round 6; the knob-off bit-identity tests are the bridge). Result appended
when it lands.

D055's knob survives as an instrument; as a *world rule at 1 m* it is falsified twice
over — round 7 showed it blocks establishment, this round shows it kills established
consumers too. The design tension it was built to resolve is still real (round 6's
booms still bust), but the resolution cannot be "close the seabed." The owner's
hypothesis, raised on seeing these results, is that the deeper distortion is
**whole-layer access** — a creature at the right depth feeds from the entire horizontal
extent at once, no travel, no local depletion, which is the perfectly-stirred regime in
which consumer-resource theory predicts exactly the violent cycling we observe, and
which also forecloses any reason for movement to pay. That hypothesis, the literature it
needs, and the round-8 design it implies are the next decision, and it is the owner's.
