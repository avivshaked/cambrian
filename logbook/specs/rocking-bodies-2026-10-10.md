# Why stomachs rock and some bodies spin like planets

*2026-10-10. The owner saw single stomachs on the bed rocking back and forth in the round 57 film's look test,
and in other recordings bodies spinning steadily. They agreed it comes from the simulation, not the theatre,
and asked why. An investigator (Opus) read the farm's recorded poses and the code, and the coordinator checked
the two load-bearing code claims. Nothing was changed or run. The poses are the six seconds of `r57-s2` from
47,994 to 48,000 s recorded for the film at 30 frames a second, which match the run's own `poses.bin` at
48,000 s bit for bit in all 8,436 bodies. The run's own poses, every 10 s, show the motions last at least
1,000 s.*

## What the poses show (measured)

- **The capsules move.** Among single-part absorptive bodies, capsules with a buoyancy offset turn at a median
  of 1.69 rad/s (rms). 270 of 437 rock, reversing four or more times in six seconds, and 35 turn more than
  once. Spheres never move (33 of 33 below 0.05 rad/s), boxes mostly not (5 of 7 still), and leaves almost
  never (median 0.000 rad/s over about 7,000 bodies).
- **They are pendulums.** A body's measured period equals a free pendulum's period at its measured swing,
  computed from its genome (median ratio 0.986). Periods run from 0.65 to 3.4 s, median 1.57 s. The median
  largest tilt of the lever from upright is 24°, and 13% pass 90°.
- **Nothing slows them.** Over six seconds the peak spin rate holds at 0.999 of its start (median). Body
  171118 wobbles 12° to 21° from upright through every 10 s sample from 47,000 to 48,000 s.
- **The half-second step is not the cause.** Reversals fall evenly across the 0.5 s metabolic step, and the
  spin rate shows no kick at its boundary. The step back every 0.5 s seen in the pixels belongs to the largest
  stomachs, whose half-period happens to be 0.43 to 0.5 s.
- **Newborns are put down at rest in development's attitude.** In 992 of 1,024 moving lever bodies the
  thinnest axis is x, so the lever starts out horizontal, about 89° from upright, and the body swings at once.
- **The spinners start balanced at the top.** A capsule whose thinnest axis is y, with a negative offset, is
  born with its lever straight down, the balance point at the top. Fourteen of 19 such capsules go over the
  top, five to ten turns in six seconds, and keep turning at 5 to 7 rad/s at the top. Their motion fits the
  pendulum's energy relation to within 0.5%. Body 168586's sequence of 10 s attitudes repeats exactly about
  880 s later.

## What the code does (read, and checked)

- **The water resists only a face moving into it.** `src/Evosim.Dynamics/Fluid.cs` (lines 78 to 91) takes each
  panel's velocity, keeps only its part along the panel's normal, and skips trailing panels. There is no
  rotational drag or angular damping anywhere. A sphere's panels have normals through its centre, so it can
  turn in place without drag. A capsule turning about its long axis feels none either, and its tumbling drag
  grows with its straight section's length. A box's panels sit off its face centres, so boxes do feel drag
  when they turn, which fits their stillness.
- **Neither the water nor contact turns a one-part body.** The current is sampled at the part's centre, and
  contact pushes and friction act at its bounding sphere's centre.
- **D111's buoyancy torque is the only torque on a one-part body** (`Fluid.cs`, lines 224 to 244). The part's
  displaced weight acts at a lever along its thinnest axis, so a capsule with an offset is a pendulum, and a
  sphere, which has no thinnest axis, is not.
- **Birth zeroes the spin** in the attitude development gave the body. **A growth resize keeps the spin rate,
  not the angular momentum**, so the swing gains energy as the body grows.
- **The theatre adds nothing.** It shows the nearest recorded frame without interpolation, and its shader
  moves only the caustics.

## The causes

The rocking is measured. D111 made a capsule with an offset into a pendulum, newborns start with the lever
lying sideways, and nothing in the water brakes turning, so the swing never dies. Why most swings are now
nearer 24° than 89° is inferred: the longer capsules' tumbling drag, which grows with the square of the spin
rate.

The spinning motion is measured, and its energy is partly inferred. The spinners start at the top's balance
point and fall off it. They turn faster at the top than a fall from rest can give, and the investigator's
inference is that the growth resize adds the difference by keeping the spin rate while the swing's natural
rate falls. The six-second window holds no growth step, so this is not tested.

D111 is a rule chosen when nothing could turn a capsule. It is the kind CLAUDE.md asks to re-question when a
later change makes it bite: a world where rotation was never braked was harmless until something turned bodies.

## What would fix it

Both fixes change the world's rules, so they are the owner's.

- A drag on turning: drag on the tangential part of a panel's velocity, or a viscous torque against the spin.
  This fixes both motions, and real water does resist a turning body through its skin.
- Or newborns put down with the lever upright, and angular momentum kept across a growth resize in place of
  the spin rate. This removes the starts and the energy gain but leaves any later swing undamped.

The theatre needs nothing. It shows the farm's poses faithfully, and smoothing them would misrepresent the
recording.
