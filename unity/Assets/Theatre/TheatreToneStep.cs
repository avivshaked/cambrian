using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Evosim.Theatre
{
    /// <summary>
    /// The underwater look's second batch, item 2 (<c>logbook/specs/underwater-look/brief-2.md</c>):
    /// one tone curve after everything else, a black point and a gentle S, so the darkest tenth of
    /// a frame reads dark again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not a grade.</b> The curve is taken on the luma of the display-encoded colour and the
    /// colour is scaled by the curve's ratio, so a pixel's hue and its channels' proportions are
    /// kept (<c>TheatreToneStep.shader</c>); and a ratio that would push any channel past white is
    /// held at white, so it cannot clip a leaf's highlight.
    /// </para>
    /// <para>
    /// <b>Where it runs.</b> After the post-processing, as a pass the theatre queues on each game
    /// camera as it starts rendering (URP's scripted injection, no renderer feature and no change to
    /// the project's renderer asset), so the grade's tone mapping, bloom and vignette are already in
    /// the picture it shapes. It runs only while <see cref="Active"/> is set, which the skin sets from
    /// the switch and clears while a census view holds today's look, and only for a camera in the
    /// water.
    /// </para>
    /// </remarks>
    public static class TheatreToneStep
    {
        /// <summary>True while the skin wants the curve on the next render.</summary>
        public static bool Active;

        /// <summary>Black point, the S's toe power, its pivot and its shoulder power, display-encoded luma.</summary>
        public static Vector4 Curve = new Vector4(0.01f, 1.2f, 0.2f, 1.1f);

        private static Material _material;
        private static TonePass _pass;
        private static bool _hooked;

        /// <summary>Hooks the pass into URP's camera loop once; harmless to call again.</summary>
        public static void Ensure()
        {
            if (_hooked) return;

            Shader shader = Shader.Find("Evosim/Theatre Tone Step");
            if (shader == null)
            {
                Debug.LogWarning("[Theatre] tone step: shader 'Evosim/Theatre Tone Step' not found, so the curve is off");
                return;
            }

            _material = new Material(shader) { name = "Theatre Tone Step", hideFlags = HideFlags.DontSave };
            _pass = new TonePass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            _hooked = true;
        }

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!Active || _material == null || camera == null) return;
            if (camera.cameraType != CameraType.Game) return;

            // Only for an eye in the water: the curve gives back the black point the water's light
            // took, and a view from beyond the glass (the bed view, the close view) looks mostly
            // through air at a backdrop and never lost it (iteration 3: the curve took the bed
            // view's mean from 37 to 30 and crushed its earth wall).
            TheatreSkin skin = TheatreSkin.Current;
            if (skin != null && !skin.EyeInWater(camera.transform.position)) return;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null) return;

            _material.SetVector("_ToneCurve", Curve);
            data.scriptableRenderer.EnqueuePass(_pass);
        }

        private sealed class TonePass : ScriptableRenderPass
        {
            public TonePass()
            {
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                if (!source.IsValid()) return;

                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "Theatre Tone Step";
                desc.clearBuffer = false;

                TextureHandle destination = renderGraph.CreateTexture(desc);

                var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
                renderGraph.AddBlitPass(blit, "Theatre Tone Step");

                resources.cameraColor = destination;
            }
        }
    }
}
