# 0059 — The newborn's spin

**2026-09-04**  ·  a post-mortem, not a round

The first physics divergence in a scored run was a two-second-old newborn with links the
weight of a coin. At the 0.02 step, one integration step spun a joint by thousands of
radians per second, and everything downstream of it was NaN. Two repairs came out of the
post-mortem. A divergence is now a counted death with a dump beside it, and above 0.01 a
limiter caps how much one step can change a joint's spin.

The owner's instruction was short.

> fix the bug(s)

## What happened

The arm `r20q-s1` of logbook/0056 ran at dt 0.02, on seed 1, under the shuffled conception
order. It died at t=15,345, when PhysX refused `{NaN, NaN, NaN}` forces for all three parts
of one creature nine steps running.

Then `World.Observe` refused the creature's non-finite height and the run threw. The
manifest, new that morning, recorded `status error` with the exception, which made it the
first run to write its own crash. It also recorded zeros for the footer's facts, which the
error path did not carry.

## The replay

The divergence build replayed the arm's exact configuration on the same build lineage, as
`r20q-s1-replay`. PhysX replays bit for bit on this machine (0052), and all 154 rows to
t=15,300 were byte-identical.

At physics step 767,266 the new check fired on the same creature, id 3075. It wrote its
dump, killed the creature as a counted death, and the run went on to 20,000 s with the audit
at 0.0000% and no second divergence.

The dump holds three parts and four degrees of freedom. There is a photosynthetic root of
1.6 kg, and two identical universal-jointed links of 0.143 kg and 0.000143 m³, at `Power`
8.2 N·m each. The creature was generation 41 and two seconds old, so it blew up within a
hundred physics steps of birth.

The drive torques on its last step were ordinary, at about 1.5 N·m per axis. Its last finite
state was already gone: speeds of 10¹¹–10¹² m/s, spin rates of 10¹³ rad/s, and all three
parts at one coordinate ten million kilometres away. Float resolution there is a kilometre.
The step-old copy was far too shallow to see the onset.

The arithmetic says where the onset was. A link with the smallest principal inertia of order
10⁻⁵ kg·m² under 1.5 N·m accelerates at about 10⁵ rad/s². At dt 0.02 that is a change of
about 3,000 rad/s in one step, or five hundred revolutions per second. One integration step
does it, on a body the drag model then has to stop.

Nothing in `Evosim.Sim` set a joint velocity cap, and `ArticulationBody.maxJointVelocity`
defaults to unbounded. At dt 0.01 the same link takes half the kick per step and the drag
limiter never engages, and this has never been seen at 0.01. The fault is the fast step's,
on a newborn's tiny links.

## The two repairs

The first repair makes a divergence a death rather than a crash.

Once per metabolic step, before `Observe`, each creature's root position is read. A
non-finite one is dumped to `runs/<arm>/<run>/diverged/<id>.json`, carrying the genome, the
last finite root position, and the velocities and applied torques as they were.

The creature is then killed through the same path as starvation, with `DeathCause.Diverged`,
so tissue, matter and the lineage row are all accounted and the audit closes.

The report's `diverged` column and the manifest's `divergedTotal` count it.

The cost was measured at 0.6% of wall clock on a 5,000-creature world, and the error
manifest now carries the last known facts.

The second repair is the drive impulse limiter. It runs at steps above 0.01 only, with the
drag limiter's precedent and gating. It caps each driven degree of freedom's torque, so that
one step cannot change a link's spin by more than 30 rad/s. Every bind is counted as
`driveImpulsesLimited` in the footer and the manifest.

The setting `maxJointVelocity` is left alone, because PhysX's own cap would be uncountable.
The step 0.01 replays bit for bit, and the un-limited branch is the original expression to
the character. A `float` local in the same path was enough to change Mono's rounding and
break identity. The validation arm caught that at t=200.

The second replay, with the limiter, ran the same configuration as `r20q-s1-replay2`. Its
rows are byte-identical to the original for 71 samples, and the first difference falls at
t=7,100, when the cap first binds. There was no divergence in 20,000 s, the audit closed,
1,819 were alive at the end, and the cap bound 173,099 times in a million steps.

Two things weigh against each other in that. Creature 3075 never exists in the second
replay, so this does not demonstrate that the cap saved that body, only that the seed and
settings now complete without one.

And 173,099 binds is a great deal of intervention. At 0.02 the cap is actively reshaping
what evolved muscle can do, so the fast step under-drives joints relative to the confirming
step. The best speed reads 0.42 m/s against the first replay's 1.30, which was plausibly a
body on its way to blowing up.

For jointless worlds nothing changes. For any question about swimming, 0.02 is not the same
physics, and 0052's rule that swimming is read at 0.01 only is now load-bearing rather than
cautious.

## Where it is

| file | what is in it |
|---|---|
| `unity/Assets/Evosim/Sim/Ecosystem.cs` | `CheckFinite`, `HandleDivergence` |
| `EffectorDriver.cs` | `MaxJointAngularVelocity`, `ImpulsesLimited` |
| `src/Evosim.Core/Ecosystem/World.cs` | `Bury`, `KillDiverged`, `Diverged` |
| `Organism.cs` | `DeathCause.Diverged` |
| `DivergenceTests.cs` | — |

The commit is `1673dce`.
