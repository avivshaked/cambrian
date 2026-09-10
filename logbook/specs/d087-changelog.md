## 0u. Changelog — bodies that grow (2026-09-09, D087)

§5A.6's two evolved numbers become three dials: `BroodSize`, `BirthInvestment` (a fraction
of the parent's tissue value, replacing the endowment in joules) and `AdultScale` (one scalar
on every node's dimensions). A parent breeds when it holds the investment plus the brood's
overhead and spends exactly that; each child's share is split into body and reserve by
`NewbornReserveFraction`, its birth fraction is the body over its adult tissue value, capped
at one and refused under `MinNewbornPartKilograms`. The child is developed once at its adult
size and scaled by the cube root of its fraction (`Phenotype.Scaled`; `Organism.AdultPhenotype`,
`BodyFraction`). `World.Grow` moves reserve into tissue and matter from the body's cell into
the body each metabolic step above `GrowthReserveFloor`, so §5A.2d's matter draw now happens
at growth as well as at conception, and both books close by construction. The harness
resizes the articulation in place every `GrowthStepSeconds`. `MaximumTissueJoules` ends a
runaway on biomass beside `MaximumPopulation`'s count. Genome format 5; `bf` and `as` on
lineage birth rows; four table columns. Built and smoked 2026-09-09 (logbook/0081).

