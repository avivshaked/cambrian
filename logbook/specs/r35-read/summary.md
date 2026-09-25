# Round 35 read \u2014 raw numbers

Gathered per logbook/0087's "Rules, before scoring". Four landed arms: `r35-s1`, `r35-s2`,
`r35-s3`, `r35-s5`. `r35-s4` is still running and was not touched. No verdicts or
interpretation below \u2014 plain readings only, for the entry that scores them.

Sources, per row: `scripts/analyse-arm.ps1` against `runs/<arm>.md` (the report the script
reads is at that path, per its own `$path = Join-Path $repo "runs/$name.md"`), `run.json`
under `runs/<arm>/<timestamp>/`, `scripts/clade-score.ps1`, and
`scripts/positions-read.py --at 5000,15000,30000` against `positions.jsonl` in the same run
directory.

## V1 \u2014 header and manifest tokens

| | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| `dispersal=5 m` in header | yes | yes | yes | yes |
| `current 0.1 m/s transport` in header | yes | yes | yes | yes |
| `dt=0.01` in header | yes | yes | yes | yes |
| `cols`, `cols abs`, `x sd` columns present | yes | yes | yes | yes |
| `simHash` (run.json `source.simHash`) | `b5d31a48b18c0a5c\u2026` | `b5d31a48b18c0a5c\u2026` | `b5d31a48b18c0a5c\u2026` | `b5d31a48b18c0a5c\u2026` |
| `coreHash` (run.json `source.coreHash`) | `cafcb692d771b3ce\u2026` | `cafcb692d771b3ce\u2026` | `cafcb692d771b3ce\u2026` | `cafcb692d771b3ce\u2026` |
| `physicsJobWorkers` | 0 | 0 | 0 | 0 |
| `status` | ended | ended | ended | ended |
| `reason` | budget | budget | budget | budget |
| `diverged` (run.json `divergedTotal`) | 0 | 0 | 3 | 1 |

Note: `simHash` and `coreHash` are identical across all four arms \u2014 one build, as 0087's launch
note claims. Full hashes: `simHash b5d31a48b18c0a5ca060d92c1d22c1043be3a6261f21ddbe013e5f4fd44e9ff7`,
`coreHash cafcb692d771b3ce9c7b82d029f6a5617d097bd9cddade81fbf4cddb478a81f8`. Header line read
with `pwsh -NoProfile -File scripts/analyse-arm.ps1 <arm> -Header`; `run.json` fields read
directly with Python's `json` module.

## V2 \u2014 audit and matter identity, every row

| | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| rows read | 300 | 300 | 300 | 300 |
| max \|`audit`\| | 0.0000% | 0.0000% | 0.0000% | 0.0000% |
| max \|`mat resid`\| | 0.0 | 0.0 | 0.0 | 0.0 |

Every row's `audit` cell reads `0.0000%` and every `mat resid` cell reads `0` (or an em dash,
treated as absent, not a nonzero value) for all four arms \u2014 the maximum absolute value over
the whole run is 0 in both columns for every arm. `runs/<arm>.md`'s columns headed `audit` and
`mat resid` were parsed directly (all 300 data rows of each report, header row used to build a
name-to-index map exactly as `analyse-arm.ps1` does, not by position).

## S1 \u2014 footprint (`cols`) after t = 3,000 s, and spread at three times

| | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| min `cols` for samples with t > 3,000 s (270 samples/arm) | 97/100 | 98/100 | 96/100 | 99/100 |
| first sample after t = 3,000 s (t = 3,100 s) already >= 90? | yes (97) | yes (98) | yes (96) | yes (99) |
| first t (any time) at which `cols` >= 90 | 900 s | 1,100 s | 1,100 s | 1,000 s |

| at t = 5,000 s | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| `cols abs` | 56/100 | 41/100 | 70/100 | 96/100 |
| `x sd` | 7.95 | 7.65 | 8.44 | 6.89 |
| `alive` (report) | 578 | 1017 | 727 | 729 |

