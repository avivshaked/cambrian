# 0030 — The mutation that never got the memo

**2026-08-28.** The one code path by which a creature can invent a muscle was charging five
times the going rate, because it was still using a ceiling the design retired three weeks ago.

## How it surfaced

Not by looking for it. [0029](0029-the-floor-kept-putting-the-muscles-back.md) had just
established that the jointed share was partly a readout of the population floor, and the
question that followed was whether the *cost* side had a similar hole. The framing I had been
working from — carried in my own summary of the day — was that "the idle capacity charge
doesn't scale away", which is why no knob could make a joint competitive.

That framing was wrong, and reading the code rather than trusting it said so. `MorphNode.Power`
is evolvable per node, `LinkCell` bills in proportion to it, and founders draw it from
`RandomGenomeOptions.MinLinkPower`..`MaxLinkPower` = 5..20 N·m. The charge scales fine. It
scales down to 0.1 W.

So where did the "88% of a plant" ceiling in [D043](../DECISIONS.md#d043) come from? From
`MuscleThatEarnsIsWhatMakesATwoPartFlagellateSolvent`, which builds its jointed creature with
`Power = 20f` — the maximum. A worst-case number I had been quoting as a structural one.

Pulling that thread reached `Mutator.ChangeJointType`:

```csharp
if (dof > 0 && node.Power <= 0f) node.Power = rng.Range(5f, 120f);
```

120 N·m. `GenomeFactory`'s own remark, forty lines away in another file, says why that is
wrong: *"The upper bound was 120 N·m until logbook/0017 measured what that costs to own."*
[D032](../DECISIONS.md#d032) lowered the founder path and left this one alone.

## Why it is worse than a stale constant

The guard `node.Power <= 0f` means the node had no joint. This branch is not a general capacity
draw — **it is the only way a lineage with no muscle acquires one.** Founders are unaffected,
and so is every descendant that inherits a founder's joint and perturbs it. The creatures this
overcharged were precisely the de-novo muscle inventions, which is the event
[D042](../DECISIONS.md#d042) onwards has been trying to observe.

| | draw | mean | idle per DOF | of a leaf's ~2.3 W |
|---|---|---|---|---|
| founder | 5–20 | 12.5 N·m | 0.25 W | 11% |
| mutant | **5–120** | **62.5 N·m** | **1.25 W** | **54%** |

1.25 W is within a percent of the 1.24 W median link that [0017](0017-what-a-muscle-costs-to-own.md)
measured nothing surviving at any irradiance from 64 to 400 W/m². `ChangeJointType` picks
uniformly from seven joint types, so a sixth of new joints are spherical — three degrees of
freedom, **3.75 W standing before moving once.**

And [D044](../DECISIONS.md#d044)'s thrust survey, run last week, measures 120 N·m at 0.052 m/s,
the fastest swimming in the table. That is what a mutation-born muscle was buying. It swam well
and went bankrupt doing it.

## And the same constant a third time

Written up, checked, and then `EvolutionRun` turned out to have its own copy:

```csharp
float maxPower = Env("EVOSIM_MAXPOWER", 120f);   // -> config.Genome.MaxLinkPower
```

The runner overrides the design default with the retired ceiling unless an arm names the knob.
Which flips the impact around in a way worth stating plainly, because my first draft of this
entry had it wrong: in a run that left `EVOSIM_MAXPOWER` alone, founders drew 5–120 as well, and
the mutator's hardcode agreed with everything around it and did nothing.

It bit only where a run **set the ceiling to 20** — `sink-mid`, `sink-slow`, `sink-still`,
`linkearn`, every arm launched to test whether a cheap joint could survive. Those founders drew
5–20 and their lineages' de-novo muscles drew 5–120.

**So the fault landed precisely on the experiments designed to detect it.** An arm that lowered
the ceiling to make joints affordable got affordable founders and unaffordable inventions, and
reported that cheap joints do not establish.

Both `EVOSIM_MAXPOWER` and `EVOSIM_MINPOWER` now default from `RandomGenomeOptions.Default`.

## The fix and its guard

`Mutate` now takes `RandomGenomeOptions` and the draw uses `MinLinkPower`..`MaxLinkPower`; one
source of truth rather than a second copy, since two constants obliged to agree is the
arrangement that produced this. `World` forwards `Config.Genome`.

Two tests, and the second exists because of a warning this project has already earned twice —
*prove a parameter reached the thing it configures*. `AMutationBornJointIsNoStrongerThanAFounderIsAllowedToBe`
bounds the draw; `TheRunsConfiguredPowerCeilingReachesTheMutator` sets a ceiling of 2 N·m and a
floor of 90 and asserts the outputs actually move, which a test asserting only "within bounds"
would pass while the option went nowhere.

Both failed first, for a reason worth keeping: they sampled every jointed node, including
inherited ones, and `Perturb` is *relative* — sigma = 0.15 × value, so one step carries a 20 N·m
joint past 24. The tests now start from a jointless parent and zero `ScalarChance`, so what is
measured is the draw rather than the draw plus a random walk. **A bound assertion on a perturbed
value cannot distinguish the bug from the noise**, and the first version of the test would have
gone green on a 30 N·m ceiling.

`Mutator.CodeVersion` → 2. Stored seeds no longer replay to the same offspring.

⚠ Bumping it revealed that **nothing reads it.** The class remarks say it is "recorded per
birth", and it is not recorded anywhere — `lineage.jsonl` is the file that would carry it and is
deliberately still unwritten (one row per creature ever born, at ~40,000 births an hour, for an
ancestry nothing yet reads). So the mechanism that makes a replay mismatch detectable is a
constant with no consumers, and the doc asserts a thing that is not true today. Left alone rather
than fixed, because the fix is `lineage.jsonl` and that is a storage decision, not a bug — but the
sentence should not be read as describing a working guard.

## What it does not fix

Not the joint-share decay in any run so far: those populations are dominated by founders and
their children, who were priced at whatever ceiling the run configured, and D043's arithmetic is
untouched. The honest prediction is narrow —
**de-novo joint lineages should appear more often, not win more often.** `jointedInherited`, added
this morning in 0029, is the column that will say whether they do, and no arm currently running
has it.

⚠ All five live arms predate both this and 0029. Nothing running right now can answer the
question either change was made to answer.
