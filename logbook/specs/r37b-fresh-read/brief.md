# Read brief: the fresh seeds, round 37b's world on seeds 6 to 10 (logbook/0098's F1 to F8), 2026-09-14

Read `CLAUDE.md` (the Commands section, the gotchas from "Watch a round in the theatre" to
the end) and `logbook/0098-a-second-draw-of-the-water.md` in full (what it asks, the world,
the rules before scoring, F1 to F8, the two-sided readings, the launch section), then
`logbook/0097-the-water-carried-and-the-tank-threw.md` for the shape of the read this one
is compared against and `logbook/specs/r37b-read/` for the files that read left behind:
they carry round 37b's five seeds' numbers ready to use, and the scripts that made them.

Rules: write only under `D:\Projects\experiments\evolution-simulator\logbook\specs\r37b-fresh-read\`
(tables, TSVs, extracted numbers, the scripts you write) and nowhere else, never the
session scratchpad or TEMP; no `.md` files (the entry is written by the caller from your
final message); no commit; no Unity; do not touch `runs/` except to read it, and do not
stop, kill or launch anything. Never sleep, poll or wait. Use absolute paths; the working
directory resets between tool calls. Call the PowerShell scripts from `pwsh` with real
arrays for `-Columns` (a comma list from bash names no column and prints `?`).

The arms are `r37b-s6` to `r37b-s10` under `D:\Projects\experiments\evolution-simulator\runs\`.
The comparison is round 37b's `r37b-s1` to `r37b-s5` (seed 5 censored at 29,200 s; take
its numbers from `logbook/specs/r37b-read/`, do not recompute). All five fresh seeds should
read `ended` with reason `budget` at 30,000 s; if any reads otherwise, say so first and
read it at its last sample as 0098's V5 says. Renders may be running on some workers and
write only under `scratch/snaps/`; ignore them.

## Instruments

- `pwsh -File scripts/analyse-arm.ps1 <arm> -ListColumns` first, then
  `-Timeline -Every 1000 -Columns alive,cols,'cols abs','x sd',p0,p1,p2,p3,'jnt inh','mean m/s',diverged,wraps,crowded,stillb,births,audit,'mat resid','det patch sd','patch max share',corpses`
  (never index columns by position; call from `pwsh` so the array binds).
- `pwsh -File scripts/clade-score.ps1 r37b-s6 r37b-s7 r37b-s8 r37b-s9 r37b-s10`; quote each
  verdict line whole, with its duration, floor and reading segment, and each passing clade's
  kind, founding time and size.
- `python scripts/positions-read.py <arm> --summary` and `--at 5000,15000,30000` (pictures
  under `scratch/positions/<arm>/`, the reader's own default, allowed).
- `python scripts/reads/diverged-read.py <arm>` for any dumps (`runs/<arm>/<run>/diverged/`).
- Round 37b's read scripts under `logbook/specs/r37b-read/`, reused as they are or
  adapted: the drift table by starting radius against the fully-mixed bound (0097's
  method, not the by-bin zero test), `nn_matched.py` (matched abundance against round
  36's same-numbered seed does not exist for seeds 6 to 10: match instead against round
  37b's seed with the nearest `alive` at each anchor, and say so), `cycle.tsv`'s method,
  `exposure.tsv`'s method for jointed body-seconds, `pace.tsv`'s normalisation.
- Every manifest's `simHash`, `coreHash`, `configHash`, `physicsJobWorkers`, and each
  `runs/<arm>/prereg.json`'s commit, against 0098's V1.

## The readings, prediction by prediction (0098's table is the authority on thresholds)

1. **V1 to V5** before anything: the header tokens and hashes per seed, `audit` and
   `mat resid` on every row, `wraps`, a trace file beside every dump, and each manifest's
   status.
2. **F1**: `alive` at 30,000 s per seed; between 1,250 and 2,300 in at least 4 of 5.
3. **F2**: the rim quarter (`p3` over `alive`) at 5,000, 15,000 and 30,000 s per seed, 15
   to 40% in at least 4 of 5; the pooled radial drift over 100 s per seed below 0.01 m
   (0097's method against the fully-mixed bound; 37b read 0.0025 to 0.0032).
4. **F3**: corrected `cols` at least 60 from 5,000 s and `x sd` 2.0 to 3.4 m at the three
   times, in at least 4 of 5.
5. **F4**: D063 as amended met in at least 3 of 5; the passing clade founded after 1,000 s
   in at least 2 of 5. Give kind, founding time and size for every passing clade.
6. **F5**: seeds holding at least 10 inherited jointed bodies at 30,000 s (between 0 and 3
   holds; 0 or 4 to 5 are the two-sided readings). Also the jointed count's peak and time
   per seed.
7. **F6**: `diverged` per seed and per million jointed body-seconds (0 to 10 in at least 4
   of 5), and for every dump the anatomy (parts, masses, ratio, age, jointed, distance to
   the glass, steps since a resize): every dump a jointed body under 10 s old with a mass
   ratio under 3.
8. **F7**: the three-dimensional nearest neighbour at matched abundance within 0.8 to 1.35
   of the comparison seed's in at least 3 of the anchors that match.
9. **F8**: wall seconds per 1,000 simulated seconds per living body within 0.7 to 1.4 of
   37b's five; say how many arms and renders shared the machine (0098's launch section
   has the launch times; the machine ran three to four arms and one to three renders).
10. **Recorded and not predicted**: stillbirths against births per seed at the three times
    and at the end (seeds 6 and 8 read 439 and 130 at the end, seed 7 read 0), `det patch
    sd` and `patch max share`, `corpses`, `det deep`, and the eaters' counts (`absorpt`,
    `inherit`) at the three times.
11. **The two-sided readings** of 0098: for whichever prediction fails, produce the numbers
    its branch names.

## Files to leave

`timeline-key.tsv`, `timeline-every1000.txt`, `verdicts.txt`, `headers.tsv` (V1 per seed),
`positions-r37b-s*.txt`, `p3-share.tsv`, `drift.tsv`, `nn-matched.tsv`, `radial-hist.tsv`,
`diverged.txt` and `diverged-anatomy.tsv` (empty files if there are no dumps, saying so),
`exposure.tsv`, `cycle.tsv`, `pace.tsv`, `eaters.tsv`, `stillbirths.tsv`, `fields.tsv`,
and any script you wrote.

## Final message

The V1 to V5 check, the F1 to F8 table with holds and fails per seed and the counts, the
drift table, the matched-abundance comparison, the exposure table, the cycle table, the
pace table, the eaters, stillbirths and fields tables, and any place the data would not
answer the question and why. Numbers as measured; no rounding beyond the instruments'
own. Under 2,000 words.
