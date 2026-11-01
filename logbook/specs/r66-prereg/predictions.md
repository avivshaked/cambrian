# r66, the mouth screen: pre-registration

*Drafted by the coordinator on 2026-11-01, before D275's build has landed. It is committed, with the checks' results
filled in, before any world of the screen runs. The screen's design is the coordinator's, under the owner's delegation
of the grazer screens (D275's status).*

## The question

D275 lets a mouth's blow wound and feed only where the mouth point lies within one mouth radius of the bitten part's
true surface. In r64t's measured film window, only 25% of leaf kills and 41% of wounds on animals met that test. So the
rule takes most of a grazer's bites away at first. D275 enters it overpowered (D262): the first screen pairs it with a
richer bite, then dials back.

The screen asks three things:

- whether grazers live under the rule;
- which richer bite overpowers them on living leaves;
- whether they evolve to aim the mouth.

## The dial, and why this one

A richer bite can come from two dials (`logbook/specs/bite-needs-mouth-spec.md`, section 7).

- **The bite damage scale** (D226) deepens every mouth blow, on animals as on leaves. About two-thirds of r65's mouth
  blows left their part alive. A leaf bite usually takes the whole leaf part (`logbook/specs/kill-rule-read.md`), so
  those unfinished blows are more likely on animals (inference, unchecked). A deeper wound would then feed predators
  first, which is D225's risk and against the owner's direction that grazers earn from grazing (2026-10-30).
- **The leaf energy** (D211, `EVOSIM_LEAF_TISSUE_ENERGY`) multiplies the joules in a leaf's tissue. A leaf costs that much
  more light to build, and every bite of it holds that much more. Only a bite of a leaf, or of a leaf's remains, gains.

The screen uses the leaf energy, with a ladder derived from the landed share s of mouth blows. The power of two nearest
1/s gives a grazer about C's leaf joules per contact, and four times that gives about four times C's. If a quarter of leaf
bites land, the arms are 1, 4 and 16. The switch-on sitting below measures s on founders, and the arms' values are
written here from it before launch.

Two windows measured s on the drawn surface against the census pair. In r64t's, 25% of leaf kills were mouth touches. In
r65-c-6203's 90 s window from 22,500 s (c8's whole-window table of 2026-11-01, `scratch/leaf-clip/ate-table-r65-90s.tsv`),
53 of 327 leaf kills were, 16%, and 131 of 999 touching wounds on animals, 13%. At 16% the ladder would be 1, 8 and 32.

At leaf energy 2, r62g's grazers gained nothing (`logbook/specs/r62-grazer-screen/README.md`). That was under the corpse
rule and a 3 m reach, where finding the leaf limited a grazer and a meal was a corpse shared by several. Under contact
eating each contact is one bite of one part, so the joules per contact count directly (inference).

## What runs

D's world under true-shape contact with the smell off, as r65's C (`rounds/env-r65-c.ps1`), with D275's switch on:

| Arm | `EVOSIM_BITE_NEEDS_MOUTH` | `EVOSIM_LEAF_TISSUE_ENERGY` |
|---|---|---|
| M | on | 1 |
| Mk | on | k, the power of two nearest 1/s (4 if s is a quarter) |
| M4k | on | 4k |

Each arm runs on seeds 6202 and 6203 for 24,000 s, as two card groups of three, 6202 first, as r65 ran. The control is
r65's C, `runs/r65-c-6202/2026-11-01-131455-2991f19e` and `runs/r65-c-6203/2026-11-01-162827-2991f19e`, if check 2
below holds. Otherwise C reruns in each group, as a group of four.

## Before the screen

1. **The suites pass on the build**: Core's default suite, Dynamics and Farm, with only main's owed failures. The crowd
   fixture re-recorded with the switch off equals `runs/cefix-s4/2026-10-31-205155-87f3b329` row for row
   (`compare-run-rows.py --from 0`).
2. **With the switch off, the build is r65's C.** C founded on the card on each seed for 1,200 s equals r65-c's prefix in
   every stats, digest and lineage row. The configs differ only in the new key, at false.
