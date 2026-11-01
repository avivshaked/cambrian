# Kill gaps: the run's own kills measured on the host replay

Every row is one kill row from the run's lineage, measured on the host film window's live state at its second (--kill-pairs). `surf` is the true-shape gap between the two named parts (core distance minus both margins); negative is overlap. "named" is whether the host's own census named the pair at that step.

## Window summaries

| world | restore | window kills | matched | max surf, window kills | run pairs | present |
|---|---|---|---|---|---|---|
| s6203 | 10500 | 1 | 1 |  | 1 | 1 |
| s6203 | 12000 | 1 | 1 |  | 1 | 1 |
| s6203 | 13500 | 1 | 1 |  | 1 | 1 |
| s6203 | 15000 | 6 | 6 |  | 6 | 6 |
| s6203 | 16500 | 6 | 6 |  | 6 | 6 |
| s6203 | 18000 | 2 | 2 |  | 2 | 2 |
| s6203 | 19500 | 21 | 21 |  | 21 | 21 |
| s6203 | 21000 | 24 | 24 |  | 24 | 24 |
| s6203 | 22500 | 5 | 5 |  | 5 | 5 |
| s6203 | 4500 | 7 | 7 |  | 7 | 7 |
| s6203 | 7500 | 14 | 14 |  | 14 | 14 |
| s6203 | 9000 | 11 | 11 |  | 11 | 11 |
| t6202 | 12000 | 1 | 1 | -0.00630 | 1 | 1 |
| t6202 | 13500 | 5 | 5 | -0.00003 | 4 | 4 |
| t6202 | 15000 | 4 | 4 | -0.00028 | 4 | 4 |
| t6202 | 16500 | 2 | 2 | -0.00002 | 2 | 2 |
| t6202 | 18000 | 4 | 4 | -0.00169 | 4 | 4 |
| t6202 | 19500 | 25 | 25 | 0.00009 | 26 | 26 |
| t6202 | 21000 | 23 | 23 | -0.00015 | 25 | 25 |
| t6202 | 22500 | 12 | 12 | 0.00023 | 11 | 11 |
| t6203 | 10500 | 3 | 3 | -0.00115 | 3 | 3 |
| t6203 | 12000 | 7 | 7 | 0.00007 | 7 | 7 |
| t6203 | 13500 | 4 | 4 | -0.00081 | 4 | 4 |
| t6203 | 15000 | 22 | 22 | 0.00012 | 20 | 20 |
| t6203 | 16500 | 19 | 19 | -0.00000 | 18 | 18 |
| t6203 | 18000 | 11 | 11 | 0.00019 | 8 | 8 |
| t6203 | 19500 | 14 | 14 | 0.00023 | 14 | 14 |
| t6203 | 21000 | 32 | 32 | 0.00012 | 31 | 31 |
| t6203 | 22500 | 27 | 27 | 0.00021 | 26 | 26 |
| t6203 | 7500 | 9 | 9 | -0.00001 | 9 | 9 |
| t6203 | 9000 | 15 | 15 | -0.00010 | 15 | 15 |

## Run kills by whether the host also named them

| world | named | kills | surf > 0.05 m | largest surf | median surf |
|---|---|---|---|---|---|
| s6203 | True | 0 | 0 |  |  |
| t6202 | False | 7 | 3 | 0.55527 | 0.03974 |
| t6202 | True | 70 | 0 | 0.00023 | -0.00110 |
| t6203 | False | 5 | 3 | 0.39191 | 0.09756 |
| t6203 | True | 150 | 0 | 0.00023 | -0.00104 |

## Every run kill with surf above 0.05 m

| world | restore | s since | killer (guild, core) | victim (guild, core) | root | named | match so far | report agree | surf m | mouth m |
|---|---|---|---|---|---|---|---|---|---|---|
| t6202 | 19500 | 3.0 | 41286 (grazer, point) | 41839 (leaf, box) | True | False | 0.86 | between | 0.555 | 0.71293 |
| t6202 | 21000 | 6.5 | 47195 (grazer, box) | 42570 (leaf, box) | True | False | 0.84 | between | 0.542 | 0.95642 |
| t6202 | 21000 | 8.0 | 39490 (grazer, box) | 52415 (grazer, point) | False | False | 0.82 | between | 0.369 | 0.85303 |
| t6203 | 15000 | 6.5 | 31017 (grazer, box) | 35177 (leaf, box) | True | False | 0.92 | between | 0.098 | 0.28351 |
| t6203 | 21000 | 7.5 | 50975 (other, box) | 64460 (leaf, box) | True | False | 0.94 | between | 0.392 | 0.80774 |
| t6203 | 22500 | 0.5 | 66874 (grazer, box) | 70081 (leaf, box) | True | False | 0.00 | between | 0.182 | 0.49957 |

## Run kills by seconds since the restore

| s since | kills | host named | surf > 0.05 m |
|---|---|---|---|
| 0-2 | 69 | 67 | 1 |
| 2-4 | 63 | 61 | 1 |
| 4-6 | 74 | 72 | 0 |
| 6-8 | 54 | 50 | 4 |
| 8-10 | 71 | 69 | 0 |

Run pairs not present on the host at their second: 0.
