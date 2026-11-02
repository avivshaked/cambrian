# D283's build spec: where a mouth sits is heritable

*An Opus design read for the coordinator, 2026-11-02, read-only. Lines are on `mouth-feel` at `f9d566b9` (main `e81fcd08` for files it leaves alone), and the theatre's on `theatre-look-merged`. Build on `mouth-feel`, whose `MouthFeel.cs` is a reader. Inference is marked.*

## 1. The gene's form

- **One direction a node.** `MorphNode.Mouth` is a `Float3?`, a unit vector in the node's own frame. The mouth sits where the ray from the part's origin along it leaves the true surface. Every part grown from the node shares it. Every node carries it and only eating parts read it, as `MaxModules` is carried (`MorphNode.cs:134-136`).
- **Per shape.** `h` is the part's own half-extents, so scale is carried:
  - box: `t = min over u_k ≠ 0 of h_k/|u_k|`, and the point is `t·u`.
  - sphere: `u·SphereShape.Radius(h)`.
  - capsule: the ray met in closed form against the cylinder, or else the cap. Along ±y it is exactly `(0, ±(HalfSpan + Radius), 0)`.
  - family part (D233): along an axis it is `AxisReach` (`Superellipsoid.cs:1230`). Otherwise a 64-step bisection of `InsideFunction` (:140) on `[0, Reach(h)]` (:468), in double, runs once at development. The result is stored as `PhenotypePart.MouthReach`, which `Phenotype.Scaled` multiplies with the half-extents (D255's rule, `Developer.cs:316`). That the ray crosses each form once is inference, and a test checks it.
  - `Scaled` and `WithoutSubtrees` carry both fields.
- **Mirror.** The developer stores the part's direction flipped on x exactly where it flips `ChildAnchorLocal` (`Developer.cs:307-309`). A reflected twin's mouth is then its original's mirror image, and an unreflected twin stacks, as now.
- **Face, radius and normal.** A placed mouth's face axis is the `k` maximising `|p_k|/R_k`, x before y before z. `R_k` is the part's reach along ±e_k: `h_k`, the capsule's `Radius` or `HalfSpan + Radius`, or `|AxisReach|`.
  - The radius stays D275's: half the smaller of the other two half-extents, or a sphere's smallest. It is constant across a face, steps at an edge, and equals today's at every face centre.
  - A new `MouthPoint.Normal` gives the unit outward normal at `Local`, for placed and unplaced mouths alike: ±e_k on a box, `p/|p|` on a sphere, `(p − spine)/r` on a capsule, and on a family part the gradient that `SurfacePointAndNormal` takes (:449-455; exposing it at a point is inference).
- **One function.** `MouthPoint.Tip` is today's `Local` text, moved unchanged. `Local` returns the place for an eating part that carries one, and `Tip` otherwise; `Radius` keeps today's branch for an unplaced part. Nose tips call `Tip` (`CreatureSenses.cs:416`, :679; `NoseGraft.cs:219`), so a nose never follows the gene.

A joint anchor's cube map is not used: it clamps a box anchor inside the box (`StandardShapes.cs:42-46`), and on a family part it misses the axis through the centre of mass where today's mouth sits (inference). The ray gives every rule mouth back bit for bit.

## 2. The off path and the migration

- **Recommended: an extension, written only into a genome that has it.**
  - A genome carries places on every node or on none (`Genome.Validate`).
  - One without them is written as formats 11 to 14, byte for byte as now.
  - One with them is written as format 15 (old model) or 16 (one brain): format 13 or 14 plus `"mouth":[x,y,z]` after `buoyancyOffset` on every node (`GenomeJson.cs:129-155`, :511, :558).
  - A 15 or 16 node missing the key is refused, and so is an 11 to 14 node holding it.
  - Writing places into every genome would change every lineage row and checkpoint written with the switch off (`WorldState.cs:476`, :1357), and put development inside `GenomeJson.Read`.
- **The switch decides which genomes a world takes.**
  - Off, founding refuses a genome holding places. No part then holds one, every reader runs today's text, and nothing is drawn, so nothing drifts.
  - On, founding refuses a genome without places and names the write-out verb. It also refuses a world where neither `BiteNeedsMouth` nor `IntakeReachFromMouth` is on, since nothing would select a mouth's place (the buoyancy offset's rule, `MorphNode.cs:193-196`).
- **The write-out.** `MouthPlaces.WriteOut(genome, limits, shapes, cellTypes)` develops the adult under the target world's limits.
  - Each node takes its lowest-indexed part's rule direction, flipped back for a reflected part: ±e_k on a box, a capsule or a family part, and `d.Normalized` on a sphere. The root therefore keeps the face opposite its first child, and later edge mutations no longer move it.
  - A node grown at more than one rule direction is a *split node*, and the verb counts and lists it. An unreached node takes its +longest axis.
  - The nursery verb `--write-mouths <pool> --recipe <r>` writes `inocula/<pool>-mouths.json`, with a TSV of every eating node's direction and the split count, as `--graft-mouth-feel` writes its pool.
  - `inocula/`, the nursery's pools and recorded runs read as now.

