# 0075 — The vertex world at the new price

**2026-09-08**  ·  round 30, pre-registered before launch; D082 and D083 in one build

Round 29 (logbook/0072) read in the small hours: the senses are carried by a third to a
half of the population in three seeds of five at today's price, no seed keeps a jointed
body, the goal holds in two seeds of five, and the world stands under added mass in every
seed. The owner had ruled two things the evening that round launched. D082 brings the
neuron and its inputs down about tenfold and the stroke's work to a fraction. D083 puts the
water on vertices, so a body feeds from the water at its own position and a still body eats
a hole. Both run in one build here, with round 29 as the price control and round 28 as the
world control. D079's one-change rule bends a second time, and D083 says so; this entry
says it again because the reading below has to be made with that in mind.

## What is held

Everything in round 29's launcher: round 28's contact world, the three senses on, added mass
0.5, the global brain retired, single-threaded physics, 30,000 s, five seeds, the same
irradiance, exudation, mixing, matter and floor settings. Round 29's M5 held, so the base is
round 29's world and not a reversion to round 28's.

## What changes

1. **The field.** `EVOSIM_FIELD vertices`, with the detritus read through a 1 m kernel, the
   matter through 1.8 m (the old cell's volume to within 3%, so the matter gate binds where
   it bound), merge distance 0.25 m, a cap of 100,000 vertices and a quantum of 0.125 J.
   The build is commit `312e9b5` or later, whose take fills across the vertices in reach
   with a pass per neighbour; the seed-2 screen on the eight-pass build refused 403
   conceptions as short by 2,900 s and the seed-1 screen on this build refused none
   (logbook/0074).
2. **The price.** `EVOSIM_NEURON_COST 0.005`, `EVOSIM_CONNECTION_COST 0.001` (D082's
   tenfold) and `EVOSIM_WORK_COST 0.25`. The work fraction is set from the record as D082
   asked: in round 28's first window the founders' work was 4.9 to 6.8% of all spending in
   every seed while 9 to 17 of 40 bodies were jointed, so a stroke cost a jointed body about
   15 to 30% of what it spent, and by 600 s the share was 0.1 to 1.7% with 6 to 10 still
   jointed. A quarter takes the stroke to 4 to 8% of a jointed body's spending, below the
   neuron's new share and above the zero D082 rejected. The cost is the one D082 stated:
   cheaper work cheapens flapping as much as swimming, so M3 reads the guilds' feeding
   against each other.
3. **Sideways mixing.** `EVOSIM_H_MIXING 0.2`. In the cell world this knob was the exchange
   between patches and round 29 ran it at 0. In the vertex world it is the sideways step of
   every vertex's random walk, and at 0 the water would move only up and down. Set equal to
   the vertical diffusivity so the walk is isotropic. This is a world setting and the
   owner's to veto; it is stated here so that the veto has a number to act on.

## Sequencing

The owner's instruction is to fail fast where it is appropriate. Five seeds run first at
dt 0.02 for 10,000 s on workers 2 to 6 (`r30q-s1` to `r30q-s5`), read within the step and
only for what a fast step can say: the world stands, the two identities close, a stomach
line forms, the detritus loop closes, the vertex count stays under the cap. Nothing about
joints, swimming, depth or the film is read from them (CLAUDE.md's 0.02 rule; the fast step
under-drives evolved muscle). If the screen does not fail the world, the round runs at 0.01
for 30,000 s on the same five workers (`r30-s1` to `r30-s5`), and that is the round this
entry scores.

## Validity checks

