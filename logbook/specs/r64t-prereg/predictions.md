# The true-shape screen: do mouths that really touch still feed a grazer world?

*By the coordinator on 2026-10-31, from an Opus subagent's draft, before any world of it ran. Committed before the screen
launches; the screen launches from a clean worktree at this file's commit (`scratch/wt-r64t`, with its own Release farm),
on main after the smell build (D270) lands, since its env files name that build's switches. The owner chose on 2026-10-31
that no mouth video goes out until mouths really touch; this screen asks what true-shape contact costs a grazer world.*

## The question

Under sphere contact (D252), a mouth bites when two balls first touch. In 11 recorded kills the real surfaces were still
0.32 to 0.97 m apart (`scratch/owner/mouths-in-action/ball-table.md`). D228's level (a), `TrueShape`, keeps the ball as
the broadphase and tests the pair on cores and margins by GJK. On the host a pair enters the overlap census only after that
test passes (`ContactTrueShape.cs`, `NoteOverlapPart` after `Pair`), and the census is the list the mouth reads
(`Metabolise.HandOverContacts`). So the question is this: when mouths must touch the true surface, how much less do grazers
bite, and does D's grazer world still hold its grazers, at what pace?

## The worlds

| World | Contact | Source |
|---|---|---|
| `r64-d-6202` (off, 6202) | `Spheres` | recorded run, not rerun |
| `r64t-s-6203` (off, 6203) | `Spheres` | run |
| `r64t-t-6202` | `TrueShape` | run |
| `r64t-t-6203` | `TrueShape` | run |

- **The world is `rounds/env-r64-d.ps1`.** Its chain is `r63h-d`, `r63c-b`, `r63b-b`, `r63b-a`, `r62cf` and back to `r55`,
  which sets `EVOSIM_CONTACT_PER_PART = 1`, so the true shape is allowed. The new env file `rounds/env-r64t-true.ps1` is D's
  file plus `EVOSIM_CONTACT_SHAPE = 'trueshape'`. Following `r63b-a`'s convention, the smell build's switches are set off
  by name.
- **The second seed is 6203,** the film line's second seed (`r63c-b`, `r63s` and the anchoring check used 6202 and 6203).
  D's world has never run on 6203, so its "off" must be run.
- **"Off" for 6202 is the recorded run.** It rests on two sittings matching `r64-d-6202`'s prefix row for row: the smell
  landing's switch-off sitting (`rounds/identity-d270.ps1 -Part off`, 600 s) and step 2 below, as `r63s` reused `r63c-b`.
  Group makeup does not move a world's bits (`identity-anchor` matched D in a two-world group against a five-world
  recording), and D271 and anchoring are identical with their switches off.
- **The length is 24,000 s,** the recorded run's whole length. It covers four of D's bloom periods (5,000 to 10,000 s),
  and D's boom came only at 18,000 to 24,000 s (grazer means 709, then 1,255).
- **The launcher** is `rounds/launch-r64t.ps1`, shaped like `launch-r64.ps1`: one card group `r64t` of the three run worlds,
  `-Group 32 -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 -HeatStop -WallMinutes 720 -WaitForManifestSeconds 600`,
  with `-AllowBeside` as the day's machine ruling sets it.
- **No world is resumed.** A resumed world cannot change its contact form (D271), so both true-shape arms are founded.
- **Anchoring is not in D's world** (`EVOSIM_ANCHORING` unset), so grips are not read here. If the owner wants grips read
  under true shape, they ride along as a fourth world (`env-anchor-brain-check.ps1` plus `trueshape`), read for grips only.

## Checked before launch

On the merged build, after the smell landing's own sittings pass:
1. **The CPU accelerator, founded.** Replaced before launch by a check that already ran on this build:
   `GpuEngineTests.TheDriveAndThePushesOnTheCpuAcceleratorAreTheSolver` with `trueshape` and `trueshape-crowd` steps founded
   true-shape worlds on the CPU accelerator and matches the solver to the bit after every block, a death and a birth
   included, after showing each world parts from its sphere control. It passed among the 122 Gpu tests run on the smell
   build on 2026-10-31 (`scratch/smell-builder/land/gpu-nomean2.log`). A resume is not part of the screen, which founds
   every world.
2. **The switch-on sitting, as a mixed group.** One group of `r64t-t-6202` and a spheres D on 6202, for 600 s at group 32
   and again at 64: `gpu-accept.py` t at 32 against t at 64 exits 0; `compare-run-rows.py` the spheres world at 32 against
   `r64-d-6202`'s prefix, row for row (the reuse, and that a true-shape world in the group leaves a spheres world unchanged);
   the manifests' farm, Dynamics and Core hashes recorded here and reappearing in the screen's.
3. Each world's config shows `trueshape` or `spheres` as its row says, or that world is stopped.

## Readouts

Per 3,000 s window from 6,000 s, each true-shape world against the same seed's spheres world.
- **Contact share**: mouth blows per grazer-second. `stats.jsonl`'s `mouthBlows` (cumulative, every mouth's blows),
  differenced per window, over the grazer-seconds from `lineage.jsonl` as `scripts/reads/bloom/r63g-motion/income.py` counts them. An upper
  bound, since every mouth's blows are counted. The script is `scripts/reads/contact-share.py` (`--against` gives the ratio per window).
