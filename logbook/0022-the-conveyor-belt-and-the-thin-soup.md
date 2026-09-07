# 0022 — The conveyor belt, and the thin soup

**2026-08-26**  ·  Milestone 4

[Entry 0021](0021-the-food-all-fell-to-the-bottom.md) ended with a current specified, never
built, and resolving three separate measured failures at once. This entry builds it, as two
mechanisms rather than one, because they are different physics and neither does the other's
job.

## Two things, and not one

Moving water, in `CurrentField`, enters where §5A.4 said it would. Drag is computed against
a velocity, so subtracting the water's velocity from the body's turns drag into advection,
for one evaluation per part. Nothing else in the fluid model changed.

Stirring, in `NutrientField.Mix`, is separate, and it has to be, because detritus is not
physical. A corpse is not an object here. On death the tissue energy is deposited into a
scalar field indexed by depth, and the articulation is destroyed. The current cannot touch
it.

That is not an oversight. §6.3 tiles creatures a hundred metres apart so they cannot
collide, and that is what makes predation a Milestone 7 problem. A physical corpse would sit
in its own tile where nothing could ever reach it.

The scalar field is what lets a creature at −8 m eat detritus at −8 m without being in the
same place. It matches the one-dimensional ecology that tiling forces.

Mixing is Fick's law across layer interfaces rather than a per-layer average, so it
conserves every joule regardless of timestep. It is clamped at a Courant number of ½ rather
than sub-stepped. An unstable explicit diffusion would oscillate a layer negative, and
conservation would faithfully preserve the resulting debt.

## The first version was a conveyor belt

The field was a travelling wave. Its time-average velocity at every fixed depth is zero,
there is a test asserting it, and the test passed.

The first embodied run carried the entire population six metres above the surface, and it
was still climbing when it ended. Mean depth went −8.3, then −3.1, then +1.0, then +2.7,
then +6.0.

The mean velocity at a fixed point is the Eulerian mean. What matters is the mean
displacement of something the water is carrying, which is the Lagrangian mean, and for a
travelling wave those are not the same number. A particle rides along with the phase.

I had written a guard against this very failure. Its comment says this:

> *a field with a nonzero time-mean is a conveyor belt… it would carry every creature and every
> particle steadily in one direction*

And then I measured the wrong mean.

The replacement is two standing waves at incommensurate periods. Each term is
`sin(ky)·sin(ωt)`, antisymmetric about the half-period, so the second half of a cycle undoes
the first and a particle in one term returns home. That is zero drift by symmetry rather
than by cancellation.

One such term would also mix nothing, since every particle comes home every cycle, so a
creature born deep stays deep. That is why there are two, with cell heights and periods in
the ratio of the golden section. The sum never repeats, so trajectories separate. That is
chaotic advection, and it disperses without a mean.

Three tests now hold it, and the useful one is not "the mean is small". The mean is never
zero over a finite window, because incommensurate periods guarantee a partial cycle left
over. What separates that remainder from a real bias is that it shrinks as the window grows.
So the test quadruples the window and requires the mean to halve, which a slow conveyor
would fail and a sampling residual passes.

Its companion asserts the tension directly: large mean absolute displacement, meaning things
are mixed, with near-zero mean signed displacement, meaning nothing is carried away. Either
one alone is easy and useless.

## What it bought

Same seed, same settings, 64 W/m², with the two mechanisms and without them.

| | still water | current + mixing |
|---|---|---|
| detritus on the floor | **77.5%** | **2.7%** |
| food density where creatures live | **0** | **0.18 J/m³** |
| distance from birth depth, per life | **0.006 m** | **0.34–0.70 m** |
| audit residual | 0.0000% | 0.0000% |

The energy return path exists. The world no longer drains its own fertility onto a floor
nothing can reach. And a creature now moves a hundred times further from where it was born,
which is the ratio [0021](0021-the-food-all-fell-to-the-bottom.md) identified as making
swimming invisible to selection. It was 1:3300 against the founder scatter, and it is now
nearer 1:30.

## And absorptive creatures still starve

Five alive at t=500, two at t=1000, none by t=1500. Food income is back to 0%.

The break-even is clean, and it is size-independent, because income and upkeep both scale
with volume so the body cancels. Break-even density is upkeep divided by clearance:
4 ÷ 0.5 = 8 J/m³.

The world produces 0.18. That is short by a factor of forty-four, and no body plan can fix
it, because the ratio does not contain one.

Chase that to its root and it is not really about absorptive cells at all. A cubic metre of
tissue is worth 500 J and costs 3–4 W to keep, so a body is worth about two minutes of its
own metabolism. Everything else follows from that.

Detritus reaching 8 J/m³ would need the water to hold roughly sixty times the world's entire
standing biomass in corpses. Since nothing remineralises detritus, which §5A.4 flags, the
pool does grow without bound and would get there eventually. At the observed 0.7 J/s that is
about 260,000 simulated seconds away, or some hours of wall clock. The population runaway
would end the run long before it.

The human predicted this mechanism before any of it was measured.

> *the world should have more and more material as entities die… as the world gets more populated
> with food, then animals that consume the dead matter should start to be more common.*

That is right, and it is what the pool does. The objection is to the constant in front of
the mechanism rather than to the mechanism.

## What is still not fixed

Joints still go to zero by t=1000. The population still runs away, with 781 alive and
climbing at t=3000, and only 98 deaths against 1,164 births, an 8% death rate. A world where
almost nothing dies is a world where almost nothing is selected, and that sits upstream of
every other question here. The current and the mixing both make swimming matter more, and
neither makes selection act more.

## The pattern

Entry 0018 was a hypothesis confirmed and still not the answer. Entry 0019 was three knobs
never read. Entry 0020 was a mechanism over a substrate that did not exist. Entry 0021 was
the previous entry's diagnosis wrong in the way this logbook keeps recording.

This one is that I wrote the guard, named the failure in its comment, and then measured a
different quantity from the one the comment described. It is not a missing test. It is a
test whose assertion did not match its own stated intent, which is worse, because it is a
guard that reports success. The gap between the mean velocity at a point and the mean
displacement of something carried is one word in English and two different physical
quantities. The population went into the sky through it.
