using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Nursery;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// What the room stands on: the repository root it was started in, the main checkout (where the one
    /// registry lives, whichever worktree a run is launched from), its settings, and the live run readers.
    /// </summary>
    public sealed class RoomContext
    {
        public string Root { get; }
        public string MainCheckout { get; }
        public RoomSettings Settings { get; }
        public string RegistryFile { get; }
        public RunStore Runs { get; }

        /// <summary>The activity log, in the main checkout (found as the registry is).</summary>
        public ActivityLog Activity { get; }

        /// <summary>Starts runs. The room's tests give it a fake, so that nothing swims.</summary>
        public IRunLauncher Launcher { get; }

        /// <summary>Reads the machine's queue (room.json's queueFile, in the main checkout), for the room's slot. The room's tests give it a value.</summary>
        public IQueueReader Queue { get; }

        /// <summary>Reads the heat check (room.json's heatScript). The room's tests give it a value and never run the script.</summary>
        public IHeatReader Heat { get; }

        /// <summary>
        /// The processors the machine has, as the system's mask (<see cref="SystemProcessors"/>, read once when the room starts): the one source of
        /// <see cref="Logical"/>, of what the operations check an affinity mask against and of what the launcher checks it against.
        /// </summary>
        public ulong SystemMask { get; }

        /// <summary>The logical processors the machine has: the processors <see cref="SystemMask"/> names.</summary>
        public int Logical { get; }

        /// <summary>The build as a rerun compares it with an entry's: the commit, the dirty flag and the four tree hashes of the tree at the root. The tests give it a fixed record.</summary>
        public Func<string, RunRecord> ReadBuild { get; }

        /// <summary>Asks whether a process the room started still lives and ends one (an episode re-step). The room's tests give it a fake, so that nothing runs.</summary>
        public IProcessControl Processes { get; }

        /// <summary>Copies a file by content into an episode's cache folder. The room's tests give it a fake that damages the copy.</summary>
        public IEpisodeFileCopier Copier { get; }

        /// <summary>
        /// Held while a re-step starts or is stopped, and while the live ones are counted, so that the count of granted threads and a stop never see a step half made.
        /// Taken after the operations' own lock and never before it.
        /// </summary>
        public object StepGate { get; } = new object();

        /// <summary>One run this room started: its folder name, the folder (relative to the main checkout, as an activity row records it), the threads granted and when.</summary>
        public sealed class LaunchedRun
        {
            public string Name, Folder;
            /// <summary>The recipe the run was started on, repository-relative (a rerun's is its entry's recipe), or null when the row that recorded it did not say.</summary>
            public string Recipe;
            public int Threads;
            public DateTime StartedUtc;
            /// <summary>True for a run started on a recipe outside rounds/nursery/: it runs unregistered, as a trial.</summary>
            public bool Trial;
        }

        /// <summary>
        /// Runs this room started since it began, by folder (relative to the main checkout, as a row names it), so that the registry screen shows a run
        /// while it runs. Keyed by folder and not by name: two rooms in two trees may launch the same name, and they are two runs.
        /// </summary>
        private readonly ConcurrentDictionary<string, LaunchedRun> _launched = new ConcurrentDictionary<string, LaunchedRun>(StringComparer.OrdinalIgnoreCase);

        // The activity log parsed for its launches, kept while the file's length and write time are as they were.
        private readonly object _logGate = new object();
        private long _logLength = -1;
        private DateTime _logWritten;
        private List<LaunchedRun> _logLaunches = new List<LaunchedRun>();

        public RoomContext(string root, RoomSettings settings, string mainCheckout = null, string registryFile = null,
            string activityFile = null, IRunLauncher launcher = null, IQueueReader queue = null, IHeatReader heat = null, int? logical = null, Func<string, RunRecord> readBuild = null,
            ulong? systemMask = null, IProcessControl processes = null, IEpisodeFileCopier copier = null)
        {
            Processes = processes ?? new SystemProcessControl();
            Copier = copier ?? new ByContentCopier();
            Root = Path.GetFullPath(root);
            Settings = settings;
            MainCheckout = mainCheckout ?? RunRecord.MainCheckoutOf(Root);
            RegistryFile = registryFile ?? RegistryEntry.FileUnder(MainCheckout);
            Activity = new ActivityLog(activityFile ?? Path.GetFullPath(Path.Combine(MainCheckout, settings.ActivityFile.Replace('/', Path.DirectorySeparatorChar))));
            // The processors the machine has: given (a test's fake), or the first logical processors a count names (a test's 32), or the system's.
            // The room's own launcher reads the very same value, so that the operations and the launcher cannot disagree about the machine.
            SystemMask = systemMask ?? (logical.HasValue ? (logical.Value >= 64 ? ulong.MaxValue : (1UL << logical.Value) - 1UL) : SystemProcessors.Read());
            Logical = CoreMask.Count(SystemMask);
            if (logical.HasValue && logical.Value != Logical)
                throw new ArgumentException($"the logical processor count {logical.Value} is not the {Logical} processors the system mask {CoreMask.Format(SystemMask)} names");
            Launcher = launcher ?? new ProcessRunLauncher(() => SystemMask);
            Queue = queue ?? new QueueFileReader(Path.GetFullPath(Path.Combine(MainCheckout, settings.QueueFile.Replace('/', Path.DirectorySeparatorChar))), settings.QueueFile);
            Heat = heat ?? new ScriptHeatReader(ResolveFile(settings.HeatScript, out _) ?? Path.Combine(Root, settings.HeatScript), settings.HeatScript);
            ReadBuild = readBuild ?? (r => { var rec = new RunRecord(); rec.ReadBuild(r); return rec; });
            Runs = new RunStore();
        }

        /// <summary>The folder a launched run of this name is made in: under the settings' runsDir, in this root.</summary>
        public string LaunchFolder(string name) => Path.GetFullPath(Path.Combine(Root, Settings.RunsDir.Replace('/', Path.DirectorySeparatorChar), name));

        /// <summary>
        /// This tree's path relative to the main checkout, forward slashes, <c>.</c> for the main checkout itself: what an activity row
        /// carries as <c>tree</c> so that a row from a worktree room says which tree it was made in.
        /// </summary>
        public string TreeRelative
        {
            get
            {
                string rel = Path.GetRelativePath(MainCheckout, Root).Replace('\\', '/');
                if (rel == "" || rel == ".") return ".";
                return rel.StartsWith("..") || Path.IsPathRooted(rel) ? "(a tree outside the main checkout)" : rel;
            }
        }

        /// <summary>
        /// A folder as an activity row names it: relative to the main checkout, forward slashes, whichever tree the room runs in, so that every
        /// reader resolves it the same way (<see cref="ResolveFromMain"/>). A path outside the main checkout is a fixed note, never the path.
        /// </summary>
        public string MainRelative(string full)
        {
            if (string.IsNullOrEmpty(full)) return full;
            string abs;
            try { abs = Path.GetFullPath(full); } catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException) { return "(a path outside the repository)"; }
            string b = Path.GetFullPath(MainCheckout).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return abs.StartsWith(b, StringComparison.OrdinalIgnoreCase) ? abs.Substring(b.Length).Replace('\\', '/') : "(a path outside the repository)";
        }

        /// <summary>The folder an activity row names (relative to the main checkout), or null when it is not a plain relative path that stays inside it.</summary>
        public string ResolveFromMain(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':')) return null;
            string b = Path.GetFullPath(MainCheckout).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(b, relative.Replace('/', Path.DirectorySeparatorChar)));
            return full.StartsWith(b, StringComparison.OrdinalIgnoreCase) ? full : null;
        }

        /// <summary>Remembers a run this room started (its folder relative to the main checkout, the threads it was granted).</summary>
        public void NoteLaunched(string name, string mainRelativeFolder, int threads, DateTime startedUtc, string recipe = null, bool trial = false) =>
            _launched[mainRelativeFolder] = new LaunchedRun { Name = name, Folder = mainRelativeFolder, Recipe = recipe, Threads = threads, StartedUtc = startedUtc, Trial = trial };

        /// <summary>
        /// The runs this room started: those since it began, and those its activity log records as launched or rerun (so that a restart does
        /// not lose them). The log is parsed again only when its length or write time changed.
        /// </summary>
        public List<LaunchedRun> LaunchedRuns()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var l = new List<LaunchedRun>();
            try
            {
                foreach (LaunchedRun r in LaunchesInLog()) if (seen.Add(r.Folder)) l.Add(r);
            }
            catch (IOException) { }
            foreach (LaunchedRun r in _launched.Values) if (seen.Add(r.Folder)) l.Add(r);
            return l;
        }

        private List<LaunchedRun> LaunchesInLog()
        {
            var info = new FileInfo(Activity.Path);
            if (!info.Exists) return new List<LaunchedRun>();
            lock (_logGate)
            {
                if (info.Length == _logLength && info.LastWriteTimeUtc == _logWritten) return _logLaunches;
                var found = new List<LaunchedRun>();
                foreach (System.Text.Json.JsonElement row in Activity.ReadAll())
                {
                    string op = Js.Str(row, "operation");
                    if ((op != "run.launch" && op != "run.rerun") || Js.Str(row, "outcome") != "ok") continue;
                    string name = Js.Str(row, "args", "name"), folder = Js.Str(row, "result", "folder");
                    if (name == null || folder == null) continue;
                    DateTime started = DateTime.TryParse(Js.Str(row, "timeUtc"), System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime t) ? t : DateTime.MinValue;
                    found.Add(new LaunchedRun { Name = name, Folder = folder, Recipe = Js.Str(row, "result", "recipe"), Threads = (int)(Js.Num(row, "args", "threads") ?? 0), StartedUtc = started, Trial = Js.Bool(row, "result", "trial") == true });
                }
                _logLength = info.Length; _logWritten = info.LastWriteTimeUtc; _logLaunches = found;
                return found;
            }
        }

        /// <summary>True when this room started the run in <paramref name="folder"/> (a full path).</summary>
        public bool IsLaunchedFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return false;
            string want = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return LaunchedRuns().Any(r =>
            {
                string f = ResolveFromMain(r.Folder);
                return f != null && string.Equals(f.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), want, StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// The threads the room itself has granted that count against its free cores now: those of the runs it started that are still running
        /// (their record says running and the process lives) or began in the last <see cref="Cap.RecentLaunch"/>,
        /// and one thread for each episode re-step whose process lives, for exactly as long as it lives (<see cref="EpisodeSteps.LiveThreads"/>).
        /// </summary>
        public int GrantedThreads(DateTime nowUtc)
        {
            int sum = EpisodeSteps.LiveThreads(this);
            foreach (LaunchedRun r in LaunchedRuns())
            {
                if (r.Threads <= 0) continue;
                if (nowUtc - r.StartedUtc < Cap.RecentLaunch) { sum += r.Threads; continue; }
                string folder = ResolveFromMain(r.Folder);
                if (folder == null || !Directory.Exists(folder)) continue;
                RunFolder f = Runs.Get(folder);
                f.Refresh(1.0);
                if (Js.Str(f.RunRecord, "status") == "running" && RunFolder.ProcessAlive(f.RunRecord) != false) sum += r.Threads;
            }
            return sum;
        }

        /// <summary>The room's slot as the queue gives it now, inside room.json's affinity (<see cref="Cap.SlotOf"/>).</summary>
        public Slot ReadSlot() => Cap.SlotOf(Queue.Read(), Settings.Affinity);

        /// <summary>The first folder at or above <paramref name="from"/> that holds src/Evosim.Core, or null.</summary>
        public static string FindRoot(string from) => Recipe.RepoRootAbove(from);

        /// <summary>
        /// A folder a setting or an entry names: relative to the repository root, or to the main
        /// checkout when the run's files are not in this worktree (scratch/ is gitignored, so a run made
        /// in the main checkout is not in a worktree's copy). An absolute path is taken as it is.
        /// </summary>
        public string ResolveFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            if (Path.IsPathRooted(folder)) return Path.GetFullPath(folder);
            string rel = folder.Replace('/', Path.DirectorySeparatorChar);
            string here = Path.GetFullPath(Path.Combine(Root, rel));
            if (Directory.Exists(here) || string.Equals(Root, MainCheckout, StringComparison.OrdinalIgnoreCase)) return here;
            string there = Path.GetFullPath(Path.Combine(MainCheckout, rel));
            return Directory.Exists(there) ? there : here;
        }

        /// <summary>
        /// A path as a screen shows it: relative to the repository root where it lies under it (this
        /// worktree's, else the main checkout's), forward slashes; anything else as it is. The full path
        /// goes in a title beside it.
        /// </summary>
        /// <summary>
        /// A path as an activity row or a result records it: repository-relative where it lies under the root, else a fixed
        /// note, so that a row never holds a path outside the repository.
        /// </summary>
        public string RelativeOrOutside(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (!Path.IsPathRooted(path) && !path.Contains(':')) return path.Replace('\\', '/');
            string shown = Display(path);
            return Path.IsPathRooted(shown) || shown.Contains(':') ? "(a path outside the repository)" : shown;
        }

        /// <summary>A message with the repository's own folders taken out of every path in it (the nursery names files in full), and the check copy's name as the file's.</summary>
        public string Scrub(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;
            foreach (string dir in new[] { Root, MainCheckout })
            {
                if (string.IsNullOrEmpty(dir)) continue;
                string b = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                message = message.Replace(b + Path.DirectorySeparatorChar, "", StringComparison.OrdinalIgnoreCase)
                                 .Replace(b.Replace('\\', '/') + "/", "", StringComparison.OrdinalIgnoreCase);
            }
            return System.Text.RegularExpressions.Regex.Replace(message.Replace(".json.writing", ".json"), @"\.json\.[0-9a-f]{8}\.writing", ".json");
        }

        public string Display(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string full;
            try { full = Path.GetFullPath(path); } catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException) { return path; }
            foreach (string baseDir in new[] { Root, MainCheckout })
            {
                if (string.IsNullOrEmpty(baseDir)) continue;
                string b = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (full.StartsWith(b, StringComparison.OrdinalIgnoreCase)) return full.Substring(b.Length).Replace('\\', '/');
            }
            return path;
        }

        /// <summary>
        /// Why the room does not write at <paramref name="full"/>, or null: some folder or file that exists on the way from the root down to it is a
        /// reparse point (a junction or a symbolic link), so that a path that looks inside the repository would land somewhere else. Parts that do not
        /// exist yet are not checked (nothing is there to be a link); the root itself and what lies above it are the owner's. The one check every write
        /// and every folder the room makes goes through (<see cref="EnsureDirectory"/>); a part that cannot be inspected is a refusal too.
        /// </summary>
        public string LinkProblem(string full)
        {
            if (string.IsNullOrEmpty(full)) return null;
            string target;
            try { target = Path.GetFullPath(full); }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException) { return "the path cannot be read as a path, so whether a link lies on it cannot be checked"; }
            string rel = Path.GetRelativePath(Root, target);
            if (rel == "." || rel == "") return null;
            if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel)) return "a path outside the repository, so whether a link lies on it cannot be checked";
            string at = Root;
            string shown = "";
            foreach (string part in rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
            {
                at = Path.Combine(at, part);
                shown = shown.Length == 0 ? part : shown + "/" + part;
                FileAttributes attributes;
                try { attributes = File.GetAttributes(at); }
                catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException) { return null; }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return $"{shown} cannot be inspected ({Scrub(e.Message)}), so whether it is a link cannot be checked"; }
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    return $"{shown} is a link (a reparse point: a junction or a symbolic link), and the room writes nothing through one";
            }
            return null;
        }

        /// <summary>Makes a folder the room writes into: refused if a link lies on its way (checked before and again after, in case one was put there in between).</summary>
        public void EnsureDirectory(string full)
        {
            string before = LinkProblem(full);
            if (before != null) throw new ArgumentException(before + ". Nothing was written.");
            Directory.CreateDirectory(full);
            string after = LinkProblem(full);
            if (after != null) throw new ArgumentException(after + ". Nothing more was written.");
        }

        /// <summary>
        /// A path inside the repository, from a relative path a caller gave. Refuses a rooted path, a
        /// drive and anything that climbs out of the root, so no operation or resource reads past it.
        /// </summary>
        public string ResolveInRepo(string relative, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(relative)) { problem = "a path is required"; return null; }
            if (Path.IsPathRooted(relative) || relative.Contains(':')) { problem = $"{relative} is not relative; paths are relative to the repository root"; return null; }
            string full = Path.GetFullPath(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
            string rootWithSep = Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)) { problem = $"{relative} leaves the repository"; return null; }
            return full;
        }

        /// <summary>
        /// A file a setting names, relative to the repository root, found as <see cref="ResolveFolder"/> finds a run's folder: in this tree, else in the
        /// main checkout (scratch/ is gitignored, so a worktree does not hold the main checkout's runs or their configs). The same path guard on both:
        /// never rooted, never a drive, never out of the tree it is looked up in. The path is returned whether or not the file exists.
        /// </summary>
        public string ResolveFile(string relative, out string problem)
        {
            string here = ResolveInRepo(relative, out problem);
            if (here == null || File.Exists(here) || string.Equals(Root, MainCheckout, StringComparison.OrdinalIgnoreCase)) return here;
            string full = Path.GetFullPath(Path.Combine(MainCheckout, relative.Replace('/', Path.DirectorySeparatorChar)));
            string mainWithSep = Path.GetFullPath(MainCheckout).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(mainWithSep, StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : here;
        }
    }

    /// <summary>One <see cref="RunFolder"/> per folder, kept for the life of the server so its readers keep their offsets.</summary>
    public sealed class RunStore
    {
        private readonly ConcurrentDictionary<string, RunFolder> _folders = new ConcurrentDictionary<string, RunFolder>(StringComparer.OrdinalIgnoreCase);

        public RunFolder Get(string folder) => _folders.GetOrAdd(Path.GetFullPath(folder), p => new RunFolder(p));
    }
}
