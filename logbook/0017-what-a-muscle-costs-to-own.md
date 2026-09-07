# 0017 — What a muscle costs to own

**2026-08-07**  ·  Milestone 4

[Entry 0016](0016-the-brain-that-was-never-read.md) ended by saying the next step was a
longer run rather than another mechanism. A random genome swims at 0.485 m/s about one time
in two hundred, so the capability exists. And 200 simulated seconds with 114 births was too
little search to find it. This entry is that longer run. The premise was wrong, and the run
said so within ten minutes.

## Two things the calibration sweep found before the long run started

The default configuration is 400 W/m², which §5A.2b measured as far into runaway. So the
first job was to find an irradiance that sustains a bounded population. That sweep answered
a different question first.

§5A.2b's transition has moved, and a long way. That section located the self-sustaining
transition at 32 W/m². It was measured before bodies cost tissue
([D026](../DECISIONS.md#d026)) and before swimming cost anything
([D029](../DECISIONS.md#d029)). Embodied, it looks like this.

| W/m² | alive at t=2000 | births | reading |
|---|---|---|---|
| 48 | 40 | 6 | pinned at the population floor; 99 deaths; nothing reproduces |
| 64 | 61 | 42 | barely above replacement |
| 100 | 1500 | 1492 | growing hard |

At 48 W/m² the floor is doing all the work and `gen min` never leaves zero, which is the
definition of a world that is not running itself (§5A.6b). Every number in §5A.2b describes
a world that no longer exists. That is not a fault. It is what happens when two knobs the
section did not have get added. But it needs saying out loud, because those numbers have
been accumulating authority for two weeks.

And the jointed share went to zero at every single irradiance, at 64, 100, 150, 200 and 400,
with up to 4,046 births and lineages sixteen generations deep. That is not a search-time
problem. Something was structurally killing joints.

## The arithmetic, and then the measurement

`LinkCell.IdleWattsPerNewtonMetre` is 0.02 and `RandomGenomeOptions.MaxLinkPower` is 120, so
a median link bills 1.24 W continuously, before it has moved once. A photosynthetic part at
100 W/m² earns about 2.3 W in total. One actuator's standing cost is over half a leaf's
entire income.

`LinkCell`'s own documentation names both failure modes:

> It trades directly against the work coefficient: too low and capacity is effectively free again,
> too high and nothing can afford to move.

So the question is only which end we are on, and it is answerable by moving the knob. Both
knobs, in fact, because they enter the ledger as a product, so cutting either should do.
Nine runs at 100 W/m² and 1500 simulated seconds, sorted by that product. The last column is
the count that matters.

| idle W/N·m | maxPower N·m | **standing cost W** | jointed creatures alive at t=1500 |
|---|---|---|---|
| 0.02 | 120 | **2.4** | 0, 1, 1 *(seeds 1–3 — the shipped default)* |
| 0.02 | 60 | 1.2 | 1 |
| 0.005 | 120 | 0.6 | 4, 11, 4 *(seeds 1–3)* |
| 0.02 | 20 | 0.4 | 4 |
| 0.02 | 8 | 0.16 | 8 |

Monotonic in the product, reached independently down two different knobs, and consistent in
direction across three seeds. A creature can afford a joint when owning it costs well under
a fifth of what its body earns, and cannot when it costs all of it. The shipped default is
on the wrong side by roughly a factor of five.

## The mistake I made reading it, twice

The harness reported jointed creatures as a percentage, and I read "0%" as extinction across
the whole sweep. It was not. Population was growing four- and five-fold inside the
measurement window, so the denominator was exploding. At one setting the jointed count went
11 → 14 → 16 → 19 while the share fell from 17.5% to 4.5%. The joints were being out-bred
rather than dying out.

Only the shipped default is anywhere near true extinction, and even that is 0–1 rather than
0.

This is the second time in two entries that a ratio said the opposite of the underlying
counts. Entry 0016 was a mean hiding a 78× tail. Both times the aggregate was the
honest-looking number, and both times it was wrong. The harness now prints the count.

I also called the first four-point sweep "a textbook calibration curve", one message before
noticing that its cheapest setting performed worse than the setting above it. With counts
between 4 and 19, and one run per point, that is noise. The nine-run version with three
seeds is what the table above rests on.

## An unplanned check that passed

Two runs with the same config and seed, in separate Unity processes, produced byte-identical
trajectories. That is 508 alive, 491 births, 57 deaths, and every row the same.

§7 only ever promised same-machine, same-version reproducibility, and it explicitly declined
to promise more. That is the promise which holds.

## What is still not known

Nothing in the world swims yet. Even where joints persist, the fastest creature in the world
is doing 0.0075 m/s. Persistence is a precondition for a swimmer evolving rather than
evidence that one will. The question 0016 asked is still open. It just could not be asked at
all until joints had stopped being unaffordable.

The default is left where it is. A knob moved to make an uncomfortable result go away is how
a finding gets buried. And which of the two to move, the cost coefficient or the power range
it multiplies, is a design decision rather than a measurement. The measurement is above.

## The pattern

Entry 0013 was a guard shaped like the bug it guarded. Entry 0014 was an estimate wrong
enough to change the plan. Entry 0015 was the codebase having already written the answer
down. Entry 0016 was a mean that said the opposite of its own distribution.

This one is the previous entry's closing recommendation turning out to be wrong, and running
it was the cheapest possible way to find that out. "Run it for longer" cost ten minutes and
returned "you are not looking at a search problem". The recommendation was reasonable on the
evidence available. It was still wrong. And the correction was cheap because it had been
stated sharply enough to be tested.
