# Build report: the state digest

*Builder's report against `logbook/specs/digest-spec.md`, 2026-09-06. Nothing committed, nothing
pushed. Everything below ran on `unity-w7`; workers 2–6 were left alone with round 27 on them
and were still running it at the end.*

## Headline

The instrument works, and it has already answered the question it was built for. Six shared
runs of seed 4 on one worker and one build are **bit-identical for 147,777 physics steps**
and then split. At the first differing step the difference is **one creature** — id 38, a
two-part body — and **only its velocities**: every position and rotation in the world, id 38's
included, is still identical to the bit. One step later the difference has reached two of its
neighbours, and by step 147,800 four bodies, all in the same crowded pocket around
(10.0, −1.3, 2.6) m.

## What changed

| File | Change |
|---|---|
| `unity/Assets/Evosim/Sim/Ecosystem.cs` | +231 lines. The digest itself: `EnableDigest`, `Digest`, `DumpBodies`, `ReadPartState`, `PartStateJson`, `Fnv1a`, one gated call in `Step()` after `Steps++`, and the writers closed in `DestroyAll`. |
| `unity/Assets/Evosim/Sim/Editor/EvolutionRun.cs` | +66 lines. `EVOSIM_DIGEST_EVERY` and `EVOSIM_DIGEST_DUMP_STEPS` read beside the wall budget, `EnvSteps` to parse the comma list, and the call to `EnableDigest` beside `DivergenceDumpDirectory`. |
| `scripts/digest-diff.py` | New. |
| `rounds/launch-det.ps1` | `-DigestEvery` and `-DigestDump` pass-throughs. ASCII only, no BOM, as it was. |

**Nothing in `src/Evosim.Core` was touched.** That was deliberate: a Core edit reaches every
worker launched after it (CLAUDE.md), and round 27 is live on five of them. The `coreHash`
is `52eb6496…5521` before and after, the same value `fl-replay` and `det0-a` recorded.

**New `simHash`: `2c964296a2ba893c652b31c58fe33d42a9a6ab625c69ad492c9f126b4eee40fc`**
(was `1f5455f4851591d00ccf0e8115976cc6caf798efbf8f993d469b00ed9a3c41f4`). Expected: two `.cs`
files under `Assets/Evosim` changed. Worker 7 was refreshed with
`pwsh -NoProfile -Command "./scripts/new-worker.ps1 -Workers @(7)"` and every later launch
carried `-ExpectSimHash 2c964296…`, so no run in this report could have come off a stale tree.
`unity-w7/ProjectSettings/DynamicsManager.asset` is now byte-identical to the main tree's, as
the spec asked — the enhanced-determinism probe settings are gone.

## What it writes

`runs/<arm>/<run>/digest.jsonl`, one row per digested step, flushed each row:

```
{"step":1,"t":0.0099999997764825821,"bodies":0,"hash":"cbf29ce484222325","first":-1}
{"step":100,"t":0.99999997764825821,"bodies":2,"hash":"0a5a7c321dc63186","first":0}
```

`bodies` is the number of living creatures hashed and `first` the id of the one at the head of
`World.Living`, or −1 in an empty world. `t` is `Steps * (double)FixedDt`, so it is the float
`0.01f` widened — ugly to read, identical across runs, which is all a diff needs.

`runs/<arm>/<run>/digest-bodies.jsonl` at the requested steps, one row per living creature:

```
{"step":147778,"id":38,"root":[13 numbers],"links":[[13 numbers]],"sleeping":false,"contacts":0}
```

## Deviations from the spec, and why

1. **Thirteen floats per part, not ten.** The spec's item 1 enumerates position (3), rotation
   (4), linear velocity (3) and angular velocity (3) — thirteen — and then item 2 calls it
   "10 floats as above". The enumeration is what is implemented. Dropping any of the four
   quantities would make the digest blind to a divergence that surfaces first in the dropped
   one, and this run's divergence surfaces **first and only in the velocities**, so a
   pose-only digest would have missed the birth of it by a step. `digest-diff.py`'s component
   names (`pos.x` … `ang.z`) match the thirteen.
