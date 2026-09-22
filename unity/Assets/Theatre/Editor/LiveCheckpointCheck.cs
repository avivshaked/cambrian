using System;
using System.Diagnostics;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// The reading behind continuing a farm run in the theatre: restore a recorded second out of
    /// its checkpoint, carry the world on live in the Editor, and ask at every sample what the
    /// difference between this world and the recorded one has grown to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two questions, and they want opposite answers.</b> At the instant of the restore nothing
    /// has been stepped: the counters in the world are the ones the checkpoint carried, and the
    /// checkpoint was written immediately after the report row at that second, so
    /// <c>alive</c>, <c>births</c> and <c>deaths</c> must equal that row exactly. Anything else
    /// means the restore did not land where it says it did, and the check fails on it. After that
    /// the world is stepped by this Editor's Mono against a recording made by the farm's RyuJIT,
    /// which do not agree on a double sum (CLAUDE.md, 2026-09-22), so the two must be allowed to
    /// part. What the later samples are for is to say <i>how far</i> they have, which is a reading
    /// and not a verdict.
    /// </para>
    /// <para>
    /// <b>The first stepped sample is printed and not asserted, on purpose.</b> It is ten seconds
    /// of forty bodies after the restore and the three columns are integers, so it agreeing proves
    /// little and it parting proves less. The assertion that means something is the one taken
    /// before a step.
    /// </para>
    /// <para>
    /// <b>Edit mode, no graphics device</b>, as <see cref="LiveWorldCheck"/>: the solver is
    /// managed C#, the bodies are transforms, and what is checked is their numbers. The skin is
    /// therefore not applied and the palette runs bare.
    /// </para>
    /// <code>
    /// $env:EVOSIM_THEATRE_CHECKPOINT = "$PWD/scratch/checkpoint/runs/ckA"
    /// $env:EVOSIM_THEATRE_SEEK = "400"
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', "$PWD/unity-w6", '-batchmode', '-quit', '-nographics',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.LiveCheckpointCheck.Run',
    ///   '-logFile', "$PWD/scratch/ckpt-theatre/check.log")
    /// </code>
    /// </remarks>
    public static class LiveCheckpointCheck
    {
        private const string Tag = "[LiveCheckpointCheck] ";

        /// <summary>The fixture, relative to the repository root, when nothing names another.</summary>
        private const string Fixture = "scratch/checkpoint/runs/ckA";

        [MenuItem("Evosim/Theatre/Live Checkpoint Check")]
        public static void Run()
        {
            int code = 1;

            try
            {
                code = Check() ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "failed: " + e);
            }

            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static bool Check()
        {
            string what = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_CHECKPOINT");

            if (string.IsNullOrWhiteSpace(what))
            {
                what = System.IO.Path.Combine(BuildIdentity.RepositoryRoot(), Fixture);
            }

            double at = Number("EVOSIM_THEATRE_SEEK", 400d);
            double until = Number("EVOSIM_LIVE_UNTIL", 600d);
            double wallMinutes = Number("EVOSIM_THEATRE_WALL_MINUTES", 30d);

            TheatreDynamicsReplay live = TheatreDynamicsReplay.Continue(what, at, out string refusal);

            if (live == null)
            {
                Debug.LogError(Tag + "refused: " + refusal);
                return false;
            }

            using (live)
            {
                LiveCheckpoint checkpoint = live.ContinuedFrom;
                RunRecord record = live.Record;

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    Tag + "{0} seed {1}, engine {2}, dt {3} s, threads {4}, config {5}",
                    record.ArmName ?? "run", record.Seed, record.Engine,
                    live.Sim.PhysicsDt, live.Threads, record.ConfigHash));

                Debug.Log(Tag + checkpoint.Line());

                if (live.Faithful)
                {
                    return No("a continued world reports itself as a faithful replay");
                }

                // The label is the whole of the provenance a frame carries, so it is asserted
                // rather than trusted: a picture that does not say cousin is a picture that will
                // be read as the run.
                string label = live.LabelFor("side", "look");
                Debug.Log(Tag + "label: " + label);

                if (label.IndexOf("cousin", StringComparison.Ordinal) < 0)
                {
                    return No("the label does not carry the word cousin: " + label);
                }

                if (Math.Abs(live.ElapsedSeconds - checkpoint.Seconds) > 1e-6)
                {
                    return No(string.Format(
                        CultureInfo.InvariantCulture,
                        "the checkpoint stands at {0:0.###} s and the restored world at {1:0.###} s",
                        checkpoint.Seconds, live.ElapsedSeconds));
                }

                var view = new LiveWorldView(live.Sim) { Palette = new TheatrePalette() };

                using (view)
                {
                    view.Sync();

                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        Tag + "restored {0} bodies of {1} links; the view drew {2} bodies and " +
                        "{3} parts",
                        checkpoint.Bodies, checkpoint.Links, view.BodyCount, view.PartCount));

                    if (view.BodyCount != checkpoint.Bodies)
                    {
                        return No(
                            "the checkpoint restored " + checkpoint.Bodies +
                            " bodies and the view drew " + view.BodyCount);
                    }

                    if (view.PartCount != checkpoint.Links)
                    {
                        return No(
                            "the restored bodies have " + checkpoint.Links +
                            " links and the view drew " + view.PartCount + " parts");
                    }

                    if (!AgreesAtTheRestore(live)) return false;

                    return Carry(live, view, until, wallMinutes);
                }
            }
        }

        /// <summary>
        /// The one assertion nothing has stepped past: the restored counters against the row the
        /// run wrote at that second.
        /// </summary>
        private static bool AgreesAtTheRestore(TheatreDynamicsReplay live)
        {
            double t = live.ElapsedSeconds;

            if (!Row(live, t, out RunSample row))
            {
                return No(string.Format(
                    CultureInfo.InvariantCulture,
                    "the run wrote no stats row at t={0:0.#} s, so there is nothing to check the " +
                    "restore against", t));
            }

            WorldCensus census = live.Census;

            Debug.Log(Say("restore", t, row, census));

            if (row.Alive == census.Alive && row.Births == census.Births &&
                row.Deaths == census.Deaths)
            {
                return true;
            }

            return No(string.Format(
                CultureInfo.InvariantCulture,
                "the restored world disagrees with the row at t={0:0.#} s before a single step: " +
                "alive {1}/{2}, births {3}/{4}, deaths {5}/{6} (recorded/restored)",
                t, row.Alive, census.Alive, row.Births, census.Births, row.Deaths, census.Deaths));
        }

        /// <summary>
        /// Carries the world on, and prints the recording beside it at every sample the record
        /// has one for.
        /// </summary>
        private static bool Carry(
            TheatreDynamicsReplay live, LiveWorldView view, double until, double wallMinutes)
        {
            double from = live.ElapsedSeconds;

            var clock = Stopwatch.StartNew();
            long steps = 0;
            int samples = 0;
            int compared = 0;
            int agreed = 0;
            string firstDrift = null;
            bool timedOut = false;
            bool extinct = false;

            while (live.ElapsedSeconds < until - 1e-9)
            {
                if (clock.Elapsed.TotalMinutes >= wallMinutes) { timedOut = true; break; }

                bool metabolic = live.Step(out bool met, out _, out RunSample row);
                steps++;

                if (!metabolic) continue;

                samples++;
                view.Sync();

                if (met)
                {
                    compared++;
                    WorldCensus census = live.Census;

                    bool same = row.Alive == census.Alive && row.Births == census.Births &&
                                row.Deaths == census.Deaths;

                    if (same) agreed++;
                    else if (firstDrift == null)
                    {
                        firstDrift = string.Format(
                            CultureInfo.InvariantCulture, "t={0:0.#} s", census.T);
                    }

                    Debug.Log(Say(same ? "same" : "drift", census.T, row, census));
                }

                if (live.Sim.World.Living.Count == 0) { extinct = true; break; }
            }

            double wall = Math.Max(1e-6, clock.Elapsed.TotalSeconds);

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                Tag + "carried t={0:0.#} to {1:0.#} s in {2:0.#} s of wall, {3} physics steps, " +
                "{4} metabolic steps, {5} of them on a recorded sample — {6:0.##}x real time{7}{8}",
                from, live.ElapsedSeconds, wall, steps, samples, compared,
                (live.ElapsedSeconds - from) / wall,
                timedOut ? ", WALL REACHED" : "", extinct ? ", world empty" : ""));

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                Tag + "the view: {0} built, {1} destroyed, {2} rebuilt, {3} resized by growth, " +
                "{4} pose(s) refused for a link that was not finite",
                view.Built, view.Removed, view.Rebuilt, view.Resized, view.SkippedNonFinite));

            // The recording's own five-column check, for what it says about the doubles: the three
            // counts above are integers and can hold for a long time after the joules have parted.
            Debug.Log(Tag + live.IdentityLine() + " — by column: " + live.Identity.ByColumn());

            if (timedOut)
            {
                return No(string.Format(
                    CultureInfo.InvariantCulture,
                    "the wall was reached at t={0:0.#} s on the way to {1:0.#} s",
                    live.ElapsedSeconds, until));
            }

            if (compared == 0)
            {
                return No("the world reached no recorded sample after the restore");
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                Tag + "ok, continued from {0:0.#} s to {1:0.#} s, {2} bodies, {3} parts, " +
                "{4} of {5} samples agree on alive/births/deaths, first drift {6}{7}",
                from, live.ElapsedSeconds, view.BodyCount, view.PartCount, agreed, compared,
                firstDrift ?? "none",
                extinct ? ", and the world emptied before the window ended" : ""));

            return true;
        }

        /// <summary>The recorded row and the live world side by side, as one line.</summary>
        private static string Say(string what, double t, RunSample row, WorldCensus census) =>
            string.Format(
                CultureInfo.InvariantCulture,
                Tag + "{0,7}  t={1,6:0.#} s  alive {2,5}/{3,-5} births {4,6}/{5,-6} " +
                "deaths {6,6}/{7,-6} (recorded/live)",
                what, t, row.Alive, census.Alive, row.Births, census.Births,
                row.Deaths, census.Deaths);

        /// <summary>The recorded sample at a second, or false when the run wrote none there.</summary>
        private static bool Row(TheatreDynamicsReplay live, double t, out RunSample found)
        {
            found = default;

            System.Collections.Generic.IReadOnlyList<RunSample> samples = live.Record.Samples;

            for (int i = 0; i < samples.Count; i++)
            {
                if (Math.Abs(samples[i].T - t) > 1e-6) continue;

                found = samples[i];
                return true;
            }

            return false;
        }

        private static bool No(string why)
        {
            Debug.LogError(Tag + "FAIL: " + why);
            return false;
        }

        private static double Number(string name, double fallback)
        {
            string set = Environment.GetEnvironmentVariable(name);

            return !string.IsNullOrWhiteSpace(set) &&
                   double.TryParse(set, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) &&
                   v > 0d
                ? v
                : fallback;
        }
    }
}
