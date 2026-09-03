# 0052 — The coarse step

*2026-09-03. Pre-registered before launch; results appended after. A validation, not a
round: does the world survive a coarser physics step, measured against a seed already run.
The owner's request of the day — "I do think the world will survive coarser steps; I'd love
to test it against a seed we already ran to see what the difference is."*

## What is being tested

Every run so far has integrated physics at dt = 0.01 s, fifty steps per 0.5 s metabolic
step, and the physics is where the wall-clock goes: 30,000 simulated seconds take five to
ten hours. The worlds of rounds 10–15 are drifting rigid clumps — no joints, no swimming —
so the argument that a coarser step "changes what is physically possible" (Ecosystem.cs's
own remark on the two clocks) may be true and irrelevant at once. The only honest test is
to run the same seed at a coarser step and measure the drift against the drift two runs of
the same world already have with no change at all, because PhysX is not bitwise
deterministic.

`EVOSIM_DT` now sets the step (Ecosystem.ConfigurePhysicsStep: the metabolic step stays
0.5 s, so dt must divide it; the header's `dt=` token and config.json's `physicsDtSeconds`
carry the value; 0.01 is every earlier run bit for bit).

## Design

Reference: `r13a-s2` (round 13 arm A, seed 2; logbook/0049), run to 30,000 s at dt 0.01.

| arm | dt | steps per metabolic step | what it measures |
|---|---|---|---|
| `r16dt-01` | 0.01 | 50 | **replay noise**: the same world twice with nothing changed |
| `r16dt-02` | 0.02 | 25 | a 2× coarser step |
| `r16dt-05` | 0.05 | 10 | a 5× coarser step |

Every other knob `r13a-s2`'s (clearance 1, sink 0.002 both fields, vent off, rolls 0.3 m/s in
30 m cells, excretion 0.01, 4 patches, area 100, ceiling 8,000, floor closes 3,000). Budget
15,000 s — enough to cover founding, the climb to the surface and the first larder cycle;
the reference's rows to 15,000 are the comparison. Wall 300 min.

