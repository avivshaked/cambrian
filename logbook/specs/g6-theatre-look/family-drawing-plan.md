# G6, the family drawing: the plan

G5 passed on 2026-10-29 (`logbook/specs/r63s-read/`), so D233's shape family and the shaped added mass go into G7's round,
and the owner wants the shapes seen in the film. This plan says what the theatre needs to draw a family limb from its
shape genes, where it is built, and how its first stills are checked against r63s's runs. The theatre lane wrote it on
2026-10-30 for the coordinator, before any build.

## What exists, and what is missing

- **Parts drawn one by one.** `EVOSIM_THEATRE_FAMILY_MESH` (on the trunk, off by default) draws a superellipsoid part as
  its own solid. Every vertex sits on the surface by Core's `Superellipsoid.SurfacePointAndNormal`, with Core's normal, so
  there is one mesh to a form. On the bench, Core's surface points sit on the mesh for seven forms. A part with no family
  keeps the box.
- **The one skin.** The film look draws every animal as one skin (levels 3 wide and 6 close), which has no superellipsoid
  case. The skin builds a distance field for each part, as a box, ellipsoid, capsule or ball. At levels 5 and 6 it also
  builds the part's collider, and it meshes the field with surface nets. A family part falls to the box branch in both
  places, which is the gap: the film shows a family limb as a box, and a tapered limb loses its taper.
- **Reading r63s.** The theatre branches are 76 commits behind main, which holds r63s's build (`d03a19b2`, in main).
  Their strict config reader refuses r63s's config, whose 67 keys r62d's lacks include `development.shapeIds` and
  `fluid.shapedAddedMass`. The tolerant picture reader (`PictureConfig`) would default `shapeIds` to none, which would draw
  every family limb as a box. So the theatre must have main merged in before it can draw r63s at all.

## The build

1. A branch, `theatre-look-family`, in a new worktree `scratch/wt-theatre-family`. It starts from
   `theatre-look-remains-port` (`65858963`), which holds hold51, hold52 and the remains theatre half. Main (`9235a0fc`) is
   merged into it.
   - remains-farm is already in main, so the farm half should merge as the same lines.
   - The merge touches no theatre file, so I resolve any conflict by hand and list each one.
   - `theatre-look-skinmap` is merged in once phase6f's sheet passes. Its dial is off by default and touches no shared
     line, so the order does not matter to the film.
2. A dial for the skin, `EVOSIM_THEATRE_FAMILY_SKIN`, 0 by default. At 0 its lines do not run, so a frame is the same
   to the pixel. At 1:
   - a new primitive kind is built for a part whose shape is a superellipsoid with a family.
   - Its field works in the part's frame, which is shifted to the centre of mass. Core's inside function F, raised to
     the power ε1/2, grows in step with distance along a ray. One less than that, times the smallest local half extent,
     is zero on Core's surface and under the true distance elsewhere, as the ellipsoid case's bound is.
   - Beyond the solid's ends Core's function is infinite. There the field is the larger of the slab distance and the
     estimate at the nearest end.
   - It calls only Core's public `InsideFunction` and the form's exponents, so Core does not change and neither does any
     hash.
   - At levels 5 and 6, `ColliderOf5` gets a fourth collider kind with the same field, so the skin's hinge clamp holds
     family tissue inside the solid the world simulates (D252).
3. Opt-outs:
   - A family limb takes no paddle and no drawn-box taper or bend. Its shape is already what the physics has, and a
     second taper would draw it unlike its collider.
   - A family part that is a producer's blade keeps the alga dress, which sizes a blade by its half extents. The stills
     show whether it reads right.
4. The film look sets `FAMILY_MESH 1` and `FAMILY_SKIN 1` once the first still check passes. The theatre's
   `CLAUDE.md` gets one paragraph on the new dial.

A Sonnet subagent writes steps 2 and 3 from a brief that names the files and the field. I review the code and judge
every picture myself. Writing code needs no slot, but compiling and rendering wait for the coordinator's slot.

## The first still check, against r63s

