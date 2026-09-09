# Logbook

Dated entries about what actually happened: what was tried, what broke, what surprised us,
and what the numbers were on the day.

This is the fifth document in the set described in [`CLAUDE.md`](../CLAUDE.md). The other
four describe the system as it *currently stands*. This one describes how it got there, and
it is the only one allowed to be out of date, because it is history, and history doesn't
drift.

## The rule that keeps this honest

The logbook is never a source of truth.

If an entry needs to state a design fact, it links to [`DESIGN.md`](../DESIGN.md) or quotes
it. It does not restate it in friendlier words. The moment an entry starts explaining what
the system *does*, it has become a second specification that nobody will remember to update,
and then neither document can be trusted.

An entry may freely describe what the system did **on the day it was written**, including
things that were true then and are wrong now. That is the point. Superseded entries are
never edited to match the present. A later entry supersedes an earlier one, just as in
[`DECISIONS.md`](../DECISIONS.md).

## How this differs from the other documents

| | Holds |
|---|---|
| [`DESIGN.md`](../DESIGN.md) | the specification — what the system is |
| [`DECISIONS.md`](../DECISIONS.md) | conclusions — what we chose, and what we rejected |
| **`logbook/`** | **process — what we did, what happened, what it cost** |

`DECISIONS.md` says *we rejected pooling*. The logbook says *we spent an afternoon building
a benchmark and misread it by a factor of thirty. We caught it by cross-referencing two
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

Write entries on the **day something happens**, rather than later. The specific details are
perishable within about a week. They are also the entire value of the thing: which run it
was, what the number actually was, what it felt like to be wrong. A tidy entry written a
month later is worth less than a scrappy one written the same afternoon.

Entries about things that failed are worth more than entries about things that worked, and
are the ones most likely to go unwritten.

## How to write an entry

The rules are in [`STYLE.md`](../STYLE.md), and `scripts/style-check.py` counts the habits
it warns against.

The short form: open with the point in one plain paragraph, and tell it in the order it
happened, in the first person. Define every term where it first appears, or link to the key
below. Say what a number means before quoting it, and keep one idea per sentence.

The pre-registration blocks are written before the run and never edited after it. Those are
the hypothesis, the predictions, the falsifiers and the two-sided readings.

## Reading the entries, a key for the newcomer

The entries are written for two audiences at once: agents continuing the work, and humans
reading the research cold.

From [0036](0036-the-floor-gives-back.md) onward the entries follow a pre-registration
protocol. Everything above each entry's *Results* line was written and committed **before**
the experiment ran, so the git history proves the predictions preceded the data. That covers
the hypothesis, the predictions, what would falsify each, and what either outcome would
mean.

Those entries lean on a shared vocabulary, which this section defines once rather than
having each entry re-explain it.

First, the vocabulary of the work itself.

| term | what it means |
|---|---|
| ***milestone*** | one of the numbered stages of the build plan in [`DESIGN.md`](../DESIGN.md), so "Milestone 3" names a place in the plan and not a date |
| ***spike*** | a throwaway project built to answer one question and then abandoned. There has been one, and it asked whether Unity's physics engine could carry the evaluation loop at the scale this project needs |
| ***round*** | a set of arms launched together to answer one question, numbered in order; its number is the first half of each arm's name |
| ***arm*** | one experiment: one world configuration and one random seed, run for a budget of simulated seconds. Arms are named like `d057-s2`, which is round `d057`, seed 2 |
| ***worker*** | a copy of the Unity project, so several arms can run at once without sharing state |
| ***run report*** | `runs/<name>.md`, gitignored: a header line recording every setting the arm actually ran with — the settings truth, always trusted over the launch command — then one table row per ~100 simulated seconds, and a footer saying how the run ended |
| ***sample*** | one of those table rows |

A run can end in four ways.

| ending | what it means |
|---|---|
| **budget** | the simulated seconds it was asked for, and the only ending that counts as the world's own answer |
| **extinction** | nothing left alive |
| **wall clock** | a real-time limit on the machine |
| **ceiling** | the population maximum the instrument can afford to simulate, `MaximumPopulation`; a run the ceiling ends is called a ***runaway*** |

The last two are limits of the instrument rather than of the world, so a run they end is
called ***censored***. That is the survival-analysis sense of the word: cut short for a
reason external to what was being measured. Its data is read up to the cut and never treated
as an outcome. Every pre-registration says in advance how censored runs will be scored.

