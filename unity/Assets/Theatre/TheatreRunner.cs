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

        private string _error;
        private double _pending;
        private bool _seeking;
        private double _seekTarget;

        private long _selectedId = -1;
        private readonly List<string> _sensorLines = new List<string>();

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _pacedFrom;
        private double _pacedAt;
        private double _measuredPace;
        private long _repaintCursor;

        private GUIStyle _panel;
        private GUIStyle _text;

        private void Start()
        {
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
                (_replay.Faithful ? "same source as the recording" : "SOURCE DIFFERS: " + _replay.SourceDifference));

            if (Water != null)
            {
                Water.Show(
                    _replay.Record.Config.WorldDepthMetres, WaterExtentMetres, Ecosystem.TileSpacing);
            }

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
            if (Water != null) Water.Show(config.WorldDepthMetres, 40f, 5f);

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
            if (Water != null) Water.Hide();
            _selectedId = -1;
            _pending = 0d;
            _seeking = false;
        }

        private void OnDisable() => Close();

        // ---------------------------------------------------------------- the loop

        private void Update()
        {
            ReadKeys();

            if (_replay != null) StepWorld();
            else if (_solo != null) StepSolo();
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

        private void BeginSeek(double target)
        {
            if (_replay == null) return;

            _seekTarget = target;
            _seeking = target > _replay.ElapsedSeconds;

            if (!_seeking) EndSeek();
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
            if (Input.GetKeyDown(KeyCode.Space)) Paused = !Paused;
            if (Input.GetKeyDown(KeyCode.H)) ShowOverlay = !ShowOverlay;

            if (Input.GetKeyDown(KeyCode.C))
            {
                ColourByCellType = !ColourByCellType;
                _palette.Clear();
            }

            if (Input.GetKeyDown(KeyCode.LeftBracket)) Rate = Mathf.Max(0.05f, Rate * 0.5f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) Rate = Mathf.Min(512f, Rate * 2f);

            if (Input.GetKeyDown(KeyCode.K) && SeekToSeconds > 0f) BeginSeek(SeekToSeconds);
            if (Input.GetKeyDown(KeyCode.Escape)) { _selectedId = -1; FlyCamera?.StopFollowing(); }

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

        // ---------------------------------------------------------------- the overlay

        private void OnGUI()
        {
            if (!ShowOverlay) return;

            if (_panel == null)
            {
                var background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.62f));
                background.Apply();

                _panel = new GUIStyle { normal = { background = background } };
                _text = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    normal = { textColor = Color.white },
                    richText = true,
                };
            }

            if (_error != null)
            {
                GUILayout.BeginArea(new Rect(12f, 12f, 720f, 140f), _panel);
                GUILayout.Space(8f);
                GUILayout.Label("<b>The theatre could not open this.</b>", _text);
                GUILayout.Label(_error, _text);
                GUILayout.EndArea();
                return;
            }

            if (_replay != null) WorldOverlay();
            else if (_solo != null) SoloOverlay();
        }

        private void WorldOverlay()
        {
            WorldCensus c = _replay.Census;
            RunRecord r = _replay.Record;

            GUILayout.BeginArea(new Rect(12f, 12f, 560f, 400f), _panel);
            GUILayout.Space(8f);

            GUILayout.Label(
                $"<b>{r.ArmName ?? "run"}</b>  seed {r.Seed}  dt {r.PhysicsDtSeconds}  " +
                $"config {Shorten(r.ConfigHash)}", _text);

            GUILayout.Label(
                _replay.Faithful
                    ? "<color=#9fe6a0>same source as the recording — this is the run</color>"
                    : "<color=#ff8080><b>NOT A FAITHFUL REPLAY</b> — " +
                      _replay.SourceDifference + "</color>",
                _text);

            GUILayout.Space(6f);

            string pace = _seeking
                ? $"<b>SEEKING</b> to t={_seekTarget:0} — {_measuredPace:0.#}x real time, " +
                  $"{Remaining()} to go"
                : Paused ? "<b>paused</b>" : $"{Rate:0.##}x requested, {_measuredPace:0.#}x actual";

            GUILayout.Label($"t = <b>{c.T:0.#} s</b>   {pace}", _text);

            GUILayout.Space(6f);
            GUILayout.Label(
                $"alive <b>{c.Alive}</b>   births {c.Births}   deaths {c.Deaths}   " +
                $"jointed {c.Jointed}", _text);
            GUILayout.Label(
                $"absorptive <b>{c.Absorptive}</b>   photosynthetic <b>{c.Photosynthetic}</b>" +
                (c.Diverged > 0 ? $"   <color=#ff8080>diverged {c.Diverged}</color>" : ""), _text);
            GUILayout.Label(
                $"mean depth {c.MeanHeight:0.#} m   audit {c.AuditPercent:0.0000}%   " +
                $"matter here {c.MatterHere:0.###}", _text);

            GUILayout.Space(6f);

            string identity = _replay.IdentityLine();
            GUILayout.Label(
                _replay.FirstMismatch != null
                    ? "<color=#ff8080><b>" + identity + "</b></color>"
                    : "<color=#9fe6a0>" + identity + "</color>",
                _text);

            // Past the last recorded sample there is nothing left to check against, and a green
            // "all samples match" line above a world nobody ever recorded would read as though
            // it were still being verified. It is the same world, still deterministic, and no
            // longer a replay of anything.
            if (_replay.ElapsedSeconds > _replay.RecordedThroughSeconds + 1e-6)
            {
                GUILayout.Label(
                    $"<color=#ffd080>past the record's last sample " +
                    $"(t={_replay.RecordedThroughSeconds:0.#} s): the world is running on, and " +
                    "nothing after that instant is checked against anything</color>", _text);
            }

            if (r.ConfigHashMismatch != null)
            {
                GUILayout.Label("<color=#ffd080>" + r.ConfigHashMismatch + "</color>", _text);
            }

            if (r.StepDisagreement != null)
            {
                GUILayout.Label("<color=#ffd080>" + r.StepDisagreement + "</color>", _text);
            }

            if (r.SamplesNote != null)
            {
                GUILayout.Label("<color=#ffd080>" + r.SamplesNote + "</color>", _text);
            }

            GUILayout.Space(6f);
            GUILayout.Label(SelectionLine(), _text);

            GUILayout.EndArea();

            Keys(
                "<b>Space</b> pause   <b>[</b> <b>]</b> pace   <b>K</b> seek to the field's second   " +
                "<b>C</b> colour   <b>R</b> reload",
                "click a creature to select and follow   <b>F</b> follow/free   <b>Esc</b> deselect   " +
                "<b>H</b> hide",
                "fly: <b>WASD</b> + <b>QE</b>, right-drag to look, wheel for speed, <b>Shift</b> to boost");
        }

        private string Remaining()
        {
            double left = _seekTarget - _replay.ElapsedSeconds;
            if (_measuredPace <= 0.001d) return "measuring";

            double seconds = left / _measuredPace;
            return seconds > 90d ? $"about {seconds / 60d:0} min" : $"about {seconds:0} s";
        }

        private string SelectionLine()
        {
            if (!_map.Reliable)
            {
                return "<color=#ffd080>selection unavailable: the id map could not be verified (" +
                       _map.Note + ")</color>";
            }

            if (_selectedId < 0) return "no creature selected — click one";

            Organism creature = CreatureIdMap.Find(_replay.Eco.World, _selectedId);
            if (creature == null) return $"creature {_selectedId} — <b>dead</b>";

            Transform root = _map.RootOf(_selectedId);
            float speed = 0f;

            if (root != null && root.childCount > 0)
            {
                var body = root.GetChild(0).GetComponent<ArticulationBody>();
                if (body != null) speed = body.linearVelocity.magnitude;
            }

            string reserve = float.IsPositiveInfinity(creature.SecondsOfReserve)
                ? "∞"
                : creature.SecondsOfReserve.ToString("0");

            return
                $"creature <b>{creature.Id}</b>  gen {creature.GenerationDepth}  " +
                $"parent {(creature.ParentId >= 0 ? creature.ParentId.ToString() : "founder")}  " +
                $"age {creature.Age:0} s  reserve {reserve} s  " +
                $"depth {-creature.HeightY:0.#} m  speed {speed:0.###} m/s  " +
                $"patch {creature.Patch}  " +
                (creature.HasPhotosyntheticTissue ? "photo " : "") +
                (creature.HasAbsorptiveTissue ? "absorptive" : "");
        }

        private void SoloOverlay()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 560f, 400f), _panel);
            GUILayout.Space(8f);

            GUILayout.Label("<b>one creature</b>   " + _solo.Source, _text);
            GUILayout.Label(
                $"{_solo.Phenotype.PartCount} parts   {_solo.Instance.TotalDof} DOF   " +
                $"{_solo.Genome.GlobalBrain.Length} global neurons", _text);

            GUILayout.Space(6f);
            GUILayout.Label(
                $"t = <b>{_solo.ElapsedSeconds:0.#} s</b>   " +
                (Paused ? "<b>paused</b>" : $"{Rate:0.##}x requested, {_measuredPace:0.#}x actual"),
                _text);

            GUILayout.Label(
                $"speed <b>{_solo.Speed:0.###} m/s</b>   travelled {_solo.Travelled:0.##} m   " +
                $"depth {_solo.Depth:0.#} m   joint rate {_solo.MeanJointRate():0.00} rad/s", _text);

            string reserve = _solo.Starving
                ? $"reserve {_solo.ReserveSeconds:0} s and falling"
                : $"reserve held at {_solo.ReserveSeconds:0} s (no economy in this mode)";

            GUILayout.Label(
                (_solo.UseTestSine
                    ? "<color=#ffd080><b>driven by the test sine</b> — not its brain</color>"
                    : "driven by its own brain") + "   " + reserve,
                _text);

            GUILayout.Space(6f);
            GUILayout.Label(
                _solo.SmellDensity > 0f
                    ? $"<color=#ffd080>smell field set by hand to {_solo.SmellDensity:0.###} J/m3 " +
                      "— not the run's detritus, which no run stores</color>"
                    : "smell field: the world's initial detritus (empty in a reference world)",
                _text);

            _solo.ReadSensors(_sensorLines);
            for (int i = 0; i < _sensorLines.Count; i++)
            {
                GUILayout.Label(_sensorLines[i] + "   <i>(at the root part)</i>", _text);
            }

            GUILayout.EndArea();

            Keys(
                "<b>Space</b> pause   <b>[</b> <b>]</b> pace   <b>T</b> brain/test sine   " +
                "<b>G</b> starve   <b>C</b> colour   <b>R</b> regrow",
                "<b>F</b> follow/free   <b>H</b> hide",
                "fly: <b>WASD</b> + <b>QE</b>, right-drag to look, wheel for speed");
        }

        private void Keys(params string[] lines)
        {
            GUILayout.BeginArea(
                new Rect(12f, Screen.height - 20f - 18f * lines.Length, 720f, 18f * lines.Length + 14f),
                _panel);
            GUILayout.Space(6f);
            for (int i = 0; i < lines.Length; i++) GUILayout.Label(lines[i], _text);
            GUILayout.EndArea();
        }

        private static string Shorten(string hash) =>
            string.IsNullOrEmpty(hash) ? "(none)" :
            hash.Length <= 10 ? hash : hash.Substring(0, 10);
    }
}
