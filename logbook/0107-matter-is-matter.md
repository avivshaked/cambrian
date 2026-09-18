# 0107 — Matter is matter

**2026-09-18, night, pre-registered before launch**  ·  round 41: round 40's world on the one-substance economy (D098 as amended), the matter cut from 11,000 to 3,000 units and a child's overhead raised from 25 to 100 J, five seeds, three arms at a time (D095), read for the crowd the matter builds, the water it regenerates and the larder it leaves the eaters

---

## What it asks

Whether a world with one kind of matter runs, and what it does with the count. Until
today the world had two currencies. A body was made of tissue that cost matter drawn from
the water at conception. It ran on energy that light or food supplied, and that energy
left the world as heat. D098 as amended this evening (`DECISIONS.md`, `logbook/specs/
economy-spec.md`) replaces both with one substance in two states. A unit of matter is
*charged*, carrying 100 J, or *spent*, carrying none. A leaf takes spent units from the
water and charges them with light. Every living cell burns charged matter to live, and
what it burns returns to the water spent, at the body's own point. An eater moves charged
matter from a body to itself. A child is charged matter handed over from its parent's
reserve, and nothing draws the water at a birth. A corpse is the tissue and the reserve
together, sinking as marine snow that an eater can take or that the water slowly
remineralises back to spent. Both books close on every row: the energy audit as before,
and the matter identity, which now counts every joule as a hundredth of a unit.

The first thing the build showed was that the count was never the economy's to set. In
the old world a child cost a fixed draw of matter, and that draw was the cap on how many
bodies the stock could build. Under one substance a body holds its tissue plus its
reserve, and a leaf's tissue is 0.38 J. The smoke at 11,000 units built 6,300 bodies in
1,100 s with the surface stripped twenty-fold (`r41smoke2-s1`). A per-body standing cost
does not bound the count, because it sets where the water settles and not how many bodies
share it. A dearer tissue does bound it, yet at 100,000 J/m³ and above the founders spend
their whole stake growing and never breed (`r41t1e5-s1`, `r41t2e5-s1`, zero births by
2,500 s). What bounds it is what a breeder must hold: the price of a child, which is the
child's body plus its reserve plus the overhead burnt at the birth. So the count in a
closed one-substance world is the capacity over the holding, and the two dials are the
budget and the overhead. The owner ruled the budget the crowd's dial ("I agree with your
recommendations. Let's proceed"), and the screens tonight set both.

## The world

Round 40's launcher on the new build (`rounds/launch-r41.ps1`). The tank is 2,200 m² and
45 m deep with the tilted bed. The streams run at 0.1 m/s with the acceleration force,
corpses decay at 0.005/s, the light's reach is 6 m, and the step is dt 0.01 for 30,000 s.
Three dials move against round 40. The matter budget is 3,000 units, from 11,000. The
child's overhead is 100 J, from 25. And the economy is D098's. A charged unit carries
100 J. A leaf takes 0.3 spent units per square metre of lit face per second, halving at
0.05 units/m³ of spent water. Snow nobody eats remineralises at 5e-4 per second, a
half-life of 1,400 s. Eating costs 0.1 J per joule eaten. There is no reserve cap. The
breeding margin gene is drawn from 0 to 600 s in the founders and mutates at 0.08 (genome
format 6). The tissue value stays at 500 J/m³ and the founders at their usual size. The
tissue ceiling is off, since in a closed world the budget is the ceiling. The header carries `economy rho 100 J/unit,
uptake 0.3 /m2/s at K 0.05 /m3, remin 0.0005 /s, handling 0.1, reserveCap off, margin
0-600 s at 0.08`, `tissue 500 J/m3`, `overhead 100 J`, `founders 0.15-0.4 m` and
`matterBudget 3000`. The build is `simHash 50cf58e4…`, `coreHash 729a0de1…`.

Eleven screens set the two dials, all at dt 0.02 and seed 1. Every one was stopped as
soon as it had answered, under the owner's rule from tonight that a run which has shown
the adjustment it needs is not completed. At the old overhead the crowd ran away at
11,000 units and starved at the floor at 400. It plateaued near 380 at 1,000 units and
750 at 2,000, with the water stripped and the eaters never founding. At an overhead of
100 J, 2,000 units plateaued at 400 bodies and 3,000 at 650, and 4,000 was on the same
line at 1,200 s. The base round takes 3,000 units and 100 J. Its bars come from
`r41o100b3k-s1` at 2,200 s, since that screen had answered before 5,000 s. At that second
it read 648 alive and 697 births, 288 of them by 1,000 s. The living held 1,619 units,
0.54 of the budget, and the snow 914, 0.30. The spent water stood at 0.005 units/m³ at
the top against 0.007 at depth, and the leaves were bound by uptake on 78 to 93% of their
steps. The mean depth was 3 to 4.5 m, the shading 8 to 9.5%. There were 8 inherited
eaters and 386 inherited jointed bodies of 648. The mean margin was 214 s and the adult
scale 0.98. The rim quarter held 0.14 to 0.24 of the crowd. The pace was 1.64 times real
time beside a render and a second screen.

## Rules, before scoring

