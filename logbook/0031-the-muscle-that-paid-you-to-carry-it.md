# 0031 — The muscle that paid you to carry it

**2026-08-28**

The knob added this morning to make a muscle worth owning had, at low capacity, made it
worth more than not having one. And the number that hid it was a test constant.

## The number that was hiding it

[D043](../DECISIONS.md#d043) said the cost side was structurally closed, and quoted a
ceiling: even at `linkPhoto = 1.0` a jointed creature reaches only **88% of a plant**. I had
been repeating that all day as a structural fact.

It came from `MuscleThatEarnsIsWhatMakesATwoPartFlagellateSolvent`, which builds its jointed
creature like this:

```csharp
Genome jointed = TwoPart(CellTypeIds.Link, 20f, JointType.Hinge);
```

`20f` is `MaxLinkPower`, the most expensive joint a founder can draw.

Power is evolvable per node in `MorphNode.Power`, down to the floor `MinLinkPower`.

`LinkCell` bills in proportion to it, so 88% is the **worst case** rather than the ceiling,
and I had been quoting a worst case as a limit. A lineage that lowers its capacity is
walking down a curve that number says nothing about.

So the curve wants measuring, in `TheCeilingOnAnEarningMuscleIsSetByCapacity`:

| capacity | linkPhoto 0 | linkPhoto 1 | share of a plant |
|---|---|---|---|
| **5 N·m** | 0.0044 W | 1.9954 W | **103.7%** |
| 10 | −0.0956 W | 1.8954 W | 98.5% |
| 20 | −0.2956 W | 1.6954 W | 88.1% |
| 60 | −1.0956 W | 0.8954 W | 46.5% |
| 120 | −2.2956 W | −0.3046 W | −15.8% |

## Over the line

103.7% at 5 N·m: a joint was **paying you to carry it**.

The mechanism is an upkeep asymmetry the knob never accounted for.
`PhotosyntheticEfficiency` buys a link a photosynthetic cell's income, while a link's own
upkeep is 2.5 W/m³ and green tissue's is 3. So at full efficiency a link took a plant's
income for half a watt per cubic metre less, and the joint came free on top.

D043 set out to price a trade-off and had abolished it. That is the specific failure §5A
exists to avoid. A muscle that spreads because it is free tells you nothing about whether
muscles are worth having. It would have looked just like the result this project has been
chasing since [0017](0017-what-a-muscle-costs-to-own.md).

Three arms had been running with it for twenty-five minutes.

## The fix

`LinkCell.Upkeep` now adds a surcharge, proportional to its share of
`PhotosyntheticCell.DefaultEfficiency`, that brings its rate to green tissue's at full
efficiency.

It is derived from `PhotosyntheticCell` rather than taken as a parameter, because it is not
an independent choice: it is whatever green tissue costs. A second copy is how
[0030](0030-the-mutation-that-never-got-the-memo.md)'s two ceilings drifted apart six hours
earlier.

| capacity | linkPhoto 1, before | after |
|---|---|---|
| 5 N·m | 103.7% | **94.8%** |
| 20 | 88.1% | 79.2% |
| 120 | −15.8% | −24.7% |

Monotone in capacity, and never at parity. The test pins both ends. The curve **must** move
with capacity, or the charge D032 relies on is not doing its job. And it must **never**
reach parity, or a joint drifts neutrally instead of being selected.

## What the number turns out to be

An earning muscle at minimum capacity sits **5.2% below a plant**, and that 5.2% is the idle
charge on 5 N·m. It is not 12%, and it is not a wall.

That is a different claim from the one D043 made, and a much more interesting one. The claim
is not *no muscle can ever compete*; it is *a muscle competes if it is cheap, earns, and is
worth the last five per cent*. Whether anything in this world is worth five per cent is an
empirical question, and that is the first time it has been one.

What survives from D043 untouched is the half that was never about cost. A muscle still has
to **buy** something, and under [D037](../DECISIONS.md#d037) depth is still the only thing it
can buy.

## The part worth keeping

Two errors, found within an hour of each other, with the same shape. Both are constants that
drifted from the thing they were supposed to track: 120 against `MaxLinkPower`'s 20 in
[0030](0030-the-mutation-that-never-got-the-memo.md), and 2.5 against green tissue's 3 here.
Both were invisible because the code around each was internally consistent, and both were
found by **recomputing a number I already believed** rather than by looking for a bug.

The three arms were stopped, their output deleted, and the workers re-synced before
relaunching. Twenty-five minutes is cheaper than ten hours of a false positive that would
have confirmed just what I was hoping to see.
