# Contact eating: the build spec for D219 and D220

*Draft of 2026-10-23, by the speed agent, for the owner's reading before the screen. D219 rules that a mouth eats
only what it touches. D220 amends it: a bite eats as it wounds, and the dead stay in the water as edible remains for
a short window. This file says how, step by step. It gives the matter and energy route of every step, and how the
remains stay in the solver on the card and the CPU. It also covers the checkpoint, the dials and the size of the
build. Where the rulings leave a choice, the default recommended is given and marked as a choice. Code names are
those on `mouth-point` at `3180cb57`.*

## What exists today, in brief

A mouth is any part whose cell carries intake (`PhenotypePart.Intake > 0`). In round 62 that is the consumer cell,
which also carries attack. The farm hands Core one contact per pair of overlapping bodies, naming the nearest part on
each side (`Metabolise.HandOverContacts`). Core's mouth pass (`WorldMouth.ApplyMouth`) runs four steps. First, each
named part's attack, net of the other part's armour, lowers that part's health; no joule moves. Second, every mouth
within reach of a corpse takes charged units from it, a fifth of which goes to the Nutrients field as waste. Third,
lost health is healed out of the reserve, and the joules are burnt. Fourth, a part at zero health is shed with the
limbs that hang from it, and its tissue and reserve share become a corpse; a body that loses its root dies, and all
of it becomes a corpse. A corpse pays its joules into Nutrients as it sinks, and spoils.

## The dials

- `RunConfig.ContactEating` (`EVOSIM_CONTACT_EATING`), off by default. Off, nothing below runs and the world is
  round 62's bit for bit. On, the corpse intake is never called, no corpse is founded, and bites feed.
- `RunConfig.RemainsWindowSeconds` (`EVOSIM_REMAINS_WINDOW`), 60 by default, read only with the first dial on: how
  long a dead body or a cut-off limb stays in the water.

`IntakeScale` is unchanged and is the yield dial D219's risk names. `IntakeReachMetres` and `IntakeReachFromMouth`
are read by nothing with the first dial on, and their values stay in the config.

## 1. A bite feeds as it wounds

The damage step is today's. A named part with attack lowers the other named part's health by its blow, net of the
part's armour, over the part's health pool.

Under the dial, health is also the share of the part's tissue still uneaten. Call the part's tissue T: its volume at
its cell type's tissue price, as `Metabolism.TissueJoules` counts it, with the free first link's share at zero. A
wound that takes a share `s` of health takes `s × T` joules of tissue out of the body at once. The bitten body's
tissue account falls by that much, and the same amount goes to the biter, less waste.

**Choice 1, the meal gate.** *Recommended:* the biter keeps the wound's tissue only if its biting part carries
intake. What it keeps in a step is capped at that part's intake rate: intake times surface area, times
`IntakeScale`, times the step, at 100 J a unit. A biting part named in several contacts in one step has one cap,
shared in proportion to what each wound offered. The rest of the wound's tissue goes to the water as waste. This keeps
intake meaningful and priced, and keeps `IntakeScale` as the yield dial. *Alternative:* every wound past armour feeds
its biter in full, whatever its intake.

Of what is kept, `IntakeWasteFraction` (0.2) is waste. The other 0.8 goes into the biter's reserve.

**Choice 2, the waste route.** *Recommended:* all waste, the 0.2 and anything over the cap, goes to Nutrients as
detritus at the bitten body's point. That is today's route for a corpse meal, and D220's "its matter returns to the
water". *Alternative:* the waste is burnt, its joules out as heat and its units into the spent field.

A tough part has a large health pool, so one bite takes a small share of its health and of its tissue: a tough part
takes more bites for the same mouthful, as D220 says.

## 2. Healing re-buys the tissue

A part above zero health heals as today, at `HealingPerSecond` of its health a second, paid from its owner's reserve.
The joules for the health are burnt, as now. Under the dial, healing also re-buys the tissue the health stands for:
restoring a share `h` moves `h × T` joules from the reserve into the tissue account. That is charged matter staying
charged, and nothing is burnt for it. Without this a healed part could be eaten whole twice, and the tissue account
would fall below zero. A body that cannot pay both heals in proportion to what it can pay.

This is a consequence of D220, not a choice.

## 3. Growth reads the intact body

