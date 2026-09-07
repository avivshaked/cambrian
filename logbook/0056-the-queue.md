# 0056 — The queue

**2026-09-04**  ·  a diagnosis, then a screen for D072 · pre-registered before launch, results appended after

A stomach with four times a child's price in reserve had no children, and the reason was a
list. The world breeds oldest first, because reproduction walks the living in birth order,
and in a matter-starved layer the oldest solvent body takes the matter every step. Removing
the queue looked like the obvious fix. It cost the stomachs in both seeds it was tried on,
because the queue was the one thing in this world that turned an energy advantage into a
reproductive one.

## The diagnosis

0055 closed with the free matter pool at a tenth of the stock, and the stomachs sharing the
leaves' band and losing the contest for each unit that arrives. Before screening any lever
that enlarges the pool, the contest itself was read from the code, following the CLAUDE.md
rule about reading loop bounds before building on a mechanism.

`World.Reproduce()` walks the living once per metabolic step, in the order the list holds
them. Each parent above its breeding gate conceives, and `Conceive` draws the child's matter
from the parent's own layer and patch, refusing when the layer's stock is short.

The list `_living` is birth-ordered: births are appended, and death removes in place. So
when a layer's stock covers one child and ten solvent bodies want it, the oldest gets it,
every step. A younger body breeds only when everyone older in its layer is dead or broke.

Nothing in DESIGN.md specifies an order. The order is an artefact of the list rather than a
world rule anyone ever chose. That makes it a fault by CLAUDE.md's rule about the engine
doing what the design did not ask for.

The measurement comes from `lineage.jsonl`, read by `scratch/parent-age.py`. It takes a
parent's age at each regular birth, in the growth phase from 3,000 to 10,000 s and in the
plateau after 10,000 s.

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

A lifetime is 3,000 s, set by `EVOSIM_SENESCENCE`. In the reference world's plateau, half or
more of all conceptions go to bodies past a lifetime, which is the front of the queue.
During growth, when matter is not short, the median parent is a few hundred seconds old.

The two fast-step arms sit between. The surface-film control gets more matter to its layer
and queues less, and the faster-sink arm queues less still. Absorptive children in the
plateau follow the same shape, at a median parent age of 2,874 s in the first seed and
3,804 s in the fourth.

The consequence for the goal is a sentence about age. A stomach born by mutation at t=16,000
into a saturated layer stands behind every leaf older than itself. By the time it reaches
the front, its own senescence has raised its upkeep past its income. Round 18's failing seed
is that sentence with numbers (0054).

The consequence for the producers is a selection pressure nobody wrote. Every plateau since
D065 has selected for outliving the queue rather than for fecundity, and the design never
intended it.

## The rule under test

The rule under test is a conception order, held behind a knob.

| setting | what it does |
|---|---|
| `age` | today's walk, the default, bit-identical to every run before it |
| `shuffled` | a fresh uniformly random permutation of the living each step, from a dedicated seeded stream, so the same seed and config replay |

The knob is `EVOSIM_CONCEPTION_ORDER` and the property behind it is `ConceptionOrder`. Two
things follow from keeping the fix behind a knob. The earlier runs still replay, and
adoption into the reference world stays the owner's ruling on a measured result rather than
a silent change (D072).

## The arms

The reference world is round 18's, at exudation 0.15 and sinks 0.002, run at dt 0.02 for
20,000 s.

| arm | order | role |
|---|---|---|
| `r20q-s1` | shuffled | seed 1 |
| `r20q-s4` | shuffled | seed 4 |
| `r20q0-s4` | age | seed 4's control; seed 1's control is 0055's `r19m0-s1` at this step |

The launcher is `scratch/launch-r20.ps1`. Workers were refreshed to the build and launched
with `-ExpectSimHash`, at most 3 concurrent.

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

- If M1 and M2 hold, the queue was the stomachs' refusal, and the fix goes to the owner for
  adoption into the reference world (D072). A confirmation at 0.01 over five seeds under the
  amended goal would follow, and the first pass would then be one without the queue.

