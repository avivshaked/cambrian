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
the card is worked by day with nothing else on the machine. The total load stays at or under
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

### Round 50 is built and waits for tonight

The owner ruled option (a) on the morning of 2026-09-26 (D125). A leaf founder is set in the cell of its column where its own income, light and
matter together, is largest. The price is the call the world bills a body with. A stomach is placed
as before. The build is `leaf-income-depth` (`scratch/wt-leafincome`); the launcher is
`rounds/env-r50.ps1` (`EVOSIM_FOUNDERS_INCOME_DEPTH 1`). Its pre-registration is logbook/0123.
The launch is in the queue below.

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
should save more at round 48's (not measured).

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

1. Round 50 launches this evening. The checks and the pre-registration are in 0123's
   "Before the launch". The queue is `scratch/r50-launch/seeds.ps1`, started detached from the
   committed, clean `scratch/wt-leafincome` at about 19:00. It runs seeds 1, 2 and 3 one at a
   time at 16 threads with a 600-minute wall each, then V2 on seed 1 (`v2.ps1`, logged to
   `scratch/logs/r50-v2.log`). Round 49's three seeds took 196, 318 and 235 minutes, so the
   queue should end by about 08:00. The watch runs from the session's schedule every hour,
   never from a shell loop: `python scripts/watch-round.py r50 --seeds 1,2,3 --read
   scripts/reads/r50-read.py`, run from the main tree. The full read at the end adds
   `--windows-root` (V1) and `--v2-log scratch/logs/r50-v2.log` (V2).
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
   runs on the card is the owner's: single precision is a new realisation of every seed. So is
   whether a CPU round may use the card for the transport alone, which gives the CPU's bits.
3. The lit link is read and not built: `scripts/reads/linklight.py <arm>` splits a stomach
   body's income into its links' light and its food, by window and by line (the first decision
   above). It runs on round 50 when that round is read.
4. The storage reductions that lose nothing come next. A checkpoint that points at `genomes.jsonl.gz`
   instead of copying every living genome (a new `Checkpoint.Version`, with 6 still read), and
   the feeding log gzipped. Then a measured test of a better codec for the moving state.
5. The theatre's four checkpoints are re-recorded on the current build: ckA, ckB, ckC and ckUi.
6. Seed 2's budded line is read for the stomach's cost. M4 failed in round 49's seed 2 with
   the stomach bringing 2.7% of the income. What the bud costs in tissue and upkeep is not yet
   set against it (0122).
7. Round 49's video is made when the owner asks for it, from film windows from the round's own
   checkpoints, which V1 showed are faithful. After the owner approves it as final, the keep
   list for its checkpoints is built from its scene table and shown before each thinning.
8. Round 50's read and entry (0124) follow its last seed, with the theatre's pictures taken
   before it is written.

The rounds after 50 are not planned past the two decisions above. The proposal most likely to
come next is the feeding change: a mouth fed from a neighbourhood, or intake by the water
passing the mouth. It goes to the owner as a proposal file before anything is built.

## How the experiments are run

CLAUDE.md holds the commands and the gotchas. This is where each tool sits.

| | |
|---|---|
| the farm | `scripts/run-farm.ps1 <arm> -Launcher rounds/env-rNN.ps1 -Seed N -Seconds S -Threads 16`, run from the tree the round belongs to, since the farm hashes the source above its working directory; `-ResumeFrom <run> -At <s>` continues a run from a checkpoint; stopped by `stop-arm.ps1`, which writes a `STOP` file |
| launching | a round's queue script under `scratch/rNN-launch/`, started detached, refusing a dirty tree; the pre-registration's record is the manifest's `gitCommit` on a clean tree |
| reading | the round's reader `scripts/reads/rNN-read.py` (every clause, per seed, with a held line per clause); the entry's own reads under `scripts/reads/rNN-entry/`, their outputs under `logbook/specs/rNN-read/`; `scripts/analyse-arm.ps1` by column name, never positionally |
| checking | `Evosim.Farm.exe --verify-checkpoint <ckpt> <s> <out> <threads>` for a checkpoint member by member; `scripts/compare-det.py` for a resume against its run; a film window's identity rows for V1 |
| pictures | `scripts/theatre-snap.ps1 <arm> -From snapshot -At <s>` for a still from the record; `scripts/theatre-film.ps1` from a checkpoint for a clip; every render checked on a sheet against a reference before the owner sees it |
| tests | `scripts/core-test.ps1` (the default set; `-All` before a change to the world), and `dotnet test` on `Evosim.Farm.Tests` and `Evosim.Dynamics.Tests`; the fixtures are `src/Evosim.Core.Tests/fixtures/r42-config.json` and the crowd named in `RunFixture.cs`, re-recorded on every build that adds a tunable |
| the card | ILGPU under `src/Evosim.Farm.Gpu`, worked by day with nothing else on the machine; kernels regenerated before a build; `EVOSIM_GPU_TRANSPORT` puts the snow's transport on it (`GpuTransport`, `GpuTransportTests`); `src/Evosim.UnityProxy` compiles the Farm package as the Editor would |
| the Unity farm | idle since round 42; its workers and caps are CLAUDE.md's, for the theatre only |
