# The theatre, first cut — build report

Written by the implementing agent for `logbook/specs/theatre-spec.md` (D075 items 1–4), 2026-09-05.
Everything below was run on this machine; nothing is claimed that was not.

## Commits (on `main`, not pushed)

| Commit | What |
|---|---|
| `1392ccd` | Snapshot rows carry the creature id: `GenomeJson` format 4, the snapshot writer, three round-trip tests, `SnapshotJoinTests`, a note in DESIGN.md §9 |
| `0fa6568` | The theatre: `Evosim.Theatre` + `Evosim.Theatre.Editor`, the generated scene, and CLAUDE.md's entry for it |

Nothing else in the working tree was touched. The two `sol-gpt-*-review.md` files at the
repository root were already untracked when I started and are not mine.

## How to open it

1. **The scene is generated, never hand-edited**, like the sandbox:
   `Evosim/Rebuild Theatre Scene` (or `-executeMethod
   Evosim.Theatre.EditorTools.TheatreSceneBuilder.Run`). It writes
   `unity/Assets/Scenes/Theatre.unity` — a URP camera with `TheatreCamera`, a directional light,
   and one `Theatre Runner` object carrying `TheatreRunner` and `WaterBounds`. The committed
   scene was generated this way on worker 6 and copied into `unity/` with its `.meta`; every new
   script carries a hand-written `.meta` with a fixed GUID, so the main project and every worker
   agree on what the scene points at.
2. **Open the scene, fill in `Run Directory`, press Play.** A run directory
   (`runs/th-ref/2026-09-05-143009-6b0097f7`) or the arm directory above it (`runs/th-ref`, whose
   newest run is taken).

### Inspector fields on `Theatre Runner`

