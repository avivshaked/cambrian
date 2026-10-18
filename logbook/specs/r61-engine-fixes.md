# Round 61's engine fixes: giant parts, the intake's columns and the pace guard, each proven to leave every bit (2026-10-17)

On 2026-10-17, two of round 61's worlds slowed on the graphics card for two different reasons, and three changes
to the engine went in that day. The giants fix makes a part with a huge contact sphere cheap. The intake's columns
make feeding cheap when hundreds of mouths share a world with sixty thousand corpses. The pace guard makes the farm
notice a slowing world by itself, stop it at a checkpoint and say why. None of the three changes a world's
numbers. This report says what each found and fixed. It also gives every gate span that proved the build
bit-identical, so that the resumes of round 61's extension can cite it. The speed agent wrote it for the
coordinator.

The report leans on a few terms. The farm is the program that steps the worlds, and the card is the graphics card it steps them
on. A checkpoint is a world's whole state, saved every 1,500 simulated seconds and at a stop. A resume starts a
run from one, and a run resumed on the same build must write the same rows, bit for bit, as the run that never
stopped. A row is one line of a run's files: the stats file gets one every 10 simulated seconds, and the lineage
file one per creature born. Pace is given as times real time: 1.6 means 1.6 simulated seconds for every second
of wall-clock time.

## The three fixes, and what each found

### The giants fix keeps a huge part out of the contact grid

Round 61's r61-p6103 slowed tenfold from about 45,300 s. Contact on the card treats each part as a sphere the
size of its half-diagonal. The world's grid buckets every sphere by the cells it covers, and one thread walks each
sphere's cells every step. One body had grown a photosynthetic ribbon by adding modules in life (HANDOFF's
giant-parts bullet has the genome). Its sphere's half-diagonal went to 10.4 m, then to 18.4 m, which covered
15,000 cells and then 81,000.

The fix, at `b81ca261`, keeps any sphere that covers 1,024 cells or more out of the grid. The setting is
`EVOSIM_GPU_GIANT_CELLS`, and 0 restores the old path. Each giant gets its own list of the rows near it, and
every other query tests the giants directly. Two rows share a cell when, and only when, their cell boxes meet.
So every query finds the same rows as the cell walk did, and the contacts kept are the same bits.

### The intake's columns make feeding scale with neighbours, not with the whole world

The extension's two control worlds slowed as well. The world r61-c6101x went from 0.92 to 0.55 times real time
between 48,000 and 55,500 s. It had no giant: its largest part grew from 1.81 m to 2.35 m. The physics' wall time
stayed flat, and the wall time of the world's own rules went from 490 to 1,267 ms a simulated second. A build
with a timer on each of the world's phases found the cause. Every step, each mouth tested every corpse in the
world, so the intake cost grew with mouths times corpses.

| world, second | mouths | corpses | intake, ms a step |
|---|---|---|---|
| r61-c6101 at 48,000 s, the old intake | 65 | 59,892 | 25.3 |
| r61-c6101x at 55,500 s, the old intake | 398 | 62,754 | 156.3 |
| r61-c6101x at 55,500 s, the columns | 398 | 62,756 | 13.7 |

The table shows the cost following the mouths, then falling more than tenfold with the fix. The fix, at
`eba1ad60` off the round's own build, sorts the corpses into vertical columns at least as wide as the step's
longest reach. Each mouth tests only the corpses in the nine columns around its
own. The candidates are put back in their original order and run through the same tests. So every sum is taken in
the same order, and the result is the same bits. A new test, `IntakeColumnsTests`, holds the columns against
the old walk at reaches of 0.5, 1 and 5 m. The default Core suite passed 1,191 of 1,191 on it.

### The pace guard stops a slowing world with a checkpoint and a reason

r61-p6103's slowdown was found by an agent reading a stats file an hour after it began. The owner ruled that day
that a world running too slow should stop by itself with a detailed reason, and
a proposal set out how, absorbed into D197 on 2026-10-18. Its five items were built
first on a branch of their own. Every stats row gains counters of the contact work, from the most cells
one sphere covered to the largest part. A pace guard stops a slowing world at a checkpoint. The reason written
into the run names the slow part and quotes the card's own timings. A first overflow of a contact cap forces a
checkpoint and is logged, and the card group runs `scripts/pace-check.py` every 15 minutes.