3. **With the switch on, the card agrees with itself.** M on 6202 at groups 32 and 64 for 600 s passes `gpu-accept.py`,
   and M resumed from its 300 s checkpoint to 600 s repeats its founded rows (D273).
4. **The landed share s** is read from check 3's world: mouth blows that land, over mouth blows measured. It sets the
   ladder as above.
5. Each world's header names `trueshape` and the mouth rule, and Mk's and M4k's configs carry their leaf energy, or the
   world is stopped.

### The checks' results

The build is `bite-needs-mouth` in `scratch/wt-mouth`, its code that of `f28dbbc7`. Runs are under `scratch/r66/identity/`.

- **Check 1.** The default suites ran on 2026-11-01 (`scratch/mouth-bite/suites/`). Core passed 1,793 of 1,793 and Farm 413
  of 414. The one failure is `FilmWindowDigestTests`' pin, which main already fails, and whose digest covers the window's
  build hashes. Dynamics passed 453 of 466, with 3 skipped and 10 failed. All 10 read the crowd fixture, which predates the
  tunable and is refused. The fixture is re-recorded before the merge.
- **Check 2 passes.** C on the new build, founded on the card at group 32 for 1,200 s (`r66id-c-6202`, `-6203`), equals
  r65's C on both seeds: no difference in any of 24,120 stats values, and lineage equal row for row (3,114 and 2,662 rows,
  apart from the columns `compare-run-rows.py` names as the build's).
- **Checks 3 and 4 run longer than written.** No world bites before about 3,800 s: r65's C first bit at 3,850 s on 6202
  and 3,760 s on 6203, and M's 1,200 s at group 32 counted no blow at all. So the switch-on comparison runs to 6,000 s: M
  founded at group 64 to 6,000 s, against g32's M resumed from 1,200 s to 6,000 s. s is read over 3,000 to 6,000 s, in
  bodies that are no longer founders.

## Readouts

A grazer is a body born with attack and intake both above 0. No window is judged on fewer than 30 (D244).

1. **Grazers live.** Grazers number at least 30 in every judged window from 12,000 s (D256), in every arm on both seeds.
2. **Grazers live on leaves.** Over 21,000 to 24,000 s, the leaf share of bite income (`income-split.py`) is at least
   0.8, in Mk or M4k, on both seeds.
3. **The richer bite overpowers.** In the same arm, grazers in the last window number at least C's, and the leaves at
   24,000 s are at most half of C's (`overshoot-read.py`). Grazers that hold the leaves down are the overshoot that
   D262 asks for. Without it, the dial goes up again before it comes down.
4. **Grazers learn to aim.** The landed share of mouth blows over 21,000 to 24,000 s is at least 1.25 times its share
   over 12,000 to 15,000 s, in M on both seeds. Reported in every arm. The early window is the first with thousands of
   blows on both seeds: r65's C counted 55 and 272 blows by 6,000 s, and 2,716 and 6,465 between 12,000 and 15,000 s.
5. **The pace holds.** A world under 0.5 times real time is stopped at once. Each arm's body-seconds per wall second are
   within 10% of r65-C's on the same seed. The groups run on different days, so raw pace is reported and not judged.

Also reported: D222's three tests, grazer deaths by cause (`grazer-deaths.py`), grazer-on-grazer kills (`cannibal.py`),
newborns' time to first bite (`first-bite.py`), the share of kills by claws, and the fewest leaves after 3,000 s.

## Predictions (inference, written before any run)

- **The landed share of mouth blows on founders** is 0.15 to 0.35. The two windows above found 16% and 25% against the
  census pair, and the build measures every mouth against the nearest part, which can only raise it.
- **M** holds the grazer floor on both seeds (low), with grazers at 0.3 to 0.8 of C's in the last window. Under true
  shape r64t's grazers bit half as often and still rose, because grazers killed one another five to eight times less. In
  r65's window the rule would keep about the same share of leaf kills as of wounds on animals, 16% against 13%, so M's
  leaf share stays within 0.04 of C's 0.77 and 0.74 (low).
- **Mk** holds grazers near C's, at 0.8 to 1.5 times, with leaf share 0.78 to 0.85 (low).
- **M4k** overshoots on at least one seed: grazers 1.5 to 4 times C's and leaves under half of C's (low). The leaves'
  costlier growth may thin them before grazers boom. If so, M4k's leaves fall early, before 9,000 s, and its grazers
  never rise.
