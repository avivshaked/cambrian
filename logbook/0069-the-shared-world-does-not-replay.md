# 0069 — The shared world does not replay

*2026-09-06. An instrument note, written while round 27 (logbook/0068) runs. Not a round;
nothing here is scored. Agent work throughout, no world rule touched.*

## What was noticed

Round 27's seed 4 (`r27-s4`) and the diagnostic that justified the round (`r26d-s4`) are
the same world: same seed, same build (`simHash 1f5455f4…`, `coreHash 52eb6496…`), same
`config.json` (hash `ca86fa92`), same worker copy (`unity-w5`), differing only in the
seconds budget (30,000 against 20,000), which is not a tunable. The record says two such
runs are one run: PhysX replays bit for bit on this machine (logbook/0052, `r16dt-01c` ≡
`-01d` ≡ `-01e`), and every build since has been validated by replay identity. Yet at
t=25,900 `r27-s4` read zero stomachs where `r26d-s4` had held a stable clade of 195 to
20,000. The rows were compared: **identical through t=1,400, different from t=1,500** in
forty fields at once, the differences at the fifth significant figure and growing — the
signature of one changed bit amplified by chaos, not of a changed rule.

## The probes

`scratch/launch-det.ps1`: 0068's world, seed 4, dt 0.01, 3,000 s, run on `unity-w7` one at
a time (5.4 wall-minutes each). `scripts/compare-det.py` reports the first stats sample at
which two arms differ. Every pair is the same inputs on the same build on the same worker.

| pair | physics settings | identical through | first difference |
|---|---|---|---|
| `r26d-s4` / `r27-s4` | the round's (enhanced determinism off) | 1,400 | 1,500 |
| `det0-a` / `r26d-s4` | the round's | 1,400 | 1,500 |
| `det0-a` / `det0-b` | the round's | 1,500 | 1,600 |
| `det1-a` / `det1-b` | **enhanced determinism on** | 1,500 | 1,600 |
| `det1-a` / `det0-a` | on against off | 1,500 | 1,600 |
| `det2-a` / `det2-b` | on, **sleep threshold 0, sweep-and-prune broadphase, scratch buffer 64** | 1,300 | 1,400 |

Six runs of one world, six realisations. Unity's *Enable Enhanced Determinism* — the
setting whose documented purpose is to make an island's simulation independent of the
other islands in the scene — changes nothing, and neither does removing sleep, changing
the broadphase or enlarging PhysX's scratch buffer (the three settings together moved the
trajectory from t=500, as they should, and the pair still parted). The divergence is not
random in time: every pair holds for 140,000–150,000 physics steps, through a thousand
seconds of creature–creature contact (from t≈500; 47 pairs per step by 1,400), and parts in
the same two hundred seconds — when the population is about 110 and the newborn crowd
begins (`crowded` per window 8 → 54 between 1,300 and 1,400).

