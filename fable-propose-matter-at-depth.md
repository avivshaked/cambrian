# Proposal: matter at depth — the constraint after the leak

*Fable, 2026-09-04, after round 18 (logbook/0054) met the goal rule 4 of 5. For the
owner's ruling. Absorbed into DECISIONS.md on ruling, then deleted. Numbers are from the
five round-18 arms' reports and their absorptive logs; estimates are marked.*

## The finding

Seed 1 failed with its last stomachs at −15 m in 13.7 J/m³ — the richest water of the
round — earning +0.08 W each and holding 537 J in reserve, four times a child's price,
with no children. They were refused, not starved. Every mature world in round 18 refuses
100,000–290,000 conceptions per 100-s window for want of matter, and the line's size at
the end tracks the free matter at depth across the five seeds (0.2–0.55 units/m³ → 221;
0.08–0.1 → 4 and 76).

The arithmetic behind it, which also explains a number every round has shown:

| quantity | value | source |
|---|---|---|
| matter the world holds | 1 unit/m³ × 100 m² × 60 m = **6,000 units** | `InitialMatterPerCubicMetre`, `WorldDepthMetres`; conserved (D048) |
| matter locked in bodies at maturity | **~5,500 units** (92%) | `mat locked`, all five seeds |
| a child's matter price | 3 (fixed, D065) + 0.5 per joule of tissue ≈ **3.5 units**, from the parent's own layer | `MatterPerCreature`, `MatterPerTissueJoule` |
| bodies the world can hold | 6,000 / ~3.2 ≈ **1,900** | *estimate* |
| the population plateau in every round since D065 | **1,700–1,850** | `alive`, rounds 12–18 |

**The population ceiling this campaign has been reading as "carrying capacity" is the
matter cap.** Producers alone fill it. A second trophic level therefore does not need
more energy — the leak gave it 10–29 W and it ate 74–103% of that — it needs matter at
its own layer, and it competes for the same 6,000 units as the producers above it. Free
matter at −15 m runs at 0.08–0.55 units/m³; a 25 m³ cell at 0.1 holds 2.5 units against a
price of 3.5, so a stomach there cannot conceive until a corpse's matter sinks past it.

Why the deep is dry: matter returns to the water where a body dies (D048), which is the
producers' layer, and the producers re-lock it before it sinks — because **round 13 slowed
the matter sink from 0.02 to 0.002 m/s together with the detritus sink** (D067's marine
snow, `EVOSIM_MATTER_SINK`). At 0.002 m/s a unit of matter released at −2 m takes ~6,500 s
to reach −15 m; at 0.02 it took ~650 s. The slow detritus sink is what keeps the exudate
near the stomachs; the slow matter sink is what keeps the matter away from them. The two
were set together for one reason (remains should fall like marine snow) and only one of
them was ever about energy.

## The levers, and what each one does

1. **Matter sink back to 0.02 m/s, detritus sink kept at 0.002** (`EVOSIM_MATTER_SINK`).
   Changes *where* the matter is, not how much: released matter reaches the deep in
   hundreds of seconds rather than thousands. Existing knob, D048's own default, no
   code. Producers lose nothing they were using — they re-lock at their own layer from
   the same pool either way. *Risk:* the surface is stripped of matter faster (D048's
   "stripped surface"), which is a pressure on the producers' recruitment at the top and
   might move the population down; that is a reading, not a reason against. **This is
   the screen I recommend**, at 0.02 on seeds 1 and 4 (the two matter-starved lines),
   20,000 s, predicting `mat deep` ≥ 0.3 units/m³ at t > 10,000 and a line ≥ 10 at the
   end in both.
2. **Matter excretion up** (`EVOSIM_EXCRETION` 0.01 → 0.05). Living bodies give matter
   back continuously — but the fixed 3 units per body are never excretable (D065's
   contract: `excretable = locked − MatterPerCreature`), so at most the ~0.5 tissue units
   per body, ~15% of the locked pool, can move this way. Real, small; a second lever, not
   the first.
3. **The fixed matter price down** (`MatterPerCreature` 3 → 1.5). Doubles the world's
   body count; changes the producer world wholesale (a population near 3,800, the
   miniaturisation wall D065 was built against moves out). Not for this question.
4. **More matter** (`InitialMatterPerCubicMetre` 1 → 2). Same objection as 3 from the
   other side; and matter is conserved, so it is a permanent change to the world's size.
5. **A matter price that the stomach pays from where its food is, not where it sits.**
   A new rule, and a wrong one: matter is where it is.

## What I am asking the owner to rule on

1. Whether the matter sink may be decoupled from the detritus sink — `EVOSIM_MATTER_SINK`
   0.02 with `EVOSIM_SINK` 0.002 — as a world rule, screened first as above. (My reading:
   yes; it undoes an accidental coupling rather than adding a mechanism.)
2. Whether the campaign's next scored goal is set now, or after the screen. With D063 met,
   the candidates the record already names are: a *late* stomach invading (the assay at
   0.15, which I will run as agent work regardless); movement paying its cost (0049's
   frontier, D040–D047); and a pass outside the discovery regime (cell-type mutation at
   its default). Which of these is the goal is yours.
3. Whether the primer should now get its first chapter — the mechanism works
   (`primer/` is written "after a mechanism works", CLAUDE.md), and the leak, the flux
   instrument and the matter cap are a story with sources.
