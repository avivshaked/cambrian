# Proposal: world rules that make living take doing, starting with fountains that move

*A draft for the owner, written at the coordinator's request, 2026-10-23, after the fast-rot screen (D217, D218) and
D219. Every rule here is a world rule and the owner's to rule. Nothing runs before D219's screen is read, since the
owner ruled that nothing else in the world changes until round 62's dials are settled (`HANDOFF.md`). Every claim
about the code names its file. What is a guess is marked as inference.*

## The proposal in one paragraph

In today's world a body can live well by doing nothing. Leaves sit in the light over a fed island, fix enough to pay
their upkeep and breed, and the crowd grows until the population ceiling stops the run. I propose two rules that make
staying put a losing strategy for anything that eats, without making it one for plants, which in a real ocean do sit
still. The first is a build. Each island's fountain of matter has a lifetime, switches off, and opens again somewhere
else. An oasis is then born, lives and dies, and a lineage that stays with it dies with it. The second needs no build:
a deeper winter, so the lean season bites. A third rule, crowds that pay for their own light, is set out and held. A
search for swimmers in the latest worlds found none. Two cheap instruments should say first whether the drive caps
their speed or swimming does not pay, since the answer decides which remedy helps. Each rule is aimed at the owner's
picture of the ocean: separate oases, near-empty water between them, and swimmers that cross to graze or hunt.

## What the screens found, read carefully

The two worlds of the fast-rot screen with the richer dose (D218) ended on the population ceiling of 25,000 bodies.
Their stop message says light is covering upkeep so completely that nothing has to do anything. That sentence is fixed
text the farm prints at every ceiling stop, written with D021 (`src/Evosim.Farm/Program.cs`). It is not a measurement
of these worlds. The numbers behind it do support it. The table below, from the reader's tables in
`logbook/specs/r62-rot-screen/` on the `r62-launch` branch, shows what the crowd was made of at the last second both
worlds reached. Look at the leaf column: four bodies in five were leaves.

| World | Alive | Leaves | Absorbers | Grazers | Living on the islands | Bodies seen crossing between islands in the year | Of them, grazers |
|---|---|---|---|---|---|---|---|
| r62f-h30m-6201, 20,310 s | 24,987 | 20,601 | 2,807 | 1,046 | 0.65 | 21 | 0 |
| r62f-h30m-6202, 20,310 s | 22,759 | 19,626 | 1,932 | 611 | 0.71 | 10 | 0 |

At round 62's own dose the crowd was smaller but still climbing fast. Its four fast-rot worlds held 7,000 to 13,600
bodies at 24,000 s. That is one and a half to twice what they held 6,000 s earlier (the same tables). I expect them to
reach the ceiling too, given time (inference). Across all six worlds, the bodies counted crossing from one island to
another in the last year were nearly all leaves drifting with the water. At most one a world was a grazer.

The problem has two halves. The leaves never run short, because the fountains feed the same places for the whole run.
And nothing that eats has a reason to leave the oasis where it was born.

## Nothing swims yet, and two readings could explain it

The theatre agent searched for swimmers over the last 300 s of all twelve worlds of round 62 and the fast-rot screen,
and found none. It took each body's movement over the local drift of the water out, and compared bodies with joints to
bodies without. Its tables are in `scratch/r62-swimmers/search-all.txt`, and should move under `logbook/specs/` if a
decision comes to cite them. Look at the first two rows of the table below: a body with no joint, which cannot push
itself at all, goes as far as one with joints.

| Measure, over 300 s | Jointed bodies | Bodies with no joint |
|---|---|---|
| Median distance beyond the drift | 1.2 to 2.9 m | 1.4 to 2.4 m |
| The best body in a world | 15 to 25 m, 0.05 to 0.1 m/s | the same |
| The water's own speed | 0.02 m/s | 0.02 m/s |

