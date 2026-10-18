# Foraging bench notes (propose-foraging.md), 2026-09-29, night

Working notes while the bench runs; the bench's entry is written from them.

## Items 1 and 2: the push itself (LimbPushTests)

- A link swinging across its axis at 2 rad/s, C_T 0.3, added-mass coefficient 0.5, volume
  0.016 m3, reach 0.2 m: the push is -1.92 N along the link's axis toward the joint, as the
  rule's arithmetic says (0.3 x 8 kg x 0.2 m x 4 rad2/s2).
- A twist about the link's own axis, a whole body turning as one, a rigid body and a link above
  the waterline: no push.
- A two-part chain wagging at 1 Hz for 20 s: 1.17 m toward its head with the push, 0.04 m
  without. It drifted 0.35 m sideways with the push.

## Item 7: a hand-built forager (HandForagerBench)

A trunk (0.3 x 0.2 x 0.4 m) with a nose, and two tails (0.3 x 0.1 x 0.03 m) at its rear corners,
hinged, power 10, +-0.6 rad. Still water, no weight, one corpse of 100 J 8 m away, C_T 0.3.

- First cut, each tail stroking k(x0 - s_tail): the difference across 16 cm was about 2% of
  the stroke and the body never turned. With the corpse ahead it slowed through it and swam
  on; off its heading it swam past.
- Second cut, tail against trunk: each tail strokes a - k(s_tail - s_trunk), a = 0.8, k = 50,
  trunk half-width 0.2 m. From 45 and 90 degrees off its heading it turned and came within
  2.2 and 2.5 m, then circled 5 m out. The blind control (scent weights 0) never came within
  5.7 and 8 m and ended 47 to 52 m away.
- Third cut, with Braitenberg's slowing, - m s_trunk: at m = 0.6 it closed to 1.5 m and
  circled within 2.3 m to the end (900 s). At m = 0.9 it came within 0.3 and 0.5 m, then its
  stroke's sign flipped and it coasted out. The push goes as the swing squared, so a reversed
  stroke pushes as hard as a forward one: a body stops only at an amplitude near zero.
- A corpse dead behind it is never found: two noses side by side cannot tell behind from
  ahead. A corpse dead ahead is passed through.
- Speed: 5.7 cm/s at C_T 0.3 with the wide trunk, 11 cm/s with the narrow one (0.2 m wide,
  first cut). The owner's 4 to 8 cm/s is inside that range.
- Cost: the body weighs 38.7 kg and its joints do 63 to 182 W of work (the sum of |tau . omega|
  over its two hinges), where the proposal estimated about 2 W for a smaller tail. The drive law
  at power 10 works against its own damper and the limits, and the absolute sum counts all of
  it. So rule 2's multiplier cannot be set from the proposal's arithmetic: it waits for the
  probe's measured work on round 52's real bodies (item 6).

## Item 3: speed against the stroke (LimbPushTests.AFasterStrokeSwimsFaster)

The two-part chain (a 0.4 m cube's root, a 0.4 x 0.1 x 0.4 m link, power 10, +-1.2 rad), C_T 0.3,
commanded at four frequencies for 20 s:

| command | rms swing rate | forward measure | with the push | without |
|---|---|---|---|---|
| 0.25 Hz | 2.01 rad/s | 3.17 rad2/s2 | 3.1 cm/s | -1.4 cm/s |
| 0.5 Hz | 2.87 rad/s | 6.60 rad2/s2 | 7.4 cm/s | -0.3 cm/s |
| 1 Hz | 1.85 rad/s | 2.61 rad2/s2 | 5.8 cm/s | 0.2 cm/s |
| 2 Hz | 0.85 rad/s | 0.44 rad2/s2 | 2.0 cm/s | 0.1 cm/s |

The forward measure is the mean of the swing rate squared times the cosine of the link's angle
off the body's axis, which is what the rule's forward thrust goes as. The command's frequency is
not the stroke: the drive's power caps the swing, so the 2 Hz command swings least, and the slow
command holds the link at its limits, where the push points sideways. The strongest measure swims
fastest and the weakest slowest; between them the 0.25 Hz stroke is slower than its measure says,
and drag alone drives it backward. The owner's expectation that a faster swing makes a faster swimmer holds in the swing
the joint reaches, not in the command.

## Items 2 and 4, the rest (LimbPushTests, HandForagerBench), 2026-09-29 late

- A link swung through a whole turn about its hinge: 1.92 N at each angle, and 2.9e-14 N summed
  over 360 angles. No net push.
- The fins: the forager's trunk stroking blind for 300 s. With a mirrored pair it turned 0
  degrees and moved 17.1 m. With one side fin it turned 1,790 degrees and moved 0.21 m: it spins in
  place.
- A lone tail's stroke is not symmetric on this drive. The joint is driven by a torque with
  nothing centring it, and at power 10 the link rails to one side at every start phase (16
  tried). It strokes between the axis and a limit, with a mean angle of 0.58 to 0.67 rad, and the
  push's mean sideways part is 63 to 77% of its forward part. The proposal's "under 5% for a
  symmetric wag" is unanswerable on this drive because no wag is symmetric. So in round 54 a
  one-tailed body swims in a curve and a straight course needs a mirrored pair; the drive is
  a lever of its own (a centring spring, or a position target) and is not part of the proposal.
