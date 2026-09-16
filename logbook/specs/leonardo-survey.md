# Leonardo.ai for the theatre's assets: a survey

**2026-09-16**  ·  asked by the owner ("have a look at the latest leonardo.ai documentation and find out if you could use it for asset or skin creation for the theatre; capture the research and ideas"). A Sonnet subagent's web survey, read-only: no account, no upload, no API call, nothing installed. Several of Leonardo's own pages refused automated fetching (403 and 404), so the pricing and terms figures come from secondary summaries and are marked so; the feature claims come from Leonardo's own reference pages where they loaded. The agent's reading follows the survey.

## The reading (the agent's)

**Where it fits: the video, not the skin.** Leonardo is a good tool for the presentation
layer that sits outside the world: backdrops and matte paintings for the safari's arrival
shot, title cards, thumbnails and concept art for the YouTube videos. None of that touches
a rule. It is a poor tool for the skin itself, for three reasons that are the theatre's
own and not Leonardo's.

1. **The skin is arithmetic on purpose.** `research/theatre-look/README.md` and
   `TheatreSkin.cs` hold to no committed binary: every mesh, texture, material and shader
   is built at start from code, the guild colour rides the Fresnel rim and nowhere else,
   the carve is noise in object space, the caustics follow the sun the water model has. A
   generated texture is a fixed file that has to be re-made when a dial moves, and it puts
   colour where the design forbids it unless someone is careful every time.
2. **The record needs reproducibility Leonardo does not promise.** Its `seed` parameter
   reproduces an image today; nothing in its docs promises the same image after a model
   is retired, and its model line has turned over twice in a year. A per-clade skin from
   a genome hash, the idea that makes people's eyes light up, would not replay in a year.
   The queued **inherited skin genes** (HANDOFF item 11: a few neutral numbers in the
   genome that the procedural shader reads, so relatives resemble each other) does the
   same job deterministically forever, with nothing leaving the machine. That is the
   route for relatives looking alike.
3. **Any upload is data leaving the machine.** A theatre picture, a body's shape, or a
   number derived from a genome sent to Leonardo needs the owner's approval for that
   instance, under the standing rule. For backdrops and title cards nothing of the
   project's has to go up; a prompt is enough.

**For sand, a CC0 library beats a generator.** If the bed ever wants a texture rather
than its two greys and its procedural ripple, Poly Haven or ambientCG give a tileable
albedo, normal and roughness set under CC0, with nothing uploaded and nothing to
reproduce, and Blender can bake a procedural one offline with the same guarantees.

**Recommendation.** Keep Leonardo in mind for the videos (uses e and f below) when the
first story is chosen, on a paid private tier so the outputs are the owner's; ask before
any project image goes up. Do not use it for bodies, the bed or the water. Build the
inherited skin genes when the theatre work resumes, since that is what "skins" means
for this world.

## How the key will be handled (owner, 2026-09-16 afternoon)

The script is `scripts/leonardo-generate.py` and the prompt sheet `design/leonardo-prompts.md`.
The owner's chosen pattern for the key is the one the synthetic-signals project on this
machine already runs: a **versioned `.env`** holding only 1Password references
(`EVOSIM_LEONARDO_KEY=op://<vault>/<item>/credential`), resolved for one run at a time by
`op run --env-file=.env -- python scripts/leonardo-generate.py ...`. The secret never sits on
disk in the project, and the file documents which secrets exist. It needs three repo changes
when it is set up: the commit hook's rule on `*.env` becomes "tracked and every value a
reference or non-secret" with a check; the script refuses a value starting with `op://` and
says to run under `op run`; the `.env` is un-ignored. One cost: `op run` may ask the app for
approval, so the agent cannot start a generation unattended, which for Leonardo is no cost.

**Until the owner has time for that, Leonardo is used by hand**: the agent writes the prompt
into the sheet, the owner pastes it into Leonardo and drops the picture under
`scratch/leonardo/<slug>/`. The script's `.env` reader and the ignore rule from the afternoon
stay as the interim, unused.

## The survey (Sonnet, read 2026-09-16)

### 1. What Leonardo.ai offers today

