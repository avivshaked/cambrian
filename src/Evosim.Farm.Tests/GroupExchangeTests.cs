using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D159's exchange on its own, over Core worlds and no solver: the pick, the ring, the two
    /// barriers, the check that the worlds meet, and a world leaving the group.
    /// </summary>
    public class GroupExchangeTests
    {
        private const float Step = 0.5f;

        private static RunConfig Small() => new RunConfig
        {
            MinimumPopulation = 10,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            Light = new LightModel(64f, 12f),
        };

        /// <summary>A world stepped until its floor has founded.</summary>
        private static World Founded(ulong seed)
        {
            var world = new World(Small(), seed);
            for (int i = 0; i < 4; i++) world.Step(Step);
            Assert.True(world.Living.Count >= 5);
            return world;
        }

        /// <summary>Each world on a thread of its own through one exchange at its clock; what each Run returned.</summary>
        private static bool[] ExchangeOnce(GroupExchange exchange, World[] worlds, double at,
            List<string>[] records = null)
        {
            var results = new bool[worlds.Length];
            var errors = new Exception[worlds.Length];
            var threads = new Thread[worlds.Length];

            for (int k = 0; k < worlds.Length; k++)
            {
                int place = k;
                threads[k] = new Thread(() =>
                {
                    try
                    {
                        results[place] = exchange.Run(
                            place, worlds[place], at,
                            records == null ? null : (from, m, id) =>
                                records[place].Add(ImmigrantRecord.Row(
                                    worlds[place].ElapsedSeconds, from, m, id, worlds[place].LastImmigrantRefusal,
                                    id >= 0 ? ImmigrantRecord.GuildOf(worlds[place], id) : -1)));
                    }
                    catch (Exception e)
                    {
                        errors[place] = e;
                        exchange.Leave(place, "error");
                    }
                });
            }

            foreach (Thread t in threads) t.Start();
            foreach (Thread t in threads) Assert.True(t.Join(TimeSpan.FromSeconds(30)), "a world was left waiting at the exchange");

            foreach (Exception e in errors)
            {
                if (e != null) throw new AggregateException(e);
            }

            return results;
        }

        [Fact]
        public void ThePickIsDistinctLivingBodiesAndTheSameForTheSameExchange()
        {
            World world = Founded(3);

            GroupExchange.Migrant[] a = GroupExchange.Pick(world, k: 4, count: 3);
            GroupExchange.Migrant[] b = GroupExchange.Pick(world, k: 4, count: 3);
            GroupExchange.Migrant[] c = GroupExchange.Pick(world, k: 5, count: 3);

            Assert.Equal(3, a.Length);
            Assert.Equal(a.Select(m => m.Id), b.Select(m => m.Id));
            Assert.Equal(3, a.Select(m => m.Id).Distinct().Count());
            Assert.All(a, m => Assert.Contains(world.Living, o => o.Id == m.Id));

            // A copy, never the living body's own genome.
            Assert.All(a, m => Assert.DoesNotContain(world.Living, o => ReferenceEquals(o.Genome, m.Genome)));

            // Another exchange draws another stream; over five bodies of ten or more, the same
            // three ids in the same order would be a stream that does not move.
            Assert.NotEqual(a.Select(m => m.Id), c.Select(m => m.Id));

            // Fewer alive than asked: all of them.
            Assert.Equal(world.Living.Count, GroupExchange.Pick(world, 1, world.Living.Count + 5).Length);
        }

        [Fact]
        public void EachWorldPlantsWhatTheWorldBeforeItSent()
        {
            World[] worlds = { Founded(1), Founded(2), Founded(3) };
            foreach (World w in worlds) w.DrainLineageEvents();

            var exchange = new GroupExchange(new[] { "a", "b", "c" }, every: 2d, count: 2);
            var records = new[] { new List<string>(), new List<string>(), new List<string>() };

            GroupExchange.Migrant[][] sent = worlds.Select((w, k) => GroupExchange.Pick(w, 1, 2)).ToArray();
            Assert.All(ExchangeOnce(exchange, worlds, at: 2d, records), Assert.True);

            for (int k = 0; k < 3; k++)
            {
                int from = (k + 2) % 3;
                Assert.Equal(2L, worlds[k].Immigrated);

                List<LineageEvent> rows = worlds[k].DrainLineageEvents()
                    .Where(e => e.Kind == LineageEventKind.Birth && e.BirthKind == BirthKind.Immigration).ToList();

                Assert.Equal(2, rows.Count);
                Assert.All(rows, r => Assert.Equal(from, r.ImmigrantFromWorld));
                Assert.Equal(sent[from].Select(m => m.Id), rows.Select(r => r.ImmigrantFromId));
                Assert.Equal(2, records[k].Count);
                Assert.Contains("\"from\":" + from, records[k][0]);
            }
        }

        [Fact]
        public void TheSameWorldsExchangeTheSameWay()
        {
            List<string>[] Once()
            {
                World[] worlds = { Founded(1), Founded(2), Founded(3) };
                var exchange = new GroupExchange(new[] { "a", "b", "c" }, every: 2d, count: 3);
                var records = new[] { new List<string>(), new List<string>(), new List<string>() };

                ExchangeOnce(exchange, worlds, at: 2d, records);
                for (int i = 0; i < 4; i++) foreach (World w in worlds) w.Step(Step);
                ExchangeOnce(exchange, worlds, at: 4d, records);

                return records;
            }

            List<string>[] first = Once(), second = Once();
            for (int k = 0; k < 3; k++) Assert.Equal(first[k], second[k]);
        }

        [Fact]
        public void WorldsAtDifferentSecondsAreRefused()
        {
            World[] worlds = { Founded(1), Founded(2) };
            worlds[1].Step(Step);

            var exchange = new GroupExchange(new[] { "a", "b" }, every: 2d, count: 1);
            var e = Assert.Throws<AggregateException>(() => ExchangeOnce(exchange, worlds, at: 2d));
            Assert.Contains("metabolic step", e.InnerException.Message);
        }

        /// <summary>
        /// A world that leaves for anything but the budget ends the group, and a world waiting at
        /// the exchange for it is released with false rather than held.
        /// </summary>
        [Fact]
        public void AWorldThatLeavesEndsTheGroupAndReleasesTheOthers()
        {
            World[] worlds = { Founded(1), Founded(2), Founded(3) };
            var exchange = new GroupExchange(new[] { "a", "b", "c" }, every: 2d, count: 1);

            var results = new bool[2];
            var waiting = new[]
            {
                new Thread(() => results[0] = exchange.Run(1, worlds[1], 2d)),
                new Thread(() => results[1] = exchange.Run(2, worlds[2], 2d)),
            };

            foreach (Thread t in waiting) t.Start();
            Thread.Sleep(200);
            exchange.Leave(0, "wall");

            foreach (Thread t in waiting) Assert.True(t.Join(TimeSpan.FromSeconds(30)), "a world was held at the exchange");

            Assert.False(results[0]);
            Assert.False(results[1]);
            Assert.Equal("a ended (wall)", exchange.EndedBy);
            Assert.Equal(0L, worlds[1].Immigrated + worlds[2].Immigrated);

            // A second word from the same world changes nothing.
            exchange.Leave(0, "error");
            Assert.Equal("a ended (wall)", exchange.EndedBy);
        }

        [Fact]
        public void LeavingAtTheBudgetDoesNotEndTheGroup()
        {
            var exchange = new GroupExchange(new[] { "a", "b" }, every: 2d, count: 1);
            exchange.Leave(0, "budget");
            Assert.Null(exchange.EndedBy);
        }

        [Fact]
        public void TheNextExchangeComesFromTheClock()
        {
            var exchange = new GroupExchange(new[] { "a", "b" }, every: 10d, count: 1);
            Assert.Equal(10d, exchange.NextAfter(0d));
            Assert.Equal(10d, exchange.NextAfter(9.5d));
            Assert.Equal(20d, exchange.NextAfter(10d));
            Assert.Equal(20d, exchange.NextAfter(10d - 1e-12));
        }
    }
}
