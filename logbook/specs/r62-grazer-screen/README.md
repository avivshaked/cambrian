# The r62 grazer screen

This is a screen, so it has no pre-registration. The coordinator set it out on 2026-10-19, after the owner stopped
round 62 at about 900 s. The owner judged that round 62's grazer dials had not been found, and that the
round would end as round 61 did. Round 61's grazers ate about a sixteenth of what they burned. Each arm here moves
one dial on that gap, and one arm moves them together.

## The world and the build

Every arm is round 62's world (`rounds/env-r62.ps1`, D209) with one change of founding: the trickle draws from round
59's pool (`rounds/r59-pool.tsv`) at a share of 0.5. Round 62's own pool held the four stomachs alone, and in 900 s
of `r62-s6201` not one founder was born with a mouth. Round 59's pool adds the leaf tank's ten direction swimmers,
each with a mouth (attack 1, intake 1), so each world founds enough grazers to read.

The build is branch `r62-graze`: round 62's build (`1465b670`) and the two dials below. The owner asked for them on
2026-10-19, with the rule that every world rule has a dial or a stated reason why it has none.

- **`EVOSIM_INTAKE_SCALE`** (`RunConfig.IntakeScale`, default 1) multiplies the rate every mouth takes from a corpse
  at, in `WorldMouth.MouthRate`. That one function sets both the demand that shares a corpse among its mouths and
  each mouth's take, so a corpse still bounds every meal. The mouth's price stays on the Intake gene.
- **`EVOSIM_LEAF_TISSUE_ENERGY`** (default 1) multiplies the photosynthetic cell's tissue energy, in the cell table
  the farm builds and the config saves and hashes. One function, `Metabolism.TissueJoules`, gives both what a body
  costs to build and what its corpse holds. A leaf therefore costs that much more to grow, paid from light, and feeds
  that much more. No energy is created.

## The arms

All are founded at seed 6201 and run for 12,000 s, as one card group, with the snow's transport on the card.

| Arm | Launcher | Change from the control | configHash |
|---|---|---|---|
| `r62g-ctl` | `env-r62g.ps1` | none | `ad5ab4c5f22cc763` |
| `r62g-reach` | `env-r62g-reach.ps1` | the mouth's reach 2.0 m (0.5) | `91f367cd8d147dbe` |
| `r62g-eat4` | `env-r62g-eat4.ps1` | intake scale 4 | `3bba0fd8a3220dee` |
| `r62g-food2` | `env-r62g-food2.ps1` | leaf tissue energy 2 | `3354d3715c177b23` |
| `r62g-endow` | `env-r62g-endow.ps1` | founder endowment 2,400 s (600), every founder | `f7609700b90565a1` |
| `r62g-combo` | `env-r62g-combo.ps1` | reach 2.0, intake scale 4, leaf energy 2, endowment 2,400 s | `a963184e84a7192d` |
| `r62g-nursery` | `env-r62g-nursery.ps1` | the pool is the nursery's 23 smell-following foragers | `bcde91333d953abf` |
| `r62g-price` (optional) | `env-r62g-price.ps1` | the mouth's upkeep 0 (0.1 W per unit per m²) | `afc31dc59c02cf16` |
| `r62g-keep` (optional) | `env-r62g-keep.ps1` | intake waste 0 (0.2) | `6d76db5c18b963e3` |

The nursery arm's pool is `rounds/r62g-pool-nursery.tsv` over `inocula/r62g-nursery-d178/`: the 23 examined genomes
of the nursery run `leaf-direction-patches` (registry row 20). It asks whether skill helps once food pays.

## The endowment arm is cut by D124's cap

D124 caps a founder's start, purse and endowment together, at 0.9 of its breeding gate plus what its growth will
cost (`EVOSIM_FOUNDER_RESERVE_CAP` 0.9). In the preflight's 30 s the floor founders' endowment averaged 148 J at
600 s and 289 J at 2,400 s, about twice where four times was asked. The largest rose from 1,076 J to 1,251 J.

The owner ruled on 2026-10-19 to keep the cap and to read the capped arm first. Without the cap, most founders would
breed on the gift at once, as round 48's did. If the capped arm does no better than the control, a bigger start
will not help. If it does better, the next step is a gift that pays only upkeep, which needs a build and a ruling.
No uncapped worlds run in this screen.

## A known confound in the food arms

