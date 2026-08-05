# Evolution Simulator — Design Document

**Status:** Draft 4 — revised against literature, then against a working implementation.
Milestone 1 complete: genomes develop, phenotypes build into articulations, creatures move.
No fluid, no fitness, no search yet.
**Date:** 2026-08-02

A Karl Sims–style evolved-virtual-creatures simulator in Unity. Genomes encode both
**body plan** and **brain**; creatures are grown from a directed graph and evaluated in
physics. Primary goal is something mesmerising to watch; strong secondary goal is a
genuine research instrument with reproducible runs and exportable data.

> **On citations.** Every claim taken from the literature carries a locator of the form
> `[KEY §section, p.N]`, where `p.N` is the **PDF page number**, which maps directly to the
> `### Page N` heading in that source's extracted `source.md`. See §13 for the reference
> list, local package paths, and how to re-open any source.

---

## 0. Changelog — draft 1 → draft 2

Draft 1 was written from first principles before any literature was read. Three papers
have since been read in full. Seven things changed, and one whole failure mode had been
missed.

| Change | Was (draft 1) | Now | Why |
|---|---|---|---|
| **§2 new** | *(absent)* | Premature morphological convergence as the primary risk | [EA23 §1, p.2] — the dominant documented failure mode of this exact problem, entirely omitted before |
| **§8.2** | Uniform selection from filled cells | **Fitness-proportional** selection | [EA23 §3.5.4, p.12; §7, p.27] — improves diversity protection, diversity quality *and* global fitness at once |
| **§8.3** | Single morphological descriptor grid | **Multi-BC**: aligned + unaligned | [EA23 §2.10, p.7] and [K12 §4, p.11] — each descriptor type fails alone, in opposite directions |
| **§5.3** | "Entirely sufficient" per-part drag | Exploitable; champion validation harness added | [U07 §5, p.9] — per-part drag and real hydrodynamics disagreed on *direction of travel* |
| **§4.4** | PD targets vs raw torque (binary) | Third option: torque + mass-scale + smoothing | [K12 §2.2, p.5] — cheaper than PD, suppresses the buzzing that motivated PD |
| **§4.1** | `reflect: bool` | Three reflection flags, one per axis | [K12 §2.1, p.3] — yields 2/4/8 mirrored copies |
| **§11.2** | Vague "GAs find exploits" warning | Concrete four-check rejection checklist | [K12 §2.3, p.7] — a working system's actual anti-exploit machinery |

## 0b. Changelog — draft 2 → draft 3

Two further papers read ([L21] partial, [C18] partial). One open question closed, one
terminology error fixed, and **one correction to draft 2's own reasoning** rather than to
anything in the literature.

| Change | Was (draft 2) | Now | Why |
|---|---|---|---|
| **§12.1 ✅ closed** | Encoding unresolved; CPPN a live threat to the design | **Keep the recursive graph** | [L21 Table 6, p.18] — the CPPN advantage is a *soft-body* result; on rigid bodies, direct/recursive encodings win or tie |
| **§4.1 terminology** | Called a "direct graph" | It is an **indirect / generative** encoding | [L21 §4.2, p.8] classifies Sims as "an indirect representation that supports recursive structures." Recursion + reflection + cumulative transforms *are* the generative machinery — the regularity CPPNs were wanted for is already present |
| **§5.4 ⚠ self-correction** | Crude fluid model costs scientific validity only — "fine for goal #1" | It **also collapses morphological diversity**, which is goal #1's currency | [C18 §4, p.28] — simplified fluid produces "anatomical uniformity", no fish, no squid. Added mass promoted to Milestone 3 |
| **§5.1 evidence** | Water-first argued from reasoning alone | Now has empirical support **and** a stated significance caveat | [C18 §3.3, pp.22–27] — land→water detrimental (p<0.01); water→land beneficial but **not significant** (p>0.05) |
| **§4.3** | MVP operators `{oscillate-wave, sin, sum, sigmoid}` | Adds **`oscillate-saw`** | [C18 §4, p.30] — "non-harmonic actuation routines are of importance in unsteady aquatic locomotion" |
| **§11.2** | Four exploit checks | Adds **self-collision vibration** | [C18 Fig. 13, p.19] — stiff robots "exploit self-collisions resulting in fast vibrations to produce thrust" |
| **§6.2** | Engine choice argued from reasoning | Now has precedent | [L21 Table 2, p.15] — PhysX used by Lessin et al. for rigid-body EVCs; Unity ML by Pathak et al. 2019 |

**Still unread:** [TM01], [CEA07], [CU15], plus the open-access set (Krčah's GECCO'07
reimplementation, Lessin's thesis, Sims' originals, Veenstra & Glette 2020, Cheney et al.
2018, Lehman et al. 2020). Research questions 2 (Sims reproduction) and 5 (controller
representation) remain only partly answered.

## 0c. Changelog — draft 3 → draft 4

Draft 3 was the last version written before any of it existed. Building Milestone 1 and
starting Milestone 2 changed five things. **No new literature was read**; every change below
was forced by an
implementation or by a human looking at the result, and the distinction matters — these are
the corrections a review could not have supplied.

| Change | Was (draft 3) | Now | Why |
|---|---|---|---|
| **§4.2 recursion** | "Cycle traversal decrements a per-node counter; at zero, only `terminalOnly` edges are followed" | Occurrence counts per path, with the exhaustion and terminal rules stated explicitly | Read literally, the old wording made every non-recursive genome develop into a single box. Ambiguous rather than wrong, but not implementable as written |
| **§4.2 reflection** | *(unstated)* | Reflection is meaningful only about the **attachment axis** | Mirroring displaces a point only if it has a component on that axis. Choosing axes independently put 69.7% of random creatures partly inside themselves, while still looking plausible |
| **§4.2 overlap** | "Overlap at joints permitted" | Overlap permitted, **burial** tracked and rejected | A human saw boxes inside boxes within seconds. Per-part fluid forces would make coincident parts collect thrust twice |
| **§11.2** | Five checks, all from the literature | Adds **momentum conservation** and **buried parts** | Both earned in implementation. The first caught an actuation model that manufactured angular momentum from nothing — invisible to every "is it finite and moving" check that preceded it |
| **§11.2** | Momentum conservation measured with self-collision disabled | Measured **both** with and without, and adds a **depenetration velocity cap** | A human asked whether directly-connected boxes can actuate at all. They can — PhysX exempts jointed links — but the question exposed that non-adjacent parts jam, that jamming *paid* (the two least mobile creatures were the two furthest travelled), and that the conservation check had been excused from the one configuration where the leak lived. Contact between a creature's own parts is internal, not external; the original exclusion was reasoned, confident and wrong |

**What the process suggests.** The literature corrected the *design*; running it corrected
the *specification*. Four of the five entries above were found by a person watching creatures
move or asking whether the mechanism could work at all, after the headless test suite had
passed. Milestone 3's visual payoff is not decoration on the schedule — it is an instrument,
and the only one that reports this class of fault.

The fifth entry sharpens that. It came from a question rather than an observation, and the
question was answerable from documentation — the answer to *"can two jointed boxes rotate
against each other"* is yes, by exemption. Answering it and stopping there would have left the
exploit in place. What found the fault was checking a claim that was already known to be true.

---

## 0d. Changelog — draft 4 → draft 5

One change, and it is a change of genre rather than of detail. **No new papers were retrieved**;
one already-held paper was read further ([L21 §13]), which is a review scope change and is
recorded as such in `research/LITERATURE-REVIEW.md`.

| Change | Was (draft 4) | Now | Why |
|---|---|---|---|
| **§5A (new)** | Exogenous fitness: evaluate a creature alone, score it by displacement, let the score drive search | Endogenous selection: energy is a conserved budget, creatures acquire and spend it, reproduce on surplus and die at zero | Nothing is being taught to swim. If swimming is what keeps a creature alive it will appear on its own, and if it does not, the thing that does appear is the more interesting answer |
| **§5.5** | The fitness function | ❌ Superseded; displacement demoted to an observable | Follows from the above |
| **§6.3, §6.4, §8** | Tiling as isolation; throughput in evaluations/second; MAP-Elites as the selector | Tiling as spatial partition; throughput in simulated seconds per wall-clock second; MAP-Elites demoted to an observatory | An ecosystem is one shared world with no episode boundary. MAP-Elites solved a problem that exogenous fitness creates; ecological niches serve that role here |

| **§11.2** | Six checks | Adds **engine damping defaults** and **energy balance** | Both found while building §5A's first measurement. Creatures had been swimming in two fluids since Milestone 1 — §5.2's, and PhysX's `angularDamping`/`jointFriction` defaults, which nobody chose and which removed ~10x more. The energy balance is what caught it; verifying that balance on a two-body system then verified the work measurement itself. See logbook/0008 |

**Timing, and why it cost almost nothing.** This arrived at Milestone 2, before search
(Milestone 4) existed. Everything built to date — genome, development, phenotype builder,
fluid model, effector conditioning, determinism — is unaffected, because the encoding does not
care how selection happens. Proposed after Milestone 4 it would have discarded an island
model, an archive-driven selector, and a throughput target derived from a unit that no longer
applies.

**What did not change:** the priority order. Mesmerising to watch first, research-grade
instrument a strong second. §5A is a bet that a food web is more watchable than a highlight
reel of champions — and it is a bet, recorded as one in `DECISIONS.md` D017.

---

## 1. Target hardware

| | |
|---|---|
| CPU | Intel i9-13900K — 24 cores (8P + 16E) / 32 threads |
| GPU | NVIDIA RTX 4090 (24GB) |
| RAM | 64 GB |
| Disk | ~865 GB free on `D:` |

This workload is **CPU-bound**. The GPU matters only for the presentation layer.

Calibration against the literature: [EA23 §3.4, p.10] ran 5 algorithms × 20 runs × 1000
generations × population 100 ≈ 10⁷ evaluations on GPU voxel physics. [K12 §3.3, p.10]
ran population 300 × 150 generations × 25–30 runs ≈ 1.35×10⁶ evaluations of rigid-body
ODE creatures. The latter is the closer analogue and is comfortably in reach here.

---

## 2. The primary risk: premature morphological convergence

**This is the failure mode that kills projects of this kind, and draft 1 did not mention it.**

### 2.1 The mechanism

