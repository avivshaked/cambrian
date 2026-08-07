# 0011 — The sun was infinite

*2026-08-07*

The plan was to run one experiment. §5A.2 calls the ratio of basal metabolism to peak
photosynthesis *the knob that decides everything*, [D022](../DECISIONS.md#d022) said the value
need not be known — only located, by sweeping until the phase transition announces itself — and
[the ecosystem loop](../src/Evosim.Core/Ecosystem/World.cs) had just been built to do it. Sweep
surface irradiance over 400×, read the table, write down the number.

The number does not exist. Four separate things had to be found and fixed before the sweep meant
anything, and each one was invisible until the one before it was out of the way.

## 1. The sweep gave a confident wrong answer

The first run, at 4,000 simulated seconds per setting, produced a clean table. At 48 W/m² it
reported a stable world of 259 creatures, floor-fed, median generation depth 6. Below that,
nothing reproduced. Above 64, runaway. A tidy window.

Running the same setting for 30,000 s instead:

| t (s) | population |
|---|---|
| 1,000 | 30 |
| 2,000 | 37 |
| 3,000 | 84 |
| 4,000 | **259** ← where the sweep stopped looking |
| 5,000 | 907 |
| 5,303 | ceiling breached |

It was not stable. It was an exponential, sampled one tripling before it became obvious. **A
truncated run cannot tell a steady state from an exponential caught early**, and the shorter the
run, the more confidently it reports the wrong thing.

The other half of the same mistake showed up later: 96 W/m² read as a runaway against a ceiling
of 1,500, and was perfectly regulated at a ceiling of 50,000. A ceiling near the carrying
capacity you are trying to measure is a measuring instrument that changes the answer.

## 2. There was no transition to find

Deaths told the real story. Over the run above, the population went from 30 to 907 while total
deaths went from 80 to 104. **After the first die-off, essentially nothing died.**

The reason is structural, not a matter of calibration. A creature's income depended on its own
depth and nothing else. So every creature above break-even accumulated surplus at a fixed rate
and bred on a fixed period regardless of how many others existed — a linear birth process, which
grows without bound above break-even and goes extinct below it. **A step function, not a
transition.** The knob only ever decided how fast.

Sweeping harder would never have found anything. What was missing was *density dependence*: no
mechanism by which one creature's existence cost another anything.

The fix is [D023](../DECISIONS.md#d023) — make the sun finite. The world has a horizontal area,
receives `irradiance × area` watts and no more, and light one creature absorbs never reaches what
is below it. This was preferred over a crowding penalty with a coefficient because it is a
conservation law rather than another tuning parameter, which is the same reason §11.2 prefers
momentum invariants to plausibility checks. Carrying capacity stopped being a number we choose.

Two details in [`LightField`](../src/Evosim.Core/Environment/LightField.cs) were not obvious:

- **Store a multiplier, not an irradiance.** The first version gave everyone in a layer the light
  entering the top of that layer. So drifting from open water into an occupied layer *raised*
  your income — free energy manufactured by the discretisation, pointing exactly the direction
  evolution looks. Storing a factor in (0, 1] against exact Beer–Lambert cannot do that.
- **`1 − exp(−x)` in float is wrong where it matters most.** For a shadow of 10⁻⁴ m² in a 400 m²
  world, `1f − exp(−2.5e−7)` evaluates to 2.384e−7 against a true 2.5e−7: the subtraction cancels
  every significant bit and leaves the spacing of floats near 1. That is a 4.6% light tax on an
  almost-empty world, and it gets *worse* as the world empties. Caught by a test asserting the
  field reduces to the unshaded model — which it is worth noting is a test of a property, not of
  a value, and no test of a value would have found it.

## 3. Evolution found the free lunch immediately

With light finite, populations bounded — and shadow areas reached **10³⁷ m² in a 400 m² world**.

Half-extent mutation perturbs by a Gaussian scaled to the half-extent itself, which is geometric
Brownian motion: log-size diffuses without bound and has no stationary distribution. §4.5 *relies*
on the lower tail being absorbed by `MinPartVolume` — extinction-by-shrinking is the only thing
that removes nodes, and it is why genome size settles near 39 nodes without any rate saying so.
**Nothing absorbed the upper tail.** Bodies grew until volume passed `float.MaxValue` and the
energy audit became permanently NaN.

Adding `MaxPartVolume` did not fix it, which was the interesting part. **Volume does not bound
surface area.** A box of half-extents (10⁻²⁵, 10⁻⁵, 10³⁰) has a volume of 8 m³ and a surface of
10²⁶ m², and passes a volume limit without complaint. Income scales with area and upkeep with
volume, so the cheapest way to earn is to be thin, without limit. This is §11.2's physics
exploitation moved into the economy — a free lunch discovered by evolution rather than designed
in, and our own arithmetic handed it over.

Measured, holding volume fixed at that of a 0.3 m cube:

| thickness | lit area | income/upkeep |
|---|---|---|
| 0.3 m | 0.54 m² | 1.0 |
| 0.1 m | 0.75 m² | 1.4 |
| 0.03 m | 1.91 m² | 3.5 |
| 0.01 m | 5.47 m² | 10.1 |

The obviously principled fix — an area-proportional upkeep term, since real tissue costs to
maintain per unit area, which is why leaves have a minimum viable thickness — **was worked
through and rejected because it does not work.** Income and an area cost both scale linearly with
area, so their difference still scales linearly and thinness is still unbounded. The coefficient
can only choose between "thinning is free" and "thinning never pays", with nothing in between.
What actually bounds a body is that the world's light runs out. `MinPartHalfExtent` was added
instead, and it clamps rather than prunes, because flatness is a trait worth keeping: a flat box
is the strongest paddle in the shape registry.

Not adding the area term is the whole value of having checked. Recorded in
[D024](../DECISIONS.md#d024).

## 4. Two guards had the hole they existed to catch

**The self-sustaining test was unreachable.** [D022](../DECISIONS.md#d022) defined it as minimum
generation depth above zero — no living creature is a floor spawn. But nothing dies of age; §5A.6
kills only at zero energy, so a founder whose income covers its upkeep never dies. A handful of
immortal generation-zero photosynthesisers pin the minimum at zero forever. Worlds that had not
needed the floor for 17,000 s, running at median depth 78 with births and deaths in balance, were
reported as floor-fed. **The instrument was measuring immortality, not dependence.** D022 had
explicitly rejected *time since the floor last fired* as too coarse; that is now what is used
([D025](../DECISIONS.md#d025)).

**And the config-hash guard had forgotten a whole object.** `RunConfigTests` walks every tunable
by reflection so that adding one without folding it into the hash fails immediately. It walked
`RunConfig` and `RandomGenomeOptions` — not `DevelopmentLimits`. So `MaxPartVolume` reached
neither the hash nor the JSON, silently, and the test that exists to prevent exactly that passed.
It now walks every sub-config, named in a literal list rather than discovered by reflection:
reflection would have missed this case too, since `Shapes` and `CellTypes` are registries with no
settable scalars and cannot be exercised the same way, so an automatic walk quietly passes for
whatever it did not think to look at.

A third, smaller: a genome that develops into no parts had zero income and zero upkeep, so its
energy never moved and death-at-zero never fired. An immortal creature costing nothing, doing
nothing, holding a slot against the population floor. Reachable today — extinction-by-shrinking
prunes the root as readily as any other node.

## What the sweep says now

Three seeds each, 20,000 s of world per setting, attenuation 1/e at 12 m, 400 m² aperture:

| Surface irradiance | Outcome |
|---|---|
| 4 – 24 W/m² | Nothing reproduces. Max depth 1, floor firing continuously |
| **32 W/m²** | **Lineages establish — median depth 15–79, floor falls silent** |
| 48 – 400 W/m² | Establishes; more light buys fewer, larger creatures, not more of them |

The transition is sharp, consistent across seeds, and sits where the arithmetic said it would:
break-even for a 0.3 m photosynthetic cube, where lit area × irradiance × efficiency equals
volume × upkeep, is 24 W/m². The measurement and the hand calculation agree, which is the point
at which either becomes worth believing.

Nothing runs away above the transition. Every setting to 400 W/m² settles at tens to hundreds of
creatures against a ceiling of 50,000, with total shadow 10–290× the world's area. And more light
produces *bigger* creatures rather than more of them, because a large body shades its
competitors — which nothing in the model was told to do.

## What is still wrong

**The world has no length scale.** At 400 W/m² the survivors carry thousands of square metres of
surface in an aperture 20 m across. Nothing relates a body's size to the world's size except the
light budget, because `Evosim.Core` has no positions beyond depth. That constraint arrives with
the physics simulation at Milestone 4, and until then the sizes here should be read as an economic
result rather than a physical one.

**Nothing dies of age**, which is what made instrument (4) unreachable and which leaves the
standing population a set of immortal breeders. Whether senescence belongs in the design is now a
visible open question rather than an unexamined absence.

## The pattern, again

[Logbook 0010](0010-the-world-starts-with-almost-nothing.md) ended on the observation that its
findings were all things *working exactly as written*, where what was written had never been
checked against what it claimed. This entry is four more of them, and the sequence matters: the
sweep was wrong because the runs were short; the runs being short hid that there was no
transition; there being no transition hid the thinness exploit; the thinness exploit hid the two
broken guards. **None of them could be found before the one in front of it was fixed**, which is
an argument for measuring at every step rather than building three systems and then measuring.

The one that should sting is the hash guard. It was written specifically because a parameter
silently failing to reach what it configures has happened twice on this project, it was
reflection-driven precisely so it could not be forgotten — and it was checking two objects out of
four. A guard you do not test the coverage of is a guard you are trusting on the strength of its
intent.