The bodies with no joint set the measure's floor. What moves them is sinking, bumping, and the error in the drift
taken out (inference). The top movers with joints are biters: consumer tissue and one link on a spherical or hinge
joint. Many bodies wander far and arrive nowhere. One jointed body in `r62-h6202` covered 117 m of path in 1,200 s and
ended 5.9 m from where it began.

Two readings fit, and they may each hold for different bodies.

- The first reading is that the drive caps the speed. Under D135 a stroking limb is pushed by a rule whose stroke rate
  is fixed at 2 radians a second, whatever the genome says (`src/Evosim.Dynamics/Fluid.cs`). A limb's Power gene does
  not scale the push (D135's third ruling), so no gene makes a stroke faster. Speed grows only with a limb's volume
  and reach against the drag on the body's front. The bench that set the rate found founders at full power swimming
  6.2 to 7.8 cm/s (D135). The best bodies in the search move 5 to 10 cm/s. So the best are at about the speed the
  drive allows, which points to a cap for them (inference: their stroke powers were not read against it).
- The second is that swimming does not pay. The median jointed body goes no further than a body that cannot swim, so
  most jointed bodies are not stroking toward anything. The nursery's foragers, bred on their energy return, cut their
  stroke to about a tenth of their seeds' (`HANDOFF.md`). There, at least, a stroke cost more than it brought in. In
  the open water, food that never moves and light that pays where a body already is give a stroke little to buy.

Two instruments tell them apart, and both are agent work.

1. The first is a ceiling bench. The search's top movers go into the swim probe (`src/Evosim.SwimProbe`) in still
   water, each run under its own brain and again with every power held at full, as D135's swim test ran a plain
   stroke. If the two speeds are close and near 7 cm/s, the brain is already using what the drive gives. The bench
   then runs once more at a stroke rate of 4 radians a second. If speed about doubles, the drive law sets the ceiling.
2. The second is a stroke knockout. Cousins of a world are resumed from a checkpoint with the stroke's push switched
   off for every body, beside an unchanged cousin, for a year. If the jointed lines' intake and births do not fall,
   the stroke does not pay them anything. The nursery's knockout exam is the pattern. The resume with changed settings
   allows only the economy, mouth and cell-type groups today (`HANDOFF.md`), so the knockout needs that list widened
   by an hour or two of agent work.

The answer decides what helps. If the cap binds, the stroke rate is a world dial, and the owner's to rule. If swimming
does not pay, the rules below are the remedy, because they give a stroke something to buy. For the fountains' rule,
the best bodies are already fast enough. At 0.07 m/s a body crosses the 90 m of desert that D204 set between an oasis
and its copy in about 1,300 s. That is inside the 3,000 s base of the age hazard, if the body holds a course.

## What the world already has

Most of the levers a lean world needs are built. The rules below change how they act over time and space rather than
adding new ones.

- Matter arrives on fed islands, at a fixed rate in the top 12 m of the island columns, and is buried at each column's
  floor (D175). Round 62's dose is 3.6 units a second, with 45,000 units to start (D187). A richer dose dissolves the
  oases (D186, D187), and 1.5 times it ran into the ceiling (D218).
- The open water is a desert. Matter outside the islands is buried at 0.02 a second, twenty times the islands' rate
  (D190, D191). So the open water stays below what a leaf needs to grow.
- Plants keep their matter. A plant's upkeep spends energy and keeps the unit in its body (D206, D207). A leaf that
  drifts into the desert lives on light but cannot grow or breed there. Breeding happens where the fountains are.
- The light has seasons and weather. A year is 6,000 s and a day 1,000 s. Each year has three days of summer at 1.6
  times the mean light, then three days of winter at 0.4, with day-to-day weather on top (D151).
- The ocean current runs at 0.02 m/s, the speed at which the oases held in the open square's screens (D209).
- Bodies shade each other. Light one body absorbs is light that does not reach the bodies below it. That shading is
  pooled over the whole world at each metre of depth (`src/Evosim.Core/Environment/LightField.cs`; D164 noted the
  same).
- From D219-D221, a biter eats only what it touches, a living body or remains in the 60 s after a death. Grazers
  that stay in one place will empty it of leaves,
  which is the local depletion this proposal wants for animals. D219 brings it for nothing.

## The candidate rules

Each rule below says what changes, what it should push evolution toward, how it fits the separate oases, its risk, its
build and how a screen reads it. The table sums them up. Look at the build column: the deeper winter is a dial that
already exists.

| Rule | Pushes toward | Build, agent time | Older configs |
|---|---|---|---|
| Fountains with lifetimes | crossing the desert, reserves, migrating grazers | half a day to a day | refused |
| A deeper winter | storage, timed breeding, die-off and recovery | none, a dial | readable |
| A seasonal fountain | the same, through matter rather than light | a few hours | refused |
| Crowds pay for their own light | crowds bounded by their oasis, canopies | one to two days | refused |
| A direct cost of standing still | rejected, below | | |

### Fountains with lifetimes: oases are born and die

Each island keeps its place, but its fountain runs for a set lifetime and then stops. When one stops, another island's
fountain starts, on a schedule fixed by the world's seed, so the number of running fountains holds steady. The island
that went quiet keeps its leaves for a while. They live on light and cannot breed (D206). So they die off over a few
lifetimes of a body, a few thousand seconds (inference from the age hazard's base of 3,000 s in round 62's config).
The new fountain fills from whatever reaches it.

The design study before round 60 called this wandering sources and found two forms
(`logbook/specs/r60-patchiness/report.txt`, its third candidate). A fountain that drifts slowly is followed by the
crowd growing at its leading edge, with no body ever crossing. A fountain that jumps 100 m or more across the desert
needs a body that crosses. So the switching form is the one that makes crossing pay. D175 and D190 both held it for a
later round.

It pushes toward bodies that cross the desert. A leaf line that drifts can seed a new oasis only by luck. A grazer
line that crosses on its reserve can graze a young oasis before others arrive. Large reserves, and brains that turn
away from a thinning oasis, would pay (inference). That is what the owner's picture of migrating swimmers needs. It
fits the separate oases directly, since there are still oases with desert between them, now with a history.

The risks are three. Oases may die faster than they are founded, and the world thins. The founding trickle may do the
crossing by fiat. Round 62's founders are placed where the matter is, by a switch in its config, so a new fountain
would be seeded by the trickle before anything crossed to it. The trickle's placement needs a ruling with the rule.
And the switch is a step in the matter, which the oases' crowds may lag far behind.

The influx and burial run on the host computer, so the card needs no change (the round 60 proposal said the same, and
its screens confirmed it). A schedule computed from the seed and the clock needs no new state in the checkpoint
(inference, to be checked when built). It adds a tunable or two, a lifetime and a count, so every older config is
refused. Half a day to a day with its tests.

A screen reads it on two seeds to 24,000 s, beside a twin with fixed fountains, after D219 is settled. Round 60's
reader already counts the living per island, the bodies seen crossing by kind, and the lines that live on two or more
islands (`scripts/reads/r60-read.py` on `r62-launch`). Two readings are new. One is how long each fountain that opens
takes to hold a crowd. The other is how much of that crowd descends from bodies born elsewhere in the world, and how
much from trickle founders.

### A deeper winter: a dial that exists

Winter's light today is 0.4 of the year's mean. The season's amplitude is a dial (0.6 in round 62's config), and at
0.8 winter falls to 0.2. Nothing is built, and every older config stays readable.

