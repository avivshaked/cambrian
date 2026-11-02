# D280's build spec: the mouth's feel and the bite's reach dial

*A design read for the coordinator, 2026-11-02, on main at `7c7c7ea5`, shaped as `logbook/specs/bite-needs-mouth-spec.md`. An
Opus design reader wrote sections 1 to 8, which are kept as it wrote them. The coordinator's amendments follow in section 9,
and where they differ, section 9 rules. Inference is marked.*

## 1. The feel's form

- Each eating part reads the smallest ratio of gap to mouth radius from its mouth point to any link of any other body, living or remains. The gap is `MouthTouch.GapAt`'s, distance to the core less the margin (`src/Evosim.Dynamics/MouthTouch.cs:53-67`, made internal). The gate bites whatever body it touches (D219, D275), so the feel reports what the gate rules on, and "food only" would be the same set.
- The body's own parts do not count, nor do the bed, the reef or coral, or the glass. Rock has its own sense (D162), and a floor holding the feel at 1 would hide the food on it.
- The curve is `f = clamp((R - ratio) / (R - 1), 0, 1)` in double, narrowed once. It reads 1 at D275's touch, a ratio of 1 or less (`MouthTouch.cs:11-14`), so at k = 1 a feel of 1 is a bite that would land. It falls linearly to 0 at R. With nothing in range it reads 0.
- R is 8 radii, a tunable. In r65-c-6203's film window, where only a part touched the food, the mouth lay a median 3.07 to 3.21 radii from it on the true surface, p90 5.0 to 6.3 (`logbook/specs/r66-read/theatre-measure/split-pair-summary.txt`). At 8, nine in ten of those mouths read above 0. The pool's direction swimmer (`inocula/r59-direction-tops/direction-sparse-g33-47.json`) has a 0.3 by 0.15 by 0.3 m root, so its range is 0.3 or 0.6 m, by which face holds the mouth (inference).
- It is held once a metabolic step, at the top of `Simulation.Step` beside `HoldFarSenses` (`src/Evosim.Farm/Simulation.cs:429-434`), after the reconcile, and read through the block's 50 physics steps. A per-step feel would need GJK and a neighbour search in the card's kernel. The poses are the ones the gate has just ruled on, so after a bite the brain reads the measure that decided it. `Contact` is as stale (`CreatureSenses.cs:172-174`). It reads only poses and core tables, so a resume recomputes it, as with `HoldFar` (`CreatureSenses.cs:327-331`).

## 2. Where it is computed, and its cost

- The census names only pairs whose true shapes penetrate (`ContactTrueShape.cs:112`, :128, :285), so a mouth two radii from a leaf it does not touch is in no pair. A neighbour query is needed, in a new `src/Evosim.Dynamics/MouthFeel.cs` with reused arrays:
  1. Hash every link of every solver body by its centre into cells of edge h (1 m suggested), wrapping along a seamed axis with a whole number of cells a side.
  2. A feeler is not remains, has a brain that reads channel 22, holds its phenotype's plan and has an eating part of radius above 0. Skipping a non-reader changes no number (`src/Evosim.Core/Brain/Brain.cs:143-145`).
  3. For each mouth point, posed as at `src/Evosim.Farm/Metabolise.cs:432-434`, scan the cells within `R * r` plus the step's largest `LinkReach`. Skip a link whose bounding ball grown by `R * r` misses the point (the test at `MouthTouch.cs:125`), call GJK on the rest the short way round, and keep the smallest ratio.
  4. Write f into the feeler's `CreatureSenses`.
  Correctness must not depend on h.
- The arithmetic stays on the host and the card copies the value. A minimum ignores order and each feeler writes only its own array, so the pass runs over `Parallelism.ForRanges` as `Exposure` does (`Metabolise.cs:147`) and is the same at any thread count.
- The cost is inference until measured. At 24,000 s r66-m-6202 held about 11,300 links and 630 eaters, and r66-m-6203 about 20,800 links and 2,170 eaters. The grid costs about 1 ms. A mouth scans 27 cells and tens of bounding balls and makes a few point-to-core GJK calls of about a microsecond, so 5 to 10 µs. A thousand to 3,300 mouths take 5 to 30 ms a metabolic step on one thread, 3 to 12% of the runs' 190 and 242 ms (`wallTotalMs` over 48,000), and about 1% on ten threads.

## 3. The genome side

