# The water held, and the bush refused

*2026-09-20, 04:00. Written by the agent as the pre-registration of round 41e, on the owner's
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
2,200 m² by 45 m with the bed tilted 30 m (corrected 2026-09-20, below), the streams at 0.1 m/s with the fluid acceleration on, the grid, the
one-substance economy at 3,000 units and 100 J a unit with the overhead at 100 J and the
remineralisation at 0.002 /s, the silhouette cap on, the joint at 0.0001, dt 0.01, five
seeds, 30,000 s, a wall of 1,800 minutes, three arms at a time on workers 2, 3 and 4.

*Correction, 2026-09-20 afternoon.* This section said "the tank of 100 m² by 60 m" until
then. The launcher's defaults and every seed's header say `space tank r=26.46 m (2200 m2),
depth 45` with the bed's relief at 1.5 m and its tilt at 30 m, which is D093's tank and the
one rounds 39 to 41d ran in. The mistake was the agent's, written from memory of round 37's
world and not from the header. No prediction moves: E7's column scale of 2,211 was taken
from the true world, and every other clause reads a share or a count. The agent then
carried the same wrong tank into two messages to the owner, and a recommendation built on
it (a "wider" tank of 400 m²) was withdrawn the same afternoon.

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

*2026-09-20, 04:14 to 04:16.* Seeds 1, 2 and 3 launched on workers 2, 3 and 4, each refreshed
from the main tree first, the queue's hash check against the main-tree smoke's `simHash
46335d9f…` passing (`runs/r41esmoke-main`, worker 2 after the stale locks were cleared:
header `silhouette on · selfOverlap 0.1 · water held 0.5 s`, one self-overlap stillbirth among
the founders) and `prereg.json` at this entry's commit (`fe0b790`) beside the arm and beside
the run. Every manifest reads `simHash 46335d9f…`, `coreHash c4821b33…`, `configHash
f6416481`, `physicsJobWorkers 0`, `gitCommit 71e8bc3`; every header `dt=0.01`, `idle 0.0001`,
`silhouette on`, `selfOverlap 0.1`, `water held 0.5 s`. The config hash is the branch screen's
(`r41escreen-s1`, `cheapening-spec.md` §4), so the screen is a realisation of this world. The
queue is `scratch/r41/queue-e.ps1`, detached, and seeds 4 and 5 launch as arms end. The
early look draws seed 1 at 3,000 and 6,000 s from its snapshots on worker 6.

## Stopped at 10,000 to 17,000 s, for the machine and not for the world

*2026-09-20, 14:00. Written by the agent after the owner's reboot.*

The three seeds were stopped at 12:42 with `stop-arm.ps1 -Reason manual-other`. Seed 1 was
at 13,200 s, seed 2 at 17,700 s and seed 3 at 10,400 s. Seeds 4 and 5 never launched. The
cause was the agent's, and it had nothing to do with the round. Watch loops left behind by earlier sessions had multiplied over four days. Once their
session was gone, each opened a terminal window for every process it forked, and the owner
could not use the desktop (CLAUDE.md's gotcha on background shell loops; HANDOFF). The arms were healthy when they
were stopped. The shared world replays bit for bit, so a relaunch of a seed reproduces what
is read here; what the stop cost is wall time. The owner chose to take the stop as the round's end and to weigh changes before the next
one. So the round is read as it stands: three seeds of five, censored. Nothing below is a
30,000 s clause.

### The read

| clause | seed 1 | seed 2 | seed 3 | reading |
|---|---|---|---|---|
| E1 `alive` at 5,000 s (350 to 1,100) | 996 | 1,003 | 1,206 | two hold, seed 3 high |
| E2 `alive` over its 5,000 s count, at the last read | 1.34 at 10,000 s | 1.65 at 15,000 s | 1.23 at 10,000 s | on the way; unread at 30,000 s |
| E2 `margin s` at the same reads | 93 | 93 | 287 | seed 3 holds a wide margin |
| E3 `upt lim` (40 to 95%) | 85% | 76% | 75% | holds; top under deep in all |
| E4 snow over the budget (0.10 to 0.40) | 0.19 | 0.22 | 0.17 | holds |
| E5 inherited eaters, peak after 5,000 s | 110, and 99 at 13,000 s | 28 | 1 | one seed of three founded |
| E7 diverged per million jointed body-seconds | 0 in 5.7 M | 0 in 2.9 M | 0 in 7.8 M | holds; nothing thrown at all |
| E7 rim quarter; `cols` over uniform | 0.34; 0.91 | 0.28; 0.86 | 0.37; 0.86 | holds, `cols` at its floor |
| E8 wall s per 1,000 s per 1,000 bodies (700 to 1,800) | 2,070 to 2,220 | 1,150 to 1,310 | 2,920 | holds in the leafy seed, fails in the two jointed |
| E9 `pairs/body` at the last window | 0.06 | 0.06 | 0.22 | holds; no doubling anywhere |
| E10 bodies at 16 parts; share at 8 or more; probe pairs a body | 0; 0; 0.004 | 0; 0; 0.001 | 0; 0; 0.002 | holds at every snapshot probed |
| E11 refused at birth over births, by window | 1.4% at 5,000 s, 3.7% at 10,000 s | 0.3%, 1.3%, 1.9% at 15,000 s | 2.8%, then 10.0% at 10,000 s | holds in two; seed 3 on the line and rising |
| E11 the floor's last firing | 500 s | 300 s | 600 s | holds |
| E12 median parts a body at the last snapshot | 2 | 3 at 17,000 s (1 at 15,000 s) | 3 | holds where it can be read |

E6 cannot be read: no boom above 300 eaters had happened. The probe ran on the 5,000 and 10,000 s snapshots of every seed. It also ran on seed 1's at
13,000 s and on seed 2's at 15,000 and 17,000 s (`scripts/overlap/run.ps1`'s program,
called on the files).

