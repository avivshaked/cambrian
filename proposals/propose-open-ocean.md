# Proposal: an open ocean with no glass, its edges joined, and island shoals inside it

*2026-10-18. On the owner's idea, discussed the same evening. The round glass tank gives way to an
endless sea: a square whose opposite edges are joined, so a body swimming out of one side comes back
in at the other. The oases sit in the middle and a wide desert runs to every edge. The theatre draws
each body at its copy nearest the camera, so no seam is ever seen. For the owner's ruling; absorbed into DECISIONS.md on ruling, then deleted. Helper scripts and listings are in
`scratch/open-ocean/`.*

## 1. The proposal, and a first look that weakens its main reason

We would retire the glass tank (D089) for new rounds and run them in the joined square the code
already has (the box, D077). The box would be extended to carry what the tank has gained since: a
shaped bed, reefs, fed islands, and the graphics card. Islands of matter would sit in the middle with
desert on every side. As a second step, the bed could rise inside the world into shoals, flat tops a
metre or two under the surface, where an oasis can sit in lit water. The tank stays in the code, so
every recorded world still replays.

The reason given was that life crowds against the glass. It did in round 37 (D090) and in round 60
(D181, D187), and round 61's first theatre look showed bands of bodies along the rim. Three causes were
suspected: the glass's friction (D137), the current's speed toward the glass falling to zero there, and
leaves living on matter that pools there.

I took a first look before writing this, because the cheapest part of the measurement in section 5
could be read in minutes from files the runs already wrote. It does not support a crowd held at the
glass in rounds 60 and 61. The outer 15 m of the tank is 17% of its floor. It held 8 to 10% of the
bodies at round 61's end, and 2 to 7% earlier in round 61 and in round 60. Dissolved matter and
falling detritus (the snow) there were also below their share. The crowd is over-represented further
in, 30 to 60 m from the glass, and off the islands. Round 37's crust was real and D090 cured it.

Two of the three causes can be checked from the runs already. Friction at the glass was off in both
rounds (the bed friction setting is 0 in their configs), so it cannot be holding anything. Matter does
not pool at the glass in round 61. The third, the current, is still a candidate in a weaker form. In
the tank, the water's speed toward or away from the centre carries a factor that falls as the glass
nears. It is a sixth 15 m out and a third at 30 m. So the outer band trades bodies and matter with the
middle slowly, and that could hold a crowd 15 to 60 m in. I read this off the field's formula and have
not measured it.

The owner saw bands at the rim in the theatre, and I believe they are there. The numbers say where they are. Within 30 m of the glass, two of eight compass sectors hold 1.6 and 1.9 times their share of area.
Within 15 m, none holds more than 1.3 times. My guess is that the bands seen are these. From a camera looking across a
tank 335 m wide, a crowd 20 to 50 m inside the glass sits visually against it. A theatre view from
above, with the islands drawn, would settle it.

So the case for the open ocean now rests less on curing a trap and more on the owner's standing aim
of a world like a real ocean. A real ocean has no wall, so no swimmer can evolve to follow one. Separate oases with empty water between them are what the open square is built for.

## 2. What the first look measured

Each row gives the share of the tank's floor area in a band, and the share of the bodies (or the
stock) found in that band, for one recorded moment. Look at the outer 15 m.

| run, time | outer 15 m: area | bodies | dissolved matter | snow | 30–60 m: area | bodies |
|---|---|---|---|---|---|---|
| r61-p6101x2, 72,000 s | 0.171 | 0.095 | 0.139 | 0.117 | 0.262 | 0.335 |
| r61-c6101x2, 72,000 s | 0.171 | 0.082 | | | 0.262 | 0.340 |
| r61-p6102x2, 72,000 s | 0.171 | 0.100 | | | 0.262 | 0.343 |
| r61-p6101, 12,000 s | 0.171 | 0.017 | | | 0.262 | 0.516 |
| r61-p6101, 30,000 s | 0.171 | 0.060 | | | 0.262 | 0.435 |
| r60-s6001, 48,000 s | 0.171 | 0.042 | | | 0.262 | 0.382 |
| r60-s6002, 48,000 s | 0.171 | 0.068 | | | 0.262 | 0.275 |

A confound shows up in the bed. Round 61's floor is tilted by 41 m. At the glass it lies 4 to 8 m deep
on one side and 37 to 43 m on the other (`fields/bed.f32`). The fuller sector within 30 m lies
over the shallow side, and the other over a floor 17 m deep. Round 60's band at the north-east glass
(D187) was seen at 20,000 s. The positions I found for that screen end at 10,000 s, so I have not
checked it.

