using System;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The contact grid's reach, through the farm (<c>logbook/specs/r57-build/build-brief.md</c>):
    /// a body whose sphere strays out of the grid's reach inside a physics step is killed there as
    /// a counted divergence, with its dump and a reason naming the sphere, at that step and at any
    /// thread count, and the run goes on. Round 56 seed 2 ended in an IndexOutOfRangeException on
    /// such a body instead (body 86464 at 28,179.5 s, 2026-10-05).
    /// </summary>
    public sealed class ContactStrayKillTests
    {
        private readonly ITestOutputHelper _out;

        public ContactStrayKillTests(ITestOutputHelper output) => _out = output;

        private sealed class Outcome
        {
            public long Victim;
            public long DivergedBefore, DivergedAfter;
            public int Living;
            public ulong Digest;
            public string Dump;
        }

        private static Outcome Run(int threads)
        {
            string scratch = SimulationTests.Scratch("contact-stray-" + threads);
            RunConfig config = SimulationTests.SmallWorld();
            var world = new World(config, 1UL);
            RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow);

            using (dir)
            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: threads, runDirectory: dir.Path))
            {
                for (int i = 0; i < 300; i++) sim.Step();

                Assert.True(sim.Dynamics.Creatures.Count >= 3, "too few bodies to pick a stray from");

                // Thrown 1e12 m along x between two steps, as a body that blew up between two
                // divergence checks would be.
                Creature victim = sim.Dynamics.Creatures[1];
                var outcome = new Outcome { Victim = victim.Id, DivergedBefore = world.Diverged };
                victim.PlaceAt(new Vec3(1e12, victim.Position[1], victim.Position[2]), QuatD.Identity);

                sim.Step();

                outcome.DivergedAfter = world.Diverged;
                Assert.DoesNotContain(world.Living, o => o.Id == outcome.Victim);
                Assert.DoesNotContain(sim.Dynamics.Creatures, c => c.Id == outcome.Victim);

                string dump = Path.Combine(dir.Path, "diverged", outcome.Victim + ".json");
                Assert.True(File.Exists(dump), "no divergence dump at " + dump);
                outcome.Dump = File.ReadAllText(dump);

                // And on.
                for (int i = 0; i < 300; i++) sim.Step();

                outcome.Living = world.Living.Count;
                outcome.Digest = sim.Dynamics.Digest();
                return outcome;
            }
        }

        [Fact]
        public void AStrayBodyIsKilledAsDivergedAtThatStepAtOneAndFourThreads()
        {
            Outcome one = Run(1);
            Outcome four = Run(4);

            _out.WriteLine($"victim {one.Victim}: diverged {one.DivergedBefore} -> {one.DivergedAfter}; " +
                           $"{one.Living} living after; digest {one.Digest:x16} / {four.Digest:x16}");

            Assert.Equal(one.DivergedBefore + 1, one.DivergedAfter);
            Assert.Equal(four.DivergedBefore + 1, four.DivergedAfter);
            Assert.Contains("out of the contact grid's reach", one.Dump);
            Assert.Contains("1000000000000", one.Dump);

            Assert.Equal(one.Victim, four.Victim);
            Assert.Equal(one.Living, four.Living);
            Assert.Equal(one.Digest, four.Digest);
        }
    }
}
