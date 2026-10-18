using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Evosim.Core;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// The genome resources after the server review: the config a run grew under and where it came from, ports in Core's order under any pool, a failed
    /// read that is not an absent file, a half-written last line, malformed rows, the tie between a top genome and a member, the names a plan may give,
    /// and each body shape. Synthetic run folders only, a few lines each; nothing swims.
    /// </summary>
    public class GenomeFixTwoTests : IDisposable
    {
        private readonly string _dir = Where.Fresh("genomefix");
        private readonly ITestOutputHelper _out;
        public GenomeFixTwoTests(ITestOutputHelper output) { _out = output; }
        public void Dispose() => Directory.Delete(_dir, true);

        private const string DefaultExam = "\"exam\":{\"name\":\"exam\",\"brains\":[{\"label\":\"own\",\"kind\":\"own\"}],\"genomes\":[{\"id\":\"g2.0\",\"body\":0},{\"id\":\"g2.5\",\"body\":1}],\"episodes\":2,\"salt\":\"1\",\"seeds\":[\"1\",\"2\"]}";

        private string NewFolder() => Path.Combine(_dir, "run" + Guid.NewGuid().ToString("N").Substring(0, 6));

        private string Rel(string path) => Path.GetRelativePath(Where.Root, path).Replace('\\', '/');

        /// <summary>A resolver over one run folder, listed in room.json as a plain folder name or, with a config, as an object entry.</summary>
        private ResourceResolver Resolver(string folder, string configRel = null)
        {
            string rel = Rel(folder);
            string entry = configRel == null ? "\"" + rel + "\"" : "{\"folder\":\"" + rel + "\",\"config\":\"" + configRel + "\"}";
            RoomSettings s = RoomSettings.Parse(Where.SettingsJson(entry));
            return new ResourceResolver(new RoomContext(Where.Root, s, Where.Root, Path.Combine(_dir, "none.jsonl")));
        }

        private static JsonElement Read(ResourceResolver r, string uri) => Js.Parse(Js.Serialize(r.Read(uri)));

        private static string[] Ids(JsonElement list) => list.GetProperty("genomes").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();

        private static string[] Notes(JsonElement list) => list.GetProperty("notes").EnumerateArray().Select(x => x.GetString()).ToArray();

        private static string RunJson(string episodeConfig = null, string exam = DefaultExam)
        {
            var parts = new List<string> { "\"status\":\"completed\"" };
            if (episodeConfig != null) parts.Add("\"config\":{\"episode\":" + episodeConfig + "}");
            if (exam != null) parts.Add(exam);
            return "{" + string.Join(",", parts) + "}";
        }

        private static string D(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        private static string GenRow(int gen, int body, int born, int episodes, double score) =>
            "{\"gen\":" + gen + ",\"top\":{\"score\":" + D(score) + ",\"episodes\":" + episodes + ",\"body\":" + body + ",\"born\":" + born + "}}";

        private static string ScoresRow(int gen, params (int body, int born, int episodes, double? value)[] ms) =>
            "{\"gen\":" + gen + ",\"reward\":\"Net\",\"members\":[" + string.Join(",", ms.Select(m =>
                "{\"body\":" + m.body + ",\"born\":" + m.born + ",\"episodes\":" + m.episodes + ",\"value\":" + (m.value.HasValue ? D(m.value.Value) : "null") + ",\"parts\":{}}")) + "]}";

        private (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) Made(Action<GenomeResourceTests.RunMaker> tweak = null)
        {
            string folder = NewFolder();
            var m = new GenomeResourceTests.RunMaker(folder);
            tweak?.Invoke(m);
            m.Make();
            return (Resolver(folder), Path.GetFileName(folder), m);
        }

        // ------------------------------------------------------------------ the tie between a top genome and a member

        [Fact]
        public void SeveralMembersSharingABodyAndBornAreTiedByTheFirstOfTheHighestValue()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made(mk => mk.Plan = false);
            string f = Path.Combine(_dir, run);
            File.WriteAllText(Path.Combine(f, "tank-generations.jsonl"),
                GenRow(0, 0, 0, 4, 5) + "\n" + GenRow(1, 0, 0, 8, 7) + "\n" + GenRow(2, 0, 1, 4, 7) + "\n");
            File.WriteAllText(Path.Combine(f, "tank-scores.jsonl"),
                // generation 0: three members of one body and one born, the highest value held by two of them: the first is the top the search kept
                ScoresRow(0, (0, 0, 4, 5.0), (0, 0, 4, 5.0), (0, 0, 4, 2.0)) + "\n" +
                // generation 1: an elite (born 0, eight episodes) ties a child (born 1); the elite comes first and is the generation row's top
                ScoresRow(1, (0, 0, 8, 7.0), (0, 1, 4, 7.0), (0, 1, 4, 1.0)) + "\n" +
                // generation 2: the same tie, but the generation row's top is the child: the first of the tied members is not the top, so nothing is tied
                ScoresRow(2, (0, 0, 8, 7.0), (0, 1, 4, 7.0), (0, 1, 4, 1.0)) + "\n");
            JsonElement list = Read(r, $"nursery://genomes/{run}");
            JsonElement[] gs = list.GetProperty("genomes").EnumerateArray().ToArray();
            Assert.Equal("g0.0", gs[0].GetProperty("id").GetString());
            Assert.Equal("g1.0", gs[1].GetProperty("id").GetString());
            Assert.Equal("best.g2", gs[2].GetProperty("id").GetString());
            Assert.Contains("shared by 2 members", gs[2].GetProperty("idNote").GetString());
            Assert.Contains("g2.0", gs[2].GetProperty("idNote").GetString());
        }

        [Theory]
        [InlineData("noscores", "has no row for generation 2")]
        [InlineData("notop", "has no top")]
        [InlineData("othergen", "is for generation 7")]
        [InlineData("novalue", "no member of generation 2's scores row has a finite value")]
        [InlineData("nomembers", "lists no member")]
        [InlineData("topmismatch", "does not match the generation row's top")]
        [InlineData("nogenrow", "has no row for generation 2")]
        [InlineData("othergenrow", "is for generation 5")]
        public void ATopThatCannotBeTiedSaysTheRealReason(string kind, string expected)
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made(mk => mk.Plan = false);
            string f = Path.Combine(_dir, run);
            string gens = GenRow(0, 0, 0, 4, 5) + "\n" + GenRow(1, 0, 1, 4, 7) + "\n" + GenRow(2, 0, 2, 4, 8) + "\n";
            string scores = ScoresRow(0, (0, 0, 4, 5.0), (0, 0, 4, 1.0)) + "\n" + ScoresRow(1, (0, 1, 4, 7.0), (0, 0, 4, 1.0)) + "\n" + ScoresRow(2, (0, 2, 4, 8.0), (0, 0, 4, 1.0)) + "\n";
            switch (kind)
            {
                case "noscores": scores = ScoresRow(0, (0, 0, 4, 5.0)) + "\n" + ScoresRow(1, (0, 1, 4, 7.0)) + "\n"; break;
                case "notop": gens = GenRow(0, 0, 0, 4, 5) + "\n" + GenRow(1, 0, 1, 4, 7) + "\n{\"gen\":2}\n"; break;
                case "othergen": scores = ScoresRow(0, (0, 0, 4, 5.0)) + "\n" + ScoresRow(1, (0, 1, 4, 7.0)) + "\n" + ScoresRow(7, (0, 2, 4, 8.0)) + "\n"; break;
                case "novalue": scores = ScoresRow(0, (0, 0, 4, 5.0)) + "\n" + ScoresRow(1, (0, 1, 4, 7.0)) + "\n" + ScoresRow(2, (0, 2, 4, null), (0, 2, 4, null)) + "\n"; break;
                case "nomembers": scores = ScoresRow(0, (0, 0, 4, 5.0)) + "\n" + ScoresRow(1, (0, 1, 4, 7.0)) + "\n" + ScoresRow(2) + "\n"; break;
                case "topmismatch": scores = ScoresRow(0, (0, 0, 4, 5.0)) + "\n" + ScoresRow(1, (0, 1, 4, 7.0)) + "\n" + ScoresRow(2, (0, 9, 4, 8.0), (0, 0, 4, 1.0)) + "\n"; break;
                case "nogenrow": gens = GenRow(0, 0, 0, 4, 5) + "\n" + GenRow(1, 0, 1, 4, 7) + "\n"; break;
                case "othergenrow": gens = GenRow(0, 0, 0, 4, 5) + "\n" + GenRow(1, 0, 1, 4, 7) + "\n" + GenRow(5, 0, 2, 4, 8) + "\n"; break;
            }
            File.WriteAllText(Path.Combine(f, "tank-generations.jsonl"), gens);
            File.WriteAllText(Path.Combine(f, "tank-scores.jsonl"), scores);
            JsonElement[] gs = Read(r, $"nursery://genomes/{run}").GetProperty("genomes").EnumerateArray().ToArray();
            Assert.Equal("best.g2", gs[2].GetProperty("id").GetString());
            string note = gs[2].GetProperty("idNote").GetString();
            Assert.Contains("not tied", note);
            Assert.Contains(expected, note);
        }

        // ------------------------------------------------------------------ the config a run grew under

        private static string ConfigText(Action<RunConfig> tweak)
        {
            var c = new RunConfig();
            tweak(c);
            return RunConfigJson.Write(c);
        }

        [Fact]
        public void TheConfigIsReadFromRunJsonAndNamedAsItsSourceAndTheirPortsComeInCoresOrder()
        {
            string text = ConfigText(c => { c.SenseChemical = true; c.SenseEnergy = true; c.SenseScent = true; });
            RunConfig cfg = RunConfigJson.Read(text);
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            File.WriteAllText(Path.Combine(_dir, run, "run.json"), RunJson(text));
            JsonElement g = Read(r, $"nursery://genome/{run}/g0.1");
            JsonElement c0 = g.GetProperty("config");
            Assert.Equal("run.json", c0.GetProperty("source").GetString());
            Assert.True(c0.GetProperty("loaded").GetBoolean());
            Assert.Equal(JsonValueKind.Null, c0.GetProperty("note").ValueKind);
            Assert.Contains("run.json", g.GetProperty("development").GetProperty("limits").GetString());
            Assert.Contains("run.json", g.GetProperty("brain").GetProperty("pool").GetString());
            Assert.Equal("run.json", Read(r, $"nursery://genomes/{run}").GetProperty("config").GetProperty("source").GetString());

            // every port of that pool, in Core's own order: part first, then the pool's order
            Phenotype body = Developer.Develop(m.G0, cfg.Development, null, cfg.Shapes);
            string[] core = CentralWiring.Ports(body, cfg.SensorPool()).Select(p => BrainIds.Hex(p.Id)).ToArray();
            JsonElement[] ports = g.GetProperty("brain").GetProperty("ports").EnumerateArray().ToArray();
            Assert.Equal(core, ports.Select(p => p.GetProperty("id").GetString()).ToArray());
            Assert.All(ports, p => Assert.True(p.GetProperty("inPool").GetBoolean()));
            Assert.Equal(cfg.SensorPool().Select(x => x.ToString()).ToArray(), g.GetProperty("brain").GetProperty("poolChannels").EnumerateArray().Select(x => x.GetString()).ToArray());
        }

        [Fact]
        public void ThePortsOfAPoolThatAddsAChannelComeInCoresOrderUnlinkedOnesToo()
        {
            // The pool adds the scent. The genome's links read a joint angle on part 1 and a chemical on part 2 (not in this pool) and, here, a scent on part 0.
            string text = ConfigText(c => c.SenseScent = true);
            RunConfig cfg = RunConfigJson.Read(text);
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made(mk =>
            {
                Phenotype b = Developer.Develop(mk.G0);
                mk.G0.Brain.Links.Add(new BrainLink { From = GenomeResourceTests.Port(b, 0, SensorChannel.Scent), To = GenomeResourceTests.Out(b, 1, BrainOutputKind.Bend), Weight = 0.2f });
            });
            File.WriteAllText(Path.Combine(_dir, run, "run.json"), RunJson(text));
            Phenotype body = Developer.Develop(m.G0, cfg.Development, null, cfg.Shapes);
            JsonElement[] ports = Read(r, $"nursery://genome/{run}/g0.1").GetProperty("brain").GetProperty("ports").EnumerateArray().ToArray();
            string[] ids = ports.Select(p => p.GetProperty("id").GetString()).ToArray();
            List<BrainPort> pool = CentralWiring.Ports(body, cfg.SensorPool());
            // Core's ports, unlinked ones included, are all there, in Core's order, with the one port outside the pool (a chemical on part 2) inside its own part, after that part's pool ports
            Assert.Equal(pool.Select(p => BrainIds.Hex(p.Id)).ToArray(), ids.Where(i => pool.Any(p => BrainIds.Hex(p.Id) == i)).ToArray());
            Assert.True(pool.Count(p => !ports.Any(q => q.GetProperty("id").GetString() == BrainIds.Hex(p.Id))) == 0);
            JsonElement scent0 = ports.Single(p => p.GetProperty("part").GetInt32() == 0 && p.GetProperty("channel").GetString() == "Scent");
            Assert.True(scent0.GetProperty("inPool").GetBoolean());
            JsonElement chem = ports.Single(p => p.GetProperty("channel").GetString() == "Chemical");
            Assert.False(chem.GetProperty("inPool").GetBoolean());
            Assert.Equal(2, chem.GetProperty("part").GetInt32());
            int[] partOrder = ports.Select(p => p.GetProperty("part").GetInt32()).ToArray();
            Assert.Equal(partOrder.OrderBy(x => x).ToArray(), partOrder);   // part first, always
            int lastPart2Pool = Array.FindLastIndex(ports, p => p.GetProperty("part").GetInt32() == 2 && p.GetProperty("inPool").GetBoolean());
            Assert.True(Array.IndexOf(ids, chem.GetProperty("id").GetString()) > lastPart2Pool);
        }

        [Fact]
        public void ANonPoolPortLinkedOnAnEarlierPartStaysInsideItsOwnPartNotAtTheEnd()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made(mk =>
            {
                Phenotype b = Developer.Develop(mk.G0);
                mk.G0.Brain.Links.Add(new BrainLink { From = GenomeResourceTests.Port(b, 0, SensorChannel.Scent), To = GenomeResourceTests.Out(b, 1, BrainOutputKind.Bend), Weight = 0.2f });
            });
            Phenotype body = Developer.Develop(m.G0);
            JsonElement[] ports = Read(r, $"nursery://genome/{run}/g0.1").GetProperty("brain").GetProperty("ports").EnumerateArray().ToArray();
            int scent = Array.FindIndex(ports, p => p.GetProperty("part").GetInt32() == 0 && p.GetProperty("channel").GetString() == "Scent");
            int firstPart1 = Array.FindIndex(ports, p => p.GetProperty("part").GetInt32() == 1);
            int lastPart0Pool = Array.FindLastIndex(ports, p => p.GetProperty("part").GetInt32() == 0 && p.GetProperty("inPool").GetBoolean());
            Assert.True(scent > lastPart0Pool, "after its part's pool ports");
            Assert.True(scent < firstPart1, "before the next part's ports");
        }

        [Fact]
        public void TheConfigARoomJsonEntryNamesIsReadWithCoresOwnLoaderAndNamedAsItsSource()
        {
            string text = ConfigText(c => { c.SenseChemical = true; c.SenseFlow = true; });
            RunConfig cfg = RunConfigJson.Read(text);
            string folder = NewFolder();
            var m = new GenomeResourceTests.RunMaker(folder) { Plan = false };
            m.Make();
            string cfgFile = Path.Combine(_dir, "config-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".json");
            File.WriteAllText(cfgFile, text);
            ResourceResolver r = Resolver(folder, Rel(cfgFile));
            string run = Path.GetFileName(folder);
            JsonElement g = Read(r, $"nursery://genome/{run}/g0.1");
            JsonElement c0 = g.GetProperty("config");
            Assert.Equal("room.json", c0.GetProperty("source").GetString());
            Assert.Equal(Rel(cfgFile), c0.GetProperty("file").GetString());   // the path as room.json gives it, never a full path
            Assert.True(c0.GetProperty("loaded").GetBoolean());
            Phenotype body = Developer.Develop(m.G0, cfg.Development, null, cfg.Shapes);
            Assert.Equal(CentralWiring.Ports(body, cfg.SensorPool()).Select(p => BrainIds.Hex(p.Id)).ToArray(),
                g.GetProperty("brain").GetProperty("ports").EnumerateArray().Select(p => p.GetProperty("id").GetString()).ToArray());
            Assert.DoesNotContain(Path.GetPathRoot(Where.Root).Replace("\\", "\\\\"), Js.Serialize(r.Read($"nursery://genome/{run}/g0.1")), StringComparison.OrdinalIgnoreCase);

            // The config file changes: the next read says so (the cache is keyed on the config file too).
            string version = Read(r, $"nursery://genomes/{run}").GetProperty("version").GetString();
            File.WriteAllText(cfgFile, ConfigText(c => { c.SenseChemical = true; c.SenseFlow = true; c.SenseContact = true; }));
            JsonElement again = Read(r, $"nursery://genomes/{run}");
            Assert.NotEqual(version, again.GetProperty("version").GetString());
            int pool = RunConfigJson.Read(File.ReadAllText(cfgFile)).SensorPool().Length;
            Assert.Equal(pool, Read(r, $"nursery://genome/{run}/g0.1").GetProperty("brain").GetProperty("poolChannels").GetArrayLength());
        }

        [Fact]
        public void ARunsOwnConfigWinsOverTheOneRoomJsonNames()
        {
            string fromRun = ConfigText(c => c.SenseChemical = true), fromRoom = ConfigText(c => c.SenseFlow = true);
            string folder = NewFolder();
            new GenomeResourceTests.RunMaker(folder).Make();
            File.WriteAllText(Path.Combine(folder, "run.json"), RunJson(fromRun));
            string cfgFile = Path.Combine(_dir, "room-config.json");
            File.WriteAllText(cfgFile, fromRoom);
            JsonElement c0 = Read(Resolver(folder, Rel(cfgFile)), $"nursery://genomes/{Path.GetFileName(folder)}").GetProperty("config");
            Assert.Equal("run.json", c0.GetProperty("source").GetString());
        }

        [Fact]
        public void WithNoConfigRecordedTheSourceIsUnknownAndTheNoteSaysItIsNotRecorded()
        {
            (ResourceResolver r, string run, _) = Made(mk => mk.Plan = false);   // no run.json at all
            const string note = "the run's config is not recorded; the body and ports shown are grown under Core's defaults, which may differ from what the run used";
            JsonElement g = Read(r, $"nursery://genome/{run}/g0.1");
            Assert.Equal("unknown", g.GetProperty("config").GetProperty("source").GetString());
            Assert.False(g.GetProperty("config").GetProperty("loaded").GetBoolean());
            Assert.Equal(note, g.GetProperty("config").GetProperty("note").GetString());
            Assert.Equal(note, g.GetProperty("development").GetProperty("note").GetString());
            Assert.Contains("not known", g.GetProperty("brain").GetProperty("pool").GetString());
            Assert.DoesNotContain("under Core's default limits and standard shapes, and the sensor pool", Js.Serialize(r.Read($"nursery://genome/{run}/g0.1")));   // the old wording stated a guess as fact

            // run.json present but with no episode config is the same
            File.WriteAllText(Path.Combine(_dir, run, "run.json"), RunJson(null));
            Assert.Equal(note, Read(r, $"nursery://genome/{run}/g0.1").GetProperty("config").GetProperty("note").GetString());
            Assert.Equal("unknown", Read(r, $"nursery://genomes/{run}").GetProperty("config").GetProperty("source").GetString());
        }

        [Fact]
        public void ARecordedConfigThisBuildRefusesIsSaidToBeRefusedWithItsReason()
        {
            (ResourceResolver r, string run, _) = Made(mk => mk.Plan = false);
            File.WriteAllText(Path.Combine(_dir, run, "run.json"), RunJson("{\"format\":99}"));
            JsonElement c0 = Read(r, $"nursery://genome/{run}/g0.1").GetProperty("config");
            Assert.Equal("run.json", c0.GetProperty("source").GetString());
            Assert.False(c0.GetProperty("loaded").GetBoolean());
            Assert.Contains("format 99", c0.GetProperty("refusal").GetString());
            string note = c0.GetProperty("note").GetString();
            Assert.Contains("recorded in run.json but this build refuses it", note);
            Assert.Contains("format 99", note);
            Assert.Contains("which may differ from what the run used", note);

            // the same for a room.json config: a file that is not there, and one this build refuses
            string folder = Path.Combine(_dir, run);
            JsonElement missing = Read(Resolver(folder, Rel(Path.Combine(_dir, "no-such-config.json"))), $"nursery://genomes/{run}").GetProperty("config");
            Assert.Equal("room.json", missing.GetProperty("source").GetString());   // both are refused; room.json's entry is the one named, with run.json's refusal beside it
            Assert.Contains("the file is not there", missing.GetProperty("refusal").GetString());
            Assert.Contains("run.json's own episode config is refused too", missing.GetProperty("refusal").GetString());
            File.Delete(Path.Combine(folder, "run.json"));
            JsonElement gone = Read(Resolver(folder, Rel(Path.Combine(_dir, "no-such-config.json"))), $"nursery://genomes/{run}").GetProperty("config");
            Assert.Equal("room.json", gone.GetProperty("source").GetString());
            Assert.Contains("the file is not there", gone.GetProperty("refusal").GetString());
            Assert.Contains("recorded in room.json", gone.GetProperty("note").GetString());
            string bad = Path.Combine(_dir, "bad-config.json");
            File.WriteAllText(bad, "{\"format\":3}");
            JsonElement refused = Read(Resolver(folder, Rel(bad)), $"nursery://genomes/{run}").GetProperty("config");
            Assert.Equal("room.json", refused.GetProperty("source").GetString());
            Assert.Contains("format 3", refused.GetProperty("refusal").GetString());
            Assert.Equal(Rel(bad), refused.GetProperty("file").GetString());
        }

        // ------------------------------------------------------------------ room.json's runs entries

        [Fact]
        public void ARunsEntryMayCarryAConfigAndTheShippedRoomJsonNamesThePilotsOwn()
        {
            RoomSettings s = RoomSettings.Parse(Where.SettingsJson("\"a/b\",{\"folder\":\"c/d\",\"config\":\"e\\\\f.json\"},{\"folder\":\"g\"}"));
            Assert.Equal(new[] { "a/b", "c/d", "g" }, s.Runs.ToArray());
            Assert.Equal("e/f.json", s.RunConfigs["c/d"]);
            Assert.False(s.RunConfigs.ContainsKey("a/b"));
            Assert.False(s.RunConfigs.ContainsKey("g"));
            RoomSettings shipped = RoomSettings.Load(Path.Combine(Where.Root, "rounds", "nursery", "room.json"));
            Assert.Equal("scratch/nursery/runs-jd/big-cur-deep-brain2/config.json", shipped.RunConfigs["scratch/nursery/pilot-deep-brain2"]);
        }

        [Theory]
        [InlineData("{\"folder\":\"a\",\"conf\":\"x.json\"}", "unknown conf")]
        [InlineData("{\"config\":\"x.json\"}", "names its folder")]
        [InlineData("{\"folder\":\"a\",\"config\":7}", "the config of the runs entry a")]
        [InlineData("{\"folder\":\"a\",\"config\":\"\"}", "the config of the runs entry a")]
        [InlineData("{\"folder\":\"a\",\"config\":\"D:/x.json\"}", "the config of the runs entry a")]
        [InlineData("{\"folder\":\"a\",\"config\":\"/x.json\"}", "the config of the runs entry a")]
        [InlineData("{\"folder\":\"a\",\"config\":\"../x.json\"}", "the config of the runs entry a")]
        [InlineData("{\"folder\":\"a\",\"config\":\"x/../../y.json\"}", "the config of the runs entry a")]
        [InlineData("{\"folder\":\"D:/a\",\"config\":\"x.json\"}", "folders are relative")]
        [InlineData("7", "runs holds folder names")]
        public void ARunsEntryThatIsNotWellFormedIsRefusedNamingTheFileAndTheKey(string entry, string expected)
        {
            var e = Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(Where.SettingsJson(entry), "my/room-settings.json"));
            Assert.Contains("my/room-settings.json", e.Message);
            Assert.Contains(expected, e.Message);
        }

        // ------------------------------------------------------------------ files a nursery is still writing, and files that cannot be read

        [Fact]
        public void AHalfWrittenLastLineOfTheTopsFileIsLeftOutAndSaidSoAndAWholeOneWithoutItsNewlineIsKept()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made(mk => mk.Plan = false);
            string best = Path.Combine(_dir, run, "tank-best.jsonl");
            File.WriteAllText(best, GenomeResourceTests.Row(m.G0) + "\n" + GenomeResourceTests.Row(m.G1) + "\n" + GenomeResourceTests.Row(m.G2).Substring(0, 40));
            JsonElement list = Read(r, $"nursery://genomes/{run}");
            Assert.Equal(new[] { "g0.1", "g1.0", "exam.0", "exam.1" }, Ids(list));
            Assert.Contains(Notes(list), n => n.Contains("last line of tank-best.jsonl does not parse"));
            File.WriteAllText(best, GenomeResourceTests.Row(m.G0) + "\n" + GenomeResourceTests.Row(m.G1) + "\n" + GenomeResourceTests.Row(m.G2));   // no newline, but whole
            list = Read(r, $"nursery://genomes/{run}");
            Assert.Equal(new[] { "g0.1", "g1.0", "g2.0", "exam.0", "exam.1" }, Ids(list));
            Assert.DoesNotContain(Notes(list), n => n.Contains("does not parse"));
        }

        [Fact]
        public void AHalfWrittenLastLineOfTheExamsGenomeFileIsLeftOutAndSaidSo()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            string tsv = Path.Combine(_dir, run, "tank-exam-genomes.tsv");
            File.WriteAllText(tsv, "0\t" + GenomeResourceTests.Row(m.G2) + "\n1\t" + GenomeResourceTests.Row(m.Exam2).Substring(0, 30));
            JsonElement list = Read(r, $"nursery://genomes/{run}");
            Assert.Equal(new[] { "g0.1", "g1.0", "g2.0" }, Ids(list));
            Assert.Contains(Notes(list), n => n.Contains("last line of tank-exam-genomes.tsv does not parse"));
            // a line cut before its tab is a half-written line too
            File.WriteAllText(tsv, "0\t" + GenomeResourceTests.Row(m.G2) + "\n1");
            list = Read(r, $"nursery://genomes/{run}");
            Assert.Equal(new[] { "g0.1", "g1.0", "g2.0" }, Ids(list));
            Assert.Contains(Notes(list), n => n.Contains("last line of tank-exam-genomes.tsv does not parse"));
        }

        [Fact]
        public void ARunsFilesReadWhileTheNurseryHoldsThemOpenForAppendingAreReadWhole()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            string f = Path.Combine(_dir, run);
            using (var w1 = new FileStream(Path.Combine(f, "tank-best.jsonl"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var w2 = new FileStream(Path.Combine(f, "tank-exam-genomes.tsv"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var w3 = new FileStream(Path.Combine(f, "tank-generations.jsonl"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                JsonElement list = Read(r, $"nursery://genomes/{run}");
                Assert.Equal(new[] { "g0.1", "g1.0", "g2.0", "g2.5" }, Ids(list));
                Assert.Empty(Notes(list));
            }
        }

        /// <summary>A fresh run whose one file is held with no sharing while the room first reads it, and read again once it is released.</summary>
        private (JsonElement held, JsonElement released) ReadWhileLocked(string file, Action<string> prepare = null)
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            string f = Path.Combine(_dir, run);
            prepare?.Invoke(f);
            JsonElement locked;
            using (new FileStream(Path.Combine(f, file), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                locked = Read(r, $"nursery://genomes/{run}");
                Assert.EndsWith("|unread", locked.GetProperty("version").GetString());
                Assert.DoesNotContain(Path.GetPathRoot(Where.Root).Replace("\\", "\\\\"), Js.Serialize(r.Read($"nursery://genomes/{run}")), StringComparison.OrdinalIgnoreCase);
            }
            return (locked, Read(r, $"nursery://genomes/{run}"));
        }

        [Fact]
        public void AFileThatCannotBeReadIsNotTheRunHavingNoneAndTheResultIsNeverKept()
        {
            if (!OperatingSystem.IsWindows()) { _out.WriteLine("skipped: an exclusive lock refuses a reader only on Windows"); return; }
            string[] full = { "g0.1", "g1.0", "g2.0", "g2.5" };

            // The tops file held: the read fails. That is said, and it is not "this run has no tank-best.jsonl". Released, the next read is the whole run again;
            // had the failure been kept as "absent", the tops would still be gone.
            (JsonElement held, JsonElement released) = ReadWhileLocked("tank-best.jsonl");
            Assert.Contains(Notes(held), n => n.Contains("tank-best.jsonl could not be read"));
            Assert.DoesNotContain(Notes(held), n => n.Contains("has no tank-best.jsonl"));
            Assert.Equal(new[] { "g2.0", "g2.5" }, Ids(held));
            Assert.Equal(full, Ids(released));
            Assert.Empty(Notes(released));
            Assert.DoesNotContain("|unread", released.GetProperty("version").GetString());

            // The generation rows held: the tops are there but untied, then tied again once released.
            (held, released) = ReadWhileLocked("tank-generations.jsonl");
            Assert.Equal("best.g1", held.GetProperty("genomes")[1].GetProperty("id").GetString());
            Assert.Contains("tank-generations.jsonl could not be read", held.GetProperty("genomes")[1].GetProperty("idNote").GetString());
            Assert.Equal("g1.0", released.GetProperty("genomes")[1].GetProperty("id").GetString());

            // The scores held: the same for the other half of the tie.
            (held, released) = ReadWhileLocked("tank-scores.jsonl");
            Assert.Contains("tank-scores.jsonl could not be read", held.GetProperty("genomes")[1].GetProperty("idNote").GetString());
            Assert.DoesNotContain(Notes(held), n => n.StartsWith("no tank-scores.jsonl"));
            Assert.Equal(full, Ids(released));

            // The exam's genome file held: the exam's genomes are not reported as absent.
            (held, released) = ReadWhileLocked("tank-exam-genomes.tsv");
            Assert.Contains(Notes(held), n => n.Contains("tank-exam-genomes.tsv could not be read"));
            Assert.DoesNotContain(Notes(held), n => n.Contains("run has no tank-exam-genomes.tsv"));
            Assert.Equal(new[] { "g0.1", "g1.0", "g2.0" }, Ids(held));
            Assert.Equal(full, Ids(released));

            // run.json held: the plan and the config are not read, and that is said; released, the config is the run's own.
            string text = ConfigText(c => c.SenseScent = true);
            (held, released) = ReadWhileLocked("run.json", f => File.WriteAllText(Path.Combine(f, "run.json"), RunJson(text)));
            Assert.Contains(Notes(held), n => n.Contains("run.json could not be read"));
            Assert.Equal("unknown", held.GetProperty("config").GetProperty("source").GetString());
            Assert.Equal("run.json", released.GetProperty("config").GetProperty("source").GetString());
            Assert.Equal(full, Ids(released));
        }

        [Fact]
        public void ALineageFileThatCannotBeReadIsSaidSoAndReadAgainNotTakenForAnAbsentOne()
        {
            if (!OperatingSystem.IsWindows()) { _out.WriteLine("skipped: an exclusive lock refuses a reader only on Windows"); return; }
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            string lin = Path.Combine(_dir, run, TankLog.LineageFile);
            using (new FileStream(lin, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                JsonElement g = Read(r, $"nursery://genome/{run}/g1.0");
                JsonElement l = g.GetProperty("lineage");
                Assert.False(l.GetProperty("predatesLineage").GetBoolean());
                Assert.Contains("could not be read", l.GetProperty("note").GetString());
                Assert.Equal("unreadable", Read(r, $"nursery://genomes/{run}").GetProperty("lineageState").GetString());
                Assert.False(Read(r, $"nursery://genomes/{run}").GetProperty("predatesLineage").GetBoolean());
            }
            JsonElement ok = Read(r, $"nursery://genome/{run}/g1.0");
            Assert.True(ok.GetProperty("lineage").GetProperty("present").GetBoolean());
            Assert.Equal("g1.0", ok.GetProperty("lineage").GetProperty("row").GetProperty("id").GetString());
        }

        [Fact]
        public void TheSetIsReplacedWhenAFileChangesAndKeptWhenNoneDoes()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            JsonElement a = Read(r, $"nursery://genomes/{run}"), same = Read(r, $"nursery://genomes/{run}");
            Assert.Equal(a.GetProperty("version").GetString(), same.GetProperty("version").GetString());
            Assert.Equal(4, a.GetProperty("count").GetInt32());
            File.AppendAllText(Path.Combine(_dir, run, "tank-best.jsonl"), GenomeResourceTests.Row(m.G0) + "\n");   // the nursery wrote another generation's top
            JsonElement b = Read(r, $"nursery://genomes/{run}");
            Assert.NotEqual(a.GetProperty("version").GetString(), b.GetProperty("version").GetString());
            Assert.Equal(5, b.GetProperty("count").GetInt32());
            Assert.Contains("best.g3", Ids(b));
            // a genomes file under another name changes the version too: the signature is not a fixed list of file names
            string other = Path.Combine(_dir, run, "tank-warm-genomes.tsv");
            File.WriteAllText(other, "0\t" + GenomeResourceTests.Row(m.G0) + "\n");
            Assert.NotEqual(b.GetProperty("version").GetString(), Read(r, $"nursery://genomes/{run}").GetProperty("version").GetString());
        }

        // ------------------------------------------------------------------ rows that are not what they should be

        [Fact]
        public void AMalformedLineageRowIsLeftOutWithANoteAndNeverFailsTheRead()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            File.WriteAllText(Path.Combine(_dir, run, TankLog.LineageFile),
                TankLog.LineageRow(0, "g0.1", 0, 0, new string[0], false) + "\n" +
                "{\"gen\":1,\"id\":\"g1.0\",\"body\":0,\"born\":1,\"parents\":[7],\"elite\":false}\n" +       // a parent that is a number: GetString throws InvalidOperationException
                "{\"gen\":2,\"body\":0}\n" +                                                                  // no id
                "[1,2]\n" +                                                                                    // not an object
                "{\"gen\":2,\"id\":\"g2.0\",\"body\":0,\"born\":2,\"parents\":[\"g1.7\"],\"elite\":false}\n" +
                "{\"gen\":3,\"id\":\"g3.0\",\"par");                                                          // half written
            JsonElement g = Read(r, $"nursery://genome/{run}/g1.0");
            JsonElement lin = g.GetProperty("lineage");
            Assert.Equal(JsonValueKind.Null, lin.GetProperty("row").ValueKind);   // its row was the malformed one
            string[] problems = lin.GetProperty("problems").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Equal(4, problems.Length);
            Assert.Contains(problems, p => p.Contains("line 2") && p.Contains("left out"));
            Assert.Contains(problems, p => p.Contains("line 3"));
            Assert.Contains(problems, p => p.Contains("line 4"));
            Assert.Contains(problems, p => p.Contains("last line of tank-lineage.jsonl does not parse"));
            Assert.Equal("g1.7", Read(r, $"nursery://genome/{run}/g2.0").GetProperty("changes").GetProperty("parent").GetString());   // the good rows are read
        }

        [Fact]
        public void AGenomeLineThatIsNotJsonIsAStatedProblemNotAFailedRequest()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            File.WriteAllText(Path.Combine(_dir, run, "tank-best.jsonl"), GenomeResourceTests.Row(m.G0) + "\n{this is not json\n" + GenomeResourceTests.Row(m.G2) + "\n");
            JsonElement list = Read(r, $"nursery://genomes/{run}");
            Assert.Contains("is not JSON", list.GetProperty("genomes")[1].GetProperty("problem").GetString());
            JsonElement g = Read(r, $"nursery://genome/{run}/" + list.GetProperty("genomes")[1].GetProperty("id").GetString());
            Assert.Contains("does not load", g.GetProperty("problem").GetString());
            Assert.Equal(JsonValueKind.Null, g.GetProperty("bodyPlan").ValueKind);
        }

        [Fact]
        public void AGenomeThatDoesNotDevelopShowsCoresMessageWithEveryIssueItListsOnOneLine()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made(mk => mk.Plan = false);
            // Two links into ids that are no unit of the brain, and a pair of links repeated: Core's validation lists each of them.
            Genome bad = GenomeResourceTests.Synthetic();
            Phenotype bb = Developer.Develop(bad);
            bad.Brain.Links.Add(new BrainLink { From = GenomeResourceTests.Port(bb, 1, SensorChannel.Depth), To = 0xAAAA0000AAAA0001UL, Weight = 0.1f });
            bad.Brain.Links.Add(new BrainLink { From = GenomeResourceTests.Port(bb, 2, SensorChannel.Depth), To = 0xBBBB0000BBBB0002UL, Weight = 0.1f });
            bad.Brain.Links.Add(new BrainLink { From = GenomeResourceTests.Port(bb, 2, SensorChannel.Depth), To = 0xBBBB0000BBBB0002UL, Weight = 0.2f });
            File.WriteAllText(Path.Combine(_dir, run, "tank-best.jsonl"), GenomeResourceTests.Row(m.G0) + "\n" + GenomeResourceTests.Row(bad) + "\n");
            JsonElement g = Read(r, $"nursery://genome/{run}/best.g1");
            string problem = g.GetProperty("problem").GetString();
            _out.WriteLine(problem);
            Assert.DoesNotContain("\n", problem);
            Assert.DoesNotContain("\r", problem);
            Assert.Contains("; ", problem);
            Assert.Contains("cannot be developed", problem);
            IReadOnlyList<string> issues = bad.Validate();
            Assert.True(issues.Count >= 3, "Core lists every issue");
            foreach (string issue in issues) Assert.Contains(issue.Trim(), problem);
            Assert.Equal(JsonValueKind.Null, g.GetProperty("bodyPlan").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, g.GetProperty("graph").ValueKind);   // the genome's own graph is still read
        }

        [Fact]
        public void AHeldParentWhoseTextDoesNotParseIsAStatedProblemInTheChangeList()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            // g1.0's parent is g0.1, held as the first line of the tops file: make that line not JSON
            File.WriteAllText(Path.Combine(_dir, run, "tank-best.jsonl"), "{broken parent\n" + GenomeResourceTests.Row(m.G1) + "\n" + GenomeResourceTests.Row(m.G2) + "\n");
            JsonElement ch = Read(r, $"nursery://genome/{run}/g1.0").GetProperty("changes");
            Assert.Equal(JsonValueKind.Null, ch.GetProperty("against").ValueKind);
            Assert.Equal("g0.1", ch.GetProperty("parent").GetString());
            Assert.Contains("does not parse", ch.GetProperty("reason").GetString());
            Assert.Contains("g0.1", ch.GetProperty("reason").GetString());
        }

        [Fact]
        public void TheChangeListIsCappedAtFiveHundredAndSaysHowManyThereAre()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made(mk =>
            {
                Genome child = mk.G0.Clone();
                for (int i = 0; i < 620; i++) child.Brain.Hidden.Add(new BrainUnit { Id = 0x5000UL + (ulong)i, Bias = 0.1f });
                mk.G1 = child;
            });
            JsonElement ch = Read(r, $"nursery://genome/{run}/g1.0").GetProperty("changes");
            Assert.Equal("g0.1", ch.GetProperty("against").GetString());
            Assert.True(ch.GetProperty("truncated").GetBoolean());
            Assert.Equal(500, ch.GetProperty("changes").GetArrayLength());
            Assert.Equal(620, ch.GetProperty("count").GetInt32());
        }

        // ------------------------------------------------------------------ which id a genome goes by

        [Fact]
        public void WhenAPlansIdIsATiedTopsIdWithOtherTextTheRecordedIdWinsAndTheTopFallsBack()
        {
            // The plan says g2.0 is Exam2's text; the scores and generation rows tie the generation-2 top (G2's text) to g2.0 as well.
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            File.WriteAllText(Path.Combine(_dir, run, "tank-exam-genomes.tsv"), "0\t" + GenomeResourceTests.Row(m.Exam2) + "\n1\t" + GenomeResourceTests.Row(m.G0) + "\n");
            JsonElement list = Read(r, $"nursery://genomes/{run}");
            Assert.Equal(new[] { "g0.1", "g1.0", "best.g2", "g2.0", "g2.5" }, Ids(list));
            JsonElement[] gs = list.GetProperty("genomes").EnumerateArray().ToArray();
            Assert.Equal(JsonValueKind.Null, gs[2].GetProperty("memberId").ValueKind);
            Assert.Contains("run.json's exam plan names a different genome g2.0", gs[2].GetProperty("idNote").GetString());
            Assert.Equal("g2.0", gs[3].GetProperty("memberId").GetString());
            Assert.Equal("named by run.json's exam plan", gs[3].GetProperty("idNote").GetString());
            Assert.Contains(Notes(list), n => n.Contains("the plan's genome keeps the id"));
            // The id reads the plan's genome (Exam2's text); the top is reached by its generation's name.
            Assert.Equal(GenomeResourceTests.Row(m.Exam2), r.GenomeText(run, "g2.0").text);
            Assert.Equal(GenomeResourceTests.Row(m.G2), r.GenomeText(run, "best.g2").text);
            // and the top no longer claims a lineage row it was only tied to by the id
            Assert.Contains("do not tie this genome to a member id", Read(r, $"nursery://genome/{run}/best.g2").GetProperty("lineage").GetProperty("note").GetString());
        }

        [Fact]
        public void APlanWhoseCountDisagreesWithTheGenomesFileIsSaidSoInBothDirections()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            string tsv = Path.Combine(_dir, run, "tank-exam-genomes.tsv");
            // the plan names two, the file holds three: the third has no id from the plan
            File.WriteAllText(tsv, "0\t" + GenomeResourceTests.Row(m.G2) + "\n1\t" + GenomeResourceTests.Row(m.Exam2) + "\n1\t" + GenomeResourceTests.Row(m.G0) + "\n");
            JsonElement list = Read(r, $"nursery://genomes/{run}");
            Assert.Contains(Notes(list), n => n.Contains("names 2 genomes and tank-exam-genomes.tsv holds 3"));
            Assert.Equal(new[] { "g0.1", "g1.0", "g2.0", "g2.5", "exam.2" }, Ids(list));
            Assert.Contains("names no id for line 3", list.GetProperty("genomes")[4].GetProperty("idNote").GetString());
            // the plan names two, the file holds one
            File.WriteAllText(tsv, "0\t" + GenomeResourceTests.Row(m.G2) + "\n");
            list = Read(r, $"nursery://genomes/{run}");
            Assert.Contains(Notes(list), n => n.Contains("names 2 genomes and tank-exam-genomes.tsv holds 1"));
            Assert.Equal(new[] { "g0.1", "g1.0", "g2.0" }, Ids(list));
        }

        [Fact]
        public void ABenchRunNamesItsGenomesByThePlansPoolIdsAndByTheirLineInTheBenchFile()
        {
            string folder = NewFolder();
            Directory.CreateDirectory(folder);
            var m = new GenomeResourceTests.RunMaker(folder);
            string bench = "\"exam\":{\"name\":\"bench\",\"brains\":[{\"label\":\"own\",\"kind\":\"own\"}],\"genomes\":[{\"id\":\"p0\",\"body\":0},{\"id\":\"p1\",\"body\":1}],\"episodes\":2,\"salt\":\"1\",\"seeds\":[\"1\",\"2\"]}";
            File.WriteAllText(Path.Combine(folder, "run.json"), RunJson(null, bench));
            File.WriteAllText(Path.Combine(folder, "tank-bench-genomes.tsv"), "0\t" + GenomeResourceTests.Row(m.G0) + "\n1\t" + GenomeResourceTests.Row(m.G1) + "\n");
            ResourceResolver r = Resolver(folder);
            string run = Path.GetFileName(folder);
            JsonElement list = Read(r, $"nursery://genomes/{run}");
            Assert.Equal(new[] { "p0", "p1" }, Ids(list));
            JsonElement[] gs = list.GetProperty("genomes").EnumerateArray().ToArray();
            Assert.Equal("bench", gs[0].GetProperty("examName").GetString());
            Assert.Equal(new[] { "bench.0" }, gs[0].GetProperty("aliases").EnumerateArray().Select(x => x.GetString()).ToArray());
            Assert.Equal(new[] { "exam" }, gs[1].GetProperty("kinds").EnumerateArray().Select(x => x.GetString()).ToArray());
            Assert.Equal("p1", Read(r, $"nursery://genome/{run}/bench.1").GetProperty("id").GetString());
            Assert.Equal(GenomeResourceTests.Row(m.G1), r.GenomeText(run, "p1").text);
            Assert.Throws<ResourceNotFound>(() => r.Read($"nursery://genome/{run}/exam.0"));   // a bench's genomes do not answer to exam.<n>
            // no tops file: the note says the run has none (it could be read; it is not there)
            Assert.Contains(Notes(list), n => n.Contains("has no tank-best.jsonl"));
            string version = list.GetProperty("version").GetString();
            File.AppendAllText(Path.Combine(folder, "tank-bench-genomes.tsv"), "2\t" + GenomeResourceTests.Row(m.G2) + "\n");
            Assert.NotEqual(version, Read(r, $"nursery://genomes/{run}").GetProperty("version").GetString());   // the signature covers the bench file the plan names

            // a bench with no plan: the genomes are named by their line in the bench file
            string folder2 = NewFolder();
            Directory.CreateDirectory(folder2);
            File.WriteAllText(Path.Combine(folder2, "tank-bench-genomes.tsv"), "0\t" + GenomeResourceTests.Row(m.G0) + "\n");
            JsonElement bare = Read(Resolver(folder2), $"nursery://genomes/{Path.GetFileName(folder2)}");
            Assert.Equal(new[] { "bench.0" }, Ids(bare));
            Assert.Contains("this run wrote no exam plan", bare.GetProperty("genomes")[0].GetProperty("idNote").GetString());
        }

        [Fact]
        public void AnExamWhosePlanNamesItAnythingButAPlainNameReadsNoFileOutsideTheRun()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            File.WriteAllText(Path.Combine(_dir, run, "run.json"), RunJson(null, DefaultExam.Replace("\"name\":\"exam\"", "\"name\":\"../exam\"")));
            JsonElement list = Read(r, $"nursery://genomes/{run}");
            Assert.Equal(new[] { "g0.1", "g1.0", "g2.0" }, Ids(list));
            Assert.Contains(Notes(list), n => n.Contains("not a plain name"));
        }

        [Fact]
        public void AnIdThatHoldsASeparatorOrIsADotNameIsNeverUsedAndNoAddressReachesOutOfTheRun()
        {
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made();
            string exam = DefaultExam.Replace("{\"id\":\"g2.0\",\"body\":0},{\"id\":\"g2.5\",\"body\":1}", "{\"id\":\"a/b\",\"body\":0},{\"id\":\"..\",\"body\":1},{\"id\":\"..\\\\x\",\"body\":1}");
            File.WriteAllText(Path.Combine(_dir, run, "run.json"), RunJson(null, exam));
            File.WriteAllText(Path.Combine(_dir, run, "tank-exam-genomes.tsv"),
                "0\t" + GenomeResourceTests.Row(m.G2) + "\n1\t" + GenomeResourceTests.Row(m.Exam2) + "\n1\t" + GenomeResourceTests.Row(m.G0) + "\n");
            JsonElement list = Read(r, $"nursery://genomes/{run}");
            Assert.Equal(new[] { "g0.1", "g1.0", "g2.0", "exam.0", "exam.1", "exam.2" }, Ids(list));   // no plan id is a plain name: every exam genome is named by its line
            Assert.All(Ids(list), id => Assert.DoesNotContain("/", id));
            Assert.All(Ids(list), id => Assert.NotEqual("..", id));
            Assert.All(Enumerable.Range(3, 3), i => Assert.Contains("not a plain name", list.GetProperty("genomes")[i].GetProperty("idNote").GetString()));

            Assert.Throws<ResourceNotFound>(() => r.Read($"nursery://genome/{run}/.."));
            Assert.Throws<ResourceNotFound>(() => r.Read($"nursery://genome/{run}/%2E%2E"));
            Assert.Throws<ResourceNotFound>(() => r.Read($"nursery://genome/{run}/a%2Fb"));
            Assert.Throws<ResourceNotFound>(() => r.Read($"nursery://genome/{run}%2F../g0.1"));
            Assert.Throws<ResourceNotFound>(() => r.Read("nursery://genomes/.."));
            Assert.Throws<ResourceNotFound>(() => r.Read("nursery://genomes/%2E%2E"));
            Assert.Throws<ResourceNotFound>(() => r.GenomeText(run, ".."));
            Assert.Throws<ResourceNotFound>(() => r.GenomeText(run, "a/b"));
            Assert.Throws<ResourceNotFound>(() => r.GenomeText("..", "g0.1"));
            // a file name made from a genome id never holds a separator either
            Assert.DoesNotContain("/", r.GenomeText(run, "exam.1").fileName);
        }

        // ------------------------------------------------------------------ what a link names that is not there

        [Fact]
        public void EveryIdALinkNamesThatIsNothingIsListedOnceWhicheverEndNamesItAndBothEndsAreMarked()
        {
            // A port that is no port of the grown body (read as a source), and an output the genome stores for a joint the body does not have (read as a
            // target by one link and as a source by another); a second stored output of the same kind that no link names.
            const ulong GhostFrom = 0xDEADBEEF12345678UL, GhostOut = 0x0BADF00D87654321UL, Unlinked = 0x1111222233334444UL;
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made(mk =>
            {
                Phenotype b = Developer.Develop(mk.G0);
                ulong angle = GenomeResourceTests.Port(b, 1, SensorChannel.JointAngle);
                mk.G0.Brain.Outputs.Add(new BrainUnit { Id = GhostOut, Bias = 0.2f });
                mk.G0.Brain.Outputs.Add(new BrainUnit { Id = Unlinked, Bias = 0.3f });
                mk.G0.Brain.Links.Add(new BrainLink { From = GhostFrom, To = GenomeResourceTests.Out(b, 1, BrainOutputKind.Bend), Weight = 0.3f });
                mk.G0.Brain.Links.Add(new BrainLink { From = angle, To = GhostOut, Weight = 0.4f });                                         // a target that names nothing
                mk.G0.Brain.Links.Add(new BrainLink { From = GhostOut, To = GenomeResourceTests.Out(b, 2, BrainOutputKind.Power), Weight = -0.2f });   // the same id as a source
                mk.G0.Brain.Links.Add(new BrainLink { From = GhostFrom, To = GenomeResourceTests.Out(b, 2, BrainOutputKind.Bend), Weight = 0.1f });    // the ghost port again
            });
            JsonElement b2 = Read(r, $"nursery://genome/{run}/g0.1").GetProperty("brain");
            string hexFrom = BrainIds.Hex(GhostFrom), hexOut = BrainIds.Hex(GhostOut), hexUnlinked = BrainIds.Hex(Unlinked);
            JsonElement[] nothing = b2.GetProperty("nothing").EnumerateArray().ToArray();
            Assert.Equal(new[] { hexFrom, hexOut, hexUnlinked }, nothing.Select(x => x.GetProperty("id").GetString()).ToArray());   // each once, in the order the links first name them
            Assert.True(nothing[0].GetProperty("asSource").GetBoolean()); Assert.False(nothing[0].GetProperty("asTarget").GetBoolean());
            Assert.True(nothing[1].GetProperty("asSource").GetBoolean()); Assert.True(nothing[1].GetProperty("asTarget").GetBoolean());
            Assert.False(nothing[2].GetProperty("asSource").GetBoolean()); Assert.False(nothing[2].GetProperty("asTarget").GetBoolean());
            Assert.Equal(new[] { 2, 2, 0 }, nothing.Select(x => x.GetProperty("links").GetInt32()).ToArray());
            Assert.Equal(3, b2.GetProperty("counts").GetProperty("nothing").GetInt32());
            // each is also in exactly one of the node lists, flagged as existing nowhere, never twice
            foreach (string id in new[] { hexFrom, hexOut, hexUnlinked })
            {
                int inPorts = b2.GetProperty("ports").EnumerateArray().Count(p => p.GetProperty("id").GetString() == id && !p.GetProperty("exists").GetBoolean());
                int inOuts = b2.GetProperty("outputs").EnumerateArray().Count(p => p.GetProperty("id").GetString() == id && !p.GetProperty("exists").GetBoolean());
                Assert.Equal(1, inPorts + inOuts);
            }
            // and each link marks the end that names nothing, alike for a port and for a stored output
            JsonElement[] links = b2.GetProperty("links").EnumerateArray().ToArray();
            Assert.All(links.Where(l => l.GetProperty("from").GetString() == hexFrom), l => Assert.Equal("nothing", l.GetProperty("fromKind").GetString()));
            JsonElement into = links.Single(l => l.GetProperty("to").GetString() == hexOut);
            Assert.Equal("nothing", into.GetProperty("toKind").GetString());
            Assert.Equal("port", into.GetProperty("fromKind").GetString());
            JsonElement outOf = links.Single(l => l.GetProperty("from").GetString() == hexOut);
            Assert.Equal("nothing", outOf.GetProperty("fromKind").GetString());
            Assert.Equal("output", outOf.GetProperty("toKind").GetString());
            Assert.DoesNotContain(links, l => l.GetProperty("fromKind").GetString() == "output-nothing" || l.GetProperty("toKind").GetString() == "output-nothing");
        }

        // ------------------------------------------------------------------ bodies: mirrored, recursive, round, three joints, and the old format

        private static Genome Wire(Genome g)
        {
            g.NumberEdges();
            CentralWiring.Convert(g);
            Phenotype body = Developer.Develop(g);
            foreach (BrainOutput o in CentralWiring.Outputs(body)) g.Brain.Outputs.Add(new BrainUnit { Id = o.Id, Bias = 0.1f });
            List<BrainOutput> outs = CentralWiring.Outputs(body);
            List<BrainPort> ports = CentralWiring.Ports(body, SensorChannels.DefaultPool);
            if (outs.Count > 0 && ports.Count > 0) g.Brain.Links.Add(new BrainLink { From = ports[0].Id, To = outs[0].Id, Weight = 0.5f });
            return g;
        }

        private static Genome Mirrored()
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(GenomeResourceTests.Box(0.5f, JointType.Fixed));
            g.Nodes.Add(GenomeResourceTests.Box(0.3f, JointType.Hinge));
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f), Reflect = new Bool3(false, false, true) });
            return Wire(g);
        }

        private static Genome Recursive()
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(GenomeResourceTests.Box(0.5f, JointType.Fixed));
            g.Nodes.Add(GenomeResourceTests.Box(0.25f, JointType.Hinge));
            g.Nodes[1].RecursiveLimit = 3;
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f) });
            g.Nodes[1].Edges.Add(new MorphEdge { Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f) });
            return Wire(g);
        }

        private static Genome Round()
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(GenomeResourceTests.Box(0.5f, JointType.Fixed));
            g.Nodes.Add(GenomeResourceTests.Box(0.3f, JointType.Hinge));
            g.Nodes[0].ShapeId = ShapeIds.Sphere;
            g.Nodes[1].ShapeId = ShapeIds.Capsule;
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f) });
            return Wire(g);
        }

        private static Genome ThreeJoints()
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(GenomeResourceTests.Box(0.5f, JointType.Fixed));
            g.Nodes.Add(GenomeResourceTests.Box(0.3f, JointType.Hinge));
            g.Nodes.Add(GenomeResourceTests.Box(0.3f, JointType.Universal));
            g.Nodes.Add(GenomeResourceTests.Box(0.3f, JointType.Spherical));
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f) });
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 2, ParentAnchor = new Float3(-1f, 0f, 0f), ChildAnchor = new Float3(1f, 0f, 0f) });
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 3, ParentAnchor = new Float3(0f, 1f, 0f), ChildAnchor = new Float3(0f, -1f, 0f) });
            return Wire(g);
        }

        private static Genome OldFormat()
        {
            // the format before the one brain (11): per-node neurons, no brain; written as it was
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(GenomeResourceTests.Box(0.5f, JointType.Fixed));
            g.Nodes.Add(GenomeResourceTests.Box(0.3f, JointType.Hinge));
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f) });
            return g;
        }

        private static void AssertBodyAndBrain(JsonElement g, Genome genome, bool central)
        {
            Phenotype core = Developer.Develop(genome);
            JsonElement plan = g.GetProperty("bodyPlan");
            Assert.Equal(core.PartCount, plan.GetProperty("partCount").GetInt32());
            Assert.Equal(core.Parts.Count(p => !p.IsRoot && p.JointType != JointType.Fixed), plan.GetProperty("joints").GetInt32());
            Assert.Equal(core.TotalDof, plan.GetProperty("dof").GetInt32());
            JsonElement[] parts = plan.GetProperty("parts").EnumerateArray().ToArray();
            for (int i = 0; i < core.PartCount; i++)
            {
                Assert.Equal(core.Parts[i].ShapeId, parts[i].GetProperty("shape").GetString());
                Assert.Equal(core.Parts[i].Mirrored, parts[i].GetProperty("mirrored").GetBoolean());
                Assert.Equal(core.Parts[i].CellTypeId, parts[i].GetProperty("cellType").GetString());
                Assert.Equal(core.Parts[i].HalfExtents.X, (float)parts[i].GetProperty("halfExtents").GetProperty("x").GetDouble(), 5);
                if (!core.Parts[i].IsRoot) Assert.Equal(core.Parts[i].ParentIndex, parts[i].GetProperty("parent").GetInt32());
                if (core.Parts[i].ShapeId == ShapeIds.Sphere) Assert.Equal(SphereShape.Radius(core.Parts[i].HalfExtents), (float)parts[i].GetProperty("radius").GetDouble(), 5);
                if (core.Parts[i].ShapeId == ShapeIds.Capsule) Assert.Equal(CapsuleShape.HalfSpan(core.Parts[i].HalfExtents), (float)parts[i].GetProperty("halfSpan").GetDouble(), 5);
            }
            JsonElement b = g.GetProperty("brain");
            if (!central) { Assert.Equal("per-node", b.GetProperty("model").GetString()); return; }
            Assert.Equal("central", b.GetProperty("model").GetString());
            Assert.Equal(CentralWiring.Outputs(core).Select(o => BrainIds.Hex(o.Id)).ToArray(), b.GetProperty("outputs").EnumerateArray().Select(o => o.GetProperty("id").GetString()).ToArray());
            Assert.Equal(CentralWiring.Ports(core, SensorChannels.DefaultPool).Select(p => BrainIds.Hex(p.Id)).ToArray(), b.GetProperty("ports").EnumerateArray().Select(p => p.GetProperty("id").GetString()).ToArray());
        }

        [Fact]
        public void AMirroredARecursiveARoundAndAThreeJointedBodyAreEachWhatCoreGrowsAndWires()
        {
            Genome mirrored = Mirrored(), recursive = Recursive(), round = Round(), three = ThreeJoints();
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made(mk => mk.Plan = false);
            File.WriteAllText(Path.Combine(_dir, run, "tank-best.jsonl"), string.Join("\n", new[] { mirrored, recursive, round, three }.Select(GenomeResourceTests.Row)) + "\n");
            File.Delete(Path.Combine(_dir, run, "tank-scores.jsonl"));
            Assert.True(Developer.Develop(mirrored).Parts.Any(p => p.Mirrored), "the mirrored body has a mirrored part");
            Assert.True(Developer.Develop(recursive).PartCount >= 4, "the recursive body repeats a node");
            Assert.Contains(Developer.Develop(round).Parts, p => p.ShapeId == ShapeIds.Sphere);
            Assert.Equal(3, Developer.Develop(three).Parts.Count(p => !p.IsRoot && p.JointType != JointType.Fixed));
            JsonElement list = Read(r, $"nursery://genomes/{run}");
            Assert.Equal(new[] { "best.g0", "best.g1", "best.g2", "best.g3" }, Ids(list).Take(4).ToArray());
            AssertBodyAndBrain(Read(r, $"nursery://genome/{run}/best.g0"), mirrored, true);
            AssertBodyAndBrain(Read(r, $"nursery://genome/{run}/best.g1"), recursive, true);
            AssertBodyAndBrain(Read(r, $"nursery://genome/{run}/best.g2"), round, true);
            AssertBodyAndBrain(Read(r, $"nursery://genome/{run}/best.g3"), three, true);
            JsonElement t = Read(r, $"nursery://genome/{run}/best.g3");
            Assert.Equal(3, t.GetProperty("bodyPlan").GetProperty("joints").GetInt32());
            Assert.Equal(Developer.Develop(three).TotalDof, t.GetProperty("bodyPlan").GetProperty("dof").GetInt32());
            Assert.True(t.GetProperty("bodyPlan").GetProperty("dof").GetInt32() > 3, "a hinge, a universal and a spherical joint add up to more than three degrees of freedom");
        }

        [Fact]
        public void AFormatElevenGenomeIsReadWithItsPerNodeBrainAndNoPortsToDraw()
        {
            Genome old = OldFormat();
            (ResourceResolver r, string run, GenomeResourceTests.RunMaker m) = Made(mk => mk.Plan = false);
            File.WriteAllText(Path.Combine(_dir, run, "tank-best.jsonl"), GenomeResourceTests.Row(old) + "\n");
            File.Delete(Path.Combine(_dir, run, "tank-scores.jsonl"));
            JsonElement g = Read(r, $"nursery://genome/{run}/best.g0");
            Assert.Equal(11, g.GetProperty("genome").GetProperty("format").GetInt32());
            AssertBodyAndBrain(g, old, false);
            Assert.Contains("per-part neurons", g.GetProperty("brain").GetProperty("note").GetString());
            Assert.Equal(JsonValueKind.Null, g.GetProperty("problem").ValueKind);
            Assert.Equal(JsonValueKind.Null, Read(r, $"nursery://genomes/{run}").GetProperty("genomes")[0].GetProperty("brain").ValueKind);
        }

        // ------------------------------------------------------------------ the pilot, as a real run

        /// <summary>
        /// The finished pilot run read as the room reads it, with the config room.json names for it. Every link end of every one of its genomes names a port of
        /// that config's pool; under Core's default pool 4,462 of the 6,451 link source ends would name none. Read-only: nothing is written under the run.
        /// </summary>
        [RealRunFact("pilot-deep-brain2/tank-best.jsonl", "runs-jd/big-cur-deep-brain2/config.json", Skip = "runs at nursery-room e824ed4, or once room.json names a copy of the pilot's config that this build loads: this build refuses scratch/nursery/runs-jd/big-cur-deep-brain2/config.json, which lacks the season and floor-pool-share tunables")]
        public void EveryLinkEndOfThePilotsGenomesNamesAPortOfTheConfigRoomJsonGivesIt()
        {
            string main = RealRunFactAttribute.Find("pilot-deep-brain2/tank-best.jsonl");
            string folder = Path.Combine(main, "scratch", "nursery", "pilot-deep-brain2");
            RoomSettings shipped = RoomSettings.Load(Path.Combine(Where.Root, "rounds", "nursery", "room.json"));
            string cfgRel = shipped.RunConfigs["scratch/nursery/pilot-deep-brain2"];
            // This tree's room: the run and its config are found in the main checkout, as a worktree's room finds them.
            var withConfig = new ResourceResolver(new RoomContext(Where.Root, RoomSettings.Parse(Where.SettingsJson("{\"folder\":\"scratch/nursery/pilot-deep-brain2\",\"config\":\"" + cfgRel + "\"}")), main, Path.Combine(_dir, "none.jsonl")));
            var without = new ResourceResolver(new RoomContext(Where.Root, RoomSettings.Parse(Where.SettingsJson("\"scratch/nursery/pilot-deep-brain2\"")), main, Path.Combine(_dir, "none.jsonl")));

            JsonElement list = Read(withConfig, "nursery://genomes/pilot-deep-brain2");
            Assert.Equal("room.json", list.GetProperty("config").GetProperty("source").GetString());
            Assert.True(list.GetProperty("config").GetProperty("loaded").GetBoolean());
            int count = list.GetProperty("count").GetInt32(), linkEnds = 0, unlistedUnderConfig = 0, outsidePoolUnderConfig = 0, outsidePoolDefault = 0;
            Assert.True(count >= 100);
            RunConfig cfg = RunConfigJson.Read(File.ReadAllText(Path.Combine(main, cfgRel)));
            foreach (string id in Ids(list))
            {
                JsonElement g = Read(withConfig, "nursery://genome/pilot-deep-brain2/" + id);
                Assert.Equal(JsonValueKind.Null, g.GetProperty("problem").ValueKind);
                JsonElement b = g.GetProperty("brain");
                Assert.Equal(0, b.GetProperty("counts").GetProperty("nothing").GetInt32());
                foreach (JsonElement l in b.GetProperty("links").EnumerateArray())
                {
                    linkEnds++;
                    if (l.GetProperty("fromKind").GetString() == "nothing" || l.GetProperty("toKind").GetString() == "nothing") unlistedUnderConfig++;
                }
                outsidePoolUnderConfig += b.GetProperty("ports").EnumerateArray().Count(p => p.GetProperty("links").GetInt32() > 0 && !p.GetProperty("inPool").GetBoolean());
                // ports come in Core's order for the config's pool
                Phenotype body = Developer.Develop(GenomeJson.Read(TextOf(folder, list, id)), cfg.Development, null, cfg.Shapes);
                Assert.Equal(CentralWiring.Ports(body, cfg.SensorPool()).Select(p => BrainIds.Hex(p.Id)).ToArray(),
                    b.GetProperty("ports").EnumerateArray().Where(p => p.GetProperty("inPool").GetBoolean()).Select(p => p.GetProperty("id").GetString()).ToArray());
                JsonElement d = Read(without, "nursery://genome/pilot-deep-brain2/" + id);
                outsidePoolDefault += d.GetProperty("brain").GetProperty("ports").EnumerateArray().Count(p => p.GetProperty("links").GetInt32() > 0 && !p.GetProperty("inPool").GetBoolean());
                Assert.Equal("unknown", d.GetProperty("config").GetProperty("source").GetString());
            }
            _out.WriteLine($"pilot-deep-brain2: {count} genomes, {linkEnds} links; under the room.json config: {unlistedUnderConfig} link ends name nothing, {outsidePoolUnderConfig} linked ports outside its pool; under Core's defaults: {outsidePoolDefault} linked ports outside the default pool");
            Assert.Equal(0, unlistedUnderConfig);
            Assert.Equal(0, outsidePoolUnderConfig);
            Assert.True(outsidePoolDefault > 0, "under Core's defaults the pilot's links name ports the default pool lacks");
        }

        private static string TextOf(string folder, JsonElement list, string id)
        {
            // the genome's text as the room serves it (a read of the same files), to compare ports against Core's
            JsonElement x = list.GetProperty("genomes").EnumerateArray().Single(q => q.GetProperty("id").GetString() == id);
            string source = x.GetProperty("source").GetString();   // "<file> line <n>"
            int at = source.LastIndexOf(" line ", StringComparison.Ordinal);
            string file = source.Substring(0, at);
            int line = int.Parse(source.Substring(at + 6), CultureInfo.InvariantCulture) - 1;
            string l = GenomeSet.ReadLines(Path.Combine(folder, file))[line];
            return file.EndsWith(".tsv", StringComparison.Ordinal) ? l.Substring(l.IndexOf('\t') + 1) : l;
        }
    }
}