- `SensorChannel.MouthFeel = 22` (`src/Evosim.Core/Genome/NeuronInput.cs:303` holds 21), index count 1. `IsSmell` (`SensorChannels.cs:127-130`) leaves it out, so a nose world's eating parts carry it.
- `CentralWiring.IndicesOn` returns `MouthPoint.IsEatingPart(part) ? 1 : 0` for it, beside the grip rule (`CentralWiring.cs:76`). So `Ports` (founders' wiring, mutation's draw) and `AllPorts` (what stored links resolve against) carry one feel port on each eating part.
- `RunConfig.SensorPool` (`RunConfig.cs:4826`) appends it after the ground current (:4889), in `extra` and the all-off test. Off, every draw is the draw it was.
- `CreatureSenses.Read` (:792) returns the held value, `Report.cs:905` names it `mouthfeel`, and the nursery lists it (`TankWatcher.cs:364-397`, `FrameRecorder.cs:588`).
- Genomes recorded before it read nothing from channel 22 and resolve as before, since `AllPorts` only adds ids no stored link names. A feel link in a world with the feel off is kept and reads 0.
- Random founders read it at once, since `FounderLinkShare` 1 wires every pool port to every output. Pool founders keep their stored brains unmutated (`src/Evosim.Core/Ecosystem/World.cs:7384-7391`) and gain it only through `AddLink` (`CentralMutation.cs:86-104`). The direction swimmer has 33 ports under r66-M's pool and 3 units, so at `AddLinkChance` 0.3 about one birth in 120 adds a feel link, of random sign and target. r66's grazers ran 10 to 15 generations, so most lines would never carry one (inference).
- I recommend a graft, `src/Evosim.Core/Genome/MouthFeelGraft.cs`, a genome edit as D235's `NoseGraft` is (`NoseGraft.cs:39-41`), with a nursery verb beside `NoseGraftCli`. It adds one link from each eating part's feel port to every output, weight drawn from N(0, 1) on a stated seed, D147's founder scale for one input and strong as D262 asks. It writes the grafted pool to `inocula/` with its TSV, and refuses a per-part brain, a body without an eating part and a genome already reading the feel. No world rule changes, and the pre-registration chooses which arms take it.

## 4. The reach dial

- `RunConfig.BiteReachRadii`, `[Tunable("mouth")]` after `BiteNeedsMouth` (`RunConfig.cs:1798`), default 1, finite and above 0, JSON `biteReachRadii`.
- The gate reads it once a pass beside `needsMouth` (`src/Evosim.Core/Ecosystem/WorldMouth.cs:384`) and hands it to `StrikeNeedingMouth`, whose test at :495 becomes `mouthRatio <= reach`. A NaN still refuses.
- `MouthTouch.Reach` (:89) takes it, and its skip at :125 becomes `reach[q] + reachRadii * radius`. Without that, a mouth between 1 and k radii outside a part's bounding ball reads infinity and is refused. `Metabolise.cs:525` and :531 pass it. At 1 every expression is the old one.
- The dial widens the gate on the census's pairs and makes no pair, since D280 amends D275's reach and a mouth's blow is a contact's (D219, D226). D280's sentence also reads as a bite at a distance, so the owner should confirm.
- In the same window about half the part-only contacts had the mouth within 3 radii, so about half of all mouth blows should land at k = 3, against r66-M's 0.17 (inference).
- `mouthBlowsWide` counts blows landing at a ratio above 1 and at most k. Its column and checkpoint field exist only with the rule on and k not 1, as `mouthBlowsRefused`'s do (`src/Evosim.Farm/Row.cs:1071-1073`, `WorldState.cs:646`, :1041). Its share of `mouthBlows` falling is the sign the dial can come back. The header's `· a bite needs the mouth` (`Report.cs:490`) gains ` within k radii` when k is not 1.
- The theatre's mouth-touch verdict tests a ratio of at most 1 (`theatre-look-merged`) and must read k, or filmed and counted bites disagree. That is the theatre lane's.

## 5. Switches, tunables and what moves

| Tunable | Group | Default | Variable | Refused |
|---|---|---|---|---|
| `SenseMouthFeel` | sense | off | `EVOSIM_MOUTH_FEEL` | on without a true contact shape |
| `MouthFeelRangeRadii` | sense | 8 | `EVOSIM_MOUTH_FEEL_RANGE` | unless finite and above 1 |
| `BiteReachRadii` | mouth | 1 | `EVOSIM_BITE_REACH` | unless finite and above 0; off 1 with the mouth rule off (`World.cs:3179`) |

