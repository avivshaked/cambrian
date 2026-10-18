# Proposal: exploring the speed-ups that change results

Draft of 2026-10-05, for the owner. The owner asked that day for the speed-ups that change the
world or its numbers to be explored, not only the ones that leave every result as it was
(`logbook/specs/speed-plan.md`, classes NR and WR). Exploring here means measuring two things for
each option: how much faster it makes the farm or the nursery, and how far it moves what the
creatures do. Adopting any of them stays the owner's ruling, one at a time.

## The options

Gains are the speed plan's, marked as it marks them. "Config" means the world already has a setting
for it, so a trial needs no code. "Switch" means code behind a setting that is off by default, so
the build without it stays byte-identical.

| id | What changes | Program | Speed, as the plan has it | How it is tried |
|---|---|---|---|---|
| N1 | The water's velocity is read off the gradient the acceleration already computes, so each sample is taken once instead of twice | both | water a quarter cheaper: about 2% of a nursery episode, about 6% of the farm's wall (believed) | switch |
| N2 | A simpler sea floor: fewer bed shapes, or the floor's height and slope read from a table instead of computed. A world rule: the contacts feel the floor too (below) | both | 7 to 12% of the farm's physics, 3 to 4% of a nursery episode (believed) | switch |
| R2 | The water's acceleration sampled every few steps instead of every step | both | up to about 15% of the farm's wall (believed) | switch |
| W1 | The water at a part held for a moment (`WaterHoldSeconds` above 0), one drag panel a side (`PanelsPerAxis` 1), or no acceleration force (`FluidAccelerationCoefficient` 0) | both | the water half to two thirds cheaper, the drag 60% cheaper (believed) | config |
| S7 | The snow's eddy sum regrouped, the same water to rounding | nursery, and the farm until the snow moves to the card | about 15% of a nursery episode (believed) | switch |
| B3 | The snow carried every second world step instead of every one | nursery | about 1.3 times an episode (believed) | switch |
| B4 | The snow on 2 m cells instead of 1 m (`FieldCellMetres`) | nursery | about 1.6 times an episode (believed) | config |
| B1 | Sixteen bodies a tank instead of eight | nursery | up to 1.5 to 1.6 times a generation (believed) | config |

## How far each one moves the results

Each test sets the option's difference against a noise floor measured the same way, so the
threshold is written before the trial and no trial ends in a judgement call. Every trial records
its pace beside its results, so each option arrives at the owner as a pair: so much faster, and this
much different, against this much noise.

### The nursery: the same genomes swum in both worlds

One generation's 96 genomes are swum on the same seeds and tank draws, once in today's world and
once with the option on. Selection only cares about order, so the measure is the rank correlation
(Spearman) of the 96 genomes' scores between the two worlds, and how many of the 12 that would be
kept as elites stay among the 12.

The noise floor is the same 96 genomes swum in today's world on a second set of seeds. That is how
much a genome's rank moves when nothing changes but the luck of the draw, and selection already
lives with it. The option's thresholds follow from it:

| Reading | Verdict |
|---|---|
| Rank correlation with today's world at or above the floor's, and elites kept at or above the floor's | **Within noise**: it changes the numbers and not what is selected |
| Below the floor, but its distance from 1 at most one and a half times the floor's (for a floor of 0.80, from 0.70 to 0.80) | **Small**: put to the owner with the numbers |
| Lower than that | **Changes the experiment** |

A short search of five generations each way, on the same seeds, then checks the climb: the best
and the median score each generation, against the gap between two seeds of today's world over the
same five generations. B1 changes who swims with whom, so it has no genome-by-genome pairing. Its
96 scores are compared as distributions (median, top-12 mean), against the same floor.

### The farm: a resume each way, read by the round's own clauses

Round 56's seed 2 resumes from its checkpoint at 28,000 s and runs to 30,000 s, once as it is and
once with the option on, both on the same build. Both restore brain v2 from the same checkpoint;
that restore is not faithful to the recording, but it is the same in both, so the comparison holds.

**The clauses a window can score.** Ending at 30,000 s lets three of round 56's five pre-registered
clauses be scored on each run as the round scored them. The other two need the whole round.

