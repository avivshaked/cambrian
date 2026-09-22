using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;
using Solver = Evosim.Dynamics.Creature;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// The reading behind the live world: step a farm-recorded world on <c>Evosim.Dynamics</c>
    /// inside the Editor with <see cref="LiveWorldView"/> drawing it, and ask at every sample
    /// whether what is in the scene is what is in the world.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Four questions, and none of them is about the trajectory.</b> Whether the world is the
    /// recorded one is <c>DynamicsReplayCheck</c>'s question and it is already answered; this
    /// entry asks only whether the view is honest about whatever world it is given. So: one
    /// object per part of every living body that has a solver body, at every sample; every part's
    /// transform finite and inside the water; an object where a birth happened and none where a
    /// death did; and no body left standing that the world has buried.
    /// </para>
    /// <para>
    /// <b>Edit mode, and no graphics device.</b> Nothing here is Unity's: the solver is managed
    /// C#, the bodies are transforms, and what is checked is their numbers. A camera would add a
    /// picture and no evidence, and the machine this runs on cannot spare a graphics device
    /// today. The skin is therefore not applied — the palette runs without one, which is the
    /// plain look the headless checks have always got — so what this entry does not cover is the
    /// mesh swap, the carve and the necks. Those are the interactive path's, and the interactive
    /// path is where they are seen.
    /// </para>
    /// <code>
    /// $env:EVOSIM_THEATRE_RUN = "$PWD/scratch/live-render/runs/livesmoke"
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', "$PWD/unity-w5", '-batchmode', '-quit', '-nographics',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.LiveWorldCheck.Run',
    ///   '-logFile', "$PWD/scratch/logs/live-world.log")
    /// </code>
    /// </remarks>
    public static class LiveWorldCheck
    {
        private const string Tag = "[LiveWorldCheck] ";

        /// <summary>Metres of slack allowed outside the water before a part is called out.</summary>
        /// <remarks>
        /// A part is not a root, and the world's own guards are on the root: the radius guard
        /// kills a body whose root passes R + 1 m, so a limb of a body standing legally at the
        /// glass reaches further than that by its own half-extent. Two metres is more than any
        /// body in the record is long, and the check is for a view that has lost a body entirely
        /// rather than for the physics' own bounds.
        /// </remarks>
        private const float SlackMetres = 2f;

        [MenuItem("Evosim/Theatre/Live World Check")]
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
            string run = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN");

            if (string.IsNullOrWhiteSpace(run))
            {
                Debug.LogError(
                    Tag + "EVOSIM_THEATRE_RUN names no run. Point it at a run directory the " +
                    "console farm recorded, or at the arm directory above it.");

                return false;
            }

            double seconds = Number("EVOSIM_LIVE_SECONDS", 20d);
            double wallMinutes = Number("EVOSIM_THEATRE_WALL_MINUTES", 20d);

            // A window later than founding, run to with the view switched off. Deaths are what a
            // founding world does not have, and "objects vanish for deaths" is one of the four
            // things this entry exists to answer; twenty seconds from t=0 cannot answer it.
            double from = Number("EVOSIM_LIVE_FROM", 0d);

            // As the runner opens it: live, so a source mismatch is a caveat and not a refusal.
            TheatreDynamicsReplay replay =
                TheatreDynamicsReplay.Open(run, true, out string refusal);

            if (replay == null)
            {
                Debug.LogError(Tag + "refused: " + refusal);
                return false;
            }

            using (replay)
            {
                RunRecord record = replay.Record;
                RunConfig config = record.Config;

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    Tag + "{0} seed {1}, engine {2}, dt {3} s, threads {4}, config {5} — {6}",
                    record.ArmName ?? "run", record.Seed, record.Engine,
                    replay.Sim.PhysicsDt, replay.Threads, record.ConfigHash,
                    replay.Faithful
                        ? "same source as the recording"
                        : "SOURCE DIFFERS: " + replay.SourceDifference));

                float radius = config.SharedSpace && config.WorldShape == WorldShape.Tank
                    ? TankGeometry.RadiusFor(config.WorldAreaSquareMetres)
                    : 0f;

                var view = new LiveWorldView(replay.Sim)
                {
                    // Without a skin, which is the plain look every headless check gets. What it
                    // exercises is the gather, the property block and the aspect squash; the mesh
                    // swap and the necks need TheatreSkin and a scene, and are not asked for here.
                    Palette = new TheatrePalette(),
                };

                using (view)
                {
                    if (from > 0d && !Reach(replay, from, wallMinutes)) return false;

                    return Watch(replay, view, config, radius, seconds, wallMinutes);
                }
            }
        }

        /// <summary>
        /// Runs the world forward to a second with nothing drawn — the runner's seek, without a
        /// camera to switch off.
        /// </summary>
        private static bool Reach(TheatreDynamicsReplay replay, double second, double wallMinutes)
        {
            var clock = Stopwatch.StartNew();

            // A world starts empty and the population floor trickles its founders in over the
            // first steps, so "nobody alive" only means extinction once somebody has been alive.
            // Without this the first step of every run reads as a world that has died.
            bool populated = replay.Sim.World.Living.Count > 0;

            while (replay.ElapsedSeconds < second - 1e-9)
            {
                if (clock.Elapsed.TotalMinutes >= wallMinutes)
                {
                    Debug.LogError(string.Format(
                        CultureInfo.InvariantCulture,
                        Tag + "the wall was reached at t={0:0.#} s on the way to {1:0.#} s.",
                        replay.ElapsedSeconds, second));

                    return false;
                }

                replay.Step();

                if (replay.Sim.World.Living.Count > 0) populated = true;
                else if (populated)
                {
                    Debug.LogError(string.Format(
                        CultureInfo.InvariantCulture,
                        Tag + "the world emptied at t={0:0.#} s, before the window.",
                        replay.ElapsedSeconds));

                    return false;
                }
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                Tag + "ran to t={0:0.#} s with nothing drawn, in {1:0.#} s of wall — {2} alive.",
                replay.ElapsedSeconds, clock.Elapsed.TotalSeconds, replay.Sim.World.Living.Count));

            return true;
        }

        private static bool Watch(
            TheatreDynamicsReplay replay, LiveWorldView view, RunConfig config,
            float radius, double seconds, double wallMinutes)
        {
            World world = replay.Sim.World;

            double from = replay.ElapsedSeconds;
            double until = from + seconds;

            long birthsAt = world.Births;
            long deathsAt = world.Deaths;

            var previous = new HashSet<long>();
            var present = new HashSet<long>();
            var parts = new List<Transform>();

            var clock = Stopwatch.StartNew();
            long steps = 0;
            int samples = 0;
            int faults = 0;
            bool timedOut = false;
            bool extinct = false;

            // As in Reach: an empty world is only extinct once it has been a world at all.
            bool populated = world.Living.Count > 0;

            // The first sweep before anything is stepped, so the founders are checked too: a view
            // that only ever drew what it saw born would pass every assertion below and show an
            // empty box on a world that opened full.
            view.Sync();

            while (replay.ElapsedSeconds < until - 1e-9)
            {
                if (clock.Elapsed.TotalMinutes >= wallMinutes) { timedOut = true; break; }

                bool sample = replay.Step();
                steps++;

                if (!sample) continue;

                view.Sync();
                samples++;

                if (!Agrees(replay, view, config, radius, present, previous, parts, samples))
                {
                    faults++;
                    if (faults >= 5) break;
                }

                previous.Clear();
                foreach (long id in present) previous.Add(id);

                if (world.Living.Count > 0) populated = true;
                else if (populated) { extinct = true; break; }
            }

            double wall = System.Math.Max(1e-6, clock.Elapsed.TotalSeconds);

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                Tag + "stepped t={0:0.#} to {1:0.#} s in {2:0.#} s of wall, {3} physics steps, " +
                "{4} samples — {5:0} steps per wall second, {6:0.##}x real time{7}{8}",
                from, replay.ElapsedSeconds, wall, steps, samples, steps / wall,
                (replay.ElapsedSeconds - from) / wall,
                timedOut ? ", WALL REACHED" : "", extinct ? ", world empty" : ""));

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                Tag + "the view: {0} built, {1} destroyed, {2} rebuilt, {3} resized by growth, " +
                "{4} pose(s) refused for a link that was not finite",
                view.Built, view.Removed, view.Rebuilt, view.Resized, view.SkippedNonFinite));

            long births = world.Births - birthsAt;
            long deaths = world.Deaths - deathsAt;

            if (faults > 0 || timedOut)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    Tag + "FAILED: {0} sample(s) disagreed{1}", faults,
                    timedOut ? " and the wall was reached before the window ended" : ""));

                return false;
            }

            if (samples == 0)
            {
                Debug.LogError(Tag + "FAILED: the world ran no metabolic step in the window.");
                return false;
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                Tag + "ok, {0} bodies, {1} parts, {2} births, {3} deaths",
                view.BodyCount, view.PartCount, births, deaths));

            return true;
        }

        /// <summary>
        /// One sample's four questions. False on the first thing that is not true, which is named.
        /// </summary>
        private static bool Agrees(
            TheatreDynamicsReplay replay, LiveWorldView view, RunConfig config, float radius,
            HashSet<long> present, HashSet<long> previous, List<Transform> parts, int sample)
        {
            World world = replay.Sim.World;
            IReadOnlyList<Organism> living = world.Living;

            present.Clear();
            int bodies = 0;
            int expected = 0;

            for (int i = 0; i < living.Count; i++)
            {
                long id = living[i].Id;

                // A creature conceived this step has no solver body yet, and the view deliberately
                // does not draw one for it. It is not counted on either side.
                if (!replay.Sim.TryPose(id, out Solver body) || body == null) continue;

                present.Add(id);
                bodies++;
                expected += body.Phenotype.PartCount;

                if (!view.Holds(id))
                {
                    return No(sample, replay,
                        "creature " + id + " is alive and has a solver body, and nothing is " +
                        "drawn for it");
                }
            }

            foreach (long id in previous)
            {
                if (present.Contains(id) || !view.Holds(id)) continue;

                return No(sample, replay,
                    "creature " + id + " is no longer in the world and its body is still drawn");
            }

            if (view.BodyCount != bodies)
            {
                return No(sample, replay,
                    "the world holds " + bodies + " bodies and the view draws " + view.BodyCount);
            }

            if (view.PartCount != expected)
            {
                return No(sample, replay,
                    "the world's bodies have " + expected + " parts and the view draws " +
                    view.PartCount);
            }

            parts.Clear();
            view.CollectParts(parts);

            if (parts.Count != view.PartCount)
            {
                return No(sample, replay,
                    "the view counts " + view.PartCount + " parts and holds " + parts.Count +
                    " transforms");
            }

            for (int i = 0; i < parts.Count; i++)
            {
                Transform part = parts[i];
                if (part == null) return No(sample, replay, "a part transform has been destroyed");

                Vector3 at = part.position;

                if (!Finite(at.x) || !Finite(at.y) || !Finite(at.z))
                {
                    return No(sample, replay, part.name + " of " + Owner(part) + " is at " + at);
                }

                if (!Inside(at, config, radius, out string where))
                {
                    return No(sample, replay, part.name + " of " + Owner(part) + " is " + where);
                }
            }

            return true;
        }

        /// <summary>Whether a part is in the water, with a limb's worth of slack.</summary>
        private static bool Inside(Vector3 at, RunConfig config, float radius, out string where)
        {
            where = null;

            float depth = config.WorldDepthMetres;

            if (!World.HeightIsInTheWorld(at.y, depth + SlackMetres))
            {
                where =
                    "at y=" + at.y.ToString("0.##", CultureInfo.InvariantCulture) +
                    " in a world " + depth.ToString("0.#", CultureInfo.InvariantCulture) +
                    " m deep";

                return false;
            }

            if (radius <= 0f) return true;

            double squared = TankGeometry.SquaredRadiusAt(at.x, at.z, radius);
            double allowed = radius + SlackMetres;

            if (squared > allowed * allowed)
            {
                where =
                    System.Math.Sqrt(squared).ToString("0.##", CultureInfo.InvariantCulture) +
                    " m from the tank's axis, which is " +
                    radius.ToString("0.##", CultureInfo.InvariantCulture) + " m across";

                return false;
            }

            return true;
        }

        private static string Owner(Transform part) =>
            part.parent != null ? part.parent.name : "an unparented body";

        private static bool No(int sample, TheatreDynamicsReplay replay, string what)
        {
            Debug.LogError(string.Format(
                CultureInfo.InvariantCulture,
                Tag + "sample {0} at t={1:0.#} s: {2}", sample, replay.ElapsedSeconds, what));

            return false;
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

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
