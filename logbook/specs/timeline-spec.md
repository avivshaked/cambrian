# The timeline: a scrub mode and charts over a recorded run

**2026-09-15**  ·  the first of three tools the owner asked for on the evening of
2026-09-15 (`video-tools-notes.md`).

> "something like Civilization does where you can play the timeline and you are the graph
> of a thing over time and can jump around in the timeline"

It is theatre only. It moves no hash and needs no worker. It is built first because it makes
every later question faster to answer, ours and the videos'. This goes in front of the owner
before building.

## What it is for

The theatre reaches a second by re-simulating from zero (`TheatreRunner.BeginSeek`, a loop
of `TheatreReplay.Step` until the clock passes the target). So finding the moment worth
watching costs as long as the farm took to get there.

The recording itself is instant. Every sample of `positions.jsonl` holds every living
body's place and guild, and every row of `stats.jsonl` holds a hundred and eighteen numbers
about the world.

The timeline draws the recording and lets a reader move through it at will, with the numbers
plotted over the run above the world they describe. It is faithful by construction, because
it shows nothing the run did not write.

## The scrub mode

1. **A third mode**, `ViewMode.Record`, beside World (Mode B) and Solo (Mode A):
   `TheatreRunner.Mode`, or `EVOSIM_THEATRE_MODE=record` from a script.

   It opens the same run directory and reads `config.json`, `run.json` and `stats.jsonl`
   (`RunRecord`, as now), then `positions.jsonl` and `lineage.jsonl` (`LineageIndex`, as
   now). The positions file is read whole, into memory: three hundred samples of 1,600
   bodies at five numbers each is under 20 MB as floats.

   It builds no `Ecosystem` and runs no physics. It refuses a run without a positions file,
   which means a tiled world or anything recorded before 2026-09-10, with a message that
   says why.

2. **The world is drawn from the recording**: the water, the glass, the drape and the
   shaped bed appear as they do in World mode, from the config and the seed. `BedShape` is
   rebuilt the same way, and `SeaFloor` gives its mesh without its collider.

   Each body of the current sample is one marker at its recorded place. The marker is a
   sphere, coloured by the skin's guild colour from the row's flags. Its radius is the
   genome's adult body, taken from the latest snapshot at or before the sample that holds
   the id.

   A body in no snapshot, born and dead inside one snapshot interval, is drawn at its
   guild's median adult radius and hollow, and the legend says so. Growth is not in the
   recording, so every marker is adult-sized, and the strip says "adult sizes" once. Markers
   are instanced (`Graphics.RenderMeshInstanced`), so 5,000 of them cost one draw.

3. **No motion between samples**: they are 100 s apart and a body drifts metres in that
   time, along a path the recording does not hold, so nothing is interpolated. Scrubbing
   shows the nearest sample and the clock reads that sample's second. Play advances one
   sample per tick at the chosen pace, and the camera is free to move while the world holds.
   This is a rule and not a limitation to fix later, because an interpolated body is a
   fabricated one.

4. **Seek is instant** from the bar, the charts (below), the `K` key and
   `EVOSIM_THEATRE_SEEK`. Backwards as easily as forwards.

5. **The interface reads the row**: the census panel shows the sample's `stats.jsonl`
   values in the fields it already has, which are alive, births, deaths, the audit and the
   residual. The strip's provenance word takes a third state, **record**, with a popover
   that says the recording is stills 100 s apart and names the two files.

   Clicking a marker selects the id. The inspector shows what the lineage holds, which is
   birth, parent, generation, guild, patch and death if dead. That is the dead panel's
   content today, and nothing the recording does not have: no reserve, no age, no phenotype.
   Follow (`F`) locks the camera to a marker across samples. At a glance, that is what a
   clade's drift looks like.

6. **The free camera, the skin's keys and the snapshot views work unchanged**, so
   `theatre-snap.ps1` can take a picture of any sample of any run in seconds. There is no
   replay to wait for. Its `-Mode record` (default `world`) is the one switch, and the label
   on the frame says "record" where it says "faithful".

## The charts

