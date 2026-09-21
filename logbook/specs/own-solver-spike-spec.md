# A solver of our own: the spike

*2026-09-21. The owner's ruling of the afternoon: pace comes before a more complex world,
because what is built for it now is used by every round after. This is the spike that says
whether the project's own articulated-body solver is worth adopting. It decides nothing by
itself. Adoption is a proposal to the owner with these numbers in it.*

## Why

Round 42's seed 1 spent 37% of its wall in PhysX and 54% in the harness around it, all on
one thread. PhysX is held to one thread by D078, because touching articulations solved on
several threads part from their recording. The harness is on the main thread because every
read from and write to the engine has to be. One arm therefore uses about one core of a
machine with twenty-four, and a world of 900 bodies at 3.5 links each runs at 0.4 times
real time. The cost grows with links and with contacts, which is the direction the worlds
have to go.

## What it is

A reduced-coordinate solver in plain C# with no Unity in it, in a project of its own
(`src/Evosim.Dynamics`, referencing `Evosim.Core`), stepping each creature alone.

- **Bodies.** A floating base and a tree of links from Core's `Phenotype`: masses and
  inertias from the parts' shapes and the tissue density, joints of every `JointType` with
  their limits. Featherstone's articulated-body algorithm, O(links).
- **Forces.** The water as `FluidEnvironment` has it (panel drag, added mass, the water's
  acceleration, buoyancy and the surface rule), ported term for term; joint drives from the
  creature's own `Brain` through `EffectorDriver`'s conditioning; joint limits.
- **Contact.** Never a shared solve. A soft push between bodies from positions at the end
  of the previous step, found through a uniform grid and summed in a fixed order, and the
  same for the bed and the glass. No creature's step reads another's current state.
- **Threads.** Bodies are stepped in parallel. No arithmetic depends on the partition, so
  a trajectory is the same to the bit at any thread count. A test asserts it.

## What it has to show

1. **Sanity.** Momentum held in still vacuum under internal drives; energy held by an
   undriven chain with no water; a towed box reaches the drag law's terminal speed; a
   driven two-link body makes headway in water and none in vacuum.
2. **Stability.** Round 42's evolved bodies (`runs/r42-s4`, the 20,000 s snapshot) stepped
   under their own brains for 600 s at dt 0.01 and at 0.02 with nothing non-finite. The
   throws of rounds 34 to 37b were the engine's; this is where ours would show.
3. **Pace.** The same thousand bodies, microseconds per body-step and per link-step at 1,
   2, 4, 8, 16 and 24 threads. Today's figure is about 20 µs per body-step on one thread
   (11.5 harness, the rest PhysX), and no more threads to give it.
4. **Identity.** State digests equal at 1, 4 and 16 threads after 10,000 steps.
5. **Parity**, by the caller afterwards and not by the building agent: a few genomes swum
   alone in still water in both engines, speed and joint traces compared. Agreement to the
   bit is not the bar; the same stroke giving the same order of headway is.

## What it is not

No economy, no grid, no run directory, no Unity. No change to `Evosim.Core` or to anything
under `unity/`. Disposable if the numbers say so.
