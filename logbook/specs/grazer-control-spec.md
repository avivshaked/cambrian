# The grazer control: can a mouth eat round 58's leaves?

*2026-10-12, by the coordinator, on the owner's go of that afternoon. It is a diagnostic, not a round. Its
sources are a Fable agent's advice on evolving grazers (`logbook/specs/grazer-control/fable-advice.md`) and an
Opus agent's genome design (`logbook/specs/grazer-control/design-notes.txt`). Every number says whether it is read
from code, computed, measured or guessed. Nothing in it has run yet.*

## The question

Nothing grazes in round 58. Each world ends with 5,300 to 6,900 bodies, nearly all leaves, and its counters
read no part killed, no body eaten and no corpse from a kill (measured, the last stats rows). On paper a kill
pays well. A small mouthed body costs about half a watt, and one median leaf, eaten whole, keeps about 82 J
after the mouth's waste (computed). The Fable agent named three blocks that might explain it:

- **A two-gene valley.** Attack and intake both start at zero on every founder, and neither pays alone (read
  from code).
- **Placement.** A body with intake is planted where the corpses lie, which is on the bed, and the leaves
  float above it (read from code, D129).
- **Reach.** A kill's corpse appears at the dead body's centre (`World.cs`, Bury). A mouth eats only within
  the corpse's radius plus 0.5 m of its own centre (`WorldMouth.cs`, InReach), about 0.87 m for a median
  leaf, and that leaf is a sheet 2.7 m long (computed). So a killer may be unable to eat what it kills.

The control skips the first block by hand and asks whether the other two stop a working mouth.

## The bodies

Hand-built, format 12, in `inocula/grazer-control/`. The runs' manifests record each genome's sha256 over its
bytes with LF line ends, as committed: `83b7303a…` for plate-late and `1510f5b4…` for plate at margin 600. A
Windows checkout writes CRLF, so hash `git show HEAD:<path>` rather than the working copy. None has a brain, a joint or a bladder. The mouth bites from
the step's contact list and needs no brain (read from code). Round 58's bodies of 0.25 m³ or less feel no
sink at all (`fluid.neutralBodyVolume`), so a bladder would do nothing (read from code and config). The
breeding genes come from a real seed-1 genome (167087), except investment.

- **`grazer-plate-late.json`**: a consumer plate 0.74 by 0.06 by 0.74 m, attack 1 and intake 0 when planted,
  with a 0.2 m consumer cube at attack 1 and intake 1 added as a module at its first growth step.
  - Planted without intake, it skips the corpse placement and lands at the depth asked for, among the
    leaves. Whether an inoculant adds the module is a path never yet used (read from code; untested).
  - It kills a bare median leaf in one step, costs about 0.32 to 0.41 W, and lives about 480 s unfed
    (computed).
- **`grazer-plate.json`**: the plate with attack 1 and intake 1 from the start. The placer puts it by the
  corpses, so it tests the mouth on the bed (read from code; where it lands is a guess). It costs 0.44 W
  and lives about 456 s unfed (computed).
- **Reserve margin 600 s** in both, against the source genome's 87 s. The first smoke (`r58g-smoke`, 60 s,
  read by `scripts/reads/r58g-read.py` into `logbook/specs/grazer-control/r58g-smoke-read.txt`) planted all 20 at
  the asked depth, with no overflow. But at margin 87 each grazer bred twice within 1.5 s of planting, which
  left it about 58 J, below the 94 J the module rule needs, so no mouth grew. The 40 children starved within
  16 s, and one parent and child killed each other, being born touching. Breeding is gated at the brood's
  cost plus the margin times standing watts (read from code, `Organism.ReproductionThreshold`). At 600 s that
  gate is about 259 J before the mouth and 315 J after for plate-late, and 333 J for plate, all above the
  197 J purse (computed). So no grazer breeds before it has eaten. The margin-87 files are kept as
  `*-m87.json`.
- **Investment 1.3** in both, against the source genome's 0.11. An inoculant is planted at its genome's own
  birth fraction, and at 1.3 that is the whole adult with the full 200 J purse. The cost is that a child is
  as large as its parent and starts with a fifth of its tissue as reserve, so the line can hardly breed
  (computed). The control reads killing and eating first. Breeding is a weak third reading here, and it
  says so.

## The runs

Each run resumes the isolated seed-1 world `r58-s1ir` from its 12,000 s checkpoint, alone, on round 58's build
(`r58-runner` `141c97a3`, the card, every switch of the round, with no checkpoints written). It plants 20
grazers at the first step: `EVOSIM_INOCULATE` names the genome, `EVOSIM_INOCULATE_AT=12000`,
`EVOSIM_INOCULATE_COUNT=20`, and `EVOSIM_INOCULATE_DEPTH` is the leaves' mean depth in `r58-s1ir` at
12,000 s (photosynthetic bodies only). A resume takes the assay's four settings from the launcher, not from
the resumed config (read from code, `Program.cs`), so a launch sets all four. `card-group.ps1` clears them
and refuses a group of one, so the runs go through `card-run.ps1`, by the speed agent's
`rounds/r58g-control.ps1`, which logs the whole environment to `scratch/logs/r58g-<step>.log`.
The manifest records only the inoculum's path and hash. On a resume the report's settings line describes the
launch's environment rather than the resumed world. Zero's line reads a mouth with reach 0 and no prices. The
world it ran is `r58-s1ir`'s `config.json`, with reach 0.5 m, waste 0.2, both prices 0.1 and neutral volume
0.25 m³, as its configHash confirms. Read the world from `config.json`.

