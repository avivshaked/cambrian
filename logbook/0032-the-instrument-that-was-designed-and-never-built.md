# 0032 — The instrument that was designed and never built

**2026-08-28.** [D021](../DECISIONS.md#d021) anticipated this failure exactly, specified the
report that would catch it, and the report was never written. A day of arms was read as data
without anyone able to tell a living world from a life-supported one.

## What D021 said

> *"A floor is an exogenous intervention in a deliberately endogenous system — the class of thing
> D017 exists to remove — and its failure mode looks like success. If it fires regularly, the world
> is not sustaining life, **we** are, and the run still shows a stable population, births, deaths
> and accumulating lineages. Every figure consistent with a working ecosystem; every one of them
> propped up."*
>
> *"**The success condition is that it fires at t=0 and never again.**"*

And then: *"a floor spawn is its own event type in `lineage.jsonl`."* `lineage.jsonl` is
deliberately unwritten — one row per creature ever born, ~40,000 births an hour, for an ancestry
nothing reads. So the instrument existed as a design and as two public properties on `World`
(`FloorSpawns`, `SecondsSinceFloorFired`) and as nothing a run ever printed.

`gen min` was standing in for it and cannot do the job: **`gen min = 0` cannot distinguish
"founders from t=0 are still alive" from "the floor fires every step".** Those are a healthy young
world and a dead one.

## What it says now

Three fields in `stats.jsonl` — `floorSpawns`, `floorSpawnsWindow`, `secondsSinceFloorFired` — and
a `floor` column in the report, next to `gen min` so the two read together. First run, at the
default 48 W/m²:

| t | alive | floor | gen min |
|---|---|---|---|
| 100 | 40 | 50 | 0 |
| 200 | 40 | **21** | 0 |
| 400 | 40 | 7 | 0 |
| 600 | 40 | 4 | 0 |

107 floor spawns against 6 births, and `secondsSinceFloorFired` of 8.0 at the end. **By D021's own
criterion that world was never alive**, and it is the default configuration.

## The re-audit

Completed runs never recorded the counter, but `alive` pinned at `MinimumPopulation` with
`gen min = 0` is a sound proxy, and `gen min > 0` proves the floor is idle. Across today's arms:

| run | rows with no founders alive | rows pinned at the floor |
|---|---|---|
| `g-c0.5-s1` | 79% | 9% |
| `g-c0.5-s3` | 73% | 17% |
| `g-c1.0-s1` | 70% | 13% |
| `g-c1.0-s2` | 64% | 10% |
| **`sink-mid`** | **21%** | 16% |
| **`sink-slow`** | **3%** | 12% |
| **`sink-still`** | **0%** | 12% |

A clean split, and not the one I would have guessed. **The clearance arms were largely
self-sustaining; the sink arms essentially never were.** `sink-still` did not manage a single
sample with no founder alive.

So [0028](0028-the-canopy-closed-and-the-scavengers-came.md)'s canopy food chain stands, and its
second one — the consumer–resource cycle in `sink-mid` — is weaker than I wrote it. That entry now
carries the correction. The absorptive *lineage* is real (floor spawns have `parentId = -1` and can
never count as inherited, and 163 were inherited at the peak); the oscillation's amplitude and
timing are partly the refill rule.

## The part worth keeping

This is the fourth time in one day that the fault was a thing the project had already got right and
then failed to keep connected: a retired ceiling still hardcoded in the mutator
([0030](0030-the-mutation-that-never-got-the-memo.md)), a runner default overriding the design
default (same), an upkeep rate that did not track the cell it was imitating
([0031](0031-the-muscle-that-paid-you-to-carry-it.md)), and now a report specified in a decision
and never built.

None of these were wrong ideas. All four were **correct decisions that lost their connection to the
code**, and every one of them was invisible because the code around it was internally consistent.
The measurement discipline in `CLAUDE.md` — *prove a parameter reached the thing it configures* —
needs a second half: **prove the thing a decision promised to report is actually reported.**
