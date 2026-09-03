# 0049 — Marine snow

*2026-09-03. Pre-registered before launch; results appended after. Round 13: one treatment,
five seeds, the owner's ruling on the trapdoor of [0048](0048-stirring-the-pot.md). Written
while the machine is reserved for other work; nothing launches until the owner frees it.*

## Where round 12 left the patient

Round 12 stirred the soup and the ingredient fell through the bottom of the pot. The rolls
reach the physics — patch-to-patch detritus spread went from ~0.4 to 4–6 J/m³, bodies ride
the water (three seeds held a whole population at −15 m, which still water under D064
cannot do), and producers survive 30 m cells. But a roll that stops above the floor is a
trapdoor: what the down leg drops below 30 m sinks out at 0.02 m/s and never returns, so the
lit half of the column stays at 0.2–3 J/m³ while the still half beneath it piles up to
15–21. One absorptive line formed in the film at ~1 J/m³ and drifted out at 0.75 children
per member; three mid-water seeds at 2–3 J/m³ formed none. The bar for a line is above 3,
and round 11 put the larder's own bar near 7.

## The treatment

| arm | knobs (all others: round 12 arm B exactly — rolls 0.3 m/s in 30 m cells, period 6,000, blink 3,000, fields advected, 4 patches, excretion 0.01) | mechanism bet |
|---|---|---|
| **A** (`r13a-s1..5`) | `EVOSIM_SINK` **0.002** · `EVOSIM_MATTER_SINK` **0.002** (both were 0.02) | **marine snow**: remains and dissolved matter fall ten times slower, so a parcel makes ~10 circuits of the lit roll before the trapdoor takes it instead of one, and the matter the surface famine is short of stays where the deaths are |
| **B** (`r13b-s1..5`) | arm A + `EVOSIM_VENT` **0.05** (patch 0, depth 60, legs 1 m — [D067](../DECISIONS.md)) | **the vent**: what the trapdoor still takes comes back up the plume; the deep larder round 12 piled up (15–21 J/m³) is spread through the lit roll within the first few thousand seconds |

*Amended 2026-09-03, before launch, on the owner's ruling: the vent runs in parallel rather
than as the next round, because it is the pre-registered next lever either way and running
both saves a day for five arms of machine time. A vent-only arm at sink 0.02 is deliberately
not run — it isolates the vent's share, which is the question after a pass, not before one.*

**Dose arithmetic, stated first.** 0.02 m/s is ~1,700 m/day — a rate for large aggregates.
The remains of a 0.01 m³ body are marine snow, which falls metres per day, so 0.002 m/s
(~170 m/day) is still generous by an order of magnitude; it is chosen as one decade, not as
a measurement. The trapdoor's leak rate scales with the sink speed at the roll's floor, where
the roll's own vertical velocity is zero by construction, so the surface stock should rise
by something like the same factor until another sink binds — grazing by the producers'
matter draw at conception, or the floor. Both fields are slowed together because they are
the same physics; D052's excretion returns matter *dissolved*, and a dissolved field that
sinks at 0.02 m/s is what makes the surface a permanent famine (0048's S3: excretion 0.01
cut refusals only 1–4×). Budget 30,000 s, wall 600 min, ceiling 8,000, area 100, seeds 1–5.
Controls: round 12's arm B (`r12y-s1..5`), the same world at sink 0.02.

The knobs exist (`EVOSIM_SINK`, `EVOSIM_MATTER_SINK`; header `sink 0.002 m/s, matter
0.002 m/s`), defaults unchanged, so every earlier run is untouched.

**Arm B's dose, stated first.** The plume rises through one patch of four, 25 m² of a 100 m²
floor, so at 0.05 m/s it lifts 1.25 m³/s of deep water; at round 12's 15–20 J/m³ that is
~20 J/s into the surface leg, about the rate at which deaths rain detritus into the column
now (~1,700 bodies of ~70 J living ~8,000 s). The vent therefore doubles the surface supply
rather than swamping it, and turns the whole column over in ~80 min, so the deep larder
round 12 accumulated is spread through the lit roll early in the run. At 0.1 m/s the column
would turn over every 40 min — a well-mixed world, closer to the full-column roll that
killed founding — so 0.05 is the first dose and 0.1 the pre-registered escalation. The cost
is a dark excursion for bodies: the return sinks through the other three patches at a third
of the plume speed, ~0.017 m/s, and a body it captures at the roll's floor spends ~3,600 s
descending and ~1,200 s riding the plume back — half a lifetime in the dark. Producers live
inside the upper roll, whose own vertical velocities are ten times the return's, so they are
exposed only where the roll's flow goes to zero: at the surface, where the return pushes
them back into the roll (harmless), and at 30 m, where it pushes them out (fatal). Round 12's
populations sat at −3 to −15 m with spreads of 5–8 m, so few reach 30 m; how few is M6.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `sink 0.002 m/s, matter 0.002 m/s`; arm A `vent off`, arm B `vent 0.05 m/s in patch 0 from 60 m, legs 1 m`; every other token equals `r12y-sN`'s | header line 3 |
| V2 | no arm replays its round-12 twin past t=0 | row diff vs `r12y-sN.md` at t=100 |
| V3 | `floor` = 0 after t=3,100 | `floor` |
| V4 | audit 0.0000% every sample | `audit` |
| V5 | monitor: 32-min content-growth stall rule, 90-s discriminator before any kill | monitor config |

