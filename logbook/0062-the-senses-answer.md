# 0062 — The senses answer

**2026-09-05**  ·  a build record, not a round

Three of the channels §4.4 names for finding food now report real quantities instead of
zero. The pool a genome may draw from became a run setting rather than a static list. The
build also came out about a fifth faster than the one it replaced, because a per-brain mask
skips the reads that nothing looks at.

This entry is written from the implementing agent's report,
`scratch/perception-build-report.md`. The owner's ruling on starting the work in parallel
with the confirmation round, rather than after it, was short.

> agreed, go ahead

## What was built

The commit is `e59f6af`, at 517 Core tests, up from 508.

| channel | what it reports |
|---|---|
| Chemical, per part | the edible nutrient density at the part's own height and patch (`EdibleDensityAt`, D055's refuge discount included), squashed `x / (x + k)` with `k` 10 J/m³ so a gradient sensor keeps resolution across the two decades the field spans |
| Energy, per creature | `SecondsOfReserve`, as `tanh(s / 3,000 s)`, where infinity reads 1 |
| Flow, per part, three axes | the relative water velocity the drag pass already computes, rotated into the part's frame, clamped at 0.3 m/s per axis |

The reserve channel had said "what `SensorChannel.Energy` reports" in its own comment since
it was written, and had never been connected.

Flow reads the previous physics step's value, 10 ms at 0.01, because `Sample()` runs before
the fluid and reordering the step would end the replay of every earlier run. The code says
so at the site.

The pool itself is three switches, off by default.

| switch | set by |
|---|---|
| `SenseChemical`, `SenseEnergy`, `SenseFlow` | `EVOSIM_SENSE_*` |

Every run answers all seven channels through `CreatureSensors`. The switches decide only
what founders and mutation may reach for. A longer pool returns a different channel for the
same `Rng.Pick` draw, and that is the thing that would break replay.

The header token reads `senses jointangle,jointrate,up,depth[,chemical,energy,flow]`, with
the three scale constants beside it, all three marked unmeasured.

§4.4's mask is built too. It is a per-brain bitmask of the channels any neuron references,
taken at birth, and `Sample()` skips the transform reads and field lookups that nothing
reads.

The mask is bit-identical by construction, and it is the source of a gain in wall clock. The
build is about 21% faster than what it replaced on the same configuration and seed. That is
2.4 minutes down to 1.9 for 2,000 s at 0.01, with four arms running alongside. The sign and
the order are trustworthy, and the figure is not a benchmark.

The instrument for the movement round, which is D075's, is four new columns.

| columns | what they carry |
|---|---|
| `spd jnt`, `spd rig` | root speed over the window, jointed and jointless bodies separately, accumulated rather than sampled so a stroke's phase is not read as a speed |
| `food jnt`, `food rig` | edible density at the root at the sample |

A guild with no members prints an em-dash, and the JSONL carries sums and counts.

Movement pays when `food jnt` sits above `food rig` by more than the wingspan across seeds,
with the jointed speed column non-trivial.

## Validation

The Milestone 1 smoke test was extended with a gradient field, a current and a reserve
stand-in. All seven channels come back finite and non-constant.

Replay identity holds at the default. The pre-change build and the post-change build ran on
the same settings and seed, at r16dt-01c's configuration for 2,000 s at dt 0.01. They give
20 of 20 report rows byte-identical across all 54 baseline columns, the same 456 births, and
the same fastest creature.

The agent could not diff against the historical `r16dt-01c` report itself, which predates
two rounds of columns and a config-hash change. Running the pre-change build fresh is the
stronger comparison.

With all three senses on, in `pv-senses` over 2,000 s at dt 0.01, the audit reads 0.0000%
every row with no divergence. There were 534 births against 456, since a changed pool is a
changed world, and the last snapshot carries 6 Chemical, 6 Energy and 2 Flow inputs across 8
genomes.

The manifests record the build's `simHash` as `30b96bf6f4da339b`.

Its matching `coreHash 75fc43d8a0d1589c` sits beside that.

## Two notes for the owner

Matter smell was offered and not built. In code it is one array and one `case`, a second
`Chemical` index that reads the matter field.

What stops it is `rng.Range(IndexCount)`. Adding an index changes what that draw returns,
which is a replay-breaking change needing the pool's treatment. Reading `World.Matter` is
also a world-rule question, because a stomach that can smell matter can move to it, and that
makes the open budget something the animals participate in. It is the owner's call, when the
movement round is read.

Reading the fluid's velocity array by creature slot is wrong, and it fails without a sign.
The slot order is rebuilt on every birth and death, so a sampler reading by slot would
sometimes report another animal's water, plausible and invisible.

The value lives on `CreatureInstance.RelativeVelocity` instead, allocated only for the
brains that read Flow.

## What changed for everyone

Every config hash changed again, so older `config.json` files no longer load on this build.
The reason is §9's refuse-rather-than-default rule and a missing `sense` group.

That means `ledger.ps1 -Config` against a run directory written before `e59f6af` throws.

And round 24's fifth seed runs on this build while seeds 1 to 4 run on the one before it.
That is 0061's launch note, made on the strength of the identity check above.
