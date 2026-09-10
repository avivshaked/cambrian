# Build report: a photosynthetic flag on lineage birth rows

*Builder's report against `logbook/specs/photo-flag-spec.md`, 2026-09-06. I committed nothing and
pushed nothing. Another session committed the four files as `95d2091` while I was writing
this up; the bytes on disk did not move, and every hash below still holds. Both runs are on
`unity-w7`. Workers 2, 3, 4 and 6 were carrying round 27 throughout and were still carrying
it at the end; I touched none of them.*

## What it does now

Every birth row in `lineage.jsonl` says whether the developed body has photosynthetic
tissue. The field is `"pho"`, written between `"jnt"` and `"pt"`, so a diff of a new row
against an old one is one inserted field. Death rows are unchanged.

The flag comes from the same local `World.Admit` already computed for
`Organism.HasPhotosyntheticTissue`, passed rather than recomputed. A row and the creature it
describes cannot disagree about what the body is made of.

D063's producer clause can now be read from a parent chain. `clade-score.ps1` printed
"flag absent" for both of its photosynthetic readings on every run ever recorded; against
`pho-a` it prints verdicts.

## What changed

| File | Change |
|---|---|
| `src/Evosim.Core/Ecosystem/LineageEvent.cs` | +18 −5. A `HasPhotosynthetic` birth-only property, carried through the private constructor and the `Birth` factory, written by `ToJson` after `jnt`. |
| `src/Evosim.Core/Ecosystem/World.cs` | +5 −3. The birth site passes the `photosynthetic` local, and three comment lines say where it comes from. |
| `src/Evosim.Core.Tests/LineageEventTests.cs` | +81. Field order on founder rows, the flag against the creature on a reproduction birth, no birth-only keys on a death row, and one new test over two inoculants. |
| `src/Evosim.Core.Tests/SnapshotJoinTests.cs` | +4 −4. Four synthetic fixture rows gained the new argument. |

The fourth file is outside the spec's list, and I could not avoid it. `LineageEvent.Birth`
has four call sites in `SnapshotJoinTests`, all of them hand-built fixture rows, and a new
`bool` before `patch` stops them compiling. The alternative was a defaulted parameter, which
would let a future call site omit the flag and write a false zero. Each of the four gained
one `false` and nothing else.

Nothing under `unity/Assets` was touched. `git status` on that path is empty, and the
theatre is untouched.

## What a row looks like

The first four birth rows of `pho-a`, beside the same four from `jobs-a`, the reference run
recorded before this change:

```
pho-a  {"e":"b","t":0.5,"id":0,"p":-1,"k":"f","g":0,"s":0,"abs":0,"jnt":0,"pho":1,"pt":0}
pho-a  {"e":"b","t":0.5,"id":1,"p":-1,"k":"f","g":0,"s":0,"abs":0,"jnt":0,"pho":0,"pt":2}
pho-a  {"e":"b","t":1,"id":2,"p":-1,"k":"f","g":0,"s":0,"abs":0,"jnt":0,"pho":0,"pt":0}
pho-a  {"e":"b","t":1,"id":3,"p":-1,"k":"f","g":0,"s":0,"abs":1,"jnt":1,"pho":0,"pt":1}

jobs-a {"e":"b","t":0.5,"id":0,"p":-1,"k":"f","g":0,"s":0,"abs":0,"jnt":0,"pt":0}
jobs-a {"e":"b","t":0.5,"id":1,"p":-1,"k":"f","g":0,"s":0,"abs":0,"jnt":0,"pt":2}
jobs-a {"e":"b","t":1,"id":2,"p":-1,"k":"f","g":0,"s":0,"abs":0,"jnt":0,"pt":0}
jobs-a {"e":"b","t":1,"id":3,"p":-1,"k":"f","g":0,"s":0,"abs":1,"jnt":1,"pt":1}
```

Both values are present among the founders, from the first metabolic step onwards. The
first six founder rows read 1, 0, 0, 0, 0, 1.

## Validation

### The Core suite

