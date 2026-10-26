using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Evosim.Farm;

namespace Evosim.Nursery
{
    /// <summary>
    /// What the exam is going to swim, written into run.json before it starts: its brains in order, its genomes (the id each had in the
    /// search, and its body), the draws per brain, the salt of its seeds and the seeds themselves. Tank-mates are in
    /// <c>tank-exam-draws.jsonl</c> (<c>tank-bench-draws.jsonl</c> for a bench), per brain.
    /// </summary>
    public sealed class ExamPlan
    {
        public sealed class BrainEntry { public string Label, Kind; }

        public sealed class GenomeEntry { public string Id; public int Body; }

        public List<BrainEntry> Brains = new List<BrainEntry>();
        public List<GenomeEntry> Genomes = new List<GenomeEntry>();

        /// <summary>The exam's draws, which are its episodes of each genome per brain.</summary>
        public int Episodes;

        public ulong Salt;

        /// <summary>What this plan is of: <c>exam</c> (the held-out swim that follows a search) or <c>bench</c> (a pool swum as it is, with no search). The draws file is named for it (<see cref="TankLog.BenchOrExamDrawsFile"/>).</summary>
        public string Name = "exam";

        /// <summary>The exam's episode seeds, one a draw. Written as decimal strings: they are 64 bits, and a JSON reader that takes numbers as doubles would round them.</summary>
        public ulong[] Seeds = new ulong[0];

        internal void Render(Json.Writer w)
        {
            w.BeginObject("exam");
            w.Field("name", Name);
            w.BeginArray("brains");
            foreach (BrainEntry b in Brains)
            {
                w.BeginObject();
                w.Field("label", b.Label);
                w.Field("kind", b.Kind);
                w.EndObject();
            }
            w.EndArray();
            w.BeginArray("genomes");
            foreach (GenomeEntry g in Genomes)
            {
                w.BeginObject();
                w.Field("id", g.Id);
                w.Field("body", g.Body);
                w.EndObject();
            }
            w.EndArray();
            w.Field("episodes", Episodes);
            w.Field("salt", Salt.ToString(CultureInfo.InvariantCulture));
            w.BeginArray("seeds");
            foreach (ulong seed in Seeds) w.Value(seed.ToString(CultureInfo.InvariantCulture));
            w.EndArray();
            w.EndObject();
        }
    }

    /// <summary>
    /// The run record (logbook/specs/nursery-general-build.md, section 1): <c>run.json</c> in the
    /// run's folder, written before the first episode and again when the run ends, saying how the run
    /// was made. Until the recipe exists (step 2) it holds the arguments and every resolved option.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The tree hashes are the farm's</b> (<see cref="Manifest.HashSourceTree"/>), taken at run
    /// time from the source above the working directory. They name the source beside the program,
    /// which matches the program only while nobody edits the tree between its build and its launch;
    /// <c>programPath</c> says which build ran.
    /// </para>
    /// <para>
    /// <b>The build's half never stops a run</b>, but for one thing. git may be missing or the root
    /// unfindable: each failure writes <c>"unknown"</c> (or a null dirty flag) and says why in
    /// <c>note</c>, as the farm's manifest does. The list of appended files
    /// (<see cref="AppendedPaths"/>) is the exception: a tree without it is refused, naming it. The
    /// opening write does stop a run that cannot record itself, before any episode; the closing write
    /// only warns (<see cref="TryWrite"/>).
    /// </para>
    /// <para>
    /// A killed run leaves <c>status</c> at <c>running</c>: nothing can write an ending for a
    /// process stopped from outside. The process id and its start time say whether it still runs.
    /// </para>
    /// </remarks>
    public sealed class RunRecord
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string[] Args = Array.Empty<string>();

        /// <summary>Every resolved option, its name and its value as JSON, in declaration order.</summary>
        public List<KeyValuePair<string, string>> Options = new List<KeyValuePair<string, string>>();

        /// <summary>The recipe file, its name, the hash of the resolved recipe and its text (recipe.json); set by every run, since the nursery runs only from a recipe.</summary>
        public string RecipePath, RecipeName, RecipeHash, RecipeJson;

