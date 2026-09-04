# Cambrian

**An evolving aquatic ecosystem in Unity — bodies and brains encoded together, selected by
whether they can afford to stay alive.**

Creatures are grown from a recursive directed graph that encodes *both* morphology and
neural control. They are dropped into simulated water where **energy is a conserved
budget**: they earn it from sunlight, drifting nutrients, or each other, and they spend it on
tissue, on thinking, and on every movement they make. Run out and you die. Accumulate a
surplus and you reproduce.

**There is no fitness function.** Nothing is scored, and nothing is being taught to swim. If
swimming is what keeps a creature alive it will appear on its own — and if something else
appears instead, that is the more interesting answer. A creature made only of rigid feeding
cells is a plant; one that eats plants is a herbivore; the word "species" appears nowhere in
the code.

The name is the ambition: the Cambrian explosion was a rapid diversification of **body
plans**, which is what an open-ended ecosystem is being built to produce.

> **Status: the ecosystem runs, and it holds a food chain.**
> Genomes develop into bodies, bodies build into articulations driven by their own evolved
> brains, and energy is a conserved budget audited to 0.0000% across the whole food web. A
> finite competed-for sun, currents and mixing, senescence, a matter currency, a buoyancy
> organ, living excretion, marine snow and a vent are all in and measured
> ([`DECISIONS.md`](DECISIONS.md) D023–D071). Eighteen scored rounds
> ([logbook/0036](logbook/0036-the-floor-gives-back.md) onward) found the constraint one
> layer at a time — the floor, the drowning, the matter ratchet, the stomach's gearing — and
> the last of them was the flux: the second trophic level was fed at one percent of the
> first because producers fed the water only by dying. With exudation (D070) the standing
> goal is met, 4 of 5 seeds in [logbook/0054](logbook/0054-the-confirmation.md): a
> connected absorptive clade alive and breeding through two lifetimes, on an inherited
> producer lineage, floor closed. The failing seed named the next constraint — matter at
> depth (D071, screening). Movement has never paid its energy cost — the cost side is
> closed, the prize side is open — and throughput binds every remaining question.

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

Where this departs from Sims is the **selection**. Sims scored creatures on a fitness
function and bred the winners. Here nothing is scored: a part is made of a specific tissue,
each tissue earns and costs energy differently, and a creature that cannot cover its own
metabolism dies without anything having to judge it. Two consequences fall out rather than
being designed in:

- **Trophic strategy is morphology.** Photosynthesis, filter-feeding and predation are cell
  types, so a species is a distribution of cell types over a body plan and speciation is a
  change in that distribution. No separate species or niche concept exists.
- **Motion costs a part.** Only a *link* cell may carry a joint, so two parts cannot move
  relative to each other without one between them — and link tissue is the most expensive
  there is, charged for its strength whether or not it moves. A creature with no links is a
  rigid body drifting on the current, which is what a plant is, without "plant" being defined
  anywhere.

Which of those is the right call is genuinely open. §5A was reasoned from first principles
and labelled as such; review round 3 (2026-08-29, [logbook/0035](logbook/0035-the-neuron-was-priced-in-1994.md))
has since searched the artificial-ecosystem and open-ended-evolution literature and
confirmed several of its bets at the primary sources.

---

## Where the design comes from

Most decisions in [`DESIGN.md`](DESIGN.md) cite peer-reviewed literature with page-level
locators — `[K12 §2.3, p.7]` resolves to an exact page of an exact paper.
[`research/`](research/) holds the review those citations came from; §7 lists what the review
did not establish, and two of its six questions remain only partly answered.

**The ecosystem's part of the design was reasoned first and read later.** Energy, cell
types, feeding and reproduction were built from first principles with §5A saying so in the
text; review round 3 then searched the open-ended-evolution and artificial-ecosystem
literature ([`research/LITERATURE-REVIEW.md`](research/LITERATURE-REVIEW.md) §0, round 3)
and the food-chain campaign's mechanisms since (excretion, the refuge, species accounting)
are labelled project inference in their D-entries where the review has not yet reached.

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
DESIGN.md                     the specification — start here (§5A is the ecosystem)
DECISIONS.md                  why things are the way they are, and what was rejected
primer/                       what was built and why it is interesting — start here to learn
logbook/                      dated entries on what was tried, and what broke
CLAUDE.md                     orientation for AI assistants — what will bite you
LICENSE / LICENSE-DOCS        MIT for code, CC BY 4.0 for prose — see below
src/
  Evosim.Core/                no UnityEngine, runs headless in ~1 s
    Genome/                   the recursive graph: nodes, edges, neurons, reproduction traits
    Development/              genotype to phenotype — growing a body from the graph
    Cells/                    what a part is made of, and how it earns and spends energy
    Mutation/                 the variation operators
    Serialization/            hand-written JSON; genomes, run settings, the run directory
    Environment/              fluid model — drag and added mass
  Evosim.Core.Tests/          xUnit suite over the above
