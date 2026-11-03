# The forager kit: grazers that can aim the mouth, feel where a leaf lies, remember for seconds and feel hunger

*Written on 2026-11-03 for the owner and the record, under the owner's delegation of that day (D291). The owner asked
what holds the nursery's grazers back from biting leaves better, and for rules that give them a mouth they can aim, a
sense of where leaves are, and memory. They asked whether hunger pushes the creatures to food, held that creatures should
be obsessed with eating or the world is set up wrongly, and asked for a hunger sense on a log scale, which is rule 4.
Drafted by an Opus agent from the record and the code, and reviewed by the coordinator, who settled its four rules as
D292 to D295. The owner may overrule any of them.*

## The proposal in one paragraph

The retrain's best grazers eat five times what a brainless body eats, and still miss most of the leaves they touch. I
read four limits in the exam and the code. The body turns at 0.125 rad/s, so a quarter turn takes 13 s. The mouth feels
how near another body is, and cannot tell which way it lies or whether it is a leaf. A brain unit forgets within one
step of 0.01 s unless a weight sits within a hundredth of 1. And the energy sense reads about 0.99 all episode, so a
grazer in the tank cannot feel hungry. Three measures come first, as agent work. Then come four rules. The body evolves
in the nursery, and the mouth feels which way the nearest leaf lies. Each brain unit inherits how fast it forgets, and a
hunger sense reads the reserve on a log scale. Each enters overpowered (D262), behind a switch that is off by default,
so every recorded run replays.

## 1. The grazers feel food near and cannot turn onto it

The retrain evolved brains and mouth places together (D280, D283, D284) in the oasis tank, 220 living leaves in 400 m².
Each body is a 0.30 × 0.15 × 0.30 m box with one hinged limb and two brain outputs. A blow lands when the body touches a
leaf and its mouth lies within three mouth radii, 11 cm, of the touched part (D282). D275's one radius is where that
reach is meant to end. The search stopped after generation 29 under D172's rule. The table shows what it changed.

| | Generation 0 | Generation 29 |
|---|---|---|
| Eaten a member | 93.7 J | 214 J |
| Landed share: blows landed over all blows | 0.28 | 0.52 |
| Tight share: blows landed within one radius, over all blows | 0.076 | 0.12 |
| Members whose mouth left the rule's face | 0.42 | 0.96 |

In the exam the tops ate 233.5 J under their own brains and 43.9 J with none. Knocking out the smell left 101.6 J.
Knocking out the mouth's feel left 172.8 J, a loss of 61 J with a standard error of 7 J. So the feel is used. The aim
barely improved: the tight share rose 1.6 times, and 76 % of landed blows came from between one and three radii. At one
radius about three in four of them would have been refused. The last control, the mouths put back where the body's rule
had them, was not yet read when this was written.

The code shows what the senses give. The feel is the smallest gap from the mouth to any other body, leaf, animal or
remains, in mouth radii (`Evosim.Dynamics/MouthTouch.cs`, `MouthFeel.cs`). It reads 1 at a touch and 0 at 8 radii,
30 cm. The gap comes from the contacts' distance routine (GJK, `ConvexCore.Distance`), which also returns the nearest
point on the other body, and the feel discards that point. The feel is refreshed every 0.5 s
(`Simulation.HoldMouthFeel`), while the brain steps every 0.01 s. The leaf smell lies on 2 m cells filled every 0.5 s
from 6 m cones. Its direction is the slope between eight cell centres (`ScentField.GradientAt`), so near a patch it
points to the patch's middle (D280). Between 30 cm and 2 m nothing says where the nearest leaf is. Spread evenly, these
leaves would be about 1.35 m apart (my arithmetic).

Each brain unit is the tanh of its bias plus its weighted links, reading senses now and units from the step before
(D146). A self-loop of weight w keeps w of a small value each step. Keeping half for 1 s needs w = 0.993. A mutated
weight moves by a Gaussian step of 0.1, fourteen times that margin (my arithmetic from the code and the recipe). A
weight above 1 latches instead. The top brain's two self-loops are negative, and alternate rather than hold. The search
adds a unit at a chance of 0.1 a child (`CentralMutation.cs`), and no top gained one in 29 generations.

