# Proposal — how to proceed from here

**2026-08-29 · Written by Claude (Fable 5), after a full review of DESIGN.md, DECISIONS.md
(D001–D050), all 34 logbook entries, the primer, both research areas, the run archive and the
code.**

This is advice, not a document of record. Nothing here is a source of truth — where it states a
fact it links to the document that owns it, and where it makes a judgement call it says so. If any
of it is adopted, the adoption belongs in `DECISIONS.md`, not here.

---

## 1. Where the project actually is

The project has quietly succeeded at something harder than what its own documents describe. The
status blocks still say "Milestone 1 done, no fluid, no economy" — what exists is a running
endogenous-selection ecosystem: a conserved energy economy auditing to 0.0000% across a food web
(D026), a finite competed-for sun (D023), currents and mixing (D037), senescence that retired the
founders and made minimum generation depth reachable (D038), a matter currency that made producers
strip their own surface and built a 300× vertical gradient out of a uniform start (D048,
logbook/0033), a buoyancy organ under real selection with evolved lift above the founder range
(D050, logbook/0034), and — twice — a food chain nobody designed: sun → producer → corpse →
detritus → consumer, with a quarter to a third of the detritivores *born* into the trade
(logbook/0025, logbook/0028). D017's bet has paid at least once, on the record, with the audit
closed the whole way.

The record itself ranks the open problems, and I agree with its ranking:

1. **No world currently holds a full ecology and a food chain at the same time.**
   Every buoyancy-era world lost its absorptive lineage and detritus piled up unconsumed —
   logbook/0034 calls this "the oldest open problem here and it is not a buoyancy problem."
2. **Movement has never paid**, and after D042–D046 the cost side is *closed*: an earning muscle
   at minimum capacity sits 5.2% below a plant and cannot reach parity. Every remaining option is
   on the prize side, and the prize is depth (logbook/0027, logbook/0031).
3. **Throughput binds every remaining question.** D040: the binding constraint stopped being
   ecological around t≈9,500 and became wall clock. Every open question lives in states the world
   reaches later than it can currently be run to.

And one meta-problem: **half the documents describe a project that no longer exists** (§3 below).

---

## 2. The largest single gap: the project changed genres and never read the new genre's literature

Draft 5 turned a Sims-style directed-search simulator into an open-ended artificial ecosystem.
That is a move from a literature the review covered thoroughly into one it has **never searched at
all** — the review says so itself, in §7.1: *"§5A should be read as unreviewed design rather than
as evidence-led,"* and its §9 lists round 3 as the correction. That was written on 2026-08-02.
Since then, essentially all the work has been §5A work, and round 3 has not run.

This matters more than any single mechanism, because the failures the project has been debugging
one expensive arm at a time are the classic failures of exactly this genre. Strategy collapse to
the cheapest trade, trophic levels that won't establish, treadmill dynamics, the cost of
complexity never paying — Avida, Tierra, PolyWorld, Geb, Gene Pool and the ALife
open-ended-evolution literature have thirty years of documented answers and instruments. The
project's own founding story (logbook/0001: the review overturned the design three times, and the
cheapest correction was the one read rather than run) is the argument for doing this *before* the
next round of arms, not after.

**Recommended round 3 scope**, concretely:

- The searches §7.1 already names: *open-ended evolution, artificial ecosystem, endogenous
  fitness, energy-based selection, Avida, Tierra, PolyWorld, Geb, Ventrella Gene Pool.* Three
  specific questions, all already posed in the review: does anything report trophic levels
  emerging from morphology-encoded feeding; what stops these systems collapsing to one strategy;
  is there precedent for a per-neuron metabolic charge (PolyWorld is the likely primary source —
  it is currently known only through a survey).
- **Gene Pool specifically**: it evolved swimmers under an energy economy with no fitness
  function — which is the exact thing this project has not managed. Whatever made that work
  (its prize structure — food particles and in-world mating as *reasons to travel* — is the
  obvious suspect) bears directly on §5 below.
- **Open-endedness instruments.** §5A.6b admits its own limit: generation depth measures
  reproduction, not adaptation, and cannot see a treadmill. The OEE literature has purpose-built
  instruments — Bedau–Packard evolutionary activity statistics, and the MODES toolbox (Dolson et
  al.) — that answer "is the world *interesting*" rather than "is the world alive". Importing one
  of these is probably the single highest-value instrument the project could add.