unity/                        the real Unity project; Evosim.Core is a local package
  Assets/Evosim/Sim/          phenotype builder, effectors, fluid, sandbox scene
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
runs/                         NOT COMMITTED — simulation output, see below
```

**A run is a directory, not a file.** `config.json` holds every tunable and its own hash;
`lineage.jsonl` holds one row per creature ever born and one per death — a compact
`{"e":"b",...}` / `{"e":"d",...}` pair per event, drained from the world every report row;
`stats.jsonl` one row per sample;
`snapshots/` the world state. The two high-volume files are append-only and line-oriented, so
a run killed halfway leaves every completed row valid and readable — and can be watched live
by tailing it. Creatures are **rows, not files**: a genome measures ~5 KB and the working
estimate is 40,000 births an hour.

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

`Evosim.Core` — the genome, the development that grows a creature from it, the cell types,
mutation and serialization — has no `UnityEngine` dependency, so it builds and tests as
ordinary C# in under a minute (364 tests as of 2026-08-31). **No .NET SDK is required:** the script uses a system-wide one
if you have it and otherwise falls back to the complete .NET 8 SDK that ships inside the
Unity install.

The no-dependency rule is enforced by the build, not by memory: the package sets
`noEngineReferences`, so Unity refuses to compile `Evosim.Core` if anything in it reaches for
`UnityEngine`. It is also why the JSON layer is hand-written rather than taken from a library.

Add `-Filter` to run one class: `./scripts/core-test.ps1 -Filter MutationTests`, and
`-ShowOutput` to see what the tests print — several of them report measurements rather than
just passing.

### 5. Run the Milestone 1 smoke test

Builds twelve creatures, checks their geometry against the phenotype, actuates them, and
asserts two conservation laws:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
$log   = Join-Path $env:TEMP 'evosim-smoke.log'

Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    '-projectPath', (Join-Path $PWD 'unity'), '-batchmode', '-quit', '-nographics',
    '-executeMethod', 'Evosim.Sim.EditorTools.Milestone1Smoke.Run', '-logFile', $log)

Get-Content $log | Select-String 'Milestone 1 smoke' -Context 0,140
```

**Never run this while the Editor has the project open** — two Unity processes sharing one
`Library/` corrupt it. A cold run is several minutes of silence either way, so tail the log
in a second terminal (`Get-Content $log -Wait -Tail 5`) rather than guessing.

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

**Revised.** The original plan built a search engine — quality-diversity archive, island
model — and treated currents and predators as a final sandbox to play in. That got inverted:
currents and feeding *are* the mechanism, and search is something the world does by itself.
Milestones 0–2 survived unchanged, because they are physics, and physics does not care how
selection happens.

| # | Milestone | Ends with | |
|---|---|---|---|
| — | Spike 01 — `ArticulationBody` at scale | Measured, six for six | ✅ |
| 0 | Unity project, assemblies, URP, physics config | Empty scene that builds headless | ✅ |
| 1 | Genome, development rules, phenotype builder | Spawn a random creature and watch it flop | ✅ |
| 2 | Physics harness: fixed stepping, config hash, fluid forces, anti-exploit checks, work accounting | Throughput in *simulated seconds per wall-clock second* | ✅ |
| 3 | Metabolism: per-part upkeep, neural cost, energy as a running balance | **A creature that starves** — the first thing here that can fail on its own | ✅ |
| 4 | World: current field, light and depth gradient, drifting nutrients | A creature that survives by drifting into food, and one that doesn't | ✅ |
| 5 | Life cycle: death returns tissue to the water, reproduction on surplus | **A population that persists without intervention** | ✅ |
| 6 | Perception: photosensors, evolvable colour, closed-loop brain graph | Directed foraging — a creature that moves *toward* something | ← partial: the loop is closed and four channels read; photosensors, colour, `Chemical`/`Energy`/`Flow` do not exist |
| 7 | Food web: predation, carrion, attack and defence | Trophic levels, or clear evidence of why not | ← partial: carrion and detritivory work, and a food chain has assembled twice; contact predation waits on shared space |
| 8 | Theatre: replay, gallery, charts, lineage, fluid validation harness | Showpiece and research instrument | |
| 9 | Land: contact, gravity | Deferred — the ecosystem is a water design | |

Milestones 2–5 completed out of the listed order — the ecosystem work of `DECISIONS.md`
D017–D050 is the real sequence, and the logbook is the day-by-day record of it.

**Milestone 3 is the pivot.** Everything before it is a simulator; everything after it is a
world. It is also the cheapest place to find out that the metabolism-to-photosynthesis ratio
is wrong, because at that point nothing eats yet and starvation is the only outcome.

The quality-diversity archive is not deleted, but it is **demoted**: it existed to solve a
problem that exogenous fitness creates, and under endogenous selection that job belongs to
ecological niches. It survives as an *observatory* — a record of what lived and what it
looked like — which is what makes a long run legible.

**Known gaps, recorded rather than hidden.** Reproduction is asexual, which leaves the
design's only recombination operator with no mechanism to fire; several energy-economy
numbers are still unmeasured (`DESIGN.md` §5A.10 is the honest list — some are now located
by sweeps, others remain placeholders); and the literature review had never covered
open-ended evolution or artificial ecosystems when §5A was written — review round 3
(logbook/0035) has since searched exactly that literature, and the campaign mechanisms
built after it carry their own ⚠ project-inference labels pending round 4.

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
