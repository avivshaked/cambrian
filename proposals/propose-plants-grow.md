# Proposal: plants choose between growing and budding, and the world holds more than one kind of plant

*Draft for the owner, 2026-11-06. It takes up the two plant ideas of the owner's discussion of 2026-11-03. One is a
sense of the matter at each blade, with an urge to grow beside the urge to breed. The other is plants that grow large
rather than many. It adds plant varieties, which the owner made part of the work on 2026-11-06, and the overlap rule,
which the first measures turned up. It builds on the plants spec (`logbook/specs/plants-hold/spec.md`, D296 to D298)
and reads the measures in `logbook/specs/plants-hold/`. World rules are the owner's. Instruments, reads, fault fixes and
dials within a ruled rule are agent work, and each section says which it is. Absorbed into DECISIONS.md on ruling, then
deleted.*

## The proposal in one paragraph

Round 67's worlds ended as runaways over 25,000 bodies, about four in five of them plants. The plants were one kind,
single drifting blades 40 to 110 cm across. A plant has one way to use a full reserve, which is to bud. I propose that
a plant's brain chooses what to do with it. A second urge, to grow, joins D299's urge to breed. A sense of the matter
dissolved at each blade tells the brain whether the water around it is rich or stripped. A plant in rich water can then
hold its births and grow, and one in stripped water can bud a child into other water. Growing is first made possible,
by fixing a fault in the module rule that refuses nine additions in ten. D296 to D298 make it worth something: a blade
earns where it stands, a holdfast keeps a plant where matter falls, and a bitten blade grows back. For varieties I
propose seeding more than one kind of plant at the start and letting the world sort them, rather than waiting for a
second kind to appear by mutation. The overlap rule stays as it is until it is counted again after the fault fix. Every
part goes behind its own switch, off by default, and starts overpowered (D262).

## What the measures found

| Measure | Found | File |
|---|---|---|
| What plants the worlds held | One kind, single drifting blades, in 16 to 21 effective plans a world; nine in ten made of light-catching cells alone | `r67-plant-census.md` |
| Why bodies do not grow | 756,984 additions refused for shape on seed 6202 against 3,659 made. About nine refusals in ten are a fault in the rule; the plants' other third is a blade that would stand inside its own plant | `refusal-probe.md` |
| What a part costs against a body | About 48 µs of machine time a simulated second for a body before its parts, and about 22 µs a part, so four blades as one body cost about half what four bodies do. Read under load; a quiet timing is owed | `r67-plant-census.md` |
| Whether a holdfast grips | No grip formed in 3,000 s, but no founder carried the anchoring cell, so the test is not yet made | `grip-smoke-r67.md` |

## 1. Growing is made possible: the module rule's fault (agent work, being built)

The rule picks the lowest indeterminate node under its ceiling. Development may never reach that node, or the node may
lie on no cycle back to itself. Either way, raising its count adds no part, and the body asks again at every growth step
for life. The fix picks the lowest node that can grow, behind a switch that is off by default. It is a fault under D298,
and it is being built now on branch `module-fix`. It needs no ruling, and nothing below can show until it is in.

## 2. The grow urge and the matter sense (ruled by the owner on 2026-11-06 as D308)

**The brain gains** one output, the urge to grow, beside D299's urge to breed. The module rule adds a module only while
the urge is up and the reserve is over the module's line. That is how a birth under D299 waits for the urge to breed.
The brain also gains one sense, the matter dissolved in each light-catching part's cell. It is read at each part, as
D154's light sense reads light, and on a log scale, as D294's hunger sense reads the reserve. No body senses dissolved
matter today; `Chemical` reads the snow.

**Both urges go together**, because a plant that holds its births banks its reserve, and a banked reserve is what pays
for a module. So the choice between growing and budding needs both urges on in the same world. The matter sense is what
makes the choice a good one. A second blade pays only where the water is not stripped, and a child pays only where it
lands in better water.

**It starts overpowered**, as D262 asks. Both urges are read every step and cost nothing. The module's line is lowered from its
present value until additions are common, which is a dial within the rule and agent work. The dial-back drops the
matter sense first, then raises the line.

**It amends** D106's module rule as D299 amended D087's gate, and extends the brain's outputs as D268
and D299 did. New tunables refuse every older config, and the fixtures are re-recorded, as with every build. On the card
it is one output, one sense channel a part and one comparison a body, with the kernels regenerated and sat again
(inference, settled in the build). A plant that never raises the urge grows as it does today, so a world cannot do
worse than today's for want of it (inference). The risk runs the other way. Plants that grow and never bud may leave too
few children to hold the water between the oases, and the screen measures that.

## 3. Large plants rather than many (ruled by the owner on 2026-11-06 as D309)

The owner's aim is a world that holds more matter in fewer, larger plants. That serves the grazers, and a world of
20,000 small plants runs into the body ceiling. Sections 1 and 2, with D296 to D298, give a large plant its reasons.
Each blade earns light and draws matter where it stands, a holdfast keeps the plant where matter falls, and a bitten
blade grows back. None of them makes a part cheaper than a body. So a large plant is allowed to be large and is not paid
for it, and evolution decides whether it pays.

