# Proposal: foraging, a stroke that pushes and a scent to steer by

*Opus, 2026-09-29, evening, for round 54. The owner approved its four rules in conversation the
same evening ("Approve all four") before going to sleep, and delegated the numbers and the build;
nothing here reaches a round until the owner has seen the bench. It narrows
`fable-propose-reactive-thrust.md` (approved by D127) to the owner's own simpler rule, and it is
absorbed into DECISIONS.md when the bench is read, then deleted.*

## Why

Round 52 ended with mouths eating corpses for the first time (170 and 114 in seeds 1 and 2,
against 1 in round 51) and with no mouth making a living, while jointed lines died out in every
seed. A still stomach empties its own cell of water and then eats only what the stirring brings
back, and it cannot reach a corpse the current does not bring it (`logbook/specs/r52-larder.txt`).
Movement is the wall, and in this water a wag cannot move a body: drag is the only force, and over
a symmetric stroke it cancels exactly (the swim probe, logbook/0118). Nothing a brain does changes
what a body earns, so no brain has ever been selected for anything (the last section but one has
the record).

## The four rules

**1. A swinging limb pushes the body away from itself.** Every link that swings about its parent
joint feels a force along its own axis, pointing from the link toward the joint:

    F = C_T · m_a · r · ω⊥²

`m_a` is the link's added mass (`C_a ρ V`, 0.5 × 1000 × its volume in the campaign), `r` the
distance from the joint to the link's centre, and `ω⊥` its angular speed relative to the parent,
perpendicular to its axis. The force is always positive and always along the limb, so over a
symmetric wag the sideways parts cancel and the part along the limb's mean direction adds up. A
tail wagged about the body's rear drives it forward. A single side fin swung fore and aft pushes
it sideways and so turns it, and a pair of mirrored fins drives it straight. A limb spun through
a full turn gains nothing, because its axis points every way in turn. Drag, unchanged, is the
decay: it takes the speed back once the stroke stops. The force acts on the link at the joint and
reaches the body through the joint like any other.

**2. Strokes cost energy.** `WorkCostMultiplier` exists and has been 0 in every launcher since
round 41. It goes above 0, charging every joule of joint work `∫|τ·ω| dt` to the reserve.

**3. A corpse has a scent.** A new sense channel, `Scent`, reads at each part the sum over corpses
of the corpse's remaining joules times a cone that falls from 1 at the corpse to 0 at 10 m. It is
read per part like the chemical channel, so two parts a body length apart read two values, and a
brain that compares them has a direction. It is built as a coarse field (2 m cells), filled once a
metabolic step from every corpse's cone and read by trilinear interpolation, so its cost is the
corpses times a thousand cells every half second.

**4. Rules 1 to 3 land together in round 54, as one change called foraging**, on round 53's world
as read. The owner ruled them as one, since a push with nothing to steer toward, or a scent with
no way to follow it, cannot be selected.
## The numbers, as estimates to be fixed on the bench

A reference swimmer: a trunk with a frontal area of 0.04 m² and a tail link 0.3 × 0.1 × 0.03 m
(volume 0.0009 m³, added mass 0.45 kg, centre 0.15 m from the joint), wagged ±0.5 rad at 1 Hz,
so that the mean square swing rate is 4.9 rad²/s². The push is then 0.33 · C_T N, and the drag at
speed U is ½ · 1000 · 1.5 · 0.04 · U² = 30 U² N, so U = 0.105 · √C_T m/s. The owner's target of 4
to 8 cm/s for such a tail-wagger puts **C_T between 0.15 and 0.6, about 0.3**. This is my
arithmetic on an idealised body; the bench measures it on the solver.

The cost is where this can fail. Wagging that tail against its own drag takes about 2 W of joint
work, and the push it buys is about 6 mW at 6 cm/s, so the stroke is roughly 0.3% efficient in
this model: the limb's drag is loss, and only the push rule moves the body. A still stomach's keep
is 0.11 to 0.44 W (round 52), so a work cost of 1 would make cruising cost five to twenty times
living, and no forage could pay for it: a corpse 10 m away, reached at 6 cm/s, would cost more than
its 26 J before it was reached. The multiplier is therefore set so that the reference swimmer's
stroke costs **one to two times its standing keep**, which puts it near **0.1 to 0.2**. Real fish
at a cruising pace spend a small multiple of their resting rate; that is my recollection and not a
cited figure, and the number here is a design choice that the bench and round 54 test. It is set
once, before the round, from the bench.

