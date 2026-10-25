using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D235's nose on the farm: every body is handed the world as its sniff source and its own id,
    /// a nose world resumes from a checkpoint exactly, the header says the nose is on, and the card
    /// refuses a nose world until its kernels carry one.
    /// </summary>
    /// <remarks>
    /// <b>A small lit box whose founders carry noses and leaves, under the one brain and the leaf
    /// smell's direction form</b>, so a nose's noisy reading reaches the joints. GripCheckpointTests'
    /// box, otherwise.
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public class NoseFarmTests
    {
        private readonly ITestOutputHelper _out;

        public NoseFarmTests(ITestOutputHelper output) => _out = output;

        private const int StepsPerMetabolic = 25;

        private static RunConfig SmellingWorld()
        {
            RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>
            {
                { "EVOSIM_AREA", "25" },
                { "EVOSIM_DEPTH", "5" },
                { "EVOSIM_FOUNDER_DEPTH", "5" },
                { "EVOSIM_PATCHES", "1" },
                { "EVOSIM_SHARED_SPACE", "1" },
                { "EVOSIM_FIELD", "grid" },
                { "EVOSIM_FIELD_CELL", "1" },
                { "EVOSIM_FIELD_MATTER_CELL", "5" },
                { "EVOSIM_IRRADIANCE", "200" },
                { "EVOSIM_MATTER_BUDGET", "300" },
                { "EVOSIM_TISSUE_ENERGY", "500" },
                { "EVOSIM_RHO", "100" },
                { "EVOSIM_OVERHEAD", "100" },
                { "EVOSIM_DT", "0.02" },
                { "EVOSIM_LEAF_SCENT", "direction" },
                { "EVOSIM_BRAIN_MODEL", "central" },
                { "EVOSIM_NEURON_COST", "0" },
                { "EVOSIM_CONNECTION_COST", "0" },
            })));

            config.CellTypes = config.CellTypes.Appending(new NoseCell(leafNoise: 0.2f));
            config.Genome.FounderCellTypes = new[]
            {
                CellTypeIds.Nose, CellTypeIds.Link, CellTypeIds.Photosynthetic,
            };

            return config;
        }

        private sealed class Pair : IDisposable
        {
            public World World;
            public Simulation Sim;

            public void Dispose() => Sim?.Dispose();
        }

        private static Pair Found()
        {
            var world = new World(SmellingWorld(), 3UL);
            return new Pair
            {
                World = world,
                Sim = new Simulation(world, 3UL, 0.02f, StepsPerMetabolic, threads: 2, runDirectory: null),
            };
        }

        private static byte[] Checkpoint(Pair pair)
        {
            pair.Sim.SettleBeforeCheckpoint();

            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    pair.World.WriteState(w);
                    pair.Sim.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        private static Pair Restore(byte[] state)
        {
            Pair pair = Found();

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                pair.World.ReadState(r);
                pair.Sim.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            return pair;
        }

        private static int Noses(Simulation sim)
        {
            int noses = 0;
            foreach (Creature body in sim.Dynamics.Creatures)
            {
                foreach (PhenotypePart part in body.Phenotype.Parts)
                {
                    if (part.CellTypeId == CellTypeIds.Nose) noses++;
                }
            }

            return noses;
        }

        [Fact]
        public void EveryBodyIsHandedTheWorldAndItsOwnIdAndOnlyNosesCarrySmellPorts()
        {
            using (Pair live = Found())
            {
                // A metabolic step at a time until some body carries a nose: the founders arrive
                // two at a time (6 s held three noses on the first run of this test).
                for (int m = 0; m < 40 && Noses(live.Sim) == 0; m++)
                {
                    for (int i = 0; i < StepsPerMetabolic; i++) live.Sim.Step();
                }

                Assert.True(Noses(live.Sim) > 0, "no founder grew a nose in 40 metabolic steps");
                foreach (Creature body in live.Sim.Dynamics.Creatures)
                {
                    Assert.Same(live.World, body.Senses.Sniffs);
                    Assert.Equal((long)body.Id, body.Senses.BodyId);

                    foreach (BrainPort port in CentralWiring.Ports(body.Phenotype, live.World.Config.SensorPool()))
                    {
                        if (port.Channel.IsSmell()) Assert.Equal(CellTypeIds.Nose, body.Phenotype.Parts[port.Part].CellTypeId);
                    }
                }
            }
        }

        [Fact]
        public void ANoseWorldResumesFromACheckpointAsItRanOn()
        {
            using (Pair live = Found())
            {
                for (int i = 0; i < 6 * StepsPerMetabolic; i++) live.Sim.Step();
                _out.WriteLine($"{live.World.Living.Count} living, {Noses(live.Sim)} noses at the checkpoint");
                Assert.True(Noses(live.Sim) > 0, "no body carries a nose at the checkpoint");

                byte[] state = Checkpoint(live);

                using (Pair resumed = Restore(state))
                {
                    // Four metabolic steps, so four sniffs, each the run's own.
                    for (int i = 0; i < 4 * StepsPerMetabolic; i++)
                    {
                        live.Sim.Step();
                        resumed.Sim.Step();
                        Assert.Equal(live.Sim.Dynamics.Digest(), resumed.Sim.Dynamics.Digest());
                    }

                    Assert.Equal(Checkpoint(live), Checkpoint(resumed));
                }
            }
        }

        [Fact]
        public void TheHeaderSaysTheNoseIsOnAndTheCardRefusesIt()
        {
            RunConfig config = SmellingWorld();
            string token = Report.RoundFiftyNineToken(config);
            _out.WriteLine(token);
            Assert.Contains("nose noise leaf 0.2/1", token);
            Assert.Contains("facing off", token);

            // As a tank, so the box's own refusal (its seams) does not answer first.
            SolverConfig solver = SolverConfig.From(config, 0.02);
            solver.TankRadiusMetres = 5.0;
            Assert.NotNull(solver.Nose);
            var refused = Assert.Throws<NotSupportedException>(() => GpuWorld.Refuse(solver));
            Assert.Contains("nose", refused.Message);

            // Without the nose in the table, the card's refusal is not the nose's.
            config.CellTypes = CellTypeRegistry.Standard;
            SolverConfig plain = SolverConfig.From(config, 0.02);
            plain.TankRadiusMetres = 5.0;
            Assert.Null(plain.Nose);
            try
            {
                GpuWorld.Refuse(plain);
            }
            catch (NotSupportedException other)
            {
                Assert.DoesNotContain("nose", other.Message);
            }
        }
    }
}