## 3. What assumes the tank

The joined square runs on the computer's processor, and neither on the card nor in the theatre. Everything below was built for
the tank alone, and each needs a box version before option B can run. File and line references are
for whoever builds it.

| piece | today | what the open square needs |
|---|---|---|
| The bed | Cosines of random direction over the disc (`BedShape.cs:545-580`); the box refuses relief and tilt (`World.cs:1649-1662`, `RunConfig.cs:1910-1913`) | Snap each wave to a whole number of wavelengths across the square, so it joins at the seams (at round 61's 17.6 m scale the change of direction is a few degrees, my arithmetic). A tilt is a plane and cannot join, so it is refused |
| The grid's live mask | Built only in a tank (`GridField.cs:552-555`, `576-615`); it already marks a column filled to the surface as dead | Build it in the box too, inside always true |
| The island map | Perlin noise, joined only every 256 wavelengths (`GradientNoise.cs`, `SampleAt`); a glass margin keeps islands 30 m in (`GridField.cs:1495-1553`), refused in a box (`:1505`) | An edge margin in its place. The seam then lies in desert, and the map need not join |
| The current | Tank: the streams, with no flow through the glass (`CurrentField.cs:2108-2212`). Box: D088's five eddies (`:1781-1791`, `1880-1965`) | The box field is a sum of curls, so it has no source and no sink, and its eddies drift and never hold a centre. It fits D090's lessons, and D090's force on bodies is the same in either shape (`Fluid.cs:50`, `205`). But at 297 m wide all five eddies are 130 to 300 m across with one layer in depth (`VerticalWaves`, `:1832-1836`, my arithmetic). It does not follow the bed (`:509`) and ignores reefs (`:613`) |
| Reefs | Placed over the disc, refused in a box (`ReefGeometry.cs:416`, `575-586`) | Placement with distances measured the short way round |
| Light | Depth and reef shade only in round 61; ring patches are a reporting bin (`TankGeometry.cs:123-133`, `World.cs:3637-3652`) | Nothing beyond the reefs |
| Founders | Uniform over the disc or the box (`SharedVolume.cs:558-570`); following food is shape-free (`World.cs:428-500`) | Nothing |
| Contacts between bodies | One hash grid with no wrap (`Contacts.cs:1-60`) | Bodies either side of a seam do not touch today (my reading; no farm round has run in a box). They need the short-way distance |
| The glass | Spring and damper (`Contacts.cs:952-976`, `1174-1196`), a kill past R + 1 m (`Divergence.cs:113-127`) | Off by itself: the solver's radius is 0 in a box (`SolverConfig.cs:309-313`) |
| The card | Refuses any current but the streams (`GpuWorld.cs:388-393`) and any scent grid but a tank's (`:458-463`); its transport has no wrap (`GpuTransport.cs:330`, `449`, `514`); glass and the tank's cell walk in the kernel (`gpu-kernel.template:3583`, `4140`) | The box current, wrapped transport and scent, wrapped contacts, and bodies moved across the seam inside the card's block of steps |
| Readers | `positions-read.py` already handles the box (`:204-271`). Round readers measure off the glass (`r61-read.py:153-183`, `r60s-islands-glass.py:63-70`, `field-map.py:146-157`); `leaf-pool.py:85-112` refuses a box; `r59-read.py:607-613` takes a missing shape for a tank | A new round's reader, written for the box |
| Theatre | No wrap anywhere. Bodies drawn where recorded (`LiveWorldView.cs:489`, `630`); one bed mesh over the box (`TheatreSkin.cs:1775-1814`); the camera kept inside the glass or box (`FilmPlans.cs:249-279`); safaris arrive from outside the glass (`SafariPlans.cs:205-277`); crowd centres are plain means (`:76-79`); the water's colour is computed only inside the box (`TheatreWater.hlsl:277-331`) | Each body and reef at its copy nearest the camera; a bed drawn around the camera, read through the join; no clamps; centres taken the short way. The fog already hides the far side: at its density of 0.035 a body 50 m off keeps 5% of its light, and half the square is 148 m. The snapshot camera's fog widens with the box (`SnapshotCamera.cs:1380-1428`) and needs a cap |

## 4. Island beaches

The bed cannot break the surface today. With the shore setting on, the floor is clamped to a flat
shoal at a chosen depth, never above it (`BedShape.cs:461-480`, `752-773`, `936-952`). Round 61 had that setting
at 1 m, but its floor never rose that high.

Island shoals would reuse the clamp. A few smooth rises are added to the bed where the oases are,
with distances measured the short way round. Their tops are clamped to a shoal 1 to 3 m down. Light is by depth,
so a shoal is lit, which is the point. The grid's mask and the bed contact already work on any height
map. What the box lacks is a current that follows the floor. The tank's streams follow it and fade
toward a shoal (D092, and the shore fade), and the box's eddies would push water straight through
rock. That port is the main build of option C.

Dry land, a bed above the surface, is further work for little gain: rules for a body stranded in air,
and a current that goes to zero at a waterline. I would leave it out.

## 5. The measurement to run first

It needs the processor only and reads round 61's six worlds at every 6,000 s.

- **The inputs** are the positions file, one row every 10 s with each body's id, place and flags
  (`positions-read.py:575-589`), and the `fields/` folder with the matter, the snow and the bed. The
  `snapshots/` folder holds genomes and no places (`Row.cs:1262-1328`).
- **The radial profile** counts bodies and stock in bands 0–5, 5–15, 15–30, 30–60 and over 60 m from
  the glass, against area. It splits them by compass sector, by floor depth and by kind of body.
- **The dwell** is the time a body entering the outer 15 m stays before it leaves or dies, against
  the same for a band of equal area at 30 to 45 m.
- **The radial speeds** are each body's speed toward or away from the centre between samples, by band.

What each outcome points to:

1. The outer band stays under-filled and dwell there is no longer: the glass is no trap. B and C are
   chosen for the ocean's sake, or A is enough.
2. Dwell at the rim is long and radial speeds collapse there: a wall effect, which B removes.
3. The excess sits over the shallow floor alone: the tilt's shallow side attracts. A box without a tilt
   removes it, and C's shoals would put that attraction where the oases are.
4. The excess is 15 to 60 m in and radial speeds fall there: the current's slow outer band, which B
   removes.

## 6. Options

| | A. A larger tank with a wide desert ring | B. The open square | C. B with island shoals |
|---|---|---|---|
| Build | Settings only: more area, a wider island margin | Section 3 | B, plus floor-following water in the box and the shoal rises |
| Record | One more tank round; comparable with 60 and 61 | A new geometry. A new realisation of every seed, rings become squares in the report, the tilt is gone | As B, with shallows placed where the oases are |
| Machine | Grids grow with area: twice the area is about 5.6M snow cells and roughly twice the transport (inference) | Round 61's area costs what round 61 cost (inference) | As B |
| Risk | Low. The wall stays, only further off | The card's port is the largest unknown | B's, plus the water at a shoal, the hardest physics here |

**I recommend** running the measurement, then C, built as B first and the shoals second, each screened
before the next. A tells us little that rounds 60 and 61 have not. If the measurement shows outcome 1
and the owner would rather spend the agent time elsewhere, A is the cheap fallback.

## 7. Estimates, in agent time

| step | agent build | machine | owner review |
|---|---|---|---|
| The measurement | half an afternoon | 10 to 20 minutes a world on the processor, not beside a card group | 15 minutes |
| A | an hour (launcher, reader) | a round | 15 minutes |
| B on the processor | one to one and a half afternoons | the full test suite, about 30 minutes | |
| B on the card | one and a half to two afternoons | identity checks against the processor, about an hour | |
| B in the theatre and readers | one and a half to two afternoons | | 30 to 45 minutes on pictures |
| B's screens | | two seeds at 10,000 s, two to three hours | 30 minutes |
| C on top of B | two to two and a half afternoons | the same again | 30 to 45 minutes |

B is four to five and a half afternoons. C adds two or so.

## 8. Questions for the ruling

1. Does the measurement in section 5 run before anything is built?
2. Which option: A, B, or C built in two steps?
3. Is the open square round 61's 88,000 m², or larger to widen the desert? At 88,000 m² it is 297 m
   a side, and a 30 m edge margin leaves 60 m of desert between the oases and their copy.
4. Is the tilt dropped in the open square, leaving the shoals as the only shallow places?
5. Are island beaches shoals 1 to 3 m under the surface, with dry land left out?
6. Does the box's water keep D088's five eddies, or gain a finer spectrum like the streams at this size?
   It is a world rule, so it is the owner's.
7. Do the reefs come into the open square?