## Predictions

Scored under D063 unchanged, recruitment clause from `lineage.jsonl`; a pass is a pass in the
discovery regime (mutation 0.005) and is labelled so. `J/m3 here` is the field at the
population's mean depth in patch 0 (0048's instrument note); read it with `det patch sd`.

| # | prediction | falsified by |
|---|---|---|
| M1 | **the larder stays in the light**: `J/m3 here` ≥ 5 at t=15,000–25,000 in ≥ 3 of 5 seeds (round 12: 0.2–3), and `det deep` at t=30,000 is below round 12's 15–21 | `J/m3 here`, `det deep` |
| M2 | **the famine eases**: refused conceptions (`mat blk`) at t > 10,000 fall ≥ 5× against `r12y-sN` (round 12's S3, re-asked with the matter now staying up) | `mat blk` |
| M3 | **no bloom**: no seed reaches the 8,000 ceiling; if matter kept in the light lets producers run away, the round is censored and the reading is irradiance down or `MatterPerCreature` up, not sink speed back | `**Ended:**`, `alive` |
| M4 | **chains arrive** (first inherited absorptive) in ≥ 3 of 5 seeds, and at least one line reaches ≥ 10 inherited at some sample | `inherit`, lineage |
| M5 | **the round's answer**: ≥ 3 of 5 seeds pass D063 in at least one arm. Written honestly: the pre-registration predicts **B passes and A does not** — A lifts the surface by keeping remains up, B also lifts the deep larder that is already there, and the bar is ~7 J/m³ — and would count A passing alone as the more useful result (the simpler world suffices) | the scoring table |
| M6 | **producers survive the vent** (arm B's Sverdrup risk): `alive` ≥ 1,000 by t=10,000 in ≥ 4 of 5 arm-B seeds, and mean `depth m` at t > 5,000 stays above −20 m; falsified by the signature that killed the 60 m roll — a population that never rises, or a collapse inside the first 5,000 s | `alive`, `depth m` |

M1–M4 are read per arm.

## The two-sided readings

- **M5 passes:** the goal is met (discovery regime, stated as such); the frontier moves to
  movement, in water that moves and feeds.
- **M1 holds, M4 fails:** food at the mutants' depth is above 5 J/m³ and no line grows — the
  gate is inside the mutant after all (0048's line bred at 0.75 per member on ~1 J/m³; if it
  breeds no better on 5, the absorptive's economics are the next round, not the world's).
- **M1 fails with V1/V4 held:** the sink was not the binding leak — look at `% on floor` and
  `det deep` to see where the stock went; the vent (D067, built alongside this round and not
  yet run) is the pre-registered next lever, because it returns what the trapdoor takes.
- **M3 fails:** matter in the light is a bloom; censor, then irradiance or the fixed matter
  cost, both known levers.
- **M6 fails:** the return flow captures producers; vent speed comes down (the excursion
  lengthens but fewer bodies leak — a trade to measure), or the plume patch count goes up
  (`EVOSIM_PATCHES` 6–8 cuts the return speed per patch); arm A's result stands on its own.
- **B passes, A fails:** the vent is load-bearing; the world keeps it and the vent-only arm
  (sink 0.02 + vent) is the mechanism round after the pass.
- **M2 fails while M1 holds:** detritus stays up and matter does not — the two fields part
  ways somewhere (excretion's deposit depth, the matter draw at conception); read `mat top`
  against `J/m3 here` before choosing.

## Launch

Ten arms, ≤ 5 concurrent, interleaved so both arms have early seeds: `r13a-s1`, `r13b-s1`,
`r13a-s2`, `r13b-s2`, `r13a-s3` in the first batch, the rest as workers free. Workers
refreshed and hash-checked before launch (D067's edit to `EvolutionRun.cs` post-dates the
last check), headers verified against the table before any arm is believed, a fresh monitor
on every arm (the round-12 monitor exited with its list empty). Held until the owner frees
the machine. Results appended below.
