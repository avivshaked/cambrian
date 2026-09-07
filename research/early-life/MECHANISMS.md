# What the early world had, and what this one is missing

**2026-08-29**  ·  a survey, prompted by a run of worlds that hold a buoyancy lineage and no
food chain

The sources, and how much of each was read, are in [`SOURCES.md`](SOURCES.md). Nothing here
is a source of truth, and `DESIGN.md` is.

## The headline

**Most of what is missing is world mechanics rather than organs.** This project's instinct
when a world underperforms has been to add a cell type. D049's buoyancy cell is the most
recent of those.

Of the six gaps below, **two** would be cell types, and both are blocked behind a missing
environmental process. The others are chemistry the world does not do: nothing decomposes,
nothing excretes, and there is one nutrient where reality runs at least two.

⚠ *inference.* The pattern is mine rather than a source's. It is stated here because it
predicts that the next few cell types will underperform for the same reason `Consumer` does.
An organ needs something in the environment to be an organ *for*.

## 1. Ballast is buoyancy that regulates itself, with no brain

**What the evidence says** is that cyanobacteria regulate buoyancy three ways: gas-vesicle
collapse under turgor, regulation of gas-vesicle formation, and **carbohydrate ballast**.
The third dominates short-term movement, because carbohydrate fluctuates faster than
vesicles do [MICROCYSTIS11].

Carbohydrate accumulates while the cell photosynthesises, weighing it down until it sinks.
In the dark the carbohydrate is consumed, the ballast depletes, and it rises again.

Fogg & Walsby proposed in 1971 that this exploits the **separation of light and nutrients**,
and it became a paradigm. [BORMANS99] **questions it**. Evidence for population migration
deep enough to reach nutrients is "tenuous", and most field observations show no such
migration.

> algal buoyancy appears to be dependent much more on light than on nutrients

That is consistent with ballast being a light response that happens to move cells, rather
than a nutrient-seeking strategy.

**What this world has** is `BuoyancyCell` with a genome-encoded, *fixed* lift (D049, D050). A
lineage picks a depth at birth and holds it for life.

**What is missing** is any response. Lift responds to nothing, so a creature's buoyancy is a
constant, and the organ can choose a depth and can never trade between two.

**Why it matters here** is a measured gradient. D048 built a world with light at the top and
matter at the bottom, and measured the consequence. At the end of `d050-mix-heavy`, surface
matter read 0.02 against 1.095 deep, a **55× gradient**, with 8,777 conceptions blocked for
want of matter.

The buoyant hold the light and starve for matter. The sinkers hold the matter and starve for
light. Fixed lift cannot resolve that, and ballast can, with no neurons at all: the feedback
runs entirely through the energy reserve this simulator already tracks per creature.

⚠ *inference.* Two further arguments, both mine. First, this is the Archean-appropriate
version. The project has already once asked a Cambrian organ, the joint, to do an Archean
job, and spent months finding out it does not pay (logbook/0027, logbook/0030).

Second, **this world is a cleaner test of Fogg & Walsby than a lake is.** [BORMANS99]'s
field evidence is confounded by surface mixed-layer dynamics and lateral advection. Here the
separation is enforced by construction and the confounds are absent, so the hypothesis is
decidable.

## 2. Nothing decomposes

**What the evidence says** is that in the real ocean a large fraction of production never
reaches a consumer at all. Viral lysis infects 20–40% of bacterial biomass and converts
particulate organic matter to **dissolved** organic matter, which microbes take straight
back up. As much as **25% of global phytoplankton primary production** is recycled this way
[VIRALSHUNT].

**What this world has** is a corpse. Death deposits its joules as detritus in the layer it
died in, where the detritus sinks. Only an `Absorptive` or `Consumer` cell can ever touch it
again.

**What is missing** is a path from detritus back to the resource pool that does not
require an organism. Detritus is a one-way ratchet. It accumulates, and if the cells that eat it go
extinct it accumulates forever.

**What was measured** was that in every buoyancy world run on 2026-08-28, `absorpt` reached 0
and detritus climbed monotonically to 30,000–69,000 J. The reason is not that filter feeding
is mispriced. It is that the food is too thin where the creatures are.

