using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Evosim.Core
{
    /// <summary>
    /// The on-disk layout of one run — DESIGN.md §9.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <code>
    /// runs/2026-08-05-143022-fcec2777/
    ///   config.json      settings, seed, hash. Written once, meant to be read and edited
    ///   lineage.jsonl    one row per creature ever born
    ///   stats.jsonl      one row per sample interval
    ///   snapshots/       world state, one file per save point
    /// </code>
    /// </para>
    /// <para>
    /// <b>The two high-volume files are line-oriented and append-only, and that is the design.</b>
    /// A run killed with ctrl-C, or one that crashes, leaves every completed row valid and
    /// readable — a single rewritten document would leave a truncated file that parses as
    /// nothing. It also means a run can be watched while it happens by tailing a file, and
    /// forked by copying a folder.
    /// </para>
    /// <para>
    /// <b>Creatures are rows, not files.</b> At an estimated 40,000 births an hour, one file per
    /// creature is 40,000 files an hour and, at the ~5 KB a genome measures, about 200 MB an
    /// hour. Rows fix the file count; the diff-and-keyframe scheme (§9) fixes the volume, and
    /// needs the mutation operators of §4.5 to exist before it can be written.
    /// </para>
    /// </remarks>
    public sealed class RunDirectory : IDisposable
    {
        public string Path { get; }
        public RunConfig Config { get; }

        /// <summary>One row per creature ever born.</summary>
        public JsonlWriter Lineage { get; }

        /// <summary>One row per sample interval.</summary>
        public JsonlWriter Stats { get; }

        public string SnapshotsPath => System.IO.Path.Combine(Path, "snapshots");

        private RunDirectory(string path, RunConfig config)
        {
            Path = path;
            Config = config;

            Directory.CreateDirectory(path);
            Directory.CreateDirectory(SnapshotsPath);

            File.WriteAllText(
                System.IO.Path.Combine(path, "config.json"), RunConfigJson.Write(config), Utf8);

            // Lineage is the hot path — a birth every few milliseconds — so it buffers.
            // Stats is one row every few simulated seconds and flushes each time, because it is
            // what you watch a long run through and a buffered tail shows nothing.
            Lineage = new JsonlWriter(System.IO.Path.Combine(path, "lineage.jsonl"), flushEachRow: false);
            Stats = new JsonlWriter(System.IO.Path.Combine(path, "stats.jsonl"), flushEachRow: true);
        }

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>
        /// Creates a new run directory named for the time it started and its settings hash.
        /// </summary>
        /// <param name="root">Usually <c>runs/</c>. Created if absent.</param>
        /// <param name="config">Written to <c>config.json</c>; its hash names the directory.</param>
        /// <param name="startedUtc">
        /// Passed in rather than read from the clock, so that a caller wanting a reproducible
        /// directory name can supply one — and so this stays testable.
        /// </param>
        /// <remarks>
        /// The hash is in the name so that two runs differing only in settings are distinguishable
        /// in a directory listing, which is the case you most want to spot by eye and the one
        /// where a timestamp alone tells you nothing.
        /// </remarks>
        public static RunDirectory Create(string root, RunConfig config, DateTime startedUtc)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (config == null) throw new ArgumentNullException(nameof(config));

            string name = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd-HHmmss}-{1}", startedUtc, config.Hash().Substring(0, 8));

            return new RunDirectory(System.IO.Path.Combine(root, name), config);
        }

        /// <summary>Reopens an existing run directory for reading.</summary>
        /// <param name="path">The run directory itself, not the <c>runs/</c> root.</param>
        /// <param name="hashMismatch">
        /// Set when the settings file was edited after it was written — see
        /// <see cref="RunConfigJson.Read(string, out string)"/>.
        /// </param>
        public static RunConfig ReadConfig(string path, out string hashMismatch)
        {
            string file = System.IO.Path.Combine(path, "config.json");
            if (!File.Exists(file))
            {
                throw new FileNotFoundException($"No config.json in '{path}'.", file);
            }

            return RunConfigJson.Read(File.ReadAllText(file, Utf8), out hashMismatch);
        }

        /// <summary>Path a snapshot at the given simulated time should be written to.</summary>
        /// <remarks>
        /// Zero-padded so a directory listing sorts chronologically. Snapshots are large and
        /// infrequent, so they stay one file each — which is also what makes forking a run a
        /// matter of copying three files and one snapshot.
        /// </remarks>
        public string SnapshotPath(double simulatedSeconds) =>
            System.IO.Path.Combine(
                SnapshotsPath,
                string.Format(CultureInfo.InvariantCulture, "{0:000000000}.json", (long)simulatedSeconds));

        public void Dispose()
        {
            Lineage?.Dispose();
            Stats?.Dispose();
        }
    }

    /// <summary>
    /// Appends one JSON object per line to a file — DESIGN.md §9.
    /// </summary>
    /// <remarks>
    /// Rejects rows containing a line break rather than writing them, because one embedded
    /// newline splits a record across two lines and every row after it in the file becomes
    /// unreadable. That is a whole-file loss caused by one bad record, so it is worth refusing
    /// at the point the record arrives.
    /// </remarks>
    public sealed class JsonlWriter : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly bool _flushEachRow;

        public string Path { get; }
        public long RowCount { get; private set; }

        /// <param name="path">File to append to. Parent directories are created.</param>
        /// <param name="flushEachRow">
        /// <c>true</c> costs a system call per row and guarantees a killed process loses nothing.
        /// <c>false</c> buffers, which is what a birth-rate write path needs; on a crash the last
        /// few rows are lost, and every row before them is still intact.
        /// </param>
        public JsonlWriter(string path, bool flushEachRow)
        {
            Path = path;
            _flushEachRow = flushEachRow;

            string directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            _writer = new StreamWriter(
                new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read),
                new UTF8Encoding(false));
        }

        public void Write(string jsonRow)
        {
            if (jsonRow == null) throw new ArgumentNullException(nameof(jsonRow));

            if (jsonRow.IndexOf('\n') >= 0 || jsonRow.IndexOf('\r') >= 0)
            {
                throw new ArgumentException(
                    "A row contains a line break, which would split one record across two lines " +
                    "and make every row after it unreadable. Serialize with indent: false.",
                    nameof(jsonRow));
            }

            _writer.Write(jsonRow);
            _writer.Write('\n');
            RowCount++;

            if (_flushEachRow) _writer.Flush();
        }

        /// <summary>Writes a genome as one row of <c>lineage.jsonl</c>.</summary>
        public void WriteGenome(Genome genome) => Write(GenomeJson.Write(genome));

        /// <summary>
        /// Builds one row inline. The callback receives a writer with an object already open.
        /// </summary>
        /// <remarks>
        /// Deliberately not a fixed schema for statistics. What a sample row should contain —
        /// population, births, deaths, mean energy, a census by cell type — is a property of an
        /// energy economy that does not exist yet (§5A), and inventing the columns now would
        /// mean inventing them wrong. The file format is settled; the columns are the caller's.
        /// </remarks>
        public void WriteRow(Action<Json.Writer> build)
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject();
            build(w);
            w.EndObject();
            Write(w.ToString());
        }

        public void Flush() => _writer.Flush();

        /// <summary>
        /// Reads every complete row of a <c>.jsonl</c> file, <b>including one still being
        /// written</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Opens with <see cref="FileShare.ReadWrite"/>, which is the whole point of this method
        /// existing rather than callers reaching for <c>File.ReadAllLines</c>. That opens with
        /// <see cref="FileShare.Read"/>, which refuses to coexist with a writer and fails with a
        /// sharing violation — so watching a run in progress, one of the reasons the format is
        /// line-oriented at all, would not have worked.
        /// </para>
        /// <para>
        /// A final partial line is dropped rather than returned: mid-write, the last line may be
        /// half a record, and half a record parsed as a creature is worse than no creature.
        /// </para>
        /// </remarks>
        public static string[] ReadRows(string path)
        {
            var rows = new System.Collections.Generic.List<string>();

            using (var stream = new FileStream(
                       path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
            {
                string all = reader.ReadToEnd();
                int start = 0;

                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != '\n') continue;

                    int end = i > start && all[i - 1] == '\r' ? i - 1 : i;
                    if (end > start) rows.Add(all.Substring(start, end - start));
                    start = i + 1;
                }
            }

            return rows.ToArray();
        }

        public void Dispose()
        {
            _writer.Flush();
            _writer.Dispose();
        }
    }
}
