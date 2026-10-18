using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D159's migrating group through the farm's own entry (<see cref="Program.Main"/>,
    /// <c>--group</c>), on the CPU engine: a group with the exchange off is each world alone; a
    /// migrating group replays itself; its immigrants arrive in the ring's order and are recorded;
    /// a film window across an exchange is faithful; and the settings a lone run or an uneven group
    /// would get wrong are refused.
    /// </summary>
    /// <remarks>
    /// The recordings are made once for the class (<see cref="Recorded"/>): the small world of
    /// <see cref="FilmWindowTests.SmallRun"/> for forty seconds, three worlds trading two genomes
    /// every ten, twice; two worlds with the exchange off; and the first of those alone.
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public class MigratingGroupTests : IClassFixture<MigratingGroupTests.Recorded>
    {
        private const int Seconds = 40;
        private const int Every = 10;
        private const int Count = 2;

        private readonly Recorded _runs;

        public MigratingGroupTests(Recorded runs)
        {
            _runs = runs;
        }

        public sealed class Recorded : IDisposable
        {
            public string Root { get; }

            /// <summary>The migrating group, twice: each world's run directory, in the group's order.</summary>
            public string[] First { get; }
            public string[] Second { get; }

            /// <summary>Two worlds with the exchange off, and the first of them alone.</summary>
            public string[] Unjoined { get; }
            public string Alone { get; }

            public Recorded()
            {
                Root = Path.Combine(AppContext.BaseDirectory, "group-tests", Guid.NewGuid().ToString("N"));

                First = Group("first", migrate: true, seeds: new ulong[] { 1, 2, 3 });
                Second = Group("second", migrate: true, seeds: new ulong[] { 1, 2, 3 });
                Unjoined = Group("unjoined", migrate: false, seeds: new ulong[] { 1, 2 });

                string runs = Path.Combine(Root, "alone");
                var settings = new List<string>(FilmWindowTests.SmallRun(runs, "w0", Seconds, checkpointEvery: 15))
                {
                    "EVOSIM_SEED=1",
                };

                int exit = Program.Main(settings.ToArray());
                if (exit != 0) throw new InvalidOperationException("The lone run exited " + exit + ".");
                Alone = Directory.GetDirectories(Path.Combine(runs, "w0"))[0];
            }

            private string[] Group(string name, bool migrate, ulong[] seeds)
            {
                string runs = Path.Combine(Root, name);
                Directory.CreateDirectory(runs);

                var files = new List<string>();
                for (int k = 0; k < seeds.Length; k++)
                {
                    var lines = new List<string>(FilmWindowTests.SmallRun(runs, "w" + k, Seconds, checkpointEvery: 15))
                    {
                        "EVOSIM_SEED=" + seeds[k],
                    };

                    if (migrate)
                    {
                        lines.Add("EVOSIM_MIGRATE_EVERY=" + Every);
                        lines.Add("EVOSIM_MIGRATE_COUNT=" + Count);
                    }

                    string file = Path.Combine(runs, "w" + k + ".args");
                    File.WriteAllLines(file, lines);
                    files.Add(file);
                }

                int exit = Program.Main(new[] { "--group" }.Concat(files).ToArray());
                if (exit != 0)
                {
                    string errs = string.Join(" | ", files.Select(f => File.ReadAllText(Path.ChangeExtension(f, ".err"))));
                    throw new InvalidOperationException("The group " + name + " exited " + exit + ": " + errs);
                }

                return Enumerable.Range(0, seeds.Length)
                    .Select(k => Directory.GetDirectories(Path.Combine(runs, "w" + k))[0])
                    .ToArray();
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

        private static string[] Lineage(string run) =>
            File.ReadAllLines(Path.Combine(run, "lineage.jsonl")).Where(l => l.Length > 0).ToArray();

        /// <summary>The fields of a stats row that are the world and not the machine.</summary>
        private static List<string> WorldRows(string run)
        {
            var rows = new List<string>();
            foreach (string line in File.ReadAllLines(Path.Combine(run, "stats.jsonl")))
            {
                if (line.Length == 0) continue;
                JsonNode row = Json.Parse(line);
                rows.Add(string.Join(",",
                    new[] { "t", "alive", "births", "deaths", "auditResidual", "meanHeight" }
                        .Select(f => row[f].AsDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
            }

            return rows;
        }

        /// <summary>A markdown row's cells, trimmed, without the empty ends.</summary>
        private static string[] Cells(string row) =>
            row.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();

        private static JsonNode RunJson(string run) => Json.Parse(File.ReadAllText(Path.Combine(run, "run.json")));

        [Fact]
        public void AGroupWithTheExchangeOffIsEachWorldAlone()
        {
            Assert.Equal(Lineage(_runs.Alone), Lineage(_runs.Unjoined[0]));
            Assert.Equal(WorldRows(_runs.Alone), WorldRows(_runs.Unjoined[0]));

            Assert.False(File.Exists(Path.Combine(_runs.Unjoined[0], ImmigrantRecord.FileName)));
            Assert.False(RunJson(_runs.Unjoined[0]).Has("migrateEverySeconds"));
            Assert.DoesNotContain("\"immigrated\"", File.ReadAllText(Path.Combine(_runs.Unjoined[0], "stats.jsonl")));
        }

        [Fact]
        public void AMigratingGroupReplaysItself()
        {
            for (int k = 0; k < 3; k++)
            {
                Assert.Equal(Lineage(_runs.First[k]), Lineage(_runs.Second[k]));
                Assert.Equal(WorldRows(_runs.First[k]), WorldRows(_runs.Second[k]));
                Assert.Equal(
                    File.ReadAllLines(Path.Combine(_runs.First[k], ImmigrantRecord.FileName)),
                    File.ReadAllLines(Path.Combine(_runs.Second[k], ImmigrantRecord.FileName)));
            }
        }

        /// <summary>
        /// Every world is offered two genomes at each of the four exchanges, from the world before
        /// it in the ring, each a creature that world had at the time; each planted one has its
        /// lineage row; and the migrating world parts from the same seed alone at the first.
        /// </summary>
        /// <summary>
        /// A planted offer's row carries its body's guild, and it is the flags that body has in the
        /// first positions sample after its planting, by the same tests; a body gone before any
        /// sample is not compared.
        /// </summary>
        [Fact]
        public void APlantedOffersGuildIsItsBodysFlagsInPositions()
        {
            int compared = 0;
            for (int k = 0; k < 3; k++)
            {
                Dictionary<double, Dictionary<long, int>> flags = FilmWindowTests.PositionFlags(_runs.First[k]);
                double[] seconds = flags.Keys.OrderBy(t => t).ToArray();

                foreach (string line in File.ReadAllLines(Path.Combine(_runs.First[k], ImmigrantRecord.FileName)))
                {
                    if (line.Length == 0) continue;
                    JsonNode o = Json.Parse(line);
                    long id = (long)o["id"].AsDouble();
                    if (id < 0) continue;

                    int guild = o["guild"].AsInt();
                    Assert.InRange(guild, 0, PositionsRow.AllBits);

                    double t = o["t"].AsDouble();
                    foreach (double at in seconds)
                    {
                        if (at + 1e-9 < t || !flags[at].TryGetValue(id, out int seen)) continue;
                        Assert.True(seen == guild, $"world {k}: creature {id}, planted at {t} s with guild {guild}, has flags {seen} at {at} s");
                        compared++;
                        break;
                    }
                }
            }

            Assert.True(compared > 0, "no planted body was seen in a positions sample, so nothing was compared");
        }

        [Fact]
        public void EachWorldIsSentTwoGenomesAtEveryExchangeFromTheWorldBeforeIt()
        {
            for (int k = 0; k < 3; k++)
            {
                int from = (k + 2) % 3;
                string[] offers = File.ReadAllLines(Path.Combine(_runs.First[k], ImmigrantRecord.FileName))
                    .Where(l => l.Length > 0).ToArray();

                Assert.Equal((Seconds / Every) * Count, offers.Length);

                var bornAtHome = new Dictionary<long, double>();
                foreach (string line in Lineage(_runs.First[from]))
                {
                    JsonNode r = Json.Parse(line);
                    if (r["e"].AsString() == "b") bornAtHome[(long)r["id"].AsDouble()] = r["t"].AsDouble();
                }

                var planted = new HashSet<long>();
                foreach (string line in Lineage(_runs.First[k]))
                {
                    JsonNode r = Json.Parse(line);
                    if (r["e"].AsString() != "b" || r["k"].AsString() != "m") continue;
                    Assert.Equal(from, r["from"].AsInt());
                    planted.Add((long)r["id"].AsDouble());
                }

                for (int i = 0; i < offers.Length; i++)
                {
                    JsonNode o = Json.Parse(offers[i]);
                    double t = o["t"].AsDouble();
                    Assert.Equal(Every * (i / Count + 1), t, 6);
                    Assert.Equal(from, o["from"].AsInt());

                    long fid = (long)o["fid"].AsDouble();
                    Assert.True(bornAtHome.TryGetValue(fid, out double born) && born <= t,
                        "offer " + i + " names creature " + fid + ", not one of world " + from + "'s before " + t + " s");

                    long id = (long)o["id"].AsDouble();
                    if (id >= 0)
                    {
                        Assert.Contains(id, planted);
                        Assert.False(o.Has("why"), "a planted offer carries a refusal's reason");
                    }
                    else
                    {
                        Assert.Contains(o["why"].AsString(), new[] { "floor", "room", "stillborn" });
                        Assert.False(o.Has("guild"), "a refused offer carries a body's guild");
                    }
                }

                Assert.True(planted.Count > 0, "world " + k + " planted none of its offers");

                JsonNode manifest = RunJson(_runs.First[k]);
                Assert.Equal(Every, manifest["migrateEverySeconds"].AsDouble());
                Assert.Equal(Count, manifest["migrateCount"].AsInt());
                Assert.Equal(k, manifest["groupPlace"].AsInt());
                Assert.Equal("budget", manifest["reason"].AsString());

                Assert.Contains("\"immigrated\"", File.ReadAllText(Path.Combine(_runs.First[k], "stats.jsonl")));

                // The report says the same: the exchange at the settings line's end, and every row's
                // running totals are the stats row's of the same second.
                string[] report = File.ReadAllLines(
                    Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_runs.First[k])), "w" + k + ".md"));
                Assert.EndsWith(" · exchange every " + Every.ToString(CultureInfo.InvariantCulture) + " s x " + Count,
                    report.First(l => l.Contains("configHash")));

                string[] head = Cells(report.First(l => l.StartsWith("| t (s)")));
                var stats = File.ReadAllLines(Path.Combine(_runs.First[k], "stats.jsonl"))
                    .Where(l => l.Length > 0).Select(l => Json.Parse(l))
                    .ToDictionary(r => r["t"].AsDouble().ToString(CultureInfo.InvariantCulture));
                int rows = 0;
                foreach (string line in report.Where(l => l.Length > 2 && l.StartsWith("| ") && char.IsDigit(l[2])))
                {
                    string[] cells = Cells(line);
                    JsonNode s = stats[cells[0]];
                    Assert.Equal("**" + (long)s["immigrated"].AsDouble() + "**", cells[Array.IndexOf(head, "**imm**")]);
                    Assert.Equal(((long)s["immigrantsRefused"].AsDouble()).ToString(CultureInfo.InvariantCulture),
                        cells[Array.IndexOf(head, "imm refused")]);
                    rows++;
                }

                Assert.True(rows > 10, "the report of world " + k + " has " + rows + " rows");
            }

            Assert.NotEqual(Lineage(_runs.Alone), Lineage(_runs.First[0]));

            string[] unjoined = File.ReadAllLines(
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_runs.Unjoined[0])), "w0.md"));
            Assert.DoesNotContain(unjoined, l => l.Contains("exchange every") || l.Contains("**imm**"));
        }

        /// <summary>
        /// A window from the checkpoint at 15 s to 32 s crosses the exchanges at 20 and 30 s; it
        /// plants the world's recorded offers and is faithful to the run.
        /// </summary>
        [Fact]
        public void AFilmWindowAcrossAnExchangeIsFaithful()
        {
            string outDirectory = Path.Combine(_runs.Root, "windows", Guid.NewGuid().ToString("N"));

            FilmWindow.Result result = FilmWindow.Film(
                new FilmWindow.Options
                {
                    Run = _runs.First[1],
                    FromSeconds = 18,
                    ToSeconds = 32,
                    Out = outDirectory,
                    FramesPerSecond = 2,
                    Threads = 2,
                },
                name => null,
                TextWriter.Null);

            Assert.Equal(15d, result.RestoredSeconds, 6);
            Assert.True(FilmWindow.Faithful == result.Verdict, result.Verdict + ": " + result.Reason);

            // The window had something to plant: offers inside it that the run planted.
            int plantedInside = File.ReadAllLines(Path.Combine(_runs.First[1], ImmigrantRecord.FileName))
                .Where(l => l.Length > 0)
                .Select(l => Json.Parse(l))
                .Count(o => o["t"].AsDouble() > 15d && o["t"].AsDouble() <= 32d && o["id"].AsDouble() >= 0);
            Assert.True(plantedInside > 0, "no offer between 15 and 32 s was planted, so the window crossed nothing");
        }

        /// <summary>
        /// The control: the same window over a copy of the run whose record of offers is empty
        /// plants nothing and parts from the run, so the faithful window above owes its verdict to
        /// the replay.
        /// </summary>
        [Fact]
        public void AWindowWithoutTheRecordOfOffersPartsFromTheRun()
        {
            string copy = Path.Combine(_runs.Root, "stripped", Path.GetFileName(_runs.First[1]));
            CopyDirectory(_runs.First[1], copy);
            File.WriteAllText(Path.Combine(copy, ImmigrantRecord.FileName), "");

            FilmWindow.Result result = FilmWindow.Film(
                new FilmWindow.Options
                {
                    Run = copy,
                    FromSeconds = 18,
                    ToSeconds = 32,
                    Out = Path.Combine(_runs.Root, "windows", Guid.NewGuid().ToString("N")),
                    FramesPerSecond = 2,
                    Threads = 2,
                },
                name => null,
                TextWriter.Null);

            Assert.NotEqual(FilmWindow.Faithful, result.Verdict);
        }

        private static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            foreach (string dir in Directory.GetDirectories(from)) CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)));
        }

        /// <summary>The settings files of a group resuming the first recording at the given seconds.</summary>
        private string[] ResumeFiles(string name, double[] at, bool migrate = true)
        {
            string runs = Path.Combine(_runs.Root, name);
            Directory.CreateDirectory(runs);

            var files = new List<string>();
            for (int k = 0; k < at.Length; k++)
            {
                var lines = new List<string>(FilmWindowTests.SmallRun(runs, "w" + k, Seconds, checkpointEvery: 15))
                {
                    "EVOSIM_RESUME=" + _runs.First[k],
                    "EVOSIM_RESUME_AT=" + at[k].ToString(System.Globalization.CultureInfo.InvariantCulture),
                };

                if (migrate)
                {
                    lines.Add("EVOSIM_MIGRATE_EVERY=" + Every);
                    lines.Add("EVOSIM_MIGRATE_COUNT=" + Count);
                }

                string file = Path.Combine(runs, "w" + k + ".args");
                File.WriteAllLines(file, lines);
                files.Add(file);
            }

            return files.ToArray();
        }

        private static IEnumerable<string> After(IEnumerable<string> rows, double seconds) =>
            rows.Where(l => l.Length > 0 && Json.Parse(l)["t"].AsDouble() > seconds + 1e-9);

        /// <summary>
        /// The group resumed from its checkpoints at 15 s, across the exchanges at 20, 30 and 40 s,
        /// is the group straight through: every world's lineage, stats and offers after 15 s.
        /// </summary>
        [Fact]
        public void AGroupResumedAcrossAnExchangeIsTheGroupStraightThrough()
        {
            string[] files = ResumeFiles("resumed", new[] { 15d, 15d, 15d });
            Assert.Equal(0, Program.Main(new[] { "--group" }.Concat(files).ToArray()));

            string runs = Path.Combine(_runs.Root, "resumed");
            for (int k = 0; k < 3; k++)
            {
                string resumed = Directory.GetDirectories(Path.Combine(runs, "w" + k))[0];

                Assert.Equal(After(Lineage(_runs.First[k]), 15d), After(Lineage(resumed), 15d));
                Assert.Equal(WorldRows(_runs.First[k]).Where(r => double.Parse(r.Split(',')[0], System.Globalization.CultureInfo.InvariantCulture) > 15d),
                    WorldRows(resumed).Where(r => double.Parse(r.Split(',')[0], System.Globalization.CultureInfo.InvariantCulture) > 15d));
                Assert.Equal(
                    After(File.ReadAllLines(Path.Combine(_runs.First[k], ImmigrantRecord.FileName)), 15d),
                    After(File.ReadAllLines(Path.Combine(resumed, ImmigrantRecord.FileName)), 15d));

                Assert.NotEmpty(After(File.ReadAllLines(Path.Combine(resumed, ImmigrantRecord.FileName)), 15d));
            }
        }

        /// <summary>
        /// The group resumed from its checkpoints at 30 s, a second with an exchange of its own, is
        /// the group straight through after it: the checkpoint holds that exchange's immigrants, and
        /// the resumed worlds' first exchange is the one at 40 s, so none is lost or made twice.
        /// </summary>
        [Fact]
        public void AGroupResumedAtAnExchangesSecondIsTheGroupStraightThrough()
        {
            string[] files = ResumeFiles("resumed-on-exchange", new[] { 30d, 30d, 30d });
            Assert.Equal(0, Program.Main(new[] { "--group" }.Concat(files).ToArray()));

            string runs = Path.Combine(_runs.Root, "resumed-on-exchange");
            for (int k = 0; k < 3; k++)
            {
                string resumed = Directory.GetDirectories(Path.Combine(runs, "w" + k))[0];

                Assert.Equal(After(Lineage(_runs.First[k]), 30d), After(Lineage(resumed), 30d));
                Assert.Equal(WorldRows(_runs.First[k]).Where(r => double.Parse(r.Split(',')[0], System.Globalization.CultureInfo.InvariantCulture) > 30d),
                    WorldRows(resumed).Where(r => double.Parse(r.Split(',')[0], System.Globalization.CultureInfo.InvariantCulture) > 30d));
                Assert.Equal(
                    After(File.ReadAllLines(Path.Combine(_runs.First[k], ImmigrantRecord.FileName)), 30d),
                    After(File.ReadAllLines(Path.Combine(resumed, ImmigrantRecord.FileName)), 30d));

                Assert.NotEmpty(After(File.ReadAllLines(Path.Combine(resumed, ImmigrantRecord.FileName)), 30d));
                Assert.NotEmpty(File.ReadAllLines(Path.Combine(_runs.First[k], ImmigrantRecord.FileName))
                    .Where(l => l.Length > 0 && Math.Abs(Json.Parse(l)["t"].AsDouble() - 30d) < 0.5d));
            }
        }

        [Fact]
        public void AGroupResumedAtDifferentSecondsIsRefused()
        {
            Assert.Equal(1, Program.Main(new[] { "--group" }.Concat(ResumeFiles("uneven-resume", new[] { 15d, 30d, 15d })).ToArray()));
        }

        /// <summary>A world of a migrating run resumed with the exchange off would be another world: refused.</summary>
        [Fact]
        public void AMigratingWorldResumedWithoutItsExchangeIsRefused()
        {
            string[] files = ResumeFiles("no-exchange", new[] { 15d, 15d, 15d }, migrate: false);
            Assert.Equal(1, Program.Main(new[] { "--group" }.Concat(files).ToArray()));
        }

        [Fact]
        public void ALoneRunThatAsksForAnExchangeIsRefused()
        {
            string runs = Path.Combine(_runs.Root, "refused-lone");
            var settings = new List<string>(FilmWindowTests.SmallRun(runs, "lone", 2, 0)) { "EVOSIM_MIGRATE_EVERY=1" };

            Assert.Equal(1, Program.Main(settings.ToArray()));
            Assert.False(Directory.Exists(Path.Combine(runs, "lone")));
        }

        [Fact]
        public void AGroupWhoseWorldsAskForDifferentExchangesIsRefused()
        {
            string runs = Path.Combine(_runs.Root, "refused-uneven");
            Directory.CreateDirectory(runs);

            string a = Path.Combine(runs, "a.args"), b = Path.Combine(runs, "b.args");
            File.WriteAllLines(a, FilmWindowTests.SmallRun(runs, "a", 2, 0).Concat(new[] { "EVOSIM_MIGRATE_EVERY=1" }));
            File.WriteAllLines(b, FilmWindowTests.SmallRun(runs, "b", 2, 0).Concat(new[] { "EVOSIM_MIGRATE_EVERY=2" }));

            Assert.Equal(1, Program.Main(new[] { "--group", a, b }));
            Assert.False(Directory.Exists(Path.Combine(runs, "a")));
        }
    }
}
