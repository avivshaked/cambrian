# One limb steers its body under D135: the steering checks

*2026-10-12, by the speed agent (evolution-simulator-08), at the coordinator's request, for the nursery's swimmer
design. Measured by `TurningDragTests.ABentFinOnAVerticalHingeSteersItsBody` and
`TurningDragTests.AUniversalLimbsTwoBendsSteerItsBody` on branch `r59-anchor`, step 1 of
[`r59-anchor-build-plan.md`](r59-anchor-build-plan.md).*

## The question

Under the bend-and-power drive (D135) a brain holds each joint at a bend and gives it a power. The power pushes the limb
along its own length toward its joint. The coordinator asked whether one limb on one joint can steer its body that
way, and how much D161's drag on turning slows the turn.

## The bench

The test body is a box root 0.4 m on a side, with one link cell 0.4 by 0.1 by 0.4 m on its +x face. The link's two
neurons, or four on a universal joint, hold constant outputs: full power, and each bend at the top of its range or
centred. The joint limits are ±1.2 rad. The water is still, with added mass 0.5, the limb push 0.3, Ω = 6 rad/s and a
pose of 2 Hz. There is no weight and no bed. Each run lasts 10 s at a 0.01 s step. The drag on turning is off, or at
0.05 in either of its forms. The nursery check sets the real value.

The body's turn is the root's spin integrated in its own frame: yaw about its vertical axis, pitch about its lateral
axis, roll about its length.

## A fin on a vertical hinge

The test chain's hinge bends about a horizontal axis. Here the limb is turned a quarter about its own length. The hinge
then stands vertical (the test asserts the axis's vertical component is 1.000), and the limb is an upright fin.

| Turning drag | Bend held at one end | Bend centred |
|---|---|---|
| off | turned 319°, no tilt | 0°, swam 2.8 m straight |
| skin, 0.05 | turned 308° | 0°, 2.7 m |
| spin, 0.05 | turned 312° | 0°, 2.8 m |

A held bend turns the body nearly a full circle in 10 s. The drag on turning at 0.05 slows that by 2 to 4%.

## A limb on a universal joint

On this solver a universal joint is not two bends. Its first degree of freedom twists the limb about its own length.
Its second bends the limb about the joint frame's vertical axis.

| Held | Yaw | Pitch | Roll |
|---|---|---|---|
| nothing | 0° | 0° | 0° |
| the twist | 0° | 0° | −57° |
| the bend | −586° | 0° | 0° |
| both | −198° | −551° | −56° |

The table is with the drag off. The two forms give the same pattern. They slow the bend's yaw to 468° (skin) and 542°
(spin), and the pitch with both held to 436° and 508°.

The twist alone pushes nothing, because D135 weights each degree of freedom by how far its axis stands from the limb, and
a twist's axis is the limb. The body only rolls against the held limb. The bend alone is a pure yaw. With both held, the
1.2 rad twist tilts the bend's axis to (0, 0.36, 0.93) in the body's frame. The turn follows that axis: mostly pitch,
with some yaw.

## What it means

*Inference, not measured beyond the cases above.* One limb on one joint steers a body in three dimensions under D135.
On a universal joint the twist chooses the turn's axis, which can stand anywhere within the twist's limit of the body's
vertical. A pure pitch needs a twist of 90°, and these limbs stop at 69°, so every pitch they make comes with some yaw
and some roll. An evolved joint's limits set that range.

The older `BendAndPowerTests` check uses the chain's horizontal hinge, so the turn it measures is a pitch. It was
renamed `ABentPoweredLimbPitchesItsBody` the same day.