| at t = 15,000 s | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| `cols abs` | 65/100 | 58/100 | 51/100 | 72/100 |
| `x sd` | 7.28 | 7.94 | 7.72 | 6.61 |
| `alive` (report) | 891 | 1444 | 1188 | 1121 |

| at t = 30,000 s (last sample) | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| `cols abs` | 75/100 | 37/100 | 34/100 | 66/100 |
| `x sd` | 8.43 | 7.06 | 9.21 | 8.36 |
| `alive` (report) | 1270 | 1613 | 1671 | 1380 |

All four samples land exactly on the asked times (`t (s)` column matches 5000.0/15000.0/30000.0
exactly; no nearest-sample gap). Every sample after t = 3,000 s stays at or above 90 in every
seed \u2014 no seed falls under S1's bar at any post-3,000 s sample, so no "read for why" case
arises from this rule alone. `cols` and `cols abs` were read as the integer numerator before
the `/100` (e.g. `97/100` -> 97).

## S2 \u2014 positions.jsonl: nearest neighbour, kin, depth by guild

`python scripts/positions-read.py <arm> --at 5000,15000,30000` (no `--out` passed, per
instruction \u2014 it re-wrote the same picture paths under `scratch/positions/<arm>/`, which
already existed). `nn h` = median horizontal nearest-neighbour distance, `nn 3d` = median 3-D
nearest-neighbour distance (a `~` prefix marks the thinned/approximate reading, which the
script applies once population exceeds `--nn-max` 1200); `clade 1m` = kin-within-1 m count.

| t = 5,000 s | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| `n` (positions count) | 577 | 1017 | 726 | 728 |
| `nn h` | 0.20 | 0.14 | 0.18 | 0.17 |
| `nn 3d` | 0.87 | 0.67 | 0.81 | 0.82 |
| `clade 1m` (kin within 1 m) | 304 | 689 | 395 | 308 |
| leaf n / mean y | 489 / -16.07 | 960 / -14.87 | 616 / -12.89 | 391 / -14.01 |
| stom n / mean y | 88 / -24.19 | 57 / -24.94 | 110 / -22.47 | 337 / -18.77 |
| mixo / jnt / plain n | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 |

| t = 15,000 s | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| `n` (positions count) | 891 | 1442 | 1188 | 1119 |
| `nn h` | 0.15 | 0.12 | 0.13 | 0.14 |
| `nn 3d` | 0.71 | 0.62 | 0.67 | 0.63 |
| `clade 1m` (kin within 1 m) | 591 | 1149 | 699 | 750 |
| leaf n / mean y | 776 / -16.38 | 1362 / -17.33 | 1115 / -15.23 | 970 / -15.96 |
| stom n / mean y | 115 / -22.58 | 80 / -23.25 | 73 / -18.91 | 149 / -19.32 |
| mixo / jnt / plain n | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 |

| t = 30,000 s | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| `n` (positions count) | 1270 | 1613 | 1671 | 1379 |
| `nn h` | ~0.13 | ~0.12 | ~0.11 | ~0.12 |
| `nn 3d` | ~0.70 | ~0.64 | ~0.62 | ~0.65 |
| `clade 1m` (kin within 1 m) | 890 | 1332 | 1171 | 1015 |
| leaf n / mean y | 1135 / -18.51 | 1564 / -20.98 | 1632 / -21.54 | 1270 / -19.24 |
| stom n / mean y | 135 / -22.32 | 49 / -23.97 | 39 / -29.11 | 109 / -22.92 |
| mixo / jnt / plain n | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 |

No mixotroph, jointed, or plain-bodied creature is alive in any of the twelve samples read \u2014
every living body in every one of these snapshots carries exactly one of the leaf/stomach
guild flags, matching `jnt inh = 0` and `jointed % = 0%` seen at the report's last row for
every arm (G3, below).

