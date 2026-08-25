# 0019 — Three knobs that reached nothing, and a seed that was not a seed

**2026-08-24**  ·  Milestone 4

[Entry 0018](0018-nothing-to-swim-towards.md) concluded that an open-loop swimmer has negative
expected value, and that §4.4's sensor channels — filed under this milestone and never built — were
the precondition for locomotion being worth anything. This entry is building them. **The sensors
were the smallest part of the day.**

## What was actually missing

`ISensorField` existed. `Brain` read it, resolved every input kind through it, and defaulted it to
`null`. `SensorChannel` declared ten channels with a paragraph of reasoning each. `NeuronInput`
could name any of them, `GenomeJson` round-tripped them, `Genome.Validate` accepted them.

Nothing implemented the interface. Every call site passed `null`.

And `GenomeFactory` has always drawn `SensorChannel.JointAngle` for **half of every neuron's
inputs**. So roughly half the wiring in every creature that has ever run in this project evaluated
to a constant zero. Not a missing feature — a brain smaller than its own genome said it was.

Four channels now read: `JointAngle` and `JointAngularVelocity` against the joint's own limits,
`OrientationUp`, and `Depth`. The Milestone 1 smoke test drives a creature and asserts each one
varies over the run or across the body, because a channel that reads a flat zero is
indistinguishable from a dead input from inside the brain — which is the whole lesson of
[0016](0016-the-brain-that-was-never-read.md).

## Then two more knobs turned out to be decorative

**`MutationRates.RewireInputChance` was declared, reached the config hash, survived the JSON round
trip, and was set to 0.9 by the heavy-mutation test. Nothing read it.**

Weights mutated. Constants mutated. Operators mutated. **The topology inside a node did not.** Every
neuron in every lineage read exactly what its founder happened to draw, for every generation ever
run here, and the only way a lineage could acquire a new kind of connection was to duplicate a whole
node. Wiring a depth sensor into the world would have helped only those creatures whose founder
already happened to reference it.

The two reflection tests could not catch this. They prove a tunable reaches the hash and the file —
statements about serialization, not about whether any code consults the value. There is now a test
that turns the knob off and on and asserts the wiring differs.

Implementing it exposed a second fault immediately, which is the part worth keeping:

> **Nodes were repaired against the old global brain, and then the global brain was replaced.** A
> `GlobalBrain` reference validated against a list that no longer existed, and if the new list was
> shorter the genome was invalid.

That had been sitting there the whole time, unreachable, because nothing in the codebase could
*produce* a `GlobalBrain` reference — founders draw sensors and same-node links only, and mutation
could not change an input's kind. A third one behind it: when the global brain repaired *itself*,
it clamped against `g.GlobalBrain`, which the caller had not assigned yet, so it read the
pre-mutation list.

One dead knob was hiding two live bugs. That is the argument against leaving a knob unimplemented
rather than deleting it: the code behind it never runs, so it is never wrong, so it never gets
fixed.

## And then the seeds

The embodied A/B reported **the same fastest-ever speed to four significant figures for two
different seeds.** 0.0747 and 0.0747; 0.0926 and 0.0926. This project has twice agreed that
identical numbers across a configuration change mean the change did not reach the thing it
configures ([0007](0007-the-creature-that-was-paid-to-jam.md),
[0008](0008-the-energy-audit.md)), so it got looked at rather than reported.

The first guess was a birth transient — `PreviousCentre` is taken the instant a body is built,
before the solver runs, so a newborn's first speed sample contains whatever the spawn pose does
while depenetrating. That is real and is now excluded. It was not the answer: with newborns
excluded the peaks were still identical, just at different times.

The answer was in `World`:

```csharp
_nextSeed = seed;            // constructor
ulong seed = _nextSeed++;    // every founder, every birth
```

A run seeded 1 drew its founders from seeds 1…40. A run seeded 2 drew from 2…41. **Thirty-nine of
the same forty genomes.** Two "independent" runs were one experiment offset by a single creature,
which is exactly why the fastest founder in each was the same animal producing the same number.

