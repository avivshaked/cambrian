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

**The graph is evaluated by `Brain` in `Evosim.Core`** — built once per creature at birth, stepped
once per physics step, producing one drive value per joint DOF for §4.4's effector. Until it
existed the genome's neurons were carried, developed and mutated but never read, and every creature
ran one shared sine regardless of genome (D030, logbook/0016). Three properties are load-bearing:
**synchronous update** (every neuron reads the previous step and writes a separate buffer, which is
what makes the one-node-per-step latency below true rather than an artefact of iteration order);
**neuron *d* of a part drives DOF *d*** of its joint, the only effector mapping that survives
recursion, since the genome carries no mapping field of its own; and **`sigmoid` is `tanh`**, because
a logistic curve is strictly positive and a joint driven through one could not oscillate.

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
(Milestone 6); **chemical**, **energy** and **flow** (Milestone 4). [K12 §2.2, p.4] used only
joint-angle sensors — *"A sensor in each body part is measuring current angle of each degree
of freedom of a joint."*

**Five of the original six sense the creature's own body.** Only the photosensor and contact
say anything about the world, and contact only reports something already touching. That is
adequate for locomotion — a central pattern generator needs nothing else — and inadequate for
everything §5A is about. A world where nothing can perceive anything at a distance cannot
produce foraging, pursuit or avoidance; it produces rhythmic open-loop swimmers and drift.

**The largest omission was smell.** §5A.1 says an absorptive cell *"rewards being where food
is"*, and until draft 5 nothing could sense where food was. Absorptive feeding was therefore
not a strategy but a lottery: intake depended on what a creature drifted into, and no degree of
control or intelligence could improve it. Chemotaxis predates vision by billions of years and
bacteria manage it, which is some indication of how basic the missing capability was.

| Channel | Reads | Why |
|---|---|---|
| **Chemical** | Nutrient concentration at this part | Smell. Makes §5A.1's absorptive cell a strategy rather than luck |
| **Energy** | The creature's reserve, as **seconds of life remaining at its current burn rate** | The state variable everything in §5A turns on |
| **Flow** | Water velocity relative to this part, per axis | A lateral line: currents (§5A.4), and something large moving before it arrives |
| **Depth** | How deep this part is, as a fraction of the world's depth | The axis the whole environment is structured along — and the only way to know it at night |

**Depth is not redundant with the photosensor, and the reason is the night.** Irradiance is a
usable depth proxy only while the sun is up; once §5A.4's diurnal cycle exists, light at night
says nothing about depth at all. This channel is what makes **diel vertical migration**
expressible — rising in darkness, sinking by day — which §5A.4 names as the most watchable
outcome this design could produce.

It is called depth rather than pressure deliberately. Pressure would be the better framing, a
local quantity rather than a world coordinate — but §5.2 disables gravity outright to obtain
neutral buoyancy, so hydrostatic pressure here is uniform and the name would promise a model
that does not exist.

**It is also the one channel reporting a world-frame quantity**, which is worth stating rather
than hiding: every other channel is something physically present at the part. It is admitted
because depth in this world is not an arbitrary coordinate but a real gradient a creature is
immersed in — and because the gradient rule below still applies to it, so a creature recovers
*which way is up* from its own morphology with no light required.

**Energy is the level, not a hunger flag.** A flag needs a threshold, and choosing one is us
deciding when a creature ought to feel hungry — an unmeasured constant (§5A.10) baked where no
run could vary it. A neuron builds whatever thresholds it wants from a weight and a bias, and
can hold several at once for different behaviours. Hunger is derivable from the level; the
level is not recoverable from hunger. It is normalised against the creature's own burn rate so
it reads as a duration: raw joules are meaningless across body sizes, and normalising against
the §5A.6 reproduction threshold instead would let a brood-size mutation silently rescale how a
creature perceives itself.

**Every distal sense is a scalar, and direction is computed by the body.** No channel reports a
*bearing* to anything. A sensor on a part is already a gradient sensor for a creature made of
several parts: two cells at opposite ends read different concentrations, and the difference is
a direction — which is how chemotaxis actually works. This makes **morphology part of the
sensory apparatus.** A long creature resolves direction better than a compact one; a
bilaterally symmetric one can compare left against right; and because signals meet at one node
per step (above), a long body senses direction better but thinks about it slower.

