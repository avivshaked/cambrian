using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// What the runner knows about itself and the interface cannot see: the pace, the pause, the
    /// seek, and the selection. Passed in once a frame.
    /// </summary>
    public struct TheatreUiState
    {
        public bool Paused;
        public float Rate;
        public double MeasuredPace;
        public bool Seeking;
        public double SeekFrom;
        public double SeekTarget;

        /// <summary>The creature the viewer clicked, or -1.</summary>
        public long SelectedId;

        /// <summary>Its root's speed, m/s. Read by the runner, which owns the scene.</summary>
        public float SelectedSpeed;
    }

    /// <summary>
    /// The theatre's interface: the strip, the census, the warnings, the popover, the inspector
    /// and the bar — design/SPEC.md and <c>design/canvas/theatre.uss</c>, built 2026-09-12.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the interface is for, in one line each:</b> is this the run or a lookalike, what
    /// does the world look like, is anything broken, what is this creature I clicked. Everything
    /// on screen answers one of those four and nothing is here for decoration.
    /// </para>
    /// <para>
    /// <b>It replaces the IMGUI block.</b> <c>OnGUI</c> rebuilt every string in the overlay on
    /// every frame and laid them out with <c>GUILayout</c>; this builds a retained tree once and
    /// touches four text fields and one width per frame. The cadence split is a performance
    /// contract rather than a detail: the theatre often runs beside five simulation processes, and
    /// a viewer that spends a millisecond a frame formatting numbers nobody is reading is a viewer
    /// that changes what it is watching.
    /// </para>
    /// <para>
    /// <b>Nothing here takes the mouse.</b> Every element in the document is
    /// <c>PickingMode.Ignore</c>: a left-click in the water selects a creature through the
    /// runner's own raycast and a right-drag looks around, so the interface must never stand in
    /// front of either. The hover rules in the stylesheet are therefore decorative and, with no
    /// EventSystem in the theatre scene, never fire.
    /// </para>
    /// <para>
    /// <b>It is outside <c>simHash</c>, like the rest of <c>Assets/Theatre</c>.</b> A label here
    /// cannot orphan a recording, which is the whole reason the theatre sits beside the simulation
    /// rather than inside it (CLAUDE.md).
    /// </para>
    /// </remarks>
    public sealed class TheatreUi : IDisposable
    {
        /// <summary>Panel width at which the chrome takes the design's density step.</summary>
        public const int WideAtPixels = 2240;

        /// <summary>And the second step, for the owner's 3840-wide monitor.</summary>
        public const int WiderAtPixels = 3400;

        /// <summary>
        /// What the vermilion plate fires on: the energy audit, as a percentage of energy in.
        /// </summary>
        /// <remarks>
        /// <b>The two identities and nothing else</b> (logbook/specs/theatre-ui-spec.md, item 1).
        /// A healthy world reads 0.0000% in the run's own table, which is four decimal places, so
        /// the threshold is the first digit that table could show. The replay itself has no
        /// tolerance to borrow: its comparison against the record is exact equality, which is a
        /// question about identity rather than about conservation. Mean depth, matter here and
        /// every other reading move in a healthy world and are never alarms.
        /// </remarks>
        public const double AuditPercentTolerance = 1e-4;

        /// <summary>
        /// And the matter identity: units of matter, against the stock the world was seeded with.
        /// </summary>
        /// <remarks>
        /// Relative, with an absolute floor at the precision the report prints (<c>mat resid</c>
        /// is written to three decimals). A closed world holds six thousand units, so the floor is
        /// what decides in practice; the relative term is for a world with influx.
        /// </remarks>
        public const double MatterResidualFloor = 1e-3;

        public const double MatterResidualRelative = 1e-6;

        /// <summary>The six provenance states: the design's five, plus the thread caveat.</summary>
        public enum Provenance
        {
            /// <summary>The build, the threads and every compared sample agree.</summary>
            Faithful,

            /// <summary>Normal, but the checking has run out — past the record's last sample.</summary>
            Unverified,

            /// <summary>
            /// The physics ran on job worker threads, or the manifest never said. D078: the shared
            /// world forks from itself after about 148,000 steps whatever this process does, so
            /// such a run is never shown as faithful.
            /// </summary>
            Threaded,

            /// <summary>A valid world grown from the same config and seed, but not this run.</summary>
            Cousin,

            /// <summary>It parted from the record at a sample that was compared.</summary>
            Diverged,

            /// <summary>The world is right; clicking a body cannot name it.</summary>
            IdsUnverifiable,
        }

        private const string DocumentResource = "TheatreUiDocument";
        private const string StylesResource = "TheatreUiStyles";
        private const string ThemeResource = "TheatreTheme";

        // ---------------------------------------------------------------- the panel

        private GameObject _host;
        private UIDocument _document;
        private PanelSettings _settings;

        // ---------------------------------------------------------------- what is on screen

        private TheatreReplay _replay;
        private CreatureIdMap _map;
        private SoloCreature _solo;
        private LineageIndex _lineage;
        private string _runDirectory;

        private Provenance _state = (Provenance)(-1);
        private string _warningSignature;
        private bool _alarm;
        private bool _popoverBuilt;
        private string _liveCoreHash;
        private string _liveSimHash;

        private double _drawnCensusT = double.NaN;
        private double _lastMatchedT = double.NaN;
        private int _lastMatched = -1;
        private long _drawnSelection = long.MinValue;
        private bool _drawnReliable = true;
        private double _axisEndSeconds = 1d;
        private double _soloDrawnAt = -1d;
        private int _widthStep = -1;

        private readonly List<string> _sensorLines = new List<string>();
        private readonly List<Label> _soloValues = new List<Label>();
        private readonly List<Label> _soloSensorValues = new List<Label>();

        // ---------------------------------------------------------------- elements

        private VisualElement _root;
        private VisualElement _census, _censusWorld, _censusSolo, _inspector, _warnings, _popover;
        private VisualElement _strip, _bar, _error, _rowDiverged;
        private VisualElement _status, _statusMarker, _alarmPlate, _identBadges;
        private VisualElement _seekPlate, _seekbar, _seekProgress, _glyphPlay, _glyphPause;
        private VisualElement _timeline, _timelineTrack, _timelineElapsed, _timelineElapsedBeyond;
        private VisualElement _timelineHead, _timelineBeyond, _popoverRows, _popoverMarker;
        private VisualElement _legend, _popoverStatus;
        private VisualElement _inspectorEmpty, _inspectorLive, _inspectorDead, _inspectorGone;
        private VisualElement _guilds, _ancestry, _ancestryDead, _soloKeySine, _soloKeyStarve;

        private Label _statusWord, _alarmText, _identArm, _identMeta, _seekPlateText, _seekMeta;
        private Label _pace, _clockValue, _censusCadence, _errorDetail;
        private Label _valueAlive, _valueJointed, _valueAbsorptive, _valuePhotosynthetic;
        private Label _valueDiverged, _valueBirths, _valueDeaths, _valueAudit;
        private Label _valueMatterResidual, _valueMatterHere, _valueMeanDepth;
        private Label _inspectorId, _inspectorBadge, _valueGeneration, _valueParent;
        private Label _valueReserve, _valueSpeed, _valuePartsDof, _ancestryChain, _ancestryNote;
        private Label _deadId, _deadBadge, _valueLived, _valueDeadGeneration, _valueChildren;
        private Label _ancestryDeadChain, _unavailableProse;
        private Label _transportWord, _tickPeak, _tickRecordEnd, _tickEnd;
        private Label _popoverWord, _popoverNote, _popoverProse;

        private TheatreUi() { }

        /// <summary>The document's root, for the Play-mode check to query.</summary>
        public VisualElement Root => _root;

        /// <summary>The panel the interface is drawn on, made at Start and owned by this.</summary>
        public UIDocument Document => _document;

        /// <summary>H. The whole interface, on or off, so a picture carries the render alone.</summary>
        public bool Visible
        {
            get => _root != null && !_root.ClassListContains("is-hidden");
            set => _root?.EnableInClassList("is-hidden", !value);
        }

        /// <summary>P. The popover and the warnings share one anchor and are exclusive.</summary>
        public bool ProvenanceOpen { get; private set; }

        /// <summary>The state the strip is showing.</summary>
        public Provenance State => _state;

        /// <summary>True while the vermilion plate is up: one of the two identities is broken.</summary>
        public bool InvariantBroken => _alarm;

        /// <summary>The lineage index, built on the first selection of a creature that has died.</summary>
        public LineageIndex Lineage => _lineage;

        // ---------------------------------------------------------------- building

        /// <summary>
        /// Builds the panel and the document. Returns null, having said why, when the UI assets
        /// are not in the project — a theatre with no interface is still a theatre, and refusing
        /// to play the run would be the wrong answer to a missing stylesheet.
        /// </summary>
        /// <remarks>
        /// The panel is made here rather than put in the theatre scene on purpose. The scene is
        /// generated (<c>Evosim/Rebuild Theatre Scene</c>) and lives under <c>Assets/Scenes</c>,
        /// which every worker copies and nothing refreshes on a change; a UIDocument in it would
        /// mean a stale worker plays the run with no interface and says nothing about it. A panel
        /// built at Start is the same in every worker that has this file.
        /// </remarks>
        public static TheatreUi Create()
        {
            var uxml = Resources.Load<VisualTreeAsset>(DocumentResource);
            var styles = Resources.Load<StyleSheet>(StylesResource);

            if (uxml == null || styles == null)
            {
                Debug.LogWarning(
                    "[Theatre] the interface did not load: " +
                    (uxml == null ? "Resources/" + DocumentResource + ".uxml" : "") +
                    (uxml == null && styles == null ? " and " : "") +
                    (styles == null ? "Resources/" + StylesResource + ".uss" : "") +
                    " is missing. The run still plays, with nothing drawn over it.");

                return null;
            }

            var ui = new TheatreUi();

            ui._settings = ScriptableObject.CreateInstance<PanelSettings>();
            ui._settings.name = "Theatre Panel";

            // Constant Pixel Size, scale 1: the chrome is fixed in pixels and the water gains the
            // area. Unity's own scaling would scale the type, which the design rules out.
            ui._settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            ui._settings.scale = 1f;

            var theme = Resources.Load<ThemeStyleSheet>(ThemeResource);
            if (theme != null) ui._settings.themeStyleSheet = theme;

            ui._host = new GameObject("Theatre UI");
            ui._document = ui._host.AddComponent<UIDocument>();
            ui._document.panelSettings = ui._settings;

            VisualElement documentRoot = ui._document.rootVisualElement;

            if (documentRoot == null)
            {
                Debug.LogWarning("[Theatre] the UI document has no root; the interface is off.");
                ui.Dispose();
                return null;
            }

            documentRoot.styleSheets.Add(styles);
            uxml.CloneTree(documentRoot);

            ui._root = documentRoot.Q<VisualElement>("theatre-root") ?? documentRoot;
            ui._root.pickingMode = PickingMode.Ignore;

            ui.Find();
            ui.PaintThePlayGlyph();

            // The density step, and again whenever the window changes size. Registered here
            // rather than per run, so reloading with R does not stack a second callback.
            ui._root.RegisterCallback<GeometryChangedEvent>(_ => ui.ReadTheWidth());
            ui._timelineBeyond?.RegisterCallback<GeometryChangedEvent>(_ => ui.Dashes());
            Centre(ui._tickPeak);
            Centre(ui._tickRecordEnd);
            ui.ReadTheWidth();

            return ui;
        }

        private void Find()
        {
            _census = _root.Q<VisualElement>("census");
            _censusWorld = _root.Q<VisualElement>("census-world");
            _censusSolo = _root.Q<VisualElement>("census-solo");
            _inspector = _root.Q<VisualElement>("inspector");
            _warnings = _root.Q<VisualElement>("warnings");
            _popover = _root.Q<VisualElement>("popover");
            _strip = _root.Q<VisualElement>("strip");
            _bar = _root.Q<VisualElement>("bar");
            _error = _root.Q<VisualElement>("error");
            _rowDiverged = _root.Q<VisualElement>("row-diverged");

            _status = _root.Q<VisualElement>("status");
            _statusMarker = _root.Q<VisualElement>("status-marker");
            _alarmPlate = _root.Q<VisualElement>("alarm-plate");
            _identBadges = _root.Q<VisualElement>("ident-badges");
            _seekPlate = _root.Q<VisualElement>("seek-plate");
            _seekbar = _root.Q<VisualElement>("seekbar");
            _seekProgress = _root.Q<VisualElement>("seek-progress");
            _glyphPlay = _root.Q<VisualElement>("glyph-play");
            _glyphPause = _root.Q<VisualElement>("glyph-pause");

            _timeline = _root.Q<VisualElement>("timeline");
            _timelineTrack = _root.Q<VisualElement>("timeline-track");
            _timelineElapsed = _root.Q<VisualElement>("timeline-elapsed");
            _timelineElapsedBeyond = _root.Q<VisualElement>("timeline-elapsed-beyond");
            _timelineHead = _root.Q<VisualElement>("timeline-head");
            _timelineBeyond = _root.Q<VisualElement>("timeline-beyond");

            _legend = _root.Q<VisualElement>("timeline-legend");
            _popoverRows = _root.Q<VisualElement>("popover-rows");
            _popoverMarker = _root.Q<VisualElement>("popover-marker");
            _popoverStatus = _root.Q<VisualElement>("popover-status");

            _inspectorEmpty = _root.Q<VisualElement>("inspector-empty");
            _inspectorLive = _root.Q<VisualElement>("inspector-live");
            _inspectorDead = _root.Q<VisualElement>("inspector-dead");
            _inspectorGone = _root.Q<VisualElement>("inspector-unavailable");
            _guilds = _root.Q<VisualElement>("guilds");
            _ancestry = _root.Q<VisualElement>("ancestry");
            _ancestryDead = _root.Q<VisualElement>("ancestry-dead");
            _soloKeySine = _root.Q<VisualElement>("keyhint-solo-sine");
            _soloKeyStarve = _root.Q<VisualElement>("keyhint-solo-starve");

            _statusWord = _root.Q<Label>("status-word");
            _alarmText = _root.Q<Label>("alarm-text");
            _identArm = _root.Q<Label>("ident-arm");
            _identMeta = _root.Q<Label>("ident-meta");
            _seekPlateText = _root.Q<Label>("seek-plate-text");
            _seekMeta = _root.Q<Label>("seek-meta");
            _pace = _root.Q<Label>("pace");
            _clockValue = _root.Q<Label>("clock-value");
            _censusCadence = _root.Q<Label>("census-cadence");
            _errorDetail = _root.Q<Label>("error-detail");

            _valueAlive = _root.Q<Label>("value-alive");
            _valueJointed = _root.Q<Label>("value-jointed");
            _valueAbsorptive = _root.Q<Label>("value-absorptive");
            _valuePhotosynthetic = _root.Q<Label>("value-photosynthetic");
            _valueDiverged = _root.Q<Label>("value-diverged");
            _valueBirths = _root.Q<Label>("value-births");
            _valueDeaths = _root.Q<Label>("value-deaths");
            _valueAudit = _root.Q<Label>("value-audit");
            _valueMatterResidual = _root.Q<Label>("value-matter-residual");
            _valueMatterHere = _root.Q<Label>("value-matter-here");
            _valueMeanDepth = _root.Q<Label>("value-mean-depth");

            _inspectorId = _root.Q<Label>("inspector-id");
            _inspectorBadge = _root.Q<Label>("inspector-badge");
            _valueGeneration = _root.Q<Label>("value-generation");
            _valueParent = _root.Q<Label>("value-parent");
            _valueReserve = _root.Q<Label>("value-reserve");
            _valueSpeed = _root.Q<Label>("value-speed");
            _valuePartsDof = _root.Q<Label>("value-parts-dof");
            _ancestryChain = _root.Q<Label>("ancestry-chain");
            _ancestryNote = _root.Q<Label>("ancestry-note");

            _deadId = _root.Q<Label>("inspector-dead-id");
            _deadBadge = _root.Q<Label>("inspector-dead-badge");
            _valueLived = _root.Q<Label>("value-lived");
            _valueDeadGeneration = _root.Q<Label>("value-dead-generation");
            _valueChildren = _root.Q<Label>("value-children");
            _ancestryDeadChain = _root.Q<Label>("ancestry-dead-chain");
            _unavailableProse = _root.Q<Label>("inspector-unavailable-prose");

            _transportWord = _root.Q<Label>("transport-word");
            _tickPeak = _root.Q<Label>("tick-peak");
            _tickRecordEnd = _root.Q<Label>("tick-record-end");
            _tickEnd = _root.Q<Label>("tick-end");

            _popoverWord = _root.Q<Label>("popover-word");
            _popoverNote = _root.Q<Label>("popover-note");
            _popoverProse = _root.Q<Label>("popover-prose");
        }

        /// <summary>
        /// The play triangle, painted rather than typed.
        /// </summary>
        /// <remarks>
        /// The mockup drew it as a zero-sized box with transparent borders, which is a browser
        /// technique and not a USS one, and a text glyph was never available either: U+25B6 is
        /// absent from all three IBM Plex faces this interface ships (checked with fontTools,
        /// 2026-09-12), so it would have drawn as a blank box. <c>Painter2D</c> takes the design's
        /// 12 px box and its colour from the stylesheet.
        /// </remarks>
        private void PaintThePlayGlyph()
        {
            if (_glyphPlay == null) return;

            VisualElement glyph = _glyphPlay;

            glyph.generateVisualContent += context =>
            {
                Rect box = glyph.contentRect;
                if (box.width <= 1f || box.height <= 1f) return;

                Painter2D painter = context.painter2D;
                painter.fillColor = glyph.resolvedStyle.color;

                painter.BeginPath();
                painter.MoveTo(new Vector2(box.xMin + 1f, box.yMin));
                painter.LineTo(new Vector2(box.xMax, box.yMin + box.height * 0.5f));
                painter.LineTo(new Vector2(box.xMin + 1f, box.yMax));
                painter.ClosePath();
                painter.Fill();
            };

            glyph.MarkDirtyRepaint();
        }

        /// <summary>
        /// The design's density step, as a class on the root rather than as custom properties.
        /// </summary>
        /// <remarks>
        /// UI Toolkit has no API for writing a custom property from C#, which is what the design
        /// asked for; a class on the root is the same three tokens through the same stylesheet.
        /// The second step (<c>.is-wider</c>, 3400) is this project's: the owner's monitor is
        /// 3840 pixels wide, where the 2240 chrome is small enough to read badly.
        /// </remarks>
        private void ReadTheWidth()
        {
            float width = _root.resolvedStyle.width;
            if (width <= 1f) width = Screen.width;

            int step = width >= WiderAtPixels ? 2 : width >= WideAtPixels ? 1 : 0;
            if (step == _widthStep) return;

            _widthStep = step;
            _root.EnableInClassList("is-wide", step >= 1);
            _root.EnableInClassList("is-wider", step >= 2);
        }

        // ---------------------------------------------------------------- opening

        /// <summary>Nothing opened; the one thing worth showing is why.</summary>
        public void ShowError(string error)
        {
            if (_root == null) return;

            Close();

            _errorDetail.text = error ?? "no reason given";
            Show(_error, true);
            Show(_strip, false);
            Show(_bar, false);
            Show(_census, false);
            Show(_inspector, false);
        }

        /// <summary>Mode B: a recorded world, with the identity check on screen.</summary>
        public void OpenWorld(TheatreReplay replay, CreatureIdMap map, string runDirectory)
        {
            if (_root == null) return;

            Close();

            _replay = replay;
            _map = map;
            _runDirectory = runDirectory;

            Show(_error, false);
            Show(_strip, true);
            Show(_bar, true);
            Show(_census, true);
            Show(_inspector, true);
            Show(_censusWorld, true);
            Show(_censusSolo, false);
            Show(_soloKeySine, false);
            Show(_soloKeyStarve, false);

            RunRecord record = replay.Record;

            _identArm.text = record.ArmName ?? "run";

            BuildTheTimeline();
            Sample(true);
        }

        /// <summary>Mode A: one creature, alone, under its own brain — the same strip, a
        /// different census (design/SPEC.md §8).</summary>
        public void OpenSolo(SoloCreature solo)
        {
            if (_root == null) return;

            Close();

            _solo = solo;

            Show(_error, false);
            Show(_strip, true);
            Show(_bar, true);
            Show(_census, true);
            Show(_inspector, false);
            Show(_censusWorld, false);
            Show(_censusSolo, true);
            Show(_soloKeySine, true);
            Show(_soloKeyStarve, true);

            _census.AddToClassList("panel--census-solo");

            _identArm.text = "one creature";
            _identMeta.text = solo.Source ?? "";

            // No record, so no provenance, no timeline and no warnings: there is nothing this
            // world could be a cousin of. The strip keeps the clock, the pace and the keys.
            Show(_status, false);
            Show(_alarmPlate, false);
            Show(_warnings, false);
            Show(_popover, false);
            Show(_timeline, false);
            Show(_legend, false);

            BuildTheSoloCensus();
        }

        private void Close()
        {
            _replay = null;
            _map = null;
            _solo = null;
            _lineage = null;
            _runDirectory = null;

            _state = (Provenance)(-1);
            _warningSignature = null;
            _alarm = false;
            _popoverBuilt = false;
            _drawnCensusT = double.NaN;
            _lastMatchedT = double.NaN;
            _lastMatched = -1;
            _drawnSelection = long.MinValue;
            _drawnReliable = true;
            _soloDrawnAt = -1d;
            ProvenanceOpen = false;

            _soloValues.Clear();
            _soloSensorValues.Clear();
            _censusSolo?.Clear();
            _warnings?.Clear();
            _popoverRows?.Clear();
            _identBadges?.Clear();

            _census?.RemoveFromClassList("panel--census-solo");
            Show(_status, true);
            Show(_popover, false);
            Show(_timeline, true);
            Show(_legend, true);
            Show(_seekbar, false);
            Show(_seekPlate, false);
            Show(_seekMeta, false);
        }

        // ---------------------------------------------------------------- the loop

        /// <summary>
        /// One frame. Four text fields and one width, plus the census when the world's clock has
        /// moved since it was last drawn.
        /// </summary>
        /// <remarks>
        /// <b>The cadence split is the contract</b> (design/SPEC.md §9 item 6). Per frame: the
        /// clock, the pace, the seek note, the selected creature's speed, and the width of the
        /// timeline's elapsed bar. Everything else waits for the census to move, which is once per
        /// metabolic step of simulated time and at most once per frame — so at a hundred times
        /// real time the whole census is drawn once for every two hundred simulated seconds, and
        /// at a tenth of real time it is drawn when something has actually changed.
        /// </remarks>
        public void Tick(TheatreUiState state)
        {
            if (_root == null) return;

            if (_replay != null) { TickWorld(state); return; }
            if (_solo != null) TickSolo(state);
        }

        private void TickWorld(TheatreUiState state)
        {
            WorldCensus census = _replay.Census;

            // ---- per frame: four fields and one width -----------------------------
            _clockValue.text = "t " + TheatreUiFormat.Clock(census.T);
            _pace.text = state.Paused
                ? "PAUSED"
                : TheatreUiFormat.Pace(state.Rate, state.MeasuredPace);
            _pace.EnableInClassList("pace--paused", state.Paused);

            Seeking(state);
            Progress(census.T);

            if (state.SelectedId >= 0 && _inspectorLive != null &&
                !_inspectorLive.ClassListContains("is-gone"))
            {
                _valueSpeed.text = TheatreUiFormat.Fixed(state.SelectedSpeed, 2);
            }

            Pulse();

            // ---- per sample --------------------------------------------------------
            bool moved = double.IsNaN(_drawnCensusT) || census.T != _drawnCensusT;
            bool selectionMoved =
                state.SelectedId != _drawnSelection || (_map != null && _map.Reliable != _drawnReliable);

            if (!moved && !selectionMoved) return;

            _drawnCensusT = census.T;

            if (moved) Sample(false);

            _drawnSelection = state.SelectedId;
            _drawnReliable = _map == null || _map.Reliable;

            Inspector(state.SelectedId, state.SelectedSpeed);
        }

        private void TickSolo(TheatreUiState state)
        {
            _clockValue.text = "t " + TheatreUiFormat.Clock(_solo.ElapsedSeconds);
            _pace.text = state.Paused
                ? "PAUSED"
                : TheatreUiFormat.Pace(state.Rate, state.MeasuredPace);
            _pace.EnableInClassList("pace--paused", state.Paused);

            Transport(state, "PLAYING", "PAUSED");

            // Mode A has no samples to hang a cadence on and its readings move every step, so the
            // census is refreshed on a wall clock instead — five times a second, which is faster
            // than an eye reads a number and slower than a frame.
            double now = Time.realtimeSinceStartupAsDouble;
            if (_soloDrawnAt > 0d && now - _soloDrawnAt < 0.2d) return;

            _soloDrawnAt = now;
            SoloCensus();
        }

        /// <summary>Everything that waits for the world's clock to move.</summary>
        private void Sample(bool firstTime)
        {
            WorldCensus census = _replay.Census;

            _valueAlive.text = TheatreUiFormat.Quantity(census.Alive);
            _valueJointed.text = TheatreUiFormat.Quantity(census.Jointed);
            _valueAbsorptive.text = TheatreUiFormat.Quantity(census.Absorptive);
            _valuePhotosynthetic.text = TheatreUiFormat.Quantity(census.Photosynthetic);
            _valueBirths.text = TheatreUiFormat.Quantity(census.Births);
            _valueDeaths.text = TheatreUiFormat.Quantity(census.Deaths);

            _valueAudit.text = TheatreUiFormat.Fixed(census.AuditPercent, 4);
            _valueMatterHere.text = TheatreUiFormat.Fixed(census.MatterHere, 2);
            _valueMeanDepth.text = TheatreUiFormat.Depth(census.MeanHeight);

            _valueMatterResidual.text = TheatreUiFormat.Fixed(census.MatterResidual, 3);

            bool diverged = census.Diverged > 0;
            Show(_rowDiverged, diverged);
            if (diverged) _valueDiverged.text = TheatreUiFormat.Quantity(census.Diverged);

            _censusCadence.text = Cadence();

            if (_replay.SamplesMatched != _lastMatched)
            {
                _lastMatched = _replay.SamplesMatched;
                _lastMatchedT = census.T;
            }

            Alarm(census);
            Status(firstTime);
            Warnings();
            Marks();
        }

        // ---------------------------------------------------------------- the strip

        /// <summary>Which of the six states the strip is in, and everything that follows from it.</summary>
        private void Status(bool force)
        {
            Provenance now = Decide();

            if (!force && now == _state)
            {
                // The meta carries the coverage, which moves with every sample even when the
                // state does not.
                _identMeta.text = Meta(now);
                return;
            }

            _state = now;

            _statusWord.text = Word(now);
            _status.EnableInClassList("status--abnormal", now != Provenance.Faithful);

            // The pulse leaves the marker wherever the tween had it, so a state that stops
            // pulsing has to be handed its opacity back.
            if (now != Provenance.Diverged) _statusMarker.style.opacity = 1f;

            Marker(_statusMarker, now);
            _identMeta.text = Meta(now);

            Badges();
            _popoverBuilt = false;
            if (ProvenanceOpen) Popover();
        }

        private Provenance Decide()
        {
            if (!_replay.Faithful) return Provenance.Cousin;
            if (_replay.FirstMismatch != null) return Provenance.Diverged;

            if (_replay.ThreadCaveat != null || _replay.PhysicsJobWorkers > 0)
            {
                return Provenance.Threaded;
            }

            if (_map != null && !_map.Reliable) return Provenance.IdsUnverifiable;

            if (PastTheRecord() || _replay.Record.Samples.Count == 0)
            {
                return Provenance.Unverified;
            }

            return Provenance.Faithful;
        }

        private static string Word(Provenance state)
        {
            switch (state)
            {
                case Provenance.Faithful: return "FAITHFUL";
                case Provenance.Unverified: return "FAITHFUL " + TheatreUiFormat.Dot + " UNVERIFIED";
                case Provenance.Threaded: return "THREADS UNVERIFIED";
                case Provenance.Cousin: return "COUSIN";
                case Provenance.Diverged: return "DIVERGED";
                default: return "IDS UNVERIFIABLE";
            }
        }

        /// <summary>
        /// The line under the arm name. Every faithful state carries its coverage, because
        /// "faithful" without it is a claim about nothing (logbook/specs/theatre-ui-spec.md item 2).
        /// </summary>
        private string Meta(Provenance state)
        {
            RunRecord record = _replay.Record;
            string dot = " " + TheatreUiFormat.Dot + " ";

            string identity = Coverage();

            switch (state)
            {
                case Provenance.Cousin:
                    return "a valid world, not this run" + dot +
                           (_replay.SourceDifference ?? "the build differs");

                case Provenance.Diverged:
                    return "parted from the record at " + (_replay.FirstMismatch ?? "a sample") +
                           dot + identity;

                case Provenance.IdsUnverifiable:
                    return "the world is right" + dot + "clicking a body cannot name it" + dot +
                           (_map != null ? _map.Note : "");

                case Provenance.Threaded:
                    return "seed " + TheatreUiFormat.Identifier((long)record.Seed) + dot +
                           "dt " + TheatreUiFormat.Step(record.PhysicsDtSeconds) + dot +
                           TheatreUiFormat.Hash(record.ConfigHash) + dot +
                           (_replay.ThreadCaveat ?? "physics jobs " + _replay.PhysicsJobWorkers) +
                           dot + identity;

                case Provenance.Unverified:
                    return "seed " + TheatreUiFormat.Identifier((long)record.Seed) + dot +
                           "dt " + TheatreUiFormat.Step(record.PhysicsDtSeconds) + dot +
                           TheatreUiFormat.Hash(record.ConfigHash) + dot +
                           (PastTheRecord()
                               ? "past the record since t=" +
                                 TheatreUiFormat.Seconds(_replay.RecordedThroughSeconds) +
                                 dot + "same world, nothing left to check against"
                               : identity);

                default:
                    return "seed " + TheatreUiFormat.Identifier((long)record.Seed) + dot +
                           "dt " + TheatreUiFormat.Step(record.PhysicsDtSeconds) + dot +
                           TheatreUiFormat.Hash(record.ConfigHash) + dot +
                           "physics jobs " + _replay.PhysicsJobWorkers + ", as recorded" +
                           dot + identity;
            }
        }

        /// <summary>
        /// The identity check's own sentence, with the second it holds through.
        /// </summary>
        /// <remarks>
        /// Taken from <c>TheatreReplay.IdentityLine()</c> rather than rebuilt, so the interface
        /// and the log say the same thing; the clock is added because "24 of 24 samples match" is
        /// a statement about a prefix of the run and the viewer needs to know how long a prefix.
        /// </remarks>
        private string Coverage()
        {
            string line = _replay.IdentityLine();
            const string prefix = "identity: ";

            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                line = line.Substring(prefix.Length);
            }

            if (_replay.SamplesMatched > 0 && !double.IsNaN(_lastMatchedT))
            {
                line += " through t = " + TheatreUiFormat.Seconds(_lastMatchedT) + " s";
            }

            return line;
        }

        /// <summary>The zero to two badges that trail a cousin's strip.</summary>
        private void Badges()
        {
            _identBadges.Clear();

            RunRecord record = _replay.Record;

            if (_state == Provenance.IdsUnverifiable)
            {
                // The struck SELECTION badge of provenance state 5: a wrapper, a badge, and the
                // 1px rule over it, because USS has no line-through.
                var wrapper = new VisualElement();
                wrapper.AddToClassList("struck");
                wrapper.AddToClassList("struck--after-meta");
                wrapper.pickingMode = PickingMode.Ignore;

                var badge = new Label("SELECTION");
                badge.AddToClassList("badge");
                badge.AddToClassList("lead");

                var rule = new VisualElement();
                rule.AddToClassList("struck__rule");
                rule.pickingMode = PickingMode.Ignore;

                wrapper.Add(badge);
                wrapper.Add(rule);
                _identBadges.Add(wrapper);
                return;
            }

            if (_state != Provenance.Cousin && _state != Provenance.Threaded)
            {
                if (_state == Provenance.Faithful) Badge("AS RECORDED", false);
                return;
            }

            int added = 0;

            if (record.UnityVersion != null && record.UnityVersion != Application.unityVersion &&
                added < 2)
            {
                Badge("build " + Application.unityVersion + " " + TheatreUiFormat.NotEqual + " " +
                      record.UnityVersion, true);
                added++;
            }

            if (added < 2)
            {
                string recorded = record.PhysicsJobWorkers.HasValue
                    ? record.PhysicsJobWorkers.Value.ToString()
                    : "unrecorded";

                if (!record.PhysicsJobWorkers.HasValue ||
                    record.PhysicsJobWorkers.Value != _replay.PhysicsJobWorkers)
                {
                    Badge("physics jobs " + _replay.PhysicsJobWorkers + " " +
                          TheatreUiFormat.NotEqual + " " + recorded, true);
                    added++;
                }
                else if (_state == Provenance.Threaded)
                {
                    Badge("physics jobs " + _replay.PhysicsJobWorkers, true);
                    added++;
                }
            }

            if (added == 0) Badge("source differs", true);
        }

        private void Badge(string text, bool differs)
        {
            var badge = new Label(text);
            badge.AddToClassList("badge");
            if (differs) badge.AddToClassList("badge--differs");
            _identBadges.Add(badge);
        }

        /// <summary>The five markers, each a bordered box with at most one child.</summary>
        private static void Marker(VisualElement marker, Provenance state)
        {
            if (marker == null) return;

            marker.Clear();
            marker.ClearClassList();
            marker.AddToClassList("marker");
            marker.pickingMode = PickingMode.Ignore;

            switch (state)
            {
                case Provenance.Faithful:
                    marker.AddToClassList("marker--faithful");
                    return;

                case Provenance.Unverified:
                case Provenance.Threaded:
                    marker.AddToClassList(
                        state == Provenance.Unverified ? "marker--unverified" : "marker--threaded");
                    marker.Add(Child("marker__half"));
                    return;

                case Provenance.Cousin:
                    marker.AddToClassList("marker--cousin");
                    marker.Add(Child("marker__diamond"));
                    return;

                case Provenance.Diverged:
                    marker.AddToClassList("marker--diverged");
                    marker.Add(Child("marker__bar"));
                    return;

                default:
                    marker.AddToClassList("marker--unverifiable");
                    marker.Add(Child("marker__quad"));
                    return;
            }
        }

        private static VisualElement Child(string className)
        {
            var element = new VisualElement();
            element.AddToClassList(className);
            element.pickingMode = PickingMode.Ignore;
            return element;
        }

        // ---------------------------------------------------------------- the alarm

        /// <summary>
        /// The vermilion plate: the two identities, and nothing else.
        /// </summary>
        /// <remarks>
        /// The energy audit and the matter residual are the world's two conservation laws, and
        /// both are meant to read zero for the life of a run. Nothing else on the census is an
        /// alarm — mean depth, matter here and the population all move in a healthy world, and a
        /// plate that fired on them would teach a viewer to ignore the plate.
        /// </remarks>
        private void Alarm(WorldCensus census)
        {
            bool auditBroken = Math.Abs(census.AuditPercent) > AuditPercentTolerance;

            double matterScale = Math.Max(1d, Math.Abs(census.MatterStanding));
            bool matterBroken =
                Math.Abs(census.MatterResidual) >
                Math.Max(MatterResidualFloor, MatterResidualRelative * matterScale);

            bool broken = auditBroken || matterBroken;

            if (broken)
            {
                _alarmText.text = auditBroken
                    ? "AUDIT " + TheatreUiFormat.Fixed(census.AuditPercent, 4) + " % " +
                      TheatreUiFormat.EmDash + " ENERGY LEDGER DOES NOT BALANCE"
                    : "MATTER RESIDUAL " + TheatreUiFormat.Fixed(census.MatterResidual, 3) + " " +
                      TheatreUiFormat.EmDash + " MATTER IS NOT CONSERVED";
            }

            if (broken == _alarm) return;

            _alarm = broken;

            Show(_alarmPlate, broken);
            Show(_status, !broken);
            _strip.EnableInClassList("panel--strip-alarm", broken);
            _census.EnableInClassList("panel--census-alarm", broken);
            _timelineHead.EnableInClassList("timeline__head--broken", broken);

            if (broken) Mark("timeline__mark--break", census.T);
            if (!broken) _alarmPlate.style.opacity = 1f;
        }

        /// <summary>
        /// The design's one piece of motion: a 1.4 s fade on whichever abnormal element is up.
        /// </summary>
        /// <remarks>
        /// USS has transitions and no keyframes, so this is a tween on one element, running only
        /// while the state is abnormal (the design's exception 1). It touches one property on one
        /// element and nothing else, and a still picture of it still reads: the shape and the
        /// plate carry the state whatever the opacity is doing.
        /// </remarks>
        private void Pulse()
        {
            VisualElement pulsing =
                _alarm ? _alarmPlate : _state == Provenance.Diverged ? _statusMarker : null;

            if (pulsing == null) return;

            float phase = Mathf.PingPong(Time.unscaledTime / 1.4f, 1f);
            pulsing.style.opacity = Mathf.Lerp(0.55f, 1f, phase);
        }

        // ---------------------------------------------------------------- warnings

        /// <summary>
        /// The three the record carries and the one the clock makes, each a title and a detail.
        /// </summary>
        /// <remarks>
        /// They grow downward on the left over open water, so the census never moves when one
        /// appears or clears (design/SPEC.md §9 item 4). Rebuilt only when the set changes, which
        /// for the record's three is once.
        /// </remarks>
        private void Warnings()
        {
            RunRecord record = _replay.Record;
            var titles = new List<string>();
            var details = new List<string>();

            if (record.ConfigHashMismatch != null)
            {
                titles.Add("The config's hash does not match its settings");
                details.Add(record.ConfigHashMismatch);
            }

            if (record.StepDisagreement != null)
            {
                titles.Add("The manifest and the config disagree on the step");
                details.Add(record.StepDisagreement);
            }

            if (record.SamplesNote != null)
            {
                titles.Add("The record's samples");
                details.Add(record.SamplesNote);
            }

            if (PastTheRecord())
            {
                titles.Add("Running past the record");
                details.Add(
                    "last recorded sample t=" +
                    TheatreUiFormat.Seconds(_replay.RecordedThroughSeconds) + " " +
                    TheatreUiFormat.Dot + " " + BeyondSamples() + " samples beyond " +
                    TheatreUiFormat.Dot + " nothing left to check against");
            }

            string signature = string.Join("|", details.ToArray());
            if (signature == _warningSignature) return;

            _warningSignature = signature;
            _warnings.Clear();

            for (int i = 0; i < titles.Count; i++)
            {
                var warning = new VisualElement();
                warning.AddToClassList("warning");
                if (i == 0) warning.AddToClassList("lead");
                warning.pickingMode = PickingMode.Ignore;

                VisualElement marker = Child("marker");
                marker.AddToClassList("marker--unverified");
                marker.AddToClassList("marker--sm");
                marker.Add(Child("marker__half"));

                var body = new VisualElement();
                body.AddToClassList("warning__body");
                body.pickingMode = PickingMode.Ignore;

                var title = new Label(titles[i]);
                title.AddToClassList("warning__title");

                var detail = new Label(details[i]);
                detail.AddToClassList("warning__detail");

                body.Add(title);
                body.Add(detail);
                warning.Add(marker);
                warning.Add(body);
                _warnings.Add(warning);
            }

            ShowWarningsOrPopover();
        }

        private string BeyondSamples()
        {
            double interval = SampleInterval();
            if (interval <= 0d) return "some";

            double beyond = (_replay.ElapsedSeconds - _replay.RecordedThroughSeconds) / interval;
            return TheatreUiFormat.Quantity(Math.Max(0d, beyond));
        }

        // ---------------------------------------------------------------- the popover

        /// <summary>P: the evidence behind the strip's one word, and the recorded value beside it.</summary>
        public void ToggleProvenance()
        {
            if (_root == null || _replay == null) return;

            ProvenanceOpen = !ProvenanceOpen;

            if (ProvenanceOpen) Popover();
            ShowWarningsOrPopover();
        }

        /// <summary>The two share an anchor and are mutually exclusive, by design.</summary>
        private void ShowWarningsOrPopover()
        {
            Show(_popover, ProvenanceOpen);
            Show(_warnings, !ProvenanceOpen && _warnings.childCount > 0);
        }

        private void Popover()
        {
            if (_popoverBuilt || _replay == null) return;

            _popoverBuilt = true;

            RunRecord record = _replay.Record;

            Marker(_popoverMarker, _state);
            _popoverWord.text = Word(_state);
            _popoverNote.text = Note(_state);
            PopoverStatus(_state != Provenance.Faithful);
            _popoverProse.text = Prose(_state);

            // Hashed once, on the first P of a session: HashSourceTree walks every .cs under two
            // trees, which is not something to do on a frame.
            if (_liveCoreHash == null) _liveCoreHash = BuildIdentity.CoreHash();
            if (_liveSimHash == null) _liveSimHash = BuildIdentity.SimHash();

            _popoverRows.Clear();

            string recordedJobs = record.PhysicsJobWorkers.HasValue
                ? TheatreUiFormat.Identifier(record.PhysicsJobWorkers.Value)
                : "unrecorded";

            bool jobsDiffer =
                !record.PhysicsJobWorkers.HasValue ||
                record.PhysicsJobWorkers.Value != _replay.PhysicsJobWorkers;

            ProvRow("physics jobs", TheatreUiFormat.Identifier(_replay.PhysicsJobWorkers),
                "recorded " + recordedJobs, jobsDiffer);

            bool buildDiffers =
                record.UnityVersion != null && record.UnityVersion != Application.unityVersion;

            ProvRow("build", Application.unityVersion,
                record.UnityVersion == null
                    ? "unrecorded"
                    : buildDiffers ? "recorded " + record.UnityVersion : "match",
                buildDiffers);

            bool coreDiffers = !Same(_liveCoreHash, record.CoreHash);
            ProvRow("core hash", TheatreUiFormat.Hash(_liveCoreHash),
                record.CoreHash == null
                    ? "unrecorded"
                    : coreDiffers ? "recorded " + TheatreUiFormat.Hash(record.CoreHash) : "match",
                coreDiffers);

            bool simDiffers = !Same(_liveSimHash, record.SimHash);
            ProvRow("sim hash", TheatreUiFormat.Hash(_liveSimHash),
                record.SimHash == null
                    ? "unrecorded"
                    : simDiffers ? "recorded " + TheatreUiFormat.Hash(record.SimHash) : "match",
                simDiffers);

            ProvRow("config hash", TheatreUiFormat.Hash(record.ConfigHash),
                record.ConfigHashMismatch == null ? "match" : "does not match its settings",
                record.ConfigHashMismatch != null);

            ProvRow("seed", TheatreUiFormat.Identifier((long)record.Seed), "as recorded", false);

            bool mapBroken = _map != null && !_map.Reliable;
            ProvRow("id map", mapBroken ? "unverified" : "checked",
                mapBroken ? _map.Note : "selection ok", mapBroken);

            ProvRow("samples", Coverage(),
                TheatreUiFormat.Quantity(record.Samples.Count) + " recorded",
                _replay.FirstMismatch != null);
        }

        private void PopoverStatus(bool abnormal) =>
            _popoverStatus?.EnableInClassList("status--abnormal", abnormal);

        private void ProvRow(string label, string value, string recorded, bool differs)
        {
            var row = new VisualElement();
            row.AddToClassList("prov-row");
            row.pickingMode = PickingMode.Ignore;

            var mark = new Label(differs ? TheatreUiFormat.Times : TheatreUiFormat.Dot);
            mark.AddToClassList("prov-row__mark");
            if (!differs) mark.AddToClassList("prov-row__mark--ok");

            var name = new Label(label);
            name.AddToClassList("prov-row__label");

            var reading = new Label(value);
            reading.AddToClassList("prov-row__value");
            if (!differs) reading.AddToClassList("prov-row__value--ok");

            var was = new Label(recorded);
            was.AddToClassList("prov-row__recorded");
            if (!differs) was.AddToClassList("prov-row__recorded--ok");

            row.Add(mark);
            row.Add(name);
            row.Add(reading);
            row.Add(was);
            _popoverRows.Add(row);
        }

        private static string Note(Provenance state)
        {
            switch (state)
            {
                case Provenance.Faithful: return "this is the run";
                case Provenance.Unverified: return "the same world, past the checking";
                case Provenance.Threaded: return "the threads cannot be vouched for";
                case Provenance.Cousin:
                    return "a valid world " + TheatreUiFormat.Dot + " not this run";
                case Provenance.Diverged: return "it parted from the record";
                default: return "the world is right, the names are not";
            }
        }

        private static string Prose(Provenance state)
        {
            switch (state)
            {
                case Provenance.Faithful:
                    return "Every sample compared so far matches the recording exactly, on this " +
                           "build and at the thread count the run was made at. What is on screen " +
                           "is the run in the logbook.";

                case Provenance.Unverified:
                    return "The world has run past the record's last sample. It is the same " +
                           "deterministic world, still stepping, and there is nothing left to " +
                           "check it against: nothing after that instant is verified.";

                case Provenance.Threaded:
                    return "The physics ran on job worker threads, or the manifest never said how " +
                           "many. The shared world parts from itself after about 148,000 steps " +
                           "when two bodies in contact are solved in a different order (D078), so " +
                           "nothing here can promise this is the same realisation.";

                case Provenance.Cousin:
                    return "Nothing here is written back. What you are watching is a legitimate " +
                           "world grown from the same config and seed, but it is not the run in " +
                           "the logbook " + TheatreUiFormat.EmDash +
                           " do not quote its numbers as that run's.";

                case Provenance.Diverged:
                    return "The replay disagreed with the recording at a sample that was " +
                           "compared. From that instant this is a different realisation, and the " +
                           "numbers on screen are its own.";

                default:
                    return "The creature-id map could not be checked against the recording, so a " +
                           "click can point at a body but cannot name it. The world itself is " +
                           "unaffected.";
            }
        }

        // ---------------------------------------------------------------- the inspector

        /// <summary>
        /// Three states and a fourth: nothing selected, alive, dead, and selection unavailable.
        /// </summary>
        private void Inspector(long selectedId, float speed)
        {
            bool unreliable = _map != null && !_map.Reliable;

            _inspector.RemoveFromClassList("panel--inspector-empty");
            _inspector.RemoveFromClassList("panel--inspector-dead");
            _inspector.RemoveFromClassList("panel--inspector-unverifiable");

            if (unreliable)
            {
                _inspector.AddToClassList("panel--inspector-unverifiable");
                _unavailableProse.text =
                    "The creature-id map could not be checked against the recording, so a click " +
                    "can point at a body but cannot name it." +
                    (string.IsNullOrEmpty(_map.Note) ? "" : " " + _map.Note + ".");

                Only(_inspectorGone);
                return;
            }

            if (selectedId < 0)
            {
                _inspector.AddToClassList("panel--inspector-empty");
                Only(_inspectorEmpty);
                return;
            }

            Organism creature = CreatureIdMap.Find(_replay.Eco.World, selectedId);

            if (creature != null)
            {
                Living(creature, speed);
                Only(_inspectorLive);
                return;
            }

            _inspector.AddToClassList("panel--inspector-dead");
            Dead(selectedId);
            Only(_inspectorDead);
        }

        private void Only(VisualElement block)
        {
            Show(_inspectorEmpty, block == _inspectorEmpty);
            Show(_inspectorLive, block == _inspectorLive);
            Show(_inspectorDead, block == _inspectorDead);
            Show(_inspectorGone, block == _inspectorGone);
        }

        private void Living(Organism creature, float speed)
        {
            _inspectorId.text = "creature " + TheatreUiFormat.Identifier(creature.Id);
            _inspectorBadge.text = "ALIVE";

            _valueGeneration.text = TheatreUiFormat.Identifier(creature.GenerationDepth);
            _valueParent.text = creature.ParentId >= 0
                ? TheatreUiFormat.Identifier(creature.ParentId)
                : "founder";

            _valueReserve.text = float.IsPositiveInfinity(creature.SecondsOfReserve)
                ? "inf"
                : TheatreUiFormat.Quantity(creature.SecondsOfReserve);

            _valueSpeed.text = TheatreUiFormat.Fixed(speed, 2);

            int parts = creature.Phenotype != null ? creature.Phenotype.PartCount : 0;
            int dof = 0;

            if (creature.Phenotype != null)
            {
                for (int i = 0; i < creature.Phenotype.Parts.Count; i++)
                {
                    dof += creature.Phenotype.Parts[i].JointType.DofCount();
                }
            }

            _valuePartsDof.text =
                TheatreUiFormat.Identifier(parts) + " " + TheatreUiFormat.Dot + " " +
                TheatreUiFormat.Identifier(dof);

            Guilds(creature, dof > 0);
            Ancestry(creature.Id, _ancestry, _ancestryChain, _ancestryNote);
        }

        /// <summary>
        /// The three guild chips. An absent guild is struck rather than dropped: which of the
        /// three a body does not have is as much of an answer as which it has.
        /// </summary>
        private void Guilds(Organism creature, bool jointed)
        {
            _guilds.Clear();

            Chip("absorptive", creature.HasAbsorptiveTissue, true);
            Chip("jointed", jointed, false);
            Chip("photosynthetic", creature.HasPhotosyntheticTissue, false);
        }

        private void Chip(string text, bool on, bool first)
        {
            if (on)
            {
                var chip = new Label(text);
                chip.AddToClassList("filter-chip");
                chip.AddToClassList("filter-chip--on");
                if (first) chip.AddToClassList("lead");
                _guilds.Add(chip);
                return;
            }

            var wrapper = new VisualElement();
            wrapper.AddToClassList("struck");
            wrapper.AddToClassList("struck--after-chip");
            wrapper.pickingMode = PickingMode.Ignore;

            var off = new Label(text);
            off.AddToClassList("filter-chip");
            off.AddToClassList("filter-chip--off");
            off.AddToClassList("lead");

            var rule = new VisualElement();
            rule.AddToClassList("struck__rule");
            rule.pickingMode = PickingMode.Ignore;

            wrapper.Add(off);
            wrapper.Add(rule);
            _guilds.Add(wrapper);
        }

        /// <summary>
        /// A creature the world no longer holds. Everything shown comes from
        /// <c>lineage.jsonl</c>, which is read the first time a viewer asks for it.
        /// </summary>
        private void Dead(long id)
        {
            _deadId.text = "creature " + TheatreUiFormat.Identifier(id);

            if (_lineage == null) _lineage = LineageIndex.Read(_runDirectory);

            if (!_lineage.Available || !_lineage.TryFind(id, out LineageIndex.Entry entry))
            {
                _deadBadge.text = "DIED";
                _valueLived.text = TheatreUiFormat.EmDash;
                _valueDeadGeneration.text = TheatreUiFormat.EmDash;
                _valueChildren.text = TheatreUiFormat.EmDash;
                _ancestryDeadChain.text = _lineage.Note ?? "not in lineage.jsonl";
                return;
            }

            _deadBadge.text = entry.Died
                ? "DIED t=" + TheatreUiFormat.Seconds(entry.DiedAt)
                : "GONE FROM THE WATER";

            _valueLived.text = entry.Died && !double.IsNaN(entry.BornAt)
                ? TheatreUiFormat.Seconds(entry.DiedAt - entry.BornAt)
                : TheatreUiFormat.EmDash;

            _valueDeadGeneration.text = entry.Generation >= 0
                ? TheatreUiFormat.Identifier(entry.Generation)
                : TheatreUiFormat.EmDash;

            _valueChildren.text = TheatreUiFormat.Identifier(entry.Children);

            Ancestry(id, _ancestryDead, _ancestryDeadChain, null);
        }

        private void Ancestry(long id, VisualElement section, Label chain, Label note)
        {
            if (_lineage == null) _lineage = LineageIndex.Read(_runDirectory);

            int depth = 0;
            long founder = -1L;
            string text = _lineage.Available ? _lineage.Chain(id, out depth, out founder) : null;

            if (text == null)
            {
                Show(section, false);
                return;
            }

            Show(section, true);
            chain.text = text;

            if (note == null) return;

            int shown = Math.Min(LineageIndex.ChainShown, depth);
            note.text =
                TheatreUiFormat.Identifier(shown) + " of " + TheatreUiFormat.Identifier(depth) +
                " shown " + TheatreUiFormat.Dot + " founder " + TheatreUiFormat.Identifier(founder);
        }

        // ---------------------------------------------------------------- the bar

        private void BuildTheTimeline()
        {
            double recorded = _replay.RecordedThroughSeconds;
            double requested = _replay.Record.RequestedSeconds ?? 0d;

            _axisEndSeconds = Math.Max(1d, Math.Max(requested, recorded));

            double beyond = Math.Max(0d, _axisEndSeconds - recorded);
            _timelineBeyond.style.width = Length.Percent((float)(100d * beyond / _axisEndSeconds));

            _tickEnd.text = TheatreUiFormat.Seconds(_axisEndSeconds) + " s";

            // R reopens the run into the same tree, so last time's marks have to go or the axis
            // would carry two record-ends and Marks would decline to draw the new one.
            for (int i = _timelineTrack.childCount - 1; i >= 0; i--)
            {
                VisualElement child = _timelineTrack[i];
                if (child.ClassListContains("timeline__mark")) _timelineTrack.RemoveAt(i);
            }

            Show(_tickPeak, false);
            Show(_tickRecordEnd, false);

            Dashes();
            Marks();
        }

        /// <summary>The dashed rule of the unverified future, as many 4x2 children as it takes.</summary>
        private void Dashes()
        {
            float width = _timelineBeyond.resolvedStyle.width;
            int wanted = width <= 0f ? 0 : Mathf.Clamp(Mathf.FloorToInt(width / 7f), 0, 400);

            if (_timelineBeyond.childCount == wanted) return;

            _timelineBeyond.Clear();

            for (int i = 0; i < wanted; i++)
            {
                VisualElement dash = Child("timeline__dash");
                if (i == 0) dash.AddToClassList("lead");
                _timelineBeyond.Add(dash);
            }
        }

        /// <summary>
        /// The marks on the track: the peak population, every snapshot on disk, and the record's
        /// end. Drawn once per open; the break mark is added when an identity breaks.
        /// </summary>
        private void Marks()
        {
            if (_timelineTrack == null || _replay == null) return;
            if (_timelineTrack.Q<VisualElement>("mark-record-end") != null) return;

            double recorded = _replay.RecordedThroughSeconds;

            // The peak is free: RunSample already carries alive, and the samples are all loaded.
            IReadOnlyList<RunSample> samples = _replay.Record.Samples;
            int peak = 0;
            double peakAt = 0d;

            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].Alive <= peak) continue;
                peak = samples[i].Alive;
                peakAt = samples[i].T;
            }

            if (peak > 0)
            {
                VisualElement mark = Mark("timeline__mark--peak", peakAt);
                mark.name = "mark-peak";

                Show(_tickPeak, true);
                _tickPeak.text = "peak " + TheatreUiFormat.Quantity(peak);
                Anchor(_tickPeak, peakAt);
            }

            foreach (double t in SnapshotSeconds())
            {
                Mark("timeline__mark--snapshot", t);
            }

            VisualElement end = Mark("timeline__mark--record-end", recorded);
            end.name = "mark-record-end";

            Show(_tickRecordEnd, true);
            _tickRecordEnd.text = "record ends " + TheatreUiFormat.Seconds(recorded) + " s";
            Anchor(_tickRecordEnd, recorded);
        }

        private VisualElement Mark(string className, double seconds)
        {
            VisualElement mark = Child("timeline__mark");
            mark.AddToClassList(className);
            mark.style.left = Length.Percent(Percent(seconds));
            _timelineTrack.Add(mark);
            return mark;
        }

        /// <summary>
        /// An axis label centred under its mark: a left in percent, with half the label's own
        /// width taken off as a negative margin by the callback registered at build time.
        /// </summary>
        /// <remarks>
        /// USS has no transform-based centring that survives text measurement, so the offset has
        /// to be applied after the text has been laid out. The callback is registered once, in
        /// <see cref="Centre"/>, rather than here: registering it per run would stack one
        /// subscription per reload on the same two labels.
        /// </remarks>
        private void Anchor(Label tick, double seconds) =>
            tick.style.left = Length.Percent(Percent(seconds));

        /// <summary>Keeps a label centred on its own left edge, whatever it says.</summary>
        private static void Centre(Label tick)
        {
            if (tick == null) return;

            tick.RegisterCallback<GeometryChangedEvent>(
                _ => tick.style.marginLeft = -0.5f * tick.resolvedStyle.width);
        }

        private float Percent(double seconds) =>
            Mathf.Clamp((float)(100d * seconds / _axisEndSeconds), 0f, 100f);

        /// <summary>Every snapshot the run wrote, by the second in its file name.</summary>
        private IEnumerable<double> SnapshotSeconds()
        {
            string directory = Path.Combine(_runDirectory ?? "", "snapshots");
            if (!Directory.Exists(directory)) yield break;

            foreach (string path in Directory.GetFiles(directory))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name)) continue;

                if (double.TryParse(name, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out double seconds))
                {
                    yield return seconds;
                }
            }
        }

        /// <summary>The one width of the per-frame contract.</summary>
        private void Progress(double t)
        {
            double recorded = _replay.RecordedThroughSeconds;

            if (t > _axisEndSeconds)
            {
                // A run stepped past its own axis: widen once rather than pin the head at the end
                // and lie about where it is.
                _axisEndSeconds = t;
                _tickEnd.text = TheatreUiFormat.Seconds(_axisEndSeconds) + " s";
            }

            _timelineElapsed.style.width = Length.Percent(Percent(Math.Min(t, recorded)));

            double over = t - recorded;
            Show(_timelineElapsedBeyond, over > 0d);

            if (over > 0d)
            {
                _timelineElapsedBeyond.style.width = Length.Percent(Percent(over));
            }
        }

        private void Seeking(TheatreUiState state)
        {
            Show(_seekPlate, state.Seeking);
            Show(_seekMeta, state.Seeking);
            Show(_seekbar, state.Seeking);

            if (!state.Seeking)
            {
                Transport(state, "PLAYING", "PAUSED");
                return;
            }

            _seekPlateText.text =
                "SEEKING " + TheatreUiFormat.RightArrow + " t " +
                TheatreUiFormat.Clock(state.SeekTarget) + " s";

            double left = state.SeekTarget - _replay.ElapsedSeconds;
            double eta = state.MeasuredPace > 0.001d ? left / state.MeasuredPace : double.NaN;

            _seekMeta.text =
                "simulating " + TheatreUiFormat.Dot + " " +
                TheatreUiFormat.Fixed(state.MeasuredPace, 1) + TheatreUiFormat.Times +
                " real time " + TheatreUiFormat.Dot + " " +
                (double.IsNaN(eta) ? "measuring" : TheatreUiFormat.Remaining(eta));

            double span = Math.Max(1e-6d, state.SeekTarget - state.SeekFrom);
            double done = Mathf.Clamp01((float)((_replay.ElapsedSeconds - state.SeekFrom) / span));
            _seekProgress.style.width = Length.Percent((float)(100d * done));

            _transportWord.text = "SEEKING";
            Show(_glyphPlay, true);
            Show(_glyphPause, false);
        }

        private void Transport(TheatreUiState state, string playing, string paused)
        {
            bool isPaused = state.Paused;

            _transportWord.text = _alarm && isPaused ? "PAUSED AT FAILURE" : isPaused ? paused : playing;
            Show(_glyphPlay, !isPaused);
            Show(_glyphPause, isPaused);
        }

        // ---------------------------------------------------------------- the solo census

        /// <summary>
        /// Mode A's census: the same panel, the readings a single creature has (design/SPEC.md §8).
        /// </summary>
        /// <remarks>
        /// <b>No global neurons.</b> The line this replaces printed
        /// <c>Genome.GlobalBrain.Length</c>, which D081 retired on 2026-09-07: a child is born
        /// without one, a rewire never draws the input kind, and <c>Brain.For</c> builds no
        /// partless group, so stored genomes carry one or two neurons that this build never
        /// evaluates. <c>Brain.NeuronCount</c> is the body's count and is what this shows.
        /// </remarks>
        private void BuildTheSoloCensus()
        {
            _censusSolo.Clear();
            _soloValues.Clear();
            _soloSensorValues.Clear();

            Title(_censusSolo, "THE BODY");
            SoloRow("parts", TheatreUiFormat.Identifier(_solo.Phenotype.PartCount), null);
            SoloRow("dof", TheatreUiFormat.Identifier(_solo.Instance.TotalDof), null);
            SoloRow("neurons", TheatreUiFormat.Identifier(_solo.Brain.NeuronCount), null);

            VisualElement swimming = Section(_censusSolo, "SWIMMING");
            SoloRow("speed", "0.00", "m/s", swimming, true);
            SoloRow("travelled", "0.0", "m", swimming, true);
            SoloRow("depth", "0.0", "m", swimming, true);
            SoloRow("joint rate", "0.00", "rad/s", swimming, true);

            VisualElement drive = Section(_censusSolo, "DRIVE");
            SoloRow("reserve", "0", "s", drive, true);
            SoloRow("driven by", "its brain", null, drive, true);
            SoloRow("smell field", "the world's", null, drive, true);

            VisualElement sensors = Section(_censusSolo, "SENSORS, AT THE ROOT PART");

            _solo.ReadSensors(_sensorLines);

            for (int i = 0; i < _sensorLines.Count; i++)
            {
                string line = _sensorLines[i];
                int space = line.LastIndexOf(' ');

                string name = space > 0 ? line.Substring(0, space) : line;
                string value = space > 0 ? line.Substring(space + 1) : "";

                VisualElement row = Row(sensors, name, value, null, true);
                row.AddToClassList("census-row--sensor");
                _soloSensorValues.Add(row.Q<Label>("solo-value"));
            }
        }

        private void SoloCensus()
        {
            if (_soloValues.Count < 7) return;

            _soloValues[0].text = TheatreUiFormat.Fixed(_solo.Speed, 2);
            _soloValues[1].text = TheatreUiFormat.Fixed(_solo.Travelled, 1);
            _soloValues[2].text = TheatreUiFormat.Fixed(_solo.Depth, 1);
            _soloValues[3].text = TheatreUiFormat.Fixed(_solo.MeanJointRate(), 2);

            _soloValues[4].text = TheatreUiFormat.Quantity(_solo.ReserveSeconds) +
                                  (_solo.Starving ? " falling" : " held");

            _soloValues[5].text = _solo.UseTestSine ? "THE TEST SINE" : "its brain";

            _soloValues[6].text = _solo.SmellDensity > 0f
                ? TheatreUiFormat.Fixed(_solo.SmellDensity, 3) + " by hand"
                : "the world's";

            _solo.ReadSensors(_sensorLines);

            for (int i = 0; i < _soloSensorValues.Count && i < _sensorLines.Count; i++)
            {
                string line = _sensorLines[i];
                int space = line.LastIndexOf(' ');
                _soloSensorValues[i].text = space > 0 ? line.Substring(space + 1) : line;
            }
        }

        private static void Title(VisualElement parent, string text)
        {
            var title = new Label(text);
            title.AddToClassList("section__title");
            parent.Add(title);
        }

        private static VisualElement Section(VisualElement parent, string title)
        {
            var section = new VisualElement();
            section.AddToClassList("section");
            section.pickingMode = PickingMode.Ignore;
            Title(section, title);
            parent.Add(section);
            return section;
        }

        private void SoloRow(string label, string value, string unit) =>
            SoloRow(label, value, unit, _censusSolo, false);

        private void SoloRow(string label, string value, string unit, VisualElement parent, bool live)
        {
            VisualElement row = Row(parent, label, value, unit, true);
            if (live) _soloValues.Add(row.Q<Label>("solo-value"));
        }

        private static VisualElement Row(
            VisualElement parent, string label, string value, string unit, bool named)
        {
            var row = new VisualElement();
            row.AddToClassList("census-row");
            row.AddToClassList("field");
            row.pickingMode = PickingMode.Ignore;

            var name = new Label(label);
            name.AddToClassList("field__label");

            var holder = new VisualElement();
            holder.AddToClassList("field__value");
            holder.AddToClassList("numeric");
            holder.AddToClassList("numeric--wide");
            holder.pickingMode = PickingMode.Ignore;

            var reading = new Label(value);
            if (named) reading.name = "solo-value";
            holder.Add(reading);

            if (unit != null)
            {
                var suffix = new Label(unit);
                suffix.AddToClassList("unit");
                holder.Add(suffix);
            }

            row.Add(name);
            row.Add(holder);
            parent.Add(row);
            return row;
        }

        // ---------------------------------------------------------------- odds and ends

        private bool PastTheRecord() =>
            _replay != null && _replay.ElapsedSeconds > _replay.RecordedThroughSeconds + 1e-6;

        private double SampleInterval()
        {
            IReadOnlyList<RunSample> samples = _replay.Record.Samples;
            return samples.Count >= 2 ? samples[1].T - samples[0].T : 0d;
        }

        private string Cadence()
        {
            int compared = _replay.SamplesMatched + _replay.SamplesSkipped;
            double interval = SampleInterval();

            return "sample " + TheatreUiFormat.Identifier(compared) +
                   (interval > 0d
                       ? " " + TheatreUiFormat.Dot + " every " +
                         TheatreUiFormat.Seconds(interval) + " s"
                       : "");
        }

        private static bool Same(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) &&
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static void Show(VisualElement element, bool shown) =>
            element?.EnableInClassList("is-gone", !shown);

        public void Dispose()
        {
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
                _host = null;
            }

            if (_settings != null)
            {
                UnityEngine.Object.Destroy(_settings);
                _settings = null;
            }

            _document = null;
            _root = null;
        }
    }
}
