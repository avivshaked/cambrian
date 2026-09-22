# Proposal: the physics on the card, in single precision

*Fable, 2026-09-22, draft. For the owner's ruling after the GPU spike (logbook/0112,
`spikes/02-gpu-featherstone/results/FINDINGS.md`). Absorbed into DECISIONS.md on ruling,
then deleted.*

## What the spike settled

The per-creature articulated-body step of `Evosim.Dynamics` runs on the RTX 4090 through
ILGPU with nothing cut, bit-identical run to run and across launch shapes, and the
transcription is exact against the library. In double the card is slower than sixteen of
this machine's cores at 10,000 bodies and level at 100,000. In single it is about six times
the CPU at 10,000 and the same six times at 100,000, with a deviation after ten simulated
seconds of 1.4 mm at worst and 62 µm rms in position and 1e-5 rad in joint angle.

## What I propose

1. **The port runs in single precision.** Double buys nothing on this card. The deviation
   of single is below what the ecology reads: bodies are tens of centimetres across, contact
   is a soft push between bounding spheres, and the fields are metre cells. A body still
   goes non-finite the same way it does now, and the divergence guard stays.
2. **It is a new engine and a new realisation**, recorded as such: `run.json` carries
   `engine: "dynamics-gpu"` and a `kernelHash`, no recorded run replays on it, and the base
   round on it is read the way round 43 read the farm against round 42 (D104's gate,
   distributions across seeds and every mechanism prediction). Identity claims are made on
   the card against itself, at one launch shape and another, the way the farm's are made at
   one thread and sixteen.
3. **The target is 100,000 bodies, not 10,000.** The card is not busy until about 30,000,
   so the port is designed for the world that fills it: the flat, struct-of-arrays state
   with a fixed link ceiling per kernel build, positions read back once a metabolic step,
   the world step still on the CPU until it binds.
4. **The brain is measured before the port is sized.** The spike's reduced step is about a
   quarter of the real one; the brain and the senses are branchy per-creature code and may
   not take the same six times. One more spike, the brain and the senses as a kernel on
   the same bodies, before any date is put on the port.
5. **Contact stays on the CPU in the first port.** The contact grid is the one part of the
   step that couples creatures; the first port moves the uncoupled step only, and the
   contact pairs are found on the CPU from the positions read back each metabolic step and
   applied as an impulse the kernel reads as a constant for the next fifty steps. Whether
   that is faithful enough to the farm's contact law is a measurement in the port's spec.

## What the owner rules

1. Single precision as the campaign's physics on the card, knowing the record does not
   carry across.
2. The target crowd, 100,000, and with it the world that holds it (area, matter), which is
   a world rule.
3. Whether the brain spike runs before the animal kit's first rung or after it.