- `SolverConfig` carries the feel's two (`SolverConfig.cs:449-451`), and `GpuWorld` (:60, :216) and the stepped-against-built check (`GpuBackend.cs:275`) the switch.
- On the card, `FeelHeld` is one float a link, a one-cell stub when off, made as `CurrentHeld` is (`gpu-runner.template:92-99`, :202-211). The flag is set beside `FarCurrent` at :1117, `PackBlock` copies it after :3310, and `SensorRead` reads it after `gpu-kernel.template:5615` behind `cfg.MouthFeel`, with `SMouthFeel = 22` at :647. Regenerate with `EVOSIM_GPU_REGENERATE=1` (`GpuEngineTests.cs:691`).
- With the feel on, the sense token (`Report.cs:677`) gains `· mouth feel range 8 radii`, and the row gains `mouthFeelReaders` (living bodies that read it), `mouthFeelNear` (eating parts above 0 at the last hold) and `wallHarnessFeelMs`.
- The three move `src/Evosim.Core.Tests/fixtures/r42-config.json` and `r57-s2-config.json` (keys and hashes), `EnvBindingTests.cs:62`, `ReportTests.cs`' `Replace` and `EndsWith` lines, the live `rounds/nursery/configs/` (`scripts/nursery-config-forward.py`) and `src/Evosim.Nursery.Tests/WorldConfigFileTests.cs` (`InlineData`, the asserts at :58). The crowd fixture (`src/Evosim.Dynamics.Tests/RunFixture.cs:45`, :55, :344-345) is re-recorded. `ParallelIdentityTests`' word may move (unconfirmed). The theatre's `RunRecord.ProvenOffSwitches` takes the three after their proofs, and needs a number fill, since it fills only `false` (inference from `d71f29b2`).
- `DialOffDigestTests.PinnedDigest` (:48), `BiteRebuildTests`' folded hashes and `FilmWindowDigestTests` must not move. The last is stale on main already (HANDOFF).

## 6. Proofs, in order

