# Proposal: matter kinds and a digestion gene, so that what a body eats is a choice it evolves

*Draft for the owner, 2026-10-30, from their idea of matter types and entropy levels and their addition the same day: plants cheap to kill, animals much harder, a kill paying because an animal is concentrated matter. World rules are the owner's; this is what they rule on.*

## The point

Today one mouth eats everything with one yield, so diet is whatever is touched, and the cheap-mouth screens (`r63g`) made grazers that took about half their bite income from other animals, mostly each other. I propose that every charged unit of matter has a **kind**, read from the tissue it is in, and that each genome carries a **digestion gene** that sets how much of each kind its owner keeps. A specialist keeps nearly all of its own food and almost none of the other kind, so a plant-eater that bites a grazer pays for a wound and gets no meal. **Three entropy levels** are the three places matter already lives (living body, dead matter, dissolved mineral), with one change: remains that close go to the water as food, not as heat. Matter is conserved on every leg and sits under D261's fixed total. The minimal build touches the bite, the swallow, the remains' closing and one gene, adds no per-step cost anywhere, and is screened in one round.

## 1. The mechanism in plain words

**One matter, as today, with a kind on the charged unit.** The economy (D098, `logbook/specs/economy-spec.md`) has one matter in two states: a *charged* unit carries ρ = 100 J, a *spent* unit none, and nothing creates or destroys a unit. I keep that whole. The kind is not stored: it is read from where the unit sits.

| Kind | Where it is | Biology it stands for |
|---|---|---|
| **P, plant matter** | the tissue of a photosynthetic part, and the share of the body's store (reserve, gestation) a bite on it takes | leaf: abundant, does not flee, poor food |
| **A, animal matter** | the tissue of every other part, and the store taken with it | muscle and fat: dense, close to the eater's own make-up |
| **D, detritus** | marine snow (`World.Nutrients`) | dead matter the bacteria have begun on |

A bite takes a share of one part's tissue and, with D224's dial, the same share of the body's reserve (`WorldContact.OfferBite`). Under this rule the offer is one lump of one kind, the bitten part's, so the kind costs no state.

**Three entropy levels, discrete: the three places matter already lives.**

| Level | Meaning | Who can take it | What moves it on |
|---|---|---|---|
| **0, fresh** | charged matter in a living body | a mouth, by contact (D219) | death, or a bite |
| **1, dead** | charged matter in remains (D220) or as marine snow | a mouth on remains, an absorptive cell on snow | the remains window closing, and remineralisation `1 − e^(−r·dt)` a step |
| **2, spent** | dissolved mineral, `World.Matter` | a photosynthetic cell, by fixation `min(L, U·ρ)` | fixation |

The level says how much of a unit's energy is still in an eater's reach and what it takes to get it, which is what the owner means by entropy. The levels are free because each is a different container; §4 costs the smooth gradient.

**One change to the legs.** Today remains whose window closes are *burnt*: the joules leave as heat and the units go spent (D220). In `r62ce` that was 7.1 MJ burnt against 62 kJ ever bitten (D224). I propose that closing remains deposit as marine snow at their point, charged, kind D. Level 0 then flows to level 1, level 1 to level 2 by remineralisation alone, with the bacteria's heat booked where it is now (`EnergyOut += moved`). Scavengers and absorbers get the larder the burning destroys.

**What each leg yields.** An eater with efficiency *e* for the kind it bit keeps *e* of the offer in its reserve and returns the rest at the bitten body's point as charged snow (faeces, kind D), as `SettleBites` returns the intake waste today. The handling cost (`HandlingCostPerJouleEaten`, today on absorptive draws only) is charged on the whole offer, so biting the inedible costs energy. A swallowed kill (D232) has its yield applied at the swallow: the keepable share enters the gut, the rest goes to snow at the kill, and the gut stays one number.

