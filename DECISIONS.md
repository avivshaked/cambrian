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
