# Round 56's predictions: the sun starts to move

*2026-10-04, written before round 56's first seed and committed before it starts. It rests on the owner's
rulings of 2026-10-04 (HANDOFF item 1b, `proposals/propose-r56-light-cycles.md`), on the groundwork in
`logbook/specs/r56-light/` and on round 55's seeds while they ran. Every number is computed, measured or guessed,
and each says which.*

## What it asks

Every round so far has had a sun that never moved. D035 built a day and night long ago and every farm
round ran it at zero. In that steady light one leaf lineage takes each tank and holds it. By 10,000 s the
largest root lineage held 57% of round 54 seed 2's living and 98% of round 55 seed 3's (measured by this
round's reader). Round 56 asks whether a light that changes over a lifetime and over generations loosens
that hold. It asks whether the seasons bite (S1), whether the jointed bodies gain ground (S2), and whether
the leaves come to keep more in reserve (S3).

## The world

Round 55's world (`rounds/env-r55.ps1` on `round-55`), with the light cycled, in `rounds/env-r56.ps1` on
`round-56`:

- **A day of 1,000 s at amplitude 0.5**: thirty days a run. Each night is about 0.4 of a leaf's median
  life of 1,250 s (measured on round 55 seed 2).
- **A season of 10,000 s at amplitude 0.5**, multiplying the day: three years a run, ten days a season. The
  light runs between a quarter and two and a quarter times its mean and averages to it over each season.
  The run starts at the mean and rises into its first summer, whose peak is at 2,500 s and whose deepest
  winter is at 7,500 s.
- **The seeds keep round 55's roles**: seed 1 without the nursery's genomes, seeds 2 and 3 with them. With
  the season at zero the new build replays round 55 to the bit, so each seed's baseline is round 55's same
  seed and the round needs no other control.
- **No light sense.** No creature can see the light; a body feels night only as its reserve falling. A
  sense that mutation can add and no founder carries is ruled for round 57.

The cycle preserves the mean light and not the mean income. A leaf earns the lesser of its light and its
matter uptake, and 63 to 81% of leaf body-steps in rounds 54 and 55 were bound by uptake (measured). Such a
leaf cannot use a bright noon and still suffers a dark night, so round 56 is a poorer world as well as a
varying one.

## The launch, staged

Seed 2 starts first, beside round 55's control seed, at 8 threads each. At 10,000 s, after its first
winter, it is read against the gate G below before seeds 3 and 1 start. If G holds, seed 2 stops at a
checkpoint once round 55's control has ended, and resumes beside seeds 3 and 1, all three sharing 16
threads. A resume writes the same rows as the unbroken run. A farm run is the same at any thread
count, and the checkpoint check (`--verify-checkpoint`) is run on a round-55 world before seed 2 is stopped.
If G fails, nothing more starts and the owner rules.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| G | **the first winter does not empty the tank**: seed 2's living at 10,000 s are at least a quarter of round 55 seed 2's at 10,000 s (2,031, measured). The ledger has a typical leaf still earning more than it spends at the winter's trough, and the trickle refounds whatever dies. I guess this holds | stats.jsonl |
| S1 | **the seasons bite**: in years 2 and 3, the fewest living in the year's second half are at least 20% below the most in its first half, in both years, in 3 of 3 seeds. Without seasons the tank grows through those years: the reader finds the fewest within 1% of the most in round 54, every seed and year. I guess this holds | the same |
| S2 | **the jointed gain ground**: the jointed share of the living at 30,000 s at least twice round 55's same seed, in at least 2 of 3 seeds. Round 55's seeds 2 and 3 held 9 and 14 jointed bodies at 15,000 s, so a few bodies move this bar, and I set it at two seeds for that reason. The swimmers pay for their joints in the same dark, so I guess this fails | the last stats row |
| S3 | **the leaves keep more in reserve**: the median breeding margin of the living leaves at 30,000 s is above round 55's same seed, in 3 of 3 seeds. A margin carries a leaf through a night; the groundwork computes a typical grown leaf needing about 100 s of reserve for a night at this setting, against 360 or more held. The medians differ from seed to seed (154 to 362 s at 10,000 s in round 55, measured), so chance alone passes this one time in eight. I guess this holds | lineage.jsonl's birth rows |
| B1 | **the books balance**: the audit under 0.1 J and the matter residual under 1e-5 units at every sample, nothing diverges, and every seed ends on its budget, in 3 of 3, read by round 54's reader | the stats rows, run.json |

## The readings

- The living and the jointed every 2,500 s, with the light factor at each, so a reader can see where in
  the year a crash or a boom falls.
- The leaves' median margin and median lifespan gene at 10,000, 20,000 and 30,000 s.
- The largest root lineage's share of the living at the same times, against round 55's same seed.
- Round 55's own clauses (I1, I2, I3) on seeds 2 and 3 by round 55's reader, as readings.
- The pictures: the theatre at a summer's peak and a winter's trough of seed 2.

## What the round does not ask

Whether a body can learn the time: it has no sense to learn it with. Whether the cycle changes where in the
water the leaves live: the reader does not read depth, and a depth reading waits for the light sense.
Whether a cycle without the income cut would do the same: the round runs one setting.

## The reader

`scripts/reads/r56-read.py` reads every clause but B1, each seed against round 55's same seed, and with
`--gate` it reads G alone. Run on rounds 54 and 55 against themselves, it fails every clause, as a world without
seasons should.

## Before the launch

The pre-registration's record is the commit that adds this entry, on a clean tree, merged into
`round-56`. Every seed's manifest names it as `gitCommit`, with no DIRTY mark. The build is the branch
`round-56`, launched from `scratch/wt-r56` into the main tree's runs, on the CPU at 8 threads a seed
within the owner's limit of half the machine. Before the launch the fixtures are re-recorded for the
season's two new tunables and the suites run on them.
