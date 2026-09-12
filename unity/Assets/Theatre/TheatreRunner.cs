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

        [Tooltip("Never spend more than this fraction of a frame stepping, so the Editor stays " +
                 "responsive when the world is larger than the pace can serve.")]
        public float FrameBudgetSeconds = 0.05f;

        [Tooltip("Wall-clock seconds per frame given to a seek. Higher seeks faster and makes " +
                 "the Editor less responsive while it runs.")]
        public float SeekBudgetSeconds = 0.25f;

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
        private SoloCreature _solo;
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
            _palette.Skin = _skin;

            _palette.Photosynthetic = PhotosyntheticColour;
            _palette.Absorptive = AbsorptiveColour;
            _palette.Structural = StructuralColour;
            _palette.Starving = StarvingBrightness;

            string run = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN");
            if (!string.IsNullOrEmpty(run)) RunDirectory = run;

            string genome = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_GENOME");
            if (!string.IsNullOrEmpty(genome)) { GenomePath = genome; Mode = ViewMode.Solo; }

            string seek = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SEEK");
            if (!string.IsNullOrEmpty(seek) &&
                float.TryParse(seek, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float seekTo))
            {
                SeekToSeconds = seekTo;
            }

            if (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_OVERRIDE") == "1")
            {
                AllowSourceMismatch = true;
            }

            // Before the run opens, because opening is what fills it in.
            _ui = TheatreUi.Create();
            if (_ui != null) _ui.Visible = ShowOverlay;

            OpenWhateverModeSays();
        }

        private void OpenWhateverModeSays()
        {
            Close();
            _error = null;

            try
            {
                if (Mode == ViewMode.World) OpenWorld();
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
            if (_solo != null) { _ui.OpenSolo(_solo); return; }

            _ui.ShowError("nothing opened, and nothing said why");
        }

        private void OpenWorld()
        {
            if (string.IsNullOrWhiteSpace(RunDirectory))
            {
                _error =
                    "No run directory. Set it on the Theatre Runner in the scene, or launch with " +
                    "EVOSIM_THEATRE_RUN pointing at runs/<arm>.";
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

            if (Water != null)
            {
                RunConfig water = _replay.Record.Config;

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
                        Mathf.Max(1, (int)water.HorizontalPatches));
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
            // (SnapshotCamera.BoxOf) rather than a second copy of sqrt(area / K) here.
            _skin.Dress(SnapshotCamera.BoxOf(_replay, out _));

            if (SeekToSeconds > 0f) BeginSeek(SeekToSeconds);
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
            _solo?.Dispose();
            _solo = null;
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
            _skin.Dispose();
            _ui?.Dispose();
            _ui = null;
        }

        // ---------------------------------------------------------------- the loop

        private void Update()
        {
            ReadKeys();

            if (_replay != null) StepWorld();
            else if (_solo != null) StepSolo();

            DrawTheInterface();
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
        /// Read here rather than in the interface because the scene is the runner's: the map, the
        /// transform and the articulation are all things the interface deliberately never touches.
        /// </remarks>
        private float SelectedSpeed()
        {
            if (_replay == null || _selectedId < 0) return 0f;

            Transform root = _map.RootOf(_selectedId);
            if (root == null || root.childCount == 0) return 0f;

            var body = root.GetChild(0).GetComponent<ArticulationBody>();
            return body != null ? body.linearVelocity.magnitude : 0f;
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
                _pending += Time.unscaledDeltaTime * Mathf.Max(0f, Rate);

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

        private void StepSolo()
        {
            double before = _solo.ElapsedSeconds;
            double wallBefore = _clock.Elapsed.TotalSeconds;

            _solo.UseTestSine = TestSine;
            _solo.Starving = Starve;

            if (!Paused)
            {
                _pending += Time.unscaledDeltaTime * Mathf.Max(0f, Rate);

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
            double sim = (_replay?.ElapsedSeconds ?? _solo?.ElapsedSeconds ?? 0d);
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
            if (_replay == null) return;

            _seekFrom = _replay.ElapsedSeconds;
            _seekTarget = target;
            _seeking = target > _replay.ElapsedSeconds;

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
            if (_replay == null || FlyCamera == null) return;

            var camera = FlyCamera.GetComponent<Camera>();
            if (camera == null) return;

            if (!Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 5000f))
            {
                return;
            }

            long id = _map.IdOf(hit.transform);
            if (id < 0) return;

            _selectedId = id;
            FollowSelection();
        }

        private void FollowSelection()
        {
            if (_selectedId < 0 || FlyCamera == null) return;

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
