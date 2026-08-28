# 0029 — The floor kept putting the muscles back

**2026-08-28.** The jointed share was never only a measure of selection. It was partly a
readout of how often the population floor respawned, and in a world that spends its life at
the floor it was mostly that.

## What it looked like

`sink-mid` — 200 W/m², tissue denser than water — kept producing what looked like a muscle
result. Five separate times the jointed share fell below 5% and then climbed back over 30%,
and once it reached **59% at generation 28**, long after any founder cohort should have been
alive. That is exactly the shape a real muscle lineage would make, and it is the thing this
project has been trying to produce since [0017](0017-what-a-muscle-costs-to-own.md).

It was not one. The tell is in the births column:

| t | alive | jointed | jointed % | Δbirths | gen min | work J/s |
|---|---|---|---|---|---|---|
| 26,200 | 42 | 1 | 2.4% | **0** | 7 | 0.00 |
| 26,300 | 40 | 8 | 20.0% | 2 | **0** | 1.43 |
| 26,500 | 80 | 46 | 57.5% | 38 | 0 | 6.77 |
| 26,700 | 359 | 208 | 57.9% | 198 | 0 | 2.01 |
| 26,900 | 907 | 479 | 52.8% | 231 | 0 | 8.93 |

At t=26,200 the world is dead: forty-two creatures and **not one birth**. At t=26,300 the
population touches `MinimumPopulation` (40), the floor trickles founders in, and `gen min`
drops from 7 to **0**. Everything after that is founders and their children. Founders draw a
joint about two times in five (`RandomGenomeOptions.FounderCellTypes`), so a
founder-descended boom is ~50% jointed **by construction**, and `gen min` stays 0 all the way
up because the founders are still alive being counted.

All five "recoveries" begin at `alive` = 40, 40, 41, 42, 49. Every one of them is the floor.

## Why the existing guard did not catch it

`FloorSpawnsPerStep` = 2 exists precisely because of this class of problem, and its own
remark says so: a cohort spawned together "tends to die together, which manufactures a
boom-and-bust oscillation that is an artefact of the refill rule rather than anything the
world is doing." The trickle does prevent synchronous *death*. It does nothing about
morphology statistics, because two founders per step still arrive at the founder joint rate,
and a world pinned at the floor receives them continuously — `sink-mid` returns `gen min = 0`
at t = 8,100, 12,100, 16,100, 20,100 and 24,100.

So the guard solved the oscillation it was written for and left the measurement bias
untouched. Worth stating plainly: **every jointed-share number this project has recorded from
a bottlenecked world is contaminated**, and the ones from healthy populations are only clean
because the floor was idle, which was luck rather than design.

## The fix, which already existed for the other cell type

[0025](0025-something-ate-something.md) hit the identical ambiguity for feeding and solved it
by counting creatures whose *parent* was also absorptive — `EverAbsorptive` /
`absorptiveInherited`. The comment there says why: "food income is 0% has two completely
different causes and the share cannot tell them apart." Joint share has the same two causes —
*joints keep arriving* versus *joints are being kept* — and had no such column.

It does now. `EverJointed` / `jointedInherited`, same construction, in the JSONL and as a
`jnt inh` column in the report. First run out of the box:

| t | jointed | jnt inh | jointed % |
|---|---|---|---|
| 100 | 8 | **0** | 20% |
| 200 | 5 | **0** | 12.5% |
| 300 | 4 | **0** | 9.8% |
| 400 | 2 | **0** | 5% |

Eight jointed founders, **not one of them ever passed a joint to a surviving child**, share
decaying to 5% in 400 seconds. The artefact and the signal, separated in one column.

## What this changes

Nothing about the economics — [0027](0027-the-prize-was-smaller-than-the-entry-fee.md)'s
finding stands on arithmetic, not on this statistic, and the five decays are if anything a
cleaner demonstration than before: a ~45% jointed cohort was stripped to under 5% by selection
five independent times in one run.

What it changes is what counts as evidence. **A jointed share is not evidence of a muscle
lineage; a non-zero `jnt inh` is.** The prediction the arm is now running against is explicit:
if joints are still uncompetitive, `sink-mid`'s current 53% falls back below 5% within about
2,000 s, as it did the previous five times.

⚠ Two arms (`g-c0.5-s3`, `g-c1.0-s4`) are on workers synced before `stats.jsonl` was wired and
write a zero-byte file. Their markdown reports are complete at 100 s resolution, so nothing is
lost, but they must be parsed from the table rather than the JSONL — and this is the second
time today a stale worker has quietly changed what a run records.
