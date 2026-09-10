# 0075 — The vertex world at the new price

**2026-09-08**  ·  round 30, pre-registered before launch; D082 and D083 in one build

Round 29 (logbook/0072) read in the small hours. The senses are carried by a third to a
half of the population in three seeds of five at today's price. No seed keeps a jointed
body. The goal holds in two seeds of five, and the world stands under added mass in every
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
   with a pass per neighbour. The seed-2 screen on the eight-pass build refused 403
   conceptions as short by 2,900 s. The seed-1 screen on this build refused none
   (logbook/0074).
2. **The price.** `EVOSIM_NEURON_COST 0.005`, `EVOSIM_CONNECTION_COST 0.001` (D082's
   tenfold) and `EVOSIM_WORK_COST 0.25`. The work fraction is set from round 28's first
   window, as D082 asked. There the founders' work was 4.9 to 6.8% of all spending in every
   seed while 9 to 17 of 40 bodies were jointed, so a stroke cost a jointed body about 15 to
   30% of what it spent. By 600 s the share was 0.1 to 1.7% with 6 to 10 still jointed. A
   quarter takes the stroke to 4 to 8% of a jointed body's spending, below the
   neuron's new share and above the zero D082 rejected. The cost is the one D082 stated:
   cheaper work cheapens flapping as much as swimming, so M3 reads the guilds' feeding
   against each other.
3. **Sideways mixing.** `EVOSIM_H_MIXING 0.2`. In the cell world this knob was the exchange
   between patches and round 29 ran it at 0. In the vertex world it is the sideways step of
   every vertex's random walk, and at 0 the water would move only up and down. Set equal to
   the vertical diffusivity so the walk is isotropic. This is a world setting and the
   owner's to veto; it is stated here so that the veto has a number to act on. *Ruled the
   same day, D084: it stays.*

## Sequencing

The owner's instruction is to fail fast where it is appropriate. Five seeds run first at
dt 0.02 for 10,000 s on workers 2 to 6 (`r30q-s1` to `r30q-s5`). They are read within the
step and only for what a fast step can say: the world stands, the two identities close, a
stomach line forms, the detritus loop closes, the vertex count stays under the cap. Nothing about
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
read at the body's own position, and exudate lands where the producers stand. A stomach
beside a producer therefore reads hundreds of joules per cubic metre while the layer mean
is tens; the 3,000 s screen read 378 at rigid bodies against a layer mean of 25. The two
guilds' rates are read as a ratio against each other, never as levels against earlier
rounds.

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
them where they die, from 48,000 seeded to about 1,300 at 10,000 s in both screens. `vtx`
is read for clustering. The identity column is what says whether matter was lost.

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

## Launch

*2026-09-08, morning.* The screen: `r30q-s1` to `r30q-s5` on workers 2 to 6 at dt 0.02
for 10,000 s, every manifest reading `coreHash adbb400d…`, `simHash d928e2a5…`,
`configHash ed91d7c5…`, `physicsJobWorkers 0` and `gitDirty false`. Seed 1 records commit
`360a271` and the rest `e11fddc`, one prose commit apart on the same code. Every header
carries V1's tokens, read from the report. The machine holds the five screens and nothing
else. The first rows show the identity at 0 and no short takes in every arm; the screen is
read at its end against the sequencing section, and the round at 0.01 follows if it does
not fail the world.

## The screen

*2026-09-08, midday.* All five ended on budget at 10,000 s in about three hours. Read
against the sequencing section and nothing else.

