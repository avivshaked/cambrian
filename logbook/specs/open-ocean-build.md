# The open square's build: what the processor does, and what the card must copy

*Started 2026-10-18 by the speed agent, on the coordinator's order for D204's first step. It
records the rules the build chose, so the card's copy can follow them and a later reader can
tell a rule from an accident. The survey of what assumed the tank is
[`open-ocean-survey.md`](open-ocean-survey.md).*

D204 replaces the glass tank with a square whose opposite edges are joined. A body that swims
out of one side comes back in at the other. The processor's half of the first step is in
this branch. Bodies touch across the join, and the floor's waves meet themselves there. The
grid of matter and snow masks its cells against that floor. The islands keep 45 m from every
edge, and the coral is placed inside that margin. The water's new current waits for the
coordinator's spec, and the card's copy of all this waits for the processor's to pass its
tests.

## Bodies touch across the join

The box has wrapped a body's root at the edges since D077, but two bodies on either side of
an edge never touched. Their contact read the plain difference between the two centres, and
that difference is the whole box. A new setting, `ReadAcrossSeams`, turns on the short way
round. It is off by default so that every recorded box world replays, and a tank refuses it.

The setting changes two things. The contact grid makes its cells a whole fraction of the box along both
horizontal axes, so a cell number can be counted round the edge. And the push reads each
difference between two centres the short way: a component more than half the box long is
moved by the box's length, once. A pair that does not reach across an edge takes no new
arithmetic at all, so its push is the same to the last bit as without the setting. The tests
in `SeamContactTests` hold both properties.

## The floor joins at the edges

The tank's floor is a sum of waves pointing in random directions, which cannot meet itself
at an edge. In the square each wave is turned to the nearest one that fits a whole number
of times along both sides. The seed's draws are the tank's, in the tank's order, so a seed's
square floor is its tank floor with every wave nudged. The tilt is refused in the square,
because a plane cannot join.

Take a wave of the largest band, which fits three times to a side at the default scale. When
it is nudged it can turn by a dozen degrees and change its length by a fifth. That is about as much as the band's own
random spread. I worked this out from the lattice of whole wavenumbers and have not measured
it on a drawn floor.

## The grid masks its cells in the square

A box used to be all water. With a shaped floor it takes the tank's mask: a cell is water
when its centre is above the floor. A point below the floor is lifted to the column's lowest
water, as in the tank. A shoal can leave a column with no water in it on a coarse grid. Such a
column hands the point to the nearest column that has water, searched ring by ring round the
edges.

## The islands and the coral keep off the edges

The tank's margin from the glass becomes, in the square, a margin from every edge. It uses
the same setting, so the islands keep 45 m off the edge as D204 says. The coral's colonies
keep the same margin, as they keep it from the glass in the tank. So no rock stands near an
edge, and nothing about the coral has to be read the short way round.

## The smells cross the join

The smells of carrion and of leaves are kept on grids of 2 m cells. In the box they stopped at
the faces, so a body beside an edge smelled nothing of a corpse a metre away across it. The
same setting makes the grids rings along both horizontal axes. A corpse's cone of smell is laid
across a seam, and a read near a seam takes its neighbours from the far side. The box's sides
must be a whole number of cells, which 300 m and 2 m are, and the cone must be narrower than
the box.

## What the card copies

The card's realisation is its own, since single precision is a new realisation of every seed,
so its arithmetic need not match the processor's to the bit. It follows the same rules, and
the first five are written in the kernel templates:

- the contact grid, and its lists for giant bodies, count cells round the edge;
- the push reads the short-way difference in the processor's own order, the plain difference
  moved by the box's extent once, so that the card in double is the solver to the bit (in single
  a pair across a seam loses about 1e-5 m of its separation in a 300 m box);
- the root is wrapped inside the card's block of steps, with what the sum rounds away kept in
  the position's carry;
- the smell grids are rings, the carrion's fill taking each cell's copy nearest the corpse;
- the chemical sense finds its cell round the seams, as the processor's grid does;
- the open ocean's current, folded into the box at each entry as the processor folds it;
- the transport of matter and snow stays on the processor in a box: the grid offers the card no
  plan there, so the card's transport declines it.

The floor needs nothing new on the card. The square's floor is centred at the origin, so the
kernel's offset by the tank's radius is zero there. The card carries a box only with its seams
read across and with the open ocean's current. It refuses the ocean with the shore's fade or
with reefs, until their fades around it are shown to match. On ILGPU's CPU accelerator in
double the square is the solver to the bit after every block, a death and a birth included
(`GpuSquareTests`, 2026-10-19). The test places bodies clear of the seams, reaching over one,
and touching across one. The root's wrap inside the card's block has no partner in the solver,
which leaves the wrap to the farm's Simulation between steps. So the wrap is read on the card,
at the farm's level.
