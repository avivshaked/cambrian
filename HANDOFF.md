# Handoff: where to pick up

*Rewritten 2026-09-26 from the current state. What happened is in the logbook, and why it was
chosen is in [`DECISIONS.md`](DECISIONS.md). This file says only where things stand and what is
queued; it is rewritten, never appended to. The notes it carried before this rewrite, rounds 42
to 49 and the speed work of 2026-09-25, are `logbook/specs/handoff-archive-2026-09-26.md`, and
the ones before those are `logbook/specs/handoff-archive-2026-09-22.md`.*

## Where things stand

### The machine's rulings

Speed first (the owner, 2026-09-25): 10,000 creatures fast is the committed target and 100,000
a stretch. Option A: rounds run overnight on the CPU, one seed at a time at sixteen threads, and
the card is worked by day with nothing else on the machine. From round 51 an overnight
round also hands the snow's transport to the card, which gives the CPU's bits (D126, the owner's
ruling of 2026-09-26). Nothing else runs on the card at night. The total load stays at or under
half the machine, 16 of 32 logical processors. The processor's microcode is still 0x10E. The
owner deferred the BIOS flash and watches for warning signs, and any unexplained crash or clean
re-run is reported the same hour (CLAUDE.md, "Read the CPU's microcode"). Load is reported as
`% Processor Utility`, the counter Task Manager shows.

### Round 49 is read

The entry is logbook/0122, committed as `0f737f1`. Twenty-five clauses hold, six fail
and four are readings. The founder cap held: no founder had a child within ten seconds of
landing, and one pool stomach in 63 earned its child. The eater lines that lasted came from
random founders that were a stomach on a link, and a link catches light in this world, so those
bodies were part plant. The depth rule set the trickle's leaves where the dissolved matter was
richest, deep in the dark. They died in about a minute and a half. A clamp to the bed at the
tank's centre held most of them at 44 m; it is fixed. The round's own checkpoints gave the first
faithful film windows. Round 49 is not filmed yet. Its checkpoints stay whole until the owner
approves its video as final, and then they are thinned only as the storage ruling below says.

### Merged into main on 2026-09-26

- `bd4427b`: the founder clamp reads the bed under the candidate (`founder-depth-bed`). A new
  test fails on the old placer and passes on the fix. The fidelity check skips the contact
  flag `TouchedBedOrGlass`, which round 49's V2 named, and V2 passes on the fixed check.
- `044164d`: the speed patch (`speed-serial`). The serial phases after the physics step run
  across the world's threads. It was accepted from round 49 seed 2's checkpoint at 29,000 s
  against round 49's own exe. The digest was identical over 3,000 steps at 16 threads and 200
  at one, and the stats identical on 30 samples. It ran 300 s of about 10,600 bodies in 4.2
  minutes against 4.8, 1.19x real time against 1.04x (`scratch/r49-speed/accept2.log`).
- `8e0741a` and `0f737f1`: round 49's entry reads under `scripts/reads/r49-entry/`, and the
  entry. `5c98a5c`: round 50's reader, `scripts/reads/r50-read.py`.

### Round 50 has run

The owner ruled option (a) on the morning of 2026-09-26 (D125). A leaf founder is set in the cell of its column where its own income, light and
matter together, is largest. The price is the call the world bills a body with. A stomach is placed
as before. The build is `leaf-income-depth` (`scratch/wt-leafincome`); the launcher is
`rounds/env-r50.ps1` (`EVOSIM_FOUNDERS_INCOME_DEPTH 1`). Its pre-registration is logbook/0123.
The owner said to start it at 23:52 on 2026-09-26. The three seeds ran to 30,000 s in 178, 236 and 
211 minutes and the queue ended at 10:27 on 2026-09-27 (queue item 1).

### The card, by day

