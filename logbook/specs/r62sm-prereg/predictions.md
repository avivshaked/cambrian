# The structural mutation screen: how often a child's body plan should change

*Pre-registration, DRAFT of 2026-10-27, the speed agent. The owner approved, on 2026-10-27, screening the structural
mutation rate on the card before choosing a lower rate or fitting the brain to the new body at birth. The mutation
rates are the owner's, and this screen only measures them. G's start is filled in from growth group 2's read
(`logbook/specs/r62gr2-read/24000/`) before the commit that launches it. The build, the identity sitting and the
launcher come after it. Every number below that is not a measurement is a guess or derived, and says so.*

## What it asks

The newborn ledger's genome diff (`logbook/specs/r62p-read/newborn/README.md`) found the slow newborns in `r62p-t3`
to be the structural mutants. 2,003 bred grazers had a measured first speed, and 561 of them swam under 0.03 m a
second. 66 % of the slow ones had a changed body plan, against 44 % of the moving ones (measured). The reading was
that a child whose body plan mutated carries a brain fitted to its parent's body, so it barely swims (inference).

1. Does a lower structural mutation rate cut the share of slow newborns, and by how much?
2. Does it cost the lines anything: D222's tests, the depth of the chains, or the variety of body plans?
3. Is the rate the lever, or must the brain be fitted to the new body at birth instead?

## The bound on the answer

This is derived from the genome diff's table, and it assumes that `r62p-t3`'s split carries over to the screen's
world (inference). Among the newborns whose structure did not change, about 191 of 999 were slow, a share of 0.19.
Among those whose structure changed, about 370 of 1,004 were, a share of 0.37. So the slow share is about 0.19 plus
0.18 times the share of births whose structure changes.

| Structural rate, against today's | Births with a structural change (derived) | Slow newborns (derived) |
|---|---|---|
| 1 (today) | 0.48 | 0.28 |
| 0.5 | 0.28 | 0.24 |
| 0.25 | 0.15 | 0.22 |
| 0.1 | 0.06 | 0.20 |
| 0 | 0 | 0.19 |

The change share treats the structural operators as one Poisson draw a birth, at today's 0.48 (measured: 1,004 of
2,003). **So the rate can take at most about a third of the slow newborns away.** The other 0.19 are slow with their
parent's body plan, and no rate reaches them (derived). If the screen confirms this, the rate is a small lever, and
fitting the brain at birth is the larger one.

## The worlds

Four worlds on seed 6201, 24,000 world seconds each, in one card group. The base is the screen's winning settings:
- `r62p-t16`'s world (`rounds/env-r62p-t16.ps1`: the limb push at 4.8, the work price at 0.03125, the newborn reserve
  at 0.5, the kill gut);
