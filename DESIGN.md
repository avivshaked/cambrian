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

Draft 3 was the last version written before any of it existed. Building Milestone 1 changed
four things. **No new literature was read**; every change below was forced by an
implementation or by a human looking at the result, and the distinction matters — these are
the corrections a review could not have supplied.

| Change | Was (draft 3) | Now | Why |
|---|---|---|---|
| **§4.2 recursion** | "Cycle traversal decrements a per-node counter; at zero, only `terminalOnly` edges are followed" | Occurrence counts per path, with the exhaustion and terminal rules stated explicitly | Read literally, the old wording made every non-recursive genome develop into a single box. Ambiguous rather than wrong, but not implementable as written |
| **§4.2 reflection** | *(unstated)* | Reflection is meaningful only about the **attachment axis** | Mirroring displaces a point only if it has a component on that axis. Choosing axes independently put 69.7% of random creatures partly inside themselves, while still looking plausible |
| **§4.2 overlap** | "Overlap at joints permitted" | Overlap permitted, **burial** tracked and rejected | A human saw boxes inside boxes within seconds. Per-part fluid forces would make coincident parts collect thrust twice |
| **§11.2** | Five checks, all from the literature | Adds **momentum conservation** and **buried parts** | Both earned in implementation. The first caught an actuation model that manufactured angular momentum from nothing — invisible to every "is it finite and moving" check that preceded it |

**What the process suggests.** The literature corrected the *design*; running it corrected
the *specification*. Three of the four entries above were found by a person watching
creatures move, after the headless test suite had passed. Milestone 3's visual payoff is
not decoration on the schedule — it is an instrument, and the only one that reports this
class of fault.

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
velocity per DOF; contact (land only); orientation vs world up; photosensor triple
(Milestone 6). [K12 §2.2, p.4] used only joint-angle sensors — *"A sensor in each body
part is measuring current angle of each degree of freedom of a joint."*

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

Perturb scalars (Gaussian); add/remove morph node; add/remove morph edge; add/remove
neuron; rewire neuron input (respecting §4.3); change joint type; change neuron op;
toggle any `reflect` flag or `terminalOnly`; change `recursiveLimit`; and **graft** —
attach a subgraph from genome B at a random edge of genome A.

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

### 5.5 Fitness (water)

Horizontal displacement of centre of mass, discarding ~1 s of settling. [K12 §3.1, p.9]
used *"the distance between the final and starting positions of the robot"* over 60 s.

**Evaluation window is an open parameter.** Draft 1 proposed 15 s at 1/120 s = 1800 steps.
[K12 §2.3, p.7] used **30 steps/simulated second over 60 s** — coincidentally also 1800
steps, very differently distributed. Finer steps resist solver exploitation; longer
windows separate a real gait from a lucky transient. Note [K12] still needed an explicit
oscillation detector at 30 Hz. Proposed compromise for Milestone 2 measurement:
**1/100 s over 20–30 s**, treated as measured rather than guessed.

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

| # | Milestone | Ends with |
|---|---|---|
| **0** | Unity project, URP, assemblies, physics config, git init | Empty scene that builds and runs headless |
| **1** | Genome model, development, phenotype builder | Spawn a random creature, watch it flop. **First visual payoff.** |
| **2** | Eval harness: tiling, fixed stepping, seeding, fluid forces, fitness, **anti-exploit checks (§11.2)** | Measured throughput. Go/no-go on §6.4. |
| **3** | Multi-BC MAP-Elites + CPG controllers, water | **First real swimmers.** Check for morphological stagnation (§2). |
| **4** | Island model across processes | Overnight runs, full archive |
| **5** | Land: contact, gravity, anti-degenerate fitness | Walkers |
| **6** | Full brain graph, sensors, photosensors | Target-following, reactive behaviour |
| **7** | Theatre: replay, gallery, charts, lineage, export, **fluid validation harness (§5.4)** | Showpiece + research instrument |
| **8** | Sandbox: currents, predators, obstacles | You as the selection pressure |

Milestone 3 is where it stops being a project and becomes fun.

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

Self-collision vibration is from [C18 Fig. 13, p.19], which reports that "some of the best
stiff robots (S5) **exploit self-collisions resulting in fast vibrations to produce
thrust**" — a distinct exploit from solver-error oscillation, and one an articulated
creature with overlapping parts (§4.2) is well-placed to discover.

The last two rows are not from the literature. They were earned during Milestone 1 and
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
| **[L21]** ⚠ *partial* | G. Lai, F. F. Leymarie, W. Latham, T. Arita, R. Suzuki, "Virtual Creature Morphology – A Review," *Computer Graphics Forum*, vol. 40, no. 2, pp. 659–681, 2021. DOI `10.1111/cgf.142661` — **read: §4.1–4.2 (pp.7–8), Table 2 (p.15), Table 6 (p.18). Rest unread.** All §12.1 claims come from these sections | `research/papers/14-lai-2021-virtual-creature-morphology-review/` |
| **[K12]** | P. Krčah, "Solving Deceptive Tasks in Robot Body-Brain Co-evolution by Searching for Behavioral Novelty," in *Advances in Robotics and Virtual Reality*, Intelligent Systems Reference Library vol. 26, Springer, 2012, pp. 167–186. DOI `10.1007/978-3-642-23363-0_7` | `research/papers/05-krcah-2012-solving-deceptive-tasks/` |
| **[U07]** | Y. Usami, "Re-examination of Swimming Motion of Virtually Evolved Creature Based on Fluid Dynamics," in *Advances in Artificial Life* (ECAL 2007), LNCS 4648, Springer, pp. 183–192. DOI `10.1007/978-3-540-74913-4_19` | `research/papers/28-usami-2007-swimming-motion-fluid-dynamics/` |
| **[EA23]** | L. Eguiarte-Morett and W. Aguilar, "Premature convergence in morphology and control co-evolution: a study," *Adaptive Behavior*, vol. 32, no. 2, pp. 137–165, 2023. DOI `10.1177/10597123231198497` 🔓 CC BY-NC 4.0 | `research/papers/03-eguiarte-morett-2023-premature-convergence/` |

> **Page-numbering note for [EA23]:** citations use **PDF page numbers** (matching
> `### Page N` in `source.md`). Journal page = PDF page + 136. So `[EA23 §7, p.27]` is
> journal page 163.

### 13.3 Retrieved, not yet read

| Key | Citation | Local package |
|---|---|---|
| **[TM01]** | T. Taylor and C. Massey, "Recent Developments in the Evolution of Morphologies and Controllers for Physically Simulated Creatures," *Artificial Life*, vol. 7, no. 1, pp. 77–87, 2001. DOI `10.1162/106454601300328034` | `research/papers/09-taylor-massey-2001-recent-developments/` |
| **[CEA07]** | N. Chaumont, R. Egli, C. Adami, "Evolving Virtual Creatures and Catapults," *Artificial Life*, vol. 13, no. 2, pp. 139–157, 2007. DOI `10.1162/artl.2007.13.2.139` | `research/papers/12-chaumont-egli-adami-2007-catapults/` |
| **[CU15]** | A. Cully, J. Clune, D. Tarapore, J.-B. Mouret, "Robots that can adapt like animals," *Nature*, vol. 521, no. 7553, pp. 503–507, 2015. DOI `10.1038/nature14422` | `research/papers/19-cully-clune-tarapore-mouret-2015-robots-that-can-adapt/` |

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
