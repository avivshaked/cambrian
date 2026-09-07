# 0019 — Three knobs that reached nothing, and a seed that was not a seed

**2026-08-24**  ·  Milestone 4

[Entry 0018](0018-nothing-to-swim-towards.md) concluded that an open-loop swimmer has
negative expected value. The sensor channels of §4.4, filed under this milestone and never
built, were the precondition for locomotion being worth anything. This entry is building
them. The sensors were the smallest part of the day.

## What was actually missing

Everything except the implementation was already there.

| piece | state |
|---|---|
| `ISensorField` | existed; every call site passed `null` |
| `Brain` | read it, resolved every input kind through it, defaulted it to `null` |
| `SensorChannel` | declared ten channels, with a paragraph of reasoning each |
| `NeuronInput` | could name any of them |
| `GenomeJson` | round-tripped them |
| `Genome.Validate` | accepted them |

Nothing anywhere implemented the interface.

And `GenomeFactory` has always drawn `SensorChannel.JointAngle` for half of every neuron's
inputs. So roughly half the wiring in every creature that has ever run in this project
evaluated to a constant zero. That is not a missing feature. It is a brain smaller than its
own genome said it was.

Four of the channels now read. `JointAngle` and `JointAngularVelocity` report against the
joint's own limits.

The other two are `OrientationUp` and `Depth`. The Milestone 1 smoke test drives a creature
and asserts that each channel varies over the run or across the body. A channel reading a
flat zero is indistinguishable from a dead input when seen from inside the brain, which is
the whole lesson of [0016](0016-the-brain-that-was-never-read.md).

## Then two more knobs turned out to be decorative

`MutationRates.RewireInputChance` was declared, reached the config hash, survived the JSON
round trip, and was set to 0.9 by the heavy-mutation test, and nothing read it.

Weights, constants and operators all mutated. The topology inside a node did not. Every
neuron in every lineage read what its founder happened to draw, for every generation ever
run here. The only way a lineage could acquire a new kind of connection was to duplicate a
whole node. Wiring a depth sensor into the world would have helped only those creatures
whose founder already happened to reference it.

The two reflection tests could not catch this. They prove a tunable reaches the hash and the
file, which are statements about serialization rather than about whether any code consults
the value. There is now a test that turns the knob off and on and asserts that the wiring
differs.

Implementing it exposed a second fault immediately, and that is the part worth keeping.
Nodes were repaired against the old global brain, and then the global brain was replaced. A
`GlobalBrain` reference validated against a list that no longer existed, and if the new list
was shorter the genome was invalid.

That had been sitting there the whole time, unreachable, because nothing in the codebase
could produce a `GlobalBrain` reference. Founders draw sensors and same-node links only, and
mutation could not change an input's kind.

There was a third fault behind it. When the global brain repaired itself, it clamped against
`g.GlobalBrain`, which the caller had not yet assigned, so it read the pre-mutation list.

One dead knob was hiding two live bugs. That is the argument against leaving a knob
unimplemented rather than deleting it: the code behind it never runs, so it is never wrong,
so it never gets fixed.

## And then the seeds

The embodied A/B reported the same fastest-ever speed to four significant figures for two
different seeds. It gave 0.0747 and 0.0747, then 0.0926 and 0.0926. This project has twice
agreed that identical numbers across a configuration change mean the change did not reach
the thing it configures ([0007](0007-the-creature-that-was-paid-to-jam.md),
[0008](0008-the-energy-audit.md)). So it got looked at rather than reported.

The first guess was a birth transient. `PreviousCentre` is taken the instant a body is
built, before the solver runs, so a newborn's first speed sample contains whatever the spawn
pose does while depenetrating. That is real, and it is now excluded. It was not the answer:
with newborns excluded the peaks were still identical, at different times.

The answer was in `World`:

```csharp
_nextSeed = seed;            // constructor
ulong seed = _nextSeed++;    // every founder, every birth
```

A run seeded 1 drew its founders from seeds 1…40. A run seeded 2 drew from 2…41. That is
thirty-nine of the same forty genomes. Two "independent" runs were one experiment offset by
a single creature. That is how the fastest founder in each came to be the same animal
producing the same number.

