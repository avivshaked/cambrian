# 04 — Nobody decides who wins

Every piece so far has built a creature. [Piece 01](01-a-graph-that-grows-a-body.md) grew a
body from a graph, [piece 02](02-from-a-tree-of-boxes-to-something-that-moves.md) made it move,
[piece 03](03-what-it-means-to-push-against-water.md) gave it water to move through. The obvious
next step — the one almost every project of this kind takes — is to measure how far each creature
swims and keep the best ones.

This one does not. There is no score anywhere in it. Nothing is ranked, nothing is compared,
nothing is selected. Creatures have an energy balance, and a creature whose balance reaches zero
stops. That is the entire mechanism.

This piece is about why that is a better idea than it sounds, and about the four ways the world
tried to cheat once we let it run.

## The problem with scoring

A fitness function is a sentence you write down: *swim far*. It sounds neutral. It is not.

Whatever you write, you have decided in advance what the creatures are for. "Swim far" produces
things that swim far, which is not the same as things that are interesting to watch, and it
certainly is not the same as an ecosystem. There is no reason for a plant to appear in a world
optimising for distance. There is no reason for a predator: nothing in the objective rewards
eating, so eating never happens. Every strategy you might hope to see emerge has to be written
into the objective first, which means it did not emerge.

Worse, the objective is a target and targets are exploitable. If the score is displacement, a
creature that finds a bug in the physics engine and teleports scores brilliantly. The usual
defence is to notice and throw the run away.

**In a world with no score there is nothing to throw away.** A creature that discovers free
energy in the solver does not get a suspiciously high number; it gets *food*, and it feeds its
descendants on the trick forever. That sounds like a much worse position to be in. It is
actually a much better one, because it converts a judgement call into an accounting question:
energy in versus energy out. Sunlight is the only input. Metabolism is the only outflow.
Everything else moves energy around without creating any. If the books do not balance, something
is manufacturing energy, and you know it without having to recognise the exploit.

## What replaces the score

Three rules, and no more:

1. **Everything costs.** Tissue costs to keep alive, in proportion to its volume. Neurons cost.
   Moving a joint costs the mechanical work it actually did. A part that costs nothing is a free
   lever, and evolution takes free levers to the limit every time.
2. **Some tissue earns.** A photosynthetic part converts light into energy in proportion to the
   area it presents and the light available there. Other cell types earn other ways — from
   nutrients in the water, from tissue they touch — which is where herbivores and carnivores come
   from later.
3. **Enough surplus makes a copy.** A creature above a threshold pays energy out of its own
   reserve to produce mutated offspring. How much it gives each one, and how many it makes, are
   heritable traits rather than settings.

Nothing in that list mentions swimming. Whether swimming is worth doing is a question the world
answers, not one we answer for it. And a creature that just sits there is not "efficient" — it
is paying upkeep and earning only what falls on it, which may or may not be enough. Doing nothing
is a strategy with a price like any other.

## The first result: there is a largest plant

Before running anything, the arithmetic already says something that nobody put in.

Income scales with **surface area** — light falls on the outside of you. Upkeep scales with
**volume** — you have to keep the inside alive. Double a body's size and its area goes up four
times while its volume goes up eight. So the ratio of what it earns to what it costs falls as it
grows:

| half-extent | income/upkeep |
|---|---|
| 0.05 m | 6 |
| 0.15 m | 2 |
| 0.3 m | 1 |
| 0.6 m | 0.5 |
| 1.0 m | 0.3 |

Below 1, the body cannot pay for itself on light. **There is a largest creature that can live on
sunlight, and its size is set by geometry rather than by anything we chose.** Real biology has
the same constraint and solves it the same way — this is why photosynthetic things are small, or
thin, or both, and why a tree is mostly dead wood with a thin skin of living tissue on the
outside.

That is the kind of result worth building a world for: not something the simulation was told,
but something that falls out of it.

## And then the world cheated, four times

