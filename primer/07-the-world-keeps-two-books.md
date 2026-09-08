# 07 — The world keeps two books

Every number this project reports about its creatures rests on one habit: the world counts
what it has, every half second, and refuses to let the count drift. This piece is about
that habit. It is the oldest instrument in the campaign and the newest, and the reason a
food web assembling itself could be believed rather than suspected.

## What is conserved

The world has two quantities that nothing inside it can make or unmake.

The first is energy. Sunlight enters at the surface and is fixed by photosynthetic tissue.
From there it is held in a body's reserve or spent as upkeep, work and nerve. It is paid
out at a birth, lost in the transfer when a stomach eats, and returned to the water as
detritus when a body dies. Every one of those is a movement of joules from one place to another. None of
them is a place where joules appear.

The second is matter. A body is built from it at conception and returns it at death, and it
is neither eaten nor spent. Until [D074](../DECISIONS.md#d074) the world's matter was a
closed stock; since then a vent can add some and the floor can bury some, and those two are
the only doors.

## The first book

The energy book is a single identity, kept in
[`World.cs`](../src/Evosim.Core/Ecosystem/World.cs) as three running totals. Everything
that has ever entered. Everything that has ever left. Everything standing in the world now,
which is the reserves in living bodies plus the joules in the water. The residual is
the first less the second less the third, and it has to be zero. The run report prints it
as `audit`, as a percentage of what entered, on every row.

[`DESIGN.md` §5A.2](../DESIGN.md) promised this before it was true: sun in, metabolism out,
everything else conserved. For a while the middle clause was an aspiration, because two
things were free. A body cost nothing to build, and a corpse was worth nothing. §5A.2c
closed both with one number, the energy a cubic metre of tissue is worth. The parent pays
it at a birth and the water receives it at a death. The two have to be the same
figure or a birth-and-death cycle creates energy, so both call one method.

What the identity forbids is any operation that is not a transfer. A stomach cannot read a
density and convert it to joules unless the same joules leave the water. A sun cannot shine
on a body without the light being counted in. A field cannot hold a value that is adjusted;
it holds an amount that is moved. That rule is why the water could later be rebuilt on
vertices at all ([piece 08](08-the-water-as-vertices.md)): the vertices had to carry
amounts, so that a take had somewhere to subtract from and a merge lost nothing.

## What the first book found

The audit was built as an instrument, not as a proof, and it earned its keep by failing.

The first thing it caught was outside the world entirely. In the physics spike the energy
a creature spent did not match the energy the water took out of it, and the gap was
roughly ten times the fluid drag. Three explanations were tried and wrong, and the fourth
was two damping terms the physics engine applies by default and documents nowhere,
removing energy from every joint invisibly
([logbook 0008](../logbook/0008-the-energy-audit.md)). Nothing had looked broken and the creatures swam; only the book was off.

The second was the sun. The first ecosystem loop took an irradiance and multiplied it by
lit area, and the light a body received did not reduce the light anyone else received.
That is an infinite subsidy, and a population under it is unbounded
([logbook 0011](../logbook/0011-the-sun-was-infinite.md)). The audit could not see it
directly, because the sun was counted in as it was consumed. What it saw was a world whose
income had no ceiling. The fix was to make light a stock that shading depletes.

The third is the one it exists for and has never had to catch: a physics exploit that
becomes a food source. A body that gains energy from a contact, a joint or a numerical
error is the classic failure of this whole genre, and [`DESIGN.md` §11.2](../DESIGN.md)
lists it as the takeover the audit guards against. Every run since has closed at 0.0000%,
including the ones in which a food chain assembled itself
([piece 06](06-the-producers-feed-the-water.md)). That is what lets those runs be read as
ecology rather than as a leak with a story attached.

## The second book

For most of the campaign the world kept one book, and it was enough,
because matter was a closed stock that only moved between bodies and water in lockstep
with energy. D074 opened it: a vent adds matter, the floor buries it, and the world's
size becomes a flow rather than a constant. From then on matter has its own identity. What
was there at the start, plus what flowed in, less what was buried, must equal what stands
in the water and the bodies. `World.cs` keeps the four totals, and since 2026-09-07 the run
report prints the residual as `mat resid` beside `audit`.

It was added the week the water was rebuilt on vertices, and it caught a fault the energy
book could not see. The new field's take delivered less than its gate had promised, and
conception booked the full price into the child anyway. Every birth created a few units
of matter from nothing. The energy audit read 0.0000% on every row while it happened,
because no joules were involved: matter is not energy, and a book that counts one
substance cannot notice the other. The statistics file showed the world's standing matter
rising from 6,000 units to 28,520 in 3,000 s
([logbook 0074](../logbook/0074-the-water-as-vertices.md)). Once the residual had a column
of its own, the next fault of that kind was one glance wide, and two more were found and
fixed the same night.

That is the argument for two books rather than one. They count different things, and a
world can balance the one to the last joule while it is wrong about the other.

## What a closed book buys

It buys nothing about the creatures. A world whose books balance can still be a bad world, and
this one has been several kinds of bad world with both books at zero. What it buys is the
right to believe the other columns. When a stomach line appears, the energy it lives on
came from somewhere the book can name. When the population reaches a ceiling, the ceiling
is matter and not an accounting artefact. When a swimmer fails to pay its way, it failed
in a world that did not cheat in either direction.

The habit costs almost nothing to keep, once the rule that every operation is a transfer
is in the code. It costs a great deal to add later. By then every mechanism has been
written as a value that is adjusted rather than an amount that is moved, and each of those
is a place where the book can be made to lie.

## What here is inference

- **The framing of the two identities as "books"** is the author's. The identities
  themselves are the code's, and the residuals are printed on every run.
- **"Every operation is a transfer" as the rule behind both** is the author's reading of
  §5A.2c's design and of how the vertex field was made to close. The decisions record the
  reasons and never the slogan.
- **That the sun's subsidy was a thing the audit "saw indirectly"** is the author's
  characterisation; logbook 0011 records what was found and how.
- **The cost of adding an audit late** is the author's judgement and has not been measured.

## Where it is

| file | what it holds |
|---|---|
| [`World.cs`](../src/Evosim.Core/Ecosystem/World.cs) | both identities: energy in, out and standing; matter initial, influxed, buried and standing |
| [`EvolutionRun.cs`](../unity/Assets/Evosim/Sim/Editor/EvolutionRun.cs) | the `audit` and `mat resid` columns, printed on every row |
| [`DESIGN.md`](../DESIGN.md) §5A.2, §5A.2c, §11.2 | the promise, the two prices that made it true, and the takeover it guards against |
| [`DECISIONS.md`](../DECISIONS.md) D074, D083 | the open matter budget; the water as vertices, with the amendments the second book forced |
| [logbook 0008](../logbook/0008-the-energy-audit.md), [0011](../logbook/0011-the-sun-was-infinite.md), [0074](../logbook/0074-the-water-as-vertices.md) | what each book found, on the day |
