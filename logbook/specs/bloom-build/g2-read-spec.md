# G2's read: the bloom screen's gates and story material

The specification of `scripts/reads/bloom/g2-read.py` and the farm command it needs, `--grazer-traits`. It implements
G2 in `logbook/specs/grazer-film-plan.md` as amended at `bbcd187a`, with the definitions the coordinator approved on 2026-10-28. It reads a run's files only, and steps nothing.

## Inputs

- A RUN is an arm folder (the latest dated run is read) or a run directory. A run that resumed another is read as one
  world, as `r61-grazer-economy.py`'s `Economy` reads it.
- `lineage.jsonl`, `stats.jsonl`, `positions.jsonl.gz`, `config.json`, the report's header, and the trait TSV that
  `--grazer-traits` writes.
- `--end` (24,000), `--from` (6,000), `--window` (3,000). The judged windows are [6,000, 9,000) to [21,000, 24,000],
  six of them.

## Definitions, reused rather than restated

- **A grazer** is a body born mouthed, with `atk` 1 and `ink` 1 in its birth row. That is `r61-grazer-economy.py`'s
  definition; import it from there.
- **D222's tests and D244's floor** come from the scripts that read `r62cf`'s verdict: `r61-grazer-economy.py` and
  `r62-health.py`. Import their functions; do not re-derive the tests. Print each test per window, the verdict at the
  floor of 30, the verdict at 40 beside it, and the strict verdict (every window judged), as D244 asks.
- **The islands** are D251's: the influx plan's islands, labelled as the build labels them. Their column count,
  centroid and r_eff come from the report's header. A grove is the disc of radius r_k + 10 m round island k's
  centroid. In a Box world each axis takes the shorter way round (D204).

## Gates

1. **Grazers survive.** At least 30 grazers alive in every judged window, under D244's floor and D222's tests. A
   window's grazer count is the fewest grazers alive at any stats sample in it. Print that count and the mean.
2. **The bloom reaches the food** (arms B to D). Judge only islands of 5 or more columns whose mean `bloomLeafArea`
   over the judged span is at least 1 m².
   - For each such island, find the Pearson correlation between `bloomFactor_k(t)` and `bloomLeafArea_k(t + lag)`
     over the judged span, for every lag from 0 to 3,000 s in steps of the stats cadence. Report the best lag and its
     correlation.
   - It passes when every judged island reaches at least 0.5.
   - Print each island's row, judged or not, and say why an island was left out.
3. **Grazers evolve.** At least one trait shifts by at least one founder standard deviation between the first and
   last judged windows.
   - The traits come from `--grazer-traits`: body mass, part count, limb push, and the brain's mean drive. The drive
     is read in two states, at rest and fed (below), and the gate takes the larger of its two shifts.
   - The sample is the grazers born in each window. The founder SD is the trait's standard deviation over every
     founder grazer: a grazer whose birth row has `p` −1, or whose source is the floor, the trickle or the pool.
   - **The scale has a floor.** Founders are often clones of a few inocula, so their SD can be near zero, and any
     drift would then pass. The scale is max(founder SD, the SD of the first window's grazers). Print which was used.
   - The shift is |mean(last) − mean(first)| / scale. Print every trait's shift, both means, both counts, both SDs and
     which one is the scale.
   - A window with fewer than 30 grazer births is not used. Then use the nearest judged window that has 30. Say which.

## Reported, not gated

4. **The world is rich.**
   - Grazer clades: a clade's root is a grazer birth whose parent is not a grazer, or that has no parent, and every
     grazer descended through grazers belongs to its root's clade. Print, for each window, how many clades hold at
     least 10% of the living grazers. The test is that three clades do so in some window.
   - "Something eats grazers or their remains": count the kill rows whose victim is a grazer, by whether the attacker
     is a grazer. Count the bites on grazers' remains if the record has them, and say if it does not.
5. **Story material.**
   - **Crossings and strolls.** Walk each grazer's position fixes in time, as `grazer-moves-read.py` walks them, with
     the groves above in place of its islands.
     - A move goes from grove A to grove B, B not A. Fixes outside every grove between them do not reset A.
     - A move is a crossing when A's and B's groves do not touch: the distance between their centroids is more than
       r_A + r_B + 20 m. Every other move is a stroll.
     - A point inside two groves belongs to the nearer centroid.
     - Print per window both counts, the counts per grazer life, and each crossing's median distance and duration.
   - **Crowds at blooms.** For each island and each stats sample, count the grazers inside its grove. Print, per
     island, the correlation of that count with `bloomFactor_k` at the best lag from 0 to 3,000 s, and that lag. The
     lag is how fast grazers gather at a bloom.
   - **Busts and recoveries.** In the whole world's grazer count at each stats sample: a bust is a fall to half or
     less of the highest count in the 1,500 s before it. A recovery is a return to that earlier highest count. Print
     each bust's time, depth and recovery time, or "none".
   - **The four parts of story material:** crossings; the islands whose crowd correlation is at least 0.3;
     recoveries; and the largest trait shift in gate 3, in scale units, since a lineage that changes is the film's
     spine.

## The ranking

The coordinator ruled this on 2026-10-28. Given several runs, the script ranks the arms that pass every gate.
- On each of the four parts, arms are ranked by the mean of their seeds, with rank 1 the most.
- The ranks are summed, and the lowest sum wins.
- A tie goes to the simplest arm, in the order A, B, C, D.
- An arm is the run's folder name less its seed suffix. The script prints the table of parts, ranks and sums.

## Output

One block per run, in this order: header (run, arm, seed, hashes), gates 1 to 3 with PASS or FAIL and the numbers
behind each, then 4 and 5. Finally a one-line summary per run: the three gate verdicts and the story parts.

## The farm command: `--grazer-traits <run dir> <out tsv>`

Read-only. It reads `genomes.jsonl.gz` and the run's `config.json`, and writes one row per genome whose id is a
grazer in `lineage.jsonl`. The world's own code builds each body.

- `id`.
- `parts`: the developed body's part count at birth, from `Developer` with the run's config and cell table.
- `mass_kg`: the adult body's mass, the birth body's mass times `adultScale`³, using the density the world uses. Name
  the expression in a comment.
- `jointed`: the jointed parts.
- `limb_push`: the sum of the node's `Power` gene over the developed body's jointed parts.
- `drive_rest` and `drive_fed`: the brain's mean drive, |output| averaged over every output that drives a joint.
  Run the creature's own brain open-loop for 10 s at the world's physics step.
  - At rest, every sense reads 0.
  - Fed, the leaf-smell strength reads 0.8, the energy sense reads 0.5, and every other sense reads 0.
  - A brain at rest often gives a constant, which is why there are two states.
  - If the brain cannot run without the physics, the fallback is the mean over joint-driving neurons of
    `Amplitude` × (frequency > 0) + |`Bias`|, in both columns. The header line the command prints says which was
    used.

A test holds `--grazer-traits` on a small recorded fixture: the parts and the jointed count equal what the world
developed for those ids.
