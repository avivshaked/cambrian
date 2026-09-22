using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// The interface in live mode, checked against the world it claims to be showing — the same
    /// Play-mode walk <see cref="TheatreUiCheck"/> makes, pointed at a farm checkpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An entry and not a second harness.</b> Everything below the strip asks the same
    /// questions of a live world as of a replay, and the phase machine, the capture plumbing, the
    /// density readings and the exit code all live in <see cref="TheatreUiCheck"/>; its walk
    /// branches on <c>TheatreRunner.Live</c>. What this file is for is the launch: a live world
    /// needs a checkpoint and a second to restore from, and a caller who has to set two
    /// environment variables by hand to run a check is a caller who will set one of them wrong.
    /// So this fills in the fixture's defaults and hands over.
    /// </para>
    /// <para>
    /// <b>The defaults are the fixture the checkpoint work was built on</b>
    /// (<c>scratch/checkpoint/runs/ckA</c>, 400 s — <see cref="LiveCheckpointCheck"/>'s own), and
    /// anything already set in the environment wins over them, so a person pointing this at
    /// another run does not have to know that a default existed.
    /// </para>
    /// <para>
    /// <b>A fixture goes stale the way a config does.</b> A checkpoint carries a layout version
    /// and the reader refuses an older one outright, so a Core change that moves the layout
    /// leaves every recorded fixture unreadable until it is recorded again — which is what
    /// happened to ckA on 2026-09-22, hours after it was made. The walk says so and stops:
    /// before that guard went in, a refused live world left both worlds null and
    /// <see cref="TheatreUiCheck"/> read that as Mode A and reported "solo mode checked" over a
    /// world that never opened. Re-record with <c>run-farm.ps1 -CheckpointEvery</c>.
    /// </para>
    /// <para>
    /// <b>The variables are set on the process and survive the domain reload</b> that entering
    /// Play mode causes, which is why they are set here rather than carried in
    /// <c>SessionState</c> beside the walk's own resume: the scene's <c>TheatreRunner.Start</c>
    /// reads them in Play mode, in this process, exactly as it reads a launcher's.
    /// </para>
    /// <code>
    /// # NO -quit and NO -nographics: it enters Play mode and photographs what it asserts.
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', "$PWD/unity-w6", '-batchmode',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.LiveUiCheck.Run',
    ///   '-logFile', "$PWD/scratch/live-ui/live-ui-check.log")
    /// </code>
    /// </remarks>
    public static class LiveUiCheck
    {
        /// <summary>The fixture, relative to the repository root, when nothing names another.</summary>
        private const string Fixture = "scratch/checkpoint/runs/ckA";

        /// <summary>The second it is restored at, which is one the fixture holds a checkpoint for.</summary>
        private const string RestoreAtSeconds = "400";

        [MenuItem("Evosim/Theatre/Check the interface, live (Play mode)")]
        public static void FromMenu() => Run();

        /// <summary>
        /// Batchmode entry point. <b>Launch it without <c>-quit</c> and without
        /// <c>-nographics</c>.</b> It restores a farm checkpoint, carries the world on live,
        /// walks the interface's live states, prints one line per assertion and exits 0 or 1.
        /// </summary>
        public static void Run()
        {
            string checkpoint = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_CHECKPOINT");

            if (string.IsNullOrWhiteSpace(checkpoint))
            {
                checkpoint = Path.Combine(BuildIdentity.RepositoryRoot(), Fixture);
                Environment.SetEnvironmentVariable("EVOSIM_THEATRE_CHECKPOINT", checkpoint);
            }

            // With a checkpoint named, EVOSIM_THEATRE_SEEK is the second to continue from rather
            // than one to seek to — one variable, two meanings, decided by the runner. Setting it
            // here keeps the fixture landing on a checkpoint the run actually wrote.
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SEEK")))
            {
                Environment.SetEnvironmentVariable("EVOSIM_THEATRE_SEEK", RestoreAtSeconds);
            }

            // A run directory would send the runner down the replay path instead, and a genome
            // would send it to Mode A. Neither is what was asked for here.
            Environment.SetEnvironmentVariable("EVOSIM_THEATRE_RUN", null);
            Environment.SetEnvironmentVariable("EVOSIM_THEATRE_GENOME", null);

            if (!Directory.Exists(checkpoint) && !File.Exists(checkpoint))
            {
                Debug.LogError(
                    "[Theatre] the live interface check has nothing to open: '" + checkpoint +
                    "' is neither a directory nor a file. Point EVOSIM_THEATRE_CHECKPOINT at a " +
                    "run directory holding a checkpoints/ directory, the arm directory above it, " +
                    "or one .ckpt file.");

                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[Theatre] live interface check: continuing {0} from t={1} s.",
                checkpoint, Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SEEK")));

            TheatreUiCheck.Run();
        }
    }
}