| Field | What it does |
|---|---|
| `Mode` | `World` (Mode B, a recorded run) or `Solo` (Mode A, one creature) |
| `Run Directory` | Mode B's run; in Mode A it supplies the water (config and physics step) |
| `Allow Source Mismatch` | Play a run this build did not record, with the banner |
| `Seek To Seconds` | Run unpaced with the camera off to this simulated second, then render. `K` re-triggers it |
| `Genome Path`, `Genome Row`, `Genome Id` | Mode A's genome: a `snapshots/*.jsonl` file, by row index or by creature id (`-1` uses the row) |
| `Solo Depth Metres`, `Solo Seed`, `Solo Smell Density` | Where the creature sits, the seed of the field it smells, and how much detritus to lay in front of its nose (0 = the world's initial field, which is empty in a reference world) |
| `Test Sine`, `Starve` | Mode A's two toggles, also `T` and `G` |
| `Paused`, `Rate`, `Frame Budget Seconds`, `Seek Budget Seconds` | Pace. `Rate` is simulated seconds per wall second; the budgets cap how much of a frame stepping may take |
| `Colour By Cell Type`, the three colours, `Starving Brightness`, `Repaints Per Frame` | The view mode (§5A.5 keeps real colour as an evolvable trait, so this is an instrument and can be turned off) |
| `Show Overlay`, `Fly Camera`, `Water`, `Water Extent Metres` | The HUD, and the two scene objects the runner drives |

### Environment variables

`EVOSIM_THEATRE_RUN` (run directory), `EVOSIM_THEATRE_GENOME` (also switches to Mode A),
`EVOSIM_THEATRE_SEEK`, `EVOSIM_THEATRE_OVERRIDE=1` (allow a source mismatch), and
`EVOSIM_THEATRE_SECONDS` (the two headless checks only).

### Keys

`Space` pause · `[` `]` halve/double the pace · `K` seek to `Seek To Seconds` · `C` colour mode ·
`F` follow/free · `Esc` deselect · `R` reload · `H` hide the overlay · left-click a creature to
select and follow it. Camera: `WASD` + `Q`/`E`, right-drag to look, wheel for speed (0.5–800 m/s),
`Shift` to boost.

## Mode B's identity check — what it compares, and what it said

At every sample time in the run's own `stats.jsonl`, the live world is compared against the
recorded row on five columns, **exactly** (`Json.Writer` writes doubles with "R", the shortest
round-tripping string, so a stored double and a live one are comparable to the last bit):

- `alive`, `births`, `deaths` — the population's whole history in three integers;
- `auditResidual` — a double accumulated over millions of steps, the strictest available test of
  bit-identity;
- `meanHeight` — the only positional quantity a stats row carries, computed the way
  `EvolutionRun.Row` computes it (a `double` sum of `HeightY` over `World.Living`, in order).

The first difference is reported with the column named, in red, and the HUD keeps it. The theatre
writes nothing into the run directory.

**On `th-ref`** — 1,000 simulated seconds, dt 0.01, seed 1, the round-24 world exactly as
`rounds/launch-r24.ps1` sets it, recorded on worker 6 with `scripts/run-arm.ps1`
(`runs/th-ref/2026-09-05-143009-6b0097f7`, config hash `6b0097f75e13e5db`, simHash
`382c48b8f676759a…`, coreHash `e96df9722deab355…`; 32 births, 44 alive at the end, 35.3× real
time when recorded):

```
source: identical to the recording
10 recorded samples, last at t=1000 s; replaying to t=1000 s
identity check: identical on all 10 samples compared
  10 matched, 0 skipped, of 10 recorded
  replayed 1000 s in 0.47 min (35.6x real time), 44 alive at the end
```

**Refusal and override, both exercised.** Against an earlier build (the theatre had gained one
file since the recording), the same run was refused:

```
[Theatre] refused: This build is not the build that recorded the run, so what it produced would
not be that run: simHash d93887d07568… recorded, 6a8fcb023e12… here.
```

and under `EVOSIM_THEATRE_OVERRIDE=1` it played and still matched 10 of 10 — which is worth
recording as a fact about the coupling rather than about the run: the only difference was the
theatre's own source, and the theatre does not touch the simulation. `coreHash` and `simHash` are
reported separately for exactly this reason.

## The snapshot id round trip

`GenomeJson.FormatVersion` is 4. A snapshot row now begins
`{"id":17,"format":4,"root":0,…}` — the id first, written by `EvolutionRun.Snapshot` through
`GenomeJson.Write(genome, indent: false, id: creature.Id)` rather than by
`JsonlWriter.WriteGenome`, because a genome is a recipe shared by every creature that develops it.

- Three unit tests: the id is the first field and survives a round trip; a row without one reads
  `GenomeJson.NoId` and contains no `"id"` key; a format-3 row is refused with both versions named.
- `SnapshotJoinTests.SnapshotIdsJoinLineage` asks the join itself of every run under `runs/` that
  carries both a lineage and a format-4 snapshot: every id present in a birth row, born at or
  before the snapshot's time, not dead before it. **352 snapshot ids from 4 snapshots across 4
  runs joined `lineage.jsonl`, none failing.**
- Full suite: **521 tests, 0 failures** (517 before this work).

**Consequence to know about:** every stored format-3 genome is refused by this build — the
inocula in `inocula/` (`inoculum-r13a-s2-t17000.json`, `s4-*.json`) and anything `ledger.ps1
-Genome` is pointed at. The genome fields did not change across the bump, so re-extracting an
inoculum from a new snapshot brings it forward; I did not edit those files, since they are inputs
to owner-designed assays. Noted in CLAUDE.md beside the matching `config.json` gotcha.

## What Mode A does and does not do

Does: reads a genome from a snapshot row (by index or by creature id) or from a standalone file,
develops it with the run's own limits and shape registry, builds it with `PhenotypeBuilder`, wires
`Brain.For` + `CreatureSensors` + `EffectorDriver` the way `Ecosystem.Build` does, and steps it in
a `FluidEnvironment` built from the run's config — the same drag, added mass and current field.
The HUD shows speed, distance travelled, depth, mean joint rate, the reserve, and all seven sensor
channels read through `ISensorField.Read`. `T` swaps the brain for the test sine; `G` lets the
reserve run down.

Does not: feed, breed, bill or kill — there is no economy, so nothing seen in Mode A is evidence
about whether the animal could survive. It cannot choose a patch: `CreatureInstance.Patch` is
`internal set` to `Evosim.Sim` (Ecosystem owns it and refreshes it as dispersal and advection move
a creature), so the creature swims in patch 0's water; widening that seam for a viewer was not
worth changing the simulation for. And **the detritus it smells is not the run's**: a run stores
aggregates and genomes, never a field, so the nutrient field comes from a `World` that is
constructed and never stepped — the world's *initial* field, which in a reference world is empty.
`Solo Smell Density` lets a viewer put something in front of the nose, and the overlay says
plainly when it has been used.

The wiring is a deliberate ~50-line duplicate of `Ecosystem.Build`, not a refactor of it: the
perception build is in that file and replay identity is measured there.

## Every validation I ran

| # | Check | Result |
|---|---|---|
| 1 | `./scripts/core-test.ps1` | 521 passed, 0 failed, 1 m 37 s |
| 1 | Milestone 1 smoke (worker 6) | **PASS** — 12 creatures built, geometry verified, actuated, torn down; all 7 channels in `SensorChannels.Implemented` vary. Unchanged |
| 2 | Mode B headless on `th-ref` (`TheatreIdentityCheck`) | source identical to the recording; **10 of 10 samples identical**, 0 skipped; 1,000 s in 0.47 min (35.6× real time) |
| 3 | Mode B refuses a mismatched build | refused, naming the hash that differs and which side is which; exit code 1 |
| 3 | Mode B under the override | played, 10 of 10 matched, exit 0 |
| 4 | Snapshot ids join lineage | 352 ids, 4 snapshots, 4 runs, none failing |
| 5 | Mode A headless (`TheatreSoloCheck`) | creature 73 from `snapshots/000001000.jsonl` (2 parts, 1 DOF): under its own brain **0.6745 m** travelled and 0.018 rad/s mean joint rate; under the test sine **1.5983 m** and 1.675 rad/s, over 30 s each. All seven channels finite |
| 6 | Wall-clock, headless | recording 35.3×, replay 34.4–35.6× real time on the same 44-body world |

Sensor readings from the Mode A check, at every part over 30 s (`min .. max`):

```
                       brain                     test sine
JointAngle             -1 .. 0                   -1 .. 1
JointAngularVelocity   -0.0462 .. 0              -0.3237 .. 0.3234
OrientationUp          0.8863 .. 1               0.9088 .. 1
Depth                  0.181 .. 0.2              0.1654 .. 0.2
Chemical               0.3333 (constant)         0.3333 (constant)
Energy                 0.3799 (constant)         0.3799 (constant)
Flow                   -0.1829 .. 0.0297         -0.5213 .. 0.5269
```

`Chemical` is constant because the check lays a uniform 5 J/m³ into every layer and the creature
barely changes depth; `Energy` is constant because Mode A has no economy and `Starve` was off.
Both are the mode behaving as designed, not a dead channel — the Milestone 1 smoke is what asserts
the channels vary in a world.

## What needs eyes — I could not run it

Batch mode has no display, so **everything rendered is unvalidated**. Specifically:

- **The scene as a picture.** That the camera, the light and the URP material resolve; that
  creatures are visible against the background; that `WaterBounds`' GL grid draws at all (URP
  invokes `OnRenderObject`, but I could not see it) and that `Hidden/Internal-Colored` resolves.
