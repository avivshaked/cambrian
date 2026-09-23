# The support cost: the build spec

*Fable, 2026-09-23, from D107 (the price is the next proposal) and the screen by arithmetic
(`logbook/specs/r45-read/support-cost-screen.txt`), for `fable-propose-round-46.md`'s rule 3.
Written before the owner's ruling on the form and the price so that the build is a day and
not a discussion; the two numbers below are the recommendation and the tunable takes any.
Off at 0, the recorded world.*

## 1. The quantity

Each part pays an upkeep for the load it puts on the chain to the root:

    support_i = price · LitArea_i · d_i²     [W]

where `LitArea_i` is the part's orientation-averaged lit area (a quarter of its surface, the
same number the light bills on; with D110 on, the exposure factor does **not** enter here,
because the load is the sheet's size and not its angle to the sky) and `d_i` is the distance
of the part's centre from the root's origin in the developed body's own frame
(`PhenotypePart.Position`, the root at the origin), scaled with growth as every length is.
The root pays nothing (`d = 0`). The square form is the recommendation: at 0.1 W per m² per
m² it puts round 44's giant at −179 W where it earned 618 and leaves a 1.5 m kelp 86% and a
half-metre two-part body 86% of their income, and a copy's own support equals its own income
at `sqrt(10 / price)` metres, ten metres at that price; the linear form needs ten times
the price per metre to sink the giant and takes 21% from the kelp. A tunable exponent is not
built: the form is the owner's ruling and a second tunable for it is a knob nobody asked for.

## 2. Where it lives

- **`RunConfig.SupportWattsPerSquareMetrePerSquareMetre`**, float, default 0;
  `EVOSIM_SUPPORT` in `EnvBinding`; the header token `support 0.1 W/m2/m2` or `support
  off`, beside the four attribute prices. `RunConfigJson` writes and reads it, so every
  earlier config is refused (the base-round build re-records the fixtures once for all its
  tunables). `RunConfigTests` and `RunConfigJsonTests` catch an omission.
- **`Metabolism`**, in the per-part loop where the attribute prices are charged: with the
  price above 0, `upkeep += price · part.LitArea · part.DistanceFromRoot² · seconds`, worn
  by the senescence factor with the rest of the upkeep, as `AttributeWatts` is. With the
  price at 0 the term is not computed and no float is added (the `priced` pattern).
- **`PhenotypePart.DistanceFromRoot`**, set by `Developer` when the body is complete and
  scaled by `Phenotype.Scaled` with the half-extents, so the distance the part pays on is
  the grown body's. A mirrored part reads the same distance as its twin.
- **`Metabolism.StandingWatts`** carries the term, so the ledger and the screen read it.
- **The module rule**: `WorldModules`' price for adding a copy stays as it is (the tissue
  price); the support cost is an upkeep the copy then pays for life, which is what bounds
  the fan. No change there beyond the header.

## 3. The ledger and the screen

`scripts/ledger.ps1` (`src/Evosim.Ledger`) reads the price from the config it is given and
prints `support W` beside `standing W` in its cost line, so the screen of the leaf, the
two-part body, the kelp and the giant (`logbook/specs/r45-read/support-cost-screen.py`) is
re-run through the ledger on the build and must reproduce the arithmetic's numbers to the
watt: giant −179 W net at 0.1, kelp 12.19 W, two-part 6.15 W, leaf 3.08 W. That is the
build's acceptance beside the tests.

## 4. The instrument

- `stats.jsonl` gains `supportWatts`, the living bodies' summed support cost per second,
  and `meanReach`, the area-weighted mean of `d_i` over living parts; the table prints
  `support W` and `reach m`. `reach m` is the readout of the bound the price sets, against
  round 44's 10.5 m giant and round 45's crowd of half-metre leaves.
- `absorptive.jsonl` gains `support` per body-sample beside `upkeep`.

## 5. Tests

1. A one-part body pays 0 at any price.
2. A two-part body with the second part's centre 0.5 m from the root at 0.1 W per m² per
   m² pays `0.1 · LitArea · 0.25` W, to 1e-6, and a grown copy at scale 2 pays four times
   the distance term on four times the area (sixteen times).
3. The giant of `scratch/logs/giant-7597.txt` (its genome under `inocula/` if it is not
   there yet: extract it) reads −179 W net through `StandingWatts` at the screen's income
   assumptions, within a watt.
4. With the price at 0 the crowd fixture's 3,000 s regress is identical in every field and
   the lineage byte-equal.
5. With the price at 0.1 a 3,000 s dt 0.02 screen of round 45's launcher closes both books
   and prints `support W` and `reach m`; the entry reports whether the fan ever forms.

## 6. What it does not do

It prices a leaf's reach and not a joint's torque, a stalk's thickness or a body's mass: a
sheet of fixed thickness has mass in proportion to area, so mass times distance is the same
shape and is not a second term. It does not bound the size of a single part, which D107
left to the economy and which a single part cannot escape by copying itself.
