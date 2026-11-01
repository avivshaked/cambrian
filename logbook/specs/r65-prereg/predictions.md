# The smell screen: does a carried leaf smell send grazers between oases?

*By the coordinator on 2026-11-01, from an Opus subagent's draft, before any world of the screen ran. Committed before the screen launches, which it
does from a clean worktree at this file's commit, with its own Release farm. It is D270's screen, designed in
`logbook/specs/smell-plume-design.md`, and changed here so that its answer applies to the film's round, which runs on
true-shape contact (D272). Where the two differ, this file holds for the screen.*

## The question

Today a grazer between groves smells nothing. The leaves' smell ends at 6 m, and groves lie 20 to 30 m apart. D270 adds a
far smell on a 5 m grid that the current carries, and a current sense that tells each part which way the water runs over
the ground. The realistic form, R, reads the far smell's strength. The overpowered form, O (D262), adds its gradient. The
question is whether either sends grazers from grove to grove, feeds them sooner and raises the leaves' share of their
income, in D's world under true shape, and at what pace.

## The worlds

Six worlds run for 24,000 s: C, R and O on seeds 6202 and 6203.

| World | Env file | What it is |
|---|---|---|
| `r65-c-6202`, `-6203` | `rounds/env-r65-c.ps1` | D's world under true shape with the smell off, which is `env-r64t-true.ps1` |
| `r65-r-6202`, `-6203` | `rounds/env-r65-r.ps1` | C with the far smell's strength and the current sense |
| `r65-o-6202`, `-6203` | `rounds/env-r65-o.ps1` | R with the far gradient as well |

- **Every world runs under true-shape contact**, so the answer is about the film's round. The design had run D's world
  under spheres.
- **No recorded world is the control.** The nearest recordings, r64t's two true-shape worlds, ran before the card's GJK
  fix (`fe0548e7`). That fix ended a few bites in a hundred that landed at a distance. So C runs on both seeds.
- **The dials** are the build's defaults: 5 m cells, a 3,000 s lifetime, a current half-speed of 0.01 m/s. The founding
  bench sets the plume's half-scale, which goes into `env-r65-r.ps1` before this file is committed. D's world lists no
  nose, so every part reads the far smell, and the near cone stays at 6 m.
- **Every world is founded.** A resumed world cannot change its contact form, and the plume's grid is state. A world the
  heat stop or a driver event ends is resumed from its last checkpoint and carries on as the same world: the build
  carries the fix that writes the remains' exposure into the checkpoint (branch `resume-exposure`), and step 2 checks
  that a resume repeats its run.

## The founding bench sets the half-scale

A part reads the far smell as s / (s + k). The bench sets k to the far smell 20 to 40 m downstream of a grove, so that a
grazer there reads 0.5. Its reader is `scripts/reads/smell/plume-bench.py` (`a8425c89`). A grove is g2-read's: the disc
of r_eff + 10 m round an island of at least 5 columns that holds a leaf. The current is not in the dump and turns faster
than the plume ages. So downstream is the bearing of the density-weighted centroid of the annulus 20 to 40 m past the
grove's edge. The figure is the median density within 45 degrees of that bearing, pooled over groves. The reader prints
the median over all bearings and each grove's drift beside it.

The design named a 1,200 s founding. The coordinator ruled 6,000 s, since 1,200 s is too short, for two reasons.

- A grove that emits steadily holds 1 − e^(−t/3000) of its final plume after t seconds. That is 0.33 at 1,200 s and 0.86
  at 6,000 s (calc).
- The water moves 24 m in 1,200 s at the mean current of 0.02 m/s. The annulus's outer half is still at the plume's
  leading edge, which reaches 40 m past the grove after about 2,000 s (calc).

The reader's test world (arm O under spheres, 600 s) agrees: its downstream figure, 0.053 m² s/m³, was still rising. The
leaves are steadier. The islands' lit leaf area in `r64t-t-6202` was 256 m² at 1,200 s and 216 m² at 6,000 s.

