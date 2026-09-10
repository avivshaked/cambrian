# Build report: a real sea bed (`logbook/specs/floor-spec.md`)

Implementing agent's hand-back. Everything in the spec's scope is built. Two of the spec's
literal numbers were changed and both are recorded in **What I could not do as written**,
with the measurement that forced them; neither is a world rule. One measurement in the
record turns out to be **mis-attributed**, and that one *is* the owner's — see
**The founding bounce was never the floor**.

## The commit

On `main`, not pushed, no history rewrite, no `--no-verify`.

| commit | what | files |
|---|---|---|
| `a268311` | *A real sea bed: the floor under the box is a collider, not a spring* | 12 (+795 / −62) |

One commit rather than two: the Core half (`IBodyPlacement`) and the Sim half
(`SharedVolume`) are the same change seen from two sides, and Core alone would not compile
against the Unity project.

**No tunable was added, renamed or removed.** The r25q configuration's `configHash` is still
`162996716da49619` and the r24 configuration's is still `a521eff52dc063fd`, so every
`config.json` this build can read, the previous build could read, and the identity check
below compares like with like.

| | value |
|---|---|
| `simHash` (this build) | `1f5455f4851591d00ccf0e8115976cc6caf798efbf8f993d469b00ed9a3c41f4` |
| `coreHash` (this build) | `52eb64962a26a8b81572fa1088bb62fbfd7a541fbb1580e202bafacb1f275521` |
| `simHash` (previous build) | `c27c23c0aa3b0b9ea8942969177effc152f5954010996f41914261ff6810a720` |

Worker 6 carries this build; pass the `simHash` to `run-arm.ps1 -ExpectSimHash`. The two
untracked `sol-gpt-*-review.md` files in the working tree are not mine and were left alone.

## What the floor is

`unity/Assets/Evosim/Sim/SeaFloor.cs`, built by `Ecosystem`'s constructor when
`RunConfig.SharedSpace` is on and destroyed by `DestroyAll`. Null in a tiled world, which
has no shared coordinates to put a collider at.

| | |
|---|---|
| **collider** | one static `BoxCollider` — no `Rigidbody`, no `ArticulationBody`, so PhysX treats it as immovable scenery |
| **extent** | `(K·W + 2m) × T × (W + 2m)`; at the screen's settings **50 × 2 × 20 m**, measured in the smoke |
| **top face** | y = −`WorldDepthMetres` **exactly** — the centre sits at −D − T/2, so the water's depth is unchanged and every field, light curve and layer index reads the world it always read |
| **seam margin** | `SeamMarginMetres` = **5 m** past each horizontal face. A root is wrapped when it leaves the ring but its limbs follow a step later, so a bed that stopped at the seam would be a hole exactly where a body is mid-crossing. Five metres is several times the largest bounding radius this world has produced (0.63 m mean over founders, logbook/0064) and costs nothing — it is one static box either way |
| **thickness** | `ThicknessMetres` = **2 m**. Nothing here moves more than a centimetre per step, so tunnelling is three orders of magnitude away; and a body somehow inside it is nearer the top face than the bottom, so depenetration pushes it *up* |
| **layer** | `PhenotypeBuilder.CreatureLayer` = **8**, the creatures' own. The collision matrix is untouched, so the bed collides with exactly what creatures collide with. ⚠ That ties it to self-collision: `FluidEnvironment.ConfigureScene(selfCollision: false)` ignores layer 8 against layer 8 and would switch the bed off with it. Every harness that has a box passes `true`; the coupling is written down in `SeaFloor`'s remarks |
| **material** | `sharedMaterial` left **null** — Unity's built-in default, friction 0.6/0.6 and **bounciness 0**. `ProjectSettings/DynamicsManager.asset` has `m_DefaultMaterial: {fileID: 0}`, so nothing overrides it. Bounce is the whole thing this replaces; a bed that returned momentum would be the mirror again, harder to see |
| **contacts** | `providesContacts = true`, and its pairs are counted apart from creature–creature pairs |

**The restoring mirror below −D is retired where there is a bed.**
`FluidEnvironment.FloorIsSolid` is set by `Ecosystem` when the floor exists and reaches
`FluidEnvironment.Restore` as `floorRestores: false`; the surface half is untouched.
`FluidConfig.SurfaceRestoringFraction`'s remark now says the fraction governs the surface
only where there is a bed. A tiled world keeps the mirror unchanged — it has nowhere to put
a collider, and that branch had to stay today's expression to the character.

