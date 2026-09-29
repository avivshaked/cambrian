# Proposal: round 53, a larder on the bed

*2026-09-29, from the conversation after round 52's launch. For the owner's ruling. The
readings behind it are `logbook/specs/r52-larder.txt` (scripts `scripts/reads/r52-larder.py`
and `r52-larder-cell.py`, round 51's three seeds). Ruled by the owner on 2026-09-29 as D131
and kept as the arithmetic D131 cites.*

## What it is for

The owner's picture of the dead: a corpse settles, enriches its cell a great deal, and anything
that absorbs food there can live on it, while a mouth can eat the corpse whole. Round 51 has
every piece of that, and the bed is still not rich. Four measured things say why.

- **The corpses are small and spread thin.** At the end of seed 1, 17,866 corpses held 226 kJ,
  13 J each. Deaths by age leave their reserve in the corpse, a median of 42 J, and they are 39 to
  50% of deaths after 5,000 s. So a corpse holds about 34 J when it dies, three times round 50's. Even
  so, the corpses bring about 65 W to a bed of 22,000 m2, 3 mW a square metre.
- **They release slowly, and the water spreads what they release.** A corpse gives its cell
  1/2,600 of itself a second. The stirring and the current take about 0.15 of a cell's excess a
  second. So the water in a cell holds about 0.26% of the pile lying in it. The bed's water
  averages 0.6 to 0.9 J/m3, twice the tank's mean, and not more.
- **A still stomach empties its own cell.** It clears about 0.9 m3 a second from a 1 m3 cell,
  and its cell sits at a third to a half of its neighbours'. Its income is about 0.15 W for each
  J/m3 of the water round it, whatever its size. The stomachs that reached the bed cost a median
  of 0.44 W and earned 0.08 to 0.10 W. About 555 of them lay on the bed across the three seeds;
  none had a child there.
- **The water is short of matter, and most of it lies where nothing lives.** On 59% of leaf-steps
  a plant's growth was bound by uptake and not by light. Bodies live at a mean depth of 13 m in a
  tank 45 m deep. The deep water holds five times the surface's matter (0.011 against 0.002
  units/m3, where uptake runs at half speed at 0.05).

## Rule A: a corpse decomposes fast once it settles

A corpse keeps its present slow rate while it sinks (0.000385 a second, a half-life of 30
minutes). It switches to a faster rate the step it settles, on the bed or on a reef top, the two
places where D127 already stops it. So the corpse carries its joules down and releases them where
the owner wants the larder.

- **Why not a faster rate everywhere.** Corpses sink at 5 cm/s, so a plant dying at 13 m
  reaches a 45 m bed in about ten minutes. At a two-minute half-life nearly all of it would
  dissolve on the way down, into the mid-water, and the bed would gain nothing.
- **What it buys.** A stomach lying on a settled corpse catches about 85% of what the corpse
  releases. At a settled half-life of 139 s (0.005 a second), a 0.11 W stomach breaks even on a
  corpse of about 26 J, which most corpses of age hold. A 0.44 W one needs about 104 J. At about
  six minutes (0.002 a second) those become 65 J and 260 J. The food is the same in total. A
  faster rate hands it over in minutes instead of an hour, so a stomach needs the next corpse
  sooner.
- **What it costs mouths.** A pile that dissolves in minutes is a shorter meal for a mouth to
  find. Round 52 reads whether mouths eat corpses at all. If they do, the settled rate is a trade
  between the two eaters, and the screens read both.
- **The build.** One knob, `CorpseSettledDecayPerSecond` (`EVOSIM_CORPSE_DECAY_SETTLED`, 0 means
  the sinking rate, so every recorded world replays), read in `StepCorpses` from the corpse's own
  `Settled` flag. About half a day with its tests. It is a new realisation of every seed when on.

## Rule B: a shallower tank, the matter held

The tank's depth is already a knob (`EVOSIM_DEPTH`). At 25 or 30 m, with the same 15,000 units
and the same area:

- **The matter is 1.5 to 1.8 times as dense**, and less of it can pool in water no body uses. The
  light is unchanged, since it falls per square metre.
- **The bed is near the crowd.** A corpse reaches it in about four minutes instead of ten, and the
  plants' own leakage lands closer to where the stomachs lie.
