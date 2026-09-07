# 0008 — What an energy audit found

**2026-08-04**  ·  Milestone 2

[`DESIGN.md`](../DESIGN.md) §5A makes energy a conserved budget, so the first thing
Milestone 2 needed was a measurement of what a creature actually spends. The first run said
kilowatts, and that number was one commit from becoming the calibration basis for the whole
energy economy. About six-sevenths of it was not what the column said. Finding that out took
four wrong explanations and turned up three real defects along the way.

## The measurement, and the check that stopped it

Joint power is torque times angular velocity, written `τ·ω`. Both quantities were already
in hand, because the driver knows the torque it applied and the bodies know how fast they
are turning. Seed 3 reported 46,393 J over 8 seconds. That is 5,799 W, or 170,537 joules per
metre travelled.

Water is dense. A 300 kg limb moving at a few metres per second really does cost that much.
The number survived a back-of-envelope check.

What it did not survive was the audit. With no gravity, no contact and no ground, the signed
work done at the joints has to equal the change in kinetic energy plus whatever the drag
model dissipated:

`signed joint work = ΔKE + energy dissipated by drag`

Nothing else acts on the creature. If that does not close, the number is arithmetic on the
wrong quantity.

It did not close. 85% of the claimed work was unaccounted for.

What follows is four wrong explanations in a row, which is the point of this entry.

## First wrong explanation: the joints were slamming into their limits

A joint limit is a hard constraint. A limb arriving at one is stopped dead and its kinetic
energy is destroyed there, which is a sink appearing in neither term of the audit. I
estimated about 470 J per impact and about 13 impacts per joint over 8 s, which lands
squarely inside the 13,700–63,000 J residual range.

Landing in the right range is how a wrong explanation survives, so I swept the drive
strength. If limit-slamming is the sink, a weaker drive should collapse the residual. The
thing to notice about this table is that nothing in it moves.

| torque N·m/kg | joint work J | unaccounted | at limit |
|---|---|---|---|
| 0.05 | 54526.6 | 90.4 % | 44.2 % |
| 0.2 | 54526.6 | 90.4 % | 44.2 % |
| 0.5 | 54526.6 | 90.4 % | 44.2 % |
| 1 | 54526.6 | 90.4 % | 44.2 % |
| 2 | 54526.6 | 90.4 % | 44.2 % |

Identical across a 40× sweep. That is not a physics result. The sweep had never happened.

## The bug the sweep found instead

`TorqueScale` was an auto-property read once, in the constructor, while every call site set
it by object initializer, which runs after construction. So every
`new EffectorDriver(c) { TorqueScale = x }` silently kept the default.

That includes [`CreatureSpawner`](../unity/Assets/Evosim/Sim/CreatureSpawner.cs), which
means the sandbox's inspector field had never done anything at all. Every creature ever
watched on screen ran at 2 N·m/kg whatever the Editor said.

Byte-identical output across a configuration change means the change was not applied. That
tell had already appeared once this week, when self-collision "made no difference"
([logbook 0007](0007-the-creature-that-was-paid-to-jam.md)). It was the same tell for the
same reason.

## Second wrong explanation: still the joint limits

With the sweep actually working, the residual stayed at about 85% at every drive strength,
including 0.05 N·m/kg, where no joint touched a limit at all. So the explanation is refuted.

## Third wrong explanation: the drive damping

`ArticulationDrive.damping = 1` is set on every joint, deliberately, so that undriven joints
settle instead of ringing. Measured, it accounts for 0.5–1% of the work. That is not the
sink either.

## What it actually was, part one

The residual sat at about 85% regardless of drive strength across a 40× range. A loss that
does not scale with the drive is the signature of a linear damping term.

`ArticulationBody.angularDamping` defaults to 0.05. So does `jointFriction`.

[`PhenotypeBuilder`](../unity/Assets/Evosim/Sim/PhenotypeBuilder.cs) never set either of
them.

The creatures had been swimming in two fluids at once. Ours is specified in §5.2,
unit-tested to prove it can never add energy, and motivated by [U07]'s account of a
published search that exploited that very flaw. PhysX's was chosen by nobody, appeared in no
figure, and removed roughly ten times more energy than ours did.

Every displacement number this project had produced was measured against the wrong
resistance.

Zeroing it raised drag dissipation from 4,672 to 5,705 J on the same creature. Energy that
had been vanishing now reached the water.

## What it actually was, part two

With PhysX's damping gone, the residual finally tracked limit contact: 39% at 0.05 N·m/kg
and 88% at 2. But drive strength moves both variables at once, so the correlation is not
causal. Holding the drive fixed and widening only the joint ranges gives the table below.
The column to watch is drag out.

| limits | joint work J | drag out J | unaccounted | at limit |
|---|---|---|---|---|
| 1× | 56511.8 | 5705.5 | 88.0 % | 47.6 % |
| 2× | 58683.9 | 13053 | 75.5 % | 24.3 % |
| 4× | 59385.2 | 33602.6 | 34.1 % | 13.3 % |
| 20× | 57298.4 | 66196.6 | −21.9 % | 6.8 % |

Work barely changes while drag dissipation rises 12×. The energy was always there. The limit
constraint was destroying it before it could reach the water.