`World.GrowOne` grows a juvenile by the gap between its adult tissue and its tissue account. Under the dial a bitten
adult's account is below its intact tissue, so it would look like a juvenile and be re-scaled smaller, with its
reserve refunded. So, with the dial on, every place that compares the tissue account with a phenotype's tissue reads
the intact figure instead: the account plus each part's eaten share `(1 − health) × T`. That covers `GrowOne`, the
part's release in `KillPart`, the module rule's release (`DropOneModule`) and the account `AdoptPlan` sets. A plan
change sets the account to the new plan's tissue at each surviving part's health. The eaten share is regained by
healing alone.

This is a consequence of D220, not a choice.

## 4. The leaf

A leaf is bitten as any other body is.

**Choice 3, the leaf's light.** *Recommended (as the coordinator proposed):* a part's lit area is scaled by its
health, which is now its uneaten share, so a grazed leaf fixes less light until it heals. It is a change to
`Metabolism`'s lit area, made only with the dial on. *Alternative:* a leaf fixes light at full size until a part dies.

## 5. Death, and the remains

A part dies at zero health. By then it has been eaten whole, so nothing of it is left. A body dies as today: its root
dies, it falls under the mass floor, it starves or it ages.

Whatever dies uneaten becomes remains: a whole body that died, or the limbs beyond a part that was eaten whole.
Remains keep the dead body's shape, health and tissue account. They have no brain and no upkeep. They stay in the
solver as a physical body, are bitten by contact as in section 1, and are healed by nothing.

**Choice 4, the reserve.** *Recommended:* a dying body's reserve and gestation account go into its remains as one
store, eaten in proportion to the tissue: a bite that takes a share of the remains' tissue takes the same share of
the store. A cut-off limb takes its reserve share by volume into its remains, as a corpse does today. *Alternative:*
the reserve is burnt at the death, and a cut-off limb's share stays with the living body.

**Choice 5, the window.** *Recommended:* remains stay whole and edible until `RemainsWindowSeconds` after the death,
then close. The theatre fades them by age. *Alternative:* they lose a share of what they hold every second.

At the close, D220 rules the route: what is left is burnt. Its joules leave as heat and its units go into the spent
field at the remains' point, where plants take them up (`BurnUnbitten`'s route).

**Choice 6, the remains' physics.** *Recommended:* brain off, joints limp (no drive), powered float off. Remains
drift and sink by their own buoyancy, and collide and are touched as bodies. *Alternative:* frozen where they died.

**Choice 7, a diverged body.** *Recommended:* a body whose physics diverged is burnt at once and leaves no remains,
since its geometry cannot be trusted.

**Choice 8, a plant's held spent units** (D206). *Recommended:* they go to the spent field at the death, today's
route for a death without a corpse. *Alternative:* they stay with the remains until the window closes.

**Choice 9, remains of remains.** *Recommended:* a part of the remains eaten whole sheds its uneaten limbs as new
remains, which keep the original death second, so no bite renews the window.

## 6. Every route matter takes with the dial on

A unit is charged (a joule over ρ, 100 J a unit) or spent.

| Step | From | To | Books |
|---|---|---|---|
| a bite, kept share | the bitten part's tissue (or the remains' store) | the biter's reserve | charged to charged |
| a bite, waste and over-cap share | the bitten part's tissue | Nutrients at the bitten body | charged to charged |
| healing, the health | reserve | heat and the spent field | `EnergyOut`, `BurntTotal`, as today |
| healing, the tissue | reserve | tissue | charged to charged |
| a death or a cut-off limb | the body | its remains, whole | charged to charged |
| a diverged body | the body | heat and the spent field | `EnergyOut`, `BurntTotal` |
| a plant's held units at death | the plant | the spent field | `HeldReleasedTotal`, as today |
| the window closes | the remains | heat and the spent field | `EnergyOut`, `BurntTotal` |
| Nutrients | absorbers, the spent field | uptake, remineralisation | as today |

Photosynthesis, exudation, upkeep, the islands' influx and burial are untouched. Every new leg either moves charged
matter between accounts that `StandingJoules` sums, or books `EnergyOut` and the spent field in the same line.
`StandingJoules` gains one term, the remains' tissue and store. So `MatterResidual` and `AuditResidual` close with
nothing new to explain. Tests assert both stay at rounding over a dial-on world.

## 7. What changes for the absorbers and the Nutrients field

With corpses, nearly every death fed Nutrients through a corpse's instalments. Under the recommended routes, Nutrients
is fed by exudation (`ExudationFraction`, 0.15 in round 62) and by the waste of bites alone, and a body's matter goes
to the spent field when its remains close. So the dissolved food in the water falls, and the absorbers lose most of
what fed them, as D219's reasoning expects. I infer that the oases grow richer in plants and poorer in absorbers. The
screen's read reports both, and the Nutrients total beside them.

