using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D220, contact eating: a body whose organism is remains is a physical body and nothing more. The solver runs
    /// no senses and no brain for it, pushes no stroke and no drive torque, gives it no float lift and holds no grip;
    /// drag, buoyancy, gravity, contact and the joints' own limits act on it as on any body. With the flag off the
    /// step is the one it always was (the farm's dial-off digest holds that to the bit).
    /// </summary>
    public sealed class RemainsBodyTests
    {
        private readonly ITestOutputHelper _out;

        public RemainsBodyTests(ITestOutputHelper output) => _out = output;

        private static SolverConfig Water()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = 0.3;
            config.PowerPushRadiansPerSecond = 6.0;
            config.PoseHertz = 2.0;
            return config;
        }

        /// <summary>A two-link chain whose link's brain holds a full power and a bent joint.</summary>
        private static Creature Swimmer(SolverConfig config)
        {
            Genome g = TestBodies.Chain(links: 2, power: 10f);
            MorphNode link = g.Nodes[1];
            link.Neurons = new[]
            {
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 1f, Inputs = Array.Empty<NeuronInput>() },
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0.8f, Inputs = Array.Empty<NeuronInput>() },
            };
            link.Drive = new DriveGenes(0f, 0.05f, 0.05f, 0.05f);

            Creature body = TestBodies.Build(g, config);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
            return body;
        }

        [Fact]
        public void AFlaggedBodyRunsNoBrainAndPushesNoStrokeWhileAnUnflaggedOneSwims()
        {
            SolverConfig config = Water();

            var liveWorld = new DynamicsWorld(config);
            Creature live = Swimmer(config);
            liveWorld.Add(live);

            var deadWorld = new DynamicsWorld(config);
            Creature dead = Swimmer(config);
            deadWorld.Add(dead);

            // Let the second one swim a little so it has a stroke and a joint angle to lose, then it dies.
            for (int i = 0; i < 300; i++)
            {
                liveWorld.Step();
                deadWorld.Step();
            }

            Assert.True(dead.Drive.Stroke[0] > 0.5, "the body never took a stroke before it died");
            Assert.NotEqual(0f, dead.DriveSignal[0]);

            dead.BecomeRemains();
            Assert.True(dead.Remains);
            Assert.False(live.Remains);
            Assert.Equal(0.0, dead.Drive.Stroke[0]);
            Assert.True(dead.DrainMechanicalWork() > 0.0, "the swimmer drew no work while it lived");

            Array.Clear(dead.DriveSignal, 0, dead.DriveSignal.Length);
            Array.Clear(dead.BendSignal, 0, dead.BendSignal.Length);
            Vec3 diedAt = Vec3.Read(dead.Position, 0);
            Vec3.Write(dead.Velocity, 0, Vec3.Zero);
            Vec3.Write(dead.Spin, 0, Vec3.Zero);
            Array.Clear(dead.Qd, 0, dead.Qd.Length);

            for (int i = 0; i < 1500; i++)
            {
                liveWorld.Step();
                deadWorld.Step();
            }

            double liveTravel = (Vec3.Read(live.Position, 0) - new Vec3(0, -20, 0)).Magnitude;
            double deadTravel = (Vec3.Read(dead.Position, 0) - diedAt).Magnitude;
            _out.WriteLine($"the swimmer went {liveTravel:0.###} m; the body that died went {deadTravel:0.#####} m after it, stroke {dead.Drive.Stroke[0]}");

            // No brain: the signals the brain writes every step stay where they were zeroed. The live body's are
            // written on every step.
            Assert.Equal(0f, dead.DriveSignal[0]);
            Assert.Equal(0f, dead.BendSignal[0]);
            Assert.NotEqual(0f, live.DriveSignal[0]);

            // No stroke and no drive torque: in still water with no weight a limp body at rest goes nowhere, but for
            // the settling of its own joint, a few millimetres of recoil against the swimmer's metres.
            Assert.Equal(0.0, dead.Drive.Stroke[0]);
            Assert.True(deadTravel < 0.02, $"a limp body at rest in still water drifted {deadTravel} m");
            Assert.True(deadTravel < liveTravel / 10, $"the body that died drifted {deadTravel} m against the swimmer's {liveTravel} m");
            Assert.True(liveTravel > 0.2, $"the live swimmer went only {liveTravel} m");

            // No work is drawn.
            Assert.Equal(0.0, dead.DrainMechanicalWork());
        }

        [Fact]
        public void AFlaggedBodyIsLimpItsJointsSettleAndAreNotHeldAtTheBend()
        {
            SolverConfig config = Water();
            var world = new DynamicsWorld(config);
            Creature body = Swimmer(config);
            world.Add(body);

            for (int i = 0; i < 500; i++) world.Step();

            double bent = body.Q[0];
            Assert.True(Math.Abs(bent) > 0.3, $"the live joint was bent only {bent} rad");

            body.BecomeRemains();

            // Pushed off the bend, a limp joint is not pulled back to it: the muscle is gone, only the joint's own
            // damper and limits act.
            body.Q[0] = 0.0;
            Array.Clear(body.Qd, 0, body.Qd.Length);
            Vec3.Write(body.Velocity, 0, Vec3.Zero);
            Vec3.Write(body.Spin, 0, Vec3.Zero);
            Kinematics.Poses(body);
            Kinematics.Velocities(body);

            for (int i = 0; i < 500; i++) world.Step();

            _out.WriteLine($"live bend {bent:0.###} rad; limp joint after 5 s {body.Q[0]:0.#####} rad");
            Assert.True(Math.Abs(body.Q[0]) < 0.05, $"a limp joint went back to {body.Q[0]} rad");
        }

        [Fact]
        public void AFlaggedBodyTakesNoFloatLiftAndSinksWhereAFloatingOneDoesNot()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.TissueExcessDensity = 50.0;       // a body is heavier than the water by 5%, as the farm's is
            config.WorldDepthMetres = 1e6;
            config.FloorIsSolid = false;

            Creature Float()
            {
                Genome g = TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f));
                g.Nodes[0].CellTypeId = CellTypeIds.Buoyancy;
                g.Nodes[0].Lift = 1f;
                Creature body = TestBodies.Build(g, config);
                body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
                return body;
            }

            Creature floating = Float();
            Creature remains = Float();
            Assert.Equal(1.0, floating.Lift[0]);
            remains.BecomeRemains();

            var a = new DynamicsWorld(config);
            a.Add(floating);
            var b = new DynamicsWorld(config);
            b.Add(remains);

            for (int i = 0; i < 500; i++)
            {
                a.Step();
                b.Step();
            }

            double floatingY = floating.Position[1] + 20.0;
            double remainsY = remains.Position[1] + 20.0;
            _out.WriteLine($"after 5 s the float moved {floatingY:0.####} m in height and the remains {remainsY:0.####} m");

            // Full lift cancels the whole of the weight; the flag takes the lift away and the body sinks.
            Assert.True(Math.Abs(floatingY) < 1e-6, $"a full float moved {floatingY} m");
            Assert.True(remainsY < -0.01, $"the remains sank only {remainsY} m");
        }

        [Fact]
        public void AFlaggedBodyStillDragsAndMovesWithTheWater()
        {
            SolverConfig config = TestBodies.Water(0.01);

            Creature Moving()
            {
                Creature body = TestBodies.Build(TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f)), config);
                body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
                Vec3.Write(body.Velocity, 0, new Vec3(1.0, 0, 0));
                return body;
            }

            Creature free = Moving();
            Creature remains = Moving();
            remains.BecomeRemains();

            var a = new DynamicsWorld(config);
            a.Add(free);
            var b = new DynamicsWorld(config);
            b.Add(remains);

            for (int i = 0; i < 100; i++)
            {
                a.Step();
                b.Step();
            }

            _out.WriteLine($"speed after 1 s: free {free.Velocity[0]:0.#####}, remains {remains.Velocity[0]:0.#####}");

            // Drag acts on remains exactly as on a body that lives: the two states are the same to the bit.
            Assert.True(remains.Velocity[0] < 1.0);
            Assert.True(remains.Velocity[0] > 0.0);
            Assert.Equal(free.Velocity[0], remains.Velocity[0]);
            Assert.Equal(free.Position[0], remains.Position[0]);
        }

        [Fact]
        public void BecomingRemainsLeavesTheBodysPoseVelocityAndJointsExactlyAsTheyWere()
        {
            SolverConfig config = Water();
            var world = new DynamicsWorld(config);
            Creature body = Swimmer(config);
            world.Add(body);

            for (int i = 0; i < 200; i++) world.Step();

            double[] position = (double[])body.Position.Clone();
            double[] rotation = (double[])body.Rotation.Clone();
            double[] velocity = (double[])body.Velocity.Clone();
            double[] spin = (double[])body.Spin.Clone();
            double[] q = (double[])body.Q.Clone();
            double[] qd = (double[])body.Qd.Clone();

            body.BecomeRemains();
            body.BecomeRemains();

            Assert.Equal(position, body.Position);
            Assert.Equal(rotation, body.Rotation);
            Assert.Equal(velocity, body.Velocity);
            Assert.Equal(spin, body.Spin);
            Assert.Equal(q, body.Q);
            Assert.Equal(qd, body.Qd);
        }

        [Fact]
        public void ALimbIsPosedFromTheLinksOfTheBodyItWasCutFromAtTheirPlaceAndSpeed()
        {
            SolverConfig config = Water();
            var world = new DynamicsWorld(config);

            // Four links, every joint driven by its own oscillator, swimming: every link moves.
            Genome g = TestBodies.Chain(links: 4, power: 10f, hz: 0.7f);
            Creature source = TestBodies.Build(g, config);
            source.PlaceAt(new Vec3(1, -20, 2), QuatD.Identity);
            world.Add(source);

            for (int i = 0; i < 400; i++) world.Step();

            // The limb is links 2 and 3: a body of its own on a phenotype cut out of the source's.
            int[] map = { 2, 3 };
            Phenotype limbPlan = source.Phenotype.Select(map);
            var limb = new Creature(9, limbPlan, config);
            limb.PoseFromLinksOf(source, map);

            for (int k = 0; k < map.Length; k++)
            {
                int s = map[k];
                for (int d = 0; d < 3; d++)
                {
                    Assert.Equal(source.Position[3 * s + d], limb.Position[3 * k + d], 9);
                    Assert.Equal(source.Velocity[3 * s + d], limb.Velocity[3 * k + d], 9);
                    Assert.Equal(source.Spin[3 * s + d], limb.Spin[3 * k + d], 9);
                }

                for (int d = 0; d < 4; d++) Assert.Equal(source.Rotation[4 * s + d], limb.Rotation[4 * k + d], 9);
            }

            double speed = Vec3.Read(limb.Velocity, 0).Magnitude;
            _out.WriteLine($"the limb's root moves at {speed:0.#####} m/s, as its source link did");
            Assert.True(speed > 1e-4, "the fixture never moved the limb, so the test says nothing about velocity");
        }

        [Fact]
        public void APosedBodyWithNoRootLinkIsRefused()
        {
            SolverConfig config = Water();
            Creature source = TestBodies.Build(TestBodies.Chain(links: 3, power: 10f), config);
            Creature limb = TestBodies.Build(TestBodies.Chain(links: 2, power: 10f), config, id: 1);

            Assert.Throws<ArgumentException>(() => limb.PoseFromLinksOf(source, new[] { -1, 1 }));
            Assert.Throws<ArgumentException>(() => limb.PoseFromLinksOf(source, new[] { 1 }));
        }
    }
}
