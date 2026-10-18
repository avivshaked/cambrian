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
- **Heat watch (the owner, 2026-10-01 evening):** while any heavy job runs, a session cron runs
  `scripts/temp-check.ps1 -Log` every ten minutes, and the work stops if the machine gets too hot, the
  CPU or the GPU; the other session on the card is asked to stop if the GPU is hot. Thresholds and the
  stop order are in `logbook/specs/machine-and-tools.md` ("Heat"). The CPU's temperature is read from
  MSI Afterburner's shared memory while the owner keeps it running; without it the CPU is judged by its
  own limiting.
  **It fired on 2026-10-01 at 19:17:** WARN at 19:07 and 19:17, the CPU at 90 °C, with the nursery's
  control arm (`pilot-deep-own16`, pid 94484) at 14 threads on all cores. The chain that would have
  started the free arm next (pid 95116) was stopped. The arm was not killed, because it has no resume
  and was at generation 62 of 100. It was pinned to the efficiency cores (logical 16 to 31) instead, and
  the CPU read 55 °C a minute later. That departs from the written order, which says to stop the job; the
  owner was told the same hour, and agreed. The free arm has not started: the owner put the drive
  sense first (D144; queue item 10a).
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
| 53 | Run to 30,000 s on all three seeds, the last ending at 10:38 on 2026-09-30 (pre-registration 0129, `6701dc9`; branch `round-53` in `scratch/wt-r53`). The read is `logbook/specs/r53-read/read-30000.txt` on the branch (`340e21f`). N1 holds on every seed (bed snow 2.82, 2.49 and 3.08 times round 52's). N2 fails on seed 2 alone (the drop in the uptake-limited share 0.016, -0.0006 and 0.034 against a bar of 0.015 in every seed). N3 holds on every seed (corpses eaten 256, 241 and 224 against 170, 114 and 129) | Read in entry 0132 (`a546006` on `round-54`, with two pictures in `logbook/images/`): one leaf line from the first 15 s held each tank, the crowd 2.2, 2.6 and 1.3 times round 52's, and N2's margin fell as the crowd grew (an inference). Nothing owed |
| 54 | **Ended 2026-10-01: all three seeds on budget at 30,000 s (s3 12:56, s2 13:37; 7,147, 6,010 and 6,190 alive). The read at 30,000 s is `logbook/specs/r54-read/read-30000.txt` and `.tsv` on `round-54` (`c3d8966`). The theatre's pictures from snapshots at 30,000 s (`scratch/snaps/r54b-s*/`): a layer of leaves over the whole tank, most within 12 m of the surface, a few mouths in the mid-water, no columns. N3 holds on every seed (corpses eaten 332, 647 and 459 against round 53's 256, 241 and 224; 1,438 against 721 pooled). J1 fails on every seed (jointed over rigid speed 0.968, 0.824 and 0.700, 1.05x, 1.03x and 1.07x round 53's against a bar of 1.2x). J2 fails (seed 1 margin -0.025; seeds 2 and 3 too few readers to count). K4 fails on every seed (a mouth founder's median life 0.98x, 0.84x and 0.54x round 53's). O4 holds on seed 1, the one seed asked (its line 0.53 jointed at 16,000 s, 0.24 at 30,000). Seeds 2 and 3's tanks went to lines founded by rigid leaves in the first seconds, and at the end no jointed body there had a jointed parent (jointed samples 1.0% and 0.2% of the rigid; seed 1 62%), and the work paid per body fell to 3% of round 53's on seed 1 and under 0.1% on seeds 2 and 3.** Relaunched 2026-10-01 ~01:45 local (00:44:40 UTC, the run directory's name) on the owner's word to launch it with the agent's recommendations**: `r54b-s1`, uninoculated, at five threads, from `scratch/wt-r54` at `3a7324e` (0130 revised: seed 1 as registered; seeds 2 and 3 to carry nursery gaits only after a confirmation in round-like water). configHash `4070da9c8eeca51e`, the first launch's; its rows must equal `r54-s1`'s through 5,510 s (`scratch/edit/r54b-identity.py`). The still-water gaits failed the confirmation in the current (+4 J over the best constant, se 17) and with the 25 m depth (−21 J, se 25), so they were re-evolved in that water (`scratch/nursery/pilot-deep-gait`). **They failed too** (03:02): overall +93 J over a steady half-power swim (se 48), but per body none beat its best constant by two standard errors and three lost to one; and they had gone blind (brainless −3 J, se 3). **No inoculant. D142 (the owner, morning of 2026-10-01): seeds 2 and 3 run uninoculated at eight threads each once seed 1 ends; inoculation moves to round 55 after the nursery's selection is fixed.** **Seed 1 ended 08:32 on its budget** (7,147 alive; B1 clean; identical to `r54-s1` through 5,510 s). Its read at 30,000 s is `scratch/r54b-read-s1.txt` (to be moved under `logbook/specs/r54-read/` with the round's read): J1 0.968, 1.05x round 53's 0.921 (bar 1.2, fails); J2 margin -0.011 (fails); K4 157.5 s, 0.98x (fails); N3 332 against 256 (holds); J3 work per jointed body 0.0082 W early, 0.0015 W late; J4 scent readers 0.186 at 5,000 s, 0.030 at 15,000, 0.027 at 30,000; adult scale median 1.03. **Seeds 2 and 3 launched 08:32 local** at eight threads each from `252818c` (0130's third note), clean, configHash `4070da9c8eeca51e`, the same source hashes; about 3x real time at 09:34 (s2 9,920 s, s3 11,720 s), ends expected 12:30 to 13:30; load 89% Processor Utility at 16 threads. Then: the read at 30,000 s of all three (`scripts/reads/r54-read.py` on `round-54`, default arms r54b), the round's entry, and the nursery's fix (D142: measure the noise per episode with a many-draw bench of one brain; more draws per brain; `--snow-fixed`; train in `scratch/nursery/runs-jd/big-cur-deep`; exam against frozen, constants and kinesis) once the machine is free. In the same water the hand-wired rate kinesis matched the blind gaits (−8 J, se 18) and beat the half-power swim by 85 J, so two arms of D141's scent track run from 03:10 at five threads each on the same six bodies (`pilot-deep-kin`, from the kinesis; `pilot-deep-own`, from their random brains), exams near 05:45. Earlier: **paused by the owner (D140) at 17:10 on 2026-09-30, and not started again that day**: it waits until the nursery names the creatures it will inoculate. Seed 1 stopped at 5,510 s with `stop-arm.ps1` (`runs/r54-s1/2026-09-30-154834-4070da9c`, checkpoints kept); the queue stopped before seeds 2 and 3; the hourly watch cancelled. 0130 says no inoculant and is revised before a relaunch. Before the pause: launched 16:48 on 2026-09-30 from `scratch/wt-r54` (branch `round-54`), seeds 1 to 3 one at a time at twelve threads (`rounds/queue-r54.ps1`, log `scratch/logs/r54-queue.log`); pre-registration 0130, `1d65ebe`, clean. D138's world with D139's fix: a silent joint has no power. The first screen (`r54sc-s1`, half power) lost two thirds of its jointed bodies; the second (`r54sc2-s1`) is seed 1's first 5,000 s. The reader is `scripts/reads/r54-read.py` | Entry 0131 is written (`b69b0ac` on `round-54`, with the index rows for 0127, 0129 and 0130); `round-54` is not yet merged into main. The nursery's fix (D142) is under way: the noise bench is in `logbook/specs/nursery-plan-joint-drive.md` (pairing cancels the snow's density, so `--snow-fixed` buys nothing; 2 draws carry a 105 J standard error); the deep-water arm at 16 draws a generation (`scratch/nursery/pilot-deep-own16`, pid 94484, 14 threads, launched 14:24 on 2026-10-01 after the owner said the other session works mainly on the card) runs about 4.9 min a generation (gen 7 at 38.6 min), so its 100 generations end near 22:30 on 2026-10-01, then its exam on 32 draws. **D143 (the owner, afternoon of 2026-10-01): the first joint is free, its stroke included.** A detached chain (`scratch/nursery/chain-free.ps1`, pid 95116, log `chain-free.log`, deadline 08:00 on 2026-10-02) starts the same arm with the first link wholly free and no work cost (`pilot-deep-free16`, config `runs-jd/big-cur-deep-free`, constants to 1) when pid 94484 exits; about 8 h at 14 threads. Read the two exams side by side: whether movement stays on, whether the scent is read, and whether a brain beats full power. Then the owner rules what comes back, one cost at a time |

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
controls. Round 54 ran uninoculated (D142), and the nursery now feeds round 55's inoculant. The plan and its record are
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

- **Whether round 54 inoculates a nursery winner.** Ruled by D140: round 54 waits for one. The owner, leaving the agent to work alone that evening, asked it not to launch the round after the nurseries, and to keep working on the nurseries until they give a good, meaningful result the owner is happy with. D141 answered the control: one seed stays uninoculated. The inoculants are the blind arm's gaits, whose search did not start hand-wired. D138 had ruled round 54 without one (option a). The
  agent recommended a fourth run beside the three seeds, seed 1 again with the nursery's best brain
  and its knockout inoculated, run only if the winner beats its controls (option b); or the
  inoculant in seed 3 (option c). A fourth run would queue after the three seeds and needs its own
  short pre-registration, so the question does not hold up the launch.