## How placement keeps bodies above it

`IBodyPlacement`'s two reserve methods now take `heightY` **by `ref`**, with a one-way
contract written into the interface: an implementation may **raise** it and may never lower
it, and whatever comes back is the height the creature is *admitted* at. That last clause is
the one that matters — the body is built at the reserved position and the economy charges
the admitted height, and if the two disagreed a creature would eat the light and the matter
of a layer its body is not in.

`SharedVolume` raises a placement to `−D + boundingRadius + SeaFloor.ClearanceMetres`
(5 cm), so the whole bounding sphere clears the rock:

- **founders and inoculants** — the draw is `−Rng.Range(0, FounderDepthSpread)`, and the
  reference world sets `EVOSIM_FOUNDER_DEPTH 60` in a 60 m world, so the deep end of the
  lottery lands *inside* the bed. Clamped rather than redrawn: a redraw would change the
  depth distribution founding is calibrated against (§5A.2), where a clamp moves only the
  handful of bodies that were about to be buried. Measured in the smoke: **2 of 200**
  founders drawn inside the rock and raised, deepest bounding sphere then reaching −59.95 m.
- **newborns** — the child is placed on a horizontal ring about its parent at
  `max(parent's y, the floor's minimum for the child's own radius)`. A parent lying on the
  bed therefore breeds **beside** itself and not below itself, which is the case the three
  `r25q-s2` divergences were newborns of; and a child larger than its parent cannot be put
  half inside the rock its parent is resting on.
- Core's own `World` passes `ref` through all three paths and admits the height that comes
  back. `Inoculate` uses a per-copy local, so one raised inoculant does not move the cohort.

## Contacts: `floor con`

`Ecosystem.ContactPairs` is now creature–creature pairs **only**; `Ecosystem.FloorContactPairs`
is pairs against the bed. Split per *pair*, not per header: a static collider has no
`Rigidbody` or `ArticulationBody` to be named by, so `ContactPairHeader.bodyInstanceID`
cannot be trusted to identify the floor, while `ContactPair.colliderEntityId` /
`otherColliderEntityId` name the colliders themselves and the bed definitely has one. The
cost is one comparison per contact pair per step.

