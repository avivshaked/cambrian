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

        /// <summary>
        /// <c>EVOSIM_STILLS_FROM=mouth</c>: each still that has an eating part is drawn facing its main mouth (the largest eating
        /// part by the product of its half extents, as <c>TheatreMouth2.MouthKind</c> takes it), the eye <see cref="MouthTurn"/> of
        /// the way from the mouth's outward axis toward the usual bearing, framing that part alone, so the mouth's kind can be seen.
        /// A body with no eating part, or a mouth at its part's centre, is drawn as usual. Unset, every still is as before.
        /// <c>EVOSIM_STILLS_FROM=mouthclose</c> faces it the same way and frames a box <see cref="MouthCloseReach"/> mouth radii
        /// (<c>TheatreMouth2.Measure</c>) each way from the mouth point instead of the part, so a mouth on a thin face, whose
        /// radius follows that face's smaller half extent, fills the still.
        /// </summary>
        private static readonly string StillsFrom = Environment.GetEnvironmentVariable("EVOSIM_STILLS_FROM");
        private static readonly bool FromMouth = StillsFrom == "mouth" || StillsFrom == "mouthclose";
        private static readonly bool MouthClose = StillsFrom == "mouthclose";

        /// <summary>
        /// <c>EVOSIM_STILLS_FRAME_SAVE=&lt;file&gt;</c>: each still's frame (the box the camera holds) and bearing are written there, one
        /// line a still. <c>EVOSIM_STILLS_FRAME_LOAD=&lt;file&gt;</c>: a still whose name has a line there is framed from it instead of
        /// its own renderers, so two looks whose meshes differ in size (NO_BOX 0 and 1) are drawn from the same camera.
        /// </summary>
        private static readonly string FrameSave = Environment.GetEnvironmentVariable("EVOSIM_STILLS_FRAME_SAVE");
        private static readonly string FrameLoad = Environment.GetEnvironmentVariable("EVOSIM_STILLS_FRAME_LOAD");

        /// <summary>The share of the way from facing the mouth head on toward the usual bearing, so its rim and depth read.</summary>
        private const float MouthTurn = 0.3f;

        /// <summary>The close frame's half size in mouth radii: the crown's lobes reach 2.6, and a little body shows round them.</summary>
        private const float MouthCloseReach = 3.2f;

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
            // A solo still is a record, a cut-out compared with others like it, and keeps today's
            // look whatever the underwater look's dials say (logbook/specs/underwater-look/brief.md).
            var skin = new TheatreSkin { Look = TheatreLook.Off };
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
            TheatreOneSkin oneSkin = null; // NOBOX
            try
            {
                var palette = new TheatrePalette { Skin = skin };

                // The genome-to-skin map (EVOSIM_THEATRE_SKIN_MAP): the body is painted under its own id, so its vector is its
                // own and not the first still's, and its genome is registered. With the dial off every still is painted as id 1,
                // unless EVOSIM_STILLS_OWN_ID=1 paints it under its own id too: the id seeds the carve, so a sheet of the map off
                // against on compares like with like only when both runs paint under the same id.
                long paintId = 1;
                if (TheatrePalette.SkinMapOn || Environment.GetEnvironmentVariable("EVOSIM_STILLS_OWN_ID") == "1")
                {
                    long rowId = GenomeJson.ReadId(row);
                    if (rowId != GenomeJson.NoId) paintId = rowId;
                }
                if (TheatrePalette.SkinMapOn) TheatrePalette.RegisterForSkinMap(paintId, genome, config);

                // NOBOX begin
                // EVOSIM_THEATRE_NO_BOX: the film's one skin on the still, built from the body's part transforms and visuals before the
                // first paint and dressed after it, as LiveWorldView does. Null with the rule off or the skin's dial off.
                if (TheatreNoBox.Enabled && body.Bodies.Length == phenotype.PartCount && body.Renderers.Length > 0)
                {
                    var partTransforms = new Transform[body.Bodies.Length];
                    for (int partAt = 0; partAt < partTransforms.Length; partAt++)
                    {
                        partTransforms[partAt] = body.Bodies[partAt] != null ? body.Bodies[partAt].transform : null;
                    }

                    oneSkin = TheatreOneSkin.Build(
                        paintId, body.Root.transform, partTransforms, TheatreNoBox.VisualsByPart(partTransforms), phenotype, phenotype,
                        config.Shapes, body.Renderers[0].sharedMaterial, palette);
                }
                // NOBOX end

                palette.Paint(paintId, body.Root.transform, phenotype, 1f, true);
                if (oneSkin != null) oneSkin.Dress(palette, paintId); // NOBOX

                // The main mouth, its kind as painted (TheatreMouth2's code in _MouthRadii.w, stripes + 16 * kind) logged for the
                // sheet's labels, and with EVOSIM_STILLS_FROM=mouth the frame turned to face it.
                int mouthPart = MainMouthPart(phenotype);
                Bounds frame = BoundsOf(body.Root);
                Vector3 bearing = Bearing;
                if (mouthPart >= 0)
                {
                    LogMouthKind(paintId, body, mouthPart);
                    if (FromMouth) FaceMouth(body, phenotype, mouthPart, ref frame, ref bearing);
                }

                PinFrame(item.name, ref frame, ref bearing);
                Frame(cam, frame, bearing);
                if (FromMouth && mouthPart >= 0) LogMouthGeometry(paintId, body, phenotype, mouthPart, cam);
                LogParts(paintId, body, phenotype);
                skin.Aim(cam.transform.rotation);
                back = BackLight(cam);
                Render(cam, item.size, item.png_file);
            }
            finally
            {
                if (back != null) UnityEngine.Object.DestroyImmediate(back.gameObject);
                if (oneSkin != null) oneSkin.Release(); // NOBOX
                body.Destroy();
            }
        }

        private static RunConfig ReadConfig(string runDirectory)
        {
            try
            {
                return RunRecord.ReadConfigFillingProvenOff(runDirectory, out _);
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
            if (counts == null && lost == null) return Developer.Develop(genome, config.Development, null, config.Shapes, cellTypes: config.CellTypes);

            var paths = new List<int[]>();
            Phenotype phenotype = Developer.Develop(genome, config.Development, null, config.Shapes, counts, paths, config.CellTypes);
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

        /// <summary>The largest eating part by the product of its half extents, or -1 for a body with none.</summary>
        private static int MainMouthPart(Phenotype phenotype)
        {
            int main = -1;
            float largest = -1f;
            for (int i = 0; i < phenotype.PartCount; i++)
            {
                PhenotypePart p = phenotype.Parts[i];
                if (!MouthPoint.IsEatingPart(p)) continue;
                float volume = Mathf.Abs(p.HalfExtents.X * p.HalfExtents.Y * p.HalfExtents.Z);
                if (volume > largest) { largest = volume; main = i; }
            }
            return main;
        }

        /// <summary>The main mouth's kind as painted, from its first renderer's <c>_MouthRadii.w</c>: one line per still.</summary>
        private static void LogMouthKind(long id, CreatureInstance body, int part)
        {
            var block = new MaterialPropertyBlock();
            for (int r = 0; r < body.Renderers.Length; r++)
            {
                if (body.RendererPart[r] != part) continue;
                body.Renderers[r].GetPropertyBlock(block);
                float w = block.GetVector("_MouthRadii").w;
                int kind = w >= 16f ? Mathf.FloorToInt(w / 16f) : 0;
                Debug.Log(string.Format(CultureInfo.InvariantCulture, "TheatreSoloStill: id {0} mouth part {1} code {2:0} kind {3} stripes {4:0}",
                    id, part, w, kind, w - 16f * kind));
                return;
            }
        }

        /// <summary>
        /// With <c>EVOSIM_STILLS_PART_LOG=1</c>, one line per renderer as painted: its part, cell and shape, the shader, and the block's
        /// colours and link values, so a part drawn wrong (the flat white link of 2026-10-31) can be told from its neighbours by number.
        /// </summary>
        private static void LogParts(long id, CreatureInstance body, Phenotype phenotype)
        {
            if (Environment.GetEnvironmentVariable("EVOSIM_STILLS_PART_LOG") != "1") return;

            var block = new MaterialPropertyBlock();
            for (int r = 0; r < body.Renderers.Length; r++)
            {
                var renderer = body.Renderers[r];
                int part = body.RendererPart[r];
                renderer.GetPropertyBlock(block);
                PhenotypePart p = part >= 0 && part < phenotype.PartCount ? phenotype.Parts[part] : null;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "TheatreSoloStill: id {0} renderer {1} part {2} cell {3} shape {4} shader {5} enabled {6} base {7} color {8} cell colour {9} link shape {10} link axis {11} leaf {12} fade {13}",
                    id, r, part, p != null ? p.CellTypeId : "-", p != null ? p.ShapeId : "-",
                    renderer.sharedMaterial != null ? renderer.sharedMaterial.shader.name : "none", renderer.enabled,
                    block.GetColor("_BaseColor"), block.GetColor("_Color"), block.GetColor("_CellColour"),
                    block.GetVector("_LinkShape"), block.GetVector("_LinkAxis"), block.GetFloat("_Leaf"), block.GetFloat("_Fade")));
            }
        }

        /// <summary>
        /// With the stills facing the mouth, one line per renderer of the main mouth's part: Core's mouth point taken through the
        /// part's body (what <see cref="FaceMouth"/> faces), the opening the shader draws (<c>_Mouth</c> through the renderer's own
        /// transform, and <c>_MouthRadii</c>), the gap between the two, and the angle between the eye's line and the outward axis.
        /// </summary>
        private static void LogMouthGeometry(long id, CreatureInstance body, Phenotype phenotype, int part, Camera cam)
        {
            Float3 tip = MouthPoint.Local(phenotype, part);
            var local = new Vector3(tip.X, tip.Y, tip.Z);
            if (body.Bodies == null || part >= body.Bodies.Length || body.Bodies[part] == null) return;
            Transform t = body.Bodies[part].transform;
            Vector3 core = t.TransformPoint(local);
            Vector3 outward = t.TransformDirection(local.normalized);
            Float3 h = phenotype.Parts[part].HalfExtents;
            TheatreMouth2.Measured m = TheatreMouth2.Measure(phenotype, part);
            var block = new MaterialPropertyBlock();
            for (int r = 0; r < body.Renderers.Length; r++)
            {
                if (body.RendererPart[r] != part) continue;
                Renderer renderer = body.Renderers[r];
                renderer.GetPropertyBlock(block);
                Vector4 mouth = block.GetVector("_Mouth"), radii = block.GetVector("_MouthRadii");
                Vector3 drawn = renderer.transform.TransformPoint(new Vector3(mouth.x, mouth.y, mouth.z));
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "TheatreSoloStill: id {0} mouth geometry part {1} renderer {2} half {3:0.000} {4:0.000} {5:0.000} tip {6:0.000} {7:0.000} {8:0.000} " +
                    "radius {9:0.000} core {10:0.000} {11:0.000} {12:0.000} drawn {13:0.000} {14:0.000} {15:0.000} gap {16:0.000} " +
                    "drawnRadii {17:0.000} {18:0.000} {19:0.000} scale {20:0.000} {21:0.000} {22:0.000} eyeToOutward {23:0.0} deg",
                    id, part, renderer.name, h.X, h.Y, h.Z, local.x, local.y, local.z, m.RadiusMetres, core.x, core.y, core.z,
                    drawn.x, drawn.y, drawn.z, Vector3.Distance(core, drawn), radii.x, radii.y, radii.z,
                    renderer.transform.lossyScale.x, renderer.transform.lossyScale.y, renderer.transform.lossyScale.z,
                    Vector3.Angle(-cam.transform.forward, outward)));

                // The disc's axis and the flat and relief dials as the renderer's block holds them (EVOSIM_THEATRE_MOUTH2_FLAT runs only
                // on an axis of length over 0.5).
                Vector4 axis = block.GetVector("_MouthAxis");
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "TheatreSoloStill: id {0} mouth disc part {1} renderer {2} axis {3:0.000} {4:0.000} {5:0.000} phase {6:0.000} flat {7:0.###} slack {8:0.###} relief {9:0.###}",
                    id, part, renderer.name, axis.x, axis.y, axis.z, axis.w, block.GetFloat("_MouthFlat"), block.GetFloat("_MouthFlatSlack"),
                    block.GetFloat("_MouthRelief")));
            }
        }

        /// <summary>
        /// The frame and bearing that face the main mouth: the part's renderers' box, and the eye on the mouth's outward axis
        /// (<c>MouthPoint.Local</c>, from the part's centre) turned <see cref="MouthTurn"/> of the way toward the usual bearing.
        /// Left as they are when the mouth point is at the part's centre.
        /// </summary>
        private static void FaceMouth(CreatureInstance body, Phenotype phenotype, int part, ref Bounds frame, ref Vector3 bearing)
        {
            Float3 tip = MouthPoint.Local(phenotype, part);
            var local = new Vector3(tip.X, tip.Y, tip.Z);
            if (!(local.magnitude > 1e-4f) || body.Bodies == null || part >= body.Bodies.Length || body.Bodies[part] == null) return;

            Vector3 outward = body.Bodies[part].transform.TransformDirection(local.normalized);

            // EVOSIM_THEATRE_MOUTH2_FACE: a sucker on the body's rim is faced along its own axis.
            float turn = MouthTurn;
            if (TheatreMouth2.Face && TheatreMouth2.PaintedKind(body.Renderers) == 2 && TheatreMouth2.FacesOnRim(phenotype.Parts[part], local))
            {
                turn = 0f;
                Debug.Log("TheatreSoloStill: mouth face-on: part " + part + " is a sucker on the rim, faced along its axis");
            }

            Vector3 facing = Vector3.Slerp(-outward, Bearing.normalized, turn);
            if (Mathf.Abs(Vector3.Dot(facing.normalized, Vector3.up)) > 0.98f) facing = Vector3.Slerp(facing, Bearing.normalized, 0.3f);

            bool any = false;
            Bounds box = default;
            for (int r = 0; r < body.Renderers.Length; r++)
            {
                if (body.RendererPart[r] != part) continue;
                if (!any) { box = body.Renderers[r].bounds; any = true; }
                else box.Encapsulate(body.Renderers[r].bounds);
            }
            if (!any) return;

            if (MouthClose)
            {
                TheatreMouth2.Measured m = TheatreMouth2.Measure(phenotype, part);
                if (m.Valid)
                {
                    Vector3 at = body.Bodies[part].transform.TransformPoint(local);
                    box = new Bounds(at, Vector3.one * (2f * MouthCloseReach * m.RadiusMetres));
                }
            }

            frame = box;
            bearing = facing;
        }

        /// <summary>
        /// <see cref="FrameLoad"/>'s frame and bearing for this still when it has a line, and <see cref="FrameSave"/>'s line written
        /// for it; each logged.
        /// </summary>
        private static void PinFrame(string name, ref Bounds frame, ref Vector3 bearing)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;

            if (!string.IsNullOrEmpty(FrameLoad))
            {
                if (!File.Exists(FrameLoad)) throw new FileNotFoundException("EVOSIM_STILLS_FRAME_LOAD names no file", FrameLoad);

                bool found = false;
                foreach (string line in File.ReadAllLines(FrameLoad))
                {
                    string[] f = line.Split(' ');
                    if (f.Length != 10 || f[0] != name) continue;

                    float N(int k) => float.Parse(f[k], inv);
                    frame = new Bounds(new Vector3(N(1), N(2), N(3)), 2f * new Vector3(N(4), N(5), N(6)));
                    bearing = new Vector3(N(7), N(8), N(9));
                    found = true;
                }

                Debug.Log(string.Format(inv, "[Theatre] stills frame: {0} {1} from {2}", name, found ? "pinned" : "not listed, its own", FrameLoad));
            }

            if (!string.IsNullOrEmpty(FrameSave))
            {
                Vector3 c = frame.center, e = frame.extents;
                File.AppendAllText(FrameSave, string.Format(inv, "{0} {1:R} {2:R} {3:R} {4:R} {5:R} {6:R} {7:R} {8:R} {9:R}\n",
                    name, c.x, c.y, c.z, e.x, e.y, e.z, bearing.x, bearing.y, bearing.z));
                Debug.Log(string.Format(inv, "[Theatre] stills frame: {0} saved to {1}", name, FrameSave));
            }
        }

        /// <summary>The given bearing, at the distance that holds the whole box in a square frame.</summary>
        private static void Frame(Camera cam, Bounds bounds, Vector3 bearing)
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
