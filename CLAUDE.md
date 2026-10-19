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
| `CLAUDE.md` (this file, and the nested files its table names) | *What will bite you* |
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

**Where work stands is [`HANDOFF.md`](HANDOFF.md)**: the current round, what runs, what is queued and the machine's load ruling in force. This file does not restate it.

## Nested files, read when their trigger applies

This file holds what bites anywhere. What bites only in one area lives beside that area's code, in a `CLAUDE.md` that Claude Code loads by itself when a session opens a file in that folder. Two reference files under `logbook/specs/` hold the rest. Other agents read them as ordinary files. None of them is read at the start of a session: read one when its trigger applies, and not before.

| Read | When |
|---|---|
| [`src/Evosim.Farm/CLAUDE.md`](src/Evosim.Farm/CLAUDE.md) | Before launching, queuing, resuming or stopping a farm run; before touching checkpoints, `Evosim.Dynamics`, the manifest or its hashes; before comparing a number across the farm and the Editor |
| [`src/Evosim.Farm.Gpu/CLAUDE.md`](src/Evosim.Farm.Gpu/CLAUDE.md) | Before any work on the graphics card (ILGPU, `EVOSIM_GPU_*`, `-GpuTransport`) |
| [`src/Evosim.Core/CLAUDE.md`](src/Evosim.Core/CLAUDE.md) | Before changing a world rule, a tunable, the economy, the fields, the current, the brain or development |
| [`scripts/CLAUDE.md`](scripts/CLAUDE.md) | Before reading, scoring or watching a run, stopping one, or writing or calling a PowerShell script |
| [`unity/CLAUDE.md`](unity/CLAUDE.md) | Before running Unity in batch mode, refreshing a worker, running a Unity arm, or reading a run the Unity farm recorded (round 42 and earlier) |
| [`unity/Assets/Theatre/CLAUDE.md`](unity/Assets/Theatre/CLAUDE.md) | Before taking a picture, filming, replaying a run in the theatre or working on its interface |
| [`scripts/channel/CLAUDE.md`](scripts/channel/CLAUDE.md) | Before rendering the channel's title films in Blender |
| [`logbook/specs/build-readings.md`](logbook/specs/build-readings.md) | When reading a run recorded on an earlier build, or a column or field whose meaning may have changed |
| [`logbook/specs/machine-and-tools.md`](logbook/specs/machine-and-tools.md) | When the machine misbehaves (a freeze, a black screen, the fans), before a pace or timing comparison, and when a Claude Code tool asks or refuses unexpectedly |

A lesson that bites in one area goes in that area's file. One that bites anywhere goes here, in one to three lines, with its detail in the area's file. The record written before 2026-09-30 cites "CLAUDE.md's gotcha" on many things. Most of those passages now sit word for word in the files above, and a search for their words across `**/CLAUDE.md` and `logbook/specs/` finds them.

## Commands

Unity is at `C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe`.

**Use `Start-Process`, not `& $unity ...`.** Direct invocation silently fails — Unity exits
without writing a log. Check compile errors with
`Select-String -Path $env:TEMP\spike.log -Pattern 'error CS'`.

**Stop any run with `scripts/stop-arm.ps1`, never with `Stop-Process`.** It writes the manifest's ending before it kills a Unity run, and leaves a `STOP` file for a farm run. A bare kill leaves a manifest that reads `running` forever.

**Rounds run on the farm** (`scripts/run-farm.ps1`, `scripts/farm-queue.ps1`; `src/Evosim.Farm/CLAUDE.md`). The Unity farm has been idle since round 42 and serves the theatre.

**Read a run with the scripts, by named columns.** `analyse-arm.ps1` reads the report, `clade-score.ps1` scores the food chain, and `ledger.ps1` screens any income or cost knob in seconds before a day of machine time is spent on it. `scripts/CLAUDE.md` says how to read each.

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

`src/Evosim.Farm/` and `src/Evosim.Dynamics/` are the farm that has run every round since 2026-09-22. It is a .NET 8 console that steps Core's `World` with its own articulated-body solver, bit-identical at any thread count. Both are also Unity local packages, which the theatre's live mode steps inside the Editor as a labelled cousin. The farm's `CLAUDE.md` has the rest, and the planned list below predates it.

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

