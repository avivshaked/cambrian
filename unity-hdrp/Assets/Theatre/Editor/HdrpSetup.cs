using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Puts unity-hdrp/ on HDRP the first time it opens, for the theatre's HDRP spike (D202).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The project's settings were copied from unity/, which runs on URP; their pipeline asset is
    /// URP's and is not in this project. This makes an HDRP asset in code, as unity/'s
    /// <c>RenderPipelineSetup</c> makes URP's, and points the graphics and every quality level at it.
    /// Running it again changes nothing that is already right.
    /// </para>
    /// <para>
    /// What the spike needs of the asset: volumetric fog (light in the water), the water system
    /// (the surface seen from below and its caustics), ray tracing (the path tracer) and a
    /// physically based depth of field at the high quality the grade asks for. Ray tracing needs
    /// Direct3D 12, so the Windows graphics API is set to it as well, and a batch run is started
    /// with <c>-force-d3d12</c>.
    /// </para>
    /// <para>
    /// <c>Unity.exe -batchmode -projectPath unity-hdrp -executeMethod
    /// Evosim.Theatre.EditorTools.HdrpSetup.Run -quit -logFile ...</c>
    /// </para>
    /// </remarks>
    public static class HdrpSetup
    {
        private const string Folder = "Assets/Rendering";
        private const string AssetPath = Folder + "/HDRenderPipelineAsset.asset";

        [MenuItem("Evosim/Set Up HDRP")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Rendering");

            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
                Debug.Log("[HdrpSetup] made " + AssetPath);
            }

            RenderPipelineSettings settings = asset.currentPlatformRenderPipelineSettings;
            settings.supportVolumetrics = true;
            settings.supportWater = true;
            settings.supportRayTracing = true;
            if (settings.postProcessQualitySettings != null)
            {
                bool[] physical = settings.postProcessQualitySettings.DoFPhysicallyBased;
                for (int i = 0; i < physical.Length; i++) physical[i] = true;
            }
            asset.currentPlatformRenderPipelineSettings = settings;
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            string[] levels = QualitySettings.names;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < levels.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D12 });
            PlayerSettings.colorSpace = ColorSpace.Linear;

            AssetDatabase.SaveAssets();

            Debug.Log("[HdrpSetup] HDRP asset set for graphics and " + levels.Length +
                      " quality levels; volumetrics, water, ray tracing on; depth of field physically based;" +
                      " Direct3D 12 for Windows; device now " + SystemInfo.graphicsDeviceType +
                      ", ray tracing supported " + SystemInfo.supportsRayTracing);
        }

        /// <summary>
        /// Says, for each of the spike's four graphs, whether Unity found its shader and every error
        /// and warning the shader holds. A shader that fails draws magenta and its error can be
        /// logged once, at the compile, and never again.
        /// </summary>
        [MenuItem("Evosim/Check HDRP Shaders")]
        public static void CheckShaders()
        {
            foreach (string name in new[] { "Evosim/Theatre Body", "Evosim/Theatre Bed", "Evosim/Theatre Rock", "Evosim/Theatre Earth" })
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.Log("[HdrpCheck] " + name + ": NOT FOUND");
                    continue;
                }

                ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
                Debug.Log("[HdrpCheck] " + name + ": supported " + shader.isSupported + ", has error " +
                          ShaderUtil.ShaderHasError(shader) + ", passes " + shader.passCount + ", " +
                          messages.Length + " message(s)");
                foreach (ShaderMessage m in messages)
                    Debug.Log("[HdrpCheck]   " + m.severity + " " + m.platform + " " + m.file + "(" + m.line + "): " + m.message);

                // Every pass compiled once, its fragment program with no keywords, for Direct3D: a
                // pass that fails here is a pass that draws magenta.
                ShaderData data = ShaderUtil.GetShaderData(shader);
                for (int s = 0; s < data.SubshaderCount; s++)
                {
                    ShaderData.Subshader sub = data.GetSubshader(s);
                    for (int p = 0; p < sub.PassCount; p++)
                    {
                        ShaderData.Pass pass = sub.GetPass(p);
                        ShaderData.VariantCompileInfo info = pass.CompileVariant(UnityEditor.Rendering.ShaderType.Fragment,
                            new string[0], UnityEditor.Rendering.ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                        Debug.Log("[HdrpCheck]   subshader " + s + " pass " + p + " '" + pass.Name + "': " +
                                  (info.Success ? "compiles" : "FAILS"));
                        if (!info.Success)
                            foreach (ShaderMessage m in info.Messages)
                                Debug.Log("[HdrpCheck]     " + m.file + "(" + m.line + "): " + m.message);
                    }
                }
            }
        }
    }
}
