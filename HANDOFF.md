# Handoff: where to pick up

*Rewritten 2026-09-13 from the current state. What happened is in the logbook, and why it was
chosen is in [`DECISIONS.md`](DECISIONS.md). This file says only where things stand and what
is queued; it is rewritten, never appended to.*

## Where things stand

**Round 41 was stopped as futile at 23:03 and round 41b, the same world with the
remineralisation at 0.002 /s, launched at 23:04 (logbook/0107's second launch section;
`rounds/launch-r41b.ps1`; queue `scratch/r41/queue-b.ps1`, detached, log
`scratch/logs/r41b-queue.out`). Seeds 1 to 3 verified: `configHash faeca97c`, the same
`simHash` and `coreHash`, `remineralisationPerSecond 0.002` in every config and `remin
0.002 /s` in every header, `prereg.json` at `af39b13`.** What round 41 showed at 7,000 to 9,600 s: both books
closed, the count flat at 530 to 600 (E1 held at 5,000 s in all three), the leaves
uptake-bound on 60 to 90% of their steps, the margin falling, and the marine snow nobody
ate plateaued at 0.47 to 0.53 of the 3,000-unit budget in every seed, E4 failing high.
The rate had been sized for a fifth of 11,000 units; four times the rate puts the stock
near a fifth of 3,000. The eaters had barely founded (9, 6 and 0 inherited), read as a
crowd a fifth of round 40's supplying a fifth the mutants, a reading the relaunch tests
under E5. E1 to E8 stand unchanged, E4's band included. `runs/r41-s1..3` are kept and not
read further. **Round 41b stopped as futile at 02:55 on 2026-09-19 (seeds 1 to 3 at 5,600, 7,900 and
6,400 s; the queue killed before seeds 4 and 5).** Not for the joints alone. The pace fell
from 1.0x at 3,000 s to 0.14 to 0.18x by 02:40 with the count flat near 800 to 1,000, and
the wall split says why: physics rose from a quarter to half the wall on **contact
pairs**, cumulative `contactPairs` growing by 150 to 390 million per 1,000 s in seeds 2
and 1 against round 40's 0.1 to 0.5 million at 2,400 bodies, a thousandfold, and doubling
every thousand seconds. Seed 2 with 90 jointed bodies showed it as much as seed 1 with
630, so the joints are a cost on top and not the cause. Every seed passed E4's snow read
at 5,000 s (0.20 to 0.23 of the budget) before the stop, so the remineralisation number
stands; the eaters were 11, 0 and 2 inherited at the stop, unread. The arms used 1.15
cores each (measured over a minute), so the pace was the simulation's own. Candidate
causes, unread at the stop: bodies at full adult size in the film (body fraction 0.98,
where round 40's crowd was juveniles at adult scale 0.30), the placer's reservation
against grown bodies, `crowded` rising 20 to 99 a window; a corpse lingers about 3,700 s
until its joules fall under 1e-6 (1,600 standing, as in round 40; a cost, not the cause).
The diagnosis is the morning's first job, with `positions-read.py`'s nearest-neighbour
column and a reconstructed frame of seed 2 at 7,000 s (`scratch/snaps/r41b-s2/`).
**The pace, read at 01:10 on 2026-09-19, and the reason:** seeds 1 and 3 run at 0.22 to
0.26 times real time and falling, seed 2 at 0.42, with physics at a third to a half of
the wall where round 40 gave it a fifth. Under this economy the joint is nearly free (a
leaf's tissue is 0.38 J, growth draws no field, the link earns half a leaf's light, the
idle charge is 1e-4) and jointed bodies have spread: 627 of 825 in seed 1 and 688 of 864
in seed 3 with 1,530 and 2,440 degrees of freedom, against round 40's 25 of 2,249 with 55.
Seed 2, with 129 jointed, is the fast one. So the crowd the machine can afford is set by
degrees of freedom now, not by bodies, and at this pace 30,000 s is about 38 hours against
the 1,800-minute wall: the seeds are censored near 22,000 s unless something changes. This
is a finding of the economy (the old joint price was the matter drawn for tissue at
conception, and D098 removed it by design) and the joint's price is a world rule, so the
choice is the owner's: a longer wall and the neutral joint read as it is, or a price on
the joint (round 33's idle 0.02, or a link that earns nothing) and a relaunch. The seeds
run on to the 10,000 s eater read meanwhile, since that read does not depend on it.
**Fail fast (owner, 23:20):** the round is read as it runs and stopped the
moment it answers. The snow share is read at 5,000 s (E4's band, 0.10 to 0.40; a seed
above it again means the rate is still wrong). The eaters are read at 10,000 s, not
15,000: if no seed has `inherit` at 20 by then, the founding lottery is the block, the
round stops, and the absorptive-first world (`fable-propose-soup.md`, written 23:30 for
the owner's ruling: eaters as founders on a fading influx of charged matter, the leaf by
mutation) is the next round. The paragraph below is round 41's launch record, kept for the pointers.

**Round 41, the one-substance economy's base round, ran from 2026-09-18 20:14 to 23:03
(logbook/0107, D098 as amended).** It is round 40's world at 3,000 units of matter,
from 11,000, and a 100 J per-child overhead, from 25. Five seeds run at three arms on
workers 2, 3 and 4, dt 0.01 for 30,000 s, wall 1,800 minutes. The queue is
`scratch/r41/queue.ps1`, detached, logging to `scratch/logs/r41-queue.out`, and seeds 4
and 5 launch as arms end. Seeds 1 to 3 are verified from their manifests, configs and
headers: `simHash 50cf58e4…`, `coreHash 729a0de1…`, `configHash a2aa45a6`, physics jobs
0, the budget 3,000, the overhead 100 J, a unit worth 100 J, the light's reach 6 m, the
reserve cap off, tissue 500 J/m³ on every cell type, and `prereg.json` at `3fa8876` beside
the arm and the run. The round is read on E1 to E8 against the 3,000-unit screen
(`r41o100b3k-s1`, dt 0.02, stopped at 2,500 s). Frames of seed 1 at about 3,000 and
6,000 s come from worker 5 (`scratch/r41/snap-early.sh`), one at a time. The owner's rule
from tonight binds the round. A seed that shows the adjustment the world needs is stopped
as `manual-futility` and the round re-planned rather than completed.

Two mislaunches precede it, renamed and not deleted. `runs/r41mis-s1..3` (20:10, a
minute each) ran the launcher's old defaults, 11,000 units and 25 J. The queue passes a
seed, a worker and the hash and nothing else, and the screened dials were on the command
line and not in the launcher (CLAUDE.md's gotcha). `runs/r41mis2-s1..2` (20:13) were the
corrected queue relaunched inside the agent's background task, and they died with it when
the agent stopped that task to detach the queue. Both sets read `stopped`, `manual-other`.

How the dials were set, all at dt 0.02 and seed 1, every screen stopped as soon as it
had answered. D098's build makes the count the capacity over what a breeder holds, since
a leaf's tissue is 0.38 J against a 25 J child. `r41smoke2-s1` built 6,300 bodies in
1,100 s at 11,000 units. A per-body standing cost does not bound it. The tissue value at
100,000 and 200,000 J/m³ (`r41t1e5-s1`, `r41t2e5-s1`) and at 50,000 with small founders
(`r41t5e4b2k-s1`, `r41t5e4b4k-s1`) froze the founding. The budget alone at 25 J
plateaued at 380 for 1,000 units and 750 for 2,000 (`r41b1000-s1`, `r41b2000-s1`), 400
starved at the floor, and it drifts several-fold as bodies shrink. At 100 J the overhead
is the floor under a breeder's holding. That gave 400 bodies for 2,000 units and 650 for
3,000, and 4,000 sat on the same line (`r41o100b2k-s1`, `r41o100b3k-s1`,
`r41o100b4k-s1`). The spec's §4 table, DESIGN
§5A.2d and CLAUDE.md carry the ruling. `EVOSIM_TISSUE_ENERGY`, `EVOSIM_FOUNDER_EXTENT_MIN/
MAX` and `EVOSIM_OVERHEAD` are launch knobs from tonight (header tokens `tissue N J/m3`,
`founders a-b m`, `overhead N J`).

The build: D098 as amended is on main from `a5cc504` (branches `economy` and `margin`,
merged; their worktrees and branches were removed on the owner's word at 20:25, and main
was pushed to origin): the spec is `logbook/specs/economy-spec.md` (§11 has the build
notes), the map `logbook/specs/economy-inventory.md`, DESIGN §5A.2d and §0x carry the
rules, CLAUDE.md the gotcha. The default suite is green at 710 and the full suite is green
after the mixing experiment's conservation check was made to span both states. Genome
format is 6 and every stored genome, snapshot and `config.json` on disk is refused;
`inocula/growth-ledger-config.json` and `growth-ledger-genome.json` are regenerated from
round 41's seed 1 and the 3,000-unit screen's snapshot, and the two historical configs
under `inocula/` are kept as history and marked refused. Worker 7 still carries round 39's
tree for its render.

**The snapshot render is built and merged (owner's ruling, 2026-09-18 about 22:00; spec
`logbook/specs/snapshot-render-spec.md`; merged `6a4f8ca` at 23:05, worktree
`scratch/wt-snapshot` and branch `snapshot-render` still present).** A still frame joined from a run's snapshot
and its positions row at a snapshot second, no re-simulation: `theatre-snap.ps1 -From
snapshot`, theatre side only, no hash moved, the frame labelled reconstructed with its
two caveats (adult size, default orientation). An Opus subagent built it on branch
`snapshot-render` against worker 6 in fifty minutes; the acceptance frames of
`r41o100b3k-s1` at 2,000 s (655 joined) are in `scratch/snaps/r41o100b3k-s1/`, and the
replay control of the same second is unchanged. Worker 6 carries the merged theatre and
the current `Assets/Evosim`. Both theatre renders were stopped for it at 22:03 on the owner's word: round
39 seed 1's full-length replay on worker 7 (`scratch/r39-renders-2.ps1`, since 15:11,
about three hours short of its 30,000 s frame) and round 41 seed 1's 3,000 s look on
worker 5 (its first attempt timed out on the 30-minute default wall). **The first pictures with it (22:49 to 22:50, worker 6, a minute each):** round 41 seed 1
at 3,000 s (539 joined of 539) and 6,000 s (590 of 590), `scratch/snaps/r41-s1/
r41-s1-t3000-recon-side.png`, `-t6000-recon-side.png`, `-t6000-recon-top.png`. What they
show: at 3,000 s the whole crowd in the top ten metres over the shallow half, leaves and a
scatter of jointed bodies, one body below 15 m; at 6,000 s the same band, thinner, with
more multi-part jointed bodies in it, a dozen bodies strung down the deep half to the
floor, and from above the crowd heaviest on the rim's south-east arc and thin at the
centre. My reading, as inference: the count is flat near 550 to 600 because the water is
stripped (`upt lim` 80 to 87%), the eaters have only just founded, and nothing yet reads
as an adjustment the world needs. **Round 39 seed 1 at 30,000 s was refused**: the reader
builds the furniture from `config.json` and every config before D098 is refused on its
missing tunables (`maxReserveMargin`), and round 39's genomes are format 5, refused the
same way. So a run recorded before a tunable cannot be drawn this way as built. Queued,
for the owner to weigh against the refuse-rather-than-default rule: a picture-only read
of the geometry (shape, area, depth, bed) and of a genome's body fields, tolerant of the
economy and margin fields it does not need, marked on the label. The owner ruled it at 23:20 and the subagent built it (spec §11; merged `1aac839`):
`PictureConfig` and `PictureGenome` read only what a picture needs, tried after the strict
readers refuse, the label's first line gaining `OLD-RUN READ`. Round 39 seed 1 at 30,000 s
drew in a minute (1,920 joined, format 5) and 0105 is written on it and the faithful 5,000
and 15,000 s frames, committed with its pictures and its key row.

Also tonight: round 40's read is in 0106 (V5, three seeds: L3 held where readable, L1
failed on the deep quartile, the deep tail is newborns dropped where nothing pays;
`logbook/specs/r40-read/`).

**The campaign's direction changed at midday on 2026-09-18 (D097).** The owner ruled "Let's
follow your recommendations" on the agent's diagnosis that the matter cell is a one-child
cliff that decides every reading, and that nothing in the world eats anything alive. After
round 40's read the foundation rounds leave the head of the queue; next are the matter
proposal (the per-child price, screened by a smoke on blocked conceptions per birth), then
the mouth proposal (herbivory by contact, then predation), then the senses and the stroke's
price. Both proposals are written from round 40's numbers once its read is in; the matter
smoke can run on a free worker before that. The owner's question on how a body is eaten
without senses is answered in D097: by contact in a crowd whose nearest neighbour is under
half a metre, and by the chemical sense that already reads the leaves' exudate.

**Round 40, the light's reach, ran from 2026-09-18 08:02 and was stopped at 16:57 (logbook/0106 has its read at the last samples).** What follows is the launch-time paragraph, kept for the pointers.
Round 39's world with `EVOSIM_LIGHT_REACH` 6 m and nothing else changed; five seeds at
three arms on workers 2, 3 and 4 (`scratch/r40/queue.ps1`, log `scratch/logs/r40-queue.out`;
seeds 4 and 5 launch as arms end, since the worker list is the three-arm rule). Seeds 1
to 3 verified from their manifests: `simHash 302df848…`, `coreHash 96488dec…`,
`configHash 866b711a`, `physicsJobWorkers 0`, `attenuationDepth 6` in every config,
`prereg.json` at `8e77099` beside the arm and the run. The header token `light reach 6 m`
is checked from each arm's log once the first table row is in (the smoke's log carries
it). Read on L1 to L9 with round 39's numbers as the baselines; frames of a live arm at
about 3,000 and 6,000 s on a worker the queue is not using (5 or 6), one at a time. The
wall is 1,800 minutes. Round 39's render chain restarted as `scratch/r39-renders-2.ps1`
(log `scratch/logs/r39-renders-2.out`, 2026-09-18 15:11: seed 1's 30,000 s frames first, then
seeds 2 to 5, 900-minute walls; the first chain's 600-minute wall cut seed 1 short of
30,000 s and its wait hung on an orphaned licensing helper, CLAUDE.md's gotcha; worker 7
still carries round 39's tree and is not refreshed), so a fourth Unity process is the
render and the owner's Editor is the fifth; the early-look chain (`scratch/r40/snap-early.ps1`, worker 5, one side
frame of seed 1 at 3,000 and 6,000 s) is the short visual check on top. **The early look
at 3,000 s (`scratch/snaps/r40-s1/`, replay faithful, 30 of 31 samples):** 1,255 leaves in
the top quarter of the tank, a packed row against the waterline thinning to nothing by
about 15 m, the whole lower thirty metres and the floor empty but for a dozen bodies; the
leaves' median 3.5 m at 1,000 s and 7 to 8 m by 2,300 to 2,600 s, moving down as the crowd
grows, and seed 1's mean 8.3 m at 4,300 s with 1,940 alive (round 39's seed 1 had 1,083 at
3,000 s). Seed 2 sits shallowest (mean 3.5 m at 2,700 s). Shading 2.8 to 3.7% at 2,700 to
4,300 s, round 39's run maxima already. No eater has founded in any seed by 4,300 s. My
reading: the band is forming where the ledger said and the film is thinning rather than
filling, which is L1's direction; nothing is read until 15,000 s. **The look at 6,000 s
(faithful, 60 of 61):** 2,460 alive; the row at the waterline is still packed, the crowd
now fills the top half of the tank to about 22 m in a gradient, and the lower twenty
metres hold a few dozen bodies with a handful near the deep floor, where round 39's seed 1
filled all 45 m at 15,000 s. The leaves' median from the positions: seed 1 7 to 8 m at
2,600 s, 14 m at 7,800 s; seed 2 8.5 m at 5,500 s; seed 3 7.6 m at 6,700 s, its 29 to 62
eaters 6 m below its leaves. My reading, as inference: the light made the dark column,
and the band is about twice as thick as the lone leaf's ledger said, sinking with time as
the crowd grows, so L1's 10 m bar is in doubt while L3's direction is already visible.
Where the band stops is the 15,000 s reading. **How the deep leaves live (the owner's
question, 2026-09-18 midday; `scratch/r40/birth-depth.py`, to move to the round's read):**
the ledger at reach 6 says a leaf at 14 m still earns +0.006 W at birth and lives 6,000 s
but never breeds (R0 0 from 12 m down), so surviving deep is cheap and breeding is what
needs light. Seed 1 to 10,100 s: a child is set down at its parent's own depth (median
difference 0.0 m; the dispersal disc is horizontal), and the newborns' median moved from
2.5 m before 4,000 s to 19.6 m at 6,000 to 8,000 s, so most breeding now happens at 15 to
25 m, deeper than the crowd. By birth depth: born in the top 4 m, 100% alive at 1,500 s,
62% breed, 3.5 children each; born at 16 to 22 m, 46% alive at 1,500 s, 9% breed, 0.22
children; born below 22 m, 88% dead within 500 s. The deep births are a sink fed from
above. The matter reads 0.024 units/m³ at the top against 0.028 deep, 190,000 to 270,000
conceptions blocked on matter per window against 1,700 births, and a 5 m cell then holds
3.0 units at the top and 3.5 deep against a child's 3.1. My reading, as inference: the
crowd is matter-limited and not light-limited; parents conceive where a cell can afford a
child, which is the deep water by a hair because the crowd strips the top faster than
the 2 m²/s mixing refills it; the ledger cannot see this (it has no matter draw). The
test is births by matter cell against the cell's stock, which needs the cell's matter on
the birth row or a per-layer count of matter-blocked conceptions: an instrument for the
read, not for this round.

