using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Evosim.Theatre
{
    /// <summary>
    /// The theatre's lens (D208, item 1): depth of field on the bodies and the scenery, from the shot's
    /// own focus, through a thin lens's circle of confusion. <c>TheatreLens.shader</c> says how it is
    /// drawn and why URP's own Bokeh would not do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>When it runs.</b> Only for a placed frame (a film shot or a safari take) while
    /// <see cref="TheatreLook.Lens"/> is on: the snapshot arms it before the render with the shot's focus,
    /// or none for an autofocus at the frame's centre, and disarms it after. A census view is never armed,
    /// since a diagram in focus everywhere is the point of a diagram. While it is armed the grade leaves
    /// URP's own depth of field off (<see cref="TheatreGrade.FocusPortrait"/>).
    /// </para>
    /// <para>
    /// <b>Where.</b> After the transparent objects and before the post-processing, queued on the camera as it
    /// starts rendering, as the tone step is. The surface and the shafts write no depth, so the lens takes
    /// them at the depth of what lies behind them: the surface far off and soft, the shafts already soft.
    /// The snow shells cannot be taken so, since a mote in focus in front of a far bed would be blurred
    /// by the bed's depth. So while the lens is armed their renderer is held off the camera's own drawing
    /// (<see cref="Renderer.forceRenderingOff"/>), and the lens draws them itself after its composite,
    /// sharp, to blur each mote by its own depth (<see cref="TheatreSkin.PushSnowLens"/>).
    /// </para>
    /// <para>
    /// <b>The lens.</b> The aperture is the grade's (<see cref="TheatreGrade.PortraitAperture"/>, which a
    /// story scene's focus may set) on the grade's frame (<see cref="TheatreGrade.FormatMillimetres"/>),
    /// with the focal length the camera's field of view makes on it: the same lens the snow takes.
    /// </para>
    /// </remarks>
    public static class TheatreLens
    {
        /// <summary>True between <see cref="Arm"/> and <see cref="Disarm"/>.</summary>
        public static bool Armed { get; private set; }

        private static float _focus;
        private static float _fieldOfView;
        private static Material _material;
        private static LensPass _pass;
        private static bool _hooked;
        private static Renderer _shells;
        private static Mesh _shellsMesh;
        private static string _told;

        /// <summary>Whether the look wants the lens (<see cref="TheatreLook.Lens"/>).</summary>
        public static bool Wanted => TheatreSkin.Current != null && TheatreSkin.Current.Look.Lens;

        /// <summary>
        /// Arms the lens for the next render: the focus along the view axis, m, or 0 for the autofocus,
        /// and the camera's vertical field of view, degrees. Nothing when the look has it off.
        /// </summary>
        public static void Arm(float focusMetres, float fieldOfView)
        {
            if (!Wanted) { Armed = false; return; }

            // A scene whose story turns the focus off (EVOSIM_THEATRE_DOF=0, or a focus of "off") is sharp.
            if (TheatreGrade.Current != null && !TheatreGrade.Current.PortraitDepthOfField) { Armed = false; return; }
            Ensure();
            if (_material == null) { Armed = false; return; }

            _focus = Mathf.Max(0f, focusMetres);
            _fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
            Armed = true;

            // Said in the log whenever it changes, not every frame.
            string told = Describe(_focus, _fieldOfView);
            if (told != _told) Debug.Log("[Theatre] " + told);
            _told = told;

            // The snow shells, taken off the camera's own drawing to be drawn after the lens.
            Renderer shells = TheatreSkin.Current.ShellsRenderer;
            MeshFilter filter = shells != null ? shells.GetComponent<MeshFilter>() : null;
            if (shells != null && shells.enabled && !shells.forceRenderingOff && filter != null && filter.sharedMesh != null)
            {
                _shells = shells;
                _shellsMesh = filter.sharedMesh;
                _shells.forceRenderingOff = true;
            }
        }

        public static void Disarm()
        {
            Armed = false;
            if (_shells != null) _shells.forceRenderingOff = false;
            _shells = null;
            _shellsMesh = null;
        }

        /// <summary>The lens in one phrase, for the log.</summary>
        public static string Describe(float focusMetres, float fieldOfView)
        {
            TheatreGrade grade = TheatreGrade.Current;
            float format = grade != null ? grade.FormatMillimetres : 24f;
            float n = grade != null ? Mathf.Clamp(grade.PortraitAperture, 1f, 32f) : 2f;
            float focal = Camera.FieldOfViewToFocalLength(Mathf.Clamp(fieldOfView, 1f, 179f), format);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "lens {0:0} mm at f/{1:0.#} on a {2:0} mm frame, focus {3}, blur at most {4:0} px at 1080 lines",
                focal, n, format, focusMetres > 0f ? focusMetres.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " m" : "auto",
                TheatreSkin.Current != null ? TheatreSkin.Current.Look.LensBlurCap : 24f);
        }

        private static void Ensure()
        {
            if (_hooked) return;

            Shader shader = Shader.Find("Evosim/Theatre Lens");
            if (shader == null)
            {
                Debug.LogWarning("[Theatre] lens: shader 'Evosim/Theatre Lens' not found, so the lens is off");
                _hooked = true;
                return;
            }

            _material = new Material(shader) { name = "Theatre Lens", hideFlags = HideFlags.DontSave };
            _pass = new LensPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            _hooked = true;
        }

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!Armed || _material == null || camera == null) return;
            if (camera.cameraType != CameraType.Game) return;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null) return;

            data.scriptableRenderer.EnqueuePass(_pass);
        }

        // Sets the lens's numbers for a target of this many lines.
        private static void Push(int width, int height)
        {
            TheatreGrade grade = TheatreGrade.Current;
            TheatreLook look = TheatreSkin.Current != null ? TheatreSkin.Current.Look : null;
            float format = grade != null ? grade.FormatMillimetres : 24f;
            float n = grade != null ? Mathf.Clamp(grade.PortraitAperture, 1f, 32f) : 2f;
            float focal = Camera.FieldOfViewToFocalLength(_fieldOfView, format) / 1000f;
            float aperture = focal / n;
            float tanHalf = Mathf.Tan(0.5f * _fieldOfView * Mathf.Deg2Rad);
            float k0 = aperture * height / (4f * tanHalf);
            float cap = Mathf.Clamp(look != null ? look.LensBlurCap : 24f, 1f, 120f) * height / 1080f;

            _material.SetVector("_LensParams", new Vector4(_focus, k0, cap, 12f));
            _material.SetVector("_LensTarget", new Vector4(1f / width, 1f / height, width, height));
        }

        private sealed class LensPass : ScriptableRenderPass
        {
            public LensPass()
            {
                requiresIntermediateTexture = true;
            }

            private sealed class PassData
            {
                public TextureHandle Source;
                public TextureHandle Depth;
                public TextureHandle Blur;
                public Material Material;
                public int Pass;
                public Mesh Shells;
                public Matrix4x4 ShellsMatrix;
                public Material ShellsMaterial;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                TextureHandle depth = resources.cameraDepthTexture;
                if (!source.IsValid() || !depth.IsValid()) return;

                TextureDesc full = renderGraph.GetTextureDesc(source);
                Push(full.width, full.height);

                TextureDesc half = full;
                half.width = Mathf.Max(1, full.width / 2);
                half.height = Mathf.Max(1, full.height / 2);
                half.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                half.depthBufferBits = DepthBits.None;
                half.msaaSamples = MSAASamples.None;
                half.clearBuffer = false;
                half.filterMode = FilterMode.Bilinear;

                half.name = "Theatre Lens Prefilter";
                TextureHandle prefiltered = renderGraph.CreateTexture(half);
                half.name = "Theatre Lens Gather";
                TextureHandle gathered = renderGraph.CreateTexture(half);

                TextureDesc outDesc = full;
                outDesc.name = "Theatre Lens";
                outDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(outDesc);

                Add(renderGraph, "Theatre Lens Prefilter", source, depth, TextureHandle.nullHandle, prefiltered, 0, false);
                Add(renderGraph, "Theatre Lens Gather", prefiltered, depth, TextureHandle.nullHandle, gathered, 1, false);
                Add(renderGraph, "Theatre Lens Composite", source, depth, gathered, destination, 2, true);

                resources.cameraColor = destination;
            }

            private static void Add(RenderGraph renderGraph, string name, TextureHandle source, TextureHandle depth,
                TextureHandle blur, TextureHandle destination, int pass, bool withShells)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(name, out PassData data))
                {
                    data.Source = source;
                    data.Depth = depth;
                    data.Blur = blur;
                    data.Material = _material;
                    data.Pass = pass;
                    if (withShells && _shells != null && _shellsMesh != null && _shells.sharedMaterial != null)
                    {
                        data.Shells = _shellsMesh;
                        data.ShellsMatrix = _shells.localToWorldMatrix;
                        data.ShellsMaterial = _shells.sharedMaterial;
                    }

                    builder.UseTexture(source);
                    builder.UseTexture(depth);
                    if (blur.IsValid()) builder.UseTexture(blur);
                    builder.SetRenderAttachment(destination, 0);

                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        d.Material.SetTexture("_LensDepth", d.Depth);
                        if (d.Blur.IsValid()) d.Material.SetTexture("_LensBlur", d.Blur);
                        Blitter.BlitTexture(context.cmd, d.Source, new Vector4(1f, 1f, 0f, 0f), d.Material, d.Pass);

                        // The snow, after the lens and blurred by its own (TheatreSnowShells.shader reads
                        // the scene's depth from the copy URP made before this pass).
                        if (d.Shells != null) context.cmd.DrawMesh(d.Shells, d.ShellsMatrix, d.ShellsMaterial, 0, 0);
                    });
                }
            }
        }
    }
}
