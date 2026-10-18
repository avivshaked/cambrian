using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D117 in the farm: the pool's files and hash, the refusal of a changed pool on a resume
    /// (<see cref="TricklePoolFiles.Load"/>, which is the resume's own check), the two variables,
    /// the header's token, the table's column and the stats fields; and the weighted list
    /// (<c>@list</c>, <c>pool/index.tsv</c>), pools past 100 and the header's <c>weighted</c> and
    /// <c>brain central</c>. The draw itself is Core's (<c>Evosim.Core.Tests.TricklePoolTests</c>).
    /// </summary>
    public class TricklePoolFarmTests
    {
        private static string Fixture(string file) =>
            Path.Combine(AppContext.BaseDirectory, "fixtures", "pool", file);

        private static string PoolList() =>
            Fixture("r45s1-stomach-100.json") + ";" + Fixture("growth-ledger-genome.json");

        private static string Scratch(string name)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "pool-tests", name, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        // ------------------------------------------------------------------ the files and the hash

        /// <summary>
        /// The pool is re-serialised in this build's bytes (a snapshot row's <c>id</c> is
        /// dropped), written one line a file, and the hash is over those files in order: the
        /// files on disk hash to what the config pins, and they load back as the same genomes.
        /// </summary>
        [Fact]
        public void APreparedPoolWritesAndLoadsBackUnderItsHash()
        {
            TricklePoolFiles.Pool pool = TricklePoolFiles.Prepare(PoolList());

            Assert.Equal(2, pool.Genomes.Count);
            Assert.Matches("^[0-9a-f]{64}$", pool.Hash);
            Assert.DoesNotContain("\"id\"", pool.Lines[0]);
            Assert.StartsWith("{\"format\":", pool.Lines[0]);

            string run = Scratch("roundtrip");
            TricklePoolFiles.Write(run, pool.Lines);

            byte[] first = File.ReadAllBytes(Path.Combine(run, "pool", "00.json"));
            Assert.Equal(TricklePoolFiles.BytesOf(pool.Lines[0]), first);
            Assert.Equal((byte)'\n', first[first.Length - 1]);
            Assert.True(File.Exists(Path.Combine(run, "pool", "01.json")));

            var config = new RunConfig
            {
                FoundingTricklePoolCount = pool.Genomes.Count,
                FoundingTricklePoolHash = pool.Hash,
            };

            TricklePoolFiles.Pool loaded = TricklePoolFiles.Load(run, config);
            Assert.Equal(pool.Hash, loaded.Hash);
            Assert.Equal(pool.Lines, loaded.Lines);
            Assert.Equal(GenomeJson.Write(pool.Genomes[1]), GenomeJson.Write(loaded.Genomes[1]));

            // The same genomes named in another order are another pool.
            TricklePoolFiles.Pool swapped = TricklePoolFiles.Prepare(
                Fixture("growth-ledger-genome.json") + ";" + Fixture("r45s1-stomach-100.json"));
            Assert.NotEqual(pool.Hash, swapped.Hash);

            // And no pool named, none loaded.
            Assert.Null(TricklePoolFiles.Load(run, new RunConfig()));
        }

        /// <summary>
        /// A resume reads the source run's <c>pool/</c> through <see cref="TricklePoolFiles.Load"/>
        /// and refuses it when one byte of one file has changed since the launch, whether or not
        /// the file still parses; and a file missing is refused too.
        /// </summary>
        [Fact]
        public void AChangedPoolIsRefusedOnResume()
        {
            TricklePoolFiles.Pool pool = TricklePoolFiles.Prepare(PoolList());
            var config = new RunConfig
            {
                FoundingTricklePoolCount = pool.Genomes.Count,
                FoundingTricklePoolHash = pool.Hash,
            };

            string run = Scratch("changed");
            TricklePoolFiles.Write(run, pool.Lines);

            string file = Path.Combine(run, "pool", "01.json");
            byte[] bytes = File.ReadAllBytes(file);

            // One digit of the adult scale, "1.280298" -> "1.280299": still a genome, another body.
            string text = Encoding.UTF8.GetString(bytes);
            string edited = text.Replace("\"adultScale\":1.280298", "\"adultScale\":1.280299");
            Assert.NotEqual(text, edited);
            File.WriteAllText(file, edited, new UTF8Encoding(false));
            GenomeJson.Read(edited);

            var changed = Assert.Throws<InvalidOperationException>(() => TricklePoolFiles.Load(run, config));
            Assert.Contains("hashes to", changed.Message);
            Assert.Contains("the config pins " + pool.Hash, changed.Message);

            File.WriteAllBytes(file, bytes);
            Assert.NotNull(TricklePoolFiles.Load(run, config));

            File.Delete(file);
            var missing = Assert.Throws<InvalidOperationException>(() => TricklePoolFiles.Load(run, config));
            Assert.Contains("01.json is missing", missing.Message);
        }

        /// <summary>
        /// A file the build cannot read is refused with the reader's own text, and a list with a
        /// hole in it is refused rather than closed up.
        /// </summary>
        [Fact]
        public void AnUnreadableOrMissingPoolFileIsRefusedAtLaunch()
        {
            string dir = Scratch("unreadable");
            string old = Path.Combine(dir, "format8.json");
            File.WriteAllText(
                old, File.ReadAllText(Fixture("r45s1-stomach-100.json")).Replace("\"format\":" + GenomeJson.FormatVersion, "\"format\":8"));

            var unreadable = Assert.Throws<InvalidOperationException>(() => TricklePoolFiles.Prepare(old));
            Assert.Contains("is not a genome this build reads", unreadable.Message);

            Assert.Throws<FileNotFoundException>(
                () => TricklePoolFiles.Prepare(Path.Combine(dir, "nothing.json")));

            var hole = Assert.Throws<InvalidOperationException>(
                () => TricklePoolFiles.Prepare(Fixture("r45s1-stomach-100.json") + ";;" + Fixture("growth-ledger-genome.json")));
            Assert.Contains("entry 1 is empty", hole.Message);
        }

        // ------------------------------------------------------------------ the variables and the header

        [Fact]
        public void TheTwoVariablesReachTheSettingsAndTheShareTheConfig()
        {
            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_TRICKLE"] = "1/30";
            block["EVOSIM_TRICKLE_POOL"] = PoolList();
            block["EVOSIM_TRICKLE_POOL_SHARE"] = "0.1";

            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            Assert.Equal(PoolList(), settings.TricklePool);
            Assert.Equal(0.1f, settings.TricklePoolShare);

            RunConfig config = EnvBinding.BuildConfig(settings);
            Assert.Equal(0.1f, config.FoundingTricklePoolShare);
            Assert.Equal(0, config.FoundingTricklePoolCount);

            Assert.Empty(EnvBinding.UnknownNames(new[] { "EVOSIM_TRICKLE_POOL", "EVOSIM_TRICKLE_POOL_SHARE" }));

            block.Remove("EVOSIM_TRICKLE_POOL");
            block.Remove("EVOSIM_TRICKLE_POOL_SHARE");
            EnvSettings off = EnvBinding.Read(EnvBinding.Of(block));
            Assert.Null(off.TricklePool);
            Assert.Equal(0f, EnvBinding.BuildConfig(off).FoundingTricklePoolShare);
        }

        /// <summary>
        /// <c>EVOSIM_FLOOR_POOL_SHARE</c> (the owner's ruling of 2026-10-04) reaches the settings
        /// and the config as the trickle's share does: unset is 0, a word that is not a number is
        /// refused when the block is read, and a share outside [0, 1] when the config is built.
        /// </summary>
        [Fact]
        public void TheFloorShareVariableParsesAndRefusesAsTheTricklesDoes()
        {
            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_TRICKLE"] = "1/30";
            block["EVOSIM_TRICKLE_POOL"] = PoolList();
            block["EVOSIM_FLOOR_POOL_SHARE"] = "0.6";

            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            Assert.Equal(0.6f, settings.FloorPoolShare);
            Assert.Equal(0.6f, EnvBinding.BuildConfig(settings).FoundingFloorPoolShare);
            Assert.Empty(EnvBinding.UnknownNames(new[] { "EVOSIM_FLOOR_POOL_SHARE" }));

            block.Remove("EVOSIM_FLOOR_POOL_SHARE");
            EnvSettings off = EnvBinding.Read(EnvBinding.Of(block));
            Assert.Equal(0f, off.FloorPoolShare);
            Assert.Equal(0f, EnvBinding.BuildConfig(off).FoundingFloorPoolShare);

            foreach (string name in new[] { "EVOSIM_TRICKLE_POOL_SHARE", "EVOSIM_FLOOR_POOL_SHARE" })
            {
                var words = new Dictionary<string, string>(block) { [name] = "most" };
                var notANumber = Assert.Throws<ArgumentException>(() => EnvBinding.Read(EnvBinding.Of(words)));
                Assert.Contains(name + " is 'most', which is not a number", notANumber.Message);

                var above = new Dictionary<string, string>(block) { [name] = "1.5" };
                EnvSettings read = EnvBinding.Read(EnvBinding.Of(above));
                Assert.Throws<ArgumentOutOfRangeException>(() => EnvBinding.BuildConfig(read));
            }
        }

        /// <summary>
        /// A floor share above 0 adds <c>, floor 0.6</c> to the pool token, and a share of 0 adds
        /// nothing, so the token is the trickle build's.
        /// </summary>
        [Fact]
        public void TheHeaderNamesTheFloorShareOnlyAboveZero()
        {
            Assert.Equal(
                " · pool 0.6 of 2, floor 0.6",
                Between(HeaderWith(0.6f, pool: true, floorShare: 0.6f), " · trickle 1/30 s", " · ceiling "));
            Assert.Equal(
                " · pool 0 of 2, floor 1",
                Between(HeaderWith(0f, pool: true, floorShare: 1f), " · trickle 1/30 s", " · ceiling "));
            Assert.Equal(
                " · pool 0.6 of 2",
                Between(HeaderWith(0.6f, pool: true, floorShare: 0f), " · trickle 1/30 s", " · ceiling "));
            Assert.DoesNotContain(", floor ", HeaderWith(0.6f, pool: true));
        }

        /// <summary>
        /// The header reads <c>pool 0.1 of 2</c> after the trickle's token when a pool is named,
        /// <c>pool 0 of 2</c> for a pool named and unused, and nothing at all without one.
        /// </summary>
        [Fact]
        public void TheHeaderNamesThePoolAfterTheTrickle()
        {
            Assert.Equal(" · pool 0.1 of 2", Between(HeaderWith(0.1f, pool: true), " · trickle 1/30 s", " · ceiling "));
            Assert.Equal(" · pool 0 of 2", Between(HeaderWith(0f, pool: true), " · trickle 1/30 s", " · ceiling "));

            string none = HeaderWith(0f, pool: false);
            Assert.Contains(" · trickle 1/30 s · ceiling ", none);
            Assert.DoesNotContain(" · pool ", none);
        }

        private static string HeaderWith(
            float share, bool pool, string weightedList = null, bool central = false, float floorShare = 0f)
        {
            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_TRICKLE"] = "1/30";
            block["EVOSIM_TRICKLE_POOL_SHARE"] = share.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (floorShare > 0f)
            {
                block["EVOSIM_FLOOR_POOL_SHARE"] = floorShare.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (central)
            {
                // A Central world refuses a neural price it could not charge (D146).
                block["EVOSIM_BRAIN_MODEL"] = "central";
                block["EVOSIM_NEURON_COST"] = "0";
                block["EVOSIM_CONNECTION_COST"] = "0";
            }

            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig config = EnvBinding.BuildConfig(settings);

            TricklePoolFiles.Pool files = null;
            if (pool)
            {
                files = TricklePoolFiles.Prepare(weightedList != null ? "@" + weightedList : PoolList());
                config.FoundingTricklePoolCount = files.Genomes.Count;
                config.FoundingTricklePoolHash = files.Hash;
            }

            var world = new World(config, settings.Seed, files?.Genomes, files?.Weights);
            SpaceFacts space = SpaceFacts.Of(
                world,
                hasWall: config.SharedSpace && config.WorldShape == WorldShape.Tank,
                hasFloor: config.SharedSpace,
                threads: 4,
                inoculumHashShort: null);

            return Report.HeaderLine(settings, config, space, "9.9.9.9");
        }

        /// <summary>The text after <paramref name="after"/> up to the first <paramref name="until"/>.</summary>
        private static string Between(string line, string after, string until)
        {
            int start = line.IndexOf(after, StringComparison.Ordinal);
            Assert.True(start >= 0, "no '" + after + "' in the header");
            start += after.Length;
            int end = line.IndexOf(until, start, StringComparison.Ordinal);
            return line.Substring(start, end - start);
        }

        // ------------------------------------------------------------------ the weighted list

        private static string ListFile(string dir, params string[] rows)
        {
            string path = Path.Combine(dir, "pool.tsv");
            File.WriteAllText(path, string.Join("\n", rows) + "\n", new UTF8Encoding(false));
            return path;
        }

        /// <summary>
        /// Two genomes, one named by an absolute path and one beside the list by a relative one,
        /// with the four origin columns round 55's lists carry.
        /// </summary>
        private static string TwoGenomeList(string dir)
        {
            File.Copy(Fixture("growth-ledger-genome.json"), Path.Combine(dir, "g.json"));
            return ListFile(
                dir,
                "path\tweight\trun\tbody\tborn\tscore",
                Fixture("r45s1-stomach-100.json") + "\t3\tnursery-a\t12\t400\t512.5",
                "g.json\t1.0\tnursery-b\t7\t\t480");
        }

        /// <summary>
        /// <c>EVOSIM_TRICKLE_POOL=@list</c>: the genomes are the unweighted pool's bytes, the
        /// weights and origins are pinned in <c>pool/index.tsv</c> (no paths, the weights in their
        /// shortest round-trip text), the hash takes the index after the genome files, and the
        /// run directory loads back the same weights, origins and hash.
        /// </summary>
        [Fact]
        public void AListFilePreparesWritesAndLoadsWithItsIndex()
        {
            string dir = Scratch("list");
            string list = TwoGenomeList(dir);

            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_TRICKLE_POOL"] = "@" + list;
            Assert.Equal("@" + list, EnvBinding.Read(EnvBinding.Of(block)).TricklePool);

            TricklePoolFiles.Pool pool = TricklePoolFiles.Prepare("@" + list);

            Assert.Equal(2, pool.Genomes.Count);
            Assert.Equal(new[] { 3d, 1d }, pool.Weights);
            Assert.Equal(new[] { "run", "body", "born", "score" }, pool.OriginNames);
            Assert.Equal(new[] { "nursery-b", "7", "", "480" }, pool.Origins[1]);
            Assert.Equal(
                "pool\tweight\trun\tbody\tborn\tscore\n" +
                "0\t3\tnursery-a\t12\t400\t512.5\n" +
                "1\t1\tnursery-b\t7\t\t480\n",
                pool.IndexText);

            TricklePoolFiles.Pool plain = TricklePoolFiles.Prepare(PoolList());
            Assert.Equal(plain.Lines, pool.Lines);
            Assert.NotEqual(plain.Hash, pool.Hash);
            Assert.Equal(TricklePoolFiles.HashOf(pool.Lines, pool.IndexText), pool.Hash);

            string run = Scratch("list-run");
            TricklePoolFiles.Write(run, pool.Lines, pool.IndexText);
            Assert.Equal(
                new UTF8Encoding(false).GetBytes(pool.IndexText),
                File.ReadAllBytes(Path.Combine(run, "pool", "index.tsv")));

            var config = new RunConfig
            {
                FoundingTricklePoolCount = pool.Genomes.Count,
                FoundingTricklePoolHash = pool.Hash,
            };

            TricklePoolFiles.Pool loaded = TricklePoolFiles.Load(run, config);
            Assert.Equal(pool.Hash, loaded.Hash);
            Assert.Equal(pool.Lines, loaded.Lines);
            Assert.Equal(pool.Weights, loaded.Weights);
            Assert.Equal(pool.OriginNames, loaded.OriginNames);
            Assert.Equal(pool.Origins, loaded.Origins);
            Assert.Equal(pool.IndexText, loaded.IndexText);

            // The same bodies at the same weights, spelt otherwise from another folder, are the
            // same pool; another weight is another pool.
            string other = Scratch("list-other");
            File.Copy(Fixture("growth-ledger-genome.json"), Path.Combine(other, "renamed.json"));
            string respelt = ListFile(
                other,
                "path\tweight\trun\tbody\tborn\tscore",
                Fixture("r45s1-stomach-100.json") + "\t3.000\tnursery-a\t12\t400\t512.5",
                Path.Combine(other, "renamed.json") + "\t1e0\tnursery-b\t7\t\t480");
            Assert.Equal(pool.Hash, TricklePoolFiles.Prepare(" @" + respelt).Hash);

            string reweighed = ListFile(
                other,
                "path\tweight\trun\tbody\tborn\tscore",
                Fixture("r45s1-stomach-100.json") + "\t2\tnursery-a\t12\t400\t512.5",
                Path.Combine(other, "renamed.json") + "\t1\tnursery-b\t7\t\t480");
            Assert.NotEqual(pool.Hash, TricklePoolFiles.Prepare("@" + reweighed).Hash);
        }

        /// <summary>
        /// A pool named the old way hashes exactly as before: SHA-256 over its files' bytes and
        /// nothing else, no <c>index.tsv</c> written, and no weights loaded.
        /// </summary>
        [Fact]
        public void AnUnweightedPoolHashesAsItAlwaysHas()
        {
            TricklePoolFiles.Pool pool = TricklePoolFiles.Prepare(PoolList());
            Assert.Null(pool.Weights);
            Assert.Null(pool.IndexText);

            string expected;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = new List<byte>();
                foreach (string line in pool.Lines) bytes.AddRange(new UTF8Encoding(false).GetBytes(line + "\n"));
                expected = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }

            Assert.Equal(expected, pool.Hash);
            Assert.Equal(expected, TricklePoolFiles.HashOf(pool.Lines));

            string run = Scratch("plain");
            TricklePoolFiles.Write(run, pool.Lines);
            Assert.False(File.Exists(Path.Combine(run, "pool", "index.tsv")));

            var config = new RunConfig { FoundingTricklePoolCount = 2, FoundingTricklePoolHash = pool.Hash };
            TricklePoolFiles.Pool loaded = TricklePoolFiles.Load(run, config);
            Assert.Null(loaded.Weights);
            Assert.Null(loaded.IndexText);
        }

        /// <summary>
        /// The index is part of the pinned pool: a weight edited after the launch, the index
        /// removed, or an index added to a run launched without one are each refused on resume.
        /// </summary>
        [Fact]
        public void AChangedIndexIsRefusedOnResume()
        {
            TricklePoolFiles.Pool pool = TricklePoolFiles.Prepare("@" + TwoGenomeList(Scratch("index")));
            var config = new RunConfig { FoundingTricklePoolCount = 2, FoundingTricklePoolHash = pool.Hash };

            string run = Scratch("index-run");
            TricklePoolFiles.Write(run, pool.Lines, pool.IndexText);
            string index = Path.Combine(run, "pool", "index.tsv");
            byte[] bytes = File.ReadAllBytes(index);

            File.WriteAllText(index, pool.IndexText.Replace("0\t3\t", "0\t4\t"), new UTF8Encoding(false));
            var edited = Assert.Throws<InvalidOperationException>(() => TricklePoolFiles.Load(run, config));
            Assert.Contains("hashes to", edited.Message);
            Assert.Contains("index.tsv", edited.Message);

            File.WriteAllBytes(index, bytes);
            Assert.Equal(new[] { 3d, 1d }, TricklePoolFiles.Load(run, config).Weights);

            File.Delete(index);
            var removed = Assert.Throws<InvalidOperationException>(() => TricklePoolFiles.Load(run, config));
            Assert.Contains("the config pins " + pool.Hash, removed.Message);

            TricklePoolFiles.Pool plain = TricklePoolFiles.Prepare(PoolList());
            var plainConfig = new RunConfig { FoundingTricklePoolCount = 2, FoundingTricklePoolHash = plain.Hash };
            string plainRun = Scratch("index-added");
            TricklePoolFiles.Write(plainRun, plain.Lines);
            Assert.NotNull(TricklePoolFiles.Load(plainRun, plainConfig));

            File.WriteAllBytes(Path.Combine(plainRun, "pool", "index.tsv"), bytes);
            var added = Assert.Throws<InvalidOperationException>(() => TricklePoolFiles.Load(plainRun, plainConfig));
            Assert.Contains("hashes to", added.Message);
        }

        /// <summary>
        /// A list the farm cannot read row for row is refused before anything is written: a
        /// header that is not <c>path</c> and <c>weight</c>, a row short of a cell, a weight that
        /// is not a positive finite number, an origin column named twice or taking a reserved
        /// name, a list with no rows, a missing list or genome, and an <c>@</c> naming nothing.
        /// </summary>
        [Fact]
        public void AListFileItCannotReadIsRefused()
        {
            string dir = Scratch("bad-list");
            string g = Fixture("r45s1-stomach-100.json");

            void Refused(string contains, params string[] rows)
            {
                var e = Assert.Throws<InvalidOperationException>(() => TricklePoolFiles.Prepare("@" + ListFile(dir, rows)));
                Assert.Contains(contains, e.Message);
            }

            Refused("does not start with a header line", "file\tweight", g + "\t1");
            Refused("does not start with a header line", g + "\t1");
            Refused("has 2 cells on line 2", "path\tweight\trun", g + "\t1");
            Refused("names no genomes", "path\tweight\trun");
            Refused("repeats a column name", "path\tweight\trun\trun", g + "\t1\ta\tb");
            Refused("repeats a column name", "path\tweight\tpool", g + "\t1\t3");

            foreach (string weight in new[] { "0", "-1", "NaN", "Infinity", "heavy", "" })
            {
                Refused("gives the weight '" + weight + "'", "path\tweight", g + "\t" + weight);
            }

            Assert.Throws<FileNotFoundException>(
                () => TricklePoolFiles.Prepare("@" + ListFile(dir, "path\tweight", "nothing.json\t1")));
            Assert.Throws<FileNotFoundException>(
                () => TricklePoolFiles.Prepare("@" + Path.Combine(dir, "no-list.tsv")));
            Assert.Throws<ArgumentException>(() => TricklePoolFiles.Prepare("@"));
        }

        /// <summary>
        /// A pool of 150 prepares, pins and loads: indices below 100 keep their two-digit names
        /// and the rest take the plain number, and a world takes all 150 with their weights. A
        /// pool of 1,001 is refused, from a list or from the variable, before any genome is read.
        /// </summary>
        [Fact]
        public void APoolOf150PreparesPinsAndLoads()
        {
            Assert.Equal("07.json", TricklePoolFiles.FileName(7));
            Assert.Equal("99.json", TricklePoolFiles.FileName(99));
            Assert.Equal("100.json", TricklePoolFiles.FileName(100));

            string dir = Scratch("big");
            var rows = new List<string> { "path\tweight\tbody" };
            for (int i = 0; i < 150; i++)
            {
                rows.Add(Fixture("r45s1-stomach-100.json") + "\t" + (i + 1) + "\t" + i);
            }

            TricklePoolFiles.Pool pool = TricklePoolFiles.Prepare("@" + ListFile(dir, rows.ToArray()));
            Assert.Equal(150, pool.Genomes.Count);

            string run = Scratch("big-run");
            TricklePoolFiles.Write(run, pool.Lines, pool.IndexText);
            foreach (string name in new[] { "00.json", "09.json", "99.json", "100.json", "149.json" })
            {
                Assert.True(File.Exists(Path.Combine(run, "pool", name)), name + " was not written");
            }

            Assert.False(File.Exists(Path.Combine(run, "pool", "150.json")));
            Assert.False(File.Exists(Path.Combine(run, "pool", "000.json")));

            var config = new RunConfig { FoundingTricklePoolCount = 150, FoundingTricklePoolHash = pool.Hash };
            TricklePoolFiles.Pool loaded = TricklePoolFiles.Load(run, config);
            Assert.Equal(150, loaded.Genomes.Count);
            Assert.Equal(150d, loaded.Weights[149]);
            Assert.Equal("149", loaded.Origins[149][0]);

            var world = new World(config, 1UL, loaded.Genomes, loaded.Weights);
            Assert.Equal(150, world.TricklePool.Count);

            // The old form carries 150 too.
            var paths = new string[150];
            for (int i = 0; i < paths.Length; i++) paths[i] = Fixture("r45s1-stomach-100.json");
            Assert.Equal(150, TricklePoolFiles.Prepare(string.Join(";", paths)).Genomes.Count);

            var tooMany = new List<string> { "path\tweight" };
            for (int i = 0; i <= RunConfig.MaxTricklePool; i++) tooMany.Add("absent-" + i + ".json\t1");
            var fromList = Assert.Throws<InvalidOperationException>(
                () => TricklePoolFiles.Prepare("@" + ListFile(dir, tooMany.ToArray())));
            Assert.Contains("a pool holds at most 1000", fromList.Message);

            var semicolons = new string[RunConfig.MaxTricklePool + 1];
            for (int i = 0; i < semicolons.Length; i++) semicolons[i] = "absent.json";
            var fromVariable = Assert.Throws<InvalidOperationException>(
                () => TricklePoolFiles.Prepare(string.Join(";", semicolons)));
            Assert.Contains("a pool holds at most 1000", fromVariable.Message);
        }

        // ------------------------------------------------------------------ the header's two tokens

        /// <summary>
        /// A weighted pool's token reads <c>pool 0.1 of 2 weighted</c>; the same genomes named
        /// without weights read as they always have.
        /// </summary>
        [Fact]
        public void TheHeaderSaysWeightedOnlyForAWeightedPool()
        {
            string weighted = HeaderWith(0.1f, pool: true, weightedList: TwoGenomeList(Scratch("header")));
            Assert.Equal(" · pool 0.1 of 2 weighted", Between(weighted, " · trickle 1/30 s", " · ceiling "));

            string plain = HeaderWith(0.1f, pool: true);
            Assert.Equal(" · pool 0.1 of 2", Between(plain, " · trickle 1/30 s", " · ceiling "));
            Assert.DoesNotContain("weighted", plain);
        }

        /// <summary>
        /// A Central world's header carries <c>brain central</c> after the neuron token, and a
        /// per-node world's has no brain token at all, so every header already written stands.
        /// </summary>
        [Fact]
        public void TheHeaderNamesTheCentralBrainOnlyInACentralWorld()
        {
            string central = HeaderWith(0f, pool: false, central: true);
            Assert.Matches(" W/input, work x[0-9.]+ · brain central · field ", central);

            string perNode = HeaderWith(0f, pool: false);
            Assert.DoesNotContain(" · brain ", perNode);
            Assert.Matches(" W/input, work x[0-9.]+ · field ", perNode);
        }

        // ------------------------------------------------------------------ the table and the stats

        [Fact]
        public void ThePoolColumnFollowsTheTrickleOnlyWhenAPoolIsNamed()
        {
            var plain = new Report("unused.md", new RunConfig { HorizontalPatches = 2 });
            Assert.Equal(Report.BaseColumns.Length + 2, plain.Columns.Count);
            Assert.DoesNotContain(Report.PoolColumn, plain.Columns);

            var named = new Report("unused.md", new RunConfig
            {
                HorizontalPatches = 2,
                FoundingTricklePoolCount = 3,
                FoundingTricklePoolHash = new string('d', 64),
            });

            Assert.Equal(Report.BaseColumns.Length + 3, named.Columns.Count);
            // The last base column, `max reach` since the instruments of 2026-09-23, and then the pool.
            Assert.Equal(Report.BaseColumns[Report.BaseColumns.Length - 1], named.Columns[Report.BaseColumns.Length - 1]);
            Assert.Equal("**pool**", named.Columns[Report.BaseColumns.Length]);
            Assert.Equal("p0", named.Columns[Report.BaseColumns.Length + 1]);
            Assert.Equal("p1", named.Columns[Report.BaseColumns.Length + 2]);
        }

        /// <summary>
        /// A small farm world with the floor closing at 20 s, a trickle of one a second and half
        /// of it from the pool, stepped to 60 s: the sampler's row fills the pool column (it
        /// throws on a count mismatch), the stats row carries <c>poolSpawns</c>, the lineage
        /// carries <c>src: pool</c>, and the sampler's state round-trips with the pool's baseline.
        /// </summary>
        [Fact]
        public void AFarmWorldWithAPoolReportsItsPoolFounders()
        {
            RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>
            {
                { "EVOSIM_AREA", "100" },
                { "EVOSIM_DEPTH", "20" },
                { "EVOSIM_FOUNDER_DEPTH", "20" },
                { "EVOSIM_PATCHES", "1" },
                { "EVOSIM_SHARED_SPACE", "1" },
                { "EVOSIM_FIELD", "grid" },
                { "EVOSIM_FIELD_CELL", "2" },
                { "EVOSIM_FIELD_MATTER_CELL", "5" },
                { "EVOSIM_IRRADIANCE", "200" },
                { "EVOSIM_MATTER_BUDGET", "300" },
                { "EVOSIM_TISSUE_ENERGY", "500" },
                { "EVOSIM_RHO", "100" },
                { "EVOSIM_OVERHEAD", "100" },
                { "EVOSIM_DT", "0.02" },
                { "EVOSIM_FLOOR_CLOSES", "20" },
                { "EVOSIM_TRICKLE", "1" },
                { "EVOSIM_TRICKLE_POOL_SHARE", "0.5" },
            })));

            TricklePoolFiles.Pool pool = TricklePoolFiles.Prepare(PoolList());
            config.FoundingTricklePoolCount = pool.Genomes.Count;
            config.FoundingTricklePoolHash = pool.Hash;

            var world = new World(config, 1UL, pool.Genomes);
            string scratch = Scratch("farm");
            RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow);
            TricklePoolFiles.Write(dir.Path, pool.Lines);

            var sampler = new Sampler { PoolNamed = true };
            IReadOnlyList<string> columns = Sampler.Columns(config);
            string row;

            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: dir.Path))
            using (dir)
            {
                while (world.ElapsedSeconds < 60d) sim.Step();

                Assert.True(world.PoolSpawns > 0, "the pool admitted nobody by 60 s");
                Assert.True(world.TrickleSpawns > world.PoolSpawns, "the lottery drew nobody by 60 s");

                row = sampler.Write(sim, dir, columns);

                foreach (LineageEvent evt in world.DrainLineageEvents())
                {
                    dir.Lineage.Write(evt.ToJson());
                }

                // The sampler's state, with the pool's baseline, round-trips.
                using (var buffer = new MemoryStream())
                {
                    using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true)) sampler.WriteState(w);

                    long withPool = buffer.Length;
                    buffer.Position = 0;
                    var back = new Sampler { PoolNamed = true };
                    using (var r = new BinaryReader(buffer, Encoding.UTF8, leaveOpen: true)) back.ReadState(r);
                    Assert.Equal(withPool, buffer.Position);

                    using (var without = new MemoryStream())
                    {
                        using (var w = new BinaryWriter(without, Encoding.UTF8, leaveOpen: true))
                        {
                            new Sampler().WriteState(w);
                        }

                        var emptyWith = new MemoryStream();
                        using (var w = new BinaryWriter(emptyWith, Encoding.UTF8, leaveOpen: true))
                        {
                            new Sampler { PoolNamed = true }.WriteState(w);
                        }

                        Assert.Equal(without.Length + sizeof(long), emptyWith.Length);
                    }
                }
            }

            string[] cells = row.Trim('|', ' ').Split(new[] { " | " }, StringSplitOptions.None);
            Assert.Equal(columns.Count, cells.Length);

            int poolAt = new List<string>(columns).IndexOf(Report.PoolColumn);
            Assert.Equal("**" + world.PoolSpawns + "**", cells[poolAt]);

            string stats = File.ReadAllText(Path.Combine(dir.Path, "stats.jsonl"));
            Assert.Contains("\"poolSpawns\":" + world.PoolSpawns, stats);
            Assert.Contains("\"poolSpawnsWindow\":" + world.PoolSpawns, stats);

            string lineage = File.ReadAllText(Path.Combine(dir.Path, "lineage.jsonl"));
            Assert.Contains("\"src\":\"pool\",\"pool\":", lineage);
        }
    }
}
