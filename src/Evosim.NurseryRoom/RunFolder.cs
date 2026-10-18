using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// One run's folder, read live. Each file the nursery writes has its own incremental reader
    /// (<see cref="LiveRows"/>, <see cref="LiveLines"/>), so a poll takes only the lines added since the
    /// last one and the parsed rows stay cached. Every file is optional: an old run lacks
    /// <c>run.json</c>, <c>recipe.json</c> and <c>tank-scores.jsonl</c>, and a run in its search has no
    /// exam yet. What is absent is null in the snapshot, never defaulted.
    /// </summary>
    public sealed class RunFolder
    {
        public const int LogTailLines = 300;

        private readonly object _gate = new object();
        private DateTime _lastRefreshUtc = DateTime.MinValue;

        public string Path { get; }

        private readonly LiveDocument _run, _recipe;
        private readonly LiveRows _generations, _exam;
        private readonly LiveLines _best, _crossovers, _scores, _log;

        // What is kept of the files whose rows are large or many: a summary, not the rows.
        private int _bestRows;
        private JsonElement? _bestLast;
        private readonly CrossoverTotals _cross = new CrossoverTotals();
        private readonly List<ScoreRow> _scoreRows = new List<ScoreRow>();
        private readonly LinkedList<string> _logTail = new LinkedList<string>();

        public RunFolder(string path)
        {
            Path = path;
            _run = new LiveDocument(System.IO.Path.Combine(path, "run.json"));
            _recipe = new LiveDocument(System.IO.Path.Combine(path, "recipe.json"));
            _generations = new LiveRows(System.IO.Path.Combine(path, "tank-generations.jsonl"));
            _exam = new LiveRows(System.IO.Path.Combine(path, "tank-exam.jsonl"));
            _best = new LiveLines(System.IO.Path.Combine(path, "tank-best.jsonl"));
            _crossovers = new LiveLines(System.IO.Path.Combine(path, "tank-crossovers.jsonl"));
            _scores = new LiveLines(System.IO.Path.Combine(path, "tank-scores.jsonl"));
            _log = new LiveLines(System.IO.Path.Combine(path, "nursery.log"));
        }

        public bool Exists => Directory.Exists(Path);

        // What a caller gets is a copy made under the lock: the readers append to their lists while a
        // request is serializing, and a list shared past the lock is a list read mid-write.
        public IReadOnlyList<JsonElement> Generations { get { lock (_gate) return _generations.Rows.ToList(); } }
        public IReadOnlyList<JsonElement> ExamRows { get { lock (_gate) return _exam.Rows.ToList(); } }
        public int BestRows { get { lock (_gate) return _bestRows; } }

        /// <summary>The exam table as the snapshot builds it (see <see cref="Snapshot"/>), or null when the run has no exam row.</summary>
        public ExamTable.Result ExamResult()
        {
            lock (_gate)
            {
                if (_exam.Rows.Count == 0) return null;
                JsonElement? recipe = Recipe;
                bool rewardIsNet = !recipe.HasValue || Js.Str(recipe, "reward", "class") == "Net";
                string ownLabel = Js.Str(Js.Items(Js.At(recipe, "exam", "controls")).Cast<JsonElement?>().FirstOrDefault(), "kind");
                return ExamTable.Build(_exam.Rows, ownLabel, rewardIsNet);
            }
        }
        public int ScoreRows { get { lock (_gate) return _scoreRows.Count; } }
        public IReadOnlyCollection<string> LogTail { get { lock (_gate) return _logTail.ToList(); } }

        /// <summary>Reads what the files gained since the last refresh. Safe to call from several requests at once.</summary>
        public void Refresh(double minAgeSeconds = 0)
        {
            lock (_gate)
            {
                if ((DateTime.UtcNow - _lastRefreshUtc).TotalSeconds < minAgeSeconds) return;
                _lastRefreshUtc = DateTime.UtcNow;
                _run.Poll();
                _recipe.Poll();
                _generations.Poll();
                _exam.Poll();

                LiveLines.PollResult b = _best.Poll();
                if (b.Reset) { _bestRows = 0; _bestLast = null; }
                foreach (string line in b.Lines)
                {
                    _bestRows++;
                    _bestLast = BestSummary(line) ?? _bestLast;
                }

                LiveLines.PollResult x = _crossovers.Poll();
                if (x.Reset) _cross.Clear();
                foreach (string line in x.Lines) _cross.Add(line);

                LiveLines.PollResult sc = _scores.Poll();
                if (sc.Reset) _scoreRows.Clear();
                foreach (string line in sc.Lines)
                {
                    ScoreRow s = ScoreRow.Parse(line);
                    if (s != null) _scoreRows.Add(s);
                }

                LiveLines.PollResult lg = _log.Poll();
                if (lg.Reset) _logTail.Clear();
                foreach (string line in lg.Lines)
                {
                    _logTail.AddLast(line);
                    if (_logTail.Count > LogTailLines) _logTail.RemoveFirst();
                }
            }
        }

        /// <summary>The recipe as the run saved it: <c>recipe.json</c>, or the resolved text inside <c>run.json</c>.</summary>
        public JsonElement? Recipe => _recipe.Value ?? Js.At(_run.Value, "recipe", "resolved");

        public JsonElement? RunRecord => _run.Value;

        /// <summary>
        /// The change key a screen compares to decide whether to redraw: every reader's offset, the record
        /// and recipe write times, and the phase with whether the process is alive. A killed run changes
        /// no file, so only the last two move when it dies. Call with the lock held.
        /// </summary>
        private string VersionOf(string phase, bool? alive) => string.Join("-",
            _generations.Offset, _exam.Offset, _best.Offset, _crossovers.Offset, _scores.Offset, _log.Offset,
            _run.ModifiedUtc?.Ticks ?? 0, _recipe.ModifiedUtc?.Ticks ?? 0,
            phase ?? "", alive == true ? "alive" : alive == false ? "gone" : "unknown");

        // ------------------------------------------------------------------ the registry's line

        /// <summary>What the registry shows of a run, read under the lock: the sparkline's series and the best score.</summary>
        public sealed class RegistryLine
        {
            /// <summary><c>rewardMean</c> or <c>topScore</c>, or null when the run has neither.</summary>
            public string Measure;
            public string Label;
            public List<double?> Series;
            /// <summary>The top genome's score in the last generation row.</summary>
            public double? Best;
        }

        /// <summary>
        /// The run's series for the registry's sparkline: the reward's mean per generation when
        /// <c>tank-scores.jsonl</c> has rows, else the top genome's <c>score</c> in the generation rows.
        /// The series is a copy.
        /// </summary>
        public RegistryLine RegistrySummary()
        {
            lock (_gate)
            {
                var l = new RegistryLine();
                if (_generations.Rows.Count > 0) l.Best = Js.Num(_generations.Rows[_generations.Rows.Count - 1], "top", "score");
                if (_scoreRows.Any(s => s.Mean.HasValue))
                {
                    string reward = _scoreRows[_scoreRows.Count - 1].Reward ?? Js.Str(Recipe, "reward", "class");
                    l.Measure = "rewardMean";
                    l.Label = reward == null ? "reward, mean per generation" : reward + " reward, mean per generation";
                    l.Series = _scoreRows.Select(s => s.Mean).ToList();
                }
                else if (_generations.Rows.Any(g => Js.Num(g, "top", "score").HasValue))
                {
                    l.Measure = "topScore";
                    l.Label = "top genome's score per generation";
                    l.Series = _generations.Rows.Select(g => Js.Num(g, "top", "score")).ToList();
                }
                return l;
            }
        }

        // ------------------------------------------------------------------ the snapshot

        /// <summary>
        /// The run as a resource: its files parsed, and what can be read off them. <paramref name="controlsFallback"/> are the exam controls of the recipe the registry
        /// names for a run that holds no recipe of its own, so that a column's heading can take a knockout's channel from them (see <see cref="ExamHeaders"/>).
        /// </summary>
        public Dictionary<string, object> Snapshot(DateTime nowUtc, bool full = true, IReadOnlyList<JsonElement> controlsFallback = null)
        {
            lock (_gate)
            {
                JsonElement? run = _run.Value, recipe = Recipe;
                List<JsonElement> gens = _generations.Rows.ToList();

                int? planned = Js.Num(recipe, "search", "generations") is double pg ? (int)pg : (int?)null;
                int? lastGen = gens.Count > 0 ? (Js.Num(gens[gens.Count - 1], "gen") is double lg ? (int)lg : gens.Count - 1) : (int?)null;
                double? pace = PaceMinutes(gens);
                DateTime? lastRow = _generations.ModifiedUtc;
                // The exam pairs on what the run selected for: net per draw when the reward is Net or the
                // run predates recipes, the reward's value otherwise. Its own column is the recipe's first control.
                bool rewardIsNet = !recipe.HasValue || Js.Str(recipe, "reward", "class") == "Net";
                string ownLabel = Js.Str(Js.Items(Js.At(recipe, "exam", "controls")).Cast<JsonElement?>().FirstOrDefault(), "kind");
                ExamTable.Result exam = _exam.Rows.Count > 0 ? ExamTable.Build(_exam.Rows, ownLabel, rewardIsNet) : null;
                List<JsonElement> controls = Js.Items(Js.At(recipe, "exam", "controls")).ToList();
                if (controls.Count == 0 && controlsFallback != null) controls = controlsFallback.ToList();
                // The brains the exam is planned to swim: the recipe's controls (or, for a run with no recipe of its own, those of the registry entry's recipe).
                int? expectedBrains = controls.Count > 0 ? controls.Count : (int?)null;

                bool? alive = ProcessAlive(run);
                string recorded = Js.Str(run, "status");
                string phase = Phase(recorded, alive, _exam.Rows.Count > 0, gens.Count > 0, run.HasValue);
                // A run stopped before its run.json exists leaves a folder holding the stop file and nothing else (the nursery refuses a folder that holds anything).
                bool stoppedBeforeBegin = !run.HasValue && StoppedBeforeItBegan(Path);
                if (stoppedBeforeBegin) phase = "stopped";

                // The stall alarm: a run marked running whose files have not grown for more than three times its median generation time.
                var cumulative = new List<double>();
                foreach (JsonElement g in gens) if (Js.Num(g, "seconds") is double sec) cumulative.Add(sec);
                Stall stall = StallRule.Evaluate(cumulative, new (string, DateTime?)[]
                {
                    ("tank-generations.jsonl", _generations.ModifiedUtc), ("tank-exam.jsonl", _exam.ModifiedUtc), ("tank-best.jsonl", _best.ModifiedUtc),
                    ("tank-crossovers.jsonl", _crossovers.ModifiedUtc), ("tank-scores.jsonl", _scores.ModifiedUtc), ("nursery.log", _log.ModifiedUtc),
                }, nowUtc, phase == "search" || phase == "exam");

                ExamPlanCount examPlan = CountExam(run, expectedBrains, phase, exam);

                var files = new List<object>();
                void File(string name, long length, DateTime? mtime, int? rows, int? bad, string problem)
                {
                    if (mtime.HasValue || length > 0) files.Add(new { name, bytes = length, modifiedUtc = mtime, rows, bad, problem });
                }
                var runInfo = _run.ModifiedUtc.HasValue ? new FileInfo(_run.Path).Length : 0;
                File("run.json", runInfo, _run.ModifiedUtc, null, null, _run.Problem);
                File("recipe.json", _recipe.ModifiedUtc.HasValue ? new FileInfo(_recipe.Path).Length : 0, _recipe.ModifiedUtc, null, null, _recipe.Problem);
                File("tank-generations.jsonl", _generations.Length, _generations.ModifiedUtc, _generations.Rows.Count, _generations.Bad, _generations.Problem);
                File("tank-best.jsonl", _best.Length, _best.ModifiedUtc, _bestRows, null, _best.Problem);
                File("tank-crossovers.jsonl", _crossovers.Length, _crossovers.ModifiedUtc, _cross.Rows, null, _crossovers.Problem);
                File("tank-scores.jsonl", _scores.Length, _scores.ModifiedUtc, _scoreRows.Count, null, _scores.Problem);
                File("tank-exam.jsonl", _exam.Length, _exam.ModifiedUtc, _exam.Rows.Count, _exam.Bad, _exam.Problem);
                File("nursery.log", _log.Length, _log.ModifiedUtc, _log.Lines, null, _log.Problem);

                var d = new Dictionary<string, object>
                {
                    ["folder"] = Path,
                    ["nowUtc"] = nowUtc,
                    ["version"] = VersionOf(phase, alive),
                    ["name"] = Js.Str(recipe, "name") ?? Js.Str(run, "recipe", "name") ?? System.IO.Path.GetFileName(Path.TrimEnd('/', '\\')),
                    ["description"] = Js.Str(recipe, "description"),
                    ["state"] = new
                    {
                        recorded,
                        phase,
                        note = stoppedBeforeBegin ? "stopped before it began: the STOP file was written before the run wrote its run.json" : null,
                        processAlive = alive,
                        processId = Js.Num(run, "processId"),
                        startedUtc = Js.Str(run, "startedAtUtc"),
                        endedUtc = Js.Str(run, "endedAtUtc"),
                        error = Js.Str(run, "error"),
                        threads = Js.Num(run, "threads"),
                    },
                    ["progress"] = new
                    {
                        rows = gens.Count,
                        lastGen,
                        planned,
                        paceMinutes = pace,
                        wallSeconds = gens.Count > 0 ? Js.Num(gens[gens.Count - 1], "seconds") : null,
                        lastRowUtc = lastRow,
                        lastRowAgeSeconds = lastRow.HasValue ? Math.Max(0, (nowUtc - lastRow.Value).TotalSeconds) : (double?)null,
                        examBrains = exam?.Brains.Count,
                        examExpected = examPlan.Brains,
                        examGenomes = examPlan.Genomes,
                        examDone = examPlan.Done,
                    },
                    ["stall"] = stall,
                    ["files"] = files,
                    ["reward"] = _scoreRows.Count > 0 ? _scoreRows[_scoreRows.Count - 1].Reward : Js.Str(recipe, "reward", "class"),
                    ["recipeHash"] = Js.Str(run, "recipe", "hash"),
                    ["recipe"] = recipe,
                    ["run"] = run,
                };
                if (!full) return d;

                d["generations"] = gens;
                d["scores"] = _scoreRows.Select(s => s.ToObject()).ToList();
                d["crossovers"] = _cross.Rows > 0 ? _cross.ToObject() : null;     // a copy: its dictionary is cloned
                d["best"] = _bestRows > 0 ? new { rows = _bestRows, last = _bestLast } : null;
                d["exam"] = exam == null ? null : ExamObject(exam, controls);
                d["examEnd"] = phase == "exam" ? ExamEndOf(run, expectedBrains, gens) : null;
                d["log"] = new { total = _log.Lines, lines = _logTail.ToList() };
                return d;
            }
        }

        private sealed class ExamPlanCount { public int? Brains, Genomes, Done; }

        /// <summary>
        /// How far the exam has got, for the band: the brains it is planned to swim (run.json's exam plan, else the recipe's controls), the genomes each is swum on
        /// (the plan, else the genomes file while the exam runs, else the genomes the rows name), and the brains whose every genome's row is written. Call with the lock held.
        /// </summary>
        private ExamPlanCount CountExam(JsonElement? run, int? recipeBrains, string phase, ExamTable.Result exam)
        {
            var c = new ExamPlanCount { Brains = Js.Items(Js.At(run, "exam", "brains")).Count() is int pb && pb > 0 ? pb : recipeBrains };
            int? genomes = Js.Items(Js.At(run, "exam", "genomes")).Count() is int pg && pg > 0 ? pg : (int?)null;
            if (genomes == null && phase == "exam")
            {
                try
                {
                    string genomeFile = System.IO.Path.Combine(Path, "tank-exam-genomes.tsv");
                    if (System.IO.File.Exists(genomeFile)) genomes = System.IO.File.ReadLines(genomeFile).Count(l => l.Length > 0);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            else if (genomes == null && exam != null) genomes = exam.Bodies.Count;
            c.Genomes = genomes;
            // The exam is in its phase once it has a row (see Phase), so no row means it has not begun: nothing is done and nothing is said.
            if (_exam.Rows.Count == 0) return c;
            var perBrain = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (JsonElement row in _exam.Rows)
            {
                string brain = Js.Str(row, "brain");
                if (brain != null) perBrain[brain] = perBrain.TryGetValue(brain, out int n) ? n + 1 : 1;
            }
            // Without the genome count a brain still being swum cannot be told from a finished one, so a run in its exam says none; a run that is over swam every brain it named.
            if (genomes.HasValue) c.Done = perBrain.Values.Count(n => n >= genomes.Value);
            else c.Done = phase == "exam" ? (int?)null : perBrain.Count;
            return c;
        }

        /// <summary>The exam's end, for a run in its exam (<see cref="ExamEstimate"/>): the plan's brains and genomes from run.json's exam plan where the run wrote one, else the recipe's controls and the exam genomes file's lines. Call with the lock held.</summary>
        private ExamEnd ExamEndOf(JsonElement? run, int? recipeBrains, List<JsonElement> gens)
        {
            int? brains = Js.Items(Js.At(run, "exam", "brains")).Count() is int pb && pb > 0 ? pb : recipeBrains;
            int? genomes = Js.Items(Js.At(run, "exam", "genomes")).Count() is int pg && pg > 0 ? pg : (int?)null;
            string genomeFile = System.IO.Path.Combine(Path, "tank-exam-genomes.tsv");
            DateTime? start = null; string from = null;
            try
            {
                var fi = new FileInfo(genomeFile);
                if (fi.Exists)
                {
                    start = fi.LastWriteTimeUtc; from = "the write of tank-exam-genomes.tsv, which opens the exam";
                    if (genomes == null) genomes = System.IO.File.ReadLines(genomeFile).Count(l => l.Length > 0);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (start == null && _generations.ModifiedUtc.HasValue) { start = _generations.ModifiedUtc; from = "the last generation row, which the exam follows"; }
            return ExamEstimate.Compute(_exam.Rows.Count, brains, genomes, start, _exam.ModifiedUtc, from);
        }

        private static object ExamObject(ExamTable.Result e, IReadOnlyList<JsonElement> controls) => new
        {
            brains = e.Brains,
            headers = ExamHeaders.Build(e.Brains, controls),
            pooledBasis = ExamCompare.PooledBasis(e),
            own = e.Own,
            basis = e.Basis,
            hasScore = e.HasScore,
            hasFresh = e.HasFresh,
            draws = e.Draws,
            bodies = e.Bodies.Select(b => new
            {
                genome = b.Genome,
                body = b.BodyIndex,
                ownNet = b.OwnNet,
                ownScore = b.OwnScore,
                ownFresh = b.OwnFresh,
                cells = b.Cells.Select(CellObject).ToList(),
            }).ToList(),
            pooled = e.Pooled.Select(CellObject).ToList(),
        };

        private static object CellObject(ExamTable.Cell c) => new { brain = c.Brain, net = c.Net, score = c.Score, fresh = c.Fresh, diff = c.Diff, se = c.Se, n = c.N, clears = c.Clears };

        /// <summary>Minutes a generation takes: the mean of the last ten gaps in the rows' cumulative wall seconds.</summary>
        public static double? PaceMinutes(IReadOnlyList<JsonElement> gens)
        {
            var secs = new List<double>();
            foreach (JsonElement g in gens)
                if (Js.Num(g, "seconds") is double s) secs.Add(s);
            if (secs.Count < 2) return null;
            int from = Math.Max(0, secs.Count - 11);
            double gaps = secs[secs.Count - 1] - secs[from];
            int n = secs.Count - 1 - from;
            return n > 0 && gaps > 0 ? gaps / n / 60.0 : (double?)null;
        }

        /// <summary>
        /// Whether the process that wrote <c>run.json</c> still runs: the id exists and started when the
        /// record says it did, so a reused id does not read as alive. Null when the record names none.
        /// </summary>
        public static bool? ProcessAlive(JsonElement? run)
        {
            double? pid = Js.Num(run, "processId");
            string started = Js.Str(run, "processStartedAtUtc");
            if (!pid.HasValue || pid.Value <= 0 || started == null) return null;
            try
            {
                using (Process p = Process.GetProcessById((int)pid.Value))
                {
                    if (p.HasExited) return false;
                    if (!DateTime.TryParse(started, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime recorded)) return true;
                    return Math.Abs((p.StartTime.ToUniversalTime() - recorded).TotalSeconds) < 5;
                }
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return null; }
        }

        /// <summary>
        /// True when the folder holds the stop file and nothing else but a launcher's <c>.launch.log</c>: a run that was asked to stop
        /// before it wrote its record, which the nursery then refused to begin in (its folder was not empty).
        /// </summary>
        public static bool StoppedBeforeItBegan(string folder)
        {
            try
            {
                if (!Directory.Exists(folder)) return false;
                string[] names = Directory.GetFileSystemEntries(folder).Select(System.IO.Path.GetFileName).ToArray();
                return names.Contains(Evosim.Nursery.RunRecord.StopFileName) && names.All(n => n == Evosim.Nursery.RunRecord.StopFileName || n.EndsWith(".launch.log", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return false; }
        }

        /// <summary>The run's phase: what it is doing, read from the record and the files, or <c>unrecorded</c> when there is no record.</summary>
        public static string Phase(string recorded, bool? alive, bool examStarted, bool anyRows, bool haveRecord)
        {
            if (!haveRecord) return anyRows ? "unrecorded" : "empty";
            switch (recorded)
            {
                case "running":
                    if (alive == false) return "killed";
                    return examStarted ? "exam" : "search";
                case "completed": return "completed";
                case "exited": return "exited";
                case "failed": return "failed";
                default: return recorded ?? "unrecorded";
            }
        }

        private static JsonElement? BestSummary(string line)
        {
            try
            {
                using (JsonDocument d = JsonDocument.Parse(line))
                {
                    JsonElement g = d.RootElement;
                    int nodes = Js.Items(Js.Get(g, "nodes")).Count();
                    JsonElement? brain = Js.Get(g, "brain");
                    return Js.Parse(Js.Serialize(new
                    {
                        nodes,
                        outputs = Js.Items(Js.Get(brain, "outputs")).Count(),
                        hidden = Js.Items(Js.Get(brain, "hidden")).Count(),
                        links = Js.Items(Js.Get(brain, "links")).Count(),
                    }));
                }
            }
            catch (JsonException) { return null; }
        }

        // ------------------------------------------------------------------ summaries of the big files

        /// <summary>One <c>tank-scores.jsonl</c> row cut to what the screens plot: the generation's top member and the mean.</summary>
        private sealed class ScoreRow
        {
            public int Gen;
            public string Reward;
            public int Members;
            public double? Mean;
            public JsonElement? Top;

            public static ScoreRow Parse(string line)
            {
                try
                {
                    using (JsonDocument d = JsonDocument.Parse(line))
                    {
                        JsonElement r = d.RootElement;
                        var s = new ScoreRow { Gen = Js.Num(r, "gen") is double g ? (int)g : -1, Reward = Js.Str(r, "reward") };
                        double sum = 0; int n = 0; double best = double.NegativeInfinity;
                        foreach (JsonElement m in Js.Items(Js.Get(r, "members")))
                        {
                            s.Members++;
                            double? v = Js.Num(m, "value");
                            if (!v.HasValue) continue;
                            sum += v.Value; n++;
                            if (v.Value > best) { best = v.Value; s.Top = m.Clone(); }
                        }
                        s.Mean = n > 0 ? sum / n : (double?)null;
                        return s;
                    }
                }
                catch (JsonException) { return null; }
            }

            public object ToObject() => new { gen = Gen, reward = Reward, members = Members, mean = Mean, top = Top };
        }

        /// <summary>The breeding file, counted: the rows are many (one a crossover) and the generation rows already hold the per-generation story.</summary>
        private sealed class CrossoverTotals
        {
            public int Rows, Transferred, BaseBetter, Refused, Mutated, Distances;
            public double DistanceSum;
            public readonly SortedDictionary<string, int> Ends = new SortedDictionary<string, int>(StringComparer.Ordinal);

            public void Clear()
            {
                Rows = Transferred = BaseBetter = Refused = Mutated = Distances = 0; DistanceSum = 0; Ends.Clear();
            }

            public void Add(string line)
            {
                try
                {
                    using (JsonDocument d = JsonDocument.Parse(line))
                    {
                        JsonElement r = d.RootElement;
                        Rows++;
                        if (Js.Bool(r, "transferred") == true) Transferred++;
                        if (Js.Bool(r, "baseBetter") == true) BaseBetter++;
                        if (Js.Get(r, "refused").HasValue) Refused++;
                        if (Js.Bool(r, "mutated") == true) Mutated++;
                        if (Js.Num(r, "d") is double dist) { DistanceSum += dist; Distances++; }
                        if (Js.Str(r, "end") is string end) Ends[end] = Ends.TryGetValue(end, out int c) ? c + 1 : 1;
                    }
                }
                catch (JsonException) { }
            }

            public object ToObject() => new
            {
                rows = Rows,
                transferred = Transferred,
                baseBetter = BaseBetter,
                refused = Refused,
                mutated = Mutated,
                meanDistance = Distances > 0 ? DistanceSum / Distances : (double?)null,
                ends = new SortedDictionary<string, int>(Ends, StringComparer.Ordinal),
            };
        }
    }
}