**Round 39's entry waits for its frames.** `logbook/0105-the-shelf-was-never-in-the-light.md`
is drafted and uncommitted with a `PICTURES-15000-30000` marker; the numeric read is
committed as `logbook/specs/r39-read/` (`summary.tsv` first). Seed 1's 15,000 s frames landed
2026-09-18 08:59 (`scratch/snaps/r39-s1/`, identity 150 of 300 samples, faithful): the
leaves fill the whole 45 m from the waterline to the floor, densest in the top half, and
the eaters (601 of 1,915) sit in the lower half along the whole ramp with a scatter on the
deep side's floor, which is 0105's E4 and E9 in one picture. When the 30,000 s frames land,
look at them, replace the marker with both, copy the chosen frames to
`logbook/images/0105-…`, add the README row, commit; then restyle 0102 (its
pre-registration is read).

**The cloud CPU survey is written (`logbook/specs/cloud-cpu-survey.md`, 2026-09-18).** The
owner asked for prices and options for running the arms off the machine. The finding:
the licence is the decision (one seat, one instance; Build Server covers builds only;
Unity Simulation is gone), nothing a hyperscaler rents is as fast per core as the desktop
(0.6 to 0.8), and a desktop-Ryzen bare-metal box (OVH RISE-L, $177 a month, read) is both
the fastest and the cheapest at about $0.30 a seed. Cloud recordings replay in the theatre
as cousins, so screening would go remote and base rounds stay home. **The owner ruled it
off the same morning: the campaign stays on this machine.**

