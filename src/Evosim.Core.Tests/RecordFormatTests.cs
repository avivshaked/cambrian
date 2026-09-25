using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Record format 2's files and readers (<c>logbook/specs/record-and-film-spec.md</c> Part A):
    /// gzip in members with a torn last member skipped and never parsed, the slim snapshot row
    /// and its join back to format 1's row to the byte, a slim row with no genome refused and
    /// counted, the old record still read, and the dispatch on <c>run.json</c> or the files.
    /// </summary>
    /// <remarks>
    /// Written under the test binary's own directory rather than the system temp, under the
    /// project's rule that nothing of it is written outside the repository.
    /// </remarks>
    public class RecordFormatTests : IDisposable
    {
        private readonly string _root;

        public RecordFormatTests()
        {
            _root = Path.Combine(AppContext.BaseDirectory, "record-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
            catch (IOException) { }
        }

        private static readonly DateTime Started = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

        private string File_(string name) => Path.Combine(_root, name);

        private static List<string> Lines(string path, out GzipMemberReaderState state)
        {
            using (GzipMemberReader reader = GzipMemberReader.Open(path))
            {
                var lines = new List<string>(reader.Lines());
                state = new GzipMemberReaderState
                {
                    Members = reader.CompleteMembers,
                    Torn = reader.Torn,
                    TornAt = reader.TornAt,
                    TornPromised = reader.TornPromised,
                    Note = reader.TornNote,
                };
                return lines;
            }
        }

        private sealed class GzipMemberReaderState
        {
            public int Members;
            public bool Torn;
            public long TornAt;
            public long TornPromised;
            public string Note;
        }

        // ------------------------------------------------------------------ the member

        [Fact]
        public void AMemberCarriesItsOwnLengthAndInflatesToWhatWentIn()
        {
            byte[] data = Encoding.UTF8.GetBytes("{\"id\":1}\n{\"id\":2}\n" + new string('x', 5000) + "\n");

            byte[] member = GzipMembers.Member(data, 0, data.Length);

            Assert.Equal(0x1f, member[0]);
            Assert.Equal(0x8b, member[1]);
            Assert.Equal(0x04, member[3]);
            Assert.Equal((byte)'E', member[12]);
            Assert.Equal((byte)'V', member[13]);
            Assert.Equal(member.LongLength, GzipMembers.LengthOf(member));

            Assert.Equal(data, GzipMembers.Inflate(member, 0, member.Length));
        }

        [Fact]
        public void RowsRoundTripAcrossMembers()
        {
            string path = File_("rows.jsonl.gz");
            var written = new List<string>();

            using (var writer = new JsonlGzWriter(path, memberEachRow: false))
            {
                for (int m = 0; m < 4; m++)
                {
                    for (int i = 0; i < 25; i++)
                    {
                        string row = "{\"id\":" + (m * 100 + i) + ",\"v\":\"" + new string('a', i) + "\"}";
                        writer.Write(row);
                        written.Add(row);
                    }

                    writer.EndMember();
                }

                // A drain with nothing in it writes no empty member.
                writer.EndMember();
                Assert.Equal(4, writer.MemberCount);
            }

            List<string> read = Lines(path, out GzipMemberReaderState state);

            Assert.Equal(written, read);
            Assert.Equal(4, state.Members);
            Assert.False(state.Torn);
            Assert.Null(state.Note);
        }

        [Fact]
        public void AMemberEachRowWritesOneMemberPerRow()
        {
            string path = File_("each.jsonl.gz");

            using (var writer = new JsonlGzWriter(path, memberEachRow: true))
            {
                writer.Write("{\"t\":10,\"n\":0,\"b\":[]}");
                writer.Write("{\"t\":20,\"n\":0,\"b\":[]}");
                writer.Write("{\"t\":30,\"n\":0,\"b\":[]}");
            }

            List<string> read = Lines(path, out GzipMemberReaderState state);

            Assert.Equal(3, read.Count);
            Assert.Equal(3, state.Members);
        }

        [Fact]
        public void ARowWithALineBreakIsRefused()
        {
            using (var writer = new JsonlGzWriter(File_("refuse.jsonl.gz"), memberEachRow: false))
            {
                Assert.Throws<ArgumentException>(() => writer.Write("{\"a\":1}\n{\"b\":2}"));
            }
        }

        // ------------------------------------------------------------------ a killed write

        [Theory]
        [InlineData(1)]      // the last byte of the trailer
        [InlineData(20)]     // inside the deflate stream
        [InlineData(-10)]    // inside the header's length field: measured from the member's start
        public void ATornLastMemberIsReportedAndSkippedAndNeverParsed(int cut)
        {
            string path = File_("torn.jsonl.gz");
            long thirdAt;

            using (var writer = new JsonlGzWriter(path, memberEachRow: true))
            {
                writer.Write("{\"t\":10}");
                writer.Write("{\"t\":20}");
            }

            thirdAt = new FileInfo(path).Length;

            using (var writer = new JsonlGzWriter(path, memberEachRow: true))
            {
                writer.Write("{\"t\":30,\"pad\":\"" + new string('q', 400) + "\"}");
            }

            long whole = new FileInfo(path).Length;
            long keep = cut > 0 ? whole - cut : thirdAt - cut;

            using (var file = new FileStream(path, FileMode.Open, FileAccess.Write))
            {
                file.SetLength(keep);
            }

            List<string> read = Lines(path, out GzipMemberReaderState state);

            Assert.Equal(new[] { "{\"t\":10}", "{\"t\":20}" }, read);
            Assert.True(state.Torn);
            Assert.Equal(2, state.Members);
            Assert.Equal(thirdAt, state.TornAt);
            Assert.NotNull(state.Note);

            // A cut inside the length field leaves a header too short to promise anything.
            if (cut < 0) Assert.Equal(0, state.TornPromised);
            else Assert.Equal(whole - thirdAt, state.TornPromised);
        }

        [Fact]
        public void ADamagedCompleteMemberIsRefusedNotSkipped()
        {
            string path = File_("damaged.jsonl.gz");

            using (var writer = new JsonlGzWriter(path, memberEachRow: true))
            {
                writer.Write("{\"t\":10,\"pad\":\"" + new string('z', 300) + "\"}");
            }

            byte[] all = File.ReadAllBytes(path);
            all[all.Length - 12] ^= 0x5a;   // inside the deflate stream, before the trailer
            File.WriteAllBytes(path, all);

            Assert.Throws<InvalidDataException>(() => Lines(path, out _));
        }

        [Fact]
        public void AFileThatIsNotARecordMemberIsRefused()
        {
            string path = File_("plain.jsonl.gz");
            File.WriteAllText(path, new string('{', 200));

            Assert.Throws<InvalidDataException>(() => Lines(path, out _));
        }

        // ------------------------------------------------------------------ the slim row and the join

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void TheJoinIsFormatOnesRowToTheByte(bool counts, bool lost)
        {
            Genome genome = GenomeFactory.Random(new Rng(11));
            const long id = 4217;

            int[] moduleCounts = counts ? new[] { 1, 3, 2 } : null;
            List<int[]> lostPaths = lost ? new List<int[]> { new[] { 0, 1 }, new[] { 2 } } : null;

            string old = GenomeJson.Write(genome, indent: false, id: id,
                moduleCounts: moduleCounts, lostPartPaths: lostPaths);

            string genomeRow = GenomeJson.Write(genome, indent: false, id: id);
            string slim = RecordFiles.SlimRow(id, moduleCounts, lostPaths, 0.3125f);

            Assert.Equal(old, RecordFiles.Join(slim, genomeRow));
            Assert.Equal(0.3125f, RecordFiles.BodyFractionOf(slim));
            Assert.Equal(id, RecordFiles.IdOf(slim));

            // The converter's slim row carries no fraction, and joins the same.
            string noFraction = RecordFiles.SlimRow(id, moduleCounts, lostPaths, float.NaN);
            Assert.DoesNotContain("\"bf\"", noFraction);
            Assert.Equal(old, RecordFiles.Join(noFraction, genomeRow));
            Assert.True(float.IsNaN(RecordFiles.BodyFractionOf(noFraction)));

            // And the joined row reads back through the same readers the theatre uses.
            Assert.Equal(id, GenomeJson.ReadId(RecordFiles.Join(slim, genomeRow)));
            Assert.Equal(GenomeJson.Write(genome), GenomeJson.Write(GenomeJson.Read(RecordFiles.Join(slim, genomeRow))));
        }

        [Fact]
        public void TheJoinRefusesAnotherBodysGenome()
        {
            Genome genome = GenomeFactory.Random(new Rng(12));

            Assert.Throws<FormatException>(() => RecordFiles.Join(
                RecordFiles.SlimRow(7, null, null, 1f), GenomeJson.Write(genome, indent: false, id: 8)));

            Assert.Throws<FormatException>(() => RecordFiles.Join(
                "{\"id\":7,\"bf\":1,\"later\":2}", GenomeJson.Write(genome, indent: false, id: 7)));
        }

        // ------------------------------------------------------------------ a snapshot, both records

        private string FormatTwoRun(out Dictionary<long, string> oldRows, long missing)
        {
            string run = Path.Combine(_root, "run2");
            Directory.CreateDirectory(Path.Combine(run, "snapshots"));
            oldRows = new Dictionary<long, string>();

            var slim = new List<string>();

            using (var genomes = new JsonlGzWriter(Path.Combine(run, RecordFiles.GenomesName), memberEachRow: false))
            {
                for (long id = 10; id < 16; id++)
                {
                    Genome g = GenomeFactory.Random(new Rng((ulong)id));
                    int[] counts = id % 2 == 0 ? new[] { 2, 1 } : null;

                    oldRows[id] = GenomeJson.Write(g, indent: false, id: id, moduleCounts: counts);
                    slim.Add(RecordFiles.SlimRow(id, counts, null, 0.1f * (id - 9)));

                    if (id != missing) genomes.Write(GenomeJson.Write(g, indent: false, id: id));

                    // Two drains, so the walk crosses a member boundary.
                    if (id == 12) genomes.EndMember();
                }
            }

            using (var writer = new JsonlGzWriter(
                       Path.Combine(run, "snapshots", RecordFiles.SnapshotName(100, RunRecordFormat.Compact)),
                       memberEachRow: false))
            {
                foreach (string row in slim) writer.Write(row);
            }

            File.WriteAllText(Path.Combine(run, "run.json"), "{\n  \"arm\": \"t\",\n  \"recordFormat\": 2\n}");
            return run;
        }

        [Fact]
        public void AFormatTwoSnapshotJoinsEveryRowAndRefusesAndCountsOneWithoutAGenome()
        {
            string run = FormatTwoRun(out Dictionary<long, string> oldRows, missing: 13);

            Assert.Equal(RunRecordFormat.Compact, RecordFiles.FormatOf(run));
            Assert.Equal(new[] { 100d }, RecordFiles.SnapshotSeconds(run, RunRecordFormat.Compact));

            SnapshotRead read = RecordFiles.ReadSnapshot(run, 100, RunRecordFormat.Compact);

            Assert.Equal(1, read.WithoutAGenome);
            Assert.Equal(new List<long> { 13 }, read.RefusedIds);
            Assert.Equal(5, read.Rows.Length);
            Assert.Empty(read.Notes);

            for (int i = 0; i < read.Rows.Length; i++)
            {
                Assert.Equal(oldRows[read.Ids[i]], read.Rows[i]);
                Assert.Equal(0.1f * (read.Ids[i] - 9), read.BodyFractions[i]);
            }
        }

        [Fact]
        public void TheOldRecordIsStillRead()
        {
            string run = Path.Combine(_root, "run1");
            Directory.CreateDirectory(Path.Combine(run, "snapshots"));

            var rows = new List<string>();
            for (long id = 1; id <= 3; id++)
            {
                rows.Add(GenomeJson.Write(GenomeFactory.Random(new Rng((ulong)(40 + id))), indent: false, id: id));
            }

            File.WriteAllText(
                Path.Combine(run, "snapshots", "000000200.jsonl"), string.Join("\n", rows) + "\n");

            // A manifest from before the build: no recordFormat.
            File.WriteAllText(Path.Combine(run, "run.json"), "{\n  \"arm\": \"old\"\n}");

            Assert.Equal(RunRecordFormat.Jsonl, RecordFiles.FormatOf(run));

            SnapshotRead read = RecordFiles.ReadSnapshot(run, 200, RunRecordFormat.Jsonl);

            Assert.Equal(rows.ToArray(), read.Rows);
            Assert.Equal(new long[] { 1, 2, 3 }, read.Ids);
            Assert.All(read.BodyFractions, bf => Assert.True(float.IsNaN(bf)));
            Assert.Equal(0, read.WithoutAGenome);

            // The other record holds nothing at that second, and says so with a null.
            Assert.Null(RecordFiles.ReadSnapshot(run, 200, RunRecordFormat.Compact));
        }

        [Fact]
        public void TheFormatIsReadFromTheManifestTheMarkOrTheFiles()
        {
            string a = Path.Combine(_root, "a");
            Directory.CreateDirectory(a);
            File.WriteAllText(Path.Combine(a, "run.json"), "{ \"recordFormat\": 1 }");
            Assert.Equal(RunRecordFormat.Jsonl, RecordFiles.FormatOf(a));

            string b = Path.Combine(_root, "b");
            Directory.CreateDirectory(b);
            File.WriteAllText(Path.Combine(b, "run.json"), "{ \"arm\": \"x\" }");
            File.WriteAllText(Path.Combine(b, RecordFiles.ConvertedName), "{}");
            Assert.Equal(RunRecordFormat.Compact, RecordFiles.FormatOf(b, out string how));
            Assert.Contains(RecordFiles.ConvertedName, how);

            string c = Path.Combine(_root, "c");
            Directory.CreateDirectory(c);
            File.WriteAllText(Path.Combine(c, "run.json"), "{ \"recordFormat\": 7 }");
            Assert.Throws<FormatException>(() => RecordFiles.FormatOf(c));

            string d = Path.Combine(_root, "d");
            Directory.CreateDirectory(d);
            File.WriteAllBytes(Path.Combine(d, RecordFiles.GenomesName), Array.Empty<byte>());
            Assert.Equal(RunRecordFormat.Compact, RecordFiles.FormatOf(d));

            string e = Path.Combine(_root, "e");
            Directory.CreateDirectory(e);
            Assert.Equal(RunRecordFormat.Jsonl, RecordFiles.FormatOf(e));
        }

        // ------------------------------------------------------------------ the run directory

        [Fact]
        public void AFormatTwoDirectoryWritesGzippedPositionsAndAGenomeFileAndFormatOneIsUnchanged()
        {
            var config = new RunConfig { SharedSpace = true };
            string row = PositionsRow.Write(
                100d, 1, new long[] { 1 }, new[] { 2f }, new[] { -3f }, new[] { 4f },
                new[] { PositionsRow.AbsorptiveBit });

            string compactRoot = Path.Combine(_root, "compact");
            string compact;

            using (RunDirectory dir = RunDirectory.Create(compactRoot, config, Started, RunRecordFormat.Compact))
            {
                compact = dir.Path;
                Assert.Equal(RunRecordFormat.Compact, dir.Format);
                Assert.Null(dir.Positions);
                Assert.NotNull(dir.PositionMembers);
                Assert.NotNull(dir.Genomes);
                Assert.True(dir.RecordsPositions);
                Assert.EndsWith("000000100.jsonl.gz", dir.SnapshotFilePath(100));

                dir.WritePositions(row);
                dir.WritePositions(row.Replace("\"t\":100", "\"t\":110"));
            }

            Assert.False(File.Exists(Path.Combine(compact, RecordFiles.PositionsName)));
            Assert.True(File.Exists(Path.Combine(compact, RecordFiles.GenomesName)));

            var read = new List<string>(RecordFiles.PositionLines(compact, RunRecordFormat.Compact));
            Assert.Equal(new[] { row, row.Replace("\"t\":100", "\"t\":110") }, read);

            string plainRoot = Path.Combine(_root, "plain");
            string plain;

            using (RunDirectory dir = RunDirectory.Create(plainRoot, config, Started))
            {
                plain = dir.Path;
                Assert.Equal(RunRecordFormat.Jsonl, dir.Format);
                Assert.NotNull(dir.Positions);
                Assert.Null(dir.PositionMembers);
                Assert.Null(dir.Genomes);

                dir.WritePositions(row);
            }

            Assert.False(File.Exists(Path.Combine(plain, RecordFiles.PositionsMembersName)));
            Assert.False(File.Exists(Path.Combine(plain, RecordFiles.GenomesName)));
            Assert.Equal(row + "\n", File.ReadAllText(Path.Combine(plain, RecordFiles.PositionsName)));
            Assert.Equal(new[] { row }, new List<string>(RecordFiles.PositionLines(plain, RunRecordFormat.Jsonl)));
        }

        [Fact]
        public void ATornPositionsMemberIsSaidAndSkipped()
        {
            string run = Path.Combine(_root, "tornpos");
            Directory.CreateDirectory(run);
            string path = Path.Combine(run, RecordFiles.PositionsMembersName);

            using (var writer = new JsonlGzWriter(path, memberEachRow: true))
            {
                writer.Write("{\"t\":10,\"n\":0,\"b\":[]}");
                writer.Write("{\"t\":20,\"n\":0,\"b\":[]}");
            }

            using (var file = new FileStream(path, FileMode.Open, FileAccess.Write))
            {
                file.SetLength(file.Length - 3);
            }

            string note = null;
            var read = new List<string>(RecordFiles.PositionLines(run, RunRecordFormat.Compact, n => note = n));

            Assert.Equal(new[] { "{\"t\":10,\"n\":0,\"b\":[]}" }, read);
            Assert.NotNull(note);
        }

        // ------------------------------------------------------------------ the world's queue

        [Fact]
        public void TheAdmissionQueueIsOffByDefaultAndOnGivesOneGenomePerBirthRow()
        {
            var config = new RunConfig { MinimumPopulation = 20, MaximumPopulation = 2_000 };
            config.Light = new LightModel(4000f, 40f);

            var off = new World(config, seed: 1);
            off.Step(1f);
            Assert.NotEmpty(off.DrainLineageEvents());
            Assert.Empty(off.DrainAdmittedGenomes());

            var on = new World(config, seed: 1) { QueueAdmittedGenomes = true };

            var births = new List<long>();
            var genomes = new List<long>();

            // Sixty seconds: the light is generous enough that the crowd passes four hundred
            // near 80 s, and a short run holds hundreds of births without a runaway.
            for (int i = 0; i < 60; i++)
            {
                on.Step(1f);

                foreach (LineageEvent e in on.DrainLineageEvents())
                {
                    if (e.Kind == LineageEventKind.Birth) births.Add(e.Id);
                }

                foreach (AdmittedGenome g in on.DrainAdmittedGenomes())
                {
                    Assert.NotNull(g.Genome);
                    genomes.Add(g.Id);
                }
            }

            Assert.NotEmpty(births);
            Assert.Equal(births, genomes);
        }

        [Fact]
        public void TheQueueChangesNothingTheWorldDoes()
        {
            var config = new RunConfig { MinimumPopulation = 20, MaximumPopulation = 2_000 };
            config.Light = new LightModel(4000f, 40f);

            var off = new World(config, seed: 3);
            var on = new World(config, seed: 3) { QueueAdmittedGenomes = true };

            for (int i = 0; i < 60; i++)
            {
                off.Step(1f);
                on.Step(1f);
            }

            Assert.Equal(off.Living.Count, on.Living.Count);
            Assert.Equal(off.Births, on.Births);
            Assert.Equal(off.Deaths, on.Deaths);
            Assert.Equal(off.EnergyIn, on.EnergyIn);
            Assert.Equal(off.EnergyOut, on.EnergyOut);
        }
    }
}
