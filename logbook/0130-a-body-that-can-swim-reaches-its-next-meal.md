# Round 54's predictions: a body that can swim reaches its next meal

*2026-09-30, written before round 54's first seed and committed before it starts. It rests on round
53's read (logbook/specs/r53-read/read-30000.txt) and the owner's rulings D135 to D139. It also rests
on a screen of seed 1 to 5,000 s on the round's build (`r54sc2-s1`). Every number is round 53's or
the screen's, and each says which. A number I guessed says so.*

## The revision of 2026-10-01, before the relaunch

*Written under D140 and D141 and committed before the relaunch's first seed. Everything below this
section stands as first registered, except where this section says otherwise.*

The first launch ran seed 1 to 5,510 s and was stopped on the owner's ruling (D140), to wait for the
nursery's inoculant. Its run stays as it ended (`runs/r54-s1/2026-09-30-154834-4070da9c`). The relaunch
runs under new arm names, `r54b-s1` to `r54b-s3`.

Seed 1 runs as registered, without an inoculant. It is D141's control for D135's drive. Its world is
the first launch's, so its rows must repeat the first launch's to the last digit through 5,510 s, at
any thread count. If they part, the seed is stopped and the cause found before anything is read.

Seeds 2 and 3 are to carry the nursery's gaits in the trickle pool, after round 47's four stomachs.
The pool's share is raised so the stomachs arrive at the rate they did (D141). They launch only after
their inoculants pass a confirmation in round-like water, and a second revision naming the genomes is
committed before they start. The first confirmation failed: the gaits evolved in the nursery's still
tank, 5 m deep, beat the best blind constant there by 201 J over 600 s (standard error 33, fresh
draws). With the round's current of 0.1 m/s they beat it by 4 J (standard error 17). With the current
and the round's 25 m depth they lost by 21 J (standard error 25). So the gaits are evolved
again in a tank with the current and the depth, starting from their brains, before any is inoculated.

The clauses change as follows.

| clause | as first registered | in the relaunch |
|---|---|---|
| F3, W2, W3, EK2, EK3, EK5, EK6 | every pool founder | the stomachs alone, pool indices 0 to 3, so the gaits change nothing they read |
| I1 (new) | — | **a gait takes hold**: a line rooted in a nursery gait has at least ten living at 30,000 s, in 1 of the 2 inoculated seeds. Round 47's stomachs have entered every round since 48 through the same pool, and none has rooted a line of ten (F3). I guess this fails, as theirs did |
| I2 (new) | — | a reading: the gait founders' number, deaths, median age at death and how many bred, beside the stomachs' |
| J1, J2 | 3 of 3, 2 of 3 | seed 1 alone, the uninoculated control; read on seeds 2 and 3, where the gaits are jointed swimmers that would move both |
| P1 | 3 of 3 | a reading: the relaunch runs its seeds side by side at five threads each, which no earlier round's pace was read at |

The seeds run at five threads each, side by side, so that all three can end by the next morning. A
farm run's rows do not depend on its threads (the farm's identity tests), so only pace changes.

## The third note, of 2026-10-01 morning: no inoculant

*Committed before seeds 2 and 3 start, under D142.*

No nursery brain passed the confirmation in the round's water, so seeds 2 and 3 run as first
registered, without an inoculant. They run side by side at eight threads each, after seed 1. J1 and
J2 go back to their first bars, 3 of 3 and 2 of 3, over all three seeds. I1 and I2 read nothing, since
no seed carries a gait. P1 stays a reading, since the seeds ran at five and eight threads. Seed 1's
run is unchanged by this note: it started under the revision above and carries no inoculant.

## What it asks

Round 52 sent the founders that carry a mouth to the corpses, and round 53 made the tank shallower so
the bed had more to find. The mouths ate: 256, 241 and 224 corpses in 30,000 s. But a meal still
bought little. Round 53's mouth founders lived a median of 161, 179 and 204 s, and no line rooted in
a mouth kept more than one living member. D132 named the wall: a mouth that cannot swim to its next
meal cannot live on corpses, however well it lands.

Round 54 is the first round in which a body can swim on purpose. D135 gives every joint a power and a
bend from the brain, and the power pushes the limb through the water. A corpse now has a smell that
carries about ten metres. Work costs half its joules from the reserve.

