# Round 55's nursery genomes

Every distinct genome that the four brain-v2 nursery runs saved, one file each. Round 55 is
inoculated with a mix of them; the mix is set by the round's proposal
(`proposals/propose-r55-mixed-inoculation.md`) and the owner's ruling on it. **This folder holds the
whole set and chooses nothing.** The round's weighted list is written from `origins.tsv` after the
ruling.

## Where they come from

| run | folder | recipe |
|---|---|---|
| `pilot-deep-brain2` | `scratch/nursery/pilot-deep-brain2` | registry entry 1, the reference |
| `brain2-rep2` | `scratch/wt-nursery-general/runs/nursery/brain2-rep2` | the replicate, seed 0x2 |
| `brain2-nodrive` | `scratch/wt-nursery-general/runs/nursery/brain2-nodrive` | brain2 without the drive sense |
| `brain2-nocross` | `scratch/wt-nursery-general/runs/nursery/brain2-nocross` | brain2 without crossover |

Two of each run's files are used:
- **`tank-exam-genomes.tsv`:** the exam's six genomes, one per body.
- **`tank-best.jsonl`:** each generation's top genome. Its body, born generation, score and episodes
  are on the same line of `tank-generations.jsonl`, under `top`.

## How they were cut

`scripts/reads/r55-cut-inoculants.py`, on 2026-10-04:
- Each genome's bytes are written exactly as its run wrote them, one line with no line break.
- A genome met more than once is one file, with a row in `origins.tsv` for every place it was met:
  - an elite that stayed top for several generations
  - an exam genome that was also a top (4 of them)
  - one genome that two runs share: generation 0's top of body 10 is byte-identical in
    `pilot-deep-brain2` and `brain2-nocross`, because both drew it from seed 0x1
- A file is named for the first place it was met: `<run>-exam-b<body>.json`, or
  `<run>-best-g<generation>-b<body>.json`.

There are 341 files and 428 rows. Counted run by run, the distinct genomes are 83, 84, 88 and 87,
which sum to 342 because of the shared one. Every file is genome format 12, the central brain's,
which `GenomeJson.Read` takes. Each file's SHA-256 is in its rows.

**`origins.tsv`'s columns:**
- `file`, `sha256`
- `run`, `kind` (`exam` or `best`)
- `body`, `born`, and `gen`, the generation it was top
- `source`, `line`: the source file and its 1-based line
- `score`, `episodes`, and `scoreIs`, which says what the score is. For an exam genome it is its
  own brain's mean net over the exam's 32 episodes. For a top it is that generation's top score.

## How many each body has

Distinct genomes per run and body, as exam genomes and as distinct tops, with the number of
generations the body held the top:

| run | body 0 | body 1 | body 3 | body 4 | body 8 | body 10 |
|---|---|---|---|---|---|---|
| `pilot-deep-brain2` | 1 + 7 (7) | 1 + 19 (25) | 1 + 11 (13) | 1 + 0 (0) | 1 + 0 (0) | 1 + 41 (56) |
| `brain2-rep2` | 1 + 0 (0) | 1 + 25 (39) | 1 + 22 (25) | 1 + 1 (1) | 1 + 2 (3) | 1 + 29 (33) |
| `brain2-nodrive` | 1 + 8 (8) | 1 + 37 (47) | 1 + 9 (11) | 1 + 0 (0) | 1 + 1 (1) | 1 + 28 (34) |
| `brain2-nocross` | 1 + 10 (11) | 1 + 13 (15) | 1 + 16 (20) | 1 + 0 (0) | 1 + 2 (2) | 1 + 41 (53) |

The set is skewed:
- Bodies 4 and 8 held the top in at most 3 of a run's 101 generations, so for them the exam genome
  is nearly all there is.
- Body 10 has 29 to 42 genomes a run, and body 1 has 14 to 38.

A share drawn evenly from the files would be mostly bodies 10 and 1. How to weigh them is the
proposal's question.
