# Growth as a climb: a rate law, and a reserve threshold the genome carries

*2026-10-26, the speed agent, for the coordinator and the owner. A design, not a build. Nothing here runs until the owner
rules on it, and G2 (`logbook/specs/r62p2-prereg/predictions.md`) stays parked until then. It replaces this file's
first design, a fixed runway. The owner asked that growth be a longer, continuous process up to adulthood, and that it
happen only while the reserve stands above a threshold the creature inherits. The case is the newborn ledger
(`logbook/specs/r62p-read/newborn/`). Numbers from `r62p-t3-6201` are marked measured or derived. The rest is
inference and says so.*

## The rule

Today `World.GrowOne` invests everything above `GrowthReserveFloor` (0.1 of the body's tissue) every metabolic step,
so a newborn grazer is adult-sized within half a second. Under the rule, with the switch on, each step invests the
smallest of three amounts:

1. **The rate law's allowance.** The step's length times dm/dt = a·m^¾·(1 − (m/M)^¼), with m the body's tissue
   joules and M its adult's. This is West, Brown and Enquist's ontogenetic growth model, taken here from general
   knowledge and not checked against the paper.
2. **What the reserve can spare above `keep`**, where `keep = max(0.1 × tissue, G × StandingWatts)` and G is the
   creature's own gene, below.
3. **What is left of the adult.**

A body that cannot spare anything above `keep` does not grow. The law has a closed form that makes its one constant
easy to set. With r = (m/M)^¼, dr/dt = (a / 4M^¼)(1 − r), so 1 − r falls exponentially with the time constant
τ = 4M^¼/a (derived).

**One dial, the time to adulthood.** `RunConfig.GrowthAdultSeconds` T is the time a grazer born at 0.65 of its adult's
tissue takes to reach 0.95 of it, growing at the law's full rate. From 0.65 to 0.95 is 2.081 time constants, so
τ = T/2.081 (derived). T = 0, the default, is today's instant growth: the branch is never entered, and every recorded
world runs as it does today.

**Adulthood is a snap at 0.95.** The law approaches M without reaching it. A body whose growth would carry it to 0.95
takes the rest of the adult in the same step, if its reserve above `keep` pays for it. So T is the time to
adulthood, and not merely to 95 %.

**The same τ for every body.** That makes the time to adulthood depend on the birth fraction alone, not the size. A
true West, Brown and Enquist world would have one a, so that time scales as M^¼, and that needs a reference mass as
a second tunable. Between a 56 J grazer and a ten-times-larger body, M^¼ is a factor of 1.8. I recommend the single
dial, with the M^¼ form as an option the owner can ask for.

**The growth draw at birth** (derived). At T = 60, 120 and 240 s, a newborn grazer's law allows 0.57, 0.28 and 0.14 W,
against an upkeep at birth of 0.62 W. Today it takes 19.3 J in one step.

## The gene

`GrowthReserveSeconds` G is the threshold, in seconds of the body's own standing cost, so it scales with size.

- **Where it lives.** It is one number a genome, a whole-creature dial beside `AdultScale` and the four reproduction
  dials. It acts only when T > 0.
- **Mutation.** A log-normal step, a Gaussian of `ScalarStdDev` on its logarithm, reflected into a range, as b4's form
  genes step. The range is [1, 300] s. A logarithm needs a positive floor, and 1 s is effectively none. It is rolled
  under its own `MutationRates.GrowthReserveChance`, tested against 0 before any draw, so a world without it takes
  exactly today's draws.
- **Its start.** Founders, trickle and pool founders, inocula and nursery tank bodies start at
  `RunConfig.GrowthReserveStartSeconds`, written into their genomes. The screens below set it, and evolution moves
  it from there.
- **On the lineage row.** Each birth row carries the value its body was born with, as `grs` when T > 0, so the
  record shows it evolve.

**Tunables.** `GrowthAdultSeconds`, `GrowthReserveStartSeconds`, `GrowthReserveMinSeconds` (1),
`GrowthReserveMaxSeconds` (300), and the mutation chance. Each changes every config hash, and the fixtures are
re-recorded, as for any new tunable (`src/Evosim.Core/CLAUDE.md`).

## The newborn grazer's runway

These figures are for an unfed newborn grazer in t3's world, from a step-by-step model of the rule (derived). It is
born with 36.7 J of tissue and 36.7 J of reserve, its adult's tissue is 56 J, and its upkeep runs from 0.62 W at birth
to 0.908 W adult, as tissue to the 0.9 (the audit's exponent).

| T | G | Body at 30 s | Reserve at 30 s | Starves, unfed, at |
|---|---|---|---|---|
| today | — | adult from 0.5 s | — | 19.5 s |
| 60 s | 0 | 0.86 | 3.6 J | 35 s |
| 60 s | 30 s | 0.77 | 9.5 J | 43.5 s |
| 60 s | 60 s | 0.66 | 18.1 J | 59.5 s |
| 120 s | 0 | 0.78 | 9.3 J | 41.5 s |
| 120 s | 30 s | 0.74 | 12.1 J | 48 s |
| 120 s | 60 s | 0.66 | 18.1 J | 59.5 s |
| 240 s | 0 | 0.72 | 13.3 J | 47.5 s |
| 240 s | 30 s | 0.71 | 14.4 J | 52 s |
| 240 s | 60 s | 0.66 | 18.1 J | 59.5 s |

The newborn ledger measured 18 s today; the model gives 19.5 s. At G = 60 s a newborn's reserve (36.7 J) is below 60 s
of its upkeep (37.3 J), so it does not grow until it eats, and it lasts as long as its birth reserve does. The
never-ate newborns' median straight-line time to a leaf was 26 s (derived, the ledger). Every row but today's covers
that, and G = 30 s covers it with room at any T.

A newborn that does eat grows out of its meal at the law's rate. It reaches adulthood in about T, so long as its intake
covers its upkeep and its allowance. It is smaller meanwhile, with limbs of about 0.87 to 1 of an adult's length and
its drive's power an adult's (`Phenotype.Scaled`), unless the juvenile-strength switch below scales it. Whether a
small body swims slower or faster than an adult is not measured; the switch's still-water test measures it.

## Is a separate runway floor still needed?

No, because the gene is the runway. At G = 60 s it gives the fixed-runway design's 60 s, and it lets selection choose
the value instead of the owner. The tissue floor of 0.1 stays as the least any body keeps, and with G at its
minimum of 1 s it is the floor that binds. The rate law alone, at G = 0, roughly doubles today's runway, to 35 to
48 s. That is because it slows the spend, not because it protects the reserve.

## Breeding

Today "grow first, breed later" falls out of the ordering, because growth runs first and takes the reserve down to
the floor. Under a rate law it does not. A juvenile whose intake beats its allowance banks the surplus, and its gate
is a juvenile's, small, because the price is a share of current tissue. So it could breed small. The rule therefore
needs an explicit gate: **with T > 0, a body breeds only once adult**, which `World` checks at conception. That is a
world rule, and so the owner's.

For a grazer this barely moves the generation time (inference). A t3 grazer parent's median age at a breeding was
about 400 s (measured, the ledger), against an adulthood of T. And a juvenile's earnings above its growth bank toward
the gate it will meet as an adult.

## The reserve-margin gene

The two stay separate genes. `ReserveMargin` is the seconds of upkeep a parent keeps back at breeding, which under the
gate above acts only on adults. G acts only on juveniles. So one gene could serve both without a clash in timing, but
not at the values selection has found. t3's grazers carry a margin of about 590 s (derived, from the 684 J gate). If
that number were G, a newborn would bank 590 s of upkeep, about 370 J, before it grew: about ten times its birth
reserve, and so it would never grow. The two answer different questions with different costs: "how safe before I grow"
against "how safe after I breed". Selection needs to move them apart.

**Species distance.** G joins `SpeciesDistance.ParameterUnits` once a genome, as the log gap over its range,
|ln G₁ − ln G₂| / ln(300/1), the form b4 uses for its form genes. It goes in its own statement, so a pair that carries
no gene adds exactly 0, and every recorded world's species replay.

## The genome format, with b4

b4's branch `shape-genes` (`2b8e3e58`) writes formats 13 and 14 only for a genome that holds a family node. Every
other genome still writes 11 or 12. Two independent extensions would need four more numbers: growth alone, growth
with the one brain, and each with a family node. I propose one numbering, agreed with b4 before `shape-genes` merges:

- **Formats 13 and 14 become the extended formats.** They are 11 and 12 plus either optional extension: b4's family
  forms, and `growthReserveSeconds`. The refusal on `shape-genes` of a 13 or 14 with no family node becomes a refusal
  of one carrying neither extension.
- **The gene's key is always written in 13 and 14,** as a number or as `null`. So a dropped key is still refused,
  under the refuse-rather-than-default rule, and a null is a stated absence. The family forms stay keyed by each
  node's shape, because the shape already says whether a form must be present (b4's adjustment).
- **The writer writes the lowest format that holds what the genome carries.** A genome with neither extension
  writes 11 or 12 byte for byte as today, so every genome written today loads and round-trips unchanged, and every
  stored inoculum and pool genome with it.
- **The gene is carried only when present** (the coordinator's ruling). In a world without the climb, no genome ever
  has it. In a world with it, every entry point gives the gene `GrowthReserveStartSeconds` explicitly: founders,
  trickle and pool founders, inocula and nursery tank bodies. The value is written into the genome and logged as
  `grs` on the birth row, and nothing fills it in at load. Children inherit it.
- **A genome that carries the gene, entering a world with T = 0, is refused by name,** as a family shape is refused in
  a world without the family.

## What it does to leaves and plants' early production

A bred leaf is born at a median of 0.19 of its adult (measured, 65,027 births). From 0.19 the law takes 3.28 time
constants to reach 0.95, so 1.58 T (derived). That is 95, 189 and 379 s at T = 60, 120 and 240 s, against a median
bred leaf life of 1,717 s (measured).

The law binds only where light pays faster than it allows. Today's leaf growth speed is not in the record: the lineage
has no body fraction after birth. So whether 1.58 T is slower than leaves grow now is unmeasured. A read of the
checkpoints' body fractions by age, in a HOLD window, answers it.

Where the law does bind, a leaf stays small longer. Its light area runs as its fraction to the ⅔, so at 0.19 it holds
a third of an adult's. So the stand's early production falls, most in the founding years when most leaves are young,
and with it the grazers' food in standing tissue. A juvenile's surplus banks as reserve, which a bite does not take
but a kill's swallow does (D232). These effects are inference.

A leaf's G is inherited like a grazer's. In poor light a lineage with a high G grows slowly or never, and selection
sets it.

## Founders and their endowment cap

A founder starts with `FounderEnergyJoules × birthFraction` plus 600 s of its newborn upkeep, capped by D124 at 0.9
of its gate plus what its growth costs (`World.FounderStartCap`). That start is far above 30 s of its upkeep, so a
founder grows at the law's full rate. A grazer founder, born at 0.65, takes T to adulthood. A leaf founder, born at
0.14, takes 1.64 T (derived). It holds the rest of its start as reserve meanwhile.

The cap's growth term stays exact, because growth's debits still sum to the adult's tissue less the newborn's, only
over more steps. The breeding gate at adulthood enforces D124's purpose, that no founder breeds on its gift, in the
structure as well as in the cap.

## The nursery (D240)

The nursery trains under every condition set for the world, so it gains the rate law and the gene with the rest. Its
tanks (`Evosim.Nursery.LeafTank`, `Tank`) start a body grown by paying it the tissue it owes as reserve, which its
growth then spends at once. Under the law that payment buys a climb of up to 1.58 T instead. The seam is to place tank
bodies at their adult directly when the tank means "grown", which b4's build gives as `MakeAdult(Organism)`. It
gifts the tissue with no spend on any ledger, so a tank's record says so. Tank genomes from the pools carry no gene
and are given the start value. 6d takes the placement change; it does not collide with the pace seam, which touches
only the step count and the clocks.

## The two clocks

The owner ruled the growth rule on 2026-10-26 (D241) and asked that the design consider the physics clock and the
metabolic clock.

**Growth ticks in world seconds, on metabolic steps, at any pace factor.** Core's `World.Step` takes the step's world
seconds, 0.5 at any k: on branch `pace`, `Metabolise` passes `WorldSecondsPerMetabolicStep`
(`src/Evosim.Farm/Metabolise.cs:120` there). So the allowance a step is the rate times 0.5 world seconds, whatever the
physics did inside the step. At the pace factor k, the k × 0.5 / dt physics steps between two metabolic steps all see
the same body. A climb of T world seconds is T / 0.5 metabolic steps (240 at T = 120 s) and k times as many physics
seconds. Nothing in the rate law reads the physics clock, so it needs no k-dependent constant, which is the scaling
audit's finding for every other price (`logbook/specs/scaling-audit.md`).

**The resize seam sits between physics steps.** Core moves the joules and scales the phenotype at once. The physics
body follows only when the harness applies it, every `GrowthStepSeconds` (10 s), also counted in world seconds
(`_sinceGrowthStep += seconds`, `Metabolise.cs`). It happens after `World.Step` and before the next physics step.
`Creature.Resize` (`src/Evosim.Dynamics/Creature.Growth.cs`) rewrites in one derivation every link's volume, both
masses, the inertia tensor, both joint anchors and frames, and the drag panels. The root stays where it is and the
rest follows the new anchors outward.

Between applications the body carries its new size's drag and lit area but its old mass and anchors. That is today's
seam, and the climb keeps it. What changes is how large each application is:
- **Today** a newborn grazer's whole growth lands in one resize, from 0.65 to 1, about a 15 % change in length
  (0.65^⅓ = 0.87) (derived).
- **At T = 120 s** the first 10 s of the climb moves r = (m/M)^¼ by about 0.016. Length goes as r^(4/3), so that is
  about a 2.4 % change in length a resize, and less as the body nears its adult (derived).

The climb splits one large jump into many small ones. That should be easier on the solver, since a large resize is
where joint spin-up and divergence come from (logbook/0059, 0077; inference). The screens read it in the `diverged` count,
and b4's Farm test on a climbing body measures the momentum jump at each resize against today's single one. The
harness's `MaxResizeJumpMetres` reads 0 by construction, since a resize never moves the root.

The resize keeps the root's velocity and spin to the bit, and the joint coordinates and rates. The links' velocities
are re-derived from them, so linear momentum jumps with the mass and angular momentum with the geometry
(`Creature.Growth.cs`'s remarks, read). That is today's behaviour, and the climb makes each jump small.

**T in units of the body's metabolic clock.** The owner ruled one T for all bodies, in world seconds. A body's own
metabolic time is its tissue over its upkeep, how long its standing cost takes to spend its own body's worth:
- A grazer's is about 59 s at birth (36.7 J over 0.62 W) and 62 s as an adult (56 J over 0.908 W) (derived).
- A leaf's is 125 to 170 s, from the code's 500 J of tissue a cubic metre against 3 to 4 W a cubic metre
  (`RunConfig.GrowthReserveFloor`'s remark).

So T = 60, 120 and 240 s is about 1, 2 and 4 metabolic times for a grazer, and about 0.4 to 1.9 for a leaf, whose
climb from 0.19 also takes 1.58 T. One T for all gives the fast-living grazer the longer childhood in its own time,
and the leaf the shorter. A rule written in metabolic times instead would make T scale as the body's tissue over its
upkeep, close to West, Brown and Enquist's M^¼ (inference). The screens read the result in both units, world seconds
and metabolic times, so the owner can see which one the winning T is simple in.

## Juvenile strength

A body scaled to a fraction of its adult copies the adult's joint torque capacity (`Power`) unchanged. So a
two-thirds-size juvenile has an adult's torque on smaller limbs. Under a climb, bodies spend minutes at that size, so
the coordinator asked for a switch (the text is b4's, accepted by the coordinator):

- **The switch.** `RunConfig.JuvenileStrengthScales`, env `EVOSIM_JUVENILE_STRENGTH`, off by default. With it on, a
  scaled body's `Power` scales as muscle does. Force goes with cross-section (L²) and the moment arm with L, so torque
  goes as L³, the mass fraction. The idle bill reads `Power`, so it falls with it.
- **Where it acts.** Wherever a body is scaled, through `Phenotype.ScaledFor(config, linear)`: growth, conception, the
  newborn, the module rule, and the checkpoint's rebuild, so a resumed juvenile comes back at its scaled strength.
  The theatre keeps today's scaling until its own piece of work.
- **Nothing else needs scaling** (b4's survey). `Power` is the only absolute strength a part has. Lift is in kg/m³ of
  displaced water. Attack, intake and protection are per m² of part area, toughness per m³, and a grip is the
  anchoring cell's N/m² times area. The drive genes are times and fractions. All of these already grow with the
  geometry `Phenotype.Scaled` re-measures.
- **Its tests.** A still-water test measures a 0.65-fraction juvenile's speed and stroke work a second under the same
  brain, with the switch off and on. The growth screens carry one arm with it on at the winning T (below), so the
  owner's question is measured in the world.

## The machine

Applying growth to the physics took 0.47 % of t3's wall (measured: 55.6 s of 11,761 s, `wallHarnessGrowthMs`). That
was with nearly every body resized once. The harness applies size every `GrowthStepSeconds` (10 s). A leaf climbing
1.58 T would be resized about 10, 19 and 38 times at T = 60, 120 and 240 s, so applying growth might take about 5, 9
and 18 % of the wall (a guess that scales today's cost by the count). Raising `GrowthStepSeconds` while bodies climb,
or resizing only on a 5 % change of length, keeps it near today's.

## The screens that set the numbers

The owner ruled that the time to adulthood and the founders' threshold are settled by measurement, not chosen. Two
groups do it, each pre-registered before it launches, on t3's world (`rounds/env-r62p-t3.ps1`: the thrust arm, the
kill gut, the newborn reserve at 0.5). The gene evolves inside every arm.

**Group 1: the time to adulthood.** Four worlds on seed 6201, to 24,000 world seconds, with G starting at 30 s. Its pre-registration is `logbook/specs/r62gr-prereg/predictions.md`:

| World | T |
|---|---|
| `r62gr-t60-6201` | 60 s |
| `r62gr-t120-6201` | 120 s |
| `r62gr-t240-6201` | 240 s |
| `r62gr-t120s-6201` | 120 s, with the juvenile-strength switch on |

The controls are `r62p-t3-6201` (T = 0, today's growth) and `r62cs-w500-6201`. The deciding numbers are D222's
tests, the share of bred grazers that never ate, their age at death, intake over upkeep, and the leaf stand against
the control's by `overshoot-read.py`. The winning T is the one that passes D222 with the most bred grazers that
eat, set by a rule the pre-registration fixes in advance. Beside them it reads the gene's drift (`grs` on the birth
rows) and each guild's time to adulthood, from the checkpoints. All three run at the pace factor 1 and at about t3's
wall pace, plus the cost of applying growth (the machine, above), so about 3.5 to 4 hours.

**Group 2: the starting threshold.** Three worlds at the winning T, with G starting at 0 (the range's floor, 1 s), 30
and 60 s. The question is whether the start matters once the gene evolves: whether the three converge on one value,
and how long that takes. The juvenile-strength switch is on or off in it by group 1's rule.

The farm's header gives T in both units: world seconds, and the consumer's metabolic time, which it computes from the
world's own cell table (tissue J/m³ over upkeep W/m³).

The world rules are the owner's: the rate law itself, the single-dial τ against the M^¼ form, and the adult breeding
gate. The numbers the screens settle are T and G's start.

## Recommendation and tests

I recommend the single-dial τ, the adult breeding gate, and formats 13 and 14 shared with b4, with T and G's start
left to the two screens.

b4 builds it, on a branch from `pace` with `shape-genes` merged, once the owner rules. The genome format is b4's
work, and the speed agent reviews the economy. Its tests:

- With T = 0, a recorded fixture replays identical, and a format-11 or 12 genome round-trips byte for byte.
- The allowance in one step matches the closed form within rounding, and a newborn at 0.65 reaches the snap at T
  when unlimited.
- With the reserve under G × `StandingWatts`, a body does not grow. The tissue floor binds when it is the larger.
- With T > 0, a juvenile is refused at conception.
- The gene steps on its logarithm, inside its range, and only when its chance is above 0. Species distance adds 0 for
  two genomes without it.
- A format-13 genome with `null` loads. One missing the field is refused. A gene-less genome admitted at T > 0
  carries the start value on its birth row.
- `EnvBinding` carries the four tunables. The two reflection tests cover the hash and the save-and-reload.
