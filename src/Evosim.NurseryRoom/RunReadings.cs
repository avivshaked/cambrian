using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Evosim.NurseryRoom
{
    // What the run screen reads off a run that is not a file's content: the headings of the exam's columns, the exam set beside a control's, the stall alarm, the
    // exam's end and the activity of one run. Each is a plain function of its inputs (a test gives it hand-made ones), and each answers with types of properties,
    // since Js.Out writes properties only.

    /// <summary>One exam column's heading: the brain's label whole in <see cref="Title"/>, and a short form of it in <see cref="Short"/> that never cuts a word.</summary>
    public sealed class ExamHeader
    {
        public int Index { get; set; }
        public string Label { get; set; }
        /// <summary>What the brain is: the recipe's control kind where the control at its index agrees with the label, else the label's first word, lower case.</summary>
        public string Kind { get; set; }
        public string Short { get; set; }
        /// <summary>The whole label, with the channels of a knockout whose label named none.</summary>
        public string Title { get; set; }
    }

    public static class ExamHeaders
    {
        /// <summary>The knockout's kind, as the nursery names it. (The nursery's words for the controls are the framework's, not an experiment's.)</summary>
        public const string Knockout = "knockout";

        /// <summary>
        /// The headings of the exam's brains, read from the labels the file names and the recipe's controls at the same index. A knockout whose label has no
        /// channel takes it from its control (<c>channels</c>): the file's first knockout is labelled "knockout" where the recipe's is "knockout Chemical".
        /// A word is never cut: a long label is a short <i>whole</i> word with the full label in the title, and a list of channels is its first and how many more
        /// ("Stroke+2"). Nothing here knows what any label means beyond the kind word.
        /// </summary>
        public static List<ExamHeader> Build(IReadOnlyList<string> brains, IReadOnlyList<JsonElement> controls)
        {
            var l = new List<ExamHeader>();
            for (int i = 0; i < brains.Count; i++)
            {
                string label = (brains[i] ?? "").Trim();
                string[] words = label.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string first = words.Length > 0 ? words[0] : "";
                JsonElement? control = controls != null && i < controls.Count ? controls[i] : (JsonElement?)null;
                string controlKind = Js.Str(control, "kind");
                bool agrees = controlKind != null && string.Equals(controlKind, first, StringComparison.OrdinalIgnoreCase);
                string kind = (agrees ? controlKind : first).ToLowerInvariant();
                string title = label, shortForm = label;
                if (kind == Knockout)
                {
                    string channels = words.Length > 1 ? string.Join(" ", words.Skip(1)) : null;
                    if (channels == null && agrees) channels = string.Join(",", Js.Items(Js.At(control, "channels")).Where(c => c.ValueKind == JsonValueKind.String).Select(c => c.GetString()));
                    if (!string.IsNullOrEmpty(channels))
                    {
                        string[] items = channels.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        title = "knockout " + channels;
                        shortForm = items.Length == 0 ? "knockout" : "KO " + items[0] + (items.Length > 1 ? "+" + (items.Length - 1).ToString(CultureInfo.InvariantCulture) : "");
                    }
                    else shortForm = "knockout";
                }
                l.Add(new ExamHeader { Index = i, Label = label, Kind = kind, Short = shortForm, Title = title });
            }
            return l;
        }
    }

    /// <summary>An exam column whose control can leave a genome as it is: a knockout (with the channels it cuts) or brainless.</summary>
    public sealed class ExamReachControl
    {
        public int Col { get; set; }
        public string Kind { get; set; }
        /// <summary>The channels a knockout names; empty for brainless, which names none.</summary>
        public List<string> Channels { get; set; } = new List<string>();
    }

    /// <summary>The columns whose control cuts nothing from one exam genome.</summary>
    public sealed class ExamReachGenome
    {
        public int Genome { get; set; }
        public List<int> Columns { get; set; } = new List<int>();
    }

    public sealed class ExamReachResult
    {
        public List<ExamReachControl> Controls { get; set; } = new List<ExamReachControl>();
        public List<ExamReachGenome> Genomes { get; set; } = new List<ExamReachGenome>();
    }

    /// <summary>
    /// Which exam columns are uninformative for which genome. A control is uninformative for a genome when it leaves that genome exactly as it is: then the brain swims as
    /// <c>own</c> does and own less it is zero by construction. That is a <c>knockout</c> whose channels the genome reads at no port, and <c>brainless</c> on a genome with no
    /// sense link. The count is the nursery's own (<see cref="Evosim.Nursery.KnockoutReach"/>, which runs the exam's knockout), so the room and the exam cannot disagree.
    /// No other kind is ever listed, and a channel that does not parse or a genome that does not load gives no entry.
    /// </summary>
    public static class ExamReach
    {
        /// <summary>The knockout channels a header names, or null when it names none or one of them is not a sensor channel.</summary>
        private static List<Evosim.Core.SensorChannel> ParseChannels(string title)
        {
            const string prefix = "knockout ";
            if (title == null || !title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            var l = new List<Evosim.Core.SensorChannel>();
            foreach (string name in title.Substring(prefix.Length).Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(name, out _) || !Enum.TryParse(name, false, out Evosim.Core.SensorChannel ch) || !Enum.IsDefined(typeof(Evosim.Core.SensorChannel), ch)) return null;
                if (!l.Contains(ch)) l.Add(ch);
            }
            return l.Count == 0 ? null : l;
        }

        /// <param name="headers">The exam's column headings (<see cref="ExamHeaders.Build"/>): a knockout's channels are read from its title, as the heading shows them.</param>
        /// <param name="genomes">Each exam genome's number in the exam and its text.</param>
        /// <param name="config">The config the run's genomes grow under, or null for Core's defaults.</param>
        public static ExamReachResult Build(IReadOnlyList<ExamHeader> headers, IEnumerable<KeyValuePair<int, string>> genomes, Evosim.Core.RunConfig config)
        {
            var controls = new List<ExamReachControl>();
            var channelsOf = new Dictionary<int, Evosim.Core.SensorChannel[]>();
            foreach (ExamHeader h in headers)
            {
                if (h.Kind == "brainless") { controls.Add(new ExamReachControl { Col = h.Index, Kind = "brainless" }); channelsOf[h.Index] = null; }
                else if (h.Kind == ExamHeaders.Knockout && ParseChannels(h.Title) is List<Evosim.Core.SensorChannel> chs)
                {
                    controls.Add(new ExamReachControl { Col = h.Index, Kind = ExamHeaders.Knockout, Channels = chs.Select(c => c.ToString()).ToList() });
                    channelsOf[h.Index] = chs.ToArray();
                }
            }
            var result = new ExamReachResult();
            if (controls.Count == 0) return null;
            Evosim.Core.DevelopmentLimits limits = config != null ? config.Development : Evosim.Core.DevelopmentLimits.Default;
            Evosim.Core.PartShapeRegistry shapes = config != null ? config.Shapes : Evosim.Core.PartShapeRegistry.Standard;
            var seen = new HashSet<int>();
            foreach (KeyValuePair<int, string> g in genomes)
            {
                if (!seen.Add(g.Key)) continue;
                Evosim.Core.Genome genome;
                try { genome = Evosim.Core.GenomeJson.Read(g.Value); }
                catch (Exception e) when (!(e is OutOfMemoryException)) { continue; }
                var cols = new List<int>();
                foreach (ExamReachControl c in controls)
                {
                    try { if (Evosim.Nursery.KnockoutReach.Count(genome, channelsOf[c.Col], limits, shapes, config?.CellTypes) == 0) cols.Add(c.Col); }
                    catch (Exception e) when (!(e is OutOfMemoryException)) { }
                }
                if (cols.Count > 0) result.Genomes.Add(new ExamReachGenome { Genome = g.Key, Columns = cols });
            }
            result.Controls = controls;
            return result.Genomes.Count == 0 ? null : result;
        }
    }

    /// <summary>What one run says of one brain in a comparison: the pooled paired difference and its standard error.</summary>
    public sealed class ExamSide
    {
        public double? Diff { get; set; }
        public double? Se { get; set; }
        /// <summary>The genomes the difference was pooled over.</summary>
        public int N { get; set; }
    }

    public sealed class ExamCompareBrain
    {
        public string Label { get; set; }
        public ExamSide Run { get; set; }
        public ExamSide Control { get; set; }
        /// <summary>Run less control, when both have a pooled difference.</summary>
        public double? Delta { get; set; }
        /// <summary>√(se₁² + se₂²), when both have a standard error.</summary>
        public double? DeltaSe { get; set; }
        /// <summary>True when the two differ by more than two combined standard errors; false when not; null when it cannot be said (<see cref="Why"/>).</summary>
        public bool? Differs { get; set; }
        public string Why { get; set; }
    }

    /// <summary>One run's own brain on one body: its mean net over the draws it swam, and the standard error of that mean.</summary>
    public sealed class ExamBodySide
    {
        public double? Net { get; set; }
        /// <summary>Null from fewer than two numbers (a single genome's single net has none).</summary>
        public double? Se { get; set; }
        /// <summary>The numbers the mean is over: the draws of every genome of the body (<c>draws</c>), or, where a genome's draws are not in the file, the genomes' nets (<c>genomes</c>).</summary>
        public int N { get; set; }
        public string Over { get; set; }
        public int Genomes { get; set; }
    }

    public sealed class ExamBodyCompare
    {
        public int Body { get; set; }
        public ExamBodySide Run { get; set; }
        public ExamBodySide Control { get; set; }
    }

    public sealed class ExamComparison
    {
        /// <summary>Each body either exam swam: this run's own net on it against the control's own net on it (null where that run's exam did not swim the body). Not paired: they are two runs.</summary>
        public List<ExamBodyCompare> Bodies { get; set; } = new List<ExamBodyCompare>();
        public List<int> RunOnlyBodies { get; set; } = new List<int>();
        public List<int> ControlOnlyBodies { get; set; } = new List<int>();
        /// <summary>Genomes whose row names no body, left out of <see cref="Bodies"/>, in this run's exam and in the control's.</summary>
        public int RunUnplaced { get; set; }
        public int ControlUnplaced { get; set; }
        /// <summary>One line on how the two runs' bodies differ, or null when they are the same bodies.</summary>
        public string BodiesNote { get; set; }
        /// <summary>False when the two exams are not on one footing (<see cref="Note"/> says why): the brains are listed, no one is marked.</summary>
        public bool Comparable { get; set; }
        public string Note { get; set; }
        public string RunBasis { get; set; }
        public string ControlBasis { get; set; }
        public string RunOwn { get; set; }
        public string ControlOwn { get; set; }
        public int RunGenomes { get; set; }
        public int ControlGenomes { get; set; }
        /// <summary>The arithmetic, in words, as the screen titles it.</summary>
        public string Rule { get; set; }
        public List<ExamCompareBrain> Brains { get; set; } = new List<ExamCompareBrain>();
    }

    public static class ExamCompare
    {
        public const double Bar = 2.0;

        public const string RuleText = "the difference of two paired means: this run's pooled difference less the control's, with standard error √(se₁² + se₂²); marked where it is more than two of those apart";

        /// <summary>
        /// What a pooled cell is a difference of: the reward's value (<c>score</c>) when the run selected on a reward other than Net, else net. Two exams are
        /// compared only when this is the same for both, since a difference of scores and a difference of nets are not one quantity.
        /// </summary>
        public static string PooledBasis(ExamTable.Result r) => r.Basis == "score" ? "score" : "net";

        /// <summary>
        /// The two exams' pooled paired differences, brain by brain, matched by label. A brain is compared when both exams swam it and both hold a difference and a
        /// standard error for it (the exam's own brain has no difference: it is what the others are subtracted from).
        /// </summary>
        public static ExamComparison Build(ExamTable.Result run, ExamTable.Result control)
        {
            var c = new ExamComparison
            {
                RunBasis = PooledBasis(run), ControlBasis = PooledBasis(control), RunOwn = run.Own, ControlOwn = control.Own,
                RunGenomes = run.Bodies.Count, ControlGenomes = control.Bodies.Count, Rule = RuleText,
            };
            c.Comparable = c.RunBasis == c.ControlBasis;
            if (!c.Comparable)
                c.Note = $"the two exams are not on one footing: this run's is on {c.RunBasis}, the control's on {c.ControlBasis} (the rewards they selected on differ), and a difference of one is not a difference of the other, so none is marked";
            else if (c.RunGenomes != c.ControlGenomes)
                c.Note = $"each difference is pooled over that run's own genomes ({c.RunGenomes} here, {c.ControlGenomes} in the control), which are not the same genomes";
            var labels = new List<string>(run.Brains.Where(b => b != run.Own));
            foreach (string b in control.Brains) if (b != control.Own && !labels.Contains(b)) labels.Add(b);
            foreach (string label in labels)
            {
                var item = new ExamCompareBrain { Label = label, Run = SideOf(run, label), Control = SideOf(control, label) };
                if (item.Run == null || item.Control == null)
                    item.Why = item.Run == null ? "this run's exam did not swim this brain" : "the control's exam did not swim this brain";
                else if (item.Run.Diff.HasValue && item.Control.Diff.HasValue)
                {
                    item.Delta = item.Run.Diff.Value - item.Control.Diff.Value;
                    if (item.Run.Se.HasValue && item.Control.Se.HasValue)
                    {
                        item.DeltaSe = Math.Sqrt(item.Run.Se.Value * item.Run.Se.Value + item.Control.Se.Value * item.Control.Se.Value);
                        if (!c.Comparable) item.Why = "not on one footing";
                        else item.Differs = Math.Abs(item.Delta.Value) > Bar * item.DeltaSe.Value;
                    }
                    else item.Why = "a standard error needs two genomes in each exam";
                }
                else item.Why = "no pooled difference in one of the exams";
                c.Brains.Add(item);
            }
            AddBodies(c, run, control);
            return c;
        }

        /// <summary>The own brain's net on each body, in each run (<see cref="ExamComparison.Bodies"/>), and how the two runs' bodies differ.</summary>
        private static void AddBodies(ExamComparison c, ExamTable.Result run, ExamTable.Result control)
        {
            c.RunUnplaced = run.Bodies.Count(b => !b.BodyIndex.HasValue);
            c.ControlUnplaced = control.Bodies.Count(b => !b.BodyIndex.HasValue);
            var all = new SortedSet<int>();
            foreach (var b in run.Bodies) if (b.BodyIndex.HasValue) all.Add(b.BodyIndex.Value);
            foreach (var b in control.Bodies) if (b.BodyIndex.HasValue) all.Add(b.BodyIndex.Value);
            foreach (int body in all)
            {
                ExamBodySide r = BodySide(run, body), k = BodySide(control, body);
                c.Bodies.Add(new ExamBodyCompare { Body = body, Run = r, Control = k });
                if (r != null && k == null) c.RunOnlyBodies.Add(body);
                if (k != null && r == null) c.ControlOnlyBodies.Add(body);
            }
            var parts = new List<string>();
            string Join(List<int> l) => string.Join(", ", l);
            if (c.RunOnlyBodies.Count > 0) parts.Add($"{(c.RunOnlyBodies.Count == 1 ? "body" : "bodies")} {Join(c.RunOnlyBodies)} {(c.RunOnlyBodies.Count == 1 ? "is" : "are")} in this run's exam only");
            if (c.ControlOnlyBodies.Count > 0) parts.Add($"{(c.ControlOnlyBodies.Count == 1 ? "body" : "bodies")} {Join(c.ControlOnlyBodies)} {(c.ControlOnlyBodies.Count == 1 ? "is" : "are")} in the control's exam only");
            if (c.RunUnplaced > 0 || c.ControlUnplaced > 0) parts.Add($"{c.RunUnplaced + c.ControlUnplaced} genome row(s) name no body and are left out");
            if (parts.Count > 0) c.BodiesNote = "the two runs' bodies differ: " + string.Join("; ", parts);
        }

        /// <summary>
        /// The own brain's mean net on one body: over the draws of every genome of that body where their rows hold them, else over the genomes' nets. Null when the
        /// exam swam no genome of that body (or none that landed).
        /// </summary>
        private static ExamBodySide BodySide(ExamTable.Result r, int body)
        {
            var genomes = r.Bodies.Where(b => b.BodyIndex == body && (b.OwnPer != null && b.OwnPer.Length > 0 || b.OwnNet.HasValue)).ToList();
            if (genomes.Count == 0) return null;
            var values = new List<double>();
            string over;
            if (genomes.All(g => g.OwnPer != null && g.OwnPer.Length > 0)) { foreach (var g in genomes) values.AddRange(g.OwnPer); over = "draws"; }
            else { foreach (var g in genomes) if (g.OwnNet.HasValue) values.Add(g.OwnNet.Value); over = "genomes"; }
            if (values.Count == 0) return null;
            double mean = values.Average();
            double? se = null;
            if (values.Count >= 2) se = Math.Sqrt(values.Sum(x => (x - mean) * (x - mean)) / (values.Count - 1) / values.Count);
            return new ExamBodySide { Net = mean, Se = se, N = values.Count, Over = over, Genomes = genomes.Count };
        }

        private static ExamSide SideOf(ExamTable.Result r, string label)
        {
            int i = r.Brains.IndexOf(label);
            if (i < 0) return null;
            ExamTable.Cell cell = r.Pooled[i];
            return new ExamSide { Diff = cell.Diff, Se = cell.Se, N = cell.N };
        }
    }

    /// <summary>The stall alarm: a run its record calls running whose files have not grown for more than three times its median generation time.</summary>
    public sealed class Stall
    {
        /// <summary>True when the alarm is up.</summary>
        public bool Stalled { get; set; }
        /// <summary>False when the rule could not be applied (the run is not marked running, or no median generation time is known yet): <see cref="Reason"/> says why.</summary>
        public bool Evaluated { get; set; }
        public string Reason { get; set; }
        public double? MedianGenerationSeconds { get; set; }
        public double? LimitSeconds { get; set; }
        public double? SinceWriteSeconds { get; set; }
        public DateTime? LastWriteUtc { get; set; }
        public string LastWriteFile { get; set; }
    }

    public static class StallRule
    {
        public const double Factor = 3.0;

        /// <param name="cumulativeSeconds">The generation rows' wall seconds, cumulative as the rows hold them.</param>
        /// <param name="writes">The run's files that grow, and when each was last written.</param>
        /// <param name="markedRunning">True when the run's record says running (and its process is not known to be gone).</param>
        public static Stall Evaluate(IReadOnlyList<double> cumulativeSeconds, IReadOnlyList<(string file, DateTime? writtenUtc)> writes, DateTime nowUtc, bool markedRunning)
        {
            var s = new Stall();
            double? median = MedianGap(cumulativeSeconds);
            s.MedianGenerationSeconds = median;
            if (median.HasValue) s.LimitSeconds = Factor * median.Value;
            (string file, DateTime? at) last = (null, null);
            foreach (var w in writes)
                if (w.writtenUtc.HasValue && (!last.at.HasValue || w.writtenUtc.Value > last.at.Value)) last = (w.file, w.writtenUtc);
            s.LastWriteUtc = last.at;
            s.LastWriteFile = last.file;
            if (last.at.HasValue) s.SinceWriteSeconds = Math.Max(0, (nowUtc - last.at.Value).TotalSeconds);
            if (!markedRunning) { s.Reason = "the run is not marked running"; return s; }
            if (!median.HasValue) { s.Reason = "fewer than two generation rows carry wall seconds, so there is no median generation time to measure a silence against"; return s; }
            if (!s.SinceWriteSeconds.HasValue) { s.Reason = "no file of the run has been written"; return s; }
            s.Evaluated = true;
            s.Stalled = s.SinceWriteSeconds.Value > s.LimitSeconds.Value;
            return s;
        }

        /// <summary>The median of the gaps between consecutive cumulative wall seconds (a gap of zero or less is not a generation's time), or null with fewer than one.</summary>
        public static double? MedianGap(IReadOnlyList<double> cumulativeSeconds)
        {
            var gaps = new List<double>();
            for (int i = 1; i < cumulativeSeconds.Count; i++)
            {
                double g = cumulativeSeconds[i] - cumulativeSeconds[i - 1];
                if (g > 0) gaps.Add(g);
            }
            if (gaps.Count == 0) return null;
            gaps.Sort();
            int n = gaps.Count;
            return n % 2 == 1 ? gaps[n / 2] : (gaps[n / 2 - 1] + gaps[n / 2]) / 2.0;
        }
    }

    /// <summary>When an exam that is under way will end, and what that is computed from.</summary>
    public sealed class ExamEnd
    {
        /// <summary><c>estimated</c>, <c>complete</c> or <c>unknown</c> (with <see cref="Reason"/>).</summary>
        public string State { get; set; }
        public string Reason { get; set; }
        public int RowsDone { get; set; }
        public int? Brains { get; set; }
        public int? Genomes { get; set; }
        public int? RowsExpected { get; set; }
        public DateTime? StartUtc { get; set; }
        public DateTime? LastRowUtc { get; set; }
        public double? SecondsPerRow { get; set; }
        public DateTime? EndsUtc { get; set; }
        /// <summary>The computation, in words: what the estimate stands on, so that a reader can check it.</summary>
        public string Basis { get; set; }
    }

    public static class ExamEstimate
    {
        /// <summary>
        /// The exam is brains × genomes rows. At the pace of the rows so far (the time from the exam's start to its last row, over the rows written), the rows that
        /// remain end it that long after the last row. Nothing is guessed: a missing plan, a missing start or no row yet is said as <c>unknown</c>.
        /// </summary>
        public static ExamEnd Compute(int rowsDone, int? brains, int? genomes, DateTime? startUtc, DateTime? lastRowUtc, string startedFrom)
        {
            var e = new ExamEnd { RowsDone = rowsDone, Brains = brains, Genomes = genomes, StartUtc = startUtc, LastRowUtc = lastRowUtc };
            if (!brains.HasValue || !genomes.HasValue || brains.Value <= 0 || genomes.Value <= 0)
            {
                e.State = "unknown";
                e.Reason = "the plan's brains and genomes are not both known (" + (brains.HasValue ? "brains " + brains : "no brain count: the run has no recipe") + ", " + (genomes.HasValue ? "genomes " + genomes : "no genome count") + ")";
                return e;
            }
            e.RowsExpected = brains.Value * genomes.Value;
            if (rowsDone >= e.RowsExpected.Value) { e.State = "complete"; e.Basis = $"{rowsDone} of {e.RowsExpected} rows ({brains} brains × {genomes} genomes) are written"; return e; }
            if (rowsDone <= 0 || !lastRowUtc.HasValue) { e.State = "unknown"; e.Reason = "no exam row has been written yet, so there is no pace to take"; return e; }
            if (!startUtc.HasValue) { e.State = "unknown"; e.Reason = "the exam's start is not known"; return e; }
            double elapsed = (lastRowUtc.Value - startUtc.Value).TotalSeconds;
            if (elapsed <= 0) { e.State = "unknown"; e.Reason = "the last row is not later than the exam's start, so the pace cannot be taken"; return e; }
            e.SecondsPerRow = elapsed / rowsDone;
            e.EndsUtc = lastRowUtc.Value.AddSeconds(e.SecondsPerRow.Value * (e.RowsExpected.Value - rowsDone));
            e.State = "estimated";
            e.Basis = $"{rowsDone} of {e.RowsExpected} rows ({brains} brains × {genomes} genomes) written in {Minutes(elapsed)} from {startedFrom} to the last row, {e.SecondsPerRow.Value.ToString("0.#", CultureInfo.InvariantCulture)} s a row; " +
                      $"the {e.RowsExpected.Value - rowsDone} rows that remain, at that pace, end it {Minutes(e.SecondsPerRow.Value * (e.RowsExpected.Value - rowsDone))} after the last row";
            return e;
        }

        private static string Minutes(double seconds) => (seconds / 60.0).ToString("0.#", CultureInfo.InvariantCulture) + " min";
    }

    /// <summary>Which rows of the activity log are about one run.</summary>
    public static class ActivityFilter
    {
        /// <summary>The operations whose rows name a run.</summary>
        public static readonly string[] Operations = { "run.launch", "run.rerun", "run.stop", "episode.step" };

        /// <summary>
        /// The run's own rows, in the order given: its launch (the row whose result names its folder or id, or a launch refused for its name), a rerun of its entry
        /// or the rerun that made it, its stops, and the episodes stepped on it. <paramref name="isRun"/> says whether a run address a row holds (an id, a number, a
        /// name) is this run; <paramref name="isFolder"/> whether a folder a row holds (main-relative) is this run's.
        /// </summary>
        public static List<JsonElement> ForRun(IEnumerable<JsonElement> rows, Func<string, bool> isRun, Func<string, bool> isFolder)
        {
            bool Run(string s) => !string.IsNullOrEmpty(s) && isRun(s);
            bool Folder(string s) => !string.IsNullOrEmpty(s) && isFolder(s);
            var l = new List<JsonElement>();
            foreach (JsonElement row in rows)
            {
                string op = Js.Str(row, "operation");
                if (!Operations.Contains(op)) continue;
                bool refused = Js.Str(row, "outcome") != "ok";
                bool mine = false;
                switch (op)
                {
                    case "run.launch":
                        mine = Run(Js.Str(row, "result", "id")) || Folder(Js.Str(row, "result", "folder")) || (refused && Run(Js.Str(row, "args", "name")));
                        break;
                    case "run.rerun":
                        // the rerun that made this run, and a rerun of this run's entry
                        mine = Run(Js.Str(row, "result", "id")) || Folder(Js.Str(row, "result", "folder")) || Run(Js.Str(row, "args", "entry")) || (refused && Run(Js.Str(row, "args", "name")));
                        break;
                    case "run.stop":
                        mine = Run(Js.Str(row, "args", "id")) || Run(Js.Str(row, "result", "id"));
                        break;
                    case "episode.step":
                        mine = Run(Js.Str(row, "args", "run")) || Run(Js.Str(row, "result", "run"));
                        break;
                }
                if (mine) l.Add(row);
            }
            return l;
        }
    }
}
