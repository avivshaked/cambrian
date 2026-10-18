using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The episode row's file format, the running mean that must match the tank's old one bit for bit,
    /// the two rewards, and the registry that builds them from a recipe.
    /// </summary>
    public class RewardTests
    {
        private static JsonNode J(string text) => Json.Parse(text);

        private static IReadOnlyList<EpisodeRecord> Fixture()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "episodes-small.jsonl");
            return File.ReadAllLines(path).Where(l => l.Length > 0).Select(EpisodeRecord.FromJson).ToList();
        }

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        // ------------------------------------------------------------ the row

        private static EpisodeRecord Full() => new EpisodeRecord
        {
            Generation = 12, Draw = 3, Seed = 0xFEDCBA9876543210UL, Arena = "exam", Body = 5,
            Seconds = 60.5, Start = 10.1, End = 0, Net = -10.1, Food = 0.1, Eaten = 81.6, Light = 1d / 3d,
            Work = 2.5, Upkeep = 1e-9, Handling = 123456.789, Exuded = 0, Path = 4.75, Turn = 0.1 + 0.2,
            Fresh = double.NaN, Survived = false, DiedAt = 31.25,
        };

        private static void AssertSame(EpisodeRecord a, EpisodeRecord b)
        {
            Assert.Equal(a.Generation, b.Generation);
            Assert.Equal(a.Draw, b.Draw);
            Assert.Equal(a.Seed, b.Seed);
            Assert.Equal(a.Arena, b.Arena);
            Assert.Equal(a.Body, b.Body);
            Assert.Equal(a.Survived, b.Survived);
            foreach (string m in EpisodeRecord.Measures)
                Assert.Equal(Bits(a.Measure(m)), Bits(b.Measure(m)));
            Assert.Equal(Bits(a.Seconds), Bits(b.Seconds));
        }

        [Fact]
        public void ARowRoundTripsEveryFieldExactly()
        {
            EpisodeRecord a = Full();
            string line = a.ToJson();
            Assert.DoesNotContain("\n", line);
            Assert.Contains("\"seed\":\"0xfedcba9876543210\"", line);
            Assert.Contains("\"fresh\":null", line);
            Assert.Contains("\"food\":0.1,\"eaten\":81.6,", line);
            Assert.Contains("\"path\":4.75,\"turn\":0.30000000000000004,", line);
            EpisodeRecord b = EpisodeRecord.FromJson(line);
            AssertSame(a, b);
            Assert.True(double.IsNaN(b.Fresh));
            Assert.Equal(0xFEDCBA9876543210UL, b.Seed);
            Assert.Equal(-10.1, b.Net);
            Assert.Equal(81.6, b.Eaten);
            Assert.Equal(Bits(0.1 + 0.2), Bits(b.Turn));
            Assert.Equal(line, b.ToJson());
        }

        [Fact]
        public void ATurnWithNoStepMeasuredReadsBackAsNaN()
        {
            EpisodeRecord a = Full();
            a.Turn = double.NaN;
            string line = a.ToJson();
            Assert.Contains("\"turn\":null", line);
            Assert.True(double.IsNaN(EpisodeRecord.FromJson(line).Turn));
        }

        [Fact]
        public void ASurvivorsDiedAtReadsBackAsNaN()
        {
            EpisodeRecord a = Full();
            a.Survived = true;
            a.DiedAt = double.NaN;
            EpisodeRecord b = EpisodeRecord.FromJson(a.ToJson());
            Assert.True(b.Survived);
            Assert.True(double.IsNaN(b.DiedAt));
        }

        [Fact]
        public void ARowRefusesAMissingFieldAndAnUnknownOne()
        {
            string line = Full().ToJson();
            string missing = line.Replace("\"path\":4.75,", "");
            var e1 = Assert.Throws<InvalidDataException>(() => EpisodeRecord.FromJson(missing));
            Assert.Contains("path", e1.Message);

            // A row written before the mouth's count and the rocking were measured lacks them, and is refused rather than read as 0.
            foreach (string field in new[] { "\"eaten\":81.6,", "\"turn\":0.30000000000000004," })
            {
                var old = Assert.Throws<InvalidDataException>(() => EpisodeRecord.FromJson(line.Replace(field, "")));
                Assert.Contains(field.Substring(1, field.IndexOf('"', 1) - 1), old.Message);
            }

            string unknown = line.Substring(0, line.Length - 1) + ",\"extra\":1}";
            var e2 = Assert.Throws<InvalidDataException>(() => EpisodeRecord.FromJson(unknown));
            Assert.Contains("extra", e2.Message);

            string badSeed = line.Replace("\"0xfedcba9876543210\"", "\"12\"");
            var e3 = Assert.Throws<InvalidDataException>(() => EpisodeRecord.FromJson(badSeed));
            Assert.Contains("seed", e3.Message);
        }

        [Fact]
        public void MeasureNamesEveryFieldAndRefusesAStranger()
        {
            EpisodeRecord a = Full();
            Assert.Equal(18, EpisodeRecord.Measures.Count);
            Assert.Equal(81.6, a.Measure("eaten"));
            Assert.Equal(Bits(0.1 + 0.2), Bits(a.Measure("turn")));
            Assert.Equal(0d, a.Measure("survived"));
            a.Survived = true;
            Assert.Equal(1d, a.Measure("survived"));
            Assert.Equal(31.25, a.Measure("diedAt"));
            Assert.Equal(4.75, a.Measure("path"));
            Assert.Throws<ArgumentException>(() => a.Measure("speed"));
        }

        // ------------------------------------------------------------ the fixture

        [Fact]
        public void TheFixtureIsWrittenAsToJsonWritesIt()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "episodes-small.jsonl");
            foreach (string line in File.ReadAllLines(path).Where(l => l.Length > 0))
                Assert.Equal(line, EpisodeRecord.FromJson(line).ToJson());
        }

        [Fact]
        public void NetOnTheFixtureIsTheHandComputedRunningMean()
        {
            // Generations 0, 1, 2 hold nets {2}, {1,2,5}, {0.5,1.5,3,7}:
            // 2; (2*1+8)/4 = 2.5; (2.5*4+12)/8 = 2.75.
            IReadOnlyList<EpisodeRecord> episodes = Fixture();
            Assert.Equal(8, episodes.Count);
            IReward net = RewardRegistry.Create("Net", J("{}"));
            Score s = net.Score(episodes);
            Assert.Equal(2.75, s.Value);
            Assert.Single(s.Parts);
            Assert.Equal("net", s.Parts[0].Key);
            Assert.Equal(2.75, s.Parts[0].Value);
            Assert.Equal(new[] { "net" }, net.Reads);
        }

        [Fact]
        public void WeightedSumOnTheFixtureIsTheHandComputedValueAndParts()
        {
            // Weights net 1, path -0.5. Per episode: 0 | 0, -1, 4 | 0, 0, 1, 3.
            // Value: 0; (0*1+3)/4 = 0.75; (0.75*4+4)/8 = 0.875.
            // net part 2.75; path part -2 | -1,-3,-1 | -0.5,-1.5,-2,-4:
            // -2; (-2-5)/4 = -1.75; (-1.75*4-8)/8 = -1.875.
            IReward r = RewardRegistry.Create("WeightedSum", J("{\"weights\":{\"path\":-0.5,\"net\":1}}"));
            Assert.Equal(new[] { "net", "path" }, r.Reads);
            Score s = r.Score(Fixture());
            Assert.Equal(0.875, s.Value);
            Assert.Equal(2, s.Parts.Count);
            Assert.Equal("net", s.Parts[0].Key);
            Assert.Equal(2.75, s.Parts[0].Value);
            Assert.Equal("path", s.Parts[1].Key);
            Assert.Equal(-1.875, s.Parts[1].Value);
        }

        [Fact]
        public void AWeightedNaNMeasureMakesTheScoreNaNForGood()
        {
            // The third episode of generation 1 has no fresh reading, so that generation's sum is NaN,
            // and the running mean stays NaN through the clean generation after it. The old loop
            // forgot it there (see RewardMath.RunningMean); this one does not.
            IReward r = RewardRegistry.Create("WeightedSum", J("{\"weights\":{\"fresh\":1}}"));
            Assert.True(double.IsNaN(r.Score(Fixture()).Value));

            // A NaN in the last generation leaves the value NaN.
            var eps = Fixture().ToList();
            eps[7].Fresh = double.NaN;
            Assert.True(double.IsNaN(r.Score(eps).Value));
        }

        // ------------------------------------------------------------ the running mean

        /// <summary>
        /// The loop as the tank kept it, with its NaN test for "no generation yet", copied here so that
        /// the test does not test the code against itself. On finite data it must agree to the bit.
        /// </summary>
        private static double Reference(IReadOnlyList<EpisodeRecord> episodes, Func<EpisodeRecord, double> value)
        {
            double score = double.NaN;
            int count = 0;
            int i = 0;
            while (i < episodes.Count)
            {
                int g = episodes[i].Generation;
                double sum = 0;
                int n = 0;
                // The group summed as the tank summed it: Enumerable.Sum with a selector.
                var group = new List<EpisodeRecord>();
                for (; i < episodes.Count && episodes[i].Generation == g; i++) group.Add(episodes[i]);
                sum = group.Sum(value);
                n = group.Count;
                score = double.IsNaN(score) ? sum / n : (score * count + sum) / (count + n);
                count += n;
            }
            return score;
        }

        private static List<EpisodeRecord> Random200(int seed)
        {
            var rng = new Random(seed);
            var list = new List<EpisodeRecord>();
            int generation = 0, remaining = 200;
            // Seven generations of uneven size, the last taking what is left.
            for (int g = 0; g < 7; g++)
            {
                int n = g == 6 ? remaining : rng.Next(1, 50);
                n = Math.Min(n, remaining - (6 - g));
                for (int k = 0; k < n; k++)
                    list.Add(new EpisodeRecord
                    {
                        Generation = generation, Draw = k, Arena = "search",
                        Net = (rng.NextDouble() - 0.4) * Math.Pow(10, rng.Next(-3, 4)),
                        Path = rng.NextDouble() * 20,
                    });
                remaining -= n;
                generation++;
            }
            return list;
        }

        [Fact]
        public void RunningMeanMatchesTheReferenceLoopBitForBit()
        {
            bool sawADifferenceFromAPlainMean = false;
            for (int seed = 1; seed <= 20; seed++)
            {
                List<EpisodeRecord> eps = Random200(seed);
                Assert.Equal(200, eps.Count);
                Assert.Equal(7, eps.Select(e => e.Generation).Distinct().Count());
                Assert.Equal(Bits(Reference(eps, e => e.Net)), Bits(RewardMath.RunningMean(eps, e => e.Net)));
                Assert.Equal(Bits(Reference(eps, e => e.Path)), Bits(RewardMath.RunningMean(eps, e => e.Path)));

                double plain = 0;
                foreach (EpisodeRecord e in eps) plain += e.Net;
                plain /= eps.Count;
                if (Bits(plain) != Bits(RewardMath.RunningMean(eps, e => e.Net))) sawADifferenceFromAPlainMean = true;
            }
            // The check can tell the running mean from a plain mean: they differ in the last bits.
            Assert.True(sawADifferenceFromAPlainMean);
        }

        [Fact]
        public void EpisodesOutOfTheOrderSwumAreRefused()
        {
            // A generation going back, a draw not rising, or a second arena would be summed in an order
            // the tank never summed in, and the last bit could move with nothing to say so.
            var back = new List<EpisodeRecord>
            {
                new EpisodeRecord { Generation = 0, Arena = "search", Net = 1 },
                new EpisodeRecord { Generation = 1, Arena = "search", Net = 3 },
                new EpisodeRecord { Generation = 0, Arena = "search", Net = 5 },
            };
            Assert.Throws<ArgumentException>(() => RewardMath.RunningMean(back, e => e.Net));
            var draws = new List<EpisodeRecord>
            {
                new EpisodeRecord { Generation = 0, Draw = 1, Arena = "search", Net = 1 },
                new EpisodeRecord { Generation = 0, Draw = 1, Arena = "search", Net = 3 },
            };
            Assert.Throws<ArgumentException>(() => RewardMath.RunningMean(draws, e => e.Net));
            var arenas = new List<EpisodeRecord>
            {
                new EpisodeRecord { Generation = 0, Draw = 0, Arena = "search", Net = 1 },
                new EpisodeRecord { Generation = 0, Draw = 1, Arena = "exam", Net = 3 },
            };
            Assert.Throws<ArgumentException>(() => RewardMath.RunningMean(arenas, e => e.Net));
        }

        [Fact]
        public void NothingToAverageIsNaN()
        {
            Assert.True(double.IsNaN(RewardMath.RunningMean(new List<EpisodeRecord>(), e => e.Net)));
            Score s = RewardRegistry.Create("Net", J("{}")).Score(new List<EpisodeRecord>());
            Assert.True(double.IsNaN(s.Value));
            Assert.NotNull(s.Parts);
        }

        // ------------------------------------------------------------ the registry

        [Fact]
        public void TheRegistryListsTheRewardsSortedOrdinal()
        {
            Assert.Contains("Net", RewardRegistry.Names);
            Assert.Contains("WeightedSum", RewardRegistry.Names);
            Assert.Equal(RewardRegistry.Names.OrderBy(n => n, StringComparer.Ordinal).ToList(), RewardRegistry.Names.ToList());
        }

        [Fact]
        public void CreateBuildsTheRewardsAndRefusesBadRecipes()
        {
            Assert.IsType<Net>(RewardRegistry.Create("Net", J("{}")));
            var w = Assert.IsType<WeightedSum>(RewardRegistry.Create("WeightedSum", J("{\"weights\":{\"net\":1,\"path\":-0.5}}")));
            Assert.Equal(-0.5, w.Weights["path"]);

            var unknownName = Assert.Throws<ArgumentException>(() => RewardRegistry.Create("Nope", J("{}")));
            Assert.Contains("Net", unknownName.Message);

            var unknownKey = Assert.Throws<ArgumentException>(() => RewardRegistry.Create("Net", J("{\"x\":1}")));
            Assert.Contains("'x'", unknownKey.Message);
            Assert.Contains("Net", unknownKey.Message);

            var missing = Assert.Throws<ArgumentException>(() => RewardRegistry.Create("WeightedSum", J("{}")));
            Assert.Contains("weights", missing.Message);

            Assert.Throws<ArgumentException>(() => RewardRegistry.Create("WeightedSum", J("{\"weights\":{\"speed\":1}}")));
            Assert.Throws<ArgumentException>(() => RewardRegistry.Create("WeightedSum", J("{\"weights\":{}}")));
            Assert.Throws<ArgumentException>(() => RewardRegistry.Create("WeightedSum", J("{\"weights\":3}")));
            Assert.Throws<ArgumentException>(() => RewardRegistry.Create("WeightedSum", J("{\"weights\":{\"net\":\"one\"}}")));
            Assert.Throws<ArgumentException>(() => RewardRegistry.Create("Net", J("[]")));
        }

        [Fact]
        public void WeightedSumReadsTheMouthsCountAndTheRockingByName()
        {
            // Two generations: eaten {10} | {20, 60}; turn {0.5} | {1.5, 2.5}. The running means: 10 then (10 + 80) / 3 = 30, and 0.5
            // then (0.5 + 4) / 3 = 1.5.
            var eps = new List<EpisodeRecord>
            {
                new EpisodeRecord { Generation = 0, Draw = 0, Arena = "search", Eaten = 10, Turn = 0.5 },
                new EpisodeRecord { Generation = 1, Draw = 0, Arena = "search", Eaten = 20, Turn = 1.5 },
                new EpisodeRecord { Generation = 1, Draw = 1, Arena = "search", Eaten = 60, Turn = 2.5 },
            };

            IReward eaten = RewardRegistry.Create("WeightedSum", J("{\"weights\":{\"eaten\":1}}"));
            Assert.Equal(new[] { "eaten" }, eaten.Reads);
            Score s = eaten.Score(eps);
            Assert.Equal(30d, s.Value);
            Assert.Equal("eaten", Assert.Single(s.Parts).Key);

            IReward both = RewardRegistry.Create("WeightedSum", J("{\"weights\":{\"turn\":-2,\"eaten\":1}}"));
            Assert.Equal(new[] { "eaten", "turn" }, both.Reads);
            Score t = both.Score(eps);
            Assert.Equal(27d, t.Value);
            Assert.Equal(new[] { "eaten", "turn" }, t.Parts.Select(p => p.Key));
            Assert.Equal(-3d, t.Parts[1].Value);

            // The default reward does not read them.
            Assert.Equal(new[] { "net" }, RewardRegistry.Create("Net", J("{}")).Reads);
        }

        [Fact]
        public void WeightedSumRefusesANonFiniteWeight()
        {
            var w = new WeightedSum { Weights = new Dictionary<string, double> { { "net", double.NaN } } };
            Assert.Throws<ArgumentException>(() => w.Validate());
            w.Weights["net"] = double.PositiveInfinity;
            Assert.Throws<ArgumentException>(() => w.Validate());
        }

        [Fact]
        public void WeightedSumSumsInMeasureOrderNotInTheRecipesOrder()
        {
            // 0.1 + 0.2 + 0.3 differs from 0.3 + 0.2 + 0.1 in the last bit; the recipe's key order must not matter.
            var e = new EpisodeRecord { Generation = 0, Start = 0.1, End = 0.2, Net = 0.3 };
            IReward a = RewardRegistry.Create("WeightedSum", J("{\"weights\":{\"start\":1,\"end\":1,\"net\":1}}"));
            IReward b = RewardRegistry.Create("WeightedSum", J("{\"weights\":{\"net\":1,\"end\":1,\"start\":1}}"));
            double sum = 0; sum += 0.1; sum += 0.2; sum += 0.3;
            Assert.Equal(Bits(sum), Bits(a.Score(new[] { e }).Value));
            Assert.Equal(Bits(a.Score(new[] { e }).Value), Bits(b.Score(new[] { e }).Value));
        }

        // ------------------------------------------------------------ D178's net energy

        private static EpisodeRecord Swim(int generation, int draw, double eaten, double upkeep, double work, double handling, double diedAt = double.NaN) =>
            new EpisodeRecord
            {
                Generation = generation, Draw = draw, Seed = 1, Arena = "search", Body = 0, Seconds = 1800,
                Eaten = eaten, Upkeep = upkeep, Work = work, Handling = handling,
                Survived = double.IsNaN(diedAt), DiedAt = diedAt, Fresh = double.NaN, Turn = double.NaN,
            };

        private static NetEnergy Energy(bool toEnd) =>
            Assert.IsType<NetEnergy>(RewardRegistry.Create("NetEnergy", J(toEnd ? "{\"chargeDeathToEnd\":true}" : "{\"chargeDeathToEnd\":false}")));

        [Fact]
        public void NetEnergyIsEatenLessTheStandingCostTheWorkAndTheHandling()
        {
            Assert.Contains("NetEnergy", RewardRegistry.Names);
            EpisodeRecord e = Swim(0, 0, eaten: 400, upkeep: 380, work: 25, handling: 50);
            Assert.Equal(400d - 380d - 25d - 50d, Energy(true).Score(new[] { e }).Value);
            Score s = Energy(true).Score(new[] { e, Swim(0, 1, 300, 380, 15, 37.5) });
            Assert.Equal(((400d - 380d - 25d - 50d) + (300d - 380d - 15d - 37.5d)) / 2, s.Value);
            Assert.Equal(new[] { "eaten", "upkeep", "work", "handling" }, s.Parts.Select(kv => kv.Key));
            Assert.Equal(-380d, s.Parts[1].Value);
        }

        [Fact]
        public void NetEnergyChargesADeadBodyToTheEndSoThatDyingNeverPays()
        {
            // A body that eats less than it costs: 100 J eaten against 0.2 W standing. Living the whole 1,800 s it nets
            // 100 - 360 - 10 - 12.5; dying at 450 s, having paid a quarter of its upkeep, it would net 100 - 90 - 10 - 12.5 uncharged.
            EpisodeRecord lived = Swim(0, 0, 100, 360, 10, 12.5);
            EpisodeRecord died = Swim(0, 0, 100, 90, 10, 12.5, diedAt: 450);
            Assert.Equal(100d - 90d - 10d - 12.5d, Energy(false).Score(new[] { died }).Value);
            Assert.True(Energy(false).Score(new[] { died }).Value > Energy(false).Score(new[] { lived }).Value);
            Assert.Equal(360d, Energy(true).Standing(died));
            Assert.Equal(Energy(true).Score(new[] { lived }).Value, Energy(true).Score(new[] { died }).Value);
            // No upkeep paid before dying leaves no rate to carry on: NaN, which ranks last.
            Assert.True(double.IsNaN(Energy(true).Score(new[] { Swim(0, 0, 0, 0, 0, 0, diedAt: 0.5) }).Value));
        }

        [Fact]
        public void NetEnergyRequiresItsChargeAndReadsItsMeasures()
        {
            var missing = Assert.Throws<ArgumentException>(() => RewardRegistry.Create("NetEnergy", J("{}")));
            Assert.Contains("chargeDeathToEnd", missing.Message);
            Assert.Throws<ArgumentException>(() => RewardRegistry.Create("NetEnergy", J("{\"chargeDeathToEnd\":1}")));
            Assert.Equal(new[] { "eaten", "work", "upkeep", "handling", "diedAt" }, Energy(true).Reads);
        }

        private static EnergyReturn Return(bool toEnd) =>
            Assert.IsType<EnergyReturn>(RewardRegistry.Create("EnergyReturn", J(toEnd ? "{\"chargeDeathToEnd\":true}" : "{\"chargeDeathToEnd\":false}")));

        [Fact]
        public void EnergyReturnIsEatenOverWhatWasSpentAndIdlingScoresNothing()
        {
            Assert.Contains("EnergyReturn", RewardRegistry.Names);
            EpisodeRecord eater = Swim(0, 0, eaten: 400, upkeep: 300, work: 80, handling: 20);
            Assert.Equal(1.0, Return(true).Score(new[] { eater }).Value);
            EpisodeRecord idle = Swim(0, 0, eaten: 0, upkeep: 50, work: 0.5, handling: 0);
            Assert.Equal(0.0, Return(true).Score(new[] { idle }).Value);
            // NetEnergy ranks the idler above the eater that loses money; the return ranks them the other way.
            EpisodeRecord loser = Swim(0, 0, eaten: 200, upkeep: 300, work: 80, handling: 20);
            Assert.True(Energy(true).Score(new[] { idle }).Value > Energy(true).Score(new[] { loser }).Value);
            Assert.True(Return(true).Score(new[] { loser }).Value > Return(true).Score(new[] { idle }).Value);
            Score s = Return(true).Score(new[] { eater, Swim(0, 1, 200, 300, 80, 20) });
            Assert.Equal((1.0 + 0.5) / 2, s.Value);
            Assert.Equal(new[] { "eaten", "spent" }, s.Parts.Select(kv => kv.Key));
        }

        [Fact]
        public void EnergyReturnChargesADeadBodyToTheEnd()
        {
            EpisodeRecord died = Swim(0, 0, eaten: 100, upkeep: 90, work: 10, handling: 0, diedAt: 450);
            Assert.Equal(100.0 / 100.0, Return(false).Score(new[] { died }).Value);
            Assert.Equal(100.0 / 370.0, Return(true).Score(new[] { died }).Value);
            Assert.True(double.IsNaN(Return(false).Score(new[] { Swim(0, 0, 0, 0, 0, 0) }).Value));
            Assert.Contains("chargeDeathToEnd", Assert.Throws<ArgumentException>(() => RewardRegistry.Create("EnergyReturn", J("{}"))).Message);
        }
    }
}
