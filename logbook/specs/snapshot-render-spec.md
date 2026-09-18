# A still frame from the run's own data: the snapshot render

*2026-09-18, night. Written by the agent on the owner's ruling ("proceed") after the day's
renders: round 39 seed 1's 30,000 s frame cost a full re-simulation from 15:11, and round
41's first early look timed out on the snapshot tool's 30-minute wall with nothing written.
The owner's question was whether a picture needs the world to step again. It does not.*

## 1. What it is

A third way to take a picture with `theatre-snap.ps1`, beside the faithful replay and the
cousin. The farm already records, on the same cadence, everything a world view needs:

- `snapshots/NNNNNNNNN.jsonl` every 1,000 s: one row per living body, its genome with the
  organism `id` (format 6).
- `positions.jsonl` every sample: `{"t":..., "n":..., "b":[[id, x, y, z, flags], ...]}`,
  every living body's centre and guild flags (bit 0 absorptive, bit 1 jointed, bit 2
  photosynthetic, as `scripts/positions-read.py` reads them).

Join the two on `id` at a snapshot second, develop each genome, set the phenotype down
with its centre at the recorded position, draw it in the theatre's skin, and photograph
the box from the usual views. No physics steps, no identity check, no wall to time out on.
The frame is a drawing of the run's data at that second and its label says so.

## 2. What it gets wrong, and says

Two things are not recorded and are not invented silently. **Orientation**: every body is
drawn in the developer's own frame, root upright, unrotated. **Size**: a body is born at a
fraction of its adult body and grows, and only the adult is in the genome, so every body is
drawn at its adult size. Both are stated in the burnt-in label on every frame, in the log,
and in the tool's help. The `close` view is refused in this mode with a reason naming both,
because a portrait of two bodies is exactly where they show. A world view at thirteen
pixels a metre shows neither.

## 3. The entry

The same `Evosim.Theatre.EditorTools.TheatreSnapshot.Run`, selected by
`EVOSIM_THEATRE_SNAP_FROM=snapshot` (`replay` or unset is today's behaviour, unchanged to
the bit). `theatre-snap.ps1` gains `-From snapshot|replay` (default `replay`), prints it
in its settings block, and passes it through. The Unity command line is the same as the
replay's (`-batchmode`, no `-quit`, no `-nographics`): a picture needs a graphics device.

Requested seconds must be snapshot seconds. A second with no snapshot file is refused
before the Editor enters Play mode, naming the nearest snapshot seconds on either side. A
snapshot second whose `positions.jsonl` row is missing is refused the same way. The frame
is taken at the snapshot's own second and the label prints that second.

## 4. The join

For each requested second: read the snapshot rows (`JsonlWriter.ReadRows`, never
`File.ReadAllLines`; the run may be live) into a map by `id`; read the positions row at
`t` equal to that second. A body in both is drawn. A body in the positions row without a
genome, or a genome without a position, is counted and not drawn. The log prints
`[Theatre] snapshot from snapshots/000002000.jsonl: 655 genomes, 655 positions, 655
joined, 0 without a genome, 0 without a position`, and the label carries the joined count
and the unmatched count when it is not zero. The tool does not refuse on unmatched bodies:
a live run's writers can be a sample apart, and the count is the reader's caveat. The
positions row's guild flags are checked against the developed phenotype's guilds and a
disagreement is counted and logged, never silently corrected.

## 5. The bodies

Develop each genome with `Evosim.Core`'s developer exactly as Mode A does for a solo body
(`SoloCreature.cs` is the reference for going from a snapshot row to parts in the scene),
at the genome's adult scale. Build each part as a plain renderer with the skin's meshes
and `TheatreBody.shader` carrying the guild on the rim, the carve at the run's depth as
the replay draws it; no `ArticulationBody`, no collider, no physics. Place the phenotype
so that its centre of mass is at the recorded `(x, y, z)`; the developer's frame gives the
orientation. Physics is not stepped in this mode: set `Physics.simulationMode` to script
(or the project's equivalent) for the session so that nothing moves between build and
shot. A world of 3,000 bodies at three parts each is under 10,000 renderers, which the
Editor draws in one frame; if building them takes more than a frame, build across frames
and shoot when done, as `Drive()` already waits for the replay to reach its second.

## 6. The furniture and the views

The skin's world, the bed, the glass, the water, the surface, the snow and the fog, is
built from the run's `config.json` exactly as the replay builds it, so a reconstructed
frame and a replay frame of the same world are comparable at a glance. The views are the
replay's (`side`, `end`, `top`, `iso`, `bed`, and whatever else `SnapshotCamera` names),
fitted from the config as now, at the same sizes. `close` is refused (§2). `-Chrome` is
refused in this mode: the interface reads a replay's census and there is none.

## 7. The label

The replay's label says *faithful* or *NOT A FAITHFUL REPLAY*. This mode's says
`RECONSTRUCTED FROM SNAPSHOT` on its own line, then the arm, the second, the joined count,
and `adult size, default orientation`; when unmatched bodies exist, `N unmatched`. The
arm, seed and hashes come from `run.json` as they do now; no hash check is made, because
no build is being compared, and the log says `no identity check: nothing was simulated`.
Pictures land in `scratch/snaps/<arm>/` beside the replay's with `-recon` in the file
name before the view (`r41-s1-t6000-recon-side.png`), so a directory never mixes the two
kinds under one name.

## 8. What does not change

Nothing under `Assets/Evosim`, nothing in `src/Evosim.Core`, nothing in the replay path.
`simHash` and `coreHash` do not move; the check is the `simHash` of a worker's manifest
before and after (`scripts/run-arm.ps1` prints it; a recorded run must still replay). The
theatre's Mode A and Mode B are untouched. `positions.jsonl`'s and the snapshots' formats
are untouched: this is a reader.

## 9. Acceptance

1. `theatre-snap.ps1 r41o100b3k-s1 -At 2000 -From snapshot -Views side,top -Worker 6`
   writes `r41o100b3k-s1-t2000-recon-side.png` and `-top.png` inside a few minutes of
   Editor start, the log's join line reads 655 genomes, 655 positions, 655 joined, and
   the label reads as §7.
2. `-At 2500 -From snapshot` is refused before Play mode, naming 2000 and 3000.
3. `-Views close -From snapshot` is refused with the reason in §2.
4. `-From replay` (and no `-From`) against the same run at 2000 s behaves exactly as
   before; the replay's frame and the reconstructed frame of the same second sit side by
   side in `scratch/snaps/r41o100b3k-s1/` and show the same crowd in the same places.
5. The main tree's `unity/Assets/Evosim` is byte-identical before and after (`git status`
   shows nothing under it).

## 10. Where it goes afterwards

CLAUDE.md's "An agent has no eyes" paragraph gets the mode in a sentence with its two
caveats; `theatre-snap.ps1`'s help block describes `-From`; DESIGN §6.1's theatre line
notes the reader. Round 41's frames from 6,000 s on are taken this way, and the entry's
pictures say `reconstructed` in their captions. Late frames by re-simulation stop being
the default; the replay is for watching motion and for a picture that has to be faithful.