Per-creature seeds are now `SplitMix64(worldSeed, index)`, a bijection with avalanche, so
adjacent inputs give unrelated outputs and no two pairs collide. A wider stride was
considered and rejected. It makes overlap unlikely rather than impossible, and it leaves the
streams correlated in a way nobody would think to check. The fault it replaces was already
the plausible-looking kind.

Two tests now hold it, one on `SeedFor` and one on the founder populations themselves. The
second was checked against the old code to confirm that it fails there, rather than being
trusted on its intentions.

Every claim in this logbook of the form "consistent across three seeds" is weaker than it
reads. [Entry 0017](0017-what-a-muscle-costs-to-own.md)'s actuator-cost calibration is the
one that matters, and its three-seed rows are three runs of nearly one experiment.

## What the corrected runs say

Six runs at 100 W/m² and 1500 simulated seconds, with truly independent seeds, comparing the
two ends of the cost knob that 0017 measured. Read the jointed column down by seed
rather than by setting.

| idle · maxPower | seed | alive | **jointed** | best m/s |
|---|---|---|---|---|
| 0.02 · 20 | 1 | 889 | 3 | 0.0731 |
| 0.02 · 20 | 2 | 631 | 0 | 0.0126 |
| 0.02 · 20 | 3 | 523 | 36 | 0.0314 |
| 0.005 · 120 | 1 | 1127 | 3 | 0.0464 |
| 0.005 · 120 | 2 | 687 | 0 | 0.0333 |
| 0.005 · 120 | 3 | 692 | 18 | 0.0632 |

Seed variance dwarfs the knob. Seed 2 gives zero jointed creatures under both settings, and
seed 3 gives the most under both. Three runs cannot separate these, and before today three
runs looked like enough, because three runs were nearly one run.

The audit closes at 0.0000% in all six.

## The half of the trade 0017 did not measure

Entry 0017 argued that the idle coefficient and the power ceiling "enter the ledger as a
product, so cutting either should do". On that basis the ceiling was cut from 120 N·m to 20.
The symmetry is false. Both knobs enter the cost as a product, and only `MaxLinkPower`
enters the benefit, because it multiplies straight into joint torque, and torque is what
makes thrust.

Measured on identical genomes, since the setting consumes the same single RNG draw either
way, so the two arms really are the same animals with different muscles:

| | power 20 | power 120 |
|---|---|---|
| founders over 1 cm/s | 4% | 14.5% |
| randomViable median | 0.0047 | 0.0079 |
| randomViable over 1 cm/s | 22.5% | 38.5% |
| randomViable best | 0.072 | 0.261 |

Between 1.7× and 3.6× of swimming ability, across the distribution, in isolation. The
embodied runs above cannot see that difference at all, because at n=3 the seed swamps it.

## What is still not known, and it is the same thing

Nothing in these runs swims. Sustained speed at the last sample is 0.001–0.004 m/s
everywhere. Joints now persist, from 0 to 36 of them where the shipped default gave 0 to 1,
which is progress against [0017](0017-what-a-muscle-costs-to-own.md) and is not the thing
that was wanted.

Mean depth ends between −1.5 m and −4.1 m, from founders scattered over the top twenty. The
population did climb, by differential survival rather than by swimming. And that may be the
answer to why swimming still does not pay. With light attenuating over 12 m, a lineage
already at −1.5 m has perhaps 13% of income left to gain from ever moving again. The
gradient has been solved by sitting in the right place, and a solved gradient is not a
reason to travel.

That is a hypothesis rather than a finding, and it is the next thing to take a measurement
of.

## The pattern

Entry 0013 was a guard shaped like the bug it guarded. Entry 0014 was an estimate wrong
enough to change the plan. Entry 0015 was the codebase having already written down the
answer. Entry 0016 was a mean that said the opposite of its distribution. Entry 0017 was a
recommendation that was wrong, where running it was the cheapest way to find out. Entry 0018
was a hypothesis confirmed decisively and still not the answer.

This one is three separate knobs that were declared, stored, hashed, tested and never read.
The tests that exist to prevent this checked that a value reaches the config file, which is
not the same as checking that anything consults it. A value nothing consults is invisible in
just the way this project keeps discovering things are invisible. The recurring shape is not
carelessness. It is that a correct-looking number is the hardest kind of wrong to see, and
every instance here has been one.
