# Proposal: the next base round (46), five rules and what each is for

*Fable, 2026-09-23, after round 45's read (logbook/0114) and the light-exposure ruling of the
same morning. This is the one text for the owner to rule the next base round on. Each rule
is a world rule and is the owner's; the build order, the fixtures and the pre-registration
are mine once ruled. Absorbed into DECISIONS.md on ruling, then deleted.*

## Why a base round, and why these five

Round 45 said three things about the world that no round on the same rules can change. No
consumer can found, because the world is empty of its food at the start and a consumer
founder starves in a minute (J1, 0 of 3, and the corpses it left uneaten). A body's angle to
the sky is never read (the owner's observation; `fable-propose-light-exposure.md`), so a leaf
born on edge earns what a flat one does and nothing selects a pose. And the size of a part
is unbounded (D107), so a self-copying leaf grows into a fourteen-metre fan that the
one-sphere contact turns into a bulldozer (logbook/0113, 0114's launch note). Each of the
five rules below answers one of those, and together they are a new realisation of every
seed, so they land in one base round with one set of fixtures. A sixth joined them on the
afternoon of 2026-09-23, the beach (rule 6 below), from the first stomach screen's reading
that the eaters' food is on the floor forty metres under them. The mushroom reef the owner
described the same afternoon (a rock column with a cap that shades and a top that holds
snow in the light) is the round after: a solid with an overhang needs a masked column in the
grid, a shade cast by a cap and the current deflected round a stem, and a rule that shapes
the light would confound the exposure rule's first reading.

## 1. Light by exposure, with the one-part leaf's way to lie flat

Ruled on 2026-09-23 ("I'll support your recommendations") with two conditions, both
answered in `fable-propose-light-exposure.md`: the cost is microseconds a metabolic step, and
a body needs a way to affect its angle. Three ways exist (a float part above tissue, rising
or sinking under the panels, a joint with the up sense). The fourth, for the one-part leaf,
**a heritable offset of a part's centre of buoyancy along its thinnest axis** (ruled
"agreed" on 2026-09-23 afternoon, D111), as a fraction of that half-extent, so the buoyant face turns up.
It is a genome field (a format bump), mutable and priced like lift, acting only where the
node carries lift above zero. My recommendation is yes, because without it the rule kills
the leaves that were born wrong and rewards nothing they can do.

## 2. A consumer that can found

The fact: at 100 s the charged field holds eleven units over a million cells, the snow
reaches 80 to 110 kJ only by 5,000 s, and a consumer founder lives 20 to 55 s on its
reserve. So the founding lottery draws consumers into a world with nothing to eat, and the
floor closes at 3,000 s before there is any. Three forms:

- **A second founding window (recommended).** Founders arrive again when there is food:
  from `ConsumerFoundingAtSeconds` (6,000 s in the pre-registration; the snow is at its
  plateau) the floor draws founders that carry intake, `ConsumerFoundingCount` of them
  (40, the first founding's size) over `ConsumerFoundingWindowSeconds` (1,000 s), into
  the crowd as it is. It is the larva arriving on the reef, and it is what the campaign's
  inoculation route already does by hand. Every founder is still a random genome; only the
  time and the guild of the draw change.
- Intake drawn on non-consumer founders, so a leaf that also eats can carry the mouth
  through the empty start. Cheaper, but it founds mixotrophs and never a consumer, and
  round 45's reading was of the consumer.
- A larger reserve for a consumer founder, so it lasts to the snow. It would need a
  hundredfold margin to reach 5,000 s, which is a founder that is not a founder.

## 3. The support cost

D107 ruled no hard bound on a part's size and said the price is the next proposal. The
screen (`logbook/specs/r45-read/support-cost-screen.txt`) at the ledger's 10 W per m² lit and
3 W per m³ standing: a cost of **price × lit area × distance² from the root**, at 0.1 W per
m² per m², puts round 44's giant at −179 W where it earned 618, and leaves a 1.5 m kelp of
four leaf parts 86% of its income and a two-part half-metre body 86%; a copy's own support
equals its own income at sqrt(10 / price) metres, ten metres at that price. The linear form
needs 1 W per m² per m to sink the giant and takes 21% from the kelp, so the square is the
better separator. It is an upkeep term per part, charged in the metabolic step from the
developed body's geometry (the distance is the part's centre from the root's origin, fixed
at development and scaled with growth), and a tunable at 0 is the recorded world.
**Ruling asked: the square form at 0.1 W per m² per m².**

## 4. Per-part contact

The farm's contact is a soft push between one bounding sphere a body, so a fan of seven
leaves is a sphere twenty metres across that shoves every neighbour. The change: one sphere
a part, entered in the grid as spike3's kernel enters spheres, the push between the nearest
part pair, and the mouth's contact read on the part that touches (which the mouth already
asks for: it kills a part). The census columns keep their names and read pairs of parts.
The cost is the entry count times the parts a body, about two on the record, which the
contact kernel's occupancy says the card and the CPU both carry. A tunable, off by default, so
every recorded world replays and the manifest says which model ran (the build spec is
`logbook/specs/per-part-contact-spec.md`). **Ruling asked:
yes.**

