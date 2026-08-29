# Handoff — where to pick up

**Written 2026-08-29, end of the autonomous session.** This is the one file a new agent should
read first. It says what was being attempted, how far it got, what is queued, and exactly how
to continue. It is a pointer, not a source of truth: the specification is `DESIGN.md`, the
reasoning is `DECISIONS.md`, the history is `logbook/`, and the operating rules are
`CLAUDE.md`. When this file and those disagree, those win.

## The goal that was being pursued

**A self-sustaining world that holds a food chain — verified across seeds, not observed
once.** Chosen by the owner on 2026-08-29 from the options in `fable-propose.md`. Scored by a
rule written before each round launched:

> In at least 3 of 5 seeds, with the population floor closed: the producers persist to the end
> of the run, an absorptive lineage is inherited for ≥ 20 consecutive samples, and it is still
> alive (≥ 10) at the last sample.

**Status: not met.** The failure has been narrowed to one mechanism with two faces, and the
owner's instruction was to work through three candidate fixes in order until one meets the
rule. Fix 1 was tested and failed; fix 2 is half-run; fix 3 is untried.

## What was established, in order (the logbook entries are the record)

| entry | round | what changed | what it found |
|---|---|---|---|
| [0036](logbook/0036-the-floor-gives-back.md) | 1 | D051 remineralisation (a floor→water leak) and — the thing that mattered — **mixing 0.2 m²/s**, a value nobody had run (only 0 and 2 ever had) | D051 is inert: `NutrientField.Mix` already exchanges across the floor. At mixing 0.2 the deep water crosses the absorptive break-even by t≈2,300–4,000 in every seed and **food chains appear** — 4 of 6 seeds, 3 from the world's own mutants. Every lineage booms then busts |
| [0037](logbook/0037-the-net-comes-down.md) | 2a, 2b | `FloorClosesAfterSeconds` — the population floor stops firing after founding | The floor had been running the founding lottery (40 random genomes breed in 1 seed of 4) **and** rescuing every matter crash. With it closed at 3,000 s, **3 of 5 seeds go extinct** at their first drought. One mutant chain arose with the net down and bust to zero |
| [0038](logbook/0038-a-lighter-world.md) | 3, 3b | fix 1: `excessDensity` 0.1 → 0.02, then 0.05 | At 0.02 producers stop dying and **run away** to the population ceiling (uninterpretable); one seed died of **age synchrony** instead. At 0.05 they die as at 0.1. No density gives a scoreable world on its own |
| [0039](logbook/0039-a-slower-drought.md) | 4 | fix 2: senescence 3,000 → 10,000 s, in the 0.02 world, ceiling 8,000 | **Two of five seeds run; three not launched.** See *Queued* |

**The mechanism, in one paragraph.** Surface matter runs out (D048's economy — producers
strip it), conceptions are refused, births stop. From there one of two things is
irreversible: at excess density 0.05–0.1 the bodies sink out of the photic band within
~1,000 s and age out in the dark while the matter recovers above them; at 0.02 they stay lit
but the drought has made the survivors one cohort, and under D038's wear a cohort past
~1,300 s has no surplus to breed with. Every extinction watched — eleven of them — has one of
those two signatures. Separately, every absorptive lineage that established (five of them)
ate the deep water from 20–38 J/m³ to ~4 in about 1,500 s and bust to zero or nearly so;
nothing damps a consumer here but its food.

## Queued — what to do next, in order

1. **Finish round 4** (fix 2). Launch `d054-s3`, `d054-s4`, `d054-s5` exactly as below, then
   score all five with `./scripts/read-arm.ps1 -Name d054-s1,d054-s2,d054-s3,d054-s4,d054-s5`
   against the S1–S7 table in logbook/0039, and write its *Results*. If S2 and S4 hold and
   S6 fails by bust, go to step 2. If S2 fails, try fix 2's other form (faster matter return —
   `MatterMixingDiffusivity` is 2 m²/s and has no env var; the lag is the population's, so
   consider `MatterSinkMetresPerSecond` or a matter analogue of the D051 leak, pre-registered).
   If S4 fails, the 0.02 world is not self-limiting and irradiance/shading is the next knob.

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

2. **D052 — the excretion contract.** Decided with the owner on the evening of 2026-08-29,
   not built. Living bodies return locked matter to their own layer in proportion to upkeep;
   death returns the rest; `ExcretionPerJoule` defaults to 0; `EVOSIM_EXCRETION` prints in
   the header. Conservation: debit `MatterInBodies` by exactly what the field is credited,
   and `MatterIsConservedBecauseNothingCreatesIt` must pass with the knob on. Three tests in
   the D051 shape (default unchanged, knob works, knob reaches the arithmetic). Then a
   pre-registered five-seed round in round 4's world (0.02, senescence 10,000, floor 3,000,
   ceiling 8,000) with the same success rule. This comes **before** fix 3.

3. **Fix 3 — damping on the consumer.** Untried, undesigned. The consumer–resource cycle here
   has no damping: `AbsorptiveCell` captures at a fixed clearance regardless of density and a
   lineage grows until the water is empty. Candidates the owner named: a density-dependent
   capture rate, or the deep water below break-even being less reachable. This needs a
   D-entry in `DECISIONS.md` (D052 is the next number; `d052`/`d053`/`d054` were *round*
   names, not decisions) and a pre-registration in the 0036–0039 style before any arm runs.

4. **After the goal:** `fable-propose.md` §4b–§8 (ballast, excretion contract, added mass,
   dt sweep, IL2CPP, aquarium) and the infrastructure items from `sol-gpt-propose.md` that
   were deferred as sound-but-not-on-the-path: typed run manifest with source fingerprint,
   compact `lineage.jsonl` birth/death events, a Unity boundary smoke test, physics timestep
   in the config hash. Its three small code points were fixed (commit 403e684).

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
  review round 3 (`research/LITERATURE-REVIEW.md` §0, logbook/0035); `fable-propose.md`
  (the plan the goal came from) and `sol-gpt-propose.md` (an independent review, owner-provided).

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

- Whether fix 3 should change biology (`AbsorptiveCell` capture) or the world (reachability of
  the deep water) — the two are different D-entries.
- Whether the 0.02 world's runaway is acceptable to score with a higher ceiling, or whether
  irradiance should come down first so the world limits itself.
- The paywalled reading list in `research/LITERATURE-REVIEW.md` §9, which needs the owner's
  institutional access.
