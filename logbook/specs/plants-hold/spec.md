# Plants hold the rock at the oases and grow as one body, each part fed where it stands

*Written on 2026-11-03 under the owner's delegation of that day (D291). The owner observed that every film shows the
plants as single leaves, asked why plants do not grow, and asked for it to be fixed. Drafted by an Opus agent from the
record, the code and round 66's world, and reviewed by the coordinator, who settled its three rules as D296 to D298. The
owner may overrule any of them. Rule 1's anchored founders reverse D165's rejection, which the coordinator flagged to
the owner.*

## The proposal in one paragraph

Nothing in today's world pays a plant to be more than one blade. A body earns its light and draws its matter at one
point, its centre of mass, so a stalk lifts nothing and a second blade drinks from the same stripped water as the first.
Uptake of matter held back 96% of plant steps in round 66's world, and light almost none. The rule that should grow and
regrow blades refused all but 136 of 42,651 attempts. Three rules follow, each behind a switch that is off by default.
A holdfast grips the rock at the oases, carried by founders as well as buds. Every part earns light and draws matter
where it stands, as the design already says a photosynthetic cell does. A bitten module frees its place, so the plant
can grow it again. A plant that holds an island's rock stays in the matter that falls there, can raise blades into
brighter water, and lives through a bite on a blade. The forests become the oases, and the deep water between them
stays open. Reads and a ledger screen come first, then the build, then six worlds on the card.

## 1. Plants float at mid-depth as single blades, and matter holds them back

The coordinator counted round 66's world, `r66-m-6203`, at 24,000 s (leaf pool `leaves-4452a00019df0811`). Of 5,311
living leaves, 1,336 genomes have one node, and none has more than two nodes above the 3 cm minimum. The root holds a
median 82% of a genome's node volume. 695 genomes carry an indeterminate node, D106's module gene, which lets a body add
parts while it is rich.

Four more facts come from the same pool and from the run's last stats row.

- **The leaves sit** 1.3 to 7.0 m deep, median 3.4 m. Light falls to 1/e every 6 m, so a leaf there gets 0.31 to 0.81 of
  the surface light, and 0.57 at the median.
- **Every leaf holds** its depth for free. D064 makes a body under 0.25 m³ neutrally buoyant. The pool's median root is
  0.064 m³, and the nodes of only 34 genomes sum above 0.25 m³. A newborn lands on a disc around its parent (D088), so a
  plant's depth is mostly inherited (inference).
- **Light binds little** in this world. In the window ending at 24,000 s, 96% of photosynthetic body-steps were held
  back by matter uptake rather than by light (`uptakeLimitedShare` 0.959). The crowd's shade took 2.8% of the light at
  its mean depth.
- **The module rule** hardly runs. Over the run it added 136 modules and dropped 86. It refused 42,515 additions, all
  for the body's shape and none for want of reserve.

The node count can mislead in two ways. A node is not a part. A node with a recursive limit above one develops into a
chain or a fan, so a one-node genome can be a body of many parts. A node can also develop into no part at all, cut by
the 16-part or 8-level limit or lost to a bite. And a node at the 3 cm minimum may be a bud that arrived at that size
(D119) as easily as a blade that evolution shrank. So the count does not show shrinking. A census of developed parts per
living plant, taken from the snapshot, would show it, and it is among the reads in section 3.

## 2. A blade, a stalk and a float cost little, and none earns from where it stands

The table prices each piece in round 66's world from the code and the run's config. The third column is the one to
read: nothing earns from its own height or its own water.

| Piece | What it costs | What it earns today |
|---|---|---|
| A second blade of the pool's median 0.064 m³ | 32 J of tissue at 500 J/m³; 0.19 W upkeep at 3 W/m³; under 0.01 W of support at 0.5 m from the root | its lit area times the body's one irradiance times 0.05, capped by matter drawn at the body's one point |
| The same blade as a separate child | the per-child overhead, at least 50 J, plus its tissue | the same, at a new place up to 30 m away |
| A 3 m structural stalk, 5 cm square | 3.75 J; 0.0075 W at 1 W/m³; about 0.03 W of support | nothing of its own, since structural tissue earns nothing and a link catches no light (D127); it raises the body's centre of mass, and so the whole body's light, by less than its length |
| A float | 2.5 W/m³, plus 0.05 W per lift unit per m³ | nothing on a body under 0.25 m³, where D064 sets to zero the excess density that lift multiplies |

