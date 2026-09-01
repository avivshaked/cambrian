# Proposal: D061 — the patchy world (for the owner's round-8 ruling)

*2026-09-01. A proposal, not a decision — the fork is yours. Written after the invasion
assay (logbook/0043) and literature round 4 (LITERATURE-REVIEW.md §0, Q9), which this
document cites. The pattern follows fable-propose.md: absorb into DECISIONS.md on ruling,
then delete.*

## The hypothesis being turned into a design

Yours, verbatim in spirit: every depth layer is a perfectly-stirred tank, so a creature at
the right depth eats from the entire 400 m² at once — no travel, no local depletion, no
"over there." That breaks the law of nature under which real consumer-resource systems
persist, and it also forecloses movement ever paying.

Round 4 backs this with primary sources, and adds teeth:

- The well-mixed limit is the *provably unstable* case: a water-column model with a growth
  gradient and a mobile grazer is stable even with our exact feeding type (linear) at
  effectively infinite food — until diffusion crosses a critical value, homogenises the
  system, and it becomes "always globally unstable" [FM15 p.1, p.19].
- The cleanest experiment ever run on this: one continuous 90-plant platform, dead in 120
  days; eight islands with *deliberately throttled* bridges and **fewer total plants**,
  persisting 393–447+ days, every island still busting locally [JN97 p.7]. Persistence
  came from asynchrony plus limited migration, not from more resource.
- But subdivision alone is the null result: dispersal by itself can *destabilise* (Briggs
  & Hoopes 2004, abstract), and identical patches buy nothing — the patches must be
  **unequal** [HZ13 p.5].
- And the design criterion is not a dispersal rate but a length-scale ratio: coexistence
  dies when the boom-bust pattern's wavelength outgrows the domain [RMF07 p.5].

## The arithmetic that constrains the design (our own numbers)

Boom-to-bust in our world runs ≈ 5,000–8,000 s (assay controls: establishment ~t=9,700,
peak ~14,000, deep larder eaten to ~2–4 J/m³ by 20,000). The current is 0.05 m/s. A 400 m²
world is ~20 m across. So anything advected by the current crosses the whole world in
~400 s — **fifteen times faster than one boom-bust cycle**. By [RMF07]'s criterion the
pattern wavelength (cycle time × transport speed ≈ 300 m) dwarfs the domain (20 m): patches
coupled at current speed are cosmetic — one pool with extra bookkeeping.

Consequence: **horizontal exchange must be a separate, slow knob**, not the existing
current. Janssen's persistence regime had throttled bridges; ours needs the same. This is
the single most important thing the literature adds to the raw hypothesis.

## The mechanism, concretely

