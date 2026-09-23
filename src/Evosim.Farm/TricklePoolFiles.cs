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
    /// pin the same pool. Each file is one line and a newline, <c>pool/NN.json</c> with a
    /// two-digit index in the order the launcher named them.
    /// </para>
    /// <para>
    /// <b>The hash is over the files, in order.</b> SHA-256 over the concatenation of every
    /// file's bytes (each line's UTF-8 and its newline), lowercase hex. A resume recomputes it
    /// from the source run's <c>pool/</c> and refuses a difference, because a pool edited after
    /// the run started is a different world under the same <c>configHash</c>.
    /// </para>
    /// </remarks>
    public static class TricklePoolFiles
    {
        /// <summary>The run directory's pool folder.</summary>
        public const string DirectoryName = "pool";

        /// <summary><c>pool/NN.json</c>.</summary>
        public static string FileName(int index) =>
            index.ToString("00", CultureInfo.InvariantCulture) + ".json";

        /// <summary>A launched pool: the genomes and the lines they are written as.</summary>
        public sealed class Pool
        {
            public IReadOnlyList<Genome> Genomes;
            public IReadOnlyList<string> Lines;
            public string Hash;
        }

        /// <summary>
        /// Reads the launcher's <c>EVOSIM_TRICKLE_POOL</c> list (semicolon-separated genome
        /// files), refusing an empty list entry, a missing file, a file the build cannot read and
        /// a pool larger than <see cref="RunConfig.MaxTricklePool"/>.
        /// </summary>
        public static Pool Prepare(string pathList)
        {
            if (string.IsNullOrWhiteSpace(pathList)) throw new ArgumentException("No pool named.", nameof(pathList));

            string[] paths = pathList.Split(';');
            if (paths.Length > RunConfig.MaxTricklePool)
            {
                throw new InvalidOperationException(
                    "EVOSIM_TRICKLE_POOL names " + paths.Length + " genome files and a pool holds at " +
                    "most " + RunConfig.MaxTricklePool + " (D117; its files are named by a two-digit index).");
            }

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

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        "EVOSIM_TRICKLE_POOL names " + path + ", which does not exist (D117).", path);
                }

                Genome genome;
                try
                {
                    genome = GenomeJson.Read(File.ReadAllText(path, Encoding.UTF8));
                }
                catch (Exception e) when (e is FormatException || e is InvalidOperationException ||
                                          e is KeyNotFoundException || e is ArgumentException)
                {
                    throw new InvalidOperationException(
                        "EVOSIM_TRICKLE_POOL's " + path + " is not a genome this build reads: " +
                        e.Message, e);
                }

                genomes.Add(genome);
                lines.Add(GenomeJson.Write(genome));
            }

            return new Pool { Genomes = genomes, Lines = lines, Hash = HashOf(lines) };
        }

        /// <summary>The bytes of one pool file: the line and its newline, UTF-8, no BOM.</summary>
        public static byte[] BytesOf(string line) => new UTF8Encoding(false).GetBytes(line + "\n");

        /// <summary>SHA-256 over every file's bytes in order, lowercase hex.</summary>
        public static string HashOf(IReadOnlyList<string> lines)
        {
            using (var buffer = new MemoryStream())
            {
                foreach (string line in lines)
                {
                    byte[] bytes = BytesOf(line);
                    buffer.Write(bytes, 0, bytes.Length);
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

        /// <summary>Writes <c>pool/NN.json</c> into <paramref name="runDirectory"/>.</summary>
        public static void Write(string runDirectory, IReadOnlyList<string> lines)
        {
            string folder = Path.Combine(runDirectory, DirectoryName);
            Directory.CreateDirectory(folder);

            for (int i = 0; i < lines.Count; i++)
            {
                File.WriteAllBytes(Path.Combine(folder, FileName(i)), BytesOf(lines[i]));
            }
        }

        /// <summary>
        /// The pool a run directory carries, checked against its config: null when the config
        /// names none, and a refusal when the files are missing, fewer or more than the count, or
        /// hash to anything but <see cref="RunConfig.FoundingTricklePoolHash"/>.
        /// </summary>
        /// <remarks>
        /// The hash is taken over the bytes on disk, not over a re-serialisation, so a file
        /// edited by a single byte is refused even when it still parses to the same genome.
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

                string hash = HashOfBytes(all.ToArray());
                if (!string.Equals(hash, config.FoundingTricklePoolHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The trickle pool in " + folder + " hashes to " + hash + " and the config " +
                        "pins " + config.FoundingTricklePoolHash + ". The pool was changed after the " +
                        "run was launched, so carrying on from it would be a different world under " +
                        "the same configHash (D117). Restore the pool's files, or launch a new run.");
                }

                return new Pool { Genomes = genomes, Lines = lines, Hash = hash };
            }
        }
    }
}
