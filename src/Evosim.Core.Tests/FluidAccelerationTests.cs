using System;
using System.Diagnostics;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The water's own acceleration and the force it puts on a body —
    /// <c>logbook/specs/water-carries-spec.md</c>, <c>fable-propose-water-carries.md</c>
    /// (2026-09-12): <c>Du/Dt = ∂u/∂t + (u·∇)u</c> from the field itself, and
    /// <c>F = c·(ρV + m_added)·Du/Dt</c> on a part.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Four things are worth holding here, and only one of them is the arithmetic.</b> That the
    /// difference stencil is the derivative it claims to be — checked against a difference four
    /// times finer on both fields a world can have, and against two fields whose answer is known
    /// in closed form. That a still or uniform steady field gives <i>exactly</i> zero, since a
    /// term that leaks a force into water that is not moving would be a free-energy source of the
    /// kind DESIGN §5.3 warns is found and exploited. That the term does what it was built for: a
    /// drag-coupled body in a circular current follows the circle with it on and spirals out of
    /// the tank without it. And that at the default coefficient it is zero to the bit, which is
    /// what lets every recorded world replay.
    /// </para>
    /// <para>
    /// <b>The vertical component at the waterline is the fifth, and it is a boundary rather than
    /// an arithmetic.</b> D050 stops upward net force at <c>y</c> = 0 and CLAUDE.md's rule is that
    /// anything new that can push a body up is asked the same question. The answer here is that
    /// the field's vertical velocity is exactly zero on that plane and above it, so the vertical
    /// component of <c>Du/Dt</c> is exactly zero there too — asserted, so that
    /// <c>FluidEnvironment</c>'s clamp is known to be belt and braces rather than load-bearing.
    /// </para>
    /// </remarks>
    public class FluidAccelerationTests
    {
        private readonly ITestOutputHelper _output;

        public FluidAccelerationTests(ITestOutputHelper output) => _output = output;

        // The campaign's two containers, at the geometry the runs use: the box is 100 m2 over four
        // patches of 60 m depth, which is 20 x 5 x 60 m; the tank is the same footprint as a disc,
        // radius 5.642 m, four rings.
        private const float Area = 100f;
        private const int Patches = 4;
        private const float Depth = 60f;
        private const float Speed = 0.3f;
        private const float Period = 6000f;
        private const ulong Seed = 20260912UL;

        private static float TankRadius => TankGeometry.RadiusFor(Area);

        private static CurrentField Box()
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = Speed,
                PeriodSeconds = Period,
            };

            field.SetBox((float)Math.Sqrt(Area / Patches), Patches, Depth, Seed);
            return field;
        }

        private static CurrentField Tank()
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = Speed,
                PeriodSeconds = Period,
            };

            field.SetBox(
                (float)Math.Sqrt(Area / Patches), Patches, Depth, Seed,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: TankRadius);

            return field;
        }

        /// <summary>
        /// The stencil is the derivative: a quarter of its step gives the same answer to well
        /// inside a per cent, on both the box's transport field and the tank's streams.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Held against a finer difference rather than against algebra, and the spec asks for
        /// that on purpose.</b> The two fields have closed-form derivatives in principle — they
        /// are sums of sines — but writing them out is writing the construction twice, and the
        /// second copy is the one that would be wrong. Halving the step twice changes the
        /// truncation error sixteenfold, so agreement between the two is evidence about the
        /// stencil that a transcription could not give.
        /// </para>
        /// <para>
        /// <b>Compared against the field's own RMS acceleration, not pointwise.</b> <c>Du/Dt</c>
        /// passes through zero inside the box — every component of it does, many times — and a
        /// relative error at a point where the true value is 1e-6 of the RMS says nothing about
        /// anything. The tolerance is therefore 1% of the RMS magnitude, taken over the same
        /// points, and the worst pointwise relative error is printed beside it so that a reader
        /// can see both.
        /// </para>
        /// <para>
        /// <b>The points are set a metre in from every face.</b> A stencil that straddles the
        /// surface, the bed or the glass reads the clamp — deliberately, since that is the water a
        /// body there feels — but a clamped difference is not the same number at two step sizes,
        /// so the comparison would be measuring the boundary rather than the derivative.
        /// </para>
        /// </remarks>
        [Fact]
        public void TheStencilAgreesWithAFinerDifference()
        {
            Check("the box's transport field", Box(), (rng, inset) => InBox(rng, inset));
            Check("the tank's streams", Tank(), (rng, inset) => InTank(rng, inset));

            void Check(
                string what, CurrentField field,
                Func<Rng, float, (float X, float Y, float Z)> place)
            {
                var rng = new Rng(7UL);

                double worst = 0d, worstRelative = 0d, sumSquare = 0d;
                const int Points = 400;

                for (int i = 0; i < Points; i++)
                {
                    (float x, float y, float z) = place(rng, 1f);
                    double t = 137d + 4000d * rng.NextFloat();

                    Float3 coarse = field.AccelerationAt(x, y, z, t);
                    Float3 fine = Finer(field, x, y, z, t);

                    double error = (coarse - fine).Magnitude;
                    double magnitude = fine.Magnitude;

                    sumSquare += magnitude * magnitude;
                    if (error > worst) worst = error;
                    if (magnitude > 1e-4d && error / magnitude > worstRelative)
                    {
                        worstRelative = error / magnitude;
                    }
                }

                double rms = Math.Sqrt(sumSquare / Points);

                _output.WriteLine(
                    $"{what}: |Du/Dt| RMS {rms:0.000000} m/s² over {Points} interior points, " +
                    $"worst disagreement with a stencil 4x finer {worst:0.00000000} m/s² " +
                    $"({worst / rms:0.0000%} of the RMS), worst pointwise relative error " +
                    $"{worstRelative:0.000%} where |Du/Dt| > 1e-4");

                Assert.True(
                    worst < 0.01d * rms,
                    FormattableString.Invariant(
                        $"{what}: the stencil and a finer one disagree by {worst} m/s², which is more than 1% of the field's own {rms} m/s² RMS."));
            }
        }

        /// <summary>
        /// <c>Du/Dt</c> of a quarter-step stencil — the same arithmetic as
        /// <see cref="CurrentField.MaterialDerivative"/> with its steps divided by four, written
        /// here because the steps are constants of the field rather than arguments.
        /// </summary>
        private static Float3 Finer(CurrentField field, float x, float y, float z, double t)
        {
            const float h = 0.0125f;
            const double dt = 0.0025d;

            Float3 u = field.VelocityAt(x, y, z, t);

            Float3 total =
                (field.VelocityAt(x + h, y, z, t) - field.VelocityAt(x - h, y, z, t)) *
                    (u.X / (2f * h)) +
                (field.VelocityAt(x, y + h, z, t) - field.VelocityAt(x, y - h, z, t)) *
                    (u.Y / (2f * h)) +
                (field.VelocityAt(x, y, z + h, t) - field.VelocityAt(x, y, z - h, t)) *
                    (u.Z / (2f * h));

            return total +
                (field.VelocityAt(x, y, z, t + dt) - field.VelocityAt(x, y, z, t - dt)) *
                    (float)(1d / (2d * dt));
        }

        /// <summary>
        /// A uniform steady field accelerates nothing, exactly — and neither does still water.
        /// </summary>
        /// <remarks>
        /// <b>Exactly, to the bit, and that is the point rather than pedantry.</b> A stencil that
        /// returned 1e-12 m/s² in water that is not moving would put a force on every part of
        /// every body in the world, always in the same direction, for the whole of a run — the
        /// shape of fault that carried a population six metres into the air on a vertical velocity
        /// of 1.2e-16 (logbook/0022). A central difference of a constant is a difference of two
        /// identical floats, so zero is available here and is asserted rather than approximated.
        /// The unsteady uniform case is the control that says the clock's half of the stencil is
        /// wired at all: a field that is uniform in space but ramping in time has
        /// <c>Du/Dt = du/dt</c> and nothing else.
        /// </remarks>
        [Fact]
        public void UniformSteadyWaterHasNoAcceleration()
        {
            var uniform = new Float3(0.4f, -0.2f, 0.1f);

            Assert.Equal(
                Float3.Zero,
                CurrentField.MaterialDerivative((x, y, z, t) => uniform, 3f, -11f, 2f, 500d));

            // Still water, through the field's own gate: Speed 0 is the world every earlier run
            // measured and it must cost nothing to ask.
            var still = new CurrentField { Mode = CurrentMode.Transport, Speed = 0f };
            still.SetBox(5f, Patches, Depth, Seed);

            Assert.Equal(Float3.Zero, still.AccelerationAt(3f, -11f, 2f, 500d));

            // Uniform in space, ramping in time: Du/Dt is du/dt, which here is 0.05 m/s² along x.
            Float3 ramping = CurrentField.MaterialDerivative(
                (x, y, z, t) => new Float3((float)(0.05d * t), 0f, 0f), 3f, -11f, 2f, 500d);

            _output.WriteLine(
                $"a uniform field ramping at 0.05 m/s²: Du/Dt = {ramping}");

            Assert.Equal(0.05d, ramping.X, 4);
            Assert.Equal(0d, ramping.Y, 10);
            Assert.Equal(0d, ramping.Z, 10);
        }

        /// <summary>
        /// Solid-body rotation: the stencil returns the centripetal acceleration
        /// <c>−Ω²r</c> that holds the water on its circle.
        /// </summary>
        /// <remarks>
        /// <b>The one case with an exact answer and an exact stencil.</b> A rotating field is
        /// linear in position, so a central difference of it is not an approximation at all — the
        /// truncation term carries a second derivative that is identically zero — and the whole of
        /// <c>Du/Dt</c> is the advective part, <c>(u·∇)u = −Ω²r</c> pointing at the axis. So this
        /// tests the assembly of the stencil (which velocity multiplies which difference, and the
        /// signs) against algebra rather than against another difference, and it is the field the
        /// riding test below is built on.
        /// </remarks>
        [Fact]
        public void SolidBodyRotationAcceleratesTowardTheAxis()
        {
            const double omega = 0.05d;

            var rng = new Rng(11UL);
            double worst = 0d;

            for (int i = 0; i < 200; i++)
            {
                float x = (float)(20d * (rng.NextFloat() - 0.5d));
                float z = (float)(20d * (rng.NextFloat() - 0.5d));
                float y = (float)(-60d * rng.NextFloat());

                Float3 got = CurrentField.MaterialDerivative(
                    (px, py, pz, pt) => Rotating(px, py, pz, omega), x, y, z, 900d);

                var want = new Float3(
                    (float)(-omega * omega * x), 0f, (float)(-omega * omega * z));

                double error = (got - want).Magnitude;
                double magnitude = want.Magnitude;

                if (magnitude > 1e-6d && error / magnitude > worst) worst = error / magnitude;
            }

            _output.WriteLine(
                $"solid-body rotation at {omega} rad/s: worst relative error against −Ω²r over " +
                $"200 points, {worst:0.0000000%}");

            Assert.True(worst < 1e-4d, FormattableString.Invariant(
                $"the stencil is off the exact centripetal acceleration by {worst:P4}."));
        }

        /// <summary>Solid-body rotation about the y axis at <paramref name="omega"/> rad/s.</summary>
        private static Float3 Rotating(float x, float y, float z, double omega) =>
            new Float3((float)(-omega * z), 0f, (float)(omega * x));

        /// <summary>
        /// A drag-coupled body in a circular current follows the circle with the term on, and
        /// spirals out of a tank's worth of water without it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is the whole reason the term was built, in one body and one closed-form
        /// field.</b> A body tied to the water by drag alone cannot turn as sharply as the water
        /// does: expand <c>v ≈ u − τ·Du/Dt</c> and the leftover is an outward drift of
        /// <c>τ·u_θ²/r = τΩ²r</c> per second, which compounds — 5 m becomes 115 m over ten turns
        /// at the numbers below, and in a walled tank it is the whole population against the glass
        /// (<c>StreamsTests</c> has the identity and the readings). Add the water's own
        /// acceleration and the first-order slip cancels exactly: <c>v = u</c> solves the
        /// equation, so the body is a tracer to first order in its response time and what is left
        /// is <c>O(τ²)</c>.
        /// </para>
        /// <para>
        /// <b>Started at rest rather than at the water's velocity</b>, because <c>v = u</c> is an
        /// exact solution and starting there would assert that an exact solution stays exact,
        /// which is a statement about the integrator. From rest the body has to be gathered onto
        /// the circle by the drag it is coupled through, and the radius is read over the last nine
        /// turns so the first turn's transient is not counted.
        /// </para>
        /// <para>
        /// <b>The body is a real one and the force goes through the real function.</b> A 0.2 m
        /// cube of neutrally buoyant tissue — 0.008 m³, 8 kg — with added mass at 1, so its
        /// effective mass is 16 kg (<see cref="FluidModel.EffectiveMass"/>) and the force is
        /// <see cref="FluidModel.AccelerationForce"/>'s <c>ρV(1 + Ca)·Du/Dt</c> = 16·Du/Dt: the
        /// displaced mass and the added mass, as the Morison term has it. The drag is linearised
        /// to a response time of 1 s rather than integrated quadratically, which is
        /// <c>StreamsTests.Tracers</c>' assumption and about what §5.2's drag gives a
        /// founder-scale part in this water; the quadratic version is PhysX's job and not a Core
        /// test's.
        /// </para>
        /// </remarks>
        [Fact]
        public void ANeutralBodyRidesTheWatersCircle()
        {
            const double omega = 0.05d;         // one turn in 126 s
            const double radius = 5d;           // the tank's own scale
            const double tau = 1d;              // the assumed drag response, see the remarks
            const double turns = 10d;
            const double step = 0.02d;

            var config = new FluidConfig
            {
                Density = 1000f,
                AddedMassCoefficient = 1f,
                FluidAccelerationCoefficient = 1f,
            };

            const float volume = 0.008f;
            float mass = config.Density * volume;                      // neutrally buoyant
            float effective = FluidModel.EffectiveMass(mass, volume, config);

            Assert.Equal(16f, effective, 4);

            (double last, double all) riding = Ride(1f);
            (double last, double all) dragOnly = Ride(0f);

            _output.WriteLine(
                $"a {tau} s body in a {omega} rad/s current at {radius} m, over {turns} turns: " +
                $"with the fluid acceleration at 1 the radius stays within " +
                $"{riding.last / radius - 1d:+0.00%;-0.00%} over the last nine turns " +
                $"({riding.all / radius - 1d:+0.00%;-0.00%} over all ten); at 0 it " +
                $"reaches {dragOnly.last:0.0} m, {dragOnly.last / radius:0.0}x its own radius");

            // The spec's band: the water's circle to within 5% of the radius.
            Assert.InRange(riding.last, 0.95d * radius, 1.05d * radius);

            // And the control, which is the failure the term exists to remove. It leaves the
            // tank's water entirely — the assertion is deliberately weak, because the number
            // itself (23x) is not the point and would move with any of the three knobs above.
            Assert.True(
                dragOnly.last > 2d * radius,
                FormattableString.Invariant(
                    $"drag alone should carry the body off its circle; it reached {dragOnly.last} m."));

            // Returns the mean radius over the last nine turns and over all ten.
            (double Last, double All) Ride(float coefficient)
            {
                var fluid = config.Clone();
                fluid.FluidAccelerationCoefficient = coefficient;

                double x = radius, z = 0d, vx = 0d, vz = 0d;
                double seconds = turns * 2d * Math.PI / omega;
                int steps = (int)(seconds / step);
                int after = (int)(steps / turns);

                double sumLast = 0d, sumAll = 0d;
                int countLast = 0;

                for (int n = 0; n < steps; n++)
                {
                    // Midpoint, for StreamsTests.Tracers' reason: this measures whether a body
                    // stays on a circle, and forward Euler loses a circle to its own truncation.
                    (double ax, double az) = Slope(x, z, vx, vz);

                    double mx = x + vx * step / 2d;
                    double mz = z + vz * step / 2d;
                    double mvx = vx + ax * step / 2d;
                    double mvz = vz + az * step / 2d;

                    (double bx, double bz) = Slope(mx, mz, mvx, mvz);

                    x += mvx * step;
                    z += mvz * step;
                    vx += bx * step;
                    vz += bz * step;

                    double r = Math.Sqrt(x * x + z * z);
                    sumAll += r;
                    if (n >= after)
                    {
                        sumLast += r;
                        countLast++;
                    }
                }

                return (sumLast / countLast, sumAll / steps);

                (double X, double Z) Slope(double px, double pz, double pvx, double pvz)
                {
                    Float3 u = Rotating((float)px, 0f, (float)pz, omega);

                    Float3 acceleration = CurrentField.MaterialDerivative(
                        (qx, qy, qz, qt) => Rotating(qx, qy, qz, omega),
                        (float)px, 0f, (float)pz, 0d);

                    Float3 force = FluidModel.AccelerationForce(acceleration, volume, fluid);

                    return (
                        (u.X - pvx) / tau + force.X / effective,
                        (u.Z - pvz) / tau + force.Z / effective);
                }
            }
        }

        /// <summary>
        /// At the default coefficient the force is exactly zero, and at 1 it is the displaced
        /// mass plus the added mass times the water's acceleration.
        /// </summary>
        /// <remarks>
        /// The first half is what lets every world on file replay: <c>FluidEnvironment</c> skips
        /// the sampling entirely at 0, and this says the force it would have added is zero to the
        /// bit rather than small — a run reproduced "to within a rounding error" is not reproduced
        /// at all in a world where one capped drag impulse is a different realisation (CLAUDE.md's
        /// butterfly rule).
        /// </remarks>
        [Fact]
        public void TheForceIsZeroAtTheDefaultAndTheMorisonTermAtOne()
        {
            var acceleration = new Float3(3f, -4f, 12f);   // 13 m/s², absurd on purpose
            const float volume = 0.008f;

            var off = new FluidConfig { AddedMassCoefficient = 1f };

            Assert.Equal(0f, off.FluidAccelerationCoefficient);
            Assert.Equal(Float3.Zero, FluidModel.AccelerationForce(acceleration, volume, off));

            var on = new FluidConfig
            {
                AddedMassCoefficient = 1f,
                FluidAccelerationCoefficient = 1f,
            };

            Float3 force = FluidModel.AccelerationForce(acceleration, volume, on);

            // rho*V*(1 + Ca) = 1000 * 0.008 * 2 = 16 kg, which is EffectiveMass of a neutral part.
            Assert.Equal(16f * 3f, force.X, 4);
            Assert.Equal(16f * -4f, force.Y, 4);
            Assert.Equal(16f * 12f, force.Z, 4);

            // Half the coefficient, half the force: linear in the dial, so a round can carry part
            // of the physics if the owner ever wants to.
            var half = new FluidConfig
            {
                AddedMassCoefficient = 1f,
                FluidAccelerationCoefficient = 0.5f,
            };

            Assert.Equal(
                force.Magnitude / 2f,
                FluidModel.AccelerationForce(acceleration, volume, half).Magnitude,
                3);
        }

        /// <summary>
        /// The vertical acceleration is exactly zero at the waterline, above it and at the bed —
        /// which is why D050's clamp on this force never binds.
        /// </summary>
        /// <remarks>
        /// <b>Not a clamp but a consequence, and worth asserting for that reason.</b> Both fields
        /// set the vertical velocity to exactly zero on the surface and the bed and clamp a point
        /// outside the box to the nearest face, so on that whole plane <c>u_y</c> is zero and so is
        /// every horizontal derivative of it; the only surviving term of <c>Du/Dt|_y</c> carries
        /// <c>u_y</c> as a factor. So the water cannot push a body up through the waterline
        /// however fast it is turning underneath, and <c>FluidEnvironment</c>'s guard is there
        /// against the next field rather than against this one.
        /// </remarks>
        [Fact]
        public void TheWaterDoesNotAccelerateABodyThroughTheSurfaceOrTheBed()
        {
            foreach ((string what, CurrentField field, bool tank) in
                     new[] { ("the box", Box(), false), ("the tank", Tank(), true) })
            {
                var rng = new Rng(23UL);

                for (int i = 0; i < 200; i++)
                {
                    (float x, float _, float z) = tank ? InTank(rng, 0.2f) : InBox(rng, 0f);
                    double t = 500d * rng.NextFloat();

                    foreach (float y in new[] { 0f, 0.5f, 12f, -Depth })
                    {
                        Float3 a = field.AccelerationAt(x, y, z, t);

                        Assert.True(a.IsFinite, $"{what}: Du/Dt is not finite at y = {y}.");
                        Assert.Equal(0f, a.Y);
                    }
                }

                _output.WriteLine(
                    $"{what}: the vertical Du/Dt is exactly 0 at y = 0, +0.5, +12 and −{Depth} m " +
                    "over 200 places and times");
            }
        }

        /// <summary>
        /// What the term costs to sample, printed rather than asserted — the farm pays it per part
        /// per physics step.
        /// </summary>
        /// <remarks>
        /// <b>A reading, not a guard.</b> Nine field samples and three instants' trigonometry per
        /// call is the arithmetic; what that comes to against a plain velocity sample is a fact
        /// the caller needs before running a round on the term, and a wall-clock number in a test
        /// is too noisy to assert on. The instant memo is what keeps the ratio near the sample
        /// count rather than at three times it (<c>CurrentField.EnsureInstant</c>).
        /// </remarks>
        [Fact]
        public void WhatTheTermCostsToSample()
        {
            CurrentField field = Tank();
            var rng = new Rng(5UL);

            const int Calls = 40000;
            var xs = new float[Calls];
            var ys = new float[Calls];
            var zs = new float[Calls];

            for (int i = 0; i < Calls; i++)
            {
                (xs[i], ys[i], zs[i]) = InTank(rng, 0.1f);
            }

            // Warm: the streams are built lazily and the first sample would carry the whole
            // construction.
            field.VelocityAt(xs[0], ys[0], zs[0], 1d);

            double sink = 0d;

            var clock = Stopwatch.StartNew();
            for (int i = 0; i < Calls; i++) sink += field.VelocityAt(xs[i], ys[i], zs[i], 1d).X;
            double velocity = clock.Elapsed.TotalSeconds;

            clock.Restart();
            for (int i = 0; i < Calls; i++) sink += field.AccelerationAt(xs[i], ys[i], zs[i], 1d).X;
            double acceleration = clock.Elapsed.TotalSeconds;

            _output.WriteLine(
                $"{Calls:n0} samples of the tank's streams at one instant: " +
                $"VelocityAt {velocity * 1e9 / Calls:0} ns each, AccelerationAt " +
                $"{acceleration * 1e9 / Calls:0} ns each, a factor of " +
                $"{acceleration / velocity:0.0} (the stencil is nine samples). " +
                $"Sum {sink:0.000} so the loops cannot be optimised away.");

            Assert.True(acceleration > 0d);
        }

        /// <summary>A point inside the box, <paramref name="inset"/> in from the surface and bed.</summary>
        private static (float X, float Y, float Z) InBox(Rng rng, float inset)
        {
            float width = (float)Math.Sqrt(Area / Patches);

            return (
                width * Patches * rng.NextFloat(),
                -inset - (Depth - 2f * inset) * rng.NextFloat(),
                width * rng.NextFloat());
        }

        /// <summary>A point inside the tank, <paramref name="inset"/> in from every face.</summary>
        private static (float X, float Y, float Z) InTank(Rng rng, float inset)
        {
            float r = (float)((TankRadius - inset) * Math.Sqrt(rng.NextFloat()));
            double theta = 2d * Math.PI * rng.NextFloat();

            return (
                (float)(TankRadius + r * Math.Cos(theta)),
                -inset - (Depth - 2f * inset) * rng.NextFloat(),
                (float)(TankRadius + r * Math.Sin(theta)));
        }
    }
}
