using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;
using Evosim.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Draws single bodies, each grown from a genome row, as square PNGs with a transparent
    /// background in the theatre's skin: the stills a story film's graphics set beside their
    /// charts (<c>.claude/skills/create-story-video/graphics.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// One batch launch draws a list. <c>EVOSIM_STILLS_LIST</c> names a JSON file whose items carry
    /// <c>name</c>, <c>genome_file</c> (one snapshot row), <c>run_dir</c> (whose config grew the
    /// body), <c>png_file</c> and <c>size</c>; the entry writes <c>result.json</c> beside it and
    /// exits 0 when every item was drawn. Launch it with <c>-batchmode -executeMethod
    /// Evosim.Theatre.EditorTools.TheatreSoloStill.Run</c>, without <c>-quit</c> (it exits itself)
    /// and without <c>-nographics</c> (a picture needs a graphics device).
    /// </para>
    /// <para>
    /// Edit mode, an empty scene, no physics step and no brain: a still is a body's shape, which
    /// the genome, the run's development limits and the row's plan fields decide
    /// (<c>SnapshotWorld.DevelopWithPlan</c>'s rule, repeated here). It is drawn from the close
    /// view's bearing with the close view's back light, and without the grade: URP drops a
    /// camera's alpha when post-processing is on and the project's pipeline asset does not allow
    /// alpha output, so a still has no tonemapping, bloom or vignette. Its supersampled pixels are
    /// averaged with their alpha, which the theatre's own box filters set to one.
    /// </para>
    /// </remarks>
    public static class TheatreSoloStill
    {
        private const int Supersample = 2;
        private const float FieldOfView = 28f;
        private const float Margin = 1.12f;
        private static readonly Vector3 Bearing = new Vector3(-0.78f, -0.34f, 1f);

        [Serializable]
        private sealed class Item
        {
            public string name;
            public string genome_file;
            public string run_dir;
            public string png_file;
            public int size;
        }

        [Serializable]
        private sealed class Listing
        {
            public string schema;
            public Item[] items;
        }

        [Serializable]
        private sealed class Outcome
        {
            public string name;
            public bool drawn;
            public string why;
            public int parts;
            public float reach;
        }

        [Serializable]
        private sealed class Result
        {
            public Outcome[] items;
        }

        public static void Run()
        {
            int code = 1;
            try
            {
                code = DrawAll();
            }
            catch (Exception e)
            {
                Debug.LogError("TheatreSoloStill: " + e);
            }

            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static int DrawAll()
        {
            string listPath = Environment.GetEnvironmentVariable("EVOSIM_STILLS_LIST");
            if (string.IsNullOrEmpty(listPath) || !File.Exists(listPath))
            {
                Debug.LogError("TheatreSoloStill: EVOSIM_STILLS_LIST names no file");
                return 1;
            }

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError("TheatreSoloStill: no graphics device; launch without -nographics");
                return 1;
            }

            Listing listing = JsonUtility.FromJson<Listing>(File.ReadAllText(listPath));
            float wall = 30f;
            string wallText = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_WALL_MINUTES");
            if (!string.IsNullOrEmpty(wallText)) float.TryParse(wallText, NumberStyles.Float, CultureInfo.InvariantCulture, out wall);
            DateTime deadline = DateTime.UtcNow.AddMinutes(wall);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var holder = new GameObject("Solo Still Camera");
            Camera cam = holder.AddComponent<Camera>();
            var skin = new TheatreSkin();
            skin.Apply(cam);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.fieldOfView = FieldOfView;
            cam.cullingMask = ~0;
            cam.enabled = false;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;

            var outcomes = new List<Outcome>();
            int failed = 0;
            foreach (Item item in listing.items ?? new Item[0])
            {
                var outcome = new Outcome { name = item.name };
                if (DateTime.UtcNow > deadline)
                {
                    outcome.why = "the wall of " + wall.ToString(CultureInfo.InvariantCulture) + " minutes passed first";
                }
                else
                {
                    try
                    {
                        DrawOne(item, skin, cam, outcome);
                        outcome.drawn = true;
                    }
                    catch (Exception e)
                    {
                        outcome.why = e.GetType().Name + ": " + e.Message;
                    }
                }

                if (!outcome.drawn) failed++;
                Debug.Log("TheatreSoloStill: " + item.name + (outcome.drawn ? " drawn, " + outcome.parts + " parts" : " not drawn: " + outcome.why));
                outcomes.Add(outcome);
            }

            UnityEngine.Object.DestroyImmediate(holder);
            string resultPath = Path.Combine(Path.GetDirectoryName(listPath) ?? ".", "result.json");
            File.WriteAllText(resultPath, JsonUtility.ToJson(new Result { items = outcomes.ToArray() }, true));
            Debug.Log("TheatreSoloStill: " + (outcomes.Count - failed) + " of " + outcomes.Count + " drawn");
            return failed == 0 ? 0 : 1;
        }

        private static void DrawOne(Item item, TheatreSkin skin, Camera cam, Outcome outcome)
        {
            string row = File.ReadAllText(item.genome_file).Trim();
            RunConfig config = ReadConfig(item.run_dir);
            Genome genome = ReadGenome(row);
            Phenotype phenotype = Develop(genome, config, GenomeJson.ReadModuleCounts(row), GenomeJson.ReadLostPartPaths(row));
            outcome.parts = phenotype.PartCount;
            outcome.reach = SnapshotCamera.ReachOf(phenotype);

            CreatureInstance body = PhenotypeBuilder.Build(phenotype, Vector3.zero, null, config.Shapes);
            Light back = null;
            try
            {
                var palette = new TheatrePalette { Skin = skin };
                palette.Paint(1, body.Root.transform, phenotype, 1f, true);
                Frame(cam, BoundsOf(body.Root));
                skin.Aim(cam.transform.rotation);
                back = BackLight(cam);
                Render(cam, item.size, item.png_file);
            }
            finally
            {
                if (back != null) UnityEngine.Object.DestroyImmediate(back.gameObject);
                body.Destroy();
            }
        }

        private static RunConfig ReadConfig(string runDirectory)
        {
            try
            {
                return RunDirectory.ReadConfig(runDirectory, out _);
            }
            catch (Exception)
            {
                return PictureConfig.Read(runDirectory, out _);
            }
        }

        private static Genome ReadGenome(string row)
        {
            try
            {
                return GenomeJson.Read(row);
            }
            catch (Exception)
            {
                return PictureGenome.Read(row, out _);
            }
        }

        /// <summary>The body the row's plan grew: <c>SnapshotWorld.DevelopWithPlan</c>'s rule.</summary>
        private static Phenotype Develop(Genome genome, RunConfig config, int[] counts, List<int[]> lost)
        {
            if (counts == null && lost == null) return Developer.Develop(genome, config.Development, null, config.Shapes);

            var paths = new List<int[]>();
            Phenotype phenotype = Developer.Develop(genome, config.Development, null, config.Shapes, counts, paths);
            if (lost == null || lost.Count == 0) return phenotype;

            var drop = new bool[phenotype.PartCount];
            bool any = false;
            for (int i = 0; i < phenotype.PartCount; i++)
            {
                foreach (int[] path in lost)
                {
                    if (!SamePath(paths[i], path)) continue;
                    drop[i] = true;
                    any = true;
                    break;
                }
            }

            return any ? phenotype.WithoutSubtrees(drop, out _) : phenotype;
        }

        private static bool SamePath(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static Bounds BoundsOf(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("the body has no renderer to frame");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>The close view's bearing, at the distance that holds the whole box in a square frame.</summary>
        private static void Frame(Camera cam, Bounds bounds)
        {
            Quaternion rotation = Quaternion.LookRotation(Bearing.normalized, Vector3.up);
            Quaternion inverse = Quaternion.Inverse(rotation);
            float tan = Mathf.Tan(0.5f * FieldOfView * Mathf.Deg2Rad);
            float need = 0f, reachZ = 0f;
            Vector3 half = bounds.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
                Vector3 c = inverse * corner;
                reachZ = Mathf.Max(reachZ, Mathf.Abs(c.z));
                need = Mathf.Max(need, Mathf.Abs(c.y) / tan - c.z);
                need = Mathf.Max(need, Mathf.Abs(c.x) / tan - c.z);
            }

            float standoff = Margin * Mathf.Max(need, reachZ + 0.01f);
            Vector3 forward = rotation * Vector3.forward;
            cam.transform.SetPositionAndRotation(bounds.center - forward * standoff, rotation);
            cam.nearClipPlane = Mathf.Max(0.001f, 0.02f * standoff);
            cam.farClipPlane = standoff + 4f * reachZ + 10f;
        }

        /// <summary>The close view's back light (<c>SnapshotCamera.BackLight</c>), for this camera.</summary>
        private static Light BackLight(Camera cam)
        {
            var holder = new GameObject("Solo Still Back Light");
            Vector3 towardsCamera = -cam.transform.forward;
            holder.transform.rotation = Quaternion.LookRotation((towardsCamera + Vector3.down * 0.45f).normalized, Vector3.up);
            Light light = holder.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.85f, 1f);
            float intensity = 2.4f;
            string dial = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_BACK");
            if (!string.IsNullOrEmpty(dial)) float.TryParse(dial, NumberStyles.Float, CultureInfo.InvariantCulture, out intensity);
            light.intensity = Mathf.Clamp(intensity, 0f, 12f);
            light.shadows = LightShadows.None;
            return light;
        }

        private static void Render(Camera cam, int size, string path)
        {
            int n = size * Supersample;
            var target = new RenderTexture(n, n, 24, RenderTextureFormat.ARGB32) { name = "Solo Still", antiAliasing = 1 };
            var full = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var still = new Texture2D(size, size, TextureFormat.RGBA32, false);
            RenderTexture active = RenderTexture.active;
            try
            {
                cam.targetTexture = target;
                cam.Render();
                RenderTexture.active = target;
                full.ReadPixels(new Rect(0f, 0f, n, n), 0, 0, false);
                full.Apply(false);
                still.SetPixels32(BoxDown(full.GetPixels32(), size, Supersample));
                still.Apply(false);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllBytes(path, still.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active;
                cam.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(full);
                UnityEngine.Object.DestroyImmediate(still);
            }
        }

        /// <summary>Each s-by-s block as one pixel, its colour weighted by its alpha so a clear background adds no black.</summary>
        private static Color32[] BoxDown(Color32[] big, int size, int s)
        {
            var down = new Color32[size * size];
            int n = size * s;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;
                    for (int dy = 0; dy < s; dy++)
                    {
                        for (int dx = 0; dx < s; dx++)
                        {
                            Color32 p = big[(y * s + dy) * n + x * s + dx];
                            r += p.r * p.a;
                            g += p.g * p.a;
                            b += p.b * p.a;
                            a += p.a;
                        }
                    }

                    down[y * size + x] = a == 0
                        ? new Color32(0, 0, 0, 0)
                        : new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / (s * s)));
                }
            }

            return down;
        }
    }
}
