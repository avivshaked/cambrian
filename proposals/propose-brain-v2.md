# Proposal: one brain per creature (brain v2)

Status: ruled (D146) and being built, 2026-10-01 night. The owner's rulings are recorded below as they
were given. What was built, and which of its numbers are guesses, is
[`logbook/specs/brain-v2-build.md`](../logbook/specs/brain-v2-build.md); the literature's first pass is
[`research/brain-v2/pass1-candidates.md`](../research/brain-v2/pass1-candidates.md).

## Why

The nursery's control arm (`pilot-deep-own16`) ended its search with a best brain of one neuron and no
inputs at generation 90: a blind pacemaker. Four facts of the current brain explain it.
- Mutation adds a neuron to a part at 0.05 a birth and removes each neuron at 0.04. That balances near
  1.25 neurons a part, whatever would be useful; the figure is the agent's calculation from the rates.
- No mutation adds an input to a neuron; an input can only be rewired, at 0.05 an input.
- A part's outputs are its first neurons by position, so deleting neuron 0 makes the bend neuron the power.
- Neurons cost nothing (`neuralCostPerNeuronWatts` 0), so the neural cell's discount buys nothing.

A brain that cannot grow cannot learn to steer to food. The owner said on 2026-10-01 that the work was trying to get
creatures to evolve in a way they simply cannot. A joint reflex, they said, can make a creature jump when it is bitten,
but is not meant to guide it to food.

## The owner's rulings so far (2026-10-01, night)

1. **One brain per creature, and no reflex neurons in the parts for now.** The owner ruled to remove
   all reflex neurons for now and to give each creature one brain, which can start very small and grow
   over generations if that pays.
2. **Best chance first; thinking is free until learning is proven.** The owner ruled that the
   creatures start with the best chance to succeed and learn, which means thinking is free for now, and
   the cost is calibrated later once it is proven that the creatures learn and use their brains.
3. **The brain is nowhere in the body.** On the agent's note that brain placement no longer evolves,
   the owner agreed that every creature gets a "magical" brain that exists nowhere specific.
4. **Rhythm is the actuator's, not the brain's.** On the agent's suggestion to keep oscillating neurons,
   the owner disagreed: the oscillation should be the action and not the trigger, so that
   something triggers an oscillator actuator that propels.
5. **Limbs come wired.** The owner ruled that every evolved limb, joints included, is evolved already wired as inputs to
   the brain, so a mutation that adds a limb gets those inputs and a creature that loses a limb loses
   those inputs. The same holds for outputs.
6. **Senses belong to the brain, not the joint.** The owner said to also consider what inputs make sense for a
   reflex, and that smell is probably not right for the joint.

## Design

Code locations are from a survey of `joint-drive` at `8d7daba`. Anything marked as inference is the
agent's reading, and was not measured.

### The brain

One network per creature, held in the genome beside the body and not on any node. It has three kinds of
unit.
- **Input ports.** One per sense the world offers, on every part. That is each channel of the run's sensor
  pool, at each of its indices: a joint's degrees of freedom, a flow's three axes. The drive sense of D144 is three
  more channels, so it is wired the same way.
- **Output units.** Two per degree of freedom of every joint: the power and the bend of D135. They are
  fixed in number by the body and can never be deleted. Today a part's outputs are its first neurons by
  position, so a deleted neuron 0 turns the bend into the power (Brain.cs:277-297); this ends that.
- **Hidden units.** None at the start; mutation adds them.

A unit is plain: a weighted sum of its inputs and a bias, through `tanh`. The power reads the positive half
as D139 rules. There are no oscillators and no operator set. The rhythm is the actuator's: the brain sets
how hard a joint strokes, and the joint's stroke makes the push (D135).

**Loops are proposed, awaiting a ruling.** A unit may read any unit's value from the step before,
including its own. That is the brain's only memory. Without it a brain answers the present instant only,
and cannot tell smell rising from smell falling.

### Wiring, and the best chance

Every input port is connected to every output unit from birth. A founder's weights are drawn small and
random; the rest of the brain starts empty. Evolution then only has to strengthen a connection that
exists, where today it has to rewire one of a handful of inputs by luck (5% an input).

**A limb brings its wiring.** A body that gains a part gains that part's input ports at weight 0. A
mutation that adds a limb thus leaves a working brain as it was. A body that gains a joint gains its
outputs. **Proposed, awaiting a ruling:** the new outputs copy the weights of the parent joint, or of the
nearest joint when there is none. With weights of 0 the new joint would stay silent until a second mutation found
it, the two-step valley D019 kept the per-part neurons to avoid. A body that loses a part loses its ports
and their weights.

### Growth

Mutation on the one network, with the rates as tunables:
- **Weight steps**, each weight with a chance, so a weight at 0 can become a reading.
- **Add a connection** between any two units, a loop included.
- **Add a hidden unit** by splitting a connection. The old weight is kept on the way out and the way in
  is 1, so the brain behaves almost as before until the unit is tuned. A unit through `tanh` is not the
  identity, so the split moves the brain a little (inference).