| Clause | What it reads | From a 28,000 to 30,000 s window |
|---|---|---|
| G | the living at 10,000 s | cannot be scored |
| S1 | the fewest and the most living in each half of years 2 and 3 | cannot be scored |
| S2 | the jointed share of the living at 30,000 s | scored, for seed 2 against round 55's seed 2 |
| S3 | the median breeding margin of the living leaves at 30,000 s | scored; the leaves born before 28,000 s take their birth rows from the round's own `lineage.jsonl` |
| B1 | the audit and the matter residual at every sample, nothing diverging | scored over the window |

S3 already sits on its threshold in round 56 (it held on one seed of three), so a flip there is
reported as fragile, not as a verdict on the option alone. Beside the clauses go the rows: the
bodies alive, births and deaths, the clades' shares, and the matter in bodies, snow and bed.

**The noise is measured first, because it may not stay small.** The farm is chaotic. A change at the
last bit of one body's position, a nudge of one part in 10^12 behind its own off-by-default
setting, is resumed beside today's world over the same window, and the rows are read against time
to see when the two part. Round 56's three seeds at 30,000 s give the outer bound, the spread the
round already lives with. What follows depends on that first read.

- **If the nudged run stays close to today's through 30,000 s**, the comparison is by trajectory, run
  against run, with two floors: the nudge's distance below, the seeds' spread above.

  | Reading | Verdict |
  |---|---|
  | S2, S3 and B1 score as on today's run, and every row within the nudge's distance | **Within noise** |
  | The clauses score the same, and the rows past the nudge but within the seeds' spread | **Small**: put to the owner with the numbers |
  | A clause scores differently, or a row past the seeds' spread | **Changes the world** |

- **If the nudged run parts from today's within the window**, as the record of every per-step change
  says it likely will, trajectory says nothing, and the comparison is by distribution. Four
  resumes of today's world, each with a different nudge, make the noise ensemble, measured once and
  shared by every option. Each option gets four nudged resumes of its own. For each clause's
  quantity (the jointed share, the median margin) and each row at 30,000 s, the option's four are
  set against today's four.

  | Reading | Verdict |
  |---|---|
  | The option's mean inside the range of today's four, on every quantity, and B1 holding on all eight | **Within noise** |
  | A mean outside today's range but within the seeds' spread | **Small**: put to the owner with the numbers |
  | A mean past the seeds' spread, or B1 failing on any run | **Changes the world** |

### N2 is a world rule, ruled on its own

A simpler sea floor changes the floor's shape, and the bodies' contacts feel the same floor. So it
changes the world itself, as the speed plan says, and is not a speed setting. It is measured like
the others, so the owner has its speed and its difference, but it goes to the owner as a separate
ruling on the world, after the others.

## Order and cost

1. The floors first, once. The nursery's second-seed swim (about 5 minutes) and its two
   five-generation searches (about 40 minutes). The farm's nudge switch (half a day of code), then
   one nudged resume read against time (about 40 minutes at 8 threads). If it parts from today's
   run, three more nudged resumes complete the noise ensemble (about 2 hours, or 1 hour two at a
   time inside the 16-processor cap).
2. Then the config options (W1, B4, B1). The nursery pair takes about 10 minutes each. A farm option
   takes about 40 minutes if the trajectory holds, or about 2.5 hours for its four resumes if it
   does not (1.3 hours two at a time). Across the farm's five options (W1's three settings here, N1 and R2
   in step 3), that is about 3.5 hours of machine in the first case and about 7 to 13 hours in the
   second, spread over the week by day.
3. Then the switches, cheapest first: N1 (half a day), B3 (half a day), R2 (a day), S7 (one to two
   days), and N2 last (two to three days), as its own ruling. Each switch is proven off by the digest check before its trial,
   so the default build cannot have moved.
4. The bit-identical build goes on beside this and comes first on the machine, since its gains are
   certain and these are trials.

A trial is a measurement, so it runs in a slot the coordinator gives, with the heat watch on.
Nothing here changes a round until the owner rules on it.

## What the owner is asked

Whether this way of measuring is the right one, with its thresholds: the rank of 96 genomes and a
short search for the nursery against a second-seed floor, and for the farm a resume from 28,000 to
30,000 s scored by the three of round 56's clauses a window can score, by trajectory if a
nudge stays close and by a four-run ensemble if it does not. And whether to start with the
config options this week, beside the bit-identical build.
