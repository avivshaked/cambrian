# 13 — A seed is one world, at any thread count and on the card

A world in this project is a long calculation. It starts from a seed, the number that starts its random number
generator, along with a settings file and a build of the code. Run it twice with all three the same, and the project
expects the same world twice, to the last binary digit. That property is called replay. It is meant to hold however
many processor threads the farm, the program that runs the worlds, spreads the work over. It is meant to hold on the
graphics card too, the card for short, within the card's own arithmetic.

This piece says why replay matters here and how the farm keeps it. It says what the card gave up to join the farm, and
what it had to prove. Then it tells where replay failed and what each failure cost. My reading of the whole record is
short. Replay held wherever the arithmetic was made to run in one order, and it broke wherever something let the order
vary.

## A seed has to mean one sequence

Every world draws its random numbers from one seeded generator, and nothing else in it is random ([DESIGN.md
§7](../DESIGN.md)). The generator is PCG, a recipe O'Neill published in 2014 that steps from one whole number to the
next by a fixed integer formula ([its source](../src/Evosim.Core/Rng.cs)). The generator built into .NET, the platform
the code is written for, was turned down. Its algorithm is not promised to stay the same between versions, and .NET
Core did change it ([the root CLAUDE.md](../CLAUDE.md)). A fixed formula gives a seed the same stream on every runtime
and every machine.

A seed's world is also fragile, because a world is chaotic. In early September one seed was run three times for 5,000
simulated seconds, and the three reports agreed to the last decimal. Then a change capped a drag impulse, which fired
only sixty-eight times. By 5,000 s it had moved the same seed's population by up to 20% ([DESIGN.md §7](../DESIGN.md),
from [logbook 0052](../logbook/0052-the-coarse-step.md)).

That is the butterfly rule. A before-and-after on one seed cannot tell a change from the luck of the draw, so results
are compared across seeds. Replay is what makes the rule work. Two runs of the same inputs give the same world, so any
difference between two runs has its cause in the inputs (my reading). When replay fails, a run is one realisation of
its seed: one roll of the dice, rather than the outcome of that seed ([the reader's key](../logbook/README.md)).

Replay does four jobs here. The theatre, the program that draws a recorded world, re-simulates a run from its seed. It
checks itself against the run's own statistics as it goes, so a viewer knows they are watching the run ([logbook
0069](../logbook/0069-the-shared-world-does-not-replay.md)). A checkpoint, a saved copy of a world's whole state, lets
a stopped run carry on, and the farm counts the continuation as the run itself ([the farm's
notes](../src/Evosim.Farm/CLAUDE.md)). A new build can prove that it changes nothing while its new features are off.
And a fault can be rerun from checkpoints, span by span, until it shows itself.

## The shared world did not replay in Unity

Until late September the worlds ran inside Unity, a game engine, on its physics engine, PhysX. The early worlds were
tiled: each creature sat 100 m from every other, nothing touched, and those worlds replayed. On 2026-09-06 the shared
box, where creatures lived in one volume and bumped into each other, was found not to (logbook 0069).

It showed up by accident. Round 27's seed 4 and the diagnostic run that had justified the round were the same world,
on the same code, settings and copy of the Unity project. The diagnostic held a steady line of 195 stomachs, bodies
that feed on matter dissolved in the water, at 20,000 s. The round's run had none at 25,900 s. The two agreed sample
by sample through 1,400 s, then parted in forty columns at once.

A session then ran the same world again and again. Unity's Enhanced Determinism setting, whose stated purpose is to
make results independent of the rest of the scene, changed nothing. Neither did three other physics settings. Six runs
of one world gave six worlds.

To find where they parted, the session built a state digest. Every few physics steps it writes one hash of every
living body's position, orientation and velocities. A hash is a short fingerprint of a set of numbers, so two equal
fingerprints mean equal numbers. A physics step is a hundredth of a simulated second. Two runs agreed through step
147,777. At step 147,778 one creature's velocity differed by one or two units in the last place, the smallest change a
number can hold. Its root moved at −0.07242366 m/s in one run and at −0.0724236444 m/s in the other.

