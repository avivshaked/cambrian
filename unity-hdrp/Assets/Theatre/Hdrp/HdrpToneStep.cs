using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Evosim.Theatre
{
    /// <summary>
    /// The URP theatre's tone step on HDRP, for the spike (D202): the same curve on the luma of the
    /// display-encoded colour (<c>TheatreToneStepHdrp.shader</c>, from <c>TheatreToneStep.shader</c>),
    /// run after HDRP's post-processing as URP runs it after its own. The skin sets
    /// <see cref="TheatreToneStep.Active"/> and <see cref="TheatreToneStep.Curve"/> in either theatre.
    /// </summary>
    /// <remarks>
    /// A pass of its own rather than a <see cref="FullScreenCustomPass"/>, because the curve reads the
    /// pixel it writes and HDRP's full-screen pass does not copy the frame for its shader (its colour
    /// fetch only resolves MSAA). So this copies the frame into a buffer of half floats and draws the
    /// curve from that copy back over the frame.
    /// </remarks>
    [System.Serializable]
    public sealed class HdrpToneStep : CustomPass
    {
        public Material Material;

        private RTHandle _copy;
        private static readonly int SourceId = Shader.PropertyToID("_ToneSource");
        private static readonly int CurveId = Shader.PropertyToID("_ToneCurve");

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            _copy = RTHandles.Alloc(Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
                colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true, name: "Theatre tone step copy");
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (Material == null || !TheatreToneStep.Active) return;

            CustomPassUtils.Copy(ctx, ctx.cameraColorBuffer, _copy);
            ctx.propertyBlock.SetTexture(SourceId, _copy);
            ctx.propertyBlock.SetVector(CurveId, TheatreToneStep.Curve);
            CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ClearFlag.None);
            CoreUtils.DrawFullScreen(ctx.cmd, Material, ctx.propertyBlock, shaderPassId: 0);
        }

        protected override void Cleanup()
        {
            _copy?.Release();
            _copy = null;
        }
    }
}