- **The debts the review already lists**: the 2025 co-optimisation preprint (never followed up,
  flagged as the cheapest high-value action remaining), forward snowballing from [EA23]/[C18],
  and the §13.4 quarantine — Cheney 2018 and Pugh 2016 are load-bearing and unverified.
- **Upgrade `research/early-life/` before it feeds a tunable.** Its own SOURCES.md says six of
  eight keys rest on search-result summaries. The mechanism survey is good design input as-is;
  no parameter should be taken from it until the load-bearing sources are full-text reads.

This runs as its own workstream and does not block building — but it should *precede the tuning*
of anything built in §4, because tuning against a known result is cheaper than rediscovering it.

---

## 3. Make the documents true again — one day, and it is not housekeeping

The six-document system is the project's epistemology, and three of the six currently misstate
the present. Logbook/0032's lesson — *prove the thing a decision promised to report is actually
reported* — applies to documents too. Specific instances found in this review:

| Document | Stale claim |
|---|---|
| `README.md` status block | "No energy economy yet, so nothing lives or dies… controller is still a test sine" — every clause false since early August |
| `CLAUDE.md` "Current state" | "Milestone 1 essentially done… no fluid, no fitness, no brain evaluation and no search" |
| `DESIGN.md` header | "Milestone 1 complete… No fluid, no fitness, no search yet. Date: 2026-08-02" — atop a document whose §5A now records measurements from late August |
| `DESIGN.md` §10 | The milestone table no longer describes the path taken: M3 (metabolism), M5 (life cycle) and half of M4 and M6 are *done*, out of order; the ecosystem work of D035–D050 maps to no row at all |
| `primer/05` | Closes with "the sensors that are not built yet" — four channels have read since D033 |
| `primer/` as a whole | Ends at the brain. Everything since — the finite sun, senescence, the food chain, matter, buoyancy — is unwritten, and it is the best material the project has |

Recommended: a draft-6 changelog entry in DESIGN.md and a rewritten §10 that states the actual
position (what is done, what was reordered and why, what remains), rather than patching clauses.
The primer gets its stale line fixed now and new pieces when convenient — pieces are written when
the thing works, and several things now work.

---

## 4. Close the loop before adding organs

`research/early-life/MECHANISMS.md` has the right headline: **what is missing is world mechanics,
not organs** — an organ needs something in the environment to be an organ *for*, and the last
several cell types have each underperformed for exactly that reason. I endorse its ranking, with
one amendment on sequencing. In order:

**4a. Remineralisation first.** It attacks open problem #1 directly. The trophic failure is a
ratchet: detritus is a one-way trip, so when the absorptive lineage crashes, the food thins where
creatures live (0.07–1.0 J/m³ in the buoyancy worlds against the 17.8 that sustained a food
chain), nothing can re-establish, and the pool buries itself. A slow abiotic return of detritus to
the resource pool — the viral shunt, at its coarsest — breaks the ratchet without an organism
having to exist first. It is a world process, cheap, unblocked, and D026 flagged the accumulation
as unresolved on the day it was built. **Acceptance test: a D048+D050 world that holds an
inherited absorptive lineage** — the precise thing every world run on 2026-08-28 lost.

**4b. Ballast buoyancy second.** Reserve-coupled lift — heavier while the reserve is full,
lighter as it drains — is the survey's #1: the mechanism cyanobacteria actually use, it needs no
neurons, and the feedback runs entirely through the per-creature energy reserve the simulator
already tracks. It is the Archean-correct answer to the two-gradient world D048 built (light at
the top, matter at the bottom, the buoyant starving for matter and the sinkers for light), where
fixed lift demonstrably cannot resolve the tension. It also makes this world a genuinely decidable
test of the Fogg & Walsby hypothesis, which is the most publishable-shaped claim the project has.

**4c. The excretion-into-a-field contract third, as a deliberate platform decision.** The survey
is right that it has the largest blast radius and unlocks photoferrotrophy, oxygen dynamics and
most of the rest. It should land after round 3 has been read (this is exactly the mechanism the
OEE literature will have opinions about) and after 4a/4b have been measured — not in the same
burst.

