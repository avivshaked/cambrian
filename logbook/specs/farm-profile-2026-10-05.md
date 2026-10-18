# The farm profile of 2026-10-05

Round 56's seed 2 (`runs/r56-s2/2026-10-04-183358-a174d013`) was resumed from its checkpoint at
29,700 s on main's farm build (a89eb7f/5a3fde4, Release built at 10:43). The farm refused the
checkpoint at first, because its hashes differ from main's checkout. They differ by carriage
returns alone: 23 Core files differ in bytes and none in text. So the farm ran with
`EVOSIM_ALLOW_SOURCE_MISMATCH=1`, as a cousin of the recording. It ran at eight threads with nothing
else running, and the heat read OK at every check. What follows is shares and pace only, and no
identity claim, since the brain-v2 restore is not faithful.

## Pace and wall split (measured, `stats.jsonl`)

Two plain resumes of 290 s each (29,710 to 30,000 s), about 7,440 to 7,540 bodies alive:

| | Resume a | Resume b |
|---|---|---|
| Wall, s | 330.1 | 329.0 |
| Pace, times real time | 0.879 | 0.881 |
| Physics | 81.5% | 81.4% |
| of which the bodies' phase (`StepOne`, parallel) | 67.4% | 66.9% |
| of which the contact grid build | 12.6% | 13.1% |
| of which after-step | 1.5% | 1.5% |
| World step | 16.2% | 16.3% |
| Harness | 1.4% | 1.4% |
| Writers | 0.9% | 0.9% |

The farm slows as its crowd grows. The original run's own `stats.jsonl` shows it, though that run
shared the machine with others (measured): 3.08 times real time at 1,547 bodies (5,000 s), 2.44 at
2,407, 1.83 at 4,084, 1.18 at 4,958, 0.87 at 5,855 and 0.66 at 7,457 (29,700 s). The figures of 1.29 to 1.31 times
quoted earlier were taken at 4,300 to 4,900 bodies.

## Shares by method (measured, the EventPipe sampler over a 60 s resume)

225,052 samples passed through an `Evosim` frame. Inclusive shares of those:

| Where | Inclusive |
|---|---|
| `DynamicsWorld.StepOne` (a body's step) | 85.3% |
| `Water.Sample` (the water at every link) | 33.5% |
| of which `CurrentField.VelocityAndAccelerationAt` | 30.0% |
| of which `ReefStreamsAccelerationAt` (the acceleration near the reef) | 15.7% |
| `Contacts.Apply` | 25.0% |
| of which `ContactGrid.NeighbourLinks` (exclusive) | 16.5% |
| of which `ContactReef.Push` (reef distance) | 4.8% |
| `Fluid.Apply` (drag, exclusive) | 14.4% |
| `Simulation.Step` (the host's world and harness) | 17.6% |
| `Aba.Solve` (the articulated solve) | 4.6% |

Largest leaves (exclusive): `NeighbourLinks` 16.5%, `Fluid.Apply` 14.4%, `StreamsUnitWithGradient`
8.1%, `ReefGeometry.SignedDistance` 4.7%, `StreamsPotentialUnitWithGradient` 4.5%, `Water.Sample`
3.5%, `StreamsUnit` 3.5%. Thread-pool waits inside Evosim frames: `Monitor.Wait` 1.2%, `SpinWait`
0.4%.

## Whether the farm's Parallel.For leaves threads idle

This profile does not answer it. The sampler's effective rate under load is not known, so its
counts do not convert into thread-seconds. The sampled run was also slower than the plain resumes,
at 64.5 s of wall for 50 simulated seconds.

Two measurements would answer it. One is the same resume at one thread and at eight, comparing the
wall of the bodies' phase. The other is a busy counter per thread in a body's step, like the
nursery's, read against the phase's wall. The farm hands out 7,500 bodies of uneven cost. A tail of
one chunk is believed small against that many, but it is not measured.

## Reading (inference)

At a round's end the water at the links and the contact search are the two largest costs, a third
and a quarter of the samples. The water is the same routine the nursery's A2 targets, so a fused
sampler pays in both programs; near the reef the acceleration's gradient is half of it. The contact
search is `NeighbourLinks` walking the grid per part; the grid build itself is 13% of the wall. The
solver proper is under 5%.
