using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// A still of a recorded world, drawn from the run's own files with nothing simulated:
    /// <c>snapshots/NNNNNNNNN.jsonl</c> joined to <c>positions.jsonl</c> on the organism id
    /// (<c>logbook/specs/snapshot-render-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it exists.</b> A picture of a world at 30,000 s cost a full re-simulation from the
    /// first second, which is a day of an Editor's life for one frame, and round 41's first early
    /// look timed out on the snapshot tool's wall with nothing written (2026-09-18). The farm
    /// already records, on the same cadence, everything a world view needs: every living body's
    /// genome in a snapshot, and every living body's place and guild in <c>positions.jsonl</c>.
    /// So the picture is a join and a development, and the box is drawn in a few seconds.
    /// </para>
    /// <para>
    /// <b>Two things it gets wrong, and neither is invented silently.</b> <i>Orientation</i>: no
    /// file records how a body was lying, so every body is drawn in the developer's own frame,
    /// root upright and unrotated. <i>Size</i>: a body is born at a fraction of its adult body and
    /// grows (D087), and only the adult is in the genome, so every body is drawn at its adult
    /// size. Both are on the burnt-in label of every frame, in the log, and in the tool's help,
    /// and the close view is refused in this mode because a portrait of two bodies is exactly
    /// where they show.
    /// </para>
    /// <para>
    /// <b>A third thing, smaller, and said here rather than on the label.</b> Nothing records a
    /// body's reserve, so every body is painted sated; and a body's centre is taken as the
    /// volume-weighted centre of its parts, where the harness's own reading includes the water a
    /// body entrains (<c>FluidEnvironment.CentreOfMass</c> over masses that added mass has
    /// multiplied). The difference is centimetres on a body, under a pixel at a world view's
    /// thirteen pixels a metre.
    /// </para>
    /// <para>
    /// <b>It writes nothing and steps nothing.</b> The run directory is read and never touched,
    /// physics is put into script mode for the session so that nothing moves between the build
    /// and the shot, and no body here has an <c>ArticulationBody</c>, a collider or a rigidbody
    /// at all: a part is a transform with a renderer on it, in the skin the replay wears.
    /// </para>
    /// </remarks>
    public sealed class SnapshotWorld : IDisposable, ITheatreFrame
    {
        /// <summary>One body that was joined and is waiting to be developed and built.</summary>
        private struct Pending
        {
            public long Id;
            public int Row;
            public Vector3 At;
            public int Flags;
        }

        /// <summary>One body that has been built.</summary>
        private sealed class Body
        {
            public long Id;
            public Phenotype Phenotype;
            public Vector3 At;
            public bool Absorptive;
            public bool Photosynthetic;
        }

        // ---------------------------------------------------------------- what is on screen

        public RunRecord Record { get; private set; }

        public BedShape Bed { get; private set; }

        /// <summary>The snapshot second being drawn.</summary>
        public double Second { get; private set; }

        /// <summary>True once every joined body has been built. Nothing is photographed before it.</summary>
        public bool Ready { get; private set; }

        /// <summary>How many bodies were in both files.</summary>
        public int JoinedCount { get; private set; }

        /// <summary>Bodies in the positions row with no genome in the snapshot.</summary>
        public int WithoutAGenome { get; private set; }

        /// <summary>Genomes in the snapshot with no place in the positions row.</summary>
        public int WithoutAPosition { get; private set; }

        /// <summary>Bodies whose recorded guild flags and developed body disagree.</summary>
        public int GuildDisagreements { get; private set; }

        /// <summary>Genomes this build could not read, which are counted and not drawn.</summary>
        public int Unreadable { get; private set; }

        /// <summary>The skin's palette, set by the runner before the first build.</summary>
        public TheatrePalette Palette;

        /// <summary>Whether the palette paints the guilds. The runner's own switch.</summary>
        public bool ColourByCellType = true;

        private readonly List<Body> _bodies = new List<Body>();
        private readonly List<Pending> _queue = new List<Pending>();
        private string[] _rows = new string[0];
        private readonly Dictionary<long, int> _rowOf = new Dictionary<long, int>();
        private int _built;
        private bool _begun;
        private string _snapshotName = "";
        private GameObject _holder;
        private Material _material;
        private SimulationMode _previousMode;
        private bool _modeSet;
        private string _firstUnreadable;

        private SnapshotWorld() { }

        // ---------------------------------------------------------------- opening

        /// <summary>
        /// Opens a run for reconstruction: its manifest, its config and the floor under it.
        /// </summary>
        /// <remarks>
        /// <b>No hash is compared, and the log says so.</b> A replay is refused when this build is
        /// not the build that recorded the run, because what it would produce is a cousin world.
        /// Nothing is produced here: the bodies, their places and the water's shape are all read
        /// off the run, so there is no second realisation to be wrong about. What can still differ
        /// is the development a genome grows into, which is <c>Evosim.Core</c>'s, and a genome the
        /// build cannot read at all is counted and not drawn rather than guessed at.
        /// </remarks>
        public static SnapshotWorld Open(string runDirectory, out string refusal)
        {
            refusal = null;
            var world = new SnapshotWorld();

            try
            {
                world.Record = RunRecord.Load(runDirectory);
            }
            catch (Exception e)
            {
                refusal = e.Message;
                return null;
            }

            // The floor the replay would build, from a world constructed and never stepped — the
            // same move Mode A makes for the field its creature smells. Everything the furniture
            // needs is in the config and the seed, so the sand here is the sand the run had.
            try
            {
                var water = new World(world.Record.Config, world.Record.Seed);
                world.Bed = water.Bed;
            }
            catch (Exception e)
            {
                refusal =
                    "the run's config would not build a world, so the floor under the picture " +
                    "cannot be drawn: " + e.GetType().Name + ": " + e.Message;
                return null;
            }

            world._previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            world._modeSet = true;

            world._holder = new GameObject("Reconstruction");

            Debug.Log("[Theatre] no identity check: nothing was simulated");

            return world;
        }

        // ---------------------------------------------------------------- the join

        /// <summary>
        /// Reads one snapshot second and the positions row at it, and queues what is in both.
        /// </summary>
        /// <remarks>
        /// The counts are logged here rather than after the build, because the join is the thing a
        /// reader has to be able to check: a live run's writers can be a sample apart, and a body
        /// in one file and not the other is the caveat that produces, not a reason to refuse.
        /// </remarks>
        public void Begin(double second)
        {
            Clear();

            Second = second;
            Ready = false;
            _begun = true;

            string directory = Record.Path;
            string snapshot = SnapshotFileAt(directory, second);

            if (snapshot == null)
            {
                Debug.LogError(
                    "[Theatre] no snapshot at " + Seconds(second) + " s in " + directory);
                Ready = true;
                return;
            }

            _snapshotName = Path.GetFileName(snapshot);

            // ReadRows, never File.ReadAllLines: a snapshot of a live run has a writer on it.
            _rows = JsonlWriter.ReadRows(snapshot);
            _rowOf.Clear();

            for (int i = 0; i < _rows.Length; i++)
            {
                long id;

                try
                {
                    id = GenomeJson.ReadId(_rows[i]);
                }
                catch (Exception e)
                {
                    Unreadable++;
                    if (_firstUnreadable == null) _firstUnreadable = e.Message;
                    continue;
                }

                if (id >= 0 && !_rowOf.ContainsKey(id)) _rowOf[id] = i;
            }

            string line = PositionsRowAt(directory, second);

            if (line == null)
            {
                Debug.LogError(
                    "[Theatre] no positions row at " + Seconds(second) + " s in " + directory);
                Ready = true;
                return;
            }

            var seen = new HashSet<long>();
            JsonNode row = Json.Parse(line);
            JsonNode bodies = row["b"];

            for (int i = 0; i < bodies.Count; i++)
            {
                JsonNode entry = bodies[i];

                var at = new Vector3(
                    (float)entry[1].AsDouble(),
                    (float)entry[2].AsDouble(),
                    (float)entry[3].AsDouble());

                long id = (long)entry[0].AsDouble();
                seen.Add(id);

                if (!_rowOf.TryGetValue(id, out int index))
                {
                    WithoutAGenome++;
                    continue;
                }

                _queue.Add(new Pending
                {
                    Id = id,
                    Row = index,
                    At = at,
                    Flags = entry[4].AsInt(),
                });
            }

            foreach (long id in _rowOf.Keys)
            {
                if (!seen.Contains(id)) WithoutAPosition++;
            }

            JoinedCount = _queue.Count;
            _built = 0;

            Debug.Log(
                "[Theatre] snapshot from snapshots/" + _snapshotName + ": " +
                _rows.Length + " genomes, " + bodies.Count + " positions, " +
                JoinedCount + " joined, " + WithoutAGenome + " without a genome, " +
                WithoutAPosition + " without a position");

            if (Unreadable > 0)
            {
                Debug.LogWarning(
                    "[Theatre] " + Unreadable + " genome(s) in " + _snapshotName +
                    " this build cannot read, counted and not drawn: " + _firstUnreadable);
            }
        }

        // ---------------------------------------------------------------- the bodies

        /// <summary>
        /// Develops and builds bodies until the budget runs out. Returns true when the world is
        /// whole.
        /// </summary>
        /// <remarks>
        /// Across frames rather than in one call, for the reason <c>Drive()</c> waits for a replay
        /// to reach its second: an Editor that spends thirty seconds inside one <c>Update</c>
        /// looks exactly like an Editor that has wedged, and a batch Editor with a wall clock on
        /// it cannot tell the difference either.
        /// </remarks>
        public bool BuildSome(double budgetSeconds)
        {
            // Nothing has been asked for yet: the runner opens the water and the caller names the
            // second. Without this the first Update would declare an empty world whole and log a
            // reconstruction of nothing at t=0.
            if (!_begun) return false;

            if (Ready) return true;

            var clock = Stopwatch.StartNew();

            while (_built < _queue.Count)
            {
                Build(_queue[_built]);
                _built++;

                if (clock.Elapsed.TotalSeconds > budgetSeconds) return false;
            }

            Ready = true;

            Debug.Log(
                "[Theatre] reconstructed " + _bodies.Count + " bodies at t=" + Seconds(Second) +
                " s, every one at its adult size in the developer's own frame" +
                (GuildDisagreements > 0
                    ? ", " + GuildDisagreements + " whose recorded guild flags and developed body disagree"
                    : ""));

            return true;
        }

        private void Build(Pending pending)
        {
            Genome genome;

            try
            {
                genome = GenomeJson.Read(_rows[pending.Row]);
            }
            catch (Exception e)
            {
                Unreadable++;
                if (_firstUnreadable == null) _firstUnreadable = e.Message;
                return;
            }

            Phenotype phenotype;

            try
            {
                phenotype = Developer.Develop(
                    genome, Record.Config.Development, null, Record.Config.Shapes);
            }
            catch (Exception e)
            {
                Unreadable++;
                if (_firstUnreadable == null) _firstUnreadable = e.Message;
                return;
            }

            if (phenotype.PartCount == 0) return;

            bool absorptive = Carries(phenotype, CellTypeIds.Absorptive);
            bool photosynthetic = Carries(phenotype, CellTypeIds.Photosynthetic);
            bool jointed = phenotype.TotalDof > 0;

            // Counted and logged, never corrected: the flags were written by the harness from the
            // body as it stood, which at a fraction of its adult size may have had a part pruned
            // under minPartVolume that the adult keeps. A reader is owed the disagreement.
            int flags = pending.Flags;
            bool recordedAbsorptive = (flags & PositionsRow.AbsorptiveBit) != 0;
            bool recordedPhotosynthetic = (flags & PositionsRow.PhotosyntheticBit) != 0;
            bool recordedJointed = (flags & PositionsRow.JointedBit) != 0;

            if (recordedAbsorptive != absorptive ||
                recordedPhotosynthetic != photosynthetic ||
                recordedJointed != jointed ||
                (flags & ~PositionsRow.AllBits) != 0)
            {
                GuildDisagreements++;

                if (GuildDisagreements == 1)
                {
                    Debug.LogWarning(
                        "[Theatre] creature " + pending.Id + "'s recorded guild flags and its " +
                        "developed body disagree: the row says absorptive " + recordedAbsorptive +
                        ", jointed " + recordedJointed + ", photosynthetic " +
                        recordedPhotosynthetic + "; the adult develops absorptive " + absorptive +
                        ", jointed " + jointed + ", photosynthetic " + photosynthetic +
                        ". Counted, not corrected. The marker's colour is the row's answer.");
                }
            }

            GameObject root = Assemble(phenotype, pending.Id, pending.At);

            _bodies.Add(new Body
            {
                Id = pending.Id,
                Phenotype = phenotype,
                At = pending.At,

                // The row's answer, not the development's: the report's guild counts are the
                // harness's, and a picture that coloured a body from a second opinion would put
                // two answers to one question into the record.
                Absorptive = recordedAbsorptive,
                Photosynthetic = recordedPhotosynthetic,
            });

            if (Palette != null) Palette.Paint(pending.Id, root.transform, phenotype, 1f, ColourByCellType);
        }

        /// <summary>
        /// One body as transforms and renderers: the hierarchy <c>PhenotypeBuilder</c> makes,
        /// with the physics left out.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The names and the shape of the tree are the contract.</b>
        /// <see cref="TheatrePalette"/> reads a part's index off its transform's name and finds a
        /// visual by walking the renderers under the root, so a reconstructed body has to be
        /// built the way a simulated one is: <c>PartNN_nK</c> parented to its parent part, each
        /// with its visuals as children. A different tree would be dressed wrongly and nothing
        /// would say so.
        /// </para>
        /// <para>
        /// <b>The visual plan is duplicated, deliberately.</b>
        /// <c>PhenotypeBuilder.VisualPlan</c> is private to <c>Evosim.Sim</c> and a sphere drawn
        /// at twice its radius and a capsule drawn as a cylinder between two spheres is what it
        /// says; widening that seam so a viewer could call it would be changing the simulation's
        /// source for the theatre's convenience, which is the rule <see cref="SoloCreature"/> is
        /// written under. The dimensions all come from the shape's own statics, so the two copies
        /// cannot disagree about how large a part is.
        /// </para>
        /// </remarks>
        private GameObject Assemble(Phenotype phenotype, long id, Vector3 at)
        {
            var root = new GameObject("Creature") { layer = PhenotypeBuilder.CreatureLayer };
            root.transform.SetParent(_holder.transform, false);

            var transforms = new Transform[phenotype.PartCount];

            for (int i = 0; i < phenotype.PartCount; i++)
            {
                PhenotypePart part = phenotype.Parts[i];

                var go = new GameObject(
                    string.Format(CultureInfo.InvariantCulture, "Part{0:00}_n{1}", i, part.SourceNode))
                {
                    layer = PhenotypeBuilder.CreatureLayer,
                };

                go.transform.SetParent(
                    part.IsRoot ? root.transform : transforms[part.ParentIndex], false);

                if (part.IsRoot)
                {
                    go.transform.localPosition = part.Position.ToVector3();
                    go.transform.localRotation = part.Rotation.ToQuaternion();
                }
                else
                {
                    PhenotypePart parent = phenotype.Parts[part.ParentIndex];
                    Quaternion inverse = Quaternion.Inverse(parent.Rotation.ToQuaternion());

                    go.transform.localPosition =
                        inverse * (part.Position - parent.Position).ToVector3();
                    go.transform.localRotation = inverse * part.Rotation.ToQuaternion();
                }

                transforms[i] = go.transform;

                AddVisuals(go.transform, part, Record.Config.Shapes.Resolve(part.ShapeId));
            }

            // The recorded position is where the body's centre was, so the body is hung from its
            // own centre rather than from its root part.
            root.transform.position = at - CentreOf(phenotype);

            return root;
        }

        private void AddVisuals(Transform part, PhenotypePart what, PartShape shape)
        {
            Float3 h = what.HalfExtents;

            switch (shape)
            {
                case SphereShape _:
                {
                    float r = SphereShape.Radius(h);
                    AddMesh(part, Sphere, Vector3.zero, Vector3.one * (2f * r));
                    break;
                }

                case CapsuleShape _:
                {
                    float r = CapsuleShape.Radius(h);
                    float span = CapsuleShape.HalfSpan(h);

                    if (span > 0f)
                    {
                        AddMesh(part, Cylinder, Vector3.zero, new Vector3(2f * r, span, 2f * r));
                    }

                    AddMesh(part, Sphere, new Vector3(0f, span, 0f), Vector3.one * (2f * r));
                    AddMesh(part, Sphere, new Vector3(0f, -span, 0f), Vector3.one * (2f * r));
                    break;
                }

                default:
                {
                    AddMesh(part, Cube, Vector3.zero, new Vector3(
                        2f * Mathf.Abs(h.X), 2f * Mathf.Abs(h.Y), 2f * Mathf.Abs(h.Z)));
                    break;
                }
            }
        }

        private void AddMesh(Transform parent, Mesh mesh, Vector3 offset, Vector3 scale)
        {
            var visual = new GameObject("Visual") { layer = PhenotypeBuilder.CreatureLayer };
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = offset;
            visual.transform.localScale = scale;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = PartMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// The volume-weighted centre of a developed body, in its own frame.
        /// </summary>
        /// <remarks>
        /// Volume rather than mass, which is the same thing: every part is tissue of one density
        /// (<c>PhenotypeBuilder.DensityKgPerM3</c>), so the two differ only by the added mass a
        /// swimming body entrains, which the class remarks say is centimetres here.
        /// </remarks>
        private static Vector3 CentreOf(Phenotype phenotype)
        {
            Vector3 sum = Vector3.zero;
            float volume = 0f;

            foreach (PhenotypePart part in phenotype.Parts)
            {
                float v = Mathf.Max(1e-9f, part.Volume);
                sum += part.Position.ToVector3() * v;
                volume += v;
            }

            return volume > 1e-9f ? sum / volume : Vector3.zero;
        }

        private static bool Carries(Phenotype phenotype, string cellTypeId)
        {
            foreach (PhenotypePart part in phenotype.Parts)
            {
                if (part.CellTypeId == cellTypeId) return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- the meshes

        private static Mesh _cube;
        private static Mesh _sphere;
        private static Mesh _cylinder;

        /// <summary>
        /// The engine's own primitives, by the names <see cref="TheatreMeshes.RoundedFor"/> reads.
        /// </summary>
        /// <remarks>
        /// Borrowed as builtin resources rather than made with <c>CreatePrimitive</c>, which would
        /// put a cube, a sphere and a cylinder in the water for the rest of the frame. The names
        /// are what the skin swaps on, so a reconstructed body is rounded, carved and necked
        /// exactly as a simulated one is.
        /// </remarks>
        private static Mesh Cube =>
            _cube != null ? _cube : (_cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx"));

        private static Mesh Sphere =>
            _sphere != null ? _sphere : (_sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx"));

        private static Mesh Cylinder =>
            _cylinder != null ? _cylinder : (_cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"));

        /// <summary>The material a part is born with, which the skin replaces on the first paint.</summary>
        private Material PartMaterial
        {
            get
            {
                if (_material != null) return _material;

                Shader shader =
                    Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

                _material = new Material(shader) { name = "Theatre Reconstructed Part" };
                return _material;
            }
        }

        // ---------------------------------------------------------------- the frame

        public int BodyCount => _bodies.Count;

        public Vector3 PositionOf(int index) => _bodies[index].At;

        public Phenotype PhenotypeOf(int index) => _bodies[index].Phenotype;

        public bool AbsorptiveAt(int index) => _bodies[index].Absorptive;

        public bool PhotosyntheticAt(int index) => _bodies[index].Photosynthetic;

        /// <summary>
        /// Two lines: what this is, and then which world, when, how many and how it is wrong.
        /// </summary>
        /// <remarks>
        /// The first line is the whole point of the label. A still of a reconstruction under the
        /// arm's own name, sitting in <c>logbook/images/</c> beside a replay's frame of the same
        /// second, is the quietest way this project could mislead itself — which is the argument
        /// the replay's own NOT A FAITHFUL REPLAY was written under.
        /// </remarks>
        public string LabelFor(string view, string look)
        {
            int unmatched = WithoutAGenome + WithoutAPosition;

            return
                "RECONSTRUCTED FROM SNAPSHOT\n" +
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}  t={1:0.#}s  joined {2}  {3}  {4}  adult size, default orientation{5}",
                    Record.ArmName ?? "run", Second, JoinedCount, view, look,
                    unmatched > 0 ? "  " + unmatched + " unmatched" : "");
        }

        // ---------------------------------------------------------------- the files

        /// <summary>A run directory, or the arm directory above it, resolved as the replay does.</summary>
        public static string Resolve(string path) => RunRecord.ResolveRunDirectory(path);

        /// <summary>Every second a snapshot was written at, ascending. Empty when there are none.</summary>
        public static double[] SnapshotSeconds(string runDirectory)
        {
            string directory = Path.Combine(runDirectory, "snapshots");
            if (!Directory.Exists(directory)) return new double[0];

            var seconds = new List<double>();

            foreach (string file in Directory.GetFiles(directory, "*.jsonl"))
            {
                if (long.TryParse(
                        Path.GetFileNameWithoutExtension(file), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out long t))
                {
                    seconds.Add(t);
                }
            }

            seconds.Sort();
            return seconds.ToArray();
        }

        /// <summary>The snapshot file at a second, or null.</summary>
        public static string SnapshotFileAt(string runDirectory, double second)
        {
            string file = Path.Combine(
                Path.Combine(runDirectory, "snapshots"),
                string.Format(CultureInfo.InvariantCulture, "{0:000000000}.jsonl", (long)second));

            return File.Exists(file) ? file : null;
        }

        /// <summary>
        /// Every sample time in <c>positions.jsonl</c>, ascending, read without loading the file.
        /// </summary>
        /// <remarks>
        /// Streamed rather than taken through <c>JsonlWriter.ReadRows</c>, which reads the whole
        /// file into memory: a full run's positions are tens of megabytes and only one row of them
        /// is ever wanted. The share mode is the one that method exists for, so a live run is
        /// readable while its writer holds the file.
        /// </remarks>
        public static double[] PositionSeconds(string runDirectory)
        {
            var seconds = new List<double>();

            foreach (string line in Rows(runDirectory))
            {
                double t = TimeOf(line);
                if (!double.IsNaN(t)) seconds.Add(t);
            }

            seconds.Sort();
            return seconds.ToArray();
        }

        /// <summary>The positions row at a second, or null.</summary>
        public static string PositionsRowAt(string runDirectory, double second)
        {
            foreach (string line in Rows(runDirectory))
            {
                double t = TimeOf(line);
                if (!double.IsNaN(t) && Math.Abs(t - second) <= 1e-3) return line;
            }

            return null;
        }

        /// <summary>Whether the run wrote positions at all. A tiled world writes none.</summary>
        public static bool HasPositions(string runDirectory) =>
            File.Exists(Path.Combine(runDirectory, "positions.jsonl"));

        private static IEnumerable<string> Rows(string runDirectory)
        {
            string path = Path.Combine(runDirectory, "positions.jsonl");
            if (!File.Exists(path)) yield break;

            using (var stream = new FileStream(
                       path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false)))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    // A row being written is half a row, and half a row parsed as a world is
                    // worse than no world. Every complete row ends its body array and its object.
                    if (line.Length == 0 || !line.EndsWith("}", StringComparison.Ordinal)) continue;

                    yield return line;
                }
            }
        }

        /// <summary>A row's <c>t</c> without parsing the rest of it, or NaN.</summary>
        private static double TimeOf(string line)
        {
            const string opening = "{\"t\":";

            if (!line.StartsWith(opening, StringComparison.Ordinal)) return double.NaN;

            int end = line.IndexOf(',', opening.Length);
            if (end < 0) return double.NaN;

            return double.TryParse(
                line.Substring(opening.Length, end - opening.Length),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double t)
                ? t
                : double.NaN;
        }

        private static string Seconds(double t) =>
            t.ToString("0.###", CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------- housekeeping

        private void Clear()
        {
            if (_holder != null)
            {
                Transform holder = _holder.transform;

                for (int i = holder.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(holder.GetChild(i).gameObject);
                }
            }

            Palette?.Clear();

            _bodies.Clear();
            _queue.Clear();
            _rowOf.Clear();
            _rows = new string[0];
            _built = 0;
            _begun = false;

            JoinedCount = 0;
            WithoutAGenome = 0;
            WithoutAPosition = 0;
            GuildDisagreements = 0;
            Unreadable = 0;
            _firstUnreadable = null;
        }

        public void Dispose()
        {
            Clear();

            if (_holder != null)
            {
                UnityEngine.Object.DestroyImmediate(_holder);
                _holder = null;
            }

            if (_material != null)
            {
                UnityEngine.Object.DestroyImmediate(_material);
                _material = null;
            }

            if (!_modeSet) return;

            Physics.simulationMode = _previousMode;
            _modeSet = false;
        }
    }
}
