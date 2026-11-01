# True shape: mouths that must touch bite half as often, and the card bit at a distance

*2026-11-01, the morning after the screen ran, written by the writing lane. Its sources are the pre-registration in r64t-prereg, the read in r64t-read, D272 and the merge commits of the fix. The kill-gap tables it rests on are committed beside it in `logbook/specs/true-shape-kill-gaps/`. Every figure is measured unless it says
otherwise.*

Until this screen a mouth bit when two balls touched, one drawn around each part, and on film the mouth bit open
water. The screen r64t made mouths touch the true surface. They bit about half as often for each second a grazer
lived, and yet grazers ended more numerous, because they stopped eating each other. The screen's last check then
caught the graphics card letting a few kills land at a distance. The cause was in the card's arithmetic, a rule the
card's own notes had stated since September and that one routine had not followed. It is fixed, and the owner ruled
that the film's round runs on true-shape contact (D272).

## Why mouths had to touch

Contact in this world has long been cheap. Each part is treated as a ball whose radius is its reach, the half-diagonal
from its centre to a corner (D196, D252). Two parts touch when their balls do. For a thin blade that ball stands up to
0.55 m past its flat faces.

On 31 October the theatre lane measured eleven recorded kills from two round 63 worlds, on film windows a fraction of
a second before each kill. The table is `ball-table.md` beside this entry. At every kill the two balls had just
touched, the centres apart by the sum of the reaches to about a centimetre. The real surfaces were still 0.32 to 0.97
m apart. On screen a mouth closed on nothing. The owner chose that day that no mouth film goes out until mouths really
touch.

The project already had the remedy, built and switched off. D228's level (a), ***true-shape contact***, keeps the
balls as a first cheap test. A pair whose balls overlap is then tested on its true shapes by GJK, a standard method
for the distance between two convex shapes. Only a pair that passes enters the list a mouth reads. The screen asked
what that costs a grazer world.

## The screen

The coordinator pre-registered r64t before any world of it ran (`logbook/specs/r64t-prereg/predictions.md`). It took
round 64's arm D, the world where grazers had boomed, and ran it to 24,000 s on seeds 6202 and 6203. True shape ran on
both seeds. Sphere contact ran on 6203, and on 6202 the recorded run of arm D stood as its control. The three new
worlds ran as one group on the card, and a sitting first showed the spheres world on 6202 matching the recorded run
row for row.

The table sets each prediction beside what was measured, as true shape over spheres on the same seed. A blow, here, is
one bite by any mouth, and a grazer-second is one grazer alive for one second.

| Readout | Predicted | 6202 | 6203 |
|---|---|---|---|
| Mouth blows per grazer-second, 6,000 to 24,000 s | 0.3 to 0.6 | 0.47 | 0.67 |
| Intake per grazer | about the blow ratio | 0.75 | 0.71 |
| Grazers, mean of the last window | 0.2 to 0.6 | 1.23 (1,540 against 1,255) | 2.16 (2,351 against 1,087) |
| Grazers killed by a mouth, share of grazer deaths | at most spheres' | 0.015 against 0.067 | 0.009 against 0.091 |
| Starved, share of grazer deaths | above spheres' | 0.56 against 0.60 | 0.56 against 0.58 |
| Leaf share of income, last window | 0.45 to 0.65 | 0.75 against 0.59 | 0.80 against 0.62 |
| Leaves standing at 24,000 s | 1.2 to 2 | 1.94 | 2.63 |
| Pace | 0.80 to 0.98 | 1.10, across groups | 0.87 |
| Remains lost through the top | at most spheres' | 0 against 12 | 0 against 4 |

The geometry came out as predicted. Blows fell to about half per grazer-second, inside the range on 6202 and just
above it on 6203. Neither came near the falsifiers of 0.2 and 0.8, so the balls had been what set the rate of bites.
Pace held on the clean comparison, 6203, at 0.87 of spheres'. On 6202 the spheres control ran on an earlier build in a
group of five, so its ratio of 1.10 crosses groups and says little.

## The grazer prediction failed

The pre-registration expected grazers to fall to between a fifth and three-fifths of their number under spheres. They
rose instead, to 1.2 and 2.2 times. The pre-registration named grazers above 0.8 of spheres' as a result that shows
the prediction wrong, and both seeds cleared it. Both held the floor of 30 in every window from 12,000 s, and neither
died out, though 6202's fewest in one window dipped to 24.

What changed was who got bitten. Grazers kept biting grazers at about the same rate, 1,104 kill rows against 1,110 on
6202 and 1,754 against 1,916 on 6203. Under true shape most of those bites wounded and few killed: 87 lethal against
432, and 102 against 821. The share of grazer deaths that were kills by a mouth fell four- and tenfold. Starvation's
share did not rise as predicted. Leaves stood two to 2.6 times as many and supplied three-quarters of a grazer's
income or more.

The read's own reading, which it marks as inference, is that a grazer's ball reached a neighbour's parts before its
surface did. Most lethal grazer-on-grazer kills came from that reach. Take the reach away and grazers lose some leaf
bites, but they stop eating each other, and the leaves they no longer reach stand and feed more of them. I find that
reading convincing, because the ball was doing more than making the film look wrong. It was handing the grazers a
weapon.

## A slow start that looked like a fault

The 6202 world under true shape began slowly. Its grazers averaged 10 between 6,000 and 9,000 s, against 88 under
spheres, and 6203's averaged 234 against 453. Early in the run raw mouth blows at 5,100 s stood 11 and 53 times lower
under true shape. While the screen ran, the coordinator read that gap as a fault in the card's hand-over of touching
pairs to the mouths.

