# Notes toward three specs: the safari, the timeline, the checkpoints (owner, 2026-09-15 evening)

On the evening of 2026-09-15 the owner said what the videos are for. They are films about
the world we are building, made with the agent or a parallel agent, and each one tells a
story.

> "Whenever we do [have a story], I'd like to be able to click safari and record (as well
> as other things we might want to record and edit into the video). The safari should be
> both informative and cinematic in nature."

These notes hold what was agreed in that conversation, checked against the transcript. Three
specs were written from them the same night: `timeline-spec.md`, `checkpoint-spec.md` and
`safari-spec.md`. All three go in front of the owner before anything is built. The order
agreed that evening is round 39's launch first, then the timeline, then the checkpoints,
then the safari director, with the Recorder overlay bug fixed on the way. None of it
competes with the round for workers, because the theatre work runs in the owner's Editor
and the Core work in tests.

## The safari

A safari is a trip through one recorded run. It is a list of scenes, and a scene is a camera
plan with captions. The scenes are built from a procedural guide to the run's species, which
is the safari's first stage in HANDOFF. Clades come from the scorer's parent walk and names
are deterministic binomials from the genome. The cards of facts come from the lineage, the
positions file and the ledger. A clade is ranked "interesting" over novelty, success,
persistence, rarity and firsts. Every caption is a fact from the guide; the meaning is the
owner's narration.

The owner accepted the agent's list of stations.

- **Arrival**, a wide shot from outside the glass at the surface, ten seconds, one caption.
- **Descent** through the lit band along the shallow arc until the sand rises.
- **Portraits** of a named body, long lens, lit from one side, the fog as depth of field, no
  box and no markers.
- **The floor**, low along the slope, across a hollow and what has settled in it.
- **A birth or a split**, sought to the second a clade was founded, the parent and the child.
- **The colony**, a pull-back from a portrait to the clade's colour in the crowd to the disc
  from above (the census view as a chapter card, once).
- **Time**, a crossfade between two seconds of one shot thousands of seconds apart.

The owner added three rules of his own.

- The trip visits more than one species. Scenes are a list. **Next** and **previous** move
  between them at any time, a **picker** jumps to a species by name, and **auto mode** plays
  the list through with a pause between scenes. The list comes from a heuristic the user
  chooses: the top ten by the guide's ranking, or by guild, by depth band, by clade age, by
  firsts. Every station, every individual species and auto mode are all in.
- **Following a creature is an orbit and never a tail.** The camera moves slowly around the
  body, sometimes horizontally, sometimes vertically, sometimes both. The default is a
  quarter to a half turn, and a full circle only when asked. The subject stays off centre
  and the camera keeps a slight lead, so the body swims into the frame. Consecutive scenes draw
  different arcs, and a sitter gets a slower, tighter arc than a swimmer. The arc's radius
  sets the lens, and the speed never exceeds a body length every few seconds. Each scene
  ends with the camera parked, so the next begins with a cut rather than a rush.
- **Strong camera movements ease in and out**, the stronger the move the longer the ease;
  small drifts inside a hold may stay linear.

The agent offered two candidate first stories, and the owner has not chosen between them.
The first is "The world that stood": thirty-eight rounds, from creatures swimming in vacuum
to a tank where 1,600 bodies live on a closed budget. It is told through the failures, which
are the energy audit, the ribbons and the centrifuge, and every picture it needs is already
in our record. The second is "The boom and the bust": round 38's eaters rising to seven
hundred and collapsing in every seed. Round 39's floor is the next experiment, filmed as it
happens.

The cut is the owner's. The safari produces clips and a caption log with timecodes. The
narration is the owner's too, drafted by an agent from the cards and the entry if wanted,
with the facts never coming from the prose.

The owner accepted a list of rules the agent holds to.

- One move per shot, and nothing faster than a body swims.
- Light from above and slightly behind the camera.
- Captions one short sentence, white, lower third, gone after four seconds.
- The interface hidden except the provenance word in a corner, faithful or cousin.
- No wireframe box, ring lines or markers, and no whip pans.
- The camera never clips sand or bodies, and no caption interprets.

Record starts the Recorder with the interface hidden or shown, the user's choice, and writes
a caption log with timecodes beside the clips.

## The timeline

The owner asked for graphs that can be played:

> "we are also missing some tools that show graphs over time of various aspects of the
> world, or even something like Civilization does where you can play the timeline and you
> are the graph of a thing over time and can jump around in the timeline."

The agreed design is a scrub mode that draws the world from `positions.jsonl` at any sample.
Bodies stand where they stood, coloured by guild and sized from the snapshot genomes, with
no physics and an instant seek. It is faithful by construction, because it draws what was
recorded. Under it sits a charts panel over `stats.jsonl`'s fields. It picks fields, plots them over
the run and seeks when a point is clicked. It carries the lineage's events on the same axis:
a clade's founding, a bust, a throw. The graph is the timeline. It is built first, because
it makes every later question faster to answer.

## The checkpoints

The owner asked to be able to jump:

> "One thing we don't have is the ability to take world snapshots and replay them, which
> means it takes a long time to get to any interesting point in the recording."

We agreed that the farm writes a full-state checkpoint every 1,000 s or so. It holds the
whole of the Core world: the fields, the organisms with their reserves, ages, growth and
brain state, the corpses, the reservations and the RNG's state. It holds every part's pose
and velocities too, with the joint positions, the joint velocities and the drive targets.
The theatre seeks to the nearest checkpoint and plays forward with the physics running, as a
**labelled cousin**.
PhysX keeps solver state that no API returns, so the step after a restore is solved in a
different contact order and the world parts from its recording within minutes. The owner
accepts the cousin for filming ("Yes a labeled cousin is acceptable"), and the overlay says
"faithful to N s, cousin after". The RNG itself comes back bit for bit.

The size was estimated at round 38's scale, 1,600 bodies and 24,000 live cells. Genomes take
about 8 MB, per-organism state about 1.5 MB, physics about 0.2 MB, fields about 1 MB and the
rest under 0.1 MB. That is 10 to 12 MB raw and about 2 MB gzipped. At round 39's grids that
becomes about 15 MB raw. Thirty checkpoints per 30,000 s run is 300 to 450 MB raw and under
100 MB compressed, so a run roughly doubles on disk. Holding genomes by reference to the
lineage id would take it under 3 MB raw; we build the plain form first.