- **Aim** rises in M by 1.1 to 1.5 times (low). The rule pays a body whose mouth leads. Between the windows' middles
  r65's C gained 10 and 15 generations at its deepest line (`generationMax` 22 to 32, and 28 to 43), which is little time
  for selection, and r65 found that founders barely steer.
- **The pace** falls by at most 5% per body. The gap measure is up to a few GJK calls per contact per metabolic step,
  screened first by distance.

## What would show each wrong

- The landed share above 0.6: most of D275's misses came from the census pair's choice, and few from where the mouth was.
- M's leaf share falling below C's: the rule hits grazing harder than predation.
- M4k's leaves crashing while its grazers fall: the leaf energy thins the food faster than it enriches the bite.
- Aim falling in M: the rule kills the aiming bodies too slowly to be selected (inference: newborns starve before a
  first landed bite).

## What follows each outcome

- **Mk or M4k passes marks 1 to 3 on both seeds**: D262's dial-back runs between the two richest settings that passed,
  and the film's round takes the rule at the value where it settles.
- **Grazers live in every arm but mark 3 fails**: no arm overshot, and the ladder goes up by 4 again.
- **Mark 2 fails everywhere while the grazers live**: grazers earn too much from animals under the rule. That is the gap
  D264's phase 3, the diet gene and matter kinds, is meant to close (inference).
- **Mark 4 fails, and grazers struggle in M**: the founders aim badly and the world does not select aim in time. The
  pool's founders were trained in the nursery under the old rule, where any part touching food could eat. The next step
  is then the nursery retraining the pool with D275 on, before the film's round, rather than raising the dials further
  (agreed with the owner on 2026-11-01). A tank steps the farm's own `Simulation`, so a tank world with contact eating,
  true-shape contact and the switch carries the rule (read from the code, not yet run).
- **M dies on either seed**: the rule is too strict for founders. That goes to the owner with the read, since the rule's
  radius is the owner's.

## Rules chosen cheap, re-asked here

- **The mouth radius is half the mouth face.** It was chosen to match the drawing. For a thin eating part the radius is
  small, so a thin mouth must aim harder than a thick one. That favours thick mouths, a selection the screen does not
  measure.
- **The pose lags by one physics step**, as D214's mouth points do, about 1 mm at a grazer's speed.
- **Claws are unchanged.** A claw still wounds by brushing, and feeds nothing, since a claw has no intake.
- **Remains are unchanged as attackers.** A live mouth on a remains part is a mouth's blow and needs the mouth.
- **A leaf costs more to build at higher leaf energy.** Under D206 and D207 plants keep their matter, so whether light or
  matter limits a leaf decides how far M4k thins them. Which one limits D's world is not known here.

## Pace, and the groups

r65's groups of three ran 3 h 6 min and 3 h 21 min, at 2.0 to 2.3 times real time over the run. The screen takes the
same shape, so about 6 hours of card time, back to back. If M4k's grazers boom to four times C's, its pace falls with
its body count (`logbook/specs/slowdown/`), and the group runs longer.

## Open points

1. **The landed-share column.** The build counts `mouthBlowsRefused`. Mark 4 needs the measured blows in each window,
   which are `mouthBlows` plus `mouthBlowsRefused`. The build does not split a refusal by its target, so mark 4 is
   read over all mouth blows, and the leaf-and-animal split is reported only from film windows' ate-contact tables.
2. **Written**: `rounds/env-r66-m.ps1`, `identity-r66.ps1` and `launch-r66.ps1`. `env-r66-mk.ps1` and `-m4k.ps1` are
   written from check 4's s. The landed share is read from the stats columns `mouthBlows` and `mouthBlowsRefused`.
3. **The screen runs from `scratch/wt-mouth`**, branch `bite-needs-mouth`, at this file's commit, with the worktree's own
   Release farm. The branch merges to main after the crowd fixture is re-recorded on it, which the screen does not wait
   for: the switch-off proof that licenses r65's C as the control is check 2, on the card.