## 5. The two pre-registration repairs

Not world rules, mine, listed so the round's design is in one place. J2's twenty consecutive
windows of kills is re-asked on the same budget as **twenty windows with a kill inside the
last 10,000 s**, which round 45 seed 3 would have met on its slope; and J4's protection
before attack takes a **crowd floor of 500 alive** before either share counts, so a
founder-era mutant of one body in forty does not decide it.

## What the round costs

Every tunable refuses every earlier config (rounds 41 to 45 included) and the genome field
refuses every snapshot, so the three fixtures and the three checkpoints are re-recorded once
on the finished build, the inocula re-extracted, and the Farm tests' hash re-pinned. The
machine: three seeds at five threads as round 45 ran, at the same 22,000 m² and 15,000
units, about eight hours a seed. The GPU port continues in parallel on the CPU device's
transcription and takes the machine for its timings between the round's reads.

## 6. The beach: the floor raised to the surface along one side

The owner's rule, proposed on the afternoon of 2026-09-23 after the stomach screen
(`logbook/specs/beach-spec.md`): "what we need is the gradient floor back, and finally put
in the beach ... a point where the floor sits closer to the surface, so the initial
evolving stomachs still have access to the snow." The gradient floor has been on since
round 39 (D092's tilt at 30 m in a 45 m tank), with its shallow arc 18 m under the band the
crowd lives in. The beach raises that tilt to about 96 m, the plane that breaks the surface
at the shore, and clamps the floor at a shoal a metre under the surface, so no column is
ever dry and no body is ever on sand (the rules for a body in air stay the terrestrial
round's). The floor then lies inside the founders' 12 m over about a tenth of the disc
along one side, and the snow that settles there stays in the top cell, in full light. Two
tunables (`BedShoreDepthMetres`, `BedShoreFadeMetres`), both 0 = the recorded world; the
current's potential fades to nothing over the shoal so the floor-following map's stretch is
not a jet at the shore. The recommendation is the beach in this round with the shore at 1 m
and the fade at 15 m, and the consumer's second window (rule 2) placed over the shelf by
rule, so that the forty consumers of the window land where the snow lies in the light
rather than one in ten of them. What it does not do: widen the shelf with a second slope
(the plane's tenth of the disc is read first), or move the snow downslope (settling stays
vertical).

## The questions

1. The buoyancy offset as a genome field: ruled yes (D111).
2. The consumer's founding: the second window (recommended), intake on non-consumer
   founders, or the larger reserve.
3. The support cost: the square form at 0.1 W per m² per m², or a price of the owner's.
4. Per-part contact: yes or no.
5. The reef held for round 47: agreed in conversation (2026-09-23 afternoon, "we don't
   have to do both in the same run"); the mushroom shape is the owner's and its spec follows
   the beach's.
6. The beach in this round at a 1 m shoal and a 15 m fade, with the second founding window's
   cohort placed over the shelf by rule: yes, or the beach alone with the cohort placed as
   the first founders were.
