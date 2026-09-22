# The mouth: round 45's build

*Design spec, 2026-09-22 evening, written by the agent from D106 the hour it was ruled.
Builds on round 44's (`module-gene-spec.md`), whose format 7 already carries the four
attributes at zero. The cap table and the four prices below are the agent's first values,
put here for the owner to amend before the round launches; the rules are the owner's.*

## What it is

A cell is the unit of death, and any cell can hurt, eat, resist or endure according to four
heritable, mutable, priced and per-type-capped attributes. A part whose health reaches zero
leaves the body as a corpse; the killer gains nothing from the kill; the yield is intake
from corpses, which drift and decay into marine snow as they have since round 32. One
organ gives a predator, a grazer and a scavenger.

## The rules

1. **The four attributes**, per node in the genome (format 7, at zero since round 44):
   `Attack` (damage per second to a part of another body in held contact), `Intake`
   (charged units per second taken from a corpse in reach), `Protection` (damage per second
   absorbed before health suffers), `Toughness` (health per unit of volume). Founders draw
   attack and protection at zero, intake at the consumer cell's recorded rate on consumer
   nodes and zero elsewhere, toughness at the default. The mutator moves each as a scalar
   at the scalar mutation rate, clamped to the cell type's cap.
2. **The cap table**, per cell type, the agent's first values, the owner's to amend:

   | cell type | attack max | intake max | protection max | toughness max |
   |---|---|---|---|---|
   | structural | 1 (a claw) | 0 | 1 (armour) | 4 |
   | link | 0.5 (a tail) | 0 | 0.5 | 2 |
   | neural | 0 | 0 | 0 | 1 |
   | photosynthetic (a leaf) | 0 | 0 | 0.25 (a cuticle) | 1 |
   | absorptive | 0 | 0 | 0.25 | 1 |
   | consumer (a mouth) | 1 | 1 | 0.5 | 2 |
   | buoyancy | 0 | 0 | 0 | 1 |

   The units: attack and protection in health per second per square metre of the part's
   area; intake in units per second per square metre; toughness in health per cubic metre
   relative to the default of 1 (so a part's pool is its volume in cubic metres times
   toughness times `HealthPerCubicMetre`). The table lives in the cell-type registry
   (`CellTypeJson`) beside the roles, and a stored genome above a cap is refused, not
   clamped, at load.
3. **Health is state.** Each part carries a pool of volume × toughness ×
   `HealthPerCubicMetre`, full at birth and when a module is added or grown. Damage net
   of protection drains it each metabolic step; it refills at `HealingPerSecond` of the
   pool per second, paid from the reserve at `HealingJoulesPerHealth`, and not at all when
   the reserve is empty. A part at zero health dies at the end of the step.
4. **The kill.** The dead part and everything hanging from it become a corpse at the part's
   place with the part's tissue, the subtree's tissue and the body's reserve × (their
   volume over the body's); the body's tissue and reserve fall by the same; the body
   rebuilds as a growth resize does. The root's death is the body's, `Eaten`. A body under
   the newborn mass floor after a loss dies `Starved` as now. Counters: `partsKilled`,
   `eaten`, `corpsesFromKills`.
5. **Contact for attack** is the farm's creature-creature contact (the overlap census's
   pairs): a part with attack in contact with another body's part damages that part;
   with several in contact, the nearest. `Damage` (the sense) reads the part's health lost
   this step over its pool; `Contact` reads whether the part is in contact.
6. **Intake.** A part with intake within `IntakeReachMetres` of a corpse's centre (the
   corpse's radius from its volume, plus the reach) takes `Intake` × area × dt charged
   units from it into the body's reserve, less the waste share `IntakeWasteFraction` to
   marine snow at the corpse's cell; several parts in reach share by their rates, a corpse
   is finite, and what is left decays as before. Counters: `unitsEaten`, `corpsesEaten`.
   The absorptive guild's feeding on snow is untouched.
7. **The prices**, upkeep in watts per unit of the attribute per unit of the part's area
   (volume for toughness), added to the part's upkeep: `AttackWattsPerUnit`,
   `IntakeWattsPerUnit`, `ProtectionWattsPerUnit`, `ToughnessWattsPerUnit`. Nothing
   beneficial is free. Found with the ledger before the round (below).
8. **What is recorded.** Columns `attack %`, `intake %`, `prot %` (shares of the living
   with the attribute above zero), `killed`, `eaten`, `corpse eat` per window, `heal J`;
   lineage birth rows gain `atk`, `ink`, `prt` flags; `stats.jsonl` the counters; the
   checkpoint each part's health.

## The tunables

`HealthPerCubicMetre`, `HealingPerSecond`, `HealingJoulesPerHealth`, `IntakeReachMetres`,
`IntakeWasteFraction`, the four prices, `AttributeMutationChance`; all in `RunConfig` and its
hash, refusing every earlier config, the header printing `mouth hp=.. heal=../s@..J reach=..
waste=.. prices atk=.. ink=.. prt=.. tgh=..`. A world with every price at zero and no
attribute above zero is the recorded world.

## The ledger screen before the round

`scripts/ledger.ps1` gains `-Attack`, `-Intake`, `-Protection`, `-Toughness` on a genome,
and prints the attribute's upkeep beside the body's income. The screen asks four things of
round 43's median leaf and a one-part consumer of the same volume. A claw at the
structural cap costs less per lifetime than one leaf-sized corpse repays. A cuticle at
the leaf's cap costs more than a tenth of the leaf's income, so leaves do not armour for
free. The default toughness lets an unprotected leaf die to a capped claw within three
metabolic steps, and a capped cuticle holds it for thirty. Intake at the mouth's cap
empties a leaf-sized corpse in under the corpse's decay time. The prices that satisfy all
four go into the launcher and the pre-registration.

## The acceptance

- **The recorded world**: every price at zero and every attribute at zero reproduces round
  44's rows (`regress.py`).
- **Core, alone**: a claw against a leaf in a two-body world kills the leaf's part in the
  predicted steps, and the corpse carries the tissue and the reserve share with both
  identities closed. A mouth in reach empties the corpse at its rate, the waste to snow. A
  cuticle at cap holds the part for the predicted steps. Healing drains the reserve at the
  price and stops when the reserve is empty.
- **The root**: a body whose root dies is `Eaten`, its corpse the whole body.
- **The caps**: a genome above a cap is refused at load; the mutator never exceeds one.
- **The farm**: a 600 s run with an inoculated claw among round 43's plants shows kills,
  corpses, intake and rebuilds, no divergence, the checkpoint identity holding with
  damaged parts.
- **Suites green**; the reflection tests catch the tunables.

## Round 45's predictions, to pre-register on the build

Round 44's world, three seeds, 30,000 s, every founder at zero attack and protection. J1: a
scavenger line founds (a lineage with intake above zero and `corpse eat` above zero for 20
samples) in 2 of 3. J2: a killer line founds (attack above zero, `killed` above zero for 20
samples) in 1 of 3 or more. J3: an indeterminate plant survives grazing: among plants
killed at a non-root part, the share alive 1,000 s later is above 0.5 in 2 of 3. J4:
protection appears in a lineage after attack does, never before, in 3 of 3. J5: the crowd
stays above 300 at 30,000 s in 2 of 3 (the massacre reading if not). J6: both books closed,
`diverged` 0. J7: a body's reserve is where the yield comes from: `unitsEaten` per corpse
tracks the corpse's reserve share and not its tissue.
