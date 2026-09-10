# 0085 — The water carries now

**2026-09-10**  ·  the three fixes after the ribbons, built and smoked in one afternoon; what the owner should see in the theatre before anything is re-read

0083 found the world was two ribbons and named the three rules that made it. By the evening
all three were replaced, the replay fault found the same day was fixed beside them, and one
run of the new world had been watched by the identity check in Play mode. This entry is
the build record. D088 is the ruling; the numbers below are from the smokes, six hundred
seconds each, and say nothing about a world that has run.

## Dispersal

A newborn is set down at a point drawn uniformly over a horizontal disc around its parent,
of radius `OffspringDispersalMetres`, uniform in area so that children do not pile up near
the parent, never closer than the parent's radius plus the child's, wrapped at the seams,
at the parent's depth, under the same free-spot test and the same 64 attempts. At 0 the
placer takes the branch it always took, so every config in the record still describes its
world. The first value is 5 m, and the owner has not ruled on it; in a box 5 m wide the
disc reaches across the whole width and a quarter of the length. The variable is
`EVOSIM_OFFSPRING_DISPERSAL`, because `EVOSIM_DISPERSAL` was already D061's retired
patch lottery and one name would have set both.

With it, the instrument the record lacked: at every sample the number of occupied 1 m
columns of the footprint out of a hundred, the same for the eaters, and the spread of x
and z, circular because the box wraps. In the table they are `cols`, `cols abs` and
`x sd`. Round 33's ribbons were about 17 columns for a thousand bodies.

## The current

The rolls were built for a world in which x and z did not exist (D037's own proof says
that a field of depth and time alone cannot be divergence-free and depth-varying at once).
They stay, as `CurrentMode Rolls`, so that every recorded run replays. The new mode is
`Transport`: a three-dimensional velocity field over the whole box, the curl of a vector
potential of five Fourier modes, so divergence-free by derivation rather than by patching a
component; periodic along the length and across the width; its vertical component a
half-sine in depth that is exactly zero at the surface and at the bed; each mode's phase
drifting at an incommensurate rate so that the pattern never repeats and a parcel is
carried rather than returned. The knob is the same `CurrentSpeedMetresPerSecond`, read now
as the field's root-mean-square speed over the box.

The first cut was eleven to one vertical. The reason is continuity: a mode's vertical speed
carries the horizontal wavenumbers and its horizontal speed the vertical one, and with
wavelengths of the order of the box on every axis the vertical wins in a box 5 m wide and
60 m deep. The owner's words were "in all directions really", so the vertical mode number
is now derived from the horizontal ones by the one condition that makes the three
mean squares equal, and the eddies are squat because the box is narrow: vertical
wavelengths of 20 m down to 4.4 m. The per-axis speeds agree within 0.2%, the fastest water
is 2.45 times the knob, and the least-moved of 64 parcels over one period travelled 6.5 m.

Bodies ride it through drag against the water at their root; corpses drift by the same
sampler; the grid advects each cell by the water at its centre, on all three axes, and
when the fastest water would cross more than half a cell in one metabolic step the
advection splits the step in two or more rather than refusing it, up to eight. At 0.3 m/s
on 1 m cells that is two substeps and 24,000 field samples per half second of world time,
which cost about a quarter of the throughput in the smoke.

## The replay, and the reservation

The theatre's replay had parted from round 33 at 200 s because a dead body's destroy was
deferred to the end of the frame in Play mode and immediate in batch (0083). It is
immediate in both now, in the body, the sea floor, the mesh cache's temporaries and the
world's teardown; the one caller that destroyed from inside its own destruction keeps the
deferred form, named as the exception. A Play-mode identity check now exists beside the
edit-mode one, launched without `-quit`, driving the runner's own frame with tens of steps
in it; on the transport smoke it read identical on all six samples. The edit-mode check
says in its verdict that it is the edit-mode check, since it passed for three days while
the Play-mode replay was wrong.

The placer reserved a newborn's spot at the size it was born with and the body grew up to
twentyfold in place (0082's suspect for seed 2's contact divergence). It reserves the
adult's radius now, and the dispersal floor is the parent's radius plus the child's adult
radius. Founders still reserve their birth radius; that is a founder rule and it is the
owner's. The theatre's pairing of recorded ids to bodies was also wrong for any bred child,
since it compared the root's height with the centre of mass's; it compares part counts and
patches now.

## The smoke

Seed 3, 600 s, dt 0.02, dispersal 5 m, transport at 0.3 m/s over 6,000 s, on worker 7,
`simHash 7796150b…`, `coreHash e67316f9…`. Both books closed at every sample, nothing
above the waterline, nothing diverged, no crowded stillbirth.

| t (s) | alive | `cols` | `x sd` | wraps | mean m/s | depth m |
|---|---|---|---|---|---|---|
| 100 | 40 | 33/100 | 6.33 | 93 | 0.23 | −19.2 |
| 300 | 54 | 38/100 | 5.37 | 137 | 0.23 | −10.4 |
| 600 | 119 | 71/100 | 8.28 | 281 | 0.22 | −10.5 |

The rolls at half that speed, before the axes were balanced, gave 45 columns and an
`x sd` of 3.5 at the same point. One reading to carry: `mean m/s` is the water now. A
sitter in this field moves at a fifth of a metre a second because the water does, and
the column that was the campaign's locomotion readout no longer is; a swimmer's speed is
its speed relative to the water, and there is no column for that yet.

## What to watch

The owner's ruling was to see the fixed world before deciding which experiments stand.
A viewing arm is running for that: seed 3 of the same launcher at 0.01, 30,000 s
(`r35v-s3`, worker 7), readable in the theatre from the moment it starts. The things to
look for are the ones the ribbons hid: whether a clade spreads along the box or still
sits in a column; whether bodies are carried through one another's water; whether the
eddies look like water or like a machine; and whether anything sits where the current
puts it or holds against it.

## Sources

The three build reports (dispersal, transport, destroy), `logbook/specs/current-transport-spec.md`,
`logbook/specs/destroy-fix-spec.md`; `runs/r35smoke`, `r35tsmoke`, `r35tsmoke2`, `r35tsmoke3`;
`scratch/logs/theatre-identity-play.log`; `CurrentTransportTests`; D037, D066, D077, D086,
D087, D088; logbook/0082, 0083, 0084.
