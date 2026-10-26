using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D235's graft: a nose stood on a bred swimmer, every old part and port kept, the smell links
    /// moved onto the nose. Run on D178's ten founders (<c>inocula/r59-direction-tops</c>), the
    /// bodies the graft's film swims.
    /// </summary>
    public sealed class NoseGraftTests
    {
        private readonly ITestOutputHelper _out;

        public NoseGraftTests(ITestOutputHelper output) => _out = output;

        private static readonly CellTypeRegistry Table = CellTypeRegistry.Standard.Appending(new NoseCell());

        /// <summary>On the root's top face, toward its front: the mouth looks along -z, away from the link at +z.</summary>
        private static readonly Float3 Anchor = new Float3(0f, 1f, -0.7f);
        private static readonly Float3 Up = new Float3(0f, 1f, 0f);

        private List<(string Name, Genome Genome)> Founders()
        {
            string folder = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "inocula", "r59-direction-tops"));
            if (!Directory.Exists(folder))
            {
                _out.WriteLine($"no founders at {folder} — skipped");
                return new List<(string, Genome)>();
            }

            return Directory.GetFiles(folder, "*.json").OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => (Path.GetFileName(f), GenomeJson.Read(File.ReadAllText(f)))).ToList();
        }

        private static NoseGraftResult Graft(Genome g) => NoseGraft.Graft(g, Table, g.RootIndex, Anchor, Up);

        [Fact]
        public void TheGraftAddsOneNoseOfTheReferenceAreaAndKeepsEveryPartItsPathAndTheMouth()
        {
            foreach ((string name, Genome genome) in Founders())
            {
                string was = GenomeJson.Write(genome);
                NoseGraftResult graft = Graft(genome);
                Assert.Equal(was, GenomeJson.Write(genome));

                Phenotype before = Developer.Develop(genome, cellTypes: Table);
                Phenotype after = Developer.Develop(graft.Genome, cellTypes: Table);
                Assert.Equal(before.Parts.Count + 1, after.Parts.Count);
                for (int i = 0; i < before.Parts.Count; i++)
                {
                    Assert.Equal(before.Parts[i].PathId, after.Parts[i].PathId);
                    Assert.Equal(before.Parts[i].CellTypeId, after.Parts[i].CellTypeId);
                    Assert.False(after.Parts[i].Smells);
                }

                PhenotypePart nose = after.Parts[after.Parts.Count - 1];
                Assert.Equal(CellTypeIds.Nose, nose.CellTypeId);
                Assert.True(nose.Smells);
                Assert.Equal(graft.NosePath, nose.PathId);
                Assert.InRange(nose.SurfaceArea, 0.1f * 0.999f, 0.1f * 1.001f);
                Assert.Equal(MouthPoint.Local(before, 0), MouthPoint.Local(after, 0));

                // The nose stands up off the root's top face, unrotated, its tip at the far end.
                Float3 local = MouthPoint.Local(after, nose.Index);
                Float3 tip = new Float3(nose.Position.X + local.X, nose.Position.Y + local.Y, nose.Position.Z + local.Z);
                Assert.True(local.Y > 0f);
                Assert.True(tip.Y > before.Parts[0].HalfExtents.Y);
                _out.WriteLine($"{name}: nose {nose.HalfExtents}, area {nose.SurfaceArea:0.0000} m², tip {tip}, " +
                    $"{graft.LinksMoved} links moved into {graft.LinksMade}, {graft.DirectionLinksReframed} reframed");
            }
        }

        [Fact]
        public void TheSmellLinksMoveOntoTheNoseSummedAndEveryOtherLinkIsKept()
        {
            int moved = 0;
            foreach ((string name, Genome genome) in Founders())
            {
                NoseGraftResult graft = Graft(genome);
                Phenotype before = Developer.Develop(genome, cellTypes: Table);
                Phenotype after = Developer.Develop(graft.Genome, cellTypes: Table);

                var smellPorts = new Dictionary<ulong, (SensorChannel, int)>();
                foreach (PhenotypePart part in before.Parts)
                {
                    foreach (SensorChannel c in (SensorChannel[])Enum.GetValues(typeof(SensorChannel)))
                    {
                        if (!c.IsSmell()) continue;
                        for (int i = 0; i < c.IndexCount(); i++) smellPorts[BrainIds.Port(part.PathId, c, i)] = (c, i);
                    }
                }

                List<BrainLink> old = genome.Brain.Links;
                List<BrainLink> now = graft.Genome.Brain.Links;
                var others = old.Where(l => !smellPorts.ContainsKey(l.From)).Select(Key).ToList();
                Assert.Equal(others, now.Take(others.Count).Select(Key).ToList());

                var expected = new Dictionary<(ulong, ulong), double>();
                foreach (BrainLink l in old.Where(l => smellPorts.ContainsKey(l.From)))
                {
                    (SensorChannel c, int i) = smellPorts[l.From];
                    var pair = (BrainIds.Port(graft.NosePath, c, i), l.To);
                    expected[pair] = (expected.TryGetValue(pair, out double w) ? w : 0) + l.Weight;
                }

                List<BrainLink> made = now.Skip(others.Count).ToList();
                Assert.Equal(expected.Count, made.Count);
                Assert.Equal(graft.LinksMade, made.Count);
                Assert.Equal(old.Count - others.Count, graft.LinksMoved);
                Dictionary<ulong, BrainPort> ports = CentralWiring.AllPorts(after);
                foreach (BrainLink l in made)
                {
                    Assert.Equal(expected[(l.From, l.To)], l.Weight, 5);
                    Assert.True(ports.ContainsKey(l.From), "a moved link reads a port the grafted body has");
                    Assert.Equal(after.Parts.Count - 1, ports[l.From].Part);
                }

                // Under the nose's table no old part keeps a smell port, so nothing was left reading nothing.
                Assert.DoesNotContain(smellPorts.Keys, k => ports.ContainsKey(k));
                moved += graft.LinksMoved;
            }

            // D178's founders were bred to the leaf's direction, so they read it: the graft had work to do.
            if (Founders().Count > 0) Assert.True(moved > 0);
        }

        [Fact]
        public void TheGraftedGenomeSurvivesTheFile()
        {
            foreach (var (_, genome) in Founders())
            {
                string text = GenomeJson.Write(Graft(genome).Genome);
                Assert.Equal(text, GenomeJson.Write(GenomeJson.Read(text)));
            }
        }

        [Fact]
        public void TheGraftRefusesWhatItCannotDoHonestly()
        {
            List<(string Name, Genome Genome)> founders = Founders();
            if (founders.Count == 0) return;
            Genome g = founders[0].Genome;

            // A table without the nose, where the nose would not develop.
            Assert.Throws<ArgumentException>(() => NoseGraft.Graft(g, CellTypeRegistry.Standard, 0, Anchor, Up));

            // A per-part brain, which has no ports to move.
            Genome perPart = g.Clone();
            perPart.Brain = null;
            Assert.Throws<ArgumentException>(() => NoseGraft.Graft(perPart, Table, 0, Anchor, Up));

            // An anchor off the face the axis names, and an axis that is not an axis.
            Assert.Throws<ArgumentException>(() => NoseGraft.Graft(g, Table, 0, new Float3(0f, 0.5f, -0.7f), Up));
            Assert.Throws<ArgumentException>(() => NoseGraft.Graft(g, Table, 0, Anchor, new Float3(0f, 0.7f, 0.7f)));

            // A second nose.
            Assert.Throws<ArgumentException>(() => Graft(Graft(g).Genome));
        }

        private static (ulong, ulong, float) Key(BrainLink l) => (l.From, l.To, l.Weight);
    }
}
