# The kill rule's facts: what a kill holds, and how a body dies today

*2026-10-25, the speed agent. The factual sections of a proposal for a death threshold, which the owner set aside on
2026-10-26 (D238). They are kept here because D238 cites them. Each claim says whether it was read in the code,
measured, or is inference. The finding is also in `logbook/specs/r62kg-prereg/predictions.md`, under "What the check
found"; the design read behind D238 is `logbook/specs/kill-rule-read.md`.*

## The finding in plain words

In this world a grazer eats only what its mouth touches, and each bite feeds it from the tissue the bite wounds
(D219, D220). Since D232, a grazer that kills a body swallows what is left of it into its gut, a store it digests at
its mouth's own rate. We built the gut expecting a kill to hand over most of a body, about 350 to 450 J. That was the
proposal's guess, and it was wrong.

The kill-meal check ran one world for 6,000 s on the graphics card. Its grazers made 34 kills. Only 12 had anything
left to swallow, and those held 564 J between them, 47 J a kill on average (measured). The kill rows say why. Each row
records what the dead body still held at the moment it died. Of the 30 kills of a whole body, 19 held under 1 J, and
the mean was 30.5 J. The deeper-wound screen with no gut, `r62bd-x2-6201`, gave 36 of 52 under 1 J and a mean of
36.4 J over the same 6,000 s (measured).

So about two kills in three come on the bite that takes the last of a body. The rest of it was already eaten, bite by
bite, before it died. A body dies only when its root part or its last part dies (read in the code, below). A grazer
that strips a body part by part therefore kills it only at the end, and by then there is no meal left to swallow. The
gut changes where the remainder goes, into the killer instead of into remains on the floor. It does not change how
much the remainder is.

Which kinds of body were killed comes from the kill rows whose attacker was born a grazer, each victim classed by its
birth row (`scripts/reads/grazer-tally.py`, run on 2026-10-26). In the check, grazers killed 27 leaves
and 3 grazers whole. Two of the three are the one large swallow, at 4,657.5 s, where two grazers killed each other in
the same step (measured). In `r62bd-x2-6201` over 24,000 s grazers killed 1,710 leaves, 185 absorbers and 12 grazers
(measured). So a grazer's kill is nearly always a leaf, and a grazer kills another grazer about once in 150 kills.

## What the code does today

Everything in this section was read in `src/Evosim.Core/Ecosystem/` on `main`. The world's settings are those of
the check, `rounds/env-r62kg.ps1`.

### A body dies only when its root or its last part dies

Each step a biting part wounds the parts it touches. A wound takes a share of the part's health, and the same share
of its tissue feeds the biter, up to the mouth's per-step cap (D220, D221). With the store share on, the bite also
takes that share of the body's reserve (D224). When a part's health reaches zero, the step's collection pass finds it
and does one of three things (`WorldMouth.KillPart`):
1. If it is the root part, or the body's only part, the body dies, killed by its last attacker.
2. If it is another part, the body loses it as a limb. The part's own tissue is gone. Each subtree beyond it becomes a
   separate dead body, called remains, which biters can go on eating until a window closes (500 s in this world). The
   body lives on with the rest, and its plan is rebuilt without the lost part.
3. If rebuilding the plan leaves no parts, which happens when a subtree loses its only anchor, the body dies as in 1.

After a limb loss the body also dies if it falls below the mass floor, the smallest part a newborn may have. That
death is recorded as starvation, and it leaves remains rather than going into a gut.

Nothing compares the tissue a body has lost with the tissue it holds. A body may lose every part but its root and
live. The tissue sums a threshold would need are there: each part's tissue in joules and its health are read every
step, and the code already sums the eaten and uneaten tissue for its accounts. But no rule uses them.

### The kill gut takes only a death by a bite with a living killer