## Gotchas that bite anywhere

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
- **The Editor cannot replay a farm run.** Mono and .NET 8 round floats and doubles differently, so live play in the Editor is a cousin by construction. An identity claim about a farm run is made on the farm (`src/Evosim.Farm/CLAUDE.md`).
- **Adding a tunable, or bumping a genome or checkpoint format, refuses everything written before it.** Loading refuses rather than defaults, so the new build cannot read an older `config.json`, genome or checkpoint. The recorded test fixtures need re-recording (`src/Evosim.Core/CLAUDE.md`). Which build refused what is in `logbook/specs/build-readings.md`.
- **A rule chosen for being cheap gets re-asked when a later change makes it bite.** The report probably already counts it. When a world rule changes, list the rules chosen for cheapness under the old one and ask each whether it still holds.
- **A watch is one look that exits, and its schedule belongs to the session.** Never a `while true` shell loop or any other background loop. Such loops outlive the session and multiply with every re-arm; on 2026-09-20 they opened several hundred terminal windows and took the owner's desktop down. Run `scripts/sweep-orphans.ps1` at the start of every session and before arming anything, and `-Kill` what it lists. A one-off wait carries a deadline in its own condition. Stopping a background Bash task leaves its script running, so list processes by command line and stop them by id. `scripts/CLAUDE.md` has the watch script.
- **Every look at a running world checks its pace as well as its heat, and a world that slows is stopped and probed without waiting to be asked** (the owner, 2026-10-17). `python -I scripts/pace-check.py` flags any running world at under half its own pace of the hour before. A slowed world is a finding: stop it with `stop-arm.ps1`, resume it from its last checkpoint with the probes on, since a resume replays exactly, and tell the owner after. On 2026-10-17 a world of round 61's third pair ran slow for about an hour, most of it at a tenth of its pace, before anyone acted.
- **A world far slower than real time is not left to run for hours** (the owner, 2026-10-19, who stopped a five-world screen at 0.32x two hours in, and set about 2x as the aim rather than a hard rule). Fix the speed first, and stop a long run once it has answered its question.
- **Nothing private goes into a tracked file, and a push reads every commit** (the owner, 2026-10-18,
  after 17 unpushed days needed a privacy pass before they could go out). No address, user-profile path,
  home or routine detail, or other project's name is written into a tracked file, at the time it is
  written. `scripts/githooks/pre-push` checks each pushed commit against the ignored
  `scratch/machine/private-patterns.txt` and refuses an author that is not a GitHub noreply address.
- **Say how long it has been since the last push.** When it is more than three days, every status says
  so in its first lines, with the count of unpushed commits (the owner asked to be warned, 2026-10-18).
  A push is the owner's to approve each time; code and prose only, never data.
- **A pre-registration is committed before its round launches.** A farm round launches only from a clean tree after the entry's commit. Its manifest's `gitCommit` then names the pre-registration, and a `(DIRTY)` in the launch printout means it does not.
- **If a write or command prompts unexpectedly, stop and tell the owner.** Never switch tools to avoid a prompt. Change a permission setting or hook only when the owner asks for it in their own words.
- **Never `cd` or `Set-Location` into a worktree.** A write outside the session's working directory asks, so every write back to the main checkout would. Reach it from the project root with absolute paths, `git -C <worktree>` and Start-Process's `-WorkingDirectory`. Make one with `git worktree add scratch/wt-<name> -b <branch>`. A worktree is its own checkout, with its own hashes, `scratch/` and `runs/`.
- **Report the machine's load as `% Processor Utility`, as Task Manager does, not `% Processor Time`.** Read it before adding a job rather than counting threads. After a black screen or a reset, read the `nvlddmkm` events and Kernel-Power 41 before blaming a run.
- **At most five Unity processes, the owner's open Editor counted.** A sixth only for a short visual check that comes and goes, never for an arm and never beside a Core test suite.
- **From PowerShell, `bash` is WSL's and cannot see `D:/`**; a detached launcher names Git's `bash.exe` under `C:/Program Files/Git/bin/`. PowerShell scripts need a UTF-8 BOM, and an array parameter does not bind when a script is called with `-File` from bash (`scripts/CLAUDE.md`).
- **Watch a round in the theatre before writing it up, and say what was seen.** Thirty-three rounds were read on a report with nothing about horizontal position while every clade was a metre-wide column (logbook/0083). Read `cols` and `x sd` with `alive`.
- **Any work in DaVinci Resolve starts from `logbook/specs/story-resolve.md`, whatever the job.**
  The owner finishes the films in Resolve Studio 21.1, in the project Cambrian. The spec says how
  to connect: plain Python with external scripting set to Local, or the Resolve MCP tools in a
  Claude session. It also says which bins the project holds, and what the API does that its
  documentation does not say. Nothing outside the story skills pointed to it until 2026-09-28, so
  the channel intro's bin was made that day without it. Three rules hold for every job. A bin or
  a timeline is added beside the owner's, and theirs are never changed. An import links a file
  where it lies and copies nothing, so a moved or missing file shows offline; that place is under
  `assets/` (`assets/README.md`), never `scratch/` or a downloads folder. And whatever a
  session learns in Resolve goes into the spec with its date.