- **report column** `floor con`, appended after `contacts` (an em-dash where there is no bed,
  for `contacts`' own reason — a 0 would read as "nothing touched")
- **`stats.jsonl`** gains `floorContactPairs` and `floorContactPairsPerStep`
- **header token** `space shared 4x10x10 m, depth 60, wrap, bed` (or `no bed`), read off the
  `Ecosystem` rather than off the config, so a header cannot say "bed" on the day one stops
  being built
- **footer** `… · sea bed collider at -60 m · floor pairs per physics step 0.0114`

⚠ `floor con` sits *before* the per-patch `p0..pK-1` columns, because those are appended for
every run after `BaseColumns`. A positional reader of the table breaks; CLAUDE.md already
forbids one, and `analyse-arm.ps1` reads by name.

## The theatre

`WaterBounds` fills the bed as a quad (`GL.QUADS`, drawn before the lines so the grid and
the seams read on top of it) instead of drawing it as a wire grid, and the material is now
two-sided so a camera under the world sees rock rather than a hole. What is filled is the
box's own footprint — the collider's 5 m overhang is an implementation margin, and drawing
it would put sea bed where there is no water. **Not looked at by a human:** `OnRenderObject`
does not run in `-batchmode -nographics`, so this is verified by compilation and by reading,
exactly as rule 7's drawing was in the footprint build. It wants one glance in the Editor.

---

# Validation

## 1. Core tests — **527 passed, 1 failed**, and the failure is data rather than code

`./scripts/core-test.ps1`, 528 tests, 1 m 5 s. The suite gained one test
(`NobodyIsAdmittedBelowTheHeightThePlacerHandedBack`) and `SharedSpaceTests`' stub placer
gained the height contract, so `IBodyPlacement`'s change is covered from Core's side: the
stub raises to a bed at −40 m, and no living creature is admitted below it.

**The failure is `SnapshotJoinTests.SnapshotIdsJoinLineage`, and it is not mine.** That test
reads `runs/` and no code at all — it walks every run directory joining snapshot ids to
`lineage.jsonl` birth rows. It fails on `runs/r25-s2/2026-09-05-230338-45405543`, whose
`run.json` reads `status ended / reason wall`: 38 ids on the last snapshot (t=19,827) appear
in no birth row, because `lineage.jsonl` is buffered and the run was cut off by its wall
clock before the tail was flushed. The test's own remarks say it skips a snapshot that is
past the end of the lineage "for a run still in flight"; the skip does not catch a
wall-ended run. The footprint build recorded 527/0 on 2026-09-05 before round 25 ran, and
those run directories were written afterwards. **I have not fixed it** — it is an instrument
gap about run data, outside this spec, and `runs/` is gitignored.

While writing the new test I hit and fixed a trap worth recording: the stub's
`MinimumHeightY` first defaulted to `0`, which is a bed at the waterline, which raised every
founder into the brightest water in the world and turned `ACrowdedBirthCostsItsParentNothingAndIsCounted`
into a §5A.7 photosynthetic mat (`PopulationRunawayException` at 415 creatures). It defaults
to negative infinity now, with the reason beside it.

## 2. Replay identity at the default (tiled) — **PASS, 20 of 20 rows, 0 differing cells**

`runs/fl-replay`: `rounds/launch-r24.ps1 -Seed 2 -Worker 6 -Seconds 2000`, **dt 0.01**,
200,000 physics steps, configHash `a521eff52dc063fd`, on `a268311` clean.

| baseline | what it is | columns compared | rows | differing |
|---|---|---|---|---|
| `fp-replay3` | the D077 build after the 2026-09-05 ruling | **all 66** | 20 | **0** |
| `fp-base2` | pre-D077 entirely | 58 | 20 | **0** |

The header line is **character-for-character identical** to `fp-replay3`'s, and the run's own
footer reads *"Fastest creature seen at any point: 0.5052 m/s, at t=1588.5 s"* — the same
figure `fp-base2`, `fp-replay2` and `fp-replay3` all report. Drag and drive impulses limited:
0 / 0. 45 births at t=2,000 in both.

`scripts/reads/compare-rows.py` could not do this comparison, and the reason is worth having:
it assumes the new report's columns are the baseline's plus a suffix, and `floor con` lands
*inside* the table (before the per-patch columns). `scripts/reads/compare-rows-by-name.py` matches
columns by name instead, which is CLAUDE.md's own rule for reading a report applied to the
identity check.

## 3. `SharedSpaceSmoke` — **PASS**, exit 0

`Evosim.Sim.EditorTools.SharedSpaceSmoke.Run` against the main `unity/` project on the
committed tree (`scratch/logs/floor-smoke.log`). Part 1 grew from 19 cases to 24; parts 2 and
3 grew the bed's own assertions.

**Part 1 — the boundary's arithmetic**, 24 cases, all ok. The five new ones are the retirement:
with `floorRestores: false` a heavy, a buoyant and a neutral body below the floor are all
returned *unchanged* (the rock holds them), while above the line and at the line the answers
are exactly what they were. That pair is the claim — the bottom is gone, the top is untouched.

**Part 2 — the box and the bed**:

```
- placed 200 of 200 founders, 0 refused for want of room, 9 rejected attempts (0.05 per body), hash cell 1 m
- bed at y=-60 m, BoxCollider 50 x 2 x 20 m, layer 8, material project default, providesContacts True
- 2 of 200 founders were drawn inside the rock and raised clear; deepest bounding sphere now reaches -59.95 m
- ok   overlapping pairs = 0
- ok   founders whose sphere reaches into the bed = 0
- ok   founders placed at a depth other than the one reported = 0
- ok   the clamp was exercised: 2 raised
```

The last line is there so the line above it cannot pass for the wrong reason: "nothing is in
the bed" is also true of a world where the bed was never consulted.

**Part 3 — two hundred bodies in it**, 2,000 steps at dt 0.01, 4 × 10 × 10 × 60 m:

```
- 200 alive after 2000 steps at dt 0.01, 213 floor spawns, 6 births, 0 crowded stillbirths, 0 diverged
- box 4 x 10 x 10 m, depth 60, hash cell 1.984 m, 10 placement rejections
- wraps 2, contact pairs 3873 (1.9365 per physics step)
- displaced at step 200: 10 to y=+0.25, 10 to y=-60.1 (inside the bed); back inside by step 2000: 10 and 10
- the buried ten: deepest any root was under the bed after the push 0.0998 m, all clear of it by step 705
- the bed: 20747 floor contact pairs (10.3735 per physics step), counted apart from the 3873 creature-creature pairs
- centre-of-mass census at the end: 0 above the surface, 0 below the floor
- ok   bodies loose outside the box, any step = 0
- ok   of those, back inside from above = 10
- ok   bodies built with their sphere in the bed = 0        (219 bodies checked as they were built)
- ok   of those, back out of the bed = 10
- ok   deepest any buried root ever got under the bed: worst 0.0998 m out, bound 0.101
- ok   excursion left under the bed at the end: worst 0 m out, bound 0.1
- ok   `above` as the report reads it = 0
- ok   below the floor, centre of mass = 0
- ok   diverged = 0
- ok   the bed reports contacts: 20747 pairs
- ok   non-finite parts = 0
```

**219 bodies were checked as they were built** — every body, the first step it exists, which
is after `Ecosystem.Build` placed it and before physics has moved it. That is rule 2 measured
on real articulations rather than on reserved spheres.

## 3b. Milestone 1 smoke — **PASS**, exit 0, unchanged

*"12 creatures built, geometry verified, actuated and torn down"*; momentum conservation ok
on all 6 seeds with and without self-collision.

## 4. `fl-smoke` — the 3,000-s arm at `r25q-s2`'s exact settings

`rounds/launch-r25.ps1 -Seed 2 -MatterInitial 0.25 -Seconds 3000`, dt 0.02, worker 6,
configHash `162996716da49619` — **the same configHash `r25q-s2` ran under**, so only the code
differs. Header verified: `dt=0.02`, `space shared 4x10x10 m, depth 60, wrap, bed`,
`surface restore 1`, `area 400 m2`, `matter in 0.6/s at vent, burial 0.01/s`, `from 0.25/m3`,
`vent 0.05 m/s in patch 0 from 60 m, legs 1 m`.

| t | alive | depth m | max m/s | diverged | below world | above | contacts | floor con | audit |
|---|---|---|---|---|---|---|---|---|---|
| 100 | 40 | −29.2 | 0.2199 | 0 | 0 | 0 | 0 | 0 | 0.0000% |
| 300 | 40 | −26.6 | 0.1135 | 0 | 0 | 0 | 0.238 | 0.022 | 0.0000% |
| 800 | 47 | −8.0 | 0.2083 | 0 | 0 | 0 | 9.267 | 0.294 | 0.0000% |
| 1300 | 149 | −1.2 | 0.3475 | 0 | 0 | 0 | 98.348 | 0 | 0.0000% |
| 2000 | 383 | −1.6 | 0.2198 | 0 | 0 | 0 | 418.786 | 0 | 0.0000% |
| 3000 | 674 | −1.3 | 0.1914 | 0 | 0 | 0 | 1020.192 | 0 | 0.0000% |

Totals: 150,000 physics steps, 677 births, 39,843 wraps, 1,910 crowded stillbirths, 327.14
creature-creature pairs per physics step, 0.0114 floor pairs per physics step, 596 drag
impulses limited, 810,160 drive impulses limited, `status ended / budget`.

Against the spec's checklist:

- **`diverged` 0 — PASS** at all 30 samples. ⚠ **On its own this is not evidence the
  divergences are gone**, and saying it would be would be dishonest: `r25q-s2`'s three are at
  t = 14,793.5, 15,630 and 19,973, all past 3,000 s, and the previous build's own 3,000-s run
  (`fl-prev`, below) also reads 0. §6 is the arm that answers it, and it does: **0 in
  20,000 s against 3**.
- **`below` 0 — PASS** at all 30 samples.
- **floor contacts reported — PASS**: `floor con` reads 0.022 and 0.294 pairs/step at t = 300
  and 800, then 0 once the population leaves the bottom. Small, and the reason is where the
  world went, not the instrument: mean height is −1.3 m at t = 3,000. The population is in the
  surface film, which is `dt 0.02`'s known bimodality (CLAUDE.md; logbook/0056) plus the
  plume, and neither is this build's business. The smoke exercises the instrument properly:
  20,747 pairs from ten bodies actually lying on it.
- **audit 0.0000% at every row — PASS.** A static collider in the water does not open the
  energy ledger.
- **`max m/s` in the first 100 s — 0.2199** (the t = 100 row), and `mean m/s` 0.027. But the
  run's *fastest ever* is **1.06 m/s at t = 5 s**, and the spec asked whether that is still a
  floor bounce. **It is not, and it never was.** See below.

## 5. Wall clock against the previous build — **3.5 min against 3.4 min**

`fl-prev`: the identical arm on the previous build, run solo on the same machine immediately
before. The previous build was put on disk with `scratch/floor-swap-build.py`, which
reconstructs each file with `git show` rather than `git checkout` — `core.autocrlf` is true
and every file this build touches is LF in the working tree, so a checkout would have
rewritten them CRLF and changed `simHash` for no visible reason (CLAUDE.md's `simHash`
gotcha). That the reconstruction was exact is not asserted but measured: `run-arm.ps1`
reported `simHash c27c23c0…` and `coreHash 092eabbd…` for `fl-prev`, which are
character-for-character the hashes `logbook/specs/footprint-build-report.md` recorded for that
build. Afterwards the tree was restored the same way and the byte digest of
`unity/Assets/Evosim` returned to its pre-swap value (`05728423a5a9ca8a`, 21 files), with
worker 6 refreshed and re-checked against it.

| | `fl-prev` (previous build) | `fl-smoke` (this build) |
|---|---|---|
| simHash | `c27c23c0…` | `1f5455f4…` |
| configHash | `162996716da49619` | `162996716da49619` |
| **wall clock** | **3.4 min** | **3.5 min** |
| **× real time** | **14.7** | **14.3** |
| alive at t = 3,000 | 671 | 674 |
| births | 673 | 677 |
| mean depth at t = 3,000 | −1.3 m | −1.3 m |
| creature contact pairs / step | 316.2 | 327.1 |
| floor pairs / step | — (no bed) | 0.0114 |
| crowded stillbirths | 2,003 | 1,910 |
| wraps | 39,356 | 39,843 |
| drag / drive impulses limited | 219 / 467,619 | 596 / 810,160 |
| diverged, below, audit | 0, 0, 0.0000% | 0, 0, 0.0000% |
| **fastest ever** | **1.06 m/s at t = 5 s** | **1.06 m/s at t = 5 s** |

**~3%, which is inside repeat noise.** The bed costs nothing measurable: it is one static
box in the broadphase, and the per-pair classification in the contact callback is a
comparison against a number the solver has already computed. The two worlds are different
chaotic realisations of the same seed (a per-step change is a butterfly — CLAUDE.md), so the
population and contact figures are read as "the same world, differently rolled", not as
effects.

## 6. `fl-long` — the 20,000-s arm, and the question the 3,000-s one cannot answer

The spec asks for 3,000 s, and 3,000 s cannot test the thing this build exists to fix:
`r25q-s2`'s three divergences are at t = 14,793.5, 15,630 and 19,973. So I ran the whole
20,000 s at the same settings, same seed and the same configHash `162996716da49619` that
`r25q-s2` ran under. `runs/fl-long`, worker 6, solo, `status ended / budget`, 144.3 min.

| | `r25q-s2` (previous build) | `fl-long` (this build) |
|---|---|---|
| simHash | `c27c23c0…` | `1f5455f4…` |
| **`diverged` at t = 14,000 / 16,000 / 18,000 / 20,000** | **0 / 2 / 2 / 3** | **0 / 0 / 0 / 0** |
| `diverged` over all 200 samples | reaches 3 | **0 at every one** |
| `runs/<arm>/<run>/diverged/` | 3 dumps (`4560`, `4823`, `6225`) | **directory never created** |
| `below world` | 0 throughout | 0 throughout |
| audit | 0.0000% | **0.0000% at all 200 rows** |
| alive at 14,000 / 16,000 / 18,000 / 20,000 | 2034 / 2387 / 2797 / 3142 | 2078 / 2409 / 2743 / 3103 |
| mean depth at the same four | −18.0 / −6.7 / −3.8 / −29.8 m | −16.2 / −6.0 / −3.8 / −29.8 m |
| births | 6,148 | 6,269 |
| contact pairs / step | 1,146.4 | 1,231.5 |
| floor pairs / step | — (no bed) | **8.90** (peaks at 121.1 in a window) |
| wall clock | 212.9 min at 1.6× | 144.3 min at 2.3× |

**Three divergences become none.** And the usual caution — that a per-step change makes the
same seed a different chaotic realisation, so a per-seed A/B cannot separate the change from
the roll — is unusually weak here: the two worlds track each other to within a few percent
of population and within 2 m of depth over 20,000 s, which is far inside the ±20% wingspan
logbook/0052 measured. This is close to the same world with and without the spring, and the
spring's three casualties are the difference.

All three dumps were newborns (ages 1, 8 and 13 s) within 0.8 m of −60 m, which is exactly
the case rule 2 removes: a newborn is placed at its parent's depth, a parent resting on the
mirror sits where the buoyancy term flips sign across a plane, and this build refuses to
place a body there at all.

⚠ Two readings from this arm that are **not** results, and should not be read as any:
`floor con` and the depth column wander between −4 m and −36 m over the run, and the fastest
creature is 2.5944 m/s at t = 1,665 s. This is dt 0.02, where the drag limiter bound
1,684,628 times and the drive limiter 1,311,636 times — CLAUDE.md's rule is that anything
about swimming, joints or depth at the coarse step is read at 0.01 only. What the arm is
evidence for is the divergence count, the audit and the bed being used at all.

---

# The founding bounce was never the floor

This is the one finding that changes something already in the record, and it is the owner's
because it is about a world rule.

`logbook/specs/footprint-build-report.md` flagged, and D077's ruling note carries: *"The fastest
creature in `fp-smoke2` is a floor bounce, not a swim: 1.0338 m/s at t = 6.5 s. Founders
spawn at `EVOSIM_FOUNDER_DEPTH 60` in a 60 m world — on the floor — and a body that drifts a
hair under −D now gets its whole weight upward … This is the placeholder floor showing
through, and it is the concrete reason … a real sea bed is a collider that stops a body, not
a spring that returns it with the momentum it arrived with."*

With the real sea bed in, and founders placed with their whole bounding sphere clear of it,
**the number does not move**:

| arm | build | bed | surface restore | fastest ever |
|---|---|---|---|---|
| `fl-prev` | previous | no (mirror) | 1 | **1.06 m/s at t = 5 s** |
| `fl-smoke` | this | **yes** | 1 | **1.06 m/s at t = 5 s** |
| `fl-surf0` | this | **yes** | **0** | **0.3461 m/s at t = 99.5 s** |

`fl-surf0` is a 300-s diagnostic arm at `fl-smoke`'s settings with `EVOSIM_SURFACE_RESTORE 0`
and everything else identical. It is decisive rather than suggestive: at t = 5 s only the
forty founders exist, placement draws from its own RNG stream and is untouched by the
fraction, and nothing has moved more than centimetres — so the two worlds at t = 5 s differ
*only* by the restoring term. Turning the bottom into rock changes nothing; turning the
**top** off removes it. `fl-smoke`'s `floor con` reads 0 at t = 100 and 200, so nothing was
touching the bed then either.

**What it actually is:** the founder draw is uniform over 0…−60 m and `EVOSIM_FOUNDER_FLOAT`
is 0.5, so about one founder in sixty lands in the top metre and about half of those are
buoyant. Such a body rises the few millimetres to y = 0, crosses it, loses its buoyant
support entirely (D077 rule 4 as the owner amended it — *out of the water a body feels its
whole weight*), falls back at the rule's terminal speed of about a metre a second, is
buoyant again, and rings. 1.06 m/s is the surface's terminal fall speed, not a bounce off
anything.

**So `bestSpeed` in a shared world's founding is still not locomotion, and the sea bed was
not the fix.** The floor was worth fixing for its own reasons — the three divergences, and a
bed a creature can rest on — but the surface is a second discontinuity of the same shape and
it is the one this number came from. Whether that matters is a world-rule question: a body
that emerges and falls back *is* the honest physics the owner ruled for, and the ringing is
the price of a hard boundary at the waterline. The cheap instrument fix, if one is wanted, is
to stop `bestSpeed` counting a body that is above y = 0 — but what a speed instrument may
ignore is not mine to decide.

---

# What I could not do as written, and why

**1. The buried bodies are pushed 0.1 m into the bed, not 0.25 m, and they clear it in 505
steps, not 200.** The spec asks for "2,000 steps with 20 bodies pushed under the floor by
hand — all resting on or above it after 200 steps". Both numbers are about
`Physics.defaultMaxDepenetrationVelocity`, which this project deliberately sets to **0.02
m/s** (`FluidEnvironment.MaxDepenetrationVelocity`) because a separating velocity the solver
invents is free energy and logbook/0007 measured a creature farming it. A body teleported
inside a static collider therefore comes out at five centimetres a second and no faster:
0.25 m would take 12.5 s against a body that is simultaneously being pulled down at the
smoke's `ExcessDensity`, and a smoke that failed on that would be reporting the cap rather
than the bed. So the push is 0.1 m — ten times the default contact offset, unambiguously
inside — and what is asserted is the pair of properties a bed actually has: **nothing ever
got deeper than the push** (0.0998 m against a bound of 0.1, so nothing sank *through* the
rock) and **all ten were fully back inside** by the end, in fact by step 705, 5.05 s after
the push, which is 0.1 m ÷ 0.02 m/s to two figures. The report prints the clearing step, so
a future reader sees the cap rather than a bare pass.

The surface half of the displacement is unchanged at 0.25 m: nothing caps a restoring force.

**2. "None below −D − radius" is asserted on the root, not on the sphere.** Every other depth
reading in this project is denominated in the root's position (`Ecosystem`'s `below world`,
`AboveSurface`, `Organism.HeightY` and the whole surface rule), and a bound stated in one
denominator and checked in another is how a column comes to mean something other than its
name. The bound the smoke asserts is tighter than the spec's: the deepest any root got below
the bed is 0.0998 m — the push itself — where −D − radius would have allowed roughly 0.6 m.

