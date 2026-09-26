# A glossary for the story films

*Written 2026-09-25 for round 48's story film, version 2. It lists every word a viewer meets on
screen, says what each one means in one or two plain sentences, and names where the meaning is
set: the code, a decision in `DECISIONS.md`, or the round's own settings. Where the code says
something different from what the first film said, the code wins, and the entry says so. A film
uses these words and no synonyms. A word a film needs that is not here gets an entry before the
film is filmed.*

The settings quoted are round 48's, read from `runs/r48-s1/2026-09-24-115115-e5a30c15/config.json`
(the three seeds share one configuration hash, `e5a30c15`). The code is `src/Evosim.Core` on the
main tree at `4355869`.

## The tank and its time

| word | what it means | where it is set |
|---|---|---|
| tank | One world: a round disc of water, 167 m across, lit from above. Its floor slopes from a shoal 1 m deep to about 93 m, so "45 m deep" is only the average. | world area 22,000 m², so the radius is 83.68 m; `bed.bedTiltMetres` 96 and `bed.bedShoreDepthMetres` 1 (the bed map `bed.f32` beside each run). The first film said "45 m deep"; that is the mean depth, not the depth. |
| run, seed | One tank from its first second to its last. The three tanks of a round share every rule and differ only in their random seed, so each tells a different history. | the run directory; its `run.json` names the seed |
| round | One experiment: a set of tanks run together under the same rules to answer one question. Round 48 is the 48th. | `logbook/0119` (round 48's pre-registration) |
| second, s | Time inside the tank. A run lasts 30,000 s, about eight hours. The film shows it at the tank's own speed: a second on screen is a second in the tank. | the run budget; the director's real-time rule (`SafariDirector.cs`) |
| light | Sunlight at the surface is 200 W/m². It fades with depth: to 37% at 6 m, 14% at 12 m and 8% at 15 m. | `light.surfaceIrradiance` 200, `light.attenuationDepth` 6; `LightModel.IrradianceAt` |
| rock table, reef | A flat rock on a stem, its top about 3 m under the surface. Nothing passes light through it, so the water beneath lies in its shade. Tank 1 has 15, tank 2 has 21 and tank 3 has 16, covering a quarter of the area. | `reef.reefCover` 0.25, `reefCapDepthMetres` 3; `ReefGeometry.CapTransmission` = 0 (D118); `run.json` `reefs` |
| shade | Bodies shade the water under them too, so a crowd near the surface dims the light for everything below it. A body never shades itself. | the light field (`LightField`); `world.lightSilhouetteCap` on |
| current | The water turns slowly in large loops and carries bodies, corpses and snow with it, up and down as well as sideways, at about 0.1 m/s. | `current.mode` Transport, `current.speed` 0.1 (D088, D090) |
| checkpoint | A saved copy of a whole tank at one moment, from which the tank can be started again. Tanks 1 and 2 saved one every 2,500 s, tank 3 every 500 s. | `EVOSIM_CHECKPOINT_EVERY`; `checkpoints/` in each run |
| COUSIN | The word in the corner of every frame. The viewing program re-runs each scene from the nearest checkpoint, so the bodies on screen start as recorded and then drift apart from what was recorded. A birth on screen may not be the recorded one; a caption that says "in the run" gives the recorded fact. | the theatre's provenance word (CLAUDE.md, live play is a cousin by construction) |

## Bodies and their parts

| word | what it means | where it is set |
|---|---|---|
| body | One living creature. | `Organism` |
| recipe | The instructions a body is built from, for its body plan and its small brain together. A child inherits its parent's recipe with small random changes. Scientists call it the genome. | `Genome`, `Developer`, `Mutator` |
| evolution | Recipes change a little from parent to child, and recipes that leave more children become common. Nothing else chooses. | `World.Conceive` (a child is a mutated copy); there is no fitness function |
| part | One block of a body: a box, a ball or a capsule. Each part is one kind of tissue. A round-48 body has at most 16 parts. | `development.maxParts` 16; the cell types in `config.json` `cellTypes` |
| kind of part | What a part does: a leaf, a stomach, a link, a float, a mouth, a nerve part or a plain part. | `StandardCellTypes.cs` |
| leaf | A part that earns energy from the light that falls on it. It catches more lying flat than on edge. It also needs matter dissolved in the water to build with, so in water stripped of it a leaf earns less than the light allows. | photosynthetic cell, efficiency 0.05; `Fixation.Fix` takes light × lit area × 0.05, capped by what dissolved matter it can take up; `world.lightByExposure` on |
| stomach | A part that filters snow out of the water. Each second it clears ten times its own volume of water and keeps the snow in it, so what it earns is its size times the snow's thickness where it is. | absorptive cell, clearance 10, yield 1; `AbsorptiveCell`. Body 3318 at 3,500 s: 10 × 0.0899 m³ × 1.393 J/m³ = 1.25 W, its recorded intake |
| eater | The film's word for a body with a stomach and no leaf. It lives on snow alone. The guide names its lines Gastrella, Phagus, Voratrix or Stomachium, and Natogastrum and its kin when it has a joint. | the flags abs 1, pho 0; `scripts/guide.py` GENERA |
| link | A plain limb part, the only kind that can carry a moving joint. In round 48 a link also catches light, at half a leaf's rate, so a jointed eater earns some of its living from light. | link cell, `photosyntheticEfficiency` 0.025 against a leaf's 0.05 (`EVOSIM_LINK_PHOTO` 0.5). The first film called a link "a plain part that a joint can swing"; it left out the light. Natogastrum plocrele's founder earned 0.35 W of light beside 0.47 W of food at 5,000 s. |
| joint | A hinge between two parts that lets them swing. The body's brain drives it. A body counts as jointed when any joint can move. | the `jnt` flag (a degree of freedom greater than 0); `LinkCell` |
| brain | A small network of neurons in the body that reads its senses and drives its joints. In round 48 a brain costs nothing to run. | `Brain`; `economy.neuralCostPerNeuronWatts` 0 |
| mouth | A part that bites other bodies or corpses. No living body was eaten in round 48; a few corpses were bitten (3, 5 and 2 in the three tanks). | consumer cell; `stats.jsonl` `bodiesEaten` 0 and `corpsesEaten` 3, 5, 2 at each run's end |
| float | A part that lifts the body in the water. | buoyancy cell |
| bud | A small new part of a different kind that a child is born with, welded onto a copy of one of its parent's parts. Since round 48 this is the only way a new kind of part arrives; no mutation turns an existing part into another kind. | D119; `Mutator` (the old `ChangeCellType` is gone), `Developer` (the welded bud is exempt from the smallest-part rule) |
| speck of stomach | The film's words for a stomach bud that stays tiny: body 201's was 0.24% of its body, body 48048's 0.17%. | `absorptive.jsonl` `absVolume` over `volume` |

## Energy: earning, spending, saving

| word | what it means | where it is set |
|---|---|---|
| joule, J | The unit of energy. The film counts everything a body earns, spends and saves in joules. | |
| watt, W, "J a second" | A joule a second. The film says "J a second" on screen and the charts say W. | |
| income | What a body earns each second: light through its leaves (and links), plus snow through its stomach. | `World.Metabolise`; `absorptive.jsonl` `lightW` and `foodW` |
| upkeep | What a body pays each second just to stay alive. It is set by the size and kind of its parts (a stomach costs more per litre than a leaf, a leaf more than a plain part), plus a small cost for holding its shape, and it rises with age. An eater also pays a tenth of what it eats to digest it. | `cellTypes[].upkeepWattsPerCubicMetre` (plain 1, link 2.5, leaf 3, stomach 4 W/m³); `world.supportWattsPerSquareMetrePerSquareMetre`; `world.handlingCostPerJouleEaten` 0.1; `Metabolism.Bill` |
| leak | A living leaf lets 15% of the light energy it catches escape into the water as snow. | `feeding.exudationFraction` 0.15; `World.Metabolise` deposits the leak into the snow |
| reserve | A body's savings, in joules: everything it has earned and not spent. It is the line the film's account chart draws. | `Organism.Energy`; `absorptive.jsonl` `energy` |
| account chart | The corner chart that draws the followed body's reserve as the scene plays, with its births marked. | `SafariChart` kind `account` |
| body's worth, tissue | The energy built into a body's parts, 500 J for every cubic metre. It is what a parent draws on to make children and what a corpse gives back. | `cellTypes[].tissueEnergyPerCubicMetre` 500; `Organism.TissueJoules` |
| born small | A child starts as a smaller copy of its adult body and grows. Body 201 was born at 40% of its adult size, a Frondium nisimocrax child at a median 6%. | the lineage row's `bf` (the child's body at birth over its own adult body) |
| growth | While a body is below its adult size, each half-second it spends every joule of its reserve above a tenth of its body's worth on growing. So a young body grows first and saves later. | `World.Grow` runs before `Reproduce`; `growth.growthReserveFloor` 0.1 |

