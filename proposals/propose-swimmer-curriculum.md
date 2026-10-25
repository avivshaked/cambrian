# Proposal: a nursery where travel between patches pays

*A draft for the owner, written at the coordinator's request on 2026-10-25. It follows D229, which put a nursery curriculum between the fin law and the moving-oasis screen. Each claim about the code names its file. Anything I reached by reasoning rather than by reading or measuring is marked as inference. Nothing was built or run for this draft.*

## The proposal in one paragraph

The nursery is a small simulated tank where swimmers are bred on their eating before they are planted into a world. Its patchy tank showed that its swimmers find a leaf by smell and do not hold to a patch, because drift brings any body back to food about as well as a brain does. I propose a tank where food moves. Two patches of living leaves take turns being fed. In the fed patch, a leaf that is eaten grows back. When the feeding switches, the other patch's leaves wither, and the new fed patch opens at a place the swimmer cannot smell from where it stands. A swimmer that stays where it ate goes hungry. One that leaves and searches finds the new patch while it is full.

The curriculum has two steps, called rungs. The first is in today's 3,400 m² tank. The second is in a larger tank, where the gap between patches is set so that drift alone misses the new patch. The score stays the energy return. Travel is measured at the exam but never scored. The founders are the ten swimmers that round 59 planted, plus the same ten with their round limb swapped for a flat blade, all under the fin law. The winners go into the moving-oasis screen beside a cold start and beside the untrained founders.

## What the nursery does today

A nursery run is a recipe file in `rounds/nursery/`, and the program is `src/Evosim.Nursery/`. A recipe names a world config with overrides, the founding bodies, the episode, the search, the reward and the exam. The run that matters here is `leaf-direction-patches` (`rounds/nursery/leaf-direction-patches.json`, registry entry 20). It is set up as follows:

- **An episode** lasts 1,800 simulated seconds at a 0.01 s step, with births held and the population floor closed. Eight bred bodies share a tank and start anywhere in it, 0.5 to 1.5 times 11.5 m above the bed. Each is given its grown body plus 3,830 s of its standing upkeep, so a body that eats nothing still lives to the end (`Tank.cs`, the purse rule `grown`).
- **The food** is 31 living leaves drawn from round 58's leaves (`rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz`). They are laid once, at the start, from the episode seed's own leaf stream, and nothing replaces a leaf that dies (`LeafTank.cs`, `LayLeaves`). The leaves give off their own smell (D167), here the direction form with a 6 m reach (D174). The age rule is off in the tank (D169).
- **The patches** (D182, D184) are three discs of radius 6 m on a ring of radius 21 m. The ring turns by a random angle each episode, and the 31 leaves are dealt into the discs largest first (`LeafTank.cs`, `LeafPatches.Centre`). The patch setting is optional, so older recipes keep their hashes.
- **The search** has a population of 48 genomes. Each generation swims every genome in 8 draws, keeps 8 elites and picks parents by a tournament of 3. A recipe with `evolve: kind` mutates bodies as well as brains (D178). The search stops when eaten energy, smoothed over 5 generations, has gained no more than 5% in 10 generations. It never stops before generation 20 and always stops at 60 (D172, `SearchEnd.cs`).
- **The score** is the energy return (D179, `Reward.cs`, `EnergyReturn`). That is the joules a body's mouth kept, divided by what it spent on upkeep, on the stroke's work and on handling. A dead body is charged its upkeep to the episode's end. A body that breaks even scores 1, and an idle one scores 0.
- **The exam** swims the top genome of every generation and the last elites on 32 held-out episodes, each under its own brain, with no brain, and with the leaf smell knocked out (D172). The recipe also offers a frozen brain (every power at −1, so no stroke), constant full power, a constant power, and a hand-wired kinesis (`Tank.cs`, `ExamBrain`).

