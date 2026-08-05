# 0007 — The creature that was paid to jam

**2026-08-02**  ·  Milestone 2

A question, not a bug report:

> i think that if you have two 3d rectangles connected to each other directly, and self collision
> is not allowed, unless you're adding some kind of connector, it's not likely to allow a lot of
> actuation. no?

The geometry is right. Two boxes joined face-to-face, hinged at the shared face: a corner of the
child sits at `h√2` from the joint centre while the shared plane is at `h`, so the corner crosses
into the parent at essentially zero degrees of rotation. Enforce collision between them and the
joint is welded.

That specific case turns out to be safe — PhysX articulations never collide directly-jointed
links, so a part and its own parent are exempt regardless of layer settings, which is also why
[`DESIGN.md`](../DESIGN.md) §4.2's permitted joint overlap works at all. The easy answer would
have been to say so and move on.

## The answer was right and the reasoning was untested

Nothing exempts *siblings*, or a grandchild folding back into its grandparent. Those had just
been switched on ([logbook 0006](0006-boxes-inside-boxes.md)) and the cost had never been
measured.

So: run each creature twice, identical seed and drive, and total the angle swept by every degree
of freedom over eight seconds. Joint travel is the quantity the question is actually about,
because it is what a creature needs in order to swim.

| seed | travel, collision off | travel, collision on | kept |
|---|---|---|---|
| 1 | 88.58 | 89.83 | 101 % |
| 2 | 80.97 | 2.19 | **3 %** |
| 3 | 77.46 | 77.44 | 100 % |
| 4 | 54.73 | 46.65 | 85 % |
| 5 | 39.28 | 13.13 | **33 %** |
| 7 | 188.56 | 83.17 | **44 %** |

Three creatures in eight lost most of their range of motion, and these are three-part
creatures — the smallest the developer produces. Seed 2 swept 2.19 radians in eight seconds,
which is a creature welded shut.

## The finding underneath the finding

Seed 2 also travelled **further**: 0.338 m with collision off, 1.426 m with it on. Seed 7 did the
same, 0.610 → 2.099.

A creature whose joints have stopped moving has nothing to swim with. Drag is the only force on
it and drag removes energy. It should drift *less*.

Fitness is displacement of the centre of mass (§5.5). The two creatures that jammed were the two
best swimmers in the population. Search would have found that within a few generations and never
looked at swimming again — §11.2's physics exploitation, in our own simulator rather than in a
cited one.

## A check that was excused from the case it needed to cover

The momentum conservation check ran with self-collision **off**, and the comment explained why:
contact is an external force, so leaving it on would test two things at once.

That reasoning is wrong. Two parts of the *same* creature pushing each other is internal — A
pushes B, B pushes A, the total is unchanged. It belongs inside the conservation law. Excluding
it is precisely what stopped the check from seeing this.

Run with collision on, the law breaks:

| seed 2 | collision off | collision on |
|---|---|---|
| speed of centre of mass | 0.00214 m/s | **0.25388 m/s** |
| specific angular momentum | 0.00417 m²/s | **0.04839 m²/s** |

No gravity, no drag, no ground. 0.254 m/s of centre-of-mass velocity out of purely internal
forces, 119× the same creature with collision off.

It **passed**. `ComSpeedTolerance` was 0.5 and `AngularTolerance` 0.1, both derived from a single
fault — removing the joint reaction torque ([logbook 0005](0005-the-creatures-were-swimming-in-vacuum.md)) — which is
enormous, 3.4–16.9 m²/s. A bar sized against a huge fault says nothing about a small one.

## The source, and the cap

`Physics.defaultMaxDepenetrationVelocity` is 10 m/s by default. Depenetration is a correction,
not a force: the solver assigns separating velocity to resolve an overlap and is under no
obligation to conserve momentum doing it. Fold a limb into your own body and the solver pays you
to unfold it.

Capped at 0.5 m/s:

| seed 2, collision on | uncapped | capped | reference, collision off |
|---|---|---|---|
| speed of centre of mass | 0.25388 | 0.00410 | 0.00214 |
| specific angular momentum | 0.04839 | 0.00498 | 0.00417 |
| displacement in water | 1.253 m | 0.037 m | — |

Back to the order of the honest baseline, and the worst swimmer in the population rather than the
best — which is what 2 % of a range of motion should buy.

Tolerances re-derived against both known faults rather than one: 0.03 m²/s angular and 0.15 m/s
linear, roughly 1.7× above the worst honest run in either collision regime.

## And one that only showed up because of the fix

`CheckSwimming` restored self-collision at the **end** of its loop rather than the start, so it
inherited whatever the momentum check left behind, which was *off*. Every displacement figure the
water table had ever produced was measured with self-collision disabled — including the figures
used, in this session, to argue that enabling self-collision had changed nothing. Identical
numbers across a configuration change were reported as evidence the configuration did not matter.
They were evidence it had never been applied.

Scene state in Unity is global. A check that depends on it has to set it, not assume it.

## What is not fixed

The exploit is closed; the mobility cost is not. Seeds 2, 5 and 7 still keep 2 %, 33 % and 40 % of
their range of motion. The question that opened this entry has a real answer outstanding —
creatures probably do need gaps or connectors between parts — and §4.2 and §11.2 still disagree
about whether overlap is a feature or the exploit.

## The pattern, fifth instance

[0002](0002-the-spike-that-was-too-fast.md), [0004](0004-two-ways-to-report-a-success-you-dont-have.md),
[0005](0005-the-creatures-were-swimming-in-vacuum.md), [0006](0006-boxes-inside-boxes.md), and now
this. Every one is a check that answered *did something happen* while the thing that happened was
wrong. This time there were two at once: a tolerance calibrated in a regime it was no longer being
applied to, and a test that had silently never run in the configuration it claimed to test.

The countermeasure that keeps working is the conservation law. It needed no tuning, it could not be
satisfied by an impressive-looking failure, and the only reason it did not catch this immediately is
that it had been *excused* from the case — by a comment, confidently argued, that was wrong.
