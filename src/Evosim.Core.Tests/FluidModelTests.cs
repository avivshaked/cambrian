using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// DESIGN.md §5.2 on the projected-area rule: "Three lines of code; decides whether the
    /// project works." Worth testing properly, then.
    /// </summary>
    public class FluidModelTests
    {
        private readonly ITestOutputHelper _output;

        public FluidModelTests(ITestOutputHelper output) => _output = output;

        private static readonly FluidConfig Water = FluidConfig.DragOnly;

        /// <summary>A flat plate: thin on X, broad on Y and Z.</summary>
        private static readonly Float3 Plate = new Float3(0.05f, 0.5f, 0.5f);

        private static readonly Float3 Cube = new Float3(0.25f, 0.25f, 0.25f);

        [Fact]
        public void StillWaterExertsNoForce()
        {
            FluidModel.BoxDrag(Cube, Quat.Identity, Float3.Zero, Float3.Zero, Water,
                out Float3 force, out Float3 torque);

            Fixtures.AssertClose(Float3.Zero, force);
            Fixtures.AssertClose(Float3.Zero, torque);
        }

        [Fact]
        public void DragOpposesMotionAndMatchesTheClosedForm()
        {
            // Moving along +X, only the +X face leads. Expected magnitude is the §5.2 formula
            // with A_effective the area of that face.
            var velocity = new Float3(2f, 0f, 0f);
            FluidModel.BoxDrag(Cube, Quat.Identity, velocity, Float3.Zero, Water,
                out Float3 force, out Float3 torque);

            float area = 4f * Cube.Y * Cube.Z;
            float expected = 0.5f * Water.Density * Water.DragCoefficient * area * 2f * 2f;

            Fixtures.AssertClose(new Float3(-expected, 0f, 0f), force, expected * 1e-4f);

            // Symmetric body, pure translation: nothing to turn it.
            Fixtures.AssertClose(Float3.Zero, torque, 1e-3f);
        }

        [Fact]
        public void DragIsQuadraticInSpeed()
        {
            FluidModel.BoxDrag(Cube, Quat.Identity, new Float3(1f, 0f, 0f), Float3.Zero, Water,
                out Float3 slow, out _);
            FluidModel.BoxDrag(Cube, Quat.Identity, new Float3(3f, 0f, 0f), Float3.Zero, Water,
                out Float3 fast, out _);

            Fixtures.AssertClose(9f, fast.Magnitude / slow.Magnitude, 1e-3f);
        }

        [Fact]
        public void BroadsideGeneratesFarMoreForceThanEdgeOn()
        {
            // The rule DESIGN.md §5.2 says decides whether anything ever swims. A paddle only
            // produces net thrust because the power stroke costs more than the recovery.
            var speed = new Float3(1f, 0f, 0f);

            FluidModel.BoxDrag(Plate, Quat.Identity, speed, Float3.Zero, Water,
                out Float3 broadside, out _);

            // Same plate turned 90 degrees about Z: now moving edge-on.
            Quat edgeOn = Quat.FromAxisAngle(new Float3(0f, 0f, 1f), (float)System.Math.PI / 2f);
            FluidModel.BoxDrag(Plate, edgeOn, speed, Float3.Zero, Water,
                out Float3 edge, out _);

            float ratio = broadside.Magnitude / edge.Magnitude;
            _output.WriteLine($"broadside/edge-on force ratio: {ratio:0.##}");

            // Areas are 4*0.5*0.5 = 1.0 broadside against 4*0.05*0.5 = 0.1 edge-on.
            Fixtures.AssertClose(10f, ratio, 0.05f);
        }

        [Fact]
        public void OnlyLeadingFacesContribute()
        {
            // Counting trailing faces too would double the force and cancel the asymmetry a
            // paddle depends on. Force on a cube must equal one face's worth, not two.
            FluidModel.BoxDrag(Cube, Quat.Identity, new Float3(1f, 0f, 0f), Float3.Zero, Water,
                out Float3 force, out _);

            float oneFace = 0.5f * Water.Density * Water.DragCoefficient * (4f * Cube.Y * Cube.Z);
            Fixtures.AssertClose(oneFace, force.Magnitude, oneFace * 1e-3f);
        }

        [Fact]
        public void RotationProducesOpposingTorqueButNoNetForce()
        {
            // Angular drag is not a separate formula here — it falls out of faces moving
            // while the centre does not.
            var spin = new Float3(0f, 0f, 4f);
            FluidModel.BoxDrag(Plate, Quat.Identity, Float3.Zero, spin, Water,
                out Float3 force, out Float3 torque);

            Assert.True(torque.Z < 0f, $"torque should oppose the spin, got {torque}");
            Assert.True(force.Magnitude < torque.Magnitude * 1e-3f,
                $"a symmetric body spinning in place should feel no net force, got {force}");
        }

        [Fact]
        public void SpinningAboutAPrincipalAxisStillFeelsDrag()
        {
            // Sampling one point per face reports exactly zero here, because a face centre
            // moves perpendicular to its own normal. A limb flapping about its joint is this
            // motion, so getting zero would mean paddling produced almost no force.
            FluidModel.BoxDrag(Plate, Quat.Identity, Float3.Zero, new Float3(0f, 0f, 3f), Water,
                out _, out Float3 torque);

            _output.WriteLine($"torque on a plate spinning about its own Z: {torque}");
            Assert.True(torque.Magnitude > 1f, $"expected real resistance, got {torque}");

            // One panel per face is the degenerate case, kept as evidence rather than folklore.
            var singleSample = new FluidConfig { PanelsPerAxis = 1 };
            FluidModel.BoxDrag(Plate, Quat.Identity, Float3.Zero, new Float3(0f, 0f, 3f), singleSample,
                out _, out Float3 blind);

            Fixtures.AssertClose(Float3.Zero, blind, 1e-4f);
        }

        [Fact]
        public void FasterRotationMeansMoreTorque()
        {
            FluidModel.BoxDrag(Plate, Quat.Identity, Float3.Zero, new Float3(0f, 0f, 1f), Water,
                out _, out Float3 slow);
            FluidModel.BoxDrag(Plate, Quat.Identity, Float3.Zero, new Float3(0f, 0f, 3f), Water,
                out _, out Float3 fast);

            Assert.True(fast.Magnitude > slow.Magnitude * 5f,
                $"expected strongly superlinear growth, got {slow.Magnitude} -> {fast.Magnitude}");
        }

        [Fact]
        public void TheModelIsFrameIndependent()
        {
            // Rotating the part and its velocity together must rotate the result and nothing
            // else. Water has no preferred direction.
            var rng = new Rng(17);

            for (int i = 0; i < 100; i++)
            {
                Float3 v = rng.NextFloat3(-3f, 3f);
                Float3 w = rng.NextFloat3(-3f, 3f);
                Quat frame = rng.NextRotation();

                FluidModel.BoxDrag(Plate, Quat.Identity, v, w, Water,
                    out Float3 force, out Float3 torque);
                FluidModel.BoxDrag(Plate, frame, frame.Rotate(v), frame.Rotate(w), Water,
                    out Float3 rotatedForce, out Float3 rotatedTorque);

                Fixtures.AssertClose(frame.Rotate(force), rotatedForce, 1e-2f);
                Fixtures.AssertClose(frame.Rotate(torque), rotatedTorque, 1e-2f);
            }
        }

        [Fact]
        public void DragAlwaysRemovesEnergy()
        {
            // Power delivered by drag must never be positive: F.v + T.w <= 0. If it can be
            // positive the model is a free energy source, and a search WILL find that —
            // [U07 §2, p.3] documents exactly this happening in published work.
            var rng = new Rng(99);

            for (int i = 0; i < 2000; i++)
            {
                Float3 v = rng.NextFloat3(-5f, 5f);
                Float3 w = rng.NextFloat3(-5f, 5f);
                Float3 half = new Float3(rng.Range(0.05f, 0.6f), rng.Range(0.05f, 0.6f), rng.Range(0.05f, 0.6f));

                FluidModel.BoxDrag(half, rng.NextRotation(), v, w, Water,
                    out Float3 force, out Float3 torque);

                float power = Float3.Dot(force, v) + Float3.Dot(torque, w);
                Assert.True(power <= 1e-3f, $"drag delivered {power} W of power into the body");
            }
        }

        [Fact]
        public void AddedMassInflatesMassByTheDisplacedWater()
        {
            // A neutrally buoyant part displaces its own mass of water, so Ca = 1 doubles it.
            var config = new FluidConfig { AddedMassCoefficient = 1f };
            float volume = 0.5f;
            float mass = config.Density * volume;

            Fixtures.AssertClose(2f * mass, FluidModel.EffectiveMass(mass, volume, config), 1e-2f);
            Fixtures.AssertClose(mass, FluidModel.EffectiveMass(mass, volume, FluidConfig.DragOnly), 1e-2f);
        }
    }
}
