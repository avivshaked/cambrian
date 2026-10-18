# Round 59's predictions: trained grazers planted in a world that can smell its leaves

*2026-10-14, written after the leaf tank's third arm and the card's timing of the leaf smell, and committed
before round 59's three worlds start. The owner approved the plan on 2026-10-14 (D174) and ruled its open
choices the same afternoon (D176 to D178) [OWNER: the date this entry is approved]. It rests on D161, D167,
D168, D171 to D174 and D176 to D178, on the leaf tank's reads
(`logbook/specs/nursery-leaf-read.txt` on `nursery-leaf`, `nursery-leaf-sparse-read.txt` and
`nursery-leaf-sparse3-read.txt` on `nursery-d172`) and on round 58's isolated worlds (logbook/0139). Every
number says whether it is measured, computed or guessed.*

## What it asks

Grazing is the owner's goal for these rounds. A grazer here is a body with a mouth that finds living leaves,
bites them and eats what the bite frees. The world's leaves are bodies that live on light and matter alone. No
world so far has grown grazers. Round 58's three isolated worlds recorded no bite at all in 48,000 s: their
running count of parts killed by a mouth stood at 0 at every year's end, though bodies carrying a mouth's
attack cells were born in all three (measured, `scratch/r59-prereg/helpers/r58-kills.py` and `r58-mouths.py`
on `r58-s1ir`, `r58-s2ir` and `r58-s3ir`; round 59's reader on the same worlds is kept in
`logbook/specs/r59-prereg-r58-baseline.txt`).

The nursery is a separate program that breeds brains on fixed bodies in a small tank. Over the last two days it
bred a swimmer that eats leaves. Its body is a box with a mouth and one hinged fin. In a tank with 31 living leaves
and nothing else to eat, its best genomes ate about 440 J of leaf in a 1,800 s episode. Across the arm's examined
genomes, knocking out their sense of the leaves' smell cost 35% of their eating, about six standard errors
(measured, D174). That sense is
new: since D167, living leaves give off a smell of their own, and a body can read its strength and direction.

Round 59 gives every world that smell and plants the ten best swimmers into two of three isolated worlds. Seed
1 gets none and is the control. The round asks whether the planted swimmers' lines outlive their first
generations (P), whether grazing evolves in the control from the world's own founders (G), whether the planted
worlds eat more leaves than the control and use the smell to do it (D), and whether the new pieces leave the
crowd and the founding as round 58 had them (C).

## The world

Round 59 runs round 57's world, which round 58 also ran, from `rounds/env-r57.ps1`. Its year of 6,000 s has
three days of summer at 1.6 of the mean light and three of winter at 0.4. Its day is 1,000 s long, and the
weather changes daily. The sky is drawn from its own seed, 57, so every world of the round shares one sky. The
tank is 22,000 m2 and 25 m deep (round 59's founded config, `worldAreaSquareMetres` and `worldDepthMetres`).
Species tracking is off.

Round 59 adds four pieces, set in `rounds/env-r59.ps1` on the round's branch, `round-59`:

- **A drag on turning** (D161). Water resists a body's turning, which stops single-part bodies from rocking and
  spinning without end. It is the spin form at a coefficient of 0.5 (`EVOSIM_TURNING_DRAG` `spin`,
  `EVOSIM_SPIN_DRAG` 0.5). The nursery's drag check found that the spin form at 0.5 brings two spinning
  stomachs from 1.35 and 2.52 rad/s to 0.04 rad/s, and leaves the swimmers' speeds as they were (measured,
  `nursery-leaf-tank-plan.md` step 6 on `nursery-d172`). The owner ruled 0.5, the size the planted swimmers
  were bred under (D176).
- **The touch of the rock, and its sense** (`EVOSIM_SENSE_BED_TOUCH` 1). A part that touches the bed or a reef
  reads 1 on a new channel, a step late. The glass reads 0.
