# Round 27 readings as arms land (for 0068's Results)

| arm | t last | alive | absorpt | largest clade / min last 6,000 s | D063 | height t>10k | above max | standing 20k->30k | crash worst alive/max after 6k | contacts mean | wall min |
|---|---|---|---|---|---|---|---|---|---|---|---|
| r27-s4 | 30,000 | 2,527 | 26 | 26 (root born 27,343) / 0 | FAIL (stability; founder clade 182 at 6,000 fell to 11 by 10,000, 0 by 24,000) | -18.8 | 0.28% | 6,251 -> 9,401 (+50%) | 0.21 at 9,700 | 817 (min 3, max 4,134) | 379 |
| r27-s5 | 30,000 | 3,452 | 356 | 334 (root born 4,387) / 73 | PASS | -19.8 | 0.23% | 8,275 -> 12,408 (+50%) | 0.99 at 9,900 | 1,288 (min 29, max 5,419) | 625 |
| r27-s3 | 30,000 | 3,181 | 20 | 16 (root born 9,427) / 7 | FAIL (stability min 7) | -20.0 | 0.57% | 7,625 -> 11,701 (+53%) | 0.86 at 6,700 | 920 (min 14, max 4,251) | 495 |
| r27-s1 | 30,000 | 3,711 | 177 | 176 (root born 5,543) / 79 | PASS (diverged 2) | -20.3 | 0.19% | 9,013 -> 13,221 (+47%) | 0.90 at 6,800 | 1,254 (min 35, max 4,737) | 669 |
| r27-s2 | 30,000 | 3,682 | 100 | 93 (root born 5,664) / 38 | PASS (diverged 1) | -20.0 | 0.19% | 9,177 -> 13,205 (+44%) | 0.98 at 10,800 | 1,333 (min 40, max 5,059) | 707 |

Notes: r27-s4 is the same world as r26d-s4 (held 195 stomachs at 20,000) — a different realisation (0069). patches at end [447, 1151, 783, 146]; crowded 15,497 vs births 5,401; influx 18,000, buried 14,599, standing 9,401.

## Late balance (last two lifetimes, 24,000-30,000; scripts/reads/late-balance.py)

| arm | standing 24k -> 30k | slope /1,000 s | influx /1,000 s | slope as % of influx | burial as % of influx |
|---|---|---|---|---|---|
| r27-s4 | 7,632 -> 9,401 | +295 | 600 | 49% | 51% |
| r27-s5 | 9,936 -> 12,408 | +412 | 600 | 69% | 31% |
| r27-s3 | 9,272 -> 11,701 | +405 | 600 | 67% | 33% |
| r27-s1 | 10,707 -> 13,221 | +419 | 600 | 70% | 30% |
| r27-s2 | 10,843 -> 13,205 | +394 | 600 | 66% | 34% |

Notes r27-s5: crowded total 28,689 vs births 7,358 (crowd exceeds births at samples after 15,000 -> 0068 M7 fails); patches at end [724, 1421, 1092, 215]; 6 living clades, 1 passes; contacts max 5,419 briefly above the 5,000 bound.
Notes r27-s3: 5 living clades, none pass; the largest (born 9,427) held 322 members ever and 16 at the end, dipping to 7 in the last two lifetimes; above 0.57% at one sample (M2 marginal); crowded 20,356 vs births 7,119; patches [523, 1326, 1193, 139].
Notes r27-s1: 2 living clades, 1 passes; crowded 22,594 vs births 9,099; patches [652, 1517, 1379, 163]; 2 diverged deaths. r27-s2: 3 living clades, 1 passes; crowded 20,649 vs births 9,526; patches [553, 1553, 1390, 186]; 1 diverged death.
