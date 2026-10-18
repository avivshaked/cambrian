using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// tank-population.jsonl (the owner's ruling, 2026-10-04: every run keeps its last generation). The search
    /// itself swims episodes and is reached only through the program's entry point, so the file is tested
    /// where it is made, on hand-built members.
    /// </summary>
    public class TankPopulationTests
    {
        private static Genome Body(float bias)
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(new MorphNode { JointType = JointType.Fixed, Neurons = new[] { new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = bias, Inputs = new NeuronInput[0] } } });
            return g;
        }

        [Fact]
        public void TheLastGenerationIsKeptWholeByBodyThenScore()
        {
            var members = new[]
            {
                new PopulationMember { Genome = Body(1f), Body = 1, Born = 2, Score = 5.5, Episodes = 6, Net = 4.25 },
                new PopulationMember { Genome = Body(2f), Body = 0, Born = 0, Score = double.NaN, Episodes = 0, Net = double.NaN },
                new PopulationMember { Genome = Body(3f), Body = 0, Born = 3, Score = 9.125, Episodes = 3, Net = 9.5 },
                new PopulationMember { Genome = Body(4f), Body = 1, Born = 1, Score = 7.0, Episodes = 9, Net = 7.0 },
            };
            string dir = Path.Combine(System.AppContext.BaseDirectory, "population-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);   // beside the test binaries, inside the repository
            try
            {
                TankPopulation.Write(dir, 7, members);
                string[] lines = File.ReadAllLines(Path.Combine(dir, "tank-population.jsonl"));
                Assert.Equal(members.Length, lines.Length);

                // Bodies in order, best score first within a body, a member that never landed last.
                float[] biases = { 3f, 2f, 4f, 1f };
                for (int i = 0; i < lines.Length; i++)
                {
                    JsonNode row = Json.Parse(lines[i]);
                    Assert.Equal(7, row["gen"].AsInt());
                    Assert.Equal(JsonNode.NodeKind.Object, row["genome"].Kind);
                    Genome g = GenomeJson.Read(lines[i].Substring(lines[i].IndexOf("\"genome\":") + 9, lines[i].Length - lines[i].IndexOf("\"genome\":") - 10));
                    Assert.Equal(biases[i], g.Nodes[0].Neurons[0].Bias);
                }
                JsonNode first = Json.Parse(lines[0]);
                Assert.Equal(0, first["body"].AsInt());
                Assert.Equal(3, first["born"].AsInt());
                Assert.Equal(9.125, first["score"].AsDouble());
                Assert.Equal(3, first["episodes"].AsInt());
                Assert.Equal(9.5, first["net"].AsDouble());
                Assert.Contains("\"score\":null", lines[1]);
                Assert.Contains("\"net\":null", lines[1]);
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
