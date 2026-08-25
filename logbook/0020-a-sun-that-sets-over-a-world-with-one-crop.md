# 0020 — A sun that sets, over a world with one crop

**2026-08-25**  ·  Milestone 4

[Entry 0019](0019-three-knobs-that-reached-nothing.md) ended with joints persisting, nothing
swimming, and a hypothesis: the population had climbed to −1.5 m by differential survival, light
attenuates over 12 m, so a lineage already at the top has almost nothing left to gain by ever
moving. **The gradient had been solved by sitting in the right place, and a solved gradient is not
a reason to travel.**

The design's own answer to that is §5A.4's diurnal cycle — the thing that makes the best depth a
moving target rather than a fixed one, and the thing §4.4 says the `Depth` sensor exists for. It is
built. It works. **It changed nothing, and the reason is one number.**

## Building it as one unknown instead of two

`LightModel` carried a paragraph explaining why there was no cycle yet, and the objection was
good: a cycle turns §5A.2's calibration from *does light cover upkeep* into *does light cover
upkeep averaged over a period, and can anything survive the trough* — two unknowns at once, before
either had been measured alone.

That objection is answered by construction rather than by deferral. The cycle is
**mean-preserving**: `SurfaceIrradiance` stays the daily mean and the amplitude modulates around
it, so amplitude 0 is the acyclic world *exactly* and turning it up does not move the world's
energy budget by one joule.

The obvious shape was the wrong one. `max(0, sin)` gives a true half-day night and averages to 1/π
of its peak — so switching it on at a fixed irradiance would quietly cut the world's income to a
third and present as a diurnal effect. A tenth of the day's difficulty would have been the cycle
and nine tenths would have been an unannounced 68% cut to the sun.

Two guards, both written before the first run: the daily mean of the factor is 1 to three decimal
places at full amplitude, and at amplitude 0 the field returns bit-identical irradiance across 500
advances of the clock.

## Where the phase does not live

First attempt put a mutable `DayFactor` on `LightModel`. The reflection guard from
[D027](../DECISIONS.md#d027) rejected it within a second — *"settable but not `[Tunable]`:
Light.DayFactor"* — and it was right for a reason worth writing down: **`LightModel` is
configuration.** Every other member is a tunable, it is what §7 hashes, and *where the world has
got to* is not part of *how the world was set up*. Two runs differing only in how far through them
you look are not two configurations.

The phase moved to `LightField`, which already holds the other thing about light that changes every
step — who is shading whom.

The second guard failed too, and that one was a real hole in the guard rather than in the code. It
nudges every float tunable by +7.5 to check it reaches the hash, and `DayNightAmplitude` is a
fraction that refuses anything outside [0, 1]. A fixed nudge quietly demands that every knob in the
project be unbounded, which is the opposite of what §7 wants — *loading refuses rather than
defaults*, and a knob that rejects nonsense is doing its job. It now shrinks the nudge until one
sticks, and still fails loudly if none does.

## The measurement

Four runs at 100 W/m², 1500 s, cycle off and at full amplitude, two seeds:

| amplitude | seed | alive | deaths | jointed | **food %** | depth m | depth sd |
|---|---|---|---|---|---|---|---|
| 0 | 1 | 889 | 69 | 3 | **0%** | −1.5 | 1.67 |
| 0 | 2 | 631 | 51 | 0 | **0%** | −4.0 | 1.56 |
| 1 | 1 | 507 | 180 | 3 | **0%** | −1.7 | 2.18 |
| 1 | 2 | 429 | 134 | 0 | **0%** | −4.2 | 1.83 |

The `sun` column cycles 0 → 2 → 0 as it should, so the knob reaches what it configures. Nothing
migrates. Depth spread rises by about a quarter and mean depth does not move.

**Food income is 0% of all income, in every run.** Not small — zero.

So the chain is closed, and every link of it is measured rather than argued:

> Absorptive cells are effectively unreachable from a founder → nothing earns from nutrients →
> light is the only income → light is monotonically decreasing in depth at every hour → the
> surface wins at every hour → there is no optimum to track → a moving sun moves nothing.

A cycle needs *two* incomes pulling in opposite directions before it can move a balance point.
There is only one crop in this world, and it grows at the top.

## The rate that assumes a body nobody has

`MutationRates.CellTypeChance` is 0.001, and its own documentation explains the choice:

> A body has around eight parts, so a per-node chance of 0.006 changes something in roughly one
> birth in twenty (…). At 0.001 it is about one birth in a hundred and twenty.

**Founders have 1.51 parts.** So the effective rate is nearer one birth in six hundred, and only a
fifth of those land on absorptive — call it one in three thousand, in runs of about a thousand
births. The estimate is not wrong; it was made for a body plan the population does not have.

Which is the same fault as [0018](0018-nothing-to-swim-towards.md), in a different subsystem. That
entry found founders averaging 1.5 parts against the 5.8 of a full random genome, and concluded
that reasoning calibrated on mature bodies does not transfer to the population that actually
exists. This is a per-node rate documented against eight nodes, running on one and a half.

## The half of the objection that was right

The cycle preserves the mean exactly and **deaths roughly tripled** — 69 → 180 and 51 → 134 — with
the population ending about 40% lower. Identical settings, identical seeds, same total energy
delivered.

That is not a leak; the audit closes at 0.0000% in all four. It is that **death is a threshold, and
the threshold of an average is not the average of a threshold.** A creature that runs out of energy
at midnight does not get to average over the following noon. Mean-preserving buys comparability of
the *budget*; it does not buy comparability of the *world*, and the original objection's second
half — *can anything survive the trough* — turns out to have been the load-bearing one.

## The pattern

0018: a hypothesis confirmed decisively and still not the answer. 0019: three knobs that were
declared, stored, hashed and never read.

This one: **the mechanism worked on the first run and the result was still flat**, because the
thing it acts on does not exist yet. Nothing was broken and nothing was mismeasured — the guards
all fired where they should, the audit closed, the sun rose and set. A working mechanism with no
substrate looks exactly like a mechanism that does not work, and the only thing that told them
apart was a column reporting 0%.

The lesson is the cheap one: **before building a mechanism that acts on a quantity, measure the
quantity.** One column would have said, before any of this was written, that this world has one
income and cannot have a moving optimum.
