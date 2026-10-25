# Proposal: new cell types for more kinds of life, starting with poison and fat

*Ruled: D228 and D229 rule every decision below, and the file is kept as the record of its survey (the round 62 cell-type table, the armour shares, and the designs of the held lateral line and cilia). Moved from `proposals/` on 2026-10-25.*

*A draft for the owner, written at the coordinator's request, 2026-10-23. It follows D219-D221, which make mouths eat only
what they touch and leave no corpse point, only remains edible by contact for 60 s. Cell types are world rules, so nothing here is built before the owner rules.
Every claim about the code names its file. What is a guess is marked as inference.*

## The proposal in one paragraph

A body is built from parts, and each part is made of one kind of tissue, its cell type. The type decides what the part
costs to keep alive and how it earns or spends energy. The code has eight types today. Two things a new type might add
are already in the world. Armour is a trait any part can carry, and a grip that holds rock exists, though round 62
left it out of its table. I propose four new types. I recommend building two of them first. One is a poison cell that
makes a body costly to eat. The other is a fat cell that stores energy in tissue, which makes a richer meal for
whoever eats it. Both are rules on the host computer alone, with no change to the physics on the graphics card. Both
are small builds, and D219's contact eating gives each of them a reason to pay. Before either, I suggest switching the
existing grip back on in a screen, which costs no build at all if the grip already holds coral.

## What a cell type is, and what a new one costs to build

A cell type is a class in `src/Evosim.Core/Cells/StandardCellTypes.cs`. It has a name and an upkeep in watts per cubic
metre of tissue. It has a build price, the energy a cubic metre of it embodies. And it has a rule for what the part
earns each step. Each type also caps four traits that any part may evolve. They are attack (how hard the part hurts
what it touches), intake (how fast it eats), protection (damage it absorbs) and toughness (how much health it has).
The world's table of types is written into every run's config, so each world says which types exist and what they
cost.

Round 62's table, read from the config of `r62f-h30-6201`, is below. Look at the upkeep column: the leaf is cheap to
run, and the biter (the consumer cell) is the dearest tissue in the world.

| Type | What it does | Upkeep, W/m³ | Attack cap | Protection cap | Toughness cap |
|---|---|---|---|---|---|
| structural | bulk, earns nothing | 1 | 1 | 1 | 4 |
| link | a joint that moves | 2.5 | 0.5 | 0.5 | 2 |
| neural | cheaper neurons | 5 | 0 | 0 | 1 |
| photosynthetic (leaf) | fixes light into food | 3 | 0 | 0.5 | 1 |
| absorptive (absorber) | takes dissolved food from the water | 4 | 0 | 0.5 | 1 |
| consumer (biter) | damages and, from D219, eats what it touches | 6 | 1 | 0.5 | 2 |
| buoyancy | cancels weight | 2.5 | 0 | 0 | 1 |
| anchoring (grip) | holds the bed or a reef it touches | 2.5 by default | not in round 62's table | | |

Read in the code, a new type costs five things.

1. A subclass with its own parameters, and one line registering its reader in
   `src/Evosim.Core/Serialization/CellTypeJson.cs`. The serializer was written so that adding a type touches neither
   development, mutation nor the genome format, as its own remarks say.
2. The rule it adds. Most of the work is here. A type that only earns, like the absorber, is one method. A type that
   changes how bodies meet, like the grip, reaches the physics, the card's kernels and the checkpoint. The grip is
   named in 26 source files, tests included, and it took the checkpoint's world state to 17 (`HANDOFF.md`).
3. Its effect on what older runs can load. A type that keeps all its numbers on itself leaves older configs readable,
   since a world that does not list it never sees it. A new world-level dial is a new tunable, and a new tunable makes
   every older config unreadable (`src/Evosim.Core/CLAUDE.md`). New per-body state that must survive a resume bumps
   the checkpoint's format. A new trait on a genome's node bumps the genome's format, and older genomes are refused.
   The recorded test fixtures need re-recording after either bump.
4. A new realisation of every seed. Mutation picks a part's new type from the table by a random draw. So a world whose
   table gains a type is a different world even before anything uses the type.
5. A colour in the theatre's palette (`unity/Assets/Theatre/TheatrePalette.cs`). The theatre sits outside the folder
   the simulation's hash covers, so this changes no recording's identity.

## Armour and the grip are already in the world