So the bench founds R on seeds 6202 and 6203 in one card group for 6,000 s. That takes about 30 minutes at r64t's early
pace of 3.5 times real time. k is the median of the 6202 figure over the dumps from 4,500 to 6,000 s. The 6203 figure and
the 1,200 s figure are reported beside it. The bench runs at the default k of 1. I expect that to move the figure less
than the seed does (a guess).

### The bench's result

The bench ran on 2026-11-01 from `e663768b` (`rounds/bench-r65.ps1`), both worlds to 6,000 s at 2.89 and 2.84 times
real time, books closed. Its output is `bench/plume-bench.txt` beside this file. The
downstream figure rose until about 3,600 s and held from there: on 6202 it was 0.25 at 1,200 s, 1.41 at 3,600 s and
1.28 at 6,000 s (m² s/m³). **k is 1.31**, the median over 6202's sixteen dumps from 4,500 to 6,000 s (range 1.19 to
1.60). 6203 gave 1.61. The 6,000 s figure is 5.2 times the 1,200 s one, inside the 2.5 to 6 predicted below. The
prediction for k itself, 0.2 to 0.6, was wrong by a factor of two or more, written before the bench ran and kept as
written. Downstream holds about five times the median over all bearings at 6,000 s (0.28 on 6202), and one grove a seed
shows little drift by the end (0.14 and 0.29).

## Checked before launch

On the merged build, in this order:

1. **The bench**, above. k goes into `env-r65-r.ps1` with the bench's run named beside it.
2. **The identity sitting.** The smell and true shape have never run together on the card. C, R and O on seed 6202, at
   the bench's k, are founded in one card group for 600 s at group 32, then at group 64.
   - `gpu-accept.py` at 32 against 64 exits 0 for each of the three.
   - C's `configHash` is `2991f19e83d0a446`, r64t's true-shape hash, or every field that moves it is named and traced
     to a tunable added since r64t at its off value. R's and O's configs differ from C's only in the plume form, its
     half-scale and the current sense, and their stats show the plume emitting.
   - **A resume repeats its run.** The sitting at group 32 writes checkpoints every 300 s. R and O are resumed from their
     300 s checkpoints to 600 s as one group (a card group is two worlds or more), and `compare-run-rows.py --from 300`
     against each founded world finds no difference in any row, and lineage byte-equal.
   - The farm, Dynamics and Core hashes are recorded here and reappear in the screen's manifests.
3. **C is held to no recording.** `compare-run-rows.py` C at 32 against `r64t-t-6202`'s prefix is reported and gates
   nothing. The fix writes the card's single-precision GJK uncontracted. So C may part from r64t at its first touching
   pair (inference). A match through 600 s would say the fix had not bitten by then, and a part says when it did. Neither
   bears on the screen, whose control is C itself. What C must show is that the card agrees with itself, which step 2
   checks.
4. Each world's header names `trueshape`, and R's and O's name their plume form and the current sense, or the world is
   stopped.

### The checks' results

The sitting ran on 2026-11-01 from `d89896b7` (`rounds/identity-r65.ps1`, the half-scale at 1.31). Its hashes are core
`946858c7`, Dynamics `7076166a` and farm `289d0397`. Runs are under `scratch/r65/identity/`.

- **Groups 32 and 64 agree.** `gpu-accept.py` exits 0 for C, R and O: stats identical on all 60 samples, digest on all
  601 rows to step 60,000, lineage byte-equal (1,465, 1,052 and 1,183 lines). Every world ended at its budget with its
  books closed.
- **C's `configHash` is `2991f19e83d0a446`**, r64t's. The configs read `TrueShape` in all three; R reads `Strength` and O
  `Gradient`, both at half-scale 1.31 with the current sense on; C reads no plume and the sense off.
- **A resume repeats its run.** R and O resumed from their 300 s checkpoints to 600 s match the founded worlds on all
  300 digest rows and on every world column of all 30 stats rows. Lineage is equal (843 and 970 rows). Only the wall
  clocks and two counters of the process's own work since it started (`harnessBodySteps`, `fluidLinkSteps`) differ.
  This is D273's first test in a world.