**What the tiled world's replays covered.** `r16dt-01c/d/e`, `fp-replay2/3` and
`fl-replay` are all tiled: creatures 100 m apart on one layer, never touching, every body
its own solver island. The shared world was validated for *zero divergences* on a 20,000-s
run (0066's floor addendum) and never for identity against a second run of itself. The
guarantee was never tested where it now fails.

## The audit

A read-only audit of the shared-space path (`SharedVolume`, `SeaFloor`, `Ecosystem`,
`FluidEnvironment`, `CreatureInstance`, `PhenotypeBuilder`, `EvolutionRun`, the placement
seam in `World`) found no nondeterminism in the C#: no clocks, no random source outside
`Rng`, no physics queries, no iteration over reference-keyed or string-keyed hashes
(`_bodies` and `_departed` are keyed by `long`), one `Parallel.For` whose iterations write
disjoint slots and which the tiled path shares, the one unstable sort given a total order,
`DestroyImmediate` in batch mode so actor removal happens at a defined point, and a contact
handler that only counts (`Interlocked.Add` on two longs; nothing in the simulation reads
them). What it named as amplifiers — one RNG draw per placement attempt, the per-step sort
of parents by surplus, the hard branches at the waterline, a crowded refusal turning a
birth into a stillbirth — turn one changed bit into a different world within a metabolic
step, which is why the first *visible* difference is forty fields at once.

## The digest

The stats rows are sampled every 100 s and cannot say which step or which body parted
first, so an instrument was built for it (`scratch/digest-spec.md`, report
`scratch/digest-build-report.md`): `EVOSIM_DIGEST_EVERY N` writes one FNV-1a hash of every
living body's pose and velocities, in `World.Living` order, every N physics steps to
`digest.jsonl`; `EVOSIM_DIGEST_DUMP_STEPS` writes every body's thirteen floats at named
steps; `scripts/digest-diff.py` finds the first differing step, body and value. Off by
default; with it off the tiled replay `fl-replay2` matches `fl-replay` on every field, and
with it on (`fl-replay-dg2`) it still does — the instrument reads and does not act. Every
100 steps it is free; every step costs 5% of wall time. The build is `simHash 2c964296…`
(a `.cs` under `Assets/Evosim` changed; `coreHash` unchanged, round 27's workers untouched).

**Where the runs part.** `det3-a` / `det3-b` (digest every step): identical through step
147,777 and different at **147,778 (t=1,477.78 s)**, in **one creature (id 38) and only in
its velocities** — root `vel.x` −0.07242366 against −0.0724236444, one to two ulp — with
every position and rotation in the world still bit-identical. It spreads by contact: three
bodies at the next step, four by 147,785, all mutual neighbours 0.4–0.6 m apart in one
crowded pocket. All six digested runs share the hash `9c176deb07b9664b` at step 147,700
and fall into four states by 147,800 — a fork with few branches, not noise.

**With one job-system worker** (`-job-worker-count 1`, `det4-a` / `det4-b`; the log
confirms `JobSystem: Creating JobQueue using job-worker-count value 1`): the pair holds
longer — identical through step 184,600 — and still parts, at 184,700 (t=1,847 s). And
`det4-a` matches `det3-a` (fifteen workers) through step 151,000, past the step at which
`det3-a` and `det3-b` had already parted: whatever the fork is, the number of threads
changes how often it is reached, not whether.

**With no job-system workers** (`-job-worker-count 0`, `det5-a` / `det5-b`; the log
confirms `job-worker-count value 0`): **identical over all 300,000 steps** — every digest
hash and every one of the 30 stats samples, to t=3,000, with 593 contact pairs per step at
the end. The shared world replays when the physics step runs on one thread.

## Reading

The cause is Unity's threading of the physics step, not the project's code and not a
setting Unity exposes in its physics panel. PhysX's own documentation says the result does
not depend on the number of worker threads; in this build, with articulated bodies in
contact, it does — one to two ulp in one body's velocity at a fork that arrives whenever
touching bodies are solved in a different order, more often with more threads (fifteen
workers: ~148,000 steps; one: ~185,000; none: never in 300,000). The tiled world never
reached the fork because no two creatures ever shared an island. So the record's replay
guarantee (logbook/0052) was a property of the tiled world, and every shared-world run to
date — 0065, 0066, 0067, 0068's round 27 and its diagnostic — is one realisation of its
seed, not reproducible from `(genome, seed, configHash)`. Nothing in how the rounds were
*read* changes: a seed was already one draw, compared across seeds, and the fast-step
butterfly rule (CLAUDE.md) already said as much. What changes is what a replay *is*: the
theatre's World mode re-simulates from the seed, and on a shared-world run its identity
check will pass to t≈1,500 and then watch a cousin — until the physics runs single-threaded
there too.

**The cost**, on this world at 120–180 bodies: 6.42 wall-minutes for 3,000 s against 5.39
with fifteen workers (+19%) and 5.78 with one. The drag loop, 56% of the step (0064), is
the project's own `Parallel.For` on .NET threads and is untouched; only the solver's third
serialises. A 10,000-s pair (`det6-a` / `det6-b`, to ~1,000 bodies) measures it at scale
and confirms identity over a longer run; results below.
