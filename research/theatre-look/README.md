# How to make a box look alive

**Technique reading for the theatre's skin: what artificial-life projects did about boxy bodies, what cheap shading can do for two thousand of them, and how plankton is lit so that it can be seen.**

## Why this is separate from `../LITERATURE-REVIEW.md`

That review is a systematic review of the evolved-virtual-creatures methodology literature,
with an update protocol and a question set. This is a reading of rendering technique, done
in one afternoon (2026-09-10) by four readers at moderate depth, at the owner's request:
"skin" in both senses, the biological tissue and the graphical layer laid over the collider
shapes, to break the harsh geometric look of the theatre. It is design input for the
theatre and nothing else. Nothing here decides a trajectory; the theatre lives beside the
simulation source so that its look can change without orphaning a recording.

The constraints the reading was done under: sizes stay true, so that what is seen is what
collides; hundreds to a couple of thousand bodies at once; URP in the Editor's Play mode
and in headless batch rendering to a texture; no purchased assets and nothing fetched at
run time; the look must serve reading the ecology (guild, size, joints) before prettiness.

## What the precedents did

Sims never dressed his creatures, and said so in the paper's future work: "flexible skin
could surround or be controlled by the rigid components", scales, hair, fur, eyes,
tentacles, and their inclusion in the genome [S94 §8]. The famous boxes are an
acknowledged gap, not a style. Framsticks keeps the stick skeleton visible and colours
function: red muscle, green assimilation, yellow ingestion, translucent sensors [FR].
Keiwan's Evolution renders jointed boxes plainly and adds cosmetic stickers, eyes above
all, that snap to bones and change nothing in the simulation [KE]. Species: ALRE skins a
mesh over a skeleton and textures it by projecting one seamless texture from three axes,
because parts that scale by gene stretch any UV map; its author found raw RGB gene colours
drift garish and moved to HSL [SP1, SP2]. The Bibites draws each body's sprite from its
genes, so size, speed and diet shape the silhouette [BB]. Slug Disco's Ecosystem evolves
the skin itself and ties it to camouflage [EC]. The lesson across them: keep function
legible, mute the colour, and let eyes do the work of reading as alive.

## Lighting, before any material

Plankton photographers light transparent animals by dark field: block the direct light,
so that the background is black and only what scatters at an edge or an internal boundary
reaches the lens [DF, WU]. Sardet's Plankton Chronicles shoots live animals that way [PC];
Semenov cross-lights from two sides in a black tank [AS]; underwater macro underexposes
the water by two stops and lights the subject alone [IK]; Blue Planet's deep sequences are
lit by the animals themselves against the real dark [BP]. The rendered equivalent is a
near-black water colour and low ambient, a light from behind or beside rather than in
front, a Fresnel term that brightens grazing edges, and the guild colour on that rim
rather than on the face. This costs nothing per body and does more than any material.

## The four facets

**Skin as material.** Cheap and instanceable at two thousand bodies: a Fresnel rim [U1];
thickness-faked subsurface, the single-pass approximation from Battlefield 3 [BB11] in
John Austin's URP port with no pre-pass [JA] or a Shader Graph subgraph with a thickness
input [CS]; matcap for a waxy sheen with no lighting at all [MC]; a thin-film ramp for
iridescence on chitin [AZ1]; a Voronoi cell pattern from the built-in node for mottling
[U2]. Ruled out: true subsurface (HDRP only), and shell-textured cilia over whole bodies,
which is viable on a few small parts at a reduced shell count and not on a swarm [HF]. One
engine caveat: per-body properties through a material property block break SRP batching
for that draw, so pick one batching strategy and keep it [U3].

**Skin as shape.** Bake one rounded mesh per primitive, clamped inward so that it never
leaves the collider, and rescale it on growth [CC1]; add a small vertex "puff" along
smoothed normals, clamped to a fraction of the smallest half-extent [CY]. Ruled out at this
scale: metaballs and marching cubes (per body, per frame, and they bulge past colliders by
design [MC1]), implicit skinning (built for a handful of rigged characters [VA]), live
subdivision (an offline technique; bake it once if at all [NI]).

**Motion in the surface.** Vertex-stage displacement is cheap and scales on the GPU; the
ripple is keyed to the body's id and to the water speed at its position, read from a small
flow texture updated from the current field [U4, CY, RO]. CPU mesh deformation is the
thing to avoid [JP].

**The water.** Depth fog from the depth texture [CC2]; a panning caustics texture projected
from world position onto the bed and the upper bodies [CY2, AM]; marine snow as one
particle system over the whole box [U5]; a triplanar sandy bed with a normal map [CC3,
BG]. God rays are the one expensive item: URP has no volumetric fog, so it is a custom
renderer feature [CQ] or additive quads that fake shafts [CY3].

## A first day's cut

All under `Assets/Theatre`, so no recording is orphaned: dark-field lighting and fog; one
body material with rim by guild, fake subsurface, muted HSL colour, Voronoi mottle and a
faint inner glow for reserve; baked rounded meshes true to the collider, a joint shown as
a short neck; marine snow and caustics on the bed. Then screenshots of a round in the new
skin. Ripple, iridescence and eyes are the second day if the first reads well.

## Sources

