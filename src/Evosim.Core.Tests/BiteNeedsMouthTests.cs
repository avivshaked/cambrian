using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D275: a bite needs the mouth. Core has no part poses, so each test hands the world the contact list the farm would
    /// make, with the mouth's measure on it, as <see cref="ContactEatingTests"/> hands it the plain one.
    /// </summary>
    public class BiteNeedsMouthTests
    {
        private readonly ITestOutputHelper _output;

        public BiteNeedsMouthTests(ITestOutputHelper output) => _output = output;

        private static float ConsumerIntake => ContactEatingTests.ConsumerIntake;

        /// <summary>The bite stage of <see cref="ContactEatingTests"/>, on the true shape, with the rule on or off.</summary>
        private static RunConfig Stage(bool needsMouth, bool trueShape = true)
        {
            RunConfig config = ContactEatingTests.Stage(on: true);
            config.ContactPerPart = true;
            config.ContactShape = trueShape ? ContactShapeForm.TrueShape : ContactShapeForm.Spheres;
            config.BiteNeedsMouth = needsMouth;
            return config;
        }

        /// <summary>The leaf's part 1 as the hunter's part 0 meets it, with the hunter's measure on the contact.</summary>
        private static List<CreatureContact> Measured(
            Organism hunter, Organism leaf, float ratio, int mouthLink = 0, int target = 1, int census = 1) =>
            new List<CreatureContact>
            {
                new CreatureContact(hunter.Id, 0, leaf.Id, census, ratio, mouthLink, target, float.NaN, -1, -1),
            };

        private static (World World, Organism Leaf, Organism Hunter) Bite(
            RunConfig config, Func<Organism, Organism, List<CreatureContact>> contacts, float seconds = 0.1f)
        {
            var world = new World(config, seed: 3);
            world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
            world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            world.SetContacts(contacts(hunter, leaf));
            world.ApplyMouth(seconds);
            return (world, leaf, hunter);
        }

        private static bool Whole(Organism body) =>
            body.PartHealth == null || Array.TrueForAll(body.PartHealth, h => h == 1f);

        // ------------------------------------------------------------------------ the switch

        [Fact]
        public void TheSwitchIsOffByDefaultAndMovesTheHash()
        {
            Assert.False(new RunConfig().BiteNeedsMouth);
            Assert.NotEqual(Stage(needsMouth: false).Hash(), Stage(needsMouth: true).Hash());
            Assert.True(new World(Stage(needsMouth: true), seed: 3).Config.BiteNeedsMouth);
        }

        [Fact]
        public void RefusedWithoutContactEating()
        {
            RunConfig config = ContactEatingTests.Stage(on: false);
            config.ContactPerPart = true;
            config.ContactShape = ContactShapeForm.TrueShape;
            config.BiteNeedsMouth = true;

            ArgumentException error = Assert.Throws<ArgumentException>(() => new World(config, seed: 3));
            Assert.Contains("BiteNeedsMouth", error.Message);
            Assert.Contains("ContactEating", error.Message);
            Assert.Contains("D275", error.Message);
        }

        [Fact]
        public void RefusedWithoutTrueShape()
        {
            RunConfig spheres = Stage(needsMouth: true, trueShape: false);
            ArgumentException error = Assert.Throws<ArgumentException>(() => new World(spheres, seed: 3));
            Assert.Contains("BiteNeedsMouth", error.Message);
            Assert.Contains("true shape", error.Message);
            Assert.Contains("D275", error.Message);

            // Both true shapes are accepted.
            RunConfig atPoint = Stage(needsMouth: true);
            atPoint.ContactShape = ContactShapeForm.TrueShapeAtPoint;
            Assert.True(new World(atPoint, seed: 3).Config.BiteNeedsMouth);
        }

        // ------------------------------------------------------------------------ switch off reads nothing

        [Fact]
        public void SwitchOffReadsNoMeasure()
        {
            byte[] Run(Func<Organism, Organism, List<CreatureContact>> contacts)
            {
                var world = new World(Stage(needsMouth: false), seed: 3);
                world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
                world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake), 1, -1f);
                Organism leaf = world.Living[0];
                Organism hunter = world.Living[1];

                for (int step = 0; step < 24; step++)
                {
                    world.SetContacts(contacts(hunter, leaf));
                    world.Step(0.5f);
                }

                Assert.True(world.MouthBlows > 0L, "the fixture never bit");
                Assert.Equal(0L, world.MouthBlowsRefused);
                return ContactEatingTests.StateOf(world);
            }

            byte[] old = Run((h, l) => ContactEatingTests.Touching(h, 0, l, 1));
            byte[] five = Run((h, l) => Measured(h, l, 5f));
            byte[] nan = Run((h, l) => Measured(h, l, float.NaN, mouthLink: -1, target: -1));
            byte[] elsewhere = Run((h, l) => Measured(h, l, 0.5f, mouthLink: 0, target: 0));

            Assert.Equal(old, five);
            Assert.Equal(old, nan);
            Assert.Equal(old, elsewhere);
        }

        // ------------------------------------------------------------------------ the rule on

        [Fact]
        public void ASideBrushBlowIsRefusedAndAMouthTouchLands()
        {
            // The switch off: the blow of the census pair, as it has always been.
            var off = Bite(Stage(needsMouth: false), (h, l) => ContactEatingTests.Touching(h, 0, l, 1));
            Assert.False(Whole(off.Leaf), "the plain world should wound");

            // At a ratio of 3 the mouth is three radii from the part: nothing is wounded, offered or recorded.
            var tissue = new World(Stage(needsMouth: true), seed: 3);
            tissue.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
            double tissueBefore = tissue.Living[0].TissueJoules;

            var brush = Bite(Stage(needsMouth: true), (h, l) => Measured(h, l, 3f));
            Assert.True(Whole(brush.Leaf), "a brush must not wound");
            Assert.Equal(tissueBefore, brush.Leaf.TissueJoules);
            Assert.Equal(0u, brush.Hunter.TookMatterMask);
            Assert.Equal(0L, brush.World.MouthBlows);
            Assert.Equal(0L, brush.World.MouthBlowsFinishing);
            Assert.Equal(0d, brush.World.BiteKeptFromLeavesJoules);
            Assert.Equal(0d, brush.World.BiteWasteJoules);
            Assert.Equal(1L, brush.World.MouthBlowsRefused);

            // The hunter's reserve is what a hunter that touched nothing holds.
            var none = Bite(Stage(needsMouth: true), (h, l) => new List<CreatureContact>());
            Assert.Equal(none.Hunter.Energy, brush.Hunter.Energy);

            // At 0.5 the blow lands exactly as with the switch off.
            var touch = Bite(Stage(needsMouth: true), (h, l) => Measured(h, l, 0.5f));
            Assert.Equal(0L, touch.World.MouthBlowsRefused);
            Assert.Equal(off.Leaf.PartHealth[1], touch.Leaf.PartHealth[1]);
            Assert.Equal(off.Leaf.TissueJoules, touch.Leaf.TissueJoules);
            Assert.Equal(off.Leaf.PartDamage[1], touch.Leaf.PartDamage[1]);
            Assert.Equal(off.Hunter.Energy, touch.Hunter.Energy);
            Assert.Equal(off.Hunter.TookMatterMask, touch.Hunter.TookMatterMask);
            Assert.NotEqual(0u, touch.Hunter.TookMatterMask);
            Assert.Equal(off.World.MouthBlows, touch.World.MouthBlows);
            Assert.Equal(off.World.BiteKeptFromLeavesJoules, touch.World.BiteKeptFromLeavesJoules);
            Assert.Equal(off.World.BiteWasteJoules, touch.World.BiteWasteJoules);

            // A ratio of exactly 1 is a touch, and a ratio that is not a number is a refusal.
            var edge = Bite(Stage(needsMouth: true), (h, l) => Measured(h, l, 1f));
            Assert.Equal(0L, edge.World.MouthBlowsRefused);
            Assert.False(Whole(edge.Leaf));

            var unmeasured = Bite(Stage(needsMouth: true), (h, l) => Measured(h, l, float.NaN, mouthLink: -1, target: -1));
            Assert.True(Whole(unmeasured.Leaf));
            Assert.Equal(1L, unmeasured.World.MouthBlowsRefused);

            _output.WriteLine(
                $"off: leaf part 1 health {off.Leaf.PartHealth[1]:0.####}; touch: {touch.Leaf.PartHealth[1]:0.####}; brush refused {brush.World.MouthBlowsRefused}");
        }

        [Fact]
        public void ATouchLandsOnTheMeasuredPartAndNotTheCensusPart()
        {
            // The census names the leaf's part 0; the mouth touches its part 1. The wound is on part 1, and the
            // attacker recorded against it is the mouth's part.
            var bite = Bite(Stage(needsMouth: true), (h, l) => Measured(h, l, 0.25f, mouthLink: 0, target: 1, census: 0));

            Assert.Equal(1f, bite.Leaf.PartHealth[0]);
            Assert.True(bite.Leaf.PartHealth[1] < 1f, "the measured part should be wounded");
            Assert.Equal(0L, bite.World.MouthBlowsRefused);
            Assert.Equal(1L, bite.World.MouthBlows);

            // And the same census with the mouth out of reach wounds nothing: the census pair is not a fallback.
            var brush = Bite(Stage(needsMouth: true), (h, l) => Measured(h, l, 2f, mouthLink: 0, target: 1, census: 0));
            Assert.True(Whole(brush.Leaf));
            Assert.Equal(1L, brush.World.MouthBlowsRefused);
        }

        [Fact]
        public void AStaleMouthIndexIsOutOfReachAndNeverReadAsAnotherPart()
        {
            // A part index past the end of the plan is not evidence about a different part.
            var bite = Bite(Stage(needsMouth: true), (h, l) => Measured(h, l, 0.1f, mouthLink: 0, target: 9));
            Assert.True(Whole(bite.Leaf));
            Assert.Equal(1L, bite.World.MouthBlowsRefused);

            var link = Bite(Stage(needsMouth: true), (h, l) => Measured(h, l, 0.1f, mouthLink: 7, target: 1));
            Assert.True(Whole(link.Leaf));
            Assert.Equal(1L, link.World.MouthBlowsRefused);
        }

        [Fact]
        public void AClawAndRemainsAreUnchanged()
        {
            // A claw has no intake: its blow lands at NaN exactly as with the switch off, and refuses nothing.
            (World World, Organism Leaf) Claw(bool needsMouth)
            {
                var world = new World(Stage(needsMouth), seed: 3);
                world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
                world.Inoculate(ContactEatingTests.Claw(), 1, -1f);
                Organism leaf = world.Living[0];
                Organism claw = world.Living[1];

                world.SetContacts(Measured(claw, leaf, float.NaN, mouthLink: -1, target: -1));
                world.ApplyMouth(1.5f);
                return (world, leaf);
            }

            var clawOff = Claw(needsMouth: false);
            var clawOn = Claw(needsMouth: true);
            Assert.True(clawOff.Leaf.PartHealth[1] < 1f, "the claw should wound");
            Assert.Equal(clawOff.Leaf.PartHealth[1], clawOn.Leaf.PartHealth[1]);
            Assert.Equal(clawOff.Leaf.TissueJoules, clawOn.Leaf.TissueJoules);
            Assert.Equal(0L, clawOn.World.MouthBlowsRefused);

            // A part of remains carries its mouth and its claw and bites as a claw would, whatever the measure.
            (World World, Organism Remains, Organism Leaf)? Dead(bool needsMouth, ulong seed)
            {
                RunConfig config = Stage(needsMouth);
                config.AgeHazardBaseSeconds = 2f;
                config.SenescenceDoublingSeconds = 0f;
                config.SenseContact = true;

                var world = new World(config, seed);
                world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake), 1, -1f);
                world.Inoculate(ContactEatingTests.Leaf(2), 8, -1f);
                Organism hunter = world.Living[0];

                for (int step = 0; step < 400 && !hunter.IsRemains; step++) world.Step(0.5f);
                if (!hunter.IsRemains || world.Living.Count == 0) return null;
                return (world, hunter, world.Living[0]);
            }

            ulong found = 0;
            for (ulong candidate = 3; candidate < 80 && found == 0; candidate++)
            {
                if (Dead(needsMouth: false, candidate) != null) found = candidate;
            }

            Assert.True(found != 0, "no seed left a leaf living beside the dead hunter");

            var remainsOff = Dead(needsMouth: false, found).Value;
            var remainsOn = Dead(needsMouth: true, found).Value;
            Assert.True(remainsOn.Remains.Phenotype.Parts[0].Intake > 0f && remainsOn.Remains.Phenotype.Parts[0].Attack > 0f);

            remainsOff.World.SetContacts(Measured(remainsOff.Remains, remainsOff.Leaf, float.NaN, mouthLink: -1, target: -1));
            remainsOn.World.SetContacts(Measured(remainsOn.Remains, remainsOn.Leaf, float.NaN, mouthLink: -1, target: -1));
            remainsOff.World.ApplyMouth(1.5f);
            remainsOn.World.ApplyMouth(1.5f);

            Assert.True(remainsOff.Leaf.PartHealth[1] < 1f, "the remains' part should wound");
            Assert.Equal(remainsOff.Leaf.PartHealth[1], remainsOn.Leaf.PartHealth[1]);
            Assert.Equal(remainsOff.Leaf.TissueJoules, remainsOn.Leaf.TissueJoules);
            Assert.Equal(0L, remainsOn.World.MouthBlowsRefused);
        }

        // ------------------------------------------------------------------------ the record

        [Fact]
        public void ACheckpointCarriesTheRefusalsAndAResumeEqualsTheUnbrokenRun()
        {
            long leafId = -1, hunterId = -1;

            List<CreatureContact> Step(World world, int step)
            {
                Organism leaf = null, hunter = null;
                foreach (Organism body in world.Living)
                {
                    if (body.Id == leafId) leaf = body;
                    if (body.Id == hunterId) hunter = body;
                }

                if (leaf == null || hunter == null) return new List<CreatureContact>();
                return Measured(hunter, leaf, step % 3 == 0 ? 0.5f : 3f);
            }

            var world = new World(Stage(needsMouth: true), seed: 3);
            world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
            world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake), 1, -1f);
            leafId = world.Living[0].Id;
            hunterId = world.Living[1].Id;

            for (int step = 0; step < 60; step++)
            {
                world.SetContacts(Step(world, step));
                world.Step(0.5f);
            }

            Assert.True(world.MouthBlowsRefused > 0L, "the fixture refused nothing");
            Assert.True(world.MouthBlows > 0L, "the fixture bit nothing");

            World restored = ContactEatingTests.Restored(world.Config, 3, ContactEatingTests.StateOf(world));
            Assert.Equal(world.MouthBlowsRefused, restored.MouthBlowsRefused);
            Assert.Equal(world.MouthBlows, restored.MouthBlows);
            Assert.Equal(world.MouthBlowsFinishing, restored.MouthBlowsFinishing);
            Assert.Equal(ContactEatingTests.StateOf(world), ContactEatingTests.StateOf(restored));

            for (int more = 60; more < 120; more++)
            {
                world.SetContacts(Step(world, more));
                restored.SetContacts(Step(restored, more));
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(world.MouthBlowsRefused, restored.MouthBlowsRefused);
            Assert.Equal(world.MouthBlows, restored.MouthBlows);
            Assert.Equal(ContactEatingTests.StateOf(world), ContactEatingTests.StateOf(restored));
            _output.WriteLine($"refused {world.MouthBlowsRefused}, landed {world.MouthBlows}");
        }

        [Fact]
        public void TheSwitchOffWritesNoRefusalCountAndTheLayoutIsTheOneBefore()
        {
            // A world with the switch off and one with it on differ in the state only by the counts the switch adds: the
            // switch-off state is read back by a switch-off config and counts nothing.
            var world = new World(Stage(needsMouth: false), seed: 3);
            world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
            world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake), 1, -1f);
            for (int step = 0; step < 10; step++)
            {
                world.SetContacts(ContactEatingTests.Touching(world.Living[1], 0, world.Living[0], 1));
                world.Step(0.5f);
            }

            World back = ContactEatingTests.Restored(world.Config, 3, ContactEatingTests.StateOf(world));
            Assert.Equal(0L, back.MouthBlowsRefused);
            Assert.Equal(0L, back.MouthBlows);
            Assert.Equal(ContactEatingTests.StateOf(world), ContactEatingTests.StateOf(back));
        }

        // ------------------------------------------------------------------------ the radius

        private static Phenotype Single(string shapeId, Float3 half)
        {
            Genome genome = Fixtures.ArmedBox(intake: 1f, cellTypeId: CellTypeIds.Consumer);
            genome.Nodes[0].ShapeId = shapeId;
            genome.Nodes[0].Dimensions = half;
            return Developer.Develop(genome, DevelopmentLimits.Default);
        }

        [Fact]
        public void MouthRadiusClosedForms()
        {
            // A box: the mouth is the centre of the face on the longest axis, and the radius is half the smaller of the
            // face's two half-extents.
            Phenotype box = Single(ShapeIds.Box, new Float3(0.5f, 0.2f, 0.1f));
            Float3 boxHalf = box.Parts[0].HalfExtents;
            Assert.Equal(0.5f * Math.Min(Math.Abs(boxHalf.Y), Math.Abs(boxHalf.Z)), MouthPoint.Radius(box, 0));
            Assert.True(MouthPoint.Radius(box, 0) > 0f);

            Phenotype tall = Single(ShapeIds.Box, new Float3(0.1f, 0.4f, 0.3f));
            Float3 tallHalf = tall.Parts[0].HalfExtents;
            Assert.Equal(0.5f * Math.Min(Math.Abs(tallHalf.X), Math.Abs(tallHalf.Z)), MouthPoint.Radius(tall, 0));

            // A capsule: the tip of the long axis; the face is the other two half-extents.
            Phenotype capsule = Single(ShapeIds.Capsule, new Float3(0.2f, 0.5f, 0.3f));
            Float3 capHalf = capsule.Parts[0].HalfExtents;
            Assert.Equal(0.5f * Math.Min(Math.Abs(capHalf.X), Math.Abs(capHalf.Z)), MouthPoint.Radius(capsule, 0));

            // A sphere: the smallest half-extent, whichever way the point looks.
            Phenotype sphere = Single(ShapeIds.Sphere, new Float3(0.3f, 0.3f, 0.3f));
            Float3 ballHalf = sphere.Parts[0].HalfExtents;
            Assert.Equal(
                0.5f * Math.Min(Math.Abs(ballHalf.X), Math.Min(Math.Abs(ballHalf.Y), Math.Abs(ballHalf.Z))),
                MouthPoint.Radius(sphere, 0));

            Phenotype oblate = Single(ShapeIds.Sphere, new Float3(0.3f, 0.12f, 0.2f));
            Float3 oblateHalf = oblate.Parts[0].HalfExtents;
            Assert.Equal(
                0.5f * Math.Min(Math.Abs(oblateHalf.X), Math.Min(Math.Abs(oblateHalf.Y), Math.Abs(oblateHalf.Z))),
                MouthPoint.Radius(oblate, 0));
        }

        [Fact]
        public void MouthRadiusOfAFamilyPartFollowsTheAxisItsPointLiesOn()
        {
            var shapes = new PartShapeRegistry(new BoxShape(), new SphereShape(), new CapsuleShape(), new SuperellipsoidShape());
            Genome genome = Fixtures.ArmedBox(intake: 1f, cellTypeId: CellTypeIds.Consumer);
            MorphNode node = genome.Nodes[0];
            node.ShapeId = ShapeIds.Superellipsoid;
            node.Form = new PartForm(0.6f, 1f, 1.5f);
            node.Dimensions = new Float3(0.4f, 0.25f, 0.15f);

            Phenotype family = Developer.Develop(genome, shapes: shapes);
            Assert.NotNull(family.Parts[0].Family);

            Float3 point = MouthPoint.Local(family, 0);
            Float3 h = family.Parts[0].HalfExtents;
            float ax = Math.Abs(point.X), ay = Math.Abs(point.Y), az = Math.Abs(point.Z);
            float face = ax >= ay && ax >= az
                ? Math.Min(Math.Abs(h.Y), Math.Abs(h.Z))
                : ay >= az ? Math.Min(Math.Abs(h.X), Math.Abs(h.Z)) : Math.Min(Math.Abs(h.X), Math.Abs(h.Y));

            Assert.True(point.Magnitude > 1e-6f);
            Assert.Equal(0.5f * face, MouthPoint.Radius(family, 0));
        }

        [Fact]
        public void MouthRadiusIsZeroForAPartThatDoesNotEatAndForAnIndexOutsideTheBody()
        {
            Phenotype claw = Developer.Develop(Fixtures.ArmedBox(attack: 1f), DevelopmentLimits.Default);
            Assert.Equal(0f, MouthPoint.Radius(claw, 0));

            Phenotype eater = Single(ShapeIds.Box, new Float3(0.5f, 0.2f, 0.1f));
            Assert.Equal(0f, MouthPoint.Radius(eater, -1));
            Assert.Equal(0f, MouthPoint.Radius(eater, 1));
            Assert.Throws<ArgumentNullException>(() => MouthPoint.Radius(null, 0));
        }
    }
}