## Working with the owner

- **Every piece of prose follows [`STYLE.md`](STYLE.md)** — the logbook and primer serve
  agents *and* humans who want to read the research, and the record is meant to become a
  book. Run `python scripts/style-check.py <file>` before committing prose; keep the
  reader's key current (`logbook/README.md`).
- **The record paraphrases the owner; it does not quote them** (the owner's ruling,
  2026-10-03). Whatever is committed may be pushed and read by anyone, so a ruling goes in as
  what was decided, by whom and on which date: "the owner ruled on 2026-10-03 that the
  watchdog stays out of git", not the owner's sentence in quotation marks. When a ruling is
  subtle, show the owner the wording before committing it. One file is the exception: the nursery
  room's `rounds/nursery/activity.jsonl` is committed whole, the reasons the owner types on the
  room's screen included, and the screen says so beside each reason box (the owner's ruling of
  2026-10-05, `logbook/specs/nursery-room-tests-plan.md`). Never paraphrase or scrub its rows. Quotes written before this ruling
  are to be paraphrased in a cleanup that is queued in HANDOFF.
- **First-person voice is welcome in the record** — the owner invited genuine agent
  reactions, as the agent's own: honest and brief, never performative (logbook/0042's
  personal note is the precedent).
- **Pick a subagent's model by the work** (the owner's rule of 2026-09-24, amended 2026-10-01, put
  here on 2026-10-05 so every agent follows it): Sonnet for simple building and bulk reading, Opus for
  reasoning, reviews and anything visual, and Fable only for the hardest problems. A top-level agent hands work to the
  fitting subagent rather than doing all of it in its own model (the owner, 2026-10-19, to conserve tokens). On a build it plans and
  defines the work, a Sonnet subagent builds it and runs the first tests, and the top-level agent reviews the result.
- **A subagent cannot wait, so never hand it a task that ends in one.** It cannot sleep and
  cannot return before its report, so a suite or a smoke as its last step turns into a no-op
  shell command every few seconds until someone stops it; on 2026-09-09 that ran for an hour
  and the owner had to stop the session (logbook/0081). An agent's task ends when its edits
  and the fast filtered tests are in; the caller launches anything long in the background,
  where a completion notice costs nothing, and reads the result.
