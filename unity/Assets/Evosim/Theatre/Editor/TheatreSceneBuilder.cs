using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Generates the theatre scene rather than committing a hand-edited one — the rule the
    /// sandbox scene already follows.
    /// </summary>
    /// <remarks>
    /// Unity scenes are large YAML files that merge badly and cannot be reviewed in a diff. This
    /// one holds a camera, a light and one component, so a script that rebuilds it is both
    /// smaller and readable, and it can be re-run after a change instead of being fixed up by
    /// hand in the Editor.
    /// </remarks>
    public static class TheatreSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Theatre.unity";

        [MenuItem("Evosim/Rebuild Theatre Scene")]
        public static bool Build()
        {
            // SaveScene reports a missing folder by returning false rather than by throwing, and
            // the first version of the sandbox builder logged success while writing nothing
            // (logbook/0004). Both halves of that lesson are kept here.
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;

            // Deep water, so pale creatures read against it — the sandbox's colour, kept so the
            // two views look like the same project.
            camera.backgroundColor = new Color(0.05f, 0.13f, 0.20f);

            // The habitable band is tens of metres deep and the lattice is kilometres wide, so
            // the far plane has to be much further out than a default scene's.
            camera.farClipPlane = 8000f;
            camera.nearClipPlane = 0.05f;
            cameraGo.transform.position = new Vector3(-14f, -6f, -26f);
            cameraGo.transform.rotation = Quaternion.Euler(8f, 28f, 0f);

            var fly = cameraGo.AddComponent<TheatreCamera>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;

            // Down and slightly to one side: the world's light comes from above, and a body lit
            // from the horizon reads as a silhouette.
            lightGo.transform.rotation = Quaternion.Euler(62f, -25f, 0f);

            var runnerGo = new GameObject("Theatre Runner");
            var water = runnerGo.AddComponent<WaterBounds>();
            var runner = runnerGo.AddComponent<TheatreRunner>();
            runner.FlyCamera = fly;
            runner.Water = water;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[Evosim] Failed to save the theatre scene to {ScenePath}.");
                return false;
            }

            AssetDatabase.Refresh();

            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[Evosim] SaveScene reported success but {ScenePath} does not exist.");
                return false;
            }

            Debug.Log($"[Evosim] Theatre scene written to {ScenePath}");
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
