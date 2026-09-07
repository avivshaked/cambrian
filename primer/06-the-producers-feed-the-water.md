# 06 — The producers feed the water

[Piece 04](04-nobody-decides-who-wins.md) ended on a rule that sounded like a slogan and
turned out to be a measuring instrument.

> A trait pays only if the world contains a route from the trait to energy.

Twice a trait had disappeared because the route was missing, first joints and then aimed
swimming. Each time the only signal was the trait going without a word.

This piece is about the third time, which took longest to see and is the one that finally
worked. The trait is eating. The route was there. It was a hundredth as wide as it needed
to be, and nothing in the world said so until we built the instrument that counts it.

## What a stomach is

The world has two ways to earn. A photosynthetic part earns light, less what the bodies
above it shade.

An *absorptive* part earns from dead matter dissolved in the water around it. The primer
calls such a part a stomach, because that is what it is. What it feeds on is the nutrient
field, [`NutrientField.cs`](../src/Evosim.Core/Environment/NutrientField.cs), which corpses
feed and which sinks slowly toward the floor.

A stomach sweeps some volume of water per second and keeps the energy it finds there. That
sweep rate is its *clearance*, and [`DESIGN.md` §5A.2c](../DESIGN.md) owns the number.

A creature carrying only stomachs is a second trophic level. It does not compete for light
at all, and it lives off what the producers leave behind.