## G0 \u2014 goal rule by connected clade (D063 as amended)

`pwsh -NoProfile -File scripts/clade-score.ps1 <arm>`, first line (verdict) and the "clades
with a living member" continuity line.

| | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| verdict | PASS | PASS | PASS | PASS |
| passing clade | root 70 (kind f, born 16.5) | root 6 (kind f, born 1.5) | root 878 (kind r, born 1991) | root 741 (kind r, born 1653) |
| alive at end (passing clade) | 130 | 20 | 31 | 77 |
| min over last 6,000 s (passing clade) | 95 | 16 | 19 | 74 |
| inherited absorptive births in last 20 samples (passing clade) | 116 | 13 | 24 | 66 |
| clades with a living member at t = 30,000 s | 3 | 6 | 6 | 3 |
| clades passing (of those with a living member) | 1 of 3 | 1 of 6 | 1 of 6 | 2 of 3 |
| aggregate `inherit` at end (all clades) | 134 | 48 | 35 | 110 |

All four landed seeds pass G0, same as round 33's five of five (per logbook/0087 this is read
against that bar; `r35-s4` is not scored here because it is still running).

## G1 \u2014 inherited stomach births in the last window

The report's population column for this trait is named `inherit` (index confirmed with
`-ListColumns`; `analyse-arm.ps1`'s own default status columns use the same name for the
"absorptiveInherited" quantity CLAUDE.md's gotchas describe). It is a standing count of
currently-alive creatures whose parent expressed the absorptive trait, not a births-in-window
count by itself, so it is reported alongside two window readings.

| at last sample (t = 30,000 s) | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| `inherit` (report, population, last row) | 134 | 48 | 35 | 110 |
| `inherit` ~2,000 s earlier (t = 28,000 s) | 150 | 46 | 25 | 112 |
| `births` (report, cumulative, last row) | 24,481 | 22,567 | 21,697 | 24,883 |
| `births` at t = 28,000 s | 22,939 | 21,431 | 20,679 | 23,443 |
| all-cause births in the last 20 samples / 2,000 s (`births` delta) | 1,542 | 1,136 | 1,018 | 1,440 |
| inherited absorptive births in last 20 samples, passing clade only (`clade-score.ps1`, from `lineage.jsonl`) | 116 | 13 | 24 | 66 |

Read against round 33's 51-187 (per 0087's G1 wording), the passing-clade inherited-absorptive
birth count is the number that matches that comparison directly: 116, 13, 24, 66. The report
table itself carries no column that isolates absorptive-only births in a window \u2014 `births` is
all-cause and cumulative, and `inherit` is a standing population, not a birth count \u2014 so the
window figure above (13\u2013116) comes from `clade-score.ps1`'s streamed read of `lineage.jsonl`,
not from the `.md` report.

## G2 \u2014 the dials at the end

| at last sample (t = 30,000 s) | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| `adult scale` | 0.648 | 0.290 | 0.317 | 0.516 |
| `invest` | 0.367 | 0.657 | 0.777 | 0.329 |
| `brood` | 3.565 | 1.539 | 1.513 | 2.578 |

