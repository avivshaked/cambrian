# Proposal: a bigger meal for a grazer that kills

*The speed agent, 2026-10-25, draft. For the owner's ruling, on D230. D230 found that a grazer's meal is bounded by
the one part it eats. It asked for this proposal, and asked it first to check whether a dial already ruled could reach
the old corpse rule's meal without a new rule. The read behind it is `logbook/specs/r62bd-read/README.md`. Absorbed
into DECISIONS.md on ruling, then deleted. Each claim says whether it was read in the code, measured, or is inference.*

## The problem in plain words

Grazers in this world eat only what their mouths touch (D219), and a bite feeds as it wounds (D220). Over the last
three screens they have never earned their keep. In the newest screen a grazer kept about 70 J from each kill
(measured: 61 to 77 J). Under the old corpse rule it kept about 420 J (measured: 418 to 434 J), and grazer lines
then bred for 20 generations. Grazers kill as often as they did then, or more often. What they lack is the meal.

The reason is in how a bite is paid (read in the code, `World.OfferBite`). A bite takes the share of one part's tissue
that its wound took. The store share (D224) adds the same share of the body's reserve. So the most one touch can ever
yield is one part and that part's slice of the store. A typical leaf has several parts. The rest of its body becomes
remains when it dies, and the grazer swims on, so almost all of those remains close unbitten: 6 to 7 MJ burnt in each
world, against a few tens of kJ that grazers kept from them.

## Can a dial already ruled reach the meal?

No. Each was checked against the code and the screens.

| Dial | What it does to the meal | Evidence |
|---|---|---|
| The bite damage scale (D226) | It takes a deeper share of the part, up to the whole part. It cannot reach past the part. | At 2, 4 and 16 times the meal stayed at 61 to 77 J, and at 4 times every blow already took the whole part (measured, `r62bd`) |
| The store share (D224) | It adds the part's slice of the reserve. It is already on. | `r62cs`: the meal rose from 24 to 27 J to 56 to 89 J, then stopped (measured) |
| The intake scale (D221) | Under contact eating it lifts only the per-step cap on what a mouth keeps. | At 16 times the cap cut 15 to 24% of what bites offered (measured, `bite-cap.txt`). Lifting it entirely would raise the meal by a quarter at most, to about 90 J (inference) |
| The remains window (D220) | It keeps the rest of the body edible for longer. | From 60 s to 1,500 s, grazers' kept watts did not rise (measured, `r62cs`, 0.39 to 0.47 W at every window). Grazers do not come back to the remains |

The per-step cap matters to both options below, so it is stated here (read in the code). Each step, a mouth keeps at
most its intake gene, times its surface area, times the intake scale, times the step, in joules. Anything offered
above that in one step goes to the water. So a rule that hands a grazer a whole body in one step would throw most of
it away. A bigger meal must either arrive over many steps, or be exempt from the cap.

## Option A: a kill feeds the killer from the whole body (recommended)

**The rule.** When a bite kills a body, the killer swallows the whole body. Its remaining tissue and
store go into the killer's gut, and no remains are left. The gut is digested at the killer's mouth rate, the same
per-step cap a bite meets, less the intake waste. What is still in the gut when the killer dies goes with its own
body. A body that dies another way (starvation, age, the mass floor) leaves remains as today.

This keeps D219's rule that a mouth eats only what it touches. The killer touched the body it killed, and nothing is
eaten at a distance. It also keeps D220's slope: a bite still pays as it wounds, and the kill adds the rest.

**The predicted meal.** Close to the corpse rule's, about 350 to 450 J kept a kill. That is inference. Under the
corpse rule, a kill's whole body gave about 420 J to whoever ate it, and the bodies being killed now are the same
kind of leaf. The gut hands that over at the mouth's own rate, so the cap does not throw it away.

**What it changes.** Grazers get a reason to kill whole bodies rather than graze parts. Remains from kills disappear,
so the sea floor holds only bodies that died of age or starvation, which fits D219's wish that carrion not be a way
of life. Larger prey becomes worth more than small, which pushes toward hunting (inference).