Body and brain are co-adapted. A morphological mutation invalidates the controller tuned
to the *old* body, so the offspring performs badly **even when the new body is better**.
Fitness-based selection discards it. Within a few dozen generations the population locks
onto whichever body plans appeared early, and all further progress happens in the
controllers alone — discarding the entire point of co-evolution.

[EA23 §1, p.2] states it directly: *"the evolution of morphology stagnates early on,
resulting in a subset of very similar morphologies... the potential benefits of
co-evolving both aspects are lost, and optimization occurs only in the controllers."*
Same page attributes the diagnosis to Lipson et al. 2016, Kriegman et al. 2018 and
Joachimczak et al. 2016.

It is **not** a tuning problem. It is structural and must be designed against.

### 2.2 Evidence that mitigation is worth the complexity

[EA23 Table 5, p.24] — Condorcet pairwise wins, 5 algorithms × 20 runs:

| | ME | MNSLC | NSLC | QN | SO |
|---|---|---|---|---|---|
| **ME** (MAP-Elites) | — | 5 | 6 | 9 | 11 |
| **MNSLC** (multi-BC NSLC) | **6** | — | 9 | 7 | 10 |
| **NSLC** | 5 | 2 | — | 6 | 9 |
| **QN** (fitness+novelty MOEA) | 2 | 4 | 5 | — | 10 |
| **SO** (no diversity protection) | 0 | 1 | 2 | 0 | — |

The plain single-objective baseline **loses to everything** — zero pairwise wins against
both MAP-Elites and QN. Some diversity mechanism is mandatory, not optional.

### 2.3 Mitigations in the literature

Survey of strategies at [EA23 §1, p.2] and [EA23 §2, pp.3–8]:

| Strategy | Source | Adopted? |
|---|---|---|
| Morphological innovation protection — shield recent morphological changes from selection | Cheney et al. 2018, via [EA23 §1, p.2] | **Yes** — §8.4 |
| QD with morphological descriptors | Nordmoen et al. 2020, via [EA23 §2.4, p.4] | **Yes** — §8 |
| Two-phase: co-evolve, then controller-only refinement | Nygaard et al. 2017, via [EA23 §1, p.2] | Deferred — fallback if §8 underperforms |
| Speciation + fitness sharing (NEAT-style) | [K12 §2.5, p.9] | No — archive cells serve a similar role |
| Multi-BC (aligned + unaligned) | [EA23 §2.10, p.7] | **Yes** — §8.3 |

**A structural observation worth stating:** MAP-Elites' archive *is itself* morphological
innovation protection, given morphological descriptors. A mutant with a novel body lands
in a **different cell** and competes only against others of its own body class, never
against the globally-fittest incumbent — exactly the protection Cheney et al. add
explicitly. A strong reason to prefer a morphology-descriptor archive over a plain GA,
independent of the gallery benefit.

---

## 3. Guiding principles

1. **The farm and the theatre are separate programs.** Evaluation is headless, ugly and
   fast; presentation is slow, beautiful, and reads stored genomes.
2. **The simulation core is engine-agnostic** — plain C#, no `UnityEngine`, unit-testable.
3. **Diversity is a first-class objective**, per §2 — not merely because a varied gallery
   looks nicer.
4. **Everything reproducible from `genome + seed + configHash`.**
5. **Milestones gated on visible payoff.**

---

## 4. Genotype

### 4.1 Morphology graph

A directed graph, possibly cyclic. Cycles make the encoding compact: a self-loop with
`recursiveLimit = 5` yields a five-segment spine from one node. Structure follows [S94]
and the working reimplementation described at [K12 §2.1, pp.2–3].

> **Terminology — this is an *indirect* encoding, not a direct one.** Draft 2 called this
> a "direct graph," which is wrong and matters. [L21 §4.2, p.8] classifies Sims as *"an
> indirect representation that supports recursive structures."* Recursion, reflection and
> cumulative subtree transforms **are** generative machinery: a small genotype unfolds
> into a much larger phenotype, and regularity comes for free. The argument for switching
> to CPPNs was that they buy regularity and symmetry — but this encoding already has that
> mechanism. See §12.1.

```
MorphNode
  dimensions      : float3      // box half-extents
  jointType       : enum        // Fixed(0 DOF) | Hinge(1) | Twist(1)
                                //      | HingeTwist(2) | TwistHinge(2)
                                //      | Universal(2) | Spherical(3)
  jointLimits     : float2[]    // min/max per DOF
  recursiveLimit  : int         // times this node may recur within a cycle
  neurons         : NeuronDef[] // local brain, duplicated with the node (§4.3)

MorphEdge
  child           : nodeIndex
  parentAnchor    : float3      // attachment point on parent surface
  childAnchor     : float3      // attachment point on child surface
  orientation     : quaternion
  scale           : float3      // applied cumulatively to whole child subtree
  reflect         : bool3       // ← CHANGED: one flag per major axis
  terminalOnly    : bool        // expand only when recursion is exhausted
```

**Joint type list taken verbatim from [K12 §2.1, p.3]** — a working system rather than a
guess. Joint type is itself mutable, with limits resampled to the new DOF count.

**`reflect` is now three flags, not one.** [K12 §2.1, p.3]: *"if one, two or three
reflection flags are enabled, two, four or eight mirrored copies of a child node are
created in the phenotype graph."* Reflection is the sole source of bilateral symmetry,
and symmetric creatures read as *organisms* rather than debris — [K12 Fig. 1, p.4] shows
symmetry in three of four evolved examples, captioned *"Several evolved robots exhibit
symmetry (1a, 1b, 1d) and segmentation (1c)."* Widening one boolean to three costs nothing.

**`terminalOnly`** gives differentiated extremities — per [K12 §2.1, p.3], it *"can be
used to represent structures appearing at the end of chains or repeating units."*

### 4.2 Development (genotype → phenotype)

Depth-first traversal from the root, emitting parts in **pre-order** so a part's parent
always precedes it — an articulation must be assembled parent-first, so this makes a single
forward pass over the part list correct by construction.

**Recursion, stated precisely.** Each node carries `recursiveLimit`, the number of times it
may occur along one root-to-leaf path. Traversal keeps a per-node occurrence count for the
current path, and:

- a **non-terminal** edge into node *c* is followed while `occurrences[c] < recursiveLimit[c]`;
- a node's recursion is **spent** when no non-terminal edge from it can still be followed;
- a **`terminalOnly`** edge is followed only once its source node's recursion is spent.

So a self-loop with `recursiveLimit = 5` yields a five-segment spine, and a `terminalOnly`
edge attaches one differentiated extremity at the tip of that chain rather than one per
segment.

> Draft 3 said only *"cycle traversal decrements a per-node counter; at zero, only
> `terminalOnly` edges are followed."* Read literally that makes a node with
> `recursiveLimit = 1` spent on first entry, so an ordinary non-recursive genome expands no
> edges at all and every creature is a single box. The wording above is what was implemented
> and tested; it is a clarification of intent, not a change of behaviour.

All geometric transforms (scale, rotation, reflection) are **cumulative down the subtree** —
[K12 §2.1, p.3]: *"they are applied to the entire subtree of the phenotype graph during its
construction."* Worked example at [K12 Fig. 4, p.6].

**Reflection is only meaningful about the axis the child is attached along.** Mirroring
displaces a point only if that point has a component on the mirrored axis, so a child
attached to the parent's +Y face and mirrored about X lands exactly on top of itself: two
parts occupying one volume, rather than a bilateral pair. This is not a constraint the
genome enforces — mutation may set any flag — but generators and mutation operators should
prefer the attachment axis, and §11.2 carries the check for when they do not.

Guard rails:
- Hard cap on total parts (proposed **16**) and tree depth (proposed **8**).
- **Minimum part volume**, enforced pre-simulation — [K12 §2.3, p.7]: *"the volume of each
  body part must be larger than the specified threshold as extremely small body parts
  cause instability in the physical engine."*
- Overlap at joints permitted. Sims allowed it; enforcing non-overlap kills too many
  viable genomes. **But *burial* is different from overlap:** a part whose centre lies
  inside another part reads as physically impossible, and once fluid forces are computed
  per part it also collects drag and thrust twice for one body's worth of volume. Tracked
  as a distinct measure and checked in §11.2; no single-edge rule can predict it, because
  whether a child lands on top of its own grandparent depends on the path taken to reach
  the node.

The phenotype is always a **tree**, mapping cleanly onto a PhysX articulation.

### 4.3 Brain graph

Neurons live **inside morph nodes**, so recursion duplicates a segment's neurons along
with the segment — producing a chain of identical local controllers, structurally a
central pattern generator. [K12 §2.2, p.3] uses the same arrangement: *"Each body part
contains a local neuro-controller (an artificial neural network), as well as a local
sensor and effector."*

Input references restricted to: a sensor on the owning part, another neuron in the same
node, a neuron in the parent or child node, a global-brain neuron, or a constant. This
restriction preserves the duplication semantics.

