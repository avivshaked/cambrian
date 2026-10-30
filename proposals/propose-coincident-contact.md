# Proposal: push coincident bodies apart, not both up

*A draft for the owner, written by c0 at the coordinator's request on 2026-10-30, after the shape screen's read
(`logbook/specs/r63s-read/README.md`, "The floaters"). The contact law is part of the world, so the change is a world
rule and the owner's to rule. Nothing is built. Every claim about the code names its file at `217e843f`. What is a
guess is marked as inference.*

## The defect in one paragraph

When a dead body is cut into remains, two of its pieces sometimes come out at the same place, with the same velocity. The contact law then has no direction to push them apart along. Each body works out its own push against every
body it overlaps, from the line between their two centres. When the centres coincide, that line has no length, and the
code falls back to straight up for each body. Both twins are pushed up, neither is pushed away from the other, and they
stay coincident. The push repeats every step, at about five times gravity, until they leave the world 25 m above the
surface. The shape screen lost 18 and 25 such pairs in its two worlds, and `r63c-b`, which has no new shapes, lost 6 and
13. No living body was lost in any of the four. The matter in the twins leaves with them instead of rotting back into
the world.

## Where it is

| Path | Code | Used by |
|---|---|---|
| A body's one contact sphere | `src/Evosim.Dynamics/Contacts.cs` lines 987-989 | worlds with `contactPerPart` off |
| A part's contact sphere | `src/Evosim.Dynamics/Contacts.cs` lines 1212-1214 | worlds with `contactPerPart` on: r63c-b, r63s and r63d |
| The card's two copies of the same | `src/Evosim.Farm.Gpu/gpu-kernel.template` lines 6461-6469 and 6823-6831, and the `Kernels.F.g.cs` and `Kernels.D.g.cs` generated from it | card runs |
| True-shape contact | `src/Evosim.Dynamics/ContactTrueShape.cs` lines 119 and 287 | worlds with `contactShape` true-shape |

The true-shape path is not broken. It orders each pair the same way from both sides (`firstIsThis`), so where the
centres coincide the first body is pushed up and the second down, and they part. The fix below gives the sphere paths
the same property.

Why the push never tires: `ContactLaw.PairPush` caps a step's push at the reduced mass times `MaxSeparationSpeed`
(1 m/s) over the step (0.01 s), plus whatever the pair is closing at. Twins have equal velocities, so they are never
closing and never parting, and every step grants the whole allowance. That is about 50 m/s² on each twin.

## The fix

**Order the pair by organism id, and push along a fixed horizontal axis.** Where two centres lie within 1e-9 m, the
body with the lower id takes the normal `-X` and the one with the higher id `+X`. Ids are unique, so the two bodies always
disagree, and the pushes are equal and opposite. Momentum is kept, as it is for every other pair. The twins part at no
more than the separation cap, 1 m/s, and drag stops them within a second or so (inference). Neither is lifted toward the
surface.

Three axes were weighed:

- **Fixed `X`, by id order (recommended).** No trigonometry and no new arithmetic, only a comparison and a sign, so the
  card's double kernel and the processor agree to the bit without argument. The pieces stay at their depth.
- **Vertical, by id order, as the true-shape path does.** Also exact, and consistent with that path. But it kicks one
  twin up at up to 1 m/s, and a piece cut near the surface could then break it briefly (inference).
- **A horizontal axis that varies with the pair**, such as `±X` or `±Z` by the parity of the lower id. It scatters twins
  in both directions, which looks more natural in a film, and still needs no trigonometry. It buys little, since twins
  are rare, and costs one more branch to keep identical on the card.

**Every pair (recommended).** The defect is in the pair law, and remains are only where it has shown. A living pair would be
launched the same way if two living bodies ever coincided. None was seen in four runs, and how often living bodies
coincide is not measured. Limiting the fix to remains would read a further flag in the innermost loop and leave the
living case on the old law. The branch is only entered at coincidence, so applying it to every pair changes nothing
else.

**Behind a switch, off by default (recommended).** Every world measured here has twins, 6 to 25 pairs a run. A world
with the fix on therefore parts from its own recording at its first twin. That breaks three things the record leans on:
- a resume of any of today's runs, r63d's step 2 among them;
- the switch-off identity sittings that licence reusing a run as an arm, as G5 reused `r63c-b`;
- every recorded test fixture.

A switch keeps today's solver at false, bit for bit. Its new branch is not entered then, the pattern of D229's
`ShapedAddedMass` (`src/Evosim.Dynamics/SolverConfig.cs`). The switch would be a `SolverConfig` flag bound from a
`RunConfig` tunable with a launcher binding. The tunable has the cost every tunable has. A build with it cannot read a
`config.json` written before it until the config is carried forward. Two tools already do that for the shape build:
`scripts/nursery-config-forward-nose.py`, and the hand-forwarded config at
`logbook/specs/shape-build/r62p-t3-6201-config-fwd.json`. And the fixtures that read old configs need re-recording.

**The true-shape path stays as it is.** It has no defect. Moving it to the horizontal axis would change the bits of
every true-shape world for the sake of tidiness.

## What it costs the card port

- **Code.** The same comparison in the two `else` branches of `gpu-kernel.template`. The kernel already holds each
  body's id (`gl.Id`, read beside both branches for the overlap note). The flag goes in through the kernel's config like
  the others. `Kernels.F.g.cs` and `Kernels.D.g.cs` are regenerated, and the regeneration check must show no other diff.
- **Bits.** With the switch off, nothing on the card changes. With it on, the new branch is a sign chosen by an integer
  comparison, so the double kernel matches the processor to the bit, and the float kernel's rounding question does not
  arise (inference, to be shown by the sitting below).
- **Pace.** The branch is entered only at coincidence, a few dozen times in a 24,000 s world. No cost is expected, and a
  timing read on a quiet machine would confirm it (inference).
- **Checks.**
  - A Dynamics test: two coincident spheres on each path part along `X`, with equal and opposite pushes, and neither
    rises.
  - The same pair on the card's kernel.
  - A switch-off sitting against an existing recording, as the shape landing ran.
  - A switch-on sitting at groups 32 and 64.

  On the shape build's precedent, that is one Sonnet build of an hour or two and the Dynamics, Farm and card suites in a
  HOLD window. One card sitting of under an hour follows (inference from G4's landing).

## What it would change in a world

The twins' pieces stay in the water, rot or are eaten as remains are, and their matter stays in the world's books. At
1 to 2 % of remains in the shape screen's worlds, that is a small change to the matter budget (inference). The diverged
count would fall to whatever is left once twins part, which this proposal predicts is none of the 62 pairs seen. Gate 2
and gate 3 of G5 read living bodies, so the screen's verdict would not change.

## A second cause, held

Twins exist because a dead body is sometimes cut into two pieces at one place. Why some lineages grow coincident parts
is not known. One launched genome, a two-part superellipsoid leaf, grows from a self-recursive reflected edge, which is
a guess at the cause. The cut could merge coincident pieces into one, or development could refuse coincident parts. Each
touches D220's remains or development, and so the matter books and what a body is. The contact fix makes twins harmless
without either. I recommend holding this one until a measurement says coincident parts matter for living bodies too.

## The decision

1. Does the contact law change, before G7's round, so that coincident bodies are pushed apart?
2. Which axis: fixed horizontal by id order (recommended), vertical by id order, or a horizontal axis that varies with
   the pair?
3. Every pair (recommended), or remains only?
4. Behind a switch that keeps today's worlds replaying (recommended), or unconditionally?
5. If the switch is built, does G7's round run with it on?
