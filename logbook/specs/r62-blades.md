# Round 62's kelp blades: three ways to stop the self-scale compounding, and a drag limiter that holds short blades but not long ones (2026-10-18)

D196 gave round 62 two rules for the kelp blades. A module's self-scale no longer compounds, and the drag limiter
engages at every step. This note is for the owner, before anything is built. It does two things.

First, "does not compound" has three readings that fit its words, and they differ a great deal. Most living
bodies grow by a repeating edge, not only the kelp, so the choice reaches most of the world. I recommend the
reading closest to the ruling's words: an edge's scale is applied once on any path from the root.

Second, I tested whether the existing limiter holds a rigid 40 m kelp blade. It does not. It makes such a blade
run away from a tenth of the spin it took before. Blades grown under the new scale rule are only a few metres
long. On those the limiter holds seven of the eight bodies tested, up to 100 radians a second at both steps. The
speed agent wrote this for the coordinator to show the owner.

The note leans on a few terms. A genome is a graph, and each of its nodes describes a part. Each edge says
"attach a copy of that node here", with a scale for the copy along each of its three axes. An edge that points
back to its own node is a self-edge. It repeats up to the node's recursive limit, and each repeat is a module. A
blade is the kelp family's chain of flat parts made this way. The step is the physics clock: the rounds run at
0.01 s, and the grazers' screens may run at 0.02 s. A body diverges when its numbers run away to infinity in a
few steps, and the run counts that as a death.

## Today every copy multiplies the scale again

Development walks the genome graph from the root and builds one part per copy. Each copy's size is the scale it
was reached with times its edge's scale (`Developer.Expand`). A self-edge with scale s therefore makes copies at
S, S·s, S·s², S·s³ and on. The kelp's self-edge multiplies the length by about 1.96 and the width by 0.74 per
copy, and the thickness stops at the 0.02 m floor. So an eight-module blade's last part was 41.6 m long in
r61-p6103x2, and body 134411's was 102 m.

Growth in life runs through the same code. A module added to a living body re-develops the body from its genome
with one more copy counted. One rule in development therefore covers birth, growth and life.

The census below shows how far a change reaches. It counts the 7,345 living bodies of r61-p6103x2 at 55,140 s.
Nearly every body with a repeating self-edge scales some axis away from 1. Most do as the kelp does, growing one
axis and shrinking another.

| living bodies at 55,140 s | count |
|---|---|
| all | 7,345 |
| with a self-edge that repeats (recursive limit 2 or more) | 4,077 |
| whose self-edge grows some axis by 10% or more a copy | 3,506 |
| whose self-edge shrinks some axis by 10% or more a copy | 3,974 |
| whose self-edge is within 10% of 1 on every axis | 88 |
| with a cycle through two or more nodes | 2,743 |

The largest per-axis self-scale has a median of 1.84 and reaches 3.74.

## Three readings of "does not compound"

Each reading below is described by what it does to a self-edge of scale s on a node of size S.

### Reading one, which I recommend: an edge's scale applies once on any path

An edge's scale applies the first time a path from the root follows it. Each later time the same path follows it,
the copy keeps the scale it arrived with. A self-edge then makes S, S·s, S·s, S·s and on. A cycle through several
nodes behaves the same way: each of its edges applies once, and after one lap the sizes repeat.

This is what the ruling's sentence says. A module takes its scale from the edge once, and from no copy before it.
It also leaves no other route for a size to compound. I built it as a probe to test the limiter below. The probe
keeps a list of the edges on the current path and checks it before multiplying. It was about ten lines, and it is
not committed.

### Reading two: the same for self-edges only

The rule applies only to an edge that points back to its own node. Cycles through two or more nodes keep
compounding. For the kelp this gives the same blade as reading one. It differs for the 2,743 bodies with a longer
cycle. Those cycles would stay a way to grow without bound, so I think the next problem family would arrive
through them (inference).

### Reading three: no compounding upward