**Operator set** (Sims' set, lightly trimmed):

| Category | Ops |
|---|---|
| Arithmetic | `sum`, `product`, `divide`, `abs`, `min`, `max` |
| Comparison | `greater-than`, `sign-of`, `if`, `interpolate` |
| Waveform | `sin`, `cos`, `oscillate-wave`, `oscillate-saw` |
| Transfer | `sigmoid`, `sum-threshold` |
| Temporal | `integrate`, `differentiate`, `smooth`, `memory` |

**Staging:** the MVP restricts the initial population to
`{oscillate-wave, oscillate-saw, sin, sum, sigmoid}` with self-only connections — a pure
CPG. Directly supported by [K12 §2.2, p.3]: *"Apart from standard sigmoidal transfer
function, oscillatory transfer function was used to enable faster discovery of efficient
swimming strategies."* This is a *population constraint, not a separate system* — no code
is discarded when it lifts.

**`oscillate-saw` is in the MVP set deliberately.** [C18 §4, p.30] warns that purely
harmonic actuation is a real limitation: aquatic organisms use "swimming cycles where
impulsive thrusting phases are associated with ramp down, recovering ones, which helps
inducing non-symmetric inertial effects which result in a positive net thrust," concluding
that "non-harmonic actuation routines are of importance in unsteady aquatic locomotion,
and should be taken into account." A sawtooth has the asymmetric duty cycle a sine cannot
express, and it costs nothing to include from the start.

### 4.4 Sensors and effectors

**Sensors** (per part, normalised to ≈[-1, 1]): joint angle per DOF; joint angular
velocity per DOF; contact; orientation vs world up; **damage**; photosensor triple
(Milestone 6). [K12 §2.2, p.4] used only joint-angle sensors — *"A sensor in each body
part is measuring current angle of each degree of freedom of a joint."*

**Damage is readable on every part, not only on links.** Once creatures eat each other
(§5A.3), being bitten is the most consequential thing that happens to a body cell, and a
cell that cannot report it leaves the creature unable to distinguish a good photosynthetic
pose from being slowly eaten in one. It reads as the fraction of the part's own stored
energy taken over the last step, so it is scale-free and does not need a separate
normalisation constant.

Contact moves forward with it. Draft 3 scoped contact to terrain and Milestone 5; §5A makes
it aquatic, because contact is how a consumer cell finds tissue to bite. Both channels are
needed as soon as predation is, which is Milestone 3.

**No relay mechanism is added for damage to reach the rest of the creature.** A neuron on
the bitten part is already readable by its neighbours through `ParentNode` and `ChildNode`
inputs, so a hit propagates one node per step on machinery that exists. The latency is a
feature — it is what a conduction delay looks like, and it is bounded by body length. The
alternative, an input kind naming an absolute part, would break under recursion for the
reason §4.3 gives.

**Which channels a part may read is not restricted by cell type.** The joint channels read
zero on anything rigid, which achieves what a restriction would without making a genome's
legality depend on a mutation elsewhere in it. What limits perception is cost, not
permission: a sensor is only useful through a neuron, neurons are billed per step (§5A.2),
and a creature that senses everything everywhere starves.

**Effectors:** one per joint DOF. Draft 1 posed this as binary; [K12 §2.2, p.5] supplies
a third option that is cheaper than PD control and empirically works:

| Scheme | Mechanism | Trade-off |
|---|---|---|
| Raw torque | output → torque directly | Faithful to [S94]; buzzing high-frequency motion the GA loves and viewers dislike |
| PD angle target | output → target angle, PD-controlled, torque ceiling | Smooth and organic; extra tuning and state |
| **Torque + scale + smooth** ← *recommended* | clamp to [-1,1]; scale by mass of the **smaller** of the two connected parts; average over previous **10** values; apply as torque | [K12 §2.2, p.5]: *"eliminates sudden large forces and also improves stability of the simulation"* |

Mass-scaling matters independently — [K12 §2.2, p.5] notes it *"limits the maximum size
of a force to some reasonable value,"* preventing evolution from discovering arbitrarily
powerful tiny motors. The 10-step moving average suppresses the buzzing that motivated PD
control, at a fraction of the complexity.

### 4.5 Mutation and variation

Perturb scalars (Gaussian); duplicate a morph node; add/remove morph edge; add/remove
neuron; rewire neuron input (respecting §4.3); change joint type; change neuron op;
toggle any `reflect` flag or `terminalOnly`; change `recursiveLimit`; change cell type
(rare, §5A.3); perturb brood size and offspring endowment (§5A.6); and **graft** —
attach a subgraph from genome B at a random edge of genome A.

**There is no remove-node operator, and that is deliberate.** A node enters small — a
duplicate arrives just above the extinction threshold rather than at its source's size — and
leaves by shrinking below it. Nothing else deletes a node.

Three things follow, and the third is the reason:

1. **Duplication is nearly neutral on the birth it happens.** A new part too small to change
   much can then grow if it turns out to be worth something. Arriving full-size made every
   duplication a large jump, and a large jump in a co-adapted body is almost always worse than
   what it replaced (§2).
2. **Genome size is emergent, not declared.** It settles near 39 nodes over a 100,000-birth
   lineage without any rate saying so — the balance of duplication against drift across the
   threshold.
3. **Removal is filtered by selection instead of blind.** A per-node deletion chance hits
   useful and useless nodes at the same rate, so selection must keep re-winning structure it
   has already won; and the rate's magnitude silently decides how large a genome may be, which
   is not mutation's decision to make. Here a node vanishes only by shrinking, and shrinking is
   something selection can prevent: a node doing work is held large, one doing nothing drifts
   out. **Extinction reaches exactly the nodes nothing is holding up** — which is also what
   makes genetic bloat self-clearing, since unexpressed nodes are unexpressed *because* nothing
   selects on them.

The threshold is not a new invented constant: development already refuses to grow a part
below `MinPartVolume`, and a node shrunk past the point where it would produce a viable part
is the natural definition of extinct.

⚠ Size is perturbed proportionally, so log-size random-walks and the threshold is an
*absorbing* barrier — with no selection at all, every node is eventually absorbed. That is
intended rather than a flaw, and it is why duplication still has a rate. The measured history
of getting here is in the changelog: per-birth add/remove drifted to **847 nodes over 100,000
births** and took the replay cost quadratic (32 s); per-node removal fixed the size but
imposed a restoring force that fought selection; this fixes it without imposing one.

Grafting is Sims' crossover. Plain parameter-vector crossover is meaningless on a
variable-topology graph. (Note [K12 §2.5, p.8] instead used NEAT **historical markings**
to align variable-topology genomes for crossover — a more principled alternative worth
considering if grafting proves too destructive.)

---

## 5. Environment: water first

### 5.1 Why water

No ground-contact instability, no falling over, a trivial force model, gaits emerge
readily. On land the first thing every GA finds is that the best "locomotion" is to build
a tall tower and fall over. [K12 §4, p.11] evolved swimmers reaching **108.32 m in 60 s**
with a simple model — water is empirically a low-friction start.

**[C18 §3.3, pp.22–27] tested the ordering directly**, and the result supports this
milestone plan:

| Transition | Effect |
|---|---|
| land → water | **Detrimental** to swimming evolution (p < 0.01) |
| water → land | **Not detrimental**; trends beneficial, and dominates the *entire* energy–performance pareto front |

Their explanation for why water-first helps matches the bootstrapping argument above —
[C18 §3.3.2, p.24]: "it may be easier to achieve a smoother fitness gradient in water with
respect to land. Especially at the beginning of the run, it may be easier to achieve
robots that **do not move at all on land, not being able to overcome static friction**. In
water this does not happen, as robots are free to move and may be able to generate at
least some propulsive action."

**State the strength honestly:** the water → land benefit **did not reach statistical
significance** (p > 0.05). [C18 §3.3.2, p.24] attributes this to "the reduced number of
repetitions and generations, as well as a weak evolutionary algorithm." The *asymmetry*
between the two directions is demonstrated; the benefit of water-first is suggestive
only. It is enough to justify the milestone ordering, not enough to claim as a result.

A bonus for the theatre (§10, Milestone 7): [C18 Fig. 22, p.27] documents **exaptation**
across the transition — "appendages that were useful in water are not completely lost when
moving onto land, but are instead repurposed... tentacles are shortened, and become legs
to support quadrupedal locomotion." A lineage view showing a tentacle becoming a leg is
exactly the kind of thing worth building the gallery around.

### 5.2 The model

Per part, each fixed step:

```
drag         F = -0.5 * rho * Cd * A_effective * |v| * v
angular drag T = -k * |w| * w
buoyancy       neutral — cancels gravity exactly
```

Matches [K12 §2.1, p.3]: *"Water environment was simulated by applying a drag force
opposing the movement of each body part and disabling gravity."*

`A_effective` is cross-sectional area **projected onto the velocity direction**. That
projection makes paddling work — a flat part moving broadside must generate far more
thrust than the same part edge-on. Get it wrong and oscillating limbs produce no net
thrust and nothing ever swims. Three lines of code; decides whether the project works.

### 5.3 ⚠ This model is exploitable — and has already been exploited in print

**The most important correction in draft 2.**

[U07 §2, p.2] compared exactly this scheme — he calls it a *"fixed frame reaction force
model"*, summing local reaction forces per part — against particle-based hydrodynamics
(Moving Particle Semi-implicit, [U07 §3, p.4]), on an evolved *Anomalocaris*.

**The two models disagreed on direction of travel.** [U07 §3, p.5]: under hydrodynamics
*"Anomalocaris moves in a left direction. This result is the opposite result of the fixed
frame reaction model shown in Fig.2."* Not a magnitude error — a sign flip. Conclusion at
[U07 §5, p.9]: *"Reaction force model neglects collaborating motion of fluid, which brings
serious error in predicting swimming motion of creature in water."*

Worse: **the GA found that gait precisely because the model was wrong.** [U07 §2, p.3]:
*"This waving pattern is unusual, and we have never observed such motion in natural
phenomena."* Physics exploitation (§11.2) occurring in published work, in the exact
environment proposed here.

Second disagreement, [U07 §3, p.5]: synchronous "boat-race" rowing scored high under
per-part drag but under hydrodynamics *"produces large disturbance of surrounding water.
Furthermore, the swimming speed is comparatively slow."*

**Scope limits of [U07], stated fairly:** 2D only ([U07 §3, p.5]: *"Our simulation is
performed in a two-dimensional space"*), a single morphology, prescribed sinusoidal
kinematics rather than evolved in-the-loop ([U07 §3, p.5]), Re ≈ 0.25×10⁵ ([U07 Appendix,
p.10]). A narrow study — but the mechanism generalises, and it bites hardest where a
creature has **many small independent paddling surfaces**, exactly what a recursive graph
encoding likes to produce.

### 5.4 Response: validate champions, don't slow the search

> **⚠ Correction to draft 2's own reasoning.** Draft 2 concluded that simple drag was
> "fine for goal #1, not fine for goal #2" — i.e. that the cost of a crude fluid model was
> purely scientific. **That is wrong, and [C18] says so directly.** The simplification also
> costs *morphological variety*, which is goal #1's entire currency. See below.

**[C18] uses precisely this model** — [C18 §2.2, p.5] a "simplified mesh-based quadratic
drag model", per-facet drag using the facet's **normal** speed ("achieved by projecting
the speed of the corresponding voxel along the facet's normal"), C_d = 1.5, with neutral
buoyancy implemented by "simply setting the gravity acceleration to zero." That is §5.2
of this document, independently arrived at. Reassuring — and it means [C18]'s reported
limitations apply here directly.

[C18 §2.2, p.5] names exactly what is missing: "**inertial and viscous fluid terms**" — the
former being "reactive forces due to the acceleration of a body (or part of it) in a
fluid" (added mass), the latter vortex shedding.

**And the consequence is not only physical inaccuracy** — [C18 §4, p.28]:

> "the approximations adopted prevent the model from capturing the dynamics associated
> with vortex formation, thus **precluding the evolution of fish-like creatures**... Having
> neglected added-mass contributions, pulsed-jetting modes cannot be successfully
> predicted, thus **overlooking squid-like creatures**. The outcome consists of organisms
> vaguely resembling medusoids and **morphologically similar among themselves**... The
> **anatomical uniformity of the evolved morphologies is due to the overly-simplistic
> nature of the fluid-body interaction** which constrains the degree of variability of the
> modes of locomotion and, as a consequence, of the emerging morphologies."

A crude fluid model doesn't merely produce fictitious physics. **It collapses the
morphological diversity of what evolves** — no fish, no squid, a gallery of similar
medusoids. That directly attacks the mesmerising goal *and* the MAP-Elites coverage
metric. Added mass is therefore promoted from "nice refinement" to **the highest-value
single improvement to the fluid model**.

Response, in priority order:

1. **Add an added-mass term — do this at Milestone 3, not later.** Force proportional to
   part acceleration, representing fluid accelerated with the body. It is the cheapest
   approximation of the "collaborating motion of fluid" [U07 §5, p.9] identifies as
   missing, it is the term [C18 §4, p.28] blames for the absence of jetting creatures, and
   it costs roughly one extra term per part per step.
2. **Keep quadratic drag in the search loop.** [K12 §4, p.11] shows it produces good
   swimmers and [C18] a wide behavioural range; full CFD in-loop is unaffordable.
3. **Support non-harmonic actuation.** [C18 §4, p.30]: aquatic organisms "rely on swimming
   cycles where impulsive thrusting phases are associated with ramp down, recovering ones,
   which helps inducing non-symmetric inertial effects which result in a positive net
   thrust... **non-harmonic actuation routines are of importance** in unsteady aquatic
   locomotion." Pure sinusoids cannot express this — see §4.3.
4. **Build a validation harness (Milestone 7).** Re-simulate archive champions under a
   higher-fidelity fluid model; record whether displacement agrees in **sign and
   magnitude**. Disagreement is a first-class logged result, surfaced in the theatre UI.
5. **Penalise the exploit signature** — high-frequency, low-amplitude, many-surface
   thrashing, which the §11.2 oscillation detector already targets.

**Revised honest framing:** simple drag produces swimming that looks plausible, is
physically fictitious, **and is less morphologically varied than it should be**. The third
of those is the one that hurts most here, and added mass is the cheapest thing that
addresses it.

### 5.5 Fitness (water) — ❌ superseded by §5A

> **Superseded, kept for the record.** §5A replaces exogenous fitness with an energy economy:
> nothing is scored, and creatures persist by staying solvent. Displacement survives as an
> *observable* — it is still how §5A.7 detects the "efficient nothing" failure, and still what
> the §11.2 checks are measured against — but it no longer selects anything.
>
> The evaluation-window reasoning below stays relevant: an ecosystem has no episode boundary,
> so the question changes from "how long is an evaluation" to "how long before a behaviour is
> distinguishable from a transient", which is the same question with the answer no longer
> bounded by a budget.

Horizontal displacement of centre of mass, discarding ~1 s of settling. [K12 §3.1, p.9]
used *"the distance between the final and starting positions of the robot"* over 60 s.

**Evaluation window is an open parameter.** Draft 1 proposed 15 s at 1/120 s = 1800 steps.
[K12 §2.3, p.7] used **30 steps/simulated second over 60 s** — coincidentally also 1800
steps, very differently distributed. Finer steps resist solver exploitation; longer
windows separate a real gait from a lucky transient. Note [K12] still needed an explicit
oscillation detector at 30 Hz. Proposed compromise for Milestone 2 measurement:
**1/100 s over 20–30 s**, treated as measured rather than guessed.

---

## 5A. Ecosystem: energy, food webs and endogenous selection

**Status: specification, not yet implemented.** Nothing in this section has been built or
measured. It supersedes §5.5, and changes the role of §6.3, §6.4 and §8 — see §5A.8.

Lettered rather than renumbered because `§4.2`-style locators appear throughout this
document, in `DECISIONS.md`, and in code comments, and renumbering would silently invalidate
all of them.

### 5A.0 What changes, and why

Everything above §5.5 assumes **exogenous** selection: a creature is evaluated alone, scored
by a function we wrote, and the score drives search. §5.5 is that function.

This section replaces it with **endogenous** selection. Energy is a real, conserved budget.
Creatures acquire it, spend it on everything they do, reproduce when they have a surplus, and
die when they run out. Nothing is scored. Swimming well is not rewarded — it is one of
several ways to stay solvent, and it wins only where it happens to be the cheapest route to
food.

Two systems in the corpus work this way, both reported in [L21 §13]. PolyWorld, p.14:
*"Actions spend energy, so the creatures need to hunt for food."* Ventrella's Gene Pool
swimbots, pp.15–16: *"swimbots spend energy when they move. Once its energy level drops below
a certain threshold, a swimbot starts looking for food. A swimbot with zero energy dies."*

> ⚠ **Provenance.** §13.2 records [L21] as read only for §4.1–4.2 and two tables. §13 of that
> paper was read while drafting this section, which is a scope change to the review under
> `research/LITERATURE-REVIEW.md` §3.5 and needs a round entry there rather than a quiet
> citation here.

**What this does not change.** §4.1 genome, §4.2 development, §4.3 brain graph, §5.1–5.4
fluid model, §6.1 assemblies, §7 reproducibility, §11.2 exploit checks. The encoding does not
care how selection happens. Everything built through Milestone 2 remains correct.

### 5A.1 Cell types

Energy acquisition is a property of a **part**, not of a creature. A species is therefore a
distribution of part types over a body plan, and speciation is a change in that distribution —
no separate species, niche or strategy concept is required, because §4.1's graph already
encodes exactly this.

Implementation: one new field on `MorphNode`, and mutation operators (§4.5) that can change
it.

| Type | Gains energy from | Notes |
|---|---|---|
| **Photosynthetic** | Light, ∝ exposed area × irradiance at its depth | Reliable, free, and favours flat spread-out bodies — which cost mobility |
| **Absorptive** | Nutrient particles it intersects | Nutrients drift on the current, so this rewards being *where* food is |
| **Consumer** | Tissue on contact — living or dead | See §5A.3. Works on carrion without perception, which is what makes it survivable early |
| **Structural** | Nothing | Cheapest upkeep, but **not free** — see §5A.2 |

**Why `Structural` exists.** Without a part type permitted to have no energy function, every
part must pay for itself and a tail can never evolve: a fin pays only indirectly, through
better swimming, so it is a net loss on the step it appears. Structural parts are what make
fins, levers and streamlining reachable — the difference between creatures with bodies and
creatures that are clumps of stomachs.

**Why its upkeep is nonzero.** A part costing nothing is a free lever: arbitrarily large
bodies, arbitrarily long limbs, no pressure to be economical. This is the same class of fault
as the free momentum in §11.2 — a resource with no price gets spent without limit. *(Author's
inference; no source in the corpus addresses zero-cost structure.)*

### 5A.2 The energy economy

**Sunlight is the only primary input.** Nutrients are recycled dead matter; predation
transfers between creatures; death returns tissue to the nutrient pool. Metabolism is the only
outflow, and it leaves the system.

This makes total world energy **auditable**: sun in, metabolism out, everything else
conserved. It is the economic counterpart of the momentum invariant in §11.2, and it matters
for the same reason. Under exogenous fitness a physics exploit produces a bad evaluation that
gets discarded. Here there is nothing to discard into — **free energy from the solver is free
food**, and a creature that extracts it eats without foraging while its descendants inherit
the trick. The depenetration leak found at Milestone 2 (0.254 m/s of centre-of-mass velocity
from purely internal forces) would have been a population-wide takeover rather than one bad
score. A global energy audit detects that class of fault without tuning, which is the property
that makes conservation laws worth preferring.

**Expenditure, per step:**

| Term | Scales with | Purpose |
|---|---|---|
| **Basal metabolism** | Part volume × type multiplier | Makes stasis cost something. Without it, doing nothing is free and doing nothing wins |
| **Neural** | Neuron count + connection count | Stops brains bloating without a fitness penalty |
| **Mechanical work** | ∫\|τ·ω\| dt over all DOF | The physically honest actuation cost; we already apply τ and the bodies know ω |

The neural term is the one the user asked for and the one with no precedent in the corpus.
[C18 §2.4, p.8] minimises *"the percentage of actuated voxels (energy)"* — a proxy, not work —
and [CU15] measures energy in N·m·rad only as a **behavioural descriptor**, never as a cost.
No paper we hold implements a work integral or a per-neuron charge. *(Both terms are therefore
this design's own, and must not be presented as literature-backed.)*

> **The knob that decides everything.** The ratio of basal metabolism to peak photosynthesis.
> If sunlight alone covers upkeep anywhere in the world, nothing there ever has to move, and
> the world becomes a photosynthetic mat. It has to not quite cover it.

### 5A.3 Feeding, and where herbivores come from

A `Consumer` part gains energy on contact with tissue. Yield depends on **what it touches**:

| Target | Yield | Consequence |
|---|---|---|
| Dead tissue (carrion, sinking) | High, no resistance | Scavenging works while drifting blind — no perception needed |
| Living `Photosynthetic` tissue | Moderate | Grazing. Viable as soon as photosynthesisers are common |
| Living `Consumer` tissue | Low, and contested | Predation proper. Needs perception to be worth attempting |

Herbivore and carnivore are therefore **behavioural outcomes of what a creature ends up
eating**, not separate body types — which is why they can appear in that order without any
morphological innovation between them.

**The predator valley, and two bridges across it.** A `Consumer` part costs upkeep from the
mutation that creates it and pays nothing until perception, directed movement and prey density
all exist together. Populations do not cross valleys that wide, and a lineage that loses the
part may never recover it. Two mechanisms keep it reachable:

1. **Carrion.** A `Consumer` that feeds on dead tissue has a payoff from the first generation,
   so the part is not purged while it waits for perception. Detritivore → scavenger → predator
   is a gradient rather than a leap.
2. **Cell-type mutation.** A rare mutation converting one part type into another means the
   trait need not be conserved continuously — it can be rediscovered. This is
   **density-dependent and therefore self-timing**: a `Photosynthetic → Consumer` mutation is
   worthless in an empty world and immediately profitable in one full of plants, so it fires
   when the food web is ready for it rather than when we schedule it.

Attack and defence — whether a target resists, at what cost, and whether armour or toxicity
are expressible — are **deliberately unspecified**. They are the next layer and should be
designed against observed behaviour, not guessed now.

### 5A.4 Environment: currents, light, depth

**Currents.** A procedural divergence-free field (curl noise), evolving slowly. It enters the
existing model at one point: `FluidModel.BoxDrag` already takes a velocity, so passing
`bodyVelocity − currentVelocity` gives real advection for the price of a noise lookup. Drifting
stops being free, and nutrients — being small and drag-dominated — are carried much further by
it than creatures are.

**Light and depth.** Irradiance falls off with depth; dead matter sinks. This is the cheapest
available source of **spatial heterogeneity**, and heterogeneity is what stops one strategy
winning everywhere: the surface is the photosynthetic niche, the depths are the detritus
niche, and neither dominates. Two trophic modes maintained by geometry rather than by tuning.

**Light cycle.** A diurnal cycle over irradiance. Combined with depth and visual predators,
this gives **diel vertical migration** — rising at night, sinking by day, trading light against
being seen — a genuine reason to emerge. Real plankton do this. It is the single most
watchable outcome this design could produce, and it is legible to anyone who knows the
biology. *(Stated as a target, not a prediction.)*

### 5A.5 Colour and perception

Part colour is an evolvable genome field with no physical cost. It is inert until creatures
can see, and §4.4 already specifies a **photosensor triple** on parts, scheduled at
Milestone 6. Once perception is closed-loop, colour becomes a signal in a world where things
look at each other:

- **Camouflage** — matching the water or the nutrient field
- **Warning colouration**, and mimicry of it, if some targets are costly to attack
- **Foraging as a visual task**, if nutrients are coloured — the brain has to find food rather
  than being handed it
- **Display**, if reproduction is in-world

The light cycle then does ecological work rather than decorative work: at night the visual
channel narrows and the balance between sight-based hunting and everything else shifts on a
cycle.

Nothing guarantees signalling emerges — it requires a payoff structure that supports it. But
heritable, visible colour costs a genome field, and if it goes unused it is still a lineage
marker that makes speciation watchable.

### 5A.6 Reproduction and death

**Death** at zero energy. Tissue returns to the nutrient pool at its location and sinks,
which is what closes the loop in §5A.2.

**Reproduction** in-world, paying the offspring's starting energy out of the parent's.
**Asexual — a mutated copy, with no recombination.** Sexual reproduction requires
partner-finding, which requires perception, and stacking both on the first implementation
would confound two failures into one.

This has a consequence worth stating plainly rather than discovering later: **§4.5's
grafting operator has no mechanism to fire.** Grafting is the design's only recombination —
plain parameter-vector crossover being meaningless on a variable-topology graph — and it
needs a second parent that asexual reproduction never supplies. So variation is by mutation
alone. That is a defensible position (Tierra and Avida produce open-ended dynamics with no
recombination at all, and §2's co-adaptation argument says recombination is where the damage
to a tuned controller is worst), but it is **not an evidence-led one**: the literature review
has no coverage of crossover or recombination whatsoever, and Q2 remains 🟡 partial. Settling
it is review round 3 work, before Milestone 6. Two alternatives were considered and deferred:
sexual reproduction once perception exists, and horizontal gene transfer through predation —
which would give grafting a mechanism at Milestone 5 without needing perception, at the cost
of breaking the archive's assumption that lineage is a tree.

**Brood size and offspring endowment are evolved, not global constants.** A creature carries
two numbers: *n*, how many offspring per event, and *e*, how much energy each starts with.
A reproduction event costs

```
n × (e + overhead)
```

where `overhead` is a world constant (§5A.10) and **not** evolvable — a creature permitted
to set its own overhead would set it to zero, and every lineage would converge on the largest
brood it could express.

The overhead term is load-bearing, not bookkeeping. Without it, cost is strictly proportional
to energy invested, and one brood of four is indistinguishable from four broods of one — same
energy, same offspring, differing only in timing. Brood size would then select for nothing.
The overhead is what separates them, and what makes r/K selection an axis the world can
explore: the same surplus buys one well-provisioned offspring or eight feeble ones, and which
wins is a property of the environment rather than something written in here.

**The reproduction threshold is derived, not configured.** A creature reproduces once it
holds `n × (e + overhead)` above its own reserve. A creature that evolves a larger brood
therefore waits longer for it automatically, and there is no separate constant to keep in
sync.

⚠ Until growth exists, an offspring is born full-size, so endowment buys it *time* — how long
it can search before starving — rather than body. Early runs are therefore unusually kind to
the many-and-feeble strategy, and should be read with that in mind.

Neutral buoyancy is retained (§5.2): depth changes only by swimming. Per-part density is a
tempting knob — swim bladders are cheap and biological — but it is a second system and is
deferred.

### 5A.7 Failure modes

| Failure | Why it happens | Defence |
|---|---|---|
| **Photosynthetic mat** | Sunlight covers upkeep, so nothing must move | Basal metabolism above peak photosynthesis at depth; depth/light gradient (§5A.4) |
| **No carnivores ever** | Predator valley; trait purged before it can pay | Carrion feeding, plus cell-type mutation (§5A.3) |
| **Free-energy takeover** | A physics exploit becomes an unlimited food source | Global energy audit (§5A.2) and §11.2's conservation checks |
| **Efficient nothing** | With no reachable strategy, minimising cost is the winning move | Watch for populations with near-zero work and near-zero displacement. [C18 §3, p.13] documents exactly this under directed search: artefacts *"which may be mistaken for the existence of highly energy efficient locomotion strategies"* |
| **Bloat (body)** | Any resource with no price | Every part and every neuron costs, including `Structural` (§5A.1) |
| **Bloat (genome)** | Development caps a body at `MaxParts`, so nodes beyond what it expresses are never grown, cost nothing, and are invisible to an economy that prices bodies. Measured: with per-birth add/remove, a 100,000-birth lineage reached **847 nodes** and replay went quadratic — 32 s for the chain | Nodes enter small and leave by shrinking below an extinction threshold (§4.5). Unexpressed nodes are unexpressed *because* nothing selects on them, so they are exactly the ones that drift out — bloat clears itself, with no rate, cap or price doing the work. Measured at ~39 nodes over 100,000 births. ⚠ Still worth pricing genome size at Milestone 3, since replication is genuinely costly in real cells; the tension to resolve is that unexpressed nodes are also the raw material adaptation draws on. Genetic-programming bloat is well studied and the review has never covered it — round 3 |

### 5A.8 What this supersedes

| Section | Status |
|---|---|
| **§5.5 Fitness (water)** | ❌ **Superseded.** There is no fitness function. Displacement becomes an observable, not an objective |
| **§6.3 Tiling** | ⚠ **Repurposed.** Tiling isolated simultaneous independent evaluations. An ecosystem is one shared world where creatures must be able to meet, so tiling becomes spatial partitioning rather than isolation |
| **§6.4 Throughput** | ⚠ **Unit changed.** Evaluations per second stops being meaningful. The unit is simulated seconds per wall-clock second at a given population |
| **§8 MAP-Elites** | ⚠ **Demoted.** It was the selector, and it existed to solve a problem exogenous fitness creates (§2). Under endogenous selection its innovation-protection role is served by ecological niches. Retained as an **observatory** — an archive recording what lived and what it looked like — which costs little and is what makes a long run legible |
| **§10 Milestones** | ⚠ Milestone 6 (sensors, photosensors) moves from last to load-bearing; foraging is target-following with the target being food |

### 5A.9 Feasibility

Spike 01 (§11.1) measured 128 actuated creatures at **1.945 ms/step**. At dt = 0.01 that is
195 ms of compute per simulated second — **5× faster than real time with 128 creatures in the
world**. Those figures exclude fluid drag, self-collision and brain evaluation, all of which
now exist or are coming; applying a 3× penalty still leaves 128 creatures at ~1.7× real time,
and roughly 500 at about half real time.

PhysX parallelises across solver islands, which is why per-creature cost fell to 0.28× going
from 1 to 64 creatures. Creatures in open water are mostly far apart and stay separate
islands, so that scaling largely survives; it degrades only where creatures touch, which here
means predation — rare and local.

**Cheap:** nutrients as plain advected data with a spatial hash (never rigidbodies — that is
the one trap, and would cost more than every creature combined); the current field; the light
cycle; brain evaluation at a few hundred creatures.

**Expensive:** creature count and DOF count. The wall is population, somewhere around 500–1000,
and it is physics rather than ecosystem bookkeeping.

### 5A.10 Open parameters

Every number below is unknown and must be measured rather than guessed. They are listed here
so that a value appearing in code without an entry here is visible as an unexamined choice.

**They all live in one place: `RunConfig` (`Evosim.Core`).** A constant compiled into a class
is a constant no run can vary, and an unmeasured constant that cannot be varied is an
assumption wearing the costume of a fact. So every number here is a settable property, cell
upkeep and feeding rates included, and a run is defined by a `RunConfig` instance.

`RunConfig.Hash()` is **§7's `configHash`** — the same object that parameterises a run is the
one that identifies it. Two tests drive that by reflection over `RunConfig` and
`RandomGenomeOptions`, so a tunable added without being folded into the hash fails
immediately rather than years later; the first run of that test found `MaxEdgesPerNode`
already missing. This matters most in the case where two runs produce *identical* output,
which on this project has twice meant a configuration change never reached the thing it
configured (logbook/0007, logbook/0008). A hash that differs while the results do not is the
cheapest way to tell that apart from a parameter that genuinely does not matter.

- Basal metabolic rate per unit volume, per part type
- Peak photosynthetic rate, and its falloff with depth — jointly with the above, this is the
  knob in §5A.2
- Neural cost per neuron and per connection
- Mechanical work coefficient — what a joule of ∫\|τ·ω\| dt is worth against a joule of sunlight
- Yield fractions in §5A.3, and the loss on transfer
- Nutrient spawn rate from decay, and sink rate
- ~~Reproduction threshold and offspring endowment~~ — **resolved by §5A.6**: endowment and
  brood size are evolved genome traits, and the threshold is derived from them. What remains
  is the per-offspring **overhead**, which is a world constant and still unmeasured
- Cell-type mutation rate — "very scarce" is the requirement; the value is not known
- Current field magnitude and correlation length
- Starting population and world volume

---

## 6. Runtime architecture

### 6.1 Assemblies

| Assembly | Unity? | Contents |
|---|---|---|
| `Evosim.Core` | No | Genome, development, mutation, archive, serialization, RNG |
| `Evosim.Sim` | Yes | Phenotype builder, environments, sensors, effectors, evaluator, tiling |
| `Evosim.Farm` | Yes | Headless orchestration, island model, batch entry point |
| `Evosim.Theatre` | Yes | Replay, camera, lighting, gallery, charts, validation harness |
| `Evosim.Tests` | No | Edit-mode tests against `Evosim.Core` |

### 6.2 Physics

- **`ArticulationBody`**, not `Rigidbody` + `ConfigurableJoint` — creatures are articulated
  kinematic trees, which is what PhysX articulations exist for, and far more stable under
  high joint torque. **Open risk — §11.1.** [K12 §2.3, p.7] used ODE with plain joints and
  needed substantial anti-exploit machinery; articulations should reduce but not eliminate
  that need.
- **Engine precedent.** [L21 Table 2, p.15] surveys what this field actually builds on:
  ODE (Shim 2003, Auerbach & Bongard 2010), Bullet (Joachimczak 2011), Voxelyze for
  soft-body work, **PhysX for Lessin et al.'s rigid-body EVCs (2013–15)**, and **Unity ML
  for Pathak et al. 2019**. So both the engine and the physics backend chosen here have
  direct precedent for rigid-body evolved creatures — this is not an unusual bet.
- `Physics.simulationMode = Script` — manual stepping, nothing tied to frame rate.
- Fixed timestep and solver iteration counts, both in the config hash.

### 6.3 Getting work out of 24 cores

**Multiple `PhysicsScene`s are not parallelism** — `Simulate()` runs serially on the main
thread. Two things work:

1. **Tiling** — many creatures per scene, spatially offset, on mutually-ignoring collision
   layers. PhysX splits its solver across independent islands. Proposed 100 m spacing,
   ~64 creatures per scene.
2. **Island model across processes** — `-batchmode -nographics`, ~10–12 workers, each with
   its own archive and seed, migrating genomes through a shared directory. [EA23 §2.9, p.7]
   notes MAP-Elites *"can be massively parallelized... if a pessimistic locking scheme is
   considered"*, and that batch processing gives implicit parallelism.

DOTS / Unity Physics is the escalation path if Milestone 2 disappoints — deliberately not
the starting point.

### 6.4 Throughput expectations

**No longer hypothetical — measured by Spike 01 (§11.1), 2026-08-02.**

Physics-only cost, 10-part creatures, 2000 steps per evaluation:

| Tiling | ms/step | Batch wall time | Evaluations / minute, **one process** |
|---|---|---|---|
| 64 creatures | 1.191 | 2.38 s per 64 | **≈ 1,610** |
| 128 creatures | 1.945 | 3.89 s per 128 | **≈ 1,970** |

Draft 2 hoped for 500–2000 evaluations/minute *across ten processes*. **A single process
already reaches the top of that range**, leaving roughly an order of magnitude of headroom
for the costs the spike excluded (fluid forces, brain evaluation, collision — see §11.1).

Two consequences:

- **The island model is no longer a throughput necessity.** §6.3's multi-process design
  should be retained for its *evolutionary* value — independent subpopulations with
  migration, which addresses §2 — but it is no longer load-bearing for speed. If it proves
  awkward, dropping to 2–4 workers is now viable.
- **[K12]'s entire study (≈1.35×10⁶ evaluations) is roughly 14 hours on one process** at
  the measured rate, before overheads. Overnight runs are comfortably in reach.

Re-measure at Milestone 3 once fluid and controller costs are real. The margin is large
but it is not infinite, and the brain graph is the term most likely to consume it.

---

## 7. Reproducibility

Every evaluation defined by `(genome, seed, configHash)`.

- Seeded PRNG per evaluation, stored with the result. No ambient randomness.
- `configHash` covers timestep, solver iterations, fluid constants, caps, Unity version.
- **Honest caveat:** PhysX is not bitwise deterministic across CPUs, drivers or Unity
  versions. Same-machine same-version replay is reliable in practice; cross-machine is not
  guaranteed. The hash exists so mismatches are *detected*.

---

## 8. Search: MAP-Elites, revised

> ⚠ **Demoted by §5A — read this section as conditional.** MAP-Elites was the *selector*, and
> §8.1's argument for it is an argument about exogenous fitness: it protects morphological
> innovation from being out-competed before its controller re-adapts (§2). Under endogenous
> selection nothing is scored, and that role passes to ecological niches — spatial and
> trophic — maintained by the depth/light gradient and cell-type mutation (§5A.3, §5A.4).
>
> **Retained as an observatory:** an archive recording what lived and what it looked like,
> which is what makes a long run legible and what the §8.5 metrics measure. That is a much
> cheaper claim than the one this section originally carried.
>
> Everything below remains correct *if* directed search is ever restored — for a benchmark, or
> to seed a population the ecosystem then has to keep alive. §8.2 (fitness-proportional
> selection) and §8.3 (two descriptors) are the parts that would be easy to get wrong twice,
> which is why they are kept rather than deleted.

### 8.1 Why it stays

[EA23 Table 5, p.24] ranks MNSLC above MAP-Elites (Condorcet 6–5), but their own practical
recommendation governs here — [EA23 §7, p.27]: MAP-Elites *"is a worse algorithm than
MNSLC in terms of protection of morphological diversity but probably represents a better
option in practice for the co-evolution of morphology and control of virtual creatures
due to its balanced trade-off between exploration and exploitation."*

Plus the structural argument in §2.3, and the practical one: the grid doubles as the
gallery UI, making the research instrument and the visual showpiece the same screen.

**Transfer caveat:** [EA23 §3.1, pp.8–9] uses soft voxel robots (5×5×5, CPPN-encoded,
Voxcraft) on straight-line locomotion with 5 s evaluations ([EA23 §4.1.1, p.12]) — *not*
rigid articulated Sims creatures. The premature-convergence mechanism is
substrate-independent and well-replicated; the specific algorithm ranking is not
demonstrated on this substrate.

### 8.2 ⚠ Selection is fitness-proportional, not uniform

Draft 1 specified uniform sampling from filled cells — canonical MAP-Elites per
[EA23 §2.9, p.7]. But [EA23 §3.5.4, p.12] modified this to *"select parents
proportionally to their fitness"* (after Cully & Demiris 2018), and [EA23 §7, p.27] reports
the result: it *"led to the resulting algorithm not only achieving more robust solutions
against premature convergence of morphology with respect to control and higher quality in
terms of diversity but also attaining solutions that are more globally fit."*

Improvement on all three axes for a trivial implementation change.

### 8.3 ⚠ Descriptor choice is load-bearing — use two

A behaviour characterisation is **aligned** if seeking diversity in it also finds higher
fitness — definition at [EA23 §2.10, p.7]. Two failures bracket the problem:

- **Unaligned-only.** Morphological descriptors are unaligned — [EA23 §2.10, p.7] notes
  *"the search for novel morphologies in many cases does not lead to better walking
  cycles"*, and reports Pugh et al. 2016's warning that with unaligned BCs, QD algorithms
  *"fail to obtain optimal solutions, and in more complex problems, they may completely
  fail to make any progress toward even viable solutions."*
- **Aligned-only.** [K12 §3.1, p.9] used final position — three unbounded reals, essentially
  fitness restated — and at [K12 §4, p.11] divergent search **lost** to plain fitness search
  on swimming: **96.23 m vs 108.32 m** (p < 0.05; random baseline 14.46 m).

Neither alone. **Adopt multi-BC** — an aligned grid alongside the morphological one, per
[EA23 §2.10.2, p.8] (ME-ME) and the MNSLC result.

| Descriptor | Type | Role |
|---|---|---|
| Part count (1–16) | Unaligned | Gallery axis; legible in a thumbnail |
| Gait frequency (dominant joint oscillation) | Unaligned | Gallery axis; separates eels from paddlers |
| Displacement direction / magnitude | **Aligned** | Guides search toward viability |
| Elongation, symmetry, mean joint activity | Unaligned | Candidate alternates |

Descriptors must be **bounded** and, on the unaligned grid, **not fitness proxies**. Grid
16×16 proposed; [EA23 §3.4, p.10] used `grid_dimensions: [25, 25]`.

### 8.4 Explicit innovation protection

The archive gives implicit protection (§2.3). If Milestone 3 still shows morphological
stagnation, add Cheney et al. 2018-style explicit protection — shield genomes whose
morphology changed within the last *N* generations from elimination, letting controllers
re-adapt. Held in reserve, not built up front.

### 8.5 Metrics

**Coverage** (filled cells / total) and **QD-score** (sum of quality over filled cells) —
both defined at [EA23 §4.1.3, p.13]. Far more informative than a single best-fitness curve.
Track morphological and genotypic diversity **separately**; [EA23 Table 2, p.24] shows
algorithms that preserve one while losing the other (e.g. ME scores 0 on genetic diversity
`D_g` and `D_gc` while scoring 4 on coverage).

---

## 9. Persistence and data

```
runs/<runId>/
  run.json            config, seeds, engine version, configHash, git rev
  archive/            one JSON genome per filled cell, named by cell coords
  archive-aligned/    the second (aligned) grid
  log.jsonl           per evaluation: genome hash, fitness, descriptors, seed, wall time
  metrics.csv         per iteration: evals, coverage, QD-score, best fitness,
                      morphological diversity, genotypic diversity
  migrants/           island-model exchange
  champions/          hand-picked genomes for the theatre
  validation/         champion re-simulation under higher-fidelity fluid (§5.4)
```

Genomes as **JSON** — readable, diffable, git-friendly, small.

---

## 10. Milestones

**Revised at draft 5.** The old plan built a search engine (MAP-Elites, island model) and
treated currents and predators as a final sandbox. §5A inverts that: currents and feeding are
the mechanism, and search is something the world does by itself. Milestones 0–2 are unchanged
because they are physics, and physics does not care how selection happens.

| # | Milestone | Ends with |
|---|---|---|
| **0** ✅ | Unity project, URP, assemblies, physics config | Empty scene that builds and runs headless |
| **1** ✅ | Genome model, development, phenotype builder | Spawn a random creature, watch it flop. **First visual payoff.** |
| **2** | Physics harness: fixed stepping, seeding + config hash (§7), fluid forces, **anti-exploit checks (§11.2)**, mechanical work accounting | Throughput in *simulated seconds per wall-clock second* at a given population — not evaluations/second (§5A.8) |
| **3** | Metabolism: cell types on the genome, per-part upkeep, neural cost, energy as a running balance. Mutation operators (§4.5) | **A creature that starves.** The first thing in this project that can fail on its own |
| **4** | World: current field, light/depth gradient, nutrient particles and absorption | A creature that survives by drifting into food, and one that doesn't |
| **5** | Life cycle: death returns tissue to the nutrient pool, reproduction on an energy threshold, mutation on reproduction | **A population that persists without intervention.** The first open-ended run, and where it stops being a project and becomes fun |
| **6** | Perception: photosensors, evolvable colour, closed-loop brain graph (§4.3, §4.4) | Directed foraging — a creature that moves *toward* something |
| **7** | Food web: `Consumer` cells, carrion, predation, attack and defence | Trophic levels, or clear evidence of why not (§5A.7) |
| **8** | Theatre: replay, gallery, charts, lineage, export, **fluid validation harness (§5.4)** | Showpiece + research instrument |
| **9** | Land: contact, gravity | Deferred. Water first, and the ecosystem is a water design |

**Milestone 3 is the pivot.** Everything before it is a simulator; everything after it is a
world. It is also the cheapest place to discover that the metabolism/photosynthesis ratio in
§5A.2 is wrong, because at that point nothing eats yet and starvation is the only outcome
being measured.

**Superseded:** old Milestone 3 (multi-BC MAP-Elites + CPG) and old Milestone 4 (island
model). Neither is deleted from §8 — see the note there — but neither is on the path.

---

## 11. Risks

### 11.1 ✅ `ArticulationBody` at scale — RESOLVED by measurement

Was the top open risk. **Spike 01 ran on 2026-08-02 against Unity 6000.5.6f1 and all six
measurements passed.** Full results: [`spikes/01-articulation-body/results/FINDINGS.md`](spikes/01-articulation-body/results/FINDINGS.md).

| Measurement | Budget | Measured | |
|---|---|---|---|
| Build + teardown, 10-part creature | < 15 ms | **0.335 ms** median | ✅ 45× margin |
| Step cost @ 64 tiled | 0.15–0.30 ms/creature | **0.0186 ms** | ✅ 8–16× margin |
| Scaling, 64 vs 1 | must be sub-linear | **0.28×** | ✅ real island parallelism |
| Torque stability, 2000 steps | no NaN / separation / blow-up | clean | ✅ |
| Determinism, 10 runs same seed | < 1e-4 m drift | **0.0 m** (bitwise) | ✅ |
| Chain depth 16 | §4.2 caps at 8 | stable, 0.097 ms/step | ✅ headroom |

**Consequences:**
- **Pooling is not required.** §11.1's fallback is unnecessary — rebuild-per-evaluation
  costs 0.335 ms against a 2.4 s batch. The topology-bucketing constraint that pooling
  would have forced does not apply, so morphology can vary freely within a batch.
- **`ArticulationBody` is confirmed** over `Rigidbody` + `ConfigurableJoint`.
- **Tiling (§6.3) is confirmed.** DOTS stays in reserve.

⚠ **What the spike did *not* measure** — the margins above are real but will shrink:
1. **No fluid forces.** Per-part drag + added mass (§5.2, §5.4) is extra work per part per step.
2. **No brain evaluation.** A full §4.3 graph is far heavier than the sine used here, and
   it is managed C# in the hot loop — the main argument for adding IL2CPP before Milestone 4.
3. **Collision is disabled entirely.** The spike sets
   `IgnoreLayerCollision(CreatureLayer, CreatureLayer, true)`, which suppresses tile-to-tile
   *and* self-collision. Real runs want self-collision — [C18 Fig. 13, p.19] shows creatures
   exploiting it — and contact solving is not free.
4. **Same-process determinism only.** Cross-process and cross-run reproducibility, which
   §7 actually depends on for the island model, is untested.

### 11.2 Physics exploitation — now a concrete checklist

Draft 1 had a vague warning. [K12 §2.3, p.7] documents the machinery a working system
actually needed, adopted wholesale:

| Check | Trigger | Action |
|---|---|---|
| **Joint separation** | Relative displacement of two connected parts above threshold | Zero fitness, abort |
| **Velocity cap** | Any part exceeds linear *or* angular velocity threshold | Zero fitness |
| **Oscillation detector** | Creature moving via small oscillations (solver-error exploitation) | Discard |
| **Minimum volume** | Any part below volume threshold | Reject **pre-simulation** |
| **Self-collision vibration** | Thrust generated by parts colliding with each other at high frequency | Flag / discard |
| **Momentum conservation** | Total linear or angular momentum changes while nothing external acts | Reject — the actuation model is wrong |
| **Buried parts** | Any part's centre inside another part's box | Reject **pre-simulation** |
| **Depenetration velocity cap** | Solver separating overlapping parts faster than the cap | Prevent — engine configuration, not a per-creature test |
| **Engine damping defaults** | Any physics-engine damping the design did not specify | Zero it — §5.2 is the only resistance a creature may feel |
| **Energy balance** | Joint work ≠ ΔKE + drag dissipated, on a system with no other sink | Reject — the actuation or fluid model is not measuring what it claims |

Self-collision vibration is from [C18 Fig. 13, p.19], which reports that "some of the best
stiff robots (S5) **exploit self-collisions resulting in fast vibrations to produce
thrust**" — a distinct exploit from solver-error oscillation, and one an articulated
creature with overlapping parts (§4.2) is well-placed to discover.

The last three rows are not from the literature. They were earned during implementation and
differ from the rest of the table in a way worth stating: **they are checks on the
simulator, not on the creature.**

- **Momentum conservation.** With no gravity, drag or contact, nothing external acts on a
  creature, so its total momentum cannot change however its joints move. The first effector
  implementation applied joint torque to the child link without the reaction on the parent,
  making every joint an external torque; creatures span up without bound. This is a
  conservation law rather than a threshold, so it needs no tuning and cannot be satisfied by
  an impressive-looking failure — measured specific angular momentum was ~0.001 m²/s when
  correct against 1–2 m²/s when not.
- **Buried parts.** See §4.2. Free thrust from coincident parts requires no cleverness from
  the search to discover; it is simply lying there.
- **Engine damping defaults.** `ArticulationBody.angularDamping` and `jointFriction` both
  default to 0.05, and nothing in Milestone 1 set them. Creatures were therefore swimming in
  two fluids: the one in §5.2, which is unit-tested to prove it can never add energy, and
  PhysX's, which nobody chose and which removed roughly **ten times more**. Every displacement
  figure produced before 2026-08-04 was measured against the wrong resistance. Zeroing it
  raised measured drag dissipation on one creature from 4,672 to 5,705 J — energy that had been
  vanishing now reaches the water. If the solver ever needs damping for stability, that is a
  fluid-model parameter belonging in `FluidConfig` where it can be measured, not an engine
  default.
- **Energy balance.** With no gravity, contact or ground, signed joint work must equal the
  change in kinetic energy plus everything drag dissipated. Verified on a two-cube, one-hinge
  system with the DOF genuinely free: the residual converges first-order with the timestep
  (6.60% → 3.55% → 1.99% → 1.04% as dt halves), which establishes that `τ·(ω_child − ω_parent)`
  measures what it claims. On real creatures the balance does *not* close, and the gap is real
  energy destroyed by joint-limit constraints — a calibration finding rather than a defect, so
  it is reported as *energy into joint limits* rather than asserted on. See logbook/0008.
- **Depenetration velocity cap.** PhysX resolves an overlap by assigning separating velocity,
  which is a positional correction rather than a force and does not conserve momentum. At
  Unity's default of 10 m/s this is a free-energy source a creature can reach deliberately:
  fold a limb into your own body and the solver pays you to unfold it. Measured with
  self-collision enabled, a creature retaining 2% of its free-swinging range of motion reached
  0.254 m/s of centre-of-mass velocity under purely internal forces — 119× the same creature
  with self-collision off — and travelled further in water than any creature that actually
  swam. Since fitness is displacement (§5.5), the search would have converged on jamming.
  `Physics.defaultMaxDepenetrationVelocity` is set to **0.02 m/s**. It was 0.5 until the
  engine-damping row below removed a term that had been masking a second, smaller injection;
  the leak falls monotonically with the cap (0.5 → 0.045, 0.1 → 0.032, 0.02 → 0.019 m²/s) and
  is bounded rather than eliminated. Unlike the rest of the table this is a *prevention* rather than a test, and it is
  the momentum conservation row that detects it if the setting is ever lost.

The general lesson is worth keeping: for a physical simulation, prefer *"which conservation
law would this violate if it were wrong"* over *"does this output look reasonable."* A
search will satisfy the second while violating the first.

[K12 §2.3, p.7] on the last: *"Such tests help detect invalid robots early, so that they do
not consume computational resources during full-scale physical simulation"* — a throughput
win as much as a correctness one.

Add, from §5.3, a **fluid-artifact check**: flag champions whose gait is high-frequency,
low-amplitude and many-surfaced, and route them to the validation harness first.

Managed, never solved. [K12 §2.3, p.7] is blunt: *"search algorithms often exploit errors
and instabilities in the physics engine to increase the fitness value of robots."*

### 11.3 Premature morphological convergence

Addressed structurally in §2 and §8. **Watch explicitly at Milestone 3** — track
morphological diversity per generation, not just fitness. Rising fitness with flat
morphological diversity is the diagnostic signature.

### 11.4 Throughput shortfall

Escalation is DOTS / Unity Physics. Decision at Milestone 2 on measured data.

### 11.5 Scope creep

Milestones 5–8 are all desirable and all deferrable.

---

## 12. Open questions

1. **✅ RESOLVED — Encoding: keep the recursive graph.** Draft 2 flagged this as the most
   expensive decision to retrofit, because [EA23 §3.1, p.8] used a **CPPN** encoding and
   cited Cheney et al. 2014 that CPPNs *"tend to lead to better evolvability, as they are
   capable of producing morphologies and controllers with regular patterns."*

   [L21 Table 6, p.18] synthesises every published encoding comparison in the field, and
   the result splits cleanly **by phenotype type**:

   | Year | Authors | Tested | Best | Phenotype |
   |---|---|---|---|---|
   | 2001 | Komosiński & Rotaru-Varga | Direct, direct-recursive, L-system | Recursive/indirect in one test, otherwise **no significant difference** | Rigid |
   | 2010 | Hiller et al. | DCT, CPPN, GMX | GMX | Soft |
   | 2011 | Auerbach & Bongard | Non-recurrent vs recurrent CPPN | Recurrent CPPN | Rigid |
   | 2013 | Cheney et al. | Direct, CPPN-NEAT | **CPPN-NEAT** | **Soft** |
   | 2017 | Veenstra et al. | Direct, L-system | L-system, only for small robots / few generations | Rigid |
   | 2020 | Veenstra & Glette | Direct, L-system, CPPN, CE | **Direct and L-system** | Rigid |

   **The CPPN advantage is a soft-body result.** Cheney et al. 2013 — the study [EA23]
   relies on — tested soft voxels, and [EA23]'s own substrate is soft voxels. On **rigid
   articulated bodies**, the substrate used here, CPPNs do not win: the most recent and
   most comprehensive comparison (Veenstra & Glette 2020, four encodings) places **direct**
   among the winners, and the 2001 study found no significant difference.

   The 2017 generative advantage also decays with compute — [L21 §4.2, p.8] reports
   significance only at a five-module cap; at ten and twenty modules the L-system beat
   direct at 6,250 evaluations but **not at 12,000 or 25,000**. [L21] additionally flags
   that comparison as confounded: *"the rules that govern the evolutionary algorithms were
   different, and therefore it makes an accurate comparison difficult."*

   **Decision:** keep the recursive graph of §4.1. Revisit only if this project ever moves
   to a soft-body phenotype, where the evidence genuinely favours CPPN-NEAT.
2. **Part cap of 16** — higher gives more spectacular creatures, costs throughput.
3. **Evaluation window and timestep** — see §5.5; measured at Milestone 2.
4. **Archive resolution** — 16×16 proposed; [EA23] used 25×25.
5. **Unity version** — Unity 6 LTS proposed; nothing here depends on a patch version.

---

## 13. References and source access

### 13.1 How to get back to a source

Retrieved PDFs and their extracted packages live under `research/papers/` and are
**gitignored** (copyright — see `.gitignore`). Each paper has a package:

```
research/papers/<key>/
  source.md          ← canonical reading copy; "### Page N" matches p.N in citations
  candidates/        raw extraction
  figures/           extracted figures + per-figure notes
  tables/            extracted tables (.md and .csv)
  manifest.json
```

`research/FETCH-RESULTS.md` is tracked and records the **exact retrieval URL** for every
paper, so the whole set can be re-fetched by anyone with equivalent access. If a package
is missing, start there.

### 13.2 Read — claims above cite these directly

| Key | Citation | Local package |
|---|---|---|
| **[C18]** ⚠ *partial* | F. Corucci, N. Cheney, F. Giorgio-Serchi, J. Bongard, C. Laschi, "Evolving Soft Locomotion in Aquatic and Terrestrial Environments," *Soft Robotics*, vol. 5, no. 4, pp. 475–495, 2018. DOI `10.1089/soro.2017.0055` — **read: abstract, §2.1–2.2 (pp.4–5), §3.2 (pp.17–21), §3.3 (pp.22–27), §4 (pp.28–30). Sections 2.3–3.1 unread.** | `research/papers/27-corucci-2018-evolving-soft-locomotion/` |
| **[L21]** ⚠ *partial* | G. Lai, F. F. Leymarie, W. Latham, T. Arita, R. Suzuki, "Virtual Creature Morphology – A Review," *Computer Graphics Forum*, vol. 40, no. 2, pp. 659–681, 2021. DOI `10.1111/cgf.142661` — **read: §4.1–4.2 (pp.7–8), §8.2 (p.10), §13 (pp.14–16), Table 2 (p.15), Table 6 (p.18). Rest unread.** §12.1 claims come from §4.1–4.2; §5A's claims about complexity penalties come from §8.2 and its account of PolyWorld and Gene Pool from §13 (review round 2) | `research/papers/14-lai-2021-virtual-creature-morphology-review/` |
| **[K12]** | P. Krčah, "Solving Deceptive Tasks in Robot Body-Brain Co-evolution by Searching for Behavioral Novelty," in *Advances in Robotics and Virtual Reality*, Intelligent Systems Reference Library vol. 26, Springer, 2012, pp. 167–186. DOI `10.1007/978-3-642-23363-0_7` | `research/papers/05-krcah-2012-solving-deceptive-tasks/` |
| **[U07]** | Y. Usami, "Re-examination of Swimming Motion of Virtually Evolved Creature Based on Fluid Dynamics," in *Advances in Artificial Life* (ECAL 2007), LNCS 4648, Springer, pp. 183–192. DOI `10.1007/978-3-540-74913-4_19` | `research/papers/28-usami-2007-swimming-motion-fluid-dynamics/` |
| **[EA23]** | L. Eguiarte-Morett and W. Aguilar, "Premature convergence in morphology and control co-evolution: a study," *Adaptive Behavior*, vol. 32, no. 2, pp. 137–165, 2023. DOI `10.1177/10597123231198497` 🔓 CC BY-NC 4.0 | `research/papers/03-eguiarte-morett-2023-premature-convergence/` |
| **[TM01]** ⚠ *partial* | T. Taylor and C. Massey, "Recent Developments in the Evolution of Morphologies and Controllers for Physically Simulated Creatures," *Artificial Life*, vol. 7, no. 1, pp. 77–87, 2001. DOI `10.1162/106454601300328034` — **read: pp.4, 6–8 (joint actuation, fitness-function design, complexity caps). Rest unread.** Moved here from §13.3 in review round 2 | `research/papers/09-taylor-massey-2001-recent-developments/` |
| **[CEA07]** ⚠ *partial* | N. Chaumont, R. Egli, C. Adami, "Evolving Virtual Creatures and Catapults," *Artificial Life*, vol. 13, no. 2, pp. 139–157, 2007. DOI `10.1162/artl.2007.13.2.139` — **read: §3.2–3.4 (pp.3–6), §5 (pp.13–14). Rest unread.** Moved here in round 2. ⚠ The C_l/C_s/C_v/C_c equations on pp.5–6 are **lost in PDF extraction** — the prose survives, the display equations do not. Check the PDF before citing any formula | `research/papers/12-chaumont-egli-adami-2007-catapults/` |
| **[CU15]** ⚠ *partial* | A. Cully, J. Clune, D. Tarapore, J.-B. Mouret, "Robots that can adapt like animals," *Nature*, vol. 521, no. 7553, pp. 503–507, 2015. DOI `10.1038/nature14422` — **read: supplementary pp.15, 22, 24, Extended Data Figs. 4 and 7. Main text unread.** Moved here in round 2; §5A cites it only for energy as a *behavioural descriptor* | `research/papers/19-cully-clune-tarapore-mouret-2015-robots-that-can-adapt/` |

> **Page-numbering note for [EA23]:** citations use **PDF page numbers** (matching
> `### Page N` in `source.md`). Journal page = PDF page + 136. So `[EA23 §7, p.27]` is
> journal page 163.

### 13.3 Retrieved, not yet read

**Empty as of draft 5.** All three former entries were read in part during review round 2 and
have moved to §13.2 — [TM01], [CEA07] and [CU15]. They are cited with page locators in §5A
and `DECISIONS.md` D017, and a citation whose source is filed as unread is exactly the kind of
drift the round protocol exists to catch.

### 13.4 Cited via read papers — ⚠ metadata from their reference lists, NOT independently verified

Treat these as leads, not citations. Verify before relying on any of them.

| Key | Citation | Where it was cited from |
|---|---|---|
| [S94] | K. Sims, "Evolving Virtual Creatures," SIGGRAPH '94, pp. 15–22; and "Evolving 3D Morphology and Behavior by Competition," *Artificial Life* 1(4):353–372, 1994 | [K12 refs 6–7, p.18]; [U07 refs 1–2, p.10] |
| [LSBC16] | Lipson, SunSpiral, Bongard, Cheney, "On the difficulty of co-optimizing morphology and control in evolved virtual creatures," ALIFE 2016, pp. 226–233 | [EA23 refs, p.28] |
| — | Cheney, Bongard, SunSpiral, Lipson, *J. R. Soc. Interface* 15(143):20170937, 2018. DOI `10.1098/rsif.2017.0937` | [EA23 refs, p.27] |
| [PSS16] | Pugh, Soros, Stanley, "Quality Diversity: A New Frontier for Evolutionary Computation," *Frontiers in Robotics and AI* 3, 2016. DOI `10.3389/frobt.2016.00040` | [EA23 refs, p.28] |
| — | Lehman & Stanley, "Evolving a diversity of virtual creatures through novelty search and local competition," GECCO '11, pp. 211–218 | [EA23 §2.8, p.6] |
| — | Cully & Demiris, "Quality and diversity optimization: a unifying modular framework," *IEEE TEVC* 22(2):245–259, 2018 | [EA23 §3.5.4, p.12] |
| — | Nordmoen, Veenstra, Ellefsen, Glette, "Quality and diversity in evolutionary modular robotics," IEEE SSCI 2020, pp. 2109–2116 | [EA23 §2.4, p.4] |
| — | Nygaard, Samuelsen, Glette, "Overcoming initial convergence in multi-objective evolution of robot control and morphology using a two-phase approach," EvoApplications 2017, pp. 825–836 | [EA23 §1, p.2] |
| — | Kriegman, Cheney, Bongard, "How morphological development can guide evolution," *Scientific Reports* 8, 2018 | [EA23 §1, p.2] |
| — | Ijspeert, "A connectionist central pattern generator for the aquatic and terrestrial gaits of a simulated salamander," *Biol. Cybern.* 84:331–348, 2001 | [U07 ref 14, p.10] |
| [VG20] | Veenstra & Glette, "How Different Encodings Affect Performance and Diversification when Evolving the Morphology and Control of 2D Virtual Creatures," ALIFE 2020 | Pass-1 search — *to read, settles §12.1* |
| [LCM20] | Lehman, Clune, Misevic et al., "The Surprising Creativity of Digital Evolution," *Artificial Life* 26(2):274–306, 2020 | Pass-1 search — *to read, expands §11.2* |
