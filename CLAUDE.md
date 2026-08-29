# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Karl Sims–style evolved-virtual-creatures simulator in Unity. Genomes encode **both**
body plan and brain; creatures are grown from a recursive directed graph and evaluated in
physics. Aquatic locomotion first, terrestrial later.

**Six documents, deliberately non-overlapping.** Keep them that way — duplicated rationale
drifts and then none of it can be trusted.

| File | Answers |
|---|---|
| [`DESIGN.md`](DESIGN.md) | *What the system is* — the specification |
| [`DECISIONS.md`](DECISIONS.md) | *Why we chose this over that*, and what was rejected. Append-only; reversals get a new entry marking the old one superseded |
| [`research/LITERATURE-REVIEW.md`](research/LITERATURE-REVIEW.md) | *What the evidence says* |
| [`logbook/`](logbook/) | *What happened, and when* — dated entries on what was tried and what broke. Never a source of truth: it links to the documents above rather than restating them, and is the only one allowed to be out of date |
| [`primer/`](primer/) | *What the thing is, and why it is interesting* — explanatory prose for a reader, written after a mechanism works. Also never a source of truth. Anything it asserts that is not traceable to a cited source must be marked as inference, in the text and in its sources table |
| `CLAUDE.md` (this file) | *What will bite you* |

**Two licences.** Code is MIT ([`LICENSE`](LICENSE)); the prose — `DESIGN.md`,
`DECISIONS.md`, `README.md`, this file, and everything under `research/`, `logbook/` and
`primer/` — is CC BY 4.0
([`LICENSE-DOCS`](LICENSE-DOCS)). New files land under whichever applies; if you add a
directory that is neither clearly code nor clearly prose, say which it is in `LICENSE-DOCS`
rather than leaving it ambiguous.

**Commits are guarded.** `scripts/githooks/pre-commit` blocks copyrighted PDFs, secrets,
stray emails, Unity build output and files over 5 MB. Enable with
`git config core.hooksPath scripts/githooks`. Do not bypass it with `--no-verify`; if it
fires, the finding is real until proven otherwise.

**[`DESIGN.md`](DESIGN.md) is the source of truth.** Most decisions in it cite peer-reviewed
literature with page locators. Read it before proposing architectural changes; several
obvious-seeming ideas were already tested against the literature and rejected, for reasons
recorded there.

Current state: **the ecosystem runs.** Genomes develop into phenotypes, articulations swim
under their own evolved brains, and `Evosim.Core`'s world charges upkeep, feeds, breeds and
kills — the energy audit closes at 0.0000% across a food web that has twice assembled itself
(logbook/0025, 0028). Milestones 2–5 are done, out of the listed order; perception is partial
(four sensor channels read — `Chemical`, `Energy` and `Flow` do not). The open frontier is
DECISIONS.md D040–D050: no world yet holds a full ecology and a food chain at once, movement
has never paid its energy cost (the cost side is closed, the prize side is open), and
throughput binds every remaining question. Experiments are *arms*, launched with
`scripts/run-arm.ps1` against worker copies `unity-w2`..`unity-w7` — never two processes on
one worker, at most five concurrent arms, and verify every arm's settings from the header its
run report writes, not from the launch command.

## Commands

Unity is at `C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe`.

**Run the physics spike** (compiles, runs M1–M6, writes `results/`):

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
$proj  = 'd:\Projects\experiments\evolution-simulator\spikes\01-articulation-body'
$a = @('-projectPath',$proj,'-batchmode','-quit','-nographics',
       '-executeMethod','Spike.EditorTools.SpikeEntry.Run','-logFile',"$env:TEMP\spike.log")