Per-creature seeds are now `SplitMix64(worldSeed, index)` — a bijection with avalanche, so adjacent
inputs give unrelated outputs and no two pairs collide. A wider stride was considered and rejected:
it makes overlap unlikely rather than impossible and leaves the streams correlated in a way nobody
would think to check, and the fault it replaces was already the plausible-looking kind. Two tests
now hold it, one on `SeedFor` and one on the founder populations themselves, and the second was
checked against the old code to confirm it fails there rather than being trusted on its intentions.

**Every claim in this logbook of the form "consistent across three seeds" is weaker than it
reads.** [Entry 0017](0017-what-a-muscle-costs-to-own.md)'s actuator-cost calibration is the one
that matters, and its three-seed rows are three runs of nearly one experiment.

## What the corrected runs say

Six runs at 100 W/m², 1500 simulated seconds, genuinely independent seeds, comparing the two ends of
the cost knob that 0017 measured:

| idle · maxPower | seed | alive | **jointed** | best m/s |
|---|---|---|---|---|
| 0.02 · 20 | 1 | 889 | 3 | 0.0731 |
| 0.02 · 20 | 2 | 631 | 0 | 0.0126 |
| 0.02 · 20 | 3 | 523 | 36 | 0.0314 |
| 0.005 · 120 | 1 | 1127 | 3 | 0.0464 |
| 0.005 · 120 | 2 | 687 | 0 | 0.0333 |
| 0.005 · 120 | 3 | 692 | 18 | 0.0632 |

**Seed variance dwarfs the knob.** Seed 2 gives zero jointed creatures under both settings; seed 3
gives the most under both. Three runs cannot separate these, and before today three runs looked like
enough because three runs were nearly one run.

The audit closes at 0.0000% in all six.

## The half of the trade 0017 did not measure

0017 argued that the idle coefficient and the power ceiling "enter the ledger as a product, so
cutting either should do", and on that basis the ceiling was cut from 120 N·m to 20. **The
symmetry is false.** Both knobs enter the *cost* as a product; only `MaxLinkPower` enters the
*benefit*, because it multiplies straight into joint torque and torque is what makes thrust.

Measured on identical genomes — the setting consumes the same single RNG draw either way, so the
arms really are the same animals with different muscles:

| | power 20 | power 120 |
|---|---|---|
| founders over 1 cm/s | 4% | 14.5% |
| randomViable median | 0.0047 | 0.0079 |
| randomViable over 1 cm/s | 22.5% | 38.5% |
| randomViable best | 0.072 | 0.261 |

Between 1.7× and 3.6× of swimming ability, across the distribution. In isolation. The embodied runs
above cannot see that difference at all, because at n=3 the seed swamps it.

## What is still not known, and it is the same thing

**Nothing swims.** Sustained speed at the last sample is 0.001–0.004 m/s in every run. Joints now
persist — 0 to 36 of them, where the shipped default gave 0 to 1 — which is progress against
[0017](0017-what-a-muscle-costs-to-own.md) and is not the thing that was wanted.

Mean depth ends between −1.5 m and −4.1 m, from founders scattered over the top twenty. The
population *did* climb, by differential survival rather than by swimming. And that may be the
answer to why swimming still does not pay: with light attenuating over 12 m, a lineage that is
already at −1.5 m has perhaps 13% of income left to gain from ever moving again. **The gradient has
been solved by sitting in the right place, and a solved gradient is not a reason to travel.**

That is a hypothesis, not a finding, and it is the next thing to take a measurement of.

## The pattern

0013: a guard shaped like the bug it guarded. 0014: an estimate wrong enough to change the plan.
0015: the codebase had already written down the answer. 0016: the mean said the opposite of the
distribution. 0017: the previous entry's recommendation was wrong and running it was the cheapest
way to find out. 0018: a hypothesis confirmed decisively and still not the answer.

This one: **three separate knobs that were declared, stored, hashed, tested and never read.** The
tests that exist to prevent exactly this checked that a value reaches the config file, which is not
the same as checking that anything consults it — and a value nothing consults is invisible in
precisely the way this project keeps discovering things are invisible. The recurring shape is not
carelessness. It is that a correct-looking number is the hardest kind of wrong to see, and every
instance here has been one.