2. **The pose is read through the Transform, the velocities through the `ArticulationBody`.**
   The spec says read everything through the `ArticulationBody`; Unity exposes no world pose on
   `ArticulationBody` — there is no `position`/`rotation` property, only `transform`. This is
   the same read `Ecosystem.CheckFinite`, `WrapAtTheSeams` and the divergence dump already
   make. `Physics.Simulate` writes the transform back on every step regardless of
   `Physics.autoSyncTransforms`, and reading a Transform syncs nothing, so the value is the
   solver's own and the read has no side effect.
3. **The digest sits after `Fluid.Settle` and the drivers' `Settle`, not immediately after
   `Physics.Simulate`.** Neither `Settle` writes to a body — both only read velocities to
   integrate work — so it is the same state the solver left, with `Steps` already advanced so
   the row can name its own step. It is before `Metabolise()`, so a digest row is always
   pre-economy.
4. **`contacts` is always 0.** PhysX reports contacts to a scene-wide callback as pairs, not
   per body; there is no cheap per-body count. The spec allows 0.
5. **A dump step that is not a multiple of `EVOSIM_DIGEST_EVERY` still gets a digest row**, so
   that "at each listed step, after the digest row" is true whatever list is passed.
6. **Non-finite components in `digest-bodies.jsonl` are written as quoted strings** (`"NaN"`),
   the divergence dump's convention. `Json.Writer` refuses them, correctly, and a dump that
   throws while recording a divergence records nothing.
7. **`run.json` was left alone.** The digest settings are not recorded in the manifest. The
   spec forbade the header and `config.json`; the manifest was not mentioned, and adding a
   field would change a shape several scripts read. The launch prints a `digest: every N …`
   line to the Unity log instead (`scratch/logs/evosim-<arm>.log`).

`EVOSIM_DIGEST_DUMP_STEPS` set with `EVOSIM_DIGEST_EVERY` unset logs a warning rather than
running silently, and an unparseable entry in the list throws — §9's refuse-rather-than-default
applied where the cost of defaulting is a run with no dump at the one step it was launched for.

## Validation

### The Core suite

```powershell
./scripts/core-test.ps1                 # Passed! Failed: 0, Passed: 530, Total: 530, 1 m 12 s
./scripts/core-test.ps1 -Filter RunConfig   # Passed! Failed: 0, Passed: 27, Total: 27, 168 ms
```

`RunConfigTests` and `RunConfigJsonTests` are green, as they must be: `EVOSIM_DIGEST_EVERY` is
a runner setting read in `EvolutionRun`, not a `RunConfig` tunable, so it reaches neither
`Hash()` nor `config.json`.

### The gate is off, and the world is unchanged

```powershell
pwsh -NoProfile -Command "./rounds/launch-r24.ps1 -Seed 2 -Worker 7 -Seconds 1000 -Name fl-replay2"
```

`fl-replay2` (worker 7, new build, digest off) against `fl-replay` (worker 6, old build,
2,000 s): **10 shared samples, 84 fields each, 0 differing**. `configHash a521eff52dc063fd` on
both, and the run directory name carries it. No `digest.jsonl` was written.

### The gate is on, and the world is *still* unchanged

Not in the spec, and worth the 32 seconds: the shared world cannot show that the instrument
reads without acting, because it does not replay in the first place. The tiled world can.

```powershell
$env:EVOSIM_DIGEST_EVERY = '100'
./rounds/launch-r24.ps1 -Seed 2 -Worker 7 -Seconds 1000 -Name fl-replay-dg2 -ExpectSimHash 2c964296…
```

`fl-replay-dg2` against `fl-replay`: **10 shared samples, 0 differing**, with 1,001 digest rows
written. The digest reads and does not act.

*(`runs/fl-replay-dg` is a discard — a first attempt where `$env:EVOSIM_DIGEST_EVERY=100` was
interpolated away by the outer shell inside a nested `pwsh -Command "…"`, so the run is simply
a third digest-off replay. Its stats are identical to `fl-replay`'s too. Left in place rather
than deleted; it is not evidence of anything except the quoting trap.)*

