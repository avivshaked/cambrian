# Round 51's ledger check: can tissue-only husks feed an eater?

*2026-09-27, for D127's conditional rule (the owner: "first check with the ledger whether
tissue-only husks, gathered on the reefs, can feed an eater; add death by age only if they can't").
A calculation from round 50's record and `scripts/ledger.ps1`, not a run.*

## The answer

Tissue-only husks cannot feed an eater. Husks that hold their tissue alone put about 40 W into a tank of 22,000 m2. Dissolved, that
food thins below what a still eater needs, and eaten whole, it is spread too thinly for any body
that does not move several centimetres a second. So by the ruling, death by age with the reserve
kept in the husk goes into round 51. It roughly doubles the larder, and that halves the speed an
eater of husks would need. It does not make a still eater viable. The husks set the table in
round 51, and the water's thrust in round 52 is what could let an eater use it.

## The supply, from round 50 seed 2's last 5,000 s

- Deaths: 16,731, or 3.35 a second. Every one of them was a starvation with the reserve at 0.
- Corpse deposits into the snow: 199,947 J, or 40 W. That is about 12 J a death, the tissue alone.
  A living body holds about 42 J, most of it reserve (`matterLocked` over `alive` at 30,000 s).
- The plants' exudate into the snow: 835,722 J, or 167 W.
- Bodies over a reef cap: about a fifth (seed 2: 2,035 of 9,823 at 30,000 s), half of them above
  the cap's top (1,049). The caps cover 6,092 m2, 28% of the tank.
- Age at death after 20,000 s: median 1,902 s; a quarter under 400 s; 33% over 3,000 s and 20%
  over 4,000 s.

## The eater's side, from the ledger (round 50's config, clearance 10)

Line 4292's founder (`inocula/r51-ledger/stomach-line4292-r50s1-4145.json`) is seed 1's rigid three-part stomach whose line lived on the snow alone. The
pool's one-part stomach is `inocula/pool-r47/stomach-r45s1-100.json`. Both break even at a snow
density of 0.44 J/m3 at the mouth. Line 4292's founder needs about 1 J/m3 for an R0 above 1.

Line 4292's founder:

- Parts: 1
- Volume: 0.0277 m3
- Tissue: 13.8473 J
- Standing cost: 0.1108 W (0.0011 units/s)
| this body as a corpse |13.8473 J, 11.0778 J kept at waste 0.2 |
| claw repaid by one such corpse |250.2499 s of claw |
| mouth at the consumer cap (1) |0.0443 W |
| a capped mouth emptying a corpse of this body |0.3128 s, against 200 s of decay |
| depth (m) | density (J/m3) | net W at birth | break-even (J/m3) | lifetime (s) | R0 | first child (s) | units/child |
|---|---|---|---|---|---|---|---|
|3|0.25|-0.0485|0.4444|29|0|never|0.5709|
|3|0.5|0.0138|0.4444|842|0|never|0.5709|
|3|1|0.1385|0.4444|5956|3|633.5|0.5709|
|3|2|0.3877|0.4444|12780|34|201.5|0.5709|
|3|4|0.8862|0.4444|27196.5|183|87|0.5709|
|3|8|1.8832|0.4444|55713|834|41|0.5709|
|3|16|3.8772|0.4444|60000*|2903|20|0.5709|
|44|1|0.1385|0.4444|5956|3|633.5|0.5709|

The pool's one-part stomach:

- Parts: 1
- Volume: 0.0899 m3
- Tissue: 44.952 J
- Standing cost: 0.3596 W (0.0036 units/s)
| this body as a corpse |44.952 J, 35.9616 J kept at waste 0.2 |
| claw repaid by one such corpse |370.5405 s of claw |
| mouth at the consumer cap (1) |0.0971 W |
| a capped mouth emptying a corpse of this body |0.4632 s, against 200 s of decay |
| depth (m) | density (J/m3) | net W at birth | break-even (J/m3) | lifetime (s) | R0 | first child (s) | units/child |
|---|---|---|---|---|---|---|---|
|3|0.25|-0.1573|0.4444|24|0|never|0.6867|
|3|0.5|0.045|0.4444|826|0|never|0.6867|
|3|1|0.4495|0.4444|4985|11|227.5|0.6867|
|3|2|1.2587|0.4444|12119|94|78|0.6867|
|3|4|2.8769|0.4444|26346.5|498|34|0.6867|
|3|8|6.1135|0.4444|54256.5|2261|16|0.6867|
|3|16|12.5866|0.4444|60000*|7845|8|0.6867|

## Three routes, each worked

### Dissolved, as a larder of snow

Snow released at the bottom is stirred upward at the mixing
rate (0.02 m2/s) against its own sinking (0.002 m/s). So it spreads into a layer about D/w = 10 m
thick, and it is lost to remineralisation at 0.0005 a second. Its steady density is about the
flux over the rate times the layer. Over the whole bed, 40 W over 22,000 m2 is 1.8 mW/m2, which
gives about 0.36 J/m3. Over the reef tops, the tenth of the deaths above a cap's top gives about
4 W over 6,100 m2 and about 0.13 J/m3. Both are under the 0.44 J/m3 break-even. They are further
under it than they look. A mouth empties its own cell, which then holds 5 to 25% of the water
round it (CLAUDE.md, "A mouth is priced at the cell it is emptying").

### A still eater beside a husk

A 12 J husk that dissolves with a half-life of 30 minutes gives its cell 4.6 mW
(12 x ln 2 / 1,800). Line 4292's founder costs 111 mW to keep, so it would need
about 24 husks in its cell at once.

### A mouth eating settled husks whole

The ledger has a capped mouth empty such a corpse in under
half a second and keep 80% of it, 9.6 J. Line 4292's founder with a mouth costs 111 + 44 = 155 mW,
so it needs a husk every 62 s. At 3.35 husks a second and a mean life of 2,600 s (a 30-minute
half-life), the bed holds about 0.40 husks a square metre. A body whose reach sweeps a path 0.5 m
wide meets 0.2 husks per metre travelled, so it needs about 8 cm/s. Round 47's swim probe found
evolved strokers at 2 to 6 mm/s and the best hand-built stroke at 7 cm/s.

## With the reserve kept in the husks of bodies that die of age

If a body dies at 3,000 s, a third of the deaths are by age. Say each of those husks keeps a reserve
of about 30 J, the living mean less its tissue. The husk flux then rises by about 33 W
(3.35 x 0.33 x 30), to about 73 W, 1.8 times the tissue-only larder. The mean husk then holds about 22 J. The
mouth route needs about 4.4 cm/s, and the dissolved route rises to about 0.65 J/m3 over the bed
before the emptied-cell discount. The age is a knob. A lower one adds more reserve to the husks and
shortens every plant's breeding life, so its value is screened for the plant crowd's plateau before
the pre-registration.

## What this rests on

The layer thickness D/w comes from a balance and was never measured. It assumes the current does not reach the
bed; the reefs' fade calms the water near a reef, and the bed's current is not measured. The reserve
an old body holds at 3,000 s is taken from the living mean, since no file holds a reserve by age. The reach width of 0.5 m is a guess at a small body's. Each is read off round 51's own record.
