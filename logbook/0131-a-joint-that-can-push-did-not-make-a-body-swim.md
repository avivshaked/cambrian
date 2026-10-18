# A joint that can push did not make a body swim to its next meal

*2026-10-01. Written by the agent (Opus 5.5, in the session that relaunched the round) as the read of
round 54, whose pre-registration is 0130. All three seeds ran their 30,000 simulated seconds. The
verdicts are the round's own reader's (`scripts/reads/r54-read.py`), and its output is kept as
`logbook/specs/r54-read/read-30000.txt`. The other numbers come from the runs' own files, and the
sources table names each.*

Round 54 gave every joint a push. A brain could now drive a limb through the water, a corpse gave off
a scent that carried ten metres, and moving cost a body half its mechanical work. The round asked
whether a body would use that to swim to its next meal. It did not. The jointed bodies moved no faster
than the rigid ones in any seed. The bodies that could smell the dead did not head towards them. The
founders that eat the dead lived no longer than in round 53. More corpses were eaten than in round
53, in every seed and twice as many in one, but no body swam to them. In two seeds the tank went to a
line founded by a rigid leaf in the first seconds, and the joints went with the lines that lost. In
the third the winning line carried joints, and they mostly sat still.

## The round asked whether a body that can swim reaches its next meal

A few words first, for a reader new to the project. The world is a round tank of water 25 m deep in
this round, with a reef of rock columns and a gentle current. Every creature grows from a genome
that encodes its body and its brain. A leaf is a part that earns energy from light. A mouth is a part
that eats, in this world mostly the dead. A joint links two parts, and a body with one is jointed; a
body without one is rigid. A founder is a body placed in the world from outside rather than born, and
a line is a founder with all its descendants.

Rounds 52 and 53 set the founders that carry a mouth where the corpses lie, and they ate. A meal still
bought little. Round 53's mouth founders lived a median of 161, 179 and 204 s in its three seeds, and
no line rooted in a mouth kept more than one member alive. D132 named the wall: a mouth that cannot
swim to its next meal cannot live on corpses. So the owner ruled D135, D138 and D139. Each joint gets
a power and a bend from the brain, and the power pushes the limb along its length. A joint pushes
only when its neuron is above the joint's own threshold, so a silent joint costs nothing. Work costs
half its joules from the body's reserve.

The round's four questions were pre-registered in 0130. J1 asked whether the jointed bodies keep a
faster pace than the rigid ones. J2 asked whether the jointed bodies that smell the dead head up the
scent more than those that do not. K4 asked whether a mouth founder lives longer than round 53's, and
N3 whether the corpses are still eaten at round 53's rate.

The round had a long road to its launch. Its first seed was stopped at 5,510 s on the owner's ruling
(D140), to wait for the nursery to supply swimmers for seeds 2 and 3. The nursery is a separate small
tank where brains are evolved on their own. No nursery brain passed its test in the round's water, so
the owner ruled that the round would run without them (D142). Seed 1 was relaunched as `r54b-s1`, and
its rows repeated the first launch's to the last digit through 5,510 s, as the farm promises at any
thread count.

## Seed by seed, the founding lottery decided whether the tank kept joints

Seed 1 ran from 01:44 to 08:32 local time on 2026-10-01 at five threads, beside two nursery arms.
Seeds 2 and 3 then ran side by side at eight threads each, seed 3 ending at 12:56 and seed 2 at
13:37. All three ended on their budget at 30,000 s, from a clean tree on one world
(`configHash 4070da9c…`). In the table, look first at the line that held each tank and then at the
jointed bodies.