Running it was where the interesting part started. Every one of the following was working exactly
as specified, and every specification was wrong.

### The sun was infinite

The plan was to find one number by experiment. Somewhere there is a ratio of metabolism to
photosynthesis where a population holds steady — too low and nothing lives, too high and
everything becomes a mat of contented plants. You do not have to guess it; you sweep it, and the
transition announces itself.

There was no transition. Over a 400× sweep, every setting either went extinct or grew without
limit.

The reason turned out to be structural. A creature's income depended on its own depth and on
nothing else — in particular, not on how many other creatures existed. So every creature above
break-even accumulated surplus at a fixed rate and bred on a fixed schedule *no matter how
crowded the world got*. That is a population with births and no brake: it doubles, and doubles
again, forever. The knob we were sweeping only ever decided how fast.

What was missing has a name in ecology: **density dependence**. Nothing in the world made one
creature's existence cost another anything.

The fix is the physically honest one rather than a crowding penalty with a coefficient. **The sun
is finite.** The world is a certain number of square metres wide; it receives that many square
metres' worth of sunlight and not one watt more; and light that one creature absorbs is light
that never reaches whatever is below it. Total income across the entire world is now capped by a
conservation law, whatever evolution discovers.

Carrying capacity stopped being a number we picked and became a consequence of the world having a
size. And the transition appeared immediately, sharp and repeatable — and landed exactly where
the hand calculation for a single break-even plant said it would, which is the point at which
either is worth believing.

There is a second result here, and it was not designed either: **more light produced bigger
creatures, not more of them.** Once light is contested, a large body is worth having because it
shades its competitors.

That one turned out to be a symptom rather than a finding — see *the corpse that was worth
nothing*, below.

### Bodies grew until the arithmetic broke

With light finite, populations settled — and body sizes ran to 10¹⁸ metres.

Mutation nudges a body dimension by an amount proportional to that dimension, which makes size a
random walk *in the exponent*. Such a walk has no resting place; it wanders to the extremes and
stays there. The design relied on that, at one end: a part that wanders small enough is deleted,
and that deletion is the only thing that ever removes a node from a genome. It is a deliberate,
load-bearing mechanism.

Nobody had noticed there was no matching mechanism at the other end. So a bound was added — and
it did not help.

### Volume does not bound surface area

The reason is the most interesting bug in the project so far.

The economy pays for **area** and charges for **volume**. So the cheapest possible way to earn is
to be *thin*. A sheet with the volume of a small cube can have the surface area of a car park.
Bounding volume does nothing to stop it:

| thickness (volume held constant) | income/upkeep |
|---|---|
| 0.3 m | 1.0 |
| 0.1 m | 1.4 |
| 0.03 m | 3.5 |
| 0.01 m | 10.1 |

Evolution found this within a few thousand births and took it as far as the arithmetic allowed:
shadows of 10³⁷ square metres in a world four hundred square metres wide.

This is a free lunch that *our own accounting handed over*. It is also, at a sane scale,
completely correct biology — a leaf is a thin sheet for exactly this reason, and so is a kelp
blade. What real tissue has and this model lacks is a cost that scales with area, which is why
leaves have a minimum thickness rather than being infinitely thin.

So the obvious fix is an area-proportional upkeep term. **It was worked through and rejected,
because it does not work.** Income and an area cost both scale linearly with area — so their
difference also scales linearly, and thinness is still unbounded. Turning the coefficient up
makes thinning never pay; turning it down makes it free. There is no setting in between. What
actually bounds a body is that the world's light runs out, which was already true. A floor on
thickness was added instead, purely so the numbers stay representable on the way there.

Not adding the plausible fix is the entire value of having checked.

### Two of the guards had the hole they existed to catch

The measure of "is this world running itself, or are we propping it up" was: does any creature
alive still date back to the world's seeding? If none does, everything here got here by being
born.