**Conservation.** Every leg moves units between a body's accounts, the snow and the spent field, or changes state in place; nothing books a unit in or out, so `MatterResidual` reads 0 as today. Under D261 the influx and burial are 0, the total 25,000 units, and the cycle closes: spent → plant tissue → grazer store (kept) and snow (waste) → predator store or remains → snow → spent. D074's warning stands: a conserved stock once locked 97% of itself in bodies and stopped selecting (logbook/0055-0057). There matter left a body only at death; here it leaves at every bite's waste and every closing of remains, so the lock is weaker, and the screen reads `mat locked` to see.

## 2. How diet becomes an evolved choice

**The gene.** `ReproductionTraits` gains `Diet`, 0 to 1, drawn by founders uniformly, mutated by the margin's reflected step, and in `SpeciesDistance`. Two efficiencies are cached on the body at birth:

- plant matter: `e_P = E_max × (1 − Diet)^α`
- animal matter: `e_A = E_max × Diet^α`
- detritus: `e_D`, one world tunable for everyone (the bacteria did the digesting), or a third gene in the full design.

With α = 2 a plant specialist keeps `E_max` of leaf and nothing of grazer; a body in the middle keeps a quarter of each, under half of a specialist's take on its own food. **That is why a specialist beats a generalist, and it is the whole cost**: no enzyme upkeep to tune, the trade-off is the price. α dials how hard the choice is; α = 1 is the null in which a generalist equals two specialists, and the screen runs it to prove the mechanism does the separating. D262 asks for overpowered, so `E_max` starts at 1.0.

**Why the cannibal stops.** In `r63g-b` a grazer bit a grazer at a leaf's yield, and a grazer's reserve is concentrated: one bite of neighbour was worth many of leaf. Under the gene a plant-eater's bite on a grazer keeps `e_A ≈ 0`, pays handling on the offer and risks a wound back, so it loses on every such bite; what it tears off goes to snow. The only grazer that gains from eating grazers has moved its Diet toward 1 and lost its leaf income. That is the predator, and it is a different lineage.

**The owner's three refinements, and where each lives.** These are general knowledge about trophic ecology and not verified against `research/`.

1. *Quality.* Herbivores assimilate something like a fifth to a half of what they ingest, carnivores well over half: plant structure is cellulose and lignin an animal gut cannot break, and plant tissue is carbon-rich and nitrogen-poor against an animal's own ratio (ecological stoichiometry's C:N argument). Quality is `E_max` per kind, `E_maxP` below `E_maxA`, say 0.5 against 0.9 once dialled back. *Concentration* has a lever already in the config: `TissueEnergy` J/m³, one number for every part. Per kind, with plant tissue cheaper, a plant is cheap to build and to kill, and an animal of the same size a bigger prize and investment. Second option: it reprices every plant body, not only its edibility.
2. *Partial against whole.* Grazing takes a share of a part and the plant heals it (D220-D221); a kill swallows the whole body (D232). Both stand; the gene decides who keeps what.
3. *Resistance.* "Harder to kill" lives in the bodies: `Toughness` (health per volume, paid as upkeep), `Protection` (armour), `Attack` and swimming away are genes today. The design adds no flat rule; it adds that a kill pays only a body equipped to digest it, so evolved defences and the predator's digestion are selected against each other.

**Plants' defences fall out with one more gene, optional.** A per-node `Digestibility`, 0 to 1, on the prey side multiplies the eater's efficiency and costs upkeep per unit of indigestibility, as toughness does; a leaf lineage buys inedibility with its margin. Second option.

## 3. What it replaces, simplifies and leaves alone

