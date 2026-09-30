# The nursery against review round 7: the agent's reading

The agent's reading of round 7's twenty JARTs (`research/papers/175-195`, each folder's `JART.md`)
against the nursery's open decision, which `fast-nursery-measurements.md` sets out under "What next".
Written on 2026-09-30, before Pass 2 has checked the papers against the databases and written round 7
into the review. So a paper is named here by its number and its page (the PDF page, as in its JART),
and the review's keys come later. The claims the recommendation rests on (Berg and Brown's pp.3-4,
Grabowski's reward on p.4, Chaumont's 3% on p.5) were checked against the papers' own extracted text,
not only their JARTs. It is input for the owner, who deferred the decision until this round was in.
It is not the decision. Where a line says what a paper means for this project, it is the agent's
inference and says so.

## Why the kinesis graft failed

Berg and Brown tracked single *E. coli* in three dimensions (186). A cell's runs grow longer while it
swims up a gradient and stay as they were while it swims down one (pp.3-4). It does not steer during a
run, and the direction it takes after a tumble does not depend on the gradient (p.4). What it answers
is the rise in concentration over time. For aspartate only the rate of rise mattered, and for serine
both the rate and the level (p.4). A level on its own does something else: serine in water with no
gradient lengthened runs all the same (pp.2-3), which is a change of pace and not a taxis.

The graft of measurement 3 slowed the tail where the scent was strong. That answers the level of the
scent, not its change. The agent's inference: a level answer makes a body linger where the scent is
high and does not carry it up the gradient, which is what measurement 3 saw. The body sat 0.9 m
nearer the food and ate no more. So the failure follows from the mechanism, and it is not evidence
that a one-tailed body cannot find food.

The drift up the gradient is small. It was about 2 µm/s in serine for cells that swim at about
14 µm/s (pp.1, 4). A control built on this mechanism has to be scored over enough time and trials to
see an effect of that size.

## Does a comparison over time evolve?

In Avida, organisms evolved to follow a gradient within one or two thousand updates (182, p.5). They
did so both when the sensing instruction handed them the previous value and when they had to keep it
themselves. The handed value did better: a best-distance ratio of 0.90 against 0.85 over the last
50,000 updates, over 50 runs (p.5). Two cautions come with it. The organism was paid each time it came
closer (p.4), and its sensor was a noise-free distance on a grid.

Lehman and Miikkulainen found that behaviours needing memory are deceptive (189). A fixed objective
converged on reactive policies, while novelty search found the memory task's solution. In the harder
maze the objective methods never solved the memory task, and novelty search did in 16% of runs (p.6).

The agent's inference: a brain that must build its own memory finds it slowly under a fixed score. A
ready-made `Differentiate` neuron plays the part of Avida's handed-over previous value.

## Does coming nearer pay?

Lenski and colleagues' most complex function, EQU, never evolved (0 of 50 populations) when only EQU
was rewarded (185, p.5). With one or two of the simpler functions unrewarded it still evolved in 34%
of 360 populations. Complex functions grew from simpler ones that were themselves paid, though no
single intermediate was essential (pp.1, 5).

The agent's inference: in measurement 3 the simpler step, coming nearer the food, did not raise what
was eaten. If nearness is not paid, steering has no paid step on the way to it, which is Lenski's
EQU-only case. So the next control has to be read on two numbers, distance and energy eaten. If it
comes nearer and still eats no more, the obstacle is how nearness becomes a meal (reach, the mouth,
how a corpse is found). That is a question about the world's rules, and so the owner's.

## How others got steering in physics

- Chaumont and Adami evolved three-dimensional foragers with Sims-style neurons, `Differentiate`
  among them (184, pp.4-5). The sensors reported the angle and distance to the food, the score paid
  for approach, and a human picked each stage's founder (pp.5-6, 11-12). Only 3% of 400 first runs
  produced organisms that took different paths to different foods (p.5). In the early stages the
  organisms could not bear noise in where the food was placed (p.23). Reaching one food and reaching
  several turned out to be separate skills (p.23).