- **A subagent's default temporary location is its session scratchpad, which is outside the
  project.** A file there is invisible to the next agent and to the owner, and any other path
  outside the repository stalls the agent on the owner's guard hook. Every
  subagent brief therefore names the absolute path under `scratch/` it may write to
  (`D:\Projects\experiments\evolution-simulator\scratch\<task>\`) and forbids the session
  scratchpad and TEMP by name; "write under scratch/" alone was not enough on 2026-09-12. It also names a home under `scratch/` for
  the agent's own helper scripts and lists: a brief that allowed edits to named files and forbade every
  other place sent two agents' helpers to their scratchpad on 2026-10-04.
  The same rule binds the calling agent: nothing of the project's is written outside the
  repository (Conventions, below).
- **A subagent that a skill launches is briefed from the skill's files alone** (the owner
  ruled on 2026-09-26 that any new session should be able to get the same results). Every session that runs
  the skill must hand the subagent the same words, so its prompt is the skill's fixed template with
  nothing filled in but paths and numbers, and everything else it needs is in a file the template names: its
  brief, and the working folder whose files hold the case at hand. Nothing from the session goes
  in: no account of what happened, no lines of an earlier draft, no ruling or its date, no numbers
  that are not in the folder's files. A brief states rules and examples and carries no history;
  how it came to be goes in the logbook or in a record file beside it. Round 49's writer was
  briefed with a rewrite that told the story of the draft it replaced, and a prompt that retold it
  again; a new session would have briefed it differently. **Nor is any verdict in such a flow the
  session's own** (the owner, the same day, said that a gate the story flow named and the session
  decided was not good enough). A step that judges is a tool's check, a subagent's answer saved as it came,
  or the owner's words saved as they wrote them; a note a subagent reads is one of those three and
  never the session's reading; the flow's state records every verdict, and every loop has a limit
  after which the owner rules. `scripts/story-flow.py` (`next`, `save`, `owner`) is the pattern. A
  reader that must know nothing of the project is an `Explore` agent, which is given no CLAUDE.md,
  memory or git status; a `general-purpose` agent is given all three, and they change by the day.
- **Sample the pictures while a round runs, not only when it is written up** (owner,
  2026-09-12 evening, who said that some things cannot be evaluated without
  seeing them). The theatre-watch rule above covers the entry; this one covers the run. Take
  a frame or two from a live arm every few thousand simulated seconds (`theatre-snap.ps1`
  on a worker the queue is not using, one frame at a time on a loaded machine) and look at
  them, and say in the status what was seen. Round 37's crust at the glass was in the table
  for hours before anyone read it as a crust.
- **Check every test render against a reference before the owner sees it** (the owner,
  2026-09-25, asked why the agent was not picking these problems up itself from a PNG sheet). The
  first window-route clips of round 48 went to the owner with bodies popping in size every
  growth step and the picture too bright, and the second with the scenery in question, each
  found by the owner and not by the agent, who had looked at the contact sheets only for the
  fault last reported. The check: a side-by-side sheet of the new frames and the reference
  (the last film the owner accepted, at the same run and second where there is one), read for
  size, brightness, the bed, the reef, the caustics and the shafts, the bodies' shapes and
  motion between consecutive frames, and the captions; a number for what a number can say
  (mean luma, the largest frame-to-frame jump against its neighbours); and what was checked
  said in the same message as the clip's path. A change to a render path is checked against
  the path it replaces before anything else is judged.
- **Say when a ruling blocks the work, and keep working on the rest** (owner, 2026-09-22
  evening, who wants to be told explicitly when the agent is blocked by a
  decision it needs from them, and wants it working all the time). A status that lists open
  rulings among other news reads as information; the owner cannot tell the campaign has
  stopped on them. When the next step needs a ruling, lead with a line of the form "I am
  blocked on <decision> from you", and then put the decision in full, never in shorthand
  (the owner, ten minutes later, asked for the full topic with recommendations and
  implications): the topic in plain words for someone who has not read the proposal, the
  recommendation and why, what each option implies for the record, the rounds, the machine
  and the risk, and the exact question to answer. A pointer to a proposal file is not a
  request for a ruling. A decision still open is set out in full again in every message that
  asks for it, and each option carries its own implications. "Unchanged from my last message"
  is shorthand too (the owner, 2026-09-25 afternoon, asked whether the agent knew how they prefer
  to get decision topics). Then say what is being worked on meanwhile. Never end a turn idle while anything not gated
  remains: loose ends, instruments, measurements, the round-gap changes that are a new
  realisation of every seed and land best while no arm runs, pre-registration drafts and
  ledger screens for the round that waits.
- **Cap the load a build puts on the machine** (owner, 2026-09-23 afternoon, on seeing the
  CPU at 100%, asked whether farms and unit tests were running at the same time). A subagent's build of
  a tunable ran two farm runs at six threads each beside the test suites, because its brief
  capped nothing. A farm run's result is bit-identical at any load, so nothing was wrong;
  what a loaded machine breaks is pace against a wall limit, every timing reading taken
  meanwhile (a cost measured beside a test suite is not a measurement), and the owner's own
  use of the machine. The rule, in every brief: one farm run at a time beside a test suite
  and never two farm runs beside tests; a subagent's work takes at most half the machine's
  logical processors while the owner is at it; and a timing read (a wall split, a pace
  number, a kernel time) is taken with nothing else running, or it is re-taken.
- **Load is no bar; heat and headroom are, and the main agent shares the machine** (the owner,
  2026-10-09). How much of the machine the work uses does not matter in itself, as long as it does not
  overheat (the heat watch, `logbook/specs/machine-and-tools.md`) and keeps a little headroom for other
  work. The coordinator, or any main agent, shares the machine between the projects on it as their work
  needs, through the queue in `scratch/machine/queue.md`.
- **A round's checkpoints are thinned only after the owner approves its video as final**, and each thinning's keep list and freed space are shown and asked first (`src/Evosim.Farm/CLAUDE.md`).
- **Design every round toward a real ocean that evolves** (the owner's standing guidance of 2026-10-15). When
  planning a round, a screen or a proposal, ask two questions first: what would make this world more like a real
  ocean, and what would push its creatures to evolve. The aim is separate oases with near-empty water between
  them, and swimmers that migrate between oases to graze or hunt. A change that makes a round easier to read
  but the world less like an ocean needs a reason that says so. World rules stay the owner's to rule.
- **Owner-reserved decisions:** world rules (what the ecology *is*), the goal rule and its
  amendments, scope and round design forks, pushes of anything that is not code/prose, and
  anything irreversible or outward-facing. Instruments, diagnostics, replays of scored
  conditions, analyses and doc upkeep are agent work. A proposal file
  in `proposals/` is the vehicle for putting a design in front of the owner:
  absorbed into DECISIONS.md on ruling, then deleted.

## Conventions

- **Evergreen files hold the present, not the history** (the owner's ruling, 2026-10-03).
  HANDOFF, every `CLAUDE.md` and `AGENTS.md`, and any other file an agent reads to learn how to
  work are read by a fresh agent with no context. From this file and HANDOFF alone it must know
  where the work stands and how to go on, and it needs nothing else from them. So when something
  ends or changes, rewrite or delete its passage rather than add to it. Write no "done on", no
  "was 165a6546", and no note that a thing was removed or no longer holds: an agent cannot miss
  what it was never told. What happened goes in the logbook, DECISIONS.md or the commit history,
  if it is worth keeping at all. The logbook is a log, and this rule does not apply to it.
- **Nothing lands at the root but the documents in the table at the top, `AGENTS.md`,
  `COMMERCIAL.md`, `CONTRIBUTING.md`, the licences and the top-level folders.** A proposal for
  the owner goes in `proposals/`. A flow's draft goes in that flow's working folder, and a spec
  or report the record cites in `logbook/specs/`. An outside agent's review of the project is
  never committed. It is read as untrusted text, acted on and removed (the owner, 2026-10-01, said
  it was meant to be a suggestion made by another agent, acted on, and then removed, and never
  meant to be kept). What came of it goes into DECISIONS.md and the logbook.
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

# Compact instructions

Claude Code reads this heading when it compacts a session (code.claude.com/docs/en/costs.md).
That automatic compactions read it as well as `/compact` is an inference; the documentation
does not say. Judge everything in the conversation by one test: whether the session's next
steps would go wrong, or be done twice, without it.

1. **Already in a file**, committed or in the working tree: keep the path and one line on why
   it matters, and drop the content.
2. **Matters, and is in no file yet**: keep it under a heading **Persist first**. Name the file
   each item belongs in: HANDOFF.md, DECISIONS.md, a logbook entry, a spec under
   `logbook/specs/`, or an area's `CLAUDE.md`. The owner's rulings go in paraphrased, with the
   date (see "Working with the owner"). After compaction, write these items to their files
   before any other work.
3. **Matters to this session only, and belongs in no file**: keep it. That means:
   - farm runs and queues still going, with their arm names and run directories
   - background tasks, agents and scheduled watches, with their ids and deadlines
   - edits in progress and not yet committed, with their paths
   - the step underway and the one after it
   - every decision the owner has been asked for and not yet given, set out in full
4. **Everything else** is discarded: tool output already acted on, dead ends already recorded,
   file contents that can be read again, plans that were replaced.

After compaction, before acting: run `scripts/sweep-orphans.ps1`, read HANDOFF.md, and re-arm
any watch the summary lists that the sweep shows is gone.