It is a true statement. It is also unreachable — because nothing dies of old age. A plant whose
income covers its upkeep simply never dies, so a handful of immortal originals pin that measure
at zero permanently. Worlds that had been self-sufficient for hours, with lineages a hundred
generations deep and births balancing deaths, were being reported as propped up. **The instrument
was measuring immortality, not dependence.**

And the test whose whole job is to catch a setting that never reaches the thing it configures —
written because that has happened twice here before, and deliberately automated so it could not
be forgotten — was checking two of the four objects it was supposed to. The new size bound sailed
straight through it.

A guard whose coverage you never test is a guard you are trusting on the strength of its
intentions.

### The corpse that was worth nothing

The rules at the top of this piece have a hole in them, and it took building the rest of the food
web to see it. **Everything costs — except being born.**

A parent paid its offspring a starting stake and a fixed fee. That was the whole price, the same
for a mote as for a whale. Making a body was free; only keeping it alive cost anything, and that
bill went to the offspring, later. In anything real, building tissue is the *dominant* cost of
reproduction — it is why an egg is expensive and why having many is a trade against having big
ones.

The matching hole is at the other end. When a creature died, its body went nowhere. So there was
nothing for a scavenger to scavenge, and the whole detritus half of the world had no fuel.

Both are fixed by one number, and it has to be one number: **what a cubic metre of tissue is
worth is also what it costs.** The parent pays it to build the body; the world gets it back as
detritus when that body dies. If those two figures ever differed, then birth followed by death
would create or destroy energy — a free lunch put there by us rather than found by evolution,
which is the one thing the whole no-scoring approach cannot survive.

And that closed the loop the piece opened with. The world's accounts are now an equality across a
whole food web: **what came in as sunlight, minus what metabolism burned, equals exactly what is
standing** — energy in creatures' reserves, plus energy locked in their bodies, plus dead matter
drifting in the water. Measured residual: 0.0000%.

It also explained the "more light makes bigger creatures" result above, which was not the finding
it looked like. Bodies grew because they were free; the only limit on size was shading, and
shading rewards being large. With tissue priced, the same worlds hold **many small creatures
instead of a few enormous ones, at the same total biomass** — and the total shadow the population
casts fell from 290 times the world's own area to a little over one.

That last number matters more than it looks. Nothing measured the world and forbade a giant. A
giant simply became unaffordable to build.

## The world answered, and the answer was no

Rule 1 above says *moving a joint costs the mechanical work it actually did*. When this piece was
first written, that was a specification rather than a fact.

The economy had a work term and the physics simulator computed joules of actuation, and the two had
never been introduced. Every call site that charged a creature for moving passed literally zero.
Two simulators, running the same creatures, with no seam between them — and nothing failed, because
a bill of zero is a perfectly valid bill.

Joining them is one method: the simulator hands the world a measured joule count and a height, and
the world still knows nothing about a solver. (The one-way direction is not fastidiousness — the
economy has to remain testable in a second, outside the editor, which is the rule that has kept it
honest since piece 01.) The moment that seam existed, *"whether swimming is worth doing is a
question the world answers"* stopped being rhetorical.

**The world answered no. Twice, for two different reasons, and both times the economy was right and
the world was incomplete.**

The first is [piece 05](05-a-brain-that-is-copied-with-the-limb.md)'s opening: every creature was
driven by the same test sine, so the controller did not vary across the population and a uniform
flap produces no thrust. A real cost against an unreachable benefit. The world deleted every joint
it had, inside a minute.

The second appeared once that was fixed. A creature with a brain but no senses swims in whatever
direction its body dictates and cannot tell up from down. Moving up earns light, moving down loses
it, undirected the two cancel — and the work is billed either way. So the surviving jointed
creatures are exactly the ones whose joints barely move, which is what the ledger should do, given
a world where locomotion cannot be aimed.

