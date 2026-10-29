# The nose on the card: its identity sitting

The card port of D235's nose, on branch `nose` at `99258173` (`scratch/wt-nose`), sat on the card on 2026-10-29 in two
parts, by `scratch/nose-card/sitting-nose.ps1` and read by `scratch/nose-card/accept-nose.ps1` over
`scripts/reads/gpu-accept.py`. Each part ran three worlds, seeds 6202 to 6204, to 600 s.

- **Part a, switch off,** at group 32 from 01:21 to 01:27 UTC: every world stepped identically to `r62cf`'s prefix, with
  stats on all 60 shared samples, the digest on all 601 rows to step 60,000, and the lineage byte-equal on the shorter
  run.
- **Part b, the nose on,** first launched at 01:28 UTC and cut off a minute in by Windows Update's restart at 01:29, which
  left no run directory. Its logs are kept in `scratch/wt-nose/scratch/logs/cut-by-restart-2026-10-29/`. It ran again from
  10:07 to 10:18 UTC. On every seed, group 32 and group 64 gave identical stats on all 60 samples, identical digests on all
  601 rows and byte-equal lineages (1,667, 1,290 and 1,447 lines).

| Seed | Nose at 32, pace | Nose at 64, pace | Off at 32, pace |
|---|---|---|---|
| 6202 | 3.88x | 3.91x | 3.88x |
| 6203 | 4.17x | 4.23x | 3.94x |
| 6204 | 4.00x | 4.04x | 3.78x |

The nose's pace is at or above the switch-off pace on every seed. That is not a timing read: the off worlds ran in the
night's sitting and the nose worlds in the morning's, and the two worlds part after their first births. It says only that
the port adds no cost large enough to see at 600 s.

G3's card half (`logbook/specs/grazer-film-plan.md`) is passed. Its other half, the nursery test in the patch tank, comes
next.
