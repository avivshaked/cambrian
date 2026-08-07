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
