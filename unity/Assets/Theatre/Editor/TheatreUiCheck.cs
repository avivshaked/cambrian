using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// The interface, checked against the numbers it claims to be showing — in Play mode, in the
    /// runner's own frame, the way this project checks everything else about the theatre.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why an entry point rather than the Test Framework.</b> The theatre already has two
    /// batchmode entries that enter Play mode and exit 0 or 1 —
    /// <see cref="TheatreIdentityCheck.RunInPlayMode"/> and <see cref="TheatreSnapshot"/> — and the
    /// package the other way would need (<c>com.unity.test-framework</c> with
    /// <c>-runTests -testPlatform PlayMode</c>) is not what any of this is written against. One
    /// harness style is easier to keep than two.
    /// </para>
    /// <para>
    /// <b>What it can prove and what it cannot.</b> It can prove that the label named
    /// <c>value-alive</c> carries the living count and not the death count, that a number over a
    /// thousand is grouped with a thin space, that the states the design names produce the classes
    /// the stylesheet draws, and that a dead creature's panel agrees with a count this file makes
    /// itself out of <c>lineage.jsonl</c>. It cannot prove that any of it is legible, which is
    /// what the pictures are for (logbook/specs/theatre-ui-test-spec.md §2): a picture the agent
    /// cannot read as the design intends is a failure whatever this printed.
    /// </para>
    /// <code>
    /// # a world, with the interface on. NO -quit, and NO -nographics: it needs frames and a
    /// # graphics device, exactly like the snapshot entry.
    /// $env:EVOSIM_THEATRE_RUN = "$PWD/runs/uicheck"
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', "$PWD/unity-w6", '-batchmode',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreUiCheck.Run',
    ///   '-logFile', "$PWD/scratch/logs/theatre-ui-check.log")
    ///
    /// # the cousin states: the same entry against a run this build did not record
    /// $env:EVOSIM_THEATRE_OVERRIDE = '1'
    ///
    /// # the solo census: the same entry with a genome instead of a run
    /// $env:EVOSIM_THEATRE_GENOME = "$PWD/inocula/some-genome.json"
    /// </code>
    /// </remarks>
    public static class TheatreUiCheck
    {
        private const string PendingKey = "Evosim.Theatre.UiCheck.Pending";
        private const string ExitKey = "Evosim.Theatre.UiCheck.Exit";

        /// <summary>Frames to let the interface settle between one state and the next.</summary>
        private const int SettleFrames = 3;

        private static double _wallSecondsAllowed;
        private static double _deadline;
        private static bool _driving;
        private static int _phase;
        private static int _settle;
        private static int _passed;
        private static int _failed;
        private static int _skipped;
        private static bool _paceSet;
        private static double _seekTarget;

        private static long _selected = -1L;
        private static double _deadBorn = double.NaN;
        private static double _deadDied = double.NaN;
        private static int _deadChildren;
        private static int _deadGeneration = -1;

        private static TheatreRunner _runner;
        private static readonly List<string> _shots = new List<string>();
        private static readonly List<Wanted> _queue = new List<Wanted>();

        /// <summary>Editor ticks a capture has been armed for, so the panel settles before it.</summary>
        private static int _armedTicks;

        /// <summary>One density reading per width, not one per photographed state.</summary>
        private static readonly HashSet<int> _densityRead = new HashSet<int>();

        /// <summary>A picture that has been asked for and not yet taken.</summary>
        private struct Wanted
        {
            public string State;
            public string Path;
            public int Width;
            public int Height;
        }

        /// <summary>
        /// The two sizes every state is photographed at.
        /// </summary>
        /// <remarks>
        /// Not one size and a supersize multiplier, which is what a screen capture offered and
        /// what this used to ask for. A panel pointed at a texture lays out at that texture's
        /// pixels (<see cref="TheatreUiCapture"/>), so the second size is not the first one
        /// enlarged — it is the interface at 3840 across, which is the only way the design's
        /// 3400-pixel density step (<c>.is-wider</c>) is reached on a machine whose Game View is
        /// smaller than that. The first is the ordinary desk, below the 2240 step.
        /// </remarks>
        private static readonly int[,] Sizes = { { 1920, 1080 }, { 3840, 2160 } };

        // ------------------------------------------------------------------ the entry

        [MenuItem("Evosim/Theatre/Check the interface (Play mode)")]
        public static void FromMenu() => Run();

        /// <summary>
        /// Batchmode entry point. <b>Launch it without <c>-quit</c> and without
        /// <c>-nographics</c>.</b> It enters Play mode, walks the interface's states, prints one
        /// line per assertion and exits the Editor with 0 or 1.
        /// </summary>
        public static void Run()
        {
            bool world = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN"));
            bool solo = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_GENOME"));

            if (!world && !solo)
            {
                Debug.LogError(
                    "[Theatre] neither EVOSIM_THEATRE_RUN nor EVOSIM_THEATRE_GENOME is set: " +
                    "there is nothing to check the interface against.");

                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Theatre] already in Play mode: stop it before asking for this.");
                return;
            }

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[Theatre] this Editor has no graphics device, so nothing is drawn and " +
                    "nothing can be photographed. Drop -nographics; -batchmode alone is right.");

                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            _wallSecondsAllowed = 60d * IntFrom("EVOSIM_THEATRE_WALL_MINUTES", 30, 1, 1440);

            if (!OpenTheTheatreScene()) return;

            Debug.Log("[Theatre] interface check: entering Play mode.");

            SessionState.SetString(
                PendingKey, _wallSecondsAllowed.ToString("R", CultureInfo.InvariantCulture));

            Arm();
            EditorApplication.EnterPlaymode();
        }

        private static bool OpenTheTheatreScene()
        {
            if (SceneManager.GetActiveScene().path == TheatreSceneBuilder.ScenePath) return true;

            if (!Application.isBatchMode)
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }

            if (File.Exists(TheatreSceneBuilder.ScenePath))
            {
                EditorSceneManager.OpenScene(TheatreSceneBuilder.ScenePath, OpenSceneMode.Single);
                return true;
            }

            if (TheatreSceneBuilder.Build()) return true;

            Debug.LogError("[Theatre] no theatre scene, and it could not be built.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return false;
        }

        [InitializeOnLoadMethod]
        private static void ResumeAcrossTheDomainReload()
        {
            string exit = SessionState.GetString(ExitKey, "");

            if (!string.IsNullOrEmpty(exit))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                SessionState.EraseString(ExitKey);
                Quit(int.TryParse(exit, out int code) ? code : 1);
                return;
            }

            string pending = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(pending)) return;

            _wallSecondsAllowed =
                double.TryParse(pending, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double wall)
                    ? wall
                    : 1800d;

            Arm();
        }

        private static void Arm()
        {
            _deadline = EditorApplication.timeSinceStartup + _wallSecondsAllowed;
            _phase = 0;
            _settle = 0;
            _passed = 0;
            _failed = 0;
            _skipped = 0;
            _paceSet = false;
            _selected = -1L;
            _runner = null;

            _queue.Clear();
            _densityRead.Clear();
            _armedTicks = 0;
            TheatreUiCapture.Disarm();

            if (_driving) return;

            _driving = true;
            EditorApplication.update += Drive;
        }

        // ------------------------------------------------------------------ the walk

        private static void Drive()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    Finish(1, "TIMED OUT at phase " + _phase);
                    return;
                }

                if (!EditorApplication.isPlaying) return;

                if (_runner == null)
                {
                    _runner = UnityEngine.Object.FindFirstObjectByType<TheatreRunner>();

                    if (_runner == null)
                    {
                        Finish(1, "no TheatreRunner in the scene: nothing is playing the run");
                        return;
                    }
                }

                if (_runner.Ui == null)
                {
                    if (!string.IsNullOrEmpty(_runner.Error))
                    {
                        Finish(1, "refused: " + _runner.Error);
                        return;
                    }

                    // Start may not have run yet on the first tick. Once it has, an interface that
                    // is still null is the fault this check exists to catch.
                    if (_runner.Replay != null || _phase > 0)
                    {
                        Finish(1, "the interface did not load: TheatreUi.Create returned nothing");
                    }

                    return;
                }

                // A run was named and no run opened. Before this guard, Step() read a null replay
                // as Mode A and walked the solo assertions instead, so a world the build refuses
                // to open passed as a creature: runs/r37-s1's config predates two of D089's
                // tunables, §9's refuse-rather-than-default rule turned it away, and the cousin
                // check reported "solo mode checked, 6 of 6" (2026-09-13). Mode A is what
                // EVOSIM_THEATRE_GENOME asks for and nothing else.
                if (_runner.Replay == null && WorldWasAsked())
                {
                    Finish(1,
                        "EVOSIM_THEATRE_RUN was set and no world opened, so there is no interface " +
                        "to check: " + (_runner.Error ?? "the runner gave no reason"));

                    return;
                }

                if (_settle > 0) { _settle--; return; }

                if (!_paceSet) SetThePace();

                // A picture takes two ticks (arm, then read back), so the phases wait while one
                // is in flight rather than every phase learning to span a tick.
                if (Photographing()) return;

                Step();
            }
            catch (Exception e)
            {
                Finish(1, e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
        }

        private static void SetThePace()
        {
            _paceSet = true;

            _runner.ShowOverlay = true;
            _runner.Paused = false;
            _runner.Rate = 200f;
            _runner.FrameBudgetSeconds = 0.1f;

            TheatreReplay replay = _runner.Replay;

            Debug.Log(
                "[Theatre] interface check: " +
                (replay != null
                    ? "arm " + replay.Record.ArmName + ", " + replay.Record.Samples.Count +
                      " recorded samples, last at t=" +
                      replay.RecordedThroughSeconds.ToString("0.#", CultureInfo.InvariantCulture)
                    : "solo mode") +
                ", window " + Screen.width + "x" + Screen.height);
        }

        /// <summary>One phase per tick, each one settling before the next reads the screen.</summary>
        private static void Step()
        {
            TheatreRunner runner = _runner;
            TheatreUi ui = runner.Ui;
            VisualElement root = ui.Root;

            if (runner.Replay == null)
            {
                // Mode A: the solo census, and the retired readout that must not come back.
                switch (_phase)
                {
                    case 0: Solo(root); break;
                    case 1: Shot("solo"); break;
                    default: Finish(_failed == 0 ? 0 : 1, "solo mode checked"); return;
                }

                Next();
                return;
            }

            TheatreReplay replay = runner.Replay;

            switch (_phase)
            {
                case 0:
                    // Nothing is checkable until a sample has been compared: before that the
                    // identity line says so and the census is a world one step old.
                    if (replay.SamplesMatched + replay.SamplesSkipped < 1 &&
                        replay.ElapsedSeconds < replay.RecordedThroughSeconds)
                    {
                        return;
                    }

                    break;

                case 1: Identity(root, replay); break;
                case 2: Census(root, replay); break;
                case 3: Timeline(root, replay); break;
                case 4: Shot("playing"); break;

                case 5:
                    runner.TogglePause();
                    break;

                case 6: Paused(root); Shot("paused"); break;

                case 7:
                    runner.TogglePause();
                    runner.ToggleProvenance();
                    break;

                case 8: Popover(root); Shot("provenance"); break;

                case 9:
                    runner.ToggleProvenance();
                    break;

                case 10: Warnings(root, replay); break;

                case 11:
                    _seekTarget = replay.ElapsedSeconds + 60d;
                    runner.BeginSeek(_seekTarget);
                    break;

                case 12: Seeking(root, runner); Shot("seeking"); break;

                case 13:
                    if (runner.Seeking) return;
                    Arrived(root);
                    break;

                // Select, then read. A phase of its own for each, because the interface draws the
                // selection on its next tick and reading it in the same one would be reading the
                // frame before the click.
                case 14: SelectSomethingAlive(runner, replay); break;
                case 15: Selection(root, replay); Shot("selected"); break;
                case 16: SelectSomethingDead(runner, replay); break;
                case 17: Dead(root); Shot("dead"); break;
                case 18: AskForAnIdThatIsNotHere(runner); break;
                case 19: Unavailable(root, runner); break;

                case 20:
                    runner.ShowOverlay = false;
                    break;

                case 21: Hidden(root); Shot("hidden"); break;

                case 22:
                    runner.ShowOverlay = true;
                    break;

                case 23: Back(root); break;

                default:
                    Finish(_failed == 0 ? 0 : 1,
                        _passed + " passed, " + _failed + " failed, " + _skipped + " skipped");
                    return;
            }

            Next();
        }

        private static void Next()
        {
            _phase++;
            _settle = SettleFrames;
        }

        // ------------------------------------------------------------------ the assertions

        private static void Identity(VisualElement root, TheatreReplay replay)
        {
            RunRecord record = replay.Record;

            Is("strip: arm", Text(root, "ident-arm"), record.ArmName ?? "run");

            // The state this check works out for itself, from the same five facts the design's
            // decision tree reads. Agreeing with TheatreUi is the point: two readings of one
            // world that disagree is exactly the bug worth catching.
            string expected =
                !replay.Faithful ? "COUSIN" :
                replay.FirstMismatch != null ? "DIVERGED" :
                replay.ThreadCaveat != null || replay.PhysicsJobWorkers > 0 ? "THREADS UNVERIFIED" :
                replay.ElapsedSeconds > replay.RecordedThroughSeconds + 1e-6 ||
                record.Samples.Count == 0 ? "FAITHFUL " + TheatreUiFormat.Dot + " UNVERIFIED" :
                null;

            string word = Text(root, "status-word");

            if (expected != null)
            {
                Is("strip: state", word, expected);
            }
            else
            {
                // Either FAITHFUL or the id-map state, which depends on a pairing that may have
                // failed at any birth; both are correct readings of this world.
                True("strip: state is faithful or ids-unverifiable",
                    word == "FAITHFUL" || word == "IDS UNVERIFIABLE", word);
            }

            string meta = Text(root, "ident-meta");

            // Decision 2: "faithful" always carries its coverage.
            if (word == "FAITHFUL" || word.StartsWith("FAITHFUL") || word == "THREADS UNVERIFIED")
            {
                True("strip: coverage is on the identity line",
                    meta.Contains("samples match") || meta.Contains("no sample reached") ||
                    meta.Contains("past the record") || meta.Contains("no recorded samples"),
                    meta);
            }

            if (word == "COUSIN")
            {
                True("strip: a cousin says it is not this run", meta.Contains("not this run"), meta);
                True("strip: a cousin carries a badge",
                    root.Q<VisualElement>("ident-badges").childCount > 0, "no badges");
            }

            // The clock is one of the four per-frame fields, and it is the one every picture is
            // read against.
            string clock = Text(root, "clock-value");
            True("strip: the clock says t", clock.StartsWith("t "), clock);
            Near("strip: the clock is the world's", Number(clock.Substring(2)), replay.Census.T, 0.2d);

            True("strip: the pace is not empty", Text(root, "pace").Length > 0, "");
            True("strip: transport says something",
                Text(root, "transport-word").Length > 0, "");

            // The seed is on the line in every state that has room for it; the three states whose
            // meta is a sentence about what went wrong say that instead, which is the design's.
            bool sentence = word == "COUSIN" || word == "DIVERGED" || word == "IDS UNVERIFIABLE";

            True("strip: the seed is named",
                sentence || meta.Contains("seed " + TheatreUiFormat.Identifier((long)record.Seed)),
                meta);
        }

        private static void Census(VisualElement root, TheatreReplay replay)
        {
            WorldCensus census = replay.Census;

            Grouped("census: alive", Text(root, "value-alive"), census.Alive);
            Grouped("census: jointed", Text(root, "value-jointed"), census.Jointed);
            Grouped("census: absorptive", Text(root, "value-absorptive"), census.Absorptive);
            Grouped("census: photosynthetic", Text(root, "value-photosynthetic"), census.Photosynthetic);
            Grouped("census: births", Text(root, "value-births"), census.Births);
            Grouped("census: deaths", Text(root, "value-deaths"), census.Deaths);

            Near("census: audit", Number(Text(root, "value-audit")), census.AuditPercent, 1e-4);
            Near("census: matter here", Number(Text(root, "value-matter-here")), census.MatterHere, 0.01d);
            Near("census: matter residual",
                Number(Text(root, "value-matter-residual")), census.MatterResidual, 0.002d);

            string depth = Text(root, "value-mean-depth");

            if (census.MeanHeight < 0d)
            {
                True("census: depth carries a true minus (U+2212)",
                    depth.StartsWith(TheatreUiFormat.Minus), depth);
            }

            Near("census: mean depth", Number(depth), census.MeanHeight, 0.1d);

            bool diverged = census.Diverged > 0;
            Is("census: the diverged row is present only when something diverged",
                Shown(root, "row-diverged").ToString(), diverged.ToString());

            True("census: the cadence line names a sample",
                Text(root, "census-cadence").StartsWith("sample "), Text(root, "census-cadence"));
        }

        private static void Timeline(VisualElement root, TheatreReplay replay)
        {
            VisualElement elapsed = root.Q<VisualElement>("timeline-elapsed");
            VisualElement beyond = root.Q<VisualElement>("timeline-beyond");

            True("bar: the elapsed bar has a width", elapsed.resolvedStyle.width >= 0f, "");

            True("bar: the record's end is marked",
                root.Q<VisualElement>("mark-record-end") != null, "no record-end mark");

            True("bar: the unverified future is dashed",
                beyond.childCount > 0 || replay.RecordedThroughSeconds >= AxisEnd(replay),
                beyond.childCount + " dashes");

            True("bar: the axis ends with a second",
                Text(root, "tick-end").EndsWith(" s"),
                Shown(root, "tick-end") ? Text(root, "tick-end") : "folded into the record's label");

            NoGarble(root);
        }

        /// <summary>
        /// No two axis labels share a spot.
        /// </summary>
        /// <remarks>
        /// The four of them are placed independently — zero is flush left, the axis's end flush
        /// right, the peak and the record's end anchored in percent — and on a run recorded to
        /// the second it was asked for, with its peak at the last sample, three of the four land
        /// on the same pixel. Every frame of the first Editor run had them drawn over each other
        /// at the bottom right, which reads as garble and not as a fault, so nobody would think
        /// to look at the numbers (2026-09-13). This is the assertion that would have said so.
        /// </remarks>
        private static void NoGarble(VisualElement root)
        {
            string[] names = { "tick-zero", "tick-peak", "tick-record-end", "tick-end" };
            var boxes = new List<KeyValuePair<string, Rect>>();

            foreach (string name in names)
            {
                Label tick = root.Q<Label>(name);
                if (tick == null || !Shown(root, name)) continue;
                if (tick.resolvedStyle.width <= 1f) continue;

                boxes.Add(new KeyValuePair<string, Rect>(name, tick.worldBound));
            }

            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    Rect a = boxes[i].Value;
                    Rect b = boxes[j].Value;

                    True("bar: " + boxes[i].Key + " and " + boxes[j].Key + " do not overlap",
                        !a.Overlaps(b),
                        "x " + a.xMin.ToString("0", CultureInfo.InvariantCulture) + ".." +
                        a.xMax.ToString("0", CultureInfo.InvariantCulture) + " against " +
                        b.xMin.ToString("0", CultureInfo.InvariantCulture) + ".." +
                        b.xMax.ToString("0", CultureInfo.InvariantCulture));
                }
            }
        }

        private static double AxisEnd(TheatreReplay replay) =>
            Math.Max(replay.Record.RequestedSeconds ?? 0d, replay.RecordedThroughSeconds);

        private static void Paused(VisualElement root)
        {
            Is("paused: the pace reads PAUSED", Text(root, "pace"), "PAUSED");

            True("paused: the pace takes its paused class",
                root.Q<Label>("pace").ClassListContains("pace--paused"), "no class");

            Is("paused: the transport word", Text(root, "transport-word"), "PAUSED");

            True("paused: the pause glyph is up", Shown(root, "glyph-pause"), "hidden");
            True("paused: the play glyph is down", !Shown(root, "glyph-play"), "shown");
        }

        private static void Popover(VisualElement root)
        {
            True("P: the popover is up", Shown(root, "popover"), "hidden");
            True("P: the warnings are down", !Shown(root, "warnings"), "shown");

            VisualElement rows = root.Q<VisualElement>("popover-rows");
            True("P: the popover lists its evidence", rows.childCount >= 6, rows.childCount + " rows");

            Is("P: the popover's word is the strip's",
                Text(root, "popover-word"), Text(root, "status-word"));

            True("P: the popover has its prose", Text(root, "popover-prose").Length > 40, "");
        }

        private static void Warnings(VisualElement root, TheatreReplay replay)
        {
            RunRecord record = replay.Record;

            int expected =
                (record.ConfigHashMismatch != null ? 1 : 0) +
                (record.StepDisagreement != null ? 1 : 0) +
                (record.SamplesNote != null ? 1 : 0) +
                (replay.ElapsedSeconds > replay.RecordedThroughSeconds + 1e-6 ? 1 : 0);

            VisualElement warnings = root.Q<VisualElement>("warnings");

            Is("warnings: one panel row per warning the record carries",
                warnings.childCount.ToString(), expected.ToString());

            Is("warnings: the panel is up only when there is one",
                Shown(root, "warnings").ToString(), (expected > 0).ToString());

            if (expected == 0) Skip("warnings: this run carries none, so the texts are unchecked");
        }

        private static void Seeking(VisualElement root, TheatreRunner runner)
        {
            if (!runner.Seeking)
            {
                Skip("K: the seek finished inside one frame, so its state could not be read");
                return;
            }

            True("K: the seek plate is up", Shown(root, "seek-plate"), "hidden");
            True("K: the seek bar is up", Shown(root, "seekbar"), "hidden");

            string plate = Text(root, "seek-plate-text");
            True("K: the plate names the target",
                plate.Contains(TheatreUiFormat.RightArrow) &&
                plate.Contains(TheatreUiFormat.Clock(_seekTarget)), plate);

            string note = Text(root, "seek-meta");
            True("K: the note carries an estimate",
                note.Contains("to go") || note.Contains("measuring") || note.Contains("arriving"),
                note);

            Is("K: the transport word", Text(root, "transport-word"), "SEEKING");
        }

        private static void Arrived(VisualElement root)
        {
            True("K: the seek plate is down once it arrives", !Shown(root, "seek-plate"), "shown");
            True("K: the seek bar is down once it arrives", !Shown(root, "seekbar"), "shown");
        }

        /// <summary>Selects the first living creature, through the same call a click makes.</summary>
        private static void SelectSomethingAlive(TheatreRunner runner, TheatreReplay replay)
        {
            _selected = -1L;

            IReadOnlyList<Organism> living = replay.Eco.World.Living;
            if (living.Count == 0) return;

            if (runner.SelectById(living[0].Id)) _selected = living[0].Id;
        }

        private static void Selection(VisualElement root, TheatreReplay replay)
        {
            if (_selected < 0)
            {
                Skip("selection: nothing alive could be selected (an empty world, or an " +
                     "unreliable id map)");
                return;
            }

            Organism creature = CreatureIdMap.Find(replay.Eco.World, _selected);

            if (creature == null)
            {
                Skip("selection: the creature died between the click and the reading");
                return;
            }

            True("selection: the living panel is up", Shown(root, "inspector-live"), "hidden");

            Is("selection: the id", Text(root, "inspector-id"),
                "creature " + TheatreUiFormat.Identifier(creature.Id));

            Is("selection: the generation", Text(root, "value-generation"),
                TheatreUiFormat.Identifier(creature.GenerationDepth));

            Is("selection: the parent", Text(root, "value-parent"),
                creature.ParentId >= 0
                    ? TheatreUiFormat.Identifier(creature.ParentId)
                    : "founder");

            True("selection: the guild chips are drawn",
                root.Q<VisualElement>("guilds").childCount == 3,
                root.Q<VisualElement>("guilds").childCount + " chips");

            // The live readings are this world's own and stand on a cousin. The chain is read out
            // of the recording's lineage.jsonl, and on a cousin that file is about other
            // creatures, so it must not be shown as this one's.
            if (!_runner.Ui.IdsNameTheRecording)
            {
                Is("selection on a cousin: no ancestry chain",
                    Text(root, "ancestry-chain"), TheatreUiFormat.EmDash);

                True("selection on a cousin: it says why",
                    Text(root, "ancestry-note").Contains("cousin"), Text(root, "ancestry-note"));

                True("selection on a cousin: the live readings stand",
                    Text(root, "value-reserve").Length > 0 && Text(root, "value-speed").Length > 0,
                    "a live reading is blank");
            }
            else
            {
                True("selection: the ancestry chain names the creature",
                    Text(root, "ancestry-chain").Contains(
                        TheatreUiFormat.Identifier(creature.Id)),
                    Text(root, "ancestry-chain"));
            }
        }

        /// <summary>
        /// Finds a creature that has died, from this file's own scan of <c>lineage.jsonl</c>, and
        /// selects it.
        /// </summary>
        /// <remarks>
        /// The scan is deliberately not <see cref="LineageIndex"/>'s: an index agreeing with
        /// itself proves nothing, so the children, the birth and the death this check compares
        /// against are counted here, with the project's own JSON reader, from the same file.
        /// </remarks>
        private static void SelectSomethingDead(TheatreRunner runner, TheatreReplay replay)
        {
            _selected = -1L;
            _deadBorn = double.NaN;
            _deadDied = double.NaN;
            _deadChildren = 0;
            _deadGeneration = -1;

            string path = Path.Combine(replay.Record.Path, "lineage.jsonl");

            if (!File.Exists(path))
            {
                Skip("dead: this run wrote no lineage.jsonl");
                return;
            }

            var info = new FileInfo(path);

            if (info.Length > 64L * 1024L * 1024L)
            {
                Skip("dead: lineage.jsonl is " + info.Length / (1024 * 1024) +
                     " MB, too large for this check's own scan");
                return;
            }

            // The check's own count, read from the file rather than from LineageIndex: an index
            // agreeing with itself proves nothing.
            long chosen = -1L;
            double born = double.NaN, died = double.NaN;
            int children = 0;
            int generation = -1;

            var birthAt = new Dictionary<long, double>();
            var generationOf = new Dictionary<long, int>();
            var parentOf = new Dictionary<long, long>();
            var deaths = new List<KeyValuePair<long, double>>();

            foreach (string row in JsonlWriter.ReadRows(path))
            {
                if (string.IsNullOrWhiteSpace(row)) continue;

                JsonNode node;
                try { node = Json.Parse(row); } catch { continue; }

                string kind = node.Has("e") ? node["e"].AsString() : null;
                if (kind == null || !node.Has("id")) continue;

                long id = (long)node["id"].AsDouble();

                if (kind == "b")
                {
                    birthAt[id] = node["t"].AsDouble();
                    generationOf[id] = node.Has("g") ? node["g"].AsInt() : -1;
                    parentOf[id] = node.Has("p") ? (long)node["p"].AsDouble() : -1L;
                    continue;
                }

                if (kind == "d") deaths.Add(new KeyValuePair<long, double>(id, node["t"].AsDouble()));
            }

            for (int i = deaths.Count - 1; i >= 0 && chosen < 0; i--)
            {
                if (!birthAt.ContainsKey(deaths[i].Key)) continue;

                chosen = deaths[i].Key;
                died = deaths[i].Value;
                born = birthAt[chosen];
                generation = generationOf[chosen];
            }

            if (chosen < 0)
            {
                Skip("dead: no creature in this run has both a birth row and a death row yet");
                return;
            }

            foreach (KeyValuePair<long, long> pair in parentOf)
            {
                if (pair.Value == chosen) children++;
            }

            if (!runner.SelectById(chosen))
            {
                Skip("dead: the id map is unreliable, so a dead creature cannot be named");
                return;
            }

            _selected = chosen;
            _deadBorn = born;
            _deadDied = died;
            _deadChildren = children;
            _deadGeneration = generation;
        }

        private static void Dead(VisualElement root)
        {
            if (_selected < 0) return;   // SelectSomethingDead already said why, as a skip.

            // A cousin's ids name this world's bodies and nothing in the recording, so a dead id
            // has no life to show: the interface owes the viewer the unavailable state instead of
            // another creature's dates. The first Editor run showed the other thing — creature
            // 149's ancestry off the recording's lineage.jsonl, over a body that never had it.
            if (!_runner.Ui.IdsNameTheRecording)
            {
                // The id came out of the recording's lineage.jsonl, and in a cousin's world that
                // number may well belong to something still swimming. Nothing is wrong when it
                // does; there is simply no dead body to look at.
                if (CreatureIdMap.Find(_runner.Replay.Eco.World, _selected) != null)
                {
                    Skip("dead on a cousin: the id taken from the recording is alive in this " +
                         "world, so no dead selection could be made from it");
                    return;
                }

                True("dead on a cousin: the unavailable panel is up, not a dead one",
                    Shown(root, "inspector-unavailable"), "the dead panel");

                True("dead on a cousin: no dead panel",
                    !Shown(root, "inspector-dead"), "the dead panel is up too");

                True("dead on a cousin: it says the ids are not the recording's",
                    Text(root, "inspector-unavailable-prose").Contains("cousin"),
                    Text(root, "inspector-unavailable-prose"));

                return;
            }

            True("dead: the dead panel is up", Shown(root, "inspector-dead"), "hidden");

            Is("dead: the id", Text(root, "inspector-dead-id"),
                "creature " + TheatreUiFormat.Identifier(_selected));

            Is("dead: died at", Text(root, "inspector-dead-badge"),
                "DIED t=" + TheatreUiFormat.Seconds(_deadDied));

            Is("dead: lived", Text(root, "value-lived"),
                TheatreUiFormat.Seconds(_deadDied - _deadBorn));

            Is("dead: children", Text(root, "value-children"),
                TheatreUiFormat.Identifier(_deadChildren));

            if (_deadGeneration >= 0)
            {
                Is("dead: generation", Text(root, "value-dead-generation"),
                    TheatreUiFormat.Identifier(_deadGeneration));
            }

            True("dead: the id is struck through rather than decorated",
                root.Q<Label>("inspector-dead-id").parent.ClassListContains("struck"), "no .struck");
        }

        /// <summary>
        /// Selects an id no world can hold, so the unavailable state is reached on purpose.
        /// </summary>
        /// <remarks>
        /// The state is what the interface shows when it cannot name what a click points at, and
        /// the surest way to ask for it is to name something that is not there. On a faithful run
        /// this lands on the empty state instead, which is why the assertion that follows is
        /// skipped there.
        /// </remarks>
        private static void AskForAnIdThatIsNotHere(TheatreRunner runner)
        {
            if (runner.Ui == null || runner.Ui.IdsNameTheRecording) return;

            runner.SelectById(long.MaxValue);
        }

        private static void Unavailable(VisualElement root, TheatreRunner runner)
        {
            TheatreUi ui = runner.Ui;

            // Two ways in, and a cousin is one of them: its pairing is sound and its ids still
            // do not name the recording's creatures, which is the same thing to a viewer. Before
            // this the phase skipped on every cousin run, so the state was never once read.
            if (ui.IdsNameTheRecording)
            {
                Skip("unavailable: this run's ids name the recording's creatures, so the state " +
                     "cannot be produced here — it needs a cousin or a run whose pairing failed");
                return;
            }

            True("unavailable: the inspector says so", Shown(root, "inspector-unavailable"), "hidden");
            True("unavailable: it says why", Text(root, "inspector-unavailable-prose").Length > 40, "");
        }

        private static void Hidden(VisualElement root)
        {
            True("H: the interface is gone", root.ClassListContains("is-hidden"), "no class");

            True("H: nothing of it is laid out",
                root.resolvedStyle.display == DisplayStyle.None,
                root.resolvedStyle.display.ToString());
        }

        private static void Back(VisualElement root)
        {
            True("H: it comes back", !root.ClassListContains("is-hidden"), "still hidden");

            True("H: and is laid out again",
                root.resolvedStyle.display != DisplayStyle.None,
                root.resolvedStyle.display.ToString());
        }

        private static void Solo(VisualElement root)
        {
            True("solo: the solo census is up", Shown(root, "census-solo"), "hidden");
            True("solo: the world census is down", !Shown(root, "census-world"), "shown");
            True("solo: the inspector is down", !Shown(root, "inspector"), "shown");

            var words = new StringBuilder();
            root.Query<Label>().ForEach(label => words.Append(label.text).Append('\n'));

            string all = words.ToString();

            True("solo: the body's neuron count is shown", all.Contains("neurons"), "");

            // D081 retired Genome.GlobalBrain on 2026-09-07, and the line this interface replaced
            // still printed it. A stored genome's global neurons are never evaluated by this
            // build, so a count of them is a count of nothing.
            True("solo: no global-brain readout survives", !all.Contains("global neurons"),
                "the retired readout is back");

            True("solo: the sensors are listed",
                all.Contains("Chemical") || all.Contains("Depth") || all.Contains("Flow"), "");
        }

        // ------------------------------------------------------------------ the pictures

        /// <summary>
        /// Asks for a picture of the state just asserted, at both sizes, for the agent to read
        /// afterwards. It is taken over the ticks that follow, not here.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This used to call <c>ScreenCapture.CaptureScreenshot</c> and produce nothing.</b>
        /// Under <c>-batchmode</c> there is a graphics device and no presented backbuffer, so
        /// every capture was queued and none landed: a full pass logged a shot for each of its
        /// states and left <c>scratch/snaps/ui/</c> empty (2026-09-13). The route now is
        /// <see cref="TheatreUiCapture"/> — a RenderTexture the world and the panel are both
        /// pointed at, read back with <c>ReadPixels</c>, which is what
        /// <see cref="SnapshotCamera"/> has always done for the four framed views.
        /// </para>
        /// <para>
        /// <b>Two ticks per picture</b>, because the panel draws on its own next repaint, so the
        /// request is queued here and <see cref="Photographing"/> services it while the phase
        /// machine waits.
        /// </para>
        /// </remarks>
        private static void Shot(string state)
        {
            try
            {
                string arm = _runner.Replay != null
                    ? _runner.Replay.Record.ArmName ?? "run"
                    : "solo";

                string directory = Path.Combine(
                    Path.Combine(Path.Combine(BuildIdentity.RepositoryRoot(), "scratch"), "snaps"),
                    "ui");

                directory = Path.Combine(directory, arm);
                Directory.CreateDirectory(directory);

                for (int i = 0; i < Sizes.GetLength(0); i++)
                {
                    int w = Sizes[i, 0];
                    int h = Sizes[i, 1];

                    _queue.Add(new Wanted
                    {
                        State = state,
                        Width = w,
                        Height = h,
                        Path = Path.Combine(
                            directory, arm + "-" + state + "-" + w + "x" + h + ".png"),
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Theatre] could not ask for a picture of " + state + ": " + e.Message);
            }
        }

        /// <summary>
        /// Takes one queued picture over two ticks: arm, let a frame draw the panel, read back.
        /// </summary>
        /// <returns>True while a picture is being taken, so the phase machine holds still.</returns>
        private static bool Photographing()
        {
            if (TheatreUiCapture.Armed)
            {
                // Two frames, not one. Pointing the panel at a texture resizes it, the density
                // classes are set from the geometry event that resize raises, and the layout
                // those classes ask for lands on the frame after that. One frame would photograph
                // the interface mid-decision and read it the same way.
                if (++_armedTicks < 2) return true;
                _armedTicks = 0;

                Wanted taken = _queue.Count > 0 ? _queue[0] : default;
                if (_queue.Count > 0) _queue.RemoveAt(0);

                // The panel is laid out at the texture's width right now, which is the one moment
                // the density step can be read at a width no Game View here ever has.
                Density(taken.Width);

                int bytes = TheatreUiCapture.Shoot(taken.Path, out string wrote);

                if (bytes > 0)
                {
                    _shots.Add(taken.Path);
                    Debug.Log("[Theatre] ui shot: " + taken.State + " " + wrote + " -> " + taken.Path);
                }
                else
                {
                    Debug.LogWarning(
                        "[Theatre] the picture of " + taken.State + " was not written: " + wrote);
                }

                // The panel has just been handed back to the screen, and it was laid out at the
                // texture's width until a moment ago. Let the density classes settle before the
                // next phase reads anything off it.
                if (_queue.Count == 0) _settle = SettleFrames;

                return true;
            }

            if (_queue.Count == 0) return false;

            Wanted next = _queue[0];

            if (!TheatreUiCapture.Arm(
                    next.Width, next.Height, _runner.ViewCamera, _runner.Ui?.Panel, out string why))
            {
                Debug.LogWarning(
                    "[Theatre] could not photograph " + next.State + " at " + next.Width + "x" +
                    next.Height + ": " + why);

                _queue.RemoveAt(0);
            }

            return true;
        }

        /// <summary>
        /// The design's density step, read at the width the armed capture gave the panel.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Pointing the panel at a texture lays it out at that texture's pixels, so this is the
        /// only place in the project where the interface is ever 3840 wide. The step is a class
        /// on the root (<c>.is-wide</c> at 2240, <c>.is-wider</c> at 3400) because UI Toolkit
        /// cannot write a custom property from C#, and the three tokens it moves are the strip's
        /// height, the panel width and the edge padding — never the type, which the design fixes
        /// in pixels at every resolution.
        /// </para>
        /// <para>
        /// Asserted once per arm rather than on all fourteen states: the classes do not depend on
        /// what is on screen, and fourteen copies of one answer is noise in a log an agent reads.
        /// </para>
        /// </remarks>
        private static void Density(int width)
        {
            if (_runner.Ui == null || !_densityRead.Add(width)) return;

            VisualElement root = _runner.Ui.Root;
            VisualElement strip = root.Q<VisualElement>("strip");

            bool wide = width >= TheatreUi.WideAtPixels;
            bool wider = width >= TheatreUi.WiderAtPixels;

            True("density at " + width + ": the panel is that wide",
                Mathf.Abs(root.resolvedStyle.width - width) <= 1f,
                root.resolvedStyle.width.ToString("0.#", CultureInfo.InvariantCulture));

            True("density at " + width + ": is-wide " + (wide ? "on" : "off"),
                root.ClassListContains("is-wide") == wide, "the other way");

            True("density at " + width + ": is-wider " + (wider ? "on" : "off"),
                root.ClassListContains("is-wider") == wider, "the other way");

            float wanted = wider ? 66f : wide ? 44f : 40f;

            Near("density at " + width + ": the strip is " + wanted + " px",
                strip.resolvedStyle.height, wanted, 1f);
        }

        /// <summary>
        /// Whether a run was named. Mode A is what <c>EVOSIM_THEATRE_GENOME</c> asks for, and a
        /// null replay is only ever Mode A when no run was named at all.
        /// </summary>
        private static bool WorldWasAsked() =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN"));

        // ------------------------------------------------------------------ saying so

        private static void Pass(string what)
        {
            _passed++;
            Debug.Log("[Theatre] ok   " + what);
        }

        private static void Fail(string what, string saw)
        {
            _failed++;
            Debug.LogError("[Theatre] FAIL " + what + " — saw: " + saw);
        }

        private static void Skip(string what)
        {
            _skipped++;
            Debug.LogWarning("[Theatre] skip " + what);
        }

        private static void Is(string what, string saw, string wanted)
        {
            if (string.Equals(saw, wanted, StringComparison.Ordinal)) Pass(what);
            else Fail(what, "'" + saw + "' wanted '" + wanted + "'");
        }

        private static void True(string what, bool held, string saw)
        {
            if (held) Pass(what);
            else Fail(what, saw);
        }

        private static void Near(string what, double saw, double wanted, double tolerance)
        {
            if (Math.Abs(saw - wanted) <= tolerance) Pass(what);
            else Fail(what, saw.ToString("R") + " wanted " + wanted.ToString("R"));
        }

        /// <summary>
        /// A quantity: the digits must be the number, and the thin space must be there from a
        /// thousand up and absent below it.
        /// </summary>
        /// <remarks>
        /// Checked against an independently formatted string rather than against the formatter's
        /// own output, because a test that asks a formatter to agree with itself passes whatever
        /// the formatter does. U+2009 is invisible, so this is the only thing standing between the
        /// design's separator and an ordinary space nobody would notice.
        /// </remarks>
        private static void Grouped(string what, string saw, long value)
        {
            string bare = saw.Replace(TheatreUiFormat.ThinSpace, "");

            if (bare != value.ToString(CultureInfo.InvariantCulture))
            {
                Fail(what, "'" + saw + "' wanted " + value);
                return;
            }

            bool grouped = saw.Contains(TheatreUiFormat.ThinSpace);
            bool wanted = Math.Abs(value) >= 1000L;

            if (grouped != wanted)
            {
                Fail(what + " (thin space at a thousand)",
                    "'" + saw + "' " + (grouped ? "is grouped" : "is not grouped"));

                return;
            }

            Pass(what);
        }

        private static string Text(VisualElement root, string name)
        {
            var label = root.Q<Label>(name);
            return label == null ? "" : label.text ?? "";
        }

        private static bool Shown(VisualElement root, string name)
        {
            VisualElement element = root.Q<VisualElement>(name);
            return element != null && !element.ClassListContains("is-gone");
        }

        private static double Number(string text)
        {
            var digits = new StringBuilder();

            foreach (char c in text)
            {
                if (char.IsDigit(c) || c == '.' || c == '-') digits.Append(c);
                else if (c == TheatreUiFormat.Minus[0]) digits.Append('-');
            }

            return double.TryParse(digits.ToString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double value)
                ? value
                : double.NaN;
        }

        private static int IntFrom(string name, int fallback, int least, int most)
        {
            string text = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrEmpty(text) ||
                !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return fallback;
            }

            return Mathf.Clamp(value, least, most);
        }

        private static void Finish(int code, string verdict)
        {
            EditorApplication.update -= Drive;
            _driving = false;
            SessionState.EraseString(PendingKey);

            // An armed capture holds the panel's target texture, which would leave the interface
            // drawing into a texture nobody reads for the rest of the session.
            TheatreUiCapture.Disarm();
            _queue.Clear();
            _armedTicks = 0;

            var files = new StringBuilder();
            foreach (string path in _shots) files.Append("\n  ").Append(path);

            Debug.Log(
                "[Theatre] interface check: " + verdict + "\n" +
                "  " + _passed + " passed, " + _failed + " failed, " + _skipped + " skipped" +
                (_shots.Count > 0 ? "\n  pictures:" + files : "\n  no pictures"));

            if (code != 0 || _failed > 0)
            {
                Debug.LogError("[Theatre] interface check FAILED: " + verdict);
                code = 1;
            }

            _runner = null;

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Quit(code);
                return;
            }

            SessionState.SetString(ExitKey, code.ToString(CultureInfo.InvariantCulture));
            EditorApplication.playModeStateChanged += QuitOnLeavingPlayMode;
            EditorApplication.isPlaying = false;
        }

        private static void QuitOnLeavingPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;

            EditorApplication.playModeStateChanged -= QuitOnLeavingPlayMode;

            string exit = SessionState.GetString(ExitKey, "");
            if (string.IsNullOrEmpty(exit)) return;

            SessionState.EraseString(ExitKey);
            Quit(int.TryParse(exit, out int code) ? code : 1);
        }

        private static void Quit(int code)
        {
            if (!Application.isBatchMode)
            {
                Debug.Log("[Theatre] interface check finished with code " + code + ".");
                return;
            }

            EditorApplication.Exit(code);
        }
    }
}
