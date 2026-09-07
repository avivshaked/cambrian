# 02 — From a tree of boxes to something that moves

[Piece 01](01-a-graph-that-grows-a-body.md) ended with development producing a `Phenotype`:
a tree of parts, each with a position, an orientation, a size, a shape and a joint. It is
pure data, with no physics engine anywhere near it.

> Everything below was written when every part was a box, and the title has kept the name. Parts
> can now also be spheres and capsules — what that changes about *swimming* is
> [piece 03](03-what-it-means-to-push-against-water.md); what it changes here is one line about
> colliders, marked where it appears. The scale trap and the effector conditioning are unaffected,
> because neither has anything to do with what shape a part is.

That separation is deliberate, and it deserves defending before we cross it. The temptation
is always to skip it, to have the graph traversal create physics objects directly and save a
data structure.

## Why growing and building are separate

Development is where recursive encodings go wrong. A limb attached at the wrong anchor, a
subtree scaled twice, a mirrored copy that mirrors the position and not the orientation:
these are ordinary bugs. Every one of them produces *a creature*. Something appears on the
screen. It has limbs. It moves when you actuate it. It is not the creature the genome
described, and there is no way to tell by looking.

Keep development free of the engine and it becomes ordinary code you can interrogate:

```csharp
Phenotype p = Developer.Develop(Fixtures.SelfLoopSpine(4));
for (int i = 0; i < p.PartCount; i++)
    Fixtures.AssertClose(new Float3(i * 1f, 0f, 0f), p.Parts[i].Position);
```

Four unit boxes attached face to face have centres one metre apart. That test runs in
under a millisecond, needs no editor, and fails loudly the moment a transform is composed in
the wrong order. Fifty-eight of them run in about a second.

The cost of this is real, because `Evosim.Core` cannot use the engine's own maths types. It
carries three of its own.

| type | what it replaces |
|---|---|
| [`Float3`](../src/Evosim.Core/Math/Float3.cs) | `Vector3` |
| [`Quat`](../src/Evosim.Core/Math/Quat.cs) | `Quaternion` |
| [`Mat4`](../src/Evosim.Core/Math/Mat4.cs) | the engine's matrix |

That is a few hundred lines of maths which already exists in the engine. It buys a feedback
loop measured in seconds instead of a minute, on the one part of the system where bugs are
invisible.

The rule is mechanical rather than aspirational: Core's assembly definition sets
`noEngineReferences`, so Unity refuses to compile it if anyone reaches for the engine.

## An articulation, not a pile of rigid bodies

Crossing into physics brings the first real choice: how the parts are connected.

The obvious route is a rigid body per part with a joint between each pair. It works, and
under the torques an evolutionary search discovers it works badly. Joints stretch, chains
sag, and a deep chain under load develops a rubbery quality that a search will happily
exploit as locomotion.

PhysX offers a better fit. An **articulation** is a kinematic tree solved as a single
system, rather than a set of bodies with constraints that the solver reconciles iteratively.
Joints do not separate under load because separation is not representable. This project's
creatures *are* kinematic trees, which development guarantees, so the structure matches the
solver.

That match is why [Spike 01](../spikes/01-articulation-body/results/FINDINGS.md) exists: the
question was whether articulations could be built and destroyed fast enough for an
evolutionary loop, since every evaluation needs a new creature. Build plus teardown of a
ten-part creature measured **0.335 ms** against a 15 ms budget, which settled it. It also
killed a planned object-pooling system that would have been pure complexity.

There is one ordering constraint. An articulation is assembled parent-first: adding a body
to an object whose ancestor already has one makes it a child link, and the hierarchy is
fixed at that moment. So the builder needs parents to exist before children.

It gets that for free. Development emits parts depth-first in pre-order, so a part's parent
always has a lower index than the part itself. A single forward pass over the list is
therefore correct by construction. That is a property of the data structure, rather than
something the builder has to be careful about.

## The trap: scale is not a transform you can undo

Here is where this actually went wrong. The bug is invisible and the fix is unobvious, which
earns it a section.