A "direction to nearest food" channel is therefore rejected, not deferred: it would hand the
creature a solved problem and sever the link between body shape and perception. The photosensor
triple is the same design — an eyespot, not an eye. Directional light sensing comes from parts
facing different ways and shading each other, not from rendering a view per creature.
*(Author's inference; the corpus addresses none of these channels.)*

**Sensors are evaluated on demand, not per channel per part.** Which `(part, channel)` pairs any
neuron ever references is a static property of a developed phenotype, so the builder computes a
requirement mask once and the step loop evaluates only what is in it. Cost then scales with what
evolution actually uses rather than with what is declared, and adding a channel costs nothing
until something reads it. This matters because §5A.9's measured bottleneck is a per-part
per-step loop already, and the mask is what stops perception becoming a second one.

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

### 5A.0b Generation zero: what the world starts with

§4.1's `GenomeFactory.Random` builds creatures of two to five nodes that develop into three to
sixteen parts, with branching, recursion, bilateral pairs and several joints. That was correct
under exogenous fitness: a fitness function needs something to grade on the first evaluation,
so the initial population has to be able to do the thing being graded.

Under §5A nothing grades anything, and that argument disappears. Worse, it inverts: **a
founder population with body plans we designed makes every later claim about morphology a
claim about our initial conditions.** If bilateral symmetry is present at t=0, its appearance
in the archive is not evidence that bilateral symmetry pays.

**A founder is one cell, or one cell and a beating appendage.**

| Founder | Parts | Can it move? |
|---|---|---|
| Blob | 1 body cell | No |
| Flagellate | 1 body cell + 1 link | Yes — the link *is* the tail |

Drawn half and half. A link is a full part with its own tissue, upkeep and shape, and it does
not require a child, so one link hanging off one cell is a flagellum rather than an incomplete
two-cell creature. Everything else — branching, symmetry, limbs, recursion, more than one
strategy in one body — has to be discovered, priced, and kept because it paid.

**Founders draw only from the earning cell types** (`Photosynthetic`, `Absorptive`,
`Consumer`), weighted 2:1:1. `Structural` and `Link` acquire nothing, so a founder built from
those alone has zero income against nonzero upkeep and starves with certainty in every world —
compute spent to produce a corpse. Structure remains one mutation away; it is simply not where
a lineage starts.

**The half that cannot eat yet is the point.** At t=0 there is no nutrient in the water and no
corpse to bite, so absorptive and consumer founders earn nothing and die. Their tissue becomes
the first nutrient anything has ever had (§5A.6 returns tissue to the pool). **The doomed half
of generation zero is the primordial soup**, and it is what makes the other two strategies mean
something by generation two. Weighting photosynthesis double is not a claim that it is the best
strategy — that is the world's to decide, and handing it over outright would make "plants came
first" an arrangement rather than a finding. It is only the ratio that keeps generation zero
from being entirely stillborn.

**Nothing filters founders for viability.** `GenomeFactory.RandomViable` rejects genomes with
no degrees of freedom, which every blob has. Under §5A stillness is a way of living rather than
a defect: a photosynthetic blob paying its bills in the light is a plant, and refusing to spawn
it would be an exogenous judgement about what deserves to exist — the exact thing this section
removes.

**Measured:** over 500 seeds, 52% blobs / 48% flagellates and 52.8% / 24.6% / 22.6% across the
three strategies. A single founder node reaches 32 nodes and 16-part bodies within 2,000
births under mutation alone (`FounderTests`), which is the load-bearing check: if founders are
this small, complexity must be reachable from them or the world never becomes interesting.

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
| **Neural** | Nothing | Hosts neurons cheaply. Makes a *brain* a morphological trait — see below |
| **Link** | Nothing *by default* | The only type that may carry a joint, so the only source of motion. See below — the default is now a setting rather than a rule |
| **Buoyancy** | Nothing | Holds gas: cancels some of its own weight, and is billed per unit of lift whether or not it is useful. The only type that can choose a depth without swimming — D049 |

**Buoyancy tissue, and why it is not a muscle.** Gas vesicles are the oldest behaviour in the
record — cyanobacteria hold a depth in a light gradient with them, three billion years before
anything had muscle. Under §5A.2d the water column has light at the top and matter at the
bottom, so **depth is very nearly a creature's whole strategy**, and until D049 there was no
organ for choosing one. The joint was being asked to do that job, which is a Cambrian answer to
an Archean question and is why it never paid (logbook/0027, logbook/0030).

**Lift, not density.** Being *heavier* than water is already free — §5.2's neutral buoyancy and
D044's `TissueExcessDensity` — so the thing that needs an organ, a price and a genome field is
going **up**. `MorphNode.Lift` carries it, evolvable per part and zero on every other cell type
by validation.

**In multiples of the sink it cancels, not in kg/m³** — D050. 1 is neutral buoyancy, 2 rises as
fast as a bare body falls, and `FluidEnvironment` applies `excessDensity × (1 − lift)`. Absolute
units made the genome's meaning depend on a world constant §5.2 flags as unmeasured, and the two
were 25× to 2,500× apart: the weakest bladder a creature could be born with was already a runaway
and neutral buoyancy was not a reachable value, which is the one thing gas vesicles are for
(logbook/0034).

**The water column has a top.** A part at or above y = 0 gets no *upward* net force, because the
water it displaces has run out; sinking across the line is untouched. Without it the world was an
unbounded ray — `LightModel.IrradianceAt` returns a constant above 0 and `NutrientField.LayerOf`
clamps to layer 0, so every metre gained was physically identical to floating at the waterline
while still costing upkeep to hold. Measured: buoyant creatures at +155.8 m in a band 23.7 m deep.

**Charged for whether or not it is doing anything**, exactly as a link is charged for capacity.
Free lift runs away to whatever ceiling exists and returns every creature to the surface, which
is the world §5A.2d was built to escape. It earns nothing, like structural tissue: a bladder has
to pay for itself entirely through where it puts the rest of the body, which is the same shape
of bet as a fin.

**Link tissue and whether a muscle may earn.** A link was given no income for the same reason
`Structural` has none: it is not a feeding organ. The consequence was not noticed for four months.
Because intake scales with volume, making one part of a two-part creature a link forfeits **1.30 W**
of photosynthesis — more than the link's own upkeep (0.51 W) and idle capacity charge (0.40 W)
together. A joint's *best possible* case is therefore costing nothing and earning nothing, which
loses to a photosynthetic part of the same volume at every price. Four sweeps of the other two terms
found nothing alive with a joint, and could not have (D042, D043, logbook/0026, logbook/0027).

`LinkCell.PhotosyntheticEfficiency` makes this a measured parameter (§5A.10) rather than a rule,
expressed as a fraction of `PhotosyntheticCell.DefaultEfficiency`. **It defaults to 0, which is this
section exactly as written above**, so every number recorded before 2026-08-28 stands unchanged.

The biological case for it being non-zero is that motility did not begin as inert tissue: a
flagellum is an organelle on a metabolically productive cell, not a segment bolted to one.
*Chlamydomonas* swims and photosynthesises with a single cell, and the choanoflagellates animals
descend from feed with the collar that drives the flagellum. A muscle that earns nothing describes a
large animal, not the first thing that moved. ⚠ At a fraction of 1.0 the trade-off disappears and a
joint drifts neutrally rather than being selected, which is as uninformative as never affording one.

**Why `Neural` exists, and it is this section's own argument turned on cognition.** The thesis
above is that energy acquisition is a property of a part rather than of a creature, and that
this is what makes trophic strategy a morphological trait the §4.1 graph already encodes. The
identical argument applies to thinking. With neurons distributed uniformly across parts and
`Genome.GlobalBrain` owned by no part at all, a brain has no volume, no location, and cannot be
damaged — so **brain size and brain placement cannot evolve, because there is nowhere for a
brain to be.** Cephalization, one of the most universal patterns in animal evolution, was
structurally unreachable.

`GlobalBrain` was also the one cost in §5A attached to no tissue: joules spent, nothing to bite.

**Every cell hosts a small baseline of neurons; neural tissue makes them cheaper.** The
baseline is a nerve net, which is what cnidarians have, and it exists to avoid a valley: a
flagellate that needed neural tissue before its joint could be driven would require two
mutations that are each useless alone, and populations do not cross those.

**Neural tissue discounts rather than gates** — it does not cap how many neurons a part may
carry, it reduces what they cost (§5A.2). Gating was rejected: it would couple genome
*validity* to part size, and under §4.5's extinction-by-shrinking parts change size constantly,
so a shrinking cell could invalidate a genome that was legal when it was written.

The discount produces cephalization as an economic outcome rather than a rule. §4.3 requires a
neuron to sit on the part whose joint it drives, and it reads that part's sensors — but neurons
are cheaper where the neural tissue is. Motor neurons stay out at the muscles; everything else
migrates. And because capacity follows volume while *latency* follows topology (§4.3 reaches a
neighbour in one node per step), one large neural cell and several small ones of the same total
volume behave differently:

- **Centralised** — neurons reach each other in one step, but sit far from the sensors and
  joints they serve.
- **Distributed** — short paths to local sensors and joints, ganglia slow to reach each other.

That is octopus against vertebrate, and it is a trade-off selection resolves rather than one we
pick. *(Author's inference; no source in the corpus addresses neural tissue as an evolvable
part type.)*

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

### 5A.2b Competition for light — where carrying capacity comes from ✅ measured

> **§5A.2's knob decides how fast, not whether.** Sweeping the metabolism-to-photosynthesis
> ratio over 400× found no setting where a population held steady, because there was none to
> find: with irradiance a function of depth alone, a creature's income does not depend on how
> many others exist, so every creature above break-even accumulates surplus at a fixed rate and
> breeds on a fixed period regardless of the crowd. That is a linear birth process. It grows
> without bound above break-even and goes extinct below it — a step function, not a transition
> (logbook/0011).

What was missing is **density dependence**, and the physically honest source of it is that the
sun is finite. Sunlight arrives as watts per square metre of surface, so a world of finite width
receives finite power, and light one creature absorbs is light that never reaches whatever is
below it.

Total photosynthetic income across the whole world therefore cannot exceed
`surfaceIrradiance × worldArea`, whatever evolution discovers. **Carrying capacity stops being a
number we chose and becomes a consequence of the world having a size** — which is the same class
of constraint as the momentum invariant in §11.2, and preferred for the same reason: it is a
conservation law, not a tuning parameter.

**The mechanism.** The water column is divided into layers. A layer holding total projected area
*L* over horizontal area *A* intercepts a fraction 1 − e<sup>−L/A</sup> of the light passing
through it, and its occupants share that in proportion to their own lit area. This is not an
analogy to Beer–Lambert but the same derivation for randomly-placed absorbers, which is why real
ocean optics treats chlorophyll and water as two terms of one exponent. The naive `min(1, L/A)`
would claim two creatures of half a layer's area shade it completely — true only if their
shadows never overlap — and would put a hard edge in the model at exactly the density where
competition begins to matter.

Three properties are load-bearing:

1. **It reduces to §5A.4's plain depth model exactly**, continuously, as the world empties. What
   is stored per layer is a *multiplier* in (0, 1] rather than an irradiance, so the depth term
   stays exact and occupancy can only ever take light away. Storing an irradiance instead means
   everyone in a layer receives the light entering its top, so drifting into an occupied layer
   *raises* your income — a free-energy source manufactured by the discretisation, in precisely
   the direction §11.2 says to look.
2. **Non-photosynthetic tissue shades too.** Every part casts a shadow whether or not it can use
   the light, and light a structural part intercepts is lost. That is what makes bulk cost
   something in a crowd, and it prices a canopy correctly: a creature holding a large opaque body
   above a photosynthetic one is taking food from it.
3. **Lit area is a projected area, not a surface area.** A part's lit area is a quarter of its
   surface, which by Cauchy's formula is exactly its orientation-averaged projected area — so the
   number a creature earns on is the same number it denies to whatever is beneath it. Nothing can
   collect light it does not also block.

**The measured transition.** With attenuation 1/e at 12 m, a 400 m² aperture, and default cell
upkeep, three seeds each over 20,000 s of world:

| Surface irradiance | Outcome |
|---|---|
| 4 – 24 W/m² | Nothing reproduces. Max generation depth 1, floor firing continuously |
| **32 W/m²** | **Lineages establish — median depth 15–79, floor falls silent** |
| 48 – 400 W/m² | Establishes; more light buys fewer, larger creatures, not more of them |

So the ratio is **located between 24 and 32 W/m²** for these upkeep rates, and the analytic
break-even for a 0.3 m photosynthetic cube — where lit area × irradiance × efficiency equals
volume × upkeep — is 24 W/m². The measurement and the arithmetic agree, which is the point at
which either is worth believing.

**Above the transition, nothing runs away.** The light is fully contested and the population is
regulated by that, not by us. What light caps is **living biomass**, not head count: in steady
state the world's metabolic burn equals its light income, and burn is proportional to tissue.
Two worlds of equal biomass can hold forty giants or eight thousand motes.

~~More light produces *bigger* creatures rather than more of them, because a larger body shades
its competitors.~~ ⚠ **Superseded by §5A.2c.** That was true, and it was true because an
offspring's body cost its parent nothing to build — so the only limit on size was shading, and
shading rewards being large. Once tissue has to be paid for, the same worlds hold many small
creatures instead of a few enormous ones, at the same total biomass. The observation was
correct; the cause was a missing price, not a property of light.

~~⚠ The world has no length scale, and this exposes it. At 400 W/m² the surviving creatures carry
thousands of square metres of surface in an aperture 20 m across.~~ **Largely resolved by
§5A.2c**, and not by adding a length scale. With bodies costing what they are worth, total shadow
across the population fell from 10–290× the world's area to **0.3–1.3×** — bodies are now
world-scale because a giant is unaffordable to construct, not because anything measured the world
and forbade one. Spatial extent still arrives with the physics simulation at Milestone 4, and it
is still the honest constraint; it is no longer load-bearing for plausibility.

### 5A.2c Bodies cost, corpses feed — the loop that closes ✅ implemented

§5A.2 promised an auditable world: sun in, metabolism out, everything else conserved. Until
Phase 2 the middle clause was aspiration. Two things were free.

**A body was free to build.** A parent paid its offspring's endowment and a fixed overhead, and
nothing else — the same price for a mote as for a whale. Only upkeep ever noticed the
difference, and upkeep is paid later by the offspring rather than now by the parent. Building
tissue is the dominant cost of reproduction in anything real, and its absence made offspring
size a lever with no price on it, which §5A.1 says is exactly what evolution takes to the limit.

**A corpse was worth nothing.** Death removed a creature and its body went nowhere, so the
detritus niche §5A.4 depends on had no fuel and `Consumer` had nothing to eat.

Both are fixed by one number. **`CellType.TissueEnergyPerCubicMetre` is what a body is worth,
and it is therefore what a body costs** — a parent pays it to build each offspring, and the
nutrient pool receives it when that offspring dies. The two must be the same figure or a
birth-and-death cycle creates or destroys energy, so both call one method.

**The nutrient pool is a stock, not a density.** `AbsorptiveCell` previously read a density from
its surroundings and converted it to joules with nothing anywhere being reduced — the same
infinite-subsidy shape that made the population unbounded when light worked that way (§5A.2b).
It had not bitten only because the density was always zero. `NutrientField` holds joules per
layer, feeding removes them, and demand above supply is shared proportionally exactly as light
is.

**Detritus sinks, and that is what makes the deep a niche rather than only a dark place.** Light
falls off downward; food falls *toward* the dark. The two gradients oppose, so neither strategy
wins everywhere — and nobody arranged it. It follows from photosynthesis needing the surface and
corpses having mass. The world therefore gains its first vertical bound
(`WorldDepthMetres`): light can attenuate forever, but a sinking pool needs a floor or energy
falls out of the world, and vanished energy is what the audit exists to notice.

**Carrion is the predator valley's bridge, and it now exists.** §5A.3 argues a `Consumer` part
costs upkeep from the mutation that creates it and pays nothing until perception, directed
movement and prey density coexist — a valley too wide to cross. `ConsumerCell` could only feed on
`TissueContact`, which needs physics and does not arrive until Milestone 4, so consumers earned
exactly nothing. They can now scavenge the detritus pool at `CarrionYield`. Detritivore →
scavenger → predator is a gradient the population can actually walk.

**The audit is now an equality, not a check.** `EnergyIn − EnergyOut == StandingJoules`, where
standing energy is creature reserves plus creature bodies plus detritus. Sunlight and floor
spawns are the only sources; metabolism, reproductive overhead and the loss on every feeding
transfer are the only sinks. Measured residual over a 300 s run with births, deaths and feeding:
**0.0000%**.

**What it did to the world.** At 96 W/m², where the population previously settled at a few dozen
giants, the same world now holds **551 creatures at t=10,000 s and 835 at t=20,000 s while living
biomass goes 111 m³ → 129 m³** — head count up half again over a doubling of elapsed time, tissue
up 16%. Total tissue is capped by light exactly as §5A.2b says; what changed is that it is now
divided among many small creatures rather than a few enormous ones, because a large offspring is
finally expensive to build. That is the pressure §5A.2b was missing.

The transition itself did not move: **32 W/m², identical across three seeds**, and agreeing much
more closely between them than before. What changed is everything above it.

⚠ **Detritus accumulates on the sea floor and nothing removes it.** 80–93% of all detritus ends
up in the bottom layer, where no lineage has yet evolved to live.
It is a sink and not a source, so conservation is unaffected — but it grows without bound, and
whatever first reaches it inherits a very large bank. Real remineralisation would return it
slowly to the water; whether that matters here is unmeasured and deliberately not guessed at.

### 5A.2d Matter — what the producer consumes ✅ implemented

Until D048 the producer consumed nothing. `PhotosyntheticCell.Acquire` returns light and draws
no pool, so **nothing a creature did made its own position worse.** The only thing a producer
emitted was shade, which harms creatures below it and never itself. There was no negative
feedback anywhere on occupying the best spot, and the consequence ran through everything
measured here: the depth axis was a ramp with its maximum at the boundary rather than a
landscape with an interior optimum, so every run sorted to the surface and stayed, and depth
was never worth buying.

The real ocean's vertical structure *is* that feedback. Light is at the top and nutrients are
at the bottom, and **the surface is nutrient-poor because producers live there and strip it**;
corpses sink, remineralise at depth, and the deep stays rich and dark. Two opposed gradients,
one of them made by the organisms.

**Light is energy; matter is matter.** They are separate currencies and are never added.

| | source | sink | conserved by |
|---|---|---|---|
| energy (J) | sunlight, founder endowment | metabolism, reproductive overhead | §5A.2's audit, a hard equality |
| **matter** | seeded once at `InitialMatterPerCubicMetre` | nothing — it is only ever moved | `World.StandingMatter` |

- **Reproduction requires matter as well as energy**, `MatterPerTissueJoule` per joule of the
  child's tissue, drawn from the parent's own layer. No amount of sunlight builds a daughter
  cell without nitrogen and phosphorus. §5A.6 has no growth, so reproduction is the only moment
  tissue is ever created and therefore the only place this can be charged.
- **A matter-starved world does not kill its inhabitants, it stops them breeding** — which is
  what happens to a nutrient-limited bloom, and is why the charge is here rather than in upkeep.
- **Death returns it** to the layer the body died in, whence it sinks. Floor founders are exempt
  because they never paid; crediting them would mine matter out of nothing.
- **`World.Matter` is deliberately outside `StandingJoules`.** Matter is not energy, and folding
  it into §5A.2's audit would let the books balance by counting a different substance — the
  exact failure that audit exists to catch.

**Measured.** A 200 W/m² world at `MatterPerTissueJoule` 0.5 from 1.0/m³:

| t | alive | matter at surface | matter deep | conceptions blocked | floor spawns |
|---|---|---|---|---|---|
| 100 | 40 | 0.802 | 1.136 | 0 | 48 |
| 500 | 219 | 0.009 | 1.393 | 4,707 | 0 |
| 1,200 | 931 | **0.004** | **1.151** | 114,450 | **0** |

A ~300× vertical gradient, built by the creatures out of a uniform start. The population still
grows, because mixing resupplies the surface from below — so **primary production is now limited
by vertical nutrient flux**, which is the constraint that governs it in the real ocean. And the
floor stops firing entirely, which by D021 is the only statement that this world is alive rather
than being kept alive.

⚠ **The tension it creates has no answer yet.** Creatures sit at −2.6 m in stripped water while
the matter is at depth, and nothing in §5A.1 can move them there. That is the selective pressure
D049's buoyancy cell exists to meet, and it is why D049 is sequenced after this rather than
before: buoyancy in a world whose optimum is at the surface collapses to "everyone floats".

⚠ `MatterPerTissueJoule` and `InitialMatterPerCubicMetre` are unmeasured (§5A.10). The blocked
count above is very large relative to the population, which says the ratio binds hard; whether
it binds *too* hard is open. Zero disables the mechanism and reproduces every result recorded
before D048.

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

**Built, and it is off by default** — `LightModel.DayNightAmplitude` and `DayLengthSeconds`
(D035). The cycle is **mean-preserving**: `SurfaceIrradiance` remains the daily mean and the
amplitude modulates around it, so 0 reproduces the acyclic world exactly and switching it on
does not move the §5A.2 calibration. That is what let it land as one unknown rather than two.

**A cycle alone does not move the best place to be, and the first runs say so.** Irradiance stays
monotonically decreasing in depth at every hour, so light on its own always favours the surface.
What a cycle moves is the *balance* against the deep income — and in the runs so far food income
is **0% of all income**, because absorptive cells are effectively unreachable from a founder at
`MutationRates.CellTypeChance` (logbook/0020). With one income term there is no optimum to track
and nothing migrates. The mechanism is built; what it needs is a second income worth having.

**And mean-preserving is not survival-preserving.** Death is a threshold, and the threshold of an
average is not the average of a threshold: at full amplitude, deaths roughly tripled and the
population fell by about 40% on identical settings and seeds. The energy budget is untouched and
the world is meaningfully harder. That was the *"can anything survive the trough"* half of the
original objection to adding a cycle at all, and it is real.

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

**A population floor, and it is the only thing that ever creates a creature.** When the living
count falls below a configured minimum, fresh founders (§5A.0b) are spawned until it is met.
This subsumes world seeding rather than sitting beside it: at t=0 the population is zero, the
floor fires, and generation zero appears. One mechanism, exercised continuously, rather than an
initialisation path that runs once and is therefore tested once.

Refills are **fresh founders, never descendants of survivors.** Choosing who repopulates is
selection performed by us, which is what §5A exists to remove. And it is not a reset: the
nutrient pool left by everything that died persists, so refills enter a world *richer* than the
original founders did — extinction followed by radiation into an empty, nutrient-loaded ocean.
They arrive as a trickle rather than a cohort, so the refill rule cannot itself manufacture a
boom-and-bust oscillation.

> **The floor is an instrument, not a safety net, and the distinction decides whether any
> number from a run means anything.** A floor that fires regularly means the world is not
> sustaining life — *we* are — and the run would look healthy while doing it: stable
> population, births, deaths, lineages accumulating, every figure consistent with a working
> ecosystem and every one of them propped up. So a floor spawn is recorded as its own event
> type in `lineage.jsonl`, never indistinguishable from a birth, and **a floor that keeps
> firing is a failed world, reported as failed.**

**No ceiling of the same kind.** The opposite failure (§5A.7's photosynthetic mat) does not go
extinct, it explodes, and §5A.9 puts real time at roughly 200 creatures. But killing creatures
to stay inside a compute budget is selection by us of the worst sort — arbitrary, invisible in
the lineage record, and biased towards whatever the cull happens to reach first. A population
ceiling is therefore a **hard stop with a loud report**, not a silent cull.

### 5A.6b Is the world alive? — generation depth

**Generation depth** is the number of reproduction events between a creature and the founder it
descends from. It is free: §5A.6 makes reproduction asexual and mutation-only, so every birth
is exactly one mutation event, and depth is a counter inherited from the parent. Because of
that it measures two things at once — how many generations have passed, and how far the genome
has drifted from its founder.

~~**Minimum depth among the living is the definition of self-sustaining.** If it is greater than
zero, no creature currently alive is a floor spawn: everything here got here by being born.
That is a single integer with no threshold to choose and no window to average over, and the
moment it first rises above zero and stays there is a dated event worth recording — *the point
at which life became self-sustaining.*~~

⚠ **Superseded — it is a true statement and an unreachable one.** Nothing dies of age: §5A.6
kills only at zero energy, so a founder whose income covers its upkeep never dies at all. A
handful of immortal generation-zero photosynthesisers pin the minimum at zero permanently, in
worlds that stopped needing the floor thousands of seconds earlier and are visibly running
themselves at median depth 78 (logbook/0011). The test was measuring immortality, not dependence.

↻ **Un-superseded, 2026-08-26 — by the mechanism the paragraph above names.** §5A.6c gives
creatures a metabolism that ages, so a founder whose income covers its upkeep no longer covers it
forever. Minimum depth now rises above zero and keeps rising: replicated across three independent
seeds, first crossing at t=3,500–4,700 s and reaching 3, 3 and 6 by the end of the run, in worlds
carrying 619–1,919 creatures with 40–53% mortality — against 16% and a permanent zero in the
immortal control (D038, logbook/0023). **The struck claim is restored: minimum depth above zero
means no creature alive is a floor spawn, and it is the stronger statement.** How long the floor has
been silent stays, because it is the cheaper reading and answers the same question sooner.

**What replaces it: how long the floor has been silent.** That is literally the question — is
anyone being handed life by us right now — and unlike minimum depth it is reachable. It needs a
window, which minimum depth did not, and the window has to be long against the generation time of
whatever is living there; so it is supplied per run rather than defaulted. Minimum depth is still
reported and is still the stronger claim if it ever rises, which it will as soon as anything can
die of something other than starvation.

The distribution matters more than the mean, because a takeover and a healthy world can have
identical means:

| Shape | Reads as |
|---|---|
| everything at 0–1 | Too harsh. Nothing reproduces; the floor is the only source of creatures |
| climbing fast, population at the ceiling | Too generous — §5A.7's mat |
| max high, **median near zero** | One lucky lineage among floor spawns. Life possible but rare |
| min and max close | A bottleneck or a takeover: all descent from one recent ancestor |
| **wide spread** | Deep lineages coexisting with young ones — a working world |

**This is how §5A.2's ratio gets calibrated, and it needs no prior guess at the right value.**
There is a phase transition: below some metabolism-to-photosynthesis ratio depth pins at zero,
above it lineages establish. Sweep the knob and the transition locates itself — §5A.2b reports
where it is. That is what `RunConfig` and its hash exist for: a swept parameter that silently
fails to reach the thing it configures has already happened twice here (logbook/0007,
logbook/0008).

⚠ **A sweep is only as good as its runs are long.** The first pass ran each world for 4,000 s
and reported a stable floor-fed world at 48 W/m²; that world was on a clean exponential and blew
past the population ceiling at t=5,303 s. **A truncated run cannot tell a steady state from an
exponential caught early**, and the shorter the run the more confidently it reports the wrong
answer. The other half of the same lesson: the ceiling must be far above the carrying capacity
being measured, or dense-but-bounded worlds read as runaways — 96 W/m² tripped a ceiling of
1,500 while being perfectly regulated.

Paired with **age at death** and **depth per wall-clock hour**, because depth alone can be
fooled: a world where creatures reproduce instantly and die instantly posts healthy depth and is
broken. Depth per hour is the actual evolutionary clock and decides whether a run of a given
length can produce anything at all.

⚠ **Depth measures reproduction, not adaptation.** A world can post excellent depth statistics
and be a treadmill — lineages turning over forever with nothing improving. That is §5A.7's
plateau and this instrument cannot see it. *Is the world alive* and *is the world interesting*
are different questions, and conflating them is how a run looks healthy for twelve hours and
produces nothing.

### 5A.6c Senescence — ageing as an energy phenomenon

**Death at zero energy is the only death (§5A.6), and by itself it makes a creature whose income
covers its upkeep immortal.** §5A.6b already records the consequence and treats it as an
instrument problem: a handful of generation-zero photosynthesisers pin minimum depth at zero
forever. It is an ecological problem as well. Measured: **98 deaths against 1,164 births**, an 8%
death rate, with the literal t=0 founders still alive at t=3,500 (logbook/0023). Selection needs
differential *mortality* as well as differential *reproduction*; a successful lineage that is never
replaced, only added to, is a world in which almost nothing is selected.

**`SenescenceDoublingSeconds` raises the cost of being alive rather than killing anybody.** A
maximum lifespan would be exogenous — us deciding how long a creature ought to live, which is what
§5A.0 exists to remove. At age *t* the wear factor is `1 + t/T`. Death stays exactly where §5A.6
puts it, at a reserve of zero: an old creature starves, and how long that takes depends on how well
it earned, which is the world's answer rather than ours.

**It moves both sides of the ledger, from one number.** Upkeep and neural cost are multiplied by
the wear factor; income is divided by it. Costs alone would be the cheaper implementation and the
wrong biology — senescence is loss of function first and expense second, and a creature
photosynthesising at full efficiency until the day it starved would be an odd thing to call old.

**What falls is what a creature keeps, not what it takes.** `PoolDrawn` is unscaled: an ageing
population strips the larder exactly as fast while feeding itself worse, and the shortfall leaves
the world through the transfer loss §5A.3 already accounts for. Scaling the draw instead would make
ageing a form of restraint — a world of the old would deplete *less* than a world of the young.

**Linear, and not heritable.** Linear so the doubling time means something plain. Not heritable
because nothing here costs anything to repair, so an evolvable senescence rate would go straight to
zero and buy immortality free — a §11.2 free lunch arriving through the ledger. Making it evolvable
needs the disposable-soma trade-off, where repair competes with reproduction for the same joules,
and that is a larger design than a knob.

⚠ **Unmeasured (§5A.10), and default 0.** Zero is bit-identical to the world every earlier number
was measured in — the property D031 lost and D035 and D037 both kept deliberately.

### 5A.6d Margin, not break-even — what makes a trade viable

**A trade that exactly covers its costs founds nothing.** §5A.6 pays for offspring out of surplus,
so a creature at break-even survives indefinitely and never reproduces. Quoting a break-even density
therefore says where a trade stops losing money, not where it is worth doing, and the difference is
the whole question of whether a niche can be occupied.

**The comparable quantity is the margin**: net watts per cubic metre of tissue, divided into the
body's tissue cost to give **the seconds a body takes to earn its own replacement**. It is the
closest thing to a generation time this economy has, and unlike break-even it is comparable between
trades that acquire energy in completely different ways — photosynthesis scales with lit area,
absorption with volume, and the two have no common denominator until they are both expressed as
surplus per body.

Measured at 64 W/m² (D039, `TrophicMarginTests`):

| trade | conditions | net | earns its own tissue in |
|---|---|---|---|
| photosynthetic | −2 m, full sun | 1.063 W | 470 s |
| absorptive | −2 m, 10 J/m³ | 1.000 W | 500 s |
| photosynthetic | −45 m | −2.887 W | never |

⚠ **Any claim in this document of the form "X is not viable because the world only produces Y" is
suspect until restated as a margin.** §5A.2c's absorptive break-even of 8 J/m³ was quoted three
times as the reason detritus feeding could not work; the world reached 12.3 J/m³ unaided and the
trade turned out to be at parity with photosynthesis (logbook/0024).

### 5A.7 Failure modes

| Failure | Why it happens | Defence |
|---|---|---|
| **Photosynthetic mat** | Sunlight covers upkeep, so nothing must move | Basal metabolism above peak photosynthesis at depth; depth/light gradient (§5A.4) |
| **No carnivores ever** | Predator valley; trait purged before it can pay | Carrion feeding, plus cell-type mutation (§5A.3) |
| **Free-energy takeover** | A physics exploit becomes an unlimited food source | Global energy audit (§5A.2) and §11.2's conservation checks |
| **Efficient nothing** | With no reachable strategy, minimising cost is the winning move | Watch for populations with near-zero work and near-zero displacement. [C18 §3, p.13] documents exactly this under directed search: artefacts *"which may be mistaken for the existence of highly energy efficient locomotion strategies"* |
| **Bloat (body)** | Any resource with no price | Every part and every neuron costs, including `Structural` (§5A.1) |
| **Unbounded population** | Income independent of density: nobody's earnings fall as the crowd grows | Finite sun over a finite aperture (§5A.2b). Without it there is no calibration that helps |
| **Unbounded body size** | Half-extent mutation is a multiplicative random walk, and only §4.5's lower tail was absorbed | `MaxPartVolume` — extinction by growing, mirroring extinction by shrinking (§4.2) |
| **The infinitely thin sheet** | Income scales with area, upkeep with volume, and volume does not bound area | `MinPartHalfExtent` keeps the arithmetic representable; what actually bounds a body is the light running out (§5A.2b) |
| **The immortal nothing** | A genome that develops into no parts has zero income and zero upkeep, so death-at-zero never fires | Stillbirth: a bodyless creature is refused at admission and counted (§5A.6) |
| **The free body** | An offspring's tissue costs the parent nothing, so offspring size is a lever with no price | A parent builds each body out of its own reserve (§5A.2c) |
| **The refunded meal** | A feeder keeps a fraction of what it takes; leaving the rest in the pool makes every meal a partial refund | `CellIntake` reports what was drawn as well as what was kept, and the difference leaves the world |
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

~~Spike 01 (§11.1) measured 128 actuated creatures at 1.945 ms/step … applying a 3× penalty
still leaves 128 creatures at ~1.7× real time, and roughly 500 at about half real time. …
**Expensive:** creature count and DOF count. The wall is population, somewhere around
500–1000, and it is physics rather than ecosystem bookkeeping.~~ — **superseded.** That was
Spike 01's figure with a guessed penalty applied for drag and self-collision, both of which
now exist. `ThroughputSurvey` measures the real configuration: self-collision on, §5.2 drag
applied, mixed shapes, creatures tiled at 100 m.

~~At 512 creatures drag is 88% of the step … real time holds to about **200 creatures** as the
code stands.~~ — **superseded by the optimisations below**, which were listed there as deferred
to Milestone 4 and have now been done. The measurement that produced that text is kept in
logbook/0014, because the attribution it established is still the reason any of this happened.

| creatures | ms/step | drag ms | physics ms | µs per creature | real time |
|---|---|---|---|---|---|
| 1 | 0.069 | 0.014 | 0.054 | 69.0 (1.00×) | 144.97× |
| 8 | 0.312 | 0.109 | 0.203 | 39.1 (0.57×) | 32.01× |
| 32 | 0.806 | 0.202 | 0.604 | 25.2 (0.37×) | 12.41× |
| 64 | 0.991 | 0.293 | 0.699 | 15.5 (0.22×) | 10.09× |
| 128 | 1.465 | 0.588 | 0.877 | 11.4 (0.17×) | 6.83× |
| 256 | 2.760 | 1.343 | 1.417 | 10.8 (0.16×) | 3.62× |
| 512 | 6.446 | 3.521 | 2.925 | 12.6 (0.18×) | 1.55× |

**Real time now holds to 512 creatures**, against about 200 before. The step is 4.0× faster at
that population and the drag loop 6.4×, and no force changed: `DragEquivalenceTests` holds the
new path against a frozen transcription of the old one and measures them equal to 6–9×10⁻⁸ of
the panel-force scale, which is float epsilon.

Three changes, none of them physical:

- **The sum runs in the part's own frame.** Rotating each panel's normal and centre into world
  space is two quaternion rotations per panel — 48 for a box. Rotating the two inputs in and the
  two results out is four per *part*, and gives the same numbers, because a rotation preserves
  the dot product and `Rᵀ(ω × Rc) = (Rᵀω) × c`.
- **Panels are built once**, at a creature's first step, instead of regenerated through a virtual
  call on every part on every step. A part's local geometry is fixed at development.
- **The arithmetic phase is spread across cores**, between a serial gather and a serial apply,
  because Unity permits neither `Transform` reads nor `AddForce` off the main thread.

**What the remaining cost is, and why this is the place to stop.** At one creature — below the
threshold where work is spread out, so the first two changes acting alone — the drag loop went
0.053 → 0.014 ms, **3.8×**. At 512 the same loop is 6.4× faster, so parallelising 2,871 parts
across 24 cores bought only a further **1.7×**. That is not a bad implementation of parallelism;
it is what happens when little of what remains is arithmetic. The gather and apply phases are
per-body Unity interop — four property reads and two force calls, 2,871 times — and they are
serial by Unity's rules, not by ours.

So the drag loop is now **55% of the step against PhysX's 45%**, and the part of it that further
arithmetic work could touch is a minority of a minority. Vectorising the panel sum or integrating
faces analytically would be sound engineering aimed at almost nothing. The next real lever is
the timestep, which multiplies simulated seconds rather than dividing the cost of one.

Shape still matters: the loop is linear in panel count, and a capsule emits 40 panels to a box's
24. A population drifting towards capsules gets slower for a reason no other measurement shows.

PhysX parallelises across solver islands. Creatures in open water are mostly far apart and stay
separate islands, so that scaling survives; it degrades only where creatures touch, which here
means predation — rare and local.

**Cheap:** nutrients as plain advected data with a spatial hash (never rigidbodies — that is
the one trap, and would cost more than every creature combined); the current field; the light
cycle; brain evaluation at a few hundred creatures. Physics, at any population tried.

**Expensive:** per-body engine interop, which is now the largest term we own, and is the reason
the population ceiling moves with batching rather than with arithmetic from here.

### 5A.10 Open parameters

Every number below is unknown and must be measured rather than guessed. They are listed here
so that a value appearing in code without an entry here is visible as an unexamined choice.

**They all live in one place: `RunConfig` (`Evosim.Core`).** A constant compiled into a class
is a constant no run can vary, and an unmeasured constant that cannot be varied is an
assumption wearing the costume of a fact. So every number here is a settable property, cell
upkeep and feeding rates included, and a run is defined by a `RunConfig` instance.

`RunConfig.Hash()` is **§7's `configHash`** — the same object that parameterises a run is the
one that identifies it. Tests drive that by reflection over `RunConfig` and over each
sub-config it owns, so a tunable added without being folded into the hash fails immediately
rather than years later; the first run of that test found `MaxEdgesPerNode` already missing.
This matters most in the case where two runs produce *identical* output, which on this project
has twice meant a configuration change never reached the thing it configured (logbook/0007,
logbook/0008). A hash that differs while the results do not is the cheapest way to tell that
apart from a parameter that genuinely does not matter.

#### One declaration, four consumers

**A knob is declared once, with `[Tunable]`, and everything else is derived from it.** It used to
be written out four times — the property, `Hash()`, the JSON writer, the JSON reader — which at
this size is close to four hundred sites held in agreement by memory. `ConfigSchema.Of(config)`
walks the config and returns every declared knob with a dotted path, a group, a unit and a typed
get/set pair; the hash iterates it, the writer iterates it, and the reader iterates it and
*requires* a value for each. A knob that exists is therefore hashed, written and demanded on
load, and cannot be present in three of the four and missing from the fourth (D027).

**Reflection is used for discovery and never for ordering.** `Type.GetProperties()` guarantees no
order, so a hash taken in discovery order would be stable on one runtime and quietly different on
the next — §7's identity holding only until someone upgrades .NET. Entries are sorted by full
path, ordinal, so the order is a property of the names alone.

`EverySettableValueIsDeclaredTunable` is what makes this self-enforcing: it fails on **any** public settable
property carrying none of `[Tunable]`, `[TunableGroup]` or `[TunableRegistry]`. Not a list of
types we thought of — its own first draft checked `float`/`int`/`bool`/`string[]` and passed while
`RandomGenomeOptions.JointTypes`, a `JointType[]` that decides which joints a random genome may
draw, sat outside both the hash and the file. The two group attributes are how a property says it
is *not* a knob, and both are statements someone wrote down. Silence is the failure.

⚠ **The guard had the same hole it exists to catch.** It walked `RunConfig` and
`RandomGenomeOptions` only, so `DevelopmentLimits.MaxPartVolume` reached neither the hash nor
the JSON — a check against forgetting a tunable that had itself forgotten a whole object. The
sub-configs are now named in a literal list rather than discovered by reflection, because
reflection would have missed that case too and would fail the same way again: `Shapes` and
`CellTypes` are registries with no settable scalars and cannot be exercised this way, so an
automatic walk silently passes for whatever it did not think to look at. A list can only be
wrong in a way a human reading it can see. The test also fails if any named sub-config exposes
nothing checkable, so it cannot quietly stop covering one.

#### Every knob that moves energy

**Nothing that costs or earns energy may be a constant in a class.** Almost every number in §5A
is unmeasured, so a cost baked into code is an assumption nobody can test, and a cost that varies
without reaching `RunConfig.Hash()` is two different experiments filed under one identity.

| Knob | Where | What it decides |
|---|---|---|
| **Earning** | | |
| Surface irradiance, attenuation depth | `RunConfig.Light` | How much energy enters the world at all, and how deep it reaches |
| Photosynthetic efficiency | `PhotosyntheticCell.Efficiency` | Joules per watt of light per m² of lit area |
| World aperture | `RunConfig.WorldAreaSquareMetres` | Total watts arriving — the carrying capacity (§5A.2b) |
| Filter clearance rate | `AbsorptiveCell.ClearanceRate` | Water searched per m³ of tissue — what limits feeding in thin water |
| Filter assimilation | `AbsorptiveCell.Yield` | Fraction of captured matter kept. 1 by default, and that is a claim, not an omission |
| Bite rate | `ConsumerCell.BiteRate` | Joules swallowed per m³ per second — what limits feeding in rich water |
| Scavenge rate | `ConsumerCell.ScavengeRate` | Water searched for carrion. Separate from bite rate because they fail differently |
| Carrion / grazing / predation yield | `ConsumerCell.*Yield` | Fraction kept per target type. Carrion highest — the predator valley's bridge (§5A.3) |
| Founder stake | `RunConfig.FounderEnergyJoules` | The only energy besides sunlight created from nothing |
| **Spending** | | |
| Basal upkeep, per type | `CellType.UpkeepWattsPerCubicMetre` | What tissue costs to keep alive. Never zero (§5A.1) |
| Idle actuator cost | `LinkCell.IdleWattsPerNewtonMetre` | What capacity costs whether or not it is used |
| Mechanical work | `RunConfig.WorkCostMultiplier` | What a joule at the joint costs against a joule of sunlight |
| Neuron and connection cost | `RunConfig.NeuralCost*` | What thinking costs |
| Neural discount | `NeuralCell.NeuronsSupportedPerCubicMetre`, `.DiscountedCostFraction` | What a brain buys over a nerve net |
| **Moving between accounts** | | |
| Tissue energy, per type | `CellType.TissueEnergyPerCubicMetre` | What a body costs to build and is worth dead — one number, both (§5A.2c) |
| Per-offspring overhead | `RunConfig.PerOffspringOverheadJoules` | Burned, not transferred — what makes brood size a strategy |
| Detritus sink rate | `RunConfig.NutrientSinkMetresPerSecond` | Whether the deep is a niche or a graveyard |

Two things deliberately **not** tunable, and the distinction matters:

- **Offspring endowment and brood size are evolved genome traits**, not config. A creature that
  could choose its own would choose whatever is free.
- **Lit area is a quarter of surface area.** That is Cauchy's formula — the orientation-averaged
  projected area of any convex body — not a coefficient. A tunable there would be a licence to
  break geometry.

`EnergyKnobTests` enforces all of this rather than trusting it. It walks every cell type's saved
JSON, mutates each numeric field, reloads it and demands the hash move — proving writable,
readable and identifying in one pass — and separately drives each ledger term end to end to
confirm the knob actually reaches the arithmetic. Both faults it was written after had shipped:
`ConsumerCell`'s scavenging rate as a hardcoded coefficient of 1, and `LightModel` handed to the
world *beside* its config rather than inside it, so every run of the §5A.2b sweep — from the
extinct end to the runaway end — carried one identical `configHash` (logbook/0013).

⚠ **A knob in the hash is not necessarily a knob that does anything.** The declarative walk found
`RunConfig.CellTypeMutationChance` and `MutationRates.CellTypeChance` — two knobs for one thing,
differing tenfold, and only the second read by `Mutator`. Setting the first moved the `configHash`
and changed nothing about the run, which is the failure of §7 in its least visible direction: two
records claiming to be different experiments and byte-identical in fact. The dead one is gone, and
this remains the project's recurring fault in its third form. *Prove a parameter reached the thing
it configures* (logbook/0007, logbook/0008, logbook/0013).

- Basal metabolic rate per unit volume, per part type — ~~✅ **located**: the transition sits
  between 24 and 32 W/m² of surface irradiance against the default upkeep rates (§5A.2b)~~
  ⚠ **Superseded by embodiment.** That figure was measured with `workJoules: 0` and a height fixed
  at birth. With bodies costing tissue (§5A.2c) and swimming costing work (§10 M4), 48 W/m² no
  longer sustains a population at all — 6 births against 99 deaths, pinned at the floor — and 64
  W/m² is barely above replacement. Re-measure before quoting (logbook/0017)
- **What owning an actuator costs, `IdleWattsPerNewtonMetre` × `MaxLinkPower`** — ✅ **located**,
  and it decides whether anything in the world can move. The two knobs enter the ledger as a
  product, so only the product matters: at the shipped 2.4 W a jointed creature is at the edge of
  extinction (0–1 alive across three seeds), and below roughly 0.5 W joints persist and grow. The
  threshold is a fraction of what a body earns — a photosynthetic part makes ~2.3 W at 100 W/m²,
  so an actuator is affordable at well under a fifth of income and unaffordable at all of it.
  ⚠ **The default is on the unaffordable side by about 5×** and is deliberately left there until
  the choice of which knob to move is made (logbook/0017)
- Peak photosynthetic rate, and its falloff with depth — jointly with the above, this is the
  knob in §5A.2. ✅ **located** by the same sweep
- **World aperture** (`WorldAreaSquareMetres`) — new with §5A.2b, and the only thing setting
  carrying capacity. It scales total life in proportion; it does not change its density
- **Shading layer thickness** (`LightLayerMetres`) — a discretisation of who shades whom, so it
  wants to be near a creature's own size
- Neural cost per neuron and per connection
- Mechanical work coefficient — what a joule of ∫\|τ·ω\| dt is worth against a joule of sunlight
- Yield fractions in §5A.3, and the loss on transfer
- **Energy embodied in a cubic metre of tissue**, per type — new with §5A.2c, and doing two jobs
  at once: what a parent pays to build an offspring and what the world gets back when it dies.
  It sets how expensive a large body is to make, which is what decides whether a world holds a
  few big creatures or many small ones
- Nutrient sink rate — how fast detritus falls, which decides whether the deep is a niche or a
  graveyard. ⚠ And whether detritus should remineralise at all: it currently does not, so the
  sea floor accumulates energy nothing can reach (§5A.2c)
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
| | ⚠ **The join is done; the work term must wait for 6.** `World.Observe` carries height and mechanical work from the simulator into the ledger, and the audit holds at 0.0000% with it live. But billing work before a genome-specified controller exists exterminated every jointed creature in sixty simulated seconds ([D029](DECISIONS.md#d029), logbook/0015): with one shared test sine driving every creature, a uniform flap yields no net thrust, so work is a tax on *having* a body part rather than a price for *using* one. The current field is deferred with it, for the same reason — it displaces creatures that have no way to hold station | |
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
