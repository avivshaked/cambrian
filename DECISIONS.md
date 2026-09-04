# Decision Log

Why things are the way they are — including the alternatives that were rejected, and the
occasions when we were wrong and changed our minds.

**Where this fits.** Four documents, deliberately non-overlapping:

| Document | Answers |
|---|---|
| [`DESIGN.md`](DESIGN.md) | *What the system is* — the current specification |
| [`research/LITERATURE-REVIEW.md`](research/LITERATURE-REVIEW.md) | *What the evidence says* |
| **`DECISIONS.md`** (this file) | *Why we chose this over that*, and what we rejected |
| [`CLAUDE.md`](CLAUDE.md) | *What will bite you* — operational gotchas |

**Rules.** Append-only; never rewrite an entry. If a decision is reversed, add a new entry
and mark the old one `SUPERSEDED BY Dxxx` — the reversal is the valuable part. Record
rejected options with the reason, because "did we consider X?" is the most common question
a future reader will have. Keep entries short; link out rather than restating.

---

## Index

| # | Decision | Date | Status |
|---|---|---|---|
| [D001](#d001) | Unity, not Unreal or Godot | 2026-08-02 | active |
| [D002](#d002) | Co-evolve morphology *and* control (Sims lineage) | 2026-08-02 | active |
| [D003](#d003) | Water before land | 2026-08-02 | active |
| [D004](#d004) | MAP-Elites, not a plain GA | 2026-08-02 | active |
| [D005](#d005) | Recursive graph encoding, not CPPN | 2026-08-02 | active |
| [D006](#d006) | `ArticulationBody`, not `Rigidbody` + joints | 2026-08-02 | active |
| [D007](#d007) | Effectors: torque + mass-scale + smoothing | 2026-08-02 | active |
| [D008](#d008) | Separate the farm from the theatre | 2026-08-02 | active |
| [D009](#d009) | Spike the physics before more research | 2026-08-02 | active |
| [D010](#d010) | Pooling rejected — rebuild per evaluation | 2026-08-02 | active |
| [D011](#d011) | Island model demoted from throughput necessity | 2026-08-02 | active |
| [D012](#d012) | Living literature review, extended in rounds | 2026-08-02 | active |
| [D013](#d013) | Public repository, named `cambrian` | 2026-08-02 | active |
| [D014](#d014) | Papers and extractions never committed | 2026-08-02 | active |
| [D015](#d015) | MIT for code, CC BY 4.0 for documentation | 2026-08-02 | active |
| [D016](#d016) | A logbook, kept as dated entries | 2026-08-02 | active |
| [D017](#d017) | Endogenous selection: an energy economy, not a fitness function | 2026-08-02 | active |
| [D018](#d018) | Generation zero is one cell, or one cell and a tail | 2026-08-07 | active |
| [D019](#d019) | `Neural` is a cell type; it discounts neurons rather than gating them | 2026-08-07 | active |
| [D020](#d020) | Distal senses are scalars; direction is computed by the body | 2026-08-07 | active |
| [D021](#d021) | A population floor, treated as an instrument rather than a safety net | 2026-08-07 | active |
| [D022](#d022) | Generation depth is the calibration readout | 2026-08-07 | ⚠ partly superseded by D025 |
| [D023](#d023) | The sun is finite: light is competed for, and that is the carrying capacity | 2026-08-07 | active |
| [D024](#d024) | Bodies are bounded at both ends, and a bodyless genome is stillborn | 2026-08-07 | active |
| [D025](#d025) | Self-sustaining means the floor has gone quiet, not that minimum depth rose | 2026-08-07 | active |
| [D026](#d026) | A body costs what a corpse is worth, and it is one number | 2026-08-07 | active |
| [D027](#d027) | A knob is declared once, and everything else is derived from that declaration | 2026-08-07 | active |
| [D028](#d028) | Drag is summed in the part's own frame, and the panels are built once | 2026-08-07 | active |
| [D029](#d029) | The physics and the economy are joined by one method, and it revealed that work must not be billed yet | 2026-08-07 | active |
| [D030](#d030) | The genome's brain is evaluated, and creatures can swim | 2026-08-07 | active |
| [D031](#d031) | What owning an actuator costs is the knob that decides whether anything can move | 2026-08-07 | ⚠ partly superseded by D032 |
| [D032](#d032) | The actuator-cost correction goes on `MaxLinkPower`, and the two knobs are not interchangeable | 2026-08-24 | active |
| [D033](#d033) | Founders draw sensor references from the implemented channel set, and mutation may rewire an input | 2026-08-24 | active |
| [D034](#d034) | Per-creature seeds are mixed with the world seed, not counted from it | 2026-08-24 | active |
| [D035](#d035) | The diurnal cycle is mean-preserving, and off by default | 2026-08-25 | active |
| [D036](#d036) | The world needs a current: energy has no return path without one | 2026-08-25 | active |
| [D037](#d037) | The current is two standing waves, and stirring is separate from it | 2026-08-26 | active |
| [D038](#d038) | Ageing is an energy phenomenon, and immortality was suppressing selection | 2026-08-26 | active |
| [D039](#d039) | The trophic niche is arrival-limited, not viability-limited — measure the margin, not the break-even | 2026-08-27 | active · open question resolved by D040 |
| [D040](#d040) | The world grew a food chain, and the binding constraint is now throughput | 2026-08-27 | active |
| [D041](#d041) | Filtering clearance 0.5 → 1.0 — the two trades want opposite bodies | 2026-08-27 | active |
| [D042](#d042) | Joints were never affordable — D031 and D032 swept the wrong side of a line at 5 N·m | 2026-08-28 | active |
| [D043](#d043) | A muscle that earns its keep — `LinkCell.PhotosyntheticEfficiency` | 2026-08-28 | ⚠ its 88% ceiling superseded by D046 |
| [D044](#d044) | Tissue is denser than water, because staying still has to cost something | 2026-08-28 | active |
| [D045](#d045) | A mutation-born joint draws from the same bounds a founder does | 2026-08-28 | active |
| [D046](#d046) | A link that photosynthesises pays green tissue's upkeep | 2026-08-28 | active · supersedes D043's ceiling |
| [D047](#d047) | The alphabet stays flat; what was missing is the floor’s report | 2026-08-28 | active |
| [D048](#d048) | Producers must consume something — nutrient is matter, light is energy | 2026-08-28 | active · built, load-bearing since logbook/0033 |
| [D049](#d049) | A buoyancy cell, passive before controlled | 2026-08-28 | active · built; rescaled by D050 |
| [D050](#d050) | Lift is a multiple of the sink it cancels, and the ocean has a top | 2026-08-28 | active · fixes D049 units |
| [D051](#d051) | The floor gives back — remineralisation closes the nutrient cycle | 2026-08-29 | built · redundant with D036 mixing except at mixing 0 |
| [D052](#d052) | Living bodies give matter back — the excretion contract | 2026-08-29 | active · measured — first bounded living worlds (logbook/0041) |
| [D053](#d053) | Right-sizing the dish — the equilibrium must sit inside the instrument | 2026-08-30 | closed — no geometric rescale bounds this world (logbook/0040) |
| [D054](#d054) | The floor becomes a gradient — a beach, a shelf, and a deep | 2026-08-30 | decided · not designed |
| [D055](#d055) | The seabed is a refuge — the floor layer cannot be grazed | 2026-08-31 | rejected as world rule at 1 m — starves the benthic consumer outright (logbook/0042, 0043); knob kept as instrument |
| [D056](#d056) | Mutation rate: fixed within a round, raisable between rounds, never a thermostat | 2026-08-31 | direction · nothing built |
| [D057](#d057) | A species is a clade within a drift threshold of its founder | 2026-08-31 | active · built, θ uncalibrated |
| [D058](#d058) | Only the budget ends a run — censored arms cannot pass | 2026-08-31 | active |
| [D059](#d059) | The ocean gets a floor — D050's mirror, per-column under D054 | 2026-08-31 | decided · not built |
| [D060](#d060) | The invasion assay — a labeled hand for a mechanism question | 2026-08-31 | active · built, first use logbook/0043 |
| [D061](#d061) | The patchy world — horizontal structure, throttled exchange, endogenous inequality | 2026-09-01 | built · round 8: 0-of-5 by producer extinction — a founding cost at this dose (logbook/0044); knobs kept for the movement question |
| [D062](#d062) | The satiation cap — a filter feeder's mouth gets its physical limit | 2026-09-01 | built · round 8: unanswered — every clean arm ran away before a chain formed (logbook/0044) |
| [D063](#d063) | The recruitment clause — a chain must be breeding, not merely surviving | 2026-09-01 | active · earned its keep in round 8: failed the flagship's sterile cohort at the cut (logbook/0044) · **met 2026-09-04, round 18 (logbook/0054): 4 of 5 seeds, discovery regime, with exudation 0.15 (D070)** · amended the same day (owner): one connected clade, held ≥ 10 through the last two lifetimes; producers scored as an inherited photosynthetic line |
| [D064](#d064) | Size-dependent buoyancy — small bodies float in place, growth is what sinks you; founders anywhere in the column | 2026-09-02 | built · round 10c: the drowning is cured — five of five producer worlds to budget, uncensored, depth held within 3 m through 24,000 s of throttled births (logbook/0046); the chain still does not arrive |
| [D065](#d065) | The fixed matter cost — a body costs a minimum of matter to exist, so the count cannot ratchet through shrinking | 2026-09-02 | built · round 10c: counts levelled at 1,490–1,610 against a ceiling of 8,000, uncensored (logbook/0046) |
| [D066](#d066) | Roll cells — the water moves everything, and it stirs: one prescribed flow over patches advects bodies and fields; blinking rolls for chaotic advection | 2026-09-02 | built · round 12 (logbook/0048) 0 of 6: the rolls stir fields and bodies and keep producers alive at 30 m cells, but a roll that stops above the floor is a trapdoor for remains — surface detritus 0.2–3 J/m³, deep 15–21; one absorptive line formed and drifted out. Next lever proposed: sink speed of remains |
| [D067](#d067) | The vent — an upwelling plume from the floor in one patch, return through the others, superposed on the rolls; returns what the trapdoor takes | 2026-09-03 | built · run as round 13 arm B (logbook/0049), two seeds: producers survive the return flow (M6 held), the deep field falls to the lowest of the round (2.0–2.4 J/m³), no chain forms, matter refusals rise 1.5–5× against the twin. Neither harmful nor load-bearing while absorbers cannot breed; off in round 14 |
| [D068](#d068) | The stomach's gearing — clearance 5 and 10, because a stomach at clearance 1 cannot out-earn a leaf and a mutant that earns less than its siblings never leaves a line | 2026-09-03 | ruled · round 14 (logbook/0050) running under D069's sequential rule: 0 of 3 scored so far — clearance 10 grows the first 20–48-member lines of the campaign and they graze their field and crash; clearance 5 holds a line of 3–9 |
| [D069](#d069) | Compute, screen, confirm — the ledger calculator, the invasion assay, futility and sequential-seed rules, and a configurable physics step validated against a seed already run | 2026-09-03 | ruled and built · calculator and assay in use (logbook/0051); coarse step settled (logbook/0052): 0.02 screens, 0.01 confirms, 0.05 out |
| [D070](#d070) | Exudation — producers deposit a fraction of photosynthetic intake as detritus while alive, because the second trophic level is fed at ~1% (0.2 W against ~17 W) and the flux, not the gearing, caps a line at about six | 2026-09-03 | ruled in principle · build gated on `r14c10-s1-flux`'s measured income and `r14c10-s4`'s outcome; review round 5 done (PER 10–20%, screen at 0.15); screen pre-registered; **confirmed 2026-09-04** (logbook/0054: 4 of 5 seeds pass D063 at dt 0.01 with exudation 0.15; the failing seed's stomachs were matter-blocked, not starved) · a world rule, DESIGN.md §5A.2c |
| [D071](#d071) | Matter at depth — the matter sink decoupled from the detritus sink (0.02 m/s against 0.002), because the population plateau was the matter cap and the deep was dry; a vent that adds matter deferred as a future experiment | 2026-09-04 | ruled · **screened 2026-09-04 (logbook/0055): not adopted** — the free pool is 10% of the stock at any sink speed; overtaken by D072 (the contest for each unit was an age queue) |
| [D072](#d072) | Conception order — the breeding walk shuffled each step behind a knob, because the world bred oldest-first and a young stomach stood behind every older leaf in its layer; a fault by CLAUDE.md's rule, measured at 48–62% of plateau births to bodies past a lifetime | 2026-09-04 | built behind `EVOSIM_CONCEPTION_ORDER` · **screened 2026-09-04 (logbook/0056): `shuffled` not adopted** — the queue was the stomachs' lifeline, because in a matter-bound plateau energy buys no fecundity; ruled as D073 |
| [D073](#d073) | Energy buys matter — scarce matter to the parent with the largest energy reserve (`reserve`), screened against a stock large enough that light binds first (3/m³) | 2026-09-04 | ruled · **screened 2026-09-04 (logbook/0057): neither adopted** — the stomachs bid highest and still breed no more than the leaves; the larger stock triples everything at the same share; the open matter budget follows as D074 |
| [D074](#d074) | The open matter budget — an influx (surface or vent) and burial at the floor, the world's size a flow, because a conserved stock locks and stops selecting | 2026-09-04 | ruled · built · screening (logbook/0058) · adoption is the owner's |
| [D075](#d075) | The path after the open budget — movement that pays first, on the vent's stage, the theatre in parallel; then predation, the cell types, the archive | 2026-09-04 | ruled (owner: "lock it in") · begins when an open world confirms at 0.01 |

---

### D001
**Unity, not Unreal or Godot** · 2026-08-02

The bottleneck in this project is iteration speed on *simulation logic* — fitness
functions, mutation operators and sensor layouts get rewritten dozens of times — not
rendering fidelity. C# hot-iteration beats C++ recompiles for that.

**Rejected:** *Unreal* — better default visuals and Chaos physics, but every simulation edit
pays a compile cost, and Nanite/Lumen solve problems this project doesn't have. *Godot* —
tiny and fast to start, but weaker 3D physics and a much smaller ecosystem for this work.

Later corroborated rather than merely asserted: [L21 Table 2, p.15] shows PhysX used for
Lessin et al.'s rigid-body evolved creatures and Unity ML for Pathak et al. 2019.

---

### D002
**Co-evolve morphology *and* control (Karl Sims lineage)** · 2026-08-02

Chosen over three alternatives after an explicit comparison: neural brains on fixed bodies,
ecology/population-genetics simulation, and a player-shaped sandbox. Selected because
evolved *bodies* are the visually spectacular outcome, which was the stated primary goal.

**Known cost, accepted deliberately:** this is by far the most computationally expensive
option, and the one with the worst-documented failure mode ([D004](#d004)).

---

### D003
**Water before land** · 2026-08-02

On land the first thing a GA discovers is that the highest-scoring "locomotion" is to build
a tall tower and fall over. Water has no ground-contact instability, no falling, and a
trivial force model.

Argued from reasoning first, then supported: [C18 §3.3, pp.22–27] found land→water
transitions **detrimental** (p<0.01) while water→land trended beneficial. Their explanation
matches ours — on land, early creatures cannot overcome static friction and score zero,
so there is no gradient to climb.

**Honesty caveat:** the water→land benefit was **not statistically significant** (p>0.05).
Enough to justify the ordering; not enough to claim as a result.

---

### D004
**MAP-Elites, not a plain GA** · 2026-08-02

Co-evolving body and brain converges prematurely: a morphological mutation invalidates the
co-adapted controller, selection discards the offspring even when the body is better, and
morphology stagnates while controllers keep improving. [EA23] benchmarked five algorithms;
the no-diversity-protection baseline **lost every pairwise comparison**.

MAP-Elites cells also *are* morphological innovation protection — a novel body competes only
within its own cell, never against the global champion.

**Rejected:** *plain GA* — the losing baseline. *MNSLC* — won [EA23]'s formal comparison
6–5, but their own practical recommendation for virtual creatures was MAP-Elites, for its
exploration/exploitation balance. Revisit if Milestone 3 shows stagnation.

**Two corrections applied:** selection is fitness-proportional, not uniform; descriptors are
multi-BC (aligned *and* unaligned), because [K12 §4, p.11] showed aligned-only losing to
plain fitness search, and Pugh et al. warn unaligned-only can fail to reach viable solutions.

---

### D005
**Recursive graph encoding, not CPPN** · 2026-08-02

Looked like the most expensive decision to retrofit, and CPPNs initially looked stronger —
[EA23] used them and cited better evolvability through regular patterns.

[L21 Table 6, p.18] aggregates every published encoding comparison and the result splits by
**phenotype type**: CPPNs win on *soft bodies*; on *rigid articulated bodies* — our
substrate — direct and recursive encodings win or tie, including in the most recent
four-way comparison (Veenstra & Glette 2020). The apparent threat was a substrate mismatch.

**Also corrected a mislabel:** this encoding is *indirect*, not direct. [L21 §4.2, p.8]
classifies Sims as "an indirect representation that supports recursive structures."
Recursion, reflection and cumulative subtree transforms **are** generative machinery, so the
regularity CPPNs were wanted for is already present.

**Revisit if** the project ever moves to soft-body phenotypes.

---

### D006
**`ArticulationBody`, not `Rigidbody` + `ConfigurableJoint`** · 2026-08-02

Creatures are articulated kinematic trees, which is what PhysX articulations exist for.
Configurable joints develop rubber-band jitter under high torque that a GA will immediately
discover and exploit as "locomotion."

Stability was well-founded; **scale was not, and was the top open risk**. Settled by
measurement — see [Spike 01](spikes/01-articulation-body/) and [D010](#d010).

---

### D007
**Effectors: torque, mass-scaled, temporally smoothed** · 2026-08-02

Originally framed as a binary between raw torque (faithful to Sims, but produces buzzing
high-frequency motion the GA loves and viewers dislike) and PD position targets (smooth, but
more tuning and state).

[K12 §2.2, p.5] supplies a cheaper third option: clamp to [-1,1], scale by the mass of the
**smaller** of the two connected bodies, then average over the previous **10** values. The
mass scaling prevents arbitrarily powerful tiny motors; the smoothing suppresses the buzzing
that motivated PD in the first place.

---

### D008
**The farm and the theatre are separate programs** · 2026-08-02

Evaluation is headless, ugly and fast; presentation is slow, beautiful, and replays stored
genomes. They share a serialization format and nothing else.

Conflating them is the classic failure mode of this genre — it makes the search too slow to
succeed *and* the visuals too constrained to be lovely. Named explicitly because it is the
easiest principle here to violate by accident.

---

### D009
**Spike the physics before doing more research** · 2026-08-02

At the point where four of six research questions were answered, the remaining unknowns
split into two kinds. Literature gaps are questions *someone has already written down an
answer to*. Whether Unity can build and destroy articulated hierarchies hundreds of times a
second is not — no paper answers it, and it stays open regardless of how much you read.

It was also the risk that invalidated the most downstream work if it failed.

**Rejected:** finishing the reading first — the remaining papers were more likely to confirm
than overturn, and further reading has low marginal value without implementation feedback.

---

### D010
**Pooling rejected — rebuild articulations per evaluation** · 2026-08-02

[`DESIGN.md`](DESIGN.md) §11.1 planned a pool of pre-allocated articulations as a fallback
in case teardown proved expensive. Spike 01 measured build+teardown at **0.335 ms** against
a 15 ms budget and a ~2.4 s evaluation batch.

Pooling is therefore unnecessary — and its awkward consequence disappears with it. A pool
would have had to be **bucketed by topology** (joint limits and drive targets are
reconfigurable at runtime; tree topology is not), which would have constrained how freely
morphology could vary within a batch. Rebuilding leaves morphology unconstrained.

---

### D011
**Island model demoted from throughput necessity to evolutionary choice** · 2026-08-02

The design assumed ~10–12 worker processes were needed to reach 500–2000 evaluations/minute.
Spike 01 measured **≈1,610 evaluations/minute from a single process** — the whole target
from one worker.

The island model stays in the design for its *evolutionary* value: independent
subpopulations with migration is one of the [D004](#d004) diversity mitigations. But it is
no longer load-bearing for speed, so if it proves awkward to build, dropping to 2–4 workers
is now viable rather than fatal.

**Caveat:** the measurement excluded fluid forces, brain evaluation and collision. Re-measure
at Milestone 3.

---

### D012
**Living literature review, extended in rounds** · 2026-08-02

A PRISMA report is a snapshot by design. Appending papers to one silently falsifies its
counts and its self-assessment — §7.3's "small n" claim is a live statement, not decoration.

The review now carries a round table (§0) and a search update protocol (§3.5, which is
PRISMA-S item 12 and was originally omitted). Superseded claims are struck through rather
than deleted, mirroring [`DESIGN.md`](DESIGN.md)'s draft changelogs.

---

### D013
**Public repository, named `cambrian`** · 2026-08-02

The Cambrian explosion was a rapid diversification of **body plans**, which is exactly what a
morphological archive exists to produce. It also hooks the literature — [U07], which drove
the largest correction to the design, analyses *Anomalocaris*, a Cambrian animal.

**Rejected:** `evolved-virtual-creatures` (discoverable but generic and long), `medusoid`
(wry, but names a limitation rather than an ambition), `evosim` (matches the namespaces,
but generic).

Public was made safe first: PDFs verified gitignored, no emails in tracked content, and the
institution name genericized to "university subscription access" — legitimate use, but a
personal detail of no value to a reader, and publishing personal information is much harder
to undo than adding it back.

---

### D014
**Papers and their extractions are never committed** · 2026-08-02

`research/papers/` holds copyrighted publisher PDFs plus machine extractions; two were
obtained through a university subscription. Converting a paywalled paper to markdown does
not make it redistributable.

[`research/FETCH-RESULTS.md`](research/FETCH-RESULTS.md) is tracked instead and records the
exact retrieval URL for every paper, so the set is reconstructible by anyone with equivalent
access.

Enforced by `.gitignore` **and** by [`scripts/githooks/pre-commit`](scripts/githooks/pre-commit),
because a gitignore rule is one careless `git add -f` away from being bypassed — and because
an over-broad ignore rule silently dropped Unity's `Packages/manifest.json` from the first
commit, which is the same class of failure in the opposite direction.

---

### D015
**MIT for code, CC BY 4.0 for documentation** · 2026-08-02

This repository is roughly 90% prose and 10% throwaway spike code. A single code licence
would have been the reflexive choice and the wrong one: MIT says nothing meaningful about a
literature review or a design specification, and the thing most likely to be reused here is
the written work.

CC BY 4.0's attribution requirement matches the norm the prose already follows — every
load-bearing claim in `DESIGN.md` carries a page-level locator, so asking the same of anyone
downstream is consistent rather than restrictive. Both licences are permissive; neither
imposes copyleft on anything built on top.

**Rejected:** MIT alone (silent on prose); Apache-2.0 (the patent grant is irrelevant to
evolutionary algorithms and adds a NOTICE obligation for nothing); CC BY-NC (looks
protective, but blocks academic reuse in commercially-funded research, which is most of it);
CC BY-SA (viral into anything quoting the review — a nuisance for the exact audience this is
written for).

`LICENSE-DOCS` references the canonical CC BY text at creativecommons.org rather than
reproducing it. Several thousand words transcribed from memory is a real risk of a subtly
wrong licence, and a wrong licence is worse than a link.

Neither licence covers the quoted paper excerpts, which stay their publishers' copyright
under fair dealing and are attributed at the point of use. See [D014].

---

### D016
**A logbook, kept as dated entries rather than assembled as a book** · 2026-08-02

The four existing documents all describe the system as it currently stands. None of them
records *process* — what was tried, what broke, what the numbers were on the day. That
material is perishable: the specifics that make it useful are gone within about a week, and
they are exactly what makes a failure instructive rather than embarrassing.

`logbook/` fills that gap. Entries are dated, written the day something happens, and never
edited to match a later reality — a superseded entry is superseded by a newer one, as in
this file.

**Considered and deferred:** writing a book alongside the work. Two objections. First, it
duplicates `DESIGN.md` in friendlier prose, and when the spec changes the book is silently
wrong — the failure mode `CLAUDE.md` exists to prevent, at the largest possible scale.
Second, committing to book shape before there is a working system claims an outcome the work
hasn't earned. If the entries later turn out to be chapter-shaped, they can be assembled
then; they cannot be reconstructed from memory.

The rule that makes it safe: **the logbook is never a source of truth.** It links to the
other documents for facts rather than restating them.

**Rejected names:** `notebook/` (collides with Jupyter), `journal/` (in a repo full of
citations, that word means a publication venue), `logs/` and `docs/` — the first is caught by
`[Ll]ogs/` in `.gitignore` and would have been silently untracked, the second implies
reference documentation, which the four existing documents already are.

---

### D017
**Endogenous selection: an energy economy instead of a fitness function** · 2026-08-02

The design through draft 4 was Sims-lineage directed search: evaluate a creature alone, score
it by displacement (§5.5), let the score drive MAP-Elites. Draft 5 replaces that with an
ecosystem. Energy is a conserved budget with sunlight as its only primary input; creatures
spend it on metabolism, thinking and mechanical work; they reproduce on surplus and die at
zero. Nothing is scored. Specification in DESIGN.md §5A.

**The argument.** The project was never about producing the best swimmer. If swimming is what
keeps a creature alive, swimmers appear without being asked for — and if something else
appears instead, that is the more interesting result, because it was not the answer to a
question we wrote down. Directed search can only return a better version of the thing the
fitness function already describes.

**Why energy acquisition is a property of a part.** Cell types (photosynthetic, absorptive,
consumer, structural) make trophic strategy a *morphological* trait, which means the §4.1
graph encoding already expresses it. No species, niche or strategy concept is needed: a
species is a distribution of part types, and speciation is a change in that distribution. This
is what makes the whole thing cost one new field on `MorphNode` rather than a new subsystem.

**What the literature says, honestly.** It splits, and one paper argues the opposite.
[C18 §2.4, p.8] optimises against energy and material and credits the energy term with
producing passive tissue that creatures then exploit hydrodynamically. But [CEA07 §3.4, p.5]
pays a *bonus* for complexity (k^N, k = 1.02) on the grounds that *"simpler creatures are
likely to evolve faster and gain speed earlier"* — complexity is slow to pay off and gets
out-competed before it can. Via [L21 §8.2, p.10], Cheney et al. found complexity penalties
*"drive morphological evolution, [but don't] influence the overall performance of the
algorithm."* And [TM01 p.7] argues against elaborate fitness functions in principle, since
creatures find ways to score highly *"while not performing the sort of behavior that we had
hoped for."*

The energy economy sidesteps that debate rather than settling it: cost is not a term subtracted
from a score, it is a budget that runs out. [L21 §13] reports two systems that work this way
(PolyWorld, Ventrella's Gene Pool). Neither is a rigorous result — they are described in a
survey, not evaluated — and this decision should not be read as literature-backed. It is a bet.

**What it is a bet on.** That a food web is more watchable than a highlight reel of champions,
and that endogenous selection produces something we did not specify. The risk is real and runs
the other way: directed search reliably produces spectacular swimmers, and open-ended systems
reliably produce plateaus. DESIGN.md §5A.7 lists the failure modes and what defends against
each; the load-bearing one is that if sunlight alone covers upkeep, the world becomes a
photosynthetic mat and nothing ever has to move.

**Why now rather than later.** Milestone 4 had not been built. Everything through Milestone 2
is unaffected because the encoding does not care how selection happens. After Milestone 4 this
would have discarded an island model, a selector, and a throughput target measured in a unit
that no longer applies.

**Supersedes:** DESIGN.md §5.5 entirely. Demotes §8's MAP-Elites from selector to observatory —
it existed to protect morphological innovation against premature convergence under exogenous
fitness (§2), and ecological niches serve that role here. It is retained because an archive of
what lived is what makes a long run legible, which is a cheaper claim than the one it used to
carry.

**Considered and rejected:** an energy cost as a term *inside* the fitness function. It fights
[CEA07]'s finding directly, taxing elaborate bodies before they have learned to use themselves —
the same direction as the premature-convergence failure that §2 is built to resist. An earlier
draft of this decision proposed energy as a MAP-Elites descriptor instead, following [CU15]'s
use of it as a behavioural descriptor rather than an objective; that remains the right answer
*if* directed search is ever restored, and is recorded here so it does not have to be
rediscovered.

**Deferred deliberately:** attack and defence mechanics, sexual reproduction, per-part density
(swim bladders). Each is a second system, and each should be designed against observed
behaviour rather than guessed before anything runs.

---

### D018
**Generation zero is one cell, or one cell and a tail** · 2026-08-07

`GenomeFactory.Random` built founders of two to five nodes developing into three to sixteen
parts, with branching, recursion, bilateral pairs and several joints. Under exogenous fitness
that was necessary: a fitness function has to grade something on the first evaluation, so the
initial population must already be able to do the graded thing.

[D017](#d017) removed the fitness function, and with it that requirement. What remained was a
liability: **a founder population whose body plans we designed makes every later claim about
morphology a claim about our initial conditions.** Bilateral symmetry present at t=0 cannot be
evidence that bilateral symmetry pays.

Founders are now one earning cell, and half the time one link — a blob or a flagellate. A link
is a full part with tissue and upkeep and needs no child, so one hanging off one cell is a
flagellum rather than half a creature. Everything else must be discovered and must pay.

**Founders draw only from the earning cell types**, weighted 2:1:1 toward photosynthesis.
`Structural` and `Link` acquire nothing, so a founder of those alone starves with certainty
rather than probably — compute spent to make a corpse.

**The half that cannot eat is deliberate.** At t=0 there is no nutrient and no carrion, so
absorptive and consumer founders die, and their tissue is the first nutrient the world has.
The doomed half of generation zero *is* the primordial soup. Photosynthesis is weighted double
only so generation zero is not entirely stillborn — not as a claim that it wins, which is the
world's to determine.

**Rejected: filtering founders for viability.** `RandomViable` rejects genomes with no degrees
of freedom, which every blob has. Under §5A stillness is a way of living, not a defect, and
refusing to spawn a plant would be an exogenous judgement about what deserves to exist.

**Rejected: two cells as the minimum**, which was the first proposal. It assumed a link sits
*between* two cells; the implementation lets it dangle, and a dangling link is a tail. Two
parts is therefore already a swimmer, and one is the true floor.

**The load-bearing check:** if founders are this small, complexity must be reachable from them.
Measured — a single founder node reaches 32 nodes and 16-part bodies within 2,000 births under
mutation alone (`FounderTests`).

**Supersedes:** nothing. `GenomeFactory.Random` is retained for the Milestone 1 harnesses,
which need creatures with joints to actuate.

---

### D019
**`Neural` is a cell type; it discounts neurons rather than gating them** · 2026-08-07

Neurons lived on `MorphNode`, distributed across parts, plus a `GlobalBrain` array owned by no
part at all. That global array was the only cost in [D017](#d017)'s economy attached to no
tissue: joules spent, nothing to bite, nowhere to be.

The argument for changing it is §5A.1's own, applied to cognition. That section's thesis is
that energy acquisition is a property of a *part*, and that this is what makes trophic strategy
a morphological trait the graph already encodes. **The same holds for thinking: if a brain has
no location, brain size and placement cannot evolve, and cephalization — among the most
universal patterns in animal evolution — is structurally unreachable.**

**Every cell hosts a baseline of neurons; neural tissue makes them cheaper.** The baseline is a
nerve net, and it exists to avoid a valley: if neural tissue were required before any joint
could be driven, gaining a working tail would need two mutations each useless alone, and
populations do not cross those.

**Rejected: gating.** Capping neurons per part by tissue volume couples genome *validity* to
part size — and under §4.5's extinction-by-shrinking, parts change size constantly, so a
shrinking cell could invalidate a genome that was legal when written. Discounting has no such
coupling and nothing is silently disabled.

Discounting also produces cephalization as an economic outcome rather than a rule. §4.3
requires a neuron to sit on the part whose joint it drives, but neurons are cheaper where the
tissue is; motor neurons stay at the muscles and everything else migrates. And because capacity
follows volume while latency follows topology, one large neural cell and several small ones of
equal volume behave differently — centralised thinks fast and senses far, distributed the
reverse. Octopus against vertebrate, settled by selection.

**Rejected: forcing every creature to carry neural tissue.** A photosynthetic blob needs no
neurons, and charging it for them charges most of the world for a capability it never uses.
Plants have no neurons, and that is a strategy rather than an oversight.

---

### D020
**Distal senses are scalars; direction is computed by the body** · 2026-08-07

Five of the six original sensor channels report the creature's own body. Only the photosensor
and contact concern the world, and contact only reports what is already touching. Adequate for
a central pattern generator; inadequate for everything [D017](#d017) is about.

**The largest omission was smell.** §5A.1 says an absorptive cell *"rewards being where food
is"* — and nothing could sense where food was, so absorptive feeding was a lottery rather than
a strategy, with no degree of control able to improve intake. Added: `Chemical`, `Energy`,
`Flow`, `Depth`.

**`Depth` earns its place despite looking redundant with the photosensor.** Irradiance proxies
depth only while the sun is up; once §5A.4's diurnal cycle exists, light at night says nothing
about depth, and depth is the axis the entire environment is structured along. It is what makes
diel vertical migration expressible at all. Named depth rather than pressure because §5.2
disables gravity to get neutral buoyancy, so hydrostatic pressure here would be uniform and the
name would promise a model that is not there. It is the only channel reporting a world-frame
quantity, admitted because depth is a real gradient rather than an arbitrary coordinate — and
the gradient rule below still recovers *which way is up* from morphology alone.

**`Energy` reports the level, not hunger.** A hunger flag needs a threshold, and choosing one
is deciding for the creature when it ought to feel hungry — an unmeasured constant where no run
could vary it. A neuron builds any threshold from a weight and a bias, and several at once.
Hunger is derivable from the level; the reverse is not. Normalised as **seconds of life
remaining at the current burn rate**, so it is a duration rather than a quantity: raw joules
are meaningless across body sizes, and normalising against the reproduction threshold would let
a brood-size mutation rescale how a creature perceives itself.

**Rejected: any channel reporting a bearing to something.** A "direction to nearest food"
sensor hands the creature a solved problem. It is also unnecessary — a scalar sensor on a part
is already a gradient sensor for a body of several parts, since two cells at opposite ends read
different values and the difference is a direction. That is how chemotaxis works, and it makes
**morphology part of the sensory apparatus**: a long creature resolves direction better than a
compact one, and thinks about it slower, because signals meet at one node per step.

**Rejected: image-forming vision.** The photosensor stays a triple — an eyespot, not an eye.
Rendering a view per creature per step would be enormous, and directional light already falls
out of parts facing different ways and shading each other. Consistency is the point: every
distal sense is a scalar and the body does the geometry.

**Evaluated on demand.** Which `(part, channel)` pairs are ever read is static once a phenotype
is developed, so the builder computes a mask and the step loop evaluates only what is in it.
Cost scales with what evolution uses, not with what is declared. This is not premature: §5A.9's
measured bottleneck is already a per-part per-step loop at 88% of the frame, and the mask is
what stops perception becoming a second one.

---

### D021
**A population floor, treated as an instrument rather than a safety net** · 2026-08-07

[D017](#d017)'s world can go extinct, and a dead world is a wasted run. A minimum living count
that spawns fresh founders when breached prevents that.

It also **subsumes world seeding**. At t=0 the population is zero, the floor fires, and
generation zero appears — so there is no separate initialisation path that runs once and is
therefore tested once. One mechanism, exercised continuously.

**The risk is that it lies.** A floor is an exogenous intervention in a deliberately endogenous
system — the class of thing D017 exists to remove — and its failure mode looks like success. If
it fires regularly, the world is not sustaining life, *we* are, and the run still shows a stable
population, births, deaths and accumulating lineages. Every figure consistent with a working
ecosystem; every one of them propped up. That is the same fault as everything in the logbook: a
right-looking number arrived at for the wrong reason.

So the floor reports. A floor spawn is its own event type in `lineage.jsonl`, never
indistinguishable from a birth, and **a floor that keeps firing is a failed world, reported as
failed.** The success condition is that it fires at t=0 and never again.

**Rejected: repopulating from survivors or the recently dead.** Choosing who repopulates is
selection performed by us. Fresh founders require no judgement from us, and it is not a reset
anyway — the nutrient pool left by the dead persists, so refills enter a richer world than the
original founders did.

**Rejected: a population ceiling of the same kind.** §5A.7's mat explodes rather than dies, and
§5A.9 puts real time near 200 creatures — but culling to fit a compute budget is selection by us
of the worst sort: arbitrary, invisible in the lineage record, and biased toward whatever the
cull reaches first. A hard stop with a loud report instead.

**The trap this does not solve:** a floor can mask bad calibration indefinitely. If §5A.2's
ratio is set so nothing can pay its bills, the population never reaches zero and the failure
never announces itself. See [D022](#d022) — the floor's firing rate is the calibration
instrument, and it only works if it is reported.

---

### D022
**Generation depth is the calibration readout** · 2026-08-07

The question [D021](#d021) leaves open — *is this world running itself, or are we running it* —
is answered by **generation depth**: the number of reproduction events between a creature and
its founder.

**Minimum depth among the living is the definition of self-sustaining.** Above zero means no
living creature is a floor spawn. A single integer, no threshold to choose, no averaging window.
An earlier draft of this decision proposed *time since the floor last fired*, which is coarse
and nearly binary; depth is continuous, always available, and has structure.

It is free. §5A.6 makes reproduction asexual and mutation-only, so a birth is exactly one
mutation event and depth is a counter inherited from the parent — which also makes it a measure
of accumulated genetic distance from the founder. Two instruments in one integer.

**The distribution is reported, not just the mean**, because a takeover and a healthy world have
the same mean. A high maximum with a median near zero is one lucky lineage among floor spawns;
a narrow spread is a bottleneck; a wide spread is a working world.

**This is how §5A.2's ratio gets calibrated without guessing it.** There is a phase transition —
below some metabolism-to-photosynthesis ratio depth pins at zero, above it depth runs away — and
sweeping the knob locates the transition without anyone needing to know the right value in
advance. `RunConfig`'s hash exists for exactly this; a swept parameter silently failing to reach
what it configures has already happened twice (logbook/0007, logbook/0008).

Paired with **age at death** and **depth per wall-clock hour**, since a world where creatures
reproduce and die instantly posts healthy depth and is broken.

**Known limit, recorded rather than solved:** depth measures reproduction, not adaptation. A
treadmill — lineages turning over forever with nothing improving — posts excellent depth
statistics. That is §5A.7's plateau, and *is the world alive* and *is the world interesting* are
different questions. What answers the second is undecided; the likeliest candidate is whether
the distribution of strategies moves over time rather than sitting still, but that is a weaker
instrument and is not being adopted on the strength of a hunch.

---

### D023
**The sun is finite: light is competed for, and that is the carrying capacity** · 2026-08-07

[D017](#d017) made energy the selective mechanism and [D022](#d022) proposed sweeping §5A.2's
metabolism-to-photosynthesis ratio to find where a population sustains itself. The sweep was run
over 400× and **found no such setting, because there was none.**

With irradiance a function of depth alone, a creature's income does not depend on how many others
exist. Every creature above break-even accumulates surplus at a fixed rate and breeds on a fixed
period regardless of the crowd — a linear birth process, which grows without bound above
break-even and goes extinct below it. A step function, not a transition. **The knob decided only
how fast the world exploded.** Measured: at 48 W/m² the population went 30 → 37 → 84 → 259 → 907
over 5,000 s, roughly tripling per thousand seconds, with deaths flat at ~100 throughout
(logbook/0011).

What was missing is **density dependence**. The design had no mechanism by which one creature's
existence cost another anything, and no calibration can substitute for one.

**Chosen: make the sun finite.** The world has a horizontal area; it receives
`surfaceIrradiance × worldArea` watts and no more; light one creature absorbs never reaches what
is below it. Total photosynthetic income is then capped whatever evolution discovers. This is
preferred over the alternatives because it is a **conservation law rather than a tuning
parameter** — the same reason §11.2 prefers momentum and energy invariants to plausibility
checks. Carrying capacity stops being a number we chose and becomes a consequence of the world
having a size.

Shading uses `1 − e^(−L/A)` per layer, which is Beer–Lambert's own derivation applied to biomass
rather than water, and what real ocean optics does. What is stored per layer is a **multiplier**
in (0, 1], not an irradiance — see §5A.2b for why an irradiance is a free-energy source.

**Result:** the transition exists and sits between 24 and 32 W/m² for the default upkeep rates,
consistent across three seeds, and it agrees with the analytic break-even for a 0.3 m
photosynthetic cube. Above it nothing runs away: every setting to 400 W/m² settles at tens to
hundreds of creatures against a ceiling of 50,000. More light produces *bigger* creatures, not
more of them, because a larger body shades its competitors — which nothing was told to do.

**Rejected:** *a population-dependent penalty* — a crowding term with a coefficient is the
tuning parameter this replaces, and it would have to be calibrated by the same sweep that could
not find anything. *A hard population cap* — that is selection performed by us, invisible in the
lineage record, and [D021](#d021) already rejected it as a mechanism. *Nutrient limitation
first* — real, and coming in Phase 2, but it constrains absorptive and consumer feeding, and the
lineages that exploded were photosynthetic.

**Known limit, recorded rather than solved:** the world still has no length scale. At 400 W/m²
survivors carry thousands of square metres of surface in an aperture 20 m across, because nothing
relates a body's size to the world's size except the light budget — `Evosim.Core` has no
positions beyond depth. Spatial extent arrives with the physics simulation at Milestone 4.

---

### D024
**Bodies are bounded at both ends, and a bodyless genome is stillborn** · 2026-08-07

Three faults found while calibrating [D023](#d023), all of the same family: a quantity with a
floor and no ceiling.

**1. Size had one absorbing tail.** Mutation perturbs a half-extent by a Gaussian scaled to the
half-extent itself — geometric Brownian motion, whose log diffuses without bound and has no
stationary distribution. §4.5 *depends* on the lower tail hitting `MinPartVolume`:
extinction-by-shrinking is what removes nodes at all, and what makes genome size settle near 39
nodes on its own. Nothing absorbed the upper tail. Bodies grew until half-extents reached
10¹⁸ m, at which point volume (10⁵⁴) passes `float.MaxValue`, upkeep becomes infinite, energy
goes to −∞ and §5A.2's audit is permanently NaN with no way back.

Added `MaxPartVolume`, pruned by the same mechanism as the minimum, giving **extinction by
growing** to mirror extinction by shrinking. It is deliberately set far outside anything the
economy tolerates — 10⁶ m³ is a 100 m cube — because *the economics already forbid giants*:
income scales with area and upkeep with volume, so a body that size starves within one step.
This bound exists only so the arithmetic survives long enough to say so, not to judge what
evolution may build.

**2. Volume does not bound surface area, and the economy pays for area.** A box of half-extents
(10⁻²⁵, 10⁻⁵, 10³⁰) has a volume of 8 m³ and a surface of 10²⁶ m², and `MaxPartVolume` admits
it. Selection found this immediately: shadow areas reached 10³⁷ m² in a 400 m² world. This is
§11.2's physics exploitation moved into the economy — a free lunch discovered rather than
designed, handed over by our own arithmetic.

Added `MinPartHalfExtent`, and it **clamps rather than prunes**. Flatness is a real and valuable
trait: a flat box is the strongest paddle in the shape registry, 12× more directional than a
sphere, so dropping thin parts would delete the best swimmers in the world.

**Rejected: an area-proportional upkeep term.** It is the obviously principled fix — real tissue
costs to maintain per unit area, which is why leaves have a minimum viable thickness — and it
does not work. Income and an area cost both scale linearly with area, so their difference still
scales linearly and thinness is still unbounded; the coefficient can only choose between
"thinning is free" and "thinning never pays", with nothing in between. What actually bounds a
body is that the world's light runs out ([D023](#d023)). Not adopting it is the whole value of
having checked.

**3. A genome that develops into no parts was immortal and free.** With nothing to price, income
and upkeep are both exactly zero, energy never moves, and §5A.6's death-at-zero never fires — a
creature costing nothing, doing nothing, and holding a slot against the population floor forever.
Reachable today, since extinction-by-shrinking prunes the root as readily as any other node. Such
a creature is now refused at admission and counted as a **stillbirth**; the count is reported,
because a rising stillbirth rate says mutation is pushing bodies past what development will build
and looks exactly like ordinary mortality in a population curve.

---

### D025
**Self-sustaining means the floor has gone quiet, not that minimum depth rose** · 2026-08-07

⚠ Supersedes the central claim of [D022](#d022), which remains correct in everything else.

D022 said: minimum generation depth above zero means no living creature is a floor spawn, so the
world is running itself — a single integer, no threshold, no averaging window. It explicitly
rejected *time since the floor last fired* as coarse and nearly binary.

**It is a true statement and an unreachable one.** Nothing dies of age: §5A.6 kills only at zero
energy, so a founder whose income covers its upkeep never dies. A handful of immortal
generation-zero photosynthesisers pin the minimum at zero permanently — measured, in worlds that
had not needed the floor for 17,000 s and were running at median depth 78 with births and deaths
in balance. The instrument reported those worlds as floor-fed. **It was measuring immortality,
not dependence.**

So the rejected option is adopted: how long the floor has been silent. It is coarse, and it needs
a window that minimum depth did not — supplied per run, because it has to be long against the
generation time of whatever lives there, which is a property of the run and not of the
measurement. Minimum depth is still reported and remains the stronger claim if it ever rises. It
will, as soon as anything can die of something other than starvation, and whether senescence
should exist is an open question this makes visible rather than settles.

**The lesson worth keeping:** an instrument whose reading is pinned by a mechanism elsewhere in
the design is not a conservative instrument, it is a broken one. D022 reasoned about what minimum
depth *would* mean without checking whether the world could produce it.

---

### D026
**A body costs what a corpse is worth, and it is one number** · 2026-08-07

Phase 2 of the energy economy: death returns tissue, absorptive cells feed on it, and consumers
scavenge it. Building it exposed that §5A.2's audit had been closing for a smaller world than it
claimed.

**Two things were free.** An offspring's *body* cost its parent nothing — the same endowment and
overhead bought a mote or a whale, and only upkeep noticed afterwards. And a corpse was worth
nothing, so the detritus niche §5A.4 depends on had no fuel and `Consumer` had nothing to eat.

**Both are fixed by one number, and it must be one number.**
`CellType.TissueEnergyPerCubicMetre` is what a body is worth, so it is what a body costs: the
parent pays it at birth, the pool receives it at death. If the two ever differed, a
birth-and-death cycle would create or destroy energy — a free-energy source built in by us rather
than discovered, which is the single thing §5A.2 exists to make impossible. So both sides call
one method.

**The pool is a stock, not a density.** `AbsorptiveCell` read a density and converted it to joules
with nothing anywhere reduced — the same infinite-subsidy shape that made the population unbounded
when light worked that way ([D023](#d023)). It had not bitten only because the density was always
zero. Building it as a finite stock that feeding depletes means the fault cannot appear rather
than appearing later under another name.

**Consumers can finally eat.** `ConsumerCell` fed only on `TissueContact`, which needs physics and
does not arrive until Milestone 4, so consumers earned exactly nothing and the predator valley
§5A.3 worries about was infinitely wide. Scavenging the detritus pool at `CarrionYield` is the
bridge §5A.3 already specified; it just had no pool to scavenge from.

**`CellIntake` replaced a float, and the reason is a conservation law.** A cell used to report one
number. The world needs three facts about it: how much was sunlight (new energy), how much the
cell kept from eating (transferred), and how much left the pool to yield that (drawn). Yield is
below 1, so what is drawn exceeds what is kept, and leaving the difference in the pool would make
every meal a partial refund and a food chain a perpetual motion machine. The first attempt
recovered the split by *re-running the whole metabolic step with the food removed and subtracting*
— two expressions of one quantity, on the only hot loop in the design, at four evaluations per
creature per step.

**Result:** the audit is now an equality across the whole food web —
`EnergyIn − EnergyOut == reserves + bodies + detritus` — measured at 0.0000% residual over a run
with births, deaths and feeding.

**And it supplied the pressure [D023](#d023) noticed was missing.** That entry recorded, as an
oddity, that more light bought *bigger* creatures. It did, because bodies were free. At 96 W/m²
the population was ~38 giants; it is now 6,400 rising to 8,500 while living biomass converges at
about 550 m³. Light still caps total tissue exactly as before. What changed is how it is divided.

**Rejected:** *charging construction as upkeep spread over a lifetime* — it prices the body but
not the decision, so a parent still makes an arbitrarily large offspring for free and the cost
lands on someone who did not choose it. *Refusing a whole brood the parent cannot fully afford* —
a slightly-too-expensive mutation would then cost a lineage every offspring rather than one, which
is a selection pressure invented by the accounting; the affordable prefix is born instead.

**Two mistakes worth recording.** The reproduction gate was left checking only endowment and
overhead while the price now included tissue, so every solvent creature mutated and developed a
genome each step purely to discover it could not pay for it — the test suite went from 18 seconds
to not finishing. And the first sweep after this change looked like unbounded growth; it was not.
Population rose while *biomass* held flat, which is the quantity light actually caps. Watching the
wrong variable made a correct result look like the failure of [D023](#d023).

**Known limit, recorded rather than solved:** detritus does not remineralise, so it sinks and
accumulates on the sea floor forever — 93% of all detritus after 40,000 s. It is a sink and not a
source, so conservation holds, but it grows without bound and whatever first evolves to live down
there inherits a very large bank. Whether that is a flaw or the most interesting thing in the
world is not yet measurable.

---

### D027
**A knob is declared once, and everything else is derived from that declaration** · 2026-08-07

Every tunable used to be written out four times: the property, `RunConfig.Hash()`, the JSON
writer and the JSON reader. With around ninety knobs that is nearly four hundred sites, kept in
agreement by memory. Memory lost twice in two weeks. `DevelopmentLimits.MaxPartVolume` reached
two of the four ([D024](#d024)); `RunConfig.Light` reached none of them; and
`RandomGenomeOptions.JointTypes` — which decides what joints a random genome may draw, and so
which experiments are even different from each other — was found sitting outside the hash and
outside the file during this work.

**`[Tunable]` plus a reflection walk replaces all four.** `ConfigSchema.Of(config)` returns every
declared knob with a path, a group, a unit and a get/set pair. `Hash()` iterates it. The writer
iterates it. The reader iterates it and *demands* a value for each. So a knob that exists is
hashed, is written, and is required on load, and there is no way to be in three of those and
missing from the fourth.

**Reflection for discovery, never for ordering, and the distinction is the whole safety
argument.** `Type.GetProperties()` is explicitly documented to make no guarantee about order. A
hash taken in discovery order would be stable on this runtime and quietly different on the next —
turning §7's promise that `(genome, seed, configHash)` identifies a run into one that holds until
someone upgrades .NET, at which point every stored result becomes unmatchable and nothing says
why. Entries are sorted by full dotted path with an ordinal comparison, so the order is a property
of the names and of nothing else.

**The coverage test is the part that makes it hold.** `EverySettableValueIsDeclaredTunable` fails on *any*
public settable property that carries none of the three attributes. Not a list of types we thought
of — the first draft of that test checked `float`/`int`/`bool`/`string[]` and passed while
`JointType[]` escaped, which is the same fault as the code it was guarding. `[TunableGroup]` and
`[TunableRegistry]` are the two ways to say "this is not a knob", and both are statements someone
wrote down. Silence is the failure.

**It immediately found a dead knob.** `RunConfig.CellTypeMutationChance` and
`MutationRates.CellTypeChance` were two knobs for one thing, differing tenfold, and only the
second had a reader. Setting the first changed the config hash and changed nothing about the run —
two records that claim to be different experiments and are byte-identical. That is §7 failing in
the direction it least wants to, and it is the third instance of this project's recurring fault:
*prove a parameter reached the thing it configures.* The dead one is gone.

**Rejected:** *a hand-maintained list of tunables* — that is what the four sites already were.
*Source generation* — it would move the same declaration into a build step and buy a startup cost
we do not pay; a walk of ninety properties happens once per run. *Deriving the file's sections
from the C# object graph* — the object graph is organised for the code, and the file is organised
for the person editing it, so `Group` is named explicitly and `light` gets a section that no
single object owns.

**Deferred by agreement:** a GUI for editing these. The schema is what such a UI would need —
path, group, unit, description, typed get/set — and it now exists, so building it is presentation
work against a stable surface rather than a second copy of the list.

---

### D028
**Drag is summed in the part's own frame, and the panels are built once** · 2026-08-07

§5A.9 had measured the drag loop at 88% of the step against PhysX's 12%, and named two levers as
deferred to Milestone 4. This pulls them, plus a third.

**The fork this started from was wrong.** The speed question had been posed as *keep every
creature in PhysX, or sample physics occasionally and run the economy in arithmetic.* The second
is aimed at 12% of the cost. Physics was never the wall; our own managed loop was.

**Three changes, no change to any force.**

- **Summed in the part's local frame.** Two quaternion rotations per panel — 48 for a box — become
  four per part. A rotation preserves the dot product and `Rᵀ(ω × Rc) = (Rᵀω) × c`, so this is a
  change of basis and not of arithmetic.
- **Panels built once**, at a creature's first step, rather than regenerated through a virtual
  call per part per step. Local geometry is fixed at development.
- **The arithmetic phase spread across cores**, between a serial gather and a serial apply, since
  Unity permits neither `Transform` reads nor `AddForce` off the main thread.

**Measured: 4.0× on the step at 512 creatures, 6.4× on the drag loop, and real time now holds to
512 creatures rather than about 200.**

**Equivalence is asserted, not asserted-to.** `DragEquivalenceTests` keeps a frozen transcription
of the world-space loop and holds the new path against it: agreement to 6–9×10⁻⁸ of the
panel-force scale, against a float epsilon of 1.19×10⁻⁷. This is the one place the project
deliberately keeps two implementations of one quantity — the usual rule (logbook/0009) is the
opposite, and it inverts here because the claim being made *is* that they are equal.

**Rejected, and rejected on the measurement rather than on principle:** *vectorising the panel
sum*, and *replacing the 2×2 midpoint sampling of each face with Gauss–Legendre nodes or a
closed-form integral*. Both are sound. Both are now aimed at the arithmetic share of a loop that
is 55% of a step, and the measurement says arithmetic is the minority of what remains: the first
two changes alone gave 3.8× on a single creature, so parallelising 2,871 parts across 24 cores
added only 1.7×. **What is left is per-body engine interop, which is serial by Unity's rules.**
The ceiling now moves with batching, not with flops.

**Also rejected:** *physics level-of-detail between creatures in one world* — a creature under a
cheaper drag model swims differently, ends up somewhere else and earns different light, which is
two populations under two physics reported as one experiment. And *caching a gait per genome and
advecting kinematically* — highest theoretical payoff, and §11.2 already records this drag model
as exploitable; a cache would be a third model to exploit, and it deletes exactly the physical
accidents that make evolved swimming worth watching.

**Still open, and deliberately not decided here:** the timestep. It is the only lever that
multiplies simulated seconds rather than dividing the cost of one, and its acceptance test already
exists — if the energy audit's residual grows, the speed was bought with free energy that
selection will find. Also `AddedMassCoefficient`, which is 0 today and consumes dt headroom when
enabled, so it has to be settled before any dt sweep means anything.

---

### D029
**The physics and the economy are joined by one method, and it revealed that work must not be billed yet** · 2026-08-07

Three facts, found by grep before anything was written: `new World(...)` appeared only in tests,
`workJoules:` was `0f` at every call site in the repository, and `Organism.HeightY` was written
once at birth and never again. **Two correct halves that had never met** — physics could swim a
creature and measure its work, the economy could feed and kill one, and no number crossed between
them.

It also bounds what §5A.2b established: those numbers describe a world of stationary organisms for
whom motion is free.

**The seam is `World.Observe(creature, heightY, workJoules)` and it points one way.** §6.1 forbids
`UnityEngine` in `Evosim.Core`, so the world cannot ask where anything is; `Evosim.Sim.Ecosystem`
reads the articulations and pushes both numbers in, then reconciles births and deaths against the
scene. The world stays runnable with nothing attached, which is the fast sweep.

**Work accumulates and is drained exactly once.** Physics runs at 100 Hz and the economy at 2 Hz,
so a metabolic step is the sum of fifty strokes. Charged twice it destroys energy; never drained it
bills a creature forever for one stroke. The audit holds at 0.0000% with the term live.

**Two clocks, and only one of them is safe to coarsen.** Energy is an integral, so a slower
metabolic clock changes its quantisation and not its value. A slower *physics* clock changes what
is physically possible and hands free energy to whatever finds it (§11.2). They are not the same
kind of approximation.

**What the first run found: every creature with a moving part was dead within sixty simulated
seconds**, and none was ever born again — 10% jointed at t=20, 2.3% at t=40, 0% thereafter. The
population then grew from 45 to 183 without a joule of mechanical work.

**And this is correct behaviour, not a fault in the join.** There is no brain evaluator. The genome
carries `NeuronDef`, `NeuronInput` and oscillator frequencies, development places them, and nothing
reads them — every creature is driven by one shared sine on every degree of freedom, identical
regardless of genome. So the controller is a constant, a uniform flap produces no net thrust
(measured: 0.0003 m/s while spending 38 J/step), and the 27× light gradient overhead is
unreachable. A real cost against an unobtainable benefit. Selection removed the cost.

**Therefore: billing mechanical work belongs after Milestone 6, not at Milestone 4.** Until a
creature's own genome decides how it moves, work is a tax on *having* a body part rather than a
price for *using* one, and §10's ordering is wrong on this specific point. The join itself belongs
exactly where it is.

**Rejected:** *setting `WorkCostMultiplier` to 0 as a new default* — a default chosen to make an
uncomfortable result go away is how a finding gets buried. It stays at 1 and the finding is
recorded. *Widening joint limits to reduce the waste* — the smoke test measured that 4× wider
ranges cut energy destroyed at the stops from 74.5% to 14.4%, but a smaller pure cost against an
unobtainable benefit is still selected out; it delays the extermination without preventing it.
*Adding the current field now* — it was next on the list and would push creatures off their depth
with no controller able to hold station, which adds mortality rather than a reason to swim.

**One thing worked that neither half could have produced alone.** Mean depth rose from −9.98 m to
−5.91 m while mean speed was zero: the shallow out-bred the deep and the population's centre of
mass moved by differential survival. Selection acting on a spatial trait, in a world where the
trait cannot be changed within a lifetime.

---

### D030
**The genome's brain is evaluated, and creatures can swim** · 2026-08-07

[D029](#d029) found that billing mechanical work exterminated every joint in the world in sixty
seconds, because every creature ran one shared test sine and a uniform flap produces no net
thrust. The genome had carried neurons, oscillator frequencies and input references since draft 1;
development copied them onto every part; mutation perturbed them. **Nothing read them.**

**Less was missing than it looked.** The effector side was complete (§4.4's torque + mass-scale +
10-step average, validated by Spike 01 M4) and the genome side was complete. The entire gap was
one function: `Phenotype + time → float[TotalDof]`.

**Three decisions the genome did not make for us.**

- **Synchronous update.** Neurons read the previous step's outputs and write a separate buffer,
  swapped at the end. In-place update would make a neuron's value depend on part iteration order —
  the plausible-number fault this project has paid for twice (logbook/0007, 0008) — and it is what
  makes §4.4's "one node per step" latency a property rather than an accident. Measured: a signal
  reaches parts 0, 1, 2 on steps 1, 2, 3.
- **Neuron *d* of a part drives DOF *d*.** The genome has no effector-mapping field, so this was
  invented rather than read. It is the only mapping that survives recursion, since neurons are
  copied with the morph node. Summing all of a part's neurons instead would make gain depend on
  neuron count, so adding a neuron for any unrelated purpose would silently change how hard the
  creature swims.
- **`Sigmoid` is `tanh`.** Both are sigmoids; only one is centred. A logistic curve is strictly
  positive, so a joint driven through one could only ever push one way and could not oscillate —
  paralysis in a direction, reported by nothing except a poor swimmer.

**Every operator is implemented, not only the MVP set**, because §4.3 states the MVP is a
population constraint rather than a separate system. Restricting the evaluator would turn a
constraint on the starting population into a permanent ceiling.

**Result: joints survive.** Jointed share at t=200 went from 0% to 6.4%, and it is no longer zero
at t=60; deaths over the run halved. The audit still closes at 0.0000%.

**And the mechanism is confirmed reachable, which the mean could not show.** Mean speed in an
embodied run is ~0.0002 m/s, which reads as "nothing swims". The distribution says otherwise: over
200 random genomes driven by their own brains, the median is 0.0062 m/s, the 90th percentile
0.0276, and **the best is 0.485 m/s — 78× the median, with no selection at all.** 35% exceed 1
cm/s. A good swimmer exists in roughly one genome in two hundred.

**So [D029](#d029)'s conclusion narrows rather than reverses.** Billing mechanical work is still
premature, but no longer because swimming is impossible — because two hundred simulated seconds
and 114 births is nowhere near enough search to find a swimmer, let alone keep one. What follows
is a longer run, not another mechanism.

**Rejected:** *restricting the evaluator to the MVP operators* — see above. *Reading sensors in
this pass* — the MVP set's oscillators have arity zero and need no inputs at all, so a working
per-genome gait costs no sensor plumbing; closing the loop is Milestone 6 and gets §4.4's
requirement-mask treatment when it lands. *Summing neurons into a joint, or a dedicated output
neuron* — the first makes gain depend on neuron count, the second needs a new genome field.

---

### D031
**What owning an actuator costs is the knob that decides whether anything can move** · 2026-08-07

[D030](#d030) closed by recommending a longer run: a random genome swims at 0.485 m/s about one
time in two hundred, so the capability exists and 200 seconds was too little search to find it.
**That recommendation was wrong, and ten minutes of running it said so.**

**§5A.2b's calibration is superseded by embodiment.** It located the self-sustaining transition at
32 W/m², measured before bodies cost tissue ([D026](#d026)) and before swimming cost work
([D029](#d029)). Embodied, 48 W/m² sustains nothing — 6 births against 99 deaths, population pinned
at the floor, `gen min` never leaving zero — and 64 W/m² is barely above replacement. The numbers in
that section describe a world that no longer exists and must be re-measured before being quoted.

**Joints were dying at every irradiance tested** — 64, 100, 150, 200, 400 W/m², with up to 4,046
births and lineages sixteen generations deep. Not a search-time problem.

**The cause is the standing cost of owning an actuator**, `IdleWattsPerNewtonMetre` ×
`MaxLinkPower`. At the shipped 0.02 × 120 that is **2.4 W billed continuously before the joint
moves once**, against roughly 2.3 W of total income for a photosynthetic part at 100 W/m². The
actuator costs a creature's entire earnings simply to exist. `LinkCell`'s own documentation names
the failure — *"too high and nothing can afford to move"* — and §5A.10 marks the knob unmeasured.

**Measured, nine runs at 100 W/m² over 1500 s, sorted by the product:**

| idle | maxPower | standing cost | jointed alive at t=1500 |
|---|---|---|---|
| 0.02 | 120 | 2.4 W | 0, 1, 1 *(3 seeds — default)* |
| 0.02 | 60 | 1.2 W | 1 |
| 0.005 | 120 | 0.6 W | 4, 11, 4 *(3 seeds)* |
| 0.02 | 20 | 0.4 W | 4 |
| 0.02 | 8 | 0.16 W | 8 |

Monotonic in the product, reached independently down two different knobs, consistent in direction
across three seeds. **A joint is affordable at well under a fifth of what a body earns and
unaffordable at all of it**, and the default is on the wrong side by about 5×.

**The default is deliberately left where it is.** A knob moved to make an uncomfortable result go
away is how a finding gets buried, and *which* of the two to move is a design decision rather than
a measurement: the coefficient is the pressure §5A.10 intends to keep evolved `Power` down, while
the power range is what founders start from. Only one of those should absorb the correction, and
that choice is not the measurement's to make.

**Two reading errors worth recording, both of the same shape.** The harness reported jointed
creatures as a percentage and the population was quadrupling inside the window, so "0%" read as
extinction when the underlying count was going 11 → 14 → 16 → 19. And a first four-point sweep was
called a clean curve one step before its cheapest setting turned out to perform *worse* than the
setting above it — noise, at counts between 4 and 19 with one run per point. Both were aggregates
that looked more authoritative than the counts underneath them, which is the same fault as
[D030](#d030)'s mean hiding a 78× tail. The harness now prints counts.

**Unplanned, and it passed:** two runs of the same config and seed in separate Unity processes
produced byte-identical trajectories. §7 promises same-machine same-version reproducibility and
declines to promise more; it holds.

**Still not known:** whether persisting joints ever produce a swimmer. The fastest creature in any
of these runs manages 0.0075 m/s. Persistence is a precondition, not evidence.


---

### D032
**The actuator-cost correction goes on `MaxLinkPower`, and the two knobs are not interchangeable** · 2026-08-24

`RandomGenomeOptions.MaxLinkPower` 120 N·m → 20 N·m; `LinkCell.IdleWattsPerNewtonMetre` left at
0.02.

[D031](#d031) measured the standing cost of owning a joint and left the correction unapplied,
because *which* knob absorbs it is a design decision. It is applied here, and the argument it was
applied on turned out to be wrong in a way worth recording rather than quietly fixing.

**The argument given, and why it was wrong.** D031 observed that the coefficient and the ceiling
enter the ledger as a product and concluded that cutting either would do, so the choice could rest
on which knob carries design intent — the coefficient is the pressure that keeps evolved `Power`
down, so the ceiling should move. Both halves of that are true and the conclusion does not follow.
**Both knobs enter the cost as a product; only `MaxLinkPower` enters the benefit**, because it
multiplies straight into joint torque and torque is what makes thrust. D031 measured one side of a
two-sided trade and read it as symmetric.

Measured on identical genomes — the setting consumes the same single RNG draw either way, so the
two arms are the same animals with different muscles:

| | 20 N·m | 120 N·m |
|---|---|---|
| founders over 1 cm/s | 4% | 14.5% |
| randomViable median | 0.0047 m/s | 0.0079 m/s |
| randomViable over 1 cm/s | 22.5% | 38.5% |
| randomViable best | 0.072 m/s | 0.261 m/s |

So the correction costs **1.7× to 3.6× of swimming ability**, in still water with no selection.

**It is applied anyway, and provisionally.** At 120 N·m nothing with a joint survives at any
irradiance from 64 to 400 W/m² (D031), and three times the thrust on a creature that starves is
worth nothing. Six embodied runs at 100 W/m² with genuinely independent seeds cannot separate the
two settings at all — seed 2 gives zero jointed creatures under both, seed 3 gives the most under
both, and the seed-to-seed spread swamps the knob (logbook/0019). **This is a reversible
calibration choice resting on a measurement that n=3 cannot make**, not a design commitment, and
the honest position is that the affordable region has a lower edge nobody has looked for.

Superseded in part: D031's "they enter the ledger as a product, so cutting either should do".

---

### D033
**Founders draw sensor references from the implemented channel set, and mutation may rewire an input** · 2026-08-24

§4.3 describes the founder population as a pure CPG with self-only connections. That was
never quite what `GenomeFactory` did — it has always drawn `SensorChannel.JointAngle` for half of
every neuron's inputs — and it is now deliberately not what it does.

**The reason is that an open-loop swimmer cannot aim.** [logbook/0018](logbook/0018-nothing-to-swim-towards.md)
argues that in an economy whose only prize is a gradient in depth, undirected locomotion earns
nothing on average while costing real work, so the ledger correctly deletes it. A controller that
cannot read the world is not a simpler version of one that can; it is one for which the trait being
selected is unreachable, which is the same shape as the shared test sine that
[D030](#d030) removed.

Three things follow, and each is a rule rather than a value:

1. **`SensorChannels.Implemented` is what new references are drawn from.** Every channel stays
   legal in a genome — the enum is complete so the format does not change when a milestone lands,
   and an unimplemented channel reads zero by definition (§4.4). But *introducing* one by mutation
   spends a sensory mutation on a dead input, and a dead input is indistinguishable from a live one
   that is not helping.
2. **The list is a promise Core makes about Sim**, which §6.1 makes uncheckable by reference. It is
   checked by measurement instead: the Milestone 1 smoke test drives a creature and fails any
   listed channel that reads a constant.
3. **`MutationRates.RewireInputChance` is implemented.** It was declared, hashed, serialized and
   set to 0.9 by a test, and nothing read it — so neuron topology inside a node was frozen at
   whatever the founder drew, for every generation of every lineage ever run here (logbook/0019).

**Rejected: restricting which channels a founder may read.** §4.4 already rejects cell-type
restrictions on perception, on the grounds that what should limit a sense is cost rather than
permission. The same argument applies to founders. A channel a founder cannot draw is one evolution
reaches only through mutation, which is a slower version of the same outcome bought with a rule
somebody has to remember.

---

### D034
**Per-creature seeds are mixed with the world seed, not counted from it** · 2026-08-24

`Rng.SeedFor(stream, index)` — SplitMix64's finaliser — replaces `_nextSeed++`.

`World` issued every founder and every birth a seed from a counter initialised to the run's own
seed. A run seeded 1 drew its founders from seeds 1…40; a run seeded 2 drew from 2…41. **Two
"independent" runs shared thirty-nine of forty founder genomes.**

It surfaced as an anomaly rather than a suspicion: an embodied A/B reported the same fastest-ever
speed to four significant figures under two different seeds, which by this project's own rule
(logbook/0007, logbook/0008) means a change has not reached what it configures. It had; the seeds
had not.

**Consequence, stated plainly: every claim of the form "consistent across three seeds" made before
today rests on much less than it reads.** [D031](#d031)'s actuator calibration is the one that
matters. Its rows are not withdrawn — the direction it found is monotonic in a physical quantity
and reproduced down two independent knobs — but its *replication* is not replication.

**Rejected: a wider stride between runs.** Starting run *n* at `n × 1_000_000` makes overlap
unlikely rather than impossible and leaves the streams correlated in a way nobody would think to
check. The fault being replaced was already the plausible-looking kind; the replacement should not
be. A mixing bijection with avalanche gives unrelated outputs for adjacent inputs and cannot
collide.

Two tests hold it, and the one that matters — that two world seeds give different founder
populations — was run against the old code first to confirm it fails there. A guard that has never
been seen to fail is a guard trusted on its intentions (D027).

---

### D035
**The diurnal cycle is mean-preserving, and off by default** · 2026-08-25

`LightModel.DayNightAmplitude` and `DayLengthSeconds` implement §5A.4's day/night cycle.

**The objection this answers.** `LightModel` carried a paragraph explaining why there was no cycle:
one turns §5A.2's calibration question from *does light cover upkeep* into *does light cover upkeep
averaged over a period, and can anything survive the trough* — two unknowns at once, before either
had been measured alone. That is a good objection and it is answered by construction rather than by
deferral. `SurfaceIrradiance` remains the daily **mean** and the amplitude modulates around it, so
amplitude 0 reproduces the acyclic world exactly and turning it up does not move the world's energy
budget by one joule. One new unknown, with a defined zero.

**Rejected: `max(0, sin)`, the obvious shape.** It gives a true half-day night and averages to 1/pi
of its peak, so switching it on at a fixed irradiance would cut the world's income to a third and
present as a diurnal effect. Most of the resulting difficulty would have been an unannounced 68%
cut to the sun. A centred sinusoid trades a literal night for a budget that does not move, and the
budget is what every earlier result was measured against.

**What it does not buy, stated as a test rather than a caveat.** Irradiance stays monotonically
decreasing in depth at every hour, so light alone always favours the surface and a cycle moves no
optimum on its own. What it moves is the *balance* against the deep income — and the first runs
measured food income at **0% of all income**, because absorptive cells are effectively unreachable
from a founder at the current `CellTypeChance` (logbook/0020). With one income term there is
nothing to track. `TheSunMovesAndTheSurfaceIsStillTheBrightestPlace` asserts the monotonicity so
this limit cannot be quietly forgotten.

**Mean-preserving is not survival-preserving**, and this is the half of the original objection that
was load-bearing. At full amplitude, on identical settings and seeds, deaths roughly tripled and
the population ended about 40% lower. Death is a threshold, and the threshold of an average is not
the average of a threshold: a creature that runs out of energy at midnight does not get to average
over the following noon. The audit closes at 0.0000%, so this is a harder world rather than a
leaking one.

**Off by default**, for the same reason D032 is marked provisional: every number on file was
measured without a night, and a default that changed them all would mean no earlier result
described a world that still exists — which is what §5A.2b turned out to be (D031), and is not a
thing to do twice deliberately.

**The phase is not on `LightModel`.** It lives on `LightField`, which already holds the other thing
about light that changes every step. D027's coverage guard rejected the first attempt within a
second — *settable but not `[Tunable]`* — and was right: that class is configuration, §7 hashes it,
and where the world has got to is not part of how it was set up.

That guard's companion, the hash test, failed too, and that one was a hole in the guard. It nudged
every float tunable by +7.5, which silently required every knob in the project to be unbounded —
the opposite of §7's *loading refuses rather than defaults*. It now shrinks the nudge until one is
accepted, and still fails if none is.

---

### D036
**The world needs a current: energy has no return path without one** · 2026-08-25

§5A.4 has always specified a current — *"a procedural divergence-free field (curl noise), evolving
slowly"*, entering the model at one point because `FluidModel.BoxDrag` already takes a velocity, so
`bodyVelocity - currentVelocity` gives real advection for a noise lookup. §10 lists it under
Milestone 4. It was never built, and this records that it is not optional.

**Three separately measured failures resolve to its absence** (logbook/0021):

1. **Energy is immobilised, not lost.** Light enters at the surface, plants grow and die at the
   surface, and their bodies sink past everything that could eat them onto a floor sixty metres
   down. By 3,500 s, 77.5% of every joule of dead matter the world has produced is on the sediment
   and the nutrient density where the living population sits is exactly zero. The audit closes at
   0.0000% throughout, which is the point: the books balance perfectly on a world with a one-way
   trip from surface to bottom. Real oceans have this problem and solve it with upwelling, which is
   where essentially all marine productivity comes from.
2. **Position is inherited and effectively immutable.** A creature moves **six millimetres** from
   where it was born over a mean life of 289 s, against a founder scatter of twenty metres — a
   ratio of 1:3300. Swimming accounts for roughly 0.03% of the variance in a creature's realised
   depth, so selection cannot see it at any generation count. A current makes birth depth stop
   being destiny.
3. **Swimming has no climbable gradient, and this is the load-bearing one.** In still water doing
   nothing is free *and optimal* — drifting costs zero, the best strategy is to be a blob, and
   every run so far has duly converged on blobs. In moving water, **station-keeping is a task with
   continuous returns from arbitrarily close to zero**: a creature that swims slightly holds
   position slightly better than one that does not swim at all. That is a gradient evolution can
   climb from nothing. "Swim four metres to reach better light" is not, because the first four
   metres pay nothing.

**Rejected: making creatures faster instead.** The obvious reading of six millimetres is that
muscles are too weak, and D032 already shows torque buys speed. It does not fix this. The problem is
the *ratio* of achievable travel to inherited scatter, and closing a factor of 3,300 by strengthening
actuators would need a world where creatures cross the water column in a lifetime — which is a
different world, not a calibrated one. A current changes the denominator instead, and changes it for
free.

**Rejected: reducing `FounderDepthSpread` to shrink the scatter.** Same ratio from the other end, and
it works, and it is the wrong mechanism: it makes swimming selectable by making the world flat, which
removes the vertical structure §5A.4 exists to provide. It would trade the reason for locomotion
against the ability to select for it.

**Note what this says about D035.** The diurnal cycle was built to give locomotion a moving target
and could not, because the second income it needed does not exist. A current is upstream of it: with
detritus returned to the lit zone, absorptive feeding becomes viable, the two incomes finally pull in
opposite directions, and the cycle has a balance point to move. The cycle is not wasted work; it was
built one layer too early.

---

### D037
**The current is two standing waves, and stirring is separate from it** · 2026-08-26

[D036](#d036) established that the world needs a current. This is what was built, and two of the
three choices in it were forced rather than preferred.

**Moving water and stirring are separate mechanisms.** `CurrentField` advects bodies;
`NutrientField.Mix` diffuses detritus. One cannot do both jobs, because **detritus is not
physical** — a corpse is deposited into a scalar field indexed by depth and its articulation is
destroyed, so a velocity field applied to `ArticulationBody` drag cannot touch it. That is not an
omission to fix later: §6.3 tiles creatures a hundred metres apart so they cannot collide, which is
what defers predation to Milestone 7, and a physical corpse would sit in its own tile where nothing
could reach it. The scalar field is what lets a creature at −8 m eat detritus at −8 m without being
in the same place, and it matches the one-dimensional ecology that tiling forces. **Physical
corpses become the right design at the same moment tiling stops being necessary, and not before.**

**Rejected: the curl-noise field §5A.4 specifies**, on a proof rather than a preference. Tiling
makes horizontal position a recycled bookkeeping slot, so a field that reads it makes an artefact
ecologically meaningful — which forces the field to depend on depth and time alone. For such a
field `div v = ∂w/∂y`, so divergence-free requires `w` constant in depth: a uniform drift that moves
every creature identically and shears nothing past anything. Depth-varying vertical flow and
divergence-free are incompatible here. The compensating horizontal circulation is real and lies
outside a column one tile wide.

**Rejected: a single travelling wave**, which was built first and was a conveyor belt. Its
time-average velocity at every fixed depth is exactly zero — the Eulerian mean — and a particle
riding it is still dragged along with the phase. The first embodied run carried the population six
metres above the surface and was still climbing (logbook/0022). What matters is the mean
displacement of something the flow carries, and the guard that existed asserted the other one while
its own comment described this one.

**Two standing waves at incommensurate periods.** Each term is `sin(ky)·sin(ωt)`, antisymmetric
about its half-period, so a particle in one term returns exactly home — zero drift by symmetry
rather than cancellation, independent of the integrator. One term alone would also mix nothing, so
there are two, in the ratio of the golden section: their sum never repeats, trajectories separate,
and the field disperses without a mean. The ratio is a constant rather than a tunable because a
rational value would give the whole field a common period and switch the mixing off at a timescale
nobody chose.

**Both default to off.** Every number on file was measured in still water, and a default that
perturbed them would mean no earlier result describes a world that still exists — the mistake
D031 recorded and D035 declined to repeat.

**Measured, same seed and settings, 64 W/m²:** detritus on the sea floor falls from 77.5% to 2.7%;
nutrient density where creatures actually live goes from 0 to 0.18 J/m³; and the distance a creature
travels from its birth depth in a lifetime rises from 0.006 m to 0.34–0.70 m — roughly a hundredfold,
which is the ratio D036 named as the reason selection could not see swimming.

**What it did not fix, and the number that explains it.** Absorptive creatures still starve. Their
break-even is size-independent, since income and upkeep both scale with volume:
`upkeep / clearance = 4 / 0.5 = 8 J/m³`, against a world that produces 0.18. The root is further
back: a cubic metre of tissue is worth 500 J and costs 3–4 W to maintain, so **a body is worth about
two minutes of its own metabolism**, and detritus at 8 J/m³ would require the water to hold some
sixty times the world's standing biomass in corpses. Nothing remineralises detritus, so the pool
grows without bound and would reach it in around 260,000 simulated seconds — hours of wall clock,
and long after the population runaway ends the run.

---

### D038
**Ageing is an energy phenomenon, and immortality was suppressing selection** · 2026-08-26

§5A.6 kills at zero energy and nowhere else, so **a creature whose income covers its upkeep never
dies.** §5A.6b already recorded the consequence and treated it as an instrument fault — "a handful
of immortal generation-zero photosynthesisers pin the minimum at zero permanently" — and struck out
minimum generation depth as *"a true statement and an unreachable one"*. It is an ecological fault
as well, and the larger of the two. **Selection needs differential mortality, not only differential
reproduction.** A lineage that succeeds and is never replaced, only added to, is a world in which
almost nothing is selected: measured at 98 deaths against 1,164 births (logbook/0022), and still
only 233 against 1,475 once shading began to bite.

**`SenescenceDoublingSeconds` changes the terms of being alive rather than killing anybody.** A
maximum lifespan would be exogenous — us deciding how long a creature ought to live, which is what
§5A.0 exists to remove. At age *t* the wear factor is `1 + t/T`, and death stays exactly where
§5A.6 puts it, at a reserve of zero. An old creature starves; how long that takes depends on how
well it earned, which is the world's answer rather than ours.

**Both sides of the ledger, from the one number.** Upkeep and neural cost are multiplied by the
wear factor and income is divided by it, so an old body spends more *and* converts less. Costs
alone would have been the cheaper implementation and the wrong biology: senescence is loss of
function first and expense second, and a creature photosynthesising at full efficiency until the
day it starved would be an odd thing to call old. The human asked for exactly this and was right to.

**What falls is what a creature keeps, not what it takes.** `PoolDrawn` is unscaled. An ageing
population strips the larder at full speed and feeds itself worse on it, and the shortfall leaves
the world through the transfer loss §5A.3 already accounts for — so §5A.2's audit closes at 0.0000%
with no new term. Scaling the draw instead would have made ageing a form of restraint, with a world
of the old depleting *less* than a world of the young.

**Rejected: making it heritable.** Nothing here costs anything to repair, so an evolvable senescence
rate goes straight to zero and buys immortality free — a §11.2 free lunch arriving through the
ledger rather than through the physics. Evolvable senescence needs the disposable-soma trade-off,
where repair competes with reproduction for the same joules, and that is a larger design than a knob.

**Default 0, bit-identical to the immortal world**, guarded by a test rather than by intent. Every
number on file was measured without this, and D031 is what a default that silently perturbs the
ledger costs.

#### What it measured

64 W/m², current 0.05 m/s, mixing 2 m²/s, `maxPower` 20. **The doubling time has to be long against
the reproductive career, not against the lifespan**, and the first two values tried were chosen
against the wrong quantity and were lethal:

| T | alive at t=4000 | births | floor spawns | gen max | gen min |
|---|---|---|---|---|---|
| off | 1,342 | 1,475 | 100 | 17 | **0** |
| 30,000 s | 1,066 | 1,177 | 99 | 14 | 0 |
| 10,000 s | 876 | 946 | 105 | 15 | 0 |
| 3,000 s | 106 | 142 | 134 | 20 | **1** |
| 1,200 s | 40 | 35 | **473** | 9 | 0 |
| 300 s | 40 | 42 | **754** | 1 | 0 |

At 300 s a creature pays double before it has reproduced once, which is what `gen max = 1` says
happened; both short arms pinned at the population floor and were kept alive by it, which §5A.6
defines as a failed world.

**Minimum generation depth rises above zero, for the first time in this project.** Replicated
across three independent seeds (D034), it climbs and holds rather than touching:

| run | first t with gen min > 0 | gen min at end | alive | deaths / births |
|---|---|---|---|---|
| T=3,000, seed 1 | 3,600 s | 1 | 106 | 170 / 142 |
| T=3,000, seed 2 | 3,500 s | **3** | 1,919 | 1,226 / 3,038 (40%) |
| T=3,000, seed 3 | 4,400 s | **6** | 699 | 524 / 1,046 (50%) |
| T=5,000, seed 1 | 4,700 s | **3** | 619 | 582 / 1,089 (53%) |

Gen min is 0 in every immortal control, always. **No floor-spawned creature is
alive in any of these worlds**, which is what §5A.6b called *the point at which life became
self-sustaining* before striking it out as unreachable — and the struck passage names this exact
mechanism as what would make it reachable again. Seed 1's small population turns out to be
seed-specific; the rest carry 619–1,919 creatures, so this is turnover rather than collapse.

**The control is sharper than a mortality comparison, and corrects one.** The immortal world is not
a world without death — at seed 2 it kills 828 of 2,496 (33%) by t=4,100, because shading at 1,761
creatures is severe. Matched on seed and elapsed time:

| at t=4,100 s, seed 2 | immortal | T=3,000 |
|---|---|---|
| alive | 1,761 | 1,599 |
| deaths / births | 828 / 2,496 — 33% | 629 / 2,121 — 30% |
| shading | 57.5% | 48.1% |
| gen max | 19 | **23** |
| **gen min** | **0** | **3** |

**Near-identical mortality, and only one of them retires its founders.** What immortality protects
is not creatures in general but the specific creatures that got the good depths first and never had
to give them up. That is the thing senescence removes, and it is why the death *rate* was the wrong
number to look at.

**And it is faster.** Seed 1 at T=3,000 reached generation 20 in 1.3 minutes of wall clock against
the immortal world's 17 in 6.9. §5A.6b calls depth per wall-clock hour the actual evolutionary
clock; carrying thirteen hundred immortal creatures was buying population, not progress.

⚠ **T itself remains unmeasured (§5A.10).** What is measured is that the transition exists and lies
between 1,200 s and 3,000 s for this configuration. It is a property of the world's energy margin,
not a constant, so it will move whenever §5A.2's ratio does.

---

### D039
**The trophic niche is arrival-limited, not viability-limited — measure the margin, not the
break-even** · 2026-08-27

§8 demotes MAP-Elites on the grounds that under endogenous selection its innovation-protection role
*"passes to ecological niches — spatial and trophic"*. That is a claim about the world, so it is
measurable, and D038's corpses made it worth measuring: nutrient density where creatures live rose
from 0.18 to 2.1–4.3 J/m³ and then, over a 15,000 s run, to **12.3 J/m³** — past the absorptive
break-even of 8. **The trophic niche opens on its own.** Absorptive creatures arrived, survived a
thousand seconds apiece, and produced the first non-zero food income on record (0.03 → 0.12% over
four consecutive samples). The count never reached two.

**Break-even is the wrong number and this entry retires it.** `upkeep / clearance = 4 / 0.5 = 8
J/m³` is where a trade stops losing money, not where it is worth doing: §5A.6 pays for offspring out
of surplus, so a creature at break-even survives forever and founds nothing. **The quantity that
decides a trade is the margin** — net watts per cubic metre, divided into a body's tissue cost to
give the seconds it takes to earn its own replacement. That number is comparable across trades that
acquire energy in completely different ways, which break-even is not.

**Measured, and it refuted the change this entry was going to authorise.** The plan was to raise
`AbsorptiveCell.ClearanceRate`, on the reasoning that a 25% margin over break-even explains
creatures that survive without breeding. Both that knob and its upkeep are ⚠ unmeasured (§5A.10), so
setting them was legitimate. Then:

| trade | conditions | net | earns its own tissue in |
|---|---|---|---|
| photosynthetic | −2 m, full sun | 1.063 W | **470 s** |
| absorptive | −2 m, 10 J/m³ | 1.000 W | **500 s** |

**Parity, at 1.06×.** At the density the world now produces, eating the dead is as good a living as
photosynthesis in the light. No knob was changed. `TrophicMarginTests` keeps the measurement.

**What is actually scarce is arrivals.** Two sources, both measured in `TrophicInvasionTests`:

| source | rate |
|---|---|
| founder draw | **24.7%** carry an absorptive part |
| cell-type mutation from a photosynthesiser | **one per 5,128 births** |

The founder draw is 1,265× faster and **the population floor is its only user.** D021 makes the
floor fire only to hold the population up, so it goes silent when the world becomes self-sustaining
— at t≈2,700 s, against a larder that does not become worth entering until t≈9,500. **The world
switches off its supply of consumers at the moment it starts producing food for them.** Neither
mechanism is wrong; their timing collides.

**Rejected: making the floor draw consumers when the larder is rich.** It would work and it is
§5A.0's exact prohibition — us deciding what the world should contain, at a moment we chose, on
evidence we read. The floor is an instrument, and D021 is emphatic that it is not a safety net.

**`EVOSIM_CELLTYPE_MUTATION` is exposed as a probe, not as a new default.** Arrival-limited and
establishment-limited predict opposite things when the arrival rate is raised twentyfold — a trophic
level, or a parade of solitary creatures that never become two — and nothing else distinguishes
them. The default is unchanged.

⚠ **Open.** Which of the two it is, is being measured. If it is arrival-limited the fix is
ecological (a source of consumers that does not switch off); if establishment-limited, something
not yet identified prevents a viable trade from founding a lineage, and that would be the more
interesting result.

---

### D040
**The world grew a food chain, and the binding constraint is now throughput** · 2026-08-27

[D039](#d039) left one question open: absorptive creatures arrive on a larder that can feed them,
survive a thousand seconds apiece, and the count never reaches two — **arrival-limited or
establishment-limited?** Raising the cell-type mutation rate twentyfold answers it. Arrival-limited.

**Arrivals stayed flat while the standing population grew fifteenfold** (1 → 15), which a crop
maintained by mutation pressure alone cannot do. But that argument has a confound in the next column
of the same table — nutrient density rose 5.99 → 8.60 J/m³ over exactly that window, so creatures
were living longer, and a longer-lived standing crop draws the same curve as a reproducing one.

**So the claim rests on an instrument rather than on the inference.** An `inherit` column counts
living absorptive creatures whose *parent* was also absorptive, checked against every absorptive id
ever seen rather than against the living — a parent that has already died being exactly the case
that matters, since it means the lineage outlived its founder.

| t | density | absorptive | inherited | food income |
|---|---|---|---|---|
| 10,500 | 6.96 J/m³ | 8 | **1** | 0.13% |
| 11,000 | 8.65 | 10 | **2** | 0.11% |
| 11,250 | 8.00 | 12 | **3** | 0.13% |
| 12,250 | 8.60 | **15** | **4** | **0.42%** |

Better than a quarter of the detritivores were born into the trade, and the share rises with the
larder. The counter under-reports — it is sampled every 250 s, so a creature that lives and dies
between samples is never credited — which makes 27% a floor. **Energy path: sun → photosynthesiser
→ corpse → detritus → consumer, with §5A.2's audit closing at 0.0000% throughout.**

**Nothing was designed to produce this**, which is the entire content of the claim. No fitness
function, no niche assignment, no archive cell, no code naming detritivores. D038 made things die of
age; the corpses raised density past the margin (D039); mutants arrived and bred. That chain is
D017's bet and this is the first time it has paid.

**It is a toehold, not a trophic level:** 15 creatures in 2,478, 0.42% of income.

**The probe stays a probe.** `EVOSIM_CELLTYPE_MUTATION` defaults to §5A.3's rate and is not changed
by this entry. Twentyfold arrivals accelerated a clock; they did not alter the energetics, the cell
parameters, or what makes the trade viable. The same state is reachable at the default rate — one
arrival per 5,128 births is rare, not impossible.

#### What this changes about what to do next

**The run ended on wall clock.** Not extinction, not runaway, not §5A.7's ceiling: ninety minutes at
2.3× real time and falling, stopping at t=12,309 against a 16,000 s target, having produced 8,140
births. At the default mutation rate the same result needs roughly twenty hours.

**The binding constraint stopped being ecological somewhere around t=9,500 and became throughput.**
Every question still open — do joints ever pay, does anything swim on purpose, does shading ever
close the population, is 0.42% food income a toehold or a ceiling — is a question about states this
world reaches later than it can currently be run to. That makes §6.4's throughput work
(`windows-il2cpp`, the island model) the lever that makes every other question cheaper, rather than
one that answers a single one.

⚠ **Single seed.** Seed 3, one configuration. The mechanism is not in doubt — the `inherit` column
is a direct observation, not a statistic — but how reliably a world grows a food chain, and how
large the toehold becomes, are unmeasured.

---

### D041
**Filtering clearance 0.5 → 1.0 — the two trades want opposite bodies** · 2026-08-27

[D039](#d039) proposed raising `AbsorptiveCell.ClearanceRate`, measured photosynthesis and
absorption at 470 s and 500 s to earn their own tissue, called it parity, and withdrew the change.
**That measurement priced both trades on a cube, and a cube is a shape neither trade would build.**

Photosynthesis scales with **lit area**; absorption scales with **volume**. At equal volume
(0.5 m³), before this change:

| body | area/volume | photosynthesis | filtering at 10 J/m³ |
|---|---|---|---|
| flat plate | 4.50 | **4.59 W** | 0.50 W |
| cube | 1.89 | 1.06 W | 0.50 W |

**Filtering is completely indifferent to shape. Photosynthesis is 4.3× better spread out** — and
selection in a lit layer spreads bodies out, so that is the body every real creature has. Every
absorptive creature in this world is a mutant of a photosynthesiser, so conversion cost it **9.2×
its income**, not the 1.06× the cube measurement reported. That is why a lone arrival on a rich
larder survived a thousand seconds, ate, and never bred (D040, logbook/0025): solvent, and many
times poorer than the siblings it was competing against.

**1.0 rather than the 1.3 that would equalise them, and the gap is the design.** Filtering is
depth-independent and photosynthesis is not. A trade that ties at the surface wins everywhere
below it, and the world would swap a photosynthetic monoculture for a detritivore one — which
would look like success for a long time, because food income would finally be large. At 1.0:

| | photosynthesis | filtering |
|---|---|---|
| −2 m, spread out | **4.59 W** | 3.00 W |
| −45 m | −1.33 W | **3.00 W** |
| −2 m, compact | 1.06 W | **3.00 W** |

**Two niches where there was one, and they divide on two axes at once** — by depth, which §5A.4's
gradient always promised and nothing could ever use, and by *shape*, which was not the intent. A
spread body should stay in the light; a compact one does better filtering, at any depth. That is a
morphological trade-off rather than a positional one, and it is the first thing in this world that
gives a creature a reason to be a particular shape.

`TheLitLayerStaysPhotosynthesisAndTheDeepWaterOpensToFiltering` guards both directions, because the
failure modes are symmetric and only one of them looks like a failure.

⚠ **This changes `configHash`.** Every measurement on file — D031's calibration, the §5A.2b sweep,
D038's senescence transition, D040's food chain — was taken at clearance 0.5, and the arms of any
comparison spanning this entry are not comparable. `EVOSIM_CLEARANCE` exists so a run can go back.

⚠ **Unmeasured in the world.** The margins above are arithmetic on a developed body; whether a
population actually splits into a lit canopy and a filtering deep is the run this authorises, not
a result it reports.

---

### D042
**Joints were never affordable — D031 and D032 swept the wrong side of a line at 5 N·m** · 2026-08-28

Nothing in this project has ever evolved a working muscle. [D031](#d031) and [D032](#d032) swept
actuator cost across irradiances from 64 to 400 W/m², found nothing alive with a joint at any
setting, and concluded the failure was not a knob. **It is a knob, and the sweeps ran entirely above
the value that mattered.**

**A link is charged three times, and none of the charges requires it to move.**
`LinkCell.Acquire` returns `CellIntake.None`, so link tissue earns nothing at all. For a real
creature — half-extent 0.35 m, which is what survivors measure — carrying one 20 N·m hinge:

| | |
|---|---|
| income forfeited by non-earning tissue | 1.30 W |
| link tissue's higher base upkeep (2.5 vs 1.0 W/m³) | 0.51 W |
| idle capacity charge, 0.02 × power × dof | 0.40 W |
| **total** | **2.22 W**, against a surplus of **1.92 W** |

**115% of everything the creature earns.** Measured directly: two photosynthetic parts net
**+1.92 W**; one part plus an idle hinge nets **−0.30 W**. Insolvent before actuating once.

**The solvency threshold is 5 N·m, and `MinLinkPower` is 5.** Capacity is drawn uniformly from
[5, 20], so the *minimum possible draw* sits exactly at break-even and everything above it is
insolvent. D031 and D032 swept `MaxLinkPower` at 8, 20, 60 and 120 — all above the line — while
`MinLinkPower` was never touched in any arm.

**And size cannot rescue it, because nothing selects for size.** The fixed charges amortise: at
three parts a 20 N·m hinge is comfortably solvent (+1.63 W). But 2,000 founder draws give a mean of
**1.51 parts**, **0%** at three or more, and **every jointed founder is exactly two parts** — one
photosynthetic, one link. Escaping needs a third part first, and a 3-part jointless creature earns
no more per unit tissue than a 1-part one, so the intermediate step buys nothing. (Development is
not the culprit: **100%** of the genome reaches the body. `MinNodes`/`MaxNodes` of 2–5 belong to
`GenomeFactory.Random`, not `Founder`.)

**So the failure is not behavioural.** Nothing has ever been eliminated for swimming badly; jointed
creatures die of arithmetic before behaviour is tested, and "are joints useless?" has never been an
askable question because an affordable joint has never existed here.

**Structurally identical to [D039](#d039), three days apart.** A trade priced at break-even, where
break-even is not viability because §5A.6 pays for offspring out of surplus. §5A.6d was written two
days ago about absorptive cells and closes with a warning — *"any claim of the form 'X is not viable
because the world only produces Y' is suspect until restated as a margin"* — that applied to joints
the whole time, in the same document.

⚠ **No fix is chosen here, deliberately.** Four different knobs produce that 2.22 W —
`MinLinkPower`, `IdleWattsPerNewtonMetre`, link tissue upkeep, and §5A.1's rule that link tissue
cannot earn — and only the first has been examined. Which one should move is a design decision
about what a muscle *is* in this world, not a calibration.

⚠ **Unestablished: whether an affordable joint is any use.** All of the above is arithmetic on a
developed body — no physics, no thrust, no swimming. A 5 N·m hinge on a 0.34 m³ body may produce
nothing worth having, in which case the fix is elsewhere entirely. That measurement needs a run.

---

### D043
**Muscle may earn, because the term nobody swept is the one that dominates** · 2026-08-28

[D042](#d042) priced a 20 N·m hinge on a survivor-sized two-part creature at 2.22 W against a
surplus of 1.92 W, and closed by refusing to pick which of four knobs should move. This picks one,
and it picks it on arithmetic rather than preference: of that 2.22 W, **1.30 W is neither upkeep
nor idle capacity — it is the photosynthesis the same volume would have done.** The other two terms
together are 0.91 W, less than three quarters of it.

**Every sweep this project has run moved one of the small two.** D031 and D032 swept actuator
power; the two probes of 2026-08-28 moved `MinLinkPower` to 1–4 N·m and `IdleWattsPerNewtonMetre`
to a tenth. The weak probe put a joint genuinely below break-even and **joints still reached 0% by
t=7,500 while the run went on to generation 36.** Cheapness is not enough, and it cannot be, because
§5A.1 makes a link's best possible case *earning nothing* — and nothing loses to a photosynthetic
cell of the same volume earning 0.96 W, at every price. **No setting of the swept knobs can reach a
term they do not contain.**

**Chosen: `LinkCell.PhotosyntheticEfficiency`, defaulting to 0 — §5A.1 unchanged.** What is added is
the ability to ask the question, not an answer to it. At 0 every earlier number stands exactly.
`JointMarginTests` pins the arithmetic: insolvent at 0 (−0.2956 W), solvent at half of green
tissue's capture rate, and still behind two photosynthetic parts at full rate — so the trade-off is
priced rather than abolished. Expressed as a *fraction of* `PhotosyntheticCell.DefaultEfficiency`
rather than as an absolute, after a first attempt that read 20× high because 1.0 meant "all incident
light" instead of "as good as a leaf".

**Why this and not the other three knobs, on biology.** Motility did not begin as inert tissue a
body had to afford. It began in cells that swam and fed at once: *Chlamydomonas* photosynthesises
and swims with one cell, and the choanoflagellates the animals descend from feed with the collar
that drives their flagellum. A flagellum is an organelle on a productive cell, not a segment bolted
to one. **A muscle that earns nothing is a large-animal arrangement, and §5A.1 charges it to the
first thing that ever moved.** The three rejected knobs all make muscle cheaper; only this one makes
it *ancestral*.

**Rejected: bigger founders.** [D042](#d042) shows three parts carries a 20 N·m hinge at +1.63 W, and
raising `Founder`'s body count is a two-line change. It is an engineering workaround with no
biological claim behind it — evolution made muscle affordable by fusing it with metabolism, not by
growing large first, and growing large first is the thing that could not happen because the
intermediate buys nothing.

**Rejected for now: negative buoyancy.** The strongest real mechanism — most cells are denser than
seawater and flagella in phytoplankton are largely anti-sinking machinery, so immobility carries a
continuous cost. It contradicts §5.2, which disables gravity to obtain neutral buoyancy on Sims'
own wording, and [D036](#d036) rejected the neighbouring "make creatures faster" for building a
different world rather than a calibrated one. Worth a probe, after this one.

⚠ **Still unestablished, and unchanged from [D042](#d042): whether an affordable joint is any use.**
This makes one affordable. Whether a hinge on a 0.34 m³ body produces thrust worth having is a
measurement that needs a run, and two are in flight — `linkearn` at 0.5, and `daynight` at
`DayNightAmplitude` 1 testing [D035](#d035)'s cycle in the world [D037](#d037) finally gave it.

**Amended the same day, by the arm it authorised.** `linkearn` ran at 0.5 and **link tissue was
gone from the population by t=3,000** — not the joints only, the cell type. The pricing says why,
and it is the error this project keeps making:

```
two photosynthetic parts, no joint :  1.9239 W
one part + 20 N.m hinge, photo 0.50:  0.6999 W   solvent — and 36% of the plant
one part + 20 N.m hinge, photo 1.00:  1.6954 W   solvent — and 88% of the plant
```

**Solvency was never the bar; out-reproducing is.** §5A.6 pays for offspring out of surplus, so a
creature banking at 36% of its neighbours' rate is outbred and gone regardless of being comfortably
alive. That is [D039](#d039) and [D042](#d042) again, and `linkearn` is the **fourth** arm in a row
built to reach break-even when the requirement was competitiveness.

**And the ceiling is structural.** At a fraction of 1.0 — muscle as good at light as a leaf, the
literal *Chlamydomonas* case — a jointed creature still reaches only 88% of a plant, because the
idle capacity charge does not scale away. **No setting of this knob alone can make a joint
competitive**, and the test written to guard the change asserts exactly that domination. The arm's
own guard guaranteed it could not succeed.

What this establishes is a *requirement*, which is worth more than the arm was: **movement must be
worth more than 12% of a creature's income**, since that is what the best possible muscle still
gives away. It is currently worth about 7% (≈0.13 W against 1.92 W) and realised at 0%. So the cost
side is now closed — it is bounded at 88% and cannot reach — and every remaining option is on the
prize side.

⚠ [D035](#d035)'s cycle alone is also closed: `daynight` at amplitude 1 kept `linkPhoto` at 0, so its
joints were fully insolvent at −0.30 W and died by t=5,000 having never been affordable. Affordability
and a reason to move are each useless without the other, and were run as separate arms when they
had to be one. `linkday` — fraction 1.0 with amplitude 1 — is that arm, and is the first test in
this project's history in which a joint is both affordable and has something to buy.

---

### D044
**Tissue is denser than water, because staying still has to cost something** · 2026-08-28

[D043](#d043) closed the cost side: the best possible muscle reaches 88% of a plant and cannot do
better, so a joint must *earn* its keep and the only thing it can buy is position. This is the
mechanism that makes position worth buying.

**First, the measurement that should have preceded all of it.** `SwimSurvey` had measured signed
vertical displacement since logbook/0018 and nobody had read it as a thrust curve. Founder-shaped
bodies, best of 200, 20 s, no gravity, no contact:

| joint capacity | sustained vertical |
|---|---|
| 5 N·m | 0.008 m/s |
| **20 N·m** | **0.017 m/s** |
| 60 N·m | 0.029 m/s |
| 120 N·m | 0.052 m/s |

**A hinge on a two-part body produces real directed thrust.** The question open since
[D042](#d042) — *is an affordable joint any use?* — is answered yes. Every failure recorded in
D042 and D043 was an affordability failure and none was ever about swimming. The median is 0.0000 m
at every capacity, which is correct for unselected random brains and makes this a bound on what
selection could find, from below.

**Chosen: `FluidConfig.TissueExcessDensity`, kg/m³ over the water, defaulting to 0.** At 0 this is
§5.2 exactly — the same survey returns byte-identical numbers — so nothing measured before today
moves. Above 0 a creature sinks out of the light unless it swims, and *"doing nothing"* stops being
free. §5A.6 already called per-part density "a tempting knob — swim bladders are cheap and
biological — but a second system, deferred": one global constant is not that second system, and
evolvable per-part density stays deferred.

**An excess density, not a sink rate**, so the physics decides the rate: a body sinks until §5.2's
drag balances its excess weight, and a flat body that collects more light also sinks more slowly.
Shape therefore pays twice, through one mechanism, without anyone arranging it.

**Calibrated against the observable, because the arithmetic was wrong by 10×.** Predicting the sink
rate from quadratic drag gave 0.011 kg/m³ for 0.01 m/s. Measured, with the median brain — which
cannot swim, so its net displacement *is* the terminal sink:

| excess density | sink rate |
|---|---|
| 0.001 kg/m³ | 0.0001 m/s |
| 0.01 kg/m³ | 0.00097 m/s |
| 0.1 kg/m³ | 0.0089 m/s |

**Linear, not square-root** — at hundredths of a metre per second these bodies are not in the
quadratic regime the estimate assumed. Real densities do not transfer either: diatoms run 5–75 kg/m³
over seawater, but they are microscopic and in Stokes flow while these are tenths of a metre.

**0.15 kg/m³ for the first arm, and the margin is the point.** Averaging income over the descent
rather than taking its endpoint, a non-swimmer at the resulting ~0.013 m/s loses **~30%** of lifetime
income against a muscle costing **12%** — a ratio of 2.5. At 0.1 kg/m³ it would have been 1.75, and
**every arm this project has run at a ratio near 1 has failed**: D031, D032, the two joint probes,
and `linkearn`. The requirement is not to clear the bar but to clear it visibly. The sink also stays
under the 0.017 m/s a founder can already swim, so holding station is achievable rather than
notional, and a partial swimmer sinks proportionately more slowly — [D036](#d036)'s "continuous
returns from arbitrarily close to zero", finally available to the photosynthetic majority instead of
to a 0.01% minority.

⚠ **Non-zero values invalidate §11.2's momentum check by construction**, which asserts that nothing
external acts on a creature. That is an exemption, not an oversight: the check runs at 0, which is
the default and what every harness uses. The metabolic audit is untouched — sinking moves a creature
without moving a joule.

**Amended the same day: 0.15 kg/m³ drowned the world, and the reason was never buoyancy.** `sink`
ran its full 40,000 s and ended **alive=40, generation 0** — every living creature a floor spawn,
nothing ever bred, mean lifetime expenditure 241 J against 87 J of income. Work share stayed between
0.2% and 5.8% throughout, so station-keeping was never the expense.

The missing measurement, taken afterwards and belonging before every arm of the day:

| depth | irradiance | net W |
|---|---|---|
| 0 m | 64.00 | +2.6460 |
| −8 m | 32.86 | +0.3571 |
| **−10 m** | **27.81** | **−0.0136** |
| −20 m | 12.09 | −1.1695 |

**The habitable band is eight metres.** The sink rate above was chosen against what a joint can push
(0.017 m/s) and never against where a creature can live; it carried the population to −19 m, more
than twice the band, and a world with no solvent depth reports exactly what a world with no reason
to swim reports.

Three consequences that outlive this arm:

- **Surface sorting was never a preference.** Mean height of +0.7 to +5.4 m across every run is the
  top of an 8 m band.
- **`FounderDepthSpread` is 20 m**, so a large fraction of founders in every run ever performed are
  born below −10 m and cannot survive at any depth they were placed.
- **Depth as a selectable axis is capped by irradiance.** Band depth is `12·ln(I_surface / 27.8)`:
  8 m at 64 W/m², 24 m at 200. Eight metres is 0.67 attenuation lengths, so income varies by at most
  51% across the whole livable world and a descent worth 27% would consume most of it.

**Irradiance and buoyancy have room together that neither has alone**, and it is not a coincidence:
§5A.2b holds irradiance at 64 because light covering upkeep completely is what makes a runaway, and
a sink is a cost that restores the pressure a brighter world removes. `sink-lit` tests the pair —
200 W/m² for a 24 m band, 0.13 kg/m³ for an 8 m lifetime descent, giving a 27% prize against the
muscle's 12% at a ratio of 2.25, with the required 0.0116 m/s inside the 0.0168 m/s a founder can
already swim. ⚠ A runaway at 200 W/m² is a possible and informative outcome ([D021](#d021)), not a
failure.

**Second amendment: the coupling is confirmed, and the density window is what is being searched.**
Three arms at 200 W/m² differing only in `TissueExcessDensity`:

| arm | density | outcome |
|---|---|---|
| `lit` | 0 | **RUNAWAY at t=938 s, 5,008 alive** |
| `sink-lit` | 0.13 | pinned at the population floor, 40–113 alive, search-limited |
| `sink-slow` | 0.02 | 217 alive at t=1,000 and growing |

**A bright world alone is untenable and a sink is what makes it affordable.** §5A.2b holds
irradiance at 64 because light covering upkeep completely is the runaway condition; `lit` reproduces
that in 938 seconds at 200. The same world with a sink does not run away — buoyancy restores exactly
the pressure brightness removes. That was the argument for pairing them and this is the measurement.

⚠ What remains unknown is whether the window between "runs away" and "cannot sustain a population"
contains anything, and whether a joint is selected inside it. `sink-mid` at 0.05 brackets it.

**And a consequence of the band that outlives all of this.** `World.SpawnFounders` places founders
at `-rng.Range(0, FounderDepthSpread)` — uniform over 20 m — with a comment describing it as "the
lit zone". The lit zone is 9.75 m at 64 W/m². **Fifty-one per cent of every floor spawn is born
below break-even**, in every run this project has performed, which halves the mutational supply
exactly where the open questions turn on whether a rare variant can be found at all. Not changed
here: [D036](#d036)'s objection to shrinking the spread is untouched by this, and the fix is to
derive it from the light model rather than pick a smaller constant.
`MostFoundersAreBornSomewhereTheyCanLive` is the invariant that would have caught it.

### D045
**A mutation-born joint draws from the same bounds a founder does** · 2026-08-28

[D032](#d032) lowered the joint capacity ceiling to 20 N·m after logbook/0017 measured what
capacity costs to own, and `RandomGenomeOptions.MaxLinkPower` has said 20 ever since. The
mutation operator was never told. `Mutator.ChangeJointType` drew `rng.Range(5f, 120f)` — the
retired ceiling, as a literal — and nothing pointed at the option it was supposed to honour.

**It fires on exactly one branch, and it is the branch that matters:**

```csharp
if (dof > 0 && node.Power <= 0f) node.Power = rng.Range(5f, 120f);
```

`node.Power <= 0f` means the node had no joint. So this is not a general capacity draw — it is
the single code path by which a lineage that has no muscle invents one. Founders were never
affected; their descendants inherit a founder-drawn capacity and perturb it from there. What was
overcharged was every *de novo* muscle, which is the event this project has been trying to
observe since [D042](#d042).

| | draw | mean | idle at `LinkCell` 0.02 W/N·m | against a leaf's ~2.3 W income |
|---|---|---|---|---|
| founder | 5–20 N·m | 12.5 | 0.25 W per DOF | 11% |
| **mutant (before)** | **5–120 N·m** | **62.5** | **1.25 W per DOF** | **54%** |

D032 put the affordable region "well under a fifth of income". A founder-drawn joint sits inside
it at 11%; a mutation-drawn one sat at 54%, and 1.25 W is within a percent of the 1.24 W median
link that logbook/0017 measured nothing surviving at any irradiance from 64 to 400 W/m². A
spherical joint made it three degrees of freedom: **3.75 W standing, before the joint moved
once.**

[D044](#d044)'s thrust table is the other half of the joke. It measures 120 N·m at 0.052 m/s —
the best swimming in the survey. That is what a mutation-born muscle was buying, and it was
paying five times a founder's rate for it.

**Chosen: `Mutate` takes `RandomGenomeOptions` and `ChangeJointType` draws from
`MinLinkPower`..`MaxLinkPower`.** One source of truth rather than a second copy, because two
constants that must agree is the arrangement that produced this. `World` forwards `Config.Genome`,
so a run's configured ceiling reaches the operator — `TheRunsConfiguredPowerCeilingReachesTheMutator`
asserts that specifically, since a bound that never arrives is the same fault wearing a
different hat.

`Mutator.CodeVersion` goes to 2. The class contract is that an offspring is determined by its
inputs, and this changes what a stored seed reproduces; old chains would rebuild into creatures
that are plausible, valid, and not the ones that lived.

**A second copy of the same stale constant, found while writing this up.** `EvolutionRun` reads
`EVOSIM_MAXPOWER` and assigns it to `config.Genome.MaxLinkPower` — defaulting to `120f`, the same
retired ceiling, as its own literal. So the effective bound depended on whether a run happened to
name the knob:

| run | founders | mutation-born |
|---|---|---|
| ceiling not set (runner default 120) | 5–120 | 5–120 |
| **ceiling set to 20** (`sink-mid`, `sink-slow`, `sink-still`, `linkearn`) | **5–20** | **5–120** |

The bug was therefore invisible in exactly the runs that did not care, and bit only the ones
**deliberately lowering the ceiling to find out whether an affordable joint could survive.** Every
affordability experiment this project has run was overcharging de-novo muscles by six times on the
one path that creates them. Both defaults now come from `RandomGenomeOptions.Default` rather than
from literals.

**What this does not claim.** It does not explain the joint-share decay in the runs to date —
those populations are dominated by founders and their children, who were correctly priced, and
[D043](#d043)'s arithmetic that the cost side is structurally closed stands untouched. What it
explains is narrower and more useful: why no lineage ever *invented* a muscle and kept it. The
prediction is that de-novo joint lineages become more frequent, not that jointed creatures start
winning — the two are different claims and only the first is tested here.

### D046
**A link that photosynthesises pays green tissue's upkeep** · 2026-08-28

[D043](#d043) added `LinkCell.PhotosyntheticEfficiency` and concluded from it that the cost side
is structurally closed: *"even at `linkPhoto = 1.0` a jointed creature reaches only 88% of a
plant"*. Both halves of that were wrong, in opposite directions, and they were hiding each other.

**The 88% was measured at `Power = 20f`** — `MaxLinkPower`, the most expensive joint a founder can
draw — in a test that hardcoded it. `MorphNode.Power` is evolvable per node down to
`MinLinkPower`, and `LinkCell` bills in proportion to it, so 88% is the worst case rather than
the ceiling. The question that matters is what the *cheapest* joint costs, because a lineage that
lowers its capacity is walking down that curve.

**Walking down it went past parity.** A link earning at a photosynthetic cell's efficiency was
paying a link's upkeep — 2.5 W/m³ against green tissue's 3 — so at full efficiency it took a
plant's income for half a watt per cubic metre less:

| capacity | before | after |
|---|---|---|
| 5 N·m | **103.7%** of a plant | 94.8% |
| 20 N·m | 88.1% | 79.2% |
| 120 N·m | −15.8% | −24.7% |

At 5 N·m the knob had made a joint **pay you to carry it**. D043 intended to price a trade-off and
had abolished it, which is the failure mode §5A exists to avoid: a muscle that spreads because it
is free tells you nothing about whether muscles are worth having.

**Chosen: `LinkCell.Upkeep` charges a surcharge, proportional to its share of
`PhotosyntheticCell.DefaultEfficiency`, that brings its rate to
`PhotosyntheticCell.DefaultUpkeepWattsPerCubicMetre` at full efficiency.** The capacity term is
then the only thing separating a link from a plant, which is what D043 meant to be measuring.

Derived from `PhotosyntheticCell` rather than taken as a parameter: it is not an independent
choice — it is whatever green tissue costs — and a second copy is exactly how
[D045](#d045)'s two ceilings drifted apart.

Being derived meant the config hash could not see it, and the first relaunch proved it: three arms
came back carrying **byte-identical hashes to the runs the fix had just invalidated**. A hash whose
job is to detect exactly that mismatch and cannot is worse than no hash, so
`PhotosyntheticCell.DefaultUpkeepWattsPerCubicMetre` is now in `LinkCell.HashContribution`.
`TheCeilingOnAnEarningMuscleIsSetByCapacity` pins the curve at both ends — it must move with capacity, and it must never reach parity.

**What this changes about the muscle question.** The honest number is that an earning muscle at
minimum capacity sits **5.2% below a plant**, and that 5.2% is precisely the idle charge on 5 N·m.
That is a far narrower gap than D043 reported and a very different claim: not "no muscle can ever
compete" but "a muscle competes if it is cheap, earns, and is worth the last five per cent". D043's
conclusion is **superseded** — the cost side is not structurally closed. What remains true is the
part that was never about cost: a muscle still has to buy something, and depth is still the only
thing it can buy ([D037](#d037)).

### D047
**The alphabet stays flat; what was missing is the floor's report** · 2026-08-28

A day spent asking why a muscle never evolves ended in the observation that our six cell types sit
at wildly different points on life's timeline and are all available at t=0: photosynthesis (~3.5
Ga), detritivory (~2 Ga), predation (~0.8 Ga), and nervous systems and muscle (~0.6 Ga, Cambrian).
`FounderTailChance` is 0.5, so **half of all founders are born with a Cambrian organ** in a world
that is otherwise Archean — no predators to flee, no prey to chase, and under [D037](#d037) no
spatial structure to exploit.

**Rejected: staging the cell-type registry by era**, whether gated on time or on ecological
precondition. The objection that killed it is that natural selection already does this, only more
wastefully — and the day's data agrees. In `sink-mid` the jointed share was driven below 5% **five
separate times** from cohorts starting near 45%. Selection never failed once.

Two further reasons:

- **What gating approximates is dependency, and we have not modelled it.** Muscle proteins descend
  from cytoskeletal ones; nervous systems from ion channels used for osmoregulation. Later
  innovations are *built out of* earlier ones, which is the actual reason they cannot appear first.
  Our six cell types are independent atoms with no ancestry between them. A gate would be a crude
  stand-in for a structure the model lacks, and recording the gap is more honest than faking it.
- It is a second exogenous intervention in a system [D017](#d017) exists to keep endogenous.

**Also rejected: simplifying the founder draw** to photosynthetic blobs, requiring everything else
to be evolved. [D021](#d021) and `FounderCellTypes` already answer this: the doomed absorptive and
consumer founders of generation zero *are* the primordial soup — they starve, and their tissue is
the first nutrient anything ever has. And handing generation zero to photosynthesis would make
"plants came first" an arrangement rather than a finding. Both arguments hold.

**What is real is that D021's report was never built.** Its success condition is that the floor
"fires at t=0 and never again", and its failure mode "looks like success". The reporting it
specifies lives in `lineage.jsonl`, which is deliberately unwritten — so the guard existed as a
design and as two unread properties on `World`. `gen min` cannot substitute: `gen min = 0` reads
identically for a healthy young world and one the floor is holding up.

**Chosen: `floorSpawns`, `floorSpawnsWindow` and `secondsSinceFloorFired` in `stats.jsonl`, and a
`floor` column beside `gen min`.** The default 48 W/m² world takes 107 floor spawns against 6
births in its first 600 s. See
[logbook/0032](../logbook/0032-the-instrument-that-was-designed-and-never-built.md) for the
re-audit; the short version is that the clearance arms were largely self-sustaining and the sink
arms essentially never were.

---

### D048
**Producers must consume something — nutrient is matter, light is energy** · 2026-08-28

`PhotosyntheticCell.Acquire` returns `CellIntake.Light(...)` and draws no pool. `AbsorptiveCell`
draws from the nutrient field. **So consumers deplete and producers do not**, and the only thing a
producer emits is shade, which harms creatures below it and never itself.

The consequence is that nothing a creature does makes its own position worse. There is no negative
feedback anywhere on occupying the best spot, so every arm sorts to the surface and stays, and the
depth axis is a ramp with its maximum at the boundary rather than a landscape with an interior
optimum. That absence has been mistaken for several other things today, including the conclusion
that a muscle has nothing worth buying.

The real ocean's vertical structure *is* this feedback: light at the top, nutrients at the bottom,
and the surface is nutrient-poor **because producers live there and strip it**. Corpses sink,
remineralise at depth, and the deep stays rich and dark. Two opposed gradients, one of them created
by the organisms themselves — and the reason gas vesicles exist ([D049](#d049)).

**Chosen:**

- Light stays energy; photosynthetic income is unchanged.
- A **separate nutrient field** carries matter. Not the detritus pool: reusing it is cheaper and
  would make producers and decomposers compete directly, which is ecologically right, but it
  conflates joules with matter in §5A.2's audit, and an audit that cannot tell energy from matter
  is the failure mode this project keeps rediscovering.
- **Reproduction requires nutrient as well as energy**, drawn locally. Biologically exact — no
  amount of sunlight builds a daughter cell without nitrogen and phosphorus — and it fits §5A.6,
  where growth does not exist and tissue is only ever created at reproduction.
- **Death returns the matter** to the field, where it sinks. The loop closes and nothing leaks.

**Rejected: an imposed photodamage cost at high irradiance.** It would also produce an interior
optimum, and it is real Archean biology (no ozone). But it manufactures the optimum by fiat where
this produces one endogenously, and a moving one — the band shifts as the population shifts.

Predicted, and the thing to check the implementation against: a **deep chlorophyll maximum**, a
standing layer of peak productivity at intermediate depth, and blooms that crash by exhausting
their own resource rather than by the crowding artefact seen so far.

---

### D049
**A buoyancy cell, passive before controlled** · 2026-08-28

Gas vesicles are the earliest mechanism life evolved for exactly this world's problem —
positioning in a light gradient in a water column — and predate muscle by roughly three billion
years. This world has been asking how a creature controls its depth while omitting the organ that
does it, and answering with a joint, which is a Cambrian solution to an Archean problem.

**Chosen: a `BuoyancyCell` with a genome-encoded, evolvable lift, priced like `LinkCell`'s
capacity.** The machinery is largely present: [D044](#d044)'s `FluidConfig.TissueExcessDensity`
already applies a buoyancy force per body in `FluidEnvironment`, and the change is to make it
per-part and cell-determined.

**Passive first, controlled second.** Step one is a fixed evolvable lift per cell: a lineage evolves
to sit at a depth. Step two adds brain-driven modulation as a new effector channel — brains already
run (`Brain.Step` → `Driver.Drive` in `Ecosystem.cs`), so this is a channel rather than a subsystem.
Splitting them isolates *can creatures find their depth* from *does regulating help*.

**Sequenced after [D048](#d048), deliberately.** Buoyancy in a world whose optimum is at the surface
collapses to "everyone floats". Real cyanobacteria regulate buoyancy to **hold a band**; the band
has to exist first.

### D050
**Lift is a multiple of the sink it cancels, and the ocean has a top** · 2026-08-28

[D049](#d049) shipped and was measured, and the measurement said the organ was a rocket:
buoyant creatures 100–178 m above the rest of the population in a world whose habitable band is
23.7 m deep, holding a share that fell from 25% to 4% while they climbed (logbook/0034). Two
faults, independent of each other and of D049's reasoning, which stands.

**Lift was denominated in absolute kg/m³ and the thing it opposes was not.** `TissueExcessDensity`
was 0.02 kg/m³ in every one of those runs; the founder lift range was 0.5–5 and the bound 50 — so
25× to 2,500× the weight there was to cancel. The weakest bladder a creature could be born with was
already a runaway, and **neutral buoyancy was not a reachable genome value**, which is the one
thing gas vesicles are for.

**Chosen: `MorphNode.Lift` is a multiple of `TissueExcessDensity`.** 1 is neutral, 2 rises as fast
as a bare body sinks; `FluidEnvironment` computes `excessDensity × (1 − lift)`. The founder range
became 0.25–2 and the bound 3. The alternative — keep absolute units and fix the numbers — was
rejected because §5.2 flags `TissueExcessDensity` as unmeasured and expects to retune it, and
absolute lift fails that silently: every sweep of the sink would rescale what the genome means
without changing a line of it. This couples a genome field's force to a world constant, which is
the cost, and is the same coupling a real fish has to the water it is in.

**Chosen: no upward net force at or above y = 0.** Above the waterline `LightModel.IrradianceAt`
returns a constant and `NutrientField.LayerOf` clamps to layer 0, while the physics keeps
integrating — so the world was an unbounded ray on which every point is identical to floating at
the surface, and lift bought a hundred metres of nothing at a standing cost. §5A.1 anticipated
that "free lift runs away to whatever ceiling exists"; the ceiling was what was missing. A real
body stops rising as it emerges because the water it displaces runs out, and this is that at its
coarsest. Sinking across the line is untouched.

**`GenomeJson.FormatVersion` 2 → 3**, because the units of a stored field changed and §9's rule is
that loading refuses rather than reinterprets. **`MaxLiftSinkMultiples` joins
`BuoyancyCell.HashContribution`**: it is a `const` and not a `[Tunable]`, so `RunConfigTests`
cannot see it, but `Mutator` clamps to it and it therefore bounds which genomes exist — exactly
[D046](#d046)'s bug, where a derived value decided behaviour and was absent from the hash meant to
notice.

**Measured.** Same worlds, same seed, new units: the runaway is gone, `flt m` never rises above
−3.7 m, selection moves lift from a founder mean of 1.15 to 1.6–1.8 — just above neutral — and
52 of 56 buoyant creatures inherited the trait with zero floor spawns after t=400. At sink 0.1
`d050-heavy` crashed to 63 and recovered to 940 with buoyancy at 47% through the bottleneck; at
sink 0.02 the organ is nearly inert, which is correct, because there is almost nothing to cancel.

⚠ **The surface clamp never fired** in either arm, so the rescaling is what fixed the runaway and
the clamp is so far only a guard. It is still the right thing to have: a brain-driven lift channel
([D049](#d049) step two) is precisely what could push a creature up again.

⚠ **What it does not establish.** Buoyancy stayed a 6% minority in worlds whose absorptive cells
had gone extinct by t=1300, leaving detritus piling up unconsumed and half the population sunk on
the sea bed as producers in the dark. Passive lift lets a lineage choose a depth; the world D048
built has light at the top and matter at the bottom and rewards being in *both*, which is a case
for D049's second step rather than against its first. Re-run in D048's world (`mixing 2`, `current 0.05`,
`senescence 3000`) the buoyancy result sharpens into a dose-response — extinct at sink 0.02,
15% of the population and lift 2.06 at sink 0.1, the latter *above* the range a founder can be
born into — while the trophic failure survives unchanged: `absorpt` reaches 0 there too. No world
measured today held a food chain, and that is not a buoyancy problem (logbook/0034).

---

### D051
**The floor gives back — remineralisation closes the nutrient cycle** · 2026-08-29

Every run in the D048 world ends the same way: the water where the population lives thins to
0.07–1.9 J/m³ against a break-even of 4 J/m³ (`AbsorptiveCell`: 4 W/m³ upkeep over a
clearance of 1) and an observed establishment threshold around 14 (logbook/0028), the
absorptive lineage starves before t≈1,200, and the food chain dies from the bottom
(logbook/0033, 0034). The floor is not a separate bank in code — it is the last layer of the
same `NutrientField` creatures eat from — but `Settle` only ever pays *into* it, nothing
decays *out* of it, and the only debit path is a creature physically resident in the bottom
layer. A pool with an inflow and no outflow is a ratchet.

**How much the ratchet matters depends on the mixing, and only two mixings have ever been
run.** Across every arm in `runs/`, `mixing 0` worlds end with 66–76% of all detritus on the
floor and `mixing 2` worlds with 0.2–7%: at 2 m²/s the column is uniform to the bottom and the
floor is nothing, at 0 the floor is everything and nothing above it is fed. Neither regime is
the ocean, whose deep water is rich *because* remineralisation at the sediment feeds a
gradient that weak mixing carries upward. The regime in between — sinking winning in the
column, mixing lifting what the floor returns over a scale of D/v metres — has never been
measured here, and it is the one this decision's mechanism acts in.

The literature says this is not a tuning problem but a structural one. Goyal et al. 2023:
a materially closed ecosystem extracts energy *only* through balanced cycles — every
transformation needs a return path, and locally greedy populations then self-organize to
~100× the energy extraction of an uncycled community [GOY23]. The real ocean's version is
benthic remineralisation: bacteria mineralise sediment organic matter and the dissolved
nutrients re-enter the water column at the sediment–water interface, carried up by mixing.
This world built the sink (D036), the mixing (D037), and the deposit (D023/D048) — and
omitted the return leg.

**Chosen: a first-order leak from the floor layer into the layer above it, per field.**
`NutrientField.Remineralise(seconds, ratePerSecond)` moves ~~`min(1, rate·dt)`~~ *(as
built after review, 2026-08-31 correction of this text: the exact first-order fraction
`1 − exp(−rate·dt)`, step-size independent, with a composition test)* of the last
layer's stock up one layer; mixing does the rest of the transport, exactly as it does for
every other gradient. Two knobs, `NutrientRemineralisationPerSecond` and
`MatterRemineralisationPerSecond` (both `[Tunable]`, both default 0 — the world is
bit-identical until a run asks otherwise). It runs in `World.Step` between `Settle` and
`Mix`, and it is an internal transfer within a stock the audit already sums whole, so §5A.2's
energy audit and D048's matter-drift test close without a new term.

**The rate is a rate constant (s⁻¹), not a velocity.** Sinking is transport through water and
is rightly m/s; the floor is a stock being decayed, not a distance being crossed, and
first-order decay of sediment organic matter is the standard model. A velocity denominated
knob would imply a layer thickness below the floor that does not exist.

**Only the floor remineralises, and that is not an approximation.** This model deliberately
conflates particulate detritus and dissolved nutrient into one absorbable pool (D023);
"remineralisation" of a water-column layer would move joules from a pool to itself. The floor
is the one place where the conflation breaks down, because `Settle` traps stock there — so
the leak is defined exactly where it is needed and nowhere else.

**Rejected: a separate dissolved pool that does not sink** (the viral-shunt analog). Two more
fields, a second audit surface, and it un-conflates particulate from dissolved at exactly one
seam while every other mechanism keeps the conflation — the inconsistency would cost more
than the realism buys. Revisit if the single-pool model itself is superseded.

**Rejected: lowering the sink rate instead.** It slows the ratchet without removing it, it
retunes transport everywhere in the column at once, and D050's buoyancy dose–response is
denominated in the sink — a knob three decisions now depend on for three different things is
a knob to stop turning.

**Rejected: column-wide redistribution** (leak the floor into all layers pro rata, an
upwelling shortcut). It moves matter with no transport mechanism the design owns; mixing
exists and is that mechanism. If mixing proves too slow to matter, that is a finding about
the D036 balance, not a licence to teleport.

Predicted, and the acceptance experiment for the food-chain goal will check (pre-registered
in logbook/0036 before launch): in the D048+D050 reference world at `mixing 0.2` — chosen so
the D/v scale is 10 m, a gradient rather than a line or a uniform soup — with the leak at
0.01 s⁻¹, `% on floor` plateaus near the 17% the steady-state arithmetic gives instead of
rising past 60%; `det deep` (a new column, density 6 m above the floor) climbs past the
4 J/m³ break-even by t≈5,000 s while the control stays below 2; and `absorptiveInherited`
stays above zero with the population floor silent. If the density arrives and the chain
still does not, that is evidence the missing ingredient is spatial — a mutant has to arise
*where* the food is — rather than recycling; the two-sided reading is the point of running
it.

**Measured, same day, and the premise was wrong wherever mixing is on.** `NutrientField.Mix`
already runs Fick's law across the floor interface — its loop covers every
`layer < LayerCount − 1` — so at mixing 0.2 the floor exchanges 20% of its excess per second
with the water above it and a 1%/s leak on top is noise: control and treatment differ by 0.2
points of floor share and 0.04 J/m³ of deep density at the same seed (logbook/0036). The
sentence *"nothing decays out of it"* is true at mixing exactly 0 and at no other setting.
The mechanism is kept — it is small, tested, and off by default — but it is **redundant with
D036's mixing in every world the design has asked for**, and its only distinguishable regime
(mixing ≲ 0.01 m²/s) is one no decision wants. The unexpected result is the useful one: at
mixing 0.2 the deep water crosses the absorptive break-even by t≈2,300–4,000 in every seed,
which no earlier world did — so the world for the food-chain question is *that* one, and
whether a lineage establishes in it is what 0036's remaining predictions score.
`STATUS: built · redundant except at mixing 0`.

**Scored, end of day.** Six arms at mixing 0.2: four grew an inherited absorptive lineage,
three of them from the world's own cell-type mutants with the population floor silent, the
largest 1,092 strong and still rising at t=20,000. Every lineage that bred boomed within
1,500 s and drew the deep water from 22–38 J/m³ to 3–13; the two that had time to finish
the arc busted. The world holds a food chain and has not yet been shown to keep one, and its
producers still lean on the population floor during D048's matter crashes (logbook/0036).
The next round closes the floor (`FloorClosesAfterSeconds`, D021's "never again" enforced
rather than measured) and runs 40,000 s — logbook/0037.

---

### D052
**Living bodies give matter back — the excretion contract** · 2026-08-29

Eleven extinctions across logbook/0037–0039 read the same way: a bloom of producers locks the
surface's matter into bodies, the surface falls to ~0.01/m³, conceptions are refused, births
stop, and then the survivors either sink out of the light (excess density 0.05–0.1) or age
out together before the matter returns (0.02). The drought is the common cause, and the
drought lasts longer than a lifetime because **matter leaves a body only at death** (D048),
and a body dies at depth after sinking. Nothing a living creature takes at the surface ever
comes back to the surface.

That is harsher than any real ocean. The sunlit sea *is* a nutrient desert — the design was
right to make it one — but most of what its producers take is regenerated in place within
days by excretion, grazing and leakage, and only a minority sinks out. This world has no
regeneration while alive at all. The owner's question — "where does matter come from?" —
had the answer "from the seed at t=0, and then only from corpses", and that is the gap.

**Chosen: living creatures return matter continuously, at their own depth, in proportion to
their metabolism.** A fraction `ExcretionPerJoule` of the matter locked in a body is returned
to the body's current layer per joule of energy the body spends on upkeep — turnover, the
microbial loop at its simplest. Death returns whatever is still locked. The body's locked
matter therefore falls over its life; **the child's price is unchanged** (`MatterPerTissueJoule`
per joule of tissue), so a parent that has excreted much of its own matter has not thereby
made children cheaper — the price is paid from the layer, as now. `MatterInBodies` is
debited by exactly what the field is credited, so `World.StandingMatter` does not drift and
D048's test passes unchanged. One knob, default 0, bit-identical world; `EVOSIM_EXCRETION` in
the header.

**Rejected for now: a matter source paid in energy** (the owner's first formulation —
"a creature that consumes energy excretes a percent of it as matter"). It has a real
analogue, nitrogen fixation, where microbes spend energy to make inert N₂ usable, and it is
the honest meaning of "life changed the composition of the sea". But it makes matter out of
energy, which breaks the one property D048 built the field on and removes the limit
entirely — a world where every joule mints nutrient has no drought and no reason for the
deep to be worth anything — and a source needs a sink (burial, denitrification) or the pool
grows without bound. That is a larger world than the first self-sustaining one needs.
Revisit as a second knob once the closed cycle has been shown to hold or shown not to.

**Rejected for now: several kinds of matter.** The literature round's [CO02] says more
depletable resources sustain more coexisting strategies, which is a diversity result for
after the world stands up, not a way to make it stand.

**Sequenced:** after round 4 (logbook/0039) is scored and before fix 3 (damping on the
consumer), because it addresses the producers' failure that every round has ended on, and
the consumers cannot be watched for long enough to score in a world whose producers die.
Pre-registration in the 0036–0039 style before any arm; the world is round 4's.

⚠ Project inference from general marine ecology; the review has not searched nutrient
regeneration, and a primary source on new versus regenerated production should be added
before the numbers here are leaned on.

*Built 2026-08-30* — `RunConfig.ExcretionPerJoule`, `Organism.LockedMatter` (set to the
conception price at birth, drawn down by excretion, paid out at death; zero for a floor
founder, which never paid — replacing the old `ParentId` exemption with arithmetic),
`EVOSIM_EXCRETION` in the header. Knob-off is bit-identical: tissue never changes between
birth and death, so the remaining `LockedMatter` equals the old death payout exactly. Four
tests in the D051 shape; suite 348. Found in passing, pre-existing and untouched: a
stillborn conception (`Admit` returns null) leaves its `matterPrice` credited to
`MatterInBodies` with no organism to ever pay it back — conservation holds but the matter is
orphaned in a phantom pool. Unmeasured until round 6 runs.

*Corrected 2026-08-31, prompted by an independent review:* the orphan is **vacuous as the
code stands** — `Admit` refuses only a zero-part phenotype, whose tissue and hence matter
price are exactly zero, so nothing is ever orphaned today. What stands is the *shape* of
the debt: `Conceive` debits before admission, so any future rejection reason would orphan
for real. The guard remains as queued — a test forcing admission failure and proving field
matter, `MatterInBodies`, parent energy and `EnergyOut` all close. Also named by the same
review, a modelling abstraction this entry should have stated: a living body can excrete
its entire `LockedMatter` while its tissue is unchanged, so at k = 0.001 an old body has
fully drained and returns almost nothing at death — the currency behaves as *recyclable
nutrient content carried by tissue*, not as the matter the tissue is built of. That is the
microbial-loop regime this contract chose, stated here plainly; a non-excretable
structural fraction is the alternative if the abstraction ever misleads.

### D053
**Right-sizing the dish — the equilibrium must sit inside the instrument** · 2026-08-30

Round 4 (logbook/0039) removed the last thing that killed producers, and both seeds then
grew until the run ended — one at the 8,000 ceiling, one at the wall clock, still climbing.
The world almost certainly has its own carrying capacity — light income is a finite rate
(irradiance × area, discounted by shading) and matter is a finite stock that bodies lock —
but at irradiance 200 both limits sit **above the population the machine can afford**:
8,000 creatures already run at a fifth of real time. Every run that ends as a runaway is
censored, and a censored run can never satisfy the goal's "alive at the last sample". The
world is not broken; it is bigger than the instrument.

**Chosen: scale the world down until its natural equilibrium sits inside what we can
compute** — lower `IrradianceWattsPerM2` (`EVOSIM_IRRADIANCE`) until the round-4 world
settles at roughly 1,500–3,000 creatures, found by a pre-registered dose probe
(logbook/0040) rather than by guessing. No new death, no new cap: the same biology in a
dimmer sea.

**Rejected: raising the ceiling** — throughput is population (CLAUDE.md), so chasing the
equilibrium upward buys nothing but wall clock; and the equilibrium might sit at 20,000.
**Rejected: an artificial population cap or culling** — it would *be* the self-limitation
the goal requires the world to provide, and every result downstream would be about the cap.
**Deferred: shrinking the area instead** — in principle the cleaner rescale (same
per-creature physics, fewer creatures), but area is not known to be one knob
(`NutrientField` area, fluid bounds, spawn region), and irradiance already has the env var
and the header line. Revisit if lowering irradiance distorts per-creature margins enough to
matter — it shallows the photic band, which is a real change to the world, not a pure
rescale.

**Cost, named:** a smaller population is a slower mutation supply — mutant absorptives
arrived at roughly one per 23,000 arm-seconds at round-4 population sizes, and arrival
scales with births. Scoreability is being bought with evolutionary throughput.

**Sequenced before D052's round**, reordering D052's own "next" — because D052's round
cannot be scored in a world that ends every run by censoring, whatever excretion does.

### D054
**The floor becomes a gradient — a beach, a shelf, and a deep** · 2026-08-30

The owner's proposal: one side of the world is a beach where the floor meets the surface,
and the floor slopes from there to full depth at the far side. Decided as a direction and an
architecture; not designed, not scheduled before the goal.

**Why it matters here, beyond looks.** The project's central disease is that matter falls
out of the light and returns from 60 m down over timescales longer than a lifetime. On a
sloped floor the shallow end has its floor *inside the photic band*: detritus landing there
mixes back into lit water over a metre, not sixty. A shelf is regeneration by geometry — the
same mechanism D052 buys by contract — and it is why real coastal shelves are the ocean's
productive zones. It also creates habitat structure this world has never had: a
self-fertilising shallows against an oligotrophic deep, basins that trap matter, sills that
partition populations — the island model emerging from terrain instead of orchestration.

**The load-bearing architectural commitment, made now so the first build does not preclude
the later ones:** the floor is **a depth per column** — `floorDepth[x]`, the environment
becoming 2-D (depth varies along one axis, uniform along the other; the nutrient field
gains columns, `Settle`/`Mix`/floor-exchange and shading go per-column) — and a straight
slope is merely the first *profile* fed to it. An irregular bottom is then content, not
architecture. The physics floor is a mesh following the profile.

**The profile is config, hashed like everything else:** explicit control points *or*
generator parameters plus a seed — loading refuses ambiguity, per the project's rule — so a
run stays reproducible from `(genome, seed, configHash)`. **Procedural generation is
deferred** until terrain diversity is itself the question (many worlds, or the island
model); when built, it is a seeded 1-D midpoint-displacement/fractal pass over `Rng` in
`Evosim.Core` — Unity's Perlin is neither seedable nor allowed there.

**Questions the design must answer before building** (recorded so they are not discovered
mid-build): the world currently has no lateral bounds at all — the beach forces the question
of what the other three sides are; the corner where the floor meets D050's y=0 surface clamp;
whether a deep basin at low mixing becomes a matter ratchet with geography (the CLAUDE.md
floor-ratchet gotcha, per column); and conservation audits closing at 0.0000% across a
per-column floor.

**Sequenced after the goal** (D053 → D052 round → fix 3 if still needed), as the destination
the goal's world grows into — unless D052's round fails in a way that regeneration by
geometry would answer and the contract cannot, in which case this moves up and gets its
design pass early.

### D055
**The seabed is a refuge — the floor layer cannot be grazed** · 2026-08-31

This is fix 3, the damping on the consumer, decided by the owner on 2026-08-30 after round 6
(logbook/0041) left it the only open failure. The consumer–resource cycle here has no
damping at all: `AbsorptiveCell` captures at a fixed clearance whatever the density, so every
lineage that establishes eats the deep water from ~27 J/m³ to ~4 and busts — seven rounds,
seven busts, and in round 6's s3 the bust became the first trophic collapse, the chain
dragging its whole world down with it. Nothing damps a consumer here but its food, so the
fix is to give the food a floor it can retreat beneath.

**Chosen: the floor layer of the detritus field (`Nutrients`) cannot be absorbed by
creatures.** Detritus that has settled onto the seabed is buried beyond the reach of any
mouth; it re-enters the food web only through the paths that already exist — `Mix` across
the floor interface (at mixing 0.2 that returns ~20%/s of the floor's excess to the water
above; CLAUDE.md's ratchet gotcha) and `Remineralise` where it is on. Feeding prices the
floor layer at zero, and `Demand`/`Take` refuse it outright, so the invariant lives in the
field, not in whoever happens to call it. `Deposit`, `Settle`, `Mix` and `Remineralise` are
untouched — matter still arrives, still piles, still leaves by physics. `DensityAt` keeps
reporting the true density; what feeding reads is a separate edible density, so the
instruments do not lie about what the water holds.

This is the classic prey-refuge stabiliser: a consumer–resource cycle with an inaccessible
resource pool cannot be ground to zero, because the pool refills the accessible water at a
rate set by the gradient, not by the consumer. The bust should soften into a dip — the
lineage crashes to what the floor's slow release can carry, instead of to nothing.

**The form: a world rule, not biology.** The owner's explicit call, against the alternative
recorded in the handoff — making `AbsorptiveCell`'s capture density-dependent. A
density-dependent mouth is a per-creature behaviour the genome should be able to discover
and tune; hard-coding it would put the damping inside the organism by fiat, and every
future feeding strategy would sit on top of an invisible curve. A refuge is geography:
selection stays endogenous, and creatures remain free to evolve any mouth they like against
a world that simply has one shelf they cannot graze. It also foreshadows D054 exactly — a
sloped world is refuges and larders *by geography*; this is the flat world's one-metre
preview of that mechanism.

**The knob: `FloorRefugeMetres`, default 0, off — bit-identical when unset**, in the
D051/D052 shape. Metres rather than a boolean because the tunable machinery is float-typed
throughout, because "how thick is the ungrazeable sediment" is the honest physical
quantity, and because a thicker refuge is the obvious dose if one layer's larder proves too
small. Layers whose index is within `ceil(refuge / LayerMetres)` of the floor are refuge.
`EVOSIM_FLOOR_REFUGE` reaches it; the header prints `· refuge X m`. Round 7 runs it at 1 —
exactly the floor layer.

**Scope, drawn precisely:** the refuge applies to the `Nutrients` field only. The `Matter`
field is not grazed by anyone — creatures draw it at conception, which is a payment, not a
meal — and a matter refuge would only create a sterile bottom band for no damping gain.
Scavenging, when it exists as its own path, is a mouth like any other and meets the same
refuge.

**Rejected: density-dependent capture** (above — biology by fiat, and it forecloses
evolution's version). **Rejected: making the deep water less reachable** (the handoff's
other candidate — e.g. a swimming cost gradient): it damps by distance, which round 5b
showed this world converts into irreversible sinking deaths, and it would punish the diving
strategy as such rather than capping the harvest.

~~⚠ Project inference from general consumer–resource theory (refugia stabilise
Rosenzweig–MacArthur-type cycles); the literature review has not searched predator–prey
refuge models. Add a primary source before leaning on the dynamics quantitatively.~~
↻ **Searched — review round 4 (2026-09-01), and the theory convicts this entry.** The
primary source is Křivan 2013 [KR13, `research/LITERATURE-REVIEW.md` §5]: the consumer's
equilibrium requires the refuge floor *plus* its break-even accessible density
(R\* = R_c + m/(λ(e−hm)), [KR13 p.2]), so a refuge that covers the feeding ground does
not stabilise — it removes the consumer, the terminal outcome in González-Olivares &
Ramos-Jiliberto 2003's progression (cycles → stable point → predator extinction). The
inference above was not wrong about refuges; it was wrong about *which pool was the
refuge*: for a benthic consumer the floor layer is the feeding ground, not the reserve.
Had this warning been resolved before round 7 ran, the assay's answer was on the shelf.

**Rejected as a world rule at 1 m** · 2026-09-01. Falsified twice over, once from each
side. Round 7 (logbook/0042) showed the refuge blocks establishment: chains start by
grazing the floor pantry, and every treated establishment was strangled at birth. The
invasion assay (D060, logbook/0043) then showed it kills established consumers too: in
paired worlds with a verified consumer hand-placed at t=8,000, both control lineages
established and boomed past 100 while both treated cohorts starved to extinction on top
of ~13 kJ of forbidden floor stock, without one descendant. The mechanism error was in
the prey-refuge analogy's premise: the analogy assumes the consumer feeds in the
*accessible* pool and the refuge protects a *reserve*. This ecology's only evolved
consumer is benthic — it sinks to the floor within ~900 s and feeds there — so the
"reserve" is the entire food supply and the refuge is total exclusion, not a fraction
held back. The knob (`FloorRefugeMetres`) is kept as a built instrument for exclusion
experiments; it is not a candidate stabiliser at any dose that covers the floor layer,
which every dose ≥ 1 does. The design tension it addressed remains open, passed to the
round-8 decision (owner's hypothesis on record in logbook/0043: the deeper distortion is
whole-layer horizontal access, not floor access).

### D056
**Mutation rate: fixed within a round, raisable between rounds, never a thermostat** · 2026-08-31

Discussed with the owner while round 7 ran. The consumer-bust question is now studied at
roughly three chain-establishment events per round — trial count, not arrival, is the
scarce resource — and the owner asked whether the cell-type mutation rate (0.001 since it
existed) should rise, or vary dynamically with the condition of the world.

**Settled, in three parts:**

1. **Fixed within any round.** The rate never changes mid-round, and a round that changes
   it changes nothing else — it is a world parameter like any other, and cross-round
   comparability is the instrument everything else is read through.
2. **The contingent lever, named before round 7 answers.** If round 7 lands in its
   pre-registered "W4 holds but W6 fails" branch (busts soften, chains arrive too late to
   accumulate twenty samples — logbook/0042), round 8 raises cell-type mutation **5–10×**
   rather than running longer: earlier arrivals buy the same measurement window without
   more wall clock, and there is precedent — the first probe chain was found at 20×. Not
   20× again: mutation churn cuts both ways, mutating established absorptives *out* of
   the trade, so `inherit` gets noisier and a genuinely persistent lineage can undercount.
   The round-8 pre-registration must therefore carry an explicit prediction about
   `inherit` stability, so the churn is measured rather than suffered.
3. **The dynamic version is queued for after the goal, in per-creature form only.**
   Stress-induced mutagenesis is real biology (bacterial SOS response, mutator strains
   under stress), and its honest translation is a *local* rule — an organism conceiving
   on a thin margin rolls its offspring at a higher rate — or the fully endogenous form,
   the mutation rate itself a heritable, mutable trait (evolution-strategies
   self-adaptation). Either keeps selection inside the world. Two caveats recorded now:
   the coupling may not reach where it is needed, because mutation happens at conception
   and the most-stressed creatures are exactly the ones not conceiving; and it touches
   every lineage's heritability at once, so it needs its own calibrated baseline, after
   the reference ecology passes.

**Rejected: a global condition-driven controller** — the world senses trouble and turns
up mutation. Same argument as D053's rejected population cap, one level up: a designed
feedback loop steering evolution's fuel supply *is* the adaptive response the goal
requires the world to produce, and every downstream result would be about the
controller. It also breaks the fixed-rate assumption under which arrival rates and
`inherit` streaks have been interpreted since round 1.

⚠ Project inference: the literature review has searched neither stress-induced
mutagenesis nor self-adaptive mutation rates. Before part 3 is built, a review round
should — the SOS-response and evolvability literatures are the places to look.

### D057
**A species is a clade within a drift threshold of its founder** · 2026-08-31

(The decision D057 and the round name `d057` coexist, as D052/`d052` already do — a round
name is not a decision number.)

DESIGN.md §5A.1 defines a species as "a distribution of part types over a body plan" —
which says what a species *is* and gives no boundary: it cannot count species, cannot
separate two independent origins of the same body plan, and cannot say when a mutating
lineage stops being what it was. The owner asked for an operational definition with
mutation tolerance — a boundary crossed by accumulated change, never by every birth.

**Chosen: species membership assigned at birth, by descent plus a drift threshold.**
Every creature carries a species ID. A child inherits its parent's species unless its
genome's distance from that species' **founding genome** exceeds a threshold θ, in which
case the child founds a new species and its genome becomes the new reference. A species
is therefore a clade, measured from its founder, whose members remain within θ of where
the species started. Consequences, all intended: a parameter tweak never speciates; a
lineage that keeps drifting eventually does — the chronospecies boundary, crossed at a
declared distance from origin even though every parent–child pair along the way was
nearly identical; species are countable at any sample (distinct IDs among the living) and
have founders, birthdates and lifespans for free; two independent origins of one body
plan are two species, which no signature-only definition can distinguish.

**The distance metric is where the design work lives.** A weighted sum over the genome
graph, NEAT-shaped: cell-type differences, topology differences (nodes and edges added
or removed), and parameter differences summed small. One deliberate commitment: **a
cell-type change weighs ≥ θ**, so gaining or losing a trophic trade is always a
speciation event by construction — the operational definition then agrees exactly with
§5A.1's, while adding the gradualist boundary it lacked.

**The brain is a fourth term, weighted zero for now** (owner's question, same day).
Brains are near-selectively-neutral in the current world — movement has never paid and
perception is partial — so brain drift is mostly neutral noise, and counting it toward a
founder-anchored threshold would guarantee meaningless speciation by drift. The term
exists in the metric's structure with its own weight so it can be raised between rounds
once brains earn selection; at that point behaviorally distinct, morphologically
identical lineages — biology's cryptic species — become visible to the instrument.
Raising it is a hashed config change like any other. θ and the weights are tunables
in the config hash, calibrated the project's usual way: measure the distance distribution
of actual single mutations in a reference world, set θ several typical-mutation-lengths
out.

**Character: pure instrumentation.** The species ID influences nothing in the world — no
round is perturbed, no comparison confounded, and D056's thermostat rejection is not
touched. Assignment is deterministic at birth (one graph comparison against a fixed
reference — cheap at ~40,000 births/hour on ~5 KB genomes), so `(genome, seed,
configHash)` replays exactly. It is also the *component definition* the Bedau/MODES
open-endedness instruments require before they can compute anything, the natural field
on the queued `lineage.jsonl` birth events, and the sharper measurement D056's round-8
`inherit`-stability prediction wants (species survival versus trait survival).

**Rejected: reproductive isolation** (asexual world — inapplicable in principle).
**Rejected: clustering the living population** (OTU-style, at sample time): identities
are unstable across samples and chaining artifacts merge distinct clades; assignment at
birth gives every creature one permanent answer. **Rejected: signature-only equivalence
classes** (the cell-type multiset alone): too coarse — a world could diversify radically
in shape, size and brain while reading as one species forever — and polyphyletic.

**Sequenced:** implementation queued with the lineage-events infrastructure (HANDOFF,
after the goal); nothing about it blocks or is blocked by the current rounds.

*Built 2026-08-31, pulled forward by the owner* (no retroactive version exists — every
round without it is species data lost). `SpeciesDistance` (four weighted terms; nodes,
edges and neurons matched by list index, the same in-place contract `Mutator` already
follows, so a mismatch counts as topology rather than being re-aligned; the cell-type
term is a position-independent multiset so one trophic change reads exactly 1.0),
`Organism.SpeciesId`, the registry on `World`, five `[Tunable("species")]` knobs
(`SpeciesDriftThreshold` default 0 = off and bit-identical; brain weight default 0 per
the amendment), `EVOSIM_SPECIES_THETA`, header token, `species` column appended last.
Ten tests; suite 354 → 364. θ is uncalibrated: the harness
(`./scripts/core-test.ps1 -Filter SpeciesCalibration`) measured default-weight
single-mutation distances at median 1 / p90 3.0 / max 9.4, and an isolated cell-type
change at 1.0 minimum — θ goes several mutation-lengths out, at or below the cell-type
weight, chosen in the round that first turns it on.

⚠ Project inference: a synthesis of NEAT's compatibility distance (Stanley &
Miikkulainen 2002), microbiology's OTU thresholds, and phylogeny-tracking-with-coarsening
(Dolson & Ofria's systematics work). The review has read none of these primarily; NEAT
and the systematics paper are the two sources a review round should verify before the
metric is built.

### D058
**Only the budget ends a run — censored arms cannot pass** · 2026-08-31

Ratified by the owner from an independent review (owner-provided, 2026-08-31), whose
argument stands on its own: wall-clock censoring correlates with population, because a
larger world buys simulated seconds more slowly — so the worlds most likely to carry a
large chain get the shortest observation windows, and throughput can masquerade as
persistence. The review also caught what our own record had stretched: round 6's sole
qualifying chain (`d056-s5`) was scored at a **wall** cut, while 0041's declared
scored-at-last-sample clause named only *budget* and *ceiling*. The letter of the
pre-registration did not cover it.

**The rule, from round 7 onward:**
- A **budget-complete** arm may pass or fail the final persistence endpoint.
- An **extinction** is a failure.
- A **wall or ceiling cut is censored**: it may support intermediate mechanism findings,
  and can never pass the endpoint.
- A censored arm that looked promising is **rerun alone** to the same simulated-time
  budget — same seed and config replay identically on the same machine and Unity
  version, and a solo arm owns the whole machine, so the wall moves out.

Round 7 is dual-scored: the pre-registered W table as written (historical honesty), and
the stricter completed-budget table beside it. Historical scores stand as recorded;
under this rule round 6's food-chain count reads as zero confirmed passes and one
promising censored arm.

### D059
**The ocean gets a floor — D050's mirror, per-column under D054** · 2026-08-31

The owner ratified that `WorldDepthMetres` names a physical seabed. The hole it closes
is recorded twice over: round 5b's last survivors died at −131 m in a 60 m world
(logbook/0040, the CLAUDE.md gotcha), and the review's sharper point against D055 —
a creature resting at the intended seabed and one fallen seventy metres beneath the
world read the same field state, so a refuge result cannot currently distinguish
geography from exile.

**Chosen: clamp net downward force at y = −WorldDepthMetres** — the exact mirror of
D050's surface rule, which stopped net upward force at y = 0. Creatures stop sinking at
the seabed and can rest there. No collision plane: resting contact, corpse piles and
their physics are deferred to D054, where this invariant becomes **`floorDepth[column]`
— the seabed differs by place under a gradient (the owner's point), and the flat −60 m
clamp is that invariant's flat-world special case.** Default-off knob in the house
shape, so every prior world replays bit-identically; switched on only between rounds,
by pre-registration.

**Lands together with the below-world observables** (they are what make a refuge result
readable even before the clamp is on): count and share of organisms below
−`WorldDepthMetres`, the same for absorptives, edible versus physical detritus density
at mean depth, refuge-layer stock, and mixing flux out of the refuge per report window.

### D060
**The invasion assay — a labeled hand for a mechanism question** · 2026-08-31

Approved by the owner as a diagnostic. The goal contains two questions the full-world
rounds currently ask at once: can an established consumer persist in this ecology, and
can the native evolutionary process discover one often enough? Waiting on a rare
mutation arrival means most of a ten-hour arm produces no evidence about the damping
mechanism at all.

**The assay:** save one verified absorptive genome from round 6, introduce the same
small inoculum into paired worlds — excretion-only against excretion-plus-refuge — at a
fixed simulated time, same seeds, and measure peak, trough, recovery above a declared
threshold, resource draw, and descendant persistence at fixed checkpoints. It answers
"does the refuge stabilise a consumer once present" directly.

**It can never satisfy the endogenous goal** — it is an instrument, exactly as the
founding floor is: a hand that builds the experimental condition, labeled as such, with
the endogenous question answered separately by native-mutation rounds. It refines
D056's contingency ordering: the invasion assay comes **before** any mutation-raising —
if invasions persist but native arrivals stay late, D056's 5–10× rate runs as a
separate *discovery* treatment, reported as a different evolutionary regime; if
invasions fail under the refuge, mutation supply was never the problem and no rate
change is warranted.

**Built** · 2026-09-01. `World.Inoculate(genome, count, heightY)` on the floor-founder
pattern exactly: endowment `FounderEnergyJoules`, `EnergyIn` credited so the audit closes,
no matter debt, generation 0, no parent — so descendants land in the `inherit` column and
each inoculant founds its own species under D057 without any new species code. The genome
is copied verbatim, never mutated: a mutated copy would not be the genome the caller
verified. Seeds come from the world's own stream (`Rng.SeedFor(Seed, _nextIndex++)`), so a
run replays from (genome, seed, configHash, inoculation). Timing and dose are hashed
tunables (`InoculateAtSeconds` default 0 = never, `InoculateCount`, `InoculateDepthMetres`);
the genome itself is a file, not a number, so its identity is recorded where files are —
its SHA-256 in the report header and `run.json` — rather than folded into `configHash`.
Harness fires once at the first metabolic step past the instant, warns loudly on either
half-configuration (a time with no genome, a genome with no time — the identical-numbers
gotcha, pre-empted in both directions). Four tests: default-off bit-identical, exact-credit
audit closure, species founding under θ on/off, same-seed replay. The first inoculum is
`inocula/d056-s5-absorptive.json` (SHA-256 `e6f8e4da1edb…`), the largest-volume absorptive
from round 6 s5's final snapshot — notably a **one-part body**: all 50 absorptive genomes
in that snapshot develop to solitary absorptive blobs, everything else pruned for volume.
First use pre-registered in logbook/0043.

### D061
**The patchy world — horizontal structure, throttled exchange, endogenous inequality** · 2026-09-01

Decided by the owner (three-arm round 8, this as arm A) from the proposal in
`fable-propose-d061.md`, absorbed here and deleted per the propose-file pattern. The
hypothesis is the owner's, raised on seeing round 7.5's results: every depth layer is a
perfectly-stirred tank — a creature at the right depth feeds from the entire 400 m² at
once, no travel, no local depletion — which breaks the spatial structure real
consumer–resource persistence depends on, and forecloses movement ever paying.

**The evidence (review round 4, Q9).** The well-mixed limit is the provably unstable
case: a water column with a growth gradient and a mobile grazer is stable even under
linear feeding at effectively unbounded carrying capacity, until diffusion crosses a
critical value and homogenises it into the always-unstable classical model [FM15 p.1,
p.19]. Empirically: one continuous 90-plant platform died in 120 days while eight islands
with deliberately throttled bridges — and fewer plants — persisted 393–447+ days, no
island individually persistent [JN97 p.7]; Huffaker's 120-position universe held three
oscillations where every simple universe died [HUF58 p.39–41]. Two constraints with
teeth: subdivision alone is the null result — dispersal can destabilise (Briggs & Hoopes
2004, abstract) and identical patches buy nothing, the patches must be **unequal**
[HZ13 p.5]; and the criterion is a length-scale ratio, not a dispersal rate — coexistence
dies when the boom-bust pattern's wavelength outgrows the domain [RMF07 p.5].

**The arithmetic that shaped the design.** Boom-to-bust here runs ≈5,000–8,000 s; the
current is 0.05 m/s; a 400 m² world is ~20 m across. Anything advected at current speed
crosses the world in ~400 s — fifteen times inside one cycle — so patches coupled at
current speed are one pool with extra bookkeeping. **Horizontal exchange must be its own
slow knob.**

**The mechanism:**
1. Each nutrient/matter layer splits into `HorizontalPatches` cells (round 8 runs K=8,
   Janssen's number). Deposit/Settle stay within-patch; `Mix` generalises sideways under
   its own `HorizontalMixingDiffusivity`, much smaller than vertical — the throttle.
2. Organisms gain a patch index. Feeding, deposit, excretion and death-return are local.
   Movement between patches is a `DispersalChancePerStep` (metapopulation-style, like the
   throttled bridges), not continuous advection. Offspring are born in the parent's patch.
3. **Inequality is endogenous:** `PerPatchShading` makes each patch's producers shade only
   their own column. Crowded patches darken themselves; the creatures generate the
   heterogeneity that stabilises them — no painted-on constant, and the first reason a
   producer has ever had to be somewhere else.
4. The prey/predator dispersal asymmetry (prey dispersal stabilises, predator dispersal
   destabilises [JN97 p.11]) is left to the physics: detritus already moves by mixing (the
   reseeding path), consumers sink vertically. If evolution finds horizontal movement
   worth paying for, the movement frontier finally has a prize.

All knobs default to off (K=1, today's world) — bit-identical in the D052/D055 shape.
**Dose target from the measured trap (logbook/0043):** a stabiliser succeeds iff some
reachable pool stays above ≈7 J/m³ (the transplant genotype's reproduction threshold;
survival is 4, senescent 8 — thresholds are per-genotype, the trap is not) through the
trough. Natural rain re-arms any patch left ungrazed — which is exactly what arm A banks
on.

**Rejected within this decision:** patch exchange via the existing current (the
wavelength arithmetic above); painted-on patch inequality (a constant nobody can defend
when shading does it endogenously); continuous horizontal advection of creatures (the
throttled-bridge regime is the one with the empirical persistence record).

### D062
**The satiation cap — a filter feeder's mouth gets its physical limit** · 2026-09-01

Round 8's arm B, the cohort-trap hedge, decided by the owner alongside D061. The lineage
record (logbook/0043) shows every bust — transplanted and natural — is a **recruitment
collapse**: the boom grazes the pool below the reproduction break-even while every adult
still clears survival, and the cohort pins its own food in that band and ages out
sterile. A mechanism that softens the drawdown before the trough is therefore a direct
candidate, and the literature adds that our current mouth is not a simplification but an
impossibility: a true type I response has a satiation plateau *by definition* — 814
catalogued responses, type I exclusive to filter feeders, all with the threshold
[JKT04 p.1] — and slight relaxation of consumption at low resource density (q=0.1 of the
way to type III) eliminated extinctions in model food webs [DBWM05 p.12].

**The mechanism:** two knobs on `AbsorptiveCell`, both default-off (today's unbounded
linear clearance, bit-identical):
- `SatiationWattsPerCubicMetre` — intake per m³ of tissue is capped at this power. The
  plateau a real filter is forced to by handling time.
- `ClearanceToeDensity` — below this density, effective clearance scales down smoothly
  (a type-III toe): the relaxation-at-low-density stabiliser, dose-tunable toward
  q≈0.1's cheapness.

This touches §5A.1's feeding contract, which is why it is its own D-entry and not a
sweep parameter. ⚠ The honest doubt, pre-registered: a pure intake cap slows the boom
but adults' *survival* grazing is what pins the pool in the trap band — the toe, which
lets the pool escape upward at low density, may be the half that matters.

### D063
**The recruitment clause — a chain must be breeding, not merely surviving** · 2026-09-01

The goal rule's blind spot was demonstrated, not suspected (logbook/0043): "inherited
≥ 20 consecutive samples and ≥ 10 alive at the last sample" is satisfied for thousands
of seconds by a sterile relic — s2's 104-sample streak was sterile from sample ~60,
recruitment having collapsed at the population peak in every measured boom. The owner
amended the rule:

> In at least 3 of 5 seeds, with the population floor closed: the producers persist to
> the end of the run, an absorptive lineage is inherited for ≥ 20 consecutive samples,
> it is still alive (≥ 10 individuals) at the last sample, **and at least one absorptive
> birth occurred within the last 20 samples.**

Applied from round 8 onward; earlier rounds' scores stand as recorded (none passed, so
nothing is retroactively changed by the tightening). Every future scoring reports the
lineage's last-birth time alongside the streak. The clause is computable two ways —
`lineage.jsonl` (exact) or a births-in-window read of the absorptive columns — and the
pre-registration must name which it uses.

**Amendment 2026-09-04 (owner's ruling, after the Sol/GPT review of 2026-09-03 and
logbook/0054's addendum).** The rule scores **one connected clade**, not the aggregate: a
clade begins at an absorptive birth whose parent did not express the trait, or at an
absorptive founder; membership follows the parent chain while the trait is inherited; and
the clauses are asked of that clade — alive for ≥ 20 consecutive samples to the end, ≥ 10
living members at the last sample, an inherited absorptive birth inside the clade within
the last 20 samples — plus two additions: **(a) stability** — the clade holds ≥ 10 living
members through the last two lifetimes (the last 6,000 s), not only at the final sample;
**(b) the producers are scored as a lineage** — an inherited photosynthetic line alive at
the end with a photosynthetic birth in the last 20 samples, read from the `photo` /
`photo inh` columns, not from `alive`. Not added, deliberately: a mutant-origin clause. The
owner's reading of the bar is "the minimum state from which we can move on", and a
founder-rooted clade the world has sustained for 30,000 s is that state; whether a late
stomach can establish was answered on its own (0051 amendment 2; 0054's seed 4). Round 18
(0054) is the first pass under this wording: 4 of 5 by clade, minima over the last two
lifetimes 48 / 41 / 24 / 127; the producer clause is read from the columns the
contract-repairs build adds and is recorded when they exist. `scratch/clade-score.py` is
the scorer until it moves into `scripts/`.

### D064
**Size-dependent buoyancy — small bodies float in place, growth is what sinks you; founders anywhere in the column** · 2026-09-02

Owner's design, ruled on the agent's proposal after rounds 8 and 9 scored zero passes
between them (logbook/0044, 0045). The diagnosis those rounds converged on: every body
carries the same excess density (D044) whatever its size, so the whole population sinks
its whole life and holds the photic band only because breeding is concentrated where the
light is. A matter drought pauses births for ~1,500 s, the standing crowd sinks out of
the light together, and the world starves in the dark with its larder full — three worlds
died exactly so, one of them an untreated control. Float tissue (D049) works but selection
prices it out to ~1% between crises, because a floatless producer breeds cheaper before it
sinks out; in r9-s2 the literal last survivor was a floater holding at −14.5 m. The
treatments of rounds 8–9 metered the pantry; the patient was drowning.

**The rule.** Effective excess density scales with body volume:

> ρ_eff(V) = `TissueExcessDensity` × max(0, 1 − (V0 / V)^⅔)

with V the phenotype's `TotalVolume` and V0 a new tunable `FluidConfig.NeutralBodyVolume`.
At or below V0 a body is neutrally buoyant and holds its depth for free; every added cell
above it begins to cost depth; as V → ∞ the rule converges to today's constant, so the
worlds run to date are its large-body limit. Lift (D049/D050) is unchanged and nets against
the scaled density exactly as before — it simply has something worth buying now. **The rule
is universal across guilds, at the owner's explicit direction:** a plants-only version
would leave sinking as a free elevator to the floor larder for precisely the guild that
wants to descend; universal, descent is a priced choice — an absorber reaches the pantry
by being *large*, paying the upkeep of the size that sinks it. Size becomes trophic
strategy: small floaters where the light is, large sinkers where the detritus is, plankton
against benthos as an emergent and falsifiable prediction. Growth does not exist within a
lifetime (§5A.6), so V is fixed at birth and "growth" here means evolutionary growth across
generations, which is what the owner asked for ("only sink when they start to evolve and add
more matter or more cells").

**Founders anywhere.** `FounderDepthSpread` becomes an env knob (`EVOSIM_FOUNDER_DEPTH`)
and round 10 sets it to the full water column, so the world stops privileging the surface at
founding; deep producer founders die of darkness by selection, not by rule, and a small
absorptive founder scattered deep now *holds position* near the larder instead of
plummeting past it. Children born beside their parents was the owner's third point and is
already the code's behaviour (`World` gives offspring the parent's height); recorded so the
package is complete on paper. x and z remain cosmetic outside patchy worlds — the
layer-as-stirred-tank limitation (D061's motivation) stands and is not addressed here.

**Marked as inference.** The shape is Stokes-flavoured — small particles effectively do not
sink, sinking speed rises with size — but Stokes strictly gives velocity ∝ r²·Δρ, and folding
the size dependence into Δρ with a ⅔ exponent is this project's simplification, chosen so one
existing mechanism (excess density × drag) carries the whole rule with one knob. No source is
cited for the exponent; §5A.10's rule applies and a run can vary it.

**Rejected alternatives** (the agent's first draft, superseded by the owner's design):
lighter tissue for everyone (`TissueExcessDensity` 0.02 → 0.005) — fixes the drowning but
removes the size trade and dulls the future movement prize; cheaper lift (`WattsPerLiftUnit`
0.05 → 0.01) — makes insurance affordable but leaves the un-physical size-blind sinking in
place; mixed-layer turbulence — the honest physics for resuspension, deferred as new
mechanism mid-campaign, and still the right later replacement if depth-holding proves to
need more than buoyancy.

**Default-off, bit-identical**: `NeutralBodyVolume` = 0 reproduces today's behaviour exactly
(suite-enforced), hashed and JSON round-tripped by the two reflection tests, and rendered in
every run header as `neutralV`/`founderDepth` tokens. Round 10: one package arm × five seeds,
mutation 0.005 (the D056 discovery regime), no refuge, budget 30,000 s, scored under D063
unchanged; pre-registered as logbook/0046 before launch.

### D065
**The fixed matter cost — a body costs a minimum of matter to exist, so the count cannot ratchet through shrinking** · 2026-09-02

Rounds 10 and 10b (logbook/0046) showed D064 working exactly as designed and the world
answering it with the one strategy the rule makes free: **get small.** Under D064 a small
body floats in place and, under D048's proportional matter price, costs almost nothing to
conceive; selection halved mean body matter every ~1,100 s in all ten arms (25 → 4 units per
body at area 400; 14 → 1.6 at area 100), and the population count — bounded by total matter
÷ matter per body — rose without a floor toward the instrument ceiling. Shrinking the dish
(area 400 → 100, the owner's ruling for round 10b) scaled the cap by four and the bodies kept
dividing it; no dish is small enough against a divisor that goes to zero. The world is not
wrong — nutrient-starved surface water dominated by ever-smaller cells is what real
oligotrophic oceans do, for the same surface-to-volume reason — but the computer cannot
afford it, and the goal rule does not need it.

**The rule.** A child's matter price becomes

> `MatterPerTissueJoule × tissue + MatterPerCreature`

with `RunConfig.MatterPerCreature` a new tunable, units of matter per body, default 0. Real
cells carry a machinery mass that no shrinking removes; this is that, at its coarsest. ~~The
fixed term is locked in the body like the proportional one and returns on the same legs —
excretion (D052) drains it, death deposits it — so the audit closes by construction.~~
**Amended 2026-09-02, before round 12:** the fixed term is *machinery mass* and leaves only
with the body. Excretion (D052) drains the proportional (tissue) share alone —
`min(LockedMatter − MatterPerCreature, k × upkeep)` — and death deposits whatever remains,
fixed term included; the audit still closes by construction. The reason is the owner's
ruling to raise excretion tenfold (0.001 → 0.01, round 12 arm B, logbook/0048): at that rate
a body would otherwise excrete its fixed cost within a lifetime, living bodies would hold
less and less, and the count cap would ratchet again through the back door — the disease
this entry cured, returning by a second route. Bit-identical at `MatterPerCreature` = 0. The
cheapest-child early-out and its guard include it. Founders and inoculated creatures follow
whatever they do today for the proportional term. It also gives D064's size axis the lower
bound it needs: below some size a body's matter is all overhead, which is where the
plankton-versus-benthos trade should start.

**Dose for round 10c.** 3 units per body, at area 100: 6,000 units ÷ (3 + a proportional
share now at 1.6–2.7) caps the count near 1,100–1,400 bodies whatever the bodies weigh,
against a ceiling of 8,000. Chosen so the bloom is affordable, not so it is small — the
count should level where matter runs out, well below the instrument.

**Rejected alternatives.** Raising the ceiling (one-part bodies are cheap in physics, but
8,000 → 20,000 puts a 30,000-s arm past the 10-hour wall for five concurrent seeds);
lowering `InitialMatterPerCubicMetre` or the area further (both scale the cap without
touching the divisor — the ratchet continues, only later); a hard body-size floor via
`MinPartVolume` (a development limit dressed as ecology, and it says nothing about *why*
small should cost).

**Marked as inference.** The minimum-mass argument is a physical commonplace, uncited here;
§5A.10 applies and the value is a run knob. Default-off, bit-identical, hashed and
round-tripped by the reflection tests, rendered in every header as
`matter <per J>/J + <fixed> each`. Round 10c: the D064 package at area 100 plus this,
five seeds, scored under D063 unchanged; pre-registered in logbook/0046 before launch.

### D066
**Roll cells — the water moves everything, and it stirs** · 2026-09-02

Rounds 10c and 11 (logbook/0046, 0047) left one gate: the larder is full and the eaters
cannot reach it. Detritus sinks at 0.02 m/s and clears the lit band in twenty minutes; the
deep holds ~10 J/m³ and the surface 0.2; every absorptive mutant is born small at the
surface, floats there under D064, and starves above a full pantry. The owner's reading:
the current was supposed to move creatures on every axis *and* move matter, and does
neither — and a single uniform flow could not stir in any case, because stirring is
*differential* motion. "What we need is something that can stir the soup."

**What the current was.** Two depth-only standing waves with provably zero mean (the
2026-08-26 entry), advecting bodies alone; at speed 0.05 m/s and period 300 s a
displacement of ≈2.4 m in a 25 m cell — a jiggle. Fields were deliberately left to a
separate diffusion (`Mix`, 0.2 m²/s) on the argument that a corpse is not a physical object.
Half right: a scalar field is exactly the thing a velocity field advects. What was true is
that a one-column oscillation acting on a field amounts to mixing, and at 0.2 m²/s the mixing
loses to sinking (2,900 s to cross the lit band against 1,200 s).

**The rule.** One prescribed, analytic, divergence-free flow over D061's patches advects
bodies and the scalar fields (detritus, matter) alike:

- **Structure — convection rolls.** Each roll spans two adjacent patches: patch *k* flows up,
  patch *k+1* flows down, joined by horizontal flow across the surface and the floor. With a
  roll of width 2W over patches of width W and a cell of depth H, the stream function
  ψ = Ψ·sin(πx/2W)·sin(πy/H) gives a patch-averaged vertical velocity w_k(y) = s_k·(Ψ/W)·sin(πy/H)
  with s_k = ±1 alternating, and a boundary horizontal velocity u(y) = Ψ(π/H)·cos(πy/H) between
  the patches of a roll (surface-ward in one direction, floor-ward the other). Vertical
  velocity is zero at y = 0 by construction — the property that prevents logbook/0022's flying
  population — and zero at the floor. With one patch the rule degenerates to today's pure
  vertical oscillation with the fields now advected too.
- **Time — chaotic advection.** Amplitude Ψ(t) carries the existing two incommensurate periods
  (the golden-ratio pair), and the roll pattern *blinks*: every `RollBlinkSeconds` the parity
  s_k flips, so a parcel that rode up in patch *k* next rides down in it. Blinking rolls stretch
  and fold fluid exponentially — Aref's "stirring by chaotic advection" (J. Fluid Mech. 1984)
  and the oscillating-roll experiments of Solomon & Gollub (1988); **both from memory, to be
  verified before either enters DESIGN.md**. The stirring the owner asked for does not need
  turbulence computed; it needs a structured flow that changes in time.
- **Fields.** Conservative upwind transfer: at each layer interface within a patch, move
  min(½, |w|·dt/h) of the upwind layer's stock across; at each patch boundary of a roll, per
  layer, move min(½, |u|·dt/W) of the upwind patch's stock across. Every move takes from one
  cell and gives to another, so every joule and every unit of matter is conserved exactly, at
  any timestep; the Courant clamp is the one `Mix` already uses. Diffusion stays as the
  residual, at its own knob.
- **Bodies.** Advected by their patch's w_k(y,t) through the existing drag-relative-velocity
  path; the horizontal crossing between patches uses D061's `Disperse` with per-step
  probability |u(y)|·dt/W at the creature's depth, so a creature and the detritus beside it
  cross together in expectation. Nothing in the base world reads x; with patches, x now
  means which roll leg you are in.

**Cost.** No fluid is simulated. Bodies evaluate an analytic velocity once per body per
physics step, as today; fields cost O(patches × layers) per metabolic step — a few hundred
operations for four patches — less than the diffusion pass. The stirred-tank layer, D061's
motivation, dissolves without a particle being computed: a patch is no longer sealed.

**What it buys.** Detritus carried up into the light and small mutants carried down to the
larder, in the same parcel; the deep matter reservoir (≈15% of the world's matter, unreachable
in round 10c) in circulation; light averaged over a cycle for producers — Sverdrup's critical
depth becomes a real constraint, which is a feature and a named risk; and later, water to swim
against, where movement has something to pay for.

**Named risks, pre-registered in logbook/0048.** Sverdrup (1953): a cell deeper than the photic
band stirs producers into the dark for half of every cycle; a 25 m cell against a 24 m band is
marginal and these bodies carry a 3× light margin — measured, not assumed. Founding: round 8's
patchy world died of founding cost in eight sealed pools; rolls are the opposite regime, fast
exchange, and the result does not transfer, but it is checked. **Deferred, at the owner's
direction:** currents against structures in the ground (the sloped world, D054) — when the
floor has shape the flow must go around it, and a prescribed roll will not; that is a later
decision, not this one.

**Config.** `CurrentField` gains `Rolls` (bool, default false), `RollBlinkSeconds` (default 0 =
steady parity), `AdvectFields` (bool, default false); `Speed`, `PeriodSeconds`, `CellMetres`
gain env knobs. All defaults reproduce today's world bit for bit (suite-enforced); rendered in
every header. Round 12: round 11's world plus this, two arms × five seeds — one patch (pure
vertical stirring, the location gate alone) and four patches (rolls, the stirred soup).

---

### D067

**The vent — an upwelling plume that returns what the trapdoor takes** · 2026-09-03

Round 12 (logbook/0048) measured D066's rolls doing everything they were built to do and
one thing they cannot: a roll that stops above the floor is a trapdoor. Its cell depth is
30 m because 60 m stirred producers into the dark and killed founding (Sverdrup); at 30 m
the vertical velocity is zero at the cell's floor by the same construction that zeros it at
the waterline, and whatever the down leg drops there sinks out at 0.02 m/s for good. The lit
half of the column stays at 0.2–3 J/m³ while the still half beneath it piles up to 15–21.
The owner's proposal: **vents at the bottom**. Read as upwelling rather than as
hydrothermal chemistry — a flow, not a new guild — it is the one thing that closes the loop
without deepening the roll, and it is how a real ocean feeds its surface from a stagnant
deep.

**What it is.** A prescribed plume of water rising at `VentSpeed` from the floor to the
surface in one patch (`VentPatch`), with the return sinking uniformly at
`VentSpeed / (K − 1)` through every other patch, joined by a surface leg that carries the
plume's water half to each side around D061's ring and a floor leg that brings it back. It
is superposed on the roll — two divergence-free prescribed flows add — on the same staggered
grid: vertical velocity at layer interfaces per patch, horizontal transport at patch faces
per layer, upwind and clamped at the same Courant ½. The legs are defined by *volume flux*,
so the fraction of a cell that crosses a face is width-free (`|c_j|·s·dt/L` with
`c_j = ½ − j/(K−1)` for the j-th face from the plume); the drag a creature feels in a leg is
the velocity that flux implies through a face one leg thick, which is why the field is told
the patch width once, by the world, and is not asked to guess it. Bodies and stock cross
faces by the same fractions, so they travel together in expectation, as under D066. A
uniform field stays uniform under the vent up to the operator-splitting residual (order the
Courant number, non-accumulating), and every cell's inflow equals its outflow to 1e-6 — the
test pins the balance directly rather than through the uniformity.

**What it costs, pre-registered.** The rule since D066 is that water carries bodies. A body
at the bottom of a roll's down leg is now carried into the return flow, to the floor, along
it, and up the plume — a dark excursion of roughly `D·(K−1)/s + D/s` seconds per circuit,
which at 0.1 m/s in four patches is ~1,800 s down and 600 s up. r12a-s1's full-column roll
killed founding with about half of each circuit dark; the vent's excursion is shorter and
fixed, and whether it sits inside the producers' 3× light margin is the round's S2, not an
assumption. A narrower plume lowers the return speed and lengthens the excursion in
proportion, so only `K` and `VentSpeed` tune it.

**Config** (all `[Tunable]` on `CurrentField`, hashed, saved, refused-not-defaulted on load;
env `EVOSIM_VENT`, `EVOSIM_VENT_PATCH`, `EVOSIM_VENT_DEPTH`, `EVOSIM_VENT_LEG`; header
`vent 0.1 m/s in patch 0 from 60 m, legs 1 m` or `vent off`): `VentSpeed` (m/s, 0 = off,
which is every run before D067 bit for bit, suite-enforced), `VentPatch` (default 0),
`VentDepthMetres` (default 60; the world refuses a vent whose depth is not its own floor,
because a plume that stops short of the bottom is the trapdoor again), `VentLegMetres`
(default 1 = one layer; a whole number of layers or the world refuses; exact continuity
needs exactly one). One patch is no vent — there is no return patch — and the code takes the
pre-D067 path. `PatchWidthMetres` on the field is derived, not tunable. Built the same day,
22 tests, 442 green; **not yet run**: round 13 (logbook/0049) is the sink lever first, and
the vent is that round's pre-registered next lever if the sink does not bind.

**Rejected here.** A second roll stacked under the first: two prescribed rolls meet at an
interface where the vertical velocity is zero on both sides, so lifted material crosses it
upward only by diffusion — the trapdoor moved down 30 m. Deepening the roll to the floor:
measured, it kills founding (r12a-s1). Exempting bodies from the vent's return flow: it
would make the vent a field-only pump, which breaks D066's rule that the water carries
what is in it and would be the first place in the world where a creature and the water
beside it disagree about where they are going.

### D068

**The stomach's gearing — clearance 5 and 10, because a stomach that cannot out-earn a leaf
never leaves a line** · 2026-09-03

Round 13 (logbook/0049) closed the trapdoor and produced no chain: absorptive mutants
appeared at one or two per sample in every arm and none left an absorptive child, including
one sitting in 7–9 J/m³ with conception refusals flat. Two rounds had moved the water under a
mutant whose budget nobody had read, so it was read from the code. An absorptive part earns
`density × ClearanceRate` per m³ of tissue against 4 W/m³ of upkeep, and the clearance rate
has been 1 m³/s per m³ in every round (`EVOSIM_CLEARANCE`), so a stomach breaks even at
4 J/m³ and clears 3–5 W/m³ in the best water any seed has offered. A photosynthetic part at the
surface clears about 47 W/m³ (a 0.2 m cube, top face lit, 200 W/m², efficiency 0.05,
unshaded — an estimate). A mutant that swaps a leaf for a stomach gives up an order of
magnitude of income on that part, breeds slower than its siblings, and drifts out — which is
the 0.75 children per member 0048 measured. The gate is relative fitness inside the mutant,
not the location of the food; marine snow (0049) lifted the food into the light, where the
leaf wins by ten to one, and left the deep below the stomach's break-even.

**The owner's proposal** was an absorption scalar that converts matter to more energy, with
the option of charging more upkeep for a more efficient stomach. Mapped onto the code:
`AbsorptiveCell.Yield` is that scalar and is capped at 1, because above 1 a feeder returns
more energy than it took from the field — a free-energy source (DESIGN.md §11.2), refused by
the constructor. `ClearanceRate` is the physical version of the same idea — how much water a
stomach processes — and is unbounded, listed as unmeasured in §5A.10, and never measured.
**Ruled (owner, 2026-09-03): raise it.** Round 14 (logbook/0050) runs clearance 5 and 10
on round 13's arm-A world — 5 is where a stomach in 7 J/m³ matches a leaf at the surface and
the surface film is at break-even; 10 is where the stomach wins in any water above ~5 J/m³.
No code changes: the knob and its header token (`clearance N`) exist, so every earlier run
is untouched. Round 13 is cut from ten arms to the five running, on the same ruling.

**Deferred, not rejected: the evolvable trade-off.** Coupling upkeep to clearance per
creature only teaches something if the coupling is chosen so that an interior optimum
depends on local density. Linear coupling leaves break-even fixed at 4 J/m³ and makes the
scalar a gearing knob (bigger surplus above break-even, bigger loss below); sub-linear
coupling makes higher clearance strictly better, evolution drives it to the cap, and the cap
is the parameter. That is a second experiment after the first shows a stomach can win
anywhere.

**Rejected here.** Yield above 1: free energy. A higher cell-type mutation rate: supply of
mutants is not the constraint — they arrive at one or two per sample and fail to breed; the
rate also loads the producers and moves any pass further from a natural regime. Longer
runs: a line breeding below replacement drifts out at any length. A coarser physics step:
a different world, every comparison broken, articulated bodies unstable before they are
fast. Smaller worlds for more seeds: population is the mutant supply.

### D069

**Compute, screen, confirm — the campaign stops learning one number a day from a ten-hour
physics run** · 2026-09-03

Fourteen rounds took a day each and each asked one question of one lucky mutant. The
owner's ruling, after a written assessment of where the time goes ("all I want is the
ability to simulate faster"): build the cheap instruments, adopt the sequential rules, and
test the coarse step — but not the kinematic fast world (the "Tier 2" of that assessment:
a Core-only mover replacing PhysX for jointless bodies), which is deferred as likely to be
overtaken by the physics the world will gain (terrain, particles, structures). Four things,
all built the same day:

1. **The ledger calculator** (`scripts/ledger.ps1`, `Evosim.Ledger` over
   `LedgerForecast` in Core, 6 tests). One body's energy ledger integrated alone, in 0.5 s
   steps with senescence, under exactly `World`'s breeding rule (gate, then price, brood
   size), at a chosen irradiance, nutrient density, shade and absorptive clearance — no
   population, no field, no physics. It reports net watts at birth, break-even density,
   lifetime, R0, time to first child and the matter price of a child, and `-Compare` swaps
   every stomach for a leaf of the same shape and back. It runs in seconds and screens a
   knob before a worker touches it. Its first table (logbook/0051) read: a child costs
   ~129 J whatever the body earns, so R0 is set by net watts alone; the round-13 stomach at
   clearance 10 has R0 = 0 below 2 J/m³, 2 at 4 (a leaf's figure), 6 at 10.
2. **The invasion assay** (logbook/0051), on the run's existing inoculation knob: fifty
   verbatim copies of a genome the world itself produced, dropped into an established
   world; per-capita growth when rare read from `lineage.jsonl` by
   `scripts/lineage-invasion.ps1`. Two hours and one worker per dose instead of a day and
   five; a sample of fifty instead of one.
3. **Round design rules, from round 15 on.** *Futility:* an arm with no inherited
   absorptive by t=15,000 is stopped and scored as failed — no line that went on to matter
   has started later in fourteen rounds. *Sequential seeds:* seeds 1–2 first; 3–5 only if
   a line appears in either. Applied to round 14 from the moment of ruling (0050's
   amendment). Both are the owner's, as round-design rules. *Reaffirmed 2026-09-04 against the
   Sol/GPT review's proposal to report futility-stopped arms as censored: the owner's
   reading is that an arm is stopped under the rule because it is expected to fail on the
   merits, so the stop saves time and the result is a negative result — censoring it would
   prime the record; censoring is for arms that fail through error or a fault in the
   experiment itself. If a stop could only be reported as censored, the arm should not be
   stopped.*
4. **The coarse step** (logbook/0052; `EVOSIM_DT`, Ecosystem.ConfigurePhysicsStep). The
   physics timestep becomes configurable — the metabolic step stays 0.5 s, one clock, only
   the integration granularity changes — and is validated against a seed already run: the
   same world at 0.01 (replay noise), 0.02 and 0.05. D068's rejection of "a coarser physics
   step" stands as a rejection of *changing the world under a comparison*; measuring the
   change against a reference is the opposite of that. *Outcome (0052, closed 2026-09-03):*
   PhysX replays bit for bit under one build, so there is no replay noise and any per-step
   change is a butterfly (±20% population, 4 m depth, half the larder); 0.02 sits inside
   that wingspan and is the screening step at ~3× the pace; 0.01 confirms; 0.05 is out
   (crash without a drag limiter, surface migration and an open audit with one). The
   limiter that makes coarse steps stable engages only above 0.01, so the historical
   record stays replayable.

**What this is not.** Not a change to the goal rule (D063 as amended), which still scores a
full run; the screen decides what is worth confirming, and only a confirmation run passes.
Not a metabolic-step knob: the economy runs once per fifty physics steps and is not the
cost, and the fields' advection is Courant-limited on that step (0.15 at 0.5 s for 0.3 m/s
rolls over 1 m layers; 0.75 at 2.5 s, above the ½ clamp). Not a claim that the calculator
is the world: it has no matter draw, no shading, no field depletion and no drift — 0051's
M5 tests whether that matters, by running both.

**Rejected here.** A scalar on reported time: simulated time advances only when a step is
computed and the loop is unthrottled, so a scalar cannot make steps cheaper, only mislabel
them; applied to the economy alone it makes creatures age and eat at a different rate than
they sink and drift, and buys nothing. Cloud workers: the owner's money and setup, outside
what an agent may do alone.

### D070

**Exudation — the producers feed the water while they live, because the second trophic
level is fed at one percent** · 2026-09-03 (ruled in principle; build gated on two readings)

Round 14 (logbook/0050) grew the campaign's first real absorptive lines — 22 and 48
members at clearance 10 — and both grazed their field below break-even and died back to a
handful with no second wave inside the budget; clearance 5 held a line of 3–9 that neither
grew nor died. The reason is one column: `detritus J`, the whole world's standing detritus,
fell from 9,744 J to 4,215 J while `r14c10-s1`'s line lived and rebuilt at **0.19 W** with no
grazer on it. Dead tissue is the nutrient field's only income (excretion, D052, moves
matter, not energy) and feeding its only outflow, so that slope is the income. The ledger
(D069) says a clearance-10 stomach of the mutant's size needs ~0.03 W to hold R0 = 1, so
**the flux sustains a standing line of about six**; the booms of 22 and 48 ate stored
capital, and the goal's alive clause asks for ten. Clearance 5's line of 3–9 is the same
capacity read from the other dose. Against a producer economy of ~17 W the second level is
fed at ~1%; real pelagic transfer efficiencies run near 10%, and the difference is that this
world's producers exude nothing while alive.

The existing stabilisers cannot fix this, and the ledger says so for this body at −12 m,
clearance 10: satiation 20 W/m³ (round 8's value) caps R0 at 1 at every density — a line
cannot get from one mutant to ten; satiation 8–12 gives R0 = 0 everywhere, because 103 J of
the 129 J child price is the endowment and a 0.0022 m³ body capped at 12 W/m³ of tissue
cannot earn it in a lifetime; the clearance toe (D062) at 1 or 4 J/m³ raises the break-even
to 0.86 or 1.48 without raising the income; the floor refuge (D055) protects a layer these
stomachs do not sit in and was rejected twice. None of the three touches the flux, and the
flux is the constraint.

**The rule, in principle.** A producer deposits a fraction of its photosynthetic intake
into the nutrient field at its own layer and patch, each step, while it lives — dissolved
organic matter, the input to the microbial loop. Configurable, default 0 (bit-identical
until asked), header token, its own counter in the detritus-flux instrument so the first
arm shows deaths and exudation separately, and visible to the ledger through
`Metabolism.StepAt` like the toe. The fraction is not set here: a literature round on
phytoplankton exudation comes first, so the number carries a citation before it is a rule.

**Gated on two readings, both landing within hours of the ruling** (the owner's
acceptance of the agent's recommendation, verbatim in spirit: "rule yes in principle, and
gate the build on two readings"):

1. `r14c10-s1-flux` — `r14c10-s1`'s world at the 0.02 screening step on the build that
   carries `det in` / `det out`. It must read the income at about the inferred 0.2 W with no
   grazer present. If it reads much higher, the diagnosis is wrong and nothing is built.
   *Read the same night (0050's instrument note): met.* The income is a founding pulse —
   4.3 W while founders die, 0.57 W at t=15,000 and falling by a third every 2,000 s as the
   tissue per corpse drops from 87 J to 4.7 — that lands where the 0.01 record put it. A
   corollary for the rule: a flux riding on intake does not shrink with the body the way
   a corpse does.
2. `r14c10-s4` — the richest water any arm has had (−15 m in 7 J/m³). If a larger stock
   turns the crash into a cycle that holds ten, the flux is not the binding constraint and
   the rule is reconsidered before it is built. *Read 2026-09-04: met.* No boom at all —
   61 absorptive births of which 51 were floor-era founders, six mutants and four
   inherited after the floor closed, three breeders in the run, never more than five
   alive, a line of two; the five grazed a
   20,600 J stock down at 0.24 W net. The ceiling read directly. **Gate open; build
   started.**

**The screen, pre-registered now.** Ledger the mutant under the exudation config at the
densities the new flux would set; then two arms at the 0.02 step on the clearance-10 worlds
that formed lines (seeds 1 and 2), run to 20,000 s so the line is read past two lifetimes,
with `det in` split by source. Prediction: **an inherited line holds ≥ 10 alive past two
lifetimes in at least one of the two.** If it does, a confirmation round at 0.01 under D063
as written; if it does not, exudation is rejected as a world rule and this entry is marked
so. *Screened 2026-09-04 (logbook/0053): the prediction held in both seeds — inherited
lines at 77 and 83 two lifetimes after reaching ten, minima 10 and 12, 46 and 133 at
20,000 s; the control had none; the leak delivered 14–20 W against 0.3–0.5 W of corpses
and the stomachs ate it as it landed (standing field *lower* than the control's). The lines
descend from founder-era stomachs that the leak kept alive, not from mutants. Confirmation
round at 0.01 pre-registered as logbook/0054.* **Confirmed the same day: 4 of 5 seeds
pass D063 as amended** — inherited lines of 76–221 at 30,000 s, all founder-descended, all
recruiting, producers at 1,500–1,800, audit closed. The failing seed's last stomachs held
537 J in 13.7 J/m³ and had no children: refused conceptions for want of matter at their
layer, the constraint that follows this one. Exudation at 0.15 is a world rule from this
date (DESIGN.md §5A.2c, changelog 0k). The producers' own dynamics are read on the same arms: a fraction of intake is a tax on
growth, and in the light-0.02 world producers have run away before, so the tax may be
harmless or useful, but it is not assumed either way.

**Rejected alternatives**, each with its reason:

- *Remains that carry a reserve* — raises the flux only by changing what a producer keeps
  at death, which is the breeding rule; deeper, and moves the producers more than a tax.
- *A cheaper child* — the endowment is 80% of the price and a 30 J endowment triples the
  capacity, but it is genome-encoded and a line of six will not evolve it at mutation 0.005
  in any budget; a world-level cap changes every creature's economics, not the stomachs'.
- *Relaxing the goal's alive clause to six* — fits the goal to the world; the failure mode
  this campaign has avoided.
- *Stopping* — legitimate, and the fallback if the screen fails: the record would say the
  ecology as configured cannot hold a second trophic level, and why.
- *Another seed of round 14* — a draw against a ceiling of six cannot reach ten; seed 5 of
  each dose is not launched (owner, same night).

**What this is not.** Not a change to D063; the screen decides what is worth confirming and
only a confirmation run passes. Not a fourth turn of the same knob: sink speed (D067) and
gearing (D068) moved where detritus goes and how fast it is eaten; this is the first lever
on how much there is. And not a number yet — the fraction waits on the review.

**The number, from the review's round 5 (same night; LITERATURE-REVIEW.md Q10).** Percentage
extracellular release runs 10–20% as a world-ocean general range (Nagata 2000 via [CH20
p.5]), 13% as a cross-system mean that three fetched papers call a methodological floor
([BP91], not obtained — reached through [MCP05 p.9], [LS11 p.7], [CH20 p.5]), ~20% flat
across a 150-fold productivity range [MCP05 p.1, p.9], and 37–41% in oligotrophic water
[LS11 p.1; CH20 p.1]. It does not vary with cell size (isometric, slope 0.95) or growth
phase [LS13 p.1], so a flat fraction of intake is literature-faithful in those respects.
**The screen runs at 0.15**, with a bracket of 0.05 / 0.13 / 0.20 / 0.37 held in reserve if
the first reading is ambiguous. Three caveats carried from the review: (i) the reference
point is worse than the proposal said — the measured producer→herbivore step is 13% [ED21
p.14], and Pauly & Christensen's 10% is a TL2→4 figure [PC95 p.3], so 1% is against 13, not
10; (ii) real release is highest where light is lowest, at the base of the euphotic layer
[MCP05 p.8–9], so a fraction-of-intake rule under-delivers in the dim band; the faithful
alternative is a per-biomass release independent of instantaneous photosynthesis, which
would have to be floored at what the reserve holds — a world-rule choice the owner has not
yet made, and the screen starts with fraction-of-intake because the mutants are born where
producers earn; (iii) exudation alone does not close bacterial demand even in the ocean
(5–14× short, [LS11 p.8]; [CH20 p.1]), so a failed screen at 0.15 is consistent with the
literature rather than a refutation of the mechanism. Design impact recorded in the
review's round table; DESIGN.md's changelog takes the rule when it is built.

*Absorbed from `fable-propose-detritus-flux.md` (2026-09-03), deleted on this ruling.*

### D071

**Matter at depth — the matter sink decoupled from the detritus sink, because the
population plateau was the matter cap and the deep was dry** · 2026-09-04 (ruled; screen
first)

Round 18 (logbook/0054) met D063 4 of 5 and its failing seed named the next constraint:
the last stomachs sat at −15 m in 13.7 J/m³, earning +0.08 W each with 537 J in reserve —
four times a child's price — and had no children. Refused, not starved. Every mature world
in the round refused 100,000–290,000 conceptions per 100-s window for want of matter, and
the largest clade's size at the end tracked the free matter at depth across the five seeds.
The arithmetic: 6,000 units of matter in the world (1/m³ over 100 m² × 60 m, conserved
since D048), ~5,500 locked in bodies at maturity, ~3.5 per child from the parent's own
layer, so about 1,900 bodies can exist — which is the 1,700–1,850 plateau every round since
D065 has shown and read as carrying capacity. Producers alone fill it; a stomach at −15 m
conceives only when a corpse's matter sinks past it, and round 13 (D067's marine snow)
slowed the matter sink from 0.02 to 0.002 m/s together with the detritus sink, so matter
released at the producers' layer is re-locked there before it goes down: ~6,500 s to reach
−15 m instead of ~650. The slow detritus sink is what keeps the exudate near the stomachs;
the slow matter sink is what keeps the matter away from them.

**The rule.** `EVOSIM_MATTER_SINK` returns to 0.02 m/s (D048's default) while `EVOSIM_SINK`
stays at 0.002: matter and detritus fall at different speeds, as they may — one is a
mineral currency and the other is organic energy, and nothing in D048 or D067 tied them
beyond convenience. No code; it changes where the matter is, not how much there is, and
conservation stands. **Screened first**, at the 0.02 step on the two matter-starved seeds
(1 and 4), 20,000 s, scored by connected clade (0054's addendum): prediction, `mat deep`
≥ 0.3 units/m³ at t > 10,000 and a clade ≥ 10 at the end in both; adopted into the
reference world only if it holds, and read for the cost — a surface stripped of matter
faster presses on the producers' recruitment at the top.

**Rejected or deferred.** Matter excretion up (`EVOSIM_EXCRETION`): real but small — the
fixed 3 units per body are never excretable under D065's contract, so at most ~15% of the
locked pool can move this way; a second lever. The fixed matter price down or the initial
matter up: both change the producer world wholesale (a population near 3,800) and the
second is a permanent change to the world's size; not for this question. A price paid from
where the food is rather than where the body sits: wrong — matter is where it is.
**Deferred as a future experiment (owner, 2026-09-04): a vent that adds matter, paired
with burial at the floor** — the ocean's open budget, the world's size set by two flows
rather than a conserved stock; it would make D067's vent load-bearing for the first time,
needs the burial sink or the population runs to the ceiling, and injects matter at the
plume's base where it rides to the surface and the leaves take it first, so it helps the
deep only where the return flow sinks — which is why this rule comes first regardless. A
vent community living on chemistry is a new cell type and belongs with the cell-type
expansion.

*Absorbed from `fable-propose-matter-at-depth.md` (2026-09-04), deleted on this ruling.*

**Screened the same day (logbook/0055): not adopted.** M1 falsified in both seeds
(minima 0.147 and 0.151 at −54 m against the prediction's 0.3; at the population's own
depth 0.10–0.15 units/m³ in the treatment arms, 0.14 in the control, 0.08 in round 18),
M5 falsified (population inside the wingspan of the control's), M2 short, M3 unreadable
in seed 1 (no stomach population survived the founding at this realisation) and held in
seed 4 (a mutant-rooted clade of 67 against the control's 46). The cap arithmetic above
stands; the lever does not: 90% of the stock is locked in bodies at maturity in every
arm at every sink speed, and the free 10% spread over the column is the ~0.1 units/m³
every layer reads. A sink speed cannot enlarge the free pool. The stomachs share the
leaves' band and lose the contest for each arriving unit; only a rule that changes the
free pool — the fixed price, the initial stock, excretion of the tissue share, or the
vent as a source with burial — can move the refusals, and each grows the producer
population with it. Owner's choice; `EVOSIM_MATTER_SINK` stays at 0.002. *Overtaken the
same day by D072: the contest for each unit was never a lottery.*

### D072

**Conception order — the breeding walk is shuffled each step, because the world bred
oldest-first and a young stomach stood behind every older leaf in its layer** ·
2026-09-04 (agent's fault fix behind a knob, on the owner's "proceed with your
recommendations"; adoption into the reference world is the owner's ruling on the screen)

0055 left the stomachs contesting a free matter pool of a tenth of the stock with the
leaves in their own band. Reading the contest in the code (logbook/0056): `World.Reproduce`
walks the living in list order once per step and each solvent parent draws its child's
matter from its layer before the next is considered; the list is birth-ordered, so in a
starved layer the oldest solvent body takes the matter every step and a younger one
breeds only when everyone older is dead or broke. DESIGN.md specifies no order. By
CLAUDE.md's rule that is a fault — the engine doing what the design did not ask — and it
was measured before it was named: in the reference world's plateau the median parent age
at conception is 3,352–4,536 s with 48–62% of births to bodies past a lifetime, against
376–558 s during growth when matter is not short. That is why round 18's last stomachs,
holding four times a child's price, had no children; and it means every plateau since
D065 has selected producers for outliving the queue rather than for fecundity.

**The rule.** `ConceptionOrder` (`EVOSIM_CONCEPTION_ORDER`): `age` — today's walk, the
default, bit-identical so the record replays; `shuffled` — a fresh uniform permutation of
the living each step from a dedicated seeded stream, so same seed and config replay. Not
a lottery weighted by anything: the design has no basis for weighting, and a uniform
order is the one that adds no rule. Screened first (logbook/0056: seeds 1 and 4 at 0.02
with the fix, a seed-4 control, seed 1's control being 0055's), predictions M1–M5 there.
If M1–M2 hold, the owner is asked to adopt `shuffled` into the reference world and the
goal is confirmed at 0.01 without the queue.

**Rejected.** Fixing it silently as the default — a butterfly across every recorded run
with no way to compare. Weighting the draw by energy or size — a world rule with no
source behind it; if the ecology wants a weighting it can evolve one through brood size
and endowment, which are already traits. Leaving the age order as a deliberate rule —
nothing in the literature the review has read models conception as an age queue, and
the pressure it exerts was never pre-registered or read.

*Proposal absorbed on the owner's delegation; reversible by ruling.*

**Screened the same day (logbook/0056): `shuffled` not adopted.** The queue is gone
under the fix (median parent age 852 s and 2,117 s in the plateau against 4,632 s and
4,318 s in the controls) and the stomachs lose in both seeds: seed 2's line fell from 81
to 7 with no recent births against the control's 120; seed 4's clade 185 against 227.
The age queue was a longevity premium the design never chose and it was the only route
by which an energy advantage became a reproductive one in a matter-bound world; without
it every solvent body has the same fecundity and the small clade drifts out. `age` stays
the default and the reference world's order. What is actually broken — that energy does
not buy matter at the plateau, so §5A stops selecting — is put to the owner in
`fable-propose-matter-economy.md`. *Ruled as D073.*

### D073

**Energy buys matter — scarce matter goes to the parent with the largest energy
reserve, screened against a stock large enough that light binds first** · 2026-09-04
(owner: "proceed with your recommendations", on `fable-propose-matter-economy.md`)

Two screens the same day (logbook/0055, 0056) established that at the plateau matter
binds, nine-tenths of it locked in bodies, and that every solvent body then has the same
fecundity whatever its energy income: a child's matter is refused or granted without
reference to the parent's reserve. The energy economy — §5A, the project's premise —
selects at the plateau only for not starving. The age queue (D072) was the accidental
route from an energy advantage to a reproductive one, and the stomachs' lifeline;
removing it took the stomachs with it.

**The rule (B).** A third conception order, `Reserve`: each step the solvent parents
are walked in descending order of energy surplus above the breeding gate, ties by list
index, so when a layer's matter covers one child the parent with the most energy to
spare takes it. Deterministic, no draw. The world's size is unchanged; the energy books
become the tie-breaker for scarce matter, which is what selection needs. A new rule
with no source — the review has read nothing that allocates a mineral currency by
energy reserve — and close to what the age queue did by accident, with the right
variable. Behind the same knob (`EVOSIM_CONCEPTION_ORDER reserve`); `age` remains the
default until this is adopted.

**The null (A).** `EVOSIM_MATTER_INITIAL` 3 units/m³ (from 1): a stock three times the
size, under the age order, to read whether the population then plateaus below the matter
cap — light binding first, the energy economy deciding who breeds as designed — or
merely at a larger cap. The cost is the world's size and the machine's pace.

**Screened first** (logbook/0057): seeds 2 and 4 at 0.02, 20,000 s, `reserve` and the
3/m³ stock as separate arms, against 0056's `age` controls. Adoption into the reference
world on the owner's ruling over the screen; confirmation at 0.01 under the amended goal
after that. **Rejected:** specifying the age queue as the design's rule (a longevity
premium the ecology never argued for); enlarging the free pool by the fixed price or
excretion (the first changes the world's size, not who wins; the second is already at
the rate that returns the tissue share in seconds).

*Absorbed from `fable-propose-matter-economy.md` (2026-09-04), deleted on this ruling.*

**Screened the same evening (logbook/0057): neither adopted.** `reserve` makes the
stomachs the most solvent bodies in the world (median 218 J above the gate in seed 2
against the control's −95) and they still breed no more than the leaves — clades 22 and
64 against 120 and 227 — because a saturated leaf's reserve is of the same order and the
rule ranks stomachs among the leaves, not above them. The 3/m³ stock binds again at 97%
locked with the population and the stomachs both tripled at the same share, the age
queue back at 44–66%, and the pace a third. The reading that reaches past both is the
owner's: matter is not finite; the world needs an influx and an outflow, and its size
should be a flow. *Ruled as D074.*

### D074

**The open matter budget — an influx and a burial, the world's size a flow, because a
conserved stock locks and stops selecting** · 2026-09-04 (owner: "matter like energy is
not finite. there's a constant influx of both on our planet" · "let us see what happens
when matter does not lock")

Three screens in one day (logbook/0055–0057) said one thing from three sides: the world's
matter is a conserved 6,000 units, 97% of it in bodies at maturity, the count of bodies
is the stock over the price, and once the count is set every solvent body has the same
fecundity whatever it earns. Moving the matter, fixing the age queue, letting energy bid
for it or tripling it changed the arrangement and not the arithmetic. The ocean is not a
stock: nutrients enter by rivers, weathering and dust and leave by burial, and
productivity is set by supply rate. DESIGN.md §5A already treats energy so — light in,
respiration out. Matter takes the same shape.

**The rule.** Two terms, default 0 so the record replays: `MatterInfluxPerSecond`
(`EVOSIM_MATTER_INFLUX`, units/s), deposited each step at the surface of every patch
(`EVOSIM_MATTER_INFLUX_AT surface`, rivers and dust) or at the vent's base in the vent
patch (`vent`, riding D067's upwelling); and `MatterBurialPerSecond`
(`EVOSIM_MATTER_BURIAL`, 1/s), the fraction of each patch's floor-layer free matter
removed from the world each second. The identity `initial + influxed − buried = free +
locked` replaces conservation in the tests; `mat in` / `mat buried` per window in the
report; the header carries both. Detritus is not buried by this rule — the leak's
energy budget is a separate question. Surface first (no other mechanism touched); the
vent shape a second screen if the surface holds.

**Dose.** So that the equilibrium stock is about today's: burial 0.01/s removes ~0.6
units/s at a floor holding ~60 units, so influx 0.6/s holds ~6,000 in steady state and
turns the stock over in ~10,000 s. The ledger cannot forecast a matter flow; the first
arms read the equilibrium and the dose is corrected from them. A second dose at 1.2/s
in the same round reads the world's size as a flow.

**Screened first** (logbook/0058): seeds 2 and 4 at 0.02, 20,000 s, both doses, against
0056's controls; adoption into the reference world on the owner's ruling over the
screen; 0.01 after. **Rejected:** influx without burial (the ceiling within a lifetime);
burial of detritus (changes the energy budget the leak was tuned against); a source that
follows the population (hands the contested resource to whoever already wins).

*Absorbed from `fable-propose-open-matter-budget.md` (2026-09-04), deleted on this ruling.*

### D075

**The path after the open budget — movement that pays first, on the vent's stage, with
the theatre built in parallel; then predation, the cell types, the archive** ·
2026-09-04 (owner: "agreed. lock it in.")

The goal rule's bar was "the minimum state from which we can move on" (D063's
amendment). Once an open-budget world confirms at the fine step under the amended goal,
that bar is met as meant: producers, a breeding second trophic level, and a matter
budget that flows rather than locks. The order after that, ruled now so the record
carries it:

1. **Movement that pays.** The cost side of locomotion has been closed since piece 04;
   the prize side never existed because nothing was anywhere worth going. In the vent
   world (D074's second shape) matter arrives at one place and rises, so a body that
   finds the plume earns children. Needs: the three unread sensor channels wired —
   `Chemical`, `Flow`, `Energy` (DESIGN.md §4.5; four of seven read today) — and a
   scored clause for a lineage whose income depends on where it goes. Fine step only:
   the fast step under-drives muscle (logbook/0059), so a movement round costs 5–10 h
   per seed. Pre-registered as its own round when the perception build lands.
2. **The theatre** (`Evosim.Theatre`, DESIGN.md §6.1): replay, gallery, charts from
   stored genomes. Built in parallel by a separate agent from the start of item 1,
   because it touches nothing the farm runs on and it is the owner's game thread.
3. **Predation** — a consumer cell type eating living tissue; after movement, because
   the design says it needs perception to be worth attempting (§5A's table).
4. **The cell-type expansion**, a vent community living on chemistry among them, with
   the owner's immigration hypothesis screened alongside (D060's assay, scored on
   establishment).
5. **The archive and the islands** — MAP-Elites and the farm's Milestone 4.

Before item 1: round 22 (logbook/0058) is written up, D074's adoption and dose are put
to the owner, the vent shape is screened, and the adopted world is confirmed at 0.01
under D063 as amended. Nothing in this entry changes a world rule; it fixes the order
of the questions.
