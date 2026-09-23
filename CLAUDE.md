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
and bodies are born small and grow (D087, logbook/0081). The goal is a ladder of milestones since D094 (2026-09-16): rung 1, D063's
self-sustaining food chain, is met and its scorer prints a state and not a verdict; rung 2,
adaptation by degree, is a reading until its threshold is measured; every round is read on
its own pre-registered predictions. Before that, the goal rule (D063 as amended) was
last met five seeds of five in round 30 (logbook/0075); the grid's base round 32 read two of
five with every mechanism prediction holding (0079); round 33, the growth base, met it five of five
and was the first world in which a trait moved by degree rather than by switch (0082). Then
the theatre showed every clade as a metre-wide column (0083): a newborn beside its parent
and a current that returned bodies. D088 (2026-09-10) replaced both, a newborn dispersed
over a disc and a current that carries in three dimensions, and the agent's own pictures
(0086, `scripts/theatre-snap.ps1`) show the fixed world filling the box. Round 35 is the
base round in that world and read five of five (0087), the dials no longer walking one way;
which of the earlier readings survive it is 0084's list, and round 34, the free joint, runs
on it.
The open frontier: why the eaters' lines stop recruiting on the grid, whether a free joint
survives founding (round 34, logbook/0080), and movement, which has never paid its energy
cost (the cost side is closed, the prize side is open; `mean m/s` reads the water now).
Throughput still binds: dt 0.02
screens, 0.01 confirms (logbook/0052). Experiments are *arms*, launched with
`scripts/run-arm.ps1` against worker copies `unity-w2`..`unity-w7` — never two processes on
one worker, **three concurrent arms** (owner's ruling, 2026-09-17: five arms on this
machine deliver about 1.7x real time in total and three about 1.5x, so the fourth and
fifth buy little and put every seed a day later and past its wall; `scripts/pace-survey.py`
is the reading), five Unity processes at most with the owner's open Editor on `unity/`
counted, and verify every arm's settings from the header its run report writes, not from
the launch command. Renders go between rounds or on a slot the arms are not using, never
on top of a full set. A **sixth** Unity process is allowed for a short visual check that
comes and goes (a compile, a render of a few frames; owner's ruling, 2026-09-16), never for
an arm, and never beside a Core test suite: pictures or tests on top of the cap, not both.

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

**Score a run by connected clade** (D063 as amended, logbook/0054's addendum; since D094
the clauses are a state per seed and not the campaign's bar, and the verdict line is being
rewritten to say *holds*, *cycles* or *no chain*, HANDOFF's queue) with
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
From 2026-09-12 the verdict line ends with a qualifier segment that changes no token:
`duration <simulated> of <requested> s`, with `SHORT` below 30,000 s (a 10,000 s budget
passed unqualified until the Astra review's fixture showed it), `floor open` or `floor closes
at <n> s` from the run's `config.json`, and `reading: absorptive clade only; producer clause
not decided`. Fixture tests live in `scripts/tests/clade-score/` (`run-tests.ps1`, 52
assertions); run them after touching the scorer. `scripts/compare-det.py` exits 1 on a
difference, 2 on a missing arm, field or shared sample or an ambiguous run directory
(`--run` names one), 3 on unequal coverage (`--allow-partial` waives it); its fixtures are
`scripts/tests/compare-det/run-tests.ps1`.

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
**`-From snapshot` draws the frame instead of replaying it** (2026-09-18,
`logbook/specs/snapshot-render-spec.md`). The genomes come from `snapshots/NNNNNNNNN.jsonl` and the
places from `positions.jsonl`, joined on the organism id. A picture of any second of any run then
costs seconds instead of a re-simulation from the first. Two things it cannot recover, and every
frame says so on a line reading `RECONSTRUCTED FROM SNAPSHOT`. No file holds a body's orientation,
so every body is drawn upright in the developer's own frame. None holds how far it had grown, so
every body is drawn at its adult size. Only a second the run wrote a snapshot at can be drawn. The
`close` view and `-Chrome` are refused in this mode, and the pictures carry `-recon-` in the name
before the view. **A picture may read a run recorded before a tunable** (§11 of the spec, the
owner's ruling of 2026-09-18). Picture-only readers take the water's shape from a config the
strict reader refuses, and the bodies' plans from a genome of format 4 or 5. Both are tried after
the strict readers, and the label's first line then ends `· OLD-RUN READ`. The rule that loading
refuses rather than defaults is untouched for everything that simulates.

Keys: `Space` pause, `[` `]` pace, `L` pace lock (at or under real time, for filming), `K` seek,
`C` colour, `X` raw shapes (the colliders as the physics has them, no rounding, carve, taper or
bend), `F` follow, `R` reload, `H` hide, `P` provenance, click to select; fly with `WASD`+`QE`, right-drag to look, wheel for speed.
`EVOSIM_THEATRE_RUN`, `EVOSIM_THEATRE_GENOME`, `EVOSIM_THEATRE_SEEK` and
`EVOSIM_THEATRE_OVERRIDE` set the same fields from a script.

**The interface is UI Toolkit under `Assets/Theatre/UI/`** (logbook/0096, built 2026-09-13 from
the owner's `design/SPEC.md` and `logbook/specs/theatre-ui-spec.md`): the strip with the
provenance word and the identity's coverage, the transport bar and timeline, the census with
the audit and the matter residual, the popover, the inspector, the solo census. Four things
about it bite. **A cousin's ids are unverifiable**: the runner's id map names the body on
screen, but the recording's `lineage.jsonl` belongs to another realisation, so under
`EVOSIM_THEATRE_OVERRIDE` the ancestry and the dead panel are withheld and only what the live
body carries is shown. **`ScreenCapture` writes nothing under `-batchmode`**: every picture of
the interface goes through `TheatreUiCapture` (the world camera and the panel into one
`RenderTexture`, read back), which is what `TheatreUiCheck` and `theatre-snap.ps1 -Chrome`
use; the default snapshot stays chromeless so `logbook/images/` stays comparable. **The type
and the rhythm take a 1.5 step at 3400 px** (the agent's ruling for the owner's 3840 monitor;
the design's rule is against uniform panel scaling, not against a density step), carried by
literal mirrors under `.is-wider` because a custom property declared there reached nothing,
cause unsettled: the stylesheet states the six sizes twice and both move together. **The
font assets are dynamic**: a few KB each, rasterised at runtime from the three Plex TTFs
beside them, so a player build would have to carry the TTFs. The end-to-end is
`Evosim.Theatre.EditorTools.TheatreUiCheck.Run` (`-batchmode`, no `-quit`, no `-nographics`;
`EVOSIM_THEATRE_RUN` for a world, `EVOSIM_THEATRE_GENOME` for solo, `EVOSIM_THEATRE_OVERRIDE=1`
against `runs/r42smoke` for the cousin states, which is the one recorded run this build
opens under another `simHash`; `runs/r37bsmoke3` served until 2026-09-22 and is now refused
on a missing `bed` field, and `runs/r37-s1` cannot serve, its config predates two tunables),
asserting every field against the replay and writing every state at 1920 and 3840 into
`scratch/snaps/ui/`; a fixture is a 300 s smoke recorded on the worker that runs it. The
live-mode end-to-end is `Evosim.Theatre.EditorTools.LiveUiCheck.Run` (`91aea20`, 2026-09-22;
same launch shape, `EVOSIM_THEATRE_RUN` a farm run and `EVOSIM_THEATRE_CHECKPOINT` its
checkpoint), 129 assertions over the same panels answered from the live world, and it
finishes with the refusal when the live world does not open rather than falling through to
a solo verdict, which the replay path once did too (2026-09-13's note in `Drive()`).

Both modes also run headless, which is how they are tested:

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
  quiet. After killing a wedged worker, refresh it — the Library dies with the process — and
  delete its `Temp/UnityLockfile`, which the kill leaves behind and which reads as a live
  Editor to anything that checks it (`launch-queue.ps1` sat for an hour behind three
  killed renders on 2026-09-13; it now removes a lock no process holds).
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
  check is not optional. **The same bite from bash**: `pwsh -File scripts/analyse-arm.ps1 r34-s3
  -Timeline -Columns 'alive','cols'` hands `-Columns` one string, `alive,cols`, which names no
  column, and the timeline prints `?` in every cell rather than refusing (2026-09-11). Call
  the scripts that take arrays from PowerShell, or split the list inside the script as
  `theatre-snap.ps1` does.
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
- **`coreHash` is the main tree's Core, whatever the worker's manifest points at.**
  `EvolutionRun` hashes `<repo>/src/Evosim.Core` by path, not the package the manifest
  resolves, so a worker whose `Packages/manifest.json` is pointed at a worktree's Core for a
  validation (the round 38 build's chain, 2026-09-15) compiles the branch and records main's
  `coreHash`. On a normal worker the two are one tree. Read a validation run's `coreHash` as
  the main tree's, and take the branch's from a manifest written after the merge.
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
  gotcha below has the details. **Three test fixtures are recordings and every bump orphans
  them** (found on round 44's build, 2026-09-22): `src/Evosim.Core.Tests/fixtures/r42-config.json`
  (the thread-identity word, `Slow`), `src/Evosim.Dynamics.Tests/RunFixture.cs`'s run directory
  (a snapshot crowd under `runs/`, ten contact, crowd and digest tests) and the joint-damper
  reproduction, which named two body ids of that crowd. Re-record each on the build that reads
  it: a config from a run of the build, a fresh `runs/<arm>` of round 43's world with the new
  tunables at zero (`r44fix-s4`), and a pair found in the scatter rather than named by id;
  the fixture's `Why` says which of "not on this machine" and "this build cannot read it"
  applies, and they need opposite responses. And `scripts/ledger.ps1` prompts for
  `-Clearance`, `-Depth` and `-Density` when they are missing: under a non-interactive shell
  the prompt never returns and the process sits with no child and no output (one lost
  quarter-hour that evening); pass all three.
- **`simHash` is a property of a checkout, not of a commit.** It hashes the bytes of every `.cs`
  under `Assets/Evosim` on disk, and this working tree is mixed: `EffectorDriver.cs`,
  `EmbodiedRun.cs` and `ThroughputSurvey.cs` are CRLF while everything beside them is LF, which
  `core.autocrlf=true` hides from `git diff` completely. So two checkouts of the same commit can
  carry different `simHash`es, and a hash cannot be reconstructed from history with
  `git archive` (it applies the checkout conversion; `git show` does not). Compare a recorded
  `simHash` against a tree on disk, never against a commit. **A worktree is another checkout**:
  round 41c's smoke on a worker mirrored from `scratch/wt-contacts` recorded `73902d7b…`, main's
  checkout of the merged commit hashes `99e0bdcc…`, and the two trees differ by carriage returns
  alone (2026-09-19, one refused launch). Take a round's expected hash from a smoke or a
  `run-arm.ps1` printout on a worker refreshed from the tree the queue will refresh from.
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
  `matterStanding` in `stats.jsonl` against the seeded stock plus influx less burial. From
  D098 (2026-09-18) `mat resid` is `World.MatterResidual`, the spent units plus every
  standing joule over ρ against the seeded stock plus influx less burial, and `mat short`
  is gone; the two books are one equation in two units, and either can open alone when a
  leg does half of a transfer, which is what keeping both is for.
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
  matter cell over 2.45 m. The `vtx` column prints a dash on a grid. From round 37b's successor
  build the table's `det cv` and `mat cv` read how far each field is from well mixed, the
  standard deviation of the live cells' densities over their mean, and they print a dash on a
  vertex field the way `vtx` prints one on a grid. The two cell tunables
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
  above eight substeps; 0.3 m/s on 1 m cells was two, and about a quarter of the throughput, until the conservative
  transporter (2026-09-12), whose substeps come from the largest per-cell outflow and read one.
  Dispersal is `EVOSIM_OFFSPRING_DISPERSAL` (`OffspringDispersalMetres`, 0 = D077's touching
  rule), **not `EVOSIM_DISPERSAL`**, which is D061's retired patch lottery and still bound; the
  header prints `dispersal=`. The placer reserves a child's adult radius from this build, so
  `crowded` and `stillb` mean something different from round 33's; founders still reserve
  their birth radius. Both tunables refuse every earlier `config.json`, rounds 32 and 33
  included.
- **A rule chosen for being cheap gets re-asked when a later change makes it bite, and the
  report probably already says so.** D077's periodic wrap cost nothing while no body ever
  reached a seam. D088's carrying current sent every body across one about once per hundred
  seconds, the table's `wraps` column counted it in every window of rounds 35 and 36, and
  nobody read the column as a problem until the owner watched a minute of the theatre and
  saw bodies teleport (2026-09-11; `fable-propose-aquarium.md`). Read `wraps` with `alive`,
  and when a world rule changes, list the rules that were chosen for cheapness under the old
  one and ask each whether it still holds.
- **From D089 (2026-09-12) the world can be a tank, and five things read differently.**
  `EVOSIM_SHAPE Tank` (`RunConfig.WorldShape`, default `Box`) makes the footprint a disc of
  `sqrt(area / π)` with a glass wall of 48 collider slabs, ring patches of equal area (`p0`
  the centre, `p3` the rim), a masked grid (6,000 live cells at 1 m and 100 m²) and a gyre
  in place of the periodic transport field; the header reads
  `space tank r=5.64 m (100 m2), depth 60, wall, bed` and a box's token is unchanged
  (`shared 4x5x5 m`; the box branch had changed it to `4x1x5 m` and the tank spec put it
  back). In a tank `wraps` reads 0 by construction and a nonzero is a bug; `x sd` is a plain
  deviation, not a circular one; `cols` is counted against the columns inside the circle;
  and a root farther than R + 1 m from the axis dies as a counted `Diverged` death with a
  dump whose reason names the radius guard. **The 5 m matter cell's mask overshoots the
  disc**: its live volume at 400 m² is 25,500 m³ against the nominal 24,000, so a held
  `MatterBudgetUnits` (`EVOSIM_MATTER_BUDGET`, 0 = the density rule) seeds 0.235 units/m³
  where the arithmetic says 0.25; the total is exact and the density is not, and the 1 m
  detritus mask is within a cell ring of the disc. The three tunables of D089 refuse every
  `config.json` written before them, rounds 35 and 36 included. **The drive limiter at
  every step exists and is off** (`EVOSIM_DRIVE_LIMIT_ALWAYS`, header `driveLimit >0.01`
  or `always`): its check, round 34 seed 5 rerun with it on, bound 2.58 million drives and
  read 27 divergences against 17 with the same signature (jointed adults, median age
  1,171 s, the root's height going non-finite), so the throw of a jointed adult is not a
  single step's over-drive (`r34lim-s5`, D089's check 4). **Round 37, the tank, threw
  nothing** across 9.17 million jointed body-seconds (logbook/0094) and 0094 read the wrap
  as the mechanism; **round 37b threw 43 across 11.1 million** (logbook/0097), so the wrap
  was not the whole of it. Every one of the 43 was a two-part jointed body dumped within
  seconds of its birth, with a joint mass ratio under 2.4, away from the wall and from any
  resize, and clustered by parent; the mass-ratio cap is excluded by the traces and not
  proposed. **The throw trace as built misses the onset**: its three-frame ring is written
  after the check that fires it, so in 42 of 43 dumps every frame is already non-finite;
  read a dump's trace for the masses and the ratio, not for the first bad step, until the
  ring keeps the last finite frames (it does from round 40's build: seed 2's one dump held
  three finite frames, logbook/0106's read). The one-part control (seed 5 on 37b's build with
  `fluidAccel 0`, `r37bc-s5`) is the test of the force.
- **A run writes at most 50 diverged dumps** (`Ecosystem.MaxDumps`), so `diverged/` equals the
  `diverged` column only below 50: `r36-s2` counted 55 and dumped 50, and its last five
  throws have no post-mortem (logbook/0092). Read the column for the count and the dumps
  for the anatomy, and say when the second is censored.
- **A drag-only fluid is a centrifuge, and a walled world shows it.** A body pulled toward the
  water's velocity by drag alone drifts outward on every curved streamline by about
  `τ·u_θ²/r` per second (`τ` its response time, 0.4 to 1.3 s at the campaign's sizes), so any
  current with any turning in it piles bodies against a wall: round 37's tank put 58 to 96% of
  every seed in the rim quarter and the whole world in a crust at the glass at the peaks
  (D090, 2026-09-12; the owner called it a centrifuge before the agent did). The box never
  showed it because the seams wrapped. From D090 `EVOSIM_FLUID_ACCEL` (`FluidConfig.
  FluidAccelerationCoefficient`, default 0 so every recorded world replays; 1 from round 37b)
  adds the water's acceleration force, and with it a lagging body keeps an even spread (rim
  quarter 0.25 to 0.27 in `StreamsTests`) where it read 0.83 to 0.98 without. Read `fluidAccel`
  in the header, and read `p3` over `alive` in any walled world before anything else. In a
  tank the term takes the streams' closed-form derivative (`StreamsAccelerationAt`, 2.3
  velocity samples per call, `logbook/specs/streams-analytic-spec.md`); the nine-sample
  stencil (`MaterialDerivative`, about ten velocity samples) serves only the box's transport
  field, where no round has run the force. In a tank the current is D090's streams, selected by the
  shape; the header's `current` token still names the mode (`transport`), and the shape token
  is what says the streams are running.
- **A theatre render's wall is set from the replay's pace, and its wait can outlive the
  Editor.** Round 39 seed 1's full-length render (5,000, 15,000 and 30,000 s at 2,000 bodies
  beside three arms) reached 15,000 s in five hours and its 600-minute wall about 40 minutes
  short of 30,000 s (2026-09-18): the theatre quits itself at `EVOSIM_THEATRE_WALL_MINUTES`
  with no `timed out` line in the log, and the frame is simply missing. A full-length replay
  of a 30,000 s seed runs no faster than the farm did, so give it half again the seed's own
  wall (900 minutes at this crowd). And `theatre-snap.ps1`'s `Start-Process -Wait` waits for
  the Editor's process tree: an orphaned `Unity.Licensing.Client` whose parent had exited
  held the first render chain for an hour after the Editor was gone. A chain that calls the
  script in sequence should read the Editor's exit from the log and the pictures, and
  stop a helper whose parent is dead before the next render.
- **A theatre snapshot of a live run can time out before its later frames.** The early pictures
  of `r37-s1` at 5,000 and 15,000 s on a machine running five arms reached the first in about
  forty minutes and timed out at ninety before the second (2026-09-12); a live run replays no
  faster than the farm did. Take early frames one at a time, or wait for the queue. And
  the script's default wall of 30 minutes is short for even a first frame at 3,000 s
  beside three arms and a render: round 41's early look timed out on it with nothing
  written (2026-09-18). Pass `-WallMinutes` from the farm's own pace, about a minute per
  20 simulated seconds at 500 bodies on a loaded machine.
- **A pass that touches `Assets/Evosim` orphans the smoke recorded before it.** The tank's
  second pass moved `simHash` after `r37tank` was recorded, and the theatre refused its
  pictures on the mismatch (2026-09-12). Record the smoke you will photograph on the tree
  you will merge, after the last pass, or photograph before the next one.
- **Uniform water made its own patches until 2026-09-12.** A dissolved field carried by
  incompressible water stays uniform, and the grid's transporter did not keep it so: face
  velocities sampled at cell centres and three axis passes applied in sequence turned
  1 unit/m³ into 30% patchiness on 1 m cells within 600 s at the campaign's mixing (34% on
  the streams, 102% with mixing off; 5 to 6% on the 5 m matter cells) while the total held to
  1e-15, which is all the transport tests asked. Conservation, positivity and dry cells can
  all hold on a field that is wrong; a constant-field check belongs beside every conservation
  check. From round 37b's build the grid takes its face fluxes from the current's vector
  potential (`CurrentField.PotentialAt`; `logbook/specs/transport-conserves-spec.md`), so
  every cell's net flux is zero by telescoping; the rolls' scheme is untouched and every
  recorded world replays. A new current mode needs a potential, not only a velocity, before
  the grid will carry it faithfully, and the rolls have none: their scheme, rounds 32 and
  33's, fails the same test worse (113% on 1 m cells) and is left as recorded. Two readings
  come with the repair: the substep count now comes from the fluxes themselves (one substep
  in both campaign cases where the a-priori Courant check asked two, so the transport is
  cheaper per step), and a 5 m grid carries only 0.3 to 0.4 of the water's RMS because it
  samples the eddies about once per wavelength (the 1 m grid carries 0.96 to 1.07). The
  Astra review's probe that found it is `scratch/astra-check/Program.cs`.
- **A pre-registration is committed before the queue starts.** Round 37's predictions were
  committed at 08:31:49 and its first manifest written at 08:28:48 (the Astra review of
  2026-09-12): the thresholds were in the working tree and not in history when the world
  started. `launch-queue.ps1 -Prereg logbook/NNNN-….md` refuses to launch unless the entry
  is tracked and clean, and writes `runs/<arm>/prereg.json` with the commit beside each run.
- **`mat blk` and `crowded` are per-window counts that scale with the population.** Read them
  against `births` in the same window (logbook/0068: refusals at two to three times the births),
  never as an absolute threshold; a raw blocked-conception count says nothing on its own.
  `contacts` is the same rule applied to `contactPairs`, which is cumulative in `stats.jsonl`
  and in `run.json`: a window is two rows differenced, never the running total read alone. The
  contact instrument (2026-09-19, `logbook/specs/contact-instrument-spec.md`) puts three more
  cumulative fields beside it, `contactPairsJointed`, `contactPairsPersistent` and
  `contactBodies`, and three columns after `contacts`: `pairs/body`, `pairs jnt %` and
  `stuck %`. They are the window's pairs per living body per physics step, then two shares of
  those pairs. One is the share with a jointed body on at least one side. The other is the
  share the engine reported as a stay rather than as a fresh touch. All three are
  creature-creature only, as `contacts` is, and the bed and the glass stay in `floor con`. In
  the one-substance world the pairs tracked the jointed count across every run on file. Round
  41b was stopped on that explosion (HANDOFF).
- **The project is in linear colour space from 2026-09-16 (logbook/0104), and a global shader
  colour is not converted.** A material colour, a render setting and a camera's background are
  converted from sRGB by the engine; `Shader.SetGlobalColor` hands the numbers over raw. The
  first lit-water pictures were five times too bright for exactly that. Every global colour
  takes `.linear` by hand, and the first picture after a new one is the check. The owner's
  open Editor on `unity/` reimports on the flip.
- **`FindObjectsByType` does not see the skin's furniture.** Every piece of it is created with
  `HideFlags.DontSave`, and the engine's finders leave such objects out, so the snapshot's
  inside-only hiding found nothing for six days and the shafts stood in every side view
  (logbook/0104). A marker on such an object keeps its own list (`TheatreInsideOnly.All`);
  never look for the theatre's own objects with a finder.
- **A theatre render is a sixth Editor, and its start-up is the load, not the replay.** The
  asset scan and the script and shader compiles run on every core; three renders in forty
  minutes had the owner hearing the fans. `theatre-snap.ps1` runs the Editor at four job
  workers; batch the changes and render once, and never beside a test suite.
- **Set an arm's wall limit from the measured pace, not from the hoped-for one.** Round 39's
  launcher gave 1,200 min for 30,000 s on an estimate of fourteen hours a seed; at five arms
  and 2,000 bodies the seeds ran at 0.3x real time, twenty-eight hours, and seed 3 was
  censored at 25,175 s with "ended wall" (2026-09-16). A running arm's wall cannot be
  extended. Read `x real time` off a seed's footer or the early rows before launching the
  rest, and give the wall half again what that says.
- **The run footer says where the wall clock went, and the world's step is priced per cell,
  not per body.** From the timing-split build (2026-09-17) every `stats.jsonl` row carries
  cumulative `wallPhysicsMs`, `wallWorldMs`, `wallHarnessMs`, `wallWritersMs` and
  `wallTotalMs`, `run.json`'s ending block carries the totals, and the footer prints
  `wall split: physics 4%, world 85%, harness 11%, writers 0%, other 0%`. That line is the
  300 s smoke on round 39's world at founding, fifty bodies: the Core step (the 1 m grid over
  2,200 m² × 45 m, stirred and carried every half second) cost 83 ms per metabolic step
  whatever the crowd, which is about a tenth of a full seed's wall and most of an empty one's.
  At a full crowd the split turns over: round 40's three seeds at 2,500 bodies read physics
  22 to 25%, world 11 to 14%, harness 62 to 65% (`logbook/specs/r40-read/pace.tsv`), so a
  seed's wall at the crowd is the harness's per-body work, not the grid.
  Read the split from two rows' differences for a window, and from the footer for the run;
  every row and manifest before this build reads 0. The instrument is two timestamps
  around each call and changes no trajectory, but it lives under `Assets/Evosim`, so it
  moved `simHash` (`6d38c45e…`) and every worker needs a refresh before the next round.
- **A body's reserve is unbounded, and none of it ever feeds anyone.** Nothing caps
  `Organism.Energy`. Senescence (D038) is a multiplier on upkeep, `1 + age / 3,000 s`, so an
  old body burns its whole reserve as upkeep before it starves, and `World.Bury` hands the
  corpse the tissue alone (`Corpse(..., TissueJoules, LockedMatter)`) and books whatever
  reserve a diverged body still held as `EnergyOut`. Either way everything a body saved
  leaves the world as heat. In round 40's seed 1
  at 16,000 s the living had captured 764 kJ of light against 287 kJ spent, about 190 J of
  reserve a body against 0.37 J of tissue, and every one of those bodies dies at 3,000 s
  with that reserve. So the eaters' larder in rounds 38 to 40 was the tissue only, a
  fraction of a percent of what the leaves captured, which is part of why the eaters boom
  and starve (0101). The audit closes because the discard is booked as an outflow; the
  books say nothing about whether a rule is sensible. Found 2026-09-18 while sizing D098's
  loop. D098's build burns it as a charged unit returned spent to the water, so at least
  the plants get it back, sends a diverged body's reserve to the corpse, and adds
  `ReserveCapSeconds` (off in the base round) as the lever that moves a hoard into the
  larder while the body lives; a starved body still dies with nothing, because senescence
  burnt it first.
- **From D098 (2026-09-18) there is one matter in two states, and six things read
  differently.** A unit is charged (ρ = `JoulesPerUnit` joules, in bodies, corpses and the
  marine snow the code still calls `Nutrients`) or spent (none, dissolved, the field the
  code still calls `Matter`); light charges spent units at the leaf, burning returns them
  spent, eating moves charged ones, and nothing draws a field at conception or growth
  (DESIGN §5A.2d, `logbook/specs/economy-spec.md`). First: every `config.json` and every
  genome (format 6, `ReserveMargin`) written before the build is refused, so the ledger, the
  theatre's Mode B and the three configs under `inocula/` all need a new-build run to read
  from. Second: the table's `mat blk`, `mat short`, `mat orphan` and `excreted` are gone
  and `upt lim` (the share of leaf-steps bound by uptake rather than light), `burnt`,
  `remin` and `margin s` are there; `analyse-arm.ps1` reads the new names and prints `?`
  on an old report's, as it always did. Third: `EVOSIM_REMIN` sets
  `RemineralisationPerSecond`, every cell's marine snow returning spent to the water, not
  D051's floor leak; `EVOSIM_EXCRETION` and `EVOSIM_MATTER_PER_*` are unbound. Fourth:
  `mat locked` is the living bodies' charged matter, and it is almost all reserve (round
  40 held about 190 J of reserve a body against 0.37 J of tissue), so it moves with the
  hoard and not with the crowd. Fifth: the ledger takes `--spent` (the spent density at the
  body) and its last column is `units/child`; a leaf in stripped water is uptake-limited
  in the ledger as in the world. Sixth: the fields' `TotalJoules` is units on the spent
  field and joules on the charged one, by convention only, as before the build; read the
  unit off the field, never off the method's name. Seventh, and the one that cost the
  night: **a closed one-substance world's count is the capacity over the holding.** D065's
  fixed charge was the head-count cap and it is gone by design, so the first smoke at
  11,000 units built 6,300 bodies in 1,100 s. A per-body standing cost does not bound the
  count (it sets where the water settles), a tissue value dear enough to bound it freezes
  the founding (100,000 J/m³ and above: no births by 2,500 s), and what does bound it is
  the budget over what a breeder must hold, whose floor is `PerOffspringOverheadJoules`
  (`EVOSIM_OVERHEAD`, 100 J from round 41). Screen a budget or an overhead on a dt 0.02
  seed for its plateau before pre-registering on it (logbook/0107). And **a queue launches
  the launcher's defaults**: `launch-queue.ps1` passes a seed, a worker and the hash and
  nothing else, so a dial screened on the command line has to be written into the
  launcher before the queue starts; round 41's first three seeds ran a minute on 11,000
  units and 25 J because it was not (`runs/r41mis-s*`, stopped and renamed).
- **A terminal-only self-edge recurses to the depth cap, and a knot of parts is free light.**
  `Developer.Expand` asks a node's recursive limit only of an edge that is not terminal-only,
  and a terminal-only edge fires exactly when the limit is spent, so a `link` node with a
  terminal-only edge to itself and a limit of 1 grows to `MaxDepth` 8 (nine parts with the
  root) and a second root edge into it fills `MaxParts` 16. With a turn on the edge the chain
  folds into a ball in which every part overlaps every other; PhysX resolves each pair on
  every step and never separates them, so `pairs/body` climbs with the knots and `stuck %`
  reads 100. The knot pays because a body's light income and its shadow are both the sum of
  its parts' projected areas and a body never shades itself, and because D098 made a part
  cost the tissue price alone. Round 41b's knots were articulated and round 41c's rigid, so
  `pairs jnt %` reads high or low for the same cause; read `contactBodies` against
  `contactPairs` (twenty pairs per touching creature is a body touching itself) and run
  `scripts/overlap/run.ps1` on a snapshot (the project is `src/Evosim.Overlap`), which counts
  a snapshot's self-overlapping part pairs and reproduced the physics' pairs per body in
  every seed (logbook/0107's last section, 2026-09-19). Round 40 never showed it because its
  economy priced every part in matter. **From D099 (the same day) both rules are closed, and
  three things read differently.** `RunConfig.LightSilhouetteCap` (`EVOSIM_SILHOUETTE`,
  header `silhouette on`/`off`, off by default so every recorded config replays, on from
  round 41d) caps what a body earns on and shades with at its convex hull's surface over
  four, every part's share scaled by one factor; the field refuses every `config.json`
  written before it, rounds 41 to 41c included. `Developer.Expand` asks the recursive limit
  of every edge, so a stored genome with a terminal-only self-edge develops to two or three
  parts where it developed to nine or sixteen (the three knots under `inocula/`); a genome's
  body is a property of the build, as a config's world is. And the hull in Core
  (`Geometry/ConvexHull`) has a face ceiling: a degenerate cloud (random founders stack parts
  exactly on each other) falls back to the bounding box and `Phenotype.SilhouetteFellBackToBox`
  counts it, which nothing in a run report surfaces yet; the probe reports it per snapshot
  and read zero on every recorded body. The absorptive log's `TotalLitArea` column stays the
  uncapped sum.
- **From D100 and D101 (2026-09-19) the water is held and a folded body is not born, and
  both tunables refuse every earlier config.** `FluidConfig.WaterHoldSeconds`
  (`EVOSIM_WATER_HOLD`, header `water held 0.5 s` or `water per link`, default 0) samples
  the streams once a body at its root and holds the velocity and acceleration for that
  long; 0 is the recorded per-link sampling. `RunConfig.SelfOverlapDepthFraction`
  (`EVOSIM_SELF_OVERLAP`, header `selfOverlap 0.1` or `off`, default 0) makes a body with
  two non-adjacent parts overlapping deeper than that fraction of the smaller part's
  thinnest half-extent a stillbirth at `World.Admit`, founders and inoculants included,
  counted in `stillbirths` and `selfOverlapStillbirths` (the table's `self stillb`, appended
  at the end); the physics of self-collision stays on. Both are per-step or per-birth
  changes and a new realisation of every seed; rounds 41 through 41d's configs are refused
  by the build. The cap did not stop the bush: round 41d grew a one-node three-self-edge
  bush of sixteen parts that earns 1.7 times a leaf's light on the same matter with the cap
  binding (logbook/0108's last section, `inocula/bush-16-r41d-s2-15000.json`), because a
  spread body has more hull than a packed one, which is honest geometry; the fold's cost was
  the overlap, and D101 refuses that. The profile's instruments came in with them:
  `wallHarness<Phase>Ms` for ten phases and `wallFluid{Gather,Water,Compute,Apply}Ms` with
  `harnessBodySteps` and `fluidLinkSteps` on every row, the footer's `harness split`,
  `fluid split`, `harness per body-step` and `fluid per link-step` lines
  (`logbook/specs/harness-profile-spec.md`). At round 41d's crowd the wall was PhysX 25%,
  the grid 11%, the harness 64%, of which the drag pass 57% and the throw trace 20%; the
  next cheapening is reading the solver once a step and sharing it between the drag pass,
  the trace and the sensors, which keeps identity.
- **A stopped arm leaves `Temp/UnityLockfile` on its worker, and `new-worker.ps1` refuses
  the refresh.** `stop-arm.ps1` kills the process, the lock stays, and the refresh reads it
  as an open Editor; on 2026-09-20 four workers were "refreshed" this way, none moved, and
  the smoke that followed recorded the previous build's `simHash` with a header missing the
  new tokens. With no Unity process running, delete the lock and refresh again, and read
  the header tokens of the smoke before taking its hash. A worktree's worker is a copy too:
  an edit in the worktree reaches `<worktree>/unity-wN` only by a refresh from the worktree.
- **A background shell loop outlives the session that armed it, and every re-arm adds a
  copy.** A watch written as `while true; do …; sleep 120; done` and started as a background
  task or a monitor is not stopped when the monitor expires, when the session compacts or
  when it restarts: the handle dies and the Git Bash tree stays. Four days of thirty-minute
  re-arms left dozens of copies of the watches of rounds 40 to 41e running at once. While
  their session lived they were only waste. When it went, each child they forked (a
  `grep`, a `stat`, a `sleep`, about forty an iteration a copy) asked Windows for a console,
  Windows 11 hands a new console to Windows Terminal, and on 2026-09-20 several hundred
  terminal windows in twenty minutes took the owner's desktop down. The owner rebooted and round 41e lost its three arms at 10,000 to
  17,000 s (HANDOFF). The rules from it. **A watch is one look that exits**
  (`scripts/watch-round.py <round> --read <read script>`: one Python process, the state in
  `scratch/logs/<round>-watch.json`, only what is new printed; **its read call is
  `<read script> <second> <arm>`**, and a read script written to `--arms` alone prints a
  usage error at every mark, which rounds 44 and 45 did until 2026-09-23; `r45-read.py`
  accepts both forms, and a read script also takes the newest run directory of an arm
  rather than refusing a stopped launch's sibling), and **the schedule belongs
  to the session** (its cron or wake-up, which cannot outlive it), never to a shell loop.
  Run `scripts/sweep-orphans.ps1` at the start of every session and before arming
  anything, and `-Kill` what it lists. A one-off wait (`until grep …; do sleep 20; done`)
  carries a deadline in the loop's own condition, because it orphans the same way. When
  the owner says the machine is misbehaving, list processes by name and by creation time
  before anything else: the storm was visible in one query as 300 `bash.exe` under an hour
  old.
- **From 2026-09-22 there are two farms, and the one out of Unity is the one that runs.**
  `src/Evosim.Farm` is a .NET 8 console that steps Core's `World` with `src/Evosim.Dynamics`
  (Featherstone in doubles, one creature at a time, bit-identical at any thread count;
  `fable-propose-own-solver.md` until it is absorbed). It binds the same `EVOSIM_*`
  variables as `EvolutionRun`, plus `EVOSIM_THREADS` and `EVOSIM_RUNS_ROOT`, and reproduces
  a launcher's `configHash` (round 42's `ff557bce…`) and its `config.json` byte for byte.
  Round 42 seed 1's world ran 30,000 s in 24 minutes on it at 16 threads, both books
  closed, against ten hours in Unity. Eight things bite. **The manifest has no `simHash`**:
  `run.json` carries `engine: "dynamics"`, `dynamicsHash`, `farmHash`, `threads` and
  `processId`, and a run is stopped by a `STOP` file in its run directory, which
  `stop-arm.ps1` writes when the manifest says `engine: "dynamics"` (`-RunsRoot` for a run
  outside `runs/`). **A farm run can be continued from a
  checkpoint, and the continuation is the run** (`3561ec3`, `logbook/specs/checkpoint-spec.md`):
  `EVOSIM_CHECKPOINT_EVERY` writes `checkpoints/NNNNNNNNN.ckpt`, a recording setting that
  moves no hash, and `run-farm.ps1 -ResumeFrom <arm> -At <s>` writes the same rows from that
  second as the unbroken run would have. Three things about it bite. The config comes from the
  resumed run and the recording cadences from the checkpoint's header, so a launcher's
  environment is ignored except for a cadence it sets by name. A resume across any of the
  four hashes is refused unless `EVOSIM_ALLOW_SOURCE_MISMATCH`, and is then a cousin the
  manifest marks. And the last checkpoint after a `STOP` is the second the run stopped at,
  because the file is written after the report row and before the stop is acted on.
  **A checkpoint carried everything the solver reads and not everything a sense reads,
  until `StateVersion` 6** (2026-09-23): `Organism.PartDamage`, the health each part has
  lost over its life and what the `Damage` sensor channel reports, was not written, so
  every restored wounded body sensed nothing, and a resume of round 45 seed 2 parted from
  the run at its first sample in the two jointed bodies among sixteen wounded whose brains
  read the channel (six of 1,925 from the 5,000 s checkpoint; one body both times). The
  row acceptance never saw it because nothing in round 42's world bites. The check that
  names such a member is `Evosim.Farm.exe --verify-checkpoint <run dir or .ckpt> <seconds>`
  (`CheckpointFidelity`): it founds or restores a world, steps it, writes it, restores that
  and compares the two member by member, skipping by name what a step fills before it
  reads; run it on a world that has the thing you added (a bitten crowd, a grown one)
  before trusting a resume of it, and every `StateVersion` bump refuses every checkpoint on
  disk, round 45's included, which are cousins for that reason anyway. **The
  JIT decides the bits**: .NET's
  tiered compilation gives quick-JITted and optimised loops different floating-point
  results on a rounding edge, so every project that reports a digest sets
  `<TieredCompilation>false</TieredCompilation>` and `DynamicsWorld.Step` takes one
  `Parallel.For` path at every thread count, including one; a serial `for` beside it
  parted from it at one thread. **.NET 8 and Mono format a float differently under
  `"R"`** (17.641891 against 17.6418915), and that formatter feeds the config hash; no
  recorded run is affected, and a hash read from a Unity build is compared to a farm's only
  through a launcher both have written. **The contact instrument is renamed**: the farm's
  contact is a soft push between spheres, so its columns are `overlaps`, `ovl/body`,
  `ovl jnt %` and `ovl held %` and its stats fields `overlapPairs*` and `bedOrGlassBodies*`;
  the numbers do not compare with `pairs/body` across the change; `analyse-arm.ps1` and
  the Python reads (`scripts/reads/contact_aliases.py`, one table for the columns and one
  for the fields) resolve either name and print a note saying which engine's census was
  read. **No recorded run replays on it**, and none of the farm's
  replays in the Unity theatre; a `poses.jsonl` beside `positions.jsonl` is the first
  bridge. **In the Unity farm a body's own parts collide and a driven joint is mostly not
  free to turn** (the parity swims, HANDOFF 2026-09-21): forty of round 42's jointed bodies
  alone in still water agree with the new solver 40 of 40 within 0.05 rad with PhysX's
  self-collision off and 12 of 40 with it on, and a hand-built stroke agrees to the fifth
  decimal; so every round's muscle through 42 was jammed by its own siblings, and a
  reading about joints from those rounds is read with that. **D100's hold undoes D090**: a
  neutral body under a 0.5 s water hold drifts 22 m from its parcel in 1,000 s, and 5 cm
  under per-link sampling; a hold of 0 on the new engine is an owner ruling in the
  proposal. **The matter residual grows on the farm** (−3.2e-04 of 1,500 units at
  30,000 s against Unity's ±1e-04), and it is float rounding at the body's account
  against the fields' doubles, not a handoff fault: the farm's `Observe` call equals
  Unity's line for line. **From `0c19f0d` (2026-09-22 evening) the accounts are doubles
  and the residual closes**: `Organism.Energy`, `TissueJoules` and `AdultTissueJoules`,
  the ledger's sums and every booked sum in `World` (the reserve update, `Grow`, `Bury`,
  `Conceive`'s price, whose 100 J overhead against a 0.4 J body rounded 1e-5 J a birth
  into neither book) are doubles; the same seed read 1.9e-07 units at 3,000 s where the
  float build read 1.1e-04 (`dblA`/`dblB`, a factor of about 590), and the old build's
  two residuals were one fault in two units (−1.097e-02 J over 100 J/unit = 1.097e-04
  units, exactly). It is a new realisation of every seed: round 43 does not replay on
  it, `BoxPathTests`' golden and `ParallelIdentityTests`' word are re-pinned, and every
  checkpoint on disk is refused (`WorldState.StateVersion` 2, `Checkpoint.Version` 2). What
  stays float, deliberately: the ledger's nine per-step terms (each handed to the field's
  float door exactly as stored, so widening them would add a rounding), the genome's
  traits, `PendingWorkJoules` (both sides of its booking read the same float), and the
  sensor and HUD readings. **A float still enters a booked sum at exactly one place, the
  field's door**: `IMatterField.Deposit`/`Take` are float over double cells, the same
  float is booked on both sides of every transfer, and the half-ulp left at the hand-over
  is the 1e-7 that remains. Widening the field API is the next step if a round needs it.
  **`ParallelIdentityTests` is `Slow` now and pins this build's own word** rather than
  the pre-threading build's, so Core's thread-identity gate is `-All` and nothing in the
  default run. **`sweep-orphans.ps1` lists a detached farm run's `sh.exe`**
  as an orphan; it is one exiting script, not a loop, and is not killed. **From PowerShell,
  `bash` is WSL's** (`C:\WINDOWS\system32\bash.exe`) and cannot see `D:/`; a detached
  launcher names `C:\Program Files\Git\bin\bash.exe`. **The water pass runs across the
  world's threads from 2026-09-23** (`c6cbba8`): it had been serial for `CurrentField`'s
  sake and was 41% of a step at 1,800 bodies on five threads, more than the bodies' own
  phase; it now runs under `CurrentField.PinInstant`, which fills a slot for the samplers'
  phase `2π·s/P` *and* for the analytic acceleration's `(2π/P)·s` (the two differ by an ulp
  on half the steps of a run, and each grouping is what its recorded worlds replay on),
  and every lookup under the pin is a read. Bit-identical: a resume of round 45 seed 2's
  2,500 s checkpoint on the serial and the parallel build agrees in 4,290 values over 30
  samples. A new current mode has to be samplable under a pin, which means no per-call
  memo the samplers read back through fields; the bed's one-entry memo is thread-static for
  that reason. The Unity farm's water pass is untouched and still on its main thread.
- **Mono and RyuJIT do not agree on a double sum, so the Editor cannot replay a farm run**
  (2026-09-22, `ade13dd`). `Evosim.Farm` and `Evosim.Dynamics` are Unity local packages and
  `DynamicsReplayCheck` runs the farm's own loop in the Editor: the counts agree at every
  sample and `auditResidual` parts at the first one, before any body has moved. Not threads,
  not tiering: the runtime. A farm run is watched from its record (`positions.jsonl`,
  `poses.jsonl`), and a farm-side identity claim is made on the farm (`digest.jsonl` at 1
  and N threads), never across the two runtimes. **Live play in the Editor is a cousin by
  construction, and says so** (`a80b1c9`, `dab9b78`): the Runner's live mode steps a farm
  world on `Evosim.Dynamics` inside the Editor from a founding or from a checkpoint
  (`CheckpointPath`/`CheckpointSeconds`, `EVOSIM_THEATRE_CHECKPOINT`; `EVOSIM_THEATRE_SEEK`
  is the checkpoint second when one is named and the seek target otherwise), labelled
  `continued from checkpoint at <s> s (cousin)` with any differing hash named, refusing
  nothing. `LiveCheckpointCheck` showed the counts agreeing for 200 s after a restore while
  `audit` and `meanHeight` parted at the first stepped sample. From `91aea20` the interface
  is up in live mode and answers every panel from the live world through one path for both
  engines: the provenance word is always `COUSIN`, the coverage segment is a drift line
  (`drift: parted at t=410, 0 of 20 agreed`, Mono against the farm's .NET, a reading and
  never a verdict), the popover's `physics jobs` and `sim hash` rows say `not in live mode`
  and the three digests that decide the trajectory stand in their place, the ancestry and
  the dead panel are withheld as on any cousin, and a click selects by a ray against each
  part's own box because a live body has no collider. **A checkpoint version bump orphans
  every checkpoint on disk**: the double-accounts build took the layout from 1 to 2 while
  `scratch/checkpoint/runs/ckA` was the fixture, so the fixture was refused and
  `LiveUiCheck` needed `EVOSIM_THEATRE_CHECKPOINT` pointed at one the build wrote
  (`scratch/live-ui/runs/ckUi` on 2026-09-22); the layout is 3 since the refusal split, and
  ckA, ckB and ckC were re-recorded on the reach-bound build the same night (the newest run
  under each arm; the restore and the checkpoint-free run both identical, the checkpoint
  spec's acceptance). Re-record them after every `StateVersion` or `Checkpoint.Version`
  bump, before the next regress. The founding live path (no checkpoint named) has no check
  yet.
- **`World.Observe` reads a body's centre of mass and throws when it is outside the box,
  and until `2771bf0` the farm's divergence check read the root alone.** Round 44 seed 1
  ended `status error` at 22,370 s on creature 7417: the root inside the bound and the
  centre 3 cm below a 45 m bed, so `CheckFinite` passed it and `Observe` threw
  (`ArgumentOutOfRangeException … height of 45.03 m in a world 45 m deep`). From that
  commit the check reads the centre against `World.HeightIsInTheWorld` too, and such a
  body dies as a counted `Diverged` death with a reason naming the centre. A run that
  ended this way is censored and read at its last sample; its rows stand. The same seed
  is the first the farm has slowed: physics cost per body-step rose 7.6-fold with
  `ovl/body` (0.026 to 0.82, 99% held) and not with links per body, so read `ovl/body`
  beside `x real time` on any seed that falls under 1x, and profile it from a checkpoint
  (`EVOSIM_CHECKPOINT_EVERY`, from round 45's launcher). **The slowdown was the contact
  grid's cell, and it is fixed** (2026-09-22 night, the bench's `--mode record`): the grid
  entered each body's bounding sphere in one cell and sized the cell at two of the
  *largest* radius, and seed 1's module chains, one a fan of seven leaves each 1.75 times
  the last with the seventh 14.6 m long (`scratch/logs/giant-7597.txt`, drawn by
  `scripts/plot-body.py`), took the largest radius to 21 m, the cell to 43 m in a 53 m
  tank and every body's candidate list to the whole crowd: 9.9 µs a body-step against 0.33
  on the same crowd at the genome minimum. The grid now enters a sphere in every cell it
  covers at a cell of two mean radii, exact at any cell (two touching spheres share a
  cell), sorted and deduplicated so the sum is the same sum in the same order: 1.9 µs on
  the ceiling crowd, every digest unmoved, and a 3,000 s regress of `r45fix-s4` identical
  in 141 fields at 300 samples with the lineage byte-equal. **A part's size is unbounded
  above** (`MaxPartVolume` is a million cubic metres and thin sheets never reach it), so a
  self-copying leaf whose edge scale mutates above 1 grows geometrically once the module
  gene lets it copy past its recursive limit; the owner ruled no bound (D107, 2026-09-22
  night): the price is to bound it, the support cost of the next base round's proposal. **The checkpoint writer refused a body that was a
  copy of its adult at scale exactly 1** (round 45's first launch, three seeds within
  7,000 s): a volume fraction within a float's rounding of 1 has a cube root of exactly
  1f, and `Grow`, `AtTheSameFraction` and the newborn's scaling all built a copy that read
  as "neither the adult nor scaled". `Phenotype.Scaled(1f)` now returns the body itself,
  which changes no number (the regress above covers it). The three runs are
  `runs/r45void-s1..3`; seed 2's manifest reads `running` because the exception fired
  inside the stop's own checkpoint. **A snapshot row carries the body's plan from this
  build** (`moduleCounts`, `lostPaths`, read by `GenomeJson.ReadModuleCounts` and
  `ReadLostPartPaths`): the theatre's `-From snapshot` and the bench's record mode draw the
  body the run stepped, and every earlier recording is drawn at the genome minimum, which
  is why no picture of round 44 showed the fan. **The reach bound is built and off**
  (`DevelopmentLimits.MaxBodyReachMetres`, `EVOSIM_MAX_REACH`, header `reach 3 m` or
  `reach off`, default 0 = the recorded world; its value is the owner's ruling on the
  proposal): development prunes a part whose farthest corner lies farther than the bound
  from the root's origin, with its subtree, and counts it in `Phenotype.PrunedForReach`
  (the root is never past it), and the module rule refuses an addition that would be
  pruned as a shape refusal (`ref shape`) before it is paid for. It is a tunable, so it
  refuses every `config.json` written before it, rounds 41 through 45's void launch and
  both fixtures included; the Dynamics crowd is `runs/r45fixb-s4` and the thread-identity
  config is `pfix2`'s, both round 44's world with the bound off, and `r45fixb-s4` replays
  `r45fix-s4` sample for sample in 141 fields (the tree's regress, `scratch/r45-build/
  regress.py`). The Unity farm does not bind the variable; a world built there carries the
  bound at 0 and its header has no `reach` token.
- **A blue screen beside a GPU probe was the file-system filter stack, and a minidump is
  readable without a debugger.** The one crash in this machine's log (2026-09-22, bugcheck
  0x3B) faulted in `FLTMGR.SYS` on a `dotnet.exe` thread, entered through the Xbox Gaming
  Services filter with Avast below it and the display driver on no frame, while a Gaming
  Services update had been stuck for half an hour (`logbook/specs/crash-2026-09-22.md`). The
  GPU spike lost a day to a suspicion the stack did not support. `C:/Windows/Minidump` and
  the WER queue need an elevated shell, so the owner copies them into `scratch/crash/`, and
  `scripts/read-minidump.py <dump>` prints the bugcheck, the process, the faulting driver and
  the stack's frames by driver. Read the dump before pausing anything on a crash's account.
- **The card runs the solver through ILGPU and nothing else, and its group size is set by
  hand** (logbook/0112, `spikes/02-gpu-featherstone/`). ComputeSharp refuses a local array
  in a shader and the step needs about 1,600 words of scratch a thread, so it is out. ILGPU
  compiles the whole step, but its PTX backend has no `Sin`, `Cos` or `Atan2` without
  `ILGPU.Algorithms` and `EnableAlgorithms()`, and its automatic group size is up to 2.9
  times slower than 32 because each thread holds 12.7 kB of local memory in double; the
  link ceiling is a compile-time constant that costs every thread whether used or not.
  Double on the 4090 is slower than sixteen of this machine's cores at 10,000 bodies; single
  is about six times faster and is a new realisation of every seed. Identity on the card
  holds across launch shapes, so a GPU identity claim is made at two group sizes. Run GPU
  code in the foreground with nothing else on the machine until the owner rules otherwise.
- **From D109 (2026-09-22 night) the matter starts as islands, the matter grid stirs at a
  launcher's rate, and every earlier config is refused again.** Six tunables
  (`MatterIslandWavelengthMetres`, `MatterIslandCover`, `MatterIslandDepthMetres`,
  `FoundersFollowMatter`, `LightShadeDepth`, `LightShadeDriftMetresPerHour`; header
  `matter islands 60 m cover 0.1 to 12 m · founders in matter · shade off`, or `matter
  uniform · founders anywhere`) refuse every `config.json` written before them, rounds 41
  through 45's void launch included; the fixtures are `pfix3` → `fixtures/r42-config.json`
  and `runs/r45fixc-s4`, and the Farm tests' round 42 hash is `c862fd2c510b82e9`. Six
  things bite. **`MatterMixingDiffusivity` was a hard default of 2 m²/s that no launcher
  named**, a hundred times the snow's; `EVOSIM_MATTER_MIXING` binds it and the header
  prints `matter-mix`, and a launcher that does not set it still gets 2. **The islands do
  not spread at the dial's rate**: at 0.02 m²/s explicit they spread at about 0.1 m²/s
  by the gyre's upwind transport on the 5 m cells (`bigF`, a Gaussian fit of the peak's
  decay), so a 60 m island is soup near 5,000 s whatever the dial says, and only a finer
  cell or another scheme would slow it. **At the slow rate a leaf's income is the flux into
  its cell**, `D × ρ`, not the tank's stock: a crowd strips its 5 m cells faster than the
  neighbours refill them, `upt lim` reads that pressure, and the same budget that founded
  round 44 at 2 m²/s starves after founding at 0.02 (`bigH`); read `upt lim` with the
  stirring in mind, and read any pre-D109 `upt lim` as the stock's. **The founder rule is
  a new realisation of every seed** when on (a refusal draws the placer's stream again) and
  bit for bit the recorded world when off. **The shade map saturates at 1 across every
  island column** and shades the deserts only (the first cut shaded by the raw map, and an
  island column at 0.72 to 0.8 of the light founded at a third of round 44's pace, `bigH`
  against `bigI`); it is off in round 45 and every launcher, by the owner's objection that
  light has no concentration. **The farm dumps the fields beside every snapshot**
  (`fields/NNNNNNNNN.matter.f32` every cell, `.snow-columns.f32` the column sums,
  `layout.json` the shapes; little-endian floats in the grid's own index order), a
  recording setting that moves no hash, and `scripts/field-map.py <arm> --runs-root … --at
  … --layers 3 --snow` draws the map with the bodies on it and prints the share of bodies
  standing in above-mean columns and, with `--footprint 100`, in the columns that were
  islands when seeded, which is 0114's J8 (the first share is a coin toss once the field
  is soup; the second still reads). The Unity farm binds none of the six,
  so a world built there is uniform and its header carries no `matter` token. And **a farm
  smoke holds its exe**: `dotnet build` fails to copy over a running `Evosim.Farm.exe`, so a
  build for the next screen goes to another output (`-o artifacts/Evosim.Farm/bin/Release-b`
  and `run-farm.ps1 -Exe`) while a screen runs; the manifest's `farmHash` is of the source
  and does not care which.
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
- **A subagent's default temporary location is its session scratchpad, which is outside the
  project.** A hook blocks any write outside the repository and stalls the agent until the
  owner approves it, which is a blocked task the caller may not notice for an hour. Every
  subagent brief therefore names the absolute path under `scratch/` it may write to
  (`D:\Projects\experiments\evolution-simulator\scratch\<task>\`) and forbids the session
  scratchpad and TEMP by name; "write under scratch/" alone was not enough on 2026-09-12.
  The same rule binds the calling agent: nothing of the project's is written outside the
  repository (Conventions, below).
- **Sample the pictures while a round runs, not only when it is written up** (owner,
  2026-09-12 evening: "sometimes you can't really evaluate something without actually
  seeing it"). The theatre-watch rule above covers the entry; this one covers the run. Take
  a frame or two from a live arm every few thousand simulated seconds (`theatre-snap.ps1`
  on a worker the queue is not using, one frame at a time on a loaded machine) and look at
  them, and say in the status what was seen. Round 37's crust at the glass was in the table
  for hours before anyone read it as a crust.
- **Say when a ruling blocks the work, and keep working on the rest** (owner, 2026-09-22
  evening: "If you're waiting on me, I want an explicit message saying you are blocked by a
  decision you need from me. I want you working all the time."). A status that lists open
  rulings among other news reads as information; the owner cannot tell the campaign has
  stopped on them. When the next step needs a ruling, lead with a line of the form "I am
  blocked on <decision> from you", and then put the decision in full, never in shorthand
  (the owner, ten minutes later: "Give me the full topic with recommendations and
  implications"): the topic in plain words for someone who has not read the proposal, the
  recommendation and why, what each option implies for the record, the rounds, the machine
  and the risk, and the exact question to answer. A pointer to a proposal file is not a
  request for a ruling. Then say what is being worked on meanwhile. Never end a turn idle while anything not gated
  remains: loose ends, instruments, measurements, the round-gap changes that are a new
  realisation of every seed and land best while no arm runs, pre-registration drafts and
  ledger screens for the round that waits.
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
