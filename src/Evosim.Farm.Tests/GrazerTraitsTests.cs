using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// <c>--grazer-traits</c> (<see cref="GrazerTraits"/>): the rows equal what the world developed for each
    /// id, the two drives are finite and not negative, a brain with no joint output reads 0, and the fed
    /// state reaches the ports it is meant to reach.
    /// </summary>
    [Collection(FarmProcessCollection.Name)]
    public class GrazerTraitsTests
    {
        /// <summary>What the world developed for a body at its admission.</summary>
        private sealed class Developed
        {
            public int Parts;
            public int Jointed;
            public double MassKg;
        }

        /// <summary>
        /// Steps a small world with a compact run directory and returns the directory with what the
        /// world developed for each body the first step it was alive, read off
        /// <see cref="Organism.AdultPhenotype"/> (the body <c>World.AdmitFounder</c> and
        /// <c>World.Conceive</c> develop at birth) before any module rule can change it.
        /// </summary>
        private static string RunShortWorld(string name, out Dictionary<long, Developed> developed)
        {
            string scratch = SimulationTests.Scratch(name);
            RunConfig config = SimulationTests.SmallWorld();
            var world = new World(config, 1UL);
            RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow, RunRecordFormat.Compact);
            world.QueueAdmittedGenomes = dir.Genomes != null;

            developed = new Dictionary<long, Developed>();
            string path;

            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: dir.Path))
            using (dir)
            {
                var sampler = new Sampler();
                IReadOnlyList<string> columns = Sampler.Columns(sim.Config);
                int metabolic = 0;

                while (metabolic < 60)
                {
                    if (!sim.Step()) continue;
                    metabolic++;

                    foreach (Organism organism in sim.World.Living)
                    {
                        if (developed.ContainsKey(organism.Id) || organism.ModuleCounts != null) continue;

                        Phenotype adult = organism.AdultPhenotype;
                        developed[organism.Id] = new Developed
                        {
                            Parts = adult.PartCount,
                            Jointed = adult.Parts.Count(p => p.JointType.DofCount() > 0),
                            MassKg = RunConfig.PartDensityKilogramsPerCubicMetre * adult.Parts.Sum(p => (double)p.Volume),
                        };
                    }

                    if (metabolic % 5 == 0) sampler.Write(sim, dir, columns);
                }

                sampler.Write(sim, dir, columns);
                sampler.Close();
                path = dir.Path;
            }

            return path;
        }

        /// <summary>The TSV's rows by column name, its <c>#</c> lines kept apart.</summary>
        private static List<Dictionary<string, string>> ReadTsv(string file, List<string> comments)
        {
            var rows = new List<Dictionary<string, string>>();
            string[] header = null;

            foreach (string line in File.ReadAllLines(file))
            {
                if (line.StartsWith("#", StringComparison.Ordinal)) { comments?.Add(line); continue; }
                string[] cells = line.Split('\t');

                if (header == null) { header = cells; continue; }

                var row = new Dictionary<string, string>();
                for (int i = 0; i < header.Length; i++) row[header[i]] = cells[i];
                rows.Add(row);
            }

            Assert.NotNull(header);
            Assert.Equal(GrazerTraits.Columns, header);
            return rows;
        }

        private static double D(Dictionary<string, string> row, string column) =>
            double.Parse(row[column], System.Globalization.CultureInfo.InvariantCulture);

        [Fact]
        public void EveryRowsPartsJointsAndMassAreWhatTheWorldDevelopedForThatId()
        {
            string run = RunShortWorld("grazer-traits-world", out Dictionary<long, Developed> developed);
            string tsv = Path.Combine(SimulationTests.Scratch("grazer-traits-out"), "traits.tsv");

            // A short world founds nothing mouthed, so every birth stands in for a grazer; the default rule
            // is held apart below.
            int written = GrazerTraits.Measure(
                run, tsv, TextWriter.Null,
                select: row => row.Has("e") && row["e"].AsString() == "b");

            var comments = new List<string>();
            List<Dictionary<string, string>> rows = ReadTsv(tsv, comments);

            Assert.Equal(written, rows.Count);
            Assert.True(rows.Count > 5, "a short world should bear several bodies, got " + rows.Count);
            Assert.Contains(comments, c => c.Contains("open-loop brain"));

            int compared = 0, jointed = 0;

            foreach (Dictionary<string, string> row in rows)
            {
                long id = long.Parse(row["id"], System.Globalization.CultureInfo.InvariantCulture);
                if (!developed.TryGetValue(id, out Developed world)) continue;
                compared++;

                Assert.Equal(world.Parts, (int)D(row, "parts"));
                Assert.Equal(world.Jointed, (int)D(row, "jointed"));
                Assert.Equal(world.MassKg, D(row, "mass_kg"), 9);

                // A jointed part carries a Power gene above 0 (Genome.Validate), and nothing else does.
                Assert.Equal(world.Jointed > 0, D(row, "limb_push") > 0d);
                if (world.Jointed > 0) jointed++;
            }

            Assert.True(compared > 5, "only " + compared + " rows could be compared to the world");
            Assert.True(jointed > 0, "no jointed body in the short world: the jointed column was never exercised");
        }

        [Fact]
        public void TheTwoDrivesAreFiniteAndNotNegative()
        {
            string run = RunShortWorld("grazer-traits-drives", out _);
            string tsv = Path.Combine(SimulationTests.Scratch("grazer-traits-drives-out"), "traits.tsv");

            GrazerTraits.Measure(run, tsv, TextWriter.Null, select: row => row.Has("e") && row["e"].AsString() == "b");
            List<Dictionary<string, string>> rows = ReadTsv(tsv, null);

            Assert.NotEmpty(rows);

            foreach (Dictionary<string, string> row in rows)
            {
                foreach (string column in new[] { "drive_rest", "drive_fed" })
                {
                    double v = D(row, column);
                    Assert.True(!double.IsNaN(v) && !double.IsInfinity(v), column + " is not finite for " + row["id"]);
                    Assert.True(v >= 0d, column + " is negative for " + row["id"]);
                    Assert.True(v <= 1d + 1e-9, column + " exceeds 1 for " + row["id"]);
                }
            }
        }

        [Fact]
        public void TheDefaultGrazerRuleIsAttackAndIntakeAtBirth()
        {
            Assert.True(GrazerTraits.IsGrazerBirth(Json.Parse("{\"e\":\"b\",\"id\":1,\"atk\":1,\"ink\":1}")));
            Assert.False(GrazerTraits.IsGrazerBirth(Json.Parse("{\"e\":\"b\",\"id\":1,\"atk\":1,\"ink\":0}")));
            Assert.False(GrazerTraits.IsGrazerBirth(Json.Parse("{\"e\":\"b\",\"id\":1,\"atk\":0,\"ink\":1}")));
            Assert.False(GrazerTraits.IsGrazerBirth(Json.Parse("{\"e\":\"d\",\"id\":1,\"atk\":1,\"ink\":1}")));
            Assert.False(GrazerTraits.IsGrazerBirth(Json.Parse("{\"e\":\"b\",\"id\":1}")));
        }

        // ------------------------------------------------------------------ a brain with and without joint outputs

        private static MorphNode Node(JointType joint, string cellTypeId, float power, int limit)
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(0.1f, 0.1f, 0.1f),
                JointType = joint,
                RecursiveLimit = limit,
                CellTypeId = cellTypeId,
                Power = power,
            };
            node.ResampleJointLimits(new Float2(-1f, 1f));
            return node;
        }

        /// <summary>One structural box and nothing else: no joint, so no joint output.</summary>
        private static Genome Box(float adultScale = 1f)
        {
            var genome = new Genome { RootIndex = 0 };
            genome.Nodes.Add(Node(JointType.Fixed, CellTypeIds.Structural, 0f, 1));
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 2f };
            genome.AdultScale = adultScale;
            return genome;
        }

        /// <summary>A structural root and a spine of two hinged links: three parts, two joints.</summary>
        private static Genome Spine()
        {
            var edge = new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            };

            MorphNode root = Node(JointType.Fixed, CellTypeIds.Structural, 0f, 1);
            MorphNode link = Node(JointType.Hinge, CellTypeIds.Link, 50f, 2);
            root.Edges.Add(edge);
            link.Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = edge.ParentAnchor,
                ChildAnchor = edge.ChildAnchor,
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            var genome = new Genome { RootIndex = 0 };
            genome.Nodes.Add(root);
            genome.Nodes.Add(link);
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 2f };
            genome.AdultScale = 1f;
            return genome;
        }

        private static float Dt => 0.02f;

        [Fact]
        public void ABrainWithNoJointOutputsReadsZero()
        {
            // The old per-part brain on a body with no joint.
            Phenotype plain = Developer.Develop(Box());
            Assert.Equal(0, plain.TotalDof);
            Assert.Equal(0d, GrazerTraits.MeanDrive(plain, bendAndPower: false, Dt, GrazerTraits.DriveSeconds, GrazerTraits.Senses.Fed));
            Assert.Equal(0d, GrazerTraits.MeanDrive(plain, bendAndPower: true, Dt, GrazerTraits.DriveSeconds, GrazerTraits.Senses.AtRest));

            // D146's one brain, empty, on the same body.
            Genome central = Box();
            CentralWiring.Convert(central);
            Phenotype body = Developer.Develop(central);
            Assert.True(Brain.For(body).IsCentral);
            Assert.Equal(0d, GrazerTraits.MeanDrive(body, bendAndPower: true, Dt, GrazerTraits.DriveSeconds, GrazerTraits.Senses.Fed));

            // A jointed body whose brain has no output of its own is silent too: the drive is the outputs'.
            Genome silent = Spine();
            CentralWiring.Convert(silent);
            Phenotype hinged = Developer.Develop(silent);
            Assert.Equal(2, hinged.TotalDof);
            Assert.Equal(0d, GrazerTraits.MeanDrive(hinged, bendAndPower: false, Dt, GrazerTraits.DriveSeconds, GrazerTraits.Senses.Fed));
        }

        [Fact]
        public void TheFedStateReachesTheLeafSmellAndEnergyPortsAndTheRestStateReadsNothing()
        {
            Genome g = Spine();
            CentralWiring.Convert(g);
            Phenotype body = Developer.Develop(g);
            List<BrainOutput> outputs = CentralWiring.Outputs(body);
            List<BrainOutput> power = outputs.Where(o => o.Kind == BrainOutputKind.Power).ToList();
            Assert.Equal(2, power.Count);

            foreach (BrainOutput o in outputs) g.Brain.Outputs.Add(new BrainUnit { Id = o.Id });

            // Power 0 reads bias 0.3 and the leaf smell on the first link, power 1 the energy on the root, at weight 2.
            g.Brain.Outputs.First(u => u.Id == power[0].Id).Bias = 0.3f;
            ulong leaf = BrainIds.Port(body.Parts[1].PathId, SensorChannel.LeafScent, 0);
            ulong energy = BrainIds.Port(body.Parts[0].PathId, SensorChannel.Energy, 0);
            g.Brain.Links.Add(new BrainLink { From = leaf, To = power[0].Id, Weight = 1f });
            g.Brain.Links.Add(new BrainLink { From = energy, To = power[1].Id, Weight = 2f });

            Phenotype wired = Developer.Develop(g);

            double rest = GrazerTraits.MeanDrive(wired, bendAndPower: false, Dt, GrazerTraits.DriveSeconds, GrazerTraits.Senses.AtRest);
            double fed = GrazerTraits.MeanDrive(wired, bendAndPower: false, Dt, GrazerTraits.DriveSeconds, GrazerTraits.Senses.Fed);

            // The first step has the outputs read the senses, then every step is steady: tanh of the sums.
            Assert.Equal(0.5 * Math.Tanh(0.3), rest, 6);
            Assert.Equal(0.5 * (Math.Tanh(0.3 + GrazerTraits.FedLeafSmell) + Math.Tanh(2d * GrazerTraits.FedEnergy)), fed, 6);

            // With the bend outputs counted too, the (silent) bends halve the mean.
            double restBoth = GrazerTraits.MeanDrive(wired, bendAndPower: true, Dt, GrazerTraits.DriveSeconds, GrazerTraits.Senses.AtRest);
            Assert.Equal(0.5 * rest, restBoth, 6);
        }

        [Fact]
        public void AMassIsTheAdultBodysAndTheAdultScaleIsAlreadyInIt()
        {
            double one = GrazerTraits.MassKilograms(Developer.Develop(Box(1f)));
            double two = GrazerTraits.MassKilograms(Developer.Develop(Box(2f)));

            Assert.True(one > 0d);
            Assert.Equal(8d, two / one, 4);
        }
    }
}
