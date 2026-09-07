# Proposal: a world in which moving blindly pays, by a little

*Fable, 2026-09-07, late. For the owner, who asked how nature solved the chicken and egg of
movement and sensing, and said the best path is "an early world where random movement is
better than being stationary, even ever so slightly". Built on review round 6 (research §0,
Q11; logbook/0073), D082 and 0072. Absorbed into DECISIONS.md on ruling, then deleted.*

## What the literature says, in one paragraph

Nature never solved the chicken and egg, because it never had it. Undirected movement paid
on its own, before any sense, wherever two things held. The habitat was patchy at the scale
a mover crosses, so that "chemotaxis evolved in bacteria that were already capable of
self-propulsion in random directions" [W11 p.1]. And the body was large enough for its own
motion to refresh the water it fed from, which starts at a radius near 20 µm [KB96 p.1] and
is absent below 0.6 µm [D97 p.4]. A bacterium does not stir; it relocates "to find greener
pastures" [P77 p.8]. The first sense then rode on that walk: a scalar compared with itself a
few seconds earlier, biasing how often the walk turns [MK72 p.1; SBB86 p.1], and the scalar
could be the cell's own energy state [SJ10 p.1]. In animals, one cell sensed and moved before
any nervous system did [J11 p.1; KDL13 p.4].

## Why this world cannot do that

A body here feeds from a field cell 5 m across and one layer deep, shared with every other
body in it, at one price for all of them (DESIGN §5A.2c, §0p). Staying does not deplete a
body's own water and moving does not refresh it. A body that swims within its cell gains
nothing and pays the stroke; a body that swims to the next cell finds, in a closed world
with even patches, the same water within 12%. So undirected movement is worth zero
minus its cost, and selection has deleted the muscles in every round because that was the
right answer. Cheaper neurons (D082) make the bud cheaper to lose; they do not make it pay.

The bodies are 0.03 to 0.3 m long, a thousand times above the scale at which motion stops
mattering, and they already cross a patch several times in a lifetime at 0.02 to 0.07 m/s.
The physics is on our side. Only the feeding model does not know it.

Two things need no change. The sense is there: a `Differentiate` neuron reading `Energy`
is the bacterial rule in one neuron, and mutation can draw it (founders draw from the MVP
operator set, which lacks it; `Mutator.RandomOp` draws from all). And dispersal is there:
newborns are placed beside the parent (D077), so the moment water is local, leaving pays.

## The options

### A. A depletion halo around each body (recommended)

Each feeding body keeps one number, the fraction of its own water it has already eaten.
Its intake is drawn from the cell at that discount; the discount grows with what it takes
and relaxes back toward zero at two rates, a diffusive one that is always on and a flow one
proportional to the body's speed through the water over its own length. A body that sits
still eats itself into a hole; a body that moves, or sinks, or is carried by the current,
leaves the hole behind. Sizing: a halo of a few body volumes, and a diffusive refill time
set so that a stationary stomach at today's median density loses a stated share of its
intake, found with the ledger before any worker is spent (D069).

- Pros. It is the physics the literature names [P77; BP77; KB96], at a scale where it
  applies. Blind movement pays by exactly as much as it refreshes, no more, and never above
  the cell's own density, so there is no runaway and nothing is paid to move for its own
  sake (0031's lesson). Crowding becomes local without a finer field. Dispersal from the
  parent pays. It touches consumers only; producers eat light.
- Cons. One new per-body state and two new tunables, both unmeasured. A per-step change,
  so every seed is a new realisation. It interacts with the frozen-availability allocation
  (§0p): the discount must be applied to a body's demand before the cell's share is
  priced, so the fix of R1 stays a fix. A stationary stomach earns less than today at the
  same density, so the base world's stomach lines must be re-read, which is what the
  controls are for.

### B. A finer horizontal field

Split each patch into cells of about a body length, so a crowd depletes where it sits and
a mover finds fuller water one cell over. The field code already has patches, layers,
mixing and a horizontal diffusivity; this is more of them.

- Pros. No new per-body state. Uses machinery that exists and is tested. Structure at the
  scale a mover crosses, which is [W11]'s condition.
- Cons. Twenty-five times the cells per layer at 1 m, and the mixing step scales with it.
  A single body in a 1 m³ cell still does not deplete its own water; only a crowd does, so
  the payoff to one mover is weak until the world is dense. It re-opens D061's patch
  question at a new scale. A stationary body in an empty cell is as well off as a mover.

### C. Encounter feeding

For absorptive tissue, intake grows with the volume swept: a diffusive floor equal to
today's intake plus a term in relative speed times cross-section, capped by satiation
(D062), after [SL23]'s ballistic encounter kernel.

- Pros. The simplest to state and to price in the ledger. Directly rewards motion.
- Cons. It rewards speed whatever the direction and whatever the water, which is the
  definition of paying creatures to move; sinking and drifting bodies collect it for free;
  without the cap it runs away, and with the cap the cap decides the result. It does not
  make crowding local or dispersal pay. It is the option most likely to give a false
  positive on 0072's M3.

### D. Nothing in the world; the price round alone

- Pros. One change at a time, as D079 asks.
- Cons. The analysis above says it cannot work: a cheaper bud in water that cannot reward
  moving is still selected away, only more slowly. It spends a day to measure a zero.

## Recommendation

**A, the halo**, with its refill by relative flow, so that sinking, drifting and swimming
all count and nothing is credited to the stroke itself. Sized by the ledger. Read with the
instruments 0072 already carries: `spd jnt` against `spd rig`, `food jnt` against
`food rig`, `dep jnt` against `dep rig`, and the clade scorer for the stomach lines.

On sequencing, two ways, and I recommend the second:

1. The halo alone on the contact world at today's prices, then D082's price round on the
   halo world. Clean, and two days.
2. The halo and D082's prices in one build, with round 29 as the price control and round
   28 as the world control. The ledger has already measured what the price does to a
   stationary body, so the price is not a confound in the reading that matters, which is
   whether a moving stomach out-eats a still one; and a bud needs both to be visible in
   one round. One day. The cost is that D079's one-change rule bends again, and this entry
   says so rather than hiding it.

Either way the round is pre-registered on the base world round 29's reading leaves, with a
prediction that can fail: over the last 6,000 s, in at least three seeds of five, the
jointed guild's food exceeds the rigid guild's by a stated margin while the stomach clade
still meets the goal. If the halo makes moving pay and the muscles still go, the next
question is the stroke's price, and it is a smaller one.

## What the owner rules

1. Whether the world gets a halo (A), a finer field (B), encounter feeding (C), or nothing
   yet (D).
2. Whether the chosen rule runs alone or with D082's prices.
3. The halo's two numbers, if A: the ledger proposes them in the pre-registration and the
   owner sees them there.
