# The corpse spoilage clock (D212)

The build spec for D212's spoilage clock, on branch `r62-spoil`, made from `r62-graze`.

## The rule

A corpse's food value halves every `CorpseHalfLifeSeconds` (`EVOSIM_CORPSE_HALF_LIFE`; 0, the default, is off).
Each step, after the drift has aged it and before its decay instalment, a corpse loses
`Joules × (1 − 2^(−dt/H))`. Those joules leave the world as heat (`EnergyOut`), as the bacteria that spoil
carrion respire them. Their matter, the same joules over `JoulesPerUnit`, goes to the spent field at the corpse's
point, as a burn's does. A cumulative `CorpseSpoiledTotal` counts the joules. The loss is first order on what the
corpse still holds, so its remaining food halves every H seconds whether or not anything eats it. Settled and
sinking corpses spoil alike. The held spent matter of D206 (`Corpse.SpentUnits`) is untouched.

## Scope: corpses only

The coordinator ruled overnight on 2026-10-19, under D212 and on the owner's leave, that the dial covers corpses
only. Detritus already spoils as D212 describes. Remineralisation takes its stock at `RemineralisationPerSecond`,
5 × 10⁻⁴ per second, a half-life of about 1,386 s. It books the joules as heat and sends the matter to the spent
field. A grid cell holds no age, so a second detritus dial would only be a second remineralisation rate. A screen
may vary `RemineralisationPerSecond` beside the half-life instead.

## A side effect, named and kept

A corpse's reach to a mouth is measured from the radius its joules fill (`WorldMouth.CorpseReach`), its scent from
its joules, and its exit from its joules against `FirstJoules` (`CorpseLeavesBelowFraction`). A spoiled corpse
therefore looks smaller to a mouth, smells weaker and leaves sooner. The coordinator ruled the same night to keep
this and to name it. Without spoilage a sinking corpse leaves after about 7,800 s. At a half-life of 2,000 s it leaves
after about 4,100 s, and at 500 s after about 1,700 s. These times are worked from the rates and have not been measured.

## Identity

At 0 no new code runs, so every world is bit-identical to `r62-graze`. The state version stays 19 if older
checkpoints load and the tests prove it, as the coordinator ruled. The total is written into a checkpoint only when
the dial is on. The config hash changes, as it does for every tunable, and the fixtures re-record. Corpses live on
the CPU, and the card's scent reads a per-fill snapshot of corpse joules, so the card needs no copy. The identity
check is the accelerator suite, then a short card run with the dial on at groups 32 and 64.

## The fixtures

Core's recorded worlds are `r42` at `907cc1d7f5df2df0` and `r57-s2` at `da3a78db5d2044d6`. The Dynamics crowd
fixture is `r62sfix-s4`: round 44's world at seed 4, recorded to 20,000 s at 4 threads by `fixtures.ps1` beside
this note, on 2026-10-20 from 07:09 to 08:09. The pace guard's ratio was 0.25 from the start, since at 0.5 it had
stopped both earlier recordings as the crowd grew, and the run went unbroken to its budget. The Dynamics fast suite
passed 232 of 232 against it.
