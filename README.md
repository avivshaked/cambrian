# Cambrian

**Evolved virtual creatures in Unity — bodies and brains co-evolved by quality-diversity search.**

Creatures are grown from a recursive directed graph that encodes *both* morphology and
neural control. They are dropped into simulated water, evaluated on how well they swim, and
selected by an algorithm that rewards being *different* as much as being *fast*. Nobody
designs the creatures. The interesting ones are discovered.

The name is the ambition: the Cambrian explosion was a rapid diversification of **body
plans**, which is exactly what a morphological archive is trying to produce.

> **Status: design complete, physics foundation validated, evolution not yet built.**
> The only code here is a disposable spike that answered one question about Unity's physics
> engine. Everything else is specification. This README does not pretend otherwise.

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

## What makes this repo unusual

**The design is evidence-backed, and the evidence is traceable.**

[`DESIGN.md`](DESIGN.md) is not a sketch. Most decisions in it cite peer-reviewed literature
with **page-level locators** — `[K12 §2.3, p.7]` points at an exact page of an exact paper.
[`research/`](research/) contains the full review that produced them, including a section
that states its own limitations without softening them.

That process changed the design three times, and one of those was a correction to my own
reasoning rather than to a fact:

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

---

## Measured, not assumed

Before building anything, one question had to be answered by running code rather than
reading: can Unity's `ArticulationBody` handle hundreds of creatures built, simulated and
destroyed per minute? [Spike 01](spikes/01-articulation-body/) answered it.

| Measurement | Budget | Measured |
|---|---|---|
| Build + teardown, 10-part creature | < 15 ms | **0.335 ms** |
| Step cost @ 64 tiled creatures | 0.15–0.30 ms/creature | **0.0186 ms** |
| Scaling, 64 creatures vs 1 | must be sub-linear | **0.28×** |
| Determinism, 10 runs, same seed | < 1e-4 m drift | **bitwise identical** |

Sub-linear scaling means PhysX genuinely parallelises across solver islands, so simulating
many creatures in one scene works. Extrapolated: **~1,600 evaluations per minute from a
single process** — the original target was that figure across *ten* processes.

The first run of this spike reported numbers 150× better still, because with zero gravity
and no actuation the creatures fell asleep and PhysX skipped them entirely. The harness now
reports mean body speed so it can *prove* the creatures are awake. Results that look too
good usually are.

---

## Layout

```
DESIGN.md                     the specification — start here
DECISIONS.md                  why things are the way they are, and what was rejected
CLAUDE.md                     orientation for AI assistants
LICENSE / LICENSE-DOCS        MIT for code, CC BY 4.0 for prose — see below
scripts/githooks/             pre-commit guard (enable with core.hooksPath)
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

| # | Milestone | Ends with |
|---|---|---|
| 0 | Project scaffold, physics config | ✅ done |
| — | Spike 01 — `ArticulationBody` at scale | ✅ passed |
| 1 | Genome, development rules, phenotype builder | Spawn a random creature and watch it flop |
| 2 | Evaluation harness: tiling, seeding, fluid forces, anti-exploit checks | Measured throughput |
| 3 | Multi-BC MAP-Elites + oscillator controllers, water | **First real swimmers** |
| 4 | Island model across processes | Overnight runs, a full archive |
| 5 | Land: contact, gravity, anti-degenerate fitness | Walkers |
| 6 | Full brain graph, sensors, photoreceptors | Target-following, reactive behaviour |
| 7 | Replay, archive gallery, charts, fluid validation harness | The showpiece |
| 8 | Sandbox: currents, predators, obstacles | You as the selection pressure |

Milestone 3 is the one that matters. Everything before it is scaffolding.

---

## Reading the citations

Claims in `DESIGN.md` cite `[KEY §section, p.N]`. `p.N` is the **PDF page number**, which
matches the `### Page N` heading in that paper's extracted `source.md`. Keys resolve in
[`DESIGN.md` §13](DESIGN.md#13-references-and-source-access).

§13.4 quarantines references that were only ever seen inside *other* papers' bibliographies,
marked not-independently-verified. Two of them are load-bearing. That is stated rather than
hidden, because a review that hides its weak joints isn't worth much.

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