        public string ConfigPath;

        /// <summary>SHA-256 of the config file's bytes.</summary>
        public string ConfigFileSha256;

        /// <summary>The config's own hash as read, before the nursery's overrides.</summary>
        public string ConfigHash;

        /// <summary>The hash of the config an episode is built from, after the overrides.</summary>
        public string EpisodeConfigHash;

        /// <summary>That config, written by <see cref="RunConfigJson.Write"/>.</summary>
        public string EpisodeConfigJson;

        public string GitCommit, CoreHash, DynamicsHash, FarmHash, NurseryHash, ProgramPath, RepoRoot, Note;
        /// <summary>Null when git status could not be read, so that unknown never reads as clean.</summary>
        public bool? GitDirty;

        public ulong Seed;

        /// <summary>The tank's episode salts (<c>SeedsFor</c>), by name; the stream salts in the code are named by <c>nurseryHash</c>.</summary>
        public List<KeyValuePair<string, ulong>> Salts = new List<KeyValuePair<string, ulong>>();

        public int Threads;

        /// <summary>
        /// D240: the tank's physics step, the world's pace factor k (D239) and the physics steps in a metabolic step (0.5·k / dt), so that a batch
        /// says which pace it ran at. <see cref="StepsPerMetabolicStep"/> 0 means unset, and then <c>run.json</c> has no <c>physics</c> object.
        /// </summary>
        public float PhysicsDt, PaceFactor = 1f;
        public int StepsPerMetabolicStep;

        /// <summary>
        /// The exam's plan, set and written to run.json before the exam's first episode (a run that never reaches an exam has none, and the
        /// record then holds no <c>exam</c> field). Progress is read from <c>tank-exam.jsonl</c>'s rows, as before.
        /// </summary>
        public ExamPlan Exam;

        public string StartedAtUtc = DateTime.UtcNow.ToString("o", Inv);

        /// <summary>
        /// The process and when it started, so that a reader can tell a killed run from one still
        /// writing: <c>running</c> with no live process of this id, or a live one that started at
        /// another time (a reused id), is a killed run.
        /// </summary>
        public int ProcessId;
        public string ProcessStartedAtUtc;

        /// <summary>
        /// <c>running</c>, then <c>completed</c> (exit 0), <c>exited</c> (another code), <c>failed</c> (an
        /// exception) or <c>stopped</c> (a <see cref="StopFileName"/> file found in the run's folder).
        /// </summary>
        public string Status = "running";

        public string EndedAtUtc, Error;
        public int? ExitCode;

        /// <summary>
        /// The file whose presence in a run's folder asks the run to end cleanly (CLAUDE.md: a run is
        /// stopped by a <c>STOP</c> file, never by killing it). The nursery looks for it between
        /// generations and between the exam's brains, and reads nothing from it.
        /// </summary>
        public const string StopFileName = "STOP";

        /// <summary>True when <paramref name="folder"/> holds the stop file. Reads the directory entry and nothing else.</summary>
        public static bool StopRequested(string folder) =>
            !string.IsNullOrEmpty(folder) && File.Exists(Path.Combine(folder, StopFileName));

        /// <summary>
        /// The files every run or room operation appends to, one repository-relative path a line, <c>#</c>
        /// comments allowed. A run's dirty check leaves them out: appending to them is not editing the tree.
        /// </summary>
        public const string AppendedFileRelativePath = "rounds/nursery/appended.txt";

