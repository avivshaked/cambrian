using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The restep's <c>leaves.json</c> (D235's graft film): every body that was not a member, with its genome, its links' poses when first
    /// and last seen, and when it left the world.
    /// </summary>
    public sealed class LeavesJsonTests
    {
        private static FrameRecorder.LeafTrack Track(long id, double first, double last, double drift, double x)
        {
            var genome = new Genome();
            genome.Nodes.Add(new MorphNode { CellTypeId = CellTypeIds.Photosynthetic, JointType = JointType.Fixed });
            var pose = new[] { new LinkPose(x, -12.5, 3.25, 0, 0, 0, 1) };
            return new FrameRecorder.LeafTrack
            {
                Id = id, Genome = genome, FirstSeconds = first, LastSeconds = last, MaxDriftMetres = drift, First = pose, Last = pose,
            };
        }

        [Fact]
        public void EachLeafCarriesItsGenomeItsPosesAndWhenItLeft()
        {
            var leaves = new List<FrameRecorder.LeafTrack> { Track(7, 0.1, 300, 0.02, 12.5), Track(9, 0.1, 150.4, 0, 40.125) };
            string text = FrameRecorder.LeavesJson(leaves, 300);
            Assert.DoesNotContain("\r", text);

            JsonNode doc = Json.Parse(text);
            Assert.Equal(2, doc["leafCount"].AsInt());
            JsonNode[] rows = doc["leaves"].Items().ToArray();
            Assert.Equal(7, rows[0]["id"].AsInt());
            Assert.Equal(JsonNode.NodeKind.Null, rows[0]["goneAfter"].Kind);
            Assert.Equal(150.4, rows[1]["goneAfter"].AsDouble());
            Assert.Equal(new[] { 40.125, -12.5, 3.25, 0, 0, 0, 1 }, rows[1]["first"].Items().First().Items().Select(v => v.AsDouble()).ToArray());
            Assert.Equal(JsonNode.NodeKind.Object, rows[1]["genome"].Kind);
            Assert.Contains(GenomeJson.Write(leaves[1].Genome), text);
        }
    }
}
