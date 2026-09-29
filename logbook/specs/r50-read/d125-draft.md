### D125
**A leaf founder is set where its own income is largest, from round 50** · 2026-09-26

**Status:** ruled by the owner on the morning of 2026-09-26, option (a) of three put to them after
round 49's leaf read. Built on `leaf-income-depth` as a tunable, `FoundersFollowIncomeDepth`
(`EVOSIM_FOUNDERS_INCOME_DEPTH`), refused without `FoundersFollowFoodDepth`. The header's founder
token reads `founders in their food at its depth, leaves where they earn most`. It is off in every
recorded config and on in round 50.

**Decision.** A founder with a leaf, placed under D122's depth rule, is set in the cell of its
accepted column where its own income, light and food together, is largest. Each live cell of the
finer grid's column, 1 m in the campaign, is priced at its centre by `Metabolism.StepAt`, the call
the metabolic pass bills a body with. The price reads the shaded light there, the snow's edible
density and the dissolved matter's density, at age 0 and with no work. It is the orientation
average, since a founder has no pose until the solver places it. The founder lands in the cell
with the largest light income plus food income, the shallower on a tie, and the placer then holds
it off the surface and the bed as it holds any founder. A body that prices at nothing everywhere
keeps the drawn depth. A body with no leaf is placed as D122 placed it, so a stomach's landing does
not move. D116's acceptance still chooses the column.

**Why.** D122 set a leaf at the richest cell of the dissolved matter, and late in a run that cell
lies deep. In round 49's three seeds the trickle's leaves were set at a median 44 m down, where
the light is under a thousandth of the surface's. They died at a median 68 to 99 s, and 2 of 1,256
bred. The floor's leaves, set at 4 to 8 m during the founding, lived a median 679 to 1,432 s, and
36 of 85 bred (logbook/0122, `scripts/reads/r49-entry/leafdepth.py`). A leaf eats
light and matter together, and the world's own bill is the one price that weighs the two as the
world does.

**Rejected.** (b) Leaves at the drawn depth, 0 to 12 m, as before round 48. It ignores the matter,
which round 48's rule was written to read. (c) No change, which repeats round 49's dead trickle.

**Built with it.** The founder clamp's repair (`founder-depth-bed`, logbook/0122's W1). The
depth rule held a founder above the bed at the tank's centre, which a flat floor makes the same as
the bed under it; on round 49's tilted bed it lifted every founder whose food lay deeper than
45 m to just above 45 m. The repair reads the bed under the candidate and changes nothing on a flat
floor. Round 49's leaves were placed by the rule and the clamp together: the rule chose the deep
matter, and the clamp lifted those over a bed deeper than the centre's to about 44 m.