- If M1 holds and M2 fails, the contest was fair and the stomachs still lost. Matter is then
  scarce enough that an equal chance is not enough, and 0055's levers on the pool are next
  after all.

- If M1 fails, either the permutation is not reaching the walk, or the queue is not where the
  age skew comes from. Prove the knob reached the loop before anything else, under
  CLAUDE.md's rule about identical numbers.

- If M5 fails upward, a fair contest breeds younger and smaller and the matter builds more
  bodies. The plateau was then partly the queue, and `mat locked` and the ceiling say how
  much of it.

- If M4 fails on `photo inh`, the producers needed the queue, with age as a proxy for a proven
  body. That would be an unintended pressure that was load-bearing all the same, and a real
  finding.

## Launch

Launched on 2026-09-04 at about 13:40 on workers 2, 3 and 4, refreshed to the
conception-order build at 484 tests.

| what | value |
|---|---|
| commit | `23a6bd8` |
| launch guard | `-ExpectSimHash f99d69b7952a4285` |
| every manifest | that `simHash`, `gitCommit 23a6bd8`, `gitDirty false`, `status running` |
| header, treatment | `conception shuffled` on `r20q-s1` and `r20q-s4` |
| header, control | `conception age` on `r20q0-s4` |
| header, all | `dt=0.02`, `sink 0.002 m/s, matter 0.002 m/s`, `exudation 0.15` |

A monitor runs over the arms.

The first arm was censored at t=15,345, for a fault rather than a result. The fluid model
handed PhysX a NaN force and torque for one part of a jointed body, from
`FluidEnvironment.Apply`, and the part was `Part02_n3` of creature 3075.

The body's height went non-finite and `World.Observe` refused it, as it should.

The manifest reads `status error`, with the exception as its `ending`. That is the first run
to record its own crash.

The error path omits the footer's facts. Both `physicsSteps 0` and `dragImpulsesLimited 0`
are placeholders rather than readings.

The arm is censored under the owner's rule, as an error and a fault rather than merit. The
same seed and config would replay the same crash bit for bit. Seed 1 therefore gave way to
seed 2, which had an inherited stomach population in round 18, at 91 alive at 30,000 s.

The replacement is `r20q-s2`, shuffled, on worker 2, with its own control `r20q0-s2` on age,
worker 5, refreshed and hash-checked. From there the round ran four arms at once.

The divergence itself is filed in HANDOFF as a bug to chase. It is the first physics
divergence in a scored run. The fault was a NaN drag force at the 0.02 step, in a world that
was 1.7% jointed, with the drag limiter present.

Results are appended below.

## Results

Four arms reached budget, with manifests reading `status ended` and `reason budget`.

Checks V1 to V3 held: headers as pre-registered, `floor` 0 from t=3,100, audit 0.0000%
throughout. V4 held as well, since both age controls reproduce the queue. Their median
parent ages in the plateau are 4,318 s and 4,632 s, with 52 to 54% of births going to bodies
past 3,500 s.

| arm | order | depth at end | alive | deaths by 20,000 | absorpt | largest clade at end · min last 6,000 s · recent births | median parent age, plateau | `mat blk` mean/window t > 10,000 | mean age at end |
|---|---|---|---|---|---|---|---|---|---|
| r20q-s2 | shuffled | −11.4 m | 1,820 | 2,100 | 7 | 7 · 7 · 0 → **fail** | **2,117 s** (29% > 3,500) | 178,000 | 7,231 s |
| r20q0-s2 | age | −1.4 m | 1,801 | 1,778 | 135 | 120 · 81 · 17 → pass | 4,318 s (52%) | 257,000 | 9,364 s |
| r20q-s4 | shuffled | −0.9 m | 1,811 | 1,471 | 189 | 185 · 134 · 21 → pass | **852 s** (23%) | 187,000 | 8,912 s |
| r20q0-s4 | age | −0.9 m | 1,774 | 2,764 | 234 | 227 · 200 · 54 → pass | 4,632 s (54%) | 211,000 | 5,889 s |

Seed 2's stomach line under the shuffled order rose to 81 at 6,000 s and then fell without
interruption. It read 45 at 10,000, 18 at 16,000 and 7 at the end, with no inherited birth
in the last 20 samples.