- **Left for later by the owner (2026-09-30):** the friction's value μ, friction between creatures,
  the producer threshold under D063, a tempo dial, the open proposals, now in `proposals/` (the own
  solver's rulings, the shelf reef and turbidity, predation on contact, the soup, and the animal
  kit, whose rung B is unruled; the owner kept predation and the soup live on 2026-10-01), the
  old worktrees (`fable-propose-round-46.md` and `unity-w234567/` were removed with the owner's yes on 2026-10-01), and the lifespan literature's eight papers behind
  Europe PMC's Cloudflare page.

Ruled and in force for what comes next. Round 54 is D138's: D135, the scent and the work cost on
round 53's world. The power is read from [-1, 1] onto [0, 1], so a silent neuron is half power (D138).
The reactive thrust was approved by D127 and narrowed by `propose-foraging.md`, which D135 and D138 carry into round 54; both proposal files were deleted as absorbed on 2026-10-01, with the owner's yes. The lifespan values of round 52 hold through rounds 53 and 54 (D132). D131's places on the
bed come back one a round after that: hollows, then a seep that charges matter (put again in full
before it is built), then the anchoring cell (`logbook/specs/r53-larder-proposal.md`). Earlier:
links stop catching light from round 51 (`EVOSIM_LINK_PHOTO 0`); the
pool keeps its one-part stomach; round 52's lifespan values (the repair price 0.5, the chance 0.08,
the founders 0.5 to 1.5).

## Queued, in order

All of it is agent work. Long steps run in the background under the session, never in a subagent
and never in a shell loop; a queue that must outlive a turn is started detached.

