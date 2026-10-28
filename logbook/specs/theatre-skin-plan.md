# Skin plan: the theatre direction of 2026-10-25

The owner set the direction on 2026-10-25, relayed by the coordinator: biological-looking forms over geometric bones,
judged against the marine-life photographs. Four pieces, each a dial that is off by default and pixel-identical when
off, proven the usual way (r62d dials-off against the last batch). No Unity until the coordinator gives a slot.
Times are agent time for the build, then machine time on the coordinator's slots; the owner's review is separate.

## Order

1. **CELL VIEW**, first, as the owner asked. Cheap, and a diagnostic for everything after it.
2. **ONE SKIN, the spike**: one body, then a handful, built and rendered. The decision point, for the owner, on sheets.
3. **COLOUR** and **MOTION**, built while the spike is under review. Both are small and independent of it.
4. **ONE SKIN, in full**: the tissues, markings, mouth and growth carried onto the skin; then a crowd.
5. **GENOME TO SKIN**, after the spike: one continuous map from a genome to its skin (section 5). Design only for now.
6. **SELF-SIMILAR DETAIL**, beside GENOME TO SKIN: the same pattern at several scales, ruffled margins and branched
   surface features, driven by the skin vector (section 6). Design only for now.

| Piece | Agent build | Machine | Depends on |
|---|---|---|---|
| CELL VIEW | 1 to 2 h | one batch, about 30 min | nothing |
| ONE SKIN spike | 4 to 6 h | about 1 h | CELL VIEW (to read what merged) |
| COLOUR | about 2 h | about 30 min | nothing |
| MOTION | about 2 h | about 30 min (24 fps takes) | nothing; ported to the skin in step 4 |
| ONE SKIN in full | 1 to 2 days | about 2 h of sheets | the owner's word on the spike |

## 1. CELL VIEW (`EVOSIM_THEATRE_CELL_VIEW`)

