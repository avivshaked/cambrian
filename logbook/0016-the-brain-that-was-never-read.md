# 0016 — The brain that was never read

**2026-08-07**  ·  Milestone 4 → 6

Entry 0015 ended with a diagnosis: the world deleted every joint it had because mechanical work
was billed while every creature ran the same drive signal. `DriveTestSine` applied one shared sine
to every degree of freedom of every creature, so the controller was a *constant across the
population*, and a uniform flap produces no net thrust. A real cost against an unobtainable
benefit.

Meanwhile the genome had carried `NeuronDef`, `NeuronInput`, oscillator frequencies and phases
since draft 1. Development copied them onto every part. Mutation perturbed them. **Nothing read
them.** One line in `Developer` — `Neurons = node.Neurons` — and then the trail ended.

## What was already built

Less was missing than it looked.

The **effector** side was complete: `EffectorDriver.Drive(float[] raw)` takes one value per DOF,
clamps, averages over ten steps, scales by the mass of the smaller connected part and applies
torque with the reaction on the parent — [DESIGN §4.4](../DESIGN.md#44-sensors-and-effectors)'s
recommended scheme, validated by Spike 01's M4.

The **genome** side was complete: ops drawn from `NeuronOps.MvpSet`, per-neuron frequency, phase,
amplitude and bias, input references restricted to things that survive recursion.

So the whole gap was one function: `Phenotype + time → float[TotalDof]`.

## Three decisions the genome did not make for me

**Synchronous update.** Every neuron reads the previous step's outputs and writes to a separate
buffer, swapped at the end. Updating in place would make a neuron's value depend on the order
parts happen to be walked in — the fault that produces a plausible number rather than an error,
which this project has paid for twice (entries 0007, 0008). It is also what makes §4.4's claim
literally true rather than aspirational: a signal crosses exactly one node per step. The test
measures it arriving at parts 0, 1, 2 on steps 1, 2, 3.

**Neuron *d* of a part drives DOF *d*.** The genome has no effector-mapping field, so this had to
be invented rather than read. It is the only mapping that survives recursion — neurons are copied
with the morph node, so the mapping is copied with them, which is what makes a duplicated segment
a duplicated controller. Summing all neurons instead would make gain depend on neuron count, so
adding a neuron for an unrelated purpose would silently change how hard the creature swims.

**`Sigmoid` is `tanh`, not the logistic curve.** Both are sigmoids; only one is centred. A
logistic sigmoid is strictly positive, so a joint driven through one could only ever push one
way and could not oscillate — every creature whose gait ran through a `Sigmoid` would be
paralysed in a direction, and nothing would report it as anything but a bad swimmer.

## The bugs were in the tests, and they were the good kind

Every fixture I wrote was an invalid genome, and `Genome.Validate` said exactly why:

> Node 0: cell type 'structural' has a Hinge joint, but only 'link' may move. Two parts cannot
> actuate against each other without a link between them (§5A.1).

I had hand-built chains of structural boxes with hinges between them, which the design forbids on
purpose. The validator also rejected `SameNode input 6 has no such neuron` — so out-of-range
neuron references cannot reach the evaluator through any normal path, and a comment I had written
claiming the index-wrapping was load-bearing was wrong. It is a guard, not a mechanism, and now
says so.

Worth noting what happened here: **the error messages contained the section numbers and the
reasoning.** Fixing seven failing tests took one edit because the code being tested explained
itself.

## Does it work?

The direct test first: ten random genomes, ten distinct drive signals. Under the old code they
would have been ten identical ones.

Then the embodied run, against entry 0015's:

| t (s) | with joints — shared sine | **with own brain** | deaths — sine | **own brain** |
|---|---|---|---|---|
| 20 | 10% | **30%** | 29 | **8** |
| 60 | **0%** | **23.3%** | 35 | **10** |
| 200 | **0%** | **6.4%** | 38 | **20** |

Joints survive the run instead of being gone in sixty seconds, and deaths halve. The audit still
closes at 0.0000%.

But mean speed is still about 0.0002 m/s, and the jointed share is still falling. So the fix moved
the timescale and it is not yet clear it changed the destination.

## Two problems that need opposite responses

Either random central pattern generators are bad swimmers — expected, and precisely what selection
is for, so the answer is a longer run — or something structurally prevents thrust, in which case
no amount of evolution helps because the trait being selected for is unreachable.

The mean cannot tell these apart. The **distribution** can. Two hundred random genomes, twenty
seconds each, driven by their own brains:

| statistic | m/s |
|---|---|
| median | 0.00619 |
| 90th percentile | 0.02759 |
| **best** | **0.48465** |

**35% exceed 1 cm/s, and the best swims at 78× the median** — nearly half a metre per second, from
a random genome, with no selection whatsoever. In twenty seconds it crosses ten metres in a world
sixty metres deep.

The mechanism works. What is missing is search.

That also explains the embodied run's mean without needing anything else: at 6.4% jointed, a
population average is dominated by plants sitting at exactly zero. 6.4% × 0.006 m/s ≈ 0.0004,
which is what the table says.

## What is still true from 0015

Billing mechanical work is still premature, but for a different and much narrower reason than
before. It is no longer that swimming is *impossible* — it is that a good swimmer is about one
genome in two hundred, and two hundred simulated seconds with 114 births is nowhere near enough
search to find one, let alone to keep it. The question that follows is a longer run, not another
mechanism.

## The pattern

0013: a guard shaped like the bug it guarded. 0014: an estimate wrong enough to change the plan.
0015: the codebase had already written down the answer.

This one is the cheapest of the four and I nearly skipped it. **The mean said one thing and the
distribution said the opposite.** Mean speed of 0.0002 m/s reads as "nothing swims"; the same
population contains a creature doing 0.48 m/s. One number would have sent me looking for a bug in
the drag model. The other says go and run it for longer.
