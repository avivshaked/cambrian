# Round 55 at 30,000 s in the theatre

Drawn on 2026-10-05 from each seed's last snapshot (`scripts/theatre-snap.ps1 r55-s<n> -From snapshot -At
30000 -Views side,top`). The build was main at `544f809`. A snapshot draws every body at its recorded
size and pose, and runs no physics. Every label ends `· OLD-RUN READ`: round 55's config predates the
season's two keys, so the picture-only reader takes the water's shape from it
(`unity/Assets/Theatre/CLAUDE.md`). In the palette green is photosynthetic and orange is absorptive,
a body that feeds by mouth. The pictures are `logbook/images/r55-s<n>-t30000-recon-side.png` and
`-top.png` for seeds 1, 2 and 3.

| seed | joined | unmatched | what the pictures show |
|---:|---:|---:|---|
| 1 | 4,474 | 3 | Leaves spread through the upper half of the water, among the reef's long pillars. A small knot of orange bodies sits at the deep end, near the left wall |
| 2 | 4,952 | 5 | Leaves are densest around the pillars at the deep end. Orange bodies are spread over the whole slope, mixed with green, down to the bed |
| 3 | 7,491 | 2 | The densest of the three. From the top, orange bodies fill most of the south-east half of the disk and its rim, and green holds the north and the centre |

## The guilds at 30,000 s, both rounds

Counted from each run's `positions.jsonl.gz` row at 30,000 s, by `guild_of` in
`scripts/positions-read.py` (flag 1 absorptive, 2 jointed, 4 photosynthetic). A stomach is absorptive
and not photosynthetic; a mixotroph is both.

| seed | round 55 alive | stomachs | mixotrophs | round 56 alive | stomachs | mixotrophs |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 4,474 | 33 | 41 | 8,110 | 4 | 142 |
| 2 | 4,952 | 199 | 5 | 7,536 | 12 | 113 |
| 3 | 7,491 | 652 | 112 | 8,411 | 14 | 81 |

Round 56 kept round 55's seeds, roles and pool settings (seed 1 without the nursery's genomes, a floor
pool share of 0; seeds 2 and 3 with them, at 0.6). It changed one thing: the season, at amplitude 0.5
on every seed. Seed for seed, pure stomachs fell from 33 to 4, from 199 to 12 and from 652 to 14,
while the crowd grew. My reading, which is inference from one realisation a seed: under the cycled
light, the bodies that only eat nearly vanish, and bodies that also catch light take their place on
two seeds of three. Neither round's pre-registration predicted the guild mix, so this is an
observation for the entries, not a tested result.
