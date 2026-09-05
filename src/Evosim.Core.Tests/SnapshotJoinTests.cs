using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Every id on a snapshot row is a creature <c>lineage.jsonl</c> says was alive at that
    /// moment — D075 item 2, the theatre's join.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A join is not testable one file at a time.</b> <see cref="SnapshotReadbackTests"/>
    /// asks whether a genome a run wrote can be read back; this asks whether the id beside it
    /// means anything. A snapshot row carrying an id nothing was ever born under, or one whose
    /// creature had already died, is worse than a row with no id at all — a viewer would name a
    /// creature confidently and wrongly, which is this project's recurring failure shape.
    /// </para>
    /// <para>
    /// <b>Skips rather than fails when there is nothing to check</b>, like the readback test it
    /// sits beside: <c>runs/</c> is gitignored, and snapshots written before
    /// <see cref="GenomeJson.FormatVersion"/> carry no ids at all. A run still in flight is
    /// skipped per snapshot: <c>lineage.jsonl</c> is buffered, so its last rows can lag the last
    /// snapshot, and a snapshot past the end of the lineage would fail for a reason that is not
    /// a fault.
    /// </para>
    /// </remarks>
    public class SnapshotJoinTests
    {
        private readonly ITestOutputHelper _output;

        public SnapshotJoinTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void SnapshotIdsJoinLineage()
        {
            string root = RepositoryRoot();
            if (root == null) { _output.WriteLine("no repository root found — skipped"); return; }

            string runs = Path.Combine(root, "runs");
            if (!Directory.Exists(runs)) { _output.WriteLine("no runs/ — skipped"); return; }

            int runsChecked = 0, snapshotsChecked = 0, idsChecked = 0;
            var failures = new List<string>();

            foreach (string runDir in Directory.GetDirectories(runs, "*", SearchOption.AllDirectories))
            {
                string lineagePath = Path.Combine(runDir, "lineage.jsonl");
                string snapshotDir = Path.Combine(runDir, "snapshots");
                if (!File.Exists(lineagePath) || !Directory.Exists(snapshotDir)) continue;

                // Only snapshots this build can read. An older format has no ids on it, which is
                // archaeology and not a regression.
                var current = new List<string>();
                foreach (string file in Directory.GetFiles(snapshotDir, "*.jsonl"))
                {
                    // The first row only. Deciding a file's format by reading all of it would
                    // make this test read every snapshot of every run in the archive to discover
                    // that almost none of them are relevant.
                    string first = FirstRow(file);
                    if (first == null) continue;
                    if (FormatOf(first) != GenomeJson.FormatVersion) continue;
                    current.Add(file);
                }

                if (current.Count == 0) continue;

                // Streamed by row rather than loaded as a document: a long run's lineage.jsonl
                // reaches hundreds of megabytes (CLAUDE.md, on clade-score).
                var bornAt = new Dictionary<long, double>();
                var diedAt = new Dictionary<long, double>();
                double lastLineageTime = double.NegativeInfinity;

                foreach (string line in JsonlWriter.ReadRows(lineagePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    JsonNode row;
                    try { row = Json.Parse(line); }
                    catch { continue; }

                    long id = (long)row["id"].AsDouble();
                    double t = row["t"].AsDouble();
                    if (t > lastLineageTime) lastLineageTime = t;

                    if (row["e"].AsString() == "b") bornAt[id] = t;
                    else diedAt[id] = t;
                }

                // Whether the lineage is complete up to a given moment is a question about the
                // run, not about its last row: a world with no birth and no death in its final
                // hundred seconds has a last lineage event well before its last snapshot, and
                // reading that as "the file lags" skipped every run there was. The manifest
                // answers it directly — an ended run's lineage covers every second it simulated.
                double lineageCovers = lastLineageTime;
                string manifestPath = Path.Combine(runDir, "run.json");

                if (File.Exists(manifestPath))
                {
                    try
                    {
                        JsonNode manifest = Json.Parse(File.ReadAllText(manifestPath));
                        if (manifest.OptionalString("status", "running") != "running" &&
                            manifest.Has("simulatedSeconds"))
                        {
                            lineageCovers = Math.Max(
                                lineageCovers, manifest["simulatedSeconds"].AsDouble());
                        }
                    }
                    catch
                    {
                        // A manifest that will not parse is not this test's business; the
                        // last-row rule still applies.
                    }
                }

                runsChecked++;

                foreach (string file in current)
                {
                    // The snapshot's own time comes from its name; the writer zero-pads it so a
                    // listing sorts chronologically, which is also what makes it parseable.
                    if (!double.TryParse(
                            Path.GetFileNameWithoutExtension(file),
                            NumberStyles.Integer, CultureInfo.InvariantCulture,
                            out double snapshotTime))
                    {
                        continue;
                    }

                    // A lineage that stops short of the snapshot is a live or killed run whose
                    // last rows are still buffered, not a broken join.
                    if (lineageCovers < snapshotTime) continue;

                    snapshotsChecked++;
                    string name = Path.GetFileName(runDir) + "/" + Path.GetFileName(file);

                    foreach (string line in JsonlWriter.ReadRows(file))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        long id = GenomeJson.ReadId(line);
                        if (id == GenomeJson.NoId)
                        {
                            failures.Add(name + ": a row carries no id");
                            continue;
                        }

                        idsChecked++;

                        if (!bornAt.TryGetValue(id, out double birth))
                        {
                            failures.Add($"{name}: id {id} appears in no birth row");
                            continue;
                        }

                        // Born at the snapshot's own second counts: a snapshot is taken at the
                        // end of a metabolic step, after that step's births.
                        if (birth > snapshotTime)
                        {
                            failures.Add($"{name}: id {id} was born at t={birth}, after the snapshot");
                        }

                        if (diedAt.TryGetValue(id, out double death) && death < snapshotTime)
                        {
                            failures.Add($"{name}: id {id} died at t={death}, before the snapshot");
                        }
                    }
                }
            }

            if (runsChecked == 0 || snapshotsChecked == 0)
            {
                _output.WriteLine(
                    "no run carried both a lineage and a format-" + GenomeJson.FormatVersion +
                    " snapshot within it — skipped");
                return;
            }

            _output.WriteLine(
                $"{idsChecked:N0} snapshot ids from {snapshotsChecked} snapshot(s) across " +
                $"{runsChecked} run(s) joined lineage.jsonl");

            Assert.True(
                failures.Count == 0,
                $"{failures.Count} snapshot id(s) did not join:" + Environment.NewLine +
                string.Join(Environment.NewLine, failures.GetRange(0, Math.Min(5, failures.Count))));
        }

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

        /// <summary>The first complete line of a file, or null — the live-run-safe read.</summary>
        private static string FirstRow(string path)
        {
            try
            {
                using (var stream = new FileStream(
                           path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadLine();
                }
            }
            catch { return null; }
        }

        /// <summary>The declared format of a genome row, or null if it will not even parse.</summary>
        private static int? FormatOf(string row)
        {
            try { return Json.Parse(row)["format"].AsInt(); }
            catch { return null; }
        }
    }
}
