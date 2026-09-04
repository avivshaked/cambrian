# Handoff — where to pick up

*Rewritten 2026-09-04 from the current state, replacing four layered revisions. The
record is the logbook and DECISIONS.md; this file only says where things stand and what
is queued.*

## Status

**The standing goal is met.** D063, as amended 2026-09-04: in ≥ 3 of 5 seeds with the
population floor closed, one *connected* absorptive clade is alive for ≥ 20 consecutive
samples to the end, holds ≥ 10 living members through the last two lifetimes (6,000 s),
and has an inherited absorptive birth inside the clade in the last 20 samples, on an
inherited photosynthetic lineage. Round 18 ([logbook/0054](logbook/0054-the-confirmation.md))
passes 4 of 5 at dt 0.01, 30,000 s, with exudation 0.15 (D070) — a discovery-regime pass
(cell-type mutation 0.005), labelled so. Three of the four clades trace to founder-era
stomachs the leak kept alive; one is mutant-rooted. The owner ruled that the root's origin
is not part of the goal.

**What the pass taught, in one line each:**

- The second trophic level was fed at ~1% of the first because producers fed the water
  only by dying; real producers leak 10–20% of production while alive (D070, review round 5).
- The population plateau every round since D065 read as carrying capacity is the matter
  cap: ~6,000 units, ~5,500 locked, so ~1,900 bodies (D071).
- The physics replays bit for bit on one build; any per-step change is a butterfly, so
  0.02 screens (~3×), 0.01 confirms, and a per-seed A/B of anything in the physics loop is
  unanswerable ([logbook/0052](logbook/0052-the-coarse-step.md), DESIGN.md §7).
- Futility stops are negative results on merit, not censored; censoring is for error and
  fault only (owner, D069).

## Queued — the current path, in order

1. **The contract repairs build landed** (commit `5c6c035`): manifest, `stop-arm.ps1`,
   `-ExpectSimHash`, `photo` columns, invariant culture, the step in the hash. Every
   config hash changed across it. Remaining: a photosynthetic flag on lineage rows so the
   producer clause's birth half can be read (`scripts/clade-score.ps1` says so).
2. **The matter screen** ([logbook/0055](logbook/0055-the-dry-deep.md)) **read 2026-09-04:
   not adopted.** The free matter pool is 10% of the stock at any sink speed; the lever
   must change the pool. The next lever is a world rule (price, initial stock, excretion,
   or the vent as a source with burial) and is the owner's; every option grows the
   producer population, so it is chosen together with `EVOSIM_MAX_POP`.
3. **Round 18's producer clause** was scored from `alive` because the `photo` columns did
   not exist; once they do, either note it in 0054 or re-read one seed.
4. **The scorer is `scripts/clade-score.ps1`** (all five clauses; producer birth half
   pending the lineage flag). `scratch/matter-profile.py` reads `matterHere` from
   `stats.jsonl`, which the table does not carry — worth a `mat here` column.
5. **The next scored goal: held by the owner** until the screen answers. Candidates the
   record names: movement that pays (the prize side — DECISIONS.md D040–D050, 0049's
   reading), late invasion (the assay at 0.15 run two lifetimes past inoculation, 0051's
   amendment 2: R0 0.72 over completed members, 112 alive at 20,000 s), and the cell-type
   expansion.

## The decision in front of the owner

`fable-propose-matter-economy.md` (2026-09-04, after logbook/0055 and 0056): at the
plateau matter binds and every solvent body has the same fecundity whatever it earns,
so the energy economy selects only for not starving; the age queue (D072) was the
accidental route from energy to fecundity and the stomachs' lifeline. Three shapes —
a stock large enough that light binds first, scarce matter allocated by energy reserve,
or the queue specified as the rule — with the agent's recommendation (the second,
screened against the first). Nothing launches until it is ruled.

## Bugs to chase

