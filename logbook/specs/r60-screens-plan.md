# Round 60's fed-island screens: how they run

D175 builds fed islands and screens them before round 60 is designed. Matter arrives on D109's islands, and
burial takes from each column's lowest live cell. The screens are four arms on round 59's world for 10,000 s on
seeds 1 and 2. The arms and the reads are the proposal's (`proposals/propose-r60-patchy-matter.md` §5). This file
says how they run on the card and what holds them.

## The arms

Each arm is an environment block in `rounds/`. `env-r60s.ps1` is round 59's world as D174 sets it, on round 57's
settings. It has the drag on turning in its spin form, and the touch of the rock and its sense. The leaf smell is
in its direction form at 6 m, with the farm's 2 m cell and 1 m² half scale. There is no anchoring cell.

| Arm | Block | Its own settings |
|---|---|---|
| r60s-c | `env-r60s-c.ps1` | none, the control |
| r60s-i1 | `env-r60s-i1.ps1` | influx 1.2 units/s on the islands, burial 0.001/s |
| r60s-i2 | `env-r60s-i2.ps1` | i1 with D071's settling at 0.02 m/s |
| r60s-i3 | `env-r60s-i3.ps1` | i1 with burial at 0.003/s |

Every arm places its founders by round 59's rule, D116's, which lands a founder where its own food is. D109's
placement by the dissolved matter cannot join it, because the farm refuses a world with both on: they are one rule
asked two ways. The first check founded i1 with both and was refused, on 2026-10-14. The coordinator ruled the
same day to keep D116 in every arm, so the fed arms differ from the control in the matter alone. Where founders
start in round 60 is the proposal's question 4 and stays the owner's. Under D116 a matter-eater lands where the
matter is, which on fed islands means the islands (inference).

Loaded as `card-group.ps1` loads them, each fed block differs from the control in those settings alone, and each
is a hashtable of its own (checked on 2026-10-14). Every name was found in r60-fed's `EnvBinding.cs` before
launch, and each world's `config.json` is read back after it ends.

## On the card

The card leaves influx and burial on the host (read from code on r60-fed). `World.Step` calls
`DepositMatterInflux` and `BuryMatter`, and the farm offers the card's transport to the snow's grid alone
(`Program.cs`). Even that grid goes up before each step and comes back after, so nothing lives on the card between
steps. The matter's settling, mixing and drift stay on the host too.

A seed's four arms run as one group in one farm process (`card-group.ps1`). Its `-Launcher` now also takes a
comma list of one block a world, so worlds of different settings share the process. The group steps each world on
a thread of its own over one device. A world's pace in a group is set by its neighbours, so the arms' paces are
compared within a group only.

## The steps

All are `rounds/r60s-screens.ps1 -Commit <commit> -Step <step>`, run from r60-fed's worktree, and every card step
runs alone in a quiet machine the coordinator holds. The card's settings are round 58's: group 32, single
precision, the snow's transport, the scent's fill, the concurrent mode and the spin at 5,000. The farm runs 8
threads on logical 16-23, with the driver watch and the heat stop. Round 59's group heat-stopped at 92 C on
16 threads on 16-31 on 2026-10-14, and s1's first try at 94 C on 12 threads on 16-27 on 2026-10-15, each on a
short build burst beside the busy E-cores. The coordinator chose 8 threads on 16-23 over holding the other project's builds
through every group.

Every step but solo lets four kinds of `dotnet.exe` run beside it, by their command lines: the nursery room's
server, a nursery run on its own cores, the other project's builds and runner, and the compiler server's shutdown. The card scripts take them as
`-AllowBeside`, refuse a match that has the card's driver loaded, and name each one in the log. Any other dotnet,
test host or farm still refuses the launch. solo runs with nothing beside it, since its wall is a timing read,
unless `-SoloAllowBeside` names what may. On 2026-10-15 the coordinator kept the nursery room's server up on logical
8-11 for it; the server idles between requests.

The card is shared with the other project's drawing from 2026-10-14. The owner asked for it, and the coordinator holds that project
for solo alone. A step launches with the card's memory under 12,000 MiB, which leaves room for a group of about
7 GB on the card's 24,576. The other project's batch-mode Unity, by its command line, is logged as allowed, and any other
batch-mode Unity refuses. solo keeps the limit at 4,500 MiB, about the owner's desktop alone, and allows no Unity.
The short step's peak is now the whole card's, so it is logged beside the reading at launch.

1. **check**, off the card: ILGPU's CPU accelerator founds the four arms on seed 1 as one group for 30 s. It
   checkpoints every 15 s, on 4 threads pinned to logical 16-19. It then resumes the group at 15 s. Each world's
   resume must be its whole run after 15 s (`group-resume-compare.py --lone`). A founded card run meets code that
   a resume never reaches (`src/Evosim.Farm.Gpu/CLAUDE.md`), and this also meets the fed islands before the card
   does.
2. **short**, on the card: the seed 1 group founded for 60 s. It reads the group's memory on the card and its
   pace. Above 20,000 MiB at its peak the step stops, and the screens run in pairs.
3. **s1** and **s2**, on the card: the four arms on seed 1 (`r60s-g1`), then on seed 2 (`r60s-g2`), 10,000 s,
   checkpoints at 5,000 and 10,000 s. The farm's own cadence writes snapshots and field dumps every 100 s and
   positions every 10 s. `field-map.py` and the 10 m-square reads take those.
4. **solo**, on the card: r60s-i1 alone on seed 1 for 1,000 s. Its wall and memory are what r60s-big is set
   against, since a world in a group shares its wall.

5. **big**, on the card: r60s-big alone on seed 1 (world `r60s-big-s1`, since the step log is `r60s-big.log`) for 10,000 s, as a group of one on s1's settings, with
   checkpoints at 5,000 and 10,000 s. Its block, `env-r60s-big.ps1`, is the winner's, r60s-i2 (named on 2026-10-15
   from the eight screens' reads, `scratch/r60s-read/summary.md`), with `EVOSIM_AREA=88000`,
   `EVOSIM_MATTER_ISLANDS=90` (the islands' wavelength in metres), `EVOSIM_MATTER_ISLAND_COVER=0.05` and
   `EVOSIM_REEF_MAX_COUNT=256`. The bed's 41 m tilt is held. Each of the four settings is read back from its
   `config.json` after it ends.

Every world must end by its budget with its books closed and its manifest naming the clean commit. It must pass
`card-run-check.ps1`'s every condition, show no overflow, and carry the three switches on its engine line. A fed
world's `config.json` must place the matter on the islands. Anything else stops the step.

## The machine

| Step | Where | Time |
|---|---|---|
| check | logical 16-19, 4 threads, off the card | 20 to 40 minutes (guessed) |
| short | the card | about 5 minutes |
| s1, s2 | the card | 55 to 90 minutes each (guessed) |
| solo | the card | about 8 minutes |

The groups' times are guessed from round 58's three-world groups and the leaf timing's founding. A founded world
of round 59's kind ran alone on the card at 5.8 times real time, with 2,016 bodies at 12,000 s (r59g-found,
measured 2026-10-13). The design study puts three worlds in one group at 45 to 70 minutes for 10,000 s. In all
the card is wanted for about 2.5 to 3.5 hours, in two or three windows.
