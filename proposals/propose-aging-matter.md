# Proposal: matter that ages, so fresh food is worth more than old

*2026-10-19. On the owner's idea the same day that the world has no entropy of matter, and that one might help
creatures prefer some matter over other matter. For the owner's ruling, after the open square forms oases;
absorbed into DECISIONS.md on ruling, then deleted. The evidence is `logbook/specs/matter-ocean-scan.md`.*

## 1. What the world has now

Energy already runs one way. Sunlight becomes tissue, tissue burns, and the heat leaves the world. Matter
has two forms. A dead body's tissue becomes detritus, which carries food energy for whatever eats it.
Detritus remineralises at a fixed rate into spent matter, which only leaves can take up. Since D207, a living
body's spent matter goes first to its own leaves, then is held or released by how much the body eats.

What the world lacks is any difference within a form. A corpse feeds as well on its last day as on its first.
Detritus a thousand seconds old is the same food as detritus made a second ago. Every unit of spent matter is
as good as every other, so nothing gives a creature a reason to prefer one patch of food to another beyond
its amount.

## 2. What the ocean does

The scan's findings, in short:

- Organic matter ages from labile to refractory. About 640 of the ocean's 662 Pg of dissolved organic carbon
  is refractory, and deep dissolved carbon is about 5,600 years old (Baltar et al. 2021).
- Microbes spoil carrion quickly. Fish carrion left a day or two with microbes was about four times less
  likely to be eaten (Burkepile et al. 2006, seen only as a press release).
- Food differs in quality as well as amount. Herbivores grow at only 10 to 30% efficiency on producers with
  typical carbon-to-nutrient ratios (Elser et al. 2000, abstract).

## 3. The proposal

Two parts, each behind its own switch and each screened alone.

**A spoilage clock.** A corpse's and detritus's food value falls with age, at a half-life a screen sets. The
energy lost goes to heat, as the bacteria that spoil carrion respire it. The matter is not lost: it
remineralises as now. Fresh kills and fresh corpses are worth most, so a scavenger or hunter that arrives
quickly wins, and a creature that eats only old detritus starves.

**A refractory share.** A small fraction of remineralised matter turns refractory: still in the water and
moved by the current, but taken up by nothing for a long time, returning at a slow rate a screen sets. It is
a sink to balance the fountains. Today's loss is burial in the desert, which works only by place; a slow,
spread-out sink would keep the open water poor everywhere.

A third part, a composition per unit of matter (its carbon to nutrient ratio, so that creatures can choose
between foods), is set aside. It would add a dimension to every matter field and every body, and the world
should first show whether the two simpler parts change anything.

## 4. What it implies

- **For the creatures.** Hunting and fast scavenging gain against slow grazing on detritus. Grazers on living
  leaves are untouched, since a living leaf does not age. That may help grazers against detritus eaters,
  though the scan gives no number for it.
- **For the record.** Two new tunables. Each refuses older config files as every new tunable does, and the
  recorded fixtures re-record. Worlds run before the switch still replay.
- **For the machine.** The spoilage clock needs an age per corpse and per detritus cell: new state, a
  checkpoint format change, and a copy on the card. The refractory share is one more field, transported like
  the others, also on the card. About a day of agent build for both, then the tests and a card identity
  check.
- **For the risk.** Both parts take energy or matter out of reach, and the square already collapsed once. They
  wait until the square holds oases under D207, so that a change in the world can be traced to one cause.

## 5. The question for the owner

Should the spoilage clock and the refractory share be built after the square forms oases, each screened alone
before any round uses it?
