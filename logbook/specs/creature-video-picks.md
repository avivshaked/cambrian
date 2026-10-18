# Ten creatures for the video: the picks, where they are, how to draw them

This is the shortlist for the ten-creature film the owner asked for on 2026-10-06. It gives about
thirty seconds to each creature and about five minutes to the film. Each creature tells its own
story: where it came from, what is striking about it, and what became of it. The film is made with
`.claude/skills/create-story-video/`, and this file gives that flow its subjects. It does not replace
any of the flow's steps.

## The rule the picks were made by

A creature is chosen for what its body was in life. Its genome overstates that body. Development prunes it: no more than 16 parts, a depth of 8, and no part smaller
than 0.0001 m³ (`DevelopmentLimits`). A newborn is also drawn at a fraction of its adult size, so a
part can be missing at birth and appear as the body grows. A shortlist read off the genomes by hand
once named seven "weird" bodies. In life, none of the seven had a joint or a stomach.

So every pick below was confirmed in the pose stream, `poses.bin`. It holds every living body every
10 s: id, root position and rotation, body fraction, guild flags, reserve and joint coordinates. The
flags are absorptive 1, jointed 2 and photosynthetic 4. A leaf has flags 4 and nothing else. The
number of joint coordinates is the body's degrees of freedom (DOF). A pick's shape is still unseen
until a solo still has drawn it (below). Narrate no detail of its shape before that still exists.

## The ten

The run directories are under `runs/<arm>/`. "Born" and "died" are in simulated seconds. A round-57
year is 6,000 s long: summer runs from −250 to 2,750 s, and winter from 2,750 to 5,750 s. Rounds 53 and
55 had steady light, with no day and no season (`dayNightAmplitude` 0). Round 56 had 1,000 s days
and 10,000 s seasons, each at amplitude 0.5. Round 57 is still running, so its counts are as of about 35,000 s. Its ids
will not change.

| # | Arm, run dir | Id | Kind in life | Born → died | Why |
|---|---|---|---|---|---|
| 1 | r55-s1, `2026-10-04-171541-54137db3` | 41364 | jointed leaf, DOF 18 | 21,580 → 23,491, aged | The most jointed body in any recent round. Generation 18, no offspring |
| 2 | r53-s1, `2026-09-29-221723-7c5e42d9` | 19031 | jointed leaf, DOF 12 | 12,048 → 14,132, aged | A many-jointed leaf from a round without seasons, the comparison for #1 and #3 |
| 3 | r57-s1, `2026-10-06-065821-dfc0e881` | 18207 | jointed leaf, DOF 12 | 12,348 → 14,960, starved | Round 57's most jointed body to reach adult size; four others passed DOF 12 but showed in only one or two frames. Born in its third summer, starved 210 s into that winter |
| 4 | r57-s1, same | 20184 | leaf, stomach and joints | 13,098 → 17,510, aged | All three guilds in one body. Lived through a whole winter |
| 5 | r57-s2, `2026-10-06-065827-dfc0e881` | 16076 | stomach and joints, no leaf, DOF 4 | 7,304 → 10,604, aged | The most animal-like body: it ate and moved, and made no food from light. Generation 6 |
| 6 | r57-s2, same | 11630 | stomach only | 5,177 → 7,765, starved; 6 offspring | Founder of seed 2's matter-eater guild. It arrived by the trickle in the first winter. Its line is 723 of the 732 matter-eaters alive at 36,880 s, 20 generations on, and the guild grew in every winter |
| 7 | r56-s3, `2026-10-04-215712-a174d013` | 8004 | joints only, DOF 2 | 3,553 → 5,262, starved | A body that could only move: no leaf and no stomach, so no income. A trickle founder |
| 8 | r53-s3, `2026-09-30-054240-7c5e42d9` | 33784 | none of the three | 15,143 → 16,193, starved; 2 offspring | No leaf, stomach or joint, and it still left two young. A trickle founder |
| 9 | r57-s1, `2026-10-06-065821-dfc0e881` | 41, then 104226 | jointed leaf, DOF 2 | #41: 15.5 → 1,371, aged. #104226: 35,246 → 35,418.5, starved | Seed 1's story: one founder's line is 6,435 of the 6,451 bodies alive at 35,290 s. Its descendant #104226 is 61 generations on, one of the 161 jointed leaves alive at that second, 160 of them #41's. It was born with joints its parent lacked, and starved at 0.22 of adult size |
| 10 | r57-s2, `2026-10-06-065827-dfc0e881` | 1 | leaf | 0.5 → 2,157, aged; 16 offspring | Seed 2's founder, the saver against #9's thin margin. Its line is 8,821 of the 9,568 alive at 36,880 s |

