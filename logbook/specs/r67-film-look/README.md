# Round 67's film look: the coordinator's reads

These files are the coordinator's own reads of round 67's stills rounds, which D342 and D343 cite. The film reviewer's
verdicts are separate, in the round's film folder (`film-stills-<j>-review.md`). They are never given these notes.

| File | What it holds |
|---|---|
| `band-review.md`, `focus-review.md` | The reads of stills 4 (the window band) and stills 5 (the focus set), behind D342 |
| `stills6-review.md`, `stills7-review.md` | The reads of stills 6 and 7, every still at full size; stills 7 is behind D343 |
| `measures*.txt` | Each still's luma, tone, edges, the top and bottom fifths' luma and the green-to-blue ratio |
| `jumps-6.txt`, `jumps-7.txt` | Each take's frame-to-frame change, and its largest jump against its neighbours |
| `measure.py`, `jumps.py` | The two scripts that write them |
| `*.png` | The lower-right contrast crops and the difference that D342's half-float target rests on |

Run either script pinned off the farm's cores, from the repository root:
`cmd //c start "" //b //wait //belownormal //affinity F00 python -I logbook/specs/r67-film-look/measure.py <stills folder>`.
The bars the reads use are the reference film's (`film/reference.json` in the round's film folder): luma 64.52,
`tone_p10` 44 and `edges_p10` 16.09 at 960 wide.