What the patch arm's swimmers learned is in logbook/0146 and `logbook/specs/nursery-patches-read.txt`. The search stopped after generation 20. On held-out episodes the 23 examined genomes ate 83 J an episode under their own brains, 9 J with the smell knocked out and 3 J with no brain. So the smell carries about nine-tenths of their eating. The bodies grew cheap. Their spending fell from 1,063 J to 137 J an episode, and the best genomes return 1.24 and 0.98 times what they spend.

They do not hold to patches, though. The table below comes from re-stepping every exam tank under the patch watcher (`PatchWatcher.cs`, `scripts/reads/patch-read.py`). Look at the first column: a body with no brain spends nearly as much time near leaves as an evolved one.

| Brain | Time within smell of a leaf | Exits that turn back within 30 s | Crossings to another patch, per body |
|---|---|---|---|
| Evolved, own | 0.44 | 0.48 | 2.4 |
| Evolved, smell knocked out | 0.43 | 0.50 | 3.0 |
| Founders, own | 0.41 | 0.48 | 3.2 |
| No brain | 0.40 | 0.51 | 2.6 |

Every body that started in bare water found a smell, at a median of 209 to 226 s under every brain. Bodies also cross between patches two or three times an episode under every brain. A body with no brain still strokes, because D135 reads a silent brain output as half power. My reading, as in logbook/0146, is that drift and an unsteered stroke do the travelling in this tank, and the smell's value lies in the last few metres onto a leaf. The repeat on a second recipe seed, `leaf-direction-patches-s2-w5`, was held by the coordinator and never ran, so this result rests on one recipe seed.

## The code has moved on since the patch arm ran

Four changes since 2026-10-15 decide what the curriculum must build before anything swims.

1. **Every nursery config is refused today.** The nine configs in `rounds/nursery/configs/` were last committed on 2026-10-18. None carries the tunables added since, among them contact eating (D219-D221), the fin law and the mirrored joint. Loading refuses a config with a missing field (`src/Evosim.Core/Serialization/RunConfigJson.cs`), so a current build refuses every nursery recipe. I read this from the loader and the files. I did not run it.
2. **Under contact eating the score reads zero.** The tank's eaten measure is the body's `MouthJoules` (`Tank.cs`), which only the corpse path adds to (`WorldMouth.cs`). Under contact eating a bite's meal goes to `BiteKeptJoules` instead (`WorldContact.cs`, `SettleBites`). That counter is kept per body but does not say what was bitten. Today the tank keeps its score to grazing through D169's rule that only a bitten leaf leaves a corpse. Under contact eating there are no corpses, so the rule has nothing to act on, and a swimmer that bites another swimmer would be fed (inference from the code).
3. **The fin law is built and benched, and D231 is not.** The fin law is the `ShapedAddedMass` switch (`src/Evosim.Core/Environment/FluidConfig.cs`), and it is on main with its card port. D231's assembly push sits on branch `stroke-assembly` as a host build its commit calls untested. The fin bench in `logbook/specs/d228-d229-build/README.md` is the evidence that a broad fin pays. With the law on, a flat paddle travels 4.70 m on 1.97 J, while a stick of equal volume takes 6.29 J to travel 4.14 m.
4. **The nursery can switch these rules on.** It steps the farm's own simulation and references the dynamics project (`Evosim.Nursery.csproj`). A recipe sets any tunable path through its overrides (`Recipe.cs`, as with `Fluid.SpinDragCoefficient`). So the fin law, the mirrored joint and contact eating need only a config, not new nursery code. The card cannot run the nursery's world, so all of this runs on the CPU.

## A tank where travel pays

### Why the patches must move

There are two ways to make staying put lose. In the first, every patch thins as it is eaten and grows back slowly, so a crowd in one patch eats it down while an empty patch fills. In the second, the patches take turns: one is fed, and the others wither. I recommend the second, the nursery's version of the moving fountains in the draft proposal (`proposals/propose-doing-must-pay.md`). Under that draft, an island's fountain stops and another opens elsewhere. The quiet island's leaves live on light and cannot breed, and they die off. A grazer that stays dies with its oasis. The switching tank asks the same thing of a swimmer in thirty minutes.

