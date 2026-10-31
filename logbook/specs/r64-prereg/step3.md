# r64's step 3: D's last 1,500 s, run twice beside itself

*Pre-registration, written by the coordinator on 2026-10-31, before this step runs. It launches from `scratch/wt-phase1`
at this file's commit, with that worktree's own Release farm.*

## Why

Step 2 (`step2.md`) ended D at 46,530 s, when the graphics driver logged an error and the card group's watch stopped
the farm. Step 2's read (`logbook/specs/r64s2-read/` on main) took D to 45,000 s. The owner ruled on 2026-10-31 that
card runs resume after a solo render, beginning with D's last 1,500 s. The step has two jobs. It takes D to 48,000 s,
and it is the card's first run after the event.

## The worlds

`r64s3-d1-6202` and `r64s3-d2-6202` both resume D's checkpoint at 46,500 s
(`runs/r64s2-d-6202/2026-10-30-233542-11de0613`). They run for 1,500 s, to 48,000 s, in one card group on step 2's
terms (`rounds/launch-r64s3.ps1`). A card group needs two worlds. Two copies of one world from one checkpoint give a
check that one world and a filler would not. Each is a continuation of D (D226) and is read as one.

## What is read

1. **The two copies agree.** Every simulated column of their stats rows is equal, row for row, from 46,500 s to
   48,000 s. A difference means the card or the group does not compute two identical worlds identically, and it stops
   card work until it is found.
2. **No driver event** from launch to end. One stops the card again and goes to the owner.
3. **D's last window**, 45,000-48,000 s, joins step 2's read with step 2's marks. Its first 1,500 s are step 2's own
   rows, and its last 1,500 s are the copy `r64s3-d1-6202`'s.

## Predictions

Mine, and guesses. The copies agree exactly (high). No driver event comes, since the event came with a new shader's
first render on the card beside the farm, and this step runs alone (moderate). D's grazers in the window stay between
1,900 and 3,200, and its leaves stay above 1,686 (moderate). The step takes 10 to 20 minutes of wall time.