```powershell
./scripts/core-test.ps1                          # Failed: 0, Passed: 531, Total: 531, 1 m 9 s
./scripts/core-test.ps1 -Filter RunConfig        # Failed: 0, Passed:  27, Total:  27, 152 ms
./scripts/core-test.ps1 -Filter LineageEventTests # Failed: 0, Passed:  5, Total:   5,  35 ms
```

531 against the 530 the D078 report records, for the one test this change adds.
`RunConfigTests` and `RunConfigJsonTests` are green because nothing here is a tunable: the
flag never reaches `RunConfig`, so it cannot reach `Hash()`.

### The tiled world does not move

```powershell
pwsh -NoProfile -File rounds/launch-r24.ps1 -Seed 2 -Worker 7 -Seconds 1000 -Name pho-tiled
python scripts/compare-det.py pho-tiled fl-replay
```

`pho-tiled` against `fl-replay`: **10 shared samples, 84 fields each, 0 differing.** That is
the same number `jobs-tiled` returned on the D078 build, and it has not moved. The header
reads `physics jobs 0` and carries `configHash a521eff52dc063fd`, which is `fl-replay`'s.

I counted the fields myself rather than trusting the summary line. `compare-det.py` prints
"identical on all 10 shared samples" and no field count. So I read both files and compared
every shared key: 840 comparisons, 0 differences.

### The shared world is bit-identical to its reference

```powershell
pwsh -NoProfile -File rounds/launch-det.ps1 -Seed 4 -Worker 7 -Seconds 3000 `
    -DigestEvery 100 -Name pho-a -ExpectSimHash 63fbf5f7ea60
