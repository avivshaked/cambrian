using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Evosim.Nursery;

namespace Evosim.NurseryRoom
{
    public sealed class ResourceNotFound : Exception { public ResourceNotFound(string m) : base(m) { } }
    public sealed class ResourceRefused : Exception { public ResourceRefused(string m) : base(m) { } }

    // Properties, not fields: Js.Out writes properties only, so a public field would be written as nothing (the recipe list was empty for that).
    public sealed class ResourceTemplateInfo
    {
        public string UriTemplate { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public sealed class ResourceInfo
    {
        public string Uri { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Reads are resources, each with an address. This is the one resolver the JSON API and the MCP
    /// both call, so a screen and an agent read the same thing.
    /// </summary>
    public sealed class ResourceResolver
    {
        public const string Scheme = "nursery://";

        private readonly RoomContext _room;

        /// <summary>A reader asks for fresh bytes at most this often, so a screen and an agent polling together do not each hit the disk.</summary>
        private const double MinReadAgeSeconds = 1.0;

        public ResourceResolver(RoomContext room) { _room = room; }

        public IReadOnlyList<ResourceTemplateInfo> Templates { get; } = new[]
        {
            new ResourceTemplateInfo { UriTemplate = "nursery://run/{id}", Name = "run", Description = "One run's files parsed: its record, recipe, generation rows, scores, exam table, breeding totals and log tail. The id is a registry number or the folder name of a run listed in room.json." },
            new ResourceTemplateInfo { UriTemplate = "nursery://recipe/{path}", Name = "recipe", Description = "A recipe resolved against its base: its canonical text and hash, what differs from the base, or the refusal with the field's path; whether the file is committed (gitState: committed, changed, untracked, outside or unreadable, with the last commit and git's own message); a plain-word label for every field (labels). The path is relative to the repository root." },
            new ResourceTemplateInfo { UriTemplate = "nursery://genomes/{run}", Name = "genomes", Description = "The genomes a run holds: each generation's top genome (tank-best.jsonl) and the exam's genomes, with the id each goes by, what the run's files could not say about it, and whether the run wrote a lineage file. The run is a registry number or a folder name." },
            new ResourceTemplateInfo { UriTemplate = "nursery://genome/{run}/{id}", Name = "genome", Description = "One genome of a run, computed with Core's own code: its body as development grows it (shape, extents, position, orientation, parent, joint and limits per part), its brain (ports grouped by part and sense, hidden units, links with weight and sign; a port naming nothing in the grown body is listed as such), its lineage row, and what changed from its parent when the run holds the parent's genome. The id is a member id (g<generation>.<index>), best.g<generation>, a bench's pool index (p<index>), or <name>.<index> (exam.<index> or bench.<index>: the genome's line in the run's genomes file); the list resource says which a run answers to." },
            new ResourceTemplateInfo { UriTemplate = "nursery://exam-plan/{run}", Name = "exam-plan", Description = "What a run's exam swims: its brains in index order (label, kind, the control each applies), its genomes in pool order (id, body), the episode seeds as strings, each draw's tanks as lists of pool indices in slot order, and where each input came from. Read from run.json's exam plan and the draws file when the run wrote them; for a run made before those existed, rebuilt from its registry entry's recipe (its hash checked against the entry), its pool file and the nursery's own TankLog.PlanTanks. Refused, with why, when it cannot be rebuilt. The run is a registry number or a folder name." },
            new ResourceTemplateInfo { UriTemplate = "nursery://activity/{run}", Name = "run-activity", Description = "The activity log's rows that are about one run, newest first: its launch, its rerun (or the rerun that made it), its stops and the episodes stepped on it, each with who asked, when and why. The run is a registry number or a folder name." },
            new ResourceTemplateInfo { UriTemplate = "nursery://exam-compare/{run}/vs/{control}", Name = "exam-compare", Description = "A run's exam set beside another run's, brain by brain, matched by label: each run's pooled paired difference (own brain less that brain) with its standard error, their difference with standard error sqrt(se1^2 + se2^2), and whether it is more than two of those. The two exams must be on one footing (both on net or both on score), or none is marked, with why. The run and the control are registry numbers or folder names." },
            new ResourceTemplateInfo { UriTemplate = "nursery://episode/{run}/{b}/{d}/{t}", Name = "episode", Description = "One exam episode re-stepped by episode.step: its state (none, running with the size of its stream, stopped with why, failed with the tail of its log, or done), and when done the comparison of each member's re-stepped net with the run's own per[d] for that brain, bit for bit, with the verdict EXACT (every member equal), DIFFERS (naming the slots) or UNCOMPARED (and why), and its provenance: the episode's build hashes next to the run's, and whether the plan was the run's record or rebuilt. The run is a registry number or a folder name; b is the exam brain's index, d the draw, t the tank in that draw." },
        };

        /// <summary>The concrete resources a client can list: the registry, the activity feed, every known run, every recipe under rounds/nursery.</summary>
        public IReadOnlyList<ResourceInfo> List()
        {
            var l = new List<ResourceInfo>
            {
                new ResourceInfo { Uri = "nursery://registry", Name = "registry", Description = "Every registered experiment, and the runs room.json adds, newest first." },
                new ResourceInfo { Uri = "nursery://activity", Name = "activity", Description = "What the room's operations that change something did, newest first: when, which, with what arguments, who asked (screen or mcp), why, and how it ended." },
            };
            foreach (RunRef r in Catalog(out _))
            {
                l.Add(new ResourceInfo { Uri = Scheme + "run/" + r.Id, Name = "run " + r.Id, Description = r.Kind == "entry" ? $"Registry entry {r.Id}: {r.Name}" : $"Run folder {r.Id}" });
                l.Add(new ResourceInfo { Uri = Scheme + "genomes/" + r.Id, Name = "genomes of " + r.Id, Description = "The genomes run " + r.Id + " holds: each generation's top genome and the exam's." });
                l.Add(new ResourceInfo { Uri = Scheme + "exam-plan/" + r.Id, Name = "exam plan of " + r.Id, Description = "What run " + r.Id + "'s exam swims: brains, genomes, seeds and tanks, and where each came from; refused, with why, when it cannot be rebuilt." });
            }
            // The episodes the cache holds (stepped by episode.step), each readable as it stands.
            try { foreach ((string uri, string name) in EpisodeSteps.Cached(_room)) l.Add(new ResourceInfo { Uri = uri, Name = name, Description = "An exam episode re-stepped by episode.step: its state and, when done, its comparison with the run's own numbers." }); }
            catch (IOException) { }
            string dir = Path.Combine(_room.Root, "rounds", "nursery");
            if (Directory.Exists(dir))
                foreach (string f in Directory.GetFiles(dir, "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileName(f);
                    if (name.Equals("room.json", StringComparison.OrdinalIgnoreCase)) continue;
                    l.Add(new ResourceInfo { Uri = Scheme + "recipe/rounds/nursery/" + name, Name = "recipe " + name, Description = "A recipe file under rounds/nursery." });
                }
            // The trials folder's recipes too: written by recipe.write, launched as unregistered trials.
            string trials = Path.Combine(_room.Root, _room.Settings.TrialsDir.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(trials))
                foreach (string f in Directory.GetFiles(trials, "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileName(f);
                    l.Add(new ResourceInfo { Uri = Scheme + "recipe/" + _room.Settings.TrialsDir + "/" + name, Name = "recipe " + _room.Settings.TrialsDir + "/" + name, Description = "A trial recipe under " + _room.Settings.TrialsDir + ": launched as an unregistered trial." });
                }
            return l;
        }

        public object Read(string uri)
        {
            if (uri == null || !uri.StartsWith(Scheme, StringComparison.Ordinal)) throw new ResourceRefused($"\"{uri}\" is not a nursery:// address");
            string rest = uri.Substring(Scheme.Length);
            int slash = rest.IndexOf('/');
            string head = slash < 0 ? rest : rest.Substring(0, slash);
            string tail = slash < 0 ? "" : Uri.UnescapeDataString(rest.Substring(slash + 1));
            switch (head)
            {
                case "registry": return Registry();
                case "activity": return tail.Length == 0 ? Activity() : ActivityOf(tail);
                case "run":
                    if (tail.Length == 0) throw new ResourceRefused("nursery://run/{id} needs an id");
                    return Run(tail);
                case "recipe":
                    if (tail.Length == 0) throw new ResourceRefused("nursery://recipe/{path} needs a path");
                    return RecipeView(tail);
                case "genomes":
                    if (tail.Length == 0) throw new ResourceRefused("nursery://genomes/{run} needs a run");
                    return GenomeList(tail);
                case "genome":
                {
                    // The run is a registry number or a folder name, which may hold slashes; a genome's id never does.
                    int cut = tail.LastIndexOf('/');
                    if (cut <= 0 || cut == tail.Length - 1) throw new ResourceRefused("nursery://genome/{run}/{id} needs a run and a genome id");
                    return GenomeRead(tail.Substring(0, cut), tail.Substring(cut + 1));
                }
                case "exam-plan":
                    if (tail.Length == 0) throw new ResourceRefused("nursery://exam-plan/{run} needs a run");
                    return ExamPlanRead(tail);
                case "exam-compare":
                {
                    int cut = tail.IndexOf("/vs/", StringComparison.Ordinal);
                    if (cut <= 0 || cut + 4 >= tail.Length) throw new ResourceRefused("nursery://exam-compare/{run}/vs/{control} needs a run and its control");
                    return ExamCompareRead(tail.Substring(0, cut), tail.Substring(cut + 4));
                }
                case "episode":
                    if (!EpisodeSteps.TryParseAddress(tail, out string episodeRun, out int eb, out int ed, out int et))
                        throw new ResourceRefused("nursery://episode/{run}/{b}/{d}/{t} needs a run and the exam brain's index, the draw and the tank, each a whole number");
                    return EpisodeSteps.Read(_room, this, episodeRun, eb, ed, et);
                default: throw new ResourceNotFound($"no resource \"{head}\"; the resources are registry, activity, run/{{id}}, recipe/{{path}}, genomes/{{run}}, genome/{{run}}/{{id}}, exam-plan/{{run}} and episode/{{run}}/{{b}}/{{d}}/{{t}}");
            }
        }

        // ------------------------------------------------------------------ the catalogue of runs

        public sealed class RunRef
        {
            public string Id, Kind, Folder, Name;
            /// <summary>The folder's own name where room.json also lists this entry's folder: one folder is one row, and it still answers to this name.</summary>
            public string FolderName;
            /// <summary>The recipe the run was made on (repository-relative), where the registry entry or the activity row says; null when nothing does.</summary>
            public string RecipePath;
            public int? Number;
            /// <summary>A run listed in room.json: the config file the entry names for it (repository-relative), or null.</summary>
            public string ConfigPath;
            /// <summary>True for a run this room started, by the activity log or since it began.</summary>
            public bool Launched;
            /// <summary>True for a run this room started on a recipe outside rounds/nursery/: it runs unregistered, as a trial.</summary>
            public bool Trial;
            public RegistryEntry Entry;
        }

        /// <summary>Every run the room knows: registry entries by number, then room.json's runs by folder name.</summary>
        public List<RunRef> Catalog(out string problem)
        {
            problem = null;
            var l = new List<RunRef>();
            try
            {
                int n = 0;
                foreach (RegistryEntry e in RegistryEntry.ReadAll(_room.RegistryFile))
                {
                    n++;
                    l.Add(new RunRef { Id = n.ToString(CultureInfo.InvariantCulture), Kind = "entry", Number = n, Entry = e, Name = e.Recipe, Folder = _room.ResolveFolder(e.Out), RecipePath = e.RecipePath });
                }
            }
            catch (InvalidDataException e) { problem = e.Message; }
            foreach (string rel in _room.Settings.Runs)
            {
                string folderName = rel.Substring(rel.LastIndexOf('/') + 1);
                string folder = _room.ResolveFolder(rel);
                string cfg = _room.Settings.RunConfigs.TryGetValue(rel, out string c) ? c : null;
                // One folder is one run: a room.json run whose folder a registry entry made is that entry (the latest, if two did), which then also answers to the folder's name
                // and takes the config room.json names for it. Two rows for one folder were two states for it.
                RunRef twin = folder == null ? null : l.LastOrDefault(r => r.Kind == "entry" && r.Folder != null && SameFolder(r.Folder, folder));
                if (twin != null) { twin.FolderName = folderName; twin.ConfigPath = twin.ConfigPath ?? cfg; continue; }
                l.Add(new RunRef { Id = folderName, Kind = "run", Name = folderName, Folder = folder, ConfigPath = cfg });
            }
            // The runs this room started: shown while they run, before the nursery has appended its registry line. A run
            // already listed (by its registry entry or by room.json) under the same folder is not listed twice.
            var known = new HashSet<string>(l.Where(r => r.Folder != null).Select(r => Path.GetFullPath(r.Folder)), StringComparer.OrdinalIgnoreCase);
            foreach (RoomContext.LaunchedRun launched in _room.LaunchedRuns())
            {
                // An activity row names its folder relative to the main checkout, whichever tree the room that launched it ran in.
                string folder = _room.ResolveFromMain(launched.Folder);
                if (folder == null) continue;
                if (!known.Add(Path.GetFullPath(folder)))
                {
                    // Already listed (room.json names the folder): it keeps what the activity log says of how it was launched, a trial's flag included.
                    if (launched.Trial)
                        foreach (RunRef listed in l.Where(r => r.Kind == "run" && r.Folder != null && string.Equals(Path.GetFullPath(r.Folder), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)))
                            listed.Trial = true;
                    continue;
                }
                // Two runs of one name (launched by two rooms in two trees) are two runs: the later one is listed by its folder, which no other has.
                string id = l.Any(r => r.Kind == "run" && string.Equals(r.Id, launched.Name, StringComparison.OrdinalIgnoreCase)) ? launched.Folder : launched.Name;
                l.Add(new RunRef { Id = id, Kind = "run", Name = launched.Name, Folder = folder, Launched = true, Trial = launched.Trial, RecipePath = launched.Recipe });
            }
            return l;
        }

        private static bool SameFolder(string a, string b) =>
            string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

        private static string FolderLeaf(string folder) => Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        public RunRef FindRun(string id) => FindIn(Catalog(out _), id);

        /// <summary>The run an address names, among these (a registry number, a name, a folder name: see <see cref="FindRun"/>); throws <see cref="ResourceNotFound"/> when none.</summary>
        public static RunRef FindIn(List<RunRef> all, string id)
        {
            string last = id.Substring(id.LastIndexOf('/') + 1);
            // Every entry answers to its number and to the name of its folder, whether or not room.json also lists that folder (a folder two entries made is the later one's).
            bool EntryFolderIs(RunRef r, string name) => r.Kind == "entry" && r.Folder != null && string.Equals(FolderLeaf(r.Folder), name, StringComparison.OrdinalIgnoreCase);
            RunRef hit = all.FirstOrDefault(r => r.Kind == "entry" && r.Id == id)
                ?? all.FirstOrDefault(r => r.Kind == "run" && string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(r => r.FolderName != null && string.Equals(r.FolderName, id, StringComparison.OrdinalIgnoreCase))
                ?? all.LastOrDefault(r => EntryFolderIs(r, id))
                ?? all.FirstOrDefault(r => r.Kind == "run" && string.Equals(r.Id, last, StringComparison.OrdinalIgnoreCase) && id.Contains('/'))
                ?? all.FirstOrDefault(r => r.FolderName != null && string.Equals(r.FolderName, last, StringComparison.OrdinalIgnoreCase) && id.Contains('/'))
                ?? (id.Contains('/') ? all.LastOrDefault(r => EntryFolderIs(r, last)) : null);
            if (hit == null) throw new ResourceNotFound($"no run \"{id}\": a run is a registry number or the folder name of a run in room.json (" + string.Join(", ", all.Select(r => r.Id)) + ")");
            return hit;
        }

        // ------------------------------------------------------------------ run

        public object Run(string id)
        {
            RunRef r = FindRun(id);
            var d = new Dictionary<string, object>
            {
                ["id"] = r.Id,
                ["kind"] = r.Kind,
                ["trial"] = r.Trial,
                ["number"] = r.Number,
                ["entry"] = r.Entry == null ? null : EntryObject(r.Entry),
            };
            if (!Directory.Exists(r.Folder))
            {
                d["folder"] = _room.RelativeOrOutside(r.Folder);
                d["folderShort"] = _room.Display(r.Folder);
                d["exists"] = false;
                d["state"] = new { phase = r.Entry?.Status ?? "missing", recorded = r.Entry?.Status };
                d["problem"] = $"the run folder {_room.RelativeOrOutside(r.Folder)} does not exist on this machine";
                d["version"] = "missing";
                d["nowUtc"] = DateTime.UtcNow;
                d["name"] = r.Name;
                d["folderName"] = r.FolderName;
                d["recipePath"] = r.RecipePath;
                return d;
            }
            RunFolder f = _room.Runs.Get(r.Folder);
            f.Refresh(MinReadAgeSeconds);
            foreach (var kv in f.Snapshot(DateTime.UtcNow, full: true, controlsFallback: f.Recipe.HasValue ? null : EntryControls(r))) d[kv.Key] = kv.Value;
            // A folder with no run.json that a registry entry made has a state: the entry's. The registry row says it, so the run page does not say "not recorded".
            string snapPhase = (string)GetMember(d["state"], "phase");
            if (r.Entry != null && (snapPhase == "unrecorded" || snapPhase == "empty"))
            {
                d["state"] = new
                {
                    recorded = r.Entry.Status, phase = r.Entry.Status, fromRegistry = true,
                    note = r.Entry.Status + ", from the registry; this run predates run.json",
                    processAlive = (bool?)null, processId = (double?)null, startedUtc = r.Entry.StartedAtUtc, endedUtc = r.Entry.EndedAtUtc, error = (string)null, threads = (double?)null,
                };
                d["version"] = d["version"] + "|registry:" + r.Entry.Status;
            }
            d["uninformative"] = d.TryGetValue("exam", out object examObject) ? UninformativeColumns(r, examObject) : null;
            d["exists"] = true;
            d["folderName"] = r.FolderName;
            d["recipePath"] = r.RecipePath;
            d["recipeExists"] = r.RecipePath != null && _room.ResolveInRepo(r.RecipePath, out _) is string recipeFull && File.Exists(recipeFull);
            // The snapshot names its folder in full; a resource says it as the activity log does, relative to the repository root.
            d["folder"] = _room.RelativeOrOutside(r.Folder);
            d["folderShort"] = _room.Display(r.Folder);
            if (r.Entry != null) d["name"] = r.Entry.Recipe;
            return d;
        }

        /// <summary>
        /// The exam columns that leave a genome exactly as it is (<see cref="ExamReach"/>), for the exam table to grey: per genome, the columns whose control cuts
        /// nothing from it. Null when the run holds no exam, no genome can be read, or no control is one of the two kinds that can cut nothing.
        /// </summary>
        private object UninformativeColumns(RunRef r, object exam)
        {
            if (!(GetMember(exam, "headers") is IReadOnlyList<ExamHeader> headers)) return null;
            try
            {
                GenomeSet set = GenomesOf(r);
                var texts = new List<KeyValuePair<int, string>>();
                foreach (HeldGenome g in set.Held)
                    if (g.ExamIndex.HasValue && g.Problem == null && g.Text != null) texts.Add(new KeyValuePair<int, string>(g.ExamIndex.Value, g.Text));
                return ExamReach.Build(headers, texts, set.Config());
            }
            catch (ResourceNotFound) { return null; }
            catch (IOException) { return null; }
        }

        // ------------------------------------------------------------------ genomes

        private readonly object _genomeGate = new object();
        private readonly Dictionary<string, GenomeSet> _genomeSets = new Dictionary<string, GenomeSet>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The run's genomes, read from its files and kept until one of them changes. A set that could not read one of its files (one the nursery holds
        /// open without sharing, a file it may not open) is returned but never kept: its absent file is not the run's having none.
        /// </summary>
        private GenomeSet GenomesOf(RunRef r)
        {
            if (r.Folder == null || !Directory.Exists(r.Folder)) throw new ResourceNotFound($"the run folder of {r.Id} does not exist on this machine, so it holds no genomes to read");
            GenomeSet.RoomConfig roomConfig = null;
            if (r.ConfigPath != null)
            {
                string full = _room.ResolveFile(r.ConfigPath, out string problem);
                roomConfig = new GenomeSet.RoomConfig { Path = r.ConfigPath, Full = full, Problem = problem };
            }
            string sig = GenomeSet.SignatureOf(r.Folder, roomConfig?.Full);
            lock (_genomeGate)
            {
                if (_genomeSets.TryGetValue(r.Folder, out GenomeSet kept) && kept.Signature == sig) return kept;
                GenomeSet fresh = GenomeSet.Load(r.Folder, roomConfig);
                if (fresh.Incomplete) _genomeSets.Remove(r.Folder);
                else _genomeSets[r.Folder] = fresh;
                return fresh;
            }
        }

        public object GenomeList(string runId)
        {
            RunRef r = FindRun(runId);
            return GenomeViews.List(r, GenomesOf(r), _room.RelativeOrOutside(r.Folder));
        }

        /// <summary>
        /// One genome's text exactly as the run's file stores it (a line of tank-best.jsonl, or the part of a line of the exam's genomes file after its tab),
        /// and the name a download of it goes by. A read: the same lookup as <c>nursery://genome/{run}/{id}</c>, and nothing is written.
        /// </summary>
        public (string fileName, string text) GenomeText(string runId, string genomeId)
        {
            RunRef r = FindRun(runId);
            GenomeSet set = GenomesOf(r);
            HeldGenome held = set.Find(genomeId);
            if (held == null) throw new ResourceNotFound($"run {r.Id} holds no genome \"{genomeId}\"");
            string safe = new string((r.Id + "-" + held.Id).Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '_').ToArray());
            return (safe + ".json", held.Text);
        }

        public object GenomeRead(string runId, string genomeId)
        {
            RunRef r = FindRun(runId);
            return GenomeViews.View(r, GenomesOf(r), genomeId);
        }

        /// <summary>The plan of a run's exam (<see cref="ExamPlans"/>). A read: nothing is written and nothing swims.</summary>
        public object ExamPlanRead(string runId)
        {
            RunRef r = FindRun(runId);
            return ExamPlans.Build(_room, r, Catalog(out _));
        }

        private object EntryObject(RegistryEntry e) => new
        {
            recipe = e.Recipe, recipeHash = e.RecipeHash, recipePath = e.RecipePath, configHash = e.ConfigHash,
            gitCommit = e.GitCommit, coreHash = e.CoreHash, dynamicsHash = e.DynamicsHash, farmHash = e.FarmHash, nurseryHash = e.NurseryHash,
            @out = _room.RelativeOrOutside(e.Out), status = e.Status, startedAtUtc = e.StartedAtUtc, endedAtUtc = e.EndedAtUtc,
            rerunOf = e.RerunOf, identical = e.Identical, rerunnable = e.Rerunnable, notRerunnableBecause = e.NotRerunnableBecause,
            reference = e.Reference == null ? null : new { build = e.Reference.Build, verdict = e.Reference.Verdict, knownDifferences = e.Reference.KnownDifferences },
            digests = e.Digests,
        };

        // ------------------------------------------------------------------ registry

        public object Registry()
        {
            DateTime now = DateTime.UtcNow;
            List<RunRef> all = Catalog(out string problem);
            var items = new List<(DateTime start, int number, object item)>();
            foreach (RunRef r in all)
            {
                Dictionary<string, object> snap = null;
                RunFolder.RegistryLine line = null;
                bool exists = r.Folder != null && Directory.Exists(r.Folder);
                if (exists)
                {
                    RunFolder f = _room.Runs.Get(r.Folder);
                    f.Refresh(MinReadAgeSeconds);
                    snap = f.Snapshot(now, full: false);
                    line = f.RegistrySummary();
                }
                object state = snap?["state"];
                string phase = r.Entry != null ? r.Entry.Status : (string)GetMember(state, "phase") ?? "missing";
                string started = r.Entry?.StartedAtUtc ?? (string)GetMember(state, "startedUtc");
                DateTime start = ParseUtc(started) ?? (exists ? Directory.GetCreationTimeUtc(r.Folder) : DateTime.MinValue);
                var item = new
                {
                    id = r.Id,
                    kind = r.Kind,
                    launched = r.Launched,
                    trial = r.Trial,
                    number = r.Number,
                    name = r.Name,
                    folderName = r.FolderName,
                    // The recipe the run was made on, where the registry entry or the activity row says, and whether this tree holds the file (the screens link to it only then).
                    recipePath = r.RecipePath,
                    recipeExists = r.RecipePath != null && _room.ResolveInRepo(r.RecipePath, out _) is string recipeFull && File.Exists(recipeFull),
                    // the recipe's base (the file it is a variation of), where this tree holds the recipe and it names one; the screens link to it only when this tree holds it too
                    basePath = BaseOf(r.RecipePath, out bool baseExists),
                    baseExists,
                    description = snap?["description"],
                    phase,
                    recorded = (string)GetMember(state, "recorded"),
                    folder = _room.RelativeOrOutside(r.Folder),
                    folderShort = _room.Display(r.Folder),
                    exists,
                    startedUtc = start == DateTime.MinValue ? null : start.ToString("o", CultureInfo.InvariantCulture),
                    endedUtc = r.Entry?.EndedAtUtc ?? (string)GetMember(state, "endedUtc"),
                    progress = snap?["progress"],
                    reward = snap?["reward"],
                    best = line?.Best,
                    measure = line?.Measure,
                    measureLabel = line?.Label,
                    series = line?.Series,
                    recipeHash = r.Entry?.RecipeHash ?? (string)snap?["recipeHash"],
                    gitCommit = r.Entry?.GitCommit,
                    rerunOf = r.Entry != null && r.Entry.RerunOf > 0 ? r.Entry.RerunOf : (int?)null,
                    identical = r.Entry?.Identical,
                    rerunnable = r.Entry?.Rerunnable,
                    notRerunnableBecause = r.Entry?.NotRerunnableBecause,
                    reference = r.Entry?.Reference == null ? null : new { build = r.Entry.Reference.Build, verdict = r.Entry.Reference.Verdict },
                    version = snap?["version"],
                };
                items.Add((start, r.Number ?? 0, item));
            }
            var sorted = items.OrderByDescending(x => x.start).ThenByDescending(x => x.number).Select(x => x.item).ToList();
            return new
            {
                nowUtc = now,
                file = _room.RelativeOrOutside(_room.RegistryFile),
                fileShort = _room.Display(_room.RegistryFile),
                fileExists = File.Exists(_room.RegistryFile),
                problem,
                entries = all.Count(r => r.Kind == "entry"),
                runs = all.Count(r => r.Kind == "run"),
                items = sorted,
                version = string.Join("|", sorted.Select(i => GetMember(i, "version") ?? GetMember(i, "id"))),
            };
        }

        private static object GetMember(object o, string name) => o?.GetType().GetProperty(name)?.GetValue(o);

        private static DateTime? ParseUtc(string s) =>
            s != null && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime d) ? d : (DateTime?)null;

        // ------------------------------------------------------------------ recipe

        /// <summary>
        /// What a launch of this recipe file would do, as the launch decides it (<see cref="RecipePlace.LaunchProblem"/>, the plain name and no link on the way):
        /// <c>registered</c> (the nursery enters the run in the registry), <c>trial</c> (it runs unregistered) or <c>refused</c> with the reason.
        /// The screens enable the launch that fits and none for a refused recipe.
        /// </summary>
        private object LaunchState(string full, string relative)
        {
            string rel = Path.GetRelativePath(_room.Root, full).Replace('\\', '/');
            string problem = OperationList.RawPathProblem(relative);
            bool trial = false;
            if (problem != null) problem = problem + "; no run would be started";
            else problem = RecipePlace.LaunchProblem(rel, out trial);
            if (problem == null)
            {
                string link = _room.LinkProblem(full);
                if (link != null) problem = $"{rel} is not launched: {link}";
            }
            if (problem != null) return new { state = "refused", reason = problem };
            return new { state = trial ? "trial" : "registered", reason = (string)null };
        }

        private static bool IsTrialState(object launch) => (string)GetMember(launch, "state") == "trial";

        public object RecipeView(string relative)
        {
            string full = _room.ResolveInRepo(relative, out string problem);
            if (full == null) throw new ResourceRefused(problem);
            if (!File.Exists(full)) throw new ResourceNotFound($"no recipe at {relative}");
            JsonElement? raw = null;
            string rawProblem = null, text = null, fileSha = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(full);
                fileSha = ActivityLog.Hex(System.Security.Cryptography.SHA256.HashData(bytes));
                text = new UTF8Encoding(false).GetString(bytes);
                raw = Js.Parse(text);
            }
            catch (Exception e) when (e is JsonException || e is IOException || e is UnauthorizedAccessException) { rawProblem = e.Message; }
            string baseRel = Js.Str(raw, "base");
            object launch = LaunchState(full, relative);
            // Whether the file is committed (a pre-registered run launches only from a committed recipe): git read-only, in this root, each command with a timeout.
            RecipeGitState gitState = ReadGitState(full);
            IReadOnlyDictionary<string, string> labels = RecipeLabels.Table;
            try
            {
                // Read, not Load: a recipe whose world this build cannot load is still shown, with what it holds, and the world's refusal is its check.
                Recipe r = Recipe.Read(full);
                string worldProblem = r.WorldProblem();
                if (worldProblem != null)
                    launch = new { state = "refused", reason = $"WORLD NOT LOADED: this build cannot load the world {r.ConfigPath} ({GenomeSet.Flat(worldProblem)})" };
                string canonical = r.Canonical();
                JsonElement resolved = Js.Parse(canonical);
                object baseView = null;
                if (baseRel != null)
                {
                    string baseFull = _room.ResolveInRepo(baseRel, out _);
                    if (baseFull != null && File.Exists(baseFull))
                    {
                        Recipe b = Recipe.Read(baseFull);
                        // What differs from the base is the recipe.diff operation's answer, and only its: the screen asks for it.
                        baseView = new { path = baseRel, name = b.Name, hash = b.Hash() };
                    }
                }
                object world = new { loaded = worldProblem == null, problem = worldProblem, config = r.ConfigPath };
                object check = worldProblem == null ? new { ok = true } : OperationList.RefusalOf(relative, worldProblem);
                return new { path = relative, ok = worldProblem == null, trial = IsTrialState(launch), launch, gitState, labels, name = r.Name, hash = r.Hash(), fileSha256 = fileSha, text, canonical, resolved, @base = baseView, check, world };
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                object refusal = OperationList.RefusalOf(relative, e.Message);
                return new { path = relative, ok = false, trial = IsTrialState(launch), launch, gitState, labels, name = (string)null, hash = (string)null, fileSha256 = fileSha, text, canonical = (string)null, resolved = (JsonElement?)null, @base = (object)null, check = refusal, raw, rawProblem, world = (object)null };
            }
        }

        /// <summary>Whether the recipe file is committed (<see cref="RecipeGit"/>), asked in this room's repository root. Settable so that a test can give git a fake.</summary>
        public GitRunner GitForRecipes { get; set; }

        private RecipeGitState ReadGitState(string full) =>
            RecipeGit.Read(_room.Root, Path.GetRelativePath(_room.Root, full).Replace('\\', '/'), GitForRecipes);

        /// <summary>The <c>base</c> a recipe file names (repository-relative), or null when the file is not here, does not parse or names none. <paramref name="exists"/>: this tree holds the base file too.</summary>
        private string BaseOf(string recipePath, out bool exists)
        {
            exists = false;
            if (recipePath == null) return null;
            string full = _room.ResolveInRepo(recipePath, out _);
            if (full == null || !File.Exists(full)) return null;
            string b;
            try { b = Js.Str(Js.Parse(File.ReadAllText(full)), "base"); }
            catch (Exception e) when (e is JsonException || e is IOException || e is UnauthorizedAccessException) { return null; }
            if (b == null) return null;
            string baseFull = _room.ResolveInRepo(b, out _);
            exists = baseFull != null && File.Exists(baseFull);
            return b;
        }

        /// <summary>
        /// The exam controls of the recipe the registry names for a run that holds no recipe of its own, when that file is here, loads, and is the recipe the entry
        /// recorded (its hash agrees); null otherwise. They give a column's heading the channel of a knockout the exam file labelled bare.
        /// </summary>
        private IReadOnlyList<JsonElement> EntryControls(RunRef r)
        {
            if (r.Entry == null || string.IsNullOrEmpty(r.RecipePath)) return null;
            string full = _room.ResolveInRepo(r.RecipePath, out _);
            if (full == null || !File.Exists(full)) return null;
            try
            {
                Recipe recipe = Recipe.Read(full);   // Read: the headings need the controls, not a world this build can load
                if (!string.IsNullOrEmpty(r.Entry.RecipeHash) && !string.Equals(recipe.Hash(), r.Entry.RecipeHash, StringComparison.OrdinalIgnoreCase)) return null;
                return Js.Items(Js.At(Js.Parse(recipe.Canonical()), "exam", "controls")).ToList();
            }
            catch (Exception e) when (!(e is OutOfMemoryException)) { return null; }
        }

        /// <summary>A run's exam set beside another's (<see cref="ExamCompare"/>): a read, nothing is written.</summary>
        public object ExamCompareRead(string runId, string controlId)
        {
            List<RunRef> all = Catalog(out _);
            RunRef run = FindIn(all, runId), control = FindIn(all, controlId);
            if (ReferenceEquals(run, control)) throw new ResourceRefused($"run {run.Id} cannot be its own control");
            ExamTable.Result a = ExamOf(run), b = ExamOf(control);
            string missing = a == null ? $"run {run.Id} has no exam rows" : b == null ? $"the control, run {control.Id}, has no exam rows" : null;
            return new { ok = true, run = run.Id, control = control.Id, available = missing == null, reason = missing, comparison = missing == null ? ExamCompare.Build(a, b) : null };
        }

        private ExamTable.Result ExamOf(RunRef r)
        {
            if (r.Folder == null || !Directory.Exists(r.Folder)) return null;
            RunFolder f = _room.Runs.Get(r.Folder);
            f.Refresh(MinReadAgeSeconds);
            return f.ExamResult();
        }

        // ------------------------------------------------------------------ activity

        /// <summary>The activity log, newest first: the latest 300 rows, and how many the file holds.</summary>
        public object Activity()
        {
            List<JsonElement> rows = _room.Activity.ReadNewestFirst(300, out int total, out int unreadable);
            return new
            {
                nowUtc = DateTime.UtcNow,
                file = _room.Display(_room.Activity.Path),
                fileExists = File.Exists(_room.Activity.Path),
                total,
                unreadable,
                shown = rows.Count,
                entries = rows,
            };
        }

        /// <summary>The activity log's rows about one run (<see cref="ActivityFilter"/>), newest first, as <see cref="Activity"/> gives them.</summary>
        public object ActivityOf(string runId)
        {
            List<RunRef> all = Catalog(out _);
            RunRef run = FindIn(all, runId);
            string folderFull = run.Folder == null ? null : Path.GetFullPath(run.Folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool IsRun(string s) { try { return ReferenceEquals(FindIn(all, s), run); } catch (ResourceNotFound) { return false; } }
            bool IsFolder(string s)
            {
                string full = _room.ResolveFromMain(s);
                return full != null && folderFull != null && string.Equals(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), folderFull, StringComparison.OrdinalIgnoreCase);
            }
            List<JsonElement> rows = _room.Activity.ReadNewestFirst(int.MaxValue, out int total, out int unreadable);
            List<JsonElement> mine = ActivityFilter.ForRun(rows, IsRun, IsFolder);
            return new
            {
                nowUtc = DateTime.UtcNow,
                run = run.Id,
                file = _room.Display(_room.Activity.Path),
                fileExists = File.Exists(_room.Activity.Path),
                total,
                unreadable,
                shown = mine.Count,
                entries = mine,
            };
        }

        /// <summary>Every leaf of a recipe by dotted path, each as compact JSON; an array is one leaf.</summary>
        public static Dictionary<string, string> Flatten(JsonElement e)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            void Walk(JsonElement x, string at)
            {
                if (x.ValueKind == JsonValueKind.Object && x.EnumerateObject().Any())
                    foreach (JsonProperty p in x.EnumerateObject()) Walk(p.Value, at.Length == 0 ? p.Name : at + "." + p.Name);
                else d[at] = x.GetRawText();
            }
            Walk(e, "");
            return d;
        }
    }
}
