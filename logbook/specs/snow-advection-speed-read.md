# Where the snow advection's time goes: Fable's read of 2026-10-02

On 2026-10-02 at 16:15 a Fable subagent was asked, read-only, how to speed up carrying the snow with the current (`GridField.AdvectFromThePotential`). In a one-thread nursery episode that phase is 88% of Core's `World.Step`, and `World.Step` is 78% of the episode (HANDOFF item 10a). The subagent read the measurement worktree `scratch/wt-sense-cost` at `dc5dd6f`, whose Core matches main's. Nothing was edited, built or run. Its report is below, saved as it came on 2026-10-02 at about 16:25. Figures it marks *est.* are estimates from the code and the grid size. The subagent's text keeps its own voice and has not been restyled.

The session checked one claim before acting on it: the angle-addition form is already in `CurrentField.Eddies` (line 3135, `cosChi = cosM * instantCos[k] - sinM * instantSin[k]`). So the first option put to the owner at 16:10, "compute the same current a cheaper way with the angle-addition identity", described work that was already done. HANDOFF item 10a carries the correction.

---

## 0. The path this config takes

All line numbers are in `scratch/wt-sense-cost/src/Evosim.Core/` unless another path is given.

| Fact | Where |
|---|---|
| Tank, 1600 m², 25 m deep, snow grid on 1 m cells; matter grid on 5 m cells | config.json:329-331, 74, 76 |
| Shaped floor (relief 1.5 m, no shore fade), no reefs | config.json:10-14, 257; `_bed` set at CurrentField.cs:537, `_fadeOn` false at 541 (shore 0) |
| Grid: ny = 25 (GridField.cs:413), nx = nz = ceil(45.14 − 1e-6) = 46 (424-425) → 52,900 cells, ~40,000 live (mask built 566-618) | |
| `Advect` → tank is "of a place" → `AdvectFromThePotential` | GridField.cs:2487, 2507-2510 |
| Mode is `Transport` but in a tank the potential is the streams' (the box's 5-mode `PotentialUnit` at 1335-1360 is never reached) | CurrentField.cs:1197-1204, 1272-1286 |
| Streams terms: 4 azimuthal × 2 families × 3 vertical = 24 eddy terms + 3 overturning cells | CurrentField.cs:3539-3551 |
| Sampling path: hoisted terms + bed columns (`PrecomputeStreamsTerms`, `PrecomputeBedColumns` both default true) → `PotentialAt(in StreamsColumn, in BedColumn, in StreamsDepth, float y, double)` | GridField.cs:3446, 3460, 3707-3725; calls at 3944-3947 (x), 3995-3998 (z), 4064-4067 (y); body CurrentField.cs:2961-2987 |
| No transport device in the nursery (null → CPU) | GridField.cs:2918-2921 |

## 1. Where the time goes inside `AdvectFromThePotential` (2820-2854)

One step = `CourantSubsteps` (refusal only) → `EnsureFaceBuffers` → `SampleEdges` → `AssembleFaces` → `LargestOutflowFraction` → `SubstepsFor` → `ApplyFaces`, and for substeps > 1 the sample/assemble/apply trio again at its own clock (2848-2853).

**Substep count.** `SubstepsFor` takes ceil(outflow / 0.75) from the largest per-cell half-sum of |face flux|·dt/V (2858-2862, 4217-4279). The a-priori Courant check only refuses (2829, 2572-2576): ceiling 2.45 × 0.1 m/s × 0.5 s / 1 m = 0.12 ≤ 0.5. The outflow half-sum is at most ~3·|u|·dt/h ≈ 1.5·|u| and the discrete rim speeds are *below* the analytic ones (2796-2800), so with |u| ≲ 0.25 m/s the outflow is ≲ 0.37 < 0.75: **one substep every step** (strong inference). So the substep loop never runs here and the cost of *deciding* it is one pass over three face arrays, ~0.1 ms (est.). There is little to gain from deciding it more cheaply.

**Edge counts (exact from the geometry).** Arrays: `_edgeX` 46·26·47 = 56,212, `_edgeY` 47·25·47 = 55,225, `_edgeZ` 47·26·46 = 56,212 (3310-3312). Visited per substep: x and z edges on the 24 interior node planes (surface and floor planes are `Array.Clear`ed, 3847-3852) = 2 × 51,888; y edges on 25 layers = 55,225 → **159,001 edge visits**. Closed edges (any of four cells dead) read one byte and write 0 (3919-3923, 3969-3973, 4039-4043). With ~76% of the array live, about **110,000-120,000 `PotentialAt` calls per substep** (est.).