The first gate showed the guard's trip rule failing, and the rule was replaced. The first rule tripped after 20
stats rows in a row, each more than twice as slow as the trailing hour. A row is ten simulated seconds however
long it takes, so on the slowest world that waited 38 wall minutes. Worse, a 2.4-fold slowdown ran 17 wall
minutes without tripping at all. Single rows just under the line broke each streak and joined the trailing mean,
which crept up from 0.9 to 1.66 seconds of wall a simulated second. The rule now at `41086157` compares two
windows of wall time instead. It reads the pace over the last 10 minutes against the pace over the 60 minutes
before them. It trips when the first is more than twice as slow, with each window holding two rows or more. A
window is a sum, so no single row resets it. The card probe then runs for one row, down from three, before the
stop. The ratio of one half is still the owner's to rule on.

## How the merged build was put together

The three fixes were merged in this order, each a commit on the branch `r61-merged`:

| commit | what it adds |
|---|---|
| `9b5ed249` | the pace guard (`ff63f197`) merged with the intake's columns (`eba1ad60`), both off `f3ef8f76` |
| `308e5372` | the card's kernels regenerated from the guard's templates |
| `9162f528` | the round launcher's resume step and the card group's per-world resume seconds; scripts only |
| `41086157` | the pace guard's window rule |

The giants fix was already in the round's build. r61-p6103r ran on it from 46,050 s, and the extension ran on
`f3ef8f76`, which carries it.

The suites passed on each build before its card time. On the first merged build the farm's tests passed 56 of 56.
The test that holds the card's double-precision path to the solver passed at a giants threshold of 8 as well. On
the window rule's build, the farm's tests that touch the guard, its settings, the manifest and the generated
kernels passed 53 of 53. Nine of the dynamics' contact tests fail on the merged build and on the round's own build
alike. Their recorded fixture, r60fix-s4, predates the desert burial setting, so they need re-recording and say nothing about
these fixes.

## Every gate span, and its verdict

Each span below resumed a recorded world from a checkpoint on the card, alone, with every setting as round 61's.
Each was then compared, row by row and file by file, with a reference run by `group-resume-compare.py --lone`.
The compare leaves out the wall-time fields, which differ in every run. Against a run recorded before the guard,
it also leaves out the twelve counter fields the guard adds, since the older run has none. Against a run on a
guard build, every field is compared.

The giants fix was gated on its own build before the round's runs resumed on it. Look for "same" in the last
column, and for the pace where the old path crawled.

| span | build | world and span | reference, fields | rows compared after the resume second (lineage, stats, digest, absorptive, positions) | pace | verdict |
|---|---|---|---|---|---|---|
| b | `b81ca261` | r61-p6103 from 45,000 to 46,050 s | the round's r61-p6103 | 11,207, 105, 1,050, 72,656, 105 | 1.61× in 10.9 min | the same |
| c1 | `b81ca261` | r61-p6101 from 45,000 to 48,000 s | the round's r61-p6101 | 45,125, 300, 3,000, 553,860, 300 | 1.83× in 27.3 min | the same |
| c2 | `b81ca261` | r61-p6101 from 45,000 to 46,500 s, giants at 8 cells | the round's r61-p6101 | 22,977 lineage rows to 46,500 s | 1.69× in 14.8 min | the same to its end |

Span c2's compare ran without an end second, so it reported a difference at the first row past 46,500 s, where
c2 stopped and the round went on. Every row up to its end matched.

The merged build's gate ran in two parts. The first part, g1 to g7, ran on `308e5372`. Two of its spans, g4 and g6,
never started. The previous span's driver watch was still exiting when they launched, and the card's own check
refused them. The second part, h2 to h6, ran on `41086157` after the window rule went in. It repeated the forced trip,
the two spans that never ran, and the slow world. Look for "the same" everywhere, and for where each guard trip
stopped.

