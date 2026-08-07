using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The optimised drag path against a transcription of the one it replaced — DESIGN.md §5A.9.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two expressions of one quantity, deliberately, and only here.</b> The project's rule is
    /// the opposite — <see cref="PartShape.SurfaceArea"/> is derived from <c>AddPanels</c> rather
    /// than from a second analytic formula, precisely because two implementations are how they
    /// come to disagree silently (logbook/0009). The exception is a rewrite done for speed: the
    /// claim being made is that the new code computes <i>the same numbers</i>, and the only way to
    /// state that claim is to keep the old code somewhere and compare. It lives in the test
    /// project, is frozen, and nothing ships against it.
    /// </para>
    /// <para>
    /// <b>Tolerance rather than bitwise equality, and the reason is not sloppiness.</b> Summing in
    /// the local frame and rotating the result is exactly equal in ℝ³ and not in <c>float</c>:
    /// rotation does not distribute over addition once each step rounds. The residual is
    /// rounding, so it is asserted relative to the magnitude of the force rather than absolutely —
    /// an absolute bound would pass trivially on a still creature and fail on a fast one for no
    /// reason connected to correctness.
    /// </para>
    /// </remarks>
    public class DragEquivalenceTests
    {
        private readonly ITestOutputHelper _output;

        public DragEquivalenceTests(ITestOutputHelper output) => _output = output;

        /// <summary>
        /// The world-space per-panel sum, as it stood before the local-frame rewrite. Frozen.
        /// </summary>
        private static void ReferenceDrag(
            PartShape shape,
            Float3 halfExtents,
            Quat rotation,
            Float3 velocity,
            Float3 angularVelocity,
            FluidConfig config,
            List<DragPanel> panels,
            out Float3 force,
            out Float3 torque)
        {
            force = Float3.Zero;
            torque = Float3.Zero;

            float k = 0.5f * config.Density * config.DragCoefficient;
            if (k <= 0f) return;

            panels.Clear();
            shape.AddPanels(halfExtents, config.PanelsPerAxis < 1 ? 1 : config.PanelsPerAxis, panels);

            for (int i = 0; i < panels.Count; i++)
            {
                DragPanel panel = panels[i];
                if (panel.Area <= 0f) continue;

                Float3 normal = rotation.Rotate(panel.Normal);
                Float3 offset = rotation.Rotate(panel.Centre);

                Float3 panelVelocity = velocity + Float3.Cross(angularVelocity, offset);

                float normalSpeed = Float3.Dot(panelVelocity, normal);
                if (normalSpeed <= 0f) continue;

                Float3 panelForce = normal * (-k * panel.Area * normalSpeed * normalSpeed);

                force += panelForce;
                torque += Float3.Cross(offset, panelForce);
            }
        }

        public static IEnumerable<object[]> Shapes => new[]
        {
            new object[] { "box" },
            new object[] { "capsule" },
            new object[] { "sphere" },
        };

        [Theory]
        [MemberData(nameof(Shapes))]
        public void TheFastPathComputesTheSameForce(string shapeId)
        {
            PartShape shape = PartShapeRegistry.Standard.Resolve(shapeId);
            var config = FluidConfig.DragOnly;
            var scratch = new List<DragPanel>(64);
            var rng = new Rng(20260807);

            double worstForce = 0d, worstTorque = 0d;
            float worstNet = 0f;
            int cases = 0, moving = 0;

            for (int i = 0; i < 400; i++)
            {
                var half = new Float3(
                    rng.Range(0.05f, 0.5f), rng.Range(0.05f, 0.5f), rng.Range(0.05f, 0.5f));

                Quat rotation = rng.NextRotation();

                // Spans a still part through to one moving faster than Spike 01's measured
                // maximum of 58.6 m/s, because the rounding this test bounds grows with speed.
                float scale = i < 40 ? 0f : rng.Range(0.01f, 60f);
                Float3 velocity = RandomDirection(rng) * scale;
                Float3 spin = RandomDirection(rng) * rng.Range(0f, 40f);

                ReferenceDrag(
                    shape, half, rotation, velocity, spin, config, scratch,
                    out Float3 expectedForce, out Float3 expectedTorque);

                DragPanelSet panels = DragPanelSet.For(shape, half, config.PanelsPerAxis, scratch);

                FluidModel.Drag(
                    panels, rotation, velocity, spin, config,
                    out Float3 actualForce, out Float3 actualTorque);

                // Scaled against the size of the terms being summed, not against the net. The
                // net force of a rotating box is a near-total cancellation of large opposing panel
                // forces, so dividing by it reports 3000% for a difference in the last bit of
                // numbers that were never in disagreement — measured, on the first run of this
                // test. The honest claim is that the two agree to rounding *of the contributions*.
                float k = 0.5f * config.Density * config.DragCoefficient;
                float reach = Reach(panels);
                float speed = Length(velocity) + Length(spin) * reach;
                float forceScale = Math.Max(k * TotalArea(panels) * speed * speed, 1e-9f);

                worstForce = Math.Max(worstForce, Length(expectedForce - actualForce) / forceScale);
                worstTorque = Math.Max(
                    worstTorque, Length(expectedTorque - actualTorque) / (forceScale * Math.Max(reach, 1e-4f)));

                worstNet = Math.Max(worstNet, Length(expectedForce));

                cases++;
                if (Length(expectedForce) > 0f) moving++;
            }

            _output.WriteLine(
                $"{shapeId}: {cases} cases ({moving} with force, largest {worstNet:0.} N), " +
                $"worst error vs panel-force scale — force {worstForce:E2}, torque {worstTorque:E2}");

            // Measured at 6–9e-8 across all three shapes, against a float epsilon of 1.19e-7.
            // So the two agree to the last bit of a float, over 400 orientations per shape and
            // panel forces up to 1.9 MN — which is what "the same calculation in a different
            // basis" looks like when it is true. 1e-6 leaves an order of magnitude of headroom
            // and is still two orders below anything that could mean different physics.
            Assert.True(moving > 300, $"only {moving} cases produced force — the sweep is not exercising anything");
            Assert.True(worstForce < 1e-6, $"force differs by {worstForce:E3}");
            Assert.True(worstTorque < 1e-6, $"torque differs by {worstTorque:E3}");
        }

        [Fact]
        public void PanelsAreBuiltOnceAndDescribeTheSameSurface()
        {
            // The set drops zero-area panels, which the per-step loop used to test for on every
            // panel on every part forever. Dropping them must not change the surface it describes.
            foreach (object[] row in Shapes)
            {
                var shapeId = (string)row[0];
                PartShape shape = PartShapeRegistry.Standard.Resolve(shapeId);
                var half = new Float3(0.3f, 0.2f, 0.45f);

                DragPanelSet set = DragPanelSet.For(shape, half, 4);

                float total = 0f;
                for (int i = 0; i < set.Count; i++) total += set.Areas[i];

                float expected = shape.SurfaceArea(half);

                _output.WriteLine($"{shapeId}: {set.Count} panels, area {total:0.####} vs {expected:0.####}");
                Assert.True(Math.Abs(total - expected) < 1e-4f * expected,
                    $"{shapeId} panel area {total} != surface area {expected}");
            }
        }

        [Fact]
        public void DragStillCannotDeliverEnergyIntoABody()
        {
            // §11.2's property, restated against the new path. It is the one thing about this
            // model that a rewrite must not be allowed to break quietly: a fluid that can push
            // energy into a body is a free-energy source, and [U07 §2, p.3] documents a search
            // finding exactly that kind of flaw and building its gait on it.
            PartShape shape = PartShapeRegistry.Standard.Resolve("box");
            var config = FluidConfig.DragOnly;
            var rng = new Rng(7);

            for (int i = 0; i < 500; i++)
            {
                var half = new Float3(
                    rng.Range(0.05f, 0.5f), rng.Range(0.05f, 0.5f), rng.Range(0.05f, 0.5f));

                Quat rotation = rng.NextRotation();
                Float3 velocity = RandomDirection(rng) * rng.Range(0f, 40f);
                Float3 spin = RandomDirection(rng) * rng.Range(0f, 30f);

                DragPanelSet panels = DragPanelSet.For(shape, half, config.PanelsPerAxis);

                FluidModel.Drag(
                    panels, rotation, velocity, spin, config,
                    out Float3 force, out Float3 torque);

                float power = Float3.Dot(force, velocity) + Float3.Dot(torque, spin);

                Assert.True(power <= 1e-3f, $"drag delivered {power} W into the body");
            }
        }

        private static float TotalArea(DragPanelSet panels)
        {
            float total = 0f;
            for (int i = 0; i < panels.Count; i++) total += panels.Areas[i];
            return total;
        }

        /// <summary>Largest panel offset from the part centre — the lever arm spin acts through.</summary>
        private static float Reach(DragPanelSet panels)
        {
            float most = 0f;
            for (int i = 0; i < panels.Count; i++) most = Math.Max(most, Length(panels.Centres[i]));
            return most;
        }

        private static float Length(Float3 v) => (float)Math.Sqrt(Float3.Dot(v, v));

        private static Float3 RandomDirection(Rng rng)
        {
            var v = new Float3(
                rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f));

            float length = Length(v);
            return length > 1e-6f ? v * (1f / length) : new Float3(1f, 0f, 0f);
        }
    }
}
