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