A lit square metre at the surface earns 10 W of light before the day and the season. Matter caps that at
30 W × d/(d + 0.05), with d the dissolved matter in units per cubic metre. So matter binds wherever the water holds less
than a few hundredths of a unit. A blade's upkeep is about a fifth of the light a block of its volume earns at 3.4 m,
whichever body carries it. A grown blade is cheaper than a budded copy by at least the 50 J overhead. A stalk and its
support cost a few per cent of a blade's light. The price of parts is therefore not what keeps plants single.

## 3. Why plants stay single, cheapest test first

The ledger (`scripts/ledger.ps1`) prices one genome alone in seconds, at chosen depths, matter densities and exposures,
with no crowd, current or grazer. It can test hypotheses 2, 3 and 4 before any world runs.

1. **A body feeds** at one point. Light is read at the body's centre of mass and matter at the centre's cell. So a blade
   on a stalk earns at the centre's depth and drinks the centre's water. DESIGN.md §5A.1 says a photosynthetic cell
   earns at "its depth", so here the code departs from the specification. The code shows it, and no run is needed.
2. **Matter binds, so** height buys nothing. The plants sit at mid-depth, but more light pays little while uptake binds.
   The test is the ledger pricing the pool's median leaf at 1 to 10 m, at the matter densities round 66's field holds on
   an island and off it. If income stays flat with depth at island densities, height cannot pay until matter is also
   drawn at each part.
3. **A stalk costs** upkeep and catches no light (D127). The ledger prices the leaf with structural stalks of 1, 3 and
   5 m. A few per cent of income is expected, which would rule this out.
4. **Parts shade one** another within a body, through D099's hull cap and D110's pose. Between bodies, the shade is
   pooled over the whole 90,000 m² and took 2.8%, so the crowd is not the cause. The ledger prices a second blade beside
   the first and one above it, at exposures 1 and 2. The test is partial, because the ledger's cap is the orientation
   average rather than a pose.
5. **A child finds** new water and a blade does not. A second blade draws on its body's stripped cell. A child born up
   to 30 m away may land in unstripped water, or in the desert. The test reads round 66's positions and matter field for
   the density at each child's birth cell against its parent's. It takes minutes of reading, and the ledger cannot
   answer it.
6. **A lost part** stays lost, and a bite on the root takes everything. On a determinate node a lost part is gone for
   good (D106). On an indeterminate node the module rule regrows by count, and only up to the node's ceiling. So a plant
   that has used its count never regrows, and the shape test refused 99.7% of attempts in any case. A bite on the root
   of a two-blade body kills both blades, where two separate blades would lose one. Two reads test it. One is the share
   of plant kills that took the root, from the kill rows. The other is a probe resumed from a round 66 checkpoint that
   splits the shape refusals into their four causes; a cousin run is enough for a share of causes.

Hypotheses 1, 2 and 6 are expected to carry most of it and 3 to carry none (inference until the numbers print). Test 5
and the kill rows of test 6 were read on 2026-11-03 (`logbook/specs/kit-reads/`). A child lands in a median tenth of
its parent's matter, so hypothesis 5 fails. Bites are 4 to 12 % of plant deaths, so test 6's kill half carries little.

## 4. The rules

1. **A holdfast grips** the rock at the oases. The anchoring cell (D162, D165 and D268, on main and off in every world)
   joins the world's cell table and grips the bed and the reefs. In round 66's world the island coral is the only rock
   that rises toward the light. The cell starts overpowered (D262). Half the plant founders carry an anchoring root and
   are set touching island coral. Its bud weight is four times the other types'. Forming or releasing a grip costs about
   nothing, as D268 asks of its first screen. A 0.01 m³ holdfast holds about 2,700 N, against about 0.3 N of drag on a
   square metre of blade in the 0.02 m/s current (arithmetic). No plant is made to anchor (D268). If a census of coral
   surface by depth finds too little rock in lit water, D204's island shoals, ruled and never built, give the bed itself.