## Children

| word | what it means | where it is set |
|---|---|---|
| share | The part of its own body's worth a parent spends on each child: its recipe's investment times its body's worth, divided among the litter. | `World.Conceive`: `BirthInvestment × TissueJoules / BroodSize` |
| investment | The fraction of its own body's worth a parent spends on one litter, a number in its recipe between 0.05 and 1. A line that sets it low makes small children. | `ReproductionTraits.BirthInvestment`; `genome.minBirthInvestment` 0.05. The table's `meanBirthInvestment` is this number averaged over the living, not the size of any child. |
| brood | How many children a parent makes at once. | `ReproductionTraits.BroodSize` |
| first reserve | A fifth of each child's share becomes the child's starting savings. The other four fifths build its body. | `growth.newbornReserveFraction` 0.2 |
| fee | On top of the share, each child costs a fee of twice the child's body's worth, and never less than 50 J. The fee is burnt: its energy leaves the tank as heat. | `economy.perOffspringOverheadJoules` 50, `perOffspringOverheadPerTissueJoule` 2; `RunConfig.OverheadFor` (D120). Before round 48 the fee was a flat 100 J. |
| price of a child | Its body, plus its first reserve, plus the fee. One child of body 201 cost 11.7 + 2.9 + 50 = 64.7 J. | `World.Conceive` |
| cushion | What a parent insists on keeping after breeding: a number of seconds in its recipe times its present upkeep. Body 201 kept 51 s of upkeep, 14 J. | `ReproductionTraits.ReserveMargin` × `StandingWatts` |
| when a body breeds | A body breeds the moment its reserve covers the price of its whole litter plus its cushion, provided there is room for the child near it and the child is not too light to be built. Growth comes first, so in practice a body breeds only once it is full grown. | `World.Reproduce`, `IsSolvent`, `Conceive`; the placer (`population.offspringDispersalMetres` 5; a refusal is a crowded stillbirth); the mass floor (`growth.minNewbornPartKilograms` 0.05, weighed per rigid group) |
| gestation | A second way to breed, new in round 48: a line that gestates banks a share of every good second's income into a separate account that upkeep cannot touch, and pays for children from it. It was tried and selected out in every tank. | D120; `ReproductionTraits.Mode` and `GestationShare`; `World.Gestate` |

