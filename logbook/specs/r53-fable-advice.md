# Round 53: Fable's advice after round 52 seed 1

*2026-09-29, about 16:00. The answer of an advisory subagent (Fable, read-only), saved as it came, asked by the owner for a recommendation on round 53 given round 52 seed 1's read (`scratch/wt-r52/logbook/specs/r52-read/seed1-30000.txt`), D131's plan (`logbook/specs/r53-larder-proposal.md`) and the two literature searches (`logbook/specs/r53-literature.md`, `logbook/specs/r52-lifespan-literature-r7.md`). Measured = read from a file; inferred = its arithmetic on measured numbers; guessed = judgement.*

## 1. What seed 1 says, and what it does not

Measured: D129 works as a placer. K1 held (median fcorp/fcorpm 6.0, 48 J against 7.6 J), K2 held (223 of 223 over corpses), K3 held (170 corpses eaten, 109 units, against 1 in round 51). The meal does not buy a living: K4 failed (median age 140.5 s against 104.5 s, 1.34x against a bar of 1.5x) and K5 failed (no mouth-rooted line living; 13 of 223 founders bred, 19 children; 0.76 corpses eaten per founder, so most ate once). The gene behaves as the screen said: LS1 to LS4 held (age by bin 1,852 / 2,262 / 2,784 s; early mean 0.98, late 1.17; upkeep share 0.62 to 0.51; living 10/50/90 = 0.96/1.25/1.35). The larder is unchanged in size and still unused: 17,000 corpses, and 170 eaten is 0.4% of 38,656 deaths. Joints are leaving: 1,283 jointed at 1,000 s to 219 at 30,000 s (`runs/r52-s1.md`); seed 2 at 27,820 s has 6 jointed and `jnt inh` 0. Population 3,431 against round 51 seed 1's 4,717, a new realisation, so it attributes nothing.

What it does not say: whether mouths that ate lived longer than those that did not (K4 is a median over a mixture; a pre-53 read from the feeding log); whether the 8% upkeep rise a gene at 1.17 costs thinned the crowd; whether the bed has any current; and nothing about movement, since the mouths ate on landing.

Seeds 2 and 3 would change the answer if K5 holds in either (a still mouth line lives, so Rule A becomes a real trade and movement is less urgent); if LS4 or O1 fails with a high gene (the price is too low, and D130's "remove one" applies); or if K3 fails (the 170 would be a founder-size accident).

## 2. D131: keep Rule B, drop Rule A

Rule A should not go into round 53.
- It does not raise the bed's food. At steady state the flux from corpses into the bed water equals the arrival flux whatever the decay rate; the rate sets only the standing stock (inferred from `logbook/specs/r52-larder.txt`). Seed 1's 43.8 W over 22,000 m2 is 2 mW/m2, and a still stomach catching 85% of what lands on its square metre gets 1.7 mW against a keep of 110 to 440 mW. The proposal's "breaks even on a 26 J corpse" is true for the minutes a corpse dissolves and false on the arrival rate, which the proposal concedes ("the food is the same in total").
- It removes what D129 just proved works. The settled stock is arrival flux times mean settled life: about 114 kJ now (measured 165 kJ), 22 kJ at a 6-minute half-life, 9 kJ at 139 s. The piles that gave K1 its 6x and the mouths their 170 meals would be an order of magnitude smaller.
- The literature gives it no analogue (C1, C3): microbial rates do not rise on settling; what is fast on the bed is scavengers; microbes compete by spoiling carrion. The simulator already has the scavengers. Rule A is the microbes winning by fiat.
If the owner wants the number rather than the argument, run it as a refutation screen. Hollows concentrate piles without destroying them.

Rule B is worth doing, screened against its own fallback. Measured support: `upt lim` 59 to 69%, `mat deep` 0.011 against `mat top` 0.002; directional support in C7. The en-route corpse loss at the slow rate is 22% at 45 m, 12% at 30 m, 9% at 25 m (inferred). The larger unstated gain is the snow: at 2 mm/s and a 2,000 s mean life it falls about 4 m, so it reaches a 25 m bed at a few percent and a 45 m bed never (inferred). The cost: five rescaled dials and the loss of geometric comparability with rounds 44 to 52. The fallback, `EVOSIM_MATTER_BUDGET` 22,500 to 27,000 at 45 m, is one knob. Choose by screen.

## 3. Movement

The wall is measured: evolved strokers 2 to 6 mm/s, the best hand-built stroke 7 cm/s (`logbook/specs/r47-read/swim/summary.txt`), a mouth on the larder needs 4 to 8 cm/s. The drag-only model cannot reward a symmetric stroke (`fable-propose-reactive-thrust.md`); the tensor is a sound, bounded proposal, approved for round 52 in D127 and not built. Two costs it predates (inferred): the card's body kernels carry the link inertia too, so GPU parity breaks until ported; and with joints going extinct, round 54 must inoculate strokers. Recommendation: not in round 53 (unbuilt, unbenched, a third change), but built and benched during round 53's week so round 54 is thrust alone on round 53's world.

## 4. Lifespan: hold

Every LS clause held, and the birth push and the rate-against-onset question have no seed-1 symptom. Changing the gene alongside depth would move the corpse supply and its location in the same round. Queue a lifespan-values round after the larder and movement rounds, with the birth push at 0.1 to 0.3 doublings per adult-size child and the gene acting on the base with the doubling fixed in seconds as the two options.

## 5. Before pre-registration

Reads, no machine time: mouth founders' age at death split by ate / did not eat; body speed within 1.5 m of the bed from `positions.jsonl` as a proxy for the bed's current; seeds 2 and 3 through the reader.

Screens, 10,000 s founding runs, settled decay 0 unless named, read with `scripts/reads/r52-larder.py`:

| screen | knobs | decides |
|---|---|---|
| B30 | depth 30, tilt, shore, reef, founder and island depths scaled 30/45 | bed water against 0.6-0.9 J/m3; `upt lim` against 73%; `% on floor`; alive within 20% of 1,053; pace |
| B25 | the same at 25 m | as above; reject if the crowd sits in the surface film |
| F | 45 m, matter budget 27,000 | the control: if B30/B25 beat F, depth wins; if not, the one-knob fallback |
| A (optional) | 45 m, settled decay 0.005 | predicted: settled joules fall over 10x, K1 toward 1-2, fewer corpses eaten, bed water unchanged; if so, Rule A is dropped on a measurement |

A difference inside one realisation's wingspan decides nothing.

## 6. Questions for the owner

1. Does Rule A go into round 53? No: knob at 0, a superseding DECISIONS entry. Or screen A first (an hour).
2. Depth or matter budget? Decide by B30, B25 and F.
3. Is the thrust built now, for round 54? Yes, during round 53's week, benched on the hand-built stroker.
4. Do the lifespan values hold through rounds 53 and 54? Yes; a values round later.
5. Does round 53 keep a mouth clause (corpses eaten at or above round 52's)? Yes.
6. Hollows before the seep, and is a second energy source wanted at all? Hollows first, round 54 or 55; the seep a separate ruling.