One more habit of the entries. The word "seed" names the RNG seed an arm ran with, and by
metonymy the arm itself, so "three seeds in five died" means three of the five arms.

Next, the vocabulary of what happens in the water.

| term | what it means |
|---|---|
| ***population floor*** | a founding mechanism, with no relation to the sea floor: while active, it trickles fresh random genomes into any world that falls to 40 creatures. "The floor closes at 3,000 s" means it stops firing then, and after that a world lives or dies on its own |
| ***founding*** | the lottery of those first random genomes producing a breeding population at all |
| ***drought*** | the recurring crisis of this world's economy. Producers lock the surface's free matter into their bodies, new conceptions are refused for want of matter, and births stop until matter returns |
| ***chain*** (or *absorptive lineage*) | the food chain's second level: creatures carrying absorptive, detritus-eating tissue inherited from a parent, consumers that live on dead matter rather than light |
| ***stomach*** | the same creatures as the *chain*, named for the tissue: an absorptive lineage is a stomach line, and "the stomachs" are its living members |
| ***producer*** | a creature that lives on light alone |
| a chain ***busts*** | it eats its food column faster than the food returns, and collapses |
| ***upkeep*** | the standing energy cost of being a body, per second |
| ***break-even*** | the food density at which an absorptive's intake just pays its upkeep. Below it, eating loses money |
| a "D051"-style number | an entry in [`DECISIONS.md`](../DECISIONS.md), where the reasoning behind each mechanism lives |

Then the vocabulary of the open, shared world, from [0060](0060-the-outflow.md) onward.
Matter is the stuff bodies are built from, distinct from energy, and it now enters and
leaves the world instead of being fixed.

| term | what it means |
|---|---|
| ***influx*** | the rate new matter arrives, the "dose" |
| ***vent*** | the point on the sea floor where it arrives |
| ***plume*** | the upward current the vent drives, which lifts bodies as well as matter |
| ***stock*** (or *standing matter*) | the total free matter in the water at a moment |
| ***matter sink*** | the speed at which free matter settles toward the floor |
| ***burial*** | the fraction of floor-layer matter removed from the world each second, the outflow that lets the stock level off |
| ***patch*** | one of the world's spatial regions; since the shared box (D077) it is a literal 10 × 10 m zone read from a body's position |
| ***crowded stillbirth*** | the `crowded` column: a birth refused because no free space could be found beside the parent |
| ***goal rule*** | D063 in `DECISIONS.md`, the pass/fail criterion a world must meet. In brief: one connected stomach lineage alive and breeding to the end of a 30,000-s run, scored per seed, so "4 of 5" means four seeds passed it |
| ***simHash*** and ***coreHash*** | fingerprints of the exact source code a run was built from, the Unity side and the `Evosim.Core` side, so two runs can be known to share a build |
| ***fine step*** | dt 0.01 s, the step at which results count |
| ***fast step*** | 0.02, a screening step, three times quicker and less trustworthy on depth and movement |

Last, the vocabulary of reproducibility, from
[0069](0069-the-shared-world-does-not-replay.md) onward.

| term | what it means |
|---|---|
| ***physics step*** | one tick of the physics engine, 0.01 simulated seconds at the fine step; a 30,000-s run is three million of them |
| ***metabolic step*** | the half-second at which the economy of upkeep, feeding and breeding is settled |
| ***replay*** | the property that two runs with the same seed, settings and build are the same run to the last decimal |
| ***realisation*** | one run of a seed when replay does not hold: one roll of the dice rather than *the* outcome of that seed |
| ***ulp*** | unit in the last place, the smallest change a floating-point number can hold, and the size of difference at which two runs first part |
| ***island*** | a group of bodies that touch, which Unity solves together |
| ***job worker threads*** | the pool of extra CPU threads Unity solves those islands on |
| ***state digest*** | `EVOSIM_DIGEST_EVERY`, the instrument that fingerprints every body's position and velocity every N steps, so two runs can be compared to the step and body where they first differ |

These are the report columns the entries quote, with what each number means.

