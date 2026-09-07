# 0043 — The transplant

**2026-09-01**  ·  invasion assay · pre-registered before launch, results appended after

Two of this project's worlds had run for 30,000 simulated seconds without ever growing a
consumer of their own. I stopped waiting for one to evolve and carried one in. Five copies
of a consumer that a third world had evolved went into each of the two at t=8,000. Each
world was run twice: once as it stands, once with the seabed refuge closed. In the two
unmodified worlds the transplants sank to the floor, ate, and founded lineages that passed a
hundred creatures. In the two closed worlds the same five creatures sat on the same food,
forbidden to touch it, and died without a single descendant.

This is a diagnostic rather than a goal attempt. [D060](../DECISIONS.md) ratified
inoculation as a labeled instrument. A hand of god places a proven consumer into the world.
So nothing these arms do can count toward the standing 3-of-5 goal, which requires chains
that arise on their own. What they can do is answer the question round 7 could not. The
decision under test is D060 itself, probing [D055](../DECISIONS.md), the seabed refuge.

## The question round 7 left open

Round 7 ([0042](0042-the-larder-under-the-mud.md)) ended in a clean fork. Consumer chains
establish by grazing the floor pantry, the densest food anywhere. The refuge was built to
damp their boom and bust, and it behaved as an access gate instead. It stopped chains from
*starting*, and never got to show whether it stops them from *dying*. Every treated
establishment was strangled at birth, and every untreated one boomed and busted. No arm ever
held an established chain under the refuge, so the mechanism it was designed to test went
untested.

The assay skips establishment entirely. I take one verified absorptive genome from round 6
and inject the same small inoculum into paired worlds, one with the refuge and one without,
at the same simulated moment. Then I watch what each world does to a chain that already
exists.

## Why seeds 2 and 4

Both worlds survive to budget in this configuration, at 618 and 2,204 alive at t=30,000 in
round 7, and neither ever grew a natural absorptive lineage. Seed 2's `inherit` column is 0
at all 300 samples, and seed 4's peaks at a single blip of 1. So after injection, every
inherited absorptive is a descendant of the inoculum, and the readout is unconfounded by
natural arrivals. The treatment never bound in these seeds in round 7, since both ran
byte-identical to their round-6 twins, and that is what makes them clean assay chambers.

## The instrument, and the dose

`World.Inoculate` develops N copies of a stored genome and admits them on the floor-founder
pattern. Each transplant carries the founder endowment `FounderEnergyJoules`, which is 200
J.

That energy is credited to `EnergyIn`, so the audit still closes. The transplants carry no
matter debt, generation 0 and no parent. Having no parent is what makes their offspring land
in the `inherit` column.

Three hashed tunables aim the instrument:

| tunable | what it sets |
|---|---|
| `InoculateAtSeconds` | when the hand reaches in |
| `InoculateCount` | how many copies arrive |
| `InoculateDepthMetres` | the depth they arrive at |

All three are hashed into the config, along with the genome file, whose SHA-256 prints in
the header. An arm whose inoculum did not arrive is visible in its own report.

The inoculum is five creatures at t=8,000, placed at −50 m, from
`inocula/d056-s5-absorptive.json`, SHA-256 `e6f8e4da1edb…`. The file was copied
byte-for-byte from round 6 seed 5's final snapshot at t=22,721, a creature that was alive in
the campaign's only surviving late chain.

The genome carries three nodes but develops to a one-part body: a single absorptive sphere
of ~0.023 m³, brood 2. That is not an unlucky pick. All 50 absorptive genomes in that
snapshot of 4,927 develop to solitary absorptive blobs, because development prunes
everything else for volume. The consumer this ecology actually evolved is a single-celled
filter feeder, and that is what we transplant.

At t=8,000 both worlds are alive and past the floor era. Seed 2 has 216 alive, 101.6 kJ of
detritus standing and 14.3 J/m³ in the deep layers. Seed 4 has 552 alive, 55.3 kJ and
6.1 J/m³. The injection adds ~1–2 kJ of new energy, one to four percent of the standing
larder. The assay measures access rather than enrichment.

## The world

