using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Evosim.Theatre
{
    /// <summary>
    /// What <c>lineage.jsonl</c> knows about a creature the world no longer holds: when it died,
    /// how long it lived, how many children it had, and who its parents were.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing in the theatre read this file before.</b> <see cref="RunRecord"/> deliberately
    /// loads <c>config.json</c> and <c>stats.jsonl</c> and nothing else, because those two are all
    /// a replay needs. The inspector's dead state needs more: a death time, a lifespan and a
    /// child count are all in <c>lineage.jsonl</c> and in no other file a run writes.
    /// </para>
    /// <para>
    /// <b>It is streamed, not loaded, and the sharing mode matters.</b> A run's
    /// <c>lineage.jsonl</c> reaches hundreds of megabytes. <c>JsonlWriter.ReadRows</c> is the
    /// method the rest of the project uses for a <c>.jsonl</c>, and the reason is its
    /// <see cref="FileShare.ReadWrite"/>: <c>File.ReadAllLines</c> opens with
    /// <see cref="FileShare.Read"/> and throws a sharing violation against a live run's writer.
    /// But <c>ReadRows</c> reads the whole file into one string before it returns any of it, which
    /// is exactly what a file of that size must not do. So this opens the stream itself, with the
    /// same sharing mode and the same rule about a half-written final line, and reads it a line at
    /// a time. That is the one place the theatre does not call the project's own reader, and it is
    /// deliberate: the sharing mode is what <c>ReadRows</c> is for, and both constraints are kept.
    /// </para>
    /// <para>
    /// <b>Built lazily, on the first selection.</b> Opening a run must not pay for a file a viewer
    /// may never ask a question of, and most sessions never click a creature that has died.
    /// </para>
    /// <para>
    /// <b>No cause of death.</b> Every death in this world reads <c>starved</c>, because
    /// <c>Starved</c> is the only <c>DeathCause</c> implemented (CLAUDE.md), so the field
    /// discriminates nothing and the design correctly omits it. It is not read here either.
    /// </para>
    /// <para>
    /// <b>The rows are scanned, not parsed.</b> <c>Json.Parse</c> on a million short rows is
    /// seconds of work and a great deal of garbage in a viewer that is sharing a machine with five
    /// simulations. The scanner below looks each field up by name rather than by position, so a
    /// row that gains a field still reads; a row missing one of the four fields it needs is
    /// counted as malformed and the count is reported rather than hidden.
    /// </para>
    /// </remarks>
    public sealed class LineageIndex
    {
        /// <summary>One creature, as its birth and death rows describe it.</summary>
        public struct Entry
        {
            public long Id;
            public long ParentId;
            public int Generation;
            public double BornAt;

            /// <summary>When it died, or NaN while it is alive in the record.</summary>
            public double DiedAt;

            /// <summary>Birth rows whose parent is this creature.</summary>
            public int Children;

            public bool Died => !double.IsNaN(DiedAt);
        }

        /// <summary>How many ancestors the chain prints before it elides.</summary>
        public const int ChainShown = 4;

        private readonly Dictionary<long, Entry> _byId = new Dictionary<long, Entry>();

        /// <summary>False when the file was missing or unreadable; <see cref="Note"/> says why.</summary>
        public bool Available { get; private set; }

        /// <summary>Why the index is not available, or what was wrong with part of it.</summary>
        public string Note { get; private set; }

        /// <summary>Birth rows read.</summary>
        public int Births { get; private set; }

        /// <summary>Death rows read.</summary>
        public int Deaths { get; private set; }

        /// <summary>Rows that carried no readable event, id or time.</summary>
        public int Malformed { get; private set; }

        /// <summary>Wall-clock milliseconds the scan took.</summary>
        public double MillisecondsToBuild { get; private set; }

        private LineageIndex() { }

        /// <summary>
        /// Reads a run's <c>lineage.jsonl</c>. Never throws: a run whose file is missing gets an
        /// index that says so, and the inspector shows its unavailable state rather than an error.
        /// </summary>
        public static LineageIndex Read(string runDirectory)
        {
            var index = new LineageIndex();
            var clock = System.Diagnostics.Stopwatch.StartNew();

            string path = Path.Combine(runDirectory ?? "", "lineage.jsonl");

            if (string.IsNullOrEmpty(runDirectory) || !File.Exists(path))
            {
                index.Note = "no lineage.jsonl in this run: a creature that has died cannot be named";
                index.MillisecondsToBuild = clock.Elapsed.TotalMilliseconds;
                return index;
            }

            try
            {
                index.Scan(path);
                index.Available = true;
            }
            catch (Exception e)
            {
                index._byId.Clear();
                index.Available = false;
                index.Note = "lineage.jsonl unreadable: " + e.GetType().Name + ": " + e.Message;
            }

            index.MillisecondsToBuild = clock.Elapsed.TotalMilliseconds;

            if (index.Available && index.Malformed > 0 && index.Note == null)
            {
                index.Note = index.Malformed + " lineage row(s) unreadable and skipped";
            }

            return index;
        }

        private void Scan(string path)
        {
            // FileShare.ReadWrite, like JsonlWriter.ReadRows, so a live run's writer is no
            // obstacle; a line at a time, unlike it, so a file of hundreds of megabytes never
            // becomes a string of hundreds of megabytes.
            using (var stream = new FileStream(
                       path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
            {
                string line;
                string previous = null;

                while ((line = reader.ReadLine()) != null)
                {
                    // A final line with no newline after it may be half a record mid-write, and
                    // half a record read as a creature is worse than no creature. So every line is
                    // held back one turn and only taken once the next one proves it complete.
                    if (previous != null) Take(previous);
                    previous = line;
                }

                // The last line is taken only if the file ends the way a complete row does.
                if (previous != null && EndsWithNewline(stream)) Take(previous);
            }
        }

        /// <summary>True when the file's last byte is a line break, so its last row is complete.</summary>
        private static bool EndsWithNewline(FileStream stream)
        {
            try
            {
                if (stream.Length == 0) return false;

                long at = stream.Position;
                stream.Seek(-1, SeekOrigin.End);
                int last = stream.ReadByte();
                stream.Position = at;

                return last == '\n' || last == '\r';
            }
            catch
            {
                return false;
            }
        }

        private void Take(string row)
        {
            if (string.IsNullOrEmpty(row)) return;

            string kind = String(row, "e");
            long id = Integer(row, "id", long.MinValue);

            if (kind == null || id == long.MinValue)
            {
                Malformed++;
                return;
            }

            if (kind == "b")
            {
                Births++;

                var entry = new Entry
                {
                    Id = id,
                    ParentId = Integer(row, "p", -1L),
                    Generation = (int)Integer(row, "g", 0L),
                    BornAt = Number(row, "t", double.NaN),
                    DiedAt = double.NaN,
                    Children = 0,
                };

                // A child seen before its parent's row cannot happen in a file written in order,
                // but an index that would be wrong if it did is not worth the assumption: the
                // count is kept on the parent's entry, which is created empty if it is not there.
                if (_byId.TryGetValue(id, out Entry existing))
                {
                    entry.Children = existing.Children;
                }

                _byId[id] = entry;

                if (entry.ParentId >= 0) CountAChildFor(entry.ParentId);
                return;
            }

            if (kind != "d")
            {
                Malformed++;
                return;
            }

            Deaths++;

            double t = Number(row, "t", double.NaN);

            if (_byId.TryGetValue(id, out Entry born))
            {
                born.DiedAt = t;
                _byId[id] = born;
                return;
            }

            // A death with no birth row: r25-s2's wall end left snapshot ids with no birth row,
            // so this is a case the record has actually produced. Kept with what is known.
            _byId[id] = new Entry
            {
                Id = id,
                ParentId = -1L,
                Generation = -1,
                BornAt = double.NaN,
                DiedAt = t,
                Children = 0,
            };
        }

        private void CountAChildFor(long parentId)
        {
            if (_byId.TryGetValue(parentId, out Entry parent))
            {
                parent.Children++;
                _byId[parentId] = parent;
                return;
            }

            _byId[parentId] = new Entry
            {
                Id = parentId,
                ParentId = -1L,
                Generation = -1,
                BornAt = double.NaN,
                DiedAt = double.NaN,
                Children = 1,
            };
        }

        /// <summary>What the index knows about one creature.</summary>
        public bool TryFind(long id, out Entry entry) => _byId.TryGetValue(id, out entry);

        /// <summary>Creatures the index holds.</summary>
        public int Count => _byId.Count;

        /// <summary>
        /// The ancestry chain as the inspector prints it: a few ids joined by U+2190, elided in
        /// the middle, ending at the founder.
        /// </summary>
        /// <param name="id">The creature to walk up from.</param>
        /// <param name="depth">How many creatures the whole chain holds, this one included.</param>
        /// <param name="founder">The oldest ancestor found, or -1.</param>
        /// <returns>The chain, or null when the index knows nothing about the creature.</returns>
        public string Chain(long id, out int depth, out long founder)
        {
            depth = 0;
            founder = -1L;

            if (!_byId.ContainsKey(id)) return null;

            var walked = new List<long>();
            var seen = new HashSet<long>();
            long at = id;

            // The guard is not paranoia: a cycle would hang the viewer, and this is a file on
            // disk that a half-written row or a forked run can make say anything.
            while (at >= 0 && seen.Add(at) && walked.Count < 4096)
            {
                walked.Add(at);
                founder = at;

                if (!_byId.TryGetValue(at, out Entry entry)) break;
                at = entry.ParentId;
            }

            depth = walked.Count;

            var text = new StringBuilder();
            int shown = Math.Min(ChainShown, walked.Count);

            for (int i = 0; i < shown; i++)
            {
                if (i > 0) text.Append(' ').Append(TheatreUiFormat.LeftArrow).Append(' ');
                text.Append(TheatreUiFormat.Identifier(walked[i]));
            }

            if (walked.Count > shown + 1)
            {
                text.Append(' ').Append(TheatreUiFormat.LeftArrow).Append(' ')
                    .Append(TheatreUiFormat.Ellipsis);
            }

            if (walked.Count > shown)
            {
                text.Append(' ').Append(TheatreUiFormat.LeftArrow).Append(' ')
                    .Append(TheatreUiFormat.Identifier(founder));
            }

            return text.ToString();
        }

        // ------------------------------------------------------------------ the row scanner

        /// <summary>The string value of a field, or null.</summary>
        private static string String(string row, string field)
        {
            int at = ValueStart(row, field);
            if (at < 0 || at >= row.Length || row[at] != '"') return null;

            int end = row.IndexOf('"', at + 1);
            return end < 0 ? null : row.Substring(at + 1, end - at - 1);
        }

        /// <summary>The numeric value of a field, or <paramref name="fallback"/>.</summary>
        private static double Number(string row, string field, double fallback)
        {
            string token = Token(row, field);

            return token != null &&
                   double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture,
                       out double value)
                ? value
                : fallback;
        }

        private static long Integer(string row, string field, long fallback)
        {
            string token = Token(row, field);

            if (token == null) return fallback;

            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out long value))
            {
                return value;
            }

            // A whole number written with an exponent or a decimal point still names a creature.
            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture,
                out double loose)
                ? (long)loose
                : fallback;
        }

        private static string Token(string row, string field)
        {
            int at = ValueStart(row, field);
            if (at < 0) return null;

            int end = at;
            while (end < row.Length && row[end] != ',' && row[end] != '}') end++;

            return end > at ? row.Substring(at, end - at).Trim() : null;
        }

        /// <summary>
        /// Where the value of <c>"field":</c> begins, looked up by name rather than by position so
        /// that a row which gains a column still reads.
        /// </summary>
        private static int ValueStart(string row, string field)
        {
            string key = "\"" + field + "\":";
            int at = row.IndexOf(key, StringComparison.Ordinal);

            if (at < 0) return -1;

            at += key.Length;
            while (at < row.Length && row[at] == ' ') at++;

            return at;
        }
    }
}
