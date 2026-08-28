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
    /// </remarks>
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

            int files = 0, genomes = 0, parts = 0, neurons = 0, stale = 0;
            var cellTypes = new HashSet<string>();
            var failures = new List<string>();

            foreach (string file in snapshots)
            {
                files++;

                // ReadRows rather than ReadAllLines: the latter opens with FileShare.Read, which
                // will not coexist with a live writer and throws a sharing violation. Runs are
                // usually in flight when this matters.
                string[] rows;
                try { rows = JsonlWriter.ReadRows(file); }
                catch (Exception e) { failures.Add($"{Path.GetFileName(file)}: unreadable — {e.Message}"); continue; }

                // Archaeology is not a regression. This guards that a run cannot write a genome
                // *this build* is unable to read back; a snapshot from an older schema is
                // correctly unreadable, and GenomeJson.FormatVersion exists to say so plainly
                // rather than fail on a missing field twelve levels down. Skipped and counted,
                // never quietly passed over — a run of nothing but old files would otherwise
                // report success while testing zero genomes.
                if (rows.Length > 0 && !string.IsNullOrWhiteSpace(rows[0]) &&
                    FormatOf(rows[0]) is int format && format != GenomeJson.FormatVersion)
                {
                    stale++;
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

            Assert.True(
                failures.Count == 0,
                $"{failures.Count} of {genomes} genomes could not be read back or were invalid:" +
                Environment.NewLine + string.Join(Environment.NewLine, failures.GetRange(0, Math.Min(5, failures.Count))));

            Assert.True(
                genomes > 0 || stale == files,
                "snapshots existed but held no genomes");
        }

        /// <summary>The declared format of a genome row, or null if it will not even parse.</summary>
        private static int? FormatOf(string row)
        {
            try { return Json.Parse(row)["format"].AsInt(); }
            catch { return null; }
        }
    }
}
