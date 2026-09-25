using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Evosim.Core;
using Evosim.Farm;
using Evosim.Sim;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// A farm film window played back: every body at every frame where the farm put it, drawn
    /// from the window's own genomes and plans, with nothing stepped
    /// (<c>logbook/specs/record-and-film-spec.md</c>, B2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The farm moved the world and this draws it.</b> The Editor cannot re-simulate a farm run
    /// (Mono and .NET round a double sum differently, CLAUDE.md), so a scene is stepped by the
    /// farm from a checkpoint (<c>Evosim.Farm --film-window</c>) and written down at a frame rate:
    /// <c>film.poses.bin</c> with every body's root place, root attitude, joint coordinates, body
    /// fraction and guild at each frame; <c>genomes.jsonl.gz</c> with a genome for every body
    /// alive at the start or born inside; <c>plans.jsonl</c> with each body's module counts and
    /// lost parts; <c>events.jsonl</c> with the run's own birth, death and bite rows; and
    /// <c>identity.jsonl</c> with the farm's check against the run and its verdict. This class
    /// reads those through <see cref="FilmWindowReader"/>, develops each body once per plan,
    /// scales it to its recorded fraction, lays the recorded joints on it
    /// (<see cref="RecordedPoses.Apply"/>) and hands the parts to a fed
    /// <see cref="LiveWorldView"/>, the same body tree, skin and pick the live mode draws with.
    /// </para>
    /// <para>
    /// <b>The provenance word is the verdict's, and never this class's.</b> FAITHFUL only when the
    /// window's verdict says <c>faithful</c> (every identity row the farm compared agreed with the
    /// run's to the bit), COUSIN when it says <c>cousin</c>, and UNVERIFIED for an unverified
    /// window and for one with no verdict at all. It is the first line of every label, beside
    /// FARM FILM WINDOW, so a still cannot be mistaken for a replay or a reconstruction.
    /// </para>
    /// <para>
    /// <b>Bodies come and go with the frames.</b> A frame holds exactly the bodies the farm's
    /// solver held at that step, so a newborn appears at the first frame after the harness built
    /// its solver body and a dead one is gone from the first frame after its death, each by the
    /// view's own sweep. A body in a frame with no genome row (a fault in the window, counted, and
    /// zero in every window the tests have read) is not drawn.
    /// </para>
    /// <para>
    /// <b>A plan is the one the solver drew.</b> The farm applies a plan change in the world at a
    /// metabolic step and rebuilds the solver body at the start of the next physics step, and a
    /// frame due on the metabolic step is written between the two. So a frame takes the newest
    /// plan row written strictly before it, except the rows written as the window opened, which
    /// stand from its first frame. Where that plan's degrees of freedom still do not match the
    /// frame's joints, the older plans and then the genome's minimum are tried, and a body none
    /// of them fits is left standing where it last was for that frame and counted.
    /// </para>
    /// <para>
    /// <b>What it cannot show.</b> A body's reserve is not recorded, so every body is painted
    /// sated, as a reconstruction paints them. The fraction is the organism's, which a growing
    /// body's solver reaches at its next resize, so a body mid-growth is drawn a growth step ahead
    /// of the solver: a fraction of a centimetre. And the centre a camera frames on is the
    /// volume-weighted centre of the posed parts, without the water a swimming body entrains.
    /// </para>
    /// </remarks>
    public sealed class FilmWindowWorld : IDisposable, IFilmWorld
    {
        /// <summary>A body developed at one plan, and the arrays its pose is laid into.</summary>
        private sealed class Adult
        {
            /// <summary>The adult at this plan: also the view's key, so a new plan is a rebuild.</summary>
            public Phenotype Body;

            public int Dof;
            public bool Failed;

            public float ScaledFor = float.NaN;
            public Phenotype Scaled;

            public Vector3[] Positions = new Vector3[0];
            public Quaternion[] Rotations = new Quaternion[0];
        }

        /// <summary>One body's genome, parsed once, and its adults by plan.</summary>
        private sealed class Grown
        {
            public Genome Genome;
            public Adult Minimum;
            public readonly Dictionary<FilmWindowPlan, Adult> ByPlan = new Dictionary<FilmWindowPlan, Adult>();
        }

        /// <summary>One body drawn in the current frame.</summary>
        private struct Drawn
        {
            public long Id;
            public Phenotype Phenotype;
            public Vector3 Centre;
            public bool Absorptive;
            public bool Photosynthetic;
        }

        private readonly Dictionary<long, Grown> _grown = new Dictionary<long, Grown>();
        private readonly List<Drawn> _drawn = new List<Drawn>();
        private readonly Dictionary<long, int> _indexOf = new Dictionary<long, int>();
        private readonly SortedDictionary<int, int> _formats = new SortedDictionary<int, int>();

        private SimulationMode _previousMode;
        private bool _modeSet;
        private double _startCut;
        private string _firstUnreadable;
        private string _firstRefusal;

        // ---------------------------------------------------------------- what is on screen

        /// <summary>The window, open for the life of this world.</summary>
        public FilmWindowReader Window { get; private set; }

        /// <summary>The bodies, drawn through the live mode's view, fed rather than synced.</summary>
        public LiveWorldView View { get; private set; }

        /// <inheritdoc />
        public RunRecord Record { get; private set; }

        /// <inheritdoc />
        public BedShape Bed { get; private set; }

        /// <inheritdoc />
        public ReefGeometry Reefs { get; private set; }

        /// <summary>FAITHFUL, COUSIN or UNVERIFIED, from the window's verdict (<see cref="FilmWindowReader.ProvenanceWord"/>).</summary>
        public string ProvenanceWord => Window.ProvenanceWord;

        /// <summary>Whether the verdict says faithful. The only ground on which anything here may claim it.</summary>
        public bool Faithful => Window.Faithful;

        /// <summary>Whether the config or a genome was read by a picture-only reader (§11).</summary>
        public bool OldRunRead { get; private set; }

        /// <summary>
        /// Said when the window's stream was written under a config other than the run the
        /// theatre opened, or from another run directory; null when they agree.
        /// </summary>
        public string SourceNote { get; private set; }

        /// <summary>The frame on screen, or -1 before the first <see cref="Show"/>.</summary>
        public int FrameIndex { get; private set; } = -1;

        /// <summary>The recorded second of the frame on screen.</summary>
        public double Second { get; private set; } = double.NaN;

        public int FrameCount => Window.FrameCount;

        /// <summary>The first frame's recorded second, or NaN for an empty window.</summary>
        public double FirstSecond => FrameCount > 0 ? Window.SecondOf(0) : double.NaN;

        /// <summary>The last frame's recorded second, or NaN for an empty window.</summary>
        public double LastSecond => FrameCount > 0 ? Window.SecondOf(FrameCount - 1) : double.NaN;

        /// <summary>Bodies in the frame on screen with no genome row in the window. Reads 0.</summary>
        public int WithoutAGenome { get; private set; }

        /// <summary>Bodies in the frame on screen whose pose fitted no plan, left standing or not drawn.</summary>
        public int PoseRefusals { get; private set; }

        /// <summary>Bodies in the frame on screen drawn on an older plan than the newest one before it.</summary>
        public int PlanFallbacks { get; private set; }

        /// <summary>Bodies in the frame on screen drawn at the fraction the stream recorded.</summary>
        public int SizedCount { get; private set; }

        /// <summary>Genomes this build could not read or develop, counted once each.</summary>
        public int Unreadable { get; private set; }

        /// <summary>Frames shown since the world opened.</summary>
        public long FramesShown { get; private set; }

        /// <summary>The skin's palette. Set before the first frame.</summary>
        public TheatrePalette Palette
        {
            get => View.Palette;
            set => View.Palette = value;
        }

        /// <summary>Whether the palette paints the guilds.</summary>
        public bool ColourByCellType
        {
            get => View.ColourByCellType;
            set => View.ColourByCellType = value;
        }

        private FilmWindowWorld() { }

        // ---------------------------------------------------------------- opening

        /// <summary>Whether a directory is a film window.</summary>
        public static bool IsWindow(string directory) => FilmWindowReader.IsWindow(directory);

        /// <summary>
        /// Opens a window for playback: the window's files, the run's config and seed, and the
        /// floor under it.
        /// </summary>
        /// <param name="windowDirectory">The window's out directory, as the farm wrote it.</param>
        /// <param name="runDirectory">
        /// The run it was filmed from, or null to take the path its verdict names. A window
        /// written before the verdict carried the path, or moved to another machine, needs it.
        /// </param>
        /// <param name="refusal">Why it could not open, or null.</param>
        public static FilmWindowWorld Open(string windowDirectory, string runDirectory, out string refusal)
        {
            refusal = null;
            var world = new FilmWindowWorld();

            try
            {
                world.Window = FilmWindowReader.Open(windowDirectory);
            }
            catch (Exception e)
            {
                refusal = "the window cannot be read: " + e.Message;
                return null;
            }

            FilmWindowVerdict verdict = world.Window.Verdict;

            string run = !string.IsNullOrWhiteSpace(runDirectory) ? runDirectory : verdict?.RunDirectory;

            if (string.IsNullOrWhiteSpace(run))
            {
                world.Window.Dispose();
                refusal =
                    "the window names no run directory (a verdict written before it carried one, or " +
                    "no verdict at all), so there is no config to draw its bodies and its water from; " +
                    "name the run it was filmed from (EVOSIM_THEATRE_RUN).";
                return null;
            }

            try
            {
                world.Record = RunRecord.LoadForPicture(run, out string oldRun);

                if (oldRun != null)
                {
                    world.OldRunRead = true;
                    Debug.LogWarning("[Theatre] old-run read, config: " + oldRun);
                }

                SnapshotWorld.BuildFloor(world.Record, out BedShape bed, out ReefGeometry reefs);
                world.Bed = bed;
                world.Reefs = reefs;
            }
            catch (Exception e)
            {
                world.Window.Dispose();
                refusal = "the run " + run + " cannot be drawn: " + e.GetType().Name + ": " + e.Message;
                return null;
            }

            world.SourceNote = SourceNoteOf(world.Window, world.Record);

            if (world.SourceNote != null) Debug.LogWarning("[Theatre] film window: " + world.SourceNote);

            // The rows written as the window opened stand from its first frame; every later row
            // moved on a metabolic step and is drawn from the frame after it (the class remarks).
            world._startCut = verdict != null && !double.IsNaN(verdict.From)
                ? verdict.From + (double.IsNaN(verdict.PhysicsDt) ? 0d : 0.999d * verdict.PhysicsDt)
                : world.FirstSecond;

            world._previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            world._modeSet = true;

            world.View = new LiveWorldView(world.Record.Config, "Film Window");

            foreach (string note in world.Window.Notes) Debug.LogWarning("[Theatre] film window: " + note);

            Debug.Log(
                "[Theatre] film window " + world.Window.Describe() + "; drawn over " +
                world.Record.Path + " (" + (world.Record.ArmName ?? "run") + " seed " +
                world.Record.Seed + "): nothing is simulated, every body stands where the farm " +
                "put it");

            return world;
        }

        /// <summary>
        /// What differs between the window and the run it is drawn over, or null: the stream's
        /// config hash against the run's, and the verdict's run name against the directory's.
        /// </summary>
        private static string SourceNoteOf(FilmWindowReader window, RunRecord record)
        {
            var notes = new List<string>(2);

            string streamHash = window.Stream.Header.ConfigHash;

            if (!string.IsNullOrEmpty(streamHash) && !string.IsNullOrEmpty(record.ConfigHash) &&
                !string.Equals(streamHash, record.ConfigHash, StringComparison.OrdinalIgnoreCase))
            {
                notes.Add("the stream was written under config " + streamHash + " and the run's is " +
                          record.ConfigHash);
            }

            string named = window.Verdict?.RunName;
            string opened = System.IO.Path.GetFileName(
                record.Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));

            if (!string.IsNullOrEmpty(named) && !string.Equals(named, opened, StringComparison.Ordinal))
            {
                notes.Add("the window was filmed from run " + named + " and is drawn over " + opened);
            }

            return notes.Count == 0 ? null : string.Join("; ", notes.ToArray());
        }

        // ---------------------------------------------------------------- playback

        /// <summary>Shows the frame nearest a second, clamped to the window. False for an empty one.</summary>
        public bool ShowAt(double second)
        {
            int frame = Window.NearestFrame(second);
            if (frame < 0) return false;

            Show(frame);
            return true;
        }

        /// <summary>
        /// Puts every body where the frame has it: builds a body the frame brings, removes one it
        /// no longer holds, resizes one that grew, and rebuilds one whose plan moved.
        /// </summary>
        public void Show(int frame)
        {
            if (frame < 0 || frame >= FrameCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frame), frame, "The window holds " + FrameCount + " frame(s).");
            }

            PoseFrame poses = Window.ReadFrame(frame);

            FrameIndex = frame;
            Second = poses.Seconds;
            FramesShown++;

            _drawn.Clear();
            _indexOf.Clear();
            WithoutAGenome = 0;
            PoseRefusals = 0;
            PlanFallbacks = 0;
            SizedCount = 0;

            View.BeginFrame();

            for (int i = 0; i < poses.Bodies.Length; i++)
            {
                PoseBody body = poses.Bodies[i];
                long id = body.Id;

                Grown grown = GrownOf(id);

                if (grown == null)
                {
                    WithoutAGenome++;
                    continue;
                }

                if (grown.Genome == null) continue;

                RecordedPose pose = RecordedPoses.From(body);
                Adult adult = AdultFor(id, grown, pose.Joint.Length, out bool fellBack);

                if (adult == null)
                {
                    Refused(id, "no plan in the window develops into the " + pose.Joint.Length +
                                " joint coordinate(s) the frame carries");
                    continue;
                }

                if (fellBack) PlanFallbacks++;

                bool sized = body.FractionRecorded && body.BodyFraction > 0f;
                Phenotype phenotype = SizedFor(id, adult, sized ? body.BodyFraction : 1f);
                if (sized) SizedCount++;

                int parts = phenotype.PartCount;

                if (adult.Positions.Length < parts)
                {
                    adult.Positions = new Vector3[parts];
                    adult.Rotations = new Quaternion[parts];
                }

                if (!RecordedPoses.Apply(phenotype, pose, adult.Positions, adult.Rotations, out string refusal))
                {
                    Refused(id, refusal);
                    continue;
                }

                // Shaded by its reserve as the stepped route shades it (LiveWorldView.Sync), from the
                // stream's version 3 field. A version 2 window carries none and is drawn fully fed,
                // which is what every window drew until 2026-09-25: the owner's review found every
                // body of round 48's first window clips bright green where the stepped route drew
                // the starving ones dark.
                float tint = float.IsNaN(body.ReserveSeconds)
                    ? 1f
                    : TheatrePalette.Tint(body.ReserveSeconds, Record.Config.EnergyFullScaleSeconds);

                // A pose that is not finite leaves the body where it last stood, inside the view.
                if (!View.Place(id, adult.Body, phenotype, pose.Root, adult.Positions, adult.Rotations, tint)) continue;

                _indexOf[id] = _drawn.Count;
                _drawn.Add(new Drawn
                {
                    Id = id,
                    Phenotype = phenotype,
                    Centre = CentreOf(phenotype, pose.Root, adult.Positions),

                    // The harness's answer where the stream carries it, as positions.jsonl's is
                    // read; the development's where it does not.
                    Absorptive = body.Flags >= 0
                        ? (body.Flags & PoseStream.AbsorptiveBit) != 0
                        : Carries(phenotype, CellTypeIds.Absorptive),
                    Photosynthetic = body.Flags >= 0
                        ? (body.Flags & PoseStream.PhotosyntheticBit) != 0
                        : Carries(phenotype, CellTypeIds.Photosynthetic),
                });
            }

            View.EndFrame();
        }

        /// <summary>A body the frame could not lay on any plan: kept standing if it stands, counted.</summary>
        private void Refused(long id, string why)
        {
            PoseRefusals++;
            View.Keep(id);

            if (_firstRefusal == null)
            {
                _firstRefusal = "creature " + id + " at t=" + Seconds(Second) + ": " + why;
                Debug.LogWarning("[Theatre] film window: a pose this build could not lay on its body, " +
                                 "kept where it stood for the frame: " + _firstRefusal);
            }
        }

        /// <summary>Dresses every body the palette has not reached yet: what a still needs before it is taken.</summary>
        public int DressAll() => View.DressUndressed();

        // ---------------------------------------------------------------- the bodies

        /// <summary>A body's parsed genome, or null when the window has no row for it.</summary>
        private Grown GrownOf(long id)
        {
            if (_grown.TryGetValue(id, out Grown grown)) return grown;
            if (!Window.TryGenome(id, out string row)) return null;

            grown = new Grown();
            int format = GenomeJson.FormatVersion;

            try
            {
                grown.Genome = GenomeJson.Read(row);
            }
            catch (Exception strict)
            {
                try
                {
                    grown.Genome = PictureGenome.Read(row, out format);
                    OldRunRead = true;
                }
                catch (Exception tolerant)
                {
                    Unreadable++;

                    if (_firstUnreadable == null)
                    {
                        _firstUnreadable = "creature " + id + ": " + tolerant.Message +
                                           " [the strict reader said: " + strict.Message + "]";
                        Debug.LogWarning("[Theatre] film window: a genome this build cannot read, " +
                                         "not drawn: " + _firstUnreadable);
                    }
                }
            }

            if (grown.Genome != null)
            {
                _formats.TryGetValue(format, out int seen);
                _formats[format] = seen + 1;
            }

            _grown[id] = grown;
            return grown;
        }

        /// <summary>
        /// The adult the frame's joints fit: the plan the solver drew at this frame (the class
        /// remarks), then each older plan, then the genome's minimum.
        /// </summary>
        private Adult AdultFor(long id, Grown grown, int dof, out bool fellBack)
        {
            fellBack = false;
            IReadOnlyList<FilmWindowPlan> plans = Window.PlansOf(id);

            int primary = -1;

            for (int i = 0; i < plans.Count; i++)
            {
                double at = plans[i].Seconds;
                if (at <= _startCut || at < Second - 1e-6) primary = i;
                else break;
            }

            for (int i = primary; i >= -1; i--)
            {
                Adult adult = Develop(grown, i >= 0 ? plans[i] : null);
                if (adult.Failed || adult.Dof != dof) continue;

                fellBack = i != primary;
                return adult;
            }

            return null;
        }

        /// <summary>A genome developed at a plan (null for its minimum), once per body and plan.</summary>
        private Adult Develop(Grown grown, FilmWindowPlan plan)
        {
            Adult adult = plan == null ? grown.Minimum : grown.ByPlan.TryGetValue(plan, out Adult found) ? found : null;
            if (adult != null) return adult;

            adult = new Adult();

            try
            {
                adult.Body = SnapshotWorld.DevelopWithPlan(
                    grown.Genome, Record.Config, plan?.ModuleCounts, plan?.LostPaths);

                adult.Failed = adult.Body.PartCount == 0;
                adult.Dof = DofOf(adult.Body);
            }
            catch (Exception e)
            {
                adult.Failed = true;

                if (_firstUnreadable == null)
                {
                    _firstUnreadable = "a development failed: " + e.Message;
                    Debug.LogWarning("[Theatre] film window: " + _firstUnreadable);
                }
            }

            if (plan == null) grown.Minimum = adult;
            else grown.ByPlan[plan] = adult;

            return adult;
        }

        /// <summary>The degrees of freedom a pose must carry, counted as <see cref="RecordedPoses.Apply"/> counts them.</summary>
        private static int DofOf(Phenotype phenotype)
        {
            int dof = 0;

            for (int i = 0; i < phenotype.PartCount; i++)
            {
                PhenotypePart part = phenotype.Parts[i];
                if (!part.IsRoot) dof += part.JointType.DofCount();
            }

            return dof;
        }

        /// <summary>
        /// The adult scaled to a body fraction, as Core grows a creature (<c>World.Grow</c>: the
        /// cube root of the tissue fraction), cached until the fraction moves.
        /// </summary>
        private Phenotype SizedFor(long id, Adult adult, float fraction)
        {
            if (!(fraction > 0f) || fraction >= 1f) return adult.Body;
            if (adult.Scaled != null && adult.ScaledFor == fraction) return adult.Scaled;

            try
            {
                adult.Scaled = adult.Body.Scaled(Mathf.Pow(fraction, 1f / 3f), Record.Config.Shapes);
                adult.ScaledFor = fraction;
                return adult.Scaled;
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[Theatre] film window: creature " + id + " has a body fraction of " + fraction +
                    " this build could not scale to, so it is drawn at its adult size: " + e.Message);

                return adult.Body;
            }
        }

        /// <summary>The volume-weighted centre of a posed body, in world metres.</summary>
        private static Vector3 CentreOf(Phenotype phenotype, Vector3 root, Vector3[] positions)
        {
            Vector3 sum = Vector3.zero;
            float volume = 0f;

            for (int i = 0; i < phenotype.PartCount; i++)
            {
                float v = Mathf.Max(1e-9f, phenotype.Parts[i].Volume);
                sum += positions[i] * v;
                volume += v;
            }

            return root + (volume > 1e-9f ? sum / volume : Vector3.zero);
        }

        private static bool Carries(Phenotype phenotype, string cellTypeId)
        {
            foreach (PhenotypePart part in phenotype.Parts)
            {
                if (part.CellTypeId == cellTypeId) return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- the frame

        public int BodyCount => _drawn.Count;

        public Vector3 PositionOf(int index) => _drawn[index].Centre;

        public Phenotype PhenotypeOf(int index) => _drawn[index].Phenotype;

        public bool AbsorptiveAt(int index) => _drawn[index].Absorptive;

        public bool PhotosyntheticAt(int index) => _drawn[index].Photosynthetic;

        /// <summary>The id of a drawn body, by its index in the frame.</summary>
        public long IdAt(int index) => _drawn[index].Id;

        /// <summary>A drawn body's index in the frame, or false when the frame does not draw it.</summary>
        public bool TryIndexOf(long id, out int index) => _indexOf.TryGetValue(id, out index);

        /// <summary>A drawn body's root transform, or null: what a camera follows.</summary>
        public Transform RootOf(long id) => View.RootOf(id);

        /// <summary>The window's births, deaths and bites in (from, to]: what a story's cut is timed on.</summary>
        public IEnumerable<FilmWindowEvent> EventsBetween(double from, double to) => Window.EventsBetween(from, to);

        // ---------------------------------------------------------------- the film's world (IFilmWorld)

        /// <summary>How far apart the two points of a velocity are read, s.</summary>
        public const float VelocitySpanSeconds = 1f;

        /// <summary>
        /// A body's root velocity at the frame on screen, from its recorded path: the move over the
        /// next second, or over the last second of the window at its end. Zero when the window
        /// never holds the body.
        /// </summary>
        public Vector3 VelocityOf(long id)
        {
            if (double.IsNaN(Second) || FrameCount < 2) return Vector3.zero;

            double t0 = Second, t1 = Second + VelocitySpanSeconds;
            if (t1 > LastSecond)
            {
                t1 = LastSecond;
                t0 = Math.Max(FirstSecond, t1 - VelocitySpanSeconds);
            }

            if (!(t1 - t0 > 1e-6)) return Vector3.zero;
            if (!RootAt(id, t0, out Vector3 a) || !RootAt(id, t1, out Vector3 b)) return Vector3.zero;

            Vector3 v = (b - a) / (float)(t1 - t0);
            return FilmPlans.Shot.Finite(v) ? v : Vector3.zero;
        }

        /// <summary>
        /// Where a body's recorded path carries its root from the frame on screen in
        /// <paramref name="seconds"/>: the farm's own path, and not a straight line. Held at its last
        /// frame past the window's end or its death. Zero when the window never holds it.
        /// </summary>
        public Vector3 DisplacementOf(long id, float seconds)
        {
            if (double.IsNaN(Second)) return Vector3.zero;
            if (!RootAt(id, Second, out Vector3 a) || !RootAt(id, Second + seconds, out Vector3 b)) return Vector3.zero;
            Vector3 d = b - a;
            return FilmPlans.Shot.Finite(d) ? d : Vector3.zero;
        }

        /// <summary>A body's recorded root at a second (<see cref="FilmWindowReader.TryRootAt"/>).</summary>
        public bool RootAt(long id, double second, out Vector3 root)
        {
            bool found = Window.TryRootAt(id, second, out float x, out float y, out float z);
            root = found ? new Vector3(x, y, z) : Vector3.zero;
            return found;
        }

        /// <summary>
        /// A drawn body's account: no reserve (a window does not record one) and its children in
        /// the window up to the frame on screen. False when the frame does not draw the body.
        /// </summary>
        public bool TryAccount(long id, out double reserve, out int children)
        {
            reserve = double.NaN;
            children = 0;
            if (!_indexOf.ContainsKey(id)) return false;
            children = Window.ChildrenOf(id, Second);
            return true;
        }

        /// <summary>
        /// A body's parent and its guild flags as the window has them, for a clade walk: the parent
        /// from its birth row in the window, or -1 for a body alive at the window's start (whose
        /// clade is the recording's); the flags from the last frame that held it, in
        /// <see cref="SafariClades.FlagsOf(bool, bool, bool)"/>'s bits.
        /// </summary>
        public bool TryLineageOf(long id, out long parent, out byte flags)
        {
            parent = Window.TryBirthOf(id, out FilmWindowEvent birth) ? birth.Parent : -1;
            flags = 255;
            if (!Window.TryFlagsOf(id, out int bits)) return false;
            flags = (byte)(bits & PoseStream.AllFlagBits);
            return true;
        }

        /// <summary>
        /// Two lines: what this is and how far to trust it, then which world, when, and how much
        /// of it is drawn.
        /// </summary>
        /// <remarks>
        /// The first line is the provenance, and its word is the verdict's. A film window's frame
        /// is neither a replay (nothing was stepped here) nor a reconstruction (every body stands
        /// where the farm's solver put it, joints and all), and the label names it as the third
        /// thing so that a still in <c>logbook/images/</c> says which of the three it is.
        /// </remarks>
        public string LabelFor(string view, string look)
        {
            FilmWindowVerdict verdict = Window.Verdict;
            var inv = CultureInfo.InvariantCulture;

            string size =
                _drawn.Count == 0 || SizedCount == 0 ? "adult size"
                : SizedCount >= _drawn.Count ? "recorded size"
                : "size for " + SizedCount + " of " + _drawn.Count;

            string extra = "";

            if (verdict == null) extra += "  no verdict";
            else if (!double.IsNaN(verdict.PartedAt))
            {
                extra += string.Format(inv, "  parted t={0:0.#} in {1}", verdict.PartedAt, verdict.PartedField ?? "?");
            }

            if (verdict != null && verdict.SourcesDiffer.Count > 0) extra += "  sources differ";
            if (SourceNote != null) extra += "  run differs";

            int unmatched = WithoutAGenome + PoseRefusals;
            if (unmatched > 0) extra += "  " + unmatched + " unmatched";

            return
                "FARM FILM WINDOW · " + ProvenanceWord + (OldRunRead ? " · OLD-RUN READ" : "") + "\n" +
                string.Format(
                    inv,
                    "{0}  t={1:0.###}s  frame {2}/{3}  {4} bodies  {5}  {6}  recorded pose, {7}{8}",
                    Record.ArmName ?? verdict?.Arm ?? "run", Second, FrameIndex + 1, FrameCount,
                    _drawn.Count, view, look, size, extra);
        }

        /// <summary>One line for a log: the frame on screen and its counts.</summary>
        public string Describe() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "film window {0} t={1:0.###} s frame {2}/{3}: {4} drawn, {5} built and {6} removed " +
                "so far, {7} resized, {8} rebuilt; {9} without a genome, {10} refused, {11} on an " +
                "older plan, {12} unreadable; genome {13}",
                ProvenanceWord, Second, FrameIndex + 1, FrameCount, _drawn.Count,
                View.Built, View.Removed, View.Resized, View.Rebuilt, WithoutAGenome,
                PoseRefusals, PlanFallbacks, Unreadable, Formats());

        private string Formats()
        {
            if (_formats.Count == 0) return "no row read";

            var parts = new List<string>(_formats.Count);
            foreach (KeyValuePair<int, int> pair in _formats)
            {
                parts.Add("format " + pair.Key + " on " + pair.Value +
                          (pair.Key == GenomeJson.FormatVersion ? ", strictly" : ", by the picture-only reader"));
            }

            return string.Join("; ", parts.ToArray());
        }

        private static string Seconds(double t) => t.ToString("0.###", CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------- housekeeping

        public void Dispose()
        {
            View?.Dispose();
            View = null;

            Window?.Dispose();
            Window = null;

            _grown.Clear();
            _drawn.Clear();
            _indexOf.Clear();

            if (_modeSet)
            {
                Physics.simulationMode = _previousMode;
                _modeSet = false;
            }
        }
    }
}
