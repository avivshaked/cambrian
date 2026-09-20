# The water held, and the bush refused

*2026-09-20, 04:40. Written by the agent as the pre-registration of round 41e, on the owner's
three rulings of 2026-09-19 (the water held, D100; a body born inside itself not born, D101;
run 41d to 15,000 s and stop, 0108's last section). The predictions are committed before the
queue starts.*

## What it asks

Round 41d asked whether a body that earns its outline stops folding, and the answer was no:
the cap closed the two-node knot's route and selection found the bush by another, a spread
body earning 1.7 times a leaf's light on the same matter with the cap binding, its parts
inside each other, and the solver paying a pair per overlap on every step until the pace was
0.08x (0108). Round 41e is that world with the two rules of the same afternoon. D101: a body
whose grown parts overlap their own non-adjacent parts deeper than a tenth of the smaller
part's thinnest half-extent is a stillbirth, at the one door every birth passes, founders
included; the physics of self-collision stays on. D100: the water is sampled once a body at
its root and held for a metabolic step, in place of every link on every physics step, which
the profile measured at a sixth of the wall (`logbook/specs/harness-profile-spec.md` §6;
`logbook/specs/cheapening-spec.md`).

The round asks three things. Whether a world that cannot be born folded stays affordable,
in pairs and in pace, over a full budget. What selection grows when the fold is refused and
the outline still pays: an open bush, a thin leaf, or nothing beyond round 40's bodies. And
whether the eaters found on this larder, which 41d could not read at half its budget.

## The world

Round 41d's, with two switches on: `rounds/launch-r41e.ps1`, which is 41d's launcher with
`-WaterHold 0.5` (`EVOSIM_WATER_HOLD`, header `water held 0.5 s`) and `-SelfOverlap 0.1`
(`EVOSIM_SELF_OVERLAP`, header `selfOverlap 0.1`). Everything else is 0108's: the tank of
100 m² by 60 m, the streams at 0.1 m/s with the fluid acceleration on, the grid, the
one-substance economy at 3,000 units and 100 J a unit with the overhead at 100 J and the
remineralisation at 0.002 /s, the silhouette cap on, the joint at 0.0001, dt 0.01, five
seeds, 30,000 s, a wall of 1,800 minutes, three arms at a time on workers 2, 3 and 4.

## The smoke and the screen

The screen is in `cheapening-spec.md` §4: seed 1 for 1,500 s at dt 0.01 on the branch's
build against the profile's run of the same seconds on 41d's world, both beside 41d's arms.
The drag pass 44% cheaper a link, the harness 24% cheaper a body, the seed 1.27 times
faster at the same crowd, 13 self-overlap stillbirths in 732 conceptions, contact pairs a
body-step halved at 1,500 s. The smoke on main after the merge is the launch note's, and
its `simHash` is the queue's expected hash.

## Rules

V1 to V4 as 0108's, with the header tokens `selfOverlap 0.1` and `water held 0.5 s` added
to V1's list. V5 as 0108's. A manifest reading `error` or `stopped` is censored and read at
its last sample, the wall is 1,800 minutes, and a seed past 4,000 bodies is stopped. A seed
that shows the adjustment the world needs is stopped as `manual-futility`, the round
re-planned, and the entry says what it showed. The fail-fast reads are E9's fold signature
and E11's refusal share from the first row they can be read on, E10 on the 5,000 s
snapshots, and the eaters at 10,000 s as a reading.

## Predictions

E1 to E8 are 0108's, carried whole; E1's band failed high in two seeds of 41d and is kept
as written, so that the same clause is read across the two rounds. E9 and E10 are 0108's
with the thresholds kept, since they are what 41d failed and what D101 is for. E11 and E12
are new.

| # | prediction | falsified by |
|---|---|---|
| E1 | **the crowd is the budget's**: `alive` between 350 and 1,100 at 5,000 s, and between 600 and 3,600 at 30,000 s, in 4 of 5 | the timeline |
| E2 | **the crowd grows as the holding shrinks**: `alive` at 30,000 s at least 1.3 times `alive` at 5,000 s in 4 of 5, and `margin s` under 150 at 30,000 s in 4 of 5 | the timeline; `margin s` |
| E3 | **the water is a treadmill**: `upt lim` between 40 and 95% at 15,000 and 30,000 s in 4 of 5, and `mat top` no higher than `mat deep` at both in 4 of 5 | `upt lim`; `mat top` against `mat deep` |
| E4 | **the larder is the reserve**: the marine snow between 0.10 and 0.40 of the budget at 15,000 and 30,000 s in 4 of 5 | `detritus J` |
| E5 | **the eaters found**: `inherit` reaches 50 after 5,000 s in 3 of 5, and in every seed where it does, `inherit` is at least 10 at 30,000 s | `inherit` at every 100 s |
| E6 | **the bust is slower**: the first boom above 300 inherited eaters falls under a sixth of its peak later than 2,600 s after it, or not before the budget, in every seed that has one | `inherit`, `booms.py` |
| E7 | **nothing throws and the disc stays mixed**: `diverged` between 0 and 10 per million jointed body-seconds in 4 of 5; the rim quarter 0.12 to 0.40 and `cols` within 0.85 to 1.05 of 1 − e^(−n/2211) at 5,000, 15,000 and 30,000 s in 4 of 5 | the manifests; `p3` over `alive`; `cols` |
| E8 | **the pace is affordable**: wall seconds per 1,000 simulated seconds per 1,000 living bodies between 700 and 1,800 at three arms in 4 of 5 (41d read 4,200; the screen's window reads about 1,000) | `run.json`, `scripts/reads/r41d-read.py` |
| E9 | **the physics is affordable**: `pairs/body` under 0.5 at 5,000 s and under 1.0 at 15,000 and 30,000 s in 4 of 5, and no seed's `pairs/body` doubles in each of two consecutive thousand-second windows past 0.5, which stops the seed under V5 | `pairs/body` by window |
| E10 | **the fold is gone**: in the 5,000, 15,000 and 30,000 s snapshots no living body at `maxParts` 16, fewer than 1% of the living at eight parts or more, and the probe's self-overlapping pairs per living body under 0.3, in 4 of 5 | `scripts/overlap/run.ps1` on the snapshots |
| E11 | **the refusal is a sieve, not a wall**: `self stillb` under 10% of the window's births at 5,000, 15,000 and 30,000 s in 4 of 5 (the screen read 1.8% over 1,500 s), and the floor stops firing by 1,000 s in 4 of 5 | `self stillb` against `births`; `floor` |
| E12 | **the outline still pays and is earned open**: the median parts a body at 30,000 s is at least 2 in 4 of 5, and the probe's self-overlapping pairs per living body is under 0.3 in the same snapshots (E10's clause, read here as the shape of what selection grew) | the probe's parts histogram |

E10 under D101 reads a different thing from 41d's: the rule refuses the deep overlaps at
birth, so the probe's count on a snapshot measures the shallow ones the fraction lets
through and the ones a jointed body makes after birth. E12 says what I expect to see instead
of the fold: bodies of two or more parts spread open, which is what the ledger says the cap
rewards. If E12's median reads 1, the refusal cost more than the outline paid and the world
went back to leaves.

## The two-sided readings

- **E9 and E10 hold, E8 holds:** the fold was the physics' cost, and the profile's two levers
  were the pace's. Round 41e's world is the base for what follows, and lever 1 of the
  profile (the solver read once a step and shared) is the next cheapening, after this round.
- **E9 and E10 hold, E8 fails:** the pace is the crowd's and the harness's per-link work on
  bodies that now carry more links each; read the fluid split and the harness per body-step
  from the footer, and lever 1 comes before the next round.
- **E10 holds and E9 fails:** the pairs are between bodies, not within them: a crowd in
  contact. Read `contactBodies` against `contactPairs` (one pair a touching body is a crowd,
  twenty is a fold), and the theatre.
- **E10 fails on the parts clauses and holds on the probe's:** the outline pays and selection
  grew open bushes of many parts, which is E12's expectation and not a fold. The parts
  clauses of E10 were written for a fold and are read as such, and the entry says what the
  bodies are.
- **E11 fails high:** the depth fraction is refusing bodies the physics would have resolved,
  or founders fold too often for the floor; read the refused genomes' overlap depths from a
  smoke with the fraction at 0, and the founding time.
- **E5 fails again:** the larder question of round 40 stands independently of the fold, and
  the eaters get their own round.

## What the round does not ask

Whether an open bush is a good body, only whether it is an affordable one. Whether the held
water changed any reading: the two rules go in together, because the round is the world's
base and not an ablation, and the profile's screen is the held water's own measurement.

## Launch note

LAUNCH-NOTE