- **C against `r64t-t-6202`** (reported only): equal to 60 s, parting at 70 s in `workJoulesPerSecond`. That is where
  the card's GJK fix first changes the true-shape world.

## Readouts

A grazer is a body born with attack and intake both above 0. No window is judged on fewer than 30 (D244).

1. **Grazers eat more leaves.** Over 21,000 to 24,000 s, the leaf share of bite income (`income-split.py`) is at least
   C's plus 0.05, in R and in O, on both seeds.
2. **Grazers travel between oases.** Over the same window, crossings per grazer life (`g2-read.py`, crossing gap 10 m)
   are at least 1.5 times C's in R and twice C's in O, on both seeds. Reported beside it: the crossings' median length,
   and the share of grazer fixes more than 20 m past every grove's edge.
3. **Grazers find a patch sooner.** The gather lag is the median of g2-read's best lag between an island's bloom and its
   crowd, over islands of at least 5 columns whose correlation reaches 0.3. It is at most 0.75 of C's in R and half of
   C's in O. It is read over 12,000 to 24,000 s, not the last window, since a bloom's period of 5,000 to 10,000 s is
   longer than a window. Reported beside it: newborns' median time to first bite, over bred grazers born in the last window, and the
   share that die first.
4. **The pace holds.** A world under 0.5 times real time is stopped at once. On each seed, R and O step within 10% of
   C's pace (`pace-calc.py`). Each world's body-seconds per wall second are reported beside it. A world with more bodies
   steps slower, so a gap in pace that vanishes per body is read as crowding.

Also reported: the realised reading, `plume-bench.py` on R and O in the last window against k; the grazer floor from
12,000 s; the fewest leaves after 3,000 s (`overshoot-read.py`); and kills (`grazer-deaths.py`, `cannibal.py`).

## Predictions (inference, written before any run)

C's numbers are taken from r64t's true-shape worlds, on the unfixed card. There, grazers took 0.75 and 0.80 of their
last-window bite income from leaves. They made 0.025 and 0.028 crossings per grazer life, a crossing's median 32 m fix to
fix. Gather lags were 2,360 s on 6202 and 1,535 s on 6203. I expect C near each.

- **The bench**: k between 0.2 and 0.6 m² s/m³, and the 6,000 s figure 2.5 to 6 times the 1,200 s one. The last window's
  leaf area is about twice the bench's, so a grazer at the bench's place then reads about 0.6 to 0.75 (guess).
- **Mark 1**: C 0.73 to 0.82. O gains 0.03 to 0.10 and passes on at least one seed (moderate). R gains 0 to 0.05 and
  fails on at least one seed (moderate). A grazer drawn to a grove meets more grazers there to bite.
- **Mark 2**: C 0.02 to 0.04. R 0.9 to 1.4 times C's, failing (moderate). O 1.2 to 2.5 times, passing on one seed at
  most (low). Inside a grove the gradient points at that grove, so O may hold grazers at home.
- **Far from groves**: the share falls in R and O, by a quarter to a half in O (low). Newborns land up to 30 m from their
  parents, inside the plumes.
- **Mark 3**: C 1,500 to 2,400 s. O 0.6 to 0.9 of C's and R 0.8 to 1.0, both failing (low). The plume trails a bloom by
  its own build-up and a transit of 1,000 to 2,000 s. Newborns' first bite comes 25 to 50% sooner in O (low).
- **Leaves**: O's fewest after 3,000 s stay above 500, ending at 0.6 to 1.0 of C's (moderate).
- **Mark 4**: R and O within 5% of C per body, and within 10% raw unless O's grazers outnumber C's by a quarter.

## What would show each wrong

- The 6,000 s bench figure under twice the 1,200 s one, or over ten times it.
- R passing mark 1 or 2 on both seeds. The design's brain census says founder weights cannot cast.
- O below C on mark 2 while above it on mark 1: the gradient holds grazers at home.
- The realised reading in R above 0.9 or below 0.1: k left its range, and R's result says little.
- Mark 4 failing per body as well as raw: the plume costs more than the design's 1 ms a metabolic step.

## What follows each outcome

