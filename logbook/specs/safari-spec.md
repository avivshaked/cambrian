# The safari: a guide to a run's species, and a director that films them

**2026-09-15**  ·  the third of the three tools of `video-tools-notes.md`. The owner's brief (2026-09-11): a theatre feature that finds the species, visits the interesting ones, takes their pictures and explains each, "without requiring ad-hoc intelligence"; and (2026-09-15) the trip is for YouTube videos, "both informative and cinematic in nature", with a click to start it and a click to record. Theatre and scripts only; moves no hash. Built after the timeline and the checkpoints, which it stands on. In front of the owner before building.

## What it is

Three parts, procedural to the last sentence, because every fact a field-guide entry wants
is in the run and nothing that is not in the run belongs in a caption.

- **The guide** (a script, no Unity): the run's clades, named, ranked and described in
  cards of facts.
- **The director** (theatre): a scene list built from the guide, each scene a station,
  a subject, a camera plan and its captions; next, previous, a picker and auto mode.
- **The record** (theatre, Editor): one click starts the Recorder on the director's
  frame with the interface hidden or shown, and writes a caption log with timecodes.

The cut is the owner's: clips plus the caption log plus the owner's narration. An agent
may draft narration from the cards and the entry; the facts never come from the prose.

## The guide

1. **A clade** is the unit, from the parent walk the scorer uses, generalised: a birth
   whose expressed flags (absorptive, jointed, photosynthetic) differ from its parent's
   founds a clade, and a founder with no parent founds one; a child with its parent's
   flags belongs to its parent's clade. The same walk draws the timeline's founding marks
   (`timeline-spec.md` item 9) and is tested against `clade-score.ps1`. Species by genome
   distance wait for `SpeciesDriftThreshold` to be calibrated (the column reads 1 in every
   round); the guide says "clade" and never "species" in its data, and the owner's word
   for the videos is theirs.