| arm | alive (round 29's seed at 10,000 s) | inherited stomachs (round 29) | scorer | detritus out over exuded, last 4,000 s | detritus standing, kJ (round 29) |
|---|---|---|---|---|---|
| `r30q-s1` | 1,568 (1,638) | 56 (9) | pass, a clade of 43 | 1.04 | 5.9 (33) |
| `r30q-s2` | 1,494 (1,359) | 78 (33) | pass, a clade of 78 | 1.02 | 6.6 (19) |
| `r30q-s3` | 1,383 (1,636) | 66 (6) | pass, a clade of 64 | 1.06 | 4.7 (34) |
| `r30q-s4` | 1,389 (1,561) | 24 (134) | fail, recruitment only | 1.04 | 8.8 (11) |
| `r30q-s5` | 1,697 (1,747) | 3 (5) | fail | 0.99 | 30 (32) |

The world stands: every arm within 0.85 to 1.10 of round 29's seed. The identities close:
`mat resid` 0, `mat short` 0, `audit` 0.0000% and `diverged` 0 at every sample of every arm,
`verticesMerged` 0, and the vertex count peaked at 48,000 against a cap of 100,000. A
stomach line formed in four seeds of five and the scorer passes three at a third of the
round's length, where round 29's same seeds held 9, 33 and 6 inherited stomachs at that
age. The detritus loop closes in every arm, with the stomachs taking the exudate as fast as
it arrives over the last 4,000 s, and the standing stock is a fifth to a sixth of the cell
world's in four seeds. Seed 5 is the exception: three inherited stomachs and 30 kJ of
exudate standing, as in its cell-world seed. Nothing here about the senses, the joints or
depth is a reading; `sense` ran from 11 to 325 bodies and `jnt inh` peaked at 11 in seed 4,
and both wait for 0.01. The screen does not fail the world. The round launches at 0.01.

## Launch

*2026-09-08, afternoon.* `r30-s1` to `r30-s5` on workers 2 to 6 at dt 0.01 for 30,000 s,
wall 1,200 min, every manifest reading `coreHash adbb400d…`, `simHash d928e2a5…`,
`physicsJobWorkers 0` and `gitDirty false`; seeds 1 and 2 record commit `da520f8` and the
rest `f1c295d`, prose commits apart on the same code. The launcher refused any other
`simHash`. Every header carries V1's tokens, read from the reports. The machine holds the
five arms and nothing else. Read as they land, against the predictions and the scorer.

## Results

*2026-09-08, evening.* All five arms ended on budget at 30,000 s after nine to ten hours
each. V1 to V3 held: every header carried the round's tokens, every manifest reads
`ended budget`, `diverged 0`, `physicsJobWorkers 0` and `gitDirty false`, all five on one
`coreHash` and one `configHash`. The readings come from `scripts/clade-score.ps1` and from
the named columns over the last 6,000 s (`scripts/reads/r30-read.py`); the sense channels come
from each arm's last snapshot.

| arm | M0 alive (round 29's seed); scorer | M1 sense share | M2 `jnt inh` | M4 detritus at end, kJ (round 29) | M7 refusals per birth (round 29) |
|---|---|---|---|---|---|
| `r30-s1` | 1,743 (1,780); **pass**, a clade of 31, minimum 31 | 1.1–1.8%, failed | 0, never above 0 | 7.5 (67), loop 1.12 | 11,300 (21,600), failed |
| `r30-s2` | 1,727 (1,777); **pass**, a clade of 37, minimum 34 | 2.4–3.2%, failed | 0, never above 0 | 7.0 (113), loop 1.05 | 11,800 (11,900), failed |
| `r30-s3` | 1,770 (1,781); **pass**, a clade of 67, minimum 67 | 60–61%, held; the flow sense, 1,181 of 1,770 genomes | 0, peak 2 at 3,500 s | 8.8 (211), loop 1.03 | 13,100 (26,800), failed |
| `r30-s4` | 1,840 (1,783); **pass**, a clade of 24, minimum 11 | 2.6–3.1%, failed | 0, peak 14 at 4,000 s, last seen 10,000 s | 25.8 (34), loop 1.09 | 16,300 (25,500), failed |
| `r30-s5` | 1,776 (1,830); **pass**, a clade of 45, minimum 45 | 1.9–3.4%, failed | 0, peak 1 at 200 s | 11.3 (140), loop 1.05 | 10,900 (50,600), failed |

M5 and M6 held in every arm: the vertex count peaked at 48,000 against a cap of 100,000
with nothing merged, `mat resid` read 0 and `audit` 0.0000% at every sample, `mat short`
0, neither impulse limiter bound, nothing diverged.

**M0 held, and the goal rule is met five of five.** Every seed holds a connected stomach
clade through the last two lifetimes with recruitment in the last window, the first round
in the record to pass in every seed; the reference was round 29's two and round 28's
three. The populations sit within 3% of round 29's, so the vertex world is the same size
of world at the new price. The clades are smaller than the cell world's best (24 to 67
against round 29's 72 and 153) and there are more of them alive at the end, three to ten
per seed, which is what a world with local water should do to a guild that once shared
one pool.

**M1 failed as written, one of five.** The surprise of the round. At a tenth of the price
the senses were carried by fewer bodies, not more. Round 29's three sensing worlds, energy
in seed 2 at 36 to 40%, chemical in seeds 4 and 5 at 55% and 28%, all read 2 to 3% here,
and the one world that took a sense up did so completely: seed 3 carries the flow sense in
two thirds of its genomes. A per-seed comparison across a per-step change is a comparison
of realisations, so the reading is the distribution, three of five against one of five,
and it says the price was not what kept a sense. What keeps one is whatever seed 3's
lineage found, and this round cannot say what that was. The branch is the entry's "M0
holds, M1 fails": the squash constants are next, and they are the owner's.

**M2 failed in every arm, and seed 4 says how.** Its line of fourteen inherited jointed
bodies lasted from 2,000 s to 10,000 s, three lifetimes, longer than any jointed line in
a confirming run before it. From 4,000 s on those bodies fed in water as rich as the rigid
guild's or richer and sat shallower, and they moved at the rigid guild's speed, 0.01 to
0.06 m/s, at every sample. A stroke that produces no displacement collects no prize, and
logbook/0076 measured that in this water there was no prize to collect: at 0.2 m²/s a
sitter's hole refills in five seconds. Round 31 (D085) changes that one number. M3 does not
apply.

**M4 held in every arm.** The stomachs take the exudate as fast as it arrives, and the
standing detritus at the end is a tenth to a twentieth of the cell world's in four seeds.
Seed 4 is the exception in degree and not in kind: its stomach line thinned to 24 and the
stock rose to 26 kJ, against 34 kJ in its cell-world seed. The vertex world's water is
clean because a stomach beside a producer eats the exudate where it lands.

**M7 failed, and the prediction was wrong.** The screen's early window showed the vertex
world refusing a hundred conceptions per birth where the cell world refused ten, and I
predicted three times round 29's rate at the end. At 30,000 s the vertex world refuses
fewer conceptions per birth than the cell world in four seeds and the same in one. The
early ratio was the young world clustering its matter faster; by the end both worlds are
at the same ceiling with 5,700 to 5,850 units locked against 5,450 to 5,570. Matter is
the ceiling in both, and the vertex world does not reach it sooner in any way that lasts.

## Verdict

The vertex world at the new price is the campaign's new base. It passes the goal rule in
every seed, closes both identities, closes the detritus loop, and holds the same population
as the cell world. The price did not keep the senses and did not keep the joints; the
water did not give a mover a prize, and logbook/0076 says why. The pre-registered branches
are "M0 holds, M1 fails" and "M0 holds, M2 fails" together. Their levers, the squash
constants and the prize side, are the owner's, and the owner has ruled on the second:
round 31 stirs the water at a tenth of the rate (D085). The first waits for a round in
which a mover can be paid, since a sense that reads a gradient is worth carrying only in a
world where following it buys something.

Two corrections to my own predictions are on the record above: M7's ratio did not last,
and the deep larder I expected at low mixing is not where the food goes (0076). The round
also settles a question 0074 left open: whether the vertex world's thinner stomach lines
were a property of the world or of the seed. Five of five say the world.