## 2. Three measures come first, and need no ruling

1. **The mouth's gap behind the body's front**, along its motion, is being added to the mouth-lead instrument. A mouth
   well behind the leading point meets leaves late, which points to rule 1. A gap near 0 with blows still refused
   points to rule 2.
2. **Where the mouth was** at each refused blow, against the touched part and the body's motion. Mouths mostly on the
   body's far side point to rule 1, a faster turn. Mouths mostly just beyond three radii point to rule 2, since a small
   turn would have landed. The same read counts how often a body that felt a leaf and lost it comes back within
   seconds. Seldom points to rule 3.
3. **The hunger test** swims the tops with the energy sense held full and held empty, and sets each body's reserve
   against its stroke, turning and blows tried in the recorded trials. The tank's reserve barely moves, so world runs
   give the range. I expect no change in the tank, where the sense never varied (inference). World bodies short of
   reserve that forage no harder than full ones point to rule 4.

Each takes an hour or two of agent work and, at the exam's pace, under an hour of machine time (inference).

## 3. Three rules give the body aim, a bearing and a memory

### Rule 1. The body evolves in the nursery, and the reach comes back once aim shows

The tank's search mutates the body beside the brain and the mouth: limbs, joints, part sizes and D233's shape family.
It scores D179's energy return, because an eaten score would reward ever larger eating parts (D178), and D285 makes a
mouth grow with its part. The reach stays at 3 radii until an arm's tight share doubles from 0.12, then steps towards 1,
one arm a step.

All of it exists: D178's body search, D179's score, and the family limbs of D233 and D252. D284 held every body gene but
the mouth fixed, so rule 1 is a recipe and needs no build. A slow turn is the plainest limit in the numbers. "Turning
appears harder to evolve than running" in evolved 3D foragers [CA16 p.16], and a second limb may turn faster
(inference). It comes after one arm of rules 2 and 3, for D284's reason: a body shaped for a brain without a bearing
would be reshaped once the brain has one.

### Rule 2. The mouth feels which way the nearest leaf lies

Each eating part reads, beside today's feel, the nearest leaf's direction and the nearest animal's. Each is three
numbers in a frame whose first axis points out of the mouth, scaled by nearness as the smell's direction is scaled by
strength. It is refreshed every brain step, and starts at a 1 m range with no noise.

GJK already finds the nearest point, so a direction costs a subtraction and a rotation per mouth. Telling a leaf from an
animal keeps a second minimum, sorted by whether the other part photosynthesises. Refreshing every step runs the search
fifty times as often, and its cost is untimed, so the build times it first. Small grazers bring the mouth onto food by
touch and taste (D280), and a touch on one side of a lip gives a side (inference).

Each alternative gives less than a bearing. A nose (D235, D257, D265) reads the same 2 m field at its tip, with noise, so it finds the
patch's middle as the body's smell does. A rate of change of the smell is how bacteria and worms steer [SBB86 p.1; IL10
HTML]. On cells of 2 m filled every 0.5 s it would change little across one body near one leaf (inference), and it needs
rule 3. A simple eye would see leaves, and D280 rejected vision as a large build. Rule 2 is built first.

### Rule 3. Each brain unit inherits how fast it forgets

Each unit gains a time constant τ, a gene from 0.01 s to 30 s, mutated on its logarithm. Each step the unit moves the
share 0.01 s / τ of the way from its old value to today's tanh. At τ = 0.01 s the unit is today's, and the build keeps
today's arithmetic there. Stored genomes are written out at that value, as D283 wrote out mouth places. The search's
add-unit chance stays 0.1, and the arm counts units added and kept each generation.

