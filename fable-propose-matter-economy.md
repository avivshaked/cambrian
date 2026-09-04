# Proposal — the matter economy sets fecundity, and the energy economy has stopped selecting

Fable, 2026-09-04, after logbook/0055 and 0056. For the owner's ruling; absorbed into
DECISIONS.md when ruled, then deleted.

## What two screens established today

1. **The free matter pool is a tenth of the stock at any sink speed** (0055). Nine-tenths
   is locked in bodies at maturity; the tenth is the tissue share in transit — excreted
   at 0.01 matter/J of upkeep, a body returns its excretable share in tens of seconds, so
   raising `EVOSIM_EXCRETION` is a dead lever, as D071 half-said. Every layer reads
   ~0.1 units/m³; the column is dry from top to bottom.
2. **Conception was an age queue** (0056): the breeding walk ran oldest-first, and in a
   starved layer the oldest solvent body took the matter every step — 48–62% of plateau
   births to bodies past a lifetime. Fixed behind `EVOSIM_CONCEPTION_ORDER shuffled`,
   the queue is gone (median parent age 603–1,886 s against 3,778–4,318 s). **And the
   stomachs did worse, not better** — seed 2's line fell from 81 to 11 while the control's
   held 135; seed 4's clade was 185 against the control's 227. Turnover moved both
   ways (seed 4: 1,471 deaths against the control's 2,764; seed 2: 2,100 against 1,778),
   so the age of the population under a fair draw is a realisation, not a rule.

The second result explains the first campaign's whole shape. **When matter is the binding
constraint, every solvent body has the same fecundity whatever its energy income.** A
stomach earning three times a leaf's net has no more children than the leaf; energy
surplus buys nothing once the child's matter is refused. The age queue was the only thing
that turned an energy advantage into a reproductive one — a long-lived body reaches the
front — and the stomachs under the leak are long-lived. Remove the queue and the stomach
line is a small clade with the leaves' fecundity, and drift takes it.

So the energy economy — the whole of DESIGN.md §5A, the thing piece 04 of the primer is
about — selects, at the plateau, only for not starving. Fecundity is set by the matter
economy, which has one rule (a fixed price from the parent's layer) and no way for a
better body to earn more of it.

## The decision

Three ways to make energy count again. Each is a world rule.

**A. Matter stops binding: a stock large enough that light binds first.** Raise
`EVOSIM_MATTER_INITIAL` (1 → 3–5 units/m³) so the population reaches the light-limited
capacity before the matter cap. The energy economy then decides who breeds, as designed.
Cost: the population runs to the light capacity, which before D065 was the shrinking
ratchet and the ceiling (logbook/0046: counts of 1,490–1,610 *against a ceiling of 8,000*
were D065's doing) — D064/D065 stay, so the ratchet is closed, but the count may be
3,000–6,000 and the pace 2–3× slower. Throughput is the price. No code.

**B. Matter binds, but the price is paid in energy's currency.** A child's matter is
drawn in proportion to the parent's *energy* surplus — a solvent parent bids its reserve
above the gate, and the layer's matter goes to the highest bids first (a per-step sort by
reserve, deterministic). This keeps the world's size and makes the energy economy the
tie-breaker for scarce matter, which is what selection needs. It is a new rule with no
source; it is also close to what the age queue did by accident, with the right variable.
Code: one sort in `Reproduce`, behind the same knob as a third `ConceptionOrder` value.

**C. Keep the queue, and specify it.** Adopt `age` as the design's rule in DESIGN.md
§5A — oldest first — because it is the order under which the goal was met and the
stomachs held. Honest, but it is a longevity premium the ecology never argued for, and it
halves nothing while explaining nothing.

**My recommendation: B, screened against A.** B is cheap, keeps the world the size the
machine can run, and makes the energy books load-bearing again; A is the null the design
implicitly assumed and is worth one arm per seed to know what the light-limited world
looks like with the ratchet closed. Both at 0.02 first, seeds 2 and 4, with 0056's `age`
controls already run; then 0.01 on whichever holds. Not C.

## What does not change with any of these

The goal rule, the leak, the fixed matter cost's purpose (no ratchet), conservation.
`age` stays the default until the owner adopts something else, so the record replays.

## One caveat on the screening step, discovered on the way

Three of six fast-step worlds this session sat in the surface film (−0.3 to −1.5 m) where
their 0.01 counterparts sat at −12 to −15 m: `r19m0-s1`, `r20q0-s4`, `r20q-s4`, `r20q0-s2`.
0052 checked population and depth per seed and found the deviations inside the wingspan;
it did not see this, because the film is a bimodal outcome, not a deviation. A film world
is a different ecology (light uncontested by depth, matter and detritus arriving at the
top). Screens at 0.02 remain useful for mechanism questions read within one step, as
today's were, but a confirmation at 0.01 is not optional, and CLAUDE.md's gotcha should
say so.