Any part may carry protection and toughness up to its type's caps, and mutation moves both
(`src/Evosim.Core/Mutation/Mutator.cs`). A blow's damage is the attacker's attack times its area, less the defender's
protection times its area (`src/Evosim.Core/Ecosystem/WorldMouth.cs`). Protection costs 1 W a unit and toughness 0.1 W
a unit, from the mouth group of round 62's config. Structural tissue can carry the most of both, so a shell is a body
that has evolved protection on structural parts. The farm writes the share of the living that carry any protection
into every stats row. In the fast-rot world `r62f-h30-6201` at 24,000 s it was 0.8%, against 1.1% carrying any attack.
Armour is possible and rare. The likelier reasons are its price, or too few biters to make it pay (inference), and
both are dials. So the first step is to read the share in the D219 screen, where biters must touch to eat. Building an
armour type comes after that, if ever.

The grip is the anchoring cell (D162, D165). It holds the bed or a reef on the first step it touches, weakens over
time, and lets go when its body starves (`src/Evosim.Dynamics/Anchor.cs`). Round 59's control found that no grip ever
formed, because hovering grazers never touched the mushroom caps (D171). D194 left the grip as a question for after
the coral round, and the coral's branches give far more surface to touch. Whether the grip counts coral as reef has to
be read on the round 62 build, where the coral code lives, since main has none. If it does, the grip comes back as a
screen arm with no build. If not, adding coral to the grip's touch test is a few hours, with its copy on the card.

## Four candidates

Each candidate below says what it does, the niche it opens, why not every body takes it, how it meets D219, and what
it needs in the code. The table sums them up first. Look at the third and fourth columns: the first two candidates
touch neither the card nor the formats.

| Candidate | Opens | Card change | Format bumps | Agent time |
|---|---|---|---|---|
| Poison | plants that are costly to eat | none | none | half a day to a day |
| Fat | bodies that bank summer for winter | none | config (one world dial) | half a day to a day |
| Lateral line | hunting and fleeing | a new smell field | config, new sensor | one and a half to two days |
| Cilia | swimming without a stroke | a new force | config, genome (brain) | about two days |

### Poison makes a body costly to eat

A poison cell earns nothing and pays upkeep. When a biter eats from a body that carries poison, the biter loses energy
in proportion to the poison in what it ate. In a simple form the body's poison strength is its poison tissue's share
of its volume. Each joule a biter takes from it then costs the biter a fixed fraction of a joule more. The poisoned
body gains nothing at the bite. Its lineage gains because a biter that eats it is weaker.

Today a leaf's only defence is protection, which works against the blow and not the meal. Poison gives plants a second
strategy. A mixed crowd of poisoned and plain leaves is also a reason for biters to differ (inference). Poison tissue
displaces leaf tissue in a body of a given size, and fixes nothing. Where there are no biters it is pure cost. So it
should be rare where biters are rare and common where they are common. Chemical defence in real plants behaves that
way (inference from general ecology; no source was fetched for this draft).

Contact eating is what makes poison work. A mouth must touch the part it eats, so the poison is charged on the touch
that feeds. Remains keep their poison through their 60 s window (D220). The build spec states what a bite moves to the biter
(`logbook/specs/contact-eating-spec.md` on `mouth-point`, D221), and poison is one more term on that route. So poison is written against that spec, after it.

In the code, poison is a new type with two numbers, its upkeep and its cost to the eater per joule. It adds one term
to the eating path in `WorldMouth.cs`, which runs on the host. It needs no per-body state, so no checkpoint bump, and
no genome trait, so no genome bump. It needs no card change, and a theatre colour. It is half a day to a day of agent
time with its tests, then a screen of two seeds beside D219's own control.

### Fat stores energy in tissue, and is a richer meal

A body's reserve has no ceiling today (`src/Evosim.Core/CLAUDE.md`, the gotcha on reserves). Storing energy is free,
so a fat cell alone would add nothing. It makes sense with one world rule beside it. The reserve a body may hold is
capped, and fat raises the cap by a set number of joules per cubic metre. Fat tissue has a low upkeep and a high build
price. Every type already carries its own tissue energy (`src/Evosim.Core/Cells/CellType.cs`), so fat is also worth
more to the biter that eats it.

The niche is bodies that bank the summer and spend it in winter. Round 57 gave the world two seasons, with winter's
light at 0.4 of the mean (D151). A plant keeps its matter and lives on light (D206), so today's winter costs a plant
little. A body that eats faces a lean season when its food thins, and fat is how it would carry through (inference).
Fat also adds weight, which sinks a body unless buoyancy pays for it.

A fat body costs more to build, so it breeds later or smaller. It is a richer meal, so it draws biters. And a body
that never meets a lean season gains nothing from the cap it raises. Against D219's biters, a fat body is the best
food in the water. That pairs it with poison: a fat, poisoned body is one strategy, and a lean, plain one another.