- **The subjects.** A picker, `scratch/family-draw/pick_family.py`, uses `g5-read.py`'s family-limb rule at the snapshot
  at 24,000 s of `runs/r63s-on-6202` and `runs/r63s-on-6203`. It picks eight bodies:
  - six with a family limb, grazers first: the read counts 22 grazers with a family limb on 6202 and 40 on 6203. Their
    forms span the near-box, the near-capsule and the most tapered.
  - two jointed bodies with no family limb, as controls.
- **One slot on worker 7, one Unity at a time, under the chain's guard:**
  - Step 0 compiles the branch. Then it runs the dials-off check, r62d against the pace references, which must be
    IDENTICAL: the merge must not move the off path. r62d is still read through the tolerant reader. Last come the
    film-look smokes, wide and close.
  - Step Ѯ draws portraits of the bodies, seeked from the run's own checkpoints, in today's box fallback and in the film
    look with the family skin, wide and close. Solo stills cannot be used: `TheatreSoloStill` builds a body through
    `PhenotypeBuilder`, which has neither the one skin nor the family mesh. A live portrait goes through `LiveWorldView`,
    which has both. r63s is a card run, so the safari passes `-AllowSourceMismatch`, the dynamics replay opens engine
    `gpu`, and every frame is labelled a cousin.
  - A live portrait restores the whole checkpoint, about 8,300 bodies, for each scene: 4 minutes a scene in the wide look
    and 12.7 in the close one, whose skin logged two lines a body with a stack trace each. The batch safari now logs plain
    lines without a trace, and `theatre-safari.ps1` removes the lockfile its own Editor leaves after a wall or a crash.
    The 6202 check drew wide and box in full and three of five close scenes before the close look's wall; the
    coordinator ruled on 2026-10-30 that this is enough for the family skin.
  - Step 2 draws one wide crowd frame of r63s-on-6202 at 24,000 s in the film look, once a film window of r63s exists.
- **The checks:**
  - The family skin's log line gives the field's largest gap over Core's surface points: 0.000 mm on 6202.
  - No skin tissue stands outside a family limb's solid.
  - A control body is unchanged on its own pixels, within 2 levels, between the family dials off and on. The whole frame
    is not the test: the family skin reshapes every producer with a family part around the control, and the light and
    depth-of-field blur at its edges move by a level or two. On 6202, 67997's own pixels were within 1 level at the 99th
    percentile and 6 at most, while 440,000 pixels of its frame differed.
  - The tapered limbs read as tapered in both skins.
  - The sheet sets each still beside the owner's best-so-far frame (`best-so-far-53880.png`) and the realism references.
- **The owner's preview.** The pair, box today against the family drawn, goes to `scratch/owner/look-previews/` as the
  next numbered item, with a ping. The work does not wait on it.
- **A producer's family part takes its true shape, not the blade dress.** The coordinator ruled this on 2026-10-30, under
  D242's rule that the picture is what the world simulates. A flat family part still reads as a blade, and a thick one is
  drawn as the thick solid the world swims and drags. The owner can overrule from preview 19.
- **The blades' colour.** r63s's world is r63c-b's, whose blade row is held until the film's world is chosen. The stills
  use r62cf's row and are labelled so.

## For G8, the film

The film's look belongs to the story-video lane, whose settings this lane does not edit. The coordinator ruled on 2026-10-30
that G8 carries the following into the film's look, and the coordinator hands them to the film lane when G8 starts:

- `EVOSIM_THEATRE_FAMILY_MESH 1` and `EVOSIM_THEATRE_FAMILY_SKIN 1`. The family drawing is checked on r63s-on-6202 and
  merged with the skin map at `3bfdef6a`, where dials-off is identical and both film-look smokes pass. Both dials are inert
  on a run with no family genes. The theatre lane's own slot look has carried them since the same day.
- The skin map, once the owner rules the palette's path. The theatre lane's pick is `EVOSIM_THEATRE_SKIN_MAP 2` with
  `EVOSIM_THEATRE_SKIN_MAP_DEEPEN 0.5` (`logbook/specs/skin-map-hue/dials.md`). The owner's question and its options are in
  that record.

## Open

- Phase6a checks the remains port alone. Step 0 above checks the same code with main merged in, so 6a could be folded into
  it to save a slot. If it is, a failure would need a bisect. That choice is the coordinator's.
- No render has checked a growing family body yet. The one skin takes growth as a scale, and the family mesh scales by
  `FamilyScale`.