0. **Paraphrase the owner's quotes across the record, on 2026-10-04 in the evening** (the owner
   ruled on 2026-10-03 that the record paraphrases them from now on, and chose to clean up what
   exists too, on that evening, when tokens are to spare). The parts:
   - The files: HANDOFF, DECISIONS, the logbook, the specs, every `CLAUDE.md` and the skills'
     files. Turn each quote into what was decided and when. This is ordinary edits, a job for a
     subagent.
   - The story flow's saved owner files (`*-owner.md`): they hold the owner's words as the
     flow's verdicts, so put to the owner whether they move out of git or are paraphrased.
   - The commits not yet pushed (99 ahead of `origin/main` on 2026-10-03): ask the owner whether
     their messages and contents are reworded before the next push. Rewording them is a local
     history rewrite, so it is the owner's call.
   - The commits already pushed: these change only by a history rewrite and a force push, which
     is irreversible and the owner's alone. Put it to them; do nothing without their word.
   - Trim this file and every `CLAUDE.md` to the present (CLAUDE.md, "Evergreen files hold the
     present"). Move any history worth keeping to the logbook, and delete the rest.

1. **The nursery's pilots and their exams** (D140: round 54 waits on them), read against the controls.
   The per-body selection (`--per-body`, uncommitted on `joint-drive`, built to `artifacts/nursery-perbody`)
   ran on sparse islands at 300, 600 and 1,200 s (the owner's order). In the 10 m tank no search found a
   sense, and a sense handed to it earned no more than a tuned blind stroke. In a 40 m tank with 20 m
   islands, bodies planted in the food, the rate kinesis beats the best blind constant by 61 J (se 11):
   the first larder where the scent pays (nursery plan, last section). Two per-body arms evolve there from
   19:05 (`scratch/nursery/pilot-big-own`, `pilot-big-kin`). Read at 20:24: the blind arm evolved gaits
   that read the body's own senses (+394 J, 149 J over the kinesis), and the arm started from the kinesis
   kept the scent on four bodies but earned 48 J less (se 18). The owner asked for the machine free from
   20:24. **D141 (option C, to be done the next day): on 2026-10-01**, confirm the gaits (a re-exam of the blind
   arm's elites on fresh draws; a bench knocking out one channel at a time; an exam in the round's own
   water), then revise 0130 for an inoculated round 54 with one seed uninoculated as the control, and
   launch it only with the owner. The scent's search resumes after, starting from the gaits, for round 55. The launcher is
   `scratch/nursery/run-pilot.ps1`; the configs are `scratch/nursery/runs-jd/still-w2-f0-big*`. The owner
   hears each result, and the inoculant is chosen with them.
1b. **Round 56: light cycles** (the owner, 2026-10-04): day and night (D035's `DayNightAmplitude` and
   `DayLengthSeconds`, built and off in every farm round) and seasons (not built), on round 55's world, to
   test whether a light that varies over a lifetime loosens the leaves' hold. The proposal,
   `proposals/propose-r56-light-cycles.md`. The owner ruled on 2026-10-04: days of 1,000 s and seasons
   of 10,000 s, both at amplitude 0.5 (Q1 A); round 55's seeds in their roles (Q4); every seed a full
   30,000 s run; a staged launch, seed 2 first and read at 10,000 s (its first winter's trough is at
   7,500 s) before seeds 3 and 1 start; and no light sense in round 56. A light sense that no founder
   is wired to but mutation can add is moved to round 57 (Q3; the design is in the proposal's Q3 and in
   `CentralWiring.Effective`'s founder branch and `CentralMutation.AddLink`, which draw from the same
   pool). The pre-registration is `logbook/0135` (`552834e`, approved by the owner on 2026-10-04),
   merged into `round-56` at `1285504`, read by `scripts/reads/r56-read.py` (`--gate` for G); the
   launcher is `rounds/env-r56.ps1` on `round-56`. **r56-s2 runs** (launched 19:34, pid 274372,
   8 threads, `gitCommit` `1672983`, clean, configHash `a174d013f5aa74f3`, header `day ±0.5 over
   1000 s · season ±0.5 over 10000 s`), beside round 55's control. **G held** at 10,000 s (2,407
   living against round 55 seed 2's 2,031, 1.19 against the bar of 0.25), sent to the owner at 21:00;
   `logbook/specs/r56-build/s3-s1-chain.ps1` (pid 283632, log `scratch/logs/r56-s3-s1-chain.log`,
   `scratch/r56-build/HOLD` stops it) starts seed 3 and then seed 1 as farm slots free. It runs to 30,000 s without a stop: the checkpoint
   check on round 55 seed 2's last checkpoint failed (`logbook/specs/r56-build/verify-checkpoint.log`),
   the twin worlds agreeing for 100 physics steps and 2 metabolic steps but the restore leaving
   `Brain._portValue`, the scent, drive-sense and `BendSignal` caches at zero in 224 bodies. So no
   brain-v2 run is stopped and resumed until a build carries those members or proves them filled
   before read and lists them. Seeds 3 and 1 start after the owner has seen G: seed 3 when round
   55's control ends, seed 1 when seed 2 ends, 8 threads each. The owner left the slot priority to
   the agent on 2026-10-04: round 56 has first claim on both slots; a8's machine-slot work (3b's
   digest rerun, S1, S3/S4) may use the slot round 55's control frees only while seed 3 waits for the
   owner's look at G, a job of at most 8 threads and about 2 hours, announced first; otherwise it runs
   after round 56. The build is `round-56` at
   `1672983` (seasons `03bf4da`, fixtures pfix20 `52a1d4c87b6ea69c` and `r56fix-s4`, suites all
   passing, `logbook/specs/r56-build/fixtures.log`). The nursery's config (`7319d6b528cc77f1`) and its four recipes need
   migrating for the new tunables when the branch merges. Round 57 is left open; an open matter budget (D074's influx and burial, sources that move) was
   discussed as a later stage and not ruled.

1a. **Round 55's mixed inoculation: a proposal being prepared.** On 2026-10-04 the owner agreed in
   principle to inoculate round 55 with the best creatures of every brain-v2 nursery run (pilot,
   replicate, nodrive, nocross), each founding drawing one with some probability, and to let the world's
   selection say which wins, in place of choosing one nursery recipe. The proposal
   (`proposals/propose-r55-mixed-inoculation.md`) sets out what it needs: brain v2 running in the world,
   a weighted list of inoculants with each one's origin carried into the lineage, one seed uninoculated
   as the control, the win written down before launch, and the change it makes to D145's bar. The
   owner ruled the same day that the inoculants are what the four runs saved: each body's exam genome
   and the best genome of each generation. Every nursery run from now on also keeps its last
   generation's whole population, so a later mix can take ten per body. The owner also agreed that
   inoculated founders, like ordinary ones, keep arriving through the round rather than only at its
   start, so each nursery brain gets many chances in different crowds. The work is split, by the owner's
   word the same day, and starts at once:
   - **evolution-simulator-6e: the world side**, on branch `round-55` in `scratch/wt-r55`, taken from
     `nursery-general` (which holds round 54's world code, the drive sense and brain v2). Main merged in
     for farm-speed; the farm and physics fixtures re-recorded; pool founders without a central brain
     given one at arrival, as ordinary founders are (D146); a weighted pool pick with an origin table
     in the pinned pool; the pool's cap raised if needed; the brain model in the run header; Core
     `-All`; then a smoke run in the world that measures births, brain mutation and genome size.
   - **The inoculant set is in `inocula/r55-nursery/`** (a8, `2e3c396`; the cut script
     `scripts/reads/r55-cut-inoculants.py`): 341 genome files as the runs wrote them, `origins.tsv` (428
     rows: run, kind, body, born, score), and a README. No selection is made there; the round's
     weighted list is written from `origins.tsv`. a8's trial confirmed that `tank-population.jsonl` is
     written on `f6f01f6`'s Release, one row per member.
   - **The owner's rulings on the proposal, 2026-10-04:** half of all trickle founders come from the
     nursery pool; round 54's four stomachs stay at one in ten, given brain v2 on arrival; seed 1 is the
     uninoculated control and seeds 2 and 3 are inoculated. D145's bar is replaced (D149).
   - **The smoke** (`r55smoke-s2`, read in `logbook/specs/r55-build/smoke-read.txt`): every genome
     central, brains mutating at birth, about 4 MB a simulated hour on disk. Founders arriving after the
     floor closes almost never breed (3 of 44 ordinary, 3 of 56 pool; 32 of 45 floor founders did). So
     the owner ruled the same day that the floor's founders are also drawn from the pool, at the same
     share: a new tunable (`FoundingFloorPoolShare`, being built), then the fixtures re-recorded and the
     suites run again before the launch. The control seed sets it to 0.
1b. **A story film for rounds 50 to 54** (create-story-video): the facts are gathered and passed their source check on the second try (`scratch/story-video/rounds-50-54-202610010807/`: `facts.md`, `sources-check-1.md` FAIL, `sources-check-2.md` PASS). The angles writer's four angles (`angles.md`) were all declined; the owner's pick, saved word for word in `pick.md`: the owner liked none of the angles. They had hoped for a narrative around the changes to the world and the eventual tragedy of those changes not producing the desired outcome, since a lot of rules were changed after round 49. The story writer was stopped before writing anything, on the owner's word that they did not want the agent to write the story, and only wanted to see whether there was a good enough angle yet. The agent's reading, given to the owner: the angle holds (each round 50 to 54 fixed the last wall; round 54 gave bodies a swim and evolution turned it off), it waits for round 54's seeds 2 and 3, and `facts.md` predates seed 1's final read (gathered at 28,690 s) so its round 54 facts need a questions-file update. Nothing more of the flow runs until the owner says.
2. **Merge everything since round 53 into main, on 2026-10-05 from about 10:00**, when round 56 has
   freed the machine. Prepared the evening before in a separate worktree, so main's checkout is not
   touched until the suites pass. The worktree is `scratch/wt-merge`, branch `merge-main` off main.
   `round-56` is merged (`6dd8f41`; it holds `round-55` and `nursery-general`), and so is `round-54`
   (`433f160`; its four conflicts were the r42 and crowd fixtures, recorded once per build, and
   round-56's recording is kept; the logbook index gained rows for 0133 to 0135). Left:
   `nursery-room` (a8's yes first), then the nursery's config. `rounds/nursery/configs/7319d6b528cc77f1.json`
   predates `foundingFloorPoolShare` and the two season tunables, and those are the only keys it lacks.
   The migrated file is drafted as `scratch/merge-prep/nursery-config-migrated.json`: byte for byte the
   old one, CRLF kept, plus `seasonAmplitude` 0, `seasonLengthSeconds` 30000 (the default; the
   season is off at amplitude 0) and `foundingFloorPoolShare` 0. Its hash needs a nursery exe built on
   the merged Core: build it, point `brain2.json` at the draft, and `--recipe rounds/nursery/brain2.json
   --check` names the hash it computes. The file goes in under that name, with its `configHash` field set
   to it, and the four `rounds/nursery/brain2*.json` recipes and the `Evosim.Nursery.Tests` pins
   (`RecipeTests.cs` lines 71, 102 and 172, `RegistryTests.cs` line 32) are pointed at it. Line 71's
   episode hash `87d66e98fed542c2` moves too. The recipes' own hashes change with them, so read how
   `--rerun` checks a registered entry's recipe hash before committing, and ask a8 if it is unclear.
   The old file stays. Then the suites, Core `-All`, Dynamics, Farm, Nursery and the room, about 45 minutes at
   8 threads. Then main fast-forwards to the tested merge and main's Release farm is rebuilt, so
   `ledger.ps1` reads round 55 and 56 configs. The machine's order that morning: the merge's suites,
   then the speed-up agent's quiet slot (11a), then a8's room walk.
3. **The entries owed**, each with the theatre's pictures taken first: 0124 (round 50, after V1's
   film windows from the round's own checkpoints), 0126 (round 51) and 0128 (round 52).
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
   fluid, the brain and the solve. At the owner's suggestion (2026-10-02 evening), the Fable
   subagent that read the snow's advection was asked the same question of the farm's physics,
   read-only: where the water's sampling at the links goes (25% of round 49's profile) and what
   would speed it up. Its answer is `logbook/specs/farm-physics-speed-read.md`. First, with no
   ruling, three bit-identical changes in Dynamics (estimated, not measured, 10 to 18% of the
   physics at a full crowd): fold the commit into the next step's grid build (P1), sample the
   water inside each body's step instead of in a pass of its own (P2), and skip the bed's sample
   in `ContactBed.Push` when a link is provably clear of the highest floor (P3). They are built
   after the snow kernel lands, one subagent at a time, and checked the way the profile's §4
   was: a resume of a round against its rows, at 1 and 16 threads. Before any change to the
   water's own arithmetic, `scripts/profile-farm.ps1` is re-run on a free machine with the bed's
   `HeightGradientAndHessian` read as a frame of its own. By the read's estimate it is about 40%
   of a far link's water sample, and reducing it would be a world rule (N2, a cheaper floor).
   For the owner later, each in full: N1 (the velocity read off the acceleration's gradient, a
   new realisation, about 5%), N2, R2 (the acceleration sampled less often, a world rule) and
   the standing N3 (the solver on the card in single precision, about five times).
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
10a. **Paused, 2026-10-01 23:00: both chains stopped (101656, 92252), nothing launched.** The owner said they
    were not sure there was any point in another round or nursery until this was solved: the work was trying to
    get creatures to evolve in a way they simply cannot, since a joint reflex can make a creature jump when it is
    bitten but is not meant to guide it to food. What "this" is: the control's best brain at generation
    90 is one neuron with no inputs; mutation balances near 1.25 neurons a part (add 0.05 a part, remove 0.04
    a neuron); no mutation adds an input; neurons cost nothing, so the neural cell's discount buys nothing.
    **Brain v2 is the goal now (D146, ruled 2026-10-01 night).** The owner's goal: build the
    proposal, review it, perhaps with another subagent collaborating on the review and fixes, then build the
    proposal into the world and into the nursery, review the build, run the relevant tests, fix, and repeat until
    the agent is happy. Then run a few generations (a smoke) in the nursery, bake the breeding proposal into the
    nursery as well, and run a nursery run with the new brains and breeding. State at 00:30 on 2026-10-02:
    - **Built** in `scratch/wt-joint` on top of `8d7daba`: Core (one brain, ids, wiring, mutation, format
      12, the World's founder and birth hooks), the farm's `EVOSIM_BRAIN_MODEL` and the card's refusal,
      the nursery (fresh brain per member, central children, knockouts, the crossover re-based on ids).
      What was built, the review and which numbers are guesses: `logbook/specs/brain-v2-build.md`.
    - **Reviewed twice** by an Opus subagent: one blocker (a sense linked twice) and five should-fixes, all
      fixed; the second pass closed them and found three nits. `CentralBrainTests` 21, nursery 34.
    - **D147** (the owner, 00:15): founders wired all to all, LeCun-scaled weights, biases at 0.
    - **The smoke** (`scratch/nursery/smoke-brain2`, 24 genomes, 3 generations, 2 threads, the build before
      D147): every path ran; its 3-generation brains scored 262 J against constants of 330 to 394 J and
      the kinesis 401 to 438 J, as random starts would. d_ref on its exam pool: median 0.785
      (`scratch/nursery/dref-brain2`).
    - **The run**: `scratch/nursery/launch-brain2.ps1 -DRef 0.785 -Threads 14 -EfficiencyCores`, out
      `scratch/nursery/pilot-deep-brain2`, on the efficiency cores. Config
      `scratch/nursery/runs-jd/big-cur-deep-brain2` (the drive-sense config with the `brain` group,
      `brainModel` Central, `founderBiasStdDev` 0). Built from `74f2cc8`.
      - **First launch stalled** (00:26, pid 110736, nursery-v9; its directory is now
        `pilot-deep-brain2-stalled`). At 00:46 no generation 0 had been written. One thread had run
        1,163 s straight at full load, and the main thread was waiting at the join. Every other tank
        of generation 0 had finished. Stopped at 00:50.
      - **Relaunched at 00:51, pid 113496**, on nursery-v10. That is v9 plus a report: a tank still
        swimming after a minute of wall time logs `slow tank` to `nursery.log` once a minute, with its
        simulated time, living bodies, links and each body's state. `Say` is now locked.
      - **Generation 0 finished in 462 s with no slow tank**, on the same seed and the same genomes.
        So the stall did not repeat, and it was not a slow episode. A race is the likeliest cause, but
        reading the new code found no shared state that gets written (`Effective` copies; `Inoculate`
        clones only an empty brain). The cause is open.
      - **If it stalls again:** `slow tank` lines that keep printing mean a slow episode, and they say
        where it is. A stall with no line means the thread is stuck inside a single step, a code hang;
        stop it and dump the stack.
      - About 7.7 minutes a generation, against the control's 4.6 on the same threads and cores.
        That puts the end near 14:00 on 2026-10-02, then the exam. The last job of each generation
        runs alone for about two minutes, and the % Processor Utility dips to about 20% are those
        tails, not stalls.
      - **Read at 06:50 (generations 0 to 45).** Alive, no `slow tank` line all night, no orphans.
        Brain v2 is ahead of the control at the same generation in all 46 generations.

        | Mean net energy | brain v2 | control |
        |---|---|---|
        | Over generations 0 to 45 | 401 J | 341 J |
        | Over the last 10 | 425 J | 387 J |

        By generation:

        | Gen | brain v2 mean | brain v2 best | control mean | control best | hidden, mean and max | links |
        |---|---|---|---|---|---|---|
        | 0 | 194 | 512 | 122 | 361 | 0, 0 | 82.0 |
        | 10 | 503 | 808 | 450 | 709 | 0.76, 3 | 82.7 |
        | 20 | 398 | 639 | 357 | 560 | 1.40, 4 | 83.2 |
        | 30 | 477 | 726 | 427 | 667 | 2.16, 5 | 84.4 |
        | 45 | 290 | 567 | 260 | 553 | 3.76, 7 | 87.1 |

        **Brain v2 differs from the control in three things, not one.** It has the one brain with
        96 drawn founders, the drive sense (`senseDrive` true; the control's `big-cur-deep` config
        has it off), and the crossover breeding. So the lead belongs to the package, not to the brain
        alone. The single-change arms queued below separate them.

        What I take from it, as inference:
        - The lead was there at generation 0: 96 drawn brains start better than the control's six
          copied ones. The gains since generation 0 are about the same in both runs.
        - Both runs rise and fall at the same generations, because each generation's water comes
          from the same seed in both. So a single generation's row reads the water as much as the
          brains.
        - Hidden units grow slowly (mean 3.8 by generation 40), and links rose from 82 to 87.

        Whether any brain uses its senses, rather than having found a better steady stroke, is the
        exam's question. The control's brains failed it.
      - **How the lead is made (09:50, generations 0 to 69).** It is not speed. Brain v2's path is
        about 70 m an episode against the control's about 77 m, so it covers less ground. Its
        swimming work is about half the control's. And the food density at its bodies is 5 to 15%
        higher in every decade. So the lead is economy and position.

        Richer water could be steering by scent, or it could be a slower body staying in the patch it
        landed in. The exam's Scent knockout separates the two.

        The owner, 2026-10-02 morning, on adding displacement and top speed to the report, declined:
        the owner thought the agent had checked the right thing.
      - **The search ended at 13:46 on 2026-10-02, all 100 generations, with no `slow tank` line.**
        Brain v2 was ahead of the control in every generation. Decade means:

        | Gens | Net, brain v2 | Net, control | Lead | Path m, v2 / control | Work W, v2 / control | Food at body J/m³, v2 / control |
        |---|---|---|---|---|---|---|
        | 0–9 | 373 | 240 | +133 | 64.7 / 61.6 | 0.040 / 0.106 | 1.22 / 1.07 |
        | 10–19 | 423 | 386 | +37 | 69.2 / 73.9 | 0.046 / 0.074 | 1.27 / 1.20 |
        | 30–39 | 461 | 416 | +45 | 70.0 / 74.0 | 0.055 / 0.055 | 1.34 / 1.24 |
        | 50–59 | 471 | 432 | +39 | 68.8 / 76.5 | 0.046 / 0.088 | 1.35 / 1.28 |
        | 70–79 | 503 | 468 | +35 | 71.6 / 79.1 | 0.048 / 0.098 | 1.39 / 1.32 |
        | 90–99 | 434 | 406 | +28 | 69.9 / 80.7 | 0.046 / 0.112 | 1.28 / 1.22 |

        - The lead after the first decade stays between 28 and 51 J, so the gains since generation 0
          match the control's.
        - The control swam further and harder as it evolved. Brain v2 held its path near 70 m and its
          work near 0.05 W, and its food at the body stayed 5 to 10% higher.
        - Hidden units rose from 0 to a mean of 7.4 (max 14), and links from 82 to 96.
        - Wall time was 775 min for 100 generations, 7.75 min a generation. The episodes' own time
          (`seconds`) averaged 6.6 min against the control's 4.3, about 1.5 times. That gap is the
          cores, not the brain (measured below).
      - **The exam ended at 14:17** (13:54 to 14:17; each log line pools all six genomes). The table
        and the read are in `logbook/specs/nursery-plan-joint-drive.md`, "Brain v2's exam", made by
        `scripts/reads/nursery-exam-per-body.py`. In short:
        - Bodies 0 and 10 beat their best constant by 2.4 and 2.2 standard errors. The control
          managed none.
        - Body 0's brain reads the flow, its drive and the reserve, and body 10's reads depth.
        - The Scent knockout cost nothing on any body, and the pooled food at the body did not move
          (1.30 J/m³ both).
        - Against the control's brain on the same body, only body 10 is better by two standard
          errors, and body 8 is worse by 3.2. Body 8's best brain scores below every constant.
      - **The next arms, ruled by the owner on 2026-10-02 at about 15:10.** The order put to them:
        the sensing cache, then a replicate of the baseline on a new search seed, then the baseline
        without the drive sense, then without the crossover. Their answers:
        - On the cache: go ahead, but make sure it does save time in runs, and consider clean
          architecture so that it creates no odd couplings.
        - On the replicate: that makes sense.
        - On the separation arms: the owner remarked that it was long and asked whether it would still
          be 13 after the optimisation work of 1.
        - On round 55's inoculant: the owner would rather not start a full round yet, because they also
          want the nursery v2 finished and tested before a round starts.

        So no round, and no world inoculant, until nursery v2 is finished and tested. The replicate
        is nursery v2's first registered run.
        The two arms are drafted, unregistered, in `scratch/nursery/arms/` (2026-10-02 evening).
        a8 checked both with `--recipe … --check` at nursery-room 8489b8d: both load and differ from
        brain2 as intended. nocross was then changed from the pool to brain2's founders, and the
        session re-checked both on `56c0f6f`'s build (recipe hashes `f7a83c9d…` nodrive and
        `907fdf7c…` nocross; nocross's episode config is brain2's own, `87d66e98fed542c2`).
        - `brain2-nodrive`: `SenseDrive` false. Its bodies come from rep2's pool, with seed 0x1.
        - `brain2-nocross`: `search.crossover` null, with brain2's own founders. brain2 reproduces
          the pilot exactly, so its generation 0 is the pilot's.

        a8's reading of the pairing:
        - A search episode's water is a pure function of seed, salt, generation and draw. So both
          arms swim the pilot's water in every generation, and their exams pair with the pilot's and
          rep2's.
        - With `SenseDrive` off, fresh brains are drawn from a sensor pool without Stroke, Hold and
          Ramp. So nodrive's brains differ from the first port on.
        - A caveat for later reads: an evolved genome that already has drive links keeps reading
          them under `SenseDrive` off (`CentralWiring.AllPorts`). So a bench of a drive genome with
          the flag off still senses drive. Only the exam's knockout removes it.

        They go to the owner for a go, with the replicate's spread and the measured speed-up, before
        they move to `rounds/nursery/`.
      - **The joint-drive line stays off main** (the owner, 2026-10-02 at about 16:00: proceed with
        the recommendation). The line is `joint-drive` (72 commits past main) and the nursery
        branches on it. It merges into main when the first round on it is being pre-registered, with
        Core `-All`, the Farm and Dynamics suites, round 52's known failures fixed or re-recorded, and
        a check that the theatre still replays. Until then every nursery run launches from the
        `nursery-general` worktree, and no farm queue runs. The farm-queue exclude for
        `rounds/nursery/appended.txt` goes in with that merge.
      - **Measured 2026-10-02 at 15:40: brain v2 is not slower. The cores were different.** The
        control ran on all cores until its generation 62. It was then pinned to the efficiency cores
        (the heat entry at the top of this file). Its pace was 4.6 min a generation before the pin and 7.64 after it.
        Brain v2 ran pinned from the start, at 7.7 to 7.8. My earlier "the same threads and cores"
        was wrong. A one-thread timing on one efficiency core confirms it, on generation 0 at population
        16 and 2 draws. It took 77.1 s under brain v2's setup, and 77.3 s under the old brain with no
        drive sense. The measurement build was `dc5dd6f` with Stopwatch counters around `Senses.Sample` and
        `Brain.Step` in `DynamicsWorld.StepOne`, never committed. Its counters split the process's
        73 s of CPU:

        | Setup | Senses | Brain | Rest of the body's step | Outside the body's step |
        |---|---|---|---|---|
        | Brain v2 | 0.71 s | 0.83 s | 5.46 s | about 66 s |
        | Old brain | 0.22 s | 0.46 s | 5.63 s | about 66 s |

        The body's own step is under a tenth of the time. The rest is the world's step between
        bodies, the episodes' setup and the search. So no fix to the senses or the brain can pay,
        and the owner's item 1 is closed with no change. The suspects below were wrong.
      - **Where an episode's time goes** (the same one-thread read, 15:55, brain v2's setup; the old
        brain's within 2 s of it). The episode loop takes 76.8 s, and its setup 0.2 s.

        | Part | Seconds | Share |
        |---|---|---|
        | Core's `World.Step`, every metabolic step | 59.6 | 78% |
        | Physics: the bodies' own steps | 7.6 | 10% |
        | Physics: the water sampled at each body | 6.7 | 9% |
        | Physics: the contact grid, commit and after-step | 2.7 | 3% |
        | The harness | 0.15 | 0% |

        So the world's step between physics steps is what a speed-up has to reach. Inside it
        (Core's own probe, a `-p:WorldPhaseProbe=1` build, one thread on a performance core,
        16:10, summed by `scripts/reads/world-probe-sum.py`), carrying the snow with the current
        (`GridField.AdvectFromThePotential`) is 88% of the world's step, 6.4 ms of 7.3 ms a
        metabolic step. Mixing the snow is 5%, and nothing else reaches 2%.
      - **The speed-up, after Fable's read** (`logbook/specs/snow-advection-speed-read.md`, saved as it
        came, 2026-10-02 about 16:25). At 16:10 the owner was told the clock's cosine per edge was
        left to remove, and was offered the angle-addition identity as a change of rounding. That was
        wrong: the identity has been in `CurrentField.Eddies` since D105 (line 3135, checked), and
        an edge's sample calls no sine or cosine. The time goes to overhead: about 115,000 edge
        samples a step at about 55 ns each, through guard calls, bounds-checked loads, a `BedMap`
        struct and per-edge recomputation of each column's 24 phase terms. Fable's plan is a flat
        CPU kernel over the tables the card's transport plan already builds, with each column's
        phase terms computed once. All of it is bit-identical, so it is agent work under the
        owner's go-ahead (make sure it saves time; clean architecture, no weird couplings).
        An Opus subagent built it in `scratch/wt-advect-fast`: branch `advect-fast`, commit
        `d58d7af` (16:43) on `56c0f6f`, Core only. `SampleByColumn` (default on) walks each lattice a
        column at a time, computing each column's 24 phase terms once, and on a flat floor the x
        and z edges skip the eddy sum they never read. #6 was left out because the layer split
        would need a barrier, and #4's table because it costs 128 MB on round 50's farm grid. The
        card's plan is unchanged. `ColumnKernelTests` holds every edge and face bit for bit against
        the old path on four floors, and the stock over 300 steps at 2 threads against 1; 146 guard
        tests pass. The session read the diff against `StreamsPotentialFrom`, `MapFrom`,
        `FadedPotentialOf` and the plane loops' indexing and found the same grouping and casts.
        Still to do, after the replicate and with nothing else running: the `-All` suite, then
        the timing. The timing is a trial recipe under `scratch/` (a8: a recipe outside
        `rounds/nursery/` is a trial and enters no registry line), based on `brain2.json`, with
        2 generations, an exam of 1 episode on its own brain, at `--threads 1`, on `56c0f6f`'s
        build and `d58d7af`'s, alternated. The results must match exactly. Before it starts, ask
        the other projects' sessions on the machine to pause their CPU jobs until it ends
        (expressive-tts-28 was told so on 2026-10-02 evening). a8 is suspended by the owner until
        Sunday evening, 2026-10-04, so its slot is skipped: timing, then `-All`, then the merge.
        Its digest check and S1 wait for its rebase of `nursery-room` (671ee5f, with uncommitted
        edits in its worktree that are left alone).
        **Resumed 2026-10-03 at about 12:00.** The owner told the agent to proceed with the work, and said that if token
        usage rose too high they might halt it, in which case work would continue the next
        night after the usage reset. expressive-tts-28 stays paused; a8 stays suspended.
        - **The timing, 12:02 to 12:15:** `time-builds.ps1`, nothing else running, one thread on
          one efficiency core, alternated. The old build (`56c0f6f`) took 246.1 and 247.9 s, the
          column kernel (`d58d7af`) 141.5 and 139.1 s. That is 1.76 times faster, inside the
          estimate of 1.6 to 1.8 below. All four runs wrote byte-identical
          `tank-generations.jsonl`, `tank-exam.jsonl` and `tank-best.jsonl`. Outputs are under
          `scratch/advect-fast/` in each worktree. The one-core tail was not measured separately.
        - **Core `-All` on `scratch/wt-advect-fast`**, 12:16 to 12:49, pinned to the efficiency
          cores: 1,102 passed and 1 failed, `ParallelIdentityTests.Round42SteppedFourHundredTimes…`.
          Its round-42 config lacks `bedFriction`, which the joint-drive line added, and the same
          test fails the same way on `56c0f6f`. So the kernel adds no failure.
        - **Merged:** `nursery-general` fast-forwarded to `d58d7af` at 12:52, and its Release
          nursery rebuilt. Every nursery run from here is on the column kernel.
        - **Fable's P1 to P3** (`logbook/specs/farm-physics-speed-read.md`, all bit-identical) are
          being built by an Opus subagent in `scratch/wt-farm-speed` (branch `farm-speed` off main
          at `61e3bb1`), with code and filtered tests only. Their check (a round resumed against its
          own rows, digests at 1 and 16 threads, the pace alternated) needs the machine to itself,
          so it waits for a gap between nursery arms.
        - **The two separation arms: the owner's go**, 2026-10-03 at about 13:15, on the four options
          put to them (both in turn, nodrive only, a third replicate first, or hold): the owner told the agent to proceed with
          its recommendation, which was both in turn, nodrive first, after the farm check. Each
          takes about 8 hours on 14 threads (an estimate: the replicate's 13.6 h over 1.76). The read
          rule is fixed in `logbook/specs/nursery-plan-joint-drive.md`, "The separation arms,
          stated before they start". While they run, a first look at the stuck bodies (the
          pilot's body 8, the replicate's body 0) compares the brain's running mean in the search
          with its exam, using no CPU.
        - **`brain2-nodrive` is read** (the nursery spec, "brain2-nodrive: the read"): pooled own
          brain 505.7 J, inside the margin by 1.3 J, so no effect seen at one seed; one body over the
          bar (10); none stuck. The pooled gain over the pilot is the pilot's stuck body 8; the other
          five differ by +7 ± 14 J. Out `runs/nursery/brain2-nodrive` in `scratch/wt-nursery-general`.
        - **`brain2-nocross` is read** (the nursery spec, "brain2-nocross: the read"): pooled own
          brain 463.6 J, 6 ± 15 J above the pilot and well inside the margin, so no effect seen at one
          seed; one body over the bar (10); none stuck. Leaving out the pilot's stuck body 8, the other
          five are 36 ± 15 J below the pilot, inside what the seed alone moved. Out
          `runs/nursery/brain2-nocross` in `scratch/wt-nursery-general`.
        - **Round 55 runs** (item 1a; pre-registration `logbook/0134`, `dcd21cc`): `r55-s2` and `r55-s3`
          ran from 12:54 on 2026-10-04 and ended on budget at about 18:15 and 18:30, 8 threads each, from
          `scratch/wt-r55` at `ef93817` (clean), configHash `d573544fd7837861`. The control `r55-s1`
          (pid 268560) started at 18:15 and ends near 22:45; then the full read, saved under
          `logbook/specs/r55-read/`. The readings taken before it, with the table of who starves, are
          `logbook/specs/r55-read/interim-2026-10-04.md`. The readers are `scripts/reads/r55-read.py` and round 54's
          `scripts/reads/r54-read.py` on `round-54`.
          At 5,000 s the floor's pool share drew pool genomes at about 60% of attempts, but 88 of 93 in
          seed 2 were refused: D116 lands a founder only where its own food is, and the nursery's
          bodies eat matter, which at the start sits only in the islands. The 5 and 6 that landed at
          the floor had no child by 5,000 s; trickle-time nursery founders bred (6 and 5). The
          nursery-rooted share of the living was 1.4% and 0.5%. The owner ruled the same day to let
          the round run as it is; the write-up says that the floor's share barely took, and why.
        - **`nursery-general` is at `f6f01f6`**: the search writes `tank-population.jsonl`, its last
          generation whole, and the registry's rerun check skips that file for older entries. Its
          129 tests pass. The Release nursery in that tree predates it, so rebuild it before the next run.
        - **`farm-speed` is merged into main** (`675ef5f`): Core `-All` passed on it, 1023 of 1023.
          Main's Release farm build predates the merge, so rebuild it before the next farm run.
        - **A heat watchdog runs beside each arm.** The owner approved it on 2026-10-03 and ruled that
          it stays out of git, as a matter of the machine rather than of the code.
          `scratch/heat-watchdog/watchdog.py`, gitignored. Every 60 s it reads the last minute of MSI Afterburner's one-second log
          (`D:\Monitoring\HardwareMonitoring.hml`, which ml-store-f5 rotates daily; read only, opened
          so Afterburner can still write). It stops the arm by id when 10 s of the minute are hot
          (CPU 95 °C, GPU 85 °C or the GPU's temp limit), or after five warm minutes in a row (CPU
          90 °C, GPU 80 °C). It then writes `STOPPED-<arm>.txt`, which keeps the cron from launching
          the next arm. Its log is `scratch/heat-watchdog/<arm>.log`.
        - **P1 to P3 checked identical and 8.5% faster in the physics** (13:16 to 13:26). Every
          column but the wall times agreed in all 10 rows between main's build and farm-speed's, between
          the two runs of each, and between 4 and 16 threads over 20 s. Both builds' lineage and
          25,100 s checkpoint equal round 50's own. Round 50's rows themselves differ from both builds
          in two counters only (`harnessBodySteps`, `fluidLinkSteps`), the same on both. The physics
          took 39.4 s a 10 s row on main's build and 36.0 s on farm-speed's (alternated c, g, g, c),
          and the whole row 71.4 s against 68.0 s, 4.7%: under Fable's estimate of 10 to 18%. The
          card check's `compare.py` printed DIFF on the rows only because two runs sat under each
          arm's name. `farm-speed` (`69515c5`) merges into main after Core `-All` passes on it.
        - **The farm check of P1 to P3** (`scratch/farm-speed/check.ps1`, from 13:16): round 50 seed 2
          resumed from 25,000 s for 100 s at 16 threads on the efficiency cores, main's exe and
          farm-speed's alternated (c, g, g, c), then farm-speed's at 4 threads for 20 s. The card
          check's `compare.py` reads each against round 50's own record. The diff was read by the
          session (`69515c5`): the grid build runs every CPU step and commits every body, dead ones
          included, before any read, so no commit falls inside the parallel phase. Heat watch
          686409bd covers the check and the arms.
        The hold, as it stood (2026-10-03 about 00:10). The owner, on pausing the agents, asked to pause expressive tts as
        well, with the reason explained to it, and agreed with the other recommendations. So:
        - the replicate runs to its end and its exam, and its results wait on disk;
        - the heat watch runs every 30 minutes instead of 10 until the replicate ends, then is
          deleted;
        - no timing, `-All`, merge or read until resume. On resume the timing is
          `scratch/advect-fast/time-builds.ps1`. It is checked with `--check` on both builds. It
          copies `brain2-time.json` into each tree, because the nursery refuses a recipe outside
          the tree it runs from, and writes each run's output inside that tree. It writes
          `scratch/machine/timing-read.json` while it runs, the declaration agreed with a8, which
          the room honours. One more thing to measure there: the replicate drops to one core for
          about a minute at the end of every generation while its last episode finishes (seen at
          17:15 and at every low heat-watch reading). That tail may be a second saving, beside
          the kernel;
        - expressive-tts-28 was asked to pause its work. Then the merge into
        `nursery-general`. a8 leaves the two Core files alone until then, and its digest check
        runs before or after the timing, never during it.
        Estimated, not measured: the phase from 6.4 to 2 to 3 ms, an episode 1.6 to 1.8 times
        faster. A change of rounding (#7, regrouping the eddy sum, about 1.5 times more) and the
        world rules (#10, the snow carried every second step; #11, 2 m snow cells) go to the
        owner only after the bit-identical work is measured, and only if it falls short.
      - **The replicate ended** (search 05:11, exam 05:34 on 2026-10-03; registry entry 3, which
        the nursery writes to the main checkout's `rounds/nursery/registry.jsonl` by design). Every
        constant and kinesis scored as in the pilot to the hundredth of a joule. The read is in
        `logbook/specs/nursery-plan-joint-drive.md`, "The replicate's exam". In short:
        - Body 10 beats its best constant again, by 3.7 standard errors. No other body clears that
          bar in both runs.
        - One body in six ends stuck in each run, on a brain that barely moves: the pilot's body
          8, the replicate's body 0.
        - Which senses a brain reads differs between the runs. The scent is unread in both.
        - The pooled own brain is 24 J below the pilot's (about 16 J standard error). That is the
          yardstick for the two separation arms, with the count of bodies that clear the bar.
        Its launch, as recorded: `brain2-rep2`, the first registered arm, pid 142044,
        started 2026-10-02 at 15:57. It was launched from `scratch/wt-nursery-general` at
        `56c0f6f`, recipe hash `52a4d23a…090b`, 14 threads pinned to the efficiency cores, and
        writes to `runs/nursery/brain2-rep2` there. Its design:
        - the six bodies from pilot-deep-brain2's exam pool (`rounds/nursery/pools/97d4a4fc12a91017.tsv`);
        - fresh brains, and the search's water, drawn from seed 0x2;
        - exam salt 0x62656e636b, so its held-out draws are the pilot's.

        It ends near 05:00 on 2026-10-03, its exam after. Check first that every constant scores
        what it scored in the pilot's exam, to the joule. Then read it with
        `scripts/reads/nursery-exam-per-body.py` against `scratch/nursery/pilot-deep-brain2`.
      - **Superseded by the measurement above.** My inference, not measured: the
        dense founders read every sense on every part each physics step. The position-dependent
        channels (flow, scent, food) are lookups in the world's fields, and the control's sparse
        brains mostly skipped them. The network itself is about 85 multiply-adds, and `Brain.For` builds it
        once per body (`Creature.cs:420`), not every tick.
        - Measure first, with nothing else running: one tank episode profiled with the full pool
          against the joint senses only.
        - The fixes, in order:
          1. a cache within the step. A field is looked up once per part per step and shared
             across channels. Bit-identical, so agent work. **Already in place, found
             2026-10-02 at 15:20.** `CreatureSenses.Sample` (`src/Evosim.Dynamics/CreatureSenses.cs`)
             already samples each channel once per part per step into arrays that every input
             reads, and it skips any channel the brain's sensor mask leaves out. So this fix buys
             nothing, and the cause of the 1.5 times is unmeasured. The other suspects are the
             dense brain's own step (about 96 inputs, each through `Read`'s switch) and the
             channels a dense mask can no longer skip (flow's rotation, scent, food);
          2. the owner's idea, from 2026-10-02 morning: make the input tensor the source of truth,
             written when its source changes rather than read every tick. It wins only for channels
             whose source changes on a known beat (the solver's joint values, the metabolic step's
             reserve). It saves nothing for position-dependent ones, which change every step. Its
             costs are a second copy of the truth, the tensor as checkpoint state, and the world
             knowing the brain's port layout;
          3. sampling the expensive senses at the metabolic step. That is a world rule, so it is
             the owner's call.
      - **How to read its exam.** The control's old brain has oscillator neurons, so it can stroke
        with no senses at all, as a pattern generator. Five of its six bodies' brains were blind. Brain
        v2's units are all tanh sums with no oscillator and no clock, so every stroke starts from a
        sense. A brainless or all-senses knockout therefore costs brain v2 by construction, and shows
        dependence, not skill. The questions to read are two:
        - does each body's own brain beat its best constant stroke by two standard errors;
        - does the Scent knockout cost energy, and does it remove the richer water at the body?
      - **Coordination, from 2026-10-02 late morning.** This session (evolution-simulator-6e) is the
        owner's coordinator for the general nursery build (11b). The owner said the coordinator could tell it to wait
        until the coordinator was ready, since the coordinator is in charge, and to tell it how to work with subagents.
        - Owner-reserved decisions still go to the owner directly: world rules, a planned arm's reward,
          the next arms, scope, anything irreversible or outward-facing, installs, outbound data.
        - At 11:10 the builder (a8) had step 1 landed (`dd4fce4`) and a go for steps 2 to 4, light
          load only: filtered tests at `-m:1`, no episodes.
        - Step 5, the check, waits for "machine free", sent when `pilot-deep-brain2` and its exam end.
    - **Literature pass 1** for the proposal's review round: `research/brain-v2/pass1-candidates.md`. Two
      candidates bear against one brain per creature by their summaries (arXiv 2206.13366 first); read
      them in pass 2.
    **Superseded by D148** (2026-10-02). The baseline is now brain v2's setup, and the next arms are the
    owner's, chosen with brain v2's exam in hand. If its brains use their senses, each arm removes one
    piece of the baseline; if they are blind, the arms change what pays. The list below is the history.
    **The nursery's next arms, in order** (2026-10-01 evening, before the pause). Each changes one thing against the
    control `pilot-deep-own16`, which ends near 01:00 on 2026-10-02 on the efficiency cores, its exam after.
    1. **The drive sense (D144)**, built on `joint-drive` as `8d7daba` and built as nursery-v8:
       `SensorChannel` Stroke, Hold and Ramp; `RunConfig.SenseDrive`. Core 1,045 pass; Dynamics 141 of
       151 and Farm 191 of 197, the same failures as the commit before (the r52fix-s4 fixture; the hash
       and header pins from round 52's build). The identity run started 19:42 on 2026-10-01 (pid 91696, out
       `identity-v8-drive`); its founders match the control's draw for draw; the chain is pid 101656. Configs:
       `runs-jd/big-cur-deep-v8` (the control's with `senseDrive` false) and `runs-jd/big-cur-deep-drive`
       (true). `scratch/nursery/identity-drive.ps1` runs v8's generation 0 at 2 threads and writes
       `identity-v8-drive.verdict` ("same" when its row equals the control's but for the clock).
       `chain-drive.ps1` starts `pilot-deep-drive16` on the efficiency cores when pid 94484 exits, and only
       on "same"; log `chain-drive.log`.
    2. **The free first joint (D143)**: `chain-free.ps1` (pid 92252 from 22:03, log `chain-free.log`,
       deadline 12:00 on 2026-10-03) starts `pilot-deep-free16` on nursery-v6 when `chain-drive.log` says
       the sense arm ended, pinned to the efficiency cores.
    3. **Round 55** (D145, the owner agreed and said to lock it, unless something very interesting turns up in
       between): tests in the world whichever of the free joint and the drive sense earned more, or both;
       no inoculant unless an arm's brain beats its body's best constant by two standard errors, and then
       one seed only, with the owner. Its pre-registration is drafted while the two arms run.
    4. **The envelope evolving with the brain** (`--evolve-envelope`, `066548f`; the owner thought the
       envelope should be part of the nursery's experiment genome and not fixed), its own arm.
    5. **The crossover** (11a).
11a. **The nursery's two-parent crossover** (`proposals/propose-nursery-crossover.md`, the owner's design
    of 2026-10-01, which the owner asked to have written up). **Built** on `joint-drive` as `2186b07`
    (nursery-v7, 26 tests; options off byte-identical to nursery-v6 on a tiny search). The distance
    counts each added neuron or input as one unit beside Core's brain term; there is no neuron limit.
    d_ref on the older `pilot-deep-own` pool: median 0.13 (quartiles 0.01 to 1.01, 21% of mutations
    move nothing). Next: d_ref on `pilot-deep-own16`'s exam pool (`--measure-dref`), the identity check
    (`scratch/nursery/launch-crossover.ps1 -DRef <d> -Identity`, its gen 0 line against the control's),
    then the run (`launch-crossover.ps1 -DRef <d>`, out `pilot-deep-cross16`) at 14 threads in the
    order of 10a, the other session told first. Its scripts name nursery-v7 and the control's config,
    which v7 reads; v8 would refuse that config (a new tunable) and takes `big-cur-deep-v8` instead.
11a. **No nursery run until its pace is understood and sped up** (the owner, 2026-10-04 evening).
    A day for one nursery would slow the campaign too much, and round 56 finishes first. The next
    nursery design was discussed and not proposed: a dark, leafless tank with food measured from the
    rounds' matter-eaters, bodies and brains evolving together by mutation alone, newborn starts,
    episodes of about 1,500 s, scored on time to a fixed breeding gate (the body's own child cost and a
    fixed reserve floor) with the energy banked and then survival as partial credit, never distance,
    and niches by body type. Brain v2 is not slower than the per-node brain: its 7.7 minutes a
    generation (600 s episodes, 14 threads) were on the efficiency cores, where the per-node control ran
    7.64 against 4.6 on all cores (measured 2026-10-02, item 10 below). On the column kernel a generation
    takes 5.0 to 5.1 minutes on 14 efficiency cores, so a 1,500 s episode puts one at about 12.6 to 12.8
    minutes, the speed-up agent's estimate, which assumes the cost scales with episode length and which its
    slot will firm up. The speed-up agent's reading puts an episode's time in the world's water and the
    physics, not the brain; its bit-identical candidates are the generation's tail, the water sampled
    per link, the bodies' own step and the rest of the advection list. All cores against the efficiency
    cores (about 1.6 times) is a heat ruling for the owner. The owner put a separate agent on it on 2026-10-04: session
    evolution-simulator-51, branch `nursery-speed` from `nursery-general` in `scratch/wt-speed`,
    findings to `logbook/specs/nursery-speed.md`; it runs nothing beyond reading and compiling at
    2 processors without the coordinator's go, and its timing slot follows the main merge's suites on
    2026-10-05.
    **The nursery on the card: two questions open with the owner** (raised by the owner on 2026-10-04
    evening, answered by evolution-simulator-51). A generation's tanks are independent, and the card
    could run them all at once; the body step is already ported and exact in double (logbook/0115).
    51's guess is 3 to 10 times faster a generation. Three things block it: the card's hold to about
    2026-10-06; single precision, which would be a new realisation and is the owner's call; and the
    many-tank batching, a build of several days by 51's guess. The questions put to the owner: whether to
    plan a nursery-on-the-card test after the hold, and whether single precision is acceptable for the
    nursery. A read-only Fable planner (0 processors, writing only under `scratch\nursery-speed\gpu-plan\`)
    starts only if the owner says yes to the first.

11b. **The next proposal: a general nursery** (the owner, 2026-10-01, wanted it so that the project can just change some
    settings, perhaps describe the reward function or register one, and not rewrite
    code every time it trains something else; not for this round of the nursery but for the
    next). **Ruled on 2026-10-02 (D148); being built.** The design and the build's state are in
    `logbook/specs/nursery-general-build.md`, which absorbed the proposal (deleted; the final version is
    `3b603e9`). It covers the run record, recipes, the episode's setup as settings, rewards as registered
    C# classes, the registry in `rounds/nursery/`, and brain v2's setup as the baseline. One check comes
    before the old code is deleted: the baseline recipe must reproduce `pilot-deep-brain2`'s first
    generations. The old experiments are discarded, and the archive mode and the swim test retired.
    - **Who:** session evolution-simulator-a8 builds it, on instructions from evolution-simulator-6e,
      the owner's coordinator. Branch `nursery-general` from `joint-drive` at `dc5dd6f`, worktree
      `scratch/wt-nursery-general` (`scratch/wt-nursery` was taken by the older branch `nursery`).
    - **State (2026-10-02, afternoon):**
      - Step 1, the run record, is committed (`dd4fce4`).
      - Steps 2 to 4 (the recipe, the reward interface, the registry) are committed (`41206ff`), with
        123 tests passing and two Opus reviews.
      - `ba8f970` on `nursery-general`: two recipe tests read the baseline with CRLF turned to LF,
        since a fresh checkout writes CRLF.
      - Recipe runs launch from a worktree (the coordinator's ruling). The two `CLAUDE.md.meta`
        files are committed on main (`9e11e3d`), so a run from main is clean too.
      - The registry is the main checkout's `rounds/nursery/registry.jsonl`, committed with the
        merge and not before. The same merge commit adds it as an exclude to
        `scripts/farm-queue.ps1`'s clean-tree check (the coordinator's go, for that one pathspec), and
        keeps `scripts/CLAUDE.md`'s description of the check in step.
      - **Step 5 passed** (2026-10-02, 14:47 to about 15:08): every row of generations 0 to 3
        identical; the table is in the build spec (`e7dbb2e`), and the coordinator accepted it.
        `pilot-deep-brain2` is entry 1 of the main checkout's `rounds/nursery/registry.jsonl`, a
        reference entry, uncommitted until the merge. **Step 6's rerun** of the same recipe on
        nursery-g6 (Release, `2706a21`) runs: pid 128988 from 15:14, 14 threads on the efficiency
        cores (affinity 0xFFFF0000, the coordinator's rule), out
        `scratch/wt-nursery-s6/scratch/step6check/run`. A session cron reads the heat every 10
        minutes. When it ends, `python scratch/nursery-general/step6/digests.py` must print
        DIGEST-IDENTICAL; the result goes to the coordinator. Step 5's setup was:
        - The recipe is `scratch/wt-nursery-general/scratch/step5/brain2-check3.json`: brain2 for 3
          generations, the exam cut to own on one draw. Its recipe hash is `6cf7efd4…`.
        - The Release build is `scratch/wt-nursery-general/artifacts/nursery-g1` (from `41206ff`).
        - The launcher is `scratch/wt-nursery-general/scratch/step5/launch.ps1`: 14 threads,
          detached, out `scratch/step5/run`.
        - The comparison is `python scripts/reads/nursery-baseline-check.py
          scratch/nursery/pilot-deep-brain2 <run>`, which prints a table per file and generation.
          It is uncommitted on main.
        - The report to the coordinator is that table plus the thread count. The table also goes
          into the reference entry's verdict, and the entry is written only if all is identical.
          If anything differs, the first differing file and line go to the coordinator before
          anything is decided.
        - Temperature is read with `scripts/temp-check.ps1` every 10 minutes while it runs.
      - **Step 6** is committed on branch `nursery-general-s6` (worktree `scratch/wt-nursery-s6`) as
        `b6d8511`, rebased onto `nursery-general` (so it carries `ba8f970`). 124 tests pass, and Opus
        found nothing that changes a number or draw. Uncommitted on top of it, with its test build
        still to run: `Recipe.Resolved` refuses a path that leaves the repository (`..`), with three
        tests (the room's review, finding 6). It deletes:
        - the archive search, the swim test, the timing run and film (the owner agreed it should be deleted)
        - the snapshot body source, FlagBrains and every old flag
        - purse "world"

        `--measure-dref` now runs under a recipe. To finish: rerun the step 5 check on its build,
        which must be digest-identical to step 5's run. The merge goes to the coordinator first.
      - The coordinator's old build `scratch/wt-joint/artifacts/nursery-v10` keeps `--time-one`
        for its sensing-cost measurement. Nobody touches `scratch/wt-joint` until it says so.
      - Until "machine free": one build or test at a time across every worktree, at `-m:1`, and
        nothing swims.
    - **The coordinator's rulings, inside D148:**
      - If the check passes, `pilot-deep-brain2` enters the registry as a reference entry, naming its
        build and the check's verdict, and `--rerun` refuses it.
      - The Core purse method is deferred. At step 6 the purse "world" option is dropped if no recipe
        uses it.
    - **The nursery room** (the old dashboard proposal, rethought by the owner): a design brief and
      wireframes in `design/nursery/`, made with a Claude Design session ("Cambrian Nursery Design").
      The owner's rulings:
      - The new nursery should be completely in git.
      - Go with a web app, and if a good creature render is wanted it can be done in Blender.
      - The app must have an MCP that an agent can communicate with, because the agent will manage
        the experiments and the owner will not do it by clicking.
      - The owner wants a cool design and not an ugly one.
      - Responsive across common monitors, with no mobile or vertical layout.
      - Of three visual directions, the owner chose the dark-ocean one (direction A looked best to them,
        though it might be slightly over the top), which is being toned down across the five screens.

      - The design's pass 2 (`556c550`): all five screens, contrast checked by
        `design/nursery/contrast.py`, every panel annotated with its data source. The owner, the
        same day, was pleased with all of it.
      - The owner's further rulings:
        - Be careful not to make things specific. We now have scent, but next week some other input
          may matter, and we may have different outputs and different reward functions. The aim
          is to generalise.
        - Any action taken by hand on the screen should be possible for the agent through the MCP,
          which needs a proper design that does not create an endless number of tools in a single
          MCP. The recommended shape (BRIEF.md) was accepted.
        - The owner asked whether there was a reason not to start the build already, and whether it
          had to be explicitly read only or whether that mattered. So the build has started. Its spec is
          `logbook/specs/nursery-room-build.md`.
      - **Phase 1 of the room** (server, operation list, MCP, the registry, run and recipe screens
        on real files) is built on branch `nursery-room` (worktree `scratch/wt-nursery-room`, from
        `nursery-general`), committed and then rebased onto step 6's `2706a21` as `1248d94`, with the
        Opus review's fixes in and contrast clean at three sizes. What it holds and how to start it
        are in the build spec. The owner was sent `http://127.0.0.1:5179`. **Phase 2** (the writes,
        the clean stop in the nursery, the cap, the activity log, `rounds/nursery/appended.txt`) is
        with a Sonnet builder (brief: `scratch/nursery-general/room2/brief.md`). It stops the room
        server (pid 103368) to rebuild; restart the server after it. Its acceptance run (an agent
        writes, checks, launches a 1-generation trial through the MCP, stops it) waits for "machine
        free". At the merge, `scripts/farm-queue.ps1` reads `appended.txt` for its excludes (the
        coordinator's go, 2026-10-02, on the condition that the list is defined once).
        **Phase 2 is committed** on `nursery-room` as `6edb22f` (two Opus reviews, every finding
        fixed; 297 room tests, 138 nursery tests; a smoke of every refusal). The owner approved
        stopping the old room server (the owner said it could be stopped). The room runs on
        `127.0.0.1:5179` from the `nursery-room` worktree. **Its acceptance passed** through the MCP:
        write, check, launch, and a stop after generation 0 (`accept-stop`: status stopped, exit 0,
        not registered, 5 activity rows). One mishap: the first acceptance recipe, `accept-1gen`,
        ended in seconds before a stop could reach it and was registered as entry 2 (not
        rerunnable, dirty tree). The line is kept; whether to commit the two acceptance recipes
        or drop the line before the registry's first commit is with the coordinator. Not built yet:
        the room does not pin a launch to the efficiency cores (a `room.json` setting is suggested).
      - **Merge scope** (the owner, through the coordinator, 2026-10-02: the joint-drive line stays
        off main until its first round is pre-registered; HANDOFF 10a, `45bb05e`): `nursery-general`
        is fast-forwarded to step 6 (`2706a21`), and `nursery-room` sits on it. Nursery runs launch
        from the `nursery-general` worktree. The farm-queue exclude goes in with the merge to main.
      - **The first registered arm, `brain2-rep2`** (the owner approved it as the baseline again
        with a new search seed): `rounds/nursery/brain2-rep2.json` and the pool
        `rounds/nursery/pools/97d4a4fc12a91017.tsv` are written in the `nursery-general` worktree,
        committed on `nursery-general` as `56c0f6f`. The exam's draws are `SeedFor(seed ^ salt, e)`,
        so its salt is 0x62656e636b (the coordinator's ruling): 0x2 ^ salt = 0x62656e6369, the
        pilot's, which pairs the exams draw for draw. **The coordinator launched it** at 15:57:37:
        pid 142044, 14 threads, affinity 0xFFFF0000, recipe hash `52a4d23a…090b`, out
        `scratch/wt-nursery-general/runs/nursery/brain2-rep2`. It ends near 05:00 with its exam
        after. Until then: light load only, no other episodes, nothing in the room longer than a
        smoke, and nothing that rebuilds or moves the `nursery-general` worktree. `nursery-room` is
        rebased onto it (phase 1 `8cc52cd`, phase 2 `61e6523`, acceptance recipes `b8cf15c`).
      - **Phase 3a and 3b** are built, uncommitted (166 nursery tests, 312 room tests, nothing
        swum). Opus traced every random draw and result write against `b8cf15c` and found nothing
        that moves a byte or a draw. One should-fix: the mask is to be checked against the system
        mask, not the room's own processor count. That fix, the nits, and launch as trial (the
        owner agreed) were built (`room3/fix-brief.md`; 172 nursery tests, 353 room tests, both
        re-run by me). A second Opus review found one must-fix that predates the change: the guard
        against replacing a recipe a run is using lapses once the run writes `run.json`, whose
        recipe path is absolute. It also found a trial flag on the recipe screen that disagrees
        with launch, no check for links, and nits. A second Sonnet builder fixed them
        (`room3/fix2-brief.md`). **Committed as `6cc0c3f` on `nursery-room`**: 401 room tests and
        172 nursery tests, re-run by me. The room server runs it, pid 155168 on 127.0.0.1:5179,
        started from the worktree's Debug build, logs in `scratch/wt-nursery-room/scratch/logs/`.
        Launches are now pinned to 0xFFFF0000. Brief: `scratch/nursery-general/room3/brief.md`. The build went into
        `artifacts/verify-out` because the room server holds the normal output. 3a is
        `room.json`'s `affinity`, set before the launched process runs its first instruction. 3b is
        the nursery's new data for the tank and genome screens: `tank-draws.jsonl`,
        `tank-exam-draws.jsonl`, `tank-lineage.jsonl` and the exam's plan in `run.json`. Every
        existing result file stays byte for byte; that check is a digest against step 5's run,
        after 05:00.
      - **The machine's order after the replicate and its exam** (the coordinator, 2026-10-02):
        (1) its timing of advect-fast (`d58d7af`, a bit-identical Core speed-up to the snow's
        advection), four one-thread trial runs with nothing beside them; (2) the digest check
        above, 14 threads pinned, started only on the coordinator's word; (3) the Core `-All`
        suite on advect-fast, alone; (4) advect-fast merged into `nursery-general`. Until then,
        nothing is committed to `nursery-general` without telling the coordinator, and
        `GridField.cs` and `CurrentField.cs` are left alone.
      - **Phase 3c, the genome screen**, is committed as `1738587` on `nursery-room` (465 room
        tests; briefs in `scratch/nursery-general/room4/`). The room server runs it, pid 138208.
        The pilot's genomes are now grown under the config it ran on, which `room.json` names.
        Under Core's defaults, 4,462 of their 6,451 links named no port. Next: the tank screen.
        Its re-stepping runs an episode, so it waits for the machine.
      - **The tank's plan** is `logbook/specs/nursery-room-tank-plan.md`: a read-only watcher in
        the nursery's `TankEpisode` and a `--restep` child process, steps S0 to S7. Its first
        risk is that the pilot's exam has never been reproduced on the current code; S1, a
        2-minute trial bench, tests that with no new code. Asked of the coordinator: the
        nursery edits on `nursery-room`, and a timing-read declaration file. S0 (the room
        rebuilds the pilot's exam plan; no swim) is with a Sonnet builder
        (`scratch/nursery-general/room5/s0-brief.md`).
      - **Tank S0, S2 and S5 committed** on `nursery-room`: `15c6874` (S0, the exam plan; `15924a3`
        fixes a test's cleanup that 15c6874 was committed failing) and `8489b8d` (S2, the
        nursery's read-only watcher and `--restep`, after an Opus review and a fix round; S5, the
        room's `episode.step`, cache, cap and timing-read refusal). 209 nursery tests, 534 room
        tests; room server pid 159736. **In the machine slot, in order:** 3b's digest check,
        rerun of step 5's recipe on the current Release build (`artifacts/Evosim.Nursery/bin/Release`,
        which includes S2's null-watcher path), checked by `scratch/nursery-general/room3/digest3b.py`;
        then S1 (`scratch/nursery-trials/s1-pilot-exam.json` in the room worktree, launched
        through the room as a trial); then the swim tests S3 and S4 (the command is in
        `RestepSwimTests.cs`'s header).
      - **The room as operated** (the owner, 2026-10-02 evening, argued it was not working as
        expected, asked whether the agent had tried to operate it, and suggested having a Sonnet open the browser and inspect it).
        - A Sonnet agent drove headless Edge over the DevTools protocol and found 21 faults
          (`scratch/room6-inspect/`, driver `cdp-lib.ps1`). The worst was an empty RECIPE tab.
        - Round A fixed 15 of them; a fresh walk confirmed each. It is committed as `f430ecc` with
          S6, the tank screen's first cut. 573 room tests; room server pid 161364.
        - Round A2 is with a Sonnet builder (`scratch/nursery-general/room6/fix-a2-brief.md`): the
          walk's six new faults, one of them a regression. Then another walk.
        - Round B, the mocks' unbuilt panels, comes after.
        - The rule is now in `nursery-room-build.md`: a screen is walked in a browser before
          anyone calls it working.
        - Room work resumed on 2026-10-04 at 20:00. B1b is committed as `5e02ca9` (room suite 622).
          B2 is committed as `4d36925` (room suite 657); the room scope the owner cut is built, and `nursery-room` holds `nursery-general` by a merge (`e824ed4`; nursery 211, room 657). The
          walks' small leftovers are built only if the owner asks. The owner ruled on 2026-10-04 that a8
          asks the coordinator before anything heavy runs (a browser walk, a builder round, a full suite, a
          nursery run); its next is the Sonnet walk of `logbook/specs/nursery-room-tests-plan.md` step 2,
          given the slot after the speed-up agent's timing on 2026-10-05. The coordinator reviewed the
          merge `e824ed4` and kept it. The room
          server runs on 5179 from `scratch/wt-nursery-room`. Machine-slot work (3b's digest rerun,
          S1, S3/S4) waits until round 56 frees its slots, about 10:00 on 2026-10-05.
        - Builders have gone round the guard's "system path 'C:\Program'" block on `Remove-Item`
          with `rm` three times today, on their own files. Reported to the owner; briefs now
          forbid it by name.
      - The owner's rulings, 2026-10-02 evening (`e65d071`): the 1366×768 run screen scrolls as one
        column (the owner agreed); launch as trial is allowed (the owner agreed), to be built in the room after 3a and 3b.
        Whether the recipe nursery keeps a way to time one episode the owner handed to the
        coordinator. The owner said they did not know, asked whether it needed one, and thought
        it was a question for the other agent. The coordinator ruled to leave it out. A timing read is a small trial recipe at
        `--threads 1` with its exam cut to `own` on one draw (nursery-general-build.md, uncommitted).
    The text below is the history. The owner's aim is for the nursery to take as much as
    possible from the simulation world and to abstract its settings as far as possible,
    so that we can experiment without massive code changes to the nursery each time,
    only evolving it, with a simple experiment registry that we can persist and rerun on
    demand. The owner agreed with the agent's three cautions and asked for them to be passed along, and the
    filed proposal must carry them as review notes: (1) named, tested components in code that an experiment
    composes by name and weight in settings, not a reward expression language; (2) the world's rules
    and config by default, every departure (held births, fixed bodies, episode length, purse) named in
    the experiment's settings; (3) generalise around the experiments actually run (still tank, big tank,
    deep water, noise bench, priced and free, crossover), each re-expressed as a registry entry that
    reproduces its original output on its build, every entry naming its commit. Built after the
    crossover test, off round 55's path.
12. **Watch the story flow's first subagent write with the owner**, since a subagent's writes are
    untested.

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