There are four reserves, in case a still shows a pick to be dull:

- r56-s2 `2026-10-04-183358-a174d013` #90944: a leaf with DOF 12 in the stream, but jointed 0 in its
  birth row. It may have grown its joints after birth. Read it before using it. It was alive at the
  run's end.
- r55-s1 #34381: a jointed leaf with DOF 18, from 18,724 to 19,296, starved.
- r57-s2 #99200: stomach and joints, a trickle founder born at 32,793, died aged at 37,628.
- r56-s1 `2026-10-05-002700-a00d025c` #3605: a jointed leaf with DOF 12, from 1,164 to 1,676, aged.

## Finding them in the records

Each run directory holds the following:

- **`lineage.jsonl`** has one row per birth (`e: "b"`: `id`, parent `p`, time `t`, generation `g`,
  the flags `abs`, `jnt` and `pho` at birth, and `src` for a founder, `floor` or `trickle`) and one
  row per death (`e: "d"`: `id`, `t`, cause `c`). Follow `p` back to find a line's founder.
  Read a live run row by row and stop at a last line with no line break.
- **`genomes.jsonl.gz`**: the genome, keyed by the organism's id.
- **`poses.bin`**: the body over its life, read with `python scripts/poses-read.py <run dir> --at <s>`.
  `scripts/reads/stream-scan.py` lists the jointed, the odd and the large in a run. Its tables
  are `scratch/creature-pick/stream-<arm>.tsv`, with columns `id`, `first`, `last`, `maxdof`, `flags`,
  `frames` and `maxfrac`.
- **`snapshots/NNNNNNNNN.jsonl.gz`**: every living genome every 100 s.
  **`positions.jsonl.gz`**: where each one was. **`checkpoints/`**: the world every 100 s, for
  filming.
- **`config.json`, `run.json` and `stats.jsonl`**: the world, the manifest and the census. Each round's
  logbook entry says what its world was. Round 57's is `logbook/0136-winters-and-weather.md`, its
  pre-registration, on the `round-57` branch until the round ends.

## Drawing them

Follow the story flow's `graphics.md` and `filming.md`. Three things are particular to these picks:

1. **Draw a solo still of each pick first.** `TheatreSoloStill` grows the body from its snapshot row
   with its own run's development limits (`graphics.md`). Choose a snapshot second inside the pick's
   life, near its largest `maxfrac`. Confirm the guilds and the joints the still shows against the
   table above. If they disagree, the still is right and the table is wrong.
2. **Every round needs its own build.** `-From snapshot` stills and solo stills read an older run's
   config through the picture-only readers. A film window is recorded on the farm program that
   recorded the run (`filming.md`), and that means the round's branch: `round-53`, `round-55`,
   `round-56` or `round-57`. Each worktree is `scratch/wt-r5N`. The manifest's config hash, the run
   directory's suffix, names the world.
3. **Refresh the render worker before the first picture** (`./scripts/new-worker.ps1 -Workers <n>`).
   A stale worker draws in an old skin, or refuses a view it does not know
   (`unity/Assets/Theatre/CLAUDE.md`).

The reference film is the last one the owner accepted: `scratch/owner/r48-story-full/r48-story-full-v2.mp4`.
Every test render is set beside it before the owner sees it, in the check the root `CLAUDE.md`
describes.

All work on the machine, Python reads included, goes through `scratch/machine/queue.md` and the
coordinator's go.