**No new cell types before these.** Consumer's failure is correct for an Archean world and stays
correct until standing biomass and a second resource exist (survey §6). Sessility waits on the
floor being worth attaching to. A second nutrient waits on the field contract.

---

## 5. Retire the muscle campaign as currently framed

Four arms in a row (D031, D032, the two joint probes, `linkearn`) were built to reach break-even
when the requirement was competitiveness, and the record now states the rule: **an arm whose
prize-to-cost ratio is near 1 will fail; build arms at ratio ≥ 2** (D044). The cost side is
closed — 5.2% below a plant at minimum capacity, bounded, and no knob left to sweep (D046). What
remains is entirely on the prize side, and the honest reading of the record is:

- **Depth is the only thing movement can buy** (D037 makes horizontal position economically
  meaningless by construction), and depth-*choice* has a cheaper organ than a joint. When
  `sink-mid` forced creatures out of the light, the population did not evolve a muscle — it
  changed trophic strategy, because that was cheaper (logbook/0028). Buoyancy will beat muscle
  for station-holding every time, and that is the system working, not failing.
- So the sequence that gives a joint a fair test is: passive lift (done, D050) → ballast (4b) →
  **brain-driven lift plus a `Chemical` sensor** — matter is now what blocks reproduction and
  nothing in the world can sense it; `Chemical`, `Energy` and `Flow` are still unimplemented —
  → and only then re-ask whether a joint pays, in a world whose optimum *moves* faster than
  ballast can track (diel amplitude over the matter dynamics, the pairing D035 built and never
  had a substrate for).
- **Accept the possible finding.** A joint is a Cambrian organ (D047) and this world is Archean.
  "Muscle does not pay yet, and here is the sequence of world-states in which it starts to" is a
  better result than a muscle coaxed into existence by pricing.

One physics item belongs here rather than under throughput: **`AddedMassCoefficient` is 0 in
every evolution run.** The sandbox and the smoke tests set it to 1; `EvolutionRun` never sets it,
so the term the review promoted to "the highest-value single improvement to the fluid model" —
the one [C18 §4, p.28] blames for medusoid anatomical uniformity — has never acted on a world
where anything evolved. While swimming earns nothing this costs little; it must be decided
*before* any arm in which swimming is supposed to pay, and D028 already notes it must be settled
before the timestep sweep. Deciding it is a config-default choice plus a re-calibration, and it
should get a DECISIONS entry either way.

---

## 6. Throughput, in the order the record implies

D040 is explicit that this is now the lever that makes every question cheaper. Order of
operations:

1. **Settle added mass** (above) — it consumes dt headroom, so it goes first.
2. **The timestep sweep.** D028 names dt as "the only lever that multiplies simulated seconds
   rather than dividing the cost of one" and its acceptance test already exists: if the audit
   residual grows, the speed was bought with free energy. This sweep has never been run. It is
   the cheapest large win available.
3. **Install `windows-il2cpp`** (your action, via the Hub GUI — the CLI is broken). CLAUDE.md
   already schedules it before per-creature brain evaluation matters at scale.
4. **Only then** consider orchestration beyond the current worker pool. The five-arm cap stands
   (it is in memory for a reason), and the drag loop is closed — D028 rejected further
   arithmetic work on measurement and nothing has changed.

---

## 7. Methodology, hardened into pre-flight checks

