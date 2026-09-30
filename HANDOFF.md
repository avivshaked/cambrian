# Handoff: where to pick up

*Rewritten 2026-09-30 from the current state. What happened is in the logbook, why it was chosen
is in [`DECISIONS.md`](DECISIONS.md), and how to do things is in `CLAUDE.md` and the nested files
its table names. This file says only where things stand and what is queued; it is rewritten,
never appended to. The version before this rewrite, with how rounds 51 and 52 were built, the
card's speed work day by day and the run details of rounds 49 to 53, is
`logbook/specs/handoff-archive-2026-09-30.md`; the earlier archives of 2026-09-26 and 2026-09-22
sit beside it.*

## Where things stand

### The machine

**Hold from about 10:15 on 2026-09-30: the owner needs the machine for a rendering job.** Round
53's third seed ended at 10:38 and no farm run is left. Nothing starts (no nursery, no extension,
no theatre render, no test suite) until the owner says the render is done.

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
| 53 | Run to 30,000 s on all three seeds, the last ending at 10:38 on 2026-09-30 (pre-registration 0129, `6701dc9`; branch `round-53` in `scratch/wt-r53`). The read is `logbook/specs/r53-read/read-30000.txt` on the branch (`340e21f`). N1 holds on every seed (bed snow 2.82, 2.49 and 3.08 times round 52's). N2 fails: the drop in the uptake-limited share was 0.016, -0.0006 and 0.034 against a bar of 0.015 on every seed. N3 holds on every seed (corpses eaten 256, 241 and 224 against 170, 114 and 129) | The pictures, the entry. The entry asks who the extra bodies are: the round carries more than round 52's crowd at 30,000 s (7,403, 7,291 and 7,607 alive against 3,431, 2,813 and 5,691), and a larger crowd of leaves on the same nutrients would explain N2 (a guess until counted). The reader's summary prints N3 as held in 0 of 0 though each seed's line reads held; check its tally before quoting the summary |

What the rounds found, in a line each. Round 51: one corpse was eaten a seed in 30,000 s, so the
larder lies unused. B1 failed on the matter residual alone, the float door at the burn
(`logbook/specs/r51-read/b1-probe.txt`). Widening that door to double would close it, at the
price of a new realisation of every seed. Round 52: mouths landed on corpses and ate 170, 114 and
129 of them, but a meal bought little and no line rooted in a mouth lasted. Round 53 made the tank
25 m deep with the matter held (D131, D132's screens).

A round's farm build stays as long as its runs do, because `film.py` refuses a run whose
`programPath` is gone: `scratch/wt-leafincome` holds round 50's, and `wt-r51`, `wt-r52` and
`wt-r53` the rest.

### The nursery (D133, D134)

`src/Evosim.Nursery` on the branch `nursery` (`scratch/wt-nursery`) evolves brains by direct
selection on bodies pooled from round 52's snapshots, scored in joules eaten (DESIGN §8.6). Runs 1
and 2 measured nothing about foraging, because they laid their corpses about the wrong origin
(fixed as `67e3961`). D134's fast nursery, design A, is built (`67e3961`, `81a07c4`). Its
measurements 1 to 3 ran from 03:46 to 04:18 on 2026-09-30 (`logbook/specs/fast-nursery-measurements.md`),
and the hand-wired positive control did not beat its knockout in any condition, so design A was
not run. What the nursery does next is the owner's, and they have deferred it until review round 7 is written; the options and the agent's recommendation (a run-and-tumble control first) close the measurements file.

The literature is `logbook/specs/nursery-curriculum-literature.md`: it supports the method in part
and not the claim, so a nursery brain counts as naturally selected only once it out-breeds its
ancestor in a round. Review round 7's candidate pool (Pass 1) is committed for the owner's trim
(`f876209`), and `research/nursery-curriculum/round7-fetch-brief.md` briefed a Sonnet session with a
browser, which the owner logged in, to fetch its papers. That fetch is done (2026-09-30): twenty of
twenty-one papers are filed as 175 to 192, 194 and 195 with packages, and `research/FETCH-RESULTS.md`
records each. Pass 2 of the review, Step 3 of the brief, has not started and waits for a fresh session.

### Round 54's swimming rules are on the bench

The branch `forage` (`scratch/wt-forage`) carries the four rules the owner approved for round 54:
the push along a limb, a stroke's cost on a knob, a corpse scent to about 10 m, and both together.
The bench notes are `logbook/specs/forage-bench/notes.md` on the branch (`3897a4d`). A hand-wired
brain finds a corpse. A torque drive has no centring, so a lone tail strokes off-centre, and a
mirrored pair of fins drives a body straight where one fin spins it in place. Nothing reaches a
round until the owner has seen the bench, and round 54 is theirs (below).

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

- **The nursery's next step**, now that the positive control failed. The reading and the options
  are in `logbook/specs/fast-nursery-measurements.md`.
- **The lifespan literature's eight papers** sit behind Europe PMC's Cloudflare page. The owner
  fetches them in a browser, or the review records them as not fetched.
- **Older items**, each unchanged since it was last put: the proposals at the repository's root
  (the own solver's rulings, the shelf reef and turbidity, predation on contact), the producer
  threshold under D063, a tempo dial, and the paywalled reading list. `fable-propose-soup.md`
  (2026-09-18, the soup first and the leaf invented) is also at the root and on no list; its
  status is to be asked. The old worktrees wait on
  the owner, who has the one-line command that removes them. The animal kit's and round 46's
  proposal files are absorbed (D106, D112 onward) and wait only to be deleted.

Ruled and in force for what comes next. Round 54 is the swimming rules alone on round 53's world
(D132). The reactive thrust was approved by D127, and `propose-foraging.md` narrows it to the four
rules above, which the owner approved on 2026-09-29; both files are absorbed when the bench is
read. The lifespan values of round 52 hold through rounds 53 and 54 (D132). D131's places on the
bed come back one a round after that: hollows, then a seep that charges matter (put again in full
before it is built), then the anchoring cell (`logbook/specs/r53-larder-proposal.md`). Earlier:
links stop catching light from round 51 (`EVOSIM_LINK_PHOTO 0`); the
pool keeps its one-part stomach; round 52's lifespan values (the repair price 0.5, the chance 0.08,
the founders 0.5 to 1.5).

## Queued, in order

All of it is agent work. Long steps run in the background under the session, never in a subagent
and never in a shell loop; a queue that must outlive a turn is started detached.

1. **During the owner's render**, only work that barely loads the machine: writing, and reading
   finished runs with the Python readers.
2. **Round 53's pictures and entry.** The read is done (`logbook/specs/r53-read/read-30000.txt` on `round-53`).
3. **The entries owed**, each with the theatre's pictures taken first: 0124 (round 50, after V1's
   film windows from the round's own checkpoints), 0126 (round 51), 0128 (round 52) and round
   53's.
4. **Round 54's bench**, items 5, 6 and 8 on `forage` (`logbook/specs/forage-bench/notes.md`).
   Items 5 and 6 are swim probes on real bodies at push 0, 0.1, 0.3 and 1. Item 8 is identity at
   1 and N threads, and the member check on a world with corpses and scent. Then the bench goes
   to the owner.
5. **The card's transport check** against round 50's own record (D126's conditions), by day with
   nothing else on the machine and after the week's cap. Round 50's seeds are resumed on main's
   exe with `EVOSIM_GPU_TRANSPORT` from checkpoints near 5,000, 15,000 and 25,000 s, 1,000 s each,
   against the same windows on the CPU's transport; `logbook/specs/card-transport-check/check.ps1`
   runs them and `compare.py`, and was rehearsed on `r50smoke-s1`. Until it has run, rounds use
   the CPU's transport; the first round after it is queued with `scripts/farm-queue.ps1 -GpuTransport`.
6. **Round 52's extension**, seeds 2 and 3 to 50,000 s, when a lane is free.
7. **The card's speed work**, after the cap, largest first. The snow's settling,
   remineralisation and mixing (about 8 ms a step) could join the transport on the card. The
   engine uploads the snow again for the senses (3 to 6 ms a block) and could take the
   transport's result instead, if nothing between them changes the snow (unchecked). The class
   uploads could send the changed rows alone (3 to 4% of the wall, an estimate). Whether a round
   runs on the card in single precision is the owner's.
8. **The CPU's physics at a full crowd** (`logbook/specs/cpu-profile-2026-09-26.txt`): the
   water at the links and contact per part are merged; what is left is the contact pass, the
   fluid, the brain and the solve.
9. **The storage reductions that lose nothing**: a checkpoint that points at `genomes.jsonl.gz`
   (a new `Checkpoint.Version`, with 6 still read) and the feeding log gzipped, then a measured
   test of a better codec for the moving state.
10. **The theatre's four checkpoints** re-recorded on the current build: ckA, ckB, ckC and ckUi.
11. **The records owed**: the review round filing `logbook/specs/r52-lifespan-literature.md` (on `round-52`) in
    `research/LITERATURE-REVIEW.md`; the literature round for D128's sources; a nursery entry
    once the owner's ruling gives it a result, and a primer after it works.
12. **Round 49 seed 2's budded line**, read for what the bud costs against M4's 2.7%, from what
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
