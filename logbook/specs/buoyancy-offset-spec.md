# The buoyancy offset: the build spec

*Fable, 2026-09-23, for `fable-propose-round-46.md`'s question 1 (the one-part leaf's way to
lie flat under D110), written before the ruling so the build is a day. What a duckweed frond
has: gas on one face, so that its weight and its buoyancy act at different points and it
floats face up however it was dropped in. A genome field, so a format bump (7 to 8).*

## 1. The quantity

Every part displaces its volume of water. Today the solver books one signed term per link
(`Fluid.Apply`: `netDensity = excess · (1 − Lift)`, a force at the link's origin, no torque),
because a uniform part has its mass centre and its volume centre at the same point. The
offset separates them: a part's **buoyancy centre** sits at `offset · h_thin` along its own
thinnest axis from its origin, where `offset` is the node's heritable value in `[−1, 1]` and
`h_thin` the part's smallest half-extent. The forces do not change; a torque appears:

    torque_i = r_i × B_i,   r_i = R_i · (offset · h_thin · axis_thin),   B_i = ρ_water · V_i · g · up

with `R_i` the link's rotation, `ρ_water` the fluid density the config carries, `V_i` the
link's volume. The weight stays at the origin, so a part whose buoyant face is down turns over
and comes to rest with it up, and the net force on the body is what it was to the bit. The
torque uses the full displaced weight and not the 2% excess, which is the physics: a leaf's
righting moment is the whole of the water it displaces acting off its mass centre. For a
half-metre leaf (0.16 m³, `h_thin` 4.5 cm) at `offset` 0.5 that is about 35 N·m, which rights
it in well under a second against the panels' damping; the divergence check reads the screen.
A sphere part has no thinnest axis and the offset does nothing on it (`e = 1` under D110 for
the same reason). Above the waterline the `Restore` term clamps the force; the torque follows
the clamped share of the buoyancy (zero when the term is zeroed), so a body pressed to the
surface is not spun by water it is not in.

## 2. Where it lives

- **`MorphNode.BuoyancyOffset`**, float in `[−1, 1]`, default 0; founders draw 0 (a trait
  selection must find, as `Power` and the attributes are drawn); `Mutator` perturbs it as a
  signed value clamped to the range at the rate of `Lift`; `SpeciesDistance` counts it;
  `GenomeJson` writes `buoyancyOffset` and reads it, `FormatVersion` 8, every stored genome
  refused (the base-round build re-extracts the inocula once).
- **`PhenotypePart.BuoyancyOffset`** carried from the node through development, mirrored
  parts unchanged (the thin axis mirrors with the part).
- **`RunConfig.BuoyancyOffsetWattsPerCubicMetre`**, the price, `EVOSIM_BUOYANCY_OFFSET_COST`,
  default 0 = the field is refused at validation above 0, as `Lift` is on a cell that does
  not hold gas, so that no world carries a trait nothing charges for. The header prints
  `buoyancy offset 0.02 W/m3` or `off`. With the price on, `Metabolism` charges
  `price · |offset| · Volume` in the part's upkeep, worn with the rest. The number is the
  ledger's: the screen asks that a leaf paying for `offset` 0.5 keeps most of the doubled
  income D110 gives it flat, and the proposal's recommendation is 0.02 W per m³ (a tenth of
  standing cost at full offset), to be screened before the pre-registration.
- **`Creature.BuoyancyOffset[i]`** and the thin axis index per link, filled by the farm's body
  builder from the phenotype; **`Fluid.Apply`** adds the torque under the price above 0, and
  under 0 the branch is not entered.
- The GPU kernel carries it as one cross product a link; the Unity farm does not bind it.

## 3. The instrument

`stats.jsonl` gains `meanBuoyancyOffset` over living photosynthetic parts (area-weighted, the
absolute value) and the table prints `float off`; D110's `expo` is the readout it should move.

## 4. Tests

1. A one-link box with `offset` 0.5 and its thin axis horizontal, alone in still water, turns
   until the thin axis is within 5° of vertical with the offset face up, within 10 s, and the
   net force on it at every step equals the offset-free body's to the bit.
2. The same at `offset` −0.5 comes to rest with the other face up.
3. A sphere with any offset reads zero torque.
4. `offset` 0 is the recorded solver bit for bit (the crowd fixture's 3,000 s regress
   identical, lineage byte-equal), with the price at 0 and the field at 0 in every genome.
5. `ParallelIdentityTests`' word holds; the digest agrees at 1 and 16 threads with the
   torque on.
6. A 3,000 s dt 0.02 screen of round 45's launcher with D110 on, the price on and every
   founder at `offset` 0: both books close, nothing diverges, `float off` starts at 0 and the
   entry says whether it moves.