The logbook's recurring lessons are currently habits; they should be a checklist that
`run-arm.ps1` prints (or at minimum a template the arm's launch note fills in):

1. **Margin ratio ≥ 2, computed before launch** — the prize the arm offers over the cost it
   charges, as arithmetic on a developed body (the calculation logbook/0026 shows takes three
   multiplications and went unrun for four months).
2. **One variable against a named control, verified by diffing the run headers** — silently
   inherited defaults have burned three arms (logbook/0027, 0034; the memory note exists).
3. **A written prediction and the column that falsifies it**, before launch — logbook/0029's
   explicit "falls below 5% within ~2,000 s" is the model.
4. **Floor-aware columns only**: `inherited` and `floor` read together with `gen min`; a share
   is never evidence (logbook/0029, 0032).

Three specific debts in the same spirit:

- **Replicate the food chains.** Both headline results are n=1 (the canopy chain: seed 2 only;
  the probe chain: seed 3 only, at 20× mutation). Three seeds each, at whatever throughput
  allows, before they harden into narrative.
- **Audit the promised reports.** `Mutator.CodeVersion` claims to be "recorded per birth" and is
  recorded nowhere (logbook/0030). Decide `lineage.jsonl`: write it (sampled, or compact — the
  ancestry is what makes food-chain claims auditable and what any future observatory needs) or
  delete the promise from the class remarks and D021. Half-promises are the 0032 failure mode.
- **Pace mechanism landings.** 2026-08-28 landed nine decisions, three mid-flight bug catches
  and several wasted arms in one day; mechanisms were landing faster than the world could be
  re-calibrated under them, and the confounded three-settings-at-once comparison at the end of
  logbook/0034 is the symptom. One mechanism per calibrated world.

---

## 8. The theatre is not decoration

DESIGN.md's priority order is unchanged since draft 1: *mesmerising to watch first.* Nothing can
currently watch a running world. Meanwhile the logbook's own evidence is that a human looking at
the screen has been the best instrument the project owns — the momentum leak (0005), buried
parts (0006), and the uninspectable viewer (0010) were all invisible to every headless check and
obvious to a person in seconds.

I am not proposing the Milestone 8 Theatre. I am proposing a **minimal aquarium view**: one scene
that attaches to a live run (or replays a snapshot), with the camera and seed controls that
already exist from logbook/0010's fixes, colour by cell type, and nothing else. It is cheap, it
serves the stated primary goal for the first time since Milestone 1, and on the record it will
find bugs. The first food chain this project can *see* will also tell you things the stats
columns cannot.

---

## 9. Decisions that are yours, not mine

1. **The tiling fork.** D037 is explicit: physical corpses — and with them predation, patches,
   and everything spatial — "become the right design at the same moment tiling stops being
   necessary, and not before." That moment is a large rebuild and changes the throughput
   arithmetic. I recommend *not now*, but the trigger should be decided in advance (my
   suggestion: when a world reliably holds a food chain in depth-only ecology, the 1-D world has
   given what it has to give).
2. **Goal priority.** If "mesmerising" is still #1, §8 gets a few days soon. If the research
   instrument has quietly become #1 — which is what the last month's work pattern suggests —
   that is a legitimate change, but it should be a D-entry, not a drift.
3. **`lineage.jsonl`** — the storage budget question in §7 (hundreds of MB per long run, against
   auditability of ancestry claims).
4. **IL2CPP install** — needs your hands on the Hub GUI.
5. **Round 3 timing** — my recommendation is that it precedes the *tuning* of §4's mechanisms
   and parallels their *building*; if you would rather sequence it strictly first or strictly
   later, that changes the two-week shape below.

---

## 10. What not to do — collected from the record so it does not have to be re-argued

- **No MAP-Elites archive.** Already demoted (D017, §8), and the demotion was re-confirmed the
  day it was nearly violated (logbook/0024).
- **No Consumer/predation tuning.** Its failure is correct for this world's stage
  (early-life §6); tuning it to succeed would be tuning it to succeed in a world that should
  not support it.
- **No further drag-loop optimisation.** Closed on measurement (D028).
- **No more joint-affordability arms.** The cost side is bounded and closed (D043/D046).
- **No new cell types before the world processes of §4.** The survey's headline, and the
  project's own repeated experience.
- **No silently changed defaults.** Every default that moved so far moved loudly, with the old
  world reproducible (D035, D037, D038, D044). Keep that rule; it is why the record is still
  trustworthy.

---

## Proposed sequence

| When | What |
|---|---|
| Day 1 | §3: make the documents true. Fix the primer's stale line. |
| Days 1–3 | §2: literature round 3, as its own workstream (searches, Gene Pool + PolyWorld primaries, OEE instruments, the 2025 preprint, quarantine verification). |
| Days 2–4 | §4a: remineralisation, with the acceptance run (D048+D050 world holds an inherited absorptive lineage, 3 seeds). Replicate the two food chains while those workers are busy. |
| Days 4–6 | §4b: ballast buoyancy; re-run the D050 pair against it. |
| Next | §6: added-mass decision → dt sweep → IL2CPP. |
| Then | §5: brain-driven lift + `Chemical` sensor; the moving-optimum joint test only after that. |
| Whenever a human wants to look | §8: the aquarium view. Orthogonal to everything above. |

The through-line: **read the genre, repair the record, close the matter loop, give depth-choice
its cheap organ, buy throughput, and only then re-ask the muscle question in a world that can
finally answer it.** Everything on that path is either cheap, already scheduled by the project's
own documents, or both.

---

# Revision 1 — self-review after round 3 (2026-08-29, same author)

The proposal above is kept as written. This section reviews it against what the day produced:
§3's document repair is **done**, §2's literature round is **done** (an afternoon, against the
three-day estimate — see `research/LITERATURE-REVIEW.md` §0 round 3 and logbook/0035), and the
round tested several of the proposal's predictions. Scorecard first, then the amendments.

**What was tested and held.** The §2 bet confirmed more specifically than predicted: PolyWorld
is the per-neuron precedent ([Y94, p.7], verified at the page); Gene Pool's swimmers are
explained by prize structure (movement was the only route to both food and mates, [VG05]); the
OEE literature supplies the treadmill instrument (Bedau activity classes, with Standish's
permuted-shadow making it computable with no fitness function). §1's problem ranking and §10's
prohibitions stand unchanged.

**A bias to record.** The round-3 search briefs contained this proposal's hypotheses ("PolyWorld
is the likely primary source"), so the sweeps were confirmation-shaped. Mitigations: every
design-touching claim was re-verified against primary text, and the sweeps returned genuine
disconfirmations — EcoSim's trophic levels are *scripted*, not emergent (one hoped-for precedent
weakened); [EA23], which §2 of the design leans on, has three citations; [PU16]'s real claim is
narrower than the design's paraphrase. The proposal should have flagged this risk before the
round rather than after it.

