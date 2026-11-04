using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Draws a hand-made body with a nose, from three views, as PNGs in the theatre's skin: the test picture of the nose
    /// pit (<c>EVOSIM_THEATRE_NOSE_PIT</c>, D242), made before any recorded world carries a nose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The body</b> is a structural root of 0.2 by 0.12 by 0.12 m and a nose of 0.06 by 0.03 by 0.03 m hung on the
    /// root's +x face by a fixed edge, as the nose tests' <c>WithANose</c> and <c>Snout</c> do. It is developed with the
    /// nose table (<c>CellTypeRegistry.Standard.Appending(new NoseCell())</c>) and refused, with a message that says so,
    /// when that table does not hold the nose or does not take the body.
    /// </para>
    /// <para>
    /// <b>The path</b> is <see cref="TheatreSoloStill"/>'s, which is the simplest one that puts a developed phenotype on
    /// screen with the theatre's palette and skin: edit mode, an empty scene, <c>PhenotypeBuilder.Build</c>, the palette's
    /// <c>Paint</c> and one camera, with no world, no physics step and no recorded run, which a nosed body does not yet
    /// have. Its recipe is repeated here and not called, because its helpers are private and it reads a genome row from a
    /// file; this entry builds the genome in code. The skin is <c>TheatreLook.Off</c> and the frame has no grade, as a solo
    /// still's, so the two settings of the dial differ in the pit alone. The background is opaque grey-blue so the
    /// outline reads.
    /// </para>
    /// <para>
    /// <b>The views,</b> three to a launch: <c>threequarter</c>, the whole body from the solo still's bearing;
    /// <c>side</c>, the whole body along its z axis, the nose to the right; and <c>tip</c>, a close view straight down the
    /// nose's axis onto its end face. The dial is read once at start (a static), so the two settings of it are two
    /// launches, and the files carry the setting in their names: <c>nose-pit-dial0-side.png</c>, <c>nose-pit-dial1-tip.png</c>.
    /// Launch it with <c>-batchmode -executeMethod Evosim.Theatre.EditorTools.NosePitStill.Run</c>, without <c>-quit</c>
    /// (it exits itself) and without <c>-nographics</c> (a picture needs a graphics device), with
    /// <c>EVOSIM_THEATRE_NOSE_PIT</c> set to 0 or 1. The pictures go to <c>scratch/nose-pit/</c> under the repository
    /// that holds the project, or to <c>EVOSIM_NOSE_PIT_OUT</c>. <c>EVOSIM_NOSE_PIT_SIZE</c> sets their side in pixels
    /// (900).
    /// </para>
    /// </remarks>
    public static class NosePitStill
    {
        private const int Supersample = 2;
        private const float FieldOfView = 28f;
        private const float Margin = 1.12f;
        private static readonly Vector3 ThreeQuarter = new Vector3(-0.78f, -0.34f, 1f);
        private static readonly Color Ground = new Color(0.16f, 0.20f, 0.24f, 1f);

        public static void Run()
        {
            int code = 1;
            try
            {
                code = DrawAll();
            }
            catch (Exception e)
            {
                Debug.LogError("NosePitStill: " + e);
            }

            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static int DrawAll()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError("NosePitStill: no graphics device; launch without -nographics");
                return 1;
            }

            // The nose table, or no picture. The nose is not in the standard table; a world that wants it appends it.
            CellTypeRegistry table = CellTypeRegistry.Standard.Appending(new NoseCell());
            if (!table.Ids().Contains(CellTypeIds.Nose))
            {
                Debug.LogError("NosePitStill: refusing to run: the cell type table has no '" + CellTypeIds.Nose +
                               "', so there is no nose to draw. Build with the nose table " +
                               "(CellTypeRegistry.Standard.Appending(new NoseCell())).");
                return 1;
            }

            // The nose is 54 cubic centimetres, under the default floor on a part's volume (1e-4), so the floor comes down.
            var config = new RunConfig { CellTypes = table, Development = new DevelopmentLimits { MinPartVolume = 1e-5f } };

            Phenotype phenotype;
            try
            {
                phenotype = Developer.Develop(HandMade(), config.Development, null, config.Shapes, cellTypes: table);
            }
            catch (ArgumentException e)
            {
                Debug.LogError("NosePitStill: refusing to run: the nose table does not take the hand-made body: " + e.Message);
                return 1;
            }

            if (phenotype.PartCount != 2 || phenotype.Parts[1].CellTypeId != CellTypeIds.Nose)
            {
                Debug.LogError("NosePitStill: refusing to run: the body developed to " + phenotype.PartCount +
                               " parts and part 1 is '" + (phenotype.PartCount > 1 ? phenotype.Parts[1].CellTypeId : "none") +
                               "', not a nose (a part under the floors is pruned with its subtree).");
                return 1;
            }

            int size = 900;
            string sizeText = Environment.GetEnvironmentVariable("EVOSIM_NOSE_PIT_SIZE");
            if (!string.IsNullOrEmpty(sizeText)) int.TryParse(sizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
            size = Mathf.Clamp(size, 128, 2048);

            string folder = Environment.GetEnvironmentVariable("EVOSIM_NOSE_PIT_OUT");
            if (string.IsNullOrEmpty(folder))
            {
                // Application.dataPath is <repository>/unity/Assets.
                folder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..")), "scratch", "nose-pit");
            }

            Directory.CreateDirectory(folder);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var holder = new GameObject("Nose Pit Still Camera");
            Camera cam = holder.AddComponent<Camera>();
            var skin = new TheatreSkin { Look = TheatreLook.Off };
            skin.Apply(cam);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Ground;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.fieldOfView = FieldOfView;
            cam.cullingMask = ~0;
            cam.enabled = false;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;

            int drawn = 0;
            CreatureInstance body = PhenotypeBuilder.Build(phenotype, Vector3.zero, null, config.Shapes);
            try
            {
                var palette = new TheatrePalette { Skin = skin };
                palette.Paint(1, body.Root.transform, phenotype, 1f, true);

                string dial = TheatreNosePit.Enabled ? "1" : "0";
                TheatreNosePit.Measured pit = TheatreNosePit.Measure(phenotype, 1);
                Debug.Log("NosePitStill: dial " + dial + ", nose tip " + pit.Tip.ToString("0.0000") + " m in the part's frame, " +
                          "pit radius " + (pit.RadiusMetres * 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " mm, depth " +
                          (pit.DepthMetres * 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " mm, nose length " +
                          (pit.LengthMetres * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " mm");

                Bounds all = BoundsOf(body.Root);
                Transform nose = body.Bodies[1].transform;
                Vector3 axis = nose.TransformDirection(pit.Tip.normalized);
                Vector3 tip = nose.TransformPoint(pit.Tip);

                var views = new List<(string name, Vector3 position, Quaternion rotation, float near, float far)>();
                views.Add(Whole("threequarter", all, ThreeQuarter));
                views.Add(Whole("side", all, Vector3.forward));

                // Straight down the nose's axis: the camera on the axis beyond the tip, looking back along it, close
                // enough that the end face (a little over 0.03 m) fills the frame.
                float tan = Mathf.Tan(0.5f * FieldOfView * Mathf.Deg2Rad);
                float stand = 0.03f / tan;
                Quaternion down = Quaternion.LookRotation(-axis, Vector3.up);
                views.Add(("tip", tip + axis * stand, down, 0.01f * stand, stand + 1f));

                foreach (var view in views)
                {
                    cam.transform.SetPositionAndRotation(view.position, view.rotation);
                    cam.nearClipPlane = view.near;
                    cam.farClipPlane = view.far;
                    skin.Aim(cam.transform.rotation);

                    Light back = BackLight(cam);
                    try
                    {
                        string path = Path.Combine(folder, "nose-pit-dial" + dial + "-" + view.name + ".png");
                        Render(cam, size, path);
                        Debug.Log("NosePitStill: wrote " + path);
                        drawn++;
                    }
                    finally
                    {
                        if (back != null) UnityEngine.Object.DestroyImmediate(back.gameObject);
                    }
                }
            }
            finally
            {
                body.Destroy();
                UnityEngine.Object.DestroyImmediate(holder);
            }

            Debug.Log("NosePitStill: " + drawn + " of 3 drawn");
            return drawn == 3 ? 0 : 1;
        }

        /// <summary>The hand-made body: a root and a nose on its +x face, both structural in all but the nose's type.</summary>
        private static Genome HandMade()
        {
            var g = new Genome { RootIndex = 0, AdultScale = 1f };
            g.Nodes.Add(Node(new Float3(0.10f, 0.06f, 0.06f), CellTypeIds.Structural));
            g.Nodes.Add(Node(new Float3(0.03f, 0.015f, 0.015f), CellTypeIds.Nose));
            g.Nodes[0].Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });
            return g;
        }

        private static MorphNode Node(Float3 half, string cellType) => new MorphNode
        {
            Dimensions = half,
            CellTypeId = cellType,
            ShapeId = ShapeIds.Box,
            JointType = Evosim.Core.JointType.Fixed,
            JointLimits = Array.Empty<Float2>(),
            RecursiveLimit = 1,
        };

        private static Bounds BoundsOf(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("the body has no renderer to frame");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>A view of the whole body from a bearing, at the distance that holds its box in a square frame.</summary>
        private static (string name, Vector3 position, Quaternion rotation, float near, float far) Whole(
            string name, Bounds bounds, Vector3 bearing)
        {
            Quaternion rotation = Quaternion.LookRotation(bearing.normalized, Vector3.up);
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
            return (name, bounds.center - forward * standoff, rotation, Mathf.Max(0.001f, 0.02f * standoff), standoff + 4f * reachZ + 10f);
        }

        /// <summary>The close view's back light (<c>TheatreSoloStill</c>'s), for this camera.</summary>
        private static Light BackLight(Camera cam)
        {
            var holder = new GameObject("Nose Pit Still Back Light");
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
            var target = new RenderTexture(n, n, 24, RenderTextureFormat.ARGB32) { name = "Nose Pit Still", antiAliasing = 1 };
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

        /// <summary>Each s-by-s block as one pixel, its colour the mean of the block's; the background is opaque.</summary>
        private static Color32[] BoxDown(Color32[] big, int size, int s)
        {
            var down = new Color32[size * size];
            int n = size * s;
            int area = s * s;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int dy = 0; dy < s; dy++)
                    {
                        for (int dx = 0; dx < s; dx++)
                        {
                            Color32 p = big[(y * s + dy) * n + x * s + dx];
                            r += p.r;
                            g += p.g;
                            b += p.b;
                        }
                    }

                    down[y * size + x] = new Color32((byte)(r / area), (byte)(g / area), (byte)(b / area), 255);
                }
            }

            return down;
        }
    }
}
