using System;
using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// How far a joint driven flat out stands past its own stop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the one number the PhysX parity swims could read directly.</b> PhysX solves an
    /// <c>ArticulationDofLock.LimitedMotion</c> as an inequality and holds a driven joint exactly
    /// at the limit the genome gave it — round 42's genome 1 sat at 0.514528 rad against a stored
    /// limit of 0.514528. A penalty spring cannot do that; it can only be stiff enough that the
    /// difference stops mattering, and the difference it allows against a constant drive
    /// <c>m</c> is <c>m / k</c>.
    /// </para>
    /// <para>
    /// The tolerance is the spike's stated target: two hundredths of a radian, a little over one
    /// degree, at dt 0.01. The first build allowed 0.0907 rad on that genome.
    /// </para>
    /// </remarks>
    public sealed class JointLimitOvershootTests
    {
        private const double Tolerance = 0.02;

        [Theory]
        [InlineData(JointType.Hinge, 0.4f, 10f)]
        [InlineData(JointType.Hinge, 0.4f, 60f)]
        [InlineData(JointType.Twist, 0.6f, 25f)]
        [InlineData(JointType.Universal, 0.5f, 30f)]
        [InlineData(JointType.Spherical, 0.5f, 30f)]
        public void ADriveHeldAgainstAStopDoesNotPassItByMuch(
            JointType joint, float limit, float power)
        {
            SolverConfig config = TestBodies.Water(0.01);

            Creature body = TestBodies.Build(
                HeldHardOver(joint, limit, power), config);

            body.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);

            var world = new DynamicsWorld(config);
            world.Add(body);

            for (int step = 0; step < 4000; step++) world.Step();

            Assert.True(body.Alive, "the body was lost before the limit could be read");

            bool anyDriven = false;

            for (int d = 0; d < body.Dof; d++)
            {
                // Only a degree of freedom the brain is actually leaning on says anything.
                double magnitude = Math.Abs(body.Drive.Magnitude(d));
                if (magnitude < 1e-6) continue;

                anyDriven = true;

                double q = body.Q[d];
                double over = q > body.LimitHi[d] ? q - body.LimitHi[d]
                    : q < body.LimitLo[d] ? body.LimitLo[d] - q
                    : 0;

                Assert.True(
                    over < Tolerance,
                    $"dof {d} of a {joint} stands {over:0.#####} rad past its stop " +
                    $"({body.LimitLo[d]:0.###}..{body.LimitHi[d]:0.###}) under {magnitude:0.###} N.m; " +
                    $"the spring is worth {body.LimitStiffness[d]:0.#} N.m/rad, " +
                    $"which predicts {magnitude / body.LimitStiffness[d]:0.#####}");
            }

            Assert.True(anyDriven, "nothing in this body was driven, so nothing was tested");
        }

        /// <summary>
        /// Two parts, one joint, and a brain that emits a hard +1 on every degree of freedom for
        /// ever — so every one of them is pinned against its upper stop by the end.
        /// </summary>
        private static Genome HeldHardOver(JointType joint, float limit, float power)
        {
            int dof = joint.DofCount();

            var limits = new Float2[dof];
            for (int d = 0; d < dof; d++) limits[d] = new Float2(-limit, limit);

            var neurons = new NeuronDef[dof];
            for (int d = 0; d < dof; d++)
            {
                // A sum of nothing plus a bias of one: a constant, which an oscillator is not.
                neurons[d] = new NeuronDef
                {
                    Op = NeuronOp.Sum,
                    Amplitude = 1f,
                    Bias = 1f,
                    Frequency = 0f,
                    Phase = 0f,
                    Inputs = Array.Empty<NeuronInput>(),
                };
            }

            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits
            {
                BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f,
            };

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
            });

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.2f, 0.05f, 0.2f),
                CellTypeId = CellTypeIds.Link,
                ShapeId = ShapeIds.Box,
                JointType = joint,
                JointLimits = limits,
                Power = power,
                RecursiveLimit = 1,
                Neurons = neurons,
            });

            genome.Nodes[0].Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            return genome;
        }
    }
}
