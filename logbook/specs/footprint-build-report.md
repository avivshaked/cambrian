# Build report: the footprint world (D077)

Implementing agent's hand-back for `logbook/specs/footprint-spec.md`. Everything in the spec's scope
is built. Two deviations from the spec's literal wording are recorded in **What I could not do
as written** below, both with the measurement that forced them; one of them is a world rule and
is the owner's to confirm or overturn.

## Commits

On `main`, not pushed, no history rewrite, no `--no-verify`.

| commit | what | files |
|---|---|---|
| `686561e` | *The ocean gets a top and a bottom that push back (D077 rule 4)* — `FluidConfig.SurfaceRestoringFraction`, `FluidEnvironment.Restore` and the branch at the buoyancy clamp | 2 (+127 / −1) |
| `8690a92` | *The footprint world: one box, four regions, a wrap, and a place beside the parent (D077)* — rules 1, 2, 3, 5, 6, 7 and the instruments | 13 (+2262 / −36) |

`686561e` was verified green on its own: **521 Core tests, 0 failed**, with the rest of the
build stashed. The two untracked `sol-gpt-*-review.md` files in the working tree are not mine
and were left alone.

## The two new tunables

Both reach `RunConfig.Hash()` and `config.json` through `ConfigSchema`'s reflection walk, so
`RunConfigTests` and `RunConfigJsonTests` cover them without a line of new test code.

| tunable | group | default | env var | header token |
|---|---|---|---|---|
| `RunConfig.SharedSpace` | `world` | `false` | `EVOSIM_SHARED_SPACE` | `space …` |
| `FluidConfig.SurfaceRestoringFraction` | `fluid` | `0` | `EVOSIM_SURFACE_RESTORE` | `surface restore …` |