Round 54 asks four things. The first is whether the jointed bodies keep a faster pace than the rigid
ones through the run, where round 53's lost theirs (J1). The second is whether, by the last 10,000 s, the jointed
bodies that smell swim up the scent more than the jointed bodies that do not (J2). The third is
whether a mouth founder lives longer than round 53's did (K4). The fourth is whether the corpses are
still eaten at round 53's rate (N3).

## The world

Round 53's world (0129's world section), with D138's and D139's changes, from `rounds/env-r54.ps1` on the
branch `round-54`:

- **D135's joint drive** gives each of a joint's degrees of freedom a power and a bend from the
  brain. The bend is the pose the joint holds, reached at 2 Hz. The power pushes the limb along its
  length, as hard as a limb swinging at that power would be pushed. The stroke itself is not
  simulated. A joint pushes only when its neuron is above the joint's threshold gene, so a silent
  neuron gives no power (D139). Four drive genes on every joint shape the command:
  a threshold, a reaction, a hold and a release. Founders draw them, and a child's mutate at 0.08 a
  gene, as the gestation genes do.
- **A corpse's scent** carries to 10 m, at half strength at 20 J.
- **The work cost at 0.5** charges half the push's mechanical work to the reserve. At full power on
  every joint that is about 1.35 times a body's standing keep.
- **The water is as D136 ruled it.** A coasting body slows as its drag says, with no rule that stops
  it. D137's friction at the bed and the rocks is built and off.

Three seeds of 30,000 s run at dt 0.01, one at a time at twelve threads, with a checkpoint every
100 s. The change is a new realisation of every seed, so no seed replays round 53's.

## The screen

The first screen of seed 1 (`r54sc-s1`) ran with a silent neuron at half power, as the first build
read it. Every jointed body pushed from birth and could not stop. By 5,000 s it held 266 jointed
bodies against round 53's 917, and its jointed children lived a median 1,108 s against 1,466. The
owner's design had said a joint pushes only above its threshold, and D139 restored that. The second
screen (`r54sc2-s1`) is the round's build, and it ran seed 1 to 5,000 s at eight threads. Its first
5,000 s will be seed 1's, since the build and the world are the same and the thread count moves
nothing. Those seconds are seen.

The table reads it against round 53 seed 1 at the same seconds. The speed ratio is the jointed
bodies' mean speed over the rigid bodies'. Both are speeds over the ground, and the current carries
both, so the ratio cancels most of the current.

| | the screen | round 53 seed 1 |
|---|---|---|
| alive at 5,000 s | 2,046 | 2,065 |
| jointed at 5,000 s | 947 | 917 |
| jointed children born by 4,000 s, and their median life | 3,135, 1,348 s | 2,966, 1,466 s |
| jointed founders' median life | 769 s | 571 s |
| corpses eaten by 5,000 s | 32 | 5 |
| the mouth founders' median life from 3,000 s | 218 s over 13 deaths | 196 s over 18 |
| the speed ratio over 1,000 to 5,000 s | 1.25 | 1.20 |
| work a jointed body, 1,000 to 5,000 s | 0.004 W | round 53 charged its old drive's swing, 0.69 W a body |

The joints now cost a body little, and the jointed bodies hold round 53's numbers. They do not yet
cover more ground than round 53's did. Most joints sit silent until selection finds a use for them, so
the work paid is small. The corpses eaten are six times round 53's, which is a small count early in
the run. Among the jointed bodies, those whose brains read the scent headed up it no more than those
whose brains do not: the margin was −0.005.

## Predictions

### Movement

