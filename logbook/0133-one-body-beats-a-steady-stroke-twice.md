# One body's brain beats a steady stroke twice, and the rest does not repeat

*2026-10-03. Written by the agent (Opus 5.5, in the session that ran the replicate) after the replicate's
exam. Its sources are the two exams' tables in `logbook/specs/nursery-plan-joint-drive.md` ("Brain v2's
exam" and "The replicate's exam"), made by `scripts/reads/nursery-exam-per-body.py`, and `HANDOFF.md`
item 10a. Every number below is from those tables.*

## What happened

I ran the first evolved brain that beats a steady stroke a second time, on a new seed, and one result came
back. Body 10's brain beat the best fixed stroke on its body in both runs. Nothing else did. A second body
cleared the same bar in the first run and failed it badly in the second. The senses each brain relied on
changed from one run to the next, and in each run one body of six ended on a brain worse than any steady
stroke.

## The nursery, and the bar a brain has to clear

The nursery is a small tank beside the full world. Six bodies are fixed there, and only their brains
evolve. It exists to test cheaply, before a round spends days on it, whether a brain can learn to use what
it senses. A round in the full world mixes that question with growth, breeding and food
chains, and a nursery run takes about 13 hours.

A body can swim without sensing anything. Every joint pushes at a fixed power, a ***constant***, and some
constant is best for each body. So a brain earns its place only if it beats its own body's best constant.
D145 set the bar at two standard errors. The test that measures it swims each body's best brain on 32
held-out draws of water the search never swam, and it is called the ***exam***. It also swims each brain with one sense cut at a time, a ***knockout***. A knockout
that costs a brain energy says the brain reads that sense.

The brain here is brain v2 (D146, D147). It is one network per body, wired to all 13 senses from the
start, and bred by crossover as well as mutation. The old brain it replaced cleared the bar on none of the
six bodies, and five of its six were blind.

## The pilot, and the replicate

The pilot ran on 2026-10-02 and cleared the bar on two bodies. Body 0 beat its best constant by 2.4
standard errors and body 10 by 2.2. Body 0's brain read the flow of the water, and body 10's read depth. Body
8's search ended on a brain that barely moved, 175 J below its best constant.

Six bodies tested at two standard errors will clear the bar about once in eight times by luck alone. So
the owner agreed to a replicate: the same six bodies and the same exam draws, with a new seed for the
search. It ran from 15:57 that afternoon to 05:34 the next morning. Every constant scored the same in both
exams to the hundredth of a joule, so the draws matched and the two runs could be paired body by body.

| Body | Pilot: own minus best constant | Replicate: own minus best constant |
|---|---|---|
| 0 | +83 ± 34 J (2.4) | -252 ± 57 J (-4.4) |
| 1 | +30 ± 32 J (0.9) | +59 ± 36 J (1.6) |
| 3 | -9 ± 35 J | -22 ± 43 J |
| 4 | +22 ± 45 J | -30 ± 58 J |
| 8 | -175 ± 52 J (-3.4) | +42 ± 49 J (0.9) |
| 10 | +81 ± 38 J (2.2) | +90 ± 24 J (3.7) |

Body 10 cleared it again, by more. Body 0 went from clearing it to being the replicate's stuck body, the
one whose brain barely moves. Body 8, stuck in the pilot, was ordinary here. The scent went unread on every
body in both runs.

The senses did not repeat. In the pilot, body 10's brain lost 92 J when its depth sense was cut. In the
replicate, cutting depth cost nothing measurable, and the brain leaned on its drive sense instead. It also
did 69 J better without its chemical sense.

## What I take from it

This is my reading, and it is inference.

The one brain can beat a steady stroke, and body 10 shows it is not luck. Which body gets there, and by
which sense, depends on the path the search happened to take as well as on the body and the water. So one
run per setting tells us what one brain did. What the setup does takes several.

That bears on the two separation arms queued next. Each changes one thing from the pilot: one drops the
drive sense, the other drops crossover. A single run of either cannot be read body by body against the
pilot, because the seed alone moved one body by 5.8 standard errors. It can be read on the pooled score of
the six bodies' brains, which moved 24 J between the two runs, and on how many bodies clear the bar.

The stuck body is a question of its own. Twice the search ended one body in six on a brain worse than
standing at a fixed stroke, and I do not know why it kept that brain.

## Two things I misread on the way

On the morning the exam ended I read the log's pooled lines, one per control over all six bodies. From
them I told the owner that no single sense mattered in the replicate. The per-body table says otherwise. Pooling had
added a body that gains from a knockout to one that loses from it and reported nothing. Before that I had
called the exam's "brainless" control a body with no brain. It is the evolved brain with every sense
disconnected, so it differs from run to run, as it should. The tables were on disk the whole time. I
should have run the script before answering.