### Two shared runs, digest every 100 steps

`det3-a` then `det3-b`, one after the other, never together:

```powershell
pwsh -NoProfile -Command "./rounds/launch-det.ps1 -Seed 4 -Worker 7 -Seconds 3000 -Name det3-a -DigestEvery 100 -ExpectSimHash 2c964296…"
pwsh -NoProfile -Command "./rounds/launch-det.ps1 -Seed 4 -Worker 7 -Seconds 3000 -Name det3-b -DigestEvery 100 -ExpectSimHash 2c964296…"
python3 scripts/digest-diff.py det3-a det3-b
```

```
det3-a vs det3-b: identical through step 147700 (t=1477.0 s);
                  first differing step 147800, t=1478.0 s
    det3-a: hash 387ca2df342a4164, bodies 117, first id 0
    det3-b: hash bdd926282ab2d395, bodies 117, first id 0
```

`configHash ca86fa92bc5eb3fa` — the same hash `det0-a` recorded, and the report header of
`det3-a` is byte-identical to `det0-a`'s line 3. `config.json` and the stats row's 84 field
names are unchanged.

### Two more with the per-body dump

`det3-c` and `det3-d`, both `-DigestEvery 100 -DigestDump '147700,147800'`:

```
det3-c vs det3-d: identical through step 147700; first differing step 147800
bodies at step 147700: 117 rows each — every creature identical at this step
bodies at step 147800: 117 rows each
    first differing creature: id 38 (row 1), 23 differing values
      root pos.z: 2.51915359 vs 2.519154
      root rot.x: 0.5953373 vs 0.595337152
      …
      root vel.x: -0.07357148 vs -0.07357175
      link 1 ang.z: 0.0516291969 vs 0.051629886
```

**The 100-step cadence is repeatable across all four runs.** Every one of the six pairings
gives step 147,700 as the last identical digest and 147,800 as the first differing one, except
`det3-a` vs `det3-c`, which are the *same* realisation through 151,000 and split at 151,100.
The world at 147,700 hashes `9c176deb07b9664b` in all four; at 147,800 there are three distinct
states — `387ca2df342a4164` (a and c), `bdd926282ab2d395` (b), `da7b8619015b605f` (d).

Across all **six** shared runs the 147,700 hash is `9c176deb07b9664b` without exception, and at
147,800 there are four states over six runs: `387ca2df342a4164` (a, c, e), `bdd926282ab2d395`
(b), `da7b8619015b605f` (d), `c7dc45a3228c44cf` (f). So the outcome is drawn from a small
handful of realisations rather than being freshly random each time — `det3-e` parts from
`det3-f` at 147,778 and still arrives at exactly the state `det3-a` and `det3-c` are in
twenty-two steps later.

### The exact step (beyond the spec)

Given how repeatable the window was, the 100-step cadence was worth trading for the real
answer. `det3-e` and `det3-f`, `-DigestEvery 1` with the per-body dump at every step from
147,701 to 147,800 — 300,000 digest rows and 11,700 body rows per run, for about 5% of wall
clock:

```
det3-e vs det3-f: identical through step 147777 (t=1477.77 s);
                  first differing step 147778, t=1477.78 s
bodies at step 147777: 117 rows each — every creature identical at this step
bodies at step 147778: 117 rows each
    first differing creature: id 38 (row 1), 12 differing values
      root vel.x:  -0.07242366   vs -0.0724236444
      root vel.y:  -0.00047555432 vs -0.0004757172
      root vel.z:   0.055993747  vs 0.0559937
      root ang.x:   0.0608344562 vs 0.0608348735
      root ang.y:   0.05711331   vs 0.0571130551
      root ang.z:   0.0423932858 vs 0.04239398
      link 1 vel.x: -0.0643715858 vs -0.0643718839
      link 1 vel.y: -0.0144578246 vs -0.0144546488
      link 1 vel.z:  0.0542480946 vs 0.0542490259
      link 1 ang.x:  0.06310518   vs 0.06308582
      link 1 ang.y:  0.03067972   vs 0.0306786038
      link 1 ang.z:  0.03809317   vs 0.0380884521
```

