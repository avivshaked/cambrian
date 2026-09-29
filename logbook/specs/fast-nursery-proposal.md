# A faster nursery that still selects on the currency

Fable's proposal of 2026-09-29 night, written at the owner's request ("a nursery that is both fast
and keeps the principle of natural selection"), on the nursery as built (D133, DESIGN §8.6) and
run 2's first generations. Saved as it came; the owner rules on section 5's list.

Marks: [K] known from the code or tonight's logs; [B] believed, an inference from them; [G] a guess.

## 1. What "keeping natural selection" means here

Natural selection is differential reproduction caused by heritable variation in fitness, in an environment the organism did not choose. The nursery already keeps the heritable variation (the ecology's own mutation operator, at its rates and sensor pool) and the fitness *component* (joules of income, on a fixed body, with upkeep constant, so income is what the brain changes) [K]. What it replaces is the environment (corpses laid round the body) and the reproduction rule (an archive). So the honest claim is: *directed selection on a fitness component the ecology itself pays*. Breeding, in the owner's sense, is selecting on a trait the designer thinks leads to fitness.

Distance to food is that trait. Its optimum coincides with the currency's only when reaching implies eating. Here it nearly does: a mouth within `IntakeReachMetres` 0.5 m plus the corpse's radius (0.27 m for 40 J at 500 J/m³) eats at `Intake × area` units per second, so a 40 J corpse in reach is emptied in a step or two [K for the rule, G for the bodies' actual intake values]. But the optimum still differs: the bench's hand forager circled a corpse at 2.3 m for 900 s and ate nothing [K]; a body that sinks onto the bed, where every corpse settles within 30 s of being laid (sink 0.05 m/s from 1.5 m) [K], scores well on proximity without sensing anything; and a body whose centre stays 1 m from a corpse can never eat it. Proximity as a *score* would fill the archive with circlers and sitters.

The defensible middle, in order of how little it bends the principle:
- Proximity as the archive's *descriptor* (behaviour bin), never as the score. The nursery already does this [K]. It keeps approachers that do not yet eat alive as stepping stones without rewarding them.
- The currency over a short horizon. A forager that needs 900 s to cover 4 m is not one.
- A curriculum on the *environment* (how dense the carrion is, how strong the water), with the round's own layout as the held-out test. Selection in the ecology already acts under a changing environment; what must not change is the thing scored.
- Not: tie-breaking zero-income brains by distance. That is breeding at exactly the moment the currency is silent, which is when it matters.

## 2. Why the current nursery is slow, and why its gradient is weak

**Slow.** A 900 s episode is 2.4 to 3.0 s single-threaded [K]. The 1 s smoke on this config split the wall as physics 26%, world 52%, harness 15% at one body [K, at founding, so a guess for a full episode]; the 900 m² × 20 m tank with 4.5 times the cells took 2.6 to 4.8 times as long [K]. So about half to two thirds of an episode is the water: the 1 m snow grid (4,000 cells) mixed, advected and settled every half second, plus the 5 m matter grid [B]. The body's own physics, 90,000 steps of a two-part chain, is about 1 s and cannot be cut without changing the step, which is ruled out.

**Weak.** Income is quantised: light 0, snow 0.1 to 0.9 J, and one corpse is 40 J [K]. The ancestors' second-half distance to the nearest corpse is 8 to 13 m while the scent reaches 10 m [K], and their closest approach is 1 to 3.4 m against a reach of 0.77 m [K], so an ancestor scores 40 J only when the gyre happens to carry it through a corpse. From tonight's 200 ancestor and child episodes, that happens in roughly one episode in twenty [B]. The "+10 J best over ancestor" in generation 1 is exactly one lucky hit averaged over four episodes (40/4) [B], and run 2 shows the incumbents' running means falling from 10 to 25 J to under 1 J by generation 5 as they are re-scored [K]. The archive learned nothing; it recorded luck.

**Credit assignment, in numbers.** With a hit rate p = 0.05 and a 40 J quantum, one episode's income has mean 2 J and standard deviation about 8.7 J. A brain that *doubles* the hit rate moves the mean by 2 J. To see that at two standard errors, even with common seeds halving the variance, takes about 70 to 80 episodes per candidate [B, arithmetic on a guessed p]. At four episodes the standard error is 4.4 J against a 2 J effect: selection is a lottery, and the head-to-head cannot save it, because both sides of it are draws from the same lottery.

The lever is the quantum, not the horizon. Lay the same 200 J as 50 corpses of 4 J scattered over the same 0.5 to 4 m: a drifting body then eats one to three by chance, the standard deviation falls to about 6 J, and a brain that doubles the rate moves the mean by about 8 J. That is a signal-to-noise gain of roughly six, so about 35 times fewer episodes for the same verdict [B]. The score is still joules eaten. Scent cones sum, so 50 small corpses smell about as one large one at range [K]; each reads weaker near it (half-scale 20 J), which the check in the round's own layout will expose if it matters.

## 3. Three designs

Common to all: the score is income; births held; the knockout (scent and chemical read 0) and the ancestor are the controls; held-out seeds; a child replaces an incumbent only by beating it on common seeds by more than twice the paired standard error (tonight's rule has no margin and churns on noise [K]). Cost arithmetic assumes 2.7 s per 900 s episode at 400 m² × 10 m, the water 55% of it and the body 45%, and 12 threads at the 0.68 efficiency tonight's 4-thread run showed (540 CPU-s in 198 s wall) [B].

**A. Dense carrion, short horizon, small tank (recommended first).** Episode: 300 s, tank 100 m² × 5 m (500 snow cells), the round's current and rules otherwise, 50 corpses of 4 J at 0.5 to 4 m, body 1.5 m above the bed, purse held at 2,000 J so the Energy sense reads the same as today. Measured: income, plus the distance descriptor. Selection: the archive as built, 40 children, 8 episodes each. Cost per episode: body 1.1 × ⅓ ≈ 0.37 s, water 1.5 × ⅓ × ⅛ ≈ 0.06 s, about 0.45 s [G, within a factor of two]. Generation: (40 + 10) × 8 = 400 episodes ≈ 180 CPU-s → about 22 s wall; 100 generations in 40 minutes. Wrong things it can select: a mover that eats more by moving at all, without sensing (the knockout scores the same; report over-knockout and over-ancestor separately, as the check already does); a sitter on the bed; a brain tuned to a small tank's gyre. Check: the knockout, held-out seeds, and the *final* table run in the round's own tank with 5 × 40 J at 900 s, which is design C's episode.

**B. Curriculum on the water.** Design A with the current at 0 until a body passes its knockout on held-out seeds, then the round's 0.1 m/s. Reason: the bench's two-part chain swims 3 to 11 cm/s in still water [K] and the water carries a sitter at about two thirds of the RMS, 7 cm/s [K, CLAUDE.md]; every candidate body is one-tailed and swims in a curve [K]. A brain that cannot hold station has no gradient to climb while it is learning to turn. Same cost as A. Risk: the still-water brain fails in the current; the stage-2 check says so, and that answer is itself D133's measurement for these bodies. Whether a tank accepts current 0 is untested [G].

**C. The ecology's own layout with real statistics.** The nursery as built (900 s, 400 m², 5 × 40 J), population 24, 32 episodes each: (24 + 10) × 32 × 2.7 ≈ 2,940 CPU-s → about 6 minutes a generation, 100 generations in 10 hours. This is the faithful baseline and the right final examination for A and B's winners; as the search it is slow and, at p ≈ 0.05, still marginal.

## 4. Several bodies in one world

The saving is bounded by the water's share, about half an episode [B]. The 400 m² tank (r = 11.3 m) fits two bodies 20 m apart with their corpses 14 m from the other body, outside the 10 m scent [K], but the gyre moves a body 8 to 13 m within the episode [K], so by the second half each smells and can eat the other's corpses (the mouth's proportional split), the descriptor reads the neighbour's corpses, and a child's score depends on its tenant-mate's brain, which breaks the common-seed pairing. Shrinking the tank saves more and couples nothing. Not recommended.

## 5. Recommendation and what to run first

Build A, with B's still-water stage as a switch, and keep C as the final table. Before any of it, four measurements, each a few minutes on the free cores:

1. **Where the wall goes.** `Evosim.Nursery --time-one` on three episode configs made by the farm's own binding as `nursery-cfg-s` was: the current 400 m² × 10 m; 100 m² × 5 m; and 100 m² × 5 m with `EVOSIM_MIXING 0`, `EVOSIM_H_MIXING 0`, `EVOSIM_MATTER_MIXING 0`. Read `steps` seconds. The first ratio says how much the cells cost; the second says whether the fields, not the current, are the cost. If the small tank is under 1 s per 900 s, the water is solved without touching the fields.
2. **The hit rate and the quantum.** Score the ten ancestors on 32 episodes at both layouts: `--generations 0 --episodes 32 --final-episodes 8` with the current `--corpses 5 --corpse-joules 40`, and with `--corpses 50 --corpse-joules 4`. Read the per-episode mean, standard deviation and share of zero-income episodes from `evaluations.jsonl`. This replaces my guessed p with a number and fixes the episode count the power arithmetic needs.
3. **The positive control.** Graft the bench's Braitenberg law (`HandForagerBench`'s second and third cuts) onto one of the ten bodies by hand and run it through A's episode and C's. If a hand-wired brain cannot score on a one-tailed body in 0.1 m/s water, no search will, and the answer to D133 for round 52's bodies is "not with this body", which is worth knowing before 10 hours of generations. This also calibrates the ceiling (200 J).
4. **The bodies' intake.** Read the consumer node's `intake` off the ten ancestors' genomes. If it is small, a corpse takes many steps to eat and the score is less of a step function than I have assumed.

Rulings the owner has to make: (a) whether a curriculum on the environment (carrion density, still water first) keeps the principle, given that the score never changes; (b) whether the search may run in a smaller tank than the round's, with the round's tank as the examination; (c) the horizon, 300 s; (d) no proximity tie-break (my recommendation); (e) the load, 12 threads; (f) the round's world for the inoculation test stays open as D133 left it.

Transfer back: inoculate each winner and its knockout in one seed of the next round, read both lines' income and persistence from `lineage.jsonl` and `absorptive.jsonl`. Note that round 52's world prices work at ×0 and neurons at 0 W [K], so a thrashing brain is free in both places; if a later round prices work, the nursery's config must carry it and the winners are re-scored.

## 6. What I know, believe and guess

Known: the score, the controls, the archive rule, the 40 J quantum, the reach rule, the scent cone and its 2 m grid, the sink and settle rates, the mutation rates, tonight's timings, drift distances and the decay of the lucky scores. Believed: the water is over half an episode; p ≈ 0.05; the gain from small corpses; the 0.68 thread efficiency carrying to 12 threads. Guessed: the small tank's per-episode cost; that a tank runs at current 0; that the ancestors' intake empties a corpse in a step; that the streams near the bed are at full strength (no shore fade in this config, but the depth profile I did not read).