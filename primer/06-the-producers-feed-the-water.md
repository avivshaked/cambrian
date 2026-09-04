# 06 — The producers feed the water

[Piece 04](04-nobody-decides-who-wins.md) ended on a rule that sounded like a slogan and
turned out to be a measuring instrument: *a trait pays only if the world contains a route
from the trait to energy.* Twice a trait had disappeared because the route was missing —
joints, then aimed swimming — and each time the only signal was the trait quietly going.

This piece is about the third time, which took longest to see and is the one that finally
worked. The trait is eating. The route was there. It was a hundredth as wide as it needed
to be, and nothing in the world said so until we built the instrument that counts it.

## What a stomach is

The world has two ways to earn. A photosynthetic part earns light, less what the bodies
above it shade. An *absorptive* part — the primer calls it a stomach, because that is what it
is — earns from dead matter dissolved in the water around it: the nutrient field,
[`NutrientField.cs`](../src/Evosim.Core/Environment/NutrientField.cs), which corpses feed and
which sinks slowly toward the floor. A stomach sweeps some volume of water per second and
keeps the energy it finds there; the sweep rate is what the record calls its *clearance*,
and [`DESIGN.md` §5A.2c](../DESIGN.md) owns the number.

A creature carrying only stomachs is a second trophic level. It does not compete for light
at all. It lives off what the producers leave behind, and a world that holds both, breeding,
is a food chain — the thing the whole campaign was reaching for and the thing the goal rule,
[`DECISIONS.md` D063](../DECISIONS.md#d063), was written to recognise.

For eighteen scored rounds no world held one. Stomachs appeared — mutation writes a stomach
into a leaf's lineage often enough — and stomachs died. A few times a line grew to twenty or
fifty and collapsed. The obvious readings were tried and each was wrong in an instructive
way: perhaps the stomach earned too little per litre (its *gearing*, D068, was raised
tenfold), perhaps the field was too thin where it lived (a floor refuge, D055, rejected
twice), perhaps too many stomachs choked on one field (a satiation cap, D062). All three
touch how much a stomach can take from the water that is there. None of them touches how
much arrives.

## The number that was the whole problem

The nutrient field has one income and one outflow. Its income is dead tissue: when a
creature starves, its body becomes energy in the water at the height it died. Its outflow is
feeding. So when a world holds no stomachs at all, the field's slope *is* the income, and
you can read it from a column that had been in the report all along.

In the world where the first real absorptive line grew and died
([logbook 0050](../logbook/0050-the-stomachs-gearing.md)), that slope read a few tenths of a
watt. The producers in the same world were earning about seventeen. The second trophic
level was being fed at roughly one percent of the first.

The ledger — a calculator that runs one body's energy balance under the world's own breeding
rule, without Unity, in seconds ([`DECISIONS.md` D069](../DECISIONS.md#d069)) — turned that
into a headcount: at that income a line of about six stomachs can hold replacement. The
booms to twenty and fifty were eating stored capital, the field laid down by a founding
generation's deaths, and every one of them fell back to a handful once the capital was
spent. The goal asked for ten.

Then the instrument that splits the field's income by source was built, and the income was
worse than the slope had said, because it was *falling*. Most of the tissue in a mature
world is in bodies that have shrunk to the size the economy allows, and a small corpse is a
small meal: the energy per death dropped from tens of joules while founders were dying to a
few by the end. A flux that rides on corpses shrinks with the bodies. That was the point at
which the gearing stopped being the suspect.

## Why nothing already built could fix it

This is worth being explicit about, because the three stabilisers looked like remedies and
the ledger says why they were not.

The satiation cap limits how fast one stomach can eat at high density. At the value round 8
used it holds a stomach's reproductive rate at exactly replacement whatever the density — a
line cannot grow from one mutant to ten. Set lower, the stomach cannot earn a child's price
in a lifetime at all, because most of that price is the endowment the child is born with.
The clearance toe (D062's other half) raises the density a stomach needs before it earns
anything, without raising what is there to earn. The floor refuge protects a layer these
stomachs do not live in.

Every one of them acts on the demand side. The constraint was supply. A world can be tuned
indefinitely on the wrong side of that line and each tuning will fail for a reason that
sounds specific.

## What the ocean does

Real producers do not only feed the water by dying. Phytoplankton leak a substantial share of
what they fix, while alive, as dissolved organic carbon — the input to the microbial loop,
which is where most of the ocean's primary production actually goes
[PWAH07, p.5]. The leaked share has a name, the percentage of extracellular release, and a
literature. It runs near a fifth of production and stays flat across a 150-fold range of
productivity [MCP05, p.1]; it is higher, near forty percent, in the poorest water
[LS11, p.8] [CH20, p.5]; and it does not depend on the size of the cell or its growth
stage, so a rule that leaks a flat fraction is the rule the evidence supports rather than a
simplification of it [LS13, p.1].

There is a second number that explains why one percent was always going to fail. The
efficiency with which energy moves from producers to the animals that eat them is around
thirteen percent in surveyed marine ecosystems [ED21, p.14], and the older ten-percent rule
that everyone half-remembers is real but was measured between the second and fourth trophic
levels, not at the base [PC95, p.3]. This world's second level was being fed at a tenth of
that, and the missing nine-tenths is exactly the part that real producers leak while alive.

So the rule is exudation ([`DECISIONS.md` D070](../DECISIONS.md#d070)): each step, a
producer deposits a fixed fraction of what it just earned from light into the nutrient field
at its own height and patch. Off its own net, so the leaf pays for it. Its own counter in the
flux instrument, so the first arm would show deaths and leak as two numbers rather than one.
Default zero, so every run before it replays unchanged. The fraction is in
[`DESIGN.md` §5A.2c](../DESIGN.md) with its citation; it sits inside the general range and
below the poor-water values, on purpose.

The build was gated on two readings before a line of it was written: that a grazer-free world
really did read the income the diagnosis inferred, and that the richest water any arm had
ever had still failed to hold a line. Both came in the same night as the ruling and both
held. The second is the one I would point at — a world with the largest stored field of the
campaign, and three stomach breeders in the whole run. Stock was never the constraint.
Flow was.

## The leak

Two arms, both seeds that had grown and lost a line, at the screening step, run to twenty
thousand seconds so the result would be read past two lifetimes
([logbook 0053](../logbook/0053-the-leak.md)).

The flux instrument reads the leak at thirty to seventy times the corpses. And the stomachs
eat almost all of it as it lands — the standing field in the leak worlds is *lower* than in
the control, which has no stomachs and nothing to spend the leak on. That is the signature of
a chemostat: an inflow and a consumer, and a concentration set by the consumer rather than
by the stock. The reading I had expected was a fuller field; the reading I got was a fuller
population and an emptier field, which is the better result and the one the biology
predicts.

Both seeds held an inherited line above ten for two lifetimes after reaching it, at forty-six
and a hundred and thirty-three by the end. The control held none.

One thing the screen said that the prediction had not: the lines were not descended from
fresh mutants. They came from stomachs that had been born in the founding lottery and that
the leak kept alive long enough to breed. That is not a weaker result — a founder-era stomach
is a stomach — but it is a different mechanism from the one the writing had assumed, and the
record says so.

## The confirmation

Five seeds at the fine step, thirty thousand seconds each, scored against the goal as written
and then re-scored under a stricter reading of it
([logbook 0054](../logbook/0054-the-confirmation.md)). Four of five pass. Inherited lines of
seventy-six to two hundred and twenty-one at the end, all still recruiting, producers steady,
the energy audit closed to the last decimal.

The re-scoring matters more than it sounds. The goal as first written counted absorptive
creatures in aggregate; a world could pass it with several unrelated stomach lines each too
small to matter. The reading that survived asks for one *connected clade* — a root, and
members joined to it through an unbroken chain of parents that expressed the trait — alive
above ten through the last two lifetimes, still breeding at the end. It also stops counting
producers by the population total, which had always been the wrong column: a producer
lineage is the count of leaves whose parents were leaves, and the report now carries it.
Four of five holds under the stricter reading too. One of the four clades has a mutant at
its root, born late in the run; the other three trace to founders. The goal was never about
where the root came from, and the record does not pretend it was.

The failing seed is the interesting one. Its last stomachs were sitting in the richest water
of the round, earning a positive balance, holding four times a child's price in reserve, and
they had no children. They were not starving. They were *refused*.

## The dry deep

A child costs energy, which the stomachs had, and matter, which is conserved and which they
did not have where they were.

This world has a fixed stock of matter — the arithmetic is in
[`DECISIONS.md` D071](../DECISIONS.md#d071) — and at maturity nearly all of it is locked in
bodies. A conception takes its matter from the parent's own layer, so a stomach at depth
conceives only when matter sinks past it. A round earlier, marine snow (D067) had slowed the
detritus sink so that food would linger where stomachs live, and had slowed the matter sink
with it, for no better reason than that the two were one knob. The slow detritus sink is what
keeps the leak near the stomachs. The slow matter sink is what keeps the matter away from
them: released at the producers' layer, it is re-locked into a new leaf long before it
reaches fifteen metres down.

This also re-reads a number every round since D065 had taken as the world's carrying
capacity. The population plateau of seventeen to eighteen hundred is not what the light
supports. It is how many bodies the matter stock can build. The producers fill it alone,
and every refused conception below them — a hundred thousand and more per hundred-second
window in every mature world of the round — is a stomach asking for matter that is up in the
leaves.

~~The rule that follows lets the two sinks differ, which nothing in their design ever forbade:
matter is a mineral currency and detritus is organic energy, and they may fall at different
speeds. That is ruled and screening as this is written, not confirmed; if it fails, D071
will say so and this paragraph will be struck rather than rewritten.~~

Struck the same day ([logbook 0055](../logbook/0055-the-dry-deep.md)). The screen showed
that the sink speed was never the lever: nine-tenths of the world's matter is locked in
bodies at maturity, at either speed, and the remaining tenth spread over the whole column
is the thin reading every layer gives. The column is dry from top to bottom, and the
stomachs sit in the leaves' own band and lose the contest for each unit that arrives. What
can change that is a rule that changes the size of the free pool — and every such rule also
grows the producers, so it is a decision about how big the world is, which is the owner's.

## The butterfly

A short section on a different kind of finding, because it changed how every arm above was
read.

The physics solver replays bit for bit on one machine under one build; three identical arms
produced identical rows to the last sample ([logbook 0052](../logbook/0052-the-coarse-step.md)).
That is a stronger guarantee than the design had promised itself. It also means the opposite
of what it first suggests: because nothing is noise, *everything* is signal, and any change
that touches the physics loop even once per step — a different timestep, an impulse limiter,
a rule that fires on contact — is a butterfly whose wingspan by five thousand seconds is a
fifth of the population and half the larder. Two arms of one seed cannot tell a change from
a realisation. The honest test of anything that touches the loop is distributional, seeds
against seeds, and a screen at the coarse step buys three times the pace at the cost of
being exactly that: a screen, confirmed at the fine step before it is believed. That is
the shape every result above took.

## What remains

A food chain exists. It is not yet an ecology.

Movement has never paid. The cost side closed in piece 04; the prize side — a reason for a
body to go somewhere, sensed and reachable — is still open, and every jointed creature in
these worlds is jointed by inheritance rather than by advantage. Perception reads four
channels of the seven the design specifies. A stomach that can smell the field it is drifting
past is a different animal from one that waits for the leak to land on it.

And the matter question is open. The chain was built on an energy economy that balances to
the last joule and a matter economy that was measured for the first time this week, and the
measurement says the world is as large as its matter allows and no larger. Whether to make
it larger, and how, is the next thing the owner will be asked.

## Sources

| Key | Used here for |
|---|---|
| `[PWAH07]` | The microbial loop: most primary production is respired by bacteria, and dissolved organic carbon is its input |
| `[MCP05]` | Percentage of extracellular release near a fifth of production, flat across a 150-fold productivity range |
| `[LS13]` | Exudation independent of cell size and growth stage, so a flat fraction is the supported rule |
| `[LS11]`, `[CH20]` | The high end of the range in oligotrophic water, and the range the rule sits below |
| `[ED21]` | Producer-to-herbivore transfer efficiency of about thirteen percent |
| `[PC95]` | The ten-percent rule's provenance: measured between trophic levels two and four, not at the base |

**Mine, not the literature's:**

- **The one-percent-against-ten-percent comparison.** The world's second-level income is
  measured ([logbook 0050](../logbook/0050-the-stomachs-gearing.md)); the transfer
  efficiencies are the literature's; putting them side by side as the diagnosis is this
  project's inference, recorded in [`DECISIONS.md` D070](../DECISIONS.md#d070).
- **"A flux that rides on corpses shrinks with the bodies."** Read from one arm's flux
  instrument; the generalisation is ours.
- **The chemostat reading of the lower standing field.** The mechanism is textbook; that it is
  what the leak worlds show is inferred from two arms and a control.
- **The demand-side / supply-side framing of the three stabilisers.** The ledger's numbers are
  reproducible; the framing is the author's.
- **The matter cap as the true population plateau.** The arithmetic is in D071 and is ours;
  it reinterprets a number that two earlier decisions read differently. The cap held up in
  the screen; the lever D071 proposed for it did not.
- **"Everything is signal."** The bit-for-bit replay is measured; the operational rule drawn
  from it is the project's own, in [`DESIGN.md` §7](../DESIGN.md).
- **The ruling that a mutant root is not required.** A design choice by the owner, not a
  finding; the primer reports it because the earlier writing had assumed otherwise.

## Where it is

- [`Metabolism.cs`](../src/Evosim.Core/Ecosystem/Metabolism.cs) — the leak, charged against a producer's net before anything else
- [`World.cs`](../src/Evosim.Core/Ecosystem/World.cs) — the flux totals the instrument reads: deposited, exuded, taken
- [`NutrientField.cs`](../src/Evosim.Core/Environment/NutrientField.cs) — the field the leak lands in, and the sink that moves it
- [`LedgerForecast.cs`](../src/Evosim.Core/Ecosystem/LedgerForecast.cs) — the calculator: one body, one world, break-even and R0 without Unity
- [`ledger.ps1`](../scripts/ledger.ps1) and [`analyse-arm.ps1`](../scripts/analyse-arm.ps1) — asking the ledger, and reading a run by named column
- [`DESIGN.md`](../DESIGN.md) §5A.2c for the rule and its number, §6.2 and §7 for the step policy and the determinism rule
- [`DECISIONS.md`](../DECISIONS.md) D063 (the goal, as amended), D069 (compute, screen, confirm), D070 (exudation), D071 (matter at depth)
