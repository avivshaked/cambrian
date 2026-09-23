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
    /// the header's token, the table's column and the stats fields. The draw itself is Core's
    /// (<c>Evosim.Core.Tests.TricklePoolTests</c>).
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
            string old = Path.Combine(dir, "format7.json");
            File.WriteAllText(
                old, File.ReadAllText(Fixture("r45s1-stomach-100.json")).Replace("\"format\":8", "\"format\":7"));

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

        private static string HeaderWith(float share, bool pool)
        {
            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_TRICKLE"] = "1/30";
            block["EVOSIM_TRICKLE_POOL_SHARE"] = share.ToString(System.Globalization.CultureInfo.InvariantCulture);

            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig config = EnvBinding.BuildConfig(settings);

            TricklePoolFiles.Pool files = null;
            if (pool)
            {
                files = TricklePoolFiles.Prepare(PoolList());
                config.FoundingTricklePoolCount = files.Genomes.Count;
                config.FoundingTricklePoolHash = files.Hash;
            }

            var world = new World(config, settings.Seed, files?.Genomes);
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
            Assert.Equal("**trickle**", named.Columns[Report.BaseColumns.Length - 1]);
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