The file `absorptive.jsonl` says why. From t=10,000 the living stomachs' mean net was
negative, between −0.04 and −0.10 W. The field at their depth rose over the same span, from
2.7 to 6.1 J/m³. Food was not short.

The stomachs were old. Senescence had raised their upkeep past their income, and they were
not being replaced. Under a fair draw, an old solvent stomach has one chance in a hundred
and eighty of the layer's next unit. Under the age order the same stomach, being the oldest
solvent body in its layer, had the next unit outright.

Here is how the predictions came out.

| # | prediction | result |
|---|---|---|
| M1 | median parent age < 1,000 s in the plateau and < half the control's | **half held**: 852 s and 2,117 s against 4,632 s and 4,318 s — less than half the control's in both, under 1,000 in one. The queue is gone; the shuffled worlds still breed older than growth-phase worlds because their bodies are older |
| M2 | a clade ≥ 10, stable, larger than the same seed's control's | **falsified in both**: 7 against 120 (seed 2), 185 against 227 (seed 4) |
| M3 | refusals within a factor of two of the control's | held (0.69× and 0.89×) |
| M4 | producers persist, younger | `photo inh` held (1,812 and 1,616, no ceiling); *younger* falsified in seed 4 (8,912 s against 5,889) and held in seed 2 (7,231 against 9,364) — the age effect is a realisation, not a rule |
| M5 | `alive` within the wingspan | held (+1% and +2%) |

One reading was not predicted. Seed 4's shuffled arm made 1,471 deaths against the control's
2,764 at the same population, and seed 2's made 2,100 against 1,778. Turnover is not
consistently changed either way.

## Verdict

The queue was real, and it was the stomachs' lifeline. Removing it costs the stomachs in
both seeds.

The mechanism is plain once it is seen. When matter binds, every solvent body has the same
fecundity whatever its energy income, because a child's matter is refused or granted without
reference to the parent's reserve. A stomach earning three times a leaf's net has no more
children than the leaf.

The age queue was the one thing in the world that turned an energy advantage into a
reproductive one. A long-lived body reaches the front, and the stomachs under the leak are
long-lived. Take the queue away and the stomach line is a small clade with the leaves'
fecundity, which drift or senescence then takes.

That reading reaches further than the stomachs. At the plateau the energy economy of §5A,
the thing the project is built on, selects only for not starving. Fecundity is set by the
matter economy, which has one rule and no way for a better body to earn more of it.

The pre-registration's second reading was half right. It expected a fair contest that the
stomachs would still lose, with 0055's levers on the pool next. Those levers would enlarge
the world without changing who wins, and excretion is already at the rate that returns the
tissue share in tens of seconds.

What has to change is whether energy buys matter. That is a world rule, and it is put to the
owner in `fable-propose-matter-economy.md`. The proposal has three shapes. One is a stock
large enough that light binds first. One is scarce matter allocated by energy reserve. One
is the age queue specified as the rule it accidentally was.

The shuffled order is not adopted. The order `age` stays the default and the reference
world's, until the owner rules, and D072 is marked so. The fault stands as diagnosed, since
the design still specifies no order, and the fix that adds no rule turns out to remove the
one selective pressure the plateau had.

A caveat on the screening step turned up on the way. Three of this session's six fast-step
worlds sat in the surface film, between −0.3 and −1.5 m. Their fine-step counterparts sat at
−12 to −15 m. The three are `r19m0-s1`, `r20q0-s2` and both seed-4 arms here. The two seed-2
arms of one pair sat 10 m apart.

0052 checked population and depth per seed and found deviations inside the wingspan. It did
not see this, because the film is a bimodal outcome rather than a deviation, and a film
world is a different ecology. A 0.02 screen still answers a mechanism question read within
one step, as this one did and 0055 did. A confirmation at 0.01 is not optional, and
CLAUDE.md's gotcha now says so.

Closed on 2026-09-04. The arms were r20q-s2, r20q0-s2, r20q-s4 and r20q0-s4, with r20q-s1
censored above, and the result is negative on merit and uncensored.
