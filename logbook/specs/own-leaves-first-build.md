# D207's build: own leaves first, and D206's share made smooth

Built on branch `r62-d207` (from `r62-card` at `6590b026`) on 2026-10-19, after the owner's ruling recorded as D207.
D206's build is `plants-keep-matter-build.md`; this amends its form and leaves its store, its corpses and its books
as they were.

## The switch

`RunConfig.PlantsKeepOwnFirst` (`EVOSIM_PLANTS_KEEP_OWN_FIRST`, header `own leaves first`), off by default. It needs
`PlantsKeepMatter`, and a world given one without the other is refused. Off, every line D206 runs is unchanged.
Like any tunable it changes every config hash, so a run written before it cannot be resumed on this build. A fresh
launch with the switch off does D206's arithmetic under a new hash.

## What it does, on

Every burn of a body with photosynthetic tissue goes through `World.ReturnSpent`, as under D206: the metabolic
step's, a grip's, a birth's overhead and healing.

1. The body's own leaves come first. It holds its spent units up to one step's light capacity in units less what it
   already holds. The capacity is the last metabolic step's `EnergyLedger.LightCapacity` over ρ: the joules its
   leaves could fix at that light before the water's uptake bound them. The metabolic step's own burn reads the
   ledger it has just made. The side burns read `Organism.LastLedger`, which the step now keeps for photosynthetic
   bodies as well as absorptive ones. It was already written to every checkpoint for every body, so no field or
   state version changes, and a resume reads the same capacity.
2. The remainder is split smoothly, by `Metabolism.SmoothHeldShare`. A body with no eating tissue holds it all. A body with
   eating tissue holds its photosynthetic upkeep over its photosynthetic and eating upkeep together, and releases
   the rest where it is. Eating is a consumer or absorptive cell's whole upkeep. A mouth on any other cell would add
   its intake price, but the standard table gives a mouth to the consumer alone. Structure, muscle and brain count
   on neither side. D206's share put them all in the denominator.

A pure plant's share is 1, so it holds every burn in one addition, as under D206, and its arithmetic is D206's to
the bit. A body that has not yet been through a metabolic step has no capacity, and its whole burn goes to the
share.

`HeldShare` is unchanged in value. Its per-part pricing moved into `HeldPartWatts`, which both shares use, with the
same expressions in the same order.

## Tests

`PlantsKeepOwnFirstTests` (Core), with the numbers they printed:

- A leaf, a stalk and a stomach hold 0.494 of the remainder under the smooth share, against D206's 0.456, because the
  stalk counts on neither side. A leaf and a stomach alone give D206's share to the bit.
- A tiny mouth moves the share a little. Give a leaf and its stalk a consumer mouth of half-size 0.05, 0.08, 0.12
  and 0.2 m. It then holds 0.969, 0.904, 0.761 and 0.439 under D207. D206 gives 0.831, 0.783, 0.674 and 0.408. Development drops a
  part below its minimum, so a smaller mouth is no mouth.
- Off by default, and refused without D206.
- A pure plant is D206's to the bit over 400 steps: the held, recharged and burnt totals, the standing matter and
  the living.
- In the light, a leaf with a stomach keeps all of its burn, where D206 kept 0.494, because its leaves can fix more
  than it burns. In the dark it keeps 0.4941, the smooth share, since the leaves can take nothing.
- Every burn reads the last step's capacity. A leaf with a mouth and no absorptive cell keeps its ledger only with
  the switch on. A burn under the room is held whole. A burn over it holds the room, then the share of the rest. A
  full store holds the share alone.
- A checkpoint carries the capacity, and the resume is the same world, byte for byte, 150 steps on.

The config fixtures `r42-config.json` and `r57-s2-config.json` carry the new field and their new
hashes (`45482bac06dd7701`, `81c4166044db692f`), and so do the farm tests that name them. The Dynamics crowd fixture
is re-recorded by `logbook/specs/d207-build/fixtures.ps1` as `d207fix-s4`.

## Cost

A few comparisons and one multiply a burn, for photosynthetic bodies on the host. `SmoothHeldShare` walks the body's
parts as `HeldShare` does. Nothing moves on the card: D206 lives in Core's host-side metabolism, and the kernel has
no store.

## On the card, on round 62's build

On `r62-build` (`915a63aa`), round 62's square with own leaves first on and the snow's transport on the card
(`-Transport`) was founded three ways on 2026-10-19. A group of two on ILGPU's CPU accelerator, seeds 6201 and 6202,
ran 30 s with nothing diverged, and its books held to 4e-12 J and 3e-8 units. Seed 6201 was then founded on the card
for 300 s at group 32 and at group 64 (`d207-sq-g32`, `d207-sq-g64`, config `ae488ff653e59e86`, the CPU preflight's
hash). The two match on all 30 stats samples, all 301 digest rows and the lineage, byte for byte, with nothing
diverged. Each ran 6.7 to 6.8 times real time at 578 bodies, and the books held to 2e-6 J and 2e-7 units.
