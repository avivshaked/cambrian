using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// <see cref="GateCensus"/> on a world small enough to record in a test: its rows name every
    /// living body, carry Core's own gate, and the mode refuses a second the run has no checkpoint
    /// for (<c>logbook/specs/breeding-urge/spec.md</c>, measure 1).
    /// </summary>
    /// <remarks>
    /// <b>One recording for the class</b>, as <see cref="FilmWindowTests"/> makes one: sixty
    /// seconds with a checkpoint every twenty, recorded by <see cref="Program.Main"/> itself. The
    /// tests never read <c>runs/</c>.
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public class GateCensusTests : IClassFixture<GateCensusTests.RecordedRun>
    {
        private static readonly EnvBinding.Lookup NoEnvironment = name => null;

        private readonly RecordedRun _run;

        public GateCensusTests(RecordedRun run) => _run = run;

        /// <summary>A sixty second run of the small world, recorded once for the class.</summary>
        public sealed class RecordedRun : IDisposable
        {
            public string Root { get; }
            public string RunDirectory { get; }

            public RecordedRun()
            {
                Root = Path.Combine(AppContext.BaseDirectory, "gate-census-tests", Guid.NewGuid().ToString("N"));
                string runsRoot = Path.Combine(Root, "runs");

                int exit = Program.Main(FilmWindowTests.SmallRun(runsRoot, "gated", seconds: 60, checkpointEvery: 20));
                if (exit != 0) throw new InvalidOperationException("The recording exited " + exit + ".");

                RunDirectory = Directory.GetDirectories(Path.Combine(runsRoot, "gated"))[0];
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

        private string Out(string name) => Path.Combine(_run.Root, "out", name);

        private static List<JsonNode> Parse(string file) =>
            File.ReadAllLines(file).Where(l => l.Length > 0).Select(Json.Parse).ToList();

        /// <summary>The run's checkpoint at <paramref name="second"/>, restored the way the census restores it.</summary>
        private (World World, Simulation Sim, RunConfig Config) Restore(double second)
        {
            CensusRead read = CensusRead.Open(
                "gate-census-test", GateCensus.Usage, _run.RunDirectory, Out("unused.jsonl"), NoEnvironment, TextWriter.Null);
            CensusRead.Found found = read.Find(second, TextWriter.Null);

            Parallelism.Threads = 1;

            var world = new World(read.Config, found.Header.Seed, read.Pool?.Genomes, read.Pool?.Weights) { QueueAdmittedGenomes = true };
            var sim = new Simulation(world, found.Header.Seed, found.PhysicsDt, found.StepsPerMetabolic, 1, runDirectory: null);
            var sampler = new Sampler { PoolNamed = read.Config.FoundingTricklePoolCount > 0, Migrating = read.Migrating };
            Program.ReadCheckpoint(found.Path, world, sim, sampler);
            return (world, sim, read.Config);
        }

        /// <summary>A float written with "R" and read back as a double is the float to within a float's own spacing.</summary>
        private static void AssertSingle(float expected, double read) =>
            Assert.Equal(expected, (float)read);

        [Fact]
        public void RowsNameEveryLivingBodyAndCarryCoresGate()
        {
            string file = Out("rows.jsonl");
            GateCensus.Measure(_run.RunDirectory, new[] { 20d, 40d }, file, NoEnvironment, TextWriter.Null);

            List<JsonNode> rows = Parse(file);

            foreach (double second in new[] { 20d, 40d })
            {
                var restored = Restore(second);

                using (restored.Sim)
                {
                    List<Organism> living = restored.World.Living.Where(o => !o.IsRemains).ToList();
                    Assert.NotEmpty(living);

                    List<JsonNode> atSecond = rows.Where(r => r["t"].AsDouble() == second).ToList();
                    Assert.Equal(
                        living.Select(o => o.Id).OrderBy(i => i).ToList(),
                        atSecond.Select(r => (long)r["id"].AsDouble()).OrderBy(i => i).ToList());

                    foreach (Organism organism in living)
                    {
                        JsonNode row = atSecond.Single(r => (long)r["id"].AsDouble() == organism.Id);

                        Assert.Equal(organism.BreedingGate(restored.Config), row["gate"].AsDouble());
                        Assert.Equal(organism.Energy, row["reserve"].AsDouble());
                        Assert.Equal(organism.Gestates ? "gestating" : "lump", row["mode"].AsString());
                        Assert.Equal(organism.Gestates ? organism.GestationJoules : 0d, row["account"].AsDouble());
                        Assert.Equal(Metabolism.IsPlant(organism.Phenotype), row["plant"].AsBool());
                        AssertSingle(organism.StandingWatts, row["standingWatts"].AsDouble());
                        AssertSingle(organism.Age, row["age"].AsDouble());
                        Assert.Equal(organism.TissueJoules, row["tissue"].AsDouble());
                    }
                }
            }
        }

        [Fact]
        public void AnAdultIsOneThatGrowthAndTheAssayDoNotRefuse()
        {
            string file = Out("adult.jsonl");
            GateCensus.Measure(_run.RunDirectory, new[] { 40d }, file, NoEnvironment, TextWriter.Null);

            foreach (JsonNode row in Parse(file))
            {
                Assert.Equal(row["grown"].AsBool() && !row["births_held"].AsBool(), row["adult"].AsBool());
            }
        }

        [Fact]
        public void ASecondWithNoCheckpointIsRefused()
        {
            // The recording holds checkpoints at 20, 40 and 60 s and nothing at 30.
            string file = Out("none.jsonl");

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() =>
                GateCensus.Measure(_run.RunDirectory, new[] { 30d }, file, NoEnvironment, TextWriter.Null));

            Assert.Contains("no checkpoint at 30", refused.Message);
            Assert.False(File.Exists(file));
            Assert.Equal(1, GateCensus.Run(new[] { "--gate-census", _run.RunDirectory, "30", file }));
        }

        [Fact]
        public void AnOutFileInsideTheRunIsRefused()
        {
            string inside = Path.Combine(_run.RunDirectory, "gate.jsonl");

            Assert.Throws<ArgumentException>(() =>
                GateCensus.Measure(_run.RunDirectory, new[] { 20d }, inside, NoEnvironment, TextWriter.Null));
            Assert.False(File.Exists(inside));
        }
    }
}