Adding them moves the configHash for every configuration: the r24 settings go from
`6b0097f75e13e5db` to `a521eff52dc063fd`. That also makes every `config.json` written before
this build unreadable by it (§9's refuse-rather-than-default) — see the theatre note below.

## The header token, exactly as rendered

Appended after `· area <n> m2`, both rendered unconditionally (D065's rule: "tiled" is an
answer, not an absence). From `runs/fp-smoke.md` and `runs/fp-tiled.md`:

```
 · space shared 4x10x10 m, depth 60, wrap · surface restore 1
 · space tiled 100 m · surface restore 1
```

`4x10x10` is K × W × W with W read from `NutrientField.PatchWidthMetres` — the world's own
`sqrt(area / K)`, not recomputed in the report — so the header cannot describe a box the
simulation does not have. `depth 60` is `WorldDepthMetres`.

The footer gained one line, also unconditional:

```
Shared space: on · wraps 156719 · crowded stillbirths 67264 · contact pairs per physics step 1223.0678
Shared space: off · wraps 0 · crowded stillbirths 0 · contact pairs per physics step —
```

## The columns

Appended after `**food rig**`, and nothing already written moved:

```
above | wraps | crowded | contacts | p0 | p1 | p2 | p3
```

`above` is living roots at y > 0 (counted in the tiled world too — it reads and does not act).
`below` reuses the existing `below world`. `wraps` and `crowded` are **per window**, the
`floor` / `mat blk` shape. `contacts` is the mean `Physics.ContactEvent` pair count per physics
step over the window, and an **em-dash** in a tiled world — the instrument is off there, and a
0 would read as "nothing touched" (CLAUDE.md's species-column trap). `p0`..`p(K−1)` is alive
per patch, one column per patch, **for every run including K = 1**, so the table's width is a
function of exactly one setting. This is the first column set whose count depends on the
config; `analyse-arm.ps1` reads by name and is unaffected.

`stats.jsonl` gains `sharedSpace`, `aboveSurface`, `wraps`, `wrapsWindow`, `crowded`,
`crowdedWindow`, `contactPairs`, `contactPairsPerStep` and an `alivePerPatch` array.
`run.json`'s end block gains `sharedSpace`, `wraps`, `crowded`, `contactPairsPerStep`, and the
error path carries the same from the last metabolic step.

## The two things I judged

**The founder sink constant is `FluidConfig.TissueExcessDensity`, unscaled by D064.** It is the
existing constant that produces the sink rate (D048 calibrated it against the observable: 0.1
kg/m³ → 0.0089 m/s), and it is 0.02 in the reference world. Deliberately *not* multiplied by
`BuoyancyModel.ExcessDensityFactor`: `NeutralBodyVolume` is 0.25 m³ in the reference world and
makes most bodies exactly neutral, so a restoring force scaled the same way would be zero for
precisely the bodies that most need bringing back — the surface would stay a ratchet for
everything under the neutral volume. The restoring density is therefore
`SurfaceRestoringFraction × TissueExcessDensity`: the world's rule about its own boundary, not
a property of the body at it.

**The spatial hash cell is `max(1 m, 2 × the largest bounding radius in the world)`**, chosen
once per metabolic step when the hash is first queried. Two of the largest radius means a query
for a body no bigger than that reaches every sphere that could touch it within one cell in each
direction; the 1 m floor stops a world of tiny bodies building a hash with millions of cells
across a 60 m box. A query whose own radius exceeds the stored maximum widens the search
(`ceil((r + rmax) / cell)`) rather than being wrong. Measured: **1.984 m** with 200 real
founders in the box; 1 m in an empty box. Buckets are keyed by the standard
`ix·73856093 ^ iy·19349663 ^ iz·83492791` hash, so a collision costs a distance test that would
have been made anyway and can never give a wrong answer.

## Validation

### 1. `./scripts/core-test.ps1` — **527 passed, 0 failed** (1 m 4 s)

Up from 521. The six new ones are `SharedSpaceTests`: both knobs off by default and both
changing the hash (and surviving `FluidConfig.Clone()`); shared space retiring both transport
lotteries **without drawing from the RNG stream** (a world with dispersal 0.5 and rolls on is
step-for-step identical to one with both off, which only holds if neither lottery draws); the
same pair differing with the box off, so the test above cannot pass for the wrong reason; a
crowded birth costing its parent nothing and being counted; a refused conception leaving D074's
matter identity exact; and a patch being read from the body rather than inherited.

### 2. Replay identity at the default — **PASS, 120 of 120 rows**

`rounds/launch-r24.ps1`'s settings, seed 2, **12,000 s at dt 0.01** (1.2 M physics steps),
against a pre-change run of the same launched and completed before a line of source was touched.

| | `fp-base2` (pre-change) | `fp-replay2` (this build) |
|---|---|---|
| commit | `2de27cb` clean | `2de27cb` dirty (this build, pre-commit) |
| simHash | `995cda59…` | `11cbfd73…` |
| configHash | `6b0097f75e13e5db` | `a521eff52dc063fd` (two new tunables) |
| columns | 58 | 66 |
| physics steps | 1,200,000 | 1,200,000 |
| births | **1,639** | **1,639** |
| fastest creature ever | **0.5052 m/s at t=1588.5 s** | **0.5052 m/s at t=1588.5 s** |
| drag / drive impulses limited | 0 / 0 | 0 / 0 |

```
$ python scripts/reads/compare-rows.py runs/fp-base2.md runs/fp-replay2.md
baseline columns: 58   new columns: 66   appended: ['above', 'wraps', 'crowded', 'contacts', 'p0', 'p1', 'p2', 'p3']
baseline rows: 120   new rows: 120
rows compared: 120   differing: 0
```

This was run **twice**: once on the first build (`757391c4…`) and again on the final build
after the two fixes below (`11cbfd73…`). Both are 0 differing. A shorter 2,000-s pre-change run
(`fp-base`, on an LF Core checkout) also matches `fp-base2`'s first 20 rows exactly, which
disposes of the CRLF/LF difference `git checkout` introduced while the baseline was being made.

### 3a. Milestone 1 smoke — **PASS**, unchanged

`Evosim.Sim.EditorTools.Milestone1Smoke.Run` against `unity/`, exit 0: *"12 creatures built,
geometry verified, actuated and torn down"*, all 7 sensor channels varying.

### 3b. The new shared-space smoke — **PASS**

`Evosim.Sim.EditorTools.SharedSpaceSmoke.Run` (menu: `Evosim/Shared Space Smoke`), exit 0.
Three parts:

**1. The restoring boundary's arithmetic**, 17 cases on the pure `FluidEnvironment.Restore`:
sign above and below, magnitude `f × excess`, continuity with D050's clamp at y = 0 exactly, a
body already coming back left alone at both faces, half strength at half the fraction, and the
neutral-body cases below. At fraction 0 every case returns exactly what D050's clamp returns.

**2. The box.** Ring length 40 m; the patch of x = 0 / 9.99 / 10 / 39.99 / 40 / −1 (0 / 0 / 1 /
3 / 0 / 3); the wrap at each face (x = 41 → 1, x = −1 → 39, z = 12 → 2, depth preserved, a body
inside untouched); the minimum-image distance across each seam (39.5 → 0.5 reads 1 m, not 39);
and **200 real founders placed with 0 overlapping pairs**, 9 rejected attempts (0.05 per body),
hash cell 1 m in the empty box.

**3. Two hundred bodies in it**, 4 × 10 × 10 × 60 m, 2,000 physics steps at dt 0.01, twenty of
them pushed 0.25 m out through the top and the bottom at step 200:

```
- 200 alive after 2000 steps at dt 0.01, 213 floor spawns, 6 births, 0 crowded stillbirths, 0 diverged
- box 4 x 10 x 10 m, depth 60, hash cell 1.984 m, 10 placement rejections
- wraps 2, contact pairs 4045 (2.0225 per physics step)
- displaced at step 200: 10 to y=+0.25, 10 to y=-60.25; back inside by step 2000: 10 and 3
- centre-of-mass census at the end: 0 above the surface, 15 below the floor
- furthest any root was seen past a horizontal face, any step: 0.000006 m
- ok   bodies loose outside the box, any step = 0
- ok   of those, back inside from above = 10
- ok   everything pushed below is held at the floor: worst 0.1778 m out, bound 0.5
- ok   everything pushed out ended nearer the world: worst is now 0.1778 m out, from 0.25
- ok   `above` as the report reads it = 0
- ok   diverged = 0 · contacts reported: 4045 · non-finite parts = 0
```

Two of those numbers are not the assertion the spec asked for, and both are the rule behaving
rather than failing:

- **6 µm, not 0, is the furthest a root was ever seen past a face.** A body reads one step
  stale on the step it wraps — `TeleportRoot` writes the root pose into PhysX and the managed
  `Transform` catches up at the next solver writeback — so a check made immediately after
  `Ecosystem.Step` sees the pre-wrap position exactly once per crossing. One such reading in two
  thousand steps, matching the run's one wrap, at 0.000006 m. The bar is 5 cm.
- **3 of 10 pushed below are fully back, not 10.** The two boundaries are not symmetric, and
  the reason is inertia rather than the rule: above the surface both a buoyant body (restored)
  and a heavy one (its own weight) are pushed the same way, so a body pushed up returns and
  stays — 10 of 10. Below the floor the sign flips *at* the boundary, so −D is an equilibrium a
  body oscillates about, damped only by §5.2's drag. What is asserted there is that the body is
  *held* at the floor (worst 0.178 m, bound 0.5) and has moved back (0.178 m from 0.25 m), which
  is what the rule promises.

### 3c. The theatre — **10 of 10 samples identical**

`Evosim.Theatre.EditorTools.TheatreIdentityCheck.Run` against a fresh 1,000-s recording made by
this build (`runs/th-ref2`, seed 1, dt 0.01): *"t=1000 s, alive 44, identity: 10 of 10 samples
match, 44.3x real time"*, exit 0.

**The spec's `runs/th-ref` replay is not possible on this build and cannot be made possible.**
CLAUDE.md's own gotcha — "adding a tunable makes every older `config.json` unreadable by the new
build" — fires exactly here, and I ran it to record the refusal rather than assert it:

```
[Theatre] refused: Missing required field 'surfaceRestoringFraction'. Present: addedMassCoefficient,
density, dragCoefficient, neutralBodyVolume, panelsPerAxis, tissueExcessDensity.
```

The fresh recording is the substitute, and it exercises the same path.

### 4. `fp-smoke` — the 3,000-s screening arm, dt 0.02, seed 2, shared

`rounds/launch-fp.ps1 -Shared 1`: the r24 settings plus `EVOSIM_SHARED_SPACE 1`,
`EVOSIM_SURFACE_RESTORE 1`, `EVOSIM_AREA 400`, `EVOSIM_DT 0.02`. Ended `budget reached`;
configHash `45405543faba0b31`, simHash `11cbfd73…`.

| t | alive | depth m | audit | diverged | above | below world | wraps | crowded | contacts | p0 | p1 | p2 | p3 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 100 | 40 | −28.6 | 0.0000% | 0 | 0 | 0 | 12 | 0 | 0 | 7 | 9 | 14 | 10 |
| 700 | 202 | −1.2 | 0.0000% | 0 | 75 | 0 | 699 | 171 | 111.6 | 68 | 127 | 4 | 3 |
| 1300 | 697 | 0.2 | 0.0000% | 0 | 525 | 0 | 1524 | 1082 | 455.5 | 253 | 435 | 5 | 4 |
| 1900 | 1333 | 0.1 | 0.0000% | 0 | 1018 | 0 | 7478 | 1171 | 1490.6 | 494 | 821 | 2 | 16 |
| 2500 | 1867 | 0.0 | 0.0000% | 0 | 1526 | 0 | 11382 | 5931 | 2523.0 | 669 | 1150 | 0 | 48 |
| 3000 | 2245 | 0.1 | 0.0000% | 0 | 1634 | 0 | 12009 | 5204 | 3378.8 | 701 | 1514 | 0 | 30 |

Totals: 150,000 physics steps, 2,271 births, **156,719 wraps**, **67,264 crowded stillbirths**,
**1,223 contact pairs per physics step** over the run, fastest creature 0.7463 m/s.

Against the spec's checklist:

- **audit 0.0000% every row — PASS.** The energy audit closes with a crowd in it.
- **`diverged` 0 — PASS.**
- **the header tokens present — PASS** (above).
- **`crowded` reported — PASS**, and it is the loudest number in the run: 67,264 refusals
  against 2,271 births.
- **patches all populated by t = 3,000 — FAIL.** p2 reads 0 from t ≈ 2,400 onward and p3 is
  thin; p0 and p1 hold 701 and 1,514. The vent is in patch 0.
- **`above`/`below` 0 after t = 500 — `below` PASS (0 at every row), `above` FAIL:** 1,634 of
  2,245 at t = 3,000. See below — this is a dose, and it is the owner's.
- **wall clock against the same settings tiled — see below.**

### The wall clock, shared against tiled

`rounds/launch-fp.ps1 -Shared 0` with byte-identical settings otherwise (`fp-tiled`,
configHash `11325668e4f1dcc6`), run alone on the machine immediately after `fp-smoke`, also run
alone:

| | `fp-smoke` (shared) | `fp-tiled` (tiled) |
|---|---|---|
| wall clock | **16.1 min** | **3.4 min** |
| × real time | 3.1 | 14.7 |
| alive at t = 3,000 | 2,245 | 1,099 |
| births | 2,271 | 1,434 |
| mean depth at t = 3,000 | +0.1 m | −13.4 m |
| contact pairs / step (mean) | 1,223 | — (instrument off) |
| audit | 0.0000% | 0.0000% |

**4.7× is not a cost ratio for shared space, and reading it as one would be a mistake.** The
two are different worlds — the shared one ends with twice the population and sitting 13 m
higher — and the dominant term is contacts: 3,379 pairs per physics step at the end, against a
spike prediction of 0.39 at 0.17 bodies/m³ (logbook/0064). The reason is not the box, it is
where the population went: 2,245 bodies in a surface film about a metre thick over 400 m² is
~5 bodies/m³, thirty times the densest cell the spike measured. If the film question is settled
the density falls with it. The wrap itself is one `Transform` read per creature per physics
step; the census and placement are once per metabolic step, which is one fiftieth of that.

## What I could not do as written, and why

**1. `runs/th-ref` cannot be replayed by this build.** Recorded above with the exact refusal;
a fresh recording was substituted. Not fixable without abandoning §9's refuse-rather-than-default
rule, which is not mine to abandon.

**2. The restoring boundary acts on a body with exactly zero net density, where the spec says
"negative".** This is a world rule and therefore the owner's; I changed it because the spec's
literal wording defeats the rule it implements, and I have the measurement.

The spec says *"a body at y > 0 with negative net density gets `netDensity = +f ×
founderSinkExcess`"*. D077 itself says *"above y = 0 and below y = −D a body is restored at no
more than the founder sink rate"*, unconditionally. The two differ for one case, and D064's
`NeutralBodyVolume = 0.25` in the reference world makes that case most of the population: a
body under the neutral volume has `netDensity` **exactly 0**, so a strict `< 0` test leaves it
untouched above the waterline — carried over the top by the vent's plume, coasting to a stop,
and staying there forever, which is precisely the ratchet logbook/0061 found.

Measured, on the same arm, same seed, same settings, changing only this:

| | strict (`< 0`) | not-already-sinking (`<= 0`) |
|---|---|---|
| `above` at t = 3,000 | **2,055 of 2,227** | **1,634 of 2,245** |
| mean depth at t = 3,000 | **+0.8 m** | **+0.1 m** |
| `crowded` (total) | 28,793 | 67,264 |

(The first is `runs/fp-smoke-prefix.md`, kept.) I implemented `<= 0` above the surface and
`>= 0` below the floor. At y = 0 exactly the result is still D050's clamp, so continuity holds.

**It is still not enough, and that is a dose rather than a bug.** The rule caps the restoring
force at the founder sink rate — about 1.8 mm/s at `TissueExcessDensity` 0.02 — while D067's
vent pushes bodies up at 50 mm/s, twenty-eight times harder. The plume delivers bodies to the
film faster than the boundary can return them, so 73% of the population is still above the
waterline at t = 3,000. Closing it wants a lever the spec explicitly puts out of scope (the
vent's transport, `VentCapMetres` from `logbook/specs/surface-spec.md` Option B, a restoring fraction
above 1, or a smaller `EVOSIM_VENT`). **⚠ And it is read at dt 0.02, which CLAUDE.md says is
bimodal on depth and cannot stand in for the 0.01 world — three of six fast-step worlds sat in
the surface film at −1 m where their 0.01 seeds sat at −12 to −15 m (logbook/0056). So this is a
suspicion strong enough to act on, not a result.** The tiled arm at the same step and settings
sits at −13.4 m with 260 above, which is the comparison that makes the plume the suspect rather
than the box.

**3. Two faults the first screening arm found, both fixed and both re-validated.**

- **A wrap is not a swim.** `Ecosystem.Metabolise` differences a body's position across a
  metabolic step, and a body translated at a seam moves 40 m in one step of it: the first
  `fp-smoke` reported a *fastest creature ever* of **82.4576 m/s** (40 m / 0.5 s to three
  figures) in a world whose fastest animal has managed 0.5. `mean m/s`, `max m/s`, `spd jnt` and
  `spd rig` were all corrupted. Fixed with the minimum-image convention
  (`SharedVolume.ShortestDistance`), applied only when there is a box — the tiled expression is
  unchanged to the character. After the fix the same arm reads **0.7463 m/s**.
- **The neutral-body case above.**

**4. Founders and inoculants that cannot be placed.** The spec names the crowded stillbirth for
newborns only. I extended it: a floor founder or an inoculant that exhausts the same 64-attempt
budget is also counted in `crowded` and refused, and the attempt still counts against
`FloorSpawns` / `Inoculated` — matching `EnforceFloor`'s existing rule that a stillborn founder
is still an attempt, so the floor's trickle stays a trickle and does not retry until something
fits (which would be the floor packing the world rather than sampling it). Not exercised in
anger: 0 crowded founders in the smoke, and the arm's 67,264 are all offspring.

**5. Rule 7's drawing is verified by compilation and by reading, not by a picture.**
`WaterBounds.ShowBox` and `TheatreRunner`'s branch into it compile and are covered by the
theatre identity check running through the same file, but `OnRenderObject` does not run in
`-batchmode -nographics`, so nobody has looked at the box. It wants one human glance in the
Editor.

## Housekeeping

- Nothing was written outside the repository. Unity logs are in `scratch/logs/`; the arms are in
  `runs/` (gitignored); `rounds/launch-fp.ps1` is the arm launcher and `scratch/edit_cs.py` is
  the byte-preserving editor the source edits were made with (it exists because `git`'s
  `autocrlf` and a careless writer will silently change a file's line endings and therefore its
  `simHash`, for no reason anybody can see).
- Runs kept: `fp-base` / `fp-base2` (pre-change baselines), `fp-replay` / `fp-replay2`
  (identity), `th-ref2` (theatre), `fp-smoke` (shared), `fp-smoke-prefix` (the strict-`<`
  measurement), `fp-tiled` (wall clock).
- Worker 6 carries `simHash 11cbfd73235c3d64e3aeab39a4557671282d8024cce84c3029ec60dba3ca2022`,
  which is this build; pass it to `run-arm.ps1 -ExpectSimHash` for the screen.
- Nothing is running on the machine.

---

# Addendum, 2026-09-05: rule 4's strength was the spec's error

The owner ruled on the measurement above: the restoring boundary was too weak by three orders
of magnitude, and the honest physics is that **above the waterline there is no water, so a body
loses its buoyant support entirely and feels its full weight**. Rule 4 was rewritten to that,
and everything in §Validation was re-run.

## The commit

| commit | what | files |
|---|---|---|
| `78cb35c` | *Out of the water a body feels its whole weight (D077 rule 4, owner ruling 2026-09-05)* | 3 (+145 / -83) |

On `main`, not pushed. `simHash` is now
`c27c23c0aa3b0b9ea8942969177effc152f5954010996f41914261ff6810a720`; `coreHash`
`092eabbd79f4abec56696d7cecd4f1356c3c898b1ce193ca13c5c7169b0f482d`. **No tunable was added,
renamed or removed** — `SurfaceRestoringFraction` keeps its name, its group, its default of 0
and its `EVOSIM_SURFACE_RESTORE`, and the r24 configHash is still `a521eff52dc063fd`. What
changed is what the fraction is a fraction *of*.

## The rule

`FluidEnvironment.Apply` computes a per-call restoring density; that one line is the change:

```csharp
float restoringDensity = restoringFraction * PhenotypeBuilder.DensityKgPerM3;   // was * excessDensity
```

The buoyancy force is `-netDensity x (mass / DensityKgPerM3) x g`, so passing the water's own
density back in recovers **mass x g exactly**. A fraction of 1 is a body falling at g; 0.5 is
half its weight; 0 is D050's clamp, character for character. Drag is untouched, so the fall is
damped and reaches a terminal speed of order a metre a second rather than free fall.

`Restore` became monotone rather than a pair of returns:

```csharp
if (heightY > 0f) return Mathf.Max(netDensity, restoringDensity);
if (netDensity <= 0f && heightY == 0f) return 0f;
if (heightY < -worldDepthMetres) return Mathf.Min(netDensity, -restoringDensity);
return netDensity;
```

**`Max`/`Min` rather than the literal `<=` branch, and that is a judgement I should name.** The
instruction was to keep the `<=` treatment for neutral bodies; a literal reading keeps the old
branch shape and applies full weight only when `netDensity <= 0`. That leaves a discontinuity
the size of the whole change: a body at `+0.001` kg/m3 above the surface would fall five hundred
times slower than the exactly-neutral body beside it, because its own excess density happened to
be positive. A body above the line is out of the water whatever its density, and air offers less
upthrust than water and never more — so the boundary may *raise* a downward force and may never
reduce one. `Max` says that in one expression and subsumes the `<=` case (`max(0, R) = R`).
`y = 0` exactly is untouched: still D050's clamp.

## Validation, re-run

### 1. `./scripts/core-test.ps1` — **527 passed, 0 failed** (1 m 3 s)

### 2. Replay identity at the default — **PASS, 120 of 120 rows, 0 differing**

`fp-replay3`: `launch-r24.ps1 -Seed 2 -Worker 6 -Seconds 12000`, dt 0.01, 1,200,000 physics
steps, configHash `a521eff52dc063fd`, on `78cb35c`. Compared two ways:

| baseline | that build | columns compared | rows | differing |
|---|---|---|---|---|
| `fp-replay2` | the pre-ruling D077 build | **all 66** | 120 | **0** |
| `fp-base2` | pre-D077 entirely | 58 (8 appended) | 120 | **0** |

Births 1,639 in all three. Fastest creature 0.5052173733711243 m/s at t = 1,588.5 s in all
three, to the last digit. **The default is still the historical record, bit for bit** — which
is the check that matters, because the new `restoringDensity` local is computed on every call
and the whole claim is that it never reaches the arithmetic when the fraction is 0.

### 3. `SharedSpaceSmoke` — **PASS** (exit 0), against the main `unity/` project on the committed tree

Part 1 grew from 17 cases to 19, and its magnitude is now `weight = 1000` rather than an excess
density. The four new cases are the monotonicity ones, and they are where the judgement above is
actually asserted:

```
- ok   on,  heavy above the line     -> +f x weight, not its excess — got 1000, want 1000
- ok   on,  heavier than its weight  -> unchanged (never reduced) — got 2000, want 2000
- ok   on,  buoyant under the floor  -> -f x weight, not its lift — got -1000, want -1000
- ok   on,  lighter than its weight  -> unchanged (never reduced) — got -2000, want -2000
```

**The return-from-above numbers the ruling asked for**, Part 3, 200 bodies, 2,000 steps at
dt 0.01, 10 pushed 0.25 m above the surface and 10 pushed 0.25 m below the floor at step 200:

| | before (sink-rate rule) | after (full weight) |
|---|---|---|
| back inside from **above** | 10 of 10 | **10 of 10** |
| back inside from **below** | 3 of 10 | **10 of 10** |
| worst excursion left past a face | 0.1778 m (below) | **0 m** |
| centre-of-mass census at the end | — | **0 above the surface, 0 below the floor** |
| furthest a root was ever seen past a horizontal face | 0.000006 m | **0.000006 m** |
| `above` as the report reads it | 0 | **0** |
| wraps / contact pairs | 2 / 4045 | 2 / 4057 |
| diverged / non-finite parts | 0 / 0 | **0 / 0** |

The floor half is the visible difference. It used to be a soft equilibrium a body oscillated
about, and three of ten displaced bodies were still under it at step 2,000. It is now stiff
enough that all ten are back and none is outside. `FloorBandMetres` (0.5 m) is kept as a bound
on a body caught mid-crossing, and its remark now says the floor is a bouncer rather than a bed.

### 4. `fp-smoke2` — the same 3,000-s arm, same settings, same seed

`rounds/launch-fp.ps1 -Shared 1 -Worker 6 -Name fp-smoke2`. configHash `45405543faba0b31` —
**identical to the original `fp-smoke`**, so only the code differs.

| t | alive | depth m | audit | diverged | above | below world | wraps | crowded | contacts | p0 | p1 | p2 | p3 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 100 | 40 | -28.7 | 0.0000% | 0 | 0 | 0 | 13 | 0 | 0 | 6 | 10 | 14 | 10 |
| 700 | 186 | -2.2 | 0.0000% | 0 | 0 | 0 | 1261 | 404 | 165.0 | 56 | 123 | 4 | 3 |
| 1300 | 658 | -4.1 | 0.0000% | 0 | 0 | 0 | 7724 | 1156 | 914.4 | 282 | 366 | 7 | 3 |
| 1900 | 1148 | -8.1 | 0.0000% | 0 | 0 | 0 | 16097 | 2421 | 1612.4 | 403 | 665 | 79 | 1 |
| 2500 | 1394 | -8.6 | 0.0000% | 0 | 0 | 0 | 15048 | 2917 | 2081.1 | 474 | 798 | 122 | 0 |
| 3000 | 1550 | -6.4 | 0.0000% | 0 | 0 | 0 | 14208 | 6424 | 2501.1 | 467 | 1016 | 66 | 1 |

Totals: 150,000 physics steps, 2,036 births, **273,166 wraps**, **45,941 crowded stillbirths**,
**1,160.8 contact pairs per physics step**, 753 drag impulses limited, 0 drive impulses limited,
`status ended / budget`.

**Do the plume-lifted neutral bodies now fall back? Yes.** `above` is **0 at every one of the
31 samples but three**, where it reads **1** (t = 2,400, 2,600, 2,800) — one creature, caught
crossing. Mean `above` over the last 1,000 s is **0.27 of 1,385 alive**. `below world` is 0
throughout. The vent still lifts at 50 mm/s; the boundary now answers at about 1 m/s, twenty
times harder rather than twenty-eight times weaker, and the ratchet is closed.

**Where the population sits.** Mean height over the last 1,000 s (11 samples, t >= 2,000):

| arm | rule | mean height, last 1,000 s | mean `above`, last 1,000 s |
|---|---|---|---|
| `fp-smoke` | sink rate | **-0.02 m** | 1,464 of ~2,000 |
| **`fp-smoke2`** | **full weight** | **-8.03 m** | **0.27 of 1,385** |
| `fp-tiled` | sink rate, no box | -13.76 m | 189 of ~1,100 |

The shared world has left the surface film and settled about 8 m down, between the film it was
stuck in and the tiled world's -13.8 m. **⚠ Still dt 0.02**, which CLAUDE.md says is bimodal on
depth and the film and cannot stand in for the 0.01 world, so this is a strong suspicion and not
a result. What it does establish is that the boundary is no longer the binding constraint.

Against the spec's checklist, with the two failures from the first pass:

- **audit 0.0000% every row — PASS.**
- **`diverged` 0 — PASS.**
- **`above` / `below` 0 after t = 500 — PASS.** `below` 0 at every sample; `above` 0 at every
  sample but three, each reading 1. Previously 1,634 of 2,245.
- **patches all populated by t = 3,000 — still FAIL, but differently.** p2 recovered (0 before;
  122 at t = 2,500, 66 at the end) and p3 is now the thin one, at 1. p0 467, p1 1,016. The
  imbalance is the vent's rather than the box's — four regions on a ring with one vent in patch 0
  is what D077 asked for, and the world has not had time to fill.

### The wall clock

| | `fp-smoke` (old rule) | **`fp-smoke2`** | `fp-tiled` (tiled) |
|---|---|---|---|
| wall clock | 16.1 min | **12.2 min** | 3.4 min |
| x real time | 3.1 | **4.09** | 14.7 |
| alive at t = 3,000 | 2,245 | **1,550** | 1,099 |
| births | 2,271 | **2,036** | 1,434 |
| contact pairs / step | 1,223 | **1,161** | — |

Shared against tiled is now **3.6x** rather than 4.7x, on a world with 1.4x the tiled
population. The cost of the box did not change; the crowd did, a little, because the population
is spread over 8 m of depth instead of stacked in a metre of film.

## Two things to flag

**1. D077 rule 4's text in DECISIONS.md is now superseded, and that entry is the owner's.** It
reads *"above y = 0 and below y = -D a body is restored at no more than the founder sink rate"*.
The code no longer does that, and the code is right. I have not edited DECISIONS.md — world
rules and their amendments are owner-reserved and the file is append-only — so it wants an entry
recording the 2026-09-05 ruling and marking that clause superseded. `FluidConfig` and
`FluidEnvironment` both carry the reasoning in their remarks in the meantime.

**2. The fastest creature in `fp-smoke2` is a floor bounce, not a swim: 1.0338 m/s at
t = 6.5 s.** Founders spawn at `EVOSIM_FOUNDER_DEPTH 60` in a 60 m world — on the floor — and a
body that drifts a hair under -D now gets its whole weight upward. `max m/s` is 0.12-0.22 at
every sample from t = 100 on, so it is confined to the founding seconds, but **`bestSpeed` in a
run's manifest is no longer a locomotion measurement in the first few seconds of a shared
world.** This is the placeholder floor showing through, and it is the concrete reason the
remarks now say so in both files: a real sea bed is a collider that stops a body, not a spring
that returns it with the momentum it arrived with. Founding a metre off the floor, or a real
floor collider, would both remove it; neither is mine to choose.

## Housekeeping, updated

- Runs added: `fp-replay3` (identity, 120/120) and `fp-smoke2` (the re-screen). The originals
  are kept for the comparison: `fp-smoke` (sink-rate rule), `fp-smoke-prefix` (the strict-`<`
  measurement), `fp-tiled`, `fp-base2`, `fp-replay2`, `th-ref2`.
- **Worker 6 now carries `simHash c27c23c0aa3b0b9ea8942969177effc152f5954010996f41914261ff6810a720`**;
  pass it to `run-arm.ps1 -ExpectSimHash` for the screen. The earlier `11cbfd73...` is the
  pre-ruling build.
- The theatre was **not** re-run. `78cb35c` touches no serialization and adds no field, so
  `th-ref2` should replay under it unchanged — but I did not run it, and I am not claiming I did.
- Nothing was written outside the repository. Nothing is running on the machine.