7. **A charts panel** across the top of the bar's timeline, hidden until toggled by a key
   chosen at build from the unbound ones, shown in every mode. It holds **lanes**, stacked
   under a shared time axis. A lane is one field of `stats.jsonl` plotted over the whole run
   on its own vertical scale. They are small multiples rather than one pair of axes, because
   two series on one pair invite a reading the numbers do not support.

   Four lanes come up by default: `alive`, `absorptiveInherited`, `photosyntheticInherited`
   and `detritusCv`. A picker lists every numeric field of the row, arrays excluded, and a
   lane is added, removed or swapped from it, with the choice persisted in `EditorPrefs`.

   Each lane is one `VisualElement` painted with `Painter2D` in `generateVisualContent`, the
   route the play glyph already uses. The series is a polyline of the samples, with no
   smoothing.

8. **The cursor** is a vertical line across every lane at the current second, moving as the
   world plays. Hovering shows the second and the lane's value at the nearest sample, and
   clicking or dragging on any lane seeks there. In Record mode the seek is instant. In
   World mode it is the replay's seek, forward only until the checkpoints land and from the
   nearest checkpoint after that, and the cursor moves when the world arrives.

9. **Events on the axis**, as marks with a one-word label, taken from the run's own files.
   They are a clade's founding, a boom's peak and its bust, a throw (`diverged` rising), a
   snapshot, the identity check's first mismatch in World mode, and the run's end. A
   founding comes from the scorer's parent walk, generalised: a birth whose flags differ
   from its parent's founds a clade, and the mark names the guild. A bust is an inherited
   count over 300 that falls under 50 within 10,000 s, the reading round 38 introduced.

   The safari's guide uses the same walk (`safari-spec.md`), so it is written once, in C# in
   the theatre. The scorer's `Get-Clades` is its reference, and a test says the two agree on
   a recorded run.

10. **A picture of the charts** through `TheatreUiCapture`: `theatre-snap.ps1 -Charts
    alive,detritusCv -At 30000` writes the panel at the frame's width as its own PNG. It is
    for the entries, which have no plots of their own today, and it is last in the build.

## The rules it keeps

- **Nothing is written into the run directory** (D075), and preferences go to `EditorPrefs`.

- **`simHash` does not move**: everything lives under `Assets/Theatre/`.

- **The interface's own rules hold** (`design/SPEC.md` §9). No hue in the chrome except
  the alarm plate, so a lane's line is the chrome's grey and a guild's colour appears
  only on a body. The type scale and the density step stay as they are. Nothing moves on
  screen but the cursor and the four fields that already tick.

- **Long files are read the way the runner reads them** (`JsonlWriter.ReadRows`, so a
  live run's file can be opened), once at open. A reload (`R`) re-reads a growing run's
  tail.

## Validation

- **A fixture in `TheatreUiCheck`**: open a recorded smoke in Record mode and seek to a
  sample. The marker count must equal the row's `n`, the census must equal the row, and
  the cursor must sit at the row's second. Toggle the charts: a lane's polyline has one
  point per sample, with the first and last at the row's values. Pictures at 1920 and
  3840 into `scratch/snaps/ui/` as the other states have.

- **The clade walk against the scorer**: on `r38-s1`, the founding marks equal
  `clade-score.ps1`'s clades for `abs` and `pho` (same roots, same member counts).

- **A look**: pictures of `r38-s1` at 5,000, 15,000 and 30,000 s in Record mode beside
  the replay's frames at the same seconds. They must show the same crowd in the same
  places, since the replay's positions are what the file holds.

## Not in this pass

Interpolation between samples, which is the rule above. Growth in the recording: a per-body
size in `positions.jsonl` would be a Core change and a new file version, and it is asked for
if the markers mislead. Charts of `positions.jsonl` derivatives, such as depth by guild or
the rim quarter. The python reader has them, and a lane over a derived series is a later
addition once the panel exists. Comparing two runs on one chart.

## Cost

One to two days: the mode and markers in a day, the charts and events in the second, the
picture last. It runs in the owner's Editor, with no worker.
