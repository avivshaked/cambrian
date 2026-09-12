# Handoff: where to pick up

*Rewritten 2026-09-06 from the current state. What happened is in the logbook, and why it was
chosen is in [`DECISIONS.md`](DECISIONS.md). This file says only where things stand.*

## Where things stand

The whole record (logbook, primer, research) was restyled under STYLE.md and landed on
2026-09-07 after a pair-by-pair review; the git history holds every original.

**Round 36 is read and round 37, the tank, is launching (logbook/0092 and 0093, 2026-09-12
morning).** Round 36 passed five of five; its pre-registered bar for a standing jointed line
failed in all five, but every seed carried its inherited jointed line further than round 34's
twin, two to the last birth, and seed 1 ended with 75 jointed bodies, all absorptive and all
from one jointed absorptive founder: the record's first standing jointed population, on a body
plan the round did not predict. The ledger (R1) reads the hinge neutral to a tenth of a percent
and the link's earning symmetric between jointed and rigid, so the round raised the income of
any body with a link and did not price the hinge; why seed 1's jointed eaters bred better than
their rigid neighbours is not on the books. Divergences rose with jointed adult-seconds (33, 55,
20, 6, 17); seed 2 hit the 50-dump cap. The tank (D089; branch `tank`, `logbook/specs/tank-spec.md`)
passed its six checks: Core 653 green, the box digest identical under the new build, dead
pockets 0.004, the dilute arithmetic, a 600 s smoke with pictures (`scratch/snaps/r37tank2/`,
wraps 0, no body outside the glass), and the sitter-against-mover rerun on the dilute tank
(mover over sitter 3.0 at low mixing, the box's number). The limiter's check failed: round 34
seed 5 rerun with it on bound 2.58 million drives and read 27 divergences against 17 with the
same signature, so it is not adopted and round 37 runs with `driveLimit >0.01`. The three
proposals are absorbed into D089 and deleted. The box's header token had drifted to `4x1x5 m`
on the branch and was put back to the recorded `4x5x5 m`. Round 37 is round 36's world in the
tank, pre-registered as logbook/0093 with seven predictions (M1: the rim ring holds 15 to 40%
of the bodies; M2: the middle is not empty; M3: the crowd is round 36's), launched on the merged
build after round 36's last render: all five seeds between 08:28 and 08:31 on workers 2 to 6
(`simHash c50c465b…`, `coreHash e6797e6e…`, `configHash 96d4bce6`; the full suite 666 green
first), the render queue beside them; every header verified.
Worker 7 ran the tank checks off the worktree's Core and was restored to the main tree's before
the launch. Round 38's launcher (`rounds/launch-r38.ps1`: area 400, budget 6,000, corpse decay
0.005) is ready and needs no build.

**Round 34 is read (logbook/0090, 2026-09-11 midday): five seeds of five pass the goal
rule, the free joint founds in every seed (46 to 140 inherited at 1,000 s) and is gone from
every seed by 23,100 s, re-invented by mutation to the end and never re-founded. The ledger
charges it nothing (R1), jointed newborns die in their first minute whichever lineage they
arrive in (R2), and the stroke is driven flat out at 5 to 77 W per jointed body while it
exists, moving the body faster than the water (R3); the divergences are jointed adults thrown
by the solver, two percent of jointed births. The reading: the price was the founding
barrier, the unthrottled stroke is what removes the joint afterwards. Consequences recorded in
the path: round 39 (the stroke priced alone) stays moved forward, and the drive impulse
limiter at every step goes to the owner as a proposal before any cheap-stroke round. Round
36 (the link earns) is running: seed 1 ended at 12:06 and passes with a jointed line alive
at 30,000 s (73 inherited, from a peak of 167), which no round 34 seed managed; seeds 2 and
3 run on workers 6 and 5, seeds 4 and 5 queue behind them. The round 34 pictures for seed 4
are still to render (its first render was stopped a minute in to give round 36 seed 3 the
slot). The theatre skin's third day landed (commit `e4d6540`, `logbook/specs/skin-spec-3.md`:
taper, bend, pillow dial, carve default 0.35, softer rim; boxes only, all inside the
collider); the three comparison pictures of `r35-s1` at 5,000 s are in
`scratch/snaps/skin3-*/`, the agent's reading is keep the defaults and refuse carve 0.5,
and the pillow (0.34 or 0.5) is the owner's ruling. The fourth day, the sun and the surface
(`logbook/specs/skin-spec-4.md`: rippling surface from below, Snell's window and a sun,
light shafts, caustics on bodies and bed from one ripple, a `sky` snapshot view), compiled
first time on worker 3, rendered `r36-s1` at 5,000 s (`scratch/snaps/skin4/`) and merged
into main the same evening (branch `skin4`); its five dials (`EVOSIM_THEATRE_WAVE`,
`_WAVELENGTH`, `_WAVE_SPEED`, `_LIGHT_REACH`, `_SHAFTS`) are unjudged by the owner, and
the agent's first reading is that the surface reads as bands rather than water, which is
the wave steepness. Both skin days are written up as logbook/0091; the calm sea (0.022 m over 3.2 m) is the
wave default, picked from two pictures; the pillow ruling is still the owner's. The owner
ruled the aquarium's shape the same evening ("yes proceed" on the cylinder, the square as
fallback; `fable-propose-aquarium.md`), so the tank build starts on a branch off `box`, with
a code survey first and the proposal's six checks before any scored round; rulings 2 and 3
(the dilution's numbers, the concentrators) are taken as the agent's recommendation until
the owner says otherwise, and the proposal is absorbed into DECISIONS.md when the build
lands. The owner watched `r36-s1` in the theatre (Mode B,
seek 5,000 s) and saw no sun, shimmer or ripple, which is why the day was moved up. Round 36 is fully launched (seed 5 at 19:23); seeds 1, 2 and 3 have landed with
a jointed inherited count at 30,000 s of 73, 4 and 0. The box build (branch `box`, worktree
`scratch/wt-box`) now waits for round 36's renders as well as its arms, because the theatre
refuses a recording whose `coreHash` the build did not write and every worker compiles Core
from the main tree: merging before the last render would make every round 36 picture a
declared cousin. Order: skin day 4 merges first (no hash), then the box with the limiter
tunable if the owner rules for it, after the last round 36 render. The round 34 read files
moved from `scratch/r34-read/` to `logbook/specs/r34-read/` because 0090 cites them.**

**Round 35 is read (logbook/0087, 2026-09-11 small hours): the three-dimensional base stands
in five seeds of five, every book closed, every seed filling the box from 1,100 s on, the
goal rule held with smaller stomach clades (20 to 130 against round 33's 74 to 120), and
round 33's one-way dial walk did not replicate: seeds 1 and 5 kept it, seeds 2, 3 and 4
went to small adults born nearly whole in litters of 1.5 to 1.7. The stomach lines thin
from an early peak in four seeds, round 32's shape, so 0079's question stands in the fixed
world. Pictures at 5,000, 15,000 and 30,000 s of every seed are in `scratch/snaps/r35-s*/`
(three beside the entry). The theatre's skin had its second day (carving, ellipsoids, the
close view; CLAUDE.md's theatre paragraph) and the owner picked carve 0.35 by eye.**

**Round 35, the base round in three dimensions, is launching (logbook/0087, 2026-09-10
evening): round 33's world with D088, dispersal 5 m, transport at 0.1 m/s, founders
reserving the adult, five seeds at 0.01 on workers 2 to 6 (`rounds/launch-r35.ps1`
defaults). The owner ruled the five questions with "proceed with your recommendations". The
viewing arm `r35v-s3` (0.3 m/s) was stopped at 11,100 s to free the fifth worker; its
pictures are in 0086. Round 34, the free joint, follows on this base. The theatre has a skin (logbook/0088,
`research/theatre-look/`), all under `Assets/Theatre`, no hash moved.**

**The three-dimensional world is built (D088, logbook/0085, 2026-09-10 afternoon): a newborn
dispersed over a 5 m disc about its parent (`EVOSIM_OFFSPRING_DISPERSAL`, 0 replays the
record), a current that carries bodies, corpses and the grid's cells in three dimensions with
equal speed on every axis (`EVOSIM_CURRENT_MODE Transport`; the rolls kept as `Rolls`), every
destroy immediate so that the theatre's replay matches the farm (identity 6 of 6 in Play mode
on the smoke), the placer reserving the adult's radius, the theatre's id pairing fixed for bred
children. The table carries `cols`, `cols abs`, `x sd`. The viewing arm `r35v-s3` (seed 3,
0.01, 30,000 s, worker 7, `rounds/launch-r35.ps1` defaults) runs for the owner's theatre
pass; the owner's sequence is to see the fixed world first, then rule on 0084's knob list
(dispersal radius, box shape, speed and period, founders' reservation, mixing, prices,
corpses, the mass floor, shading's length scale), then the retries in 0084's bin 3. Round 34
waits behind that. `mean m/s` reads the water now; a relative-speed column does not exist.
The owner delegated the theatre pass (2026-09-10 late): the agent photographs runs headless
(`TheatreSnapshot`, `scripts/theatre-snap.ps1`) and reads `positions.jsonl`
(`scripts/positions-read.py`), both built and committed the same evening; logbook/0086 is the
first look, and the old-rules recreation `r35old-s3b` (dispersal 0, rolls) is the before
picture, 23 columns against 100. A non-finite link killed a process (`r35old-s3`) and the
finiteness check now reads every link. Five questions are in front of the owner: the
dispersal radius (recommend keep 5 m), the box's shape (not yet), the current's speed
(recommend 0.1 m/s), founders reserving the adult (recommend yes), matplotlib for the reader.**
**Round 33, the growth base, is read (logbook/0082, 2026-09-10 morning): the goal rule in five
seeds of five, every stomach line recruiting and four of five founded by a late child; adult
scale, investment and litter walked the same way in every seed and stopped at the mass floor,
which is now a rule of the world; one contact divergence in seed 2 with the placer's birth-size
reservation as the suspect; a cheap joint outlived founding in seed 4 to 10,000 s. The owner's
theatre session (2026-09-10 midday) showed the world as two vertical ribbons a metre wide
(logbook/0083): a child is placed touching its parent, the current returns a body to where it
found it, and nothing else moves a sitter sideways, so every clade is a column, and since the
grid a column drains its own cells. The owner ruled the fix started at once: offspring
dispersal (`OffspringDispersalMetres`, first value 5 m) and a horizontal-position instrument
(`cols`, `cols abs`, `x sd`), being built with a smoke on worker 7. The theatre's replay also
parted from the recording at 200 s, a separate fault under investigation. Round 34, the free
joint, waits behind both; the placer's birth-size reservation fix waits with it.** **Round 30 is read (logbook/0075, 2026-09-08 evening): the vertex world at D082's prices
meets the goal rule in 5 of 5, the first round to do so, with both identities closed, the
detritus loop closed and the population within 3% of round 29's. It was the base until the grid (D086). The
senses were carried in 1 of 5 (round 29: 3 of 5) and no jointed guild held; seed 4's line
of 14 lasted to 10,000 s feeding as well as the sitters and moving no faster than the
drift. Logbook/0076 measured why: at 0.2 m²/s a sitter's hole refills in five seconds, so
there was no prize. Round 31 (D085, logbook/0077) is round 30 with the detritus mixing at
0.02 on both axes, read 2026-09-09 (0077): 4 of 5 under D063 as amended, the population
unchanged, the hole in the water at large rather than at the sitter, no joint surviving
founding in any seed, the larder gone deep; the vertex world's last word. Meanwhile the owner asked where the hole lives and the
answer replaced the representation: the water as a grid of cells with corpses as particles
(D086, logbook/0078), built, reviewed and smoked on 2026-09-08 night, ruled 2026-09-09
(matter cells 5 m, matter mixed at 2 m²/s on every axis, corpses at 0.005/s). Round 32,
the grid's base round at round 31's prices and mixing, is running: `r32-s1..s5` on workers
2 to 6 at 0.01, launched 2026-09-09 midday (`rounds/launch-r32.ps1`), pre-registered as
logbook/0079, read 2026-09-09 evening: the mechanism holds in every arm, the population is higher, and the goal rule holds in two seeds of five against round 31's four, the stomach lines thinning from founding without recruiting; the grid stands as the base and the eaters' recruitment is the open question (a per-guild feeding trace and a corpses-off seed are the two reads). Round 33 is growth, running since the evening of 2026-09-09 on workers 2 to 6 (`r33-s1..s5`, `simHash 0924b9ad…`, `coreHash 621d32ee…`, commit 0b8e822) (D087, logbook/0081; `rounds/launch-r33.ps1`):
the growth build landed on 2026-09-09 and the pre-growth world no longer exists in the code,
so the owner's conditional ruling of that morning (growth first if its build was close) puts
growth before the free joint. Round 34 is the free-joint test (logbook/0080, amended before
launch; `rounds/launch-r34.ps1`, round 33's launcher with the four prices at zero) on the
growth world, with round 33's same seed as its control. **When round 33 lands, work pauses**
(owner, 2026-09-09 afternoon): the owner tests the theatre on it, since a growth-build run is
the first one the current code can replay, and nothing launches until the owner says so.
Predation waits behind round 34.** Round 29 (logbook/0072) was the price
control: the senses carried by a third to a half of the population in three seeds of five
at the old price, no jointed body, the goal 2 of 5, the world standing under added mass. Round 29 was the movement round on round 28's world with the three senses
on, added mass 0.5 and the global brain retired (D081's build, commit `40b16b8`,
`simHash e43b81a8…`): `r29-s1` to `r29-s5` on workers 2 to 6, dt 0.01, 30,000 s; round 28's arms are the
controls. Six predictions and their two-sided readings are in 0072; the scorer's verdict
line now says `CENSORED` for a run that ends short of its budget. Round 28 read 3 of 5
(0070) and is the base world by D081. Then the owner asked how nature solved the chicken
and egg of movement and sensing; review round 6 answered it the same night (research §0,
Q11; logbook/0073), and the answer is that this world cannot bootstrap movement by
construction. The owner ruled the water into vertices (D083, logbook/0074): detritus and
free matter are now sets of vertices holding joules at positions, read through a 1 m
kernel, so a still body eats a hole in its own water and a moving body leaves it. Built,
tested (549 Core tests, the audit and the matter identity closing on a vertex world) and
smoked the same night. Three fast-step screens on worker 7 found three faults in turn and
each was fixed and committed: the cull collapsing the field (merging is now a cap-only
rule), the matter gate unable to afford a child from a 1 m kernel (the matter field reads
through its own 1.8 m kernel), and a take delivering less than its gate promised while
conception booked the price (the take fills, conception books what was taken, and the table
prints `mat resid` beside `audit`). The fourth screen, `r30v-s2t`, ran seed 2 for 10,000 s
at dt 0.02: `mat resid` 0 throughout, 1,567 alive at the end, the detritus loop closed, a
stomach line of 20 that fails the scorer on stability alone (0074). It also found the fill's
eight-pass bound refusing takes as short; the bound is the neighbour count from 312e9b5, and
`r30v-s1u` screened seed 1 on that build for 10,000 s: `mat short` 0 throughout, 85
inherited stomachs at the end, and the clade scorer's first vertex-world pass.
D082 (the price of a bud) is folded into the same build, and round 30 (0075) is that build.
Conditional (D084): 0072's pre-registered follow-up to a failed M4, senses off with added
mass on in the cell world, runs only if round 30 reads below round 28 as well; if round 30
reads 3 of 5 or better the world control closes the question. Closed: round 30 read 5 of 5,
above round 28's 3 of 5, so the condition cannot fire.

The goal has been met once, and no world since has matched it. D063, as amended 2026-09-04,
asks for a clade that lasts. One connected absorptive clade must be alive for 20 consecutive
samples to the end of a 30,000-s run. It must hold 10 or more members through the last two
lifetimes (6,000 s) and still be breeding. It must do so in at least 3 of 5 seeds, with the
population floor closed, on an inherited photosynthetic lineage. Round 18
([logbook/0054](logbook/0054-the-confirmation.md)) passed 4 of 5, at dt 0.01, with exudation
0.15 (D070), in the discovery regime and in a closed matter world. That world still replays,
and we have gone back to it. Every round since stacked a change on it; here is what each found.

| entry | what it found |
|---|---|
| [logbook/0055](logbook/0055-the-dry-deep.md) | A faster matter sink does not wet the deep. About 90% of the world's matter is locked in bodies whatever the sink does, so D071's lever was the wrong one. Not adopted |
| [logbook/0056](logbook/0056-the-queue.md) | The world bred oldest-first, an artefact of a list order nothing had specified, and the stomachs had come to depend on it |
| [logbook/0057](logbook/0057-energy-buys-matter.md) | Letting energy bid for matter, and tripling the stock, each changed the world's size and not who wins it. Neither adopted |
| [logbook/0058](logbook/0058-the-open-budget.md) | With matter flowing in at the surface the population becomes a flow. The leaves lock it within a step, so almost nothing reaches burial |
| [logbook/0059](logbook/0059-the-newborns-spin.md) | A newborn's link in r20q-s1 spun up in one step. A non-finite body is now dumped and counted as a divergence, and a drive limiter caps each joint above dt 0.01 |
| [logbook/0060](logbook/0060-the-outflow.md) | Matter delivered at the vent's base (D067) does reach burial: 43–50% of the influx against the surface's 16%. Neither shape balanced at 0.6/s |
| [logbook/0061](logbook/0061-the-open-world-at-the-fine-step.md) | The open world scored 3 of 5 at the fine step. The stock kept growing, and every population sat at the waterline, because the plume lifts bodies through a top that was not there |
| [logbook/0062](logbook/0062-the-senses-answer.md) | The perception build. All seven sensor channels answer, and the replay stayed bit-identical |
| [logbook/0063](logbook/0063-the-theatre-opens.md) | The theatre's first cut. It re-simulates a recorded run and checks itself against the recording as it goes |
| [logbook/0064](logbook/0064-the-crowd-costs-nothing.md) | The shared-space spike. Contact between creatures costs nothing measurable at 250 to 2,000 bodies |
| [logbook/0065](logbook/0065-the-first-shared-water.md) | D077's box screened at the fast step and worked: 4 of 5, and a stomach lineage evolved at the vent. Its costs were the plume's crowd, a springy placeholder floor, and the size of the world |
| [logbook/0066](logbook/0066-one-box.md) | The build record for D077: the box, the wrap, the restoring top, the real sea bed, newborns placed beside the parent |
| [logbook/0067](logbook/0067-the-shared-world-at-the-fine-step.md) | 2 of 5. The lean dose starved the founders' stomachs, and a mutant stomach line then evolved at the vent's floor in three seeds of three |
| [logbook/0068](logbook/0068-the-shared-world-fed.md) | The same world fed at influx 0.6/s: 3 of 5 pass D063 as amended, below the round's bar of 4, and the stock grew 44–53% over the last third in every seed. The fed world is a screen |

[logbook/0069](logbook/0069-the-shared-world-does-not-replay.md) then reframed all of it: the
shared world does not replay. Six runs of one seed on one build gave six different worlds.
They part at step 147,778 by one or two ulp, where touching bodies were solved in a different
thread order. Turning Unity's job worker threads off restores identity over 300,000 steps,
and over the million steps of det6-a and det6-b. D078 makes single-threaded physics the
default and records it in every manifest. No round's reading changes, since a seed was already
read as one draw ([logbook/0052](logbook/0052-the-coarse-step.md)); the false promise was that
a shared-world seed could be re-run and watched.

## The path, ruled

D079 (owner, 2026-09-06) set the method: one change at a time, asking D063 of each, a
change that costs the rule read rather than tuned around. D081 (owner, 2026-09-07, after
round 28) set the base and the order.

1. **The base world is round 28's**: round 18's closed world plus D077's box, wrap,
   placement, restoring top and real floor, under D078's single-threaded physics
   (`rounds/launch-r28.ps1`). It meets the goal at 3 of 5 and misses round 18's reference
   by one seed, which starved on its larder (logbook/0070).
2. **Movement that pays** (D075's first item), next. On the base world with added mass on,
   the three senses on, and the global brain removed, all in one build that is a new
   realisation of every seed. The draft is `logbook/specs/movement-prereg-draft.md`; it is
   rewritten for this world before launch, with the ledger setting the added-mass
   coefficient and the active-versus-clamped assay (queue item 7) beside it.
3. **The vertex world with the price of a bud** (D083 and D082, owner 2026-09-07): the
   water as vertices, so that blind movement pays by what it refreshes, and the neuron and
   its inputs about tenfold cheaper, in one build on the base round 29's reading leaves.
   Round 29 is the price control and round 28 the world control. Pre-registered as
   logbook/0075 on 2026-09-08 with the work fraction at 0.25 from round 28's first window;
   fast-step screens first (`rounds/launch-r30.ps1 -Dt 0.02`), the confirming round at 0.01.
4. **The water stirred less** (D085, owner 2026-09-08): round 31 runs the detritus mixing at
   0.02 m²/s on both axes, everything else round 30's, because at 0.2 a sitter's hole refills
   in five seconds and a mover gains nothing (logbook/0076); pre-registered as
   logbook/0077; the screen stood (five of five, three passing the scorer at 10,000 s) and
   the round ran at dt 0.01 on workers 2 to 6 (`rounds/launch-r31.ps1`) and is read
   (0077 Results and Verdict): M0 held, M1 failed in reverse, M2 failed, M5 failed, M4 in
   three, M6 in four with seed 3's caveat. Seed 3 fell at 11,533 s to a body the solver threw to a
   finite height no test caught (0077's "Seed 3 fell"); the guard was widened the same
   evening and seed 3 reruns as `r31-s3b` on worker 7 on the fixed build, so the round is
   split across two builds the way round 24 was (0061). If the prize appears, the round
   after screens the exudate fraction; founding a vertex per excretion is queued behind both.
5. **The water as a grid** (D086, ruled 2026-09-09; logbook/0078): cells that hold
   amounts, one cell per mouth, Fick's law between neighbours, upwind advection, corpses as
   particles; matter cells 5 m from 0078's screens, matter mixed at 2 m²/s on every axis,
   corpses at 0.005/s. Round 32 is its base round and is running (0079); read M0 to M6
   against round 31's same seed when it lands. The negative vertex is retired unbuilt with
   its geometry argument as its record.
6. **A joint made free** (owner 2026-09-09; logbook/0080, amended): the hypothesis that joints
   are selected out for their price, tested by charging nothing for the idle muscle, the
   stroke and the brain; read as the jointed-parent share against the founder draw, with
   round 33, the growth base, as the priced control. Round 34, after the owner's theatre
   pause; if the share collapses anyway, the divergence dumps, the joint mutation rates and
   jointed bodies' depth are read before anything is priced again.
6b. **Every code change orphans every earlier run for the theatre** (owner 2026-09-09: "a
   problem we should consider on its own", not to be answered now). The theatre replays a
   run only under the build that recorded it, and the farm compiles Core from the main tree,
   so the day growth landed no run on disk was replayable. Candidates when it is taken up: a
   tagged build per round kept beside the tree, the theatre built against a run's recorded
   commit in a worktree, or a replay-only mode that loads old formats read-only.
7. **Bodies that grow** (D087, ruled 2026-09-09; logbook/0081):
   `BirthInvestment` as a fraction of the parent's tissue value replaces the endowment,
   `AdultScale` scales the plan, brood stays; a child is born at the investment over the
   litter and grows to its adult size before it breeds; three world constants with first
   values, a newborn mass floor of 0.5 kg against the divergence record. Built in both
   halves and smoked on 2026-09-09 (logbook/0081): the suite passes at 604, the smoke
   closes both books with 1,093 in-place resizes and no divergence, and children are born
   at a median third of their adult body. Ruled as D087 the same afternoon (investment 0.5
   with `MaximumTissueJoules` as a biomass ceiling beside the count, one gate of 0.08,
   founders 0.25 to 1.0, the reserve capped beside the body, the reserve floor 0.1); round
   33 is the growth base and is running; round 34, the free joint, launches on
   `rounds/launch-r34.ps1` once round 33 has read and the owner's theatre pause has ended.
8. **The ten rounds after the base** (owner, 2026-09-11 morning: "plan the next 10 rounds
   and change only if a result compels us"; the box folded in and "proceed autonomously").
   One change per round, each pre-registered, each read against the round before it, which
   is its control and never a replay where a build moved the hashes. Rounds 34 (the free
   joint, running) and 35 (the base, read) stand before it.
   1. **Round 36, the link that earns**: `EVOSIM_LINK_PHOTO` 0.5 on round 34's launcher; no
      build; pre-registered as logbook/0089; read 2026-09-12 as logbook/0092 (five of five;
      jointed inherited at 30,000 s of 73, 4, 0, 0, 0; the standing line a clade of jointed
      eaters in seed 1).
   *Resequenced 2026-09-11 evening by the agent under the owner's grant, after the owner
   watched `r36-s1` in the theatre and saw two things the report had been counting without
   anyone reading them: bodies teleported at the seams (the wrap, once per body per hundred
   seconds since the current carries) and a crowd a body apart (median nearest neighbour
   0.65 m), which is read as the reason movement has never paid: a sitter eats as well as a
   swimmer when food is a body length away. D089 carries the design
   and the three rulings; the old order is in D089's sequence table and in git.*
   2. **Round 37, the tank** (D089, ruling 1): a cylinder of the same 100 m² with a glass
      wall, a gyre from stream functions that vanish on the wall, a cell mask on the grid,
      rings for patches. Built on branch `tank`, checked six ways (0093's sources), merged
      2026-09-12 and launched as logbook/0093 on round 36's world. The limiter is not on: its
      check failed (D089's check 4). The 2 by 2 box's shape code is absorbed and no round runs
      it.
   3. **Round 38, the dilute tank** (rulings 2 and 3): four times the area, 400 m², with the
      matter held at 6,000 units by a new tunable whose default is today's scaling; corpses
      as objects at 0.005/s; no new build (`rounds/launch-r38.ps1` is written). The dilute
      arithmetic passed (a 5 m matter cell holds 31 units at 0.235/m³; the 5 m mask overshoots
      the disc by 6%, D089's check 6). Read: nearest neighbour, founding (`mat blk`, `mat short`
      against births), the gyre's downwelling as the first patch, sitter against mover.
   4. **Round 39, a bed with shape** (was round 42): rocks, ridges, hollows; the current
      flows around them, detritus settles into them, the grid's floor follows; the theatre
      draws the same data. Moved up because in a dilute world it is a coast. Proposal first.
   5. **Round 40, a light sense** (was round 38): one new sense input, light and its vertical
      gradient; read on jointed against rigid against buoyant depth, now in a world with
      something to steer toward. Proposal first.
   6. **Round 41, the stroke priced alone** (was round 39): the work cost back to D082's 0.25
      with the idle charge still at 0.0001; no build. *First reordered 2026-09-11 morning
      after round 34's first three seeds (the free stroke driven flat out, about 23 W of
      unpaid work per jointed body, the solver throwing jointed adults, jointed newborns dead
      in their first minute half the time; confirmed 5 of 5 in logbook/0090): the stroke's
      price shapes what a joint does, the idle charge only what it costs to own.* Now behind
      the dilution and the bed, so that there is a chase to pay for.
   7. **Round 42, ellipsoids in the physics** (was round 40): spheres and capsules collide and
      drag as their three half-extents; preceded by the offline read of whether boxes have
      flattened. Proposal first.
   8. **Round 43, the anchoring cell**: holds a body to the bed or a rock against the
      current; pairs with the bed, now behind it. Proposal first.
   9. **Round 44, shading with a length scale** (was round 41): self-shading by the
      neighbours above rather than the patch mean. Moved down as the least urgent.
      Proposal first.
   10. **Round 45, the remaining prices restored** (the idle charge to 0.02, the neuron and
      the connection to D082's values) on whichever world of 39 to 44 carries joints; no
      build.
   11. **Round 46, the long arm**: one seed, 300,000 s, on the richest standing world, the
      stroke read against the water every 1,000 s; one worker for a week.
   Also from round 34: the drive impulse limiter at every step was built as a tunable in the
   tank build and failed its check (D089), so no round runs it; what throws a jointed adult
   is open, and the next instrument is a per-link dump at the step before the non-finite one.
   Beside the ten and not in them: 0084's bin 3 screens (dispersed against undispersed on
   round 32's seeds, mixing 0.2 against 0.02, corpses off) on any free worker; the skin in
   the genome and the theatre's sun, which need no round; predation, which the owner ruled
   too early; surface waves as physics.
9. **Predation on contact** (`fable-propose-predation.md`, consolidated), the first thing a
   brain can be selected for, after the ten (owner, 2026-09-11: too early).
   *Run length (owner, 2026-09-08 night: "that sounds good"):* 30,000 s gives 40 to 50
   generations along the deepest line and three to four turnovers of the standing crop,
   most of them in the first third of a run. That is enough to read whether a trait the
   world already contains is kept or lost, which is what every round so far has asked, and
   not enough for a trait that needs several mutations to line up before it pays. Rounds
   that ask whether a trait is kept stay at 30,000 s. The question of whether a wired sense
   is *used* goes to an inoculated round (round 15's tool), not a longer one; it is decided
   after round 31 reads. *Mutation rates stay where they are (owner, same night: "agreed"):*
   a child already carries of the order of one structural change per birth and brain
   changes are the cheapest, so novelty is not what is short; more mutation breaks
   assembled combinations as fast as it makes them and makes the inherited columns read
   draw rather than descent. The one targeted test allowed is the input-rewiring chance
   alone, with a control, if inoculation shows the world keeps a wired sense but never
   finds one.
9. **The open matter budget** (D074) and the vent, when a round shows the larder binds.
10. **The cell types and immigration**, then the archive and the islands.

Two bars are named from D081 on: *the goal* (D063's 3 of 5) and *the reference* (the base
world's own count). A change joins the base only at the reference; a round that meets the
goal and misses the reference is read as "meets the goal, cost recorded".

## Queued, in order

1. **The D078 build** (done 2026-09-06, commit `d2b59ac`): `physicsJobWorkers` in the
   manifest, the count set per launch, identity proven on a zero-worker digest pair over
   300,000 steps (0069) and then 1,000,000 at a full round's population (0070).
2. **The scorer** scores every clade rather than the largest (done 2026-09-06, commit
   `b9baef1`). A seed passes when any connected clade meets every clause; the report names
   it and still prints the largest. Re-scored: round 18 stands at 4 of 5 with its minima
   unchanged, and no historical verdict moved.
3. **A photosynthetic flag on lineage birth rows** (done 2026-09-06: `pho` on every birth
   row, Core change; `pho-a` identical to `jobs-a` over 3,001 digests, the tiled replay
   unmoved). Runs before it print `flag absent` in the scorer's producer readings.
4. **A current-build round-18 check** (done 2026-09-06: `r18chk-s1` matches `r18x-s1` on
   all 30 samples to 3,000 s, so the historical five stand as the control; 0070 records
   it, and the report file the check overwrote by mistake was rebuilt from the run's data).
5. **Round 27's results** (done 2026-09-07, logbook/0068): over the last two lifetimes the
   stock rose at 49–70% of the influx in every seed, and burial carried the rest.
6. **Run identity** in DESIGN.md section 7 and CLAUDE.md gains the build's `simHash` and the
   physics worker count, both read from the manifest, once D078 has landed.
7. **The movement pre-registration** gains a second layer: an active-versus-clamped assay on
   saved members of the jointed clade.
8. **A `mat here` column** in the run report (done 2026-09-07 in the movement build, with
   `sense`, `dep jnt` and `dep rig`).

Captured 2026-09-07 from the three outside reviews (logbook/0071), in no order of urgency:

9. **The movement rule's ecological layer**, beside item 7's assay: a connected jointed clade
   persisting two lifetimes, recruiting in the final window, paying positive net after
   mechanical work; the assay repeated across several starting orientations and positions.
10. **Round 18 as a committed reference**: it exists only as a launcher script and five
    gitignored run directories. Its config, hashes and a representative lineage should be
    committed once if it is the base D079 builds on.
11. **A manifest-versus-header contract test at the Unity boundary**: the one seam with no
    automated check; the by-hand header read stands in for it and is where every settings
    bug in this record was caught.
12. **The transactional guard test D052 queued** (force an `Admit` failure; field matter,
    `MatterInBodies`, parent energy and `EnergyOut` all close) still does not exist; D077's
    ordering makes today's refusal paths safe.
13. **Two columns**: gross photosynthesis per window (the one term in the energy identity that
    is inferred rather than reported, and the balance rule will want it) and the matter drawn
    at conception per window (`mat blk` counts refusals; the units drawn are not reported). The flux counters are world
    totals; the patch dimension is only a standing-stock spread.
14. **The clade scorer checks nothing about which round a report belongs to**; the launch-side
    `-ExpectSimHash` guard stands in for it.
15. **The theatre has never been looked at by a person.** Its identity check proves it is the
    right world, not that the world is legible; one session in the Editor before it carries a
    reading.

Captured 2026-09-07 from the Astra review (its response file at the root has the verdicts and
the running status), in the order they are done:

16. **Feeding allocation and the stillbirth charge** (the review's R1 and R2; done
    2026-09-07, commit `1fb3752`; `r28chk-s1` identical to `r28-s1` over 3,001 digest steps). Availability
    frozen for the consumption pass, a short take fed back to the ledger, a zero-part body
    refused before any charge, an orphaned-matter invariant, and `stillbirths` and
    `mat orphan` in the statistics and the table; tests for reordered identical feeders,
    a capped take, and a stillbirth under D065's fixed term. Neither branch fired in any
    scored world (the response file has the measurements), so the fix is expected to replay
    the record bit for bit, proved on a zero-worker digest pair against `r28-s1`'s digest.
17. **The scorer's window** (R4; done 2026-09-07, same commit): an upper bound at the last sample, the interval read from
    the sample axis rather than assumed to be 100 s, and a completed-manifest gate that
    prints *provisional* on a running arm; a fixture for each.
18. **One Unity and script build** (done 2026-09-07; compiled on worker 7, the other workers
    refreshed after round 28's arms end, so the next launch reports a new `simHash`):
    inoculation counted in `Ecosystem`'s reconciliation revision; the theatre refuses a
    recording with an inoculation and compares the Unity version as well as the hashes;
    `EvolutionRun.Env` refuses a malformed value instead of defaulting; `new-worker.ps1`
    refuses a worker with a Unity process; `run-arm.ps1` restores its settings in a
    `finally`; the lineage writer is flushed on every orderly end (r25-s2 lost 38 ids to its
    wall); the digest's cadence and dump settings are written beside the digest; the digest
    and determinism comparisons exit nonzero on a mismatch.
19. **The research registry** (done 2026-09-07, commit `cad91a2`): the disclosure sentence,
    [CB18] and [PU16]'s retrieval records, round 4's count, the scope of Q1's answer, the
    framing of Q10's rule.
20. **The predation proposal consolidated** into one operative design for the owner (done
    2026-09-07; `fable-propose-predation.md` is one text again).
21. **The movement draft** (done 2026-09-07): the right denominator (8 of 930), the
    sensing-versus-acquisition seam stated, added mass named as off.
22. **A fresh seed batch** once the rules and instruments are stable, so the conclusion is
    checked beyond the five founding lotteries every round has reused (owner's call on when).
23. **Astra's second response** (2026-09-07 evening; the companion file's last section).
    Done the same evening: the scorer's line reads `CENSORED` with the reason and the
    seconds reached on any run that ended short of its budget (wall, ceiling, error, a
    stop), with a fixture; `digest-diff.py` returns its exit code on the path with no body
    dump; DESIGN §0p, CLAUDE.md, D019's note, the primer's butterfly section and README's
    source count reworded. Still queued, on priority and not on need: **a checksum table
    for the research sources** (SHA-256 of each PDF under `research/papers/` beside its
    retrieval record), so a future rebuild of the corpus can tell the file it fetched from
    the file that was read. Done 2026-09-07, after round 29 launched: every entry in
    `research/FETCH-RESULTS.md` carries its PDF's SHA-256 and size, or says no PDF is on disk.
24. **Two primer pieces** (owner: "sgtm", 2026-09-08; done the same evening, commits
    `b974e92` and `585d435`): piece 07, the accounting, the two books the world keeps
    (the energy audit since logbook/0008, the matter identity since 0074) and why every
    operation is a transfer; piece 08, the water as vertices (D083, 0074, 0075). The
    accounting piece first, since the vertex piece leans on it; inference marked, values
    linked to DESIGN and not restated, per `primer/README.md`.

## The owner's ideas of 2026-09-10 evening, in the order they will be taken

Written down here so that none is lost; each is agent work unless marked as the owner's rule.

1. **The carved skin** (in build tonight, `logbook/specs/skin-spec-2.md`): inward-only impressions,
   a pinch at joints, a carved bed, a close view, three carve depths for the owner to pick
   from pictures; sphere parts drawn as the ellipsoid the genome asks for, bounded by the
   collider.
2. **Skin seeded from the genome** rather than from the body's id, so relatives resemble each
   other and a clade has a face. Theatre only.
3. **The sun and the surface**: a rippling surface plane seen from below, Snell's window with
   the sun's disc, intensity from the run's irradiance and its day-night cycle if on; a
   screen-space shimmer; light shafts if the frame time allows. Theatre only.
4. **A reading, offline, any time**: have the shapes that already can flatten (a box's three
   half-extents mutate on their own axes) flattened under light pressure in the record? From
   the snapshots.
5. **Inherited skin genes** (owner's rule, a proposal after round 34 reads): six to ten neutral
   numbers under one gate, inherited and mutated, drifting apart between species and shown by
   the theatre; selected only once something can see. Genome format bump, no per-step effect.
6. **Ellipsoids and flattened capsules in the physics** (owner's rule; on the table once round
   34 answers whether a free joint survives): the visual and the drag honour the three
   half-extents, the collider is the best-fitting primitive inside.

The round work stays first: round 35 read, then round 34 on this base.

## The owner's ideas of 2026-09-11 morning, with the agent's view on each

After round 34's first seed (a free joint survives founding and thins afterwards; 0080),
the owner's call was that predators are too early and the world should get more interesting
before any long arm is run. Each of these is a world rule and comes to the owner as a
proposal before it is built; the order is the agent's suggestion, by how much each gives
selection to see for what it costs. What already exists is said so that nothing is built
twice.

1. **A light sense.** The three senses are chemical, energy and flow (D081); nothing reads
   light or its direction. Light is the one gradient this world has that a body could steer
   by, the vertical one, and a leaf that could feel "brighter above" has the first reason to
   use a stroke. Small build: one input kind, one hash move, no new tissue. The agent's first
   pick.
2. **The link that earns** (round 36 as proposed, no build): `EVOSIM_LINK_PHOTO` at 0.5 on round
   34's launcher. A hinge on a producer instead of dead weight.
3. **More extreme shapes.** A box's three half-extents already mutate on their own axes with
   no aspect limit (`Mutator.MutateNode`; only a part whose mean half-extent falls under the
   extinction threshold is pruned), so a plate or a rod is reachable today and item 4 of the
   evening list asks whether any lineage has gone there. A sphere and a capsule cannot
   flatten, because the physics collides them as a ball or a tube; that is the ellipsoid
   item below. So this is a reading first, then a shape.
4. **Ellipsoids and flattened capsules in the physics** (item 6 of the evening list): drag
   and the visual honour the three half-extents, the collider is the best primitive inside.
5. **New cell types.** The buoyancy cell exists (D049; `Lift` is a mutable dial, founders
   carry it at `EVOSIM_FOUNDER_FLOAT`), and it is the cheap way to hold a depth that the
   stroke competes with, so it is read against the joint in every round from 34 on. A "nose"
   is the chemical sense, already on. Genuinely new would be: a light sense (item 1); a
   sticky or anchoring cell that holds a body to the bed or to a structure against the
   current, which makes the bed matter; a storage cell that changes the reserve's price.
6. **A bed with shape**: rocks, ridges, hollows, a slope; static colliders the current flows
   around and detritus settles into, so that food is patchy where the current cannot smooth
   it and there is somewhere to be. Medium build (a height field or a set of convex
   colliders, the grid's floor cells following it, the theatre's bed mesh from the same
   data). This is the "food the current does not smooth" prize in its most natural form,
   and it pairs with the anchoring cell.
7. **Surface waves.** Cosmetic in the theatre first (the evening list's item 3, the sun and
   the surface). As physics it is another current mode whose orbital motion decays with
   depth, and it would move only the top few metres; worth it once something lives there.
8. **The skin in the genome** (item 5 of the evening list): inherited, mutated, shown by the
   theatre, neutral until something can see.
9. **The box that stops looking made**: the skin's third day. The owner, on `r35-s1`'s close
   view at 5,000 s (2026-09-11 midday): "the spheres look amazing", the boxes "still look
   somewhat non-biological"; "let's try". A corner radius a third of the smallest half-extent rather than
   the sliver it has, an inward taper toward one end so a box reads as a seed rather than a
   brick, one low-frequency bend or twist per body seeded from its id so no two are congruent,
   a wider and softer rim. All of it inward and inside the collider, as the carve is; no hash
   moves. Beside it, a second way for the close view to choose its subject, the body with the
   most parts rather than the largest, framed by its neighbours rather than its reach, because
   the eight-metre chain in `r35-s3` backs the camera off until everything is a speck.
   Photograph the same seed-1 crowd afterwards for the comparison. Waits behind the box merge,
   which touches the same theatre files.
10. **A safari** (owner, 2026-09-11 midday: a theatre feature that finds the species, visits
    the interesting ones, takes their pictures and explains each, "without requiring ad-hoc
    intelligence"). The agent's view: it can be procedural to the last sentence, because every
    fact a field-guide entry wants is already in the run. Clades from the scorer's parent
    walk (or genome distance under the species knob); a clade's founding time, the parent
    clade it split from and the mutation at the split (the two genomes differ in a
    listable way); its share of the living, its peak, its generation depth; where it lives
    from `positions.jsonl` (depth band, spread, drift with the water); its body from the
    genome (parts, shapes, symmetry, recursion, joints, guild); its life from lineage rows
    (lifetime, age at first birth, litter, investment); its economics from the ledger
    (net watts, break-even density, against its ancestor's). "Interesting" is a ranking
    over those: novelty (distance from the ancestor), success (share), persistence (age),
    rarity (old and small), firsts (the first jointed clade, the first with three parts).
    A name is a deterministic binomial from the genome hash. The explanation is a template
    with the facts in its slots and a few conditional sentences; what it cannot do is say
    why one won, beyond what the ledger measures. An entry says "the split changed X; the
    ledger reads Y" and stops. Three stages, none of them a round: a census
    script that writes the guide as prose with the names and the numbers (a day, on the
    existing reads); pictures through `theatre-snap.ps1` with a view that frames a named body
    at a named second (half a day; Mode A can also grow one alone as a portrait); and the
    theatre's tour mode that flies to each in Play mode with the entry on screen (later).
    An LLM, if ever, is a polish pass over the template's prose, off by default, and the
    facts never come from it.


Queued behind the next Core change (it moves both hashes, so it lands between rounds and never
alone): eighteen comments in nine C# files still cite `scratch/floor-spec.md`, `digest-spec.md`,
`footprint-survey.md`, `footprint-build-report.md` and `floor-build-report.md`, which now live in
`logbook/specs/`. Also for the owner: the `scratch/` cleanout list in the migration report
(logbook/specs/scratch-migration-spec.md's companion, 2026-09-10): 70 MB of logs, review
captures, probe output, one-off edit scripts, stale copies; nothing was deleted.

## The decisions in front of the owner

- **Things wrong in motion** (owner, 2026-09-11 evening, after watching `r36-s1` in the
  theatre): "there are some issues with the world that you can only see when rendering",
  set aside for now and not yet named. The agent reads stills only, so when they are named
  the route is a Game Bar or Recorder film of the session and frames pulled from it at the
  seconds in question, read as pictures beside the numbers.
- **The aquarium's second and third rulings** (D089, 2026-09-11): the dilution to 400 m² with
  the matter held at 6,000 units, and corpses as objects at 0.005/s from round 38, go ahead on
  the agent's recommendation under "follow your suggestions" and can be overruled before
  round 38 launches. The limiter of the same entry was built, failed its check (27
  divergences against 17, same signature) and is not adopted; no ruling is needed unless the
  owner wants it kept on anyway.
- **The pillow dial** (skin day 3, logbook/0091): 0.34 (the old rounding, the default) or
  0.5 (every box a bean); two pictures in `scratch/snaps/skin3-*/`.
- **The producer threshold** is unsettled. D063's amendment asks for one living inherited
  member with a recent photosynthetic birth; the scorer substituted 10 members through two
  lifetimes. Both will be printed, and the ruling picks one.
- **A late resource-balance rule** needs its own decision, with a tolerance: what a levelled
  stock means as a number.
- **Multithreaded physics** may be allowed for labelled screens; D078 keeps the setting
  adjustable and recorded, so it needs only a sentence.
- **Predation on contact**, in `fable-propose-predation.md`: the injury pool with fixed
  geometry, dt 0.01 from the first screen, stable contact keys, an internal matter reserve.
- **Extending a passing seed past 30,000 s**, to see whether the balance holds.
- **The maintenance assay**, a multi-genome inoculum with cell-type mutation off: a good
  instrument for after round 28, and one the owner should scope.
- **Added mass** is ruled on for the movement build (D081); the coefficient's value is set
  in the pre-registration with the ledger's help, and the owner sees it there.
- **The food chain's meaning under D063** (any absorptive body, or a lineage materially
  dependent on detritus) and **the split between the lineage rule and a balance rule**, both
  noted under D063.
- **Whether the absence of CI is a choice.** The suite runs by hand before a launch; nothing
  says whether that is the standing decision.

Raised 2026-09-07 by round 28's reading (logbook/0070), and still open after D081:

- **The width of the box.** M5 failed in every seed at area 100: a wider box is a different
  light budget, so the width is a world rule. Raised again if the movement round shows
  packing binds. (The collisions-off control, the global brain, the two bars and the
  futility clause were ruled in D081.)

## How the experiments are run

CLAUDE.md holds the commands and the gotchas. This is where each tool sits.

| | |
|---|---|
| workers | arms run on `unity-w2..unity-w7`, one per worker, at most five at once; after a change under `unity/Assets`, run `scripts/new-worker.ps1 -Workers N` once per worker and check the hash |
| launching | `scripts/run-arm.ps1` with `-ExpectSimHash`, logs in `scratch/logs/`; end an arm with `stop-arm.ps1` and never with a kill; read every setting back from the run header and the manifest, never from the launch command |
| reading | `scripts/analyse-arm.ps1` by column name (`-ListColumns`), never positionally, passing `-Columns` as a real array from inside PowerShell, since through `pwsh -File` the comma list arrives as one string and every cell reads `?`; `mat blk`, `floor` and the `det in/out/exuded` columns are per-window deltas; `matterHere` is in `stats.jsonl` and not in the table; `lineage.jsonl` holds one row per birth with the `pho` flag from the 2026-09-06 build onward (older runs print `flag absent` in the scorer), while the report's `photo` columns carry the producer population |
| scoring | `scripts/clade-score.ps1` for D063, `scripts/absorptive-log.ps1 <arm>` for what a stomach earned, `scripts/lineage-invasion.ps1` for an inoculated lineage, and `scripts/ledger.ps1` (D069) before a worker |
| monitoring | `scripts/monitor-r13.sh` over `scratch/evosim-watch-arms.txt`; it exits when the list is empty and must be restarted after the list is set |
| throughput | about 1,800 bodies at dt 0.01 with five arms sharing the machine is five to six hours per 30,000 s; single-threaded physics cost nothing measurable in 0069's confirmation at 518 bodies, though its short probes at 120 to 400 bodies read about 15% with a spread as wide as the gap, and it is unmeasured at a round's population (0070 budgets a quarter); the ceiling (`MaximumPopulation`, `EVOSIM_MAX_POP`) ends a run as a censored runaway |

## Open decisions for the owner

- **Speed, and the game's clock** (owner, 2026-09-03). The timestep has done what it can. A
  world eventful on a human timescale is a world-rule question: shorter lifetimes, faster
  turnover, a theatre that runs the farm ahead and jumps to events.
- **Immigration as a world rule when the cell types expand** (owner's hypothesis,
  2026-09-03). Score establishment, tag the immigrants, run with and without a trickle.
- **The paywalled reading list** in `research/LITERATURE-REVIEW.md` needs the owner's
  institutional access. Its queue was re-prioritised on 2026-09-06 around the open-flow
  matter balance, contact feeding, and movement-assisted foraging.
- **Pushing code and prose** in batches is approved (2026-09-01); data, output and weights never.
- **The Astra review of 2026-09-07** (`gpt-astra-2026-09-07-1316-review.md`) is answered in
  `gpt-astra-2026-09-07-1316-review-response.md`, which carries the verdict on every point
  and the status of the work it queued (items 16 to 22 above). Both files are the owner's to
  absorb or delete when that work is done. The three earlier reviews were captured and
  removed on 2026-09-07 (logbook/0071).