**Round 38, the dilute tank, is read (logbook/0101, 2026-09-15 night).** Five seeds on one
build ended at 30,000 s; D1 to D4, D6 and D8 hold, D5 fails, D7's count holds and its
anatomy does not. The world stands at 1,600 in every seed, mixed and thin, and every
seed's eaters rise to a fifth to two fifths of the living by 12,000 to 17,000 s and
starve to nothing by 23,000 to 25,000 s: a food crash (the detritus stock drawn from
120 to 196 kJ to 24 to 40 kJ at the peak, the eaters' net watts crossing zero at the
peak, `matterStanding` exact, shading falling), a consumer-resource oscillation with a
period longer than the run, and each later boom a fresh line from the leaves. The goal
rule fails five of five on its stability clause. The fields are uniform (`det cv` under
0.5 after 5,000 s in every seed), which is round 39's baseline. Seed 5's eight "throws"
are one-part bodies that leaked through the glass at the surface and were caught by the
radius guard, which should never fire (queued). The read's files are
`logbook/specs/r38-read/`.

**Round 37b, the water carried as water, is the base and is read (logbook/0095, 0097; D090,
D091).** Round 37's tank on the streams with the fluid acceleration force at 1, the
conservative transporter, the whole-body wall clearance at birth, the corrected `cols` and
the throw trace; one build (`simHash 5e164d01…`, `coreHash ad5c952a…`, `configHash
2430e660`, `physicsJobWorkers 0`). Read 2026-09-13 as 0097: the rim quarter 19 to 27%
(round 37: 45 to 98%), the crowd the box's at matched abundance, the cycle gone, the goal
rule five of five; seed 5 wedged at 29,200 s and is censored. M6 failed on 43 throws, all
two-part jointed newborns, and the control `r37bc-s5` (seed 5 with `fluidAccel 0`) threw
nothing, so the force is the world in which the throws happen; the field probe reads at
most 0.12 m/s² and the genomes say the throw is one hinge lineage driven at full power from
its first step (0097's addendum; `scratch/accel-probe/`, `scratch/throw-parents/`).
**The fresh seeds are read (logbook/0098, 0099; 2026-09-14 night).** Seeds 6 to 10 on the
same world and build, pre-registered, all ended on budget: F1 to F8 hold, so 37b's readings
are the world's. Ten seeds of ten pass D063 on this base (the reference bar from here), the
disc is mixed in ten of ten, the joint survives founding in three of ten and grows in none
(every jointed peak is before 3,000 s and decays), and the fresh five threw nothing over
3.79 million jointed body-seconds, so the 43 throws are lineage-bound (inference). The
stillbirth reading of 0097's addendum does not hold on seed 10 and stays open. The trace's
second pass is built on branch `trace2` (worktree `scratch/wt-trace2`, commit `7846cc3`:
the ring keeps only finite frames, records the first non-finite step, and carries the
step's drag, acceleration force and water acceleration per link; the smoke's forced case
uses a poison hook on the ring's read because PhysX takes no velocity on a link). It
compiled clean on worker 6 at 00:33 on 2026-09-14 (a compile-only pass, a sixth Editor
for three minutes over the cap, noted; worker 6 was then restored from main and checked
file for file), and waits for a worker under the cap to run its smoke and the box
digest. **Both landed 2026-09-15 00:25**: the candidate (main + `trace2` + `fieldcv`) passed the
shared-space smoke with the forced case, the 600 s box digest identical to main's over 31
steps, and the dilute tank smoke with 0100's header; main is fast-forwarded to `582ee6a`.
**Round 38 is running on it** (logbook/0100): seeds 1 and 2 launched 2026-09-15 00:25 on
workers 2 and 3 (`simHash ecc41ec5…`, `coreHash 16073c69…`, `configHash 30637fb0`,
headers verified), seeds 3 to 5 queued behind the fresh seeds' renders
(`scratch/logs/r38-queue.out`). Renders of the five seeds run on workers 2, 3, 5 and 7 (`scratch/r37b-chain.ps1`);
seed 5's render cannot reach a 30,000 s frame, and **the render queue did not take it**: a
stopped run's report carries no Ended footer, which was all the queue read, so the chain
sat two hours behind it on 2026-09-14 morning and was stopped (the queue now also reads
the manifest's status, so a stopped or errored run is taken); seed 5's frames at
5,000 and 15,000 s are taken by hand (`theatre-snap.ps1 r37b-s5 -At 5000,15000 -Worker 7`)
when a slot frees, after the fresh seeds' renders. Its first attempt was killed the same
morning for putting the machine one over the cap (the queue counted before its Editor was
up), and worker 7 was refreshed after the kill. The fresh seeds' renders run on
`scratch/r37b-fresh-chain.ps1` (seeds 7, 6 and 8 at the three times, 9 and 10 at 5,000 and
15,000 s; seeds 7 and 6 started 10:59 on workers 6 and 3). **A 30,000 s render does not
fit the queue's 720-minute wall on a loaded machine**: seed 7's did (its 30,000 s frames
are 0099's), seed 6's timed out at 22:59 with its 5,000 and 15,000 s frames only and is
not re-run (seed 7 and the positions plots cover the end state), and seed 8's, started
13:25, may go the same way. Seed 5's two frames started by hand on worker 7 at 23:10.
**The two merges wait for those frames**: both
branches move Core or `Assets/Evosim`, and an Editor started after the merge would refuse
the fresh seeds' recordings, since every worker compiles Core from the main tree.

**The theatre has an interface (logbook/0096, 2026-09-13 night).** Built by a subagent from
the owner's design (`design/SPEC.md`, committed with its `LICENSE-DOCS` lines at the owner's
word) and the two specs (`logbook/specs/theatre-ui-spec.md`, `theatre-ui-test-spec.md`), run
seven times in the Editor on worker 6 to all green (world 81 of 81, cousin 79 of 79, solo 26
of 26, three skips the fixtures cannot produce), merged into main; nothing under `Assets/Evosim` moved. UI Toolkit
under `unity/Assets/Theatre/UI/`: the identity strip, the census with both identities, the
warning plate, the transport bar and timeline, the provenance popover, the inspector in its
states, the solo census; `TheatreUiCheck.Run` is the end-to-end check and
`theatre-snap.ps1 -Chrome` photographs the chrome over a frame. Two rulings the runs forced,
both reversible in one block: the type and the rhythm take a 1.5 step at 3400 px, and a
cousin's lineage fields are withheld because its ids are unverifiable. Not verified: use by a
person, a player build, the chrome over the bright surface frame, the prose leading at 3840,
and why a custom property declared under `.is-wider` reaches nothing.

**Round 37 is read (logbook/0094).** Five of five on the population, four of five on the goal
rule, and a crust: the rim quarter held 45 to 98% of the bodies at every named time, the drift
was outward at every radius while births landed inward, so the gathering was the drag-only
body's centrifuge (D090; the owner named it first). The tank threw no body across 9.17
million jointed body-seconds where the box threw 131 across 6.91 million, so the wrap is read
as the throw's mechanism (inference), the mass-ratio cap is not proposed, and round 37b's
`diverged` is the test. The population cycle (a factor of three on 10,000 s) has no mechanism
yet. Seed 3 ended with 148 jointed bodies, the largest standing jointed population so far.

**The Astra review of 2026-09-12 is answered** (`gpt-astra-2026-09-12-1308-review-response.md`):
the transporter that made 30% patchiness from uniform water and the placer that tested a
centre and never a radius are both repaired in round 37b's build; the review's document
claims were verified row by row and the documentation pass (DESIGN through D090 and the
repairs, README, primers 05 and 06, the research counts) is done; the adoption-rule
inconsistency it found is ruled as D091 (replacements and treatments). Round 36 is read as
logbook/0092 (five of five; a standing jointed population of absorptive bodies in seed 1, on a
body plan the round did not predict).

**The goal is a ladder since D094 (owner, 2026-09-16; logbook/0103).** Rung 1, a self-sustaining food
chain, was D063's question and is met (rounds 30, 33, 35, 37b); its clauses stay in the
scorer as a state per seed (the chain holds, cycles, or no chain), never a verdict. Rung 2
is adaptation by degree, a standing reading until its threshold is measured on three
rounds; rungs 3 and 4 (a sense changes behaviour, a form changes earnings) are named and
not worded. Every round is read on its own pre-registered predictions, committed before
launch, held or failed one by one, and that is the whole of the discipline now. The
paragraph and the table below are kept as the record of the bar as it stood through
round 38; the goal-rule column is D063's count as recorded.

*As it stood until D094:* the goal has been met, and the base world keeps moving under it. D063, as amended
2026-09-04, asks for a clade that lasts: one connected absorptive clade alive for 20
consecutive samples to the end of a 30,000 s run, 10 or more members through the last two
lifetimes and still breeding, in at least 3 of 5 seeds, with the population floor closed, on
an inherited photosynthetic lineage. Since D091 a replacement of the world reads the rule
without requiring it, and a treatment is adopted only at the bar its pre-registration names.
The rounds of the shared world, each read against the one before it:

| round | entry | world | goal rule |
|---|---|---|---|
| 28 | 0070 | round 18's closed world in D077's box under D078's single thread; the base by D081 | 3 of 5 |
| 29 | 0072 | the senses on, added mass 0.5, the global brain retired; the price control | 2 of 5 |
| 30 | 0075 | the water as vertices, the bud priced | 5 of 5 |
| 31 | 0077 | detritus mixing at 0.02 | 4 of 5 |
| 32 | 0079 | the water as a grid of cells, corpses as particles | 2 of 5 |
| 33 | 0082 | bodies born small and growing; the first trait moved by degree | 5 of 5 |
| 35 | 0087 | dispersal 5 m, a current that carries, founders reserving the adult | 5 of 5 |
| 34 | 0090 | the joint made free: founds in every seed, gone from every seed by 23,100 s | 5 of 5 |
| 36 | 0092 | the link earns at 0.5; a jointed absorptive population stands in seed 1 | 5 of 5 |
| 37 | 0094 | the tank: a glass wall, a gyre, ring patches; the crust at the glass | 4 of 5 |
| 37b | 0095, 0097 | the streams, the fluid force, the conservative transporter; the base | 5 of 5 |
| 6 to 10 | 0098, 0099 | 37b's world on five fresh seeds; not a round | 5 of 5 |
| 38 | 0100 | the dilute tank: 400 m² with the matter held at 6,000 units | running |

Round 34 ran after round 35 (the owner's theatre pause moved the base round first); the
entries are in `logbook/README.md`'s key.

## The path, ruled

D079 (owner, 2026-09-06) set the method: one change at a time, asking D063 of each, a change
that costs the rule read rather than tuned around. D081 set the base and the two bars (*the
goal*, D063's 3 of 5; *the reference*, the base world's own count). D091 (owner, 2026-09-12)
split the changes in two: a *replacement* changes what the world is and becomes the base on
the owner's ruling, its round read for the mechanism and the goal rule not required; a
*treatment* changes one price, sense or rule on a fixed world and joins the base only at its
pre-registered bar. D094 (owner, 2026-09-16) deprecated D063 as the campaign's bar and set
the ladder above; a treatment's bar is written in its own terms. The owner's ten-round plan of 2026-09-11 ("plan the next 10 rounds and
change only if a result compels us"; "proceed autonomously") is the sequence below. The agent
may reorder it when a result compels (owner's grant, 2026-09-11), records each reorder here
with its date and reason, and never adds a world rule: those come to the owner as
`fable-propose-*.md`, absorbed into DECISIONS.md on ruling and then deleted. Rounds 28 to 37
are in the table above; the free joint's reading (round 34: the price was the founding
barrier, the unthrottled stroke removes the joint afterwards) is why the stroke's price moved
ahead of the idle charge.

1. **Round 37b, the water carried as water** (D090, D091; running, above). *Resequenced
   2026-09-12 afternoon by the agent, on the owner's diagnosis of round 37's gathering; amended
   the same evening after the Astra review to carry the transporter, the clearance and the
   corrected `cols`, none a world rule.* Read as a fresh baseline, not as round 37 repaired
   (D091): its changes are not attributed one by one unless a later question needs it.
2. **The one-part control and the fresh seeds on the base.** First `r37bc-s5` (0097's
   control: seed 5 on 37b's build with `fluidAccel 0`, everything else 37b's, so the force
   is the one difference from the streams alone; a replay of a scored condition, agent
   work), launched 2026-09-13 afternoon on worker 6 as the renders hold the other slots;
   read on `diverged` against 37 and the traces' anatomy. Then the fresh seeds (D091; owner
   2026-09-12, "proceed with your recommendations"): round 37b's world on seeds 6 to 10, no
   build, **pre-registered as logbook/0098 (F1 to F8) and queued 2026-09-13 evening**
   (`launch-queue.ps1 -Prereg` on workers 2, 3, 4, 5 and 7 as the renders free them, log
   `scratch/logs/r37b-fresh-queue.out`); read against 37b's five as a second draw of the
   same world, with F5 the joint's question and F6 the throws'. *Moved here from the old queue's item 22 because the five
   founding lotteries have guided nine rounds of adaptive change and round 37's standing
   jointed populations have to be shown to be the world's.* **Done 2026-09-14: both read (0097's addendum, 0099); F1 to F8 hold, the reference bar is ten of ten.**
3. **Round 38, the dilute tank** (D089 rulings 3 and 4): 400 m² with the matter held at 6,000
   units, corpses as objects at 0.005/s; **pre-registered as logbook/0100 (D1 to D8)** on
   2026-09-14 night, its hashes to be recorded at launch; `rounds/launch-r38.ps1` is written
   and launches on the build that carries the two merges. *Repaired 2026-09-13 morning:*
   it had been written from round 37's launcher before D090 and carried no
   `EVOSIM_FLUID_ACCEL`, so round 38 would have run the drag-only centrifuge; it now sets the
   force at 1 and its header comment says to verify `fluidAccel 1`. The dilute arithmetic passed
   (a 5 m matter cell holds 31 units at 0.235/m³; the mask overshoots the disc by 6%). Read:
   nearest neighbour, founding (`mat blk`, `mat short` against births), the downwelling as the
   first patch, sitter against mover. The build queued between the fresh seeds and this
   round (the queue's item 2, `field cv`) lands first, since it moves both hashes.
4. **Round 39, a bed with shape** (was 42): **ruled 2026-09-15 morning (D092;
   `logbook/specs/bed-spec.md`)**: a seeded height map at three scales, the streams'
   potential in floor-following coordinates so the water slows in the hollows, the grid
   masked below the floor, one static collider, relief 0 replaying the flat world; rocks
   and the shelf out. Builds on a branch after round 38's read, validated (constant field
   on the sloped grid, digest at relief 0, the current's divergence and floor flux, a
   settling test, the pace within 15%), pre-registered, launched as round 39. **The Core
   half is built** (2026-09-15 midday, branch `bed`, worktree `scratch/wt-bed`, `4feb648`,
   from `logbook/specs/bed-build/brief.md`; 27 tests, the suite 703 green): `BedShape`
   (three cosine bands at a -1.5 spectrum plus the tilt, mean-zero over the disc, fitted
   to the dial under the slope bound, hollows counted), the grid masked below the floor
   with the array reaching under the mean depth (so `LayerCount` and the refuge layers are
   no longer `depth/cell` in a tank with a bed; the Unity half must read them from the
   grid), and the streams' potential pulled back as a 1-form with the velocity carried by
   the Piola transform (the brief's velocity formula was inverted and the builder caught
   it; floor flux 3e-7 of the RMS, divergence 3e-4 per metre, the acceleration analytic
   to 0.06%, 1.3 times the flat field's cost). Two findings decide the dials: at 400 m² a
   red spectrum under a 30° slope bound gives about 1 m of relief at basins a third of
   the tank (the spec's "a few metres" needs a wider tank), and a tilt of any size spent
   the whole bound and left no hollows, so **the bound was split** (`4ce2ba0`, 2026-09-15
   afternoon; 28 bed tests, the suite 704 green): the bands at 30° on their own, the tilt
   refused above a 25° ramp, the sum allowed and reported as `SteepestTotalSlopeRadians`,
   and the hollows counted on the bands alone (a ramp makes no basin; counted on the whole
   map a 6 m tilt read 0 hollows against 3 on the same seed). The dial table at 400 m²,
   relief dial 12 m so the bound binds, five seeds averaged: scale 7.52 m gives a range of
   1.13 m and 1.6 hollows; 11.28 m, 1.75 m and 0.6; 15 m, 2.26 m and 0.6; 22.57 m, 3.09 m
   and 0.2; the range and the hollow count are the same at tilt 0, 2, 6 and 10 m at every
   scale, and the total slope reads 28.7° at tilt 0, 30 to 30° at 2 m, 34 to 36° at 6 m
   and 40 to 42° at 10 m. Round 39 then runs about 1 m of relief at the default scale
   (a third of the diameter) with one or two hollows and a tilt of about 6 m, a 15° ramp
   with the shallow arc 3 m above the mean depth, far below the lit band. Every config
   before the build is refused by the new `bed` group. **The Unity half is built and
   validated** (2026-09-15 afternoon, `56b2713` on `bed`, from
   `logbook/specs/bed-build/brief-unity.md`): a mesh collider from the height map at half
   a metre (8,712 triangles at 400 m²) over a backstop slab a metre under the lowest rock,
   the glass down to that rock, the placer's clamp read under each candidate after the
   draw (no RNG draw moved), a root more than its radius under the floor killed as a
   counted `Diverged` death whose dump names the bed, `EVOSIM_BED_RELIEF/_TILT/_SCALE`,
   the header's `bed` token carrying the dials and the map's facts on a shaped floor and
   unchanged on a flat one, seven `bed*` manifest fields, `refuge J` read from the grid's
   refuge cells (`GridField.RefugeStock`), two columns `floor low %` and `floor J` (a dash
   on a flat bed), the smoke's part 4b, and the theatre's drape, floor lines and camera box.
   Validation on worker 5 (`scratch/bed-chain.ps1`, log `scratch/logs/bed-chain.out`): the
   shared-space smoke passed with 4b (3 hollows, 1 ridge at a 4 m dial; 200 founders clear
   of the rock under their own columns; a body pushed two metres into the rock killed as
   the guard's death); the 600 s box digest at relief 0 identical to round 38's build over
   all 31 steps; the 600 s tank at relief 0 identical to `r38smoke` on every stats field at
   every sample (its bit-level reference on main's build, `tankdig-r38`, is still to run);
   the bed smoke `r39smoke` (600 s, dt 0.02, seed 3, relief 1 m, tilt 6 m) founded 172
   births against the flat smoke's 166, both books closed, no throws, header `bed relief 1 m
   tilt 6 m scale 7.52 m (hollows 3, ridges 1, range 1.00 m, steepest 36° bands 26°, bound
   clear)`, the hollows holding 1.7% rising to 16.1% of the floor's detritus over the 600 s;
   its pictures (`scratch/snaps/r39smoke/`) show the tilt from the side and a faithful
   replay, and a metre of relief is below what the world views can show (a low-angle floor
   view is a theatre item). The tank digest reference on main's build (`tankdig-r38`)
   is identical to the bed build at relief 0 over all 31 steps, so both of the spec's
   digest checks hold. **The pace** (spec item 8; `scratch/bed-pace*.ps1`, 6,000 s at
   dt 0.02, seed 3, the same worker and load): `tankpace-flat` 407 s of wall per 1,000 s
   simulated at a mean of 921 alive; `tankpace-bed` 482 (18% over) at 843; after one bed
   sample per part per step (`CurrentField.BedSample`, `f102104`) `tankpace-bed2` 455 (12%
   over), bit-identical to the first over 301 digest steps. A Core probe
   (`scratch/bed-build/unity/probe/`) split the rest: the grid's face fluxes paid the
   floor-following pullback at every fixed edge point every step (the per-step transport
   at 1 m cells 27 ms shaped against 9 flat, tilt alone the same as the full map), and
   the water's per-part sample reads 3.4 µs against 2.4. The columns are now precomputed
   once per edge point (`CurrentField.BedColumn`, `GridField` at construction, 105 kB at
   400 m²; `c94a91f`; bit-identical by `ThePrecomputedBedIsTheSameWaterToTheBit`), which
   takes the transport to 9.7 ms, and `tankpace-bed3` (evening, the same load) reads
   **433 s per 1,000 s, 6.4% over the flat run's 407**, bit-identical to the first bed run
   over 301 digest steps. The per-body figure (0.513 against 0.441, 16%) is confounded:
   the bed's realisation of seed 3 carried 8% fewer bodies (843 against 921 on average),
   which is the seed's butterfly and not the bed's cost. Spec item 8 is read as met on
   the whole run's pace. What remains per part is the map's twelve cosines and the
   Jacobian at each part, about 1 µs a part-step; sharing one sample across a body's
   parts would not help (most bodies are one part). `floorStockByFloorDecile` (ten
   shares of the floor's detritus by decile of floor height) joined `stats.jsonl` for
   round 39's E3 (`ef7c85a`), rechecked by smoke on worker 5 (`scratch/bed-recheck.ps1`).
   Merged into main on the evening of 2026-09-15 (`999ee8f`), workers 5 and 6 refreshed.
   **Then the owner saw the pictures and resized the tank (D093, `db15dba`)**: "Can barely
   see anything. And I think we need a much bigger tank. Much." Round 39 runs at 2,200 m²
   (radius 26.46 m), 45 m deep, a 30 m tilt (a 29.6° ramp, the shallow arc at 30 m, the
   lit band's floor, the deep arc at 60 m), relief 1.5 m at scale 17.6 m; the tilt's cap
   is 30°, `EVOSIM_DEPTH` is new, and the shelf is folded into the round. The matter
   budget comes from three 600 s founding smokes at 6,000, 9,000 and 12,000 units
   (`scratch/r39-big-chain.ps1`, worker 5; the water is 5.5 times round 38's, and a 5 m
   matter cell at 6,000 units holds about one child's cost), with the floor's pictures
   through the new `bed` view of `theatre-snap.ps1` (`352a4af`). Then round 39's prereg
   (`logbook/specs/r39-prereg-draft.md`, rewritten for the size and the light, baselines
   from 0101) after round 38's read, and the launch through `launch-queue.ps1 -Refresh
   -ExpectSimHash` from the smoke's manifest.
5. **Round 40, the light's reach: running** (D096, logbook/0106; the light sense that held
   this slot moves down the queue). Then **the shelf** in the lit band L1 defines: a floor
   raised into the top ten metres over part of the disc, the retry of 0102's E9 and E10,
   with births and free matter by side of the edge as the checks on 0105's deepward lean.
   Then the light sense: one new input, light and its vertical gradient; read on jointed
   against rigid against buoyant depth, in a world with something to steer toward.
   Proposal first.
6. **Round 41, the stroke priced alone** (was 39): the work cost back to D082's 0.25 with the
   idle charge still at 0.0001; no build. *First reordered 2026-09-11 morning after round 34's
   first three seeds: the stroke's price shapes what a joint does, the idle charge only what
   it costs to own. Now behind the dilution and the bed, so that there is a chase to pay for.*
   Before it is read on stroke quality, two things (owner 2026-09-12 night, "let's follow
   your recommendation"):
   - **The water that a fin can push on**: lift on a panel (a Kutta-style term in speed
     squared, angle of attack and area, in the drag's own loop) and the reactive force of a
     body bending through water (Lighthill's elongated-body theory, an unsteady added-mass
     term), each a tunable defaulting to 0 so every recording replays. Proposal first (the
     two terms, a validation against a known case such as a flapping plate, the cost per part
     per step), built on a branch, switched on for round 41. The rigid-body engine stays
     unless the throw trace says the throws are its and not ours.
   - **The champion in real water**: DESIGN §5.4's validation harness, one evolved swimmer in
     SPH or lattice-Boltzmann, to say whether a stroke evolution found is real or the
     approximation's. After the two terms have been read once; an instrument, not a round.
7. **Round 42, ellipsoids in the physics** (was 40): spheres and capsules collide and drag as
   their three half-extents; preceded by the offline read of whether boxes have flattened.
   Proposal first.
8. **Round 43, the anchoring cell**: holds a body to the bed or a rock against the current;
   pairs with the bed. Proposal first.
9. **Round 44, shading with a length scale** (was 41): self-shading by the neighbours above
   rather than the patch mean. The least urgent. Proposal first.
10. **Round 45, the remaining prices restored** (the idle charge to 0.02, the neuron and the
    connection to D082's values) on whichever world of 39 to 44 carries joints; no build.
11. **Round 46, the long arm**: one seed, 300,000 s, on the richest standing world, the stroke
    read against the water every 1,000 s; one worker for a week.
12. **Predation on contact** (`fable-propose-predation.md`, consolidated), the first thing a
    brain can be selected for; the owner ruled it too early on 2026-09-11 and it waits behind
    the ten.
13. **The open matter budget** (D074) and the vent, when a round shows the larder binds; then
    **the cell types and immigration**, the archive and the islands.

Standing rulings on the rounds. *Run length* (owner, 2026-09-08): 30,000 s gives 40 to 50
generations and three to four turnovers of the standing crop, enough to read whether a trait
the world contains is kept or lost; whether a wired sense is *used* goes to an inoculated
round (round 15's tool), not a longer one. *Mutation rates stay* (owner, same night): a child
already carries of the order of one structural change per birth, so novelty is not what is
short; the one targeted test allowed is the input-rewiring chance alone, with a control, if
inoculation shows the world keeps a wired sense but never finds one. *The limiter at every
step* was built as a tunable and failed its check (D089; 27 divergences against 17 with the
same signature), so no round runs it. *Every code change orphans every earlier run for the
theatre* (owner 2026-09-09, "a problem we should consider on its own", not to be answered
now): candidates when it is taken up are a tagged build per round kept beside the tree, the
theatre built against a run's recorded commit in a worktree, or a replay-only mode that loads
old formats read-only.

## Queued, in order

Agent work unless marked. Long steps (a suite, a smoke, a render) are launched by the agent in
the background and never handed to a subagent, which cannot wait.

0. **D098's build, ahead of everything below; D097's sequence follows it (the mouth on the new base).** (a) Round 40's
   read as pre-registered (0106, L1 to L9), the entry with its pictures, `birth-depth.py`
   moved into `logbook/specs/r40-read/`. (b) `fable-propose-economy.md` (2026-09-18 evening, for the
   owner's ruling; it superseded `fable-propose-matter.md` the same evening after the owner
   asked for the economy rethought): one substance, matter in two states, energy as the
   organic state's content at ρ joules a unit; photosynthesis makes organic from inorganic
   at a saturating uptake, living burns organic back to inorganic, eating moves organic,
   a child is organic matter given by its parent, detritus remineralises, the fixed charge
   goes, one breeding-margin gene (the owner's rule). Both audits kept. A Core build of
   three to five days, a 5,000 s screen, then a five-seed base round. (c) `fable-propose-mouth.md`: herbivory by
   contact, the rate, the two ledgers, what the harness reports (contacts per part per
   step), the Core tests, the smoke; then predation as its own proposal. (d) A prey sense
   and the stroke's price, on the fed world. The items below stand as written and move
   down.

1. **Round 38 landed and is read (0101); round 39 is launching (0102, committed
   `716e37a`).** Five seeds through the queue (`scratch/r39-queue.ps1`, log
   `scratch/logs/r39-queue.out`): workers 2, 5 and 6 first and 3, 4 and 7 as their round
   38 renders end, each refreshed, `-ExpectSimHash 353e0dff…` from the sizing smokes, the
   pre-registration named. Watch it (`scratch/arms-watch.sh r39-s1..s5`), look at a live
   arm at about 3,000 and 6,000 s with the bed view among the frames, and arm the render
   queue only after the last seed has launched. Each seed runs about fourteen hours at about
   1,900 bodies (0102's early look: round 38's per-body pace, not the smokes' three times
   it), so the round lands about 2026-09-17 with seed 5 a slot behind. **State at
   2026-09-16 14:18**: seed 1 ended at its 30,000 s budget; seeds 2, 3 and 4 run on workers
   5, 6 and 3; seed 5 launched on worker 2 on the same build, and the queue is done.
   **17:30: seed 2 landed at budget; seed 3 hit the launcher's 1,200 min wall at 25,175 s
   (0.3x real time at five arms, "ended wall", censored SHORT).** Seeds 4 and 5 run at
   1,400 and 1,640 s/h over their lives (local clock). **23:00: seed 4 ended on the wall
   at 27,517 s, censored.** Seed 5 alone on the machine ran 5,500 s in the four hours after
   the render Editor stopped (12,600 s at 23:04) and is on pace to land at budget near
   08:00 on the 17th, ahead of its 10:17 wall; the render chain stays paused until it does.
   **17th, 05:54: Windows restarted the machine for its update and killed seed 5 at 26,600 s**
   (the event log's two restarts at 05:54 and 05:55, then the owner's at 09:31; the Editor's
   log last written 05:53:16); its manifest is merged by hand the way `stop-arm.ps1` merges,
   `stopped` / `windows-update-restart`. **09:53: seed 5 relaunched on worker 2** through the
   queue with the prereg and the hash (`runs/r39-s5/2026-09-17-085303-…`, the killed run's
   directory kept beside it), on the owner's standing word that a run Windows takes is
   repeated; `launch-r39.ps1`'s wall is 1,800 min from this launch. At zero physics workers
   it replays the killed run's 26,600 s bit for bit (D078): check it with
   `scripts/compare-det.py --allow-partial` against the killed directory once the rerun is
   past a few thousand seconds, and again at the end. **09:58: seeds 3 and 4 relaunched on
   workers 3 and 4** the same way, on the owner's word ("proceed with your recommendations"),
   so every seed of the round reads at the pre-registered 30,000 s; the three reruns at
   three arms should land in about 17 h, the night of the 17th. The same replay check
   applies to each against its censored directory. `unity/Temp/UnityLockfile` is stale
   from the restart and the owner's Editor clears it on open. Three Opus agents are
   restyling the recent prose (entries 0097 to 0104, the recent specs, D090 to D094) on
   the owner's note of style drift; the caller reviews the diffs and commits. **Two rulings
   of the 17th** ("proceed with your recommendations"): three concurrent arms, not five,
   with renders between rounds (CLAUDE.md's cap; `scripts/pace-survey.py` is the evidence:
   round 39 read 25 to 35% costlier per body beside five arms and renders and cheaper than
   round 38 alone); and seeds per round, five for population-level predictions, ten when a
   prediction is about a rare event or a five-seed round reads 2 or 3 of 5 on the reading
   that matters, a rare event pre-registered as a count and never as a 4-of-5 threshold.
   The second goes to DECISIONS.md as D095 once the style agent is out of that file. A
   timing split for the run report (wall milliseconds per step in physics, the world, the
   harness and the writers, in `stats.jsonl` and the footer) is built and committed
   (`simHash 6d38c45e…`; the 300 s smoke `tsplit-smoke` read physics 4%, world 85%,
   harness 11% at founding, so the grid's per-cell step is most of an empty world's cost
   and about a tenth of a full seed's). Round 39's reruns are unaffected: their workers
   carry the round's tree. Every worker but 7 was refreshed for round 40 (7 carries round 39's tree for the renders). **The style pass is done and pushed** (0097 to 0101,
   0103, 0104, seven specs, D090 to D094; frozen pre-registration blocks and the entry
   skeleton untouched); 0102's restyle waits for round 39's read, since the entry is the
   live pre-registration. D095 records the seed and arm rulings. **The reruns replay their
   censored copies bit for bit so far**: `compare-det.py --run-a/--run-b --allow-partial`
   (new switches, fixtures pass) reads identical on every shared sample to 2,600, 2,200 and
   2,900 s at 10:40; `scratch/r39-after-reruns.ps1` (detached, log
   `scratch/logs/r39-after-reruns.out`) runs it again over the full prefix when the three
   land, then renders all five seeds one at a time on worker 7 with the round's
   `Assets/Evosim` copied from worker 3 over the refresh (the main tree's `simHash` moved
   with the timing split; a plain refresh would make the theatre refuse the recordings).
   **18th, 03:30 to 04:10: all three reruns landed at budget** (30,000 s; 1,043 to 1,087 min
   wall at three arms, 0.5x real time) and every one replayed its censored copy bit for bit:
   seed 3 identical on all 251 shared samples to 25,100 s, seed 4 on all 275 to 27,500 s,
   seed 5 on all 266 to 26,600 s. Round 39 is five seeds of five at the pre-registered
   second on one build. **The numeric read is done** (`logbook/specs/r39-read/`, summary.tsv
   first): E1, E5, E6, E7, E11 hold, E3 splits, E2, E8, E10 fail, E9 fails with the sign
   reversed (the shelf at 30 m is darker than the water the leaves float in; the tilt was
   sized for the geometry and never asked the light model), E4 holds and means nothing (the
   flat floor of round 38 reads the same). Goal rule 2 of 5, both fresh lines after the
   bust. **0105 is drafted and uncommitted** (`logbook/0105-the-shelf-was-never-in-the-
   light.md`) with seed 1's 5,000 s side view; it waits for the 15,000 and 30,000 s frames
   from the chain (rendering seed 1 on worker 7 since 04:10; `PICTURES-15000-30000` marks
   the place), then the README row and the commit. The placer lifts a child drawn under
   the rock onto it, so the deepward lean is not a placement leak; its cause is open. A running arm's
   wall cannot be extended. A rerun on the same build at zero physics workers replays the
   censored prefix bit for bit (D078) and continues it, so a rerun of seeds 3 and 4 is an
   extension and not a new realisation. Every prediction of 0102 reads at 30,000 s by name. The render chain was stopped at 17:30 to give
   them the machine; restart it after the round lands. The owner decides whether the read
   takes seeds 3 and 4 at their last sample or reruns them after the restart with a
   1,800 min wall (about 17 h each at two arms).
   Landings at the afternoon's pace: seed 2 about 15:45, seeds 3 and 4 about 21:00 to 23:00,
   seed 5 the morning of the 17th. **A Windows update restart is planned for the morning
   of the 17th after seed 5 lands** (owner, 14:40): stop the arms with `stop-arm.ps1` and
   pause the render chain first. If Windows restarts on its own before then, the killed
   seeds are censored (`running` manifests, no orderly end), the workers are refreshed and
   their `Temp/UnityLockfile` removed, and the lost seeds relaunch through
   `scratch/r39-queue.ps1`'s launcher against the same pre-registration and hash; the
   landed seeds keep their results. Two things the read left: **the glass leaks** (eight one-part
   bodies at the surface of seed 5 passed the wall and died on the radius guard, spinning
   at 9 to 48 rad/s; the dumps carry no contact history, so the reading is a contact log
   at the wall or a film of a leak), and **the long arm moves up** (an oscillation with a
   15,000 to 20,000 s period is read only by a run longer than it; path item 11). Two
   theatre items wait for a worker with a graphics device and move no hash: **the skin's
   rounding capped** so a near-cubic box stays a box, with a key that shows the raw
   collider shapes (the owner's observation of 2026-09-13), and **the Recorder's capture
   reproduced** (folded into the safari spec, item 2c). The bed view (`theatre-snap.ps1
   -Views bed`) frames the floor whole down the tilt under a raking light since
   2026-09-15 night; a tighter fit is queued.
2. **The video tools, pencilled in (owner, 2026-09-15 evening; `logbook/specs/video-tools-notes.md`,
   the three specs beside it).** The owner wants YouTube videos of the world, with a
   click to run a safari and a click to record; the tools are built during round 39's
   run, in the owner's Editor and in Core's tests, so they take no worker from the round.
   In order, each spec in front of the owner before its build starts:
   **The look is built (2026-09-16 afternoon, logbook/0104)**: the one-day and week passes
   in one day on the owner's ruling, everything below except the Recorder (with the safari)
   and the genome version of the skin genes (a proposal for a round boundary). Every picture
   from the build carries `look 2`. Round 39's post-run renders on it run from
   `scratch/r39-render-chain.ps1` (log `scratch/logs/r39-render-chain.out`): each landed
   seed at 5,000, 15,000 and 30,000 s, side, close, bed and iso, on worker 7 under the
   five-Editor cap and only after seed 5's queue has taken its slot. The pencilled dates
   below move up by two days: the timeline from the 17th.
   **Leonardo** (owner, 16th afternoon): by hand for now, the agent writes the prompt into
   `design/leonardo-prompts.md` and the owner pastes it; the 1Password `op run` pattern with
   a versioned `.env` is captured in `logbook/specs/leonardo-survey.md` for when the owner
   has time, three small repo changes listed there.
   **Re-pencilled 2026-09-16 morning after the look's design pass**
   (`logbook/specs/striking-theatre-menu.md`, the owner's rulings in it): first **the look**,
   because its pictures are what every later judgement is made from. 16th: the one-day pass
   (the post stack with tonemapping, bloom and vignette; the key light aimed at the rendering
   camera; supersampling; a floor light; the sky view promoted; the starving floor) and linear
   colour space with the re-tune, before-and-after pairs to the owner, the Leonardo prompt
   sheet under `design/`. 17th: the week pass's first half (a back light for portraits,
   ambient occlusion, depth-softened shafts, snow with depth, the rounding cap, size-dependent
   detail, depth of field, motion blur, the pace lock) and round 39's read. 18th: the second
   half (lit surface water, translucent leaves, the glass, the Recorder with its overlay bug,
   the absorptive tissue's own surface, the skin from the genome's features). Then the three
   tools: (a) the timeline 19th to 20th, (b) the checkpoints 21st to 23rd, (c) the safari
   24th to 28th. Round 40's pre-registration in between. The genome version of the skin genes
   is a proposal for a round boundary if the theatre version is not enough.
   The original pencilling, kept: (a) **the timeline** (`timeline-spec.md`, one to two days, 2026-09-16 to 17): a Record
   mode that draws any sample of `positions.jsonl` instantly with no physics, and a charts
   panel of `stats.jsonl` lanes with the lineage's events on the axis, clicking to seek;
   (b) **the checkpoints** (`checkpoint-spec.md`, two to three days, 2026-09-17 to 19): the
   whole state written every 1,000 s beside the snapshots (`EVOSIM_CHECKPOINT_EVERY`,
   a recording, no hash moves), restored in Core and the harness, the theatre seeking
   from the nearest one as a labelled cousin (the owner accepted the cousin);
   (c) **the safari** (`safari-spec.md`, four to five days, 2026-09-19 to 23): the guide
   script (clades, binomials, cards, a ranking), the director (the seven stations, a
   scene per clade, next, previous, a picker, auto mode, orbits with eases), and record
   through the Recorder with the overlay bug fixed on the way. The tank changes for round
   39 are D093 (path item 4), already built and launched ahead of these. Round 39 is
   watched and its pictures sampled through all of it; the read (0103) takes precedence
   on the day it lands. Open for the owner: which first story, "the world that stood"
   (the thirty-eight rounds) or "the boom and the bust" (round 38's eaters and round 39's
   floor).
3. **D094's two pieces of work** (owner, 2026-09-16): the scorer's verdict line becomes a
   state per seed (*the chain holds* / *cycles*, with the period / *no chain*), every number
   kept, PASS and FAIL gone, the 52 fixture assertions updated to the new line, and
   CLAUDE.md's scorer paragraph with it; and **the trait-by-degree instrument** (0082's
   reading from the snapshots, made on every round: per clade, a heritable number's
   distribution by generation, against a neutral number's drift), built with the timeline
   so rung 2 can be measured on rounds 39, 40 and 41 before its threshold is set.
4. **Landed 2026-09-15 00:25 with the trace's second pass**: `field cv` (`det cv`, `mat cv`
   in the table, `detritusCv` and `matterCv` in `stats.jsonl`; `logbook/specs/field-cv-spec.md`)
   and the trace that keeps the last finite frames and names the first non-finite step
   (`throw-trace-spec.md`'s second pass), validated together on one candidate tree (the
   smoke with the forced case, the 600 s box digest identical, the dilute tank smoke) and
   merged as `582ee6a`. Round 38 is the first round that reads both.
5. **The streams' analytic derivative is already in round 37b's build** (`dcc9a24`, inside
   the `streams` merge; `logbook/specs/streams-analytic-spec.md`): in a tank the force takes
   a closed-form time derivative and Jacobian at 2.3 velocity samples per call where the
   stencil cost 9.6, agreeing to 0.15% of the RMS. The nine-sample stencil survives only for
   the box's transport field, where no round has run the force. This item was carried in
   the old handoff as queued after it had landed; M8 reads the pace it actually costs.
6. **Pockets, then a bigger tank** (owner, 2026-09-12 night): the proposal follows round 37b's
   read, because it depends on how the streams move sinking matter (below).
7. **The fluid terms' proposal** (path item 6) and, after it has read once, the §5.4 harness.
8. **The throw's mitigation.** The trace is the per-link instrument; what it decides between
   (a mass-ratio cap at build and resize; the search names the 10:1 rule,
   `logbook/specs/throw-trace-research.txt`) goes to the owner only if a tank throws.
9. **0084's bin 3 screens** on any free worker: dispersed against undispersed on round 32's
   seeds, mixing 0.2 against 0.02, corpses off.
10. **Older items still open, in the order they were captured**: the movement assay (active
   against clamped on saved members of a jointed clade, repeated across orientations) and its
   ecological layer (a connected jointed clade persisting two lifetimes and paying positive net
   after work); round 18 as a committed reference (its config, hashes and a representative
   lineage, if the base still builds on it); a manifest-against-header contract test at the
   Unity boundary; D052's transactional guard test (force an `Admit` failure and close every
   book); two columns, gross photosynthesis per window and the matter drawn at conception per
   window; the scorer checking which round a report belongs to (`-ExpectSimHash` stands in).
11. **Theatre work that needs no round**: the skin seeded from the genome so relatives resemble
   each other; the offline reading of whether any lineage's boxes have flattened under light
   (from the snapshots; it precedes round 42); inherited skin genes (six to ten neutral numbers
   under one gate, a genome format bump, no per-step effect; owner's rule, a proposal); the
   safari (below). The theatre's sun and surface dials (`EVOSIM_THEATRE_WAVE`, `_WAVELENGTH`,
   `_WAVE_SPEED`, `_LIGHT_REACH`, `_SHAFTS`, logbook/0091) are unjudged by the owner; the
   agent's reading is that the surface reads as bands, which is the wave steepness.
12. **For the owner**: the `scratch/` cleanout list in the migration report (companion to
    `logbook/specs/scratch-migration-spec.md`, 2026-09-10; 70 MB of logs, review captures,
    probe output, one-off edit scripts, stale copies; nothing deleted). The worktrees under
    `scratch/wt-*` stay until the owner says otherwise.

**The safari** (owner, 2026-09-11: a theatre feature that finds the species, visits the
interesting ones, takes their pictures and explains each, "without requiring ad-hoc
intelligence"). The agent's view: procedural to the last sentence, because every fact a
field-guide entry wants is in the run. Clades from the scorer's parent walk; a clade's
founding time, the clade it split from and the mutation at the split; its share of the
living, its peak, its generation depth; where it lives from `positions.jsonl`; its body from
the genome; its life from lineage rows; its economics from the ledger against its ancestor's.
"Interesting" is a ranking over novelty, success, persistence, rarity and firsts; a name is a
deterministic binomial from the genome hash; the explanation is a template with the facts in
its slots, which says "the split changed X; the ledger reads Y" and stops. Three stages, none
a round: a census script that writes the guide; pictures through `theatre-snap.ps1` with a
view that frames a named body at a named second; the theatre's tour mode. **Specified on
2026-09-15 as `logbook/specs/safari-spec.md`** (queued item 2c), with the owner's stations,
the orbit family and the trip's navigation. An LLM, if ever, is
a polish pass over the template's prose, off by default, and the facts never come from it.

## Pockets, then a bigger tank (owner, 2026-09-12 night)

"If we find a way to properly make pockets of matter and energy in the world, then we could
really expand the tank properly. Think about it and if you get a good idea at some point,
write it down." The agent's first thoughts; a proposal follows round 37b's read.

1. **A pocket has to be made by physics, not painted.** An incompressible current cannot
   concentrate a dissolved field (the constant-field rule the transporter now keeps), so a
   pocket of dissolved matter needs a source, a sink or slow mixing. What a current *can*
   concentrate is anything that sinks: falling particles gather under downwelling and are
   swept from under upwelling, so corpses and marine snow in D090's overturning cells should
   already collect in moving bands. Round 37b's `det patch sd` and `patch max share`, and a
   picture of where the corpses are, say whether they do.
2. **Three sources the design already half-owns.** A bed with shape (round 39): hollows that
   sinking detritus settles into and cannot leave, ridges that shed it. A matter seep (D067's
   vent, off since its round): a point on the bed that leaks matter at a rate, a pocket whose
   size is the rate over the mixing. A shelf: a bed that rises to a few metres under the
   surface on one side of the tank, sunlit and settled on at once, which is what a reef is.
   Each is a physical rule with one dial, and a bigger tank is then dilute between pockets and
   rich in them.
3. **A reading first**: `field cv` (queue item 2), because no patchiness measure before
   2026-09-12 can be trusted.
4. **What not to do.** Slow the mixing further (the detritus already mixes at 0.02 m²/s, the
   prize has not appeared, and a matter grid stirred slower strands stock in cells too small
   to afford a child). Paint patches into the field: a source that nothing feeds is a rule the
   world cannot explain.

## The decisions in front of the owner

- **Cloud CPU: off (owner, 2026-09-18 morning: "cloud CPU right now is off. We'll continue
  working on my machine").** The survey stands in `logbook/specs/cloud-cpu-survey.md` for
  the day it is reopened; its finding was that the licence, not the price, is the
  decision. Nothing is to be built for it: no CPU field in the manifest, no script port.
  Round 40's ruling (6 m, "proceed with your recommendations") is D096 and is running.

- **A tempo dial, later (owner, 2026-09-17 afternoon).** The owner asked whether the world's
  metabolic rate could rise so a run holds more generations. Worked through in conversation:
  scale every ecological rate together (upkeep, income, growth, breeding age, senescence,
  corpse decay) and leave the physics alone, and a body's lifetime budget in joules, its
  depletion of its own cell per life and the joule cost of a metre swum are all unchanged,
  so the economics of moving against sitting are tempo-invariant to first order. What
  halves per life is everything that arrives by physics: sinking, the current's carriage,
  dispersal. A sitter's supply per life halves and a mover's reach still covers the tank,
  so on paper the faster world is slightly kinder to movement. The one choice is the
  muscle's price: idle upkeep scales, the joules per unit of mechanical work do not. The
  test is a control pair on one seed, tempo 2 at 30,000 s against tempo 1 at 60,000 s
  (the long arm already queued for the oscillation). The owner's ruling: not now; when the
  world has something worth speeding up. A proposal file then, not before round 39's read.

- **The theatre in person: done.** The owner tried the interface on 2026-09-13 morning
  ("not perfect yet, amazing progress"; good enough for now, the world comes first). On
  2026-09-13 at 23:47 the owner ruled "proceed with your recommendations" on the agent's
  plain-language brief. So the three rulings made under delegation stand: the 1.5 type step
  at 3400 px (build spec item 5), a cousin's lineage fields withheld, the pillow at 0.34
  (0091's addendum). The carve is 0.35 by the owner's eye; carve 0.5 was refused by the
  agent from pictures. Two things the owner raised that morning are open. A joint's moving
  link reads as a ball on screen: the skin's rounding on near-cubic boxes, and a cap on the
  rounding and a key for raw collider shapes are queued. And the Recorder's capture hides
  the interface from the Game View while it records, which is not intentional; it is to be
  reproduced on a worker with a graphics device and fixed in theatre code.
- **Things wrong in motion** (owner, 2026-09-11 evening: "there are some issues with the
  world that you can only see when rendering"), set aside and not yet named. The agent reads
  stills only; when they are named the route is a film of the session with frames pulled at
  the seconds in question.
- **Round 38's two rulings** are no longer open. D089's dilution to 400 m² with the matter
  held at 6,000 units, and corpses as objects at 0.005/s, were confirmed by the owner on
  2026-09-13 at 23:47 with the same ruling.
- **The producer threshold** is unsettled: D063's amendment asks for one living inherited
  member with a recent photosynthetic birth; the scorer prints that, the 10-through-two-
  lifetimes reading and the population-only column reading, and decides on none of them. The
  ruling picks one. With it, **the food chain's meaning under D063** and **the split between
  the lineage rule and a balance rule**, both noted under D063; **a late resource-balance
  rule** needs its own decision with a tolerance.
- **Predation on contact**, in `fable-propose-predation.md`: the injury pool with fixed
  geometry, dt 0.01 from the first screen, stable contact keys, an internal matter reserve.
- **Multithreaded physics** for labelled screens (D078 keeps the setting recorded; one
  sentence). **Extending a passing seed past 30,000 s. The maintenance assay** (a
  multi-genome inoculum with cell-type mutation off), which the owner should scope. **The
  width of the box** (raised by round 28's M5; the dilute tank is the first answer). **Whether
  the absence of CI is a choice.**
- **Speed, and the game's clock** (owner, 2026-09-03): a world eventful on a human timescale
  is a world-rule question. **Immigration as a world rule when the cell types expand**
  (owner's hypothesis, 2026-09-03).
- **The paywalled reading list** in `research/LITERATURE-REVIEW.md` needs the owner's
  institutional access.
- **The review files at the root.** Both Astra pairs (2026-09-07 and 2026-09-12, each with its
  response) are answered; every item they queued is done, ruled or in the queue above. They
  are the owner's to absorb or delete.

## How the experiments are run

CLAUDE.md holds the commands and the gotchas. This is where each tool sits.

| | |
|---|---|
| workers | arms run on `unity-w2..unity-w7`, one per worker, at most five Unity editors at once (the owner's on `unity/` counts), plus a sixth for a short visual check that comes and goes, never beside a test suite (owner, 2026-09-16); after a change under `unity/Assets`, `scripts/new-worker.ps1 -Workers N` once per worker and check the hash; a killed Editor leaves `Temp/UnityLockfile`, which reads as busy until removed |
| launching | a round goes through `scripts/launch-queue.ps1` with `-Prereg logbook/NNNN-….md` (refuses unless the entry is tracked and clean; writes `prereg.json`), `-Refresh` (each worker refreshed as it frees) and `-ExpectSimHash`; `scripts/run-arm.ps1` underneath it; logs in `scratch/logs/`; end an arm with `stop-arm.ps1` and never with a kill; read every setting back from the run header and the manifest |
| renders | `scripts/render-queue.ps1` beside the launch queue, frames at 5,000, 15,000 and 30,000 s into `scratch/snaps/<arm>/`; `scripts/theatre-snap.ps1` for one frame of a live arm (`-WallMinutes 150` on a loaded machine; `-Chrome` for the interface; `-Views close` for a portrait) |
| reading | `scripts/analyse-arm.ps1` by column name (`-ListColumns`), never positionally, `-Columns` as a real array from inside PowerShell; `python scripts/positions-read.py <arm> --summary` for where the bodies are; the per-round reads in `scripts/reads/` |
| scoring | `scripts/clade-score.ps1` for D063, `scripts/absorptive-log.ps1 <arm>` for what a stomach earned, `scripts/lineage-invasion.ps1` for an inoculated lineage, `scripts/ledger.ps1` (D069) before a worker |
| monitoring | one script per round under `scratch/` (`r37b-watch.sh` today): ending, error signature, and a stall on the report's byte size at 30 minutes read as a suspicion; `scripts/monitor-r13.sh` over a watch list is the older form |
| identity | `scripts/compare-det.py` (exit 1 on a difference, 2 missing, 3 unequal coverage) and `digest-diff.py` on a zero-worker pair; `scripts/theatre-check.ps1` for the replay |
| throughput | about 1,800 bodies at dt 0.01 with five arms sharing the machine is five to six hours per 30,000 s; the fluid force at 1 takes the streams' closed form in a tank (2.3 velocity samples per call; M8 of 0095 reads what it costs); the ceilings (`EVOSIM_MAX_POP`, `EVOSIM_MAX_TISSUE`) end a run as a censored runaway |
