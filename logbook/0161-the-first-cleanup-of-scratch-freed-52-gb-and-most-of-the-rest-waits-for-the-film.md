# 0161 — The first cleanup of scratch freed 52 GB, and most of the rest waits for the film

**2026-11-10**  ·  Disk upkeep: scratch/ against its 75 GB budget

The project's scratch folder, where transient files go, held 300 GB against a budget of 75 GB. Today I cut it to 224 GB
and freed about 52 GB on the D: drive, in two batches that the owner approved one at a time. I did not get further,
because most of what remains belongs to round 67's film, which has not shipped. The owner decides when that data can go.

## How I worked

The rule for this job was to delete nothing without the owner's yes for that batch, and to leave alone whatever a film
lane or the coordinator still used. So I did the reading first. Two subagents listed every folder and every worktree
with its size, its age and whatever pointed at it: tracked files, the logbook, HANDOFF and the test pins that
`scripts/test-pins.py` lists. A worktree is a second checkout of the repository in its own folder. Then I asked the
sessions that own the data, the coordinator and the film and theatre lanes, whether they still needed it. Only after
that did I show the owner a batch.

One fact changed the sums. The film windows (recordings of a world's bodies for the camera) are stored with hard links:
two file names pointing at one copy on disk. A tool that adds up file sizes counts such a file twice. The inventory
put `scratch/story-video-colour` at 57 GB; on disk it held about 34 GB.

## What was removed

| Batch | Item | Size | Why it was safe |
|---|---|---|---|
| 1 | `scratch/readme-pass` | 0.68 GB | A clone and logs from a quickstart test. Nothing tracked cites it. |
| 1 | `scratch/world-skin` | 0.34 GB | One run's pose data and a clip, ten days old. Nothing cites it. |
| 1 | `scratch/merge-bite` | 0.15 GB | Build and test logs from a merge. Nothing cites it. |
| 2 | `scratch/story-video-colour/dryrun-test`, `-test2`, `-test3` | about 34 GB on disk | A builder's dry runs of 2026-11-07. The film lane said nothing reads them. The poses come from round 67's checkpoints, which are not thinned, so they can be recorded again. |
| 2 | `scratch/theatre-realism/windows`, `corpse`, `cf-sample`, `pose-film`, and `growth-pulses` but for its note | about 14 GB | The theatre lane said it no longer used them. They can be recorded again from checkpoints (the lane's belief; neither of us opened each folder). |
| 2 | Thirteen worktrees, removed with `git worktree remove` | 4.4 GB | Merged into main and clean. Their branches are kept, so any can be made again. |

The 13 worktrees were `wt-smell`, `wt-mouth`, `wt-nose-mirror`, `wt-bloom`, `wt-bite-bit`, `wt-int2`, `wt-gate`,
`wt-ts-family`, `wt-gjk-fix`, `wt-remains-farm`, `wt-r64t`, `wt-r63b-fix` and `wt-r62cf`. Two of them, `wt-r64t` and
`wt-r63b-fix`, were on a detached head with no branch of their own; the inventory showed their commits are in main.
Some old launchers under `rounds/` name these folders. None is a round 67 launcher. A rerun of one of them would need
its worktree made again first.

Free space on D: went from 255 GiB to 306 GiB across the two batches. One growth note was cited from HANDOFF at a path
inside the folder I cleaned. I copied `growth-pulses/notes.md` to
[`specs/growth-pulses-notes.md`](specs/growth-pulses-notes.md) and changed HANDOFF's citation, because a file that a document
cites must not stay in scratch.

## What I left, and why

About 150 GB of scratch is off limits until the owner approves round 67's film as final. That is the film folder under
`scratch/story-video` (about 65 GB), `wt-theatre-look` (51 GB), `wt-camera5` (22 GB), and a long tail of theatre-lane
worktrees. Round 67's checkpoints stay whole for the same reason, under the rule that a round's checkpoints are
thinned only after its film is final. So the 75 GB budget cannot be met today, and I do not think it can be met before
that approval.

Other things I found and did not touch:

- **Local-only work.** No worktree branch has a copy on the remote. Branches such as `cand-cap`, `cand-cap-det`,
  `desert-burial` and `nursery-d178` carry hundreds of commits that exist on this machine alone. Some may be old
  history merged under other patch ids; I have not checked, and I removed none of them.
- **`scratch/killgap/windows`**, 3.5 GB of pose data. The owner did not approve it, so it stays.
- **`runs/`**, 138 GB against its 300 GB budget. Thinning checkpoints there needs a keep list per round, shown to the
  owner first, and only for rounds whose film the owner has approved.
- **A cleanup of 2026-10-29** ([`specs/data-retention-and-backup.md`](specs/data-retention-and-backup.md)) already set the
  rules for what a finished round keeps. I followed its guards: nothing written in the last 48 hours, and no worktree
  holding data of its own.

## What this changes

Nothing in the world or the code. The open choices are the owner's. One is whether `killgap/windows` goes. Another is
which rounds' films are final, so that their checkpoints can be thinned. The last is whether any local-only branch
should be pushed or retired. I will bring the next batch when one of those is decided.