Thinning alone is weaker in a tank of eight. Bodies share patches, so whether staying loses depends on how many others came. The patch arm also showed that drift spreads bodies across patches anyway (inference from the table above). In the switching tank the tank decides where food will be, so the signal does not depend on the crowd.

### The rules of the switching tank

These are draft values. Each one is a recipe field, and the calibration bench below checks them before any arm runs.

- **Two patches**, the owner's word in D229. Each is a disc of radius 6 m holding 15 or 16 of D173's 31 leaves, so the total food at the start equals today's tank. A patch is therefore about 1.5 times as dense as D184's. The glass margin stays at 6 m, one smell reach.
- **One patch is fed at a time.** The fed patch switches every 600 s, at 600 s and 1,200 s in an 1,800 s episode. Which patch is fed first is drawn per episode.
- **The new patch opens somewhere new.** At each switch the patch that becomes fed is laid fresh, at an angle on the ring at least 120° from the old one, drawn from the leaf stream. Its centre is then 36 to 42 m from the old centre at rung 1. A course held straight from the old patch therefore does not always find it, so the swimmer must search, as a migrant across a desert would. D184 rejected random centres because they add noise. Here the randomness is the point, and the 8 draws a generation average it (inference).
- **Eaten leaves grow back in the fed patch.** A leaf killed there is replaced after 120 s by a new leaf drawn from the pool, laid grown at a random point in the patch. In a withering patch nothing grows back.
- **The old patch withers.** After a switch, each leaf of the old patch dies at a random second within 300 s and leaves no food, the tank's version of a quiet island's die-off.
- **The episode stays 1,800 s**, so its cost compares with the patch arm's.

### Staying must lose and travelling must win

Three conditions make the tank ask for travel. Each can be read on the calibration bench.

1. *Staying loses.* After a switch a stayer keeps eating for at most the 300 s withering window, from a thinning patch. A traveller that arrives after a crossing time `t` eats for the rest of the 600 s period, in a patch kept full by regrowth. If both eat at the same rate inside a stocked patch, travel wins whenever `600 − t` exceeds about half the window, that is whenever `t` is under about 450 s. This is a rough balance, not a model.
2. *A directed swimmer can cross in time.* The patch arm's founders covered 214 to 258 m of path in an episode, about 0.12 to 0.14 m/s (pace benches, registry entries 18 and 19). Rung 1's patches lie 30 m apart edge to edge, two to four minutes at that speed. The fin law should make these founders faster. Their limb is a capsule 0.52 m long and 0.16 m thick (`rounds/nursery/pools/README.md`). By D229's formulas its added mass rises about 1.8 times, which under quadratic drag means about 1.3 times the speed. That is my arithmetic from the formulas, not a measurement. Round 62's fastest movers swam 10 to 17 cm/s under their own brains in the swim-ceiling bench (`scratch/swim-ceiling/out/`, in scratch and not yet in the record).
3. *Drift must fail.* If bodies with no brain or with the stroke frozen reach the new patch about as quickly as evolved ones, the tank does not ask for travel. The rule for rung 2's size is that drift's median time to the new fed patch exceeds one period, 600 s, while a body cruising under its own brain could cross in under half of one.

### The two rungs