That speck spread by touch, to three bodies at the next step and four within seven, all in one crowded pocket. Unity
solves bodies that touch together, and it shares that work among extra worker threads. With the default 31 workers,
two runs parted at step 147,778. With one worker they parted near step 184,700. With none they agreed over all 300,000
steps. NVIDIA, who make PhysX, document that its results do not depend on the thread count. PhysX had not met these
creatures.

The fix was [D078](../DECISIONS.md#d078). The physics solver runs with no worker threads, and every run's manifest,
the file that records how a run was made, names the count. D078 was an instrument decision, which the owner could
reverse for speed. It cost 19% of the wall-clock time at 120 to 180 bodies. A pair then ran to a million steps and
agreed on all 10,001 digests. Round 28's probe agreed with its scored run at about 1,700 bodies.

Every shared-world run before the fix became one realisation of its seed. No result was read differently, since seeds
were already compared as draws. What was lost was the promise that a given run could be re-run and watched. D078 also
turned down a shortcut. The theatre could have recorded every body's pose and played it back. That would make it a
video, and its identity check would have nothing to check.

## The farm sums in a fixed order

On 2026-09-22 the project left Unity's physics for a farm of its own. The farm is a console program on .NET 8 that
steps the world with the project's own solver ([the farm's notes](../src/Evosim.Farm/CLAUDE.md)). The solver uses
Featherstone's articulated-body method, a standard way to step a tree of jointed links in time that grows with the
number of links. It works in double precision, the wider of the two number formats a processor offers.