Start-Process -FilePath $unity -ArgumentList $a -Wait -NoNewWindow
```

**Use `Start-Process`, not `& $unity ...`.** Direct invocation silently fails — Unity exits
without writing a log. Check compile errors with
`Select-String -Path $env:TEMP\spike.log -Pattern 'error CS'`.

**Run the Milestone 1 smoke test** (builds creatures, checks geometry, momentum and swimming):

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
$proj  = 'd:\Projects\experiments\evolution-simulator\unity'
$log   = "$env:TEMP\evosim-smoke.log"
$a = @('-projectPath',$proj,'-batchmode','-quit','-nographics',
       '-executeMethod','Evosim.Sim.EditorTools.Milestone1Smoke.Run','-logFile',$log)
Start-Process -FilePath $unity -ArgumentList $a -Wait -NoNewWindow
Get-Content $log | Select-String 'Milestone 1 smoke' -Context 0,140
```

Note `Evosim.Sim.**EditorTools**`, not `Evosim.Sim.Editor` — the folder is `Editor/` (which
is what makes it an editor-only assembly) but the namespace is not. Getting it wrong costs a
full asset reimport before Unity reports
`executeMethod class 'Milestone1Smoke' could not be found` and exits 1. That failure looks
exactly like a hang: `-batchmode` prints nothing to the console, so a cold run is several
minutes of silence whether it is working or not. Tail the log in a second terminal
(`Get-Content $log -Wait -Tail 5`) rather than guessing, and check for
`could not be found` as well as `error CS` — a missing method is not a compile error and
will not show up in the usual grep.

**Unity Hub CLI is broken** in Hub 3.14 — the Electron wrapper consumes the `--` separator
and treats `--headless` as a module path. Fails identically in PowerShell, cmd and bash.
Don't waste time on it; use the Hub GUI.

**Test `Evosim.Core`** (fast — the whole suite is well under a second):

```powershell
./scripts/core-test.ps1
./scripts/core-test.ps1 -Filter DevelopmentTests
```

**There is no .NET SDK installed system-wide on this machine** — `dotnet --list-sdks` is
empty, only runtimes are present. Unity ships a complete .NET 8 SDK at
`<Unity>\Editor\Data\DotNetSdk\dotnet.exe`, and `core-test.ps1` finds and uses it. Don't
install an SDK to work around this, and don't reach for the Unity Test Runner for anything
that belongs in `Evosim.Core` — running these outside the Editor is the whole point of
§6.1's no-`UnityEngine` rule, and it is the difference between a one-second feedback loop
and a thirty-second one.

## Architecture

### What exists

`src/Evosim.Core/` — genome (§4.1), development (§4.2), deterministic RNG (§7), and the
small amount of vector maths that follows from having no `UnityEngine`. `src/Evosim.Core.Tests/`
covers it. Plain .NET projects, `netstandard2.1` and C# 9, which is what Unity 6 consumes —
anything that builds here builds in the Editor.

`unity/` — the real Unity project. `Evosim.Core` is pulled in as a **local package** via
`Packages/manifest.json` (`file:../../src/Evosim.Core`), so there is exactly one copy of the
source and no DLL to keep in sync. Its asmdef sets `noEngineReferences`, which means Unity
refuses to compile Core if anyone reaches for `UnityEngine` — §6.1's rule is enforced by the
build, not by memory. `src/Directory.Build.props` redirects .NET output to `artifacts/` for a
related reason: a stray `Evosim.Core.dll` inside the package directory would be imported as a
plugin and collide with the same code compiled from source.

**The sandbox scene is generated, not hand-edited** — `Evosim/Rebuild Sandbox Scene`, or
`-executeMethod Evosim.Sim.EditorTools.SandboxSceneBuilder.Run`. Scene YAML merges badly and
cannot be reviewed in a diff; a script that rebuilds it can.

`spikes/01-articulation-body/` — a standalone Unity project, **disposable by design**. It
answers one question: can `ArticulationBody` support the evaluation loop at scale? It can;
see `results/FINDINGS.md` and DESIGN.md §11.1. Don't build features into it.