Each axis keeps compounding where the edge shrinks it (s under 1), and stops after one copy where the edge grows
it. A tapering tail still tapers, and a blade still stops growing in length. The kelp blade keeps its length at
S·1.96 per part while its width and thickness keep shrinking down to the floor. This reading spares the 3,974
bodies that taper. The case for it is that a shape that only shrinks never loaded the engine. It goes beyond the
ruling's words, though.

### Readings one and two end tapering

Under readings one and two, every body that tapers today stops tapering after its first copy. A tail of five
shrinking segments becomes a larger first segment followed by four equal ones. That is the largest change to the
world's animals in the ruling, and it is the owner's call whether that is intended.

### What any reading needs in the build

The rule itself goes in development, behind a development tunable, so that round 61's configurations still name
the old rule. Old configurations then stop loading on the new build until they carry the new field, as with any
new tunable, and the recorded test fixtures need re-recording.

A resumed world re-develops every body from its genome when it loads. A cousin resumed from a round 61 checkpoint
under the new rule therefore finds every kelp blade shortened at its first step. I have not checked what the
solver makes of a jointed body whose parts change size under a stored pose. That needs a test before D198's
grazer cousins rely on it.

## The limiter is switched on by a tunable beside the drive limiter's

The water's drag limiter caps each part's opposing drag at what would stop it in one step. For a part moving, the
cap on the force is its mass times its speed over the step. For a part turning, the cap on the torque is its
smallest moment of inertia times its spin over the step. The limiter runs only when the step is longer than
0.0100001 s (`SolverConfig.DragLimiterEngages`), so the 0.01 s rounds have never used it.

The drive limiter had the same gate until a tunable opened it at every step. The same pattern fits here, as a
tunable `DragLimitAtEveryStep` in the run's physics settings. The gate becomes the step test or the tunable. The
card reads the same gate, so it follows with no change of its own.

Hard-wiring the gate open would also do it. Every earlier 0.01 s run would then replay differently on the new
build, and the tunable avoids that at the cost of one field.

## What the limiter does to a rigid blade

I took eight kelp bodies from r61-p6103x2: seven that diverged, plus 134411 grown with its eighth module. I set
each one alone in open water on the CPU solver at the run's fluid settings, with contacts off. I gave it a spin
about one axis and stepped it for 3 s, trying spins from 0.1 to 100 radians a second. For each case I recorded
the smallest spin from which it ran away (past a million radians a second).

The harness cannot set the limiter's gate directly. A step of 0.0100002 s passes it, so that variant is the 0.01 s
step, 0.002% longer, with the limiter on. At 0.02 s the limiter is on already. Each body was grown twice: with
today's compounding, and with the reading-one probe.

The table gives, for each body, the smallest starting spin that ran away, over its three axes, in radians a
second. Read across a row for the effect of the limiter. Then compare the two halves for the effect of the scale
rule. A dash means no axis ran away up to 100.

| body | longest part, compounding | 0.01 s | 0.01 s, limiter | 0.02 s, limiter | longest part, reading one | 0.01 s | 0.01 s, limiter | 0.02 s, limiter |
|---|---|---|---|---|---|---|---|---|
| 105464 | 1.4 m | 20 | – | 100 | 0.73 m | 50 | – | – |
| 118901 | 1.8 m | 20 | 10 | 5 | 0.46 m | 50 | – | – |
| 108541 | 4.2 m | 10 | 5 | 2 | 0.77 m | 20 | – | – |
| 112066 | 10 m | 5 | 0.5 | 0.5 | 0.67 m | 20 | – | – |
| 102219 | 21 m | 2 | 0.2 | 0.1 | 0.72 m | 20 | – | – |
| 108599 | 42 m | 1 | 0.1 | 0.1 | 0.73 m | 10 | – | – |
| 120214 | 50 m | 0.5 | 0.1 | 0.1 | 0.59 m | 10 | – | – |
| 134411, 8 modules | 102 m | 0.5 | 0.1 | 0.1 | 1.01 m | 20 | 20 | 10 |

