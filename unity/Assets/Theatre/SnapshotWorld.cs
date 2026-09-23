using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using Evosim.Core;
using Evosim.Farm;
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
    /// <b>Two things it got wrong, and neither was invented silently.</b> <i>Orientation</i>: no
    /// file recorded how a body was lying, so every body was drawn in the developer's own frame,
    /// root upright and unrotated. <i>Size</i>: a body is born at a fraction of its adult body and
    /// grows (D087), and only the adult is in the genome, so every body is drawn at its adult
    /// size. Both are on the burnt-in label of every frame, in the log, and in the tool's help,
    /// and the close view is refused in this mode because a portrait of two bodies is exactly
    /// where they show.
    /// </para>
    /// <para>
    /// <b>The first of the two is closed for a run that recorded poses.</b> The farm writes
    /// <c>poses.jsonl</c> beside <c>positions.jsonl</c> at the same cadence — each body's root
    /// place, its root attitude and its joint coordinates — so where a row exists the body is
    /// drawn lying as it lay, joints and all (<see cref="RecordedPoses"/>). A body without one
    /// keeps the developer's frame, and the label says <i>recorded pose</i> only when every drawn
    /// body had a row, <i>pose for n of m</i> when some did, and <i>default orientation</i> when
    /// the run wrote no poses at that second at all. Size is unchanged: nothing records how far a
    /// body had grown, so <i>adult size</i> stays on every frame.
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

            /// <summary>How far the body had grown: 1 when nothing recorded it.</summary>
            public float Fraction;
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

        /// <inheritdoc />
        public ReefGeometry Reefs { get; private set; }

        /// <summary>The second being drawn.</summary>
        public double Second { get; private set; }

        /// <summary>
        /// Whether the frame came from the state stream rather than from <c>positions.jsonl</c>.
        /// </summary>
        /// <remarks>
        /// The stream is <c>poses.bin</c> (<c>logbook/specs/state-stream-spec.md</c>), written
        /// every half second or so where a snapshot is written every thousand. So a picture from
        /// the stream is a join of two instants: the genomes from the last snapshot at or before
        /// the second, and the poses, the places and the sizes from the stream's frame at it. Both
        /// seconds go on the label, because a body's plan and a body's attitude coming from
        /// different instants is the kind of thing a still has to say out loud.
        /// </remarks>
        public bool FromStream { get; private set; }

        /// <summary>The snapshot second the genomes came from. Equals <see cref="Second"/> off the stream.</summary>
        public double SnapshotSecond { get; private set; }

        /// <summary>
        /// Whether every drawn body was drawn at the size it was rather than at its adult size.
        /// </summary>
        public bool RecordedSize { get; private set; }

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

        /// <summary>
        /// Bodies drawn in the attitude <c>poses.jsonl</c> recorded for them, rather than upright
        /// in the developer's own frame.
        /// </summary>
        public int PosedCount { get; private set; }

        /// <summary>
        /// Bodies whose recorded pose this build could not lay on the developed body — counted,
        /// drawn upright, and named once in the log.
        /// </summary>
        public int PoseRefusals { get; private set; }

        /// <summary>Whether a pose row was found at the drawn second at all.</summary>
        /// <remarks>
        /// The label turns on this rather than on <see cref="PosedCount"/>: a run with no
        /// <c>poses.jsonl</c>, or one whose poses do not reach this second, says
        /// <i>default orientation</i> exactly as it did before the file existed, and a run that
        /// has poses says how many of the drawn bodies wear one.
        /// </remarks>
        public bool PosesRecorded { get; private set; }

        /// <summary>
        /// Whether either picture-only reader was used — §11's <c>OLD-RUN READ</c>.
        /// </summary>
        /// <remarks>
        /// Set when the config or any genome was refused by the strict reader and read by the
        /// tolerant one. It goes on the label because a frame travels without its log, and the
        /// reader of a still is owed the fact that this build is not the build that wrote the
        /// run it is looking at.
        /// </remarks>
        public bool OldRunRead { get; private set; }

        /// <summary>How many rows were read in each genome format, for the log.</summary>
        private readonly SortedDictionary<int, int> _formats = new SortedDictionary<int, int>();

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
        private Dictionary<long, RecordedPose> _poses;
        private string _firstPoseRefusal;

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

            string oldRun;

            try
            {
                // Strict first, tolerant second: §11's ruling, and the order is what keeps a run
                // this build recorded reading exactly as it did before that ruling.
                world.Record = RunRecord.LoadForPicture(runDirectory, out oldRun);
            }
            catch (Exception e)
            {
                refusal = e.Message;
                return null;
            }

            if (oldRun != null)
            {
                world.OldRunRead = true;
                Debug.LogWarning("[Theatre] old-run read, config: " + oldRun);
            }

            // The floor, built the way World builds it and from the same two lines of its
            // constructor: a tank with a relief or a tilt gets a height map drawn from the bed's
            // own stream of the run's seed, and everything else gets the flat floor. Built here
            // rather than by constructing a World, because a World is the thing that simulates
            // and a picture-only config has no business inside one — and because the fields a
            // World allocates on the way are a hundred thousand cells nothing here would read.
            try
            {
                RunConfig config = world.Record.Config;

                world.Bed =
                    config.WorldShape == WorldShape.Tank &&
                    (config.BedReliefMetres > 0f || config.BedTiltMetres > 0f)
                        ? new BedShape(
                            TankGeometry.RadiusFor(config.WorldAreaSquareMetres),
                            config.WorldDepthMetres, config.BedReliefMetres,
                            config.BedTiltMetres, config.BedScaleMetres,
                            Rng.SeedFor(world.Record.Seed, World.BedShapeIndex),
                            config.BedShoreDepthMetres, config.BedShoreFadeMetres)
                        : null;

                // The reefs, placed as World places them: the same stream of the run's seed over
                // the same floor, so the rock stands where the run's rock stood. The two refusals
                // about the rest of the world are skipped (worldRules: false), because a
                // picture-only config does not carry the field model and the farm already ran it.
                world.Reefs = config.ReefCount > 0
                    ? ReefGeometry.Place(
                        config, TankGeometry.RadiusFor(config.WorldAreaSquareMetres), world.Bed,
                        Rng.SeedFor(world.Record.Seed, World.ReefPlacementIndex), worldRules: false)
                    : null;
            }
            catch (Exception e)
            {
                refusal =
                    "the run's config does not describe a floor this build can draw: " +
                    e.GetType().Name + ": " + e.Message;
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
            SnapshotSecond = second;
            Ready = false;
            _begun = true;
            FromStream = false;
            RecordedSize = false;

            string directory = Record.Path;

            // The state stream first, when the run wrote one and it holds this second. Its frames
            // are half a second apart where a snapshot is a thousand, so this is the path that
            // makes a second between snapshots drawable at all.
            if (BeginFromStream(directory, second)) return;

            string snapshot = SnapshotFileAt(directory, second);

            if (snapshot == null)
            {
                Debug.LogError(
                    "[Theatre] no snapshot at " + Seconds(second) + " s in " + directory);
                Ready = true;
                return;
            }

            ReadGenomes(snapshot);

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

                    // Nothing in positions.jsonl says how far a body had grown, so this path draws
                    // the adult, as it always has.
                    Fraction = 1f,
                });
            }

            foreach (long id in _rowOf.Keys)
            {
                if (!seen.Contains(id)) WithoutAPosition++;
            }

            JoinedCount = _queue.Count;
            _built = 0;

            // The third file of the join, and the only optional one: a run recorded before
            // 2026-09-21 has none, and a body is then drawn in the developer's frame as every
            // reconstruction was before poses existed.
            _poses = RecordedPoses.At(directory, second);
            PosesRecorded = _poses != null;

            Debug.Log(
                "[Theatre] snapshot from snapshots/" + _snapshotName + ": " +
                _rows.Length + " genomes, " + bodies.Count + " positions, " +
                JoinedCount + " joined, " + WithoutAGenome + " without a genome, " +
                WithoutAPosition + " without a position; " +
                (PosesRecorded
                    ? _poses.Count + " poses at this second"
                    : RecordedPoses.Has(directory)
                        ? "poses.jsonl carries no row at this second, so every body is drawn upright"
                        : "no poses.jsonl, so every body is drawn upright"));

            if (Unreadable > 0)
            {
                Debug.LogWarning(
                    "[Theatre] " + Unreadable + " genome(s) in " + _snapshotName +
                    " this build cannot read, counted and not drawn: " + _firstUnreadable);
            }
        }

        /// <summary>
        /// Queues the bodies from <c>poses.bin</c>'s frame at a second, joined to the nearest
        /// snapshot at or before it. False when the run has no stream or no frame there.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Two instants, and the label says both.</b> A stream frame carries a place, an
        /// attitude, joint coordinates and a body fraction, and no genome. A snapshot carries
        /// genomes and is written a thousand seconds apart. So the genomes come from the last
        /// snapshot at or before the second, and a body born after that snapshot has no genome at
        /// all: it is skipped, counted in <see cref="WithoutAGenome"/>, and the label's
        /// <i>unmatched</i> is what says so on the frame.
        /// </para>
        /// <para>
        /// <b>What this path can do that the other cannot.</b> The stream carries the body
        /// fraction, which no other file ever has, so a body is drawn at the size it was rather
        /// than at its adult size. What it loses is the harness's guild flags: those are in
        /// <c>positions.jsonl</c> at the sample cadence and not in the stream, so a body's guild
        /// here is the development's answer and the disagreement count has nothing to compare.
        /// </para>
        /// </remarks>
        private bool BeginFromStream(string directory, double second)
        {
            string streamPath = PoseStream.PathIn(directory);
            if (streamPath == null) return false;

            PoseFrame frame;

            try
            {
                using (PoseStreamReader reader = PoseStreamReader.Open(streamPath))
                {
                    frame = reader.At(second);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[Theatre] " + Path.GetFileName(streamPath) + " could not be read, so the " +
                    "picture falls back to positions.jsonl: " + e.Message);

                return false;
            }

            if (frame == null) return false;

            string snapshot = SnapshotFileAtOrBefore(directory, second);

            if (snapshot == null)
            {
                Debug.LogError(
                    "[Theatre] the stream has a frame at " + Seconds(second) + " s and no " +
                    "snapshot was written at or before it, so there are no genomes to draw.");

                Ready = true;
                return true;
            }

            FromStream = true;
            RecordedSize = true;
            SnapshotSecond = SecondOfSnapshot(snapshot);

            ReadGenomes(snapshot);

            _poses = new Dictionary<long, RecordedPose>(frame.Bodies.Length);

            var seen = new HashSet<long>();

            for (int i = 0; i < frame.Bodies.Length; i++)
            {
                PoseBody body = frame.Bodies[i];
                long id = body.Id;

                seen.Add(id);
                _poses[id] = RecordedPoses.From(body);

                if (!_rowOf.TryGetValue(id, out int index))
                {
                    // Born after the snapshot, so nothing in the run says what it is made of.
                    WithoutAGenome++;
                    continue;
                }

                _queue.Add(new Pending
                {
                    Id = id,
                    Row = index,
                    At = new Vector3(body.X, body.Y, body.Z),

                    // The stream carries no guild flags. Build takes the development's answer and
                    // makes no comparison, which is what a negative here means.
                    Flags = -1,
                    Fraction = body.BodyFraction > 0f ? body.BodyFraction : 1f,
                });
            }

            foreach (long id in _rowOf.Keys)
            {
                if (!seen.Contains(id)) WithoutAPosition++;
            }

            JoinedCount = _queue.Count;
            _built = 0;
            PosesRecorded = true;

            Debug.Log(
                "[Theatre] pose t=" + Seconds(second) + " s from poses.bin of snapshot " +
                Seconds(SnapshotSecond) + " s (snapshots/" + _snapshotName + "): " +
                _rows.Length + " genomes, " + frame.Bodies.Length + " poses, " +
                JoinedCount + " joined, " + WithoutAGenome + " born after the snapshot and " +
                "skipped, " + WithoutAPosition + " in the snapshot and not in the frame; every " +
                "body at the size the stream recorded for it");

            if (Unreadable > 0)
            {
                Debug.LogWarning(
                    "[Theatre] " + Unreadable + " genome(s) in " + _snapshotName +
                    " this build cannot read, counted and not drawn: " + _firstUnreadable);
            }

            return true;
        }

        /// <summary>Reads a snapshot file's rows and builds the id-to-row map.</summary>
        private void ReadGenomes(string snapshot)
        {
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
                    if (_firstUnreadable == null) _firstUnreadable = "row " + i + ": " + e.Message;
                    continue;
                }

                // A row with no id is a row from before format 4, and a body with no id cannot be
                // told from any other body in positions.jsonl. Counted and named, never guessed
                // at by position in the file.
                if (id < 0)
                {
                    Unreadable++;

                    if (_firstUnreadable == null)
                    {
                        _firstUnreadable =
                            "row " + i + " carries no organism id, so it cannot be joined to a " +
                            "place; the row is format " + Format(_rows[i]) + " and a picture " +
                            "reads " + PictureGenome.OldestFormat + " and up";
                    }

                    continue;
                }

                if (!_rowOf.ContainsKey(id)) _rowOf[id] = i;
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
                " s, every one at its adult size; " +
                (PosedCount == 0
                    ? "every one in the developer's own frame"
                    : PosedCount == _bodies.Count
                        ? "every one in its recorded pose"
                        : PosedCount + " in their recorded pose and " +
                          (_bodies.Count - PosedCount) + " in the developer's own frame") +
                (GuildDisagreements > 0
                    ? ", " + GuildDisagreements + " whose recorded guild flags and developed body disagree"
                    : "") +
                "; genome " + Formats());

            if (PoseRefusals > 0)
            {
                Debug.LogWarning(
                    "[Theatre] " + PoseRefusals + " recorded pose(s) this build could not lay on " +
                    "the developed body, counted and drawn upright: " + _firstPoseRefusal);
            }

            return true;
        }

        /// <summary>Two developer paths name the same part — <c>World.DevelopPlan</c>'s test.</summary>
        private static bool SamePath(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private void Build(Pending pending)
        {
            Genome genome;
            int format = GenomeJson.FormatVersion;

            try
            {
                genome = GenomeJson.Read(_rows[pending.Row]);
            }
            catch (Exception strict)
            {
                // §11: a picture may read a row this build would refuse to simulate. Tried only
                // after the strict reader, and it marks the frame.
                try
                {
                    genome = PictureGenome.Read(_rows[pending.Row], out format);
                    OldRunRead = true;
                }
                catch (Exception tolerant)
                {
                    Unreadable++;

                    if (_firstUnreadable == null)
                    {
                        _firstUnreadable =
                            "row " + pending.Row + " (creature " + pending.Id + "): " +
                            tolerant.Message + " [the strict reader said: " + strict.Message + "]";
                    }

                    return;
                }
            }

            _formats.TryGetValue(format, out int seen);
            _formats[format] = seen + 1;

            Phenotype phenotype;

            try
            {
                // The body's own plan, when the row carries it (the farm writes the module counts
                // and the bitten-off part paths beside the genome from 2026-09-22 night): a row
                // without them is developed at the genome's minimum, which is every earlier
                // recording and what hid round 44 seed 1's fourteen-metre leaf from the pictures.
                int[] counts = GenomeJson.ReadModuleCounts(_rows[pending.Row]);
                List<int[]> lost = GenomeJson.ReadLostPartPaths(_rows[pending.Row]);

                if (counts == null && lost == null)
                {
                    phenotype = Developer.Develop(
                        genome, Record.Config.Development, null, Record.Config.Shapes);
                }
                else
                {
                    var paths = new List<int[]>();
                    phenotype = Developer.Develop(
                        genome, Record.Config.Development, null, Record.Config.Shapes, counts, paths);

                    if (lost != null && lost.Count > 0)
                    {
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

                        if (any) phenotype = phenotype.WithoutSubtrees(drop, out _);
                    }
                }
            }
            catch (Exception e)
            {
                Unreadable++;

                if (_firstUnreadable == null)
                {
                    _firstUnreadable =
                        "row " + pending.Row + " (creature " + pending.Id + "): " + e.Message;
                }

                return;
            }

            if (phenotype.PartCount == 0) return;

            // The size the body was, when something recorded it. Core grows a creature by scaling
            // its adult phenotype by the cube root of the tissue fraction (World.Grow), so the
            // same scaling here draws the body the run had rather than the adult it would become.
            if (pending.Fraction > 0f && pending.Fraction < 1f)
            {
                try
                {
                    phenotype = phenotype.Scaled(
                        Mathf.Pow(pending.Fraction, 1f / 3f), Record.Config.Shapes);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        "[Theatre] creature " + pending.Id + " has a body fraction of " +
                        pending.Fraction + " this build could not scale to, so it is drawn at " +
                        "its adult size: " + e.Message);
                }
            }

            bool absorptive = Carries(phenotype, CellTypeIds.Absorptive);
            bool photosynthetic = Carries(phenotype, CellTypeIds.Photosynthetic);
            bool jointed = phenotype.TotalDof > 0;

            // Counted and logged, never corrected: the flags were written by the harness from the
            // body as it stood, which at a fraction of its adult size may have had a part pruned
            // under minPartVolume that the adult keeps. A reader is owed the disagreement.
            int flags = pending.Flags;

            // A negative is the stream's answer: it carries no guild flags, so there is no second
            // opinion to disagree with and the development's own is what the body is painted by.
            bool fromTheRow = flags >= 0;

            bool recordedAbsorptive =
                fromTheRow ? (flags & PositionsRow.AbsorptiveBit) != 0 : absorptive;

            bool recordedPhotosynthetic =
                fromTheRow ? (flags & PositionsRow.PhotosyntheticBit) != 0 : photosynthetic;

            bool recordedJointed =
                fromTheRow ? (flags & PositionsRow.JointedBit) != 0 : jointed;

            if (fromTheRow &&
                (recordedAbsorptive != absorptive ||
                 recordedPhotosynthetic != photosynthetic ||
                 recordedJointed != jointed ||
                 (flags & ~PositionsRow.AllBits) != 0))
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

            RecordedPose pose = null;

            if (_poses != null && _poses.TryGetValue(pending.Id, out RecordedPose recorded))
            {
                pose = recorded;
            }

            GameObject root = Assemble(phenotype, pending.Id, pending.At, pose);

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
        private GameObject Assemble(Phenotype phenotype, long id, Vector3 at, RecordedPose pose)
        {
            int count = phenotype.PartCount;

            // The pose, resolved before anything is placed: a row that does not fit this body
            // leaves the developer's own frame in the arrays and is counted, so a refusal never
            // half-poses a creature.
            var positions = new Vector3[count];
            var rotations = new Quaternion[count];
            bool posed = false;

            if (pose != null)
            {
                if (RecordedPoses.Apply(phenotype, pose, positions, rotations, out string refusal))
                {
                    posed = true;
                    PosedCount++;
                }
                else
                {
                    PoseRefusals++;

                    if (_firstPoseRefusal == null)
                    {
                        _firstPoseRefusal = "creature " + id + ": " + refusal;
                    }
                }
            }

            if (!posed)
            {
                for (int i = 0; i < count; i++)
                {
                    positions[i] = phenotype.Parts[i].Position.ToVector3();
                    rotations[i] = phenotype.Parts[i].Rotation.ToQuaternion();
                }
            }

            var root = new GameObject("Creature") { layer = PhenotypeBuilder.CreatureLayer };
            root.transform.SetParent(_holder.transform, false);

            var transforms = new Transform[count];

            for (int i = 0; i < count; i++)
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
                    go.transform.localPosition = positions[i];
                    go.transform.localRotation = rotations[i];
                }
                else
                {
                    Quaternion inverse = Quaternion.Inverse(rotations[part.ParentIndex]);

                    go.transform.localPosition =
                        inverse * (positions[i] - positions[part.ParentIndex]);
                    go.transform.localRotation = inverse * rotations[i];
                }

                transforms[i] = go.transform;

                AddVisuals(go.transform, part, Record.Config.Shapes.Resolve(part.ShapeId));
            }

            // A posed body's arrays are in its own frame with the root link at the origin, and
            // the row's place is that link's: it goes there, exactly. Without a pose the body
            // keeps the rule reconstruction has always used, hung by its volume-weighted centre
            // at the position the row carries.
            root.transform.position = posed ? pose.Root : at - CentreOf(phenotype);

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

            string join = FromStream
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "  pose t={0:0.###} of snapshot {1:0.#}", Second, SnapshotSecond)
                : "";

            return
                "RECONSTRUCTED FROM SNAPSHOT" + (OldRunRead ? " · OLD-RUN READ" : "") + "\n" +
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}  t={1:0.#}s{2}  joined {3}  {4}  {5}  {6}, {7}{8}",
                    Record.ArmName ?? "run", Second, join, JoinedCount, view, look,
                    RecordedSize ? "recorded size" : "adult size", Attitude(),
                    unmatched > 0 ? "  " + unmatched + " unmatched" : "");
        }

        /// <summary>
        /// The half of the label that says how the bodies are held.
        /// </summary>
        /// <remarks>
        /// Three wordings and not two, because "recorded pose" on a frame where a tenth of the
        /// crowd is standing in the developer's frame would be the quiet kind of wrong this
        /// label exists to prevent. A run that recorded no poses reads exactly as it did before
        /// the file existed, to the character.
        /// </remarks>
        private string Attitude()
        {
            if (!PosesRecorded || _bodies.Count == 0) return "default orientation";

            return PosedCount == _bodies.Count
                ? "recorded pose"
                : "pose for " + PosedCount + " of " + _bodies.Count;
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
        /// The last snapshot file written at or before a second, or null when there is none.
        /// </summary>
        /// <remarks>
        /// What a state-stream picture joins its poses to. Nearest <i>at or before</i> and never
        /// the nearest of the two: a later snapshot holds genomes of bodies that did not exist at
        /// the second being drawn, and it is missing ones that did.
        /// </remarks>
        public static string SnapshotFileAtOrBefore(string runDirectory, double second)
        {
            double[] seconds = SnapshotSeconds(runDirectory);
            double best = double.NaN;

            foreach (double s in seconds)
            {
                if (s > second + 1e-3) continue;
                if (double.IsNaN(best) || s > best) best = s;
            }

            return double.IsNaN(best) ? null : SnapshotFileAt(runDirectory, best);
        }

        /// <summary>The second a snapshot file's name states.</summary>
        private static double SecondOfSnapshot(string path) =>
            long.TryParse(
                Path.GetFileNameWithoutExtension(path), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out long t)
                ? t
                : 0d;

        /// <summary>Whether the run wrote a state stream.</summary>
        public static bool HasStream(string runDirectory) => PoseStream.Has(runDirectory);

        /// <summary>
        /// Every second the state stream holds a complete frame at, ascending, or an empty array.
        /// </summary>
        /// <remarks>
        /// Read from <c>poses.idx</c> when the run left one and by scanning the stream when it did
        /// not, which is what a killed or a live run needs. The refusals are swallowed here rather
        /// than thrown, because a caller asking which seconds are drawable wants an answer and the
        /// picture path logs the reason it fell back.
        /// </remarks>
        public static double[] StreamSeconds(string runDirectory)
        {
            string path = PoseStream.PathIn(runDirectory);
            if (path == null) return new double[0];

            try
            {
                using (PoseStreamReader reader = PoseStreamReader.Open(path))
                {
                    return reader.Seconds();
                }
            }
            catch (Exception)
            {
                return new double[0];
            }
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

        /// <summary>Which genome formats were read, and how many rows of each.</summary>
        /// <remarks>
        /// Printed on every reconstruction rather than only on an old one, because "format 6,
        /// read strictly" is the fact a reader of an old frame needs to compare against.
        /// </remarks>
        private string Formats()
        {
            if (_formats.Count == 0) return "no row was read";

            var parts = new List<string>(_formats.Count);

            foreach (KeyValuePair<int, int> pair in _formats)
            {
                parts.Add(
                    "format " + pair.Key + " on " + pair.Value + " row(s)" +
                    (pair.Key == GenomeJson.FormatVersion
                        ? ", read strictly"
                        : ", read by the picture-only reader"));
            }

            return string.Join("; ", parts.ToArray());
        }

        /// <summary>A row's format, or -1 when it does not say. For a refusal's wording only.</summary>
        private static int Format(string row)
        {
            try
            {
                JsonNode node = Json.Parse(row);
                return node.Has("format") ? node["format"].AsInt() : -1;
            }
            catch (Exception)
            {
                return -1;
            }
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

            _formats.Clear();

            JoinedCount = 0;
            WithoutAGenome = 0;
            WithoutAPosition = 0;
            GuildDisagreements = 0;
            Unreadable = 0;
            _firstUnreadable = null;

            _poses = null;
            PosesRecorded = false;
            PosedCount = 0;
            PoseRefusals = 0;
            _firstPoseRefusal = null;

            FromStream = false;
            RecordedSize = false;
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