The scent's range is 10 m and its scale is a corpse's joules (round 52's corpses hold tens of
joules). Across a body 0.3 m long the cone changes by 3% of a corpse's signal, which a brain can
read.

## The bench, before anything reaches a round

On a branch in `scratch/wt-forage`, in `Evosim.Dynamics` and `Evosim.Core`, with tests:

1. A rigid body moving steadily gains no push, and a limb held still gains none.
2. A tail wagged symmetrically drives its body forward, and the sideways push averages to under 5%
   of the forward push over a cycle. A limb spun through full turns gains no net push.
3. Speed rises with the product of amplitude and frequency, and the reference swimmer's speed sets
   C_T inside the owner's 4 to 8 cm/s.
4. A single side fin turns its body, and a mirrored pair drives it straight.
5. Round 52's jointed bodies at 30,000 s, alone in still water under their own brains (the swim
   probe): the distribution of speeds, and no body faster than one body length a second. That is
   the exploit check DESIGN §5.3 asks for.
6. With the work cost set, the reference swimmer's stroke power over its standing keep is between
   1 and 2.
7. A hand-built forager, with its left scent wired to its right fin and its right scent to its
   left fin, reaches a corpse 10 m away. If a hand-built brain cannot close the loop, an evolved
   one cannot either, and the round is not run.
8. Identity: the digest at 1 and at N threads, and `--verify-checkpoint` on a world with corpses
   (the scent field is filled from the corpses each step and is not state, which the check will
   say).
## How round 54 will know whether a brain evolved

Three instruments, built with the rules. None of them has been built before, because nothing gave
a brain anything to do until now.

- **Steering toward the scent.** For each body at each sample, its own movement (its displacement
  less the water's) against the scent's gradient at its position: the mean cosine, against 0 for
  random movement. A rise in the mean over the run is selection for following the scent.
- **Brain swap.** The swim probe, extended: a body alone in still water with a corpse 5 m away in a
  random direction, run with its own brain, then with its scent inputs cut, then with its brain's
  weights shuffled. If its own brain reaches the corpse sooner than the other two, the brain is
  doing the work.
- **Inheritance.** Parent against child, for the probe's speed and for the steering cosine. A trait
  that passes from parent to child is one selection can act on.

The share of brains wired to the scent is read beside them. It is a weaker sign: a wire can drift
in without doing anything, as the contact channel's 2 to 5% did in round 49.

## What the record says about brains so far

Nothing shows a brain doing meaningful work. Most jointed bodies do not stroke alone in still
water (258 of 268 in round 47 seed 1, 1,148 of 1,422 in seed 3), and the strokers move 2 to 6 mm/s
against a current of 100 mm/s (logbook/0118). The share of bodies whose brain reads the contact or
the damage channel was 2 to 5% in round 49, and in seed 1 it fell from 19% to 2% (logbook/0122).
Jointed lines fall in every round from 48 to 52 (round 52 seed 1: 342 jointed with a jointed parent
at 5,000 s, 138 at 30,000 s), and at 30,000 s jointed and rigid bodies move at the same mean speed,
0.116 and 0.117 m/s, which is the water's (round 52 seed 1's `stats.jsonl`). The reason is the
precondition logbook/0072 named: a brain is selected only where control changes income or
survival, and here it changes neither. Joint work is free and neurons cost nothing, so brains
drift. Round 54 is the first round in which a better controller can mean more children.

## What it is not

Not lift, not a wake, and not the added-mass tensor of `fable-propose-reactive-thrust.md`, which
stays approved and unbuilt behind this simpler rule. Not a change to drag. Not a neuron cost:
neurons stay free in round 54, so that the prize of a brain is read before its price.
