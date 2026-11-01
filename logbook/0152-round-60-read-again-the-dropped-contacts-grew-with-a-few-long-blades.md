# Round 60 read again: the crowd started the dropped contacts, and a few long blades made most of them

*2026-10-31, written by the writing lane from committed files, two weeks after the round. These are the census of
each world's largest part (`logbook/specs/r61-kelp-divergence/census/`), the span reads of the dropped contacts
(`logbook/specs/r60-read/overflow-spans.txt`) and round 60's read. No run was read anew for this entry. Every figure
is measured unless it says otherwise.*

This entry goes back two weeks, before rounds 62 to 64, to read round 60 again. [Logbook
0143](0143-the-crowds-leave-the-islands.md) told how round 60's graphics card dropped contacts. In a dense crowd the
card's list of parts that might touch filled up, and what came past it was lost. Seed 6002 lost 6.26 million
candidates and seed 6003 lost 19,008, so the clause that the card keeps every contact (B2) failed. The entry put it
down to the crowd. On 17 October round 61 found kelp blades: leaves whose genome repeats a part that doubles in length
with each copy. A census then found one such blade in round 60, in seed 6002, and the handoff of the day asked for
round 60 to be read again with it (`logbook/specs/handoff-archive-2026-10-25.md`).

Read again, the crowd started the drops, and the blades most likely made nearly all of them. Seed 6002's first drops
came while no part in the world reached past a metre. But seed 6003 carried a larger crowd and dropped about 300 times
fewer. What seed 6002 had that 6003 lacked was a few dozen blades reaching past 2 m, and one nearly 15 m long. None of
round 60's blades blew up, spinning out of control as round 61's did. The B2 verdict stands, with its cause now shared
between the crowd and the blades in a split that is an inference.

## What a kelp blade is