Round 6's world unchanged, passed explicitly rather than by default. The settings are
irradiance 200, area 400 m², mixing 0.2 and current 0.05, with excessDensity 0.02.
Senescence is 10,000, the floor closes at 3,000, remin is 0, the ceiling is 8,000 and
excretion is 0.001. Each pair differs in one setting, `EVOSIM_FLOOR_REFUGE`, at 0 for the
control and 1 for the treatment. Budget 20,000 s, wall 600 min.

| pair | control, refuge 0 | treatment, refuge 1 |
|---|---|---|
| seed 2 | `d060-s2r0` | `d060-s2r1` |
| seed 4 | `d060-s4r0` | `d060-s4r1` |

All four arms run concurrently on freshly refreshed workers. Their combined population, at
most ~2,500 each by the round-7 trajectories of these seeds, is below what three round-7
arms carried at once.

The config hash will differ from both round-7 waves, because the workers pick up the species
columns and the inoculation tunables together. This is the documented pattern
([0042](0042-the-larder-under-the-mud.md), addendum 3). The world is the same when the
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

The scoring rules come from [D058](../DECISIONS.md). Only budget-complete arms answer. An
extinct arm is a failure of its world rather than of the assay. A wall-cut or ceiling-cut
arm is censored, and its predictions are read only up to the cut. Results are appended below
when the arms land.

## Mid-round addendum: the instrument verified at t≈8,100

All four headers carry the treatment, printed as
`inoculate 5 @ 8000 s, 50 m, genome e6f8e4da1edb`, and pairs share config hashes as they
must. The checks read as follows.

- V1 holds in all four arms. All 79 pre-injection rows replay their twins token-for-token on the shared 36 columns. The comparison is on that common prefix, because the d060 reports append seven newer columns. There was one false alarm first. The checking script cut the rows
  one field wide, keeping the new `species` column against the twin's trailing emptiness,
  and flagged every row. The instrument was right and the ruler was bent. I record it
  because the pre-registration's own rule, that a V1 failure is a build alarm before it is a
  finding, is what forced the second look.
- X1 holds as well, with `absorpt` = 5 in all four arms at t=8,000 and t=8,100. The
  transplants are alive. Inherited absorptives are still 0 at 8,100, so no descendants yet.
- V2 is in progress. Within each seed the pair is still identical at t=8,100, and the first
  divergence from the *twins* lands at the injection row. Seed 2 shows 262 alive against the
  twin's 259, which is five inoculants minus what their competition displaced. The hand
  moved the world at the pre-registered instant and nowhere earlier.

## Results

All four arms ran to their full 20,000 s budget, with no wall cuts, no ceiling and no
extinctions. Under [D058](../DECISIONS.md) every arm answers. V2 completed cleanly, with
both pairs diverging at t=8,900, nine hundred simulated seconds after injection. V3 held, at
`floor` = 0 for every sample after t=3,100.

That t=8,900 is the round's first finding. The transplants were placed at −50 m, in the
water column. Within ~900 s they were feeding at the floor, in both seeds, at the same
sample. The consumer this ecology evolved is not a drifting filter feeder that happens to
visit the bottom. It is benthic. It sinks, since tissue runs 0.02 kg/m³ over water, lands on
the pile, and eats where it lands.

### The fates

| arm | treatment | lineage established | peak `absorpt` | at t=20,000 | lineage fate |
|---|---|---|---|---|---|
| `d060-s2r0` | none | t=9,700 | 135 | **56 alive, 56 inherited** | alive, declining |
| `d060-s2r1` | refuge 1 m | never | 5 (the transplants) | 0 | **extinct by t=13,100, zero descendants** |
| `d060-s4r0` | none | t=11,300 | 121 | **93 alive, 90 inherited** | alive, declining |
| `d060-s4r1` | refuge 1 m | never | 5 (the transplants) | 0 | **extinct by t=11,400, zero descendants** |

In both control worlds the five transplants sank, grazed the floor pantry, and founded a
lineage that boomed past 100. In both treated worlds the same five creatures sank to the
same floor and sat on ~13 kJ of food they were forbidden to price. The water column at their
depth held 0.7–1.3 J/m³, which kept them alive for three to five thousand seconds and never
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

