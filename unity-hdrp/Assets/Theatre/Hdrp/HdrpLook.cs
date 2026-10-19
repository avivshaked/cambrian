using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Evosim.Theatre
{
    /// <summary>
    /// What HDRP draws in place of the URP theatre's hand-made water, for the spike (D202). The
    /// water's fade, its backdrop and the light shafts are the URP theatre's own arithmetic, run as
    /// custom passes after the opaque objects (<c>TheatreWaterPass.shader</c>), and its tone step
    /// after the post-processing (<see cref="HdrpToneStep"/>); the ceiling is a port of its surface
    /// shader; the ambient is a sky; and a still can be path traced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The skin still builds the world as it does in unity/: the bed, the earth, the reefs, the
    /// bodies and the two lights. This runs before every render and only adds to that: it turns the
    /// lights into HDRP's units, hides the URP-only furniture (the surface plane, the shafts, the
    /// glass and the snow, whose shaders are URP's), and keeps one volume and one water surface.
    /// </para>
    /// <para>
    /// <b>Units.</b> HDRP's exposure is fixed at EV 0 by <see cref="TheatreGrade"/>, a multiplier
    /// of one, and HDRP's Lambert divides by pi where the URP theatre's did not, so a light is set
    /// at pi times its URP strength, in lux for a directional light and candela for a lamp. The
    /// picture before the grade then has the URP picture's brightness, to the extent the two
    /// lighting models agree.
    /// </para>
    /// <para>
    /// Dials: <c>EVOSIM_HDRP_PATHTRACE</c> (samples a still accumulates; 0, the default, renders
    /// normally), <c>EVOSIM_HDRP_WATER</c> (1 draws HDRP's water surface, default 1),
    /// <c>EVOSIM_HDRP_VOLUMETRIC</c> (1 volumetric fog, default 1), <c>EVOSIM_HDRP_FOG_METRES</c>
    /// (the fog's mean free path; by default one over the skin's fog density, where the URP fog
    /// has lost two thirds of the light), <c>EVOSIM_HDRP_LIGHT_SCALE</c> (default pi).
    /// </para>
    /// </remarks>
    public static class HdrpLook
    {
        /// <summary>
        /// HDRP's own look, no longer matched to the URP theatre's (<c>EVOSIM_HDRP_NATIVE</c>, 1 on, default 0;
        /// the owner's second test of D202, 2026-10-19, to see how far HDRP goes when it is used its own
        /// way). It changes the defaults of the dials below: HDRP's volumetric fog in place of the water
        /// pass, its shafts from the sun's cookie scattered in that fog in place of the URP march, its
        /// water surface in place of the ported ceiling, the bodies in its subsurface scattering with
        /// transmission, the stone with its own specular, its reflections, the snow writing its depth so
        /// the physical camera's depth of field blurs every mote by its own distance, and every frame
        /// rendered <see cref="Settle"/> times under TAA so the temporal effects converge. Each dial
        /// still overrides its own default.
        /// </summary>
        public static bool Native = TheatreSkin.Dial("EVOSIM_HDRP_NATIVE", 0f, 0f, 1f) >= 0.5f;

        public static int PathTraceSamples = (int)TheatreSkin.Dial("EVOSIM_HDRP_PATHTRACE", 0f, 0f, 16384f);
        public static bool WaterSurfaceOn = TheatreSkin.Dial("EVOSIM_HDRP_WATER", Native ? 1f : 0f, 0f, 1f) >= 0.5f;
        public static bool Volumetric = TheatreSkin.Dial("EVOSIM_HDRP_VOLUMETRIC", 1f, 0f, 1f) >= 0.5f;
        public static float FogMetres = TheatreSkin.Dial("EVOSIM_HDRP_FOG_METRES", 0f, 0f, 10000f);
        public static float LightScale = TheatreSkin.Dial("EVOSIM_HDRP_LIGHT_SCALE", Mathf.PI, 0f, 100f);

        /// <summary>
        /// Lights every material in the forward pass rather than the deferred one
        /// (<c>EVOSIM_HDRP_FORWARD</c>, 1 forward, the default; 0 deferred).
        /// </summary>
        public static bool Forward = TheatreSkin.Dial("EVOSIM_HDRP_FORWARD", 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The water as the URP theatre fades into it, applied as a custom pass after the opaque
        /// objects (<c>TheatreWaterPass.shader</c>; <c>EVOSIM_HDRP_WATER_PASS</c>, 1 on, the default).
        /// HDRP's own fog is off while it is on.
        /// </summary>
        public static bool WaterPassOn = TheatreSkin.Dial("EVOSIM_HDRP_WATER_PASS", Native ? 0f : 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The sky's strength, which is the ambient light every surface takes from it
        /// (<c>EVOSIM_HDRP_SKY_AMBIENT</c>, default 1). The backdrop the camera sees is the water
        /// pass's, so this changes only the light.
        /// </summary>
        public static float SkyAmbient = TheatreSkin.Dial("EVOSIM_HDRP_SKY_AMBIENT", 1f, 0f, 4f);

        /// <summary>
        /// The shafts' fog with the water pass on: its mean free path in metres
        /// (<c>EVOSIM_HDRP_SHAFT_METRES</c>, default 0, off). Tried at 200 m on 2026-10-19: no shaft
        /// could be seen, and past the volumetric range the fog's constant colour darkened the far reef.
        /// </summary>
        public static float ShaftMetres = TheatreSkin.Dial("EVOSIM_HDRP_SHAFT_METRES", 0f, 0f, 100000f);

        /// <summary>
        /// The light shafts as the URP theatre marches them, two more passes after the water's
        /// (<c>TheatreWaterPass.shader</c>; <c>EVOSIM_HDRP_SHAFTS</c>, 1 on, the default). They are
        /// drawn only where the skin turns its own march on, and need the water pass.
        /// </summary>
        public static bool ShaftsOn = TheatreSkin.Dial("EVOSIM_HDRP_SHAFTS", Native ? 0f : 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The marine snow as the URP theatre's shells draw it, one more pass after the shafts
        /// (<c>EVOSIM_HDRP_SNOW</c>, 1 on, the default), where the skin turns its own shells on.
        /// </summary>
        public static bool SnowOn = TheatreSkin.Dial("EVOSIM_HDRP_SNOW", 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The URP theatre's tone step after HDRP's post-processing (<see cref="HdrpToneStep"/>;
        /// <c>EVOSIM_HDRP_TONE_STEP</c>, 1 on, the default). It runs only where the skin turns it on.
        /// </summary>
        public static bool ToneStepOn = TheatreSkin.Dial("EVOSIM_HDRP_TONE_STEP", Native ? 0f : 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// Whether HDRP's lights draw a specular highlight (<c>EVOSIM_HDRP_LIGHT_SPECULAR</c>, 1 on, the
        /// default). A test dial: the URP theatre's bed and rock have no specular term, and HDRP Lit's
        /// grows toward a grazing view.
        /// </summary>
        public static bool LightSpecular = TheatreSkin.Dial("EVOSIM_HDRP_LIGHT_SPECULAR", 1f, 0f, 1f) >= 0.5f;

        /// <summary>HDRP's environment reflections (<c>EVOSIM_HDRP_REFLECTIONS</c>, 0 off, the default).</summary>
        public static bool Reflections = TheatreSkin.Dial("EVOSIM_HDRP_REFLECTIONS", Native ? 1f : 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// How the bodies take light through their tissue (<c>EVOSIM_HDRP_BODY_LIGHT</c>): 0 the URP body's own
        /// term by hand, as the matched look draws it; 1, the default under the native look, HDRP's translucent
        /// type ("Evosim/Theatre Body Translucent"), its transmission through the profile
        /// <c>Resources/TheatreBodyDiffusion.asset</c>; 2 HDRP's subsurface scattering with transmission
        /// ("Evosim/Theatre Body SSS"), which drew every body black on 2026-10-19 for a reason not found.
        /// The guild's rim and the fed body's glow stay, as emission, in all three.
        /// </summary>
        public static int BodyLight = (int)TheatreSkin.Dial("EVOSIM_HDRP_BODY_LIGHT", Native ? 1f : 0f, 0f, 2f);

        private static bool BodySss => BodyLight > 0;

        /// <summary>
        /// The snow writes its nearest mote's depth (<c>EVOSIM_HDRP_SNOW_DEPTH</c>, 1 on, default 1 under the
        /// native look), so HDRP's depth of field blurs each mote by its own distance and not by the
        /// distance of the water behind it.
        /// </summary>
        public static bool SnowDepth = TheatreSkin.Dial("EVOSIM_HDRP_SNOW_DEPTH", Native ? 1f : 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// Renders a frame takes (<c>EVOSIM_HDRP_SETTLE</c>, 1 to 64, default 8 under the native look, else 1),
        /// the camera's history reset before the first: a film frame is one pose after a jump, and HDRP's
        /// TAA, its volumetric fog and its physically based depth of field each accumulate over frames.
        /// </summary>
        public static int Settle = (int)TheatreSkin.Dial("EVOSIM_HDRP_SETTLE", Native ? 8f : 1f, 1f, 64f);

        /// <summary>HDRP's temporal antialiasing (<c>EVOSIM_HDRP_TAA</c>, 1 on, default 1 under the native look).</summary>
        public static bool Taa = TheatreSkin.Dial("EVOSIM_HDRP_TAA", Native ? 1f : 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The water surface's own underwater rendering, inside a box over the world (<c>EVOSIM_HDRP_UNDERWATER</c>,
        /// 1 on, default 0): HDRP's absorption and scattering under the surface, in place of the fog.
        /// </summary>
        public static bool Underwater = TheatreSkin.Dial("EVOSIM_HDRP_UNDERWATER", 0f, 0f, 1f) >= 0.5f;

        /// <summary>The fog's colour from the sky toward each direction (<c>EVOSIM_HDRP_FOG_SKY</c>, default 1 under the native look).</summary>
        public static bool FogSky = TheatreSkin.Dial("EVOSIM_HDRP_FOG_SKY", Native ? 1f : 0f, 0f, 1f) >= 0.5f;

        /// <summary>The water surface's own moving caustics (<c>EVOSIM_HDRP_WATER_CAUSTICS</c>, their intensity, default 0, off).</summary>
        public static float WaterCaustics = TheatreSkin.Dial("EVOSIM_HDRP_WATER_CAUSTICS", 0f, 0f, 10f);

        /// <summary>
        /// The render on which the cost of each of HDRP's features is timed (<c>EVOSIM_HDRP_BENCH</c>, the
        /// render's count from 1; 0, the default, times nothing). See <see cref="RunBench"/>.
        /// </summary>
        public static int Bench = (int)TheatreSkin.Dial("EVOSIM_HDRP_BENCH", 0f, 0f, 1000000f);

        private static readonly FrameSettingsField[] NoReflections =
        {
            FrameSettingsField.SkyReflection, FrameSettingsField.SSR, FrameSettingsField.ReflectionProbe,
            FrameSettingsField.PlanarProbe,
        };

        private static CustomPassVolume _passVolume;
        private static Material _passMaterial;
        private static readonly List<CustomPass> ShaftPasses = new List<CustomPass>();
        private static readonly List<CustomPass> SnowPasses = new List<CustomPass>();
        private static Material _snowMaterial;
        private static CustomPassVolume _toneVolume;
        private const string UrpMarch = "Evosim/Theatre Shaft March";
        private const string UrpShells = "Evosim/Theatre Snow Shells";

        // The URP-only furniture, by shader name: drawn by HDRP itself, or not at all, in the spike.
        private static readonly HashSet<string> UrpOnly = new HashSet<string>
        {
            "Evosim/Theatre Shafts", "Evosim/Theatre Shaft March",
            "Evosim/Theatre Glass", "Evosim/Theatre Backdrop", "Evosim/Theatre Snow",
            "Evosim/Theatre Snow Shells",
        };

        private static GameObject _holder;
        private static VolumeProfile _profile;
        private static Fog _fog;
        private static GradientSky _sky;
        private static PathTracing _pathTracing;
        private static WaterSurface _water;
        private static readonly Dictionary<Light, float> LightSet = new Dictionary<Light, float>();
        private static readonly Dictionary<Light, float> LightUrp = new Dictionary<Light, float>();
        private static readonly Vector4[] LightDirs = new Vector4[8];
        private static readonly Vector4[] LightColours = new Vector4[8];

        // The skin's ceiling material and the HDRP one drawn in its place (TheatreCeilingHdrp.hlsl).
        private const string UrpCeiling = "Evosim/Theatre Surface";
        private static readonly Dictionary<Material, Material> Ceilings = new Dictionary<Material, Material>();
        private static bool _described;

        /// <summary>Renders the camera through HDRP's look: once, or a path-traced still.</summary>
        public static void Render(Camera camera, Bounds? world)
        {
            Prepare(camera, world);
            if (BodyDebug > 0f && camera != null) LogNearestBody(camera);

            _renders++;
            if (Bench > 0 && _renders == Bench) RunBench(camera);

            if (PathTraceSamples <= 0 || !SystemInfo.supportsRayTracing)
            {
                if (_pathTracing != null) _pathTracing.enable.Override(false);
                RenderSettled(camera, Settle);
                return;
            }

            // The path tracer adds one sample a frame and starts again when anything moves, so a
            // still is the same frame rendered as many times as it has samples.
            _pathTracing.enable.Override(true);
            _pathTracing.maximumSamples.Override(PathTraceSamples);
            for (int i = 0; i < PathTraceSamples; i++) camera.Render();
            _pathTracing.enable.Override(false);
        }

        private static int _renders;

        // One frame: the camera's history reset, as at a cut, then rendered as many times as the
        // temporal effects need; the clock does not move between them (EVOSIM_THEATRE_SAFARI_CLOCK).
        private static void RenderSettled(Camera camera, int times)
        {
            _frameWatch.Restart();
            if (ResetEachFrame && (times > 1 || Taa)) HDCamera.GetOrCreate(camera).Reset();
            for (int i = 0; i < Mathf.Max(1, times); i++) camera.Render();

            // Every frame's time, the GPU waited for: the cost of the look as it renders, and a growing
            // time is seen at once (an in-process bench that reset the camera between settings grew
            // from 45 ms to 1.9 s a render on 2026-10-19).
            if (camera.targetTexture != null) AsyncGPUReadback.Request(camera.targetTexture).WaitForCompletion();
            _frameWatch.Stop();
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[HdrpLook] frame {0}: {1} renders in {2:0} ms",
                _renders, Mathf.Max(1, times), _frameWatch.Elapsed.TotalMilliseconds));
        }

        /// <summary>
        /// Whether a frame resets the camera's history before its renders (<c>EVOSIM_HDRP_RESET</c>, 1 on, the default).
        /// A test dial: a frame's time grew through the take with it on (2026-10-19).
        /// </summary>
        public static bool ResetEachFrame = TheatreSkin.Dial("EVOSIM_HDRP_RESET", 1f, 0f, 1f) >= 0.5f;

        private static readonly System.Diagnostics.Stopwatch _frameWatch = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// The cost of each of HDRP's features on one frame: the frame rendered with everything the look
        /// turns on, then with each feature off in turn through the camera's frame settings (the snow by
        /// its passes, TAA by the camera's antialiasing), each a mean over thirty renders after four to warm
        /// up, the GPU waited for. Logged as "[HdrpLook] bench"; the frame's own render follows unchanged.
        /// </summary>
        private static void RunBench(Camera camera)
        {
            HDAdditionalCameraData data = camera.GetComponent<HDAdditionalCameraData>();
            if (data == null || camera.targetTexture == null) { Debug.LogWarning("[HdrpLook] bench: no camera data or target"); return; }

            // The depth of field costs far more than the rest together (the first bench, 2026-10-19), and
            // its time varies by more than the others cost, so each of the others is timed with it off
            // and set against the frame without it.
            var cases = new (string name, FrameSettingsField[] off, bool snowOff, bool taaOff)[]
            {
                ("everything", new FrameSettingsField[0], false, false),
                ("without depth of field", new[] { FrameSettingsField.DepthOfField }, false, false),
                ("without depth of field, volumetric fog", new[] { FrameSettingsField.DepthOfField, FrameSettingsField.Volumetrics }, false, false),
                ("without depth of field, the water surface", new[] { FrameSettingsField.DepthOfField, FrameSettingsField.Water }, false, false),
                ("without depth of field, subsurface scattering and transmission", new[] { FrameSettingsField.DepthOfField, FrameSettingsField.SubsurfaceScattering, FrameSettingsField.Transmission }, false, false),
                ("without depth of field, the snow's passes", new[] { FrameSettingsField.DepthOfField }, true, false),
                ("without depth of field, reflections", new[] { FrameSettingsField.DepthOfField, FrameSettingsField.SSR, FrameSettingsField.SkyReflection, FrameSettingsField.ReflectionProbe }, false, false),
                ("without depth of field, temporal antialiasing", new[] { FrameSettingsField.DepthOfField }, false, true),
                ("without any of these", new[] { FrameSettingsField.Volumetrics, FrameSettingsField.Water, FrameSettingsField.SubsurfaceScattering,
                    FrameSettingsField.Transmission, FrameSettingsField.DepthOfField, FrameSettingsField.SSR, FrameSettingsField.SkyReflection,
                    FrameSettingsField.ReflectionProbe }, true, true),
            };

            var was = new Dictionary<FrameSettingsField, bool>();
            foreach (var c in cases)
                foreach (FrameSettingsField f in c.off)
                    if (!was.ContainsKey(f)) was[f] = data.renderingPathCustomFrameSettings.IsEnabled(f);
            var snowWas = new List<bool>();
            foreach (CustomPass pass in SnowPasses) snowWas.Add(pass.enabled);
            HDAdditionalCameraData.AntialiasingMode aaWas = data.antialiasing;

            var watch = new System.Diagnostics.Stopwatch();

            // One case's time a render: its settings applied, four renders to warm up, thirty timed.
            double TimeCase((string name, FrameSettingsField[] off, bool snowOff, bool taaOff) c)
            {
                foreach (var pair in was)
                {
                    data.renderingPathCustomFrameSettings.SetEnabled(pair.Key, pair.Value && System.Array.IndexOf(c.off, pair.Key) < 0);
                    data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)pair.Key] = true;
                }
                for (int i = 0; i < SnowPasses.Count; i++) SnowPasses[i].enabled = snowWas[i] && !c.snowOff;
                data.antialiasing = c.taaOff ? HDAdditionalCameraData.AntialiasingMode.None : aaWas;

                HDCamera.GetOrCreate(camera).Reset();
                for (int i = 0; i < 4; i++) camera.Render();
                AsyncGPUReadback.Request(camera.targetTexture).WaitForCompletion();

                const int Runs = 30;
                watch.Restart();
                for (int i = 0; i < Runs; i++) camera.Render();
                AsyncGPUReadback.Request(camera.targetTexture).WaitForCompletion();
                watch.Stop();
                return watch.Elapsed.TotalMilliseconds / Runs;
            }

            // The first bench of 2026-10-19 drifted upward through its cases, so every case is set against
            // its own baseline, timed just before and just after it: the frame with everything for the
            // depth of field, the frame without the depth of field for the rest.
            foreach (var c in cases)
            {
                if (c.name == "everything") continue;
                var baseline = cases[c.name == "without any of these" || c.name.StartsWith("without depth of field,") ? 1 : 0];
                double before = TimeCase(baseline);
                double ms = TimeCase(c);
                double after = TimeCase(baseline);
                double against = 0.5 * (before + after);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[HdrpLook] bench {0}x{1}: {2}: {3:0.0} ms a render, {4:+0.0;-0.0} against {5} ({6:0.0} before, {7:0.0} after), a frame of {8} renders {9:0} ms",
                    camera.targetTexture.width, camera.targetTexture.height, c.name, ms, ms - against, baseline.name, before, after, Settle, ms * Settle));
            }
            {
                double all = TimeCase(cases[0]);
                double none = TimeCase(cases[1]);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[HdrpLook] bench {0}x{1}: everything {2:0.0} ms a render, without depth of field {3:0.0} ms: the depth of field {4:0.0} ms",
                    camera.targetTexture.width, camera.targetTexture.height, all, none, all - none));
            }

            foreach (var pair in was)
            {
                data.renderingPathCustomFrameSettings.SetEnabled(pair.Key, pair.Value);
                data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)pair.Key] = true;
            }
            for (int i = 0; i < SnowPasses.Count; i++) SnowPasses[i].enabled = snowWas[i];
            data.antialiasing = aaWas;
        }

        private static int _nearestLogged;

        // The test view's companion: the per body values of the body part nearest the camera, which in a
        // portrait is the subject's, for the first few renders.
        private static void LogNearestBody(Camera camera)
        {
            if (_nearestLogged >= 3) return;
            _nearestLogged++;
            Renderer best = null;
            float bestDistance = float.MaxValue;
            foreach (Renderer r in InScene<Renderer>())
            {
                Material m = r.sharedMaterial;
                if (m == null || m.shader == null || m.shader.name != "Evosim/Theatre Body" || !r.enabled) continue;
                float d = (r.bounds.center - camera.transform.position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = r; }
            }
            if (best == null) return;
            var block = new MaterialPropertyBlock();
            best.GetPropertyBlock(block);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[HdrpLook] nearest body '{0}' at {1:0.00} m: base {2}, rim {3}, through {4}, reserve {5:0.###}, trans gain {6:0.###}, fade {7:0.###}, leaf {8}, material rim {9}, colour space {10}",
                best.name, Mathf.Sqrt(bestDistance), block.GetColor("_BaseColor"), block.GetColor("_RimColor"),
                block.GetColor("_TransTint"), block.GetFloat("_Reserve"), block.GetFloat("_TransGain"), block.GetFloat("_Fade"),
                block.GetVector("_Leaf"), best.sharedMaterial.GetColor("_RimColor"), QualitySettings.activeColorSpace));
        }

        /// <summary>Puts HDRP's look into the scene the skin has built. Cheap to repeat.</summary>
        public static void Prepare(Camera camera, Bounds? world)
        {
            TheatreSkin skin = TheatreSkin.Current;
            EnsureVolume(skin);
            EnsureWaterPass();
            EnsureToneStep();
            HideUrpFurniture();
            Follow(UrpMarch, _passMaterial, ShaftPasses, ShaftsOn, true);
            Follow(UrpShells, _snowMaterial, SnowPasses, SnowOn, false);
            SwapCeiling();
            SwapBodies();
            ValidateMaterials();
            ConvertLights();
            if (WaterSurfaceOn && world.HasValue) EnsureWater(skin, world.Value);
            PushDepthLight(world);

            if (camera != null)
            {
                HDAdditionalCameraData data = camera.GetComponent<HDAdditionalCameraData>();
                if (data == null) data = camera.gameObject.AddComponent<HDAdditionalCameraData>();
                data.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
                data.customRenderingSettings = true;
                data.renderingPathCustomFrameSettings.litShaderMode = Forward ? LitShaderMode.Forward : LitShaderMode.Deferred;
                data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.LitShaderMode] = true;

                // No environment reflections by default: the URP theatre's surfaces reflect nothing but
                // the lights' own highlights, so a fair pair leaves HDRP's sky and probe reflections out.
                // EVOSIM_HDRP_REFLECTIONS=1 puts them back for a picture of HDRP's own.
                foreach (FrameSettingsField field in NoReflections)
                {
                    data.renderingPathCustomFrameSettings.SetEnabled(field, !Reflections);
                    data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
                }
                data.volumeLayerMask = ~0;
                data.antialiasing = Taa ? HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing : HDAdditionalCameraData.AntialiasingMode.None;
            }

            Shader.SetGlobalFloat("_EvoHdrpNativeBody", BodySss ? 1f : 0f);

            if (!_described)
            {
                _described = true;
                Debug.Log("[HdrpLook] " + Describe());

                // Every light as HDRP will use it: a light that reaches nothing reads the same in a
                // picture as one that is too weak.
                foreach (Light light in InScene<Light>())
                {
                    var data = light.GetComponent<HDAdditionalLightData>();
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[HdrpLook] light '{0}' {1} enabled {2} active {3}, {4:0.###} {5}, colour {6}, temperature {7} ({8} K), towards {9}, culling {10}, rendering layers {11}, diffuse {12}, specular {13}, dimmer {14}",
                        light.name, light.type, light.enabled, light.gameObject.activeInHierarchy, light.intensity,
                        light.lightUnit, light.color, light.useColorTemperature, light.colorTemperature,
                        -light.transform.forward, light.cullingMask, light.renderingLayerMask,
                        data != null && data.affectDiffuse, data != null && data.affectSpecular,
                        data != null ? data.lightDimmer : -1f));
                }
                foreach (Renderer r in InScene<Renderer>())
                {
                    Material m = r.sharedMaterial;
                    if (m == null || m.shader == null || m.shader.name != "Evosim/Theatre Body") continue;
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block);
                    Debug.Log("[HdrpLook] a body '" + r.name + "' rendering layers " + r.renderingLayerMask +
                              ", layer " + r.gameObject.layer + ", material keywords " + string.Join(" ", m.shaderKeywords) +
                              ", has block " + r.HasPropertyBlock() + ", block colour " + block.GetColor("_BaseColor") +
                              ", block rim " + block.GetColor("_RimColor") + ", material colour " + m.GetColor("_BaseColor") +
                              ", material has _BaseColor " + m.HasProperty("_BaseColor") +
                              ", stencil ref gbuffer " + (m.HasProperty("_StencilRefGBuffer") ? m.GetFloat("_StencilRefGBuffer").ToString(CultureInfo.InvariantCulture) : "none") +
                              ", stencil write gbuffer " + (m.HasProperty("_StencilWriteMaskGBuffer") ? m.GetFloat("_StencilWriteMaskGBuffer").ToString(CultureInfo.InvariantCulture) : "none") +
                              ", lit shader mode " + (Forward ? "forward" : "deferred"));
                    break;
                }

                // Every renderer wider than ten metres, with its shader: a material HDRP cannot draw
                // shows magenta, and in a picture it is not obvious which object it is.
                foreach (Renderer r in InScene<Renderer>())
                {
                    if (r.bounds.size.x < 10f && r.bounds.size.z < 10f) continue;
                    foreach (Material m in r.sharedMaterials)
                        Debug.Log("[HdrpLook] large renderer '" + r.name + "' enabled " + r.enabled + ", material '" +
                                  (m != null ? m.name : "none") + "', shader '" +
                                  (m != null && m.shader != null ? m.shader.name : "none") + "', bounds " + r.bounds.size);
                }
            }
        }

        public static string Describe() => string.Format(CultureInfo.InvariantCulture,
            "{5}fog {0} m mean free path{1}, water surface {2}, lights x{3:0.###}, path tracing {4}; water pass {6}, shafts {7}, " +
            "tone step {8}, reflections {9}, bodies {10}, stone specular {11:0.##}, snow depth {12}, {13} renders a frame, TAA {14}, " +
            "underwater {15}, water caustics {16:0.##}, sun's caustic net {17:0.#} m",
            _fog != null && _fog.enabled.value ? _fog.meanFreePath.value : 0f, Volumetric ? " volumetric" : "",
            _water != null ? "on" : "off", LightScale,
            PathTraceSamples > 0 ? PathTraceSamples + " samples" : "off", Native ? "NATIVE: " : "",
            WaterPassOn ? "on" : "off", ShaftsOn ? "on" : "off", ToneStepOn ? "on" : "off", Reflections ? "on" : "off",
            BodyLight == 2 ? "subsurface scattering" : BodyLight == 1 ? "translucent" : "standard", StoneSpecular, SnowDepth ? "on" : "off", Settle, Taa ? "on" : "off",
            Underwater ? "on" : "off", WaterCaustics, CausticMetres);

        private static void EnsureVolume(TheatreSkin skin)
        {
            if (_holder == null)
            {
                _holder = new GameObject("Theatre HDRP Look") { hideFlags = HideFlags.DontSave };
                var volume = _holder.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 5f;

                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.name = "Theatre HDRP look (built in code)";
                _profile.hideFlags = HideFlags.DontSave;

                var environment = _profile.Add<VisualEnvironment>(true);
                environment.skyType.Override((int)SkyType.Gradient);
                environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

                _sky = _profile.Add<GradientSky>(true);
                _fog = _profile.Add<Fog>(true);
                // HDRP's default volume profile turns on motion blur, ambient occlusion and contact
                // shadows, and an override left inactive hands the setting back to it. A film frame is
                // one render after a jump of half a second of camera motion, so the blur and the
                // occlusion's history smeared every moving body; the URP theatre has neither.
                var blur = _profile.Add<MotionBlur>(true);
                blur.intensity.Override(0f);
                var occlusion = _profile.Add<ScreenSpaceAmbientOcclusion>(true);
                occlusion.intensity.Override(0f);
                var contact = _profile.Add<ContactShadows>(true);
                contact.enable.Override(false);

                // The bodies' diffusion profile: HDRP draws a profile only while a volume lists it.
                if (BodySss)
                {
                    var profile = Resources.Load<DiffusionProfileSettings>("TheatreBodyDiffusion");
                    if (profile == null) Debug.LogWarning("[HdrpLook] no Resources/TheatreBodyDiffusion; the bodies' scattering has no profile");
                    else _profile.Add<DiffusionProfileList>(true).diffusionProfiles.Override(new[] { profile });
                }

                _pathTracing = _profile.Add<PathTracing>(true);
                _pathTracing.enable.Override(false);
                _pathTracing.maximumDepth.Override(6);

                volume.sharedProfile = _profile;
            }

            Color water = skin != null ? skin.Water : new Color(0.012f, 0.032f, 0.048f);
            Color ambient = skin != null ? skin.Ambient : new Color(0.020f, 0.038f, 0.046f);
            Color shallow = skin != null ? skin.ShallowAsLit : water;
            float density = skin != null ? skin.FogDensity : 0.035f;

            // The URP skin's trilight ambient as a sky: the lit water above, the flat ambient at
            // the horizon, the deep below. HDRP lights from it as well as drawing it behind.
            _sky.top.Override(shallow * 0.9f);
            _sky.middle.Override(ambient);
            _sky.bottom.Override(water);
            _sky.gradientDiffusion.Override(1f);
            _sky.skyIntensityMode.Override(SkyIntensityMode.Multiplier);
            _sky.multiplier.Override(SkyAmbient);

            // With the water pass on, HDRP's fog stays only for the shafts: volumetric, thin (a long
            // mean free path, EVOSIM_HDRP_SHAFT_METRES), white, so what it adds is the key's caustic
            // cookie scattered toward the eye as columns of light, and the water pass, which runs
            // after it, fades those with distance as it fades everything else. Off with the
            // volumetric fog off or the shafts' path at zero.
            bool shaftsOnly = WaterPassOn;
            _fog.enabled.Override(!WaterPassOn || (Volumetric && ShaftMetres > 0f));
            // Under the native look the fog takes the sky's colour toward each direction, so the water is
            // lighter looking up and darker looking down, as the URP backdrop is (EVOSIM_HDRP_FOG_SKY).
            _fog.colorMode.Override(FogSky && !shaftsOnly ? FogColorMode.SkyColor : FogColorMode.ConstantColor);
            _fog.color.Override(shaftsOnly ? Color.black : water);
            _fog.albedo.Override(shaftsOnly ? Color.white : Normalised(water));
            // The URP look's water takes light out at its absorption plus its scattering per metre,
            // per channel, red first. HDRP's fog has one extinction, so it takes the green
            // channel's, which is the one the teal of the open water is mostly made of.
            float extinction = skin != null
                ? skin.Look.AbsorptionPerMetre.y + skin.Look.ScatterPerMetre
                : density;
            _fog.meanFreePath.Override(shaftsOnly ? Mathf.Max(1f, ShaftMetres)
                : FogMetres > 0f ? FogMetres : 1f / Mathf.Max(1e-4f, extinction));
            _fog.baseHeight.Override(-1000f);
            _fog.maximumHeight.Override(1000f);
            _fog.enableVolumetricFog.Override(Volumetric);
            _fog.anisotropy.Override(0.6f);
            _fog.depthExtent.Override(80f);
        }

        // The fog's albedo is the colour it scatters, as a reflectance: the water's hue at full
        // strength, since its darkness is the mean free path's business.
        private static Color Normalised(Color c)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return m > 1e-5f ? new Color(c.r / m, c.g / m, c.b / m) : Color.white;
        }

        // Every live component of a kind in the scene, those the skin made with HideFlags.DontSave
        // included: FindObjectsByType leaves those out, and the skin makes all of its furniture so.
        // Assets and prefabs are not in a scene and not DontSave, and are left out.
        private static IEnumerable<T> InScene<T>() where T : Component
        {
            foreach (T c in Resources.FindObjectsOfTypeAll<T>())
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                if (c.gameObject.scene.IsValid() || (c.gameObject.hideFlags & HideFlags.DontSave) != 0) yield return c;
            }
        }

        /// <summary>
        /// Whether the graphs take the light out on its way down (<c>EVOSIM_HDRP_DEPTH_LIGHT</c>, default 1):
        /// <c>TheatreDepthHdrp.hlsl</c>, the URP theatre's own depth light from the globals the skin pushes.
        /// </summary>
        public static float DepthLight = TheatreSkin.Dial("EVOSIM_HDRP_DEPTH_LIGHT", 1f, 0f, 1f);

        /// <summary>A test view of the body (<c>EVOSIM_HDRP_BODY_DEBUG</c>): 0 off, 1 the rim colour, 2 the rim term.</summary>
        public static float BodyDebug = TheatreSkin.Dial("EVOSIM_HDRP_BODY_DEBUG", 0f, 0f, 4f);

        /// <summary>
        /// The bed's, the rock's and the earth's specular as a share of a dielectric's (<c>EVOSIM_HDRP_STONE_SPECULAR</c>,
        /// default 0, as the URP theatre draws them; 1 is HDRP Lit's own 0.04). The bodies keep theirs.
        /// </summary>
        public static float StoneSpecular = TheatreSkin.Dial("EVOSIM_HDRP_STONE_SPECULAR", Native ? 1f : 0f, 0f, 1f);

        private static void PushDepthLight(Bounds? world)
        {
            Shader.SetGlobalFloat("_EvoHdrpSurfaceY", world.HasValue ? world.Value.max.y : 0f);
            Shader.SetGlobalFloat("_EvoHdrpDepthLight", DepthLight);
            Shader.SetGlobalFloat("_EvoHdrpStoneSpecular", StoneSpecular);
            Shader.SetGlobalFloat("_EvoHdrpBodyDebug", BodyDebug);

            // The lights as the URP body's hand-made terms read them (TheatreBodyHdrp.hlsl): the
            // direction towards each and its colour at its URP intensity, the key marked, so the
            // light through the tissue comes from every directional light as URP's does.
            int count = 0;
            foreach (Light light in InScene<Light>())
            {
                if (light.type != LightType.Directional || !light.enabled || !LightUrp.TryGetValue(light, out float urp)) continue;
                Vector3 towards = -light.transform.forward;
                Color colour = light.color.linear * urp;
                bool key = light.name == "Theatre Key";
                if (key)
                {
                    Shader.SetGlobalVector("_EvoHdrpKeyDir", new Vector4(towards.x, towards.y, towards.z, 1f));
                    Shader.SetGlobalVector("_EvoHdrpKeyColour", new Vector4(colour.r, colour.g, colour.b, 1f));
                }
                if (count >= LightDirs.Length) continue;
                LightDirs[count] = new Vector4(towards.x, towards.y, towards.z, key ? 1f : 0f);
                LightColours[count] = new Vector4(colour.r, colour.g, colour.b, 1f);
                count++;
            }
            Shader.SetGlobalVectorArray("_EvoHdrpLightDir", LightDirs);
            Shader.SetGlobalVectorArray("_EvoHdrpLightColour", LightColours);
            Shader.SetGlobalFloat("_EvoHdrpLightCount", count);
        }

        // HDRP sets a Shader Graph material's keywords, passes and stencil when the material is made
        // in the Editor's inspector; one made in code, as the skin makes all of its own, is left
        // unset until HDMaterial.ValidateMaterial is called on it. Once a material is enough.
        private static readonly HashSet<Material> Validated = new HashSet<Material>();

        // EVOSIM_HDRP_PLAIN_BED=1 draws the bed and the earth with HDRP's own Lit in the sand's grey,
        // which tells a fault in the spike's bed graph from a fault in how HDRP draws the bed's mesh.
        public static bool PlainBed = TheatreSkin.Dial("EVOSIM_HDRP_PLAIN_BED", 0f, 0f, 1f) >= 0.5f;
        private static Material _plainBed;

        private static void ValidateMaterials()
        {
            foreach (Renderer r in InScene<Renderer>())
            {
                if (PlainBed && (r.name == "Theatre Bed" || r.name == "Theatre Earth"))
                {
                    if (_plainBed == null)
                    {
                        _plainBed = new Material(Shader.Find("HDRP/Lit")) { name = "Plain bed (test)", hideFlags = HideFlags.DontSave };
                        _plainBed.SetColor("_BaseColor", new Color(0.066f, 0.065f, 0.061f));
                        _plainBed.SetFloat("_Smoothness", 0.15f);
                        HDMaterial.ValidateMaterial(_plainBed);
                    }
                    r.sharedMaterial = _plainBed;
                }

                if (r.motionVectorGenerationMode != MotionVectorGenerationMode.Camera)
                    r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;

                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || Validated.Contains(m)) continue;
                    if (!m.shader.name.StartsWith("Evosim/Theatre ")) continue;
                    if (UrpOnly.Contains(m.shader.name)) continue;
                    HDMaterial.ValidateMaterial(m);

                    // A graph of the specular colour type (the bed, the rock and the earth;
                    // TheatreDepthHdrp.hlsl's EvoStoneSpecular) needs its material feature keyword,
                    // which ValidateMaterial leaves unset on a material made in code: without it the
                    // shader draws the standard variant, with a dielectric's specular, and the graph's
                    // Specular block is never read (2026-10-19: material id 4, no keywords).
                    if (m.HasProperty("_MaterialID") && Mathf.RoundToInt(m.GetFloat("_MaterialID")) == (int)MaterialId.LitSpecular)
                        m.EnableKeyword("_MATERIAL_FEATURE_SPECULAR_COLOR");

                    if (m.shader.name == "Evosim/Theatre Bed" || m.shader.name == "Evosim/Theatre Rock" || m.shader.name == "Evosim/Theatre Earth")
                        Debug.Log("[HdrpLook] validated '" + m.name + "': material id " +
                                  (m.HasProperty("_MaterialID") ? m.GetFloat("_MaterialID").ToString(CultureInfo.InvariantCulture) : "none") +
                                  ", keywords " + string.Join(" ", m.shaderKeywords));

                    // The body reads its leaf data from the mesh's fifth UV channel, TEXCOORD4,
                    // which HDRP's motion vector pass takes for the previous position; that pass
                    // does not compile for the body, and a body drawn through it came out with
                    // depth and no light. No frame here uses motion vectors (the blur is off), so
                    // the pass is turned off and the renderers take the camera's motion only.
                    m.SetShaderPassEnabled("MotionVectors", false);
                    Validated.Add(m);
                }
            }
        }

        private static void HideUrpFurniture()
        {
            foreach (Renderer r in InScene<Renderer>())
            {
                Material m = r.sharedMaterial;
                // Forced off rather than disabled, so enabled stays what the skin and the snapshot set:
                // Follow reads it as whether the skin wants its march or its shells drawn.
                if (m != null && m.shader != null && UrpOnly.Contains(m.shader.name)) r.forceRenderingOff = true;
            }
        }

        /// <summary>
        /// A pass drawn in place of a URP volume (the shafts' march, the snow's shells) follows the
        /// skin's own object: on while the skin draws it, with its material's values copied across
        /// before every render, and with the reefs' caps for the march.
        /// </summary>
        private static void Follow(string urpShader, Material into, List<CustomPass> passes, bool wanted, bool caps)
        {
            if (passes.Count == 0 || into == null) return;

            Material urp = null;
            foreach (Renderer r in InScene<Renderer>())
            {
                Material m = r.sharedMaterial;
                if (m == null || m.shader == null || m.shader.name != urpShader) continue;
                if (r.enabled && r.gameObject.activeInHierarchy) urp = m;
            }

            bool on = wanted && urp != null;
            foreach (CustomPass pass in passes) pass.enabled = on;
            if (!on) return;

            into.CopyMatchingPropertiesFromMaterial(urp);
            if (!caps) return;
            Vector4[] reefCaps = urp.GetVectorArray("_ReefCaps");
            if (reefCaps != null && reefCaps.Length > 0) into.SetVectorArray("_ReefCaps", reefCaps);
            into.SetFloat("_ReefCapCount", urp.GetFloat("_ReefCapCount"));
        }

        /// <summary>
        /// The sea's surface from below: the skin's quad, drawn with the HDRP port of its shader.
        /// The URP material stays the one the skin writes to (the waterline, the deep colour), and
        /// its values are copied across before every render.
        /// </summary>
        private static void SwapCeiling()
        {
            foreach (Renderer r in InScene<Renderer>())
            {
                Material m = r.sharedMaterial;
                if (m == null || m.shader == null) continue;

                Material urp = null;
                if (m.shader.name == UrpCeiling) urp = m;
                else foreach (var pair in Ceilings) if (pair.Value == m) urp = pair.Key;
                if (urp == null) continue;

                if (!Ceilings.TryGetValue(urp, out Material hdrp) || hdrp == null)
                {
                    Shader shader = Shader.Find("Evosim/Theatre Ceiling");
                    if (shader == null) { Debug.LogWarning("[HdrpLook] no Evosim/Theatre Ceiling shader; the ceiling stays hidden"); r.enabled = false; continue; }
                    hdrp = new Material(shader) { name = urp.name + " (HDRP)", hideFlags = HideFlags.DontSave };
                    Ceilings[urp] = hdrp;
                }

                hdrp.CopyMatchingPropertiesFromMaterial(urp);
                HDMaterial.ValidateMaterial(hdrp);
                r.sharedMaterial = hdrp;
                // Under HDRP's own water surface the ported ceiling would be a second sea at the same height.
                // Without HDRP's underwater rendering its water surface draws nothing seen from below.
                r.forceRenderingOff = WaterSurfaceOn && Underwater;
                // Left on or off as the skin and the snapshot set it: the census and reef views turn
                // the ceiling off for their render (SnapshotCamera.HideWhatOnlyTheWaterSees).
            }
        }

        // The skin's body material and the subsurface scattering one drawn in its place (BodySss).
        private const string UrpBody = "Evosim/Theatre Body";
        private static readonly Dictionary<Material, Material> Bodies = new Dictionary<Material, Material>();
        private static bool _bodyWarned;
        private static float _bodyProfileHash = float.NaN;

        // The bodies' profile's hash as the float a material carries: the bits of the profile's uint
        // hash, which DiffusionProfileSettings keeps in an internal field.
        private static float BodyProfileHash()
        {
            var settings = Resources.Load<DiffusionProfileSettings>("TheatreBodyDiffusion");
            if (settings == null) return float.NaN;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            object profile = typeof(DiffusionProfileSettings).GetField("profile", flags)?.GetValue(settings);
            object hash = profile?.GetType().GetField("hash", flags)?.GetValue(profile);
            if (!(hash is uint bits) || bits == 0) { Debug.LogWarning("[HdrpLook] the bodies' diffusion profile has no hash"); return float.NaN; }
            Debug.Log("[HdrpLook] the bodies' diffusion profile hash " + bits);
            return System.BitConverter.Int32BitsToSingle(unchecked((int)bits));
        }

        /// <summary>
        /// The bodies through HDRP's subsurface scattering (<see cref="BodySss"/>): every renderer on the
        /// skin's body material is given the graph "Evosim/Theatre Body SSS", which takes the same
        /// properties, with the skin's material's values copied across before every render. The palette's
        /// per body values are property blocks on the renderers and stay where they are.
        /// </summary>
        private static void SwapBodies()
        {
            if (!BodySss) return;
            string wanted = BodyLight == 2 ? "Evosim/Theatre Body SSS" : "Evosim/Theatre Body Translucent";
            Shader shader = Shader.Find(wanted);
            if (shader == null)
            {
                if (!_bodyWarned) Debug.LogWarning("[HdrpLook] no " + wanted + " shader; the bodies stay standard");
                _bodyWarned = true;
                return;
            }

            foreach (Renderer r in InScene<Renderer>())
            {
                Material[] materials = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material m = materials[i];
                    if (m == null || m.shader == null) continue;

                    Material urp = null;
                    if (m.shader.name == UrpBody) urp = m;
                    else foreach (var pair in Bodies) if (pair.Value == m) urp = pair.Key;
                    if (urp == null) continue;

                    if (!Bodies.TryGetValue(urp, out Material sss) || sss == null)
                    {
                        sss = new Material(shader) { name = urp.name + (BodyLight == 2 ? " (HDRP SSS)" : " (HDRP translucent)"), hideFlags = HideFlags.DontSave };
                        Bodies[urp] = sss;
                    }
                    if (materials[i] != sss) { materials[i] = sss; changed = true; }
                }
                if (changed) r.sharedMaterials = materials;
            }

            foreach (var pair in Bodies)
            {
                if (pair.Key == null || pair.Value == null) continue;
                pair.Value.CopyMatchingPropertiesFromMaterial(pair.Key);
                pair.Value.shaderKeywords = pair.Key.shaderKeywords;

                // As the stone's specular colour (ValidateMaterials): a graph material made in code is
                // given no material feature keyword, and without these HDRP draws the standard variant,
                // with no scattering and no transmission, and never reads the diffusion profile
                // (2026-10-19: two profiles rendered the same frame to the pixel).
                if (BodyLight == 2) pair.Value.EnableKeyword("_MATERIAL_FEATURE_SUBSURFACE_SCATTERING");
                pair.Value.EnableKeyword("_MATERIAL_FEATURE_TRANSMISSION");

                // The profile by the material's own hash, which the graph's block reads before the hash
                // it was imported with: one set in code does not depend on the graph's asset reference.
                // Set as the float HDMaterial.SetDiffusionProfile writes; that call also adds the profile to
                // the material as an asset in the Editor, which fails for a material made in code.
                if (float.IsNaN(_bodyProfileHash)) _bodyProfileHash = BodyProfileHash();
                if (!float.IsNaN(_bodyProfileHash)) pair.Value.SetFloat("_DiffusionProfileHash", _bodyProfileHash);
            }
        }

        private static void EnsureWaterPass()
        {
            if (_passVolume != null || _holder == null) return;
            if (!WaterPassOn && !ShaftsOn && !SnowOn) return;

            Shader shader = Shader.Find("Hidden/Evosim/Theatre Water Pass");
            if (shader == null) { Debug.LogWarning("[HdrpLook] no Theatre Water Pass shader; the water is HDRP's fog alone"); return; }

            _passMaterial = new Material(shader) { name = "Theatre water pass", hideFlags = HideFlags.DontSave };
            _snowMaterial = new Material(shader) { name = "Theatre snow pass", hideFlags = HideFlags.DontSave };
            _passVolume = _holder.AddComponent<CustomPassVolume>();
            _passVolume.isGlobal = true;
            // Two passes over the frame after the opaque objects and the sky: the first multiplies
            // it by what the water keeps, the second adds what it scatters in. The pass never reads
            // the frame; HDRP did not copy it for a pass to read, and the read came back black.
            _passVolume.injectionPoint = CustomPassInjectionPoint.AfterOpaqueAndSky;
            // Then the shafts, the same way: the march's colour is screened over the frame under
            // item 3 (multiplied by one less it, then added) and added otherwise. Then the snow, added.
            // Under the native look the water pass's own two are left out, and the snow writes its
            // depth after its light, for the physical camera's depth of field (EVOSIM_HDRP_SNOW_DEPTH).
            foreach (string name in new[] { "Theatre Water Keep", "Theatre Water Add", "Theatre Shaft Keep", "Theatre Shaft Add", "Theatre Snow Add", "Theatre Snow Depth" })
            {
                if (name.StartsWith("Theatre Water") && !WaterPassOn) continue;
                if (name == "Theatre Snow Depth" && !SnowDepth) continue;
                bool snow = name.StartsWith("Theatre Snow");
                var pass = _passVolume.AddPassOfType<FullScreenCustomPass>();
                pass.name = name;
                pass.fullscreenPassMaterial = snow ? _snowMaterial : _passMaterial;
                pass.materialPassName = name;
                pass.fetchColorBuffer = false;
                pass.targetColorBuffer = CustomPass.TargetBuffer.Camera;
                pass.targetDepthBuffer = CustomPass.TargetBuffer.Camera;
                if (name.StartsWith("Theatre Shaft")) { pass.enabled = false; ShaftPasses.Add(pass); }
                if (snow) { pass.enabled = false; SnowPasses.Add(pass); }
            }
            Debug.Log("[HdrpLook] the passes after the opaque objects and the sky: " +
                      string.Join(", ", _passVolume.customPasses.ConvertAll(x => x.name)));
        }

        private static void EnsureToneStep()
        {
            if (!ToneStepOn || _toneVolume != null || _holder == null) return;

            Shader shader = Shader.Find("Hidden/Evosim/Theatre Tone Step HDRP");
            if (shader == null) { Debug.LogWarning("[HdrpLook] no Theatre Tone Step HDRP shader; the frame goes without the tone step"); return; }

            _toneVolume = _holder.AddComponent<CustomPassVolume>();
            _toneVolume.isGlobal = true;
            _toneVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
            var pass = _toneVolume.AddPassOfType<HdrpToneStep>();
            pass.name = "Theatre Tone Step";
            pass.Material = new Material(shader) { name = "Theatre tone step", hideFlags = HideFlags.DontSave };
            Debug.Log("[HdrpLook] the tone step is ready after the post-processing; it runs where the skin turns it on");
        }

        private static readonly List<Light> Gone = new List<Light>();

        private static void ConvertLights()
        {
            // The snapshot makes a portrait's lights for one render and destroys them after.
            Gone.Clear();
            foreach (Light light in LightSet.Keys) if (light == null) Gone.Add(light);
            foreach (Light light in Gone) { LightSet.Remove(light); LightUrp.Remove(light); }

            foreach (Light light in InScene<Light>())
            {
                HDAdditionalLightData extra = light.GetComponent<HDAdditionalLightData>();
                if (extra == null) extra = light.gameObject.AddComponent<HDAdditionalLightData>();
                extra.affectSpecular = LightSpecular;

                // Scaled once for each value the skin sets: a light the skin has reset since the
                // last render is scaled again, and one already scaled is left alone.
                if (LightSet.TryGetValue(light, out float set) && Mathf.Approximately(set, light.intensity)) continue;

                light.lightUnit = light.type == LightType.Directional ? LightUnit.Lux : LightUnit.Candela;
                float scale = LightScale;

                // The sun carries the caustic net. The net's mean is under one, so the sun is
                // raised by its inverse and the light that reaches the water on average is the
                // light the URP picture had. The key alone: the fill is the URP skin's weak cold
                // light from the far side and carries no net there.
                if (light.type == LightType.Directional && light.name == "Theatre Key" && CausticMetres > 0f)
                {
                    Texture2D net = CausticNet(out float mean);
                    var data = light.GetComponent<HDAdditionalLightData>();
                    data.SetCookie(net, new Vector2(CausticMetres, CausticMetres));
                    data.affectsVolumetric = true;
                    data.volumetricDimmer = 1f;
                    scale /= Mathf.Max(0.05f, mean);
                }

                LightUrp[light] = light.intensity;
                light.intensity *= scale;
                LightSet[light] = light.intensity;
            }
        }

        // ---------------------------------------------------------------- the caustic net

        /// <summary>
        /// How wide one tile of the caustic net is on the sun, in metres (<c>EVOSIM_HDRP_CAUSTIC_METRES</c>,
        /// default 4; 0 leaves the sun plain). The sun's cookie is what makes both the net on every lit
        /// surface and the shafts: HDRP's volumetric fog carries a directional light's cookie, so the
        /// bright lines of the net become columns of light in the water.
        /// </summary>
        public static float CausticMetres = TheatreSkin.Dial("EVOSIM_HDRP_CAUSTIC_METRES", 4f, 0f, 100f);

        private const int NetSize = 512;
        private const int NetCells = 9;
        private static Texture2D _net;
        private static float _netMean;

        /// <summary>
        /// A tileable caustic net: the lines where a cellular pattern's two nearest points are about
        /// equally near, sharpened, with a second finer pattern laid over at half strength. Built once,
        /// from a fixed seed, so every frame and every run shows the same net.
        /// </summary>
        private static Texture2D CausticNet(out float mean)
        {
            if (_net != null)
            {
                mean = _netMean;
                return _net;
            }

            var pixels = new Color[NetSize * NetSize];
            Vector2[] coarse = CellPoints(NetCells, 7);
            Vector2[] fine = CellPoints(NetCells * 2, 11);
            double sum = 0;

            for (int y = 0; y < NetSize; y++)
            {
                for (int x = 0; x < NetSize; x++)
                {
                    var p = new Vector2((x + 0.5f) / NetSize, (y + 0.5f) / NetSize);
                    float a = Ridge(p, coarse, NetCells, 0.09f);
                    float b = Ridge(p, fine, NetCells * 2, 0.09f);
                    float v = Mathf.Clamp01(0.30f + 0.70f * Mathf.Max(a, 0.5f * b));
                    pixels[y * NetSize + x] = new Color(v, v, v, 1f);
                    sum += v;
                }
            }

            _net = new Texture2D(NetSize, NetSize, TextureFormat.RGBA32, true, true)
            {
                name = "Theatre caustic net",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave,
            };
            _net.SetPixels(pixels);
            _net.Apply(true, false);
            _netMean = (float)(sum / pixels.Length);

            Debug.Log("[HdrpLook] caustic net " + NetSize + " px, a tile " + CausticMetres + " m, mean " + _netMean.ToString("0.000"));
            mean = _netMean;
            return _net;
        }

        // One point in each cell of an n by n grid on the unit torus, from a fixed seed.
        private static Vector2[] CellPoints(int n, int seed)
        {
            var random = new System.Random(seed);
            var points = new Vector2[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    points[j * n + i] = new Vector2((i + (float)random.NextDouble()) / n, (j + (float)random.NextDouble()) / n);
            return points;
        }

        // 1 on the net's lines and 0 in the middle of a cell: the gap between the nearest and the
        // second nearest point, as a fraction of a cell, under the given width, cubed to sharpen.
        private static float Ridge(Vector2 p, Vector2[] points, int n, float width)
        {
            int ci = Mathf.FloorToInt(p.x * n), cj = Mathf.FloorToInt(p.y * n);
            float f1 = float.MaxValue, f2 = float.MaxValue;
            for (int dj = -1; dj <= 1; dj++)
            {
                for (int di = -1; di <= 1; di++)
                {
                    int i = ci + di, j = cj + dj;
                    int wi = ((i % n) + n) % n, wj = ((j % n) + n) % n;
                    Vector2 q = points[wj * n + wi] + new Vector2((i - wi) / (float)n, (j - wj) / (float)n);
                    float d = (q - p).magnitude;
                    if (d < f1) { f2 = f1; f1 = d; }
                    else if (d < f2) f2 = d;
                }
            }

            float gap = (f2 - f1) * n;
            float line = 1f - Mathf.Clamp01(gap / width);
            return line * line * line;
        }

        // The surface sits on the top of the world's box, the waterline, and reaches past its sides so
        // no edge shows from below.
        private static void EnsureWater(TheatreSkin skin, Bounds box)
        {
            if (_water != null) return;

            var go = new GameObject("Theatre HDRP Water") { hideFlags = HideFlags.DontSave };
            go.transform.position = new Vector3(box.center.x, box.max.y, box.center.z);

            _water = go.AddComponent<WaterSurface>();
            _water.surfaceType = WaterSurfaceType.Pool;
            _water.geometryType = WaterGeometryType.Quad;
            go.transform.localScale = new Vector3(box.size.x * 1.5f, 1f, box.size.z * 1.5f);
            // The ceiling only. The light under it is the volumetric fog's and the sun's caustic net's:
            // HDRP's own underwater pass needs a volume box and draws a fog of its own, and its
            // caustics would lay a second net over the sun's.
            _water.underWater = Underwater;
            _water.caustics = WaterCaustics > 0f;
            _water.causticsIntensity = WaterCaustics;
            if (Underwater)
            {
                // HDRP's underwater effect on a surface that is not infinite applies inside a box.
                var bounds = new GameObject("Theatre HDRP Underwater") { hideFlags = HideFlags.DontSave };
                bounds.transform.position = box.center;
                var collider = bounds.AddComponent<BoxCollider>();
                collider.size = box.size + new Vector3(1f, 1f, 1f);
                collider.isTrigger = true;
                _water.volumeBounds = collider;
            }

            Color water = skin != null ? skin.Water : new Color(0.012f, 0.032f, 0.048f);
            _water.refractionColor = Normalised(water);
            _water.scatteringColor = water;
            _water.absorptionDistance = skin != null ? 1f / Mathf.Max(1e-4f, skin.FogDensity) : 30f;
        }
    }
}