Every part drawn in one flat colour for its cell type, with a legend in the frame, so a creature can be filmed by its
cells. The eight types are Core's (`StandardCellTypes`): structural, link, neural, photosynthetic, absorptive,
consumer, buoyancy, anchoring. Eight distinct colours, chosen to stay apart under the water's blue-green and for a
red-green colour-blind viewer (a fixed qualitative palette, such as Okabe-Ito's eight).

- **How**: the palette sets one colour per part and a flag; the body shader, behind a branch on the flag, returns that
  colour lit by a plain wrapped key and ambient only. No mottle, carve shading, rim, pattern or water cut, so the
  colour reads true. The shape stays as drawn, carve included, so the cells sit on the real silhouette. Necks take
  the colour of the part they hang from, and mouth discs that of their consumer.
- **Legend**: a strip in the frame's corner, the type names and their swatches, through the existing burn-in and
  caption path. With `-NoBurnText` it goes to the take's captions file instead, so a film can add it in the edit.
- **Diagnostic use**: it settles questions that cost two batches this week, such as which parts of 57656 are absorbers
  and whether a subject carries the tissue a dial needs. The sheets of every later batch get a CELL VIEW tile.
- **Identity**: off, the palette never sets the flag and no legend is drawn.

## 2 and 4. ONE SKIN (`EVOSIM_THEATRE_ONE_SKIN`)

One smooth surface over a creature's body tissues, with its specialised parts drawn as today on top of it or through
it. The split is a setting, a list of types (`EVOSIM_THEATRE_SKIN_BODY`, default below).

### The split, and where the subjects argue otherwise

The coordinator's proposal: body = structural, absorptive, buoyancy, neural, link; specialised = consumer,
photosynthetic, anchoring. Three things argue for a change:

- **Consumer belongs in the body.** In round 62 the consumer part is the main body of every biter. Swimmer 44560 is a
  consumer box and a link; biter 14697 is a consumer, a link and a link; clades 4712, 18029 and 20356 are the same
  (`subject_tissues.py`, `clade_tissues.py`). With the consumer specialised, a biter's skin would hold only its links,
  and the biters, the animals the board's slugs and worms most resemble, would gain nothing. The mouth the owner likes
  is the disc at `MouthPoint`, and it stays a specialised feature drawn on the skin wherever the consumer part is.
  **Recommended default**: body = structural, absorptive, buoyancy, neural, link, consumer.
- **Leaf and holdfast stay specialised**, as proposed. Blades are separate sheets on the board (Ulva, Caulerpa), and
  grip pads are discrete.
- **Buoyancy is a judgement call.** A jellyfish's bell is its body, which argues for merging, but our floats are drawn
  translucent, and a translucent bladder merged into an opaque skin loses what makes it read. Merged by default, with
  the per-vertex tissue blend below carrying its translucency into the skin locally. If that blend reads badly in the
  spike, it moves to specialised.

### Method: a smooth union, meshed once per plan, skinned to the parts

Each body part is a signed distance field of its collider's shape, in the part's own frame: a rounded box, a capsule,
or the ellipsoid a sphere part is drawn as. The skin is the zero surface of their smooth minimum,
smin_k(d_1 ... d_n).

- **Honesty margin**: the polynomial smooth minimum is never more than k/4 below the plain minimum. So every primitive
  is inset by k/4 first, and the blended surface then lies inside the union of the colliders everywhere. The one
  exception is that it bridges a joint gap narrower than about k, as the necks already do. **Stated margin: 0 against
  the colliders, and gap bridges no wider than the neck's.** k is one number per body:
  k = min(0.5 × the median of the parts' smallest half extents, 3 cm).
- **Meshing**: sampled on a grid in the rest pose of the body's plan, at its adult size, then surface nets (smoother
  than marching cubes at the same resolution, and simpler), then two passes of Taubin smoothing. Cell size
  max(smallest half extent / 4, body extent / 64), so at most 64³ samples.
- **Motion**: the mesh is a `SkinnedMeshRenderer`. Its bones are the parts' frames, with position and rotation only and
  no scale (a part's GameObject carries its half extents as scale, which would stretch the skin). Each vertex is
  weighted to up to four parts by a softmax of −d_i/k, so the skin bends at joints the way the blend joins them.
  Unity's own skinning then runs on the graphics card, and the depth and shadow passes come with it.
- **Cost**: meshing about 0.3 M to 2 M distance evaluations per body (a guess at 10 to 40 ms in Mono, to be measured in
  the spike), once per plan per body and cached by (genome, plan). At window load, about a thousand bodies costs tens
  of seconds; per frame, only the skinning, on the card. Triangles: about 5,000 to 15,000 a body. A body under about
  40 px on screen keeps its parts (a level of detail by size), so a crowd costs no more than today.
- **Stability as bodies grow** (the round-48 lesson): growth is `Phenotype.Scaled(fraction^(1/3))`, a uniform scale as
  far as the call shows. So the skin is built once at adult size, and the bones' frames are scaled by the same linear
  factor every frame: no rebuild between growth steps, so nothing pops. A plan change (a new bud) does rebuild the
  mesh, and the old and new skins cross-fade over about 0.5 s through the existing dithered `_Fade`. The spike
  confirms that `Scaled` is uniform against the drawn parts.
- **Mirrored pairs**: a mirrored part's frame is improper (`PhenotypePart.Mirrored`). Its distance field is the
  primitive's, symmetric, so the union is correct. In skinning, the bone matrix times the inverse bind pose cancels the
  reflection, so the winding holds. The spike's first test is on n6202's mirror bodies (57656, 54071).
- **Markings across the skin**: PATTERN, MOTTLE_SOFT and RIM_BAND read rest-pose coordinates, not world or part
  space. Each vertex carries its rest-pose position in body metres as a texture coordinate, skinned with it, so a
  marking rides on the animal and runs across part boundaries without a seam.
  - PATTERN: its triplanar sampling moves from the part's frame to the body's rest frame, at one tile scale per body,
    as today.
  - RIM_BAND: it needs an outline. On the skin, e becomes the distance to the skin's silhouette in the body's thinnest
    rest axis, which is the mantle's margin for a flattened animal.
  - Per-tissue looks (colour, sheen, velvet, chromatophores): blended per vertex by the same weights, so a link's
    colour grades into the body's.
  - Carve: off on the skin at first. The smooth union is the organic form; the carve can return as a gentle relief
    once the spike reads well.
- **Specialised parts**: drawn as today, sitting on or through the skin. Leaves and holdfasts take their own shaders;
  the mouth disc is placed at `MouthPoint` on the consumer, which now lies under the skin, and projected out to the skin
  surface along its normal (still within its 1 cm stand-out).
- **Identity**: off, no skin object is made and the parts draw as now (r62d dials-off). On, the farm is untouched: the
  theatre moves no hash.

**The spike** builds the union and its mesh for 44560 (consumer and link), 57759 (three absorbers and a link) and one
mirror body. It skins them in one film window, and renders off / skin / skin + CELL VIEW, with the board below. It
measures the meshing time and triangle counts, and checks for popping across a growth step and a plan change. The owner
rules on it before step 4.

**What the spike showed, and the fixes.** The spike was batch b42 (`scratch/theatre-realism/b42/readings.md`). The
coordinator agreed these fixes on 2026-10-25, to be built after b43:
- **(a) Built from the drawn shape.** The spike's field is the collider primitives', so the skin is squarer and plainer
  than the tapered, carved parts it replaces. The field takes ShapeBox's taper (D199's ends) and, when ORGANIC is on,
  the organic silhouette, so the skin is at least as shaped as the parts.
- **(b) Markings in rest pose.** Per-vertex pattern and mottle on the rest mesh (Turk's route, section 5), so the look
  does not slide as the body turns.
- **(c) Tissue colour per vertex.** The colour is blended from the nearest parts' tissues by the skinning weights with
  a soft falloff. The link's purple and every part's tissue then survive on the skin as gradients, where the spike
  took one part's colour. This is the one-skin form of the owner's wish that cell types read on the creature.
- **(d) Thickness per vertex.** On the mirror body, a pale disc at the middle of the silhouette in every frame is the
  back light's transmission, not a highlight (inference, from the code; to be confirmed with a take at
  `EVOSIM_THEATRE_BACK=0`). The light-through-tissue term reads thinness as `_TransMetres / halfThick`. The skin passes
  the median of its parts' smallest half extents as its half thickness: a thin part's number for a thick, merged body.
  The whole skin then reads as thin, and the back light, which shines toward the camera, glows through its middle.
  The fix: at meshing, store each vertex's own half thickness, the distance along the inward normal to where the field
  turns positive again, halved, so a margin glows and a core does not.

## 3. COLOUR (`EVOSIM_THEATRE_DIVER_LIGHT`)

For close shots, the light a diver or a photographer brings, so saturated colour and strong margins read as in the
board's photographs, most of which are flash-lit.

- **How**: a URP spot light parented to the camera, white, its range and strength set from the shot's subject
  distance. The body shader attenuates it per channel over the two-way path, camera to body and back, by the water's
  own absorption, red going first. So at 0.5 m colour is nearly a flash's, and at 3 m it is gone into the blue.
  Physically, that is why close photographs are colourful and wide ones are blue.
- **Stronger rims and patterns**: under the dial the rim band's strengths and the pattern's opacity rise, toward the
  board's contrast (the coordinator's two points from b37). The dial's levels: 1 the light, 2 the light and the
  stronger markings.