| world | `J/m³ here` |
|---|---|
| clearance arms, **which held a food chain** | 17.8 |
| `d050-mix-heavy` | 0.07 – 1.0 |

That is **20–250× below** what sustained detritivory.

⚠ *inference:* detritivory has a density-dependent threshold, since it needs a producer
population dense enough to rain corpses. Remineralisation is what keeps the producer
population dense enough in the first place, by returning matter instead of burying it. The
two failures are the same failure.

## 3. One nutrient where reality runs at least two

**What the evidence says** is that marine C:N:P sits near 106:16:1 [REDFIELD], and that
limitation is **serial**. Relieve nitrogen and phosphorus immediately becomes limiting.

**What this world has** is one undifferentiated `Matter` field (D048), with one sink rate and
one mixing rate.

**What is missing** is a second nutrient with *different* physics.

⚠ *inference:* with one pool there is one depth optimum and therefore one right answer. That
is a single-peaked search space, the shape §5A.2b already identified as the thing that
collapses morphological variety. Two nutrients that sink and mix at different rates give two
optima and a reason for two body plans.

## 4. Nothing excretes

**What the evidence says** is that oxygen was **metabolic waste**. Mineral sinks absorbed it
until they saturated. It then poisoned the anaerobic incumbents into an extinction, removed
atmospheric methane, and became the fuel for aerobic metabolism [GOE].

**What this world has** is shade, and a corpse on death. D048 made producers *consume* a
resource, and it did not make anything *emit* one.

**What is missing** is a cell output that enters the environment as a field. Oxygen is the
canonical case because it is at once a **poison to the incumbent and a resource to the
successor**. That is the strongest available form of an organism changing its world so that
it cannot stay as it is.

⚠ *inference.* This is the gap with the largest blast radius and it should not be built
third. It requires a general "cells emit into a field" contract, and once that exists, the
appendix's photoferrotrophy and much of §6 follow from it rather than needing separate
machinery.

## 5. No substrate, so no sessility, and that would be a **cell type**

**What the evidence says** is that benthic microbial mats date to **3.47 Ga**, binding and
trapping sediment to build stromatolites [MATS]. The sea floor was the first ecosystem
rather than a boundary.

**What this world has** is a deepest layer where detritus piles up, and a `% on floor`
statistic.

**What is missing** is anything to attach to, and any advantage in attaching. A holdfast cell
is the obvious organ, and it is worthless until the floor is somewhere worth being, which is
§2's problem.

## 6. Predation fails, and its failure is correct: a **cell type already present**

**What the evidence says** is that predation required prior diversification and **biomass
accumulation of prey**, plus an oxygen threshold. Modern-like ocean O₂ first appears at ~521
Ma [PREDATION].

**What this world has** is `ConsumerCell`, available from t=0, which reliably fails.

**What is missing** is its prerequisites.

⚠ *inference:* `Consumer` failing is the correct answer for an Archean world rather than a
bug to fix. It stays correct until §2 raises standing biomass and §4 provides the oxygen.
Tuning it before then would be tuning it to succeed in a world that should not support it.

## Appendix: a producer for the dark

Under the ferruginous, sulfide-poor Archean ocean, **photoferrotrophy** may have accounted
for most photic-zone primary production. It is anoxygenic photosynthesis on Fe(II) rather
than water, and it was progressively **pushed to greater depths** as cyanobacteria
oxygenated the surface [PHOTOFERRO].

That is a second producer whose niche is *created by the first producer's waste*, which
makes it the natural test of §4 rather than a separate feature.

⚠ *inference:* it is also the cleanest available answer to §3's single-peak problem, because
the two producers would be limited by different things.

## Ranking

| | Gap | Kind | Blocked by |
|---|---|---|---|
| 1 | Ballast | mechanic, existing machinery | nothing |
| 2 | Remineralisation | world process | nothing |
| 3 | Excretion into a field | contract + field | nothing, but largest blast radius |
| 4 | Second nutrient | field | nothing |
| 5 | Photoferrotrophy | cell type | §4 |
| 6 | Holdfast / sessility | cell type | §2 |
| 7 | Predation prerequisites | *already built* | §2, §4 |

⚠ The ranking is inference throughout. Gaps 1 and 2 are cheap and independent. Gap 3 is the
one that unlocks the rest, and is worth doing before 5–7 are attempted at all.