2. **A name** is a deterministic binomial from the founder's genome: the genus from the
   guild (one table of stems per flag triple, so every eater's genus sounds like an
   eater's), the epithet from the hash of the founder's genome through a syllable table
   that makes a pronounceable Latin-shaped word. The same run gives the same names on
   every machine; two clades never share one (a collision takes a suffix).
3. **A card** holds what the record knows: founded at, from which clade, what changed at
   the split (the flags, and from the snapshot nearest the founding, the genome
   difference against the parent's: nodes added or lost, cell types, a joint gained, the
   adult scale), members ever, the peak count and its second, the share of the living at
   the peak, the generation depth reached, alive at the end or extinct at, where it lived
   (from `positions.jsonl`: median depth, radius from the axis, height above the floor
   from the bed rebuilt from the config and seed, over the clade's life), the body (the
   adult volume and part count from the genome), and the firsts it holds. The economics
   from the ledger (`ledger.ps1` against the ancestor's) are a second pass.
4. **Interesting** is a score over five readings, each 0 to 1, weights in the script's
   header and printed in the guide: novelty (a flag triple not seen before it), success
   (peak share of the living), persistence (life as a fraction of the run), rarity (few
   members over a long life), firsts (the first jointed body to breed, the first
   producer, the deepest clade, the shallowest, the longest-lived). The top ten by score
   are the default trip; every clade with ten members ever is in the picker.
5. **Output**: `scripts/guide.py <arm>` writes `guide.json` (the cards, the names, the
   ranking, the best second for each clade: its peak sample) and `guide.md` (the cards as
   prose from a template that says "the split changed X" and stops) beside the run under
   `runs/<arm>/<run>/guide/`, and an entry that cites a guide copies its `.md` into the
   round's read directory. The theatre reads the JSON and writes nothing.

## The director

6. **A scene** is a station applied to a subject at a second: `Station`, `Subject` (a
   clade, a body id, or the world), `At` (the second the scene opens; the guide's best
   second for a clade, or the founding second for a birth), `Plan` (a camera plan), and
   `Captions` (sentences with offsets). The seven stations are the ones the owner
   accepted, each a plan template:
   - **Arrival**: outside the glass at the surface, the rim a line, the crowd a haze
     below; a ten-second hold with a slow push of a metre or two. One caption: the arm,
     the second, the count.
   - **Descent**: a dolly down through the lit band along the shallow arc, the fog
     thickening, the sand rising on one side, ending a metre over the floor.
   - **Portrait**: a named body a third of the frame, the light from one side, the fog as
     the depth of field; the camera holds and the body does what it does.
   - **The floor**: low over the sand along the slope, across a hollow, what has settled
     in it; the bed view's angle, moving.
   - **Birth**: seek to the founding second of a clade, hold on the parent, the child
     appears and drifts within its disc. The caption names the change and stops.
   - **Colony**: pull back from a portrait until the clade shows as a colour in the
     crowd, then the disc from above (the census view as a chapter card, once).
   - **Time**: the same shot at two seconds thousands apart, a crossfade between them.
7. **The trip** is a scene list: from a heuristic the user picks (top ten by the guide's
   score; by guild; by depth band; by clade age; by firsts), the director builds arrival,
   descent, then for each clade a portrait and a birth or a colony, the floor once, time
   once, in an order that alternates stations so two portraits never follow each other.
   **Next** and **previous** move between scenes at any time; a **picker** jumps to a
   clade by name and plays its scenes; **auto mode** plays the list through with a pause
   of a second between scenes. Each scene ends with the camera parked, so the next begins
   with a cut. An individual clade is a trip of one.
8. **Seeking** uses the checkpoints (`checkpoint-spec.md` item 12): a scene at 25,000 s
   restores from 24,000 s and plays forward; the world on screen is a cousin from the
   restore and the provenance word says so. A subject alive at the checkpoint keeps its
   recorded id, so a portrait of a named body finds the body; a birth after the restore
   is the cousin's, and the director seeks a founding by restoring from the checkpoint
   before it and waiting for a birth in that clade's parent clade, naming what it got. A
   scene without a checkpoint behind it replays from zero, as now, and the director says
   how long that will take before it starts.
9. **The camera's grammar**, the owner's rules and the agent's:
   - **One move per shot**; nothing faster than a body swims (the campaign's bodies move
     at the water's 0.07 m/s and a swimmer under 0.5 m/s; the camera's ceiling is a body
     length every few seconds, and the arc's radius sets the lens).
   - **Following is an orbit, not a tail**: the plan draws an arc around the subject,
     horizontal, vertical or both, a quarter to a half turn by default, a full circle only
     when the scene asks; the subject off centre by a third; a slight lead so the body
     swims into the frame; consecutive scenes draw different arcs (the plan's family is
     chosen from the scene index and the clade's hash); a sitter gets a slower, tighter
     arc than a swimmer, read from the guide's speed.
   - **Strong moves ease in and out** (a smoothstep of the move's length; the stronger the
     move, the longer the ease, a fifth of the move at each end); small drifts inside a
     hold stay linear.
   - **Light** from above and slightly behind the camera (the skin's sun stands; the
     portrait adds a fill from the camera's side at a low strength, theatre only).
   - **The camera never clips** the sand or a body: every plan is checked before it plays
     against the bed's height map and the bodies' radii at the scene's second, and a plan
     that would clip is lifted or pulled back until it does not.
   - **No whip pans**, no box, no ring lines, no markers for small bodies, no interface
     but the provenance word in a corner.
10. **Captions**: one short sentence, white, lower third, four seconds, from the card's
    facts through templates ("Founded at 12,400 s from Ostrea lenta; the split added a
    joint"); never an interpretation. Each caption's second and text goes to the log.
11. **The interface**: a small panel (UI Toolkit, under the interface's rules) with the
    heuristic, the scene list with the current scene marked, next and previous, the
    picker, auto, record; hidden with `H` like the rest, and hidden by default while
    recording. Keys for next and previous as well.

## The record

12. **Record** starts the Unity Recorder (`com.unity.recorder` 5.1.6 is in the manifest
    and unused) through `RecorderController` from an Editor script in
    `Evosim.Theatre.Editor`, at the Game View's size or a chosen one, to
    `scratch/safari/<arm>/<date>/`, with the caption log beside the clips and one clip per
    scene or one for the trip, the user's choice.
13. **The overlay bug is fixed on the way** (HANDOFF's queued item since 2026-09-13: the
    Recorder's capture hides the interface): the director records from a render texture
    into which the world camera draws and the panel composites, the route
    `TheatreUiCapture` already takes for pictures, with the Recorder's render-texture
    input; the Game View capture is not used. Reproduced first on a worker with a
    graphics device, then fixed, then the fix shown in a recorded clip with the interface
    on.

## Validation

- **The guide**: on `r38-s1`, the clades agree with the scorer's; the names are stable
  across two runs of the script; a card's facts are spot-checked by hand against the
  lineage and the positions for three clades; the `.md` passes the style check.
- **The director, headless**: `TheatreSafari.Run` plays a trip on a recorded smoke and
  writes a frame every two seconds into `scratch/snaps/safari/<arm>/`. The agent looks at
  the frames and sends them to the owner. A check over every frame says the camera is
  above the bed and outside every body's radius, and that no move between frames
  exceeds the ceiling.
- **The record**: one clip of one scene with the interface on and one with it off, both
  opened and looked at; the caption log's seconds against the clip's length.

## Not in this pass

Species by genome distance, the ledger's economics on the card, narration and music all
wait. An LLM polish over the template's prose is off by default if it is ever built, and
the facts never come from it. Filming a live arm is out, because the theatre replays a
recording no faster than the farm ran it.

## Cost

Four to five days: the guide in a day, the director's plans and stations in two, the trip,
picker and auto in one, the record and the overlay fix in one. The overlay fix needs a
worker with a graphics device once; the rest runs in the owner's Editor.