- **The overlay.** Layout, legibility, the red mismatch line, the colours.
- **Spec item 2's Play-mode half.** The headless check proves the loader, the world construction
  and the comparison in the same mode the recording was made in. It does **not** prove Play mode
  with a camera in the scene, which is a different loop around the same `Ecosystem.Step`. If Play
  mode ever differs, the HUD is exactly the instrument that will say so — open `th-ref` and watch
  the identity line.
- **The seek.** Its stepping loop is the same `replay.Step` call the headless check makes
  1,000,000 times; what is untested is the camera toggle and the "seeking, about N s to go" line.
- **Selection and follow.** `CreatureIdMap`'s pairing is verified at runtime against the height
  each body was built at, and it turns itself off and says so if that check fails — but no click
  has ever been made. This is the piece I would watch first.
- **The colours and the reserve tint**, including whether `RepaintsPerFrame = 96` keeps up
  pleasantly in a large world.
- **Spec item 6's wall-clock figures.** They ask for the pace at 1× and at seek **on a 4,000-body
  world, with rendering**. I have neither a display nor a 4,000-body recording made by this build;
  `th-ref` holds 44 creatures. The headless numbers above are what I can honestly offer.
- **Spec item 5's "the sine toggle visibly changes the gait".** The numbers differ by 2.4× in
  distance and 90× in joint rate; whether it *looks* different is a question for the scene.

**No screenshots.** `-batchmode -nographics` cannot take one, and I had no display; nothing was
saved under `scratch/` for that reason. (Nothing at all was written outside the repository: the
Unity logs are in `scratch/logs/`, the small edit scripts I used are in `scratch/`.)

## Where I departed from the spec, and why