2. **Each part feeds** where it stands. A part's light is the irradiance at its own height and column. Its matter is
   drawn from its own cell, rationed as a body's draw is now. Each part casts its share of the body's shadow at its own
   layer, so both sides of the light still move together and D099's cap still binds the sum. This brings the code to
   DESIGN §5A.1 and to D154's light sense, which already reads the light at each part. Light and matter get a switch
   each and are screened together.
3. **A bitten module** frees its place. When a bite takes a part of an indeterminate node, that occurrence leaves the
   node's count, so the module rule can regrow it at the tissue price. D106 asked for a plant "regrowing a lost one when
   affordable". The shape test's refusals are split by cause first. A cause that is a fault is fixed as agent work. A
   cause whose fix would change what a body may be comes back as its own rule. To start overpowered, half the anchored
   founders draw their blade nodes indeterminate, with a ceiling of eight.
4. **Four rules stay** as they are. D113's support cost still prices reach. For a flat blade at 2 m, support equals its
   light-bound income about 12 m from the holdfast, and about 4 m away if matter holds income to a tenth (arithmetic).
   D064 stays, so floats lift nothing on bodies under 0.25 m³. A rigid anchored plant needs no float, because the grip
   holds its holdfast's turn with a spring and the plant stands as it grew. Floats earn their keep only on plants that
   grow past 0.25 m³, and the screen counts those. D127's dark link and D264's 30 m dispersal stay too.

## 5. The alternatives, and what each implies

- **Cheaper links for** producers would be a dial, with no build. A stalk already costs a few per cent of a blade's
  light and buys nothing while a body feeds at one point. So a null is expected, which the ledger shows in seconds.
- **Indeterminate growth made** cheaper than budding is already cheaper, by the 50 J overhead. The shape test and the
  count ceiling block it, and rule 3 takes those on. Without a holdfast, a bigger drifter still draws from one cell and
  still leaves the island with the current.
- **A light rule** that rewards height for drifters too is rule 2 alone, and arm W tests it. Drifters are neutral and
  cannot rise, and matter binds, so little is expected. Floats made to lift small bodies would let drifters rise, into a
  mat at the surface over the open water as well as the oases. That works against the near-empty water between oases.
  It would also amend D064, the owner's own design, so it is held as a fork for the owner.
- **D204's island shoals** first would change each island's water volume, matter density, burial and current in the
  same screen as the plants. They are kept as the fallback for rule 1.

## 6. Forests become the oases, and the water between stays open

Matter flows into the top 12 m of each island column and sinks at 0.02 m/s. It is buried twenty times faster under the
desert than under an island. A drifter leaves that matter at the current's 0.02 m/s, about 70 m an hour, while an
anchored plant stays in it. The deep bed between islands holds no rock in lit water, so no forest can stand there, and
only drifters and swimmers cross it. Grazers then meet fixed patches they could learn, which is D268's own reason for
the cell. Each island's bloom clock (D251) makes the forests rich in turn, so a grazer that leaves a lean forest for a
blooming one is paid for the journey. That is the migration between oases the owner's guidance of 2026-10-15 asks for
(inference; nothing here has tested it). In a laboratory predator-and-prey system on plants, eight islands joined by
throttled bridges persisted 393 and more than 447 days, where one continuous system died in 120 [JN97 p.7]. The review
reads a refuge model as showing that a refuge covering the feeding ground removes its consumer [KR13 p.2], so nothing
here shields a blade from a mouth. HANDOFF names three gaps as the world's and not the theatre's: blades with no stipe
or holdfast, level drift, and a cloud of blades. The world would close all three.

## 7. Costs and risks

