# Build spec: positions in the record (owner: "build these", 2026-09-10)

Read `CLAUDE.md` (the conventions on a run directory and `JsonlWriter`'s one-row-one-line rule; the
gotcha "A lineage dissection can answer less than it looks like it can"; the D088 gotcha), then
`logbook/0083` and `0085`, `RunDirectory.cs`, `JsonlWriter.cs`, `Ecosystem.cs` (`Row`,
`MeasureHorizontalSpread`, `Body.LastRootPosition`, the absorptive id set collected in `Row`),
`EvolutionRun.cs` where `stats.jsonl` rows are written, and `scripts/analyse-arm.ps1` for how a
run is addressed by arm name.

Rules: you are in a git worktree; edit only inside it; no commit; never poll, sleep or wait; do not
run Unity at all (the caller compiles when merging); Core tests filtered
(`./scripts/core-test.ps1 -Filter <Class>` from PowerShell, run from the worktree root); doc-comment
register: why, with the incident; no em-dashes in code comments or prose.

## Why

No file in a run says where a body is. The report carries a mean depth and per-patch bins; a
lineage row carries a patch; a snapshot carries a genome. Thirty-three rounds were read that way
and the theatre showed the world as two ribbons the first time it was pointed at a scored run
(logbook/0083). The owner asked for tools that look into the world from the maths side. This is
the first: a positions file, and a reader that draws it.

## 1. `positions.jsonl`

One row per sample, at the cadence of `stats.jsonl`, written by the harness from
`Body.LastRootPosition` (already read that step; no Transform read), skipping bodies without
a root this step and non-finite roots, exactly as `MeasureHorizontalSpread` does.

Row shape (compact, one line, refused by `JsonlWriter` if it carries a line break):

```
{"t":1234.5,"n":1481,"b":[[id,x,y,z,f],[id,x,y,z,f],...]}
```

`id` the organism id that joins to `lineage.jsonl`; `x`, `y`, `z` metres to two decimals
(`y` negative below the surface); `f` a small integer of flags: bit 0 absorptive expressed,
bit 1 jointed expressed, bit 2 photosynthetic expressed, using the same tests that produce
`absorpt`, `jointed` and `photo` in the table so that the two cannot disagree. Build the row
text in Core (`PositionsRow` or similar beside `RunDirectory`, taking arrays) so it has a test;
Sim fills the arrays. In a tiled world (`SharedSpace` off) write nothing and say so in the
remark. Size: state it for 1,500 bodies over 300 samples.

Not a tunable: it is a recording, not a world rule, and must not move the config hash or refuse
an older config. It does move `simHash` and `coreHash`, which is why it lands after the owner's
theatre pass (say this in the remark).

## 2. `scripts/positions-read.py`

Python 3, standard library plus `matplotlib` if it is installed (check with `python -c "import
matplotlib"`; if it is not, the script prints the numbers and says the pictures need it; do not
install anything). Usage:

```
python scripts/positions-read.py <arm> [--at 1000,5000,20000] [--out scratch/positions/<arm>]
python scripts/positions-read.py <arm> --summary
```

Resolves `<arm>` to the newest run directory under `runs/<arm>/`, streams `positions.jsonl`
(never loads it whole), and:

- `--summary`: per sample (or every k-th), `n`, occupied 1 m columns, x and z circular spread,
  median nearest-neighbour distance (horizontal and 3D; a KD-tree is unnecessary at 2,000
  bodies, brute force per sample at chosen samples only), per-guild mean depth, and the count of
  bodies within 1 m of another body of the same clade if `lineage.jsonl` is present (clade =
  connected component by parent chain; founders are their own roots).
- `--at`: for each time, the nearest sample, and if matplotlib is available three PNGs, side
  (x against y, the whole box in frame, 20 m by 60 m), end (z against y) and top (x against z),
  points coloured by guild (leaf, stomach, jointed, photosynthetic mixotroph), with the box drawn
  and a title carrying arm, t, alive, and a fourth PNG colouring the side view by clade (the
  largest eight clades named, the rest grey). Default output under `scratch/positions/<arm>/`.
- Read the box's dimensions from the run's `config.json` (`WorldAreaSquareMetres`, patch count,
  patch width, depth: find the fields by name; do not hard-code 20 by 5 by 60).

Add a fixture test under `scripts/tests/positions/` in the shape of the existing
`scripts/tests/clade-score/` tests: a tiny hand-written run directory with three rows, and a
check that `--summary` prints the expected columns and the expected nearest-neighbour number.

## 3. Record

- `CLAUDE.md`: one sentence in the run-directory convention naming the file and the reader; and
  amend the gotcha that says depth by guild is not measurable from a run's output to say that
  from this build it is, from `positions.jsonl`.
- `DESIGN.md` §9 (the run directory) gains the file in its list, one line.

## Report

The row builder's code and its test; the diff stat; the reader's `--summary` output on the
fixture; anything that did not fit.