1. **Patches.** Each nutrient/matter layer splits into K horizontal cells (start K=8,
   Janssen's number — [JN97] got 4.5× persistence from eight). `NutrientField` and
   `MatterField` gain a horizontal index; Deposit/Settle stay within-patch; the existing
   `Mix` generalises sideways with its own `HorizontalMixingDiffusivity`, **much smaller
   than vertical** (the throttle). Detritus raining and leaking within its column is
   unchanged physics.
2. **Creatures get an x.** Organisms gain a horizontal patch position. Feeding, deposit,
   excretion, and death-return all read/write the local patch. Movement between patches:
   passive drift at a *dispersal probability per metabolic step* (metapopulation-style,
   like Janssen's bridges), not continuous advection — the current stays vertical-world
   business for now. Offspring are born in the parent's patch.
3. **Inequality for free.** [HZ13] demands unequal patches. We do not need to paint
   inequality on: make **shading per-patch** (each patch's producer biomass shades only
   its own column). Crowded patches darken themselves; empty ones are bright. The
   creatures generate the heterogeneity that stabilises them — endogenous, no new
   arbitrary constant, and it makes horizontal position *matter* for producers too, which
   is the first-ever reason for a producer to be somewhere else.
4. **The dispersal asymmetry, left to physics.** Theory says prey dispersal stabilises,
   consumer dispersal destabilises [JN97 p.11]. We do not hard-code that: detritus
   already "disperses" via sideways mixing (the reseeding path), and consumers sink
   vertically. If the asymmetry matters, it is present naturally; if evolution finds
   horizontal movement worth paying for, the movement frontier finally has a prize.

Knobs, all default-off/bit-identical: `HorizontalPatches` (1 = today's world),
`HorizontalMixingDiffusivity`, `DispersalChancePerStep`, `PerPatchShading` (0/1).

## Measured since first writing (2026-09-01, lineage replays — 0043's final addendum)

The cheap test ran. Both assay controls, replayed with the lineage instrument: consumer
generation time ≈ 1,250 s (both seeds), the boom is 4–5 generations, and it ends in
**recruitment collapse at the peak** — last clade births at t=13,938 (s2) and t=16,915
(s4), thousands of seconds before budget, while the rest of the world bred freely. The
bust is the cohort trap: the pool is grazed below the *reproduction* break-even while
every adult clears its *survival* break-even, so the cohort holds its own food down and
ages out sterile. Three consequences for the arms below: the target is now precise (a
stabiliser must keep food somewhere above the **reproduction** threshold, not above
zero); arm B's mechanism must be judged on whether it lifts post-boom density past that
threshold (a pure intake cap may not — adults' survival grazing is what pins the pool);
and arm A's asynchrony story is strengthened, because a patch with no consumers recovers
past the reproduction threshold by rain alone, which is exactly the reseeding source
[JN97] describes. The goal rule itself needs the recruitment clause (0043, correction 2).

## The caveat that could void the whole family — and its cheap test

Round 4's sharpest warning: our busts may be **cohort cycles**, not consumer-resource
cycles. A flux-fed detritus pool with a linear consumer should be *globally stable*
[MO04 p.7]; the classical enrichment instability cannot even fire without a saturating
feeding response. If instead one dominant cohort grazes the pool below its own break-even
and starves — the de Roos & Persson mechanism, once misdiagnosed as enrichment cycling in
the *Daphnia* literature [RC07 p.4] — then **no refuge and no patchiness fixes it**, and
the right stabiliser is the satiation cap on clearance that a real filter feeder must have
anyway [JKT04 p.1].

The discriminator is cycle period vs consumer generation time — currently uncomputable
because lineage events are unwritten. So: **build lineage events first** (already
promoted to pre-round-8 in HANDOFF; it also unblocks Q8's treadmill instrument and D057
species tracking through time).

## The round-8 shape I recommend

Three treatments against the round-6 world, five seeds each where budget allows, after
the lineage-events build:

| arm | treatment | what it tests |
|---|---|---|
| A | patches: K=8, per-patch shading, throttled sideways mixing | the hypothesis proper |
| B | satiation cap on clearance (+ optional type-III toe, q≈0.1 [DBWM05 p.12]) | the cohort-cycle alternative |
| C | partial pantry — floor edibility fraction | the cheap knob, as comparison; theory expects it weak (proportional refuges are the weak form [KR13 p.1]) |

If A holds chains and B/C do not: your hypothesis wins on the merits. If B alone works:
the busts were cohort cycles and the world needed a real mouth, not geography. If both:
they compose. Every reading moves the goal.

## What stays yours

Whether D061 proceeds at all; K and the initial doses; whether per-patch shading ships in
the same round or separately (it is the biggest physics change); whether arm C is worth a
worker; and the ruling on B's satiation cap, which touches §5A.1's feeding contract and
deserves its own D-entry if adopted.

## Cost estimate

Core-only, D055-shaped but larger: fields gain one index, organisms one coordinate,
feeding/deposit/death localise, plus the lineage-events build. Two to three Sonnet build
sessions with tests; throughput cost approximately nil (same totals, K-way split). The
per-patch shading variant touches `LightModel` and is the piece I would review most
carefully.
