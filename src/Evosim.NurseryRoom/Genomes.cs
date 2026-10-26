using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Evosim.Core;
using Evosim.Nursery;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// One genome a run holds: where its text came from, and the names the run's files give it.
    /// </summary>
    /// <remarks>
    /// <b>Names.</b> A member id (<c>g&lt;generation&gt;.&lt;index&gt;</c>, <see cref="TankLog.MemberId"/>) is used wherever the run's files tie a
    /// genome to one. <c>tank-best.jsonl</c> holds one genome a line, in generation order, and writes no id beside it. The id is recovered only when
    /// <c>tank-scores.jsonl</c> and <c>tank-generations.jsonl</c> agree on which member was the top of that generation (see <see cref="GenomeSet"/>).
    /// A top genome that cannot be tied is named <c>best.g&lt;generation&gt;</c>, never an id it might not have. An exam genome is named by the
    /// id the exam plan in <c>run.json</c> gives it (a member id of the search the exam followed, or <c>p&lt;index&gt;</c> for a bench of a pool, which has no
    /// search); an id that is not a plain name (one holding a separator, or <c>..</c>) is not used. With no plan, or no id for a line, it is
    /// <c>&lt;name&gt;.&lt;index&gt;</c>, its line in the genomes file, where the name is the plan's (<c>exam</c>, <c>bench</c>) or, with no plan, whichever
    /// of the two files the run has. Every exam genome also answers to <c>&lt;name&gt;.&lt;index&gt;</c>, the index the exam table's rows carry.
    /// When the plan names a genome by an id that a tied top genome of a different text also has, the recorded id wins: the plan's genome keeps it and
    /// the top is named <c>best.g&lt;generation&gt;</c>.
    /// </remarks>
    public sealed class HeldGenome
    {
        public string Id;
        public readonly List<string> Aliases = new List<string>();
        /// <summary><c>top</c> (a generation's top genome), <c>exam</c> (a genome the exam swam), or both.</summary>
        public readonly List<string> Kinds = new List<string>();
        public int? Generation;
        public int? ExamIndex;
        public string ExamName;
        public int? Body, Born, Episodes;
        public double? Score;
        /// <summary>The member id the run's files tie this genome to, or null.</summary>
        public string MemberId;
        /// <summary>Why a top genome has no member id, or how it got one.</summary>
        public string IdNote;
        public string Text;
        public string Source;
        public int Nodes, Hidden, Links;
        public bool Central;
        /// <summary>Set when the genome's line is not JSON at all: what the list can say of it is nothing, and the view says why.</summary>
        public string Problem;
    }

    /// <summary>One row of <c>tank-lineage.jsonl</c>.</summary>
    public sealed class LineageRow
    {
        public int Gen, Body, Born;
        public string Id;
        public string[] Parents;
        public bool Elite;
    }

    /// <summary>What reading a run's file gave: its non-empty lines, or that it is not there, or why it could not be read. Exactly one of the three.</summary>
    public sealed class FileLines
    {
        public List<string> Lines;
        public bool Absent;
        public string Problem;
    }

    /// <summary>Where the config a run's genomes grow under came from, and whether this build took it.</summary>
    public sealed class ConfigInfo
    {
        /// <summary><c>run.json</c>, <c>room.json</c> or <c>unknown</c>: where the config was found. For a config this build refuses it is where it was found, and <see cref="Loaded"/> is false.</summary>
        public string Source = "unknown";
        /// <summary>For <c>room.json</c>, the config file the entry names (repository-relative); otherwise null.</summary>
        public string File;
        public bool Loaded;
        public string Refusal;
        public string Note;
        public RunConfig Config;
    }

    /// <summary>
    /// What one run's folder holds as genomes, read from its files. Built on demand and cached by the files' sizes and write times
    /// (<see cref="Signature"/>); nothing here swims and nothing is written. A file that could not be read is not the same as a file that is not
    /// there: the set says so (<see cref="Incomplete"/>, the notes) and is never kept.
    /// </summary>
    public sealed class GenomeSet
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private static readonly Regex MemberIdRe = new Regex(@"^g(\d+)\.(\d+)$", RegexOptions.CultureInvariant);
        /// <summary>A genome id the room will serve: a plain name that holds no separator and is neither "." nor "..".</summary>
        private static readonly Regex PlainIdRe = new Regex(@"\A[A-Za-z0-9_][A-Za-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant);
        /// <summary>The exam's name as a file name holds it: the plan's name is a part of a file name in the run's folder, never a path.</summary>
        private static readonly Regex PlainNameRe = new Regex(@"\A[A-Za-z0-9_-][A-Za-z0-9._-]{0,31}\z", RegexOptions.CultureInvariant);

        public const string BestFile = "tank-best.jsonl";
        public const string ScoresFile = "tank-scores.jsonl";
        public const string GenerationsFile = "tank-generations.jsonl";

        private const string Fallback = "the body and ports shown are grown under Core's defaults, which may differ from what the run used";

        /// <summary>The config a room.json entry names for a run, as the room resolved it.</summary>
        public sealed class RoomConfig
        {
            /// <summary>The path as room.json gives it, relative to the repository root: the only form of it that is ever shown.</summary>
            public string Path;
            /// <summary>The file it resolves to, or where it would be.</summary>
            public string Full;
            /// <summary>Why the path is refused as a path (it climbs out of the tree), or null.</summary>
            public string Problem;
        }

        public string Folder { get; private set; }
        public readonly List<HeldGenome> Held = new List<HeldGenome>();
        public readonly List<string> Notes = new List<string>();
        public string Signature { get; private set; }
        public JsonElement? Run { get; private set; }

        /// <summary>True when a file the set needs could not be read (as distinct from being absent): the set is shown, but never kept.</summary>
        public bool Incomplete { get; private set; }

        /// <summary>The signature, as a client sees it: it changes when the files do, and when the read was incomplete.</summary>
        public string Version => Signature + (Incomplete ? "|unread" : "");

        private readonly Dictionary<string, HeldGenome> _byName = new Dictionary<string, HeldGenome>(StringComparer.Ordinal);
        private readonly HashSet<string> _unreadable = new HashSet<string>(StringComparer.Ordinal);
        private RoomConfig _roomConfig;
        private Dictionary<string, LineageRow> _lineage;
        private bool _lineageRead;
        private string _lineageProblem;
        private readonly List<string> _lineageNotes = new List<string>();
        private ConfigInfo _config;

        /// <summary>The files this set depends on, as one string that changes when any of them does.</summary>
        public static string SignatureOf(string folder, string roomConfigFile = null)
        {
            var sb = new StringBuilder();
            void Add(string label, string p)
            {
                try
                {
                    var fi = new FileInfo(p);
                    sb.Append(label).Append(':').Append(fi.Exists ? fi.Length.ToString(CultureInfo.InvariantCulture) + "@" + fi.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) : "-").Append('|');
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { sb.Append(label).Append(":?|"); }
            }
            foreach (string name in new[] { "run.json", BestFile, GenerationsFile, ScoresFile, TankLog.LineageFile }) Add(name, Path.Combine(folder, name));
            // Every genomes file the run holds, whatever the exam plan calls the exam: the file the plan names is among them.
            try
            {
                foreach (string f in Directory.GetFiles(folder, "tank-*-genomes.tsv").OrderBy(x => x, StringComparer.Ordinal)) Add(Path.GetFileName(f), f);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { sb.Append("genomes-files:?|"); }
            if (roomConfigFile != null) Add("config", roomConfigFile);
            return sb.ToString();
        }

        /// <summary>A message with the file's full path and its folder taken out, so that nothing the room says names this machine's folders.</summary>
        private static string Scrub(string message, string path)
        {
            string m = message ?? "";
            try
            {
                m = m.Replace(path, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) m = m.Replace(dir + Path.DirectorySeparatorChar, "", StringComparison.OrdinalIgnoreCase).Replace(dir, "", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException) { }
            return Flat(m);
        }

        /// <summary>A message on one line: line breaks become "; " and a carriage return is dropped, so that every issue Core lists is kept.</summary>
        public static string Flat(string message) =>
            string.Join("; ", (message ?? "").Replace("\r", "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0));

        /// <summary>
        /// A file that a running nursery may be appending to: its non-empty lines; or that it is absent; or why it could not be read (a sharing violation, a
        /// permission). The three are not the same thing, and only the first two are ever a statement about the run.
        /// </summary>
        public static FileLines ReadFile(string path)
        {
            try
            {
                var l = new List<string>();
                using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var r = new StreamReader(s, Utf8))
                {
                    string line;
                    while ((line = r.ReadLine()) != null) if (line.Length > 0) l.Add(line);
                }
                return new FileLines { Lines = l };
            }
            catch (FileNotFoundException) { return new FileLines { Absent = true }; }
            catch (DirectoryNotFoundException) { return new FileLines { Absent = true }; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return new FileLines { Problem = Scrub(e.Message, path) }; }
        }

        /// <summary>Every line of a file, or null when the file is not there or could not be read (which <see cref="ReadFile"/> tells apart).</summary>
        public static List<string> ReadLines(string path) => ReadFile(path).Lines;

        /// <summary>A file read for this set: a failure to read is recorded in the notes and makes the set incomplete.</summary>
        private FileLines Read(string name, string path)
        {
            FileLines f = ReadFile(path);
            if (f.Problem != null)
            {
                _unreadable.Add(name);
                Incomplete = true;
                Notes.Add($"{name} could not be read ({f.Problem}): what it holds is not shown, which is not the run having none of it; it is read again on the next request");
            }
            return f;
        }

        /// <summary>
        /// The rows of a JSON-lines file, or of a genomes file (a body index, a tab, a genome's JSON). A last line that does not parse is a row still being
        /// written: it is left out, and the notes say so. Null when the file is absent or could not be read.
        /// </summary>
        private List<string> Rows(string name, bool tsv)
        {
            List<string> l = Read(name, Path.Combine(Folder, name)).Lines;
            if (l == null || l.Count == 0) return l;
            string last = l[l.Count - 1];
            bool whole;
            try
            {
                string json = last;
                if (tsv)
                {
                    int tab = last.IndexOf('\t');
                    json = tab < 0 ? null : last.Substring(tab + 1);
                }
                whole = json != null;
                if (whole) using (JsonDocument.Parse(json)) { }
            }
            catch (JsonException) { whole = false; }
            if (!whole)
            {
                l.RemoveAt(l.Count - 1);
                Notes.Add($"the last line of {name} does not parse: it is taken for a row still being written, and left out");
            }
            return l;
        }

        public static GenomeSet Load(string folder, RoomConfig roomConfig = null)
        {
            var set = new GenomeSet { Folder = folder, _roomConfig = roomConfig, Signature = SignatureOf(folder, roomConfig?.Full) };
            FileLines run = set.Read("run.json", Path.Combine(folder, "run.json"));
            if (run.Lines != null)
            {
                try { set.Run = Js.Parse(string.Join("\n", run.Lines)); }
                catch (JsonException e) { set.Notes.Add("run.json does not parse (" + Flat(e.Message) + "), so the exam plan and the run's config are not read from it"); }
            }
            set.ReadTops();
            set.ReadExam();
            foreach (HeldGenome g in set.Held)
            {
                set._byName[g.Id] = g;
                foreach (string a in g.Aliases) set._byName.TryAdd(a, g);
            }
            return set;
        }

        public HeldGenome Find(string name) => name != null && _byName.TryGetValue(name, out HeldGenome g) ? g : null;

        public IEnumerable<string> Names => _byName.Keys;

        // ------------------------------------------------------------------ the top genome of each generation

        private void ReadTops()
        {
            List<string> best = Rows(BestFile, false);
            if (best == null)
            {
                if (!_unreadable.Contains(BestFile)) Notes.Add("this run has no " + BestFile + ": no generation's top genome is kept");
                return;
            }
            List<string> gens = Rows(GenerationsFile, false);
            List<string> scores = Rows(ScoresFile, false);
            bool scoresAbsent = scores == null && !_unreadable.Contains(ScoresFile);
            if (scoresAbsent) Notes.Add("no " + ScoresFile + ": a top genome's member id cannot be recovered, so each is named best.g<generation>");

            for (int i = 0; i < best.Count; i++)
            {
                var g = new HeldGenome { Generation = i, Text = best[i], Source = BestFile + " line " + (i + 1) };
                g.Kinds.Add("top");
                Stats(g);
                g.Id = "best.g" + i.ToString(CultureInfo.InvariantCulture);
                // The generation's row says what its top was; the scores row says which member that was.
                JsonElement? top = TopRow(gens, i, out string whyNoTop);
                if (top.HasValue)
                {
                    g.Score = Js.Num(top, "score");
                    g.Body = Js.Num(top, "body") is double b ? (int)b : (int?)null;
                    g.Born = Js.Num(top, "born") is double bo ? (int)bo : (int?)null;
                    g.Episodes = Js.Num(top, "episodes") is double ep ? (int)ep : (int?)null;
                }
                string why = null;
                string tied = null;
                if (!top.HasValue) why = whyNoTop;
                else if (scores == null) why = scoresAbsent ? ScoresFile + " is absent" : ScoresFile + " could not be read";
                else if (i >= scores.Count) why = ScoresFile + " has no row for generation " + i;
                else tied = TopMember(scores[i], i, top.Value, out why);
                if (tied != null)
                {
                    g.MemberId = tied;
                    g.IdNote = "tied to member " + tied + ": the first member of tank-scores.jsonl's generation " + i + " with the highest value, whose body, born, episodes and score are the generation row's top";
                    g.Aliases.Add(g.Id);
                    g.Id = tied;
                }
                else g.IdNote = "not tied to a member id: " + why;
                Held.Add(g);
            }
        }

        /// <summary>The <c>top</c> of generation <paramref name="i"/>'s row in <c>tank-generations.jsonl</c>, or null with the reason.</summary>
        private JsonElement? TopRow(List<string> gens, int i, out string why)
        {
            why = null;
            if (gens == null) { why = GenerationsFile + (_unreadable.Contains(GenerationsFile) ? " could not be read" : " is absent"); return null; }
            if (i >= gens.Count) { why = GenerationsFile + " has no row for generation " + i; return null; }
            JsonElement row;
            try { row = Js.Parse(gens[i]); }
            catch (JsonException) { why = "the generation row for generation " + i + " does not parse"; return null; }
            // A generation row carries its own number; it must be this line's.
            double? gn = Js.Num(row, "gen");
            if (gn.HasValue && (int)gn.Value != i) { why = "the generation row at line " + (i + 1) + " is for generation " + (int)gn.Value; return null; }
            JsonElement? top = Js.Get(row, "top");
            if (!top.HasValue || top.Value.ValueKind != JsonValueKind.Object) { why = "the generation row for generation " + i + " has no top"; return null; }
            return top;
        }

        /// <summary>
        /// The member id of a generation's top, from <c>tank-scores.jsonl</c>'s row (members in the population's order, so the member's index is
        /// the id's index): the search ranks by score with a stable descending sort, so the top is the first member with the highest value, and
        /// it is accepted only when that member's body, born and episodes and its value are the generation row's <c>top</c>. Where it is not, the reason
        /// is the real one: no scores row, a row of another generation, no member with a finite value, a highest value several members share whose first
        /// is not the top, or a highest-valued member that is not the top.
        /// </summary>
        private static string TopMember(string scoresLine, int gen, JsonElement top, out string why)
        {
            why = null;
            JsonElement row;
            try { row = Js.Parse(scoresLine); }
            catch (JsonException) { why = "the scores row for generation " + gen + " does not parse"; return null; }
            double? rg = Js.Num(row, "gen");
            if (rg.HasValue && (int)rg.Value != gen) { why = "the scores row at line " + (gen + 1) + " is for generation " + (int)rg.Value; return null; }
            List<JsonElement> members = Js.Items(Js.Get(row, "members")).ToList();
            if (members.Count == 0) { why = "the scores row for generation " + gen + " lists no member"; return null; }
            int bestIndex = -1;
            double bestValue = double.NegativeInfinity;
            for (int k = 0; k < members.Count; k++)
            {
                double v = Js.Num(members[k], "value") ?? double.NegativeInfinity;
                if (bestIndex < 0 || v > bestValue) { bestIndex = k; bestValue = v; }
            }
            if (!double.IsFinite(bestValue)) { why = "no member of generation " + gen + "'s scores row has a finite value, so none is strictly highest"; return null; }
            double? score = Js.Num(top, "score");
            bool Matches(JsonElement m, double value) =>
                Js.Num(m, "body") == Js.Num(top, "body") && Js.Num(m, "born") == Js.Num(top, "born") && Js.Num(m, "episodes") == Js.Num(top, "episodes")
                && score.HasValue && Math.Abs(value - score.Value) <= 1e-6 * Math.Max(1.0, Math.Abs(score.Value));
            if (Matches(members[bestIndex], bestValue)) return TankLog.MemberId(gen, bestIndex);
            var sharing = Enumerable.Range(0, members.Count).Where(k => k != bestIndex && (Js.Num(members[k], "value") ?? double.NegativeInfinity) == bestValue).ToList();
            if (sharing.Count > 0 && sharing.Any(k => Matches(members[k], bestValue)))
                why = "the highest value of generation " + gen + "'s scores row is shared by " + (sharing.Count + 1) + " members, and the first of them (" + TankLog.MemberId(gen, bestIndex) + ") is not the generation row's top";
            else
                why = "the highest-valued member of generation " + gen + "'s scores row (" + TankLog.MemberId(gen, bestIndex) + ") does not match the generation row's top in body, born, episodes or value";
            return null;
        }

        // ------------------------------------------------------------------ the exam's genomes

        private void ReadExam()
        {
            JsonElement? plan = Js.At(Run, "exam");
            string name = Js.Str(plan, "name") ?? "exam";
            if (plan.HasValue && !PlainNameRe.IsMatch(name))
            {
                Notes.Add("run.json's exam plan names the exam \"" + (name.Length > 40 ? name.Substring(0, 40) + "..." : name) + "\", which is not a plain name, so its genomes file is not read");
                return;
            }
            string[] candidates = plan.HasValue ? new[] { name } : new[] { "exam", "bench" };
            foreach (string n in candidates)
            {
                string file = "tank-" + n + "-genomes.tsv";
                List<string> lines = Rows(file, true);
                if (lines == null) continue;
                var planned = Js.Items(Js.Get(plan, "genomes")).ToList();
                if (plan.HasValue && planned.Count != lines.Count)
                    Notes.Add($"run.json's exam plan names {planned.Count} genomes and {file} holds {lines.Count}: the plan's ids are used for the lines they reach");
                for (int i = 0; i < lines.Count; i++)
                {
                    int tab = lines[i].IndexOf('\t');
                    if (tab < 0) { Notes.Add($"{file} line {i + 1} has no tab: skipped"); continue; }
                    string text = lines[i].Substring(tab + 1);
                    int? body = int.TryParse(lines[i].Substring(0, tab), NumberStyles.Integer, CultureInfo.InvariantCulture, out int bi) ? bi : (int?)null;
                    string planId = i < planned.Count ? Js.Str(planned[i], "id") : null;
                    var g = new HeldGenome { ExamIndex = i, ExamName = n, Body = body, Text = text, Source = file + " line " + (i + 1) };
                    g.Kinds.Add("exam");
                    Stats(g);
                    string ownName = n + "." + i.ToString(CultureInfo.InvariantCulture);
                    string badId = null;
                    if (planId != null && !PlainIdRe.IsMatch(planId))
                    {
                        badId = "run.json names this genome \"" + (planId.Length > 40 ? planId.Substring(0, 40) + "..." : planId) + "\", which is not a plain name (it holds a separator or is a dot name): it is kept as " + ownName;
                        planId = null;
                    }
                    HeldGenome same = planId == null ? null : Held.FirstOrDefault(h => h.Id == planId);
                    if (planId != null && MemberIdRe.IsMatch(planId)) g.MemberId = planId;
                    if (same != null && same.ExamIndex == null && same.Text == text)
                    {
                        // One genome, held twice: the last generation's top is also an exam finalist.
                        same.Kinds.Add("exam");
                        same.ExamIndex = i; same.ExamName = n;
                        same.Aliases.Add(ownName);
                        same.Body = same.Body ?? body;
                        continue;
                    }
                    if (same != null && same.ExamIndex == null && same.MemberId != null)
                    {
                        // The recorded id wins: the exam plan says this id is this genome, so the top that the scores row tied to it is named by its generation.
                        string was = same.Id;
                        string topName = "best.g" + (same.Generation ?? 0).ToString(CultureInfo.InvariantCulture);
                        same.Aliases.Remove(topName);
                        same.Id = topName;
                        same.MemberId = null;
                        same.IdNote = "the run's files tie this genome to member " + was + ", but run.json's exam plan names a different genome " + was + ": the recorded id is the plan's, so this genome is named " + topName;
                        Notes.Add("generation " + same.Generation + "'s top genome and run.json's exam plan both claim " + was + " with different text: the plan's genome keeps the id and the top is named " + topName);
                        same = null;
                    }
                    if (same != null)
                    {
                        // Two lines of the plan carry one id: the first keeps it.
                        badId = "run.json's exam plan names " + planId + " twice: this line is kept as " + ownName;
                        planId = null;
                    }
                    g.Id = planId ?? ownName;
                    if (badId != null) g.IdNote = badId;
                    else if (planId == null) g.IdNote = plan.HasValue ? "the exam plan names no id for line " + (i + 1) : "this run wrote no exam plan: the genome is named by its line in " + file;
                    else g.IdNote = "named by run.json's exam plan";
                    if (g.Id != ownName) g.Aliases.Add(ownName);
                    Held.Add(g);
                }
                return;
            }
            if (plan.HasValue && !_unreadable.Contains("tank-" + name + "-genomes.tsv")) Notes.Add("run.json names an exam plan but the run has no tank-" + name + "-genomes.tsv: the exam's genomes are not kept");
        }

        private static void Stats(HeldGenome g)
        {
            try
            {
                using (JsonDocument d = JsonDocument.Parse(g.Text))
                {
                    JsonElement r = d.RootElement;
                    g.Nodes = Js.Items(Js.Get(r, "nodes")).Count();
                    JsonElement? brain = Js.Get(r, "brain");
                    g.Central = brain.HasValue;
                    g.Hidden = Js.Items(Js.Get(brain, "hidden")).Count();
                    g.Links = Js.Items(Js.Get(brain, "links")).Count();
                }
            }
            catch (JsonException e) { g.Problem = "this line is not JSON (" + Flat(e.Message) + ")"; }
        }

        // ------------------------------------------------------------------ lineage

        /// <summary>Whether the run has a lineage file: <c>present</c>, <c>absent</c> (a run made before the file existed) or <c>unreadable</c>.</summary>
        public string LineageFileState()
        {
            FileLines probe = ReadProbe(Path.Combine(Folder, TankLog.LineageFile));
            return probe.Problem != null ? "unreadable" : probe.Absent ? "absent" : "present";
        }

        private static FileLines ReadProbe(string path)
        {
            try { using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { } return new FileLines { Lines = new List<string>() }; }
            catch (FileNotFoundException) { return new FileLines { Absent = true }; }
            catch (DirectoryNotFoundException) { return new FileLines { Absent = true }; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return new FileLines { Problem = Scrub(e.Message, path) }; }
        }

        /// <summary>Why the lineage file could not be read, or null. Read again on the next call: a failure is never remembered as the file's being absent.</summary>
        public string LineageProblem { get { lock (this) return _lineageProblem; } }

        /// <summary>Rows of the lineage file that could not be taken, each named by its line.</summary>
        public IReadOnlyList<string> LineageNotes { get { lock (this) return _lineageNotes.ToArray(); } }

        /// <summary>
        /// <c>tank-lineage.jsonl</c> by member id, or null when the run has none (a run made before the file existed) or it could not be read
        /// (<see cref="LineageProblem"/>). Read once and kept, once it has been read; a failed read is tried again.
        /// </summary>
        public Dictionary<string, LineageRow> Lineage()
        {
            lock (this)
            {
                if (_lineageRead) return _lineage;
                FileLines f = ReadFile(Path.Combine(Folder, TankLog.LineageFile));
                if (f.Problem != null) { _lineageProblem = TankLog.LineageFile + " could not be read (" + f.Problem + ")"; return null; }
                _lineageProblem = null;
                if (f.Lines == null) { _lineageRead = true; return null; }
                var d = new Dictionary<string, LineageRow>(StringComparer.Ordinal);
                _lineageNotes.Clear();
                for (int n = 0; n < f.Lines.Count; n++)
                {
                    try
                    {
                        JsonElement r = Js.Parse(f.Lines[n]);
                        string id = Js.Str(r, "id");
                        if (id == null) throw new FormatException("the row has no id");
                        d[id] = new LineageRow
                        {
                            Id = id, Gen = (int)(Js.Num(r, "gen") ?? -1), Body = (int)(Js.Num(r, "body") ?? -1), Born = (int)(Js.Num(r, "born") ?? -1),
                            Parents = Js.Items(Js.Get(r, "parents")).Select(p => p.GetString() ?? throw new FormatException("a parent is not a string")).ToArray(), Elite = Js.Bool(r, "elite") == true,
                        };
                    }
                    catch (Exception e) when (e is JsonException || e is InvalidOperationException || e is FormatException || e is KeyNotFoundException || e is OverflowException)
                    {
                        // A last row that does not parse is one still being written; any other is a row the writer wrote wrong.
                        _lineageNotes.Add(n == f.Lines.Count - 1 && e is JsonException
                            ? $"the last line of {TankLog.LineageFile} does not parse: it is taken for a row still being written, and left out"
                            : $"{TankLog.LineageFile} line {n + 1} is not a lineage row ({Flat(e.Message)}): left out");
                    }
                }
                _lineageRead = true;
                _lineage = d;
                return d;
            }
        }

        // ------------------------------------------------------------------ what the run developed under

        /// <summary>
        /// The config the run's genomes grow under, or null for Core's defaults; <see cref="ConfigDetail"/> says where it came from. The run's own record comes
        /// first (<c>run.json</c>'s <c>config.episode</c>), then the config room.json names for the run. Neither is a guess: with none, it is <c>unknown</c>.
        /// </summary>
        public RunConfig Config() => ConfigDetail.Config;

        public ConfigInfo ConfigDetail
        {
            get
            {
                lock (this)
                {
                    if (_config != null) return _config;
                    ConfigInfo info = ResolveConfig(out bool final);
                    if (final) _config = info;
                    return info;
                }
            }
        }

        public string ConfigNote => ConfigDetail.Note;

        private static bool TryReadConfig(string text, out RunConfig config, out string reason)
        {
            config = null; reason = null;
            try { config = RunConfigJson.Read(text); return true; }
            catch (Exception e) when (e is FormatException || e is InvalidDataException || e is KeyNotFoundException || e is InvalidOperationException || e is ArgumentException || e is JsonException || e is InvalidCastException || e is OverflowException)
            {
                reason = Flat(e.Message);
                return false;
            }
        }

        private ConfigInfo ResolveConfig(out bool final)
        {
            final = true;
            string runRefusal = null;
            JsonElement? ep = Js.At(Run, "config", "episode");
            if (ep.HasValue)
            {
                if (TryReadConfig(ep.Value.GetRawText(), out RunConfig c, out string why))
                    return new ConfigInfo { Source = "run.json", Loaded = true, Config = c };
                runRefusal = why;
            }

            ConfigInfo roomRefused = null;
            if (_roomConfig != null)
            {
                string reason = null;
                if (_roomConfig.Problem != null) reason = _roomConfig.Problem;
                else
                {
                    FileLines f = ReadFile(_roomConfig.Full);
                    if (f.Problem != null) { reason = "the file could not be read (" + f.Problem + ")"; final = false; }
                    else if (f.Absent) reason = "the file is not there";
                    else if (TryReadConfig(string.Join("\n", f.Lines), out RunConfig c, out string why))
                        return new ConfigInfo
                        {
                            Source = "room.json", File = _roomConfig.Path, Loaded = true, Config = c,
                            Note = runRefusal == null ? null : "run.json's own episode config is refused by this build (" + runRefusal + "): the config room.json names for the run is used",
                        };
                    else reason = why;
                }
                roomRefused = new ConfigInfo { Source = "room.json", File = _roomConfig.Path, Refusal = reason };
                if (runRefusal != null) roomRefused.Refusal = reason + "; run.json's own episode config is refused too (" + runRefusal + ")";
            }
            if (roomRefused != null)
            {
                roomRefused.Note = "the run's config is recorded in room.json (" + roomRefused.File + ") but this build cannot use it (" + roomRefused.Refusal + "); " + Fallback;
                return roomRefused;
            }
            if (runRefusal != null)
                return new ConfigInfo { Source = "run.json", Refusal = runRefusal, Note = "the run's config is recorded in run.json but this build refuses it (" + runRefusal + "); " + Fallback };
            return new ConfigInfo { Source = "unknown", Note = "the run's config is not recorded; " + Fallback };
        }
    }

    /// <summary>
    /// The genome resources: a list of what a run holds, and one genome with its body, brain, lineage and what changed from its parent. Every number is
    /// computed here from the genome with Core's own code (<see cref="GenomeJson"/>, <see cref="Developer"/>, <see cref="CentralWiring"/>, <see cref="BrainIds"/>);
    /// nothing about any experiment is written into the room, and no episode is stepped.
    /// </summary>
    public static class GenomeViews
    {
        private const int MaxChanges = 500;

        /// <summary>A float as the number its shortest round-trip text names, so that 0.3568115 stays 0.3568115 and does not become the double nearest the float's bits.</summary>
        private static double? F(float v) => float.IsFinite(v) ? double.Parse(v.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) : (double?)null;

        private static object V3(Float3 v) => new { x = F(v.X), y = F(v.Y), z = F(v.Z) };

        /// <summary>The list resource: every genome the run holds, with what the picker needs, and what the run's files could not say.</summary>
        public static object List(ResourceResolver.RunRef run, GenomeSet set, string folder)
        {
            string lineage = set.LineageFileState();
            return new
            {
                run = run.Id,
                folder,   // as the caller names it: relative to the repository root, never the full path
                version = set.Version,
                config = ConfigObject(set),
                lineageFile = lineage == "present",
                lineageState = lineage,
                predatesLineage = lineage == "absent",
                notes = set.Notes,
                count = set.Held.Count,
                genomes = set.Held.Select(g => new
                {
                    id = g.Id,
                    aliases = g.Aliases,
                    kinds = g.Kinds,
                    generation = g.Generation,
                    examIndex = g.ExamIndex,
                    examName = g.ExamName,
                    body = g.Body,
                    born = g.Born,
                    episodes = g.Episodes,
                    score = g.Score,
                    memberId = g.MemberId,
                    idNote = g.IdNote,
                    source = g.Source,
                    problem = g.Problem,
                    nodes = g.Nodes,
                    brain = g.Central ? new { hidden = g.Hidden, links = g.Links } : null,
                }).ToList(),
            };
        }

        /// <summary>Where the config the genomes grow under came from, for a screen to say beside the body and the brain.</summary>
        private static object ConfigObject(GenomeSet set)
        {
            ConfigInfo c = set.ConfigDetail;
            return new { source = c.Source, file = c.File, loaded = c.Loaded, refusal = c.Refusal, note = c.Note };
        }

        /// <summary>What a genome that is not well-formed can make Core's reading of it throw: each becomes a stated problem and never a failed request.</summary>
        private static bool Malformed(Exception e) =>
            e is FormatException || e is KeyNotFoundException || e is InvalidOperationException || e is JsonException || e is ArgumentException
            || e is InvalidCastException || e is OverflowException || e is IndexOutOfRangeException;

        /// <summary>A genome that cannot be shown: Core's own message, every issue it lists on one line, and nothing drawn that was not computed.</summary>
        private static object Problem(Dictionary<string, object> d, GenomeSet set, HeldGenome held, string problem, bool keepGenome)
        {
            d["problem"] = problem;
            if (!keepGenome) { d["genome"] = null; d["graph"] = null; }
            d["development"] = null; d["bodyPlan"] = null; d["brain"] = null;
            d["lineage"] = LineageOf(set, held); d["parents"] = new object[0]; d["changes"] = null;
            return d;
        }

        /// <summary>One genome, read whole.</summary>
        public static object View(ResourceResolver.RunRef run, GenomeSet set, string name)
        {
            HeldGenome held = set.Find(name);
            if (held == null)
                throw new ResourceNotFound($"run {run.Id} holds no genome \"{name}\" (it holds {set.Held.Count}: " + string.Join(", ", set.Held.Take(12).Select(g => g.Id)) + (set.Held.Count > 12 ? ", ..." : "") + ")");

            var d = new Dictionary<string, object>
            {
                ["run"] = run.Id,
                ["id"] = held.Id,
                ["aliases"] = held.Aliases,
                ["kinds"] = held.Kinds,
                ["generation"] = held.Generation,
                ["examIndex"] = held.ExamIndex,
                ["examName"] = held.ExamName,
                ["body"] = held.Body,
                ["born"] = held.Born,
                ["episodes"] = held.Episodes,
                ["score"] = held.Score,
                ["memberId"] = held.MemberId,
                ["idNote"] = held.IdNote,
                ["source"] = held.Source,
                ["version"] = set.Version,
                ["config"] = ConfigObject(set),
                ["problem"] = null,
            };

            Genome genome;
            try { genome = GenomeJson.Read(held.Text); }
            catch (Exception e) when (Malformed(e))
            {
                return Problem(d, set, held, "the genome does not load on this build: " + GenomeSet.Flat(e.Message), false);
            }

            ConfigInfo info = set.ConfigDetail;
            RunConfig config = info.Config;
            DevelopmentLimits limits = config != null ? config.Development : DevelopmentLimits.Default;
            PartShapeRegistry shapes = config != null ? config.Shapes : PartShapeRegistry.Standard;
            CellTypeRegistry cellTypes = config?.CellTypes;
            SensorChannel[] pool = config != null ? config.SensorPool() : SensorChannels.DefaultPool;

            d["genome"] = new
            {
                format = GenomeJson.FormatFor(genome),
                root = genome.RootIndex,
                nodes = genome.Nodes.Count,
                adultScale = F(genome.AdultScale),
                lifespan = F(genome.LifespanScale),
                reproduction = new { brood = genome.Reproduction.BroodSize, investment = F(genome.Reproduction.BirthInvestment), margin = F(genome.Reproduction.ReserveMargin), mode = genome.Reproduction.Mode.ToString(), gestation = F(genome.Reproduction.GestationShare) },
                bytes = Encoding.UTF8.GetByteCount(held.Text),
            };
            try { d["graph"] = GraphOf(genome); }
            catch (Exception e) when (Malformed(e)) { return Problem(d, set, held, "the genome's graph cannot be read: " + GenomeSet.Flat(e.Message), true); }

            Phenotype body;
            try { body = Developer.Develop(genome, limits, null, shapes, cellTypes: cellTypes); }
            catch (Exception e) when (Malformed(e))
            {
                return Problem(d, set, held, "the genome does not develop: " + GenomeSet.Flat(e.Message), true);
            }

            object bodyPlan;
            BrainView brain;
            try
            {
                bodyPlan = BodyOf(body, shapes);
                brain = BrainOf(genome, body, pool, info);
            }
            catch (Exception e) when (Malformed(e))
            {
                return Problem(d, set, held, "the grown body or the brain cannot be read: " + GenomeSet.Flat(e.Message), true);
            }

            d["development"] = new
            {
                limits = info.Loaded ? "the config recorded in " + info.Source : "Core's default limits",
                note = info.Note,
                prunedForVolume = body.PrunedForVolume, prunedForDepth = body.PrunedForDepth, prunedForParts = body.PrunedForParts, prunedForReach = body.PrunedForReach,
                truncated = body.WasTruncated,
            };
            d["bodyPlan"] = bodyPlan;
            d["brain"] = brain.Json;
            d["lineage"] = LineageOf(set, held);

            // Parents and what changed from the base parent, when the run holds that genome.
            Dictionary<string, LineageRow> lin = set.Lineage();
            LineageRow row = held.MemberId != null && lin != null && lin.TryGetValue(held.MemberId, out LineageRow lr) ? lr : null;
            var parents = new List<object>();
            HeldGenome baseParent = null;
            if (row != null)
            {
                for (int i = 0; i < row.Parents.Length; i++)
                {
                    HeldGenome p = set.Find(row.Parents[i]);
                    string role = row.Parents.Length == 2 ? (i == 0 ? "base" : "donor") : row.Elite ? "itself, a generation ago" : "parent";
                    parents.Add(new { id = row.Parents[i], role, held = p != null, heldAs = p?.Id });
                    if (i == 0) baseParent = p;
                }
            }
            d["parents"] = parents;
            d["changes"] = ChangesOf(row, baseParent, held, brain);
            return d;
        }

        // ------------------------------------------------------------------ the genome's graph

        /// <summary>
        /// The genome's own morphology graph, read from its fields and nothing else: every node (cell type, shape, joint, recursion limit and growth rule)
        /// and every edge (its id where the genome carries one, its source and target, its reflection flags and whether it is followed only at the end of a chain).
        /// This is the plan development reads, not what it grew: the body plan is the other.
        /// </summary>
        private static object GraphOf(Genome genome)
        {
            var nodes = new List<object>();
            var edges = new List<object>();
            for (int i = 0; i < genome.Nodes.Count; i++)
            {
                MorphNode n = genome.Nodes[i];
                nodes.Add(new
                {
                    index = i,
                    root = i == genome.RootIndex,
                    cellType = n.CellTypeId,
                    shape = n.ShapeId,
                    joint = n.JointType.ToString(),
                    recursiveLimit = n.RecursiveLimit,
                    growth = n.Growth.ToString(),
                    maxModules = n.MaxModules,
                    edges = n.Edges.Count,
                });
                for (int j = 0; j < n.Edges.Count; j++)
                {
                    MorphEdge e = n.Edges[j];
                    edges.Add(new
                    {
                        id = e.Id != 0UL ? BrainIds.Hex(e.Id) : null,
                        index = j,
                        from = i,
                        to = e.Child,
                        reflect = new { x = e.Reflect.X, y = e.Reflect.Y, z = e.Reflect.Z },
                        copies = e.Reflect.CopyCount,
                        terminalOnly = e.TerminalOnly,
                    });
                }
            }
            return new { root = genome.RootIndex, nodes, edges };
        }

        // ------------------------------------------------------------------ lineage

        private static object LineageOf(GenomeSet set, HeldGenome held)
        {
            Dictionary<string, LineageRow> lin = set.Lineage();
            if (lin == null && set.LineageProblem != null)
                return new { file = TankLog.LineageFile, present = false, predatesLineage = false, note = set.LineageProblem + ": which is not the run's having none; it is read again on the next request", problems = (object)set.LineageNotes, row = (object)null };
            if (lin == null)
                return new { file = TankLog.LineageFile, present = false, predatesLineage = true, note = "this run predates lineage: it wrote no " + TankLog.LineageFile, problems = (object)set.LineageNotes, row = (object)null };
            if (held.MemberId == null)
                return new { file = TankLog.LineageFile, present = true, predatesLineage = false, note = "the run's files do not tie this genome to a member id, so its lineage row cannot be found", problems = (object)set.LineageNotes, row = (object)null };
            if (!lin.TryGetValue(held.MemberId, out LineageRow r))
                return new { file = TankLog.LineageFile, present = true, predatesLineage = false, note = "the lineage file has no row for member " + held.MemberId, problems = (object)set.LineageNotes, row = (object)null };
            return new
            {
                file = TankLog.LineageFile, present = true, predatesLineage = false, note = (string)null, problems = (object)set.LineageNotes,
                row = new { gen = r.Gen, id = r.Id, body = r.Body, born = r.Born, parents = r.Parents, elite = r.Elite },
            };
        }

        // ------------------------------------------------------------------ the body

        private static object BodyOf(Phenotype body, PartShapeRegistry shapes)
        {
            var parts = new List<object>();
            float[] lo = { float.MaxValue, float.MaxValue, float.MaxValue }, hi = { float.MinValue, float.MinValue, float.MinValue };
            foreach (PhenotypePart p in body.Parts)
            {
                // Core's own reading of a round shape's size: a collider and a mesh must take the same.
                double? radius = null, halfSpan = null;
                if (p.ShapeId == ShapeIds.Sphere) radius = F(SphereShape.Radius(p.HalfExtents));
                else if (p.ShapeId == ShapeIds.Capsule) { radius = F(CapsuleShape.Radius(p.HalfExtents)); halfSpan = F(CapsuleShape.HalfSpan(p.HalfExtents)); }
                int dof = p.JointType.DofCount();
                parts.Add(new
                {
                    index = p.Index,
                    parent = p.IsRoot ? (int?)null : p.ParentIndex,
                    sourceNode = p.SourceNode,
                    depth = p.Depth,
                    cellType = p.CellTypeId,
                    shape = p.ShapeId,
                    mirrored = p.Mirrored,
                    halfExtents = V3(p.HalfExtents),
                    radius,
                    halfSpan,
                    position = V3(p.Position),
                    rotation = new { x = F(p.Rotation.X), y = F(p.Rotation.Y), z = F(p.Rotation.Z), w = F(p.Rotation.W) },
                    volume = F(p.Volume),
                    joint = p.IsRoot ? null : new
                    {
                        type = p.JointType.ToString(),
                        dof,
                        limits = p.JointLimits.Select(l => new { min = F(l.X), max = F(l.Y) }).ToList(),
                        parentAnchor = V3(p.ParentAnchorLocal),
                        childAnchor = V3(p.ChildAnchorLocal),
                        power = F(p.Power),
                        drive = new { threshold = F(p.Drive.Threshold), reaction = F(p.Drive.Reaction), hold = F(p.Drive.Hold), release = F(p.Drive.Release) },
                    },
                    traits = new { lift = F(p.Lift), attack = F(p.Attack), intake = F(p.Intake), protection = F(p.Protection), toughness = F(p.Toughness), buoyancyOffset = F(p.BuoyancyOffset) },
                });
                // The extent a part reaches, for the screen's frame: the part's centre plus the largest of its half-extents is a bound on every corner.
                float reach = Math.Max(Math.Abs(p.HalfExtents.X), Math.Max(Math.Abs(p.HalfExtents.Y), Math.Abs(p.HalfExtents.Z))) * 1.7321f;
                for (int a = 0; a < 3; a++)
                {
                    lo[a] = Math.Min(lo[a], p.Position[a] - reach);
                    hi[a] = Math.Max(hi[a], p.Position[a] + reach);
                }
            }
            return new
            {
                frame = "the creature's own frame as Developer.Develop returns it: x across, y up, z across the other way; metres",
                partCount = body.PartCount,
                joints = body.Parts.Count(p => !p.IsRoot && p.JointType != JointType.Fixed),
                dof = body.TotalDof,
                totalVolume = F(body.TotalVolume),
                bounds = new { min = new { x = F(lo[0]), y = F(lo[1]), z = F(lo[2]) }, max = new { x = F(hi[0]), y = F(hi[1]), z = F(hi[2]) } },
                parts,
            };
        }

        // ------------------------------------------------------------------ the brain

        /// <summary>The brain's JSON, and the names it gave every unit (which the change list reuses).</summary>
        private sealed class BrainView
        {
            public object Json;
            public Func<string, string> Name = id => id;
        }

        /// <summary>How a part is named wherever a port or an output is: its index, its cell type and its shape.</summary>
        private static string PartName(PhenotypePart p) => "part " + p.Index.ToString(CultureInfo.InvariantCulture) + " (" + p.CellTypeId + ", " + p.ShapeId + ")";

        private static string SenseName(PhenotypePart part, SensorChannel channel, int index) =>
            channel.ToString() + (CentralWiring.IndicesOn(part, channel) > 1 ? "[" + index.ToString(CultureInfo.InvariantCulture) + "]" : "");

        private static string OutputName(BrainOutput o) => o.Kind.ToString() + " dof " + o.Dof.ToString(CultureInfo.InvariantCulture);

        private static BrainView BrainOf(Genome genome, Phenotype body, SensorChannel[] pool, ConfigInfo info)
        {
            var view = new BrainView();
            CentralBrain brain = genome.Brain;
            if (brain == null)
            {
                int neurons = genome.Nodes.Sum(n => n.Neurons.Length), global = genome.GlobalBrain.Length;
                view.Json = new
                {
                    model = "per-node",
                    note = "this genome carries per-part neurons (the model before D146), not one brain: " + neurons + " neurons on its nodes and " + global + " global. There are no ports, outputs or links to draw.",
                    nodeNeurons = genome.Nodes.Select((n, i) => new { node = i, neurons = n.Neurons.Length }).ToList(),
                    globalNeurons = global,
                };
                return view;
            }

            List<BrainPort> poolPorts = CentralWiring.Ports(body, pool);
            Dictionary<ulong, BrainPort> all = CentralWiring.AllPorts(body);
            List<BrainOutput> outputs = CentralWiring.Outputs(body);
            var outputById = new Dictionary<ulong, BrainOutput>();
            foreach (BrainOutput o in outputs) outputById[o.Id] = o;
            var hiddenIds = new HashSet<ulong>(brain.Hidden.Select(u => u.Id));
            var storedOutputs = new Dictionary<ulong, BrainUnit>();
            foreach (BrainUnit u in brain.Outputs) storedOutputs[u.Id] = u;
            // The links, each end classified alike: a port of the grown body (a source only), a hidden unit, an output of the grown body, or nothing the grown
            // body has. An output the genome stores for a joint the body does not have names nothing, at either end.
            string KindOf(ulong id, bool asSource)
            {
                if (hiddenIds.Contains(id)) return "hidden";
                if (outputById.ContainsKey(id)) return "output";
                if (asSource && all.ContainsKey(id)) return "port";
                return "nothing";
            }

            // Every id a link names, and what it names: the ids that name nothing (no port of the grown body, no unit of the brain) are collected once each,
            // in the order the links first name them, whichever end names them.
            var linkedFrom = new Dictionary<ulong, int>();
            var touched = new HashSet<ulong>();
            var ghosts = new List<ulong>();
            var ghostSet = new HashSet<ulong>();
            var ghostAsSource = new HashSet<ulong>();
            var ghostAsTarget = new HashSet<ulong>();
            var ghostLinks = new Dictionary<ulong, int>();
            foreach (BrainLink l in brain.Links)
            {
                linkedFrom[l.From] = linkedFrom.TryGetValue(l.From, out int c) ? c + 1 : 1;
                touched.Add(l.From); touched.Add(l.To);
                if (KindOf(l.From, true) == "nothing")
                {
                    if (ghostSet.Add(l.From)) ghosts.Add(l.From);
                    ghostAsSource.Add(l.From);
                    ghostLinks[l.From] = ghostLinks.TryGetValue(l.From, out int a) ? a + 1 : 1;
                }
                // a port is a sense, and no link drives one: it is marked, but it is a node of the port list already, so it is not listed again
                if (KindOf(l.To, false) == "nothing" && !all.ContainsKey(l.To))
                {
                    if (ghostSet.Add(l.To)) ghosts.Add(l.To);
                    ghostAsTarget.Add(l.To);
                    ghostLinks[l.To] = ghostLinks.TryGetValue(l.To, out int b2) ? b2 + 1 : 1;
                }
            }

            // Ports, in Core's own order (CentralWiring.Ports): part first, then the pool's order (a run's extra channels where the pool puts them), then the
            // index. Every port of the pool is listed, linked or not. A port a link names that is not in the pool goes inside its own part, after that
            // part's pool ports, in channel and index order. Last, the ids that name nothing and are read as a source. None is dropped.
            var portList = new List<object>();
            var names = new Dictionary<ulong, string>();
            var poolIds = new HashSet<ulong>(poolPorts.Select(p => p.Id));
            ILookup<int, BrainPort> poolByPart = poolPorts.ToLookup(p => p.Part);
            ILookup<int, BrainPort> extraByPart = all.Values.Where(p => !poolIds.Contains(p.Id) && touched.Contains(p.Id))
                .OrderBy(p => (int)p.Channel).ThenBy(p => p.Index).ToLookup(p => p.Part);
            void AddPort(BrainPort p, bool inPool)
            {
                PhenotypePart part = body.Parts[p.Part];
                string name = PartName(part) + " · " + SenseName(part, p.Channel, p.Index);
                names[p.Id] = name;
                portList.Add(new { id = BrainIds.Hex(p.Id), part = p.Part, channel = p.Channel.ToString(), index = p.Index, name, sense = SenseName(part, p.Channel, p.Index), inPool, exists = true, links = linkedFrom.TryGetValue(p.Id, out int n) ? n : 0 });
            }
            foreach (PhenotypePart part in body.Parts)
            {
                foreach (BrainPort p in poolByPart[part.Index]) AddPort(p, true);
                foreach (BrainPort p in extraByPart[part.Index]) AddPort(p, false);
            }
            foreach (ulong id in ghosts.Where(g => ghostAsSource.Contains(g) && !storedOutputs.ContainsKey(g)))
            {
                string name = BrainIds.Hex(id) + " (names nothing in the grown body)";
                names[id] = name;
                portList.Add(new { id = BrainIds.Hex(id), part = (int?)null, channel = (string)null, index = (int?)null, name, sense = (string)null, inPool = false, exists = false, links = linkedFrom.TryGetValue(id, out int n) ? n : 0 });
            }

            // Outputs: the body's, in the drive array's order, with the stored bias where the genome has one; then any stored output the body has no joint for;
            // then the ids that name nothing and are only ever a link's target.
            var outputList = new List<object>();
            foreach (BrainOutput o in outputs)
            {
                PhenotypePart part = body.Parts[o.Part];
                string name = PartName(part) + " · " + OutputName(o);
                names[o.Id] = name;
                bool stored = storedOutputs.TryGetValue(o.Id, out BrainUnit u);
                outputList.Add(new { id = BrainIds.Hex(o.Id), part = o.Part, dof = o.Dof, kind = o.Kind.ToString(), driveIndex = o.DriveIndex, name, stored, bias = stored ? F(u.Bias) : null, exists = true });
            }
            foreach (BrainUnit u in brain.Outputs)
            {
                if (outputById.ContainsKey(u.Id)) continue;
                string name = BrainIds.Hex(u.Id) + " (names nothing in the grown body)";
                names[u.Id] = name;
                outputList.Add(new { id = BrainIds.Hex(u.Id), part = (int?)null, dof = (int?)null, kind = (string)null, driveIndex = (int?)null, name, stored = true, bias = F(u.Bias), exists = false });
            }
            foreach (ulong id in ghosts.Where(g => !ghostAsSource.Contains(g) && !storedOutputs.ContainsKey(g)))
            {
                string name = BrainIds.Hex(id) + " (names nothing in the grown body)";
                names[id] = name;
                outputList.Add(new { id = BrainIds.Hex(id), part = (int?)null, dof = (int?)null, kind = (string)null, driveIndex = (int?)null, name, stored = false, bias = (double?)null, exists = false });
            }
            // The one group of ids that name nothing, each once: where it is a link's source, its target, or both; a stored output the body has no joint for
            // is in it too, linked or not.
            foreach (BrainUnit u in brain.Outputs)
                if (!outputById.ContainsKey(u.Id) && ghostSet.Add(u.Id)) { ghosts.Add(u.Id); ghostLinks[u.Id] = 0; }
            var nothingList = ghosts.Select(id => new
            {
                id = BrainIds.Hex(id), asSource = ghostAsSource.Contains(id), asTarget = ghostAsTarget.Contains(id), links = ghostLinks[id],
                stored = storedOutputs.ContainsKey(id),
                name = BrainIds.Hex(id) + " (names nothing in the grown body)",
            }).ToList();

            var inCount = new Dictionary<ulong, int>();
            var outCount = new Dictionary<ulong, int>();
            foreach (BrainLink l in brain.Links)
            {
                inCount[l.To] = inCount.TryGetValue(l.To, out int a) ? a + 1 : 1;
                outCount[l.From] = outCount.TryGetValue(l.From, out int b) ? b + 1 : 1;
            }
            var hiddenList = new List<object>();
            foreach (BrainUnit u in brain.Hidden)
            {
                string name = "hidden " + BrainIds.Hex(u.Id).Substring(0, 8);
                names[u.Id] = name;
                hiddenList.Add(new { id = BrainIds.Hex(u.Id), name, bias = F(u.Bias), links = (inCount.TryGetValue(u.Id, out int i) ? i : 0) + (outCount.TryGetValue(u.Id, out int o) ? o : 0), inbound = inCount.TryGetValue(u.Id, out int ii) ? ii : 0, outbound = outCount.TryGetValue(u.Id, out int oo) ? oo : 0 });
            }

            var linkList = new List<object>();
            foreach (BrainLink l in brain.Links)
            {
                linkList.Add(new
                {
                    from = BrainIds.Hex(l.From), to = BrainIds.Hex(l.To), weight = F(l.Weight),
                    sign = l.Weight > 0f ? "+" : l.Weight < 0f ? "-" : "0",
                    fromKind = KindOf(l.From, true), toKind = KindOf(l.To, false),
                });
            }

            view.Name = hex =>
            {
                try { return names.TryGetValue(BrainIds.ParseHex(hex), out string n) ? n : hex + " (not in the child's body)"; }
                catch (FormatException) { return hex; }
            };
            view.Json = new
            {
                model = "central",
                pool = info.Loaded
                    ? "the sensor pool of the config recorded in " + info.Source
                    : "Core's default pool (the run's config is not known, see config): ports named by a link are listed too, whatever their channel",
                poolChannels = pool.Select(c => c.ToString()).ToList(),
                counts = new { ports = portList.Count, outputs = outputList.Count, hidden = hiddenList.Count, links = linkList.Count, nothing = nothingList.Count },
                ports = portList,
                outputs = outputList,
                hidden = hiddenList,
                links = linkList,
                nothing = nothingList,
            };
            return view;
        }

        // ------------------------------------------------------------------ what changed from the parent

        private static object ChangesOf(LineageRow row, HeldGenome baseParent, HeldGenome child, BrainView childBrain)
        {
            if (row == null)
                return new { against = (string)null, reason = child.MemberId == null ? "no lineage row: the run's files do not tie this genome to a member id" : "no lineage row for this member", parent = (string)null, count = 0, changes = new object[0], truncated = false };
            if (row.Parents.Length == 0)
                return new { against = (string)null, reason = "a founder: generation 0 has no parent", parent = (string)null, count = 0, changes = new object[0], truncated = false };
            string parentId = row.Parents[0];
            if (baseParent == null)
                return new { against = (string)null, reason = "the parent's genome is not kept: this run holds a generation's top genome and the exam's genomes, and " + parentId + " is neither", parent = parentId, count = 0, changes = new object[0], truncated = false };
            var list = new List<object>();
            int total = 0;
            JsonElement a, b;
            try { a = Js.Parse(baseParent.Text); }
            catch (JsonException e) { return new { against = (string)null, reason = "the parent's genome " + baseParent.Id + " is kept, but its text does not parse (" + GenomeSet.Flat(e.Message) + "), so nothing can be compared with it", parent = parentId, count = 0, changes = new object[0], truncated = false }; }
            try { b = Js.Parse(child.Text); }
            catch (JsonException e) { return new { against = (string)null, reason = "this genome's text does not parse (" + GenomeSet.Flat(e.Message) + "), so nothing can be compared with its parent", parent = parentId, count = 0, changes = new object[0], truncated = false }; }
            try { Diff(a, b, "", list, ref total, childBrain.Name); }
            catch (Exception e) when (Malformed(e))
            {
                return new { against = (string)null, reason = "the comparison with " + baseParent.Id + " failed (" + GenomeSet.Flat(e.Message) + ")", parent = parentId, count = 0, changes = new object[0], truncated = false };
            }
            return new { against = baseParent.Id, reason = (string)null, parent = parentId, identical = total == 0, count = total, changes = list, truncated = total > list.Count };
        }

        private static bool IsHex16(string s) => s != null && s.Length == 16 && s.All(Uri.IsHexDigit);

        /// <summary>The key a keyed array's element goes by: its id, or a link's endpoints; null when the array is read by position.</summary>
        private static string KeyOf(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            string id = Js.Str(e, "id");
            if (id != null) return id;
            string f = Js.Str(e, "from"), t = Js.Str(e, "to");
            return f != null && t != null ? f + ">" + t : null;
        }

        private static string Short(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object && Js.Str(e, "from") != null && Js.At(e, "w").HasValue) return "w " + Js.At(e, "w").Value.GetRawText();
            if (e.ValueKind == JsonValueKind.Object && Js.Num(e, "child").HasValue) return "edge to node " + Js.Num(e, "child");
            string t = e.GetRawText();
            return t.Length > 120 ? t.Substring(0, 117) + "..." : t;
        }

        private static void Emit(List<object> list, ref int total, string path, string kind, JsonElement? from, JsonElement? to, Func<string, string> name)
        {
            total++;
            if (list.Count >= MaxChanges) return;
            string label = null;
            if (path.StartsWith("brain.", StringComparison.Ordinal))
            {
                var ids = Regex.Matches(path, @"[0-9a-f]{16}").Select(m => m.Value).ToList();
                if (ids.Count > 0) label = string.Join(" → ", ids.Select(name));
            }
            list.Add(new { path, kind, from = from.HasValue ? Short(from.Value) : null, to = to.HasValue ? Short(to.Value) : null, label });
        }

        private static void Diff(JsonElement a, JsonElement b, string path, List<object> list, ref int total, Func<string, string> name)
        {
            if (a.ValueKind == JsonValueKind.Object && b.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in a.EnumerateObject())
                {
                    string at = path.Length == 0 ? p.Name : path + "." + p.Name;
                    if (!b.TryGetProperty(p.Name, out JsonElement other)) Emit(list, ref total, at, "removed", p.Value, null, name);
                    else Diff(p.Value, other, at, list, ref total, name);
                }
                foreach (JsonProperty p in b.EnumerateObject())
                    if (!a.TryGetProperty(p.Name, out _)) Emit(list, ref total, path.Length == 0 ? p.Name : path + "." + p.Name, "added", null, p.Value, name);
                return;
            }
            if (a.ValueKind == JsonValueKind.Array && b.ValueKind == JsonValueKind.Array)
            {
                JsonElement[] xs = a.EnumerateArray().ToArray(), ys = b.EnumerateArray().ToArray();
                bool keyed = xs.Concat(ys).Any() && xs.Concat(ys).All(e => KeyOf(e) != null);
                if (keyed)
                {
                    var byKey = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    foreach (JsonElement y in ys) byKey[KeyOf(y)] = y;
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (JsonElement x in xs)
                    {
                        string k = KeyOf(x);
                        seen.Add(k);
                        string at = path + "[" + k + "]";
                        if (byKey.TryGetValue(k, out JsonElement y)) Diff(x, y, at, list, ref total, name);
                        else Emit(list, ref total, at, "removed", x, null, name);
                    }
                    foreach (JsonElement y in ys) if (!seen.Contains(KeyOf(y))) Emit(list, ref total, path + "[" + KeyOf(y) + "]", "added", null, y, name);
                    return;
                }
                int n = Math.Min(xs.Length, ys.Length);
                for (int i = 0; i < n; i++) Diff(xs[i], ys[i], path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", list, ref total, name);
                for (int i = n; i < xs.Length; i++) Emit(list, ref total, path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", "removed", xs[i], null, name);
                for (int i = n; i < ys.Length; i++) Emit(list, ref total, path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", "added", null, ys[i], name);
                return;
            }
            bool same = a.ValueKind == b.ValueKind;
            if (same && a.ValueKind == JsonValueKind.Number) same = a.GetDouble() == b.GetDouble();
            else if (same && a.ValueKind == JsonValueKind.String) same = a.GetString() == b.GetString();
            if (!same) Emit(list, ref total, path, "changed", a, b, name);
        }
    }
}
