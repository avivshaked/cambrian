# Notes toward three specs: the safari, the timeline, the checkpoints (owner, 2026-09-15 evening)

The owner's aim, stated on the evening of 2026-09-15: YouTube videos about the world we are
building, produced with the agent or a parallel agent, telling a story. "Whenever we do
[have a story], I'd like to be able to click safari and record (as well as other things we
might want to record and edit into the video). The safari should be both informative and
cinematic in nature." These notes hold what was agreed in that conversation; each becomes a
spec (`safari-spec.md`, `timeline-spec.md`, `checkpoint-spec.md`) before it is built, and
the specs are put in front of the owner first. Order agreed: round 39 launches, then the
timeline, then the checkpoints, then the safari director, with the Recorder overlay bug
fixed on the way. None of it competes with the round for workers: the theatre work runs in
the owner's Editor and the Core work in tests.

## The safari

**What it is.** A trip through one recorded run: a list of scenes, each a camera plan with
captions, built from a procedural guide to the run's species (the safari's first stage in
HANDOFF: clades from the scorer's parent walk, names as deterministic binomials from the
genome, cards of facts from the lineage, the positions file and the ledger; "interesting"
as a ranking over novelty, success, persistence, rarity and firsts). Every caption is a fact
from the guide; the meaning is the owner's narration.

**Stations** (the agent's list, accepted): arrival (a wide shot from outside the glass at
the surface, ten seconds, one caption); descent through the lit band along the shallow arc
until the sand rises; portraits of a named body, long lens, lit from one side, the fog as
depth of field, no box and no markers; the floor, low along the slope, across a hollow and
what has settled in it; a birth or a split, sought to the second a clade was founded, the
parent and the child; the colony, a pull-back from a portrait to the clade's colour in the
crowd to the disc from above (the census view as a chapter card, once); time, a crossfade
between two seconds of one shot thousands of seconds apart.

**Owner's additions.**

- The trip visits more than one species. Scenes are a list; **next** and **previous** move
  between them at any time; a **picker** jumps to a species by name; **auto mode** plays
  the list through with a pause between scenes. The list comes from a heuristic the user
  chooses: the top ten by the guide's ranking, or by guild, by depth band, by clade age, by
  firsts. Every station, every individual species and auto mode are all in.
- **Following a creature is an orbit, not a tail**: slow, and the camera moves around the
  body, sometimes horizontally, sometimes vertically, sometimes both, a quarter to a half
  turn by default and a full circle only when asked. The subject stays off centre; the
  camera keeps a slight lead so the body swims into the frame; consecutive scenes draw
  different arcs; a sitter gets a slower, tighter arc than a swimmer. The arc's radius
  sets the lens, and the speed never exceeds a body length every few seconds. Each scene
  ends with the camera parked, so the next begins with a cut rather than a rush.
- **Strong camera movements ease in and out**, the stronger the move the longer the ease;
  small drifts inside a hold may stay linear.

**Two candidate first stories** (the agent's, not yet chosen by the owner): "The world
that stood", thirty-eight rounds from creatures swimming in vacuum to a tank where 1,600
bodies live on a closed budget, told through the failures (the energy audit, the ribbons,
the centrifuge), every picture already in the record; and "The boom and the bust", round
38's eaters rising to seven hundred and collapsing in every seed, with round 39's floor as
the next experiment, filmed as it happens. The cut is the owner's: the safari produces
clips and a caption log with timecodes, and the narration is the owner's, drafted by an
agent from the cards and the entry if wanted, the facts never coming from the prose.

**Rules the agent holds to** (accepted): one move per shot; nothing faster than a body
swims; light from above and slightly behind the camera; captions one short sentence, white,
lower third, gone after four seconds; the interface hidden except the provenance word in a
corner, faithful or cousin; no wireframe box, ring lines or markers; no whip pans; the camera
never clips sand or bodies; no caption interprets. Record starts the Recorder with the
interface hidden or shown, the user's choice, and writes a caption log with timecodes beside
the clips.

## The timeline

The owner: "we are also missing some tools that show graphs over time of various aspects
of the world, or even something like Civilization does where you can play the timeline and
you are the graph of a thing over time and can jump around in the timeline."

Agreed design: a scrub mode that draws the world from `positions.jsonl` at any sample
(bodies where they stood, coloured by guild, sized from the snapshot genomes; no physics;
instant seek; faithful by construction because it is the record), under a charts panel over
`stats.jsonl`'s fields (pick fields, plot over the run, click a point to seek) with the
lineage's events (a clade's founding, a bust, a throw) on the same axis. The graph is the
timeline. Built first, because it makes every later question faster to answer.

## The checkpoints

The owner: "One thing we don't have is the ability to take world snapshots and replay them,
which means it takes a long time to get to any interesting point in the recording."

Agreed: the farm writes a full-state checkpoint every 1,000 s or so (the Core world exactly:
fields, organisms with reserves, ages, growth, brain state, corpses, reservations, the RNG's
state; and every part's pose, velocities, joint positions and velocities, drive targets).
The theatre seeks to the nearest checkpoint and plays forward with the physics running as a
**labelled cousin**: PhysX keeps solver state no API returns, so the step after a restore
is solved in a different contact order and the world parts from its recording within
minutes. The owner accepts the cousin for filming ("Yes a labeled cousin is acceptable"),
and the overlay says "faithful to N s, cousin after". The RNG itself restores exactly.

Size, estimated at round 38's scale (1,600 bodies, 24,000 live cells): genomes about 8 MB,
per-organism state about 1.5 MB, physics about 0.2 MB, fields about 1 MB, the rest under
0.1 MB; 10 to 12 MB raw, about 2 MB gzipped; at round 39's grids about 15 MB raw. Thirty per
30,000 s run is 300 to 450 MB raw, under 100 MB compressed, roughly doubling a run on disk.
Genomes by reference to the lineage id would take it under 3 MB raw; the plain form first.
