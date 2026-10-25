# The D228 and D229 build's checks (2026-10-25)

The plan is `../d228-d229-build-plan.md`. This file records what was run on branch `fin-shape` after the build, once
the `r62bd` card group had ended. Every switch the build adds is off by default:
- `ShapedAddedMass`, D229's fin law;
- `ReflectMirroredAnchors`, D228's mirrored joint;
- `ContactShape`, D228's contact on the true shape, `Spheres` by default.

## The crowd fixture

`runs/cefix-s4/2026-10-25-014251-059fea5e` was recorded unbroken from commit `64313cbd`, a clean tree, by
`fixtures.ps1` beside this file. It ran on the Release farm built from that commit, on logical 8 to 11 at 4 threads,
and took 37 minutes of wall. Its hashes:

| | |
|---|---|
| Config | `059fea5e430ca82d` |
| Core | `7d965516` |
| Dynamics | `8445b328` |
| Farm | `c430f904` |

Its config differs from the D226 fixture's (`runs/cefix-s4/2026-10-24-170754-85523498`) only by the three new fields,
each at its off value.

**The proof.** `python -I scripts/reads/compare-run-rows.py <D226 fixture> <this fixture> --from 0` gives:
- stats: 2,000 rows, 10 to 20,000 s, 170 fields a row, 340,000 values, no difference, and no field in one run alone;
- lineage: 10,408 rows in each, equal apart from the intake-origin fields `iok`, `iak` and `ic`.

The 20,000 s snapshot, `snapshots/000020000.jsonl`, is byte-equal between the two. The comparison's output is
`cefix-s4-d228-vs-d226-rows.txt`. So with the three switches off, this build steps the D226 build's world bit for bit
on the CPU solver for 20,000 s. `src/Evosim.Dynamics.Tests/RunFixture.cs` now names this run.

## The fin law's bench

`Evosim.SwimProbe --fin-bench` on the Debug build of `4697ca33`, on logical 8 to 11, CPU only. The two bodies are of
equal volume, 0.0672 m³, each a root with one powered box limb:
- a stick, half-extents 0.2 × 0.0447 × 0.0447 m;
- a paddle, 0.2 × 0.01 × 0.2 m, flat across its stroke.

Each is stroked by hand for 120 s in still water at a limb push of 0.3, a stroke rate of 2 rad/s and 1 Hz. The
stroke's power is given in the first column. The tables are in `fin-bench/`, one file for each power.

| Power | Stick, law off: m, J, J/m | Paddle, law off | Stick, law on | Paddle, law on |
|---|---|---|---|---|
| 0.8 (default) | 3.19, 4.01, 1.25 | 3.19, 4.01, 1.25 | 4.14, 6.29, 1.52 | 20.45, 125.83, 6.15 |
| 0.4 | 1.33, 0.50, 0.38 | the same | 1.80, 0.79, 0.44 | 9.95, 15.73, 1.58 |
| 0.295 | 0.85, 0.20, 0.24 | the same | 1.19, 0.32, 0.27 | 7.19, 6.31, 0.88 |
| 0.2 | 0.46, 0.06, 0.14 | the same | 0.66, 0.10, 0.15 | 4.70, 1.97, 0.42 |

What the bench shows:
- **With the law off, the two travel alike**, to the bit, at every power, as D135 says.
- **With the law on and the stroke the same, the paddle travels five to seven times as far** and is billed 20 times
  the stick's work at every power. Its work per metre is 2.8 to 4 times the stick's, because it swims faster against a drag that
  grows with speed.
- **At equal work, the paddle travels further.** For 6.3 J the paddle at a power of 0.295 travels 7.19 m, and the
  stick at 0.8 travels 4.14 m.
- **At equal distance, the paddle is cheaper.** The paddle at 0.2 travels 4.70 m for 1.97 J, a third of the stick's
  6.29 J for 4.14 m.

So the law rewards a broad fin per joule, not only per stroke, and the price follows the push through the work
already billed, as the coordinator's reading of D229 says. A body that keeps its drive genes and grows a paddle pays
for the faster swim. Evolution can trade the power down. That is inference from the bench, not yet seen in a world.

