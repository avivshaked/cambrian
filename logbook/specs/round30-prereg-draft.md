# Round 30 pre-registration, draft (to become logbook/0075 once round 29 is read)

Base: whichever world round 29's reading leaves (D082, D083). Written against round 29's
launcher; if round 29 fails M5 (the world does not stand under added mass) the base reverts
to round 28's launcher plus the senses, and this draft is rewritten.

## What is held

Everything in `rounds/launch-r29.ps1`: round 28's contact world, the three senses on, added
mass 0.5, the global brain retired, single-threaded physics, 30,000 s, five seeds.

## What changes (one build, D083 and D082 folded; D079 bent a second time, said plainly)

1. `EVOSIM_FIELD vertices`, kernel 1 m, matter kernel 1.8 m (the old cell's volume, so the
   matter gate binds where it did; the first seed-2 screen at 1 m bred once in 10,000 s),
   merge 0.25 m, cap 100,000, quantum 0.125 J. Build: 312e9b5 or later (the fill's pass
   bound is the neighbour count; the fourth screen `r30v-s2t` ran on 704be88 with the
   eight-pass bound and 403 short refusals by 2,900 s).
2. `EVOSIM_NEURON_COST 0.005`, `EVOSIM_CONNECTION_COST 0.001` (D082: tenfold), `EVOSIM_WORK_COST`
   0.25, decided 2026-09-07 from round 28's first window (D082 item 3): the founders' work
   was 4.9–6.8% of all spending at 100 s in every seed while 9–17 of 40 were jointed, so a
   stroke cost a jointed body about 15–30% of what it spent, and by 600 s the share was
   0.1–1.7% with 6–10 still jointed. A quarter takes the stroke to 4–8% of a jointed body's
   spending, below the neuron's new share and not the rejected zero (D082's rejected default).
   Read with M3: cheaper work cheapens flapping as much as swimming.
3. `EVOSIM_H_MIXING 0.2`: the vertex walk's sideways step equals the vertical one. Round 29 ran
   0, where the knob meant between-patch exchange only. Flagged to the owner as a world
   setting in the entry.

## Sequencing (owner: fail fast where appropriate)

Screen first: five seeds at dt 0.02 on workers 2–6 as soon as round 29's arms land
(`launch-r30.ps1 -Dt 0.02 -Name r30q-sN`), read within the step only: does the world stand
(alive, audit, vtx against the cap, mat blk against births), does a stomach line form, does
detritus close (det out against det exuded). Not read: anything about joints, swimming,
depth or the film (CLAUDE.md's 0.02 rule). Then the confirming round at 0.01 if the screen
does not fail the world.

## Validity

V1 header: everything of round 29's, plus `field vertices h=1 mh=1.8 merge=0.25 cap=100000
q=0.125`, `neuron 0.005 W + 0.001 W/input, work x0.25`, `h-mix 0.2 m2/s`. V2 audit within a
joule, `mat resid` 0 to the rounding at every sample (D074's identity; the third screen
created 22,000 units before the column existed), `mat short` 0 (the pass bound), `vtx`
below the cap at every sample (read stats `verticesMerged` = 0). V3 manifests `ended
budget`, `diverged 0`.

## Predictions (carry 0072's M1–M6 with these changes)

- M0 (the world stands on vertices): alive at 30,000 s within 0.5–1.5 of round 29's same
  seed; a stomach clade meets D063 in ≥ 3 of 5 (the goal) — falsified by the scorer.
- M1 senses ≥ 20% of alive over the last 6,000 s in every arm (cheaper now).
- M2 jointed guild `jnt inh` ≥ 10 over the last 6,000 s in ≥ 3 of 5.
- M3 movement pays: `food jnt` > `food rig` by 20% over the last 6,000 s where a guild exists,
  `spd jnt` ≥ 0.05 m/s. NEW seam: `food *` is now a kernel read at the body; exudate lands at
  producers, so a stomach beside a producer reads hundreds of J/m³ (the 3,000 s screen: 378
  at rigid bodies against a layer mean of 25). State the expected magnitude from the screen
  and read M3 as a ratio, not a level.
- M4 the detritus loop closes: `det out` within 0.5–2 of `det exuded` per window over the last
  6,000 s in the seeds that pass M0 (the screen's seed 1 accumulated 60 kJ with no stomach
  line; the fourth screen's seed 2 stood at 18 kJ and rising at 2,900 s with 27 inherited
  stomachs, where round 29's seed 2 had turned by then).
- M7 (from the fourth screen) matter is the ceiling: `mat blk` per birth in the last 6,000 s
  is above round 29's same seed by 3× or more, and alive at the same age is above it, since
  the vertex world lets a parent in a fat patch breed while thin water waits (613 against 265
  at 2,900 s). The matter field's count falls as bodies take whole quanta and return them
  where they die (48,000 to 7,600 by 2,900 s): read `vtx` for clustering, not for loss.
- M5 vertex budget: `vtx` < cap at every sample in every arm; `verticesMerged` 0.
- M6 audit closes; drag limiter never binds at 0.01.

## Controls

Round 29's five arms (the price control) and round 28's five (the world control).

## Two-sided readings

To write from 0072's table with the vertex-specific cases: M0 fails (the world stalls on
local matter or the stomachs never form) → the kernel and the quantum are the levers, the
owner's; M0 holds and M2 fails → the muscles still do not pay in a world where sitting
depletes; the stroke's price and the sense scales are next; M4 fails with M0 holding → the
exudate columns are not reachable (h-mix) or the satiation cap binds.