Two things in `Evosim.Core` that look like choices and are not:

- **`Rng` is PCG, not `System.Random`.** `System.Random`'s algorithm is not contractually
  stable across .NET versions and .NET Core changed it. §7 promises a seed means a
  sequence; that only holds with a fixed integer recurrence.
- **`Developer` keeps scale out of the transform matrix.** The matrix carries rotation,
  reflection and translation, so it stays orthogonal up to sign and decomposes cleanly;
  accumulated scale is folded into half-extents and the anchors derived from them. Baking
  scale into the matrix makes every anchor computation depend on decomposing a sheared
  basis. Reflection already forces the matrix to go improper — that is what
  `PhenotypePart.Mirrored` records.

### What is planned

Five assemblies (DESIGN.md §6.1). The load-bearing split:

- **`Evosim.Core`** — genome, development, mutation, MAP-Elites archive. No `UnityEngine`.
- **`Evosim.Sim`** — phenotype builder, environments, evaluator, tiling. Unity.
- **`Evosim.Farm`** — headless orchestration, island model.
- **`Evosim.Theatre`** — replay, gallery, charts, fluid validation harness.

Two structural principles that are easy to violate:

1. **The farm and the theatre are separate programs.** Evaluation is headless, ugly, fast;
   presentation is slow, beautiful, and reads stored genomes. They share only a
   serialization format. Conflating them is the classic failure mode of this genre.
2. **The archive is not just a gallery.** MAP-Elites cells provide morphological innovation
   protection — a mutant with a novel body competes only within its own cell, never against
   the global champion. That is why plain GA is not an option here (DESIGN.md §2).

## Research provenance

`research/` holds a literature review that directly shaped the design.

- **`LITERATURE-REVIEW.md`** — PRISMA-style report; §7 states the limitations honestly.
  **It is a living review extended in discrete rounds, not a static report.** Adding papers
  means running the update protocol in §3.5 and following the rules in §0 — append a round
  row, update the cumulative sections (question status, flow counts, synthesis matrix,
  bibliography, threats, gaps), and record any design impact in *both* this file's round
  table and DESIGN.md's changelog. Silently appending a paper without touching §7.3's
  "small n" claim makes the review dishonest. Superseded claims get struck through, not
  deleted — the record of being wrong is the reason the current draft is trustworthy.
- **`FETCH-RESULTS.md`** — exact retrieval URL for every paper. This is the
  reproducibility record; PDFs are not committed.
- **`research/papers/`** — **gitignored and must stay that way.** Copyrighted publisher
  PDFs plus their extracted markdown. Two were obtained via institutional access.
  Machine-converting a paywalled paper to markdown does not make it redistributable.

### Citation convention

Claims cite `[KEY §section, p.N]` where **`p.N` is the PDF page**, matching the
`### Page N` heading in that paper's extracted `source.md`. So any claim traces to an exact
page. Preserve this when editing DESIGN.md.

DESIGN.md §13.4 quarantines references seen only inside *other* papers' bibliographies,
marked not-independently-verified. Do not promote anything out of that table without
actually verifying it.

## Gotchas discovered the hard way

- **PhysX sleeps undriven bodies.** The first spike run reported 64 creatures costing less
  than one driven chain, because with zero gravity and no actuation everything settled and
  slept. Any physics benchmark here must actuate the creatures and assert they are awake —
  `SpikeHarness.M3` reports mean body speed for exactly this reason.
- **Unity's physics defaults are not neutral, and they are not specified anywhere.**
  `ArticulationBody.angularDamping` and `jointFriction` both default to `0.05`, and
  `Physics.defaultMaxDepenetrationVelocity` to `10`. The first two are a second drag model
  acting on top of DESIGN.md §5.2's — they removed roughly ten times more energy than the fluid
  did, invisibly, from Milestone 1 until an energy audit found them (logbook/0008). Anything the
  engine does that the design did not ask for is a fault, including doing nothing visibly wrong.
