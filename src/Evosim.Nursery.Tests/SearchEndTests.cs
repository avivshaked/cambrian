using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Nursery;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// D172's pure parts (SearchEnd.cs): the stop on a stalled curve of eaten, smoothed over 5 generations, no more than 5% gained in
    /// 10, never before generation 20; and the exam's pool of every generation's top and the last elites, each genome once.
    /// </summary>
    public class SearchEndTests
    {
        private static SearchEnd.Verdict Judge(IEnumerable<double> curve) => SearchEnd.Judge(curve.ToList(), 5, 0.05, 10, 20);

        private static IEnumerable<double> Flat(int generations, double value) => Enumerable.Repeat(value, generations);

        [Fact]
        public void TheSmoothedCurveIsTheMeanOfTheLastWindow()
        {
            var curve = new List<double> { 1, 2, 3, 4, 5, 6 };
            Assert.True(double.IsNaN(SearchEnd.Smoothed(curve, 3, 5)));
            Assert.Equal(3.0, SearchEnd.Smoothed(curve, 4, 5));
            Assert.Equal(4.0, SearchEnd.Smoothed(curve, 5, 5));
            Assert.True(double.IsNaN(SearchEnd.Smoothed(curve, 6, 5)));
        }

        [Fact]
        public void AFlatCurveStopsAtTheFloorAndNotBefore()
        {
            Assert.False(Judge(Flat(20, 100)).Stop);   // generation 19
            SearchEnd.Verdict v = Judge(Flat(21, 100)); // generation 20
            Assert.True(v.Stop);
            Assert.Equal(100.0, v.Smoothed);
            Assert.Equal(0.0, v.Gain);
        }

        [Fact]
        public void ACurveStillClimbingDoesNotStop()
        {
            // 1% a generation is about 10.5% in ten.
            var curve = Enumerable.Range(0, 61).Select(g => 100 * Math.Pow(1.01, g)).ToList();
            for (int n = 21; n <= curve.Count; n++) Assert.False(Judge(curve.Take(n)).Stop);
        }

        [Fact]
        public void AGainOfExactlyTheShareStopsAndAboveItDoesNot()
        {
            // Smoothed 100 at generation 10 and 105 at generation 20: flat at 100 to 16, then 105 from 16 on (generations 16 to 20).
            var at = Flat(16, 100).Concat(Flat(5, 105)).ToList();
            SearchEnd.Verdict v = Judge(at);
            Assert.Equal(0.05, v.Gain);
            Assert.True(v.Stop);
            var above = Flat(16, 100).Concat(Flat(5, 105.5)).ToList();
            Assert.False(Judge(above).Stop);
        }

        [Fact]
        public void AStallThenAJumpJustBeforeTheFloorKeepsTheSearchGoing()
        {
            // Flat to generation 14, then a jump from 15: at the floor the smoothed curve has doubled in ten generations.
            var curve = Flat(15, 100).Concat(Flat(6, 200)).ToList();
            Assert.False(Judge(curve).Stop);
        }

        [Fact]
        public void NoShareFromZeroOrAMissingGeneration()
        {
            Assert.False(Judge(Flat(21, 0)).Stop);
            Assert.True(double.IsNaN(Judge(Flat(21, 0)).Gain));
            var missing = Flat(21, 100).ToList();
            missing[18] = double.NaN;
            Assert.False(Judge(missing).Stop);
        }

        [Fact]
        public void AFallingCurveStops()
        {
            var curve = Enumerable.Range(0, 21).Select(g => 200.0 - g).ToList();
            SearchEnd.Verdict v = Judge(curve);
            Assert.True(v.Gain < 0);
            Assert.True(v.Stop);
        }

        [Fact]
        public void BelowZeroTheShareIsOfTheBasesSize()
        {
            // D178: a reward net of costs can stand below 0. Smoothed -200 at generation 10 and -190 at generation 20 is a gain of 5%
            // of 200, which stops; -189 is above it and does not.
            var at = Flat(16, -200).Concat(Flat(5, -190)).ToList();
            SearchEnd.Verdict v = Judge(at);
            Assert.Equal(0.05, v.Gain, 12);
            Assert.True(v.Stop);
            Assert.False(Judge(Flat(16, -200).Concat(Flat(5, -189))).Stop);
            // A flat curve below 0 has stalled; one falling further below it gains less than nothing.
            Assert.True(Judge(Flat(21, -300)).Stop);
            Assert.True(Judge(Enumerable.Range(0, 21).Select(g => -100.0 - g)).Gain < 0);
        }

        [Fact]
        public void ACurveClimbingThroughZeroDoesNotStop()
        {
            // From -300 to +100 in 20 generations: every share once a window has swum is a gain far above 5%.
            var curve = Enumerable.Range(0, 41).Select(g => -300.0 + 20 * g).ToList();
            for (int n = 21; n <= curve.Count; n++) Assert.False(Judge(curve.Take(n)).Stop);
        }

        [Fact]
        public void TheExamPoolTakesEachGenomeOnceInTheOrderItCame()
        {
            var tops = new List<(string, string, int)> { ("A", "g0-m3", 0), ("B", "g1-m0", 0), ("B", "g2-m0", 0), ("C", "g3-m5", 0) };
            var elites = new List<(string, string, int)> { ("C", "g3-m0", 0), ("D", "g3-m1", 0), ("A", "g3-m2", 0) };
            List<SearchEnd.Entry> pool = SearchEnd.ExamPool(tops, elites);
            Assert.Equal(new[] { "A", "B", "C", "D" }, pool.Select(e => e.Text));
            Assert.Equal(new[] { "g0-m3", "g1-m0", "g3-m5", "g3-m1" }, pool.Select(e => e.Id));
            Assert.Equal(new[] { 0 }, pool[0].Tops);
            Assert.Equal(new[] { 1, 2 }, pool[1].Tops);
            Assert.Equal(new[] { 3 }, pool[2].Tops);
            Assert.Empty(pool[3].Tops);
            Assert.Equal(new[] { true, false, true, true }, pool.Select(e => e.Elite));
            Assert.Equal(new[] { "g0-m3", "g3-m2" }, pool[0].Ids);
            Assert.Equal(new[] { "g1-m0", "g2-m0" }, pool[1].Ids);
            Assert.Equal(new[] { "g3-m5", "g3-m0" }, pool[2].Ids);
        }

        /// <summary>The loop cannot run without swimming, so a test that reads the source says it uses these, as TankLogTests does.</summary>
        [Fact]
        public void TheLoopStopsOnTheRuleAndExaminesTheTops()
        {
            string root = Recipe.RepoRootAbove(AppContext.BaseDirectory);
            string tank = File.ReadAllText(Path.Combine(root, "src", "Evosim.Nursery", "Tank.cs"));
            Assert.Contains("SearchEnd.Judge(curve, _o.StopWindow, _o.StopGain, _o.StopOver, _o.StopFloor)", tank);
            Assert.Contains("if (gen >= _o.Generations || stalled)", tank);
            Assert.Contains("if (_o.ExamPool == \"tops\") return ExamOfTops(tops, population, size);", tank);
            Assert.Contains("SearchEnd.ExamPool(tops, ", tank);
            // D178: the stop reads the reward, each episode scored alone, when the recipe says so, and eaten otherwise.
            Assert.Contains("curve.Add(_o.StopMeasure == \"reward\" ? MeanOf(p => p.GenerationReward) : Mean(p => p.Eaten));", tank);
            Assert.Contains("p.GenerationReward = p.Records.Skip(before).Average(e => _reward.Score(new[] { e }).Value);", tank);
        }
    }
}
