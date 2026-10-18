# Proposal: a glow in the theatre that shows a real quantity

## The question

Should the theatre make some parts glow? The owner asked this on 2026-10-06. On the same day they
deferred bioluminescence as a world rule (bodies that make light others see or sense) until after
round 58's light sense at the earliest. This proposal covers only a glow drawn by the theatre, which
changes nothing in the simulation.

## The rule it must keep

The theatre shows what happened. A viewer reads a glow as a trait of the creature, so a glow that
means nothing misleads as much as a gap at a joint did. A glow is drawn only where a real,
recorded quantity puts it, and its brightness follows that quantity.

## Candidates

1. **A stomach while it eats.** It glows with its intake rate in that frame. In winter the water
   darkens, and the bodies still feeding would light up. Round 57's matter-eater guild grows in every
   winter, and this would make that visible in a film and not only in a chart. My guess, untested, is
   that this is the most telling of the three.
2. **A leaf by the light it catches.** It glows with its absorbed watts. This mostly repeats the
   daylight already in the picture, so it says less.
3. **A sensing part, once round 58's light sense exists** (D154). It glows with its reading. It is not
   available before round 58.

## What it costs and touches

- Code goes only under `unity/Assets/Theatre/`, so no recording stops replaying.
- Live frames have the quantity on every step.
- A snapshot reconstruction has no intake rate, so it would draw no glow. Each frame would say so.
- The film's grade and brightness check need a reference frame with the glow switched off.

## Later: light as a world rule

This is recorded here so that it is not lost. It is not proposed now.

**The idea.** A cell type that makes light. Its output is set by the brain and paid for from the
reserve.

**Who would see it.** Only a body with round 58's light sense (D154), which reads the light at each
part. Until that sense exists, a glow is seen by nobody and is pure cost. So the rule cannot come
before round 58.

**What selection might find, by guesswork drawn from the deep sea:**
- a lure, if bodies learn to move toward light and something eats bodies;
- a warning;
- a signal between kin.

**One trap to design out first.** Light that a leaf can absorb is energy. Emission must cost more
than any neighbour can harvest from it, and the ledger must book it, or leaves will farm glowing
neighbours.

## What the owner is asked

Whether to build candidate 1 as a switch, off by default, and show one test frame of a winter crowd,
with and without it, before anything else uses it.