- Pilat and Jacob grafted vision neurons onto walkers already evolved, and most runs then learned to
  follow a light. Evolving body and brain from scratch did poorly (179, p.5). Bodies that held a
  steady orientation learned fast, and spinning ones struggled (pp.5-6). How the sensor was encoded
  mattered as much (p.6).
- Microcosmos evolved chemotaxis in two-dimensional filaments in a fluid, with a score that paid for
  displacement plus energy collected (194, p.6). These are demonstrations, with no counts.
- Bejjani and colleagues paid nothing at all and still saw long-range foraging evolve. Their agents
  had a compass, a global direction, and the effect held only in large worlds (192, p.8).

The agent's reading: every success in a physics world paid for approach, and so did Avida's. That is
option 4. The review gives that route the most precedent, and gives the owner's principle (the
nursery pays what the ecology pays) the least.

## An easier world first

- Gomez and Miikkulainen evolved prey capture on a grid (177). Aimed straight at the goal task,
  evolution failed in all ten runs. It succeeded when the prey began still and was sped up step by
  step, with the score, captures, unchanged (pp.13-15). Their heuristic is to raise the density of
  relevant experience in each trial (p.6). The evidence is five runs against ten, with no statistics.
- In Swain and colleagues' model, perception evolved reliably only above a threshold of food density,
  and only sporadically below it (191, p.7). The agent's inference: this supports the nursery's
  denser food.
- Jakobi's rule for moving a controller out of a simplified world is to vary every aspect the task
  does not need, at random from trial to trial, so the controller cannot come to depend on it (178,
  p.8). The agent's inference for D134's design A: vary the nursery's tank size, current and starting
  places from trial to trial, so the brain cannot lean on the nursery's walls or the direction of its
  current. The evidence is one network for each of two experiments, with no run that left the
  variation out (pp.25, 39).
- Goals that varied in a modular way sped evolution up to about a hundredfold over a fixed goal
  (187, p.2), in logic circuits, networks and RNA, not in bodies.
- POET (188) found that a direct path from easy to hard failed on its hardest levels, and that moving
  agents between environments was essential (pp.14-18). That took three runs of ten days each on
  256 cores.
- Against the small tank: in Bejjani's world, small worlds went extinct more often and found the new
  behaviour less often, and splitting one large world into many small ones did not make up for it
  (192, pp.19-20, 28). The nursery scores bodies on a fixed schedule and cannot go extinct, but fewer
  bodies explore less.
- When every individual scores equally badly there is no gradient to climb, the bootstrap problem
  (175, p.1). A pressure towards behaviour unlike the rest of the population solved it on a robot
  task, a little more slowly than paying for sub-goals (176, p.5: 218 against 138 generations).

The gap Pass 1 found still stands in these twenty. None moves an evolved brain that senses and steers
from an easier tank to a harder world with the score held fixed. Chaumont's staged food placement is
the closest, and it paid for approach and had a human choose its founders.

## What this does to the options

1. **The run-and-tumble control first** is better supported than when the agent first recommended it.
   Berg and Brown give the mechanism, and Avida shows that a ready-made comparison helps. Two
   sharpenings follow from the reading. Score the control on distance and on energy eaten, and let
   the turn come only while the scent falls, over enough trials to see a small drift.
2. **Design A without a working control** is weaker. Lenski's EQU-only case and Lehman's memory tasks
   both say a fixed score with no paid intermediate rarely finds a behaviour that needs memory.
3. **Bodies that can steer** stays the fallback. Pilat says the body's way of moving decides how fast
   steering is learned, which is the question option 1's control answers first.
4. **Credit for coming near the food** has the most precedent in this round, and it is the route the
   owner has called breeding.

Two things the round adds for the owner. A pressure towards unlike behaviour (176) could take the
place of credit for approach, if the nursery's score ever needs help. And if design A runs, its
nursery could vary its tank from trial to trial as Jakobi says.

The agent's recommendation is unchanged: option 1 first, with the two sharpenings. It is written
before Pass 2, which may correct a reading here. The decision is the owner's.
