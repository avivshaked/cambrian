# Proposal: what a body eats, whom it spares and what it smells become one evolved choice, screened in one round

*Draft for the owner, 2026-10-30. It joins the matter-kinds and recognition drafts in `proposals/` into one mechanism,
on the owner's request for a unified proposal and a means of testing it for the next world run. It reads the `r63g` and
`r63h` screens and the coordinator's reviews at the end of both drafts. World rules are the owner's; instruments, reads
and dials within a ruled rule are agent work, and each rule below says which it is. Absorbed into DECISIONS.md on
ruling, then deleted.*

## The proposal in one paragraph

Today one mouth eats everything at one yield and knows nothing about what it touches. The cheap-mouth screens made
grazers plentiful and then made them live on each other. More than half their income is other animals, more than half
of them die in a grazer's mouth, and one kill in six is close kin. I propose that every unit of matter has a kind read
from the tissue it sits in, plant or animal, with dead matter a third kind. Each genome carries a diet gene that sets
how much of each kind its owner keeps, so a plant-eater that bites a grazer pays for the wound and keeps nothing. Every
animal then smells of its tissue, and the smell carries a short heritable signature that drifts a little at each birth.
A mouth tastes that signature in what it touches, and a heritable restraint decides whether it bites. A wounded body, or
a gut digesting a kill, leaks an alarm carrying the victim's signature. Remains that close go to the water as marine
snow rather than as heat. The two halves are one mechanism: the diet gene decides what a meal is worth to whom, and the
smell decides what a body knows before it bites. Whether a lineage spares its kin is left to evolution, and the screen
measures what evolution does. Every part sits behind its own switch, off by default, so every recorded world replays.
The first values are overpowered, per D262. One screen of five worlds on seed 6202, founded as `r63g-b`, reads whether a
grazer lineage comes to live on living plants and whether kin restraint evolves.

## What the two screens showed

The screens are in `logbook/specs/r63g-read/` and `logbook/specs/r63h-read/`. Mouths at a tenth of their usual price
raised grazers five times over the control, and at a hundredth six to fourteen times. Grazers did two thirds to four fifths
of the leaf killing from 9,000 s on. The leaves still grew from the middle window to the last in every arm, by 1.17 to
1.77 times. Over the last window a grazer's bite income came 0.36 to 0.52 from living leaves and 0.42 to 0.57 from other
living bodies. The control's shares were 0.65 and 0.26. Being eaten was 55 to 76 % of grazer deaths, almost all by other
grazers, and close kin were 13.5 to 18.8 % of those kills, cousins counted as unrelated. A mouth reads only the tissue's
type, its joules and whether it is alive, so nothing in the world tells a leaf from a sibling except its price. A cheaper
mouth makes a cheaper predator as much as a cheaper grazer, and the cost dials have done what they can.

Two readings shape the design. Grazers swim at 36 to 57 cm/s but do not steer to leaves, because a newborn starts a
median 1.5 m from one, and no sense will make them steer while leaves are a carpet. And the run's files cannot show a
body's own income, since the code writes it only into checkpoints. The lineage-level gate below therefore needs a death
row that carries it.

## Where the drafts met, and how I settle each place

### One screen, with arms that hold each lever apart

The recognition draft asked to screen recognition alone first, since two levers in one screen cannot be read apart. I
settle for one screen whose arms hold each lever apart. Kin restraint only costs anything inside a lineage that eats
animals, and the diet gene is what makes such a lineage. Restraint screened on today's world would be measured among
grazers that eat each other at a leaf's yield. The gene removes that world, so the reading would not carry over. One
group of five worlds also costs about half the card time of two screens.

### A mixed body's kind follows the part bitten

A photosynthetic part is plant, every other part is animal, and the share of the body's store a bite takes carries the
bitten part's kind. The odour follows the same rule, since a body emits in proportion to its non-photosynthetic tissue.
So a mixed body smells partly of animal and is partly plant to eat, with no state kept per body. The alternative is one
kind per body by guild. It needs a guild to be defined and stored, and it would make a grazer's reserve edible to every
plant-eater that bites it.

### A killer's yield is applied at the swallow