## 3. Mutation and founders

- **Its own stream.** `MouthStream` ("MOUT") sits beside `BrainStream` (`World.cs:7163`), always `Rng.SeedFor(s, MouthStream)` of the seed `s` that `Mutate` or the founder is handed. `Conceive` passes it to `Mutator.Mutate` (:6657) with the switch on and null with it off, and a random founder draws from it after `GenomeFactory.Founder` (:7181). Every other gene, the height and the brain draw exactly as off.
- **In `Mutate`**, after the prune (`Mutator.cs:144`) and before `Validate` (:160):
  - a node without a place, which is `AddNode`'s (:770), draws one; a bud copies its source's, since `Bud` clones it (:595).
  - each eating node then turns with chance `MouthPlaceChance`: a tangent Gaussian of `MouthPlaceStepRadians` an axis, whose length is the angle, then renormalised.
  - a placed parent mutated without a stream throws.
- **D262's start.** The chance is 0.5 an eating node a birth, and the step is 0.5 rad, a mean turn of 36°. Unselected, a place drifts 90° in about 10 births and 180° in about 40 (inference, from `2n·0.5² = θ²` turns at one turn in two births).
- **The uniform draw** takes a point `p` on the node's own surface at `h = Dimensions`, then `u = p/|p|`. A box picks a face by area and a uniform point on it, a sphere a normalised Gaussian triple, a capsule its cylinder or a cap by area, and a family part a reference panel's centre by area (`AddPanels`, resolution 8). It is uniform on a developed part only where edge scales keep the proportions (inference).
- **Crossover** (`src/Evosim.Nursery/Crossover.cs:145`) takes the base's body whole by `Clone`, places included, and draws nothing more. `MorphNode.Clone` copies the place.

## 4. Every reader

| Reader | Change |
|---|---|
| `MouthTouch.cs:120-126`, `MouthFeel.cs:210-215`, `Metabolise.cs:432` | none: they read through `Local` and `Radius` |
| `CreatureSenses.cs:416`, :679, `NoseGraft.cs:219` | `Tip`, bit-identical |
| `NoseGraft` on a placed genome | the nose node takes its rule direction |
| the card | none: no kernel computes a mouth. The gate, the feel and D214's points run on the host from `solver.Phenotype` (`logbook/specs/bite-needs-mouth-spec.md:58-67`), and the card holds copies such as `FeelHeld` |
| `TheatreMouth2.Measure` (:139-176) | calls `MouthPoint.Radius`, not its copy. The disc axis (:246) and depth cap take `Normal`; `Point.normalized` is the normal only at a face centre |
| mouth shots (`TheatreSoloStill.cs:397`, :439; `SafariPlans.cs:668`) | outward axis `Normal`; nose shots (`NurseryEpisodeWorld.cs:1361`) take `Tip`. The theatre lane's, after the farm side |

## 5. The instrument

- **The measure.**
  - A body *leads* when, over its eating parts, the largest `n̂·v̂` is at least cos 45°. `n̂` is `Normal` turned by the link's rotation, and `v̂` is the body's centre-of-mass velocity. Only samples above 0.05 m/s count, and remains never do.
  - Three shares of moving body-samples: *now*, today's mouths; *chance*, the area share of the eating parts' panels (`AddPanels`) whose normal lies within 45° of `v̂`, what a random founder leads; *best*, the share within 45° of each body's mean travel direction in its eating part's frame (with its resultant length), what the best fixed place leads.
  - *Best* less *now* is the gene's room. *Best* near *chance* means the bodies tumble.
- **Where.** One Core function, `MouthLead.Of(phenotype, linkPos, linkRot, v)`, serves two read-only verbs that write TSV. The farm's `--mouth-lead` runs beside the film window's resume (`FilmWindow.cs`; the hook is inference) on the solver's poses and `Velocity` (`Creature.cs:245`). The nursery's `--mouth-lead <tank folder>` reads `--restep`'s `frames.bin` and takes `v` by finite difference. `scripts/reads/mouth-lead.py` sums them.
- **Read it** before any screen: on r66-M's 6202 and 6203 over 1,200 s from 22,500 s (a resume, the coordinator's farm run), and on the oasis tank's tops of 2026-11-02.

## 6. Switches and tunables

