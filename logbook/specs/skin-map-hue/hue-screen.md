# The skin map's hue, screened

Lineage score fitted on r62-h6202, r63b-a-6203, r63b-b-6203, r63b-c-6203, r62cf-6203 (ridge 0.05): spread over parent-child spread 52.4. Its weights, by size:

investment +0.37, log_margin +0.33, brain_log_neurons +0.14, part_aspect_median +0.09, bbox_aspect +0.08, brain_w_absp90 +0.08, brain_w_absmean +0.07, brain_w_sd +0.07, shape_capsule -0.06, brain_w_mean +0.05, shape_box +0.04, brain_log_links +0.03

orth: the hybrid with the lineage score held uncorrelated over the population with the nine other channels' logits (spread over parent-child spread 2.6). Its weights, by size:

part_aspect_median +1.44, shape_sphere +0.75, share_neural -0.73, brain_w_mean +0.59, mirrored_share -0.58, bbox_aspect -0.50, brain_log_neurons -0.46, log_chain +0.45, shape_box -0.42, share_buoyancy +0.42, brain_log_links -0.41, brain_w_sd -0.32

## All ten worlds

| | today | lineage | rank | hybrid | orth |
|---|---|---|---|---|---|
| bodies | 2131 | 2131 | 2131 | 2131 | 2131 |
| blue, bodies | 11% | 3% | 16% | 16% | 16% |
| violet, bodies | 37% | 37% | 32% | 19% | 18% |
| magenta, bodies | 4% | 10% | 10% | 24% | 24% |
| pink-red, bodies | 18% | 24% | 13% | 16% | 16% |
| red-orange, bodies | 29% | 26% | 16% | 13% | 13% |
| gold, bodies | 0% | 0% | 13% | 13% | 13% |
| blue, clades | 40 of 264 | 5 of 264 | 43 of 264 | 21 of 264 | 25 of 264 |
| violet, clades | 23 of 264 | 35 of 264 | 20 of 264 | 15 of 264 | 26 of 264 |
| magenta, clades | 4 of 264 | 26 of 264 | 13 of 264 | 36 of 264 | 150 of 264 |
| pink-red, clades | 41 of 264 | 85 of 264 | 32 of 264 | 78 of 264 | 23 of 264 |
| red-orange, clades | 156 of 264 | 113 of 264 | 145 of 264 | 87 of 264 | 12 of 264 |
| gold, clades | 0 of 264 | 0 of 264 | 11 of 264 | 27 of 264 | 28 of 264 |
| largest 5-degree bin | 35% | 16% | 32% | 3% | 4% |
| consumer hue IQR / 5-95 (deg) | 17 / 65 | 11 / 39 | 50 / 84 | 50 / 89 | 71 / 142 |
| absorber hue IQR / 5-95 (deg) | 0 / 50 | 16 / 48 | 0 / 41 | 44 / 82 | 120 / 162 |
| mixotroph hue IQR / 5-95 (deg) | 0 / 0 | 14 / 49 | 7 / 10 | 36 / 79 | 8 / 55 |
| parent-child skin, median / 95th | 0.019 / 0.715 | 0.017 / 0.709 | 0.020 / 0.727 | 0.020 / 0.659 | 0.024 / 0.760 |
| stranger skin, median / 95th | 1.368 / 1.986 | 1.333 / 2.013 | 1.399 / 1.992 | 1.305 / 2.031 | 1.341 / 1.970 |
| parent-child past the stranger median | 0.4% | 0.5% | 0.4% | 0.4% | 0.4% |
| parent-child hue, median / 95th (deg) | 0.0 / 18.2 | 0.3 / 8.5 | 0.0 / 34.8 | 0.8 / 21.5 | 1.6 / 56.9 |
| stranger hue, median / 95th (deg) | 64.3 / 139.9 | 53.6 / 109.3 | 55.9 / 142.0 | 59.0 / 148.5 | 56.8 / 149.5 |
| gold reached | 0.0% | 0.0% | 13.1% | 13.1% | 13.0% |

Pairs: 1848 parent-child, 4000 strangers.

## The five worlds the lineage weights were not fitted on

| | today | lineage | rank | hybrid | orth |
|---|---|---|---|---|---|
| bodies | 991 | 991 | 991 | 991 | 991 |
| blue, bodies | 19% | 1% | 19% | 19% | 16% |
| violet, bodies | 31% | 35% | 30% | 12% | 9% |
| magenta, bodies | 7% | 14% | 13% | 27% | 23% |
| pink-red, bodies | 21% | 28% | 15% | 18% | 13% |
| red-orange, bodies | 23% | 22% | 19% | 13% | 15% |
| gold, bodies | 0% | 0% | 4% | 11% | 24% |
| blue, clades | 22 of 139 | 2 of 139 | 23 of 139 | 12 of 139 | 13 of 139 |
| violet, clades | 12 of 139 | 21 of 139 | 11 of 139 | 9 of 139 | 13 of 139 |
| magenta, clades | 3 of 139 | 11 of 139 | 9 of 139 | 17 of 139 | 78 of 139 |
| pink-red, clades | 23 of 139 | 50 of 139 | 17 of 139 | 45 of 139 | 10 of 139 |
| red-orange, clades | 79 of 139 | 55 of 139 | 75 of 139 | 38 of 139 | 6 of 139 |
| gold, clades | 0 of 139 | 0 of 139 | 4 of 139 | 18 of 139 | 19 of 139 |
| largest 5-degree bin | 31% | 16% | 30% | 5% | 7% |
| consumer hue IQR / 5-95 (deg) | 13 / 61 | 10 / 30 | 41 / 73 | 44 / 77 | 61 / 134 |
| absorber hue IQR / 5-95 (deg) | 0 / 53 | 26 / 44 | 0 / 42 | 54 / 78 | 138 / 167 |
| mixotroph hue IQR / 5-95 (deg) | 0 / 0 | 14 / 45 | 7 / 10 | 35 / 77 | 8 / 45 |
| parent-child skin, median / 95th | 0.020 / 0.688 | 0.017 / 0.679 | 0.023 / 0.688 | 0.022 / 0.587 | 0.027 / 0.741 |
| stranger skin, median / 95th | 1.363 / 2.005 | 1.316 / 2.024 | 1.365 / 2.007 | 1.312 / 2.020 | 1.376 / 2.005 |
| parent-child past the stranger median | 0.8% | 0.8% | 0.9% | 0.6% | 0.4% |
| parent-child hue, median / 95th (deg) | 0.0 / 21.9 | 0.3 / 8.1 | 0.0 / 37.7 | 0.8 / 19.9 | 1.8 / 60.7 |
| stranger hue, median / 95th (deg) | 64.3 / 140.1 | 48.2 / 106.4 | 54.5 / 137.3 | 54.3 / 143.1 | 60.4 / 160.3 |
| gold reached | 0.0% | 0.0% | 3.8% | 10.7% | 24.2% |

Pairs: 781 parent-child, 4000 strangers.