Evolved networks with a time constant per unit stored a bit across trials [BF03 p.2, p.6–7], and caught an object seen
only briefly in 2 of 3 runs [GB99 p.3, p.6]. Animals weigh about a second of smell against the few before it [SBB86
p.1; AS18 p.12]. No study compared one recurrent controller with and without evolvable time constants. In spiking
networks, delays and time constants solved more tasks than weights alone [HEGB24 p.14]. The review infers that a time
constant per unit is the cheaper addition for windows of seconds.

A latch unit, a self-loop above 1, exists today and held state in evolved networks [SBM03 p.5–6], but it holds one bit,
on or off. A sensed derivative, a port reading a sense's change over a fixed window, is how evolved worm circuits and
walking flies kept time [IL10 HTML; AS18 p.23]. Evolution finds a use for it more easily, and we would choose its window
and its senses. Time constants are built beside rule 2 and tested in the same arm. The exam's knockout sets every τ back
to 0.01 s.

## 4. Hunger is not felt in the tank, and the tank's score does not need it

The energy sense reads S / (S + 30 s), where S is the reserve in seconds of standing cost (D245). A tank body starts
with 3,830 s of reserve beyond what its growth spends (the recipe's purse, rule `grown`), in an 1,800 s episode, and the
tops end 136 J up. So the sense reads about 0.99 throughout, by the code and the recipe. Nobody has logged it.

The nursery selects for eating whether hunger is felt or not. It scores joules eaten from leaves, so every second not
eating loses score, which is the obsession the owner asks for. Starvation costs nothing there, since no body with an
hour of reserve starves in half an hour. In the world starvation is death. In arm A of the `r63g` screen, 71 % of grazers
starved, at a median age of 72 s (`proposals/propose-recognition.md`). Whether hunger pushes the world's grazers to food
is not known, and measure 3 reads it.

Two findings would show the world set up wrongly (inference). One is a foraging return below 1 at the world's prices,
so that swimming to eat loses energy, as D179 found in the sparse tank. The other is bodies short of reserve foraging no
harder than full ones while starvation is the commonest death.

### Rule 4. A hunger sense reads the reserve on a log scale

A new sense reads h = log2(k / S) / log2(k / S_min), clipped to 0 to 1, with k = 60 s and S_min = 0.25 s. It reads 0
above a minute of reserve, rises 0.126 for each halving of the time left, and reaches 1 at a quarter of a second.
D245's sense stays beside it. The table compares the two.

| Reserve S | D245's S / (S + 30 s) | Hunger h |
|---|---|---|
| 60 s | 0.67 | 0 |
| 15 s, a newborn's (D245) | 0.33 | 0.25 |
| 4 s | 0.12 | 0.49 |
| 1 s | 0.032 | 0.75 |
| 0.25 s | 0.008 | 1 |

A tanh brain gains resolution from a log scale. A larger value only saturates it. Between 1 s and 0.25 s, D245's sense
moves 0.024, and a weight big enough to act on that saturates the unit above a few seconds (my arithmetic). An equal
step per halving gives the brain as much to work with at 1 s as at 30 s. D245 rejected a log squash for its own sense,
and rule 4 amends D245 by adding a sense, not by replacing one. It also answers D020, which kept hunger out because its
threshold would be a constant no run could vary. Here k is a world tunable. Starvation costs something in the world, so
rule 4 is for the world. In the tank at today's purse it reads 0. The birth trigger has its own proposal.

## 5. What each rule costs and risks

The table gives each rule's cost to the record, the runs and the machine, its main risk, and how it starts overpowered.

| | Rule 1, body | Rule 2, bearing | Rule 3, memory | Rule 4, hunger |
|---|---|---|---|---|
| Record | amends D284; a recipe, no build | amends D280; new ports and tunables refuse older configs | amends D146 and D147; a genome format bump | amends D245; one port, two tunables |
| Nursery | a run on two recipe seeds, as D182 asks | links grafted at random sign, as D280's were | two seeded units a founder | reads 0 at today's purse |
| World rounds | the pool meets the world's prices | needs its card copy first | the card's brain step gains a term a unit | screens first in a world, where starvation costs |
| Machine | 4 to 8 h a run plus a 2 h exam (inference) | the feel's search fifty times as often, untimed | small (inference) | negligible |
| Risk | a body change breaks a tuned brain (D002); a central network converged early under changing bodies [KGV22 HTML, Results] | at 1 m bodies home where a touch never could | memory is slower to evolve than reaction [GB99 p.3, p.6]; units go unused | unused if foraging does not pay |
| Overpowered (D262) | body mutation at several times the world's chance, back to the world's before a pool leaves | 1 m, no noise, every step; then noise, a 0.5 s hold, a range towards 8 radii | founders gain units at 1 s and 5 s, linked from the feel, the smell and the energy sense to both outputs; thinking is free (D146) | reads from a minute, four times a newborn's reserve |

## 6. Rules 2 and 3 go first, and the body after

1. The three measures, now.
2. Rules 2, 3 and 4 built in parallel on the host, each by a builder the coordinator reviews. Rule 4 gets its card
   copy too, since it is meant for the world.
3. One nursery arm from the retrain's tops with rules 2 and 3 on, the body fixed but for the mouth, the exam knocking
   each out. About 5 to 8 h of machine time (inference).
4. Rule 1's body arm from that arm's tops, then the reach's dial-back arms.

Rule 4 can enter a world screen once built, apart from the nursery. A measure can reorder this: a mouth that trails its
body moves rule 1 up.

## 7. What it amends

Rule 1 amends D284. Rule 2 amends D280's scalar feel, its 0.5 s hold and its any-body target. It departs from D020's
scalar distal senses, as the direction form of D167 and D174 did. Rule 3 amends D146's unit and D147's founders. Rule 4
amends D245 by adding a sense, and ships behind a switch as D249's did. All four follow D262. Nothing in the index is
reversed by them.

## 8. What was settled under D291

The coordinator settled all four rules on 2026-11-03 as this specification gives them, in the order of section 6.
Rule 2 is D292, rule 3 D293, rule 4 D294 and rule 1 D295. Each is built behind a switch that is off by default,
and enters a world round only after its screen passes. The owner may overrule any of them.

## Sources

None of these papers was read for this specification. Each claim reaches its paper through the synthesis rows of
`research/LITERATURE-REVIEW.md` and the locators they give. The last column says how the review read each one.

| Key | Source | How the review read it |
|---|---|---|
| AS18 | Álvarez-Salvado, E. et al. (2018) Elementary sensory-motor transformations underlying olfactory navigation in walking fruit-flies. eLife 7, e37815 | at passages |
| BF03 | Blynel, J. and Floreano, D. (2003) Exploring the T-maze: evolving learning-like robot behaviors using CTRNNs. EvoWorkshops 2003, LNCS, 593–604 | in full |
| CA16 | Chaumont, N. and Adami, C. (2016) Evolution of sustained foraging in three-dimensional environments with physics. GPEM 17, 359–390 | at the pages cited |
| GB99 | Gallagher, J.C. and Beer, R.D. (1999) Evolution and analysis of dynamical neural networks for agents integrating vision, locomotion, and short-term memory. GECCO 1999 | in full |
| HEGB24 | Habashy, Evans, Goodman and Bowers (2024) Adapting to time: why nature may have evolved a diverse set of neurons. PLoS Comput. Biol. 20, e1012673 | at passages |
| IL10 | Izquierdo, E.J. and Lockery, S.R. (2010) Evolution and analysis of minimal neural circuits for klinotaxis in *Caenorhabditis elegans*. J. Neurosci. 30, 12908–12917 | article page |
| KGV22 | Kvalsund, M.-K., Glette, K. and Veenstra, F. (2022) Centralized and decentralized control in modular robots and their effect on morphology. ALIFE 2022 | HTML |
| SBB86 | Segall, J.E., Block, S.M. and Berg, H.C. (1986) Temporal comparisons in bacterial chemotaxis. PNAS 83, 8987–8991 | abstract |
| SBM03 | Stanley, K.O., Bryant, B.D. and Miikkulainen, R. (2003) Evolving adaptive neural networks with and without adaptive synapses. CEC 2003, 2557–2564 | in full |
