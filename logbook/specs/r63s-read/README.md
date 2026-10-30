# r63s's G5 read

The shape screen is pre-registered in `logbook/specs/r63s-prereg/predictions.md`. It ran the arm "on" on seeds 6202 and
6203: `r63c-b`'s world with D233's family listed, D229's shaped added mass on and the form walk at 0.08. It was one card group
from `scratch/wt-shape-land` at `d03a19b2`, 24,000 s each, in `runs/r63s-on-{6202,6203}/2026-10-29-220442-f73b299d`. Both
worlds reached their budget, in 149 and 151 wall minutes. The manifests carry config `f73b299db0a24445`, farm
`dc035ea9779311e1`, Dynamics `6a5a06a4d8eeb78b` and Core `811b6e062fe6d263`, the pre-registration's values. The arm
"off" is `r63c-b`'s own runs, `runs/r63c-b-{6202,6203}/2026-10-29-123625-716cd9ee`. This folder is the read, which
`scripts/reads/shape/r63s-read.ps1` makes again from the runs.

- `g5-6202.txt` and `g5-6203.txt` are `scripts/reads/shape/g5-read.py` at `--every 1`: gates 2 and 3, and the reported
  lines.
- `g2-survive.txt` is `scripts/reads/bloom/g2-read.py` with `--survive-from 12000` over both "on" worlds. Only its gate 1
  is G5's. Its gates 2 and 3 are G2's, which this screen does not judge.

## What it says

The arm passes every gate on both seeds, so by the pre-registration the family and the shaped added mass go into G7's
round (D254).

| Seed | Gate 1, survive | Gate 2, family share of jointed bodies | Gate 3, moving-grazer speed on/off |
|---|---|---|---|
| 6202 | pass | 0.193 (141 of 732) | 0.978 (0.2474 against 0.2529 m/s) |
| 6203 | pass | 0.180 (109 of 605) | 0.984 (0.2617 against 0.2658 m/s) |

- **Grazers survive.** The window means from 12,000 s are 64, 68, 99 and 85 on 6202, and 35, 60, 88 and 83 on 6203,
  and D222 passes on both seeds. The sensitivity at a floor of 40, not part of the gate, fails on 6203 in its first judged window
  (mean 34.5).
- **The shapes are reachable and they last.** About a fifth of jointed bodies carry a family limb at 24,000 s. The arm
  "off" carries none, as it must.
- **Swimming costs under 3 %.** The ratios sit well inside the noise the pre-registration measured between two seeds of
  one world (0.863).

## What it does not say

- It does not say that selection favours the shapes. Few grazers carry a family limb: 22 of about 3,400 with moving pairs on 6202,
  and 40 of about 2,900 on 6203. Most family limbs are in other bodies. On 6202 the carriers swim slower than the rest
  (0.136 against 0.248 m/s), and on 6203 they swim level (0.264 against 0.262). The form genes have barely walked: the
  median family limb sits 0.108 and 0.000 range log widths from the member it entered at. That mutation alone carried
  the share past 10 % fits all of this, and is an inference.
- Nor does it give the fin law's cost in pace. "On" ran 1.32 times as fast as "off" on both seeds. The two sat in different groups,
  two worlds against `r63c-b`'s four, so the ratio says nothing about the law.
- Its "jointed" is not the stats column's. The read's count (732 and 605) is bodies whose genome grows a non-root node on a
  joint that is not Fixed. That is the pre-registration's definition, and it is wider than `stats.jsonl`'s `jointed`
  column (238 and 156 at the end).

## The predictions

- **Gate 1 holds on both seeds:** right.
- **Gate 2 passes at 15 to 40 %:** right, at 18 and 19 %.
- **Gate 3 passes at 0.85 to 1.1:** right, at 0.98 on both.
- **The form genes walk less than a third of their log width:** right, at a median of 0.11 and 0 for family limbs. The median for every grown family node is 0.13
  and 0.14.
