# Maintenance before growth: a runway for growing bodies

*2026-10-26, the speed agent, for the coordinator and the owner. A design, not a build: nothing here runs until the owner
rules on it, and G2 (`logbook/specs/r62p2-prereg/predictions.md`) stays parked until then. The case for it is the
newborn ledger (`logbook/specs/r62p-read/newborn/`). Numbers from `r62p-t3-6201` are marked measured or derived; the
rest is inference and says so.*

## The rule

Today `World.GrowOne` invests everything in the reserve above `GrowthReserveFloor` (0.1) of the body's current tissue,
every metabolic step. The rule adds a second floor: a body below its adult size keeps at least `N` seconds of its own
standing cost before it grows.

```
keep   = GrowthReserveFloor × tissueNow
if N > 0:  keep = max(keep, N × StandingWatts)
target = min(Energy − keep, adultTissue − tissueNow)
```

`StandingWatts` is the body's cached cost at its current size and age, the figure the module rule and the reserve
margin already read. `N` is a new tunable, `RunConfig.GrowthRunwaySeconds`, in the growth group, unit s, env
`EVOSIM_GROWTH_RUNWAY`. It must be finite and not negative, and it defaults to 0. At 0 the branch is never entered,
so the arithmetic is today's, operation for operation, and every world that does not name it runs as it does today.
The new tunable still changes every config hash, and the build has to re-record its fixtures the way the pace build
did (`src/Evosim.Core/CLAUDE.md`).

It touches juveniles alone: an adult has nothing left to grow, so `GrowOne` returns before it reads the floor. It is
counted in world seconds, so the pace factor does not change it (`logbook/specs/scaling-audit.md`). It costs one
comparison per juvenile per step, and nothing on the card.

## What it does to a newborn grazer

These are `r62p-t3`'s numbers. A bred grazer is born at a body fraction of 0.65 (measured), holding 36.7 J of
tissue and 36.7 J of reserve. Its standing cost is about 0.6 W, and its adult's tissue is about 56 J at 0.908 W
(derived and measured, `newborn/README.md`).

| N | Invested on the first step | Body after it | Reserve after it | Runway |
|---|---|---|---|---|
| 0 (today) | 19.3 J | adult | 16.6 J | 18 s at 0.9 W |
| 30 | 18.7 J | about 0.99 | 18 J | about 20 s |
| 45 | 9.7 J | about 0.83 | 27 J | about 36 s |
| 60 | 0.7 J | about 0.66 | 36 J | about 60 s at 0.6 W |

Every row is derived. Below about 29 s the rule does nothing for this world's newborns, because full growth already
leaves more than that. At 60 s a newborn stays near its birth size and holds a minute of its own upkeep. After that
it grows out of what it eats. A mouth blow keeps 38.5 J on average over the world (derived), so one blow takes it to
adult with about a minute of an adult's upkeep left over.

The ledger's slowest group helps show why 60 s could matter. Those that never ate had a straight-line time to the
nearest leaf of 26 s at their median speed, against today's runway of 19 s (derived). At 60 s that median newborn
has time to arrive. Whether it swims there is the screen's question, and a new one comes with it. Today's newborn
swims at its adult size by the first frame, 10 s in, because the harness applies growth every `GrowthStepSeconds`.
Under the rule it swims small, with limbs about 0.87 of an adult's length and its drive's power unscaled
(`Phenotype.Scaled`). Whether a small body swims faster or slower than an adult is not measured.

## What it does to plants

Leaves are born much smaller than grazers: a bred leaf's body fraction at birth has a median of 0.19 (measured, 65,027
births). So a leaf spends far longer as a juvenile, growing out of light. Under the rule it holds back `N` seconds of
its own small upkeep before it grows, a few joules, and that amount rises with the body. A leaf that already gains
from light reaches the runway within about `N` seconds and then grows at today's pace (inference: income above the
runway is invested as it is today). Its growth is delayed by about `N` seconds once, slightly more because a smaller
leaf intercepts less light. Against a median leaf life of 1,717 s (measured, bred leaves), 60 s is small.

What it buys a leaf is a margin while it is young. Today a juvenile sits at a tenth of its tissue, 12 to 17 s of
upkeep (the floor's own remark in `RunConfig.cs`). Under the rule it holds `N` seconds, so a passing shadow or a quiet
patch no longer kills it. In t3, 12,645 of 56,673 bred leaves starved, and only 151 of them in their first minute
(measured). The lineage does not say how many were still juveniles when they starved, so the effect on leaf
starvation is unmeasured until a screen counts it.

There are two side effects, both inference:
- A juvenile leaf holds a few more joules as reserve and fewer as tissue. A grazer that bites eats parts, so a little
  less of a young leaf is food.
- 7,203 bred leaves had an indeterminate node (measured), so the module rule applies to them. It drops a module from a
  body that stays under `ModuleDropReserveSeconds` (50 s) of its upkeep. Today growth holds every juvenile at 12 to
  17 s, under that line. At `N` ≥ 50 it no longer does. The add line, 300 s, is above any `N` proposed here, so module
  addition is unchanged.

## What it does to the founder endowment

Nothing, at the values proposed (derived). A founder's start is `FounderEnergyJoules × birthFraction` plus 600 s of
its newborn upkeep, capped by D124 at 0.9 of its gate plus what its growth costs (`World.FounderStartCap`). A capped
founder holds 0.9 of its gate once grown. That gate is at least the breeding price, about 160 s of an adult's upkeep
for t3's grazers, so the founder holds about 147 s or more. That is above any `N` up to about 140 s, so the founder
still grows at once.

An uncapped grazer founder starts with about 490 J and needs 19 J to grow. Leaf founders start small (body fraction
0.14, about 61 J of start) and already grow out of light, so they hold back a few joules, as bred leaves do. The cap's
growth term stays exact, because growth's debits still add up to the adult's tissue less the newborn's, only spread
over more steps. An inoculant keeps its named purse; one whose purse is under `N` seconds of its upkeep stays at birth
size until it earns.

## Breeding at juvenile size

Growth runs before breeding, so today a juvenile's reserve never reaches its gate. Under the rule a juvenile holds up
to `N × StandingWatts`. It can breed while small only if that clears its own gate, which needs `N` above the price over
its upkeep plus its margin gene. For t3's grazers that is about 160 s plus a margin of about 590 s (derived from the
684 J gate). So at `N` = 60 nothing changes. A lineage that evolves its margin toward 0 could breed small once `N` is
above about 160 s. That is real biology, maturing before full size, and it is also a new behaviour. An `N` at or under
120 s keeps it out.

## Recommendation and tests

My recommendation is `N` = 60 s for the screen. That is the smallest value that gives a newborn grazer a minute of
runway, and it changes nothing for founders or for breeding (derived). The world is the owner's to rule on.

The build goes on a branch from `pace` after G1 ends. Sonnet builds it and the speed agent reviews. Its tests:
- With `N` at 0, a recorded fixture replays identical.
- A juvenile whose reserve is under `N × StandingWatts` does not grow.
- One above it invests exactly `Energy − N × StandingWatts`, up to what is left of the adult.
- The tissue floor still binds when it is the larger of the two.
- A negative, infinite or NaN `N` is refused.
- `EnvBinding` carries `EVOSIM_GROWTH_RUNWAY`.

Two reflection tests cover the hash and the save-and-reload. Two remarks change: `Organism.ReproductionThreshold`'s
"down to `GrowthReserveFloor`" and `World.FounderStartCap`'s note on the floor. They change to say the larger of the
floor and the runway.