**Amendments, in order of consequence:**

1. **§9's tiling-fork trigger was one-sided — the weakest paragraph in the proposal.** It said:
   end the 1-D era when a depth-only world reliably holds a food chain. Hamm & Drossel 2021
   (round-3 lead) found spatial structure *essential* for multi-trophic persistence, so there is
   now a live hypothesis that the trigger can never fire — and as written it would fail silently
   forever. Replace with a two-sided, pre-registered test: after remineralisation lands and is
   calibrated, three seeds; a persistent inherited food chain ends the 1-D era on schedule, and
   **repeated failure under otherwise-sufficient conditions becomes the evidence that space is
   the missing ingredient** — either outcome moves the decision.
2. **§5 missed a prize-side option: mating as a reason to travel.** Gene Pool's second travel
   prize — in-world mate-finding — exists independently of food gradients and worked with
   almost no perception [VG05]. §5A.6 defers sexual reproduction on perception grounds; round 3
   suggests re-examining that deferral *as prize design* rather than as recombination machinery.
   It would also, incidentally, give §4.5's grafting operator its first mechanism to fire.
3. **§5's re-test needs an [MC25] caution.** Co-optimisation systematically *undervalues*
   newly-mutated bodies — and under §5A that mechanism plausibly arrives through the ledger: a
   jointed mutant's realised earnings under its unadapted brain understate the body's potential.
   The muscle re-test must therefore be persistence-aware (`jointedInherited` over long
   horizons at minimum), or a genuinely good joint can fail it honestly.
4. **The `lineage.jsonl` recommendation firms up from "your storage call" to "write it".** The
   only formal treadmill instrument found (§9's Q8 in the review) requires a lineage record.
   Budget remains the user's decision; the value side has moved decisively, and sampled or
   compact formats bound the cost.
5. **§4a gains the acceptance criteria it lacked.** [GOY23] frames the target (closed loops
   extract ~100× more energy than open ones); the project's own numbers set the threshold —
   food chains established near 17.8 J/m³ where creatures live and died at 0.07–1.0. So:
   remineralisation succeeds if it holds local density above the establishment threshold across
   three seeds, with an inherited absorptive lineage as the binary readout.
6. **Two actionability gaps in §7.** The margin-ratio pre-flight named no owner — it should be
   a check `run-arm.ps1` computes and prints (or a `MarginPreflight`-style test pattern), not a
   habit. And the proposal scheduled no review of itself; this revision exists because the user
   asked, which is the wrong mechanism. Next natural checkpoint: after the remineralisation
   acceptance run.
