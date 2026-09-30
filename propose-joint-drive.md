# Proposal: the joint as a bend and a power

*Opus, 2026-09-30, from the owner's design in conversation that day, for the owner's ruling. It
would replace the mechanism of rule 1 in `propose-foraging.md` (the push from a limb's real swing)
and the effector in DESIGN.md §4.4, and it keeps that proposal's other rules. It is absorbed into
DECISIONS.md on ruling, then deleted.*

## Why

The owner asked whether creatures swim. Two readings answer it, and the second corrects how the
first was read.

**In the rounds' own water, nothing could swim.** Rounds 52 and 53 ran without the limb push
(neither header prints `limbPush`; the rule is on the `forage` and `nursery` branches only, commit
`e591dc3`). Their water has drag and nothing else, and over a symmetric stroke drag cancels
exactly (logbook/0118). So round 53's jointed bodies moved no faster than rigid ones drifting in
the current (`scripts/reads/swim-speed.py`: 0.7 to 6.8 cm/s against 2.8 to 9.0), and a stroke
bought a body nothing. That is also why their brains do not stroke. At the end of round 53 seed 1,
1,701 of the 3,977 joints a genome could move have no neuron driving them at all, and 290 are
driven by any kind of oscillator (a count over the snapshot at 30,000 s). Nothing selected for
moving, because moving paid nothing.

**With the push, the bodies can move and their brains still do not.** The swim test
(`logbook/specs/swim-test-2026-09-30.txt`) put 30 jointed bodies from each of rounds 52 and 53
alone in still water, with the push at 0.3, for 300 s. Frozen, they travel 0. Under their own
brains the median is 0.1 to 0.7 cm/s. Under a plain sine on every joint it is 4 to 8 cm/s, the
range the owner set for rule 1, and every body moves. How a joint is driven is the gap.

**Driving a joint by force makes the stroke a tuning problem.** Today a neuron's output is a
torque: clamped to 1, times the part's Power, averaged over ten steps (§4.4). Read on every
physics step, a sine's swing falls from 110° at 0.5 Hz to 88°, 36° and 16° at 1, 2 and 4 Hz,
because the push reverses before the limb has moved. Position control, imitated through today's
neurons, does no better (72°, 77°, 27° and 8°): the limb's strength caps it either way, and a stiff
controller fed through the brain's one-step delay shakes in place. (A first reading taken only when
the economy ran, every half second, caught 2 and 4 Hz strokes at the same point each time and read
them as still. That reading is struck in the spec.) A brain has to find a frequency, a strength and
a phase that suit its limb before a stroke does anything. Random brains almost never do.

## The design, in the owner's words and then in the model's

The owner, 2026-09-30: the brain's inputs are the senses and internal state; its outputs are what
it can affect, like a joint. For every degree of freedom, one output from 0 to 1 sets how hard the
joint moves, off below a threshold that is in the genome; a second output from 0 to 1 bends the
joint to one side, across the full range of movement for that creature. A random brain moves its
organs at random, so bodies "swim whether they want to or not". Each joint carries three times in
its genome, mutating in children: reaction (how long from rest to moving), hold (how long a
command stands before the joint takes another) and release (how long it takes to move to the next
command). And the wiggle is for show: what matters is the joint's angle and its power at every
moment.

In the model:

1. **Two outputs per degree of freedom.** Neuron *d* of a part is the power of degree of freedom
   *d*, and neuron *n + d* its bend, *n* the joint's number of degrees of freedom. The mapping
   survives recursion as today's does: neurons copy with the node. A part with too few neurons
   leaves the rest at 0, a joint at rest and centred.
2. **The bend is a real pose.** The bend output maps 0 to 1 onto the joint's limits, and the joint
   is driven to that angle by a spring and damper whose force is capped by the part's Power. The
   gains are derived from the link's inertia, not free, so a heavy limb bends slowly and a light one
   quickly. The pose changes on the scale of the envelope, not of a stroke, so it asks the solver
   for nothing fast.
3. **The power is a stroke that is not simulated.** Above the joint's threshold, the power *s*
   (rescaled from the threshold to 1) gives the link the push of rule 1 as if it were wagging:
   `F = C_T · m_a · r · (s · Ω)²`, along the limb toward its joint. `m_a` and `r` are the link's
   added mass and reach as in rule 1, so a big fin pushes harder than a stub and body shape still
   matters, and `Ω` is one world constant, set on the bench so that the reference swimmer of
   `propose-foraging.md` at full power swims 4 to 8 cm/s. The push is applied at the limb, so a
   limb bent to one side turns the body. Steering comes out of the bend. A twist degree of freedom,
   which spins a limb about its own length, gives no push.
4. **The envelope shapes commands, not strokes.** A command is a change of power or bend. Reaction
   is the ramp from rest to the first command, hold is the time a command stands before the joint
   accepts the next, and release is the ramp to the next. Three numbers per joint, in the genome,
   mutated like its limits. They replace the ten-step average, and they are what stops a jittery
   brain from making a joint shake.
