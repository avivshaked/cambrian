using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Draws seven superellipsoid forms (D233) as <see cref="TheatreFamilyMesh"/> builds them, in a plain lit grey, with Core's
    /// own surface points over each as small red spheres, and prints how far the worst of those points stands from the mesh:
    /// the test picture of <c>EVOSIM_THEATRE_FAMILY_MESH</c>, needing no world and no genome.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The forms</b> are (profile, section, taper) = (0.5, 1, 0.25), (0.5, 1, 1), (0.5, 1, 4), (0.5, 0.1, 1), (0.5, 2, 1),
    /// (0.1, 1, 1) and (1, 1, 1), left to right, each at half-extents (0.1, 0.2, 0.1) m and built as Core's tests build one:
    /// <c>FamilyGeometry.For(new SuperellipsoidShape(), form, halfExtents)</c>. The mesh is drawn at its family scale of one,
    /// which is <see cref="TheatreFamilyMesh.ScaleOf(FamilyGeometry, float)"/>.
    /// </para>
    /// <para>
    /// <b>The dots</b> are <c>Superellipsoid.SurfacePointAndNormal</c> on a lattice of 5 by 5 on each cube face, 150 a form, at
    /// cube coordinates -0.787, -0.387, 0.013, 0.413 and 0.813, which are not the mesh's (the mesh's are multiples of 1/12), so
    /// a dot that floats off the mesh or sinks into it shows in the picture and not only in the number. Each is a sphere of
    /// 3 mm across. <b>The number</b> is the largest distance from a dot to the nearest triangle of the mesh as the engine
    /// holds it, in millimetres, one line a form: a flat triangle stands inside a curved surface by its sagitta, so a few
    /// hundredths of a millimetre is the mesh's own faceting and a millimetre is a map or a scale that is wrong.
    /// </para>
    /// <para>
    /// <b>The views</b> are a side view and a three-quarter view, 1600 by 600, orthographic, written to the folder in
    /// <c>EVOSIM_THEATRE_BENCH_OUT</c> as <c>family-mesh-side.png</c> and <c>family-mesh-threequarter.png</c>; without it the
    /// entry refuses. Launch it as <see cref="NosePitStill"/> is launched, with <c>-batchmode -executeMethod
    /// Evosim.Theatre.EditorTools.TheatreShapeBench.Run</c>, without <c>-quit</c> and without <c>-nographics</c>. It exits 0 when
    /// both pictures are written and every form measured.
    /// </para>
    /// </remarks>
    public static class TheatreShapeBench
    {
        private const int Width = 1600;
        private const int Height = 600;
        private const int Supersample = 2;
        private const int Lattice = 5;
        private const float DotDiameter = 0.003f;
        private const float Pitch = 0.45f;
        private const float Margin = 1.08f;
        private static readonly Color Ground = new Color(0.16f, 0.20f, 0.24f, 1f);
        private static readonly Float3 Half = new Float3(0.1f, 0.2f, 0.1f);

        private static readonly PartForm[] Forms =
        {
            new PartForm(0.5f, 1f, 0.25f),
            new PartForm(0.5f, 1f, 1f),
            new PartForm(0.5f, 1f, 4f),
            new PartForm(0.5f, 0.1f, 1f),
            new PartForm(0.5f, 2f, 1f),
            new PartForm(0.1f, 1f, 1f),
            new PartForm(1f, 1f, 1f),
        };

        public static void Run()
        {
            int code = 1;
            try
            {
                code = DrawAll();
            }
            catch (Exception e)
            {
                Debug.LogError("TheatreShapeBench: " + e);
            }

            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static int DrawAll()
        {
            string folder = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_BENCH_OUT");
            if (string.IsNullOrWhiteSpace(folder))
            {
                Debug.LogError("TheatreShapeBench: refusing to run: EVOSIM_THEATRE_BENCH_OUT is not set. " +
                               "Set it to the folder the two pictures are written to.");
                return 1;
            }

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError("TheatreShapeBench: no graphics device; launch without -nographics");
                return 1;
            }

            Directory.CreateDirectory(folder);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.36f, 0.40f, 1f);

            var holder = new GameObject("Shape Bench Camera");
            var scene = new GameObject("Shape Bench Row");
            Material grey = Lit(new Color(0.62f, 0.62f, 0.64f, 1f), 0.25f);
            Material red = Lit(new Color(0.9f, 0.08f, 0.06f, 1f), 0.1f);
            int written = 0;

            try
            {
                var renderers = new List<Renderer>();
                bool measured = true;

                for (int k = 0; k < Forms.Length; k++)
                {
                    PartForm form = Forms[k];
                    FamilyGeometry family = FamilyGeometry.For(new SuperellipsoidShape(), form, Half);
                    Mesh mesh = TheatreFamilyMesh.For(family);
                    Vector3 scale = TheatreFamilyMesh.ScaleOf(family, 1f);
                    var origin = new Vector3((k - 0.5f * (Forms.Length - 1)) * Pitch, 0f, 0f);

                    var body = new GameObject("Form " + k);
                    body.transform.SetParent(scene.transform, false);
                    body.transform.localPosition = origin;
                    body.transform.localScale = scale;
                    body.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer = body.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = grey;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderers.Add(renderer);

                    List<Vector3> dots = CoreDots(family);

                    foreach (Vector3 dot in dots)
                    {
                        var ball = new GameObject("Dot");
                        ball.transform.SetParent(scene.transform, false);
                        ball.transform.localPosition = origin + dot;
                        ball.transform.localScale = Vector3.one * DotDiameter;
                        ball.AddComponent<MeshFilter>().sharedMesh = TheatreMeshes.BuiltinSphere();
                        MeshRenderer dotRenderer = ball.AddComponent<MeshRenderer>();
                        dotRenderer.sharedMaterial = red;
                        dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }

                    float worst = Worst(mesh, scale, dots, out int triangles);
                    if (float.IsNaN(worst) || float.IsInfinity(worst)) measured = false;

                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "TheatreShapeBench: profile {0} section {1} taper {2}: {3} dots, worst {4:0.0000} mm from the mesh ({5} triangles, {6} vertices)",
                        form.Profile, form.Section, form.Taper, dots.Count, worst * 1000f, triangles, mesh.vertexCount));
                }

                Camera cam = holder.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Ground;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.orthographic = true;
                cam.cullingMask = ~0;
                cam.enabled = false;
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;

                Bounds all = renderers[0].bounds;
                for (int i = 1; i < renderers.Count; i++) all.Encapsulate(renderers[i].bounds);

                var views = new List<(string name, Vector3 bearing)>
                {
                    ("side", Vector3.forward),
                    ("threequarter", new Vector3(-0.78f, -0.34f, 1f)),
                };

                foreach (var view in views)
                {
                    Quaternion rotation = Quaternion.LookRotation(view.bearing.normalized, Vector3.up);
                    Quaternion inverse = Quaternion.Inverse(rotation);
                    float halfWide = 0f, halfHigh = 0f, reachZ = 0f;

                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 e = all.extents;
                        var corner = new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                        Vector3 c = inverse * corner;
                        halfWide = Mathf.Max(halfWide, Mathf.Abs(c.x));
                        halfHigh = Mathf.Max(halfHigh, Mathf.Abs(c.y));
                        reachZ = Mathf.Max(reachZ, Mathf.Abs(c.z));
                    }

                    float stand = reachZ + 1f;
                    cam.transform.SetPositionAndRotation(all.center - rotation * Vector3.forward * stand, rotation);
                    cam.orthographicSize = Margin * Mathf.Max(halfHigh, halfWide * Height / Width);
                    cam.nearClipPlane = 0.05f;
                    cam.farClipPlane = stand + reachZ + 1f;

                    var lamp = new GameObject("Shape Bench Light");
                    lamp.transform.rotation = rotation * Quaternion.Euler(30f, 25f, 0f);
                    Light light = lamp.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1.1f;
                    light.shadows = LightShadows.None;

                    try
                    {
                        string path = Path.Combine(folder, "family-mesh-" + view.name + ".png");
                        Render(cam, path);
                        Debug.Log("TheatreShapeBench: wrote " + path);
                        written++;
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(lamp);
                    }
                }

                if (!measured) Debug.LogError("TheatreShapeBench: a distance came back as not a number");
                return written == views.Count && measured ? 0 : 1;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Object.DestroyImmediate(scene);
                UnityEngine.Object.DestroyImmediate(grey);
                UnityEngine.Object.DestroyImmediate(red);
                TheatreFamilyMesh.Release();
            }
        }

        /// <summary>Core's own surface points on a lattice of <see cref="Lattice"/> by <see cref="Lattice"/> a face, in the part's frame, metres.</summary>
        private static List<Vector3> CoreDots(FamilyGeometry family)
        {
            var dots = new List<Vector3>(6 * Lattice * Lattice);
            Float3 reference = family.ReferenceHalfExtents;

            for (int axis = 0; axis < 3; axis++)
            {
                for (int side = 0; side < 2; side++)
                {
                    double sign = side == 0 ? 1.0 : -1.0;

                    for (int i = 0; i < Lattice; i++)
                    {
                        for (int j = 0; j < Lattice; j++)
                        {
                            double u = -1.0 + (2 * i + 1) / (double)Lattice + 0.013;
                            double v = -1.0 + (2 * j + 1) / (double)Lattice + 0.013;
                            double x = 0.0, y = 0.0, z = 0.0;
                            Put(ref x, ref y, ref z, axis, sign);
                            Put(ref x, ref y, ref z, (axis + 1) % 3, u);
                            Put(ref x, ref y, ref z, (axis + 2) % 3, v);

                            Double3 p = family.Geometry.SurfacePointAndNormal(new Double3(x, y, z), reference, out _);
                            dots.Add(new Vector3((float)p.X, (float)p.Y, (float)p.Z));
                        }
                    }
                }
            }

            return dots;
        }

        private static void Put(ref double x, ref double y, ref double z, int axis, double value)
        {
            if (axis == 0) x = value;
            else if (axis == 1) y = value;
            else z = value;
        }

        /// <summary>
        /// The largest distance, over the dots, to the nearest triangle of the mesh as the engine holds it, drawn at <paramref name="scale"/>.
        /// </summary>
        private static float Worst(Mesh mesh, Vector3 scale, List<Vector3> dots, out int triangleCount)
        {
            Vector3[] raw = mesh.vertices;
            int[] indices = mesh.triangles;
            var vertices = new Vector3[raw.Length];

            for (int i = 0; i < raw.Length; i++) vertices[i] = Vector3.Scale(raw[i], scale);

            triangleCount = indices.Length / 3;
            float worst = 0f;

            foreach (Vector3 dot in dots)
            {
                float nearest = float.PositiveInfinity;

                for (int t = 0; t < indices.Length; t += 3)
                {
                    nearest = Mathf.Min(
                        nearest, Distance(dot, vertices[indices[t]], vertices[indices[t + 1]], vertices[indices[t + 2]]));
                }

                worst = Mathf.Max(worst, nearest);
            }

            return worst;
        }

        /// <summary>The distance from a point to a triangle: Ericson's closest point (Real-Time Collision Detection, 5.1.5).</summary>
        private static float Distance(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return (p - a).magnitude;

            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return (p - b).magnitude;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                return (p - (a + v * ab)).magnitude;
            }

            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return (p - c).magnitude;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                return (p - (a + w * ac)).magnitude;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return (p - (b + w * (c - b))).magnitude;
            }

            float denom = 1f / (va + vb + vc);
            float vv = vb * denom;
            float ww = vc * denom;
            return (p - (a + ab * vv + ac * ww)).magnitude;
        }

        private static Material Lit(Color colour, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Theatre Shape Bench" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            return material;
        }

        private static void Render(Camera cam, string path)
        {
            int w = Width * Supersample;
            int h = Height * Supersample;
            var target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "Theatre Shape Bench", antiAliasing = 1 };
            var full = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var still = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            RenderTexture active = RenderTexture.active;
            try
            {
                cam.targetTexture = target;
                cam.Render();
                RenderTexture.active = target;
                full.ReadPixels(new Rect(0f, 0f, w, h), 0, 0, false);
                full.Apply(false);
                still.SetPixels32(BoxDown(full.GetPixels32(), Width, Height, Supersample));
                still.Apply(false);
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
        private static Color32[] BoxDown(Color32[] big, int width, int height, int s)
        {
            var down = new Color32[width * height];
            int n = width * s;
            int area = s * s;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
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

                    down[y * width + x] = new Color32((byte)(r / area), (byte)(g / area), (byte)(b / area), 255);
                }
            }

            return down;
        }
    }
}
