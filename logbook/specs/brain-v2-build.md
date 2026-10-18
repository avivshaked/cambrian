# Brain v2: the build

Written 2026-10-01 night, during the build the owner set as the goal that evening. The design and the
owner's rulings are in [`proposals/propose-brain-v2.md`](../../proposals/propose-brain-v2.md) and D146.
This file says what was built, where, and which numbers are guesses. It is updated as the build moves.

The build is on branch `joint-drive` (worktree `scratch/wt-joint`), on top of `8d7daba`.

## What a creature carries

A genome of the new model carries one brain, `Genome.Brain`, and no neurons on any node. The brain
holds three lists.
- **Outputs**: two per degree of freedom of every joint, the power and the bend of D135, each with a
  bias. They are never removed by mutation.
- **Hidden units**: none at founding; mutation adds them.
- **Links**: a source, a target and a weight. The source is a port, an output or a hidden unit. The
  target is an output or a hidden unit. A link from a unit to itself is allowed (the owner agreed).

The ports are not stored. They are the body's senses: every channel of the run's sensor pool on
every part, at each index the part has. A joint channel and the three drive-sense channels have one
index per degree of freedom and none on a rigid part. Flow has three. The reserve (`Energy`) is the
whole body's, so only the root has it.

## Identity

Every part has a `PathId`, a 64-bit hash of its developer path from the root: at each step the edge
followed and which mirror copy. An edge is named by a permanent `MorphEdge.Id`, given when the edge
is made (`Genome.NextEdgeId`) and carried by every copy. A genome of the old model has no edge ids,
and its paths use the edge's index. The nursery's bodies never change, so the index is enough there.

A port's id hashes the part's id, the channel and the index. An output's hashes the part's id, the
degree of freedom and the kind. A hidden unit's hashes the link it split and how many times that link
was split before (`BrainIds.Hidden`). The same split in two lineages gives the same id, NEAT's
historical marking without a counter. The crossover lines two brains up by these ids.

After any mutation of a central genome's body, `Genome.NumberEdges` gives a fresh id to every edge
without one and to every edge whose id an earlier edge already holds. A duplicated node copies its
edges with their ids, and two edges of one id would give two parts one path id.

## The network

`Brain.For` builds the network when the phenotype carries a brain (`Phenotype.Brain`). Units are the
outputs first, two per degree of freedom in the drive's order, then the hidden units. A step:
1. reads every port that some link reads, once, from this step's senses;
2. sums each unit's bias and its links, ports from this step and units from the step before;
3. puts the sum through `tanh`, and swaps the buffers;
4. writes unit `2k` to drive `k` and unit `2k + 1` to bend `k`.

A unit therefore answers one step after what it reads, and a loop is a memory of one step. A link of
weight 0 is left out, and so is a link whose port the body does not have. The sensor mask holds only
the channels some link reads, so a young brain samples no more than it uses.

The saved state is the two buffers and the clock, written under the same `BRAN` tag as before. A
module rebuild carries the state by unit id.

## Limbs come and go

`CentralWiring.Effective` is the brain a body runs, from the genome's brain and the body.
- An output of a joint the body no longer has is dropped, with its links.
- A link from a port the body no longer has is dropped.
- A joint the genome has no output for copies the nearest joint that has one. The search goes to
  another degree of freedom of its own part first, then up the chain to the root, then to any joint in
  part order. The copy takes the bias and every incoming link of the same kind of output. A link from
  one of the copied joint's own ports moves to the new part's port of the same channel and index,
  and a per-joint index follows the degree of freedom. This is the owner's answer on a new joint:
  follow the recommendation.
- With a founder's stream, and no joint to copy, an output is wired fresh: a bias drawn at
  `founderBiasStdDev` (0 by D147), and a link from each port of the pool with chance `founderLinkShare`.

The world materialises this at every birth and at founding (`World.GrowBrain`), so a child's new
joint is its own to mutate. A module rebuild runs it without changing the genome.

## Mutation

`CentralMutation.Mutate`, after development, because the sources a new link may come from are the
body's ports:
- each link's weight steps with chance `weightChance` by `N(0, weightStdDev)`;
- each unit's bias steps with chance `biasChance` by `N(0, biasStdDev)`;
- one new link with chance `addLinkChance`, from any port or unit to any unit, at `N(0, newLinkStdDev)`;
- one link split by a new hidden unit with chance `addUnitChance`: weight 1 in, the old weight out;
- one link removed with chance `removeLinkChance`;
- one hidden unit removed with its links with chance `removeUnitChance`.

Removal is rarer than addition, so drift does not shrink a brain. `maxHiddenUnits` only guards the
process. Thinking costs nothing: the neural cost is charged on node neurons, and a central genome has
none.

## The settings, and which numbers are guesses

All are in the config's new `brain` group (`CentralBrainOptions`). Adding the group makes every
older `config.json` unreadable by this build, as every new tunable does.

| Setting | Value | Why |
|---|---|---|
| `brainModel` | `PerNode` | The default, so every recorded run replays. `Central` is the new brain. |
| `founderLinkShare` | 1 | The proposal: every port wired to every output from birth. |
| `founderWeightScale` | 1 | A founder link's spread is this over the square root of an output's starting links: LeCun's initialisation for a tanh unit, so an output's sum starts near the size of one input (D147). |
| `founderBiasStdDev` | 0 | D147, the owner's ruling: biases start at 0, so a founder moves only when its senses push an output past the joint's threshold. The first default, 0.5, left about half of generation 0 still at rest. |
| `weightChance`, `weightStdDev` | 0.1, 0.1 | A guess. A step of 0.1 is about one founder weight's spread on a body of a hundred ports. |
| `biasChance`, `biasStdDev` | 0.1, 0.1 | A guess, the same scale. |
| `addLinkChance`, `newLinkStdDev` | 0.3, 0.3 | A guess. |
| `addUnitChance` | 0.1 | A guess, several times the old brain's net growth. |
| `removeLinkChance`, `removeUnitChance` | 0.05, 0.02 | Below the adding rates, by the proposal. |
| `maxHiddenUnits` | 128 | A guard. |