A kill still swallows the whole body into the gut (D232). The victim's joules split into plant and animal by its
tissue, with its store pro rata, and the eater's two efficiencies apply then. The kept share enters the gut, and the rest
goes to snow at the kill. The gut digests at the mouth's rate with no further waste. It keeps one more number, the
animal joules still in it, which the alarm leak reads. Today the gut keeps 0.8 of what it digests, from the intake waste
of 0.2; I checked this in the digestion pass. Under the gene that 0.8 becomes the eater's efficiency for animal matter.
The recognition draft's Hamilton sum (its section 3) then reads with that efficiency in place of 0.8.

### Animals need their own "harder to kill" lever

The diet gene does not supply one. It makes a kill pay only a body built to digest it, so it removes the motive, and it
does not change how many bites a grazer takes to die. The owner asked for both. I propose one world tunable, a health
scale for animal tissue, that multiplies the health per cubic metre of every non-photosynthetic part. The genes that buy
defence today (toughness, armour, swimming away) stay as they are. I recommend building the lever and starting it at 1,
neutral, in this screen. The profit of a kill is the lever under test, and a second lever in every arm cannot be read
apart from it. The health scale is the first dial after the screen if plant-eaters still die to each other's wounds, or
if a predator lineage strips the grazers.

### Remains close to snow, in an arm of their own

I keep the change and give it an arm. In `r62ce` the burning destroyed about a hundred times the joules that bites ever
took (D224), so sending that to the water is a large feeding of the absorbers. The risk is an absorber boom that hides
the diet result, and the screen reads the absorbers' count beside the diet histogram. Under D261's fixed total nothing
changes for matter. Burning turns units spent, snow keeps them charged, and both conserve the count; it is energy that
changes hands. For a world meant to be an ocean, dead matter becoming snow is the right shape.

### D235's condition is not met, and D262 is the better case

D235 gated the animal smell on grazers living on living leaves. The control draws 0.68 of its bite income from leaves,
but 71 % of its grazers starve at a median age of 72 s, and every screen has failed D222's living test. I agree with the
coordinator that the condition is not plainly met. The case for building the smell now is D262, which asks for a feature
to enter overpowered so that the screen shows whether evolution wants it. The owner's own question about kin is the other
half of the case, since no sense in the world can answer it today. The owner rules on that reading.

### Two smaller settles

The detritus efficiency starts at 1.0, so absorbers are unchanged by the gene and only the remains change moves them.
The matter-kinds draft proposed 0.5. But as far as I can find, the absorptive draw passes through no intake waste today,
since the waste fraction appears in the bite and gut code only. Any value under 1.0 would cut absorbers' income at the
same time as the larder grows. The matter budget for this screen stays `r63g-b`'s open one, an influx of 3.6 units/s at
the islands and burial at the floor. The owner ruled on 2026-10-30 that grazers are overpowered before any limit on
matter, and that D261's fixed total is held and still stands. D261 waits for the screen after this one.

## The rules

Each rule says whose it is and where it starts. A world rule is the owner's to rule on. Agent work is an instrument or a
dial within a rule already ruled.

1. **Every charged unit has a kind** (a world rule), read from where it sits and stored nowhere. Plant matter is the
   tissue of a photosynthetic part and the share of the store a bite on it takes. Animal matter is the tissue of every
   other part and its store share. Detritus is marine snow.
2. **Three entropy levels are the three places matter already lives** (a world rule, as a reading of what exists). Fresh
   matter is in a living body and is taken by a mouth on contact. Dead matter is in remains or snow and is taken by a
   mouth or an absorptive cell. Spent matter is dissolved in the water and is taken by a photosynthetic cell. Each is a
   different container, so the levels cost nothing.
3. **Remains that close deposit as snow** (a world rule) at their point, charged, as detritus, rather than burning to
   heat. This reverses one clause of D220. The window itself stays at 500 s as `r63g-b` has it.
4. **A diet gene** (a world rule; its values are agent dials) in the reproduction traits, 0 to 1, drawn uniformly by
   founders and mutated by the margin's reflected step. Two efficiencies are cached on the body at birth: for plant
   matter `(1 − Diet)^α` times a maximum, for animal matter `Diet^α` times a maximum. The trade-off is the whole cost of
   specialising, and there is no upkeep for an enzyme. It starts at α = 2 with both maxima at 1.0, so a specialist keeps
   everything of its own food and a body in the middle a quarter of each. The detritus efficiency is 1.0 for everyone.