| | seed 1 | seed 2 | seed 3 |
|---|---|---|---|
| wall clock, threads | 407 min, 5 | 305 min, 8 | 264 min, 8 |
| alive at the end (round 53's same seed) | 7,147 (7,403) | 6,010 (7,291) | 6,190 (7,607) |
| births | 82,201 | 83,811 | 64,900 |
| the line holding the tank: its living at the end, its founder | 7,138: a leaf with a joint, from 9.5 s | 5,870: a leaf with no joint, from 0.5 s | 6,008: a leaf with no joint, from 5 s |
| jointed bodies at 4,000, 16,000 and 30,000 s | 948, 2,330, 1,702 | 268, 16, 4 | 101, 10, 7 |

As in round 49, one founder from the first seconds took each tank. Seeds 2 and 3's
winners had no joint. By 16,000 s their jointed bodies numbered 16 and 10. By the end not one of
them had a jointed parent, and the few left were newcomers dropped in from outside. Seed 1's winner
had a joint, and its line kept one in a large share of its members. That share fell from 53% at
16,000 s to 24% at the end.

While the round still ran, I read the collapse in seeds 2 and 3 as selection turning the joints off.
The lineage shows that the joints went with the lines that lost the founding lottery, and
selection inside the winning lines had nothing to turn off.

## Three of the four questions fail, and the corpses were eaten

The table gives each question's number in every seed against its bar. Read the last column first.

| # | asked | seed 1 | seed 2 | seed 3 | bar | verdict |
|---|---|---|---|---|---|---|
| J1 | the jointed bodies' speed over the rigid bodies', 5,000 to 30,000 s, as a multiple of round 53's | 0.968 against 0.921: 1.05 | 0.824 against 0.796: 1.03 | 0.700 against 0.656: 1.07 | 1.2, in 3 of 3 | fails |
| J2 | the jointed scent readers' heading up the scent, less the jointed non-readers', 20,000 to 30,000 s | −0.025 | too few readers (158 reads) | too few readers (90 reads) | +0.02, in 2 of 3 | fails |
| K4 | the mouth founders' median age at death, as a multiple of round 53's | 157.5 s against 160.75: 0.98 | 150 s against 179: 0.84 | 110.5 s against 203.75: 0.54 | 1.25, in 2 of 3 | fails |
| N3 | corpses eaten by 30,000 s, against round 53's | 332 against 256 | 647 against 241 | 459 against 224 | at or above, in 2 of 3; 721 pooled | holds; 1,438 pooled |

The speeds are over the ground. The current carries every body, so the ratio cancels most of it. A
ratio under 1 means the jointed bodies went slower than the rigid ones, as they did in round 53.
Round 54 moved each seed's ratio up by 3 to 7%, against the 20% asked.

J2 needs a thousand counted readings by jointed bodies that read the scent, and only seed 1 had them.
There the readers headed up the scent slightly less than the non-readers did. The share of living
bodies whose brain reads the scent fell in every seed. Founders draw their brains at random, so about
a fifth read it in seed 1 at 5,000 s, and their descendants did not keep it.

| share of the living whose brain reads the scent | 5,000 s | 15,000 s | 30,000 s |
|---|---|---|---|
| seed 1 | 19% | 3.0% | 2.7% |
| seed 2 | 13% | 4.2% | 1.7% |
| seed 3 | 2.7% | 0.1% | 0.3% |

The joints were barely used. A body pays for its pushes in work, so the work paid shows how much the
joints moved. Per living body it was 0.0021 W in seed 1. Round 53 paid 0.061 W for its old drive's
swing. In seeds 2 and 3 it was under 0.001 W. Seed 1's jointed bodies paid 0.008 W each over the
first part of the run and 0.0015 W over the last. A silent joint costs nothing under D139, and most of
seed 1's joints were silent.

So the mouths ate more without swimming. I think the corpses came to them. The mouth founders land
over the corpses (K2 holds in every seed), and more corpses fell where the crowd lived. That is a
guess, because the reader does not split a meal by how the eater reached it.

## Every carried clause with a bar holds

0130 carried 34 clauses with bars from round 53 and earlier rounds, and every one holds in every seed
that asks it. They are listed here by what they cover.

- the founders' placing and the pool: W2, W3, F3, EK2, EK3 and EK6
- growth and gestation: G1 to G4
- the senses' plumbing: C2 and C3
- the books: B1 and X1
- the stomach buds: M1 to M3
- where the crowd lives: O1, O2, O4 and LC
- the husks and the dead: A1 to A3, H1, H2, H4 and S3
- the corpses' landing: K2 and K3
- the lifespan gene: LS1 and LS4

 The world under the swim worked as built.
No film windows were cut, so V1 and V2 read nothing. No seed carried a nursery swimmer, so I1 and I2
read nothing either. The readings are in the read file, unjudged.

## What was seen in the theatre

The theatre is the project's viewer, and a round is looked at before it is written up. I drew each
seed at 30,000 s from its snapshot rather than replaying it. The bodies are set where the run recorded
them, in the pose it recorded, at the size its state stream gives. All three seeds looked alike. A
wide layer of leaves filled the tank from the glass to the middle, most of it within about 12 m of
the surface. The reef's dark flat tops and pillars hung into it from above. A handful of mouths sat in
the middle water, none near the bed. There were no one-metre columns like those that went unseen
through round 33.

The positions file agrees with the pictures. At 18,750 s seed 3's 4,563 bodies covered 4,080 of the
tank's one-metre squares. They spread about 40 m in each direction, and seed 2's were spread the same
at 23,320 s. Each
frame carries the label "old-run read". The pictures' worker is older than the round's build, so it
read the water's shape with the picture-only reader. That changes nothing in what is drawn.

## The swim waits on the nursery, and the owner rules what comes next

A joint that can push did not make a body swim to its next meal, because no line that held a tank
used its joints to move. In two seeds no winning line had a joint at all, so selection never got to
try. The founding lottery will not find the swim on its own. D142 already moved the next step to the
nursery. Its selection is being fixed there, so that a brain is chosen for what it does and not for
its luck with the draws. The first measurement, made this afternoon, found that two draws a
generation give each brain a standard error of about 105 J. That is two to five times the differences
selection has to find (`logbook/specs/nursery-plan-joint-drive.md`). A run at sixteen draws is
training tonight. If a brain passes its exam in the round's water, round 55 would carry it into a
seed, with an uninoculated seed beside it as the control.

The world rules stay the owner's. The two proposal files still at the root, the reactive thrust and
the foraging change, are carried by D127, D135 and D138. With this read they can be deleted as
absorbed, once the owner agrees.

## Sources

| claim | source |
|---|---|
| every verdict, the clause numbers and the readings | `logbook/specs/r54-read/read-30000.txt` and `.tsv` (`scripts/reads/r54-read.py`, default arms `r54b`) |
| seed 1's identity with the first launch through 5,510 s | `logbook/specs/r54-read/r54b-identity.py`, run 2026-10-01 at 05:45; recorded in HANDOFF |
| wall clock, threads, ending, alive at the end | each run's `run.json` |
| jointed bodies at 4,000, 16,000 and 30,000 s, and none inherited in seeds 2 and 3 at the end | each run's `stats.jsonl`, the `jointed` and `jointedInherited` columns |
| the line holding each tank, its founder and its jointed share | the reader's O4 rows |
| the share of the living that read the scent | the reader's J4 rows |
| the work paid | the reader's J3 rows |
| the spread of the bodies | `scripts/positions-read.py --summary` on `r54b-s2` and `r54b-s3`, 2026-10-01 |
| the pictures | `scripts/theatre-snap.ps1 <arm> -At 30000 -From snapshot`, in `scratch/snaps/r54b-s1` to `r54b-s3` |
| that the corpses came to the mouths | inference, not measured |