**Trig calls per sample: zero.** The angle-addition rewrite the brief asks about is already in place (D105): cos(mθ + ψ_k(t)) = cos mθ·C_k(t) − sin mθ·S_k(t) at CurrentField.cs:3135, with cos mθ/sin mθ per column (2857-2885, built once per current 3508-3536) and C_k, S_k per pinned instant (4532-4537). The depth sines are tabulated per (column, depth) on a shaped floor (3569-3571, 3580-3602). The only transcendentals per substep are `PinInstant` → `EnsureInstant`: 24×4 + 3×4 = 108 per slot, and the pin fills up to two slots (4405, 4409) → ≤ 216 Sin/Cos per substep, ~4 µs (est.).

**Per-sample work, hoisted bed path** (CurrentField.cs:2961-2987):

| Piece | Where | Ops per edge (est.) |
|---|---|---|
| Guards: `OpenHoistedSample` (HasPotential → VentActive, shape, bed, Sampled) + `RequirePinnedAt` | 2993-3029, 3035-3040, 1139-1140, 898 | ~10 branches, 2 calls; all invariant across the pass |
| `MapFrom`: clamp, `y·C`, `y·D/d²`, `·hx`, `·hz`, builds a 10-field `BedMap` | 2438-2465 | ~6 flops, ~12 stores/loads (struct too big for JIT promotion — inference) |
| `StreamsPotentialFrom`: sin2, sin3; 8 `Eddies` calls × 3 terms | 3069-3112, 3117-3141 | 24 terms × (1 mul `base·profile` + 3 for cosChi + 2 for `(amp·f)·cosChi` + 1 for `ay -=`) = 168 flops; 72 bounds-checked array loads; 3 array-field fetches per call; a branchy profile select (3129) |
| Overturning: 3 cells | 3096-3108 | ~12 flops, 3 loads, 3 branches |
| Float3 build, bed combine `A_x + a·A_y`, `c·A_y`, `A_z + b·A_y`, scale `(float)(speed·scale·bedScale)` recomputed per edge, ×h | 3110-3111, 2980-2986, GridField.cs:3950 | ~12 flops, 6 float casts |
| **Total** | | **≈ 200 flops, ≈ 80 table loads, 10-13 method calls if nothing inlines, ≈ 25 branches** |

115k samples × 200 flops ≈ 23 Mflop per step. At 6 ms that is ~4 Gflop/s on a core that can do an order of magnitude more: the leg is overhead- and latency-bound, not arithmetic-bound. Two things set the floor: instruction count (~600-700 instructions per edge incl. bounds checks and calls, ≈ 50-55 ns at IPC ~3 and 4.5 GHz — which matches 6.4 ms / 115k = 55 ns, so the estimate is self-consistent) and the 24-deep dependent chain of `ay -=` (3137), which alone is ~24 × 3-4 cycles ≈ 16-20 ns per edge that out-of-order execution can only partly overlap across edges.

**Leg split per step (est.)** — anchored on two measurements: round 50's probe put `SampleEdges` at 35-38 of `Advect`'s 48-51 ms on an 8M-edge grid at 16 threads (logbook/specs/transport-probe/Program.cs:21-29), and this run's `Mix` (same shape as assemble + apply: one flux build over all cells + three applies, GridField.cs:2344-2419) measured 0.37 ms.

| Leg | ms per step | Share |
|---|---|---|
| `SampleEdges` | ≈ 5.7-5.9 | ≈ 90% |
| `AssembleFaces` (4095-4206) | ≈ 0.15-0.25 | 3% |
| `LargestOutflowFraction` (4217-4279) | ≈ 0.1 | 1.5% |
| `ApplyFaces` flux build + `ApplyEast/Down/Front` (4295-4385, 4591-4667) | ≈ 0.2-0.3 | 4% |
| Pin, fold, Courant, clears | < 0.02 | — |

Memory is not the limiter: per-step traffic is ~10 MB (edge 1.3 MB, faces 1.2 MB, fluxes 1.2 MB, stock 0.4 MB, column tables ~1 MB, shaped depth tables ~3.7 MB, openness 0.17 MB) ≈ 0.5 ms at single-core bandwidth, and much of it is L2-resident (est.).

**Redundancy found.** No point is sampled twice (the three lattices are distinct midpoints) and nothing time-independent is rebuilt per step (ReferenceEquals guards 3486, 3625, 3760). What *is* recomputed needlessly, per edge: (a) `cosChi` for all 24 k, which depends on the column and the instant only, so it is identical for the ~24 edges of one column at different depths — 72 of the 200 flops and 48 of the 80 loads; (b) the two guard methods and the scale float, invariant over a pass; (c) `MapFrom`, a pure function of (column, depth); (d) on a **flat** floor the horizontal edges need only `w·Dz` / `−w·Dx` (3110-3111; callers take `.X`/`.Z` at 3940, 3991) and the whole 24-term `ay` is thrown away — not the case here because the bed mixes `A_y` into every component (2982-2985).

