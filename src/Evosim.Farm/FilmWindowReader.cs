using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>What a film window's last line says about it (<see cref="FilmWindow"/>).</summary>
    public sealed class FilmWindowVerdict
    {
        /// <summary><c>faithful</c>, <c>cousin</c> or <c>unverified</c>, as written.</summary>
        public string Verdict;

        public string Reason;

        /// <summary>The run directory's name, as every verdict carries it.</summary>
        public string RunName;

        /// <summary>
        /// The run directory's full path, or null for a window written before the field existed.
        /// </summary>
        public string RunDirectory;

        public string Arm;
        public double From;
        public double To;
        public double Fps;
        public double PhysicsDt;

        /// <summary>The checkpoint's file name, or null when the window was founded.</summary>
        public string RestoredFrom;

        public double RestoredAt;
        public int Frames;
        public int Bodies;
        public int Births;
        public int Deaths;
        public int Bites;
        public int BirthsWithoutAGenome;
        public int Rows;
        public int RowsAgreed;
        public int RowsBefore;
        public int RowsBeforeAgreed;

        /// <summary>The second a row parted at, or NaN when none did.</summary>
        public double PartedAt = double.NaN;

        public string PartedField;

        /// <summary>The config's hash line when it differs from the run's, or null.</summary>
        public string ConfigDiffers;

        /// <summary>Every source line the verdict names: a differing hash, or a lossy checkpoint.</summary>
        public readonly List<string> SourcesDiffer = new List<string>();

        public bool EndedExtinct;
    }

    /// <summary>One birth, death or bite inside a window, from <c>events.jsonl</c>.</summary>
    public struct FilmWindowEvent
    {
        /// <summary><c>b</c>, <c>d</c> or <c>k</c>: the lineage row's own <c>e</c>.</summary>
        public char Kind;

        public double Seconds;
        public long Id;

        /// <summary>A birth's parent, or -1 (a founder, or not a birth).</summary>
        public long Parent;

        /// <summary>A bite's attacker, or -1.</summary>
        public long By;

        /// <summary>A death's cause code (<c>starved</c>, <c>eaten</c>, <c>diverged</c>), or null.</summary>
        public string Cause;

        /// <summary>The row as the run wrote it.</summary>
        public string Row;
    }

    /// <summary>A body's plan from a window's <c>plans.jsonl</c>: its module counts and lost parts.</summary>
    public sealed class FilmWindowPlan
    {
        /// <summary>The second the plan was written at: the window's start, or the metabolic step it moved on.</summary>
        public double Seconds;

        public int[] ModuleCounts;
        public List<int[]> LostPaths;
    }

    /// <summary>
    /// A film window read back: the verdict, the frames, the genomes, the plans and the events,
    /// for a player that draws the window without stepping anything
    /// (<c>logbook/specs/record-and-film-spec.md</c>, B2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Plain .NET, so the theatre and a test read it alike.</b> The theatre reaches it through
    /// the farm package, as it reaches <see cref="PoseStreamReader"/>, and <c>FilmWindowTests</c>
    /// reads what a window wrote with it. Nothing here knows about Unity.
    /// </para>
    /// <para>
    /// <b>The provenance word is the verdict's.</b> <see cref="ProvenanceWord"/> says FAITHFUL only
    /// when the verdict line says <c>faithful</c>, COUSIN when it says <c>cousin</c>, and UNVERIFIED
    /// for an unverified window and for one with no verdict line at all, which is a window killed
    /// before its end. A player never decides faithfulness itself: the farm compared the rows and
    /// the player has nothing to compare.
    /// </para>
    /// <para>
    /// <b>A plan is at or before the frame.</b> The window writes a body's plan at the start where
    /// it differs from the genome's minimum and at every metabolic step it moves, and a frame at a
    /// physics step after that metabolic step draws the body the step built. So the plan a frame
    /// wants is the newest one written at or before the frame's second, and a body with no plan
    /// row at or before it develops at its genome's minimum (<see cref="PlanAt"/> returns null).
    /// </para>
    /// </remarks>
    public sealed class FilmWindowReader : IDisposable
    {
        private const double Eps = 1e-6;

        private readonly Dictionary<long, string> _genomes = new Dictionary<long, string>();
        private readonly Dictionary<long, List<FilmWindowPlan>> _plans = new Dictionary<long, List<FilmWindowPlan>>();
        private readonly List<FilmWindowEvent> _events = new List<FilmWindowEvent>();
        private readonly Dictionary<long, int> _birthOf = new Dictionary<long, int>();
        private readonly Dictionary<long, int> _deathOf = new Dictionary<long, int>();
        private readonly List<string> _notes = new List<string>();

        /// <summary>The window's directory, full path.</summary>
        public string Directory { get; }

        /// <summary>The verdict, or null when <c>identity.jsonl</c> ends without one.</summary>
        public FilmWindowVerdict Verdict { get; private set; }

        /// <summary>The frames, open for the life of the reader.</summary>
        public PoseStreamReader Stream { get; private set; }

        /// <summary>Things a reader of the window is owed: a torn member, an unreadable row.</summary>
        public IReadOnlyList<string> Notes => _notes;

        /// <summary>Every event, in the window's order.</summary>
        public IReadOnlyList<FilmWindowEvent> Events => _events;

        /// <summary>Every id with a genome row.</summary>
        public IEnumerable<long> GenomeIds => _genomes.Keys;

        public int GenomeCount => _genomes.Count;

        public int PlanRows { get; private set; }

        public int FrameCount => Stream.Frames.Length;

        private FilmWindowReader(string directory)
        {
            Directory = directory;
        }

        /// <summary>Whether a directory holds a film window: its pose stream is the one file every window has.</summary>
        public static bool IsWindow(string directory) =>
            !string.IsNullOrEmpty(directory) &&
            File.Exists(Path.Combine(directory, FilmWindow.PosesFileName));

        /// <summary>
        /// Reads a window. Throws when the directory has no pose stream, which is the one thing a
        /// player cannot do without; every other file missing is a note and an empty answer.
        /// </summary>
        public static FilmWindowReader Open(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("No window directory.");

            string full = Path.GetFullPath(directory);
            string poses = Path.Combine(full, FilmWindow.PosesFileName);

            if (!File.Exists(poses))
            {
                throw new FileNotFoundException(
                    full + " holds no " + FilmWindow.PosesFileName + ", so it is not a film window.", poses);
            }

            var reader = new FilmWindowReader(full);

            try
            {
                reader.Stream = PoseStreamReader.Open(poses);
                reader.ReadVerdict();
                reader.ReadGenomes();
                reader.ReadPlans();
                reader.ReadEvents();
            }
            catch
            {
                reader.Dispose();
                throw;
            }

            return reader;
        }

        // ---------------------------------------------------------------- the verdict

        /// <summary>
        /// FAITHFUL, COUSIN or UNVERIFIED: the verdict's own word, and UNVERIFIED when there is no
        /// verdict or it says something this build does not know.
        /// </summary>
        public string ProvenanceWord => WordOf(Verdict?.Verdict);

        /// <summary>The word for a verdict string, by the rule in the class remarks.</summary>
        public static string WordOf(string verdict) =>
            verdict == FilmWindow.Faithful ? "FAITHFUL"
            : verdict == FilmWindow.Cousin ? "COUSIN"
            : "UNVERIFIED";

        /// <summary>Whether the verdict says faithful. The only test a caller may use to claim it.</summary>
        public bool Faithful => Verdict != null && Verdict.Verdict == FilmWindow.Faithful;

        private void ReadVerdict()
        {
            string path = Path.Combine(Directory, FilmWindow.IdentityFileName);

            if (!File.Exists(path))
            {
                _notes.Add("no " + FilmWindow.IdentityFileName + ", so the window has no verdict");
                return;
            }

            string last = null;

            foreach (string line in SharedLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || !trimmed.EndsWith("}", StringComparison.Ordinal)) continue;
                if (trimmed.IndexOf("\"verdict\"", StringComparison.Ordinal) >= 0) last = trimmed;
            }

            if (last == null)
            {
                _notes.Add(FilmWindow.IdentityFileName + " ends without a verdict: the window stopped before its end");
                return;
            }

            JsonNode v = Json.Parse(last);
            var verdict = new FilmWindowVerdict
            {
                Verdict = Str(v, "verdict"),
                Reason = Str(v, "reason"),
                RunName = Str(v, "run"),
                RunDirectory = Str(v, "runDirectory"),
                Arm = Str(v, "arm"),
                From = Num(v, "from"),
                To = Num(v, "to"),
                Fps = Num(v, "fps"),
                PhysicsDt = Num(v, "physicsDt"),
                RestoredFrom = Str(v, "restoredFrom"),
                RestoredAt = Num(v, "restoredAt"),
                Frames = Int(v, "frames"),
                Bodies = Int(v, "bodies"),
                Births = Int(v, "births"),
                Deaths = Int(v, "deaths"),
                Bites = Int(v, "bites"),
                BirthsWithoutAGenome = Int(v, "birthsWithoutAGenome"),
                Rows = Int(v, "rows"),
                RowsAgreed = Int(v, "rowsAgreed"),
                RowsBefore = Int(v, "rowsBefore"),
                RowsBeforeAgreed = Int(v, "rowsBeforeAgreed"),
                PartedAt = Num(v, "partedAt"),
                PartedField = Str(v, "partedField"),
                ConfigDiffers = Str(v, "configDiffers"),
                EndedExtinct = v.Has("endedExtinct") && v["endedExtinct"].Kind == JsonNode.NodeKind.Bool && v["endedExtinct"].AsBool(),
            };

            if (v.Has("sourcesDiffer") && v["sourcesDiffer"].Kind == JsonNode.NodeKind.Array)
            {
                foreach (JsonNode line in v["sourcesDiffer"].Items())
                {
                    if (line.Kind == JsonNode.NodeKind.String) verdict.SourcesDiffer.Add(line.AsString());
                }
            }

            Verdict = verdict;
        }

        private static string Str(JsonNode v, string name) =>
            v.Has(name) && v[name].Kind == JsonNode.NodeKind.String ? v[name].AsString() : null;

        private static double Num(JsonNode v, string name) =>
            v.Has(name) && v[name].Kind == JsonNode.NodeKind.Number ? v[name].AsDouble() : double.NaN;

        private static int Int(JsonNode v, string name) =>
            v.Has(name) && v[name].Kind == JsonNode.NodeKind.Number ? v[name].AsInt() : 0;

        // ---------------------------------------------------------------- the frames

        /// <summary>The second a frame was recorded at.</summary>
        public double SecondOf(int frame) => Stream.Frames[frame].Seconds;

        /// <summary>One frame, read from the stream.</summary>
        public PoseFrame ReadFrame(int frame) => Stream.Read(frame);

        /// <summary>
        /// The last frame recorded at or before a second, or -1 when the second is before the first
        /// frame. A frame sits up to one physics step after its nominal second, so a second asked
        /// for by its nominal value finds the frame before it; <see cref="NearestFrame"/> is the
        /// one a caller naming a nominal second wants.
        /// </summary>
        public int FrameAtOrBefore(double seconds)
        {
            PoseFrameRef[] frames = Stream.Frames;
            int lo = 0, hi = frames.Length - 1, found = -1;

            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;

                if (frames[mid].Seconds <= seconds + Eps)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return found;
        }

        /// <summary>The frame nearest a second, clamped to the window, or -1 for an empty stream.</summary>
        public int NearestFrame(double seconds)
        {
            int count = FrameCount;
            if (count == 0) return -1;

            int before = FrameAtOrBefore(seconds);
            if (before < 0) return 0;
            if (before >= count - 1) return count - 1;

            return seconds - SecondOf(before) <= SecondOf(before + 1) - seconds ? before : before + 1;
        }

        // ---------------------------------------------------------------- the genomes

        /// <summary>A body's genome row, the run's own form, or false when the window has none for it.</summary>
        public bool TryGenome(long id, out string row) => _genomes.TryGetValue(id, out row);

        private void ReadGenomes()
        {
            string path = Path.Combine(Directory, FilmWindow.GenomesFileName);

            if (!File.Exists(path))
            {
                _notes.Add("no " + FilmWindow.GenomesFileName + ", so no body can be developed");
                return;
            }

            IEnumerable<string> lines = GzipMemberReader.ReadLines(path, out string torn);
            int unreadable = 0;

            foreach (string line in lines)
            {
                if (line.Length == 0) continue;

                long id;

                try
                {
                    id = GenomeJson.ReadId(line);
                }
                catch (Exception)
                {
                    unreadable++;
                    continue;
                }

                if (id < 0)
                {
                    unreadable++;
                    continue;
                }

                if (!_genomes.ContainsKey(id)) _genomes[id] = line;
            }

            if (torn != null) _notes.Add(FilmWindow.GenomesFileName + ": " + torn);
            if (unreadable > 0) _notes.Add(unreadable + " genome row(s) carry no readable id and are not drawn");
        }

        // ---------------------------------------------------------------- the plans

        /// <summary>
        /// The plan a body stood on at a second: the newest row written at or before it, or null
        /// when none was, which is the genome's own minimum.
        /// </summary>
        public FilmWindowPlan PlanAt(long id, double seconds)
        {
            if (!_plans.TryGetValue(id, out List<FilmWindowPlan> plans)) return null;

            FilmWindowPlan found = null;

            for (int i = 0; i < plans.Count; i++)
            {
                if (plans[i].Seconds > seconds + Eps) break;
                found = plans[i];
            }

            return found;
        }

        /// <summary>Every plan row for a body, in the order written, or an empty list.</summary>
        public IReadOnlyList<FilmWindowPlan> PlansOf(long id) =>
            _plans.TryGetValue(id, out List<FilmWindowPlan> plans) ? plans : (IReadOnlyList<FilmWindowPlan>)Array.Empty<FilmWindowPlan>();

        private void ReadPlans()
        {
            string path = Path.Combine(Directory, FilmWindow.PlansFileName);
            if (!File.Exists(path)) return;

            foreach (string line in SharedLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || !trimmed.EndsWith("}", StringComparison.Ordinal)) continue;

                JsonNode row = Json.Parse(trimmed);
                long id = (long)row["id"].AsDouble();

                var counts = new List<int>();
                foreach (JsonNode count in row["moduleCounts"].Items()) counts.Add(count.AsInt());

                var lost = new List<int[]>();
                foreach (JsonNode lostPath in row["lostPaths"].Items())
                {
                    var steps = new List<int>();
                    foreach (JsonNode step in lostPath.Items()) steps.Add(step.AsInt());
                    lost.Add(steps.ToArray());
                }

                var plan = new FilmWindowPlan
                {
                    Seconds = row["t"].AsDouble(),
                    ModuleCounts = counts.ToArray(),
                    LostPaths = lost,
                };

                if (!_plans.TryGetValue(id, out List<FilmWindowPlan> plans))
                {
                    plans = new List<FilmWindowPlan>();
                    _plans[id] = plans;
                }

                plans.Add(plan);
                PlanRows++;
            }

            // Written in time order already; sorted anyway, stably, so a reader never depends on it.
            foreach (List<FilmWindowPlan> plans in _plans.Values)
            {
                StableSortBySecond(plans);
            }
        }

        private static void StableSortBySecond(List<FilmWindowPlan> plans)
        {
            for (int i = 1; i < plans.Count; i++)
            {
                FilmWindowPlan item = plans[i];
                int j = i - 1;

                while (j >= 0 && plans[j].Seconds > item.Seconds)
                {
                    plans[j + 1] = plans[j];
                    j--;
                }

                plans[j + 1] = item;
            }
        }

        // ---------------------------------------------------------------- the events

        /// <summary>A body's birth inside the window, or false when it was alive at the start.</summary>
        public bool TryBirthOf(long id, out FilmWindowEvent birth)
        {
            if (_birthOf.TryGetValue(id, out int index))
            {
                birth = _events[index];
                return true;
            }

            birth = default;
            return false;
        }

        /// <summary>A body's death inside the window, or false when it outlived it.</summary>
        public bool TryDeathOf(long id, out FilmWindowEvent death)
        {
            if (_deathOf.TryGetValue(id, out int index))
            {
                death = _events[index];
                return true;
            }

            death = default;
            return false;
        }

        /// <summary>The events in (from, to], in the window's order.</summary>
        public IEnumerable<FilmWindowEvent> EventsBetween(double from, double to)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                FilmWindowEvent e = _events[i];
                if (e.Seconds > from + Eps && e.Seconds <= to + Eps) yield return e;
            }
        }

        private void ReadEvents()
        {
            string path = Path.Combine(Directory, FilmWindow.EventsFileName);
            if (!File.Exists(path)) return;

            foreach (string line in SharedLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || !trimmed.EndsWith("}", StringComparison.Ordinal)) continue;

                JsonNode row = Json.Parse(trimmed);
                string kind = Str(row, "e");
                if (string.IsNullOrEmpty(kind)) continue;

                var e = new FilmWindowEvent
                {
                    Kind = kind[0],
                    Seconds = row["t"].AsDouble(),
                    Id = (long)row["id"].AsDouble(),
                    Parent = row.Has("p") && row["p"].Kind == JsonNode.NodeKind.Number ? (long)row["p"].AsDouble() : -1L,
                    By = row.Has("by") && row["by"].Kind == JsonNode.NodeKind.Number ? (long)row["by"].AsDouble() : -1L,
                    Cause = Str(row, "c"),
                    Row = trimmed,
                };

                if (e.Kind == 'b' && !_birthOf.ContainsKey(e.Id)) _birthOf[e.Id] = _events.Count;
                if (e.Kind == 'd' && !_deathOf.ContainsKey(e.Id)) _deathOf[e.Id] = _events.Count;

                _events.Add(e);
            }
        }

        // ---------------------------------------------------------------- housekeeping

        /// <summary>
        /// A text file's lines, read under a writer's share mode, so a window still being written
        /// is readable (<c>JsonlWriter.ReadRows</c>' reason).
        /// </summary>
        private static IEnumerable<string> SharedLines(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false)))
            {
                string line;
                while ((line = reader.ReadLine()) != null) yield return line;
            }
        }

        /// <summary>One line for a log: what the window is and what it holds.</summary>
        public string Describe()
        {
            FilmWindowVerdict v = Verdict;
            var inv = CultureInfo.InvariantCulture;

            string span = FrameCount > 0
                ? string.Format(inv, "{0:0.###} to {1:0.###} s", SecondOf(0), SecondOf(FrameCount - 1))
                : "no frame";

            return string.Format(
                inv,
                "{0}: {1} frame(s), {2}, {3} genome(s), {4} plan row(s), {5} event(s); {6}{7}",
                Directory, FrameCount, span, GenomeCount, PlanRows, _events.Count,
                ProvenanceWord,
                v == null ? " (no verdict)" : " (" + v.Reason + ")");
        }

        public void Dispose()
        {
            Stream?.Dispose();
            Stream = null;
        }
    }
}
