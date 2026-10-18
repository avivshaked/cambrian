# Round 60's read: the islands feed the tank, and the crowds leave them

*2026-10-16, written the day the round ended, against its pre-registration (logbook/0142, committed as `22ab1d73`).
Each finding says whether it is measured, computed or guessed. The reader's output is
`logbook/specs/r60-read/r60-read.txt`, and the spans of dropped contacts are `logbook/specs/r60-read/overflow-spans.txt`.*

## What happened

Round 60 was the first tank whose matter was not a closed stock. Food arrived on a few islands and was buried at
the floor of every column, fast in the open water and slowly on the islands, so the water between the islands
was fed only by what drifted from them. The tank was four times round 57's area. The three seeds ran together on
the graphics card, as one card group, from 01:19 to 10:04 on 2026-10-16, and every world ended on its budget of
48,000 s, eight years of 6,000 s. "The living" counts every body, leaves included.

The seeds drew three different maps. Seed 6001 drew eight islands, the largest 97 columns of 318. Seed 6002 drew
three islands of nearly equal size, 117, 111 and 100 columns. Seed 6003 drew five islands, one of them 258 of its
307 columns (measured, from the matter at 100 s). These were very different worlds, and the reads below differ by map
as much as by anything else.

The crowds grew all round and ended at 10,558, 16,345 and 17,325. They spread off the islands into the water between
them, and in each world one line came to hold most of the tank.

## The predictions

| # | prediction | result |
|---|---|---|
| M1 | at least 1,000 alive at every year's end from year 2, in 3 of 3 | **failed**: seeds 6001 and 6002 had 791 and 897 at year 2's end; seed 6003 had 5,280, and from year 3 all three held (measured). As guessed |
| M2 | at least 0.3 of the 10 m squares empty at every year's end, in 2 of 3 | **failed** in all three: seed 6003 fell to 0.14 by 18,000 s, seed 6001 to 0.25 by 42,000 s, seed 6002 to 0.24 at the end (measured). As guessed |
| M3 | at least 0.6 of the living on the islands at 48,000 s, in 2 of 3 | **failed**: 0.34, 0.38 and 0.38 (measured). As guessed |
| M4 | at least 3 islands each with 5% of the living, in 2 of 3 | **failed**: held only in seed 6002, whose three islands held 0.19, 0.12 and 0.07. In seeds 6001 and 6003 one island held the rest (measured). I had guessed it holds |
| M5 | burial 0.8 to 1.2 times the influx over year 8, in 3 of 3 | **held**: 0.87, 0.90 and 0.98 (measured) |
| M6 | the desert's median column below a leaf's break-even, in 1 of 3 | **held** in seeds 6002 and 6003, at 0.00021 and 0.00020 units/m³; seed 6001's was 0.00028 (measured) |
| M7 | at least 20 crossers a year without photosynthetic tissue in years 6 to 8, and a root line on 2 or more islands, in 1 of 3 | **held** in seed 6002 alone, with 21, 71 and 126 crossers, but weakly: see the dropped contacts below |
| L1 | no root line over half the living, in 2 of 3 | **failed**: 0.999, 0.926 and 0.773 (measured). As guessed |
| G1 | at least 100 leaf kills a year by grazers in 2 of years 6 to 8, in 1 of 3 | **held** in seed 6002 alone, with 703, 1,365 and 621, but weakly, as M7. I had guessed it fails |
| S1 | smell readers climb the gradient | **unread**: no grazer in any seed read the smell (measured) |
| B1 | the books balance, in 3 of 3 | **held** (measured) |
| B2 | the card holds, with no contact candidate dropped, in 3 of 3 | **failed**: seed 6002 dropped candidates in 6,257,897 queries and seed 6003 in 19,008 (measured) |
| B3 | seed 6001 resumed from 45,000 s matches the unbroken run | **held** (measured) |

## Where the crowds went

<the year-by-year shape: the islands fill first, then the water between them; seed 6003's one large island fills
the tank by 18,000 s; the seasons as a ratchet, growth in one half of each year and a dip of at most 5 to 10% in
the other; the desert's matter held low while bodies stood in it>

## Seed 6002's grazers

Only seed 6002 grew bodies with mouths. Sixteen appeared by 18,000 s, and they rose to 149 by 36,000 s. Then they
fell, to 45 at 42,000 s and 9 at the end (measured). Their leaf kills peaked in year 7 at 1,365. The same seed had
the most crossers, but the grazers were not among them: of 4,448 bodies seen over two islands in year 8, 4,309
were leaves, 126 had no photosynthetic tissue and none had a mouth, and only one crosser in the whole round had a
mouth (measured). M7's crossers were bodies with neither leaf nor mouth. The ocean the owner asked for, of swimmers
that cross to graze, has not begun. Seed 6002 is the only seed whose islands were three and of a size, and I think
the map is why it grew grazers at all (inference).

## The dropped contacts

The card finds which bodies might touch by sorting them into a grid and listing, for each part, the parts that
share its cells. That list holds at most 256 entries. In a dense crowd it fills, and what is past it is dropped. The
check after the round found that seed 6002 had dropped candidates in 6,257,897 queries, and seed 6003 in 19,008.

Seed 6002 was rerun from its checkpoints span by span to find where the drops began (measured,
`overflow-spans.txt`). Nothing was dropped to 31,500 s. Twenty-four queries overflowed between 31,500 and
33,000 s, 520 between 33,000 and 36,000 s, and all the rest after 36,000 s, when the crowd passed about 13,000.

Which entries a full list keeps depended on the order the card's threads wrote them, which is a race. So a world that
overflows does not replay from its checkpoints: seed 6002 resumed at 46,500 s differed from the round from its first
second. Years 7 and 8 of seed 6002, where G1 and M7 rest, are one realisation that no rerun can reproduce. A dropped
candidate may be a contact the world never felt, and a mouth that never touched its leaf, so the grazers' kills in
those years may be lower than the world's rules would give (guessed).

The owner consented on 2026-10-16 to raising the list's size (D193). At 512 the early drops went, and the span
from 31,500 to 33,000 s matched the round at every second. But the dense span from 46,500 to 48,000 s still
overflowed in 915,155 queries (measured). The reason was in the code. The grid enters a part in every cell its
sphere covers, and the query walks every cell of its own range, so a pair sharing several cells arrived several
times, up to eight. Each arrival took a slot before the list was sorted and its duplicates removed. Most of the
full lists were full of repeats.

The fix keeps the list sorted as it fills, refuses a repeat, and past the cap keeps the lowest distinct entries
whatever order they arrive in. With it at 512, the dense span dropped nothing, ran at 0.74 times real time
against 0.68 without it, and two runs of each span were identical in every record (measured, on branch
`cand-cap-det`). It says that span never needed more than 512 distinct candidates a query, and nothing about a
denser world. Round 61 runs on it, and its B2 asks again.

## The pace

At 16,700 bodies a world ran at 0.75 times real time alone on the card (measured, on the span 46,500 to 48,000 s).
The round's three worlds together took 8 h 45 min, against a guess of 9 to 11 h.

## The pictures

<theatre frames of round 60 at 6,000, 24,000 and 48,000 s, and what was seen>

## Caveats

There was no control on these seeds, so the predictions read against fixed bars, not against a closed tank. The
three maps differed so much that a seed's result says as much about its map as about the world's rules. Round 61
fixes one landscape for every world (D192) for this reason. And seed 6002's last two years did not replay.

## What I take from it

<to write once the pictures are seen>
