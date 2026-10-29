# r63c's G2 read

The bloom rerun pre-registered in `logbook/specs/r63c-prereg/predictions.md` ran four worlds: arms B and C on seeds 6202
and 6203, at bloom periods of 5,000 to 10,000 s, one card group from `scratch/wt-r63b` at `ebd88f65`, 24,000 s each, in
`runs/r63c-{b,c}-{6202,6203}/2026-10-29-123625-*`. Every world reached its budget, and the books balanced in all four.
The farm hash `d53ec6ee` and the Dynamics hash `ddb068fd` are `r63b`'s, as the pre-registration requires. This folder
is its read by `scripts/reads/bloom/g2-read.py` with `--survive-from 12000` (D256), which `scripts/reads/bloom/r63c-read.ps1`
makes again from the runs. It ran beside a card group as light work (D258).

- `g2-read.txt` is the read's whole output.
- `traits-r63c-<arm>-<seed>.tsv` are the grazer trait tables gate 3 reads, from the screen's own Release farm.
- The controls, `r63b`'s A and `r62cf`, are read in `logbook/specs/r63c-prereg/r63b-from12k.txt`.

## What it says

No arm passes all three gates on both seeds, so nothing is ranked, and the film's world goes to the owner.

| World | Gate 1, survive | Gate 2, islands at r ≥ 0.5 | Gate 3, largest shift | Crossings | Crowd islands | Recoveries |
|---|---|---|---|---|---|---|
| B-6202 | pass | 4 of 8 | 0.23, fail | 5 | 8 | 2 |
| B-6203 | pass | 5 of 8 | 1.41, pass | 11 | 7 | 3 |
| C-6202 | pass | 2 of 8 | 0.40, fail | 7 | 7 | 2 |
| C-6203 | pass | 2 of 7 | 1.30, pass | 12 | 7 | 2 |

- **The slower cycle helps the leaves follow the bloom, but not enough.** At `r63b`'s periods of 2,500 to 6,000 s, B and
  C had 1, 0, 0 and 0 islands at 0.5. Here they have 4, 5, 2 and 2 (measured). The bar is every judged island.
- **Survival passes everywhere,** D222 included, where `r63b` failed it in two of six bloom worlds.
- **Evolution passes on seed 6203 in both arms,** and fails on 6202, where `r63b`'s B passed.
- **The far smell buys little here.** C's crossings exceed B's by 2 and 1 (in `r63b` it was 7 to 8). C's leaves follow
  the bloom on fewer islands than B's. That the scent draws grazers off their islands, and away from the leaves the bloom
  feeds, is an inference.

## The predictions

- **Gate 2, at least half the judged islands in each world:** wrong for C (2 of 8, 2 of 7), right for B. The likeliest
  result, a fail in every world on one or two islands each, held for the fail. The misses were more than two islands in
  every world.
- **Gate 1, the floor passes everywhere and D222 near-misses in at least one world:** the floor held. D222 passed in all
  four, so the near miss was wrong.
- **Gate 3, a fail in at least three worlds:** wrong. It failed in two.
- **Story, C more crossings than B, with crowds on 4 to 6 islands:** C has more crossings, by little. The crowds were on
  7 or 8 islands, more than predicted.

## What it means

Per the pre-registration, a fail in every world on most islands would have taken the bloom out of the film's story of
food. That is not this result: in B, half the islands follow. At 24,000 s a world sees only 2.4 to 4.8 cycles of each
island, so a correlation over so few cycles is noisy. G7's 48,000 s round doubles that (an inference). The coordinator's
recommendation to the owner is B's world for the film: the slow bloom with the far smell off. It is the simpler arm,
its leaves follow the bloom best, and on seed 6203 it matches C in crossings and evolves. Gate 2 is then judged again in
G7.