**First differing step: 147,778 (t = 1,477.78 s). First differing creature: id 38, at row 1 of
`World.Living`. First differing value: `root vel.x`, −0.07242366 against −0.0724236444 — a
relative difference of about 2 × 10⁻⁸, which is one to two ulp of a float.**

Four facts about that step, all read off the dump:

- **Only one creature differs.** 116 of the 117 are identical to the bit.
- **Only velocities differ.** All twelve differing values are linear or angular velocity; every
  position and rotation in the world, id 38's included, is still identical. (Consistent with
  the rounding: at |pos| ≈ 10 m a float resolves about 10⁻⁶ m, and a 10⁻⁸ m/s velocity
  difference moves a body 10⁻¹⁰ m in a step. The pose difference exists and is below the last
  bit until it accumulates — `link 1 pos.x` first differs one step later, at 147,779.)
- **It spreads by contact.** 1 creature differs at 147,778; 3 at 147,779 (38, 130, 176); 4 by
  147,785 (adding 216) and still 4 at 147,800. Their root positions at 147,777 are
  (9.98, −1.04, 2.51), (9.98, −1.19, 3.05), (9.94, −1.49, 2.79) and (10.05, −1.59, 2.24) —
  one crowded pocket, mutual near-neighbours at 0.4–0.6 m in a world running 160 contact pairs
  a step.
- **Id 38 is not special otherwise.** Two parts, awake, unremarkable position.

I read this as: the nondeterminism is born inside PhysX's own velocity solve for one contact
island, not in anything `Evosim.Sim` hands it — the C# inputs at 147,777 (positions, rotations,
velocities, and therefore every sensor, brain and drive value derived from them) are identical
across the two runs. That is consistent with the read-only C# audit the spec cites having found
nothing. It is a claim about where to look next, not a proof: the digest records the solver's
output, so it cannot by itself distinguish "PhysX is nondeterministic here" from "something
outside the digest's thirteen numbers — a drive torque, a drag impulse — was already different
and the digest cannot see it". The natural next probe is one more field per part in the dump:
the driver's `AppliedTorque(b)` and the fluid's pending force, both of which are already
computed and both of which would settle that question at step 147,778.

### The digest's own cost

| Run | Digest | Wall min | Births | Alive at end |
|---|---|---|---|---|
| `det0-a` (2026-09-06, old build) | off | **5.39** | 419 | 407 |
| `det3-a` | every 100 | **5.31** | 423 | 413 |
| `det3-b` | every 100 | **5.40** | 415 | 407 |
| `det3-c` | every 100 + 2 dump steps | **5.09** | 418 | 410 |
| `det3-d` | every 100 + 2 dump steps | **5.09** | 432 | 422 |
| `det3-e` | **every step** + 100 dump steps | **5.66** | 416 | 405 |
| `det3-f` | **every step** + 100 dump steps | **5.63** | 426 | 417 |

Every-100 is free — inside the spread of the no-digest runs, and the run-to-run spread here is
itself an artefact of the machine's other five arms. Every-step costs about 5%. File sizes:
`digest.jsonl` 261 KB at every-100 over 3,000 s, 26 MB at every step; `digest-bodies.jsonl`
57 KB for two dump steps, 2.9 MB for a hundred.

## Runs left on disk

`runs/fl-replay2`, `fl-replay-dg`, `fl-replay-dg2` (tiled, 1,000 s each) and `runs/det3-a` …
`det3-f` (shared, 3,000 s each). All ended `status ended`, `reason budget`, `divergedTotal 0`.
Nothing was stopped, and `stop-arm.ps1` was not needed.

## What I could not do

- **Prove the digest does not perturb the *shared* world.** It cannot be proved there — that
  world does not replay, which is the whole reason this exists. The tiled proof
  (`fl-replay-dg2`) is the strongest available, and it is exact.
- **Read a per-body contact count.** No cheap query exists; `contacts` is 0 everywhere.
- **Say whether the first divergence is PhysX's or something upstream of it.** See the note
  above — it needs one more field in the dump, not more runs.
