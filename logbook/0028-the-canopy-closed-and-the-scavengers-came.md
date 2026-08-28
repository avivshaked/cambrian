# 0028 — The canopy closed, and the scavengers came

**2026-08-28**  ·  Milestone 4

A food chain assembled itself in `g-c1.0-s2` — clearance 1.0, seed 2 — while the day's attention was
on the muscle economy ([0027](0027-the-prize-was-smaller-than-the-entry-fee.md)). Nothing was
arranged for it. It is the first result here that looks like ecology rather than like a calibration.

## The sequence

| t | alive | absorptive | inherited | shading | J/m³ | food share |
|---|---|---|---|---|---|---|
| 8,000 | 1,727 | 0 | 0 | 59.4% | 10.2 | 0% |
| 8,250 | 1,538 | 2 | 1 | 64.4% | **13.9** | 0.02% |
| 8,500 | 1,350 | 5 | 4 | 77.3% | 12.9 | 0.1% |
| 8,750 | 1,161 | 13 | 12 | 80.8% | 13.1 | 0.28% |
| 9,000 | 915 | 37 | 36 | 83.6% | 13.1 | 0.7% |
| 9,250 | 813 | 84 | 83 | 84.8% | 11.3 | 1.49% |
| 9,500 | 824 | 167 | 166 | 86.5% | 10.1 | 3.01% |
| 9,750 | 842 | **277** | **276** | 87.0% | 10.1 | **5.66%** |

Read down the columns and the whole thing is there:

1. **Overshoot.** Photosynthesisers grow to 1,727 and close the canopy — shading passes 59%, which
   is D023's finite sun finally binding rather than being a number in a column.
2. **Crash.** Light-limited, the population falls to 842 over 1,500 s. The die-off is the largest
   the run has seen.
3. **The larder fills.** Detritus peaks at **13.9 J/m³** at t=8,250, exactly as the die-off begins:
   the corpses are the spike.
4. **Invasion.** Absorptive creatures appear at t=8,250 and go 2 → 277 in 1,500 s, **276 of 277 born
   to absorptive parents.**

**The crash precedes the invasion**, which settles the direction: scavengers did not cause the
die-off, they ate it. And they are no longer marginal — 277 of 842 is **33% of everything alive**.

## Why this one and not the others

Four arms ran the same configuration at seeds 1, 2 and 3, and two at clearance 0.5. Only this one
did it. The others sit at 2,600–2,750 alive with absorptive at 0–2 and shading in the 40–60% range:
**they have not closed their canopies yet.** Nothing distinguishes seed 2 except that it got there
first, which is what an arrival-limited process looks like from the outside and is why the arms are
replicated across seeds at all.

That also means this is **one observation**, and the clearance A/B it belongs to is not finished.
`g-c0.5-s2` ended in a D021 runaway at 5,005 alive, so the 0.5 arm is down to a single seed.
Whatever this says about `ClearanceRate` (D041) it says weakly.

## What it confirms that was previously argued

- **§5A.6d's margin, not break-even.** The trade became viable at a *density*, not at a moment:
  nothing established while detritus sat near 10 J/m³ through t=6,500–8,000, and establishment
  followed the spike to 13.9. logbook/0024 recorded absorptive arrivals failing at 8–12 J/m³ and
  called the world arrival-limited; the threshold is somewhere just above that.
- **Energy has a path.** Sun → photosynthesiser → corpse → detritus → consumer, with the audit
  residual at 0.0000% throughout. The loop D036 opened by adding a current is now carrying traffic.
- **Death is what feeds it.** The niche was created by the incumbents dying, and it was created
  *because* they were successful enough to overshoot. Nothing in §5A says that and nothing had to.

## And a second one — which turned out to be a cycle

`sink-mid` — 200 W/m², tissue denser than water, a world built for the muscle question — produced
its own food chain, and it did not look like the first one at all. It ran a **complete
consumer-resource cycle and closed it.**

| t | alive | absorptive | inherited | shading | detritus J/m³ | floor stock J | mean height |
|---|---|---|---|---|---|---|---|
| 17,400 | 1,028 | 1 | 0 | 42.1% | 4.83 | 3,989 | −16.1 m |
| 19,000 | 40 | 5 | 4 | 0.9% | 10.02 | 6,647 | −32.8 m |
| 19,900 | 122 | 93 | 85 | 3.5% | 10.25 | 5,530 | −41.2 m |
| 20,300 | 189 | **173** | **166** | 2.8% | 7.13 | 2,746 | −56.9 m |
| 21,100 | 46 | 45 | 44 | 0.4% | 6.13 | 2,453 | −69.8 m |
| 22,500 | 132 | 1 | 1 | 7.5% | 3.55 | 2,983 | −19.2 m |

Read down the columns and the whole cycle is there:

1. **Photosynthetic boom** (17,000–17,500) — 273 → 1,028 alive, shading 20% → 49%.
2. **Crash** (17,500–19,000) — back to 40 alive. The corpses are the point: detritus goes
   5.62 → 10.02 J/m³ and the floor stock 4,001 → 6,647 J.
3. **Detritivore irruption** (19,000–20,300) — absorptive 5 → 173, of which 166 inherited. They
   consume **59% of the floor stock in 1,300 s** (6,647 → 2,746 J).
4. **Detritivore crash** (20,300–21,100) — the stock they live on is gone; 173 → 45.
5. **Recovery** (21,200–22,500) — absorptive back to 1, photosynthesisers 40 → 132, shading back
   to 7.5%, mean depth back from −69.8 m to −19.2 m.

The consumer peak lags the resource peak by roughly 700 s. That lag, and the overshoot-then-deplete
shape, is what consumer–resource theory says an oscillation should look like, and nothing in §5A
was written to produce it — it falls out of *detritus is a stock, and eating it is a cell type*.

**The deep excursion is part of the cycle, not a separate finding.** Mean height reaches −69.8 m,
far below the ~24 m habitable band at 200 W/m², because a detritivore has no photic zone to be
excluded from: §5A.1's absorptive cell earns the same at any depth. So while the bloom is running,
the population is free to follow the sinking corpses down, and the survivors are back at −19 m once
it ends. This is [D041](../DECISIONS.md#d041)'s deep-water niche opening and closing under its own
dynamics rather than being opened by a parameter.

**The lesson is for the muscle, and it is not a happy one.** `sink-mid` exists to make swimming pay
by taking creatures out of the light. Faced with exactly that, the population did not evolve a
muscle to swim back up: **it stopped needing the light.** Switching trophic strategy was the cheaper
adaptation and evolution took it, which is what evolution does and is the whole premise of §5A. A
sink selects against *being a photosynthesiser at depth*, and swimming is only one of the two ways
out — the other one is cheaper.

⚠ **One cycle is not a cycle.** Two earlier absorptive appearances in this same run (t=7,300 and
t=13,900, 5 and 9 individuals) had **zero inherited** and went nowhere, so establishment needed the
corpse pulse and did not happen without it. Whether the system oscillates or merely did this once
is settled by watching for a second irruption, not by this trace.

## What is not established

Whether the absorptive lineage persists or is a bloom on a transient corpse spike. Detritus is
already falling (13.9 → 10.1) as they eat it, and a scavenger population that outgrows its supply
crashes in turn. **The interesting number is not 277; it is what 277 does when the corpses run out.**
