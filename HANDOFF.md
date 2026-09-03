# Handoff — where to pick up

**Updated 2026-09-03, after round 13 scored (0 of 5, cut to five arms: the trapdoor closed and the populations floated into a thin surface film; the gate is the stomach's ledger — D068), with round 14 (clearance 5 and 10) running under D069's sequential rule, the invasion assay (logbook/0051) and the coarse-step validation (logbook/0052) running beside it, and the ledger calculator (`scripts/ledger.ps1`) in use.** This is the one file a new agent should
read first. It says what was being attempted, how far it got, what is queued, and exactly how
to continue. It is a pointer, not a source of truth: the specification is `DESIGN.md`, the
reasoning is `DECISIONS.md`, the history is `logbook/`, and the operating rules are
`CLAUDE.md`. When this file and those disagree, those win.

## The goal that was being pursued

**A self-sustaining world that holds a food chain — verified across seeds, not observed
once.** Chosen by the owner on 2026-08-29 from the options in a written proposal
(`fable-propose.md`, absorbed into this file's queue and removed on 2026-08-31 together
with `sol-gpt-propose.md`, an owner-provided independent review — git history holds both
full texts, including what was already done or rejected). Scored by a
rule written before each round launched:

> In at least 3 of 5 seeds, with the population floor closed: the producers persist to the end
> of the run, an absorptive lineage is inherited for ≥ 20 consecutive samples, it is still
> alive (≥ 10) at the last sample, **and at least one absorptive birth occurred within the
> last 20 samples** (D063's recruitment clause, applied from round 8 onward).

**Status (2026-09-02): not met. Nine rounds scored, zero confirmed passes; round 10 is
running on the owner's own design.** Rounds 8 and 9 (logbook/0044, 0045) tested three
feeding-side stabilisers and a dose-plus-mutation follow-up and scored 0 of 15 and 0 of 5.
Their post-mortems found the worlds were not starving but **drowning**: every body carries
the same excess density whatever its size, so the population sinks its whole life and holds
the photic band only because breeding is concentrated in the light; a matter drought pauses
births for ~1,500 s, the standing crowd sinks out of the light together, and the world
starves in the dark with its larder full — three worlds died so, one an untreated control.
Float tissue works and selection discards it to ~1% between crises. Chain arrival is
drought-gated, not mutation-gated (5× mutation supplied singletons that could not breed).
The owner's response is **D064** — size-dependent buoyancy: a founder-sized body floats in
place, growth is what sinks you, universal across guilds so descent is a priced choice for
detritivores too; plus founders scattered through the full column. Built and run as round
10 (logbook/0046): the drowning is cured, and the world's answer was to shrink — so D065
(a fixed matter cost per body) bounded the count, and round 10c produced **the campaign's
first five-of-five stable, uncensored producer worlds**. The goal's first clause is now met
by construction; the whole remaining problem is the second trophic level, which does not
arrive because the detritus larder sits 50 m below the surface where every mutant is born
and, being small, stays. Round 11 (senescence 3,000) opened the rate side — the larder
crosses the breeding bar by t=15,000 — and isolated the location gate: surface detritus
0.1–0.5 J/m³ against 8–13 in the deep. Round 12 (D066: convection rolls that advect bodies
and fields, plus excretion 0.01) is running; its rolls stop at 30 m to keep producers in the
light and therefore leak remains into the dark, so surface detritus reached only 0.24–0.53,
and yet one excretion-arm seed produced the first inherited absorptive line since round 8.
The proposed next lever is the sink speed of remains and matter (0.02 → 0.002 m/s, marine
snow rather than large aggregates; `EVOSIM_SINK`, `EVOSIM_MATTER_SINK`). The paragraph below
is the pre-D064 account and stands as history.

**The account as of round 7, kept for the record.** Seven rounds were scored
(logbook/0036–0042). Under D058's completed-budget rule the campaign's honest tally is
**zero confirmed passes, ever** (round 6's single pass rode a wall cut; round 7 scored
0-of-5). Round 7 — D055's seabed refuge, run against round 6's world — accidentally became
a matched-pair experiment (deterministic replay + a staggered launch gave every treated
arm a byte-identical untreated control) and produced the campaign's causal account:
**consumer chains establish by grazing the sea-floor detritus hoard** (~66 J/m³ in one
metre, the densest food anywhere); an unguarded hoard funds booms that exhaust it and
bust; a locked hoard (the refuge) yields only slow, small chains an order of magnitude
below the booms. The refuge is falsified as damping — it was an access gate, not a meter
(the impulse harness measured a 1 m refuge as transport-identical to none). D060's
invasion assay then ran (logbook/0043, four arms, all budget-complete) and finished the
job: a verified consumer genome hand-placed into paired worlds **established and boomed
in both controls and starved to extinction in both refuge worlds, without one
descendant** — because the evolved consumer is *benthic* (it sinks to the floor in ~900 s
and feeds there), so the refuge is total exclusion, not a held-back fraction. D055 is
rejected as a world rule at any floor-covering dose. The split answer to "world problem
or mutation problem": establishment is a world problem (the on-ramp is the floor pantry),
arrival is a mutation-supply problem (these seeds never delivered a breeding absorptive
in 30,000 s; a transplant chain built itself in both). The stabiliser the goal needs must
let a founder reach concentrated food *and* stop a boom from taking all of it — and the
owner's hypothesis, on record in 0043, is that the real distortion is **whole-layer
horizontal access** (a perfectly-stirred layer: no travel, no local depletion, no reason
movement should ever pay). The round-8 design decision is the owner's, in discussion.

## What was established, in order (the logbook entries are the record)

| entry | round | what changed | what it found |
|---|---|---|---|
| [0036](logbook/0036-the-floor-gives-back.md) | 1 | D051 remineralisation (a floor→water leak) and — the thing that mattered — **mixing 0.2 m²/s**, a value nobody had run (only 0 and 2 ever had) | D051 is inert: `NutrientField.Mix` already exchanges across the floor. At mixing 0.2 the deep water crosses the absorptive break-even by t≈2,300–4,000 in every seed and **food chains appear** — 4 of 6 seeds, 3 from the world's own mutants. Every lineage booms then busts |
| [0037](logbook/0037-the-net-comes-down.md) | 2a, 2b | `FloorClosesAfterSeconds` — the population floor stops firing after founding | The floor had been running the founding lottery (40 random genomes breed in 1 seed of 4) **and** rescuing every matter crash. With it closed at 3,000 s, **3 of 5 seeds go extinct** at their first drought. One mutant chain arose with the net down and bust to zero |
| [0038](logbook/0038-a-lighter-world.md) | 3, 3b | fix 1: `excessDensity` 0.1 → 0.02, then 0.05 | At 0.02 producers stop dying and **run away** to the population ceiling (uninterpretable); one seed died of **age synchrony** instead. At 0.05 they die as at 0.1. No density gives a scoreable world on its own |
| [0039](logbook/0039-a-slower-drought.md) | 4 | fix 2: senescence 3,000 → 10,000 s, in the 0.02 world, ceiling 8,000 | **Two of five seeds run; three not launched.** The claim held: producers bred through an 8,000-s drought and **recovered from 73 individuals without the floor** (s1) and from a cohort at mean age 3,400 (s2) — states that were terminal in every earlier round. Both then **ran away** (s1 to the ceiling at t=25,998; s2 at ~6,500 and rising at the budget): a lit population that no longer dies of age has no limit (S4 failed). Consumers unchanged: s2's chain 549 → 3 over 12,000 s, no upturn (S7 falsified, sixth time). See *Queued* |
| [0040](logbook/0040-right-sizing-the-dish.md) | 5, 5b | D053: irradiance 25–175, then area 100–200 m² | **No geometric rescale bounds this world** — every dimmer sea dies (famine below 150, drought above), 200 runs away; small dishes die of shading-driven darkness and irreversible sinking (−131 m in a 60 m world — the ocean has no floor for bodies). D053 closed |
| [0041](logbook/0041-the-sea-digests.md) | 6 | D052: excretion k=0.001 | **The first bounded, living, uncensored worlds** (two of five to full budget, floor silent, drought-cycling); three chain establishments in one round; the "first trophic collapse" (s3) — later corrected by round 7. One near-pass at a wall cut. Every boom still bust |
| [0042](logbook/0042-the-larder-under-the-mud.md) | 7 | D055: refuge 1 m, on round 6's world | **The matched-pair round.** Two arms replayed round 6 byte-identically (refuge never bound); three diverged at establishment. Chains establish through the floor pantry: treated booms 71 and 1-then-dead against controls' 908 and 1,297; s5's late chain grew to 18 on column food alone (the slow path exists). s3's twin died chainless anyway — round 6's collapse was never trophic at bottom. Refuge falsified as damping; scored 0-of-5 under both tables |
| [0043](logbook/0043-the-transplant.md) | 7.5 (diagnostic) | D060: five copies of a round-6 consumer genome injected at t=8,000 into seeds 2 and 4, refuge 0 vs 1 m — paired, all four budget-complete | **The consumer is benthic** (sank to the floor in ~900 s, both seeds). Controls: established (t=9,700 / 11,300), boomed to 135 / 121, alive at budget (56 / 93) with inherit streaks of 104 / 88 samples. Treatments: starved to extinction on ~13 kJ of forbidden floor stock, zero descendants. D055 rejected as world rule; the arms are diagnostic and cannot count toward the goal. Lineage replays then found the **recruitment collapse at the peak** (every boom's bust is a sterile cohort dying on schedule) → D063's clause |
| [0044](logbook/0044-three-medicines.md) | 8 | D061 patchy world / D062 satiation cap + toe / D055 at a 0.2 edible fraction, 5 seeds each | **0 of 15.** A: producers extinct in all five (founding cost of fragmented pools). B: every clean seed ran away before a chain formed. C: one chain (s1), wedge-censored with a sterile cohort standing — D063 failed it at the cut; s2/s4 replayed their controls full-length (refuge never bound); s3 died whole-world with its larder full. Post-hoc: **the drowning** (depth timelines) |
| [0045](logbook/0045-the-dose-and-the-dice.md) | 9 | C at fraction 0.4 + mutation 0.005 (both pre-registered contingencies) | ≤ 2 of 5, formally failed mid-round: s1, s2 extinct by the drowning with no chain; s4 survived chainless; s3 (runaway-bound) and s5 were still running when round 10 launched. Lineage dissection falsified the age-structure reading of the births-freeze: deaths are `starved`, populations young — an energy death in the dark |
| [0046](logbook/0046-the-archean-package.md) | 10, 10b, 10c | D064 (`NeutralBodyVolume` 0.25, founder depth 60, mutation 0.005, no refuge); then area 400 → 100; then D065 (`MatterPerCreature` 3) | **10/10b censored early**: the rule reached the physics (surface film, no drowning) and the world answered by shrinking — matter per body halving every ~1,100 s, the count ratcheting toward the ceiling at any dish size. **10c: 0 of 5 — and five of five producer worlds to budget, uncensored, the first ever.** Drowning cured, count bounded at ~1,500, turnover a drizzle (mean age 13,500–15,500 s), larder crossed the bar only at the end in two seeds, chain never arrived: two gates named, rate and location |
| [0047](logbook/0047-the-half-life.md) | 11 | senescence 10,000 → 3,000 on the 10c world (the rate lever) | **0 of 5, five of five to budget.** Wear is a slope, not a cliff: turnover ~1.9× (X1 falsified, ages 7,200–9,300 at t=20,000), but the larder crossed 7 J/m³ by t=15,000 in four seeds and ended at 7.9–12.7 (X2 holds). Surface detritus 0.09–0.5 all round; no inherited absorptive anywhere (X4). **The location gate, isolated**: rate side open, food fifty metres below the mutants. Round 12's still-water controls |
| [0048](logbook/0048-stirring-the-pot.md) | 12 · **0 of 6**, all to budget | D066 roll cells (4 patches, 0.3 m/s, 30 m cells, period 6,000, blink 3,000, fields advected) — arm A; + excretion 0.01 with the fixed matter term non-excretable — arm B | Two mid-round dose changes on pre-registered readings: cell 60 → 30 (Sverdrup: `r12a-s1` never founded, stirred into the dark); speed 0.1 → 0.3 (the roll's surface dead zone). **S1 falsified at both speeds by mechanism**: a roll that stops at 30 m is a trapdoor — the down leg drops remains below the cell, where the 0.02 m/s sink takes them for good; surface detritus 0.24–0.53 J/m³, deep 12–15. Arm A stopped at one seed (`r12x-s1`); arm B's five seeds (`r12y-s1..5`) all to budget, three of them settled at −15 m (mid-roll, 2–3 J/m³) and two in the surface film. **First inherited absorptive since round 8** in `r12y-s3`: a four-generation single-child line, t=6,961–22,650, never above 3 inherited, 0.75 children per member — drifted out. S3: refusals down 1–4×, not 5×. Proposal to the owner: round 13 = arm B + `EVOSIM_SINK`/`EVOSIM_MATTER_SINK` 0.002 (Stokes for remains; knobs added, defaults unchanged) |
| [0049](logbook/0049-marine-snow.md) | 13 · **0 of 5**, cut to five arms, all to budget | Round 12 arm B's world + sink 0.002 both fields (arm A, 3 seeds); + D067 vent 0.05 (arm B, 2 seeds) | The trapdoor closed (deep field 2–5.6 J/m³ vs 15–19; floor share 0.4% vs 2.7%) and four of five populations floated into a surface film reading 0.3–1.4 J/m³; the one that held −11 m sat in 5–9 J/m³ and still formed no line. Refusals rose 1.4–5× (M2 reversed). Vent: producers survive it (M6), deep field lowest, no chain. Reading: the gate is the stomach's ledger, not the water — D068 |

**The mechanism, in one paragraph.** Surface matter runs out (D048's economy — producers
strip it), conceptions are refused, births stop. From there one of two things is
irreversible: at excess density 0.05–0.1 the bodies sink out of the photic band within
~1,000 s and age out in the dark while the matter recovers above them; at 0.02 they stay lit
but the drought has made the survivors one cohort, and under D038's wear a cohort past
~1,300 s has no surplus to breed with. Every extinction watched — eleven of them — has one of
those two signatures. Round 4 showed the second face is removable: at senescence 10,000 s
both seeds passed through those states and came back, once from 73 individuals with the
floor closed — and then, with nothing left to kill them, grew until the instrument or the
budget stopped the run. Separately, every absorptive lineage that established (six of them)
ate the deep water from 18–38 J/m³ to ~4 in about 1,500–4,000 s and bust to zero or nearly
so; nothing damps a consumer here but its food.

**Owner's standing priority (2026-09-01): get the goal met and move on.** This phase has
been educational but is the less interesting part — bias every choice toward the fastest
credible pass. Pre-registered contingencies fire without new deliberation (D056's 5×
discovery rerun if arrival binds); no new side investigations unless they unblock the
goal; the movement frontier and the aquarium are the destinations waiting behind the pass.

## Queued — the current path, in order

Rounds 1–9 plus the D060 assay are scored (the table above is the record). What remains:

0. **Round 13 is running, cut to five arms; round 14 is pre-registered and queued behind
   it (2026-09-03).** logbook/0049: round 12 arm B's world with `EVOSIM_SINK 0.002` and
   `EVOSIM_MATTER_SINK 0.002` — `r13a-s1..3` (vent off) and `r13b-s1..2` (`EVOSIM_VENT
   0.05`, D067's first run), launched 2026-09-03 on w2..w6, V1–V4 verified. Seeds 4 and 5
   of both arms are **not** launched, on the owner's ruling to move to round 14 (0050
   records why). Mid-run reading at t ≈ 18,000–20,000: the physics did what 0049 promised
   (deep field 2–4 J/m³ vs round 12's 9–10, M6 held for the vent) and no chain started in
   any arm — `inherit` 0 everywhere, refusals higher than round 12's. **Score M1–M6 on the
   five arms when they end** (recruitment from `lineage.jsonl`), append Results to 0049,
   update D067's index row. The launch settings are in `scratch/launch-r13.ps1`
   (gitignored, this machine): `r12y-sN`'s block plus the two sink knobs, with
   `-Seconds 30000` baked in because `run-arm.ps1` defaults to 40,000.
0z. **The speed programme (D069, 2026-09-03) — what is running and what to do with it.**
   Round 13 scored 0 of 5 (0049). Round 14 runs under the sequential rule; 0050's interim Results
   table scores it: `r14c5-s1` fail (line of 3–9 for 138 samples, 4 alive at the end),
   `r14c5-s2` fail (no line), `r14c10-s1` fail (line peaked at 22, grazed its field at
   −15 m from 3.3–4.0 to 1.1–1.3 J/m³, gone by t=16,000, no second wave by 30,000),
   `r14c10-s2` (w6) at t=22,800 with its line 48 → 4 in water it thinned to 0.4–0.7.
   `r14c10-s3` (w2) and `r14c5-s3` (w5) were stopped under the futility rule on the
   owner's word (no line by 15,000, both in the surface film) — those two workers need a
   refresh before reuse (a killed process takes its Library with it). Running:
   `r14c10-s2` (w6, ending), `r14c10-s4` (w7, keep to budget: −15 m in 7 J/m³, the
   richest water yet), `r14c5-s4` (w3, a line of 1–2; stops when the exudation screen
   needs its worker). Seed 5 of each dose will not be launched (owner). Score each as it ends into
   0050's table (the scoring script's logic is in the table's note: longest inherited run
   from the report's `inherit` column, recruitment from `lineage.jsonl`). The next screen
   after the round is the stabilisers that exist and have not run in this world — clearance toe,
   satiation (round 8's arm B, D062), floor refuge (D055, rejected twice) — ledger first. Done: `fable-propose-detritus-flux.md` — the ledger says satiation caps
   this body at R0 ≤ 1 and the toe raises its break-even; the constraint is the detritus
   energy flux (~0.2 W against a ~17 W producer economy, inferred from `detritus J`'s
   rebuild in `r14c10-s1`), and the proposed lever is exudation. **Ruled: D070**, yes in
   principle, build gated on `r14c10-s1-flux`'s measured income (~0.2 W expected) and
   `r14c10-s4`'s outcome (crash expected); the literature round on exudation runs first;
   the screen is pre-registered in D070. The proposal file is deleted.
   Built meanwhile: the detritus-flux instrument (`det in` / `det out` columns, J per
   window; `World.DetritusDepositedTotal/TakenTotal`; test in `WorldTests`). It changes
   `EvolutionRun.cs`, so every worker needs a refresh before its next launch — w4 is
   refreshed and compile-checked on it; w2, w3, w5, w6, w7 carry the previous build under
   their running arms. Report readers are named-column, so nothing else changes. The invasion
   assay (0051): `r15i-c10` scored — it cannot invade at clearance 10 (R0 0.96 on the
   founding stake, one naturally endowed child from 50; the calculator agreed); `r15i-c1`
   scored — same answer at clearance 1 in water ten times richer (0051 closed: the
   stomachs set their own field, and the assay's horizon of one lifetime cannot score
   establishment); `r15i-c5` dropped as redundant. The
   coarse step (0052) is closed: 0.02 screens, 0.01 confirms, 0.05 out, limiter gated —
   w7 is free. Every worker needs `new-worker.ps1` (once per worker, from PowerShell) plus
   the four-file hash check before a launch; the monitor's watch list is
   `scratch/evosim-watch-arms.txt`, logs in `scratch/logs/`. Launchers:
   `scratch/launch-r14.ps1`, `launch-r15.ps1`, `launch-r16.ps1`. The ledger calculator
   (`./scripts/ledger.ps1`) is the first thing to run on any proposed knob from now on.
0a. **Round 14 = D068, the stomach's gearing (logbook/0050).** The ledger, read from the
   code, says an absorptive part at clearance 1 breaks even at 4 J/m³ and clears 3–5 W/m³ in
   the best water any seed offered, against ~47 W/m³ for a leaf at the surface — so a mutant
   that swaps a leaf for a stomach breeds slower than its siblings and drifts out, whatever
   the water does. Owner's ruling: raise `EVOSIM_CLEARANCE`. Two arms on round 13's arm-A
   world: `r14c5-s1..5` (clearance 5) and `r14c10-s1..5` (clearance 10); no code change.
   `scratch/launch-r14.ps1 -Clearance 5 -Seed 1 -Worker 2` launches one arm;
   `scratch/queue-r14.ps1` (running in the background from this session) launches the next
   queued arm on each round-13 worker as it ends cleanly and passes the four-file hash
   check, and appends the arm to the monitor's watch list. Verify V1 (`clearance 5` /
   `clearance 10`, `sink 0.002 m/s, matter 0.002 m/s`, `vent off`) before believing an arm.
   M2 predicts c10 passes; if M1 fails at clearance 10, the pre-registered next step is the
   per-creature ledger instrument for absorptive individuals, not another world change.
0b. **After round 13, whatever it scores: calibrate and switch on the species column
   (owner's request, 2026-09-03).** `SpeciesDriftThreshold` has been 0 in every arm since the
   column was added, at which `AssignSpecies` gives every creature species 0 — so the
   `species` column has read "1" in every world and has never counted anything. Steps: run
   `./scripts/core-test.ps1 -Filter SpeciesCalibration` to print the single-mutation distance
   distribution; set `EVOSIM_SPECIES_THETA` several typical-mutation-lengths out, per the
   tunable's remarks in `RunConfig.cs`; record the chosen value and the distribution in a
   logbook entry; then count round 12 and 13's worlds retrospectively from their
   `lineage.jsonl` files (parent links and the per-birth species id are there — a
   dissection script, not a rerun, and a candidate for `scripts/`). From then on every arm
   carries the threshold and the column is read as diversity. Instrument work, agent-owned;
   does not touch the goal rule.
1. **~~The round-8 design~~ — ruled and run; kept for the reasoning trail.** The assay
   answered the mechanism question; the owner then advanced a hypothesis (recorded in
   0043's Results) that the deep distortion is **whole-layer horizontal access**: every
   layer is a perfectly-stirred tank, so a creature at the right depth feeds from all
   400 m² at once — no local depletion, no travel, no spatial asynchrony, which is the
   regime where consumer–resource theory predicts the widest cycles (paradox of
   enrichment — inference, uncited) and which also forecloses movement ever paying. The
   agent's recommendation, awaiting the owner's ruling: (a) ~~a small literature round
   first~~ **done — review round 4 (2026-09-01, ten papers into the synthesis, Q9)**,
   and it sharpened the options: patches must be *unequal* to stabilise [HZ13], the
   design criterion is boom-bust wavelength vs domain size [RMF07], the strong refuge
   form is fixed-number ≡ type III feeding [KR13], a satiation cap on clearance is
   physically mandatory anyway [JKT04], and the busts may be cohort cycles no spatial
   fix addresses (de Roos & Persson — lead; the discriminating measurement needs lineage
   events, **built 2026-09-01** — one row per birth/death into `lineage.jsonl`, inert by
   construction, suite 374; workers pick it up at their next refresh); the design draft
   itself is written: **`fable-propose-d061.md`** (absorb into DECISIONS on ruling, then
   delete, per the propose-file pattern); (b) a D061 design draft — horizontal patches
   per layer, organism x-position, passive drift on the existing current, local feeding
   and deposit, sideways mixing; (c) round 8 pre-registered with one partial-pantry
   comparison arm (the knob exists: a fractional `EdibleDensityAt` generalisation) so
   the world says *which* stabiliser it needed. Owner has ruled on the side questions:
   s5's solo rerun is **skipped**; pushing all commits including `inocula/` approved.
2. **Owner decisions still open besides round 8:** none. The stillborn `matterPrice`
   guard test stays queued for whenever `Conceive` is next touched (the orphan itself
   was verified vacuous — D052's corrected addendum). Run-identity git/worker
   fingerprints are still a TODO in `run.json`.
4. **The sloped world (D054)** — the destination after the goal: `floorDepth[column]`
   architecture, straight slope as the first profile, profile in the config hash, procedural
   generation deferred. Design questions it must answer first are listed in the D-entry.
5. **Round 4 seeds 3–5** were dropped by agreement (n=2 answered both directions); the
   launch command below survives only in case a baseline at irradiance 200 is ever wanted.

   ```powershell
   $s = @{ EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.05; EVOSIM_MIXING = 0.2;
           EVOSIM_SENESCENCE = 10000; EVOSIM_EXCESS_DENSITY = 0.02;
           EVOSIM_MATTER_PER_TISSUE = 0.5; EVOSIM_FOUNDER_FLOAT = 0.5; EVOSIM_REMIN = 0;
           EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000 }
   ./scripts/run-arm.ps1 -Name d054-s3 -Worker 2 -Seed 3 -Seconds 30000 -WallMinutes 600 -Settings $s
   ```
   Check `Get-Process Unity` first, refresh the worker with `./scripts/new-worker.ps1 -Workers 2`
   if `unity/` has changed since it last ran, and **verify the header** (line 3 of
   `runs/<name>.md`) shows `senescence 10000 s · excessDensity 0.02 · floor closes 3000 s ·
   ceiling 8000` before believing anything the arm says.

2b. **Fix 3 became D055 (the seabed refuge), built, running as round 7** (`d057-s1..s5`,
   pre-registered in logbook/0042 with a dated mid-round addendum). The owner ratified
   three rulings from an independent review while it ran: **D058** (only the budget ends
   a run — wall/ceiling cuts are censored and cannot pass; round 7 is dual-scored),
   **D059** (the ocean gets a floor — D050's mirror clamp, per-column under D054, with
   the below-world observables), **D060** (the invasion assay as a labeled diagnostic,
   sequenced before any mutation-raising).

3. **Before round 8 — the experiment contract** (bounded repairs, one worker refresh,
   ratified 2026-08-31; none touch round 7):
   - run identity into the run directory: exact seed, git commit + dirty flag, worker
     source hash, timesteps, Unity version, termination reason;
   - reset all static reporter state at `Run()` start;
   - split `read-arm.ps1` into a generic parser and per-round scorers (it currently
     applies D051's P-rules to every round and prints false FAILs);
   - new columns: edible vs physical detritus density at mean depth, below-world counts
     (D059), matter flows (locked, excretion flux, conception draw, death return), and
     the `species` count (D057, in build);
   - the field-only refuge impulse test (deposit into the refuge, edible layers empty,
     settle+mix, report flux over time at 0/1/5/10 m) — picks any thicker-refuge dose
     from measured transport, not the knob's unit;
   - doc repairs from the review's finding 9: D051's stale `min(1, rate·dt)` text, index
     rows for D048/D052/D053, README's status block and its `lineage.jsonl` claim (the
     file is empty — either write events or stop promising them), this file's top.

4. **After the goal** — the queue, absorbed from the two 2026-08-29 proposal documents on
   their removal (2026-08-31; the excretion contract from that list already became D052):
   - **Ballast buoyancy** — reserve-coupled lift (heavier while the energy reserve is full,
     lighter as it drains), the mechanism cyanobacteria actually use; needs no neurons.
     Normalise by seconds-of-reserve, not raw joules, or lift couples to body size; run
     paired seeds against fixed buoyancy; judge by inherited persistence and vertical
     cycling, never mean depth alone.
   - **Added mass** — `FluidConfig.AddedMassCoefficient` is 0 in every evolution run and 1
     in the sandbox and smoke tests. Decide before any arm where swimming is supposed to
     pay, and before the dt sweep (D028 orders it first). A config-default choice plus
     re-calibration; D-entry either way.
   - **The dt sweep** — D028's "only lever that multiplies simulated seconds". Acceptance
     test already stated: if the audit residual grows, the speed was bought with free
     energy. First put the physics timestep in the config hash, and hold metabolic seconds
     fixed or the sweep changes metabolism at the same time.
   - **IL2CPP** — at the Farm milestone, not before: it is a build backend, and Editor
     `-batchmode` runs are Mono regardless (settled 2026-08-31).
   - **Run-integrity infrastructure** (deferred as sound-but-not-on-the-path; three small
     code points from the same review were fixed in commit 403e684): a typed run manifest
     that rejects unknown keys and stores the resolved config plus a source fingerprint
     (git commit, dirty flag, worker fingerprint); a preflight that diffs against a named
     control arm and refuses an unplanned variable; compact per-organism birth/death
     events in `lineage.jsonl` — the evolutionary-activity measures round 3 found require
     a lineage record, and events cost a few MB per hour where full genomes would cost
     hundreds; a Unity boundary smoke that fails when a requested setting does not reach
     the arithmetic; a fast/slow test split and CI for Core.
   - **Sensing before controlled lift** — `Chemical` cannot silently stand for both
     detrital energy and reproductive matter now that D048 split them; decide the sensor
     contract (separate channels, or one declared target field) before building it. Then
     brain-driven lift is the cheapest active vertical strategy and the control any joint
     must beat.
   - **The muscle re-test** — only in a world with a moving or spatial prize, never the
     depth-only world (it rewards passive lift by construction); persistence-aware
     measures, since co-optimisation undervalues newly-mutated bodies ([MC25]); the
     untried prize-side option is mating as a reason to travel ([VG05], Gene Pool's
     second prize).
   - **The tiling fork, two-sided and pre-registered** — a persistent inherited food chain
     in depth-only ecology ends the 1-D era on schedule; repeated failure under
     otherwise-sufficient conditions becomes the evidence that space is the missing
     ingredient (Hamm & Drossel, round 3). Either outcome moves the decision; the trigger
     cannot fail silently.
   - **The aquarium** — the minimal view, not the Milestone 8 Theatre: camera, pause and
     speed, organism selection, colour by cell type, lineage identity, energy/matter
     overlays, replay from a snapshot plus events. On the record a human at the screen is
     the project's best bug-finder (logbook/0005, 0006, 0010). **Strictly post-goal, all
     of it, including the sandbox genome-loader** — the owner's ruling (2026-08-31):
     "find a world that's worth watching before seeing it."
   - **Species accounting** — D057: species ID assigned at birth by descent plus a drift
     threshold from the species' founding genome; pure instrumentation (a `species`
     count column, per-species longevity), rides on the lineage events above and supplies
     the component definition the Bedau/MODES instruments need. The distance metric and
     θ calibration are specified in the D-entry.
   - **Dynamic mutation rate, per-creature form only** — D056: stress-coupled at
     conception or a self-adaptive heritable rate, never a global controller; needs a
     literature round (SOS response, self-adaptation) and its own calibrated baseline.
     D056 also names the pre-goal contingency: if a round fails on chains arriving too
     late, the next round raises cell-type mutation 5–10× with an `inherit`-stability
     prediction attached.
   - **Standing methodology debts:** the margin-ratio ≥ 2 preflight should be computed and
     printed by `run-arm.ps1` rather than remembered; `Mutator.CodeVersion` promises a
     per-birth record that is recorded nowhere — write it or delete the promise.

## How the experiments are run — the parts that bite

- One arm per worker (`unity/` = worker 1, `unity-w2..w5`), **at most five at once**, launched
  with `scripts/run-arm.ps1`. Workers are copies: after any change under `unity/Assets`, run
  `scripts/new-worker.ps1 -Workers 2,3,4,5` before launching on them. `src/Evosim.Core` is
  shared as a `file:` package and needs no copy.
- **Every setting is verified from the run header, never from the launch command.** Defaults
  that do not print and matter: nutrient sink 0.02 m/s, `MatterMixingDiffusivity` 2, layer 1 m,
  depth 60 m, area 400 m², cell-type mutation 0.001.
- **Throughput is population.** A world of 4,000–5,000 creatures runs at 0.2–2× real time
  depending on how many arms share the machine; 30,000 simulated seconds took 5–10 hours. The
  0.02 world grows large. Budget the wall clock for that, and read a wall-ended arm as
  censored, never as an outcome.
- **The population ceiling ends a run as a runaway.** It is `MaximumPopulation` (5,000 by
  default, `EVOSIM_MAX_POP`), an instrument limit, not biology; every pre-registration
  classes hitting it as uninterpretable.
- **Founding takes 2 floor spawns per 0.5 s step**, so `EVOSIM_FLOOR_CLOSES` below 20 s
  leaves fewer than forty founders; 3,000 s is after founding and before the first crash.
- `scripts/read-arm.ps1` scores any report by column name against 0036's rule; extend it
  rather than eyeballing tables. Traces of a single arm are cheapest with an `awk` over the
  `|`-separated rows (column indices: 2 t, 3 alive, 4 births, 15 absorpt, 16 inherit, 20 det
  deep, 21 depth, 24 age, 31 mat top, 33 mat blk, 34 floor, 35 gen min).
- Monitors and `until` loops over `runs/*.md` and `%TEMP%\evosim-<arm>.log` are how a
  session waits on arms; `error CS` or `could not be found` in the log means the arm never
  ran.

## New since the last handoff-worthy commit (all committed on `main`)

- Code: `NutrientField.Remineralise` (D051, exact first-order, off by default);
  `RunConfig.NutrientRemineralisationPerSecond`, `MatterRemineralisationPerSecond`,
  `FloorClosesAfterSeconds`; `EVOSIM_REMIN`, `EVOSIM_FLOOR_CLOSES`, `EVOSIM_MAX_POP`; the
  `det deep` column (`detritusDeep` in stats.jsonl); an exact `ulong` seed parse;
  `FluidConfig.Clone` complete; `run-arm.ps1`'s busy check no longer matches worker paths as a
  prefix; `scripts/read-arm.ps1`. Core suite: 344 tests, ~1 min.
- Documents: D051 with its measured addendum; DESIGN.md §0f, §5A.2c, §5A.10; CLAUDE.md
  gotchas (floor ratchet at mixing 0 only; founding window; throughput; ceiling); literature
  review round 3 (`research/LITERATURE-REVIEW.md` §0, logbook/0035). The two 2026-08-29
  proposal documents (`fable-propose.md`, the plan the goal came from, and
  `sol-gpt-propose.md`, an owner-provided independent review) were absorbed into the
  queue above and removed on 2026-08-31; git history holds the full texts.

## A design gap the owner spotted, worth a D-entry before fix 3

Asked why sunlit producers stop breeding, the answer is D048's matter charge — and the owner
asked why the surface is that starved when a real ocean is not. The honest comparison: the
real sunlit ocean *is* a nutrient desert (the gyres), but most of what phytoplankton take
there is **regenerated in place within days** by grazers, bacteria and leakage — the
microbial loop; in oligotrophic seas the large majority of production runs on recycled
nutrient and only a minority sinks out. This world has no regeneration while alive at all:
matter leaves a body only at death, and the corpse sinks before it dissolves, so every unit
a producer takes at the surface stays locked until its owner dies at depth. That is why a
bloom here converts the whole surface reservoir into bodies and returns none of it where it
was taken, and why the drought lasts longer than a lifetime. (The reservoir is also small —
24,000 units against bodies that lock 0.5 per joule of tissue — where the real deep-ocean
inventory dwarfs living biomass.)

The fix this implies is the *excretion contract*, now **D052** in `DECISIONS.md` (decided,
not built): living creatures return matter continuously at their own depth in proportion to
upkeep, so a lit population regenerates its own surface. The owner's alternative — matter
minted from energy, the nitrogen-fixation analogue — is recorded there as rejected for now,
with the reason. Marked as project inference, not a cited claim — the literature review has
not searched marine nutrient regeneration; add a primary source (new vs regenerated
production) before the D-entry leans on the numbers above.

## Open decisions for the owner

- **Speed, and the game's clock (owner, 2026-09-03).** "All I want is the ability to
  simulate faster" — and, separately, a game in which people wait hours for something
  interesting is not a game. The first is done as far as a timestep can do it (logbook/0052:
  `EVOSIM_DT` 0.02 screens at ~3× the pace, 0.01 confirms, 0.05 is out; the metabolic step
  stays 0.5 s and is not the cost). The second is a world-rule question no timestep answers: at any speed a lifetime is
  3,000 simulated seconds and a chain has taken tens of thousands to appear. When the game
  framing becomes the goal, the levers are an ecology that is eventful on a human timescale
  (shorter lifetimes, faster turnover, more change per generation) and a theatre that runs
  the farm ahead and jumps to the events — DESIGN.md's farm/theatre split was made for
  exactly this. A cheap mover for jointless bodies (the deferred "Tier 2" of the 2026-09-03
  speed discussion: PhysX only for bodies that swim) becomes worth building at that point,
  not before.
- **Immigration as a world rule, for when the cell types expand (owner's hypothesis,
  2026-09-03).** "Having a flux of founders increases the chance that a founder of
  configuration A finds a good environment to thrive." Plausible as community assembly —
  propagule pressure is how real islands fill, and a body plan useless at t=0 can be right
  at t=20,000, which mutation from the resident lineage rediscovers slowly and a trickle of
  founders samples continuously; the more cell types, the stronger the argument. The
  floor's bad record (rescuing crashes; founders' children reading as "inherited") is an
  argument for scoring differently, not against immigration: tag immigrants and their
  descendants from `lineage.jsonl` (birth kinds and parent links are already there) and
  score **establishment** — a lineage ≥ 2 generations deep with measured R0 ≥ 1 — rather
  than presence. Not for the current goal (the assay showed the field, not supply, binds —
  logbook/0051); when the cell types expand, run the same world with and without a trickle,
  scored on established lineages net of immigrants, pre-registered. Cheap precursor: a
  calculator scan of random configurations against a run's field timeline, to see whether
  any body could have bred anywhere at any time.
- The paywalled reading list in `research/LITERATURE-REVIEW.md` §9, which needs the owner's
  institutional access.
- Pushing: the owner approved pushing code and prose in batches (2026-09-01, "Push all",
  including `inocula/`); data, run output and weights are never pushed. `main` is pushed
  through the round-10 launch.
- The untracked `sol-gpt-2026-08-31-122448-review.md` at the repo root is an owner-provided
  review; its fate (absorb or delete) is the owner's.

(Resolved since last written: fix 3's biology-vs-world call → D055 (world); the
ceiling/scoring question → D053 + D058.)