- **Identical numbers across a configuration change mean the change was not applied.** This has
  now happened twice: self-collision "made no difference" because the scene state was being
  inherited rather than set (logbook/0007), and a 40x drive-strength sweep returned byte-identical
  results because `TorqueScale` was read once in a constructor while every call site set it by
  object initializer afterwards (logbook/0008). Before concluding that a parameter does not
  matter, prove it reached the thing it configures.
- **Suspiciously good results are usually a broken measurement.** The budgets in
  `spikes/01-articulation-body/README.md` are derived backwards from throughput targets;
  beating them by 100× means the harness is wrong, not that the hardware is amazing.
- **Never run `-batchmode` against a project the Editor has open.** Two Unity processes
  sharing one `Library/` corrupt it, and the symptom arrives later as *"Corrupted Library
  Detected"* on the human's next open. Check first:
  `Get-Process Unity -ErrorAction SilentlyContinue`. The damage is cheap — `Library/` is
  gitignored because it regenerates from `Assets/` + `ProjectSettings/` +
  `Packages/manifest.json`, so *Rebuild Library* is always the right answer — but the
  interruption is not.
- **`-createProject` gives you the Built-In Render Pipeline**, which DESIGN.md §10 does not
  want and Unity 6.5 deprecates. URP has to be added deliberately; `Evosim/Set Up URP`
  generates the pipeline assets.
- **A morphology share is contaminated by the population floor.** `MinimumPopulation` (40)
  trickles fresh generation-zero founders in whenever a world drops to it, and founders carry a
  joint about two times in five. So in a bottlenecked world the jointed *share* is largely a
  readout of the founder draw, and it sawtooths — five apparent "muscle recoveries" in one run,
  one reaching 59% at generation 28, were all the floor (logbook/0029). `FloorSpawnsPerStep` = 2
  prevents synchronous cohort *death* and does nothing about this. Read `jointedInherited` /
  `absorptiveInherited` — creatures whose parent had the trait — not the share; and treat
  `gen min = 0` as "founders present, share meaningless".
- **The world has no top, and three separate clamps hide it.** `LightModel.IrradianceAt` returns a
  constant for `heightY >= 0`, `NutrientField.LayerOf` puts everything at or above 0 in layer 0, and
  `FluidEnvironment` bounds y not at all. Each is reasonable alone; together they make the region
  above the waterline an unbounded ray on which every point is physically identical to floating at
  y = 0, while the physics keeps integrating. D049's first buoyancy probe climbed 155 m into it in a
  world whose habitable band is 23.7 m deep, paying upkeep the whole way for a position no different
  from the surface (logbook/0034). D050 stops *upward* net force at y = 0. Anything else that can
  push a creature up — an effector channel, a current — needs the same question asked of it.
- **The sea floor is a ratchet at mixing 0 only.** `NutrientField.Mix` runs across every interface
  including the floor's, so at any `NutrientMixingDiffusivity` above zero the floor already gives
  back — 20%/s of its excess at 0.2 m²/s. The 80–93% on-floor figures in DESIGN.md §5A.2c and the
  66–76% in the D050 arms are all mixing-0 worlds; at 0.2 the floor holds 5–7%. D051's
  remineralisation leak was built on the premise that nothing leaves the floor, and was measured
  redundant the same day (logbook/0036). Before building a mechanism on a code fact, read the loop
  bounds, not the method's shape — a reconnaissance pass quoted `Mix` and still missed this.
- **The population floor does two jobs, and closing it exposes both.** It runs the founding
  lottery — forty random genomes breed in about one seed in four, and the floor keeps drawing
  until one does — and it rescues every matter crash. `FloorClosesAfterSeconds` (`EVOSIM_FLOOR_CLOSES`)
  stops it; founding takes 2 spawns per 0.5 s step, so anything under 20 s leaves fewer than forty
  founders, and 3,000 s is after founding and before the first crash. With it closed, three seeds in
  five die at their first drought (logbook/0037). A world that needs the net is not self-sustaining.
