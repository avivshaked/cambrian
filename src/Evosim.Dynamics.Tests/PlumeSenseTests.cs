using System;
using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D270's carried leaf smell and current sense as a body reads them: the strength and the gradient from the
    /// plume, the water's velocity over the ground in the part's own frame, the mirrored part, the nose world, the
    /// hold through a block, and a world with both off.
    /// </summary>
    public sealed class PlumeSenseTests
    {
        private readonly ITestOutputHelper _out;

        public PlumeSenseTests(ITestOutputHelper output) => _out = output;

        private const double PlumeHalf = 0.5;
        private const double CurrentHalf = 0.01;

        private static readonly DevelopmentLimits Reflecting = new DevelopmentLimits { MaxParts = 64, ReflectMirroredAnchors = true };

        // ------------------------------------------------------------------ the worlds

        /// <summary>A plume over a 40 by 40 by 20 m box of 5 m cells whose density rises along x: 1 + 0.5 ix.</summary>
        private static LeafPlumeField Ramp()
        {
            var plume = new LeafPlumeField(new GridField(1600f, 0f, 20f, 0f, 0f, 1, 5f));
            double volume = plume.Grid.CellVolume;
            for (int iy = 0; iy < plume.CellsY; iy++)
            for (int ix = 0; ix < plume.CellsX; ix++)
            for (int iz = 0; iz < plume.CellsZ; iz++)
            {
                var centre = new Float3((ix + 0.5f) * 5f, -(iy + 0.5f) * 5f, (iz + 0.5f) * 5f);
                plume.Deposit(centre, (1d + 0.5d * ix) * volume);
            }

            plume.RebuildGradient();
            return plume;
        }

        private static SolverConfig Config(bool plume, bool current, LeafPlumeForm form = LeafPlumeForm.Gradient)
        {
            SolverConfig config = TestBodies.Water(0.01);
            if (plume)
            {
                config.LeafPlume = form;
                config.LeafPlumeHalfScale = PlumeHalf;
            }

            if (current)
            {
                config.CurrentSense = true;
                config.CurrentSenseHalfSpeedMetresPerSecond = CurrentHalf;
            }

            return config;
        }

        private static NeuronDef Reads(params (SensorChannel Channel, int Index)[] inputs) => new NeuronDef
        {
            Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f,
            Inputs = inputs.Select(i => NeuronInput.FromSensor(i.Channel, i.Index)).ToArray(),
        };

        /// <summary>The neurons that put every one of D270's channels on the brain's mask.</summary>
        private static NeuronDef[] ReadsFar() => new[]
        {
            Reads((SensorChannel.LeafPlume, 0)),
            Reads((SensorChannel.LeafPlumeGradient, 0), (SensorChannel.LeafPlumeGradient, 1), (SensorChannel.LeafPlumeGradient, 2)),
            Reads((SensorChannel.GroundCurrent, 0), (SensorChannel.GroundCurrent, 1), (SensorChannel.GroundCurrent, 2)),
        };

        /// <summary>One box that reads the far senses.</summary>
        private static Creature Box(SolverConfig config, QuatD attitude, Vec3 at)
        {
            Genome g = TestBodies.Box(new Float3(0.2f, 0.1f, 0.1f));
            g.Nodes[0].Neurons = ReadsFar();
            Creature body = TestBodies.Build(g, config);
            body.PlaceAt(at, attitude);
            return body;
        }

        private static void SetWater(Creature body, double x, double y, double z)
        {
            for (int link = 0; link < body.Links; link++)
            {
                body.Water[3 * link] = x;
                body.Water[3 * link + 1] = y;
                body.Water[3 * link + 2] = z;
            }
        }

        private static Vec3 Current(Creature body, int part) => new Vec3(
            body.Senses.Read(part, SensorChannel.GroundCurrent, 0),
            body.Senses.Read(part, SensorChannel.GroundCurrent, 1),
            body.Senses.Read(part, SensorChannel.GroundCurrent, 2));

        private static Vec3 Gradient(Creature body, int part) => new Vec3(
            body.Senses.Read(part, SensorChannel.LeafPlumeGradient, 0),
            body.Senses.Read(part, SensorChannel.LeafPlumeGradient, 1),
            body.Senses.Read(part, SensorChannel.LeafPlumeGradient, 2));

        // ------------------------------------------------------------------ the current

        [Fact]
        public void TheCurrentTurnsWithThePart()
        {
            var at = new Vec3(20, -10, 20);

            Creature level = Box(Config(false, true), QuatD.Identity, at);
            SetWater(level, 0.01, 0, 0);
            level.Senses.Sample();
            Vec3 a = Current(level, 0);

            // A quarter about y: the body's own -z faces where the water's +x pushes.
            QuatD turned = QuatD.FromAxisAngle(new Vec3(0, 1, 0), Math.PI / 2);
            Creature yawed = Box(Config(false, true), turned, at);
            SetWater(yawed, 0.01, 0, 0);
            yawed.Senses.Sample();
            Vec3 b = Current(yawed, 0);
            Vec3 world = Mat3.Read(yawed.RotationMatrix, 0) * b;

            _out.WriteLine($"level ({a.X:0.####}, {a.Y:0.####}, {a.Z:0.####}); yawed in the part ({b.X:0.####}, {b.Y:0.####}, {b.Z:0.####}), in the world ({world.X:0.####}, {world.Y:0.####}, {world.Z:0.####})");

            Assert.Equal(0.5, a.X, 5);
            Assert.True(Math.Abs(a.Y) < 1e-6 && Math.Abs(a.Z) < 1e-6);
            Assert.True(Math.Abs(b.X) < 1e-5, "the part's own frame did not turn the reading");
            Assert.Equal(a.X, world.X, 5);
            Assert.Equal(a.Magnitude, b.Magnitude, 5);
        }

        [Theory]
        [InlineData(0.01, 0.5)]
        [InlineData(0.02, 2d / 3d)]
        [InlineData(0.0, 0.0)]
        public void TheCurrentsLengthIsSpeedOverSpeedPlusTheHalfSpeed(double speed, double expected)
        {
            Creature body = Box(Config(false, true), QuatD.Identity, new Vec3(20, -10, 20));
            SetWater(body, 0.6 * speed, 0, 0.8 * speed);
            body.Senses.Sample();
            Assert.Equal(expected, Current(body, 0).Magnitude, 5);
        }

        [Fact]
        public void AMirroredPartReadsTheCurrentInTheFrameItsGeometryStandsIn()
        {
            Genome genome = TestBodies.Chain(2, neurons: false);
            genome.Nodes[0].Edges[0].Reflect = new Bool3(true, false, false);
            genome.Nodes[1].Neurons = ReadsFar();

            SolverConfig config = Config(false, true);
            Phenotype adult = Developer.Develop(genome, Reflecting);
            Assert.Equal(3, adult.Parts.Count);
            Assert.True(adult.Parts[2].Mirrored);
            var body = new Creature(0, adult, config);
            body.PlaceAsDeveloped(new Vec3(20, -10, 20), adult);

            // Water in the mirror plane (normal x) is its own mirror image: the twin reads what the original does.
            SetWater(body, 0, 0.05, 0.2);
            body.Senses.Sample();
            for (int axis = 0; axis < 3; axis++)
            {
                float one = body.Senses.Read(1, SensorChannel.GroundCurrent, axis);
                float two = body.Senses.Read(2, SensorChannel.GroundCurrent, axis);
                _out.WriteLine($"in the plane, axis {axis}: original {one:0.######}, twin {two:0.######}");
                Assert.Equal(one, two, 4);
            }

            // Water along the normal: the twin reads what its original reads of the water mirrored, the
            // component along the reflection's normal negated.
            SetWater(body, 0.2, 0.1, 0.05);
            body.Senses.Sample();
            var twin = new[] { body.Senses.Read(2, SensorChannel.GroundCurrent, 0), body.Senses.Read(2, SensorChannel.GroundCurrent, 1), body.Senses.Read(2, SensorChannel.GroundCurrent, 2) };
            Assert.True(Math.Abs(twin[0]) + Math.Abs(twin[1]) + Math.Abs(twin[2]) > 0.1f);

            SetWater(body, -0.2, 0.1, 0.05);
            body.Senses.Sample();
            for (int axis = 0; axis < 3; axis++)
            {
                float original = body.Senses.Read(1, SensorChannel.GroundCurrent, axis);
                _out.WriteLine($"along the normal, axis {axis}: twin {twin[axis]:0.######}, original on the mirrored water {original:0.######}");
                Assert.Equal(original, twin[axis], 4);
            }
        }

        // ------------------------------------------------------------------ the plume

        [Fact]
        public void TheStrengthIsTheSquashedInterpolatedDensityAndTheGradientPointsUpTheRampInThePartsFrame()
        {
            LeafPlumeField plume = Ramp();
            var at = new Vec3(21, -10, 20);

            Creature level = Box(Config(true, false), QuatD.Identity, at);
            level.Senses.LeafPlume = plume;
            level.Senses.Sample();

            plume.Sample(new Float3(21f, -10f, 20f), out double density, out _, out _, out _);
            float expected = (float)(density / (density + PlumeHalf));
            float strength = level.Senses.Read(0, SensorChannel.LeafPlume, 0);
            Vec3 d = Gradient(level, 0);
            _out.WriteLine($"density {density:0.####}, strength {strength:0.####}, gradient ({d.X:0.####}, {d.Y:0.####}, {d.Z:0.####})");

            Assert.True(density > 0d);
            Assert.Equal(expected, strength, 6);
            Assert.True(d.X > 0, "the gradient does not point up the ramp");
            Assert.Equal(strength, d.Magnitude, 5);
            Assert.True(Math.Abs(d.Y) < 1e-4 * strength && Math.Abs(d.Z) < 1e-4 * strength);

            // Turned a quarter about y, the same gradient is read in the part's own frame.
            QuatD turned = QuatD.FromAxisAngle(new Vec3(0, 1, 0), Math.PI / 2);
            Creature yawed = Box(Config(true, false), turned, at);
            yawed.Senses.LeafPlume = plume;
            yawed.Senses.Sample();
            Vec3 e = Gradient(yawed, 0);
            Vec3 world = Mat3.Read(yawed.RotationMatrix, 0) * e;
            Assert.Equal(strength, yawed.Senses.Read(0, SensorChannel.LeafPlume, 0), 6);
            Assert.True(Math.Abs(e.X) < 1e-4 * strength, "the part's own frame did not turn the reading");
            Assert.Equal(d.X, world.X, 4);

            // No plume is no smell.
            Creature blind = Box(Config(true, false), QuatD.Identity, at);
            blind.Senses.Sample();
            Assert.Equal(0f, blind.Senses.Read(0, SensorChannel.LeafPlume, 0));
            Assert.Equal(0d, Gradient(blind, 0).Magnitude);
        }

        [Fact]
        public void TheStrengthHasNoStepAtACellBoundary()
        {
            LeafPlumeField plume = Ramp();

            // x = 20 is a boundary of the 5 m cells; the read a hair either side differs by a hair.
            float below = Read(plume, 19.999f), above = Read(plume, 20.001f);
            _out.WriteLine($"strength {below:0.######} just below, {above:0.######} just above");
            Assert.True(Math.Abs(above - below) < 1e-3f);

            static float Read(LeafPlumeField p, float x)
            {
                Creature body = Box(Config(true, false, LeafPlumeForm.Strength), QuatD.Identity, new Vec3(x, -10, 20));
                body.Senses.LeafPlume = p;
                body.Senses.Sample();
                return body.Senses.Read(0, SensorChannel.LeafPlume, 0);
            }
        }

        // ------------------------------------------------------------------ the nose world

        private static Creature Snout(NoseCell cell, Vec3 at, bool farmed = false)
        {
            SolverConfig config = Config(true, true);
            config.Nose = cell;

            Genome g = TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f));
            g.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.15f, 0.05f, 0.05f), CellTypeId = CellTypeIds.Nose, ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed, JointLimits = Array.Empty<Float2>(), RecursiveLimit = 1,
            });
            g.Nodes[0].Edges.Add(new MorphEdge
            {
                Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity, Scale = Float3.One,
            });
            foreach (MorphNode n in g.Nodes) n.Neurons = ReadsFar();

            Phenotype body = Developer.Develop(g, DevelopmentLimits.Default, cellTypes: CellTypeRegistry.Standard.Appending(cell));
            var c = new Creature(0, body, config);
            c.PlaceAt(at, QuatD.Identity);
            c.Senses.LeafPlume = Ramp();
            if (farmed)
            {
                c.Senses.HeldSniff = true;
                c.Senses.NoiseSeed = 99UL;
                c.Senses.BodyId = 7;
            }

            return c;
        }

        private static Vec3 Tip(Creature c, int part)
        {
            Float3 t = MouthPoint.Local(c.Phenotype, part);
            return Vec3.Read(c.Position, 3 * part) + Mat3.Read(c.RotationMatrix, 9 * part) * new Vec3(t.X, t.Y, t.Z);
        }

        [Fact]
        public void ANoseWorldGivesThePlumeToNosesAloneAtTheirTipsAndTheCurrentToEveryPart()
        {
            Creature c = Snout(new NoseCell(leafNoise: 0f, carrionNoise: 0f, chemicalNoise: 0f), new Vec3(20.2, -10, 20));
            SetWater(c, 0.01, 0, 0);
            c.Senses.Sample();

            // The root smells nothing of the plume.
            Assert.Equal(0f, c.Senses.Read(0, SensorChannel.LeafPlume, 0));
            Assert.Equal(0d, Gradient(c, 0).Magnitude);

            // The nose reads it at its tip, with no noise.
            Vec3 tip = Tip(c, 1);
            c.Senses.LeafPlume.Sample(new Float3((float)tip.X, (float)tip.Y, (float)tip.Z), out double density, out _, out _, out _);
            float want = (float)(density / (density + PlumeHalf));
            float read = c.Senses.Read(1, SensorChannel.LeafPlume, 0);
            _out.WriteLine($"tip x {tip.X:0.###}: density {density:0.####}, read {read:0.####}");
            Assert.True(read > 0f);
            Assert.Equal(want, read, 6);
            Assert.True(Gradient(c, 1).X > 0);

            // The current is every part's.
            Assert.Equal(0.5, Current(c, 0).Magnitude, 5);
            Assert.Equal(0.5, Current(c, 1).Magnitude, 5);

            // The ports follow the same rule: plume ports on the nose alone, the current's on both parts.
            var pool = new RunConfig { LeafPlume = LeafPlumeForm.Gradient, CurrentSense = true }.SensorPool();
            List<BrainPort> ports = CentralWiring.Ports(c.Phenotype, pool);
            Assert.Equal(new[] { 1 }, ports.Where(p => p.Channel == SensorChannel.LeafPlume).Select(p => p.Part).ToArray());
            Assert.Equal(new[] { 1, 1, 1 }, ports.Where(p => p.Channel == SensorChannel.LeafPlumeGradient).Select(p => p.Part).ToArray());
            Assert.Equal(new[] { 0, 0, 0, 1, 1, 1 }, ports.Where(p => p.Channel == SensorChannel.GroundCurrent).Select(p => p.Part).ToArray());
        }

        // ------------------------------------------------------------------ the hold

        [Fact]
        public void TheReadingsHoldThroughABlockUntilTheNextHold()
        {
            Creature c = Snout(new NoseCell(leafNoise: 0f, carrionNoise: 0f, chemicalNoise: 0f), new Vec3(20.2, -10, 20), farmed: true);
            SetWater(c, 0.01, 0, 0);

            // A farmed body reads nothing until the farm holds: Sample leaves it alone.
            c.Senses.Sample();
            Assert.Equal(0f, c.Senses.Read(1, SensorChannel.LeafPlume, 0));
            Assert.Equal(0d, Current(c, 0).Magnitude);

            c.Senses.HoldFar();
            float strength = c.Senses.Read(1, SensorChannel.LeafPlume, 0);
            Vec3 current = Current(c, 1);
            Assert.True(strength > 0f);
            Assert.Equal(0.5, current.Magnitude, 5);

            // The body moves and the water changes mid-block: the held readings stand.
            c.PlaceAt(new Vec3(30.5, -10, 20), QuatD.Identity);
            SetWater(c, 0.02, 0, 0);
            c.Senses.Sample();
            Assert.Equal(strength, c.Senses.Read(1, SensorChannel.LeafPlume, 0));
            Assert.Equal(current.X, Current(c, 1).X);

            // And the next hold moves them.
            c.Senses.HoldFar();
            Assert.NotEqual(strength, c.Senses.Read(1, SensorChannel.LeafPlume, 0));
            Assert.Equal(2d / 3d, Current(c, 1).Magnitude, 5);

            // The card's copy is these floats.
            c.Senses.HeldFar(1, out float plume, out float gx, out float gy, out float gz, out float cx, out float cy, out float cz);
            Assert.Equal(c.Senses.Read(1, SensorChannel.LeafPlume, 0), plume);
            Assert.Equal(c.Senses.Read(1, SensorChannel.LeafPlumeGradient, 0), gx);
            Assert.Equal(c.Senses.Read(1, SensorChannel.GroundCurrent, 0), cx);
            Assert.Equal(0f, gy + gz + cy + cz);
        }

        // ------------------------------------------------------------------ both off

        [Fact]
        public void WithBothSwitchesOffThePortsAndTheReadingsAreWhatTheyWere()
        {
            Assert.Same(SensorChannels.DefaultPool, new RunConfig().SensorPool());

            Genome g = TestBodies.Chain(3);
            Phenotype body = Developer.Develop(g, DevelopmentLimits.Default);
            List<BrainPort> off = CentralWiring.Ports(body, new RunConfig().SensorPool());
            List<BrainPort> on = CentralWiring.Ports(body, new RunConfig { LeafPlume = LeafPlumeForm.Gradient, CurrentSense = true }.SensorPool());
            Assert.DoesNotContain(off, p => p.Channel == SensorChannel.LeafPlume || p.Channel == SensorChannel.LeafPlumeGradient || p.Channel == SensorChannel.GroundCurrent);
            Assert.Equal(off.Count + body.Parts.Count * 7, on.Count);

            // A body that reads none of them, with the world's far senses off, holds nothing and reads zeros.
            Creature plain = TestBodies.Build(TestBodies.Chain(3), TestBodies.Water(0.01));
            SetWater(plain, 0.3, 0.2, 0.1);
            plain.Senses.LeafPlume = Ramp();
            plain.Senses.Sample();
            plain.Senses.HoldFar();
            for (int part = 0; part < plain.Links; part++)
            {
                Assert.Equal(0f, plain.Senses.Read(part, SensorChannel.LeafPlume, 0));
                Assert.Equal(0d, Gradient(plain, part).Magnitude);
                Assert.Equal(0d, Current(plain, part).Magnitude);
            }
        }

        [Fact]
        public void APartThatIsNotFiniteReadsZeros()
        {
            Creature body = Box(Config(true, true), QuatD.Identity, new Vec3(21, -10, 20));
            body.Senses.LeafPlume = Ramp();
            SetWater(body, 0.01, 0, 0);
            body.Position[0] = double.NaN;
            body.Senses.Sample();

            Assert.Equal(0f, body.Senses.Read(0, SensorChannel.LeafPlume, 0));
            Assert.Equal(0d, Gradient(body, 0).Magnitude);
            Assert.Equal(0d, Current(body, 0).Magnitude);
        }
    }
}