Read against round 33's 0.39-0.60 (adult scale), 0.20-0.49 (investment) and 1.9-5.1 (litter):
adult scale spans 0.290-0.648 (below round 33's floor in s2 and s3, above its ceiling in s1),
investment spans 0.329-0.777 (above round 33's ceiling in s2 and s3), and brood spans
1.513-3.565 (below round 33's floor in s2 and s3).

## G3 \u2014 movement and the jointed line

| at last sample (t = 30,000 s) | r35-s1 | r35-s2 | r35-s3 | r35-s5 |
|---|---|---|---|---|
| `mean m/s` | 0.0824 | 0.0850 | 0.0905 | 0.0816 |
| `jnt inh` | 0 | 0 | 0 | 0 |
| `food jnt` | (em dash) | (em dash) | (em dash) | (em dash) |
| `food rig` | 2.3463 | 2.7332 | 3.4859 | 2.1264 |

`jointed %` also reads `0%` at the last row of all four arms (no jointed body alive at
t = 30,000 s in any seed), which is why `food jnt` prints as an em dash \u2014 the report's own
convention for "no such creature this sample" (CLAUDE.md's gotchas: "a stored 0 for 'no such
creature' is the trap"). Both columns exist in every report (confirmed via `-ListColumns`);
neither is `column absent`.

## Anomalies

- `alive` (report) vs. positions.jsonl body count at the same t is off by 1 in three of
  twelve samples. At t = 5,000 s: r35-s1 report `alive` = 578 vs. positions `n` = 577;
  r35-s3 report `alive` = 727 vs. positions `n` = 726; r35-s5 report `alive` = 729 vs.
  positions `n` = 728. At t = 15,000 s and t = 30,000 s all four arms match exactly, and
  r35-s2 matches at all three times. Both sources report `t` = 5000.0 exactly, so this is not
  a nearest-sample mismatch; it looks like the report's `alive` counter and the
  `positions.jsonl` snapshot are read from the world at slightly different points within the
  same 5,000 s sample (e.g. a birth or death straddling the two writes). One body, three of
  twelve samples \u2014 noted, not chased further here.
- `mat resid` prints as an em dash on some rows, which this read treated as "no value" (not
  0, not counted toward the max) rather than parsing it as zero \u2014 consistent with `audit`'s and
  `mat resid`'s max staying exactly 0 either way, since no row printed a nonzero number in either
  column.
- G1 has no single report column matching "inherited stomach births in a window." See G1's
  note above \u2014 the number reported against round 33's 51-187 is read from `clade-score.ps1`
  (which streams `lineage.jsonl`), not from the `.md` report table's `inherit` or `births`
  columns, because no report column isolates that quantity. Flagged so the round entry does not
  cite `inherit` or `births` for this rule by mistake.
- `r35-s3` is the only arm with a nonzero `diverged` (3, matching `run.json`'s
  `divergedTotal`), and it is the only arm read with S1's minimum at the lower end (96/100).
  Both numbers are reported as read; no causal claim is made here between them.
- No command failed, no report was missing, and no run's manifest read anything other than
  `status: ended, reason: budget` for the four arms asked about. `r35-s4` was not touched, per
  instruction, and does not appear in any table above.

## r35-s4 (added 2026-09-11 by the main session; the agent stalled before adding it)

Manifest: ended budget, simHash b5d31a48, coreHash cafcb692, physicsJobWorkers 0, divergedTotal 0,
451 wall minutes at 1.11x, 21,299 births, 1,709 alive. Header tokens as the others.
V2: audit 0.0000% and mat resid 0 on all 300 rows. S1: cols 100/100 at every sample from 500 s.
cols abs / x sd / alive: 5,000 s 97 / 7.78 / 1,380; 15,000 s 37 / 6.92 / 1,601; 30,000 s 46 / 7.13 / 1,709.
G0: PASS, root 3312 (kind r, born 4178), 54 alive, min last 6000 s 39, inherited births last 20 samples 42,
4 living clades, aggregate inherit 67. G1: inherit 364 at 5,000, 113 at 10,000, 45 at 15,000, 67 at end;
births 20,430 at 28,000 and 21,299 at 30,000. G2: adult scale 0.338, invest 0.841, brood 1.698.
G3: mean m/s 0.0876, jnt inh 0, food jnt dash, food rig 2.559.
S2 (positions): 5,000 s n 1379, nn h ~0.12, nn 3d ~0.66, clade 1m 1085, leaf 1013 at -13.20, stom 366 at -32.82;
15,000 s n 1600, ~0.11, ~0.62, 1329, leaf 1552 at -19.72, stom 48 at -24.96;
30,000 s n 1709, ~0.11, ~0.63, 1403, leaf 1641 at -22.65, stom 68 at -27.34.
