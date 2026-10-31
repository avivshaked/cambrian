using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D268's brain-held grip in the solver: a hold output that lets a grip go below the cell's threshold, the prices of
    /// forming and of letting go and none for holding, and the two grip senses. Every body here is a gripping cube on a
    /// flat bed, with the one brain's hold output at a bias of the test's choosing.
    /// </summary>
    public sealed class GripBrainHoldTests
    {
        private readonly ITestOutputHelper _out;

        public GripBrainHoldTests(ITestOutputHelper output) => _out = output;

        private static readonly Float3 Half = new Float3(0.05f, 0.05f, 0.05f);

        // Two prices that are not each other's, so a mix-up shows.
        private static AnchoringCell Cell(bool hold = true, float below = -0.5f) => new AnchoringCell(
            buildJoulesPerNewton: 2e-4f, brainHold: hold, releaseBelow: below, releaseJoulesPerNewton: 3e-4f);

        private static CellTypeRegistry TableOf(AnchoringCell cell) => CellTypeRegistry.Standard.Appending(cell);

        private static SolverConfig StillWorld(AnchoringCell cell, double dt = 0.01)
        {
            SolverConfig config = TestBodies.Water(dt);
            config.WorldDepthMetres = 12;
            config.FloorIsSolid = true;
            config.CreatureContact = true;
            config.ContactPerPart = true;
            config.TissueExcessDensity = 50;
            config.Anchoring = cell;
            return config;
        }

        /// <summary>
        /// A cube of the anchoring cell with the one brain: its hold output at <paramref name="holdBias"/>, and, when
        /// <paramref name="selfWeight"/> is not 0, a link from its own gripping sense into the hold output.
        /// </summary>
        private static Genome Gripper(float holdBias, float selfWeight = 0f, string cellType = CellTypeIds.Anchoring)
        {
            Genome g = TestBodies.Box(Half);
            g.Nodes[0].CellTypeId = cellType;
            CentralWiring.Convert(g);

            ulong hold = BrainIds.Output(BrainIds.Root, 0, BrainOutputKind.Grip);
            g.Brain.Outputs.Add(new BrainUnit { Id = hold, Bias = holdBias });
            if (selfWeight != 0f)
            {
                g.Brain.Links.Add(new BrainLink
                {
                    From = BrainIds.Port(BrainIds.Root, SensorChannel.Gripping, 0), To = hold, Weight = selfWeight,
                });
            }

            return g;
        }

        private static Creature Build(Genome g, SolverConfig config, int id = 0) =>
            new Creature(id, Developer.Develop(g, DevelopmentLimits.Default, cellTypes: TableOf(config.Anchoring)), config);

        /// <summary>Put down with its sphere 1 cm into the flat bed.</summary>
        private static Creature OnTheBed(Genome g, SolverConfig config, int id = 0)
        {
            Creature body = Build(g, config, id);
            body.PlaceAt(new Vec3(0, 0, 0), QuatD.Identity);
            body.RefreshContactSphere();
            double radius = body.LinkContactRadius[0];
            body.PlaceAt(new Vec3(0, -config.WorldDepthMetres + radius - 0.01, 0), QuatD.Identity);
            body.RefreshContactSphere();
            body.CommitContactSphere();
            return body;
        }

        private sealed class Reserve : IReserveSource
        {
            public float SecondsOfReserve { get; set; } = float.PositiveInfinity;
        }

        /// <summary>One Anchor.Apply with the hold output and the touch set by hand, as the step would find them.</summary>
        private static void Apply(Creature body, SolverConfig config, float hold, bool touching)
        {
            body.HoldSignal[0] = hold;
            body.RockTouched[0] = touching;
            Array.Clear(body.Fext, 0, body.Fext.Length);
            Anchor.Apply(body, config, config.StepSeconds);
        }

        private static double FullPrice(AnchoringCell cell, Creature body, bool release) =>
            release
                ? cell.ReleasePrice(cell.BreakingForce((float)body.Volume[0]))
                : cell.BuildPrice(cell.BreakingForce((float)body.Volume[0]));

        // ------------------------------------------------------------------ the body

        [Fact]
        public void AGripperWithTheSwitchOnHasOneHoldOutputAndRecordsItsPull()
        {
            SolverConfig config = StillWorld(Cell());
            Creature body = Build(Gripper(0f), config);

            Assert.Equal(1, body.Brain.GripCount);
            Assert.Equal(new float[1], body.HoldSignal);
            Assert.Equal(new[] { 0 }, body.GripOutputOf);
            Assert.Equal(new double[1], body.GripPull);
            Assert.Equal(0L, body.GripsLetGo);

            // Off, and with a part that does not grip, the record is not there at all.
            SolverConfig off = StillWorld(Cell(hold: false));
            Creature plain = Build(Gripper(0f), off);
            Assert.NotNull(plain.GripHeld);
            Assert.Null(plain.HoldSignal);
            Assert.Null(plain.GripOutputOf);
            Assert.Null(plain.GripPull);
            Assert.Equal(0, plain.Brain.GripCount);

            Creature structural = Build(Gripper(0f, cellType: CellTypeIds.Structural), config);
            Assert.Null(structural.GripHeld);
            Assert.Null(structural.HoldSignal);
            Assert.Null(structural.GripPull);
        }

        // ------------------------------------------------------------------ off is bit-identical

        [Fact]
        public void AHoldBiasOfPlusThreeMatchesTheSwitchOffGripInEveryPositionAndInTheOwedJoules()
        {
            (Creature, DynamicsWorld) Run(bool on)
            {
                SolverConfig config = StillWorld(Cell(on));
                Creature body = OnTheBed(Gripper(3f), config);
                var world = new DynamicsWorld(config);
                world.Add(body);
                for (int i = 0; i < 2000; i++) world.Step();
                return (body, world);
            }

            (Creature off, _) = Run(false);
            (Creature on, _) = Run(true);

            Assert.True(off.GripHeld[0] && on.GripHeld[0]);
            Assert.True(on.HoldSignal[0] > 0.99f);
            Assert.Equal(off.Position, on.Position);
            Assert.Equal(off.Velocity, on.Velocity);
            Assert.Equal(off.Rotation, on.Rotation);
            Assert.Equal(off.GripStrength, on.GripStrength);
            Assert.Equal(off.GripOwedJoules, on.GripOwedJoules);
            Assert.Equal(off.GripsFormed, on.GripsFormed);
            Assert.Equal(off.GripHeldSeconds, on.GripHeldSeconds);
            Assert.Equal(0L, on.GripsLetGo);
            Assert.Equal(0L, on.GripsBroken);
        }

        [Fact]
        public void ABrainThatWantsToLetGoNeverGripsAndOneThatIgnoresTheOutputHolds()
        {
            SolverConfig config = StillWorld(Cell());

            Creature letting = OnTheBed(Gripper(-3f), config);
            Creature ignoring = OnTheBed(Gripper(0f), config, id: 1);
            var world = new DynamicsWorld(config);
            world.Add(letting);

            for (int i = 0; i < 100; i++) world.Step();
            Assert.False(letting.GripHeld[0]);
            Assert.Equal(0L, letting.GripsFormed);
            Assert.Equal(0d, letting.GripOwedJoules);

            var other = new DynamicsWorld(config);
            other.Add(ignoring);
            for (int i = 0; i < 100; i++) other.Step();
            Assert.True(ignoring.GripHeld[0]);
            Assert.Equal(1L, ignoring.GripsFormed);
        }

        // ------------------------------------------------------------------ the threshold

        [Fact]
        public void BelowTheThresholdAHeldGripLetsGoAndAboveItOnlyTheTouchFormsOne()
        {
            SolverConfig config = StillWorld(Cell());
            Creature body = OnTheBed(Gripper(0f), config);
            var world = new DynamicsWorld(config);
            world.Add(body);
            world.Step();
            Assert.True(body.GripHeld[0]);

            // At the threshold itself the grip holds: it lets go below it.
            Apply(body, config, -0.5f, touching: true);
            Assert.True(body.GripHeld[0]);

            // At -0.6 a held grip lets go the same step, and it is a let-go, not a break.
            Apply(body, config, -0.6f, touching: true);
            Assert.False(body.GripHeld[0]);
            Assert.Equal(1L, body.GripsLetGo);
            Assert.Equal(0L, body.GripsBroken);
            Assert.Equal(0d, body.GripStrength[0]);

            // At -0.6 on the rock nothing forms.
            Apply(body, config, -0.6f, touching: true);
            Assert.False(body.GripHeld[0]);
            Assert.Equal(1L, body.GripsFormed);

            // At +1 off the rock nothing forms. At -0.4 off the rock, nothing either.
            Apply(body, config, 1f, touching: false);
            Assert.False(body.GripHeld[0]);
            Apply(body, config, -0.4f, touching: false);
            Assert.False(body.GripHeld[0]);
            Assert.Equal(1L, body.GripsFormed);

            // Exactly at the threshold on the rock nothing forms either: a grip forms only strictly above it.
            Apply(body, config, -0.5f, touching: true);
            Assert.False(body.GripHeld[0]);
            Assert.Equal(1L, body.GripsFormed);

            // At -0.4 on the rock it forms again, with no latch to clear.
            Apply(body, config, -0.4f, touching: true);
            Assert.True(body.GripHeld[0]);
            Assert.Equal(2L, body.GripsFormed);
            Assert.Equal(1L, body.GripsLetGo);
        }

        [Fact]
        public void AThresholdOfMinusOneNeverLetsGoAndAHigherOneLetsGoSooner()
        {
            foreach ((float below, float hold, bool lets) in new[]
                     { (-1f, -0.999f, false), (-0.2f, -0.1f, false), (-0.2f, -0.3f, true), (0.5f, 0.4f, true) })
            {
                SolverConfig config = StillWorld(Cell(below: below));
                Creature body = OnTheBed(Gripper(3f), config);   // asks to hold, so it forms above any threshold here
                var world = new DynamicsWorld(config);
                world.Add(body);
                world.Step();
                Assert.True(body.GripHeld[0]);

                Apply(body, config, hold, touching: true);
                Assert.Equal(!lets, body.GripHeld[0]);
            }
        }

        // ------------------------------------------------------------------ the prices

        [Fact]
        public void FormingAndLettingGoEachPayTheirOwnPriceAndHoldingPaysNothing()
        {
            AnchoringCell cell = Cell();
            SolverConfig config = StillWorld(cell);
            Creature body = OnTheBed(Gripper(3f), config);
            var world = new DynamicsWorld(config);
            world.Add(body);

            double build = FullPrice(cell, body, release: false);
            double release = FullPrice(cell, body, release: true);
            Assert.NotEqual(build, release);

            world.Step();
            Assert.True(body.GripHeld[0]);
            Assert.Equal(build, body.GripOwedJoules, 15);

            // 500 steps held owe nothing more.
            for (int i = 0; i < 500; i++) world.Step();
            Assert.True(body.GripHeld[0]);
            Assert.Equal(build, body.GripOwedJoules, 15);
            Assert.Equal(build, body.DrainGripOwed(), 15);

            // The brain lets go: the release price, on the full breaking force and not the decayed one.
            Assert.True(body.GripStrength[0] < 1.0);
            Apply(body, config, -0.9f, touching: true);
            Assert.False(body.GripHeld[0]);
            Assert.Equal(release, body.GripOwedJoules, 15);
            Assert.Equal(release, body.DrainGripOwed(), 15);

            // Forming again pays the forming price again.
            Apply(body, config, 0.2f, touching: true);
            Assert.True(body.GripHeld[0]);
            Assert.Equal(build, body.DrainGripOwed(), 15);

            _out.WriteLine($"a 10 cm anchoring cube: forming {build:0.#######} J, letting go {release:0.#######} J");
        }

        [Fact]
        public void ABreakUnderPullAndALetGoAtAnEmptyReserveOweNoReleasePrice()
        {
            AnchoringCell cell = Cell();
            SolverConfig config = StillWorld(cell);
            config.TissueExcessDensity = 0;

            // A break: the anchor displaced past the breaking force, the output asking to hold.
            Creature broken = OnTheBed(Gripper(0f), config);
            var world = new DynamicsWorld(config);
            world.Add(broken);
            world.Step();
            Assert.True(broken.GripHeld[0]);
            broken.DrainGripOwed();
            Vec3.Write(broken.GripAnchor, 0, Vec3.Read(broken.GripAnchor, 0) + new Vec3(0, 0, 10));
            Apply(broken, config, 0.5f, touching: false);
            Assert.False(broken.GripHeld[0]);
            Assert.Equal(1L, broken.GripsBroken);
            Assert.Equal(0L, broken.GripsLetGo);
            Assert.Equal(0d, broken.GripOwedJoules);
            Assert.Equal(0d, broken.GripPull[0]);

            // A let-go with the reserve empty is the reserve's release, not the brain's, and costs nothing.
            Creature starved = OnTheBed(Gripper(0f), config, id: 1);
            var reserve = new Reserve { SecondsOfReserve = 1000f };
            starved.Senses.Reserve = reserve;
            var second = new DynamicsWorld(config);
            second.Add(starved);
            second.Step();
            Assert.True(starved.GripHeld[0]);
            starved.DrainGripOwed();
            reserve.SecondsOfReserve = 0f;
            Apply(starved, config, -0.9f, touching: true);
            Assert.False(starved.GripHeld[0]);
            Assert.Equal(1L, starved.GripsBroken);
            Assert.Equal(0L, starved.GripsLetGo);
            Assert.Equal(0d, starved.GripOwedJoules);
        }

        // ------------------------------------------------------------------ the senses

        [Fact]
        public void TheGrippingSenseReadsOneTheStepAfterFormingAndZeroTheStepAfterLettingGo()
        {
            // The body's own sense feeds its hold output: held, it asks to let go (-3); free, it asks for nothing (0,
            // above the threshold). So it forms, lets go, forms again, a step apart, and the sense is what it was at
            // the end of the step before.
            SolverConfig config = StillWorld(Cell());
            Creature body = OnTheBed(Gripper(0f, selfWeight: -3f), config);
            var world = new DynamicsWorld(config);
            world.Add(body);

            var held = new List<bool>();
            var read = new List<float>();
            for (int i = 0; i < 10; i++)
            {
                world.Step();
                held.Add(body.GripHeld[0]);
                read.Add(body.Senses.Read(0, SensorChannel.Gripping, 0));
            }

            _out.WriteLine("held after each step: " + string.Join(",", held));
            Assert.Equal(new[] { true, false, true, false, true, false, true, false, true, false }, held.ToArray());
            for (int i = 0; i < 10; i++) Assert.Equal(held[i] ? 1f : 0f, read[i]);

            Assert.Equal(5L, body.GripsFormed);
            Assert.Equal(5L, body.GripsLetGo);
            Assert.Equal(0L, body.GripsBroken);

            AnchoringCell cell = config.Anchoring;
            Assert.Equal(
                5 * FullPrice(cell, body, release: false) + 5 * FullPrice(cell, body, release: true),
                body.GripOwedJoules, 12);
        }

        [Fact]
        public void TheGripPullSenseIsThePullOverTheBreakingForceAndZeroWhenNotHolding()
        {
            AnchoringCell cell = Cell();
            SolverConfig config = StillWorld(cell);
            config.TissueExcessDensity = 0;
            Creature body = OnTheBed(Gripper(0f), config);
            var world = new DynamicsWorld(config);
            world.Add(body);

            Assert.Equal(0f, body.Senses.Read(0, SensorChannel.GripPull, 0));
            world.Step();
            Assert.True(body.GripHeld[0]);

            // Still, and pulled by a displaced anchor alone, at a share of the breaking force.
            Array.Clear(body.Velocity, 0, body.Velocity.Length);
            Array.Clear(body.Spin, 0, body.Spin.Length);
            double stiffness = body.Mass[0] * config.ContactOmega * config.ContactOmega;
            foreach (double share in new[] { 0.25, 0.5, 0.9 })
            {
                double breaking = cell.BreakingForce((float)body.Volume[0]) * body.GripStrength[0];
                Vec3 at = Vec3.Read(body.Position, 0);
                Vec3.Write(body.GripAnchor, 0, at + new Vec3(share * breaking / stiffness, 0, 0));
                Array.Clear(body.Velocity, 0, body.Velocity.Length);
                Apply(body, config, 0.5f, touching: false);

                Assert.True(body.GripHeld[0]);
                Assert.Equal(share, body.GripPull[0], 9);
                Assert.Equal((float)share, body.Senses.Read(0, SensorChannel.GripPull, 0));
            }

            // Past the breaking force the grip breaks and the pull reads 0, as it does when nothing holds.
            Vec3.Write(body.GripAnchor, 0, Vec3.Read(body.Position, 0) + new Vec3(5, 0, 0));
            Array.Clear(body.Velocity, 0, body.Velocity.Length);
            Apply(body, config, 0.5f, touching: false);
            Assert.False(body.GripHeld[0]);
            Assert.Equal(0d, body.GripPull[0]);
            Assert.Equal(0f, body.Senses.Read(0, SensorChannel.GripPull, 0));
            Assert.Equal(0f, body.Senses.Read(0, SensorChannel.Gripping, 0));

            // A let-go reads 0 too.
            Apply(body, config, 0.5f, touching: true);
            Assert.True(body.GripHeld[0]);
            Apply(body, config, -0.9f, touching: true);
            Assert.False(body.GripHeld[0]);
            Assert.Equal(0d, body.GripPull[0]);
        }

        [Fact]
        public void APartWithoutTheCellReadsZeroOnBothSenses()
        {
            SolverConfig config = StillWorld(Cell());
            Creature body = Build(Gripper(0f, cellType: CellTypeIds.Structural), config);
            Assert.Equal(0f, body.Senses.Read(0, SensorChannel.Gripping, 0));
            Assert.Equal(0f, body.Senses.Read(0, SensorChannel.GripPull, 0));
            Assert.Equal(0f, body.Senses.Read(5, SensorChannel.GripPull, 0));
        }

        // ------------------------------------------------------------------ carrying

        [Fact]
        public void ARebuiltBodyKeepsItsPullRecordAndItsLetGoCount()
        {
            SolverConfig config = StillWorld(Cell());
            Genome g = Gripper(0f);
            Creature before = OnTheBed(g, config);
            var world = new DynamicsWorld(config);
            world.Add(before);
            world.Step();
            Assert.True(before.GripHeld[0]);
            before.GripPull[0] = 0.3;
            before.GripsLetGo = 4;

            Creature after = Build(g, config);
            after.AdoptStateFrom(before, new[] { 0 });

            Assert.True(after.GripHeld[0]);
            Assert.Equal(0.3, after.GripPull[0]);
            Assert.Equal(4L, after.GripsLetGo);
            Assert.Equal(1L, after.GripsFormed);
        }

        [Fact]
        public void RemainsHoldNoOutputAndFeelNoPull()
        {
            SolverConfig config = StillWorld(Cell());
            Creature body = OnTheBed(Gripper(0f), config);
            var world = new DynamicsWorld(config);
            world.Add(body);
            world.Step();
            body.GripPull[0] = 0.4;
            body.HoldSignal[0] = 0.7f;

            body.BecomeRemains();
            Assert.False(body.GripHeld[0]);
            Assert.Equal(0d, body.GripPull[0]);
            Assert.Equal(0f, body.HoldSignal[0]);
            Assert.Equal(0f, body.Senses.Read(0, SensorChannel.GripPull, 0));

            for (int i = 0; i < 20; i++) world.Step();
            Assert.False(body.GripHeld[0]);
            Assert.Equal(0L, body.GripsLetGo);
        }

        // ------------------------------------------------------------------ identity

        [Fact]
        public void TheBrainHeldGripIsTheSameAtAnyThreadCount()
        {
            ulong Run(int threads)
            {
                SolverConfig config = StillWorld(Cell());
                var world = new DynamicsWorld(config) { Threads = threads };
                for (int i = 0; i < 8; i++)
                {
                    Creature body = Build(Gripper(i % 2 == 0 ? 0f : 0.5f, selfWeight: i % 3 == 0 ? -3f : 0f), config, i);
                    double radius = body.LinkContactRadius[0];
                    body.PlaceAt(new Vec3(-3 + 0.8 * i, -config.WorldDepthMetres + radius - 0.01 + 0.03 * (i % 2), 0.5 * (i % 3)), QuatD.Identity);
                    world.Add(body);
                }

                for (int s = 0; s < 600; s++) world.Step();
                long formed = 0, letGo = 0;
                foreach (Creature c in world.Creatures)
                {
                    formed += c.GripsFormed;
                    letGo += c.GripsLetGo;
                }

                _out.WriteLine($"{threads} thread(s): {formed} grips formed, {letGo} let go");
                Assert.True(letGo > 0, "no grip was ever let go, so nothing was tested");
                return world.Digest();
            }

            Assert.Equal(Run(1), Run(16));
        }
    }
}
