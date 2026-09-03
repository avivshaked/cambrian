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
| V1 | header carries `dt=0.02` / `dt=0.05` / `dt=0.01`, `metabolic step 0.5 s`, and every other token equals `r13a-s2`'s (the config hash is Core-side and must be **identical** to `r13a-s2`'s — dt is not in it; `physicsDtSeconds` in config.json is where it lives) | header line 3, config.json |
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