        /// <summary>
        /// The paths <see cref="AppendedFileRelativePath"/> lists, read from the tree at <paramref name="repoRoot"/>.
        /// A missing or unreadable file, a line that is no plain repository-relative path, and a file that
        /// lists nothing are each refused by naming the file: an empty list would make every run look dirty
        /// the moment it was entered in the registry.
        /// </summary>
        public static IReadOnlyList<string> AppendedPaths(string repoRoot)
        {
            string file = Path.Combine(repoRoot, AppendedFileRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string[] lines;
            try { lines = File.ReadAllLines(file); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new InvalidDataException($"{file} cannot be read ({e.Message}); it lists the files a run appends to, and the dirty check needs it", e);
            }
            var paths = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line.IndexOfAny(new[] { ':', '*', '?', '[', ']', '\\', '"', '\'', ' ' }) >= 0 || line[0] == '/' || line.Split('/').Contains(".."))
                    throw new InvalidDataException($"{file}, line {i + 1}: \"{line}\" is not a plain repository-relative path with forward slashes");
                paths.Add(line);
            }
            if (paths.Count == 0) throw new InvalidDataException($"{file} lists no file; it must at least list {RegistryEntry.RelativePath}");
            return paths;
        }

        /// <summary>Fills the build's half: the commit, the dirty flag and the four tree hashes.</summary>
        public void ReadBuild(string repoRoot)
        {
            var notes = new List<string>();
            try
            {
                using (Process self = Process.GetCurrentProcess())
                {
                    ProcessId = self.Id;
                    ProcessStartedAtUtc = self.StartTime.ToUniversalTime().ToString("o", Inv);
                }
            }
            catch (Exception e)
            {
                notes.Add("the process's start time could not be read: " + e.GetType().Name);
            }
            ProgramPath = AppContext.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool rootKnown = !string.IsNullOrEmpty(repoRoot);
            if (!rootKnown)
            {
                repoRoot = FindRepoRoot(Directory.GetCurrentDirectory()) ?? FindRepoRoot(AppContext.BaseDirectory);
                rootKnown = repoRoot != null;
                if (repoRoot == null)
                {
                    repoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
                    notes.Add("no src/Evosim.Core above the working directory or the program; repo root assumed to be the working directory, and the dirty check skipped");
                }
            }
            RepoRoot = repoRoot;
            bool rootFound = rootKnown;

            GitCommit = Git(repoRoot, "rev-parse HEAD", out string commitFailure)?.Trim();
            if (string.IsNullOrEmpty(GitCommit))
            {
                GitCommit = "unknown";
                notes.Add("git rev-parse HEAD failed: " + (commitFailure ?? "no output"));
            }

            // Code paths, as the farm's are, and rounds/ for the recipes: a markdown draft at the
            // root does not make a run dirty, and nor do the files every run or room operation appends
            // to (the registry, which a run appends to after it has ended, and the room's activity
            // log), which rounds/nursery/appended.txt lists. No optional locks, so a launch never
            // holds the index lock against a commit in the same tree.
            // With no repository root found there is no tree whose list can be read: the check is skipped, the flag is null (never
            // clean) and the note says why, where a root that is found but holds no readable list refuses the run (AppendedPaths).
            if (!rootFound) GitDirty = null;
            else
            {
                string excludes = string.Join(" ", AppendedPaths(repoRoot).Select(p => ":(exclude)" + p));
                string porcelain = Git(repoRoot, "--no-optional-locks status --porcelain -- src unity scripts rounds " + excludes, out string statusFailure);
                if (statusFailure != null) notes.Add("git status --porcelain failed: " + statusFailure);
                GitDirty = statusFailure != null ? (bool?)null : !string.IsNullOrEmpty(porcelain.Trim());
            }

            CoreHash = TreeHash(repoRoot, "Evosim.Core", notes);
            DynamicsHash = TreeHash(repoRoot, "Evosim.Dynamics", notes);
            FarmHash = TreeHash(repoRoot, "Evosim.Farm", notes);
            NurseryHash = TreeHash(repoRoot, "Evosim.Nursery", notes);
            Note = notes.Count == 0 ? null : string.Join("; ", notes);
        }

        /// <summary>Records the ending: <c>completed</c> for exit 0, <c>exited</c> for another code.</summary>
        public void End(int exitCode)
        {
            Status = exitCode == 0 ? "completed" : "exited";
            ExitCode = exitCode;
            EndedAtUtc = DateTime.UtcNow.ToString("o", Inv);
        }

        /// <summary>Records the ending of a run that found the stop file: exit 0, and no registry entry.</summary>
        public void Stop()
        {
            Status = "stopped";
            ExitCode = 0;
            EndedAtUtc = DateTime.UtcNow.ToString("o", Inv);
        }

        /// <summary>Records an exception that ended the run.</summary>
        public void Fail(Exception e)
        {
            Status = "failed";
            ExitCode = null;
            Error = e.GetType().Name + ": " + e.Message;
            EndedAtUtc = DateTime.UtcNow.ToString("o", Inv);
        }

        /// <summary>The document.</summary>
        public string Render()
        {
            var w = new Json.Writer(indent: true);
            w.BeginObject();
            w.Field("program", "Evosim.Nursery");
            w.Field("status", Status);
            w.Field("startedAtUtc", StartedAtUtc);
            // The build reads a stop file of this name; a record without the field was written by a build that does not.
            w.Field("stopFile", StopFileName);
            w.Field("processId", ProcessId);
            w.Field("processStartedAtUtc", ProcessStartedAtUtc);
            w.Field("endedAtUtc", EndedAtUtc);
            if (ExitCode.HasValue) w.Field("exitCode", ExitCode.Value); else w.Raw("exitCode", "null");
            w.Field("error", Error);

            w.BeginArray("args");
            foreach (string a in Args) w.Value(a);
            w.EndArray();

            if (RecipeHash == null) w.Raw("recipe", "null");
            else
            {
                w.BeginObject("recipe");
                w.Field("path", RecipePath);
                w.Field("name", RecipeName);
                w.Field("hash", RecipeHash);
                w.Raw("resolved", RecipeJson.TrimEnd('\n'));
                w.EndObject();
            }

            // Null under a recipe, whose resolved text above is the run's settings.
            if (Options != null)
            {
                w.BeginObject("options");
                foreach (KeyValuePair<string, string> kv in Options) w.Raw(kv.Key, kv.Value);
                w.EndObject();
            }

            w.Field("seed", Seed);
            w.BeginObject("salts");
            foreach (KeyValuePair<string, ulong> kv in Salts) w.Field(kv.Key, kv.Value);
            w.EndObject();
            w.Field("threads", Threads);

            // Written only when set, so that a record that never reached a recipe keeps the text it had.
            if (StepsPerMetabolicStep > 0)
            {
                w.BeginObject("physics");
                w.Field("dt", PhysicsDt);
                w.Field("paceFactor", PaceFactor);
                w.Field("stepsPerMetabolicStep", StepsPerMetabolicStep);
                w.EndObject();
            }

            if (Exam != null) Exam.Render(w);

            w.BeginObject("config");
            w.Field("path", ConfigPath);
            w.Field("fileSha256", ConfigFileSha256);
            w.Field("hash", ConfigHash);
            w.Field("episodeHash", EpisodeConfigHash);
            w.Raw("episode", EpisodeConfigJson);
            w.EndObject();

            w.BeginObject("build");
            w.Field("gitCommit", GitCommit);
            if (GitDirty.HasValue) w.Field("gitDirty", GitDirty.Value); else w.Raw("gitDirty", "null");
            w.Field("coreHash", CoreHash);
            w.Field("dynamicsHash", DynamicsHash);
            w.Field("farmHash", FarmHash);
            w.Field("nurseryHash", NurseryHash);
            w.Field("programPath", ProgramPath);
            w.Field("repoRoot", RepoRoot);
            w.Field("note", Note);
            w.EndObject();

            w.EndObject();
            return w.ToString();
        }

        /// <summary>
        /// Writes <c>run.json</c> into <paramref name="directory"/>, beside the file and moved over it,
        /// so that a reader never catches a truncated document.
        /// </summary>
        public void Write(string directory)
        {
            string path = Path.Combine(directory, "run.json");
            string temp = path + ".tmp";
            File.WriteAllText(temp, Render(), new UTF8Encoding(false));
            // A reader that holds run.json open can refuse the move for a moment: three tries.
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temp, path, overwrite: true);
                    return;
                }
                catch (Exception e) when ((e is IOException || e is UnauthorizedAccessException) && attempt < 3)
                {
                    System.Threading.Thread.Sleep(200);
                }
            }
        }

        /// <summary>
        /// <see cref="Write"/>, returning null, or the reason it failed; for the closing write, which
        /// must not turn a run's ending into a crash.
        /// </summary>
        public string TryWrite(string directory)
        {
            try
            {
                Write(directory);
                return null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        /// <summary>Every public instance field of <paramref name="options"/>, in declaration order, with its value as JSON.</summary>
        public static List<KeyValuePair<string, string>> FieldsOf(object options) =>
            options.GetType()
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .OrderBy(f => f.MetadataToken)
                .Select(f => new KeyValuePair<string, string>(f.Name, JsonValue(f.GetValue(options))))
                .ToList();

        /// <summary>One option's value as JSON: null, a number by "R" (NaN as null), a string, or an array of them.</summary>
        public static string JsonValue(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string s: return Quote(s);
                case bool b: return b ? "true" : "false";
                case double d: return double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", Inv);
                case float f: return float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("R", Inv);
                case int i: return i.ToString(Inv);
                case long l: return l.ToString(Inv);
                case ulong u: return u.ToString(Inv);
                case Enum e: return Quote(e.ToString());
                case System.Collections.IEnumerable items:
                    return "[" + string.Join(",", items.Cast<object>().Select(JsonValue)) + "]";
                default:
                    throw new ArgumentException("the run record cannot write an option of type " + value.GetType().Name);
            }
        }

        private static string Quote(string s)
        {
            var w = new Json.Writer(indent: false);
            w.BeginArray();
            w.Value(s);
            w.EndArray();
            string a = w.ToString();
            return a.Substring(1, a.Length - 2);
        }

        /// <summary>SHA-256 of a file's bytes, lowercase hex.</summary>
        public static string FileSha256(string path) => Manifest.HashBytes(File.ReadAllBytes(path));

        private static string TreeHash(string repoRoot, string project, List<string> notes)
        {
            string root = Path.Combine(repoRoot, "src", project);
            string hash = Manifest.HashSourceTree(root);
            if (hash != null) return hash;
            notes.Add("no .cs found under " + root);
            return "unknown";
        }

        /// <summary>
        /// The main checkout of the repository <paramref name="root"/> belongs to: the folder above git's
        /// common directory, which a worktree shares with it. <paramref name="root"/> itself when git
        /// cannot say.
        /// </summary>
        public static string MainCheckoutOf(string root)
        {
            string common = Git(root, "rev-parse --path-format=absolute --git-common-dir", out _)?.Trim();
            if (string.IsNullOrEmpty(common)) return root;
            string parent = Path.GetDirectoryName(Path.GetFullPath(common, root));
            return parent != null && Directory.Exists(Path.Combine(parent, "src", "Evosim.Core")) ? parent : root;
        }

        /// <summary>The first folder at or above <paramref name="from"/> that holds src/Evosim.Core, or null.</summary>
        public static string FindRepoRoot(string from)
        {
            for (var dir = new DirectoryInfo(Path.GetFullPath(from)); dir != null; dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, "src", "Evosim.Core"))) return dir.FullName;
            return null;
        }

        /// <summary>Runs git and returns stdout, or null. Read-only and bounded.</summary>
        private static string Git(string workingDirectory, string arguments, out string failure)
        {
            failure = null;
            try
            {
                var info = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var process = Process.Start(info))
                {
                    // Both streams read at once and the wait bounded, so a git that hangs or writes
                    // a lot to stderr cannot hold the run at its start.
                    var output = process.StandardOutput.ReadToEndAsync();
                    var error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(15000))
                    {
                        try { process.Kill(); } catch (Exception) { }
                        failure = "timed out";
                        return null;
                    }
                    process.WaitForExit();
                    if (process.ExitCode != 0) { failure = "exit " + process.ExitCode + ": " + error.Result.Trim(); return null; }
                    return output.Result;
                }
            }
            catch (Exception e)
            {
                failure = e.GetType().Name + ": " + e.Message;
                return null;
            }
        }
    }
}