The world already has a reserve cap, off in every round, which releases a hoard over the cap into the water as matter
(`src/Evosim.Core/RunConfig.cs`). The fat rule makes that cap depend on fat volume. That is one new world dial, and
every older config is refused. The reserve is already checkpointed, so nothing else bumps. It runs on the host, with
no card change, and needs a theatre colour. It is half a day to a day. It belongs with the lean seasons in
`proposals/propose-doing-must-pay.md`, because without a season that bites, a screen of fat reads nothing.

### A lateral line lets a body sense other animals

Today a body smells leaves, with a direction (D167), and smells carrion (the scent group in
`src/Evosim.Core/RunConfig.cs`). After D219-D221 carrion lasts only 60 s, so the carrion smell has little to read, and no
sense finds another animal. A lateral-line cell would give its body the direction and strength of the nearest body carrying
a biter, within a few metres, scaled by the organ's volume. The name comes from fish, which sense nearby movement
through a line of organs along each flank.

It opens hunting and fleeing. A grazer that senses a biter can turn away, and a biter that senses a grazer can turn
toward it. It is the sense a predator of grazers would need (inference). The organ pays upkeep and its neurons cost,
and a plant gains nothing by it. D219's own risk note says contact is rarer than reach, so encounters limit eating. A
sense that steers a mouth onto prey turns rare encounters into frequent ones.

This is the large build. It needs a new field of bodies to smell, like the leaf smell's field, which the card fills
(`src/Evosim.Farm.Gpu/GpuScent.cs`). It needs a new sensor channel, a run setting that opens it, and the type that
gates it. Opening a sensor changes which channel a random draw yields (`src/Evosim.Core/Brain/SensorChannels.cs`), so
every world that opens it is a new realisation, and older configs are refused. It is one and a half to two days, on
the leaf smell's precedent. It earns its cost only once biters live by eating living leaves.

### Cilia let a body swim without a stroke

A ciliary cell pushes its body along the part's own axis, at a power the brain sets. Larvae and comb jellies swim by
beating cilia rather than limbs. Today a body swims only by beating jointed limbs in a rhythm its brain must find: the
brain's only outputs are a joint's power and bend (`src/Evosim.Core/Brain/CentralBrain.cs`). Round 61's bred swimmers
moved at 0.05 to 0.08 m/s against the local drift (`HANDOFF.md`).

It opens swimmers that migrate. In the fast-rot screen's last year, the bodies counted crossing from one island to
another were nearly all drifting leaves. At most one a world was a grazer. These are the reader's migrant columns in
`logbook/specs/r62-rot-screen/read/r60-read.txt` on the `r62-launch` branch. The thrust is billed by the work it does,
so a fast body burns its reserve. Against D219, a mouth that can reach a leaf meets more leaves.

It needs a new kind of brain output, so the brain's format changes and older genomes are refused. It needs a force on
the card, so the kernels change and the card's identity check runs again. The root `CLAUDE.md` says anything that can
push a body upward needs the same stop at the waterline as buoyancy. It is about two days. It is also a choice about
the project. Locomotion evolved from jointed parts is the Sims tradition, and cilia offer a way round it. I would hold
it until the cheaper changes have been read.

## The recommendation: poison and fat first, and the grip back on

- Poison is the one candidate that needs D219 to work, and it gives the plants a second strategy whatever the grazers
  do. The owner wants more variety of life even without classic grazers. Poisoned and plain leaves are variety among
  the most common bodies in the world.
- Fat is cheap, and it is the cell that answers a lean season. It should be built with Proposal 2's season rule and
  screened with it.
- The grip may cost nothing, and the coral was built to give it something to hold.
- The lateral line waits until biters live on living leaves. Cilia wait until the cheaper changes are read.

The order follows the dependencies. D219's build spec states the routes, and D219 is built and screened. Poison is
then written against those routes. Fat is built with Proposal 2's season rule. The grip's coral check is a reading on
the round 62 build and can happen at any point.

## The decisions for the owner

1. Whether poison is built after D219, as a cell whose tissue costs a biter energy for each joule it eats from it.
2. Whether poison costs the eater energy, as drafted, or health, a wound that can kill a small biter.
3. Whether fat is built, with the world rule that a body's reserve is capped and fat tissue raises the cap. This is a
   world rule, and it refuses every older config.
4. Whether the grip comes back as a screen arm in the D219 screen, if it holds coral without a build.
5. Whether the lateral line and cilia are held, as recommended, or one of them moves ahead of fat.
6. Whether the share of bodies carrying armour is read in the D219 screen before anything about armour is built.
