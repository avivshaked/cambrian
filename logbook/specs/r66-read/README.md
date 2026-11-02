# r66, the mouth screen: the read

*By the coordinator on 2026-11-02, the morning the screen ended. Judged against the pre-registration
(`logbook/specs/r66-prereg/predictions.md`, committed at `952cbd3e` before any world of the screen ran).*

## What ran

Six worlds of r65's C under D275's rule, a mouth's blow landing only where its mouth point touches. All ran for 24,000 s on
the card from `scratch/wt-mouth` at `952cbd3e`:

- M, the rule alone;
- Mk, the rule with the leaf's tissue energy times 4;
- M4k, the rule with it times 16.

Each ran on seeds 6202 and 6203, as two groups of three. Group 6202 ran from 00:39 to 03:14 and group 6203 from 03:15 to
06:31 on 2026-11-02. Every world ended on its budget with its books closed (largest energy residual 0.0002 J). The control
is r65's C, licensed by the identity sitting's check 2.

The reads are `read-seed.sh` beside this file, run once per seed, with outputs in a folder per seed. Grazer deaths and
grazer-on-grazer kills are in `grazer-deaths.txt` and `cannibal.txt`.

## The marks

| Mark | Seed | M | Mk | M4k | Bar | Result |
|---|---|---|---|---|---|---|
| 1. grazers at least 30 in every window from 12,000 s | 6202 | 12.6 at 12,000-15,000 s | 3.8 | dead | every arm | fail |
| | 6203 | 117 or more | 1.1 | dead | | fail |
| 2. leaf share of bite income, 21,000-24,000 s | 6202 | 0.84 | 1.00 (0.00 MJ) | none | 0.8 in Mk or M4k | fail in substance |
| | 6203 | 0.83 | 0.89 | none | | |
| 3. grazers at least C's and leaves at most half C's | 6202 | 0.49 and 1.50 of C's | collapsed | collapsed | Mk or M4k | fail |
| | 6203 | 0.66 and 1.83 of C's | collapsed | collapsed | | fail |
| 4. landed share, 21,000-24,000 s over 12,000-15,000 s | 6202 | 0.174 / 0.163 = 1.07 | | | 1.25 in M | fail |
| | 6203 | 0.177 / 0.171 = 1.04 | | | | fail |
| 5. pace | both | 2.6 and 2.1 times real time | 3.3, 3.2 | 5.1, 5.4 | no world under 0.5 | pass |

Mark 2 passes on paper in Mk, but Mk-6202's last window held a mean of 6 grazers and kept no measurable bite income, so it
says nothing, and no arm overshot. By the pre-registration, the outcome is "Mark 4 fails, and grazers struggle in M": the
nursery retrains the pool with D275 on before the film's round, rather than the dials going higher.

## What the screen found

- **The richer leaf starves the world it was meant to feed.** A leaf at 4 times the tissue energy costs 4 times the light
  to build. Mk-6202 held 1,015 bodies at 3,000 s against C's 2,269, before any animal arrived. M4k never held more than
  about 40 bodies after 9,000 s, and its grazers all starved. Grazers there died at a median age of 95 to 110 s, of
  starvation in 90 to 100% of deaths. The dial trades the number of leaves for their richness, and grazers are limited by
  finding leaves, so they gained nothing. The pre-registration named this as the way the arms could fail. The
  coordinator's reasoning that each contact's joules would count directly left out that the leaves thin, as r62g's leaf
  energy 2 had already shown under the corpse rule.
- **The rule alone moves grazers onto leaves.** M's leaf share is 0.84 and 0.83 against C's 0.77 and 0.74. Bite income
  from other animals falls from 0.14 to 0.05 of it on 6202, and from 0.19 to 0.08 on 6203. Grazers killed by grazers fall
  from 68 to 4 on 6202 and from 204 to 47 on 6203. It is the first world of this line whose grazers take more than 0.8 of
  their bite income from living leaves. It does so by making eating animals harder, not by making grazing pay more: M's
  bite income kept in the last window is 0.09 MJ against C's 0.21 on 6202, and 0.26 against 0.64 on 6203.
- **The rule costs grazers numbers, and on one seed nearly their survival.** M-6202's grazers fell to a mean of 12.6
  between 12,000 and 15,000 s, then grew to 628 by the last window. M-6203's never fell below 91. In the last window M held
  0.49 and 0.66 of C's grazers, and 1.50 and 1.83 of C's leaves.
- **Aim does not evolve in 24,000 s.** About one mouth blow in six lands, at every stage: 0.16 to 0.17 from 12,000 s, 0.17
  to 0.18 at the end, and 0.17 and 0.18 over the whole run. c8's film window under the old rule found 16% of leaf kills with
  the mouth on the leaf, so the rule removes the misses and selection does not shift the rate. By inference, the founders
  from the pool, trained under the old rule, set an aim that 10 to 15 generations do not move.

## The predictions, scored

| Prediction | Result |
|---|---|
| landed share on founders 0.15 to 0.35 | 0.19 in the identity sitting: held |
| M holds the floor on both seeds | failed on 6202 for one window |
| M's grazers 0.3 to 0.8 of C's | 0.49 and 0.66: held |
| M's leaf share within 0.04 of C's | rose 0.07 and 0.09: wrong. The coordinator's first draft predicted a rise of 0.02 to 0.08, and revised it on c8's 16% against 13%, which measured leaf kills against wounds and not income |
| Mk near C's grazers | collapsed: wrong |
| M4k overshoots, or its leaves fall early and its grazers never rise | the second: held |
| aim rises 1.1 to 1.5 times | 1.07 and 1.04: at the bottom of the range, failing the mark |
| pace falls at most 5% per body | M stepped more body-seconds per wall second than C, in a different sitting: held |

## What follows

- **The nursery retrains the pool with D275 on**, as agreed with the owner on 2026-11-01. A tank steps the farm's own
  `Simulation`, so a tank world with contact eating, true-shape contact and the switch on carries the rule. The pool's ten
  `leaf-direction-sparse` swimmers are the first to retrain.
- **The leaf's tissue energy is not an overshoot dial for grazing.** No cost or yield dial within today's rules raises a
  grazer's income per second of search without thinning its food or feeding predators too (r63h, and this screen). The
  overshoot for D275 is therefore founders that aim.
- **Whether the film's round takes the rule** goes to the owner with the nursery's result. M shows the rule's trade:
  grazers that live on leaves at half to two-thirds of their numbers, with the eating real on screen.