The probe of 2026-09-26 morning timed each size class's kernel on round 48 seed 1's crowd of
8,385 bodies (`logbook/specs/gpu-probe-2026-09-26.txt`). The classes hold bodies of up to 2, 4, 8
and 16 links. Launched one after another they cost 0.83, 1.46, 2.61 and 2.08 ms a step, and the
contact grid 1.57 ms. A class costs about its slowest thread's latency and not its body count, so
the 17 sixteen-link bodies took longer than the 7,653 two-link ones.

Seven changes on the `gpu-probe` branch followed the same day. Each was checked on the card
against the run before it, round 48 seed 1 resumed at 27,500 s for 300 s. Each was identical over
all 300 digest steps and equal on the stats, at group sizes 32 and 64. The first runs the classes
on a stream each (`EVOSIM_GPU_CONCURRENT`, `13af2c3`), so the step waits for the slowest class and
not for the sum. The second is the grid's prefix sum. It ran on one group of 1,024 threads over
524,288 buckets and cost 1.25 ms; three passes over tiles now do it in 0.02 ms (`d37421f`). The
next three move work off a body's one thread and give each of its links a thread. They are the
water (`771c15e`), the contacts (`4665326`) and the fluid (`6519eb8`). The seventh sends and
fetches the overlap lists by their used rows alone (`b4c7a80`). The card's physics went from
8.98 ms a step to 1.49, and the run from 0.94x real time to 3.15x at 8,414 bodies. The probe
record has the table.

A phase probe reads the card's cycle counter between the parts of the body kernel (a build with
`-p:GpuPhaseProbe=1`, never a scored run's). Before the link kernels, the water and the contacts
were most of every class. After them the body kernel is its loads, its brain and its stores, and
the classes cost 0.60 ms a step together.

The world step and the harness came next, and they speed up the CPU farm too. A probe build
(`-p:WorldPhaseProbe=1`) timed the world step's passes: of its 68 ms, the snow's transport took 31,
the metabolic pass 16 and the corpses 7.5. The per-body passes of the world and of the harness now
run across Core's threads (`8c8120f`, `238ebce`). Each is a parallel read followed by the serial
writes in their old order. They are the lit areas, the bills, the corpses' drift, the freeze of
availability, the divergence check and the pose exposure. The world step went to 53 ms and the
harness from 14 to 6. The run went to 3.71x, about 2.5 times the CPU farm at 16 threads on the same
crowd. Every change was identical to the run before it on the card and on the CPU farm.

`gpu-probe` took main (`b7bea5d`) and was merged into main (`bf1add4`), so the card now runs
current worlds, and round 48's config is refused as it is on main. On round 50's world the merged
build is identical to round 50's own build, and the card at group 32 and 64 to its sequential run.
That crowd is smaller, 1,700 bodies, and there the card leads the CPU by about 1.4 times, so the
card pays off with the crowd. The Editor compiles the Farm as a local package, and nothing on the
branch had been through Unity. So before the merge the package was compiled the Editor's way, as a
netstandard2.1 library at C# 9 without `EVOSIM_GPU` (`src/Evosim.UnityProxy`).

A metabolic step at 8,400 bodies is now 135 ms. The card's step loop takes 53, the snow's
transport 30 and the rest of the world 23. The copies to and from the card and their preparation
take 22, and the harness 6. The transport's sum is a chain of dependent adds whose order the
bits fix.

The snow's transport runs on the card since the afternoon of 2026-09-26, under
`EVOSIM_GPU_TRANSPORT`. Core keeps the transport's decisions (the Courant refusal, the substep
rule, the instant) and hands a device the arithmetic through `GridField.TransportDevice`. The
farm's `GpuTransport` writes every product as `mul.rn`, so it gives the CPU's bits. On round 50's
world from 4,000 s it took the card from 4.0x to 6.0 and 6.9x real time, in runs alternated in
one sitting. That is 40 to 50 ms a step saved at any crowd. Every run was byte for byte the run without
it, the snow's whole stock included (`logbook/specs/gpu-probe-2026-09-26.txt`). The card's host
arrays became page-locked in the same change, which saved about 3 ms a step at that crowd and
should save more at round 48's (not measured). The CPU farm can use the device too: with
`EVOSIM_GPU_TRANSPORT` on the cpu engine the bodies stay on the CPU and the transport goes to the
card. At 16 threads on the same world that went from 3.7 to 3.8x to 5.0x real time, identical to
the CPU-only run. The launch refuses a world the card will not carry, and `run.json` records
`transport` as `cpu` or `card`. The owner ruled on 2026-09-26 that overnight rounds use it
from round 51 (D126), after the check in queue item 3.