| # | prediction | falsified by |
|---|---|---|
| J1 | **the jointed bodies keep moving**: over 5,000 to 30,000 s, the jointed bodies' mean speed over the rigid bodies' is at least 1.2 times round 53's same seed's, in 3 of 3 (round 53: 0.92, 0.80 and 0.66; the screen: 1.25 against round 53 seed 1's 1.20 over 1,000 to 5,000 s). Round 53's ratio fell late in its run, so this asks that round 54's jointed bodies keep their pace where round 53's lost it | the stats rows |
| J2 | **the smellers steer**: over 20,000 to 30,000 s, among the jointed bodies, the mean cosine between a body's velocity through the water and the scent's gradient is at least 0.02 higher for those whose brain reads the scent than for those whose brain does not, in 2 of 3 (the screen: −0.005 over 1,000 to 5,000 s). A founder's brain is drawn at random, so steering has to be selected. I guess this fails | steering.jsonl |
| K4 | **a mouth founder lives longer**: the median age at death of the mouth founders born from 3,000 s is at least 1.25 times round 53's same seed's (161, 179 and 204 s), in 2 of 3 (the screen: 218 s over 13 deaths against round 53 seed 1's 196 s over 18 by 5,000 s) | lineage.jsonl |
| N3 | **the corpses are still eaten**: `corpsesEaten` at 30,000 s is at or above round 53's same seed's (256, 241 and 224), in 2 of 3, and the three seeds' total is at or above 721 | the last stats row |

### Clauses turned

Round 53 failed seven clauses that round 54 does nothing to mend, and each becomes a reading. K1
asked the mouth founders to land where a column holds five times a random column's corpse energy.
In round 53 the medians were 3.9, 3.2 and 4.5. E3 asked a line
with ten living pure stomachs, and the best had two. LS2 and LS3 asked the lifespan gene's mean to
start near 1 and rise; seeds 2 and 3 started at 0.78 and 1.24. M4 asked a stomach bud to bring under
2% of a line's income, and seed 2's brought 4.8%. EK4 asked at most a fifth of the one-part pool
stomachs to breed, and pooled they bred 30%. W1 failed in seed 3 by one founder of 295, about 1%
under its column's mean, as it did in round 52; it becomes a reading too. N1 and N2 asked round 53's
own change and are read against round 53 without a bar. A clause expected to fail asks nothing.

## The clauses carried from round 53

Each is asked as 0129 asked it, with round 53's same seed as the baseline where one is named.

| # | prediction, in short | bar | round 53 |
|---|---|---|---|
| K2 | at least nine in ten of the mouth founders land over a corpse | 3 of 3 | all, in every seed |
| K3 | at least 30 corpses eaten by 30,000 s | 3 of 3 | 256, 241, 224 |
| LS1 | every birth carries the lifespan gene, and the median age at an aged death rises across its three bins | 3 of 3 | held |
| LS4 | the living's mean lifespan gene at the end under 2.5 | 3 of 3 | 1.03, 0.82, 1.36 |
| A1 | deaths by age at least a fifth of the dead at the end | 3 of 3 | 0.52, 0.61, 0.45 |
| A2 | the median age at an aged death in 1,500 to 4,500 s | 3 of 3 | 2,293, 1,880, 2,887 s |
| A3 | every aged death holds a reserve, and the stats count equals the rows | 3 of 3 | held |
| B1 | the audit under 0.1 J and the matter residual under 1e-5 units at every sample, nothing diverges, the run ends on its budget | 3 of 3 | held |
| W2 | a pool founder's first mouth reading over its landing density, the median in 0.02 to 0.3 | 3 of 3 | 0.16, 0.17, 0.17 |
| W3 | the landing cell over its column's mean, the median at least 1.6 | 3 of 3 | 1.89, 2.04, 1.84 |
| G1 | births paid from a gestation account | 3 of 3 | 7,315, 10,455, 7,304 |
| G2 | the share of gestating children lower in the last 5,000 s than in 3,000 to 8,000 s | 2 of 3 | failed in seed 1 (0.14 to 0.16), held in 2 and 3 |
| G3 | accounts held at death at least a fifth of what was banked | 2 of 3 | 40%, 37%, 40% |
| G4 | banked, less held, per gestation birth at least 50 J | 3 of 3 | 64, 54, 64 J |
| C2 | no checkpoint waits, and 300 checkpoints | 3 of 3 | held |
| C3 | no starved body dies solvent | 3 of 3 | held |
| V1 | a faithful film window from the round's own checkpoints | one per seed | not recorded |
| V2 | the member check names nothing on seed 1's 15,000 s checkpoint, and a resume from it to 16,000 s agrees with the run | seed 1 | not recorded |
| M1 | at least 100 births carrying a bud | 3 of 3 | 1,184, 496, 646 |
| M2 | at least three quarters of bud births built the bud | 3 of 3 | 95%, 100%, 99% |
| M3 | a line from a stomach bud on a leaf has ten living at the end | 1 of 3 | 39, 57, 13 |
| O1 | alive at 30,000 s between 2,000 and 16,000 | 3 of 3 | 7,403, 7,291, 7,607 |
| O2 | under 25,000 alive at every sample | 3 of 3 | at most 7,670 |
| F3 | no line rooted in a pool founder has ten living at the end | 3 of 3 | the largest 2 |
| P1 | the whole run at or above real time | 3 of 3 | 2.3x, 2.8x, 2.1x |
| EK2 | the cap's counters agree with the rows, and a one-part pool stomach is cut 17 to 18.5 J | 3 of 3 | 17.76 J |
| EK3 | the one-part pool stomachs' median age at death above 300 s | 2 of 3 | 736, 700, 896 s |
| X1 | the snow's patchiness at the end between 0.4 and 1.0 | 3 of 3 | 0.44, 0.56, 0.52 |
| LC | the header carries `leaves where they earn most`, and the clamp's pile-up is absent | 3 of 3 | held |
| O4 | in the line with the most living at the end, the jointed share below its share at 16,000 s, where that line was a fifth jointed then | every such seed | not asked in any seed |
| S3 | a rigid stomach child dies young, median under 1,500 s, where ten have died | every such seed | 344 and 1,076 s; not asked in seed 2 |
| H1 | the corpses' joules over ρ at least a tenth of the standing matter | 3 of 3 | 0.23, 0.27, 0.22 |
| H2 | at least a quarter of the corpses' joules settled | 3 of 3 | 0.88, 0.89, 0.88 |
| H4 | the corpses standing lie between the deaths of the last 7,000 s, less those eaten, and those of the last 9,000 s | 3 of 3 | held |

The readings are K1, K5, K6, EK1 and EK4 to EK7, W1, E1 to E3, and LS2, LS3 and LS5. So are M4, C1,
V3, O3, S1, S2, R1a, R1b, L1 to L4, A4, H3, H5, LK1, LK2, N1, N2, J3 and J4.

## The two-sided readings

Three readings could go either way, and I set no bar on them. The first is the jointed count, 947 at
5,000 s in the screen against round 53's 917. Swimming costs a body work and may buy it food, and I
do not know which wins. The second is the crowd, 2,046 against 2,065. The third is the work paid
(J3), per jointed body, early and late. If selection finds that a push pays, it rises from the
screen's 0.004 W; if it does not, it stays there. The nursery's tank found that in an even larder a
blind, steady swim pays at a rich density and not at the round's, so I lean to its staying low. That
is a guess. Beside them J4 reads how the drive genes move and the share of the living whose brain
reads the scent, 0.19 at 5,000 s in the screen. It also reads the living's median adult scale, since
the owner asked to watch for bodies getting smaller.

## What the round does not ask

Whether a nursery brain helps. The nursery ran beside the round's preparation (logbook/specs/nursery-plan-joint-drive.md). In an even larder
every pilot evolved a blind, steady swim whose knockout scored what it did, so there was no brain to
inoculate, and the round carries none (D138). A tank with the snow in islands rewards a hand-wired
forager over a blind swim; its pilots run beside the round.

Whether a body stops the way a real one does on the bed: D137's friction is built and off until its
value is benched. Whether drag should be fitted to each creature's shape: D136 left that for later.

## The reader

The round's reader is `scripts/reads/r54-read.py`. It is round 53's reader with round 53's same seed
as the baseline. The seven failed clauses and N1 and N2 are readings, K4 is a bar again, and J1 to J4
are added. J1 and J3
average over the same seconds in the seed and in its baseline. J2 reads the per-body list in each
steering row and keeps the jointed bodies, which the lineage's birth rows name. That way a rigid body
sinking toward a corpse on the bed does not count as steering. Across all bodies in the screen
the smellers' margin was −0.017, and among the jointed bodies it was −0.005. The rigid bodies that
cannot smell drift up the scent as they sink, and that is not steering. The summary's
tally now counts N3's per-seed rows, which round 53's printed as held in 0 of 0.

## Before the launch

The pre-registration's record is the commit that adds this entry, on a clean tree. Every seed's
manifest names it as `gitCommit`, with no DIRTY mark beside it. The build is the branch `round-54`,
launched from its worktree `scratch/wt-r54` into the main tree's runs. Seed 1's `configHash` is
checked against the screen's, `4070da9c8eeca51e`, and its source hashes against the screen's:
`coreHash e58a4c25…`, `dynamicsHash dd9dbdc6…`, `farmHash 216970c2…`. Between the screen's commit
(`7a3343d`) and this entry's, only the reader and this entry changed.
