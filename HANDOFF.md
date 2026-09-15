# Handoff: where to pick up

*Rewritten 2026-09-13 from the current state. What happened is in the logbook, and why it was
chosen is in [`DECISIONS.md`](DECISIONS.md). This file says only where things stand and what
is queued; it is rewritten, never appended to.*

## Where things stand

**Round 37b, the water carried as water, is the base and is read (logbook/0095, 0097; D090,
D091).** Round 37's tank on the streams with the fluid acceleration force at 1, the
conservative transporter, the whole-body wall clearance at birth, the corrected `cols` and
the throw trace; one build (`simHash 5e164d01…`, `coreHash ad5c952a…`, `configHash
2430e660`, `physicsJobWorkers 0`). Read 2026-09-13 as 0097: the rim quarter 19 to 27%
(round 37: 45 to 98%), the crowd the box's at matched abundance, the cycle gone, the goal
rule five of five; seed 5 wedged at 29,200 s and is censored. M6 failed on 43 throws, all
two-part jointed newborns, and the control `r37bc-s5` (seed 5 with `fluidAccel 0`) threw
nothing, so the force is the world in which the throws happen; the field probe reads at
most 0.12 m/s² and the genomes say the throw is one hinge lineage driven at full power from
its first step (0097's addendum; `scratch/accel-probe/`, `scratch/throw-parents/`).
**The fresh seeds are read (logbook/0098, 0099; 2026-09-14 night).** Seeds 6 to 10 on the
same world and build, pre-registered, all ended on budget: F1 to F8 hold, so 37b's readings
are the world's. Ten seeds of ten pass D063 on this base (the reference bar from here), the
disc is mixed in ten of ten, the joint survives founding in three of ten and grows in none
(every jointed peak is before 3,000 s and decays), and the fresh five threw nothing over
3.79 million jointed body-seconds, so the 43 throws are lineage-bound (inference). The
stillbirth reading of 0097's addendum does not hold on seed 10 and stays open. The trace's
second pass is built on branch `trace2` (worktree `scratch/wt-trace2`, commit `7846cc3`:
the ring keeps only finite frames, records the first non-finite step, and carries the
step's drag, acceleration force and water acceleration per link; the smoke's forced case
uses a poison hook on the ring's read because PhysX takes no velocity on a link). It
compiled clean on worker 6 at 00:33 on 2026-09-14 (a compile-only pass, a sixth Editor
for three minutes over the cap, noted; worker 6 was then restored from main and checked
file for file), and waits for a worker under the cap to run its smoke and the box
digest. **Both landed 2026-09-15 00:25**: the candidate (main + `trace2` + `fieldcv`) passed the
shared-space smoke with the forced case, the 600 s box digest identical to main's over 31
steps, and the dilute tank smoke with 0100's header; main is fast-forwarded to `582ee6a`.
**Round 38 is running on it** (logbook/0100): seeds 1 and 2 launched 2026-09-15 00:25 on
workers 2 and 3 (`simHash ecc41ec5…`, `coreHash 16073c69…`, `configHash 30637fb0`,
headers verified), seeds 3 to 5 queued behind the fresh seeds' renders
(`scratch/logs/r38-queue.out`). Renders of the five seeds run on workers 2, 3, 5 and 7 (`scratch/r37b-chain.ps1`);
seed 5's render cannot reach a 30,000 s frame, and **the render queue did not take it**: a
stopped run's report carries no Ended footer, which was all the queue read, so the chain
sat two hours behind it on 2026-09-14 morning and was stopped (the queue now also reads
the manifest's status, so a stopped or errored run is taken); seed 5's frames at
5,000 and 15,000 s are taken by hand (`theatre-snap.ps1 r37b-s5 -At 5000,15000 -Worker 7`)
when a slot frees, after the fresh seeds' renders. Its first attempt was killed the same
morning for putting the machine one over the cap (the queue counted before its Editor was
up), and worker 7 was refreshed after the kill. The fresh seeds' renders run on
`scratch/r37b-fresh-chain.ps1` (seeds 7, 6 and 8 at the three times, 9 and 10 at 5,000 and
15,000 s; seeds 7 and 6 started 10:59 on workers 6 and 3). **A 30,000 s render does not
fit the queue's 720-minute wall on a loaded machine**: seed 7's did (its 30,000 s frames
are 0099's), seed 6's timed out at 22:59 with its 5,000 and 15,000 s frames only and is
not re-run (seed 7 and the positions plots cover the end state), and seed 8's, started
13:25, may go the same way. Seed 5's two frames started by hand on worker 7 at 23:10.
**The two merges wait for those frames**: both
branches move Core or `Assets/Evosim`, and an Editor started after the merge would refuse
the fresh seeds' recordings, since every worker compiles Core from the main tree.

**The theatre has an interface (logbook/0096, 2026-09-13 night).** Built by a subagent from
the owner's design (`design/SPEC.md`, committed with its `LICENSE-DOCS` lines at the owner's
word) and the two specs (`logbook/specs/theatre-ui-spec.md`, `theatre-ui-test-spec.md`), run
seven times in the Editor on worker 6 to all green (world 81 of 81, cousin 79 of 79, solo 26
of 26, three skips the fixtures cannot produce), merged into main; nothing under `Assets/Evosim` moved. UI Toolkit
under `unity/Assets/Theatre/UI/`: the identity strip, the census with both identities, the
warning plate, the transport bar and timeline, the provenance popover, the inspector in its
states, the solo census; `TheatreUiCheck.Run` is the end-to-end check and
`theatre-snap.ps1 -Chrome` photographs the chrome over a frame. Two rulings the runs forced,
both reversible in one block: the type and the rhythm take a 1.5 step at 3400 px, and a
cousin's lineage fields are withheld because its ids are unverifiable. Not verified: use by a
person, a player build, the chrome over the bright surface frame, the prose leading at 3840,
and why a custom property declared under `.is-wider` reaches nothing.

**Round 37 is read (logbook/0094).** Five of five on the population, four of five on the goal
rule, and a crust: the rim quarter held 45 to 98% of the bodies at every named time, the drift
was outward at every radius while births landed inward, so the gathering was the drag-only
body's centrifuge (D090; the owner named it first). The tank threw no body across 9.17
million jointed body-seconds where the box threw 131 across 6.91 million, so the wrap is read
as the throw's mechanism (inference), the mass-ratio cap is not proposed, and round 37b's
`diverged` is the test. The population cycle (a factor of three on 10,000 s) has no mechanism
yet. Seed 3 ended with 148 jointed bodies, the largest standing jointed population so far.

**The Astra review of 2026-09-12 is answered** (`gpt-astra-2026-09-12-1308-review-response.md`):
the transporter that made 30% patchiness from uniform water and the placer that tested a
centre and never a radius are both repaired in round 37b's build; the review's document
claims were verified row by row and the documentation pass (DESIGN through D090 and the
repairs, README, primers 05 and 06, the research counts) is done; the adoption-rule
inconsistency it found is ruled as D091 (replacements and treatments). Round 36 is read as
logbook/0092 (five of five; a standing jointed population of absorptive bodies in seed 1, on a
body plan the round did not predict).

The goal has been met, and the base world keeps moving under it. D063, as amended
2026-09-04, asks for a clade that lasts: one connected absorptive clade alive for 20
consecutive samples to the end of a 30,000 s run, 10 or more members through the last two
lifetimes and still breeding, in at least 3 of 5 seeds, with the population floor closed, on
an inherited photosynthetic lineage. Since D091 a replacement of the world reads the rule
without requiring it, and a treatment is adopted only at the bar its pre-registration names.
The rounds of the shared world, each read against the one before it:

| round | entry | world | goal rule |
|---|---|---|---|
| 28 | 0070 | round 18's closed world in D077's box under D078's single thread; the base by D081 | 3 of 5 |
| 29 | 0072 | the senses on, added mass 0.5, the global brain retired; the price control | 2 of 5 |
| 30 | 0075 | the water as vertices, the bud priced | 5 of 5 |
| 31 | 0077 | detritus mixing at 0.02 | 4 of 5 |
| 32 | 0079 | the water as a grid of cells, corpses as particles | 2 of 5 |
| 33 | 0082 | bodies born small and growing; the first trait moved by degree | 5 of 5 |
| 35 | 0087 | dispersal 5 m, a current that carries, founders reserving the adult | 5 of 5 |
| 34 | 0090 | the joint made free: founds in every seed, gone from every seed by 23,100 s | 5 of 5 |
| 36 | 0092 | the link earns at 0.5; a jointed absorptive population stands in seed 1 | 5 of 5 |
| 37 | 0094 | the tank: a glass wall, a gyre, ring patches; the crust at the glass | 4 of 5 |
| 37b | 0095, 0097 | the streams, the fluid force, the conservative transporter; the base | 5 of 5 |
| 6 to 10 | 0098, 0099 | 37b's world on five fresh seeds; not a round | 5 of 5 |
| 38 | 0100 | the dilute tank: 400 m² with the matter held at 6,000 units | running |

Round 34 ran after round 35 (the owner's theatre pause moved the base round first); the
entries are in `logbook/README.md`'s key.

## The path, ruled

D079 (owner, 2026-09-06) set the method: one change at a time, asking D063 of each, a change
that costs the rule read rather than tuned around. D081 set the base and the two bars (*the
goal*, D063's 3 of 5; *the reference*, the base world's own count). D091 (owner, 2026-09-12)
split the changes in two: a *replacement* changes what the world is and becomes the base on
the owner's ruling, its round read for the mechanism and the goal rule not required; a
*treatment* changes one price, sense or rule on a fixed world and joins the base only at its
pre-registered bar. The owner's ten-round plan of 2026-09-11 ("plan the next 10 rounds and
change only if a result compels us"; "proceed autonomously") is the sequence below. The agent
may reorder it when a result compels (owner's grant, 2026-09-11), records each reorder here
with its date and reason, and never adds a world rule: those come to the owner as
`fable-propose-*.md`, absorbed into DECISIONS.md on ruling and then deleted. Rounds 28 to 37
are in the table above; the free joint's reading (round 34: the price was the founding
barrier, the unthrottled stroke removes the joint afterwards) is why the stroke's price moved
ahead of the idle charge.

1. **Round 37b, the water carried as water** (D090, D091; running, above). *Resequenced
   2026-09-12 afternoon by the agent, on the owner's diagnosis of round 37's gathering; amended
   the same evening after the Astra review to carry the transporter, the clearance and the
   corrected `cols`, none a world rule.* Read as a fresh baseline, not as round 37 repaired
   (D091): its changes are not attributed one by one unless a later question needs it.
2. **The one-part control and the fresh seeds on the base.** First `r37bc-s5` (0097's
   control: seed 5 on 37b's build with `fluidAccel 0`, everything else 37b's, so the force
   is the one difference from the streams alone; a replay of a scored condition, agent
   work), launched 2026-09-13 afternoon on worker 6 as the renders hold the other slots;
   read on `diverged` against 37 and the traces' anatomy. Then the fresh seeds (D091; owner
   2026-09-12, "proceed with your recommendations"): round 37b's world on seeds 6 to 10, no
   build, **pre-registered as logbook/0098 (F1 to F8) and queued 2026-09-13 evening**
   (`launch-queue.ps1 -Prereg` on workers 2, 3, 4, 5 and 7 as the renders free them, log
   `scratch/logs/r37b-fresh-queue.out`); read against 37b's five as a second draw of the
   same world, with F5 the joint's question and F6 the throws'. *Moved here from the old queue's item 22 because the five
   founding lotteries have guided nine rounds of adaptive change and round 37's standing
   jointed populations have to be shown to be the world's.* **Done 2026-09-14: both read (0097's addendum, 0099); F1 to F8 hold, the reference bar is ten of ten.**
3. **Round 38, the dilute tank** (D089 rulings 3 and 4): 400 m² with the matter held at 6,000
   units, corpses as objects at 0.005/s; **pre-registered as logbook/0100 (D1 to D8)** on
   2026-09-14 night, its hashes to be recorded at launch; `rounds/launch-r38.ps1` is written
   and launches on the build that carries the two merges. *Repaired 2026-09-13 morning:*
   it had been written from round 37's launcher before D090 and carried no
   `EVOSIM_FLUID_ACCEL`, so round 38 would have run the drag-only centrifuge; it now sets the
   force at 1 and its header comment says to verify `fluidAccel 1`. The dilute arithmetic passed
   (a 5 m matter cell holds 31 units at 0.235/m³; the mask overshoots the disc by 6%). Read:
   nearest neighbour, founding (`mat blk`, `mat short` against births), the downwelling as the
   first patch, sitter against mover. The build queued between the fresh seeds and this
   round (the queue's item 2, `field cv`) lands first, since it moves both hashes.
4. **Round 39, a bed with shape** (was 42): **ruled 2026-09-15 morning (D092;
   `logbook/specs/bed-spec.md`)**: a seeded height map at three scales, the streams'
   potential in floor-following coordinates so the water slows in the hollows, the grid
   masked below the floor, one static collider, relief 0 replaying the flat world; rocks
   and the shelf out. Builds on a branch after round 38's read, validated (constant field
   on the sloped grid, digest at relief 0, the current's divergence and floor flux, a
   settling test, the pace within 15%), pre-registered, launched as round 39.
5. **Round 40, a light sense** (was 38): one new input, light and its vertical gradient; read
   on jointed against rigid against buoyant depth, in a world with something to steer toward.
   Proposal first.
6. **Round 41, the stroke priced alone** (was 39): the work cost back to D082's 0.25 with the
   idle charge still at 0.0001; no build. *First reordered 2026-09-11 morning after round 34's
   first three seeds: the stroke's price shapes what a joint does, the idle charge only what
   it costs to own. Now behind the dilution and the bed, so that there is a chase to pay for.*
   Before it is read on stroke quality, two things (owner 2026-09-12 night, "let's follow
   your recommendation"):
   - **The water that a fin can push on**: lift on a panel (a Kutta-style term in speed
     squared, angle of attack and area, in the drag's own loop) and the reactive force of a
     body bending through water (Lighthill's elongated-body theory, an unsteady added-mass
     term), each a tunable defaulting to 0 so every recording replays. Proposal first (the
     two terms, a validation against a known case such as a flapping plate, the cost per part
     per step), built on a branch, switched on for round 41. The rigid-body engine stays
     unless the throw trace says the throws are its and not ours.
   - **The champion in real water**: DESIGN §5.4's validation harness, one evolved swimmer in
     SPH or lattice-Boltzmann, to say whether a stroke evolution found is real or the
     approximation's. After the two terms have been read once; an instrument, not a round.
7. **Round 42, ellipsoids in the physics** (was 40): spheres and capsules collide and drag as
   their three half-extents; preceded by the offline read of whether boxes have flattened.
   Proposal first.
8. **Round 43, the anchoring cell**: holds a body to the bed or a rock against the current;
   pairs with the bed. Proposal first.
9. **Round 44, shading with a length scale** (was 41): self-shading by the neighbours above
   rather than the patch mean. The least urgent. Proposal first.
10. **Round 45, the remaining prices restored** (the idle charge to 0.02, the neuron and the
    connection to D082's values) on whichever world of 39 to 44 carries joints; no build.
11. **Round 46, the long arm**: one seed, 300,000 s, on the richest standing world, the stroke
    read against the water every 1,000 s; one worker for a week.
12. **Predation on contact** (`fable-propose-predation.md`, consolidated), the first thing a
    brain can be selected for; the owner ruled it too early on 2026-09-11 and it waits behind
    the ten.
13. **The open matter budget** (D074) and the vent, when a round shows the larder binds; then
    **the cell types and immigration**, the archive and the islands.

Standing rulings on the rounds. *Run length* (owner, 2026-09-08): 30,000 s gives 40 to 50
generations and three to four turnovers of the standing crop, enough to read whether a trait
the world contains is kept or lost; whether a wired sense is *used* goes to an inoculated
round (round 15's tool), not a longer one. *Mutation rates stay* (owner, same night): a child
already carries of the order of one structural change per birth, so novelty is not what is
short; the one targeted test allowed is the input-rewiring chance alone, with a control, if
inoculation shows the world keeps a wired sense but never finds one. *The limiter at every
step* was built as a tunable and failed its check (D089; 27 divergences against 17 with the
same signature), so no round runs it. *Every code change orphans every earlier run for the
theatre* (owner 2026-09-09, "a problem we should consider on its own", not to be answered
now): candidates when it is taken up are a tagged build per round kept beside the tree, the
theatre built against a run's recorded commit in a worktree, or a replay-only mode that loads
old formats read-only.

## Queued, in order

Agent work unless marked. Long steps (a suite, a smoke, a render) are launched by the agent in
the background and never handed to a subagent, which cannot wait.

1. **Round 38 is running** (0100, above): watch it (`scratch/arms-watch.sh r38-s1..s5`,
   the queue's log), look at a live arm every few thousand seconds (3,000 and 6,000 s are
   in 0100's launch section), launch seed 5 when a slot frees (the owner's Editor holds
   the fifth slot while four seeds run, so seed 5 lands about a day after seed 1 unless it
   closes), and **arm round 38's render queue only after seed 5 has launched**, so the
   launch queue and the render queue never count the cap in the same minute (a render's
   Editor takes a minute to appear, and 2026-09-14 morning ran six for that reason). Then
   the read against D1 to D8 as 0101, one notification. Two theatre items wait for a
   worker with a graphics device and move no hash: **the skin's rounding capped** so a
   near-cubic box stays a box, with a key that shows the raw collider shapes (the owner's
   observation of 2026-09-13), and **the Recorder's capture reproduced**, which hides the
   interface from the Game View while it records (the owner, 2026-09-13; not intentional;
   the likely fix draws the panel into a render texture the world camera composites, as
   `TheatreUiCapture` already does for pictures).
2. **Landed 2026-09-15 00:25 with the trace's second pass**: `field cv` (`det cv`, `mat cv`
   in the table, `detritusCv` and `matterCv` in `stats.jsonl`; `logbook/specs/field-cv-spec.md`)
   and the trace that keeps the last finite frames and names the first non-finite step
   (`throw-trace-spec.md`'s second pass), validated together on one candidate tree (the
   smoke with the forced case, the 600 s box digest identical, the dilute tank smoke) and
   merged as `582ee6a`. Round 38 is the first round that reads both.
3. **The streams' analytic derivative is already in round 37b's build** (`dcc9a24`, inside
   the `streams` merge; `logbook/specs/streams-analytic-spec.md`): in a tank the force takes
   a closed-form time derivative and Jacobian at 2.3 velocity samples per call where the
   stencil cost 9.6, agreeing to 0.15% of the RMS. The nine-sample stencil survives only for
   the box's transport field, where no round has run the force. This item was carried in
   the old handoff as queued after it had landed; M8 reads the pace it actually costs.
4. **Pockets, then a bigger tank** (owner, 2026-09-12 night): the proposal follows round 37b's
   read, because it depends on how the streams move sinking matter (below).
5. **The fluid terms' proposal** (path item 6) and, after it has read once, the §5.4 harness.
6. **The throw's mitigation.** The trace is the per-link instrument; what it decides between
   (a mass-ratio cap at build and resize; the search names the 10:1 rule,
   `logbook/specs/throw-trace-research.txt`) goes to the owner only if a tank throws.
7. **0084's bin 3 screens** on any free worker: dispersed against undispersed on round 32's
   seeds, mixing 0.2 against 0.02, corpses off.
8. **Older items still open, in the order they were captured**: the movement assay (active
   against clamped on saved members of a jointed clade, repeated across orientations) and its
   ecological layer (a connected jointed clade persisting two lifetimes and paying positive net
   after work); round 18 as a committed reference (its config, hashes and a representative
   lineage, if the base still builds on it); a manifest-against-header contract test at the
   Unity boundary; D052's transactional guard test (force an `Admit` failure and close every
   book); two columns, gross photosynthesis per window and the matter drawn at conception per
   window; the scorer checking which round a report belongs to (`-ExpectSimHash` stands in).
9. **Theatre work that needs no round**: the skin seeded from the genome so relatives resemble
   each other; the offline reading of whether any lineage's boxes have flattened under light
   (from the snapshots; it precedes round 42); inherited skin genes (six to ten neutral numbers
   under one gate, a genome format bump, no per-step effect; owner's rule, a proposal); the
   safari (below). The theatre's sun and surface dials (`EVOSIM_THEATRE_WAVE`, `_WAVELENGTH`,
   `_WAVE_SPEED`, `_LIGHT_REACH`, `_SHAFTS`, logbook/0091) are unjudged by the owner; the
   agent's reading is that the surface reads as bands, which is the wave steepness.
10. **For the owner**: the `scratch/` cleanout list in the migration report (companion to
    `logbook/specs/scratch-migration-spec.md`, 2026-09-10; 70 MB of logs, review captures,
    probe output, one-off edit scripts, stale copies; nothing deleted). The worktrees under
    `scratch/wt-*` stay until the owner says otherwise.

**The safari** (owner, 2026-09-11: a theatre feature that finds the species, visits the
interesting ones, takes their pictures and explains each, "without requiring ad-hoc
intelligence"). The agent's view: procedural to the last sentence, because every fact a
field-guide entry wants is in the run. Clades from the scorer's parent walk; a clade's
founding time, the clade it split from and the mutation at the split; its share of the
living, its peak, its generation depth; where it lives from `positions.jsonl`; its body from
the genome; its life from lineage rows; its economics from the ledger against its ancestor's.
"Interesting" is a ranking over novelty, success, persistence, rarity and firsts; a name is a
deterministic binomial from the genome hash; the explanation is a template with the facts in
its slots, which says "the split changed X; the ledger reads Y" and stops. Three stages, none
a round: a census script that writes the guide; pictures through `theatre-snap.ps1` with a
view that frames a named body at a named second; the theatre's tour mode. An LLM, if ever, is
a polish pass over the template's prose, off by default, and the facts never come from it.

## Pockets, then a bigger tank (owner, 2026-09-12 night)

"If we find a way to properly make pockets of matter and energy in the world, then we could
really expand the tank properly. Think about it and if you get a good idea at some point,
write it down." The agent's first thoughts; a proposal follows round 37b's read.

1. **A pocket has to be made by physics, not painted.** An incompressible current cannot
   concentrate a dissolved field (the constant-field rule the transporter now keeps), so a
   pocket of dissolved matter needs a source, a sink or slow mixing. What a current *can*
   concentrate is anything that sinks: falling particles gather under downwelling and are
   swept from under upwelling, so corpses and marine snow in D090's overturning cells should
   already collect in moving bands. Round 37b's `det patch sd` and `patch max share`, and a
   picture of where the corpses are, say whether they do.
2. **Three sources the design already half-owns.** A bed with shape (round 39): hollows that
   sinking detritus settles into and cannot leave, ridges that shed it. A matter seep (D067's
   vent, off since its round): a point on the bed that leaks matter at a rate, a pocket whose
   size is the rate over the mixing. A shelf: a bed that rises to a few metres under the
   surface on one side of the tank, sunlit and settled on at once, which is what a reef is.
   Each is a physical rule with one dial, and a bigger tank is then dilute between pockets and
   rich in them.
3. **A reading first**: `field cv` (queue item 2), because no patchiness measure before
   2026-09-12 can be trusted.
4. **What not to do.** Slow the mixing further (the detritus already mixes at 0.02 m²/s, the
   prize has not appeared, and a matter grid stirred slower strands stock in cells too small
   to afford a child). Paint patches into the field: a source that nothing feeds is a rule the
   world cannot explain.

## The decisions in front of the owner

- **The theatre in person: done.** The owner tried the interface on 2026-09-13 morning
  ("not perfect yet, amazing progress"; good enough for now, the world comes first) and on
  2026-09-13 at 23:47 ruled "proceed with your recommendations" on the agent's plain-language
  brief, so the three rulings made under delegation stand: the 1.5 type step at 3400 px
  (build spec item 5), a cousin's lineage fields withheld, the pillow at 0.34 (0091's
  addendum). The carve is 0.35 by the owner's eye; carve 0.5 was refused by the agent from
  pictures. Two things the owner raised that morning are open: a joint's moving link reads as
  a ball on screen (the skin's rounding on near-cubic boxes; a cap on the rounding and a key
  for raw collider shapes are queued), and the Recorder's capture hides the interface from
  the Game View while it records (not intentional; to be reproduced on a worker with a
  graphics device and fixed in theatre code).
- **Things wrong in motion** (owner, 2026-09-11 evening: "there are some issues with the
  world that you can only see when rendering"), set aside and not yet named. The agent reads
  stills only; when they are named the route is a film of the session with frames pulled at
  the seconds in question.
- **Round 38's two rulings** (D089's dilution to 400 m² with the matter held at 6,000 units,
  and corpses as objects at 0.005/s) were confirmed by the owner on 2026-09-13 at 23:47 with
  the same ruling; they are no longer open.
- **The producer threshold** is unsettled: D063's amendment asks for one living inherited
  member with a recent photosynthetic birth; the scorer prints that, the 10-through-two-
  lifetimes reading and the population-only column reading, and decides on none of them. The
  ruling picks one. With it, **the food chain's meaning under D063** and **the split between
  the lineage rule and a balance rule**, both noted under D063; **a late resource-balance
  rule** needs its own decision with a tolerance.
- **Predation on contact**, in `fable-propose-predation.md`: the injury pool with fixed
  geometry, dt 0.01 from the first screen, stable contact keys, an internal matter reserve.
- **Multithreaded physics** for labelled screens (D078 keeps the setting recorded; one
  sentence). **Extending a passing seed past 30,000 s. The maintenance assay** (a
  multi-genome inoculum with cell-type mutation off), which the owner should scope. **The
  width of the box** (raised by round 28's M5; the dilute tank is the first answer). **Whether
  the absence of CI is a choice.**
- **Speed, and the game's clock** (owner, 2026-09-03): a world eventful on a human timescale
  is a world-rule question. **Immigration as a world rule when the cell types expand**
  (owner's hypothesis, 2026-09-03).
- **The paywalled reading list** in `research/LITERATURE-REVIEW.md` needs the owner's
  institutional access.
- **The review files at the root.** Both Astra pairs (2026-09-07 and 2026-09-12, each with its
  response) are answered; every item they queued is done, ruled or in the queue above. They
  are the owner's to absorb or delete.

## How the experiments are run

CLAUDE.md holds the commands and the gotchas. This is where each tool sits.

| | |
|---|---|
| workers | arms run on `unity-w2..unity-w7`, one per worker, at most five Unity editors at once; after a change under `unity/Assets`, `scripts/new-worker.ps1 -Workers N` once per worker and check the hash; a killed Editor leaves `Temp/UnityLockfile`, which reads as busy until removed |
| launching | a round goes through `scripts/launch-queue.ps1` with `-Prereg logbook/NNNN-….md` (refuses unless the entry is tracked and clean; writes `prereg.json`), `-Refresh` (each worker refreshed as it frees) and `-ExpectSimHash`; `scripts/run-arm.ps1` underneath it; logs in `scratch/logs/`; end an arm with `stop-arm.ps1` and never with a kill; read every setting back from the run header and the manifest |
| renders | `scripts/render-queue.ps1` beside the launch queue, frames at 5,000, 15,000 and 30,000 s into `scratch/snaps/<arm>/`; `scripts/theatre-snap.ps1` for one frame of a live arm (`-WallMinutes 150` on a loaded machine; `-Chrome` for the interface; `-Views close` for a portrait) |
| reading | `scripts/analyse-arm.ps1` by column name (`-ListColumns`), never positionally, `-Columns` as a real array from inside PowerShell; `python scripts/positions-read.py <arm> --summary` for where the bodies are; the per-round reads in `scripts/reads/` |
| scoring | `scripts/clade-score.ps1` for D063, `scripts/absorptive-log.ps1 <arm>` for what a stomach earned, `scripts/lineage-invasion.ps1` for an inoculated lineage, `scripts/ledger.ps1` (D069) before a worker |
| monitoring | one script per round under `scratch/` (`r37b-watch.sh` today): ending, error signature, and a stall on the report's byte size at 30 minutes read as a suspicion; `scripts/monitor-r13.sh` over a watch list is the older form |
| identity | `scripts/compare-det.py` (exit 1 on a difference, 2 missing, 3 unequal coverage) and `digest-diff.py` on a zero-worker pair; `scripts/theatre-check.ps1` for the replay |
| throughput | about 1,800 bodies at dt 0.01 with five arms sharing the machine is five to six hours per 30,000 s; the fluid force at 1 takes the streams' closed form in a tank (2.3 velocity samples per call; M8 of 0095 reads what it costs); the ceilings (`EVOSIM_MAX_POP`, `EVOSIM_MAX_TISSUE`) end a run as a censored runaway |