The probe reads `EVOSIM_GPU_PROBE` from the process environment directly, which bites. The
script's `-Env` passes settings as arguments and does not reach it, and the binding warns that it
ignores the variable. Set it in the launching shell's environment (`scratch/r49-probe/probe2.ps1`).

### Storage, as the owner ruled it on 2026-09-26

A round 49 seed's folder is 3.7 to 5.4 GB, most of it
checkpoints: seed 2's 300 checkpoints are 4.1 GB. A checkpoint at 15,000 s is a 12.5 MB file.
Unpacked, it holds 16 MB of genome text, which the run's genome file already holds, and 31 MB of
state. Packed against the checkpoint 100 s before it, it keeps 91% of its size, so the state
does change from one to the next (`logbook/specs/r49-read/ckprofile.txt`, `ckdelta.txt`). The
ruling: a round's checkpoints are thinned only after the owner approves its video as final. They
then keep one every 1,000 s plus the one at or before each scene's start. The keep list comes
from the video's scene table, and the agent shows it and asks before each thinning.

### Round 48's story film is delivered

It is
`scratch/owner/r48-story-full/r48-story-full-v2.mp4`, 578.9 s, 20 scenes and the title, every
scene FAITHFUL. Three flaws went to the owner unfixed: in scenes 11 and 17 the chart panel covers
the subject, scene 16's subject is unclear, and scene 20 ends on bare sand. For the owner's
narration trial, the prose story and the captions by scene are beside it under
scratch/owner/.

Another session works on the story tools. Its files are under .claude/skills/story-resolve/ and
assets/, with logbook/specs/story-resolve.md and the scripts named story-resolve, and they are
left alone.

## The decisions in front of the owner

### Ruled on 2026-09-27, after round 50's read

- Links stop catching light from round 51 (`EVOSIM_LINK_PHOTO 0`). The owner added that the first
  link, its neurons and its muscle may be free, to encourage joints; what "free" means is for the
  round 51 proposal to set out (neurons and joint work already cost nothing in every launcher).
