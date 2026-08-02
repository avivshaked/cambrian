# Cambrian

**Evolved virtual creatures in Unity — bodies and brains co-evolved by quality-diversity search.**

Creatures are grown from a recursive directed graph that encodes *both* morphology and
neural control. They are dropped into simulated water, evaluated on how well they swim, and
selected by an algorithm that rewards being *different* as much as being *fast*. Nobody
designs the creatures. The interesting ones are discovered.

The name is the ambition: the Cambrian explosion was a rapid diversification of **body
plans**, which is exactly what a morphological archive is trying to produce.

> **Status: creatures grow and move. No water, no fitness, no evolution yet.**
> A genome develops into a body, the body builds into a physics articulation, and a sandbox
> scene spawns one and drives it. Nothing selects anything — the controller is a test sine
> wave standing in for a brain, and there is no fluid to swim in, so there is not yet
> anything to be good at.

---

## The idea

This is the lineage that starts with Karl Sims' 1994 *Evolving Virtual Creatures* — the
work that produced the famous videos of blocky creatures teaching themselves to swim, walk
and jump. Sims needed a Connection Machine CM-5. A modern desktop has rather more to work
with.

The genome is a **graph, not a list**. Cycles in the graph mean a single node can unfold
into a five-segment spine; reflection flags mean one mutation can turn a lopsided thing into
a bilaterally symmetric one. Neurons live *inside* body nodes, so when recursion duplicates
a segment it duplicates that segment's controller too — which is, structurally, how a
central pattern generator works. That is why creatures in this family produce coordinated
travelling-wave gaits instead of noise.

---

## Where the design comes from

Most decisions in [`DESIGN.md`](DESIGN.md) cite peer-reviewed literature with page-level
locators — `[K12 §2.3, p.7]` resolves to an exact page of an exact paper.
[`research/`](research/) holds the review those citations came from; §7 lists what the review
did not establish, and two of its six questions remain only partly answered.

The design was written first and the review run against it. That changed three things, one
of which was a correction to reasoning rather than to a fact:

- **A missing failure mode.** Co-evolving body and brain is pathological: a morphological
  mutation invalidates the controller co-adapted to the old body, so selection discards the
  offspring even when the new body is better. Morphology then stagnates within a few dozen
  generations. The first draft of the design didn't mention this at all. It now drives the
  whole search architecture.

- **A wrong cost model.** The draft reasoned that a crude fluid model was fine for making
  something nice to watch, and only compromised scientific accuracy. Corucci et al. show the
  opposite is the real problem: simplified fluid dynamics **collapses morphological
  variety** — no fish, no squid, just a gallery of similar medusoids. Visual variety was the
  entire goal, so the cheap model was failing at the thing it was supposed to be good at.

- **A threat that dissolved.** CPPN encodings looked like they'd force a genome rewrite.
  A survey of every published encoding comparison showed that advantage is confined to
  *soft-body* creatures; on rigid articulated bodies, recursive graph encodings win or tie.

Then building it corrected the specification, which is a different thing from correcting the
design. Four changes in `DESIGN.md` §0c came from an implementation or from watching
creatures move, and none could have come from reading:

- A recursion rule that, read literally, developed every non-recursive genome into a single
  box. Ambiguous rather than wrong, but not implementable as written.
- Reflection about the wrong axis puts a "mirrored copy" exactly on top of its original.
  69.7% of random creatures were partly inside themselves, and all of them looked fine.
- Joint torque applied without a reaction on the parent lets creatures manufacture angular
  momentum from nothing. Every headless check passed; a person watching spotted it at once.
- Two anti-exploit checks that are conservation laws rather than thresholds, so they need no
  tuning and cannot be satisfied by an impressive-looking failure.

The last two were found by looking at the screen after the test suite was green.
[`logbook/`](logbook/) records how, [`primer/`](primer/) explains what the mechanisms
actually do.

---

## Physics validation

One question had to be answered by running code rather than reading: can Unity's
`ArticulationBody` handle hundreds of creatures built, simulated and destroyed per minute?
[Spike 01](spikes/01-articulation-body/) measured it.

| Measurement | Budget | Measured |
|---|---|---|
| Build + teardown, 10-part creature | < 15 ms | **0.335 ms** |
| Step cost @ 64 tiled creatures | 0.15–0.30 ms/creature | **0.0186 ms** |
| Scaling, 64 creatures vs 1 | must be sub-linear | **0.28×** |
| Determinism, 10 runs, same seed | < 1e-4 m drift | **bitwise identical** |

