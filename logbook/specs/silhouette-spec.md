# The silhouette cap, and the terminal edge that keeps its limit: the build round 41d runs on

*2026-09-19, 07:40. Written by the agent on the owner's ruling ("Yeah sounds good", 07:05, on
`fable-propose-silhouette.md`, absorbed as D099). Round 41c was stopped on bodies of nine and
sixteen parts folded into a ball, every part overlapping every other, earning sixteen parts'
light from one point and costing the physics every pair (logbook/0107's last section). The
ledger and the probe sized the fix: capped at its convex hull's silhouette, every knot in the
record earns less than a one-part leaf (0.19 and 0.66 W net against 0.84 at 5 m), and a
one-part box reads hull equal to lit area to six decimals, so the cap never touches an honest
body.*

## 1. What exists

`Phenotype.TotalLitArea` (`src/Evosim.Core/Development/Phenotype.cs`) is the sum of the parts'
`LitArea`, each a quarter of the part's surface (Cauchy: the orientation-averaged projected
area). `World.Step` contributes it to the light field as the body's shadow
(`Field.Contribute(creature.HeightY, creature.Phenotype.TotalLitArea, creature.Patch)`) and
`Metabolism.StepAt` earns on it part by part (`litArea: part.LitArea` in each `CellContext`).
A body's parts never shade each other.

`Developer.Expand` (`src/Evosim.Core/Development/Developer.cs`) follows an edge that is not
terminal-only while the child occurs on the path fewer times than its `RecursiveLimit`
(`CanEnter`), and follows a terminal-only edge once no non-terminal edge can be followed
(`IsRecursionExhausted`), without asking `CanEnter` of it. A terminal-only self-edge is
therefore followed to `MaxDepth`.

`scripts/overlap/` (the probe) develops every genome in a snapshot and reports, per creature,
the self-overlapping part pairs, `litArea`, the convex hull's surface over four (`hullArea/4`)
and the bounding box's. Its hull is an incremental beneath-beyond hull in double precision
over the parts' oriented-box corners, eight a part.

## 2. What is built

### 2a. The silhouette

A body's shadow, and what it earns on, is at most the silhouette of the whole body, which is
the orientation-averaged projected area of its convex hull: the hull's surface over four,
by the same formula each part already uses. Concretely:

- `Phenotype.SilhouetteArea` (m²): the surface area over four of the 3D convex hull of every
  part's oriented-box corners (a sphere or capsule part contributes its half-extent box's
  corners, which overstates it, on the safe side: the cap can only be looser for a round
  body). Computed once when development finishes, in Core, double precision, deterministic,
  no library. The hull code goes in `src/Evosim.Core/Geometry/` as its own class, ported from
  the probe's (which then calls Core's, so the two cannot disagree). A degenerate cloud
  (collinear or coplanar) falls back to the axis-aligned box's surface over four and a
  counter on the phenotype says so. `Phenotype.Scaled(linear)` scales it by `linear²`, no
  recompute.
- `Phenotype.LitAreaFactor`: `min(1, SilhouetteArea / TotalLitArea)` when the cap is on, 1
  when it is off or `TotalLitArea` is 0. `Phenotype.EffectiveLitArea = TotalLitArea *
  LitAreaFactor`.
- `RunConfig.LightSilhouetteCap` (`bool`, default `false`, so every recorded config still
  describes its world and replays; `EVOSIM_SILHOUETTE` 0/1 in `EvolutionRun`; header token
  `silhouette on` or `silhouette off` beside the light tokens). Through `RunConfig.Hash()`,
  `RunConfigJson` and both reflection tests, which will fail until it is. Under §9 a
  missing field refuses the config, so every `config.json` before this build is refused by
  it, rounds 41 to 41c included; that is the standing rule and is recorded, not worked around.
- Where it applies, both places or neither: `World.Step`'s `Contribute` takes
  `EffectiveLitArea`, and `Metabolism.StepAt` multiplies each part's `litArea` by the
  phenotype's factor before it builds the `CellContext`. Nothing else reads `LitArea` for
  income; if something does, it takes the factor too and the spec is wrong to say nothing
  else does. Shading stays self-consistent: a body denies below it what it collects.
- The ledger (`src/Evosim.Ledger`) prints `Silhouette:` beside `Lit area:` and `Lit area
  (capped):` and uses the capped area when the config's cap is on, so the numbers in the
  head of this spec reproduce from `scripts/ledger.ps1`.

### 2b. The terminal edge keeps the limit

`CanEnter` is asked of every edge, terminal-only or not: a terminal-only edge fires when
recursion is exhausted **and** its child may still be entered. A terminal-only self-edge on a
node with limit 1 then grows nothing beyond the node itself, which is what a terminal edge
was for (an extremity at a chain's tip, DESIGN §4.1). A non-terminal self-edge with limit
`n` still grows an `n`-segment spine. No tunable: this is development's semantics, and a
stored genome develops as the rule now says; recorded runs replay under their own builds as
always. `DESIGN.md` §4.1's sentence on terminal edges says the rule and dates the change.

## 3. Tests

- `GeometryTests` (new): the hull of one box's corners has the box's surface (`hullArea/4 ==
  SurfaceArea/4` to 1e-6); of a box and a second box wholly inside it, the outer box's; of two
  disjoint touching boxes in a line, less than the sum of their surfaces over four and more
  than either; a coplanar cloud takes the fallback and counts it; the same cloud in another
  order gives the same area (determinism).
- `DevelopmentTests`: `knot-16-r41c-s3-1341` and `knot-9-r41c-s3-937` (copied to
  `inocula/` with README rows: the round 41c knots, format 6) develop under the run's limits
  to 2 parts each (root and one link) with 2b, where they developed to 16 and 9 before, and a
  non-terminal self-edge with limit 3 still develops to a 4-part spine. `SilhouetteArea` of a
  one-part box equals its `LitArea`; `LitAreaFactor` is 1 for it and under 0.35 for the
  16-part knot developed with the old rule (keep a copy of the old development path in the
  test only if it is cheap; otherwise assert on a hand-built 16-part phenotype).
- `RunConfigTests` and `RunConfigJsonTests` pass with the new tunable.
- `EcosystemTests`: with the cap on, a world of one hand-built knot earns less light per
  step than the same world of one leaf of the knot's silhouette; with the cap off the old
  arithmetic to the last decimal on one existing fixture (no recorded number moves at
  `LightSilhouetteCap false`).
- `./scripts/core-test.ps1` (fast) green; `-All` is the caller's, after the merge.

## 4. Acceptance

A 300 s smoke on round 41c's world with `EVOSIM_SILHOUETTE 1` and `EVOSIM_IDLE 0.0001`
(`rounds/launch-r41d.ps1 -Seconds 300 -Dt 0.02`), header `silhouette on`, both books closed.
Then the screen: seed 1 at dt 0.02 for 5,000 s, read on `pairs/body` under 0.05 at 5,000 s,
the 5,000 s snapshot's part histogram from `scripts/overlap/` with no body at either cap,
and the pace holding above 1x. The screen is the caller's, not the builder's.

## 5. What does not change

The light field, the shade rule between bodies, the part's own `LitArea`, the cell types'
rates, every price of D098. `Assets/Evosim` is untouched except `EvolutionRun`'s env binding
and header token, so `simHash` moves for that alone; `coreHash` moves with Core.