When a body dies by a bite and the gut is on, the code adds the body's reserve, its gestation account and its own
gut into one store. If its last attacker is alive and has a mouth, that attacker swallows the body's remaining tissue
and the store, and the body leaves no remains (D232). Otherwise the body becomes remains holding its store. The
killer's swallowing does not need the killing part to be a mouth: the last part to wound the body may be a claw,
provided the killer has a mouth somewhere.

The gut is digested each step at what the mouth's cap leaves after that step's bites, less the intake waste, which is
a fifth here. A limb loss never feeds a gut. Its remains are founded on the floor as before.

### Healing restores health, and nothing restores a lost part

A wounded part heals at a hundredth of its health a second in this world, about 100 s from near zero to whole if the
body can pay. The body pays from its reserve: one joule a unit of health for the healing itself, which is burnt, and
the price of the tissue the healed health stands for, which buys back what bites ate. A body that cannot pay heals in
proportion. Remains do not heal.

A lost part never grows back. The body keeps a list of the parts it has lost, and every later rebuild of its plan
leaves them out. A juvenile grows by scaling its whole adult plan up, and after a limb loss that plan is the reduced
one, so growth does not rebuild the limb either.

One route adds parts, because in this world the module rule is on. A body whose genome marks a part as one that repeats
without a fixed count adds one more of it when its reserve passes 300 s of its upkeep. It drops one after 100 s below
50 s of upkeep. A part added this way starts whole. The lost list still applies, so a module added after a loss is a
new one, not the lost one returned. I have not read whether a lost module counts toward the ceiling on how many a body
may add, nor how many bodies in these worlds carry such genes at all. So whether a grazed plant can regrow by this
route, and how fast, is open.

For a photosynthetic part, a leaf, nothing in the code differs by part. A grazed leaf part fixes light in proportion
to its health, so it earns less until it heals. A leaf that loses a part goes on photosynthesising with the parts it
keeps.

### The bite's books class a body as a leaf by any one leaf part

The stats split what grazers keep into bites on living leaves, on other living bodies and on remains. A body counts as
a leaf if any of its parts is photosynthetic, so a bite on the mouth of a body with one leaf counts as a bite on a
leaf. Food taken out of a gut is in none of the three columns.

Cell types differ in what they can become. A leaf part's armour can reach half of a structural part's, and its
toughness, which sets how much damage its health absorbs, is at most 1 against the structural part's 4.

## The rules chosen for cheapness that a death threshold would have re-asked

When a world rule changes, the rules chosen because they were cheap under the old one are asked again. These are the
ones a threshold would have touched. D238 kept the rule, so none of them was changed.

1. **A body counts as a leaf if any part is a leaf.** That served the books while a kill took the whole body. A rule
   that treats leaf tissue apart from other tissue makes the per-body flag the wrong measure, both for the threshold
   and for the stats that say what a grazer lives on.
2. **The killer is the last attacker of the part that died.** It may be a claw on a body with a mouth elsewhere. Under
   a threshold the death comes from the sum of many wounds, often by several attackers, so the question of who
   swallows the body gets harder.
3. **A limb loss founds remains and never feeds a gut.** Under a threshold most of a body's loss before death still
   comes as limbs, so most of a plant a grazer strips still falls to the floor as remains.
4. **A lost part never grows back.** If leaf tissue does not count toward death, a leaf is grazed but lives on. It can
   be cropped again only if it regrows, and today the only route is the module rule, which is open (above).
5. **A one-part body dies when its only part dies.** If leaf tissue never counts toward the threshold, a one-part
   leaf needs a rule of its own: as it stands, it would never reach the threshold, and it would die only when bites
   take all of it.
6. **The mass-floor death is recorded as starvation and leaves remains.** A body stripped near the threshold may pass
   the floor first, and then its meal goes to the floor, not to the killer.
7. **The gut has no cap** (D232). A threshold that hands over most of a body makes the gut's size matter again, which
   D229's fat rule was the proposed bound for.
