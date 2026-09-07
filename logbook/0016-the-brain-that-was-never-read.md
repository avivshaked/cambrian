# 0016 — The brain that was never read

**2026-08-07**  ·  Milestone 4 → 6

Entry 0015 ended with a diagnosis. The world deleted every joint it had because mechanical
work was billed while every creature ran the same drive signal. `DriveTestSine` applied one
shared sine to every degree of freedom of every creature, so the controller was a constant
across the population, and a uniform flap produces no net thrust. A real cost against an
unobtainable benefit.

Meanwhile the genome had carried `NeuronDef`, `NeuronInput`, oscillator frequencies and
phases since draft 1. Development copied them onto every part, mutation perturbed them, and
nothing read them.

There is one line in `Developer` that says `Neurons = node.Neurons`, and then the trail
ends.

## What was already built

Less was missing than it looked.

The effector side was complete. `EffectorDriver.Drive(float[] raw)` takes one value per
degree of freedom, clamps it, and averages over ten steps. It then scales by the mass of the
smaller connected part and applies torque with the reaction on the parent. That is
[DESIGN §4.4](../DESIGN.md#44-sensors-and-effectors)'s recommended scheme, validated by
Spike 01's M4.

The genome side was complete too: ops drawn from `NeuronOps.MvpSet`, per-neuron frequency,
phase, amplitude and bias, and input references restricted to things that survive recursion.

So the whole gap was one function: `Phenotype + time → float[TotalDof]`.

## Three decisions the genome did not make for me

The first is synchronous update. Every neuron reads the previous step's outputs and writes
to a separate buffer, swapped at the end. Updating in place would make a neuron's value
depend on the order in which parts happen to be walked. That is the fault which produces a
plausible number rather than an error, and this project has paid for it twice (entries 0007,
0008). It is also what makes §4.4's claim literally true rather than aspirational: a signal
crosses one node per step and no more. The test measures it arriving at parts 0, 1, 2 on
steps 1, 2,
3.

Neuron *d* of a part drives DOF *d*. The genome has no effector-mapping field, so this had
to be invented rather than read. It is the only mapping that survives recursion. Neurons are
copied with the morph node, so the mapping is copied with them, and that is what makes a
duplicated segment a duplicated controller. Summing all neurons instead would make gain
depend on neuron count, so adding a neuron for an unrelated purpose would silently change
how hard the creature swims.

`Sigmoid` is `tanh` rather than the logistic curve. Both are sigmoids, and only one of them
is centred.

A logistic sigmoid is strictly positive, so a joint driven through one could only ever push
one way and could never oscillate. Every creature whose gait ran through such a neuron would
be paralysed in a direction, and nothing would report it as anything worse than a bad
swimmer.

## The bugs were in the tests, and they were the good kind

Every fixture I wrote was an invalid genome, and `Genome.Validate` said why, in detail:

> Node 0: cell type 'structural' has a Hinge joint, but only 'link' may move. Two parts cannot
> actuate against each other without a link between them (§5A.1).

I had hand-built chains of structural boxes with hinges between them, which the design
forbids on purpose. The validator also rejected `SameNode input 6 has no such neuron`. So
out-of-range neuron references cannot reach the evaluator through any normal path, and a
comment I had written claiming the index-wrapping was load-bearing was wrong. It is a guard
rather than a mechanism, and it now says so.

Notice what happened in that exchange. The error messages carried the section numbers and
the reasoning, so fixing seven failing tests took one edit, because the code being tested
explained itself.

## Whether it works

The direct test first: ten random genomes produced ten distinct drive signals. Under the old
code they would have been ten identical ones.

Then the embodied run, set against entry 0015's. Compare the columns in pairs.

| t (s) | with joints — shared sine | **with own brain** | deaths — sine | **own brain** |
|---|---|---|---|---|
| 20 | 10% | **30%** | 29 | **8** |
| 60 | **0%** | **23.3%** | 35 | **10** |
| 200 | **0%** | **6.4%** | 38 | **20** |

Joints survive the run instead of being gone in sixty seconds, and deaths halve. The audit
still closes at 0.0000%.

But mean speed is still about 0.0002 m/s, and the jointed share is still falling. So the fix
moved the timescale, and it is not yet clear that it changed the destination.

## Two problems that need opposite responses

Either random central pattern generators are bad swimmers, which is expected and is what
selection is for, so the answer is a longer run. Or something structurally prevents thrust,
in which case no amount of evolution helps, because the trait being selected for is
unreachable.

The mean cannot tell those apart, and the distribution can. Two hundred random genomes,
twenty seconds each, driven by their own brains:

| statistic | m/s |
|---|---|
| median | 0.00619 |
| 90th percentile | 0.02759 |
| **best** | **0.48465** |

35% exceed 1 cm/s, and the best swims at 78× the median. That is nearly half a metre per
second, from a random genome, with no selection whatsoever. In twenty seconds it crosses ten
metres in a world sixty metres deep.

The mechanism works, and what is missing is search.

That also explains the embodied run's mean without needing anything else. At 6.4% jointed a
population average is dominated by plants sitting at zero, and 6.4% × 0.006 m/s ≈ 0.0004.
That is what the table says.

## What is still true from 0015

Billing mechanical work is still premature, but for a different and much narrower reason
than before. It is no longer that swimming is impossible. It is that a good swimmer is about
one genome in two hundred. Two hundred simulated seconds with 114 births is nowhere near
enough search to find one, let alone to keep it. What follows is a longer run rather than
another mechanism.

## The pattern

Entry 0013 was a guard shaped like the bug it guarded. Entry 0014 was an estimate wrong
enough to change the plan. Entry 0015 was the codebase having already written down the
answer.

This one is the cheapest of the four and I nearly skipped it. The mean said one thing and
the distribution said the opposite. A mean speed of 0.0002 m/s reads as "nothing swims", and
the same population contains a creature doing 0.48 m/s. One number would have sent me
looking for a bug in the drag model. The other says go and run it for longer.
