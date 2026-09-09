using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Genomes written by a real run can be read back — DESIGN.md §9.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>GenomeJsonTests already round-trips genomes this suite constructed.</b> This reads the
    /// ones an actual run wrote, which is a different claim: it is the difference between "the
    /// serializer is consistent with itself" and "the files on disk are usable". A founder pool
    /// imported from a previous run depends on the second.
    /// </para>
    /// <para>
    /// <b>Skips when there is nothing to read.</b> <c>runs/</c> is gitignored, so a clean checkout
    /// has no snapshots and this must not fail there. That makes it a weaker guard than a fixture
    /// would be — but a fixture is a genome this suite chose, and the failure being guarded
    /// against is a genome the <i>simulator</i> chose: an operator, a cell type or a sensor
    /// channel that a run can produce and the reader has never seen.
    /// </para>
    /// <para>
    /// Marked <c>Slow</c> and left out of the default run: on 2026-09-09 <c>runs/</c> held 11 GB
    /// and this one test cost 136 s, all of it spent reading full snapshot files whose first row
    /// already said they were stale. Staleness is now decided from that first row before the rest
    /// of the file is touched — see <see cref="EveryGenomeARunWroteCanBeReadBack"/>. Run it with
    /// <c>core-test.ps1 -All</c>.
    /// </para>
    /// </remarks>
    [Trait("Category", "Slow")]
    public class SnapshotReadbackTests
    {
        private readonly ITestOutputHelper _output;

        public SnapshotReadbackTests(ITestOutputHelper output) => _output = output;

        /// <summary>Walks up from the test binary to the repository root.</summary>
        private static string RepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DESIGN.md")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName;
        }

        [Fact]
        public void EveryGenomeARunWroteCanBeReadBack()
        {
            string root = RepositoryRoot();
            if (root == null) { _output.WriteLine("no repository root found — skipped"); return; }

            string runs = Path.Combine(root, "runs");
            if (!Directory.Exists(runs)) { _output.WriteLine("no runs/ — skipped"); return; }

            // Only the snapshot directories. A run directory holds other .jsonl files — stats.jsonl
            // is one, and lineage.jsonl will be another — and "every .jsonl under runs/" quietly
            // meant "every genome" only for as long as genomes were the sole thing written as
            // rows. Feeding a stats row to the genome reader is a test failure that reports a
            // corrupt genome, which is the most misleading thing this suite could say.
            string[] snapshots = Directory.GetFiles(
                runs, "*.jsonl", SearchOption.AllDirectories);
            snapshots = Array.FindAll(
                snapshots,
                f => Path.GetFileName(Path.GetDirectoryName(f)) == "snapshots");
            if (snapshots.Length == 0) { _output.WriteLine("no snapshots — skipped"); return; }

            int files = 0, genomes = 0, parts = 0, neurons = 0, stale = 0, empty = 0;
            var cellTypes = new HashSet<string>();
            var failures = new List<string>();

            foreach (string file in snapshots)
            {
                files++;

                // Staleness is decided from the first row alone, before the rest of the file is
                // touched. runs/ held 11 GB on 2026-09-09 and this test cost 136 s, almost all of
                // it spent reading full snapshot files whose first row already said they were an
                // old format this build correctly refuses. Opened with FileShare.ReadWrite, not
                // File.ReadAllLines's FileShare.Read, because a live run holds the file open for
                // append and that throws a sharing violation.
                string first;
                try { first = FirstLine(file); }
                catch (Exception e) { failures.Add($"{Path.GetFileName(file)}: unreadable — {e.Message}"); continue; }

                // A file with nothing in it is neither a genome this build can read nor an older
                // schema it correctly refuses — a run writes one whenever a sample lands on an
                // empty world. Counted on its own so the guard at the bottom can say every file
                // was accounted for, rather than being satisfied by whichever category happened
                // to be non-empty. Found when the genome format moved to 5 and the twenty-nine
                // empty files under runs/ were the only ones left over.
                if (string.IsNullOrWhiteSpace(first))
                {
                    empty++;
                    continue;
                }

                // Archaeology is not a regression. This guards that a run cannot write a genome
                // *this build* is unable to read back; a snapshot from an older schema is
                // correctly unreadable, and GenomeJson.FormatVersion exists to say so plainly
                // rather than fail on a missing field twelve levels down. Skipped and counted,
                // never quietly passed over — a run of nothing but old files would otherwise
                // report success while testing zero genomes. Decided from the first row, so a
                // stale file is never read in full.
                if (FormatOf(first) is int format && format != GenomeJson.FormatVersion)
                {
                    stale++;
                    continue;
                }

                // Only a current-format file reaches a full read. ReadRows rather than
                // ReadAllLines for the same sharing reason as FirstLine above.
                string[] rows;
                try { rows = JsonlWriter.ReadRows(file); }
                catch (Exception e) { failures.Add($"{Path.GetFileName(file)}: unreadable — {e.Message}"); continue; }

                // The first line was non-blank, but the row it belongs to may not have a trailing
                // newline yet if a run is still writing it — ReadRows drops an incomplete last
                // line, which can leave zero complete rows even though the peek above saw content.
                if (rows.Length == 0)
                {
                    empty++;
                    continue;
                }

                for (int i = 0; i < rows.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(rows[i])) continue;

                    try
                    {
                        Genome genome = GenomeJson.Read(rows[i]);
                        genomes++;
                        parts += genome.Nodes.Count;

                        for (int n = 0; n < genome.Nodes.Count; n++)
                        {
                            cellTypes.Add(genome.Nodes[n].CellTypeId);
                            neurons += genome.Nodes[n].Neurons.Length;
                        }
                        neurons += genome.GlobalBrain.Length;

                        // Readable is not the same as usable. A founder pool feeds these straight
                        // into development, and Validate is what development demands.
                        IReadOnlyList<string> issues = genome.Validate();
                        if (issues.Count > 0)
                        {
                            failures.Add(
                                $"{Path.GetFileName(file)} row {i + 1}: parses but is invalid — {issues[0]}");
                        }
                    }
                    catch (Exception e)
                    {
                        failures.Add($"{Path.GetFileName(file)} row {i + 1}: {e.Message}");
                    }
                }
            }

            _output.WriteLine(
                $"{genomes:N0} genomes from {files - stale} snapshot(s): {parts:N0} parts, " +
                $"{neurons:N0} neurons, cell types [{string.Join(", ", cellTypes)}]");
            if (stale > 0)
            {
                _output.WriteLine(
                    $"{stale} snapshot(s) skipped: written before genome format " +
                    $"{GenomeJson.FormatVersion} and correctly unreadable by this build.");
            }
            if (empty > 0) _output.WriteLine($"{empty} snapshot(s) held no rows at all.");

            Assert.True(
                failures.Count == 0,
                $"{failures.Count} of {genomes} genomes could not be read back or were invalid:" +
                Environment.NewLine + string.Join(Environment.NewLine, failures.GetRange(0, Math.Min(5, failures.Count))));

            Assert.True(
                genomes > 0 || stale + empty == files,
                "snapshots existed but held no genomes");
        }

        /// <summary>
        /// The file's first line, or null if it has none. Opened with <see cref="FileShare.ReadWrite"/>
        /// rather than <c>File.ReadAllLines</c>'s default share mode, because a live run holds the
        /// file open for append and that mode throws a sharing violation.
        /// </summary>
        private static string FirstLine(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false)))
            {
                return reader.ReadLine();
            }
        }

        /// <summary>The declared format of a genome row, or null if it will not even parse.</summary>
        private static int? FormatOf(string row)
        {
            try { return Json.Parse(row)["format"].AsInt(); }
            catch { return null; }
        }
    }
}
