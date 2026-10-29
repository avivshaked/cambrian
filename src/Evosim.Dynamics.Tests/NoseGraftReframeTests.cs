using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D235's graft keeps what the brain feels of the leaf's smell. A part reads the leaf's gradient in its own frame and the
    /// nose in the nose's, so a graft that moved a limb's direction link to the same component of the nose would read it along
    /// the wrong axis. Here the real solver reads both bodies, at rest, in a field whose gradient is uniform and off every axis,
    /// and each brain target's total smell input must come out the same.
    /// </summary>
    public sealed class NoseGraftReframeTests
    {
        private readonly ITestOutputHelper _out;

        public NoseGraftReframeTests(ITestOutputHelper output) => _out = output;

        /// <summary>The relative size a target's smell input may differ by between the plain body and the grafted one.</summary>
        private const double Tolerance = 1e-5;

        // The snout of the nose bench: on the root's front face above the mouth, pointing the way the body swims.
        private static readonly Float3 Anchor = new Float3(0f, 0.6f, -1f), Axis = new Float3(0f, 0f, -1f);
        private const float Area = 0.0288f, Slenderness = 1.5f;

        private static string Repository()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "rounds", "nursery", "configs")))
                dir = Path.GetDirectoryName(dir);
            Assert.True(dir != null, "no rounds/nursery/configs above " + AppContext.BaseDirectory);
            return dir;
        }

        private static RunConfig Config(string hash) =>
            RunConfigJson.Read(File.ReadAllText(Path.Combine(Repository(), "rounds", "nursery", "configs", hash + ".json")));

        /// <summary>
        /// A field whose value is 256 plus a plane through the origin, so its trilinear gradient is the same everywhere in
        /// the grid's interior and its value is constant to better than a part in 10^7 across a body. Every cell is a multiple
        /// of 2^-11, so the interpolation does no rounding that depends on the cell.
        /// </summary>
        private static ScentField Plane(Float3 gradient)
        {
            var field = new ScentField(40f, 40f, 20f, range: 8f, cell: 1f);
            var cells = new float[field.CellsX * field.CellsY * field.CellsZ];
            for (int i = 0; i < field.CellsX; i++)
            {
                for (int j = 0; j < field.CellsY; j++)
                {
                    for (int k = 0; k < field.CellsZ; k++)
                    {
                        double x = i + 0.5, y = -(j + 0.5), z = k + 0.5;
                        cells[(i * field.CellsY + j) * field.CellsZ + k] =
                            (float)(256.0 + (gradient.X * x + gradient.Y * y + gradient.Z * z) / 256.0);
                    }
                }
            }

            field.CopyCellsFrom(cells);
            return field;
        }

        /// <summary>Each brain target's smell input and its size (the sum of the terms' magnitudes), the body at rest in <paramref name="field"/>.</summary>
        private static (Dictionary<ulong, double> Input, Dictionary<ulong, double> Size) SmellInputs(
            Genome genome, RunConfig config, ScentField field)
        {
            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes, cellTypes: config.CellTypes);
            SolverConfig solver = SolverConfig.From(config, 0.01);
            var creature = new Creature(0, body, solver, config.Shapes);
            creature.PlaceAsDeveloped(new Vec3(20, -10, 20), body);
            creature.Senses.LeafScent = field;
            creature.Senses.HeldSniff = false;
            creature.Senses.Sample();

            Dictionary<ulong, BrainPort> ports = CentralWiring.AllPorts(body);
            var input = new Dictionary<ulong, double>();
            var size = new Dictionary<ulong, double>();
            foreach (BrainLink link in genome.Brain.Links)
            {
                if (!ports.TryGetValue(link.From, out BrainPort port) || !port.Channel.IsSmell()) continue;
                double term = link.Weight * (double)creature.Senses.Read(port.Part, port.Channel, port.Index);
                input[link.To] = (input.TryGetValue(link.To, out double a) ? a : 0.0) + term;
                size[link.To] = (size.TryGetValue(link.To, out double s) ? s : 0.0) + Math.Abs(term);
            }

            return (input, size);
        }

        private static List<(string Body, Genome Genome)> Pool()
        {
            string path = Path.Combine(Repository(), "rounds", "nursery", "pools", "410a70db44a4b20a.tsv");
            var founders = new List<(string, Genome)>();
            foreach (string line in File.ReadAllLines(path).Where(l => l.Length > 0))
            {
                int tab = line.IndexOf('\t');
                founders.Add((line.Substring(0, tab), GenomeJson.Read(line.Substring(tab + 1))));
            }

            return founders;
        }

        [Fact]
        public void TheGraftedNoseFeelsTheLeafAsTheWholeBodyDidAtRest()
        {
            RunConfig plain = Config("9adc2be043e22219"), nosed = Config("3976418af5a1286d");
            Check(plain, nosed, genome => NoseGraft.Graft(
                genome, nosed.CellTypes, genome.RootIndex, Anchor, Axis, Area, nosed.Development, nosed.Shapes, Slenderness));
        }

        /// <summary>
        /// A mirrored pair whose links are split in halves between the two noses: the twin's frame is reflected, so each half
        /// is reframed in the frame of the nose it goes to, and the two halves still give each target the input it had. Both
        /// bodies grow with reflected anchors, as a pair needs, and the pair stands either side of the middle, mirrored in x.
        /// </summary>
        [Fact]
        public void ASplitPairFeelsTheLeafAsTheWholeBodyDidAtRest()
        {
            RunConfig plain = Config("9adc2be043e22219"), nosed = Config("3976418af5a1286d");
            plain.Development.ReflectMirroredAnchors = true;
            nosed.Development.ReflectMirroredAnchors = true;
            var side = new Float3(0.5f, 0.6f, -1f);
            Check(plain, nosed, genome =>
            {
                NoseGraftResult pair = NoseGraft.Graft(genome, nosed.CellTypes, genome.RootIndex, side, Axis, Area, nosed.Development,
                    nosed.Shapes, Slenderness, mirror: 0, pairLinks: NosePairLinks.Split);
                Assert.NotEqual(0UL, pair.TwinPath);
                return pair;
            });
        }

        private void Check(RunConfig plain, RunConfig nosed, Func<Genome, NoseGraftResult> graftOf)
        {
            Assert.False(plain.CellTypes.Contains(CellTypeIds.Nose));
            Assert.True(nosed.CellTypes.Contains(CellTypeIds.Nose));

            var gradients = new[] { new Float3(0.25f, -0.5f, 0.75f), new Float3(-0.75f, 0.25f, 0.5f) };
            List<(string Body, Genome Genome)> pool = Pool();
            Assert.Equal(10, pool.Count);

            int reframed = 0;
            double largest = 0.0, felt = 0.0;
            var failures = new List<string>();
            int line = 0;
            foreach ((string body, Genome genome) in pool)
            {
                line++;
                NoseGraftResult graft = graftOf(genome);
                reframed += graft.DirectionLinksReframed;

                double worst = 0.0;
                foreach (Float3 gradient in gradients)
                {
                    ScentField field = Plane(gradient);
                    var before = SmellInputs(genome, plain, field);
                    var after = SmellInputs(graft.Genome, nosed, field);
                    Assert.NotEmpty(before.Input);

                    foreach (ulong target in before.Input.Keys.Union(after.Input.Keys))
                    {
                        double a = before.Input.TryGetValue(target, out double x) ? x : 0.0;
                        double b = after.Input.TryGetValue(target, out double y) ? y : 0.0;
                        double size = Math.Max(
                            before.Size.TryGetValue(target, out double sa) ? sa : 0.0,
                            after.Size.TryGetValue(target, out double sb) ? sb : 0.0);
                        felt = Math.Max(felt, Math.Abs(a));
                        if (size <= 0.0) continue;
                        worst = Math.Max(worst, Math.Abs(a - b) / size);
                    }
                }

                largest = Math.Max(largest, worst);
                _out.WriteLine($"founder {line} (body {body}): {graft.DirectionLinksReframed} direction links off the host, largest relative error {worst:E2}");
                if (worst > Tolerance) failures.Add($"founder {line}: {worst:E2}");
            }

            _out.WriteLine($"largest relative error {largest:E2} over {pool.Count} founders; largest smell input {felt:0.####}");

            // The test must have something to find: links off the host, and a smell that reaches the brain.
            Assert.True(reframed > 0, "no founder has a direction link off the host");
            Assert.True(felt > 1e-3, $"the largest smell input was {felt}");
            Assert.True(failures.Count == 0, "smell input differs beyond " + Tolerance + " of its size: " + string.Join("; ", failures));
        }
    }
}