python scripts/digest-diff.py jobs-a pho-a
python scripts/compare-det.py jobs-a pho-a
```

```
jobs-a vs pho-a: identical over all 3001 shared digest steps (to step 300000, t=2999.9999329447746 s)
jobs-a vs pho-a: identical on all 30 shared samples (to t=3000); contacts/step at end 552.7578
```

Three thousand and one state digests, every one of them equal, over 300,000 physics steps of
a shared world running 553 pairs of touching bodies by the end. Both manifests end with 420
births and 412 alive. A lineage field cannot move the physics, and this is the proof.

### The rows themselves

The two runs also wrote the same lineage, row for row. I stripped `,"pho":[01]` from every
line of `pho-a`'s file and compared it against `jobs-a`'s:

```
rows 658 658
rows differing after removing the pho field: 0
births 535 (420 reproduction, 115 floor), deaths 123; pho=1 477, pho=0 58
```

This is a stronger statement than the digests alone. The digest hashes body state; the
lineage records which creature was admitted, in what order, with which id.

### The scorer

```powershell
pwsh -NoProfile -File scripts/tests/clade-score/run-tests.ps1   # all 28 assertions passed across 4 fixtures
./scripts/clade-score.ps1 pho-a
```

```
pho-a: photo (owner's wording): held -- clade root 0 (born 0.5), 203 alive at end, 182 inherited
       photosynthetic births in the last 20 samples (world-wide: photosynthetic clades alive at the
       last sample 7, largest 203; inherited photosynthetic births in the last 20 samples 383)
pho-a: photo (>=10, two lifetimes): failed (best photosynthetic clade min over the last 6000 s = 1)
pho-a: photo inh (population column): held -- photo inh at end = 403, min over last 20 = 47
```

Both lineage readings print a verdict. Scored on the same afternoon, `jobs-a` still prints
"flag absent | photo lineage: flag absent" for both. The scorer is telling apart a run that
predates the field from a world with no producer clade.

The second reading fails for a reason that belongs to the run and not to the flag. Its
window is the last two lifetimes, `t >= t_last - 6000`, and `pho-a` is only 3,000 s long, so
the window reaches back to an empty world at t = 0. A 30,000-s arm is where that reading
becomes meaningful.

### The hashes

| | Before | After |
|---|---|---|
| `coreHash` | `52eb64962a26a8b81572fa1088bb62fbfd7a541fbb1580e202bafacb1f275521` | `bff3d69617fb5f889f9c90c862252d81bfd2cff8909be6d5c7c0e96dca9c32f5` |
| worker 7 `simHash` | `d9fc3fc192298332552b430af5863d79018df7142db30a05bf6033092eafe36d` (at `jobs-a`) | `63fbf5f7ea60cb32fb4fe6b1c2baae5aff029f47a9b7413acd002b62b435bb7e` |

`coreHash` moved, as three changed files under `src/Evosim.Core` require. Both runs recorded
the same new value, and both recorded `gitCommit d2b59ac (DIRTY)`, the dirt being my own
Core edits.

`simHash` had already moved before I started, for the reason the spec's correction gives: a
comment edit under `Assets/Evosim` landed in `d2b59ac` after `jobs-a` was recorded. Worker 7
was refreshed from the main tree and reported `63fbf5f7ea60...` on its first run. It reported
the same value on the second, and `pho-a` was launched with `-ExpectSimHash 63fbf5f7ea60` so
a stale tree would have refused the launch rather than produced a result.

The digests agreeing across that `simHash` change is itself worth reading. Two different
`Assets/Evosim` trees produced the same 3,001 state digests, as a comment-only edit should.

## What it cost

| Run | Simulated | Wall min | Births | Alive at end |
|---|---|---|---|---|
| `pho-tiled` (tiled) | 1,000 s | 0.4 | 29 | 29 |
| `pho-a` (shared, digests on) | 3,000 s | 5.5 | 420 | 412 |

`pho-a`'s 5.5 minutes sits inside the 5.38 to 5.80 spread the D078 report measured for three
runs of the same world at the same setting. Four round-27 arms shared the machine throughout,
as they did then.

About 55 minutes of wall clock from first edit to last validation, of which roughly 12 were
Unity and 2 were the test suite.

## Two surprises

### The one CRLF file in its directory

`LineageEvent.cs` was stored with Windows line endings, while every other file beside it in
`Ecosystem/` uses bare newlines. My first write flipped the whole file to newlines,
invisibly. `core.autocrlf` is true, so the diff showed only the 18 lines I meant to add.

I caught it by reproducing the old `coreHash` offline. The hash is 30 lines of SHA-256 over
a sorted manifest. I rebuilt it in Python, took the pre-edit file from the git blob, and
hashed the tree twice. The newline variant gave `3b8d85e2f24e392c...`; the Windows variant
gave `52eb64962a26a8b8...`, the value every round-27 run records. That settled what the file
had been, and I restored it before compiling anything.

Two things come out of that. First, a checkout's two source hashes can be recomputed from
disk in seconds. CLAUDE.md's gotcha says `scripts/simhash.py` cannot be trusted for this;
this reimplementation agreed with the manifest to all 64 characters on the new tree too.
Second, an agent editing Core through a text-mode read-modify-write can rewrite every line
of a file while showing a four-line diff.

### A manifest counts reproductions, and calls them births

`pho-a`'s manifest says 420 births. Its `lineage.jsonl` holds 535 birth rows. The difference
is the 115 floor spawns, which the lineage records and the counter does not. Both runs agree
on both numbers, so nothing here is a fault. A reader taking the manifest's figure as
"creatures admitted" would be off by the floor.

## Runs left on disk

`runs/pho-tiled` (tiled, 1,000 s) and `runs/pho-a` (shared, 3,000 s, digests every 100
steps). Both ended `status ended`, `reason budget`, `divergedTotal 0`, `driveImpulsesLimited
0`. Nothing was stopped and `stop-arm.ps1` was not needed. Unity logs are in `scratch/logs/`.

## What I could not do

- Score the two-lifetime producer reading against a world old enough for it. That wants a
  30,000-s arm, and every worker but 7 is carrying round 27.
- Show that a *late* photosynthetic clade is distinguishable from a founding one. Every
  producer clade in `pho-a` roots at a founder, so the parent-chain walk is exercised only on
  lines that start at t = 0.5.
- Say what an older reader does with the new field. `clade-score.ps1` finds it by name and
  ignores its absence, and I tested that both ways. Any other reader of `lineage.jsonl` I did
  not look for.
