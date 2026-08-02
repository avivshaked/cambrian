# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Karl Sims–style evolved-virtual-creatures simulator in Unity. Genomes encode **both**
body plan and brain; creatures are grown from a recursive directed graph and evaluated in
physics. Aquatic locomotion first, terrestrial later.

**Five documents, deliberately non-overlapping.** Keep them that way — duplicated rationale
drifts and then none of it can be trusted.

| File | Answers |
|---|---|
| [`DESIGN.md`](DESIGN.md) | *What the system is* — the specification |
| [`DECISIONS.md`](DECISIONS.md) | *Why we chose this over that*, and what was rejected. Append-only; reversals get a new entry marking the old one superseded |
| [`research/LITERATURE-REVIEW.md`](research/LITERATURE-REVIEW.md) | *What the evidence says* |
| [`logbook/`](logbook/) | *What happened, and when* — dated entries on what was tried and what broke. Never a source of truth: it links to the documents above rather than restating them, and is the only one allowed to be out of date |
| `CLAUDE.md` (this file) | *What will bite you* |

**Two licences.** Code is MIT ([`LICENSE`](LICENSE)); the prose — `DESIGN.md`,
`DECISIONS.md`, `README.md`, this file, and everything under `research/` — is CC BY 4.0
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

Current state: **design complete, Spike 01 passed, Milestone 1 in progress.** `Evosim.Core`
has genome, development and RNG under test; the phenotype builder and the Unity project
proper do not exist yet.

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
- **Suspiciously good results are usually a broken measurement.** The budgets in
  `spikes/01-articulation-body/README.md` are derived backwards from throughput targets;
  beating them by 100× means the harness is wrong, not that the hardware is amazing.
- **`windows-il2cpp` is not installed** — only Mono. Fine for now; add it before the island
  model (Milestone 4), since per-creature brain evaluation is managed C# in the hot loop.

## Conventions

- Simulation output (`runs/`) and spike CSVs are gitignored; `FINDINGS.md` is tracked
  because DESIGN.md links to it.
- Genomes serialize to **JSON**, not binary — readable, diffable, and small enough that
  packing would be premature.
- Every evaluation must be reproducible from `(genome, seed, configHash)`. PhysX is not
  bitwise deterministic across machines or Unity versions, so the hash exists to *detect*
  mismatches rather than to promise portability.