## The mirror census, with the corrected reference

`--mirror-census` was run again on `r62s-r3-h0` at 6,000 and 24,000 s. This build refuses that run's config, which
predates D226's field, as every build refuses an older config. So the census ran on branch `census-ref` (`fb5a0885`),
which is `r62-mouth`'s census commit `fc27f323` with one change: development's joint is read as
`Position + R · F · anchor`. Its table is `mirror-census-corrected.tsv`.

The counts are the finding's:
- at 6,000 s, 11 pairs with a twin, all apart in development and stacked in the solver;
- at 24,000 s, 66 pairs apart and stacked, and 5 pairs not stacked.

Against `../mirrored-anchor-census.tsv`, 22 of the 82 developed-joint gaps changed, by at most 0.039 m and mostly
by a millionth of a metre. None crosses from stacked to apart. So the finding stands as written. The old reference
was off only where a mirrored part's own anchor had a component across the mirror.

## The card identity round

It ran on the coordinator's go on 2026-10-25, from commit `15d688fd` with a clean tree, core hash `7d965516`. It used
the Release farm built from `64313cbd`, whose source the later commits do not touch. Each sitting founded seed 6201
four times for 600 s on the RTX 4090, in single precision with the card's transport, with D227's identity settings.
Every world ended at its budget. The outputs are in `identity/`.

**Sitting 1, every switch off, at group 32** (`rounds/identity-d228-off.ps1`). D227's launch group (the dial at 1,
and the 2x, 4x and 16x arms) is compared with `scratch/contact-eating/identity-bdl/r62bdl-<world>-g32`, the same
worlds on D227's build `4e94b0e0`. `gpu-accept.py` exits 0 for all four. Stats are identical on 60 samples, the digest
on 601 rows, and lineage is byte-equal (1,570 and 1,582 lines). So with its switches off, this build is D227's build
on the card, bit for bit.

The pace holds. The wall to 600 s is 0.991 to 0.994 of D227's, for example 199.4 s against 201.2 s for the world at a
dial of 1. GJK's simplex in `ContactLinks` and `OverlapsAgain` therefore costs nothing measurable with the switch
off, and the true shape's pair block stays where it is.

**Sittings 2 and 3, each switch on, at group 32 and then 64** (`rounds/identity-d228-on.ps1`). These run on
`rounds/env-r62cs-w1500.ps1`'s world, which runs per-part contact:

| World | Config hash | 32 against 64 | Parts from the switch-off world at | Alive at 600 s (off 1,454) | Wall to 600 s, 32 / 64 (off 199.4 s) |
|---|---|---|---|---|---|
| Fin law | `692d4790` | identical, 1,610 lineage lines | 10 s | 1,490 | 190.4 / 191.4 s |
| Mirrored joint | `d5df26b4` | identical, 1,606 lines | 320 s | 1,488 | 198.0 / 198.0 s |
| True shape | `38e076f2` | identical, 1,604 lines | 20 s | 1,518 | 198.1 / 196.5 s |
| All three | `081cda5b` | identical, 1,595 lines | 10 s | 1,509 | 193.3 / 191.7 s |

`gpu-accept.py` exits 0 for each world at 32 against 64. Each switch parts its world from the switch-off world, so
each reached the thing it configures. The mirrored joint parts it only at 320 s, which is probably the first birth
with a reflected part (inference). With 1,500 bodies at 600 s, no switch slows the card. The true shape's cost in a
crowd of ten thousand is not measured by these runs.

## The suites

These were run on `fin-shape` on 2026-10-25 through `scratch/r62-fix/lane-test.ps1`, at 4 threads:

| Suite | Result |
|---|---|
| Core, with the slow tests | 1,418 pass |
| Dynamics, without the slow tests | 300 pass, and one skipped: the mirrored-pose fact with its switch off |
| Farm, without the card and slow tests | 396 pass |
| The CPU-accelerator engine tests (`GpuCpuAcceleratorTests` and the refusal, instant and kernel-source tests) | 37 pass, the fin law's and the true shape's cases included |