Two sinks had been superposed, and that is why the first sweep looked flat. PhysX damping
dominated at low drive, and joint limits dominated at high drive. Removing the first exposed
the second.

## Fourth wrong explanation: a convergence study on a chaotic system

The −21.9% at 20× said the accounting now erred the other way, so I changed both integrals
from left-rectangle to midpoint. Residuals fell from about 85% to 0.5–19%, with mixed sign,
which looks like discretisation error.

Testing that properly means halving the timestep dt and watching the error fall. It did not
fall.

| seed | dt=0.01 | dt=0.005 | dt=0.0025 |
|---|---|---|---|
| 1 | −13.3 % | −27.8 % | −159.6 % |
| 3 | −17.1 % | −32.8 % | −30.9 % |
| 4 | −19.2 % | 6.3 % | 6.5 % |
| 6 | 18.5 % | 12.1 % | 7.1 % |

Two seeds converge, two diverge, and one of those diverges catastrophically. But the test
was invalid. With limits widened and the drive at full strength these creatures are chaotic,
so halving dt does not refine one trajectory. It produces a different one. Those four
columns are not four accuracies of a single run. They are four unrelated runs, and reading
convergence off them means nothing.

## The instrument that settled it

Two 0.5 m cubes, one hinge, constant torque. No chaos, so refining dt means what it ought to
mean.

The first attempt reported 1500 J at every timestep, to the digit. Each cube is 125 kg plus
125 kg of added mass, which is the water a body has to drag along with it. At 2 N·m/kg that
is 500 N·m, and the limit sat at 3 rad. 500 × 3 = 1500.

A constant torque drives a hinge to its stop and holds it there. But the number is `τ·Δθ` to
four significant figures, independent of dt. That is a textbook verification that the
joint-power measurement is right, arrived at by accident while trying to test something
else.

Widening the limit to ±100 rad gave 3141.6 J, which is 500 × 2π. PhysX does not honour a
revolute limit past one revolution. A limit cannot be moved out of the way, only removed.
With `ArticulationDofLock.FreeMotion`:

| dt | joint work J | drag out J | residual |
|---|---|---|---|
| 0.01 | 46542.6 | 38589.1 | 6.60 % |
| 0.005 | 47958.6 | 40575.2 | 3.55 % |
| 0.0025 | 48476.4 | 41899.8 | 1.99 % |
| 0.00125 | 48707.2 | 42608.5 | 1.04 % |

Halving dt halves the error, which is first-order convergence. The measurement had never
been wrong.

## What the residual was all along

Real energy, destroyed by joint limits. At the default ranges that is about 85% of
everything a creature spends.

That is a finding about calibration rather than a defect. Under §5A it is charged as
metabolic cost, and that is defensible, because a muscle slamming a joint does spend the
energy. But it means the metabolic cost is currently dominated by bang-bang actuation rather
than by swimming. An open-loop sine wave has no way to decelerate before a stop. Judging
`TorqueScale` fairly needs the brain graph of Milestone 6. So the check became a named
report rather than an assertion: *energy into joint limits*.

## The one that was hiding behind the fix

Zeroing PhysX's damping was correct, and it exposed a second thing that the damping had been
suppressing. With self-collision on, seed 2 went from 0.006 to 0.045 m²/s of specific
angular momentum, growing 1.7× over 2× the time. That is injection accumulating rather than
error random-walking.

Contact was manufacturing momentum and the damping had been bleeding it off unseen. The
depenetration cap from [logbook 0007](0007-the-creature-that-was-paid-to-jam.md), which
limits how fast the solver may shove overlapping bodies apart, went from 0.5 m/s to 0.02.

| cap m/s | seed 2, specific angular momentum |
|---|---|
| 0.5 | 0.0447 |
| 0.1 | 0.0318 |
| 0.02 | 0.0191 |

Monotone in the cap, so the injection is bounded and not eliminated. Seed 2 still grows at
1.6×, and 0.019 sits uncomfortably close to the 0.023 clean floor. Recorded as an open
weakness rather than a closed one.

## What this cost, and what it bought

One measurement, four wrong explanations, three real defects.

1. A configuration knob that had never worked, including one exposed in the Editor UI.
2. A second, unchosen drag model acting on every creature since Milestone 1.
3. Momentum injection through contact, previously masked by the second of those.

And one non-defect that matters more than any of them. The drive is far stronger than the
joint ranges can absorb, so most of what a creature spends is destroyed against its own
stops.

The kilowatt figure was defensible and survived a sanity estimate. Six-sevenths of it was
not what the column said.

The pattern from [0002](0002-the-spike-that-was-too-fast.md),
[0004](0004-two-ways-to-report-a-success-you-dont-have.md),
[0005](0005-the-creatures-were-swimming-in-vacuum.md), [0006](0006-boxes-inside-boxes.md)
and [0007](0007-the-creature-that-was-paid-to-jam.md) holds, with one addition. A
conservation law does catch what plausibility misses, but it tells you only that the books
do not balance. Every step from there to the cause was a guess, and four of those guesses
were wrong. What ended it was building the smallest system in which the answer could not
hide.
