# Handoff: where to pick up

*Rewritten 2026-09-30, and brought up to date the same afternoon after D137 and D138. What happened
is in the logbook, why it was chosen is in [`DECISIONS.md`](DECISIONS.md), and how to do things is in `CLAUDE.md` and the nested files
its table names. This file says only where things stand and what is queued; it is rewritten,
never appended to. The version before this rewrite, with how rounds 51 and 52 were built, the
card's speed work day by day and the run details of rounds 49 to 53, is
`logbook/specs/handoff-archive-2026-09-30.md`; the earlier archives of 2026-09-26 and 2026-09-22
sit beside it.*

## Where things stand

### The machine

**The owner's render is finished (2026-09-30 afternoon), and the hold is lifted.** In the afternoon
the machine ran round 54's build and fixture at four threads, its screen at eight and the nursery's
bench and pilots at four, within the half-machine cap.

- Speed first (the owner, 2026-09-25): 10,000 creatures fast is the committed target and 100,000
  a stretch. Option A: rounds run overnight on the CPU, one seed at a time, and the card is worked
  by day with nothing else on the machine. From round 51 an overnight round hands the snow's
  transport to the card (D126), once the card's check (queue item 5) has run.
- The total load stays at or under half the machine, 16 of 32 logical processors, read as
  `% Processor Utility`.
- The BIOS was flashed on 2026-09-29 (1836, microcode 0x133) and the NVIDIA driver updated
  (617.14), after a week of resets that were mostly the graphics driver failing on a live machine.
  The cap stays for about a week of clean running, to about 2026-10-06, and the card carries no
  heavy work until then. Any unexplained crash or clean re-run is reported the same hour.
- A session's hourly watch (a cron) dies with its session. A new session arms its own, after
  `scripts/sweep-orphans.ps1`.

### The rounds

