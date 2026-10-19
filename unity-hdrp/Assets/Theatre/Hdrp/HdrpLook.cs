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
        public static int PathTraceSamples = (int)TheatreSkin.Dial("EVOSIM_HDRP_PATHTRACE", 0f, 0f, 16384f);
        public static bool WaterSurfaceOn = TheatreSkin.Dial("EVOSIM_HDRP_WATER", 0f, 0f, 1f) >= 0.5f;
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
        public static bool WaterPassOn = TheatreSkin.Dial("EVOSIM_HDRP_WATER_PASS", 1f, 0f, 1f) >= 0.5f;

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
        public static bool ShaftsOn = TheatreSkin.Dial("EVOSIM_HDRP_SHAFTS", 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The marine snow as the URP theatre's shells draw it, one more pass after the shafts
        /// (<c>EVOSIM_HDRP_SNOW</c>, 1 on, the default), where the skin turns its own shells on.
        /// </summary>
        public static bool SnowOn = TheatreSkin.Dial("EVOSIM_HDRP_SNOW", 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The URP theatre's tone step after HDRP's post-processing (<see cref="HdrpToneStep"/>;
        /// <c>EVOSIM_HDRP_TONE_STEP</c>, 1 on, the default). It runs only where the skin turns it on.
        /// </summary>
        public static bool ToneStepOn = TheatreSkin.Dial("EVOSIM_HDRP_TONE_STEP", 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// Whether HDRP's lights draw a specular highlight (<c>EVOSIM_HDRP_LIGHT_SPECULAR</c>, 1 on, the
        /// default). A test dial: the URP theatre's bed and rock have no specular term, and HDRP Lit's
        /// grows toward a grazing view.
        /// </summary>
        public static bool LightSpecular = TheatreSkin.Dial("EVOSIM_HDRP_LIGHT_SPECULAR", 1f, 0f, 1f) >= 0.5f;

        /// <summary>HDRP's environment reflections (<c>EVOSIM_HDRP_REFLECTIONS</c>, 0 off, the default).</summary>
        public static bool Reflections = TheatreSkin.Dial("EVOSIM_HDRP_REFLECTIONS", 0f, 0f, 1f) >= 0.5f;

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

            if (PathTraceSamples <= 0 || !SystemInfo.supportsRayTracing)
            {
                if (_pathTracing != null) _pathTracing.enable.Override(false);
                camera.Render();
                return;
            }

            // The path tracer adds one sample a frame and starts again when anything moves, so a
            // still is the same frame rendered as many times as it has samples.
            _pathTracing.enable.Override(true);
            _pathTracing.maximumSamples.Override(PathTraceSamples);
            for (int i = 0; i < PathTraceSamples; i++) camera.Render();
            _pathTracing.enable.Override(false);
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
            }

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
            "fog {0} m mean free path{1}, water surface {2}, lights x{3:0.###}, path tracing {4}",
            _fog != null ? _fog.meanFreePath.value : 0f, Volumetric ? " volumetric" : "",
            _water != null ? "on" : "off", LightScale,
            PathTraceSamples > 0 ? PathTraceSamples + " samples" : "off");

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
            _fog.colorMode.Override(FogColorMode.ConstantColor);
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
        public static float StoneSpecular = TheatreSkin.Dial("EVOSIM_HDRP_STONE_SPECULAR", 0f, 0f, 1f);

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
                // Left on or off as the skin and the snapshot set it: the census and reef views turn
                // the ceiling off for their render (SnapshotCamera.HideWhatOnlyTheWaterSees).
            }
        }

        private static void EnsureWaterPass()
        {
            if (!WaterPassOn || _passVolume != null || _holder == null) return;

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
            foreach (string name in new[] { "Theatre Water Keep", "Theatre Water Add", "Theatre Shaft Keep", "Theatre Shaft Add", "Theatre Snow Add" })
            {
                bool snow = name.StartsWith("Theatre Snow");
                var pass = _passVolume.AddPassOfType<FullScreenCustomPass>();
                pass.name = name;
                pass.fullscreenPassMaterial = snow ? _snowMaterial : _passMaterial;
                pass.materialPassName = name;
                pass.fetchColorBuffer = false;
                if (name.StartsWith("Theatre Shaft")) { pass.enabled = false; ShaftPasses.Add(pass); }
                if (snow) { pass.enabled = false; SnowPasses.Add(pass); }
            }
            Debug.Log("[HdrpLook] the water pass is on: keep, then add, after the opaque objects and the sky");
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
            _water.underWater = false;
            _water.caustics = false;

            Color water = skin != null ? skin.Water : new Color(0.012f, 0.032f, 0.048f);
            _water.refractionColor = Normalised(water);
            _water.scatteringColor = water;
            _water.absorptionDistance = skin != null ? 1f / Mathf.Max(1e-4f, skin.FogDensity) : 30f;
        }
    }
}
