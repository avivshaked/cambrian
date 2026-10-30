# Proposal: the leaves' smell drifts with the water, and a body can feel which way the water runs

*Draft for the owner, 2026-10-30. It is phase 2 of D264 for the leaves: the one smell engine that leaves, animals,
wounds and kin will later share. It turns the outline the owner agreed on 2026-10-30 into a design. World rules are the
owner's; dials within a ruled rule are agent work, and each rule says which it is. Absorbed into DECISIONS.md on ruling,
then deleted.*

## The proposal in one paragraph

Today a body smells a leaf only within 6 m of it. Groves of leaves lie 20 to 30 m apart, so a grazer between them smells
nothing and has no reason to go anywhere. I propose one smell at two scales and one new sense. Near a leaf, today's cone
stays as it is, with its full three-dimensional direction, for the last few metres. Far from it, every leaf feeds a
coarse grid that the current carries and stirs and that slowly loses what it holds. A grove then leaves a plume tens of
metres downstream, bent by the eddies and turned aside by rock. A new current sense tells each part which way the water
runs over the ground. A body that smells the plume can swim up it, as crabs and snails do, and the near cone finishes
the approach. The far grid's cost depends on its cells alone: about 1 ms a step at 5 m by my estimate, against 2.8 ms
measured for today's near fill. The realistic form gives the far smell's strength only, and the overpowered form (D262)
adds its gradient. Every switch is off by default, so every recorded world replays. A screen of five worlds reads whether
either form sends grazers between oases.

## The smell stops where the open water begins

