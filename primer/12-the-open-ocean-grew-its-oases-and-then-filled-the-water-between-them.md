# 12 — The open ocean grew its oases, and then filled the water between them

[Piece 06](06-the-producers-feed-the-water.md) built a food chain in a tank, and
[piece 09](09-a-mouth-must-touch-what-it-eats.md) gave its grazers a mouth that has to touch what it eats. Both
happened behind glass, and since round 62 the world has had none. It is a square of open water 300 m a side whose
edges join. A body that swims out of one side comes back in at the other ([D204](../DECISIONS.md#d204)). A round is a
set of worlds launched together to answer one question, and a world is one run of the simulation on one seed.

This piece says why the project left the tank and what the open square is for. The aim is separate oases with
near-empty water between them, and swimmers that travel from one to another. The rounds since have found the first
half of that. Oases form, and then the water between them fills. Grazers make a living on their oases and almost
never leave. The world chosen for a film grew until it hit the most bodies the machine can simulate. Over a long run,
the leaves stopped following their island's bloom.

## The tank filled however it was fed

Rounds 60 and 61 ran in a circular glass tank of 88,000 m². Matter, the material a body is built from, arrived only on
a few islands ([D175](../DECISIONS.md#d175)). The water between them, the desert, buried its matter twenty times
faster than the islands did ([D190](../DECISIONS.md#d190)). The hope was that life would gather in separate places.

It did at first. In round 60 the share of the living over the islands was 0.60 to 0.87 at 6,000 simulated seconds.
At the end, 48,000 s, it was 0.34 to 0.38 ([logbook 0143](../logbook/0143-the-crowds-leave-the-islands.md)). Leaves,
the bodies that live on light, were 76 to 92% of crowds of up to 17,325. They had drifted into the desert and lived
there near break-even. Round 61 planted grazers, bodies with a mouth, to eat them down. Nearly all the grazers starved,
and the tank filled again ([logbook 0145](../logbook/0145-the-planted-grazers-starve-and-the-world-changes.md)).
Looking at the last frames, the owner found it crowded everywhere, with no empty water to be seen.

Part of the cause was a rule about matter. A living cell released its spent matter where it stood. A crowd of leaves
could fix its own releases again, so a leaf lived wherever it drifted ([D206](../DECISIONS.md#d206)).

## The glass was not the trap it seemed

The first suspect was the wall, since the owner had seen bands of bodies along the rim in round 61. Before anything
was built, the coordinator, the agent that plans the rounds, counted where the bodies were
([the survey](../logbook/specs/open-ocean-survey.md), section 2). The outer 15 m of the tank is 17% of its floor, and
at round 61's end it held 8 to 10% of the bodies. The crowd sat 30 to 60 m in from the glass, off the islands.
Friction at the glass was switched off, so the wall could grip nothing.

So the case for leaving changed. It came to rest on the owner's standing aim of a world like a real ocean. A real
ocean has no wall for a swimmer to evolve to follow (the survey, section 1). The ruling turned down a larger tank with
a wide ring of desert, because it kept a wall. It also turned down a channel with banks on two sides, which the owner
judged a wall in disguise (D204).

## A square whose edges join

The code already had a box with joined edges ([D077](../DECISIONS.md#d077)), and D204 brought into it what the tank
had gained since. The islands keep well off the edges, so a wide desert lies between each oasis and its own copy
across the join. The theatre, the program that draws a recorded world, shows each body at its copy nearest the
camera. Its fog hides anything far enough off to be a copy, so no seam is ever seen.

The floor lost its tilt, because a tilted plane cannot join at its edges. The shallow places are shoals, rising to
flat tops a few metres under the surface, where the light is strong. The water moves in swirls of several sizes and
layers that follow the floor around the shoals ([its specification](../logbook/specs/open-ocean-current.md)).
Branching coral grows on the islands and roofs part of each, and a little stands in the desert as stepping stones
([D194](../DECISIONS.md#d194)).

## Feed the islands, starve the desert, keep the plants at home

Three rules make an island an oasis. Matter enters the water at the islands and nowhere else. The desert buries what
reaches it. And since round 62 a plant keeps its matter (D206). When a leaf pays its upkeep, the energy leaves as heat
and the spent unit stays in the body. A plant needs new matter only to grow or to make a child. A leaf that drifts
into the desert can live on light but cannot breed there, and breeding happens where the matter arrives.

A body that eats follows a different rule. It releases what its food carried beyond its needs, after its own leaves,
if it has any, take what they can ([D207](../DECISIONS.md#d207)). The decision drew this from a scan of nature, which
found that on reefs the animals' excretion is the main fertiliser ([the scan](../logbook/specs/matter-ocean-scan.md)).
I report that reading and have not checked the scan's sources.

The last setting was the current. The square's first screens, short runs that each move one setting, tried two speeds
([D209](../DECISIONS.md#d209)). Only the slower made oases. At 0.02 m/s, a fifth of round 61's current, three quarters
of the living sat on five islands and four fifths of the 10 m squares were empty. At 0.1 m/s the current carried
island leaves off five to ten times faster.

## Separate patches have a reason in the literature

None of the open ocean's rulings cites ecology on patches, but the project's literature review holds some. In a
laboratory system of mites on plants, one continuous system died within 120 days. Eight small islands joined by
narrow bridges persisted for 393 and over 447 days, with fewer plants in all [JN97 p.7]. Huffaker's trays of oranges
held three full cycles of predator and prey once barriers broke up the dispersal [HUF58 p.39–41]. And a two-patch
model found that identical patches buy nothing, while patches that differ do the work [HZ13 p.5].

These are mites and equations, and the step from them to the open square is mine. Oases with thin water between them
resemble those narrow bridges. Islands that bloom out of step, later in this piece, resemble unequal patches. Neither
likeness has been tested in this world.

## The oases held at first, and then the water filled

Round 62 and its screens made grazers pay their way, a story
[logbook 0147](../logbook/0147-grazers-paid-their-way-once-they-swam-faster-and-grew-up-slowly.md) tells. Its
confirmation run, three new worlds to 24,000 s, gives the clearest picture of the open square at work
([its read](../logbook/specs/r62cf-read/24000/README.md)). Each world had three groves of coral. At 12,000 s a thin
layer of leaves lay under the surface over each grove, with clear water between and nearly empty water below.

By 24,000 s the layers were two to three times as dense and had spread sideways. The read measured the leaves in the
gap between the second and third groves, as a share of the second grove's peak. That share rose from 6 to 12% at
12,000 s to 26, 35 and 44% at 24,000 s. The gap between the first and second groves stayed open. The read's own
inference is that three oases were growing towards two.

The owner later judged that water filling between groves was no bad thing in a world of moving blooms
([D251](../DECISIONS.md#d251)). The aim of separate oases stands, and how empty the water between them must be is left
open.

## The grazers stayed home

The grazers in that run met the project's bar for a healthy guild, and they did not travel. Under 1% of bred grazers,
those born in the world, ever moved between islands. A crossing took a median of 140 to 230 s, and a grazer died at a
median age of 60 to 67 s. The read infers that few lived long enough to make one. Grazers were also found in open
water about half as often as bodies in general.

Fat, a tissue that stores more reserve, was chosen to make a crossing pay ([D247](../DECISIONS.md#d247)). Its first
step showed that grazers already carried enough to cross (D251). What was missing was a reason to leave.

## Islands that bloom on their own clocks

From round 63 each fed island's influx, the rate matter arrives there, rises and falls on a slow cycle of its own
(D251). Each island draws its period, swing and phase from the world's seed. The world's total influx is held
constant, so one island's bloom is another's lean time. Seasons of light had dimmed every island at once. Islands out
of step always leave one rich somewhere.

An earlier study had found that a crowd follows a slowly drifting fountain of matter without crossing open water
([its report](../logbook/specs/r60-patchiness/report.txt)). A fountain that jumps needs a crossing, and out-of-step
islands make the rich place jump. D251 also let a blooming island be smelled from tens of metres off. A leaf's own
smell reaches 6 m, and the gaps between groves are 20 to 30 m.

The first bloom screen set periods of 2,500 to 6,000 s, and the leaves barely followed. Their correlation with their
island's influx was 0.1 to 0.4, against a mark of 0.5
([logbook 0148](../logbook/0148-round-63-the-film-world-fills-and-cheap-mouths-eat-each-other.md)). With periods of
5,000 to 10,000 s the leaves followed on four and five islands of eight, in the arm without the far smell. The far
smell added only one or two crossings. The owner chose the simpler arm as the world for the project's grazer film
([D259](../DECISIONS.md#d259)).

## The film's world grew until the ceiling stopped it

Three worlds of that arm then ran on toward 72,000 s, and none of them got there
([the end read](../logbook/specs/r63d-read/end/README.md)). Each ended at 54,440 to 67,240 s on the population
ceiling of 25,000 bodies, the most the instrument can afford to simulate. Most of those bodies were leaves.

The bloom faded from the leaves as the runs went on. At 48,000 s the leaves followed it on 0, 1 and 0 islands of
eight, and at the end on none. The read's explanation, not yet tested, is that the leaves grew without a check. A
total that climbs through the whole run carries a trend the bloom lacks, and a plain correlation reads that trend as
noise. In one world the matter standing in the water rose from 16,100 units at 24,000 s to 29,600 at the end. More
flowed in at the islands than the desert buried.

The grazers did better. They survived in all three worlds and evolved, and in two their body mass roughly doubled
from the founders'. Grazer crowds followed the bloom on 6 or 7 of the 11 islands the read counts crowds on, lagging
it by 550 to 2,300 s. Crossings between islands came to 250 to 551 a world, rising late in the runs.

The crowd also slowed the machine. A world's step time grows in a straight line with its bodies
([the slowdown read](../logbook/specs/slowdown/README.md)). With three worlds sharing the graphics card, each falls
below real time at about 16,000 bodies. From about 45,000 s these worlds ran slower than the world they simulate.

The owner first answered with a fixed total of matter for the world ([D261](../DECISIONS.md#d261)), and withdrew it
late the same day ([D266](../DECISIONS.md#d266)). A world with a fixed stock had locked up in September and stopped
selection ([D074](../DECISIONS.md#d074)). The balance the owner wants is grazers and plants holding each other in
check, and the grazer phases of [D264](../DECISIONS.md#d264) go after it directly.

## The shortfalls so far share one cause, on my reading

Under a slow current, with plants that keep their matter, the open square grows oases, and grazers make a living on
them. It has not yet kept the water between oases thin through a long run. Nor has it made travel a habit, or held
the leaves to their island's bloom. My reading of the reads together is that each traces to one cause: nothing held
the leaves down. Round 64, the first grazer phase, made animals harder to kill. In one world grazers boomed beside
living leaves for the first time, and the leaves still climbed year on year
([logbook 0149](../logbook/0149-round-64-hard-animals-end-the-killing-and-grazers-boom.md)).

## What here is inference

- **The likeness between oases and the patch experiments** is the author's. [JN97], [HUF58] and [HZ13] studied mites
  and models, and no ruling on the open ocean cites them.
- **One cause behind the fill, the faded bloom and the ceiling** is the author's reading of the round 62, 63 and 64
  reads together. The cause named is that nothing held the leaves down, and each read makes only its own part of the
  case.
- **Few grazers live long enough to cross** is the round 62 confirmation read's inference, from median life against
  median crossing.
- **Three oases growing towards two** is the same read's inference from the leaves in the gaps.
- **The leaves' trend hiding the bloom** is the end read's explanation of round 63's long run, untested. A detrended
  correlation would test it.
- **A real ocean has no wall for a swimmer to follow** is the survey's reasoning in support of the owner's aim, and
  cites no source.
- **Nature's release rule**, that animals fertilise reefs, is D207's reading of the matter scan, reported here
  unchecked.
- **The leaves lived near break-even in the desert** is logbook 0143's reading of its counts, and was not measured on
  its own.

## Where it is

| file | what it holds |
|---|---|
| [`CurrentField.Ocean.cs`](../src/Evosim.Core/Environment/CurrentField.Ocean.cs) | the open square's water: swirls of several sizes and layers, following the floor |
| [`BedShape.cs`](../src/Evosim.Core/Environment/BedShape.cs) | the bed and its shoals |
| [`CoralGeometry.cs`](../src/Evosim.Core/Environment/CoralGeometry.cs) | the branching coral of D194 |
| [`GridField.cs`](../src/Evosim.Core/Environment/GridField.cs) | the water's matter, the islands' influx and its blooms |
| [`DECISIONS.md`](../DECISIONS.md) D204, D205, D209 | the open square, round 62 in it, the slow current |
| [`DECISIONS.md`](../DECISIONS.md) D194, D206, D207 | the coral, and plants that keep their matter |
| [`DECISIONS.md`](../DECISIONS.md) D251, D259, D261, D266 | the blooms, the film's world, the fixed total and its withdrawal |
| [`open-ocean-survey.md`](../logbook/specs/open-ocean-survey.md) | why the tank was left, and what assumed it |
| [logbook 0143](../logbook/0143-the-crowds-leave-the-islands.md), [0145](../logbook/0145-the-planted-grazers-starve-and-the-world-changes.md) | the tank's last two rounds |
| [logbook 0147](../logbook/0147-grazers-paid-their-way-once-they-swam-faster-and-grew-up-slowly.md), [0148](../logbook/0148-round-63-the-film-world-fills-and-cheap-mouths-eat-each-other.md) | rounds 62 and 63 in the open square |
| [`r62cf-read`](../logbook/specs/r62cf-read/24000/README.md), [`r63d-read`](../logbook/specs/r63d-read/end/README.md), [`slowdown`](../logbook/specs/slowdown/README.md) | grazers that stay home, the film's world at its end, the step time against the crowd |
| [`LITERATURE-REVIEW.md`](../research/LITERATURE-REVIEW.md) | [JN97], [HUF58] and [HZ13], in its bibliography and its Q9 row |