- **V1 header.** Every token of round 29's, plus `field vertices h=1 mh=1.8 merge=0.25
  cap=100000 q=0.125`, `neuron 0.005 W + 0.001 W/input, work x0.25` and `h-mix 0.2 m2/s`.
  Read from the report, not the launch command.
- **V2 identities.** `audit` within a joule at every sample. `mat resid` 0 to the rounding
  at every sample, which is D074's matter identity as a column; the third seed-2 screen
  created 22,000 units of matter before the column existed. `mat short` 0. `vtx` below the
  cap at every sample and `verticesMerged` 0 in the statistics.
- **V3 manifests.** `ended budget`, `diverged 0`, `physicsJobWorkers 0`, `gitDirty false`,
  and every arm on one `coreHash`.

## Predictions

The two bars of D081 apply. The goal is D063's three seeds of five. The reference is the
base world's own count, which is round 29's two; round 28's three stands beside it as the
world control's count.

A seam to state before M3 is read. In the cell world `food jnt` and `food rig` were the
edible density of the 25 m³ cell the body fed from. In the vertex world they are a kernel
read at the body's own position, and exudate lands where the producers stand, so a stomach
beside a producer reads hundreds of joules per cubic metre while the layer mean is tens
(the 3,000 s screen read 378 at rigid bodies against a layer mean of 25). The two guilds'
rates are read as a ratio against each other, never as levels against the record.

| # | prediction | falsified by |
|---|---|---|
| M0 | **the world stands on vertices**: `alive` at 30,000 s within 0.5 to 1.5 of round 29's same seed in every arm, and D063 as amended holds in at least 3 of 5 by the scorer | `alive`, the scorer |
| M1 | **the senses are taken up at the new price**: `sense` is at least 20% of `alive` at every sample over the last 6,000 s in at least 4 of 5 (round 29 read 3 of 5 at today's price, with seeds 1 and 3 at 1 to 2%) | `sense`, `alive` |
| M2 | **a jointed guild persists**: `jnt inh` at least 10 at every sample over the last 6,000 s in at least 3 of 5 (round 29 read 0 in every arm) | `jnt inh` |
| M3 | **movement pays where it exists**: in every arm with a jointed guild at the end, `food jnt` exceeds `food rig` by more than 20% averaged over the last 6,000 s, with `spd jnt` at least 0.05 m/s | `food jnt`, `food rig`, `spd jnt` |
| M4 | **the detritus loop closes**: `det out` within 0.5 to 2 of `det exuded` per window over the last 6,000 s in every seed that passes M0, and `detritus J` at the end below round 29's same seed (round 29 stood at 34 to 211 kJ; the vertex screens at 5 to 7 kJ at 10,000 s) | `det out`, `det exuded`, `detritus J` |
| M5 | **the vertex budget holds**: `vtx` below the cap at every sample in every arm, `verticesMerged` 0 | `vtx`, the statistics |
| M6 | **the identities close**: `audit` 0.0000% and `mat resid` 0 at every sample; `mat short` 0; neither impulse limiter binds at 0.01 | `audit`, `mat resid`, `mat short`, the manifests |
| M7 | **matter is the ceiling and the vertex world reaches it sooner**: `mat blk` per birth over the last 6,000 s at least three times round 29's same seed, and `mat locked` at the end above it (the seed-2 screen refused a hundred conceptions per birth where the cell world refused ten, and locked 5,735 of 6,000 units by 10,000 s) | `mat blk`, `births`, `mat locked` |

The matter field's vertex count falls through a run as bodies take whole quanta and return
them where they die, from 48,000 seeded to about 1,300 at 10,000 s in both screens; `vtx`
is read for clustering and not for loss, and the identity column is what says whether
matter was lost.

## The two-sided readings

- **M0 fails:** the vertex world stalls on local matter, or the stomachs never form. The
  kernel and the quantum are the levers, and they are world rules, the owner's. The cell
  field stays behind its switch, so the round can be re-run on cells at the new price to
  separate the two rulings, and that is the first follow-up arm.
- **M0 holds, M1 fails:** a tenfold cheaper input is still not carried, and the price was
  not the lever for the senses; the squash constants are next, the owner's.
- **M0 holds, M2 fails:** the muscles still do not pay in a world where sitting still
  depletes the water and the stroke costs a quarter. The prize side is then the question
  and not the cost side: read `spd jnt` in the early samples for whether there was a
  swimmer to select on, and `dep jnt` against `dep rig`.
- **M2 holds, M3 fails:** swimmers survive without eating better; `dep jnt` against
  `dep rig` first, then the active-versus-clamped assay on saved members of the jointed
  clade (HANDOFF item 7).
- **M1 to M3 hold:** movement pays. The owner words the movement clause of the goal rule
  and the round that scores it, and predation on contact is next (D081's order).
- **M4 fails with M0 holding:** the exudate is standing where the stomachs cannot reach it;
  the sideways mixing and the satiation cap are the levers, and the first is the setting
  flagged above.
- **M6 fails:** the round is censored and the fault is fixed before anything is read; a
  vertex world that creates matter has already happened twice (0074).

## What the round does not ask

Whether a brain evolves, and whether a bud (a neuron and the muscle it moves) is
selected rather than afforded; both need a jointed guild to exist first. Nor whether a
sense is used rather than carried: round 29's three sensing worlds each settled on one
channel and the round could not say which selected it. That assay is the owner's to word.