1. **Patch boundaries are not drawn.** The spec asks the water volume to show "the surface at
   y=0, the floor at the run's depth, the patch boundaries". The first two are drawn. The third
   cannot be: D061's patches are an index carried on each creature and each field cell, not a
   region of space — a creature's patch and its lattice tile are unrelated, and two creatures side
   by side on screen may be in different patches. Drawing lines between them would be inventing
   geometry the simulation does not have. The selected creature's patch is in the overlay instead.
   For the same reason the grid spans the *lattice* (creatures are tiled 100 m apart, §6.3), not
   `WorldAreaSquareMetres`, which is 100 m² — the aperture the sun shines through, not the room
   the bodies are in.
2. **Mode A cannot choose a patch** — `CreatureInstance.Patch` is `internal set`; see above.
3. **The seek suppresses rendering by disabling the camera**, not by disabling every renderer as
   the spec suggests: forty thousand renderer toggles per seek cost more than the frames they
   save, and a disabled camera is the same "no camera work".
4. **The identity check reads `stats.jsonl`**, which the survey named as the per-sample record,
   rather than parsing the markdown table. Same numbers, no column-position risk.
5. **One `TheatreRunner` with a `Mode` field** serves both modes, rather than two components: the
   spec asks the scene to hold one runner, and the two modes share the pace, the camera, the
   palette and the overlay.
6. **A fifth validation entry point exists that the spec did not ask for** —
   `Evosim/Theatre — check one creature headlessly` (`TheatreSoloCheck`) — because Mode A had no
   other way to be tested at all without a display.

## One thing the lead should decide

**The theatre lives inside the tree `simHash` covers.** The spec places it at
`unity/Assets/Evosim/Theatre/`, and `simHash` is a digest of every `.cs` under `Assets/Evosim`.
So *editing a HUD label refuses every run recorded before that edit*, and every worker refreshed
after a theatre change reports a new `simHash` to the farm's own identity record even though the
simulation did not move. I followed the spec and made the refusal message name which of the two
hashes differs, so a `coreHash` difference (certainly a different world) reads differently from a
`simHash`-only one (possibly just a viewer) — and I recorded `th-ref` four times during this
build for exactly this reason. Moving the theatre to a sibling folder outside `Assets/Evosim`
would end the coupling; it is a one-line change to two paths plus a rebuild of the scene, and it
is the lead's call because it changes what `simHash` means. It is written up in CLAUDE.md's
gotchas as it stands.

## Incidental finding, not mine to fix

`EvolutionRun.Snapshot` is called both inside the loop (`metabolicSteps % (reportEvery * 10) == 0`)
and once more after it, and both write to the path derived from the same simulated second. When a
run ends exactly on a snapshot boundary — `th-ref` at t=1000 — the file gets the living population
twice: 88 rows for 44 creatures. `JsonlWriter` opens in append mode, so this is not new and not
caused by the id. Every row is valid and every id joins; a reader counting a population from a
snapshot would double it.

---

# Addendum — the two follow-ups (2026-09-05)

Both ruled by the coordinator after the first hand-back; same rules, worker 6 only, committed on
`main`, not pushed.

| Commit | What |
|---|---|
| `8a4179c` | The theatre moves to `unity/Assets/Theatre/`, out of the tree `simHash` covers |
| `7b39e7d` | A run ending on a snapshot boundary no longer writes its population twice |

## 1. The theatre is out of `simHash`

The twelve scripts, the two asmdefs and every `.meta` moved from `unity/Assets/Evosim/Theatre/`
to `unity/Assets/Theatre/`; the scene stayed under `Assets/Scenes/`. Git recorded all thirty
files as renames, so **the GUIDs survived** and `Theatre.unity`'s three script references are
untouched. Rebuilding the scene through `Evosim/Rebuild Theatre Scene` on worker 6 after the move
produced the same three objects referencing the same three script GUIDs, differing only in the
local fileIDs Unity assigns afresh on every rebuild — so the committed scene was kept rather than
churned for nothing.

### The hashes

Taken with `scripts/tree-hash.ps1`, a copy of `run-arm.ps1`'s `Get-SourceTreeHash`. CLAUDE.md's
warning about `scripts/simhash.py` applies to any second implementation, so it was validated
first: it reproduces `382c48b8f676759a…`, the `simHash` that `th-ref`'s own `run.json` recorded,
on both the main tree and worker 6 — and later agreed with `run-arm.ps1`'s printout on a third
tree (`995cda594d03655e…`).

