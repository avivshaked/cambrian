# Light by exposure: the build spec

*Fable, 2026-09-23, from `fable-propose-light-exposure.md` as ruled that morning. What to
build so that a part earns on, and shades with, its projected area in its actual pose; off
by default so every recorded world replays bit for bit. The one-part leaf's way to lie flat
(the buoyancy offset) is a separate spec once ruled. Read `logbook/specs/mouth-spec.md` for
the shape a spec here takes and `CLAUDE.md`'s tunable rule (a new field refuses every earlier
config).*

## 1. The quantity

For a box part with half-extents `(hx, hy, hz)` and world rotation `R` (columns the part's
axes in the world), the area it projects onto the horizontal is

    projected = 4 · ( hy·hz·|Rx·up| + hx·hz·|Ry·up| + hx·hy·|Rz·up| )

with `up = (0, 1, 0)`. Its mean over all rotations is a quarter of the surface, which is what
`PhenotypePart.LitArea` is now, so the **exposure factor** of a part is

    e = projected / LitArea

and reads 2 for a thin sheet lying flat, about 0 for one on edge, 1 on average over random
poses. A sphere part has `e = 1` at every angle. A capsule uses the box formula on its
half-extents, as the hull's corners do for it (`ConvexHull.AppendPartCorners`), which is
consistent with D099's rule rather than exact. The factor is a ratio, so growth (which scales
every half-extent) leaves it unchanged.

The body's shadow with the cap on is the convex hull's projection onto the horizontal in the
same pose: the one-sided sum over the hull's faces of `area · max(0, n · up_local)`, where
the hull is the rest-pose hull `MeasureSilhouette` already builds and `up_local` is the world
up brought into the root's frame (`R_rootᵀ · up`). With the cap off there is no shadow term,
as now. When the hull fell back to the box, the box's three face pairs stand in, as they do
for the surface today.

## 2. Where it lives

- **`RunConfig.LightByExposure`**, bool, default false; `EVOSIM_LIGHT_EXPOSURE` in
  `EnvBinding`; the header token `light by exposure` or `light averaged`, printed beside
  `silhouette on/off` in `Report.cs`. `RunConfigJson` writes and reads it, so every
  `config.json` written before it is refused (rounds 41 to 45, the void launch, the three
  fixtures and the three checkpoint fixtures included: all re-recorded once on the finished
  base-round build, not per tunable). `RunConfigTests` and `RunConfigJsonTests` catch an
  omission.
- **`Organism.PartExposure`**, `float[]` indexed like `PartDamage`, one factor a part, set by
  the harness before `World.Step` at every metabolic step and read by Core inside it. It is
  not checkpointed: the solver's rotations are, and the harness fills it before the world
  reads it, so it goes on `CheckpointFidelity`'s list of members a step fills before it reads.
  Null means every factor is 1, and it is null whenever the tunable is off, so the off path
  touches no float.
- **`Phenotype.HullFaces`**: the rest-pose hull's faces as `(normal, area)` pairs, kept from
  `MeasureSilhouette` (a few dozen for a developed body), scaled with growth as
  `SilhouetteArea` is. `ConvexHull` gains a `TryFaces` beside `TrySurfaceArea` that returns
  them, or `SurfaceArea` gains an out list; the surface number it prints today must not move
  by a bit (the Slow snapshot scan is the check).
- **`Phenotype.ExposedLitArea(float[] exposure)`**: `Σ LitArea_i · e_i`, and
  **`Phenotype.ShadowArea(Quaternion rootRotation)`** for the cap. `LitAreaFactor` and
  `EffectiveLitArea` take the exposure and the root rotation when the tunable is on and are
  unchanged when it is off: with the tunable off the existing methods are called and the
  new ones are never entered, so no float is added to a recorded world's path.

## 3. The two sides of the light

Both move together or neither (D099's rule, restated at `World`'s shading site: shade cast
but not collected is light destroyed, collected but not cast is light created).

- **Shading.** `World`'s per-body `Field.Contribute` passes `EffectiveLitArea` computed with
  the exposure and the shadow cap when the tunable is on.
- **Income.** `Metabolism`'s per-part `CellContext.litArea` is `part.LitArea · e_i · litFactor`
  with `litFactor = min(1, ShadowArea / ExposedLitArea)` when the cap is on and 1 otherwise.

## 4. The harness

In `Metabolise`'s per-body pass, where `CentreOfMass(solver)` is read, add
`Exposure(solver, creature)`: for each link, read its rotation matrix from the solver, take
the three absolute dot products of its axes with up, and write the factor into
`creature.PartExposure` (allocated once per body, resized on growth as `PartDamage` is). The
link-to-part map is the one `HandBackWhatWasFelt` relies on. It runs only when the tunable is
on, and the root's rotation is handed to `World` for the cap the same way (a field on the
organism set beside the exposure, or a second array of one quaternion; the cheaper is fine).
The Unity farm does not bind the variable; a world built there carries it off and its header
has no `light` token. The GPU design's per-metabolic-block readback of every link's rotation
already carries what this needs.

## 5. The instrument

- `stats.jsonl` gains `meanExposure`, the mean of `e` over the living bodies' photosynthetic
  parts, area-weighted (1 = random, 2 = every leaf flat), and the table prints it as `expo`,
  a dash when the tunable is off. It is the round's readout, and `scripts/reads/tilt.py`'s
  flat factor is its independent check from the poses (`expo ≈ 2 · mean |cos tilt|` for a
  crowd of thin sheets).
- `absorptive.jsonl` gains `exposedArea` beside `photoArea`; `photoArea` stays the
  orientation-averaged plan.
- `scripts/ledger.ps1` takes `-Exposure` (default 1) and multiplies the lit area by it, so the
  break-even leaf can be screened at 0.5, 1 and 2 before the round runs.

## 6. Tests

1. A thin box (`hy` a tenth of `hx`, `hz`) at identity reads `e` within 1e-3 of `4hx·hz /
   LitArea` (about 2 for a thin sheet; the draft wrote a spurious factor of 2 in front, which
   the builder caught against §1 and the mean-of-1 test); rotated a quarter turn about x,
   within 1e-3 of `4hx·hy / LitArea`. Note for the readers: a thick box lying flat reads
   below 1 (thin/thick 0.55 reads 0.95, a cube 2/3 face up and 1.155 on its diagonal), so
   `expo` rewards thin and flat together, and `tilt.py`'s `2 · mean|cos|` stands for it only
   on thin sheets.
2. The mean of `e` over 10,000 uniformly random rotations of that box is 1 within 0.02
   (Cauchy).
3. A sphere reads 1 at any rotation.
4. The shadow of a cube at identity is its face; of a cube tilted 45° about x, `√2` times its
   face; and the shadow of a body never exceeds its exposed lit area sum by more than the
   hull's own excess over the parts (the cap is at most 1).
5. With the tunable off, the crowd fixture's 3,000 s regress is identical in every field and
   the lineage byte-equal (`scratch/r45-build/regress.py`), and `ParallelIdentityTests`'
   word holds.
6. With it on, both books close on a 3,000 s dt 0.02 screen of round 45's launcher
   (`audit` under 0.1 J, `mat resid` under 1e-5), and `expo` prints.
7. `CheckpointFidelity` passes on a checkpoint written with the tunable on (`--verify-
   checkpoint`), with `PartExposure` on its filled-before-read list.

## 7. Cost

By arithmetic three dot products a part and one a hull face, once in fifty physics steps.
The first screen's `wall split` and `harness split` lines are the measurement, and the entry
reports them.