The screen decides nothing for the film's round. D272 keeps the smell off there until this screen has run, and turning it
on is the owner's ruling.

- **O passes, R fails**: O is dialled back, noise on the gradient and then a coarser one, and the nursery trains casting.
- **Both pass**: R carries forward and O retires.
- **Both fail**: the nursery comes before another world screen.
- **O passes mark 1 and fails mark 2**: the smell feeds grazers where they are. The first dial-back is then a gradient
  that fades inside a grove (inference).

## Rules chosen cheap, re-asked here

What true shape changes for the smell:

- **A grazer that follows a plume must still touch a leaf.** The far smell brings it to the grove, and the 6 m cone to
  the leaf's centre. The bite then needs the true surfaces to meet, and under true shape a grazer bit about half as often.
- **Smell follows lit area** (D167). Thin blades smell as strong as thick leaves, though true shape makes blades the
  hardest to touch.
- **True shape left about twice the leaves**, so plumes are stronger than under spheres. That is why the bench runs
  under true shape.
- **The current sense reads in midwater.** Its realistic end reads only on the bed or coral, and under true shape a bed
  touch needs the true surface.

What the smell changes for true shape:

- **Grazers drawn to groves crowd there.** More pairs reach GJK, which costs pace. True shape made lethal
  grazer-on-grazer kills five- to eightfold rarer, so crowding costs grazers less than under spheres.
- **Inputs are free in D's world** (`neuralCostPerConnectionWatts` 0). A founder wires the 4 new inputs a part in R, and
  7 in O, to every output (D147) at no cost.
- **Readings are held once a metabolic step**, up to 0.5 s old. At a grazer's few centimetres a second that is under 5 cm.

## Pace, and one group or two

A card group steps each world on its own thread over one device, and one pool of 8 host threads serves them all. In the
table, look at how a world's pace falls as the group grows.

| Group | Worlds | Pace over the run | Last 6,000 s | Body-seconds per wall second, summed |
|---|---|---|---|---|
| `r64t` | 3 | 2.44 to 2.80 | 1.83 to 2.23 | 30,800 |
| `r64` | 5 | 2.21 to 2.40 | 1.78 to 1.96 | 33,200 |
| `r63b` (10 threads) | 8 | 1.22 to 1.40 | 0.93 to 1.13 | 41,600 |

Six true-shape worlds at r64t's populations hold about 27,000 bodies on average. If they reach 38,000 to 45,000
body-seconds per wall second, each steps 1.4 to 1.65 times real time over the run. That is about 1.0 to 1.3 in the last
window, and 4.0 to 4.8 hours in all (inference). The plume adds about 1 ms to a metabolic step of about 200 ms.

Two groups of three, one per seed with C, R and O in each, would each step as r64t did. That is 2.4 to 2.7 over the run
and 1.8 to 2.0 at the end, 2.6 to 2.8 hours a group, 5.2 to 5.6 hours back to back.

The screen runs as two groups of three, 6202 first. They cost about an hour more of card time, and in return:

- the worlds stay near the owner's aim of about 2 times real time;
- mark 4 compares R and O with C inside one group on both seeds;
- a driver event like the one that ended `r64s2` stops three worlds, not six;
- the identity sitting has the first group's makeup, so its pace checks that group's.

The seeds then run hours apart, so their paces are not compared. Group makeup does not move a world's bits.

## Open points

1. **The reads are built** (`bd4b825a`, under `scripts/reads/smell/`): `gather-lag.py` (mark 3, 2,350 s on
   `r64t-t-6202` over 12,000 to 24,000 s), `first-bite.py` (from the pose stream's ate mask, version 5; it refuses an
   older stream, and its 10 s frames set its resolution), `far-share.py` (groves holding a leaf at the second; 0.037 on
   `r64t-t-6202`) and `throughput.py`.
2. **No check is known to have run the far smell under true shape on the CPU accelerator.** Without one, the identity
   sitting shows the card agrees with itself, and not with the solver.
3. **Not yet written**: `rounds/launch-r65.ps1` and the bench's and the identity sitting's launchers. Threads, mask and
   `-AllowBeside` follow the day's machine ruling.