| Tunable | Group | Default | Variable | Refused |
|---|---|---|---|---|
| `HeritableMouth` | mouth | off | `EVOSIM_HERITABLE_MOUTH` | on with no mouth rule (§2) |
| `MouthPlaceChance` | mutation | 0.5 | `EVOSIM_MOUTH_PLACE_CHANCE` | unless in [0, 1] |
| `MouthPlaceStepRadians` | mutation | 0.5 | `EVOSIM_MOUTH_PLACE_STEP` | unless finite and in (0, π] |

- With the switch on, the header gains `· heritable mouth 0.5 at 0.5 rad`, and the row gains `mouthOffFace` (eating parts off their rule face) and `mouthAngleMean` (degrees from the rule direction).
- They move the two Core config fixtures' keys and hashes, `EnvBindingTests`' hash, `ReportTests`' `Replace` and `EndsWith` lines, the live `rounds/nursery/configs/` (`scripts/nursery-config-forward.py`) and `WorldConfigFileTests`, and the crowd fixture is re-recorded. The format step moves only `GenomeJsonTests`' messages. `DialOffDigestTests.PinnedDigest`, `BiteRebuildTests` and `FilmWindowDigestTests` must not move.

## 7. The nursery

- **(a) The search.** `search.evolveMouth` is optional, as `stop` is (`Recipe.cs:519-522`).
  - Absent is off: every recipe reads, hashes and reruns as now.
  - Present, it is `{"scatter": s}`, refused unless `evolve` is `brain` (`kind` turns places through `Mutate`) and the episode config has `HeritableMouth` on, as `evolveEnvelope` is checked (`Program.cs:233`).
  - `Child` and `ChildCentral` (`Program.cs:735-783`) keep the parent's body by `Graft`, then turn the child's eating places with `MouthPlaces.Step`. Its stream is `MouthStream` of a seed already drawn, so the tank's own stream never moves. Every other body gene stays fixed.
  - The three `Mutator.Mutate` calls (`Program.cs:747`, :771, `Tank.cs:775-801`) pass the mouth stream whenever the config's switch is on.
- **(b) Generation 0 is scattered.** A share `s` of each body's members, 0.5 recommended, draws its places uniformly (§3) from a stream seeded by the recipe's seed and the member; the rest keep the written-out place. Unselected at the strong step, the next face takes about 10 generations and the opposite face about 40 (§3), against the oasis arm's 19 to 27. The kept half keeps the brains trained on the rule's mouth in the race.
- **(c) The exam** gains a control of kind `planMouth` (`ExamBrain`, `Tank.cs:1062-1074`). It is the genome with every place written out again from its own body, and its brain unchanged. *Own* less *planMouth* is what the moved mouth is worth to the brain that evolved with it.
- **(d) The rows** gain, only with `evolveMouth` on, as the envelope's do (`Tank.cs:663-670`), `"mouth":{"angle","angleMax","offFace","topAngle","lead","topLead"}`. The angles are degrees between a member's eating place and its body's rule direction; `offFace` is the share on another face; `lead` is §5's *now* share, gathered in each episode at its sample step (that the arena holds poses there is inference). The exam's rows give `lead` for each control.
- **Order.** The nursery runs on the host alone and the card needs no port (§4), so Core, the nursery and their proofs land before any card sitting, which gates only the world screens.

## 8. Proofs, in order