A genome is a graph of parts, and an edge from a node back to itself repeats that part as a chain. Each copy takes the
edge's scale again. In the kelp family that scale about doubles a part's length per copy and narrows it, until the
thickness stops at the floor of 2 cm ([D196](../DECISIONS.md#d196)). The result is a long, flat, light ribbon that
catches the light.

Contact on the card treats each part as a ball whose radius is the part's half-diagonal, the distance from its centre
to a corner (D196). The census calls that the part's reach. A blade 15 m long touches as a ball more than 7 m in
radius. In round 61 the blades grew to between 10 and 50 m, stopped the world twice and blew up by the hundred
([logbook 0145](0145-the-planted-grazers-starve-and-the-world-changes.md)). Round 62 stopped the compounding, so a
repeated part now takes its edge's scale only once (D196, [the build note](specs/r62-blades.md)).

The picture is round 61's planted world from seed 6103, at 72,000 s, seen from the side. Look at the thin green lines.
Each is a blade drawn at its recorded size, and many stand far above the water's top edge, outside the tank's frame.
The dense band of green squares at the surface is the ordinary crowd of leaves. Round 60's longest blade was about 15
m from end to end, and it grew only in its last few thousand seconds. No frame of it exists yet.

![Round 61, planted world p6103x4, at 72,000 s from the side: thin green blades cross the water and stand above its
top](images/r61/r61-p6103x4-t72000-recon-side.png)

## Two seeds side by side

The census measured the largest part in every world every 1,500 s. The table gives seeds 6002 and 6003 over the
round's last 12,000 s. Look at the crowd and the 99th-percentile reach first. Seed 6003 is ahead on both. Then look at
the count over 2 m and the largest reach, where seed 6002 pulls away.

| Second | 6002 bodies | 6002 largest reach (m) | 6002 over 2 m | 6002 99th percentile (m) | 6003 bodies | 6003 largest reach (m) | 6003 over 2 m | 6003 99th percentile (m) |
|---|---|---|---|---|---|---|---|---|
| 36,000 | 12,912 | 0.95 | 0 | 0.68 | 18,058 | 2.36 | 1 | 1.61 |
| 42,000 | 16,174 | 2.27 | 2 | 1.00 | 18,203 | 2.04 | 1 | 1.55 |
| 45,000 | 17,052 | 3.43 | 14 | 1.14 | 19,341 | 2.30 | 1 | 1.52 |
| 46,500 | 16,710 | 7.36 | 41 | 1.28 | 18,346 | 1.93 | 0 | 1.52 |
| 48,000 | 16,345 | 7.36 | 54 | 1.46 | 17,325 | 1.95 | 0 | 1.55 |
| whole run, candidates dropped | 6,257,897 | | | | 19,008 | | | |

Seed 6003's longest parts had a reach of about 2 m from 30,000 s on, and never went further. Seed 6002's grew late,
from under a metre at 36,000 s to 7.36 m by 46,500 s. The 7.36 m part belongs to body 146701, a leaf of five parts
whose longest is 0.16 m wide, 2 cm thick and about 14.7 m long. The third seed, 6001, ended with 10,558 bodies,
nothing reaching past 1.05 m, and no drops.

## When the drops came

Seed 6002 was rerun from its checkpoints span by span to find where its drops lay ([the
spans](specs/r60-read/overflow-spans.txt)). Nothing dropped before 31,500 s. Then 24 candidates dropped by 33,000 s
and 520 more by 36,000 s. In those spans the largest reach in the world was 0.78 to 0.95 m, and nothing passed 2 m. So
the first drops came from the crowd alone, as 0143 said.

The other 6,257,353 came after 36,000 s, while the blades grew. A rerun of the last span, 46,500 to 48,000 s, with the
list's capacity set as the round had it, dropped 2,491,424. That is about two fifths of the whole run's, in the 1,500
s when the 7.36 m blade was alive. The rerun is not the round itself. Which candidates a full list keeps depends on
which thread writes first, a race. So a resume past a drop parts from the run at its first second. Its count is a
close guide to the round's, and is not the round's own. The spans between 36,000 and 46,500 s were never read one by
one, so how closely the drops followed the blades' growth there is unknown.

The span read gives the likely link. A query met a neighbour once for every cell of the contact search's grid the two
shared, and those repeats filled the list. A part 15 m long lies across many cells, so each body near it shared many
cells with it. My inference is that a few blades flooded the lists of everything around them, which fits a few dozen
long parts making millions of drops. Nobody counted drops part by part. The fix the owner consented to (D193) refuses
a repeat. When the list is full, it keeps the lowest-numbered distinct entries, so the choice no longer depends on
timing. With it the same last span dropped nothing, blade and all.

## The blades did not blow up in round 60

In round 61 the blades' spin ran away under the water's drag, 20 to 27 times per 1,000 s ([the
diagnosis](specs/r61-kelp-divergence.txt)). In round 60 no body diverged in any of the three seeds ([round 60's
read](specs/r60-read/s6003.txt), clause B1). Why not is unread. Most of round 61's dead blades held seven or eight
parts, and their longest parts were 10 to 50 m. Round 60's longest blade held five parts, and its longest part was 15
m. Whether the number of parts, a part's length or something else decides a blow-up was never tested.

## What changes in round 60's read

B2 still fails, and the counts stand, but the reason changes. The crowd started the drops in seed 6002, and the long
blades that grew only in that seed most likely made nearly all of them. That second half rests on two seeds and on
timing, so it stays an inference. Seed 6002's years 7 and 8, on which two of the round's clauses (G1 and M7) were
judged, are still one realisation that no rerun can reproduce (0143).

Neither can drop contacts again as it did. The card's list has refused repeats since round 61 (D193), and repeated
parts stop compounding from round 62 (D196). Nothing in this re-read touches the round's main finding, that the crowds
left the islands.

Two questions stay unread, and both are about the living. A blade touches as a ball of its reach, so in seed 6002's
last 4,500 s each long blade shoved bodies metres from its own surface. Whether that changed who lived there was never
measured. And the census shows leaves in seeds 6002 and 6003 growing thin plates at the thickness floor, with reaches
of 1.5 to 7 m. A thin plate gathers light over a large area for little matter. Whether that helped leaves live in the
empty water between round 60's islands (0142) is a question no read has asked (inference).

0143 blamed the crowd, and the spans bear it out on the first drops. The two seeds side by side suggest that the
blades made the rest.