# 09 — A mouth must touch what it eats

[Piece 06](06-the-producers-feed-the-water.md) ended with a food chain of two levels: producers that live on light,
and stomachs that live on what the producers leak into the water. Mouths came later, in round 45 ([logbook
0114](../logbook/0114-the-mouth.md)). A mouth is a cell that bites, and for two months it ate the dead. A body that
died became a corpse, and any mouth within reach of the corpse could feed from it.

This piece is about the rule that replaced that, in October 2026: a mouth eats only what it touches, as it wounds it.
It covers why the old rule hid the real problem of being a grazer, and how eating by contact works. Then it says what
the new rule took away on its first day, and why it is still the rule.

## A meal that fed whoever came by

Under the corpse rule, eating was a matter of distance. A mouth had a reach, set by a dial, and it fed from every
corpse inside that reach at a rate set by its intake gene. A corpse sank slowly and spoiled over minutes. So a dead
leaf was a small larder that any passing mouth could share.

That rule made grazers look healthy. Round 62 was the first round in the open ocean of [piece
12](12-the-open-ocean-grew-its-oases-and-then-filled-the-water-between-them.md). In it a grazer's kill was credited
with about 420 J kept, and grazer lines replaced themselves at a reach of 3 m ([logbook
0147](../logbook/0147-grazers-paid-their-way-once-they-swam-faster-and-grew-up-slowly.md)). But the share of grazers'
food that came from their own kills fell in every world as the run went on. They were living on the dead that others
had made. A later design read found that the 420 J was never one meal. It was a corpse that fed several grazers in
turn, all of it counted against the kill that made it ([`kill-rule-read.md`](../logbook/specs/kill-rule-read.md)).

