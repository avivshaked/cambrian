# 0059 — The newborn's spin

*2026-09-04. Not a round: the post-mortem of the first physics divergence in a scored
run (`r20q-s1`, logbook/0056), and the two repairs it produced. Owner: "fix the bug(s)".*

## What happened

`r20q-s1` (dt 0.02, seed 1, shuffled conception order) died at t=15,345 when PhysX
refused `{NaN, NaN, NaN}` forces for all three parts of one creature nine steps running,
then `World.Observe` refused its non-finite height and the run threw. The manifest, new
that morning, recorded `status error` with the exception — the first run to write its
own crash — but zeros for the footer's facts, which the error path did not carry.

## The replay

The divergence build replayed the arm's exact configuration on the same build lineage
(`r20q-s1-replay`). PhysX replays bit for bit on this machine (0052): **all 154 rows to
t=15,300 were byte-identical**, and at physics step 767,266 the new check fired on the
same creature, id 3075, wrote its dump, killed it as a counted death, and the run went on
to 20,000 s with the audit at 0.0000% and no second divergence.

**The dump.** Three parts, four degrees of freedom: a photosynthetic root of 1.6 kg and
two identical universal-jointed links of 0.143 kg and 0.000143 m³, `Power` 8.2 N·m each.
Generation 41. **Age two seconds** — it blew up within a hundred physics steps of birth.
The drive torques on its last step were ordinary, about 1.5 N·m per axis. Its last
finite state was already gone: speeds of 10¹¹–10¹² m/s, spin rates of 10¹³ rad/s, all
three parts at the same coordinate ten million kilometres away because float resolution
there is a kilometre. The step-old copy was far too shallow to see the onset.

The arithmetic says where the onset was. A link with the smallest principal inertia of
order 10⁻⁵ kg·m² under 1.5 N·m accelerates at ~10⁵ rad/s², which at dt 0.02 is a change
of ~3,000 rad/s in one step — five hundred revolutions per second, from one integration
step, on a body the drag model then has to stop. Nothing in `Evosim.Sim` set a joint
velocity cap; `ArticulationBody.maxJointVelocity` defaults to unbounded. At dt 0.01 the
same link takes half the kick per step and the drag limiter never engages; the record
has never seen this at 0.01. The fault is the fast step's, on a newborn's tiny links.

## The two repairs

1. **Divergence is a death, not a crash.** Once per metabolic step, before `Observe`,
   each creature's root position is read; a non-finite one is dumped
   (`runs/<arm>/<run>/diverged/<id>.json`: genome, last finite root position, the
   velocities and applied torques as they were) and the creature is killed through the
   same path as starvation with `DeathCause.Diverged`, so tissue, matter and the lineage
   row are all accounted and the audit closes. The report's `diverged` column and the
   manifest's `divergedTotal` count it. Cost measured at +0.6% wall clock on a
   5,000-creature world. The error manifest now carries the last known facts.
2. **The drive impulse limiter.** At steps above 0.01 only, with the drag limiter's
   precedent and gating, each driven degree of freedom's torque is capped so that one
   step cannot change a link's spin by more than 30 rad/s; every bind is counted as
   `driveImpulsesLimited` in the footer and the manifest. `maxJointVelocity` is left
   alone because PhysX's own cap would be uncountable. 0.01 replays bit for bit — the
   un-limited branch is the original expression to the character, because a `float`
   local in the same path was enough to change Mono's rounding and break identity
   (caught by the validation arm at t=200).

**The second replay**, with the limiter, on the same configuration (`r20q-s1-replay2`):
rows byte-identical to the original for 71 samples, first difference at t=7,100 when
the cap first binds; **no divergence in 20,000 s**, audit closed, 1,819 alive at the
end; the cap bound 173,099 times in a million steps. Two things to weigh from that.
Creature 3075 never exists in the second replay, so this is not a demonstration that the
cap saved *that* body, only that the seed and settings now complete without one. And
173,099 binds is a lot of intervention: **at 0.02 the cap is actively reshaping what
evolved muscle can do**, so the fast step under-drives joints relative to the confirming
step (best speed 0.42 m/s against the first replay's 1.30, which was plausibly a body on
its way to blowing up). For jointless worlds nothing changes; for any question about
swimming, 0.02 is not the same physics, and 0052's rule that swimming is read at 0.01
only is now load-bearing rather than cautious.

## Where it is

`unity/Assets/Evosim/Sim/Ecosystem.cs` (`CheckFinite`, `HandleDivergence`),
`EffectorDriver.cs` (`MaxJointAngularVelocity`, `ImpulsesLimited`),
`src/Evosim.Core/Ecosystem/World.cs` (`Bury`, `KillDiverged`, `Diverged`),
`Organism.cs` (`DeathCause.Diverged`), `DivergenceTests.cs`. Commit `1673dce`.