Its design started from what broke in Unity ([the solver's specification](../logbook/specs/own-solver-spike-spec.md)).
Each creature is stepped alone. Contact between bodies is a soft push, worked out from where the bodies stood at the
end of the previous step. The candidates are found through a grid and summed in a fixed order, and no creature's step
reads another's current state. So the bodies can be shared among threads in any split, and no arithmetic depends on
the split. A test asserts that a trajectory is the same to the bit at any thread count.

The order matters because computer arithmetic rounds. Adding a, then b, then c can give a different last digit from
adding c, then b, then a. One digit was all that Unity's crowd needed. A sum that always runs in the same order gives
the same digit every time. This illustration is mine, from how floating-point numbers work, and the sources state only
the rule.

The order is guarded in places a reader would not look. In its first days the farm's contact grid was rebuilt, after a
few giant bodies made every body check every other. The new grid sorts each candidate list and removes repeats. So the
sum is the same sum in the same order, and every digest stayed where it was (the farm's notes). The runtime is another
such place. .NET compiles code while it runs, first quickly and later again with optimisation. The quick and optimised
forms of one loop can round differently on an edge case, so every project here that reports a digest turns this
tiering off.

The payoff came at once. The farm ran round 42 seed 1's world for 30,000 s in 24 minutes on 16 threads, with both of
the world's books closed. Unity had taken ten hours. The farm's identity claim is a digest file written at one thread
and at many, and compared.

## The Editor cannot replay a farm run

The farm's code is also a package inside the Unity project, so the theatre can step a farm world live. A check ran the
farm's own loop in the Unity Editor. The body counts agreed at every sample, but the energy audit's residual parted at
the first sample, before any body had moved (the farm's notes). Threads and tiering were ruled out. The Editor runs C#
on Mono, an older runtime, and the farm runs on .NET 8, and the runtime was the cause.

A second check fed both runtimes four million numbers and a million angles. Their library functions for sine, cosine,
square root and rounding matched bit for bit. Their arithmetic did not. Mono holds the middle of a single-precision
expression wider than single precision, and rounds it only where the code says to. Written inline, 8% of the distances
and 25% of one family of angles came out one unit in the last place apart.

So live play in the Editor is a cousin by construction. A cousin, in this project's words, is a picture made by
restoring a run and stepping it on where it does not replay to the bit. It shows the run's bodies, but not the run's
own seconds. The theatre says COUSIN on screen (the farm's notes). An identity claim about a farm run is made on the
farm, never across the two runtimes ([the root CLAUDE.md](../CLAUDE.md)).

## The hashes detect a mismatch and promise nothing more

A run carries its identity in its manifest ([DESIGN.md §7](../DESIGN.md)). The config hash, configHash, covers every
tunable, the settings that define a world's rules, and names the world a run obeys. Hashes of the source name the
build. In Unity there were two, the simHash of the simulation's scripts and the coreHash of the core. On the farm
there are three, one each for the core, which holds the economy and development, the solver and the farm's own loop.
With the config hash they make the four a resume checks. The manifest adds the engine and the thread count, and the
whole of it is the run's identity.

The design is plain about what the hashes buy. PhysX was never bitwise deterministic across machines or Unity
versions, so the hashes exist to detect a mismatch. They do not make a run portable (the root CLAUDE.md). The farm
refuses to resume a checkpoint across any of the four hashes unless told to, and it then marks the result a cousin
(the farm's notes).

A hash reads bytes, so it catches more than code. One worktree is a second checkout of the repository. In it, one
source file sat on disk with bare line feeds, while every other file carried Windows line endings. The code was the
same, and git showed the tree clean. The coreHash differed all the same. Every run launched there on 23 and 24 October
names a build that no fresh checkout reproduces ([the build readings](../logbook/specs/build-readings.md)).

That strictness has also got in the way. The Unity hash covered every script under the simulation's folder, and the
theatre once sat inside it. Editing a label on the theatre's screen made every earlier recording refuse to replay, so
the theatre moved out of the folder (the root CLAUDE.md). Even printing a number can move a hash. .NET 8 and Mono
write the same float differently, one as 17.641891 and the other as 17.6418915. That printed text feeds the config
hash (the farm's notes).

## The card proves itself against itself

The card is an RTX 4090, and on 2026-09-22 the project measured what it could do ([logbook
0112](../logbook/0112-one-thread-a-creature.md)). Code reaches it through ILGPU, a library that compiles C# into
programs for the card, called kernels. One thread on the card steps one creature. On ILGPU's stand-in for the card,
which runs on the processor, the kernel matched the farm's solver to the bit over a thousand steps.

Precision was the question. A consumer card runs double precision at about a sixty-fourth of its single-precision
rate. In double the card was slower than sixteen of the processor's cores at 10,000 bodies. In single it was about six
times faster, and after ten seconds its bodies sat within 1.4 mm of the processor's. A single-precision world is a new
realisation of every seed, and no recorded run replays on it.

So the card gave up matching the processor. [D105](../DECISIONS.md#d105) put the physics on the card in single
precision, with every identity claim made on the card against itself. On 2026-10-08 the owner ruled that the card is a
valid engine if its world is sound ([D157](../DECISIONS.md#d157)). In the owner's view a slightly different world is
still a valid world to study, and the processor's doubles are only a closer approximation of the same physics.

Sound has terms. The card's runs must close both of the world's books, the energy audit and the matter identity
([piece 07](07-the-world-keeps-two-books.md)). They must replay themselves bit for bit on the same card. A round's
seeds and its controls all run on one engine, so no comparison mixes card and processor. And an engine changes only
between rounds.

The card's own check is made at two group sizes. The card launches its threads in groups, and the program says how
many go in each. ILGPU's automatic choice was up to 2.9 times slower than groups of 32, because each thread held 12.7
kB of working memory in double. So the size is set by hand (0112, [the card's
notes](../src/Evosim.Farm.Gpu/CLAUDE.md)). In the first measurements, every group size from 32 to 1,024 gave the same
bits. Since then, a claim of identity on the card runs each world at group 32 and again at group 64.

Why two shapes are enough is my reading. A result that changed with the grouping would show that something hung on
which threads ran together, or which ran first. That is the kind of loose order that broke Unity's crowd. Agreement at
two shapes is evidence that the card's sums run in one order whatever the grouping. It cannot rule out a race that
happens to fall the same way at both (inference).

Where a kernel copies a calculation the processor makes, the two must still match. The card fuses a multiply and the
add after it into one operation unless told not to. In a test, 14% of such doubles differed from the processor, and
nearly half of a 24-term chain did. A product written as an explicit, separately rounded multiply is never fused, and
with it every pattern tried matched bit for bit (the card's notes). The card's copy of the water's transport is held
to the processor's, byte for byte, by a test.

Single precision also cost something to repair. A move smaller than half the gap between neighbouring single-precision
numbers was lost. Resting bodies flickered out of touch, and the card counted about half the processor's contacts.
[D168](../DECISIONS.md#d168) kept each position's rounding error in a second number and measured contacts from there.
Over 10 s of round 57's world, pairs in contact rose from 6.08 a step to 12.33, against the processor's 13.20. The
changes are off in double, so the double kernels keep the processor's arithmetic to the bit.

Even a lost body, one whose numbers have stopped being numbers, can break an identity check. When both inputs of an
operation are not-a-number, the result keeps one of them, and two compilations of the same source can keep different
ones ([logbook 0115](../logbook/0115-the-crowd-on-the-card.md)). A raw digest then disagrees in the sign bit of a dead
body's values. So the card's digest hashes such values as one pattern, or leaves lost bodies out.

## A build with its switches off must step the old world row for row

New features reach the world behind switches, and every new switch is off by default ([D243](../DECISIONS.md#d243),
[D249](../DECISIONS.md#d249)). That makes a strong test possible. With every new switch off, a new build must step a
recorded world as the old build did, to the bit. On the card that test is called an identity sitting. On my reading of
the sittings' records, a build lands on the project's main line after it passes.

Integration branch 2 shows the shape ([D248](../DECISIONS.md#d248), [its
sitting](../logbook/specs/int2-identity/README.md)). The branch brought a stroke that pushes a limb's whole assembly,
and a mirrored twin drive. With every switch off, the build stepped three worlds of round 62's confirmation run for
600 s. Each matched the recorded world on all 60 samples of statistics and all 601 digest rows. Its lineage, the file
with one row for every creature ever born, was equal byte for byte.

The config hash changed all the same, from bcc3af98 to ecbba999. The build added a tunable, and a new tunable enters
the hash even while it is off. The hash said the settings were new, and the digest said the world was the same. Both
were right (my reading of the sitting).

The sitting then turned the switches on, singly and together, at group 32 and at group 64, and each pair matched.
Last, it checked that each switch changed its world. That check comes from two old mistakes. A self-collision setting
once made no difference because it never reached the scene. A 40-fold sweep of drive strength gave identical results
because the value was read once, before anything set it ([the root CLAUDE.md](../CLAUDE.md)). Identical numbers across
a change can mean the change was never applied. Here the fin law of [piece
10](10-a-fin-pushes-the-water-by-its-width.md) changed its world from step 200, and the twin drive from 340 s.

The sittings are routine now, and the machine does not always cooperate. The sitting for the nose, a sense of smell,
was cut a minute in by a Windows Update restart on 2026-10-29. It ran again that morning and passed on the same terms
([its record](../logbook/specs/nose-card-identity/README.md)).

## A resume has parted three times on the farm, and two causes are found

Since the farm took over, a resume has parted from its run in three ways. In two the cause is on record: a checkpoint
left out something the world reads, or a list on the card filled in an order that no rerun repeats. The third has no
cause on record, though a later build no longer shows it. Each left a stretch of some world that can never be run again.

The first way began with the senses. The farm's checkpoint carried everything the solver reads, and at first not
everything a sense reads. The health each part had lost went unwritten. A resume of round 45 seed 2 therefore parted
from its run at the first sample. Of sixteen wounded bodies, it parted in the two jointed ones whose brains read the
damage sense (the farm's notes).

It happened again with the contact flag, which tells a body it is touching something. Round 48's resume parted at its
first sample in bodies whose brains read contact, and its read records that a resume of that round is not faithful
([logbook 0120](../logbook/0120-the-eaters-spent-their-endowment-on-one-child.md)). The farm's fidelity check now
restores a world, steps the copy and the original side by side for a short stretch, and compares them member by
member.

The card repeated the lesson. Since D168 the card has kept each part's resting contact position, and the checkpoint
did not carry it. In round 59, a guard that stops a run when the processor runs hot halted seeds 1 and 3, and both
were resumed. Then the round's own check resumed seed 2 from 45,000 s. It parted from the unbroken run from its first
second ([logbook 0141](../logbook/0141-the-planted-grazers-hold-on.md)).

So seeds 1 and 3 after 36,620 s and 41,840 s are nearby continuations. The round's rules made them, and the first runs
would have gone on to make others. Every card resume from 2026-10-13 to round 60's build has that status. Round 60's
build carries the positions, and a resume from 1,500 s on it matched the unbroken run in every row.

The second way is a race. The card finds which parts might touch by sorting them into a grid. For each part it lists
the parts that share its cells. In round 60 that list held 256 entries, and anything past it was dropped. Threads
added entries as they arrived, and the list was sorted only afterwards ([D193](../DECISIONS.md#d193)). So which
entries a full list kept depended on which thread got there first, and no rerun goes the same way.

Seed 6002's list overflowed 6,257,897 times and seed 6003's 19,008 times, in crowds of 16,000 to 18,000 bodies (D193,
[logbook 0143](../logbook/0143-the-crowds-leave-the-islands.md)). Logbook 0143 counts one part's search as a query and
D193 counts body-steps, so the two sources differ on the unit. Spans without a drop matched when resumed. Seed 6002
resumed at 46,500 s differed from the round from its first second. Its years 7 and 8, where two of the round's
predictions rest, are one realisation that no rerun can reproduce. A dropped candidate may be a mouth that never
touched its leaf. So the kills in those years may be fewer than the rules would give. 0143 marks that as a guess.

The fix had two parts. A longer list alone did not help. At 512 entries the dense span still overflowed, because a
pair of parts sharing several cells was listed once for each. The fix refuses a repeat, and past the cap it keeps the
lowest distinct entries whatever order they arrive in. With it the dense span dropped nothing, and two runs of it
matched in every record (0143).

A later re-read looked at kelp blades, leaves with a chain of parts that each double in length. One in seed 6002 grew
nearly 15 m long. The re-read found that a few dozen long blades most likely made nearly all of that seed's drops
([logbook 0152](../logbook/0152-round-60-read-again-the-dropped-contacts-grew-with-a-few-long-blades.md)). Its rerun
of the last span counted 2,491,424 drops at the round's cap. The entry calls that count a close guide to the round's.
A resume past a drop parts from its run, so the round's own count cannot be read again.

The third parting is untraced. On 24 October the build that brought contact eating's store share
([D224](../DECISIONS.md#d224)) was resumed on the card from 6,000 s. It was resumed again from 1,500 s, while the
share was idle ([its identity record](../logbook/specs/r62cs-prereg/identity.md)). Each resume parted from its run at
the first row, in the seventh or eighth significant figure of the joules spent on gestation. Its lineage followed a
few rows later. So the parting comes with contact eating, the remains or that build's checkpoint, and no file says
which.

On 2 November one later build answered the question for itself. It was [D275](../DECISIONS.md#d275)'s, the build on
which a bite needs the mouth. Its identity check ran a world with contact eating, the remains and the mouth rule all
on. Resumed from 1,200 s at group 32, the world went on to 6,000 s. The same world ran unbroken at group 64. The two
agreed on every world value of 480 rows on both seeds, and on their lineage row for row. Only the wall clocks and two
counters of the process's own work differed ([r66's pre-registration](../logbook/specs/r66-prereg/predictions.md)).

So on D275's build a card resume repeats its run. Whatever parted D224's resumes was gone by then, and no file says
which change removed it (inference). No build between the two has been checked. A stopped world of one of them is
still rerun fresh or read as a cousin.

## Replay costs speed and buys a cause for every difference

The costs are written down. D078 added a fifth to a Unity run's wall-clock time at small crowds. The farm gave up a
runtime optimisation, and the card writes some of its products the long way. Every build that lands spends a sitting
on the card. And a few stretches of worlds stand as single realisations. They are the shared box before D078, the card
resumes before round 60's build and seed 6002's last two years. With them go the resumed stretches of the builds from
D224's up to D275's.

What it buys, on my reading, is that every difference has a cause someone can find. When two runs part, the digest
names the step and the body. When a resume has parted, a missing field or a loose order has so far been the cause. The
third parting is not traced, though it no longer shows on D275's build. The project changes its realisation freely, for speed
([D153](../DECISIONS.md#d153)) or for single precision (D157). It has not given up replay inside an engine, and every
failure above whose cause was found has been fixed (inference from the decisions read together).

## What here is inference

- **Replay held where the order was fixed, and broke where it was loose** is the author's reading of the failures
  together. Each source describes only its own case.
- **That replay lets any difference be traced to the inputs**, and the closing account of what replay buys, are the
  author's.
- **The example of adding a, b and c in two orders** is the author's illustration from how floating-point arithmetic
  rounds. The sources state the rule of a fixed order, and logbook 0069 measured what one last-place difference did.
- **Why two group sizes are enough** is the author's reading. D105 and the card's notes set the practice and give no
  reason beyond identity across launch shapes.
- **The hash and the digest both being right** in integration branch 2's sitting is the author's reading of its record.
- **That a dropped candidate cost kills** is logbook 0143's guess. **That the long blades made most of the drops** is
  logbook 0152's inference, from two seeds and the timing.
- **That the project keeps replay inside an engine as a standing rule** is inferred from D078, D153, D157 and D193 read
  together. No decision states it in those words.
- **That a build lands only after its sitting passes** generalises D243, D248 and D249, each of which names its own sitting.
- **That the third parting's cause is untraced** rests on finding no fix named in the logbook, DECISIONS.md or HANDOFF
  on 2026-11-02. **That it was gone by D275's build** is read from r66's check 3, which resumed one world on two seeds.
  No source says which change removed it.
- **Double precision as the wider of two formats**, PCG as a recipe of whole numbers, and the explicit multiply as
  "separately rounded" are the author's plain glosses. No project file states them in those words.

## Where it is

| file | what it holds |
|---|---|
| [`Rng.cs`](../src/Evosim.Core/Rng.cs) | PCG, and why the generator built into .NET was turned down |
| [`DESIGN.md`](../DESIGN.md) §7 | reproducibility, the butterfly rule, and the manifest as the run's identity |
| [`CLAUDE.md`](../CLAUDE.md) | the Editor-cannot-replay rule, the hashes as detectors, the theatre's move, the two unapplied changes |
| [`src/Evosim.Farm/CLAUDE.md`](../src/Evosim.Farm/CLAUDE.md) | thread identity, tiering, Mono against .NET 8, checkpoint fidelity, the four hashes |
| [`src/Evosim.Farm.Gpu/CLAUDE.md`](../src/Evosim.Farm.Gpu/CLAUDE.md) | the hand-set group size, the fused multiply-add, identity at two group sizes |
| [`own-solver-spike-spec.md`](../logbook/specs/own-solver-spike-spec.md) | contact from the previous step, summed in a fixed order |
| [`DynamicsWorldDigest.cs`](../src/Evosim.Dynamics/DynamicsWorldDigest.cs), [`gpu-accept.py`](../scripts/reads/gpu-accept.py) | the solver's digest, and the card's identity check |
| [`DECISIONS.md`](../DECISIONS.md) D078, D105, D153, D157, D168 | Unity on no worker threads; the card in single; speed-ups judged by trade-off; sound on its own terms; small moves kept |
| [`DECISIONS.md`](../DECISIONS.md) D193, D224, D243, D248, D249 | the race fixed; the store-share build whose resumes part; switches off by default; integration branch 2's sitting; the general off-by-default rule |
| [`DECISIONS.md`](../DECISIONS.md) D275 | the mouth rule, whose build's resume repeated its run |
| [`r62cs-prereg/identity.md`](../logbook/specs/r62cs-prereg/identity.md) | the third parting, on D224's build |
| [`r66-prereg/predictions.md`](../logbook/specs/r66-prereg/predictions.md), check 3 | a resume on D275's build that repeated its unbroken run |
| [logbook 0052](../logbook/0052-the-coarse-step.md), [0069](../logbook/0069-the-shared-world-does-not-replay.md) | the butterfly rule; the shared world that did not replay |
| [logbook 0112](../logbook/0112-one-thread-a-creature.md), [0115](../logbook/0115-the-crowd-on-the-card.md) | the card's first spike and its group sizes; the not-a-number sign bits |
| [`int2-identity`](../logbook/specs/int2-identity/README.md), [`nose-card-identity`](../logbook/specs/nose-card-identity/README.md) | two switch-off sittings |
| [logbook 0120](../logbook/0120-the-eaters-spent-their-endowment-on-one-child.md), [0141](../logbook/0141-the-planted-grazers-hold-on.md) | resumes that parted: the contact sense, the card's resting positions |
| [logbook 0143](../logbook/0143-the-crowds-leave-the-islands.md), [0152](../logbook/0152-round-60-read-again-the-dropped-contacts-grew-with-a-few-long-blades.md) | the dropped contacts, their race and the blades |
| [`build-readings.md`](../logbook/specs/build-readings.md) | the line endings that moved a hash |
