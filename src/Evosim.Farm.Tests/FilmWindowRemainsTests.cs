using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D220's remains in a film window: the rows <see cref="FilmWindow.RemainsRecorder"/> writes for a world's
    /// standing remains, a window of a world with remains carrying them, the reader that reads them back, and the
    /// compatibility the rows owe a reader or a window that does not know them. The window of a world without
    /// contact eating being the window the farm wrote before is <see cref="FilmWindowDigestTests"/>'.
    /// </summary>
    [Collection(FarmProcessCollection.Name)]
    public class FilmWindowRemainsTests : IClassFixture<FilmWindowRemainsTests.RemainsRun>
    {
        private readonly RemainsRun _run;
        private readonly Xunit.Abstractions.ITestOutputHelper _out;

        public FilmWindowRemainsTests(RemainsRun run, Xunit.Abstractions.ITestOutputHelper output)
        {
            _run = run;
            _out = output;
        }

        // ------------------------------------------------------------------ the rows, from a fixture world

        private static void Bite(RemainsFixtures.Pair pair, long biter, long target, int part)
        {
            pair.World.SetContacts(new List<CreatureContact> { new CreatureContact(biter, 0, target, part) });
            pair.World.ApplyMouth(100f);
        }

        private static List<JsonNode> Rows(List<string> lines, string kind) =>
            lines.Where(l => l.StartsWith("{\"k\":\"" + kind + "\"", StringComparison.Ordinal)).Select(Json.Parse).ToList();

        private static int[] Ints(JsonNode array)
        {
            var list = new List<int>();
            foreach (JsonNode item in array.Items()) list.Add(item.AsInt());
            return list.ToArray();
        }

        /// <summary>
        /// A body that died before the window opened is a first row marked as standing at the start, with its
        /// genome in the remains file and no map; a bite that eats one of its parts whole writes a shed row for it
        /// and a first row, with a genome of its own, for the limb cut off, naming the body it was cut from.
        /// </summary>
        [Fact]
        public void RemainsAtTheStartAndALimbCutOffInsideTheWindowAreWrittenWithTheirMapsGenomesAndSince()
        {
            using (RemainsFixtures.Pair p = RemainsFixtures.StarvedArena(RemainsFixtures.Config(true), RemainsFixtures.Fork(4), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                Organism claw = p.World.Living[1];

                RemainsFixtures.Starve(p);
                Assert.True(victim.IsRemains);
                double since = victim.RemainsSince;

                var lines = new List<string>();
                var recorder = new FilmWindow.RemainsRecorder(lines.Add);

                // The opening: the dead body stands, and its genome is nowhere in the window's own file.
                recorder.Capture(p.World.Remains, 10d, atStart: true, id => false, id => false);

                Assert.Equal(1, recorder.Remains);
                Assert.Equal(1, recorder.Genomes);
                Assert.Equal(2, lines.Count);
                Assert.Equal(victim.Id, GenomeJson.ReadId(lines[0]));

                JsonNode first = Rows(lines, "remains").Single();
                Assert.Equal(victim.Id, (long)first["id"].AsDouble());
                Assert.Equal(-1L, (long)first["from"].AsDouble());
                Assert.True(first["start"].AsBool());
                Assert.True(first["genome"].AsBool());
                Assert.Equal(since, first["since"].AsDouble(), 9);
                Assert.Equal(10d, first["t"].AsDouble(), 9);
                Assert.Equal(victim.Phenotype.ScaledBy, (float)first["scaledBy"].AsDouble());
                Assert.Equal(JsonNode.NodeKind.Null, first["map"].Kind);

                // The same remains a step later write nothing.
                recorder.Capture(p.World.Remains, 10.5d, atStart: false, id => false, id => false);
                Assert.Equal(2, lines.Count);

                // Part 1 is eaten whole: the first spine beyond it is cut off, the second stays.
                Bite(p, claw.Id, victim.Id, 1);
                Assert.Equal(2, p.World.Remains.Count);
                Organism limb = p.World.Remains.First(r => r.Id != victim.Id);

                recorder.Capture(p.World.Remains, 11d, atStart: false, id => false, id => false);

                Assert.Equal(2, recorder.Remains);
                Assert.Equal(1, recorder.Sheds);
                Assert.Equal(2, recorder.Genomes);

                JsonNode shed = Rows(lines, "shed").Single();
                Assert.Equal(victim.Id, (long)shed["id"].AsDouble());
                Assert.Equal(11d, shed["t"].AsDouble(), 9);
                Assert.Equal(new[] { 0, 5, 6, 7, 8 }, Ints(shed["map"]));

                JsonNode cut = Rows(lines, "remains").Single(r => (long)r["id"].AsDouble() == limb.Id);
                Assert.Equal(victim.Id, (long)cut["from"].AsDouble());
                Assert.False(cut["start"].AsBool());
                Assert.True(cut["genome"].AsBool());
                Assert.Equal(new[] { 2, 3, 4 }, Ints(cut["map"]));

                // The limb keeps the death second of the body it was cut from, and so does the body's remains.
                Assert.Equal(since, cut["since"].AsDouble(), 9);
                Assert.Equal(limb.RemainsSince, cut["since"].AsDouble(), 9);

                // And its genome row is the body's genome, under the limb's own id.
                string limbGenome = lines.Single(l => !l.StartsWith("{\"k\":", StringComparison.Ordinal) && GenomeJson.ReadId(l) == limb.Id);
                Assert.Equal(GenomeJson.Write(victim.Genome, indent: false, id: limb.Id), limbGenome);

                // Nothing more happens to the pair: another step writes nothing.
                int written = lines.Count;
                recorder.Capture(p.World.Remains, 11.5d, atStart: false, id => false, id => false);
                Assert.Equal(written, lines.Count);
            }
        }

        /// <summary>
        /// A body that died inside the window is in the window's own genomes file, so its first row says so and
        /// carries no genome; a body whose genome waits on the admission queue is the same.
        /// </summary>
        [Fact]
        public void ABodyThatDiedInsideTheWindowIsAFirstRowWithNoGenomeOfItsOwnAndNoFounder()
        {
            using (RemainsFixtures.Pair p = RemainsFixtures.StarvedArena(RemainsFixtures.Config(true), RemainsFixtures.Fork(4), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                RemainsFixtures.Starve(p);

                foreach (bool queued in new[] { false, true })
                {
                    var lines = new List<string>();
                    var recorder = new FilmWindow.RemainsRecorder(lines.Add);

                    recorder.Capture(
                        p.World.Remains, 20d, atStart: false,
                        id => !queued && id == victim.Id,
                        id => queued && id == victim.Id);

                    Assert.Single(lines);
                    Assert.Equal(0, recorder.Genomes);

                    JsonNode row = Rows(lines, "remains").Single();
                    Assert.False(row["start"].AsBool());
                    Assert.False(row["genome"].AsBool());
                    Assert.Equal(-1L, (long)row["from"].AsDouble());
                    Assert.Equal(victim.RemainsSince, row["since"].AsDouble(), 9);
                }
            }
        }

        /// <summary>A body that was in the queue and died before the window opened is a start remains and gets its genome.</summary>
        [Fact]
        public void AStaleAdmissionAtTheOpeningDoesNotKeepARemainsBodyFromItsGenome()
        {
            using (RemainsFixtures.Pair p = RemainsFixtures.StarvedArena(RemainsFixtures.Config(true), RemainsFixtures.Fork(4), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                RemainsFixtures.Starve(p);

                var lines = new List<string>();
                var recorder = new FilmWindow.RemainsRecorder(lines.Add);
                recorder.Capture(p.World.Remains, 5d, atStart: true, id => false, id => true);

                Assert.Equal(1, recorder.Genomes);
                Assert.Equal(victim.Id, GenomeJson.ReadId(lines[0]));
            }
        }

        // ------------------------------------------------------------------ a recorded run, a window of it

        /// <summary>
        /// A sixty second run of the small world with contact eating on and D128's hazard at a base of 15 s, so that
        /// bodies die of age from the first quarter minute on and the dead stay edible for the remains window.
        /// </summary>
        public sealed class RemainsRun : IDisposable
        {
            public string Root { get; }
            public string RunDirectory { get; }

            public RemainsRun()
            {
                Root = Path.Combine(AppContext.BaseDirectory, "remains-film-tests", Guid.NewGuid().ToString("N"));
                string runsRoot = Path.Combine(Root, "runs");

                var settings = new List<string>(FilmWindowTests.SmallRun(runsRoot, "remains", seconds: 60, checkpointEvery: 20))
                {
                    "EVOSIM_CONTACT_EATING=1", "EVOSIM_REMAINS_WINDOW=12",
                    "EVOSIM_AGE_BASE=15", "EVOSIM_AGE_DOUBLING=0.2",
                };

                int exit = Program.Main(settings.ToArray());
                if (exit != 0) throw new InvalidOperationException("The recording exited " + exit + ".");

                RunDirectory = Directory.GetDirectories(Path.Combine(runsRoot, "remains"))[0];
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root)) Directory.Delete(Root, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private FilmWindow.Result Film(double from, double to, string name, out string outDirectory)
        {
            outDirectory = Path.Combine(_run.Root, "windows", name + "-" + Guid.NewGuid().ToString("N"));

            return FilmWindow.Film(
                new FilmWindow.Options
                {
                    Run = _run.RunDirectory,
                    FromSeconds = from,
                    ToSeconds = to,
                    Out = outDirectory,
                    FramesPerSecond = 10d,
                    Threads = 2,
                },
                setting => null,
                TextWriter.Null);
        }

        /// <summary>
        /// A window over a world with remains carries every remains body: its first row, a genome for each the
        /// window's own file has none for, the second its window began, and the run's own rows counted apart from the
        /// bites. It stays faithful, because a row is read after the step and nothing the world steps on reads it.
        /// </summary>
        [Fact]
        public void AWindowOverAWorldWithRemainsCarriesTheirGenomesRowsAndSinceAndCountsThemApartFromBites()
        {
            FilmWindow.Result result = Film(35d, 45d, "remains", out string outDirectory);

            Assert.Equal(FilmWindow.Faithful, result.Verdict);
            _out.WriteLine(
                "remains " + result.Remains + ", sheds " + result.RemainsSheds + ", genomes " + result.RemainsGenomes +
                ", founded " + result.RemainsFounded + ", closed " + result.RemainsClosed + ", bites " + result.Bites +
                ", births " + result.Births + ", deaths " + result.Deaths + ", bodies " + result.Bodies);
            Assert.True(result.Remains > 0, "no remains in the window");
            Assert.True(File.Exists(Path.Combine(outDirectory, FilmWindow.RemainsFileName)));

            using (FilmWindowReader window = FilmWindowReader.Open(outDirectory))
            {
                Assert.Empty(window.Notes);
                Assert.Equal(result.Remains, window.RemainsCount);
                Assert.Equal(result.Remains, window.Verdict.Remains);
                Assert.Equal(result.RemainsSheds, window.Verdict.RemainsSheds);
                Assert.Equal(result.RemainsGenomes, window.Verdict.RemainsGenomes);

                int start = 0, died = 0, genomes = 0;

                foreach (FilmWindowRemains remains in window.Remains)
                {
                    Assert.True(window.TryRemainsGenome(remains.Id, out _), "remains " + remains.Id + " has no genome row");
                    Assert.False(double.IsNaN(remains.Since));
                    Assert.True(remains.Since <= remains.Maps[0].Seconds + 1e-6, "remains " + remains.Id + " began after it was first seen");
                    Assert.True(remains.ScaledBy > 0f && remains.ScaledBy <= 1f);

                    // The window began at 35 s: its remains standing then were dead before it, and are not in its own
                    // genomes file. A body that died inside it is, and its remains began with its death row.
                    if (remains.Start)
                    {
                        start++;
                        Assert.False(window.TryGenome(remains.Id, out _), "remains " + remains.Id + " standing at the start is in the genomes file");
                    }
                    else if (remains.From < 0)
                    {
                        Assert.True(window.TryGenome(remains.Id, out _), "remains " + remains.Id + " died inside the window and has no genome row");
                        Assert.True(window.TryDeathOf(remains.Id, out FilmWindowEvent death), "remains " + remains.Id + " has no death row");
                        Assert.Equal(death.Seconds, remains.Since, 6);
                        died++;
                    }

                    if (!window.TryGenome(remains.Id, out _)) genomes++;
                }

                Assert.True(start > 0, "no remains stood at the start");
                Assert.True(died > 0, "no body died inside the window");
                Assert.Equal(result.RemainsGenomes, genomes);

                // The run's own r and rc rows are counted apart from bites, and the reader keeps them apart.
                int bites = 0, founded = 0, closed = 0;

                foreach (FilmWindowEvent e in window.Events)
                {
                    if (e.Type == "k") bites++;
                    else if (e.Type == "r") founded++;
                    else if (e.Type == "rc") closed++;
                }

                Assert.Equal(result.Bites, bites);
                Assert.Equal(result.RemainsFounded, founded);
                Assert.Equal(result.RemainsClosed, closed);
                Assert.Equal(founded, window.Verdict.RemainsFounded);
                Assert.Equal(closed, window.Verdict.RemainsClosed);
                Assert.True(closed > 0, "no remains closed inside the window, so the rc tally is not exercised");
            }
        }

        /// <summary>
        /// Every pose a window holds with the remains bit set is a body the reader has a remains row for, and no
        /// body without the bit has one; and the genomes file is the file the farm wrote before the rows existed: it
        /// holds nobody whose only row is a remains row.
        /// </summary>
        [Fact]
        public void EveryPoseFlaggedAsRemainsHasARemainsRowAndTheGenomesFileHoldsOnlyTheBodiesItAlwaysDid()
        {
            Film(35d, 45d, "flags", out string outDirectory);

            using (FilmWindowReader window = FilmWindowReader.Open(outDirectory))
            {
                var flagged = new HashSet<long>();
                var unflagged = new HashSet<long>();

                for (int f = 0; f < window.FrameCount; f++)
                {
                    foreach (PoseBody body in window.ReadFrame(f).Bodies)
                    {
                        if ((body.Flags & PoseStream.RemainsBit) != 0) flagged.Add(body.Id);
                        else unflagged.Add(body.Id);
                    }
                }

                Assert.NotEmpty(flagged);

                foreach (long id in flagged)
                {
                    Assert.True(window.TryRemains(id, out _), "body " + id + " is posed as remains and has no remains row");
                }

                // A body is flagged from its death on and never before.
                foreach (long id in unflagged)
                {
                    if (window.TryRemains(id, out FilmWindowRemains remains) && remains.Start) Assert.Fail("body " + id + " stood as remains at the start and is posed as living");
                }

                // The genomes file holds the bodies alive at the start and those born inside: a remains row's body
                // that is in it was one of them, and the rest are in the remains file.
                foreach (FilmWindowRemains remains in window.Remains)
                {
                    bool inGenomes = window.TryGenome(remains.Id, out _);
                    Assert.Equal(!remains.Start && remains.From < 0, inGenomes);
                }
            }
        }

        /// <summary>
        /// A window written before the remains rows existed has no remains file and no remains fields in its verdict;
        /// the reader opens it as a window with no remains and no note, and the same window with its remains file
        /// taken away still reads, as a reader that did not know the file would see it.
        /// </summary>
        [Fact]
        public void AWindowWithoutTheRemainsFileStillReadsAsAWindowWithNoRemains()
        {
            Film(35d, 40d, "old", out string outDirectory);

            string file = Path.Combine(outDirectory, FilmWindow.RemainsFileName);
            Assert.True(File.Exists(file));
            File.Delete(file);

            using (FilmWindowReader window = FilmWindowReader.Open(outDirectory))
            {
                Assert.Empty(window.Notes);
                Assert.Equal(0, window.RemainsCount);
                Assert.False(window.TryRemains(1, out _));
                Assert.True(window.FrameCount > 0);
                Assert.True(window.GenomeCount > 0);
                Assert.Equal("FAITHFUL", window.ProvenanceWord);
            }
        }

        /// <summary>
        /// The rows the recorder writes for a dead body, a limb cut off it and the shed it suffers read back through
        /// the reader: the map of each in time order, the genome rows, since and the founder.
        /// </summary>
        [Fact]
        public void TheRowsTheRecorderWritesForALimbAndAShedReadBackThroughTheReader()
        {
            Film(35d, 40d, "roundtrip", out string outDirectory);
            string file = Path.Combine(outDirectory, FilmWindow.RemainsFileName);
            File.Delete(file);

            long victimId, limbId;
            double since;
            string victimGenome, limbGenome;

            using (RemainsFixtures.Pair p = RemainsFixtures.StarvedArena(RemainsFixtures.Config(true), RemainsFixtures.Fork(4), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                Organism claw = p.World.Living[1];
                RemainsFixtures.Starve(p);

                using (var writer = new JsonlGzWriter(file, memberEachRow: false))
                {
                    var recorder = new FilmWindow.RemainsRecorder(writer.Write);
                    recorder.Capture(p.World.Remains, 36d, atStart: true, id => false, id => false);

                    Bite(p, claw.Id, victim.Id, 1);
                    recorder.Capture(p.World.Remains, 37d, atStart: false, id => false, id => false);
                    writer.EndMember();
                }

                Organism limb = p.World.Remains.First(r => r.Id != victim.Id);
                victimId = victim.Id;
                limbId = limb.Id;
                since = victim.RemainsSince;
                victimGenome = GenomeJson.Write(victim.Genome, indent: false, id: victim.Id);
                limbGenome = GenomeJson.Write(victim.Genome, indent: false, id: limb.Id);
            }

            using (FilmWindowReader window = FilmWindowReader.Open(outDirectory))
            {
                Assert.Empty(window.Notes);
                Assert.Equal(2, window.RemainsCount);

                Assert.True(window.TryRemains(victimId, out FilmWindowRemains body));
                Assert.True(body.Start);
                Assert.Equal(-1L, body.From);
                Assert.Equal(since, body.Since, 9);
                Assert.Equal(2, body.Maps.Count);
                Assert.Equal(36d, body.Maps[0].Seconds, 9);
                Assert.Null(body.Maps[0].Map);
                Assert.Equal(37d, body.Maps[1].Seconds, 9);
                Assert.Equal(new[] { 0, 5, 6, 7, 8 }, body.Maps[1].Map);

                Assert.True(window.TryRemains(limbId, out FilmWindowRemains limb));
                Assert.False(limb.Start);
                Assert.Equal(victimId, limb.From);
                Assert.Equal(since, limb.Since, 9);
                Assert.Single(limb.Maps);
                Assert.Equal(new[] { 2, 3, 4 }, limb.Maps[0].Map);

                Assert.True(window.TryRemainsGenome(victimId, out string row));
                Assert.Equal(victimGenome, row);
                Assert.True(window.TryRemainsGenome(limbId, out row));
                Assert.Equal(limbGenome, row);

                // The genomes file the window always had answers TryGenome alone: neither body is in it.
                Assert.False(window.TryGenome(limbId, out _));
            }
        }

        /// <summary>The reader keys an event on its whole kind: r and rc are two kinds, and r keeps its founder.</summary>
        [Fact]
        public void TheReaderKeysAnEventOnItsWholeKindAndKeepsTheFounderOfARemains()
        {
            Film(35d, 40d, "events", out string outDirectory);

            string events = Path.Combine(outDirectory, FilmWindow.EventsFileName);
            File.AppendAllText(
                events,
                "{\"e\":\"r\",\"t\":36.5,\"id\":900001,\"from\":4242}\n" +
                "{\"e\":\"rc\",\"t\":37,\"id\":900001,\"j\":1.5}\n");

            using (FilmWindowReader window = FilmWindowReader.Open(outDirectory))
            {
                FilmWindowEvent founded = window.Events.Single(e => e.Id == 900001 && e.Type == "r");
                FilmWindowEvent closed = window.Events.Single(e => e.Id == 900001 && e.Type == "rc");

                Assert.Equal(4242L, founded.From);
                Assert.Equal('r', founded.Kind);
                Assert.Equal('c', closed.Kind);
                Assert.NotEqual(founded.Kind, closed.Kind);
                Assert.Equal(-1L, closed.From);

                // Neither is a birth, a death or a bite to any reader that asks for one.
                Assert.False(window.TryBirthOf(900001, out _));
                Assert.False(window.TryDeathOf(900001, out _));
                Assert.Equal(0, window.ChildrenOf(4242, 40d));
            }
        }
    }
}
