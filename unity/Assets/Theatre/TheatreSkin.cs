using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Evosim.Theatre
{
    /// <summary>
    /// The theatre's skin: dark field lighting, the water's fog, the sea bed, the marine snow,
    /// and the two materials every body is drawn with.
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
    /// binary. The bed is a quad built in code, the sand and the caustics are arithmetic in the
    /// shader, the snow is a particle system configured in code with a procedural mote, and the
    /// meshes come from <see cref="TheatreMeshes"/>. That is the note's constraint (no purchased
    /// assets, nothing fetched at run time) and this repository's rule about what can be reviewed
    /// in a diff.
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
        private ParticleSystem _snow;

        private Material _body;
        private Material _neck;
        private Material _bedMaterial;
        private Material _snowMaterial;

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

            BuildBed(min, size);
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

            _bed.AddComponent<MeshFilter>().sharedMesh = Quad(size.x + overhang, size.z + overhang);

            MeshRenderer renderer = _bed.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // How far below the surface the caustics still reach on the sand. In a shallow box
            // they land on the bed; in a sixty metre column they do not, which is correct: the
            // light is gone long before the floor is (DESIGN's light model).
            material.SetFloat("_CausticReach", Mathf.Max(1f, 0.22f * size.y));
        }

        /// <summary>A flat quad in the xz plane, facing up, centred on its own origin.</summary>
        private static Mesh Quad(float length, float width)
        {
            float x = 0.5f * length;
            float z = 0.5f * width;

            var mesh = new Mesh { name = "Theatre Bed Quad", hideFlags = HideFlags.DontSave };

            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-x, 0f, -z), new Vector3(-x, 0f, z),
                new Vector3(x, 0f, z), new Vector3(x, 0f, -z),
            });

            mesh.SetNormals(new List<Vector3>
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            });

            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
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

        /// <summary>Takes the bed and the snow away. The lights and the fog stay.</summary>
        public void Undress()
        {
            Discard(_bed);
            _bed = null;

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

            // The one clamp that keeps the pictures honest. The meshes were generated inset to
            // TheatreMeshes.Inset of the collider, so the shader may push a vertex out by at most
            // what is left over; taking the value from the same constant means the two cannot be
            // edited apart.
            material.SetFloat("_PuffFraction", TheatreMeshes.PuffFraction);

            return material;
        }

        private Material MakeNeckMaterial()
        {
            Material material = MakeBodyMaterial();
            if (material == null) return null;

            material.name = "Theatre Neck";

            // A neck is a marker, not tissue: no mottle, no caustics, a hard bright rim, and no
            // puff at all, because it is already sized to fit inside the part it sits in and a
            // puff would be a second bound to keep.
            material.SetFloat("_MottleStrength", 0f);
            material.SetFloat("_CausticStrength", 0f);
            material.SetFloat("_PuffFraction", 0f);
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
