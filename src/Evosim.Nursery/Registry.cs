using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Evosim.Farm;

namespace Evosim.Nursery
{
    /// <summary>
    /// The registry (logbook/specs/nursery-general-build.md, section 5): one line a run in
    /// <c>rounds/nursery/registry.jsonl</c>, naming the recipe and the world config by their hashes,
    /// the build, the output folder, how the run ended, and a digest of each result file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every run of an experiment is entered</b>, an experiment being a recipe under
    /// <c>rounds/nursery/</c>: completed, exited or failed. A run ended early by a <c>STOP</c> file in
    /// its folder (RunRecord.StopFileName) is not entered. A run from a dirty tree is entered as not
    /// rerunnable, with the reason, since the commit it names does not hold what ran. A run of a recipe
    /// anywhere else is a trial, and is not entered.
    /// </para>
    /// <para>
    /// <b>There is one registry, the main checkout's</b>, whichever worktree a run is launched from.
    /// A rerun runs in a worktree built at its entry's commit, which cannot hold the entry (the line is
    /// appended after the run), so it reads and appends to the main checkout's file.
    /// </para>
    /// <para>
    /// <b>The file is append-only</b>, so an entry's number (its place among the non-blank lines,
    /// from 1) never changes. A rerun
    /// is entered as a new line naming the entry it reran and whether it came out identical.
    /// </para>
    /// <para>
    /// <b>Result files</b> are every file in the output folder but the ones that cannot repeat or
    /// are not results: the run record, the recipe's copy, the wall times, and logs.
    /// </para>
    /// <para>
    /// <b>A reference entry</b> stands for a run made before the framework, on another build, that a
    /// check showed the recipe reproduces. It names that build, the check's verdict, and why its
    /// digests cannot match a rerun's; <c>--rerun</c> refuses it and points at the check.
    /// </para>
    /// </remarks>
    public sealed class RegistryEntry
    {
        public string Recipe, RecipeHash, RecipePath, ConfigHash;
        public string GitCommit, CoreHash, DynamicsHash, FarmHash, NurseryHash;
        public string Out, Status, StartedAtUtc, EndedAtUtc;

        /// <summary>The entry this run reran (its line number), or 0.</summary>
        public int RerunOf;

        /// <summary>For a rerun: whether every result file's digest matched the entry's; null for a first run.</summary>
        public bool? Identical;

        /// <summary>False when the commit does not hold what ran (a dirty tree), with the reason.</summary>
        public bool Rerunnable = true;
        public string NotRerunnableBecause;

        /// <summary>Null for a run the framework made; set for a reference entry.</summary>
        public ReferenceNote Reference;

        public sealed class ReferenceNote
        {
            /// <summary>The build the run was made on, in words: its commit, its name and where its program lies.</summary>
            public string Build;

            /// <summary>The check that ties the run to its recipe, and its result, as the check printed it.</summary>
            public string Verdict;

            /// <summary>Why its digests cannot match a rerun's, each a known difference and not a fault.</summary>
            public string[] KnownDifferences = new string[0];
        }

        /// <summary>Result file name to SHA-256, ordinal by name.</summary>
        public SortedDictionary<string, string> Digests = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The registry under a repository root.</summary>
        public static string FileUnder(string root) => Path.Combine(root, "rounds", "nursery", "registry.jsonl");

        /// <summary>The registry's path relative to the root, as git's pathspec writes it.</summary>
        public const string RelativePath = "rounds/nursery/registry.jsonl";

        /// <summary>Files in a run's folder that are not results: they cannot repeat (wall time, the process) or they copy an input.</summary>
        /// <remarks>
        /// The three files of who swam with whom and where each member came from (<see cref="TankLog.DrawsFile"/>,
        /// <see cref="TankLog.ExamDrawsFile"/> (or <see cref="TankLog.BenchDrawsFile"/>) and <see cref="TankLog.LineageFile"/>) are results: they repeat bit for bit from the recipe's seed, and
        /// the rule below takes them as any other .jsonl. A registry entry made before they existed does not hold them, so a rerun of it on a
        /// build that writes them would differ from the entry by those files alone ("written by the rerun, not by the entry"). That cannot
        /// arise: a rerun runs on its entry's build (<see cref="BuildDifferences"/> refuses another), and that build does not write them.
        /// </remarks>
        public static bool IsResult(string fileName)
        {
            string name = fileName.ToLowerInvariant();
            if (name == "run.json" || name == "run.json.tmp" || name == "recipe.json" || name == "timing.jsonl" || name == TankProfile.FileName) return false;
            // The stop file (RunRecord.StopFileName) asks a run to end; it is not something the run wrote. A stopped run is not entered, so no
            // registry entry was digested with one in its folder before this line: no older digest changes.
            if (name == RunRecord.StopFileName.ToLowerInvariant()) return false;
            string ext = Path.GetExtension(name);
            return ext != ".log" && ext != ".err" && ext != ".txt";
        }