## Age, death and what the dead leave

| word | what it means | where it is set |
|---|---|---|
| ageing | Upkeep rises with age: at 3,000 s old a body pays twice what it paid at birth, at 6,000 s three times. Income does not fall with age. | `world.senescenceDoublingSeconds` 3000, wear = 1 + age / 3,000 s on upkeep alone (D121). Before round 48 age also cut income. |
| death, starving | A body dies when its reserve reaches zero. In round 48 that is the only way anything died: no living body was eaten and none broke. | the death rows' cause `starved` in all 67,036, 22,504 and 47,414 deaths; `diverged` 0 |
| corpse | A dead body. It keeps its body's worth and whatever reserve it had, and crumbles into snow at half a percent a second, half of it gone in about two minutes. | `world.corpseDecayPerSecond` 0.005; `World.Bury`, `StepCorpses` |
| snow | Energy-rich matter drifting in the water, and a stomach's food. About three quarters of it is leaked by living leaves and a quarter comes from the dead. It sinks 2 mm a second, and what nobody eats slowly dissolves back into the water. The code calls it `Nutrients` and the reports call it detritus. | leaked 75.4%, 73.8% and 75.9% of the snow's joules in the three tanks (`detritusExudedTotal` against `detritusDepositedTotal`); `nutrientSinkMetresPerSecond` 0.002; `remineralisationPerSecond` 0.0005. The first film said the dead make the snow; living leaves make three times as much. |
| dissolved matter | The spent form of the same matter: what a burning body gives back to the water and what a leaf needs to build with. The tank holds a fixed 15,000 units of matter, charged or spent, and none arrives or leaves except with the newcomers. | `world.matterBudgetUnits` 15000; DESIGN §5A.2d, D098. The code calls it `Matter`. |
| snow's thickness | How much snow a cubic metre of water holds, in J/m³. Body 3318 fed at 1.39 J/m³ when young, body 3559 at 0.61 when old. | `absorptive.jsonl` `densityHere` |