| column | meaning |
|---|---|
| `t`, `alive`, `births` | simulated seconds; living creatures; cumulative births |
| `absorpt` | living creatures with absorptive (detritus-eating) tissue — the chain's size |
| `inherit` | of those, how many had an absorptive parent — a lineage, not a fresh mutation |
| `clearance` | how much water a creature filters per second per unit of absorptive tissue, the stomach's gearing (D060, logbook/0050); `clearance 10` in a header is that setting |
| `exudation` | the share of a producer's light intake it releases into the water while alive, food for the chain (D070); `exudation 0.15` in a header is that setting |
| `det deep` | detritus energy density (J/m³) in the deep water — the chain's larder |
| `mat top` | free matter density at the surface — what conceptions are paid from |
| `mat blk` | conceptions refused for want of matter since the last row — the drought gauge |
| `floor` | creatures the population floor spawned since the last row — 0 means the world is on its own |
| `gen min` / `gen max` | lowest and highest generation alive — `gen min` 0 means founders are still present |

None of this is specification. What the mechanisms *are* lives in
[`DESIGN.md`](../DESIGN.md), and the reasoning in [`DECISIONS.md`](../DECISIONS.md). This
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
| [0045](0045-the-dose-and-the-dice.md) | 2026-09-02 | The dose, and the dice |
| [0046](0046-the-archean-package.md) | 2026-09-02 | The Archean package |
| [0047](0047-the-half-life.md) | 2026-09-02 | The half-life |
| [0048](0048-stirring-the-pot.md) | 2026-09-02 | Stirring the pot |
| [0049](0049-marine-snow.md) | 2026-09-03 | Marine snow |
| [0050](0050-the-stomachs-gearing.md) | 2026-09-03 | The stomach's gearing |
| [0051](0051-the-invasion-assay.md) | 2026-09-03 | The invasion assay |
| [0052](0052-the-coarse-step.md) | 2026-09-03 | The coarse step |
| [0053](0053-the-leak.md) | 2026-09-04 | The leak |
| [0054](0054-the-confirmation.md) | 2026-09-04 | The confirmation |
| [0055](0055-the-dry-deep.md) | 2026-09-04 | The dry deep |
| [0056](0056-the-queue.md) | 2026-09-04 | The queue |
| [0057](0057-energy-buys-matter.md) | 2026-09-04 | Energy buys matter |
| [0058](0058-the-open-budget.md) | 2026-09-04 | The open budget |
| [0059](0059-the-newborns-spin.md) | 2026-09-04 | The newborn's spin |
| [0060](0060-the-outflow.md) | 2026-09-05 | The outflow |
| [0061](0061-the-open-world-at-the-fine-step.md) | 2026-09-05 | The open world at the fine step |
| [0062](0062-the-senses-answer.md) | 2026-09-05 | The senses answer |
| [0063](0063-the-theatre-opens.md) | 2026-09-05 | The theatre opens |
| [0064](0064-the-crowd-costs-nothing.md) | 2026-09-05 | The crowd costs nothing |
| [0065](0065-the-first-shared-water.md) | 2026-09-05 | The first shared water |
| [0066](0066-one-box.md) | 2026-09-06 | One box |
| [0067](0067-the-shared-world-at-the-fine-step.md) | 2026-09-06 | The shared world at the fine step |
| [0068](0068-the-shared-world-fed.md) | 2026-09-06 | The shared world, fed |
| [0069](0069-the-shared-world-does-not-replay.md) | 2026-09-06 | The shared world does not replay |
| [0070](0070-the-good-world-with-one-change.md) | 2026-09-06 | The good world with one change: contact |
| [0071](0071-three-outside-reviews.md) | 2026-09-07 | Three outside reviews |
| [0072](0072-the-round-that-asks-whether-moving-pays.md) | 2026-09-07 | The round that asks whether moving pays |
| [0073](0073-the-chicken-and-the-egg.md) | 2026-09-07 | The chicken and the egg |
| [0074](0074-the-water-as-vertices.md) | 2026-09-07 | The water as vertices |
| [0075](0075-the-vertex-world-at-the-new-price.md) | 2026-09-08 | The vertex world at the new price |
| [0076](0076-the-hole-refills-in-five-seconds.md) | 2026-09-08 | The hole refills in five seconds |
| [0077](0077-the-water-stirred-less.md) | 2026-09-08 | The water stirred less |
| [0078](0078-the-water-as-a-grid.md) | 2026-09-08 | The water as a grid |
| [0079](0079-the-grid-has-its-own-count.md) | 2026-09-09 | The grid has its own count |
| [0080](0080-when-a-joint-is-free.md) | 2026-09-09 | When a joint is free |
| [0081](0081-born-at-a-third-of-itself.md) | 2026-09-09 | Born at a third of itself |
