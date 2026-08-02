using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// Generates the sandbox scene rather than committing a hand-edited one.
    /// </summary>
    /// <remarks>
    /// Unity scenes are large YAML files that merge badly and cannot be reviewed in a diff.
    /// This scene contains a camera, a light and one component, so a script that rebuilds it
    /// is both smaller and readable — and it can be re-run headlessly after a change instead
    /// of being fixed up by hand in the Editor.
    /// </remarks>
    public static class SandboxSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Sandbox.unity";

        [MenuItem("Evosim/Rebuild Sandbox Scene")]
        public static bool Build()
        {
            // SaveScene fails if the target folder does not exist. It reports that by
            // returning false, not by throwing — so the folder is created first AND the
            // return value is checked below. The first version of this script did neither
            // and logged success while writing nothing (logbook/0004).
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Deep water, so pale creatures read against it.
            camera.backgroundColor = new Color(0.05f, 0.13f, 0.20f);
            camera.transform.position = new Vector3(0f, 2.5f, -7f);
            var follow = cameraGo.AddComponent<FollowCamera>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var spawnerGo = new GameObject("Creature Spawner");
            var spawner = spawnerGo.AddComponent<CreatureSpawner>();
            spawner.Camera = follow;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[Evosim] Failed to save the sandbox scene to {ScenePath}.");
                return false;
            }

            AssetDatabase.Refresh();

            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[Evosim] SaveScene reported success but {ScenePath} does not exist.");
                return false;
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };

            Debug.Log($"[Evosim] Sandbox scene written to {ScenePath}");
            return true;
        }

        /// <summary>Batchmode entry point.</summary>
        public static void Run()
        {
            bool ok = Build();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
