using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The per-part, per-step record of which eating parts took matter (<see cref="Organism.TookMatterMask"/> and
    /// <see cref="World.TookMatterThisStep"/>): set where the intake is credited, zeroed at the top of the next mouth
    /// pass, read by the farm's pose recorder and the theatre's live mode, and read by no rule of the world. Driven by
    /// hand as <see cref="ContactEatingTests"/> and <see cref="MouthReachTests"/> are.
    /// </summary>
    public class TookMatterMaskTests
    {
        private static readonly float Intake = ContactEatingTests.ConsumerIntake;

        /// <summary>A spine of <paramref name="parts"/> consumer boxes, every one an eating part.</summary>
        private static Genome EatingSpine(int parts)
        {
            Genome genome = Fixtures.MouthSpine(parts);
            MorphNode node = genome.Nodes[0];
            node.CellTypeId = CellTypeIds.Consumer;
            node.Attack = 1f;
            node.Intake = Intake;
            return genome;
        }

        // ------------------------------------------------------------------ the ordinal

        [Fact]
        public void EatingPartsAreCountedInPartOrderAndOtherPartsHaveNone()
        {
            var config = ContactEatingTests.Stage(on: true);
            var world = new World(config, seed: 3);
            world.Inoculate(EatingSpine(3), 1, -1f);
            world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
            Organism spine = world.Living[0];
            Organism leaf = world.Living[1];

            Assert.Equal(3, spine.Phenotype.PartCount);
            for (int k = 0; k < 3; k++) Assert.Equal(k, MouthPoint.EatingOrdinal(spine.Phenotype, k));
            Assert.Equal(-1, MouthPoint.EatingOrdinal(spine.Phenotype, 3));
            Assert.Equal(-1, MouthPoint.EatingOrdinal(spine.Phenotype, -1));
            Assert.Equal(0b111u, MouthPoint.AllEatingPartsMask(spine.Phenotype));

            Assert.Equal(-1, MouthPoint.EatingOrdinal(leaf.Phenotype, 0));
            Assert.Equal(0u, MouthPoint.AllEatingPartsMask(leaf.Phenotype));
        }

        // ------------------------------------------------------------------ a bite

        [Fact]
        public void ABiteSettledOnABodySetsTheBitingPartsBitAndTheNextStepClearsIt()
        {
            var world = new World(ContactEatingTests.Stage(on: true), seed: 3);
            world.Inoculate(ContactEatingTests.Leaf(4), 1, -1f);
            world.Inoculate(EatingSpine(3), 1, -1f);
            Organism leaf = world.Living[0];
            Organism spine = world.Living[1];

            Assert.Equal(0u, spine.TookMatterMask);
            Assert.Empty(world.TookMatterThisStep);

            // The third part bites: the third eating part, bit 2. A short step, so that a wound does not cut the part
            // the later steps bite.
            world.SetContacts(ContactEatingTests.Touching(spine, 2, leaf, 1));
            world.ApplyMouth(0.25f);

            Assert.Equal(1u << 2, spine.TookMatterMask);
            Assert.Equal(0u, leaf.TookMatterMask);
            Assert.Single(world.TookMatterThisStep);
            Assert.Same(spine, world.TookMatterThisStep[0]);

            // A step with a different part biting: this step's mask only, not the OR of the two.
            world.SetContacts(ContactEatingTests.Touching(spine, 0, leaf, 1));
            world.ApplyMouth(0.25f);

            Assert.Equal(1u, spine.TookMatterMask);
            Assert.Single(world.TookMatterThisStep);

            // A step with no contact at all.
            world.ApplyMouth(0.25f);

            Assert.Equal(0u, spine.TookMatterMask);
            Assert.Empty(world.TookMatterThisStep);
        }

        [Fact]
        public void APartWithNoIntakeTakesNothingAndSetsNoBit()
        {
            var world = new World(ContactEatingTests.Stage(on: true), seed: 3);
            world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
            world.Inoculate(ContactEatingTests.Claw(), 1, -1f);
            Organism leaf = world.Living[0];
            Organism claw = world.Living[1];

            world.SetContacts(ContactEatingTests.Touching(claw, 0, leaf, 1));
            world.ApplyMouth(1f);

            Assert.True(leaf.PartHealth[1] < 1f, "the claw should have wounded the leaf, or the test says nothing");
            Assert.Equal(0u, claw.TookMatterMask);
            Assert.Empty(world.TookMatterThisStep);
        }

        [Fact]
        public void ABodyWithoutContactEatingNeverSetsABitThroughTheBitePath()
        {
            var world = new World(ContactEatingTests.Stage(on: false), seed: 3);
            world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
            world.Inoculate(EatingSpine(1), 1, -1f);
            Organism spine = world.Living[1];

            world.SetContacts(ContactEatingTests.Touching(spine, 0, world.Living[0], 1));
            world.ApplyMouth(1f);

            Assert.Equal(0u, spine.TookMatterMask);
            Assert.Empty(world.TookMatterThisStep);
        }

        // ------------------------------------------------------------------ a corpse meal

        private static RunConfig Box(bool fromMouth) => new RunConfig
        {
            Light = new LightModel(100f, 12f),
            SharedSpace = true,
            FieldModel = MatterField.Grid,
            WorldAreaSquareMetres = 144f,
            HorizontalPatches = 4,
            WorldDepthMetres = 24f,
            InitialMatterPerCubicMetre = 0f,
            MinimumPopulation = 0,
            MaximumPopulation = 100_000,
            CorpseDecayPerSecond = 0.001f,
            IntakeReachMetres = 1f,
            IntakeWasteFraction = 0.25f,
            IntakeReachFromMouth = fromMouth,
        };

        private static readonly Float3 CorpseAt = new Float3(6f, -2f, 3f);
        private static readonly Float3 NearBy = new Float3(6.5f, -2f, 3f);
        private static readonly Float3 FarOff = new Float3(2f, -2f, 3f);

        private static World OneCorpseAndOneMouth(RunConfig config, Float3 centre, out Organism mouth)
        {
            var world = new World(config, seed: 11);

            world.Inoculate(Fixtures.ArmedBox(half: 0.2f), 1, heightY: -2f);
            Organism dying = world.Living[0];
            world.Observe(dying, CorpseAt, 0f);
            world.KillDiverged(dying);
            Assert.Single(world.Corpses);

            world.Inoculate(
                Fixtures.ArmedBox(intake: 1f, half: 0.2f, cellTypeId: CellTypeIds.Consumer), 1, heightY: -2f);
            mouth = world.Living.Single(c => c.HasIntake);
            world.Observe(mouth, centre, 0f);
            return world;
        }

        [Fact]
        public void AMealOfACorpseSetsTheBitOfAMouthInReachAndNoneOutOfIt()
        {
            World near = OneCorpseAndOneMouth(Box(fromMouth: false), NearBy, out Organism eater);
            near.ApplyMouth(0.5f);

            Assert.True(near.UnitsEaten > 0d, "nothing was eaten, so nothing was tested");
            Assert.Equal(1u, eater.TookMatterMask);
            Assert.Same(eater, near.TookMatterThisStep.Single());

            World far = OneCorpseAndOneMouth(Box(fromMouth: false), FarOff, out Organism hungry);
            far.ApplyMouth(0.5f);

            Assert.Equal(0d, far.UnitsEaten);
            Assert.Equal(0u, hungry.TookMatterMask);
            Assert.Empty(far.TookMatterThisStep);
        }

        [Fact]
        public void WithMouthPointsOnlyAMouthPointInReachCountsAsTheMouthThatAte()
        {
            // The centre is far; the handed point is near: it eats, by that point's mouth.
            World world = OneCorpseAndOneMouth(Box(fromMouth: true), FarOff, out Organism mouth);
            world.ObserveMouths(mouth, new[] { NearBy.X, NearBy.Y, NearBy.Z }, 1);
            world.ApplyMouth(0.5f);

            Assert.True(world.UnitsEaten > 0d);
            Assert.Equal(1u, mouth.TookMatterMask);

            // The converse: the centre is near and the point is far, so nothing is eaten and no bit is set.
            World second = OneCorpseAndOneMouth(Box(fromMouth: true), NearBy, out Organism other);
            second.ObserveMouths(other, new[] { FarOff.X, FarOff.Y, FarOff.Z }, 1);
            second.ApplyMouth(0.5f);

            Assert.Equal(0d, second.UnitsEaten);
            Assert.Equal(0u, other.TookMatterMask);
        }

        // ------------------------------------------------------------------ the record is read by nothing

        private static byte[] BittenState(bool scribbleOnTheMask)
        {
            RunConfig config = ContactEatingTests.Stage(on: true);
            var world = new World(config, seed: 3);
            world.Inoculate(ContactEatingTests.Leaf(4), 1, -1f);
            world.Inoculate(EatingSpine(2), 1, -1f);

            PropertyInfo mask = typeof(Organism).GetProperty(
                nameof(Organism.TookMatterMask), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int step = 0; step < 6; step++)
            {
                if (scribbleOnTheMask)
                {
                    // Whatever the record says before the pass, no rule of the world reads it.
                    foreach (Organism body in world.Living) mask.SetValue(body, 0xFFFFFFFFu);
                }

                Organism eater = world.Living.Single(c => c.HasIntake);
                Organism eaten = world.Living.Single(c => !c.HasIntake);
                world.SetContacts(ContactEatingTests.Touching(eater, step % 2, eaten, 1));
                world.ApplyMouth(0.25f);
            }

            return ContactEatingTests.StateOf(world);
        }

        [Fact]
        public void NoRuleOfTheWorldReadsTheMaskSoTheStateIsTheSameWhateverItHeld()
        {
            byte[] plain = BittenState(scribbleOnTheMask: false);
            byte[] scribbled = BittenState(scribbleOnTheMask: true);

            Assert.Equal(plain.Length, scribbled.Length);
            Assert.True(plain.SequenceEqual(scribbled), "the world's state depends on the took-matter mask");
        }
    }
}