Unity's transform hierarchy compounds: a child's world scale is the product of its own scale
and all its ancestors'. Parts are parented to each other for the articulation, so if you put
each part's size on its transform, every part inherits its parent's size too. Boxes explode
down a chain.

The tempting fix is to divide it back out, taking the parent's scale and dividing the
child's by it, per axis. That is wrong, and it is wrong in a way that passes casual
inspection.

Scale compounds *in the parent's axes*. If the child is rotated relative to the parent, and
the parent's scale is non-uniform, the result is a **shear**. The child's box is not merely
the wrong size: it is no longer a box. A per-axis division cannot undo it, because a shear
is not a per-axis operation.

Measured error before the fix: **1.09 m** in position, **0.72 m** in size, on creatures
about a metre across. Wrong shape, wrong place, wrong proportions.

The fix is to refuse the premise. Size never touches the transform that positions a body:

| what | the rule |
|---|---|
| the body transform | stays at unit scale, always |
| the **collider** | carries the size: a `BoxCollider.size`, a `SphereCollider.radius`, a `CapsuleCollider`'s radius and height, depending on what the part is |
| a separate child object | carries the visual mesh, scaled for rendering |

Nothing compounds, because nothing is scaled. It is also faster and gives PhysX cleaner
inertia tensors.

The general lesson: when a framework's behaviour makes something awkward, cancelling that
behaviour is usually worse than arranging not to trigger it.

## Making it move without making it buzz

A creature with joints does nothing. Something has to drive them, and *how* it drives them
turns out to matter more than you would guess. What is at stake is not correctness. It is
whether the result looks alive.

The naive approach is to let a controller output become joint torque directly. Evolutionary
search then discovers two things almost immediately.

The first is that tiny parts make excellent motors. If torque is independent of size, the
best possible actuator is a very small body part: it has almost no rotational inertia and
the same torque available. Search finds this within a few generations and creatures develop
absurd little high-power nubs.

The fix, from the implementation this design follows, is to scale the applied torque by the
mass of the **smaller** of the two connected parts. This *"limits the maximum size of a
force to some reasonable value"* [K12 §2.2, p.5]. A small part now gets a proportionally
small torque, and the exploit disappears. It was never detected or forbidden; it stopped
being profitable.

That is nearly always the better way to close an exploit in an evolutionary system. A rule
you enforce is a rule the search probes for edges, and a rule that changes the payoff is not
worth attacking.

The second is that vibration beats swimming. A raw signal can change sign every timestep. A
creature that vibrates at the solver's frequency often accumulates net displacement through
numerical artifacts rather than physics. It scores well and looks appalling: a blur rather
than an animal.

The countermeasure is a ten-sample moving average on each effector, which *"eliminates
sudden large forces and also improves stability of the simulation"* [K12 §2.2, p.5]. It is a
low-pass filter: signals slower than the window pass through, the buzz does not. Ten floats
and a running sum per joint, against the alternative of a full PD controller with gains that
would need tuning per creature.

So the pipeline for one effector, in
[`EffectorDriver`](../unity/Assets/Evosim/Sim/EffectorDriver.cs), is:

```
raw signal  ->  clamp to [-1,1]  ->  average over last 10  ->  x mass of smaller part  ->  torque
```

Four steps, and three of them exist to stop evolution finding something clever and useless.

## And the torque has to push against something

There is a fourth exploit, and it is not one the search has to be clever to find. It is one
the *implementation* can hand over for free.

A muscle acts between two things. Your bicep pulls the forearm up and pulls back on the
upper arm, equally and oppositely. That reaction is what makes the force internal. It is the
reason you cannot lift yourself by your own belt, and the reason an astronaut cannot swim
across a capsule by waving.

The first version of this driver applied joint torque to the child link and nothing to the
parent. Every joint was therefore an *external* torque on the creature. Angular momentum
accumulated with no source, and creatures span up without bound until parts were moving at
tens of metres per second. Applying the reaction is three lines:

```csharp
Vector3 worldTorque = body.transform.TransformDirection(torque);
body.AddTorque(worldTorque);
_creature.Bodies[part.ParentIndex].AddTorque(-worldTorque);
```

What makes this worth a section rather than a footnote is that **it is invisible to almost
every check you would naturally write.** Nothing was NaN. Nothing came apart at the joints.
The geometry was correct. The creatures moved, and vigorously. Every assertion in the
headless test passed, and it took a human pressing Play and saying "that's just spinning" to
find it.

The assertion that does catch it is not a tolerance or a heuristic. With no gravity, no drag
and no contact, nothing external acts on a creature. **Its total momentum cannot change, no
matter what its joints do.**

So measure linear and angular momentum about the creature's own centre of mass, drive every
joint as hard as possible, and require both to stay at zero. With the reaction torque
applied, specific angular momentum comes out around 0.001 m²/s; without it, 1–2 m²/s.

That is the general shape of a good check in this kind of system. The question is not *"does
the output look reasonable"*. It is *"which conservation law would this violate if it were
wrong."*

Physical simulations have plenty of those lying around, and they cost almost nothing to
assert. Unlike a plausibility check, they cannot be satisfied by an impressive-looking
failure.

It matters here more than in most software because of what is coming. An evolutionary search
is an adversary that reads your bugs as affordances, as [`DESIGN.md`](../DESIGN.md) §11.2
says. Free momentum beats swimming, so selection would have found this within a few
generations and built every creature on it. The symptom, weeks later, would have been "the
swimmers don't swim", with a fluid model and a fitness function and a search algorithm all
queuing up to be blamed first.

## What is not here yet

The creature moves but does not swim, and the distinction is the whole of Milestone 2.

There is no fluid. Nothing resists motion, so the recorded speeds measure nothing except how
much torque happened to be applied against how much inertia. They sit well under 1 m/s, now
that the torque scale has stopped being two orders of magnitude too large. The scale factor
is still provisional, because the value that matters is the one measured against real drag.

There is no brain either. The signal driving those effectors is a sine wave with a phase
offset per joint, a stand-in until the neuron graph from piece 01 is actually evaluated. The
conditioning above is the real machinery; only the thing feeding it is a stub.

And when fluid does arrive it cannot be the cheap version. A simplified fluid model does not
merely cost accuracy. It *"collapses morphological diversity"* [C18 §4, p.28], producing
anatomical uniformity where fish and squid shapes should be. That would defeat the entire
purpose, since visual variety is the point.

## Sources

| Key | Used here for |
|---|---|
| `[K12]` | Torque scaled by the smaller connected mass; the ten-sample moving average, and the stated reasons for both |
| `[C18]` | Simplified fluid dynamics collapsing morphological diversity |

Measured figures come from [Spike 01](../spikes/01-articulation-body/results/FINDINGS.md)
and from the Milestone 1 smoke test, both reproducible from this repository.

One thing here is unsourced and mine: the account of *why* raw torque invites tiny-motor
exploits and vibration gaits.

[K12] gives the countermeasures and its reasons for them. The explanation of the failure
they prevent is my reconstruction, and the claim that vibrating creatures accumulate
displacement through solver artifacts is a hypothesis this project has not yet tested.

Anti-exploit checks are Milestone 2, and [`DESIGN.md`](../DESIGN.md) §11.2 has a concrete
checklist.

## Where it is

| file | what it does |
|---|---|
| [`PhenotypeBuilder.cs`](../unity/Assets/Evosim/Sim/PhenotypeBuilder.cs) | phenotype to articulation |
| [`EffectorDriver.cs`](../unity/Assets/Evosim/Sim/EffectorDriver.cs) | the four-step pipeline |
| [`Milestone1Smoke.cs`](../unity/Assets/Evosim/Sim/Editor/Milestone1Smoke.cs) | the geometry assertion that caught the shear |
| [`CreatureSpawner.cs`](../unity/Assets/Evosim/Sim/CreatureSpawner.cs) | the sandbox scene's one component |
