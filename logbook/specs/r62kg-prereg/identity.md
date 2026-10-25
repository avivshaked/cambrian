# The card identity round for integration branch 1: level (b) and the kill gut

*2026-10-25, the speed agent, for the coordinator. Integration branch 1 carries D228's level (b), the force at the
contact point, with its card port, and D232's kill gut. It ran on the coordinator's go, from commit `3790cbb4` for part 1
and `1dda6e05` for part 2, both with a clean tree. The Release farm was built from `d2ad919d`, whose farm source the
later commits do not touch. Each group founded seed 6201 four times for 600 s on the RTX 4090, in single precision with
the card's transport, at 14 threads on logical 16 to 29, with the heat stop on. Every world ended at its budget. The
outputs are in `identity/`, beside this file.*

## Part 1: every switch off, at group 32

`rounds/identity-int1-off.ps1` runs D227's launch identity group (the bite dial at 1, and the 2x, 4x and 16x arms).
Each world is compared with `scratch/contact-eating/identity-bdl/r62bdl-<world>-g32`, the same world on D227's build
`4e94b0e0`.

`gpu-accept.py` exits 0 for all four. Stats are identical on 60 samples, the digest on 601 rows, and lineage is
byte-equal (1,570 lines and three of 1,582). So with its switches off, this build is D227's build on the card, bit for
bit.

| World | Wall to 600 s, D227 | This build | Ratio |
|---|---|---|---|
| Dial at 1 | 3.370 min | 3.392 min | 1.006 |
| 2x | 3.354 min | 3.382 min | 1.008 |
| 4x | 3.329 min | 3.385 min | 1.017 |
| 16x | 3.329 min | 3.356 min | 1.008 |

The pace is 0.98 to 0.99 of D227's, where D228's build read 0.991 to 0.994. That may be a cost of about 1 % with the
switches off, but it rests on one reading of each world and may be noise (inference).

The group reported `STOPPED: not every world wrote run.json within 120 s`. The cause was the group's wait, not the
worlds. This build's farm took 2 min 55 s to write `run.json`, past `card-group.ps1`'s default wait of 120 s, so the
group gave the worlds up while they were starting. No stop reached them, and each ran to its budget whole. D228's part 1
did the same that morning, at 2 min 34 s. Part 2's script then passed `-WaitForManifestSeconds 600`, as the screens'
launchers do (`1dda6e05`). That changes how long the group waits and nothing in the worlds.

## Parts 2 and 3: each switch on, at group 32 and then 64

`rounds/identity-int1-on.ps1` runs four worlds:
- level (b) (`rounds/env-int1-atpoint.ps1`);
- the kill gut (`env-int1-gut.ps1`);
- both (`env-int1-both.ps1`), the first three on `rounds/env-r62cs-w1500.ps1`'s world;
- the kill-meal screen's own world (`rounds/env-r62kg.ps1`), whose config hash the pre-registration names.

| World | Config hash | 32 against 64 | Parts from the switch-off world at | Alive at 600 s (off 1,454) | Wall to 600 s, 32 / 64 (off 3.392 min) |
|---|---|---|---|---|---|
| Level (b) | `7f8d660e` | identical, 1,517 lineage lines | 15 s | 1,437 | 3.471 / 3.479 min |
| Kill gut | `9a030c14` | identical, 1,570 lines | never (below) | 1,454 | 3.398 / 3.415 min |
| Both | `f21c3d22` | identical, 1,517 lines | 15 s | 1,437 | 3.457 / 3.466 min |
| Kill-meal world | `277612f7` | identical, 1,582 lines | another world | 1,457 | 3.401 / 3.417 min |

`gpu-accept.py` exits 0 for each world at 32 against 64. The books close in every world, with the largest audit residual
at 4.1e-6 J and the largest matter residual at 1.6e-6.

Level (b) parts its world from the switch-off world at step 1,500, 15 s in. So it reached the solver on the card. It
costs about 2 % of the wall to 600 s here, on one reading (inference).

