using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// propose-foraging.md's bench item 7: a hand-built forager. If a brain wired by hand cannot
    /// steer to a corpse on the scent and the push, an evolved one cannot either.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Braitenberg's vehicle 3a, "love".</b> A trunk with two tails at its rear corners and a
    /// nose of its own. Each tail's stroke is its oscillator times
    /// <c>a - k (s_tail - s_trunk) - m s_trunk</c>, with <c>s</c> the squashed scent. The tail on
    /// the corpse's side smells more than the trunk and strokes less, so the far tail's push turns
    /// the body toward the smell; <c>m</c> slows both as the smell rises. A tail's own scent alone
    /// (the first cut) gave a difference of about 2% across 16 cm and never turned, so the
    /// comparison is with the trunk, through the parent, the one other part a sibling can read.
    /// </para>
    /// <para>
    /// <b>A bench, not a proof.</b> One body, one corpse, still water, no weight. The wired brain's
    /// closest approach is read against the same body with the scent's weight at zero, which
    /// strokes at the same starting amplitude and never turns toward anything.
    /// </para>
    /// </remarks>
    public sealed class HandForagerBench
    {
        private readonly ITestOutputHelper _out;

        public HandForagerBench(ITestOutputHelper output) => _out = output;

        private const double Centre = 40;

        /// <param name="gain">k: how strongly a tail's excess of scent over the trunk's quiets it.</param>
        /// <param name="stroke">a: the stroke's amplitude where the two smell alike.</param>
        /// <param name="halfWidth">the trunk's half-width, so the tails' separation is 1.6 times it.</param>
        private static Genome Forager(float gain, float stroke, float scentWeight, float halfWidth = 0.2f, float slow = 0f, float[] sides = null)
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f };

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.15f, 0.1f, halfWidth),
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,

                // The trunk's nose: a neuron that passes on the scent at the trunk, for the tails.
                Neurons = new[]
                {
                    new NeuronDef
                    {
                        Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f,
                        Inputs = new[] { NeuronInput.FromSensor(SensorChannel.Scent, 0) },
                    },
                },
            });

            // Neuron 0 drives the hinge: the product of the oscillator (1) and the quieting (2).
            var neurons = new[]
            {
                new NeuronDef
                {
                    Op = NeuronOp.Product, Frequency = 1f, Amplitude = 1f,
                    Inputs = new[]
                    {
                        NeuronInput.FromNeuron(NeuronInputKind.SameNode, 1),
                        NeuronInput.FromNeuron(NeuronInputKind.SameNode, 2),
                    },
                },
                new NeuronDef
                {
                    Op = NeuronOp.OscillateWave, Frequency = 1f, Phase = 0f, Amplitude = 1f,
                    Inputs = Array.Empty<NeuronInput>(),
                },
                new NeuronDef
                {
                    Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f,
                    Inputs = new[]
                    {
                        NeuronInput.FromConstant(stroke),
                        NeuronInput.FromSensor(SensorChannel.Scent, 0, -gain * scentWeight),
                        NeuronInput.FromNeuron(NeuronInputKind.ParentNode, 0, (gain - slow) * scentWeight),
                    },
                },
            };

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.15f, 0.05f, 0.015f),
                CellTypeId = CellTypeIds.Link,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Hinge,
                JointLimits = new[] { new Float2(-0.6f, 0.6f) },
                Power = 10f,
                RecursiveLimit = 1,
                Neurons = neurons,
            });

            foreach (float side in sides ?? new[] { 0.8f, -0.8f })
            {
                genome.Nodes[0].Edges.Add(new MorphEdge
                {
                    Child = 1,
                    ParentAnchor = new Float3(1f, 0f, side),
                    ChildAnchor = new Float3(-1f, 0f, 0f),
                    Orientation = Quat.Identity,
                    Scale = Float3.One,
                });
            }

            return genome;
        }

        private sealed class Trip
        {
            public double Closest, ClosestAt, EndDistance, Travelled, WorkWatts, Mass;
        }

        private Trip Swim(Genome genome, double corpseAngle, double corpseDistance, double push, double seconds, bool log)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = push;
            config.ScentHalfScaleJoules = 20.0;

            Creature body = TestBodies.Build(genome, config);
            var start = new Vec3(Centre, -Centre, Centre);
            body.PlaceAt(start, QuatD.Identity);

            // The tails trail at +x, so the body's head points along -x.
            var corpse = new Vec3(
                Centre - corpseDistance * Math.Cos(corpseAngle), -Centre,
                Centre + corpseDistance * Math.Sin(corpseAngle));
            var field = new ScentField(80f, 80f, 80f, 10f);
            field.Add(new Float3((float)corpse.X, (float)corpse.Y, (float)corpse.Z), 100.0);
            body.Senses.Scent = field;

            var world = new DynamicsWorld(config);
            world.Add(body);

            var trip = new Trip { Closest = double.MaxValue, Mass = body.TotalMass };
            double work = 0;
            Vec3 last = start;
            int steps = (int)Math.Round(seconds / config.StepSeconds);
            for (int step = 1; step <= steps; step++)
            {
                world.Step();
                for (int q = 0; q < body.Dof; q++) work += Math.Abs(body.Tau[q] * body.Qd[q]) * config.StepSeconds;
                var at = new Vec3(body.Position[0], body.Position[1], body.Position[2]);
                double d = (at - corpse).Magnitude;
                if (d < trip.Closest) { trip.Closest = d; trip.ClosestAt = step * config.StepSeconds; }
                if (step % 100 == 0) { trip.Travelled += (at - last).Magnitude; last = at; }
                if (log && step % 9000 == 0)
                {
                    _out.WriteLine($"    t {step * config.StepSeconds:0} s: at ({at.X - Centre:0.00}, {at.Y + Centre:0.00}, {at.Z - Centre:0.00}), {d:0.00} m from the corpse");
                }
            }

            trip.WorkWatts = work / seconds;
            trip.EndDistance = (new Vec3(body.Position[0], body.Position[1], body.Position[2]) - corpse).Magnitude;
            return trip;
        }

        /// <summary>The root's heading turned through, radians, and the distance its centre moved, over a blind swim.</summary>
        private static (double turn, double moved) Heading(Genome genome, double seconds)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = 0.3;
            config.ScentHalfScaleJoules = 20.0;
            Creature body = TestBodies.Build(genome, config);
            var start = new Vec3(Centre, -Centre, Centre);
            body.PlaceAt(start, QuatD.Identity);
            body.Senses.Scent = new ScentField(80f, 80f, 80f, 10f);
            var world = new DynamicsWorld(config);
            world.Add(body);

            double Yaw()
            {
                Vec3 head = Mat3.Read(body.RotationMatrix, 0) * new Vec3(-1, 0, 0);
                return Math.Atan2(head.Z, head.X);
            }

            double last = Yaw(), turn = 0;
            int steps = (int)Math.Round(seconds / config.StepSeconds);
            for (int step = 0; step < steps; step++)
            {
                world.Step();
                double y = Yaw(), d = y - last;
                if (d > Math.PI) d -= 2 * Math.PI;
                if (d < -Math.PI) d += 2 * Math.PI;
                turn += d;
                last = y;
            }
            var at = new Vec3(body.Position[0], body.Position[1], body.Position[2]);
            return (turn, (at - start).Magnitude);
        }

        [Fact]
        public void OneSideFinTurnsItsBodyAndAMirroredPairDrivesItStraight()
        {
            // Bench item 4: the forager's trunk with both tails, and with the left one alone, each
            // stroking blind (the scent weights 0) for 300 s in still water.
            (double pairTurn, double pairMoved) = Heading(Forager(50f, 0.8f, 0f), 300);
            (double oneTurn, double oneMoved) = Heading(Forager(50f, 0.8f, 0f, sides: new[] { 0.8f }), 300);
            _out.WriteLine($"a mirrored pair: turned {pairTurn * 180 / Math.PI:0.#} degrees, moved {pairMoved:0.##} m");
            _out.WriteLine($"one side fin:    turned {oneTurn * 180 / Math.PI:0.#} degrees, moved {oneMoved:0.##} m");
            Assert.True(Math.Abs(pairTurn) < 20 * Math.PI / 180, $"the pair turned {pairTurn * 180 / Math.PI:0} degrees");
            Assert.True(pairMoved > 5, $"the pair moved only {pairMoved:0.##} m");
            Assert.True(Math.Abs(oneTurn) > 90 * Math.PI / 180, $"one fin turned only {oneTurn * 180 / Math.PI:0} degrees");
        }

        [Fact]
        public void AWiredForagerTurnsToACorpseAndStaysByIt()
        {
            const float stroke = 0.8f;
            double seconds = 900;
            float gain = float.Parse(Environment.GetEnvironmentVariable("FORAGER_GAIN") ?? "50", System.Globalization.CultureInfo.InvariantCulture);
            float halfWidth = float.Parse(Environment.GetEnvironmentVariable("FORAGER_HALF_WIDTH") ?? "0.2", System.Globalization.CultureInfo.InvariantCulture);
            double push = double.Parse(Environment.GetEnvironmentVariable("FORAGER_PUSH") ?? "0.3", System.Globalization.CultureInfo.InvariantCulture);
            float slow = float.Parse(Environment.GetEnvironmentVariable("FORAGER_SLOW") ?? "0.6", System.Globalization.CultureInfo.InvariantCulture);
            _out.WriteLine($"gain {gain}, stroke {stroke}, trunk half-width {halfWidth} m, push {push}, slowing {slow}");

            foreach (double angleDeg in new[] { 0.0, 45.0, 90.0, 180.0 })
            {
                double angle = angleDeg * Math.PI / 180.0;
                _out.WriteLine($"corpse 8 m away, {angleDeg} degrees off the heading");
                Trip wired = Swim(Forager(gain, stroke, 1f, halfWidth, slow), angle, 8, push, seconds, log: true);
                Trip blind = Swim(Forager(gain, stroke, 0f, halfWidth, slow), angle, 8, push, seconds, log: false);
                _out.WriteLine($"  wired: closest {wired.Closest:0.00} m at {wired.ClosestAt:0} s, end {wired.EndDistance:0.00} m, path {wired.Travelled:0.0} m, joint work {wired.WorkWatts:0.####} W, mass {wired.Mass:0.###} kg");
                _out.WriteLine($"  blind: closest {blind.Closest:0.00} m at {blind.ClosestAt:0} s, end {blind.EndDistance:0.00} m, path {blind.Travelled:0.0} m");
            }

            // What the bench of 2026-09-29 showed, held: from 45 and 90 degrees off its heading the
            // wired forager turns, closes to under 2 m and is still within 3 m at the end, where the
            // blind one never comes within 5 m and ends 45 m away. A corpse dead behind it is not
            // found: two noses side by side cannot tell behind from ahead.
            foreach (double angleDeg in new[] { 45.0, 90.0 })
            {
                double angle = angleDeg * Math.PI / 180.0;
                Trip wired = Swim(Forager(gain, stroke, 1f, halfWidth, slow), angle, 8, push, seconds, log: false);
                Trip blind = Swim(Forager(gain, stroke, 0f, halfWidth, slow), angle, 8, push, seconds, log: false);
                Assert.True(wired.Closest < 2.0, $"at {angleDeg} degrees the wired forager came only within {wired.Closest:0.00} m");
                Assert.True(wired.EndDistance < 3.0, $"at {angleDeg} degrees the wired forager ended {wired.EndDistance:0.00} m away");
                Assert.True(blind.Closest > 5.0, $"at {angleDeg} degrees the blind forager came within {blind.Closest:0.00} m");
            }
        }
    }
}