None of these was measured. The nursery run is their first test, and the generation rows record the
brains' size so the growth can be read.

## The genome file

A central genome is written as format 12: its edges carry `id`, the genome its `nextEdgeId`, and a
`brain` object its outputs, hidden units and links. Ids are written as sixteen hex digits, because a
JSON number is read as a double and would lose them. A genome of the old model is still written as
format 11, byte for byte, so every recorded run and every inoculum reads and writes as before.

## The world and the farm

- **Founders** are drawn by the old model's stream, so the bodies are the same bodies, and then
  converted: neurons dropped, edges numbered, the brain wired fresh to the developed body.
- **Births** number the child's edges, develop it, take its brain through `Effective`, and mutate it.
  The draws come from a stream derived from the birth's seed, so the old model's draws do not move.
- **The farm** reads `EVOSIM_BRAIN_MODEL` (`per-node` or `central`). The card refuses the central model:
  its kernel steps the per-part neurons only. The report's perception column counts a central genome
  as perceiving while any link reads a port, which is an upper bound.

## The nursery

- `--founders` draws the bodies as before. Under `brainModel Central` each member of the first
  generation draws its own brain on its body, so generation 0 is a spread of brains. The control's
  generation 0 was each body's one founder brain copied. This is a difference from the control.
- A child is its parent with its brain mutated on the body. Under `--evolve-envelope` it also takes the
  drive genes of a `Mutator` mutant, as before.
- Every hand-wired brain of the exam (frozen, full, the constants, the kinesis) replaces the brain
  whole. On a central genome it drops the one brain and wires the old model's neurons, so the controls
  are the same controls whichever brain was searched.
- A knockout removes the links from every port of the knocked-out channels. That is weight 0, a
  sense read as 0. Brainless removes the links from every port.
- The crossover (D145) is re-based on ids. The route starts at a drawn output of the donor and steps
  back along the incoming link of the largest absolute weight. It stops at a port, at a unit with no
  input, or where only links back onto the route are left. The child takes the route's hidden units as the donor holds them,
  and on the output the route's one link.
- The brain distance for d_ref is the summed weight and bias differences over what both brains hold,
  plus one for each link or unit only one holds. It is a new yardstick, so d_ref is measured again.
- The generation rows gain `brain`: hidden units and links, mean and largest, and the top brain's.

## Tests

`CentralBrainTests` began with 15 tests, and the review added six (below). The first 15 cover the ids, the ports and outputs, the founder wiring, the
network's timing and loops, and the state. They also cover a gained and a lost segment, the split,
mutation, format 12, and the rule that a genome carries one brain or the other. The last runs a world
that founds and breeds central creatures, beside a default world in which none appears.

## The review, and what it changed

An Opus subagent read the whole diff on 2026-10-01 at 23:45. It found nothing that moves the old
model: every new path is guarded by a central genome or the central setting, and none adds a draw to
the old one. It found one blocker and five things to fix, and all six were fixed the same night.
- **Blocker: a sense linked twice.** A dense founder's joint reads every part's senses, a rigid
  part's included. Say the rigid part gains a joint and copies the first. The first joint's link from
  its own sense moved to the new part's sense, and its existing link from that sense was copied too.
  Two links with one source and one target break `Validate`, so the creature's next mutation would
  have thrown and stopped a central world. Now the moved link wins and the other is dropped, and
  `GrowBrain` validates what it grows.
- **A pool founder's brain was mutated.** A pool founder is now checked against its body and never
  mutated; only an empty brain is wired fresh.
- **An inoculant with an empty brain swam silent.** It is now wired to its body on a stream of its
  own. An inoculant otherwise runs the brain it carries in either world, which the nursery's
  hand-wired controls rely on.
- **A neural price is charged to nobody under the one brain**, since the price reads node neurons. A
  central world now refuses a price above 0.
- **The nursery's other evolve modes froze the brain.** A body mutated there now takes its brain
  with it, as a birth in the world does.
- **A mirror twin copied its parent.** A new joint now looks first to a sibling grown from the same
  node on the same parent, and only then walks up the chain. The proposal asked for this: "a repeated
  segment gets a copy of its sibling's weights".

Smaller changes from the same review: the crossover's route ends at another joint's output, whose
units the base keeps, and the crossover respects `maxHiddenUnits`. The nursery's two seed streams no
longer overlap, and the genome reader's error names format 12. Six tests were added, bringing
`CentralBrainTests` to 21. They cover these cases and two things the first tests could not see: each
joint's units driving its own degree of freedom, and state carried to the same unit when an earlier
limb is gone.

**Not changed, and open for a world run:** a dense founder of a large body carries about ports times
outputs links, which by the reviewer's estimate is 70 to 180 KB of genome a birth on a ten-part body.
The lineage file stores a genome per birth, so a central world would write gigabytes an hour. The
nursery's bodies have two parts, and the question waits for the world. D057's brain term in the
species distance reads nothing under the one brain, as the proposal deferred.

## Open

- The theatre and the Dynamics bench do not show the new brain's units.
- The farm's perception column is an upper bound for a central genome.
- Whether the rates above suit a hundred-port body is the nursery run's question.
