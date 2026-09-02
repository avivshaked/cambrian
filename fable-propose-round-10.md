# Proposal: round 10 — the Archean package (size-dependent buoyancy)

*2026-09-02, second draft — the first draft's L1/L2/L3 knob levers are superseded by the
owner's design, which this file makes concrete for ruling. Absorbed into DECISIONS.md on
decision, then deleted.*

## The diagnosis (unchanged from draft 1)

Rounds 8–9: zero passes. Three worlds died with full larders when a matter drought paused
births and the standing population — uniformly denser than water — sank out of the photic
band and starved in the dark ([0044](logbook/0044-three-medicines.md) results). Float
tissue exists and works, but selection prices it out to ~1% between crises because a
floatless producer breeds cheaper before it sinks out. Chain arrival is drought-gated:
absorptive mutants appear and cannot breed through the drought that surrounds them.

## The owner's design (2026-09-02), in three parts

1. **Founders spawn anywhere in the water column** — any (x, y, z) above the floor, not
   just the top 20 m (`FounderDepthSpread` today). The world stops privileging the
   surface; deep producer founders die of darkness by selection, not by rule. (x and z
   remain cosmetic outside patchy worlds — the layer-as-stirred-tank limitation stands and
   is not addressed here.)
2. **Neutral buoyancy at founder scale, for every guild; sinking comes with growth.** A
   small body holds its depth for free. Adding cells and matter adds weight — so growth
   is what costs you your depth, and holding a large body in the light requires float
   tissue (or, on the movement frontier, swimming). This applies to absorbers too, at the
   owner's explicit direction: under a plants-only rule, sinking would remain a free
   elevator to the floor larder for exactly the guild that wants to descend. Universal,
   the rule makes descent a priced choice — an absorber reaches the pantry by *growing*,
   paying the upkeep of the size that sinks it.
3. **Children born beside their parents** — already the code's behaviour
   (offspring inherit the parent's height, World.cs `SpawnOffspring`); recorded here so
   the package is complete on paper.

What this buys, mechanically: the drowning death becomes survivable (the small fraction
rides out a drought holding the light and refounds); float tissue gains a real
evolutionary edge (it is the price of *size*, not an insurance policy against a crisis
selection has never met); size becomes trophic strategy (small floaters up where the
light is, large sinkers down where the detritus is — plankton versus benthos as an
emergent, falsifiable prediction); and deep-scattered absorptive founders can hold
position near the larder, giving establishment an honest shot.

## The proposed contract (for ratification — the formula IS the world rule)

Effective excess density scales with body volume:

    rho_eff(V) = TissueExcessDensity * max(0, 1 - (V0 / V)^(2/3))

- `V0` — new tunable `NeutralBodyVolume`: the volume at or below which a body is
  neutrally buoyant. Proposed default: the volume of a one-cell founder body, so a
  founder floats in place by construction and every added cell begins to cost.
- At `V = V0`: neutral. At `V = 8·V0`: 75% of today's excess density. As `V → ∞` the
  rule converges to today's constant — the current worlds are the large-body limit, so
  the change is backward-compatible in spirit as well as in code.
- The ⅔ exponent is a modeling choice shaped by Stokes' law (sinking speed rises with
  size; small particles effectively do not sink), **marked as inference, not citation**:
  Stokes strictly gives velocity ∝ r²·Δρ, and folding the size dependence into Δρ with
  this exponent is our simplification, chosen so one existing mechanism (excess density ×
  drag) carries the whole rule.
- Float (`MorphNode.Lift`, D049) is unchanged: lift still nets against weight and is
  still billed per unit held. It simply has something worth buying now.
- Config: new knob default-off (`NeutralBodyVolume` = 0 reproduces today's behaviour
  bit-for-bit; suite-enforced like every prior world knob), env var, header token, hash
  and JSON round-trip via the two reflection tests.

## Round-10 design

One treatment arm set: the package (founders full-column + size-dependent buoyancy),
five seeds, mutation 0.005 (ratified discovery regime), no refuge (the pantry meter goes
back on the shelf until a chain exists to need it), budget 30,000 s, wall 600 min,
scored under the unchanged D063 rule. Controls remain the round-6 arms by bit-identity.
Pre-registration entry before launch, as always.

Named risks, two-sided, before any data: (a) a world of tiny neutral floaters may bloom
permanently — runaway to the ceiling, everything censored; canopy shading (logbook/0028)
should self-limit it, but that is a belief, not a measurement, and if both this and the
round-8 B arms censor by ceiling the next conversation is about the light economy.
(b) If chains still fail to arrive with droughts survivable and the larder reachable by
growth, arrival was gated by something still unmeasured, and the diagnosis reopens.

## What I need from you

1. Ratify or amend the contract above — especially the formula shape and the `V0`
   default (one-cell founder volume).
2. Confirm founders-anywhere applies to the full column above the floor (it interacts
   with the buoyancy rule as described).
3. Green-light the round-10 design (single package arm × 5 seeds, refuge shelved,
   mutation 0.005).