Sub-linear scaling means PhysX genuinely parallelises across solver islands, so simulating
many creatures in one scene works. Extrapolated: **~1,600 evaluations per minute from a
single process** — the original target was that figure across *ten* processes.

The first run of this spike reported numbers more than an order of magnitude better still,
because with zero gravity and no actuation the creatures fell asleep and PhysX stopped
integrating them. The harness now reports mean body speed alongside every timing, so a
sleeping scene is visible in the results table. Full account in
[logbook/0002](logbook/0002-the-spike-that-was-too-fast.md).

---

## Layout

```
DESIGN.md                     the specification — start here
DECISIONS.md                  why things are the way they are, and what was rejected
primer/                       what was built and why it is interesting — start here to learn
logbook/                      dated entries on what was tried, and what broke
CLAUDE.md                     orientation for AI assistants
LICENSE / LICENSE-DOCS        MIT for code, CC BY 4.0 for prose — see below
src/
  Evosim.Core/                genome, development, RNG — no UnityEngine, runs headless
  Evosim.Core.Tests/          xUnit suite over the above
scripts/
  core-test.ps1               build + test Evosim.Core without the Editor
  githooks/                   pre-commit guard (enable with core.hooksPath)
research/
  LITERATURE-REVIEW.md        PRISMA-style review; §7 is the honest-limitations section
  FETCH-RESULTS.md            exact retrieval URL for every paper (reproducibility record)
  papers/                     NOT COMMITTED — see below
spikes/
  01-articulation-body/       disposable Unity project; physics validation
    results/FINDINGS.md       measured output
```

**`research/papers/` is deliberately absent.** It holds copyrighted publisher PDFs and their
machine extractions; two were obtained through a university subscription. Converting a
paywalled paper to markdown doesn't make it redistributable. `FETCH-RESULTS.md` records the
exact source URL for every paper, so anyone with equivalent access can rebuild the set.

---

## Getting started

### Requirements

| | |
|---|---|
| **OS** | Windows. Unity is cross-platform, but the run scripts here are PowerShell |
| **Unity** | `6000.5.6f1`. Other Unity 6 versions will very likely work — the Editor offers to upgrade the project on first open |
| **Disk** | ~15 GB for the Editor |
| **Git** | any recent version |

**There are no other dependencies.** No package manager, no lockfile, no third-party
libraries — the spike uses only Unity's built-in physics. Nothing to `npm install` or
`pip install`.

### 1. Install Unity

