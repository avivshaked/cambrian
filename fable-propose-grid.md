# Proposal: the water as a grid — cells that hold amounts, and corpses that stay objects

*Fable, 2026-09-08 night, from the owner's argument of the same evening. For the ruling
that replaces D083's vertex field. Absorbed into DECISIONS.md on ruling, then deleted.*

## What the vertex world got wrong

D083 put the water on vertices so that a still body would eat a hole and a moving body
would leave it. Round 30 met the goal rule on it and round 31 shows the hole is there once
the water is stirred less. The owner's objection is to where the hole lives. A take is
spread over the vertices within a metre of the mouth by their weights, so the dip is
centred on the vertices and not on the eater, and its footprint is two kernel radii because
each reduced vertex casts its own. Five bodies feeding from the tails of five vertices
lower the water most at the vertices, where a sixth body sitting beside one of them reads
the loss at nearly full weight. It pays for a meal it did not eat. The mouth's own position
is never the deepest point, because the field has no source there to lower.

The negative vertex, founded at the mouth with the take's amount, was the owner's first
answer and it is the right diagnosis of the wrong representation. A grid puts the source at
every place a body can be. The eater drains its own cell and nobody else's, both books
close because every operation is a transfer between named cells, and the four faults the
vertex field had in a day were all faults of objects a grid does not have.

## The rules

1. **Two grids of cells, each holding an amount.** The detritus grid at one metre a side,
   the matter grid at three, over the whole box: 100 m² by 60 m gives 6,000 detritus cells
   and about 220 matter cells. A cell holds joules or units, never a density; the density
   is the amount over the cell's volume, computed at the read. The two sizes are D083's two
   kernels by another name: a child costs 8 to 16 units of matter and a cubic metre holds
   about one at the seeded density, so the matter draw must reach the old cell's volume.

2. **A body feeds from one cell.** The cell holding the centre of its absorptive tissue,
   which is the centre of mass for a body that is all mouth. A body whose parts straddle
   cells still feeds from that one; at a metre against bodies of half a metre the straddle
   is rare and the harm is a two-cell hole. The draw is the rule the world has: density
   times clearance times tissue volume times the step, capped at what the cell holds, and
   the frozen-availability share when several mouths share a cell. Conception draws matter
   from the parent's matter cell under the same cap.

3. **Diffusion is Fick's law between neighbours.** Each step every pair of face neighbours
   exchanges the diffusivity times their difference in density, scaled by the step over the
   cell size squared. Twice the difference, twice the flow, none at equality. Zero flux
   through the surface and the floor; the horizontal faces wrap at the seams as D077's box
   does. The detritus diffusivity is `NutrientMixingDiffusivity`, the matter grid's is the
   matter field's own, and the two sideways knobs of D084 collapse into one, since a cube
   has no preferred axis. The constructor refuses any setting where diffusivity times step
   over cell size squared exceeds one sixth, which is the scheme's stability limit; the
   round's values sit at 0.01 and 0.11.

4. **The current carries cells downstream.** Each step every cell hands its downstream
   neighbour the fraction of its stock the current would move, speed times step over cell
   size, upwind and conservative, wrapping at the seams. A current spreads a patch along
   the flow by about half the speed times the cell size as a by-product of that scheme; the
   build measures and prints that number and it is stated in DESIGN as the current's
   stirring. No separate along-flow diffusion on the first pass.

5. **Sinking is advection downward.** Detritus sinks at the sink speed by the same
   fractional transfer; what reaches the floor's cells is buried at the burial rule.

6. **A corpse is a particle.** A death founds a particle carrying the body's tissue joules
   and its matter at the body's position. It sinks at the sink speed, rides the current,
   and decays into the cell it is in at `CorpseDecayPerSecond` of what it still holds, a
   tunable, default 0.005 per second, a half-life near two minutes. It is a Core object with
   a position and no collider on this pass. Exudate and excretion stay dissolved, into the
   producer's cell, as they are now. The vent's influx enters the cell at the vent.

7. **The chemical sense reads the cells.** The gradient a sense reports is the difference
   between the body's cell and its neighbours along each axis, over the cell size.

8. **Reads for the record stay.** The per-depth columns are the same layer-and-patch bins,
   summed over cells. The `vtx` column becomes `corpses`, the count of particles, and
   `mat resid` stays the matter identity with particles counted as standing.

9. **The grid needs shared space.** The world refuses the grid without it, as the vertex
   world does, since a cell is a position.

10. **Every earlier config is refused** by the build, per §9's rule, and every seed under it
    is a new realisation. The vertex field stays in the code under its own mode so that
    rounds 30 and 31 remain replayable by their own builds.

## What it retires

D083's vertex field as the base, with its kernel, merge distance, cap and quantum. D084's
two sideways diffusivities. The negative vertex, unbuilt, with the geometry argument above
as its record. The halo, already withdrawn.

## What it does not change

No joint survives founding in any world so far, and the grid does not change what a joint
costs on its first day. What the grid changes is that a corpse is something a body can
reach. That is the first thing a mover could be paid for that a sitter cannot have, and it
is the seam predation on contact builds on.

## Sequence

Round 31 is read as the vertex world's last word. The grid is built behind the field
interface, with its tests held to the vertex field's contracts. The experiment harness
runs the sitter against the mover, the corpse patch and the owner's five-and-five geometry
on it. Then a base round at round 31's prices and mixing, five seeds at 0.01, gives the
grid its own count under the goal rule before anything else is asked of it.

## For ruling

- Cell sizes: 1 m detritus, 3 m matter.
- The one-cell rule: the centre of the absorptive tissue.
- Corpse decay 0.005 per second, particles without colliders on the first pass.
- The current as upwind advection, its along-flow spreading measured and stated, no
  separate along-flow diffusion.
