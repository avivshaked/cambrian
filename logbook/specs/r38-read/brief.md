# Read brief: round 38, the tank four times as wide (logbook/0100's D1 to D8), 2026-09-15

Read `CLAUDE.md` (the Commands section, the gotchas from "Watch a round in the theatre" to
the end) and `logbook/0100-a-tank-four-times-as-wide.md` in full (what it asks, the world,
the rules before scoring, D1 to D8, the two-sided readings, the launch section with the
live looks), then `logbook/0099-ten-seeds-one-world.md` for the shape of the read this
one is compared against, and `logbook/specs/r37b-read/` and `logbook/specs/r37b-fresh-read/`
for the files those reads left behind: they carry round 37b's ten seeds' numbers ready to
use (seeds 1 to 5 in the first, 6 to 10 in the second) and the scripts that made them.
Round 38 is compared against all ten.

Rules: write only under `D:\Projects\experiments\evolution-simulator\logbook\specs\r38-read\`
(tables, TSVs, extracted numbers, the scripts you write) and nowhere else, never the
session scratchpad or TEMP; no `.md` files (the entry is written by the caller from your
final message); no commit; no Unity; do not touch `runs/` except to read it, and do not
stop, kill or launch anything. Never sleep, poll or wait. Use absolute paths; the working
directory resets between tool calls. Call the PowerShell scripts from `pwsh` with real
arrays for `-Columns` (a comma list from bash names no column and prints `?`).

The arms are `r38-s1` to `r38-s5` under `D:\Projects\experiments\evolution-simulator\runs\`.
All five should read `ended` with reason `budget` at 30,000 s; if any reads otherwise,
say so first and read it at its last sample as 0100's V5 says. Renders may be running on
some workers and write only under `scratch/snaps/`; ignore them.

## The world, so the numbers are read right

A tank of 400 m² (radius 11.28 m, 60 m deep) with the same 6,000 units of matter that the
100 m² tank of round 37b held, so a quarter of the density; the four ring patches `p0`
(centre) to `p3` (rim) are equal in area; the reader counted 402 live columns at 1 m. The
current is the streams at 0.1 m/s with the acceleration force on. Corpses decay at
0.005/s. The table carries `det cv` and `mat cv`, the fields' coefficient of variation,
for the first time.

## Instruments

- `pwsh -File scripts/analyse-arm.ps1 <arm> -ListColumns` first, then
  `-Timeline -Every 1000 -Columns alive,cols,'cols abs','x sd',p0,p1,p2,p3,absorpt,inherit,'jnt inh','mean m/s',diverged,wraps,crowded,stillb,births,'mat blk','mat short',audit,'mat resid','det cv','mat cv','det deep',corpses`
  (never index columns by position; call from `pwsh` so the array binds).
- `pwsh -File scripts/clade-score.ps1 r38-s1 r38-s2 r38-s3 r38-s4 r38-s5`; quote each
  verdict line whole, with its duration, floor and reading segment, and each passing
  clade's kind, founding time and size, and for a failing seed the best failing clade and
  its failed clauses.
- `python scripts/positions-read.py <arm> --summary` and `--at 5000,15000,30000` (pictures
  under `scratch/positions/<arm>/`, the reader's own default, allowed). The reader's
  three-dimensional nearest-neighbour median is D3's number; its `cols` and `x sd` are
  D4's beside the table's.
- `python scripts/reads/diverged-read.py <arm>` for any dumps (`runs/<arm>/<run>/diverged/`);
  the trace files beside them for V4.
- Round 37b's read scripts, reused as they are or adapted: `nn_matched.py` (in
  `logbook/specs/r37b-fresh-read/`), `exposure.tsv`'s method, `cycle.tsv`'s method,
  `pace.tsv`'s normalisation, `p3-share.tsv`, `eaters.tsv`, `stillbirths.tsv`, `fields.tsv`.
- Every manifest's `simHash`, `coreHash`, `configHash`, `physicsJobWorkers`, and each
  `runs/<arm>/prereg.json`'s commit, against 0100's V1 and its launch section
  (`simHash ecc41ec5…`, `coreHash 16073c69…`, `configHash 30637fb0`, prereg `4cab313`).

## The readings, prediction by prediction (0100's table is the authority on thresholds)

1. **V1 to V5** before anything: the header tokens and hashes per seed, `audit` and
   `mat resid` on every row, `wraps` on every row, a trace file beside every dump with a
   finite frame before its first non-finite step, and each manifest's status and reason.
2. **D1**: births by 1,000 s per seed (at least 40 in every seed) and `alive` at 5,000 s
   (at least 300 in 4 of 5); `mat blk` and `mat short` against `births` over the first
   3,000 s beside it.
3. **D2**: `alive` at 30,000 s per seed, between 700 and 2,300 in 4 of 5; also the mean,
   the minimum and the maximum after 5,000 s, as `summary.tsv` did.
4. **D3**: the three-dimensional nearest-neighbour median at 15,000 and 30,000 s per seed,
   matched by abundance against the 37b seed (of ten) with the nearest `alive` at that
   time, the ratio between 1.3 and 1.9 in at least 3 of the anchors that match; say which
   anchors match (within 15% on `alive`) and which do not. Beside it the depth band's
   shape from the positions file (the median depth and the interquartile range of the
   living, and of the absorptive bodies alone) against 37b's, since a crowd that packs
   the lit band would read D3 low for a reason the reading should name.
5. **D4**: the rim quarter (`p3` over `alive`) at 5,000, 15,000 and 30,000 s per seed, 15
   to 40% in 4 of 5; `cols` at least 60% of 402 from 5,000 s on (every 1,000 s row); `x sd`
   between 4.0 and 6.8 m at the three times. The pooled radial drift over 100 s per seed
   against the fully-mixed bound (0097's method, in `r37b-read/`), as a reading.
6. **D5**: inherited absorptive bodies (`inherit`) at the last sample per seed, at least 10
   in at least 3 of 5; D063 as amended from the scorer, read and not required. **The eaters'
   whole course is the round's finding, so read it fully**: per seed, `absorpt` and
   `inherit` at every 1,000 s, the peak and its time, the trough after the peak and its
   time, the count at the end, and whether a second boom followed a bust (seed 2's did:
   467 inherited at 10,000 s, 1 at 20,000, 300 at 30,000; seed 1 fell from 409 to 2 and
   stayed). Then from `lineage.jsonl` (stream it; it is large): whether the second boom is
   the same clade as the first (parent chains) or a fresh line from a leaf, and the
   generation span of each boom. And the eaters' depth against the leaves' at the peak and
   at the trough from `positions.jsonl`. Compare with 37b's ten (peaks 331 to 972, 3 to 7%
   of the living at the end).
7. **D6**: seeds holding at least 10 inherited jointed bodies at 30,000 s (0 to 2 of 5
   holds); the jointed count's peak and its time per seed; the count at every 5,000 s.
8. **D7**: `diverged` per seed and per million jointed body-seconds (0 to 10 in 4 of 5),
   the exposure table as `exposure.tsv`; for every dump the anatomy (parts, masses, the
   ratio, age, jointed, distance to the glass, steps since a resize) and whether every
   dump is a jointed body under 10 s old with a mass ratio under 3; and for every trace
   whether a finite frame precedes the first non-finite step (this is the first round
   whose trace ring was rebuilt to keep them).
9. **D8**: wall seconds per 1,000 simulated seconds per living body (the mean `alive` over
   the run) per seed, within 0.7 and 1.6 of 37b's ten-seed mean of the same statistic
   (compute it from `pace.tsv` in both 37b directories and `summary.tsv`'s mean alive).
   Say how many arms and renders shared the machine: 0100's launch section has the launch
   times (seeds 1 and 2 at 00:25, seed 3 at 03:09, seed 4 at 03:51, seed 5 at 10:18; round
   37b's fresh renders ran until about 03:50; seed 1's render from 10:50), and the
   manifests have the ends.
10. **Recorded and not predicted**: `det cv` and `mat cv` at every 1,000 s per seed (the
    first reading on any round; give the range after 5,000 s and the value at the end,
    and say whether either ever rises above 0.5 after 5,000 s); `corpses` and `det deep`
    at the three times; stillbirths against births at the three times and at the end
    (seed 1 read 1,146 at the end, seed 2 317; 37b's ten read 0 to 709); `crowded`;
    `mean m/s` (still the water's); the founding time and kind of every passing clade;
    `det patch sd` and `patch max share`.
11. **The cycle**: local extrema of `alive` after 5,000 s per seed as `cycle.tsv`, with the
    peak-to-trough ratio against 37b's 1.1 to 2.4.
12. **The two-sided readings** of 0100: for whichever prediction fails, produce the numbers
    its branch names (D2 low: where the food gathers from `det cv` and the positions; D3
    low: the depth band against 37b's; D3 high: the eaters' distribution; D5: the eaters'
    course, already in item 6; D6 high: the jointed clades' founding; D7: the anatomy and
    the trace; D8: say the profile is not available).

## Files to leave

`timeline-key.tsv`, `timeline-every1000.txt`, `verdicts.txt`, `headers.tsv` (V1 per seed),
`positions-r38-s*.txt`, `p3-share.tsv`, `drift.tsv`, `nn-matched.tsv`, `depth-band.tsv`,
`eaters-course.tsv` (every 1,000 s), `eaters-booms.tsv` (peak, trough, end, second boom,
clade identity, generation span, depths), `cols.tsv`, `diverged.txt` and
`diverged-anatomy.tsv` (empty files if there are no dumps, saying so), `exposure.tsv`,
`cycle.tsv`, `pace.tsv`, `stillbirths.tsv`, `fields.tsv`, `fieldcv.tsv`, and any script
you wrote.

## Final message

The V1 to V5 check, the D1 to D8 table with holds and fails per seed and the counts, the
eaters' course and the booms table with your reading of whether the bust is a matter
crash, a shading collapse or something else the columns can name, the matched-abundance
comparison with the depth band, the drift table, the exposure table, the cycle table, the
pace table, the stillbirths, fields and field-cv tables, and any place the data would not
answer the question and why. Numbers as measured; no rounding beyond the instruments'
own. Under 2,200 words.
