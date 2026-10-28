# D251's far smell, sized from r62cf

*2026-10-28, the speed agent. This read sets the far smell's gain before the build, as D251 and G1 of
`logbook/specs/grazer-film-plan.md` ask. The census is `--island-leaf-census` on branch `fat-step0` (`cf8eaf53`, at
r62cf's own build). It ran on `runs/r62cf-6202` and `runs/r62cf-6204` at 12,000 s and 24,000 s, read-only, pinned to
logical 20-21. The core and dynamics hashes matched the runs'. The farm hash differs only because the census adds a
farm file.*

## What it found

**The influx plan has 11 islands, not 9.** An 8-connected fill over the plan's own columns finds the same 11 on both
seeds, which share the landscape seed. Two are single columns that hold no leaves. A third, of 7 columns, holds 0 to 13
leaves at every census. The other eight carry the world (`islands-6202.tsv`, `islands-6204.tsv`).

**Groves are halos round the plan's columns.** Only 32 to 40% of living leaves stand inside an island's columns. 58 to
68% stand within 5 m of the plan's edge, 79 to 87% within 10 m, and 92 to 96% within 15 m (`leaf-reach.txt`). With a
10 m band, the nearest grove edges meet: the median gap between them is −2.9 m, against 17.1 m between plan edges.

**Grazers already read a strong leaf smell.** With no far smell, grazers in the groves read a median strength
s/(s+1) of 0.65 to 0.85. The far smell's work is in the open water.

| G, with a 10 m band | Far smell at a grove's edge, median island | Grazers in groves, median strength | Grove grid, p10 to p90 | Desert columns where far > leaf smell |
|---|---|---|---|---|
| 0 | 0 | 0.65 to 0.85 | 0.00 to 0.58-0.82 | 0% |
| 0.005 | 0.19-0.20 m² at 12,000 s, 0.52-0.60 m² at 24,000 s | 0.69 to 0.89 | 0.10-0.30 to 0.65-0.86, a spread of 0.55-0.56 | 40 to 45% |
| 0.01 | 0.38-0.39 m², 1.04-1.20 m² | 0.74 to 0.91 | spread 0.44 to 0.53 | 41 to 47% |

The grove grid is every point within an island's banded radius, at a matter column's centre, at 2, 6, 10, 14 and
20 m deep (`far-smell-band10.txt`). `far-smell-band0.txt` is the same read without the band, with L_k counted by column.

## What was ruled

The coordinator ruled on 2026-10-28 from this read:
- G = 0.005, a band of B = 10 m, and a reach of R = 30 m.
- All 11 islands cycle, with no minimum size.

Island k then adds G × L_k × max(0, 1 − max(0, d − (r_k + B)) / R) to the leaf smell. L_k is the lit area of the
leaf sources whose nearest island edge is within B. G1's spread gate is read on the grove grid, at least 0.3, and
G = 0.005 gives 0.55 to 0.56.

## What it means

These are inferences.

- **A flat top at the plan's own radius would have left most of a grove on the far smell's slope.** About 60% of
  leaves stand outside the plan's columns. The band puts nearly all of a grove inside the flat top, where leaves alone
  steer.
- **Some neighbouring groves are one grove.** With a 10 m band their edges touch, so a move between them is a stroll,
  not a crossing. The screen's crossing count has to tell the two apart.
- **The signal grows with the food.** L_k roughly triples from 12,000 to 24,000 s, and so does the far smell.

## Sources

| Claim | Source |
|---|---|
| islands, radii, leaf area by column | `islands-6202.tsv`, `islands-6204.tsv`, from `--island-leaf-census` (`cf8eaf53`) |
| leaves' distance from the plan's edge | `leaf-reach.txt`, from `scripts/reads/bloom/leaf-reach.py` |
| strengths at each gain | `far-smell-band10.txt` and `far-smell-band0.txt`, from `scripts/reads/bloom/far-smell-census.py` |
| banded L_k | the column L_k scaled by the ratio of leaf counts, banded to inside; the census gives area by column only, so this assumes the halo's leaves are the core's size (inference) |
| the rulings | the coordinator, 2026-10-28 |
| the census's bodies and grid TSVs | not committed; they come back from the runs' checkpoints in about 3 minutes each on two cores |