5. **A bite's offer is one lump of one kind** (a world rule), the bitten part's. The eater keeps its efficiency's share in
   its reserve, and the rest goes to snow at the bitten body's point, as the intake waste does today. The handling cost,
   today 0.1 J per joule on absorptive draws only, is charged on the whole offer of every bite, so biting the inedible
   costs energy. A swallowed kill is split by kind at the swallow, as above. The intake waste fraction is replaced by one
   minus the efficiency and reads 0 under the switch.
6. **A health scale for animal tissue** (a world rule; its value is the first dial after the screen), one tunable
   multiplying the health per cubic metre of non-photosynthetic parts, starting at 1.
7. **A signature of three numbers** (a world rule) in every genome, each 0 to 1, drawn uniformly by founders. At every
   birth each number takes a Gaussian step of fixed size, reflected at the bounds. The step is a world tunable and not a
   gene, so selection cannot slow the drift. The signature does nothing else; it marks descent. The step starts at 0.02.
8. **A mouth tastes** (a world rule) the signature of the living tissue it touches, as a likeness `exp(−d²/λ²)` with *d*
   the distance between the two signatures. A heritable restraint θ in each genome, 0 to 1, founders uniform, mutated as
   other traits are, decides the bite: a mouth does not bite living tissue whose likeness is above θ. A refused bite makes
   no wound and pays no handling. Remains are eaten as now. λ starts at 0.1, at which a parent and child read about 0.89,
   siblings about 0.79, bodies ten births apart about 0.3 and unrelated founders about 0.
9. **Every animal smells** (a world rule) of its non-photosynthetic tissue. The odour spreads as a cone on the 2 m grid the
   leaf smell uses, filled once a metabolic step and joined at the ocean's seams. Each cell holds five sums over the
   bodies whose cones reach it: the strength, the strength times each signature number, and the strength times the
   signature's squared length. A reader subtracts its own cone and computes the strength-weighted mean squared distance
   between its signature and those around it, with no pass over the bodies. The range starts at 6 m. This is D235's
   animal smell with identity added.
10. **A wound leaks alarm** (a world rule), in proportion to the joules lost that step, carrying the victim's signature
    into a second five-sum grid. A gut digesting animal joules leaks the same alarm, in proportion to the animal joules
    left in it, carrying its prey's signature. Leaves leak nothing, so D167's rejection of a leaf alarm stands. The range
    starts at 10 m.
11. **The brain reads five new scalars** (agent work within D020 and D146): animal odour strength and unlikeness, alarm
    strength and unlikeness on every smelling part (only noses, in a world with noses), and taste on every mouth part.
    Each is sampled once a metabolic step on the host and handed to both engines, so the card computes nothing new. No
    reading carries noise to start.
12. **No rule about kin is imposed** (a world rule, as the absence of one). Whether a body spares its kin is θ's to
    evolve, and the screen reads it.
13. **The instruments** (agent work) are the stats columns and death-row fields named in the screen below.

## What it costs

Nothing here is measured, and the pace mark below is the check. The `r63g` world is 300 by 300 m
and 25 m deep, so a 2 m grid holds about 292,500 cells. Arm B ended with about 430 animals among 5,771 bodies. The table
gives my estimates of each part's cost per metabolic step on the farm's host and on the card, and what it adds to the
stored formats. A metabolic step is 0.5 s, fifty physics steps.

| Part | Farm host, per metabolic step (estimate) | Card | Format |
|---|---|---|---|
| Kinds and the diet gene | nothing per body; one branch and one multiply per bite, as the waste fraction does today; two floats cached at birth | nothing | genome bump |
| Remains to snow | one deposit per closing, in place of one burn | nothing | one tunable |
| Animal health scale | nothing per step; one multiply at a part's birth | nothing | one tunable |
| Signature, taste, restraint | one distance and one comparison per mouth contact | nothing | genome bump |
| Animal odour grid | each animal adds its cone to about 343 cells at 6 m, five floats each: about 0.7 M adds for 430 animals, 3.4 M for 2,000, against the leaf smell's fill today | nothing; the grid stays on the host | 5.9 MB, derived, not checkpointed |
| Alarm grid | sources are bitten bodies and full guts only, tens a step | nothing | one float per body, the gut's animal joules, checkpointed with the gut |
| Readings | eight corners times ten floats per smelling part: about 4 M loads for 48,000 parts (a guess at 6,000 bodies of eight parts), a tenth with noses | one upload of five floats per part per block, about 1 MB, about 0.05 ms a block at the pinned rate the card's notes give | nothing |

