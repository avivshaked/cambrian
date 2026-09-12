# Reference frames

Three real frames from the theatre, copied here on 2026-09-12 so the interface can be
designed over the scene it will actually sit on. They were chosen to span the legibility
range rather than to look good: a busy near-black crowd, a bright pale field, and a nearly
empty wide shot. A design that reads on all three reads everywhere.

Originals are under `scratch/snaps/`, which is gitignored and gets cleaned out; these copies
are the ones `../SPEC.md` points at.

| File | What it is | Living bodies |
|---|---|---|
| `01-busy-crowd-r37tank2-t600-close.png` | The close portrait: a crowd at 600 s in the tank smoke run, three-quarter angle, no box and no markers. Green is photosynthetic tissue, grey structural, pink a joint, orange an eater — the **hardest case for status colour**, because four of the palette's hues are already spoken for by the bodies. | 120 |
| `02-bright-surface-r36-s1-t5000-sky.png` | Looking up at the surface from below: Snell's window, the sun, bodies in silhouette. A pale blue-grey field fills most of the frame. **The hardest case for text legibility** — anything tuned only for a dark ground fails here. | 307 |
| `03-near-empty-wide-r36-s1-t15000-iso.png` | The fitted wide shot of a whole box world, from a corner. Almost entirely black, a wireframe box, and bodies reduced to coloured dots at roughly thirteen pixels a metre. **The case where the overlay is most of what is on screen.** | 576 |

Two notes for whoever designs over these.

The **white text block in the top-left of every frame** is not a mock-up. It is the current
snapshot stamp — arm, simulated second, living count, view — burnt into every picture the
project's agents take, and those pictures end up in the permanent written record. It is
a later pass's work (see `../SPEC.md` §8), and redesigning it is in scope.

The frames come from **different builds of the renderer**, which is why the water looks
unalike between them: the surface, sun and light shafts arrived on 2026-09-11 and the tank
on 2026-09-12. Frames 1 and 2 show the current look. Frame 3 is the wide diagnostic view,
which is deliberately diagrammatic and has no skin to speak of.
