# Cambrian

**An evolving aquatic ecosystem: bodies and brains encoded together, selected by whether
they can afford to stay alive.**

Creatures are grown from a recursive directed graph that encodes *both* morphology and
neural control. They are dropped into simulated water where **every joule is accounted for**:
they earn energy from sunlight, drifting nutrients or each other, and spend it on tissue, on
thinking and on every movement they make. Run out and you die. Build up a surplus and you
reproduce.

**Inside a world there is no fitness function.** No creature in it is scored, and nothing is
being taught to swim. If swimming is what keeps a creature alive it will appear on its own. If
something else appears instead, that is the more interesting answer. A creature made only of
rigid feeding cells is a plant; one that eats plants is a herbivore; the word "species" appears
nowhere in the code. The one place a score is used is the nursery, a small tank that breeds
starting creatures on how much food they eat. Some of its best are released into a world,
where the score no longer applies.

The name is the ambition: the Cambrian explosion was a rapid diversification of **body
plans**, which is what an open-ended ecosystem is being built to produce.

The ultimate goal is complex creatures that react to the world through senses and a brain:
they graze, hunt each other, evolve ways to protect themselves, and live out their lives. We
are still very far from that.

Built by Aviv Shaked working with Claude (Anthropic's model) as coding and research partner.
Almost every commit is co-authored with it, and [`CLAUDE.md`](CLAUDE.md) is the working
agreement its sessions follow.

## Where it stands (November 2026)

### What runs

Worlds of a 300 m stretch of simulated ocean, with islands whose plants bloom and fade, and a
current. A world holds up to about 13,000 living creatures at once. Each creature grows from
its genome, moves under its own evolved brain, eats plants or other animals with a mouth, and
reproduces when it has energy to spare. The simulation is a C#/.NET program with its own
physics engine. Its runs are deterministic: the same seed and settings give the same world,
bit for bit, on the same build. Three worlds share one graphics card at two to two and a half
times real time. A Unity viewer films them, and the project's films are on
[its YouTube channel](https://www.youtube.com/@cambrian-sa).

### What has been measured

- Each world keeps an energy account. In one 24,000-second world, with about 82,000 births, it
  was out by 0.0001 J at the end.
- A food chain forms: plants, and animals that get over 80 % of the energy they gain by biting
  from leaves.
- Senses are used: with the mouth's sense of where food lies switched off, the nursery's best
  foragers ate about a fifth less.
- Every experiment's predictions are committed before it runs, and its results are read
  against them.
- Over 2,600 automated tests cover the core, the physics and the simulation runner.

### What doesn't work yet

- Bodies are still simple: the nursery's best foragers are all the same two-part body, and the
  variety of body plans the name refers to has not appeared.
- Populations can crash: in one of the two most recent worlds the grazers fell to about a dozen
  before recovering.
- Reproduction is asexual, so there is no recombination.
- Some of a world's founding creatures are bred in the scored nursery.
- Speed limits every experiment: a 24,000-second world takes about three hours.

The detail is in the [logbook](logbook/) (dated entries), [`DECISIONS.md`](DECISIONS.md) (why
each choice was made) and the [primer](primer/) (how the mechanisms work).

## Run it

Needs Windows, PowerShell 7, the .NET 8 SDK and Python 3. No Unity and no graphics card.

```powershell
git clone https://github.com/avivshaked/cambrian.git
cd cambrian
dotnet build src/Evosim.Farm/Evosim.Farm.csproj -c Release
./scripts/run-farm.ps1 demo -Seed 6202 -Seconds 600 -Launcher rounds/env-r66-m.ps1
```

The build makes the simulation. `run-farm.ps1` starts the most recent world design on the
processor, for ten simulated minutes, and returns once the world is running. The run is written
to `runs/demo/`: one row for every creature born and every death, samples of the world, and the
genomes of the living. Its report, `runs/demo.md`, shows how far it has got, and its last lines
say **Ended** when it has finished. Then

```powershell
python scripts/what-evolved.py runs/demo
```

reports who is alive, which lineages lead and what has changed since the founders.

```powershell
dotnet test src/Evosim.Core.Tests --filter "Category!=Slow"
```

runs the core's 1,939 tests (over 2,600 across the core, the physics and the runner) in about
seven minutes.

## How it works

### The genome is a graph

A creature grows from a recursive directed graph, the encoding Karl Sims used for his 1994
*Evolving Virtual Creatures*. A cycle in the graph can unfold into a chain of segments, and a
reflection flag can make a body symmetric in one mutation. Neurons live inside body nodes, so
when development copies a segment it copies that segment's controller too.

### Energy does the selecting

Each part is made of a tissue that earns and costs energy in its own way. Photosynthetic tissue earns from light, a mouth earns from what it bites, and a link,
the only tissue that can carry a joint, is the most expensive to keep. A creature that cannot
cover its costs dies, and one with a surplus reproduces. A plant is a body without links that
earns from light, and nothing in the code defines one. Sims scored his creatures and bred the
winners. Here the world does the selecting.

### The nursery is the one scored search

Genomes swim in a small tank for half an hour of simulated time and are scored on the food they
eat. The best are kept and the rest bred by tournament selection. Its best genomes join random
ones as a world's founders.

### Reading and building both corrected the design

The specification was written first and a literature review run against it. The review added a
failure mode the first draft had missed. A change of body breaks the brain adapted to the old
one, so a new body tends to lose before its brain catches up. It also showed that a crude fluid model collapses the variety of body
plans, and that a feared rewrite of the genome's encoding was unnecessary for rigid bodies.
Building it then corrected the specification in ways no reading could have. Reflection about
the wrong axis put mirrored copies on top of their originals, and joint torque applied without
a reaction on the parent let creatures make angular momentum from nothing. A person watching
the screen found the second after every automated check had passed.

## The film pipeline

Worlds are hard to read from tables, so the project also turns a run into a short narrated
film. The pipeline is a Claude Code skill
([`.claude/skills/create-story-video/`](.claude/skills/create-story-video/)). It runs each step
in its own subagent, and the steps hand work on only as files:

- One agent gathers the facts from the run's records.
- A writer drafts the story and the narration from those facts alone. A fact checker, a cold
  reader and a comparer each judge the draft.
- A text-to-speech service reads the script, and a shot list is timed to the narration.
- The simulation records the stretches to film, and the Unity viewer films them. Blender draws
  the graphics, and DaVinci Resolve assembles the edit.

A verdict comes only from a tool, a subagent's answer saved as it came, or a person's decision.
Each role runs on the cheapest model that can do it, and each step has a time budget. The
first four steps have run on a real round and the narration has run once. The shot list,
filming, graphics and edit are built and tested but have not yet run end to end. The pipeline
needs software outside this repository (Claude Code, a text-to-speech service, Blender,
DaVinci Resolve Studio and Unity), so it is here to read rather than to run.

## Where to look

| Document | What it holds |
|---|---|
| [`DESIGN.md`](DESIGN.md) | The specification. Its founding sections cite peer-reviewed papers to the page: 121 citations across 37 of its 96 sections, where `[K12 §2.3, p.7]` names one page of one paper (§13 says how to trace one) |
| [`DECISIONS.md`](DECISIONS.md) | Every design decision, why it was made and what it replaced: 305 so far. Most rest on experiments; 12 cite a paper |
| [`logbook/`](logbook/) | Dated entries on what was tried and what broke |
| [`primer/`](primer/) | Explanations of the mechanisms, written for a reader |
| [`research/`](research/) | The literature review. The papers are not committed; `FETCH-RESULTS.md` gives each one's source |
| [`CLAUDE.md`](CLAUDE.md) | The working agreement for the agents that build the project |
| [`unity/README.md`](unity/README.md) | Installing Unity, the viewer, and the first physics tests |

| Code | What it is |
|---|---|
| [`src/Evosim.Core/`](src/Evosim.Core/) | The genome, development, cell types, mutation and the world's rules. No Unity: it builds and tests on its own |
| [`src/Evosim.Dynamics/`](src/Evosim.Dynamics/) | The physics solver: articulated bodies in water, bit for bit at any thread count |
| [`src/Evosim.Farm/`](src/Evosim.Farm/) | The program that runs a world, records it and resumes it from checkpoints |
| [`src/Evosim.Farm.Gpu/`](src/Evosim.Farm.Gpu/) | The same world on an NVIDIA graphics card, through ILGPU |
| [`src/Evosim.Nursery/`](src/Evosim.Nursery/), [`src/Evosim.NurseryRoom/`](src/Evosim.NurseryRoom/) | The scored nursery, and its room: one service with screens, a JSON API and an MCP endpoint, so that people and Claude sessions run it through the same operations |
| [`unity/`](unity/) | The Unity project; the viewer is `unity/Assets/Theatre/` |
| [`scripts/`](scripts/) | Launch, read and check runs (PowerShell and Python) |
| [`rounds/`](rounds/) | Each experiment's settings and launcher, kept so that it can be rerun |
| [`.claude/skills/create-story-video/`](.claude/skills/create-story-video/) | The film pipeline |
| the rest of `src/` | Test projects, two benchmarks, and eight small tools and one-off experiments |

The simulation has its own physics solver and one third-party library, ILGPU, for its
graphics-card version. The tests use xUnit, the nursery room the official MCP SDK, and the
viewer needs Unity 6.

## Licence

Both licences are non-commercial. The code and the genome files under `inocula/` are under
PolyForm Noncommercial 1.0.0 ([`LICENSE`](LICENSE)), and the written work is under CC BY-NC 4.0
([`LICENSE-DOCS`](LICENSE-DOCS)). [`COMMERCIAL.md`](COMMERCIAL.md) says what counts as
commercial use and how to ask for a licence, and [`CONTRIBUTING.md`](CONTRIBUTING.md) what a
contribution grants. Versions published before 2026-09-07 were MIT and CC BY 4.0, and those
grants stand for the copies already made.

Quotations from papers are used under fair dealing and attributed where they are used. The
papers themselves are not distributed here.
