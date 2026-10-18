using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D162's touch of the rock: each link's record of being against the bed or a reef, and
    /// <see cref="SensorChannel.BedTouch"/>, which reads it one physics step late.
    /// </summary>
    public sealed class BedTouchTests
    {
        private readonly ITestOutputHelper _out;

        public BedTouchTests(ITestOutputHelper output) => _out = output;

        private static readonly Float3 Half = new Float3(0.3f, 0.05f, 0.1f);

        private static SolverConfig World(bool touch, bool reef = false, double tank = 0)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.WorldDepthMetres = 12;
            config.FloorIsSolid = true;
            config.CreatureContact = true;
            config.ContactPerPart = true;
            config.RockTouch = touch;
            config.TankRadiusMetres = tank;
            if (reef) config.Reefs = new ReefGeometry(3f, 3f, 2f, 1f, 3f, new[] { 0d }, new[] { 0d });
            return config;
        }

        /// <summary>A flat plate, thin along y, whose one neuron reads the touch of the rock.</summary>
        private static Creature Plate(SolverConfig config, Vec3 at)
        {
            Genome g = TestBodies.Box(Half);
            g.Nodes[0].Neurons = new[]
            {
                new NeuronDef
                {
                    Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f,
                    Inputs = new[] { NeuronInput.FromSensor(SensorChannel.BedTouch, 0) },
                },
            };
            Creature body = TestBodies.Build(g, config);
            Place(body, at);
            return body;
        }

        private static void Place(Creature body, Vec3 at)
        {
            body.PlaceAt(at, QuatD.Identity);
            body.RefreshContactSphere();
            body.CommitContactSphere();
        }

        private static void ContactPass(SolverConfig config, Creature body)
        {
            var list = new List<Creature> { body };
            var grid = new ContactGrid();
            grid.BuildLinks(list);
            int[] scratch = new int[8];
            Array.Clear(body.Fext, 0, body.Fext.Length);
            Contacts.Apply(body, 0, grid, config, ref scratch);
        }

        [Fact]
        public void APlateOnTheBedOrOnAReefCapTouchesAndOneInMidWaterOrAtTheGlassDoesNot()
        {
            SolverConfig flat = World(touch: true, tank: 6);
            Creature body = Plate(flat, Vec3.Zero);
            double radius = body.LinkContactRadius[0];

            // Its sphere 1 cm into the bed.
            Place(body, new Vec3(0, -12 + radius - 0.01, 0));
            ContactPass(flat, body);
            Assert.True(body.RockTouched[0], "a plate on the bed reads no touch");
            Vec3 normal = Vec3.Read(body.RockNormal, 0);
            Vec3 point = Vec3.Read(body.RockPoint, 0);
            Assert.Equal(1.0, normal.Y, 12);
            Assert.Equal(-12 - 0.01, point.Y, 9);

            // Mid-water.
            Place(body, new Vec3(0, -6, 0));
            ContactPass(flat, body);
            Assert.False(body.RockTouched[0], "a plate in mid-water reads a touch");

            // Pressed 1 cm into the glass at mid-depth: pushed, and not rock. The tank's axis
            // stands at (R, R) (SolverConfig.TankAxisX).
            Place(body, new Vec3(flat.TankAxisX + 6 - radius + 0.01, -6, flat.TankAxisZ));
            ContactPass(flat, body);
            Assert.True(Vec3.Read(body.Fext, 3).X < 0, "the glass did not push");
            Assert.False(body.RockTouched[0], "the glass reads as rock");

            // On the reef's cap, whose top is at y -3: the normal points up out of the cap.
            SolverConfig reef = World(touch: true, reef: true);
            Creature onCap = Plate(reef, new Vec3(0.5, -3 + radius - 0.01, 0.5));
            ContactPass(reef, onCap);
            Assert.True(onCap.RockTouched[0], "a plate on a reef cap reads no touch");
            Assert.True(Vec3.Read(onCap.RockNormal, 0).Y > 0.99, "the cap's normal does not point up");
        }

        [Fact]
        public void TheSenseReadsTheTouchOnePhysicsStepLate()
        {
            SolverConfig config = World(touch: true);
            config.TissueExcessDensity = 0;
            Creature body = Plate(config, Vec3.Zero);
            Place(body, new Vec3(0, -12 + body.LinkContactRadius[0] - 0.01, 0));

            var world = new DynamicsWorld(config);
            world.Add(body);

            // Step 1 samples before its contact pass, which is the first to see the bed.
            world.Step();
            Assert.True(body.RockTouched[0]);
            Assert.Equal(0f, body.Senses.Read(0, SensorChannel.BedTouch, 0));

            // Step 2 samples what step 1's pass recorded.
            world.Step();
            Assert.Equal(1f, body.Senses.Read(0, SensorChannel.BedTouch, 0));
        }

        /// <summary>
        /// The design's question about early contact, answered with a number: the touch is read on
        /// the part's contact sphere, so a part lying flat reads 1 while its face is still this far
        /// above the rock.
        /// </summary>
        [Fact]
        public void APlateLyingFlatReadsTheTouchBeforeItsFaceArrives()
        {
            SolverConfig config = World(touch: true);
            Creature body = Plate(config, Vec3.Zero);
            double radius = body.LinkContactRadius[0];

            // Lowered in millimetres until the record first fires.
            double gap = double.NaN;
            for (int mm = 400; mm >= 0; mm--)
            {
                double face = mm / 1000.0;
                Place(body, new Vec3(0, -12 + Half.Y + face, 0));
                ContactPass(config, body);
                if (body.RockTouched[0]) { gap = face; break; }
            }

            _out.WriteLine(
                $"a plate {2 * Half.X} by {2 * Half.Y} by {2 * Half.Z} m lying flat: sphere radius {radius:0.###} m; " +
                $"the touch fires with its lower face {gap * 1000:0} mm above the bed");

            // The first millimetre below the sphere's own reach.
            Assert.InRange(gap, radius - Half.Y - 0.0015, radius - Half.Y);
        }

        [Fact]
        public void TheRecordMovesNothing()
        {
            ulong Run(bool touch)
            {
                SolverConfig config = World(touch, reef: true);
                config.TissueExcessDensity = 50;
                var world = new DynamicsWorld(config);
                for (int i = 0; i < 6; i++)
                {
                    Creature body = TestBodies.Build(TestBodies.Chain(2 + i % 2), config, i);
                    body.PlaceAt(new Vec3(-1.5 + 0.6 * i, -2.0 - 0.3 * i, 0.4 * (i % 3)), QuatD.Identity);
                    world.Add(body);
                }
                for (int s = 0; s < 600; s++) world.Step();
                return world.Digest();
            }

            Assert.Equal(Run(false), Run(true));
        }

        [Fact]
        public void TheRecordNeedsPerPartContact()
        {
            SolverConfig config = World(touch: true);
            config.ContactPerPart = false;
            Assert.Throws<ArgumentException>(() => TestBodies.Build(TestBodies.Box(Half), config));
        }
    }
}
