# The plants work on round 67's code changes nothing with its switches off (2026-11-06)

The plant work goes on top of the code round 67 ran. Branch `plants-r67` (`scratch/wt-plants-r67`) is `plants-hold`, which
carries D297's two switches, merged onto `card-kit` as merge `1c689c52`. The merge resolved twenty conflicted files.

## What ran

The crowd fixture's world, round 44's at seed 4 for 20,000 s with record format 1 at 4 threads, on the merged build's
Release farm from a clean tree, with D297's switches at their defaults, which are off
(`logbook/specs/plants-hold/fixtures.ps1`). The run is `runs/cefix-s4/2026-11-06-083054-1ad1802c`, config
`1ad1802cb9eccdb8`, against `card-kit`'s fixture `runs/cefix-s4/2026-11-03-162200-649bac7a`. The config hash differs
because the merged build has more tunables.

## What it found

| | Compared | Different |
|---|---|---|
| Stats rows, 10 to 20,000 s, every field but the wall times (`scripts/reads/compare-run-rows.py`) | 2,000 rows, 340,000 values | none |
| Fields present in one run only | | none |
| `lineage.jsonl` | 10,408 rows | byte-identical |
| The snapshot at 20,000 s | | byte-identical |

So with D297 off, the merged build steps round 44's world as `card-kit` does, row for row. The pins and recorded
fixtures that name a config hash are stale on the merged build, as with any new tunable. They are repointed once the
plant switches built on top of it are in.

## The plant build on top of it changes nothing with its switches off

Branch `plants-build` (`scratch/wt-plants-build`, `0425d88c`) adds the module rule's growable choice (`module-fix`,
`6b98db4a`), and D296's anchored founders with D298's growable blades, each off by default. The same fixture on its
Release farm (`logbook/specs/plants-hold/fixtures.ps1 -Worktree`) is `runs/cefix-s4/2026-11-06-091711-013d5f2b`, config
`013d5f2b506ace55`. Against the merged build's fixture its 2,000 stats rows hold the same 340,000 values with no field
in one run only, and its `lineage.jsonl` and its snapshot at 20,000 s are byte-identical. So every switch of the plant
work so far is off in the world it does not name.