## 2. Speed-ups

Legend for the class column: **BI** = bit-identical (same ops, same order, provably same bits); **NR** = new realisation (rounding changes, owner's decision); **WR** = world rule (the owner's). "Gain" is on this phase (6.4 ms) unless stated; the run's share is 69%, so a phase factor f gives a run factor 1/(0.31 + 0.69·f): 2× → 1.5×, 3× → 1.85×.

| # | Idea | Class | Expected gain (reasoning) | Risk | Size |
|---|---|---|---|---|---|
| 1 | **Flat "CPU kernel" for the hoisted tank path**: one method per lattice over the flattened tables `PlanPotentialTransport`/`AxisFor` already produce (GridField.cs:2968-3085) and the instant `CopyStreamsInstant` already copies (CurrentField.cs:4461-4476); guards and the scale float once per pass; the 24 terms unrolled with constant indices into locals; `MapFrom` as three scalars; no per-edge calls. The ILGPU kernel is this exact form already written out and verified to 0 of 7,993,869 edges differing (src/Evosim.Farm.Gpu/GpuTransport.cs:299-374; probe results above) — port it line for line without `M()`. | BI | Instruction count per edge ~700 → ~250 (est.); sampling 5.8 → ~2.5 ms; **phase ≈ 2×**. | Low. Must keep `(amp·f)·cosChi`, `ay -= …` order, every `(float)` cast and `value·h` (GridField.cs:3950). Bit test pattern exists: StreamsHoistTests / BedGridTests hold both paths bit for bit (3442-3444, 3455-3457). .NET's JIT does not contract to FMA, so no hidden fusion. | ~300-500 lines; Core only |
| 2 | **Column-major sampling with `cosChi` hoisted per column**: for each (i,k) column compute the 24 `cosM·C_k − sinM·S_k` once, then walk its ~24 depths. The edge value is one expression per edge, so loop order is not part of the arithmetic (the file's own argument at 3673-3679 and 4221-4226). Transpose the shaped depth table to column-major when built (3586) so the walk stays sequential. On one thread the plane split (3860-3871) is irrelevant; on the farm, split over columns instead — columns are as independent as planes. | BI | Removes 72 flops and 48 loads of ~200/80 per edge: a further ~1.3-1.5× on sampling after #1 (est.). | Low-moderate: the depth table layout and the `EdgeTerms` indexing (3810-3838) change; the `PotentialTransportPlan` consumer (GPU) must keep its own layout or be given the transposed one. | ~150 lines on top of #1 |
| 3 | **Interleave 2-4 edges per inner iteration** so their 24-deep `ay` chains overlap. Each edge's op sequence is unchanged. | BI | Only pays once #1 has made the chain the floor (~16-20 ns/edge): up to ~1.5× further on sampling (est.; depends on how far OoO already overlaps). | Low; code bloat; measure before keeping. | ~100 lines |
| 4 | **Table `MapFrom`'s A and B (and sin2, sin3) per (column, depth)** beside `SinPhi/CosPhi`, built with the same expressions (2455-2457). | BI | ~5-8% of sampling; +~2.4 MB tables. Fold into #1's record layout rather than do alone. | Low | Small |
| 5 | **Flat-floor worlds only: skip the eddy sum on x and z edges** (they read only `w·Dz`, `−w·Dx`). | BI | Zero here (bed on). On flat tanks: 2/3 of edges × ~85% of their work → ~2× on sampling. Worth a branch on `bed == null`. | Low | ~30 lines |
| 6 | **Fold `LargestOutflowFraction` into `AssembleFaces`** (max is order-independent, 4221-4226). | BI | ~0.05-0.1 ms (1-2%). | Low | Small |
| 7 | **Separable regrouping of the eddy sum**: A_y = Σ_m [cos mθ·Σ_j F_mj Σ_q P_q·(B_k C_k) − sin mθ·Σ_j F_mj Σ_q P_q·(B_k S_k)], with the 48 products B_k C_k, B_k S_k folded per pin. Shaped floor: ~115 flops/edge; flat floor: the Σ_q dots are per plane → ~40 flops/edge. | **NR** (the sum is re-associated; last bits move; chaotic amplification follows) | Shaped: ~1.5× on sampling beyond #1-#2; flat: ~3×. Conservation untouched (any edge values telescope exactly, 2782-2786); the curl test's tolerance holds; bit tests need the direct path changed identically. | Moderate: a second realisation of every seed; tests that compare hoisted vs direct to the bit must be updated. | ~200 lines + tests |
| 8 | **Skip zero-stock regions** | NR in principle (the substep count reads every live cell's faces, 4217-4279, so skipping edges changes `LargestOutflow` unless all faces are still assembled, which forfeits the saving); WR if a threshold is used | **Not worth it here**: `Mix` moves 0.01 of any non-zero stock to each neighbour every step with no threshold (2320, 2384-2411), so within ~50 steps (25 s) of the first deposit every live cell is non-zero and nothing is skippable for the remaining 575 s of a 600 s episode. `ApplyEast/Front/Down` already skip exact-zero fluxes (4609, 4634, 4660). | — | — |
| 9 | **Cache the field across steps / exploit time structure** | NR or WR | No cache hit possible: 24 rates ±(1 + j·√2) (CurrentField.cs:4131) make the field quasi-periodic with no short period. The time dependence *is* separable — the instant is 51 coefficients (24 C, 24 S, 3 cells after folding B) — and the exploitable form of that is #7; a per-edge basis table (51 × 159k doubles = 65 MB streamed per step, ~3 ms) is memory-bound and worse than #1+#2. Interpolating edge values in time between full samples changes the water: WR. | — | — |
| 10 | **Advect the snow every 2nd metabolic step with 2·dt** | **WR** | Outflow ≈ 0.37 per 0.5 s → 0.74 per 1 s, still one substep (margin 0.75, 2759): ~2× on the phase for free in compute; at every-4th the outflow bound would split it into 2 substeps (2860-2862) and the saving halves. The water is then sampled at half the clock resolution. | The owner's. Changes every result. | Small code, large record implication |
| 11 | **Coarser snow cells (2 m)** | **WR** | 8× fewer cells and edges → ~8× on this phase and on `Mix`. The 1 m cell is tied to `fieldKernelMetres` 1 m (config.json:75) and the feeding geometry. | The owner's; the biggest lever and the one most entangled with world rules. | Config only |
| 12 | **Fewer stream terms** (e.g. 3 azimuthal, 1 family) | **WR** | Proportional to the term count (24 → 9-18). Different water; `BuildStreams`' measurements and `StreamsHoistTests`' 4×2×3 unroll (2694-2697) change. | The owner's. | Moderate |
| 13 | **Use the farm's transport device** (EVOSIM_GPU_TRANSPORT, bit-identical by its own check) for the nursery | BI by evidence, not by proof | Round 50's 2.6M-cell step went 50 → 5 ms on the card; a 53k-cell step would be launch-latency bound (~0.3-0.5 ms est.), and 14 episodes would contend for one context and one card. | Operational (per-thread streams, card shared with the physics); not a Core change. | Farm plumbing |
| 14 | SIMD (`System.Numerics.Vector<T>`) across edges | BI lane-wise, but `System.Numerics` is **not used anywhere in Core today** (grep), and Mono in the Editor does not reliably accelerate it. | Up to ~2-4× on the arithmetic after #1, in .NET 8 only. | New dependency surface; two runtimes. Not first. | Moderate |

Out of scope but noted: the **box** path (`Transport` in a box) has no hoisting at all — direct `PotentialAt` with 2 cos + 1 sin per mode × 5 modes = 15 transcendentals per edge (CurrentField.cs:1347-1355; GridField.cs:3929-3935). The angle-addition rewrite there would be NR, not BI, because today's code evaluates cos(phase + φ + rate·t) directly.

## 3. Recommendation

**Do first, no ruling needed (all BI):** #1 + #2 together, with #4 and #6 folded in, and #5 behind a `bed == null` branch for the flat tanks. Expected: sampling 5.8 → ~1.5-2.5 ms, the phase 6.4 → ~2-3 ms, the run ~1.6-1.8× faster at full nursery load (est.). Verification is already in the codebase's style: hold the new path against the current one bit for bit over several instants and both floors (the StreamsHoistTests / BedGridTests pattern), and compare against the device's record (logbook/specs/card-transport-check/compare.py) if the card is free. Measure legs with `ProfileTransport` (GridField.cs:3223-3260) before and after — *after* the long simulation has finished, with nothing else running, per the timing rule. Then try #3 and keep it only if the profile shows the chain is the floor.

**Put to the owner, in full, as separate decisions:**
- #7 (regroup the eddy sum): a new realisation of every seed for roughly another 1.5× here and 3× on flat floors; conservation is untouched by construction. Recommend *after* #1-#2 have landed and been measured, so the owner sees what the bit-identical work bought before being asked to break reproducibility.
- #10 (advect every second step) and #11 (2 m snow cells): world rules with 2× and ~8× on this phase respectively; #11 interacts with the feeding kernel. Recommend asking only if #1-#2 fall short of the pace the nursery needs.

Not recommended: #8 (zero-stock skipping — the mixing pass defeats it within 25 s), #9 (no time structure to cache beyond #7), #13/#14 for now.
