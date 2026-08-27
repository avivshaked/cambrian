# 0024 — The larder filled, and nobody came

**2026-08-27**  ·  Milestone 4

[Entry 0023](0023-nothing-died-of-old-age.md) gave the world mortality. This entry is about what
mortality produced, which is food — and about a recommendation I made twice and had to withdraw
twice, both times because a measurement said something other than what the reasoning did.

## The wrong next step, caught by reading the design

Asked what to do next, I recommended building MAP-Elites. DESIGN §8 says, in a callout at the top
of the section:

> ⚠ **Demoted by §5A.** MAP-Elites was the *selector*… Under endogenous selection its
> innovation-protection role passes to **ecological niches — spatial and trophic** — maintained by
> the depth/light gradient and cell-type mutation.

So the question was never "build the archive". It was whether those two niches work, which is a
claim about the world and therefore measurable. CLAUDE.md says this in as many words — *several
obvious-seeming ideas were already tested against the literature and rejected* — and the cost of
not reading first would have been an archive over a world with one occupied cell.

## Senescence filled the water with corpses

Nutrient density where creatures actually live, before and after [0023](0023-nothing-died-of-old-age.md):

| | still + immortal | current + mixing | + senescence |
|---|---|---|---|
| density at the population's depth | 0 | 0.18 J/m³ | **2.1 – 4.3 J/m³** |

Against an absorptive break-even of `upkeep / clearance = 4 / 0.5 = 8 J/m³`. A gap that was
forty-fourfold in [0022](0022-the-conveyor-belt-and-the-thin-soup.md) closed to two- or fourfold,
without anybody touching a cell parameter. This is the mechanism the human predicted before any of
it was built — *"the world should have more and more material as entities die… then animals that
consume the dead matter should start to be more common"* — and the first half of it now works.

## The ordering problem

The second half does not, and the reason is a timing collision between two mechanisms that were
each correct on their own.

| t | event | density |
|---|---|---|
| 0 | founders are **24.7%** absorptive | — |
| ~2,400 | last absorptive creature starves | 0.33 J/m³ |
| ~2,700 | **the floor fires for the last time** — the world is self-sustaining | 0.34 J/m³ |
| 9,500 | break-even crossed | **8.21 J/m³** |
| 15,000 | | **12.27 J/m³** |

**The floor is the only fast source of absorptive creatures, and D021 makes it fire only to hold
the population up — so it goes silent exactly when the world succeeds, seven thousand seconds
before the larder is worth entering.** The world switches off its supply of consumers at the moment
it starts producing food for them.

The other route is cell-type mutation, and it is slow. Measured rather than estimated, in
`TrophicInvasionTests`:

| source | rate |
|---|---|
| founder draw | **24.7%** of founders carry an absorptive part |
| mutation from a photosynthesiser | **one per 5,128 births** |

A factor of 1,265, and runs produce one to nine thousand births.

## They arrived, they ate, and there were never two

A 15,000 s run at T=3,000 caught the whole thing happening:

```
t= 9500   alive 1246  absorptive 1   8.21 J/m3   food 0%
t=12500   alive 1356  absorptive 1  10.19 J/m3   food 0%
t=12750   alive 1442  absorptive 1  10.48 J/m3   food 0.03%
t=13000   alive 1507  absorptive 1  10.49 J/m3   food 0.06%
t=13250   alive 1573  absorptive 1  10.55 J/m3   food 0.09%
t=13500   alive 1661  absorptive 1  10.62 J/m3   food 0.12%
t=13750   alive 1720  absorptive 0  11.10 J/m3   food 0.09%
```

**The first non-zero food income in this project's history**, rising monotonically over four
consecutive samples. Two separate arrivals survived a thousand seconds and more. The niche opened
and was entered.

And the count never once reached two. That is a different failure from starving, and a much
narrower one.

Incidentally, the same run: **gen min 27, gen max 49**, 8,699 births, the floor silent since
t≈2,700. Whatever else is wrong, that world is alive.

## The recommendation that the measurement killed

From *never two*, I reasoned to a margin problem. A creature exactly at break-even survives forever
and never breeds, because §5A.6 pays for offspring out of surplus — so the world producing 10 J/m³
against a break-even of 8 gives a 25% margin, and I proposed raising `ClearanceRate` to widen it.
Both `clearanceRate` and `upkeepWattsPerCubicMetre` are ⚠ unmeasured in §5A.10, so it was a
legitimate knob to set rather than a fudge.

Then I measured it, in `TrophicMarginTests`, before changing anything:

| trade | conditions | net | earns its own tissue back in |
|---|---|---|---|
| photosynthetic | −2 m, full sun | 1.063 W | **470 s** |
| absorptive | −2 m, 10 J/m³ | 1.000 W | **500 s** |

**Parity — 1.06×.** At the density this world now produces, eating the dead is as good a living as
photosynthesis in the light. There is no margin problem, the knob did not need setting, and the
diagnosis I had reasoned my way to was wrong.

Break-even was still the wrong number to have been quoting, and that part stands: 8 J/m³ is where
the trade stops losing money, not where it starts being worth doing. But the world had already
walked past it.

## So what is it?

Two candidates remain and they are cleanly separable:

- **Arrival-limited.** The niche opened at t≈9,500 and the run ended at 15,019 — about one
  arrival's worth of opportunity at one per 5,128 births. Nothing established because nothing had
  time to.
- **Establishment-limited.** Arrivals cannot found a lineage for a reason not yet identified.

`EVOSIM_CELLTYPE_MUTATION` now exists to tell them apart: raise the arrival rate twentyfold and the
first hypothesis predicts a trophic level, the second predicts a parade of solitary creatures that
never become two. That run is going.

## The pattern

0023 was a defect written down in the right words under the wrong heading.

This one is simpler and more embarrassing: **twice in one session I proposed a change and the
measurement I ran to justify it said no.** MAP-Elites — the design already ruled it out and I had
not read the section. Clearance — parity at 1.06×, against a margin story I found completely
convincing until it produced a number.

Both were caught, and both were caught the same way, which is the part worth keeping: *the
measurement came before the change, not after it.* The cost of being wrong here was two test files
and no edits to the simulation. There is no version of this session in which I was right about
either, only versions in which I found out earlier or later.
