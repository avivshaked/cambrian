# Proposal: fat, the build specification

*Drafted on 2026-10-25 for the coordinator, the speed agent and the owner. The speed agent's question Q6 asked for three things before building: the cap's number, which bodies it binds today, and the fat cell's prices. The rule is D229, and its meeting with the gut is D232. This draft only reads. It builds nothing and ran no farm. Every claim about the code names its file, in the text or in the table at the end of its section. Anything guessed is marked as inference.*

## The specification in one paragraph

A body's reserve is the energy it has banked and can spend. Today nothing caps it in any round. A dial for a cap already exists, built for D098 and left at 0 in every round. I propose that D229's cap reuse that dial with a new form, behind a switch that is off by default. In words, the cap is three sums added together. The first is what the body's next litter costs. The second is what the body still has to grow. The third is 3,000 s of its standing cost, meaning the watts its body burns at rest. Each cubic metre of fat tissue then adds 50,000 J on top. Income above the cap goes into the water as dissolved food, which is the route the dial already uses. The cap does not count the gut: a gut waits to be digested until the reserve has room again. At 3,000 s the cap would have bound at most 0.25% of the stomach bodies in the two round-62 worlds measured. That is far too rarely for fat to buy anything in today's worlds, so the screen also needs an arm at 1,000 s, where fat has room to buy. Fat costs 0.5 W/m³ to keep and 2,000 J/m³ to build. It has no attack, intake or protection, has neutral toughness and floats no more than other tissue. It changes nothing on the graphics card and bumps neither the checkpoint nor the genome format. Like every new tunable, its switch makes older configs unreadable. The build is half a day to a day of agent time, and the screen is about six hours of machine time once grazers pass D222.

## Q6's three answers

| Question | Answer |
|---|---|
| The cap's number | `cap = litter price + growth still to come + 3,000 s × standing watts + 50,000 J/m³ × fat volume` |
| Which bodies it binds today | Stomachs: 0.25% ever, in `r62bd-x2-6201`. 0% in `r62cs-w500-6201`. Both are upper bounds, because they leave out the litter and growth terms. Leaves: not measurable from the run files (see below). Mouthed swimmers: probably none (inference). |
| The fat cell's prices | 0.5 W/m³ to keep, 2,000 J/m³ to build. Store room 50,000 J/m³. Attack, intake and protection 0, toughness 1. Lift 0. Bud weight 1 |

## The reserve today has a dial for a cap, and no cap

A body's reserve is one double-precision number of joules on the organism. Death comes when it reaches zero, and that is the ecology's only starvation rule (§5A.6). The body's tissue is held in a separate account that cannot be spent. A starving body cannot eat itself, and nothing ever shrinks (D087). A body that breeds by gestation keeps a second account, the joules it has set aside for its next litter. Upkeep never draws on that account. The kill-gut build adds a third, the gut, in the `kill-gut` worktree, uncommitted as this is written.

The cap dial is D098's leg 10. Above zero, it trims any reserve over a set number of seconds of standing cost, and it deposits the excess as charged matter into the nutrient field where the body is. Charged matter is the dissolved food that absorbers eat. The world counts the trim with exudation and keeps a running total of it. That total is checkpointed, but no stats column reports it. Every round's launcher sets the dial to 0, and every run header reads `reserveCap off`. D120 rejected the cap as a way to feed a larder and named the gestation account as the general route for a surplus.

Each half-second metabolic step runs in this order. First comes the body's bill (income, upkeep, the bite's waste), then the trim for a body that breeds in one lump. Then gestating bodies bank half their net gain and are trimmed. Then growth, then births. So a trim always lands before the step's births.

