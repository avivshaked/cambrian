# 0046 — The Archean package

*2026-09-02. Pre-registered before launch; results appended after. Round 10: one
treatment × five seeds, the owner's design ([D064](../DECISIONS.md)), after rounds 8 and 9
scored zero passes between them and their post-mortems found the worlds drowning rather
than starving ([0044](0044-three-medicines.md) results, [0045](0045-the-dose-and-the-dice.md)).*

## The diagnosis, in one paragraph

Every body carried the same excess density whatever its size, so the whole population sank
its whole life and held the photic band only because breeding is concentrated where the
light is. A matter drought paused births for ~1,500 s; the standing crowd sank out of the
light together; three worlds died with full larders, free matter and a young population —
one of them an untreated control. Float tissue works, and selection discards it to ~1%
between crises because a floatless producer breeds cheaper before it sinks out. Chain
arrival, downstream, was drought-gated: absorptive mutants appeared and could not breed
through the drought around them.

## The treatment

| arms | knobs (all others: round 6's world exactly) | mechanism bet |
|---|---|---|
| `r10-s1..5` | `EVOSIM_NEUTRAL_VOLUME` 0.25 · `EVOSIM_FOUNDER_DEPTH` 60 · `EVOSIM_CELLTYPE_MUTATION` 0.005 · no refuge | size-dependent buoyancy (D064): a founder-sized body floats in place, growth is what sinks you, universal across guilds; founders scattered through the full 60 m column; the ratified 5× discovery regime for arrival |

**Dose arithmetic, stated before results.** V0 = 0.25 m³ is the founder 90th percentile
(n = 200, default `RandomGenomeOptions`: min 0.040, median 0.141, p90 0.257, max 0.438 m³).
Nine founders in ten are neutral by construction; the largest tenth sink at ≤ 31% of the
constant. A body at twice the median founder sinks at 7%, at 1 m³ at 60%, at 2 m³ at 75%.
The alternative named now, not later: V0 = 0.44 makes every sampled founder neutral, and
is the dose to try if the trade turns out to start too early (chains fail with survivors
still sinking) — not if the world blooms, which would argue the other way.
`TissueExcessDensity` stays at 0.02, so a large body sinks exactly as every body did
before; the worlds run to date are this rule's large-body limit. Founder depth 60 is the
full `WorldDepthMetres`; the photic band ends near 23.7 m, so roughly six founders in ten
start in the dark and die of it by selection rather than by rule.

Budget 30,000 s, wall 600 min, ceiling 8,000, seeds 1–5. Controls: round 6's arms by
bit-identity (`NeutralBodyVolume` = 0 and founder depth 20 reproduce the old world
exactly, suite-enforced).

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `neutralV 0.25 m3`, `founderDepth 60 m`, `cellType mut 0.005`, `refuge 0 m`, and nothing else differs from round 6's world | header line 3 |
| V2 | no arm replays its round-6 twin past t=0 — the treatment binds from the first physics step for every founder, so a token-identical prefix of any length is proof the knob did not reach the physics | row diff vs `d056-sN.md` at t=100 |
| V3 | `floor` = 0 after t=3,100 everywhere | `floor` |
| V4 | monitor carries the 32-min content-growth stall rule and the 90-s CPU+byte discriminator before any kill | monitor config |

## Predictions, and the column that falsifies each

Scored under the amended goal rule ([D063](../DECISIONS.md)), recruitment clause from
`lineage.jsonl` (exact). A pass is a pass **in the discovery regime** (mutation 0.005) and is
labelled so wherever claimed.

| # | prediction | falsified by |
|---|---|---|
| W1 | **no drowning death**: no world's `depth m` slides more than 10 m below its pre-drought mean while `births` is frozen — the small fraction holds the light through every drought | `depth m`, `births` |
| W2 | **producers persist** to budget in ≥ 4 of 5 seeds (the second disease is cured; the first — the cohort trap — is not what this round treats) | `**Ended:**` |
| W3 | **float is kept**: the evolved float share (`float` / `alive`) at t = 20,000 exceeds round 6's ~1% in the median seed — buoyancy is now the price of size, not insurance | `float`, `alive` |
| W4 | **size sorts by guild**: at any sample with ≥ 10 inherited absorptives, their mean body volume exceeds the producers' — big sinkers down where the detritus is (needs the per-guild volume read; if the report lacks it, computed from the run's lineage + genomes post hoc and labelled as such) | lineage / snapshots |
| W5 | **chains arrive** (first inherited absorptive birth) in ≥ 3 of 5 seeds, now that droughts are survivable and the larder is reachable by growth | `inherit`, lineage |
| W6 | **the round's answer**: ≥ 3 of 5 seeds pass D063 | the scoring table |

## The two-sided readings, before the answer

- **W1–W2 hold, W6 passes:** the goal is met in the discovery regime; the frontier moves to
  movement, where the argument is already written — a swimmer pays for depth only when it
  needs it, which is cheaper than the float this round makes evolution buy.
- **W1–W2 hold, W5 fails:** the drowning is cured and arrival is *still* gated — by
  something unmeasured; the diagnosis reopens at the mutant's first day, not at the world.
- **W1–W2 hold, W5 holds, W6 fails:** chains arrive into a world that no longer drowns and
  bust anyway — the cohort trap ([0043](0043-the-transplant.md)) is back on the table on its
  own, cleanly separated from the drowning for the first time.
- **W2 fails by runaway (≥ 3 seeds ceiling-censored):** the named risk — a world of neutral
  floaters blooms permanently; canopy shading did not self-limit; the light economy is the
  next conversation, and V0 goes *down*, not up.
- **W1 fails:** the rule did not reach the physics (check V2 first), or size-dependence at
  this dose is too weak to hold a drought — then V0 = 0.44.

## Launch

Five arms, ≤ 5 concurrent machine-wide: `r10-s1..s4` on the refreshed workers 4–7 at once
(round 9's two stragglers occupy workers 2–3 and are read-only to this round), `r10-s5` as
the first straggler ends. Headers verified against this table before any arm is believed.
Results appended below.