- **Each seed is cheaper.** The world's step is priced per cell of water, so a third less water is
  a third less of that step.
- **What has to be looked at again.** The bed's tilt (96 m), the shore and its fade and the reefs'
  stems were set for 45 m. So were the founders' depth and the matter islands' depth, 12 m each.
  The header prints the shelf's shares, the bed shallower than 6, 12 and 24 m. The screens keep
  those shares near round 52's rather than keeping the numbers.

Rejected: a narrower tank, since a smaller area takes the light with it and the crowd shrinks
without getting richer. Also rejected: more energy at the start. The founders' endowment only
helps founding, energy seeded into the water burns off within an hour, and what the world is short
of is matter. **The fallback** is a larger matter budget in the 45 m tank: one knob, and a
larger and slower crowd.

## The vents and the places on the bed, where they stand

- **The vent (D067, 2026-09-03)** was an upwelling plume in the old box's rolls, run once as
  round 13's arm B. It was harmless, not load-bearing, and off from round 14. D074 then adopted a
  vent-shaped matter influx. D079 took the world back to a closed budget, with the open budget
  and the vent to "return afterwards, each earning its place". Neither has returned since. The code
  still runs a plume in the tank. But a plume has no vector potential, so a tank with one loses
  the conservative transport, and the water would make its own patches again. It has never been
  screened there.
- **The places (the animal kit's rung C, 2026-09-21)** answered the owner's ask for "natural
  structures on the floor that ecosystems can evolve around". It has four pieces. C1 is a lit
  shelf, built as round 46's beach. C2 is rock: boulders and hollows a few metres across that
  gather sinking snow and corpses and give a lee from the streams. C3 is the seep, a point on the
  bed that releases matter. C4 is the anchoring cell, which lets a part hold to the bed or a
  rock. C2 to C4 were never put for ruling. The reefs (D118) came instead. They cast shade near the
  surface, and none of them is a place on the bed.

### What I would bring back, and when

The two rules above make the whole bed richer. Places make
some of it much richer than the rest, and that difference is what lets a community form. In the order I
would build them:

1. Hollows (C2), in round 54. They gather corpses and snow by the sinking alone, so they turn
   Rule A's larder into piles without any new rule for the food. They are bed shape, from the same
   height field the relief uses.
2. A charging seep, in round 54 or 55. C3 as written releases spent matter, which feeds only a
   plant in the light, and a bed 25 m down is dark. A seep that *charges* spent matter at a point,
   as a leaf does with light, would be the world's second source of energy. It would be a
   hydrothermal vent, feeding stomachs on the dark bed with no light at all. The matter stays closed; only energy
   comes in. Energy has only ever come from light, so this changes what the ecology is, and it is
   the owner's in full. It is also the one that would make a true micro-ecosystem.
3. The anchoring cell (C4), after either. Without it no body can stay at a place in moving
   water. With it, a settled way of life can evolve beside the drifting one.

D079 asks for one change a round where it can be had. Rules A and B go together in round 53
because both answer the same question, why the bed is not rich. A place added in the same round
could not be read apart from them.

## The screens, after round 52 ends

Round 52 holds half the machine, so the screens wait for it. Each is a founding run of 10,000 s
on the farm at 16 threads, one at a time, on round 52's world with Rule A built.

| screen | depth | settled half-life | what it answers |
|---|---|---|---|
| S1 | 45 m | 139 s | Rule A alone |
| S2 | 30 m | 139 s | the depth |
| S3 | 25 m | 139 s | the depth, further |
| S4 | the better of S2 and S3 | about 6 min | the settled rate |

Each is read for the bed's water (`r52-larder.py`), the stomachs on the bed and their net watts,
`upt lim`, the living count and its guilds, the corpses eaten, and the pace. The theatre takes
pictures of the bed at 5,000 and 10,000 s. It is about a day of machine time in all.

## What the owner is asked

1. Rule A, a faster decay once a corpse settles, with the rate chosen by screen.
2. Rule B, a shallower tank with the matter held and the depth chosen by screen. The bed and the
   reefs are rescaled to keep the shelf's shares, and a larger matter budget is the fallback.
3. The order for the places: hollows first, then the charging seep, then the anchoring cell; and
   whether a charging seep, a second source of energy, is wanted at all.