A corpse's reach is measured from the radius its joules would fill at the structural cell's density
(`WorldMouth.CorpseReach`). At leaf energy 2, a leaf corpse holds twice the joules for the same body. Its own
radius therefore reads larger by the cube root of 2, about 1.26 times, before the mouth's reach is added. A leaf corpse in
`r62g-food2` and `r62g-combo` is therefore a little easier to find as well as richer. The coordinator ruled on
2026-10-19 to leave the reach as it is. The read works around it:
- the reach arm prices an easier find on its own;
- kills per grazer-second against joules kept per kill separate finding from eating.

If food2 wins and the read cannot tell the two apart, the corpse is made to carry the dial's extra leaf joules in its
checkpointed state before round 62 relaunches. `Corpse.Body` cannot serve, since a checkpoint does not carry it.

## The read

`scripts/reads/r61-grazer-economy.py` per arm gives grazer births, the deepest generation, replacement per cohort and
intake per grazer-second. Beside it go kills per grazer-second and joules kept per kill, and `scripts/reads/r60-read.py`
for the oases.

## The result

The seven worlds ran as one card group from 19:35 to 22:32 on 2026-10-19. Every world reached 12,000 s, its books
closed, and none diverged. Pace ran from 1.71 times real time (the control) to 2.21 (the nursery arm). The tables
are `r62g-read.txt`, `r61-economy.txt` (read in years of 3,000 s) and `r60-read.txt`, beside this note.

Only the reach moved the grazers.

| Arm | Grazers bred | Deepest line | Replacement | Kept intake per grazer-second | Share of upkeep |
|---|---|---|---|---|---|
| control | 9 | 1 | 0.03 | 0.14 W | 0.16 |
| endowment 2,400 s | 3 | 1 | 0.03 | 0.10 W | 0.11 |
| reach 2.0 m | 165 | 5 | 0.43 | 0.73 W | 0.80 |
| intake scale 4 | 5 | 1 | 0.00 | 0.12 W | 0.13 |
| leaf energy 2 | 11 | 1 | 0.03 | 0.15 W | 0.16 |
| all four | 115 | 2 | 0.41 | 0.61 W | 0.67 |
| nursery pool | 30 | 1 | 0.23 | 0.06 W | 0.07 |

Replacement is mouthed children per grazer, over grazers born in the first 6,000 s. The intake column charges every
mouth's kept intake in the world to the grazers, so it is an upper bound on theirs.

- **The endowment, read first as the owner ruled.** The capped endowment did no better than the control. By the
  owner's rule, a bigger start will not help, and the gift that pays only upkeep is not needed.
- **The reach.** A mouth that reaches 2 m eats corpses it did not make. In the screen's fourth year, mouths emptied
  608 corpses while grazers made 126. In the control the counts were 60 and 44. The read does not sort the
  eaten corpses by guild. My reading is that most are leaves dead of age, since leaves are most of the deaths:
  8,833 leaves died in the control, about seven in ten of age.
- **The intake scale and the leaf energy** gave nothing. What limits a grazer is finding the leaf. How fast its mouth
  works and how rich a leaf is do not. The leaf arm also held fewer bodies (992 at 12,000 s), since a leaf
  costs twice as much to grow. Its corpses read about 1.26 times larger to a mouth (the confound above), and the arm
  still did not gain. So the confound does not muddy this answer, and no change to the corpse's state is needed.
- **The combined arm** gained what the reach arm gained, a little less.
- **The nursery's foragers** lived longer as founders but killed less (57 kills against 113), and paid less of
  their upkeep.
- **No arm keeps grazers alive on its own.** At 12,000 s each world held 6 to 13 mouthed bodies, and replacement
  is below one everywhere. The trickle still carries the guild.
- **The oases held in every arm.** Between 0.81 and 0.85 of the 10 m squares were empty. Between 0.73 and 0.78 of
  the living were on five islands. The desert stayed below the line, and burial ran at 0.90 to 0.96 of the influx.
  Only the leaf arm fell below 1,000 alive.

## Files

Each arm's `config.json`, from the preflight on ILGPU's CPU accelerator at `a384d774` (30 s founded, every world
`ended`), sits beside this note as `r62g-<arm>-config.json`. The preflight founded only floor founders in its 30 s;
the trickle's pool founders come later. Both pools loaded: round 59's 14 genomes, and the nursery's 23 (the header
reads `pool 0.5 of 23 weighted`).