- **Remove** a connection or a hidden unit, at a rate well below adding, so that brains grow when that
  pays and drift does not shrink them. Outputs and ports are never removed by mutation.

Thinking costs nothing (ruling 2). It already does: `neuralCostPerNeuronWatts` and
`neuralCostPerConnectionWatts` are 0 in round 54's config and in the nursery's. The neural cell's discount
therefore buys nothing, and the cell is parked: no longer drawn into new bodies, kept in the registry so
that recorded runs load.

### Identity: the hard part, and why the nursery goes first

Weights have to stay on their limb when the body changes elsewhere. Nothing in the genome identifies a
part across a mutation (no id on `MorphNode`, `MorphEdge` or `PhenotypePart`, PhenotypePart.cs:260-266).
The developer's part path, a list of edge and mirror steps (Developer.cs:45-52), is the only identity.
It shifts when an earlier edge is removed, and node indices shift when a node is pruned (Mutator.cs:660-696).

The proposal is staged so this does not block the test.
1. **Stage 1, the nursery.** Its bodies never change, so the ports are a fixed list and no identity is
   needed. Stage 1 asks the question that matters first: whether a brain that can grow, with every sense
   wired, learns to use the senses.
2. **Stage 2, the world.** Each node and edge gets a permanent id, given at creation and carried through
   every copy. A port is keyed by its part's path, written in those ids. Mirrored parts (a left and a
   right fin) are separate parts with separate ports, since steering needs the difference between them.
   A repeated segment gets a copy of its sibling's weights. Waves along a chain would come from that copy
   and the stroke; whether that is enough is open.

### What stays, what goes

- **The old brain stays as a setting** (`brainModel`, the per-node brain by default), so every recorded
  run replays and every stored genome loads. The new brain is genome format 12; format 11 genomes load
  under the old model only.
- **Unchanged:** D135's drive and envelope, D139's power reading, D144's drive sense. The outputs are the
  same per-degree-of-freedom arrays `EffectorDrive` reads now (EffectorDrive.cs:253-300).
- **Superseded for the new model:** D019's per-part neurons and its location argument; D030's output by
  position; D081's "thinking must have a location", since the new brain is placeless by ruling 3; and
  DESIGN §4.3's operator set and oscillator founders. D082's prices wait at 0 until learning is shown.
- **Re-based later:** the nursery's crossover (D145) and D057's brain term in the species distance. The
  crossover becomes simpler: two brains on one body have the same ports, so their weights line up.
- **The card:** the gpu engine already refuses the bend-and-power drive, so the new brain is cpu-only.
- **Cost of reading every sense** (inference): every channel of every part is sampled each step, where
  today a body samples only what it reads (CreatureSenses.cs:141-150). Ports whose weights are all 0 can
  be skipped, which keeps the cost near today's while a brain is young.

### The nursery test

The control's command (`pilot-deep-own16`): the same six bodies, water, score and 100 generations, with
the new brain. It is read against the control's exam per body.
- **Read:** the knockout of each sense costs a clear amount (two standard errors of the paired difference)
  on at least one body. The scent first, since the food is what a brain is for.
- **Worth it:** the best brain beats its body's best constant power by two standard errors, the bar the
  old brain has not cleared.
- **Grown:** the hidden units and connections each generation, to show whether growth happened and paid.
- **A brain analyser**, from the replay of stored episodes, to say what a winning brain does.

### The literature

The review holds no source comparing central and distributed controllers (Q5, "Partial", line 151). It
holds none on growing a network's structure by mutation either: [K12] is described as NEAT-based, but no
NEAT paper is held. A review round should add the NEAT line: complexification from a minimal network, and
historical markings for crossover. It should also look for where rhythm lives in evolved swimmers. Until then the design's reasons are the owner's rulings and the nursery's measurements.

### The owner's answers (2026-10-01, night)

1. Loops: the owner said yes. A unit may read any unit's value from the step before, its own included.
2. A new joint's outputs: the owner said to follow the agent's recommendation. They copy the parent joint's weights, or the
   nearest joint's.
3. The owner said to build first, and that research can happen while tests or the nursery run.

And the owner set the goal the same night: build the proposal, review it, perhaps collaborate with another subagent
on the review and fix, then build the proposal into the world and into the nursery, review the build, run
relevant tests, fix, and repeat until the agent is happy, then run a few generations (a smoke) in the nursery. Bake the
breeding proposal into the nursery as well, then run a nursery run with the new brains and breeding.

On the card, the owner wondered whether 5k-7k creatures, each with a brain, would be better run on the GPU, said
the CPU and GPU need measuring against each other, including the GPU round trip, and asked whether all the state, or
most of it, should be moved to the GPU. The measurement is queued for after the card's rest (HANDOFF: no heavy work on it until about
2026-10-06). The new brain is cpu-only until then.
