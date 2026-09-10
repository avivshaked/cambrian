# Re-score after the every-clade change to `scripts/clade-score.ps1`

2026-09-06. Item 1 and item 2 of `logbook/specs/review-2026-09-06-response.md`, implemented and
run against every arm the response named: `r18x-s1..5`, round 26's six arm directories, and
`r27-s4`.

The old scorer is `git show HEAD:scripts/clade-score.ps1`, kept at
`scratch/clade-score-old.ps1`. Both were run on the same trees, minutes apart.

## What "old verdict" means, and why it is not a verdict

The old script never printed a seed verdict. It printed the largest clade's `-> clade
PASS/fail` token, which asks **D063's three original clauses only**, and then printed the
stability clause beside it as a separate segment for the reader to fold in by hand. So a
line reading `-> clade PASS | stability: min last 6000 s = 8 -> unstable` is a **failing**
seed under D063 as amended, and reads at a glance like a passing one. Two arms in this set
are exactly that. The new script's first line does the folding.

The columns below therefore separate the old script's two tokens.

## Table

| arm | old: largest, 3 clauses | old: stability | new: seed verdict | largest clade, min over last 6,000 s | clade the new verdict names | changed? |
|---|---|---|---|---|---|---|
| r18x-s1 | fail | 2, unstable | **FAIL** | 2 | best = root 549, the largest; fails 3 of 4 | no |
| r18x-s2 | PASS | 48, stable | **PASS** | **48** | root 31, the largest; 3 of 3 living clades pass | no |
| r18x-s3 | PASS | 41, stable | **PASS** | **41** | root 34, the largest; 1 of 3 pass | no |
| r18x-s4 | PASS | 24, stable | **PASS** | **24** | root 3936, the largest; 1 of 1 passes | no |
| r18x-s5 | PASS | 127, stable | **PASS** | **127** | root 252, the largest; 1 of 2 pass | no |
| r26-s1 | PASS | 41, stable | **PASS** | 41 | root 3549, the largest; 2 of 5 pass | no |
| r26-s2 | PASS | 19, stable | **PASS** | 19 | root 2760, the largest; 2 of 6 pass | no |
| r26-s3 | PASS | 8, unstable | **FAIL** | 8 | best = root 1400, the largest; fails stability alone | reading only |
| r26-s4 | unread (no clade alive) | unread | **FAIL** | — | none | no |
| r26-s5 | unread (no clade alive) | unread | **FAIL** | — | none | no |
| r26d-s4 | PASS | 29, stable | **PASS** | 29 | root 78, the largest; 1 of 1 passes | no |
| r27-s4 | PASS | 0, unstable | **FAIL** | 0 | best = root 4889, the largest; fails stability alone | reading only |

**Round 18's minima reproduce: 48 / 41 / 24 / 127** for `r18x-s2`, `-s3`, `-s4`, `-s5`, the
four passing seeds of logbook/0054's addendum. Stronger than that: the whole "largest" line
is byte-identical to the old script's line for all twelve arms, once the old line's trailing
producer segment (now its own line) is cut off. Checked mechanically, not by eye.

## The photo readings

Every arm in this set predates the `pho` flag, so both lineage readings print `flag absent`
and neither could have been anything else. The population column reading is unchanged from
what the old script printed; only its wording moved.

| arm | photo (owner's wording) | photo (>=10, two lifetimes) | photo inh (population column) |
|---|---|---|---|
| r18x-s1..s5 | flag absent | flag absent | column absent |
| r26-s1 | flag absent | flag absent | held — at end 1483, min over last 20 1483 |
| r26-s2 | flag absent | flag absent | held — at end 1605, min over last 20 1576 |
| r26-s3 | flag absent | flag absent | held — at end 1877, min over last 20 1759 |
| r26-s4 | flag absent | flag absent | failed — at end 1, min over last 20 1 |
| r26-s5 | flag absent | flag absent | held — at end 928, min over last 20 909 |
| r26d-s4 | flag absent | flag absent | held — at end 1434, min over last 20 1121 |
| r27-s4 | flag absent | flag absent | held — at end 2487, min over last 20 2345 |

The two lineage readings are exercised on synthetic worlds instead
(`scripts/tests/clade-score/`, case `pho-owner-only`), where the owner's wording holds, the
>= 10 reading fails and the column reading fails, all three in one arm — which is the point
of printing all three.

## What actually changed, and what did not

**No verdict in this set changed because a smaller clade rescued a seed.** In all twelve
arms the clade the new script names — passing or best-failing — is the largest one. Where
several clades pass (`r18x-s2` 3 of 3, `r26-s1` 2 of 5, `r26-s2` 2 of 6) the seed was
already passing on the largest alone.

That is not evidence the change was unnecessary. Scoring the largest clade alone can only
under-report, never over-report, and the failure mode it creates — a world held by a clade
that is not the biggest one — is invisible in the record precisely because nothing was
looking for it. What this re-score establishes is that the historical scores are not
distorted by it: **round 18 stands at 4 of 5, unchanged.**

The two lines that do move, `r26-s3` and `r27-s4`, move because the verdict line now folds
in the stability clause the owner added on 2026-09-04. Both were already failing seeds under
the amended rule; the old output just did not say so on one line. Anywhere those two were
reported as passes on the strength of the `-> clade PASS` token, the report was wrong and
this is the correction.

`r27-s4` is worth a second look on its own terms: its only living clade was born at
t=27,343 and reached 26 members by the last sample with a minimum of **0** over the last two
lifetimes, which is to say the world's absorptive line at the end of the run is three
lifetimes younger than the window the stability clause asks about. That is a late
colonisation, not a sustained one.
