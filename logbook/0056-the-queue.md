# 0056 — The queue

*2026-09-04. A diagnosis first, then a pre-registration. Why a stomach with four times a
child's price in reserve had no children: the world breeds oldest-first, and in a
matter-starved layer the oldest solvent body takes the matter every step.*

## The diagnosis

0055 closed with the free matter pool at a tenth of the stock and the stomachs sharing
the leaves' band, losing the contest for each unit that arrives. Before screening any
lever that enlarges the pool, the contest itself was read from the code, following the
CLAUDE.md rule about reading loop bounds before building on a mechanism.

`World.Reproduce()` walks `_living` in list order once per metabolic step; each parent
above its breeding gate conceives, and `Conceive` draws the child's matter from the
parent's own layer and patch, refusing when the layer's stock is short. `_living` is
birth-ordered: births are appended, and death removes in place. So when a layer's stock
covers one child and ten solvent bodies want it, the oldest gets it, every step, and a
younger body breeds only when everyone older in its layer is dead or broke.

Nothing in DESIGN.md specifies an order. The order is an artefact of the list, which
makes this a fault by CLAUDE.md's rule — the engine doing something the design did not
ask for — rather than a world rule the record ever chose.

**Measured**, from `lineage.jsonl` (`scratch/parent-age.py`; a parent's age at each
regular birth, growth phase 3,000–10,000 s against the plateau after 10,000 s):

| arm | step | phase | births | median parent age | share of births to parents > 3,500 s |
|---|---|---|---|---|---|
| r18x-s1 | 0.01 | growth | 2,216 | 376 s | 0% |
| r18x-s1 | 0.01 | plateau | 3,089 | **3,352 s** | **48%** |
| r18x-s4 | 0.01 | growth | 2,502 | 558 s | 3% |
| r18x-s4 | 0.01 | plateau | 2,425 | **4,536 s** | **62%** |
| r19m0-s1 | 0.02 | growth | 1,422 | 248 s | 6% |
| r19m0-s1 | 0.02 | plateau | 1,052 | 610 s | 26% |
| r19m-s4 | 0.02 | growth | 1,847 | 545 s | 1% |
| r19m-s4 | 0.02 | plateau | 2,051 | 1,168 s | 7% |

A lifetime is 3,000 s (`EVOSIM_SENESCENCE`). In the reference world's plateau half or
more of all conceptions go to bodies past a lifetime — the front of the queue — and
during growth, when matter is not short, the median parent is a few hundred seconds
old. The two fast-step arms sit between: the surface-film control gets more matter to
its layer and queues less; the faster-sink arm queues less still. Absorptive children in
the plateau follow the same shape (r18x-s1: median parent age 2,874 s; r18x-s4: 3,804 s).

The consequence for the goal: a stomach born by mutation at t=16,000 into a saturated
layer stands behind every leaf older than itself, and by the time it reaches the front
its own senescence has raised its upkeep past its income. Round 18's failing seed is
that sentence with numbers (0054). The consequence for the producers: selection in every
plateau since D065 has been for outliving the queue, not for fecundity — a pressure the
design never intended and the record never read.

## The rule under test

`ConceptionOrder` (`EVOSIM_CONCEPTION_ORDER`): `age`, today's walk, the default and
bit-identical; `shuffled`, a fresh uniformly random permutation of the living each step
from a dedicated seeded stream, so same seed and config replay. The fix is behind a knob
so the historical record replays and so adoption into the reference world is the owner's
ruling on a measured result, not a silent change (D072).

## The arms

