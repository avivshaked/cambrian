using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// D117's pool on disk: the genome files a launcher names, copied into the run directory's
    /// <c>pool/</c> in this build's own bytes, and the hash over those bytes that the config
    /// carries so that <c>configHash</c> pins which bodies could arrive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bytes are this build's.</b> Each named file is read with <see cref="GenomeJson"/>
    /// (a file the build cannot read is refused with the reader's own text) and written back
    /// through <see cref="GenomeJson.Write"/>, so an <c>id</c> or a module count carried by a
    /// snapshot row is dropped and two launchers naming the same genomes in different spellings
    /// pin the same pool. Each file is one line and a newline, <c>pool/NN.json</c> with the
    /// index, two digits at least, in the order the launcher named them.
    /// </para>
    /// <para>
    /// <b>The hash is over the files, in order.</b> SHA-256 over the concatenation of every
    /// file's bytes (each line's UTF-8 and its newline), lowercase hex. A resume recomputes it
    /// from the source run's <c>pool/</c> and refuses a difference, because a pool edited after
    /// the run started is a different world under the same <c>configHash</c>.
    /// </para>
    /// <para>
    /// <b>A weighted pool adds one file.</b> Named by a list file (<c>EVOSIM_TRICKLE_POOL</c>
    /// given as <c>@path</c>, <see cref="Prepare"/>), the pool also pins <c>pool/index.tsv</c>:
    /// each genome's weight and the origin columns the list carried (the nursery run, the body),
    /// which a founder's lineage row reaches through its <c>pool</c> index. Its bytes follow the
    /// genome files' in the hash, and only when it exists, so an unweighted pool hashes exactly
    /// as it always has.
    /// </para>
    /// </remarks>
    public static class TricklePoolFiles
    {
        /// <summary>The run directory's pool folder.</summary>
        public const string DirectoryName = "pool";

        /// <summary>The weighted pool's weights and origins, beside the genome files.</summary>
        public const string IndexFileName = "index.tsv";

        /// <summary>
        /// <c>pool/NN.json</c>: two digits below 100, as every pool on disk was written, and the
        /// plain number from 100 on (<c>pool/123.json</c>).
        /// </summary>
        public static string FileName(int index) =>
            index.ToString("00", CultureInfo.InvariantCulture) + ".json";

        /// <summary>A launched pool: the genomes and the lines they are written as.</summary>
        public sealed class Pool
        {
            public IReadOnlyList<Genome> Genomes;
            public IReadOnlyList<string> Lines;
            public string Hash;

            /// <summary>One weight a genome, in the pool's order; null for an unweighted pool.</summary>
            public IReadOnlyList<double> Weights;

            /// <summary>The text of <c>pool/index.tsv</c>; null for an unweighted pool.</summary>
            public string IndexText;

            /// <summary>The list's origin column names, after <c>path</c> and <c>weight</c>; empty unweighted.</summary>
            public IReadOnlyList<string> OriginNames = Array.Empty<string>();

            /// <summary>Each genome's origin cells, in <see cref="OriginNames"/>' order; null unweighted.</summary>
            public IReadOnlyList<IReadOnlyList<string>> Origins;
        }

        /// <summary>
        /// Reads the launcher's <c>EVOSIM_TRICKLE_POOL</c>: a semicolon-separated list of genome
        /// files, or <c>@</c> and the path of a list file (<see cref="PrepareList"/>). Refuses an
        /// empty list entry, a missing file, a file the build cannot read and a pool larger than
        /// <see cref="RunConfig.MaxTricklePool"/>.
        /// </summary>
        /// <remarks>
        /// The list file rides on the same variable rather than a second one, so a launcher cannot
        /// name a list and a pool at once, and a resume's warning that the variable is ignored
        /// covers both forms.
        /// </remarks>
        public static Pool Prepare(string pathList)
        {
            if (string.IsNullOrWhiteSpace(pathList)) throw new ArgumentException("No pool named.", nameof(pathList));

            string trimmed = pathList.Trim();
            if (trimmed.StartsWith("@", StringComparison.Ordinal)) return PrepareList(trimmed.Substring(1).Trim());

            string[] paths = pathList.Split(';');
            CheckSize(paths.Length, "EVOSIM_TRICKLE_POOL names " + paths.Length + " genome files");

            var genomes = new List<Genome>();
            var lines = new List<string>();

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i].Trim();
                if (path.Length == 0)
                {
                    throw new InvalidOperationException(
                        "EVOSIM_TRICKLE_POOL's entry " + i + " is empty ('" + pathList + "'). The " +
                        "pool's order is its indices (D117), so a list with a hole in it is refused " +
                        "rather than closed up.");
                }

                Genome genome = ReadGenome(path, "EVOSIM_TRICKLE_POOL");
                genomes.Add(genome);
                lines.Add(GenomeJson.Write(genome));
            }

            return new Pool { Genomes = genomes, Lines = lines, Hash = HashOf(lines) };
        }

        /// <summary>
        /// Reads a weighted list file: tab-separated, one genome a line after a header line whose
        /// first two columns are <c>path</c> and <c>weight</c> and whose others name the origin
        /// columns (round 55's lists carry <c>run</c>, <c>body</c>, <c>born</c> and <c>score</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// A relative path is read from the list file's own folder, so a list and the genomes
        /// beside it can be moved together. A weight is a positive finite number; a genome the
        /// trickle should never draw is left out. Origin cells are free text, kept as written
        /// (trimmed), and may be empty. Blank lines are skipped; every other line has the
        /// header's count of cells, because a short row would put a body's origin under the
        /// wrong name.
        /// </para>
        /// <para>
        /// The pinned <c>pool/index.tsv</c> leaves the paths out: they are this machine's
        /// spellings, and the genome files already pin the bodies, so two launchers naming the
        /// same bodies at the same weights from different folders pin the same pool. Its weights
        /// are written in the shortest text that reads back as the same double.
        /// </para>
        /// </remarks>
        public static Pool PrepareList(string listPath)
        {
            if (string.IsNullOrWhiteSpace(listPath))
            {
                throw new ArgumentException("EVOSIM_TRICKLE_POOL is '@' with no list file after it.", nameof(listPath));
            }

            if (!File.Exists(listPath))
            {
                throw new FileNotFoundException(
                    "EVOSIM_TRICKLE_POOL names the list file " + listPath + ", which does not exist (D117).",
                    listPath);
            }

            string folder = Path.GetDirectoryName(Path.GetFullPath(listPath));
            string[] raw = File.ReadAllText(listPath, Encoding.UTF8).Replace("\r\n", "\n").Split('\n');

            string[] header = null;
            int headerLine = 0;
            var rows = new List<(int line, string[] cells)>();

            for (int l = 0; l < raw.Length; l++)
            {
                if (raw[l].Trim().Length == 0) continue;

                string[] cells = raw[l].Split('\t');
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();

                if (header == null)
                {
                    header = cells;
                    headerLine = l + 1;
                    continue;
                }

                if (cells.Length != header.Length)
                {
                    throw new InvalidOperationException(
                        "The pool list " + listPath + " has " + cells.Length + " cells on line " + (l + 1) +
                        " and its header (line " + headerLine + ") names " + header.Length + ". Every " +
                        "row carries every column, separated by tabs, so an origin cannot land under " +
                        "another column's name.");
                }

                rows.Add((l + 1, cells));
            }

            if (header == null || header.Length < 2 ||
                !string.Equals(header[0], "path", StringComparison.Ordinal) ||
                !string.Equals(header[1], "weight", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The pool list " + listPath + " does not start with a header line whose first two " +
                    "tab-separated columns are 'path' and 'weight' (the origin columns follow them, " +
                    "e.g. run, body, born, score).");
            }

            var originNames = new string[header.Length - 2];
            var seen = new HashSet<string>(StringComparer.Ordinal) { "pool", "path", "weight" };
            for (int c = 2; c < header.Length; c++)
            {
                string name = header[c];
                if (name.Length == 0 || !seen.Add(name))
                {
                    throw new InvalidOperationException(
                        "The pool list " + listPath + "'s header names origin column " + (c + 1) + " '" +
                        name + "', which is empty or repeats a column name ('pool', 'path' and " +
                        "'weight' are taken).");
                }

                originNames[c - 2] = name;
            }

            if (rows.Count == 0)
            {
                throw new InvalidOperationException("The pool list " + listPath + " names no genomes.");
            }

            CheckSize(rows.Count, "The pool list " + listPath + " names " + rows.Count + " genome files");

            var genomes = new List<Genome>();
            var lines = new List<string>();
            var weights = new double[rows.Count];
            var origins = new IReadOnlyList<string>[rows.Count];

            for (int i = 0; i < rows.Count; i++)
            {
                (int line, string[] cells) = rows[i];
                string where = "The pool list " + listPath + " line " + line;

                if (cells[0].Length == 0) throw new InvalidOperationException(where + " names no path.");

                if (!double.TryParse(cells[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double weight) ||
                    !(weight > 0d) || double.IsInfinity(weight))
                {
                    throw new InvalidOperationException(
                        where + " gives the weight '" + cells[1] + "'; a weight is a finite number above " +
                        "0, and a genome the trickle should never draw is left out of the list.");
                }

                string path = Path.IsPathRooted(cells[0]) ? cells[0] : Path.Combine(folder, cells[0]);
                Genome genome = ReadGenome(path, where);

                genomes.Add(genome);
                lines.Add(GenomeJson.Write(genome));
                weights[i] = weight;

                var cellsOut = new string[originNames.Length];
                Array.Copy(cells, 2, cellsOut, 0, originNames.Length);
                origins[i] = cellsOut;
            }

            string index = IndexTextOf(weights, originNames, origins);

            return new Pool
            {
                Genomes = genomes,
                Lines = lines,
                Hash = HashOf(lines, index),
                Weights = weights,
                IndexText = index,
                OriginNames = originNames,
                Origins = origins,
            };
        }

        /// <summary>
        /// <c>pool/index.tsv</c>'s text: a header <c>pool</c>, <c>weight</c> and the origin names,
        /// then one row a genome with its index, its weight and its origin cells, tab-separated,
        /// each line ending in a newline. The <c>pool</c> column is the lineage row's.
        /// </summary>
        public static string IndexTextOf(
            IReadOnlyList<double> weights, IReadOnlyList<string> originNames,
            IReadOnlyList<IReadOnlyList<string>> origins)
        {
            var text = new StringBuilder("pool\tweight");
            foreach (string name in originNames) text.Append('\t').Append(name);
            text.Append('\n');

            for (int i = 0; i < weights.Count; i++)
            {
                text.Append(i.ToString(CultureInfo.InvariantCulture))
                    .Append('\t')
                    .Append(weights[i].ToString("R", CultureInfo.InvariantCulture));

                foreach (string cell in origins[i]) text.Append('\t').Append(cell);
                text.Append('\n');
            }

            return text.ToString();
        }

        private static void CheckSize(int count, string named)
        {
            if (count > RunConfig.MaxTricklePool)
            {
                throw new InvalidOperationException(
                    named + " and a pool holds at most " + RunConfig.MaxTricklePool + " (D117).");
            }
        }

        private static Genome ReadGenome(string path, string named)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(named + " names " + path + ", which does not exist (D117).", path);
            }

            try
            {
                return GenomeJson.Read(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e) when (e is FormatException || e is InvalidOperationException ||
                                      e is KeyNotFoundException || e is ArgumentException)
            {
                throw new InvalidOperationException(
                    named + "'s " + path + " is not a genome this build reads: " + e.Message, e);
            }
        }

        /// <summary>The bytes of one pool file: the line and its newline, UTF-8, no BOM.</summary>
        public static byte[] BytesOf(string line) => new UTF8Encoding(false).GetBytes(line + "\n");

        /// <summary>
        /// SHA-256 over every file's bytes in order and then <c>index.tsv</c>'s when there is one,
        /// lowercase hex.
        /// </summary>
        public static string HashOf(IReadOnlyList<string> lines, string indexText = null)
        {
            using (var buffer = new MemoryStream())
            {
                foreach (string line in lines)
                {
                    byte[] bytes = BytesOf(line);
                    buffer.Write(bytes, 0, bytes.Length);
                }

                if (indexText != null)
                {
                    byte[] index = new UTF8Encoding(false).GetBytes(indexText);
                    buffer.Write(index, 0, index.Length);
                }

                return HashOfBytes(buffer.ToArray());
            }
        }

        private static string HashOfBytes(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Writes <c>pool/NN.json</c> into <paramref name="runDirectory"/>, and
        /// <c>pool/index.tsv</c> when <paramref name="indexText"/> is given (a weighted pool).
        /// </summary>
        public static void Write(string runDirectory, IReadOnlyList<string> lines, string indexText = null)
        {
            string folder = Path.Combine(runDirectory, DirectoryName);
            Directory.CreateDirectory(folder);

            for (int i = 0; i < lines.Count; i++)
            {
                File.WriteAllBytes(Path.Combine(folder, FileName(i)), BytesOf(lines[i]));
            }

            if (indexText != null)
            {
                File.WriteAllBytes(Path.Combine(folder, IndexFileName), new UTF8Encoding(false).GetBytes(indexText));
            }
        }

        /// <summary>
        /// The pool a run directory carries, checked against its config: null when the config
        /// names none, and a refusal when the files are missing, fewer or more than the count, or
        /// hash to anything but <see cref="RunConfig.FoundingTricklePoolHash"/>. A
        /// <c>pool/index.tsv</c> beside them is hashed with them and read for the weights.
        /// </summary>
        /// <remarks>
        /// The hash is taken over the bytes on disk, not over a re-serialisation, so a file
        /// edited by a single byte is refused even when it still parses to the same genome. That
        /// holds for the index too, and its presence is part of the hash: a weighted run's index
        /// removed, or an index added to an unweighted run, hashes to another pool.
        /// </remarks>
        public static Pool Load(string runDirectory, RunConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            int count = config.FoundingTricklePoolCount;
            if (count == 0) return null;

            string folder = Path.Combine(runDirectory, DirectoryName);
            var genomes = new List<Genome>();
            var lines = new List<string>();

            using (var all = new MemoryStream())
            {
                for (int i = 0; i < count; i++)
                {
                    string path = Path.Combine(folder, FileName(i));
                    if (!File.Exists(path))
                    {
                        throw new InvalidOperationException(
                            "The config names a trickle pool of " + count + " genomes and " + path +
                            " is missing. The pool is part of the world (D117), and a run cannot " +
                            "be carried on without the bodies it could draw.");
                    }

                    byte[] bytes = File.ReadAllBytes(path);
                    all.Write(bytes, 0, bytes.Length);

                    string text = new UTF8Encoding(false).GetString(bytes);
                    lines.Add(text.TrimEnd('\n'));
                    genomes.Add(GenomeJson.Read(text));
                }

                if (File.Exists(Path.Combine(folder, FileName(count))))
                {
                    throw new InvalidOperationException(
                        "The config names a trickle pool of " + count + " genomes and " + folder +
                        " holds more. The pool is the one the config pins (D117).");
                }

                string indexPath = Path.Combine(folder, IndexFileName);
                string indexText = null;
                if (File.Exists(indexPath))
                {
                    byte[] index = File.ReadAllBytes(indexPath);
                    all.Write(index, 0, index.Length);
                    indexText = new UTF8Encoding(false).GetString(index);
                }

                string hash = HashOfBytes(all.ToArray());
                if (!string.Equals(hash, config.FoundingTricklePoolHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The trickle pool in " + folder + " hashes to " + hash +
                        (indexText != null ? " (its genome files and " + IndexFileName + ")" : "") +
                        " and the config pins " + config.FoundingTricklePoolHash + ". The pool was " +
                        "changed after the run was launched, so carrying on from it would be a " +
                        "different world under the same configHash (D117). Restore the pool's files, " +
                        "or launch a new run.");
                }

                var pool = new Pool { Genomes = genomes, Lines = lines, Hash = hash };
                if (indexText != null) ReadIndex(indexText, count, indexPath, pool);
                return pool;
            }
        }

        /// <summary>
        /// Reads a pinned <c>pool/index.tsv</c> into <paramref name="pool"/>'s weights and origins,
        /// refusing one that does not describe the pool row for row.
        /// </summary>
        /// <remarks>
        /// The hash has already passed, so a refusal here is a file this build did not write;
        /// it is still refused rather than read past, since the weights decide which body arrives.
        /// </remarks>
        private static void ReadIndex(string text, int count, string path, Pool pool)
        {
            string[] lines = text.Split('\n');

            // Every line ends in a newline, so the last piece is empty and is not a row.
            int rows = lines.Length - 1;
            if (rows < 1 || lines[rows].Length != 0 || rows - 1 != count)
            {
                throw new InvalidOperationException(
                    path + " does not hold a header and one newline-ended row for each of the pool's " +
                    count + " genomes.");
            }

            string[] header = lines[0].Split('\t');
            if (header.Length < 2 || header[0] != "pool" || header[1] != "weight")
            {
                throw new InvalidOperationException(path + "'s header does not start with 'pool' and 'weight'.");
            }

            var originNames = new string[header.Length - 2];
            Array.Copy(header, 2, originNames, 0, originNames.Length);

            var weights = new double[count];
            var origins = new IReadOnlyList<string>[count];

            for (int i = 0; i < count; i++)
            {
                string[] cells = lines[i + 1].Split('\t');
                if (cells.Length != header.Length ||
                    cells[0] != i.ToString(CultureInfo.InvariantCulture) ||
                    !double.TryParse(cells[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double w) ||
                    !(w > 0d) || double.IsInfinity(w))
                {
                    throw new InvalidOperationException(
                        path + "'s row " + (i + 1) + " is not pool " + i + " with a positive finite " +
                        "weight and every origin column.");
                }

                weights[i] = w;
                var cellsOut = new string[originNames.Length];
                Array.Copy(cells, 2, cellsOut, 0, originNames.Length);
                origins[i] = cellsOut;
            }

            pool.Weights = weights;
            pool.IndexText = text;
            pool.OriginNames = originNames;
            pool.Origins = origins;
        }
    }
}
