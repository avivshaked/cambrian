# One thread a creature

*2026-09-22, evening. Written by the agent the day the GPU spike ran, from
`spikes/02-gpu-featherstone/results/FINDINGS.md`, which carries every table, the commands and
the commit. The spike is a measurement and not the port; what it settles and what it leaves
for the owner are at the end.*

## What was asked

The farm out of Unity steps one creature at a time on the CPU, in doubles, bit-identical at
any thread count, and the machine's twenty-four cores give about twenty times real time
however they are divided (0111). The next ten times has to come from the card, an RTX 4090
that has sat idle through forty-three rounds. Two things had to be known before a port was
worth designing: whether the per-creature articulated-body step runs on the card at all,
through a C# kernel compiler and without rewriting the physics; and in which precision,
since a consumer card runs doubles at about a sixty-fourth of its single rate.

The day began badly for the card. A blue screen at 13:03 came twenty-five seconds after a
first GPU probe, and the spike waited until the minidump was read
(`logbook/specs/crash-2026-09-22.md`). The dump put the fault in the file-system filter
manager, under an Xbox filter driver, with the display driver on no frame; the probe ran
clean at 16:13 on an idle machine, and the spike followed it.

## What was built

A harness of the step and nothing else. One GPU thread is one creature. Its constants and
its whole state are loaded into thread-local arrays at kernel entry, a thousand steps run in
local memory, and the state comes back at the end. The kernel carries the library's
kinematics, the articulated-body passes with the free base's 6×6 elimination and the implicit
limit term, the per-panel pressure drag in the part's own frame, buoyancy, lift, the drive
damper and the limit springs, and the drive torque pair on child and parent; added mass is
folded into the link mass at build as the library does it. It omits the brain and the senses
(the drives are one real pass of each body's brain at its placed pose, then held), contact,
the grid, the bed and the glass, a moving current, growth, the ledgers, the trace and the
guard. The CPU reference runs the same reduced step on real `Creature` objects through the
library's own routines, so the comparison is kernel against library and not kernel against
a rewrite. The bodies are round 43 seed 1's last snapshot: 1,104 genomes, none refused, up to
nine links, 483 with a joint, repeated to fill each population.

ILGPU compiled the whole step with nothing cut: fixed-size local arrays, static methods
taking them, structs with operators, `ref` struct parameters, structs of views as kernel
arguments. The one refusal was a missing intrinsic for `Sin`, `Cos` and `Atan2` on the PTX
backend, which `ILGPU.Algorithms` supplies in software. ComputeSharp, the other candidate,
refuses a local array in a shader outright, and the step needs about 1,600 words of scratch a
thread, so it is out.

## What was measured

**The transcription is exact.** The same kernel on ILGPU's CPU device, in double, against the
library: every component of every joint coordinate, velocity and link position identical,
deviation zero, over a thousand steps. Everything below is therefore the device and the
precision.

**Deviation after ten simulated seconds, 10,000 bodies.** Double: joint angles within
2e-15 rad, positions within 3.6e-14 m, which is rounding and mostly the software
transcendentals against .NET's. Single: joint angles within 9.3e-6 rad, positions within
1.4 mm at worst and 62 µm rms, on bodies about 16 m from the origin with links tens of
centimetres long.

**Identity on the card.** Two runs of the same kernel on the same input are bit-identical at
every population in both precisions, and the digest does not move with the launch
configuration: group sizes 32 to 1,024 and the automatic grouping return the same bits. The
agent reproduced the 10,000-body digests in a separate process
(`ae4314c846190f86` double, `2a870ee0083d6b20` single).

**Pace, microseconds per body-step, median of three, group size 32.**

| bodies | double | single | CPU, 16 threads, same reduced step |
|---|---|---|---|
| 1,000 | 1.020 | 0.127 | 0.097 |
| 10,000 | 0.124 | 0.0213 | 0.089 |
| 30,000 | 0.070 | 0.0174 | 0.090 |
| 100,000 | 0.065 | 0.0188 | 0.090 |

Reading positions back once a metabolic step, the cadence the world step needs, costs 3 to
6% over the kernel alone; a round trip every physics step costs 2.2 to 3.3 times, and it is
launch latency, not bandwidth. ILGPU's automatic group size is up to 2.9 times worse than 32,
because each thread holds 12.7 kB of local memory in double; anything built on this sets
the group size by hand. Kernel compile is 0.3 to 0.6 s once a process.

## The reading

**Double on this card is not worth having.** At 10,000 bodies the card in double is slower
than sixteen of this machine's cores, and it only draws level at 100,000. The arithmetic the
CPU farm has, it would keep; the card it would waste.

**Single is about six times the CPU, and the card is not busy until about 30,000 bodies.**
Per body-step single costs 0.127 µs at 1,000, 0.021 at 10,000 and 0.017 at 30,000, so the
proposal's committed 10,000 sits on the shoulder of the curve and its stretch 100,000 is
where the hardware pays: a port aimed at 10,000 buys about six times, and aimed at 100,000
it buys the same six times on ten times the population.

**What single is not.** A single-precision world is a new realisation of every seed, the
way any per-step change is (the butterfly rule), and no recorded run replays on it. Its
deviation, 1.4 mm in ten seconds on a world whose contact is a soft push between bounding
spheres, is below anything the ecology reads; my inference, marked as such, is that it is
affordable, and the gate is D104's, distributions across seeds and not a digest. Whether
the campaign moves its physics to single is the owner's, and `fable-propose-gpu.md` puts it
in front of them.

**The ratio is honest and it is not the farm's speedup.** The reduced step costs the CPU
0.089 µs against 0.32 µs for the full `DynamicsWorld` step at round 42's crowd, so the
brain, the senses, the contact grid, the ledgers and the trace are about three quarters of
what a port would have to carry too, and the brain is branchy per-creature code the card
will like less than the solver. Sizing the port off this table without measuring the brain
would be the classic error, and measuring the brain is the next spike.

## What I make of it

I had written the GPU into the queue as a single-precision port for months without a number
for the double alternative, and the number turned out to be the whole decision. I am also
struck that the identity story survived: the card gives the same bits at every launch shape,
which is more than PhysX gave us at any thread count.