- **Caution**: the owner accepted the fixed look (back light 0.4) as the default for films on 2026-10-23. This is an
  option for close shots, never a change to that default.
- **The algal blades' colour and flutter** (the owner's ruling of 2026-10-28). The broad blades take trial d of the
  photographed ramp, `EVOSIM_THEATRE_ALGA_RAMP_PHOTO` 1 with `EVOSIM_THEATRE_ALGA_RAMP_STOP_A` 0.0697,0.0755,0.0169 and
  `_STOP_B` 0.0280,0.0303,0.0068. In the film's water light the blades' pixels then read hue 107°, saturation 0.40 and
  lightness 0.29, where the first cut read 115°, 0.52 and 0.39. Trial b is the fallback if d reads too dull in motion. It
  has the same hue at lightness 0.36, with stops 0.2325,0.2516,0.0563 and 0.0935,0.1011,0.0226. The target came from
  underwater photographs of green algae in natural light, and the strobe-lit ones were left out because a flash restores
  the red that water removes. Their sources and licences are in `logbook/specs/theatre-underwater-refs.tsv`. The blades'
  flutter is 0.4 of its first cut, with its noise at 1. It needs the arc at 2 with its room at 0.20, so the leaves are
  drawn past their colliders by that much.

## 4. MOTION (`EVOSIM_THEATRE_RIPPLE`)

A slow wave travelling along mantle margins and fins, as a cuttlefish's fin or a flatworm's skirt undulates.

- **How**: on today's parts, TissueMargin's ruffle already runs round the rim with a phase from the part's numbers.
  Under the dial that phase advances by ω × EvoTime(), so the ruffle travels round the margin, at a period of 2 to 4 s
  from the body's id. The same goes for LEAF=2's ruffle on blades, more slowly. EvoTime is the film's clock, so a
  replay renders the same frames.
- **Margin**: unchanged. The ruffle is already bounded inside the thickness the margin gave up, and moving its phase
  moves nothing outward.
- **On the skin**: a displacement along the skin's normal, weighted by how thin the skin is there (the margins), and
  inward only. Ported in step 4.
- **Check**: 24 fps takes, against the same take with the dial off, for flicker (box_change.py) and for the travel
  read on a sheet of consecutive frames.

## 5. GENOME TO SKIN (`EVOSIM_THEATRE_SKIN_MAP`), design only

The owner's direction of 2026-10-25, relayed by the coordinator: keep drawing skins from the natural references, and
work toward one continuous map from genome to skin. A large genome difference should look large, and a small one should
look small. Ordered after the ONE SKIN spike. This section is the design; nothing is built.

### What it replaces, and why