The machine side is measured. Four blades as one body cost about half the machine time of four bodies, so a world of
large plants would run faster at the same tissue (inference until the quiet timing).

## 4. Varieties: seeded kinds that the world sorts (ruled by the owner on 2026-11-06 as D310)

**A variety here** is a plant that makes its living in another way than its neighbours do. A larger or smaller blade of
the same kind is one variety. The census counts varieties by growth form: drifting or anchored, one blade or many, and
the height of the highest blade above the holdfast.

There are two ways to get them.

| | (a) Wait for them | (b) Seed them, and let the world sort them |
|---|---|---|
| How | The world offers niches (rock, height, rich water) and evolution finds forms that use them | The founders hold two or three hand-built kinds of plant from the start, in equal numbers, and evolution changes them from there |
| For | The cleanest test: every variety that holds was evolved | Fast and overpowered (D262). D296 already seeds half the plant founders anchored, so it is half done |
| Against | Round 67 waited 44,000 s and got one kind | A seeded kind that holds says the world can keep it, not that evolution could make it |

**I recommend (b)**, with two kinds that today's rules can keep, and a third only if its rule is built.

1. **The frond** holds island coral with a holdfast of the anchoring cell. It has a short stalk and indeterminate blades
   with a ceiling of eight, the founders D296 and D298 ask for.
2. **The drifting blade** is today's plant, from today's founders.
3. **The crust** is a flat mat lying on the shallow bed. It needs D204's island shoals, ruled and never built, since no
   bed stands in lit water today (inference from the spec's coral census, still owed).

A variety would count as holding when it keeps a tenth or more of plant tissue at 24,000 s on both seeds. That is a mark
for the pre-registration, and is set there.

## 5. The overlap rule stays until it is counted again (a question, yours, not yet)

D101 is the owner's own rule: a body whose parts stand inside each other is not born. The module rule refuses an addition
that would do the same, and among plants that carries about a third of the refusals. Once the fault fix lets bodies
reach other nodes, the overlaps they meet will change. So the count is taken again on the fixed build before anything
here moves. If it is still large, one option lets a plant's thin blades pass through each other, as kelp blades do.
Another has the rule try the next node that can grow when one would overlap. Both change what a body may be.

## 6. A holdfast's child settles on rock (ruled by the owner on 2026-11-06 as D307)

The anchored founders' smoke (`logbook/specs/plants-hold/anchor-smoke.md`) ran D296 and D298 on round 67's world for
3,000 s on two seeds. Holdfasts set on island coral gripped: 8 of 10 founders on one seed and 12 of 12 on the other held
a grip at their peak, the first grips in any world. The line bred well, and by 3,000 s carried a holdfast in 256 of 975
plants on one seed and 155 of 1,941 on the other. But the grips held followed the founders still alive, 4 and 1 at the end, so at most a few children gripped. A child is placed
beside its parent at the plants' dispersal (D264, 30 m), in open water at its parent's depth, so a holdfast's child
never touches rock. Small bodies weigh nothing in the water (D064), so it does not sink to rock either. The forest on
the coral lasts one generation.

| | (a) A gripping parent's child settles on rock | (b) A holdfast sinks | (c) Leave it |
|---|---|---|---|
| What changes | A child of a parent that holds a grip is placed on rock within the parent's dispersal reach, holdfast down, as the founders are; with no spot found, it is placed as today | A body whose root is the anchoring cell weighs something in the water, and sinks until it grips | Nothing |
| For | A forest can spread over an island as a kelp bed does, from spores that settle near the parent | The physics decides where it lands | No new rule |
| Against | It amends D264's placement for these children | It amends D064, and most of the water lies over dark bed, so most sinkers land in the dark | The forest dies with its founders |

**I recommend (a)**, behind a switch, off by default. The placer exists already: it is the one that sets the anchored
founders. Only children of a parent holding a grip settle, so no plant is made to anchor (D268), and a drifting child of
a drifting parent is placed as today. I ask whether a gripping plant's child may settle on rock near its parent.

## The order

1. The fault fix (agent work, building), and the plants branch brought onto round 67's code (built, its proof running).
2. D296's anchored founders and the grip test under true shape (agent work under D296, settled).
3. D298's regrowth (agent work under D298, settled). The anchored founders' smoke is in (section 6).
4. On your ruling, the grow urge and the matter sense, and the seeded varieties.
5. The overlap count on the fixed build, then the screen of the plants spec's §8 with the arms you rule in.

## What the owner rules

1. **The grow urge and the matter sense** (section 2): ruled by the owner on 2026-11-06 as recommended, D308.
2. **Large plants** (section 3): ruled by the owner on 2026-11-06 as recommended, D309.
3. **Varieties** (section 4): ruled by the owner on 2026-11-06 as recommended, D310.
4. **A holdfast's child settles on rock** (section 6): ruled by the owner on 2026-11-06, option (a), as D307.
5. **The overlap rule** (section 5) needs nothing now, and comes back with its count.