1. `./scripts/core-test.ps1`, then Dynamics, Farm and Nursery through `scripts/card/core-offcard-test.ps1` with `FullyQualifiedName!~GpuMeanTests`. `-All` before the commit.
2. `--write-mouths` on `inocula/r59-direction-tops/` and the oasis tops reports the split counts; on every eating part of an unsplit node, `Local` and `Radius` equal `Tip` and today's radius bit for bit.
3. The oasis recipe in brain mode, switch on, written-out pool, no `evolveMouth`, equals the recipe off row for row. Then a two-generation smoke with `evolveMouth`.
4. `TheGeneratedKernelsAreTheTemplatesOutput` passes unchanged.
5. Switch off on the card: `card-group.ps1` at group 32, `rounds/env-r66-m.ps1` on 6202 and 6203, 1,200 s; `python -I scripts/reads/compare-run-rows.py --from 0` against r66-M's runs finds every row equal.
6. Switch on: r66-M from the written-out pool at groups 32 and 64; `gpu-accept.py` exits 0 and the resumed group 32 equals group 64 (`rounds/identity-r66.ps1`'s shape).
7. The crowd fixture, re-recorded off, equals its run (the coordinator's).

## 9. Tests to write

- **`src/Evosim.Core.Tests/MouthPlaceTests.cs`**: `AxisPlacesAreTheTipBitForBit` (box, capsule, family, sphere; fails on any bit), `RayLandsOnTheTrueSurface` (inside function within 1e-6 of 1), `FamilyRayCrossesOnceOverTheFormRanges`, `ScaledPlaceScales`, `ReflectedTwinIsTheMirrorImage`, `UnreflectedTwinStacks`, `RadiusConstantAcrossAFaceAndTodaysAtItsCentre`, `NormalIsUnitAndOutward`, `NoseTipIgnoresThePlace`.
- **`GenomeJsonTests`**: `PlacelessGenomeKeepsItsTextAndFormat`, `FormatFifteenAndSixteenRoundTrip`, `RefusesAPlaceInElevenToFourteenAndAMissingOneInFifteen`.
- **`MouthPlaceMutationTests`**: `OffDrawsNothing` (a digest of 1,000 children pinned before the build), `OnLeavesEveryOtherGeneAsOff`, `NewNodeDrawsAndBudCopies`, `TurnMatchesTheStep`, `FounderDrawIsAreaUniform` (face counts within 3σ), `PlacedParentWithoutStreamThrows`.
- **`MouthPlaceWriteOutTests`**: `SplitNodeCounted`, `RootTakesItsFirstChild`.
- **World founding**: `OffRefusesPlacesOnRefusesNone`, `OnRefusedWithoutAMouthRule` (each fails on a missing refusal or one not naming D283).
- **`MouthLeadTests`**: `ForwardMouthLeadsSideMouthDoesNot`, `ChanceIsTheAreaShare` (a cube head-on reads 1/6), `StillBodyNotCounted`.
- **`src/Evosim.Farm.Tests/HeritableMouthFarmTests.cs`**: `SwitchOffIdentity` (pinned before the build), `PlacedMouthBitesWhereItSits` (a side mouth on a leaf lands, the rule mouth is refused), `ResumeEqualsUnbroken`, `FeelReadsThePlace`. `GpuHeritableMouthTests`, in double: `PlacedWorldOnTheCpuAcceleratorIsTheSolver`.
- **Nursery**: `RecipeWithoutEvolveMouthHashesAsBefore`, `EvolveMouthRefusedWithoutBrainOrSwitch`, `ScatterShareAndSeed`, `PlanMouthControlRestoresTheTip`, `RowsCarryMouthOnlyWhenOn`.

## 10. Risks, and what the first screen shows

- **Broad faces have twice the radius** (0.075 m against 0.0375 m on the direction box, after `mouth-feel-spec.md:12`'s inference), so places may gather there for reach and feel range rather than to lead. `offFace` by face against `lead` shows it.
- **A moved mouth breaks a trained aim**, and the scattered half dies before the brain adapts. The scattered members alive at generation 2, and `planMouth`, show it.
- **Split nodes move mouths at the write-out.** The verb's count shows it before founding.
- **The bodies tumble**, *best* is near *chance*, and the gene has nothing to find. The instrument shows it first.
- **The step is too strong**: `angle`'s spread keeps widening while `lead` stays flat. The chance is dialled back.
- **The family bisection slows development**, which `pace-check.py` shows.

## 11. The coordinator's amendments

*Added on 2026-11-02, after the read. Where they differ from the sections above, they rule.*

- **A placed mouth's size does not depend on where it sits.** Section 1 keeps D275's radius, half the smaller half-extent of
  the face the mouth lies on, so a mouth moved onto a broad face doubles its reach (section 10's first risk). Selection
  could then move mouths for size rather than to lead, which would confound the question D283 asks. So a placed mouth's
  radius is half the part's smallest half-extent, and a sphere's is as now. An unplaced mouth keeps D275's radius
  exactly, so every switch-off world is unchanged. On the direction tops' box (half-extents 0.15, 0.075 and 0.15 m), the
  rule's mouth sits on the z face, whose smaller half-extent is the box's smallest. Both radii are 0.0375 m there, and
  section 8's third proof still holds bit for bit. On a body whose rule mouth sits on a broad face, the switch on with the
  written-out places changes that mouth's radius, which is the amendment's intent. The owner is told and may overrule it.
  `RadiusConstantAcrossAFaceAndTodaysAtItsCentre` becomes `PlacedRadiusIsTheSmallestHalfExtentAnywhere` and
  `UnplacedRadiusIsTodays`.
- **The build has its own branch.** `mouth-gene`, off `mouth-feel` at `5fdf4b12`, in `scratch/wt-mouth-gene`, because
  D280's card stage is being built on `mouth-feel` meanwhile. Core, the farm's switch and rows, and the nursery come first.
  The theatre's readers (section 4) are c8's, after the farm side lands. The card needs only its identity sittings.
- **The retrain founds from the written-out oasis tops**, with the feel grafted (`14db7dfca9a9d350`), `HeritableMouth` on and
  `evolveMouth` at a scatter of 0.5 (D284). Its predictions are rewritten before it runs.
- **The instrument's tank read comes first and gates nothing.** The nursery already evolves the place (D284), so the
  instrument says how much room the gene has. The farm's resume read of r66-M is the coordinator's farm run, later.
