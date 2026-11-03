# Breeding waits for the brain: a birth needs the urge as well as the reserve

*Written on 2026-11-03 under the owner's delegation of that day (D291). The owner observed that a birth fires once the
reserve covers a child's price plus a margin, so no animal stores beyond that line, and asked for the problem to be
worked out. Drafted by an Opus agent from the record and the code, and reviewed by the coordinator, who settled rule 1
in form (b) as D299, with rule 2 in form (a) and rule 3 as the fallback. The owner may overrule any rule.*

## The proposal in one paragraph

A body breeds on the first half-second step at which its reserve covers a child's price plus a margin its genome holds
back. Nothing else has a say, the brain included, so a lineage stores up to a fixed line and no further. Three measures
come first, as agent work. Rule 1 gives the brain an output, the urge to breed, and a sense of how far the reserve
stands from the gate. A birth then needs both the urge and the reserve. The founders' urge starts high, so they breed
as today. Rule 2, a breeding store, already exists as gestation (D120) and changes only if the screen shows its account
dying unspent. Rule 3, more gates on the reflex, builds nothing. One world screen on the card tests rule 1 beside
D294's hunger sense.

## 1. The reserve's line alone times a birth

Every metabolic step of 0.5 s, the world breeds each living body whose reserve clears its gate. The gate is the
litter's price (each child's body, its first reserve and an overhead) plus the margin gene's seconds of the parent's
standing cost, its upkeep at rest (D098). In today's worlds only an adult conceives (D241), and a child under the mass
floor is refused (D087). No other condition applies. The brain's outputs are Power, Bend and Grip, so it has no say.
Today's worlds run with the reserve cap off, so the margin alone limits storage, and it has no ceiling. Founders draw it
from 0 to 600 s, and the pool's grazers carried 600 s. A body cannot store more when food is rich, or breed sooner when
its young would do well. The check is `World.IsSolvent` in Core.

A second way to pay exists. Under gestation (D120), a heritable share of each step's positive income moves into an
account that upkeep cannot touch, and a child comes when the account holds its price. The reserve keeps the rest, with
no line on it. Every world built on round 55's settings flips a child's mode at a chance of 0.08 a birth. In rounds 48
and 49 gestation lost. Gestating lineages left 0.26 to 0.64 births per death, against 1.11 to 1.25 for lump breeders,
which pay in one sum. Round 49 also lost 37 to 51 % of what was banked in accounts that died with their holders
(logbook/0120, logbook/0122). Nobody has read gestation among grazers.