The fills split rows over the threads and add every source in list order, as the remains smell's fill does, so the sums
are the same at any thread count. Sampling the readings on the host avoids new grid reads in the card's kernels, where
every product must be guarded against the fused multiply-add. At `r63g`'s pace a simulated second takes about 385 ms of
wall time, and I expect the build to add a few per cent to that, an inference from the sizes above. If the pace mark
fails, the grids are the part to cut, and arm S below is the world without them.

## The build

### What changes in Core

The cell type's photosynthetic flag gives a part's kind, and the body caches its two efficiencies at birth. The bite
offer carries its kind. The settling pass applies the efficiency and the handling charge, the swallow splits the victim
by kind, and the gut's digestion keeps everything it digests. The closing of remains deposits to the snow field instead
of burning. The reproduction traits gain the diet gene, the three signature numbers and the restraint, with mutation and
the species distance. Two new grids are built on the leaf smell's grid code, with five sums a cell. The contact record a
mouth reads gains the taste. The brain's sense pool gains the five channels, which the one brain of D146 wires at birth
as it wires every sense. A bitten body and a digesting gut leak into the alarm grid.

### What changes in the farm and the tests

The stats row gains the columns named below. The death row in the lineage file gains the body's lifetime kept joules by
kind, its diet, its signature, its restraint and its refused bites. The card transport uploads the five readings per part
per block. The two reflection tests catch every new tunable. A conservation test runs with kinds on, and a test places
one body alone and asserts it smells nothing of itself. The three recorded fixtures are re-recorded.

### The format bumps and the switches

The genome gains four genes in one bump. Formats 15 to 18 take one each of the four variants that 11 to 14 carry today
(plain, central brain, shape family, both). Or one format 15 records which extensions it holds. The builder chooses, and
the reader refuses anything else. The new tunables and switches refuse every earlier `config.json`, as every tunable has.
The checkpoint gains one float per body, the gut's animal joules, written only when the gut switch is on, as the gut's
own joules are today. Every switch is off by default, so every recorded world replays bit for bit.

| Switch | What it turns on |
|---|---|
| `MatterKinds` | the kinds, the diet gene, handling on bites, the yield at the swallow, the waste fraction replaced |
| `RemainsCloseToSnow` | remains deposit as snow at closing |
| `KinTaste` | the signature's drift, the taste and the restraint; the genes exist in every genome and do nothing with it off |
| `AnimalOdour` | the five-sum odour grid and its two readings |
| `AlarmOdour` | the alarm grid, its two readings and the gut's animal joules |
| `SignatureFresh` | the null's switch: the signature is redrawn at every birth |

The tunables are the diet exponent, the two efficiency maxima, the detritus efficiency, the animal health scale, the
signature step, the likeness scale and the two ranges.

### Agent time and card time

The economy half and the grid half are separable, so two builders can take them in parallel, an afternoon each, then the
fast suites. The card time for the screen is about four hours, estimated from `r63h`'s 2.8 h for three worlds in one
group, with five worlds sharing the card.

## The pre-registered screen

Five worlds on seed 6202, founded from `r63g-b-6202`'s config, in one card group to 24,000 s. That config has mouths at a
tenth of the control's price, intake scale 16 and bite damage 1. The store share and the gut are on, the remains window
is 500 s, handling is 0.1 on draws, the brain is the central one and the matter budget is open. Before the group launches, the new build
with every switch off replays the first 4,500 s of `r63g-b-6202`, and its digest must match bit for bit, as D232's screen
checked. A difference is a build fault and nothing launches. The recorded `r63g-b-6202` is the control and is not run
again.