Today a body's look comes from three places that are not continuous.
- **The plan hash** (`TheatrePalette`'s skin genes, the ruling of 2026-09-16) seeds the carve's offsets, the end tapers,
  the lean, `Character` and `ShapeBox`. A hash keeps kin together, but any step that changes the plan string changes
  everything at once, and two plans one step apart look as different as two strangers.
- **The pattern key** rounds investment to 0.05 and bins the margin in 24 log steps. Kin share it (about 92% of
  round 62's parent and child pairs), but a step across a bin edge replaces the tile, and the seed bin comes from the
  key's hash.
- **The regime and scale** already come from a smooth map of investment and margin (`TheatrePattern.Regime`). That is
  the one part already built the way this section wants, and it is the model for the rest.

### (a) The skin vector

Ten numbers per genome, each in [0, 1], which the shader reads in place of today's hashed choices. Section 6 adds
five more (11 to 15), and the edge option below a sixteenth:

| # | Parameter | Drives | Today from |
|---|---|---|---|
| 1 | base hue | the body tint's hue, on a hue wheel limited to the board's marine range | the guild colour plus the hash |
| 2 | base saturation | the tint's saturation | fixed per guild |
| 3 | pattern regime | position along spots, net, stripes (continuous, see (d)) | investment (smooth) |
| 4 | pattern scale | the tile's length | margin (smooth) |
| 5 | rim hue | the RIM_BAND colour, offset from the base hue | the guild |
| 6 | rim width | the band's width | fixed |
| 7 | mottle strength | MOTTLE_SOFT's gain | fixed |
| 8 | iridescence | the sheen's strength | fixed, on or off |
| 9 | countershade | dark above to pale below | none |
| 10 | carve character | the carve's depth and taper, replacing `Character` | the plan hash |

The guild colours stay where a guild carries meaning in the film: absorbers dark, photosynthetic green. The vector
moves a body around its guild's colour, not across guilds. CELL VIEW stays the diagnostic that ignores all of this.

### (b) The inputs: genome features only

Every parameter is a smooth function of a feature vector computed from the genome and the adult phenotype:
- **Cell-type shares**: the share of the body's volume in each of the eight types (eight numbers, summing to one).
- **Body-plan measures**: part count (log), the longest chain's length (log), the share of mirrored parts, the aspect
  ratio of the body's bounding box, the median part aspect, and the share of capsule, sphere and box parts.
- **Scalar genes that kin keep**: reproductive investment and reserve margin, which round 62's children carry
  unchanged in about 92% of pairs, as found for the pattern key. Others are added only after the same parent-and-child
  check.

No hash and no founder seed enters a parameter. The noise phase (where the mottle's cells and the pattern tile's
offset sit) may still come from a hash, because it moves the pattern without changing its character. The phase uses
the plan hash, so kin share it.

The functions are smooth and bounded, mostly a logistic of a weighted sum of the projected features (c). Each
parameter has two or three inputs chosen for a reason the film can tell. Examples:
- countershade rises with the swimming share (jointed and link parts against anchoring);
- the regime follows investment, as it does today;
- the hue follows the absorptive and consumer shares, so a hunter's lineage drifts warm.

The full table of weights is the build's first deliverable, with each input's reason beside it.

### (c) The projection, fixed once

The features are normalised by a projection computed once from a reference population: every living genome of round
62's h6202 window at 24,000 s. Each feature is standardised by that population's mean and spread, then the whole
vector is reduced by a principal-component rotation, kept as fixed numbers in the theatre's code with the window
named beside them. The same numbers serve every world and every later round, so a genome draws the same skin wherever
it lives. A world far from the reference saturates at the ends of the logistic and does not wrap. Redoing the
projection is a ruled change, like a look change, and is recorded with its date.

### (d) The pattern regime without flips

The Gray-Scott solver gives spots, nets and stripes along its (F, k) path, and the switch between them is sharp: a
small step in F can turn spots into stripes. Two measures keep the map continuous:
- **Blend across the edges.** Solve tiles at fixed points along the path (today's nine regime bins). A body between
  two points samples both tiles and blends them by its position, with the blend weight a smoothstep over the gap. A
  small step then changes the weight a little and never swaps the tile.
- **Scale and phase continuous.** The scale is the tile's length, already continuous. The phase is the hash above,
  shared by kin.

The tile cache stays bucketed: at most 9 regime points × 4 seed bins, solved once a run, as today. The blend costs one
more tile sample in the fragment, under the dial only.

The path's points sit inside the regimes Pearson 1993 labels in its (F, k) map (theatre-research-equations.md item 3, read in
full: Figures 2 and 3, twelve pattern types; only the labelled regimes are to be trusted). The blend then only ever
crosses the unlabelled ground between two labelled regimes, and never asks the solver for a pattern there. The first
build step checks today's nine points against those figures and moves any that fall outside a labelled regime.

### Where the research applies

From `logbook/specs/theatre-research-equations.md`, verified for the coordinator on 2026-10-25. "Read in full" is
that file's own mark; the rest are abstract or summary only, and are used for the idea, not for numbers.
- **The vector as a morphospace**: Raup 1966 (item 11, abstract and summaries). Four parameters span most coiled
  shells, and real taxa fill only parts of the space. That is the precedent for (a): a few numbers, each with a
  visible meaning, and a population that fills part of the space.
- **The regime map**: Pearson 1993 (item 3, read in full), as above.
- **Sharper colour edges**: Alessio & Gupta 2023 (item X1, read). A diffusiophoretic advection term added to
  reaction-diffusion sharpens the edges of colour patches without changing their spacing; a Péclet number of about 10
  to 50 best matches the boxfish and moray they compare. Borrowed as an option in the tile solver, so the cached tiles
  have crisp edges where a soft blur reads as rendering. The paper's model is the Brusselator, not Gray-Scott, so
  carrying the term over to our solver is inference, to be checked on a tile sheet before it reaches the vector. If
  it holds, the edge's sharpness becomes parameter 16, from the same features as the regime.
- **The chromatophores' placement and control**: Reiter et al. 2018 (item 14, mostly read).
  - Placement: new cells go where a surround-inhibition field allows, with the kernel J(x) = 1/(1 + e^(k(|x| − s0)))
    and s0 shrinking with age. That is a point process that gives an even but irregular field. It would replace
    today's jittered grid (`ChromaSpacing`), whose regularity shows up close. The cells would be placed once per
    genome on the skin and cached.
  - Control: about nine dimensions explain 85% of the pattern's variance, because the cells act in motor groups. That
    supports driving the cloud from a few state variables (MOTION's pulse), not per cell.
  - Status: both wait on the chromatophore dial passing its b43 retry; if it is retired, these go with it.
- **Patterns on the one skin**: Turk 1991 (item 8, read in full). Reaction-diffusion runs on the surface mesh itself,
  over points relaxed evenly across the surface, with Voronoi neighbours. The pattern then follows the body with no UV
  map. For the skin this is the fix for the markings' slide the spike shows: solve once per genome on the skin's
  rest-pose mesh, store the result per vertex, and the shader reads it as the skin bends. The regime and scale come
  from the vector as for tiles. Witkin & Kass 1991 (item 9, summary only) add anisotropic diffusion, so stripes can
  follow a direction field such as the body's long axis.

### (e) The check

Two checks, both before any render reaches the owner.
- **Correlation.** On one window's genomes (h6202 at 24,000 s), take every pair within a clade and a random sample
  across clades. Correlate the skin-vector distance (Euclidean, over every parameter of the vector, each in [0, 1]) with Core's
  `SpeciesDistance.Between` at the run's weights. It passes on the two bars ruled below, read off a
  scatter plot with the corner of small species distance and large skin distance counted. This runs in Python on the run's genomes, with no
  Unity.
- **The sheet.** Pairs from one window, chosen by `SpeciesDistance`: parent and child, cousins (a shared
  great-grandparent), and strangers from different clades, each pair side by side in the same light. Read for
  "kin look alike, strangers do not", against the same pairs under today's hash.

### (f) Cost: compute once, sample per frame

The vector is computed once per genome when the body is built (the same place `TheatrePattern.Register` runs) and
cached by genome. Pattern tiles stay cached by bucket. Per frame, only the shader's samples run, plus the time-driven
pulse and ripple (MOTION). A growth step changes no parameter, because none reads absolute size.

### Order of the build, when it is opened

1. The feature extractor and the projection, in Python, on h6202's window, with the correlation check (e). No Unity.
2. The weights table, for the coordinator and the owner to read.
3. The C# port behind `EVOSIM_THEATRE_SKIN_MAP`, off by default and pixel-identical when off, then the pairs sheet.

### Ruled (the coordinator, 2026-10-25)

- **The carve character moves into the vector** (parameter 10). The look is to be driven by the genome, and the plan
  hash is the jump this section removes. Only the noise phase keeps a hash.
- **The check passes on two bars.**
  - Spearman ρ between skin distance and species distance above 0.5.
  - The share of pairs with a small species distance and a large skin distance stays under 5%. Small means the pair's
    species distance is below the window's 25th percentile of all sampled pairs; large means its skin distance is
    above the 75th percentile. Both percentiles are computed on the same sampled pairs.

## 6. SELF-SIMILAR DETAIL (`EVOSIM_THEATRE_FRACTAL`), design only

The owner added on 2026-10-25, relayed by the coordinator, that a skin vector is not enough: the marine look is
self-similar, with detail inside detail. This section is the design. It comes after the ONE SKIN spike, beside
GENOME TO SKIN, and reads its parameters from section 5's vector. Every part is a level of one dial, off by default
and identical to the pixel when off.

The board for each part is listed with it. Names starting `ss-` are new Wikimedia Commons photos in
`scratch/theatre-realism/refs-self-similar/`, with their licences in its `sources.csv` and a contact sheet,
`contact.png`. They join `scratch/owner/realism-refs/` only after the coordinator's check.

### (a) Patterns within patterns (level 1)

The same generator at two or three scales. The coarse layer sets where markings are; a finer copy of the generator
lives inside each coarse marking, with its own colour step, and a third copy, if the depth allows, inside that.
- **Board**: `soft-skin-08-chromodoris` (bands inside the mantle's rim), `absorbers-colonial-02-botryllus-brest` and
  `absorbers-colonial-01-botrylloides-magnicoecum` (zooid stars inside the colony's systems),
  `tunicates-sponges-06-callyspongia` (pores inside the pitted wall); `ss-02-clathrina-picton` (a net of tubes whose
  walls carry a finer net), `ss-04-chromodoris-willani` (nested bands at the mantle's edge: white rim, pale margin,
  violet, a black line), `ss-05-botryllus-a` (dark zooid rings around shared outlets, on a bright tunic: the inverse
  of the Brest colony's pale stars on dark).
- **Method**: the Gray-Scott tiles are already a library at several scales (section 5 (d)). Level n samples the tile at
  scale s × r^n, where r is the scale ratio, and is masked by level n−1's marking (its v above a threshold), so the
  fine pattern sits only inside the coarse one. For the rims, it uses the distance to level n−1's edge instead of its
  inside: a band, then a band inside the band. For colonies, it uses the Voronoi cells MOTTLE_SOFT already has, with
  the zooid star (VELVET=2) at the finer level inside each cell.
- **Cost**: one tile sample per level in the fragment, cached tiles, no new solve. A depth of three costs two samples
  more than today.
- **Research** (`theatre-research-equations.md`):
  - **The masked levels are a cheap form of Turk 1991's cascades** (item 8, read in full). There, a first
    reaction-diffusion system lays a pattern and a second one, run over it, refines it into rosettes, nets and
    multi-width stripes. The closer version, if masking reads as stamped, solves the finer tile with the coarser one as
    its initial state or source term. That is still one solve per bucket, cached.
  - **The nested bands at a margin follow Fowler, Meinhardt & Prusinkiewicz 1992** (item 7, read in full). A shell's
    pigment comes from reaction-diffusion run along the one-dimensional growing edge, one row per time step, so bands
    lie parallel to the edge, and a modulated source gives wavy lines. For a mantle's rims within rims, the same 1D
    model runs across the margin, with the distance to the edge as its time. Each step inward is one row, so the
    bands come out nested and parallel to the rim, with their count and widths from the model's parameters. The paper's
    worked parameter sets (Amoria, Volutoconus, Oliva, Conus) are the starting points. That a mantle's bands form this
    way is inference: no verified paper models nudibranch colour with equations (the file's own finding). It is
    borrowed for the look, not as biology.

### (b) Ruffled margins (level 2)

Excess growth at an edge buckles it into ripples whose frequency rises toward the edge.
- **Board**: `algae-fronds-03-ulva-lactuca` and `algae-fronds-04-ulva-lactuca`, `soft-skin-04-pseudobiceros`,
  `soft-skin-01-hexabranchus-sanguineus`; the lettuce sea slug, `ss-01-elysia-crispata-b`
  (translucent frills, finer ripples toward the rim) and `ss-01-elysia-crispata-a`.
- **Method**: on a leaf, and on the skin's thin margins (where the skin is thin against k), a displacement along the
  normal of sum over n of a_n sin(2π f_n t + φ_n). Here t runs along the edge and f_n = f_0 × 2^n. Each octave's
  weight rises toward the edge: octave n appears only within d_n = d_0 × 2^−n of it. That is the shape growth gives,
  a long gentle wave across the margin and finer waves at its very rim.
- **The margin bound**: the displacement is folded to the inward side (|sin|), so the ruffle buckles into the body's
  thickness rather than out of the collider, as LeafCarve's sign does. The ruffle therefore reads mostly in the
  silhouette's light and shade, not as a bigger outline. An outward ruffle is allowed as a higher setting, capped
  at 0.25 of the part's smallest half extent (ruled below).
- **Cost**: vertex stage only, a handful of sines; leaves already have the edge distance (`leafOS`). On the skin it
  needs the distance to the margin per vertex, computed once at meshing.
- **Research**: Sharon et al. 2002 (`theatre-research-equations.md` item 12a, summary only) found up to six generations of
  self-similar edge waves in free sheets, each at a smaller wavelength, from a metric that grows toward the edge. That
  is the rule the octave sum above imitates: a wavelength that shrinks by a fixed ratio per generation, each
  generation nearer the edge. The ratio of 2 and the depth limits are the method's own choices; the summary gives no
  numbers. The fractal depth from (e) sets how many generations show, up to three. The 2004 review (12b) was not read.

### (c) Branching surface features (level 3)

Papillae with sub-papillae, and tufts like a nudibranch's cerata, grown by a small L-system.
- **Board**: `soft-skin-05-flabellina` and `soft-skin-06-phyllodesmium` (cerata), `coral-polyps-05-goniopora` (polyps
  with tentacles), the CORAL_POLYP_TUFTS dial already built; `ss-03-thelenota-ananas` (star-shaped
  papillae tipped with smaller knobs) and `ss-03-dendronotus-iris` (cerata branching into side shoots).
- **Method**: one instanced mesh per feature, grown once per genome: an axiom, two rules (a branch gives a stem and
  k children at angle θ, scaled by r), and a depth. Its placement uses MOTTLE_SOFT's cells on the parts the dial
  allows, at most N per body. Drawn with `Graphics.RenderMeshInstanced` and posed with the part they sit on, as the
  polyp tufts are. Inside the margin: each feature's whole height is bounded by the part's smallest half extent ×
  a share, and it sits on the carved surface.
- **Cost**: the features are meshed once per genome and drawn instanced. A crowd caps the count by screen size (d).
- **Research**: the bracketed L-systems of Prusinkiewicz & Lindenmayer 1990 (`theatre-research-equations.md` item 10, not
  read). The book is cited for the formalism alone; the rules here are the method's own. The placement could follow
  Reiter 2018's inhibition kernel (item 14, mostly read; section 5), so features keep an even but irregular spacing
  instead of MOTTLE_SOFT's cells.

### (d) Detail that holds at every distance

Every level fades in and out with the size of its wavelength on screen, so a far body does not shimmer and a near
one does not go flat.
- **Method**: for each octave or level, its wavelength in pixels is λ_px = λ_world × (pixels per metre at that depth,
  from the projection and the view distance). Its weight is smoothstep(2, 4, λ_px) × (1 − smoothstep(λ_max/2, λ_max,
  λ_px)): an octave finer than 2 to 4 pixels fades out, and one coarser than the part fades out too, so the next finer
  level carries the look up close. The ruffle's octaves (b) and the L-system's depth (c) take the same rule, the
  latter as an LOD swap between meshes of depth 1 to 3.
- **Check**: 24 fps takes of a body moving toward and away from the camera, for flicker (box_change.py), and a sheet of
  one body at four distances.

### (e) From the genome

The depth and the scale ratio of the self-similarity come from the skin vector (section 5), which grows two
parameters for it:

| # | Parameter | Drives | From the genome |
|---|---|---|---|
| 11 | fractal depth | levels in (a), the ruffle's octaves in (b), the L-system's depth in (c), 1 to 3 | the genome's own recursion |
| 12 | scale ratio | r between levels, 0.3 to 0.6 | the body's ratio of child to parent part size along its chains |

The genome's own recursion, smoothed so it never steps:
- the share of the body's parts made by repeating a node, through `MorphNode.RecursiveLimit` above 1 or a
  self-edge;
- the mean of those nodes' recursive limits;
- the longest chain's length, on a log scale.
The feature is a weighted sum of the three after section 5's projection. The integer depth is a blend between the
two neighbouring depths, as (a) does by fading the deepest level's weight, so a small step never adds a whole level at
once. A body plan that repeats itself therefore wears a skin that repeats itself; a single-node blob wears one level.
The scale ratio from the body's own child-to-parent size ratio means a creature whose segments shrink by half each
step has a skin whose markings shrink by half each level.

### (f) One field, four channels: colour, relief, gloss, translucency

The owner pointed out on 2026-10-25, relayed by the coordinator, that a real skin pattern is texture and depth as
well as colour. Today PATTERN drives colour alone, and relief comes from separate fields: the carve, the pores and the
zooid stars. Under this part, every pattern field of this section drives all four channels: each RD level of (a), the
nested bands, and the colony stars. A mark is then one thing seen four ways, not a colour painted over an unrelated
bump.

- **Height.** The field's value becomes a height on the surface.
  - Normal perturbation: the height's gradient tilts the shading normal, the same displaced-surface normal
    `CarvedNormal` builds today. This is Turk 1991's use of reaction-diffusion as a bump map (`theatre-research-equations.md`
    item 8, read in full).
  - A small true displacement, so the silhouette and the shadows carry the relief. It sits within the stated margin:
    inward by default, as the carve's sign. A higher setting allows an outward push capped at 0.25 of the part's
    smallest half extent, the same margin the coordinator ruled for ruffles, stated in the dial's doc.
  - With the sign positive, spots stand as raised papillae, stripes as ridges and a net's lines as walls. With it
    negative, spots sink as pits, stripes as grooves and nets as channels.
  - Only the levels whose wavelength is several times the vertex spacing displace. Finer levels are normal-only, since
    a mesh cannot carry relief finer than its vertices. The skin is meshed once at a known spacing, so it takes more
    of its relief as true displacement than the parts do.
- **Gloss.** The field sets the surface's smoothness and its sheen. Either raised marks are matte and the grooves
  between them wet, as on Thelenota's papillae, or the reverse, glossy ridges on a matte ground, as on a Chromodoris
  mantle.
- **Translucency.** The field adjusts the thickness the light-through-tissue term reads (`fogAndThickness.y`, the SSS
  dial's path). Thin areas, the grooves and the pores, glow when the light is behind them. Raised marks stay dense.
- **From the genome.** The sign and strength of each coupling are new skin-vector parameters (section 5), from the same
  features, so lineages differ in relief and surface as well as in colour:

| # | Parameter | Range (stored mapped to [0, 1]) | Meaning |
|---|---|---|---|
| 13 | relief sign and strength | −1 to 1 | raised (+) or sunk (−), and how far, within the margin |
| 14 | gloss coupling | −1 to 1 | marks matte and ground wet (+), or the reverse (−) |
| 15 | translucency coupling | 0 to 1 | how much the pattern thins the tissue at its low values |

- **Board.**
  - `ss-03-thelenota-ananas`: raised star papillae, matte, on a finely dotted ground; relief positive, gloss positive.
  - `soft-skin-08-chromodoris` and `ss-04-chromodoris-willani`: smooth glossy ridges and bands on the mantle; low
    relief, gloss negative.
  - `ss-05-botryllus-a` and `absorbers-colonial-02-botryllus-brest`: sunk shared outlets inside star systems; relief
    negative at the stars' centres.
  - `ss-02-clathrina-picton`: a net of tubes over thin glowing walls; translucency high.
- **Cost.** The tiles gain channels, not samples. Each cached tile stores the value with its two gradients (and the
  distance to the edge, for the bands) in one RGBA texture, solved once per bucket as now, so the fragment's one sample
  per level serves colour, normal, gloss and thickness together. The displacement adds one vertex-stage texture sample
  per displacing level, only on bodies under the dial. Memory: four channels for one, a few MB for the whole library
  at today's 36 tiles. No per-frame solve.
- **Order.** The same as this section. The couplings come with (a) on parts and go onto the skin with ONE SKIN in
  full. They take the screen-size fade (d) from the start, since a bump finer than a pixel shimmers worse than a
  colour does.

### Order and agent time

| Step | Agent build | Machine | Depends on |
|---|---|---|---|
| (e) the two features, in section 5's Python check | about 1 h | none | section 5 step 1 |
| (a) patterns within patterns, on parts | 3 to 4 h | one batch, about 1 h | the tile library; MOTTLE_SOFT |
| (d) the screen-size fade, for (a) | about 2 h | a 24 fps batch, about 1 h | (a) |
| (f) relief, gloss and translucency from the same fields | 3 to 4 h | one batch, about 1 h | (a), (d); section 5's vector for the couplings |
| (b) ruffled margins, on leaves first | 3 to 4 h | about 1 h | (d) |
| (c) the L-system features | 1 day | about 2 h of sheets | (d); the owner's word on the spike, for where they sit |
| All of it on the skin | with ONE SKIN in full | | ONE SKIN in full |

(a) and (d) go first because they need no new geometry, and (d) protects every later level from shimmering. (b) and
(c) change the shape, so they follow the owner's word on the spike, which decides whether they sit on parts or on the
skin.

### Ruled (the coordinator, 2026-10-25)

- **The ruffle's margin (b).** Inward folding stays the default. A ruffle may push outward by at most 0.25 of the
  part's smallest half extent. The dial's doc states this as its margin, as level 2's joint balls state theirs.
- **The L-system's caps (c).** At most 64 features a body and 20,000 a frame. Past the cap, the screen-size fade (d)
  culls the smallest first. The frame cost at the cap is measured on the crowd scene.

## Open questions for the coordinator

- The split: consumer in the body (recommended above) or specialised, as proposed.
- Whether CELL VIEW's legend should burn into the frame by default, or go only to the captions file.
