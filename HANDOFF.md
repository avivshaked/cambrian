# Handoff: where to pick up

*Rewritten 2026-09-06 from the current state. What happened is in the logbook, and why it was
chosen is in [`DECISIONS.md`](DECISIONS.md). This file says only where things stand.*

## Where things stand

The whole record (logbook, primer, research) was restyled under STYLE.md and landed on
2026-09-07 after a pair-by-pair review; the git history holds every original.

**Round 33, the growth base, is running on workers 2 to 6; when it lands, work pauses for
the owner's theatre test (D087, logbook/0081).** **Round 30 is read (logbook/0075, 2026-09-08 evening): the vertex world at D082's prices
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
2 to 6 at 0.01, launched 2026-09-09 midday (`scratch/launch-r32.ps1`), pre-registered as
logbook/0079, read 2026-09-09 evening: the mechanism holds in every arm, the population is higher, and the goal rule holds in two seeds of five against round 31's four, the stomach lines thinning from founding without recruiting; the grid stands as the base and the eaters' recruitment is the open question (a per-guild feeding trace and a corpses-off seed are the two reads). Round 33 is growth, running since the evening of 2026-09-09 on workers 2 to 6 (`r33-s1..s5`, `simHash 0924b9ad…`, `coreHash 621d32ee…`, commit 0b8e822) (D087, logbook/0081; `scratch/launch-r33.ps1`):
the growth build landed on 2026-09-09 and the pre-growth world no longer exists in the code,
so the owner's conditional ruling of that morning (growth first if its build was close) puts
growth before the free joint. Round 34 is the free-joint test (logbook/0080, amended before
launch; `scratch/launch-r34.ps1`, round 33's launcher with the four prices at zero) on the
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
   (`scratch/launch-r28.ps1`). It meets the goal at 3 of 5 and misses round 18's reference
   by one seed, which starved on its larder (logbook/0070).
2. **Movement that pays** (D075's first item), next. On the base world with added mass on,
   the three senses on, and the global brain removed, all in one build that is a new
   realisation of every seed. The draft is `scratch/movement-prereg-draft.md`; it is
   rewritten for this world before launch, with the ledger setting the added-mass
   coefficient and the active-versus-clamped assay (queue item 7) beside it.
3. **The vertex world with the price of a bud** (D083 and D082, owner 2026-09-07): the
   water as vertices, so that blind movement pays by what it refreshes, and the neuron and
   its inputs about tenfold cheaper, in one build on the base round 29's reading leaves.
   Round 29 is the price control and round 28 the world control. Pre-registered as
   logbook/0075 on 2026-09-08 with the work fraction at 0.25 from round 28's first window;
   fast-step screens first (`scratch/launch-r30.ps1 -Dt 0.02`), the confirming round at 0.01.
4. **The water stirred less** (D085, owner 2026-09-08): round 31 runs the detritus mixing at
   0.02 m²/s on both axes, everything else round 30's, because at 0.2 a sitter's hole refills
   in five seconds and a mover gains nothing (logbook/0076); pre-registered as
   logbook/0077; the screen stood (five of five, three passing the scorer at 10,000 s) and
   the round ran at dt 0.01 on workers 2 to 6 (`scratch/launch-r31.ps1`) and is read
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
   `scratch/launch-r34.ps1` once round 33 has read and the owner's theatre pause has ended.
8. **Predation on contact** (`fable-propose-predation.md`, consolidated), the first thing a
   brain can be selected for, after growth.
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

## The decisions in front of the owner

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
| monitoring | `scratch/monitor-r13.sh` over `scratch/evosim-watch-arms.txt`; it exits when the list is empty and must be restarted after the list is set |
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