1. Install [Unity Hub](https://unity.com/download) and sign in (a free Unity account is required).
2. **Activate a licence** — ⚙ → **Licenses** → **Add** → **Get a free personal license**.
   *Don't skip this.* Without it the Editor installs fine but silently refuses to open
   projects, which looks like a broken install.
3. **Installs** → **Install Editor** → pick **`6000.5.6f1`** (under *Archive* if it's no
   longer in *Official releases*).
4. On the modules screen: take **Windows Build Support (IL2CPP)**, and **untick Microsoft
   Visual Studio Community** if you already have Visual Studio — it installs a second copy.
   Nothing else is needed.

### 2. Clone

```powershell
git clone https://github.com/avivshaked/cambrian.git
cd cambrian
git config core.hooksPath scripts/githooks   # enable the pre-commit guard
```

That last line is worth running. `.git/hooks` isn't tracked, so the guard only applies once
you point git at the tracked copy. It blocks commits containing copyrighted PDFs, secrets,
stray email addresses, Unity build output and oversized files — see
[`scripts/githooks/pre-commit`](scripts/githooks/pre-commit).

### 3. Run the spike

Auto-detects your Editor rather than hardcoding a path:

```powershell
$unity = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -Directory |
         Sort-Object Name -Descending |
         ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } |
         Where-Object { Test-Path $_ } | Select-Object -First 1

$proj = Join-Path $PWD 'spikes\01-articulation-body'
$log  = Join-Path $env:TEMP 'spike.log'

Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    '-projectPath', $proj, '-batchmode', '-quit', '-nographics',
    '-executeMethod', 'Spike.EditorTools.SpikeEntry.Run', '-logFile', $log)

Get-Content (Join-Path $proj 'results\FINDINGS.md')
```

First run takes a few minutes — Unity imports assets and compiles. Later runs are ~30 s.

### What success looks like

`spikes/01-articulation-body/results/FINDINGS.md` reports six measurements, all `**PASS**`,
plus CSVs of the raw data. Numbers will differ from those quoted above — they were taken on
an i9-13900K — but the *shape* should hold: build+teardown well under 15 ms, and per-creature
step cost **falling** as tile count rises. That falling cost is the whole point; it's what
proves PhysX is parallelising across solver islands.

If the mean-speed column in M3 collapses toward zero, the creatures fell asleep and the
timings are meaningless. See §M3 in [the spike spec](spikes/01-articulation-body/README.md).

### 4. Run the Core tests

```powershell
./scripts/core-test.ps1
```

`Evosim.Core` — the genome and the genotype-to-phenotype development that grows a creature
from it — has no `UnityEngine` dependency, so it builds and tests as ordinary C# in about a
second. **No .NET SDK is required:** the script uses a system-wide one if you have it and
otherwise falls back to the complete .NET 8 SDK that ships inside the Unity install.

### Troubleshooting

| Symptom | Cause |
|---|---|
| Unity exits instantly, no log file written | You used `& $unity ...` instead of `Start-Process`. Direct invocation fails silently |
| `error CS` in the log | Check with `Select-String -Path $env:TEMP\spike.log -Pattern 'error CS'` |
| Editor opens but won't load the project | Licence not activated — see install step 2 |
| Unity Hub CLI fails with `Cannot find module '--headless'` | Known bug in Hub 3.14; the Electron wrapper eats the `--` separator. Use the GUI |

### Rebuilding the research sources

Not needed to run anything. `research/papers/` is gitignored, but
[`research/FETCH-RESULTS.md`](research/FETCH-RESULTS.md) lists the exact retrieval URL for
every paper — six are open access, two need a university subscription. Text and figure
extraction used [PyMuPDF](https://pymupdf.readthedocs.io/) (`pip install pymupdf`).

---

## Roadmap

| # | Milestone | Ends with | |
|---|---|---|---|
| — | Spike 01 — `ArticulationBody` at scale | Measured, six for six | ✅ |
| 0 | Unity project, assemblies, URP, physics config | Empty scene that builds headless | ✅ |
| 1 | Genome, development rules, phenotype builder | Spawn a random creature and watch it flop | ✅ |
| 2 | Evaluation harness: tiling, seeding, fluid forces, fitness, anti-exploit checks | Measured throughput; go/no-go on the cost model | ← next |
| 3 | Multi-BC MAP-Elites + oscillator controllers, water | **First real swimmers** | |
| 4 | Island model across processes | Overnight runs, a full archive | |
| 5 | Land: contact, gravity, anti-degenerate fitness | Walkers | |
| 6 | Full brain graph, sensors, photoreceptors | Target-following, reactive behaviour | |
| 7 | Replay, archive gallery, charts, fluid validation harness | The showpiece | |
| 8 | Sandbox: currents, predators, obstacles | You as the selection pressure | |

Milestone 3 is the one that matters. Everything before it is scaffolding.

Two things are deliberately missing from `Evosim.Core` and are needed before any search can
run: mutation operators (`DESIGN.md` §4.5) and genome serialization (§9).

---

## Reading the citations

Claims in `DESIGN.md` cite `[KEY §section, p.N]`. `p.N` is the **PDF page number**, which
matches the `### Page N` heading in that paper's extracted `source.md`. Keys resolve in
[`DESIGN.md` §13](DESIGN.md#13-references-and-source-access).

§13.4 quarantines references that were only ever seen inside *other* papers' bibliographies,
marked not-independently-verified. Two of them are load-bearing, and nothing should be
promoted out of that table without checking the source directly.

---

## Licence

Two licences, because this repository is mostly not code.

- **Code** — [MIT](LICENSE). The spike, and everything built later.
- **Documentation** — [CC BY 4.0](LICENSE-DOCS). `DESIGN.md`, `DECISIONS.md`, this README, and
  everything under `research/`.

The split is deliberate. A code licence has nothing useful to say about a literature review,
and the attribution requirement in CC BY matches the norm the written work is already
following: if you reuse the review's conclusions, cite it — and cite the underlying papers,
which is what §6 of the review is for.

Quotations from academic papers throughout the documentation are used under fair dealing and
remain their authors' and publishers' copyright; they're attributed at the point of use. The
papers themselves aren't distributed here.
