# Proposal: the drive impulse limiter at every step

**For the owner, 2026-09-11.** A harness change, not a world rule, but it touches the physics
loop at the screening step and the confirming step alike, so it is the owner's to rule. It is
queued in HANDOFF's path behind round 34's read (logbook/0090) and ahead of any round that
makes a stroke cheap.

## What exists

`EffectorDriver` caps each degree of freedom's drive torque so that one physics step cannot
add more than 30 rad/s of joint angular velocity, about five revolutions a second, faster
than anything a creature has ever done here. The cap is a torque cap computed from the
link's inertia, never the solver's own velocity clamp, so every bind is counted
(`driveImpulsesLimited`, printed per run and written to the manifest). It was built after
`r20q-s1`, where a 143-gram link was driven at 1.5 N·m and reached 4e13 rad/s in two seconds
(logbook/0059).

It engages only at steps coarser than 0.01. The reason was replay: every published number was
measured at 0.01, and a limiter that engaged there would have made a different world of every
historical run under its own config hash. So at 0.01, the confirming step, every drive torque
is applied as computed, and round 34's manifests all read `driveImpulsesLimited: 0`.

## What round 34 showed

With every price at zero the stroke is driven flat out: 5 to 77 W per jointed body against a
leaf's 4 W of income, the body moving faster than the water. Fifty diverged bodies across
the five seeds, and every one whose dump was read carries an active joint; their median age
is about 1,200 s, so they are jointed adults thrown out of the world by the solver, about two
percent of jointed births. That is the harness removing a body the world had not selected
against, and 0080's M3 failed on it in three seeds of five.

The rounds ahead make the stroke cheap on purpose (36 the link earns, 39 the stroke priced
alone with everything else free, 44 the remaining prices), so the leak grows with the
programme unless the limiter is there.

## The change

A tunable rather than a constant, so that the record stays readable in its own terms:

- `RunConfig.DriveLimitAtEveryStep` (`EVOSIM_DRIVE_LIMIT_ALWAYS`, default false). At false
  the driver keeps today's rule, gated to steps above 0.01, and every recorded run replays
  under its own config. At true the 30 rad/s cap applies at every step, binds counted as
  now. The header prints `driveLimit always` or `driveLimit >0.01`.
- The 30 rad/s stays a constant. It is a physical bound, not a knob, and a second dial here
  would invite tuning the solver rather than the world.

It is one tunable, so it moves `configHash` and, per the refuse-rather-than-default rule,
every earlier `config.json` becomes unreadable by the build. That is already the cost of the
box build (`fable-propose-box.md`, `PatchesAcross`), which lands between rounds 36 and 37, so
the two should land together and break the record's configs once rather than twice. The
driver edit moves `simHash` too; the box build already does.

## What it costs, and the check before it is trusted

At every step the cap is a per-DOF comparison against a torque budget read from the link's
inertia. At 0.02 it has been running for a month with a bind count of the order of 1e5 per
run; at 0.01 the budget per step halves and the count will be higher. The question the owner
should want answered before it goes into a scored round is whether it binds on a body that
was not about to diverge, which would change gaits rather than remove impossible torques.
The check is cheap: replay round 34 seed 5 (the seed with the most jointed bodies) at 0.01
with the limiter on, a new realisation of that seed, and read `driveImpulsesLimited` against
the jointed count, `diverged` against 17, and `spd jnt` against the recorded 0.09 to 0.10.
If the binds are a small number per jointed body per second and the divergences fall, the
limiter removes what it was built to remove. If the binds are of the order of the drive
rate itself, the cap is a gait and the proposal should not be adopted as it stands.

## Two ways to run it, and a recommendation

1. **On from round 37.** Every round from the box on shares it. Round 37's comparison to
   round 35 carries the limiter as a second difference. In a priced world almost no body is
   jointed, so in practice the confound is nil, and there is one rule for the rest of the
   programme.
2. **On only for the cheap-stroke rounds** (36's successors, 39, 44), off elsewhere. Each
   cheap-stroke round is then read against a base without the limiter, and a jointed body
   that diverges in the base and not in the arm is a difference the limiter made.

The agent recommends the first, after the check above passes. The limiter removes torques no
creature could apply. A base that keeps them is not a fairer base; it is a base with a leak
in it that only shows when the joint is cheap. And the reads that matter in the cheap-stroke
rounds are the jointed count and the stroke's use, which the second way would confound with
the limiter's presence anyway.

## What it does not do

It does not price the stroke, throttle it below 30 rad/s, or touch the first-minute deaths
of jointed newborns, which 0090 reads as the same stroke below the solver's threshold. Those
are round 39's, and a limiter that stopped them would be doing the world's selecting for it.

## Standing answers

The box proposal's standing answers apply. The change is a harness change with a config
tunable, defaults preserve every recorded world, and the header carries it. The Core test
that checks every tunable reaches the hash and survives a reload will catch a missed wire.