This round landed on the pre-registration's third branch, word for word:

> one metre of refuge starves even an established consumer at these column densities, and
> D055 is rejected as a world rule at this dose.

The refuge is not a meter, not a damper, and not a stabiliser that lacked an
on-ramp. For a benthic consumer, the only kind this ecology has ever made, it is total
exclusion from the only food dense enough to live on.

The paired structure makes three things unambiguous.

1. The world can hold a chain, and the mutation supply is what never delivered one to seeds 2
   and 4. Round 7 ran these worlds 30,000 s and no absorptive ever bred. Hand one verified
   genome across the establishment gap and the chain builds itself in both seeds. The
   owner's question, world problem or mutation problem, splits cleanly. Establishment is a
   world problem, since the on-ramp is narrow and the refuge closed it. Arrival is a
   mutation-supply problem.
2. The controls satisfied the goal-shaped metric at this horizon. Their post-injection
   inherit streaks ran 104 and 88 consecutive samples, both far past 20, with 56 and 93
   alive at the last sample. A transplanted chain in the *unmodified* world outlives the
   goal rule's bar for as long as we watched. The caveat sits beside it. Both were
   declining, the larder under them was nearly eaten, and round 6 says what comes after a
   peak like that when the run is longer.
3. A consumer-free treated world is the owner's oil field. With the transplants dead and
   nothing grazing the deep, the treated seed-2 world climbed monotonically to 30 J/m³ of
   deep stock and swelled to over a thousand producers. That is energy burying itself in an
   ungrazeable floor, the one-way carbon story the refuge was always going to write once
   nothing could eat it back.

### Instrument replays (added 2026-09-01, before launch)

Review round 4 raised a question this round's data cannot answer as recorded. The open
question is whether the busts are consumer–resource cycles or **cohort cycles**, in which
one dominant generation grazes the pool below its own break-even and starves. That is a
mechanism neither refuges nor patches address.

The discriminator is cycle period against consumer generation time, which needs the birth
and death record. The lineage-events instrument was built after the arms ran, and was proven
inert: a world drained or undrained steps bit-identically, suite 374.

So the two control arms are replayed as `d060b-s2r0` and `d060b-s4r0`, on the same seeds and
the same config, with the same hash expected. They are deterministic twins of scored runs.

Each now writes a `lineage.jsonl` of its own.

The validity check comes before any reading. The replay's sample rows must match the
original token-for-token. This is instrumentation of an already pre-registered condition
rather than a new treatment, and nothing here touches the round-8 fork.

The results landed the same day. Both replays are perfect twins, with config hash
`1734ee6d195cd439` unchanged and zero differing sample rows against the originals. The
lineage record rewrites this round's most optimistic reading.

| | s2r0 | s4r0 |
|---|---|---|
| clade births (5 inoculants + descendants) | 135 | 122 |
| generation time (parent birth → child birth), median | 1,234 s | 1,276 s |
| **last clade birth** | **t=13,938** | **t=16,915** |
| world (non-clade) births after that instant | thousands | 504 |
| clade state at budget (t=20,000) | 56 alive, sterile 6,062 s | 93 alive, sterile 3,085 s |

Both control chains were demographically dead long before the budget. Recruitment ceased at
the population peak in both seeds. What the reports showed afterwards as a decline was a
sterile cohort ageing toward extinction while the rest of the world bred freely. Seed 4's
clade deaths accelerated from 5 to 14 to 10 per thousand seconds, and two inoculants died at
about 10,500 s, the senescence knee.

The boom is ~4–5 generations long, and it ends in a recruitment collapse rather than a
mortality wave. The cohort grazes the pool below the *reproduction* break-even, since a 562
J brood is unfundable at post-boom densities, while every adult still clears its *survival*
break-even. So the population pins its own food in the gap between the two thresholds:
alive, grazing, sterile.

Structurally this is the de Roos–Persson cohort trap that review round 4 flagged
([LITERATURE-REVIEW.md](../research/LITERATURE-REVIEW.md) §9 item 7). The lineage record
cannot separate the energy and matter sides of the refused conceptions, and both are the
same story of at-depth exhaustion under grazing.