| Arm | Switches | Question it answers |
|---|---|---|
| F, full | every switch on | the proposal as a whole |
| N, the null | F with the signature redrawn at every birth | restraint without kinship: does the kin reading track kin, or does restraint fall for some other reason |
| D, diet alone | kinds, diet, remains to snow; no taste, restraint, odour or alarm | what the matter half does by itself; the cannibal and kin shares with no way to know kin |
| B, remains burnt | F with the remains still burning | the larder apart from the rest |
| S, no smell | F without the two grids; taste and restraint stay | the cost and the fleeing effect of the odour, and a fallback if the pace fails |

### The measures

Every measure is read per world and per 3,000 s bin, with the last window 21,000 to 24,000 s. The existing columns
`biteKeptLeaves`, `biteKeptOthers` and `biteKeptRemains` stay, classified by the bitten body as today, with the swallowed
gut beside them. A biter, here and in the reads, is a living body with a mouth part and no photosynthetic part. A
lineage is traced by the parent field to its founder.

| New column or field | Meaning |
|---|---|
| `keptPlant`, `keptAnimal`, `keptDetritus` | kept joules by the matter's kind, cumulative, on the stats row |
| `offerLost` | joules offered and not kept because of the kind |
| `remainsToSnow` | joules remains deposited at closing |
| `dietMean`, `dietLow`, `dietHigh` | the mean diet of living biters, and the shares under 0.3 and over 0.7 |
| `restraintMean`, `restraintLow`, `restraintHigh` | the mean θ of living biters, and the means among biters under and over 0.5 in diet |
| `bitesRefusedKin`, `bitesRefusedOther` | refused bites, split at a likeness of 0.5 |
| `sigSpread` | the mean pairwise signature distance over a sample of 200 living animals |
| `odourMean`, `alarmMean` | the mean readings over smelling parts |
| death row: `kp`, `ka`, `kd`, `diet`, `sig`, `theta`, `refk`, `refo` | the body's lifetime kept joules by kind, its three genes and its refused bites |

From these come the reads. The kin read of `r63g` gains cousins as a class and splits the kills by the killer's diet
bin. The income split is read by lineage from the death rows. The motion read of `r63g` is run for movement away from
alarm sources. The leaf, absorber, matter and pace columns are read as every screen reads them.

### The marks

The marks are fixed now, and each names the arm it is read in.

1. **The gate**, in F over the last window. At least one grazer lineage holds 30 or more living members at every sample
   of the window (D244). Its pooled lifetime kept bite income, bites and swallows together, is at least 0.8 from plant
   matter. The lineages that meet both make at least 0.6 of the leaf deaths in the window, as `r63g`'s gate 3 asked. This
   is the mark the owner asked for.
2. **The guilds separate**, in F and D. The diet of living biters is bimodal by 12,000 s and stays so to the end: the
   share under 0.3 is at least 0.5. The share between 0.3 and 0.7 is at most 0.2.
3. **Kin restraint**, in F against N. Close kin's share of biter-on-biter kills, cousins excluded for comparison with
   `r63g-b`'s 0.16, falls below 0.08 in F and stays above 0.12 in N. The restraint among plant-eating biters falls below
   0.4 by 12,000 s in F and stays between 0.4 and 0.6 in N, near the founders' 0.5. If instead the restraint in F rises
   above 0.6, the owner's claim that nothing selects against eating kin holds in this world, and the logbook says so. The
   restraint among meat-eating biters is reported and carries no mark; it says whether a hunter spares kin at a meal's
   cost.
4. **The cue holds**, in F. The signature spread at 24,000 s is at least half its value at 3,000 s.
5. **A hunter exists**, in F at 24,000 s. A lineage with 30 or more living members takes at least 0.6 of its kept income
   from animal matter. This is reported with no pass or fail; it says whether the ocean has a second trophic level.
6. **The larder**, F against B. The absorbers' count in F at 24,000 s is at most twice B's, and the grazers' share of leaf
   deaths in F is not more than 0.1 below B's.
7. **The smell**, F against S. The share of biter deaths that are kills is at least 0.1 lower in F than in S. Biters within
   10 m of an alarm source move away from it in the motion read more than S's biters do. Either failing says the alarm did
   nothing readable in 24,000 s.