| span | build | world and span | reference, fields | rows compared after the resume second (lineage, stats, digest, absorptive, positions) | pace | verdict |
|---|---|---|---|---|---|---|
| g1 | `308e5372` | r61-p6101 from 45,000 to 46,000 s, guard on | the round's, guard counters left out | 15,322, 100, 1,000, 192,550, 100 | 1.69× in 9.8 min | the same |
| g2 | `308e5372` | as g1, a test's trip at 45,500 s; stopped "slow" at 45,530 s with a checkpoint | the round's, counters left out; and g1, every field | 8,452, 53, 530, 103,959, 53 | 1.62× in 5.5 min | the same, both |
| g3 | `308e5372` | g2 resumed from its slow checkpoint at 45,530 to 46,000 s | g1, every field | 6,870, 47, 470, 88,591, 47 | 1.61× in 4.9 min | the same |
| g7 | `308e5372` | r61-p6101 from 48,000 to 48,500 s | the extension's r61-p6101x, counters left out | 7,959, 50, 500, 90,512, 50 | 1.67× in 5.0 min | the same |
| g5 | `308e5372` | r61-p6103 from 45,000 s, giants off, 2 minutes' warm-up; first overflow at 45,420 s forced a checkpoint; tripped at 46,000 s, stopped at 46,030 s | the round's, counters left out | 10,966, 103, 1,030, 71,261, 103 | 0.25× in 68.6 min | the same |
| h2 | `41086157` | as g2; stopped "slow" at 45,510 s after one probe row | the round's, counters left out; and g1, every field | 8,159, 51, 510, 100,116, 51 | 1.71× in 5.0 min | the same, both |
| h4 | `41086157` | r61-p6101 from 45,000 to 45,500 s, giants at 8 cells | g1, every field | 8,000, 50, 500, 98,198, 50 | 1.71× in 4.9 min | the same |
| h6 | `41086157` | r61-c6101x from 55,500 to 56,000 s | the extension's r61-c6101x, counters left out | 8,918, 50, 500, 86,880, 50 | 1.64× in 5.1 min | the same |
| h5 | `41086157` | r61-p6103 from 45,000 s, giants off, 2 minutes' warm-up; first overflow at 45,420 s forced a checkpoint; tripped at 45,570 s, stopped at 45,580 s | the round's, counters left out | 6,507, 58, 580, 39,709, 58 | 0.64× in 15.1 min | the same |

Every span closed its books. The energy audit's largest residual was under 0.002 J, against a limit of 0.1 J, and
the matter residual under 1e-6, against 1e-5. The spans of r61-p6101 and r61-c6101x passed no contact
cap and had no diverged body. The three spans of r61-p6103 (b, g5 and h5) passed the caps and had one diverged
body, 79817, in the first physics step after 45,080 s, counted in the row of 45,090 s. The round's own run did the same, and their rows match it. The spans g5 and h5 also ran the old slow
path on purpose.

## What the spans show

The fixes leave every bit as it was. A resume on the merged build writes the round's rows and the extension's
rows. Switching the card probe on in the middle of a run changes nothing (g2, h2), and a world the guard stopped
resumes to the unbroken run's rows (g3). The giants threshold does not matter to the result (c2, h4).

The guard now catches what it was built for. On the same slow world, the first rule stopped after 68.6 wall
minutes, once the tenfold stage had run 38 of them. The window rule stopped after 15.1, about eight wall
minutes into the 2.4-fold stage, which began at 45,350 s. Its reason reads:

```
stopped slow at t=45580 s, at a checkpoint: 1.96 s of wall a simulated second over the last 10 minutes' 31 rows
from t=45260 s, against 0.95 over the 60 minutes before them (2.1 times slower); ... the largest part a
half-diagonal of 10.41 m, on body 80271 of 7 parts
```

Body 80271 is the first of the two giants HANDOFF describes. The reason names in one line the cause that a stop
and a probe build took an hour to find on the 17th. Two small faults in the wording remain. When the run is
younger than an hour, the reason still says "the 60 minutes before them". A test's trip in the first ten minutes
quotes a reference of 0.00. Neither changes when the guard trips.

The intake's columns give the slowed control world its pace back. The world r61-c6101x ran at about 0.55 times
real time at 55,500 s on the old intake. On the columns it ran at 1.64 times over the 500 s after (h6). That is one
five-minute span with nothing else on the card, so it is a reading rather than a timing measurement.

## Where the runs and the code are

The gate's runs and logs were kept in a scratch folder, which is not kept. The code is in the branches, and this
report holds the numbers:

| branch | head | holds |
|---|---|---|
| `r61-giants` | `b81ca261` | the giants fix |
| `r61-mouths` | `eba1ad60` | the intake's columns |
| `farm-guard` | `ff63f197` | the pace guard as first built, before the merge |
| `r61-merged` | `41086157` | all three, the window rule and the resume step |

The four extension worlds were stopped for the fix, each at its own second:

| world | stopped at |
|---|---|
| r61-p6101x | 58,640 s |
| r61-c6101x | 56,960 s |
| r61-p6102x | 59,150 s |
| r61-c6102x | 57,610 s |

The resume step's preflight passed on `41086157`, and the
coordinator launched them on it at 20:50 as the group r61-x2, each to 72,000 s.

Two loose ends go with the build. The round launcher's pool-list check reads git's status of `rounds/r59-pool.tsv`.
In a checkout that converts line endings, that status reads changed when the bytes are the committed ones. It
should compare content instead. And the nine dynamics contact tests above need their fixture re-recorded.