- **A NaN drag force at dt 0.02** (`r20q-s1`, t=15,345, logbook/0056): `FluidEnvironment.Apply`
  handed PhysX `{NaN, NaN, NaN}` for one part of a jointed body and the height went
  non-finite. First physics divergence in a scored run; 0052 saw crashes only at 0.05
  without the limiter. Reproduce from the manifest (seed 1, config hash, commit `23a6bd8`,
  `EVOSIM_CONCEPTION_ORDER shuffled`) — it replays bit for bit — and read the panel maths
  for a zero-length or zero-area case before blaming the step.
- **The error manifest omits the footer facts**: `physicsSteps`, `dragImpulsesLimited`
  and the counts are written as zeros on the error path. Carry the last known values.

## How the experiments are run — the parts that bite

- Workers are copies `unity-w2..unity-w7`; one arm per worker, **at most five at once**,
  launched with `scripts/run-arm.ps1`, logs in `scratch/logs/`. After any change under
  `unity/Assets`, `scripts/new-worker.ps1 -Workers N` once per worker (it exits 1 on
  success) and the hash check; from the manifest build on, `run-arm.ps1 -ExpectSimHash`.
- **Every setting is verified from the run header, never from the launch command**, and
  from the manifest's `simHash` once it exists.
- Read reports with `scripts/analyse-arm.ps1` by column name (`-ListColumns`); never
  positionally. `mat blk`, `floor` and the `det in/out/exuded` columns are per-window
  deltas. Pass `-Columns` as a real array from inside PowerShell — through `pwsh -File`
  the comma list arrives as one string and every cell reads `?`.
- `scripts/ledger.ps1` before a worker (D069); `scripts/absorptive-log.ps1 <arm>` for what
  each stomach earned and where; `scripts/lineage-invasion.ps1` for an inoculated lineage.
- The monitor is `scratch/monitor-r13.sh` over `scratch/evosim-watch-arms.txt`; it exits
  when the list is empty and must be restarted after the list is set. Stall rule and the
  wedge discriminator are in CLAUDE.md's gotchas.
- Throughput is population: ~1,800 bodies at dt 0.01 with five arms sharing the machine
  is 5–6 h per 30,000 s; dt 0.02 is ~3× that pace. `MaximumPopulation` (8,000 in the
  reference world) ends a run as a runaway, censored.
- The Bash tool mangles long heredocs (quotes, backslashes); write scripts and prose with
  the Write tool, apply edits with a small python file, and delete it.

## Open decisions for the owner

- **A vent that adds matter, paired with burial at the floor** — filed as a future
  experiment (owner, 2026-09-04), in D071's deferred list; after the screen.
- **The matter economy (after 0055 and 0056)** — see the section above and the
  proposal file. Overtakes the pool levers as the question: enlarging the pool changes
  the world's size, not who wins.
- **The next scored goal** — held; 0055 has read.
- **Speed, and the game's clock (owner, 2026-09-03).** The timestep has done what a
  timestep can (0052). A game that is eventful on a human timescale is a world-rule
  question: shorter lifetimes, faster turnover, a theatre that runs the farm ahead and
  jumps to events. A cheap mover for jointless bodies becomes worth building then, not
  before.
- **Immigration as a world rule when the cell types expand (owner's hypothesis,
  2026-09-03).** Score establishment (a lineage ≥ 2 generations deep with R0 ≥ 1), tag
  immigrants from `lineage.jsonl`, run with and without a trickle, pre-registered.
- The paywalled reading list in `research/LITERATURE-REVIEW.md` §9 needs the owner's
  institutional access.
- Pushing: code and prose in batches is approved (2026-09-01); data, run output and
  weights are never pushed. `main` is pushed through 0055's pre-registration.
- The untracked `sol-gpt-2026-08-31-122448-review.md` at the repo root is the owner's;
  absorb or delete is their call. The 2026-09-03 review was evaluated in session: findings
  1, 2, 6, 7 adopted or building; 3 rejected with the owner's reasoning in D069; 4 and 5
  overtaken.