8. **The leaves**, in F at 24,000 s, are fewer than `r63g-b`'s 5,338, and their late-over-mid ratio is under its 1.43. This says the world moved toward control. `r63g`'s gates 1 and 2 are read beside it and expected to fail, since
   the matter budget is untouched.
9. **The books and the pace**, in every arm. The matter residual stays under 1e-4 units and the audit residual under
   0.01 J on every row, as `r63h` read them. No world falls under 0.5 times real time, and one that does is stopped at
   once. The group's pace is read against `r63h`'s 1.7 to 3.2 times real time with the world count in mind, and S against
   F says what the grids cost.

### What each failure means, and the dial that follows

Mark 2 failing, with the diet piled at the middle, says the choice does not pay; α rises to 3, or the handling charge on
bites rises. Mark 2 passing and mark 1 failing says the plant-eaters are there but their lineages do not persist or do
not kill the leaves. The animal health scale is then the dial, so that a grazer survives its neighbours' wounds. Every
biter at a diet near 1 says animals are too cheap to kill; the same lever, at 3. Mark 3 failing in F and N alike, with
refused bites at zero, is an instrument fault; with refused bites above zero it says λ or the step is wrong. The
restraint rising in F is a finding and not a fault, so it is recorded and nothing is dialled. Mark 4 failing is Crozier's
erosion, and the step rises. Mark 6 failing is an absorber boom; the detritus efficiency falls, or the remains keep
burning. Mark 9 failing on pace cuts the grids, and S's configuration becomes the candidate. The leaves still growing in
every arm says the matter budget is the next question, and D261's fixed total is the screen after.

### What I expect

Mark 2 passes in F and D within 9,000 s, since the trade-off is steep and the founders span it (moderate). Mark 1 passes
in F and fails in D, where plant-eaters still kill each other for nothing and their lineages thin (low). Mark 3 passes in
F: restraint costs a plant-eater nothing, since it keeps nothing of kin anyway, so θ falls fast among them and the kin
share drops (moderate). Among meat-eaters θ stays high (a guess). Mark 6 is the one most likely to fail, with absorbers
two to three times B's (low). The pace falls by under 10 % (a guess).

## What could go wrong

- The absorbers boom on the larder and the leaves fall for a reason that has nothing to do with grazers. Arm B is there to
  show it, and the detritus efficiency is the dial.
- The plant-eaters eat the leaves out at an efficiency of 1.0. That is the overshoot D262 asks for, and the maxima are
  dialled back.
- A plant-eater still kills what it touches, since it cannot close its jaw except by restraint. Grazer deaths by grazers
  may then fall less than the income shift suggests, and mark 1 reads the lineages' persistence for that reason.
- Common signatures are spared more and the cue erodes (Crozier's paradox), or a lineage drifts toward a cannibal clade's
  signature and is spared by it. The spread and the distances between guilds show both.
- Animal odour alone pushes everything apart, which suits an ocean of oases but may lower grazing; arm S shows it.
- A predator may learn to follow alarm to a wounded body; the motion read would show predators moving toward alarm.
- The build is larger than either draft alone. A fault in the swallow's split or the self-smell subtraction would hide in
  the books, and the conservation test and the alone-body test are there for it.

## What the owner rules

Each question carries my recommendation and what each answer implies for the decision log, the rounds, the machine and
the risk.

1. **One mechanism, screened in one group**, rather than recognition alone first. I recommend yes, since the log then
   gets one decision and one proposal absorbed, the rounds get one screen of about four hours instead of two, and the
   machine is used once. The risk is that the two halves are read apart only through arms D and N, which the design
   accepts.
2. **A part's kind follows its cell type**, and a bite's store share takes the bitten part's kind. I recommend yes, since
   no state is kept and a mixed body is partly each. The alternative, one kind per body by guild, needs a stored guild
   and leaves a grazer's reserve edible to every plant-eater, today's cheap kill.
3. **The diet gene with the trade-off as its whole cost**, α = 2, both maxima 1.0, detritus efficiency 1.0, the intake
   waste replaced. I recommend yes, since enzyme upkeep would be a second dial with nothing yet to tune it against. The
   values are overpowered on purpose and roll back after the screen; the risk is that plant-eaters strip the leaves.
4. **Remains that close go to the water as snow**, reversing a clause of D220. I recommend yes, with arm B holding it
   apart. The log marks D220's clause superseded. The risk is the absorber boom, which the screen reads.
5. **A health scale for animal tissue** as a world lever, started at 1 in this screen. I recommend building it and leaving
   it neutral, so the kill's profit is the only lever under test. The owner may want it at 3 in every arm now, as their
   words of 2026-10-30 suggest. That puts both levers in one screen, and the log would carry them as one change until a
   later screen separates them. The risk of 1 is that plant-eaters still die to each other's wounds and mark 1 fails for
   that reason, which the next screen then tests.
6. **A three-number signature with a fixed step, a mouth that tastes it, and a restraint gene**, with kin left to
   evolution. I recommend yes, with the brain's jaw (the recognition draft's option C) waiting until the restraint shows
   it pays.
   The risk is the cue's erosion, which mark 4 reads.
