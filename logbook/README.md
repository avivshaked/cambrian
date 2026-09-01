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

## Reading the entries — a key for the newcomer

The entries are written for two audiences at once: agents continuing the work, and humans
reading the research cold. From [0036](0036-the-floor-gives-back.md) onward the entries
follow a pre-registration protocol — everything above each entry's *Results* line
(hypothesis, predictions, what would falsify each, and what either outcome would mean) was
written and committed **before** the experiment ran, so the git history proves the
predictions preceded the data. Those entries lean on a shared vocabulary that this section
defines once, rather than each entry re-explaining it.

**An *arm*** is one experiment: one world configuration and one random seed, run for a
budget of simulated seconds. Arms are named like `d057-s2` (round `d057`, seed 2) and each
runs on a ***worker*** — a copy of the Unity project, so several arms can run at once
without sharing state. Each arm writes a ***run report*** (`runs/<name>.md`, gitignored):
a header line recording every setting it actually ran with — the settings truth, always
trusted over the launch command — then one table row per ~100 simulated seconds (a
***sample***), and a footer saying how the run ended.

**How a run can end.** By its **budget** (the simulated seconds it was asked for — the
only ending that counts as the world's own answer); by **extinction**; by the **wall
clock** (a real-time limit on the machine); or by the population **ceiling** (a maximum
the instrument can afford to simulate, `MaximumPopulation` — a run the ceiling ends is
called a ***runaway***). The last two are limits of the instrument, not of the world, so a
run they end is called ***censored***, in the survival-analysis sense: cut short for a
reason external to what was being measured, so its data is read up to the cut and never
treated as an outcome. Every pre-registration says in advance how censored runs will be
scored. One more habit of the entries: "seed" names the RNG seed an arm ran with, and by
metonymy the arm itself — "three seeds in five died" means three of the five arms.

**The vocabulary of what happens in the water.** The ***population floor*** (no relation
to the sea floor) is a founding mechanism: while active, it trickles fresh random genomes
into any world that falls to 40 creatures. "The floor closes at 3,000 s" means it stops
firing then — after that, a world lives or dies on its own. ***Founding*** is the lottery
of those first random genomes producing a breeding population at all. A ***drought*** is
the recurring crisis of this world's economy: producers lock the surface's free matter
into their bodies, new conceptions are refused for want of matter, and births stop until
matter returns. A ***chain*** (or *absorptive lineage*) is the food chain's second level:
creatures carrying absorptive (detritus-eating) tissue inherited from a parent — consumers
that live on dead matter rather than light, where a ***producer*** lives on light alone. A
chain ***busts*** when it eats its food column faster than the food returns and collapses.
***Upkeep*** is the standing energy cost of being a body, per second; ***break-even*** is
the food density at which an absorptive's intake exactly pays its upkeep — below it,
eating loses money. And "D051"-style numbers are entries in
[`DECISIONS.md`](../DECISIONS.md), where the reasoning behind each mechanism lives.

**The report columns the entries quote**, with what each number means:

| column | meaning |
|---|---|
| `t`, `alive`, `births` | simulated seconds; living creatures; cumulative births |
| `absorpt` | living creatures with absorptive (detritus-eating) tissue — the chain's size |
| `inherit` | of those, how many had an absorptive parent — a lineage, not a fresh mutation |
| `det deep` | detritus energy density (J/m³) in the deep water — the chain's larder |
| `mat top` | free matter density at the surface — what conceptions are paid from |
| `mat blk` | conceptions refused for want of matter since the last row — the drought gauge |
| `floor` | creatures the population floor spawned since the last row — 0 means the world is on its own |
| `gen min` / `gen max` | lowest and highest generation alive — `gen min` 0 means founders are still present |

None of this is specification — what the mechanisms *are* lives in
[`DESIGN.md`](../DESIGN.md) and the reasoning in [`DECISIONS.md`](../DECISIONS.md); this
key only translates the entries' reporting shorthand.

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
| [0022](0022-the-conveyor-belt-and-the-thin-soup.md) | 2026-08-26 | The conveyor belt, and the thin soup |
| [0023](0023-nothing-died-of-old-age.md) | 2026-08-26 | Nothing died of old age |
| [0024](0024-the-larder-filled-and-nobody-came.md) | 2026-08-27 | The larder filled, and nobody came |
| [0025](0025-something-ate-something.md) | 2026-08-27 | Something ate something |
| [0026](0026-nothing-was-ever-eliminated-for-swimming-badly.md) | 2026-08-28 | Nothing was ever eliminated for swimming badly |
| [0027](0027-the-prize-was-smaller-than-the-entry-fee.md) | 2026-08-28 | The prize was smaller than the entry fee |
| [0028](0028-the-canopy-closed-and-the-scavengers-came.md) | 2026-08-28 | The canopy closed, and the scavengers came |
| [0029](0029-the-floor-kept-putting-the-muscles-back.md) | 2026-08-28 | The floor kept putting the muscles back |
| [0030](0030-the-mutation-that-never-got-the-memo.md) | 2026-08-28 | The mutation that never got the memo |
| [0031](0031-the-muscle-that-paid-you-to-carry-it.md) | 2026-08-28 | The muscle that paid you to carry it |
| [0032](0032-the-instrument-that-was-designed-and-never-built.md) | 2026-08-28 | The instrument that was designed and never built |
| [0033](0033-the-surface-stripped-itself.md) | 2026-08-28 | The surface stripped itself |
| [0034](0034-the-ocean-had-no-top.md) | 2026-08-28 | The ocean had no top |
| [0035](0035-the-neuron-was-priced-in-1994.md) | 2026-08-29 | The neuron was priced in 1994 |
| [0036](0036-the-floor-gives-back.md) | 2026-08-29 | The floor gives back |
| [0037](0037-the-net-comes-down.md) | 2026-08-29 | The net comes down |
| [0038](0038-a-lighter-world.md) | 2026-08-29 | A lighter world |
| [0039](0039-a-slower-drought.md) | 2026-08-29 | A slower drought |
| [0040](0040-right-sizing-the-dish.md) | 2026-08-30 | Right-sizing the dish |
| [0041](0041-the-sea-digests.md) | 2026-08-31 | The sea digests |
| [0042](0042-the-larder-under-the-mud.md) | 2026-08-31 | The larder under the mud |
| [0043](0043-the-transplant.md) | 2026-09-01 | The transplant |
| [0044](0044-three-medicines.md) | 2026-09-01 | Three medicines, one patient |