With today's compounding the limiter makes every body worse but the smallest. The 10-50 m blades ran away from
0.1 radians a second, the bottom of the range tried, so 0.1 is only an upper bound.

A step-by-step trace of the 42 m blade shows why. Without the limiter, a spin of 0.1 about one axis decays
smoothly, to 0.01 in 25 steps. With it, that spin stays at 0.100. The limiter caps its damping at the turning
resistance of one thin part about its own long axis. That is about 0.02 kg·m², while the welded blade turns
against about 12,500.

The cap applies only to the torque along the spin axis. The torque the panels push across that axis passes
untouched, and a second spin grows from it. Over five steps it went 0.015, 3.2, 13, 200 and 97,000 radians a
second.
Started from rest, the same blade with the limiter drifted into a steady turn of 0.026 radians a second, which
nothing damped. This is my reading of the trace and the code. I have not tested a change to the law.

Under reading one the blades are chains of equal parts under a metre long, a few metres end to end. Without the
limiter they ran away only from 10-50 radians a second. With it, seven of the eight held to 100 on every axis at
both steps, and slowed to under one radian a second within 3 s. I don't know what spins a blade meets in the
world, because the run records none before a divergence. I would guess they are well under one radian a second
for a body this size (inference).

The exception is 134411 at its eighth module, with parts 1.01 m long. With the limiter it ran away from 20
radians a second at 0.01 s and from 10 at 0.02 s, about as it did without the limiter. Under reading one a lineage
that adds many copies still builds a long rigid chain. A chain of 16 one-metre parts would be about 16 m long, and
the limiter's law fails on long rigid chains. I have not tested such a chain.

## What this asks of the owner

The three readings, and my recommendation:
- **Reading one** (recommended): an edge's scale applies once on any path. It matches the ruling's words, covers
  cycles as well as self-edges, and ends tapering in about 4,000 living bodies.
- **Reading two**: the same for self-edges only, so cycles through several nodes keep compounding.
- **Reading three**: no compounding upward. Tapering stays, while growth along any axis stops after the first copy.

On the limiter, the ruling can be built as written, as a tunable, and it holds the blades that reading one grows.
It should not be relied on for a long rigid chain. Its law caps a welded part by that part's own turning
resistance, where the body's is the one that matters.

Two further changes would make it safe for any chain, and each would need measuring before round 62.
- **A cap by the whole welded body**, using its mass and moment of inertia rather than each part's. It would cap
  the whole torque as well as its share along the spin.
- **An implicit drag**, which D196 holds as the owner's open question. It would remove the instability where the
  cap only bounds it.

One question is not mine to settle: whether D198's grazer cousins, resumed from round 61's planted worlds, run
under D196.

## What was built after D199

The owner ruled D199 on 2026-10-18: reading three, and a limiter rebuilt around the whole welded body. The speed
agent built both on the branch r62-blades, off r61-merged 5747365b, as commit eee39b05. Three switches carry
them, and each is off by default.

- `DevelopmentLimits.ScaleLengthensOnce` is reading three. Development keeps the
  edges on the path from the root. When it follows an edge that path has taken already, an axis the edge
  lengthens is held where it was, and an axis it shortens keeps shortening to the floor.
- `RunConfig.DragLimitByWeldedBody` is the rebuilt limiter.
- `RunConfig.DragLimitAtEveryStep` opens the limiter's gate at the 0.01 s step.

The farm's settings for the three are EVOSIM_SCALE_LENGTHENS_ONCE, EVOSIM_DRAG_LIMIT_WELDED and
EVOSIM_DRAG_LIMIT_ALWAYS.

With all three off, the build steps round 61's bodies to the bit. The ladder's columns without the new limiter
match the table above line for line, 56 lines of 56. The fast suites pass, apart from failures the base build
shares. Every switch is hashed, though, so a round 61 config must have the three fields written in before this
build will read it. The two config fixtures were re-recorded that way.

### The limiter caps the force by each part and the torque by the welded body

