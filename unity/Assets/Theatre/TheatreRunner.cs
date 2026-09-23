using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// The theatre: a recorded world played back with rendering on (Mode B), or one creature
    /// swimming alone under its own brain (Mode A) — DESIGN.md §6.1, D075.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The farm and the theatre are separate programs.</b> Evaluation is headless, ugly and
    /// fast; presentation is slow, beautiful, and reads stored runs. They share a serialization
    /// format and nothing else — so this component reads a run directory and never writes into
    /// one, and nothing in <c>Evosim.Sim</c> references this assembly.
    /// </para>
    /// <para>
    /// <b>What is on screen is a re-run, and the HUD says whether it is the right one.</b> The
    /// identity check compares the live world against the run's own <c>stats.jsonl</c> at every
    /// sample; a viewer therefore always knows whether they are watching the run or a cousin of
    /// it. That is the difference between a showpiece and an instrument.
    /// </para>
    /// </remarks>
    public sealed class TheatreRunner : MonoBehaviour
    {
        public enum ViewMode
        {
            /// <summary>Mode B: the whole recorded world.</summary>
            World = 0,

            /// <summary>Mode A: one creature, alone, no economy.</summary>
            Solo = 1,

            /// <summary>
            /// A still drawn from the run's own files, with nothing simulated — the snapshot
            /// render (<c>logbook/specs/snapshot-render-spec.md</c>, 2026-09-18). Selected by
            /// <c>EVOSIM_THEATRE_SNAP_FROM=snapshot</c> and driven by
            /// <c>TheatreSnapshot</c>, which asks for one second at a time.
            /// </summary>
            Snapshot = 2,
        }

        [Header("What to show")]
        [Tooltip("World replays a recorded run. Solo grows one genome and watches it swim.")]
        public ViewMode Mode = ViewMode.World;

        [Header("Mode B — the recorded world")]
        [Tooltip("A run directory (runs/<arm>/<timestamp>-<hash>), or the arm directory above " +
                 "it, in which case the newest run in it is taken. EVOSIM_THEATRE_RUN overrides.")]
        public string RunDirectory = "";

        [Tooltip("Play a run this build did not record. It is then not a faithful replay, and " +
                 "the overlay says so.")]
        public bool AllowSourceMismatch;

        [Tooltip("Run unpaced with the camera off until the world reaches this simulated second, " +
                 "then render. 0 does nothing. EVOSIM_THEATRE_SEEK overrides.")]
        public float SeekToSeconds;

        [Header("Mode B — continuing from a farm checkpoint")]
        [Tooltip("A run directory holding a checkpoints/ directory, the arm directory above it, " +
                 "or one .ckpt file. Set, the world is restored from that checkpoint and carried " +
                 "on live instead of being founded at t=0. EVOSIM_THEATRE_CHECKPOINT overrides.")]
        public string CheckpointPath = "";

        [Tooltip("The simulated second to continue from: the checkpoint at it, or the last one " +
                 "before it. 0 takes the last checkpoint the run holds. EVOSIM_THEATRE_SEEK sets " +
                 "this when a checkpoint is named.")]
        public float CheckpointSeconds;

        [Header("Mode A — one creature")]
        [Tooltip("A snapshots/*.jsonl file, or any file holding one genome. " +
                 "EVOSIM_THEATRE_GENOME overrides.")]
        public string GenomePath = "";

        [Tooltip("Which row of that file, 0-based. Ignored when Genome Id is set.")]
        public int GenomeRow;

        [Tooltip("The creature to find in the file by its id, or -1 to take the row above.")]
        public long GenomeId = -1;

        [Tooltip("Where to put it, metres below the surface.")]
        public float SoloDepthMetres = 12f;

        [Tooltip("Only reaches the nutrient field the creature's nose smells.")]
        public ulong SoloSeed = 1;

        [Tooltip("Detritus to lay into every layer, J/m3, so the Chemical channel has something " +
                 "to read. 0 uses the world's initial field, which in a reference world is empty. " +
                 "Anything else is water a viewer invented, and the overlay says so.")]
        public float SoloSmellDensity;

        [Tooltip("Drive the body with the test sine instead of its brain — the null to see the " +
                 "brain's contribution against.")]
        public bool TestSine;

        [Tooltip("Let its reserve run down, so a viewer can see what hunger does to a gait.")]
        public bool Starve;

        [Header("Pace")]
        public bool Paused;

        [Tooltip("Simulated seconds per wall-clock second. 1 is real time.")]
        public float Rate = 1f;

        [Tooltip("For filming: hold the pace at or under real time whatever the rate says, so a " +
                 "recorded frame is one physics step's worth of motion and not forty (the look's " +
                 "design pass, F4). L toggles it.")]
        public bool PaceLock;

        /// <summary>The rate the world is actually stepped at: the rate, or one under the lock.</summary>
        public float EffectiveRate => PaceLock ? Mathf.Min(1f, Rate) : Rate;

        [Tooltip("Never spend more than this fraction of a frame stepping, so the Editor stays " +
                 "responsive when the world is larger than the pace can serve.")]
        public float FrameBudgetSeconds = 0.05f;

        [Tooltip("Wall-clock seconds per frame given to a seek. Higher seeks faster and makes " +
                 "the Editor less responsive while it runs.")]
        public float SeekBudgetSeconds = 0.25f;

        [Tooltip("Live mode only: simulated seconds between census lines on the console, which " +
                 "is where the live cut's census is.")]
        public float LiveLogSeconds = 50f;

        [Header("View")]
        [Tooltip("Paint each part by what it is made of, and each body by how much reserve it " +
                 "holds. An instrument, not the creature's appearance (§5A.5).")]
        public bool ColourByCellType = true;

        public Color PhotosyntheticColour = new Color(0.34f, 0.72f, 0.36f);
        public Color AbsorptiveColour = new Color(0.90f, 0.55f, 0.22f);
        public Color StructuralColour = new Color(0.72f, 0.74f, 0.78f);

        [Tooltip("Brightness of a body with nothing left, against a sated one.")]
        public float StarvingBrightness = 0.22f;

        [Tooltip("Bodies repainted per frame. The whole population is repainted in rotation, so " +
                 "in a world of thousands the tint lags by a second or two.")]
        public int RepaintsPerFrame = 96;

        public bool ShowOverlay = true;

        [Tooltip("The camera to fly and to follow with.")]
        public TheatreCamera FlyCamera;

        [Tooltip("Draws the surface and the sea floor, so depth is visible. Optional.")]
        public WaterBounds Water;

        [Tooltip("How far the water grid reaches from the origin, metres. Creatures are tiled " +
                 "100 m apart, so this is a few tiles' worth of lattice, not the world's area.")]
        public float WaterExtentMetres = 600f;

        // ---------------------------------------------------------------- state

        private TheatreReplay _replay;
        private TheatreDynamicsReplay _live;
        private LiveWorldView _liveView;
        private SoloCreature _solo;
        private SnapshotWorld _recon;
        private readonly CreatureIdMap _map = new CreatureIdMap();
        private readonly TheatrePalette _palette = new TheatrePalette();

        /// <summary>
        /// The look: dark field lighting, the water's fog, the sea bed, the snow, and the
        /// materials the palette paints with.
        /// </summary>
        /// <remarks>
        /// A plain object and safe in a field initializer, unlike the palette's property block:
        /// nothing in its constructor makes an engine object. It lives under
        /// <c>Assets/Theatre</c>, so none of it is in <c>simHash</c> and a change to the look
        /// cannot orphan a recording.
        /// </remarks>
        private readonly TheatreSkin _skin = new TheatreSkin();
        private readonly TheatreGrade _grade = new TheatreGrade();

        private string _error;
        private double _pending;
        private bool _seeking;
        private double _seekTarget;
        private double _seekFrom;

        private long _selectedId = -1;

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _pacedFrom;
        private double _pacedAt;
        private double _measuredPace;
        private long _repaintCursor;
        private double _saidAt = double.NegativeInfinity;

        /// <summary>
        /// The interface — the strip, the census, the warnings, the popover, the inspector and the
        /// bar. Null when its assets are missing, in which case the run plays with nothing drawn
        /// over it rather than not at all.
        /// </summary>
        /// <remarks>
        /// It replaced an <c>OnGUI</c> block of five methods on 2026-09-12 (design/SPEC.md). The
        /// runner keeps what it always kept — the pace, the pause, the seek and the selection —
        /// and hands them over once a frame; the interface reads the replay, the census and the
        /// record for itself. Nothing in it can decide a trajectory, and all of it sits under
        /// <c>Assets/Theatre</c>, outside <c>simHash</c>.
        /// </remarks>
        private TheatreUi _ui;

        /// <summary>
        /// The replay on screen, or null when Mode B has not opened one.
        /// </summary>
        /// <remarks>
        /// Read by the Play-mode identity check (<c>TheatreIdentityCheck.RunInPlayMode</c>), which
        /// drives this component's own <c>Update</c> and then reads the verdict off it rather than
        /// running a second loop of its own. A loop of its own is what the edit-mode check does,
        /// and what it could not see: the fault in logbook/0083 lives in the difference between
        /// one step per frame and tens of them.
        /// </remarks>
        public TheatreReplay Replay => _replay;

        /// <summary>
        /// The world being stepped live on <c>Evosim.Dynamics</c>, or null when this is not the
        /// live mode. Read by the headless check, which drives this component's own loop.
        /// </summary>
        public TheatreDynamicsReplay Live => _live;

        /// <summary>The bodies the live world is drawing, or null. The check counts them.</summary>
        public LiveWorldView LiveView => _liveView;

        /// <summary>
        /// The reconstruction on screen, or null when this is not Mode Snapshot.
        /// </summary>
        /// <remarks>
        /// Read by <c>TheatreSnapshot</c>, which asks it for a second and waits for
        /// <see cref="SnapshotWorld.Ready"/> before it photographs anything — the same shape as
        /// the replay's drive loop, where the wait is for a clock rather than for a build.
        /// </remarks>
        public SnapshotWorld Reconstruction => _recon;

        /// <summary>Why nothing opened, or null. The same string the interface prints.</summary>
        public string Error => _error;

        /// <summary>The interface, for the Play-mode check to query. Null when it did not load.</summary>
        public TheatreUi Ui => _ui;

        /// <summary>
        /// The camera the viewer flies, or null. What <see cref="TheatreUiCapture"/> renders under
        /// the chrome, so a picture of the interface is a picture of the theatre and not of a
        /// panel floating on nothing.
        /// </summary>
        public Camera ViewCamera => FlyCamera != null ? FlyCamera.GetComponent<Camera>() : null;

        /// <summary>The creature the viewer has selected, or -1.</summary>
        public long SelectedId => _selectedId;

        /// <summary>True while the world is being run forward with the camera off.</summary>
        public bool Seeking => _seeking;

        /// <summary>Where a seek is heading, when one is running.</summary>
        public double SeekTarget => _seekTarget;

        /// <summary>Simulated seconds per wall-clock second, measured over a window.</summary>
        public double MeasuredPace => _measuredPace;

        private void Start()
        {
            // The skin first, and the scene's own light off with it: the shaders the palette
            // paints with come from here, and a body gathered before the material exists would
            // keep the plain one until its root changed.
            Camera view = FlyCamera != null ? FlyCamera.GetComponent<Camera>() : null;

            _skin.Apply(view);
            _skin.SilenceSceneLights();
            _grade.Apply();
            TheatreGrade.Attach(view);
            _palette.Skin = _skin;

            _palette.Photosynthetic = PhotosyntheticColour;
            _palette.Absorptive = AbsorptiveColour;
            _palette.Structural = StructuralColour;
            _palette.Starving = StarvingBrightness;

            string run = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN");
            if (!string.IsNullOrEmpty(run)) RunDirectory = run;

            string genome = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_GENOME");
            if (!string.IsNullOrEmpty(genome)) { GenomePath = genome; Mode = ViewMode.Solo; }

            // The snapshot render's own switch. Anything but "snapshot" leaves the theatre
            // exactly as it was, which is the spec's first requirement.
            if (string.Equals(
                    Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SNAP_FROM"), "snapshot",
                    StringComparison.OrdinalIgnoreCase))
            {
                Mode = ViewMode.Snapshot;
            }

            // A checkpoint to carry on from. Named, it decides which world opens — the run
            // directory beside it is the world's, so EVOSIM_THEATRE_RUN has nothing left to say.
            string checkpoint = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_CHECKPOINT");
            if (!string.IsNullOrEmpty(checkpoint)) CheckpointPath = checkpoint;

            string seek = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SEEK");
            if (!string.IsNullOrEmpty(seek) &&
                float.TryParse(seek, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float seekTo))
            {
                // One variable, two meanings, decided by whether a checkpoint was named: seeking
                // to a second and starting from one are the same wish, and a script that asks for
                // t=400 of a run that holds a checkpoint there should not have to know which of
                // the two the theatre will do about it.
                if (!string.IsNullOrWhiteSpace(CheckpointPath)) CheckpointSeconds = seekTo;
                else SeekToSeconds = seekTo;
            }

            if (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_OVERRIDE") == "1")
            {
                AllowSourceMismatch = true;
            }

            // Before the run opens, because opening is what fills it in. Never in the snapshot
            // mode: the interface reads a replay's census and a reconstruction has none, which is
            // also why -Chrome is refused there.
            if (Mode != ViewMode.Snapshot)
            {
                _ui = TheatreUi.Create();
                if (_ui != null) _ui.Visible = ShowOverlay;
            }
            else
            {
                ShowOverlay = false;
            }

            OpenWhateverModeSays();
        }

        private void OpenWhateverModeSays()
        {
            Close();
            _error = null;

            try
            {
                if (Mode == ViewMode.World) OpenWorld();
                else if (Mode == ViewMode.Snapshot) OpenSnapshot();
                else OpenSolo();
            }
            catch (Exception e)
            {
                _error = e.GetType().Name + ": " + e.Message;
                Debug.LogError("[Theatre] " + _error);
            }

            TellTheInterface();
        }

        /// <summary>What opened, or what stopped it opening.</summary>
        private void TellTheInterface()
        {
            if (_ui == null) return;

            if (_error != null) { _ui.ShowError(_error); return; }
            if (_replay != null) { _ui.OpenWorld(_replay, _map, _replay.Record.Path); return; }
            if (_live != null) { _ui.OpenLive(_live, _live.Record.Path); return; }
            if (_solo != null) { _ui.OpenSolo(_solo); return; }

            _ui.ShowError("nothing opened, and nothing said why");
        }

        private void OpenWorld()
        {
            // A checkpoint carries its own run directory with it, so it is asked first and
            // RunDirectory is not required at all when one is named.
            if (!string.IsNullOrWhiteSpace(CheckpointPath))
            {
                OpenLive();
                return;
            }

            if (string.IsNullOrWhiteSpace(RunDirectory))
            {
                _error =
                    "No run directory. Set it on the Theatre Runner in the scene, or launch with " +
                    "EVOSIM_THEATRE_RUN pointing at runs/<arm>.";
                return;
            }

            // Which engine stepped the recording, before anything tries to rebuild it. Two farms
            // record runs now (CLAUDE.md, 2026-09-22) and they are not replayable by the same
            // code: the Editor's needs a scene and an ArticulationBody per link, the console
            // farm's needs neither and steps Evosim.Dynamics instead. Every manifest written
            // before the second farm existed has no `engine` field and is read as PhysX, so this
            // branch cannot fire on a run in the record and the path below is the one it has
            // always been.
            string engine = RunRecord.PeekEngine(RunDirectory);

            if (string.Equals(engine, TheatreDynamicsReplay.EngineName, StringComparison.Ordinal))
            {
                OpenLive();
                return;
            }

            _replay = TheatreReplay.Open(RunDirectory, AllowSourceMismatch, out string refusal);

            if (_replay == null)
            {
                _error = refusal;
                Debug.LogWarning("[Theatre] refused: " + refusal);
                return;
            }

            Debug.Log(
                $"[Theatre] {_replay.Record.ArmName} seed {_replay.Record.Seed}, " +
                $"dt {_replay.Record.PhysicsDtSeconds}, config {_replay.Record.ConfigHash}, " +
                $"physics jobs {_replay.PhysicsJobWorkers}" +
                (_replay.ThreadCaveat != null ? " (" + _replay.ThreadCaveat + ")" : "") + ", " +
                (_replay.Faithful ? "same source as the recording" : "SOURCE DIFFERS: " + _replay.SourceDifference));

            DressTheWorld(_replay);

            if (SeekToSeconds > 0f) BeginSeek(SeekToSeconds);
        }

        /// <summary>
        /// The live mode: the world the console farm's own harness is stepping, right now, on
        /// <c>Evosim.Dynamics</c>, drawn as it steps.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>It is live and not a replay, and the difference is the whole of the labelling.</b>
        /// Mode B on PhysX re-runs a recording and asks at every sample whether what is on screen
        /// is the run; here the world is stepped from the run's own config and seed, and the
        /// identity check is not asked to decide anything — the console line says whether this
        /// build is the build that recorded it, and that is the reader's whole claim. So a source
        /// mismatch is allowed rather than refused: a world that is honestly a cousin is still a
        /// world worth watching, and one that cannot be opened at all shows nothing.
        /// </para>
        /// <para>
        /// <b>The interface is up, and it reads this world.</b> The live cut of 2026-09-22 took
        /// the panel down as it opened, because every reading on it was typed on
        /// <see cref="TheatreReplay"/>; the census went to the console instead. The owner's use
        /// for live play is to film it and to read the numbers on screen, so
        /// <see cref="TheatreUi.OpenLive"/> now takes the world and asks it the same questions —
        /// and the two or three a live world has no answer to say so where they sit rather than
        /// showing a stale cell. The console census stays beside it: a session's log is worth
        /// having whether or not anyone was watching.
        /// </para>
        /// </remarks>
        private void OpenLive()
        {
            // Continued from a recorded second, or founded from the run's config and seed: the
            // difference is where the world starts, and nothing after this line cares which.
            string refusal;

            if (!string.IsNullOrWhiteSpace(CheckpointPath))
            {
                _live = TheatreDynamicsReplay.Continue(
                    CheckpointPath, CheckpointSeconds, out refusal);
            }
            else
            {
                _live = TheatreDynamicsReplay.Open(RunDirectory, true, out refusal);
            }

            if (_live == null)
            {
                _error = refusal;
                Debug.LogWarning("[Theatre] refused: " + refusal);
                return;
            }

            _liveView = new LiveWorldView(_live.Sim)
            {
                Palette = _palette,
                ColourByCellType = ColourByCellType,
                RepaintsPerFrame = RepaintsPerFrame,
            };

            Debug.Log(
                $"[Theatre] LIVE · dynamics — {_live.Record.ArmName} seed {_live.Record.Seed}, " +
                $"dt {_live.Sim.PhysicsDt} s, threads {_live.Threads}, " +
                $"config {_live.Record.ConfigHash}, " +
                (_live.Faithful
                    ? "same source as the recording, so this is that run's own trajectory"
                    : "SOURCE DIFFERS: " + _live.SourceDifference +
                      " — this is a cousin of the recorded world, not it") +
                ". The census is on this console and on the panel, which reads this world.");

            // Where this world was picked up from, said in full where there is room for it: the
            // label on a frame carries the clause and the digests do not fit on one.
            if (_live.ContinuedFrom != null)
            {
                Debug.Log("[Theatre] LIVE · dynamics — " + _live.ContinuedFrom.Line());
            }

            DressTheWorld(_live);

            // A world restored at 400 s is already past a seek to 400 s, and BeginSeek would
            // rather be told that than asked to run backwards.
            if (SeekToSeconds > _live.ElapsedSeconds) BeginSeek(SeekToSeconds);
        }

        /// <summary>
        /// The water, the glass, the seams, the sand and the rest of the skin, from the run's own
        /// config.
        /// </summary>
        /// <remarks>
        /// One method for both a replayed world and a reconstructed one
        /// (<see cref="SnapshotWorld"/>), so that a picture of the second is comparable with a
        /// picture of the first at a glance. It reads a frame rather than a replay for that
        /// reason and no other: everything here comes from the config and the floor.
        /// </remarks>
        private void DressTheWorld(ITheatreFrame frame)
        {
            if (Water != null)
            {
                RunConfig water = frame.Record.Config;

                // D077. A recording of a shared-space run has a literal box, so the theatre draws
                // that box and the seams inside it rather than a lattice grid — the patch width
                // from the world's own fields (sqrt(area / K)), never recomputed here. The layout
                // comes off the config too (fable-propose-box.md), or the picture would be of a
                // box the run was not in. A tiled recording gets the grid it always got.
                // fable-propose-aquarium.md ruling 1: the shape is read from the config like every
                // other number here, never inferred. A tank of 100 m2 and a square box of 100 m2
                // differ by their corners, and a viewer who cannot tell which they are watching
                // cannot tell whether a body is against the glass or in open water. The radius
                // comes from TankGeometry, the same square root World derived it with, rather than
                // from a second one taken here — the rule the patch width below is read under.
                if (water.SharedSpace && water.WorldShape == WorldShape.Tank)
                {
                    Water.ShowTank(
                        water.WorldDepthMetres,
                        TankGeometry.RadiusFor(water.WorldAreaSquareMetres),
                        Mathf.Max(1, (int)water.HorizontalPatches),
                        // D092: the floor's circles and its verticals are drawn at the floor's own
                        // height where there is one, so the outline meets the sand rather than
                        // cutting a plane through it. Null on every recording before it, at which
                        // every line is at −depth exactly as it was.
                        frame.Bed);
                }
                else if (water.SharedSpace)
                {
                    int patches = Mathf.Max(1, (int)water.HorizontalPatches);
                    int across = Mathf.Clamp((int)water.PatchesAcross, 1, patches);

                    Water.ShowBox(
                        water.WorldDepthMetres,
                        Mathf.Sqrt(water.WorldAreaSquareMetres / patches),
                        Mathf.Max(1, patches / across),
                        across);
                }
                else
                {
                    Water.Show(water.WorldDepthMetres, WaterExtentMetres, Ecosystem.TileSpacing);
                }

                // The skin's bed is a real renderer with sand on it, so the immediate mode quad
                // goes; the grid and the seams above it stay.
                Water.DrawBed = false;
            }

            // The box from the run's own config, taken from the one place that works it out
            // (SnapshotCamera.BoxOf) rather than a second copy of sqrt(area / K) here. The floor's
            // shape comes off the world the replay actually built (D092), so the sand is draped on
            // the same height map the collider has; null on every recording before it.
            RunConfig dressed = frame.Record.Config;
            float glassRadius = dressed.SharedSpace && dressed.WorldShape == WorldShape.Tank
                ? TankGeometry.RadiusFor(dressed.WorldAreaSquareMetres)
                : 0f;

            _skin.Dress(SnapshotCamera.BoxOf(frame, out _), frame.Bed, glassRadius, frame.Reefs);
        }

        /// <summary>
        /// Mode Snapshot: the run's water and floor, with no bodies in it yet.
        /// </summary>
        /// <remarks>
        /// The bodies come one second at a time from <see cref="ShowSnapshotSecond"/>, because the
        /// caller photographs several seconds of one run and each is a different crowd. Nothing
        /// is stepped here and nothing is checked against the record: the log says so, and every
        /// frame says so in its label.
        /// </remarks>
        private void OpenSnapshot()
        {
            if (string.IsNullOrWhiteSpace(RunDirectory))
            {
                _error =
                    "No run directory. Set it on the Theatre Runner in the scene, or launch with " +
                    "EVOSIM_THEATRE_RUN pointing at runs/<arm>.";
                return;
            }

            _recon = SnapshotWorld.Open(RunDirectory, out string refusal);

            if (_recon == null)
            {
                _error = refusal;
                Debug.LogWarning("[Theatre] refused: " + refusal);
                return;
            }

            _recon.Palette = _palette;
            _recon.ColourByCellType = ColourByCellType;

            Debug.Log(
                "[Theatre] " + (_recon.Record.ArmName ?? "run") + " seed " + _recon.Record.Seed +
                ", config " + _recon.Record.ConfigHash +
                ", reconstructed from snapshots and positions.jsonl: nothing is simulated, every " +
                "body is drawn at its adult size, in the pose poses.jsonl recorded for it where " +
                "the run wrote one and in the developer's own frame where it did not");

            DressTheWorld(_recon);
        }

        /// <summary>Draws the world as the run's files have it at one snapshot second.</summary>
        public void ShowSnapshotSecond(double second)
        {
            _recon?.Begin(second);
        }

        private void OpenSolo()
        {
            if (string.IsNullOrWhiteSpace(GenomePath))
            {
                _error =
                    "No genome. Point Genome Path at a snapshots/*.jsonl row, or launch with " +
                    "EVOSIM_THEATRE_GENOME.";
                return;
            }

            // The water comes from a run's config when one is named, so a creature swims in the
            // world it evolved in rather than in a default ocean.
            RunConfig config;
            string water;

            if (!string.IsNullOrWhiteSpace(RunDirectory))
            {
                RunRecord record = RunRecord.Load(RunDirectory);
                config = record.Config;
                water = record.ArmName ?? "the run's config";
                Ecosystem.ConfigurePhysicsStep(record.PhysicsDtSeconds);
            }
            else
            {
                config = new RunConfig();
                water = "RunConfig defaults (no run directory given)";
            }

            Genome genome = SoloCreature.ReadGenome(
                GenomePath, GenomeRow, GenomeId, out long foundId, out string description);

            _solo = SoloCreature.Build(
                genome, config, SoloSeed, SoloDepthMetres, foundId,
                description + " — water from " + water + ", patch 0", SoloSmellDensity);

            _solo.UseTestSine = TestSine;
            _solo.Starving = Starve;

            if (FlyCamera != null) FlyCamera.Follow(_solo.Instance.Root.transform, _solo.BodyRadius());

            // A tighter grid: one creature is metres across, not kilometres.
            if (Water != null)
            {
                Water.Show(config.WorldDepthMetres, 40f, 5f);
                Water.DrawBed = false;
            }

            // Mode A has no box, so the skin is dressed around the creature: the same water and
            // the same snow, over a footprint a viewer can see the edges of.
            float depth = Mathf.Max(1f, config.WorldDepthMetres);

            _skin.Dress(new Bounds(
                new Vector3(0f, -0.5f * depth, 0f), new Vector3(40f, depth, 40f)));

            Debug.Log(
                $"[Theatre] solo: {description}, {_solo.Phenotype.PartCount} parts, " +
                $"{_solo.Instance.TotalDof} DOF, water from {water}");
        }

        private void Close()
        {
            _replay?.Dispose();
            _replay = null;
            _liveView?.Dispose();
            _liveView = null;
            _live?.Dispose();
            _live = null;
            _solo?.Dispose();
            _solo = null;
            _recon?.Dispose();
            _recon = null;
            _map.Clear();
            _palette.Clear();
            _skin.Undress();

            if (Water != null)
            {
                Water.Hide();
                Water.DrawBed = true;
            }
            _selectedId = -1;
            _pending = 0d;
            _seeking = false;
        }

        private void OnDisable() => Close();

        /// <summary>
        /// Puts the render settings back, and takes the interface's panel down with it.
        /// </summary>
        /// <remarks>
        /// The fog and the ambient are global, so leaving Play mode with the theatre's dark field
        /// still set would follow the owner into the next scene they opened. The panel is the same
        /// kind of thing: a UIDocument and its PanelSettings are objects this component made, and
        /// an interface still drawing over a scene nobody opened it in is the same surprise.
        /// </remarks>
        private void OnDestroy()
        {
            _grade.Dispose();
            _skin.Dispose();
            _ui?.Dispose();
            _ui = null;
        }

        // ---------------------------------------------------------------- the loop

        private void Update()
        {
            ReadKeys();

            if (_replay != null) StepWorld();
            else if (_live != null) StepLive();
            else if (_solo != null) StepSolo();
            else if (_recon != null) _recon.BuildSome(FrameBudgetSeconds);

            DrawTheInterface();
        }

        /// <summary>The lights follow the viewer: the key rakes from behind wherever the fly camera looks.</summary>
        private void LateUpdate()
        {
            if (FlyCamera != null) _skin.Aim(FlyCamera.transform.rotation);
        }

        /// <summary>
        /// Hands the interface the four things it cannot see for itself, once a frame.
        /// </summary>
        /// <remarks>
        /// The pace, the pause, the seek and the selection all live here, because they are the
        /// viewer's state rather than the world's; everything else on screen the interface reads
        /// off the replay, the census and the record. What it does with them is four text fields
        /// and one width per frame — the cadence contract of design/SPEC.md §9 item 6.
        /// </remarks>
        private void DrawTheInterface()
        {
            if (_ui == null) return;

            _ui.Visible = ShowOverlay;

            _ui.Tick(new TheatreUiState
            {
                Paused = Paused,
                Rate = Rate,
                MeasuredPace = _measuredPace,
                Seeking = _seeking,
                SeekFrom = _seekFrom,
                SeekTarget = _seekTarget,
                SelectedId = _selectedId,
                SelectedSpeed = SelectedSpeed(),
            });
        }

        /// <summary>
        /// The selected creature's root speed, m/s, or 0.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Read here rather than in the interface because the scene is the runner's: the map, the
        /// transform and the articulation are all things the interface deliberately never touches.
        /// </para>
        /// <para>
        /// The live world has no articulation to ask, so the number comes off the solver itself —
        /// <c>Creature.Velocity</c> is the world velocity of each link's origin in metres a
        /// second, and the root link's is the one the other engine's <c>linearVelocity</c> is.
        /// </para>
        /// </remarks>
        private float SelectedSpeed()
        {
            if (_selectedId < 0) return 0f;

            if (_live != null)
            {
                if (!_live.Sim.TryPose(_selectedId, out Evosim.Dynamics.Creature body) ||
                    body == null || body.Velocity.Length < 3)
                {
                    return 0f;
                }

                double vx = body.Velocity[0], vy = body.Velocity[1], vz = body.Velocity[2];
                return (float)Math.Sqrt(vx * vx + vy * vy + vz * vz);
            }

            if (_replay == null) return 0f;

            Transform root = _map.RootOf(_selectedId);
            if (root == null || root.childCount == 0) return 0f;

            var body2 = root.GetChild(0).GetComponent<ArticulationBody>();
            return body2 != null ? body2.linearVelocity.magnitude : 0f;
        }

        private void StepWorld()
        {
            double before = _replay.ElapsedSeconds;
            double wallBefore = _clock.Elapsed.TotalSeconds;

            if (_seeking)
            {
                // Rendering off, unpaced. The camera is disabled rather than every renderer
                // switched: forty thousand renderer toggles per seek cost more than the frames
                // they save, and a disabled camera is the same "no camera work".
                if (FlyCamera != null) FlyCamera.GetComponent<Camera>().enabled = false;

                double deadline = wallBefore + Mathf.Max(0.02f, SeekBudgetSeconds);

                while (_replay.ElapsedSeconds < _seekTarget &&
                       _clock.Elapsed.TotalSeconds < deadline)
                {
                    _replay.Step(_map);
                }

                if (_replay.ElapsedSeconds >= _seekTarget) EndSeek();
            }
            else if (!Paused)
            {
                _pending += Time.unscaledDeltaTime * Mathf.Max(0f, EffectiveRate);

                float dt = _replay.Record.PhysicsDtSeconds;
                double deadline = wallBefore + Mathf.Max(0.005f, FrameBudgetSeconds);

                while (_pending >= dt && _clock.Elapsed.TotalSeconds < deadline)
                {
                    _replay.Step(_map);
                    _pending -= dt;
                }

                // Falling behind is a fact about the world's size, not a debt to repay: carrying
                // the shortfall forward would make the viewer sprint whenever it caught a breath.
                if (_pending > 4d * dt) _pending = 0d;
            }

            MeasurePace(before, wallBefore);
            Repaint();
        }

        /// <summary>
        /// <see cref="StepWorld"/>'s loop on the other engine, with the scene brought up to the
        /// world once at the end rather than once per step.
        /// </summary>
        /// <remarks>
        /// <b>The pace controls are the same controls.</b> <c>Space</c>, <c>[</c>, <c>]</c> and
        /// the filming lock all act on <c>Paused</c> and <see cref="EffectiveRate"/>, which
        /// <see cref="ReadKeys"/> sets without caring which engine is running, so nothing here had
        /// to be duplicated to keep them working. The frame budget is the same promise too: an
        /// Editor that spends a whole frame inside the solver is an Editor that looks wedged.
        /// </remarks>
        private void StepLive()
        {
            double before = _live.ElapsedSeconds;
            double wallBefore = _clock.Elapsed.TotalSeconds;
            float dt = Mathf.Max(1e-4f, _live.Sim.PhysicsDt);

            if (_seeking)
            {
                if (FlyCamera != null) FlyCamera.GetComponent<Camera>().enabled = false;

                double deadline = wallBefore + Mathf.Max(0.02f, SeekBudgetSeconds);

                while (_live.ElapsedSeconds < _seekTarget &&
                       _clock.Elapsed.TotalSeconds < deadline)
                {
                    _live.Step();
                }

                if (_live.ElapsedSeconds >= _seekTarget) EndSeek();
            }
            else if (!Paused)
            {
                _pending += Time.unscaledDeltaTime * Mathf.Max(0f, EffectiveRate);

                double deadline = wallBefore + Mathf.Max(0.005f, FrameBudgetSeconds);

                while (_pending >= dt && _clock.Elapsed.TotalSeconds < deadline)
                {
                    if (_live.Step()) SayTheCensus();
                    _pending -= dt;
                }

                if (_pending > 4d * dt) _pending = 0d;
            }

            MeasurePace(before, wallBefore);

            // Once a frame, and never per physics step: the solver takes tens of steps between
            // two frames and a viewer sees the last of them.
            _liveView.ColourByCellType = ColourByCellType;
            _liveView.Sync();
        }

        /// <summary>
        /// The census on the console, on a cadence — the live cut's stand-in for the panel.
        /// </summary>
        /// <remarks>
        /// Not every metabolic step: the economy runs twice a simulated second and a world played
        /// at a hundred times real time would put two hundred lines a second into the Editor's
        /// log, which is a way of writing nothing down. The cadence is in simulated seconds, so
        /// the record of a session reads the same whatever pace it was watched at.
        /// </remarks>
        private void SayTheCensus()
        {
            double t = _live.ElapsedSeconds;
            double every = Mathf.Max(0.5f, LiveLogSeconds);

            if (t < _saidAt + every) return;
            _saidAt = t - (t % every);

            WorldCensus census = _live.Census;

            Debug.Log(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "[Theatre] LIVE · dynamics  t={0,8:0.#} s  alive {1,5}  births {2,6}  " +
                "deaths {3,6}  bodies drawn {4,5}  parts {5,6}  audit {6:0.###e+0} J  " +
                "{7:0.##}x real time",
                census.T, census.Alive, census.Births, census.Deaths,
                _liveView != null ? _liveView.BodyCount : 0,
                _liveView != null ? _liveView.PartCount : 0,
                census.AuditResidual, _measuredPace));
        }

        private void StepSolo()
        {
            double before = _solo.ElapsedSeconds;
            double wallBefore = _clock.Elapsed.TotalSeconds;

            _solo.UseTestSine = TestSine;
            _solo.Starving = Starve;

            if (!Paused)
            {
                _pending += Time.unscaledDeltaTime * Mathf.Max(0f, EffectiveRate);

                float dt = Ecosystem.FixedDt;
                double deadline = wallBefore + Mathf.Max(0.005f, FrameBudgetSeconds);

                while (_pending >= dt && _clock.Elapsed.TotalSeconds < deadline)
                {
                    _solo.Step();
                    _pending -= dt;
                }

                if (_pending > 4d * dt) _pending = 0d;
            }

            MeasurePace(before, wallBefore);

            // Painted even while paused: a creature opened paused would otherwise sit there in
            // the plain material, which reads as "the colour mode is broken".
            if (ColourByCellType)
            {
                _palette.Paint(0, _solo.Instance.Root.transform, _solo.Phenotype,
                    TheatrePalette.Tint(_solo.ReserveSeconds, _solo.Config.EnergyFullScaleSeconds),
                    true);
            }
        }

        /// <summary>Simulated seconds per wall-clock second, over a window rather than a frame.</summary>
        private void MeasurePace(double simBefore, double wallBefore)
        {
            double sim = _replay?.ElapsedSeconds ?? _live?.ElapsedSeconds ?? _solo?.ElapsedSeconds ?? 0d;
            double wall = _clock.Elapsed.TotalSeconds;

            _pacedFrom += sim - simBefore;
            _pacedAt += wall - wallBefore;

            if (_pacedAt < 0.5d) return;

            _measuredPace = _pacedFrom / _pacedAt;
            _pacedFrom = 0d;
            _pacedAt = 0d;
        }

        /// <summary>
        /// Repaints a slice of the population, in rotation.
        /// </summary>
        /// <remarks>
        /// A budget rather than a full pass: the tint is a reading of a number that changes twice
        /// a simulated second, and repainting four thousand bodies every frame to keep it exact
        /// would be the most expensive thing in the viewer by a wide margin. The lag is stated in
        /// the overlay rather than hidden.
        /// </remarks>
        private void Repaint()
        {
            if (_replay == null || !_map.Reliable) return;

            IReadOnlyList<Organism> living = _replay.Eco.World.Living;
            if (living.Count == 0) return;

            float scale = _replay.Record.Config.EnergyFullScaleSeconds;
            int budget = Mathf.Clamp(RepaintsPerFrame, 1, living.Count);

            for (int i = 0; i < budget; i++)
            {
                Organism creature = living[(int)(_repaintCursor++ % living.Count)];
                Transform root = _map.RootOf(creature.Id);
                if (root == null) continue;

                _palette.Paint(
                    creature.Id, root, creature.Phenotype,
                    TheatrePalette.Tint(creature.SecondsOfReserve, scale), ColourByCellType);
            }

            if (_repaintCursor % 512 == 0) _palette.PurgeDead();
        }

        /// <summary>
        /// Runs the world forward to a simulated second with the camera off.
        /// </summary>
        /// <remarks>
        /// Public since 2026-09-12 so the Play-mode interface check can put the theatre into its
        /// seeking state without synthesising a keypress: <c>K</c> calls this and nothing else, so
        /// what the check exercises is the path a person's hands take.
        /// </remarks>
        public void BeginSeek(double target)
        {
            double now = _replay?.ElapsedSeconds ?? _live?.ElapsedSeconds ?? double.NaN;
            if (double.IsNaN(now)) return;

            _seekFrom = now;
            _seekTarget = target;
            _seeking = target > now;

            if (!_seeking) EndSeek();
        }

        /// <summary>Space, without the key: pause or carry on.</summary>
        public void TogglePause() => Paused = !Paused;

        /// <summary>P, without the key: the provenance popover, which hides the warnings.</summary>
        public void ToggleProvenance() => _ui?.ToggleProvenance();

        /// <summary>
        /// Selects a creature by id, as a click does — the same call, without the raycast.
        /// </summary>
        /// <remarks>
        /// Returns false when the id map is unreliable, which is the one case where a click can
        /// point at a body and not name it: a viewer confidently naming the wrong creature is
        /// worse than one that admits it cannot (<see cref="CreatureIdMap"/>).
        /// </remarks>
        public bool SelectById(long id)
        {
            // The live world hands out its own ids and there is no map to be unreliable: the view
            // built every body from the solver the harness handed it, so an id on screen is that
            // world's own. What it is not is the recording's, which the interface says and this
            // method has never been the place for.
            if (_live != null)
            {
                _selectedId = id;
                FollowSelection();
                return true;
            }

            if (_replay == null || !_map.Reliable) return false;

            _selectedId = id;
            FollowSelection();
            return true;
        }

        /// <summary>Esc, without the key.</summary>
        public void Deselect()
        {
            _selectedId = -1;
            FlyCamera?.StopFollowing();
        }

        private void EndSeek()
        {
            _seeking = false;
            _pending = 0d;
            if (FlyCamera != null) FlyCamera.GetComponent<Camera>().enabled = true;
        }

        // ---------------------------------------------------------------- input

        private void ReadKeys()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.H)) ShowOverlay = !ShowOverlay;

            // P, added by the design: the evidence behind the strip's one word, and gone again on
            // the next press. It shares an anchor with the warnings and replaces them, because it
            // says everything a warning says and more.
            if (Input.GetKeyDown(KeyCode.P)) ToggleProvenance();

            if (Input.GetKeyDown(KeyCode.C))
            {
                ColourByCellType = !ColourByCellType;
                _palette.Clear();
            }

            if (Input.GetKeyDown(KeyCode.LeftBracket)) Rate = Mathf.Max(0.05f, Rate * 0.5f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) Rate = Mathf.Min(512f, Rate * 2f);
            if (Input.GetKeyDown(KeyCode.L)) PaceLock = !PaceLock;

            // X: the collider under the skin. Every body is dressed again on the next paint.
            if (Input.GetKeyDown(KeyCode.X))
            {
                _palette.RawShapes = !_palette.RawShapes;
                _skin.ShowRawShapes(_palette.RawShapes);
                _palette.Clear();
            }

            if (Input.GetKeyDown(KeyCode.K) && SeekToSeconds > 0f) BeginSeek(SeekToSeconds);
            if (Input.GetKeyDown(KeyCode.Escape)) Deselect();

            if (Input.GetKeyDown(KeyCode.T)) { TestSine = !TestSine; }
            if (Input.GetKeyDown(KeyCode.G)) { Starve = !Starve; }

            if (Input.GetKeyDown(KeyCode.R)) OpenWhateverModeSays();

            if (Input.GetKeyDown(KeyCode.F) && FlyCamera != null)
            {
                if (FlyCamera.Following != null) FlyCamera.StopFollowing();
                else FollowSelection();
            }

            if (Input.GetMouseButtonDown(0)) Select();
        }

        private void Select()
        {
            if (FlyCamera == null) return;

            var camera = FlyCamera.GetComponent<Camera>();
            if (camera == null) return;

            SelectAt(camera.ScreenPointToRay(Input.mousePosition));
        }

        /// <summary>
        /// Selects whatever a ray strikes — the click, without the mouse.
        /// </summary>
        /// <remarks>
        /// Public for the same reason <see cref="BeginSeek"/> is: the interface check drives the
        /// path a person's hands take rather than synthesising an event, and a selection made any
        /// other way would not be the one the viewer makes. The two engines answer the ray
        /// differently — PhysX has colliders and the live world has none, so the second is
        /// arithmetic (<see cref="LiveWorldView.Pick"/>).
        /// </remarks>
        public bool SelectAt(Ray ray)
        {
            if (_live != null)
            {
                if (_liveView == null || !_liveView.Pick(ray, out long picked)) return false;

                _selectedId = picked;
                FollowSelection();
                return true;
            }

            if (_replay == null) return false;
            if (!Physics.Raycast(ray, out RaycastHit hit, 5000f)) return false;

            long id = _map.IdOf(hit.transform);
            if (id < 0) return false;

            _selectedId = id;
            FollowSelection();
            return true;
        }

        private void FollowSelection()
        {
            if (_selectedId < 0 || FlyCamera == null) return;

            if (_live != null)
            {
                Transform body = _liveView?.RootOf(_selectedId);
                if (body == null) return;

                Organism found = CreatureIdMap.Find(_live.Sim.World, _selectedId);
                FlyCamera.Follow(body, found != null ? Radius(found.Phenotype) : 1f);
                return;
            }

            if (_replay == null) return;

            Transform root = _map.RootOf(_selectedId);
            if (root == null) return;

            Organism creature = CreatureIdMap.Find(_replay.Eco.World, _selectedId);
            float radius = creature != null ? Radius(creature.Phenotype) : 1f;

            FlyCamera.Follow(root, radius);
        }

        private static float Radius(Phenotype phenotype)
        {
            float worst = 0.3f;
            if (phenotype == null) return worst;

            foreach (PhenotypePart part in phenotype.Parts)
            {
                Float3 h = part.HalfExtents;
                float reach = Mathf.Sqrt(Float3.Dot(part.Position, part.Position)) +
                              Mathf.Max(Mathf.Abs(h.X), Mathf.Max(Mathf.Abs(h.Y), Mathf.Abs(h.Z)));

                worst = Mathf.Max(worst, reach);
            }

            return worst;
        }
    }
}
