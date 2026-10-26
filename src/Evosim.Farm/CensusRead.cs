using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// What every read-only census shares: the run directory, its config, the checkpoint at a
    /// named second, and the refusals (<see cref="ExtentCensus"/>, <see cref="MouthCensus"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One reading of a recording, so two censuses cannot part on what they refuse.</b> A census
    /// restores a world from the checkpoint at exactly the second named and steps nothing. The
    /// source hashes are allowed to differ and are named; the config hash is not, whatever the
    /// environment says.
    /// </para>
    /// <para>
    /// <b>Only three environment names are read</b>, so that a shell still carrying a launcher's
    /// world cannot reach the read.
    /// </para>
    /// </remarks>
    internal sealed class CensusRead
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The tool's name, which leads each line it logs.</summary>
        public string Tool { get; private set; }

        public string RunDir { get; private set; }
        public RunConfig Config { get; private set; }
        public TricklePoolFiles.Pool Pool { get; private set; }
        public bool Migrating { get; private set; }
        public EnvSettings Settings { get; private set; }

        /// <summary>One checkpoint, found and checked: its file, header and step sizes, and how its source compares.</summary>
        public sealed class Found
        {
            public string Path;
            public CheckpointHeader Header;
            public float PhysicsDt;
            public int StepsPerMetabolic;
            public FilmWindow.SourceComparison Sources;
        }

        /// <summary>The comma-separated seconds, in the order given.</summary>
        public static double[] ParseSeconds(string text, string usage)
        {
            string[] pieces = (text ?? string.Empty).Split(',');
            var seconds = new List<double>();

            foreach (string piece in pieces)
            {
                string trimmed = piece.Trim();
                if (trimmed.Length == 0) continue;

                if (!double.TryParse(trimmed, NumberStyles.Float, Inv, out double s) ||
                    double.IsNaN(s) || double.IsInfinity(s) || s < 0d)
                {
                    throw new FormatException("'" + trimmed + "' is not a second. " + usage);
                }

                seconds.Add(s);
            }

            if (seconds.Count == 0) throw new FormatException("No seconds named. " + usage);
            return seconds.ToArray();
        }

        /// <summary>
        /// Opens the run: refuses an out file inside it, reads its config, pool and manifest.
        /// </summary>
        public static CensusRead Open(
            string tool, string usage, string runPath, string outFile, EnvBinding.Lookup env, TextWriter log)
        {
            string runDirectory = ResolveRunDirectory(runPath, usage);
            string outPath = Path.GetFullPath(outFile);
            RefuseTheRunDirectory(runDirectory, outPath);

            // Three names and no others, so that a shell still carrying a launcher's world cannot reach the read.
            EnvSettings settings = EnvBinding.Read(name =>
                name == "EVOSIM_ALLOW_SOURCE_MISMATCH" || name == "EVOSIM_REPO_ROOT" || name == "EVOSIM_THREADS"
                    ? env(name)
                    : null);

            RunConfig config = RunDirectory.ReadConfig(runDirectory, out string edited);
            if (edited != null)
            {
                log.WriteLine(tool + ": warning: " + Path.Combine(runDirectory, "config.json") + " " + edited);
            }

            TricklePoolFiles.Pool pool = TricklePoolFiles.Load(runDirectory, config);
            JsonNode manifest = Json.Parse(File.ReadAllText(Path.Combine(runDirectory, "run.json")));

            log.WriteLine(tool + ": run " + runDirectory + ", config hash " + config.Hash());

            return new CensusRead
            {
                Tool = tool,
                RunDir = runDirectory,
                Config = config,
                Pool = pool,
                Migrating = manifest.Has("migrateEverySeconds"),
                Settings = settings,
            };
        }

        /// <summary>
        /// The checkpoint at exactly <paramref name="second"/>, its header read and its config
        /// checked. A differing source is allowed and logged; a differing config is refused.
        /// </summary>
        public Found Find(double second, TextWriter log)
        {
            string checkpoint = Checkpoint.At(RunDir, second);

            if (checkpoint == null || Math.Abs(Checkpoint.SecondsInName(checkpoint) - second) > 1e-6)
            {
                throw new InvalidOperationException(
                    "The run has no checkpoint at " + second.ToString("0.###", Inv) + " s" +
                    (checkpoint == null
                        ? "."
                        : "; the nearest at or before is " + System.IO.Path.GetFileName(checkpoint) + ".") +
                    " A census reads a world as it stood and steps nothing.");
            }

            CheckpointHeader header = CheckpointReader.ReadHeader(checkpoint);
            float physicsDt = EnvBinding.ResolvePhysicsStep(header.PhysicsStepSeconds, Config.PaceFactor, out int stepsPerMetabolic);

            RunManifest build = Manifest.Build(Settings, Config.Hash(), null, physicsDt, stepsPerMetabolic, 1, Config.PaceFactor);
            FilmWindow.SourceComparison sources = FilmWindow.CompareSources(
                header, Config.Hash(), build.CoreHash, build.DynamicsHash, build.FarmHash);

            if (sources.Config != null)
            {
                throw new InvalidOperationException(
                    "This checkpoint was written under another config, so its world is not the run's " +
                    "config's: " + sources.Config + ". The census refuses it.");
            }

            if (sources.Sources.Count > 0)
            {
                log.WriteLine(
                    Tool + ": the source differs from the run's (" + string.Join("; ", sources.Sources) +
                    "); allowed for this read, which steps nothing.");
            }

            return new Found
            {
                Path = checkpoint,
                Header = header,
                PhysicsDt = physicsDt,
                StepsPerMetabolic = stepsPerMetabolic,
                Sources = sources,
            };
        }

        /// <summary>A run directory, or the newest run under an arm directory that holds a config.</summary>
        public static string ResolveRunDirectory(string path, string usage)
        {
            string full = System.IO.Path.GetFullPath(path);

            if (!Directory.Exists(full))
            {
                throw new DirectoryNotFoundException("'" + path + "' is not a directory. " + usage);
            }

            if (File.Exists(System.IO.Path.Combine(full, "config.json"))) return full;

            string[] candidates = Directory.GetDirectories(full);
            Array.Sort(candidates, StringComparer.Ordinal);

            for (int i = candidates.Length - 1; i >= 0; i--)
            {
                if (File.Exists(System.IO.Path.Combine(candidates[i], "config.json"))) return candidates[i];
            }

            throw new DirectoryNotFoundException("'" + path + "' holds no config.json and no run directory that does.");
        }

        /// <summary>The run directory is the recording, and a census never writes into it.</summary>
        public static void RefuseTheRunDirectory(string runDirectory, string outPath)
        {
            string run = runDirectory.TrimEnd(
                System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

            if (outPath.StartsWith(run + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                outPath.StartsWith(run + System.IO.Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The out file " + outPath + " is inside the run directory " + runDirectory +
                    ". A census never writes into the recording it reads.");
            }
        }
    }
}