| Round | Where it stands | What is owed |
|---|---|---|
| 49 | Read (logbook/0122); its video is delivered; its runs were deleted on 2026-09-29 | Nothing |
| 50 | Run and read (D125, pre-registration 0123; `logbook/specs/r50-read/read-end.txt`). W1, M4 and S3 fail; V1 waits for its film windows | V1's windows from the round's own checkpoints, the theatre's pictures, entry 0124 |
| 51 | Run and read (pre-registration 0125 on `round-51`, amended as `ed4a61d`; `logbook/specs/r51-read/final.txt`). 31 clauses hold; B1, W1, EK5, E3 and S3 fail; V1 held | The pictures and entry 0126; `round-51` and `round-51-tests` merged into main once the films of rounds 49 and 50 are done; a literature round for D128's sources |
| 52 | Run to 30,000 s (pre-registration 0127; seed 3 joined as `r52-s3j`). The read is `logbook/specs/r52-read/read-30000.txt` on `round-52` | The pictures, then entry 0128 |
| 52, extended | Seed 1 reached 50,000 s as `r52-s1z`, which ended on its budget on 2026-09-30. Seeds 2 and 3 never started | Seeds 2 and 3 when a lane is free, joined with `stitch-resume.py --header-from` |
| 53 | Run to 30,000 s on all three seeds, the last ending at 10:38 on 2026-09-30 (pre-registration 0129, `6701dc9`; branch `round-53` in `scratch/wt-r53`). The read is `logbook/specs/r53-read/read-30000.txt` on the branch (`340e21f`). N1 holds on every seed (bed snow 2.82, 2.49 and 3.08 times round 52's). N2 fails: the drop in the uptake-limited share was 0.016, -0.0006 and 0.034 against a bar of 0.015 on every seed. N3 holds on every seed (corpses eaten 256, 241 and 224 against 170, 114 and 129) | The pictures, the entry. The entry asks who the extra bodies are: the round carries more than round 52's crowd at 30,000 s (7,403, 7,291 and 7,607 alive against 3,431, 2,813 and 5,691), and a larger crowd of leaves on the same nutrients would explain N2 (a guess until counted). Round 53's reader dropped N3's per-seed rows from its summary; round 54's reader counts them |
| 54 | Being prepared on the branch `round-54` (`scratch/wt-r54`: `joint-drive` with `round-53` and main merged). D138 ruled it: D135's drive, the scent and the work cost at 0.5 on round 53's world, D137's friction off. `rounds/env-r54.ps1` and `queue-r54.ps1` are written; the build's fixtures are re-recorded by `logbook/specs/r54-build/build.ps1` (`r54fix-s4`, `pfix17`). A screen of seed 1 to 5,000 s is `r54sc-s1`. The reader is `scripts/reads/r54-read.py` and the pre-registration draft `logbook/0130-a-body-that-can-swim-reaches-its-next-meal.md`, both uncommitted | The build's suites, the screen's numbers into 0130, commit on a clean tree, then the queue |

What the rounds found, in a line each.What the rounds found, in a line each. Round 51: one corpse was eaten a seed in 30,000 s, so the
larder lies unused. B1 failed on the matter residual alone, the float door at the burn
(`logbook/specs/r51-read/b1-probe.txt`). Widening that door to double would close it, at the
price of a new realisation of every seed. Round 52: mouths landed on corpses and ate 170, 114 and
129 of them, but a meal bought little and no line rooted in a mouth lasted. Round 53 made the tank
25 m deep with the matter held (D131, D132's screens).

A round's farm build stays as long as its runs do, because `film.py` refuses a run whose
`programPath` is gone: `scratch/wt-leafincome` holds round 50's, and `wt-r51`, `wt-r52` and
`wt-r53` the rest.

### The nursery under D138

The nursery's code is `src/Evosim.Nursery` on `joint-drive` (`scratch/wt-joint`). D138 set its
settings: the grown purse, the work cost at 0.5, fresh founders, and Fable's R1 to R10 with the
controls. It runs beside round 54 and does not gate it. The plan and its record are
`logbook/specs/nursery-plan-joint-drive.md`, whose last section is the larder bench of 2026-09-30.
Snow laid on the bed's layer was out of the floating bodies' reach, and moving never paid. Laid
through the water column, the rate kinesis beat full power at the round's density (1.4 J/m3) and
beat frozen by 95 J at 12 J/m3. The config the new build reads is
`scratch/nursery/runs-jd/still-w2-f0` (D137's friction added at 0).

Two pilots, 200 generations each, run from `scratch/nursery/run-pilot.ps1`: `pilot-col1.4` (the
round's density) and `pilot-col12`, then a brain-only arm (R3). In the first, selection shrank the
adult (its scale gene from 1 to 0.61) and cut the work to near zero by generation 20; the search's
exam of its elites against the controls says how much of the gain is the brain's. The earlier
nursery (D133, D134, the branch `nursery`) measured nothing about foraging, and its papers wait on
review round 7's Pass 2 in a fresh session (`research/nursery-curriculum/round7-fetch-brief.md`).

### The water and the drive (D135 to D137)

D135's joint drive, genome format 11 and the nursery tank are on `joint-drive`, and so is the round
54 branch built from it. Drag stays physical (D136): the coast measured on 2026-09-30 is the drag
itself, and the owner will later fit a drag to each creature's shape and add surface friction then.
D137 built friction at the bed, the rocks, the reefs and the glass (`EVOSIM_BED_FRICTION`, off by
default), with the push along the normal that already held; a body swimming at the bed at full power
strikes 3.9 mm into it and presses 0.45 mm. Two test films of the drive are in `scratch/owner/`
(`jd-swim-own-v2.mp4`, `jd-swim-full-v2.mp4`, `jd-coast-v2.mp4`).

### Other work and the disk

Another session works on the story tools: `.claude/skills/story-resolve/`, `assets/`,
`logbook/specs/story-resolve.md` and the scripts named story-resolve are left alone. Round 48's
story film is delivered (`scratch/owner/r48-story-full/r48-story-full-v2.mp4`) with three flaws
the owner knows of.

The owner's cleanup of 2026-09-29 kept only what the videos from round 50 on need, and took the
project from about 360 GB to 65 GB. The runs that code and tests read stay: r42-s1, r42-s4,
r45fixb-s4, r37-s1, r25-s2, r25q-s2, th-ref, uicheck, r35tsmoke3, boxdig-old, r20v-age1, pfix14,
pfix15, and r48-s1's config with its 20,000 s snapshot. A round's checkpoints are thinned only
after the owner approves its video as final (`src/Evosim.Farm/CLAUDE.md`).

## The decisions in front of the owner

Each is put to the owner in full, with the options and what each implies, in any message that
asks for it.

- **Whether round 54 inoculates a nursery winner.** D138 ruled round 54 without one (option a). The
  agent recommended a fourth run beside the three seeds, seed 1 again with the nursery's best brain
  and its knockout inoculated, run only if the winner beats its controls (option b); or the
  inoculant in seed 3 (option c). A fourth run would queue after the three seeds and needs its own
  short pre-registration, so the question does not hold up the launch.
- **Left for later by the owner (2026-09-30):** the friction's value μ, friction between creatures,
  the producer threshold under D063, a tempo dial, the proposals at the repository's root (the own
  solver's rulings, the shelf reef and turbidity, predation on contact, `fable-propose-soup.md`),
  the old worktrees, the absorbed proposal files, and the lifespan literature's eight papers behind
  Europe PMC's Cloudflare page.

Ruled and in force for what comes next. Round 54 is D138's: D135, the scent and the work cost on
round 53's world. The power is read from [-1, 1] onto [0, 1], so a silent neuron is half power (D138).
The reactive thrust was approved by D127 and narrowed by `propose-foraging.md`, which D135 and D138 carry into round 54; both files are absorbed once round 54 is read. The lifespan values of round 52 hold through rounds 53 and 54 (D132). D131's places on the
bed come back one a round after that: hollows, then a seep that charges matter (put again in full
before it is built), then the anchoring cell (`logbook/specs/r53-larder-proposal.md`). Earlier:
links stop catching light from round 51 (`EVOSIM_LINK_PHOTO 0`); the
pool keeps its one-part stomach; round 52's lifespan values (the repair price 0.5, the chance 0.08,
the founders 0.5 to 1.5).

## Queued, in order

All of it is agent work. Long steps run in the background under the session, never in a subagent
and never in a shell loop; a queue that must outlive a turn is started detached.

1. **Round 54's launch**: the build's suites pass, the fixture edits and the reader are committed,
   the screen's numbers go into 0130, the style check passes, and 0130 is committed on a clean tree.
   Then `rounds/queue-r54.ps1` starts the seeds detached, at twelve threads beside the nursery's
   four.
1a. **The nursery's pilots and their exams**, read against the controls; the owner hears the result
   with the inoculation question set out again.
2. **Round 53's pictures and entry.** The read is done (`logbook/specs/r53-read/read-30000.txt` on `round-53`).
3. **The entries owed**, each with the theatre's pictures taken first: 0124 (round 50, after V1's
   film windows from the round's own checkpoints), 0126 (round 51), 0128 (round 52) and round
   53's.
4. **The card's transport check** against round 50's own record (D126's conditions), by day with
   nothing else on the machine and after the week's cap. Round 50's seeds are resumed on main's
   exe with `EVOSIM_GPU_TRANSPORT` from checkpoints near 5,000, 15,000 and 25,000 s, 1,000 s each,
   against the same windows on the CPU's transport; `logbook/specs/card-transport-check/check.ps1`
   runs them and `compare.py`, and was rehearsed on `r50smoke-s1`. Until it has run, rounds use
   the CPU's transport; the first round after it is queued with `scripts/farm-queue.ps1 -GpuTransport`.
5. **Round 52's extension**, seeds 2 and 3 to 50,000 s, when a lane is free.
6. **The card's speed work**, after the cap, largest first. The snow's settling,
   remineralisation and mixing (about 8 ms a step) could join the transport on the card. The
   engine uploads the snow again for the senses (3 to 6 ms a block) and could take the
   transport's result instead, if nothing between them changes the snow (unchecked). The class
   uploads could send the changed rows alone (3 to 4% of the wall, an estimate). Whether a round
   runs on the card in single precision is the owner's.
7. **The CPU's physics at a full crowd** (`logbook/specs/cpu-profile-2026-09-26.txt`): the
   water at the links and contact per part are merged; what is left is the contact pass, the
   fluid, the brain and the solve.
8. **The storage reductions that lose nothing**: a checkpoint that points at `genomes.jsonl.gz`
   (a new `Checkpoint.Version`, with 6 still read) and the feeding log gzipped, then a measured
   test of a better codec for the moving state.
9. **The theatre's four checkpoints** re-recorded on the current build: ckA, ckB, ckC and ckUi.
10. **The records owed**: the review round filing `logbook/specs/r52-lifespan-literature.md` (on `round-52`) in
    `research/LITERATURE-REVIEW.md`; the literature round for D128's sources; a nursery entry
    once the owner's ruling gives it a result, and a primer after it works.
11. **Round 49 seed 2's budded line**, read for what the bud costs against M4's 2.7%, from what
    `logbook/specs/r49-read/` holds, since the runs are gone; if that is not enough, it is
    dropped.

The proposal most likely to follow the swimming rules is the feeding change: a mouth fed from a
neighbourhood, or intake by the water passing the mouth. It goes to the owner as a proposal file
before anything is built.

## How the experiments are run

`CLAUDE.md` and its nested files hold the commands and the gotchas. This is where each tool sits.

| | |
|---|---|
| the farm | `scripts/run-farm.ps1 <arm> -Launcher rounds/env-rNN.ps1 -Seed N -Seconds S -Threads 16`, run from the tree the round belongs to, since the farm hashes the source above its working directory; `-ResumeFrom <run> -At <s>` continues a run from a checkpoint; stopped by `stop-arm.ps1`, which writes a `STOP` file |
| launching | `scripts/farm-queue.ps1 -Round rNN -Tree <tree> -Launcher rounds/env-rNN.ps1 -Seeds 1,2,3 [-GpuTransport]`, started detached, refusing a dirty tree; the pre-registration's record is the manifest's `gitCommit` on a clean tree. Under `-GpuTransport` it relaunches a seed the card refuses on the CPU's transport and resumes one the card ends into `<arm>c` from its last checkpoint; a read of such a seed joins the two at that checkpoint |
| reading | the round's reader `scripts/reads/rNN-read.py` (every clause, per seed, with a held line per clause); the entry's own reads under `scripts/reads/rNN-entry/`, their outputs under `logbook/specs/rNN-read/`; `scripts/analyse-arm.ps1` by column name, never positionally |
| checking | `Evosim.Farm.exe --verify-checkpoint <ckpt> <s> <out> <threads>` for a checkpoint member by member; `scripts/compare-det.py` for a resume against its run; a film window's identity rows for V1 |
| pictures | `scripts/theatre-snap.ps1 <arm> -From snapshot -At <s>` for a still from the record; `scripts/theatre-film.ps1` from a checkpoint for a clip; every render checked on a sheet against a reference before the owner sees it |
| profiling | `scripts/profile-farm.ps1 -Name <n> -ResumeFrom <arm> -At <s> [-Tree <tree>] [-AllowSourceMismatch]`: a resume under .NET's EventPipe sampler, read by method with `src/Evosim.Profile`; a share, not a pace |
| tests | `scripts/core-test.ps1` (the default set; `-All` before a change to the world), and `dotnet test` on `Evosim.Farm.Tests` and `Evosim.Dynamics.Tests`; the fixtures are `src/Evosim.Core.Tests/fixtures/r42-config.json` and the crowd named in `RunFixture.cs`, re-recorded on every build that adds a tunable |
| the card | ILGPU under `src/Evosim.Farm.Gpu`, worked by day with nothing else on the machine; kernels regenerated before a build; `EVOSIM_GPU_TRANSPORT` puts the snow's transport on it (`GpuTransport`, `GpuTransportTests`); `src/Evosim.UnityProxy` compiles the Farm package as the Editor would |
| the Unity farm | idle since round 42; its workers and caps are in `unity/CLAUDE.md`, for the theatre only |