Reference world (round 18's, exudation 0.15, sinks 0.002) at dt 0.02, 20,000 s:
`r20q-s1` and `r20q-s4` with `shuffled`; `r20q0-s4`, the control with `age`, because
0055's `r19m0-s1` already is seed 1's control at this step and seed 4 has none.
Launcher `scratch/launch-r20.ps1`; workers refreshed to the build and launched with
`-ExpectSimHash`; ≤ 3 concurrent.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `conception shuffled` (control `conception age`), `dt=0.02`, `sink 0.002 m/s, matter 0.002 m/s`, `exudation 0.15`; every other token equals round 18's | header line 3 |
| V2 | `floor` = 0 after t=3,100; audit 0.0000% every sample | `floor`, `audit` |
| V3 | manifests `status ended`, `reason budget`, `simHash` as launched | `run.json` |
| V4 | the control's parent-age profile reproduces the table's shape (median > 2,000 s in the plateau) | `scratch/parent-age.py` |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the queue is gone**: median parent age in the plateau < 1,000 s in both `shuffled` arms, and < half the control's | `scratch/parent-age.py` |
| M2 | **the stomachs breed**: in each `shuffled` arm with a stomach population at t=10,000, a connected absorptive clade ≥ 10 at 20,000 s, stable through the last 6,000 s, larger than the same seed's control clade | `scripts/clade-score.ps1` |
| M3 | **the refusals do not fall** — the demand is unchanged: `mat blk` per window within a factor of two of the control's at t > 10,000 | `mat blk` |
| M4 | **the producers persist, younger**: `photo inh` ≥ 1,000 to the end, no ceiling, and the mean age column below the control's after t=10,000 | `photo inh`, `age`, `**Ended:**` |
| M5 | **the plateau does not move**: `alive` at 20,000 s within the wingspan (±20%) of the control's | `alive` |

## The two-sided readings

- **M1–M2 hold:** the queue was the stomachs' refusal, and the fix goes to the owner for
  adoption into the reference world (D072), then a confirmation at 0.01, five seeds, under
  the amended goal — the first pass would then be one without the queue.
- **M1 holds, M2 fails:** the contest was fair and the stomachs still lost — matter is
  scarce enough that an equal chance is not enough, and 0055's levers on the pool are
  next after all.
- **M1 fails:** the permutation is not reaching the walk (prove the knob reached the
  loop before anything else — CLAUDE.md's "identical numbers" rule), or the queue is not
  where the age skew comes from.
- **M5 fails upward:** a fair contest breeds younger and smaller and the matter builds
  more bodies — the plateau was partly the queue; read `mat locked` and the ceiling.
- **M4 fails on `photo inh`:** the producers needed the queue (age as a proxy for a
  proven body); an unintended pressure that was nonetheless load-bearing, and a real
  finding.

## Launch

Launched 2026-09-04 ~13:40 on workers 2, 3 and 4, refreshed to commit `23a6bd8` (the
conception-order build, 484 tests) and launched with `-ExpectSimHash f99d69b7952a4285`;
every manifest reads that `simHash`, `gitCommit 23a6bd8`, `gitDirty false`, `status
running`. Headers verified: `conception shuffled` on `r20q-s1` and `r20q-s4`,
`conception age` on `r20q0-s4`, `dt=0.02`, `sink 0.002 m/s, matter 0.002 m/s`,
`exudation 0.15`. Monitor running.

**`r20q-s1` censored at t=15,345 — a fault, not a result.** The fluid model handed
PhysX a NaN force and torque for one part of a jointed body (`FluidEnvironment.Apply`,
`Part02_n3` of creature 3075), the body's height went non-finite and `World.Observe`
refused it, as it should. The manifest reads `status error` with the exception as its
`ending` — the first run to record its own crash — though the error path omits the
footer's facts (`physicsSteps 0`, `dragImpulsesLimited 0` are placeholders, not readings).
Censored under the owner's rule (error and fault, not merit). The same seed and config
would replay the same crash bit for bit, so seed 1 is replaced by **seed 2** — a seed
with an inherited stomach population in round 18 (91 at 30,000 s) — as `r20q-s2`
(shuffled, worker 2) with its own control `r20q0-s2` (age, worker 5, refreshed and
hash-checked). Four arms concurrent. The divergence itself is filed in HANDOFF as a bug
to chase: a NaN drag force at the 0.02 step in a world that was 1.7% jointed, with the
drag limiter present — the first physics divergence in a scored run.

Results appended below.