- **Intake over upkeep**: `scripts/reads/bloom/r63g-motion/income.py` against the standing cost of 0.0091 W, and D222's test 2 from
  `g2-read.py --upkeep-w 0.0091`.
- **Grazers and breedings**: `scripts/reads/bloom/g2-read.py --survive-from 12000`.
- **Kills**: `scripts/reads/bloom/grazer-deaths.py` and `cannibal.py`.
- **Income source**: `scripts/reads/bloom/income-split.py`, the leaf share.
- **Leaves**: `scripts/reads/overshoot-read.py` and `scripts/reads/bloom/r63g-gates.py`.
- **Pace**: `scripts/reads/pace-calc.py`; the clean ratio is t-6203 over s-6203. Against `r64-d-6202`'s own pace (2.40 times real time,
  five-world group) the ratio carries a group caveat.
- **The touch at the kill**: c8's measurement (`scratch/mouths-in-action/ball_table.py`) on film windows of both
  true-shape worlds: the part-surface gap and the mouth-point gap at the kill frame.
- **Floaters**: remains lost through the top, by `r63s-read`'s "The floaters" count.

## Predictions (inference, written before any run)

- **Contact share falls to 0.3 to 0.6 of off's.** A part's ball stands up to 0.55 m past its thin faces, and centres at
  the kill sat 0.85 to 1.34 m apart against surface gaps of 0.32 to 0.97 m. Encounter cross-section scales roughly with the
  square of the touching distance, and flat blades lose the most.
- **Intake over upkeep falls by about the same factor early**, recovering partly as lineages adapt, not to off's level.
- **Grazers fall**: last-window mean at 0.2 to 0.6 of the same seed's off (6202: about 250 to 750 against 1,255). The floor
  of 30 holds (moderate), with starvation's share of grazer deaths above off's 60 %.
- **Kill share** of grazer deaths at most off's (0.07).
- **Leaf share** between 0.45 and 0.65, against 0.59 (a guess either way).
- **Leaves end higher**: 1.2 to 2 times off's 2,430, the fewest after 3,000 s above 897.
- **Pace**: t over s is 0.80 to 0.98; a dense grove has more ball pairs that reach GJK than the identity sitting's crowd.
- **At the kill**, the part surfaces are in contact (gap 0 within the margin) in every measured kill; the mouth-point gap
  has a median at most 0.2 m, with a tail up to the eating part's length.
- **Floaters** no more than off's: true shape orders a coincident pair vertically by id, a bounded kick.

## What would show each wrong

- Contact share above 0.8 of off's: balls were not what limited bites. Below 0.2: something beyond geometry is at work.
- Grazers above 0.8 of off's, or extinct in either seed. Leaves below off's.
- Pace under 0.8 of off's. Any world under 0.5 times real time is stopped at once and probed.
- Any kill with a part-surface gap above 0.05 m: the card's census does not follow its narrowphase; stop and check the
  card against the host.
- Floaters above off's: true shape's vertical order needs its own fix.

## What the screen reports for the owner's ruling on the film's round

It decides nothing. It reports the gaps at every measured kill; whether both true-shape worlds held the floor of 30 from
12,000 s and never reached 0; contact share, intake over upkeep, grazers, kills and leaves as true-over-off ratios per
seed; pace as t over s; floaters. The owner then rules on (a) true shape in the film's round as is, (b) with the smell
build (D270) on, or (c) at level (b), `TrueShapeAtPoint`. Per D262, no dial is tuned for balance before this screen is seen.

## Rules chosen cheap under spheres, re-asked here

- The bite is a ball overlap: the finding itself.
- The 3 m `EVOSIM_INTAKE_REACH` reads corpses only; contact eating makes none, so it binds nothing in D's world.
- Grip forming: `RockTouched` and the bed touch follow the true shape; round 59's founders never gripped under spheres.
- D271 does not apply under true shape.
- Level (a) pushes at the link centre with no torque; level (b) touches the bed at every vertex.
- Overlap-based instrument columns (such as `ovl/body`) count true touches under true shape, so they are not comparable
  across arms.

## Open points

1. **The card refuses true shape when the shape list holds a family member** (`GpuGuardTests`). D's world lists no family,
   so the screen runs. A film round with both G5's family and true shape needs the card build on branch `ts-family`
   (written 2026-10-31, not yet tested).
2. The card's overlap census following its narrowphase is checked on the host only; the kill-gap readout tests it on the card.
3. Whether one card group may mix contact forms is unknown until step 2's sitting; if it is refused, the three worlds run
   as two groups and the pace ratio carries the group caveat.
4. No prediction uses off-6203's numbers, which do not exist yet; hence ratios.
5. The kill-gap read needs the theatre lane; the film recorder's eating bit is not on main, so the read uses the kill rows.
6. The env files and launchers are `rounds/env-r64t-true.ps1`, `env-r64t-spheres.ps1`, `launch-r64t.ps1` and
   `identity-r64t.ps1` (`ccc399c9`).
