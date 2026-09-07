# Handoff: where to pick up

*Rewritten 2026-09-06 from the current state. What happened is in the logbook, and why it was
chosen is in [`DECISIONS.md`](DECISIONS.md). This file says only where things stand.*

## Where things stand

The whole record (logbook, primer, research) was restyled under STYLE.md and landed on
2026-09-07 after a pair-by-pair review; the git history holds every original.

**Round 28 is running (logbook/0070), read as arms land.** As of 2026-09-07 12:30: `r28-s1`
ended (FAIL on stability, as its round-18 counterpart did; every other prediction held),
`r28-s3` ended (PASS, clade 75 with a minimum of 51), the replay probe `r28p-s1` ended
(M7 holds: identical to `r28-s1` on all 10,001 digests to 1,000,000 steps at about 1,700
bodies), and `r28-s2`, `r28-s4`, `r28-s5` are running on workers 6, 3 and 2. Readings so far
are in `scratch/r28-results.md`. For each arm that ends: `python3 scratch/score-r28.py <arm>`
and `./scripts/clade-score.ps1 <arm>`, add its row to that file; when all five are in, write
0070's Results and Verdict under STYLE.md (M1 at the round's bar of 4 of 5; read M5, the
crowd prediction, across all five before drawing anything, since it fails as written in
20–25% of windows in both arms so far while the patches stay even), update D079's index row
and this file, push, and send one notification. The two-sided readings in 0070 say what
follows each outcome; the movement pre-registration is rewritten on whichever world holds.

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

D079 (owner, 2026-09-06: "agreed. proceed with that idea"): go back to round 18's world and
add one change at a time, asking D063 of each. A change that costs the rule is read rather
than tuned around. Round 27 therefore runs on as a screen rather than a scored condition.

1. **Shared space**, round 28, pre-registered as
   [logbook/0070](logbook/0070-the-good-world-with-one-change.md). Round 18's closed world
   plus D077's box, wrap, placement, restoring top and real floor (`EVOSIM_SHARED_SPACE` and
   `EVOSIM_SURFACE_RESTORE`), under D078's single-threaded physics. Five seeds at dt 0.01 for
   30,000 s on workers 2–6 as round 27's arms end, plus a 10,000-s replay probe, r28p-s1.
2. **The open matter budget** (D074), then the vent, one at a time, each on the world that
   held the rule before it.
3. **Movement that pays** (D075's first item), rewritten on whichever world holds. Its cost
   side closed long ago and its prize side has never existed; the channels it needs are
   built, and the decisions behind it are D040–D050.
4. **Predation on contact** in D076's world; the proposal is in front of the owner.
5. **The cell types and immigration**, then the archive and the islands.

## Queued, in order

1. **The D078 build** is being built on worker 7 against `scratch/physics-jobs-spec.md`. It
   lands, with a zero-worker digest pair as its check, before round 28 launches.
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
8. **A `mat here` column** in the run report: the matter density a body sees is in the
   statistics file and not in the table.

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
    at conception per window (`mat blk` counts refusals, not units). The flux counters are world
    totals; the patch dimension is only a standing-stock spread.
14. **The clade scorer checks nothing about which round a report belongs to**; the launch-side
    `-ExpectSimHash` guard stands in for it.
15. **The theatre has never been looked at by a person.** Its identity check proves it is the
    right world, not that the world is legible; one session in the Editor before it carries a
    reading.

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
- **Added mass has been off in every run** (`addedMassCoefficient: 0` in every config; CLAUDE.md's
  gotcha). DESIGN §5.4 wants it on by Milestone 3 and the movement round is the question it
  changes. Turning it on is a world rule and a new realisation of every seed.
- **The food chain's meaning under D063** (any absorptive body, or a lineage materially
  dependent on detritus) and **the split between the lineage rule and a balance rule**, both
  noted under D063.
- **Whether the absence of CI is a choice.** The suite runs by hand before a launch; nothing
  says whether that is the standing decision.

## How the experiments are run

CLAUDE.md holds the commands and the gotchas. This is where each tool sits.

| | |
|---|---|
| workers | arms run on `unity-w2..unity-w7`, one per worker, at most five at once; after a change under `unity/Assets`, run `scripts/new-worker.ps1 -Workers N` once per worker and check the hash |
| launching | `scripts/run-arm.ps1` with `-ExpectSimHash`, logs in `scratch/logs/`; end an arm with `stop-arm.ps1` and never with a kill; read every setting back from the run header and the manifest, never from the launch command |
| reading | `scripts/analyse-arm.ps1` by column name (`-ListColumns`), never positionally, passing `-Columns` as a real array from inside PowerShell, since through `pwsh -File` the comma list arrives as one string and every cell reads `?`; `mat blk`, `floor` and the `det in/out/exuded` columns are per-window deltas; `matterHere` is in `stats.jsonl` and not in the table; `lineage.jsonl` holds one row per birth and carries no photosynthetic flag yet, while the report's `photo` columns carry the producer population |
| scoring | `scripts/clade-score.ps1` for D063, `scripts/absorptive-log.ps1 <arm>` for what a stomach earned, `scripts/lineage-invasion.ps1` for an inoculated lineage, and `scripts/ledger.ps1` (D069) before a worker |
| monitoring | `scratch/monitor-r13.sh` over `scratch/evosim-watch-arms.txt`; it exits when the list is empty and must be restarted after the list is set |
| throughput | about 1,800 bodies at dt 0.01 with five arms sharing the machine is five to six hours per 30,000 s; single-threaded physics cost nothing measurable at 500 bodies (0069) and is unmeasured at a round's population (0070 budgets a quarter); the ceiling (`MaximumPopulation`, `EVOSIM_MAX_POP`) ends a run as a censored runaway |

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
- **The three untracked review files at the repo root** are the owner's to absorb or delete.
  The 2026-09-06 review was answered in `scratch/review-2026-09-06-response.md`, whose agreed
  items make up the queue above.
