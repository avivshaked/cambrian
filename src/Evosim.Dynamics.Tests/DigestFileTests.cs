using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Evosim.Dynamics.Placement;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Package F's gate: <c>digest.jsonl</c> identical at 1, 8 and 24 threads over 2,000 steps
    /// of a world that is gaining and losing bodies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Births and deaths are the part that was not tested before.</b> The spike's identity
    /// test steps a frozen population, and a frozen population cannot show the one thing a farm
    /// loop does that breaks identity: changing the list. Every sum across bodies is taken in
    /// list order, so the trajectory depends on that order — and it stays a property of the
    /// population rather than of its history only while the list is kept in ascending id order.
    /// This adds and removes bodies from the middle of the list on a schedule driven by the step
    /// number, so the three runs see the same population at the same step and can only disagree
    /// about the arithmetic.
    /// </para>
    /// <para>
    /// <b>The files are written under <c>scratch/</c></b>, never into a run directory.
    /// </para>
    /// </remarks>
    public sealed class DigestFileTests
    {
        private static readonly string Scratch =
            @"D:\Projects\experiments\evolution-simulator\scratch\farm-port\contacts\digest";

        private const int Steps = 2000;
        private const int Initial = 200;

        private readonly ITestOutputHelper _out;

        public DigestFileTests(ITestOutputHelper output) => _out = output;

        [Fact]
        public void TheDigestFileIsTheSameAtOneEightAndTwentyFourThreads()
        {
            Assert.True(RunFixture.Present, "round 42 seed 4 is not on this machine");

            string one = Run(1);
            string eight = Run(8);
            string many = Run(24);

            _out.WriteLine($"rows: {File.ReadAllLines(one).Length}");
            _out.WriteLine(File.ReadAllLines(one)[0]);
            _out.WriteLine(File.ReadAllLines(one)[File.ReadAllLines(one).Length - 1]);

            Assert.Equal(File.ReadAllText(one), File.ReadAllText(eight));
            Assert.Equal(File.ReadAllText(one), File.ReadAllText(many));

            Assert.Equal(
                File.ReadAllText(Path.Combine(Path.GetDirectoryName(one), "digest-bodies.jsonl")),
                File.ReadAllText(Path.Combine(Path.GetDirectoryName(many), "digest-bodies.jsonl")));

            string settings = File.ReadAllText(
                Path.Combine(Path.GetDirectoryName(one), "digest-settings.json"));

            _out.WriteLine(settings);
            Assert.Contains("\"precision\": \"double\"", settings);
        }

        /// <summary>
        /// One run of the world at a thread count, into its own directory.
        /// </summary>
        private static string Run(int threads)
        {
            string directory = Path.Combine(Scratch, threads.ToString());
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);

            SolverConfig solver = RunFixture.Solver();
            var world = new DynamicsWorld(solver) { Threads = threads };
            var floor = new PlacementFloor(RunFixture.Config.WorldDepthMetres, solver.Bed);

            // Even ids at the start, so a later birth can land between two of them and exercise
            // the insert rather than only the append the bench does.
            for (int i = 0; i < Initial; i++) world.AddInIdOrder(Newborn(solver, floor, 2 * i));

            world.EnableDigest(directory, everySteps: 50, dumpSteps: new long[] { 1, 1000, Steps });

            for (int step = 1; step <= Steps; step++)
            {
                world.Step();

                if (step % 40 != 0) continue;

                int k = step / 40;

                world.Remove(2 * ((k * 7) % Initial));

                int id = 2 * ((k * 13) % Initial) + 1;
                if (world.ById(id) == null) world.AddInIdOrder(Newborn(solver, floor, id));
            }

            world.CloseDigest();
            return Path.Combine(directory, "digest.jsonl");
        }

        /// <summary>
        /// One body, placed from its own id alone — so the three runs put the same animal in the
        /// same place whatever order the births happened to arrive in.
        /// </summary>
        private static Creature Newborn(SolverConfig solver, PlacementFloor floor, int id)
        {
            List<Phenotype> pool = RunFixture.Bodies;
            var body = new Creature(id, pool[id % pool.Count], solver, RunFixture.Config.Shapes);

            var rng = new Rng((ulong)(id + 1) * 2654435761UL);

            double reach = RunFixture.TankRadius - 3.0;
            double radius = reach * System.Math.Sqrt(rng.NextFloat());
            double theta = 2.0 * System.Math.PI * rng.NextFloat();

            double x = RunFixture.TankRadius + radius * System.Math.Cos(theta);
            double z = RunFixture.TankRadius + radius * System.Math.Sin(theta);

            var turn = QuatD.FromAxisAngle(
                new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5)
                    .Normalized,
                rng.NextFloat() * 2.0 * System.Math.PI);

            body.PlaceAt(new Vec3(x, 0, z), turn);

            double y = floor.MinimumPlacementY((float)x, (float)z, (float)body.ContactRadius) +
                       0.5 * rng.NextFloat();

            body.PlaceAt(new Vec3(x, y, z), turn);
            body.EnableTrace();

            return body;
        }

        /// <summary>
        /// The list refuses two bodies with one id, and the instrument refuses a list that is not
        /// in ascending id order — the rule that keeps identity, stated where it is broken.
        /// </summary>
        [Fact]
        public void TheListKeepsItsOrderAndRefusesADuplicate()
        {
            SolverConfig config = TestBodies.Water();
            config.ContactInstrument = true;

            var world = new DynamicsWorld(config);

            foreach (int id in new[] { 5, 1, 9, 3 })
            {
                world.AddInIdOrder(TestBodies.Build(TestBodies.Box(new Float3(0.1f, 0.1f, 0.1f)), config, id));
            }

            var order = new List<int>();
            foreach (Creature body in world.Creatures) order.Add(body.Id);
            Assert.Equal(new List<int> { 1, 3, 5, 9 }, order);

            Assert.Throws<System.ArgumentException>(() =>
                world.AddInIdOrder(TestBodies.Build(TestBodies.Box(new Float3(0.1f, 0.1f, 0.1f)), config, 5)));

            Assert.True(world.Remove(5));
            Assert.False(world.Remove(5));
            Assert.Equal(3, world.Creatures.Count);

            // Appended out of order through the old Add, which the bench and the joint tests
            // still use: the instrument says so rather than counting the wrong pairs.
            world.Add(TestBodies.Build(TestBodies.Box(new Float3(0.1f, 0.1f, 0.1f)), config, 2));
            Assert.Throws<System.InvalidOperationException>(() => world.Step());
        }
    }
}
