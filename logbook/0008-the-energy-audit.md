# 0008 — What an energy audit found

**2026-08-04**  ·  Milestone 2

[`DESIGN.md`](../DESIGN.md) §5A makes energy a conserved budget, so the first thing Milestone 2
needed was to measure what a creature actually spends. Joint power is `τ·ω`, both quantities
already in hand: the driver knows the torque it applied and the bodies know their angular
velocities.

The first run reported creatures dissipating **kilowatts**. Seed 3: 46,393 J over 8 seconds,
5,799 W, 170,537 joules per metre travelled.

Water is dense. A 300 kg limb moving at a few metres per second really does cost that much.
The number survived a back-of-envelope check and was about to become the calibration basis for
the entire energy economy.

## The check that stopped it

`signed joint work = ΔKE + energy dissipated by drag`

Nothing else acts — no gravity, no contact, no ground. If that does not close, the number is
arithmetic on the wrong quantity.

It did not close. **85% of the claimed work was unaccounted for.**

What follows is four wrong explanations in a row, which is the point of the entry.

## Wrong explanation 1: joint limits

A joint limit is a hard constraint. A limb arriving at one is stopped, and its kinetic energy
is destroyed there — a sink in neither term. Estimated at ~470 J per impact and ~13 impacts per
joint over 8 s, which lands squarely in the 13,700–63,000 J residual range.

Landing in the right range is how wrong explanations survive here, so: sweep the drive strength.
If limit-slamming is the sink, a weaker drive should collapse the residual.

| torque N·m/kg | joint work J | unaccounted | at limit |
|---|---|---|---|
| 0.05 | 54526.6 | 90.4 % | 44.2 % |
| 0.2 | 54526.6 | 90.4 % | 44.2 % |
| 0.5 | 54526.6 | 90.4 % | 44.2 % |
| 1 | 54526.6 | 90.4 % | 44.2 % |
| 2 | 54526.6 | 90.4 % | 44.2 % |

Identical across a 40× sweep. Not a physics result — the sweep never happened.

## The bug the sweep found instead

`TorqueScale` was an auto-property read **once, in the constructor**, while every call site set
it by object initializer — which runs after construction. Every
`new EffectorDriver(c) { TorqueScale = x }` silently kept the default.

Including [`CreatureSpawner`](../unity/Assets/Evosim/Sim/CreatureSpawner.cs). **The sandbox's
inspector field had never done anything.** Every creature watched on screen ran at 2 N·m/kg
regardless of what the Editor said.

Byte-identical output across a configuration change means the change was not applied. That tell
had already appeared once this week, when self-collision "made no difference"
([logbook 0007](0007-the-creature-that-was-paid-to-jam.md)) — and it was the same tell for the
same reason.

## Wrong explanation 2: still joint limits

With the sweep working, the residual stayed at ~85% at *every* drive strength, including
0.05 N·m/kg where **no joint touched a limit at all**. Refuted.

## Wrong explanation 3: drive damping

`ArticulationDrive.damping = 1` on every joint, deliberately, so undriven joints settle instead
of ringing. Measured: 0.5–1% of the work. Not it.

## What it actually was, part one

The residual was ~85% *regardless of drive strength across a 40× range*. A scale-invariant loss
is the signature of a linear damping term.

`ArticulationBody.angularDamping` defaults to **0.05**. `jointFriction` defaults to **0.05**.
[`PhenotypeBuilder`](../unity/Assets/Evosim/Sim/PhenotypeBuilder.cs) never set either.

**Creatures had been swimming in two fluids.** Ours — specified in §5.2, unit-tested to prove it
can never add energy, motivated by [U07]'s account of a published search exploiting exactly that
flaw — and PhysX's, which nobody chose, which no figure accounted for, and which removed roughly
ten times more.

Every displacement number this project had produced was measured against the wrong resistance.

Zeroing it raised drag dissipation from 4,672 to 5,705 J on the same creature: energy that had
been vanishing now reached the water.

## What it actually was, part two

With that removed, the residual finally tracked limit contact — 39% at 0.05 N·m/kg, 88% at 2.
But drive strength moves both variables, so the correlation is not causal. Holding drive fixed
and varying only the ranges:

| limits | joint work J | drag out J | unaccounted | at limit |
|---|---|---|---|---|
| 1× | 56511.8 | 5705.5 | 88.0 % | 47.6 % |
| 2× | 58683.9 | 13053 | 75.5 % | 24.3 % |
| 4× | 59385.2 | 33602.6 | 34.1 % | 13.3 % |
| 20× | 57298.4 | 66196.6 | −21.9 % | 6.8 % |

Work barely changes; drag dissipation rises **12×**. The energy was always there. The limit
constraint was destroying it before it could reach the water.

Two sinks had been superposed, which is why the first sweep looked flat: PhysX damping dominated
at low drive, joint limits at high drive. Removing the first exposed the second.

