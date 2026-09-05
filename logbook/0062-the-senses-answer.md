# 0062 — The senses answer

*2026-09-05. Not a round: the build record for D075's first item, written from the
implementing agent's report (`scratch/perception-build-report.md`). Owner: "agreed, go
ahead" on starting it in parallel with the confirmation rather than after it.*

## What was built

Commit `e59f6af`, 517 Core tests (from 508). Three of the channels §4.4 names for finding
food now report real quantities instead of zero, and the machinery that decides which
channels a genome may *draw* is a run setting rather than a static list:

- **Chemical**, per part: the edible nutrient density at the part's own height and patch
  (`EdibleDensityAt`, D055's refuge discount included), squashed `x / (x + k)` with `k`
  10 J/m³ so a gradient sensor keeps resolution across the two decades the field spans.
- **Energy**, per creature: `SecondsOfReserve`, which had said "what `SensorChannel.Energy`
  reports" in its own comment since it was written and had never been connected; `tanh(s /
  3,000 s)`, infinity reads 1.
- **Flow**, per part, three axes: the relative water velocity the drag pass already
  computes, rotated into the part's frame, clamped at 0.3 m/s per axis. It reads the
  previous physics step's value (10 ms at 0.01) because `Sample()` runs before the fluid,
  and reordering the step would end the record's replay; stated in the code.
- **The pool.** `SenseChemical`, `SenseEnergy`, `SenseFlow` (`EVOSIM_SENSE_*`), default
  off. `CreatureSensors` answers all seven channels in every run; the switches decide what
  founders and mutation may reach for, because a longer pool returns a different channel
  for the same `Rng.Pick` draw and that is what would break replay. Header token
  `senses jointangle,jointrate,up,depth[,chemical,energy,flow]` plus the three scale
  constants, all three marked unmeasured.
- **The §4.4 mask, built.** A per-brain bitmask of the channels any neuron references,
  taken at birth; `Sample()` skips the transform reads and field lookups nothing reads.
  Bit-identical by construction and the source of a wall-clock *gain*: the build is about
  21% faster than what it replaced on the same configuration and seed (2.4 → 1.9 min for
  2,000 s at 0.01, four arms running alongside, so the sign and order are trustworthy and
  the figure is not a benchmark).
- **The instrument for the movement round:** `spd jnt`, `spd rig` (root speed over the
  window, jointed and jointless bodies separately, accumulated rather than sampled so a
  stroke's phase is not read as a speed) and `food jnt`, `food rig` (edible density at the
  root at the sample). A guild with no members prints an em-dash; the JSONL carries sums
  and counts. The reading "movement pays" is `food jnt` above `food rig` by more than the
  wingspan across seeds with `spd jnt` non-trivial.

## Validation

- Milestone 1 smoke, extended with a gradient field, a current and a reserve stand-in:
  all seven channels finite and non-constant.
- **Replay identity at the default:** the pre-change build and the post-change build on
  the same settings and seed (r16dt-01c's configuration, 2,000 s, dt 0.01): 20 of 20
  report rows byte-identical across all 54 baseline columns, same 456 births, same
  fastest creature. The agent could not diff against the historical `r16dt-01c` report
  itself, which predates two rounds of columns and a config-hash change; running the
  pre-change build fresh is the stronger comparison.
- With all three senses on (`pv-senses`, 2,000 s, dt 0.01): audit 0.0000% every row, no
  divergence, 534 births against 456 (a changed pool is a changed world, as expected),
  and the last snapshot carries 6 Chemical, 6 Energy and 2 Flow inputs across 8 genomes.
- The manifests' `simHash` for the build is `30b96bf6f4da339b`, `coreHash
  75fc43d8a0d1589c`.

## Two notes for the owner's record

- **Matter smell** was offered and not built. A second `Chemical` index reading
  `World.Matter` is one array and one `case`, but it changes what `rng.Range(IndexCount)`
  draws (a replay-breaking change needing the pool's treatment) and it is a world-rule
  question: a stomach that can smell matter can move to it, which makes the open budget
  something the animals participate in. The owner's, when the movement round is read.
- **Reading the fluid's velocity array by creature slot is wrong and quietly so:** the
  slot order is rebuilt on every birth and death, so a sampler reading by slot would
  sometimes report another animal's water, plausible and invisible. The value lives on
  `CreatureInstance.RelativeVelocity` instead, allocated only for brains that read Flow.

## What changed for everyone

Every config hash changed again. Older `config.json` files no longer load on this build
(§9's refuse-rather-than-default rule and a missing `sense` group), so `ledger.ps1
-Config` against a run directory written before `e59f6af` throws. And round 24's fifth
seed runs on this build while seeds 1–4 run on the one before it (0061's launch note),
on the strength of the identity check above.
