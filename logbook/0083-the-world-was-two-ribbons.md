# 0083 — The world was two ribbons

**2026-09-10**  ·  the theatre's first look at a growth-build run; what thirty-three rounds could not show; the dispersal fix started the same hour

The owner opened round 33 seed 3 in the theatre at midday, the first run the current build
could replay, and the picture at 1,834 s was two vertical ribbons about a metre wide,
running from the surface to twenty-five metres down, with nothing anywhere else in a box
twenty metres long. Two hundred and eighty-six bodies, fifty of them eaters, in two
columns. The owner's words: "what you have now is not a world. it's a weird 2d rules in a
3d world."

## Why the world looks like that

Three rules, each ruled on its own day for a reason that held on that day, and never read
together.

- **A child is placed touching its parent** (D077, 2026-09-07). The shared box needed a
  placer that could prove a spot free, and the spot it proves is one body-radius from the
  parent at a random compass angle, at the parent's depth. That is the whole horizontal law
  for a body that does not swim: a random walk of a fraction of a metre per generation.
- **The current returns a body to where it found it** (D059). Two standing waves,
  antisymmetric in time, chosen so that the water stirs without carrying anything. The
  comment on the code says why: "nothing reads horizontal position, so this changes what
  the water feels like and not where anything ends up." When it was written the world was
  tiled and x and z were, in the record's own words, cosmetic.
- **Nothing else moves a sitter sideways.** No swimmer has ever paid its cost, so every
  body in every round has been a sitter, and a sitter's x and z are where its parent put it.

A clade is therefore a column packed around the spot its founder landed on, for the whole
run. The two ribbons are the two clades alive at 1,834 s. They spread up and down by
sinking and by nothing else.

## Why nobody saw it

The run report carries a mean depth, per-guild depths, and per-patch bins. A lineage row
carries a patch index. A snapshot carries a genome. There is no number anywhere in the
record for where a body is along the box, and CLAUDE.md has said for a week that depth by
guild is not measurable from a run's output. Thirty-three rounds were read on numbers that
cannot show a ribbon, and the first instrument that draws x and z showed it within a minute
of Play. That is the theatre earning its keep, and it is my failure: I read "no swimmer"
as a statement about locomotion and never asked what a world of sitters looks like from
the side.

## What it does to the record

Not everything, and not nothing. The energy and matter books, the prices, the goal-rule
verdicts and the dials' walk are what they are: they were measured in the world that ran,
and that world was two ribbons in a box. What the ribbons confound is every claim about
*where* food is relative to *where* a body is.

- **The grid against the vertex water** (0079). Since D086 a mouth drains the one cell it
  stands in, and the record's own premise for the returning current, that nothing reads
  horizontal position, has been false since that day. A column of eaters drains the same
  handful of cells for thirty thousand seconds and its children are born into the same
  drained cells. 0079's verdict said the columns could not say why the eaters stopped
  recruiting on the grid. This is the candidate that was in front of us the whole time.
- **The hole at the sitter** (0077, 0078). The geometry argument stands, but the sitter in
  it was imagined alone in open water, and the sitters in the runs were packed shoulder to
  shoulder in a column with their whole lineage.
- **Movement never paid** (0027 onward). The prize for moving was measured in a world in
  which the food a metre away was being eaten by your cousins and the food ten metres away
  was untouched, and no body was ever placed ten metres away to find out.

The rounds mean what they measured. What they measured was a narrower world than anyone
reading them believed, and the reading must carry that from here.

## The fix, started the same hour

The owner's ruling was "lets start the fix please", and the fix is dispersal. A newborn is
placed at a point drawn uniformly over a horizontal disc of `OffspringDispersalMetres`
around its parent, never closer than the two radii, wrapped at the seams, at the parent's
depth, under the same free-spot test; at 0 the old rule runs unchanged so that every
config in the record still describes its world. The first value is 5 m, a quarter of the
box, for the owner to rule on. Founders were already placed at random across the box and
stay so.

With it, the instrument that was missing: at every sample, the number of occupied 1 m
columns of the footprint out of a hundred, the same for eaters alone, and the spread of x
and z, in `stats.jsonl` and in the table as `cols`, `cols abs` and `x sd`. No ribbon is
invisible to the record again.

The theatre also reported that its replay of the run parted from the recording at 200 s,
in the fourth figure of the audit. That is a second fault, separate from the ribbons,
under investigation as this is written; replay identity was last validated at round 28 and
three builds have landed since.

## Sources

The owner's theatre session (2026-09-10 midday; Mode B on `runs/r33-s3`); `SharedVolume.cs`
`TryReserveOffspring`; `CurrentField.VelocityAt`; D059, D077, D086; logbook/0079, 0082.
