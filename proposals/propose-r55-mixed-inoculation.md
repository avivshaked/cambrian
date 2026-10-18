# Round 55: a mixed inoculation, and the world on brain v2

*Proposal for the owner, 2026-10-04. Draft; nothing here launches before the owner rules on the four
questions at the end.*

## What the owner has already decided

On 2026-10-04 the owner ruled four things, recorded in HANDOFF item 1a:

- Round 55 is inoculated with a mix of the four brain-v2 nursery runs, not one chosen recipe. The world's
  selection says which wins.
- The inoculants are what the four runs saved: each body's exam genome and the best genome of each
  generation.
- Inoculated founders keep arriving through the round, not only at its start.
- From now on every nursery run keeps its last generation's whole population (built: `nursery-general`
  at `f6f01f6`).

This proposal sets out how to build and run that, and the four questions still open.

## Why a mix

Neither separation arm moved the nursery's exam. Dropping the drive sense gave 505.7 J, inside the margin
by 1.3 J, and dropping the crossover gave 463.6 J, against the pilot's 457.3 and the replicate's 433.2
(`logbook/specs/nursery-plan-joint-drive.md`). One seed cannot separate the four recipes. The world is
the test that matters, and it can take all four at once. Taking many genomes per body, not the exam's
single top, also blunts the winner's curse that left one body stuck in each of the two first runs.

## The inoculant set

The four runs saved 342 distinct genomes:

| Run | Body 0 | Body 1 | Body 3 | Body 4 | Body 8 | Body 10 | All |
|---|---|---|---|---|---|---|---|
| `pilot-deep-brain2` | 8 | 20 | 12 | 1 | 1 | 41 | 83 |
| `brain2-rep2` | 1 | 26 | 23 | 1 | 3 | 30 | 84 |
| `brain2-nodrive` | 9 | 38 | 10 | 1 | 2 | 28 | 88 |
| `brain2-nocross` | 11 | 14 | 17 | 1 | 3 | 41 | 87 |

The per-generation bests are mostly bodies 1, 3 and 10. Bodies 4 and 8 have little more than their exam
genome, because the search's top member was rarely on them. The early generations' bests are barely
trained.

**Recommended:** per run and body, the exam genome and the most recent distinct bests, up to ten in all,
which comes to about 160 genomes (40, 35, 42 and 44 by run). Each of the 24 run-and-body cells gets the same total weight, split
evenly among its genomes. So a cell with one genome gets as many arrivals as a cell with ten, and no
body or run is favoured by how often the search happened to rank it first.

a8 (evolution-simulator-19) cuts the set into one file per genome under `inocula/r55-nursery/`, with an
origin table: run, body, generation born, score and file hash.

## How they arrive

The world already has the means: D117's trickle pool. After the floor closes at 3,000 s, each trickle
founder is drawn from the pool with a set probability. Round 54 used it for four stomach genomes at one
founder in ten, and its seeds each took about 900 trickle founders over 27,000 s, one every 30 s on
average. Each pool founder's lineage row names its pool index, so every living body traces to its
founder, and through the origin table to its run and body.

The builds this needs, all on branch `round-55` (`scratch/wt-r55`), none adding a tunable, so a run that
uses none of them stays bit-identical:

1. **A weighted pick with origins.** A pool list file gives each genome its weight and its origin; the
   run directory pins both in `pool/index.tsv`. The pick uses the same single random draw as now.
2. **The pool's cap** raised from 100 to 1,000.
3. **Pool founders get the world's brain.** Today a pool genome goes in unconverted, so round 54's
   stomachs, which are old per-node genomes, would found old-brain lines inside a brain-v2 world. Under
   the fix they are wired as every other founder is (D146). The nursery genomes already carry brain v2
   and keep it as it is.
4. **The brain model in the run header**, which today shows only in `config.json`.