## Lines, names and newcomers

| word | what it means | where it is set |
|---|---|---|
| line | A family: a first body and every descendant born with the same kinds of part. A child that gains or loses a leaf, a stomach or a joint starts a new line of its own. | the guide's clade rule, `scripts/guide.py` `build_clades` (the flags abs, jnt, pho) |
| a line's size | The number of its members alive at a moment. | lineage event sweep, `logbook/specs/story-r48-v2/runlib.py` `Run.series` |
| name of a line | Two made-up words from the guide. The first says what kind of body the line is (a Frondium or a Thallus is a leaf, a Gastrella an eater, a Vorafrons or a Gastrophylla a leaf with a stomach, a Natogastrum a jointed eater). The second is drawn from the first body's recipe. When two lines in one tank get the same name, the later ones carry a numeral: Gastrella cidutis, II, III, and past X a plain number. | `scripts/guide.py` GENERA and the epithet hash |
| founder | A body dropped into a tank rather than born, with a random recipe or a stored one. Most lines start with a founder (973 of tank 1's 1,281); the rest start from a born child that changed kind, and trace back through it to a founder. | lineage rows of kind `f`; line roots by kind in `r48-s1` |
| population floor | For the first 3,000 s, founders are added two at a time whenever fewer than 40 bodies are alive. | `population.minimumPopulation` 40, `floorSpawnsPerStep` 2, `floorClosesAfterSeconds` 3000 |
| newcomer | After 3,000 s, a random founder is dropped in about once every 30 s, at random intervals. | `population.foundingTricklePerSecond` 1/30 (D115) |
| stored eater | One newcomer in ten is not random but an exact copy of one of four eater recipes we saved from earlier rounds. Almost all that bred were the one-part ball. | `foundingTricklePoolShare` 0.1, `foundingTricklePoolCount` 4 (D117) |
| landing rule | Every founder lands where its food is: a column of water is chosen with a chance in proportion to its food, and the founder is placed at the depth where that food is richest. The rule places the founder; whether the body then fed well is a separate question, and most pool founders first fed below their column's average. | `world.foundersFollowFood`, `foundersFollowFoodDepth` (D116, D122); logbook F4 reading 19%, 21% and 19% |
| gift | Energy a founder brings with it, on top of 200 J times its birth size: 600 s of its own upkeep at the size it lands at. It is new energy from outside the tank, like the founder's whole body. | `population.founderEndowmentSeconds` 600, `founderEnergyJoules` 200 (D122); the founder row's `endow`. The first film said nothing new arrives except the gifts; a newcomer's whole body and start arrive from outside. |
| typical | The film's word for a median: half the group is above it, half below. | |

## The record behind the film

| word | what it means | where it is set |
|---|---|---|
| guide | A reading of a finished run that names every line and ranks its facts. The director places a scene's subject from it. | `scripts/guide.py`; `guide/guide.json` beside each run |
| in the run | The recorded fact, as opposed to what the cousin on screen does. | `lineage.jsonl`, `absorptive.jsonl`, `stats.jsonl` |
| prediction | A claim written and committed before a round runs, with the number that would prove it wrong. Round 48 had fifteen, beside two readings with no threshold; the film follows two of them (M3, a budded stomach's line of ten, and F3, a stored eater's line of ten). | `logbook/0119` |