        /// <summary>SHA-256 of every result file in <paramref name="folder"/>, by name.</summary>
        public static SortedDictionary<string, string> DigestsOf(string folder)
        {
            var d = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(folder))
            {
                string name = Path.GetFileName(path);
                if (IsResult(name)) d[name] = Manifest.HashBytes(File.ReadAllBytes(path));
            }
            return d;
        }

        /// <summary>One compact line.</summary>
        public string ToJson()
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject();
            w.Field("recipe", Recipe);
            w.Field("recipeHash", RecipeHash);
            w.Field("recipePath", RecipePath);
            w.Field("configHash", ConfigHash);
            w.Field("gitCommit", GitCommit);
            w.Field("coreHash", CoreHash);
            w.Field("dynamicsHash", DynamicsHash);
            w.Field("farmHash", FarmHash);
            w.Field("nurseryHash", NurseryHash);
            w.Field("out", Out);
            w.Field("status", Status);
            w.Field("startedAtUtc", StartedAtUtc);
            w.Field("endedAtUtc", EndedAtUtc);
            w.Field("rerunOf", RerunOf);
            if (Identical.HasValue) w.Field("identical", Identical.Value); else w.Raw("identical", "null");
            w.Field("rerunnable", Rerunnable);
            w.Field("notRerunnableBecause", NotRerunnableBecause);
            if (Reference == null) w.Raw("reference", "null");
            else
            {
                w.BeginObject("reference");
                w.Field("build", Reference.Build);
                w.Field("verdict", Reference.Verdict);
                w.BeginArray("knownDifferences");
                foreach (string k in Reference.KnownDifferences) w.Value(k);
                w.EndArray();
                w.EndObject();
            }
            w.BeginObject("digests");
            foreach (KeyValuePair<string, string> kv in Digests) w.Field(kv.Key, kv.Value);
            w.EndObject();
            w.EndObject();
            return w.ToString();
        }

        /// <summary>The inverse of <see cref="ToJson"/>; refuses a line that lacks a field.</summary>
        public static RegistryEntry FromJson(string line)
        {
            JsonNode n = Json.Parse(line);
            var e = new RegistryEntry
            {
                Recipe = n["recipe"].AsString(), RecipeHash = n["recipeHash"].AsString(), RecipePath = n["recipePath"].AsString(),
                ConfigHash = n["configHash"].AsString(), GitCommit = n["gitCommit"].AsString(),
                CoreHash = n["coreHash"].AsString(), DynamicsHash = n["dynamicsHash"].AsString(),
                FarmHash = n["farmHash"].AsString(), NurseryHash = n["nurseryHash"].AsString(),
                Out = n["out"].AsString(), Status = n["status"].AsString(),
                StartedAtUtc = n["startedAtUtc"].AsString(), EndedAtUtc = n["endedAtUtc"].AsString(),
                RerunOf = n["rerunOf"].AsInt(),
                Identical = n["identical"].Kind == JsonNode.NodeKind.Null ? (bool?)null : n["identical"].AsBool(),
                Rerunnable = n["rerunnable"].AsBool(), NotRerunnableBecause = n["notRerunnableBecause"].AsString(),
            };
            JsonNode r = n["reference"];
            if (r.Kind != JsonNode.NodeKind.Null)
                e.Reference = new ReferenceNote
                {
                    Build = r["build"].AsString(), Verdict = r["verdict"].AsString(),
                    KnownDifferences = r["knownDifferences"].Items().Select(x => x.AsString()).ToArray(),
                };
            JsonNode d = n["digests"];
            foreach (string k in d.Keys()) e.Digests[k] = d[k].AsString();
            return e;
        }

        /// <summary>
        /// Every entry, numbered from 1 in file order; empty when there is no registry yet. A line that
        /// does not read is refused with its number, since every number after it depends on it.
        /// </summary>
        public static List<RegistryEntry> ReadAll(string registryFile)
        {
            var all = new List<RegistryEntry>();
            if (!File.Exists(registryFile)) return all;
            string text = null;
            for (int attempt = 1; text == null; attempt++)
            {
                // An append holds the file alone for a moment.
                try
                {
                    using (var s = new FileStream(registryFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var r = new StreamReader(s, new UTF8Encoding(false)))
                        text = r.ReadToEnd();
                }
                catch (IOException) when (attempt < 50) { System.Threading.Thread.Sleep(100); }
            }
            int number = 0;
            foreach (string line in Lines(text))
            {
                number++;
                try { all.Add(FromJson(line.TrimEnd('\r'))); }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    throw new InvalidDataException($"{registryFile}: entry {number} does not read ({e.Message})", e);
                }
            }
            return all;
        }

        /// <summary>
        /// Appends one entry and returns its number. The file is held alone while its lines are
        /// counted and the line written, so two runs ending together cannot take one number, and only
        /// the opening is tried again. A last line left without its newline gets one first.
        /// </summary>
        public static int Append(string registryFile, RegistryEntry entry)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(registryFile));
            byte[] line = new UTF8Encoding(false).GetBytes(entry.ToJson() + "\n");
            FileStream s = null;
            for (int attempt = 1; s == null; attempt++)
            {
                try { s = new FileStream(registryFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) when (attempt < 50) { System.Threading.Thread.Sleep(100); }
            }
            using (s)
            {
                var existing = new byte[s.Length];
                int read = 0;
                while (read < existing.Length) read += s.Read(existing, read, existing.Length - read);
                int count = Lines(new UTF8Encoding(false).GetString(existing)).Count();
                s.Seek(0, SeekOrigin.End);
                if (existing.Length > 0 && existing[existing.Length - 1] != (byte)'\n') s.WriteByte((byte)'\n');
                s.Write(line, 0, line.Length);
                return count + 1;
            }
        }

        /// <summary>The non-blank lines of the registry's text, a leading byte-order mark dropped: what is numbered.</summary>
        private static IEnumerable<string> Lines(string text) =>
            text.TrimStart('\uFEFF').Split('\n').Where(l => l.Trim().Length > 0);

        /// <summary>
        /// The four tree hashes that differ between this entry and a build, by name; empty when the
        /// build is the entry's. A rerun is bit for bit on the entry's build only.
        /// </summary>
        public IReadOnlyList<string> BuildDifferences(RunRecord build)
        {
            var d = new List<string>();
            if (Differ(CoreHash, build.CoreHash)) d.Add("core");
            if (Differ(DynamicsHash, build.DynamicsHash)) d.Add("dynamics");
            if (Differ(FarmHash, build.FarmHash)) d.Add("farm");
            if (Differ(NurseryHash, build.NurseryHash)) d.Add("nursery");
            return d;
        }

        /// <summary>A tree that could not be hashed matches nothing, itself included.</summary>
        private static bool Differ(string a, string b) => a != b || a == null || a == "unknown";

        /// <summary>Result files a run began to write after entries already existed, which those entries cannot hold.</summary>
        private static readonly string[] LaterFiles = { TankPopulation.FileName };

        /// <summary>
        /// Compares a rerun's digests with this entry's: null when identical, or the first result file
        /// that differs and, when the entry's folder still holds that file, its first differing line.
        /// </summary>
        public string FirstDifference(string rerunFolder, SortedDictionary<string, string> rerun)
        {
            foreach (string name in Digests.Keys.Union(rerun.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                Digests.TryGetValue(name, out string was);
                rerun.TryGetValue(name, out string now);
                if (was == now) continue;
                // An entry made before the file existed holds no digest for it; its absence there is not a difference.
                if (was == null && LaterFiles.Contains(name)) continue;
                if (was == null) return $"{name}: written by the rerun, not by the entry";
                if (now == null) return $"{name}: written by the entry, not by the rerun";
                string old = Path.Combine(Out, name);
                if (!File.Exists(old)) return $"{name}: digests differ (the entry's copy is gone from {Out})";
                return $"{name}: " + FirstDifferentLine(old, Path.Combine(rerunFolder, name));
            }
            return null;
        }

        /// <summary>The first line two files differ on, both sides cut to 200 characters.</summary>
        public static string FirstDifferentLine(string a, string b)
        {
            string[] x = File.ReadAllLines(a), y = File.ReadAllLines(b);
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                string l = i < x.Length ? x[i] : "<end of file>", r = i < y.Length ? y[i] : "<end of file>";
                if (l != r) return $"line {i + 1} differs:\n  entry: {Cut(l)}\n  rerun: {Cut(r)}";
            }
            return "the bytes differ but no line does (line ends or a final newline)";
        }

        private static string Cut(string s) => s.Length <= 200 ? s : s.Substring(0, 200) + "...";
    }
}