It pushes toward storage, breeding timed to the summer, and lines that survive a die-off. Leaves still live through
winter on what they hold, but fix less, so the grazers' food thins. Round 62's bar for a healthy grazer guild already
asks for grazers alive at every season's end (D215). Today that test fails in four of six fast-rot worlds at the
summer windows (the table in `logbook/specs/r62-rot-screen/d215-table.md` on `r62-launch`). A deeper winter makes it
harder, so it is a stress on the grazers. It fits the oases neither better nor worse, and it gives Proposal 1's fat
cell something to answer (`proposals/propose-new-cell-types.md`).

The risk is that grazers die out each winter and the trickle refounds them, which reads as a guild and is not one.
D215's born share catches it. A screen is one or two seeds at 0.8 beside the base, read on D215's tests and births by
season. It can run as soon as the card is free after D219's screen.

### A seasonal fountain: matter in a spring pulse

The fountains' yearly total stays the same, but most of it arrives in a short spring pulse, as upwelling does in real
seas. Plants keep their matter, so the pulse sets when they can breed and not whether they live. It pushes the same
way as the deeper winter, through matter instead of light. It is a few hours to build, host only, and refuses older
configs. I would hold it behind the deeper winter, which tests the same idea for nothing.

### Crowds pay for their own light

Today's shading divides a layer's leaves over the whole world's area. In round 62's world that is 90,000 m², and the
islands cover about 7% of it (`matterIslandCover` 0.067 in its config). A crowd standing on the islands therefore
shades as if it were spread over the whole sea. If shade were local, the same crowd would dim its own island far more
(inference, an order of magnitude from the cover figure and not a measurement). The world has a per-patch shading
switch (D061), but round 62's patches are 100 m squares, larger than an island, so it would not do this job. A local
shade needs a finer grid of its own.

