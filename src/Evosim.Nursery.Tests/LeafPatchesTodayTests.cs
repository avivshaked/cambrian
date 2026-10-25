using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// A recipe with D182's patches and without D236's <c>switching</c> draws, places and swims exactly as it did before the field existed.
    /// <para>
    /// <b>This file uses only what the build before the field had</b>, so that it can be copied alone into a checkout of that build to record
    /// the fixture it compares against. To record: set <c>EVOSIM_P3_RECORD</c> to a file path and run this class's one test at the build
    /// before the field; it writes what it compares (every double as the hex of its bits) to that path and passes. Commit that file as
    /// <c>fixtures/p3-patches-today.txt</c>. Without the variable the test compares this build to the committed file.
    /// </para>
    /// </summary>
    [Collection("restep")]
    public class LeafPatchesTodayTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("leaf-patches-today");

        public LeafPatchesTodayTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        private static string Bits(double v) => BitConverter.DoubleToInt64Bits(v).ToString("x16", CultureInfo.InvariantCulture);

        private static string Bits(float v) => BitConverter.SingleToInt32Bits(v).ToString("x8", CultureInfo.InvariantCulture);

        /// <summary>What the test compares of one episode: the members, the leaves as laid and as left, and the solver's digest at the end.</summary>
        private sealed class Recorder : ITankWatcher
        {
            private readonly LeafTankFixtures.Seen _seen = new LeafTankFixtures.Seen();
            public readonly List<string> Lines = new List<string>();
            private ulong _digest;
            private string _begin, _end;

            private static string Dump(string what, EpisodeProbe p) =>
                what + string.Concat(p.Others().Select(l =>
                    $" [{l.Id} {Bits((float)l.X)} {Bits((float)l.Y)} {Bits((float)l.Z)} {Bits(l.Reserve)} {Bits(l.Tissue)}]"));

            public void Begin(EpisodeProbe probe)
            {
                _seen.Begin(probe);
                _begin = Dump("begin", probe);
            }

            public void Physics(EpisodeProbe probe) => _seen.Physics(probe);

            public void Metabolic(EpisodeProbe probe) => _seen.Metabolic(probe);

            public void End(EpisodeProbe probe)
            {
                _end = Dump("end", probe);
                _digest = probe.SolverDigest();
            }

            public IEnumerable<string> Summary(RestepOutcome o)
            {
                for (int k = 0; k < o.Members.Length; k++)
                {
                    RestepMemberOutcome m = o.Members[k];
                    LinkPose? root = _seen.FirstRoots[k];
                    yield return $"member {k} {m.Landed} {m.Survived} {Bits(m.Start)} {Bits(m.End)} {Bits(m.Net)} {Bits(m.Path)} {Bits(m.Work)} {Bits(m.Upkeep)} | " +
                                 (root == null ? "none" : $"{Bits(root.Value.X)} {Bits(root.Value.Y)} {Bits(root.Value.Z)}");
                }
                yield return _begin;
                yield return _end;
                yield return "digest " + _digest.ToString("x16", CultureInfo.InvariantCulture);
            }
        }

        [Fact]
        public void ARecipeWithPatchesAndNoSwitchingDrawsExactlyAsBefore()
        {
            (string config, string hash) = LeafTankFixtures.WriteConfig(_dir, LeafTankFixtures.Config());
            string pool = LeafTankFixtures.WriteBredPool(_dir, 3);
            string leaves = "{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 6, \"spentUnitsPerCubicMetre\": 0, " +
                            "\"patches\": {\"count\": 2, \"radius\": 4.5, \"ring\": 6.5}}";
            string recipe = LeafTankFixtures.Write(_dir, "recipe.json", LeafTankFixtures.RecipeJson(config, hash, pool, 3, leaves, seconds: 4));

            var got = new List<string>();
            for (int e = 0; e < 2; e++)
            {
                var recorder = new Recorder();
                RestepOutcome o = LeafTankFixtures.Swim(_dir, recipe, 3, 3, e, recorder);
                got.Add("episode " + e);
                got.AddRange(recorder.Summary(o));
            }
            foreach (string line in got) _out.WriteLine(line);

            string record = Environment.GetEnvironmentVariable("EVOSIM_P3_RECORD");
            if (!string.IsNullOrEmpty(record))
            {
                File.WriteAllLines(record, got);
                _out.WriteLine("recorded " + got.Count + " lines to " + record);
                return;
            }

            string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "p3-patches-today.txt");
            Assert.True(File.Exists(fixture),
                "fixtures/p3-patches-today.txt is not recorded: run this test at the build before D236's field with EVOSIM_P3_RECORD set to a file path, and commit that file there");
            Assert.Equal(File.ReadAllLines(fixture), got);
        }
    }
}