It was not one. Counted per grazer-second, the early windows sat at 0.71 and 0.66 of spheres', near the rest of the
run. The gap had come mostly from how few grazers there were to bite. A probe then ran the 6202 world on the host
engine, the farm's solver on the processor, under both contact forms to 5,400 s. The ratios agreed with the card's.

| Engine and seed, 0 to 5,400 s | Blows per grazer-second, true over spheres | Grazer-seconds, true over spheres |
|---|---|---|
| Host, 6202 | 0.56 | 0.25 |
| Card, 6202 | 0.48 | 0.18 |
| Card, 6203 | 0.32 | 0.06 |

So the slow start belongs to the world. Each host world is its own realisation, so only ratios are compared. The
coordinator asked that the misreading go in plainly. It is a fair lesson: a raw count in a world whose population has
moved is a count of the population first.

## The last falsifier fired

The pre-registration's last falsifier asked the narrowest question. A kill whose part surfaces were more than 0.05 m
apart would mean the card's list of touching pairs was not following its own true-shape test. The card was then to be
checked against the host.

The theatre lane, c8, measured it, with a Sonnet builder it briefed and reviewed. Each true-shape world was replayed
on the host from its checkpoints, every 1,500 s, for ten seconds each. Each kill the run recorded in those seconds was
then found in the host's state and its gap measured. The table is `kill-gaps.md` beside this entry.

| Kills in the replayed seconds | Count | Above 0.05 m | Largest gap |
|---|---|---|---|
| Also named touching by the host | 220 | 0 | 0.0002 m |
| Named only by the card | 12 | 6 | 0.56 m |

The 220 kills both engines agreed on all touched. Of the 12 only the card made, six were 0.10 to 0.56 m apart, so the
falsifier fired. They were few, a handful against hundreds. A further pass ran the card's own GJK routine on the host,
with the host's numbers, and every pair it could test came out apart (`card-gjk-on-host.md`). So the routine's logic
was sound, and the fault lay in how the card computed it. The theatre lane also asked whether the card's list of
touching pairs could have gone stale, and read the card code that ruled it out.

## The card's arithmetic

To see the card's own numbers, a Sonnet builder added a census dump to the farm, from the coordinator's brief on c8's
plan. The coordinator then resumed the 6203 world on the card from 22,500 s. Half a second later it reproduced one far
kill, animal 66874 on leaf 70081. The dump is `census-dump-t6203.json` beside this entry, its five rows unchanged. The
bookkeeping was clean. The card's GJK had ended "origin inside" at its third pass, calling the two parts touching,
where the host found their cores 0.41 m apart. Both cores were flat, zero along one axis.

The cause was a multiply and an add fused into one operation. The card's compiler does this unless told not to, and
the fused result differs from the processor's in its last bits. The card's notes had said so since 26 September, with
the rule that a kernel which must match the processor writes every product in an unfused form. The true-shape GJK on
the card had not been written that way.

The rest followed from those last bits. A fused support point differed slightly from the vertex the routine already
held, so an exact test for a repeat missed it. The routine added a near-duplicate fourth vertex, making a tetrahedron
almost without volume. Its sign tests then read the origin as enclosed, and two parts 0.1 to 0.56 m apart were called
touching.

The fix, built by a Sonnet builder from the coordinator's brief and merged at `fe0548e7`, writes every product in the
GJK unfused. It also refuses "inside" from a tetrahedron whose volume is under 2^-18 of its longest edge cubed, and
takes its nearest face instead. On the fixed card three resumes at far kills lost those kills and kept every other
kill of their stretch, 3 of 3, 19 of 19 and 10 of 10. Groups of 32 and 64 were identical over 30 s.

## The ruling, and what it leaves

On 1 November the owner ruled that the film's round runs on true-shape contact (D272), on the coordinator's
recommendation. The smell build stays off in that round until its own screen has run. Level (b) of D228 is not used.
The card's support for the shape family's rounded parts under true shape was merged the same morning (`a1e443af`), its
products also written unfused. On the card, a family world under true shape was identical at groups 32 and 64 over
600 s.

The ruling leaves three things open. The screen's ratios were measured on the unfixed card. The far kills were a few
in a hundred of its kills, so the ratios stand, but that they stand unchanged is an inference. Nothing of the screen
has been seen in the theatre. And there are no pictures with this entry, because no world with true-shape kills has
yet been filmed. The first mouth film from a world on true shape is owed, from the film's round. Frames from the
sphere-contact film would show the old fault, so none are borrowed here.

A personal note to close: the fault that took a morning to find was one the project had already written down, five
weeks before, with its cure. A rule in a notes file binds only the code written after someone reads it.

## Sources

| File | What it holds |
|---|---|
| [`r64t-prereg/predictions.md`](specs/r64t-prereg/predictions.md) | the screen's design, predictions and falsifiers, committed before launch |
| [`r64t-read/README.md`](specs/r64t-read/README.md) | the read, the host probe, the kill gaps and the fix |
| [`true-shape-kill-gaps/`](specs/true-shape-kill-gaps/) | the ball table under spheres, the kill gaps, the card's GJK on the host's numbers, and the census dump |
| [`DECISIONS.md`](../DECISIONS.md) D196, D228, D252, D272 | the reach, the two levels of true shape, sphere contact, and the ruling |
| [`src/Evosim.Farm.Gpu/CLAUDE.md`](../src/Evosim.Farm.Gpu/CLAUDE.md) | the card's rule on fused products, and the GJK written to it |
| commits `fe0548e7`, `15396c3f`, `a1e443af` | the GJK fix, its tests, and the family hulls under true shape |
