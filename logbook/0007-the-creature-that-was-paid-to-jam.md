# 0007 — The creature that was paid to jam

**2026-08-02**  ·  Milestone 2

This one starts with a question rather than a bug report:

> i think that if you have two 3d rectangles connected to each other directly, and self collision
> is not allowed, unless you're adding some kind of connector, it's not likely to allow a lot of
> actuation. no?

The geometry in the question is right, and the particular case it worries about turns out to
be safe. But checking why it was safe uncovered two faults. Creatures were being paid to jam
their own joints shut, and the check that should have caught that had been excused from the
one configuration where it mattered.

## The answer was right and the reasoning was untested

Take two boxes joined face to face and hinged at the shared face. A corner of the child sits
at `h√2` from the joint centre while the shared plane is at `h`. So the corner crosses into
the parent at essentially zero degrees of rotation. Enforce collision between them and the
joint is welded.

That case is safe. PhysX articulations never collide directly-jointed links, so a part and
its own parent are exempt whatever the layer settings say. It is also why
[`DESIGN.md`](../DESIGN.md) §4.2's permitted joint overlap works at all. The easy answer
would have been to say so and move on.

Nothing exempts siblings, though, or a grandchild folding back into its grandparent. Those
had just been switched on ([logbook 0006](0006-boxes-inside-boxes.md)) and the cost had
never been measured.

## Measuring the thing the question was about

I ran each creature twice, with identical seed and drive, and totalled the angle swept by
every degree of freedom over eight seconds. Joint travel is what the question is really
about, because it is what a creature needs in order to swim. Watch the last column: the
fraction of its range of motion each creature kept once collision was enforced.

| seed | travel, collision off | travel, collision on | kept |
|---|---|---|---|
| 1 | 88.58 | 89.83 | 101 % |
| 2 | 80.97 | 2.19 | **3 %** |
| 3 | 77.46 | 77.44 | 100 % |
| 4 | 54.73 | 46.65 | 85 % |
| 5 | 39.28 | 13.13 | **33 %** |
| 7 | 188.56 | 83.17 | **44 %** |

Three creatures in eight lost most of their range of motion, and these are three-part
creatures, the smallest the developer produces. Seed 2 swept 2.19 radians in eight seconds,
which is a creature welded shut.

## The finding underneath the finding

Seed 2 also travelled further: 0.338 m with collision off, and 1.426 m with it on. Seed 7
did the same, going from 0.610 to 2.099.

A creature whose joints have stopped moving has nothing to swim with. Drag is the only force
acting on it, and drag removes energy. It ought to drift less.

Fitness is displacement of the centre of mass (§5.5). The two creatures that jammed were the
two best swimmers in the population. Search would have found that within a few generations
and never looked at swimming again. That is §11.2's physics exploitation, in our own
simulator rather than in a cited one.

## A check that had been excused from the case it needed to cover

The momentum conservation check ran with self-collision off, and a comment explained why:
contact is an external force, so leaving it on would test two things at once.

That reasoning is wrong. Two parts of the same creature pushing on each other is internal. A
pushes B, B pushes A, and the total is unchanged. It belongs inside the conservation law,
and excluding it is what stopped the check from seeing this.

Run with collision on, the law breaks. Both columns are the same creature under the same
drive.

| seed 2 | collision off | collision on |
|---|---|---|
| speed of centre of mass | 0.00214 m/s | **0.25388 m/s** |
| specific angular momentum | 0.00417 m²/s | **0.04839 m²/s** |

No gravity, no drag, no ground. That is 0.254 m/s of centre-of-mass velocity out of purely
internal forces, 119× the same creature with collision off.

And the check passed it. `ComSpeedTolerance` was 0.5 and `AngularTolerance` 0.1. Both were
derived from a single fault, the missing joint reaction torque of
[logbook 0005](0005-the-creatures-were-swimming-in-vacuum.md), which is enormous at
3.4–16.9 m²/s. A bar sized against a huge fault says nothing about a small one.

## The source of the free momentum, and the cap on it

`Physics.defaultMaxDepenetrationVelocity` is 10 m/s by default. Depenetration is a
correction rather than a force. The solver assigns separating velocity to resolve an
overlap, and it is under no obligation to conserve momentum while doing so. Fold a limb into
your own body and the solver pays you to unfold it.

Capped at 0.5 m/s, with the clean baseline in the last column:

| seed 2, collision on | uncapped | capped | reference, collision off |
|---|---|---|---|
| speed of centre of mass | 0.25388 | 0.00410 | 0.00214 |
| specific angular momentum | 0.04839 | 0.00498 | 0.00417 |
| displacement in water | 1.253 m | 0.037 m | — |

That is back to the order of the clean baseline, and the creature is now the worst swimmer
in the population instead of the best. It is what 2 % of a range of motion should buy.

Tolerances were re-derived against both known faults rather than one. They are now
0.03 m²/s angular and 0.15 m/s linear, roughly 1.7× above the worst clean run in either
collision regime.

## And one fault that only showed up because of the fix

`CheckSwimming` restored self-collision at the end of its loop rather than at the start, so
it inherited whatever the momentum check had left behind, which was off. Every displacement
figure the water table had ever produced was measured with self-collision disabled. That
includes the figures used, in this session, to argue that enabling self-collision had
changed nothing. Identical numbers across a configuration change were reported as evidence
that the configuration did not matter. They were evidence that it had never been applied.

Scene state in Unity is global. A check that depends on it has to set it, and may not assume
it.

## What is not fixed

The exploit is closed and the mobility cost is not. Seeds 2, 5 and 7 still keep 2 %, 33 %
and 44 % of their range of motion. The question that opened this entry still has a real
answer outstanding, because creatures probably do need gaps or connectors between their
parts. And §4.2 and §11.2 still disagree about whether overlap is a feature or the exploit.

## The pattern, fifth instance

[0002](0002-the-spike-that-was-too-fast.md),
[0004](0004-two-ways-to-report-a-success-you-dont-have.md),
[0005](0005-the-creatures-were-swimming-in-vacuum.md), [0006](0006-boxes-inside-boxes.md),
and now this one. Every one of them is a check that answered *did something happen* while
the thing that happened was wrong. This time there were two at once. One was a tolerance
calibrated in a regime it was no longer being applied to. The other was a test that had
silently never run in the configuration it claimed to test.

The countermeasure that keeps working is the conservation law. It needed no tuning, and it
could not be satisfied by an impressive-looking failure. The only reason it did not catch
this immediately is that it had been excused from the case, by a comment that was
confidently argued and wrong.
