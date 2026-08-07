# 0014 — The rotations that were not needed

**2026-08-07**  ·  Milestone 2

A question about configuration turned into a question about speed, and the useful part was
finding out that the fork I had offered was posed on a false premise.

## The fork that was not a fork

I had put the speed question as a choice: keep every creature in PhysX for its whole life, or run
the economy in fast arithmetic and sample physics occasionally to feed a work term back.

§5A.9's throughput table says the second is aimed at the wrong target. At 512 creatures PhysX was
**3.2 ms of a 25.9 ms step** — 12% — and its per-creature cost fell to 0.078× of its
single-creature figure, because it parallelises across solver islands and creatures in open water
are separate islands. Sampled physics would have been an elaborate way to avoid the cheapest part
of the simulation.

The expensive part was **our own drag loop, at 88%**.

## Three separate things called "speed"

The other thing worth writing down is that "make it faster" was three questions:

1. **Watch in real time, or not.** Already solved, and not by anything clever:
   `Physics.simulationMode = SimulationMode.Script` and `Physics.Simulate(dt)` in a loop. There is
   no clock. The "real time" column in the throughput table is simulated seconds over wall-clock
   seconds with nothing throttling it. `Stopwatch` appears in this project only to measure.
2. **More creatures per second.** Engineering, below.
3. **More evolutionary time per hour.** Which is `dt ÷ cost_per_step` — and everything below moves
   only the denominator.

The third is where the interesting constraint lives, and it is not addressed here. Recorded so
that it is visible as unaddressed.

## What the loop was doing

Per part, per step:

- rebuild the panel set from the `PartShape` through a virtual call, into a list cleared and
  refilled — for a shape whose half-extents had been fixed since development;
- for each of 24 panels on a box, rotate the normal into world space, rotate the centre into world
  space, then do a cross, a dot, a compare and a scaled add.

**Forty-eight quaternion rotations per box, and not one of them was necessary.** A rotation
preserves the dot product, so `dot(Rn, Rv) = dot(n, v)`; and `Rᵀ(ω × Rc) = (Rᵀω) × c`, so the
panel's own velocity transforms just as cleanly. Rotate the two inputs in, sum in the part's frame,
rotate the two results out: **four rotations per part**, and every intermediate quantity is the
same number written in a different basis.

That is an identity, not an approximation, which meant it could be tested as one.

## Testing a rewrite you claim changes nothing

The project's standing rule is that two implementations of one quantity is how they come to
disagree silently — `SurfaceArea` is derived from `AddPanels` rather than from a second formula for
exactly that reason (entry 0009). A speed rewrite is the one case that inverts it: the *claim* is
that the new code computes the same numbers, and stating that claim requires keeping the old code
somewhere to compare against. It lives in the test project, frozen, and nothing ships against it.

The first version of the test failed, reporting a **3000% disagreement** on boxes.

It was the test. I had normalised the difference by the magnitude of the *net* force — and the net
force on a rotating box is a near-total cancellation of large opposing panel forces, so a
last-bit difference in numbers that were never in disagreement divides by something close to zero.
Normalising against the scale of the *contributions* instead, which is the quantity the claim is
actually about:

| shape | worst disagreement, vs panel-force scale |
|---|---|
| box | 9.4×10⁻⁸ |
| capsule | 6.7×10⁻⁸ |
| sphere | 5.7×10⁻⁸ |

Float epsilon is 1.19×10⁻⁷. So they agree to the last bit, across 400 random orientations per
shape, with panel forces up to 1.9 MN.

Worth noticing that the failure and the fix were both *in the measurement*, and that the reported
number — 3000% — was large enough to look like a catastrophic bug rather than a bad denominator.
The thing that said otherwise was that `ShapeTests`' anisotropy ratios still passed, and a force
wrong by 3000% would have destroyed them.

## The numbers

| creatures | ms/step before | after | drag before | drag after | real time before | after |
|---|---|---|---|---|---|---|
| 1 | 0.134 | 0.069 | 0.053 | 0.014 | 74.9× | 145.0× |
| 128 | 6.418 | 1.465 | 5.465 | 0.588 | 1.56× | 6.83× |
| 256 | 12.772 | 2.760 | 11.013 | 1.343 | 0.78× | 3.62× |
| 512 | 25.909 | 6.446 | 22.698 | 3.521 | 0.39× | 1.55× |

Real time holds to **512 creatures** against about 200 before. The Milestone 1 smoke test passes
unchanged, including the energy balance, whose residual still shrinks with dt (6.6% at 0.01 down to
1.04% at 0.00125) — which is the property that says the balance is measuring a real quantity
rather than an artefact.

## What I predicted, and what happened

I estimated the drag loop would come down about 17× and that PhysX would end up around 70% of the
step. It came down **6.4×** and PhysX is **45%**.

The gap is the interesting part. At one creature — below the threshold where work is spread across
cores, so the first two changes acting alone — drag went 0.053 → 0.014 ms, **3.8×**. At 512 the
same loop is 6.4× faster, so spreading 2,871 parts across 24 cores contributed only a further
**1.7×**.

Which means **most of what remains is not arithmetic.** The gather and apply phases are per-body
engine interop — read a rotation, read two velocities, add a force, add a torque, 2,871 times — and
Unity permits none of it off the main thread. Parallelism cannot help with a cost that is serial by
the engine's rules rather than by ours.

## The conclusion that follows

The plan had further items: vectorise the panel sum, replace the 2×2 midpoint sampling of each face
with Gauss–Legendre nodes or a closed-form integral. Both are still sound. **Both are now aimed at
a minority of a minority** — the arithmetic share of a loop that is 55% of a step — and neither
would have been worth doing even if the estimate had been right, which is only visible now that the
estimate has been checked.

Stopping here was the plan, and the reason for stopping turned out to be different from and better
than the reason given for it.

## The pattern

Entry 0013 was about a guard that had the shape of the bug it was guarding. This one is smaller and
more ordinary: **the estimate was wrong in a way that changed what to do next, and only the
measurement could say so.** 17× and 6.4× lead to different conclusions about whether to keep
optimising — and I would have been arguing about SIMD on the strength of a number I made up.