**3. `runs/th-ref` still cannot be replayed and the theatre was not re-run.** The refusal
that the footprint build recorded is unchanged: this build adds no tunable, so `th-ref2`
should replay under it, but I did not run the theatre identity check and I am not claiming I
did. What I did do to the theatre is drawing only, and `OnRenderObject` does not execute in
`-batchmode -nographics`, so no picture has been looked at by anybody.

**4. `DECISIONS.md` is untouched.** D077's rule-4 amendment still describes the mirror as the
bottom, and its status row still lists "a real floor collider at −60 m" as pending. World
rules and their amendments are owner-reserved and the file is append-only, so it wants an
entry absorbing `logbook/specs/floor-spec.md` and marking the mirror superseded for the shared
branch. `FluidConfig`, `FluidEnvironment` and `SeaFloor` all carry the reasoning in their
remarks in the meantime.

**5. One pre-existing test is red and I left it red** — `SnapshotJoinTests`, §1 above.

---

# Housekeeping

- **Nothing was written outside the repository.** Unity logs in `scratch/logs/`
  (`floor-smoke.log`, `floor-m1.log`, `evosim-fl-*.log`); arms in `runs/` (gitignored);
  the scripts I wrote are `scratch/floor-smoke-edits.py`, `scratch/floor-smoke-edits3.py`
  (the byte-preserving source edits, via the existing `scratch/edit_cs.py`),
  `scripts/reads/compare-rows-by-name.py`, `scripts/reads/worker-hash-check.py` and
  `scratch/floor-swap-build.py`.
- **Runs kept:** `fl-replay` (tiled identity), `fl-smoke` (the screen), `fl-prev` (the
  previous build at the same settings), `fl-surf0` (the surface diagnostic), `fl-long` (the
  20,000-s divergence check: 0 divergences against `r25q-s2`'s 3). The footprint build's baselines `fp-base2`, `fp-replay3`,
  `fp-smoke2` and `fp-tiled` are untouched and were used as comparators.
- **Worker 6** carries `simHash 1f5455f4851591d00ccf0e8115976cc6caf798efbf8f993d469b00ed9a3c41f4`
  and was byte-verified against `unity/` after the build swap. No other worker was touched.
- One Unity process at a time throughout; `Start-Process` only; nothing was
  `Stop-Process`ed.