| Rung | Tank | Ring | Gap between patch edges | Patch to glass |
|---|---|---|---|---|
| 1 | 3,400 m², radius 32.9 m (D184's) | 21 m, patches 180° apart at the start | 30 m | 5.9 m |
| 2 (default) | 5,540 m², radius 42 m | 30 m | 48 m | 6 m |
| Ceiling | 12,500 m², radius 63 m | 51 m | 90 m, D204's desert | 6 m |

Rung 2's gap is set after rung 1's controls, by the drift rule above, so the 48 m default may change. A third rung at the world's 90 m is not planned. The screen tells us whether it is needed. The patch arm's pace bench found a 3,400 m² episode costs 3.05 times a 400 m² one. A straight line through those two points puts rung 2 at about 1.5 times rung 1 per episode and the ceiling at about 3 times (inference from two points).

### The food

The food is living leaves with their own smell, as D167 asked, and nothing else to eat. I recommend the direction form at 6 m that the worlds carry (D174), and the whole-body smell, not D235's nose, since no world carries the nose yet. The eating rule should be the one the moving-oasis screen will carry. Today that is contact eating at round 62's dials, plus D232's gut once it is built and screened. Under contact eating a grazed leaf that survives heals from its reserve, so a leaf can grow back between bites without a tank rule (D221). How much of that happens within an episode is unmeasured, and the bench will read it.

## The swimmer's body

The founders are the ten tops that round 59 planted (`inocula/r59-direction-tops/`, the patch arm's pool `rounds/nursery/pools/410a70db44a4b20a.tsv`). They eat the most of any examined nursery genomes, 409 J an episode against the patch arm's 83 J. They also lean on the smell, which cost the direction arm 35% of its eating when knocked out (D173's read). The patch arm's cheap foragers are left out. They stroke at about a tenth of the tops' rate and eat a fifth as much. Their stroke was tuned to the old drive, which the fin law changes (inference).

The switches go on in the tank's config:

- **The fin law**, on.
- **D231's assembly push**, on once its build is tested and benched. It changes nothing for a founder whose one link carries nothing beyond it. It matters as soon as a descendant grows a part past its link.
- **The mirrored joint (D228)**, on. No founder has a mirrored part, so it acts only on descendants that grow a pair. D234's mirrored drive follows whatever the owner rules after the film.
- **Bodies evolve** (`evolve: kind`), so a fin can widen under the fin law. The founders' limb is a capsule, and a capsule cannot go flat. The mutation would first have to change its shape to a box (inference from the shape genes).

I recommend seeding a flat blade. Ten of the twenty founders are the ten tops with the capsule replaced by a flat box limb of the same volume, inside round 58's link limit of 0.26 m half-length, hand-built as the original swimmer was (`inocula/leaf-tank/swimmer.json`). The pool mixes both kinds. Selection then decides whether the blade wins its place, as D233 requires of a new shape. The exam reads which founder each winner descends from, since the search keeps each member's parents (`Tank.cs`, `Member`).

## The score and the exam

The score stays the energy return of D179, with eating counted only from leaves. Travel is measured at the exam and never scored. A distance term would pay motion directly, the proxy that the nursery design of 2026-10-04 ruled out ("never distance"). The literature the owner commissioned warns of the same thing (`logbook/specs/nursery-curriculum-literature.md`, section 1, read at abstract level). In the switching tank, travel earns its keep through the food.

The exam keeps D172's form and adds two controls. Every genome swims under all five brains:

| Brain | What it tells us |
|---|---|
| Own | The result |
| No brain | Whether the brain steers the stroke |
| Smell knocked out | Whether the smell still finds the leaf |
| Frozen, every power at −1 | Whether the stroke pays at all: the stroke knockout |
| Constant full power, bend centred | Whether steering beats cruising straight |

The travel measures come from re-stepping the exam under an extended patch watcher, so the episode record's format does not change. They are:

- the median time from a switch to entering the new fed patch;
- the share of a body's eating taken in a patch that opened after the body's first patch;
- crossings per body, as today.

I propose that a swimmer counts as trained when all of the following hold on held-out episodes. The bar is fixed before launch.

1. The best examined genome returns at least 1, and the examined genomes' mean eating under their own brains is at least the founders' on the same episodes.
2. Under their own brains, the time from a switch to the new patch is shorter than with no brain and than frozen, by at least two standard errors.
3. The share of eating from newly opened patches is higher under their own brains than with no brain, by at least two standard errors.
4. Freezing the stroke costs at least half their eating.
5. Knocking out the smell costs at least two standard errors of eating.

Standard errors are taken by founder lineage, not over genomes. Step 16 of `logbook/specs/nursery-leaf-tank-plan.md` found close kin counted as independent. A transfer bench then swims the winners in D184's static tank, to check that they did not learn only this tank's clock.

## What carries into the moving-oasis screen

The inoculum is ten genomes, five from each of rung 2's two recipe seeds. Each is chosen by its exam on held-out episodes, never by its search score (D172). It is committed under `inocula/` with an origins file, as round 59's tops were.

The screen has three arms on the same two seeds (D180). Six worlds fill one card group, the pattern of round 62's screens.

- **Cold start**: the screen's world as configured, with its usual founding trickle.
- **Untrained plant**: the twenty curriculum founders, under the same switches, planted the same way.
- **Trained plant**: the inoculum, planted the same way.

The untrained arm separates the effect of training from the effect of planting any swimmer at all. Planting goes through the founding trickle's pool, as rounds 59 and 62 did. Where the trickle places its founders under moving fountains is decision 3 of the draft proposal, still open, and the same choice applies here. The world must carry the same eating rule and switches as the tank, or the swimmers meet a body plan and a meal they were not bred for.

## What needs building, and what it costs

| Item | Exists | To build | Agent time |
|---|---|---|---|
| The nursery config on today's format, with the curriculum's switches and the eating rule | the overrides, `Recipe.cs` | re-save the config with the new fields and re-record the nursery fixtures (`src/Evosim.Nursery.Tests/fixtures/`) | 3–5 h |
| Eating from leaves under contact eating | `BiteKeptJoules` per body; a world total from leaves (`WorldContact.cs`) | a per-body counter of food kept from leaves, an instrument that changes no config; eaten reads it, plus the gut once D232 is built | 3–4 h |
| Switching patches, regrowth, withering, the new patch's place | patches on a ring (`LeafTank.cs`) | optional recipe fields, absent by default; leaves laid mid-episode from the leaf stream; a tank-only death for withering, since the world has no public kill but `KillDiverged` (`World.cs`); `--check` printing each episode's schedule; a re-step proven bit-identical | 1–1.5 days |
| Travel measures | the patch watcher, which groups leaves within 12 m (`PatchWatcher.cs`) | patch identity from the tank's layout; time from a switch to arrival; eating by patch; a read script in `scripts/reads/` | half a day |
| Blade founders | the hand-built swimmer | ten genomes with the capsule swapped for a flat box of equal volume, with tests | 2–3 h |
| A leaf-smell kinesis control (optional) | a kinesis on the snow sense (`Tank.cs`, `GraftDriveKinesis`) | a leaf-smell mode | 1–2 h |

The total is three and a half to four and a half days of agent time. On the owner's rule, a Sonnet subagent builds each item and runs the fast filtered tests, and an Opus agent reviews it. Three gates sit outside this list. D231's host build must be tested and benched. D232's gut must be built and screened, since the eating rule waits on its read. And the moving-oasis screen's config must be settled. The tank build is not gated by any of them and can start now.

The machine time below is on the efficiency cores, one run at a time, booked through the coordinator's queue. The estimates scale the patch arm's own run, which did 20 generations and a three-brain exam in about 5.5 h on four to eight cores, about 1,900 genome-episodes an hour.

| Step | E-core hours |
|---|---|
| The founders' fin-law bench: two bench recipes, law off and on; own, full and frozen | under 1 |
| The calibration bench of rung 1's tank: twenty founders, five brains, 32 episodes | about 1 |
| Rung 1, to D172's stop (generation 20 to 60) with a five-brain exam | 6–18 |
| Rung 2 and its repeat, each at about 1.5 times rung 1 per episode | 18–55 |
| The transfer bench in D184's tank | about 1 |
| **Total** | **about 26–75** |

The CPU only, since the card refuses the nursery's world.

## The decisions for the owner

1. **The tank's eating rule.** I recommend contact eating at the dials the moving-oasis screen will carry, with D232's gut if its screen passes, the score counting food from leaves alone, and swimmers able to bite each other without it scoring. This means the arms wait for D232's read, while the build goes ahead. The alternative is the corpse rule now. It runs sooner, but it trains swimmers for an eating rule the worlds no longer use.
2. **The tank's form.** I recommend two patches that take turns being fed: a 600 s period, eaten leaves growing back after 120 s in the fed patch, the old patch withering over 300 s, and the new patch opened at least 120° away at random. The alternatives are patches that only thin and grow back, which a tank of eight makes weak, and D184's still patches, which we know do not ask for travel.
3. **The ladder.** I recommend rung 1 in D184's 3,400 m² tank with a 30 m gap, then rung 2 with its gap set so that drift misses the new patch within one period. The default is 48 m in 5,540 m². A rung at the world's 90 m is run only if the screen asks for it.
4. **The founders.** I recommend round 59's ten tops plus the same ten with a flat blade of equal volume, in one pool, so that selection chooses. The patch arm's cheap foragers are left out.
5. **The switches.** I recommend that the tank's config match the moving-oasis screen's: the fin law on, D231 on once benched, the mirrored joint on, and D234's drive as ruled after its film.
6. **The score.** I recommend that the energy return of D179 stay unchanged, with travel measured at the exam and never scored.
7. **The pass bar.** I recommend the five tests under "The score and the exam", fixed before launch, with standard errors taken by lineage, and the frozen-stroke and full-power controls added to every genome's exam.
8. **Repeats and the inoculum.** I recommend that rung 2 run on two recipe seeds and that the inoculum take five genomes from each. Rung 1 runs once.
9. **The screen's arms.** I recommend cold start, untrained plant and trained plant on two seeds: six worlds in one card group, planted through the trickle's pool. Where the trickle places them follows the owner's answer to decision 3 of the moving-fountains proposal.
10. **The order beside the nose.** I recommend that the curriculum carry today's whole-body smell, and that D235's nose test run in the nursery after it, possibly in rung 1's tank, since both want the same cores.
11. **The episode.** I recommend keeping 1,800 s, giving two switches an episode. A 2,400 s episode would give three, at a third more machine time.

## Sources

| Claim | File |
|---|---|
| The patch recipe, its search, stop, score and exam | `rounds/nursery/leaf-direction-patches.json` |
| Leaves laid once, patches on a ring, no regrowth | `src/Evosim.Nursery/LeafTank.cs` |
| The episode loop, eaten read from the mouth counter, path every half second, exam brains | `src/Evosim.Nursery/Tank.cs` |
| The energy return and its death charge | `src/Evosim.Nursery/Reward.cs` |
| Overrides by tunable path | `src/Evosim.Nursery/Recipe.cs` |
| The nursery steps the farm's simulation | `src/Evosim.Nursery/Evosim.Nursery.csproj` |
| Loading refuses a missing field | `src/Evosim.Core/Serialization/RunConfigJson.cs` |
| The nursery configs lack the new fields | `rounds/nursery/configs/*.json` |
| Contact eating's meal goes to the bite counter | `src/Evosim.Core/Ecosystem/WorldContact.cs` |
| The corpse path adds to the mouth counter | `src/Evosim.Core/Ecosystem/WorldMouth.cs` |
| The fin law's switch | `src/Evosim.Core/Environment/FluidConfig.cs` |
| The fin bench | `logbook/specs/d228-d229-build/README.md` |
| D231's untested host build | branch `stroke-assembly`, commit `bb3a6814` |
| The founders' body | `rounds/nursery/pools/README.md` |
| The patch arm's results and patch measures | `logbook/specs/nursery-patches-read.txt`, logbook/0146 |
| The pace bench and the patch arm's run time | `logbook/specs/nursery-leaf-tank-plan.md`, step 15 |
| Round 62's top movers in still water | `scratch/swim-ceiling/out/` (scratch, not committed) |
| The literature on curricula, at abstract level | `logbook/specs/nursery-curriculum-literature.md` |
| Rung 2's cost, the capsule's speed gain, drift's role | inference, as marked in the text |