The first 40 to 45 founders, the floor's, draw from the pool too, at the same share (the owner's ruling of
2026-10-04, after the smoke: founders arriving into the full world after the floor closes almost never
breed, while most of the floor's do). It is a new tunable, `FoundingFloorPoolShare`, so the fixtures are
re-recorded once more and the suites run again.

## The world

Round 55's world is round 54's, with two changes that come from rulings already made:

- **Brain v2 for every creature (D146, D147).** Every ordinary founder gets the one brain too, not only
  the inoculants. So round 55 differs from round 54 in its brains as well as its inoculants, and the
  uninoculated seed is the control for the inoculation only, not for the brain.
- **The drive sense on (D144, D145)**, so the world's senses match the nursery's thirteen. A brain
  without drive links, as `brain2-nodrive`'s are, can gain one by mutation in the world: about one new
  link in twenty births, by a rough count.

The free first joint (D143) stays off: its nursery arm never ran. Round 55 is CPU-only, as round 54 was,
since the card refuses brain v2.

## Before the launch

- **Tests.** The fixtures re-recorded on the round's build (under way since 09:41), then Core `-All`,
  Dynamics and Farm.
- **A smoke run** of round 55's world with a few nursery genomes in the pool, a few thousand seconds on
  four threads. It must show:
  - creatures born, breeding and mutating their brains without a refusal. The nursery held births, so a
    world birth under brain v2 has only been run by one Core test;
  - pool founders arriving converted or as carried, and traceable to their origin;
  - **the genome's size.** The world writes a genome for every birth. The nursery's two-part genomes
    are 5.4 KB each, but the brain-v2 build spec estimates 70 to 180 KB per birth for a fully wired
    ten-part body. If the smoke's lineage grows by gigabytes an hour, the lineage needs a leaner way to
    store genomes before the round runs.
- **A pre-registration**, committed before launch, that fixes the reading below.

## What counts as a win

Proposed for the pre-registration, read at 30,000 s:

- **Who holds the tank.** The share of living bodies descended from a nursery founder in each inoculated
  seed, and its split by run of origin. A run wins if its line holds the largest share in both
  inoculated seeds.
- **Whether inoculation changed the world.** The jointed share, and jointed speed against rigid, in the
  inoculated seeds against the control seed. Round 54's seeds 2 and 3 went to rigid leaves in the first
  seconds. If jointed nursery lines take hold when they arrive in the thousands, that is a different
  world.
- **Nothing holds.** If no nursery line lasts, that says the bodies, not the brains, are what the
  leaves crowd out, and that is a result too.

## The four questions

The owner ruled on questions 1 to 3 on 2026-10-04, each as recommended: one founder in two from the
nursery pool, the stomachs kept at one in ten, and seed 1 as the uninoculated control. Question 4 is open.

**1. The pool share: what fraction of trickle founders are nursery genomes.**
- *Recommendation: one in two.* That is about 450 nursery arrivals a seed, about 19 for each run and
  body. Ordinary founders still make up four in ten, and the stomachs keep their one in ten.
- *At one in ten*, as for the stomachs: about 90 arrivals a seed, under 4 a cell. Most genomes would
  never arrive, and one lucky line would decide the round.
- *Higher* crowds out the ordinary founders that round 54 measured against.

**2. The stomachs.**
- *Recommendation: keep them at one in ten, converted to brain v2 by the fix above*, so the round
  differs from round 54 by as little as it can.
- *Or drop them*, which simplifies the pool and loses round 54's continuity.

**3. The control.**
- *Recommendation: seed 1 uninoculated* (stomachs only), seeds 2 and 3 inoculated, as D141 set it up
  for round 54.

**4. D145's bar.** D145 said round 55 carries no inoculant unless a brain beats its body's best constant
by two standard errors, and then in one seed only. Only body 10 cleared that, in every run.
- *Recommendation: replace it* with this proposal's mix in two seeds, the selection in the world taking
  the place of the exam's bar. A new decision entry records it and marks D145's clause superseded.