The world is a 300 m square whose opposite edges are joined, 25 m deep (D204). Matter arrives at fed shoals, the oases,
and leaves grow round each in a grove. A grazer is an animal with a mouth that eats leaves. Each leaf gives off a smell
in proportion to its lit area (D167). The smell is a cone that falls to zero at 6 m, on a grid of 2 m cells. It is
refilled at every metabolic step, the half second at which the world's economy is settled. In the patch tank of the
nursery, where bodies are bred in small tanks, three quarters of the water smelled of nothing (the nose lane's notes).

Two fixes have been tried. D251 laid a level disc of smell over each blooming island. In `r63c`, the bloom screen, it
added one or two crossings from grove to grove a world (D259). D265 and D267 lengthened the cone to 15 m in worlds where
only a nose smells (D235), at about 1,770 cells a leaf against 113 at 6 m. Neither knows about the water.

A real smell is carried by the current and stirred by eddies, while molecular diffusion moves it a few millimetres an
hour (C29). Animals that track a cue mostly turn upstream when they meet it and steer back at its edges (C30). A blue
crab needs its head sensors to keep going upstream and its leg sensors to follow the plume near the source (C41).

The world already has a lateral line, the flow sense, which reads the water moving past each part. A drifting body reads
nearly zero on it, because drag brings it to the water's speed within about a second (`src/Evosim.Core/CLAUDE.md`). So
the flow sense cannot say which way the water runs. Swimming upstream needs a reference to the ground. A crawler gets it
by holding the bed (C2, C30). A fish gets it by seeing the bed slide past, which is general knowledge and not from our
record.

## The rules, and whose each is

1. **Every leaf feeds a carried smell** (a world rule). A new grid, built as the matter grid is, holds the far smell. At
   each metabolic step every living leaf adds its lit area times the step to the cell holding it. The grid then mixes,
   drifts with the current and loses a fixed share to decay. The bed and the coral are dead cells, so a plume flows
   round them as far as 5 m cells resolve them.
2. **The near smell is today's cone** (a world rule, unchanged): 6 m on 2 m cells, with strength and 3D direction. In a
   world with the far smell a nose reads this cone, and D265's 15 m holds only in nose worlds without it.
3. **Each smelling part reads the far smell's strength** (a world rule; the half-scale is a dial), squashed as
   s / (s + k). In a nose world only noses read it (D235).
4. **The overpowered form adds the far gradient** (a world rule, for the screen). It is three numbers in the part's
   frame, unit length times the strength, made as the near direction is.
5. **Every part reads the current over the ground** (a world rule). It is the water's velocity at the part in the
   part's own frame, mirrored for a mirrored part as the flow sense is (D234). Its length is speed / (speed + 0.01 m/s).
   It reads the same in midwater as on the bed, which no point of this 25 m world is far from.
6. **The two smells are two inputs and are never added** (agent work within D020 and D146). D251's disc is refused in a
   world with the carried smell.
7. **The new readings are taken on the host once a metabolic step** (agent work). The host is the main processor, and
   it hands them to the graphics card, the card for short, as it hands a nose's readings today. A reading is then at
   most 0.5 s old.

## The far smell's values make a plume tens of metres long

The values follow from one aim. In the worlds' current of 0.02 m/s on average, a plume should reach tens of metres
downstream and stay short upstream. The table says which values are physical and which are dials.

| Quantity | Value | Kind | Why |
|---|---|---|---|
| Cell | 5 m | dial | The matter grid's own lattice. The grid refuses a cell that does not divide the 300 m side and the 25 m depth, so 4 m and 2 m cannot be built without changing it; 2.5 m can |
| Mixing | 0.02 m²/s | physical | The world's own eddy mixing of matter and snow, since it is one water. The grid's transport smears about U·Δx/2 = 0.05 m²/s more at 5 m (calc) |
| Lifetime | 3,000 s | dial | Real cues turn over in days (C37). A mean field has no detection threshold, so decay stands in for dilution below what a nose can use. Set long to overshoot |
| Cadence | every metabolic step, 0.5 s | dial | The matter grid's clock |
| Stability | mixing number 0.0004, limit 1/6; flow number about 0.006 at three times the mean speed, limit 0.5; decay a factor, stable at any step | calc | The grid's own checks refuse a step past either limit |

With these values the mean field falls by a factor of e every 63 m downstream and every 3 m upstream (calc, in one
dimension). At a 1,500 s lifetime it would be 33 m. The box's eddies are 30 to 150 m across, so a real plume bends. The
build's probe measures one grove's plume in the world's current. It also checks that a uniform smell stays uniform under
the transport, and that the stock equals what was emitted less what decayed.

## The far grid reuses the matter grid's code and costs about a millisecond

The matter grid's code already has cubic cells, dead cells under the bed and in coral, and joined seams. It mixes
conservatively and transports from the current's potential. The build adds a decay pass and turns sinking off. Today's
leaf field is refilled from nothing at every step, but the far grid is state, the history of the leaves' smell. A
checkpoint must carry it, about 20,000 numbers at 5 m.

In the table, the far grid's cost grows with its cells and not with the leaves or the range.

| Piece | Cells | Host, ms a metabolic step | Card, ms a step | Basis |
|---|---|---|---|---|
| Today's near fill, 6 m cone on 2 m cells | 292,500 | 2.8 at about 3,500 bodies | none | measured, `logbook/specs/r59-leaf-timing-plan.md` |
| D267's 15 m cone on 2 m cells | 292,500 | unmeasured; 16 M additions a fill at 9,000 leaves | none | D267's count |
| Far grid at 5 m | 18,000 to 21,600 | about 1 | about 0.1 | estimate |
| Far grid at 4 m (refused by the grid) | about 39,000 | about 2 | about 0.2 | estimate |
| Far grid at 2.5 m | 144,000 | about 8 | about 0.8 | estimate |
| Far grid at 2 m (refused by the grid) | 292,500 | about 15 | about 1.6 | estimate |
| New readings, every smelling part | none | under 0.5 | up to 7 floats a part each block, about 0.5 MB | estimate |

The estimates scale the snow's transport by cell count. The snow is a 1 m grid of 2.25 million cells. Moving it took
237 ms per simulated second on the host and 25 ms on the card (`logbook/specs/box-transport-speed.md`). A metabolic step
of `r64-e-6202`, one of phase 1's worlds, took 222 ms of wall time. The far grid at 5 m adds about half a per cent
(inference).

The far grid steps on the host. D235 asked that a new field fill on the card. At 5 m a port could save under 1 ms a
step, and round 59's timing kept the near fill on the host at 4% of the wall. The readings go up with each block of
physics steps, so the card's programs gain only copies, as they already do for noses.

## The near and far smells split the work by scale

The far grid cannot find a leaf, since its gradient is constant across a 5 m cell. The near cone cannot find a grove
from 30 m away. So a body follows the plume to the grove's edge, and the cone takes over for the last metres. The two
readings are separate inputs and are never summed, so neither counts a leaf twice. D251's disc was added into the leaf
channel, and a body could not tell an island's glow from a leaf beside it.

Two nostrils could differ only in the near cone, which falls by a sixth of its peak per metre. Two sensors find a
plume's centre only when farther apart than its eddies (C40). Nostrils stay with the nose lane.

## The current sense reads the water against the ground

The drag already computes the water's velocity at every part (`Creature.Water`), so the sense costs a rotation. At
0.02 m/s it reads 0.67 of full length. Every part carries it because D146 makes every sense of every part an input, and
the lateral line stays as it is. The first dial-back fades the sense with height above the bed, standing for sight. The
realistic end reads it only while a part touches the bed or coral, as a crawler feels the current (C2, C30).

## Today's grazers carry the wiring for the easy parts and not for casting

I counted the central brains of the last 2,000 grazers born in `r64-e-6202`, from 20,076 to 23,999 s, of 5,874 grazer
births in the run. A grazer is a birth row with attack and intake both above 0, its genome found by id in the run's
genome file (`scratch/smell-proposal/`). In the table, look at the hidden units and the links that feed back.

| Per genome | Median | Mean | 90th percentile | Largest |
|---|---|---|---|---|
| Outputs (power and bend per joint) | 2 | 2.04 | 2 | 24 |
| Hidden units | 1 | 1.14 | 2 | 6 |
| Links | 61 | 62.7 | 68 | 723 |
| Links from senses | 60 | 61.5 | 65 | 711 |
| Sense inputs wired | 30 | 27.9 | 32 | 122 |
| Links between hidden units | 0 | 0.08 | 0 | 2 |
| Links from a unit to itself | 0 | 0.10 | 0 | 3 |

A quarter carry no hidden unit, 71% carry one to three, and 4.3% four or more. The median grazer is one joint driven
almost linearly by 30 senses.

By my reasoning, the smallest circuit for a one-joint body that steers by its bend has three parts.

- **Swimming upstream when smelled** needs two links. The current's sideways reading drives the bend, so the body faces
  upstream, and the far strength drives the power. A founder, a body of the first generation, has every sense linked to
  every output (D147), so only these weights must evolve. Gating the turn itself on the smell needs a product, at least two hidden units.
- **Finishing in 3D** needs two more founder links: the near direction's sideways reading on the bend, weighted above
  the current's, and the near strength on the power.
- **Casting when lost** needs a memory that the smell was there, one unit linked to itself. It also needs a slow swing
  left and right while the memory outlasts the smell, two units in a loop. That is beyond nearly every grazer alive.

In the overpowered form the far gradient points back up the plume wherever the smell is above zero, so one link steers
and no casting is needed. Each part gains four inputs in the realistic form and seven in the overpowered one, and a
founder wires each to every output.

## Every switch is off by default

| Setting | Off | In the screen | Kind |
|---|---|---|---|
| `LeafPlume` | None | Strength in the realistic arm, Gradient in the overpowered one | world rule |
| `LeafPlumeCellMetres` | 5 | 5 | dial |
| `LeafPlumeLifetimeSeconds` | 3,000 | 3,000 | dial |
| `LeafPlumeHalfScale` | 1 | set by the bench below | dial |
| `CurrentSense` | off | on in both arms | world rule |
| `CurrentSenseHalfSpeedMetresPerSecond` | 0.01 | 0.01 | dial |

The far grid mixes at the world's matter mixing and gains no knob of its own. Every new setting refuses every older
`config.json`, as every tunable does, so the nursery's configs and the recorded fixtures are brought forward. Genomes
store sensor channels by name, so the three new channels need no genome format bump (read from the code; the builder
confirms it). The checkpoint gains the far grid's stock, written only with the switch on. With every switch off no grid
is built and no input added.

## A screen of five worlds reads whether grazers travel

The screen runs in the world phase 1 settles on, named when `r64`'s read chooses it. Its recorded seed-6202 world is a
control and is not run again. Five worlds run in one card group for 24,000 s: R and O on seed 6202, and C, R and O on
seed 6203. R is the realistic form, O the overpowered one, and C the world unchanged. Before the launch, the build with
every switch off must replay phase 1's world for 600 s row for row. A 1,200 s founding of R's world then sets the far
half-scale. It is the median far smell 20 to 40 m downstream of groves, committed with the pre-registration.

The marks are read over the last window, 21,000 to 24,000 s. Grazers are as phase 1 defines them, and no window is
judged on fewer than 30 (D244).

1. **Grazers eat more leaves**, their share of bite income from living leaves (`income-split.py`) at least C's plus 0.05
   in R and in O, on both seeds.
2. **Grazers travel between oases**, with crossings between groves per grazer life (`g2-read.py`) at least 1.5 times
   C's in R and twice C's in O, on both seeds. The share of grazers more than 20 m from every grove is reported.
3. **Grazers find a patch sooner**, the lag at which they gather at the island now blooming at most 0.75 of C's in R
   and half of C's in O. The median length of a crossing is reported.
4. **The pace holds**, with no world under 0.5 times real time. A world that falls under it is stopped at once. On seed
   6203, R and O step within 10% of C's pace.

My predictions, all guesses:

- O passes marks 1 and 2 on both seeds and halves the lag. Its leaves fall faster than C's and may crash (moderate).
- R moves grazers less, with crossings 1.2 to 1.6 times C's, and passes mark 2 on one seed (low). Its likeliest failure
  is grazers that turn up a plume and lose it where an eddy bends it.
- R and O each lose 2 to 5% of pace. Five worlds take about three hours, as `r64`'s five took 178 minutes.

If O passes and R fails, O is dialled back in steps (noise on the gradient, then a coarser one), and the nursery trains
casting. If both pass, R carries forward and O retires. If both fail, the nursery comes before another world screen.

## The later smells ride the same grid

The grid's passes are linear, so any smell fed from points rides as another stock on the same cells. Each stock costs
one more set of passes, however many sources feed it. D264's animal smell is a stock fed by animal tissue, and the alarm
one fed by wounds and full guts. The kin signature of `proposals/propose-recognition.md` is five sums, five stocks.

There is one limit. A carried field cannot subtract a body's own smell, because what it gave off a minute ago has
drifted with the water it swam through. Self-subtraction stays in a near cone refilled each step, as the recognition
draft designs it. Five kin sums would cost about 5 ms a step on the host (inference).

## Rays, a longer cone and the disc are rejected

- **Casting rays from each body to each source**, the tool sight uses. Smell has no line of sight. It goes round rock
  with the water and lingers after its source has moved, and rays cost bodies times sources.
- **A longer cone** (D265, D267), whose cost grows as the cube of its reach, and which smells as strong upstream as down.
- **D251's island disc**, a level glow with no current added into the near channel, worth one or two crossings.
- **Puffs carried as particles**, which would give the bursts real plumes have (C30), at a cost that grows with the
  leaves and the lifetime.
- **Molecular diffusion as the mixing**, about 1e-9 m²/s (C29).
- **Today's flow sense as the current sense**, since a drifting body reads nothing on it.

## The owner rules on six questions

1. **Leaves feed a carried smell**, read as strength in the realistic form, on the values above. I recommend yes,
   since it is the one form whose reach follows the water. The log gains a world rule that takes up the plume D167 left
   for later and replaces D251's disc where it runs. It costs about 1 ms a step. The risk is plumes so long that every
   grove smells of every other, which the lifetime dials back.
2. **The current sense reads the water over the ground everywhere.** I recommend yes, with a fade by height above the
   bed as the first dial-back. A sense that reads only on the bed is real for crawlers and probably too hard for
   swimmers in one screen. Without either, the realistic form gives no direction.
3. **A nose in a world with the carried smell reads the 6 m cone**, amending D265 and D267 there. I recommend yes,
   since a leaf then costs 113 cells a fill and not 1,770. The nose lane's 15 m test in the nursery is unchanged.
4. **The overpowered form gives the far gradient** (D262), dialled back after the screen. I recommend yes, as the
   overshoot D262 asks for.
5. **The screen as pre-registered**, about three hours on the card. I recommend yes, with the pre-registration
   committed before the launch.
6. **This grid carries phase 2's animal smell, alarm and kin** as further stocks, with self-subtraction in a near cone.
   I recommend agreeing the direction now and building each with its own smell.

## The sources

The papers come from the project's leaf-smell literature round, `logbook/specs/leaf-scent-lit/`, whose `pass2.md` gives
each finding's page. The code was read on 2026-10-30. Everything above marked as an estimate, a guess or an inference is
mine.

| Key | Source | What it supports |
|---|---|---|
| C2 | Frings & Frings 1965, *Biol. Bull.* 128:211 | A sea hare's current sensors and cue sensors are different organs; it follows a current once the cue is in it |
| C29 | Moore & Crimaldi 2004, *J. Mar. Syst.* 49:55 | Molecular diffusion is about 1e-9 m²/s; stirring draws a cue into filaments |
| C30 | Zimmer-Faust et al. 1995, *Biol. Bull.* 188:111 | Crabs turn upstream within 1.5 s of meeting a plume and steer back at its edges; plumes come as bursts |
| C37 | Simó et al. 2000, *MEPS* 203:1 | Algal cues turn over in 0.3 to 5.4 days in open seawater |
| C40 | Webster et al. 2001, *Limnol. Oceanogr.* 46:1048 | Two sensors find a plume's centre only when farther apart than its eddies |
| C41 | Keller et al. 2003, *MEPS* 261:217 | Crabs need head sensors to go upstream and leg sensors to follow the plume near its source |
| Code | `ScentField`, `GridField`, `CurrentField.Ocean`, `CreatureSenses`, `World` | The cone, the grid's limits and passes, the ocean's eddies, the flow sense's frame, the fill |
| Record | D167, D204, D235, D251, D259, D262, D264, D265, D267; `logbook/specs/r63c-read/`, `r59-leaf-timing-plan.md`, `box-transport-speed.md` | The world, the rulings, the disc's result, the measured costs |
| Count | `scratch/smell-proposal/grazer_brains.py` and `grazer-brains.txt` | The grazers' brains |

## The coordinator's review, 2026-10-30

I checked the arithmetic and agree with the design and with each recommendation. The plume's e-folding lengths follow from
the current times the lifetime downstream and from the mixing over the current upstream. The mixing and flow numbers and
the 5 m grid's 18,000 cells are right. So is the scaling from the snow's measured transport. Five points for the owner:

- **The grazers' brains are the finding to watch.** The median grazer has one hidden unit and almost no feedback links. So
  the realistic arm can swim upstream on founder weights, but it cannot cast, and a body that loses the plume once loses
  it for good. If R fails and O passes, the next lever is the brain's growth (the add-unit and add-link rates) or the
  nursery training casting, not a stronger smell.
- **A mean field is smoother than a real plume.** Real plumes arrive in bursts with gaps between them (C30), and this grid
  gives a steady value that falls off smoothly. The realistic arm is therefore easier than the sea, which suits a first
  screen. Bursts are a later dial-back, by noise on the reading.
- **It ties to r64's finding.** In r64, newborns starved within minutes of birth, most of them away from the leaves, with
  D's young spread over 30 m. A plume reaching tens of metres downstream of each grove is what such a newborn needs to
  find its first meal. Mark 3 should report newborns' time to first bite beside the patch lag.
- **The field runs on the host, not the card.** D235 asked that new fields fill on the card. At about 1 ms a step the
  host is cheaper than the port, and I accept the deviation. The screen's pace mark is the check.
- **The world to run it in** is `r64s2-d-6202`'s, if step 2 shows its grazers and leaves balancing. Otherwise it is
  whichever world the next phase-1 screen settles on. The screen waits for that read.