## 8. The remains in the solver, CPU and card

**In Core.** The world keeps a second list, the remains, in ascending id order. A body that dies uneaten leaves
`_living` for it and keeps its id, phenotype, health, tissue account and point. The metabolic pass, growth, birth, the
senses, the census, the leaf scent and the population counts walk `_living` only, so the remains pay nothing and are
counted in none. The mouth pass looks a contact's ids up in both lists. A remains part eaten whole is shed through the
same plan change a living body takes (`KillPart` and `AdoptPlan`). Remains with no parts left, or past their window,
close and leave the list.

**In the farm's CPU solver.** `Reconcile` keeps a body's solver body when its organism moves from the living to the
remains. The body is not rebuilt, so it carries on from the pose and velocity it died with. The solver's order is
ascending id over the living and the remains together, so it is still a function of the population. `Metabolise` asks
no brain for a remains body, and its joints get no drive and its float no power. Drag, buoyancy, gravity and contact
act as on any body. `HandOverContacts` lists the remains' overlaps like any other. A remains body that sheds a part is
rebuilt on its new plan, as a bitten living body is (`RebuildChangedPlans`). The pose, position and steering records
mark remains rows with a `dead` flag and the death second.

**On the card.** The card engine steps the same body list. A remains body is uploaded with its drive and brain
switched off by a per-body flag, so the kernels skip its network and apply no joint torque. Its class and group are
chosen as for any body. The card identity runs at groups 32 and 64, with the dial off and on, prove the card and the
CPU agree.

**The checkpoint.** With the dial on, the world writes the remains list after the living: each body's organism record,
as for the living, plus its death second and store. The farm writes each remains body's solver state as it does for
the living. With the dial off the list is empty and nothing is written, so the layout stays 20 and a dial-off
checkpoint is byte for byte today's. That follows the spoil clock's precedent (`CorpseSpoiledTotal`).

**The theatre.** The live view and a replay draw remains from the records' `dead` flag, faded by the share of the
window gone. The theatre's code is the theatre agent's, so this spec names the record it reads and leaves the drawing
to it.

## 9. The instruments

The corpse origin books (`IntakeOwnKillJoules` and the rest) stay at zero under the dial, since they class corpse
meals. New stats columns take their place. They count joules kept from bites on the living and on remains, each split
into bodies with photosynthetic tissue and bodies without. They also count the waste, the tissue re-bought by healing,
the remains standing and their joules, the remains closed and the joules burnt at the close. The death row carries a
body's own kept joules from the living and from remains.

## 10. The build, in order, and its size in agent time

| Part | What | Agent time | Machine time |
|---|---|---|---|
| A | The dials, their binding, JSON, hash and validation; the corpse pass skipped and no corpse founded with the dial on; the bite pass's skeleton (gate, cap, rationing) and the new columns; the hash re-pins and the crowd fixture re-record | about 3 hours with review | minutes of tests |
| B | Sections 1 to 4 in Core: bite feeding, healing's re-buy, growth's intact tissue, the leaf's light, routes and tests | about 3 hours | minutes |
| C | Section 5 and 8 in Core: the remains list, bites on remains, the window, the routes, the checkpoint | about 3 hours | minutes |
| D | Section 8 on the CPU farm: `Reconcile`, no brain and no drive, contacts, records, the solver checkpoint | about 3 to 4 hours | a short founded run |
| E | Section 8 on the card: the per-body flag in the kernels | about 4 hours | two identity sittings, about 20 minutes each |
| F | The theatre's fading, by the theatre agent | about 2 hours | a look test |

A runs first. B and C follow, one after the other, while the owner rules on the choices; each choice touches B or C
only. D follows C. E and F can run beside each other once D's record format is fixed. The screen's launcher and its
pre-registration are written beside B and C, and the screen launches after E on the coordinator's go.

## 11. The proofs the build owes

- Dial off: the filtered Core and Farm tests, and the full Core suite. A dial-off resume of a recorded world on the
  CPU solver, compared row for row with the same resume on the recording's build. Card identity at groups 32 and 64,
  as round 62's was proven.
- Dial on: both residuals at rounding over a founded world. A test per route in section 6. A checkpoint round trip
  with remains in it, and a resume that matches the unbroken run. Card identity at groups 32 and 64.