Two corrections follow from this.

1. This entry's "alive, declining" verdicts on the controls were too kind. The controls did
   not show persistence with a downslope. They showed a four-generation boom and then a
   walking-dead tail. Round 6's booms should be presumed to carry the same hidden structure.
2. The standing goal rule has a blind spot, now demonstrated rather than suspected. One
   inherited absorptive with 10 alive at the last sample is satisfied for thousands of
   seconds by a lineage whose last birth is long past. The seed-2 control's 104-sample
   streak was sterile from sample ~60 onward. A rule amendment, requiring an absorptive
   *birth* within the last N samples, is the owner's to make. Every future scoring should
   report last-birth time alongside the streak either way.

What any round-8 stabiliser must now do is stated by the mechanism. It has to keep some
food, somewhere, above the **reproduction** threshold. Above zero is not enough, and above
the survival threshold is the trap itself.

One more replay was launched on the same reasoning, on 2026-09-01. Correction 1 above says
round 6's natural booms should be presumed to carry the same structure, and a presumption is
cheaply convertible to measurement. `d056b-s1` replays round 6's seed 1, the campaign's
biggest natural boom, 908 down to 5, a full cycle including the mortality crash the assay's
budget truncated. It runs with the lineage instrument, and it answers whether recruitment
collapse precedes the mortality crash in a natural, evolved chain, and by how long.

The validity check is as before. Rows must match `d056-s1.md` token-for-token on the shared
column prefix. The hash differs, because three generations of default-off tunables have
entered Core since round 6, and the knob-off bit-identity tests are the bridge. The result
is appended when it lands.

The replay validated, with all 228 captured rows matching the original, and the answer is
emphatic. The natural boom carries the same structure, more violently.

- Absorptive births per 500 s through the boom ran 1, 10, 107, 525, 275, 18, 0. Recruitment
  collapsed at t≈13,000, at the 908 peak, while deaths in that same window numbered twelve.
- The entire visible crash, 908 down to 5 over the following 5,000 s, happened at zero
  births. A sterile cohort died on schedule, at 129, 145, 98, 118, 101 and 197 deaths per
  500 s. What every report of every round has rendered as the bust is the after-image of a
  recruitment collapse thousands of seconds earlier, at the moment the population looked
  strongest.
- The natural chain's median generation gap is 172 s, seven times faster than the transplant
  genome's 1,234–1,276 s. Generation time is a genotype property, set by endowment and body
  volume, and the trap is not. Threshold densities in the round-8 proposal are computed for
  the transplant genome, and must be re-derived per genotype where precision matters.

Seven rounds, seven busts, and now one measured mechanism, confirmed in a transplanted chain
in two seeds and in the campaign's largest natural chain.

An instrument note, recorded because it will happen again. The replay wedged at t=22,700,
with ~6,100 alive and the world heading for its ceiling. Log and report were silent for 5.7
hours with the process alive, the first hang of the campaign, cause unknown. The wall-clock
check lives inside the frozen loop, so it could never fire, and the process was killed by
hand.

Two lessons came out of it. The monitor for this replay watched only for endings and compile
errors, dropping round 7's stall rule of a report silent for 30 minutes. That rule goes back
into every future monitor. The append-only run-directory design did what it was built for,
and every row written before the hang survived the kill, including the entire boom. Worker
2's Library died with the process, so refresh before its next arm.

D055's knob survives as an instrument. As a world rule at 1 m it is falsified twice over.
Round 7 showed it blocks establishment, and this round shows it kills established consumers
too. The design tension it was built to resolve is still real, since round 6's booms still
bust, and the resolution cannot be to close the seabed.

The owner's hypothesis, raised on seeing these results, is that the deeper distortion is
**whole-layer access**. A creature at the right depth feeds from the entire horizontal extent
at once, with no travel and no local depletion. That is the perfectly-stirred regime, in
which consumer-resource theory predicts the violent cycling we observe, and it also
forecloses any reason for movement to pay. That hypothesis, the literature it needs, and the
round-8 design it implies are the next decision, and the decision is the owner's.
