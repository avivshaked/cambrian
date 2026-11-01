# r65, the smell screen: the read

*By the coordinator on 2026-11-01, the evening the screen ended. Judged against the pre-registration
(`logbook/specs/r65-prereg/predictions.md`, committed at `effc6860` before any world ran).*

## What ran

Six worlds of D's world under true-shape contact, for 24,000 s each, on the card from `scratch/wt-r65` at `effc6860`:

- C, no far smell;
- R, the far smell's strength with the current sense;
- O, R plus the far smell's gradient.

Each form ran on seeds 6202 and 6203, as two groups of three. Group 6202 ran from 13:14 to 16:20 and group 6203 from 16:28
to 19:49. Group 6203's automatic start was refused at 16:20, because group 1's own driver watch had not yet exited, and it
was relaunched by hand. Every world ended on its budget. The two groups share configs, git commit, core and farm hashes,
within each form. The energy books closed in all six (largest residual 0.0045 J). The matter residual passed 1e-5 in four
worlds, C-6203 among them, at most 4.5e-5. That is float drift: r64t-t-6202, with no smell, reached 3.7e-5, and r64s2-d
1.9e-4.

The reads are the script `read-seed.sh` beside this file, run once per seed. Their outputs sit beside it in a folder per
seed, and the grazer deaths in a file of their own.

## The marks

| Mark | Seed | C | R | O | Bar | R | O |
|---|---|---|---|---|---|---|---|
| 1. leaf share of bite income, 21,000-24,000 s | 6202 | 0.77 | 0.76 | 0.76 | C + 0.05 | fail | fail |
| | 6203 | 0.74 | 0.73 | 0.76 | | fail | fail |
| 2. crossings per grazer life, same window | 6202 | 0.039 | 0.013 | 0.037 | R 1.5×, O 2× | fail | fail |
| | 6203 | 0.028 | 0.020 | 0.019 | | fail | fail |
| 3. gather lag, 12,000-24,000 s | 6202 | 2,300 s | 1,940 s | 2,255 s | R 0.75×, O 0.5× | fail | fail |
| | 6203 | 1,970 s | 2,120 s | 2,855 s | | fail | fail |
| 4. pace over the run, × real time | 6202 | 2.27 | 2.17 | 2.16 | within 10% of C | pass | pass |
| | 6203 | 1.99 | 2.00 | 2.18 | | pass | pass |

The outcome is the pre-registration's "both fail". Neither smell form moved leaf share, travel between groves or the speed
of gathering at a bloom, on either seed, while the pace held. By the pre-registration's own rule, the nursery comes before
another world screen.

## Reported beside the marks

| Reading | Seed | C | R | O |
|---|---|---|---|---|
| share of grazer fixes more than 20 m past every grove, 12,000-24,000 s | 6202 | 0.038 | 0.039 | 0.028 |
| | 6203 | 0.048 | 0.030 | 0.030 |
| bred grazers born 21,000-24,000 s: median seconds to first bite | 6202 | 146 | 157 | 210 |
| | 6203 | 145 | 131 | 200 |
| the same: share that die before a first bite | 6202 | 0.32 | 0.33 | 0.27 |
| | 6203 | 0.15 | 0.25 | 0.23 |
| grazers fixed in the last window | 6202 | 3,136 | 5,501 | 4,171 |
| | 6203 | 5,798 | 3,099 | 2,623 |
| grazers born over the run | 6202 | 6,270 | 11,677 | 7,670 |
| | 6203 | 13,432 | 8,004 | 7,396 |
| grazers killed by grazers (whole bodies) | 6202 | 68 | 129 | 51 |
| | 6203 | 204 | 81 | 89 |
| leaves at 24,000 s against C's (`overshoot-read.py`) | 6202 | | 1.42 | 1.18 |
| | 6203 | | 0.79 | 0.59 |
| R's realised reading, s/(s + 1.31) from the downstream median | 6202 | | about 0.8 | |
| | 6203 | | about 0.75 | |

Every world held the grazer floor of 30 in every judged window from 12,000 s.

## What holds across both seeds, and what does not

- O keeps grazers nearer the groves. Its far share is 0.74 and 0.62 of C's. Its newborns take 38 to 44% longer to
  their first bite on both seeds. On 6203 its crossings fall to 0.68 of C's. By inference, the gradient points every grazer
  in a grove at that grove's own centre, so it holds them at home rather than sending them to the next one. The
  pre-registration named this pattern as a way O could fail.
- R travels no more than C. Its crossings are 0.33 and 0.71 of C's, below C on both seeds.
- The number of grazers does not follow the smell. On 6202, R and O held 75% and 33% more grazers than C in the last
  window. On 6203 they held 47% and 55% fewer. Grazer births over the run swing the same way. The two seeds disagree in
  sign, so the size of a grazer population here is set by something other than the smell. The coordinator's interim note
  on 6202, that R's grazers might gather in plumes by slowing where the smell is strong, does not survive the second seed.

## The predictions, scored

| Prediction | Result |
|---|---|
| C's leaf share 0.73-0.82 | 0.77, 0.74: held |
| O gains 0.03-0.10 and passes mark 1 on a seed (moderate) | -0.01, +0.02: wrong |
| R gains 0-0.05 and fails on a seed (moderate) | -0.01, -0.01, fails both: held on the failure, the gain slightly under |
| C's crossings 0.02-0.04 | 0.039, 0.028: held |
| R 0.9-1.4 × C's, failing | fails, at 0.33 and 0.71: outside the range, below |
| O 1.2-2.5 ×, passing on one seed at most (low) | 0.95, 0.68: wrong |
| far share falls in O by a quarter to a half (low) | by 26% and 38%: held |
| C's gather lag 1,500-2,400 s | 2,300, 1,970: held |
| O 0.6-0.9 and R 0.8-1.0 of C's, both failing (low) | O 0.98, 1.45; R 0.84, 1.08; both fail: held on the failure, O's ratios higher |
| newborns' first bite 25-50% sooner in O (low) | 38-44% later: wrong |
| O's leaves end at 0.6-1.0 of C's (moderate) | 1.18, 0.59: wrong on both, once each side |
| R and O within 10% of C's pace | held on both seeds |
| R's realised reading neither above 0.9 nor below 0.1 | about 0.8 and 0.75: held |

## What follows

The screen decides nothing for the film's round. D272 keeps the smell off there, and turning it on is the owner's ruling.
By the pre-registration, the nursery comes next.

For the question of memory (HANDOFF, "Evolved brains hold almost no memory"), the screen's answer is narrower than hoped.
O was handed the direction a memory would have to work out, and its grazers still did not travel between oases. They stayed
closer to home. So within 24,000 s the missing piece was not information about direction. Brains that do not yet use a
sense gain nothing from it, which fits the design's own warning that founder weights cannot cast. As inference, a memory
unit would add a capability that a world screen of this length cannot show being used. The test that can is a nursery
task that rewards climbing a strength-only plume, run with brains with and without a time constant.
