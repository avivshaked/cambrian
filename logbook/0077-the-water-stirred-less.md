# 0077 — The water stirred less

**2026-09-08**  ·  round 31, pre-registered before launch; one number changes

Round 30 (logbook/0075) met the goal rule in every seed and kept no swimmer. The one
jointed line that lasted fed as well as the sitters and moved no faster than the drift.
Logbook/0076 asked the field why and measured the answer. At the world's mixing rate of
0.2 m²/s the hole a sitting mouth eats is refilled in about five seconds, faster than the
mouth drinks. A mover and a sitter therefore eat the same. At a tenth of the rate a swimmer eats
two and a half times what a sitter eats, a corpse is a patch for a minute, and the larder
stays near the producers that made it. The owner ruled (D085) that the water is stirred
less and nothing else changes. This is that round.

## What is held

Everything in round 30's launcher. The vertex world with its two kernels, merge distance,
cap and quantum. D082's prices: neuron 0.005 W, connection 0.001 W, work at a quarter.
Round 28's contact world with the three senses on, added mass 0.5, the global brain
retired, single-threaded physics. 30,000 s, five seeds. The matter field's own mixing stays
at 2 m²/s: the conception gate is not the prize under test. Remineralisation stays off,
because 0076 measured that nothing reaches the floor in a lifetime at either rate.

## What changes

`EVOSIM_MIXING 0.02` and `EVOSIM_H_MIXING 0.02`, from 0.2. This is the detritus field's
random walk, vertical and sideways, and D084's rule that the two are equal holds at the new
value. The step per half second goes from 0.45 m to 0.14 m, and the time a 1 m hole takes
to refill from about 5 s to about 50 s. Nothing in Core moves; the launcher is
`scratch/launch-r31.ps1`.

The cost stated before the reading. Less stirring also means a stomach's own water is
refilled more slowly from the field at large. A stomach line that lived on the mixing
carrying food to it may thin; 0076 measured a sitter's income tripled by the stirring. That is
the price of a world in which sitting still costs something, and M0 reads whether the
world can pay it.

## Sequencing

Five seeds at dt 0.02 for 10,000 s on workers 2 to 6 first (`r31q-s1` to `r31q-s5`). They
are read within the step and only for whether the world stands, the identities close, a
stomach line forms and the loop closes. Nothing about joints, swimming or depth is read from them.
If the screen does not fail the world, the round runs at 0.01 for 30,000 s on the same
workers (`r31-s1` to `r31-s5`), and that is the round this entry scores.

## Validity checks

- **V1 header.** Every token of round 30's, with `mixing 0.02 m2/s` and `h-mix 0.02 m2/s`.
  Every token is read from the report and never from the launch command.
- **V2 identities.** `audit` within a joule, `mat resid` 0 to the rounding, `mat short` 0,
  `vtx` below the cap, `verticesMerged` 0.
- **V3 manifests.** `ended budget`, `diverged 0`, `physicsJobWorkers 0`, `gitDirty false`,
  one `coreHash`.

## Predictions

The goal is D063's three seeds of five. The reference is the base world's own count, which
is round 30's five.