Fetched 2026-09-10; none is a peer-reviewed source and none enters the review's counts.

- [S94] Sims, K. Evolving Virtual Creatures. SIGGRAPH 1994. https://www.karlsims.com/papers/siggraph94.pdf
- [FR] Framsticks, Client3D and shaders. http://www.framsticks.com/wiki/Client3D
- [KE] Keiwan, Evolution 4.0: gallery and skins. https://keiwan.itch.io/evolution/devlog/1004070/evolution-40-gallery-and-skins
- [SP1] Species: ALRE, procedural texturing. https://speciesdevblog.wordpress.com/2013/06/03/procedural-texturing/
- [SP2] Species: ALRE, creature class and leg rendering. https://speciesdevblog.wordpress.com/2011/09/05/creature-class-and-leg-rendering/
- [BB] The Bibites, procedural sprites. https://the-bibites.fandom.com/wiki/Modding_Procedural_Sprites
- [EC] Slug Disco, Ecosystem. https://slugdisco.itch.io/ecosystem
- [DF] Dark-field microscopy. https://en.wikipedia.org/wiki/Dark-field_microscopy
- [WU] Westcott University, dark field imaging. https://westcottu.com/dark-field-imaging-tips-techniques
- [PC] Plankton Chronicles. https://planktonchronicles.org/en/
- [AS] Semenov, A., interview. https://www.divephotoguide.com/underwater-photography-special-features/article/interviews-pros-alexander-semenov/
- [IK] Ikelite, jellyfish technique. https://www.ikelite.com/blogs/cheat-sheets/jellyfish-underwater-photography-camera-settings-and-technique
- [BP] The Deep on Blue Planet II. https://ecoevocommunity.nature.com/posts/22389-the-deep-on-blue-planet-ii
- [U1] Shader Graph, Fresnel Effect node. https://docs.unity3d.com/Packages/com.unity.shadergraph@6.9/manual/Fresnel-Effect-Node.html
- [BB11] Barré-Brisebois and Bouchard, Approximating Translucency, GDC 2011. https://colinbarrebrisebois.com/2011/03/07/gdc-2011-approximating-translucency-for-a-fast-cheap-and-convincing-subsurface-scattering-look/
- [JA] Austin, J., Fast subsurface scattering for URP. https://johnaustin.io/articles/2020/fast-subsurface-scattering-for-the-unity-urp
- [CS] Simpson, C., Subsurface scattering for URP. https://github.com/CiaranSimpson/Subsurface-Scattering-for-Unity-URP
- [MC] Matcap in Shader Graph. https://discussions.unity.com/t/how-to-create-matcap-shaders-with-shader-graph/763958
- [AZ1] Zucconi, A., thin-film interference. https://www.alanzucconi.com/2017/10/27/carpaint-shader-thin-film-interference/
- [U2] Shader Graph, Voronoi node. https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Voronoi-Node.html
- [HF] hecomi, UnityFurURP. https://github.com/hecomi/UnityFurURP
- [U3] Per-instance properties and batching. https://docs.unity3d.com/6000.0/Documentation/Manual/gpu-instancing-per-instance-properties.html
- [CC1] Catlike Coding, rounded cube. https://catlikecoding.com/unity/tutorials/rounded-cube/
- [CY] Cyanilux, vertex displacement. https://www.cyanilux.com/tutorials/vertex-displacement/
- [MC1] Marching cubes with the job system. https://github.com/akoreman/Marching-Cubes-Unity-Job-System
- [VA] Vaillant et al., Implicit Skinning, SIGGRAPH 2013. https://dl.acm.org/doi/10.1145/2461912.2461960
- [NI] Niessner et al., feature-adaptive subdivision. https://www.niessnerlab.org.devweb.mwn.de/papers/2012/3feature/niessner2012feature.pdf
- [U4] Unity Learn, vertex displacement. https://learn.unity.com/tutorial/shader-graph-vertex-displacement
- [RO] Ronja, wobble displacement. https://www.ronja-tutorials.com/post/015-wobble-displacement/
- [JP] UnityJigglePhysics. https://github.com/Kovacs-3d/UnityJigglePhysics
- [CC2] Catlike Coding, looking through water. https://catlikecoding.com/unity/tutorials/flow/looking-through-water/
- [CY2] Cyanilux, water shader breakdown. https://www.cyanilux.com/tutorials/water-shader-breakdown/
- [AM] ameye.dev, real-time caustics. https://ameye.dev/notes/realtime-caustics/
- [U5] Unity Learn, weather with VFX Graph. https://learn.unity.com/tutorial/simulating-weather-with-the-vfx-graph-unity-2018-lts
- [CC3] Catlike Coding, triplanar mapping. https://catlikecoding.com/unity/tutorials/advanced-rendering/triplanar-mapping/
- [BG] Golus, B., normal mapping for a triplanar shader. https://bgolus.medium.com/normal-mapping-for-a-triplanar-shader-10bf39dca05a
- [CQ] Unity-URP-Volumetric-Light. https://github.com/CristianQiu/Unity-URP-Volumetric-Light
- [CY3] Cyanilux, god rays breakdown. https://www.cyanilux.com/tutorials/god-rays-shader-breakdown/
