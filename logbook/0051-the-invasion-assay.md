# 0051 — The invasion assay

*2026-09-03. Pre-registered within the hour of launching the first arm and before its
inoculation fired at t=5,000; results appended after. Round 15: not a goal round — a
screen. The owner's ruling of the same day: stop learning one number a day from a ten-hour
physics run when the number can be measured in two hours or computed in a second.*

## Why an assay, not another round

Rounds 12, 13 and 14 all asked the same question — does an absorptive line breed to
replacement — and each answered it by t≈10,000 with a single mutant that appeared by
chance, in a run that had to go to 30,000 for the goal rule. One lucky mutant is a sample
of one; whether it leaves a line is as much demographic luck as fitness. The quantity that
decides the goal is the stomach's **invasion fitness when rare**: the per-capita growth of
a small absorptive population dropped into an established producer world. The run already
has the instrument for exactly this (`EVOSIM_INOCULATE`, D-entry on inoculation; `Inoculate`
admits N verbatim copies of a genome at a chosen time and depth, seeded from the world's
own stream so the run replays). Fifty copies give statistics one mutant cannot, and 7,000 s
after inoculation is two to three lifetimes at senescence 3,000.

This runs in parallel with round 14 ([0050](0050-the-stomachs-gearing.md)), whose seed-1
arms are past t=10,000: `r14c5-s1` built a line of seven inherited by t=7,000 and lost it
by t=10,700; `r14c10-s1` had no line at t=5,000. Round 14 continues under the owner's
sequential rule (seeds 3–5 only if a line appears in seeds 1–2).

## Design

| arm | world | clearance | inoculum |
|---|---|---|---|
| `r15i-c10` | round 13 arm A, seed 2 (the seed whose population sat at −12 m in 6–9 J/m³ from t=12,000) | 10 | 50 copies at t=5,000, −12 m |
| `r15i-c1` | same | 1 (the control — the world in which the stomach failed) | same |
| `r15i-c5` | same | 5 | same |

**The inoculum** is the first genome carrying an absorptive node in `r13a-s2`'s t=17,000
snapshot: a **single-node pure stomach** (one absorptive part, no leaf; brood 1, endowment
103 J), SHA-256 `342f472a7dda…`, kept at `scratch/inoculum-r13a-s2-t17000.json` on this
machine (run data, not committed; the header records the hash). It is the body the world
itself produced, not one we designed. Budget 12,000 s, wall 300 min; every other knob
`r13a-s2`'s (sink 0.002 both fields, vent off, rolls 0.3 m/s in 30 m cells, excretion 0.01,
4 patches, area 100, ceiling 8,000, floor closes 3,000, mutation 0.005). Launch order c10,
c1, c5 as workers free, ≤ 5 arms on the machine with round 14's.

**Measurement**, from `lineage.jsonl` (births carry `k`: `f` floor, `r` reproduction, `i`
inoculation; `p` parent id; `abs` expressed; deaths carry `e:"d"`):

- **R0 observed**: children per inoculant, and children per member over the whole inoculant
  lineage (descendants = every birth whose parent chain reaches an `i` row).
- **Lineage size** by generation and by time; alive at t=12,000.
- **Inoculant lifetimes** (death time − 5,000).
- **Expressed share**: fraction of the lineage's children with `abs = 1`.
- `mat blk` in patch 0 around the inoculation, to see whether the stomach's refusals differ
  from the producers'.

A permanent script, `scripts/lineage-invasion.ps1`, does this (the owner's rule: recurring
analyses become scripts).

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `clearance N`, `inoculate 50 @ 5000 s, 12 m, genome 342f472a7dda`, `sink 0.002 m/s, matter 0.002 m/s`, `vent off`; every other token equals `r13a-s2`'s | header line 3 (c10: held) |
| V2 | the assay fired: 50 `k:"i"` births at t≈5,000, and the report's `absorpt` jumps by ~50 at the next sample | lineage, `absorpt` |
| V3 | `floor` = 0 after t=3,100 (the world is established before the inoculation) | `floor` |
| V4 | audit 0.0000% every sample | `audit` |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the stomach cannot invade at clearance 1**: R0 observed < 1 over the lineage, and the lineage is extinct or ≤ 5 alive at t=12,000 | `r15i-c1` lineage |
| M2 | **it can at clearance 10**: R0 > 1, ≥ 50 descendants born by t=12,000, ≥ 10 alive at t=12,000 | `r15i-c10` lineage |
| M3 | **the dose orders it**: per-capita rate c10 > c5 > c1 | the three lineages |
| M4 | **expression is not the gate**: ≥ 90% of the lineage's children read `abs = 1` (a one-node stomach has little to mutate into at 0.005) | `abs` share |
| M5 | **the assay agrees with the calculator**: the ledger tool's R0 for this genome at −12 m and the observed detritus density is on the same side of 1 as the assay in each arm | `scripts/ledger.ps1` output vs the lineages |

## The two-sided readings

- **M2 holds and round 14's c10 arms still form no line:** fitness is fine and establishment
  from a single mutant is the bottleneck — demographic stochasticity, not economics. The
  levers are then supply (a founder guild, a higher absorptive share among founders) and are
  world rules for the owner; the goal rule's silence on how the line arises matters here.
- **M2 fails:** a stomach that out-earns a leaf on paper still cannot invade — the world's
  ledger and the calculator's disagree, or something outside the ledger (matter refusals in
  the stomach's patch, the dark excursion, shading) is binding. Compare the calculator's R0
  with the observed lifetimes and children first; that comparison is the whole point of
  running both.
- **M1 fails (invasion at clearance 1):** the world's own stomach could always invade and the
  mutation route was the bottleneck — a mixotroph mutant carrying a leaf *and* a stomach is a
  worse body than a pure stomach, and mutation never produces the pure one in the light.
  Then round 14's knob was unnecessary and the answer is in how absorptive bodies arise.
- **M4 fails:** the mutation operator turns stomachs back into leaves faster than a line can
  grow; that is a Core question (mutation rates per cell type), not a world one.

## Results

*Appended when the arms end.*
