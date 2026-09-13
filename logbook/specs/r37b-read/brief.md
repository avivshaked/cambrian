# Read brief: round 37b, the water carried as water (logbook/0095's M0 to M8), 2026-09-13

Read `CLAUDE.md` (the Commands section, the gotchas from "Watch a round in the theatre" to
the end) and `logbook/0095-the-water-carried-as-water.md` in full (the world, the nine
predictions, the two-sided readings and the launch section), then
`logbook/0094-the-glass-the-centrifuge-and-the-throws-that-stopped.md` for the shape of the
read this one is compared against and `logbook/specs/r37-read/` for the files that read left
behind, which carry round 37's and round 36's numbers ready to use.

Rules: write only under `D:\Projects\experiments\evolution-simulator\logbook\specs\r37b-read\`
(tables, TSVs, extracted numbers, the scripts you write) and nowhere else, never the session
scratchpad or TEMP; no `.md` files (the entry is written by the caller from your final
message); no commit; no Unity; do not touch `runs/` except to read it, and do not stop, kill
or launch anything. Never sleep, poll or wait. Use absolute paths; the working directory
resets between tool calls. Call the PowerShell scripts from `pwsh` with real arrays for
`-Columns` (a comma list from bash names no column and prints `?`).

The arms are `r37b-s1` to `r37b-s5` under `D:\Projects\experiments\evolution-simulator\runs\`.
Their controls are `r37-s1` to `r37-s5` (M0, M1's drift, M4, M6, M7, M8) and `r36-s1` to
`r36-s5` (M3, M5). Seeds 1 to 4 read `ended` at 30,000 s. **Seed 5 reads `stopped`
(`manual-stall`): it wedged at 29,200 s and was stopped**, so it is censored 800 s short;
read it at its last sample (29,200 s) wherever a prediction says 30,000 s, mark every such
number `censored at 29,200 s`, and count it toward a "3 of 5" only when its reading at the
last sample would meet the threshold, saying so. The scorer prints `CENSORED` for it with
`t=?` (the stop does not write the simulated seconds; take 29,200 from the report). Renders may be running on some workers and write only
under `scratch/snaps/`; ignore them.

## Instruments

- `pwsh -File scripts/analyse-arm.ps1 <arm> -ListColumns` first, then
  `-Timeline -Every 1000 -Columns alive,cols,'cols abs','x sd',p0,p1,p2,p3,'jnt inh','mean m/s',diverged,wraps,crowded,stillb,audit,'mat resid','det patch sd','patch max share',corpses`
  (never index columns by position; call from `pwsh` so the array binds).
- `pwsh -File scripts/clade-score.ps1 r37b-s1 r37b-s2 r37b-s3 r37b-s4 r37b-s5`; quote each
  verdict line whole, with its duration, floor and reading segment.
- `python scripts/positions-read.py <arm> --summary` (the main tree's reader is the
  corrected one since the streams merge: `cols` counts the columns whose centres are inside
  the circle, and the summary carries the radial histogram and the per-guild depths);
  `--at 5000,15000,30000` for the per-sample readings and pictures under
  `scratch/positions/<arm>/` (that directory is the reader's own default and is allowed).
- `python scripts/reads/diverged-read.py <arm>` for any dumps (`runs/<arm>/<run>/diverged/`; at
  most 50 are written); from this build every dump has a `<id>-trace.json` beside it with
  the three-frame ring, the masses and the ratios; the reader prints them.
- Round 37's read scripts, reusable as they are or adapted: `logbook/specs/r37-read/dispersal.py`
  (the drift table by starting radius), `nn_matched.py` (the matched-abundance nearest
  neighbour), `cycle.tsv` and the method that made it (`analyse.py`), `exposure.tsv`'s
  method for jointed body-seconds. Round 37's and round 36's per-seed numbers are in that
  directory's TSVs and `positions-r3*-s*.txt`; take them from there rather than recomputing,
  and say so.

## The readings, prediction by prediction (0095's table is the authority on thresholds)

1. **M0**: `alive` at 30,000 s per seed against round 37's same seed and the ratio (0.5 to
   1.5 holds); the scorer's verdict per seed, read but not required.
2. **M1**: `p3` over `alive` at 5,000, 15,000 and 30,000 s per seed (15 to 40% holds in at
   least 3 of 5), the whole `p3` share timeline every 1,000 s as a TSV, and the radial
   histogram from the positions file at the three times. Then the drift: the mean change of
   a body's radius over 100 s, binned by starting radius (1 m bins inside 4 m), over the
   whole run, per seed, from consecutive positions samples (`dispersal.py`'s method); within
   ±0.5 m in every bin inside 4 m in at least 3 of 5 holds; round 37 read +1.1 to +3.5 m.
3. **M2**: `cols` (corrected) at 5,000 s onward, at least 60 of the live columns in at least
   3 of 5; `x sd` at the three times, 2.0 to 3.4 m in at least 3 of 5 (a spread disc reads
   2.82 m). Report the report's `cols` and the reader's side by side once to show they now
   agree.
4. **M3**: the three-dimensional nearest-neighbour median at matched abundance against round
   36's same seed (`nn_matched.py`: samples where `alive` is within 10%, nearest in time to
   the three named times); within 0.8 to 1.25 in at least 3 of the anchors that find a
   match holds; round 37 read 0.33 to 0.67 at every anchor. Report the raw medians and the
   mean depth and its spread beside them.
5. **M4**: `jnt inh` at 30,000 s per seed; the number of seeds holding at least 10, within
   one of round 37's one (seed 3 with 148). Also the jointed count's peak and time per seed.
6. **M5**: `mean m/s` at 5,000 and 30,000 s against round 36's same seed; 0.7 to 1.3 in at
   least 3 of 5 holds; round 37 read 0.68 to 0.71.
7. **M6**: `diverged` per seed (0 in at least 4 of 5 holds), and for every dump that exists,
   whether a trace file is beside it and what it says (parts, masses, the mass ratio, the
   root's height and radius at the dump, age, jointed or not, steps since the last resize,
   which guard named the death, and whether the body sat within a metre of the glass). The
   exposure: jointed inherited bodies per sample summed over samples times the sampling
   interval gives jointed body-seconds per seed; divergences per 10⁶ jointed body-seconds
   beside round 37's (0, over 9.17 million) and round 36's (131 over 6.91 million).
8. **M7**: the peak-to-trough ratio of `alive` over t ≥ 5,000 s per seed, by `cycle.tsv`'s
   method (peaks and troughs with their times and the `p3` share at each); below 2.5 in at
   least 3 of 5 holds; round 37 read 1.9, 3.3, 5.4, 1.9, 3.2.
9. **M8**: wall-clock seconds per 1,000 simulated seconds per seed from the manifest's
   timestamps (`run.json`: the start and end times; the report footer's wall-clock minutes
   as a check), against round 37's same seed; within 1.5 holds. Say how many arms shared the
   machine in each case (round 37 ran with five arms and renders; 37b's seeds started hours
   apart, `0095`'s launch section has the times), since the pace is the machine's as much
   as the build's.
10. **The two-sided readings** of 0095: for whichever prediction fails, produce the numbers
    its branch names (the rim above 40% with the drift outward; the rim below 15%; M1
    holding and M2 failing on `x sd` with clumps in angle, read from the positions file's
    azimuthal histogram; M3 failing with M1 holding, read against depth; M6 failing, the
    traces' anatomy; M7 failing with M1 holding, the producer canopy and `det deep`; M8
    failing, the per-step profile is not available, say so).
11. **The eaters**: `absorpt` and `inherit` (the absorptive count and the inherited
    absorptive count) at the three times per seed, and each seed's passing clade from the scorer
    (kind, founding time, size), so the entry can say what the standing populations are.
12. **Where the corpses and the detritus are**, for the pockets question: `det patch sd`,
    `patch max share`, `corpses` and `det deep` at the three times per seed, and the
    positions file's depth histogram beside them.

## Files to leave

`timeline-key.tsv` (the column map), `timeline-every1000.txt`, `verdicts.txt` (the scorer),
`positions-r37b-s*.txt`, `p3-share.tsv`, `drift.tsv`, `nn-matched.tsv`, `radial-hist.tsv`,
`diverged.txt` and `diverged-anatomy.tsv` (empty files if there are no dumps, saying so),
`exposure.tsv`, `cycle.tsv`, `pace.tsv`, `eaters.tsv`, `fields.tsv`, and any script you
wrote.

## Final message

The M0 to M8 table with holds and fails per seed and the counts, the drift table, the
matched-abundance comparison, the exposure table, the cycle table, the pace table, the
eaters and fields tables, and any place the data would not answer the question and why.
Numbers as measured; no rounding beyond the instruments' own. Under 2,000 words.