| Today | Under the proposal |
|---|---|
| `IntakeWasteFraction`, one waste for every bite and gut | replaced by `1 − e` per kind and eater; the dial goes |
| `HandlingCostPerJouleEaten` on absorptive draws only | on every bite's offer and every swallow as well |
| `BiteTakesStore` (D224) | stays; the store comes with the part's kind |
| the remains window (D220), closing as heat | window stays; closing deposits as snow |
| `KillSwallowsBody` (D232) | stays; yield at the swallow, gut unchanged |
| `MatterInfluxPerSecond`, `MatterBurialPerSecond` (D074) | untouched; 0 under D261 |
| `Toughness`, `Protection`, `Attack`, healing, lit area lost to grazing, `IntakeScale`, `BiteDamageScale` | untouched; the first four are the resistance lever |
| the absorptive draw from snow | keeps `e_D`; DESIGN.md §5A.3's yield table (dead high, plant moderate, animal low) becomes a gene's function |

DESIGN.md §5A.3's rule that herbivore and carnivore are behavioural outcomes of what a creature happens to eat, not body types, is what this reverses; the ruling supersedes that passage.

## 4. Compute cost

**Per body, per step: nothing new.** The two efficiencies are computed once at birth and cached as two floats on `Organism`. A bite does one branch on the bitten part's cell type and one multiply, which `SettleBites` already does with the waste fraction. The gut keeps one number; there is no new pass over the living.

**Per field cell: nothing.** The snow stays one field with one remineralisation leg, because kind D collapses P and A once the matter is dead. A smooth gradient would add an age field beside the snow (one float a cell, mixed and advected, plus a decay pass) and an age read on every absorptive draw; kind-tagged snow (`P1`, `A1`) doubles the field work. Neither is in the minimal build.

**On the card: nothing.** The bite, the gut and the fields are Core, stepped on the CPU in the farm and the card run alike. The efficiencies come from one gene by `MathF.Pow` with a fixed exponent, the same on every thread, so bit-identity holds as it does for the waste fraction.

**Formats.** The gene bumps `GenomeJson.FormatVersion` (11 → 15, with the central and family variants), refusing every stored genome and snapshot, as every gene has. New tunables (`DietExponent` α, `PlantEfficiencyMax`, `AnimalEfficiencyMax`, `DetritusEfficiency`, `RemainsCloseToSnow`) refuse every earlier `config.json`. The checkpoint gains no per-body field; `Diet` rides in the genome it holds. Three recorded fixtures need re-recording (`src/Evosim.Core/CLAUDE.md`). With every dial off and `Diet` ignored the world is `r63g`'s bit for bit: the control.

**Stats.** Per world: kept joules by kind and level (`keptP0`, `keptA0`, `keptD1`), waste by kind, remains closed to snow. Per body: the death row the `r63g` read asked for, carrying lifetime kept joules by kind and the `Diet`, which is what the screen reads. The table gains `diet` (mean and a two-bin split of living biters) and `kept P/A/D` shares.

## 5. Options

**Option A, the minimal build (recommended).** Two kinds by part type, three levels as places, remains closing to snow, one `Diet` gene with α = 2 and both `E_max` at 1.0, handling on bites, yield at the swallow. Record: one genome and one config bump, so every earlier run is a recorded world and no inoculum crosses. Rounds: one screen (§6), about four hours of card time for four worlds to 24,000 s at `r63g`'s pace, after an afternoon's build and the fast suites. Machine: no new per-step cost. Risk: D074's lock returns under a fixed total, which the screen reads; and at `E_max` 1.0 plant-eaters may eat the leaves out, which is the overshoot D262 asks for.

**Option B, A plus concentration and defence.** Adds per-kind `TissueEnergy` (plant tissue cheaper per m³), the prey-side `Digestibility` gene with its upkeep, and `E_maxP` below `E_maxA`. Record: a second bump, or the same one if built together. Rounds: three arms over A's winner. Machine: one float a part, no per-step cost. Risk: repricing plant tissue moves founding and the count at the same time as the diet, and two levers in one screen cannot be read apart. After A reads.

**Option C, the full design.** Kind-tagged snow fields, a continuous decay age on remains and snow, per-kind enzyme genes with upkeep, a third gene for detritus: the owner's idea in full, for when A and B have separated the guilds and the next limit is the dead. Machine: about double the field work and a larger checkpoint. Risk: days of build before anything is read. Not now.

