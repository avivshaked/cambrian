# Proposal: the water gives something back — added mass as a tensor, so a stroke can push

*Fable, 2026-09-24, from the swim probe of round 47 (`src/Evosim.SwimProbe`,
`logbook/specs/r47-read/swim/`, 0118's movement paragraph) and the owner's question that
started it: "I see creatures wagging, and yet they stay in place. how come?" For the round
after 48, not for 48, whose questions are about stomachs and would be confounded by a change
to every jointed body's fate. Absorbed into DECISIONS.md on ruling, then deleted.*

## What this is for

The probe put every jointed body of round 47 alone in still water under its own brain. Most
do not stroke at all, and the ones that do move two to six millimetres a second, half a
percent of a body length, against a current of a hundred. Hand-made strokes on the same
bodies, a lag down the chain or a fast out and slow back, add a factor of 1.2 to 1.6, and the
best body in three seeds reaches seven centimetres a second. The reason is the fluid model as
coded, `src/Evosim.Dynamics/Fluid.cs`: quadratic pressure drag on a link's leading panels
(`F = -½ ρ C_d A v_n |v_n| n`, trailing panels feeling nothing), the water's acceleration term
(D090), buoyancy, and added mass as one scalar folded into each link's mass at growth
(`Creature.Growth.cs`, `m + C_a ρ V`). Nothing else.

That model cannot reward a symmetric stroke. A paddle hinged to a body and flapped through
±θ feels a drag normal to its face proportional to the square of its normal speed; the
component of that force along the body is `sin θ · θ̇|θ̇|`, which over a symmetric cycle
integrates to zero exactly, because the paddle pushes as hard coming back as going out. Only
an asymmetric stroke (fast out, slow back) earns anything, and it earns the difference of two
squares, which is the millimetres the probe measured. Real swimming does not work by drag
alone: an undulating body or a flapping fin accelerates water sideways and sheds it behind,
and the momentum it gives the water is the momentum it gains. That is the reactive force,
Lighthill's elongated-body thrust at the tail is its best-known form, and it is what DESIGN
§5.3 and §5.4 already name as missing (C18's "inertial ... reactive forces due to the
acceleration of a body in a fluid"; U07's sign flip between a reaction-force model and a
fluid one). The added mass DESIGN §5.4 promoted to "the highest-value single improvement"
was built as a scalar, which gives a body more inertia in every direction alike and so
gives a stroke nothing to push on: a scalar added mass is a heavier body in a vacuum.

## The proposal: put the added mass in the inertia, as a tensor

A flat part in potential flow has a large added mass normal to its face and almost none
along it; a long part has more across than along. Replace the scalar with a per-link
**added inertia**, a 6×6 spatial inertia in the part's own frame: translational added mass
`ρ V (C_n n nᵀ + C_t (1 − n nᵀ))` with `C_n` and `C_t` from the part's aspect ratios (a thin
plate: about 1 normal and near 0 in plane; a sphere: 0.5 all round, which is what the
scalar is today), and a rotational added inertia about the in-plane axes (a plate turned
about an edge drags water round with it; about its normal, none). Add it to the link's
rigid-body inertia in the solver. Kirchhoff's equations for a body in an ideal fluid are
the rigid-body equations with the inertia replaced by the sum, so Featherstone's bias force
`v ×* (I v)`, which the solver already computes with the link's inertia, then carries the
reactive terms without a line of force code: the momentum the part gives the water as it
turns and accelerates comes back as force on the part, in the right direction, with the
right sign, within potential-flow theory. A paddle flapped symmetrically gains a net thrust
of the order of `½ m_a ⟨θ̇² r⟩ cos θ` per cycle (the water it throws back), an undulating
chain gains Lighthill's tail term as the sum over its links, and a rigid body translating
steadily gains nothing (d'Alembert), so there is no free lunch for a body that does not
move its joints. The drag stays as it is, the resistive term beside the reactive one.

Three things follow from doing it in the inertia and not as a force. It is implicit, so it
is stable at any step and the drag limiter never sees it. It conserves momentum and energy
exactly: the water's kinetic energy is part of the body's, no dissipation is added, and the
audit is untouched (the joint's work is still torque times rate). And it is one place in the
code: the link's spatial inertia at construction and at every resize (`Creature.Growth.cs`),
with the contact and the sensors reading the same body they read now.

**What it costs to build.** `Evosim.Dynamics` keeps a link's inertia in the standard form
(a scalar mass, a centre offset, a 3×3 rotational inertia). A general symmetric 6×6 per
link is what the articulated-body algorithm needs for this, and the composite and
articulated inertias are already 6×6 in the recursion, so the change is at the leaf: the
link's own `I` becomes a full matrix and the places that read `Mass[i]` as the translational
inertia (the drag limiter's `allowed`, the contact's push, the founder's mass floor, the
growth resize) are asked which mass they mean. Two or three days with the tests, and a
bench of the hand-built stroker (`logbook/specs/reactive-thrust-stroker/`) before and after. It is a
per-step change, a new realisation of every seed, and the parity with PhysX (forty bodies
40 of 40) no longer holds by construction, since PhysX has no added mass at all.

**The checks before it is trusted**, in order: a rigid sphere translating steadily gains no
force (d'Alembert); a flat plate flapped symmetrically about a hinge on a heavy body moves the
body in the direction its face throws water, at a speed that scales with the square of the
amplitude and the frequency; a two-link chain with a 90° lag moves faster than in phase and
in phase moves faster than one link alone; and every one of round 47's strokers, rerun alone,
prints its speed beside the probe's. The last is the exploit check DESIGN §5.3 asks for at
the champion level: if a body moves ten body lengths a second on a wag, the coefficients are
wrong before the world is.

## What it is not

Not lift on a foil (a circulation term with an angle of attack and a stall model), which is
the other half of fish swimming and the more exploitable one; a foil at the wrong angle in a
model with no stall is a free engine. Not vortex shedding or any wake. Not a change to the
drag. Not Lighthill's formula written as a tail force, which needs a chain axis a graph body
does not have. The tensor is the one term that comes from the geometry the body already
has, and it is the term C18 blames for the absence of jetting and fish-like forms in exactly
this model.

## Two other options, for the record

- **Per-panel lift.** Each panel's force gains a component in the plane from the angle
  between its velocity and its normal. Cheap, and it is what makes a fin a wing. Rejected
  for now for the reason above: without a stall model it is the exploit U07 found, and
  DESIGN §5.3's whole argument is against it.
- **A tail force by rule.** Lighthill's term applied at the free end of every chain from the
  root. Ad hoc for a body that is a tree, and it would reward one shape by construction.

## The decision, and what it implies

The world rule is the owner's: whether the water gives a stroke something back, and when.
My recommendation is the tensor, in a base-plus-one round after 48 (the base being 48's
world as read), with the joint work cost still at zero for that round so that the prize side
of movement can be read on its own before the cost side is turned back on. If a wag then
carries a body, the reading of 0118 that "wagging is free and goes nowhere" becomes a
reading of the old physics, and the eaters, who drift today, have a way to reach the snow
that the current does not carry them to. If nothing moves faster, the finding is that the
strokes evolved under the old water are not strokes at all, which the probe half says
already (most jointed bodies do not stroke alone), and selection has never seen a reason to
make one. Either answer is worth the round.

What the owner is asked: (1) the tensor, yes or no; (2) the round it lands in (my
recommendation: the one after 48); (3) whether the joint work cost stays at zero for that
round (my recommendation: yes, one change at a time).
