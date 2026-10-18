# Round 60's screen r60s-big3 (D185): r60s-big2's world and dose (4.8 units/s, 60,000 units, four times round 57's
# water) with D185's glass margin, no island column within 30 m of the glass, and D181's founders in the islands.
# D181 put round 60's founders in the islands as round 45's were, by D109's rule: every founder is placed by the
# matter's columns. Core refuses that rule beside D116's, so D116's four switches go off with it: the founders'
# food, its depth, the income depth and the corpses. Against r60s-big2 this screen changes two things, the margin
# and the founders, so a difference between them is not the margin's alone. Seed 1 only.
$s = . (Join-Path $PSScriptRoot 'env-r60s-big2.ps1')
$s.EVOSIM_MATTER_ISLAND_MARGIN = 30
$s.EVOSIM_FOUNDERS_FOLLOW_MATTER = 1
$s.EVOSIM_FOUNDERS_FOLLOW_FOOD = 0
$s.EVOSIM_FOUNDERS_FOLLOW_FOOD_DEPTH = 0
$s.EVOSIM_FOUNDERS_INCOME_DEPTH = 0
$s.EVOSIM_FOUNDERS_FOLLOW_CORPSES = 0
$s