| Capability | What the docs say | Source |
|---|---|---|
| Image models | Phoenix (Leonardo's own), Lucid Origin and Lucid Realism, Kino XL, Krea 2 Turbo, plus third-party models (FLUX; Veo 3 and Kling for video) | [leonardo.ai/phoenix](https://www.leonardo.ai/phoenix), [docs index](https://docs.leonardo.ai/llms.txt) |
| Image-to-image and guidance | "Guidances" (image references, up to three for some models), Remix, a canvas editor | [creategeneration](https://docs.leonardo.ai/reference/creategeneration.md) |
| 3D and texture generation | The 3D feature is Rodin V2: one to five reference images in, a GLB mesh out, with a `material` parameter of PBR, Shaded or All. The docs do not describe a texture-only mode that paints an existing mesh and do not name the map types | [rodin-v2](https://docs.leonardo.ai/docs/rodin-v2.md) |
| A "paint an uploaded OBJ" workflow | Described by third parties as taking an OBJ and a prompt and returning albedo, normal and roughness maps; not verifiable against Leonardo's own docs, which refused the fetch | [a 2026 roundup](https://www.lovart.ai/blog/5-best-ai-texture-material-generators-2026), secondary |
| Transparency | A `remove-bg` sync endpoint returns RGBA or an alpha channel, with a `semitransparency` flag for glass and smoke edges | [createsyncgeneration](https://docs.leonardo.ai/reference/createsyncgeneration.md) |
| Upscaling | Pro Upscaler Precise and Creative | [docs index](https://docs.leonardo.ai/llms.txt) |
| Seed control | A `seed` from 0 to about 4.29 billion, "reproducible results" | [creategeneration](https://docs.leonardo.ai/reference/creategeneration.md) |
| Seed stability across model versions | Not documented as guaranteed; a secondary source reports FLUX-family models drifting on a fixed seed, and models are retired and replaced | [a seeds explainer](https://aiphotographytraining.substack.com/p/ai-seeds), secondary |
| API | REST, asynchronous generations plus a narrow synchronous path (remove-bg only); quantity capped per model, often at one | the two reference pages above |
| Rate limits | A concurrency and queue page names a default of about ten concurrent jobs, raised on a paid API plan; the numeric table did not load | [concurrency guide](https://docs.leonardo.ai/docs/guide-to-concurrency-queue-and-rate-limit), [limits](https://docs.leonardo.ai/reference/limits) |
| Resolutions | An enumerated set per model (512×768, 1280×720, 1920×1080 among them); arbitrary sizes refused | [creategeneration](https://docs.leonardo.ai/reference/creategeneration.md) |
| Unity | An Editor plugin was launched on Unity's AI marketplace in 2023, generating PBR textures and sprites in the Editor against an uploaded FBX, OBJ or glTF; whether it is current in 2026 could not be confirmed from Leonardo's own site | [Leonardo's Medium post](https://medium.com/@Leonardo.AI/an-unveiling-a-new-era-leonardo-ai-debuts-on-unitys-new-ai-marketplace-in-the-unity-asset-store-9580c1ec3107), [CG Channel, 2023](https://www.cgchannel.com/2023/07/check-out-three-new-generative-ai-add-ons-for-unity/) |

### 2. Pricing and licensing (secondary sources; Leonardo's own pages refused the fetch)

| Item | Found | Source |
|---|---|---|
| Plans | Free; Essential about $12 a month; Premium about $30; Ultimate about $60; Teams from about $72 for three seats | [eesel summary](https://www.eesel.ai/blog/leonardo-ai-pricing) |
| API | A $5 starter credit, pay as you go, roughly $0.008 to $0.02 and up per token by model and mode | same |
| Free tier | 150 fast tokens a day, no rollover | same |
| Ownership, paid and private | "Paid subscribers retain full ownership, copyright, and all other intellectual property rights to their images" when generating privately | [Leonardo help centre, Commercial Usage](https://intercom.help/leonardo-ai/en/articles/8044018-commercial-usage) |
| Ownership, free or public | Leonardo, and other users when public, hold broad reuse and remix rights over public generations | same; [terms.law analysis](https://terms.law/ai-output-rights/leonardo/), secondary |
| Training on inputs | A legal summary states private content from paying subscribers is not used for training without consent, and free-tier and public content can be | [terms.law](https://terms.law/ai-output-rights/leonardo/), secondary; the Terms of Service page returned 403 |

### 3. Uses for the theatre

| Use | Feasibility | Cost | Fit with the look's rules |
|---|---|---|---|
| (a) Sand and bed albedo, normal, roughness | Rodin's PBR output or the OBJ workflow could give a near-tileable set needing seam work | One generation plus cleanup | Against the look's "no committed binary" rule; a texture under `Assets/Theatre` moves no hash, so it is a design choice for the owner rather than a technical block |
| (b) A body mottle or detail texture under the shader's own terms | Possible as a blend with `TheatreBody.shader`'s Voronoi | A generation or two plus UV work per part shape | Risks colour meaning on the face where the design keeps it on the rim |
| (c) Marine snow, plankton, particle sprites | A sprite sheet with alpha through remove-bg | Low | The snow is a procedural mote now; a photographic sprite would fight the dark field |
| (d) Caustic and light-shaft textures | A tileable panning texture | Low | The caustics are arithmetic tied to the sun and the wave dials; a baked texture has to be re-made when a dial moves |
| (e) Backdrops and matte paintings for the arrival shot | Good fit: presentation, not simulation | Several generations and curation | No conflict |
| (f) Concept art, thumbnails, title cards | Good fit | Low | No conflict |
| (g) Per-clade skins from a genome hash as the seed | An idea the docs do not support: seed stability across model retirements is not promised; the queued inherited skin genes do the job deterministically | — | Not the route |
| (h) A Unity pipeline | The 2023 Editor plugin, or the API plus import of PNG or GLB; no runtime generation | — | Import-time tooling under `Assets/Theatre` only |

### 4. What it cannot or should not do here

- Replace the deterministic-forever requirement: a generated asset is a fixed file, fine for
  one-off art, unusable as a live function of genome state.
- Put guild or colour meaning where the design forbids it; a generated skin texture makes
  that mistake easily.
- Touch `Assets/Evosim`; any asset belongs under `Assets/Theatre`.
- Take a project picture, a body's shape or a genome-derived value as input without the
  owner's approval for that instance.

### 5. Alternatives, one line each

- **Poly Haven**: free CC0 PBR textures and HDRIs, no account, no upload; for sand strictly
  better than a generative call ([licence](https://polyhaven.com/license)).
- **ambientCG**: the same CC0 model, two thousand materials.
- **Substance (Adobe)**: parametric, editable, tileable by construction; commercial software,
  an install decision for the owner.
- **Blender procedural baking**: free, offline, deterministic (a node graph, not a prompt);
  the best of the baked-texture options if a texture is ever wanted.
- **Unity Shader Graph noise**: what the theatre already does; zero asset weight, zero
  external dependency, the only option already proven against every rule above.
