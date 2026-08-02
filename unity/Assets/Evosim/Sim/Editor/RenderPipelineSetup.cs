using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// Creates the URP pipeline assets and makes them the project default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DESIGN.md §10 puts URP in Milestone 0. The project was created with
    /// <c>-createProject</c>, which defaults to the Built-In Render Pipeline — deprecated as
    /// of Unity 6.5 and supported only through 6.7 LTS.
    /// </para>
    /// <para>
    /// The switch is cheap now and expensive later: at Milestone 1 the entire visual surface
    /// is cube meshes and one material. The theatre (Milestone 7) is where the underwater
    /// look actually gets built — fog, post-processing, depth — and that work assumes a
    /// scriptable pipeline.
    /// </para>
    /// <para>
    /// Generated rather than committed, for the same reason the sandbox scene is: pipeline
    /// assets are opaque YAML that cannot be reviewed in a diff.
    /// </para>
    /// </remarks>
    public static class RenderPipelineSetup
    {
        private const string Folder = "Assets/Rendering";
        private const string RendererPath = Folder + "/UniversalRenderer.asset";
        private const string PipelinePath = Folder + "/UniversalRenderPipelineAsset.asset";

        [MenuItem("Evosim/Set Up URP")]
        public static bool Setup()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "Rendering");
            }

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);

            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            AssetDatabase.SaveAssets();

            // Verify from the asset database rather than trusting the assignment above —
            // logbook/0004.
            bool onDisk =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath) != null &&
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath) != null;

            if (!onDisk)
            {
                Debug.LogError("[Evosim] URP assets were not written to disk.");
                return false;
            }

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                Debug.LogError("[Evosim] URP asset created but GraphicsSettings still has no pipeline.");
                return false;
            }

            Debug.Log($"[Evosim] URP configured: {PipelinePath}");
            return true;
        }

        /// <summary>Batchmode entry point.</summary>
        public static void Run()
        {
            bool ok = Setup();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
