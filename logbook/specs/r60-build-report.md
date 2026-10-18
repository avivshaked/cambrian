# Round 60's build: the card resume carries its pairs, and a settled world resumed on the card is its run (2026-10-15)

This is the report on round 60's three build items, made on the branch `r60-fed` by the speed agent for the
coordinator. Round 60's pre-registration cites it. The cause it fixes is in [`r59-resume-survey.md`](r59-resume-survey.md).

## What was built

Round 59's B3 resumed a card world at 45,000 s and parted from its unbroken run at the first second. The card
holds each link sphere's centre as a float and a small rest beside it, and the root as a float position and a
carry. The checkpoint kept neither pair. The restore rebuilt the centres from the poses with the rest at zero.
For one step, every body resting on the bed or the glass was then up to a float step out.

Three items went in, at `480ea8a1`:

1. The checkpoint now carries both pairs, in a block of its own after each body. The world's state format goes
   from 17 to 18. Version 17 is still read as it was, so no recorded checkpoint, genome or test fixture was refused
   and nothing needed re-recording. On a resume the card packs a body from its pair only while the pair still sums
   to the centre the CPU holds. A body the CPU has moved since is split afresh, as at birth.
2. B1's birth overhead goes back to the matter field in double precision, through the field's exact deposit.
3. A new slow test settles a crowd for 2,000 s on the CPU engine. It then runs the crowd 20 s on ILGPU's CPU
   accelerator in single precision with the card's switches, and resumes that run from its own checkpoint at 10 s. Every digest and stats
   row after the resume must be the unbroken run's.

The new test passes in about a minute. With the restore made to drop the pairs again (a trial, never committed),
it parts at step 100,501, the first step after the resume. So the test sees B3's cause.

Two pinned hashes moved with the format, and `34dd272c` deals with them. The step-fold test in `Evosim.Dynamics`
now hashes each body without the new block, so its old pins hold. The farm's bite test hashes the whole written
state, so its two folded hashes were re-pinned; the trajectories it also pins did not move.

## The suites

Everything ran on logical processors 16 to 23 at eight threads, at BelowNormal, with the card hidden from the tests.

| suite | result |
|---|---|
| `Evosim.Core.Tests`, every test (`-All`) | 1,184 of 1,184 |
| accelerator batch (every gpu test but the card's own) | 59 of 59 |
| `Evosim.Dynamics.Tests` | 206 of 206, after the step-fold change |
| `Evosim.Farm.Tests`, card and slow tests left out | 301 of 301, after the re-pin |
| `Evosim.Nursery.Tests` | 215 passed, one skipped |
| `Evosim.NurseryRoom.Tests` | 680 of 686 |

The nursery room's six failures are its browser tests. Three stopped because Edge ended before it opened its
debugging port, and three because another process held a file in Edge's profile. They failed the same way on a
rerun, and nothing on `r60-fed` touches the room's code, so I read them as the machine's state and not the build's.

## The card check

The coordinator gave the card for one check, with the other project held. The step is `rounds/r60s-screens.ps1 -Step fix`, at
`2a72dd7f`. It founded the screens' winner, r60s-i2, alone on seed 1: 2,000 s on the card, with a checkpoint at
1,500 s. A second run resumed that checkpoint alone and ran to 2,000 s. Both were held to every condition in the card's
run check, and both met them all.

`group-resume-compare.py --lone 1500` read the resume against the unbroken run from 1,500 s on. Every row was the
same:

| file | rows after 1,500 s, all the same |
|---|---|
| lineage | 857 |
| stats | 50 |
| digest, one a second | 500 |
| absorptive | 491 |
| positions | 50 |

The founding took 4.3 min, at 7.75 times real time, and the resume 1.2 min. There were 1,850 bodies alive at
1,500 s and 1,592 at the end. The card peaked at 52 °C and 7,682 MiB, and the processor at 79 °C. The step ended at 06:47:29.

This resume was at 1,500 s, not at round 59's 45,000 s. The CPU-accelerator test shows that 2,000 s of settling is
enough to expose the old fault, so I expect the fix to hold for an older world. But no world tens of thousands of
seconds old has been resumed on the card yet. The coordinator ruled that such a check rides along with round 60's
own B3 clause rather than taking a card window of its own.

## A long resume, and a card-run fault (D186)

D186 ran big3 on from 10,000 s to 20,000 s, and the coordinator asked that the resume be checked first. The step is
`rounds/r60s-screens.ps1 -Step big3c` at `9b1ac255`. It ran on the binaries that ran big3, and nothing under src/
had changed. The check, r60s-big3r-s1, resumed big3's 5,000 s checkpoint alone on the card and ran to 10,000 s. That is 5,000 s of a big
world with 2,000 to 3,200 bodies alive, against the 500 s of a small one in the check above.

Every row after 5,000 s was the unbroken run's:

| file | rows after 5,000 s, all the same |
|---|---|
| lineage | 20,106 |
| stats | 500 |
| digest, one a second | 5,000 |
| absorptive | 4,403 |
| positions | 500 |

The two 10,000 s checkpoints hold one state, read side by side by `scripts/reads/ckpt-compare.py`. Every header
field was equal except the name of the run that wrote it. The world's payload was equal by length (76,886,356 bytes), by
the digest the header carries, and byte for byte. The files differ by one byte, in that name. The resume took
29.3 min, at 2.85 times real time; the card peaked at 52 °C and the processor at 74 °C.

The step then resumed big3's own 10,000 s checkpoint to 20,000 s, and that run met every condition too. The note
above still holds: no world tens of thousands of seconds old has been resumed on the card. This one was 10,000 s
old.

The next step founded r60s-big3m on the card, and card-run failed. run-farm waits 60 s for run.json and then
returns either way. This world, at 88,000 m², took 75 s to write it. card-run read the configHash at once and found
none. It called stop-arm, which threw for want of a run.json, so card-run ended without a STOPPED line. The farm
ran on with no heat stop and no end checks until I stopped it with stop-arm at 860 s. `a096730a` makes card-run
wait up to 5 minutes for run.json before it reads the hash, as its preflight already does. A stop-arm that fails
is now logged, and the run still ends STOPPED. The relaunch founded and pinned as it should.
