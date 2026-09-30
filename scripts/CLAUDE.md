# Reading, scoring and watching runs: what will bite you

Split out of the root [`CLAUDE.md`](../CLAUDE.md) on 2026-09-30 so that it is read only when work reaches this area. Read before reading, scoring or watching a run, stopping one, or writing or calling a PowerShell script.

The root file's rules still apply. The entries below were moved word for word, so their dates, their "until" clauses and any reference to "this file" or "CLAUDE.md" are as they were written in the root file.

## Commands

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

## Gotchas

- **A morphology share is contaminated by the population floor.** `MinimumPopulation` (40)
  trickles fresh generation-zero founders in whenever a world drops to it, and founders carry a
  joint about two times in five. So in a bottlenecked world the jointed *share* is largely a
  readout of the founder draw, and it sawtooths — five apparent "muscle recoveries" in one run,
  one reaching 59% at generation 28, were all the floor (logbook/0029). `FloorSpawnsPerStep` = 2
  prevents synchronous cohort *death* and does nothing about this. Read `jointedInherited` /
  `absorptiveInherited` — creatures whose parent had the trait — not the share; and treat
  `gen min = 0` as "founders present, share meaningless".
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
- **`stillb` and `mat orphan` read 0 in a healthy run, and the second is an invariant.** From
  the 2026-09-07 build the table carries the stillbirth total and the matter the ledger says
  is in bodies less what the living hold. A nonzero `mat orphan` means matter was charged to a
  body that does not exist, which is what happened for every stillbirth between D065 and
  2026-09-07 (none seen in the checks made on the scored worlds; DESIGN §0p).
- **A run writes at most 50 diverged dumps** (`Ecosystem.MaxDumps`), so `diverged/` equals the
  `diverged` column only below 50: `r36-s2` counted 55 and dumped 50, and its last five
  throws have no post-mortem (logbook/0092). Read the column for the count and the dumps
  for the anatomy, and say when the second is censored.
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
- **A body's angle is in the poses and, before D110, nowhere in the economy.** A part's lit
  area is a quarter of its surface (Cauchy's orientation average) on both sides of the light,
  so a leaf on edge earned what a flat one did through round 45, and nothing selected a pose;
  the owner saw the leaves standing on edge in the pictures before any instrument did
  (2026-09-23). `scripts/reads/tilt.py <run dir> <second>` reads each root part's tilt from
  `poses.jsonl` against the snapshot's genome (0 flat, 90 on edge; the mean flat factor is
  0.50 for random poses), and round 45's crowds read 90% at their birth rotation at 5,000 s
  and near random at the end (`logbook/specs/r45-read/tilt.txt`). A picture of a pose is a
  fact about the solver and not about selection until the economy reads the pose; D110's
  `light by exposure` is that reading and is off in every recorded config. Read `expo`
  (1 random, 2 flat) with `tilt.py` as its check from the poses once it exists.
- **`detritusOnFloor` is a joules total, not a density.** It is `FloorStock`, the refuge
  stock of patch 0 in joules, and the stomach screens of 2026-09-23 were first read as
  3.4 J/m³ on the bed with an R0 of 34 from it, which went to the owner before the units
  were checked; the true floor held 11 J of 79,744 J of snow and the eaters' larder is a
  thin layer under the plant crowd at about 0.5 J/m³ against a break-even of 0.44
  (`logbook/specs/stomach-screens.md`). A field total in the stats is joules or units over
  the whole bin; divide by the bin's live volume before calling it a density, and say which
  bin.
- **`stitch-resume.py` wrote plain gzip until `9503d4e`, and Core refuses it.** Record format 2's
  `genomes.jsonl.gz` and `positions.jsonl.gz` are members that carry their own length (an `EV` extra
  field, `GzipMembers`). Python's `gzip` reads them and writes none, so the first join (`r52-s3j`) was
  readable by every Python reader and refused by Core's (`the member at byte 0 is not a record
  member`). That meant the theatre, the nursery and any C# tool. The script now writes members, and
  `--fix-gz <run dir>` rewrote `r52-s3j` in place. A Python tool that writes a record file uses
  `MemberWriter` from that script, never `gzip.open(..., 'wt')`.