As first built, the limiter capped both by the welded body. The force was held to the body's mass times the
speed of its centre of mass. That held the long chains, but it failed on the short blades that reading three
grows. Started rolling about its long axis at 100 radians a second at the 0.02 s step, every one of them ran
away within seven steps.

A step-by-step trace of body 108599 shows how. The torque cap stopped the roll in one step, as designed. But a
rolling blade's centre barely moves while each of its parts crosses the water at speed. Their drag forces summed
to a large net force. The cap, measured against the centre's speed, let it through, and it threw the blade to
182 m a second in that one step.

The owner chose the alternative the same day (D200). The force is capped part by part, by each part's own mass and
speed, as the old limiter capped it. The torque is summed over the welded body about its centre of mass. It is
capped on each of the body's principal axes, by that axis's moment of inertia and the spin about it. That is now
what `DragLimitByWeldedBody` does, as commit b98823a7.

Its ladder matches the probe's line for line. Under it, at 0.02 s, five of the eight reading-three blades hold to
100 radians a second on every axis. Two run away only from 100, and 134411 runs away from 50. At 0.01 s, seven
hold to 100, and 134411 runs away from 50. The rolling blade is no longer flung. Its root still reaches 112 m a
second for one step, and the roll swings back to 47 radians a second the opposite way. The spin then settles to
under 2 within twelve steps.

### What round 62 still needs built

- The card needs its own copy of the welded limiter. The card's kernel has its own copy of the per-link law. Until it
  carries the welded one too, the card refuses a world with the switch on. The work is to note each part's force
  cap in the kernel's link loop. After the loop, each creature's thread finds its welded bodies, sums their
  torques and inertias, and finds the principal axes with the same Jacobi rotations. Every product in it is
  written so the card cannot fuse it. The test that runs the double-precision kernel on ILGPU's CPU accelerator
  against the solver must then pass with the switch on, at both steps. My estimate is two to three hours of agent work
  and a quarter of an hour of CPU tests. After that comes about an hour alone on the card, for the identity
  gates at two group sizes on the coordinator's go. One risk is pace. Each thread holds about 2 kB more scratch
  in double for a 16-link body, and the card's pace falls as a thread's local memory grows (inference).
- The Dynamics run fixture (`r60fix-s4`) is already unreadable on the base build. It needs recording on the
  round 62 build.
- A cousin resumed from a round 61 checkpoint needs a config with the three fields written in. Under reading
  three, every kelp blade shortens at the first step after the load, because a resumed world develops every body
  again from its genome. What the solver makes of parts that change size under a stored pose is untested.

## Files

The numbers above come from the files in the folder beside this note, [r62-blades/](r62-blades/).

- The compounding half of the table is in limiter-p6103x2.txt.
- The reading-one half is in limiter-once.txt, with every part's size.
- The 42 m blade step by step is in limtrace-on.txt, limtrace-off.txt and limtrace-rest.txt. They start from 0.1
  radians a second with and without the limiter, and from rest with it.
- The census's output is in recursion-census.txt. The script that counted it is
  [scripts/reads/r62-recursion-census.py](../../scripts/reads/r62-recursion-census.py).
- The ladders after D199, on the r62-blades build, are limiter-r62-compounding.txt and limiter-r62-three.txt for
  the limiter as first built. The probe's ladders, with the force capped per part, are limiter-r62p-compounding.txt
  and limiter-r62p-three.txt.
- The rolling blade is body 108599 at 0.02 s, from 100 radians a second about its long axis. Its trace is in
  limtrace-r62-welded.txt as first built, and in limtrace-r62-link.txt under the per-link limiter. Under the limiter
  as now built, it is in limtrace-r62b-welded.txt.

Two working files are not committed. The probe's change to development was a test, and nothing was built from it.
The harness that stepped the bodies was a one-off diagnostic. Both sit in the speed agent's working folder,
scratch/r61-giants, as once-per-edge-probe.diff and giant-find/Program.cs.
