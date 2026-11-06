# Proposal: a gripping plant lives through the winter

*Draft for the owner, 2026-11-06, as D318 asks. World rules are the owner's; a founder's genes and the reads are agent
work. Absorbed into DECISIONS.md on ruling, then deleted.*

## The proposal in one paragraph

A forest of gripping plants holds on an island in a world without winter, and dies each winter under D151's year. Its
plants starve, I think because they carry a few hundred seconds of reserve into a winter of 3,000 s. There are three
ways through. A plant can bank the summer's surplus, which needs no new rule. It can shed its added blades and rest at a
fraction of its costs until its light returns, D318's own example. Or its young can wait out the dark on the rock. I
recommend screening the bank now and building the rest beside it, because I expect the bank alone to fall short.

## What the runs found

Each forest run asked whether, at 12,000 s, one island's coral carried at least 20 gripping plants, four in five born
after 3,000 s. Without a winter, the diagnostic passed on both seeds, with 25 and 26 on the best islands. With D151's
year, winter at 0.4 of the mean light for 3,000 s and summer at 1.6, the second run failed on both. In each winter about
twice as many grippers died as began, most of them starving at 3 to 6 m.

The plants live close to the line. In the first run, 20 of a stand of 38 died before the 250 s ramp into winter had
ended, 7 of them in full summer light. Nothing in a plant saves for a winter. The module rule adds a blade once the
reserve passes 300 s of the plant's upkeep. A birth comes once it passes the litter's price plus a heritable margin, and
nothing else caps it (`EVOSIM_RESERVE_CAP` 0). A plant under 50 s of upkeep for 100 s drops its added blades one by one,
each taking its share of the reserve (`WorldModules.cs`). In the diagnostic without the winter, the plants that died of age
on 6202 had lived a median 1,778 s, and 712 to 3,308 s at the tenth and ninetieth percentiles (D128's hazard), so a
plant that lives through a 3,000 s winter comes out of it old.

## A: the plant banks the summer (no new world rule, agent work, a small build)

The anchored founders (D296) are given a reserve margin of 3,000 s of their upkeep, a winter's cost with no income. The
margin is the body's own heritable gene already (D098), so selection moves it from there and nothing reads the world.
Today every founder draws its margin from one range (`EVOSIM_MARGIN_MIN` and `EVOSIM_MARGIN_MAX`, 0 to 600 s in these
worlds), animals included, so A needs one small setting that gives the anchored founders a range of their own. Moving
the shared range would change the animals' world as well. The module line sits below the margin, so a plant grows its
blades first and banks once they are grown. Kelps such as *Laminaria*
store sugars in summer and live on them in winter (general knowledge, uncited). In the review's models, storage pays
more as the environment varies more, and more predictably [FDT11 Results, Figs 3–4]. A store also lets a body breed
before its feeding season [VJTF09 Abstract].

Over a year the light averages its mean, so the bank fills only for a plant that out-earns its upkeep at the mean light.
One that earns a fifth more has about 1,200 s of its upkeep to spare each year after the bank (inference, if income
rises in step with light). Before the screen, the ledger reads that surplus for a gripping plant at 3 to 6 m.

## B: the plant sheds its blades and rests (a new rule, yours)

1. A plant (a body with no eating tissue, D206) carries a heritable rest delay in seconds.
2. When its income has stood below its upkeep for longer than its delay, it rests. It drops its added blades to its
   genome's own plan and keeps their share of the reserve.
3. While it rests, its upkeep, its income and its ageing clock run at the rest factor, a world dial.
4. When its income at the full rate has stood above its upkeep at the full rate for its delay, it wakes, and the module
   rule grows the blades again.
5. It ships behind a switch, off by default.

Perennial kelps keep holdfast and stipe through the winter and grow a new blade from stores, and seagrass keeps its
rhizome (general knowledge). Plasticity evolved only where individuals could anticipate their environment [BWWR15
Results, Fig. 1]. A plant's income, low for hundreds of seconds, foretells a season of 3,000 s (inference). The plant
answers its own ledger, and no rule reads the world or the season's clock. B amends D106's drop, which today sends a
blade's reserve away, and D128 for resting bodies.

The factor does most of the work. At 0.05, the 250 s of reserve the first run's plants seem to have carried would last
5,000 s of rest with no income (inference, arithmetic). Shedding adds the autumn's litter, food for grazers as the light
fails, and costs the spring's regrowth, so the screen runs B with the drop and without it.

## C: the young wait out the dark (a new rule, yours)

A gripping plant's settled child (D307) is born resting at a heritable chance. It holds its grip, earns nothing, pays and
ages at B's factor, and wakes when its own light has stood above a heritable line for its delay. Annual kelps die back in
autumn while their microscopic stages wait on the rock (general knowledge). Dormancy in a fungus's spores evolved up and
down with how often bad years came [GSS14 Results, Table 2]. A plant here that dies of age lives under a third of a year, so
C fits its lifespan best (inference). It is the body's own trait and sense.

## What each costs, how it starts, and what a screen reads

Read the build rows first.

| | A, bank | B, rest | C, spores |
|---|---|---|---|
| Build, CPU | A setting for the anchored founders' own margin range; no format change | A gene and a rest state; genome and checkpoint formats move and fixtures are re-recorded | B's, and a new kind of birth |
| Build, card | None beyond the founders' draw; one card group of 70 to 90 minutes, as each forest run took | The factor enters each body's upkeep, income and ageing; kernels regenerated and sat again (inference, settled in the build) | B's; each resting child stays a body in the physics, at about 48 µs per simulated second (`proposals/propose-plants-grow.md`, under load) |
| Starts, then dialled back | A margin of 3,000 s, lowered until winter deaths rise | A delay of 600 s, over half a day so a night does not set it off (inference), and a factor of 0.05, raised toward 0.3 | Half the settled children resting, then fewer |
| Risk to the world | Anchored plants breed later and spread less in their first summer | Rests fired in summer, which the screen counts; grazers find fewer blades in winter | The bank could fill the body ceiling, as round 67's canopy did; the forest stands only in summer |
| The screen reads | Each gripper's reserve at each winter's start and end; births per gripper per summer | Grippers resting by season; tissue shed each autumn; grazers alive in winter | Resting children by island; wakers that grip in spring; bodies against the ceiling; pace |

Each screen also reads the grippers begun and dead by season against the second run's, the forest test and both books.
None changes a recorded world, since A is a launcher value and B and C sit behind switches.

## What I recommend

I recommend A now, and B built beside it. A costs one card group and shows whether a gripping plant can bank a winter at all. B
is screened after A reads, with A's margin as its control. The thin reserves of the first run's stand make me expect A
alone to fall short (inference), and B works on any bank. C waits, as the dearest.

## What the owner rules

1. I ask whether a plant may shed its added blades and rest, as option B sets out.
2. I ask whether a resting plant's ageing clock may run at the rest factor, amending D128 for resting bodies. Without it,
   a plant that rests through a 3,000 s winter wakes near the end of its life.
3. A needs no ruling unless you object, as founder work under D296.
