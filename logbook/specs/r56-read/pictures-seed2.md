# Round 56 seed 2 in the theatre, a summer peak and a winter trough

Drawn on 2026-10-05 from the run's snapshots (`scripts/theatre-snap.ps1 r56-s2 -From snapshot -At
22500,27500 -Views side,top,iso`). The build was main at `544f809`, on a worker refreshed from that
tree. A snapshot draws every body at its recorded size and pose, and runs no physics. The run is
`runs/r56-s2/2026-10-04-183358-a174d013`.

The season is `1 + 0.5 sin(2πt / 10,000)` (`seasonAmplitude` 0.5, `seasonLengthSeconds` 10,000), so
the light peaks at 22,500 s and is lowest at 27,500 s. The pictures are in `logbook/images/`:
`r56-s2-t22500-recon-side.png`, `r56-s2-t22500-recon-top.png`, `r56-s2-t27500-recon-side.png` and
`r56-s2-t27500-recon-top.png`.

## What the pictures show

- **From the side, both seconds look alike.** The leaves hold the upper water over the deep end, in
  and around the reef's pillars. Over the slope there is a thin scatter of bodies, and almost nothing
  sits on the bed.
- **From the top, the crowd covers the whole disk in both.** It is thinner over the reef's patches.
  In winter more orange bodies show, most of them over one reef patch right of centre. Which guild
  orange marks is not read here.
- **The joined counts are 5,631 at the summer peak and 5,967 at the winter trough**, with 2 and 3
  bodies unmatched.

## The crowd against the season, from `stats.jsonl`

| t (s) | light factor | alive |
|---:|---:|---:|
| 20,000 | 1.00 | 4,958 |
| 21,250 | 1.35 | 5,634 |
| 22,500 | 1.50 | 5,633 |
| 23,750 | 1.35 | 5,715 |
| 25,000 | 1.00 | 5,855 |
| 26,250 | 0.65 | 5,945 |
| 27,500 | 0.50 | 5,970 |
| 28,750 | 0.65 | 6,372 |
| 30,000 | 1.00 | 7,537 |

The crowd grows through the whole cycle. It does not fall in winter, and it grows fastest as the
light comes back after the trough. My reading, which is inference: at this crowd the season sets
neither the crowd's size nor where the leaves sit in the water. Whether births lag the light by
about a quarter season is untested; the births column against the season would say.
