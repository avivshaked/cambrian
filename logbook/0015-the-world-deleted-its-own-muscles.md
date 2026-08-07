# 0015 — The world deleted its own muscles

**2026-08-07**  ·  Milestone 4

Three greps, before any code was written:

- `new World(...)` appeared in five files, **all of them tests**.
- `workJoules:` was passed `0f` at **every call site in the repository**.
- `Organism.HeightY` was written once at birth and never again.

So there were two simulators and they had never met. Physics could swim a creature and measure the
work it did. The economy could feed, bill and kill one. Nothing carried a number between them.

That is not a defect — [DESIGN §10](../DESIGN.md#10-milestones) puts the join at Milestone 4 and
the project was at 2/3. But it does narrow what had been established. **The 32 W/m² transition, the
biomass cap and the food-web audit are all true of a world of stationary organisms for whom motion
is free**, and those numbers had been accumulating authority for two weeks.

## The join

One method, `World.Observe(creature, heightY, workJoules)`, and it points one way: §6.1 forbids
`UnityEngine` in `Evosim.Core`, so the world cannot ask where anything is. `Evosim.Sim.Ecosystem`
reads the articulations and pushes both numbers in, then reads back who was born and who died and
makes the scene match.

Work is *accumulated* rather than assigned, because the clocks differ — physics at 100 Hz, the
economy at 2 Hz — and drained exactly once, in `Metabolise`. A term charged twice destroys energy;
a term never drained bills a creature forever for one stroke. Both were easier to get wrong than to
notice, so both have a test.

It worked on the first run. The audit held at **0.0000%** with the work term live, at 74× real
time.

## What the run said

| t (s) | alive | mean speed m/s | work J/step | with joints | mean DOF | mean depth m |
|---|---|---|---|---|---|---|
| 20 | 40 | 0.0003 | 38.68 | 10% | 0.23 | −9.98 |
| 40 | 44 | 0.0001 | 2.62 | 2.3% | 0.05 | −9.73 |
| 60 | 45 | 0 | 0 | **0%** | 0 | −9.4 |
| 200 | 183 | 0 | 0 | **0%** | 0 | −5.91 |

**Every creature with a moving part was dead within sixty simulated seconds**, and nothing with one
was ever born again. The population then grew from 45 to 183 without a single joule of mechanical
work.

The "with joints" column is why that sentence can be written at all. Work falling to zero has two
completely different causes — creatures that still have joints and have evolved not to use them,
which is behaviour, or a population with no joints left, which is anatomy — and they need different
fixes. It is anatomy.

## One column that worked

Mean depth rose from −9.98 m to −5.91 m while mean speed was **zero**. Nothing swam upward; the
shallow ones simply out-bred the deep ones, and the population's centre of mass moved through
differential survival alone.

That is natural selection acting on a spatial trait, in a world where the trait cannot be changed
within a lifetime, and it is the first thing this project has produced that neither half could have
produced on its own. Light income measures 51 J at 0.5 m against 1.9 J at 40 m — a 27× gradient —
so there was plenty to select on.

## Why the muscles went

Not a bug in the join. The join is correct, and the extermination is the correct answer to a badly
posed question.

**There is no brain evaluator.** The genome carries `NeuronDef`, `NeuronInput` and per-node
oscillator frequencies; development places them in the phenotype; nothing reads them. Every
creature in the world is driven by `DriveTestSine` — *one shared sine, on every degree of freedom,
identical for every creature regardless of its genome*:

```csharp
scratch[i] = Mathf.Sin(2f * Mathf.PI * hz * time + i * 0.7f);
```

So the controller is a constant across the population. Morphology is the only thing that can vary,
and a uniform sine on random joints produces a symmetric flap with no net thrust: **mean speed
0.0003 m/s while spending 38 J per step.** The light gradient overhead is worth 27×, and entirely
unreachable, because nothing can steer toward it.

A cost that is real, against a benefit that cannot be obtained. Selection deleted the cost. It was
right to.

## This was predicted, and the prediction was better than I read it

The Milestone 1 smoke test already said it, in its own output:

> Under §5A this is charged as metabolic cost, which is defensible — a real muscle slamming a joint
> does spend the energy — but it means the cost is presently dominated by bang-bang actuation
> rather than by swimming … judging it fairly needs the brain graph (Milestone 6), since an
> open-loop sine has no way to decelerate before a stop.

It measured the mechanism too: widening joint ranges 4× drops the share of energy destroyed at the
stops from 74.5% to 14.4%. What it could not say was what the *population* would do about it,
because at Milestone 1 there was no population. The answer turns out to be: remove the joints, in
under a minute.

## What it means for the order of the work

DESIGN §10 has Milestone 4 (world) before Milestone 6 (perception and the brain graph). The join
belongs at 4 and it is done. **Billing mechanical work does not belong there.** It only becomes a
meaningful pressure once a creature's own genome decides how it moves, because until then work is a
tax on having a body part rather than a price for using one.

`WorkCostMultiplier` is left at 1 rather than quietly set to 0, because a default chosen to make an
uncomfortable result go away is how a finding gets buried. The finding is that at 1, with no
controller, the world becomes plants.

## The pattern

0013 was a guard shaped like the bug it guarded. 0014 was an estimate wrong enough to change the
plan. This one is smaller and stranger: **the codebase had already written down the answer**, in
the smoke test's own commentary, a week before there was a population to demonstrate it on. It was
read as a caveat about one measurement. It was a prediction about the whole world.

The three greps at the top took two minutes and were worth more than anything else done today.
