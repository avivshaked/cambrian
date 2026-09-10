# 0088 — A skin for the boxes

**2026-09-10**  ·  the theatre's first skin, built in an afternoon from a reading of how others made boxes look alive; what it changed and what it cannot show

The owner's ask, after the first pictures: "how about we give them some skins that would
make them look more biological? break that harsh geometric look", and skin in both senses,
the tissue and the graphical layer. A short reading was done first
(`research/theatre-look/README.md`): what Sims, Framsticks, Keiwan's Evolution, Species,
the Bibites and Slug Disco's Ecosystem did about boxy bodies; what shading is cheap enough
for two thousand bodies; and how plankton is photographed so that a transparent animal can
be seen at all. The last of those decided the look: dark field. A black background, light
from behind or beside, and the colour on the edge rather than the face.

## What was built

All of it under the theatre, so no recording is orphaned and the farm never knows.

- **Lighting and water.** Near-black blue water, exponential depth fog, low ambient, one
  raking light from above and behind, a weak fill from the other side.
- **One body shader.** A Fresnel rim carrying the guild's colour, muted, on a darker body of
  the same hue; a single-pass faked subsurface term, warm where the light is behind a thin
  part; a Worley cell mottle in world metres, so a large body has more cells than a small
  one; a faint inner glow from the body's reserve; caustics fading out below the top metres.
- **Rounded bodies, inside their colliders.** A rounded cube and a smooth sphere generated
  at start, inset to 0.97 of the collider, swapped in by mesh name so the theatre forms no
  opinion of its own about which primitive a part is; a vertex puff bounded at 0.03 of the
  smallest half-extent, so that a puffed vertex lands on the collider and never past it.
  The growth resize still applies, because the transform is untouched.
- **Joints.** A short neck between the two anchors, drawn inside the parent's footprint,
  recomputed on every paint because bodies grow. No collider.
- **The water's furniture.** Marine snow as one particle system clipped to the box in the
  shader, procedural caustics on the bed, a sand bed with a triplanar ripple normal map,
  cooled to near grey because warm sand sat on the stomachs' hue.

## What the pictures show

Round 35 seed 3 at 3,000 s, faithful on every sample. From above: 403 bodies, green
producers and brown stomachs telling apart at a glance, the few near-black ones starving,
a long segmented body reading as a chain, the bed a dim grey floor. From the side: green
in the top twenty metres and brown below to forty-five, which is what the positions
reader says (leaves at a mean of 12 m down, stomachs at 32). The top view checked against
the plot of the recorded positions at the same time: the stomach cluster, the two isolated
stomachs and the sparse band all coincide.

Three faults found by reading the pictures, all fixed: the sand read as a lit beach,
because URP's fog does not apply under an orthographic camera; the snow was invisible,
because the emitter's warm-up ran one duration and the lifetime was eleven minutes; the
snow leaked across the narrow axis, visible only from above.

## What it cannot show yet

A joint's neck is a feature of centimetres and the world view has a metre to thirteen to
twenty-four pixels, so necks read under the follow camera and in Mode A and not in a
picture of the box; the size rule won that trade, as the spec ordered. The 3% puff is
invisible; the rounding does the softening. No mixotroph lived in the run photographed, so
that guild's legibility is untested. The three-quarter view loses the bed to fog at 60 m.
The second day, if wanted: ripple keyed to the water, iridescence, eyes.

## Sources

`research/theatre-look/README.md`; `scratch/skin-spec.md`; `unity/Assets/Theatre/TheatreSkin.cs`,
`TheatreMeshes.cs`, `TheatreBody.shader`, `TheatreBed.shader`, `TheatreSnow.shader`;
`scratch/snaps/r35-s3/`; D075, D088; logbook/0086.
