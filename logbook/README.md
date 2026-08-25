# Logbook

Dated entries about what actually happened: what was tried, what broke, what surprised us,
and what the numbers were on the day.

This is the fifth document in the set described in [`CLAUDE.md`](../CLAUDE.md). The other
four describe the system as it *currently stands*. This one describes how it got there, and
it is the only one that is allowed to be out of date — because it is history, and history
doesn't drift.

## The rule that keeps this honest

**The logbook is never a source of truth.**

If an entry needs to state a design fact, it links to [`DESIGN.md`](../DESIGN.md) or quotes
it. It does not restate it in friendlier words. The moment an entry starts explaining what
the system *does*, it has become a second specification that nobody will remember to update,
and then neither document can be trusted.

An entry may freely describe what the system did **on the day it was written**, including
things that were true then and are wrong now. That is the point. Superseded entries are
never edited to match the present — a later entry supersedes an earlier one, exactly as in
[`DECISIONS.md`](../DECISIONS.md).

## How this differs from the other documents

| | Holds |
|---|---|
| [`DESIGN.md`](../DESIGN.md) | the specification — what the system is |
| [`DECISIONS.md`](../DECISIONS.md) | conclusions — what we chose, and what we rejected |
| **`logbook/`** | **process — what we did, what happened, what it cost** |

`DECISIONS.md` says *we rejected pooling*. The logbook says *we spent an afternoon
building a benchmark, misread it by a factor of thirty, caught it by cross-referencing two
measurements that disagreed, and only then found out pooling was unnecessary.*

Both are worth keeping. Neither substitutes for the other.

## Format

One file per entry, `NNNN-slug.md`, numbered in the order written. The number is the
chronology; the date goes in the header.

```markdown
# 0007 — Short declarative title

**2026-08-14**  ·  Milestone 2

...prose...
```

Write entries the **day something happens**, not later. The specific details — which run it
was, what the number actually was, what it felt like to be wrong — are perishable within
about a week, and they are the entire value of the thing. A tidy entry written a month
later is worth less than a scrappy one written the same afternoon.

Entries about things that failed are worth more than entries about things that worked, and
are the ones most likely to go unwritten.

## Entries

| # | Date | Entry |
|---|---|---|
| [0001](0001-the-design-was-wrong-three-times.md) | 2026-08-02 | The design was wrong three times |
| [0002](0002-the-spike-that-was-too-fast.md) | 2026-08-02 | The spike that was too fast |
| [0003](0003-the-ignore-rule-that-keeps-eating-required-files.md) | 2026-08-02 | The ignore rule that keeps eating required files |
| [0004](0004-two-ways-to-report-a-success-you-dont-have.md) | 2026-08-02 | Two ways to report a success you don't have |
| [0005](0005-the-creatures-were-swimming-in-vacuum.md) | 2026-08-02 | The creatures were swimming in vacuum |
| [0006](0006-boxes-inside-boxes.md) | 2026-08-02 | Boxes inside boxes |
| [0007](0007-the-creature-that-was-paid-to-jam.md) | 2026-08-02 | The creature that was paid to jam |
| [0008](0008-the-energy-audit.md) | 2026-08-04 | What an energy audit found |
| [0009](0009-when-a-part-stopped-being-a-box.md) | 2026-08-07 | When a part stopped being a box |
| [0010](0010-the-world-starts-with-almost-nothing.md) | 2026-08-07 | The world starts with almost nothing |
| [0011](0011-the-sun-was-infinite.md) | 2026-08-07 | The sun was infinite |
| [0012](0012-the-body-that-cost-nothing.md) | 2026-08-07 | The body that cost nothing |
| [0013](0013-ninety-knobs-and-four-copies-of-each.md) | 2026-08-07 | Ninety knobs and four copies of each |
| [0014](0014-the-rotations-that-were-not-needed.md) | 2026-08-07 | The rotations that were not needed |
| [0015](0015-the-world-deleted-its-own-muscles.md) | 2026-08-07 | The world deleted its own muscles |
| [0016](0016-the-brain-that-was-never-read.md) | 2026-08-07 | The brain that was never read |
| [0017](0017-what-a-muscle-costs-to-own.md) | 2026-08-07 | What a muscle costs to own |
| [0018](0018-nothing-to-swim-towards.md) | 2026-08-24 | Nothing to swim towards |
| [0019](0019-three-knobs-that-reached-nothing.md) | 2026-08-24 | Three knobs that reached nothing, and a seed that was not a seed |
| [0020](0020-a-sun-that-sets-over-a-world-with-one-crop.md) | 2026-08-25 | A sun that sets, over a world with one crop |
| [0021](0021-the-food-all-fell-to-the-bottom.md) | 2026-08-25 | The food all fell to the bottom |