**Recommendation: A, then B on A's reading, and C only if the dead become the question.**

## 6. The first screen

Four new worlds on seed 6202, founded as `r63g-b` (cheap mouths, the arm that raised grazers), one card group to 24,000 s:

1. **Control**: `r63g-b` itself, not re-run (bit for bit with every dial off).
2. **Kinds, α = 2, founders' `Diet` uniform.** The main arm.
3. **Kinds, α = 1.** The null: no specialist advantage.
4. **Kinds, α = 2, founders at `Diet` 0.** Every founder a plant-eater: does a predator lineage arise?
5. **Kinds, α = 2, remains still burnt.** The larder change apart from the gene.

Measures, per world and per 3,000 s bin: the share of biter deaths that are kills by biters (`r63g-b`: 57%); a ten-bin histogram of `Diet` across living biters; per lineage, from the death rows, the share of kept income from P0, A0 and D1; the leaves' count and D244's grazer floor; `mat locked` and the matter residual; the pace.

**It works if**, in arm 2 and not in arm 3, the biters' `Diet` is bimodal by 12,000 s, lineages under 0.3 take at least 0.8 of their kept income from plants, lineages over 0.7 at least 0.6 from animals, and the second group makes the kills of biters, so the cannibal share among plant-eaters falls under 0.2. Arm 4 says whether predation is reachable from grazing; arm 5 whether the larder matters. A unimodal `Diet` at the middle means the choice does not pay, and α or the handling charge is the next dial. Every biter at `Diet` 1 means animals are too cheap to kill, and resistance (option B) is next. The matter residual reads 0 on every row or the build is wrong.

## 7. Open questions for the owner

1. **Does a part's kind follow its cell type**, photosynthetic parts plant and every other part animal, so a body with both carries both? The alternative is one kind per body by guild.
2. **Does a bite's share of the store take the kind of the part bitten?** Yes makes cannibalism fail for plant-eaters; no leaves a grazer's reserve edible to everyone, today's cheap kill.
3. **Do remains that close go to the water as marine snow rather than as heat?** This reverses a clause of D220.
4. **Is the trade-off alone the cost of specialising, or does each digestion also pay upkeep?** I recommend the trade-off alone first.
5. **One detritus efficiency for everyone, or a third gene?** I recommend one tunable first.
6. **May plant tissue be cheaper per cubic metre than animal tissue, and in which option?** I recommend B.
7. **Does the prey-side `Digestibility` gene belong in the first build or the second?** I recommend the second.
8. **Under D261, do influx and burial both read 0 for this screen**, so the 25,000 units are the whole world?
9. **The starting values**: α = 2, `E_max` 1.0 for both kinds, `e_D` 0.5, handling 0.1 on bites. Any the owner wants otherwise.

*Biology cited above (assimilation efficiencies, C:N ratios, trophic transfer near a tenth) is general knowledge, not from a paper in `research/`, and is not independently verified.*

## The coordinator's review, 2026-10-30

- **Option A does not make animals harder to kill.** It makes a kill pay only a body built to digest it. The owner asked for both. In A, "harder to kill" is left to the genes that already buy it (`Toughness`, `Protection`, swimming away), and the only world-side lever is option B's denser animal tissue. If the owner wants animals harder to kill from the start, that is a third lever, outside this draft: for example animal tissue's health per volume above a leaf's.
- **Remains closing to snow is a large feeding of the absorbers.** In `r62ce` it would have put about a hundred times the bitten joules into the water. Arm 5 separates it from the gene. The risk is an absorber boom that hides the diet result, and the screen should read the absorbers' count beside `Diet`.
- **Kin is untouched.** Under the gene, a predator lineage at `Diet` near 1 still eats its own kind at full yield. The recognition proposal being drafted (`scratch/recognition-proposal/`) covers that, and the two should be ruled together.
- **The format numbers check:** genome formats 11 to 14 exist (`GenomeJson.cs`), so the new range starts at 15.