- **The leaves' smell, in its direction form** (D167, D174). Each living leaf gives off a smell in proportion to
  its lit leaf area. It spreads as a cone of 6 m around the leaf and stops when the leaf dies. Each part of a
  body reads three numbers, the smell's direction in the part's own frame, scaled by its strength
  (`EVOSIM_LEAF_SCENT` `direction`, `EVOSIM_LEAF_SCENT_RANGE` 6, `EVOSIM_LEAF_SCENT_CELL` 2,
  `EVOSIM_LEAF_SCENT_HALF` 1). The field's cells are 2 m, and its half-scale is 1 m2 of leaf. These are the
  values the planted swimmers were bred under (the tank's config `3fd98985cdf68008` carries a 2 m cell and a
  1 m2 half-scale, and the arm's recipe a 6 m reach), and this entry names them as the round's. The 3 m arm
  showed the reach is what made the nose pay. At 31 leaves and 3 m, knocking the smell out cost 7 J of 159,
  under one standard error, and at 6 m it cost 92 J of 261 (measured, `nursery-leaf-sparse3-read.txt`).
- **A mouth's equipment at a third of its price** (D177). Consumer tissue, the mouth's cell type, costs 2 W a
  cubic metre to keep, against 6 before (`EVOSIM_UPKEEP_CONSUMER` 2), and the mouth's attack and intake
  strengths 0.033 W a unit, against 0.1 (`EVOSIM_PRICE_ATTACK`, `EVOSIM_PRICE_INTAKE`). The owner ruled that
  swimming must pay: a planted swimmer stood at 0.91 W at the old prices, 0.62 W of it its box of consumer
  tissue and 0.29 W its mouth and claw, against 0.17 to 0.24 W eaten in the tank, and stands at 0.30 W at the
  new (computed, `scripts/ledger.ps1`, `scratch/swim-cost/`). Every mouth in the world pays the new prices.
  D178's smokes kept the third (below).

The anchoring cell, a cell that grips the bed or a reef, stays off (D171). The mouth keeps its rules. Every death
in the world leaves a corpse, as before. The tank's rule that only a leaf bitten to death leaves food (D169)
is the tank's alone.

Since D168, the card's single precision tracks small movements and light contacts. Round 58 ran without it, and
there the card counted about half the CPU's contacts (measured, `logbook/specs/build-readings.md`). A bite reads
the same overlaps, so round 58's bite chances and contact columns cannot be compared with round 59's. A count of
kills higher than round 58's zero may come from D168 alone (inference).

The new settings and the new checkpoint format refuse round 58's checkpoints, so every world is founded fresh.
Nothing replays round 58.

## The seeds and the planting

Three isolated worlds on seeds 1, 2 and 3, with no exchange: round 58 showed that joined worlds spread one
winner (logbook/0139). Seed 1 is the control. Its trickle is round 57's and 58's. The trickle adds a founder
about once every 30 s after the floor closes at 3,000 s (`EVOSIM_TRICKLE` `1/30`). One trickle founder in ten
is a copy of one of round 54's four stomachs (`rounds/r55-pool-control.tsv` at `EVOSIM_TRICKLE_POOL_SHARE` 0.1),
and the floor draws nothing from a pool (`EVOSIM_FLOOR_POOL_SHARE` 0). A stomach is a body that lives on the
matter it absorbs.

Seeds 2 and 3 carry the same trickle and add the ten swimmers to its pool. They are the ten genomes of
`inocula/r59-direction-tops/`, cut on 2026-10-14 from the direction arm `leaf-direction-sparse` (`9dfe61ae`).
Each ate between 379 and 443 J an episode under its own brain on held-out tanks, and each lost between 147 and
306 J to the smell's knockout (measured, that folder's `origins.tsv`). Each enters as an exact copy, with its own
brain.