```
before the move   Assets/Evosim, 30 .cs   382c48b8f676759a381b018a6ae603d9ab859e5ad40f769450a4aa6746ee9e78
after the move    Assets/Evosim, 18 .cs   ea3e5c18ddd019447790b7b579811f21ea2aa9fbce02b537f18543580383d4ab
```

**That `ea3e5c18…` is `Assets/Evosim` at 80fcedd plus the snapshot-id change**, shown two ways:

- `git diff --name-only 80fcedd HEAD -- unity/Assets/Evosim` lists `EvolutionRun.cs` and theatre
  files, and nothing else — no other simulation source has moved since 80fcedd (`fdc2ee8`, the
  commit in between, touched only DECISIONS.md, DESIGN.md and a proposal);
- deleting `Theatre/` from the *pre-move tree as it sat on disk* (worker 6's copy, the one that
  hashed to `382c48b8…`) gives `ea3e5c18…` exactly, with all 18 remaining files byte-identical to
  the post-move main tree.

A third route — reconstructing the tree from git blobs — gave a different number
(`73ab976f95a45456…`), and the reason is worth recording: **`simHash` is a property of a checkout,
not of a commit.** Three files in this working tree (`EffectorDriver.cs`, `EmbodiedRun.cs`,
`ThroughputSurvey.cs`) are CRLF on disk while everything beside them is LF, and `core.autocrlf=true`
hides that from `git diff` entirely. `git archive` applies the checkout conversion and `git show`
does not, so only a comparison against bytes on disk means anything. Now in CLAUDE.md's gotchas.

### The point, proved

Changing one HUD label (`"no creature selected"` → `"nothing selected"`):

```
simHash before the label change  ea3e5c18ddd01944...
simHash with the label changed   ea3e5c18ddd01944...   (unchanged)
simHash after reverting          ea3e5c18ddd01944...
theatre tree, label changed      3ead730f1c104632...
theatre tree, reverted           7370dcc798ef0c2c...   (the label did move something)
```

The label is reverted; `git diff` on that file is empty.

Verified on worker 6 after the move: compiles from the new location, the scene builder still runs
(`[Evosim] Theatre scene written to Assets/Scenes/Theatre.unity`), and the **Milestone 1 smoke is
unchanged** — PASS, 12 creatures built, geometry verified, actuated, torn down.

`BuildIdentity`'s class remarks said the opposite of what is now true and were rewritten; CLAUDE.md
gained the one-line reason the theatre lives beside `Assets/Evosim` rather than inside it, and its
gotcha about theatre edits was replaced by the two that are now true (the checkout/commit point
above, and "anything under `Assets/Evosim` is simulation source, whatever it does").

## 2. The double snapshot

`Snapshot` now refuses a second write at the same elapsed time — 21 lines, all inside that method
and the statics, nothing in the physics or economy path. Compared against the elapsed time rather
than against the file existing, because two metabolic steps half a second apart floor to the same
file name and a `File.Exists` guard would silently drop the final population of a run ending at
t=1000.5.

Measured by re-recording `th-ref` on the fixed build
(`runs/th-ref/2026-09-05-144601-6b0097f7`): the t=1000 snapshot holds **44 rows for 44 living
creatures**, against 88 before. Its `stats.jsonl` and `lineage.jsonl` are byte-identical to the
same seed's recording from before the change, so the world did not move.

It changes `simHash` for launches after it: `ea3e5c18ddd01944…` → `995cda594d03655e…`. Runs
recorded before it replay only under `Allow Source Mismatch`; a round held on one build should not
take this mid-flight.

## Re-validated after both changes

| Check | Result |
|---|---|
| `./scripts/core-test.ps1` | 521 passed, 0 failed |
| Milestone 1 smoke (worker 6, post-move) | PASS, unchanged |
| Theatre scene builder (worker 6, post-move) | writes the scene; same three script GUIDs |
| Mode B identity check on the new `th-ref` | **source identical to the recording; 10 of 10 samples identical**, 1,000 s in 0.47 min (35.4× real time) |
| Snapshot ids join lineage | 396 ids across 5 snapshots / 5 runs, none failing |

The reference recording is now `runs/th-ref/2026-09-05-144601-6b0097f7` (simHash
`995cda594d03655e…`, coreHash `e96df9722deab355…`, config `6b0097f75e13e5db`). The earlier
`th-ref` runs are left in place; each carries the simHash of the build that made it, which is what
the theatre's refusal path reads, so any of them is a ready-made test of that path.

Nothing in the "needs eyes" list above has changed: everything rendered is still unvalidated, and
there are still no screenshots.
