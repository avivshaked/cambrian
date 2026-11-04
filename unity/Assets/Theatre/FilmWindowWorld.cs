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
    /// of the solver: a fraction of a centimetre. With <c>EVOSIM_THEATRE_GROWTH_EASE</c> on, the drawn
    /// size is eased between the recorded metabolic steps, lagging the record by up to one step. And the centre a camera frames on is the
    /// volume-weighted centre of the posed parts, without the water a swimming body entrains.
    /// </para>
    /// </remarks>
    public sealed class FilmWindowWorld : IDisposable, IFilmWorld, ICrumbleSource
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
            public double ReserveJoules;
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

        /// <summary>The drawn stroke, or null with its dial off or a window that logged none.</summary>
        private TheatreSwimStroke _stroke;

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

        /// <inheritdoc />
        public CoralRock Coral { get; private set; }

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
            set
            {
                View.Palette = value;
                if (CorpseView != null) CorpseView.Palette = value;
                if (RemainsView != null) RemainsView.Palette = value;

                // The bend with the current (EVOSIM_THEATRE_ALGA_BEND) reads a body's own velocity through the water
                // and the second the frame shows from here; read only while that dial is on.
                View.VelocityOf = VelocityOf;
                View.SecondOf = () => Second;
            }
        }

        /// <summary>Whether the palette paints the guilds.</summary>
        public bool ColourByCellType
        {
            get => View.ColourByCellType;
            set
            {
                View.ColourByCellType = value;
                if (CorpseView != null) CorpseView.ColourByCellType = value;
                if (RemainsView != null) RemainsView.ColourByCellType = value;
            }
        }

        /// <summary>
        /// The corpses (D127), in a view of their own that the palette paints as dead tissue, or null
        /// for a window that holds none.
        /// </summary>
        public LiveWorldView CorpseView { get; private set; }

        /// <summary>Corpses drawn in the frame shown.</summary>
        public int CorpsesDrawn { get; private set; }

        /// <summary>
        /// Whether the dial is on (<c>EVOSIM_THEATRE_REMAINS</c>, off at 0) and the run is one whose dead stay in the
        /// water as remains: contact eating on, with a window above 0. A run whose config has no such window, an older
        /// build's, never draws remains. Decided when the window opens.
        /// </summary>
        public bool RemainsOn { get; private set; }

        /// <summary>
        /// The remains (D220), in a view of their own that the palette paints dulled and part-grey and that fades each
        /// body by its age, or null with the dial off.
        /// </summary>
        public LiveWorldView RemainsView { get; private set; }

        /// <summary>Remains drawn in the frame shown.</summary>
        public int RemainsDrawn { get; private set; }

        /// <summary>Remains in the frame shown that no genome or map in the window could draw, or that fitted no pose.</summary>
        public int RemainsRefused { get; private set; }

        /// <summary>Bodies flagged as remains in the frame shown that the window has no remains row for, drawn as they always were.</summary>
        public int RemainsWithoutARow { get; private set; }

        private static readonly bool RemainsDial = TheatreSkin.Dial("EVOSIM_THEATRE_REMAINS", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// D277's crumble (<c>EVOSIM_THEATRE_CRUMBLE</c>, off at 0): dead tissue dwindles continuously to nothing, dulls, and sheds
        /// flakes of snow. Null with the dial off, or for a window that holds neither remains nor corpses to draw, and then no
        /// branch below is entered and nothing is drawn differently.
        /// </summary>
        public TheatreCrumble Crumble { get; private set; }

        /// <summary>The crumble's main dial, read here so that with it off <see cref="TheatreCrumble"/> is never touched.</summary>
        private static readonly bool CrumbleDial = TheatreSkin.Dial("EVOSIM_THEATRE_CRUMBLE", 0f, 0f, 1f) > 0f;

        /// <summary>Corpses this build could not develop, or whose body has no genome in the window.</summary>
        public int CorpsesWithoutABody { get; private set; }

        /// <summary>
        /// How long a newborn takes to grow in and fade in, seconds of the run at the film's own pace,
        /// from the first frame it is drawn in; a time-lapse stretches it by its <see cref="Speed"/>,
        /// so it takes this long on screen at any speed. 0 draws it whole at once, as every film did
        /// before 2026-09-28.
        /// </summary>
        private static readonly float BirthSeconds = TheatreSkin.Dial("EVOSIM_THEATRE_BIRTH_SECONDS", 1.5f, 0f, 30f);

        /// <summary>A newborn drawn at its first frame, as a fraction of its own size.</summary>
        private const float BirthStartScale = 0.25f;

        /// <summary>
        /// How long a corpse fades out before the run removes it at its floor (D127), seconds of the
        /// run. 0 lets it vanish between two frames, as every film did before 2026-09-28.
        /// </summary>
        private static readonly float CorpseFadeSeconds = TheatreSkin.Dial("EVOSIM_THEATRE_CORPSE_FADE_SECONDS", 600f, 0f, 36000f);

        /// <summary>
        /// Seconds of the run a second of film: 1 at the run's own pace, more for a time-lapse
        /// (<c>EVOSIM_THEATRE_FILM_SPEED</c>, set by the film).
        /// </summary>
        public double Speed { get; set; } = 1d;

        /// <summary>
        /// D277: the size, in metres, of the speck a newborn starts as (<c>EVOSIM_THEATRE_BIRTH_SPECK</c>).
        /// Above 0 a newborn is drawn solid from its first frame and grows from a speck this big, or from
        /// its whole size if that is smaller, instead of fading in. 0 leaves <see cref="BirthSeconds"/> as it was.
        /// </summary>
        private static readonly float BirthSpeck = TheatreSkin.Dial("EVOSIM_THEATRE_BIRTH_SPECK", 0f, 0f, 1f);

        /// <summary>How long the speck takes to grow to the body's whole size, seconds of film.</summary>
        private static readonly float BirthSpeckSeconds = TheatreSkin.Dial("EVOSIM_THEATRE_BIRTH_SPECK_SECONDS", 2f, 0.1f, 30f);

        /// <summary>Each newborn's starting scale under <see cref="BirthSpeck"/>, fixed when its birth starts; 1 for a body drawn whole.</summary>
        private readonly Dictionary<long, float> _speckStart = new Dictionary<long, float>();

        private bool _speckLogged;

        /// <summary>
        /// Eases the drawn size of a growing body between the recorded steps (<c>EVOSIM_THEATRE_GROWTH_EASE</c>).
        /// The recorded fraction changes in steps at the 0.5 s metabolic step, a few percent of linear size at a
        /// time for a young body, which at 1x shows as a little jump every fifteen frames. Above 0 the drawn
        /// fraction runs from the previous recorded value to the current one over <see cref="GrowthEaseSeconds"/>
        /// of the run, so it lags the record by at most one step and never runs ahead. At 1 the run is a smoothstep,
        /// which brings the swelling to rest at every step and so still breathes at 2 Hz; at 2 it is a straight
        /// line, so the rate holds while the steps are equal. Display only: the phenotype is still sized at the
        /// recorded fraction and the ease scales the placement. 0 leaves the drawing exactly as it was, with no
        /// state kept.
        /// </summary>
        private static readonly float GrowthEaseDial = TheatreSkin.Dial("EVOSIM_THEATRE_GROWTH_EASE", 0f, 0f, 2f);

        private static readonly bool GrowthEase = GrowthEaseDial > 0f;

        /// <summary>Whether the ease runs in a straight line (<c>EVOSIM_THEATRE_GROWTH_EASE=2</c>) rather than a smoothstep.</summary>
        private static readonly bool GrowthEaseLinear = GrowthEaseDial >= 1.5f;

        /// <summary>The seconds of the run one recorded step is eased over.</summary>
        private const float GrowthEaseSeconds = 0.5f;

        private struct EaseState
        {
            public float Prev;
            public float Cur;
            public double Since;
            public long Seen;
        }

        /// <summary>Made only with the dial on.</summary>
        private Dictionary<long, EaseState> _ease;

        private List<long> _easeForget;
        private double _easeLastSecond = double.NaN;
        private bool _easeLogged;

        /// <summary>Clears the state when the window's second goes backwards, and forgets bodies no longer drawn.</summary>
        private void EaseBeginFrame(int bodies)
        {
            if (_ease == null)
            {
                _ease = new Dictionary<long, EaseState>();
                _easeForget = new List<long>();
            }

            if (!double.IsNaN(_easeLastSecond) && Second < _easeLastSecond - 1e-9) _ease.Clear();
            _easeLastSecond = Second;

            if (_ease.Count > 2 * bodies + 64)
            {
                _easeForget.Clear();
                foreach (KeyValuePair<long, EaseState> e in _ease)
                {
                    if (e.Value.Seen < FramesShown - 1) _easeForget.Add(e.Key);
                }

                for (int i = 0; i < _easeForget.Count; i++) _ease.Remove(_easeForget[i]);
            }
        }

        /// <summary>The uniform factor, at most 1, that takes the recorded fraction to the eased one in linear size.</summary>
        private float EaseFactor(long id, float cur)
        {
            double now = Second;

            if (!_ease.TryGetValue(id, out EaseState s))
            {
                s = new EaseState { Prev = cur, Cur = cur, Since = now };
            }
            else if (cur != s.Cur)
            {
                s.Prev = s.Cur;
                s.Cur = cur;
                s.Since = now;
            }

            s.Seen = FramesShown;
            _ease[id] = s;

            float t = Mathf.Clamp01((float)((now - s.Since) / GrowthEaseSeconds));
            float eased = Mathf.Lerp(s.Prev, s.Cur, GrowthEaseLinear ? t : t * t * (3f - 2f * t));
            if (eased > cur) eased = cur; // never ahead of the record
            float factor = Mathf.Pow(eased / cur, 1f / 3f);

            if (factor < 0.999f && !_easeLogged)
            {
                _easeLogged = true;
                Debug.Log("[Theatre] film window: EVOSIM_THEATRE_GROWTH_EASE reached the drawing, body " + id + " drawn at " + factor.ToString("0.0000") + " of its recorded linear size");
            }

            return factor;
        }

        /// <summary>The second each body born inside the window was first drawn at.</summary>
        private readonly Dictionary<long, double> _firstDrawn = new Dictionary<long, double>();

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

                // Round 62's coral, grown from the run's config as the farm grew it (null at the
                // mushrooms and with EVOSIM_THEATRE_CORAL=0).
                world.Coral = TheatreCoralRock.PlaceFor(world.Record, out _);
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
            // D239: the verdict's physicsDt is the step in world seconds (the farm writes dt over k),
            // so this offset is world seconds with no factor here.
            world._startCut = verdict != null && !double.IsNaN(verdict.From)
                ? verdict.From + (double.IsNaN(verdict.PhysicsDt) ? 0d : 0.999d * verdict.PhysicsDt)
                : world.FirstSecond;

            world._previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            world._modeSet = true;

            world.View = new LiveWorldView(world.Record.Config, "Film Window");

            // EVOSIM_THEATRE_DRESS_NEAR: set by a still close film before Play mode, so the first frame shown already leaves the
            // one skins unbuilt. False in every other session, and then nothing here runs.
            if (TheatreDressSet.Wanted) world.View.DeferSkins();

            if (world.Window.CorpseCount > 0)
            {
                world.CorpseView = new LiveWorldView(world.Record.Config, "Film Window Corpses") { Corpses = true };
            }

            // D220's remains, drawn only with the dial on and only for a run whose dead stay in the water. With it
            // off the reader has parsed the remains rows and nothing here reads them: no view, no branch in Show.
            world.RemainsOn = RemainsDial && world.Record.Config.ContactEating && world.Record.Config.RemainsWindowSeconds > 0f;

            if (world.RemainsOn)
            {
                world.RemainsView = new LiveWorldView(world.Record.Config, "Film Window Remains") { Remains = true };
            }

            // D277's crumble, built only with its dial on and only where there is dead tissue to draw. With it off nothing is made.
            if (CrumbleDial && (world.RemainsView != null || world.CorpseView != null))
            {
                world.Crumble = new TheatreCrumble(
                    world, world.Window, world.Record.Config, CorpseFadeSeconds,
                    remainsDrawn: world.RemainsView != null, corpsesDrawn: world.CorpseView != null);
            }

            // The drawn stroke (D135 leaves it to the theatre), read only with its dial on: the
            // window's logged power, drawn at the run's own omega.
            if (TheatreSwimStroke.On)
            {
                FluidConfig fluid = world.Record.Config.Fluid;
                world._stroke = TheatreSwimStroke.Open(world.Window, fluid != null ? fluid.PowerPushRadiansPerSecond : 0d);
            }

            foreach (string note in world.Window.Notes) Debug.LogWarning("[Theatre] film window: " + note);

            Debug.Log(
                "[Theatre] film window " + world.Window.Describe() + "; drawn over " +
                world.Record.Path + " (" + (world.Record.ArmName ?? "run") + " seed " +
                world.Record.Seed + "): nothing is simulated, every body stands where the farm " +
                "put it");

            if (TheatreUptake.Enabled) TheatreUptake.Declare(world.Record.Path, world.FirstSecond, world.LastSecond);

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

            if (GrowthEase) EaseBeginFrame(poses.Bodies.Length); // nothing is made or kept with the dial off

            View.BeginFrame();
            _stroke?.BeginFrame(frame, Second);

            RemainsDrawn = 0;
            RemainsRefused = 0;
            RemainsWithoutARow = 0;
            RemainsView?.BeginFrame();

            for (int i = 0; i < poses.Bodies.Length; i++)
            {
                PoseBody body = poses.Bodies[i];
                long id = body.Id;

                // D220: a body flagged as remains is drawn by the remains view, when the dial is on and the window
                // has a row for it. Everything below is as it was for every other body, and for all of them with
                // the dial off.
                if (RemainsView != null && body.Flags >= 0 && (body.Flags & PoseStream.RemainsBit) != 0)
                {
                    if (Window.TryRemains(id, out FilmWindowRemains remains))
                    {
                        ShowRemains(body, remains);
                        continue;
                    }

                    RemainsWithoutARow++;
                }

                Grown grown = GrownOf(id);

                if (grown == null)
                {
                    WithoutAGenome++;
                    continue;
                }

                if (grown.Genome == null) continue;

                // The skin pattern's key, from the genome (EVOSIM_THEATRE_PATTERN; returns at once with it off).
                TheatrePattern.Register(id, grown.Genome);
                TheatreSkinMap.Register(id, grown.Genome, Record.Config); // EVOSIM_THEATRE_SKIN_MAP; returns at once with it off

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

                // The stroke D135 leaves to the theatre: the powered links swung about their pivots,
                // after the recorded pose is laid and before the view places the parts.
                _stroke?.Apply(id, phenotype, pose.Root, adult.Positions, adult.Rotations);

                // Shaded by its reserve as the stepped route shades it (LiveWorldView.Sync), from the
                // stream's version 3 field. A version 2 window carries none and is drawn fully fed,
                // which is what every window drew until 2026-09-25: the owner's review found every
                // body of round 48's first window clips bright green where the stepped route drew
                // the starving ones dark.
                float tint = float.IsNaN(body.ReserveSeconds)
                    ? 1f
                    : TheatrePalette.Tint(body.ReserveSeconds, Record.Config.EnergyFullScaleSeconds);

                // A newborn grows in from a quarter of its size and fades in over its first
                // BirthSeconds, so a birth reads as one and not as a body that pops into the water.
                // A body alive at the start of the window is drawn whole.
                float appear = Appear(id);
                float grow = Mathf.Lerp(BirthStartScale, 1f, appear);

                // D277: with the speck dial on, a newborn is solid from its first frame and grows from a speck.
                if (BirthSpeck > 0f)
                {
                    grow = SpeckScale(id, phenotype, adult.Positions);
                    appear = 1f;
                }

                // The growth ease: with the dial off this line is not reached and grow is untouched.
                if (GrowthEase && sized) grow *= EaseFactor(id, body.BodyFraction);

                // A pose that is not finite leaves the body where it last stood, inside the view.
                if (!View.Place(id, adult.Body, phenotype, pose.Root, adult.Positions, adult.Rotations, tint, grow, appear)) continue;

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

                    // The stream's version 4 field; NaN on an older window, whose account then
                    // marks the births and draws no line, as every window's did before it.
                    ReserveJoules = body.ReserveJoules,
                });
            }

            View.EndFrame();
            RemainsView?.EndFrame();

            ShowCorpses();

            // D277: which bodies crumble at this second, for the next camera render to draw their flakes.
            Crumble?.Advance(Second);

            // D279's birth cloud (EVOSIM_THEATRE_BIRTH_CLOUD, off at 0): the newborns this frame draws for the first time, for the next
            // camera render to draw their clouds. With the dial off nothing is touched.
            if (TheatreBirthCloud.On) TheatreBirthCloud.Frame(this);

            // The death cue (EVOSIM_THEATRE_DEATH_CUE, off at 0): the death rows this frame reaches, for the next camera render to draw
            // their dull clouds. With the dial off nothing is touched.
            if (TheatreDeathCue.On) TheatreDeathCue.Frame(this);

            // Oxygen pearling (EVOSIM_THEATRE_PEARLING, off at 0): the leaf parts this frame draws and the light income each earns, for
            // the next camera render to draw their pearls. With the dial off nothing is touched.
            if (TheatrePearling.On) TheatrePearling.Frame(this);
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
        public int DressAll() => View.DressUndressed() + (CorpseView?.DressUndressed() ?? 0) + (RemainsView?.DressUndressed() ?? 0);

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
        private Adult AdultFor(long id, Grown grown, int dof, out bool fellBack) => AdultFor(id, grown, dof, Second, out fellBack);

        /// <summary>
        /// The adult the joints of a frame at <paramref name="second"/> fit, by the same rule: what a frame not yet on screen
        /// will be drawn on (<see cref="TryUnborn"/>).
        /// </summary>
        private Adult AdultFor(long id, Grown grown, int dof, double second, out bool fellBack)
        {
            fellBack = false;
            IReadOnlyList<FilmWindowPlan> plans = Window.PlansOf(id);

            int primary = -1;

            for (int i = 0; i < plans.Count; i++)
            {
                double at = plans[i].Seconds;
                if (at <= _startCut || at < second - 1e-6) primary = i;
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
        public Transform RootOf(long id) => View.RootOf(id) ?? RemainsView?.RootOf(id);

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
        /// A drawn body's account: its reserve in joules as the frame recorded it (NaN on a window
        /// older than the stream's version 4) and its children in the window up to the frame on
        /// screen. False when the frame does not draw the body.
        /// </summary>
        public bool TryAccount(long id, out double reserve, out int children)
        {
            reserve = double.NaN;
            children = 0;
            if (!_indexOf.TryGetValue(id, out int index)) return false;
            reserve = _drawn[index].ReserveJoules;
            children = Window.ChildrenOf(id, Second);
            return true;
        }

        /// <summary>
        /// A body's remains as the frame on screen draws them (D220): their drawn root, the reach and the count of the parts
        /// held at this second, found as the crumble measures them, the second they began, and the second the
        /// run removes them, that and its remains window. False with the remains dial off, for an id the window has no remains
        /// row for, and for remains the frame does not draw.
        /// </summary>
        public bool TryRemains(long id, out FilmRemains found)
        {
            found = default;
            if (RemainsView == null || !Window.TryRemains(id, out FilmWindowRemains remains)) return false;

            Transform root = RemainsView.RootOf(id);
            if (root == null || !FilmPlans.Shot.Finite(root.position)) return false;

            RemainsGrown grown = RemainsGrownOf(remains);
            if (grown == null || grown.Failed) return false;

            RemainsAdult adult = null;

            for (int i = RemainsMapAt(remains, Second); i >= 0 && adult == null; i--)
            {
                RemainsAdult candidate = RemainsAdultOf(grown, remains, i);
                if (!candidate.Failed) adult = candidate;
            }

            if (adult == null) return false;

            found = new FilmRemains
            {
                Root = root.position,
                Reach = SnapshotCamera.ReachOf(adult.Body),
                Parts = adult.Body.PartCount,
                Since = remains.Since,
                Removed = remains.Since + Record.Config.RemainsWindowSeconds,
                View = RemainsView,
            };
            return true;
        }

        /// <summary>
        /// Where a body born later in the window will first stand (D279's birth film): the root of the first frame that holds
        /// it, the box its parts fill there at their recorded size, and its reach, posed and sized as <see cref="Show"/> will
        /// draw it at that frame before the grow-in scales it, with the second of its birth row. False for an id with no birth
        /// row in the window, for a birth at or before the second on screen, for a body no frame holds, and for a first pose
        /// this build cannot lay on its body. Reads that one frame and develops the body as its first <see cref="Show"/> will;
        /// nothing drawn is touched.
        /// </summary>
        public bool TryUnborn(long id, out FilmUnborn found)
        {
            found = default;
            if (double.IsNaN(Second) || !Window.TryBirthOf(id, out FilmWindowEvent birth)) return false;
            if (!(birth.Seconds > Second + 1e-6)) return false;
            if (!Window.TryFramesOf(id, out int first, out _)) return false;

            PoseFrame poses = Window.ReadFrame(first);
            PoseBody body = default;
            bool held = false;

            foreach (PoseBody candidate in poses.Bodies)
            {
                if (candidate.Id != id) continue;
                body = candidate;
                held = true;
                break;
            }

            if (!held) return false;

            Grown grown = GrownOf(id);
            if (grown?.Genome == null) return false;

            RecordedPose pose = RecordedPoses.From(body);
            if (!FilmPlans.Shot.Finite(pose.Root)) return false;

            Adult adult = AdultFor(id, grown, pose.Joint.Length, poses.Seconds, out _);
            if (adult == null) return false;

            bool sized = body.FractionRecorded && body.BodyFraction > 0f;
            Phenotype phenotype = SizedFor(id, adult, sized ? body.BodyFraction : 1f);
            int parts = phenotype.PartCount;

            // Arrays of its own, so the adult's, which the drawn frame lays its poses in, are left as they are.
            var positions = new Vector3[parts];
            var rotations = new Quaternion[parts];
            if (!RecordedPoses.Apply(phenotype, pose, positions, rotations, out _)) return false;

            // Each part's box turned by its rotation, as an axis-aligned box about its place: the parts hang from the root
            // unturned (LiveWorldView.Place), so a part's place in the world is the root and its offset.
            Bounds box = default;
            bool any = false;

            for (int i = 0; i < parts; i++)
            {
                Float3 h = phenotype.Parts[i].HalfExtents;
                Quaternion r = rotations[i];
                Vector3 ax = r * new Vector3(Mathf.Abs(h.X), 0f, 0f);
                Vector3 ay = r * new Vector3(0f, Mathf.Abs(h.Y), 0f);
                Vector3 az = r * new Vector3(0f, 0f, Mathf.Abs(h.Z));
                Vector3 extent = new Vector3(
                    Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x),
                    Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y),
                    Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
                Vector3 centre = pose.Root + positions[i];
                if (!FilmPlans.Shot.Finite(centre) || !FilmPlans.Shot.Finite(extent)) continue;

                var part = new Bounds(centre, 2f * extent);
                if (any) box.Encapsulate(part);
                else { box = part; any = true; }
            }

            if (!any) return false;

            found = new FilmUnborn
            {
                Root = pose.Root,
                Box = box,
                Reach = SnapshotCamera.ReachOf(phenotype),
                Born = birth.Seconds,
                FirstFrame = poses.Seconds,
            };
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
            if (CorpseView != null) extra += "  " + CorpsesDrawn + " corpses";
            if (RemainsView != null) extra += "  " + RemainsDrawn + " remains";

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

        // ---------------------------------------------------------------- the corpses (D127)

        /// <summary>
        /// A corpse's body, developed once: its adult at the plan it died on, scaled to the size it
        /// died at, laid out straight in the attitude it died in.
        /// </summary>
        private sealed class CorpseBody
        {
            public Phenotype Adult;
            public Phenotype Body;
            public Vector3[] Positions;
            public Quaternion[] Rotations;

            /// <summary>The volume-weighted centre of the laid-out parts, from the root.</summary>
            public Vector3 Centre;
        }

        private readonly Dictionary<long, CorpseBody> _corpseBodies = new Dictionary<long, CorpseBody>();
        private readonly List<FilmWindowCorpsePlace> _corpsePlaces = new List<FilmWindowCorpsePlace>();

        /// <summary>
        /// Puts every corpse standing at this frame's second where the window has it, shrunk by what
        /// it has lost, in its own view.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The world's point is the body's centre.</b> The farm reports a body at its centre of
        /// mass and a corpse is founded there, so the laid-out body hangs from the root at which its
        /// shrunk centre lands on that point.
        /// </para>
        /// <para>
        /// <b>It shrinks by the cube root of what it holds.</b> A corpse's joules are its tissue, and a
        /// tissue's joules are its volume, so a corpse holding a share of what it was is drawn at the
        /// cube root of that share in every length: 0.37 of its first size when it leaves at a
        /// twentieth. The shrink is a scale on the view's root and not a new phenotype, so a corpse is
        /// dressed once and never rebuilt.
        /// </para>
        /// <para>
        /// <b>What it cannot show.</b> The pose a corpse keeps is its root's attitude at death, and
        /// its joints are drawn straight, because no joint angle is kept past the death. A corpse the
        /// window has no genome for is not drawn, and counted.
        /// </para>
        /// </remarks>
        private void ShowCorpses()
        {
            CorpsesDrawn = 0;
            if (CorpseView == null) return;

            Window.CorpsesAt(Second, _corpsePlaces);
            CorpseView.BeginFrame();

            for (int i = 0; i < _corpsePlaces.Count; i++)
            {
                FilmWindowCorpsePlace place = _corpsePlaces[i];
                CorpseBody corpse = CorpseBodyOf(place.Corpse);
                if (corpse == null) continue;

                // D277: with the crumble on, a corpse dwindles continuously to nothing over the stretch the fade took, solid to the end.
                if (Crumble != null && Crumble.CorpsesTimed)
                {
                    if (ShowCrumblingCorpse(place, corpse)) CorpsesDrawn++;
                    continue;
                }

                float shrink = Mathf.Pow(Mathf.Clamp(place.Fraction, 1e-3f, 1f), 1f / 3f);
                Vector3 root = new Vector3(place.X, place.Y, place.Z) - shrink * corpse.Centre;

                if (CorpseView.Place(
                        CorpseKey(place.Corpse.Id), corpse.Adult, corpse.Body, root,
                        corpse.Positions, corpse.Rotations, 0f, shrink, CorpseFade(place.Fraction)))
                {
                    CorpsesDrawn++;
                }
            }

            CorpseView.EndFrame();
        }

        /// <summary>
        /// A corpse's key in its own view and in the palette's cache: never a living body's id, so the
        /// palette cannot take a corpse for the body it was.
        /// </summary>
        private static long CorpseKey(long id) => -(id + 1L);

        /// <summary>
        /// How far a newborn is through its entrance: 0 at the first frame it is drawn in, 1 after
        /// <see cref="BirthSeconds"/>, eased. 1 for a body alive at the start of the window.
        /// </summary>
        private float Appear(long id)
        {
            if (!(BirthSeconds > 0f) || !Window.TryBirthOf(id, out FilmWindowEvent birth)) return 1f;

            if (!_firstDrawn.TryGetValue(id, out double first) || Second < first)
            {
                // Timed from the frame it first appears in, a metabolic step or so after its birth
                // row. A film that opens more than a second after the birth times it from the
                // birth itself, so a body born long before the first frame is not drawn arriving.
                first = Second - birth.Seconds <= 1.0 ? Second : birth.Seconds;
                _firstDrawn[id] = first;
            }

            float u = Mathf.Clamp01((float)(Second - first) / (BirthSeconds * (float)Math.Max(1d, Speed)));
            return u * u * (3f - 2f * u);
        }

        /// <summary>
        /// The scale of a newborn under <see cref="BirthSpeck"/> (D277): <c>s0^(1 - smoothstep(u))</c>, with
        /// <c>s0</c> the speck over the body's largest extent, timed from the first frame it is drawn in as
        /// <see cref="Appear"/> times it. 1 for a body alive at the start of the window or born before the
        /// film's first frame.
        /// </summary>
        private float SpeckScale(long id, Phenotype phenotype, Vector3[] positions)
        {
            if (!Window.TryBirthOf(id, out FilmWindowEvent birth)) return 1f;

            if (!_firstDrawn.TryGetValue(id, out double first) || Second < first)
            {
                first = Second - birth.Seconds <= 1.0 ? Second : birth.Seconds;
                _firstDrawn[id] = first;
                _speckStart.Remove(id);
            }

            if (!_speckStart.TryGetValue(id, out float s0))
            {
                // Fixed once, when the birth starts, so a body growing in its first seconds keeps its own curve.
                float extent = 0f;

                if (Second - birth.Seconds <= 1.0)
                {
                    Vector3 lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    Vector3 hi = -lo;

                    for (int i = 0; i < phenotype.PartCount; i++)
                    {
                        Float3 h = phenotype.Parts[i].HalfExtents;
                        Vector3 c = positions[i];
                        lo = Vector3.Min(lo, new Vector3(c.x - h.X, c.y - h.Y, c.z - h.Z));
                        hi = Vector3.Max(hi, new Vector3(c.x + h.X, c.y + h.Y, c.z + h.Z));
                    }

                    Vector3 size = hi - lo;
                    extent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                }

                s0 = extent > 1e-6f ? Mathf.Min(1f, BirthSpeck / extent) : 1f;
                _speckStart[id] = s0;

                if (!_speckLogged && s0 < 1f)
                {
                    _speckLogged = true;
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] birth speck: EVOSIM_THEATRE_BIRTH_SPECK={0} EVOSIM_THEATRE_BIRTH_SPECK_SECONDS={1}, first newborn {2}: s0={3} extent={4} m",
                        BirthSpeck, BirthSpeckSeconds, id, s0, extent));
                }
            }

            if (s0 >= 1f) return 1f;

            float u = Mathf.Clamp01((float)(Second - first) / (BirthSpeckSeconds * (float)Math.Max(1d, Speed)));
            float eased = u * u * (3f - 2f * u);
            return u >= 1f ? 1f : Mathf.Pow(s0, 1f - eased);
        }

        /// <summary>
        /// How much of a corpse is drawn as it nears the floor at which the run removes it (D127): 1
        /// until <see cref="CorpseFadeSeconds"/> before it leaves, then eased to 0 at the floor. Read
        /// from the share it still holds, so the fade ends where the run removes it, whatever
        /// second the window opens at.
        /// </summary>
        private float CorpseFade(float fraction)
        {
            float floor = Record.Config.CorpseLeavesBelowFraction;
            float rate = Record.Config.CorpseDecayPerSecond;
            if (!(floor > 0f) || !(rate > 0f) || !(CorpseFadeSeconds > 0f)) return 1f;

            float top = floor * Mathf.Exp(rate * CorpseFadeSeconds);
            float u = Mathf.Clamp01((fraction - floor) / (top - floor));
            return u * u * (3f - 2f * u);
        }

        /// <summary>A corpse's body, developed and laid out on first sight, or null when it cannot be.</summary>
        private CorpseBody CorpseBodyOf(FilmWindowCorpse corpse)
        {
            if (_corpseBodies.TryGetValue(corpse.Id, out CorpseBody found)) return found;

            CorpseBody body = null;
            Grown grown = GrownOf(corpse.Id);

            if (grown == null || grown.Genome == null)
            {
                CorpsesWithoutABody++;
            }
            else
            {
                try
                {
                    body = LayOut(grown.Genome, corpse);
                    if (body == null) CorpsesWithoutABody++;
                }
                catch (Exception e)
                {
                    CorpsesWithoutABody++;

                    if (_firstUnreadable == null)
                    {
                        _firstUnreadable = "corpse of creature " + corpse.Id + ": " + e.Message;
                        Debug.LogWarning("[Theatre] film window: a corpse this build could not develop, not drawn: " + _firstUnreadable);
                    }
                }
            }

            _corpseBodies[corpse.Id] = body;
            return body;
        }

        /// <summary>
        /// How far a corpse's body reaches at its first size, m, for a shot to frame it by: drawn, it is
        /// this times the cube root of the share of its joules it holds. NaN when the window has no
        /// such corpse or this build cannot develop it.
        /// </summary>
        public float CorpseReach(long id)
        {
            if (!Window.TryCorpse(id, out FilmWindowCorpse corpse)) return float.NaN;

            CorpseBody body = CorpseBodyOf(corpse);
            return body != null ? SnapshotCamera.ReachOf(body.Body) : float.NaN;
        }

        private CorpseBody LayOut(Genome genome, FilmWindowCorpse corpse)
        {
            Phenotype adult = SnapshotWorld.DevelopWithPlan(genome, Record.Config, corpse.ModuleCounts, corpse.LostPaths);
            if (adult.PartCount == 0) return null;

            float scale = corpse.ScaledBy;
            Phenotype sized = scale > 0f && scale < 1f ? adult.Scaled(scale, Record.Config.Shapes) : adult;

            var pose = new RecordedPose
            {
                Root = Vector3.zero,
                Attitude = corpse.HasPose ? new Quaternion(corpse.Qx, corpse.Qy, corpse.Qz, corpse.Qw).normalized : Quaternion.identity,
                Joint = new float[DofOf(sized)],
            };

            var body = new CorpseBody
            {
                Adult = adult,
                Body = sized,
                Positions = new Vector3[sized.PartCount],
                Rotations = new Quaternion[sized.PartCount],
            };

            if (!RecordedPoses.Apply(sized, pose, body.Positions, body.Rotations, out _)) return null;

            body.Centre = CentreOf(sized, Vector3.zero, body.Positions);
            return body;
        }

        // ---------------------------------------------------------------- the remains (D220)

        /// <summary>
        /// A remains body developed at one map, and the arrays its pose is laid into: the genome developed on the
        /// plan the remains row names, scaled to the size its body stood at, and selected down to the parts the row
        /// still holds, as a checkpoint restores a remains body.
        /// </summary>
        private sealed class RemainsAdult
        {
            public Phenotype Body;
            public int Dof;
            public bool Failed;
            public Vector3[] Positions = new Vector3[0];
            public Quaternion[] Rotations = new Quaternion[0];
        }

        private sealed class RemainsGrown
        {
            public Genome Genome;
            public Phenotype Developed;
            public bool Failed;
            public RemainsAdult[] ByMap;
        }

        private readonly Dictionary<long, RemainsGrown> _remainsGrown = new Dictionary<long, RemainsGrown>();

        /// <summary>
        /// Draws one remains body where the frame has it: the map that was current at the frame and whose joints
        /// the pose fits, dulled by the palette and faded by its age.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Which map.</b> A first row stands from the body's first frame. A shed row is a plan change the farm
        /// applies at a metabolic step and rebuilds the solver body for at the next physics step, so a frame takes
        /// the newest shed row written strictly before it, as <see cref="AdultFor"/> takes a plan, except those
        /// written as the window opened. Where that map's joints do not fit the frame's, the older maps are tried.
        /// </para>
        /// <para>
        /// <b>The fade.</b> One minus the share of the run's remains window gone since the body's remains began, from
        /// the first frame it is drawn, so a body that has lain half the window is drawn at half. The farm closes
        /// the remains at the window's end, and the fade ends there.
        /// </para>
        /// </remarks>
        private void ShowRemains(PoseBody body, FilmWindowRemains remains)
        {
            long id = body.Id;
            RemainsGrown grown = RemainsGrownOf(remains);

            if (grown == null || grown.Failed)
            {
                RemainsRefused++;
                return;
            }

            RecordedPose pose = RecordedPoses.From(body);
            IReadOnlyList<FilmWindowRemainsMap> maps = remains.Maps;

            int primary = 0;

            for (int i = 1; i < maps.Count; i++)
            {
                double at = maps[i].Seconds;
                if (at <= _startCut || at < Second - 1e-6) primary = i;
                else break;
            }

            RemainsAdult adult = null;

            for (int i = primary; i >= 0 && adult == null; i--)
            {
                RemainsAdult candidate = RemainsAdultOf(grown, remains, i);
                if (!candidate.Failed && candidate.Dof == pose.Joint.Length) adult = candidate;
            }

            if (adult == null)
            {
                RemainsRefused++;
                RemainsView.Keep(id);
                return;
            }

            int parts = adult.Body.PartCount;

            if (adult.Positions.Length < parts)
            {
                adult.Positions = new Vector3[parts];
                adult.Rotations = new Quaternion[parts];
            }

            if (!RecordedPoses.Apply(adult.Body, pose, adult.Positions, adult.Rotations, out _))
            {
                RemainsRefused++;
                RemainsView.Keep(id);
                return;
            }

            float window = Record.Config.RemainsWindowSeconds;
            float fade = 1f;

            if (window > 0f && !double.IsNaN(remains.Since))
            {
                fade = Mathf.Clamp01(1f - (float)((Second - remains.Since) / window));
            }

            // The death cue's drain (EVOSIM_THEATRE_DEATH_CUE): how far the body has drained to grey since its death row, -1 for
            // none, which is all it is with the dial off and is then the view's own default.
            float drain = TheatreDeathCue.On ? TheatreDeathCue.DrainOf(this, id) : -1f;

            // D277: with the crumble on, the remains are solid to the end and dwindle continuously to nothing over the last share
            // of their window, dulling as they go. A body gone to a speck is not placed and not kept, so the view's sweep removes it
            // as it does a body the frame no longer holds.
            if (Crumble != null && window > 0f && !double.IsNaN(remains.Since))
            {
                float crumbled = TheatreCrumble.ProgressOfRemains(Second - remains.Since, window);
                float shown = TheatreCrumble.ScaleAt(crumbled);
                Crumble.Trace(id, false, Second, crumbled, shown, shown);

                if (shown < TheatreCrumble.GoneBelow) return;

                if (RemainsView.Place(id, adult.Body, adult.Body, pose.Root, adult.Positions, adult.Rotations, 0f, shown, 1f, crumbled, drain))
                {
                    RemainsDrawn++;
                }

                return;
            }

            if (RemainsView.Place(id, adult.Body, adult.Body, pose.Root, adult.Positions, adult.Rotations, 0f, 1f, fade, 0f, drain))
            {
                RemainsDrawn++;
            }
        }

        private RemainsGrown RemainsGrownOf(FilmWindowRemains remains)
        {
            if (_remainsGrown.TryGetValue(remains.Id, out RemainsGrown found)) return found;

            var grown = new RemainsGrown { ByMap = new RemainsAdult[remains.Maps.Count] };

            if (!Window.TryRemainsGenome(remains.Id, out string row))
            {
                grown.Failed = true;
            }
            else
            {
                try
                {
                    try
                    {
                        grown.Genome = GenomeJson.Read(row);
                    }
                    catch (Exception)
                    {
                        grown.Genome = PictureGenome.Read(row, out _);
                        OldRunRead = true;
                    }

                    // The adult on the plan the remains were cut on, then the size their body stood at: what a
                    // checkpoint restores before it selects the parts (WorldState.ReadOrganism).
                    Phenotype adult = SnapshotWorld.DevelopWithPlan(grown.Genome, Record.Config, remains.ModuleCounts, remains.LostPaths);
                    float scale = remains.ScaledBy;
                    grown.Developed = scale > 0f && scale < 1f ? adult.Scaled(scale, Record.Config.Shapes) : adult;
                    grown.Failed = grown.Developed.PartCount == 0;
                }
                catch (Exception e)
                {
                    grown.Failed = true;

                    if (_firstUnreadable == null)
                    {
                        _firstUnreadable = "remains of creature " + remains.Id + ": " + e.Message;
                        Debug.LogWarning("[Theatre] film window: remains this build could not develop, not drawn: " + _firstUnreadable);
                    }
                }
            }

            _remainsGrown[remains.Id] = grown;
            return grown;
        }

        private RemainsAdult RemainsAdultOf(RemainsGrown grown, FilmWindowRemains remains, int mapIndex)
        {
            RemainsAdult adult = grown.ByMap[mapIndex];
            if (adult != null) return adult;

            adult = new RemainsAdult();

            try
            {
                int[] map = remains.Maps[mapIndex].Map;
                adult.Body = map == null ? grown.Developed : grown.Developed.Select(map);
                adult.Dof = DofOf(adult.Body);
                adult.Failed = adult.Body.PartCount == 0;
            }
            catch (Exception e)
            {
                adult.Failed = true;

                if (_firstUnreadable == null)
                {
                    _firstUnreadable = "remains of creature " + remains.Id + " at map " + mapIndex + ": " + e.Message;
                    Debug.LogWarning("[Theatre] film window: " + _firstUnreadable);
                }
            }

            grown.ByMap[mapIndex] = adult;
            return adult;
        }

        // ---------------------------------------------------------------- the crumble (D277)

        /// <summary>Scratch for the crumble's reading of corpses, and its cache of every corpse's place by whole second then id. Made on first use, so null with the dial off.</summary>
        private List<FilmWindowCorpsePlace> _crumblePlaces;

        private Dictionary<long, Dictionary<long, FilmWindowCorpsePlace>> _crumbleBySecond;

        /// <summary>
        /// Draws one corpse under the crumble: solid, at the shrink its share of joules gives times the crumble's scale, hung from
        /// where its centre stands at that scale so that it dwindles about itself. False when it has dwindled to nothing and is
        /// not placed, which the view's sweep then removes.
        /// </summary>
        private bool ShowCrumblingCorpse(FilmWindowCorpsePlace place, CorpseBody corpse)
        {
            float c = Crumble.ProgressOfCorpse(place.Fraction);
            float shrink = Mathf.Pow(Mathf.Clamp(place.Fraction, 1e-3f, 1f), 1f / 3f);
            float shown = TheatreCrumble.ScaleAt(c);
            float scale = shrink * shown;
            Crumble.Trace(place.Corpse.Id, true, Second, c, shown, scale);

            if (shown < TheatreCrumble.GoneBelow) return false;

            Vector3 root = new Vector3(place.X, place.Y, place.Z) - scale * corpse.Centre;

            return CorpseView.Place(
                CorpseKey(place.Corpse.Id), corpse.Adult, corpse.Body, root, corpse.Positions, corpse.Rotations, 0f, scale, 1f, c);
        }

        /// <summary>
        /// The index of the remains map in force at a second: the newest shed row written strictly before it, except the rows
        /// written as the window opened, as <see cref="ShowRemains"/> takes it.
        /// </summary>
        private int RemainsMapAt(FilmWindowRemains remains, double second)
        {
            IReadOnlyList<FilmWindowRemainsMap> maps = remains.Maps;
            int primary = 0;

            for (int i = 1; i < maps.Count; i++)
            {
                double at = maps[i].Seconds;
                if (at <= _startCut || at < second - 1e-6) primary = i;
                else break;
            }

            return primary;
        }

        /// <summary>
        /// A corpse's place at a second, held to the window's samples of it as remains are held to their frames: no earlier than its
        /// first sample, so a flake born before it takes that place, and no later than the last second the window holds it, so a corpse
        /// that has left still has one. Read at the whole second below, and every corpse's place at a whole second is worked out once
        /// and kept, so a flake costs a lookup by id and not a walk of all the corpses.
        /// </summary>
        private bool TryCorpsePlace(FilmWindowCorpse corpse, double second, out FilmWindowCorpsePlace found)
        {
            found = default(FilmWindowCorpsePlace);
            if (corpse.SampleCount == 0) return false;

            double first = corpse.SampleSeconds(0);
            double at = Math.Min(Math.Max(second, first), corpse.Gone - 0.01d);
            if (at < first) return false;

            if (_crumblePlaces == null) _crumblePlaces = new List<FilmWindowCorpsePlace>();

            double whole = Math.Floor(at);

            // Before its first whole second the corpse is read where it is, with a walk of the list.
            if (whole < first)
            {
                Window.CorpsesAt(at, _crumblePlaces);

                for (int i = 0; i < _crumblePlaces.Count; i++)
                {
                    if (!ReferenceEquals(_crumblePlaces[i].Corpse, corpse)) continue;

                    found = _crumblePlaces[i];
                    return true;
                }

                return false;
            }

            if (_crumbleBySecond == null) _crumbleBySecond = new Dictionary<long, Dictionary<long, FilmWindowCorpsePlace>>();
            if (_crumbleBySecond.Count > 2048) _crumbleBySecond.Clear();

            long key = (long)whole;

            if (!_crumbleBySecond.TryGetValue(key, out Dictionary<long, FilmWindowCorpsePlace> byId))
            {
                Window.CorpsesAt(whole, _crumblePlaces);

                byId = new Dictionary<long, FilmWindowCorpsePlace>(_crumblePlaces.Count);
                for (int i = 0; i < _crumblePlaces.Count; i++) byId[_crumblePlaces[i].Corpse.Id] = _crumblePlaces[i];

                _crumbleBySecond[key] = byId;
            }

            return byId.TryGetValue(corpse.Id, out found);
        }

        bool ICrumbleSource.CorpseSample(FilmWindowCorpse corpse, int sample, out double second, out float fraction)
        {
            second = corpse.SampleSeconds(sample);
            fraction = 0f;

            if (_crumblePlaces == null) _crumblePlaces = new List<FilmWindowCorpsePlace>();

            // At the sample's own second the place is the sample's.
            Window.CorpsesAt(second, _crumblePlaces);

            for (int i = 0; i < _crumblePlaces.Count; i++)
            {
                if (!ReferenceEquals(_crumblePlaces[i].Corpse, corpse)) continue;

                fraction = _crumblePlaces[i].Fraction;
                return true;
            }

            return false;
        }

        bool ICrumbleSource.Measure(CrumbleBody body, out float volume, out float reach)
        {
            volume = 0f;
            reach = 0f;

            if (body.Corpse)
            {
                // Corpses that are not drawn are not developed, and do not count among the ones without a body.
                if (CorpseView == null) return false;

                CorpseBody corpse = CorpseBodyOf(body.CorpseRow);
                if (corpse == null) return false;

                // At the start of its crumble a corpse holds the share of its first size its fade begins at.
                float held = Crumble.CorpseTop;
                volume = TheatreCrumble.BoxVolume(corpse.Body) * held;
                reach = SnapshotCamera.ReachOf(corpse.Body) * Mathf.Pow(held, 1f / 3f);
                return true;
            }

            RemainsGrown grown = RemainsGrownOf(body.Remains);
            if (grown == null || grown.Failed) return false;

            // The parts it held as its crumble began.
            RemainsAdult adult = null;

            for (int i = RemainsMapAt(body.Remains, body.StartSecond); i >= 0 && adult == null; i--)
            {
                RemainsAdult candidate = RemainsAdultOf(grown, body.Remains, i);
                if (!candidate.Failed) adult = candidate;
            }

            if (adult == null) return false;

            volume = TheatreCrumble.BoxVolume(adult.Body);
            reach = SnapshotCamera.ReachOf(adult.Body);
            return true;
        }

        bool ICrumbleSource.PositionOf(CrumbleBody body, double second, out Vector3 position)
        {
            position = Vector3.zero;

            if (!body.Corpse) return RootAt(body.Id, second, out position);
            if (!TryCorpsePlace(body.CorpseRow, second, out FilmWindowCorpsePlace place)) return false;

            position = new Vector3(place.X, place.Y, place.Z);
            return true;
        }

        int ICrumbleSource.FrameFor(CrumbleBody body, double second)
        {
            if (body.Corpse) return -2;
            if (!Window.TryFramesOf(body.Id, out int first, out int last)) return -1;

            return Mathf.Clamp(Window.NearestFrame(second), first, last);
        }

        PoseFrame ICrumbleSource.ReadFrame(int frame) => Window.ReadFrame(frame);

        bool ICrumbleSource.Lay(CrumbleBody body, PoseFrame poses, double second, CrumbleLayout layout)
        {
            if (body.Corpse)
            {
                if (CorpseView == null) return false;

                CorpseBody corpse = CorpseBodyOf(body.CorpseRow);
                if (corpse == null || !TryCorpsePlace(body.CorpseRow, second, out FilmWindowCorpsePlace place)) return false;

                layout.Shape = corpse.Body;
                layout.Positions = corpse.Positions;
                layout.Rotations = corpse.Rotations;
                layout.Anchor = new Vector3(place.X, place.Y, place.Z);
                layout.Centre = corpse.Centre;
                layout.BaseScale = Mathf.Pow(Mathf.Clamp(place.Fraction, 1e-3f, 1f), 1f / 3f);
                return true;
            }

            if (poses == null) return false;

            RemainsGrown grown = RemainsGrownOf(body.Remains);
            if (grown == null || grown.Failed) return false;

            PoseBody found = default(PoseBody);
            bool have = false;

            for (int i = 0; i < poses.Bodies.Length; i++)
            {
                if (poses.Bodies[i].Id != body.Id) continue;

                found = poses.Bodies[i];
                have = true;
                break;
            }

            if (!have) return false;

            RecordedPose pose = RecordedPoses.From(found);
            RemainsAdult adult = null;

            for (int i = RemainsMapAt(body.Remains, poses.Seconds); i >= 0 && adult == null; i--)
            {
                RemainsAdult candidate = RemainsAdultOf(grown, body.Remains, i);
                if (!candidate.Failed && candidate.Dof == pose.Joint.Length) adult = candidate;
            }

            if (adult == null) return false;

            int parts = adult.Body.PartCount;

            if (layout.Scratch.Length < parts)
            {
                layout.Scratch = new Vector3[parts];
                layout.ScratchRotations = new Quaternion[parts];
            }

            if (!RecordedPoses.Apply(adult.Body, pose, layout.Scratch, layout.ScratchRotations, out _)) return false;

            layout.Shape = adult.Body;
            layout.Positions = layout.Scratch;
            layout.Rotations = layout.ScratchRotations;
            layout.Anchor = pose.Root;
            layout.Centre = Vector3.zero;
            layout.BaseScale = 1f;
            return true;
        }

        // ---------------------------------------------------------------- housekeeping

        public void Dispose()
        {
            Crumble?.Dispose();
            Crumble = null;

            View?.Dispose();
            View = null;

            CorpseView?.Dispose();
            CorpseView = null;
            _corpseBodies.Clear();

            RemainsView?.Dispose();
            RemainsView = null;
            _remainsGrown.Clear();

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