This is the shape of the whole approach, and it is worth being plain about the cost. A fitness
function would have declared swimming worth twelve points and creatures would have swum — badly at
first, then well. Here, **a capability is necessary and nowhere near sufficient: a trait pays only
if the world contains a route from the trait to energy.** Twice now that route has been missing,
and each time the only signal was the trait quietly disappearing.

Getting a *no* out of a world with no score is slow, and it is not ambiguous.

## What this buys

The world now regulates itself. Populations rise, contest the light, and settle. Lineages run a
hundred generations deep without anything topping them up. Nothing was ranked and nothing was
selected, and there is still a clear difference between a body that works and one that does not.

It also gives up something real. A fitness function tells you, instantly, whether today's run was
better than yesterday's. This gives you no such number, and *is the world alive* turns out to be
a far harder question to answer than *did the score go up*. Three of the four problems above were
invisible in a population count: a world doubling every twenty minutes and a world in balance look
identical if you only sample one of them for long enough to be reassured.

The thing that made them visible was insisting the books balance.

## Sources

| Key | Used here for |
|---|---|
| `[K12]` | Recursive-graph creatures with a minimum part size, and the instability that motivates one |
| `[C18]` | Energy as something a body spends, and the observation that minimising cost with no reachable strategy produces "efficient nothing" |

**Mine, not the literature's:**

- **The whole endogenous-selection framing.** No paper in the corpus removes the fitness function
  entirely; this is the project's own choice, recorded in
  [`DECISIONS.md` D017](../DECISIONS.md#d017), and it should not be presented as literature-backed.
- **The surface-area-to-volume argument for a largest plant.** Standard biology, but the numbers
  in the table are this implementation's, measured by a test.
- **Light competition as the source of carrying capacity.** The shading maths is textbook optics
  applied to biomass; the argument that it is what the design was missing is ours, and was
  arrived at by the sweep failing (see [logbook 0011](../logbook/0011-the-sun-was-infinite.md)).
- **The rejection of an area-proportional upkeep term.** No source discusses it. The argument is
  ours and is given in full in [`DECISIONS.md` D024](../DECISIONS.md#d024) so it can be checked.
- ~~**"More light makes bigger creatures, not more of them."**~~ Withdrawn: it was an artefact of
  bodies being free to build, and reverses once they are not.
- **"A capability is necessary and nowhere near sufficient."** The author's inference, drawn from
  two measured failures rather than from any source; the second of them is argued in
  [piece 05](05-a-brain-that-is-copied-with-the-limb.md) and recorded in
  [logbook 0017](../logbook/0017-what-a-muscle-costs-to-own.md).
- **Tissue cost and corpse worth being the same number.** The conservation argument is ours
  ([`DECISIONS.md` D026](../DECISIONS.md#d026)). That reproduction is dominated by the cost of
  building tissue is ordinary biology, but no paper in the corpus models it.

## Where it is

- [`Metabolism.cs`](../src/Evosim.Core/Ecosystem/Metabolism.cs) — what one creature earns and spends in a step
- [`World.cs`](../src/Evosim.Core/Ecosystem/World.cs) — the loop: earn, spend, breed, starve, and `Observe`, the one-way seam the simulator pushes measurements through
- [`Ecosystem.cs`](../unity/Assets/Evosim/Sim/Ecosystem.cs) — the other side of that seam: physics steps, the metabolic clock, bodies built and destroyed as creatures are born and die
- [`LightField.cs`](../src/Evosim.Core/Environment/LightField.cs) — the finite sun, and who shades whom
- [`NutrientField.cs`](../src/Evosim.Core/Environment/NutrientField.cs) — dead matter in the water, sinking, and what is left after everyone has fed
- [`CellType.cs`](../src/Evosim.Core/Cells/CellType.cs) — what a part is made of, and how that decides how it earns
- [`CalibrationSweep.cs`](../src/Evosim.Core.Tests/CalibrationSweep.cs) — the experiment, and the two tables above
- [`DESIGN.md`](../DESIGN.md) §5A — the specification, and §5A.7 for the failure modes this is watched for
