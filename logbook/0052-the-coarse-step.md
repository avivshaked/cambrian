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
