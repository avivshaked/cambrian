# Proposal: the soup first, and the leaf has to be invented

*2026-09-18, night. From the owner's remark that the photosynthetic founder is an unfair
one: heterotrophs came first, on abiotic organics, and photosynthesis is the invention
evolution took longest over. For the owner's ruling, after round 41b's first read; the
decision point is round 41b's eater reading at 10,000 s (HANDOFF). Absorbed into
DECISIONS.md on ruling, then deleted.*

## 1. The question

Every round so far founds the world with leaves and asks the eaters to found on the
leaves' leavings, which is the lottery rounds 38 to 41 keep losing. This round turns it
round. The founders are eaters only, living on a supply of charged matter that stands in
for the prebiotic soup, and photosynthesis is not given: it has to appear by mutation in
an eater crowd and earn the lit water against a soup that is free. The round asks three
things. Whether an absorptive crowd sustains itself on the soup at all. Whether the leaf,
when it appears, spreads. And what the crowd looks like once it has: leaves in the light,
eaters on their leavings, the soup no longer needed, which is the chain the campaign has
been trying to found from the other end.

## 2. The world

Round 41b's world with three changes.

**Founders are eaters.** `RandomGenomeOptions.FounderCellTypes` is the founders' guild
draw, two leaves to one absorptive to one consumer today, and it has no launch knob. One
is added, `EVOSIM_FOUNDER_TYPES` (a comma list of cell type ids; the header prints
`founders absorptive,consumer`), and this round draws from absorptive and consumer only.
The body cell types a mutation can draw from stay the full set, so the leaf is reachable
at `EVOSIM_CELLTYPE_MUTATION` 0.005 per birth as now; whether that is too rare to see in
30,000 s is priced below.

**The soup is an influx of charged matter.** D074's `MatterInfluxPerSecond` deposits
*spent* units at the surface, which feeds a leaf and nothing else. This round needs
charged units, marine snow arriving from outside, so the influx gains a state:
`MatterInfluxState` (`Spent`, the default and every recorded run; `Charged`, deposited
into the charged field at the surface at ρ joules a unit, booked as influx in both books
exactly as a founder is). Header `matter in 2/s charged at surface`. The identity already
carries `MatterInfluxedTotal`; nothing about the books changes.

**The soup fades.** A soup that never runs out is a world with a free lunch forever, and
the leaf never has to win. A soup that stops dead starves the founders before a leaf
exists. So the influx is a schedule: `MatterInfluxHalfLifeSeconds` (0 = constant, every
recorded run), the rate halving every so many seconds from t=0. The world then closes on
its own, and the question of §1 is asked by the world's own clock: a leaf that appears
before the soup is thin takes over, and a crowd that never invents one starves as the
soup fades, which is the honest outcome.

Spent matter for the leaf comes from the eaters' burning, as in round 41b, so the
treadmill is there from the first second, driven by heterotrophs.

## 3. Pricing, before any of it is built

The ledger prices an absorptive founder against a charged density the way it prices a
leaf against light: at what standing snow does a founder's R0 reach one, and how long is
its first child. The influx is then set so that the standing snow at the surface, the
influx over the remineralisation rate over the lit volume, sits above that density with
room, and the half-life so that the soup is a tenth of that by about 15,000 s. The leaf's
arrival is a count: births by 15,000 s times 0.005 times the share of cell-type mutations
that draw a leaf, read from round 41b's own birth count. If that number is under about
five, the mutation rate for this round is raised and the header says so, because a round
that cannot show its own event is not a round.

## 4. What it costs

Two knobs and a schedule, all in Core and the harness, so `simHash` and `coreHash` move
and the round lands between rounds. A day of building with tests: the charged influx's
identity test (both books close with it on), the schedule's integral, the founder draw's
refusal of an unknown id, the header tokens, and the reflection guards for the new
tunables. The ledger gains nothing: it already takes a charged density.

## 5. What it would decide

Rung 1 from the other end: a food chain founded by its consumers. If the leaf appears and
takes the light, the campaign has a world in which producers were invented rather than
given, which is a finding and not an arrangement (the remark in `GenomeFactory` that
handing the world to photosynthesis would make "plants came first" an arrangement was
written for exactly this). If the eaters starve as the soup fades with no leaf, the
mutation is too rare or the leaf does not pay against eaters at the surface, and the
read says which. Either way the eaters' own economics are read without the founding
lottery in front of them, which is what round 41b may show is the block.

## 6. Open questions for the ruling

- Whether the soup fades (§2) or stays; the proposal says fades.
- Whether the founders are absorptive only, or absorptive and consumer; the proposal says
  both, since D097's mouth is the consumer's and this is its world.
- Whether the round runs before or after the mouth (D097's queue), since a consumer
  founder without a mouth is a filter feeder of snow, which is fine for §1 and thin for
  §5.