It pushes toward oases that hold only as many leaves as their light feeds, toward canopies and tall growth, and leaves
room beneath for bodies that live in shade. It would replace the population ceiling, an instrument, with a limit the
world sets itself. D021 meant the ceiling to be read that way, as an instrument and never as an outcome. The risk is
large. It changes every body's income in every world. It may push leaves out into the desert, where they live on light
though they cannot breed. And it reaches the light code, which every reading of income rests on. One to two days, the
card's use of the light to be checked in the build (I have not read whether the card computes any of it). I would hold
it until the fountains and the winter are read, and bring it back if the ceiling keeps binding.

### A direct cost of standing still, rejected

The simplest rule would charge a body more the longer it stays in one place, and I recommend against it. No ocean
mechanism does this to plants, which sit still in every real sea. It would pay motion by decree, a goal written into
the physics, and evolution would find the cheapest way to twitch. The rules above make staying put lose because the
food moves, which is the ocean's own reason.

## The recommendation

Build fountains with lifetimes, and screen a deeper winter, in this order.

1. D219 is built and screened first, as ruled. The ceiling bench and the stroke knockout run beside it, since they
   change no world.
2. The deeper winter runs as a screen with no build, as soon as the card is free. It reads in hours, and it tells
   Proposal 1's fat cell whether a lean season bites.
3. Fountains with lifetimes are built beside it and screened against fixed fountains on two seeds, with the trickle's
   placement ruled first.
4. Local shade is held until those two are read.

The fountains are the rule that best matches the owner's picture. They keep separate oases with desert between them
and give those oases a life and a death, so a lineage that never leaves dies with its oasis. The winter is the
cheapest test of whether a lean season changes anything at all.

## The decisions for the owner

1. Whether island fountains get lifetimes, switching off and opening elsewhere on a schedule fixed by the seed, with
   the number running held steady. This is a world rule, and it refuses every older config.
2. What a fountain's lifetime should be in the first screen. I suggest one or two years, 6,000 to 12,000 s. That is
   long enough for an oasis to fill, and short enough that each world sees several switches in 24,000 s.
3. Where the founding trickle places its founders under moving fountains. The choices are where the matter is, as now,
   which seeds new oases by fiat; at random in the water; or nowhere once founding is over.
4. Whether a deeper winter, the season's amplitude at 0.8 so that winter's light is 0.2 of the mean, is screened with
   no build.
5. Whether local shade is held, as recommended, or drafted as its own proposal now.
6. Whether a direct cost of standing still stays rejected.
