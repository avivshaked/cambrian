# Build spec: `field cv`, a patchiness reading for the two fields

**2026-09-13**  ·  queued in HANDOFF as the reading that comes before any pocket is built (the
owner's idea of 2026-09-12 night); an instrument, not a world rule; agent work

## What it is

Two numbers per sample: the coefficient of variation (the standard deviation over the mean) of
the detritus field's cell densities and of the matter field's, over the grid's live cells. A
uniform field reads 0; a field whose cells hold 1 and 3 in equal numbers reads 0.5. Until the
conservative transporter (2026-09-12) the grid invented patchiness of its own (30% on 1 m cells
from uniform water), so no earlier patchiness figure can be trusted and the report has never
carried one; from round 37b's build the transporter keeps a uniform field uniform, and this
reading becomes honest. It says how far the water is from well mixed at each moment, which is
the number a pocket proposal has to move.

## Definition

For a `GridField` with live cells `i = 1..N` (every cell in a box; the cells inside the disc
in a tank, `IsLive`) holding stock `s_i` in cells of one volume `V`:

- mean `m = (1/N) Σ s_i`, population deviation `sd = sqrt((1/N) Σ (s_i − m)²)`, `cv = sd / m`;
- `cv = 0` when `m = 0` (an empty field is not patchy);
- densities and stocks give the same `cv` because every cell has the same volume, so the
  method works on `_stock` and skips dead cells exactly as `Mix` and `SeedUniform` do.

The dead cells matter: a tank's array holds zeros outside the disc, and a `cv` over the whole
array would read a uniform tank as patchy. The test below is the guard.

## Where it goes

1. `src/Evosim.Core/Environment/GridField.cs`: `public double DensityCoefficientOfVariation()`
   (one pass over the live cells; no allocation; reads only). `IMatterField` is not touched:
   it exposes no cells, and a vertex field has none.
2. `unity/Assets/Evosim/Sim/Editor/EvolutionRun.cs`, in `Row`: `detritusCv` and `matterCv` in
   `stats.jsonl` (appended after the newest field in the `WriteRow` lambda; `0` on a
   `VertexField`, the `detritusVertices` convention), and two table columns `det cv` and
   `mat cv` appended to `BaseColumns` at the end with the append-rule comment, printed
   `0.###`, a dash on a vertex field as `vtx` prints. The row-count guard keeps the two in step.
3. Tests beside `GridFieldTests.cs`, with its `Field(...)` helper: a box field after
   `SeedUniform` reads 0 within 1e-12; a field with half its cells at one stock and half at
   three times it reads 0.5 within 1e-9 (set through whatever deposit or seeding route the
   class already offers, and say which); a tank field after `SeedUniform` reads 0, which
   fails if the dead cells are counted; an empty field reads 0.
4. `CLAUDE.md`'s grid gotcha gains one sentence naming the columns and the dash;
   `scripts/analyse-arm.ps1` needs nothing (it parses the header row).

## What it must not do

Change any trajectory. The method reads and never writes; nothing in the metabolic loop calls
it except the sampler. Both hashes move (the Core method moves `coreHash`, the report wiring
moves `simHash`), so it lands between rounds, after the fresh-seed batch has launched on
round 37b's build and before round 38, and the 600 s box digest against the current reference
is identical before it merges (`EVOSIM_DIGEST_EVERY`, `scripts/digest-diff.py`).

## Done when

The filtered Core tests pass, the full suite is green on the branch, the smoke's table prints
the two columns with plausible values (a fresh tank at 600 s a few percent on the 1 m
detritus grid, near 0 on the 5 m matter grid, from 0078's and the Astra probe's numbers), and
the digest is identical.