**The gut never acted.** In the gut, both and kill-meal worlds, no body was eaten, no part was killed and no kill was
swallowed by 600 s. A founded world has no killers that early. The gut world's digest and lineage are byte-equal to the
switch-off world's, and its stats differ only by the gut's new columns, all zero. Both is level (b)'s world to the bit.
So the round shows that the gut, switched on, changes nothing until a kill, on the card at both group sizes. It does not
show a swallow or a digestion on the card.

**The gap and how it closes.** The coordinator first ruled for one more group: `r62cs-w500-6201`'s checkpoint at a
second when grazers kill, resumed as a cousin with the gut on, at 32 and 64. It could not run on this build. A resume
builds its world from the source run's `config.json`, and that run was recorded before this build's tunables. Its
config has no `killSwallowsBody`, nor D228's three fields, and this build refuses a config with a field missing rather
than defaulting it. Every run on disk with kills is as old. Supplying the missing fields by hand would have defeated
the refusal, so it was not done. The coordinator then ruled, on 2026-10-25, that the proof comes from a checkpoint this
build writes. The kill-meal screen's check runs to 6,000 s with a checkpoint every 1,500 s. Its densest checkpoint
with kills is resumed for 600 s at 32 and at 64, and the pair counts only if both runs swallow a kill and eat a body
(`predictions.md`). The two runs are cousins of the check, comparable with each other only. The screen launches only
after the pair passes. The integration branch landed before the pair: the gut is off by default, and its off and idle
paths are proved on the card here.

The heat stayed at OK through the three groups: the processor at 50 to 68 °C, the card at 40 to 44 °C.

## The resume pair (2026-10-25)

The pair closes the gap. The check, `r62kgck-6201`, ran from `scratch/wt-r62kg` at `afd04461`. That commit is the
pre-registration's `9c01db64` with one change, to the two launchers under `rounds/`: one world on the card goes
through `scripts/card/card-run.ps1`, since `card-group.ps1` refuses fewer than two. Its source trees are the
pre-registration's, and its manifest carries core `a2a4aa0d`, dynamics `ec56227d`, farm `d552cc81` and config
`277612f7dc63322b`, all as named. Of its checkpoints at 1,500, 3,000 and 4,500 s, only the 4,500 s one is followed by
bodies eaten in the next 600 s (8, with 3 kills swallowed), so the pair resumed from it.

| Run | Group | Lineage | Bodies eaten, first sample to 5,100 s | Kills swallowed | Wall to 600 s |
|---|---|---|---|---|---|
| `r62kgpr-6201-g32` | 32 | 1,910 lines | 4 to 10 | 1 to 2 | 2.791 min |
| `r62kgpr-6201-g64` | 64 | byte-equal | 4 to 10 | 1 to 2 | 2.773 min |

`gpu-accept.py` exits 0: the stats are identical on all 60 shared samples, the digest on all 600 rows, and the lineage
byte for byte. Both runs swallow a kill and eat bodies, so the pair counts. The gut's swallow and its digestion are
identical on the card at groups 32 and 64. The largest audit residual is 1.1e-5 J and the largest matter residual
5.9e-6.

**The mutual kill.** At 4,657.5 s in the check, bodies 5344 and 5665 killed each other in one pass. The lineage
rows show where the joules went:
1. 5344's bite killed 5665 first. 5344 was alive, so it swallowed 5665's 314.65 J store into its gut.
2. 5665's bite then killed 5344. 5344's store took in its gut (330.49 J in all), and its killer was already dead, so
   it swallowed nothing and 5344 became remains.
3. The same bite took 5344's root. Its tissue and store moved into the remains of its lost subtree, body 6590, and
   the empty root closed at once (its closing row reads 0 J).
4. No body bit 6590, and it closed at 5,157.5 s with 645.14 J, burnt into the spent field at its point.

So the 314.65 J was never digested. It went back to the world's matter by the remains' close, and the books stayed
closed through it.
