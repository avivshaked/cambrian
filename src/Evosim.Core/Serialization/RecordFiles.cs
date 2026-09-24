using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Evosim.Core
{
    /// <summary>
    /// Which record a run directory holds — <c>logbook/specs/record-and-film-spec.md</c>, Part A.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>1, the JSONL record</b>, is what every run wrote before the console farm's round 49
    /// build and what the Unity farm still writes: every living genome in every snapshot,
    /// <c>positions.jsonl</c>, and <c>poses.jsonl</c> beside it.
    /// </para>
    /// <para>
    /// <b>2, the compact record</b>: each genome once, in <c>genomes.jsonl.gz</c>, when its body
    /// is admitted; snapshots of slim rows (<c>snapshots/NNNNNNNNN.jsonl.gz</c>) carrying what
    /// changes in a life and the body fraction; <c>positions.jsonl.gz</c>; the state stream in
    /// place of <c>poses.jsonl</c>; and compressed checkpoints. All of it gzip in members
    /// (<see cref="GzipMembers"/>).
    /// </para>
    /// <para>
    /// A reader is told which by <c>run.json</c>'s <c>recordFormat</c>, or by the files present
    /// (<see cref="RecordFiles.FormatOf(string, out string)"/>), and never by guessing inside a
    /// file.
    /// </para>
    /// </remarks>
    public static class RunRecordFormat
    {
        /// <summary>The JSONL record: genomes in every snapshot, positions and poses as JSONL.</summary>
        public const int Jsonl = 1;

        /// <summary>The compact record: genomes once, slim snapshots, gzip in members.</summary>
        public const int Compact = 2;

        /// <summary>What a run writes when its launcher does not say.</summary>
        public const int Newest = Compact;

        public static bool IsKnown(int format) => format == Jsonl || format == Compact;

        /// <summary>A few words for a label or a log line.</summary>
        public static string Describe(int format) =>
            format == Jsonl
                ? "record format 1 (JSONL)"
                : format == Compact
                    ? "record format 2 (genomes once, gzip in members)"
                    : "record format " + format.ToString(CultureInfo.InvariantCulture) + " (unknown)";
    }

    /// <summary>
    /// One snapshot, read from either record: one full row per body, the genome joined.
    /// </summary>
    /// <remarks>
    /// A row here is exactly what format 1 wrote for the body: the id, the module counts and the
    /// lost part paths when the body had them, then the genome. So a reader written against the
    /// old snapshot reads a new one's rows unchanged (<see cref="RecordFiles.Join"/>).
    /// </remarks>
    public sealed class SnapshotRead
    {
        /// <summary>The record the rows were read from.</summary>
        public int Format;

        /// <summary>The snapshot file.</summary>
        public string File;

        /// <summary>One row per body the read could give a genome, in the file's order.</summary>
        public string[] Rows = Array.Empty<string>();

        /// <summary>The organism id on each row, or <see cref="GenomeJson.NoId"/> for a row without one.</summary>
        public long[] Ids = Array.Empty<long>();

        /// <summary>
        /// Each row's body fraction, NaN where the record does not say: every format 1 row, and a
        /// row the converter wrote from one.
        /// </summary>
        public float[] BodyFractions = Array.Empty<float>();

        /// <summary>Slim rows refused because <c>genomes.jsonl.gz</c> holds no genome for their id.</summary>
        public int WithoutAGenome;

        /// <summary>The ids of those rows, in the file's order.</summary>
        public readonly List<long> RefusedIds = new List<long>();

        /// <summary>A torn member skipped in the snapshot or the genome file, or nothing.</summary>
        public readonly List<string> Notes = new List<string>();

        /// <summary>Which files were read, for a label or a log line.</summary>
        public string Source = "";
    }

    /// <summary>
    /// The files of a run's record, and the readers that take both formats.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Beside <see cref="RunDirectory"/> rather than inside it.</b> That class writes a run and
    /// is shared with the Unity farm, whose writes must stay byte for byte what they were; this
    /// one reads any run, from either farm and either record, for the theatre and the tests.
    /// </para>
    /// <para>
    /// <b>The slim row and the join.</b> A format 2 snapshot row is
    /// <c>{"id":…,"moduleCounts":[…],"lostPaths":[[…]],"bf":…}</c>, the two plan fields omitted
    /// when empty exactly as <see cref="GenomeJson.Write"/> omits them, and the body fraction
    /// last. A genome row is <see cref="GenomeJson.Write"/> with the id and nothing else. Both
    /// writers put the id first, and the old full row carried the id, the plan and then the
    /// genome, so the join is textual: the slim row without its body fraction and closing brace,
    /// a comma, and the genome row after its id. The result is the old row to the byte, which is
    /// what lets the converter check a converted run row for row by string equality.
    /// </para>
    /// </remarks>
    public static class RecordFiles
    {
        /// <summary>Format 2's genomes, one row per body admitted, gzip in members.</summary>
        public const string GenomesName = "genomes.jsonl.gz";

        /// <summary>Format 1's positions.</summary>
        public const string PositionsName = "positions.jsonl";

        /// <summary>Format 2's positions: the same rows, one member per sample.</summary>
        public const string PositionsMembersName = "positions.jsonl.gz";

        /// <summary>Format 1's poses. Format 2 writes the state stream instead.</summary>
        public const string PosesName = "poses.jsonl";

        /// <summary>The snapshots directory, in both formats.</summary>
        public const string SnapshotsDirectory = "snapshots";

        /// <summary>
        /// The mark <c>scripts/record-convert.py</c> leaves beside a format 1 run once its format 2
        /// files are written and checked row for row. Its presence makes the run read as format 2.
        /// </summary>
        public const string ConvertedName = "record-converted.json";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ which record

        /// <summary>Which record a run directory holds.</summary>
        public static int FormatOf(string runDirectory) => FormatOf(runDirectory, out _);

        /// <summary>
        /// Which record a run directory holds, and how that was decided.
        /// </summary>
        /// <remarks>
        /// In order: <c>run.json</c>'s <c>recordFormat</c>, which every console farm run from
        /// this build carries; the converter's mark; a <c>run.json</c> without the field, which is
        /// every run before this build and every Unity farm run, and is format 1; and, in a
        /// directory with no manifest at all (a fixture), the files present. An unknown
        /// <c>recordFormat</c> is refused rather than read as the nearest one.
        /// </remarks>
        public static int FormatOf(string runDirectory, out string how)
        {
            if (runDirectory == null) throw new ArgumentNullException(nameof(runDirectory));

            string manifest = Path.Combine(runDirectory, "run.json");
            bool haveManifest = File.Exists(manifest);

            if (haveManifest)
            {
                JsonNode root = Json.Parse(ReadShared(manifest));

                if (root.Has("recordFormat"))
                {
                    int format = root["recordFormat"].AsInt();

                    if (!RunRecordFormat.IsKnown(format))
                    {
                        throw new FormatException(
                            "run.json says recordFormat " + format + " and this build reads 1 and 2.");
                    }

                    how = "run.json recordFormat " + format.ToString(Invariant);
                    return format;
                }
            }

            if (File.Exists(Path.Combine(runDirectory, ConvertedName)))
            {
                how = ConvertedName + " (converted and checked by scripts/record-convert.py)";
                return RunRecordFormat.Compact;
            }

            if (haveManifest)
            {
                how = "run.json names no recordFormat, so the record before format 2";
                return RunRecordFormat.Jsonl;
            }

            if (File.Exists(Path.Combine(runDirectory, GenomesName)) ||
                File.Exists(Path.Combine(runDirectory, PositionsMembersName)))
            {
                how = "no run.json; " + GenomesName + " or " + PositionsMembersName + " is present";
                return RunRecordFormat.Compact;
            }

            how = "no run.json and no format 2 file";
            return RunRecordFormat.Jsonl;
        }

        // ------------------------------------------------------------------ snapshots

        /// <summary>A snapshot's file name at a second.</summary>
        public static string SnapshotName(double second, int format) =>
            string.Format(Invariant, "{0:000000000}", (long)second) +
            (format == RunRecordFormat.Compact ? ".jsonl.gz" : ".jsonl");

        /// <summary>The snapshot file at a second in the given record, or null when there is none.</summary>
        public static string SnapshotFile(string runDirectory, double second, int format)
        {
            string file = Path.Combine(
                Path.Combine(runDirectory, SnapshotsDirectory), SnapshotName(second, format));

            return File.Exists(file) ? file : null;
        }

        /// <summary>Every second the record holds a snapshot at, ascending.</summary>
        /// <remarks>
        /// By the name's leading digits and the record's own extension, so a converted run, which
        /// holds both kinds side by side, lists each second once under each format.
        /// </remarks>
        public static double[] SnapshotSeconds(string runDirectory, int format)
        {
            string directory = Path.Combine(runDirectory, SnapshotsDirectory);
            if (!Directory.Exists(directory)) return Array.Empty<double>();

            string suffix = format == RunRecordFormat.Compact ? ".jsonl.gz" : ".jsonl";
            var seconds = new List<double>();

            foreach (string path in Directory.GetFiles(directory))
            {
                string name = Path.GetFileName(path);
                if (!name.EndsWith(suffix, StringComparison.Ordinal)) continue;

                string digits = name.Substring(0, name.Length - suffix.Length);

                if (long.TryParse(digits, NumberStyles.None, Invariant, out long t))
                {
                    seconds.Add(t);
                }
            }

            seconds.Sort();
            return seconds.ToArray();
        }

        /// <summary>
        /// One snapshot, rows joined to their genomes, or null when the record holds no snapshot
        /// at that second.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Format 1 is the file's own rows. Format 2 reads the slim rows, then walks
        /// <c>genomes.jsonl.gz</c> once for the ids they name, and joins. A slim row whose id the
        /// genome file does not hold is refused and counted in
        /// <see cref="SnapshotRead.WithoutAGenome"/>, never drawn as some other genome. A torn
        /// last member in either file is skipped and said in <see cref="SnapshotRead.Notes"/>.
        /// </para>
        /// <para>
        /// The genome file is walked rather than loaded: a long run's is hundreds of megabytes
        /// inflated, and a snapshot wants a few thousand rows of it. The walk stops when every id
        /// is found.
        /// </para>
        /// </remarks>
        public static SnapshotRead ReadSnapshot(string runDirectory, double second, int format)
        {
            if (!RunRecordFormat.IsKnown(format))
            {
                throw new ArgumentOutOfRangeException(nameof(format), format, "Records are format 1 and 2.");
            }

            string file = SnapshotFile(runDirectory, second, format);
            if (file == null) return null;

            var read = new SnapshotRead { Format = format, File = file };

            if (format == RunRecordFormat.Jsonl)
            {
                // ReadRows, never File.ReadAllLines: a snapshot of a live run has a writer on it.
                string[] rows = JsonlWriter.ReadRows(file);

                read.Rows = rows;
                read.Ids = new long[rows.Length];
                read.BodyFractions = new float[rows.Length];

                for (int i = 0; i < rows.Length; i++)
                {
                    read.Ids[i] = IdOf(rows[i]);
                    read.BodyFractions[i] = float.NaN;
                }

                read.Source = SnapshotsDirectory + "/" + Path.GetFileName(file) + " (record format 1)";
                return read;
            }

            string[] slim = GzipMemberReader.ReadLines(file, out string tornSnapshot);
            if (tornSnapshot != null) read.Notes.Add(tornSnapshot);

            var ids = new long[slim.Length];
            var fractions = new float[slim.Length];
            var wanted = new HashSet<long>();

            for (int i = 0; i < slim.Length; i++)
            {
                ids[i] = IdOf(slim[i]);
                fractions[i] = BodyFractionOf(slim[i]);
                if (ids[i] != GenomeJson.NoId) wanted.Add(ids[i]);
            }

            Dictionary<long, string> genomes = GenomeRows(runDirectory, wanted, read.Notes);

            var rowsOut = new List<string>(slim.Length);
            var idsOut = new List<long>(slim.Length);
            var fractionsOut = new List<float>(slim.Length);

            for (int i = 0; i < slim.Length; i++)
            {
                if (ids[i] == GenomeJson.NoId || !genomes.TryGetValue(ids[i], out string genome))
                {
                    read.WithoutAGenome++;
                    read.RefusedIds.Add(ids[i]);
                    continue;
                }

                rowsOut.Add(Join(slim[i], genome));
                idsOut.Add(ids[i]);
                fractionsOut.Add(fractions[i]);
            }

            read.Rows = rowsOut.ToArray();
            read.Ids = idsOut.ToArray();
            read.BodyFractions = fractionsOut.ToArray();
            read.Source =
                SnapshotsDirectory + "/" + Path.GetFileName(file) + " joined to " + GenomesName +
                " (record format 2)";

            return read;
        }

        /// <summary>
        /// The genome row of every id asked for that <c>genomes.jsonl.gz</c> holds, by id.
        /// </summary>
        /// <param name="runDirectory">The run directory holding <c>genomes.jsonl.gz</c>.</param>
        /// <param name="ids">The ids wanted; a set, for the walk asks it of every row.</param>
        /// <param name="notes">Where a torn last member is said, when the walk reaches it. May be null.</param>
        /// <remarks>
        /// The first row for an id wins. Each body is written once, so a second row for an id is
        /// a resumed run's roster written again beside its own births, and it is the same genome.
        /// </remarks>
        public static Dictionary<long, string> GenomeRows(
            string runDirectory, ICollection<long> ids, List<string> notes)
        {
            var found = new Dictionary<long, string>();
            string path = Path.Combine(runDirectory, GenomesName);

            if (ids == null || ids.Count == 0 || !File.Exists(path)) return found;

            using (GzipMemberReader reader = GzipMemberReader.Open(path))
            {
                foreach (string line in reader.Lines())
                {
                    long id = IdOf(line);
                    if (id == GenomeJson.NoId || !ids.Contains(id) || found.ContainsKey(id)) continue;

                    found[id] = line;
                    if (found.Count == ids.Count) break;
                }

                if (reader.Torn) notes?.Add(reader.TornNote);
            }

            return found;
        }

        // ------------------------------------------------------------------ positions

        /// <summary>The positions file of the given record, or null when the run wrote none.</summary>
        public static string PositionsFile(string runDirectory, int format)
        {
            string path = Path.Combine(
                runDirectory, format == RunRecordFormat.Compact ? PositionsMembersName : PositionsName);

            return File.Exists(path) ? path : null;
        }

        /// <summary>
        /// Every complete positions row of the given record, in order, one at a time.
        /// </summary>
        /// <param name="runDirectory">The run directory.</param>
        /// <param name="format">The record to read, <see cref="RunRecordFormat"/>.</param>
        /// <param name="torn">Told the torn last member's note, when there is one. May be null.</param>
        /// <remarks>
        /// Format 1 skips a last line that does not close its object, the rule the theatre's
        /// reader has always kept for a row still being written. Format 2 skips a torn member.
        /// Neither loads the file.
        /// </remarks>
        public static IEnumerable<string> PositionLines(
            string runDirectory, int format, Action<string> torn = null)
        {
            string path = PositionsFile(runDirectory, format);
            if (path == null) yield break;

            if (format == RunRecordFormat.Compact)
            {
                using (GzipMemberReader reader = GzipMemberReader.Open(path))
                {
                    foreach (string line in reader.Lines()) yield return line;
                    if (reader.Torn) torn?.Invoke(reader.TornNote);
                }

                yield break;
            }

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || !line.EndsWith("}", StringComparison.Ordinal)) continue;

                    yield return line;
                }
            }
        }

        // ------------------------------------------------------------------ rows

        /// <summary>
        /// A format 2 snapshot row: the id, the plan fields when the body has them, and the body
        /// fraction last.
        /// </summary>
        /// <remarks>
        /// The plan fields are written with the same calls <see cref="GenomeJson.Write"/> makes
        /// and under its rule for omitting them, which is what keeps <see cref="Join"/> exact. A
        /// fraction that is not finite is left out rather than refused: a snapshot is a
        /// recording, and a reader already takes a row without one as "not recorded".
        /// </remarks>
        public static string SlimRow(
            long id, int[] moduleCounts, IReadOnlyList<int[]> lostPartPaths, float bodyFraction)
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject();
            w.Field("id", id);

            if (moduleCounts != null && moduleCounts.Length > 0)
            {
                w.BeginArray("moduleCounts");
                foreach (int count in moduleCounts) w.Value(count);
                w.EndArray();
            }

            if (lostPartPaths != null && lostPartPaths.Count > 0)
            {
                w.BeginArray("lostPaths");
                foreach (int[] path in lostPartPaths)
                {
                    w.BeginArray();
                    foreach (int step in path) w.Value(step);
                    w.EndArray();
                }
                w.EndArray();
            }

            if (!float.IsNaN(bodyFraction) && !float.IsInfinity(bodyFraction))
            {
                w.Field("bf", bodyFraction);
            }

            w.EndObject();
            return w.ToString();
        }

        /// <summary>
        /// A slim row and its body's genome row, joined into the row format 1 wrote.
        /// </summary>
        /// <exception cref="FormatException">
        /// When the slim row does not open with its id, carries anything after its body fraction,
        /// or the genome row is not that id's.
        /// </exception>
        public static string Join(string slimRow, string genomeRow)
        {
            if (slimRow == null) throw new ArgumentNullException(nameof(slimRow));
            if (genomeRow == null) throw new ArgumentNullException(nameof(genomeRow));

            long id = IdOf(slimRow);

            if (id == GenomeJson.NoId || !slimRow.EndsWith("}", StringComparison.Ordinal))
            {
                throw new FormatException(
                    "A slim snapshot row opens with its id and closes its object; this one does " +
                    "not: " + Clip(slimRow));
            }

            string prefix = "{\"id\":" + id.ToString(Invariant) + ",";

            if (!genomeRow.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new FormatException(
                    "The genome row joined to body " + id + " does not open with that id: " +
                    Clip(genomeRow));
            }

            int fraction = slimRow.IndexOf(",\"bf\":", StringComparison.Ordinal);
            string head;

            if (fraction >= 0)
            {
                if (slimRow.IndexOf(',', fraction + 6) >= 0)
                {
                    throw new FormatException(
                        "Body " + id + "'s slim row carries a field after its body fraction, which " +
                        "the join would drop: " + Clip(slimRow));
                }

                head = slimRow.Substring(0, fraction);
            }
            else
            {
                head = slimRow.Substring(0, slimRow.Length - 1);
            }

            return head + "," + genomeRow.Substring(prefix.Length);
        }

        /// <summary>
        /// The organism id a row opens with, or <see cref="GenomeJson.NoId"/> when it does not
        /// open with one.
        /// </summary>
        /// <remarks>
        /// Read off the text rather than parsed: every row this record writes opens
        /// <c>{"id":</c>, and a walk of a genome file asks this of every row in it.
        /// </remarks>
        public static long IdOf(string row)
        {
            const string opening = "{\"id\":";

            if (row == null || !row.StartsWith(opening, StringComparison.Ordinal)) return GenomeJson.NoId;

            int at = opening.Length;
            int end = at;
            if (end < row.Length && row[end] == '-') end++;
            while (end < row.Length && row[end] >= '0' && row[end] <= '9') end++;

            if (end == at || end >= row.Length || (row[end] != ',' && row[end] != '}')) return GenomeJson.NoId;

            return long.TryParse(row.Substring(at, end - at), NumberStyles.AllowLeadingSign, Invariant, out long id)
                ? id
                : GenomeJson.NoId;
        }

        /// <summary>A slim row's body fraction, or NaN when it carries none.</summary>
        public static float BodyFractionOf(string slimRow)
        {
            int at = slimRow.IndexOf(",\"bf\":", StringComparison.Ordinal);
            if (at < 0) return float.NaN;

            int start = at + 6;
            int end = slimRow.IndexOf('}', start);
            if (end < 0) return float.NaN;

            return float.TryParse(
                slimRow.Substring(start, end - start), NumberStyles.Float, Invariant, out float bf)
                ? bf
                : float.NaN;
        }

        private static string Clip(string text) =>
            text.Length <= 80 ? text : text.Substring(0, 80) + "…";

        private static string ReadShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
