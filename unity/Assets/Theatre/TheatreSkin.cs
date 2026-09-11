using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Evosim.Theatre
{
    /// <summary>
    /// The theatre's skin: dark field lighting, the water's fog, the sea bed, the sea's own
    /// surface and the shafts of light under it, the marine snow, and the two materials every
    /// body is drawn with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why dark field.</b> research/theatre-look/README.md's "Lighting, before any material":
    /// plankton photographers block the direct light so that the background is black and only what
    /// scatters at an edge reaches the lens [DF, WU, PC, AS]. The rendered equivalent is a near
    /// black water, low ambient, a raking key and a weak fill from the opposite side, and it costs
    /// nothing per body while doing more than any material. So the lighting is set up first and
    /// the shaders are written to suit it, not the other way round.
    /// </para>
    /// <para>
    /// <b>Everything here is generated.</b> No mesh, texture, material or shader is a committed
    /// binary. The bed and the surface are grids built in code, the sand, the wave and the
    /// caustics are arithmetic in the shader, the shafts are a handful of quads laid along the
    /// sun's refracted ray, the snow is a particle system configured in code with a procedural
    /// mote, and the meshes come from <see cref="TheatreMeshes"/>. That is the note's constraint
    /// (no purchased assets, nothing fetched at run time) and this repository's rule about what
    /// can be reviewed in a diff.
    /// </para>
    /// <para>
    /// <b>None of it changes a hash.</b> <c>simHash</c> is a digest of every <c>.cs</c> under
    /// <c>Assets/Evosim</c>; this file and its shaders live under <c>Assets/Theatre</c>, so a
    /// change to the look cannot refuse a recording or report a new build to the farm. That
    /// separation is the reason the theatre sits outside the simulation tree at all
    /// (<see cref="BuildIdentity"/>).
    /// </para>
    /// <para>
    /// <b>It applies to the Play mode view and to the snapshot camera alike.</b> The lights are
    /// scene objects and the fog is a render setting, so a picture is a picture of what the owner
    /// would see. <see cref="SnapshotCamera"/> rescales the fog to the span it is looking through
    /// for the length of one render, and says why there.
    /// </para>
    /// </remarks>
    public sealed class TheatreSkin
    {
        // ---------------------------------------------------------------- the water

        /// <summary>Near black blue: the camera's background, the fog's colour, the deep water.</summary>
        public Color Water = new Color(0.012f, 0.032f, 0.048f);

        /// <summary>
        /// Fog density for the Play mode view, as URP's exponential squared fog reads it.
        /// </summary>
        /// <remarks>
        /// At 0.035 the fog leaves about six tenths of a body's light at twenty metres and about a
        /// hundredth at sixty, which is the campaign box read the long way: the far wall of a 20 m
        /// box is dim but present, and the bottom of a 60 m column is the dark it should be.
        /// </remarks>
        public float FogDensity = 0.035f;

        /// <summary>Flat ambient. Low, because the dark field is the whole look.</summary>
        public Color Ambient = new Color(0.020f, 0.038f, 0.046f);

        // ---------------------------------------------------------------- the carve

        /// <summary>
        /// How deep the bodies are carved, as a fraction of a part's smallest half extent.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The second day's one dial. The first day rounded the boxes and mottled them and the
        /// owner's reading of it was that it was "still very very geometric": what makes a thing
        /// look grown is its silhouette, and rounding leaves every face flat and parallel. So the
        /// body shader cuts inward by two octaves of noise in each part's own object space, and
        /// this is how far.
        /// </para>
        /// <para>
        /// <b>It can only make a body smaller.</b> The displacement is never positive, so the
        /// setting cannot put a vertex outside a collider at any value; the shader's own
        /// <c>_CarveMaximum</c> is the separate question of not cutting a body through its middle.
        /// </para>
        /// <para>
        /// Set from <c>EVOSIM_THEATRE_CARVE</c> so that three pictures at three depths can be
        /// taken from one build and the owner can choose from pictures rather than from a number.
        /// The default is 0.35 rather than the second day's 0.2, because that is the one of the
        /// three the owner picked from the close views on 2026-09-11
        /// (logbook/specs/skin-spec-3.md).
        /// </para>
        /// </remarks>
        public float CarveFraction = Dial("EVOSIM_THEATRE_CARVE", 0.35f, 0f, 0.5f);

        // ---------------------------------------------------------------- the box that grew

        /// <summary>
        /// How far a box part narrows towards one end, as a fraction of its cross-section.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The third day's reading of the same complaint the second day answered for the surface.
        /// The owner looked at the close views and said the spheres were right and the boxes still
        /// looked manufactured: six flat faces at right angles, and no amount of surface relief
        /// argues with a silhouette that is a rectangle. A grown thing is almost never the same
        /// width at both ends, so the box narrows down its longest axis, smoothly rather than as a
        /// wedge, and reads as a seed or a grain (logbook/specs/skin-spec-3.md).
        /// </para>
        /// <para>
        /// <b>It can only make a body smaller.</b> The cross-section is multiplied by a number in
        /// [1 - this, 1], so every vertex moves towards the part's own long axis and none of them
        /// can reach the collider, let alone leave it.
        /// </para>
        /// </remarks>
        public float TaperFraction = Dial("EVOSIM_THEATRE_TAPER", 0.35f, 0f, 0.8f);

        /// <summary>
        /// How far a box part bends across itself, as a fraction of its smallest half extent.
        /// </summary>
        /// <remarks>
        /// <para>
        /// One low-frequency half wave down the longest axis, in a lateral direction drawn from
        /// the same per-body number the carve and the mottle are drawn from, so a body's parts are
        /// bent by its own id and not by the frame: it cannot flicker, and two creatures are bent
        /// differently (logbook/specs/skin-spec-3.md).
        /// </para>
        /// <para>
        /// <b>It can only make a body smaller.</b> A bend inside a box has to be paid for: the
        /// shader shrinks the cross-section by the amplitude before it displaces by it, and then
        /// clamps the object position into the mesh's own box anyway, so inside-ness is a
        /// property of the arithmetic and not of the setting (<c>TheatreBody.shader</c>,
        /// <c>ShapeBox</c>). Boxes only; a sphere or a capsule is not bent at all, because its
        /// collider is not the box the clamp would hold it inside.
        /// </para>
        /// </remarks>
        public float BendFraction = Dial("EVOSIM_THEATRE_BEND", 0.15f, 0f, 0.4f);

        // ---------------------------------------------------------------- the sea above

        /// <summary>
        /// How far the surface rises above its own level at the crest of a wave, in metres.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The fourth day's first dial. The owner ran round 36 seed 1 in the theatre and said
        /// they could not see the sun, the water shimmer or the underwater ripple. None of the
        /// three were there: the first three days were about bodies, and the world's ceiling was
        /// the same empty background as everything outside the box
        /// (logbook/specs/skin-spec-4.md). With the wavelength below, this is the whole shape of
        /// the sea the theatre is now lit by.
        /// </para>
        /// <para>
        /// A few centimetres, which with a wavelength of a metre or two is a calm day rather than
        /// surf. The steepness of the two together is what decides whether the ceiling reads as
        /// water or as crumpled foil, and it is the ratio rather than either number.
        /// </para>
        /// <para>
        /// <b>It is a visual and moves nothing.</b> No body is pushed by this wave, no current
        /// changes and no hash moves: the theatre is outside <c>simHash</c> by construction, and
        /// a sea drawn on the ceiling is a picture of the waterline the world already has.
        /// </para>
        /// </remarks>
        public float SurfaceWaveMetres = Dial("EVOSIM_THEATRE_WAVE", 0.045f, 0f, 0.5f);

        /// <summary>The longest wave train's wavelength, in metres.</summary>
        /// <remarks>
        /// Two shorter trains ride on it at about a half and a quarter of it
        /// (<c>TheatreWater.hlsl</c>, <c>EvoRipple</c>), so this is the scale of the largest
        /// thing on the surface and not the only one. The default is a metre and a half, which
        /// puts a dozen crests across the campaign box's five metre width: enough for the window
        /// to break up, few enough that the pattern is not below what a picture can hold.
        /// </remarks>
        public float SurfaceWaveLengthMetres = Dial("EVOSIM_THEATRE_WAVELENGTH", 1.6f, 0.2f, 20f);

        /// <summary>How much of their true phase speed the wave trains run at.</summary>
        /// <remarks>
        /// The trains travel by deep water dispersion, so the long one outruns the short ones and
        /// the sea never repeats on a beat. This halves the lot, and it is a dial rather than a
        /// constant for an honest reason: nobody has watched this sea yet. At the true speed a
        /// metre and a half of wavelength beats about once a second, which may well read as rain
        /// on a ceiling rather than as a calm day, and the owner can put it back to 1 from a
        /// picture without a rebuild.
        /// </remarks>
        public float SurfaceWaveSpeed = Dial("EVOSIM_THEATRE_WAVE_SPEED", 0.5f, 0f, 4f);

        /// <summary>How far below the surface its light still reaches, in metres.</summary>
        /// <remarks>
        /// <para>
        /// One number for the shafts, the caustics on the bodies, the caustics on the sand and
        /// the brightness of the window seen from far down, so the lit part of the world is one
        /// depth rather than four. The default is the habitable band: the world is 60 m deep and
        /// the top twenty is where the light and the bodies are, below which the water is the
        /// dark field it already was.
        /// </para>
        /// <para>
        /// It is a look and not the light model. <c>LightModel.IrradianceAt</c> is what the world
        /// actually charges and pays by; this decides where a picture stops drawing beams.
        /// </para>
        /// </remarks>
        public float SurfaceLightMetres = Dial("EVOSIM_THEATRE_LIGHT_REACH", 18f, 1f, 60f);

        /// <summary>How many shafts of light come down from the surface.</summary>
        /// <remarks>
        /// A handful of additive quads and not a volumetric pass: URP has no volumetric fog, so
        /// the choice is a custom renderer feature that marches the light or cards that stand in
        /// for beams, and the note's own furniture list takes the second
        /// (research/theatre-look, [CQ] [CY3]). Nine is enough for a box twenty metres long to
        /// have light falling through it and few enough that the census underneath is still the
        /// thing being looked at. Zero turns them off, which is how a picture is taken with and
        /// without them.
        /// </remarks>
        public int Shafts = Mathf.RoundToInt(Dial("EVOSIM_THEATRE_SHAFTS", 9f, 0f, 32f));

        /// <summary>Reads one of the skin's dials from the environment, or its default.</summary>
        /// <remarks>
        /// Parsed invariantly and clamped rather than trusted. A machine whose decimal separator
        /// is a comma would read 0.35 as 35 and carve every body away to nothing, and a refusal
        /// here would take the viewer down for a cosmetic setting, which WaterBounds' rule
        /// forbids. One method for all of them, because a second copy of this parse is a second
        /// place for the clamp to be forgotten; <see cref="TheatreMeshes"/> reads its pillow
        /// through it too.
        /// </remarks>
        internal static float Dial(string name, float fallback, float low, float high)
        {
            string text = System.Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            if (!float.TryParse(
                    text,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float value))
            {
                Debug.LogWarning(
                    "[Theatre] " + name + " is not a number ('" + text + "'); using " +
                    fallback.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");

                return fallback;
            }

            return Mathf.Clamp(value, low, high);
        }

        // ---------------------------------------------------------------- the furniture

        /// <summary>Motes of marine snow over the whole box, whatever the population.</summary>
        public int SnowMotes = 2600;

        /// <summary>Metres a mote falls per second. Slow: this is detritus, not rain.</summary>
        public float SnowFallMetresPerSecond = 0.09f;

        /// <summary>
        /// A mote's diameter in metres.
        /// </summary>
        /// <remarks>
        /// Small on purpose. A snapshot's marker for a body too small to draw is five pixels, and
        /// at the seventy-odd pixels a metre a top view of the campaign box gives, a six centimetre
        /// mote is nearly the same size: the furniture would then be competing with the census.
        /// Three centimetres reads as texture in the water and never as a body.
        /// </remarks>
        public float SnowSizeMetres = 0.03f;

        /// <summary>
        /// How long a mote lives, in seconds of wall clock.
        /// </summary>
        /// <remarks>
        /// Not the time it takes to fall the height of the box, which is what the first cut used.
        /// A sixty metre column at nine centimetres a second is an eleven minute lifetime, and a
        /// particle system fills at maxParticles over that lifetime: the water held a few hundred
        /// motes when the first pictures were taken and looked empty. Prewarm only simulates one
        /// duration, so the duration is set to this and the volume is seeded in full before the
        /// first frame. Nothing in a picture says how far a mote has fallen, so the shorter life
        /// costs nothing and the seeding is what is actually seen.
        /// </remarks>
        public float SnowSecondsOfLife = 24f;

        // ---------------------------------------------------------------- state

        private GameObject _root;
        private Light _key;
        private Light _fill;
        private GameObject _bed;
        private GameObject _surface;
        private GameObject _shafts;
        private ParticleSystem _snow;

        private Material _body;
        private Material _neck;
        private Material _bedMaterial;
        private Material _snowMaterial;
        private Material _surfaceMaterial;
        private Material _shaftMaterial;

        /// <summary>Unit, pointing at the sun from the water. See <see cref="ResolveSun"/>.</summary>
        private Vector3 _sun = new Vector3(0.28f, 0.92f, 0.27f);

        /// <summary>Unit, pointing down the sun's ray after the surface has bent it.</summary>
        private Vector3 _sunRay = new Vector3(0.20f, -0.96f, 0.19f);

        private bool _fogSaved;
        private bool _fogWas;
        private FogMode _fogModeWas;
        private Color _fogColourWas;
        private float _fogDensityWas;
        private AmbientMode _ambientModeWas;
        private Color _ambientWas;

        /// <summary>The one material every body renderer is painted with.</summary>
        public Material BodyMaterial => _body != null ? _body : (_body = MakeBodyMaterial());

        /// <summary>The joint neck's material. Its colour is set per neck by the palette.</summary>
        public Material NeckMaterial => _neck != null ? _neck : (_neck = MakeNeckMaterial());

        /// <summary>True when the shaders resolved and bodies can be repainted.</summary>
        public bool Ready => BodyMaterial != null;

        // ---------------------------------------------------------------- lighting

        /// <summary>
        /// Sets the water, the fog, the ambient and the two lights. Called once, from
        /// <c>TheatreRunner.Start</c>, before anything is opened.
        /// </summary>
        /// <param name="viewCamera">
        /// The camera the key is placed behind, or null for the scene's main camera. The note asks
        /// for a raking light from above and behind the viewer, which is a direction relative to
        /// where the viewer starts rather than a compass bearing.
        /// </param>
        public void Apply(Camera viewCamera)
        {
            SaveEnvironment();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Water;
            RenderSettings.fogDensity = FogDensity;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            RenderSettings.ambientIntensity = 1f;

            // No skybox. The water has no sky in it, and a default skybox would light every body
            // from every direction, which is the opposite of a dark field.
            RenderSettings.skybox = null;

            if (viewCamera == null) viewCamera = Camera.main;

            if (viewCamera != null)
            {
                viewCamera.clearFlags = CameraClearFlags.SolidColor;
                viewCamera.backgroundColor = Water;
            }

            EnsureRoot();

            Quaternion behind = viewCamera != null
                ? viewCamera.transform.rotation
                : Quaternion.identity;

            // The key: down and forward from where the viewer stands, so it rakes across a body
            // rather than facing it. A light from the camera's own axis flattens everything.
            _key = MakeLight(
                "Theatre Key",
                behind * Quaternion.Euler(38f, -26f, 0f),
                new Color(0.86f, 0.94f, 1.0f),
                2.1f,
                LightShadows.None);

            // The fill: weak, cold, from the other side, so the far side of a body is dark rather
            // than lost. The note's own wording.
            _fill = MakeLight(
                "Theatre Fill",
                behind * Quaternion.Euler(-14f, 168f, 0f),
                new Color(0.30f, 0.52f, 0.68f),
                0.55f,
                LightShadows.None);

            // The sea, told to every shader that draws any of it, now that there is a light for
            // the sun to be.
            PushWater();
        }

        // ---------------------------------------------------------------- the sea above

        /// <summary>Unit, pointing at the sun from the water.</summary>
        public Vector3 SunDirection => _sun;

        /// <summary>Unit, pointing down the sun's ray once the surface has bent it.</summary>
        public Vector3 SunRay => _sunRay;

        /// <summary>
        /// Hands the sea to every shader that draws a piece of it: the wave, the sun, and how far
        /// down the light reaches.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Global rather than per material, and that is the point.</b> Five materials draw
        /// some part of the surface's light (the ceiling, the shafts, a body, a neck, the sand),
        /// and if two of them ever held different numbers the net on a creature would stop being
        /// the light coming through the water above it, which is the whole of the fourth day.
        /// One push, one copy, no way to set four of five (<c>TheatreWater.hlsl</c>).
        /// </para>
        /// <para>
        /// Clamped here as well as in <see cref="Dial"/>, because the fields are public and a
        /// scene or a check can set them without going through it.
        /// </para>
        /// </remarks>
        public void PushWater()
        {
            _sun = ResolveSun();
            _sunRay = Refracted(_sun);

            Shader.SetGlobalVector("_EvoRipple", new Vector4(
                Mathf.Clamp(SurfaceWaveMetres, 0f, 0.5f),
                Mathf.Clamp(SurfaceWaveLengthMetres, 0.2f, 20f),
                Mathf.Clamp(SurfaceWaveSpeed, 0f, 4f),
                Mathf.Clamp(SurfaceLightMetres, 1f, 60f)));

            Shader.SetGlobalVector("_EvoSun", _sun);
            Shader.SetGlobalVector("_EvoSunRay", _sunRay);
        }

        /// <summary>
        /// Where the sun is, as a unit vector pointing at it from the water.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>It is the scene's directional light, so that the key and the sun agree.</b> A disc
        /// drawn in the ceiling at a bearing of its own would be a second sun, and a viewer would
        /// read the light on the bodies against it and find it wrong. The key is the light this
        /// class made and the one URP will pick as the main light, so it is asked first; the
        /// scene's own sun and then any enabled directional light are the fallbacks, for a scene
        /// that dressed itself without calling <see cref="Apply"/>.
        /// </para>
        /// <para>
        /// <b>Lifted to a low elevation if it points below the horizon.</b> The key is aimed
        /// relative to where the viewer's camera starts, so a scene whose camera begins looking
        /// upward would put the sun under the sea, at which Snell's window has nothing in it and
        /// the shafts point up. Eight degrees is the floor: a low sun is a long window and a nice
        /// picture, and a sun below the water is not a picture at all.
        /// </para>
        /// </remarks>
        private Vector3 ResolveSun()
        {
            Light light = _key;

            if (light == null || !light.isActiveAndEnabled || light.type != LightType.Directional)
            {
                light = RenderSettings.sun;
            }

            if (light == null || !light.isActiveAndEnabled || light.type != LightType.Directional)
            {
                light = null;

                foreach (Light other in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (other == null || !other.isActiveAndEnabled) continue;
                    if (other.type != LightType.Directional) continue;

                    light = other;
                    break;
                }
            }

            // A directional light's forward is the way its light travels, so the sun is behind it.
            Vector3 sun = light != null ? -light.transform.forward : new Vector3(0.28f, 0.92f, 0.27f);

            if (sun.sqrMagnitude < 1e-6f) sun = new Vector3(0.28f, 0.92f, 0.27f);
            sun = sun.normalized;

            const float lowest = 0.15f;

            if (sun.y < lowest)
            {
                Vector3 flat = new Vector3(sun.x, 0f, sun.z);
                if (flat.sqrMagnitude < 1e-6f) flat = Vector3.right;

                sun = (flat.normalized * Mathf.Sqrt(1f - lowest * lowest) + Vector3.up * lowest)
                    .normalized;
            }

            return sun;
        }

        /// <summary>
        /// The sun's ray once the surface has bent it, as a unit vector pointing down.
        /// </summary>
        /// <remarks>
        /// Snell's law at a flat interface, air into water, with water's index at 1.333: the ray
        /// is turned towards the vertical, so a sun forty five degrees up from the horizon sends
        /// its light down at thirty two degrees from the vertical rather than forty five. The shafts are built along this rather than along the sun itself, and the
        /// caustic net is read one of these back up the way the light came
        /// (<c>TheatreWater.hlsl</c>), so the beams and the pattern they throw agree about which
        /// way the light is going. Coming from air there is no total internal reflection, and the
        /// guard is there because an arithmetic slip should give a vertical beam rather than a
        /// non-finite one.
        /// </remarks>
        private static Vector3 Refracted(Vector3 sun)
        {
            const float eta = 1f / 1.333f;

            Vector3 travel = -sun;
            float cosIn = Mathf.Clamp01(sun.y);

            float k = 1f - eta * eta * (1f - cosIn * cosIn);
            if (k <= 0f) return travel;

            Vector3 ray = eta * travel + (eta * cosIn - Mathf.Sqrt(k)) * Vector3.up;

            return ray.sqrMagnitude < 1e-6f ? Vector3.down : ray.normalized;
        }

        private Light MakeLight(string name, Quaternion rotation, Color colour, float intensity,
                                LightShadows shadows)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root.transform, false);
            go.transform.rotation = rotation;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = colour;
            light.intensity = intensity;
            light.shadows = shadows;

            return light;
        }

        /// <summary>
        /// Turns off the scene's own directional light, if the scene builder left one.
        /// </summary>
        /// <remarks>
        /// The generated theatre scene carries a single directional light at intensity 1.1 aimed
        /// down and to one side. Leaving it on top of the key and the fill would be a third light
        /// nobody asked for, and a body lit from three directions has no dark side to read a rim
        /// against. Disabled rather than deleted, so rebuilding the scene is still the way to get
        /// it back.
        /// </remarks>
        public void SilenceSceneLights()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light == null || light == _key || light == _fill) continue;
                if (light.type != LightType.Directional) continue;

                light.enabled = false;
            }
        }

        // ---------------------------------------------------------------- the furniture

        /// <summary>
        /// Puts the bed and the snow in the water the run was simulated in.
        /// </summary>
        /// <param name="box">
        /// The world's box, the same one <see cref="SnapshotCamera"/> frames: x and z are the
        /// footprint, y runs from the floor to the waterline at zero.
        /// </param>
        public void Dress(Bounds box)
        {
            Undress();
            EnsureRoot();

            Vector3 min = box.min;
            Vector3 size = box.size;

            // Again here, because the sea's dials are public fields a caller can set between
            // constructing the skin and dressing a run, and because the shafts below are built
            // along the refracted ray this works out.
            PushWater();

            BuildBed(min, size);
            BuildSurface(min, size);
            BuildShafts(min, size);
            BuildSnow(min, size);
        }

        private void BuildBed(Vector3 min, Vector3 size)
        {
            Material material = _bedMaterial != null ? _bedMaterial : (_bedMaterial = MakeBedMaterial());
            if (material == null) return;

            // The bed reaches a little past the box on the horizontal axes, so the sand runs under
            // the box's own edge lines rather than stopping short of them and leaving a bright
            // line of background where the floor should be.
            float overhang = 0.02f * Mathf.Max(size.x, size.z);

            _bed = new GameObject("Theatre Bed") { hideFlags = HideFlags.DontSave };
            _bed.transform.SetParent(_root.transform, false);
            _bed.transform.position = new Vector3(
                min.x + 0.5f * size.x, min.y, min.z + 0.5f * size.z);

            _bed.AddComponent<MeshFilter>().sharedMesh = Bed(size.x + overhang, size.z + overhang);

            MeshRenderer renderer = _bed.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // How far below the surface the caustics still reach on the sand. In a shallow box
            // they land on the bed; in a sixty metre column they do not, which is correct: the
            // light is gone long before the floor is (DESIGN's light model).
            //
            // Off the skin's own dial since the fourth day, rather than a fraction of the box's
            // depth. The shafts, the bodies' caustics, the sand's and the window's brightness now
            // all fade on one number, and a second rule here would have made the sand the one
            // surface in the picture that disagreed about where the light stops.
            material.SetFloat("_CausticReach", Mathf.Clamp(SurfaceLightMetres, 1f, 60f));
        }

        /// <summary>
        /// Puts the ceiling on the water: one rippling quad at the waterline, seen from below.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why it reaches well past the box.</b> From three metres under, a camera looking up
        /// through a wide lens sees ten metres of surface in every direction, and the campaign's
        /// water is five metres across. A ceiling that stopped at the wall would put the empty
        /// background where the sky should be and would read as a hole rather than as the edge of
        /// a box. The margin is generous for that reason, and it costs nothing: the plane is
        /// hidden from every eye above the waterline anyway.
        /// </para>
        /// <para>
        /// <b>Hidden from above three times.</b> The mesh is wound so its front face points down,
        /// so a camera above it culls the whole quad; the shader discards again for any eye above
        /// the waterline (<c>TheatreWater.hlsl</c>, <c>EvoSeenFromBelow</c>); and the renderer is
        /// marked <see cref="TheatreInsideOnly"/>, which turns it off outright for the four
        /// snapshot views that photograph the census from outside the box. The fourth day's
        /// requirement that those views are unchanged is too much to hang on one culling flag,
        /// and two of the four cameras stand under the waterline, where the shader's own test
        /// cannot refuse them.
        /// </para>
        /// </remarks>
        private void BuildSurface(Vector3 min, Vector3 size)
        {
            Material material =
                _surfaceMaterial != null ? _surfaceMaterial : (_surfaceMaterial = MakeSurfaceMaterial());

            if (material == null) return;

            float margin = Mathf.Max(8f, 0.5f * Mathf.Max(size.x, size.z));
            float waterline = min.y + size.y;

            _surface = new GameObject("Theatre Surface") { hideFlags = HideFlags.DontSave };
            _surface.transform.SetParent(_root.transform, false);
            _surface.transform.position = new Vector3(
                min.x + 0.5f * size.x, waterline, min.z + 0.5f * size.z);

            _surface.AddComponent<MeshFilter>().sharedMesh = SurfaceMesh(
                size.x + 2f * margin, size.z + 2f * margin);

            MeshRenderer renderer = _surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Marked as something only the inside of the water sees, so that the four snapshot
            // views that photograph the census from outside the box can turn it off outright
            // (SnapshotCamera.HideWhatOnlyTheWaterSees).
            _surface.AddComponent<TheatreInsideOnly>();

            material.SetFloat("_Waterline", waterline);
            material.SetColor("_DeepColor", Water);
        }

        /// <summary>
        /// The surface's display mesh: a grid at the waterline, facing down.
        /// </summary>
        /// <remarks>
        /// Coarser than the bed's, because almost all of the shimmer is in the per pixel normal
        /// and the vertices only carry the few centimetres of wave that keep the ceiling from
        /// being a ruled line when it is seen edge on. The bounds are opened by a metre each way
        /// so that a displaced quad is not culled by a frustum test made against the flat plane.
        /// </remarks>
        private static Mesh SurfaceMesh(float length, float width)
        {
            Mesh mesh = Grid("Theatre Surface Grid", length, width, 0.5f, false);

            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, 2f, 0f));
            mesh.bounds = bounds;

            return mesh;
        }

        /// <summary>
        /// Hangs a handful of light shafts under the sun.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>They run along the refracted ray, not along the sun.</b> A beam under water is the
        /// sun's light after the surface has bent it towards the vertical, and drawing it at the
        /// sun's own angle would have the beams and the caustic net they throw disagreeing by ten
        /// or fifteen degrees, which is exactly the kind of quiet wrongness a picture is read for.
        /// </para>
        /// <para>
        /// <b>A shaft stops where the water does.</b> The length is the distance to the box's own
        /// wall or floor along the ray, capped at the light's reach: a beam is water lit from
        /// above, so one hanging outside the box would be light in water that does not exist,
        /// which is the same fault the marine snow was clipped for. A draw whose beam would be a
        /// stub is thrown away and drawn again somewhere else.
        /// </para>
        /// <para>
        /// <b>Placed from a fixed seed.</b> Two pictures of the same second must be the same
        /// picture; scattering these from the frame clock would make every render of one run a
        /// different world.
        /// </para>
        /// </remarks>
        private void BuildShafts(Vector3 min, Vector3 size)
        {
            if (Shafts <= 0) return;

            Material material =
                _shaftMaterial != null ? _shaftMaterial : (_shaftMaterial = MakeShaftMaterial());

            if (material == null) return;

            Vector3 origin = new Vector3(
                min.x + 0.5f * size.x, min.y + size.y, min.z + 0.5f * size.z);

            Mesh mesh = ShaftMesh(min, size, origin, Mathf.Clamp(Shafts, 0, 32));
            if (mesh == null) return;

            _shafts = new GameObject("Theatre Shafts") { hideFlags = HideFlags.DontSave };
            _shafts.transform.SetParent(_root.transform, false);
            _shafts.transform.position = origin;

            _shafts.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = _shafts.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _shafts.AddComponent<TheatreInsideOnly>();

            material.SetFloat("_Waterline", origin.y);
        }

        /// <summary>The shafts, as one mesh of quads about the surface's own centre.</summary>
        /// <remarks>
        /// Each quad carries, besides its corners, where it leaves the surface and how bright it
        /// is, in a second UV channel. The shader reads the wave field at that point rather than
        /// at the pixel, because a beam's brightness is decided once, where it enters the water,
        /// and is the same all the way down it.
        /// </remarks>
        private Mesh ShaftMesh(Vector3 min, Vector3 size, Vector3 origin, int count)
        {
            var random = new System.Random(20260911);

            Vector3 low = min;
            Vector3 high = min + size;
            Vector3 ray = _sunRay;

            float reach = Mathf.Clamp(SurfaceLightMetres, 1f, 60f);

            var vertices = new List<Vector3>(4 * count);
            var normals = new List<Vector3>(4 * count);
            var across0 = new List<Vector2>(4 * count);
            var tops = new List<Vector4>(4 * count);
            var triangles = new List<int>(6 * count);

            Vector3 level = Vector3.Cross(ray, Vector3.up);
            if (level.sqrMagnitude < 1e-4f) level = Vector3.right;
            level = level.normalized;

            int made = 0;

            for (int attempt = 0; attempt < 8 * count && made < count; attempt++)
            {
                var top = new Vector3(
                    Mathf.Lerp(low.x, high.x, (float)random.NextDouble()),
                    high.y,
                    Mathf.Lerp(low.z, high.z, (float)random.NextDouble()));

                float length = Mathf.Min(reach, ExitDistance(top, ray, low, high));

                // A stub is not a beam. Drawn again somewhere else rather than kept, so a box
                // whose sun comes in steeply still gets its full handful.
                if (length < 3f) continue;

                // Turned about its own ray by a draw, so the slabs do not all face one way and
                // some of them read from wherever the camera happens to be.
                Vector3 across = Quaternion.AngleAxis(360f * (float)random.NextDouble(), ray) * level;
                Vector3 normal = Vector3.Cross(across, ray).normalized;

                float halfTop = 0.18f + 0.45f * (float)random.NextDouble();
                float halfEnd = halfTop * (1.5f + 0.9f * (float)random.NextDouble());
                float brightness = 0.45f + 0.55f * (float)random.NextDouble();

                Vector3 end = top + ray * length;
                int first = vertices.Count;

                vertices.Add(top - across * halfTop - origin);
                vertices.Add(top + across * halfTop - origin);
                vertices.Add(end - across * halfEnd - origin);
                vertices.Add(end + across * halfEnd - origin);

                for (int corner = 0; corner < 4; corner++)
                {
                    normals.Add(normal);
                    tops.Add(new Vector4(top.x, top.z, brightness, 0f));
                }

                across0.Add(new Vector2(0f, 0f));
                across0.Add(new Vector2(1f, 0f));
                across0.Add(new Vector2(0f, 1f));
                across0.Add(new Vector2(1f, 1f));

                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first + 2); triangles.Add(first + 1); triangles.Add(first + 3);

                made++;
            }

            if (made == 0) return null;

            var mesh = new Mesh { name = "Theatre Shafts", hideFlags = HideFlags.DontSave };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, across0);
            mesh.SetUVs(1, tops);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>How far a ray travels from a point inside a box before it leaves it.</summary>
        private static float ExitDistance(Vector3 from, Vector3 direction, Vector3 low, Vector3 high)
        {
            float distance = Slab(from.x, direction.x, low.x, high.x);
            distance = Mathf.Min(distance, Slab(from.y, direction.y, low.y, high.y));
            distance = Mathf.Min(distance, Slab(from.z, direction.z, low.z, high.z));

            return distance;
        }

        /// <summary>Where a ray leaves one pair of walls. Infinite when it runs between them.</summary>
        private static float Slab(float from, float direction, float low, float high)
        {
            if (Mathf.Abs(direction) < 1e-5f) return float.MaxValue;

            float one = (low - from) / direction;
            float two = (high - from) / direction;

            return Mathf.Max(one, two);
        }

        /// <summary>
        /// The bed's display mesh: a grid in the xz plane, facing up, centred on its own origin.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A grid and no longer a quad.</b> The bed shader now cuts the surface downward by a
        /// low frequency field, and a displacement can only move vertices it has: four of them
        /// describe a plane whatever the field says. The spacing is set from the box rather than
        /// fixed, so a run with a different footprint gets the same size of ripple.
        /// </para>
        /// <para>
        /// <b>Nothing here is a size claim.</b> The vertices sit exactly on the collider's top
        /// plane and the shader only ever subtracts from that, so the drawn bed is at or below
        /// the sea floor and a body resting on it can appear to hover a little and never to sink.
        /// </para>
        /// </remarks>
        private static Mesh Bed(float length, float width)
        {
            // About a fifth of a metre between vertices, which is a twenty fifth of the shader's
            // five metre lobe.
            Mesh mesh = Grid("Theatre Bed Grid", length, width, 0.2f, true);

            // The shader cuts the surface down and the bounds have to know, or the bed is culled
            // from a low camera the moment its flat plane leaves the frustum.
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, 8f, 0f));
            mesh.bounds = bounds;

            return mesh;
        }

        /// <summary>
        /// A flat grid in the xz plane, centred on its own origin, facing up or down.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two surfaces are built from this: the sea bed, which faces up and is cut downward by
        /// its shader, and the sea's own surface, which faces down and is seen from underneath.
        /// One builder for both, because the only differences are the spacing and which way the
        /// triangles are wound, and a second copy of a grid is a second place for a winding to be
        /// got wrong.
        /// </para>
        /// <para>
        /// <b>The winding is what hides the ceiling from above.</b> Unity takes a triangle's
        /// front face from the order of its vertices, so the downward grid's triangles are wound
        /// the other way round and its normals point down: a camera above it sees only back faces
        /// and <c>Cull Back</c> throws them away. That is the fourth day's requirement that the
        /// snapshot's top view still sees the world, made out of the mesh rather than argued for.
        /// </para>
        /// </remarks>
        private static Mesh Grid(string name, float length, float width, float metres, bool up)
        {
            // Capped so that a very large box cannot ask for a million vertices.
            const int most = 220;

            int nx = Mathf.Clamp(Mathf.RoundToInt(length / Mathf.Max(0.01f, metres)), 1, most);
            int nz = Mathf.Clamp(Mathf.RoundToInt(width / Mathf.Max(0.01f, metres)), 1, most);

            float x = 0.5f * length;
            float z = 0.5f * width;

            var vertices = new List<Vector3>((nx + 1) * (nz + 1));
            var normals = new List<Vector3>((nx + 1) * (nz + 1));
            var triangles = new List<int>(nx * nz * 6);

            for (int j = 0; j <= nz; j++)
            {
                float pz = Mathf.Lerp(-z, z, (float)j / nz);

                for (int i = 0; i <= nx; i++)
                {
                    vertices.Add(new Vector3(Mathf.Lerp(-x, x, (float)i / nx), 0f, pz));
                    normals.Add(up ? Vector3.up : Vector3.down);
                }
            }

            int stride = nx + 1;

            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int a = j * stride + i;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;

                    if (up)
                    {
                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                        triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    }
                    else
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
                }
            }

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };

            // A grid this size passes 65,535 vertices at the cap, and the default index format
            // would wrap silently rather than refuse.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        private void BuildSnow(Vector3 min, Vector3 size)
        {
            Material material =
                _snowMaterial != null ? _snowMaterial : (_snowMaterial = MakeSnowMaterial());

            if (material == null) return;

            // The water the shader will let a mote be drawn in. Belt and braces with the shape
            // module: see the note in TheatreSnow.shader on the leak the top view found.
            material.SetVector("_BoxMin", min);
            material.SetVector("_BoxMax", min + size);

            var go = new GameObject("Theatre Snow") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(
                min.x + 0.5f * size.x, min.y + 0.5f * size.y, min.z + 0.5f * size.z);

            _snow = go.AddComponent<ParticleSystem>();

            // A particle system added at runtime is already playing when AddComponent returns,
            // and the duration cannot be set on a playing system: Unity logs an error and keeps
            // the default. The snow therefore never took its lifetime as its duration until
            // this stop was added (seen in the owner's console on 2026-09-11). Stopped and
            // cleared here, configured, then played once below.
            _snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float lifetime = Mathf.Max(1f, SnowSecondsOfLife);

            ParticleSystem.MainModule main = _snow.main;
            main.duration = lifetime;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = SnowSizeMetres;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 1f));
            main.maxParticles = Mathf.Max(1, SnowMotes);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            // Prewarmed, so the first picture of a run is of a seeded volume and not of an empty
            // one filling from the top. A picture taken at t = 1000 s of an empty water column
            // would be a picture of the particle system's age, not the world's.
            main.prewarm = true;

            ParticleSystem.EmissionModule emission = _snow.emission;
            emission.rateOverTime = Mathf.Max(1f, SnowMotes / Mathf.Max(0.001f, lifetime));

            ParticleSystem.ShapeModule shape = _snow.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;

            ParticleSystem.VelocityOverLifetimeModule velocity = _snow.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;

            // All three axes given the same kind of curve. Setting only y left x and z as plain
            // constants, and the module logs "Particle Velocity curves must all be in the same
            // mode" once a frame for the length of the run. The sideways numbers are the gentle
            // drift the note asks for, a tenth of the fall.
            float drift = 0.1f * SnowFallMetresPerSecond;

            velocity.x = new ParticleSystem.MinMaxCurve(-drift, drift);
            velocity.z = new ParticleSystem.MinMaxCurve(-drift, drift);
            velocity.y = new ParticleSystem.MinMaxCurve(
                -SnowFallMetresPerSecond * 1.4f, -SnowFallMetresPerSecond * 0.5f);

            ParticleSystem.NoiseModule noise = _snow.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.12f;
            noise.scrollSpeed = 0.05f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = -1;

            _snow.Play();
        }

        /// <summary>Takes the bed, the sea, the shafts and the snow away. Lights and fog stay.</summary>
        public void Undress()
        {
            Discard(_bed);
            _bed = null;

            Discard(_surface);
            _surface = null;

            Discard(_shafts);
            _shafts = null;

            if (_snow != null) Discard(_snow.gameObject);
            _snow = null;
        }

        /// <summary>
        /// Puts the render settings back and drops everything this made. Called when the theatre
        /// component goes away, so an Editor session is not left with the theatre's fog on.
        /// </summary>
        public void Dispose()
        {
            Undress();

            Discard(_root);
            _root = null;
            _key = null;
            _fill = null;

            Discard(_body); _body = null;
            Discard(_neck); _neck = null;
            Discard(_bedMaterial); _bedMaterial = null;
            Discard(_snowMaterial); _snowMaterial = null;
            Discard(_surfaceMaterial); _surfaceMaterial = null;
            Discard(_shaftMaterial); _shaftMaterial = null;

            TheatreMeshes.Release();

            RestoreEnvironment();
        }

        // ---------------------------------------------------------------- materials

        private Material MakeBodyMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Body");

            // Falls back rather than failing. A missing shader must not take the viewer down with
            // it (WaterBounds' rule), and URP's own Lit reads the same _BaseColor the palette
            // sets, so the fallback is the plain look this replaces.
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            var material = new Material(shader)
            {
                name = "Theatre Body",
                hideFlags = HideFlags.HideAndDontSave,
            };

            // The three dials that change what a body's outline is, and none of them can make a
            // body larger. The carve's displacement is negative by construction, the taper only
            // ever multiplies a cross-section by a number at most one, and the bend buys its
            // amplitude out of the cross-section before it spends it and is clamped into the
            // mesh's own box afterwards (TheatreBody.shader, Vertex and ShapeBox). So the meshes'
            // inset is still the whole of the size bound and these only decide how far inside it
            // the tissue sits. Clamped again here: the fields are public and a scene or a check
            // can set them without going through Dial.
            material.SetFloat("_CarveFraction", Mathf.Clamp(CarveFraction, 0f, 0.5f));
            material.SetFloat("_TaperFraction", Mathf.Clamp(TaperFraction, 0f, 0.8f));
            material.SetFloat("_BendFraction", Mathf.Clamp(BendFraction, 0f, 0.4f));

            // The one depth the surface's light reaches, the same number the sand, the shafts and
            // the window fade on. The net itself now comes from the sea overhead rather than from
            // a pattern of its own (TheatreWater.hlsl, EvoCausticNet), so this is all that is left
            // to say about it per material.
            material.SetFloat("_CausticReach", Mathf.Clamp(SurfaceLightMetres, 1f, 60f));

            return material;
        }

        private Material MakeNeckMaterial()
        {
            Material material = MakeBodyMaterial();
            if (material == null) return null;

            material.name = "Theatre Neck";

            // A neck is a marker, not tissue: no mottle, no caustics, a hard bright rim, and no
            // carve at all. It is already sized to fit inside the part it sits in, and a marker
            // with impressions cut into it would read as tissue.
            material.SetFloat("_MottleStrength", 0f);
            material.SetFloat("_CausticStrength", 0f);
            material.SetFloat("_CarveFraction", 0f);

            // A neck is drawn with the cylinder, which the shader never tapers or bends anyway;
            // set to zero so that the marker stays a marker if it is ever drawn with something
            // else.
            material.SetFloat("_TaperFraction", 0f);
            material.SetFloat("_BendFraction", 0f);
            material.SetFloat("_RimStrength", 2.6f);
            material.SetFloat("_RimPower", 1.6f);
            material.SetFloat("_GlowStrength", 0.5f);
            material.SetFloat("_TransScale", 0f);

            return material;
        }

        private Material MakeBedMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Bed");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "Theatre Bed",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        /// <summary>The ceiling's material. Falls back to nothing rather than to a lit plane.</summary>
        /// <remarks>
        /// There is no fallback shader here, unlike the body's. URP's Lit on a downward facing
        /// quad at the waterline would be a flat grey lid over the world, which is worse than no
        /// ceiling at all: a viewer would read it as the surface and take its flatness for the
        /// water's. A missing shader leaves the world as it was on the third day.
        /// </remarks>
        private Material MakeSurfaceMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Surface");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "Theatre Surface",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        /// <summary>The shafts' material. Additive, and no fallback for the ceiling's reason.</summary>
        private Material MakeShaftMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Shafts");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "Theatre Shafts",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private Material MakeSnowMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Snow");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "Theatre Snow",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        // ---------------------------------------------------------------- plumbing

        private void EnsureRoot()
        {
            if (_root != null) return;

            _root = new GameObject("Theatre Skin") { hideFlags = HideFlags.DontSave };
        }

        private void SaveEnvironment()
        {
            if (_fogSaved) return;

            _fogWas = RenderSettings.fog;
            _fogModeWas = RenderSettings.fogMode;
            _fogColourWas = RenderSettings.fogColor;
            _fogDensityWas = RenderSettings.fogDensity;
            _ambientModeWas = RenderSettings.ambientMode;
            _ambientWas = RenderSettings.ambientLight;
            _fogSaved = true;
        }

        private void RestoreEnvironment()
        {
            if (!_fogSaved) return;

            RenderSettings.fog = _fogWas;
            RenderSettings.fogMode = _fogModeWas;
            RenderSettings.fogColor = _fogColourWas;
            RenderSettings.fogDensity = _fogDensityWas;
            RenderSettings.ambientMode = _ambientModeWas;
            RenderSettings.ambientLight = _ambientWas;
            _fogSaved = false;
        }

        private static void Discard(Object thing)
        {
            if (thing == null) return;

            if (Application.isPlaying) Object.Destroy(thing);
            else Object.DestroyImmediate(thing);
        }
    }
}