### What it means

The fold is gone. Round 41d read 1.3 to 3.0 contact pairs a body at 15,000 s and up to 2.7
self-overlapping pairs a body in the probe. Here the pairs read 0.06 to 0.22. The probe finds between zero and eleven overlapping pairs
in a whole world, every one of them shallow or made by a joint after birth. The refusal that bought it cost one birth in fifty in two
seeds. In seed 3 it climbed to one in ten by 10,000 s. My reading, as inference: seed 3's jointed line of three- and four-part bodies sits close
to the overlap depth, and its mutants cross it often. That is a lineage pressing against
the rule. A sieve is for that, and the 30,000 s read would have had to watch it.

The world did not go back to leaves, which was E12's worry. The parts histograms moved
upward in every seed. Seed 3 went from a median of two parts at 5,000 s to three at
10,000 s with 161 bodies of four. Seed 2 is the one worth a picture. It is a leafy world of one-part bodies, and in it a
rigid three-part body went from 307 of 1,655 at 15,000 s to 663 of 1,736 at 17,000 s. The
median jumped from one part to three in two thousand seconds.
That is the outline paying and being earned open, in a line with no joints at all.

Nothing was thrown: no divergence in sixteen million jointed body-seconds across the three
seeds, where round 37b threw 43 in eleven million. I do not know why, and the held water is
the change I would test first, since it is the one per-step force that differs.

The pace is the round's failure. The held water and the refusal made the leafy seed affordable. They left the jointed seeds
at 2,100 to 2,900 wall seconds per 1,000 s per 1,000 bodies. A jointed world carries two
to three links a body, and the harness is priced per link. The last windows before the stop are contaminated by the terminal storm and are
not read. Lever 1 of the profile, the solver read once a step and shared, was built the
same afternoon and is being validated against seed 1's recording
(`logbook/specs/harness-profile-spec.md`).

The eaters founded in one seed of three, and in that one they stood at 99 inherited at
13,000 s. Whether they hold is the question the stop took away.

### What I saw

The pictures are reconstructions from snapshots (`theatre-snap.ps1 -From snapshot`), bodies
upright and at adult size, so they show plans and places and not postures. I looked at seed 1 at 3,000, 6,000 and 10,000 s and at seed 3 at 10,000 s. Each is a wide,
thin scatter across the whole disc in the top quarter of the water, with the bed far below
and bare. Seed 3's bodies at
10,000 s are loose jointed stacks of two to four parts with daylight between the parts, and
I found nothing folded in any frame. The replaying theatre was not opened on this round,
and the entry says so because the rule asks it to: a censored round read on reconstructions.
Nine bodies in ten live above 11 to 16 m, and fewer than one in twenty-five below 25 m.
The bed's shallowest arc is at 30 m. So the slope D093 built lies under water that almost
nothing visits.