- growth as a climb in T = 60 s with the juvenile-strength switch off (group 1's winner, `logbook/specs/r62gr-read/24000/`);
- G's start at the value growth group 2's rule picks, in [1, 300] s, mutating at 0.08 a birth.

The arms scale every structural mutation chance by one factor S:

| World | S | The chances it sets |
|---|---|---|
| `r62sm-s100-6201` | 1 | today's |
| `r62sm-s50-6201` | 0.5 | half of today's |
| `r62sm-s25-6201` | 0.25 | a quarter |
| `r62sm-s10-6201` | 0.1 | a tenth |

**The structural chances are the ones `scripts/reads/genome-diff-read.py` counts under "structure".** By
`MutationRates` today, the candidates are:

| Chance | Today's value |
|---|---|
| `AddNodeChance` | 0.04 |
| `AddEdgeChance` | 0.06 |
| `RemoveEdgeChance` | 0.05 |
| `AddNeuronChance` | 0.05 |
| `RemoveNeuronChance` | 0.04 |
| `RewireInputChance` | 0.05 |
| `RecursiveLimitChance` | 0.03 |

The build names the exact list from the mutator's code, with each operator's line, and it includes node removal if
another path does it. The brain's weights, the scalars, the drive, the limits and the whole-creature dials keep
today's rates in every arm. So the screen moves the body plan's rate alone. Today's arm is a new world, since no
recorded world has t16 and the climb together.

## The build

It is `scratch/wt-growth`'s branch, which already binds the pace factor, the limb push and the work price. t16 is
settings there, so no pace-and-growth integration is needed. One addition, in `Evosim.Farm` only:
- an environment binding for each structural chance, on `EnvBinding.cs`'s pattern for `EVOSIM_MARGIN_CHANCE`, with
  today's value as its default;
- a test that at S = 0, no child differs from its parent in the genome diff's structure category over a few thousand
  births;
- a test that the bindings unset leave every recorded config hash unchanged.

Core does not change, because the chances are already `RunConfig.Mutation`'s hashed fields. The farm's hash does
change, so an identity sitting runs before the launch. It replays `r62gr-t60-6201` with the bindings unset on the new
build, to 3,000 s on the card, against growth group 1's own run, using `rounds/identity-growth-on.ps1`'s comparison.
A Sonnet agent builds it and runs the filtered tests, and the speed agent reviews it.

## The tests and the deciding numbers

D222's three tests per world at 24,000 s, by `scripts/reads/screen-read-24000.ps1 -Gene`, as the growth groups'.

**The primary readout** is the share of bred grazers that are slow newborns, under 0.03 m a second on the ledger's
first frame pair. It is split by whether the child's structure changed, by `scripts/reads/genome-diff-read.py`, as
in the newborn ledger, with the same exclusions.

Beside it:
- the share of bred grazers that never ate, and their median age at death;
- the deepest mouthed chain;
- the variety of body plans: the quartiles of the bred grazers' part counts (`pt` on the birth rows) by 3,000 s bin;
- the share of births whose structure changed, against the table above.

**The rate's rule.** This rule is fixed now. The chosen S is the highest one that passes D222, and whose slow share is
at least 0.04 below today's arm. The highest is chosen because a lower rate costs the archive its body-plan
innovation, which D004's MAP-Elites cells exist to protect. If no arm meets both, today's rate stands, and the
structural rate is recorded as not the lever. The next screen is then fitting the brain at birth. Either way the
result goes to the owner as a proposal, since the rates are theirs.

## Predictions

These are guesses unless marked derived.

- **The change share** follows the table to within 0.05 in every arm (derived, if the operators act as one Poisson
  draw).
- **The slow share** falls with S, but by less than the table: 0.28, 0.25, 0.23, 0.22 (guess). A world with fewer
  body-plan mutants also has fewer parents that are themselves odd, which works the other way (inference).
- **D222.** Today's arm passes, as `r62p-t16` did. All four pass.
- **The rate's rule** picks S = 0.25 (guess).
- **Variety.** The part-count quartiles of the S = 0.1 arm sit within one part of today's at 24,000 s. 24,000 s is too
  short for the rate to narrow the body plans that far (guess).
- **The bound.** No arm's slow share is under 0.17.

## The machine

The group runs on logical 22 to 31 at 10 threads, with the theatre on 16 to 21. The world is `r62p-t16`'s, with the
climb. `r62p-t16` reached 24,000 s at 1.1 to 1.3 times real time late, beside one other world (measured). The growth
groups' four worlds ran 1.1 to 1.5 times late (measured). So I expect about 1.0 to 1.3 times late and 4 to 5 hours in
all (guess). The identity sitting takes about 30 minutes on the card, and the build about an hour of agent time.

The exits are the growth groups'. A world under 0.7 times real time for an hour makes the theatre give way. After
12,000 s, a world under 0.6 times real time for an hour is stopped with `scripts/stop-arm.ps1` and read at its stop
second. The pace screen's stopping rule is read at 12,000 s. A t16 world may run below 0.7 times late, so it is the
likeliest of the screens to need the theatre to give way (inference).
