using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// The room's own settings, <c>rounds/nursery/room.json</c>, committed text. Read as the nursery
    /// reads its files: a missing key and an unknown key are both refused, naming the key, so that a
    /// misspelt setting never passes for the default.
    /// </summary>
    public sealed class RoomSettings
    {
        public const string RelativePath = "rounds/nursery/room.json";

        public int Port;

        /// <summary>The address the server listens on. Only loopback is accepted: nothing the room does leaves the machine.</summary>
        public string Bind;

        /// <summary>How often a screen asks for new data, and how often the readers look at their files.</summary>
        public int PollSeconds;

        /// <summary>The share of the machine's logical processors the room may launch work onto (the owner's ruling).</summary>
        public int CapLogical;

        /// <summary>Run folders, relative to the repository root, read as runs though they predate the registry.</summary>
        public List<string> Runs = new List<string>();

        /// <summary>
        /// For a run in <see cref="Runs"/> whose own <c>run.json</c> holds no episode config (a run made before the record existed), the config it ran on, a
        /// file relative to the repository root, by the run's folder as <see cref="Runs"/> names it. An entry in room.json is an object <c>{"folder": ..., "config": ...}</c>
        /// to carry one; a plain folder name carries none.
        /// </summary>
        public Dictionary<string, string> RunConfigs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The dotnet host a run is started with (the SDK Unity ships; nothing is installed system-wide), a full path or a name on the path.</summary>
        public string Dotnet;

        /// <summary>The nursery's program, relative to the repository root the room runs in (or a full path).</summary>
        public string NurseryDll;

        /// <summary>The folder, relative to the repository root, a launched run's own folder is made in.</summary>
        public string RunsDir;

        /// <summary>The activity log, relative to the main checkout (found as the registry is). It is also listed in rounds/nursery/appended.txt.</summary>
        public string ActivityFile;

        /// <summary>
        /// The logical processors a launched run may use, as the hex string room.json holds (<c>"0xFFFF0000"</c>, the efficiency cores: the
        /// coordinator's rule) read into a mask. A launch pins the new process to it before its first instruction (<see cref="CoreMask"/>).
        /// Never 0; a processor the machine lacks is refused when a run is launched, since the machine is not known here.
        /// </summary>
        public ulong Affinity;

        /// <summary>
        /// The one folder besides rounds/nursery/ that recipe.write may write a recipe into, relative to the repository root, such as
        /// <c>scratch/nursery-trials</c>: gitignored, so that a trial's recipe never dirties the tree. A recipe launched from outside
        /// rounds/nursery/ runs as an unregistered trial. Plain characters only (A-Z a-z 0-9 . _ - and /), never rounds/nursery or inside it.
        /// </summary>
        public string TrialsDir;

        private static readonly Regex PlainFolder = new Regex(@"\A[A-Za-z0-9_-]([A-Za-z0-9._-]*[A-Za-z0-9_-])?(/[A-Za-z0-9_-]([A-Za-z0-9._-]*[A-Za-z0-9_-])?)*\z");

        private static readonly string[] Keys = { "port", "bind", "pollSeconds", "capLogical", "runs", "dotnet", "nurseryDll", "runsDir", "activityFile", "affinity", "trialsDir" };

        public static RoomSettings Load(string path)
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new InvalidDataException($"room settings: {path} cannot be read ({e.Message})", e);
            }
            return Parse(text, path);
        }

        public static RoomSettings Parse(string text, string where = "room.json")
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
            catch (JsonException e) { throw new InvalidDataException($"room settings: {where} is not JSON ({e.Message})", e); }
            using (doc)
            {
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"room settings: {where} is a JSON object");
                string[] have = root.EnumerateObject().Select(p => p.Name).ToArray();
                string[] unknown = have.Where(k => !Keys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
                string[] missing = Keys.Where(k => !have.Contains(k)).ToArray();
                if (unknown.Length > 0) throw new InvalidDataException($"room settings: {where} has unknown {string.Join(", ", unknown)}");
                if (missing.Length > 0) throw new InvalidDataException($"room settings: {where} is missing {string.Join(", ", missing)}");

                var s = new RoomSettings
                {
                    Port = Whole(root, "port", where),
                    Bind = Text(root, "bind", where),
                    PollSeconds = Whole(root, "pollSeconds", where),
                    CapLogical = Whole(root, "capLogical", where),
                    Dotnet = Text(root, "dotnet", where),
                    NurseryDll = Text(root, "nurseryDll", where),
                    RunsDir = Text(root, "runsDir", where),
                    ActivityFile = Text(root, "activityFile", where),
                    Affinity = CoreMask.Parse(Text(root, "affinity", where), "affinity", where),
                    TrialsDir = Text(root, "trialsDir", where),
                };
                if (string.IsNullOrWhiteSpace(s.Dotnet)) throw new InvalidDataException($"room settings: {where}: dotnet names the dotnet host a run is started with");
                if (string.IsNullOrWhiteSpace(s.NurseryDll)) throw new InvalidDataException($"room settings: {where}: nurseryDll names the nursery's program");
                s.RunsDir = RepoRelative(s.RunsDir, "runsDir", where);
                s.ActivityFile = RepoRelative(s.ActivityFile, "activityFile", where);
                s.TrialsDir = RepoRelative(s.TrialsDir, "trialsDir", where);
                if (!PlainFolder.IsMatch(s.TrialsDir))
                    throw new InvalidDataException($"room settings: {where}: trialsDir is a folder named with A-Z a-z 0-9 . _ - only (and / between folders), not \"{s.TrialsDir.Replace("\"", "'")}\"");
                if (s.TrialsDir.Equals("rounds/nursery", StringComparison.OrdinalIgnoreCase) || s.TrialsDir.StartsWith("rounds/nursery/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"room settings: {where}: trialsDir is {s.TrialsDir}, which is the registered recipes' own folder or inside it; a trial's folder is another one (scratch/nursery-trials)");
                string trialsProblem = TrialsFolderProblem(s.TrialsDir, s.RunsDir);
                if (trialsProblem != null) throw new InvalidDataException($"room settings: {where}: trialsDir is {s.TrialsDir}, {trialsProblem}");
                if (s.Port < 1 || s.Port > 65535) throw new InvalidDataException($"room settings: {where}: port is 1 to 65535, not {s.Port}");
                if (s.PollSeconds < 1) throw new InvalidDataException($"room settings: {where}: pollSeconds is 1 or more");
                if (s.CapLogical < 1) throw new InvalidDataException($"room settings: {where}: capLogical is 1 or more");
                if (!System.Net.IPAddress.TryParse(s.Bind, out var ip) || !System.Net.IPAddress.IsLoopback(ip))
                    throw new InvalidDataException($"room settings: {where}: bind is a loopback address (127.0.0.1), not \"{s.Bind}\": the room listens on this machine only");
                JsonElement runs = root.GetProperty("runs");
                if (runs.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"room settings: {where}: runs is an array of folders");
                foreach (JsonElement r in runs.EnumerateArray())
                {
                    string folder, config = null;
                    if (r.ValueKind == JsonValueKind.Object)
                    {
                        string[] entryUnknown = r.EnumerateObject().Select(p => p.Name).Where(k => k != "folder" && k != "config").OrderBy(k => k, StringComparer.Ordinal).ToArray();
                        if (entryUnknown.Length > 0)
                            throw new InvalidDataException($"room settings: {where}: a runs entry has unknown {string.Join(", ", entryUnknown)}; an entry is a folder name, or an object with folder and, optionally, config");
                        if (!r.TryGetProperty("folder", out JsonElement f) || f.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(f.GetString()))
                            throw new InvalidDataException($"room settings: {where}: a runs entry that is an object names its folder, relative to the repository root");
                        folder = f.GetString();
                        if (r.TryGetProperty("config", out JsonElement c))
                        {
                            if (c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString()))
                                throw new InvalidDataException($"room settings: {where}: the config of the runs entry {folder} is the path of a config file, relative to the repository root");
                            config = RepoRelative(c.GetString(), "the config of the runs entry " + folder, where);
                        }
                    }
                    else if (r.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(r.GetString())) folder = r.GetString();
                    else throw new InvalidDataException($"room settings: {where}: runs holds folder names, relative to the repository root, or objects with a folder and, optionally, a config");
                    if (Path.IsPathRooted(folder) || folder.Contains(':'))
                        throw new InvalidDataException($"room settings: {where}: runs holds {folder}; folders are relative to the repository root");
                    string key = folder.Replace('\\', '/').TrimEnd('/');
                    s.Runs.Add(key);
                    if (config != null) s.RunConfigs[key] = config;
                }
                return s;
            }
        }

        /// <summary>
        /// Why a trials folder is refused, or null: it lies under scratch/ (the gitignored folder, so a trial's recipe never dirties the tree), not under
        /// scratch/wt- (another worktree's tree), and it is not the runs folder, inside it or holding it (a run's folder is made empty and a recipe
        /// written among run folders would be taken for one). Compared by whole folder names, without regard to case.
        /// </summary>
        public static string TrialsFolderProblem(string trialsDir, string runsDir)
        {
            string t = trialsDir.ToLowerInvariant(), r = (runsDir ?? "").ToLowerInvariant();
            if (!t.StartsWith("scratch/", StringComparison.Ordinal))
                return "which is not under scratch/, the repository's gitignored folder; a trial's folder is one of its folders (scratch/nursery-trials)";
            if (t.StartsWith("scratch/wt-", StringComparison.Ordinal))
                return "which is under scratch/wt-, the folders of other worktrees; a trial's folder is not another tree's (scratch/nursery-trials)";
            if (t == r) return $"which is the runs folder (runsDir {runsDir}); a trial's recipe is not written among run folders";
            if (t.StartsWith(r + "/", StringComparison.Ordinal)) return $"which is inside the runs folder (runsDir {runsDir}); a trial's recipe is not written among run folders";
            if (r.StartsWith(t + "/", StringComparison.Ordinal)) return $"which holds the runs folder (runsDir {runsDir}); a run's folder is not made inside the trials folder";
            return null;
        }

        /// <summary>A folder or file inside the repository: relative, forward slashes, never climbing out, so no row or message names a path outside it.</summary>
        private static string RepoRelative(string path, string key, string where)
        {
            string p = (path ?? "").Replace('\\', '/').Trim().TrimEnd('/');
            if (p.Length == 0 || Path.IsPathRooted(p) || p.Contains(':') || p.Split('/').Contains(".."))
                throw new InvalidDataException($"room settings: {where}: {key} is a path inside the repository, relative to its root, not \"{path}\"");
            return p;
        }

        private static int Whole(JsonElement o, string key, string where)
        {
            JsonElement v = o.GetProperty(key);
            if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out int i)) throw new InvalidDataException($"room settings: {where}: {key} is a whole number");
            return i;
        }

        private static string Text(JsonElement o, string key, string where)
        {
            JsonElement v = o.GetProperty(key);
            if (v.ValueKind != JsonValueKind.String) throw new InvalidDataException($"room settings: {where}: {key} is a string");
            return v.GetString();
        }
    }
}