5. **The stroke is drawn.** The record logs, per degree of freedom, the joint's angle and its power.
   The theatre draws a wag about the bend angle, its size and rate following the power. Its curve is
   the owner's: a quick stroke with a slight ease-in, carried almost to the end, then a long ease-out
   at the edge. How the joints of a chain wag against each other is the next section's question.
   The films say once, in words, that the stroke is drawn from the power.
   The identity check is untouched: every position and angle it compares is simulated.

## A chain of joints: a fish or a snake

The owner, 2026-09-30: with two joints in a chain (limb, joint, limb, joint, limb) the push is easy
to get from the powers, but the look is not. A fish's two joints almost always swing the same way;
a snake's might not. Since the stroke is drawn, the drawing has to choose, and a wrong choice makes a
fish that swims like a snake or a snake that swims like a board.

The phase between neighbouring joints does not change the push in this model: each link is pushed
along its own length whatever the others do. So it is a question for the drawing, and there are
two honest ways to answer it.

- **From the body's shape.** The drawn wave's lag from joint to joint is set by the chain's length
  against its thickness. A short, deep chain draws its joints nearly in step, bending as one, like a
  fish whose wave sits mostly in the tail. A long, thin chain draws about one full wave along its
  length, like an eel or a snake. I believe fish are classed by swimming mode in roughly this way,
  from how much of the body carries the wave; that is my recollection, not a checked source, and it
  would need a JART before the drawing cites it.
- **From a gene.** Each joint carries a phase to its parent. Since the phase changes nothing the
  world scores, the gene would drift at random, and whether a line looks like a fish or a snake would
  be chance, not its body.

*Recommended: from the body's shape*, with no gene. The look then follows the body, which is what a
viewer reads as right, and the choice can be revisited if a later rule makes the phase matter.
Where the brain commands the joints of a chain differently (one pushing, one resting, or bent
opposite ways), the drawing follows the commands, so a body the brain drives as a snake is drawn as
one.

## The rulings this needs

Each is the owner's, because each is a world rule or the record's.

1. **Adopt the model**, replacing rule 1's mechanism and §4.4's effector. *Recommended.* It turns
   "does this brain stroke well" into "does this brain push when and where it pays", which is the
   question the ecology asks. What is given up: evolution no longer invents stroke patterns, only
   when, where and how hard to push, and the bodies that make that pay.
2. **The push from the power**, as in point 3, with `Ω` set on the bench. *Recommended*, because it
   keeps rule 1's dependence on the limb's size.
3. **Whether Power also scales the push**, or only caps the bend's force. *Recommend only the bend.*
   Power scaling the push too would make Power the one gene that buys speed, and a genome would
   inflate it for nothing when moving is free.
4. **The price of pushing.** Rule 2 of `propose-foraging.md` (a stroke costs one to two times the
   body's standing keep) carries over, with the stroke's work taken as the push times the drawn
   limb's tip speed, `F · r · s · Ω`. *Recommended.* Without a price, every body pushes flat out
   all the time.
5. **The envelope's ranges.** Reaction, hold and release each between 0.02 and 1 s, founders drawn
   across the range, mutated at the joint-limit rates. *Recommended as a start*; the bench reads
   whether hold time caps turning as the owner expects.
6. **The threshold per joint**, in the genome, between 0 and 0.5. *Recommended.*
7. **The drawn stroke's curve in the theatre only**, with no gene. *Recommended*: a gene that
   changes nothing but the look would drift at random and cost a genome field.
8. **A new genome format.** Every earlier genome and checkpoint is refused by the new build, as with
   every format bump. Rounds start from founders, so nothing needs translating; the nursery would
   draw its bodies from the first round run under the model.
9. **A chain's look from its shape** (the section above), with no gene. *Recommended.*

## The bench, before anything reaches a round

The eight checks of `propose-foraging.md` carry over, with the push now from the power:

- a still body and a held limb gain nothing;
- a limb bent to one side turns its body, and a mirrored pair runs straight;
- the reference swimmer sets `Ω`, and no body goes faster than one body length a second;
- the stroke costs one to two times the body's keep;
- a hand-wired forager (left scent to right fin, right scent to left fin) reaches a corpse 10 m away;
- the digest agrees at 1 and at N threads.

Two more for this model. A random-brained founder moves in still water at a speed the swim test's
sine reached. And a joint whose brain flips its command every step holds its pose for the hold time
and does not shake.
The swim test is the instrument for the first; it runs in about a minute.

## What building it takes

Core: the two outputs and the four joint genes, their mutation and JSON, and the tests that guard
the config and the format. Dynamics: the pose drive, the envelope, the push from the power in
`Fluid.Apply` beside the present one, and its tests. The farm's pose stream gains the power per
degree of freedom. The graphics card's kernels either carry it or refuse the rule, as they refused
rule 1. The theatre draws the stroke. This is my estimate of the pieces, not of the time.
