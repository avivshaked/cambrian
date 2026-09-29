# Proposal: a nursery for brains

*Drafted for the owner on the night of 2026-09-29. That evening the owner asked whether brains
had evolved. On hearing that they had not, the owner asked whether a place outside the ecology could
evolve them faster, and whether that would be fair. Nothing here is built. The file is absorbed
into DECISIONS.md and deleted once the owner has ruled.*

## The question

No brain has evolved in any sense that matters. Take round 52 seed 1 at 30,000 s. The median
genome carries one neuron and the ninetieth percentile four. Of 3,431 genomes, 1,348 carry none and
718 read a sensor at all, mostly a joint's own angular velocity (463) and the chemical sense (291).
The fastest line reached 48 generations.

A brain that finds food is three things in a row: a sense, a neuron that reads it, and a stroke
the neuron shapes. Each is worthless without the other two. In this world a stroke has never paid
for itself either. DESIGN §0r gives the reason: a body draws from a shared cell, so it neither
empties its own water by staying nor refreshes it by moving. Round 54's foraging rules make a
stroke move a body for the first time. They do not make the three pieces arrive together, and 48
generations of selection that cannot see any one of them will not assemble them.

The owner's proposal is a nursery. A reward function selects brains there directly and fast, and
the results go back into the ecology. The owner also asked whether that is fair, meaning whether it
can be argued to be the ecology's evolution compacted.

## The answer

Not in full, and the proposal does not claim it. A reward function is breeding. The nursery removes
the population, the competition, the water that changes under the bodies, drift, and the rule that
a bad body dies rather than scoring low. Those are what make the ecology slow and sometimes unable
to find anything. So a nursery result is a best case for the ecology, and it cannot say the ecology
would have found the same brain.

It can say two things, and both are worth having.

1. Whether this genome can encode a forager at all, and how far one is from our founders. The
   nursery counts the generations of direct selection from snapshot bodies to the first forager.
   If the answer is thousands, the ecology's 48 were never going to get there. That would be a
   fact about the model.
2. Whether a brain, once it exists, survives natural selection. The nursery's winners go into a
   round as inocula, as a species arriving from elsewhere does. Beside them go the same bodies
   with the sensor links cut. From the moment they land the test is entirely the ecology's, and
   if the wired bodies outgrow the cut ones, the brain pays on the ecology's terms.

DESIGN §8 already makes room for this. It demoted directed search when the ecology became the
selector. It also says the demoted design "remains correct *if* directed search is ever restored —
for a benchmark, or to seed a population the ecosystem then has to keep alive." The nursery is the
second of those.

## Two choices that bring it closest to the ecology

The first is the score. It is the ecology's own currency: a body's net joules over an episode,
from Core's `World` and its ledger with the stroke cost switched on. The water's corpses, snow and
current are sampled from a real run at a real second. The nursery then selects on what the ecology
selects on, and holds only the competition and the changing water still. A score of distance to
the corpse would be our preference, and it would pay a body that reaches the corpse and never eats.

The second is the material. The genome, the mutation and the bodies are the ecology's own. Founders
are genomes from a real snapshot, and children come from `Mutator.Mutate` at the round's own rates,
so the nursery searches the space the ecology searches and no other.

## What the nursery is

- An episode is one body in a small tank under the real world's rules. It uses the same `World`,
  the same physics in `Evosim.Dynamics`, and round 54's push and scent. Its water is seeded from a
  real run's fields. The corpses are placed at random each episode, so a brain cannot learn one
  layout. A genome swims four episodes and is scored on their mean.
- The selection is DESIGN §8 as written, which kept the parts "that would be easy to get wrong
  twice". MAP-Elites cells compare a new body only with bodies like it (§2). Parents are drawn in
  proportion to their score (§8.2). There are two descriptors, the body's part count and one of
  its behaviour (§8.3).
- The exploit check is DESIGN §5.3's: no body faster than one body length a second, which is the
  foraging bench's item 5. The ledger is a check too, since a brain that burns more than it finds
  scores below one that sits still.

## What it costs

The foraging bench swims a three-part body for 600 simulated seconds in 0.39 s on one thread, so
a 900 s episode takes about 0.6 s. A sixteen-link body costs perhaps five times that, an estimate
still to be measured. At four episodes a genome, 200 genomes over 100 generations is 80,000
episodes: about 2 hours on 8 threads for small bodies and about 8 for the largest. That is one
night at the load the owner allows, or less.

A surrogate of the inputs and outputs without the physics would be faster still and is not
proposed. A brain's output is a torque, and what a torque does depends on the body. On the bench a
lone tail on this drive strokes off to one side at every starting phase, and one side fin spins
its body in place. A brain evolved on a surrogate would be trained for a body that does not exist.

## The fair test in the ecology

A round carries nursery brains as inocula, each beside its own knockout: the same genome with
every link from a sensor to a neuron cut. The two start in equal numbers at the same places. The
reading is the wired lines' share of their pair's descendants at the round's end. A second reading
is whether the wired lines keep their sensor links through inheritance. A brain the ecology does
not pay for decays by mutation within a few dozen generations, and that decay is itself an
answer.

## Risks

- A frozen world rewards a brain for a world that stays still. The ecology's water changes under a
  crowd and the nursery's does not. The random placement and the sampled water are the guard, and
  the fair test above is the check.
- The score can be gamed through the ledger. A body that grows large and lives off its reserve for
  an episode scores without foraging. The episode's length and the knockout control bound it,
  since the knockout has the same body and the same reserve.
- The nursery's results could be read as the ecology's. Every entry that reports one says which
  it is. The round that carries inocula is read on the knockout comparison alone.

## What the owner rules on

1. Whether to build the nursery, as a program beside the farm (`src/Evosim.Nursery`, reading and
   writing the same formats), after round 54's foraging rules are benched.
2. The score: net joules over an episode (recommended), or offspring over a longer one (closer to
   the ecology's own measure, and noisier and slower).
3. What evolves: the brain alone on fixed snapshot bodies (recommended first, since it answers the
   brain question without a body changing under it), or brain and body together.
4. Whether a later round carries the nursery's winners as inocula beside their knockouts.