A world that holds both, breeding, is a food chain. That is the thing the whole campaign was
reaching for, and the thing the goal rule, [`DECISIONS.md` D063](../DECISIONS.md#d063), was
written to recognise.

For eighteen scored rounds no world held one. Stomachs appeared, since mutation writes a
stomach into a leaf's lineage often enough, and stomachs died. A few times a line grew to
twenty or fifty and collapsed.

The obvious readings were tried and each was wrong in an instructive way. Perhaps the
stomach earned too little per litre, so its *gearing* (D068) was raised tenfold. Perhaps the
field was too thin where it lived, so a floor refuge (D055) was proposed and rejected twice.
Perhaps too many stomachs choked on one field, and a satiation cap (D062) answers that.

All three touch how much a stomach can take from the water that is there. None of them
touches how much arrives.

## The number that was the whole problem

The nutrient field has one income and one outflow. Its income is dead tissue: when a
creature starves, its body becomes energy in the water at the height it died. Its outflow is
feeding. So when a world holds no stomachs at all, the field's slope *is* the income. It can
be read from a column that had been in the report all along.

In the world where the first real absorptive line grew and died
([logbook 0050](../logbook/0050-the-stomachs-gearing.md)), that slope read a few tenths of a
watt. The producers in the same world were earning about seventeen. The second trophic
level was being fed at roughly one percent of the first.

The ledger turned that into a headcount. It is a calculator that runs one body's energy
balance under the world's own breeding rule, without Unity, in seconds
([`DECISIONS.md` D069](../DECISIONS.md#d069)). At that income, it said, a line of about six
stomachs can hold replacement.

The booms to twenty and fifty were eating stored capital, the field laid down by a founding
generation's deaths. Every one of them fell back to a handful once the capital was spent.
The goal asked for ten.

Then the instrument that splits the field's income by source was built, and the income was
worse than the slope had said, because it was *falling*.

Most of the tissue in a mature world is in bodies that have shrunk to the size the economy
allows, and a small corpse is a small meal. The energy per death dropped from tens of
joules, while founders were dying, to a few by the end. A flux that rides on corpses shrinks
with the bodies. That was the point at which the gearing stopped being the suspect.

## Why nothing already built could fix it

This is worth being explicit about, because the three stabilisers looked like remedies and
the ledger says why they were not.

The satiation cap limits how fast one stomach can eat at high density. At the value round 8
used, it holds a stomach's reproductive rate at replacement whatever the density, so a line
cannot grow from one mutant to ten. Set lower, the stomach cannot earn a child's price in a
lifetime at all, because most of that price is the endowment the child is born with. The
clearance toe (D062's other half) raises the density a stomach needs before it earns
anything, without raising what is there to earn. The floor refuge protects a layer these
stomachs do not live in.

Every one of them acts on the demand side. The constraint was supply. A world can be tuned
indefinitely on the wrong side of that line and each tuning will fail for a reason that
sounds specific.

## What the ocean does

Real producers do not only feed the water by dying. Phytoplankton leak a substantial share
of what they fix, while alive, as dissolved organic carbon. That is the input to the
microbial loop, where most of the ocean's primary production goes [PWAH07, p.5].

The leaked share has a name, the percentage of extracellular release, and a literature. It
runs near a fifth of production and stays flat across a 150-fold range of productivity
[MCP05, p.1]. It is higher, near forty percent, in the poorest water [LS11, p.8] [CH20,
p.5].

It does not depend on the size of the cell or its growth stage [LS13, p.1]. So a rule that
leaks a flat fraction is the rule the evidence supports rather than a simplification of it.

There is a second number that explains why one percent was always going to fail. The
efficiency with which energy moves from producers to the animals that eat them is around
thirteen percent in surveyed marine ecosystems [ED21, p.14].

The older ten-percent rule that everyone half-remembers is real, and it was measured between
the second and fourth trophic levels rather than at the base [PC95, p.3]. This world's
second level was being fed at a tenth of that, and the missing nine-tenths is the part that
real producers leak while alive.

So the rule is exudation ([`DECISIONS.md` D070](../DECISIONS.md#d070)). Each step, a
producer deposits a fixed fraction of what it just earned from light into the nutrient field
at its own height and patch.

The leak comes off the producer's own net, so the leaf pays for it. It gets its own counter
in the flux instrument, so the first arm would show deaths and leak as two numbers rather
than one. It defaults to zero, so every run before it replays unchanged.

The fraction is in [`DESIGN.md` §5A.2c](../DESIGN.md) with its citation. It sits inside the
general range and below the poor-water values, on purpose.

The build was gated on two readings before a line of it was written. A grazer-free world had
to read the income the diagnosis inferred, and the richest water any arm had ever had still
had to fail to hold a line. Both came in the same night as the ruling and both held.

The second is the one I would point at: a world with the largest stored field of the
campaign, and three stomach breeders in the whole run. Stock was never the constraint, and
flow was.

## The leak

Two arms ran the leak, both of them seeds that had grown and lost a line. They ran at the
screening step to twenty thousand seconds, so the result would be read past two lifetimes
([logbook 0053](../logbook/0053-the-leak.md)).

The flux instrument reads the leak at thirty to seventy times the corpses. And the stomachs
eat almost all of it as it lands. The standing field in the leak worlds is *lower* than in
the control, which has no stomachs and nothing to spend the leak on.

That is the signature of a chemostat: an inflow and a consumer, and a concentration set by
the consumer rather than by the stock. I had expected a fuller field. What I got was a
fuller population and an emptier field, which is the better result and the one the biology
predicts.

Both seeds held an inherited line above ten for two lifetimes after reaching it, at
forty-six and a hundred and thirty-three by the end. The control world held none at all.

The screen said one thing the prediction had not. The lines were descended from founders
rather than from fresh mutants. They came from stomachs born in the founding lottery, which
the leak kept alive long enough to breed.

That is not a weaker result, since a founder-era stomach is a stomach. But it is a different
mechanism from the one the writing had assumed, and logbook/0054 records the difference.

## The confirmation

Five seeds ran at the fine step, thirty thousand seconds each, scored against the goal as
written and then re-scored under a stricter reading of it
([logbook 0054](../logbook/0054-the-confirmation.md)). Four of the five pass. Inherited
lines of seventy-six to two hundred and twenty-one at the end, all still recruiting,
producers steady, the energy audit closed to the last decimal.

The re-scoring matters more than it sounds. The goal as first written counted absorptive
creatures in aggregate, so a world could pass it with several unrelated stomach lines each
too small to matter.

The reading that survived asks for one *connected clade*: a root, and members joined to it
through an unbroken chain of parents that expressed the trait. That clade has to stay above
ten through the last two lifetimes and still be breeding at the end.

It also stops counting producers by the population total, which had always been the wrong
column. A producer lineage is the count of leaves whose parents were leaves, and the report
now carries it.

Four of five holds under the stricter reading too. One of the four clades has a mutant at
its root, born late in the run, and the other three trace to founders. The goal was never
about where the root came from.

The failing seed is the interesting one. Its last stomachs were sitting in the richest water
of the round, earning a positive balance, holding four times a child's price in reserve, and
they had no children. They were not starving. They were *refused*.

## The dry deep

A child costs energy, which the stomachs had, and matter, which is conserved and which they
did not have where they were.

This world has a fixed stock of matter, and the arithmetic is in
[`DECISIONS.md` D071](../DECISIONS.md#d071). At maturity nearly all of it is locked in
bodies. A conception takes its matter from the parent's own layer, so a stomach at depth
conceives only when matter sinks past it.

A round earlier, marine snow (D067) had slowed the detritus sink so that food would linger
where stomachs live. It had slowed the matter sink with it, for no better reason than that
the two were one knob.

The slow detritus sink is what keeps the leak near the stomachs. The slow matter sink is
what keeps the matter away from them. Released at the producers' layer, it is re-locked into
a new leaf long before it reaches fifteen metres down.

This also re-reads a number every round since D065 had taken as the world's carrying
capacity. The population plateau of seventeen to eighteen hundred is not what the light
supports. It is how many bodies the matter stock can build.

The producers fill it alone. Every refused conception below them is a stomach asking for
matter that is up in the leaves. There are more than a hundred thousand of those per
hundred-second window, in every mature world of the round.

~~The rule that follows lets the two sinks differ, which nothing in their design ever
forbade. Matter is a mineral currency and detritus is organic energy, and they may fall at
different speeds. That is ruled and screening as this is written, rather than confirmed. If
it fails, D071 will say so and this paragraph will be struck rather than rewritten.~~

Struck the same day ([logbook 0055](../logbook/0055-the-dry-deep.md)). The screen showed
that the sink speed was never the lever. Nine-tenths of the world's matter is locked in
bodies at maturity, at either speed. The remaining tenth, spread over the whole column, is
the thin reading every layer gives.

The column is dry from top to bottom. The stomachs sit in the leaves' own band and lose the
contest for each unit that arrives.

What can change that is a rule that changes the size of the free pool. Every such rule also
grows the producers, so it is a decision about how big the world is, which is the owner's.

## The butterfly

A short section on a different kind of finding, because it changed how every arm above was
read.

The physics solver replays bit for bit on one machine under one build, with one more
condition found later: once bodies share the water and touch, only with the physics on a
single thread ([logbook 0069](../logbook/0069-the-shared-world-does-not-replay.md)).
Three identical arms produced identical rows to the last sample
([logbook 0052](../logbook/0052-the-coarse-step.md)), which is a stronger guarantee than the
design had promised itself.

It also means the opposite of what it first suggests. Because nothing is noise, *everything*
is signal. Any change that touches the physics loop even once per step is a butterfly,
whether it is a different timestep, an impulse limiter or a rule that fires on contact. Its
wingspan by five thousand seconds is a fifth of the population and half the larder.

Two arms of one seed therefore cannot tell a change from a realisation. The test of anything
that touches the loop is distributional, seeds against seeds. A screen at the coarse step
buys three times the pace at the cost of being a screen, confirmed at the fine step before
it is believed. That is the shape every result above took.

## What remains

A food chain exists in this world, and it is not yet an ecology.

Movement has never paid. The cost side closed in piece 04, and the prize side is still open:
a reason for a body to go somewhere, sensed and reachable. Every jointed creature in these
worlds is jointed by inheritance rather than by advantage.

Perception reads all seven channels the design specifies (logbook/0062), and nothing has yet
been selected for using them. A stomach that can smell
the field it is drifting past is a different animal from one that waits for the leak to land
on it.

And the matter question is open. The chain was built on an energy economy that balances to
the last joule, and on a matter economy that was measured for the first time this week. The
measurement says the world is as large as its matter allows and no larger.

Whether to make it larger, and how, is the next thing the owner will be asked.

## Sources

| Key | Used here for |
|---|---|
| `[PWAH07]` | The microbial loop: most primary production is respired by bacteria, and dissolved organic carbon is its input |
| `[MCP05]` | Percentage of extracellular release near a fifth of production, flat across a 150-fold productivity range |
| `[LS13]` | Exudation independent of cell size and growth stage, so a flat fraction is the supported rule |
| `[LS11]`, `[CH20]` | The high end of the range in oligotrophic water, and the range the rule sits below |
| `[ED21]` | Producer-to-herbivore transfer efficiency of about thirteen percent |
| `[PC95]` | The ten-percent rule's provenance: measured between trophic levels two and four, not at the base |

Seven things here are mine rather than the literature's.

- **The one-percent-against-ten-percent comparison** is this project's inference. The
  world's
  second-level income is measured ([logbook 0050](../logbook/0050-the-stomachs-gearing.md))
  and the transfer efficiencies are the literature's. Putting them side by side as the
  diagnosis is recorded in [`DECISIONS.md` D070](../DECISIONS.md#d070).

- **"A flux that rides on corpses shrinks with the bodies"** was read from one arm's flux
  instrument, and the generalisation is ours.

- **The chemostat reading of the lower standing field** is inferred from two arms and a
  control. The mechanism itself is textbook.

- **The demand-side and supply-side framing of the three stabilisers** is the author's. The
  ledger's numbers behind it are reproducible.

- **The matter cap as the true population plateau** is ours, and the arithmetic is in D071.
  It
  reinterprets a number that two earlier decisions read differently. The cap held up in the
  screen, and the lever D071 proposed for it did not.

- **"Everything is signal"** is the project's own operational rule, in
  [`DESIGN.md` §7](../DESIGN.md). The bit-for-bit replay under it is measured.

- **The ruling that a mutant root is not required** is a design choice by the owner rather
  than a finding. The primer reports it because the earlier writing had assumed otherwise.

## Where it is

| file | what it holds |
|---|---|
| [`Metabolism.cs`](../src/Evosim.Core/Ecosystem/Metabolism.cs) | the leak, charged against a producer's net before anything else |
| [`World.cs`](../src/Evosim.Core/Ecosystem/World.cs) | the flux totals the instrument reads: deposited, exuded, taken |
| [`NutrientField.cs`](../src/Evosim.Core/Environment/NutrientField.cs) | the field the leak lands in, and the sink that moves it |
| [`LedgerForecast.cs`](../src/Evosim.Core/Ecosystem/LedgerForecast.cs) | the calculator: one body, one world, break-even and R0 without Unity |
| [`ledger.ps1`](../scripts/ledger.ps1), [`analyse-arm.ps1`](../scripts/analyse-arm.ps1) | asking the ledger, and reading a run by named column |
| [`DESIGN.md`](../DESIGN.md) §5A.2c, §6.2, §7 | the rule and its number, then the step policy and the determinism rule |
| [`DECISIONS.md`](../DECISIONS.md) D063, D069, D070, D071 | the goal as amended; compute, screen, confirm; exudation; matter at depth |
