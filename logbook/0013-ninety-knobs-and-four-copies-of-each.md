# 0013 — Ninety knobs and four copies of each

**2026-08-07**  ·  Milestone 2

This entry covers one session's worth of a single question — *what in this simulation is
configurable, and how would you know* — and it went through three answers, each of which
exposed the previous one as too small.

## The first answer: audit the energy knobs

The ask was that every energy cost be configurable, so that a run can be made fairer and so that
the effect of a cost can be measured rather than assumed. That reads like an audit: list the
places §5A spends or earns energy, check each is a settable property, fix the ones that are not.

Two were not.

**`ConsumerCell` scavenged at a hardcoded 1.** It appeared in the code as a bare coefficient in an
expression, which is the form a constant takes when nobody decided it — it is the multiplicative
identity, so it is invisible. It is now `ScavengeRate`, separate from `BiteRate` because the two
limit feeding in different regimes and there is no reason they should move together.

**`LightModel` was handed to the world beside its config rather than inside it.** So it was not in
the `RunConfig`, so it was not in the hash. Every run of the §5A.2b irradiance sweep — from the
extinct end at 24 W/m² through the transition to the runaway end — carried **one identical
`configHash`**. The sweep's whole finding was that irradiance decides everything, and the record
of it said all those runs were the same experiment.

That is §7 failing in the direction it least wants to. Not a hash that changes when it shouldn't,
which is annoying and obvious; a hash that *doesn't* change when it should, which is silent and
makes the archive wrong rather than noisy.

## The second answer: the ask was bigger than the audit

Then: *anything that costs energy should be configurable* — and separately, *anything that
absorbs energy too*. Which turned an audit into a rule. §5A.10 now carries the whole table, and
the rule that no number moving energy may be a constant in a class.

Applying it surfaced `AbsorptiveCell` keeping 100% of what it filters, with no way to say
otherwise. A yield of 1 is a *claim* — that filter feeding is lossless — and it had never been
stated, only implied by there being nothing to set. It is now `Yield`, still defaulting to 1, but
the default is now visible as a choice.

`EnergyKnobTests` came out of this: for each cell type it mutates every numeric field in the saved
JSON, reloads, and demands the hash move — writable, readable and identifying proven in one pass —
and separately drives each ledger term end to end to check the knob reaches the arithmetic.

## The third answer: four hundred sites

Then the observation that made the previous two look like symptoms: a simulation of this kind will
have *many* configurations, so there should be one centralised object for them.

There already was one — `RunConfig`. What there wasn't was one *declaration*. A knob was written
out four times: the property, `Hash()`, the JSON writer, the JSON reader. At around ninety knobs
that is close to four hundred sites, agreeing with each other only because someone remembered.

Memory had already lost, twice, in a week:

| Knob | Property | Hash | Writer | Reader |
|---|---|---|---|---|
| `DevelopmentLimits.MaxPartVolume` (0011) | ✅ | ❌ | ❌ | ❌ |
| `RunConfig.Light` | ✅ | ❌ | ❌ | ❌ |

So: `[Tunable]`, and a walk. The mechanism is in [DESIGN §5A.10](../DESIGN.md#every-knob-that-moves-energy)
and the reasoning in [D027](../DECISIONS.md#d027); what belongs here is what it found.

## What the walk found the moment it ran

**A third escapee, of a kind neither previous fix would have caught.**
`RandomGenomeOptions.JointTypes` is a `JointType[]` — it decides which joint types a random genome
may draw at all, so two runs differing in it are not the same experiment in any sense. It was
settable, in no hash, in no file. It had been in both before the refactor; my rewrite dropped it,
because the new schema handled `float`, `int`, `bool` and `string[]`, and an enum array is none of
those.

Worth being precise about the shape of that mistake. **The first draft of the coverage test looked
for exactly the four types the code handled.** So the test and the bug were the same oversight
wearing two hats, and the test passed. It now fails on *any* settable property carrying none of
the three attributes, and the attributes are how a property says what it is instead. Enum arrays
are handled by shape rather than by name, so the next one is carried without anyone remembering.

**And a dead knob.** `RunConfig.CellTypeMutationChance` and `MutationRates.CellTypeChance` were two
knobs for one thing, differing tenfold, and `Mutator` read only the second. So setting the first
moved the `configHash` and changed nothing about the run.

This is the third time on this project that a parameter did not reach the thing it configured
([0007](0007-the-creature-that-was-paid-to-jam.md),
[0008](0008-the-energy-audit.md)), and the first time it was found by a
machine rather than by someone noticing that two numbers were suspiciously identical. The other
two took a day each.

## The measurement

`config.json` is now 165 lines and ninety-odd knobs in eight sorted sections, all derived. Full
Core suite: 257 tests, 33 s, and the whole of the above is guarded by four of them.

## What I would say the lesson is

The previous entry ended with *after every change, check the thing the change was about*. This one
is narrower and less comfortable.

**A guard written by the same person who wrote the bug tends to have the bug's shape.** The
sub-config test missed a whole object (0011). The coverage test missed a whole type. Both times
the guard enumerated what its author had thought of, and both times what its author had thought of
was exactly the set that was already correct.

The version that works inverts the burden: don't ask *is each thing I can think of covered*, ask
*is there anything here that has not said what it is*. The first question is answered by the same
imagination that wrote the code. The second is answered by the compiler.
