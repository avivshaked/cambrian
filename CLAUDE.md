# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this
repository — and to any other coding agent: `AGENTS.md` points here, and everything below
is agent-agnostic unless it names a tool. **Durable project knowledge lives in this repo,
never only in an agent's private memory** — a lesson costs too much to learn twice because
it was stored somewhere one particular agent on one particular machine could see.

## What this is

A Karl Sims–style evolved-virtual-creatures simulator in Unity. Genomes encode **both**
body plan and brain; creatures are grown from a recursive directed graph and evaluated in
physics. Aquatic locomotion first, terrestrial later.

**Eight documents, deliberately non-overlapping.** Keep them that way — duplicated rationale
drifts and then none of it can be trusted.

| File | Answers |
|---|---|
| [`DESIGN.md`](DESIGN.md) | *What the system is* — the specification |
| [`DECISIONS.md`](DECISIONS.md) | *Why we chose this over that*, and what was rejected. Append-only; reversals get a new entry marking the old one superseded |
| [`research/LITERATURE-REVIEW.md`](research/LITERATURE-REVIEW.md) | *What the evidence says* |
| [`logbook/`](logbook/) | *What happened, and when* — dated entries on what was tried and what broke. Never a source of truth: it links to the documents above rather than restating them, and is the only one allowed to be out of date |
| [`primer/`](primer/) | *What the thing is, and why it is interesting* — explanatory prose for a reader, written after a mechanism works. Also never a source of truth. Anything it asserts that is not traceable to a cited source must be marked as inference, in the text and in its sources table |
| `CLAUDE.md` (this file) | *What will bite you* |
| [`STYLE.md`](STYLE.md) | *How we write* — the voice, the shape of a piece, the tells to avoid, and the rules for restyling what exists. Applies to every prose file here, this one included |
| [`HANDOFF.md`](HANDOFF.md) | *Where work stands right now*, and what is queued. The one file that is rewritten rather than appended to |

**Two licences, both non-commercial since 2026-09-07 (D080).** Code and the genome files under
`inocula/` are PolyForm Noncommercial 1.0.0 ([`LICENSE`](LICENSE)); the prose — `DESIGN.md`,
`DECISIONS.md`, `README.md`, `STYLE.md`, `HANDOFF.md`, this file, and everything under `research/`, `logbook/` and
`primer/` — is CC BY-NC 4.0 ([`LICENSE-DOCS`](LICENSE-DOCS)). `COMMERCIAL.md` draws the
commercial line and `CONTRIBUTING.md` states the grant a contribution carries; a contribution
without that grant is not merged. New files land under whichever applies; if you add a
directory that is neither clearly code nor clearly prose, say which it is in `LICENSE-DOCS`
rather than leaving it ambiguous. Versions at or before `c8f9c98` were MIT and CC BY 4.0 and
stay so for the copies already made.

**Commits are guarded.** `scripts/githooks/pre-commit` blocks copyrighted PDFs, secrets,
stray emails, Unity build output and files over 5 MB. Enable with
`git config core.hooksPath scripts/githooks`. Do not bypass it with `--no-verify`; if it
fires, the finding is real until proven otherwise.

**[`DESIGN.md`](DESIGN.md) is the source of truth.** Most decisions in it cite peer-reviewed
literature with page locators. Read it before proposing architectural changes; several
obvious-seeming ideas were already tested against the literature and rejected, for reasons
recorded there.

Current state: **the ecosystem runs**, in one shared box of water (D077) stepped on one
thread so that it replays (D078). Genomes develop into phenotypes, articulations swim under
their own evolved brains, and `Evosim.Core`'s world charges upkeep, feeds, breeds and kills
with both books closed at every sample. The water is a grid of cells (D086, logbook/0078)
and bodies are born small and grow (D087, logbook/0081). The goal rule (D063 as amended) was
last met five seeds of five in round 30 (logbook/0075); the grid's base round 32 read two of
five with every mechanism prediction holding (0079); round 33, the growth base, met it five of five
and was the first world in which a trait moved by degree rather than by switch (0082). Then
the theatre showed every clade as a metre-wide column (0083): a newborn beside its parent
and a current that returned bodies. D088 (2026-09-10) replaced both, a newborn dispersed
over a disc and a current that carries in three dimensions, and the agent's own pictures
(0086, `scripts/theatre-snap.ps1`) show the fixed world filling the box. Round 35 is the
base round in that world (0087); which of the earlier readings survive it is 0084's list.
The open frontier: why the eaters' lines stop recruiting on the grid, whether a free joint
survives founding (round 34, logbook/0080), and movement, which has never paid its energy
cost (the cost side is closed, the prize side is open; `mean m/s` reads the water now).
Throughput still binds: dt 0.02
screens, 0.01 confirms (logbook/0052). Experiments are *arms*, launched with
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

**Stop an arm with `stop-arm.ps1`, never with `Stop-Process`.** Every run writes
`runs/<arm>/<run>/run.json` before its first step — arm, seed, config hash, and a `source` block
carrying the git commit, whether the code paths were dirty, and SHA-256 fingerprints of the Core
and worker source it is actually running — and rewrites it at an orderly end with `status`,
`reason` and the footer's facts as data. A killed process cannot do that rewrite, so a bare kill
leaves a manifest that says `running` forever and reads exactly like a crash or a live arm.
`stop-arm.ps1` writes first and kills second, merging `status: "stopped"`, the reason and
`stoppedAt` into the same file:

```powershell
./scripts/stop-arm.ps1 r17-s3 -Reason manual-futility   # or manual-stall, manual-other
./scripts/stop-arm.ps1 r17-s3 -Reason manual-stall -WhatIf
```

`run-arm.ps1` prints the worker's `simHash` at launch and again from `run.json`; pass
`-ExpectSimHash <hash>` to make a mismatch refuse the launch rather than waste a day — the
by-hand hash check the `new-worker.ps1` gotcha demands, turned into a precondition.

**Read a run report** with named columns — never index columns positionally; the table has
grown columns over time and a positional misread once reported float tissue as the food
chain (logbook/0044):

```powershell
./scripts/analyse-arm.ps1 r9-s1 r9-s2                 # status line per arm
./scripts/analyse-arm.ps1 r9-s1 -Timeline -Every 500 -From 11000 -To 16000 -Columns 'alive','depth m','mat blk'
./scripts/analyse-arm.ps1 r9-s1 -ListColumns          # the name -> index map
```

**Score a run by connected clade** (D063 as amended, logbook/0054's addendum) with
`scripts/clade-score.ps1` — the goal rule's `inherit` column is an aggregate across every
absorptive lineage in the world at once, so a set of unrelated short-lived clades can sum to
a passing streak and an unrelated late mutant can satisfy recruitment for a sterile cohort.
The clade scorer instead walks `lineage.jsonl`'s parent chains, builds every connected
clade with a living member at the run's last sample, and asks all of D063's clauses of each:
the 20-sample streak to the end, ≥ 10 alive at the last sample, an inherited absorptive
birth inside the clade in the last 20 samples, and the stability clause (≥ 10 members at
every sample in the last two lifetimes, t ≥ t_last − 6,000). A seed passes when any one
clade meets every clause (changed 2026-09-06 after the Sol/GPT review; until then only the
largest clade was asked, which can only under-report — no historical verdict moved, and
round 18's minima still print 48/41/24/127). It names the passing clade, or the best
failing one with its failed clauses, and still prints the largest clade's line for
continuity. It streams the file rather than loading it (a live run's `lineage.jsonl` can
run into the hundreds of MB). The producer clause prints three readings and none of them
decides the verdict yet: the owner's wording (an inherited photosynthetic line alive at the
end with an inherited photosynthetic birth in the last 20 samples), the ≥ 10-through-two-
lifetimes reading, and the population-only column reading (`photo inh` ≥ 10 at the last
sample and each of the last 20). The first two read the `pho` flag on lineage birth rows
(built 2026-09-06, after D078; `coreHash bff3d696…` onward) and print `flag absent` on
every run recorded before it; the third prints `column absent` on a report older than the `photo`
columns. The verdict line ends `PROVISIONAL` on an arm whose manifest still says
`running` (the lineage runs ahead of the report by up to a sample, and a birth after the
last sample does not recruit) and `no manifest` on a fixture; the recruitment window is
20 sampling intervals read from the report, bounded at the last sample (2026-09-07).
Fixture tests live in `scripts/tests/clade-score/` (`run-tests.ps1`); run them
after touching the scorer.

```powershell
./scripts/clade-score.ps1 r18x-s1 r18x-s2 r18x-s3 r18x-s4 r18x-s5
```

**Ask the ledger before asking a worker** (D069). One body's energy ledger, alone, under
`World`'s own breeding rule — net watts, break-even density, lifetime, R0 — in seconds and
without Unity. Use it to screen any knob that touches income or cost before spending a
day of machine time on it:

```powershell
./scripts/ledger.ps1 -Genome scratch/some-genome.json -Config runs/r13a-s2/<run>/config.json `
    -Clearance 1,5,10 -Depth 0,12 -Density 0.5,1,2,4,7,10 -Compare
./scripts/lineage-invasion.ps1 r15i-c10     # an inoculated lineage's R0, generations, alive-at
```

A genome file is one JSON line — any row of a run's `snapshots/*.jsonl` will do. What the
calculator does not have: the matter draw at conception, shading, field depletion, drift.

**Watch a run** (D075's first cut). The theatre is a separate program from the farm: it reads
a run directory and writes nothing into it. Open `unity/Assets/Scenes/Theatre.unity` in the
Editor (rebuild it with `Evosim/Rebuild Theatre Scene`), put a run directory — or the arm
directory above it — in the Theatre Runner's `Run Directory`, and press Play. **Mode B** rebuilds
the world from `config.json` plus `run.json`'s seed and step and steps it with rendering on; the
HUD's identity check compares `alive`, `births`, `deaths`, `auditResidual` and `meanHeight`
against the run's own `stats.jsonl` at every sample, so a viewer knows whether they are watching
the run or a cousin of it. **Mode A** grows one genome from a snapshot row and drives it under its
own brain, alone, with no economy. Both refuse a run this build did not record unless
`Allow Source Mismatch` is ticked, in which case the overlay says it is not a faithful replay.
**The skin** lives beside them under `unity/Assets/Theatre/` and moves no hash, so it cannot
orphan a recording: dark-field lighting and fog, rounded meshes generated at start, a neck at
every joint, marine snow and a sand bed, all from `TheatreSkin.cs` and `TheatreMeshes.cs`.
`TheatreBody.shader` is the one body material, carrying the guild on a Fresnel rim over faked
subsurface and a Voronoi mottle. Since the second day it also **carves** each body inward by two
octaves of noise in the part's own object space, deepened at a joint anchor, with the normal
rebuilt per pixel from the same field. The depth is `EVOSIM_THEATRE_CARVE` (default 0.2, clamped
at 0.5) times the part's smallest half-extent, and the displacement is never positive, which is
the whole of what keeps a visual inside its collider. A sphere part is drawn as the genome's
three half-extents scaled so the longest semi-axis is the collider's radius: the genome's intent,
not the physics, which collides as the ball (`research/theatre-look/README.md` is the reading).

**An agent has no eyes, so it takes pictures.**
`./scripts/theatre-snap.ps1 r35tsmoke3 -At 300,600 [-Worker 6] [-Views side,top] [-Size 900x1600]`
runs `Evosim.Theatre.EditorTools.TheatreSnapshot.Run`, which replays the run in Play mode with the
identity check on and writes the box from the side, the end, the top and a corner at each named
second into `scratch/snaps/<arm>/`, each frame fitted from the run's own config and labelled with the
arm, the second, the living count and whether the replay is faithful. A world view at thirteen pixels a
metre cannot show a wrinkle. So a fifth view, **`close`**, frames the largest body and the
largest of its neighbours from a three-quarter angle at a distance that fills the frame, with no
box and no markers. It is a portrait of one crowd and never a census, it is never in the default
set, and `-Carve` sets the carve depth for the run. Its Unity command line is
`-batchmode` **without `-quit`** (the entry quits itself) and **without `-nographics`** (a picture
needs a graphics device); `Evosim/Theatre/Snapshot Now` takes the same four of the world on screen. **A worker's `Assets/Theatre` goes stale silently**: `new-worker.ps1` copies it with the rest, but
nothing checks it at a render the way `simHash` checks the simulation, so a worker refreshed before
a skin change replays faithfully in the old skin, and refuses a view it has never heard of (four
renders on 2026-09-10 died in a minute on `'close' is not a view`). Refresh the render worker
first; `Assets/Evosim` is unchanged by it, so the recording still replays.

Keys: `Space` pause, `[` `]` pace, `K` seek, `C` colour, `F` follow, `R` reload, `H` hide,
click to select; fly with `WASD`+`QE`, right-drag to look, wheel for speed. `EVOSIM_THEATRE_RUN`,
`EVOSIM_THEATRE_GENOME`, `EVOSIM_THEATRE_SEEK` and `EVOSIM_THEATRE_OVERRIDE` set the same fields
from a script. Both modes also run headless, which is how they are tested:

```powershell
$env:EVOSIM_THEATRE_RUN = "$PWD/runs/th-ref"
Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    '-projectPath', "$PWD/unity-w6", '-batchmode', '-quit', '-nographics',
    '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreIdentityCheck.Run',
    '-logFile', "$PWD/scratch/logs/theatre-identity.log")
```

That entry is the **edit-mode** check, and its verdict line now says so. It drives one physics step
per iteration of a loop of its own, so it passed for three days while the Play-mode replay was
parting from its recording (logbook/0083). `TheatreIdentityCheck.RunInPlayMode` is the other one.
Same environment, plus `EVOSIM_THEATRE_SAMPLES` (20) and `EVOSIM_THEATRE_WALL_MINUTES` (30), and
**launched without `-quit`**. It enters Play mode, lets the Theatre Runner's own `Update` step the
run many steps to a frame, prints the identity verdict at every sample, and quits the Editor with
0 or 1. `EVOSIM_THEATRE_PLAYMODE=1` sends the plain `Run` entry there instead.

**Test `Evosim.Core`** (the default run skips the `Slow` trait, which covers the calibration
sweeps, field experiments and the snapshot scan, and ran 592 tests in 58 s on 2026-09-09; the
development and ecosystem tests are seconds each, so use `-Filter` while iterating):

```powershell
./scripts/core-test.ps1
./scripts/core-test.ps1 -Filter DevelopmentTests
./scripts/core-test.ps1 -All
```

`-All` runs the slow experiments too, and takes much longer: a timed run on 2026-09-09 took
27 minutes beside five arms, with the calibration sweep alone at 20 minutes. Run `-All`
before a commit that touches the world.

**There is no .NET SDK installed system-wide on this machine** — `dotnet --list-sdks` is
empty, only runtimes are present. Unity ships a complete .NET 8 SDK at
`<Unity>\Editor\Data\DotNetSdk\dotnet.exe`, and `core-test.ps1` finds and uses it. Don't
install an SDK to work around this, and don't reach for the Unity Test Runner for anything
that belongs in `Evosim.Core` — running these outside the Editor is the whole point of
§6.1's no-`UnityEngine` rule, and it is the difference between a feedback loop of seconds
and one that starts with a Unity asset reimport.

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

`unity/Assets/Theatre/` — `Evosim.Theatre` and `Evosim.Theatre.Editor`, D075's first cut:
replay of a recorded world with the identity check on screen, one creature under its own brain,
the overlay, a free-fly camera. It references `Evosim.Sim` and `Evosim.Core` and nothing
references it — §6.1's separation, enforced by the asmdef. **It sits beside `Assets/Evosim/`
rather than inside it because `simHash` is a digest of every `.cs` under `Assets/Evosim`**: with
the theatre in there, editing a HUD label made every earlier recording refuse to replay and made
every refreshed worker report a new build to the farm's identity record, neither of which had
anything to do with the simulation. Charts, the gallery and §5.4's fluid validation harness are
not built.

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
- **`Evosim.Theatre`** — replay, gallery, charts, fluid validation harness. Replay exists
  (above); the rest does not.

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
  push a creature up — an effector channel, a current — needs the same question asked of it. The
  floor has the same hole downward: the nutrient fields clamp to their last layer but nothing stops
  a sinking body, and round 5b's last survivors died at −131 m in a 60 m world (logbook/0040). Depth
  statistics from a dying world include water that does not exist.
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
- **A wedged sim loop never reaches its own wall-clock check.** The budget and wall checks
  live inside the metabolic loop, so a hang freezes them too: d056b-s1 sat at t=22,700 with
  the process alive and the log silent for 5.7 hours (logbook/0043's instrument note — the
  campaign's first hang, cause unknown). Every monitor watching an arm therefore needs
  three alternations, not two: error signatures, the `**Ended:**` footer, **and a
  staleness check on the report's byte size, not its mtime** — the wedge keeps touching
  mtime with zero content, which fooled the original mtime rule. Threshold ≥ 30 min: under
  full machine load a healthy heavy arm can legitimately take > 16 min per table row, which
  produced a false stall alert that nearly killed a live run. So an alert is a *suspicion*,
  not a verdict — confirm with the discriminator before killing: sample the report's byte
  size and the process's cumulative CPU 90 s apart; wedged = zero byte growth **and** high
  CPU delta (the loop spins without simulating); slow-but-alive = a row appears or CPU is
  quiet. After killing a wedged worker, refresh it — the Library dies with the process.
- **PowerShell scripts need a UTF-8 BOM.** Windows PowerShell reads a BOM-less `.ps1` as ANSI, so
  an em-dash inside a double-quoted string becomes three bytes that terminate the string and the
  file will not parse — the error points at the following token and says nothing about encoding.
  The three scripts in `scripts/` carry BOMs; keep it that way when adding one, since this
  project's scripts are written with prose in them. `[ulong]` is also PowerShell 7-only — use
  `[uint64]`, which both editions accept.
- **`new-worker.ps1 -Workers 2,3,4` does nothing when run through `powershell -File`.** The
  comma list arrives as one string, fails to bind to `[int[]]`, and the script exits 1 — which
  is also its exit code on success, so nothing distinguishes the two. Six workers were "refreshed"
  this way and every one still carried the previous `EvolutionRun.cs`; the hash check caught it.
  Call it once per worker from a shell, or from inside PowerShell with a real array. The hash
  check is not optional.
- **The species column reads 1 unless `EVOSIM_SPECIES_THETA` is set.** `SpeciesDriftThreshold`
  defaults to 0, at which `AssignSpecies` gives every creature species 0 — the instrument is
  off, not reporting one species. Every arm through round 13 ran at 0; calibrate with the
  `SpeciesCalibration` test's distribution before reading the column as diversity.
- **A lineage dissection can answer less than it looks like it can.** `lineage.jsonl` rows carry
  birth time, parent, kind, generation, species, the expressed `abs`/`jnt`/`pho` flags (`pho`
  from 2026-09-06's build only) and the patch —
  no depth, volume or energy per creature — and every death reads `starved` because `Starved` is
  the only `DeathCause` implemented, so cause of death discriminates nothing. `snapshots/` hold
  each living creature's *genome graph*, not its developed phenotype (a creature can carry an
  absorptive node it never expressed, and read `abs=0` — one concrete route: the node's accumulated
  edge scale takes its part below `minPartVolume` and development prunes the subtree, so a mixotroph
  genome develops into a pure leaf; seen in `r14c10-s4`'s snapshot), and snapshot rows carry an
  organism id that joins to `lineage.jsonl` from genome format 4 (`SnapshotJoinTests`) but no
  reserve, age, position or field state, so a snapshot resumes nothing: it is a genome pool.
  Body-size-by-guild is therefore not measurable from a run's output; say so rather than
  proxying (logbook/0048's dissection). **Depth by guild stood in the same place until
  2026-09-10 and no longer does**: `positions.jsonl` carries every living body's place and its
  three guild flags at every sample, so depth by guild, the footprint, the spread and the
  median nearest neighbour are all read from a run with `scripts/positions-read.py`. Only in a
  shared world, where a coordinate is a place rather than a lattice artefact, and only from that
  build: a tiled world and every run recorded before it carry no such file.
- **PhysX replays bit for bit on this machine, so every per-step change is a butterfly.** Same
  genome, seed, config *and build* give the same run report to the last decimal (`r16dt-01c` ≡
  `-01d` ≡ `-01e`, logbook/0052). Change any per-step term — 68 capped drag impulses at dt 0.01,
  432 at 0.02 — and the same seed becomes a different chaotic realisation: ±20% population, 4 m of
  depth, half the larder by t=5,000. A per-seed A/B on anything that touches the physics loop
  therefore cannot separate the change from the realisation; compare distributions across seeds,
  or hold the difference against that wingspan. `EVOSIM_DT` 0.02 is the screening step (deviations
  inside the wingspan, ~3× the pace); 0.01 confirms and is the only step at which the historical
  record replays; 0.05 is out (population migrates into the surface film and the audit opens). The
  drag limiter engages only above 0.01 for exactly that reason. **0.02 is bimodal on depth**: three
  of six fast-step worlds on 2026-09-04 sat in the surface film at −1 m where their 0.01 seeds sat
  at −12 to −15 m, and the two arms of one seed pair sat 10 m apart (logbook/0056). 0052's
  wingspan check did not see this. A 0.02 screen answers a mechanism question read within one
  step; it does not stand in for the 0.01 world, and a 0.02 result about depth, light or the film
  is not a result. One 0.02 arm also diverged — a newborn's
  143-gram link spun up by thousands of rad/s in one step (`r20q-s1`, logbook/0059). Since then a
  non-finite body is dumped to `runs/<arm>/<run>/diverged/` and killed as a counted `Diverged`
  death (read the `diverged` column; a run with any is read with that caveat), and at steps above
  0.01 a drive impulse limiter caps each joint at 30 rad/s per step and counts its binds as
  `driveImpulsesLimited` — about 10⁵ per 0.02 run, so **the fast step under-drives evolved
  muscle; anything about swimming or joints is read at 0.01 only.** A run whose manifest reads
  `status error` is censored. **A finite body can diverge too**: `r31-s3` ended at 11,533 s
  with a root thrown to a finite height of the order of 10³¹ m, which passed both non-finite
  tests and overflowed the light field's layer index (`ArgumentOutOfRangeException` from
  `LightField.Contribute`, logbook/0077). Since 2026-09-08 the harness's check and Core's
  `Observe` guard read the height against the world's box with room
  (`World.HeightIsInTheWorld`: the depth above the surface, twice the depth below the floor),
  and such a body dies as the same counted `Diverged` death. **A non-finite link is a diverged body too, and until 2026-09-10 only the root
  was read**: `CheckFinite` ran at the metabolic cadence on the root alone, the chemical sense
  read every part's transform on every physics step, and `r35old-s3` died with the whole
  process at 618 s when a link went non-finite between two checks and the grid refused the
  point (`status error`, no `diverged/` dump; round 33's three divergences were one-part
  bodies, so the root check was lucky). From that build the check reads every link and runs
  again before the sensors on any step where a birth or a resize moved a body, and the grid's
  refusal names a non-finite position rather than `FieldPoint.At`.
- **The shared world does not replay unless the physics step runs on one thread.** Same
  genome, seed, config and build on the same worker gave six realisations of one world, every
  pair identical for ~148,000 steps and then parting in one body's velocity by one or two ulp
  where touching bodies were solved in a different order; Unity's *Enhanced Determinism*,
  sleep, broadphase and scratch-buffer settings change nothing, and `-job-worker-count 0`
  restores identity over 300,000 steps (logbook/0069), then over 1,000,000 steps at a full
  round's population (`r28p-s1` ≡ `r28-s1`, logbook/0070). PhysX documents thread-count
  independence; this build with articulations in contact does not have it. The tiled world's
  replays (`r16dt-01c/d/e`, `fp-replay*`, `fl-replay`) never reached the fork because no two
  creatures ever shared a solver island, so **a replay-identity validation in the tiled world
  says nothing about the shared one** — validate shared-world changes with the state digest
  (`EVOSIM_DIGEST_EVERY`, `scripts/digest-diff.py`) on a zero-worker pair. Every shared-world
  run before the fix (0065–0068) is one realisation of its seed; the theatre's World mode
  re-simulating one of them watches a cousin from t≈1,500.
- **Every worker compiles `src/Evosim.Core` from the main tree.** `unity-wN/Packages/manifest.json`
  points at `file:../../src/Evosim.Core`, so `new-worker.ps1` copies only `Assets/`,
  `ProjectSettings/` and `Packages/`; a Core edit in the main tree reaches every worker launched
  after it, whatever its `Assets/` carry, and the manifest's `coreHash` records which. A round
  cannot be held on one build once Core has moved (round 24's fifth seed runs on the perception
  build for this reason, 0061); land Core changes between rounds or accept and record the split.
- **`scripts/simhash.py` is not the hash.** It agreed with `EvolutionRun.HashSourceTree` on every
  tree through 1ce2e71 and disagreed on e59f6af's (`93ef4e96…` against the C#'s and
  `run-arm.ps1`'s `30b96bf6…`), which cost one refused launch. Take the expected hash from a
  manifest the build has written (`runs/<arm>/<run>/run.json`, `source.simHash`) or from
  `run-arm.ps1`'s own printout; the python is a convenience until its divergence is found.
- **Adding a tunable makes every older `config.json` unreadable by the new build** — §9's
  refuse-rather-than-default rule on a missing group. `ledger.ps1 -Config` against a run written
  before the tunable throws; take the genome from the old run and the config from a new one.
  The same rule now bites genome files: the snapshot id took `GenomeJson.FormatVersion` to 4, so
  every stored `format":3` genome under `inocula/` is refused by this build
  and by `ledger.ps1 -Genome`. The genome fields did not change across that bump — only the
  optional id was added — so an inoculum can be brought forward by re-extracting it from a new
  snapshot, which is the one route that cannot quietly mislabel a creature. The growth build
  took it to 5 on 2026-09-09 (D087), so a format-4 genome is refused the same way; the growth
  gotcha below has the details.
- **`simHash` is a property of a checkout, not of a commit.** It hashes the bytes of every `.cs`
  under `Assets/Evosim` on disk, and this working tree is mixed: `EffectorDriver.cs`,
  `EmbodiedRun.cs` and `ThroughputSurvey.cs` are CRLF while everything beside them is LF, which
  `core.autocrlf=true` hides from `git diff` completely. So two checkouts of the same commit can
  carry different `simHash`es, and a hash cannot be reconstructed from history with
  `git archive` (it applies the checkout conversion; `git show` does not). Compare a recorded
  `simHash` against a tree on disk, never against a commit.
- **Anything under `Assets/Evosim` is simulation source, whatever it does.** The theatre spent one
  commit inside it and made every earlier recording unreplayable, because a HUD label is a `.cs`
  file under that root and `simHash` cannot tell a viewer from a solver. Presentation, tooling and
  anything else that does not decide a trajectory goes beside it (`Assets/Theatre/`), not in it.
- **Every evolution run through round 28 swam with added mass off, and the knob defaults to
  off.** `FluidConfig.AddedMassCoefficient` had no initialiser and `EvolutionRun` never set it,
  so every `config.json` through round 28 reads `addedMassCoefficient: 0` and those worlds swam
  on drag alone. From the movement build (D081, 2026-09-07) `EVOSIM_ADDED_MASS` sets it, the
  header carries `addedMass` on every run, and the default stays 0 so that every launcher in
  the record still describes the world it ran. It is a per-step term: any nonzero value is a
  new realisation of every seed, and a config at 0 replays the record.
- **A stored genome may carry a global brain that nothing steps.** `Genome.GlobalBrain` was
  retired by D081 (2026-09-07): a child is born without one, a rewire never draws the
  `GlobalBrain` input kind, and `Brain.For` builds no partless group. Snapshots recorded before
  that build still load and validate, and three genomes in five in them carry one or two
  global neurons; a count of "neurons" taken from such a genome includes neurons the new build
  never evaluates. `Brain.NeuronCount` is the body's count.
- **`stillb` and `mat orphan` read 0 in a healthy run, and the second is an invariant.** From
  the 2026-09-07 build the table carries the stillbirth total and the matter the ledger says
  is in bodies less what the living hold. A nonzero `mat orphan` means matter was charged to a
  body that does not exist, which is what happened for every stillbirth between D065 and
  2026-09-07 (none seen in the checks made on the scored worlds; DESIGN §0p).
- **A vertex world needs positions, and a Core-only one has almost none.** From D083
  (2026-09-07) `EVOSIM_FIELD vertices` replaces the two cell fields with `VertexField`, which
  feeds a body at its centre of mass and refuses a point that carries only a depth and a
  patch. The world therefore refuses the tiled mode (`SharedSpace` false), and in a Core-only
  test with no placer every body sits at its patch's centre, so every deposit merges into a
  handful of vertices (20 vertices holding 14,644 J in `VertexFieldTests`) and the field reads
  nothing like the farm's. Read a vertex field's shape from a run, never from a Core test. A
  deposit that lands within `FieldMergeMetres` of a vertex joins it and the vertex keeps its
  position; the report's `vtx` column prints `detritus/matter` counts against
  `FieldVertexCap`, and the per-depth columns (`det deep`, `mat here`, `refuge J`) are the
  same layer-and-patch bins as before, summed over vertices. Every config written before
  this build lacks the `field` group and is refused by it, per the tunable rule above. **The
  two fields read through different kernels**: detritus at `FieldKernelMetres` (1 m, 4.2 m³),
  matter at `FieldMatterKernelMetres` (1.8 m, 24.4 m³, the old cell's volume). The first
  seed-2 screen ran both at 1 m and bred once in 10,000 s, because a child costs 8 to 16
  units of matter and a 1 m kernel reaches 4 at the seeded density; a vertex world whose
  founders never breed should be read at `mat blk` against `mat here` before anything else.
  **`mat resid` is the matter identity and must read 0 like `audit`.** The energy audit does
  not see matter: the second seed-2 screen created 22,000 units of matter in 3,000 s with
  `audit` at 0.0000% on every row, because the vertex take delivered less than its gate
  promised and conception booked the price. From the build that fixed it (2026-09-07 late)
  the table prints the residual and `mat short`; on a run older than the column, read
  `matterStanding` in `stats.jsonl` against the seeded stock plus influx less burial.
- **A grid world refuses three things a vertex launcher would pass, and prints a dash where
  it printed a count.** From the grid build (fable-propose-grid.md, 2026-09-08, logbook/0078)
  `EVOSIM_FIELD grid` puts both fields on cells (`GridField`; `EVOSIM_FIELD_CELL` 1 m,
  `EVOSIM_FIELD_MATTER_CELL`). A cell size must divide the box on all three axes or the
  world is refused at construction: the campaign's 100 m² over four patches at 60 m is a box
  20 × 5 × 60 m, so the matter default of 3 m refuses it and 2.5 m or 5 m runs (5 m is ruled,
  D086; 2.5 m strands a fifth of the matter in cells too small to afford a child, 0078). `EVOSIM_H_MIXING` must equal `EVOSIM_MIXING` or the world is refused,
  because a grid stirs at one rate on all six faces and the header's `h-mix` has to read what
  the detritus does; the matter grid stirs at its own `MatterMixingDiffusivity` on every axis,
  2 m²/s in the campaign, where the vertex world walked it sideways at 0.02. Explicit
  diffusion is refused above `D·dt/cell² = 1/6`, so 2 m²/s at a half-second step needs a
  matter cell over 2.45 m. The `vtx` column prints a dash on a grid. The two cell tunables
  make every earlier `config.json` unreadable by the build, rounds 30 and 31 included; their
  arithmetic replays on the vertex field under their own builds. Read a grid field's shape
  from a run, as for vertices; the Core experiments (`GridFieldExperiments`) are the
  sitter/mover, column and corpse-patch measurements and nothing else. **A corpse is an
  object only when `EVOSIM_CORPSE_DECAY` is above 0** (`CorpseDecayPerSecond`, default 0,
  at which a death deposits at once as it always did): above 0 a dead body's joules and
  matter sit in `World.Corpses`, counted as standing by both identities, and reach the
  fields at that rate per second; read the `corpses` column and the `corpseJoules` /
  `corpseMatter` stats fields, and remember `detritus J` no longer holds what a corpse
  still does.
- **From the growth build (2026-09-09, logbook/0081) a body is not a unit of biomass, and every
  stored genome is refused.** `BirthInvestment` replaced the endowment and `AdultScale` joined
  the genome, so `GenomeJson.FormatVersion` is 5 and every `format":4` inoculum and snapshot on
  disk is refused, as the format-4 bump did to format 3; re-extract from a new snapshot. A child
  is born at a fraction of its adult body (median 0.31 in the smoke) and grows, so
  `MaximumPopulation` still ends a runaway but counts bodies of any size, so D087 adds
  `MaximumTissueJoules` (`EVOSIM_MAX_TISSUE`, 0 = off; the header's `maxTissue=`), which ends a
  run as a runaway on the living bodies' standing tissue, and `run.json` names which ceiling
  fired. In a closed-matter world neither can: round 33's 6,000 units cannot build 8,000
  bodies (24,000 units for the per-creature term alone) or 30,000 J of tissue (15,000 units),
  so a runaway there is impossible by construction and both ceilings are idle instruments
  kept for a world with influx. Read `body frac`,
  `adult scale`, `invest` and `brood` in the table, `bf` and `as` on lineage birth rows, and
  `growthShortOfMatter` / `conceptionsUnderMassFloor` in `stats.jsonl`. At investment 0.5 a
  world breeds about seven times faster through founding than one whose children are born
  whole, and strips the matter at its layer within 300 s (0081's smoke against `r32-s1`).
  The harness resizes a living articulation in place every `GrowthStepSeconds`;
  `resizeJumpMetres` is exact and read 0, but `resizeStepMetres` reads the root only and
  cannot see a snap smaller than the current's drift (15 cm per step at 0.3 m/s), so a
  resize suspected of throwing a body is read from a per-link instrument that does not
  exist yet, and from `diverged`. **`conceptionsUnderMassFloor` counts attempts, not parents**:
  a refused parent keeps its reserve and draws again next step with a fresh mutant, so round
  33's two million per arm is thirty to forty parents standing at their threshold at any
  moment, and the floor is the sieve that shapes the litter (logbook/0082). **The placer
  reserves a newborn's spot at its birth radius** and the body grows up to twentyfold in
  place; `Body.Radius` is refreshed on resize for crowding, the reservation is not, and a
  contact divergence of three leaves in one step (`r33-s2`, 3,066.5 s) is read against it. `mat orphan` has read of the order of 1e-4 units on 6,000
  since before growth; the invariant is broken by a value the table does not round away.
- **Watch a round in the theatre before writing it up, and say what was seen.** On 2026-09-10 the
  owner opened round 33 seed 3 in the theatre and saw the whole world as two vertical ribbons a
  metre wide in a box twenty metres long (logbook/0083). A newborn was placed touching its
  parent (D077), the current returned every body to where it found it (D037, written when
  "nothing reads horizontal position" was true), and no body ever swam, so every clade was a
  column packed around its founder's spot for the whole run, draining its own cells since the
  grid. Thirty-three rounds were read on a report that carries a mean depth and per-patch bins
  and nothing about x or z, and the theatre had existed for three days without being pointed
  at a scored run. The rule from it: no round's entry is written until the world has been
  watched in the theatre, and the entry says what it looked like; and the table's `cols`,
  `cols abs` and `x sd` (the occupied 1 m columns of the footprint and the spread of x) are
  read with `alive`. The rounds' books, prices and verdicts stand as measured; every claim
  about where food is relative to bodies is confounded and logbook/0084 lists the retries.
- **From D088 (2026-09-10) the water carries, and three readings change with it.** `EVOSIM_CURRENT_MODE
  Transport` is a three-dimensional divergence-free field at RMS `EVOSIM_CURRENT`, equal on every
  axis, that moves bodies, corpses and the grid's cells; `Rolls` (the default, and every recorded
  config) is the returning current. Read the mode from the header (`current 0.3 m/s transport`),
  and read **`mean m/s` as the water**: a sitter in the transport field moves at about two thirds
  of the knob because the water does, so the column that was the locomotion readout is not one,
  and there is no relative-speed column yet. The grid substeps its advection when the fastest
  water (2.45 times the RMS) would cross more than half a cell in a metabolic step and refuses
  above eight substeps; 0.3 m/s on 1 m cells is two, and about a quarter of the throughput.
  Dispersal is `EVOSIM_OFFSPRING_DISPERSAL` (`OffspringDispersalMetres`, 0 = D077's touching
  rule), **not `EVOSIM_DISPERSAL`**, which is D061's retired patch lottery and still bound; the
  header prints `dispersal=`. The placer reserves a child's adult radius from this build, so
  `crowded` and `stillb` mean something different from round 33's; founders still reserve
  their birth radius. Both tunables refuse every earlier `config.json`, rounds 32 and 33
  included.
- **`mat blk` and `crowded` are per-window counts that scale with the population.** Read them
  against `births` in the same window (logbook/0068: refusals at two to three times the births),
  never as an absolute threshold; a raw blocked-conception count says nothing on its own.
- **`windows-il2cpp` is not installed** — only Mono. Fine for now; add it before the island
  model (Milestone 4), since per-creature brain evaluation is managed C# in the hot loop.

## Working with the owner

- **Every piece of prose follows [`STYLE.md`](STYLE.md)** — the logbook and primer serve
  agents *and* humans who want to read the research, and the record is meant to become a
  book. Run `python scripts/style-check.py <file>` before committing prose; keep the
  reader's key current (`logbook/README.md`).
- **First-person voice is welcome in the record** — the owner invited genuine agent
  reactions, as the agent's own: honest and brief, never performative (logbook/0042's
  personal note is the precedent).
- **A subagent cannot wait, so never hand it a task that ends in one.** It cannot sleep and
  cannot return before its report, so a suite or a smoke as its last step turns into a no-op
  shell command every few seconds until someone stops it; on 2026-09-09 that ran for an hour
  and the owner had to stop the session (logbook/0081). An agent's task ends when its edits
  and the fast filtered tests are in; the caller launches anything long in the background,
  where a completion notice costs nothing, and reads the result.
- **Owner-reserved decisions:** world rules (what the ecology *is*), the goal rule and its
  amendments, scope and round design forks, pushes of anything that is not code/prose, and
  anything irreversible or outward-facing. Instruments, diagnostics, replays of scored
  conditions, analyses and doc upkeep are agent work. A proposal file
  (`fable-propose-*.md`) is the vehicle for putting a design in front of the owner:
  absorbed into DECISIONS.md on ruling, then deleted.

## Conventions

- **Nothing of the project's is written outside the repository — TEMP included.** Transient
  files go in `scratch/` (gitignored): Unity run logs (`scratch/logs/`, where
  `run-arm.ps1` puts them), monitor watch lists, theatre pictures (`scratch/snaps/`,
  `scratch/positions/`), extracted genomes, compile logs. An agent's own session scratchpad
  and the Windows temp directory are both outside the project and both off limits (owner's
  rule, 2026-09-03; the per-arm logs lived in TEMP until then). **Whatever prose points to,
  and whatever tool will be used again, does not stay in `scratch/`** (owner's rule,
  2026-09-10): a file a citation can reach has to survive a cleanout, and what only bought
  one validation or one afternoon's understanding does not. Round launchers live in
  `rounds/`, the per-round reads and analysis scripts in `scripts/reads/`, and the build
  specs, build reports, surveys and prereg drafts the record cites in `logbook/specs/`. The
  working test is whether the record depends on the file, and the limit is derived rather
  than raw: an entry's pictures, plots, configs and genomes are committed beside it
  (`logbook/images/`, `inocula/`) as part of writing the entry, under the hook's 5 MB
  ceiling, and a run directory itself never is; its manifest and hashes are the pointer.
- Simulation output (`runs/`) and spike CSVs are gitignored; `FINDINGS.md` is tracked
  because DESIGN.md links to it.
- Genomes serialize to **JSON**, not binary — readable, diffable, and hand-written rather than
  via a library, because `Evosim.Core` has no dependencies and that is what keeps its tests at
  one second. `Json`, `GenomeJson`, `RunConfigJson`, `CellTypeJson`, `RunDirectory`.
- **A run is a directory, and its two high-volume files are append-only JSONL.** `config.json`
  (indented, hand-editable, carries its own hash), `lineage.jsonl` (one row per creature ever
  born), `stats.jsonl` (one row per sample), `snapshots/`, and, in a shared world from
  2026-09-10, `positions.jsonl` (one row per sample carrying every living body's id, x, y, z and
  its absorptive/jointed/photosynthetic flags; read it with `python scripts/positions-read.py
  <arm> --summary`, or `--at 1000,5000` for the pictures). A killed run leaves every completed
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
- Every evaluation must be reproducible from `(genome, seed, configHash)` plus the manifest's
  `simHash`, `coreHash` and `physicsJobWorkers` (D078: the shared world replays at 0 worker
  threads only, and a build change is a different realisation). PhysX is not bitwise
  deterministic across machines or Unity versions, so the hashes exist to *detect* mismatches
  rather than to promise portability.