- **The card steps** bodies in size classes of 2, 4, 8 and 16 links, and the 16-part limit keeps every body inside the
  last. The biggest plant decides the class, as D106's cost note foresaw. Round 66's world held 12,853 bodies at
  24,000 s, against the 25,002-body ceiling that ended two `r63d` worlds. Step time grows in a straight line with the
  bodies, and is expected to grow with links once plants climb classes (inference). A world under 0.5 times real time
  is stopped at once.
- **Long parts load** contact. In round 61, blades 10 to 50 m long stopped a world twice (D196). Contact now tests true
  shapes, but tall plants still add pairs, and the read counts them.
- **Growth has bounds** that read no state. A plant is bounded by 16 parts, 8 levels, D099's cap, D113's support,
  D101's overlap test and its module ceiling. A forest is bounded by matter (D074, D266) and the finite sun (D023). One
  gap stays. Shade is pooled over the whole world, so a dense forest does not darken its own understorey as a canopy
  does in the sea. While matter binds, that costs little. If forests become light-bound, local shade comes back as its
  own question.
- **No grip has** yet formed in a world under true-shape contact, so a grip smoke comes before the pre-registration.
- **Young land off** the rock. A 30 m disc around a parent on an island a few tens of metres across puts many children
  in the desert (inference). The 15% of the coral that stands in the desert as stepping stones may catch some. The read
  counts the share of anchored births that grip.
- **Grazers may lose** or gain. A forest may feed them better or lift blades out of reach, so the grazer floor is a mark.

## 8. The screen

The world is round 66's arm M, the world the nursery's retrain runs in. It runs on seeds 6202 and 6203 for 24,000 s, as
two card groups of three. Arm W carries rule 2 alone, arm A carries rules 1 and 2, and arm R carries rules 1, 2 and 3.
The control is round 66's own M runs, licensed by the switch-off proof. The pre-registration fixes these marks.

1. **Plants grow in** A or R on both seeds. At 24,000 s at least a quarter of living plants carry three or more
   developed photosynthetic parts above the minimum, and that share is at least five times the control's by the same
   census.
2. **Holdfasts are taken** in A and R. At least a third of plant tissue is anchored at 24,000 s.
3. **Forests are the** oases. The share of plant tissue in island columns rises by at least 0.1 over the control's, and
   the share of empty 10 m squares falls by no more than 0.05.
4. **Height pays in** A. The median anchored plant's highest blade stands at least 1 m above its holdfast, and income
   per square metre of lit area rises with a part's height within anchored plants.
5. **Plants regrow in** R. At least half the anchored plants that lose a blade and live 300 s more grow one back.
   Bitten plants survive 300 s at least twice as often as in the control.
6. **Grazers hold their** ground. Their mean from 12,000 s is at least half the control's, and their leaf share of bite
   income falls by no more than 0.05. Crossings between forests are counted as story material and not gated (D254).
7. **The world holds** up. Pace stays at or above 0.5 times real time, no world reaches the body ceiling, and the audit
   residuals stay at their scale.

If forests choke the islands or the grazers, the next screen dials back the anchored share, the bud weight and the grip
prices, in that order (D262). If nothing anchors, the same dials go up.

## 9. The order of work

1. **Reads and the** ledger come first: section 3's tests and the coral census. They take an hour or two of agent time.
   Machine time is seconds for the ledger, minutes for the reads, and about an hour for the probe resume, run alone off
   the card (inference).
2. **The build goes** behind switches: rules 1 to 3, a census of developed parts, rows of income per part, and a ledger
   that prices each part at its own depth. A Sonnet subagent builds it and the coordinator reviews it. The bill runs on
   the host, so no new card code is expected beyond the grip already there (inference, settled in the build).
3. **The proofs come** next. With every switch off, the build steps r66-M row for row for 1,200 s on both seeds. Each
   switch on is the same at groups 32 and 64. A grip smoke shows grips forming under true shape.
4. **The pre-registration is** committed on a clean tree before the launch.
5. **The screen takes** two card groups, a few hours each by the slowdown measure, and longer if plants climb size
   classes.