1. **Zero**: no inoculum, 300 s, compared with `r58-s1ir` from 12,000 s by `group-resume-compare.py`. A lone
   resume of a world that ran in a group has never been shown to replay it, and A and B are read against
   `r58-s1ir`, so this comes first.
2. **The smoke**: plate-late for 60 s. It checks that the genome loads (it is validated only when the assay
   fires), that 20 bodies are admitted or refused for a stated reason, where they stand, and that the module
   is added.
3. **A, the attack test**: plate-late, 3,000 s.
4. **B, the intake test**: plate, 3,000 s.

Each run is about 12 minutes of the card (guessed from round 58's pace). Both run after round 58 and B2
have ended, alone on the card, under the heat watch, with nothing beside them.

## The readings

Per run, against `r58-s1ir` over the same seconds:

- **Kills**: lineage kill rows whose `by` is an inoculant or its descendant, split into parts killed and
  bodies eaten. Also the counters `partsKilled`, `bodiesEaten` and `corpsesFromKills`.
- **Eating**: `unitsEaten` above `r58-s1ir`'s, in units of 100 J. Also the grazers' deaths: starved ones
  near 450 to 490 s show a body that did not eat, and a body alive well past that has eaten.
- **Reach**: for each kill, the eater's distance from the corpse at the kill, from positions at the nearest
  sample.
- **Breeding**: births from the inoculants' line.
- **The world**: `alive` and the leaves' count against `r58-s1ir`, in case a working grazer strips the crowd.

## What each outcome means

- **Kills and eats**: the mouth works in this world, and the block is the valley and placement. The next step
  is the nursery for a brain that finds leaves, put to the owner with its score.
- **Kills and does not eat**: the reach is the block. It is a world rule, and a proposal goes to the owner:
  measure reach from the biting part, or place the corpse at the contact.
- **No kills**: the bodies do not meet the leaves, or cannot hurt them, which a leaf's cuticle could do
  (computed: a protection above 0.18 makes a leaf proof against the plate). Read the contacts and the
  cuticles before anything else.

## What the runs showed

**Zero** (`r58g-zero`, 300 s, no inoculum) is identical to `r58-s1ir` from 12,000 s, with no overflow. A
lone card resume of a world that ran in a group replays it.

**The second smoke** (`r58g-smoke2`, 60 s, plate-late at margin 600) planted all 20 at the asked depth, with no
child. The module counters rose by 20 at the first sample, so every grazer grew its mouth at its
first growth step.

**A** (`r58g-a`, 3,000 s, read in `logbook/specs/grazer-control/r58g-a-read.txt` and `r58g-a-reach.txt`, the second by `scripts/reads/r58g-reach.py`) ran clean, with no
overflow and every check holding.
- One grazer of 20 killed two leaves, at 12,065 and 12,179 s. Both were whole-body kills, the root lost to
  the first bite. That grazer was resting still on what is likely a reef cap at −2.45 m, and both leaves
  drifted down onto it.
- The world's `unitsEaten` rose 166.6 J over the following 350 s, against 165.9 J left in the two corpses.
  The record carries no per-eater meal, so that the grazer ate them is an inference. It is supported by its
  life of 709.5 s, against 492.5 to 531.5 s for 18 of the others.
- The killer stood 1.11 m and 0.99 m from its victims' centres at the sample before each kill. Kill 2's
  corpse came within reach 30 s after the kill, as it sank past the grazer.
- The other 19 never touched a leaf. All 20 starved between 12,493 and 12,710 s, and none bred. The world's
  living and leaves tracked `r58-s1ir` within the noise.

So a mouth kills a leaf in this world, and probably eats it. In A, the first block is meeting a leaf at all.
A grazer and a leaf at the same depth drift in the same water, and only a body that holds still meets the
drifting leaves (inference). Two kills are too few to rule on reach.

**B** (`r58g-b`, 3,000 s, plate with intake from planting, read in `logbook/specs/grazer-control/r58g-b-read.txt`
and `r58g-b-reach.txt`) ran clean, with no overflow.
- The placer put 19 of the 20 on or just above the bed beside settled corpses (median −21 m), and one in the
  shallows at −2.19 m (read from code and positions).
- The bed's mouths scavenged at once. Over the line's life, `unitsEaten` rose 34.4 units against the control's
  3.3, and `corpsesEaten` 70 against 11. That is about 2,500 J kept beyond the control, nearly all carrion.
- It did not keep a 0.44 W body alive. All 20 starved, at a median of 630 s against 456 s unfed, and the
  longest at 1,381 s.
- The one kill was the shallow grazer's, of a leaf beside it, with the corpse within reach throughout. No
  grazer on the bed killed. Four children starved within 11 s, and the world's living and leaves tracked the
  control.

Across A and B, 40 mouthed bodies made three kills, all of leaves. Every corpse was in reach when measured,
or crossed into it, and each kill came with a rise in eating. A body that holds still at the leaves' depth
meets drifting leaves. One that drifts with them, or sits on the bed, does not. So in this world, at this
build, the block to grazing is meeting the leaves, which is a matter of where a body holds and where it goes
(inference). Reach is not shown to block, though three kills cannot rule it out.
