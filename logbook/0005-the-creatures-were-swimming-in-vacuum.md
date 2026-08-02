# 0005 — The creatures were swimming in vacuum

**2026-08-02**  ·  Milestone 1

The sandbox scene finally got opened by a human, who reported: *"a bunch of boxes connected,
and rotating really fast, then speeding out of the viewport."*

Correct observation, and it took about four seconds of looking. Everything headless had said
PASS.

## The bug

A muscle pushes against something. Your bicep contracts and pulls the forearm up — and pulls
back on the upper arm, equally and oppositely. That reaction is not a detail; it is what makes
the force *internal*, and it is why you cannot lift yourself by your own belt.

[`EffectorDriver`](../unity/Assets/Evosim/Sim/EffectorDriver.cs) applied joint torque to the
child link and nothing at all to the parent:

```csharp
body.AddRelativeTorque(torque);   // and that was the whole of it
```

Every actuated joint was therefore an *external* torque on the creature. Angular momentum
accumulated with no source. Nothing bounded it, so it grew for as long as the drive ran, and
the creature span up until parts were whipping round at tens of metres per second.

The fix is three lines and no cleverness — equal and opposite, in world space so it cancels
exactly:

```csharp
Vector3 worldTorque = body.transform.TransformDirection(torque);
body.AddTorque(worldTorque);
_creature.Bodies[part.ParentIndex].AddTorque(-worldTorque);
```

Mean part speed across twelve creatures went from **14–81 m/s** to **0.21–1.02 m/s**.

## The second bug, hiding behind the first

`TorqueScale` was 300. That number came from Spike 01, where it meant a **force limit** under
position control — a ceiling, rarely reached. Reused here as directly applied torque it means
something else entirely, and for a 100 kg part it is 30,000 N·m.

Reasoning from inertia instead of copying: a cube of mass *m* and half-extent *h* has moment
of inertia (2/3)mh² about its centre. A 100 kg part with h = 0.25 m gives about 4 kg·m², and
moving a joint through a radian in a quarter second wants roughly 30 rad/s², so on the order
of 130 N·m — a little over **1** N·m per kilogram. It is now 2.

Two orders of magnitude, and it survived because it was labelled "uncalibrated" in a comment.
That label was too generous. Uncalibrated means the right value is unknown; this was a
different quantity wearing the same number.

## Why nothing caught it

The smoke test asserted two things: no NaN, and the bodies are moving.

A creature spinning at 60 rad/s is extremely finite and extremely moving.

That is now the third time in one day — after
[0002](0002-the-spike-that-was-too-fast.md) and
[0004](0004-two-ways-to-report-a-success-you-dont-have.md) — that a check phrased as *"did
something happen"* has passed while the thing that happened was wrong. There appears to be no
amount of learning this lesson that prevents the next instance; only assertions prevent it.

## The assertion that does catch it

With no gravity, no drag and no contact, **nothing external acts on a creature, so its total
momentum cannot change no matter what its joints do.** That is not a heuristic or a
tolerance-tuned guess — it is conservation of momentum, and it holds for every creature, every
genome, every controller, forever.

The check drives all joints with a constant one-sided signal — the worst case, because an
oscillating signal can hide a leak by averaging it over a cycle — and measures linear and
angular momentum about the creature's own centre of mass.

| | specific angular momentum, m²/s |
|---|---|
| with the reaction torque | 0.0004 – 0.0098 |
| tolerance | 0.05 |
| with the bug reintroduced | 0.85 – 2.41 |

Two orders of magnitude of daylight on either side of the threshold, so the check is not
doing delicate work. Verified by deliberately reintroducing the bug and watching all six seeds
fail, then reverting.

## Why this one mattered more than a wrong shape

[`DESIGN.md`](../DESIGN.md) §11.2 lists the physics exploits an evolutionary search discovers
instead of locomotion. Free momentum is the canonical member of that family.

Nothing is being selected yet, so the bug cost nothing but a confusing screen. Had it survived
to Milestone 3, every creature in the archive would have been built on it — spinning for free
beats swimming, so selection would have found it immediately and never looked at anything
else. The symptom would have been "the swimmers don't swim", weeks after the cause, with a
fluid model and a search algorithm and a fitness function all available to blame first.

The anti-exploit checklist was scheduled for Milestone 2. On the evidence, the first item of
it should exist as soon as anything is actuated at all — which is to say, before there is any
selection pressure to hide behind.

## What it took to find

Someone looked at it.

Four days of headless verification, unit tests, geometry assertions and mutation testing did
not surface this. A person pressing Play surfaced it immediately, because *"rotating really
fast and flying away"* is not a subtle failure to a human eye and is nearly invisible to a
log file.

The instruments were all measuring real things. None of them was measuring the right thing,
and there was no way to know that from inside the instrument set. Worth remembering the next
time a milestone's visual payoff looks like the optional part.
