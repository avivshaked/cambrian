# Read brief: round 37, the tank (logbook/0093's M0 to M6), 2026-09-12

Read `CLAUDE.md` (the Commands section, the gotchas from "Watch a round in the theatre" to
the end) and `logbook/0093-the-water-gets-a-wall.md` in full, then `logbook/0092-the-hinge-that-stood-was-on-an-eater.md`
for the shape of a read and `logbook/specs/r36-read/` for the files a read leaves behind.

Rules: write only under `D:\Projects\experiments\evolution-simulator\logbook\specs\r37-read\`
(tables, TSVs, extracted numbers, the scripts you write) and nowhere else, never the session
scratchpad or TEMP; no `.md` files (the entry is written by the caller from your final
message); no commit; no Unity; do not touch `runs/` except to read it, and do not stop, kill
or launch anything. Never sleep, poll or wait. Use absolute paths; the working directory
resets between tool calls. Call the PowerShell scripts from `pwsh` with real arrays for
`-Columns` (a comma list from bash names no column and prints `?`).

The arms are `r37-s1` to `r37-s5` under `D:\Projects\experiments\evolution-simulator\runs\`;
their controls are `r36-s1` to `r36-s5`. Every manifest must read `ended` before you begin;
a manifest reading `error` is censored and read as such.

## Instruments

- `pwsh -File scripts/analyse-arm.ps1 <arm> -Timeline -Every 1000 -Columns alive,cols,'x sd',p0,p1,p2,p3,'jnt inh','mean m/s',diverged,wraps,crowded,stillb,audit,'mat resid'`
  and `-ListColumns` first: never index columns by position.
- `pwsh -File scripts/clade-score.ps1 r37-s1 ... r37-s5` (the verdict line now ends with a
  duration, floor and reading segment; quote it whole).
- **The positions reader from the branch, not the main tree**:
  `python D:\Projects\experiments\evolution-simulator\scratch\wt-streams\scripts\positions-read.py <arm> --summary --runs-root D:\Projects\experiments\evolution-simulator\runs`
  (the main tree's copy counts `cols` on the wrong columns in a tank; the branch's counts
  the columns whose centres are inside the circle, and its summary carries the radial
  histogram). `--at 5000,15000,30000` for the per-sample readings.
- `python D:\Projects\experiments\evolution-simulator\scratch\wt-streams\scripts\diverged-read.py <arm>`
  for the dumps (`runs/<arm>/<run>/diverged/`; at most 50 are written, so a count above 50
  is censored at the dumps and the column is the count). Round 37's build has no trace
  files; the reader prints what it has.

## The readings, prediction by prediction

1. **M0**: `alive` at 30,000 s per seed against round 36's (1,154 / 1,624 / 1,342 / 1,329 /
   1,650) and the ratio; the scorer's verdict per seed.
2. **M1**: `p3` over `alive` at 5,000, 15,000 and 30,000 s per seed, and the whole timeline
   every 1,000 s as a TSV; the radial histogram from the positions file at the three times.
3. **M2**: `cols` at 5,000 s onward from the branch reader (the report's `cols` overcounts;
   quote both and say which is which); `x sd` at the three times.
4. **M3**: the median nearest neighbour at 5,000 and 30,000 s per seed against round 36's
   same seed, raw, **and at matched abundance**: for each seed find the samples in the two
   rounds where `alive` is within 10% of each other (nearest in time to the three named
   times) and compare the medians there; report both, with the depth distribution (mean
   depth and its spread from the positions file) beside them, since neighbour distance
   moves with abundance and depth before it moves with anything else.
5. **M4**: `jnt inh` at 30,000 s per seed; seeds holding at least 10 against round 36's one.
6. **M5**: `mean m/s` at 5,000 and 30,000 s against round 36's same seed.
7. **M6**: `diverged` per seed against twice round 36's same seed plus 2 (round 36: 33, 55,
   20, 6, 17). Then the exposure: from the report's timeline, jointed inherited bodies per
   sample summed over samples times the sampling interval gives jointed body-seconds per
   seed; print divergences per 10⁶ jointed body-seconds for round 37 and round 36 side by
   side. Then the anatomy from the dumps: how many parts, the root's height and its radius
   from the axis at the dump, the age, jointed or not, and whether the height or the radius
   guard named the death; a dump whose body sits within a metre of the glass is a wall
   candidate, and the share of those is the reading M6's failure branch asks for.
8. **The two-sided readings** of 0093: for whichever fail, produce the numbers that entry
   names (the dispersal clipping at the wall: births' radii from the lineage rows' patch
   against parents'; the radial velocity of bodies against the field's from consecutive
   positions samples; the wall candidates among the dumps).
9. **The population cycle**: the early read saw `alive` swinging 700 to 1,750 on a 10,000 s
   cycle; give the peaks and troughs per seed with their times and `p3` share at each.

## Files to leave

`timeline-key.tsv` (the column map), `timeline-every1000.txt`, `verdicts.txt` (the scorer),
`positions-r37-s*.txt`, `nn-matched.tsv`, `radial-hist.tsv`, `diverged.txt` and
`diverged-anatomy.tsv`, `exposure.tsv`, `cycle.tsv`, and any script you wrote.

## Final message

The M0 to M6 table with holds/fails per seed and the counts, the matched-abundance
comparison, the exposure table, the dump anatomy summary, the cycle table, and any place the
data would not answer the question and why. Numbers as measured; no rounding beyond the
instruments' own.