| # | prediction | falsified by |
|---|---|---|
| M0 | **the world stands with its water stirred less**: `alive` at 30,000 s within 0.5 to 1.5 of round 30's same seed in every arm, and D063 as amended holds in at least 3 of 5 | `alive`, the scorer |
| M1 | **a still stomach eats a hole**: `food rig` over the last 6,000 s below round 30's same seed by 30% or more in at least 3 of 5, with the layer mean `J/m3 here` not lower by as much, since the hole is at the body and not in the water at large | `food rig`, `J/m3 here` |
| M2 | **a jointed guild persists**: `jnt inh` at least 10 at every sample over the last 6,000 s in at least 2 of 5 (round 30 read 0 in every arm at the end; seed 4's line of 14 lasted to 10,000 s) | `jnt inh` |
| M3 | **movement pays where it exists**: in every arm with a jointed guild at the end, `food jnt` exceeds `food rig` by more than 20% over the last 6,000 s, with `spd jnt` above `spd rig` by 50% or more (0076: a mover at the drift speed gains 12 to 26%, a swimmer 2.2 to 2.5 times) | `food jnt`, `food rig`, `spd jnt`, `spd rig` |
| M4 | **the loop still closes**: `det out` within 0.5 to 2 of `det exuded` per window over the last 6,000 s in every seed that passes M0, and `detritus J` at the end below three times round 30's same seed | `det out`, `det exuded`, `detritus J` |
| M5 | **the larder stays shallow**: `det deep` (the deep layer's edible density) at the end below round 30's same seed in at least 4 of 5, and `% on floor` below 5% in every arm (0076's column profile: 86% in the top 20 m at 0.02, nothing on the floor) | `det deep`, `% on floor` |
| M6 | **the identities close**: `audit` 0.0000%, `mat resid` 0, `mat short` 0 at every sample; neither impulse limiter binds; `vtx` below the cap | `audit`, `mat resid`, `mat short`, the manifests |

## The two-sided readings

- **M0 fails:** the stomach lines lived on the stirring, and a world in which sitting costs
  cannot feed a sitter. The exudate fraction and the mixing rate between 0.02 and 0.2 are
  the levers, both the owner's; the first arm is 0.05, halfway on a log scale.
- **M0 holds, M1 fails:** the hole is not there in the world though it is in the
  experiment; the producers beside every stomach refill it. Read `food rig` against
  `J/m3 here` per seed, then the exudate fraction is the lever (D085 item 3).
- **M1 holds, M2 fails:** the hole exists and no swimmer lives in it. The prize is real and
  unreachable at this stroke, so the question is the drive. Read `spd jnt` in the early
  samples for whether any line moved faster than the drift; the stroke's cost and the
  added mass are next, and they are the owner's.
- **M2 holds, M3 fails:** swimmers persist without eating better, so a jointed body pays
  for something else; `dep jnt` against `dep rig`, then the active-versus-clamped assay.
- **M1 to M3 hold:** movement pays. The owner words the movement clause of the goal rule,
  and predation on contact is next (D081's order).
- **M5 fails:** the food went deep after all and 0076's one-lifetime profile did not hold
  over ten; remineralisation is turned on in the round after, and D085's withdrawal of it
  is reversed in writing.
- **M6 fails:** the round is censored and the fault fixed before anything is read.

## What the round does not ask

Whether a sense is used. Round 30 carried one in one seed of five at the new price, and a
sense that reads a gradient is worth carrying only where following it buys something; that
question waits for a world in which M1 holds. Nor whether exudate should found vertices
(D085 item 4), which is orthogonal to the prize and queued behind this round.

## Launch

*2026-09-08, evening.* The screen: `r31q-s1` to `r31q-s5` on workers 2 to 6 at dt 0.02 for
10,000 s, every header reading `mixing 0.02 m2/s`, `h-mix 0.02 m2/s` and `work x0.25`,
every manifest `physicsJobWorkers 0` and `gitDirty false` on one `coreHash 73047e8e…`. That
hash differs from round 30's because a remark was added to the mixing knob in
`RunConfig.cs` after round 30 launched; no code moved, and the `simHash` is round 30's.
The launcher refused any other `simHash`. Seeds 1 to 3 record commit `e6449b7` and 4 and 5
`7729d2c`, one prose commit apart. The machine holds the five screens and nothing else.

## The screen

*2026-09-08, night.* All five ended on budget at 10,000 s in about an hour each. Read
against the sequencing section and nothing else.

| arm | alive (the screen at 0.2) | inherited stomachs (at 0.2) | scorer | detritus out over exuded, last 4,000 s | detritus standing, kJ (at 0.2) |
|---|---|---|---|---|---|
| `r31q-s1` | 1,524 (1,568) | 46 (56) | fail, stability | 1.26 | 10.5 (5.9) |
| `r31q-s2` | 1,570 (1,494) | 33 (78), a line born at 7,800 s | fail, late | 0.15 | 276 (6.6) |
| `r31q-s3` | 1,446 (1,383) | 143 (66) | pass, a clade of 112 | 1.06 | 4.7 (4.7) |
| `r31q-s4` | 1,507 (1,389) | 198 (24) | pass, a clade of 18 | 1.07 | 6.4 (8.8) |
| `r31q-s5` | 1,428 (1,697) | 90 (3) | pass, a clade of 83 | 1.07 | 6.2 (30) |

The world stands, every arm within 0.85 to 1.13 of round 30's screen. The identities close:
`mat resid` 0, `mat short` 0, `audit` 0.0000%, `diverged` 0 and nothing merged in every
arm. A stomach line formed in every seed and the scorer passes three, as the screen at 0.2
did; the lines in seeds 3 to 5 are two to thirty times that screen's. The loop closes in
four seeds. Seed 2 is the slow case: its exudate piled to 276 kJ before a line appeared at
7,800 s, and seeds 1 and 3 say what follows, since each ate a pile of 30 kJ down within a
lifetime once its line formed. The stated cost is visible and paid: the detritus field
holds 4,300 to 6,700 vertices where the 0.2 screen held 9,000 to 10,000, and the water
at a rigid body's mouth reads 2.8 to 4.9 J/m³ in the closed-loop seeds where the 0.2
screen read 3.2 to 12.5, with the layer means lower still. That is the hole, seen at the
fast step and read at 0.01. Nothing about joints is read here: `jnt inh` peaked at 27 in
seed 5 at 1,900 s and 0 stood at the end in every arm. The screen does not fail the world.
The round launches at 0.01.

## Launch

*2026-09-08, night.* `r31-s1` to `r31-s5` on workers 2 to 6 at dt 0.01 for 30,000 s, wall
1,200 min, every manifest reading `coreHash 73047e8e…`, `simHash d928e2a5…`,
`configHash d2d31a9b…`, `physicsJobWorkers 0` and `gitDirty false`; seed 1 records commit
`381b851` and the rest `a2ef9c1`, prose commits apart on the same code. The launcher
refused any other `simHash`. Every header reads `mixing 0.02 m2/s`, `h-mix 0.02 m2/s` and
`work x0.25`. The machine holds the five arms and nothing else. Read as they land, against
the predictions and the scorer.

## Seed 3 fell

*2026-09-08, evening.* `r31-s3` ended with `status error` at 11,533.5 s, on worker 4, with a
world of 1,468 and an inherited stomach line of 213. Its manifest counts one diverged body
and its `diverged/` directory holds five dumps. Creature 2636, a newborn of two parts on a
twist hinge, went non-finite at 10,948 s and was killed as a death. At 11,533.5 s four
bodies went in one step. One was a second newborn of two parts. Three were single-part
producers within a metre of it at x ≈ 10, y ≈ −2.3, two of them 2,400 and 2,600 s old.
Three of the four were caught at NaN and dumped. The fourth was not, because its root was
finite. The solver had thrown it to a height of the order of 10³¹ m, which passes a test
for NaN and infinity. The light field's layer index is an integer cast of the depth, and
the cast overflows there. `LightField.Contribute` threw an `ArgumentOutOfRangeException` from inside
`World.Metabolise` and the arm was censored where a NaN body would have been one more counted
death.

The divergence itself is the known kind: a newborn jointed body beside others at dt 0.01
(logbook/0059's class), and this entry does not explain it. What was wrong was the guard.
A body outside the sea is as diverged as a body at NaN, and nothing said so.

The fix is one bound in two places. A method on the world says whether a height is one the
sea could hold. Above, the bound is the world's depth past the surface, since D050 stops
upward net force at y = 0 and a body above it is coasting. Below, it is twice the depth
past the floor, which has been a collider since D077's bed. The harness's check kills at
that bound as the same counted death and dumps the body first. Core's observation refuses
at it and names the creature and the height, so the guard behind the check stays a guard.
NaN fails both comparisons, so the old case is inside the new one. A regression test in
`ObservationTests` refuses the height that fell seed 3. It accepts round 5b's sinkers at
−131 m in a 60 m world (logbook/0040). The suite is 556. Worker 7 was refreshed, and a 300 s
screen at dt 0.02 compiled and ended on budget with the round's header.

The round is now split across two builds, the way round 24 was (logbook/0061). Seeds 1, 2,
4 and 5 continue on `coreHash 73047e8e…` and `simHash d928e2a5…`; they carry the old check,
and a body of this kind would censor any of them. Seed 3 reruns as `r31-s3b` on worker 7,
launched 2026-09-08 at 18:30 on `coreHash d0f27c7b…`, `simHash b65aad0b…`, commit `b6b198e`,
`gitDirty false`, the same `configHash d2d31a9b…`; the launcher refused any other `simHash`.
The change is not a per-step term. A body the new check kills is one the old world crashed
on, so up to that instant the two builds are one realisation. The rerun is expected to
retrace the censored arm to 11,533.5 s and continue past it, and its report says whether it
did. The censored arm stays on disk with its dumps and is not scored. The machine holds five
arms.

## Results

*2026-09-09, morning.* All five ended on budget at 30,000 s in 9 to 12 hours of wall
clock, `physicsJobWorkers 0`, no impulse limiter bound. Seeds 1, 2, 4 and 5 on
`coreHash 73047e8e…`; seed 3 is `r31-s3b` on the widened guard, `coreHash d0f27c7b…`, with
its seven counted divergences at 11,533.5 s and nothing after. V1 to V3 hold, with that one
caveat on seed 3. Read against round 30's same seed, `scratch/r31-read.py`.

| arm | alive (r30) | scorer | stomach clade at end (r30 `inherit`) | sense share | `food rig` last 6,000 s (r30) | `J/m3 here` (r30) | `det deep` (r30) | floor max | detritus kJ (r30) |
|---|---|---|---|---|---|---|---|---|---|
| `r31-s1` | 1,757 (1,743) | fail, stability 5 | 20 (38) | 1% | 42.0 (6.2) | 5.98 (3.11) | 2.59 (0.72) | 3.6% | 23.1 (7.5) |
| `r31-s2` | 1,824 (1,727) | pass, 132 | 132 (82) | 26% | 5.4 (4.2) | 0.54 (1.93) | 2.99 (0.43) | 6.0% | 27.7 (7.0) |
| `r31-s3b` | 1,768 (1,770) | pass, 40 | 44 (69) | 5% | 8.4 (5.0) | 1.18 (2.48) | 0.88 (0.29) | 4.9% | 8.4 (8.8) |
| `r31-s4` | 1,706 (1,840) | pass, 126 | 134 (30) | 17% | 3.7 (18.4) | 0.65 (7.85) | 2.09 (2.92) | 5.7% | 6.8 (25.8) |
| `r31-s5` | 1,772 (1,776) | pass, 49 | 53 (58) | 55% | 13.6 (5.3) | 1.10 (2.47) | 1.81 (0.63) | 6.9% | 11.4 (11.3) |

- **M0 held.** Every arm within 0.93 to 1.06 of round 30's population, and D063 as amended
  holds in 4 of 5. Seed 1's line thinned from 20 at 15,000 s to 5 in the last two lifetimes
  and fails on stability alone; its exudate piled to 23 kJ as the line went.
- **M1 failed, and in the other direction.** The prediction was a hole at the sitter: its
  intake down, the water at large not down by as much. What happened is the reverse. The
  water at large emptied, 52 to 92% below round 30 in four seeds, and the sitting stomachs
  ate the same or more in four seeds. Less stirring did not starve the sitter. It stopped
  carrying the exudate away from the producer the sitter sits beside, and the world at
  large went hungry instead. The one seed where the sitters ate less, seed 4, is the one
  whose round 30 realisation was unusually rich, 18 J per body against 4 to 6 elsewhere.
- **M2 failed.** No jointed guild in any seed. The count of bodies born with a joint peaked
  at 7 in seed 3 at 2,300 s and at 0 to 2 in the rest, all before 5,300 s. It read 0 at
  every sample of the last 6,000 s in every arm. M3 does not apply.
- **M4 held in three.** Out over exuded is 1.06 to 1.09 in every seed. The standing detritus
  is under three times round 30's in three seeds. It is over that in seeds 1 and 2, where
  the stomach lines thinned late and the exudate piled.
- **M5 failed.** The deep layer holds more than round 30's in four seeds, three to seven
  times, and the floor's share is over 5% in four. The larder went deep after all. 0076's
  column profile was one lifetime; over ten the sink wins where the stirring no longer
  returns what fell.
- **M6 held in four**, with seed 3's seven divergences as its caveat; identities at zero on
  every row of every arm, the vertex count under half the cap.
- **Not predicted, and worth a line.** The share of bodies carrying a sense is 17, 26 and
  55% in seeds 4, 2 and 5, where round 30's same seeds read 2 to 3%. Round 30 carried a
  sense in one seed of five. Whether any is used stays the inoculated round's question.

## Verdict

The world can pay the price of stirring less: the goal rule holds in four seeds of five,
against five in round 30, with the population unchanged. The prize the round was built for
did not appear where it was predicted. The hole is not at the sitter. The producer beside
every stomach refills it, and what the stirring had been doing was carrying the exudate
away from those pairs to the water at large. With less stirring the pairs ate better and
the world elsewhere emptied. That is 0077's second reading, "M0 holds, M1 fails", and its
lever is the exudate fraction. No swimmer lived to be tested, because no joint survived
founding in any seed, which is the bottleneck this round names and cannot move. The
larder went deep, and the pre-registered reading says remineralisation goes on in the
round after. Two of my predictions were wrong: the hole's location, and the depth
profile's persistence.

The round after is not on this water. The owner's argument about where a hole lives
replaced the vertex field with a grid the same night (logbook/0078). Round 32 is therefore
the grid's base round at these prices and this mixing, with the floor's return held off as
the control. The deep larder is read there from the grid's own `det deep` before the
remineralisation lever is pulled. This is the vertex world's last word.