## Wrong explanation 4: a convergence study on a chaotic system

The −21.9% at 20× said the accounting now erred the other way, so both integrals were changed
from left-rectangle to midpoint. Residuals fell from ~85% to 0.5–19%, mixed sign — which looks
like discretisation.

Testing that properly means halving dt and watching the error fall. It did not fall:

| seed | dt=0.01 | dt=0.005 | dt=0.0025 |
|---|---|---|---|
| 1 | −13.3 % | −27.8 % | −159.6 % |
| 3 | −17.1 % | −32.8 % | −30.9 % |
| 4 | −19.2 % | 6.3 % | 6.5 % |
| 6 | 18.5 % | 12.1 % | 7.1 % |

Two converge, two diverge, one catastrophically. But the test was invalid: with limits widened
and full drive these creatures are **chaotic**, so halving dt does not refine one trajectory, it
produces a different one. Those four columns are not four accuracies of one run. They are four
unrelated runs, and reading convergence off them means nothing.

## The instrument that settled it

Two 0.5 m cubes, one hinge, constant torque. No chaos, so dt refinement means what it should.

First attempt reported exactly **1500 J at every timestep**. Each cube is 125 kg plus 125 kg
added mass; at 2 N·m/kg that is 500 N·m, and the limit sat at 3 rad. 500 × 3 = 1500.

Constant torque drives a hinge to its stop and holds it. But the number is `τ·Δθ` to four
significant figures, independent of dt — which is a **textbook verification that the joint-power
measurement is exactly right**, arrived at by accident while trying to test something else.

Widening the limit to ±100 rad gave exactly 3141.6 J = 500 × 2π: PhysX does not honour a
revolute limit past one revolution. A limit cannot be moved out of the way, only removed. With
`ArticulationDofLock.FreeMotion`:

| dt | joint work J | drag out J | residual |
|---|---|---|---|
| 0.01 | 46542.6 | 38589.1 | 6.60 % |
| 0.005 | 47958.6 | 40575.2 | 3.55 % |
| 0.0025 | 48476.4 | 41899.8 | 1.99 % |
| 0.00125 | 48707.2 | 42608.5 | 1.04 % |

Halving dt halves the error. First-order convergence. **The measurement was never wrong.**

## What the residual was all along

Real energy, destroyed by joint limits. At the default ranges that is ~85% of everything a
creature spends.

Which is a finding about calibration, not a defect. Under §5A it is charged as metabolic cost,
and that is defensible — a muscle slamming a joint does spend the energy. But it means the
metabolic cost is currently dominated by bang-bang actuation rather than by swimming, and an
open-loop sine has no way to decelerate before a stop. Judging `TorqueScale` fairly needs the
brain graph (Milestone 6). The check was accordingly changed from an assertion into a
**named report**: *energy into joint limits*.

## The one that was hiding behind the fix

Zeroing PhysX's damping was correct and exposed a second thing it had been suppressing: with
self-collision on, seed 2 went from 0.006 to 0.045 m²/s of specific angular momentum, growing
1.7× over 2× the time — injection accumulating, not error random-walking.

Contact was manufacturing momentum, and the damping had been quietly bleeding it off. The
depenetration cap from [logbook 0007](0007-the-creature-that-was-paid-to-jam.md) went from
0.5 m/s to 0.02:

| cap m/s | seed 2, specific angular momentum |
|---|---|
| 0.5 | 0.0447 |
| 0.1 | 0.0318 |
| 0.02 | 0.0191 |

Monotone in the cap. Bounded, not eliminated — seed 2 still grows at 1.6×, and 0.019 sits
uncomfortably close to the 0.023 honest floor. Recorded as an open weakness rather than closed.

## What this cost, and what it bought

One measurement, four wrong explanations, three real defects:

1. A configuration knob that had never worked, including one exposed in the Editor UI.
2. A second, unchosen drag model acting on every creature since Milestone 1.
3. Momentum injection through contact, previously masked by (2).

And one non-defect that matters more than any of them: **the drive is far stronger than the
joint ranges can absorb**, so most of what a creature spends is destroyed against its own stops.

The kilowatt figure was defensible, survived a sanity estimate, and was one commit from becoming
the calibration basis for §5A's entire economy. Six-sevenths of it was not what the column said.

The pattern from [0002](0002-the-spike-that-was-too-fast.md),
[0004](0004-two-ways-to-report-a-success-you-dont-have.md),
[0005](0005-the-creatures-were-swimming-in-vacuum.md),
[0006](0006-boxes-inside-boxes.md) and [0007](0007-the-creature-that-was-paid-to-jam.md) holds,
with one addition. A conservation law does catch what plausibility misses — but it tells you
only *that* the books do not balance. Every step from there to the cause was a guess, and four
of them were wrong. What ended it was not a better guess; it was building the smallest system
where the answer could not hide.