7. **The animal smell with identity sums and the alarm are built now**, on D262's ground and the owner's kin question,
   amending D235's clause that gated the smell on grazers living on leaves. I recommend yes, and the owner rules whether
   D235's condition is met; I read it as not met and the amendment as the better case. The risk is the cost, which arm S
   bounds.
8. **The matter budget in this screen stays `r63g-b`'s open one**, and D261's fixed total is the screen after. I recommend
   yes, as the owner's ruling of 2026-10-30 reads. The rounds then run two screens in sequence. The risk is that the
   leaves keep growing in this one for a reason this screen cannot settle, which mark 8 says.
9. **The five arms and the marks as pre-registered**, with `r63g` step 2's resume staying queued behind them. I recommend
   yes, with the pre-registration committed before launch so that the manifest names it.
10. **The starting values**, any of which the owner may set otherwise. The diet exponent is 2 and both maxima 1.0. The
    detritus efficiency is 1.0 and the animal health scale 1. The signature step is 0.02 and the likeness scale 0.1. The
    odour reaches 6 m and the alarm 10 m. Founders' diet, signature and restraint are uniform, and no new reading carries
    noise.

*The biology behind the kinds (assimilation efficiencies, carbon-to-nitrogen ratios) is general knowledge and not from a
paper in `research/`. The recognition evidence is cited in `proposals/propose-recognition.md`, mostly from abstracts. The
cost figures are from reading the code and the card's notes, and none is measured.*

## The coordinator's review, 2026-10-30

- **A plant-eater still cannot refuse a stranger's animal tissue.** The restraint gene spares only what tastes like kin. So a plant-eater in a crowd keeps biting unrelated grazers. It keeps nothing, pays the handling charge and leaves a wound, and the draft names this risk under "What could go wrong". The mouth already knows the tissue's type (`TissueContact`). A second heritable restraint on kind would be cheap: refuse a bite whose efficiency for that kind is under a gene's value. It would let a grazer evolve to leave animals alone. I recommend adding it to F, N and S. Otherwise mark 1 may fail for a reason the screen cannot separate from the diet gene.
- **Mark 1's income share is partly built in.** Under the diet gene, a lineage near Diet 0 keeps almost nothing from animal matter, so its kept share from plants is near 1 by construction. The weight of mark 1 falls on its other two clauses: 30 living members through the window, and 0.6 of the leaf deaths. That is right for the owner's question, since a grazer here earns only from grazing. But a pass on the share alone should not be read as a behaviour.
- **The prediction for mark 3 is optimistic.** Restraint saves a plant-eater only the handling charge and its kin's lives, so selection on θ among plant-eaters may be weak. A θ that stays near 0.5 in F and N alike is then a null result, not an instrument fault.
- **Five worlds in one group is more than either screen this week.** `r63g` and `r63h` ran three worlds each. The heat stop and the one-second watch hold as for every group, and the pace mark carries the world count.
- **On question 5.** The owner said on 2026-10-30 that animals should be much harder to kill. The agent keeps the health scale at 1 so that one lever is read at a time. An alternative keeps five worlds: replace arm S (no smell) with arm H (F with the animal health scale at 3). The grids' cost is then read from the pace against `r63h`, and the owner's lever is tested directly. Mark 7 then has no arm without the grids. It is read from F against D instead, which also differ in taste and restraint, so it is weaker. I recommend the swap all the same.
