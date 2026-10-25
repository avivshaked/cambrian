# Per-round reads

These scripts turn a round's arms into the numbers its entry quotes: one reader per round
for the most part, plus the older per-round scorers and the small shared analyses (parent
age, matter budget, matter profile, bids, late balance). Each takes arm names, resolves the
repository root from its own location and reads `runs/` from there, so
`python scripts/reads/r33-read.py r33-s1 r33-s2` works from any directory. They are kept
because entries cite them by name and a reading has to be reproducible; they are not general
instruments, since a reader written for one round assumes that round's columns.

Three readers from the speed work of 2026-10-05 (`logbook/specs/nursery-speed.md`) take folders
or times instead of arm names. `nursery-digests.py` compares nursery run folders file by file and
exits 0 when identical, 1 when a file differs and 3 when it could not compare; a caller treats any
code but 0 and 1 as no verdict. `nursery-profile-read.py` reads a run's `profile.jsonl`, written
when `EVOSIM_NURSERY_PROFILE=1`. `temp-curve.py` summarises the processor's temperature over a
window from MSI Afterburner's one-second log.

`nursery-rank.py` is the exploration's test of a change to the nursery's world
(`proposals/propose-speed-exploration.md`): the rank correlation and the elites kept between two
runs' generation 0 on the same genomes, against a floor recovered from a 16-draw and a 32-draw run
of today's world. It exits 3 when it could not read.

`r58-hollows-read.py` reads round 58's hollows screens against D155's bar for a material change:
the settled corpses' joules per square metre inside the pits against outside, the born bodies within
2 m of a pit floor by guild against the bed's share, and the living and guild shares against round
57's three seeds, each beside H0 (round 57 seed 2) on the same cells. The pits are found from the
beds, and the corpses from a one-second `--film-window` at the reading second, since nothing else
records where they lie. Its definitions were committed before the first screen was read.

`group-resume-compare.py` is round 58's replay read: a migrating group resumed from a checkpoint
against its whole run, world by world, after the resume's second. It takes the second and the
run folders in pairs, and exits 0 when every world is the same and 1 when one differs. Its
docstring names the three things it leaves out by construction (the steering sampler, the stats'
wall timings and two harness counters) and why.

`open-ocean-vectors.py` is not a read of a run. It draws the open ocean's 25 wave vectors by the rule
`logbook/specs/open-ocean-current-build.md` states, and prints the table `CurrentField.Ocean.cs` carries
with the widest gap between directions.

`src/Evosim.OceanTracers` screens a world before any life runs in it. It is a small .NET 8 console that builds a
world's floor, current and islands from a launcher's resolved settings, as the simulation does. It releases
tracers over each island and counts where the water takes them: over their own island, over another, or over
the desert. With `--maps FIRST-LAST` it lists each map seed's islands instead, as `r61-maps.py` does for a
founded run. It lives under `src/` so that its build lands in `artifacts/`.