- The pool keeps its one-part stomach for now.
- The owner's vision for round 51: a dead body becomes a grey husk that sinks, settles and
  shrinks as its matter dissolves into its cell, and eaters meet it and eat it directly. Round 50
  shows why there is no larder today (seed 2's last 5,000 s): every death is a starvation with
  the reserve at 0, so a corpse holds its tissue alone, about 12 J against about 42 J held by a
  living body; a corpse sinks at the snow's 2 mm/s and leaks 0.5% a second, so half of it is gone
  in about 140 s within 30 cm of the death; corpses give the snow about 40 W and the living plants'
  exudate about 170 W; 218 of 71,544 births had a mouth, and 4 corpses were eaten in the run.
- Movement: the owner sees the joints wag without moving the body, like an engine and not like
  biology. `fable-propose-reactive-thrust.md` diagnoses exactly that and waits for its ruling.
- The theatre: a connective-tissue skin over each link, in the pink-purple the owner liked (most
  likely `TheatrePalette.Jointed`, the colour of the neck at every joint). Theatre only, no hash.

The two decisions below are ruled as above and kept until the rewrite for the record.

Neither of the first two blocks round 50, which is built and pre-registered without them. Both
come from round 49's read, and each is set out in full here and in any message that asks for it.

### Whether a link should catch light

A link is the part that carries a joint. Since D043 and D046 (2026-08-28) it also catches light,
at a share of a leaf's rate that the launcher sets; every launcher from round 42 sets half
(`EVOSIM_LINK_PHOTO 0.5`). So a body made of a stomach and a link is part plant. In round 49 the
only eater lines that lasted, lines 48, 29 and 5854, were such bodies. Line 29 earned 0.19 W of
light beside 0.66 W from the snow, and line 48 faded as its members lost the link. The report's
flags count a leaf and a stomach and not a link, so these bodies are filed as pure eaters, and
round 49's S2 read them as stomach children.

- Keep the half (the world as it is). The eaters that last stay part plants, and the question
  "can an eater live on the snow" stays mixed with "can a stomach on a lit link live". The
  earlier rounds are untouched.
- Set it to 0 from a later round. A stomach then has to live on the snow alone. In rounds 48
  and 49 every line of pure stomachs died out. So the likely result is no lasting eater until
  the feeding changes (the mouth fed from a neighbourhood, D124's note). It is a
  world rule and a new realisation of every seed. It also takes income from every jointed plant,
  which round 42's reading found already pays for its joint with nothing.
- Keep it and separate the reading. That needs no build and no ruling. A body with a stomach
  and no leaf gets every watt of its light from a link. The feeding log carries each body's
  light and food, so `scripts/reads/linklight.py` splits the two for every such body. Over round
  49 (`logbook/specs/r49-read/linklight.txt`) links gave these bodies 14 to 65% of their income,
  by seed and window. Lines 48, 29 and 5854 took 31, 24 and 30% of theirs from light, and in seed
  3 at least four rows in five earned some.

Round 50's read (`logbook/specs/r50-read/linklight.txt`) changes the picture. In seed 1 a rigid
three-part stomach from the trickle, with no link, founded line 4292: 132 members, 68 living at
the peak at 9,600 s, the last death at 18,714 s, and not one watt of light. That is the first
line of pure eaters in rounds 48 to 50 to live on the snow alone for hours; round 48's best
pure-stomach lines, 45 and 24 living, lived on their founders' endowments. The eater lines with a
link still took 28% (seed 3's line 29) and 76% (seed 1's line 3726) of their income from it.

My recommendation is to keep the half through round 50, which is built on it, and to decide
between keeping it and 0 when round 50 is read. The same read will then say how much of each
eater line's income is the link's light. The question to answer is whether links keep catching light
at half a leaf's rate after round 50, or a later round sets it to 0.

### Whether the pool keeps its one-part stomach

The pool (D117) drops one of four stored stomach bodies as one trickle founder in ten. Index 0
is a one-part stomach. In round 48 it had its child on its endowment at landing, and two of its
lines reached 45 and 24 living before dying out. Under round 49's cap it has to earn the last
tenth of that child, and it did once in 63 landings. It lived a median 338 to 371 s, twice round
48's, and the flow into the cell where it lands does not feed it.

- Keep it, as the round's sentinel for a pure eater: the first round in which it founds a
  line is the round in which the snow can feed one. It costs the world a founder slot in about
  forty.
- Drop it, and let the pool carry the three bodies with a link. That changes the pool's make-up,
  so it is a new realisation of every seed. It also removes the one probe of whether the snow
  alone can feed a body.
- Keep it, and pair it with a change to how a mouth is fed in a later round (a neighbourhood
  rather than its own cell). That is the proposal D124's note already queues.

My recommendation is to keep it, as the sentinel, and to take the feeding change up as its own
proposal after round 50. The question to answer is whether the pool keeps the one-part stomach.

### Older items still open

These are carried from the archive, each unchanged since it was last put. Four are proposals at
the repository's root: the own-solver's rulings, the shelf reef and turbidity, predation on
contact, and reactive thrust. The others are the producer threshold under D063, a tempo dial for
later and the paywalled reading list. The old worktrees also wait on the owner, who has the
one-line command that removes them. The animal kit's proposal is absorbed in D106 and round 46's from D112
onward, and both files wait only to be deleted.

## Queued, in order

All of it is agent work. Long steps run in the background under the session, never in a
subagent and never in a shell loop. A queue that must outlive a turn is started detached
(CLAUDE.md).

1. Round 50 has run and is read. The owner said at 23:52 on 2026-09-26 to start it, and at 05:05 on
   2026-09-27 that the third seed could run whenever seed 2 ended. Seed 1 ended on its budget at
   02:52 after 178 minutes, seed 2 at 06:49 after 236 and seed 3 at 10:20 after 211, each at 16
   threads from `scratch/wt-leafincome` at `9ecff10`, not dirty, `configHash a2cda4b0`, with both
   books closed and no divergence. V2 passed at 10:27: the member check exited 0 and the resume from
   15,000 s was identical to seed 1 on all 100 samples. The full read is
   `logbook/specs/r50-read/read-end.txt` (`python scripts/reads/r50-read.py --logs-dir
   scratch/wt-leafincome/scratch/logs --v2-log scratch/logs/r50-v2.log`). Three clauses fail: W1
   (two snow founders in seed 2 a hair under their column's mean), M4 (in seed 3 the best
   stomach-bud line's stomach brings a median 5.4% of the income, against a bar of 2%) and S3 (seed
   1's 137 rigid stomach children died at a median 3,005 s, against a bar of 1,500). V1 waits for
   the film windows. Next come V1's windows from the round's own checkpoints, the theatre's pictures
   and the entry, 0124 (item 10).
1b. Round 51 is built next (D127 and D128, ruled 2026-09-27), every rule a knob off at its default:
    links catch no light (`EVOSIM_LINK_PHOTO 0`, an existing knob); the first link from the root
    holds no energy up to a typical link's volume (to be measured from round 50's bodies); a dead
    body is a husk that sinks faster than the snow, settles on the first reef top or the bed and
    stays, and dissolves slowly into its cell, eaten whole by a mouth that reaches it as a corpse is
    today; death by age is a Gompertz hazard stretched by a base gene that costs repair upkeep
    (genome format 10, the inocula converted by a text edit), with D038's wear off while it is on,
    and each birth and each destroyed part advance the body's ageing clock; the pool keeps its
    one-part stomach. Round 51 runs the base the same for every body and not mutating, and the round
    right after makes it heritable. The build also needs the husk's settled flag, first joules and
    body plan in the checkpoint (a `StateVersion` bump), husk rows in the record for pictures, the
    theatre's grey shrinking husk and the link's pink skin, stats for husks settled, eaten and deaths
    by age, a reader and the pre-registration (logbook/0125; 0124 is round 50's entry). The ledger
    check is `logbook/specs/r51-husk-ledger.md`. The hazard's knobs and the husk rates are screened
    on a fast-step seed for the plant crowd's plateau before the pre-registration, and a
    literature-review round checks D128's sources. Before the first night, the card check (item 3).
    The water's reactive thrust is built during round 51's week; whether it or heritable lifespan
    follows round 51 is decided on round 51's outcome (the owner, 2026-09-27).
2. The card is worked by day, with nothing else on the machine, from `scratch/wt-probe`
   (`gpu-probe`; merge main in before a day's work). The snow's transport runs on it under
   `EVOSIM_GPU_TRANSPORT` (above). Round 50's seeds are the next reference crowds once they have
   checkpoints. What is left, largest first. The snow's settling, remineralisation and mixing run
   just before the transport and take about 8 ms a step together. They could join it on the card,
   with one upload and one download for all four. The engine uploads the snow again for the
   senses, 3 to 6 ms a block. It could take the transport's result on the card instead, if nothing
   between the transport and the next block changes the snow (unchecked). The class uploads send a class
   whole when one body in it is new, and sending the changed rows would save 3 to 4% of the wall
   (an estimate). Identity on the card is claimed at two group sizes (CLAUDE.md). Whether a round
   runs on the card is the owner's: single precision is a new realisation of every seed. An
   overnight CPU round uses the card for the transport alone from round 51 (D126), after item
   3's check.
3. Before round 51, the card's transport is checked against round 50's own record (D126's
   conditions), by day with nothing else on the machine, on the first day after round 50 has run. Round 50's seeds are resumed on main's
   exe with `EVOSIM_GPU_TRANSPORT` from checkpoints near 5,000, 15,000 and 25,000 s, 1,000 s
   each, under `EVOSIM_ALLOW_SOURCE_MISMATCH`, since main is not round 50's build. Each window's
   rows, lineage and checkpoints are compared with round 50's own, the checkpoint payloads byte
   for byte. The same windows on the CPU's transport, alternated with them, are the control: a
   difference in both is the build's, and a difference in the card's alone is the card's. Their
   paces give the gain at round 50's crowds in place of the 13% estimate. Round 51's queue is
   `scripts/farm-queue.ps1 -GpuTransport` (the tools table). It logs each seed's `transport`
   and header engine words and the card's temperature, power and load at each seed's start and
   end. Both of its card faults were rehearsed on 2026-09-26 on round 50's world. A box world
   the card declines was relaunched on the CPU's transport. A seed stopped at 160 s was resumed
   from 170 s on the CPU's transport, and the two parts joined were the CPU-only control in 23
   samples, 432 lineage rows and the checkpoint payloads at 100 to 400 s
   (`scratch/farm-queue-test/`). The hourly watch adds one `nvidia-smi` reading. The check is
   `logbook/specs/card-transport-check/check.ps1`, which runs every window and then `compare.py`.
   It was rehearsed on `r50smoke-s1` from 4,000 s for 300 s on main's build: both windows were
   identical to the smoke's own rows, 456 lineage rows and three checkpoints, and the card's read
   4.99x against the CPU's 3.69x with the owner at the machine.
4. The CPU's physics at a full crowd is the next overnight lever
   (`logbook/specs/cpu-profile-2026-09-26.txt`). In round 49's seeds the physics was 62 to 70% of
   the wall and three quarters of each seed's last third. A sampling profile of round 49 seed 2 at
   25,000 s puts a quarter of the farm's time in the water at the links and a sixth in contact per
   part. Two changes that keep the bits are merged (`cpu-physics`): the reef fade computed once
   for the water's velocity and acceleration, and the contact grid's sort partitioned by owner.
   At 10,000 bodies they take the water phase from 104 to 87 ms a step and the grid from 52 to 30,
   7 to 10% of the step together, identical to round 49's record in sixteen runs. Two variants of
   the neighbour query bought nothing and were dropped. The velocity cannot be read off the
   acceleration's gradient, because the two evaluate the clock in different groupings. Round 50's
   check in the morning runs main's build, so it checks these at scale too. What is left of the
   bodies phase is the contact pass, the fluid, the brain and the solve.
5. The lit link is read and not built: `scripts/reads/linklight.py <arm>` splits a stomach
   body's income into its links' light and its food, by window and by line (the first decision
   above). It runs on round 50 when that round is read.
6. The storage reductions that lose nothing come next. A checkpoint that points at `genomes.jsonl.gz`
   instead of copying every living genome (a new `Checkpoint.Version`, with 6 still read), and
   the feeding log gzipped. Then a measured test of a better codec for the moving state.
7. The theatre's four checkpoints are re-recorded on the current build: ckA, ckB, ckC and ckUi.
8. Seed 2's budded line is read for the stomach's cost. M4 failed in round 49's seed 2 with
   the stomach bringing 2.7% of the income. What the bud costs in tissue and upkeep is not yet
   set against it (0122).
9. Round 49's video is made when the owner asks for it, from film windows from the round's own
   checkpoints, which V1 showed are faithful. After the owner approves it as final, the keep
   list for its checkpoints is built from its scene table and shown before each thinning.
10. Round 50's read and entry (0124) follow its last seed, with the theatre's pictures taken
   before it is written.

The rounds after 50 are not planned past the two decisions above. The proposal most likely to
come next is the feeding change: a mouth fed from a neighbourhood, or intake by the water
passing the mouth. It goes to the owner as a proposal file before anything is built.

## How the experiments are run

CLAUDE.md holds the commands and the gotchas. This is where each tool sits.

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
| the Unity farm | idle since round 42; its workers and caps are CLAUDE.md's, for the theatre only |