**Costs.**
- **Replay.** One new switch, off by default, so every recorded world replays unchanged. The gut is a new number per
  body, saved in checkpoints only when the switch is on, as D227's counters are. So no checkpoint written before it is
  refused. The config fixtures are re-pinned and the crowd fixture re-recorded once, as for every new switch.
- **Card.** Nothing changes on the card. The meal is a rule of the world, and it runs in `Evosim.Core` on the host,
  while the card steps only the physics.
- **Build.** About the size of D224. The gut, its digestion in the step, the kill's handover in `LeaveRemains` (which
  already knows the killer), the books, and the tests that the books still close.

**A variant, simpler and less physical.** The whole body is kept at the kill, less the waste, with no gut and no cap.
It is one line in place of a new state, but a mouth of any size could swallow a body of any size in one step. I do not
recommend it.

## Option B: a bite takes from the parts beyond the one it touches

**The rule.** A bite's wound spreads to the parts joined to the part it touches. Each joined part takes
a share of the wound, a new dial starting at one half, and pays the biter in the same way. The per-step cap still
applies.

**The predicted meal.** About two to three times today's, so 150 to 230 J a kill. That is inference: a typical leaf part
has one to three joined neighbours, and a touch lasts long enough to strip them only if the grazer stays. It stays
short of the corpse rule's meal. The cap's cut rises as each touch offers more, so this option needs the intake scale
raised with it, to 32 or 64.

**What it changes.** A grazer strips a plant faster where it touches, and armour on one part protects its neighbours
less. It keeps the meal tied to contact time, so grazers that stay at a plant eat more. It does not end the waste of
the remains.

**Costs.**
- **Replay.** One new dial, off by default (a share of 0), with the same fixture work as option A.
- **Card.** Nothing changes on the card, for the same reason.
- **Build.** It is smaller than A. The wound walks the bitten body's joints and pays each joined part. There is no new
  state.

## The screen, for whichever is ruled

The same shape as `r62bd`, read on D222's tests.
- **The base world.** `r62cs-w500` (contact eating, the store share, a 500 s window), with the intake scale at 16 and
  the bite damage at 1, as D230 rules.
- **The arms.**
  - Option A: one arm, the gut on. Option A's variant is not screened unless A fails.
  - Option B: two arms, a share of 0.5 at an intake scale of 16, and the same share at 64.
- **The worlds.** Two seeds, 6201 and 6202, founded, 24,000 s, in one group on the card.
- **The controls, not run again.** `r62cs-w500` (the base), `r62bd-x2` (the deeper wound) and `r62-h` (the corpse
  rule).
- **First, a check.** A 4,500 s run of one seed, as D227's check was. It confirms the meal moves before the machine
  spends a day, and it reads the cap's cut and the gut's size.
- **The overshoot.** It is read as before: grazers breeding lines while the leaves fall below half of the base's.
  Option A is the more likely to overshoot, since its meal is the corpse rule's and the corpse rule's own worlds held
  only 6,000 bodies. If it does, the roll-back is a slower digestion rate, a dial on the gut.

## Decisions for the owner

1. **Which rule, if either?** Option A, the killer swallows the whole body into a gut it digests at its mouth's rate
   (recommended), or option B, a bite spreads to the joined parts, or neither of them.
2. **If option A: does a kill leave any remains?** The draft says no: the killer takes the whole body, and only deaths
   by age or starvation leave remains. The alternative is that the gut takes only what the killer's size could hold,
   and the rest stays as remains. That needs a rule for what a body can hold. D229's fat rule, not yet built, could be
   that rule: it would cap a body's reserve and let fat tissue raise the cap, so the same cap could bound the gut, and the two rules
   would meet there.
3. **If option B: the starting share,** one half by default, and whether the intake scale may go to 64 for it.
4. **The screen as drafted,** with the check first: yes or changes.
