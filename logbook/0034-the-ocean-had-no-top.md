# 0034 — The ocean had no top

**2026-08-28.** [D049](../DECISIONS.md#d049)'s buoyancy cell was measured properly for the first
time and turned out to be a rocket. Buoyant creatures were 100 to 178 metres above the rest of
the population in a world whose habitable band is 23.7 m deep — and were *worse off for it*,
because above the waterline this world has nothing in it and does not end.

## The measurement gap

The first probe reported one mean depth for the whole population. It rose from −8.7 m to +12.8 m
while the buoyant share fell from 25% to 4%, which reads naturally as *"the organ is not
spreading"* and says nothing whatever about the 4%. A share cannot distinguish a trait that does
nothing from a trait that works and is priced out. So `EvolutionRun` gained `flt m` — mean depth
among creatures holding lift, against `depth m` for everyone.

The column changed the reading completely.

## What it showed

`float-m` — 200 W/m², sink 0.02 kg/m³, matter 0.5/J from 1.0/m³, founder float 0.5, seed 1:

| t | alive | float | **flt m** | depth m |
|---|---|---|---|---|
| 100 | 40 | 11 | **−4.8** | −8.6 |
| 300 | 40 | 8 | **17.8** | −4.0 |
| 500 | 100 | 13 | **52.4** | 11.5 |
| 800 | 364 | 17 | **96.8** | 16.1 |
| 1600 | 1632 | 32 | **117.5** | 12.2 |

The organ was never failing to buy position. It was buying a hundred metres of it, steadily, at
about 0.14 m/s, and never stopping.

`float-heavy` ran the same world with the sink five times stronger — the obvious control, on the
theory that lift was losing because *up* was already free by demography ([D036](../DECISIONS.md#d036)):

| t | alive | float | **flt m** | depth m | mat deep | mat blk |
|---|---|---|---|---|---|---|
| 600 | 184 | 24 | **55.2** | 8.5 | 1.421 | 1,418 |
| 1200 | 735 | 29 | **104.7** | −6.2 | 1.223 | 76,599 |
| 1800 | 1315 | 30 | **155.8** | −22.8 | 0.704 | 51,575 |

A five-fold stronger sink **widened** the gap from ~103 m to ~178 m instead of closing it, and
pushed the non-buoyant population down to −22.8 m — the edge of the lit zone — while blocked
conceptions peaked at 77,499 and then *fell*, which is a population failing to breed rather than
one under control. The buoyant fraction seceded from the world and the rest of it drowned.

## Two faults, and the smaller one is the interesting one

**The world does not end at the top.** Three clamps, none of them wrong on its own:

```csharp
LightModel.IrradianceAt   → if (heightY >= 0f) return SurfaceIrradiance;   // constant above 0
NutrientField.LayerOf     → heightY >= 0f ? 0 : (int)(-heightY / LayerMetres);  // layer 0 above 0
FluidEnvironment          → no bound on y at all
```

Light stops varying, matter stops varying, and the physics keeps integrating. So above the
waterline there is an unbounded ray on which every point is identical to floating at y = 0,
and a creature that climbs it pays lift upkeep for the entire journey and receives nothing. At
+155.8 m a bladder is not buying a worse position than the surface; it is buying *the same*
position at a standing cost. It is a pure tax with a spectacular readout.

DESIGN §5A.1 predicted half of this before it was built — *"free lift runs away to whatever
ceiling exists and returns every creature to the surface"* — and priced lift accordingly. What
it did not anticipate is that there is no ceiling, so the runaway does not return anyone to the
surface. It just leaves.

**The two sides of the trade were on unrelated scales.** Lift was in absolute kg/m³; the weight
it exists to cancel is `TissueExcessDensity`, which the same runs set to 0.02 kg/m³:

| | value | multiple of the sink |
|---|---|---|
| `TissueExcessDensity` | 0.02 kg/m³ | 1× |
| `MinBuoyancyLift` | 0.5 | **25×** |
| `MaxBuoyancyLift` | 5 | **250×** |
| `MaxLiftKgPerCubicMetre` | 50 | **2,500×** |

So the *weakest bladder a creature could be born with* was already twenty-five times
overpowered, no genome value meant "a little lift", and neutral buoyancy — which is what a swim
bladder is actually for — was not a reachable state at all. The organ had no setting at which it
could hold station, which is the one thing gas vesicles have been doing for three billion years.

## What changed — [D050](../DECISIONS.md#d050)

Lift is now denominated in **multiples of the sink it opposes**, so 1 is neutral and 2 rises as
fast as a bare body falls. `FluidEnvironment` computes `excessDensity × (1 − lift)`, the founder
range became 0.25–2, and the bound became 3. This survives retuning `TissueExcessDensity`, which
§5.2 flags as unmeasured and expects to change — the failure mode absolute units have is silent.

The ocean got a top: a part at or above y = 0 gets no *upward* net force, because the water it
displaces has run out. Sinking across the line is untouched. `GenomeJson.FormatVersion` went 2→3
so a stored genome refuses to load rather than being reinterpreted under new units, and
`MaxLiftSinkMultiples` joined `BuoyancyCell.HashContribution` — it is a `const` rather than a
`[Tunable]`, so `RunConfigTests` cannot see it, but the mutator clamps to it and it therefore
decides which genomes exist. That is [D046](../DECISIONS.md#d046)'s bug exactly.

## What the fix measured

Same worlds, same seed, new units. The runaway is gone and the organ does what an organ should:

| | sink 0.02 | sink 0.1 |
|---|---|---|
| max `flt m` | −5.3 m | −3.7 m |
| gap over the population, late | +27 m | +17 m |
| buoyant, inherited | 18 of 19 | 52 of 56 |
| evolved lift | 1.79 | 1.63 |
| floor spawns after t=400 | 0 | 0 |

Selection moved lift from a founder mean of 1.15 to **1.6–1.8** — just above neutral, hold
station and drift up, which is what a swim bladder is for and which was not a reachable value
before. `d050-heavy` crashed from 102 to 63 and recovered to 940 with **no floor spawns at all**,
buoyancy peaking at 47% of the population through the bottleneck. It is inherited, it is
selected, and it is the difference between the light and the sea bed.

**The surface clamp never fired.** `flt m` never rose above −3.7 m in either arm, so the entire
improvement came from the rescaling. The clamp is still correct to have — a brain-driven lift
channel is the next thing that could push a creature up — but it did not cause this result and
must not be credited with it.

## What it does not establish, and the part that got worse

**Buoyancy stayed a 6% minority, and the reason is not that it loses.** By t=1300 in both arms
`absorpt` is 0 and `food %` is 0: the absorptive cells went extinct and detritus piled up
**unconsumed** to 54,011 J and 69,406 J. So the 54–58% of the population sitting on `% on floor`
is not a detritivore niche. It is producers that sank into the dark and stayed there — the world
is a conveyor, born shallow, sinking, dying on the bottom, and the detritus curve rising
monotonically is the proof that nothing eats what lands.

The two niches are each sterile on their own axis. Final `d050-heavy`: matter at the surface
0.02, matter deep 1.095 — a **55× gradient** — with 8,777 conceptions blocked for want of
matter. The buoyant hold the light and starve for matter; the sinkers hold the matter and starve
for light. Passive lift lets a lineage *choose* a depth. It cannot let a creature be in two
places, and this world now requires exactly that.

That is the measured case for D049's second step rather than an argument against its first.

⚠ **These arms ran `mixing 0`, and D048's headline world ran `mixing 2`.** `run-arm.ps1` does not
set it, so it defaulted away from the world the matter economy was measured in — the same trap
as reading a share instead of an inheritance. With mixing off, matter never returns to the
surface at all, so the light niche is sterile *by construction* and the 55× gradient is partly
an artefact of the setting. `d050-mix-heavy` and `d050-mix-slow` re-run both arms with
`mixing 2`, `current 0.05` and `senescence 3000`; until they land, the trophic half of the
paragraph above is provisional and the buoyancy half is not — the organ's position-holding does
not depend on where the matter is.

The units error **was not caught by a test and could not have been**. The netting that gives
lift its meaning lives in `FluidEnvironment`, which is Unity-side and outside the one-second
suite; 330 tests passed before the change and 330 passed after it.
`AFounderBladderStraddlesNeutralBuoyancyRatherThanBeingARocket` pins the invariant going forward
but would have passed under the old values too, since 0.5 and 5 straddle 1 whatever 1 happens to
mean. This was found by looking at a number, and the number only existed because the previous run
had been uninterpretable.

The units error **was not caught by a test and could not have been**. The netting that gives
lift its meaning lives in `FluidEnvironment`, which is Unity-side and outside the one-second
suite; 330 tests passed before the change and 330 passed after it. `AFounderBladderStraddles­
NeutralBuoyancyRatherThanBeingARocket` pins the invariant going forward but would have passed
under the old values too, since 0.5 and 5 straddle 1 whatever 1 happens to mean. This was found
by looking at a number, and the number only existed because the previous run had been
uninterpretable.

Both clearance arms starved while five arms and a test build shared the machine — `g-c0.5-s3`
stopped writing for eleven minutes and resumed within seconds of the others being killed. Five
concurrent arms is the ceiling only when nothing else is compiling. They were stopped holding a
stable inherited absorptive lineage — 5 of 5 and 5 of 6, `gen min` 41–44, floor silent — which is
the food chain of [0028](0028-the-canopy-closed-and-the-scavengers-came.md) surviving in a world
that is verifiably alive, and is the thing the buoyancy arms above conspicuously lost.