1. `./scripts/core-test.ps1`, then Dynamics and Farm through `scripts/card/core-offcard-test.ps1` with `FullyQualifiedName!~GpuMeanTests`, built Release with `UseSharedCompilation=false`; `-All` before the commit.
2. `TheGeneratedKernelsAreTheTemplatesOutput` passes.
3. The card tests of section 7 and `scripts/card/accelerator-batch.ps1` on the CPU accelerator, and a founded `card-group.ps1 -Device cpu` of 30 s with every flag the screen uses.
4. Switches off on the card: `card-group.ps1` at group 32, `rounds/env-r66-m.ps1` on 6202 and 6203, 1,200 s, checkpoints every 300 s. `python -I scripts/reads/compare-run-rows.py --from 0` against `runs/r66-m-6202/2026-11-02-004136-47e1fc9f` and `runs/r66-m-6203/2026-11-02-031710-47e1fc9f` finds every row equal. If they part, main's own build is run first, since r66 ran from the `bite-needs-mouth` worktree.
5. Switches on: r66-M with the feel and k = 3 on both seeds at groups 32 and 64. Group 64 runs 6,000 s, past the pool grazers' arrival near 3,100 s. `gpu-accept.py` exits 0, and the rows agree to 1,200 s.
6. The group-32 worlds resumed from 1,200 s to 6,000 s equal group 64's from 1,200 s (`compare-run-rows.py --from 1200`). Steps 4 to 6 take `rounds/identity-r66.ps1`'s shape.
7. The crowd fixture, re-recorded with the switches off, equals `runs/cefix-s4/2026-11-02-031625-b958f841` row for row (the coordinator's farm run).

## 7. Tests to write

- `src/Evosim.Core.Tests/MouthFeelConfigTests.cs`: `DefaultsAreOffAndEachMovesTheHash`, `ChannelIsTwentyTwoForGood`, `FeelIsAppendedLastAndOffIsThePoolItWas` (fails if inserted mid-pool), `OnlyEatingPartsCarryItInANoseWorldToo`, `RangeAndReachRefuseWhatIsNotFinite`, `FeelRefusedWithoutTrueShape` and `ReachOffOneRefusedWithoutTheMouthRule` (fail on a missing refusal naming D280), `StoredBrainResolvesAsBefore` (fails if `Effective` changes a pool genome's links).
- `BiteNeedsMouthTests.cs` gains `AtReachThreeTwoAndAHalfLandsThreeAndAHalfIsRefused`, `AtReachOneStateBytesAreTheOnesBefore` and `WideCountWrittenAndCheckpointedOnlyOffOne`.
- `src/Evosim.Core.Tests/MouthFeelGraftTests.cs`: `OneLinkFromEachMouthToEachOutputAndNothingElse`, `SameSeedSameGenome` and the three refusals.
- `src/Evosim.Dynamics.Tests/MouthFeelTests.cs`: `CurveAtHalfFourAndAHalfAndNineRadii` (1, 0.5 and 0 against a box face), `NearestOtherBodyCountsTheBodyItselfNever`, `RemainsCountTheBedDoesNot`, `GridEqualsBruteForceOverACrowd` (across a seam at three cell edges; fails if the grid misses a candidate), `NonReaderReadsZero`, `SameAtOneFourAndSixteenThreads`.
- `MouthTouchTests.cs` gains `ReachAtThreeFindsWhatTheOneRadiusSkipDropped` and `ReachAtOneIsTheReachBefore`.
- `src/Evosim.Farm.Tests/MouthFeelFarmTests.cs`: `SwitchesBoundShownOnlyWhenOnAndRefused`; `SwitchOffIdentity`, pinned before the build as `DialOffDigestTests` was; `EaterBesideALeafFeelsItOneFarAwayDoesNot`; `FeelIsTheGatesMeasure` (fails if the held feel and the contact's ratio come from different poses); `RebuiltBodyAndNewbornFeltBeforeTheirFirstBlock`; `FeelOnResumeEqualsTheUnbrokenRun` (fails if held state needed a checkpoint); `AtReachThreeASideBrushWithinThreeRadiiLands`; `CostMeasuredAndNothingWhenOff`, reporting µs a mouth; `RowIsTheRowItWasWithSwitchesOff`.
- `src/Evosim.Farm.Tests/GpuMouthFeelTests.cs`, in double, run without `GpuMeanTests`: `FeelOnTheCpuAcceleratorIsTheSolver(nose, central)` after `GpuFarSmellTests.cs:153`, `FeelOffHoldsAStubAndUploadsNothing` after :264, `FeelLinkInAFeelOffWorldReadsZeroOnBoth`.

## 8. Risks, and what the first screen shows

- The feel is up to 0.5 s stale, in which a jointed grazer at r66-M's 0.16 m/s moves one or two radii. Readers' landed share against non-readers' shows it.
- Mutation finds the feel too slowly, or the graft's random signs mostly hurt. `mouthFeelReaders` and the landed share then stay flat.
- The wide reach removes the pressure to aim. `mouthBlowsWide`'s share of landed blows then does not fall.
- The reach and the feel help every mouth, so grazer-on-grazer bites, which r66-M cut four- to seventeenfold, may rise again (D225's risk). The kill counts and `biteKeptOthers` against `biteKeptLeaves` show it.
- Crowded grazers hold each other's feel at 1, which says nothing of food. `mouthFeelNear` then stays high while the leaf share is flat.
- A thin mouth feels a shorter range in metres, which favours thick mouths. No row measures it (inference).
- A dense oasis gives a mouth many candidates, which `pace-check.py` and `wallHarnessFeelMs` show.
- At k = 3 films show food taken short of the mouth again. D280 accepts that as the overshoot.

## 9. The coordinator's amendments

- **The reach widens the gate on touching pairs and makes no bite at a distance.** The coordinator reads D280's reach as
  D275's gate made wider, since D275 gates a contact's blow and the owner's option 4 widened that same test from one mouth
  radius to three. A body must still touch the other with some part, as now, and the mouth must lie within k radii of the
  bitten part. A bite across open water would need a tongue the world does not have. The owner is told this reading and may
  overrule it.
- **The graft is a starting point, and the nursery does the wiring.** A graft of random sign helps about as often as it
  hurts. The first use of the feel is therefore a nursery retrain under D280's rules, in the oasis tank of
  `rounds/nursery/leaf-direction-mouth-oasis.json` with the feel on and k = 3, starting from the oasis arm's tops of
  2026-11-02 with the graft added. Selection there keeps the feel links that help. The world screen then founds with those
  retrained tops. The graft is still built, as specified, because the retrain needs it.
- **Build order.** Core and Dynamics first: the tunables, the channel, `MouthFeel.cs`, the reach dial, the graft and their
  tests. The farm's hold and rows next. The card last, behind its own proof. Each stage ends on its filtered tests, and
  the coordinator runs the default suites.
- **The range stays at 8 radii for the first screen**, as the overshoot. The feel counts any other body, as D280 rules.
  `mouthFeelNear` against the leaf share will say whether a food-only feel is needed later.
- **The theatre's mouth-touch verdict reads k** before any film of a k = 3 world is called a bite film. That is c8's, after
  the farm side lands.