The owner had asked twice for a world without corpses. Then a screen showed that fast rot did not leave a kill to its
killer. The owner ruled that a mouth must touch what it eats, as it touches what it kills, with no reach at all
([D219](../DECISIONS.md#d219)).

## Eating is wounding

The rule that followed treats a bite as two things at once ([D220](../DECISIONS.md#d220),
[D221](../DECISIONS.md#d221)). The build specification gives every step, with its route for matter and energy
([`contact-eating-spec.md`](../logbook/specs/contact-eating-spec.md)).

When two bodies touch, the physics names the nearest part on each side. If one part carries attack, its blow, less the
other part's armour, lowers that part's health. That was already true. What is new is that health now also means the
share of the part's tissue still uneaten. A wound that takes a fifth of a part's health takes a fifth of its tissue
out of the body at once, and offers it to the biter.

The biter keeps that tissue only if its biting part also carries intake. What it keeps in one step is capped at the
part's intake rate, which grows with the part's surface area and its intake gene. A fixed share of what it keeps is
lost as waste. The waste, and anything over the cap, goes into the water as dissolved matter, where the stomachs of
piece 06 can live on it. No joule is made or lost along the way, and [piece 07](07-the-world-keeps-two-books.md)'s two
books still close.

A part eaten to zero health is cut off. If it was the body's root, the part every other part hangs from, the body
dies. If it was a leaf blade or a limb, the body lives on without it, as a kelp lives on after a fish crops its
fronds. A part cut off, or a body that dies uneaten, stays in the water as limp remains. Remains do not sink or swim,
and any mouth that touches them can go on eating them until a time window closes. The owner asked for that window
after observing that a real grazer goes on eating a plant after the plant has died (D220).

Three later rulings finished the rule. A living bite also takes its share of the victim's stored reserve, so a body is
worth its tissue and its savings ([D224](../DECISIONS.md#d224)). A killer swallows what is left of its kill into a
gut, digested at its mouth's rate ([D232](../DECISIONS.md#d232)). And a body dies when its root is eaten, with no
threshold of damage beyond that ([D238](../DECISIONS.md#d238)).

## What it took away on the first day

The first screen of the rule, `r62ce`, ran on 2026-10-24 on three seeds
([`r62ce-read/README.md`](../logbook/specs/r62ce-read/README.md)). Grazers killed leaves about as often as they had
under the corpse rule. But a kill now came with 24 to 31 J kept, not 420, and a grazer kept about a fifth to two
fifths of its upkeep. In each world 1 to 5 grazers were bred over 24,000 s. The leaves, with nothing cropping them,
more than doubled.

Three screens then tried to win the meal back: the share of the store, a wound up to sixteen times as deep, and the
gut. None brought a kill's kept joules past 90 J ([logbook
0147](../logbook/0147-grazers-paid-their-way-once-they-swam-faster-and-grew-up-slowly.md)). The gut's check found why.
About two-thirds of kills came on the bite that took the victim's last tissue and reserve, so a dead body held almost
nothing to swallow.

The meal was never the limit. A grazer's mouth was biting during about 0.5 to 1% of its life, and each bite already
took a whole leaf part ([`kill-rule-read.md`](../logbook/specs/kill-rule-read.md)). The grazer budget worked it
through. A meal took about 100 s to find, nearly all of it travel. Two or three breedings in a life allow about 41 s
([`grazer-breeding-budget.md`](../logbook/specs/grazer-breeding-budget.md)). Under the corpse rule a 3 m reach had
paid for slow searches, because a meal could be eaten from where you happened to be. Under contact eating a meal has
to be reached.

## Why it stays

The rule cut a grazer's income from about its upkeep to a fifth of it. The owner kept it anyway
([D237](../DECISIONS.md#d237)): a mouth must touch to graze, and no setting that lets eating happen without touch is
screened or built. I think this was right, and the reason is what the rule exposed (inference). A world in which food
can be eaten from a distance does not reward a body for finding it. Where food must be touched, everything that
shortens a search pays. That means speed, a sense that points to food, a nose, and a grip on the rock where the plants
grow.

The rounds since bear that out in part. Grazers first paid their way under contact eating when a stronger stroke let
them swim about three times as fast. Growth also had to stop spending a newborn's reserve before its first search
(logbook 0147). The screens of round 63 then found that cheap mouths raise grazers but make them eat each other, since
one mouth bites whatever it touches at one yield ([logbook
0148](../logbook/0148-round-63-the-film-world-fills-and-cheap-mouths-eat-each-other.md)). That is a consequence of
contact eating too. A mouth that must touch, and cannot tell a leaf from a sibling, eats the nearest thing it touches.
In a crowd of its own young, the nearest thing is often kin. The world's answer to that, still being built, is senses
for food, foes and kin ([D264](../DECISIONS.md#d264)).

The remains have a quieter role. They are the world's carrion, but a carrion that has to be touched and that closes
after a window. In the screens most remains closed unbitten, burning far more joules than grazers ate from them
(`r62ce-read/README.md`). Whether a scavenger guild could live on them, with the senses to find them, is open.

## Afterword

Contact eating is off by default in the code and on in every world since its first screen. A mouth's attack and intake
are both genes, priced as upkeep, so whether a lineage carries a strong bite, a large intake, both or neither is
selection's to settle. The theatre lane mapped ten worlds of rounds 62 and 63, and almost every eater in them carries
both at full strength. So neither has yet paid a cost that binds (the theatre lane's skin-map read of 2026-10-31; the
reading is inference).

## What here is inference

- **That eating from a distance hid the cost of search** is the author's reading of the corpse rule's 420 J beside
  the contact screens and the grazer budget. The numbers are measured; the reading is mine.
- **The kelp cropped by a fish** illustrates the rule's intent and cites no source.
- **That contact eating rewards speed, senses and grip** is the author's inference from the budget and from round 62's
  passing screens. Only speed has been shown to pay; the nose, the carried smell and the grip are built or designed
  and not yet read in a world.
- **That cannibalism follows from contact eating with no recognition** is the author's reading of D264's reason and
  round 63's kill tables.
- **"I think this was right"** is the author's judgement.
- **The afterword's reading of the theatre's skin map** is inference from a count of eaters' genes.

## Where it is

| file | what it holds |
|---|---|
| [`WorldContact.cs`](../src/Evosim.Core/Ecosystem/WorldContact.cs) | the bite's offer, the store share, the gut and its digestion |
| [`WorldMouth.cs`](../src/Evosim.Core/Ecosystem/WorldMouth.cs) | the wound, healing, cutting off a part, death at the root, the remains |
| [`contact-eating-spec.md`](../logbook/specs/contact-eating-spec.md) | the build specification, step by step, with every choice it made |
| [`kill-rule-read.md`](../logbook/specs/kill-rule-read.md), [`grazer-breeding-budget.md`](../logbook/specs/grazer-breeding-budget.md) | why the meal is not the limit, and the arithmetic of search |
| [`DECISIONS.md`](../DECISIONS.md) D219-D222, D224, D232, D237, D238 | the rulings, in order |
| [logbook 0147](../logbook/0147-grazers-paid-their-way-once-they-swam-faster-and-grew-up-slowly.md), [0148](../logbook/0148-round-63-the-film-world-fills-and-cheap-mouths-eat-each-other.md) | the screens that read it |