V1: every header carries the tokens above and round 40's; every config `joulesPerUnit
100`, `matterBudgetUnits 3000`, `perOffspringOverheadJoules 100`; every manifest one
`simHash`, one `coreHash`, one `configHash`, `physicsJobWorkers 0`, and `prereg.json`
naming this entry's commit beside the arm and beside the run. V2: `audit` 0.0000% and
`mat resid` 0 on every row. V3: `wraps` 0 on every row. V4: every diverged dump has its
trace. V5: a manifest reading `error` or `stopped` is censored and read at its last
sample; the wall is 1,800 minutes; and a seed that shows the adjustment the world needs
is stopped as `manual-futility`, the round re-planned, and the entry says what it showed.
The entry is not written until the world has been watched in the theatre, and frames of a
live arm are looked at at about 3,000 and 6,000 s.

## Predictions

Round 40's three seeds at their last samples (0106): 2,490 to 2,599 alive, the living
holding 0.71 to 0.75 of the stock, the eaters founded in two seeds of three, the rim
quarter 0.26 to 0.32, no throw in 8.3 million jointed body-seconds, and the pace 957 to
1,283 wall seconds per 1,000 simulated seconds per 1,000 bodies. The screen's numbers are
above.

| # | prediction | falsified by |
|---|---|---|
| E1 | **the crowd is the budget's**: `alive` between 350 and 1,100 at 5,000 s, and between 600 and 3,600 at 30,000 s, in 4 of 5 | the timeline |
| E2 | **the crowd grows as the holding shrinks**: `alive` at 30,000 s at least 1.3 times `alive` at 5,000 s in 4 of 5, and the table's `margin s` under 150 at 30,000 s in 4 of 5 | the timeline; `margin s` |
| E3 | **the water is a treadmill**: `upt lim` between 40 and 95% at 15,000 and 30,000 s in 4 of 5, and `mat top` no higher than `mat deep` at both in 4 of 5 | `upt lim`; `mat top` against `mat deep` |
| E4 | **the larder is the reserve**: the marine snow (`detritus J` over 100) between 0.10 and 0.40 of the budget at 15,000 and 30,000 s in 4 of 5 | `detritus J` |
| E5 | **the eaters found**: `inherit` reaches 50 after 5,000 s in 3 of 5, and in every seed where it does, `inherit` is at least 10 at 30,000 s | `inherit` at every 100 s |
| E6 | **the bust is slower**: the first boom above 300 inherited eaters falls under a sixth of its peak later than 2,600 s after it, or not before the budget, in every seed that has one | `inherit`, `booms.py` |
| E7 | **nothing throws and the disc stays mixed**: `diverged` between 0 and 10 per million jointed body-seconds in 4 of 5; the rim quarter 0.12 to 0.40 and `cols` within 0.85 to 1.05 of the uniform expectation 1 − e^(−n/2211) at 5,000, 15,000 and 30,000 s in 4 of 5 | the manifests, `diverged-read.py`; `p3` over `alive`; `cols` against `alive` |
| E8 | **the pace is affordable**: wall seconds per 1,000 simulated seconds per 1,000 living bodies between 700 and 1,800 at three arms in 4 of 5 | `run.json`, `pace-survey.py` |

These are recorded and not predicted. The jointed share, which the screen founded at 60% under the
link's half photosynthesis and its near-free idle charge. The leaves' and the eaters'
depths, since 0106 read the leaves' median as a birth statistic. The shading, three times
round 40's in the screen at a quarter of the crowd. The burn and the remineralisation
each window against what reached the snow, which is the regeneration itself. The corpse
count and the floor's holding. Adult scale, investment and brood. Stillbirths and refused
placements against births. The footer's wall split under the new world step. And how the
spent water is distributed by depth, since burning returns it where the bodies are and
the founders' corpses put it where they sank.

## The two-sided readings

- **E1 fails high:** the holding fell faster than the screen's 2,200 s could show, and
  the crowd is on its way to the population ceiling. Read the units a body holds, the
  locked matter over the count, and stop the seed under V5 if the count passes 4,000.
- **E1 fails low:** the eaters or the water took the crowd down; read E4 and E5 first,
  and `births` against `deaths` by window.
- **E2 fails with the margin still high:** breeding sooner does not pay, and the reserve
  is a hedge the world rewards; read the margin's distribution on the lineage rows against
  the death ages.
- **E2 fails with the margin low and the count flat:** the holding is the tissue and not
  the reserve, and the count is the budget over the body; read `body frac` and
  `adult scale`.
- **E3 fails high:** the leaves are uptake-bound at every step and the water never
  recovers, a crowd starving on a treadmill it cannot slow. Read the burn against the
  leaves' fixation, and the spent density by cell.
- **E3 fails low:** the water is not stripped and the light binds, as in round 40; the
  budget could be higher and the count is not the matter's.
- **E4 fails high:** the snow is a sink nobody eats, and the remineralisation is too slow
  for a world without eaters; the retry is a shorter half-life.
- **E4 fails low:** the eaters clear it or the remineralisation returns it before it
  accumulates; read E5.
- **E5 fails:** a larder of reserves and corpses did not found an eater line in most
  seeds, and the founding is the question before the bust is. Read the eaters' births by
  depth and the snow's depth.
- **E6 fails:** the eaters still boom and starve with a larder ten times round 40's; the
  bust is not the larder's, and the read turns to the eaters' own life history.
- **E7 fails on a throw:** the anatomy against 0097's; on the rim: the leaves' and the
  eaters' rim shares apart.
- **E8 fails high:** the per-cell remineralisation or the per-body burn priced the step;
  the wall split says which.

## What the round does not ask

The reserve cap is off and stays off until this round has read the hoard. The tissue value
is 500 J/m³ because every higher value screened froze the founding. A value that founds
is a later screen. The 4,000-unit screen
says a larger crowd is one dial away, and the round runs at 3,000 so that the count's drift
has room under the wall. The shelf waits for a world whose count is settled. Whether the
eaters hold is a ten-seed question (D095); five seeds count it here.

## Launch

*Appended as the seeds launch; each with its header verified and its manifest's hashes.*
