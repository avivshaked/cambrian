# Build spec: the footprint world (D077) — shared space, a top, a bottom and a wrap

Fable-written, 2026-09-05, for an implementing agent. The owner ruled D077 ("proceed" on
`fable-propose-footprint.md`, now absorbed into DECISIONS.md D077; read that entry first,
then CLAUDE.md in full, then `logbook/specs/footprint-survey.md` and `logbook/specs/surface-spec.md`
for the file:line facts, and logbook/0064 for the spike's placement code, which you reuse).
`unity/Assets/Evosim/Sim/Editor/SharedSpaceSpike.cs` has working overlap-free placement
and a `Physics.ContactEvent` counter; lift from it rather than rewriting.

## The rule of the build

**Everything is behind one switch, and at the default the record replays byte for byte.**
`RunConfig.SharedSpace` (`EVOSIM_SHARED_SPACE`, default false). False: the tiled lattice,
the inherited patch, the two transport lotteries, the inert top and bottom — today,
unchanged to the character in every per-step expression. True: rules 1–6 below. One
exception is allowed to be independent: the restoring top and bottom (rule 4) has its own
tunable so it can be read alone; its default is also today.

## The rules (true branch)

1. **The box.** K = `HorizontalPatches` patches of W × W side by side on a ring, depth D =
   `WorldDepthMetres`; W = sqrt(`WorldAreaSquareMetres` / K), and `CurrentField.
   PatchWidthMetres` is set from it (it already is; make the derivation the one source).
   Box: x ∈ [0, K·W), y ∈ [−D, 0], z ∈ [0, W). At the screen's settings: 4 × 10 × 10 m, 60 m
   deep, area 400.
2. **Patch from position.** Each metabolic step, before anything reads `Organism.Patch`,
   set it from the root: `patch = floor(x / W) mod K` (x wrapped first). `World.Disperse`
   and `AdvectBodies` (the `EVOSIM_CURRENT_ADVECT` body lottery) do nothing when
   `SharedSpace` is true; their RNG streams are not drawn (state that; it is what keeps the
   false branch's draws identical). The fields' own transport is untouched.
3. **Wrap.** After `Physics.Simulate` each step, a root whose x or z has left the box is
   translated by ±K·W or ±W (whole articulation; `ArticulationBody.TeleportRoot` with the
   same rotation; velocities untouched). Count wraps per window (`wraps` column). Contacts
   across a seam are not seen; say so in the remark.
4. **A restoring top and bottom.** `SurfaceRestoringFraction` (`EVOSIM_SURFACE_RESTORE`,
   0–1, default 0). In `FluidEnvironment`'s buoyancy step, where today
   `if (netDensity < 0f && y >= 0f) netDensity = 0f;`: with the fraction f > 0, a body at
   y > 0 with negative net density gets `netDensity = +f × founderSinkExcess` (the excess
   density that produces the founder sink rate; find the existing constant and use it, do
   not invent one) so it falls back at no more than the sink rate; a body at y < −D with
   positive net density gets the mirror. At y = 0 exactly, today's clamp (continuity). The
   default-0 branch must be the original expression verbatim. Unit-test sign and
   continuity in the Sim smoke (Core cannot see PhysX); test the config reflection in Core.
5. **Placement.** Founders and inoculants: uniform in the box at their drawn depth,
   rejection-sampled against a spatial hash of living bounding spheres (radius = max over
   parts of |offset| + |half-extents|, as the spike computes it). Newborns: at the parent's
   root plus (r_parent + r_child) in a random horizontal direction, same rejection
   sampling, an attempt budget of 64 with the direction re-drawn each time; on exhaustion
   the birth is a **crowded stillbirth** — counted (`crowded` column, manifest fact), the
   parent keeps its energy and matter (nothing was built), the lineage row is not written.
   All draws from one dedicated `Rng` stream seeded the way `ConceptionOrder`'s is (a
   distinct constant), so placement changes no other draw. `TileSpacing` and the lattice
   stay for the false branch only.
6. **Instruments.** Columns and `stats.jsonl` fields: `above` (roots with y > 0), `below`
   (reuse `belowWorld`), `wraps`, `crowded`, `contacts` (mean contact pairs per physics
   step over the window, from `Physics.ContactEvent` with `providesContacts` on every
   collider when `SharedSpace`; empty when tiled), `p0`..`p(K−1)` alive per patch. Header
   token: `space shared 4x10x10 m, depth 60, wrap` or `space tiled 100 m`; `surface restore
   f`. Manifest end facts: `crowded`, `wraps`, `contacts` mean.
7. **The theatre.** `WaterBounds` draws the box and the K−1 patch seams when the run's
   config has `SharedSpace`; nothing else changes there.

## Not in this build

Any change to feeding, light, matter, the vent's transport, the fields; predation;
per-patch light or shading changes; any dose. The screen reads the ecology.

## Validation (before handing back)

1. `./scripts/core-test.ps1` green with the new tunables in the two reflection tests.
2. **Identity at the default:** replay `runs/th-ref` (1,000 s, dt 0.01, the current
   build's recording) on the new build: every row byte-identical modulo appended columns
   and wall clock; and one 2,000-s run of `rounds/launch-r24.ps1`'s settings (seed 2)
   diffed against a pre-change run of the same, as the perception build did. Any
   difference is a fail.
3. Milestone 1 smoke unchanged, plus a shared-space smoke: 200 founders in a 4 × 10 × 10
   × 60 box for 2,000 physics steps at 0.01 — none outside the box after wrapping, `above`
   and `below` 0 after the first 200 steps with restore 1, placement rejections reported,
   contacts counted, no non-finite body.
4. A 3,000-s arm at 0.02 on seed 2 with the r24 settings plus `EVOSIM_SHARED_SPACE 1`,
   `EVOSIM_SURFACE_RESTORE 1`, `EVOSIM_AREA 400` (`fp-smoke`): audit 0.0000% every row,
   `diverged` 0, the header tokens present, `above`/`below` 0 after t = 500, patches all
   populated by t = 3,000, `crowded` reported, wall clock against the same settings tiled.
5. Report the two things the agent judged: the founder sink constant used in rule 4, and
   the spatial-hash cell size.

Constraints: workers 2–7 are free now, use `unity-w6` for the arms and the main `unity/`
for the smoke (no Editor has it open; check `Get-Process Unity`); one Unity process at a
time; nothing written outside the repository; commit on `main`, do not push; no
`--no-verify`; the physics-loop expressions on the false branch unchanged to the character.
Hand back `logbook/specs/footprint-build-report.md` with numbers, commit hashes, the header
token format, and what you could not do as written and why.