A birth draws on the reserve once. The price is the newborn's tissue, plus the newborn's starting reserve (0.2 of the parent's share), plus a fixed overhead for each child. In round 62 that overhead is at least 50 J. A body that breeds in one lump pays from its reserve and must keep its margin gene's seconds of standing cost after paying. A gestating body pays from its account. The gate a body must reach before it tries is one expression, shared by every check that reads it.

A body that dies keeps its reserve in its remains. With contact eating on, a dead body becomes remains whose store is its reserve plus its account, and a bite takes that store as it takes tissue. Under D224 a bite on a living body takes the same share of its reserve and account as of its tissue. That share is the share of the body's whole tissue value the bite removed, so a bite on a costly part takes more of the store. This matters for fat below.

One sense reads the reserve. The energy sensor reports `tanh(seconds of reserve / 3,000 s)`, and round 62's header names that full scale as `energy 3000 s`. The host computes seconds of reserve and uploads them to the card every step. The card never computes the reserve itself.

| Claim | Where |
|---|---|
| Reserve, account, tissue, standing watts, seconds of reserve, the gates | `src/Evosim.Core/Ecosystem/Organism.cs` (`Energy`, `GestationJoules`, `TissueJoules`, `StandingWatts`, `SecondsOfReserve`, `BreedingGate`, `LumpGate`) |
| The gut | `scratch/wt-killgut/src/Evosim.Core/Ecosystem/Organism.cs` (`GutJoules`, uncommitted), `scratch/wt-killgut/logbook/specs/d232-kill-gut-build.md` |
| The dial and the trim | `src/Evosim.Core/RunConfig.cs` (`ReserveCapSeconds`), `src/Evosim.Core/Ecosystem/World.cs` (`TrimReserve`, `ReserveTrimmedTotal`), `src/Evosim.Core/Ecosystem/WorldState.cs` (checkpointed) |
| Set to 0 in every round | `rounds/env-r42.ps1` to `rounds/env-r52.ps1` (`EVOSIM_RESERVE_CAP = 0`), `runs/r62bd-x2-6201.md` header |
| D120 rejected it | `DECISIONS.md` D120, under Rejected |
| Step order | `src/Evosim.Core/Ecosystem/World.cs` (`Metabolise`, `Gestate`, `Grow`, `Brood`) |
| A birth's price | `src/Evosim.Core/Ecosystem/World.cs` (`Conceive`), `src/Evosim.Core/Genome/ReproductionTraits.cs`, `src/Evosim.Core/RunConfig.cs` (`OverheadFor`) |
| Death, remains, the bite on the store | `src/Evosim.Core/Ecosystem/World.cs` (`Bury`), `src/Evosim.Core/Ecosystem/WorldContact.cs` (`OfferBite`) |
| The energy sensor and the card upload | `src/Evosim.Dynamics/CreatureSenses.cs` (`Squash`), `src/Evosim.Farm.Gpu/Kernels.D.g.cs` and `Kernels.F.g.cs` (the host writes `SecondsOfReserve` into the reserve buffer) |

## What today's reserves look like

Two worlds of round 62 were read: `r62bd-x2-6201` (bite damage ×2) and `r62cs-w500-6201` (the remains-window world that D232's screen builds on). In both the first 6,000 s were skipped, while founding still runs. No stats column gives a reserve for each body. Two files do.

The absorptive log writes a row for each logged body that has absorptive tissue. Each row carries its reserve, tissue, volume and standing watts. That gives stomachs exactly, but no plain leaves.

The lineage file's death rows carry the reserve of any body that died solvent. Most deaths in these worlds are deaths of age, so this gives leaves' reserve at the end of a full life. It gives it in joules only, because no row carries a leaf's volume or watts.

Checkpoints hold every body's reserve and body. No reader exists for them, and writing one was beyond a read-only pass.

The first table shows stomachs, taking each body's highest reserve over its life. The p99 and the maximum are what a cap must clear.

| World | Bodies | Median | p90 | p99 | Max | Per m³ of body, p99 / max |
|---|---|---|---|---|---|---|
| r62bd-x2-6201, plain stomachs | 8,628 | 588 s | 1,489 s | 2,416 s | 4,486 s | 10,900 / 21,200 J/m³ |
| r62bd-x2-6201, stomachs that also fix light | 465 | 479 s | 1,152 s | 2,083 s | 2,448 s | 8,970 / 11,000 J/m³ |
| r62cs-w500-6201, plain stomachs | 1,728 | 297 s | 673 s | 1,116 s | 2,189 s | 5,830 / 11,700 J/m³ |
| r62cs-w500-6201, stomachs that also fix light | 4,860 | 468 s | 741 s | 1,178 s | 2,568 s | 2,740 / 5,690 J/m³ |

The second table turns those into bind rates for a pure seconds cap. Look at the 3,000 s row: almost nothing reaches it. The proposed cap adds the litter price and the remaining growth on top, so it binds less often still.

| Cap | r62bd-x2-6201: bodies ever above | rows above | r62cs-w500-6201: bodies ever above | rows above |
|---|---|---|---|---|
| 600 s | 49% | 21% | 24% | 6.8% |
| 1,000 s | 25% | 7.6% | 2.6% | 0.66% |
| 2,000 s | 2.7% | 0.43% | 0.06% | 0.016% |
| 3,000 s | 0.25% | 0.043% | 0% | 0% |
| 15,000 J/m³ | 0.13% | 0.027% | 0% | 0% |

A typical evolved stomach at 24,000 s is about 0.02 to 0.03 m³ of body. It burns about 4.5 W/m³, or roughly 0.1 W, holds 10 to 15 J of tissue, and banks a median 2.4 to 3.8 times its tissue as reserve.

The third table shows reserves at a death of age, in joules. Leaves that gestate hold a little more, because only upkeep ever draws on the half of their gain that stays in the reserve.

| World, body | Deaths of age | Median | p99 | Max |
|---|---|---|---|---|
| r62bd-x2-6201, leaf, lump | 43,062 | 67 J | 207 J | 413 J |
| r62bd-x2-6201, leaf, gestating | 6,304 | 49 J | 266 J | 508 J |
| r62cs-w500-6201, leaf, lump | 33,884 | 67 J | 240 J | 407 J |
| r62cs-w500-6201, leaf, gestating | 5,402 | 46 J | 277 J | 452 J |
| r62bd-x2-6201, mouthed | 65 | 567 J | 724 J | 724 J |
| r62cs-w500-6201, mouthed | 62 | 516 J | 711 J | 711 J |

Leaves' reserves could not be turned into seconds or joules per cubic metre. The guess is that an evolved leaf burns 0.1 to 0.2 W, like the stomachs beside it. If so, its p99 reserve is 1,000 to 2,800 s, and a 3,000 s cap binds leaves rarely too. That is inference, and a 1,000 s cap might bind many leaves. Founders, read from their endowment of 600 s of newborn watts, burn about 0.5 W as adults. Evolved bodies are smaller, so that figure only bounds the guess from above. For the mouthed bodies, round 61's ledger figure for a planted swimmer, 0.91 W, was borrowed (the default in `scripts/reads/r61-grazer-economy.py`). At that rate their largest reserve is about 800 s, which binds at neither 1,000 s nor 3,000 s. This too is inference.

So step 0 of the build is a small checkpoint reader, built from the commit that wrote these runs. It prints each living body's reserve, tissue, standing watts and volume from one recorded checkpoint, and so measures leaves directly before the constants are fixed.

The readers are `scripts/reads/fat/reserve_read.py` and `scripts/reads/fat/bind_rate.py`.

## The cap rule

The cap is a set amount of room above what the body is about to spend on its next litter and its growth. Fat tissue adds room. Written out:

```
cap = P + R + s × W + Σ (room per m³ of the part's type × part volume)
  P = the next litter's price as the gate estimates it (ReproductionTraits.CostJoules at the body's tissue)
  R = adult tissue less current tissue, what growth will still draw from the reserve
  W = the body's standing watts, fat's own upkeep included
  s = ReserveCapSeconds, 3,000 s
  room = 0 for every type but fat, and 50,000 J/m³ for fat
```

### Why seconds of standing cost, and why above the litter

Seconds of standing cost beat a base per cubic metre for three reasons. The trim, its books and its tests already exist in those units. The energy sensor reads the same units: at the cap a body's sensor reads tanh(1), about 0.76. And room in seconds is room scaled to need. A body of costly tissue burns its store faster, and gets room for the same number of seconds of life. A base per cubic metre would give a body of the cheapest bulk as much room per volume as one of the dearest. Its rare-binding value for stomachs would be about 15,000 J/m³ (the second table). It is kept as the alternative in decision 1.

The litter term is there because a cap can otherwise forbid births. In round 62 a small body's gate is mostly overhead, the 50 J floor for each child. A stomach of 0.02 m³ under a cap of 5,000 J/m³ would hold 100 J. A margin gene of 600 s at 0.09 W asks 54 J on top of that 50 J. Such a body could never breed. With the price and the growth inside the cap, a body that breeds in one lump can always reach its gate, as long as its margin gene is under s.

The margin itself is left out of the cap on purpose. Otherwise a lineage could raise its store simply by evolving caution, with no fat at all. Under this rule a margin above 3,000 s makes a lineage sterile. Today's margins have a median of 160 to 320 s, and founders draw at most 600 s, so this should bite almost never (inference). A column counts it.

### Newborns, founders and inoculants

At birth a child's cap includes the overhead floor and all of its growth to come. Its starting reserve, a fifth of its share, is always well under that, so no child loses its start to the trim. Founders are capped at 0.9 of their gate plus their growth (D124, `FounderReserveCapFraction`), which is below this cap, so no founder is trimmed. An inoculant's purse is not capped. The nursery's purse could be trimmed on its first step with the switch on, so a nursery world with the switch on must size its purse under the cap.

### Gestating bodies

The gestation account stays outside the cap, as it is today. The account belongs to the litter (`World.Gestate`'s remark), and the gate already limits it, since a birth fires once the account clears the gate. The cap therefore binds gestating bodies through their reserve alone. That reserve is the half of their gain that only upkeep ever draws on.

### Computing it

The cap is derived from the body every metabolic step, on the host. That costs a sum over at most sixteen parts. Nothing is cached, so nothing has to survive a resume.

## The fat cell

Fat is a new cell type. It earns nothing and costs little to keep. It is dear to build, and it raises its body's cap. The table gives each number with its reason. All of them are parameters on the type, written into the config's table, so a world can change them without a new world tunable.

| Parameter | Value | Why |
|---|---|---|
| Upkeep | 0.5 W/m³ | Half of structural tissue, the cheapest tissue today (1 W/m³). In people, fat at rest burns about a third of what muscle does. That ratio is general physiology quoted from memory; no source was fetched for this draft. |
| Build price (tissue energy) | 2,000 J/m³ | Four times every other type's 500 J/m³. Fat must not become a cheap filler that replaces structural bulk. Over a life of age, about 2,400 s (the median for leaves dying of age in both worlds), structural costs 500 + 1 × 2,400 = 2,900 J/m³. Fat costs B + 0.5 × 2,400, so B must be at least 1,700. |
| Store room | 50,000 J/m³ | If a quarter of a 0.03 m³ body is fat, it adds 375 J. A 0.1 W body at 3,000 s already has about 300 J of room, so that is a little over double, roughly one more winter of round 62's seasons. |
| Attack, intake, protection caps | 0, 0, 0 | Fat is soft and has no mouth. |
| Toughness cap | 1 | The neutral floor (`CellType.ToughnessMax`). |
| Bud weight | 1 | D165's precedent: a new type starts at the same weight as every other. |
| Lift | 0 | It sinks as other tissue does, for the first screen. See the buoyancy subsection. |
| Joint, light | none | Like structural tissue, it allows no joint and fixes no light. |

Fat makes a richer meal by two routes without any new eating rule. A bite on a fat part takes four times the tissue joules per unit of health lost. Under D224 that same bite takes a share of the reserve in proportion to the tissue value it removed. So biting fat takes about four times the store a bite elsewhere would. In effect the store sits where the fat is, as it does in a real animal, though this is inference from the code and was not seen in a run. Remains and a swallowed body carry the fat's tissue and the full store with them.

Fat tissue is never burnt. A starving body spends its reserve, and its fat parts stay at full size and keep charging upkeep, because tissue cannot be spent and bodies do not shrink (D087). In a real animal the fat itself is the store and shrinks as it is used. Here the store is the reserve and fat is the room for it. That is a fair abstraction, and it costs a starving fat body a little upkeep for its empty fat.

The build reads the room through a virtual property on the base type. It is 0 by default and overridden by fat, so no other type's serialized form or hash changes.

### Buoyancy

Real fat is lighter than seawater. The code could carry that, but in today's worlds it would do almost nothing. The buoyancy force works per part: the body's excess density times one minus the part's lift. Lift is already a per-part value that reaches the card, so a fat part with a fixed lift needs one line in development and no change to the card's code.

Two things argue for lift 0 first. Under D064 the excess density is scaled to zero for any body smaller than the neutral volume, 0.25 m³ in round 62. Most of today's bodies are 0.02 to 0.08 m³, so for them neither fat's lift nor a float cell's lift changes anything (inference from the code). And fat with lift 1 or more would be the cheapest float in the world. A float cell costs 2.5 W/m³ plus a price for each unit of lift. Fat would undercut it, and a screen of fat would also become a screen of floating. The recommendation is lift 0 now, with lift kept as a type parameter for later, once bodies outgrow the neutral volume.

| Claim | Where |
|---|---|
| Today's types, prices, caps, bud weight, registration | `src/Evosim.Core/Cells/CellType.cs`, `src/Evosim.Core/Cells/StandardCellTypes.cs`, `src/Evosim.Core/Serialization/CellTypeJson.cs` |
| Tissue value is the sum of each part's type price times its volume | `src/Evosim.Core/Ecosystem/Metabolism.cs` (`TissueJoules`) |
| The buoyancy force and lift per part | `src/Evosim.Dynamics/Fluid.cs`, `src/Evosim.Core/Environment/BuoyancyModel.cs`, `src/Evosim.Core/Development/Developer.cs` (lift set for float cells only), `src/Evosim.Core/Genome/Genome.cs` (lift refused off a float cell) |
| The neutral volume and tissue price in round 62 | `runs/r62bd-x2-6201.md` header (`neutralV 0.25 m3`, `tissue 500 J/m3`) |

## Income above the cap goes into the water

Three things could happen to joules that arrive over the cap. The first is recommended.

1. **The water.** The excess goes into the nutrient field where the body is, as charged matter, which is what the dial does today. It is already built and booked, and a test covers it (`WorldTests.TheReserveCapTrimsAHoardIntoTheWaterAndIsOffAtZero`). It also resembles real phytoplankton, which leak surplus carbon into the water. One consequence is a new route for food: full bodies feed the absorbers round them. In an oasis world that route lands where the crowd is (inference). The screen reads it.
2. **Heat.** The excess leaves the world. This is simpler, but it destroys charged matter that D098 took care to keep.
3. **Refused income.** A full leaf fixes less light and a full mouth takes less. This is satiation, and it is realistic for a mouth. But a bite's wound still takes tissue from its victim whether or not the biter keeps it, so the refused part becomes waste anyway, and the change touches the bill and the bite.

A birth draws exactly what it draws today. The cap changes only how much a body can hold before it pays. Because the trim runs before births in the step, a lump breeder at its cap still breeds if it has reached its gate. The litter term guarantees that it can.

## The gut does not count against the cap, and waits for room

D232's gut takes in a whole killed body, with no cap for now. Each contact pass it digests into the reserve at the mouth's rate. The recommendation is that the gut stay outside the cap, and that digestion stop while the reserve is at its cap. The gut then keeps what it could not digest until upkeep makes room. A sated predator digests a large kill over a long time, which is the image this gives (inference). It changes nothing in D232's ruled meal, and no joule of a kill is thrown away.

The risk is that the gut becomes a second store with no limit. A body at its cap can still kill and swallow, so its gut can keep growing. The build reports the largest gut and the body-steps on which digestion waited at the cap. If guts grow without bound, two fallbacks exist, and both are world rules for the owner. In the first, the gut counts against the cap and a kill swallows only what fits, leaving the rest as remains. That amends D232's rule of no remains. In the second, the gut has a cap of its own from the body's volume and fat, which is the cap D232 left possible.

The digestion pause is one line in the kill-gut build's digestion pass (`DigestGuts`, uncommitted in `scratch/wt-killgut/src/Evosim.Core/Ecosystem/WorldContact.cs`). The digester gets room equal to the cap less the reserve. So fat is built on a branch that starts after kill-gut lands on main. Both builds touch `World.cs`, `RunConfig.cs`, the farm's report and environment binding, and the recorded fixtures.

## What it costs

| Cost | What it is |
|---|---|
| World tunable | One new switch for the cap's form, from D098's seconds to D229's body-and-store, off by default. `ReserveCapSeconds` is reused. Like every new tunable, it makes every older `config.json` unreadable. The two reflection tests that guard the config must see it (`RunConfigTests`, `RunConfigJsonTests`). |
| Replay | With the switch off and fat absent from the table, a world is the recorded one bit for bit, so recorded controls stand. A table that gains fat is a new realisation of every seed, because a mutation's new type is drawn from the table. |
| Checkpoint | No bump. The reserve is already checkpointed, and the cap is derived from the body. The trim total is already checkpointed. |
| Genome | No bump. Cell types are stored by name. |
| Card | None. The reserve is a host account, and the card only receives seconds of reserve each step, which the host already uploads. The kernel hash is unchanged, so no identity sitting on the card is needed. The values uploaded differ, so a world with the switch on is a new realisation. |
| Theatre | A pale yellow in `unity/Assets/Theatre/TheatrePalette.cs`. It sits outside `simHash`. |
| Reports | A header token for the form, s and fat's parameters. New stats columns: trimmed joules (cumulative, already counted by the world), living bodies within 1% of their cap, fat carriers and fat's share of living tissue volume, conceptions refused at the cap, and with the gut on, digestion steps that waited and the largest gut. A `fat` flag on the birth row beside `abs`, `jnt` and `pho`. |
| Fixtures | `src/Evosim.Core.Tests/fixtures/r42-config.json`, `r57-s2-config.json` and the crowd fixture are re-recorded. The re-recording can share D232's single re-recording if the two builds land close together. |
| Tests | The trim at the new cap. No birth is ever refused by the cap below the margin. Fat raises the cap by its room. The books close with the switch on. The switch off replays exactly. Fat survives a save and reload. A bite on fat takes the larger store share. Digestion waits at the cap. |
| Host pace | A sum over at most sixteen parts per body per metabolic step. It should be unmeasurable against the physics (inference), and the 4,500 s check reads the pace against the control. |

In agent time, the work splits like this:

- **Step 0, the checkpoint reader for leaves.** One to two hours, then minutes of machine time.
- **The build.** Half a day to a day of agent time: a Sonnet subagent builds it and runs the filtered tests, then an Opus review of an hour or two.
- **The screen.** A 4,500 s check of one seed, about an hour. Then one card group of six worlds to 24,000 s, about six hours, as r62bd's group of six took. Reading takes two hours, plus a look in the theatre before the write-up.

## A screen after D222 passes

Three things should be true before this screen runs:

- **Grazers pass D222.** Then fat's richer meal has eaters.
- **The kill-gut screen has been read.** Fat meets the gut.
- **Step 0 has said how often each cap binds leaves.**

The world is whichever passed D222, with the switch on and fat in the table where an arm says so. Its season stays as round 62 has it: winter light at 0.4 of the mean, for half of each 6,000 s year (D151).

The arms run on two seeds each, as one card group to 24,000 s.

| Arm | Cap | Fat in table | What it isolates |
|---|---|---|---|
| A | 1,000 s | yes | fat with room to buy |
| B | 1,000 s | no | the cap alone, where it binds |
| C | 3,000 s | yes | fat with almost nothing to buy, so its cost and drift. It is also the default the build ships with |

The recorded control, with no cap and no fat, is not run again, because the switch off replays it exactly.

Fat is justified if two readings hold in both seeds. First, fat's share of living tissue volume in arm A is at least twice arm C's at 24,000 s. Second, the bodies carrying fat in A use the room: a clear share of their body-steps sit above what their cap would be without fat. Signs that fat is doing what it is for would strengthen the case, but the case does not require them. One is fat carriers surviving winter more often than lean bodies of the same guild. Another is fat carriers among the migrants between islands.

A close to C reads as fat having nothing to do in this world. Fat would then wait for the moving-oasis world, where a crossing must be paid for from the store. That is the world D229 said fat matters most in.

Arm B against the recorded control shows what the cap alone does. It should bite stomachs: a quarter of them reached 1,000 s in r62bd-x2. D222's tests should hold in B. If they fail there, a 1,000 s cap is too tight to adopt with or without fat, and the next screen moves s upward.

The theatre checks one thing too. Fat bodies should look like bodies with a store, not like bodies padded out with cheap bulk.

## Decisions for the owner

1. **The cap's form.** The recommendation is D229's cap as 3,000 s of standing cost above the next litter and the growth still to come, plus fat's room. The other options: a base per cubic metre of body (about 15,000 J/m³ to bind as rarely), or D098's plain seconds. Plain seconds can forbid small bodies from ever breeding, because round 62's 50 J overhead floor dominates their gate. Under the recommended form a lineage whose margin gene rises above 3,000 s cannot breed, which should happen almost never.
2. **The value of s.** The recommendation is to ship 3,000 s as the default and screen it beside 1,000 s. At 3,000 s the cap would have bound at most 0.25% of stomachs in the two worlds measured. That is about a winter of round 62's seasons, and it changes today's world very little. It also leaves fat almost nothing to buy. At 1,000 s the cap reached a quarter of r62bd-x2's stomachs and 3% of r62cs-w500's, so fat has something to buy but the world changes too.
3. **Income above the cap.** The recommendation is to send it into the water as charged matter, as the dial already does. It keeps the books and feeds the absorbers. The other options are heat, which destroys matter, or refused income (satiation), which touches the bill and the bite and still wastes what a wound tears off.
4. **The gut and the cap.** The recommendation is that the gut not count, and that digestion wait while the reserve is full. D232's meal is unchanged. The risk is guts that grow without bound in sated killers, and columns watch for it. The fallbacks amend D232: the gut counts and a kill swallows only what fits, leaving remains; or the gut gets a cap of its own.
5. **Fat's prices.** The recommendation is 0.5 W/m³ to keep, 2,000 J/m³ to build and 50,000 J/m³ of room. Attack, intake and protection 0, toughness 1, bud weight 1. The build price keeps fat from being a cheaper bulk than structural tissue. The room means a quarter of a typical body in fat roughly doubles its store.
6. **Fat's buoyancy.** The recommendation is lift 0 for the first screen, so that fat sinks as other tissue does. Real fat floats and the code could carry it with no change to the card. But under D064 it would do nothing for bodies under 0.25 m³, which is nearly all of today's, and above that it would be the world's cheapest float.
7. **The screen.** The recommendation is arms A, B and C as above, two seeds each, one card group to 24,000 s, after a 4,500 s check of one seed. It runs after grazers pass D222 and the kill-gut screen is read. An overshoot is rolled back by lowering fat's room, at the delegation D232 gives for digestion.
8. **Step 0** is agent work and needs no ruling: a reader that measures leaves' reserves from a recorded checkpoint before the constants are fixed. It is listed so the owner knows the leaves' figures are still estimates.
