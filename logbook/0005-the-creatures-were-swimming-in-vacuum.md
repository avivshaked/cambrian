# 0005 — The creatures were swimming in vacuum

**2026-08-02**  ·  Milestone 1

A human opened the sandbox scene for the first time and reported *"a bunch of boxes
connected, and rotating really fast, then speeding out of the viewport"*. That took about
four seconds of looking. Four days of headless checks had all said PASS.

Two bugs were behind it. The joint drive pushed on one side of a joint and on nothing at all
on the other. Every muscle was therefore pushing against the outside world instead of
against the creature's own body. And the drive strength had been copied from the spike,
where the same number meant a different quantity, which made it two orders of magnitude too
large.

## A muscle with nothing to push against

A muscle pushes against something. Your bicep contracts and pulls the forearm up, and it
pulls back on the upper arm just as hard, in the opposite direction. That reaction is what
makes the force internal, and it is why you cannot lift yourself by your own belt.

[`EffectorDriver`](../unity/Assets/Evosim/Sim/EffectorDriver.cs) applied joint torque to the
child link and nothing at all to the parent:

```csharp
body.AddRelativeTorque(torque);   // and that was the whole of it
```

So every actuated joint was an external torque on the creature. Angular momentum
accumulated with no source. Nothing bounded it, so it grew for as long as the drive ran, and
the creature span up until parts were whipping round at tens of metres per second.

The fix is three lines and no cleverness. Equal and opposite, computed in world space so
that the two cancel:

```csharp
Vector3 worldTorque = body.transform.TransformDirection(torque);
body.AddTorque(worldTorque);
_creature.Bodies[part.ParentIndex].AddTorque(-worldTorque);
```

Mean part speed across twelve creatures went from 14–81 m/s to 0.21–1.02 m/s.

## The second bug, hiding behind the first

`TorqueScale` was 300. That number came from Spike 01, where it meant a force limit under
position control, a ceiling the drive rarely reached. Reused here as a torque applied
directly, it means something else entirely, and for a 100 kg part it is 30,000 N·m.

The right way to get the number is to reason from inertia rather than to copy it. A cube of
mass *m* and half-extent *h* has moment of inertia (2/3)mh² about its centre. A 100 kg part
with h = 0.25 m gives about 4 kg·m². Moving a joint through a radian in a quarter of a
second wants roughly 30 rad/s², so on the order of 130 N·m. That is a little over 1 N·m per
kilogram. It is now 2.

Two orders of magnitude out, and it survived because a comment labelled it "uncalibrated".
That label was too generous. Uncalibrated means the right value is unknown. This was a
different quantity wearing the same number.

## Why nothing caught it

The smoke test asserted two things: that no number had gone to NaN, and that the bodies were
moving. A creature spinning at 60 rad/s is extremely finite and extremely moving.

That is the third time in one day, after [0002](0002-the-spike-that-was-too-fast.md) and
[0004](0004-two-ways-to-report-a-success-you-dont-have.md), that a check phrased as *"did
something happen"* has passed while the thing that happened was wrong. There seems to be no
amount of learning this lesson that prevents the next instance. Only an assertion prevents
it.

## The assertion that does catch it

With no gravity, no drag and no contact, nothing external acts on a creature. So its total
momentum cannot change, whatever its joints do. That is no heuristic and no tolerance-tuned
guess. It is conservation of momentum, and it holds for every creature, every genome, every
controller, for ever.

The check drives all joints with a constant one-sided signal, which is the worst case: an
oscillating signal can hide a leak by averaging it away over a cycle. It then measures
linear and angular momentum about the creature's own centre of mass.

Compare the first row with the last, and both with the tolerance between them.

| | specific angular momentum, m²/s |
|---|---|
| with the reaction torque | 0.0004 – 0.0098 |
| tolerance | 0.05 |
| with the bug reintroduced | 0.85 – 2.41 |

There are two orders of magnitude of daylight on either side of the threshold, so the check
is not doing delicate work. I verified it by deliberately reintroducing the bug, watching
all six seeds fail, and then reverting.

## Why this one mattered more than a wrong shape

[`DESIGN.md`](../DESIGN.md) §11.2 lists the physics exploits an evolutionary search
discovers instead of locomotion. Free momentum is the canonical member of that family.

Nothing is being selected yet, so the bug cost nothing but a confusing screen. Had it
survived to Milestone 3, every creature in the archive would have been built on it. Spinning
for free beats swimming, so selection would have found it at once and never looked at
anything else. The symptom would have been "the swimmers don't swim", weeks after the cause,
with a fluid model and a search algorithm and a fitness function all available to blame
first.

The anti-exploit checklist was scheduled for Milestone 2. On this evidence its first item
should exist as soon as anything is actuated at all. That is to say, before there is any
selection pressure for it to hide behind.

## What it took to find

A person looked at it.

Four days of headless verification, unit tests, geometry assertions and mutation testing did
not surface this. A person pressing Play surfaced it immediately. To a human eye,
*"rotating really fast and flying away"* is not a subtle failure, and in a log file it is
nearly invisible.

The instruments were all measuring real things. None of them was measuring the right thing,
and there was no way to know that from inside the instrument set. Worth remembering the next
time a milestone's visual payoff looks like the optional part.
