using System;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    public class CellTypeTests
    {
        private readonly ITestOutputHelper _output;

        public CellTypeTests(ITestOutputHelper output) => _output = output;

        private sealed class FreeCell : CellType
        {
            public FreeCell() : base(0f) { }
            public override string Id => "free";
            public override CellIntake Acquire(in CellContext context) => CellIntake.None;
        }

        private sealed class Duplicate : CellType
        {
            public Duplicate() : base(1f) { }
            public override string Id => CellTypeIds.Structural;
            public override CellIntake Acquire(in CellContext context) => CellIntake.None;
        }

        [Fact]
        public void ACellTypeThatCostsNothingCannotBeConstructedAtAll()
        {
            // §5A.1: a free part is a free lever. Bodies grow without limit against one, in the
            // same way creatures spun up without limit against free momentum (§11.2).
            //
            // The check used to live in the registry, which meant a free type could exist as an
            // object and only failed when someone tried to register it. Now that upkeep is a
            // constructor argument so runs can sweep it (§5A.10), the guard moved to where the
            // value arrives — there is no longer a moment at which a free cell exists.
            Assert.Throws<ArgumentOutOfRangeException>(() => new FreeCell());
        }

        [Fact]
        public void UpkeepIsPerInstanceSoARunCanSweepIt()
        {
            // "Basal metabolic rate per unit volume, per part type" is the first entry in
            // §5A.10. The ratios between the five types decide which trophic strategies are
            // viable, and a number hardcoded in a class is a number no experiment can vary.
            var cheap = new StructuralCell(0.5f);
            var dear = new StructuralCell(4f);

            Assert.True(dear.Upkeep(1f, 1f) > cheap.Upkeep(1f, 1f) * 4f - 1e-4f);
            Assert.NotEqual(cheap.HashContribution(), dear.HashContribution());
        }

        [Fact]
        public void DuplicateIdsAreRejected()
        {
            // Ids are serialized into genomes, so one id must mean exactly one type.
            Assert.Throws<ArgumentException>(
                () => new CellTypeRegistry(new StructuralCell(), new Duplicate()));
        }

        [Fact]
        public void AnUnknownIdThrowsRatherThanFallingBackToADefault()
        {
            // Substituting a default would develop a creature that is not the stored one, score
            // it, and file the result under the original genome. Nothing downstream could tell.
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => CellTypeRegistry.Standard.Resolve("no-such-cell"));

            Assert.Contains("no-such-cell", e.Message);
        }

        [Fact]
        public void OnlyALinkMayCarryAJoint()
        {
            foreach (string id in CellTypeRegistry.Standard.Ids())
            {
                CellType type = CellTypeRegistry.Standard.Resolve(id);
                Assert.Equal(id == CellTypeIds.Link, type.AllowsJoint);
            }
        }

        [Fact]
        public void RegistryOrderIsPartOfTheHash()
        {
            // Cell-type mutation picks from an RNG draw, so ordering decides which type a given
            // draw yields. Two registries holding the same types in a different order are not
            // interchangeable, and §7 exists to detect exactly that.
            var a = new CellTypeRegistry(new StructuralCell(), new LinkCell());
            var b = new CellTypeRegistry(new LinkCell(), new StructuralCell());

            Assert.NotEqual(a.HashContribution(), b.HashContribution());
        }

        [Fact]
        public void TuningATypeChangesTheHash()
        {
            // A type whose parameters are configurable but absent from the hash makes two
            // materially different runs look identical.
            var a = new CellTypeRegistry(new PhotosyntheticCell(0.05f));
            var b = new CellTypeRegistry(new PhotosyntheticCell(0.20f));

            Assert.NotEqual(a.HashContribution(), b.HashContribution());
        }

        [Fact]
        public void PhotosynthesisScalesWithLitAreaNotVolume()
        {
            var cell = new PhotosyntheticCell(0.5f);

            float small = cell.Acquire(new CellContext(1f, volume: 100f, litArea: 1f, irradiance: 10f)).FromLight;
            float large = cell.Acquire(new CellContext(1f, volume: 0.01f, litArea: 4f, irradiance: 10f)).FromLight;

            // A huge dark body earns less than a small bright sheet. That trade-off — collect
            // more light, swim worse — is the whole reason the type is interesting.
            Assert.True(large > small, $"lit area should dominate: {large} vs {small}");
            Fixtures.AssertClose(5f, small, 1e-4f);
        }

        [Fact]
        public void ConsumerYieldsMostFromCarrionAndLeastFromOtherConsumers()
        {
            // §5A.3, and the reason a consumer cell survives long enough to become a predator:
            // it can pay its way on the dead before perception exists.
            var carrion = new TissueContact(new StructuralCell(), 1000f, isAlive: false);
            var plant = new TissueContact(new PhotosyntheticCell(), 1000f, isAlive: true);
            var animal = new TissueContact(new ConsumerCell(), 1000f, isAlive: true);

            var cell = new ConsumerCell();

            Assert.True(cell.YieldAgainst(carrion) > cell.YieldAgainst(plant));
            Assert.True(cell.YieldAgainst(plant) > cell.YieldAgainst(animal));
            Assert.Equal(0f, cell.YieldAgainst(null));
        }

        [Fact]
        public void AConsumerCannotTakeMoreThanIsThere()
        {
            var cell = new ConsumerCell(biteRate: 1_000_000f);
            var contact = new TissueContact(new StructuralCell(), 10f, isAlive: false);

            CellIntake bite = cell.Acquire(new CellContext(1f, volume: 1f, contact: contact));
            float gained = bite.FromPool;

            // Yield is a fraction of what was taken, and what was taken is capped by what
            // existed — otherwise a big enough mouth mints energy out of a small corpse.
            Assert.True(gained <= 10f, $"kept {gained} J from 10 J of tissue");

            // And what left the corpse is what was taken, not what was kept: the difference is
            // destroyed rather than left behind, which is what shortens a food chain.
            Assert.True(bite.PoolDrawn <= 10f, $"drew {bite.PoolDrawn} J from 10 J of tissue");
            Assert.True(bite.PoolDrawn >= gained);
        }

        [Fact]
        public void AcquisitionIsGrossNotNet()
        {
            // Upkeep is the caller's job. A type that refunded its own costs inside Acquire
            // would be invisible until a population was living on it.
            var structural = new StructuralCell();

            Assert.Equal(0f, structural.Acquire(new CellContext(1f, volume: 1f)).Total);
            Assert.True(structural.Upkeep(1f, 1f) > 0f);
        }

        [Fact]
        public void CapacityCostsSomethingEvenWhenNothingMoves()
        {
            // The whole point of the idle term. Without it a link that actuates intermittently
            // pays almost nothing for being enormous, and evolution takes the largest capacity
            // on offer and uses it occasionally.
            var link = new LinkCell();

            float weak = link.Upkeep(new CellContext(1f, volume: 0.01f, power: 10f, dof: 1));
            float strong = link.Upkeep(new CellContext(1f, volume: 0.01f, power: 200f, dof: 1));

            Assert.True(strong > weak * 2f,
                $"20x the capacity should cost materially more when idle: {weak} vs {strong}");
        }

        [Fact]
        public void MoreDegreesOfFreedomCostMore()
        {
            // A spherical joint is three actuators. Without this, DOF is free and every link
            // evolves to the most permissive joint type available.
            var link = new LinkCell();

            float hinge = link.Upkeep(new CellContext(1f, volume: 0.01f, power: 100f, dof: 1));
            float ball = link.Upkeep(new CellContext(1f, volume: 0.01f, power: 100f, dof: 3));

            Assert.True(ball > hinge, $"3 DOF should cost more than 1: {hinge} vs {ball}");
        }

        [Fact]
        public void ALinkWithNoStandingCostForCapacityIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LinkCell(0f));
        }

        [Fact]
        public void OnlyLinksAreChargedForCapacity()
        {
            // A rigid cell handed power must not quietly become expensive — validation forbids
            // the genome, and the cost model ignores it, so the two agree.
            var structural = new StructuralCell();

            float without = structural.Upkeep(new CellContext(1f, volume: 0.01f));
            float with = structural.Upkeep(new CellContext(1f, volume: 0.01f, power: 500f, dof: 3));

            Fixtures.AssertClose(without, with, 1e-9f);
        }

        [Fact]
        public void PowerOnARigidCellFailsValidation()
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Fixtures.Box());
            g.Nodes[0].Power = 25f;

            Assert.Contains(g.Validate(), i => i.Contains("Nothing reads it"));
        }

        [Fact]
        public void ALinkWithoutCapacityFailsValidation()
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Fixtures.Box());

            MorphNode link = Fixtures.Box(0.2f, JointType.Hinge);
            link.Power = 0f;
            g.Nodes.Add(link);
            g.Nodes[0].Edges.Add(Fixtures.FaceToFace(1));

            Assert.Contains(g.Validate(), i => i.Contains("cannot actuate"));
        }

        [Fact]
        public void RandomLinksCarryCapacityAndRigidPartsDoNot()
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                Phenotype p = Developer.Develop(GenomeFactory.Random(new Rng(seed)));

                foreach (PhenotypePart part in p.Parts)
                {
                    if (part.JointType.DofCount() > 0) Assert.True(part.Power > 0f);
                    else Assert.Equal(0f, part.Power);
                }
            }
        }

        [Fact]
        public void AJointedNonLinkCellFailsValidation()
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Fixtures.Box());

            MorphNode child = Fixtures.Box(0.5f, JointType.Hinge);
            child.CellTypeId = CellTypeIds.Photosynthetic;      // a stomach that is also an elbow
            g.Nodes.Add(child);
            g.Nodes[0].Edges.Add(Fixtures.FaceToFace(1));

            var issues = g.Validate();
            _output.WriteLine(string.Join("\n", issues));

            Assert.Contains(issues, i => i.Contains("only 'link' may move"));
        }

        [Fact]
        public void RigidCellsAttachedDirectlyAreLegal()
        {
            // Cells may weld to each other at any angle. That is how shells, fins and stiff
            // trunks get built, and it is why a creature made only of body cells is a plant
            // rather than an invalid genome.
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Fixtures.Box());
            g.Nodes.Add(Fixtures.Box());
            g.Nodes[1].CellTypeId = CellTypeIds.Photosynthetic;
            g.Nodes[0].Edges.Add(Fixtures.FaceToFace(1));

            Assert.Empty(g.Validate());
            Assert.Equal(2, Developer.Develop(g).PartCount);
        }

        [Fact]
        public void RandomGenomesAreValidAndCarryLinks()
        {
            const int samples = 200;
            int withLinks = 0, withActuation = 0;
            var typeCounts = CellTypeRegistry.Standard.Ids().ToDictionary(id => id, _ => 0);

            for (ulong seed = 1; seed <= samples; seed++)
            {
                Genome genome = GenomeFactory.Random(new Rng(seed));
                Assert.Empty(genome.Validate());

                Phenotype p = Developer.Develop(genome);

                bool link = false;
                foreach (PhenotypePart part in p.Parts)
                {
                    typeCounts[part.CellTypeId]++;
                    if (part.CellTypeId == CellTypeIds.Link) link = true;
                }

                if (link) withLinks++;
                if (p.TotalDof > 0) withActuation++;
            }

            _output.WriteLine($"with at least one link: {withLinks}/{samples}");
            _output.WriteLine($"with any actuation:     {withActuation}/{samples}");
            foreach (var kv in typeCounts) _output.WriteLine($"  {kv.Key,-16} {kv.Value} parts");

            // Not asserted tightly: the balance between rigid and articulated creatures is
            // LinkChance, which is an unmeasured parameter (§5A.10). Asserted only that both
            // outcomes occur, so neither is silently impossible.
            Assert.True(withLinks > 0, "no random creature grew a link — nothing could ever move");
            Assert.True(withLinks < samples, "every random creature is articulated — rigid plants are unreachable");
        }

        [Fact]
        public void EveryPartWithADofIsALink()
        {
            // The invariant the whole design rests on, checked on developed phenotypes rather
            // than on genomes: a part that can move is made of link tissue.
            for (ulong seed = 1; seed <= 200; seed++)
            {
                Phenotype p = Developer.Develop(GenomeFactory.Random(new Rng(seed)));

                foreach (PhenotypePart part in p.Parts)
                {
                    if (part.JointType.DofCount() == 0) continue;
                    Assert.Equal(CellTypeIds.Link, part.CellTypeId);
                }
            }
        }
    }
}