6. **The read follows** a look at both seeds in the theatre, and then the dial step.

Steps 1 and 2 run side by side. Only rule 3's fault fix waits on a read.

## 10. What is settled under D291, and what it amends

The reads, the ledger, the instruments, any fault in the shape test, the build, the proofs, the pre-registration, the
screen and its read are agent work. So are the bud weight and the grip prices, which are dials D165 and D268 already
provide. The coordinator settled three world rules under D291 on 2026-11-03, each in its own entry.

- **Anchored plant founders** (rule 1, D296) reverse D165's rejection of anchored founders in the farm's worlds. D268's
  clause that no plant must anchor stands.
- **Each part feeding** where it stands (rule 2, D297) amends no entry, since none rules that a body feeds at its
  centre. It extends D110 from a part's pose to its height, and it keeps D099's cap.
- **A bitten module** freeing its place (rule 3, D298) amends D106's second item, as the module spec's rule 4 built it.

The specification keeps D064, D101, D111, D113, D127, D162, D204, D209, D251 and D264 as they are. It takes up
HANDOFF's held question of holdfasts at the oases' edges. Floats that lift small bodies would amend D064, and would go
to the owner if a later screen asks for them. Nothing here is irreversible or leaves the machine.

## Sources and code read

Two papers come from the literature review. The seaweed biology in the third row is a lead from general knowledge, not
yet in the review, and no rule rests on it.

| Key or file | What it supports |
|---|---|
| [JN97 p.7] | Eight 10-plant islands joined by throttled bridges persisted 393 and 447+ days, where one continuous 90-plant system died in 120 (the review's synthesis matrix) |
| [KR13 p.2] | A consumer's equilibrium is the refuge's stock plus its break-even accessible food, so a refuge over the feeding ground removes it (the review's reading, which explained D055's failure) |
| Leads, not in the review | Kelp forests stand on rocky reefs from a holdfast, with a stipe and blades. Giant kelp carries a gas float at each blade's base. Light limits kelp's depth, and nitrate often its growth. Kelp grows from a meristem where stipe meets blade, so a grazed blade regrows, while a cut stipe or holdfast kills the plant. *Sargassum* drifts in the open ocean on gas floats |
| `src/Evosim.Core/Cells/StandardCellTypes.cs` | light and matter income (`Fixation.Fix`); float upkeep (`BuoyancyCell`); grip strength and prices (`AnchoringCell`) |
| `src/Evosim.Core/Ecosystem/World.cs` near line 5058; `src/Evosim.Farm/Metabolise.cs` lines 177 to 197 | the bill's irradiance and matter at the body's centre of mass |
| `src/Evosim.Core/Environment/LightField.cs` | shade pooled per 1 m layer over the whole world |
| `src/Evosim.Core/Ecosystem/Metabolism.cs` | upkeep and the support cost (`SupportWatts`) |
| `src/Evosim.Dynamics/Fluid.cs` near line 405; `BuoyancyModel.ExcessDensityFactor` | lift multiplies the size-scaled excess density |
| `src/Evosim.Core/Ecosystem/WorldModules.cs`; `Organism.LostPartPaths` | the module rule, its shape test, and regrowth by count up to the ceiling |
| `src/Evosim.Core/RunConfig.cs` (`OverheadFor`); `src/Evosim.Core/Genome/ReproductionTraits.cs` | the per-child overhead |
| `src/Evosim.Dynamics/Anchor.cs`; `src/Evosim.Dynamics/ContactTrueShape.cs` near line 355 | the grip's position and turn springs and its decay; coral counts as rock for a grip |
| `src/Evosim.Farm.Gpu/GpuOptions.cs` | the card's size classes |
| `runs/r66-m-6203/2026-11-02-031710-47e1fc9f/` | `config.json` for every price; `stats.jsonl` at 24,000 s for uptake, shading and the module counts |
| leaf pool `leaves-4452a00019df0811` of `r66-m-6203` at 24,000 s | heights and node volumes |
