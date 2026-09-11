# Build spec: the theatre's skin, third day: the box that stops looking made (owner, 2026-09-11 midday)

Read `research/theatre-look/README.md`, `logbook/specs/skin-spec.md`, `logbook/specs/skin-spec-2.md`,
then `unity/Assets/Theatre/TheatreMeshes.cs`, `TheatreSkin.cs`, `TheatreBody.shader`. Do not
touch `SnapshotCamera.cs`, `TheatreRunner.cs`, `WaterBounds.cs` or `CreatureIdMap.cs`: the box
branch edits them (`fable-propose-box.md`) and the two land separately.

The owner looked at the second day's close views of round 35 seed 1 at 5,000 s and said the
spheres look amazing and the boxes have improved but still look somewhat non-biological. The
same second at carve 0.35 already looked better to the owner than at 0.2. So the day has two
parts: the carve's default moves to the depth the owner chose, and the box gets what the carve
cannot give it, a silhouette that is not six planes at right angles.

Rules as on the second day: edit only under `unity/Assets/Theatre/`; nothing under
`unity/Assets/Evosim` or `src/`; no binary assets; nothing written outside the repository; no
commit; never wait; never run Unity against `unity/`; every worker is busy and the editor cap
is full, so nothing is compiled during the build and the caller compiles and photographs
afterwards on the first free worker. Doc-comment register as before: why, with the source.

## The one invariant

No vertex and no pixel displacement may lie outside the part's physics collider, which for a
box is the box of its three half-extents. Every item below says how it keeps that, and the
last word is a clamp rather than an argument about signs: a box vertex is clamped into the
mesh's own box after every displacement, so a dial past its range or an arithmetic slip cannot
put it outside.

Spheres and capsules are left as they are, apart from the carve default and the rim. The
reason is the size bound rather than taste: their colliders are the ball and the capsule, so
the box clamp that makes the bend safe would not be holding them inside anything the physics
has.

## The items

1. **Pillow.** The cube mesh's corner radius becomes a dial, `EVOSIM_THEATRE_PILLOW`, a
   fraction of the mesh's half in object units, clamped to [0, 0.5]. The second day's rounding
   was a constant 0.34, so the default is 0.34 and the dial changes nothing until it is set. At
   0.5 the cube is the ball inscribed in the box. The cylinder's rim fillet stays a constant, so
   a pillow sweep cannot change a capsule. Inside the collider because the rounded cube is built
   inside the inset box and never pushed out of it.
2. **Taper.** A box narrows toward one end along its longest axis by `EVOSIM_THEATRE_TAPER`
   (default 0.35, clamped to [0, 0.8]). The cross-section at the far end is one minus the dial
   of the full one. The narrowing is eased by a smoothstep rather than linear, because a linear
   narrowing is a wedge and a wedge is a made thing. Which end narrows is drawn from the seed
   the carve and the mottle already use, so it is a property of the creature and cannot
   flicker. Inside the collider because every lateral coordinate is multiplied by a number
   never above one.
3. **Bend.** One half-wave of lateral displacement along the longest axis, zero at both ends
   and deepest in the middle, in a direction across the axis drawn from the same seed. The
   amplitude is `EVOSIM_THEATRE_BEND` (default 0.15, clamped to [0, 0.4]) times the part's
   smallest half-extent. The cross-section gives the amplitude up before it spends it: it is
   shrunk by the amplitude and then displaced by it. So the outside of the curve reaches the
   wall at the deepest point and nothing passes it, and the position is clamped afterwards
   regardless.
   The normal is the inverse transpose of the map's Jacobian applied to the old one, so a
   tapered box shades as a tapered one and the carve's impressions sit on the bend.
4. **Carve default 0.35**, from 0.2, in the shader, in the skin and in `theatre-snap.ps1`'s
   `-Carve`. The script always writes the variable, so a default left at 0.2 there would have
   undone the ruling exactly where pictures are made. The clamp stays 0.5.
5. **Rim.** The Fresnel rim wider and softer by about a third (power 2.6 to 1.75, strength 1.5
   to 1.3), so the silhouette stops reading as a hard line drawn around the collider. Guild
   colours unchanged.

## Where the shape lives

The taper and the bend need the part's proportions, which axis is longest and the smallest
half-extent. Those arrive only as the object-to-world matrix, while one unit cube is shared by
every box in the world. So the pillow, the same on every axis, is baked into the mesh, and the
taper and bend are per-vertex work in the vertex shader. The mesh tells the shader what solid
it is through a fourth UV channel: one on the box, zero otherwise, with the mesh's own half in
the second component. A mesh that carries no such channel reads zero and gets the carve alone.

Order in the vertex stage: taper, bend, clamp, then the carve along the deformed normal. For
a box the carve's displacement is taken back into object units and clamped a second time
before the world position is rebuilt. The carve field is read at the undeformed object
position, so an impression stays on the same piece of tissue when the part is tapered and
bent. The fragment stage asks the same field at the same place to rebuild the normal.

## Known limits

- The bend is keyed per part rather than per body. The shader's seed is the creature's id mixed with
  the part index, so two parts of one body lean differently rather than as one. A body-wide
  lean is a two-line change in `TheatrePalette` when the owner wants it.
- Above roughly bend 0.4 on a very flat part the clamp starts shaving the outside of the curve
  flat rather than letting it out. That is the correct failure direction, and worth knowing
  when picking from pictures.

## Pictures

The comparison set for the owner is round 35 seed 1 at 5,000 s, close view, the crowd of the
second day's pictures: the defaults; carve 0.5; pillow 0.5. One worker, refreshed with the new
`Assets/Theatre` only, so the recording still replays.
