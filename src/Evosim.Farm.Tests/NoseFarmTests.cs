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

        private static RunConfig SmellingWorld(float leafNoise = 0.2f)
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

            config.CellTypes = config.CellTypes.Appending(new NoseCell(leafNoise: leafNoise));
            // Links twice as often as the rest, so that some founders carry a nose and a joint: a
            // body without a joint has no brain output, so nothing reads its nose.
            config.Genome.FounderCellTypes = new[]
            {
                CellTypeIds.Nose, CellTypeIds.Link, CellTypeIds.Link, CellTypeIds.Photosynthetic,
            };

            return config;
        }

        private sealed class Pair : IDisposable
        {
            public World World;
            public Simulation Sim;

            public void Dispose() => Sim?.Dispose();
        }

        private static Pair Found(float leafNoise = 0.2f)
        {
            var world = new World(SmellingWorld(leafNoise), 3UL);
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

        /// <summary>Noses on bodies whose brain reads the leaf smell: the noses a sniff reaches the joints through.</summary>
        private static int Noses(Simulation sim)
        {
            int noses = 0;
            foreach (Creature body in sim.Dynamics.Creatures)
            {
                if (!ReadsLeaf(body)) continue;
                foreach (PhenotypePart part in body.Phenotype.Parts)
                {
                    if (part.CellTypeId == CellTypeIds.Nose) noses++;
                }
            }

            return noses;
        }

        private static bool ReadsLeaf(Creature body) =>
            Brain.MaskReads(body.Brain.SensorMask, SensorChannel.LeafScent) ||
            Brain.MaskReads(body.Brain.SensorMask, SensorChannel.LeafScentDirection);

        /// <summary>Steps a metabolic step at a time until some body reads through a nose, or fails saying so.</summary>
        private static int UntilANoseIsRead(Pair live)
        {
            for (int m = 1; m <= 120; m++)
            {
                for (int i = 0; i < StepsPerMetabolic; i++) live.Sim.Step();
                if (Noses(live.Sim) > 0) return m;
            }

            throw new Xunit.Sdk.XunitException(
                "No body read the leaf smell through a nose within 120 metabolic steps, so the world cannot test the sniff: " +
                live.World.Living.Count + " living.");
        }

        [Fact]
        public void EveryBodyIsHandedTheWorldAndItsOwnIdAndOnlyNosesCarrySmellPorts()
        {
            using (Pair live = Found())
            {
                _out.WriteLine($"a nose is read after {UntilANoseIsRead(live)} metabolic steps");
                int smellPorts = 0;
                foreach (Creature body in live.Sim.Dynamics.Creatures)
                {
                    Assert.True(body.Senses.HeldSniff);
                    Assert.Equal(live.World.Seed, body.Senses.NoiseSeed);
                    Assert.Equal((long)body.Id, body.Senses.BodyId);

                    foreach (BrainPort port in CentralWiring.Ports(body.Phenotype, live.World.Config.SensorPool()))
                    {
                        if (!port.Channel.IsSmell()) continue;
                        Assert.Equal(CellTypeIds.Nose, body.Phenotype.Parts[port.Part].CellTypeId);
                        smellPorts++;
                    }
                }

                Assert.True(smellPorts > 0, "no smell port anywhere, so the check saw nothing");
            }
        }

        /// <summary>
        /// The sniff at the top of a metabolic step's block reads the poses the step left, which is
        /// what a body's own Sample would have read at the block's first physics step, and holds
        /// through the block. With the noise off, a nose's reading is then the field at its tip as
        /// the body stood when the metabolic step ended.
        /// </summary>
        [Fact]
        public void TheSniffReadsThePosesTheMetabolicStepLeftAndHoldsThroughTheBlock()
        {
            using (Pair live = Found(leafNoise: 0f))
            {
                UntilANoseIsRead(live);

                Assert.Equal(0L, live.Sim.Steps % StepsPerMetabolic);
                float half = (float)Math.Max(1e-6, live.Sim.Solver.LeafScentHalfScaleSquareMetres);
                ScentField field = live.World.LeafScent;

                // Each nose's reading-to-be, from where it stands now, at the end of the metabolic step.
                // A body whose brain reads no leaf channel samples none (D020's mask), and its nose
                // reads 0 whatever the field: not a sniff to compare.
                var expected = new Dictionary<(Creature, int), float>();
                int unread = 0;
                foreach (Creature body in live.Sim.Dynamics.Creatures)
                {
                    if (!ReadsLeaf(body))
                    {
                        unread++;

                        continue;
                    }

                    for (int i = 0; i < body.Links; i++)
                    {
                        if (body.Phenotype.Parts[i].CellTypeId != CellTypeIds.Nose) continue;
                        Float3 t = MouthPoint.Local(body.Phenotype, i);
                        Vec3 tip = Vec3.Read(body.Position, 3 * i) +
                                   Mat3.Read(body.RotationMatrix, 9 * i) * new Vec3(t.X, t.Y, t.Z);
                        float f = field.At((float)tip.X, (float)tip.Y, (float)tip.Z);
                        expected[(body, i)] = f > 0f ? (float)(f / (f + (double)half)) : 0f;
                    }
                }

                live.Sim.Step();

                int compared = 0, smelling = 0;
                var held = new Dictionary<(Creature, int), float>();
                foreach (Creature body in live.Sim.Dynamics.Creatures)
                {
                    for (int i = 0; i < body.Links; i++)
                    {
                        // A body the reconcile rebuilt is a new object, and not compared.
                        if (!expected.TryGetValue((body, i), out float want)) continue;
                        float read = body.Senses.Read(i, SensorChannel.LeafScent, 0);
                        Assert.Equal(want, read);
                        held[(body, i)] = read;
                        compared++;
                        if (want > 0f) smelling++;
                    }
                }

                _out.WriteLine($"{compared} noses compared, {smelling} smelling a leaf, {unread} bodies reading no leaf channel");
                Assert.True(smelling > 0, "no nose smelt a leaf, so the comparison saw nothing");

                // Through the rest of the block, the readings hold while the bodies move.
                for (int i = 1; i < StepsPerMetabolic; i++) live.Sim.Step();
                foreach (KeyValuePair<(Creature, int), float> h in held)
                {
                    Assert.Equal(h.Value, h.Key.Item1.Senses.Read(h.Key.Item2, SensorChannel.LeafScent, 0));
                }
            }
        }

        [Fact]
        public void ANoseWorldResumesFromACheckpointAsItRanOn()
        {
            using (Pair live = Found())
            {
                UntilANoseIsRead(live);
                _out.WriteLine($"{live.World.Living.Count} living, {Noses(live.Sim)} noses read at the checkpoint");

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

        /// <summary>
        /// The resume above can see the noise: a restore whose noses draw from another seed parts
        /// from the run. Without this it could pass in a world where no sniff reached a joint.
        /// </summary>
        [Fact]
        public void ARestoreWhoseNosesDrawAnotherNoisePartsFromTheRun()
        {
            using (Pair live = Found())
            {
                UntilANoseIsRead(live);
                byte[] state = Checkpoint(live);

                using (Pair kept = Restore(state))
                using (Pair other = Restore(state))
                {
                    foreach (Creature body in other.Sim.Dynamics.Creatures) body.Senses.NoiseSeed ^= 1UL;

                    int partedAt = 0;
                    for (int i = 0; i < 8 * StepsPerMetabolic && partedAt == 0; i++)
                    {
                        kept.Sim.Step();
                        other.Sim.Step();
                        if (kept.Sim.Dynamics.Digest() != other.Sim.Dynamics.Digest()) partedAt = i + 1;
                    }

                    _out.WriteLine("parted at physics step " + partedAt);
                    Assert.True(partedAt > 0);
                }
            }
        }

        [Fact]
        public void TheHeaderSaysTheNoseIsOnAndTheCardDoesNotRefuseIt()
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
            try
            {
                GpuWorld.Refuse(solver);
            }
            catch (NotSupportedException other)
            {
                // The card carries the nose (D235): whatever else it refuses of this world, it is not the nose.
                Assert.DoesNotContain("nose", other.Message);
            }

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
