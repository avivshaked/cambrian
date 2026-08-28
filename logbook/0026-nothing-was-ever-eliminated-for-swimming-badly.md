# 0026 — Nothing was ever eliminated for swimming badly

**2026-08-28**  ·  Milestone 4

The oldest open failure in this project, and the one every entry since
[0018](0018-the-founders-can-swim.md) has closed by listing it as still broken: **joints go to
zero.** Every run, every seed, every irradiance from 64 to 400 W/m². D031 and D032 swept actuator
cost and found nothing alive with a joint at any setting, which established that it was not a knob
and left it at that.

It is arithmetic, it was always arithmetic, and the sweeps were run entirely on the wrong side of
the line.

## Three failures, and only one of them is the one

A joint could fail three ways, and they want different fixes:

- **Unreachable** — mutation cannot produce a jointed creature.
- **Useless** — joints work and buy nothing.
- **Unaffordable** — a joint costs more than a creature can pay, so it dies before its benefit is
  ever tested.

The first is ruled out by the run tables, which were always there: **20% of founders have joints**
at t=250, decaying to 2% by t=3,000. Reachability was never the problem, and the decay is what
wanted explaining.

## A link is charged three times

`LinkCell.Acquire` returns `CellIntake.None` — link tissue earns **nothing**. So the charges are:

| | at 20 N·m, half-extent 0.35 m |
|---|---|
| income the volume forfeits by not being photosynthetic | **1.30 W** |
| link tissue's higher base upkeep (2.5 vs 1.0 W/m³) | **0.51 W** |
| idle capacity charge, 0.02 × power × dof | **0.40 W** |
| **total** | **2.22 W** |

Against a two-part photosynthesiser's entire surplus of **1.92 W**. The joint costs **115% of
everything the creature earns.** None of these three charges depends on the joint moving; a
creature that never actuates pays all of it.

## The measurement that should have been made first

Same body, same volume, same depth — the only difference is whether the second part is
photosynthetic tissue or a hinge:

```
two photosynthetic parts : income 3.9819 − costs 2.0580 = +1.9239 W
one part + an idle hinge : income 1.9909 − costs 2.2865 = −0.2956 W
```

**Insolvent.** Not disadvantaged — insolvent, before actuating once.

And the solvency threshold is **5 N·m**:

| capacity | net | |
|---|---|---|
| 1 N·m | +0.084 W | solvent |
| **5 N·m** | **+0.004 W** | **break-even** |
| 8 N·m | −0.056 W | insolvent |
| 20 N·m | −0.296 W | insolvent |
| 120 N·m | −2.296 W | insolvent |

**`MinLinkPower = 5`. `MaxLinkPower = 20`.** Joint power is drawn uniformly from exactly the range
where a creature cannot afford it, with the *minimum possible draw* sitting precisely at
break-even.

D031 and D032 swept `MaxLinkPower` at 8, 20, 60 and 120 — all above the threshold — while
`MinLinkPower` sat at 5 in every arm and was never touched. **The sweep that concluded "actuator
cost is not the problem" never once tested an affordable joint.**

## Size would rescue it, and nothing selects for size

The fixed charges amortise, so a bigger creature can carry a joint:

| body | jointless | with a 20 N·m hinge |
|---|---|---|
| 1 photosynthetic part | 0.96 W | **−0.30 W** |
| 2 parts | 1.92 W | +0.67 W |
| 3 parts | 2.89 W | +1.63 W |

Three parts and it is comfortably solvent. So the question becomes how big founders are, and 2,000
draws answer it:

| | |
|---|---|
| mean parts | **1.51** |
| genome expressed in the body | **100%** |
| exactly one part | 49.4% |
| carrying a joint | **50.6%** |
| **≥3 parts** | **0%** |

Development loses nothing — this was worth checking and I had assumed otherwise, because
`MinNodes`/`MaxNodes` say 2–5 and that is `GenomeFactory.Random`, not `Founder`. Founders are
deliberately minimal (the human chose that, and it was the right call for other reasons).

**Every jointed founder is exactly two parts: one photosynthetic, one link.** Which the table above
prices at −0.30 W.

## The whole chain

1. Half of all founders carry a joint. Reachability was never the problem.
2. Every one of them is a 2-part creature.
3. A 2-part jointed creature is insolvent at any capacity above 5 N·m.
4. Capacity is drawn from [5, 20], so it is insolvent almost always and at exact break-even at best.
5. Escaping needs a third part *first* — and a 3-part jointless creature earns no more per unit of
   tissue than a 1-part one, so nothing selects for the intermediate.

**Nothing has ever been eliminated for swimming badly.** They die of arithmetic, before behaviour
is tested. The question "are joints useless?" has never been askable, because an affordable joint
has never existed in this world.

## The same mistake, twice, in three days

This is structurally identical to the detritivore failure in
[0024](0024-the-larder-filled-and-nobody-came.md)/[0025](0025-something-ate-something.md): a trade
priced at break-even, where **break-even is not viability**, because §5A.6 pays for offspring out
of surplus. A creature at break-even survives forever and founds nothing.

§5A.6d says exactly this. It was written two days ago, about absorptive cells, and it closes with a
warning:

> ⚠ Any claim in this document of the form "X is not viable because the world only produces Y" is
> suspect until restated as a margin.

The joint failure was sitting in the same document, in the same form, the whole time.

## What is not established

**Whether an affordable joint is any use.** Everything above is arithmetic on a developed body: no
physics, no thrust, no swimming. A 5 N·m hinge on a 0.34 m³ body may produce nothing worth having,
and if so the fix is elsewhere entirely. That is the next measurement and it needs a run, not a
test.

Also unestablished: whether lowering `MinLinkPower` is the right response at all, or whether the
idle rate, the link upkeep, or §5A.1's rule that link tissue cannot earn is the thing that should
move. Four knobs produce the same 2.22 W and only one of them has been examined. **That is a design
decision and not mine to take.**

## The pattern

0024 and 0025 recorded recommendations killed by the measurements run to justify them. This entry
is the opposite failure and a worse one: **a measurement nobody ran for four months.** The
arithmetic here is three multiplications. It needed no physics, no run, no sweep — it would have
fitted in the margin of the page on which D031 was planned, and it invalidates both sweeps that
were run instead.

The reason it went unmeasured is worth naming: "joints go to zero" reads as a *behavioural*
observation, so it attracted behavioural hypotheses — the controller, the drive signal, the
actuator conditioning, the reward gradient. Nobody priced the joint, because pricing it does not
feel like an answer to a question about swimming.