## The night after: the reefs' fade measured, then computed once (2026-10-18)

D194, the decision that brings branching coral to round 62, asked for two pieces of engine work before the coral
build. One was a measurement of the reefs' share of a step. The other was to have the card compute the current's
fade near a reef once for each link rather than twice. Both were done on the night after the gate, once r61-x2 had
ended at 72,000 s. One fade turned out to cost about a quarter of a link's water work. Computing it once makes the
card's step about 11% faster and leaves every bit as it was.

### One fade costs about 168 thousand cycles a link

Every step, the card works out the water's velocity and its acceleration at each link of each body. Near a reef the
current fades, and both calls computed that fade over every reef. A build with the card's cycle counter read between
the parts of each link's work separates the two calls; it is built with the phase-probe switch. I ran r61-p6101
from its 48,000 s checkpoint for 240 s on two such builds, one at `41086157` and one with the change at `6d070df4`. Each printed 24
readings, and the figures below are their averages, weighted over the roughly 14,900 links of the world.

| per link, thousand cycles a step | fade twice (`41086157`) | fade once (`6d070df4`) |
|---|---|---|
| the water's velocity | 417 | 393 |
| the water's acceleration | 218 | 50 |
| the water in all | 635 | 444 |
| the fluid's drag | 78 | 85 |

The acceleration's part fell by 168 thousand cycles, which is the cost of one fade. Before the change, a link
spent about 336 of its 712 thousand cycles of water and drag on two fades. After it, a link spends about 168 of 528.

The card's own timings say the same in milliseconds. The water and drag passes went from 0.593 to 0.372 ms a step,
and the rest of the step did not move. So the card's step went from about 1.97 to about 1.75 ms. The fade was about
14% of the card's step and is now about 7%. Contact between bodies and the reefs cost little in either build, 10 to
22 thousand cycles of a body's 370 to 1,730. The host's share of the reefs, their light and shade, is not in this
measurement.

### Where a step's wall time goes, alone and in a group

The same night, r61-c6102x2 ran from its 72,000 s checkpoint for 420 s, alone on the card with the probe on. Its
pace in the group r61-x2 had drifted from 0.80 to 0.70 times real time. Alone it ran at 1.62 times, and 1.69 in a
run without the probe. The table splits its wall time, from the run's own stats rows.

| part | ms of wall a simulated second | share |
|---|---|---|
| the card's body step | 212 | 39% |
| the card's contact grid | 38 | 7% |
| the world's own rules, on the host | 216 | 40% |
| the harness and the file writers | 51 | 9% |
| in all | 545 | |

In the group, the same world's body step had taken 600 to 1,016 ms a simulated second, and its world part 345 to
384. The four worlds of a group share one card and one set of eight processor cores. So the drift was the share this
world got as the largest crowd, and nothing in the world itself grew. That last step is my inference from the two
splits. The world's passes, timed on the CPU at 48,000 s and at 60,000 s, grew only with the bodies and corpses.

### The change leaves every bit, in three comparisons

The two probe builds wrote the same rows to 48,240 s in every field. That is 3,827 lineage rows, 24 stats rows, 240
digest rows, 42,854 absorptive rows and 24 positions rows. The release build at `6d070df4` was then run on two spans of
the first gate and compared field by field.

| span | world and span | reference | rows compared after the resume second (lineage, stats, digest, absorptive, positions) | pace | verdict |
|---|---|---|---|---|---|
| f1 | r61-p6101 from 45,000 to 45,500 s | g1 | 8,000, 50, 500, 98,198, 50 | 1.75× (g1 1.69×) | the same |
| f2 | r61-c6101x from 55,500 to 56,000 s | h6 | 8,918, 50, 500, 86,880, 50 | 1.70× (h6 1.64×) | the same |

The change sits on the branch `r61-fade` at `6d070df4`, for round 62. The 6103 pair ran on without it.

### Two launcher faults found and fixed

The pool-list check named as a loose end above was fixed at `1d142557`, and merged into the merged build's branch
before the 6103 pair launched. A rewrite with the same bytes still left git calling the list changed,
even after a refresh of its index. So the script now writes the list only when its content would change, and the
launcher compares the list with the committed one line by line.

The gate's own runner did not pass a request for the card's probe through to `card-run.ps1`. That script clears the
probe's variable unless it is given its own switch. So the first probe of r61-c6102x2 ran without its probe, and was
run again. The runner now passes the switch.
