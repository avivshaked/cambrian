# 0022 — The conveyor belt, and the thin soup

**2026-08-26**  ·  Milestone 4

[Entry 0021](0021-the-food-all-fell-to-the-bottom.md) ended with a current specified, never built,
and resolving three separate measured failures at once. This entry builds it — as two mechanisms
rather than one, because they are different physics and neither does the other's job.

## Two things, not one

**Moving water** (`CurrentField`) enters exactly where §5A.4 said it would: drag is computed
against a velocity, so subtracting the water's velocity from the body's turns drag into advection
for one evaluation per part. Nothing else in the fluid model changed.

**Stirring** (`NutrientField.Mix`) is separate, and it has to be, because detritus is not physical.
A corpse is not an object here — on death the tissue energy is deposited into a scalar field
indexed by depth and the articulation is destroyed. The current cannot touch it. That is not an
oversight: §6.3 tiles creatures a hundred metres apart so they cannot collide, which is what makes
predation a Milestone 7 problem, and a physical corpse would sit in its own tile where nothing
could ever reach it. The scalar field is what lets a creature at −8 m eat detritus at −8 m without
being in the same place, and it matches the one-dimensional ecology tiling forces.

Mixing is Fick's law across layer interfaces rather than a per-layer average, so it conserves every
joule regardless of timestep, and it is clamped at a Courant number of ½ rather than sub-stepped —
an unstable explicit diffusion would oscillate a layer negative, and conservation would faithfully
preserve the resulting debt.

## The first version was a conveyor belt

The field was a travelling wave. Its time-average velocity at every fixed depth is exactly zero,
there is a test asserting it, and the test passed.

The first embodied run carried the entire population **six metres above the surface** and was still
climbing when it ended. Mean depth went −8.3 → −3.1 → +1.0 → +2.7 → +6.0.

The mean velocity at a fixed point is the **Eulerian** mean. What matters is the mean displacement
of something the water is *carrying*, which is the **Lagrangian** mean, and for a travelling wave
these are not the same number: a particle rides along with the phase. I wrote a guard against
exactly this failure — its comment says *"a field with a nonzero time-mean is a conveyor belt… it
would carry every creature and every particle steadily in one direction"* — and then measured the
wrong mean.

The replacement is two standing waves at incommensurate periods. Each term is `sin(ky)·sin(ωt)`,
antisymmetric about the half-period, so the second half of a cycle undoes the first *exactly* and a
particle in one term returns precisely home. That is zero drift by symmetry rather than by
cancellation. One such term would also mix nothing — every particle home every cycle, so a creature
born deep stays deep — which is why there are two, with cell heights and periods in the ratio of the
golden section. The sum never repeats, so trajectories separate. That is chaotic advection, and it
disperses without a mean.

Three tests now hold it, and the useful one is not "the mean is small":

> **The mean is never exactly zero over a finite window**, because incommensurate periods guarantee
> a partial cycle left over. What separates that remainder from a real bias is that it *shrinks* as
> the window grows. So the test quadruples the window and requires the mean to halve — which a slow
> conveyor would fail and a sampling residual passes.

And its companion asserts the tension directly: large mean **absolute** displacement (things are
mixed) with near-zero mean **signed** displacement (nothing is carried away). Either alone is easy
and useless.

## What it bought

Same seed, same settings, 64 W/m², with and without:

| | still water | current + mixing |
|---|---|---|
| detritus on the floor | **77.5%** | **2.7%** |
| food density where creatures live | **0** | **0.18 J/m³** |
| distance from birth depth, per life | **0.006 m** | **0.34–0.70 m** |
| audit residual | 0.0000% | 0.0000% |

**The energy return path exists.** The world no longer drains its own fertility onto a floor
nothing can reach. And a creature now moves a hundred times further from where it was born, which
is the ratio [0021](0021-the-food-all-fell-to-the-bottom.md) identified as making swimming
invisible to selection: it was 1:3300 against the founder scatter and is now nearer 1:30.

## And absorptive creatures still starve

Five alive at t=500, two at t=1000, none by t=1500. Food income back to 0%.

The break-even is clean, and it is size-independent — income and upkeep both scale with volume, so
the body cancels:

> **break-even density = upkeep ÷ clearance = 4 ÷ 0.5 = 8 J/m³**

The world produces **0.18**. Short by a factor of forty-four, and no body plan can fix it because
the ratio does not contain one.

Chase that to its root and it is not really about absorptive cells at all. A cubic metre of tissue
is worth **500 J** and costs **3–4 W** to keep, so **a body is worth about two minutes of its own
metabolism.** Everything follows from that. Detritus reaching 8 J/m³ would need the water to hold
roughly sixty times the world's entire standing biomass in corpses — and since nothing remineralises
detritus (§5A.4 flags this), the pool does grow without bound and *would* get there eventually. At
the observed 0.7 J/s it is about 260,000 simulated seconds away, some hours of wall clock, and the
population runaway would end the run long first.

Worth noting the human predicted this mechanism before any of it was measured: *"the world should
have more and more material as entities die… as the world gets more populated with food, then
animals that consume the dead matter should start to be more common."* That is exactly right and
exactly what the pool does. The objection is not to the mechanism but to the constant in front of
it.

## What is still not fixed

Joints still go to zero by t=1000. The population still runs away — 781 alive and climbing at
t=3000 with only 98 deaths against 1,164 births, an 8% death rate. **A world where almost nothing
dies is a world where almost nothing is selected**, and that is upstream of every other question
here: the current and the mixing both make swimming *matter more*, and neither makes selection
*act more*.

## The pattern

0018: a hypothesis confirmed and still not the answer. 0019: three knobs never read. 0020: a
mechanism over a substrate that did not exist. 0021: the previous entry's diagnosis wrong in the
way this logbook keeps recording.

This one: **I wrote the guard, named the failure in its comment, and then measured a different
quantity than the one the comment described.** Not a missing test — a test whose assertion did not
match its own stated intent, which is worse, because it is a guard that reports success. The gap
between *the mean velocity at a point* and *the mean displacement of something carried* is one word
in English and two different physical quantities, and the population went into the sky through it.
