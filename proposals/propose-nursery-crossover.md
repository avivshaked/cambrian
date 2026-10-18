# Proposal: two parents in the nursery, the strongest route passed, mutation scaled by novelty

*The agent (Opus 5.5), 2026-10-01 evening, written up from the owner's design in conversation that
afternoon. Every rule below is the owner's unless it is marked as the agent's choice. On writing it, the
owner asked for it to be written up and then asked when it can be run. Absorbed into DECISIONS.md once the
build is checked, then deleted.*

## What it changes, and why

Today the nursery makes every child as a mutated copy of one parent (`Tank.cs`: elites carried, a
tournament of three, no crossover). A working part of one brain can never reach another line. This
proposal gives each child two parents. The child takes one brain almost whole and, half of the time,
one route from the other: the route that drives one of the joint's outputs hardest. The child is then
mutated, with a chance that falls as the crossover brings more that is new. A child that would be a
copy of its parent is always mutated.

It applies to the nursery only, under `--evolve brain --per-body`. The world reproduces asexually by
design (DESIGN §5A), and carrying the rule into it would need two-parent reproduction there, which is a
separate ruling.

## The rule, for each child

### The parents

Two parents are chosen, each by today's tournament of three, from the same body's group. Both brains
therefore drive the same body, joint and senses.

### The base

The better-scoring parent is the base 70% of the time and the other 30%. The other parent is the
donor. The owner chose a fixed share over one proportional to the scores. Scores can be negative, and
any normalisation would bias the choice.

### The crossover

It happens in half of all children (`--crossover 0.5`). The share is the agent's starting value and a
setting.

### The route, by weights alone

- It starts at one output, chosen at random among every power and every bend of every degree of
  freedom of every joint. In a jointed part with *n* degrees of freedom, neuron *d* is degree *d*'s
  power and neuron *n + d* its bend.
- From a neuron, the route steps to the input with the largest absolute weight. Constant inputs and
  neurons already on the route are skipped.
- It stops at a sense, or at a neuron with no input left to follow, such as an oscillator or the end
  of a loop. What was found is passed either way.

### The copy

- Each neuron on the route is copied whole into the base, in the same part at the same position. That
  is its operation, its inputs and their weights, its frequency, phase, amplitude and bias.
- Where the base has no neuron at that position, the neuron is added to that part. Inputs between
  neurons of the route are pointed at the copies.
- An input from a copied neuron to a donor neuron that was not copied keeps its position if the base
  has a neuron there. If not, it is dropped. This is the agent's choice inside the rule.
- The output neuron is not copied. Only its one input from the route is set to the donor's. It
  replaces an input from the same source, or is added. If the output reads a sense directly, that one
  input is all that passes.
- The copy is made in the genome. A neuron in a repeated segment is one gene, copied once.
- If an added neuron would take the brain past the genome's neuron limit, there is no crossover.

### The mutation

- Its chance is p = max(0, 1 − d / d_ref).
- The distance d is the brain distance between the child and its base, the brain term of D057's
  species distance.
- The yardstick d_ref is the median distance one ordinary mutation moves a brain. It is measured on the elites of
  tonight's run before the build is used.
- A child identical to its base has d = 0 and is always mutated. The mutation itself is today's,
  unchanged.

The yardstick is the agent's choice. The owner first set the far end at the distance between two
random brains, and left the measure to the agent. A small crossover never moves a child that far from
its base. With that yardstick the chance would sit near 1, and the rule would barely act.

### What stays as it is

The elites, the tournament, the per-body groups, the score, the episodes and the draws.

## How "strongest" was settled

The owner weighed three measures before choosing the weights. A sense's spread says how lively it is
but not whether the output follows it. Correlation says whether the output follows it. But our neurons
are Sims-style operators with thresholds, products, oscillators and memory, and a sense can matter
through a curve or a delay that correlation misses. Silencing each sense in a replay of the recorded
episode measures what the brain does with it, at the cost of a day's build. The owner chose the weights
alone: a route traced from the output back by the largest absolute weight. It is half a day's build,
it needs no recording, and anyone can check by hand which route was taken.

The limits of that choice are known. A large weight on a sense that barely changes looks strong and
does little. The comparisons, the conditionals, the minimum, the maximum and the product do not weigh
their inputs alike, so the route can take the wrong branch through them. A route can end at a rhythm rather than a sense. The
silencing replay is the upgrade if the log shows routes that make no sense, and nothing else in the
rule would change.

## What the log records

For each generation the log counts the crossovers made and the neurons added. It gives each route's
length and how it ended: at a sense, an oscillator or a loop. It also gives the mean d, the share of
children mutated and the brains' sizes.

## The test

- The control is tonight's priced run, `scratch/nursery/pilot-deep-own16`.
- The new arm has the same six bodies, draws and seeds, 100 generations at 16 draws each, and the work
  cost at 0.5, with the rule on.
- The exam is 32 held-out draws, against frozen brains, the constant strokes, the kinesis and the scent
  knockout. It is read pair by pair against the control's exam.
- The new build is checked first. With every new setting off, it must reproduce the control's first
  generations to the last digit. The comparison is then of the rule and not of the build.

## The build

About half a day on the branch `joint-drive`, in its worktree under `scratch/`. It adds three settings:
the crossover's share, the base's share and the mutation by distance. The tests trace the route through hand-built brains,
including one that ends at an oscillator and one with a loop. They also check that adding a neuron
re-points the route's inputs, that d = 0 always mutates, and that the output neuron takes one input
and nothing else.

## What the evidence does and does not cover

The literature review has nothing on crossover or recombination, a gap DESIGN §5A records. DESIGN §4.5
calls plain weight-by-weight crossover meaningless on a brain whose shape varies. It names NEAT's
historical markings [K12 §2.5, p.8] as the principled way to line two brains up. This rule avoids most
of that problem by passing one route at fixed positions within one body's lineage, but it is new ground.
Scaling mutation by how alike the parents are has a history in genetic algorithms (Eshelman's CHC is
the best-known case). That is the agent's knowledge, not checked against any source the review holds.
NEAT and CHC are what a review round would read first.