- **Throughput is population, and the ceiling is an instrument.** Four to five thousand creatures
  run at 0.2–2× real time depending on how many arms share the machine; 30,000 simulated seconds
  took five to ten hours. `MaximumPopulation` (5,000, `EVOSIM_MAX_POP`) ends a run as a *runaway* —
  censored, not an outcome — and the light 0.02 world reaches it by t≈5,500 (logbook/0038).
- **`gen min = 0` has two causes and they need opposite responses.** CLAUDE.md's floor rule reads it
  as founder contamination, which is right when the floor is firing. With `EVOSIM_SENESCENCE` off,
  founders simply never age out, so `gen min = 0` persists in a world whose floor stopped firing at
  t=400 and whose traits are entirely inherited. Read `floor` and `*Inherited` together: floor 0 plus
  82-of-88 inherited is a lineage, whatever the generation minimum says.
- **PowerShell scripts need a UTF-8 BOM.** Windows PowerShell reads a BOM-less `.ps1` as ANSI, so
  an em-dash inside a double-quoted string becomes three bytes that terminate the string and the
  file will not parse — the error points at the following token and says nothing about encoding.
  The three scripts in `scripts/` carry BOMs; keep it that way when adding one, since this
  project's scripts are written with prose in them. `[ulong]` is also PowerShell 7-only — use
  `[uint64]`, which both editions accept.
- **`windows-il2cpp` is not installed** — only Mono. Fine for now; add it before the island
  model (Milestone 4), since per-creature brain evaluation is managed C# in the hot loop.

## Conventions

- Simulation output (`runs/`) and spike CSVs are gitignored; `FINDINGS.md` is tracked
  because DESIGN.md links to it.
- Genomes serialize to **JSON**, not binary — readable, diffable, and hand-written rather than
  via a library, because `Evosim.Core` has no dependencies and that is what keeps its tests at
  one second. `Json`, `GenomeJson`, `RunConfigJson`, `CellTypeJson`, `RunDirectory`.
- **A run is a directory, and its two high-volume files are append-only JSONL.** `config.json`
  (indented, hand-editable, carries its own hash), `lineage.jsonl` (one row per creature ever
  born), `stats.jsonl` (one row per sample), `snapshots/`. A killed run leaves every completed
  row valid; a single rewritten document would leave a truncated file that parses as nothing.
  Creatures are **rows, not files** — a genome measures ~5 KB and the working estimate is 40,000
  births an hour, so one file each is 40,000 files and 200 MB per hour.
  - Compact mode is not cosmetic: **one row must be one line.** `JsonlWriter` refuses a row
    containing a line break, because one embedded newline makes every row after it unreadable.
  - Read a live run with `JsonlWriter.ReadRows`, not `File.ReadAllLines` — the latter opens with
    `FileShare.Read`, which will not coexist with the writer and throws a sharing violation.
- **Two reflection-driven tests guard the config.** `RunConfigTests` checks every tunable
  reaches `RunConfig.Hash()`; `RunConfigJsonTests` checks every tunable survives a save and
  reload. Add a property to `RunConfig` or `RandomGenomeOptions` and forget either and they fail
  immediately — which is how `MaxEdgesPerNode` and `FluidConfig.PanelsPerAxis` were both caught.
- **Loading refuses rather than defaults.** A missing field throws and lists what was present;
  enums serialize by name, not ordinal. A genome that loads with one field silently defaulted is
  a different creature wearing the original's identity, measured and filed under the stored
  genome with nothing downstream able to notice.
- Every evaluation must be reproducible from `(genome, seed, configHash)`. PhysX is not
  bitwise deterministic across machines or Unity versions, so the hash exists to *detect*
  mismatches rather than to promise portability.