The planting goes through the trickle's pool, as D174 says round 55's did, at the share the owner ruled (D176).
The weights in a pool's list are relative, and the share of the trickle drawn from the pool is a setting of its
own (`World.cs`, D117's draw), so the round has a list of its own, `rounds/r59-pool.tsv`, written by
`rounds/r59-pool-list.py` as `r55-pool-list.py` wrote round 55's: the four stomachs at 0.025 each and the ten
swimmers at 0.05 each, drawn by 0.6 of the trickle (`EVOSIM_TRICKLE_POOL_SHARE` 0.6, `rounds/env-r59-planted.ps1`).
The stomachs keep the control's one trickle founder in ten, and the swimmers take one in two, round 55's nursery
share: about 100 swimmers a year and 750 in the round (computed: 200 trickle founders a year, 169 to 230 a year
measured in round 58's isolated worlds, over 7.5 years after the floor closes). Round 55's 460 to 490 nursery
founders a seed at that share left one line that held 4% of one seed and 0.1% of the other (measured,
`logbook/specs/r55-read/read-30000.txt`). The floor draws nothing from the pool (`EVOSIM_FLOOR_POOL_SHARE` 0), so
the first swimmers arrive after 3,000 s, into a full world, and the planted seeds' floors are the control's.

Here is why I expect little from the planting. The tank and the world differ in five ways that matter to a
planted swimmer.

- **Its purse.** In the tank each swimmer started with 3,639 J, enough for 3,830 s at its standing cost of 0.936 W
  (measured, the tank's traces). A world founder starts with 600 s of its standing cost
  (`EVOSIM_FOUNDER_ENDOWMENT` 600), capped at 0.9 of its breeding gate. At D177's prices that is 127 J,
  read from the smoke's birth rows (measured).
- **Its income against its costs.** Even the best swimmer in the tank lost energy. Over 1,800 s the direction
  arm's examined genomes ended 1,953 J poorer on average, at the old prices (measured). At D177's prices a
  swimmer stands at 0.30 W and strokes at 0.16 to 0.28 W, so it breaks even at about 0.5 W, about 850 J an
  episode, against the 379 to 443 J the ten ate (computed). Its consumer box may earn something from the world's drifting dead matter, which the tank had none of.
  I have not measured how much.
- **The leaves.** The tank held 31 leaves on 400 m2, 0.078 to the square metre. Round 58's isolated worlds
  held 0.14 to 0.22 living bodies to the square metre in year 2's summer, and 0.25 to 0.50 in year 8's
  (computed from logbook/0139's reader), of every guild; how many are leaves is read in round 59's yearly table.
  The leaves are also another line's, of another shape.
- **Its life.** The tank had no age rule (D169), and the world does.
- **Its brain.** The swimmers were bred without the touch sense. Read from code, a stored link resolves by its
  port, so the new channel shifts none of the swimmer's links (`CentralWiring.cs`). It just gives the new
  channel no links.

So by the tank's numbers a planted swimmer starves in a few hundred seconds unless it eats about twice what it ate
there (computed: its purse over its standing cost and stroke). The smoke's swimmers did better than that: at a
third, their median age at death was 892 s, and 13 of 21 were alive at 4,500 s (measured). At the old prices it needed four to six times. The world
holds two to six times the tank's bodies to the square metre, so twice is within reach, but nothing has shown
it. Round 55's planted swimmers, at the old prices, starved: 93% and 96% of the jointed nursery-rooted bodies died
starving, at a median age of 478 and 496 s, and 1 to 2% bred (measured, round 55's interim read). The one line that
held, in seed 2, held by dropping its joints. That is why the P clauses below are modest.

## The engine and the length

All three worlds run on the graphics card in single precision (D157), with D168's fix, at group 32, with the
snow's transport, the carrion smell's fill and the concurrent mode on the card, as round 58 did. The leaf smell's
field is filled on the host, once a metabolic step, and the card reads it.

The card's timing of the leaf smell ran on 2026-10-14 from 15:17 to 15:34 at `7105c799`. One world of round 59
alone on the card, from 1,000 s with 3,439 to 3,584 bodies, stepped at 7.02 and 7.14 times real time at 6 m
(measured, `logbook/specs/r59-leaf-timing-plan.md`, "What the window measured", on `r59-leaftime`
`157619c9`). The fill took 5.6 ms a simulated second, about 4% of the stepping wall. The pace falls as the crowd
grows, so 7.1 is an upper bound. In D159's sitting a world in a group of three kept 68% of a lone world's pace.
That puts a group of three at about 4.8 times real time at 1,000 s (computed). Round 58's isolated group ran its
46,500 s in 3 hours 19 minutes, about 3.9 times real time over the whole run (computed from logbook/0139). So I
guess a group of three takes about 3 to 4 hours for 48,000 s.

The three worlds run as one group of three. `scripts/card/card-group.ps1 -Launcher` takes a block for each
world, so seed 1 founds from `rounds/env-r59.ps1` and seeds 2 and 3 from `rounds/env-r59-planted.ps1`, in one
card process; the launcher is `rounds/r59-launch.ps1`. Isolated worlds in one group share the card and its
wall, never a body or a field, so a world's pace reads against the group's and not against a lone world's.

The length is 48,000 s, rounds 57's and 58's, which gives eight years, eight winters and about fifty leaf
generations (computed in logbook/0136), so that the crowd, the founding and the years read against round 58 year
by year. Checkpoints come every 1,500 s. The runs are `r59-s1`, `r59-s2` and `r59-s3`. Nothing else uses the
card while they run, and no other farm or test suite runs on the machine; the nursery room's server, a nursery
arm and the other project's short batches may run on their own cores (`-AllowBeside`).

## The smoke before launch

The smoke checks that the world admits the planted genomes and that they are born, move and feed, and it
chooses the mouth's price (D178). The trickle opens only when the floor closes, at 3,000 s
(`World.FoundersStillArriving`), so the smoke runs seed 2's planted world founded to 4,500 s, twice, as one
group on the card (`rounds/r59-launch.ps1 -Step smoke`): `r59m-third` at D177's prices and `r59m-sixth` at a
sixth of the old (attack and intake 0.0167, consumer upkeep 1, `rounds/env-r59-sixth.ps1`). Each holds about 50
trickle founders and so about 25 swimmers (computed), and the pair takes 15 to 25 minutes (guessed). The round
takes a sixth only if its swimmers live past 500 s and eat where a third's do not, and its leaves hold;
otherwise a third. Before it, as a founded round needs, the CPU accelerator founds every config the round and
the smoke use as one group for 30 s, with a checkpoint at 15 s and a resume from it (`-Step check`,
`src/Evosim.Farm.Gpu/CLAUDE.md`).

The smoke passes when all of these hold:

- the run's `pool/index.tsv` lists the fourteen genomes, and the ten swimmers' hashes match `origins.tsv`;
- every pool founder's birth row reads `src` `pool` with a pool index, and the swimmers' rows read `atk` 1,
  `ink` 1, `jnt` 1, `abs` 0 and `pho` 0;
- none is refused for room, and each appears in `positions.jsonl` and moves;
- `card-run-check.ps1` holds every condition, and the books close;
- the config names the drag, the touch sense and the direction form at 6 m.

I also read each swimmer's fate in the smoke, as round 55's interim read did: its age at death, its cause, and
any kill row it wrote. They are readings, and no bar rests on them.

**The smokes ran on 2026-10-14** (`r59m-third` and `r59m-sixth`, `logbook/specs/r59-smoke/read-smoke.txt` on
`round-59`). Every pass item held at both prices, and every condition of `card-run-check.ps1` held. Each world
admitted 21 swimmers, all after 3,063 s. At a third, 8 had died by 4,500 s (5 starved, 3 of age, median age
892 s, one under 600 s) and 13 were alive; 14 had a child, and 31 bodies, 2.6% of the living, descended from
them; their bodies wrote 9 kill rows, 7 on leaves. At a sixth, 9 had died (median 562 s, five under 600 s), 19
had a child, 108 bodies (7.6%) descended from them, and they wrote 80 kill rows, 33 on leaves. The leaves held
at both, 1,082 and 1,187 living. The newborns of both starved fast, at a median of 61 and 149 s (measured).
The swimmers live at a third, so by D178's rule the round runs at a third. The sixth bred and hunted more, on
one seed and 1,500 s of trickle; it is the next lever if the planted lines die out.

## Predictions

The years are round 57's: year k runs from 6,000 (k-1) to 6,000 k s. A line, or root lineage, is every body
descended from one founder, a body born without a parent. A planted line is one whose founder is a swimmer from
the pool. A leaf is a body with photosynthetic tissue and neither absorptive tissue nor a joint (round 58's
reader's rule). A mouthed body carries attack and intake cells (`atk` and `ink` 1). A grazer is a mouthed body
with no photosynthetic tissue. A leaf kill is a kill row whose victim is a leaf. *Every bar below is my choice,
set before any run of round 59's world.*

| # | prediction | falsified by |
|---|---|---|
| P1 | **the planted swimmers breed**: in each of seeds 2 and 3, at least one planted founder has a child. Round 55's jointed nursery bodies bred at 1 to 2%, and at D177's prices the tank's swimmers ate about half of what they spend. I guess this holds | lineage.jsonl, pool/index.tsv |
| P2 | **a planted line lasts a year**: in at least one of seeds 2 and 3, the living descended from planted founders, of any guild, number at least 20 at every sample for at least one year after 6,000 s. One line in round 55's seed 2 would have passed this (22 to 42 living from 20,000 to 23,000 s, 196 at 30,000 s). I guess it fails | the same |
| P3 | **the planted lines hold some of the world**: at 48,000 s, at least 1% of the living descend from planted founders, in at least one of seeds 2 and 3. Round 55 reached 4.0% and 0.1% at 30,000 s with about two thirds as many planted founders as this round's. I guess this fails | the same |
| P4 | **a lasting planted line still grazes**: among the living descended from planted founders at the end of the year in which P2 holds, more than half are grazers. Read only if P2 holds, and otherwise unread. Round 55's lasting line kept its body and dropped its joints. I guess it fails | the same |
| G1 | **grazing evolves from the world's own founders**: in seed 1, at least 100 leaf kills a year by grazers of lines not founded from any pool, in at least 2 of years 6 to 8. Round 58's isolated worlds killed nothing in eight years, before D168 and at three times D177's mouth prices (measured). I guess this fails | lineage.jsonl's birth and kill rows |
| D1 | **the planted worlds kill more leaves**: leaf kills over years 2 to 8 in each of seeds 2 and 3 number at least 50 and at least twice seed 1's. The planted founders count. The reading splits the founders' kills from their descendants', and the world's own lines' kills from both. A swimmer in the sparse tank killed about 2.8 leaves an episode, and the smoke's 21 swimmers at a third wrote 7 leaf kills in their first 1,500 s, so about 750 would write about 250 (computed from the smoke). I guess this holds | the same |
| D2 | **the planted lines steer by the smell**: in each seed where the planted-descended grazers alive at 48,000 s hold at least 8 distinct genomes, those genomes swim in D173's tank (31 leaves, 6 m) under their own brain, no brain and the smell knocked out, 32 held-out episodes, and the knockout costs at least two standard errors of their eating. Read only where the 8 exist, and otherwise unread. I guess it is unread | the bench's exam, `scripts/reads/tops-exam-read.py` |
| D3 | **grazers that read the smell climb it**: in each of seeds 2 and 3, over 3,000 to 48,000 s, the grazers whose brains read the leaf smell move along its gradient more than the grazers whose brains do not: their mean cosine between motion and gradient, weighted by the bodies counted in each row, exceeds the non-readers' by at least 0.1, where each side counts at least 1,000 body-samples. Read only where both counts are met. I guess it holds in a seed where planted founders live long enough to be sampled | `leafscent.jsonl` (D176's recorder) |
| D4 | **the control's own grazers**: in seed 1, the same comparison as D3 for the world's own grazers, where both sides count 1,000 body-samples. I guess it is unread, for want of grazers | the same |
| C1 | **the crowd stands with round 58's**: the living at every year's end, years 2 to 8, between half and twice the mean of round 58's isolated worlds at the same second, in 3 of 3 seeds. Those means are 3,792, 4,669, 4,981, 5,577, 6,249, 6,823 and 6,500 at 12,000 to 48,000 s (computed from their stats rows). I guess this holds | stats.jsonl |
| C2 | **one line takes each world again**: one root lineage holds over half the living by the end of year 3, in 3 of 3 seeds. Round 58's isolated worlds were at 92%, 99.8% and 99.4% then (measured). I guess this holds, and that a planted line is never that one | lineage.jsonl |
| B1 | **the books balance**: the audit under 0.1 J and the matter residual under 1e-5 units at every sample, nothing diverges, and every world ends on its budget, in 3 of 3 | stats rows, run.json |
| B2 | **the card holds**: `card-run-check.ps1` holds every condition in 3 of 3, with no contact candidate dropped. Round 58's seed 1 twin began dropping between 42,000 and 43,500 s, as its crowd passed about 9,500 (logbook/0139). If drops come, the read marks the second they begin, and every clause is read again to that second | the card's log, card-run-check |
| B3 | **a world resumes**: seed 2, resumed on the card from its 45,000 s checkpoint to 48,000 s, matches the unbroken run in stats, digest and lineage rows, apart from the step counters and the wall timings. The checkpoint format is new this round. About 15 minutes of the card | the resumed run `r59-s2b` (`rounds/r59-launch.ps1 -Step b3`), `scripts/reads/group-resume-compare.py --lone 45000` |

P1's bar was set before the smokes, and seed 2's smoke met it at both prices within 1,500 s of trickle. It is
kept as written, and reads as a check rather than a test.

The world has no knockout: a knockout exists only in the nursery (`src/Evosim.Nursery/KnockoutReach.cs`, the
exam's controls), and stepping a world with a sense switched off would be a new world. So the smell's use is read
two ways. D3 and D4 read D176's recorder, `leafscent.jsonl`, which writes every 100 s, for each body moving
through a smell, the cosine between its motion and the smell's gradient, whether its brain reads the smell, and
whether it is a grazer; it moves no world state (`src/Evosim.Farm/LeafScentRecorder.cs`, its test). D2 benches
survivors in the tank with a recipe that has no search and the genomes as its pool, as `leaf-direction-tops`
did; a genome the tank refuses is reported and left out of D2.

## The readings

- Per world and year: the most living in summer, the fewest in winter, the largest root lineage's share, and
  the leaves', the stomachs', the jointed bodies' and the grazers' living (round 58's yearly table).
- Per planted seed, at every 1,500 s: swimmers admitted, their children, the planted-descended living by
  guild, the planted lines alive and the largest. The swimmers that died that span, by cause and median age, as
  round 55's interim read gave them.
- Per world and year: leaf kills and all kills, split into those by planted founders, by their descendants and
  by the world's own lines. The world's `unitsEaten` and `corpsesEaten`. The run keeps no row for a meal, so which body
  ate a corpse is not in it (`scripts/reads/r58g-reach.py`'s docstring). Kills are the only per-line sign of
  grazing.
- Where the grazers are: a grazer that kills often should sit nearer the leaves than chance. Per world at year ends, the median distance from each grazer to its nearest leaf, against random
  points at the same depths, from `positions.jsonl`.
- Which of the ten swimmers rooted each planted line, and whether one swimmer wins in both seeds.
- From the recorder, per world and sample: the leaf smell's cosine for readers and non-readers, grazers and others.
- The pictures: the theatre at year 8's summer peak in seeds 1 and 2, and a planted line where one holds.
  Frames of a live world every few thousand simulated seconds while the round runs.

## What the round does not ask

Whether the smell changes the world by itself: every seed carries it, so no world here lacks it. Whether the
strength form would have done as well: the tank answered that (D174). Whether 3 m would do in the world: the
tank's third arm says no for the planted genomes, and the world is not asked. Whether the anchoring cell lets
grazers sit and wait: it is off (D171). Whether joined worlds spread a grazer: the seeds are isolated. Which of
the ten swimmers is best: the round can say which rooted a lasting line, and one or two seeds cannot rank ten
genomes. Whether grazing would come with a larger share of planted founders: the round runs one share.

## The reader

`scripts/reads/r59-read.py` reads P, G, D1, D3, D4, C and B1 (`762b4bb9` on `round-59`). Read on round 58's isolated worlds it reproduces the living at every year's end given under C1, their year-3 shares and their zero kills. P2's "every sample" is the 1,500 s grid, a kill is one kill row, and guilds are read from birth rows. It is built from round 58's
reader for the lines, guilds and yearly tables, and from `r58g-read.py` and `r58g-reach.py` for the kill rows by
attacker. It roots every body in its founder and labels each founder by its birth row's `src`: floor, trickle or
pool. A pool founder's genome is named by its pool index against the run's `pool/index.tsv`, and a planted line is
one rooted in one of the ten swimmers, matched by hash against `inocula/r59-direction-tops/origins.tsv`. The
docstring states each clause as computed. Its `--smoke` mode reads the two smokes, and there its swimmer count
must equal the pool rows in the lineage. D2 is read by the nursery's bench after the round, from genomes taken
from each seed's last snapshot. B2 is read by `card-run-check.ps1`, and B3 by `group-resume-compare.py`.

## Before the launch

The pre-registration's record is the commit that adds this entry on `round-59`, on a clean tree. Every world's
manifest names that commit as `gitCommit` with no DIRTY mark, read from `source.gitDirty` and not the blank
top-level fields (logbook/0139). The branch holds the environment blocks (`rounds/env-r59.ps1`,
`env-r59-planted.ps1`, `env-r59-sixth.ps1`), the pool list (`rounds/r59-pool.tsv`, written by
`rounds/r59-pool-list.py`, which checks each swimmer against its hash), the launcher (`rounds/r59-launch.ps1`),
the reader, D176's recorder and D177's knob, and the card scripts that take a block a world and `-AllowBeside`;
the Farm suite passes on it (326 of 326 at `762b4bb9`, 2026-10-14, the card hidden). The launcher reads every world's config.json back for its
prices, upkeep, pool and floor shares, pool count, smell, drag and touch, compares each run's pool genomes
with the list's, parsed, with the genome's own id set aside. The CPU accelerator's founded check passes first, then the smoke, then the
round. Every card sitting runs under the driver watch and the heat stop, with nothing else on the card and no
other farm or test suite on the machine; the nursery room's server, a nursery arm and the other project's short
batches may run on their own cores.