Some of what grazers do is measured. In `r62cf`, living grazers held a median of 160 to 400 s of standing cost, and at
most 710 to 909 s (fat's step 0, `logbook/specs/fat-step0/`). The pool's gate was 720 J, about fifteen meals
(`logbook/specs/grazer-breeding-budget.md`). In the smell screen's control world, starvation killed 64 and 44 % of the
grazers that died on seeds 6202 and 6203, at median ages of 256 and 362 s. Three things are not measured: the reserve at
each birth against the gate, how a parent fares after a birth, and whether margins have drifted.

No sense reads the reserve near the gate either. D294's hunger sense reads 0 above a minute of reserve. At the pool's
gate, about 790 s of standing cost, D245's energy sense reads 0.96, and at half the gate 0.93 (arithmetic).

The premise needs one correction. Biology does gate breeding on condition. In a model of storing under a varying food
supply, storing paid only at middling food with stores not yet full [FDT11 Results, Fig. 1], and storing, breeding and
upkeep could all pay at once [FDT11 Discussion]. So the best choice depends on the body's state. In a seasonal model of
zooplankton, stores paid mainly by allowing breeding before the feeding season, and also topped up breeding from income
[VJTF09 abstract]. Biology seems to have no reflex that fires the moment a line is crossed (inference). Fish skip
spawning in poor condition, a hormone made by fat (leptin) tells the brain whether the body can afford to breed, and
dynamic energy budget theory feeds a reproduction buffer over time. All three are outside the literature review. They
are leads for its next round, and no rule here rests on them.

## 2. Three measures come first, and need no ruling

1. **The reserve against the gate** of every adult grazer in the checkpoints of `r64s2-d` and of the smell screen's
   control, read through Core's own gate. By the code, a lump breeder never stays above its gate for more than one step.
   So the read shows how much of the guild sits at the line and how much sits below it.
2. **What a birth leaves a parent**, read from the lineage. For each grazer that starved, it gives the time since its
   last birth, against grazers that never bred. Many starvations soon after births would show the reflex draining
   parents. A margin of hundreds of seconds should prevent that (inference), unless margins have fallen.
3. **The drift of the margin and the mode**, window by window, on grazer births in the recorded worlds from `r62cf` to
   `r65`, against the pool's 600 s. A rising margin means storing already pays, which favours rule 1. A falling one
   means breeding sooner pays here. The same rows give gestation's share of grazer births.

Each takes an hour or two of agent work and minutes of machine time (inference).

## 3. Rule 1. A birth waits for the brain's urge

Each body gains one brain output, the urge, on its root part. It comes after the joint and hold outputs, as D268 placed
the hold, so existing units keep their places. At each metabolic step a body breeds only if its urge reads above −0.5
and its reserve, or its account, clears the gate as today. The margin stays, as the floor a parent keeps. A fresh urge
starts at a bias of +2 with the founders' usual links (D147). By D268's arithmetic for a fresh hold, it then falls below
−0.5 less than once in a hundred times, so founders breed as today. The output costs nothing, since thinking is free
(D146). A gestating body whose account already holds its price banks no more, so its share stays in the reserve. The
rule ships behind a switch that is off by default, and the switch has a knockout setting that holds the urge high.

The sense is a choice between two forms. In form (a), the urge reads today's senses, hunger among them. In form (b), a
gate sense joins them. It reads log2 of the reserve over the gate, clipped to the range −1 to 1, so it reads 0 at the
gate, 1 at twice the gate and −1 at half. Form (b) is settled: under (a) no sense moves near the gate, and D262 asks that
a new feature start strong. If brains never link the sense, the dial-back drops it.

A line fixed in the genome cannot follow the body's state, and section 1's model says the best choice does (inference).
The urge needs a sense that looks ahead, since plasticity evolved only where individuals could anticipate their
environment [BWWR15 Results, Fig. 1]. Waiting has a payoff here. Each birth advances the parent's ageing clock (D128),
the form in which a larger brood steepened jackdaws' ageing [BBV20 p.6]. A more autocorrelated environment always
favoured allocation to survival [MK07 Results, Fig. 2c]. This world's year and blooms last thousands of seconds, so
larger stores are expected with the urge on (inference).

Three alternatives are rejected. The urge in place of the margin would let a random brain breed its reserve down to
nothing and starve, so founders could not breed as today. A chance of birth that rises with the surplus would remove the
line. But its shape would be our constant, and it would read nothing the body senses. A gate on crowding is a world rule
that reads the world's state, which the owner ruled out on 2026-10-30. D124 has already rejected a fixed time between
births.

On the host, the urge adds one unit a body to each 0.01 s brain step and one comparison at the breeding check. The
comparison sits beside the gate, so D124's founder cap and D073's ranking by surplus read the same number as before. The
card runs the brain and already downloads every unit's output after each block, so the urge needs no new copy
(inference from `gpu-runner.template`, settled in the build). The gate sense goes up to the card with the reserve, one
float a body. Births run on the host in both engines, so the gate itself needs no card copy. The kernels are
regenerated, and a card identity sitting checks the switch off and on, as D248's did. The birth row gains the parent's
reserve over its gate and its urge, and the stats gain a count of the steps the urge held back.

## 4. Rule 2. The breeding store exists, and opens only if the screen asks

A share of each meal going into a breeding store is D120's gestation. It is in every world, it needs no build, and rule 1
gates it as it gates a lump birth. One change waits on the screen. In form (a), the account stays sealed, as D120 ruled.
In form (b), a body whose reserve is spent draws its account back before it starves, as animals resorb eggs (a lead the
review has not read). Form (a) is settled now, and form (b) follows if more than a third of what grazers bank dies in
accounts.

## 5. Rule 3. The reflex's extra gates are built already

In today's worlds a body conceives only once adult, after growing for 60 s (D241). Fat is built behind a switch (D247),
but it raises a reserve cap that today's worlds leave off, so it adds no room. D124 rejected a minimum time between
births. Rule 3 is the fallback. If the urge goes unused, the margin and adulthood stay the rule, and the record says so.

## 6. What each rule costs and risks

The table gives each rule's cost, its two risks and how it starts overpowered. At 25,002 bodies the farm stops a world
as a runaway.

| | Rule 1, the urge and the gate sense | Rule 2, the store | Rule 3, the gates |
|---|---|---|---|
| Record | amends D087's and D098's gate, D146's outputs and D120's banking; new tunables refuse older configs, and fixtures are re-recorded | form (b) amends D120 | nothing |
| Host and card | a unit, a sense and a comparison a body; the kernels regenerated and sat | form (b), one transfer on the host | nothing |
| A world that empties | founders breed as today, and a lineage that stops breeding leaves no heirs (inference) | form (b) keeps parents alive | as today |
| A world past the ceiling | the urge only withholds births the reflex would make, so a runaway would come from stores saving lives (inference) | the same | as today |
| Overpowered (D262) | the last word above the margin, read every step, free, with the gate sense; the dial-back drops the sense, then adds a delay before a birth | built only on evidence | none |

## 7. The screen runs rule 1 beside the hunger sense

The world is the candidate world, arm D of D264's phase 1, with contact on each part's true shape (D228). It runs as the
smell screen's control ran it, to 24,000 s on seeds 6202 and 6203 (`rounds/env-r65-c.ps1`), on the card, in three arms.

- C, that control, which is not rerun if the switch-off sitting matches it bit for bit.
- H, C with D294's hunger sense. It is also the hunger sense's world screen.
- U, H with rule 1 in form (b).

H and U on both seeds make one card group of four worlds. U is then resumed from 21,000 s with the knockout on. A resume
replays bit for bit, so the pair differs only by the urge. Each mark is read on grazers from 21,000 to 24,000 s.

| Mark | Bar |
|---|---|
| 1. Grazers live | D244's floor in every judged window, and no runaway, in U |
| 2. The urge is used | adults able to pay are held back in at least a tenth of their steps; the median reserve at a birth is at least 1.2 times the gate, against about 1 in H by construction |
| 3. The urge matters | the knockout moves grazer births or starvations by at least a fifth |
| 4. Reported, not judged | the margin's drift in U against H, in founder standard deviations (D254); gestation's share of grazer births, and the share of banked joules lost in accounts |

U passes when it meets marks 1 to 3 on both seeds, and rule 1 then joins D254's combined round. If U empties, the fresh
bias rises to +4 and U runs again. If mark 2 or 3 fails, the reflex was not what held grazers back in this world. Rule 1
then stays built and off, and rule 3 stands. The pre-registration is committed before launch. The smell screen's groups
of three took 3 to 3.5 h, so the group should take 4 to 5 h and the knockout under an hour (inference).

## 8. The order runs beside the forager kit's

1. The three measures, now, beside the kit's three.
2. Rule 1 built on the host and the card alongside the kit's rules 2 to 4, by a builder the coordinator reviews. Its card
   sitting is shared with rule 4's, since both regenerate the kernels.
3. The screen, then U's knockout, then rule 2's form (b) if mark 4 calls for it.

The nursery holds every birth (`src/Evosim.Nursery/Tank.cs`), so rule 1 runs only in worlds, and the kit's nursery arm
goes on meanwhile. Margins falling hard, with few starvations after births, would put the screen after that arm.

## 9. What it amends

Rule 1 amends the gate of D087's rule 2 and D098: a birth must still clear it, and must also have the urge. It extends
D146's outputs as D268 did, and changes D120's banking while a birth waits. Its sense joins D245's and D294's. Form (b)
of rule 2 would amend D120 again. Rule 3 changes nothing, and D241, D247 and D124's rejection stand. Every rule follows
D262, and nothing in the index is reversed.

## Sources

None of these papers was read for this specification. Each claim reaches its paper through the synthesis rows of
`research/LITERATURE-REVIEW.md` and the locators they give. The last column says how the review read each one.

| Key | Source | How the review read it |
|---|---|---|
| BBV20 | Boonekamp, J.J., Bauch, C. and Verhulst, S. (2020) Experimentally increased brood size accelerates actuarial senescence and increases subsequent reproductive effort in a wild bird population. J. Anim. Ecol. 89, 1395–1407 | at p.3 and p.6 |
| BWWR15 | Botero, C.A., Weissing, F.J., Wright, J. and Rubenstein, D.R. (2015) Evolutionary tipping points in the capacity to adapt to environmental change. PNAS 112, 184–189 | main text in full |
| FDT11 | Fischer, B., Dieckmann, U. and Taborsky, B. (2011) When to store energy in a stochastic environment. Evolution 65, 1221–1232 | in full |
| MK07 | Metcalf, C.J.E. and Koons, D.N. (2007) Environmental uncertainty, autocorrelation and the evolution of survival. Proc. R. Soc. B 274, 2153–2160 | in full, figures by caption |
| VJTF09 | Varpe, Ø., Jørgensen, C., Tarling, G.A. and Fiksen, Ø. (2009) The adaptive value of energy storage and capital breeding in seasonal environments. Oikos 118, 363–370 | abstract |

The leads named in sections 1 and 4 are outside the review: skipped spawning in fish, leptin as a signal of fat stores,
dynamic energy budget theory's reproduction buffer, and the resorption of eggs. None was read, and no rule rests on them.
