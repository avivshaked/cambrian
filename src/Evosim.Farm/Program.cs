using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// The farm, out of Unity: an arm launched from the environment, as
    /// <c>-executeMethod Evosim.Sim.EditorTools.EvolutionRun.Run</c> was.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this does today (work package I).</b> Reads the environment, builds the
    /// <see cref="RunConfig"/>, constructs the world so the header and the manifest can describe
    /// the one the run will actually have, creates the run directory with its <c>config.json</c>,
    /// writes the <c>running</c> manifest, prints the report header — and then stops, because the
    /// loop is work package G's file and is not here. Everything above the loop is finished and
    /// testable without it, which is the point of the split.
    /// </para>
    /// <para>
    /// <b>Parameterised by environment rather than by argument</b>, as the Unity entry was, so
    /// that <c>run-arm.ps1</c> and the round launchers keep working unchanged. Arguments are
    /// accepted as <c>EVOSIM_NAME=value</c> pairs that override the environment, which a console
    /// program can take and an <c>-executeMethod</c> could not; anything else is refused rather
    /// than ignored, for the reason an unparseable setting is.
    /// </para>
    /// </remarks>
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.GetType().Name + ": " + e.Message);
                return 1;
            }
        }

        private static int Run(string[] args)
        {
            var overrides = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string arg in args)
            {
                if (arg == "-h" || arg == "--help")
                {
                    Help();
                    return 0;
                }

                int eq = arg.IndexOf('=');
                if (eq <= 0 || !arg.StartsWith("EVOSIM_", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine(
                        "'" + arg + "' is not a setting. Arguments are EVOSIM_NAME=value pairs; " +
                        "everything else is refused rather than ignored.");
                    return 1;
                }

                overrides[arg.Substring(0, eq)] = arg.Substring(eq + 1);
            }

            EnvBinding.Lookup env = name =>
                overrides.TryGetValue(name, out string v) ? v : Environment.GetEnvironmentVariable(name);

            EnvSettings settings = EnvBinding.Read(env);

            // Ignored, and said aloud rather than only ignored: EvolutionRun never enumerated the
            // environment, so a typo in a launcher has always been silent, and silence is how a
            // world nobody asked for gets filed under a header that describes another one.
            var names = new List<string>(overrides.Keys);
            foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            {
                names.Add(e.Key as string);
            }

            IReadOnlyList<string> unknown = EnvBinding.UnknownNames(names);
            if (unknown.Count > 0)
            {
                Console.Error.WriteLine(
                    "warning: " + string.Join(", ", unknown) +
                    " is set and is not a setting this build reads — ignored, exactly as the " +
                    "Unity entry ignored it.");
            }

            if (settings.RequestedJobWorkers != 0)
            {
                Console.Error.WriteLine(
                    "warning: EVOSIM_PHYSICS_JOBS is " + settings.RequestedJobWorkers +
                    " and this engine has no job system — it changes nothing. The thread count " +
                    "is EVOSIM_THREADS, and it changes pace and not the trajectory.");
            }

            // The inoculum, loaded at startup rather than when the assay fires, so a malformed or
            // missing file fails the run immediately instead of thousands of simulated seconds in.
            string inoculumHash = null;
            string inoculumHashShort = null;

            if (!string.IsNullOrEmpty(settings.InoculatePath))
            {
                byte[] bytes = File.ReadAllBytes(settings.InoculatePath);
                GenomeJson.Read(Encoding.UTF8.GetString(bytes));
                inoculumHash = Manifest.HashBytes(bytes);
                inoculumHashShort = inoculumHash.Substring(0, 12);
            }
            else if (settings.InoculateAt > 0f)
            {
                Console.Error.WriteLine(
                    "warning: EVOSIM_INOCULATE_AT is set but EVOSIM_INOCULATE names no genome " +
                    "file — the assay will not fire.");
            }

            if (inoculumHash != null && settings.InoculateAt <= 0f)
            {
                Console.Error.WriteLine(
                    "warning: EVOSIM_INOCULATE names a genome file but EVOSIM_INOCULATE_AT is " +
                    "unset or zero — the assay will not fire.");
                inoculumHashShort = null;
            }

            RunConfig config = EnvBinding.BuildConfig(settings);
            float physicsDt = EnvBinding.ResolvePhysicsStep(settings.PhysicsDt, out int stepsPerMetabolic);
            int threads = settings.ResolveThreads();

            string outPath = settings.ResolveOutPath();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));

            // The world, before the directory: the header's space token and the manifest's four
            // measured bed facts are properties of the world the launch produced and exist
            // nowhere else. Constructing it here also means a config the world refuses — a cell
            // that does not divide the box, a tank under rolls — stops the launch before a run
            // directory is made for it.
            var world = new World(config, settings.Seed);

            var space = SpaceFacts.Of(
                world,
                hasWall: config.SharedSpace && config.WorldShape == WorldShape.Tank,
                hasFloor: config.SharedSpace,
                threads: threads,
                inoculumHashShort: inoculumHashShort);

            RunDirectory dir = RunDirectory.Create(
                Path.Combine(
                    Path.GetDirectoryName(outPath),
                    Path.GetFileNameWithoutExtension(outPath)),
                config, DateTime.UtcNow);

            RunManifest manifest = Manifest.Build(
                settings, config.Hash(), inoculumHash, physicsDt, stepsPerMetabolic, threads);

            Manifest.RecordBed(manifest, world.Bed);
            Manifest.Write(dir, manifest, ending: null);

            var report = new Report(outPath, config);
            report.Begin(settings, config, space, manifest.EngineVersion);
            report.Flush();

            Console.WriteLine(report.Text.TrimEnd());
            Console.WriteLine();
            Console.WriteLine("run directory: " + dir.Path);
            Console.WriteLine("report:        " + outPath);
            Console.WriteLine(
                "threads:       " + threads.ToString(CultureInfo.InvariantCulture) +
                "   dt " + physicsDt.ToString(CultureInfo.InvariantCulture) +
                " s, metabolic step " + (stepsPerMetabolic * physicsDt).ToString(CultureInfo.InvariantCulture) + " s");

            // The second write, and the reason it is here rather than after a loop: a manifest
            // reading `running` forever is exactly the thing the two-write protocol exists to stop
            // (the Sol/GPT review of 2026-09-03, finding 6), and a run directory this program
            // leaves behind must not read like a live arm or like a crash. So the protocol runs to
            // its end and says what actually happened. Work package G replaces this call with the
            // loop's own ending; nothing else here changes.
            Manifest.Write(dir, manifest, new RunEnding
            {
                Status = "error",
                Reason = "loop-not-wired",
                Prose = "simulation loop not yet wired (work package G)",
            });

            dir.Dispose();

            Console.Error.WriteLine(
                "simulation loop not yet wired (work package G). What is in: the environment " +
                "binding, the run manifest and the report header — config.json, run.json and the " +
                "report's first three lines are written, and run.json says `error` / " +
                "`loop-not-wired` rather than leaving `running` standing. Exit 2, never 0: a " +
                "queue must not read this as an arm that ran.");

            return 2;
        }

        private static void Help()
        {
            Console.WriteLine(
                "Evosim.Farm — one evolution arm, stepped by Evosim.Dynamics rather than by Unity.");
            Console.WriteLine();
            Console.WriteLine("  Evosim.Farm [EVOSIM_NAME=value ...]");
            Console.WriteLine();
            Console.WriteLine(
                "Every setting is an environment variable, as it was under the editor; an");
            Console.WriteLine(
                "argument of the same form overrides one. The settings this build reads:");
            Console.WriteLine();

            IReadOnlyList<string> names = EnvBinding.Names;
            for (int i = 0; i < names.Count; i++) Console.WriteLine("  " + names[i]);
        }
    }
}