**Compared, sample by sample against the reference and against `r16dt-01`:** `alive`,
`depth m`, `depth sd`, `J/m3 here`, `det deep`, `% on floor`, `mat blk`, `absorpt`,
`inherit`, `audit`; and the wall-clock seconds per 1,000 simulated seconds from each
report's timing, for the speed-up.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `dt=0.02` / `dt=0.05` / `dt=0.01`, `metabolic step 0.5 s`, and every other token equals `r13a-s2`'s (the config hash is Core-side and must be **identical** to `r13a-s2`'s — dt is not in it; `physicsDtSeconds` in the run-identity record `EvolutionRun.WriteRunIdentity` writes at the end is where it lives) | header line 3, run identity |
| V2 | `r16dt-01` is not row-identical to `r13a-s2` past t=0 *or* is — either is a result: identical means PhysX replays on this machine and the noise floor is zero | row diff |
| V3 | `floor` = 0 after t=3,100 in every arm | `floor` |
| V4 | audit 0.0000% every sample — a coarser step must not open the energy audit | `audit` |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the audit closes at every step** — the ledger is integrated per metabolic step, not per physics step, so dt cannot touch it | `audit` ≠ 0.0000% |
| M2 | **dt 0.02 is inside replay noise**: every compared column at t=5,000, 10,000 and 15,000 lies within the band spanned by `r13a-s2` and `r16dt-01` widened by 20% of that band's width, or by 10% of the reference value where the two references agree to better than that | the comparison table |
| M3 | **dt 0.05 drifts but survives**: founded (`floor` 0 after 3,100), no crash, `alive` within 25% of the reference at 15,000; depth and the larder may differ by more, and where they do the difference names the mechanism (drag integration, the rolls' Courant fraction, buoyancy) | `alive`, `**Ended:**` |
| M4 | **the speed-up is near-linear**: wall seconds per 1,000 simulated seconds fall by ≥ 1.7× at 0.02 and ≥ 3.5× at 0.05 (physics dominates; the economy and the fields are per metabolic step and do not shrink) | timing |

## The two-sided readings

- **M2 and M4 hold:** dt 0.02 becomes the screening step for every world without joints, with
  0.01 kept for confirmation runs and for any world where swimming is scored. Whether a pass
  under D063 may be claimed at 0.02 is the owner's call on the goal rule.
- **M2 fails, M3 holds:** the coarse step is a different world by an amount the table states;
  usable for screening only where the compared quantity is robust to it (population, the
  larder), never for depth-sensitive claims.
- **M1 fails:** the audit found a step-dependent term — a bug, since nothing in the ledger
  should see dt; fix before any other reading.
- **M3 fails (a crash or a founding failure at 0.05):** explicit drag at a coarse step is
  unstable for small bodies (the stability condition scales with dt × drag / mass) — 0.05 is
  out, and 0.02 stands or falls on M2.
- **V2 finds `r16dt-01` identical to `r13a-s2`:** the noise floor is zero on this machine, and
  M2's band collapses to the 10% rule.

## Launch

Three arms as workers free, after each worker is refreshed with the knob's build and
hash-checked (`Editor/EvolutionRun.cs`, `Ecosystem.cs`, `FluidEnvironment.cs`,
`PhenotypeBuilder.cs`). `scratch/launch-r16.ps1 -Dt 0.05 -Worker 7`. Results appended
below.

## Results

**`r16dt-05` — M3 falsified: dt 0.05 crashes at t≈3,600.** V1 held (only the `dt` token
differed; the config hash was identical to `r13a-s2`'s) and V3/V4 held to the last row.
Pace was the striking part: t=3,600 in three wall minutes with 540 alive, against ~30 min
for the same stretch at 0.01. Then the log filled with
`ArticulationBody.force assign attempt for 'Part00_n0' is not valid. Input force is { NaN,
NaN, NaN }`, a creature's height went non-finite, `World.Observe` refused it (its guard for
a diverged solver), and the run exited with return code 1 — no `**Ended:**` footer, the
report frozen at t=3,600. This is the pre-registered reading for M3 failing: explicit drag
at a coarse step is unstable for small bodies, and 0.05 is out. Before the crash the world
had already diverged qualitatively from the reference — the population rose to the surface
by t=2,100 where the reference held −15 m — but with the arm dead that comparison is moot;
0.02 stands or falls on M2 against `r16dt-01`.

*Instrument note.* The monitor's error alternation did not include `threw exception` /
`Exiting without the bug reporter`, so the crash surfaced only when the report was found
frozen 35 minutes later; both signatures and `non-finite` are in the alternation now. A
crashed run leaves no footer, so "no footer, process gone" is the third way an arm ends.

### Amendment: the drag limiter, pre-registered before the rerun

*Written 2026-09-03 after the crash, before `r16dt-05b` launched; `r16dt-02` was at
t≈6,400 and healthy, running at ~7.8× real time against ~1.1× for the dt 0.01 arms beside
it (not a like-for-like pace: 750 alive against 1,700).*

The crash is explicit-integration instability, not a limit of the idea: quadratic drag
applied once per step is stable only while a step's impulse is below the momentum it acts
on. `FluidEnvironment.Apply` now caps each body's drag impulse at that momentum (force ×
dt ≤ mass × relative speed; torque × dt ≤ smallest principal inertia × spin) — a step may
bring a body to rest relative to the water and no further, the semi-implicit treatment of
quadratic drag. The cap binds only when k·A·|v|·dt/m ≥ 1, and the report's footer now
prints `Drag impulses limited: N` so "never at dt 0.01" is checked per run rather than
assumed. Committed as the limiter; every worker needs a refresh before its next arm.

| # | prediction | falsified by |
|---|---|---|
| M5 | **the limiter is invisible at 0.01**: `r16dt-01` (and every dt 0.01 arm launched after the limiter) reports `Drag impulses limited: 0` | the footer |
| M6 | **0.05 survives with the limiter**: `r16dt-05b` reaches 15,000 s with V3/V4 held and a non-zero limiter count | the footer, `**Ended:**` |
| M7 | **stability and accuracy are separate**: `r16dt-05b` still lies outside `r16dt-01`'s band on depth by t=5,000 (the unlimited 0.05 arm had risen to the surface by t=2,100 where the reference held −15 m), while `r16dt-02` is inside it — if 0.05 lands inside the band once stable, the earlier divergence was the overshoot itself | the comparison table |

Reading: M6 holds and M7 holds → 0.02 is the screening step and 0.05 is a stress test only.
M6 holds and M7 fails → 0.05 is usable for screening and the day's speed-up is ~5×. M6 fails →
the instability is not only in our drag (the articulation solver itself, or buoyancy), and
the coarse step stops at 0.02.

### `r16dt-05b` — stable, fast, and a different world

*Ended at budget: 15,000 s in 23.3 min wall (10.7× real time; the reference needed ~157 min
for the same stretch, so ~6.7× faster). The limiter bound 1,768,013 times — about 0.6% of
body-steps — and no force went non-finite. V1 and V3 held.*

| # | verdict | evidence |
|---|---|---|
| M6 | **held** — 0.05 survives with the limiter | budget reached, no NaN, floor 0 after 1,100 |
| M4 | **held** — near-linear speed-up | 6.7× at 0.05 against a 3.5× bar |
| M1 / V4 | **falsified** — the audit opened | `audit` 0.0057–0.0101% from t=7,600 to the end (0.0000% at every row before); the reference and `r16dt-02` read 0.0000% throughout |
| M7 | **held so far** — the divergence is not the overshoot | with the drag stable, the population still rose to the surface by t=2,600 (+0.2 m, `depth sd` 0.7–1.2 m) where the reference held −11 to −14 m with `depth sd` 7–8; the field at the population's depth read 0.2–0.4 J/m³ against 3.5–6.4; refusals 3–4× the reference's. `r16dt-02` tracks the reference on all of these (below) |

### `r16dt-02` — tracks the reference, at 3× the pace

*Ended at budget: 15,000 s in 50 min wall (5× real time; the reference's average was 1.6×,
so ~3.1× faster). Launched before the limiter existed, so its drag is exactly the
reference's — a cleaner comparison than a limited arm would have been. V1, V3, V4 held;
`audit` 0.0000% at all 150 rows.*

| t | `alive` 0.02 / ref | `depth m` | `depth sd` | `J/m3 here` | `det deep` | `mat blk` |
|---|---|---|---|---|---|---|
| 5,100 | 691 / 779 | −14.1 / −13.1 | 6.7 / 7.1 | 6.6 / 5.2 | 2.3 / 1.8 | 13,026 / 28,312 |
| 10,100 | 1,425 / 1,472 | −10.9 / −11.2 | 8.1 / 8.1 | 4.9 / 4.4 | 3.6 / 2.8 | 78,076 / 65,445 |
| 15,000 | 1,720 / 1,661 | −11.1 / −12.0 | 8.1 / 7.7 | 7.3 / 6.1 | 4.8 / 3.9 | 162,236 / 76,682 |

Population within 4–11%, depth within 1 m, spread within 0.5 m, the larder within 25%, the
deep field 20–30% higher, refusals within 2× (a per-window count that swings 5–10× between
adjacent samples in every arm). No inherited line in either. Whether these differences are
the step or the seed's own replay noise is M2, and it waits on `r16dt-01`, launched on the
same worker as this arm ended. If the repeat lands inside the same distances, 0.02 is the
screening step.

### `r16dt-01` — the replay-noise floor, and a limiter that was not invisible

*Ended at budget: 15,000 s in 108 min wall (2.3× real time). Header identical to
`r13a-s2`'s, config hash included. V2: the t=500 row differs from the reference in the
last digit (70 vs 71 alive), so PhysX does not replay bit for bit on this machine and the
noise floor is real but small. Audit 0.0000% throughout.*

| t | `alive` ref / repeat / **0.02** | `depth m` | `depth sd` | `J/m3 here` | `det deep` | `mat blk` |
|---|---|---|---|---|---|---|
| 5,100 | 779 / 766 / **691** | −13.1 / −13.7 / **−14.1** | 7.1 / 6.6 / **6.7** | 5.2 / 5.3 / **6.6** | 1.8 / 1.9 / **2.3** | 28k / 22k / **13k** |
| 10,100 | 1,472 / 1,448 / **1,425** | −11.2 / −11.7 / **−10.9** | 8.1 / 7.7 / **8.1** | 4.4 / 4.7 / **4.9** | 2.8 / 2.9 / **3.6** | 65k / 81k / **78k** |
| 15,000 | 1,661 / 1,658 / **1,720** | −12.0 / −12.1 / **−11.1** | 7.7 / 7.4 / **8.1** | 6.1 / 6.2 / **7.3** | 3.9 / 4.1 / **4.8** | 77k / 84k / **162k** |

| # | verdict | evidence |
|---|---|---|
| M2 | **held for demography, falsified for the fields** — by the pre-registered band | population within 4%, depth within 1 m and spread within 0.7 m at every sample (inside the 10% rule where the two references agree); the larder 18–25% above both references at 5,100 and 15,000, the deep field 17–30% above, refusals 2× at 15,000 — outside the band. 0.02 is a screening step for *whether lines form and persist*, not for reading a larder to better than a quarter |
| M5 | **falsified** — the limiter was not invisible at 0.01 | `Drag impulses limited: 1,229,232,583` in 1.5 M steps — it bound on most body-steps. The world still matched the reference to within the noise floor on every column, so its effect was negligible, but the design was wrong: it capped the whole torque against the whole angular momentum, and a still body has none, so every drag torque that linear motion produces through off-centre panels was zeroed. `r16dt-05b`'s 1.77 M binds are mostly the same artefact |

**The limiter, corrected (committed after this arm).** Only the component of force or
torque that *opposes* the existing motion can overshoot and reverse it, so only that
component is capped, against the momentum along it; the rest passes untouched. A still body
is then untouched entirely. M5 is re-asked of the next dt 0.01 arm launched with the
corrected build: its footer must read 0, or a number small enough to name.

### `r16dt-01c` — the corrected limiter is invisible, and the noise floor is wider than one repeat

*5,000 s at dt 0.01 with the corrected limiter, 10.8 min wall. Footer: `Drag impulses
limited: 68` in 500,000 steps over ~600 bodies — M5 **held** as re-asked (a number small
enough to name). Audit 0.0000%.*

But at t=5,000 it holds 957 alive against the reference's 779 and the first repeat's 747.
Sixty-eight capped impulses cannot do that; what can is chaos in founding (the same seed
produced 93 alive at t=600 in the reference and 40 in the dt 0.05 arm) amplified by
whatever PhysX's multithreaded solver does differently under a different machine load.
So the replay-noise floor is not the 4% one pair of runs suggested; on population at
t=5,000 it is at least 25%, and M2's band has to be drawn from more than one repeat. Two
more 5,000-s repeats at 0.01 (`r16dt-01d`, `-01e`, ~11 min each) are queued to draw it;
0.02 and 0.05b are then re-read against that band at t=5,000, which is where every arm
overlaps. Until then, M2's verdict above stands as provisional: 0.02's population and
depth were inside the narrow band already, and a wider band can only move the fields'
verdict toward "inside".

### `r16dt-01d` — PhysX replays bit for bit, so "noise" was never noise

*5,000 s at dt 0.01, the same corrected-limiter build as `r16dt-01c`. Its t=5,000 row is
**identical** to `r16dt-01c`'s in every column: 957 alive, −8.9 m, spread 8.57 m, larder
2.3267, deep 1.7839, 56,488 refusals.*

So V2's finding is reversed: PhysX does replay exactly on this machine when the build is
the same, and the "noise floor" measured between the reference and `r16dt-01` was the
first limiter's 1.2 billion binds, not the solver. The consequences, in order:

- **Every difference between the three 0.01 arms is the build, amplified by chaos.** The
  corrected limiter's 68 capped impulses moved seed 2 from 779 alive at −13 m in 5 J/m³ to
  957 at −9 m in 2.3 J/m³ by t=5,000. A perturbation that small is a butterfly, and the
  world is a chaotic system: any change to any per-step term gives a different realisation
  of the same seed. Per-seed comparison therefore cannot separate "the step changed the
  world" from "the step changed the realisation" — M2's band, as pre-registered, is
  unanswerable per seed. The honest comparison of two steps is distributional: N seeds at
  each, spread against spread.
- **On that reading, 0.02 is inside the butterfly.** Its deviations from the reference
  (population −15% to +4%, depth 1 m, larder +25%) are smaller than the corrected
  limiter's own (population +23%, depth 4 m, larder −55%) at the same step. dt 0.02
  changes this world no more than sixty-eight capped impulses do. That is the strongest
  support a per-seed test can give it, and it is the reading 0052 closes on: **0.02 is
  the screening step**, at ~3× the pace, with 0.01 kept for confirmation runs and for any
  world where a swimming or contact question is scored.
- **The limiter is now gated to steps coarser than 0.01.** With replay exact, the
  historical record (every run before today, all at 0.01) is reproducible bit for bit
  under its own config hash only if 0.01 stays untouched, so the limiter engages only above
  it. At 0.02 it engages; whether it binds there at all is read from the footer of the next
  0.02 arm.
- `r16dt-01e` (queued before this was understood) is redundant and confirms determinism a
  second time; it is left to finish.

Two things need naming. **The audit.** Nothing in the ledger reads the physics step, so a
step-dependent leak of a hundredth of a percent is a bug by construction — the
pre-registered reading. It opened at t=7,200, as the population went still in the film
(`mean m/s` 0.0001 → 0, `max m/s` 0.02 → 0.002), with **zero jointed creatures and zero
joint work** throughout, so the one term that crosses the two clocks by design — work
integrated per physics step — is not the path. Round 13's surface populations at +0.4 to
+1.0 m audited clean at 0.01, so a body centre above the waterline is not the path on its
own either. The leak is steady (0.006–0.010%), not growing, which reads as a one-off
mis-accounting at t≈7,200 rather than a per-step term. Not chased today: 0.05 is out on M7
regardless, and the bug is filed here for whoever next runs the audit under a coarse step. **The depth.** A stable 0.05 puts the whole population in the film that
0.01 keeps at −12 m. The buoyancy–drag balance that sets terminal sink rate is integrated
per step, and at five times the step the balance lands somewhere else; the limiter, which
binds 0.6% of the time, is part of that. Whether 0.02 shares any of it is `r16dt-02`
against `r16dt-01`.

### `r16dt-02b` — the gated limiter at 0.02 engages, and it is a butterfly too

The rerun of the 0.02 arm on the gated build, same seed, same config hash. Footer:
**432 drag impulses limited** in 750,000 physics steps, 3,383 births, 56.9 min wall
(4.4× real time, on a machine running five arms; `r16dt-02` did 50 min at 5× on a quieter
one). Audit 0.0000% at every row, zero jointed creatures and zero joint work throughout —
the same jointless world as the other arms of this seed.

The limiter engages at 0.02, so `r16dt-02b` is not row-identical to `r16dt-02`. Where the
two part is the instructive part. The first difference is at t=2,200, in `rise m` at the
fourth decimal — one capped impulse on one body, somewhere before that row. The first
difference in a population count is at t=3,000 (244 against 246), and the mean depth is
half a metre apart from t=3,300. From there the two 0.02 worlds are two realisations:

| t | `r16dt-01` (0.01, reference) | `r16dt-02` (0.02, ungated) | `r16dt-02b` (0.02, gated, 432 binds) |
|---|---|---|---|
| 5,000 | 747 alive · −13.9 m · 5.2 J/m³ | 665 · −14.3 m · 6.5 | 608 · −14.6 m · 6.3 |
| 10,000 | 1,442 · −11.8 m · 4.7 | 1,412 · −10.9 m · 4.9 | 1,370 · −15.0 m · 8.1 |
| 15,000 | 1,658 · −12.1 m · 6.2 | 1,720 · −11.1 m · 7.3 | 1,719 · −14.9 m · 6.7 |

The gated arm sits 4 m deeper than the ungated one for the second half of the run, with a
larder up to 70% richer at t=10,000, and ends within one creature of it in population.
That deviation — between two arms at the *same* step, separated by 432 capped impulses —
is as large as, and in depth larger than, the deviation between 0.02 and 0.01. It is the
same finding as `r16dt-01c` against `r16dt-01` at the finer step: a handful of altered
impulses is a butterfly, and the butterfly's wingspan in this world is about ±20% of the
population, 4 m of depth and half the larder. 0.02's distance from the reference is inside
that wingspan, from both sides now.

Two practical readings. First, the limiter *does* bind at 0.02 — 432 times in 15,000 s
across ~1,700 bodies, one impulse in roughly three million — so it is not a no-op there,
and it cannot be dropped from the 0.02 build on the grounds that it never fires. Second,
every 0.02 arm from here runs on the gated build, replays bit for bit under it, and is
comparable row for row only with other gated 0.02 arms; `r16dt-02` (ungated) is the only
arm of its kind and stays in the record as that.

## Verdict

Closed 2026-09-03, eight arms (`r16dt-05`, `-05b`, `-02`, `-02b`, `-01`, `-01c`, `-01d`,
`-01e`; the last three 5,000-s repeats). What 0052 established, in the order it matters:

1. **PhysX replays bit for bit on this machine under one build** (`r16dt-01c` ≡ `-01d` ≡
   `-01e`). There is no replay noise; every difference between two arms of one seed is a
   change in the build or the config, amplified by chaos.
2. **Any per-step change is a butterfly** — 68 capped impulses at 0.01, 432 at 0.02 — and
   the butterfly's wingspan is ±20% population, 4 m depth, half the larder by t=5,000. A
   per-seed A/B on anything that touches the physics loop cannot separate the change from
   the realisation; M2 as pre-registered was unanswerable per seed, and the honest test of
   a step is distributional, N seeds against N seeds.
3. **0.02 is the screening step.** Its deviations from 0.01 are inside the wingspan, from
   both the ungated and the gated side; it runs at ~3× the pace of 0.01 (50–57 min against
   157 for 15,000 s). 0.01 stays the confirmation step and the step for any world in
   which swimming or contact is scored — every run before today is replayable only at 0.01,
   which is why the limiter is gated above it.
4. **0.05 is out.** It crashes on the explicit drag without the limiter (M3), and with it
   the population migrates into the surface film that 0.01 keeps at −12 m (M7's reading),
   with an audit that opens to 0.006–0.010% from t≈7,200 in a jointless, workless world —
   filed above as a bug, not chased.

D069's fourth item is settled on these terms; the CLAUDE.md gotcha on determinism carries
the operational rule.
